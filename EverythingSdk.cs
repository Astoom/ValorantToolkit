using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ValorantConfigTool;

/// <summary>
/// Managed wrapper around the Everything SDK (Everything64.dll).
/// Resolves the DLL at runtime from known install paths, binds function
/// pointers via LoadLibrary/GetProcAddress, and exposes a simple Search API.
/// </summary>
static class EverythingSdk
{
    private const int MaxResults = 200;
    private const int MaxPathLength = 260;

    // Everything SDK is NOT thread-safe — all calls must be serialized
    private static readonly object _sdkLock = new();

    // Probe paths for Everything64.dll, in priority order
    private static readonly string[] ProbePaths =
    {
        // App-local (bundled alongside exe)
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Everything64.dll"),
        // Standard install (64-bit)
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything", "Everything64.dll"),
        // Standard install (32-bit on 64-bit OS)
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything", "Everything64.dll"),
        // Portable / user installation
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Everything", "Everything64.dll"),
    };

    private static IntPtr _module = IntPtr.Zero;
    private static bool _resolved;
    private static bool _available;
    private static string? _resolvedPath;

    // Delegates (populated via GetProcAddress)
    private static EverythingNative.Everything_SetSearchW? _setSearch;
    private static EverythingNative.Everything_SetRequestFlags? _setRequestFlags;
    private static EverythingNative.Everything_Query? _query;
    private static EverythingNative.Everything_GetNumResults? _getNumResults;
    private static EverythingNative.Everything_GetResultFullPathNameW? _getResultFullPathName;
    private static EverythingNative.Everything_Reset? _reset;
    private static EverythingNative.Everything_GetLastError? _getLastError;
    private static EverythingNative.Everything_SetMax? _setMax;
    private static EverythingNative.Everything_IsDBLoaded? _isDbLoaded;

    /// <summary>
    /// Whether the Everything SDK DLL was loaded and functions resolved successfully.
    /// </summary>
    public static bool IsAvailable
    {
        get
        {
            if (!_resolved) ResolveDll();
            return _available;
        }
    }

    /// <summary>
    /// Path to the resolved Everything64.dll, or null.
    /// </summary>
    public static string? DllPath => _resolvedPath;

    /// <summary>
    /// Everything version string (e.g. "1.4" / "1.5") or null.
    /// </summary>
    public static string? Version { get; private set; }

    private static void ResolveDll()
    {
        if (_resolved) return;
        _resolved = true;

        foreach (string path in ProbePaths)
        {
            if (!File.Exists(path)) continue;

            IntPtr mod = EverythingNative.LoadLibrary(path);
            if (mod == IntPtr.Zero) continue;

            // Resolve all function pointers
            _setSearch            = GetDelegate<EverythingNative.Everything_SetSearchW>(mod, "Everything_SetSearchW");
            _setRequestFlags      = GetDelegate<EverythingNative.Everything_SetRequestFlags>(mod, "Everything_SetRequestFlags");
            _query                = GetDelegate<EverythingNative.Everything_Query>(mod, "Everything_QueryW");
            _getNumResults        = GetDelegate<EverythingNative.Everything_GetNumResults>(mod, "Everything_GetNumResults");
            _getResultFullPathName = GetDelegate<EverythingNative.Everything_GetResultFullPathNameW>(mod, "Everything_GetResultFullPathNameW");
            _reset                = GetDelegate<EverythingNative.Everything_Reset>(mod, "Everything_Reset");
            _getLastError         = GetDelegate<EverythingNative.Everything_GetLastError>(mod, "Everything_GetLastError");
            _setMax               = GetDelegate<EverythingNative.Everything_SetMax>(mod, "Everything_SetMax");
            _isDbLoaded           = GetDelegate<EverythingNative.Everything_IsDBLoaded>(mod, "Everything_IsDBLoaded");

            if (_setSearch != null && _query != null && _getNumResults != null && _getResultFullPathName != null)
            {
                _module = mod;
                _resolvedPath = path;
                _available = true;

                // Detect version
                var getMaj = GetDelegate<EverythingNative.Everything_GetMajorVersion>(mod, "Everything_GetMajorVersion");
                var getMin = GetDelegate<EverythingNative.Everything_GetMinorVersion>(mod, "Everything_GetMinorVersion");
                if (getMaj != null && getMin != null)
                    Version = $"{getMaj()}.{getMin()}";
                else
                    Version = "1.4";

                return;
            }

            // Missing required functions — free DLL and try next path
            EverythingNative.FreeLibrary(mod);
        }

        _available = false;
    }

    private static T? GetDelegate<T>(IntPtr module, string procName) where T : Delegate
    {
        IntPtr addr = EverythingNative.GetProcAddress(module, procName);
        return addr != IntPtr.Zero
            ? Marshal.GetDelegateForFunctionPointer<T>(addr)
            : null;
    }

    /// <summary>
    /// Whether the Everything database is fully loaded and ready for queries.
    /// </summary>
    public static bool IsDBLoaded
    {
        get
        {
            if (!IsAvailable || _isDbLoaded == null) return false;
            try { return _isDbLoaded(); }
            catch { return false; }
        }
    }

    /// <summary>
    /// Search for files matching the specified query.
    /// Returns null if Everything is not available.
    /// Returns an empty list if the query found nothing.
    /// </summary>
    public static List<string>? Search(string query)
    {
        if (!IsAvailable) return null;

        lock (_sdkLock)
        {
            // Reset state
            _reset!();

            // Configure query
            _setSearch!(query);
            _setRequestFlags!(EverythingNative.EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME);
            _setMax!(MaxResults);

            // Execute
            if (!_query!(/*bWait=*/ true))
            {
                uint error = _getLastError!();
                Debug.WriteLine($"Everything query failed with error code: {error}");
                return new List<string>(0);
            }

            uint count = _getNumResults!();
            if (count == 0)
                return new List<string>(0);

            var results = new List<string>((int)count);
            var sb = new StringBuilder(MaxPathLength);

            for (int i = 0; i < count; i++)
            {
                sb.Clear();
                if (_getResultFullPathName!((uint)i, sb, MaxPathLength) > 0)
                {
                    results.Add(sb.ToString());
                }
            }

            return results;
        }
    }

    /// <summary>
    /// Search specifically for GameUserSettings.ini files.
    /// </summary>
    public static List<string>? SearchGameUserSettings()
    {
        return Search("GameUserSettings.ini");
    }
}
