# DPI Preservation Fix - Documentation

## Problem Identified

The DPI (Dots Per Inch) was dropping from 300 to 96 during PDF-to-image-to-PDF conversion. This happened in two places:

### Issue 1: Hardcoded PageDimensions (Line 101 in PdfService.cs)
**Before:**
```csharp
using (var docReader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(1080, 1920)))
```

**Problem:** 
- Fixed pixel dimensions (1080x1920) were used regardless of desired DPI
- This resulted in approximately 150 DPI for US Letter size
- Not respecting the intended 300 DPI setting

**After:**
```csharp
// Calculate page dimensions based on DPI for US Letter size (8.5 x 11 inches)
int pageWidthPixels = (int)(8.5 * dpi);
int pageHeightPixels = (int)(11 * dpi);

using (var docReader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(pageWidthPixels, pageHeightPixels)))
```

**For 300 DPI:**
- Width: 8.5" × 300 = 2550 pixels
- Height: 11" × 300 = 3300 pixels

### Issue 2: Missing DPI Metadata in PNG (Lines 109-128 in PdfService.cs)
**Before:**
```csharp
using (var image = new SixLabors.ImageSharp.Image<Rgba32>(pageWidth, pageHeight))
{
    image.ProcessPixelRows(accessor => { ... });
    image.Save(outputImagePath, new PngEncoder());
}
```

**Problem:**
- ImageSharp defaults to 96 DPI when no metadata is set
- PNG files saved without DPI information
- Subsequent processing assumes 96 DPI

**After:**
```csharp
using (var image = new SixLabors.ImageSharp.Image<Rgba32>(pageWidth, pageHeight))
{
    // Set DPI metadata
    image.Metadata.HorizontalResolution = dpi;
    image.Metadata.VerticalResolution = dpi;
    image.Metadata.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;
    
    image.ProcessPixelRows(accessor => { ... });
    image.Save(outputImagePath, new PngEncoder());
}
```

## Changes Made

### 1. IPdfService Interface Update
**File:** `TestBookletProcessor.Core\Interfaces\IPdfService.cs`

Added DPI parameter with default value:
```csharp
Task ConvertPageToImageAsync(string pdfPath, int pageNumber, string outputImagePath, int dpi = 300);
```

### 2. PdfService Implementation Update
**File:** `TestBookletProcessor.Services\PdfService.cs`

**Changes:**
- Added `using SixLabors.ImageSharp.Metadata;`
- Added `dpi` parameter to `ConvertPageToImageAsync`
- Calculate `PageDimensions` based on DPI instead of using hardcoded values
- Set image metadata (HorizontalResolution, VerticalResolution, ResolutionUnits)

### 3. BookletProcessorService Update
**File:** `TestBookletProcessor.Services\BookletProcessorService.cs`

Updated calls to pass DPI:
```csharp
await _pdfService.ConvertPageToImageAsync(templatePages[i], 1, templateImg, dpi);
await _pdfService.ConvertPageToImageAsync(inputPages[i], 1, inputImg, dpi);
```

## DPI Calculation Reference

### Standard US Letter Page (8.5" × 11")

| DPI | Width (pixels) | Height (pixels) |
|-----|----------------|-----------------|
| 72  | 612            | 792             |
| 96  | 816            | 1056            |
| 150 | 1275           | 1650            |
| 200 | 1700           | 2200            |
| 300 | 2550           | 3300            |
| 600 | 5100           | 6600            |

### Formula
```csharp
int pageWidthPixels = (int)(widthInInches * dpi);
int pageHeightPixels = (int)(heightInInches * dpi);
```

## Verification

### Before Fix:
1. PDF ? Image: Creates image at inconsistent resolution
2. Image saved: PNG defaults to 96 DPI metadata
3. Image ? PDF: Interprets as 96 DPI, resulting in wrong size

### After Fix:
1. PDF ? Image: Creates image at exact DPI (e.g., 2550×3300 for 300 DPI)
2. Image saved: PNG has correct DPI metadata (300 DPI)
3. Image ? PDF: Preserves original document size and quality

## Testing Recommendations

1. **Verify PNG DPI:**
   - Open intermediate PNG files in an image viewer
   - Check properties/metadata for DPI value
   - Should show 300 DPI (or configured value)

2. **Verify Pixel Dimensions:**
   - For 300 DPI: Images should be 2550×3300 pixels
   - Check intermediate images in working folders

3. **Verify Final PDF:**
   - Open final PDF and check page size
   - Should remain 8.5" × 11"
   - Print preview should show correct dimensions

4. **Quality Check:**
   - Compare text sharpness before and after
   - Verify no scaling artifacts
   - Check that fine details are preserved

## Configuration

The DPI value is controlled in `appsettings.json`:

```json
{
  "BookletProcessor": {
    "DefaultDpi": 300
  }
}
```

This value is now properly propagated throughout the conversion pipeline:
1. Main application reads from config
2. Passes to `BookletProcessorService` constructor
3. Used in `ProcessBookletAsync` method
4. Passed to `ConvertPageToImageAsync`
5. Used for both PageDimensions calculation and PNG metadata

## Impact

### Performance
- Minimal impact on processing time
- Slightly larger intermediate PNG files at higher DPI (expected)

### Quality
- ? Maintains original document resolution
- ? Preserves fine details and text sharpness
- ? No unwanted scaling or interpolation
- ? Consistent DPI throughout pipeline

### Compatibility
- Backward compatible (default value of 300 DPI)
- Existing code without DPI parameter still works
- No breaking changes to public API

## Future Enhancements

Potential improvements:
1. Add DPI validation (reasonable range: 72-600)
2. Support different page sizes (A4, Legal, etc.)
3. Add DPI to configuration UI in SettingsWindow
4. Log DPI information during processing
5. Add DPI verification step before processing

## Related Files

- `TestBookletProcessor.Core\Interfaces\IPdfService.cs` - Interface definition
- `TestBookletProcessor.Services\PdfService.cs` - Implementation
- `TestBookletProcessor.Services\BookletProcessorService.cs` - Usage
- `TestBookletProcessor.WPF\appsettings.json` - Configuration
- `TestBookletProcessor.Console\appsettings.json` - Configuration
