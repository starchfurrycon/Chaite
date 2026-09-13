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
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class DesktopHost
{
    private const uint DesktopAccess = 0x0001 | 0x0002 | 0x0040 | 0x0080;
    private const uint JobKillOnClose = 0x2000;
    private const uint WaitObject0 = 0;
    private const uint WaitTimeout = 0x102;
    private static TextWriter Log;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--probe-child") return RunProbe();
        IntPtr desktop = IntPtr.Zero, job = IntPtr.Zero, process = IntPtr.Zero;
        PinSet pins = null;
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
            var probeChild = arguments == "--probe-child";
            if (probeChild)
            {
                var self = Path.GetFullPath(Process.GetCurrentProcess().MainModule.FileName);
                if (!string.Equals(executable, self, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(working.TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(self).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The harmless probe child must be this exact DesktopHost executable in its own directory.");
            }
            else
            {
                var expectedTarget = Path.GetFullPath(Path.Combine(working, "Terraria.exe"));
                if (!string.Equals(executable, expectedTarget, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("A pinned launch target must be working\\Terraria.exe.");
                var pinPlan = WorkspacePath(Required(options, "pinplan"), root, true);
                var pinSha256 = Required(options, "pinsha256");
                var launchBinding = WorkspacePath(Required(options, "binding"), root, true);
                var launchBindingSha256 = Required(options, "bindingsha256");
                var expectedPinPlan = Path.GetFullPath(Path.Combine(output, "launch-pin-plan.txt"));
                var expectedLaunchBinding = Path.GetFullPath(Path.Combine(output, "launch-binding.json"));
                if (!string.Equals(pinPlan, expectedPinPlan, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(launchBinding, expectedLaunchBinding, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Launch control files must be the exact files in the host output directory.");
                pins = OpenAndVerifyPins(pinPlan, pinSha256, launchBinding, launchBindingSha256, working, root);
                Write("Verified and locked launch pin plan plus " + pins.Files.Count + " prepared files through child exit.");
            }
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
                if (wait == WaitObject0) break;
                Check(wait == WaitTimeout, "WaitForSingleObject");
                if (elapsed.Elapsed.TotalSeconds >= timeout)
                {
                    StopAndDrainJob(job, process, 124, "timeout");
                    VerifyPinnedHandles(pins);
                    Write("TIMEOUT: terminated only isolated job after " + timeout + " seconds.");
                    return 124;
                }
            }
            uint exitCode;
            Check(GetExitCodeProcess(process, out exitCode), "GetExitCodeProcess");
            if (!WaitForJobEmpty(job, 1000))
            {
                StopAndDrainJob(job, process, 93, "descendant cleanup");
                throw new InvalidOperationException("The root process exited while a descendant remained in the isolated job.");
            }
            VerifyPinnedHandles(pins);
            Write("Child exit=" + exitCode + "; elapsedMs=" + elapsed.ElapsedMilliseconds + "; maximum isolated desktop windows=" + maximumWindows);
            Write("Input desktop unchanged; no test process took foreground; foreground changes from normal user activity=" + foregroundChanges);
            if (arguments == "--probe-child" && maximumWindows == 0) throw new InvalidOperationException("Probe did not demonstrate a created window.");
            if (pins != null)
            {
                WriteHostCompletion(output, pins, exitCode);
                Write("Host lock completion recorded after root signal, job empty, and pinned-handle revalidation.");
            }
            return exitCode > int.MaxValue ? 1 : (int)exitCode;
        }
        catch (Exception ex)
        {
            if (Log != null) Write("FAIL " + ex);
            else Console.Error.WriteLine(ex);
            try { StopAndDrainJob(job, process, 92, "failure cleanup"); }
            catch (Exception cleanupError)
            {
                if (Log != null) Write("FAIL cleanup " + cleanupError);
                else Console.Error.WriteLine(cleanupError);
            }
            return 1;
        }
        finally
        {
            Close(job); // KILL_ON_JOB_CLOSE contains any still-running descendants.
            Close(process);
            if (desktop != IntPtr.Zero) CloseDesktop(desktop);
            if (pins != null) pins.Dispose();
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

    private sealed class PinnedFile : IDisposable
    {
        internal string RelativePath;
        internal string ExpectedSha256;
        internal FileStream Stream;
        public void Dispose() { if (Stream != null) Stream.Dispose(); }
    }
    private sealed class PinSet : IDisposable
    {
        internal FileStream PlanStream;
        internal string PlanSha256;
        internal FileStream LaunchBindingStream;
        internal string LaunchBindingSha256;
        internal string RunId;
        internal readonly List<PinnedFile> Files = new List<PinnedFile>();
        public void Dispose()
        {
            foreach (var file in Files) file.Dispose();
            if (LaunchBindingStream != null) LaunchBindingStream.Dispose();
            if (PlanStream != null) PlanStream.Dispose();
        }
    }
    private static PinSet OpenAndVerifyPins(string planPath, string expectedPlanSha256, string launchBindingPath,
        string expectedLaunchBindingSha256, string working, string root)
    {
        ValidateSha256(expectedPlanSha256, "pin plan");
        ValidateSha256(expectedLaunchBindingSha256, "launch binding");
        var result = new PinSet();
        try
        {
            result.PlanStream = new FileStream(planPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
            if (result.PlanStream.Length < 64 || result.PlanStream.Length > 64L * 1024L * 1024L)
                throw new InvalidDataException("Launch pin plan has an invalid bounded size.");
            result.PlanSha256 = HashStream(result.PlanStream);
            if (!string.Equals(result.PlanSha256, expectedPlanSha256, StringComparison.Ordinal))
                throw new InvalidDataException("Launch pin plan hash changed.");
            result.PlanStream.Position = 0;
            string text;
            using (var reader = new StreamReader(result.PlanStream, new UTF8Encoding(false, true), false, 4096, true)) text = reader.ReadToEnd();
            var lines = text.Split(new[] { '\n' }, StringSplitOptions.None);
            if (lines.Length < 4 || lines[lines.Length - 1] != string.Empty || lines[0] != "CHAITE-LAUNCH-PINS/V1")
                throw new InvalidDataException("Invalid launch pin plan envelope.");
            if (lines[1].EndsWith("\r", StringComparison.Ordinal) || lines[2].EndsWith("\r", StringComparison.Ordinal))
                throw new InvalidDataException("Launch pin plan must use canonical LF separators.");
            var runIdParts = lines[1].Split('\t');
            Guid runId;
            if (runIdParts.Length != 2 || runIdParts[0] != "RUNID" || !Guid.TryParseExact(runIdParts[1], "D", out runId) || runId == Guid.Empty)
                throw new InvalidDataException("Invalid launch pin RunId.");
            result.RunId = runIdParts[1];
            var countParts = lines[2].Split('\t');
            int count;
            if (countParts.Length != 2 || countParts[0] != "COUNT" || !int.TryParse(countParts[1], NumberStyles.None, CultureInfo.InvariantCulture, out count) || count < 1 || count > 1000000 || lines.Length != count + 4)
                throw new InvalidDataException("Invalid launch pin count.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < count; index++)
            {
                var line = lines[index + 3];
                if (line.EndsWith("\r", StringComparison.Ordinal)) throw new InvalidDataException("Launch pin plan must use canonical LF separators.");
                var parts = line.Split('\t');
                if (parts.Length != 2) throw new InvalidDataException("Invalid launch pin row.");
                ValidateSha256(parts[0], "prepared file");
                string relative;
                try { relative = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(parts[1])); }
                catch (Exception ex) { throw new InvalidDataException("Invalid launch pin path encoding.", ex); }
                ValidateRelativePinPath(relative);
                if (!seen.Add(relative)) throw new InvalidDataException("Duplicate launch pin path.");
                var full = WorkspacePath(Path.Combine(working, relative.Replace('/', Path.DirectorySeparatorChar)), root, true);
                var file = new PinnedFile { RelativePath = relative, ExpectedSha256 = parts[0] };
                file.Stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
                var actual = HashStream(file.Stream);
                if (!string.Equals(actual, file.ExpectedSha256, StringComparison.Ordinal))
                {
                    file.Dispose();
                    throw new InvalidDataException("Prepared launch file changed: " + relative);
                }
                result.Files.Add(file);
            }
            if (!seen.Contains("Terraria.exe") || !seen.Contains("Chaite.DesktopHost.exe") ||
                !seen.Contains("probe-manifest.json") || !seen.Contains("probe-static-evidence.json"))
                throw new InvalidDataException("Launch pin plan omitted a mandatory executable or control file.");
            result.LaunchBindingStream = new FileStream(launchBindingPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
            if (result.LaunchBindingStream.Length < 64 || result.LaunchBindingStream.Length > 64L * 1024L)
                throw new InvalidDataException("Launch binding has an invalid bounded size.");
            result.LaunchBindingSha256 = HashStream(result.LaunchBindingStream);
            if (!string.Equals(result.LaunchBindingSha256, expectedLaunchBindingSha256, StringComparison.Ordinal))
                throw new InvalidDataException("Launch binding hash changed.");
            return result;
        }
        catch { result.Dispose(); throw; }
    }
    private static void VerifyPinnedHandles(PinSet pins)
    {
        if (pins == null) return;
        if (!string.Equals(HashStream(pins.PlanStream), pins.PlanSha256, StringComparison.Ordinal))
            throw new InvalidDataException("Launch pin plan changed while the child ran.");
        if (!string.Equals(HashStream(pins.LaunchBindingStream), pins.LaunchBindingSha256, StringComparison.Ordinal))
            throw new InvalidDataException("Launch binding changed while the child ran.");
        foreach (var file in pins.Files)
            if (!string.Equals(HashStream(file.Stream), file.ExpectedSha256, StringComparison.Ordinal))
                throw new InvalidDataException("Prepared launch file changed while the child ran: " + file.RelativePath);
    }
    private static uint ActiveJobProcesses(IntPtr job)
    {
        var accounting = new JobBasicAccountingInformation();
        Check(QueryInformationJobObject(job, 1, out accounting, (uint)Marshal.SizeOf(typeof(JobBasicAccountingInformation)), IntPtr.Zero),
            "QueryInformationJobObject");
        return accounting.ActiveProcesses;
    }
    private static bool WaitForJobEmpty(IntPtr job, int milliseconds)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            if (ActiveJobProcesses(job) == 0) return true;
            if (elapsed.ElapsedMilliseconds >= milliseconds) return false;
            Thread.Sleep(10);
        }
    }
    private static void StopAndDrainJob(IntPtr job, IntPtr process, uint exitCode, string context)
    {
        if (job == IntPtr.Zero) return;
        Check(TerminateJobObject(job, exitCode), "TerminateJobObject " + context);
        if (process != IntPtr.Zero)
        {
            var rootWait = WaitForSingleObject(process, 5000);
            if (rootWait != WaitObject0)
                throw new InvalidOperationException("The root process did not signal after " + context + "; wait=" + rootWait + ".");
        }
        if (!WaitForJobEmpty(job, 5000))
            throw new InvalidOperationException("The isolated job did not become empty after " + context + ".");
    }
    private static string JsonString(string value)
    {
        var result = new StringBuilder(value.Length + 2).Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"': result.Append("\\\""); break;
                case '\\': result.Append("\\\\"); break;
                case '\b': result.Append("\\b"); break;
                case '\f': result.Append("\\f"); break;
                case '\n': result.Append("\\n"); break;
                case '\r': result.Append("\\r"); break;
                case '\t': result.Append("\\t"); break;
                default:
                    if (character < 0x20) result.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                    else result.Append(character);
                    break;
            }
        }
        return result.Append('"').ToString();
    }
    private static void WriteHostCompletion(string output, PinSet pins, uint childExitCode)
    {
        var finalPath = Path.Combine(output, "host-lock-completion.json");
        var temporaryPath = Path.Combine(output, "host-lock-completion." + Guid.NewGuid().ToString("N") + ".tmp");
        var json = new StringBuilder();
        json.Append("{\n")
            .Append("  \"Schema\": ").Append(JsonString("chaite-host-lock-completion/v1")).Append(",\n")
            .Append("  \"RunId\": ").Append(JsonString(pins.RunId)).Append(",\n")
            .Append("  \"CompletedUtc\": ").Append(JsonString(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture))).Append(",\n")
            .Append("  \"CompletionStatus\": ").Append(JsonString("child-exited-job-empty")).Append(",\n")
            .Append("  \"LockStrategy\": ").Append(JsonString("desktop-host-fileshare-read-through-job-empty")).Append(",\n")
            .Append("  \"LaunchBindingSha256\": ").Append(JsonString(pins.LaunchBindingSha256)).Append(",\n")
            .Append("  \"PinPlanSha256\": ").Append(JsonString(pins.PlanSha256)).Append(",\n")
            .Append("  \"PinnedPreparedFileCount\": ").Append(pins.Files.Count.ToString(CultureInfo.InvariantCulture)).Append(",\n")
            .Append("  \"ChildExitCode\": ").Append(childExitCode.ToString(CultureInfo.InvariantCulture)).Append(",\n")
            .Append("  \"RootProcessSignaled\": true,\n")
            .Append("  \"JobActiveProcesses\": 0,\n")
            .Append("  \"PinnedHandlesRevalidated\": true\n")
            .Append("}\n");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
            {
                writer.Write(json.ToString());
                writer.Flush();
                stream.Flush(true);
            }
            if (File.Exists(finalPath)) throw new IOException("Host lock completion already exists.");
            File.Move(temporaryPath, finalPath);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
    private static string HashStream(FileStream stream)
    {
        stream.Position = 0;
        byte[] hash;
        using (var sha = SHA256.Create()) hash = sha.ComputeHash(stream);
        stream.Position = 0;
        var text = new StringBuilder(64);
        foreach (var value in hash) text.Append(value.ToString("X2", CultureInfo.InvariantCulture));
        return text.ToString();
    }
    private static void ValidateSha256(string value, string name)
    {
        if (value == null || value.Length != 64) throw new InvalidDataException("Invalid " + name + " SHA256.");
        foreach (var character in value)
            if (!((character >= '0' && character <= '9') || (character >= 'A' && character <= 'F')))
                throw new InvalidDataException("Invalid " + name + " SHA256.");
    }
    private static void ValidateRelativePinPath(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.IndexOf('\\') >= 0 || relative.IndexOf(':') >= 0 || relative.StartsWith("/", StringComparison.Ordinal))
            throw new InvalidDataException("Unsafe launch pin path.");
        foreach (var segment in relative.Split('/'))
            if (segment.Length == 0 || segment == "." || segment == "..") throw new InvalidDataException("Unsafe launch pin path segment.");
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
    [StructLayout(LayoutKind.Sequential)] private struct JobBasicAccountingInformation
    {
        internal long TotalUserTime, TotalKernelTime, ThisPeriodTotalUserTime, ThisPeriodTotalKernelTime;
        internal uint TotalPageFaultCount, TotalProcesses, ActiveProcesses, TotalTerminatedProcesses;
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
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool QueryInformationJobObject(IntPtr job, int type, out JobBasicAccountingInformation info, uint length, IntPtr returnedLength);
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
