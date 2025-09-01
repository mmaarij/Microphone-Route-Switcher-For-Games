
using System;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace MicRouteSwitch
{
    [Flags]
    public enum HotkeyModifiers
    {
        None = 0,
        Control = 1,
        Shift = 2,
        Alt = 4,
        Win = 8
    }

    public sealed class HotkeySetting
    {
        public Keys Key { get; set; } = Keys.Menu; // default Alt
        public HotkeyModifiers Modifiers { get; set; } = HotkeyModifiers.None;

        [JsonIgnore]
        public string Display =>
            (Modifiers != HotkeyModifiers.None ? $"{Modifiers}+" : "") + KeyToString(Key);

        private static string KeyToString(Keys k)
        {
            return k switch
            {
                Keys.Menu => "Alt",
                Keys.LMenu => "Left Alt",
                Keys.RMenu => "Right Alt",
                Keys.ControlKey => "Ctrl",
                Keys.ShiftKey => "Shift",
                Keys.LWin => "Left Win",
                Keys.RWin => "Right Win",
                _ => k.ToString()
            };
        }

        public bool Matches(KeyboardEvent e)
        {
            // Treat Left/Right Alt/Ctrl/Shift as their generic variants when matching
            bool keyMatch = e.Key == Key
                || (Key == Keys.Menu && (e.Key == Keys.Menu || e.Key == Keys.LMenu || e.Key == Keys.RMenu))
                || (Key == Keys.ControlKey && (e.Key == Keys.ControlKey || e.Key == Keys.LControlKey || e.Key == Keys.RControlKey))
                || (Key == Keys.ShiftKey && (e.Key == Keys.ShiftKey || e.Key == Keys.LShiftKey || e.Key == Keys.RShiftKey))
                || (Key == Keys.LWin && e.Key == Keys.LWin)
                || (Key == Keys.RWin && e.Key == Keys.RWin);

            if (!keyMatch) return FalseWithDebug("Key mismatch", e);

            // All required modifiers must be present; extra modifiers are allowed
            if (Modifiers.HasFlag(HotkeyModifiers.Control) && !e.Ctrl) return FalseWithDebug("Ctrl required", e);
            if (Modifiers.HasFlag(HotkeyModifiers.Shift) && !e.Shift) return FalseWithDebug("Shift required", e);
            if (Modifiers.HasFlag(HotkeyModifiers.Alt) && !e.Alt) return FalseWithDebug("Alt required", e);
            if (Modifiers.HasFlag(HotkeyModifiers.Win) && !e.Win) return FalseWithDebug("Win required", e);

            return true;
        }

        private bool FalseWithDebug(string reason, KeyboardEvent e) => false;
    }
}
