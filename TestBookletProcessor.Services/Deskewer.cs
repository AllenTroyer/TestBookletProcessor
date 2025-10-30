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
 // Step1: Input Validation
 // Definition: Checks if the input image file exists and is accessible.
 if (!File.Exists(imagePath))
 throw new FileNotFoundException($"Input image not found: {imagePath}");

 // Step2: Load Image in Grayscale
 // Definition: Loads the image as a single-channel grayscale image for processing.
 using var src = Cv2.ImRead(imagePath, ImreadModes.Grayscale);
 if (src.Empty()) throw new Exception("Failed to load image.");

 // Step3: Gaussian Blur
 // Definition: Applies a Gaussian blur to reduce noise and improve edge detection.
 using var blurred = new Mat();
 Cv2.GaussianBlur(src, blurred, new Size(5,5),0);

 // Step4: Edge Detection
 // Definition: Uses the Canny algorithm to find edges in the blurred image.
 using var edges = new Mat();
 Cv2.Canny(blurred, edges,50,150);

 // Step5: Find Contours
 // Definition: Finds contours (closed curves) in the edge-detected image.
 Cv2.FindContours(edges, out var contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

 // Step6: Validate Contours
 // Definition: Ensures that at least one contour was found for deskewing.
 if (contours.Length ==0)
 throw new Exception("No contours found in image.");

 // Step7: Find Largest Contour
 // Definition: Selects the contour with the largest area, assuming it's the main document region.
 var largestContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();

 // Step8: Get Rotated Rectangle
 // Definition: Fits a minimum-area rotated rectangle to the largest contour to estimate skew angle.
 var box = Cv2.MinAreaRect(largestContour);
 double angle = box.Angle;
 if (angle < -45) angle +=90;

 // Step9: Deskew Angle Threshold
 // Definition: Only deskew if the detected angle is significant (greater than2 degrees).
 if (Math.Abs(angle) >2) angle =0;
 double deskewAngle = angle;

 // Step10: Rotate Original Image
 // Definition: Rotates the original color image by the deskew angle to correct skew.
 // WarpAffine: Applies an affine transformation (rotation, translation, scaling) to an image using a transformation matrix.
 // In this context, WarpAffine rotates the image around its center by the calculated deskew angle, producing a deskewed output.
 using var orig = Cv2.ImRead(imagePath, ImreadModes.Color);
 var center = new Point2f(orig.Width /2f, orig.Height /2f);
 var rotMat = Cv2.GetRotationMatrix2D(center, deskewAngle,1.0);
 using var rotated = new Mat();
 Cv2.WarpAffine(orig, rotated, rotMat, orig.Size(), InterpolationFlags.Linear, BorderTypes.Constant);

 // Step11: Save Result
 // Definition: Saves the deskewed image to the specified output path.
 Cv2.ImWrite(outputPath, rotated);
 Console.WriteLine($"Deskewed image saved to: {outputPath} (angle: {deskewAngle:F2})");
 });
 }
 }
}
