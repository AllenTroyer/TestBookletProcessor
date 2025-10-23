using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Services;

class DummyImageProcessor : IImageProcessor
{
    public Task DeskewImageAsync(string imagePath, string outputPath)
    {
        // Simulate deskew by copying the image
        File.Copy(imagePath, outputPath, true);
        return Task.CompletedTask;
    }

    public Task AlignImageAsync(string imagePath, string templatePath, string outputPath)
    {
        // Simulate align by copying the image
        File.Copy(imagePath, outputPath, true);
        return Task.CompletedTask;
    }

    public Task<double> DetectSkewAngleAsync(string imagePath)
    {
        return Task.FromResult(0.0);
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        // Paths for testing
        string templatePdf = @"C:\TestBooklets\Input\template.pdf";
        string inputPdf = @"C:\TestBooklets\Input\input.pdf";
        string workingFolder = @"C:\TestBooklets\Working";
        string outputPdf = @"C:\TestBooklets\Output\final_output.pdf";

        // Ensure working/output folders exist
        Directory.CreateDirectory(workingFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPdf)!);

        // Create service instances
        IPdfService pdfService = new PdfService();
        IImageProcessor imageProcessor = new DummyImageProcessor();
        var bookletProcessor = new BookletProcessorService(pdfService, imageProcessor);

        try
        {
            await bookletProcessor.ProcessBookletAsync(templatePdf, inputPdf, workingFolder, outputPdf);
            Console.WriteLine("Test completed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex.Message}");
        }
    }
}