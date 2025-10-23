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
        await Task.Run(() =>
        {
            if (!System.IO.File.Exists(imagePath))
                throw new FileNotFoundException($"Input image not found: {imagePath}");
            if (!System.IO.File.Exists(templatePath))
                throw new FileNotFoundException($"Template image not found: {templatePath}");

            // Load images in grayscale
            using var img = Cv2.ImRead(imagePath, ImreadModes.Grayscale);
            using var template = Cv2.ImRead(templatePath, ImreadModes.Grayscale);

            // Detect ORB keypoints and descriptors
            var orb = ORB.Create();
            Mat descriptors1 = new Mat();
            Mat descriptors2 = new Mat();
            KeyPoint[] keypoints1, keypoints2;
            orb.DetectAndCompute(img, null, out keypoints1, descriptors1);
            orb.DetectAndCompute(template, null, out keypoints2, descriptors2);

            // Match descriptors using BFMatcher
            var bf = new BFMatcher(NormTypes.Hamming, crossCheck: true);
            var matches = bf.Match(descriptors1, descriptors2);

            if (matches.Length < 4)
                throw new Exception("Not enough matches found for alignment.");

            // Sort matches by distance
            var goodMatches = matches.OrderBy(m => m.Distance).Take(50).ToArray();

            // Get matched keypoints
            var srcPoints = goodMatches.Select(m => keypoints1[m.QueryIdx].Pt).ToArray();
            var dstPoints = goodMatches.Select(m => keypoints2[m.TrainIdx].Pt).ToArray();

            // Convert Point2f[] to Mat for FindHomography
            var srcMat = Mat.FromArray(srcPoints);
            var dstMat = Mat.FromArray(dstPoints);
            var homography = Cv2.FindHomography(srcMat, dstMat, HomographyMethods.Ransac);

            if (homography.Empty())
                throw new Exception("Homography calculation failed.");

            // Warp input image to align with template
            using var imgColor = Cv2.ImRead(imagePath, ImreadModes.Color);
            using var aligned = new Mat();
            Cv2.WarpPerspective(imgColor, aligned, homography, template.Size());

            // Save result
            Cv2.ImWrite(outputPath, aligned);
            Console.WriteLine($"Aligned image saved to: {outputPath}");
        });
    }

    // Stub method for skew angle detection, not needed in current implementation
    public async Task<Double> DetectSkewAngleAsync(string imagePath)
    {
        await Task.CompletedTask;
        Console.WriteLine($"[STUB] Detecting skew angle for: {imagePath}");
        return 0.0; // No skew detected (stub)
    }
}
