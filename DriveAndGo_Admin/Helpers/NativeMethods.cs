#nullable disable
using System.Runtime.InteropServices;

namespace DriveAndGo_Admin
{
    /// <summary>
    /// P/Invoke declarations for Windows-native APIs used by UI components.
    /// </summary>
    internal static class NativeMethods
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(
            nint hwnd,
            int  attr,
            int[] attrValue,
            int  attrSize);
    }
}
