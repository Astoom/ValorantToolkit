using System;
using System.Runtime.InteropServices;

namespace ValorantConfigTool;

static class DisplayChanger
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern int ChangeDisplaySettings(ref DEVMODE dm, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE dm);

    const int DM_PELSWIDTH = 0x80000;
    const int DM_PELSHEIGHT = 0x100000;
    const int CDS_UPDATEREGISTRY = 0x01;
    const int CDS_TEST = 0x02;
    const int ENUM_CURRENT_SETTINGS = -1;
    const int DISP_CHANGE_SUCCESSFUL = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX, dmPositionY;
        public int dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels, dmBitsPerPel;
        public int dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
        public int dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    public static (int w, int h) GetCurrent()
    {
        var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
        if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm))
            return (dm.dmPelsWidth, dm.dmPelsHeight);
        return (0, 0);
    }

    public static bool Change(int width, int height)
    {
        var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
        dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT;
        dm.dmPelsWidth = width;
        dm.dmPelsHeight = height;

        // Test first
        int testResult = ChangeDisplaySettings(ref dm, CDS_TEST);
        if (testResult != DISP_CHANGE_SUCCESSFUL)
            return false;

        // Apply and save
        int result = ChangeDisplaySettings(ref dm, CDS_UPDATEREGISTRY);
        return result == DISP_CHANGE_SUCCESSFUL;
    }

    public static readonly (string label, int w, int h)[] Presets =
    {
        ("2560×1440 (16:9 2K)",    2560, 1440),
        ("1920×1440 (4:3)",        1920, 1440),
        ("1920×1080 (16:9 FHD)",   1920, 1080),
        ("1440×1080 (4:3 拉伸)",   1440, 1080),
        ("1280×960  (4:3)",        1280, 960),
        ("1024×768  (4:3)",        1024, 768),
    };
}
