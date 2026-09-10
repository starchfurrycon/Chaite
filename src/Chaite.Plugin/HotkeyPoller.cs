using System;
using System.Runtime.InteropServices;

namespace Chaite.Plugin
{
    internal sealed class HotkeyPoller
    {
        private readonly int _activate;
        private readonly int _cancel;
        private bool _activateWasDown;
        private bool _cancelWasDown;
        private readonly uint _processId = GetCurrentProcessId();

        public HotkeyPoller(string activate, string cancel)
        {
            _activate = ParseVirtualKey(activate, 0x77); // F8
            _cancel = ParseVirtualKey(cancel, 0x78);     // F9
        }

        public HotkeyEdges Poll()
        {
            var activateDown = (GetAsyncKeyState(_activate) & 0x8000) != 0;
            var cancelDown = (GetAsyncKeyState(_cancel) & 0x8000) != 0;
            return Sample(IsGameForeground(), activateDown, cancelDown);
        }

        // Pure edge detection is also used by offline tests, without injecting keys.
        internal HotkeyEdges Sample(bool foreground, bool activateDown, bool cancelDown)
        {
            var edges = new HotkeyEdges
            {
                ActivatePressed = foreground && !cancelDown && activateDown && !_activateWasDown,
                CancelPressed = cancelDown && !_cancelWasDown
            };
            _activateWasDown = activateDown;
            _cancelWasDown = cancelDown;
            return edges;
        }

        private bool IsGameForeground()
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero) return false;
            uint owner;
            GetWindowThreadProcessId(window, out owner);
            return owner == _processId;
        }

        private static int ParseVirtualKey(string key, int fallback)
        {
            if (string.IsNullOrWhiteSpace(key))
                return fallback;
            var text = key.Trim().ToUpperInvariant();
            if (text.Length == 1 && text[0] >= 'A' && text[0] <= 'Z')
                return text[0];
            if (text.Length == 1 && text[0] >= '0' && text[0] <= '9')
                return text[0];
            if (text.StartsWith("F") && int.TryParse(text.Substring(1), out var function) && function >= 1 && function <= 24)
                return 0x70 + function - 1;
            return fallback;
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();
    }

    internal struct HotkeyEdges
    {
        public bool ActivatePressed;
        public bool CancelPressed;
    }
}
