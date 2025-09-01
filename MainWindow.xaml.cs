using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using NAudio.CoreAudioApi;
using System.Windows.Forms;
using System.Drawing;

namespace MicRouteSwitch
{
    public partial class MainWindow : Window
    {
        private readonly MMDeviceEnumerator _enum = new();
        private AudioRouter? _router;
        private bool _toggleStateB = false;
        private bool _capturingHotkey = false;
        private HotkeySetting _hotkey = new(); // default Alt
        private NotifyIcon trayIcon;

        private readonly string _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MicRouteSwitch", "settings.json");

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            RefreshDevices();
            UpdateHotkeyDisplay();
            SetupTray();

            GlobalKeyboardHook.Hook();
            GlobalKeyboardHook.KeyChanged += OnKeyChanged;
            this.Closed += (s, e) => GlobalKeyboardHook.Unhook();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var text = File.ReadAllText(_settingsPath);
                    var dto = JsonSerializer.Deserialize<SettingsDto>(text);
                    if (dto != null)
                    {
                        _hotkey.Key = dto.Key;
                        _hotkey.Modifiers = dto.Modifiers;
                        HoldModeCheck.IsChecked = dto.HoldMode;
                    }
                }
            }
            catch { /* ignore */ }
        }

        private void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
                var dto = new SettingsDto
                {
                    Key = _hotkey.Key,
                    Modifiers = _hotkey.Modifiers,
                    HoldMode = HoldModeCheck.IsChecked == true
                };
                File.WriteAllText(_settingsPath, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* ignore */ }
        }

        private void UpdateHotkeyDisplay()
        {
            HotkeyDisplay.Text = _hotkey.Display + (HoldModeCheck.IsChecked == true ? " (hold)" : " (toggle)");
        }

        private void RefreshDevices()
        {
            MicCombo.Items.Clear();
            CableACombo.Items.Clear();
            CableBCombo.Items.Clear();

            var mics = _enum.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
            foreach (var d in mics) MicCombo.Items.Add(new DeviceItem(d));
            if (MicCombo.Items.Count > 0) MicCombo.SelectedIndex = 0;

            var renders = _enum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                               .OrderBy(d => d.FriendlyName).ToList();
            var cables = renders.Where(d => d.FriendlyName.Contains("CABLE", StringComparison.OrdinalIgnoreCase)
                                          && d.FriendlyName.Contains("Input", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var d in cables)
            {
                CableACombo.Items.Add(new DeviceItem(d));
                CableBCombo.Items.Add(new DeviceItem(d));
            }
            if (CableACombo.Items.Count > 0) CableACombo.SelectedIndex = 0;
            if (CableBCombo.Items.Count > 0) CableBCombo.SelectedIndex = Math.Min(1, CableBCombo.Items.Count - 1);

            StatusText.Text = cables.Count < 1
                ? "VB-CABLE not found. Install from: https://vb-audio.com/Cable/ (need CABLE Input/Output A/B)."
                : $"Found {cables.Count} CABLE Input devices.";
        }

        private void OnKeyChanged(KeyboardEvent e)
        {
            if (_capturingHotkey)
            {
                if (e.IsDown)
                {
                    // Accept first keydown as hotkey selection (modifiers included)
                    _hotkey = new HotkeySetting
                    {
                        Key = e.Key,
                        Modifiers = (e.Ctrl ? HotkeyModifiers.Control : 0)
                                  | (e.Shift ? HotkeyModifiers.Shift : 0)
                                  | (e.Alt ? HotkeyModifiers.Alt : 0)
                                  | (e.Win ? HotkeyModifiers.Win : 0)
                    };
                    _capturingHotkey = false;
                    Dispatcher.Invoke(() =>
                    {
                        CaptureHint.Visibility = Visibility.Collapsed;
                        UpdateHotkeyDisplay();
                        SaveSettings();
                    });
                }
                return;
            }

            // Routing logic
            if (_router == null) return;

            bool matches = _hotkey.Matches(e);
            if (!matches) return;

            if (HoldModeCheck.IsChecked == true)
            {
                if (e.IsDown)
                {
                    _router.RouteToB = true;
                    Dispatcher.Invoke(() => StatusText.Text = "Routing: Application 2");
                }
                else
                {
                    _router.RouteToB = false;
                    Dispatcher.Invoke(() => StatusText.Text = "Routing: Application 1");
                }
            }
            else
            {
                // toggle on key down only
                if (e.IsDown)
                {
                    _toggleStateB = !_toggleStateB;
                    _router.RouteToB = _toggleStateB;
                    Dispatcher.Invoke(() => StatusText.Text = _toggleStateB ? "Routing: Application 2" : "Routing: Application 1");
                }
            }
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MicCombo.SelectedItem is not DeviceItem micItem ||
                CableACombo.SelectedItem is not DeviceItem aItem ||
                CableBCombo.SelectedItem is not DeviceItem bItem)
            {
                System.Windows.MessageBox.Show("Select your Mic, CABLE Input (A), and CABLE Input (B).");
                return;
            }

            try
            {
                _router = new AudioRouter(micItem.Device, aItem.Device, bItem.Device);
                _router.Start();
                StartBtn.IsEnabled = false;
                StopBtn.IsEnabled = true;
                RefreshBtn.IsEnabled = false;
                StatusText.Text = "Routing started. Use the hotkey to switch.";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Failed to start routing: " + ex.Message);
            }
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            _router?.Dispose();
            _router = null;
            StartBtn.IsEnabled = true;
            StopBtn.IsEnabled = false;
            RefreshBtn.IsEnabled = true;
            StatusText.Text = "Stopped.";
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e) => RefreshDevices();

        private void SetHotkeyBtn_Click(object sender, RoutedEventArgs e)
        {
            _capturingHotkey = true;
            CaptureHint.Visibility = Visibility.Visible;
            StatusText.Text = "Waiting for hotkey... press any key combination.";
        }

        private sealed class DeviceItem
        {
            public MMDevice Device { get; }
            public DeviceItem(MMDevice d) => Device = d;
            public override string ToString() => Device.FriendlyName;
        }

        private sealed class SettingsDto
        {
            public System.Windows.Forms.Keys Key { get; set; }
            public HotkeyModifiers Modifiers { get; set; }
            public bool HoldMode { get; set; }
        }

        private void SetupTray()
        {
            trayIcon = new NotifyIcon();
            trayIcon.Icon = new Icon("icon.ico"); // you can replace with a custom icon
            trayIcon.Text = "MicRouteSwitch";
            trayIcon.Visible = false;

            // Context menu for the tray icon
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Show", null, (s, e) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            });
            contextMenu.Items.Add("Exit", null, (s, e) =>
            {
                trayIcon.Visible = false;
                System.Windows.Application.Current.Shutdown();
            });

            trayIcon.ContextMenuStrip = contextMenu;

            // Minimize to tray
            StateChanged += (s, e) =>
            {
                if (TrayToggle.IsChecked == true && WindowState == WindowState.Minimized)
                {
                    Hide();
                    trayIcon.Visible = true;
                }
            };

            // Close to tray
            Closing += (s, e) =>
            {
                if (TrayToggle.IsChecked == true)
                {
                    e.Cancel = true;
                    Hide();
                    trayIcon.Visible = true;
                }
                else
                {
                    trayIcon.Visible = false;
                }
            };
        }

    }
}
