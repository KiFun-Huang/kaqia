using System;
using System.Windows;
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

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "KaqiaScreenshotTool_Mutex";
            bool createdNew;

            _mutex = new System.Threading.Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                // Another instance is already running
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // Create tray icon
            _notifyIcon = new TaskbarIcon();
            // Use a built-in icon or a simple one for now
            _notifyIcon.Icon = System.Drawing.SystemIcons.Information;
            _notifyIcon.ToolTipText = "Kaqia 截图工具";
            _notifyIcon.ContextMenu = (System.Windows.Controls.ContextMenu)FindResource("TrayMenu");

            // Setup a hidden window to receive hotkey messages
            var helper = new WindowInteropHelper(new Window { Width = 0, Height = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false });
            helper.EnsureHandle();
            IntPtr hWnd = helper.Handle;

            _hwndSource = HwndSource.FromHwnd(hWnd);
            _hwndSource.AddHook(HwndHook);

            // Register Ctrl+Alt+S (0x53)
            try
            {
                HotkeyManager.Register(hWnd, HOTKEY_ID, HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_ALT, 0x53);
            }
            catch (Exception ex)
            {
                MessageBox.Show("快捷键注册失败，请检查是否被占用: " + ex.Message);
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
            // Trigger screenshot and get raw pixel dimensions
            var screenshot = ScreenCapturer.CaptureAllScreens(out int pixelWidth, out int pixelHeight);
            
            // Show overlay window with pixel info for scaling
            var overlay = new MainWindow(screenshot, pixelWidth, pixelHeight);
            overlay.Show();
            overlay.Activate();
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
