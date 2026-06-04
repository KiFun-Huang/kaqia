using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KaqiaCore
{
    public class ScreenCapturer
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetWindowDC(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        private const int SRCCOPY = 0x00CC0020;
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        public static BitmapSource CaptureAllScreens(out int pixelWidth, out int pixelHeight)
        {
            int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
            pixelWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            pixelHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            using (var bitmap = new System.Drawing.Bitmap(pixelWidth, pixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (var g = System.Drawing.Graphics.FromImage(bitmap))
                {
                    IntPtr destDeviceContext = g.GetHdc();
                    IntPtr screenDeviceContext = GetWindowDC(IntPtr.Zero);

                    BitBlt(destDeviceContext, 0, 0, pixelWidth, pixelHeight, screenDeviceContext, left, top, SRCCOPY);

                    ReleaseDC(IntPtr.Zero, screenDeviceContext);
                    g.ReleaseHdc(destDeviceContext);
                }

                // Important: We need to know the system DPI to set the BitmapSource DPI correctly
                // If we set it to 96, but the screen is 144 (150%), WPF will scale it.
                // However, for pure pixel capture, we'll keep it as is and fix the rendering side.
                return ToBitmapSource(bitmap);
            }
        }

        private static BitmapSource ToBitmapSource(System.Drawing.Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                96, 96, // We'll handle scaling in WPF via RenderOptions
                PixelFormats.Bgra32, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            bitmapSource.Freeze();
            return bitmapSource;
        }
    }
}
