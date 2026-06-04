using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace KaqiaCore
{
    public class HotkeyManager
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;

        public static void Register(IntPtr hWnd, int id, uint modifiers, uint key)
        {
            if (!RegisterHotKey(hWnd, id, modifiers, key))
            {
                throw new Exception("Failed to register hotkey.");
            }
        }

        public static void Unregister(IntPtr hWnd, int id)
        {
            UnregisterHotKey(hWnd, id);
        }
    }
}
