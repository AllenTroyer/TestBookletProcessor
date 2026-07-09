namespace TestBookletProcessor.Core.Models;

/// <summary>
/// Tracks information about a page that needs to be extracted as a separate file.
/// Used during scanned sheet processing to identify and extract raw forms.
/// </summary>
public class RawFormExtractionInfo
{
    /// <summary>
    /// Gets or sets the page number in the input PDF (1-based).
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Gets or sets the path to the processed PDF page.
    /// This is the aligned/cleaned page ready for extraction.
    /// </summary>
    public string ProcessedPagePdf { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary QR code value that triggered extraction.
    /// Example: "RAWFORMQR-01"
    /// </summary>
    public string PrimaryQrCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the secondary QR code value used for file naming.
    /// Example: "Lincoln HS:John Doe"
    /// </summary>
    public string? SecondaryQrValue { get; set; }

    /// <summary>
    /// Gets or sets the path where the extracted file was saved.
    /// Set after extraction completes.
    /// </summary>
    public string? ExtractedFilePath { get; set; }
}
