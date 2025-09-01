
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MicRouteSwitch
{
    public struct KeyboardEvent
    {
        public Keys Key { get; init; }
        public bool IsDown { get; init; }
        public bool Shift { get; init; }
        public bool Ctrl { get; init; }
        public bool Alt { get; init; }
        public bool Win { get; init; }
    }

    public static class GlobalKeyboardHook
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private static LowLevelKeyboardProc? _proc;
        private static IntPtr _hookID = IntPtr.Zero;

        public static event Action<KeyboardEvent>? KeyChanged;

        public static void Hook()
        {
            _proc = HookCallback;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule!;
            _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
        }

        public static void Unhook()
        {
            if (_hookID != IntPtr.Zero) UnhookWindowsHookEx(_hookID);
            _hookID = IntPtr.Zero;
            _proc = null;
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                bool isDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
                bool isUp = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;
                if (isDown || isUp)
                {
                    var e = new KeyboardEvent
                    {
                        Key = (Keys)kb.vkCode,
                        IsDown = isDown,
                        Shift = IsDown(VK_SHIFT) || IsDown(VK_LSHIFT) || IsDown(VK_RSHIFT),
                        Ctrl = IsDown(VK_CONTROL) || IsDown(VK_LCONTROL) || IsDown(VK_RCONTROL),
                        Alt = IsDown(VK_MENU) || IsDown(VK_LMENU) || IsDown(VK_RMENU),
                        Win = IsDown(VK_LWIN) || IsDown(VK_RWIN)
                    };
                    KeyChanged?.Invoke(e);
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static bool IsDown(int vk)
        {
            short s = GetKeyState(vk);
            return (s & 0x8000) != 0;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LMENU = 0xA4;
        private const int VK_RMENU = 0xA5;
    }
}
