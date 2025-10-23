using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestBookletProcessor.Core.Interfaces
{
    public interface IPdfService
    {
        Task<List<string>> SplitPdfAsync(string inputPath, string outputFolder);
        Task MergePdfsAsync(List<string> pdfPaths, string outputPath);
        Task ConvertPageToImageAsync(string pdfPath, int pageNumber, string outputFolder);
        Task ConvertImageToPdfAsync(string imagePath, string outputPath);
    }
}
