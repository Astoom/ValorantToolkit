using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ValorantConfigTool;

/// <summary>
/// P/Invoke declarations for Everything64.dll and kernel32.dll.
/// Uses runtime LoadLibrary/GetProcAddress because the DLL path is dynamic.
/// </summary>
internal static class EverythingNative
{
    // ── Request flags ──
    internal const uint EVERYTHING_REQUEST_FILE_NAME               = 0x00000001;
    internal const uint EVERYTHING_REQUEST_PATH                   = 0x00000002;
    internal const uint EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME = 0x00000004;
    internal const uint EVERYTHING_REQUEST_DATE_MODIFIED          = 0x00000008;
    internal const uint EVERYTHING_REQUEST_SIZE                   = 0x00000010;
    internal const uint EVERYTHING_REQUEST_DATE_CREATED           = 0x00000020;
    internal const uint EVERYTHING_REQUEST_DATE_ACCESSED          = 0x00000040;
    internal const uint EVERYTHING_REQUEST_ATTRIBUTES             = 0x00000080;

    // ── Error codes ──
    internal const uint EVERYTHING_OK                = 0x00000000;
    internal const uint EVERYTHING_ERROR_MEMORY      = 0x00000001;
    internal const uint EVERYTHING_ERROR_IPC         = 0x00000002;
    internal const uint EVERYTHING_ERROR_REGISTERCLASSEX = 0x00000003;
    internal const uint EVERYTHING_ERROR_CREATEWINDOW     = 0x00000004;
    internal const uint EVERYTHING_ERROR_CREATETHREAD     = 0x00000005;
    internal const uint EVERYTHING_ERROR_INVALIDINDEX     = 0x00000006;
    internal const uint EVERYTHING_ERROR_INVALIDCALL      = 0x00000007;

    // ── kernel32.dll ──
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll")]
    internal static extern bool FreeLibrary(IntPtr hModule);

    // ── Everything SDK delegate types (StdCall to match Win32 DLL) ──
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void Everything_SetSearchW([MarshalAs(UnmanagedType.LPWStr)] string lpSearchString);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void Everything_SetRequestFlags(uint dwRequestFlags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate bool Everything_Query(bool bWait);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate uint Everything_GetNumResults();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate uint Everything_GetResultFullPathNameW(uint nIndex, StringBuilder lpString, uint nMaxCount);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void Everything_Reset();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate uint Everything_GetLastError();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void Everything_SetMax(uint dwMax);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate bool Everything_IsDBLoaded();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate uint Everything_GetMajorVersion();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate uint Everything_GetMinorVersion();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate bool Everything_GetResultDateModified(int nIndex, out long lpDateModified);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate bool Everything_GetResultSize(int nIndex, out long lpSize);
}
