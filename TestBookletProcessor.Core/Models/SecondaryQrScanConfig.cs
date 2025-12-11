namespace TestBookletProcessor.Core.Models;

/// <summary>
/// Configuration for scanning a secondary QR code region to extract file naming information.
/// Used in scanned sheet processing to dynamically rename output files based on QR code content.
/// </summary>
public class SecondaryQrScanConfig
{
    /// <summary>
    /// Gets or sets the QR code value that triggers secondary QR scanning.
    /// When a page has this primary QR code, the secondary region will be scanned.
    /// </summary>
    public string TriggerQrCode { get; set; } = "CHECKLISTQR-01";

    /// <summary>
    /// Gets or sets the X coordinate (in inches) of the secondary scan region's top-left corner.
    /// </summary>
    public double RegionXInches { get; set; } = 0.0;

    /// <summary>
    /// Gets or sets the Y coordinate (in inches) of the secondary scan region's top-left corner.
    /// </summary>
    public double RegionYInches { get; set; } = 0.75;

    /// <summary>
    /// Gets or sets the width (in inches) of the secondary scan region.
    /// </summary>
    public double RegionWidthInches { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the height (in inches) of the secondary scan region.
    /// </summary>
    public double RegionHeightInches { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the pattern in the output filename to be replaced with the extracted QR value.
    /// The extracted value (portion before colon) will replace this pattern.
    /// </summary>
    public string FileNameReplacementPattern { get; set; } = "SchoolCityState";

    /// <summary>
    /// Converts the inch-based region dimensions to pixel coordinates based on DPI.
    /// </summary>
    /// <param name="dpi">The DPI (dots per inch) of the image</param>
    /// <returns>A tuple containing pixel coordinates (x, y, width, height)</returns>
    public (int X, int Y, int Width, int Height) ToPixelCoordinates(int dpi)
    {
        return (
            (int)(RegionXInches * dpi),
            (int)(RegionYInches * dpi),
            (int)(RegionWidthInches * dpi),
            (int)(RegionHeightInches * dpi)
        );
    }
}
