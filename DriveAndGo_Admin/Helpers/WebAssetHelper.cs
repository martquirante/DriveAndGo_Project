using System;
using System.IO;

namespace DriveAndGo_Admin.Helpers
{
    /// <summary>
    /// Helper to locate WebAssets files across modular subfolders and development/runtime directories.
    /// </summary>
    public static class WebAssetHelper
    {
        public static string GetWebAssetPath(string fileName, string panelSubfolder = null)
        {
            string projectSourceDir = @"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_Admin\WebAssets";
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string outputAssetsDir = Path.Combine(baseDir, "WebAssets");

            if (!string.IsNullOrEmpty(panelSubfolder))
            {
                string p1 = Path.Combine(projectSourceDir, "panels", panelSubfolder, fileName);
                if (File.Exists(p1)) return p1;

                string p2 = Path.Combine(outputAssetsDir, "panels", panelSubfolder, fileName);
                if (File.Exists(p2)) return p2;
            }

            string p3 = Path.Combine(projectSourceDir, fileName);
            if (File.Exists(p3)) return p3;

            string p4 = Path.Combine(outputAssetsDir, fileName);
            if (File.Exists(p4)) return p4;

            try
            {
                if (Directory.Exists(projectSourceDir))
                {
                    var found = Directory.GetFiles(projectSourceDir, fileName, SearchOption.AllDirectories);
                    if (found.Length > 0) return found[0];
                }
                if (Directory.Exists(outputAssetsDir))
                {
                    var found = Directory.GetFiles(outputAssetsDir, fileName, SearchOption.AllDirectories);
                    if (found.Length > 0) return found[0];
                }
            }
            catch { }

            return Path.Combine(outputAssetsDir, fileName);
        }

        public static string GetSharedAssetPath(string fileName, string subfolder = "images")
        {
            string projectSourceDir = @"C:\Users\martq\source\repos\DriveAndGo_Project\DriveAndGo_Admin\WebAssets";
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string outputAssetsDir = Path.Combine(baseDir, "WebAssets");

            string p1 = Path.Combine(projectSourceDir, "shared", subfolder, fileName);
            if (File.Exists(p1)) return p1;

            string p2 = Path.Combine(outputAssetsDir, "shared", subfolder, fileName);
            if (File.Exists(p2)) return p2;

            string p3 = Path.Combine(projectSourceDir, fileName);
            if (File.Exists(p3)) return p3;

            return Path.Combine(outputAssetsDir, fileName);
        }
    }
}
