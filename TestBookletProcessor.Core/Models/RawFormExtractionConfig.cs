using System.Collections.Generic;

namespace TestBookletProcessor.Core.Models;

/// <summary>
/// Configuration for extracting specific sheets as separate files during scanned sheet processing.
/// Used to extract raw forms (e.g., demographic sheets) that need separate handling.
/// </summary>
public class RawFormExtractionConfig
{
    /// <summary>
    /// Gets or sets whether raw form extraction is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of QR code values that trigger extraction.
    /// Pages with these primary QR codes will be saved as separate files.
    /// </summary>
    public List<string> TriggerQrCodes { get; set; } = new() { "RAWFORMQR-01", "RAWFORMQR-02" };

    /// <summary>
    /// Gets or sets whether to extract files to a separate dedicated folder.
    /// If false, extracted files are saved in the same folder as the main output.
    /// </summary>
    public bool ExtractToSeparateFolder { get; set; } = false;

    /// <summary>
    /// Gets or sets the folder path where extracted files will be saved.
    /// Only used if ExtractToSeparateFolder is true.
    /// </summary>
    public string ExtractionFolder { get; set; } = @"C:\TestBooklets\Output\RawForms";

    /// <summary>
    /// Gets or sets the suffix to add to extracted filenames.
    /// Example: "RawForm" results in filenames like "SchoolName_RawForm.pdf"
    /// </summary>
    public string FileNameSuffix { get; set; } = "RawForm";

    /// <summary>
    /// Gets or sets whether to include page number in filename when multiple raw forms exist.
    /// Example: "SchoolName_RawForm_Page2.pdf"
    /// </summary>
    public bool IncludePageNumberInFileName { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to skip red pixel removal for extracted raw form pages.
    /// When true, extracted pages will preserve all red ink markings.
    /// Useful for demographic forms or raw score sheets that need original markings preserved.
    /// </summary>
    public bool SkipRedRemoval { get; set; } = true;

    /// <summary>
    /// Checks if a given QR code should trigger extraction.
    /// Supports exact match and wildcard patterns.
    /// </summary>
    /// <param name="qrCode">The QR code value to check</param>
    /// <returns>True if the QR code matches any trigger pattern</returns>
    public bool ShouldExtract(string? qrCode)
    {
        if (string.IsNullOrEmpty(qrCode) || !Enabled)
            return false;

        foreach (var pattern in TriggerQrCodes)
        {
            if (MatchesWildcard(qrCode, pattern, ignoreCase: true))
                return true;
        }

        return false;
    }

    private static bool MatchesWildcard(string value, string pattern, bool ignoreCase = true)
    {
        if (string.IsNullOrEmpty(pattern))
            return false;

        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*") + "$";

        var options = ignoreCase
            ? System.Text.RegularExpressions.RegexOptions.IgnoreCase
            : System.Text.RegularExpressions.RegexOptions.None;

        return System.Text.RegularExpressions.Regex.IsMatch(value, regexPattern, options);
    }
}
