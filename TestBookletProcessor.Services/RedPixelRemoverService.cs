using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;

namespace TestBookletProcessor.Services
{
    public class RedPixelRemoverService : IRedPixelRemoverService
    {
        // Original method without exclusion regions (backwards compatible)
        public async Task RemoveRedPixelsAsync(string inputImagePath, string outputImagePath, byte redThreshold, int dpi = 300)
        {
            await RemoveRedPixelsAsync(inputImagePath, outputImagePath, redThreshold, dpi, new List<RedPixelExclusionRegion>());
        }

        // Enhanced method with exclusion regions support
        public async Task RemoveRedPixelsAsync(
            string inputImagePath, 
            string outputImagePath, 
            byte redThreshold, 
            int dpi,
            List<RedPixelExclusionRegion> exclusionRegions)
        {
            await Task.Run(async () =>
            {
                using var image = await Image.LoadAsync<Rgba32>(inputImagePath);
                image.Metadata.HorizontalResolution = dpi;
                image.Metadata.VerticalResolution = dpi;

                // Create exclusion mask if there are regions to exclude
                bool[,]? exclusionMask = null;
                if (exclusionRegions != null && exclusionRegions.Count > 0)
                {
                    exclusionMask = CreateExclusionMask(image.Width, image.Height, exclusionRegions, dpi);
                    Console.WriteLine($"Created exclusion mask for {exclusionRegions.Count} region(s)");
                }

                // Process pixels with optional mask checking
                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var rowSpan = accessor.GetRowSpan(y);
                        for (int x = 0; x < rowSpan.Length; x++)
                        {
                            // Skip if this pixel is in an exclusion region
                            if (exclusionMask != null && exclusionMask[y, x])
                                continue;

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

        /// <summary>
        /// Creates a boolean mask indicating which pixels should be excluded from red removal.
        /// </summary>
        private bool[,] CreateExclusionMask(
            int imageWidth, 
            int imageHeight, 
            List<RedPixelExclusionRegion> regions, 
            int dpi)
        {
            var mask = new bool[imageHeight, imageWidth];

            foreach (var region in regions)
            {
                var (x, y, width, height) = region.ToPixelCoordinates(dpi);

                // Ensure bounds are within image
                int startX = Math.Max(0, x);
                int startY = Math.Max(0, y);
                int endX = Math.Min(imageWidth, x + width);
                int endY = Math.Min(imageHeight, y + height);

                // Mark pixels in this region as excluded
                for (int py = startY; py < endY; py++)
                {
                    for (int px = startX; px < endX; px++)
                    {
                        mask[py, px] = true;
                    }
                }

                Console.WriteLine($"Masked region '{region.Name}': ({startX},{startY}) to ({endX},{endY}) pixels");
            }

            return mask;
        }
    }
}