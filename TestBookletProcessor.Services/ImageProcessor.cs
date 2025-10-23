using OpenCvSharp;
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

        await Task.Run(() =>
        {
            if (!System.IO.File.Exists(imagePath))
                throw new FileNotFoundException($"Input image not found: {imagePath}");

            // Load image in grayscale
            using var src = Cv2.ImRead(imagePath, ImreadModes.Grayscale);
            if (src.Empty()) throw new Exception("Failed to load image.");

            // Apply Gaussian blur to reduce noise
            using var blurred = new Mat();
            Cv2.GaussianBlur(src, blurred, new Size(5, 5), 0);

            // Edge detection
            using var edges = new Mat();
            Cv2.Canny(blurred, edges, 50, 150);

            // Find contours
            Cv2.FindContours(edges, out var contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

            if (contours.Length == 0)
                throw new Exception("No contours found in image.");

            // Find the largest contour
            var largestContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();

            // Get rotated rectangle from largest contour
            var box = Cv2.MinAreaRect(largestContour);
            double angle = box.Angle;
            if (angle < -45) angle += 90;
            if (Math.Abs(angle) > 15) angle = 0; // Only deskew for small angles

            double deskewAngle = angle; // Use positive angle for rotation

            // Rotate the original image (color) for better quality
            using var orig = Cv2.ImRead(imagePath, ImreadModes.Color);
            var center = new Point2f(orig.Width / 2f, orig.Height / 2f);
            var rotMat = Cv2.GetRotationMatrix2D(center, deskewAngle, 1.0);
            using var rotated = new Mat();
            Cv2.WarpAffine(orig, rotated, rotMat, orig.Size(), InterpolationFlags.Linear, BorderTypes.Constant);

            // Save result
            Cv2.ImWrite(outputPath, rotated);
            Console.WriteLine($"Deskewed image saved to: {outputPath} (angle: {deskewAngle:F2})");
        });
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
