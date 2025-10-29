using OpenCvSharp;
using System;
using System.IO;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;

namespace TestBookletProcessor.Services
{
 public class Deskewer : IDeskewer
 {
 public async Task DeskewImageAsync(string imagePath, string outputPath)
 {
 await Task.Run(() =>
 {
 if (!File.Exists(imagePath))
 throw new FileNotFoundException($"Input image not found: {imagePath}");

 using var src = Cv2.ImRead(imagePath, ImreadModes.Grayscale);
 if (src.Empty()) throw new Exception("Failed to load image.");

 using var blurred = new Mat();
 Cv2.GaussianBlur(src, blurred, new Size(5,5),0);

 using var edges = new Mat();
 Cv2.Canny(blurred, edges,50,150);

 Cv2.FindContours(edges, out var contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

 if (contours.Length ==0)
 throw new Exception("No contours found in image.");

 var largestContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
 var box = Cv2.MinAreaRect(largestContour);
 double angle = box.Angle;
 if (angle < -45) angle +=90;
 if (Math.Abs(angle) >2) angle =0;
 double deskewAngle = angle;

 using var orig = Cv2.ImRead(imagePath, ImreadModes.Color);
 var center = new Point2f(orig.Width /2f, orig.Height /2f);
 var rotMat = Cv2.GetRotationMatrix2D(center, deskewAngle,1.0);
 using var rotated = new Mat();
 Cv2.WarpAffine(orig, rotated, rotMat, orig.Size(), InterpolationFlags.Linear, BorderTypes.Constant);

 Cv2.ImWrite(outputPath, rotated);
 Console.WriteLine($"Deskewed image saved to: {outputPath} (angle: {deskewAngle:F2})");
 });
 }
 }
}
