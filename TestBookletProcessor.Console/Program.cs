using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;
using TestBookletProcessor.Services;

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
 IDeskewer deskewer = new Deskewer();
 IImageAligner aligner = new ImageAligner();
 IRedPixelRemoverService redPixelRemover = new RedPixelRemoverService();
 byte redPixelThreshold =225;
 bool enableRedPixelRemover = true;
 int dpi =300;

 var bookletProcessor = new BookletProcessorService(
 pdfService,
 deskewer,
 aligner,
 redPixelRemover,
 redPixelThreshold,
 enableRedPixelRemover,
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
