using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ValorantConfigTool;

static class Scanner
{
    // System dirs to skip at drive root
    static readonly HashSet<string> RootSkip = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "$Recycle.Bin", "System Volume Information",
        "Recovery", "Config.Msi", "MSOCache", "PerfLogs",
        "Documents and Settings", "$WinREAgent", "Boot",
    };

    static readonly string[] ConfigPaths =
    {
        @"Program Files (x86)\Tencent Games\VALORANT\live\ShooterGame\Saved\Config",
        @"Program Files\Tencent Games\VALORANT\live\ShooterGame\Saved\Config",
        @"Tencent Games\VALORANT\live\ShooterGame\Saved\Config",
    };

    // Directory-name keywords that suggest a game install folder
    static readonly string[] GameKeywords =
    {
        "VALORANT", "无畏契约", "Valorant", "valorant",
        "Tencent", "腾讯",
        "游戏", "网络游戏", "单机游戏",
        "Game", "Games",
    };

    // ── Phase 1: known install paths (instant) ──

    static List<string> QuickScan()
    {
        var results = new List<string>();

        foreach (string drive in Environment.GetLogicalDrives())
        {
            try { if (!new DriveInfo(drive).IsReady) continue; }
            catch { continue; }

            foreach (string relPath in ConfigPaths)
            {
                string configDir = Path.Combine(drive, relPath);
                if (!Directory.Exists(configDir)) continue;
                CollectFromConfig(configDir, results);
            }

            // Also check %LOCALAPPDATA%
            string localConfig = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"VALORANT\Saved\Config");
            if (Directory.Exists(localConfig))
                CollectFromConfig(localConfig, results);
        }

        return results;
    }

    // ── Phase 1.5: shallow sweep for non-standard installs (cafes etc.) ──

    /// <summary>
    /// Walk drive roots 2 levels deep, following only keyword-matching directories.
    /// Finds VALORANT installs in custom paths like D:\网络游戏\无畏契约\.
    /// </summary>
    static List<string> ShallowSweep(IProgress<string>? progress, CancellationToken cancel)
    {
        var results = new List<string>();

        foreach (string drive in Environment.GetLogicalDrives())
        {
            if (cancel.IsCancellationRequested) return results;

            try { if (!new DriveInfo(drive).IsReady) continue; }
            catch { continue; }

            string[] rootDirs;
            try { rootDirs = Directory.GetDirectories(drive); }
            catch { continue; }

            foreach (string rootDir in rootDirs)
            {
                if (cancel.IsCancellationRequested) return results;

                string rootName = Path.GetFileName(rootDir);

                // Skip system / hidden dirs
                if (RootSkip.Contains(rootName)) continue;
                try
                {
                    if ((File.GetAttributes(rootDir) & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                        continue;
                }
                catch { continue; }

                bool rootMatch = GameKeywords.Any(k => rootName.Contains(k, StringComparison.OrdinalIgnoreCase));

                if (rootMatch)
                {
                    TryConfigPath(Path.Combine(rootDir, "live", "ShooterGame", "Saved", "Config"), results);
                    TryConfigPath(Path.Combine(rootDir, "ShooterGame", "Saved", "Config"), results);
                }

                // Also peek one level deeper for keyword subdirs
                string[] subDirs;
                try { subDirs = Directory.GetDirectories(rootDir); }
                catch { continue; }

                foreach (string subDir in subDirs)
                {
                    if (cancel.IsCancellationRequested) return results;

                    string subName = Path.GetFileName(subDir);
                    bool subMatch = GameKeywords.Any(k => subName.Contains(k, StringComparison.OrdinalIgnoreCase));

                    if (subMatch)
                    {
                        TryConfigPath(Path.Combine(subDir, "live", "ShooterGame", "Saved", "Config"), results);
                        TryConfigPath(Path.Combine(subDir, "ShooterGame", "Saved", "Config"), results);
                    }
                }
            }
        }

        return results;
    }

    static void TryConfigPath(string configDir, List<string> results)
    {
        try
        {
            if (Directory.Exists(configDir))
                CollectFromConfig(configDir, results);
        }
        catch { }
    }

    // ── Collect GameUserSettings.ini from a "Config" directory ──

    static void CollectFromConfig(string configDir, List<string> list)
    {
        try
        {
            foreach (string entry in Directory.EnumerateDirectories(configDir))
            {
                string entryName = Path.GetFileName(entry);

                if (entryName.Equals("WindowsClient", StringComparison.OrdinalIgnoreCase))
                {
                    // Config/WindowsClient/GameUserSettings.ini
                    string ini = Path.Combine(entry, "GameUserSettings.ini");
                    if (File.Exists(ini) && ContentValidator.IsValid(ini))
                        list.Add(ini);
                }
                else
                {
                    // Config/<GUID>/WindowsClient/GameUserSettings.ini
                    string ini = Path.Combine(entry, "WindowsClient", "GameUserSettings.ini");
                    if (File.Exists(ini) && ContentValidator.IsValid(ini))
                        list.Add(ini);
                }
            }
        }
        catch { }
    }

    // ── Combined scan ──

    /// <summary>
    /// Phase 1: QuickScan known paths (always runs).
    /// Phase 1.5: Shallow sweep for non-standard installs.
    /// Phase 2: Everything SDK search + content validation (if available).
    /// </summary>
    public static (List<string> paths, string source) ScanAll(
        IProgress<string>? progress = null, CancellationToken cancel = default)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string source = "QuickOnly";

        // Phase 1 — known paths (instant)
        foreach (string f in QuickScan())
            seen.Add(f);

        if (cancel.IsCancellationRequested)
            return (seen.OrderBy(x => x).ToList(), source);

        // Phase 1.5 — shallow sweep for non-standard paths
        foreach (string f in ShallowSweep(progress, cancel))
            seen.Add(f);

        if (cancel.IsCancellationRequested)
            return (seen.OrderBy(x => x).ToList(), source);

        // Phase 2 — Everything SDK
        if (EverythingSdk.IsAvailable && EverythingSdk.IsDBLoaded)
        {
            List<string>? everythingResults = EverythingSdk.SearchGameUserSettings();
            if (everythingResults != null)
            {
                int validated = 0;
                foreach (string f in everythingResults)
                {
                    if (cancel.IsCancellationRequested) break;
                    if (ContentValidator.IsValid(f))
                    {
                        seen.Add(f);
                        validated++;
                    }
                }
                if (seen.Count > 0) source = "Everything";
                progress?.Report($"Everything: {everythingResults.Count} found, {validated} validated");
            }
            else
            {
                progress?.Report("Everything query failed (IPC error).");
            }
        }

        return (seen.OrderBy(x => x).ToList(), source);
    }
}
