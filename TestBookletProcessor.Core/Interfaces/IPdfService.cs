using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TestBookletProcessor.Core.Interfaces
{
    public interface IPdfService
    {
        Task<List<string>> SplitPdfAsync(string inputPath, string outputFolder);
        Task MergePdfsAsync(List<string> pdfPaths, string outputPath);
        Task ConvertPageToImageAsync(string pdfPath, int pageNumber, string outputImagePath);
        Task ConvertImageToPdfAsync(string imagePath, string outputPath);
        Task<List<string>> SplitIntoBookletsAsync(string inputPdfPath, string templatePdfPath, string outputFolder);
    }
}
