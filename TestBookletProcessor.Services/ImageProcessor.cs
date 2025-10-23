using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;

namespace TestBookletProcessor.Services;

public class ImageProcessor : IImageProcessor
{
    public async Task DeskewImageAsync(string imagePath, string outputPath)
    {
        await Task.CompletedTask;
        Console.WriteLine($"[STUB] Deskewing image: {imagePath} -> {outputPath}");

    }

    public async Task AlignImageAsync(string imagePath, string templatePath, string outputPath)
    {
        await Task.CompletedTask;
        Console.WriteLine($"[STUB] Aligning image: {imagePath} with template: {templatePath}");

    }

    public async Task<Double> DetectSkewAngleAsync(string imagePath)
    {
        await Task.CompletedTask;
        Console.WriteLine($"[STUB] Detecting skew angle for: {imagePath}");
        return 0.0; // No skew detected (stub)
    }
}
