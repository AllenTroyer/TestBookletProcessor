using Microsoft.Extensions.Configuration;
using QrRegionScanner;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;
using TestBookletProcessor.Services;

partial class Program
{
    static async Task Main(string[] args)
    {
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
        string templatePdf = @"C:\TestBooklets\Templates\Template_ScannedSheets.pdf";
        string inputPdf = @"C:\TestBooklets\Input\input.pdf";
        string workingFolder = @"C:\TestBooklets\Output";
        string outputPdf = @"C:\TestBooklets\Output\final_output.pdf";

        // Ensure working/output folders exist
        Directory.CreateDirectory(workingFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPdf)!);

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
                qrValues);
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

        try
        {
            await bookletProcessor.ProcessBookletAsync(templatePdf, inputPdf, workingFolder, outputPdf, dpi);
            Console.WriteLine("Test completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex.Message}");
        }

        stopwatch.Stop();
        Console.WriteLine($"\nTime elapsed: {stopwatch.Elapsed.TotalSeconds:F2} seconds ({stopwatch.Elapsed:mm\\:ss\\.fff})");
        Console.WriteLine("\nPress any key to exit.");
        Console.ReadKey();
    }
}
