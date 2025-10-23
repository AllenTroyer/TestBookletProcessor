using System.IO;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;
using TestBookletProcessor.Services;

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
    TemplateFolder = @"C:\TestBooklets\Templates",
    DPI = 300
};

Console.WriteLine("Current Settings:");
Console.WriteLine($"  Input Folder:  {settings.InputFolder}");
Console.WriteLine($"  Output Folder: {settings.OutputFolder}");
Console.WriteLine($"  Template Folder: {settings.TemplateFolder}");
Console.WriteLine($"  DPI:           {settings.DPI}");
Console.WriteLine();

// Test BookletProcessorService integration
Console.WriteLine("Testing BookletProcessorService integration...");
Console.WriteLine("─────────────────────────────────────────");
var bookletProcessor = new BookletProcessorService(pdfService, imageProcessor);
string templatePdf = Path.Combine(settings.TemplateFolder, "template.pdf");
string inputPdf = Path.Combine(settings.InputFolder, "input.pdf");
string workingFolder = Path.Combine(settings.OutputFolder, "booklet_work");
string finalOutputPdf = Path.Combine(settings.OutputFolder, "final_output.pdf");
await bookletProcessor.ProcessBookletAsync(templatePdf, inputPdf, workingFolder, finalOutputPdf);
Console.WriteLine($"✓ Booklet processing completed: {finalOutputPdf}\n");

//// Test PDF Service
//Console.WriteLine("Testing PDF Service (Stub)...");
//Console.WriteLine("─────────────────────────────────────────");
//var pages = await pdfService.SplitPdfAsync(
//    Path.Combine(settings.InputFolder, "sample.pdf"),
//    settings.OutputFolder
//);
//Console.WriteLine($"✓ Split completed: {pages.Count} pages\n");

//// Test MergePdfsAsync
//Console.WriteLine("Testing MergePdfsAsync...");
//Console.WriteLine("─────────────────────────────────────────");
//var pdfsToMerge = new List<string>
//{
//    Path.Combine(settings.OutputFolder, "page_0001.pdf"),
//    Path.Combine(settings.OutputFolder, "page_0002.pdf")
//    // Add more pages as needed
//};
//string mergedPdf = Path.Combine(settings.OutputFolder, "merged.pdf");
//await pdfService.MergePdfsAsync(pdfsToMerge, mergedPdf);
//Console.WriteLine($"✓ PDFs merged to: {mergedPdf}\n");

//// Test ConvertPageToImageAsync
//Console.WriteLine("Testing ConvertPageToImageAsync...");
//Console.WriteLine("─────────────────────────────────────────");
//await pdfService.ConvertPageToImageAsync(
//    Path.Combine(settings.InputFolder, "sample.pdf"),
//    1,
//    settings.OutputFolder
//);
//Console.WriteLine($"✓ Page1 converted to image in {settings.OutputFolder}\n");

//// Test DeskewImageAsync
//Console.WriteLine("Testing DeskewImageAsync...");
//Console.WriteLine("─────────────────────────────────────────");
//string inputImage = Path.Combine(settings.InputFolder, "test_skewed.png");
//string deskewedImage = Path.Combine(settings.OutputFolder, "test_deskewed.png");
//await imageProcessor.DeskewImageAsync(inputImage, deskewedImage);
//Console.WriteLine($"✓ Deskewed image saved to: {deskewedImage}\n");

//// Test AlignImageAsync
//Console.WriteLine("Testing AlignImageAsync...");
//Console.WriteLine("─────────────────────────────────────────");
//string alignInputImage = Path.Combine(settings.InputFolder, "test_align_input.png");
//string alignTemplateImage = Path.Combine(settings.InputFolder, "test_align_template.png");
//string alignedImage = Path.Combine(settings.OutputFolder, "test_aligned.png");
//await imageProcessor.AlignImageAsync(alignInputImage, alignTemplateImage, alignedImage);
//Console.WriteLine($"✓ Aligned image saved to: {alignedImage}\n");

//// Test ConvertImageToPdfAsync
//Console.WriteLine("Testing ConvertImageToPdfAsync...");
//Console.WriteLine("─────────────────────────────────────────");
//string imageToConvert = Path.Combine(settings.InputFolder, "test_image.png");
//string pdfOutput = Path.Combine(settings.OutputFolder, "test_image_converted.pdf");
//await pdfService.ConvertImageToPdfAsync(imageToConvert, pdfOutput);
//Console.WriteLine($"✓ Image converted to PDF: {pdfOutput}\n");

//// Test Image Processor
//Console.WriteLine("Testing Image Processor (Stub)...");
//Console.WriteLine("─────────────────────────────────────────");
//var angle = await imageProcessor.DetectSkewAngleAsync("test_image.png");
//Console.WriteLine($"✓ Skew detection completed: {angle}°\n");

//Console.WriteLine("═════════════════════════════════════════");
//Console.WriteLine("All stub tests completed successfully!");
//Console.WriteLine("Press any key to exit...");
//Console.ReadKey();