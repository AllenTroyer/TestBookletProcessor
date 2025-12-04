using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;
using TestBookletProcessor.Services;

class Program
{
    static async Task Main(string[] args)
    {
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
    }
}
