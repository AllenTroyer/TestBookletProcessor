using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;

namespace TestBookletProcessor.Services
{
    public class RedPixelRemoverService : IRedPixelRemoverService
    {
        // Added dpi parameter to support sharpness control
        public async Task RemoveRedPixelsAsync(string inputImagePath, string outputImagePath, byte redThreshold, int dpi = 300)
        {
            await Task.Run(async () =>
            {
                using var image = await Image.LoadAsync<Rgba32>(inputImagePath);
                image.Metadata.HorizontalResolution = dpi;
                image.Metadata.VerticalResolution = dpi;
                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var rowSpan = accessor.GetRowSpan(y);
                        for (int x = 0; x < rowSpan.Length; x++)
                        {
                            var pixel = rowSpan[x];
                            if (pixel.R >= redThreshold && pixel.R > pixel.G && pixel.R > pixel.B)
                            {
                                rowSpan[x] = new Rgba32(255, 255, 255, pixel.A);
                            }
                        }
                    }
                });
                await image.SaveAsync(outputImagePath);
            });
        }
    }
}