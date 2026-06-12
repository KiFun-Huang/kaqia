using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using KaqiaCore;

namespace KaqiaApp
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
            this.Loaded += SplashWindow_Loaded;
        }

        private async void SplashWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try {
                var config = ConfigManager.Load();
                if (!string.IsNullOrEmpty(config.HotkeyKey)) {
                    string hotkeyStr = string.Join(" + ", config.HotkeyModifiers);
                    if (!string.IsNullOrEmpty(hotkeyStr)) hotkeyStr += " + ";
                    hotkeyStr += config.HotkeyKey;
                    
                    StatusText.Text = $"已在后台运行 (截图快捷键: {hotkeyStr})";
                }
            } catch { }

            // Show for 2.5 seconds
            await Task.Delay(2500);

            // Smooth fade out
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400));
            fadeOut.Completed += (s, ev) => this.Close();
            this.BeginAnimation(Window.OpacityProperty, fadeOut);
        }
    }
}