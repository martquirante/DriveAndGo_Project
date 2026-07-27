#nullable disable
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DriveAndGo_Admin.Helpers
{
    public static class IconHelper
    {
        private static Icon _cachedIcon = null;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;

        public static Icon GetAppIcon()
        {
            if (_cachedIcon != null) return _cachedIcon;

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string icoPath = Path.Combine(baseDir, "WebAssets", "app.ico");

                if (File.Exists(icoPath))
                {
                    using var fs = new FileStream(icoPath, FileMode.Open, FileAccess.Read);
                    _cachedIcon = new Icon(fs);
                    return _cachedIcon;
                }

                string pngPath = Path.Combine(baseDir, "WebAssets", "logo.png");
                if (File.Exists(pngPath))
                {
                    GenerateIcoFromPng(pngPath, icoPath);
                    if (File.Exists(icoPath))
                    {
                        using var fs = new FileStream(icoPath, FileMode.Open, FileAccess.Read);
                        _cachedIcon = new Icon(fs);
                        return _cachedIcon;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[IconHelper] Exception loading icon: " + ex.Message);
            }

            try
            {
                _cachedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            return _cachedIcon;
        }

        public static void ApplyToForm(Form form)
        {
            if (form == null || form.IsDisposed) return;
            try
            {
                var icon = GetAppIcon();
                if (icon != null)
                {
                    form.Icon = icon;
                    form.ShowIcon = true;
                    form.ShowInTaskbar = true;

                    void SendIconMessages()
                    {
                        try
                        {
                            if (form.IsHandleCreated && !form.IsDisposed)
                            {
                                SendMessage(form.Handle, WM_SETICON, (IntPtr)ICON_SMALL, icon.Handle);
                                SendMessage(form.Handle, WM_SETICON, (IntPtr)ICON_BIG, icon.Handle);
                            }
                        }
                        catch { }
                    }

                    if (form.IsHandleCreated)
                    {
                        SendIconMessages();
                    }
                    else
                    {
                        form.HandleCreated += (s, e) => SendIconMessages();
                    }

                    form.Shown += (s, e) => SendIconMessages();
                }
            }
            catch { }
        }

        public static void GenerateIcoFromPng(string pngPath, string icoPath)
        {
            try
            {
                using var bmp = new Bitmap(pngPath);
                using var resized = new Bitmap(bmp, new Size(64, 64));
                using var ms = new MemoryStream();
                resized.Save(ms, ImageFormat.Png);
                byte[] pngBytes = ms.ToArray();

                string dir = Path.GetDirectoryName(icoPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                using var fs = new FileStream(icoPath, FileMode.Create, FileAccess.Write);
                using var writer = new BinaryWriter(fs);

                writer.Write((short)0);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write((byte)64);
                writer.Write((byte)64);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((short)1);
                writer.Write((short)32);
                writer.Write((int)pngBytes.Length);
                writer.Write((int)22);
                writer.Write(pngBytes);
                writer.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[IconHelper] GenerateIcoFromPng error: " + ex.Message);
            }
        }
    }
}
