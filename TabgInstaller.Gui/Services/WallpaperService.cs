using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TabgInstaller.Gui.Services
{
    public class WallpaperService
    {
        private readonly Action<string> _logger;

        public WallpaperService(Action<string> logger = null)
        {
            _logger = logger ?? (_ => { });
        }

        // P/Invoke for setting wallpaper
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SystemParametersInfoW(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

        private const uint SPI_SETDESKWALLPAPER = 0x0014;
        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDWININICHANGE = 0x02;

        public async Task<bool> SetWallpaperFromFileAsync(string imagePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                {
                    _logger($"Wallpaper file not found: {imagePath}");
                    return false;
                }

                string tempDir = Path.Combine(Path.GetTempPath(), "SigmaMode");
                Directory.CreateDirectory(tempDir);
                string bmpPath = Path.Combine(tempDir, "sigma_wallpaper_fromfile.bmp");

                using (var img = Image.FromFile(imagePath))
                {
                    // Convert to BMP to maximize compatibility with SystemParametersInfo
                    img.Save(bmpPath, ImageFormat.Bmp);
                }

                bool result = SystemParametersInfoW(SPI_SETDESKWALLPAPER, 0, bmpPath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
                if (!result)
                {
                    var error = Marshal.GetLastWin32Error();
                    _logger($"Failed to set wallpaper from file. Win32Error={error}");
                    return false;
                }

                _logger($"Desktop wallpaper set from: {imagePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger($"Error setting wallpaper from file: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CaptureAndSetWallpaperAsync()
        {
            try
            {
                // Capture after short delay left to caller; this method only captures and sets
                var bmp = CaptureVirtualScreenBitmap();
                if (bmp == null)
                {
                    _logger("Wallpaper capture failed: no bitmap captured");
                    return false;
                }

                string tempDir = Path.Combine(Path.GetTempPath(), "SigmaMode");
                Directory.CreateDirectory(tempDir);
                string bmpPath = Path.Combine(tempDir, "sigma_wallpaper.bmp");

                bmp.Save(bmpPath, ImageFormat.Bmp);
                bmp.Dispose();

                _logger($"Saved wallpaper screenshot to: {bmpPath}");

                bool result = SystemParametersInfoW(SPI_SETDESKWALLPAPER, 0, bmpPath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
                if (!result)
                {
                    var error = Marshal.GetLastWin32Error();
                    _logger($"Failed to set wallpaper. Win32Error={error}");
                    return false;
                }

                _logger("Desktop wallpaper updated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger($"Error setting wallpaper: {ex.Message}");
                return false;
            }
        }

        private static Bitmap CaptureVirtualScreenBitmap()
        {
            // Determine the bounds covering all screens
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var screen in Screen.AllScreens)
            {
                if (screen.Bounds.Left < minX) minX = screen.Bounds.Left;
                if (screen.Bounds.Top < minY) minY = screen.Bounds.Top;
                if (screen.Bounds.Right > maxX) maxX = screen.Bounds.Right;
                if (screen.Bounds.Bottom > maxY) maxY = screen.Bounds.Bottom;
            }

            int width = maxX - minX;
            int height = maxY - minY;
            if (width <= 0 || height <= 0)
                return null;

            var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(minX, minY, 0, 0, new Size(width, height));
            }
            return bmp;
        }
    }
}


