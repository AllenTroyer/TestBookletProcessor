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
            if (inputImage == null || inputImage.Empty())
                throw new ArgumentException("Input image is null or empty", nameof(inputImage));
            if (templateImage == null || templateImage.Empty())
                throw new ArgumentException("Template image is null or empty", nameof(templateImage));

            Mat templateGray = ConvertToGrayscale(templateImage);
            Mat inputGray = ConvertToGrayscale(inputImage);

            using var orb = ORB.Create(DefaultFeatures);

            KeyPoint[] kpTemplate, kpInput;
            using var desTemplate = new Mat();
            using var desInput = new Mat();

            orb.DetectAndCompute(templateGray, null, out kpTemplate, desTemplate);
            orb.DetectAndCompute(inputGray, null, out kpInput, desInput);

            if (templateGray != templateImage) templateGray.Dispose();
            if (inputGray != inputImage) inputGray.Dispose();

            if (desTemplate.Empty() || desInput.Empty() || kpTemplate.Length == 0 || kpInput.Length == 0)
                throw new ArgumentException("No keypoints/descriptors found in one of the images.");

            using var bf = new BFMatcher(NormTypes.Hamming);
            DMatch[][] matches = bf.KnnMatch(desTemplate, desInput, k: 2);

            var goodMatches = matches
                .Where(m => m.Length == 2 && m[0].Distance < RatioTestThreshold * m[1].Distance)
                .Select(m => m[0])
                .ToArray();

            if (goodMatches.Length < MinimumMatches)
                throw new ArgumentException($"Not enough good matches found between the images. Found {goodMatches.Length}, need at least {MinimumMatches}.");

            Point2f[] templatePts = goodMatches.Select(m => kpTemplate[m.QueryIdx].Pt).ToArray();
            Point2f[] inputPts = goodMatches.Select(m => kpInput[m.TrainIdx].Pt).ToArray();

            using var matrix = Cv2.EstimateAffinePartial2D(
                InputArray.Create(inputPts),
                InputArray.Create(templatePts),
                null,
                RobustEstimationAlgorithms.RANSAC,
                3.0
            );

            if (matrix == null || matrix.Empty())
                throw new ArgumentException("Affine transformation calculation failed.");

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