using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using KaqiaCore;

namespace KaqiaApp
{
    public partial class SettingsWindow : Window
    {
        private AppConfig _config;
        private string _tempModifiers = "";
        private string _tempKey = "";

        public SettingsWindow()
        {
            InitializeComponent();
            _config = ConfigManager.Load();
            LoadUI();
        }

        private void LoadUI()
        {
            _tempModifiers = _config.HotkeyModifiers;
            _tempKey = _config.HotkeyKey;
            HotkeyTextBox.Text = string.IsNullOrEmpty(_tempKey) ? "无" : $"{_tempModifiers}+{_tempKey}".Replace(",", " + ");
            
            AutoStartCheckBox.IsChecked = _config.AutoStart;
            SavePathTextBox.Text = _config.DefaultSavePath;
        }

        private void OnHotkeyPreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            // Only capture actual keys, ignore pure modifiers for now
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.LeftCtrl || key == Key.RightCtrl || 
                key == Key.LeftAlt || key == Key.RightAlt || 
                key == Key.LeftShift || key == Key.RightShift || 
                key == Key.LWin || key == Key.RWin)
                return;

            StringBuilder modifiers = new StringBuilder();
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers.Append("Control,");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers.Append("Alt,");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers.Append("Shift,");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers.Append("Windows,");

            if (modifiers.Length > 0) modifiers.Remove(modifiers.Length - 1, 1); // remove last comma

            _tempModifiers = modifiers.ToString();
            _tempKey = key.ToString();

            HotkeyTextBox.Text = $"{_tempModifiers}+{_tempKey}".Replace(",", " + ");
        }

        private void OnClearHotkeyClick(object sender, RoutedEventArgs e)
        {
            _tempModifiers = "";
            _tempKey = "";
            HotkeyTextBox.Text = "无";
        }

        private void OnBrowseFolderClick(object sender, RoutedEventArgs e)
        {
            // OpenFolderDialog is available in .NET 8+
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                SavePathTextBox.Text = dialog.FolderName;
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            _config.HotkeyModifiers = _tempModifiers;
            _config.HotkeyKey = _tempKey;
            _config.AutoStart = AutoStartCheckBox.IsChecked ?? false;
            _config.DefaultSavePath = SavePathTextBox.Text;

            ConfigManager.Save(_config);
            ApplyAutoStart(_config.AutoStart);

            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ApplyAutoStart(bool enable)
        {
            try
            {
                string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path, true))
                {
                    if (enable)
                    {
                        string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                        key.SetValue("Kaqia", exePath);
                    }
                    else
                    {
                        key.DeleteValue("Kaqia", false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法设置开机启动: " + ex.Message);
            }
        }
    }
}
