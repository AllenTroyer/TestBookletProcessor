using Docnet.Core;
using Docnet.Core.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;

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
        await Task.Run(() =>
        {
            if (!File.Exists(pdfPath))
                throw new FileNotFoundException($"Input PDF not found: {pdfPath}");

            Directory.CreateDirectory(outputFolder);
            var outputPath = Path.Combine(outputFolder, $"page{pageNumber}.png");

            using (var docReader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(1080, 1920)))
            {
                // Docnet uses zero-based page numbers
                using (var pageReader = docReader.GetPageReader(pageNumber - 1))
                {
                    int pageWidth = pageReader.GetPageWidth();
                    int pageHeight = pageReader.GetPageHeight();
                    var rawBytes = pageReader.GetImage();

                    // Convert rawBytes (BGRA) to ImageSharp image
                    using (var image = new Image<Rgba32>(pageWidth, pageHeight))
                    {
                        image.ProcessPixelRows(accessor =>
                        {
                            for (int y = 0; y < pageHeight; y++)
                            {
                                var rowSpan = accessor.GetRowSpan(y);
                                int offset = y * pageWidth * 4;
                                for (int x = 0; x < pageWidth; x++)
                                {
                                    int idx = offset + x * 4;
                                    // BGRA to RGBA
                                    byte b = rawBytes[idx + 0];
                                    byte g = rawBytes[idx + 1];
                                    byte r = rawBytes[idx + 2];
                                    byte a = rawBytes[idx + 3];
                                    rowSpan[x] = new Rgba32(r, g, b, a);
                                }
                            }
                        });
                        image.Save(outputPath, new PngEncoder());
                    }
                }
            }
            Console.WriteLine($"Converted page {pageNumber} to image: {outputPath}");
        });
    }

    public async Task ConvertImageToPdfAsync(string imagePath, string outputPath)
    {
        await Task.CompletedTask;
        Console.WriteLine($"[STUB] Converting image to PDF: {imagePath} -> {outputPath}");
    }
}
