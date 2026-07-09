using QrRegionScanner;
using System;
using System.Diagnostics;
using System.IO;
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
        Console.WriteLine($"Using configuration file: {AppConfig.ConfigFilePath}");
        var appOptions = AppConfig.Load();

        _loggingService = new LoggingService(appOptions.Logging);

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
            await TestBookletProcessing(appOptions.BookletProcessor);
        }
        else
        {
            Console.WriteLine("Invalid choice. Exiting.");
        }

        // Log application stop
        await _loggingService.LogApplicationStopAsync();
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

    static async Task TestBookletProcessing(BookletProcessorOptions options)
    {
        Console.WriteLine("\n=== Booklet Processing Test ===");
        var stopwatch = Stopwatch.StartNew();

        // Paths for testing
        // To test Scanned Sheet Mode: use Template_ScannedSheets.pdf
        // To test Booklet Mode: use any other template (e.g., Template_CAT1B.pdf)
        string templatePdf = @"C:\TestBooklets\Templates\Template_ScannedSheets.pdf";
        string inputPdf = @"C:\Users\allen\Dropbox\Data\Catforms\Scans\TestScans\345 sheets\Sig 2025-12-12_0633.pdf";
        string outputFolder = @"C:\TestBooklets\Output";

        // Ensure output folder exists
        Directory.CreateDirectory(outputFolder);

        Console.WriteLine($"Red pixel remover enabled: {options.EnableRedPixelRemover}");
        Console.WriteLine($"Red pixel threshold: {options.RedPixelThreshold}");
        Console.WriteLine($"DPI: {options.DefaultDpi}");
        Console.WriteLine($"QR scanning enabled: {options.QrScanner.EnableQrScanning}");
        if (options.QrScanner.EnableQrScanning)
        {
            var qr = options.QrScanner;
            var dpi = options.DefaultDpi;
            Console.WriteLine($"QR region (inches): X={qr.QrRegionXInches:F2}, Y={qr.QrRegionYInches:F2}, Width={qr.QrRegionWidthInches:F2}, Height={qr.QrRegionHeightInches:F2}");
            Console.WriteLine($"QR region (pixels @ {dpi} DPI): X={qr.QrRegionXInches * dpi:F0}, Y={qr.QrRegionYInches * dpi:F0}, Width={qr.QrRegionWidthInches * dpi:F0}, Height={qr.QrRegionHeightInches * dpi:F0}");
            Console.WriteLine($"QR values excluding red removal: {string.Join(", ", qr.QrValuesExcludingRedRemoval)}");
        }
        Console.WriteLine($"Template exclusion patterns: {string.Join(", ", options.TemplateExclusionPatterns)}");

        if (!string.IsNullOrEmpty(options.ScannedSheets.TemplateName))
        {
            Console.WriteLine($"Scanned sheet template: {options.ScannedSheets.TemplateName}");
            Console.WriteLine($"Scanned sheet QR mappings: {options.ScannedSheets.QrToPageMapping.Count} patterns");
        }

        if (options.RedPixelExclusionRegions.Count > 0)
        {
            Console.WriteLine($"Red pixel exclusion regions: {options.RedPixelExclusionRegions.Count} region(s)");
            foreach (var region in options.RedPixelExclusionRegions)
            {
                Console.WriteLine($"  - {region.Name}: QR patterns: {string.Join(", ", region.QrCodePatterns)}");
            }
        }

        var bookletProcessor = ProcessorFactory.CreateBookletProcessor(options, _loggingService);

        // Create job for logging
        var job = new ProcessingJob
        {
            InputFilePath = inputPdf,
            TemplateFilePath = templatePdf,
            OutputFolder = outputFolder,
            Status = ProcessingJobStatus.Processing,
            StartedTime = DateTime.Now
        };

        // Determine processing mode
        string processingMode = (!string.IsNullOrEmpty(options.ScannedSheets.TemplateName) &&
                                Path.GetFileName(templatePdf).Equals(options.ScannedSheets.TemplateName, StringComparison.OrdinalIgnoreCase))
            ? "ScannedSheets"
            : "Booklet";

        // Log job started
        if (_loggingService != null)
        {
            await _loggingService.LogJobStartedAsync(job, options.DefaultDpi, options.EnableRedPixelRemover,
                options.QrScanner.EnableQrScanning, processingMode);
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
                    await _loggingService.LogJobCompletedAsync(job, options.DefaultDpi, options.EnableRedPixelRemover,
                        options.QrScanner.EnableQrScanning, processingMode);
                }
                else
                {
                    await _loggingService.LogJobFailedAsync(job, options.DefaultDpi, options.EnableRedPixelRemover,
                        options.QrScanner.EnableQrScanning, processingMode);
                }
            }

            if (result.Success)
            {
                Console.WriteLine("Test completed successfully.");
                Console.WriteLine($"Output: {result.OutputPath}");
                Console.WriteLine($"Pages processed: {result.PagesProcessed}");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"WARNING: {warning}");
                }
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
                await _loggingService.LogJobFailedAsync(job, options.DefaultDpi, options.EnableRedPixelRemover,
                    options.QrScanner.EnableQrScanning, processingMode);
            }

            Console.WriteLine($"Test failed: {ex}");
        }

        stopwatch.Stop();
        Console.WriteLine($"\nTime elapsed: {stopwatch.Elapsed.TotalSeconds:F2} seconds ({stopwatch.Elapsed:mm\\:ss\\.fff})");
        Console.WriteLine("\nPress any key to exit.");
        Console.ReadKey();
    }
}
