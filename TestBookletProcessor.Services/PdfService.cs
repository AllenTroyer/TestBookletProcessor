using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;

namespace TestBookletProcessor.Services;

public class PdfService : IPdfService
{
    public async Task<List<string>> SplitPdfAsync(string inputPath, string outputFolder)
    {
        return await Task.Run(() =>
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException($"Input PDF not found: {inputPath}");

            Directory.CreateDirectory(outputFolder);
            var outputPaths = new List<string>();

            using (PdfDocument inputDocument = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import))
            {
                int numberOfPages = inputDocument.PageCount;
                for (int i = 0; i < numberOfPages; i++)
                {
                    string outputPath = Path.Combine(outputFolder, $"page_{i + 1:D4}.pdf");
                    using (PdfDocument outputDocument = new PdfDocument())
                    {
                        outputDocument.AddPage(inputDocument.Pages[i]);
                        outputDocument.Save(outputPath);
                    }
                    outputPaths.Add(outputPath);
                    Console.WriteLine($"Extracted page {i + 1} of {numberOfPages}");
                }
            }
            return outputPaths;
        });
    }

    public async Task MergePdfsAsync(List<string> pdfPaths, string outputPath)
    {
        await Task.CompletedTask;
        Console.WriteLine($"[STUB] Merging {pdfPaths.Count} PDFs to: {outputPath}");
    }

    public async Task ConvertPageToImageAsync(string pdfPath, int pageNumber, string outputFolder)
    {
        await Task.CompletedTask;
        var outputPath = Path.Combine(outputFolder, $"page{pageNumber}.png");
        Console.WriteLine($"[STUB] Converting page {pageNumber} to image: {outputPath}");
    }

    public async Task ConvertImageToPdfAsync(string imagePath, string outputPath)
    {
        await Task.CompletedTask;
        Console.WriteLine($"[STUB] Converting image to PDF: {imagePath} -> {outputPath}");
    }
}
