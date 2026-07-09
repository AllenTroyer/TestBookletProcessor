using System.Collections.Generic;

namespace TestBookletProcessor.Core.Models;

/// <summary>
/// Configuration for scanned sheet processing mode.
/// In this mode, each page is individually aligned to a template page based on its QR code.
/// </summary>
public class ScannedSheetConfig
{
    /// <summary>
    /// Gets or sets the template filename used for scanned sheet mode detection.
    /// When this template name is detected, scanned sheet processing is automatically enabled.
    /// </summary>
    public string TemplateName { get; set; } = "Template_ScannedSheets.pdf";

    /// <summary>
    /// Gets or sets the mapping from QR code patterns to template page indices.
    /// Supports wildcard patterns using * for one or more characters.
    /// Key: QR code pattern (e.g., "FORM-A-*", "ANSWER-SHEET-*")
    /// Value: Zero-based template page index to align to
    /// </summary>
    public Dictionary<string, int> QrToPageMapping { get; set; } = new();
}
