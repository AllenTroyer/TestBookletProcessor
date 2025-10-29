using OpenCvSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;

namespace TestBookletProcessor.Services
{
 public class ImageAligner : IImageAligner
 {
 public async Task AlignImageAsync(string imagePath, string templatePath, string outputPath)
 {
 await Task.Run(() =>
 {
 if (!File.Exists(imagePath))
 throw new FileNotFoundException($"Input image not found: {imagePath}");
 if (!File.Exists(templatePath))
 throw new FileNotFoundException($"Template image not found: {templatePath}");

 using var img = Cv2.ImRead(imagePath, ImreadModes.Grayscale);
 using var template = Cv2.ImRead(templatePath, ImreadModes.Grayscale);

 var orb = ORB.Create();
 Mat descriptors1 = new Mat();
 Mat descriptors2 = new Mat();
 KeyPoint[] keypoints1, keypoints2;
 orb.DetectAndCompute(img, null, out keypoints1, descriptors1);
 orb.DetectAndCompute(template, null, out keypoints2, descriptors2);

 var bf = new BFMatcher(NormTypes.Hamming, crossCheck: true);
 var matches = bf.Match(descriptors1, descriptors2);

 if (matches.Length <4)
 throw new Exception("Not enough matches found for alignment.");

 var goodMatches = matches.OrderBy(m => m.Distance).Take(50).ToArray();
 var srcPoints = goodMatches.Select(m => keypoints1[m.QueryIdx].Pt).ToArray();
 var dstPoints = goodMatches.Select(m => keypoints2[m.TrainIdx].Pt).ToArray();

 var srcMat = Mat.FromArray(srcPoints);
 var dstMat = Mat.FromArray(dstPoints);
 var homography = Cv2.FindHomography(srcMat, dstMat, HomographyMethods.Ransac);

 if (homography.Empty())
 throw new Exception("Homography calculation failed.");

 using var imgColor = Cv2.ImRead(imagePath, ImreadModes.Color);
 using var aligned = new Mat();
 Cv2.WarpPerspective(imgColor, aligned, homography, template.Size());

 Cv2.ImWrite(outputPath, aligned);
 Console.WriteLine($"Aligned image saved to: {outputPath}");
 });
 }
 }
}
