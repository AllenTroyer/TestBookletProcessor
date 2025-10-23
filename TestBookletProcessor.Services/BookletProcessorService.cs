using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;

namespace TestBookletProcessor.Services;

public class BookletProcessorService
{
 private readonly IPdfService _pdfService;
 private readonly IImageProcessor _imageProcessor;

 public BookletProcessorService(IPdfService pdfService, IImageProcessor imageProcessor)
 {
 _pdfService = pdfService;
 _imageProcessor = imageProcessor;
 }

 public async Task ProcessBookletAsync(string templatePdf, string inputPdf, string workingFolder, string outputPdf)
 {
 //1. Split both PDFs
 var templatePages = await _pdfService.SplitPdfAsync(templatePdf, Path.Combine(workingFolder, "template_pages"));
 var inputPages = await _pdfService.SplitPdfAsync(inputPdf, Path.Combine(workingFolder, "input_pages"));

 if (templatePages.Count != inputPages.Count)
 throw new InvalidOperationException("Template and input PDF must have the same number of pages.");

 var processedPdfPages = new List<string>();

 for (int i =0; i < inputPages.Count; i++)
 {
 //2. Convert each page to image
 string templateImg = Path.Combine(workingFolder, "template_images", $"template_{i +1}.png");
 string inputImg = Path.Combine(workingFolder, "input_images", $"input_{i +1}.png");
 Directory.CreateDirectory(Path.GetDirectoryName(templateImg)!);
 Directory.CreateDirectory(Path.GetDirectoryName(inputImg)!);
 await _pdfService.ConvertPageToImageAsync(templatePages[i],1, templateImg);
 await _pdfService.ConvertPageToImageAsync(inputPages[i],1, inputImg);

 //3. Deskew and align input image to template image
 string deskewedImg = Path.Combine(workingFolder, "deskewed_images", $"deskewed_{i +1}.png");
 Directory.CreateDirectory(Path.GetDirectoryName(deskewedImg)!);
 await _imageProcessor.DeskewImageAsync(inputImg, deskewedImg);

 string alignedImg = Path.Combine(workingFolder, "aligned_images", $"aligned_{i +1}.png");
 Directory.CreateDirectory(Path.GetDirectoryName(alignedImg)!);
 await _imageProcessor.AlignImageAsync(deskewedImg, templateImg, alignedImg);

 //4. Convert processed image back to PDF
 string processedPdf = Path.Combine(workingFolder, "processed_pages", $"processed_{i +1}.pdf");
 Directory.CreateDirectory(Path.GetDirectoryName(processedPdf)!);
 await _pdfService.ConvertImageToPdfAsync(alignedImg, processedPdf);
 processedPdfPages.Add(processedPdf);
 }

 //5. Merge all processed PDFs into final output
 await _pdfService.MergePdfsAsync(processedPdfPages, outputPdf);
 Console.WriteLine($"Final output PDF created: {outputPdf}");
 }
}
