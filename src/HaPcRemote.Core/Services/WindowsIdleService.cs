using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace HaPcRemote.Service.Services;

[SupportedOSPlatform("windows")]
public sealed partial class WindowsIdleService : IIdleService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public int GetIdleSeconds()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info))
            return 0;

        var idleMs = (uint)Environment.TickCount - info.dwTime;
        return (int)(idleMs / 1000);
    }
}
