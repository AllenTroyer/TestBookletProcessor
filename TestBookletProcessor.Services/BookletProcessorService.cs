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

public class BookletProcessorService
{
    private readonly IPdfService _pdfService;
    private readonly IDeskewer _deskewer;
    private readonly IImageAligner _aligner;
    private readonly IRedPixelRemoverService? _redPixelRemover;
    private readonly RegionQrScanner? _qrScanner;
    private readonly IScannedSheetProcessor? _scannedSheetProcessor;
    private readonly ILoggingService? _loggingService;
    private readonly int _dpi;
    private readonly byte _redThreshold;
    private readonly int _qrRegionX;
    private readonly int _qrRegionY;
    private readonly int _qrRegionWidth;
    private readonly int _qrRegionHeight;
    private readonly List<string> _qrValuesExcludingRedRemoval;
    private readonly List<string> _templateExclusionPatterns;
    private readonly string _scannedSheetTemplateName;

    public BookletProcessorService(
        IPdfService pdfService,
        IDeskewer deskewer,
        IImageAligner aligner,
        IRedPixelRemoverService? redPixelRemover,
        RegionQrScanner? qrScanner,
        BookletProcessorOptions options,
        IScannedSheetProcessor? scannedSheetProcessor = null,
        ILoggingService? loggingService = null)
    {
        _pdfService = pdfService;
        _deskewer = deskewer;
        _aligner = aligner;
        _redPixelRemover = redPixelRemover;
        _qrScanner = qrScanner;
        _scannedSheetProcessor = scannedSheetProcessor;
        _loggingService = loggingService;

        _dpi = options.DefaultDpi;
        _redThreshold = options.RedPixelThreshold;
        _qrRegionX = (int)(options.QrScanner.QrRegionXInches * _dpi);
        _qrRegionY = (int)(options.QrScanner.QrRegionYInches * _dpi);
        _qrRegionWidth = (int)(options.QrScanner.QrRegionWidthInches * _dpi);
        _qrRegionHeight = (int)(options.QrScanner.QrRegionHeightInches * _dpi);
        _qrValuesExcludingRedRemoval = options.QrScanner.QrValuesExcludingRedRemoval;
        _templateExclusionPatterns = options.TemplateExclusionPatterns;
        _scannedSheetTemplateName = options.ScannedSheets.TemplateName;
    }

    public async Task<ProcessingResult> ProcessBookletsWorkflowAsync(
        string inputPdf,
        string templatePdf,
        string outputFolder,
        Action<int, int>? statusCallback = null)
    {
        // Auto-detect processing mode based on template name
        if (IsScannedSheetMode(templatePdf))
        {
            Console.WriteLine("Auto-detected: Scanned Sheet Mode");
            return await ProcessScannedSheetWorkflowAsync(inputPdf, templatePdf, outputFolder, statusCallback);
        }

        Console.WriteLine("Processing Mode: Booklet Mode");
        return await ProcessBookletWorkflowAsync(inputPdf, templatePdf, outputFolder, statusCallback);
    }

    private bool IsScannedSheetMode(string templatePdf)
    {
        if (string.IsNullOrEmpty(_scannedSheetTemplateName))
            return false;

        var templateFileName = Path.GetFileName(templatePdf);
        return templateFileName.Equals(_scannedSheetTemplateName, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ProcessingResult> ProcessScannedSheetWorkflowAsync(
        string inputPdf,
        string templatePdf,
        string outputFolder,
        Action<int, int>? statusCallback = null)
    {
        if (_scannedSheetProcessor == null)
        {
            return new ProcessingResult
            {
                Success = false,
                ErrorMessage = "Scanned sheet processor not initialized"
            };
        }

        var inputFileNameNoExt = Path.GetFileNameWithoutExtension(inputPdf);
        var finalOutputPdf = Path.Combine(outputFolder, $"{inputFileNameNoExt}_aligned.pdf");

        return await _scannedSheetProcessor.ProcessScannedSheetsAsync(
            inputPdf,
            templatePdf,
            outputFolder,
            finalOutputPdf,
            statusCallback);
    }

    private async Task<ProcessingResult> ProcessBookletWorkflowAsync(
        string inputPdf,
        string templatePdf,
        string outputFolder,
        Action<int, int>? statusCallback = null)
    {
        var result = new ProcessingResult();
        var stopwatch = Stopwatch.StartNew();

        // Generate unique folder names based on input file name to prevent conflicts when processing multiple jobs simultaneously
        var inputFileNameNoExt = Path.GetFileNameWithoutExtension(inputPdf);
        var uniqueId = $"{inputFileNameNoExt}_{Guid.NewGuid():N}";
        var jobTempFolder = Path.Combine(outputFolder, $"temp_{uniqueId}");
        var bookletsFolder = Path.Combine(jobTempFolder, "booklets");

        try
        {
            var finalOutputPdf = Path.Combine(outputFolder, $"{inputFileNameNoExt}_aligned.pdf");
            Directory.CreateDirectory(outputFolder);

            // Split input PDF into booklets
            var bookletPaths = await _pdfService.SplitIntoBookletsAsync(inputPdf, templatePdf, bookletsFolder);
            var processedBookletPaths = new List<string>();
            var totalBooklets = bookletPaths.Count;
            var bookletIndex = 1;
            foreach (var bookletPath in bookletPaths)
            {
                statusCallback?.Invoke(bookletIndex, totalBooklets);
                var bookletWorkingFolder = Path.Combine(jobTempFolder, $"booklet_{bookletIndex}");
                var processedBookletOutput = Path.Combine(bookletWorkingFolder, "processed_booklet.pdf");
                await ProcessBookletAsync(templatePdf, bookletPath, bookletWorkingFolder, processedBookletOutput,
                    bookletIndex, result.Warnings);
                processedBookletPaths.Add(processedBookletOutput);
                bookletIndex++;
            }

            // Merge all processed booklets into the final output
            await _pdfService.MergePdfsAsync(processedBookletPaths, finalOutputPdf);
            result.Success = true;
            result.OutputPath = finalOutputPdf;
            result.PagesProcessed = processedBookletPaths.Count;
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;

            foreach (var warning in result.Warnings)
                await LogWarningAsync(warning);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
            Console.WriteLine($"Booklet processing failed: {ex}");
            await LogErrorAsync($"Booklet processing failed for '{inputPdf}'", ex);
        }
        finally
        {
            // Clean up the entire job temp folder (contains both booklets and booklet working folders)
            PdfService.CleanupDirectory(jobTempFolder);
        }

        return result;
    }

    private async Task ProcessBookletAsync(
        string templatePdf,
        string inputPdf,
        string workingFolder,
        string outputPdf,
        int bookletNumber,
        List<string> warnings)
    {
        // Check if template should be excluded from QR scanning and red removal
        var templateFileName = Path.GetFileNameWithoutExtension(templatePdf);
        var isTemplateExcluded = _templateExclusionPatterns.Any(pattern =>
            WildcardMatcher.Matches(templateFileName, pattern));

        if (isTemplateExcluded)
        {
            Console.WriteLine($"Template '{templateFileName}' matches exclusion pattern - skipping QR scanning and red pixel removal");
        }

        //1. Split both PDFs
        var templatePages = await _pdfService.SplitPdfAsync(templatePdf, Path.Combine(workingFolder, "template_pages"));
        var inputPages = await _pdfService.SplitPdfAsync(inputPdf, Path.Combine(workingFolder, "input_pages"));

        if (templatePages.Count != inputPages.Count)
            throw new InvalidOperationException("Template and input PDF must have the same number of pages.");

        var processedPdfPages = new List<string>();

        for (var i = 0; i < inputPages.Count; i++)
        {
            var pageLabel = $"Booklet {bookletNumber}, page {i + 1}";

            //2. Convert each page to image
            var templateImg = Path.Combine(workingFolder, "template_images", $"template_{i + 1}.png");
            var inputImg = Path.Combine(workingFolder, "input_images", $"input_{i + 1}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(templateImg)!);
            Directory.CreateDirectory(Path.GetDirectoryName(inputImg)!);
            await _pdfService.ConvertPageToImageAsync(templatePages[i], 1, templateImg, _dpi);
            await _pdfService.ConvertPageToImageAsync(inputPages[i], 1, inputImg, _dpi);

            //3. Deskew the input image; fall back to the raw page image on failure
            var deskewedImg = Path.Combine(workingFolder, "deskewed_images", $"deskewed_{i + 1}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(deskewedImg)!);
            try
            {
                await _deskewer.DeskewImageAsync(inputImg, deskewedImg);
            }
            catch (Exception ex)
            {
                warnings.Add($"{pageLabel}: deskew failed ({ex.Message}) - using original page image");
                deskewedImg = inputImg;
            }

            // Scan QR code after deskewing to determine if red pixel removal is needed
            var shouldApplyRedRemoval = _redPixelRemover != null && !isTemplateExcluded; // Default behavior, skip if template excluded

            if (_qrScanner != null && !isTemplateExcluded)
            {
                try
                {
                    var qrCodeValue = _qrScanner.ScanRegion(deskewedImg, _qrRegionX, _qrRegionY, _qrRegionWidth,
                        _qrRegionHeight);

                    if (qrCodeValue != null)
                    {
                        Console.WriteLine($"Page {i + 1}: QR code detected: {qrCodeValue}");

                        // Check if QR code value matches any exclusion pattern (supports wildcards)
                        var qrMatchesExclusionList = _qrValuesExcludingRedRemoval.Any(pattern =>
                            WildcardMatcher.Matches(qrCodeValue, pattern));

                        shouldApplyRedRemoval = _redPixelRemover != null && !qrMatchesExclusionList;

                        Console.WriteLine(qrMatchesExclusionList
                            ? $"Page {i + 1}: QR code matches exclusion criteria - skipping red pixel removal"
                            : $"Page {i + 1}: QR code does not match exclusion criteria - applying red pixel removal");
                    }
                    else
                    {
                        Console.WriteLine(
                            $"Page {i + 1}: No QR code detected - using default red pixel removal setting");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Page {i + 1}: QR scanning error: {ex.Message} - using default red pixel removal setting");
                }
            }

            var redRemovedImg = deskewedImg;
            if (shouldApplyRedRemoval)
            {
                try
                {
                    var candidate = Path.Combine(workingFolder, "red_removed_images", $"red_removed_{i + 1}.png");
                    Directory.CreateDirectory(Path.GetDirectoryName(candidate)!);
                    await _redPixelRemover!.RemoveRedPixelsAsync(deskewedImg, candidate, _redThreshold, _dpi);
                    redRemovedImg = candidate;
                }
                catch (Exception ex)
                {
                    warnings.Add($"{pageLabel}: red pixel removal failed ({ex.Message}) - using page without red removal");
                }
            }

            // Align to the template; fall back to the unaligned page on failure so one
            // bad page cannot fail the whole booklet
            var imageForPdf = redRemovedImg;
            try
            {
                var alignedImg = Path.Combine(workingFolder, "aligned_images", $"aligned_{i + 1}.png");
                Directory.CreateDirectory(Path.GetDirectoryName(alignedImg)!);
                await _aligner.AlignImageAsync(redRemovedImg, templateImg, alignedImg);
                imageForPdf = alignedImg;
            }
            catch (Exception ex)
            {
                warnings.Add($"{pageLabel}: alignment failed ({ex.Message}) - page included unaligned");
            }

            //4. Convert processed image back to PDF
            var processedPdf = Path.Combine(workingFolder, "processed_pages", $"processed_{i + 1}.pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(processedPdf)!);
            await _pdfService.ConvertImageToPdfAsync(imageForPdf, processedPdf);
            processedPdfPages.Add(processedPdf);
        }

        //5. Merge all processed PDFs into final output
        Directory.CreateDirectory(Path.GetDirectoryName(outputPdf)!);
        await _pdfService.MergePdfsAsync(processedPdfPages, outputPdf);
        Console.WriteLine($"Final output PDF created: {outputPdf}");
    }

    private Task LogWarningAsync(string message)
        => _loggingService?.LogWarningAsync(message) ?? Task.CompletedTask;

    private Task LogErrorAsync(string message, Exception ex)
        => _loggingService?.LogErrorAsync(message, ex) ?? Task.CompletedTask;
}
