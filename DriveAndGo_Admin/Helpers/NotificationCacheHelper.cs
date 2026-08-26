using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DriveAndGo_Admin.Helpers
{
    /// <summary>
    /// Persistently tracks read notification IDs in local application storage
    /// to ensure virtual/live alerts (overdue, pending) and db items maintain
    /// accurate read status across UI navigation and restarts.
    /// </summary>
    public static class NotificationCacheHelper
    {
        private static readonly HashSet<string> _readIds = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new();
        private static readonly string _cacheFile;

        static NotificationCacheHelper()
        {
            try
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DriveAndGo_Admin");
                if (!Directory.Exists(appData))
                    Directory.CreateDirectory(appData);
                _cacheFile = Path.Combine(appData, "read_notifications.json");

                if (File.Exists(_cacheFile))
                {
                    string json = File.ReadAllText(_cacheFile);
                    var list = JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null)
                    {
                        foreach (var id in list)
                        {
                            if (!string.IsNullOrWhiteSpace(id))
                                _readIds.Add(id.Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[NotificationCache] Init warning: " + ex.Message);
            }
        }

        public static bool IsRead(string notifId)
        {
            if (string.IsNullOrWhiteSpace(notifId)) return false;
            lock (_lock)
            {
                return _readIds.Contains(notifId.Trim());
            }
        }

        public static void MarkRead(string notifId)
        {
            if (string.IsNullOrWhiteSpace(notifId)) return;
            lock (_lock)
            {
                if (_readIds.Add(notifId.Trim()))
                {
                    SaveToFile();
                }
            }
        }

        public static void MarkAllRead(IEnumerable<string> ids)
        {
            if (ids == null) return;
            lock (_lock)
            {
                bool changed = false;
                foreach (var id in ids)
                {
                    if (!string.IsNullOrWhiteSpace(id) && _readIds.Add(id.Trim()))
                    {
                        changed = true;
                    }
                }
                if (changed)
                {
                    SaveToFile();
                }
            }
        }

        private static void SaveToFile()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_cacheFile)) return;
                var list = new List<string>(_readIds);
                string json = JsonSerializer.Serialize(list);
                File.WriteAllText(_cacheFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[NotificationCache] Save warning: " + ex.Message);
            }
        }
    }
}
