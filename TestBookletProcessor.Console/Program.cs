using Microsoft.Extensions.Configuration;
using QrRegionScanner;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;
using TestBookletProcessor.Services;

partial class Program
{
    private static ILoggingService? _loggingService;
    
    static async Task Main(string[] args)
    {
        // Initialize logging service
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();
        
        _loggingService = CreateLoggingService(config);
        
        // Log application start
        var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        await _loggingService.LogApplicationStartAsync(appVersion);
        
        Console.WriteLine("=== Test Booklet Processor Console ===");
        Console.WriteLine("Select test mode:");
        Console.WriteLine("1. QR Code Scanner Test");
        Console.WriteLine("2. Booklet Processing Test");
        Console.Write("Enter choice (1 or 2): ");

        var choice = Console.ReadLine();

        if (choice == "1")
        {
            await TestQrCodeScanner();
        }
        else if (choice == "2")
        {
            await TestBookletProcessing();
        }
        else
        {
            Console.WriteLine("Invalid choice. Exiting.");
        }
        
        // Log application stop
        await _loggingService.LogApplicationStopAsync();
    }

    static ILoggingService CreateLoggingService(IConfigurationRoot config)
    {
        var loggingConfig = new LoggingServiceConfig();
        
        var logDir = config["Logging:LogDirectory"];
        if (!string.IsNullOrWhiteSpace(logDir))
        {
            loggingConfig.LogDirectory = logDir;
        }
        
        var format = config["Logging:Format"];
        if (Enum.TryParse<LogFormat>(format, true, out var logFormat))
        {
            loggingConfig.Format = logFormat;
        }
        
        var enableRotation = config["Logging:EnableLogRotation"];
        if (bool.TryParse(enableRotation, out var rotation))
        {
            loggingConfig.EnableLogRotation = rotation;
        }
        
        var maxSize = config["Logging:MaxLogFileSizeBytes"];
        if (long.TryParse(maxSize, out var size))
        {
            loggingConfig.MaxLogFileSizeBytes = size;
        }
        
        var maxFiles = config["Logging:MaxLogFiles"];
        if (int.TryParse(maxFiles, out var files))
        {
            loggingConfig.MaxLogFiles = files;
        }
        
        return new LoggingService(loggingConfig);
    }

    static async Task TestQrCodeScanner()
    {
        Console.WriteLine("\n=== QR Code Scanner Test ===");
        var stopwatch = Stopwatch.StartNew();

        // Path to test image
        string testImagePath = @"C:\TestBooklets\Input\test_page.png";

        Console.WriteLine($"Test image path: {testImagePath}");

        if (!File.Exists(testImagePath))
        {
            Console.WriteLine($"ERROR: Test image not found at {testImagePath}");
            Console.WriteLine("Please place a scanned page (8.5x11 inch @ 300 DPI) with a QR code in the lower right corner.");
            return;
        }

        try
        {
            var scanner = new RegionQrScanner();

            // For an 8.5 x 11 inch page at 300 DPI:
            // Total image size: 2550 x 3300 pixels
            // QR code region: 2 x 2 inch square = 600 x 600 pixels
            // Lower right corner position: x = 2550 - 600 = 1950, y = 3300 - 600 = 2700

            int pageWidthPixels = 2550;  // 8.5 inches * 300 DPI
            int pageHeightPixels = 3300; // 11 inches * 300 DPI
            int qrSizePixels = 600;      // 2 inches * 300 DPI

            int qrX = pageWidthPixels - qrSizePixels;  // 1950
            int qrY = pageHeightPixels - qrSizePixels; // 2700

            Console.WriteLine($"\nScanning region:");
            Console.WriteLine($"  X: {qrX}, Y: {qrY}");
            Console.WriteLine($"  Width: {qrSizePixels}, Height: {qrSizePixels}");
            Console.WriteLine($"  (Lower right corner, 2x2 inch square at 300 DPI)");

            Console.WriteLine("\nScanning for QR code...");
            string? result = scanner.ScanRegion(testImagePath, qrX, qrY, qrSizePixels, qrSizePixels);

            if (result != null)
            {
                Console.WriteLine($"\n? SUCCESS: QR code found!");
                Console.WriteLine($"QR Code Content: {result}");
            }
            else
            {
                Console.WriteLine("\n? No QR code found in the specified region.");
                Console.WriteLine("Troubleshooting tips:");
                Console.WriteLine("  - Verify the image is 2550 x 3300 pixels (8.5x11 @ 300 DPI)");
                Console.WriteLine("  - Ensure the QR code is in the lower right 2x2 inch area");
                Console.WriteLine("  - Check that the QR code is clear and not distorted");
            }

            // Also test with byte array method
            Console.WriteLine("\nTesting with byte array method...");
            byte[] imageData = await File.ReadAllBytesAsync(testImagePath);
            string? result2 = scanner.ScanRegion(imageData, qrX, qrY, qrSizePixels, qrSizePixels);

            if (result2 != null)
            {
                Console.WriteLine($"? Byte array method also successful: {result2}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nERROR: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        stopwatch.Stop();
        Console.WriteLine($"\nTime elapsed: {stopwatch.Elapsed.TotalSeconds:F2} seconds ({stopwatch.Elapsed:mm\\:ss\\.fff})");
        Console.WriteLine("\nTest completed. Press any key to exit.");
        Console.ReadKey();
    }

    static async Task TestBookletProcessing()
    {
        Console.WriteLine("\n=== Booklet Processing Test ===");
        var stopwatch = Stopwatch.StartNew();

        // Load configuration from appsettings.json
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        // Paths for testing
        // To test Scanned Sheet Mode: use Template_ScannedSheets.pdf
        // To test Booklet Mode: use any other template (e.g., Template_CAT1B.pdf)
        string templatePdf = @"C:\TestBooklets\Templates\Template_ScannedSheets.pdf";
        string inputPdf = @"C:\Users\allen\Dropbox\Data\Catforms\Scans\TestScans\345 sheets\Sig 2025-12-12_0633.pdf";
        string outputFolder = @"C:\TestBooklets\Output";

        // Ensure output folder exists
        Directory.CreateDirectory(outputFolder);

        // Read settings from appsettings.json
        var redThresholdStr = config?["BookletProcessor:RedPixelThreshold"];
        byte redPixelThreshold = byte.TryParse(redThresholdStr, out var thresholdVal) ? thresholdVal : (byte)200;

        var enableRedStr = config?["BookletProcessor:EnableRedPixelRemover"];
        bool enableRedPixelRemover = enableRedStr != null && enableRedStr.Equals("true", StringComparison.OrdinalIgnoreCase);

        var dpiStr = config?["BookletProcessor:DefaultDpi"];
        int dpi = int.TryParse(dpiStr, out var dpiVal) ? dpiVal : 300;

        // Load QR scanner configuration
        var enableQrStr = config?["BookletProcessor:QrScanner:EnableQrScanning"];
        bool enableQrScanning = enableQrStr != null && enableQrStr.Equals("true", StringComparison.OrdinalIgnoreCase);

        double qrXInches = double.TryParse(config?["BookletProcessor:QrScanner:QrRegionXInches"], out var xi) ? xi : 6.5;
        double qrYInches = double.TryParse(config?["BookletProcessor:QrScanner:QrRegionYInches"], out var yi) ? yi : 9.0;
        double qrWidthInches = double.TryParse(config?["BookletProcessor:QrScanner:QrRegionWidthInches"], out var wi) ? wi : 2.0;
        double qrHeightInches = double.TryParse(config?["BookletProcessor:QrScanner:QrRegionHeightInches"], out var hi) ? hi : 2.0;

        var qrValuesSection = config?.GetSection("BookletProcessor:QrScanner:QrValuesExcludingRedRemoval");
        var qrValues = qrValuesSection?.GetChildren().Select(c => c.Value ?? "").ToList() ??
                      new List<string> { "MACHINE_SCORED", "NO_RED_INK", "CLEAN" };

        // Load Template Exclusion Patterns
        var templateExclusionSection = config?.GetSection("BookletProcessor:TemplateExclusionPatterns");
        var templateExclusionPatterns = templateExclusionSection?.GetChildren().Select(c => c.Value ?? "").ToList() ??
                      new List<string> { "*BLANK*", "*SAMPLE*" };

        Console.WriteLine($"Red pixel remover enabled: {enableRedPixelRemover}");
        Console.WriteLine($"Red pixel threshold: {redPixelThreshold}");
        Console.WriteLine($"DPI: {dpi}");
        Console.WriteLine($"QR scanning enabled: {enableQrScanning}");
        if (enableQrScanning)
        {
            Console.WriteLine($"QR region (inches): X={qrXInches:F2}, Y={qrYInches:F2}, Width={qrWidthInches:F2}, Height={qrHeightInches:F2}");
            Console.WriteLine($"QR region (pixels @ {dpi} DPI): X={qrXInches * dpi:F0}, Y={qrYInches * dpi:F0}, Width={qrWidthInches * dpi:F0}, Height={qrHeightInches * dpi:F0}");
            Console.WriteLine($"QR values excluding red removal: {string.Join(", ", qrValues)}");
        }
        Console.WriteLine($"Template exclusion patterns: {string.Join(", ", templateExclusionPatterns)}");

        // Load Scanned Sheet Configuration
        var scannedSheetTemplateName = config?["BookletProcessor:ScannedSheets:TemplateName"];
        var scannedSheetQrMappingSection = config?.GetSection("BookletProcessor:ScannedSheets:QrToPageMapping");
        var scannedSheetQrMapping = new Dictionary<string, int>();
        if (scannedSheetQrMappingSection != null)
        {
            foreach (var child in scannedSheetQrMappingSection.GetChildren())
            {
                if (child.Key != null && int.TryParse(child.Value, out var pageIndex))
                {
                    scannedSheetQrMapping[child.Key] = pageIndex;
                }
            }
        }

        if (!string.IsNullOrEmpty(scannedSheetTemplateName))
        {
            Console.WriteLine($"Scanned sheet template: {scannedSheetTemplateName}");
            Console.WriteLine($"Scanned sheet QR mappings: {scannedSheetQrMapping.Count} patterns");
        }

        // Create service instances
        IPdfService pdfService = new PdfService();
        IDeskewer deskewer = new Deskewer();
        IImageAligner aligner = new ImageAlignerAlt();
        IRedPixelRemoverService redPixelRemover = new RedPixelRemoverService();
        RegionQrScanner qrScanner = new RegionQrScanner();

        // Load Red Pixel Exclusion Regions
        var exclusionRegionsSection = config?.GetSection("BookletProcessor:RedPixelExclusionRegions");
        var redPixelExclusionRegions = new List<RedPixelExclusionRegion>();
        if (exclusionRegionsSection != null)
        {
            foreach (var child in exclusionRegionsSection.GetChildren())
            {
                var region = new RedPixelExclusionRegion
                {
                    Name = child["Name"] ?? "",
                    XInches = double.TryParse(child["XInches"], out var x) ? x : 0,
                    YInches = double.TryParse(child["YInches"], out var y) ? y : 0,
                    WidthInches = double.TryParse(child["WidthInches"], out var w) ? w : 0,
                    HeightInches = double.TryParse(child["HeightInches"], out var h) ? h : 0
                };

                var patternsSection = child.GetSection("QrCodePatterns");
                if (patternsSection != null)
                {
                    region.QrCodePatterns = patternsSection.GetChildren()
                        .Select(c => c.Value ?? "")
                        .Where(v => !string.IsNullOrEmpty(v))
                        .ToList();
                }

                if (region.QrCodePatterns.Any())
                {
                    redPixelExclusionRegions.Add(region);
                }
            }
        }

        if (redPixelExclusionRegions.Any())
        {
            Console.WriteLine($"Red pixel exclusion regions: {redPixelExclusionRegions.Count} region(s)");
            foreach (var region in redPixelExclusionRegions)
            {
                Console.WriteLine($"  - {region.Name}: QR patterns: {string.Join(", ", region.QrCodePatterns)}");
            }
        }

        // Load Secondary QR Scan Configuration
        SecondaryQrScanConfig? secondaryQrScanConfig = null;
        var secondaryQrSection = config?.GetSection("BookletProcessor:ScannedSheets:SecondaryQrScan");
        if (secondaryQrSection != null && secondaryQrSection.Exists())
        {
            secondaryQrScanConfig = new SecondaryQrScanConfig
            {
                TriggerQrCode = secondaryQrSection["TriggerQrCode"] ?? "CHECKLISTQR-01",
                RegionXInches = double.TryParse(secondaryQrSection["RegionXInches"], out var sx) ? sx : 0.0,
                RegionYInches = double.TryParse(secondaryQrSection["RegionYInches"], out var sy) ? sy : 0.75,
                RegionWidthInches = double.TryParse(secondaryQrSection["RegionWidthInches"], out var sw) ? sw : 2.0,
                RegionHeightInches = double.TryParse(secondaryQrSection["RegionHeightInches"], out var sh) ? sh : 1.0,
                FileNameReplacementPattern = secondaryQrSection["FileNameReplacementPattern"] ?? "SchoolCityState",
                RenameInputFiles = secondaryQrSection["RenameInputFiles"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true,
                ArchiveFolder = secondaryQrSection["ArchiveFolder"] ?? @"C:\Users\allen\Dropbox\Data\Catforms\Scans\TestScans\ToArchive"
            };

            Console.WriteLine($"Secondary QR scan configured:");
            Console.WriteLine($"  Trigger QR: {secondaryQrScanConfig.TriggerQrCode}");
            Console.WriteLine($"  Region: ({secondaryQrScanConfig.RegionXInches}\", {secondaryQrScanConfig.RegionYInches}\") " +
                              $"{secondaryQrScanConfig.RegionWidthInches}\" × {secondaryQrScanConfig.RegionHeightInches}\"");
            Console.WriteLine($"  Replacement pattern: {secondaryQrScanConfig.FileNameReplacementPattern}");
            Console.WriteLine($"  Rename input files: {secondaryQrScanConfig.RenameInputFiles}");
            if (secondaryQrScanConfig.RenameInputFiles)
            {
                Console.WriteLine($"  Archive folder: {secondaryQrScanConfig.ArchiveFolder}");
            }
        }

        // Create scanned sheet processor if configured
        IScannedSheetProcessor? scannedSheetProcessor = null;
        if (!string.IsNullOrEmpty(scannedSheetTemplateName))
        {
            scannedSheetProcessor = new ScannedSheetProcessorService(
                pdfService,
                deskewer,
                aligner,
                enableRedPixelRemover ? redPixelRemover : null,
                redPixelThreshold,
                enableQrScanning ? qrScanner : null,
                enableQrScanning,
                qrXInches,
                qrYInches,
                qrWidthInches,
                qrHeightInches,
                dpi,
                qrValues,
                redPixelExclusionRegions,
                secondaryQrScanConfig);
        }

        var bookletProcessor = new BookletProcessorService(
        pdfService,
        deskewer,
        aligner,
        enableRedPixelRemover ? redPixelRemover : null,
        redPixelThreshold,
        dpi,
        enableQrScanning ? qrScanner : null,
        enableQrScanning,
        qrXInches,
        qrYInches,
        qrWidthInches,
        qrHeightInches,
        qrValues,
        templateExclusionPatterns,
        scannedSheetProcessor,
        scannedSheetTemplateName,
        scannedSheetQrMapping
        );

        // Create job for logging
        var job = new ProcessingJob
        {
            InputFilePath = inputPdf,
            TemplateFilePath = templatePdf,
            OutputFolder = outputFolder,
            Status = ProcessingJobStatus.Processing
        };
        
        job.StartedTime = DateTime.Now;
        
        // Determine processing mode
        string processingMode = (!string.IsNullOrEmpty(scannedSheetTemplateName) && 
                                Path.GetFileName(templatePdf).Equals(scannedSheetTemplateName, StringComparison.OrdinalIgnoreCase))
            ? "ScannedSheets"
            : "Booklet";
        
        // Log job started
        if (_loggingService != null)
        {
            await _loggingService.LogJobStartedAsync(job, dpi, enableRedPixelRemover, enableQrScanning, processingMode);
        }

        try
        {
            // Use ProcessBookletsWorkflowAsync which includes auto-detection for scanned sheet mode
            var result = await bookletProcessor.ProcessBookletsWorkflowAsync(
                inputPdf,
                templatePdf,
                outputFolder,
                null); // No progress callback for console

            job.Result = result;
            job.Status = result.Success ? ProcessingJobStatus.Completed : ProcessingJobStatus.Failed;
            job.ErrorMessage = result.ErrorMessage;
            job.CompletedTime = DateTime.Now;

            // Log job result
            if (_loggingService != null)
            {
                if (result.Success)
                {
                    await _loggingService.LogJobCompletedAsync(job, dpi, enableRedPixelRemover, enableQrScanning, processingMode);
                }
                else
                {
                    await _loggingService.LogJobFailedAsync(job, dpi, enableRedPixelRemover, enableQrScanning, processingMode);
                }
            }

            if (result.Success)
            {
                Console.WriteLine("Test completed successfully.");
                Console.WriteLine($"Output: {result.OutputPath}");
                Console.WriteLine($"Pages processed: {result.PagesProcessed}");
            }
            else
            {
                Console.WriteLine($"Test failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            job.Status = ProcessingJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedTime = DateTime.Now;
            
            // Log job failed
            if (_loggingService != null)
            {
                await _loggingService.LogJobFailedAsync(job, dpi, enableRedPixelRemover, enableQrScanning, processingMode);
            }
            
            Console.WriteLine($"Test failed: {ex.Message}");
        }

        stopwatch.Stop();
        Console.WriteLine($"\nTime elapsed: {stopwatch.Elapsed.TotalSeconds:F2} seconds ({stopwatch.Elapsed:mm\\:ss\\.fff})");
        Console.WriteLine("\nPress any key to exit.");
        Console.ReadKey();
    }
}
