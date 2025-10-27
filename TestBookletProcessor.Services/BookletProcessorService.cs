using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;

namespace TestBookletProcessor.Services;

public class BookletProcessorService
{
    private readonly IPdfService _pdfService;
    private readonly IImageProcessor _imageProcessor;
    private readonly IRedPixelRemoverService? _redPixelRemover;
    private readonly byte _redThreshold;
    private readonly bool _enableRedPixelRemover;
    private readonly int _dpi;

    public BookletProcessorService(
        IPdfService pdfService,
        IImageProcessor imageProcessor,
        IRedPixelRemoverService? redPixelRemover = null,
        byte redThreshold = 200,
        bool enableRedPixelRemover = false,
        int dpi = 300)
    {
        _pdfService = pdfService;
        _imageProcessor = imageProcessor;
        _redPixelRemover = redPixelRemover;
        _redThreshold = redThreshold;
        _enableRedPixelRemover = enableRedPixelRemover;
        _dpi = dpi;
    }

    public async Task<ProcessingResult> ProcessBookletsWorkflowAsync(
    string inputPdf,
    string templatePdf,
    string outputFolder,
    Action<int, int>? statusCallback = null)
    {
        var result = new ProcessingResult();
        var stopwatch = Stopwatch.StartNew();
        string bookletsFolder = Path.Combine(outputFolder, "booklets");
        var bookletWorkingFolders = new List<string>();
        try
        {
            var inputFileNameNoExt = Path.GetFileNameWithoutExtension(inputPdf);
            string finalOutputPdf = Path.Combine(outputFolder, $"{inputFileNameNoExt}_aligned.pdf");
            Directory.CreateDirectory(outputFolder);
            // Split input PDF into booklets
            var bookletPaths = await _pdfService.SplitIntoBookletsAsync(inputPdf, templatePdf, bookletsFolder);
            var processedBookletPaths = new List<string>();
            int totalBooklets = bookletPaths.Count;
            int bookletIndex = 1;
            foreach (var bookletPath in bookletPaths)
            {
                statusCallback?.Invoke(bookletIndex, totalBooklets);
                string bookletWorkingFolder = Path.Combine(outputFolder, $"booklet_{bookletIndex}");
                bookletWorkingFolders.Add(bookletWorkingFolder);
                string processedBookletOutput = Path.Combine(bookletWorkingFolder, "processed_booklet.pdf");
                await ProcessBookletAsync(templatePdf, bookletPath, bookletWorkingFolder, processedBookletOutput, _dpi);
                processedBookletPaths.Add(processedBookletOutput);
                bookletIndex++;
            }
            // Merge all processed booklets into the final output
            await _pdfService.MergePdfsAsync(processedBookletPaths, finalOutputPdf);
            result.Success = true;
            result.OutputPath = finalOutputPdf;
            result.PagesProcessed = processedBookletPaths.Count;
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
        }
        finally
        {
            PdfService.CleanupDirectory(bookletsFolder);
            foreach (var folder in bookletWorkingFolders)
            {
                PdfService.CleanupDirectory(folder);
            }
        }
        return result;
    }

    public async Task ProcessBookletAsync(string templatePdf, string inputPdf, string workingFolder, string outputPdf, int dpi)
    {
        //1. Split both PDFs
        var templatePages = await _pdfService.SplitPdfAsync(templatePdf, Path.Combine(workingFolder, "template_pages"));
        var inputPages = await _pdfService.SplitPdfAsync(inputPdf, Path.Combine(workingFolder, "input_pages"));

        if (templatePages.Count != inputPages.Count)
            throw new InvalidOperationException("Template and input PDF must have the same number of pages.");

        var processedPdfPages = new List<string>();

        for (int i = 0; i < inputPages.Count; i++)
        {
            //2. Convert each page to image
            string templateImg = Path.Combine(workingFolder, "template_images", $"template_{i + 1}.png");
            string inputImg = Path.Combine(workingFolder, "input_images", $"input_{i + 1}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(templateImg)!);
            Directory.CreateDirectory(Path.GetDirectoryName(inputImg)!);
            await _pdfService.ConvertPageToImageAsync(templatePages[i], 1, templateImg);
            await _pdfService.ConvertPageToImageAsync(inputPages[i], 1, inputImg);

            //3. Deskew and align input image to template image
            string deskewedImg = Path.Combine(workingFolder, "deskewed_images", $"deskewed_{i + 1}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(deskewedImg)!);
            await _imageProcessor.DeskewImageAsync(inputImg, deskewedImg);

            string redRemovedImg = deskewedImg;
            if (_enableRedPixelRemover && _redPixelRemover != null)
            {
                redRemovedImg = Path.Combine(workingFolder, "red_removed_images", $"red_removed_{i + 1}.png");
                Directory.CreateDirectory(Path.GetDirectoryName(redRemovedImg)!);
                await _redPixelRemover.RemoveRedPixelsAsync(deskewedImg, redRemovedImg, _redThreshold, dpi);
            }

            string alignedImg = Path.Combine(workingFolder, "aligned_images", $"aligned_{i + 1}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(alignedImg)!);
            await _imageProcessor.AlignImageAsync(redRemovedImg, templateImg, alignedImg);

            //4. Convert processed image back to PDF
            string processedPdf = Path.Combine(workingFolder, "processed_pages", $"processed_{i + 1}.pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(processedPdf)!);
            await _pdfService.ConvertImageToPdfAsync(alignedImg, processedPdf);
            processedPdfPages.Add(processedPdf);
        }

        //5. Merge all processed PDFs into final output
        await _pdfService.MergePdfsAsync(processedPdfPages, outputPdf);
        Console.WriteLine($"Final output PDF created: {outputPdf}");
    }
}
