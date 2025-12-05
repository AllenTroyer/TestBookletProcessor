using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using QrRegionScanner;
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
        
        Console.WriteLine("\nTest completed. Press any key to exit.");
        Console.ReadKey();
    }

    static async Task TestBookletProcessing()
    {
        Console.WriteLine("\n=== Booklet Processing Test ===");
        
        // Load configuration from appsettings.json
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        // Paths for testing
        string templatePdf = @"C:\TestBooklets\Input\template.pdf";
        string inputPdf = @"C:\TestBooklets\Input\input.pdf";
        string workingFolder = @"C:\TestBooklets\Working";
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

        Console.WriteLine($"Red pixel remover enabled: {enableRedPixelRemover}");
        Console.WriteLine($"Red pixel threshold: {redPixelThreshold}");
        Console.WriteLine($"DPI: {dpi}");

        // Create service instances
        IPdfService pdfService = new PdfService();
        IDeskewer deskewer = new Deskewer();
        IImageAligner aligner = new ImageAlignerAlt();
        IRedPixelRemoverService redPixelRemover = new RedPixelRemoverService();

        var bookletProcessor = new BookletProcessorService(
        pdfService,
        deskewer,
        aligner,
        enableRedPixelRemover ? redPixelRemover : null,
        redPixelThreshold,
        dpi
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
        
        Console.WriteLine("\nPress any key to exit.");
        Console.ReadKey();
    }
}
