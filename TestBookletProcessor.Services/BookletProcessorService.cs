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

public class BookletProcessorService
{
    private readonly IPdfService _pdfService;
    private readonly IDeskewer _deskewer;
    private readonly IImageAligner _aligner;
    private readonly IRedPixelRemoverService? _redPixelRemover;
    private readonly RegionQrScanner? _qrScanner;
    private readonly byte _redThreshold;
    private readonly int _dpi;
    private readonly bool _enableQrScanning;
    private readonly int _qrRegionX;
    private readonly int _qrRegionY;
    private readonly int _qrRegionWidth;
    private readonly int _qrRegionHeight;
    private readonly List<string> _qrValuesExcludingRedRemoval;

    public BookletProcessorService(
        IPdfService pdfService,
        IDeskewer deskewer,
        IImageAligner aligner,
        IRedPixelRemoverService? redPixelRemover = null,
        byte redThreshold = 200,
        int dpi = 300,
        RegionQrScanner? qrScanner = null,
        bool enableQrScanning = false,
        double qrRegionXInches = 6.5,
        double qrRegionYInches = 9.0,
        double qrRegionWidthInches = 2.0,
        double qrRegionHeightInches = 2.0,
        List<string>? qrValuesExcludingRedRemoval = null)
    {
        _pdfService = pdfService;
        _deskewer = deskewer;
        _aligner = aligner;
        _redPixelRemover = redPixelRemover;
        _qrScanner = qrScanner;
        _redThreshold = redThreshold;
        _dpi = dpi;
        _enableQrScanning = enableQrScanning;
        
        // Calculate pixel values from inches and DPI
        _qrRegionX = (int)(qrRegionXInches * dpi);
        _qrRegionY = (int)(qrRegionYInches * dpi);
        _qrRegionWidth = (int)(qrRegionWidthInches * dpi);
        _qrRegionHeight = (int)(qrRegionHeightInches * dpi);
        
        _qrValuesExcludingRedRemoval = qrValuesExcludingRedRemoval ??
                                       new List<string> { "MACHINE_SCORED", "NO_RED_INK", "CLEAN" };
    }

    public async Task<ProcessingResult> ProcessBookletsWorkflowAsync(
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
        var bookletWorkingFolders = new List<string>();

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
                bookletWorkingFolders.Add(bookletWorkingFolder);
                var processedBookletOutput = Path.Combine(bookletWorkingFolder, "processed_booklet.pdf");
                await ProcessBookletAsync(templatePdf, bookletPath, bookletWorkingFolder, processedBookletOutput, _dpi);
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
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
        }
        finally
        {
            // Clean up the entire job temp folder (contains both booklets and booklet working folders)
            PdfService.CleanupDirectory(jobTempFolder);
        }

        return result;
    }

    public async Task ProcessBookletAsync(string templatePdf, string inputPdf, string workingFolder, string outputPdf,
        int dpi)
    {
        //1. Split both PDFs
        var templatePages = await _pdfService.SplitPdfAsync(templatePdf, Path.Combine(workingFolder, "template_pages"));
        var inputPages = await _pdfService.SplitPdfAsync(inputPdf, Path.Combine(workingFolder, "input_pages"));

        if (templatePages.Count != inputPages.Count)
            throw new InvalidOperationException("Template and input PDF must have the same number of pages.");

        var processedPdfPages = new List<string>();

        for (var i = 0; i < inputPages.Count; i++)
        {
            //2. Convert each page to image
            var templateImg = Path.Combine(workingFolder, "template_images", $"template_{i + 1}.png");
            var inputImg = Path.Combine(workingFolder, "input_images", $"input_{i + 1}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(templateImg)!);
            Directory.CreateDirectory(Path.GetDirectoryName(inputImg)!);
            await _pdfService.ConvertPageToImageAsync(templatePages[i], 1, templateImg, dpi);
            await _pdfService.ConvertPageToImageAsync(inputPages[i], 1, inputImg, dpi);

            //3. Deskew and align input image to template image
            var deskewedImg = Path.Combine(workingFolder, "deskewed_images", $"deskewed_{i + 1}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(deskewedImg)!);
            await _deskewer.DeskewImageAsync(inputImg, deskewedImg);

            // Scan QR code after deskewing to determine if red pixel removal is needed
            string? qrCodeValue = null;
            var shouldApplyRedRemoval = _redPixelRemover != null; // Default behavior

            if (_enableQrScanning && _qrScanner != null)
                try
                {
                    qrCodeValue = _qrScanner.ScanRegion(deskewedImg, _qrRegionX, _qrRegionY, _qrRegionWidth,
                        _qrRegionHeight);

                    if (qrCodeValue != null)
                    {
                        Console.WriteLine($"Page {i + 1}: QR code detected: {qrCodeValue}");

                        // Check if QR code value matches any exclusion pattern (supports wildcards)
                        var qrMatchesExclusionList = _qrValuesExcludingRedRemoval.Any(pattern =>
                            MatchesWildcard(qrCodeValue, pattern, ignoreCase: true));

                        shouldApplyRedRemoval = _redPixelRemover != null && !qrMatchesExclusionList;

                        if (qrMatchesExclusionList)
                            Console.WriteLine(
                                $"Page {i + 1}: QR code matches exclusion criteria - skipping red pixel removal");
                        else
                            Console.WriteLine(
                                $"Page {i + 1}: QR code does not match exclusion criteria - applying red pixel removal");
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

            var redRemovedImg = deskewedImg;
            if (shouldApplyRedRemoval)
            {
                redRemovedImg = Path.Combine(workingFolder, "red_removed_images", $"red_removed_{i + 1}.png");
                Directory.CreateDirectory(Path.GetDirectoryName(redRemovedImg)!);
                await _redPixelRemover!.RemoveRedPixelsAsync(deskewedImg, redRemovedImg, _redThreshold, dpi);
            }

            var alignedImg = Path.Combine(workingFolder, "aligned_images", $"aligned_{i + 1}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(alignedImg)!);
            await _aligner.AlignImageAsync(redRemovedImg, templateImg, alignedImg);

            //4. Convert processed image back to PDF
            var processedPdf = Path.Combine(workingFolder, "processed_pages", $"processed_{i + 1}.pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(processedPdf)!);
            await _pdfService.ConvertImageToPdfAsync(alignedImg, processedPdf);
            processedPdfPages.Add(processedPdf);
        }

        //5. Merge all processed PDFs into final output
        await _pdfService.MergePdfsAsync(processedPdfPages, outputPdf);
        Console.WriteLine($"Final output PDF created: {outputPdf}");
    }

    /// <summary>
    /// Checks if a value matches a wildcard pattern.
    /// Supports * for one or more characters.
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <param name="pattern">The pattern with wildcards (* for one or more characters)</param>
    /// <param name="ignoreCase">Whether to ignore case</param>
    /// <returns>True if the value matches the pattern</returns>
    private static bool MatchesWildcard(string value, string pattern, bool ignoreCase = true)
    {
        if (string.IsNullOrEmpty(pattern))
            return false;

        // Convert wildcard pattern to regex pattern
        // Escape special regex characters except *
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*") + "$";

        var options = ignoreCase 
            ? System.Text.RegularExpressions.RegexOptions.IgnoreCase 
            : System.Text.RegularExpressions.RegexOptions.None;

        return System.Text.RegularExpressions.Regex.IsMatch(value, regexPattern, options);
    }
}