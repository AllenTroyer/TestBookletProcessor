using System.Threading.Tasks;

namespace TestBookletProcessor.Core.Interfaces
{
 public interface IImageAligner
 {
 Task AlignImageAsync(string imagePath, string templatePath, string outputPath);
 }
}
