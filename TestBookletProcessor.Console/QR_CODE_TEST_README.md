# QR Code Scanner Test Guide

## Overview
The Console application now includes a QR code scanner test mode specifically designed for scanning QR codes from scanned document pages.

## Test Setup

### 1. Prepare Your Test Image
- **Image Specifications:**
  - Page size: 8.5 x 11 inches
  - Resolution: 300 DPI
  - Resulting pixel dimensions: 2550 x 3300 pixels
  - Format: PNG (recommended), or any format supported by SkiaSharp

- **QR Code Position:**
  - Location: Lower right corner
  - Size: 2 x 2 inch square (600 x 600 pixels at 300 DPI)
  - Pixel coordinates: X=1950, Y=2700, Width=600, Height=600

### 2. Place Test Image
Place your test image at:
```
C:\TestBooklets\Input\test_page.png
```

You can modify the path in `Program.cs` if needed.

## Running the Test

### Option 1: From Visual Studio
1. Set `TestBookletProcessor.Console` as the startup project
2. Press F5 or click "Run"
3. When prompted, enter `1` to select QR Code Scanner Test

### Option 2: From Command Line
```bash
cd TestBookletProcessor.Console\bin\Debug\net8.0
TestBookletProcessor.Console.exe
# Enter 1 when prompted
```

## Expected Output

### Success Case:
```
=== QR Code Scanner Test ===
Test image path: C:\TestBooklets\Input\test_page.png

Scanning region:
  X: 1950, Y: 2700
  Width: 600, Height: 600
  (Lower right corner, 2x2 inch square at 300 DPI)

Scanning for QR code...

? SUCCESS: QR code found!
QR Code Content: [Your QR code data]

Testing with byte array method...
? Byte array method also successful: [Your QR code data]

Test completed. Press any key to exit.
```

### Failure Case:
```
? No QR code found in the specified region.
Troubleshooting tips:
  - Verify the image is 2550 x 3300 pixels (8.5x11 @ 300 DPI)
  - Ensure the QR code is in the lower right 2x2 inch area
  - Check that the QR code is clear and not distorted
```

## Customizing the Region

If your QR code is in a different location or size, modify these values in `Program.cs`:

```csharp
// Current settings for lower right corner, 2x2 inch square
int pageWidthPixels = 2550;  // 8.5 inches * 300 DPI
int pageHeightPixels = 3300; // 11 inches * 300 DPI
int qrSizePixels = 600;      // 2 inches * 300 DPI

int qrX = pageWidthPixels - qrSizePixels;  // 1950
int qrY = pageHeightPixels - qrSizePixels; // 2700
```

### Examples for Other Positions:

**Upper Left Corner (2x2 inch):**
```csharp
int qrX = 0;
int qrY = 0;
```

**Center of Page (1.5x1.5 inch):**
```csharp
int qrSizePixels = 450;  // 1.5 inches * 300 DPI
int qrX = (pageWidthPixels - qrSizePixels) / 2;   // 1050
int qrY = (pageHeightPixels - qrSizePixels) / 2;  // 1425
```

**Custom Position:**
```csharp
// For a 1x1 inch QR code starting 1 inch from left, 2 inches from top
int qrSizePixels = 300;  // 1 inch * 300 DPI
int qrX = 300;           // 1 inch * 300 DPI
int qrY = 600;           // 2 inches * 300 DPI
```

## Technical Details

### Scanner Features:
- Uses ZXing library for QR code detection
- Auto-rotation enabled for tilted QR codes
- "Try Harder" mode for difficult scans
- Supports both file path and byte array input

### Dependencies:
- **QrRegionScanner** project
- **ZXing** for barcode detection
- **SkiaSharp** for image processing

## Troubleshooting

### "Test image not found" Error
- Ensure the image exists at `C:\TestBooklets\Input\test_page.png`
- Check file permissions
- Verify the path is correct for your system

### QR Code Not Detected
1. **Check image dimensions:**
   ```csharp
   using SkiaSharp;
   var bitmap = SKBitmap.Decode("test_page.png");
   Console.WriteLine($"Image size: {bitmap.Width}x{bitmap.Height}");
   ```

2. **Verify QR code position:**
   - Use an image editor to measure pixel coordinates
   - Ensure the QR code is within the specified region

3. **Check QR code quality:**
   - Minimum size: ~100x100 pixels for reliable detection
   - Ensure good contrast (dark QR on light background)
   - Avoid excessive blur or distortion

4. **Try larger scan region:**
   - Increase width/height by 10-20% to account for positioning errors
   - Example: Use 700x700 pixels instead of 600x600

### Different DPI Images
If your image has a different DPI (e.g., 150 DPI or 600 DPI):

```csharp
int dpi = 150;  // Your actual DPI
int pageWidthPixels = (int)(8.5 * dpi);   // Width in pixels
int pageHeightPixels = (int)(11 * dpi);   // Height in pixels
int qrSizePixels = (int)(2 * dpi);        // 2x2 inch QR code
```

## Next Steps

After successful testing, you can integrate the QR code scanner into the main booklet processing workflow to:
- Extract student IDs or test identifiers
- Validate documents before processing
- Automate file naming based on QR code content
- Track and log processed documents
