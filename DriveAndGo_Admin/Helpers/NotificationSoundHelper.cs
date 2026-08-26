using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DriveAndGo_Admin.Helpers
{
    /// <summary>
    /// Plays notification sound effects asynchronously without blocking UI threads.
    /// Uses Windows Multimedia (winmm.dll) mciSendString to play MP3 audio natively.
    /// </summary>
    public static class NotificationSoundHelper
    {
        [DllImport("winmm.dll", EntryPoint = "mciSendStringA", ExactSpelling = true, CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern int mciSendString(string lpstrCommand, string lpstrReturnString, int uReturnLength, int hwndCallback);

        private static readonly object _lockObj = new();
        private static DateTime _lastPlayTime = DateTime.MinValue;

        /// <summary>
        /// Plays the system notification alert sound (Resources/notif_Effects/dragon-studio-new-notification-3-398649.mp3).
        /// Debounces duplicate sounds within 300ms.
        /// </summary>
        public static void PlayNotificationSound()
        {
            lock (_lockObj)
            {
                if ((DateTime.UtcNow - _lastPlayTime).TotalMilliseconds < 300)
                    return;
                _lastPlayTime = DateTime.UtcNow;
            }

            Task.Run(() =>
            {
                try
                {
                    string basePath = AppDomain.CurrentDomain.BaseDirectory;
                    string soundPath = Path.Combine(basePath, "Resources", "notif_Effects", "dragon-studio-new-notification-3-398649.mp3");

                    if (!File.Exists(soundPath))
                    {
                        // Search project tree fallback
                        string projectDir = Path.GetFullPath(Path.Combine(basePath, "..", "..", ".."));
                        string projectPath = Path.Combine(projectDir, "Resources", "notif_Effects", "dragon-studio-new-notification-3-398649.mp3");
                        if (File.Exists(projectPath))
                            soundPath = projectPath;
                    }

                    if (File.Exists(soundPath))
                    {
                        // Clean close any previous alias and open file
                        mciSendString("close dgo_notif_sound", null, 0, 0);
                        int openRes = mciSendString($"open \"{soundPath}\" type mpegvideo alias dgo_notif_sound", null, 0, 0);
                        if (openRes == 0)
                        {
                            mciSendString("play dgo_notif_sound from 0", null, 0, 0);
                            return;
                        }
                    }

                    // Fallback to system asterisk beep if file not found or mci failed
                    System.Media.SystemSounds.Asterisk.Play();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[NotificationSound] Audio playback warning: " + ex.Message);
                    try
                    {
                        System.Media.SystemSounds.Asterisk.Play();
                    }
                    catch { }
                }
            });
        }
    }
}
