using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Hardcodet.Wpf.TaskbarNotification;
using KaqiaCore;

namespace KaqiaApp
{
    public partial class App : Application
    {
        private TaskbarIcon? _notifyIcon;
        private HwndSource? _hwndSource;
        private const int HOTKEY_ID = 9000;
        private static System.Threading.Mutex? _mutex = null;
        private AppConfig _config = new AppConfig();

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "KaqiaScreenshotTool_Mutex";
            bool createdNew;

            _mutex = new System.Threading.Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                Shutdown();
                return;
            }

            base.OnStartup(e);

            _config = ConfigManager.Load();

            // Create tray icon
            _notifyIcon = new TaskbarIcon();
            var iconUri = new Uri("pack://application:,,,/favicon.ico");
            var iconStream = Application.GetResourceStream(iconUri)?.Stream;
            if (iconStream != null) _notifyIcon.Icon = new System.Drawing.Icon(iconStream);
            else _notifyIcon.Icon = System.Drawing.SystemIcons.Information;

            _notifyIcon.ToolTipText = "Kaqia 截图工具";
            _notifyIcon.ContextMenu = (System.Windows.Controls.ContextMenu)FindResource("TrayMenu");

            // Setup hotkey listener
            var helper = new WindowInteropHelper(new Window { Width = 0, Height = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false });
            helper.EnsureHandle();
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource.AddHook(HwndHook);

            RegisterGlobalHotkey();
        }

        private void RegisterGlobalHotkey()
        {
            if (_hwndSource == null) return;
            
            // Unregister first
            HotkeyManager.Unregister(_hwndSource.Handle, HOTKEY_ID);

            if (string.IsNullOrEmpty(_config.HotkeyKey)) return;

            uint modifiers = 0;
            if (_config.HotkeyModifiers.Contains("Control")) modifiers |= HotkeyManager.MOD_CONTROL;
            if (_config.HotkeyModifiers.Contains("Alt")) modifiers |= HotkeyManager.MOD_ALT;
            if (_config.HotkeyModifiers.Contains("Shift")) modifiers |= HotkeyManager.MOD_SHIFT;
            if (_config.HotkeyModifiers.Contains("Windows")) modifiers |= HotkeyManager.MOD_WIN;

            try
            {
                // Convert string Key to Virtual Key code
                Key key = (Key)Enum.Parse(typeof(Key), _config.HotkeyKey);
                uint vKey = (uint)KeyInterop.VirtualKeyFromKey(key);
                HotkeyManager.Register(_hwndSource.Handle, HOTKEY_ID, modifiers, vKey);
            }
            catch (Exception ex)
            {
                MessageBox.Show("快捷键注册失败: " + ex.Message);
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                OnHotkeyTriggered();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OnHotkeyTriggered()
        {
            var screenshot = ScreenCapturer.CaptureAllScreens(out int pixelWidth, out int pixelHeight);
            var overlay = new MainWindow(screenshot, pixelWidth, pixelHeight);
            overlay.Show();
            overlay.Activate();
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow();
            if (win.ShowDialog() == true)
            {
                _config = ConfigManager.Load(); // Reload
                RegisterGlobalHotkey();
            }
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            _notifyIcon?.Dispose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_hwndSource != null)
            {
                HotkeyManager.Unregister(_hwndSource.Handle, HOTKEY_ID);
                _hwndSource.RemoveHook(HwndHook);
                _hwndSource.Dispose();
            }
            base.OnExit(e);
        }
    }
}
