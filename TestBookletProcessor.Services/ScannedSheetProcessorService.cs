using QrRegionScanner;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;
using TestBookletProcessor.Core.Utilities;

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
    private readonly ILoggingService? _loggingService;
    private readonly int _dpi;
    private readonly byte _redThreshold;
    private readonly int _qrRegionX;
    private readonly int _qrRegionY;
    private readonly int _qrRegionWidth;
    private readonly int _qrRegionHeight;
    private readonly List<string> _qrValuesExcludingRedRemoval;
    private readonly List<RedPixelExclusionRegion> _redPixelExclusionRegions;
    private readonly Dictionary<string, int> _qrMapping;
    private readonly SecondaryQrScanConfig? _secondaryQrScanConfig;
    private readonly RawFormExtractionConfig? _rawFormExtractionConfig;

    public ScannedSheetProcessorService(
        IPdfService pdfService,
        IDeskewer deskewer,
        IImageAligner aligner,
        IRedPixelRemoverService? redPixelRemover,
        RegionQrScanner? qrScanner,
        BookletProcessorOptions options,
        ILoggingService? loggingService = null)
    {
        _pdfService = pdfService;
        _deskewer = deskewer;
        _aligner = aligner;
        _redPixelRemover = redPixelRemover;
        _qrScanner = qrScanner;
        _loggingService = loggingService;

        _dpi = options.DefaultDpi;
        _redThreshold = options.RedPixelThreshold;
        _qrRegionX = (int)(options.QrScanner.QrRegionXInches * _dpi);
        _qrRegionY = (int)(options.QrScanner.QrRegionYInches * _dpi);
        _qrRegionWidth = (int)(options.QrScanner.QrRegionWidthInches * _dpi);
        _qrRegionHeight = (int)(options.QrScanner.QrRegionHeightInches * _dpi);
        _qrValuesExcludingRedRemoval = options.QrScanner.QrValuesExcludingRedRemoval;
        _redPixelExclusionRegions = options.RedPixelExclusionRegions;
        _qrMapping = options.ScannedSheets.QrToPageMapping;
        _secondaryQrScanConfig = options.ScannedSheets.SecondaryQrScan;
        _rawFormExtractionConfig = options.ScannedSheets.RawFormExtraction;
    }

    public async Task<ProcessingResult> ProcessScannedSheetsAsync(
        string inputPdf,
        string templatePdf,
        string outputFolder,
        string outputPdf,
        Action<int, int>? progressCallback = null)
    {
        var result = new ProcessingResult();
        var stopwatch = Stopwatch.StartNew();

        // Create unique working folder name (declare outside try for cleanup in finally)
        var uniqueId = Guid.NewGuid().ToString("N");
        var workingFolder = Path.Combine(outputFolder, $"temp_scannedsheets_{uniqueId}");

        try
        {
            Console.WriteLine("=== Scanned Sheet Processing Mode ===");
            Console.WriteLine($"Input: {inputPdf}");
            Console.WriteLine($"Template: {templatePdf}");
            Console.WriteLine($"QR Mappings: {_qrMapping.Count} patterns");

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

            // Track pages for raw form extraction
            var extractionPages = new List<RawFormExtractionInfo>();

            // Track secondary QR value for file naming
            string? secondaryQrValue = null;

            // Process each page individually
            for (int i = 0; i < inputPages.Count; i++)
            {
                var pageNum = i + 1;
                Console.WriteLine($"\n--- Processing Page {pageNum}/{totalPages} ---");
                progressCallback?.Invoke(pageNum, totalPages);

                var inputPage = inputPages[i];
                var (processedPage, scannedSecondaryQr, extractionInfo) = await ProcessSinglePageAsync(
                    inputPage,
                    templatePages,
                    workingFolder,
                    pageNum,
                    result.Warnings);

                processedPages.Add(processedPage);

                // Store first found secondary QR value for file naming
                if (scannedSecondaryQr != null && secondaryQrValue == null)
                {
                    secondaryQrValue = scannedSecondaryQr;
                    Console.WriteLine($"  ? Secondary QR captured for file naming: {secondaryQrValue}");
                }

                // Track page for extraction if flagged
                if (extractionInfo != null)
                {
                    extractionInfo.SecondaryQrValue = scannedSecondaryQr;
                    extractionPages.Add(extractionInfo);
                    Console.WriteLine($"  ? Page flagged for raw form extraction");
                }
            }

            // The trigger page carrying the secondary QR may come after a raw-form page in the
            // scan stack, so backfill it now that the whole file has been scanned.
            if (secondaryQrValue != null)
            {
                foreach (var info in extractionPages.Where(p => string.IsNullOrEmpty(p.SecondaryQrValue)))
                    info.SecondaryQrValue = secondaryQrValue;
            }

            // Merge all processed pages
            Console.WriteLine($"\nMerging {processedPages.Count} processed pages...");
            await _pdfService.MergePdfsAsync(processedPages, outputPdf);

            // Extract raw form sheets if any were flagged
            var extractedFiles = new List<string>();
            if (extractionPages.Any())
            {
                Console.WriteLine($"\n=== Extracting Raw Form Sheets ===");
                Console.WriteLine($"Pages to extract: {extractionPages.Count}");

                foreach (var extractionInfo in extractionPages)
                {
                    var extractedPath = ExtractRawFormSheet(extractionInfo, outputPdf, outputFolder, extractionPages.Count);
                    extractionInfo.ExtractedFilePath = extractedPath;
                    extractedFiles.Add(extractedPath);
                }

                Console.WriteLine($"? {extractedFiles.Count} raw form(s) extracted successfully");
            }

            // Apply dynamic file naming if secondary QR was found
            var finalOutputPath = ApplyDynamicNaming(inputPdf, outputPdf, secondaryQrValue, result.Warnings);

            result.Success = true;
            result.OutputPath = finalOutputPath;
            result.PagesProcessed = processedPages.Count;
            result.ExtractedFiles = extractedFiles;
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;

            Console.WriteLine($"\n? Processing complete!");
            Console.WriteLine($"  Output: {finalOutputPath}");
            Console.WriteLine($"  Pages: {result.PagesProcessed}");
            if (extractedFiles.Any())
            {
                Console.WriteLine($"  Extracted: {extractedFiles.Count} raw form(s)");
            }
            Console.WriteLine($"  Time: {result.ProcessingTime.TotalSeconds:F2}s");

            foreach (var warning in result.Warnings)
                await LogWarningAsync(warning);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
            Console.WriteLine($"\n? Processing failed: {ex}");
            await LogErrorAsync($"Scanned sheet processing failed for '{inputPdf}'", ex);
        }
        finally
        {
            // Clean up the working folder (contains all temporary files)
            await Task.Delay(100); // Brief delay to ensure file handles released
            PdfService.CleanupDirectory(workingFolder);
        }

        return result;
    }

    /// <summary>
    /// Renames the output (and optionally archives the input) based on the secondary QR value.
    /// Failures here never fail the job; the files simply keep their original names.
    /// </summary>
    private string ApplyDynamicNaming(string inputPdf, string outputPdf, string? secondaryQrValue, List<string> warnings)
    {
        if (secondaryQrValue == null || _secondaryQrScanConfig == null)
            return outputPdf;

        var extractedValue = ExtractFileNameValue(secondaryQrValue);
        if (extractedValue.Length == 0)
        {
            Console.WriteLine($"  ? Extracted value is empty, keeping original filenames");
            return outputPdf;
        }

        var finalOutputPath = outputPdf;
        var pattern = _secondaryQrScanConfig.FileNameReplacementPattern;

        // Rename output file
        try
        {
            var newFileName = BuildDynamicFileName(Path.GetFileName(outputPdf), extractedValue, pattern);
            var candidate = Path.Combine(Path.GetDirectoryName(outputPdf) ?? "", newFileName);

            if (!string.Equals(candidate, outputPdf, StringComparison.OrdinalIgnoreCase) && File.Exists(outputPdf))
            {
                finalOutputPath = MakeUniquePath(candidate);
                File.Move(outputPdf, finalOutputPath);
                Console.WriteLine($"\n  ? File Naming:");
                Console.WriteLine($"    Original: {Path.GetFileName(outputPdf)}");
                Console.WriteLine($"    New: {Path.GetFileName(finalOutputPath)}");
                Console.WriteLine($"    Extracted: '{extractedValue}' from '{secondaryQrValue}'");
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Failed to rename output file '{outputPdf}': {ex.Message}");
            return outputPdf;
        }

        // Rename and move input file if configured
        if (_secondaryQrScanConfig.RenameInputFiles &&
            !string.IsNullOrWhiteSpace(_secondaryQrScanConfig.ArchiveFolder) &&
            File.Exists(inputPdf))
        {
            try
            {
                Directory.CreateDirectory(_secondaryQrScanConfig.ArchiveFolder);

                var newInputFileName = BuildDynamicFileName(Path.GetFileName(inputPdf), extractedValue, pattern);
                var archivePath = MakeUniquePath(Path.Combine(_secondaryQrScanConfig.ArchiveFolder, newInputFileName));

                File.Move(inputPdf, archivePath);
                Console.WriteLine($"\n  ? Input Archive:");
                Console.WriteLine($"    Original: {Path.GetFileName(inputPdf)}");
                Console.WriteLine($"    Archived as: {Path.GetFileName(archivePath)}");
                Console.WriteLine($"    Location: {_secondaryQrScanConfig.ArchiveFolder}");
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to archive input file '{inputPdf}': {ex.Message}");
                Console.WriteLine($"\n  ? Failed to rename/move input file: {ex.Message}");
                Console.WriteLine($"    Input file remains at: {inputPdf}");
            }
        }

        return finalOutputPath;
    }

    private async Task<(string processedPagePdf, string? secondaryQrValue, RawFormExtractionInfo? extractionInfo)> ProcessSinglePageAsync(
        string inputPagePdf,
        List<string> templatePages,
        string workingFolder,
        int pageNumber,
        List<string> warnings)
    {
        var pageFolder = Path.Combine(workingFolder, $"page_{pageNumber}");
        Directory.CreateDirectory(pageFolder);

        // Convert input page to image
        var inputImage = Path.Combine(pageFolder, "input.png");
        await _pdfService.ConvertPageToImageAsync(inputPagePdf, 1, inputImage, _dpi);

        // Deskew the input image; if deskewing fails, continue with the raw page image
        var deskewedImage = Path.Combine(pageFolder, "deskewed.png");
        try
        {
            await _deskewer.DeskewImageAsync(inputImage, deskewedImage);
        }
        catch (Exception ex)
        {
            warnings.Add($"Page {pageNumber}: deskew failed ({ex.Message}) - using original page image");
            Console.WriteLine($"  ? Deskew failed: {ex.Message} - using original page image");
            deskewedImage = inputImage;
        }

        // Try to scan QR code
        string? qrCode = null;
        string? secondaryQrValue = null;
        int? templatePageIndex = null;
        RawFormExtractionInfo? extractionInfo = null;

        if (_qrScanner != null)
        {
            try
            {
                qrCode = _qrScanner.ScanRegion(deskewedImage, _qrRegionX, _qrRegionY, _qrRegionWidth, _qrRegionHeight);

                if (qrCode != null)
                {
                    Console.WriteLine($"  QR Code: {qrCode}");

                    // Check if this QR code triggers extraction
                    if (_rawFormExtractionConfig?.ShouldExtract(qrCode) == true)
                    {
                        Console.WriteLine($"  ? Raw form extraction triggered by QR: {qrCode}");
                        extractionInfo = new RawFormExtractionInfo
                        {
                            PageNumber = pageNumber,
                            PrimaryQrCode = qrCode
                            // ProcessedPagePdf will be set after processing completes
                        };
                    }

                    templatePageIndex = FindTemplatePageForQr(qrCode);

                    if (templatePageIndex.HasValue)
                    {
                        Console.WriteLine($"  ? Mapped to template page {templatePageIndex.Value}");
                        if (templatePageIndex.Value < 0 || templatePageIndex.Value >= templatePages.Count)
                        {
                            warnings.Add($"Page {pageNumber}: QR '{qrCode}' maps to template page {templatePageIndex.Value}, " +
                                         $"but the template only has {templatePages.Count} pages - page left unchanged");
                            templatePageIndex = null;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  ? QR code not mapped - page will remain unchanged");
                    }

                    // Check if this QR triggers secondary scan for file naming
                    if (_secondaryQrScanConfig != null &&
                        qrCode.Equals(_secondaryQrScanConfig.TriggerQrCode, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"  ? Trigger QR detected, scanning secondary region...");
                        secondaryQrValue = await ScanSecondaryQrRegion(deskewedImage);

                        Console.WriteLine(secondaryQrValue != null
                            ? $"  ? Secondary QR found: {secondaryQrValue}"
                            : $"  ? No secondary QR found in region");
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

        // Attempt template alignment when the page is mapped; any failure in the
        // red-removal/alignment pipeline falls back to the deskewed page so a single
        // bad page cannot fail the whole job.
        if (templatePageIndex.HasValue)
        {
            try
            {
                var alignedPdf = await AlignPageToTemplateAsync(
                    deskewedImage, templatePages[templatePageIndex.Value], pageFolder, qrCode!, extractionInfo != null);

                if (extractionInfo != null)
                    extractionInfo.ProcessedPagePdf = alignedPdf;

                return (alignedPdf, secondaryQrValue, extractionInfo);
            }
            catch (Exception ex)
            {
                warnings.Add($"Page {pageNumber}: alignment failed ({ex.Message}) - using deskewed page unmodified");
                Console.WriteLine($"  ? Alignment failed: {ex.Message} - using deskewed page unmodified");
            }
        }

        // No valid template mapping (or alignment failed): return the deskewed page as-is
        var unchangedPdf = Path.Combine(pageFolder, "output.pdf");
        await _pdfService.ConvertImageToPdfAsync(deskewedImage, unchangedPdf);

        if (extractionInfo != null)
            extractionInfo.ProcessedPagePdf = unchangedPdf;

        return (unchangedPdf, secondaryQrValue, extractionInfo);
    }

    /// <summary>
    /// Runs optional red pixel removal and template alignment for a mapped page,
    /// returning the path of the finished single-page PDF.
    /// </summary>
    private async Task<string> AlignPageToTemplateAsync(
        string deskewedImage,
        string templatePagePdf,
        string pageFolder,
        string qrCode,
        bool isExtractionPage)
    {
        var templateImage = Path.Combine(pageFolder, "template.png");
        await _pdfService.ConvertPageToImageAsync(templatePagePdf, 1, templateImage, _dpi);

        // Determine if red removal should be applied
        var shouldApplyRedRemoval = _redPixelRemover != null &&
                                    !_qrValuesExcludingRedRemoval.Any(pattern => WildcardMatcher.Matches(qrCode, pattern));

        // Skip red removal if this page is flagged for extraction and config says to skip
        if (isExtractionPage && _rawFormExtractionConfig?.SkipRedRemoval == true)
        {
            shouldApplyRedRemoval = false;
            Console.WriteLine($"  ? Skipping red removal for extracted raw form");
        }

        var imageToAlign = deskewedImage;
        if (shouldApplyRedRemoval)
        {
            Console.WriteLine($"  ? Applying red pixel removal");

            // Filter exclusion regions that apply to this QR code
            var applicableRegions = _redPixelExclusionRegions
                .Where(r => r.AppliesTo(qrCode))
                .ToList();

            if (applicableRegions.Any())
            {
                Console.WriteLine($"  ? Applying {applicableRegions.Count} exclusion region(s) for QR: {qrCode}");
                foreach (var region in applicableRegions)
                {
                    Console.WriteLine($"    - {region.Name}: ({region.XInches}\", {region.YInches}\") {region.WidthInches}\" x {region.HeightInches}\"");
                }
            }

            var redRemovedImage = Path.Combine(pageFolder, "red_removed.png");
            await _redPixelRemover!.RemoveRedPixelsAsync(deskewedImage, redRemovedImage, _redThreshold, _dpi, applicableRegions);
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

    private int? FindTemplatePageForQr(string qrCode)
    {
        // Try exact match first
        if (_qrMapping.TryGetValue(qrCode, out int pageIndex))
            return pageIndex;

        // Try wildcard patterns
        foreach (var kvp in _qrMapping)
        {
            if (WildcardMatcher.Matches(qrCode, kvp.Key))
                return kvp.Value;
        }

        return null;
    }

    /// <summary>
    /// Scans a secondary QR code region on the page for file naming purposes.
    /// </summary>
    private async Task<string?> ScanSecondaryQrRegion(string imagePath)
    {
        if (_qrScanner == null || _secondaryQrScanConfig == null)
            return null;

        try
        {
            var (x, y, width, height) = _secondaryQrScanConfig.ToPixelCoordinates(_dpi);
            return await Task.Run(() => _qrScanner.ScanRegion(imagePath, x, y, width, height));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? Secondary QR scan error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extracts a raw form sheet as a separate PDF file.
    /// </summary>
    /// <param name="extractionInfo">Information about the page to extract</param>
    /// <param name="baseOutputPath">Base output path for naming reference</param>
    /// <param name="outputFolder">Folder where extracted file should be saved</param>
    /// <param name="extractionCount">Total number of extractions (for filename uniqueness)</param>
    /// <returns>Path to the extracted file</returns>
    private string ExtractRawFormSheet(
        RawFormExtractionInfo extractionInfo,
        string baseOutputPath,
        string outputFolder,
        int extractionCount)
    {
        if (_rawFormExtractionConfig == null)
            throw new InvalidOperationException("Raw form extraction config is not initialized");

        // Determine output folder
        var targetFolder = _rawFormExtractionConfig.ExtractToSeparateFolder &&
                           !string.IsNullOrWhiteSpace(_rawFormExtractionConfig.ExtractionFolder)
            ? _rawFormExtractionConfig.ExtractionFolder
            : outputFolder;

        Directory.CreateDirectory(targetFolder);

        // Build filename: prefer the secondary QR value, fall back to the main output's name
        var baseFileName = Path.GetFileNameWithoutExtension(baseOutputPath);
        if (!string.IsNullOrEmpty(extractionInfo.SecondaryQrValue))
        {
            var extractedValue = ExtractFileNameValue(extractionInfo.SecondaryQrValue);
            if (extractedValue.Length > 0)
                baseFileName = extractedValue;
        }

        var fileName = $"{baseFileName}_{_rawFormExtractionConfig.FileNameSuffix}";

        // Add page number if configured and there are multiple extractions
        if (_rawFormExtractionConfig.IncludePageNumberInFileName && extractionCount > 1)
        {
            fileName += $"_Page{extractionInfo.PageNumber}";
        }

        var extractedFilePath = MakeUniquePath(Path.Combine(targetFolder, fileName + ".pdf"));

        // Copy the processed page to the extraction location
        File.Copy(extractionInfo.ProcessedPagePdf, extractedFilePath, overwrite: false);

        Console.WriteLine($"\n  ? Raw Form Extracted:");
        Console.WriteLine($"    Page: {extractionInfo.PageNumber}");
        Console.WriteLine($"    QR Code: {extractionInfo.PrimaryQrCode}");
        Console.WriteLine($"    File: {Path.GetFileName(extractedFilePath)}");
        Console.WriteLine($"    Location: {targetFolder}");

        return extractedFilePath;
    }

    /// <summary>
    /// Extracts the filename-safe portion of a secondary QR value: the text before the
    /// first colon, sanitized for use in a filename.
    /// </summary>
    private static string ExtractFileNameValue(string secondaryQrValue)
    {
        var colonIndex = secondaryQrValue.IndexOf(':');
        var value = colonIndex >= 0 ? secondaryQrValue[..colonIndex] : secondaryQrValue;
        return SanitizeFileName(value.Trim());
    }

    /// <summary>
    /// Replaces the replacement pattern at the start of a filename with the extracted value,
    /// or prepends the value when the pattern is not present.
    /// </summary>
    private static string BuildDynamicFileName(string fileName, string extractedValue, string replacementPattern)
    {
        if (!string.IsNullOrEmpty(replacementPattern) &&
            fileName.StartsWith(replacementPattern, StringComparison.OrdinalIgnoreCase))
        {
            return extractedValue + fileName[replacementPattern.Length..];
        }

        return $"{extractedValue}_{Path.GetFileNameWithoutExtension(fileName)}{Path.GetExtension(fileName)}";
    }

    /// <summary>
    /// Returns the given path, or a timestamped variant when a file already exists there.
    /// </summary>
    private static string MakeUniquePath(string path)
    {
        if (!File.Exists(path))
            return path;

        var directory = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        var candidate = Path.Combine(directory, $"{name}_{timestamp}{extension}");
        var counter = 1;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{name}_{timestamp}_{counter++}{extension}");
        }

        return candidate;
    }

    /// <summary>
    /// Sanitizes a string for use in a filename by removing invalid characters
    /// and limiting length.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "";

        // Remove invalid filename characters
        var sanitized = string.Concat(fileName.Split(Path.GetInvalidFileNameChars()));

        // Replace spaces with underscores
        sanitized = sanitized.Replace(' ', '_');

        // Remove any remaining problematic characters
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[^\w\-_]", "");

        // Limit length to 50 characters
        if (sanitized.Length > 50)
            sanitized = sanitized[..50];

        return sanitized;
    }

    private Task LogWarningAsync(string message)
        => _loggingService?.LogWarningAsync(message) ?? Task.CompletedTask;

    private Task LogErrorAsync(string message, Exception ex)
        => _loggingService?.LogErrorAsync(message, ex) ?? Task.CompletedTask;
}
