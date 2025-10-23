using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;
using TestBookletProcessor.Services;
using System.IO;

Console.WriteLine("╔════════════════════════════════════════╗");
Console.WriteLine("║  Test Booklet Processor - Console     ║");
Console.WriteLine("╔════════════════════════════════════════╗");
Console.WriteLine();

// Initialize services
IPdfService pdfService = new PdfService();
IImageProcessor imageProcessor = new ImageProcessor();

// Configuration
var settings = new ProcessingSettings
{
    InputFolder = @"C:\TestBooklets\Input",
    OutputFolder = @"C:\TestBooklets\Output",
    TemplatePath = @"C:\TestBooklets\Templates\template.pdf",
    DPI = 300
};

Console.WriteLine("Current Settings:");
Console.WriteLine($"  Input Folder:  {settings.InputFolder}");
Console.WriteLine($"  Output Folder: {settings.OutputFolder}");
Console.WriteLine($"  Template:      {settings.TemplatePath}");
Console.WriteLine($"  DPI:           {settings.DPI}");
Console.WriteLine();

// Test PDF Service
Console.WriteLine("Testing PDF Service (Stub)...");
Console.WriteLine("─────────────────────────────────────────");
var pages = await pdfService.SplitPdfAsync(
    Path.Combine(settings.InputFolder, "sample.pdf"),
    settings.OutputFolder
);
Console.WriteLine($"✓ Split completed: {pages.Count} pages\n");

// Test ConvertPageToImageAsync
Console.WriteLine("Testing ConvertPageToImageAsync...");
Console.WriteLine("─────────────────────────────────────────");
await pdfService.ConvertPageToImageAsync(
    Path.Combine(settings.InputFolder, "sample.pdf"),
    1,
    settings.OutputFolder
);
Console.WriteLine($"✓ Page1 converted to image in {settings.OutputFolder}\n");

// Test DeskewImageAsync
Console.WriteLine("Testing DeskewImageAsync...");
Console.WriteLine("─────────────────────────────────────────");
string inputImage = Path.Combine(settings.InputFolder, "test_skewed.png");
string deskewedImage = Path.Combine(settings.OutputFolder, "test_deskewed.png");
await imageProcessor.DeskewImageAsync(inputImage, deskewedImage);
Console.WriteLine($"✓ Deskewed image saved to: {deskewedImage}\n");

// Test AlignImageAsync
Console.WriteLine("Testing AlignImageAsync...");
Console.WriteLine("─────────────────────────────────────────");
string alignInputImage = Path.Combine(settings.InputFolder, "test_align_input.png");
string alignTemplateImage = Path.Combine(settings.InputFolder, "test_align_template.png");
string alignedImage = Path.Combine(settings.OutputFolder, "test_aligned.png");
await imageProcessor.AlignImageAsync(alignInputImage, alignTemplateImage, alignedImage);
Console.WriteLine($"✓ Aligned image saved to: {alignedImage}\n");

// Test ConvertImageToPdfAsync
Console.WriteLine("Testing ConvertImageToPdfAsync...");
Console.WriteLine("─────────────────────────────────────────");
string imageToConvert = Path.Combine(settings.InputFolder, "test_image.png");
string pdfOutput = Path.Combine(settings.OutputFolder, "test_image_converted.pdf");
await pdfService.ConvertImageToPdfAsync(imageToConvert, pdfOutput);
Console.WriteLine($"✓ Image converted to PDF: {pdfOutput}\n");

// Test Image Processor
Console.WriteLine("Testing Image Processor (Stub)...");
Console.WriteLine("─────────────────────────────────────────");
var angle = await imageProcessor.DetectSkewAngleAsync("test_image.png");
Console.WriteLine($"✓ Skew detection completed: {angle}°\n");

Console.WriteLine("═════════════════════════════════════════");
Console.WriteLine("All stub tests completed successfully!");
Console.WriteLine("Press any key to exit...");
Console.ReadKey();