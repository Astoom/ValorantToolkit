using System;
using System.IO;
using System.Linq;

namespace ValorantConfigTool;

/// <summary>
/// Validates that a file is a VALORANT GameUserSettings.ini by checking
/// its actual content rather than relying on path heuristics.
/// </summary>
static class ContentValidator
{
    // Section headers that identify a VALORANT config file
    private static readonly string[] RequiredSections =
    {
        "[/Script/ShooterGame.ShooterGameUserSettings]",
        "[ShooterGameUserSettings]"
    };

    // Key fields that must be present in a VALORANT config
    private static readonly string[] RequiredKeys =
    {
        "ResolutionSizeX",
        "FullscreenMode"
    };

    /// <summary>
    /// Validates a GameUserSettings.ini by reading its content.
    /// Returns true only if the file contains a VALORANT-specific section header
    /// AND the required key fields.
    /// </summary>
    public static bool IsValid(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            // Read first 4KB — more than enough for the ~60-line INI
            string content;
            using (var reader = new StreamReader(path))
            {
                char[] buffer = new char[4096];
                int read = reader.Read(buffer, 0, buffer.Length);
                content = new string(buffer, 0, read);
            }

            // Check for at least one VALORANT-specific section header
            bool hasSection = RequiredSections.Any(
                s => content.Contains(s, StringComparison.OrdinalIgnoreCase));
            if (!hasSection)
                return false;

            // Check for required key fields
            bool hasKeys = RequiredKeys.All(
                k => content.Contains(k, StringComparison.OrdinalIgnoreCase));
            return hasKeys;
        }
        catch (IOException)
        {
            return false; // File locked, inaccessible, etc.
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
