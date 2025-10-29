using System.Threading.Tasks;

namespace TestBookletProcessor.Core.Interfaces
{
 public interface IDeskewer
 {
 Task DeskewImageAsync(string imagePath, string outputPath);
 }
}
