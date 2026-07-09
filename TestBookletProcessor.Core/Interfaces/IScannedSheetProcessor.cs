using System;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Models;

namespace TestBookletProcessor.Core.Interfaces;

/// <summary>
/// Interface for processing scanned sheets where each page is individually aligned
/// to a template page based on its QR code content.
/// </summary>
public interface IScannedSheetProcessor
{
    /// <summary>
    /// Processes scanned sheets by aligning each page to its corresponding template page
    /// based on the QR-to-page mapping in the processor's configuration.
    /// </summary>
    /// <param name="inputPdf">Path to the input PDF containing scanned sheets</param>
    /// <param name="templatePdf">Path to the multi-page template PDF</param>
    /// <param name="outputFolder">Folder for temporary files and final output</param>
    /// <param name="outputPdf">Path for the final aligned PDF output</param>
    /// <param name="progressCallback">Optional callback for progress updates (current page, total pages)</param>
    /// <returns>Processing result with success status and metrics</returns>
    Task<ProcessingResult> ProcessScannedSheetsAsync(
        string inputPdf,
        string templatePdf,
        string outputFolder,
        string outputPdf,
        Action<int, int>? progressCallback = null);
}
