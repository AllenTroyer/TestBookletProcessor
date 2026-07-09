using Docnet.Core;
using Docnet.Core.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata;
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
    /// <summary>
    /// Docnet wraps PDFium, which is not thread-safe, and DocLib.Instance is a process-wide
    /// singleton — so all rendering must be serialized even though jobs run concurrently
    /// with otherwise isolated service instances.
    /// </summary>
    private static readonly object PdfiumLock = new();

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

    public async Task ConvertPageToImageAsync(string pdfPath, int pageNumber, string outputImagePath, int dpi = 300)
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

            // Calculate page dimensions based on DPI for US Letter size (8.5 x 11 inches)
            int pageWidthPixels = (int)(8.5 * dpi);
            int pageHeightPixels = (int)(11 * dpi);

            byte[] rawBytes;
            int pageWidth, pageHeight;
            lock (PdfiumLock)
            {
                using var docReader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(pageWidthPixels, pageHeightPixels));
                using var pageReader = docReader.GetPageReader(pageNumber - 1);
                pageWidth = pageReader.GetPageWidth();
                pageHeight = pageReader.GetPageHeight();
                rawBytes = pageReader.GetImage();
            }

            // Docnet returns BGRA bytes, which is a pixel layout ImageSharp can load directly
            using (var image = SixLabors.ImageSharp.Image.LoadPixelData<Bgra32>(rawBytes, pageWidth, pageHeight))
            {
                image.Metadata.HorizontalResolution = dpi;
                image.Metadata.VerticalResolution = dpi;
                image.Metadata.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;
                image.Save(outputImagePath, new PngEncoder());
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

            // Set page size to8.5 x11 inches (US Letter)
            page.Width = XUnit.FromPoint(612); //8.5 inches *72
            page.Height = XUnit.FromPoint(792); //11 inches *72

            using var image = XImage.FromFile(imagePath);

            // Calculate scaling to fit image within page, preserving aspect ratio
            double scale = Math.Min(page.Width.Point / image.PixelWidth, page.Height.Point / image.PixelHeight);
            double imgWidth = image.PixelWidth * scale;
            double imgHeight = image.PixelHeight * scale;
            double x = (page.Width.Point - imgWidth) / 2;
            double y = (page.Height.Point - imgHeight) / 2;

            using (var gfx = XGraphics.FromPdfPage(page))
            {
                gfx.DrawImage(image, x, y, imgWidth, imgHeight);
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

    public static void CleanupDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        const int maxAttempts = 5;
        const int delayMs = 200;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                // Force readonly attributes off for all files
                var dirInfo = new DirectoryInfo(path);
                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        file.Attributes = FileAttributes.Normal;
                    }
                    catch
                    {
                        // Ignore individual file attribute errors
                    }
                }

                // Delete directory
                Directory.Delete(path, true);
                Console.WriteLine($"Cleaned up temporary folder: {path}");
                return; // Success!
            }
            catch (IOException ex) when (attempt < maxAttempts)
            {
                // File might be locked, wait and retry
                Console.WriteLine($"Cleanup attempt {attempt}/{maxAttempts} failed for {path}: {ex.Message}");
                System.Threading.Thread.Sleep(delayMs);
            }
            catch (UnauthorizedAccessException ex) when (attempt < maxAttempts)
            {
                // Permission issue, wait and retry
                Console.WriteLine($"Cleanup attempt {attempt}/{maxAttempts} failed (access denied) for {path}: {ex.Message}");
                System.Threading.Thread.Sleep(delayMs);
            }
            catch (Exception ex)
            {
                // Other errors
                Console.WriteLine($"Failed to clean up folder {path} (attempt {attempt}/{maxAttempts}): {ex.Message}");
                if (attempt == maxAttempts)
                {
                    Console.WriteLine($"⚠ WARNING: Temp folder not cleaned up: {path}");
                    Console.WriteLine($"  You may need to manually delete this folder.");
                }
            }
        }
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
