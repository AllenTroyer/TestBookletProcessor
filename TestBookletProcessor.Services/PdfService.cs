using Docnet.Core;
using Docnet.Core.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        await Task.Run(() =>
        {
            if (pdfPaths == null || pdfPaths.Count == 0)
                throw new ArgumentException("No PDF files provided for merging.");

            if (IsFileLocked(outputPath))
                throw new IOException($"The file '{outputPath}' is locked by another process. Is it open in another application?");

            using var outputDocument = new PdfDocument();

            foreach (var pdfPath in pdfPaths)
            {
                if (!File.Exists(pdfPath))
                    throw new FileNotFoundException($"PDF file not found: {pdfPath}");

                using var inputDocument = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
                for (int idx = 0; idx < inputDocument.PageCount; idx++)
                {
                    outputDocument.AddPage(inputDocument.Pages[idx]);
                }
            }

            outputDocument.Save(outputPath);
            Console.WriteLine($"Merged {pdfPaths.Count} PDFs to: {outputPath}");
        });
    }

    public async Task ConvertPageToImageAsync(string pdfPath, int pageNumber, string outputImagePath)
    {
        await Task.Run(() =>
        {
            if (!File.Exists(pdfPath))
                throw new FileNotFoundException($"Input PDF not found: {pdfPath}");

            // Ensure outputImagePath is a directory, then append a filename
            string directory = outputImagePath;
            if (Directory.Exists(outputImagePath))
            {
                outputImagePath = Path.Combine(directory, $"page_{pageNumber:D4}.png");
            }
            else if (Path.GetExtension(outputImagePath) == string.Empty)
            {
                // If no extension, treat as directory and append filename
                Directory.CreateDirectory(outputImagePath);
                outputImagePath = Path.Combine(outputImagePath, $"page_{pageNumber:D4}.png");
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputImagePath)!);
            }

            using (var docReader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(1080, 1920)))
            {
                using (var pageReader = docReader.GetPageReader(pageNumber - 1))
                {
                    int pageWidth = pageReader.GetPageWidth();
                    int pageHeight = pageReader.GetPageHeight();
                    var rawBytes = pageReader.GetImage();

                    using (var image = new SixLabors.ImageSharp.Image<Rgba32>(pageWidth, pageHeight))
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
                                    byte b = rawBytes[idx + 0];
                                    byte g = rawBytes[idx + 1];
                                    byte r = rawBytes[idx + 2];
                                    byte a = rawBytes[idx + 3];
                                    rowSpan[x] = new Rgba32(r, g, b, a);
                                }
                            }
                        });
                        image.Save(outputImagePath, new PngEncoder());
                    }
                }
            }
            Console.WriteLine($"Converted page {pageNumber} to image: {outputImagePath}");
        });
    }

    public async Task ConvertImageToPdfAsync(string imagePath, string outputPath)
    {
        await Task.Run(() =>
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Input image not found: {imagePath}");

            using var document = new PdfDocument();
            var page = document.AddPage();

            using var image = XImage.FromFile(imagePath);
            page.Width = XUnit.FromPoint(image.PixelWidth * 72.0 / image.HorizontalResolution);
            page.Height = XUnit.FromPoint(image.PixelHeight * 72.0 / image.VerticalResolution);

            using (var gfx = XGraphics.FromPdfPage(page))
            {
                // Use .Point property to avoid obsolete implicit conversion
                gfx.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
            }

            document.Save(outputPath);
            Console.WriteLine($"Converted image to PDF: {imagePath} -> {outputPath}");
        });
    }

    public async Task<List<string>> SplitIntoBookletsAsync(string inputPdfPath, string templatePdfPath, string outputFolder)
    {
        return await Task.Run(() =>
        {
            using var inputDoc = PdfReader.Open(inputPdfPath, PdfDocumentOpenMode.Import);
            using var templateDoc = PdfReader.Open(templatePdfPath, PdfDocumentOpenMode.Import);

            int inputPages = inputDoc.PageCount;
            int templatePages = templateDoc.PageCount;

            if (inputPages % templatePages != 0)
                throw new InvalidOperationException("Input PDF page count is not an exact multiple of the template PDF page count.");

            Directory.CreateDirectory(outputFolder);
            var bookletPaths = new List<string>();
            int bookletCount = inputPages / templatePages;

            for (int i = 0; i < bookletCount; i++)
            {
                string bookletPath = Path.Combine(outputFolder, $"booklet_{i + 1:D4}.pdf");
                using var bookletDoc = new PdfDocument();
                for (int j = 0; j < templatePages; j++)
                {
                    bookletDoc.AddPage(inputDoc.Pages[i * templatePages + j]);
                }
                bookletDoc.Save(bookletPath);
                bookletPaths.Add(bookletPath);
            }
            return bookletPaths;
        });
    }

    public async Task ProcessBookletsAsync(
        string inputPdfPath,
        string templatePdfPath,
        string workingFolder,
        string finalOutputPdf,
        Func<string, Task<string>> processBookletAsync)
    {
        // 1. Split input PDF into booklets
        var bookletFiles = await SplitIntoBookletsAsync(inputPdfPath, templatePdfPath, workingFolder);

        // 2. Process each booklet file
        var processedBookletFiles = new List<string>();
        foreach (var bookletFile in bookletFiles)
        {
            // The delegate should process the booklet and return the path to the processed file
            string processedFile = await processBookletAsync(bookletFile);
            processedBookletFiles.Add(processedFile);
        }

        // 3. Merge processed booklets back into a single PDF
        await MergePdfsAsync(processedBookletFiles, finalOutputPdf);
    }

    private bool IsFileLocked(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }
}
