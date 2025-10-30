using OpenCvSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;

namespace TestBookletProcessor.Services
{
    public enum MarginColor
    {
        Black,
        White
    }

    public class ImageAlignerAlt : IImageAligner
    {
        private const int DefaultFeatures = 8000;
        private const double RatioTestThreshold = 0.75;
        private const int MinimumMatches = 10;

        // Main interface method: aligns image and saves to outputPath, using white margin by default
        public async Task AlignImageAsync(string imagePath, string templatePath, string outputPath)
        {
            await Task.Run(() =>
            {
                if (!File.Exists(imagePath))
                    throw new FileNotFoundException($"Input image not found: {imagePath}");
                if (!File.Exists(templatePath))
                    throw new FileNotFoundException($"Template image not found: {templatePath}");

                using var inputImage = Cv2.ImRead(imagePath, ImreadModes.Color);
                using var templateImage = Cv2.ImRead(templatePath, ImreadModes.Color);

                using var aligned = AlignImage(inputImage, templateImage, MarginColor.White);
                Cv2.ImWrite(outputPath, aligned);
            });
        }

        // Aligns inputImage to templateImage using ORB and affine transform
        public static Mat AlignImage(Mat inputImage, Mat templateImage, MarginColor marginColor)
        {
            // Step1: Input Validation
            // Definition: Checks if the input and template images are valid and not empty.
            if (inputImage == null || inputImage.Empty())
                throw new ArgumentException("Input image is null or empty", nameof(inputImage));
            if (templateImage == null || templateImage.Empty())
                throw new ArgumentException("Template image is null or empty", nameof(templateImage));

            // Step2: Convert to Grayscale
            // Definition: Converts color images to grayscale to simplify feature detection.
            Mat templateGray = ConvertToGrayscale(templateImage);
            Mat inputGray = ConvertToGrayscale(inputImage);

            // Step3: Feature Detection with ORB
            // Definition: Detects keypoints and computes descriptors using the ORB algorithm.
            // ORB: Oriented FAST and Rotated BRIEF, a fast feature detector and descriptor.
            // Keypoints: Distinctive points in the image (corners, edges).
            // Descriptors: Numeric representations of keypoints for matching.
            using var orb = ORB.Create(DefaultFeatures);

            KeyPoint[] kpTemplate, kpInput;
            using var desTemplate = new Mat();
            using var desInput = new Mat();

            orb.DetectAndCompute(templateGray, null, out kpTemplate, desTemplate);
            orb.DetectAndCompute(inputGray, null, out kpInput, desInput);

            // Step4: Cleanup Grayscale Images
            // Definition: Disposes grayscale images if they were newly created to free memory.
            // Dispose: Releases resources used by an object.
            if (templateGray != templateImage) templateGray.Dispose();
            if (inputGray != inputImage) inputGray.Dispose();

            // Step5: Validate Feature Detection
            // Definition: Ensures that keypoints and descriptors were found in both images.
            if (desTemplate.Empty() || desInput.Empty() || kpTemplate.Length == 0 || kpInput.Length == 0)
                throw new ArgumentException("No keypoints/descriptors found in one of the images.");

            // Step6: Feature Matching
            // Definition: Matches descriptors between the template and input images using brute force.
            // BFMatcher: Brute Force Matcher, compares every descriptor from one image to every descriptor in the other.
            // Hamming Distance: Metric for comparing binary descriptors (counts differing bits).
            using var bf = new BFMatcher(NormTypes.Hamming);
            DMatch[][] matches = bf.KnnMatch(desTemplate, desInput, k: 2);

            // Step7: Filter Good Matches (Lowe's Ratio Test)
            // Definition: Filters matches using Lowe's ratio test to keep only reliable matches.
            // Lowe's Ratio Test: A match is good if the best match is much better than the second-best.
            var goodMatches = matches
                .Where(m => m.Length == 2 && m[0].Distance < RatioTestThreshold * m[1].Distance)
                .Select(m => m[0])
                .ToArray();

            // Step8: Validate Match Count
            // Definition: Ensures there are enough good matches to compute a reliable transformation.
            if (goodMatches.Length < MinimumMatches)
                throw new ArgumentException($"Not enough good matches found between the images. Found {goodMatches.Length}, need at least {MinimumMatches}.");

            // Step9: Extract Matching Point Pairs
            // Definition: Gets the coordinates of matching keypoints from both images.
            // Point2f: A structure representing a 2D point with floating-point coordinates.
            Point2f[] templatePts = goodMatches.Select(m => kpTemplate[m.QueryIdx].Pt).ToArray();
            Point2f[] inputPts = goodMatches.Select(m => kpInput[m.TrainIdx].Pt).ToArray();

            // Step10: Calculate Affine Transformation
            // Definition: Computes the affine transformation matrix using RANSAC.
            // Affine Transformation: A linear mapping that preserves points, straight lines, and planes.
            // RANSAC: Robust algorithm for fitting models to data with outliers.
            using var matrix = Cv2.EstimateAffinePartial2D(
                InputArray.Create(inputPts),
                InputArray.Create(templatePts),
                null,
                RobustEstimationAlgorithms.RANSAC,
                3.0
            );

            // Step11: Validate Transformation
            // Definition: Checks that the transformation matrix was successfully computed.
            if (matrix == null || matrix.Empty())
                throw new ArgumentException("Affine transformation calculation failed.");

            // Step12: Apply Transformation
            // Definition: Warps the input image using the computed affine transformation.
            // WarpAffine: Applies an affine transformation to an image.
            // Scalar: Represents color values for border filling.
            Mat alignedImage = new Mat();
            Scalar borderValue = marginColor == MarginColor.White
                ? new Scalar(255, 255, 255)
                : new Scalar(0, 0, 0);

            Cv2.WarpAffine(
                inputImage,
                alignedImage,
                matrix,
                new Size(templateImage.Width, templateImage.Height),
                borderValue: borderValue
            );

            // Step13: Return Result
            // Definition: Returns the aligned image as a new Mat object.
            // Mat: The OpenCV matrix/image type.
            return alignedImage;
        }

        // Converts image to grayscale if needed
        private static Mat ConvertToGrayscale(Mat image)
        {
            if (image.Channels() == 1)
                return image;

            Mat gray = new Mat();
            switch (image.Channels())
            {
                case 3:
                    Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
                    break;
                case 4:
                    Cv2.CvtColor(image, gray, ColorConversionCodes.BGRA2GRAY);
                    break;
                default:
                    throw new ArgumentException($"Unsupported number of channels: {image.Channels()}");
            }
            return gray;
        }
    }
}