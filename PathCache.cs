using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ValorantConfigTool;

/// <summary>
/// Persists validated config paths to a JSON cache file so subsequent
/// launches can load paths instantly without re-scanning.
/// Cache is stored at %LOCALAPPDATA%\ValorantToolkit\path_cache.json.
/// </summary>
static class PathCache
{
    private static readonly object _lock = new();

    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ValorantToolkit");

    private static readonly string CacheFile = Path.Combine(CacheDir, "path_cache.json");

    // Cache is valid for 7 days by default
    public const int DefaultMaxAgeDays = 7;

    // ── JSON Schema ──
    private sealed class CacheData
    {
        public int SchemaVersion { get; set; } = 1;
        public string LastScanTimestamp { get; set; } = "";
        public string ScanMethod { get; set; } = ""; // "Everything" | "DeepScan" | "QuickOnly"
        public int FileCount { get; set; }
        public List<string> Paths { get; set; } = new();
    }

    /// <summary>
    /// Load cache if it exists and is recent enough.
    /// Each cached path is re-validated (file must still exist and pass content check).
    /// Returns null if cache is missing, stale, or has no valid paths remaining.
    /// </summary>
    public static List<string>? Load(int maxAgeDays = DefaultMaxAgeDays)
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(CacheFile))
                    return null;

                string json = File.ReadAllText(CacheFile);
                var data = JsonSerializer.Deserialize<CacheData>(json);
                if (data?.Paths == null || data.Paths.Count == 0)
                    return null;

                // Check timestamp staleness
                if (!DateTime.TryParse(data.LastScanTimestamp, out DateTime lastScan))
                    return null;

                if ((DateTime.UtcNow - lastScan).TotalDays > maxAgeDays)
                    return null;

                // Re-validate every cached path: file must still exist and be valid
                var valid = data.Paths
                    .Where(p => ContentValidator.IsValid(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(p => p)
                    .ToList();

                return valid.Count > 0 ? valid : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Save paths to the JSON cache.
    /// </summary>
    public static void Save(List<string> paths, string scanMethod)
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);

                var data = new CacheData
                {
                    SchemaVersion = 1,
                    LastScanTimestamp = DateTime.UtcNow.ToString("O"),
                    ScanMethod = scanMethod,
                    FileCount = paths.Count,
                    Paths = paths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToList()
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(CacheFile, json);
            }
            catch (IOException)
            {
                // Non-critical — scanning still works without cache
            }
        }
    }

    /// <summary>
    /// Delete the cache file. Called before a forced refresh.
    /// </summary>
    public static void Clear()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(CacheFile))
                    File.Delete(CacheFile);
            }
            catch (IOException)
            {
                // Non-critical
            }
        }
    }
}
