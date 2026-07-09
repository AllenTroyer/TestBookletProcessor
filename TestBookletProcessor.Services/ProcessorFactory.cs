using QrRegionScanner;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;

namespace TestBookletProcessor.Services;

/// <summary>
/// Builds a fully wired processor stack from configuration.
/// Each call creates fresh service instances, so callers that process jobs
/// concurrently get isolated managed state per job.
/// </summary>
public static class ProcessorFactory
{
    public static BookletProcessorService CreateBookletProcessor(
        BookletProcessorOptions options,
        ILoggingService? loggingService = null)
    {
        var pdfService = new PdfService();
        var deskewer = new Deskewer();
        var aligner = new ImageAlignerAlt();
        var redPixelRemover = options.EnableRedPixelRemover ? new RedPixelRemoverService() : null;
        var qrScanner = options.QrScanner.EnableQrScanning ? new RegionQrScanner() : null;

        IScannedSheetProcessor? scannedSheetProcessor = null;
        if (!string.IsNullOrEmpty(options.ScannedSheets.TemplateName))
        {
            scannedSheetProcessor = new ScannedSheetProcessorService(
                pdfService, deskewer, aligner, redPixelRemover, qrScanner, options, loggingService);
        }

        return new BookletProcessorService(
            pdfService, deskewer, aligner, redPixelRemover, qrScanner, options, scannedSheetProcessor, loggingService);
    }
}
