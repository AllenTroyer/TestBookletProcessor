using System.Collections.Generic;

namespace TestBookletProcessor.Core.Models;

/// <summary>
/// Defines a region on a scanned sheet where red pixel removal should be excluded.
/// Used to protect handwriting or other red markings that should be preserved.
/// </summary>
public class RedPixelExclusionRegion
{
    /// <summary>
    /// Gets or sets the name of this exclusion region (for documentation/logging).
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the list of QR code patterns that this exclusion applies to.
    /// Supports exact matches (e.g., "3100QR-01") or wildcards (e.g., "3100QR-*").
    /// </summary>
    public List<string> QrCodePatterns { get; set; } = new();

    /// <summary>
    /// Gets or sets the X coordinate of the region's top-left corner in inches.
    /// </summary>
    public double XInches { get; set; }

    /// <summary>
    /// Gets or sets the Y coordinate of the region's top-left corner in inches.
    /// </summary>
    public double YInches { get; set; }

    /// <summary>
    /// Gets or sets the width of the region in inches.
    /// </summary>
    public double WidthInches { get; set; }

    /// <summary>
    /// Gets or sets the height of the region in inches.
    /// </summary>
    public double HeightInches { get; set; }

    /// <summary>
    /// Converts the inch-based dimensions to pixel coordinates based on DPI.
    /// </summary>
    /// <param name="dpi">The DPI (dots per inch) of the image</param>
    /// <returns>A tuple containing pixel coordinates (x, y, width, height)</returns>
    public (int X, int Y, int Width, int Height) ToPixelCoordinates(int dpi)
    {
        return (
            (int)(XInches * dpi),
            (int)(YInches * dpi),
            (int)(WidthInches * dpi),
            (int)(HeightInches * dpi)
        );
    }

    /// <summary>
    /// Checks if this exclusion region applies to the given QR code.
    /// Supports wildcard matching using *.
    /// </summary>
    /// <param name="qrCode">The QR code to check</param>
    /// <returns>True if this region applies to the QR code</returns>
    public bool AppliesTo(string qrCode)
    {
        if (string.IsNullOrEmpty(qrCode))
            return false;

        foreach (var pattern in QrCodePatterns)
        {
            if (MatchesPattern(qrCode, pattern))
                return true;
        }

        return false;
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return false;

        // Exact match
        if (value.Equals(pattern, StringComparison.OrdinalIgnoreCase))
            return true;

        // Wildcard match
        if (pattern.Contains('*'))
        {
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(
                value, 
                regexPattern, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return false;
    }
}
