using SkiaSharp;
using ZXing;
using ZXing.SkiaSharp;

namespace QrRegionScanner;

public class RegionQrScanner
{
    /// <summary>
    /// Scans a specific region of an image for a QR code.
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="x">Left coordinate of the region</param>
    /// <param name="y">Top coordinate of the region</param>
    /// <param name="width">Width of the region</param>
    /// <param name="height">Height of the region</param>
    /// <returns>The decoded QR code string, or null if not found</returns>
    public string? ScanRegion(string imagePath, int x, int y, int width, int height)
    {
        using var originalBitmap = SKBitmap.Decode(imagePath);
        if (originalBitmap == null)
            throw new ArgumentException("Could not load image", nameof(imagePath));

        return ScanRegion(originalBitmap, x, y, width, height);
    }

    /// <summary>
    /// Scans a specific region of an image for a QR code.
    /// </summary>
    /// <param name="imageBytes">Image as byte array</param>
    /// <param name="x">Left coordinate of the region</param>
    /// <param name="y">Top coordinate of the region</param>
    /// <param name="width">Width of the region</param>
    /// <param name="height">Height of the region</param>
    /// <returns>The decoded QR code string, or null if not found</returns>
    public string? ScanRegion(byte[] imageBytes, int x, int y, int width, int height)
    {
        using var originalBitmap = SKBitmap.Decode(imageBytes);
        if (originalBitmap == null)
            throw new ArgumentException("Could not decode image bytes", nameof(imageBytes));

        return ScanRegion(originalBitmap, x, y, width, height);
    }

    /// <summary>
    /// Scans a specific region of an SKBitmap for a QR code.
    /// The region is clamped to the bitmap bounds, since scanned pages are frequently a few
    /// pixels smaller than nominal; a region entirely outside the image returns null.
    /// </summary>
    public string? ScanRegion(SKBitmap bitmap, int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Region dimensions must be positive");

        x = Math.Max(0, x);
        y = Math.Max(0, y);
        width = Math.Min(width, bitmap.Width - x);
        height = Math.Min(height, bitmap.Height - y);
        if (width <= 0 || height <= 0)
            return null;

        // Extract the region
        using var croppedBitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(croppedBitmap);

        var sourceRect = new SKRectI(x, y, x + width, y + height);
        var destRect = new SKRectI(0, 0, width, height);

        canvas.DrawBitmap(bitmap, sourceRect, destRect);

        // Scan for QR code
        var reader = new BarcodeReader
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new[] { BarcodeFormat.QR_CODE }
            }
        };

        var result = reader.Decode(croppedBitmap);
        return result?.Text;
    }
}