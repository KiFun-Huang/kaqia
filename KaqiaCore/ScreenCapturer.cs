using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace KaqiaCore
{
    public class ScreenCapturer
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        private const int SRCCOPY = 0x00CC0020;
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        public static BitmapSource CaptureAllScreens(out int pixelWidth, out int pixelHeight)
        {
            // Get raw physical pixels
            int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
            pixelWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            pixelHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            using (Bitmap bitmap = new Bitmap(pixelWidth, pixelHeight, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    IntPtr destDeviceContext = g.GetHdc();
                    IntPtr screenDeviceContext = GetWindowDC(IntPtr.Zero);

                    // Copy the whole virtual screen
                    BitBlt(destDeviceContext, 0, 0, pixelWidth, pixelHeight, screenDeviceContext, left, top, SRCCOPY);

                    ReleaseDC(IntPtr.Zero, screenDeviceContext);
                    g.ReleaseHdc(destDeviceContext);
                }

                return ToBitmapSource(bitmap);
            }
        }

        private static BitmapSource ToBitmapSource(Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                96, 96, // Keep at 96 DPI for WPF internal mapping, we will scale manually
                System.Windows.Media.PixelFormats.Bgra32, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            bitmapSource.Freeze();
            return bitmapSource;
        }
    }
}
