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
    private readonly SecondaryQrScanConfig? _secondaryQrScanConfig;
    private readonly RawFormExtractionConfig? _rawFormExtractionConfig;

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
        List<RedPixelExclusionRegion>? redPixelExclusionRegions = null,
        SecondaryQrScanConfig? secondaryQrScanConfig = null,
        RawFormExtractionConfig? rawFormExtractionConfig = null)
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
        _secondaryQrScanConfig = secondaryQrScanConfig;
        _rawFormExtractionConfig = rawFormExtractionConfig;
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
        
        // Create unique working folder name (declare outside try for cleanup in finally)
        var uniqueId = Guid.NewGuid().ToString("N");
        var workingFolder = Path.Combine(outputFolder, $"temp_scannedsheets_{uniqueId}");

        try
        {
            Console.WriteLine("=== Scanned Sheet Processing Mode ===");
            Console.WriteLine($"Input: {inputPdf}");
            Console.WriteLine($"Template: {templatePdf}");
            Console.WriteLine($"QR Mappings: {qrMapping.Count} patterns");

            // Create working folder
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
                    qrMapping,
                    workingFolder,
                    pageNum,
                    dpi);

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
                    extractionInfo.SecondaryQrValue = scannedSecondaryQr ?? secondaryQrValue;
                    extractionPages.Add(extractionInfo);
                    Console.WriteLine($"  ? Page flagged for raw form extraction");
                }
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
                
                for (int i = 0; i < extractionPages.Count; i++)
                {
                    var extractionInfo = extractionPages[i];
                    var extractedPath = await ExtractRawFormSheet(
                        extractionInfo,
                        outputPdf,
                        outputFolder,
                        extractionPages.Count);
                    
                    extractionInfo.ExtractedFilePath = extractedPath;
                    extractedFiles.Add(extractedPath);
                }
                
                Console.WriteLine($"? {extractedFiles.Count} raw form(s) extracted successfully");
            }
            
            // Apply dynamic file naming if secondary QR was found
            string finalOutputPath = outputPdf;
            string? renamedInputPath = null;
            
            if (secondaryQrValue != null && _secondaryQrScanConfig != null)
            {
                // Rename output file
                finalOutputPath = ApplyDynamicFileName(
                    outputPdf, 
                    secondaryQrValue, 
                    _secondaryQrScanConfig.FileNameReplacementPattern);
                
                // Rename file if path changed
                if (finalOutputPath != outputPdf && File.Exists(outputPdf))
                {
                    File.Move(outputPdf, finalOutputPath);
                    Console.WriteLine($"  ? Output file renamed to: {Path.GetFileName(finalOutputPath)}");
                }
                
                // Rename and move input file if configured
                if (_secondaryQrScanConfig.RenameInputFiles && File.Exists(inputPdf))
                {
                    try
                    {
                        // Ensure archive folder exists
                        Directory.CreateDirectory(_secondaryQrScanConfig.ArchiveFolder);
                        
                        // Calculate new input filename
                        var inputFileName = Path.GetFileName(inputPdf);
                        var newInputFileName = ApplyDynamicFileNameToPath(
                            inputFileName,
                            secondaryQrValue,
                            _secondaryQrScanConfig.FileNameReplacementPattern);
                        
                        // Construct full archive path
                        renamedInputPath = Path.Combine(_secondaryQrScanConfig.ArchiveFolder, newInputFileName);
                        
                        // Handle file conflicts
                        if (File.Exists(renamedInputPath))
                        {
                            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(renamedInputPath);
                            var extension = Path.GetExtension(renamedInputPath);
                            renamedInputPath = Path.Combine(
                                _secondaryQrScanConfig.ArchiveFolder,
                                $"{fileNameWithoutExt}_{timestamp}{extension}");
                        }
                        
                        // Move and rename input file
                        File.Move(inputPdf, renamedInputPath);
                        Console.WriteLine($"\n  ? Input Archive:");
                        Console.WriteLine($"    Original: {Path.GetFileName(inputPdf)}");
                        Console.WriteLine($"    Archived as: {Path.GetFileName(renamedInputPath)}");
                        Console.WriteLine($"    Location: {_secondaryQrScanConfig.ArchiveFolder}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\n  ? Failed to rename/move input file: {ex.Message}");
                        Console.WriteLine($"    Input file remains at: {inputPdf}");
                    }
                }
            }

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
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
            Console.WriteLine($"\n? Processing failed: {ex.Message}");
        }
        finally
        {
            // Clean up the working folder (contains all temporary files)
            // Use async cleanup with delay to ensure files are released
            await Task.Delay(100); // Brief delay to ensure file handles released
            PdfService.CleanupDirectory(workingFolder);
        }

        return result;
    }

    private async Task<(string processedPagePdf, string? secondaryQrValue, RawFormExtractionInfo? extractionInfo)> ProcessSinglePageAsync(
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
        string? secondaryQrValue = null;
        int? templatePageIndex = null;
        RawFormExtractionInfo? extractionInfo = null;

        if (_enableQrScanning && _qrScanner != null)
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
                    
                    templatePageIndex = FindTemplatePageForQr(qrCode, qrMapping);
                    
                    if (templatePageIndex.HasValue)
                    {
                        Console.WriteLine($"  ? Mapped to template page {templatePageIndex.Value}");
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
                        secondaryQrValue = await ScanSecondaryQrRegion(deskewedImage, dpi);
                        
                        if (secondaryQrValue != null)
                        {
                            Console.WriteLine($"  ? Secondary QR found: {secondaryQrValue}");
                        }
                        else
                        {
                            Console.WriteLine($"  ? No secondary QR found in region");
                        }
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
            
            // Set processed page path for extraction if needed
            if (extractionInfo != null)
            {
                extractionInfo.ProcessedPagePdf = unchangedPdf;
            }
            
            return (unchangedPdf, secondaryQrValue, extractionInfo);
        }

        // Get the corresponding template page
        var templatePagePdf = templatePages[templatePageIndex.Value];
        var templateImage = Path.Combine(pageFolder, "template.png");
        await _pdfService.ConvertPageToImageAsync(templatePagePdf, 1, templateImage, dpi);

        // Determine if red removal should be applied
        var shouldApplyRedRemoval = _redPixelRemover != null && qrCode != null &&
                                    !_qrValuesExcludingRedRemoval.Any(pattern =>
                                        MatchesWildcard(qrCode, pattern, ignoreCase: true));

        // Skip red removal if this page is flagged for extraction and config says to skip
        if (extractionInfo != null && _rawFormExtractionConfig?.SkipRedRemoval == true)
        {
            shouldApplyRedRemoval = false;
            Console.WriteLine($"  ? Skipping red removal for extracted raw form");
        }

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
                        Console.WriteLine($"    - {region.Name}: ({region.XInches}\", {region.YInches}\") {region.WidthInches}\" � {region.HeightInches}\"");
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

        // Set processed page path for extraction if needed
        if (extractionInfo != null)
        {
            extractionInfo.ProcessedPagePdf = outputPdf;
        }

        return (outputPdf, secondaryQrValue, extractionInfo);
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

    /// <summary>
    /// Scans a secondary QR code region on the page for file naming purposes.
    /// </summary>
    private async Task<string?> ScanSecondaryQrRegion(string imagePath, int dpi)
    {
        if (_qrScanner == null || _secondaryQrScanConfig == null)
            return null;

        try
        {
            var (x, y, width, height) = _secondaryQrScanConfig.ToPixelCoordinates(dpi);
            
            // Scan the secondary region
            var qrValue = await Task.Run(() => 
                _qrScanner.ScanRegion(imagePath, x, y, width, height));
            
            return qrValue;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ? Secondary QR scan error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Applies dynamic file naming by extracting the portion before the colon from the QR value
    /// and replacing the pattern in the filename.
    /// </summary>
    private string ApplyDynamicFileName(
        string originalPath, 
        string secondaryQrValue, 
        string replacementPattern)
    {
        // Extract portion before colon (colon is always present per requirements)
        var colonIndex = secondaryQrValue.IndexOf(':');
        string extractedValue;
        
        if (colonIndex >= 0)
        {
            extractedValue = secondaryQrValue.Substring(0, colonIndex).Trim();
        }
        else
        {
            // Fallback if no colon found (shouldn't happen per requirements)
            extractedValue = secondaryQrValue.Trim();
        }
        
        // Sanitize for filename use
        extractedValue = SanitizeFileName(extractedValue);
        
        if (string.IsNullOrEmpty(extractedValue))
        {
            Console.WriteLine($"  ? Extracted value is empty, using original filename");
            return originalPath;
        }
        
        // Get directory and filename
        var directory = Path.GetDirectoryName(originalPath) ?? "";
        var fileName = Path.GetFileName(originalPath);
        
        // Replace pattern in filename (case-insensitive, at start of filename)
        string newFileName;
        if (fileName.StartsWith(replacementPattern, StringComparison.OrdinalIgnoreCase))
        {
            // Pattern is at the start - replace it
            newFileName = extractedValue + fileName.Substring(replacementPattern.Length);
        }
        else
        {
            // Pattern not found - prepend extracted value
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            newFileName = $"{extractedValue}_{fileNameWithoutExt}{extension}";
        }
        
        var newPath = Path.Combine(directory, newFileName);
        
        Console.WriteLine($"\n  ? File Naming:");
        Console.WriteLine($"    Original: {fileName}");
        Console.WriteLine($"    New: {newFileName}");
        Console.WriteLine($"    Extracted: '{extractedValue}' from '{secondaryQrValue}'");
        
        return newPath;
    }
    
    /// <summary>
    /// Applies dynamic file naming to a filename (without directory path).
    /// Used for renaming input files that will be moved to archive folder.
    /// </summary>
    private string ApplyDynamicFileNameToPath(
        string fileName,
        string secondaryQrValue,
        string replacementPattern)
    {
        // Extract portion before colon
        var colonIndex = secondaryQrValue.IndexOf(':');
        string extractedValue;
        
        if (colonIndex >= 0)
        {
            extractedValue = secondaryQrValue.Substring(0, colonIndex).Trim();
        }
        else
        {
            extractedValue = secondaryQrValue.Trim();
        }
        
        // Sanitize for filename use
        extractedValue = SanitizeFileName(extractedValue);
        
        if (string.IsNullOrEmpty(extractedValue))
        {
            return fileName; // Return original if extraction failed
        }
        
        // Replace pattern in filename (case-insensitive, at start of filename)
        string newFileName;
        if (fileName.StartsWith(replacementPattern, StringComparison.OrdinalIgnoreCase))
        {
            // Pattern is at the start - replace it
            newFileName = extractedValue + fileName.Substring(replacementPattern.Length);
        }
        else
        {
            // Pattern not found - prepend extracted value
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            newFileName = $"{extractedValue}_{fileNameWithoutExt}{extension}";
        }
        
        return newFileName;
    }

    /// <summary>
    /// Extracts a raw form sheet as a separate PDF file.
    /// /// </summary>
    /// <param name="extractionInfo">Information about the page to extract</param>
    /// <param name="baseOutputPath">Base output path for naming reference</param>
    /// <param name="outputFolder">Folder where extracted file should be saved</param>
    /// <param name="extractionCount">Number of extractions so far (for uniqueness)</param>
    /// <returns>Path to the extracted file</returns>
    private async Task<string> ExtractRawFormSheet(
        RawFormExtractionInfo extractionInfo,
        string baseOutputPath,
        string outputFolder,
        int extractionCount)
    {
        if (_rawFormExtractionConfig == null)
            throw new InvalidOperationException("Raw form extraction config is not initialized");

        // Determine output folder
        var targetFolder = _rawFormExtractionConfig.ExtractToSeparateFolder
            ? _rawFormExtractionConfig.ExtractionFolder
            : outputFolder;

        // Ensure target folder exists
        Directory.CreateDirectory(targetFolder);

        // Build filename
        string baseFileName;
        
        if (!string.IsNullOrEmpty(extractionInfo.SecondaryQrValue))
        {
            // Use secondary QR value for naming
            var colonIndex = extractionInfo.SecondaryQrValue.IndexOf(':');
            string extractedValue;
            
            if (colonIndex >= 0)
            {
                extractedValue = extractionInfo.SecondaryQrValue.Substring(0, colonIndex).Trim();
            }
            else
            {
                extractedValue = extractionInfo.SecondaryQrValue.Trim();
            }
            
            extractedValue = SanitizeFileName(extractedValue);
            baseFileName = string.IsNullOrEmpty(extractedValue) 
                ? Path.GetFileNameWithoutExtension(baseOutputPath) 
                : extractedValue;
        }
        else
        {
            // Use base output filename
            baseFileName = Path.GetFileNameWithoutExtension(baseOutputPath);
        }

        // Add suffix
        var suffix = _rawFormExtractionConfig.FileNameSuffix;
        var fileName = $"{baseFileName}_{suffix}";

        // Add page number if configured and there are multiple extractions
        if (_rawFormExtractionConfig.IncludePageNumberInFileName && extractionCount > 1)
        {
            fileName += $"_Page{extractionInfo.PageNumber}";
        }

        fileName += ".pdf";

        var extractedFilePath = Path.Combine(targetFolder, fileName);

        // Handle file conflicts
        if (File.Exists(extractedFilePath))
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(extractedFilePath);
            extractedFilePath = Path.Combine(targetFolder, $"{fileNameWithoutExt}_{timestamp}.pdf");
        }

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
    /// Sanitizes a string for use in a filename by removing invalid characters
    /// and limiting length.
    /// </summary>
    private string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "";

        // Remove invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(fileName.Split(invalidChars));
        
        // Replace spaces with underscores
        sanitized = sanitized.Replace(' ', '_');
        
        // Remove any remaining problematic characters
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[^\w\-_]", "");
        
        // Limit length to 50 characters
        if (sanitized.Length > 50)
            sanitized = sanitized.Substring(0, 50);
        
        return sanitized;
    }
}

