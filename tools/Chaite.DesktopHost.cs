// Isolated desktop test host. Never switches desktops or sends OS input.
// Build with Framework csc; no Terraria references. --probe-child is a harmless form.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class DesktopHost
{
    private const uint DesktopAccess = 0x0001 | 0x0002 | 0x0040 | 0x0080;
    private const uint JobKillOnClose = 0x2000;
    private static TextWriter Log;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--probe-child") return RunProbe();
        IntPtr desktop = IntPtr.Zero, job = IntPtr.Zero, process = IntPtr.Zero;
        try
        {
            var options = Parse(args);
            var root = Path.GetFullPath(Required(options, "root")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var executable = WorkspacePath(Required(options, "exe"), root, true);
            var output = WorkspacePath(Required(options, "output"), root, false);
            var working = WorkspacePath(Required(options, "working"), root, false);
            var timeout = int.Parse(Required(options, "timeout"), CultureInfo.InvariantCulture);
            if (timeout < 120 || timeout > 240) throw new ArgumentOutOfRangeException("timeout", "Timeout must be 120..240 seconds.");
            if (!Directory.Exists(output) || !Directory.Exists(working)) throw new DirectoryNotFoundException("Output and working directory must already exist.");
            var arguments = Encoding.UTF8.GetString(Convert.FromBase64String(Required(options, "args64")));
            Log = TextWriter.Synchronized(new StreamWriter(new FileStream(Path.Combine(output, "desktop-host.log"), FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true });
            var originalDesktop = DesktopName(GetThreadDesktop(GetCurrentThreadId()));
            var inputDesktop = InputDesktopName();
            if (ObjectName(GetProcessWindowStation()) != "WinSta0" || originalDesktop != inputDesktop)
                throw new InvalidOperationException("Host must start on the user's current interactive desktop.");
            var foregroundBefore = GetForegroundWindow();
            Write("Original desktop=" + originalDesktop + "; input=" + inputDesktop + "; foreground=" + foregroundBefore.ToInt64());
            Write("Target=" + executable + "; arguments deliberately omitted from log.");

            job = CreateJobObject(IntPtr.Zero, null);
            Check(job != IntPtr.Zero, "CreateJobObject");
            var limits = new JobExtendedLimitInformation();
            limits.BasicLimitInformation.LimitFlags = JobKillOnClose;
            Check(SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf(typeof(JobExtendedLimitInformation))), "SetInformationJobObject");
            var desktopName = "ChaiteTest_" + Guid.NewGuid().ToString("N");
            Exception launchError = null;
            IntPtr workerDesktop = IntPtr.Zero, workerProcess = IntPtr.Zero;
            var safeJob = job;
            // CreateDesktop associates the caller's thread with the new desktop.
            // Use a dedicated thread; the monitor stays on the original desktop.
            var launchThread = new Thread(delegate()
            {
                IntPtr stdout = IntPtr.Zero, stderr = IntPtr.Zero, stdin = IntPtr.Zero;
                ProcessInformation info = new ProcessInformation();
                var resumed = false;
                try
                {
                    workerDesktop = CreateDesktop(desktopName, null, IntPtr.Zero, 0, DesktopAccess, IntPtr.Zero);
                    Check(workerDesktop != IntPtr.Zero, "CreateDesktop");
                    if (InputDesktopName() != inputDesktop) throw new InvalidOperationException("Input desktop changed before launch.");
                    var attributes = new SecurityAttributes { Length = Marshal.SizeOf(typeof(SecurityAttributes)), InheritHandle = true };
                    stdout = CreateFile(Path.Combine(output, "child.stdout.log"), 0x40000000, 3, ref attributes, 1, 0x80, IntPtr.Zero);
                    stderr = CreateFile(Path.Combine(output, "child.stderr.log"), 0x40000000, 3, ref attributes, 1, 0x80, IntPtr.Zero);
                    stdin = CreateFile("NUL", 0x80000000, 3, ref attributes, 3, 0x80, IntPtr.Zero);
                    Check(stdout != new IntPtr(-1) && stderr != new IntPtr(-1) && stdin != new IntPtr(-1), "Create standard handle");
                    var startup = new StartupInfo { cb = Marshal.SizeOf(typeof(StartupInfo)), lpDesktop = "WinSta0\\" + desktopName,
                        dwFlags = 0x100 | 0x1, wShowWindow = 0, hStdInput = stdin, hStdOutput = stdout, hStdError = stderr };
                    var command = new StringBuilder(Quote(executable) + (arguments.Length == 0 ? string.Empty : " " + arguments));
                    // SUSPENDED | NO_WINDOW | BELOW_NORMAL_PRIORITY_CLASS.
                    Check(CreateProcess(executable, command, IntPtr.Zero, IntPtr.Zero, true, 0x4 | 0x08000000 | 0x4000, IntPtr.Zero, working, ref startup, out info), "CreateProcess");
                    Check(AssignProcessToJobObject(safeJob, info.hProcess), "AssignProcessToJobObject");
                    Write("Created suspended PID=" + info.dwProcessId + "; assigned to kill-on-close job; desktop=" + desktopName);
                    Check(ResumeThread(info.hThread) != uint.MaxValue, "ResumeThread");
                    resumed = true;
                    workerProcess = info.hProcess;
                    info.hProcess = IntPtr.Zero;
                }
                catch (Exception ex) { launchError = ex; }
                finally
                {
                    // If job assignment failed, this is the exact suspended child
                    // handle we just created, never a process discovered by name.
                    if (!resumed && info.hProcess != IntPtr.Zero) TerminateProcess(info.hProcess, 91);
                    Close(info.hProcess); Close(info.hThread); Close(stdout); Close(stderr); Close(stdin);
                }
            });
            launchThread.IsBackground = true;
            launchThread.Start();
            launchThread.Join();
            desktop = workerDesktop;
            process = workerProcess;
            if (launchError != null) throw new InvalidOperationException("Isolated launch failed.", launchError);
            if (DesktopName(GetThreadDesktop(GetCurrentThreadId())) != originalDesktop) throw new InvalidOperationException("Monitor thread desktop unexpectedly changed.");

            var elapsed = Stopwatch.StartNew();
            var foregroundChanges = 0;
            var previousForeground = foregroundBefore;
            var maximumWindows = 0;
            while (true)
            {
                if (InputDesktopName() != inputDesktop) throw new InvalidOperationException("User input desktop changed; stopping only this test job.");
                var foreground = GetForegroundWindow();
                if (foreground != previousForeground) { foregroundChanges++; previousForeground = foreground; }
                uint foregroundPid;
                GetWindowThreadProcessId(foreground, out foregroundPid);
                if (IsJobProcess(foregroundPid, job)) throw new InvalidOperationException("A test-job process became the user's foreground window.");
                var windowCount = 0;
                EnumDesktopWindows(desktop, delegate(IntPtr window, IntPtr parameter) { windowCount++; return true; }, IntPtr.Zero);
                maximumWindows = Math.Max(maximumWindows, windowCount);
                var wait = WaitForSingleObject(process, 100);
                if (wait == 0) break;
                Check(wait == 0x102, "WaitForSingleObject");
                if (elapsed.Elapsed.TotalSeconds >= timeout)
                {
                    Check(TerminateJobObject(job, 124), "TerminateJobObject timeout");
                    Write("TIMEOUT: terminated only isolated job after " + timeout + " seconds.");
                    return 124;
                }
            }
            uint exitCode;
            Check(GetExitCodeProcess(process, out exitCode), "GetExitCodeProcess");
            Write("Child exit=" + exitCode + "; elapsedMs=" + elapsed.ElapsedMilliseconds + "; maximum isolated desktop windows=" + maximumWindows);
            Write("Input desktop unchanged; no test process took foreground; foreground changes from normal user activity=" + foregroundChanges);
            if (arguments == "--probe-child" && maximumWindows == 0) throw new InvalidOperationException("Probe did not demonstrate a created window.");
            return exitCode > int.MaxValue ? 1 : (int)exitCode;
        }
        catch (Exception ex)
        {
            if (Log != null) Write("FAIL " + ex);
            else Console.Error.WriteLine(ex);
            if (job != IntPtr.Zero) TerminateJobObject(job, 92);
            return 1;
        }
        finally
        {
            Close(job); // KILL_ON_JOB_CLOSE contains any still-running descendants.
            Close(process);
            if (desktop != IntPtr.Zero) CloseDesktop(desktop);
            if (Log != null) Log.Dispose();
        }
    }

    private static int RunProbe()
    {
        var name = DesktopName(GetThreadDesktop(GetCurrentThreadId()));
        var input = InputDesktopName();
        if (!name.StartsWith("ChaiteTest_", StringComparison.Ordinal) || name == input)
        {
            Console.Error.WriteLine("Refusing probe UI outside isolated desktop.");
            return 2;
        }
        Console.WriteLine("Probe desktop=" + name + "; input desktop=" + input);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (var form = new Form { Text = "Chaite isolated desktop probe", ClientSize = new Size(420, 180) })
        using (var timer = new System.Windows.Forms.Timer { Interval = 2000 })
        {
            form.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "Harmless window on a private desktop.\nNo input, no Terraria, exits automatically.", TextAlign = ContentAlignment.MiddleCenter });
            form.Shown += delegate { Console.WriteLine("Probe window created: " + form.Handle.ToInt64()); timer.Start(); };
            timer.Tick += delegate { timer.Stop(); form.Close(); };
            Application.Run(form);
        }
        Console.WriteLine("PROBE PASS");
        return 0;
    }

    private static string WorkspacePath(string path, string root, bool file)
    {
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Target is outside this project: " + full);
        if (file && !File.Exists(full)) throw new FileNotFoundException("Target missing", full);
        for (var current = file ? Path.GetDirectoryName(full) : full; current != null && current.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase); current = Path.GetDirectoryName(current))
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Reparse-point directories are not allowed in isolated-test targets.");
        if (file && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Reparse-point executable is not allowed.");
        return full;
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (args.Length % 2 != 0) throw new ArgumentException("Expected --name value pairs.");
        for (var i = 0; i < args.Length; i += 2) result.Add(args[i].TrimStart('-'), args[i + 1]);
        return result;
    }
    private static string Required(Dictionary<string, string> options, string key) { string value; if (!options.TryGetValue(key, out value)) throw new ArgumentException("Missing " + key); return value; }
    private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
    private static void Write(string message) { Log.WriteLine(DateTime.UtcNow.ToString("o") + " " + message); }
    private static void Check(bool result, string operation) { if (!result) throw new Win32Exception(Marshal.GetLastWin32Error(), operation); }
    private static void Close(IntPtr handle) { if (handle != IntPtr.Zero && handle != new IntPtr(-1)) CloseHandle(handle); }
    private static string DesktopName(IntPtr handle) { if (handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Desktop handle"); return ObjectName(handle); }
    private static string ObjectName(IntPtr handle)
    {
        var text = new StringBuilder(512); uint needed;
        Check(GetUserObjectInformation(handle, 2, text, (uint)(text.Capacity * 2), out needed), "GetUserObjectInformation");
        return text.ToString();
    }
    private static string InputDesktopName()
    {
        var handle = OpenInputDesktop(0, false, 1);
        Check(handle != IntPtr.Zero, "OpenInputDesktop");
        try { return ObjectName(handle); } finally { CloseDesktop(handle); }
    }
    private static bool IsJobProcess(uint pid, IntPtr job)
    {
        if (pid == 0) return false;
        var handle = OpenProcess(0x1000, false, pid);
        if (handle == IntPtr.Zero) return false;
        try { bool inJob; return IsProcessInJob(handle, job, out inJob) && inJob; }
        finally { Close(handle); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { internal int Length; internal IntPtr SecurityDescriptor; [MarshalAs(UnmanagedType.Bool)] internal bool InheritHandle; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfo
    {
        internal int cb; internal string lpReserved, lpDesktop, lpTitle;
        internal uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        internal ushort wShowWindow, cbReserved2; internal IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { internal IntPtr hProcess, hThread; internal uint dwProcessId, dwThreadId; }
    [StructLayout(LayoutKind.Sequential)] private struct JobBasicLimitInformation
    {
        internal long PerProcessUserTimeLimit, PerJobUserTimeLimit; internal uint LimitFlags;
        internal UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize; internal uint ActiveProcessLimit; internal UIntPtr Affinity;
        internal uint PriorityClass, SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters { internal ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }
    [StructLayout(LayoutKind.Sequential)] private struct JobExtendedLimitInformation
    {
        internal JobBasicLimitInformation BasicLimitInformation; internal IoCounters IoInfo;
        internal UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }
    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll", EntryPoint = "CreateDesktopW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateDesktop(string name, string device, IntPtr devmode, uint flags, uint access, IntPtr attributes);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetThreadDesktop(uint thread);
    [DllImport("user32.dll")] private static extern IntPtr GetProcessWindowStation();
    [DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder value, uint length, out uint needed);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EnumDesktopWindows(IntPtr desktop, EnumWindowsCallback callback, IntPtr parameter);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateJobObject(IntPtr attributes, string name);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(IntPtr job, int type, ref JobExtendedLimitInformation info, uint length);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool IsProcessInJob(IntPtr process, IntPtr job, out bool result);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateJobObject(IntPtr job, uint code);
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInformation info);
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateFile(string path, uint access, uint share, ref SecurityAttributes attributes, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(IntPtr process, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
}
