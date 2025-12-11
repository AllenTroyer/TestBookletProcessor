using QrRegionScanner;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;

namespace TestBookletProcessor.Services;

/// <summary>
/// Service for processing scanned sheets where each page is individually aligned
/// to a template page based on its QR code content.
/// </summary>
public class ScannedSheetProcessorService : IScannedSheetProcessor
{
    private readonly IPdfService _pdfService;
    private readonly IDeskewer _deskewer;
    private readonly IImageAligner _aligner;
    private readonly IRedPixelRemoverService? _redPixelRemover;
    private readonly RegionQrScanner? _qrScanner;
    private readonly byte _redThreshold;
    private readonly bool _enableQrScanning;
    private readonly int _qrRegionX;
    private readonly int _qrRegionY;
    private readonly int _qrRegionWidth;
    private readonly int _qrRegionHeight;
    private readonly List<string> _qrValuesExcludingRedRemoval;
    private readonly List<RedPixelExclusionRegion> _redPixelExclusionRegions;

    public ScannedSheetProcessorService(
        IPdfService pdfService,
        IDeskewer deskewer,
        IImageAligner aligner,
        IRedPixelRemoverService? redPixelRemover = null,
        byte redThreshold = 200,
        RegionQrScanner? qrScanner = null,
        bool enableQrScanning = true,
        double qrRegionXInches = 6.5,
        double qrRegionYInches = 9.0,
        double qrRegionWidthInches = 2.0,
        double qrRegionHeightInches = 2.0,
        int dpi = 300,
        List<string>? qrValuesExcludingRedRemoval = null,
        List<RedPixelExclusionRegion>? redPixelExclusionRegions = null)
    {
        _pdfService = pdfService;
        _deskewer = deskewer;
        _aligner = aligner;
        _redPixelRemover = redPixelRemover;
        _qrScanner = qrScanner;
        _redThreshold = redThreshold;
        _enableQrScanning = enableQrScanning;
        
        // Calculate pixel values from inches and DPI
        _qrRegionX = (int)(qrRegionXInches * dpi);
        _qrRegionY = (int)(qrRegionYInches * dpi);
        _qrRegionWidth = (int)(qrRegionWidthInches * dpi);
        _qrRegionHeight = (int)(qrRegionHeightInches * dpi);
        
        _qrValuesExcludingRedRemoval = qrValuesExcludingRedRemoval ??
                                       new List<string> { "MACHINE_SCORED", "NO_RED_INK", "CLEAN" };
        
        _redPixelExclusionRegions = redPixelExclusionRegions ?? new List<RedPixelExclusionRegion>();
    }

    public async Task<ProcessingResult> ProcessScannedSheetsAsync(
        string inputPdf,
        string templatePdf,
        Dictionary<string, int> qrMapping,
        string outputFolder,
        string outputPdf,
        int dpi,
        Action<int, int>? progressCallback = null)
    {
        var result = new ProcessingResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            Console.WriteLine("=== Scanned Sheet Processing Mode ===");
            Console.WriteLine($"Input: {inputPdf}");
            Console.WriteLine($"Template: {templatePdf}");
            Console.WriteLine($"QR Mappings: {qrMapping.Count} patterns");

            // Create unique working folder
            var uniqueId = Guid.NewGuid().ToString("N");
            var workingFolder = Path.Combine(outputFolder, $"temp_scannedsheets_{uniqueId}");
            Directory.CreateDirectory(workingFolder);

            // Split input PDF into individual pages
            Console.WriteLine("\nSplitting input PDF into pages...");
            var inputPages = await _pdfService.SplitPdfAsync(inputPdf, Path.Combine(workingFolder, "input_pages"));
            var totalPages = inputPages.Count;
            Console.WriteLine($"Total pages to process: {totalPages}");

            // Split template PDF into pages for reference
            Console.WriteLine("Loading template pages...");
            var templatePages = await _pdfService.SplitPdfAsync(templatePdf, Path.Combine(workingFolder, "template_pages"));
            Console.WriteLine($"Template has {templatePages.Count} pages");

            var processedPages = new List<string>();

            // Process each page individually
            for (int i = 0; i < inputPages.Count; i++)
            {
                var pageNum = i + 1;
                Console.WriteLine($"\n--- Processing Page {pageNum}/{totalPages} ---");
                progressCallback?.Invoke(pageNum, totalPages);

                var inputPage = inputPages[i];
                var processedPage = await ProcessSinglePageAsync(
                    inputPage,
                    templatePages,
                    qrMapping,
                    workingFolder,
                    pageNum,
                    dpi);

                processedPages.Add(processedPage);
            }

            // Merge all processed pages
            Console.WriteLine($"\nMerging {processedPages.Count} processed pages...");
            await _pdfService.MergePdfsAsync(processedPages, outputPdf);

            result.Success = true;
            result.OutputPath = outputPdf;
            result.PagesProcessed = processedPages.Count;
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;

            Console.WriteLine($"\n? Processing complete!");
            Console.WriteLine($"  Output: {outputPdf}");
            Console.WriteLine($"  Pages: {result.PagesProcessed}");
            Console.WriteLine($"  Time: {result.ProcessingTime.TotalSeconds:F2}s");

            // Cleanup
            PdfService.CleanupDirectory(workingFolder);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
            Console.WriteLine($"\n? Processing failed: {ex.Message}");
        }

        return result;
    }

    private async Task<string> ProcessSinglePageAsync(
        string inputPagePdf,
        List<string> templatePages,
        Dictionary<string, int> qrMapping,
        string workingFolder,
        int pageNumber,
        int dpi)
    {
        var pageFolder = Path.Combine(workingFolder, $"page_{pageNumber}");
        Directory.CreateDirectory(pageFolder);

        // Convert input page to image
        var inputImage = Path.Combine(pageFolder, "input.png");
        await _pdfService.ConvertPageToImageAsync(inputPagePdf, 1, inputImage, dpi);

        // Deskew the input image
        var deskewedImage = Path.Combine(pageFolder, "deskewed.png");
        await _deskewer.DeskewImageAsync(inputImage, deskewedImage);

        // Try to scan QR code
        string? qrCode = null;
        int? templatePageIndex = null;

        if (_enableQrScanning && _qrScanner != null)
        {
            try
            {
                qrCode = _qrScanner.ScanRegion(deskewedImage, _qrRegionX, _qrRegionY, _qrRegionWidth, _qrRegionHeight);
                
                if (qrCode != null)
                {
                    Console.WriteLine($"  QR Code: {qrCode}");
                    templatePageIndex = FindTemplatePageForQr(qrCode, qrMapping);
                    
                    if (templatePageIndex.HasValue)
                    {
                        Console.WriteLine($"  ? Mapped to template page {templatePageIndex.Value}");
                    }
                    else
                    {
                        Console.WriteLine($"  ? QR code not mapped - page will remain unchanged");
                    }
                }
                else
                {
                    Console.WriteLine($"  ? No QR code found - page will remain unchanged");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ? QR scan error: {ex.Message} - page will remain unchanged");
            }
        }

        // If no valid template mapping, return the deskewed page as-is
        if (!templatePageIndex.HasValue || templatePageIndex.Value >= templatePages.Count)
        {
            var unchangedPdf = Path.Combine(pageFolder, "output.pdf");
            await _pdfService.ConvertImageToPdfAsync(deskewedImage, unchangedPdf);
            return unchangedPdf;
        }

        // Get the corresponding template page
        var templatePagePdf = templatePages[templatePageIndex.Value];
        var templateImage = Path.Combine(pageFolder, "template.png");
        await _pdfService.ConvertPageToImageAsync(templatePagePdf, 1, templateImage, dpi);

        // Determine if red removal should be applied
        var shouldApplyRedRemoval = _redPixelRemover != null && qrCode != null &&
                                    !_qrValuesExcludingRedRemoval.Any(pattern =>
                                        MatchesWildcard(qrCode, pattern, ignoreCase: true));

        var imageToAlign = deskewedImage;
        if (shouldApplyRedRemoval)
        {
            Console.WriteLine($"  ? Applying red pixel removal");
            
            // Filter exclusion regions that apply to this QR code
            var applicableRegions = new List<RedPixelExclusionRegion>();
            if (qrCode != null)
            {
                applicableRegions = _redPixelExclusionRegions
                    .Where(r => r.AppliesTo(qrCode))
                    .ToList();
                
                if (applicableRegions.Any())
                {
                    Console.WriteLine($"  ? Applying {applicableRegions.Count} exclusion region(s) for QR: {qrCode}");
                    foreach (var region in applicableRegions)
                    {
                        Console.WriteLine($"    - {region.Name}: ({region.XInches}\", {region.YInches}\") {region.WidthInches}\" × {region.HeightInches}\"");
                    }
                }
            }
            
            var redRemovedImage = Path.Combine(pageFolder, "red_removed.png");
            await _redPixelRemover!.RemoveRedPixelsAsync(deskewedImage, redRemovedImage, _redThreshold, dpi, applicableRegions);
            imageToAlign = redRemovedImage;
        }

        // Align to template
        Console.WriteLine($"  ? Aligning to template");
        var alignedImage = Path.Combine(pageFolder, "aligned.png");
        await _aligner.AlignImageAsync(imageToAlign, templateImage, alignedImage);

        // Convert back to PDF
        var outputPdf = Path.Combine(pageFolder, "output.pdf");
        await _pdfService.ConvertImageToPdfAsync(alignedImage, outputPdf);

        return outputPdf;
    }

    private int? FindTemplatePageForQr(string qrCode, Dictionary<string, int> mapping)
    {
        // Try exact match first
        if (mapping.TryGetValue(qrCode, out int pageIndex))
            return pageIndex;

        // Try wildcard patterns
        foreach (var kvp in mapping)
        {
            if (MatchesWildcard(qrCode, kvp.Key, ignoreCase: true))
                return kvp.Value;
        }

        return null;
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
