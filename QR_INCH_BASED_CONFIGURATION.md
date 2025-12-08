# QR Scanner Settings - Inch-Based Configuration

## Overview
The QR scanner settings have been converted from pixel-based to inch-based measurements. This allows the QR region to automatically scale correctly when the `DefaultDpi` setting changes, eliminating the need to recalculate pixel values manually.

## Problem Solved

### Before (Pixel-Based)
```json
{
  "DefaultDpi": 200,
  "QrScanner": {
    "QrRegionX": 888,        // Pixels at 200 DPI
    "QrRegionY": 1205,       // Pixels at 200 DPI
    "QrRegionWidth": 192,    // Pixels at 200 DPI
    "QrRegionHeight": 192    // Pixels at 200 DPI
  }
}
```

**Issue**: If you change `DefaultDpi` from 200 to 300, the QR region would still scan the same pixel coordinates (888, 1205), which would be the wrong physical location on the page.

### After (Inch-Based)
```json
{
  "DefaultDpi": 200,
  "QrScanner": {
    "QrRegionXInches": 4.44,      // Inches (physical measurement)
    "QrRegionYInches": 6.025,     // Inches (physical measurement)
    "QrRegionWidthInches": 0.96,  // Inches (physical measurement)
    "QrRegionHeightInches": 0.96  // Inches (physical measurement)
  }
}
```

**Solution**: The physical location is preserved. When DPI changes, pixel coordinates are automatically recalculated:
- At 200 DPI: 4.44" × 200 = 888 pixels
- At 300 DPI: 4.44" × 300 = 1332 pixels (correct scaling!)

## Changes Made

### 1. Configuration Files Updated

#### appsettings.json (Both WPF and Console)
**Old Keys:**
- `QrRegionX` ? `QrRegionXInches`
- `QrRegionY` ? `QrRegionYInches`
- `QrRegionWidth` ? `QrRegionWidthInches`
- `QrRegionHeight` ? `QrRegionHeightInches`

**Example Conversion** (from current 200 DPI settings):
```
X: 888 pixels ÷ 200 DPI = 4.44 inches
Y: 1205 pixels ÷ 200 DPI = 6.025 inches
Width: 192 pixels ÷ 200 DPI = 0.96 inches
Height: 192 pixels ÷ 200 DPI = 0.96 inches
```

### 2. BookletProcessorService Updated

**Constructor Parameters Changed:**
```csharp
// OLD - Pixel-based
public BookletProcessorService(
    ...,
    int qrRegionX = 1950,
    int qrRegionY = 2700,
    int qrRegionWidth = 600,
    int qrRegionHeight = 600)

// NEW - Inch-based
public BookletProcessorService(
    ...,
    double qrRegionXInches = 6.5,
    double qrRegionYInches = 9.0,
    double qrRegionWidthInches = 2.0,
    double qrRegionHeightInches = 2.0)
```

**Pixel Calculation** (inside constructor):
```csharp
// Calculate pixel values from inches and DPI
_qrRegionX = (int)(qrRegionXInches * dpi);
_qrRegionY = (int)(qrRegionYInches * dpi);
_qrRegionWidth = (int)(qrRegionWidthInches * dpi);
_qrRegionHeight = (int)(qrRegionHeightInches * dpi);
```

### 3. MainWindow.xaml.cs Updated

Both constructor and settings reload sections now read inch-based values:

```csharp
// Read inch-based settings
double qrXInches = double.TryParse(_config?["BookletProcessor:QrScanner:QrRegionXInches"], out var xi) ? xi : 6.5;
double qrYInches = double.TryParse(_config?["BookletProcessor:QrScanner:QrRegionYInches"], out var yi) ? yi : 9.0;
double qrWidthInches = double.TryParse(_config?["BookletProcessor:QrScanner:QrRegionWidthInches"], out var wi) ? wi : 2.0;
double qrHeightInches = double.TryParse(_config?["BookletProcessor:QrScanner:QrRegionHeightInches"], out var hi) ? hi : 2.0;

// Pass to BookletProcessorService (pixel calculation happens there)
_bookletProcessor = new BookletProcessorService(..., qrXInches, qrYInches, qrWidthInches, qrHeightInches, ...);
```

### 4. Console Program.cs Updated

Enhanced console output to show both inch and pixel values:

```csharp
if (enableQrScanning)
{
    Console.WriteLine($"QR region (inches): X={qrXInches:F2}, Y={qrYInches:F2}, Width={qrWidthInches:F2}, Height={qrHeightInches:F2}");
    Console.WriteLine($"QR region (pixels @ {dpi} DPI): X={qrXInches * dpi:F0}, Y={qrYInches * dpi:F0}, Width={qrWidthInches * dpi:F0}, Height={qrHeightInches * dpi:F0}");
}
```

## Configuration Examples

### Example 1: Lower Right Corner (2x2 inch QR code)
For US Letter (8.5" × 11") with QR in lower right:
```json
{
  "QrRegionXInches": 6.5,   // 8.5" - 2.0" = 6.5"
  "QrRegionYInches": 9.0,   // 11.0" - 2.0" = 9.0"
  "QrRegionWidthInches": 2.0,
  "QrRegionHeightInches": 2.0
}
```

**At different DPIs:**
- 150 DPI: Region = 975 × 1350, Size = 300 × 300 pixels
- 200 DPI: Region = 1300 × 1800, Size = 400 × 400 pixels
- 300 DPI: Region = 1950 × 2700, Size = 600 × 600 pixels
- 600 DPI: Region = 3900 × 5400, Size = 1200 × 1200 pixels

### Example 2: Center of Page (1.5x1.5 inch QR code)
```json
{
  "QrRegionXInches": 3.5,   // (8.5" - 1.5") / 2 = 3.5"
  "QrRegionYInches": 4.75,  // (11.0" - 1.5") / 2 = 4.75"
  "QrRegionWidthInches": 1.5,
  "QrRegionHeightInches": 1.5
}
```

### Example 3: Upper Left Corner (1x1 inch QR code)
```json
{
  "QrRegionXInches": 0.5,   // 0.5" margin from left
  "QrRegionYInches": 0.5,   // 0.5" margin from top
  "QrRegionWidthInches": 1.0,
  "QrRegionHeightInches": 1.0
}
```

### Example 4: Current WPF Settings
```json
{
  "DefaultDpi": 200,
  "QrScanner": {
    "QrRegionXInches": 4.44,
    "QrRegionYInches": 6.025,
    "QrRegionWidthInches": 0.96,
    "QrRegionHeightInches": 0.96
  }
}
```

## Conversion Formula

### From Pixels to Inches
```
Inches = Pixels ÷ DPI
```

**Example**: If you have pixel coordinates at 200 DPI:
```
X: 888 pixels ÷ 200 DPI = 4.44 inches
Y: 1205 pixels ÷ 200 DPI = 6.025 inches
Width: 192 pixels ÷ 200 DPI = 0.96 inches
Height: 192 pixels ÷ 200 DPI = 0.96 inches
```

### From Inches to Pixels (at runtime)
```
Pixels = Inches × DPI
```

**Example**: At 300 DPI:
```
X: 4.44 inches × 300 DPI = 1332 pixels
Y: 6.025 inches × 300 DPI = 1807.5 ? 1808 pixels
Width: 0.96 inches × 300 DPI = 288 pixels
Height: 0.96 inches × 300 DPI = 288 pixels
```

## Benefits

### 1. DPI Independence
Change `DefaultDpi` without updating QR coordinates:
```json
// Change from 200 to 300 DPI - QR region automatically scales!
"DefaultDpi": 300  // Was 200
```

### 2. Physical Accuracy
Measurements represent actual physical locations on the page:
- Easy to measure with a ruler
- Independent of scan resolution
- Portable across different DPI settings

### 3. Easier Configuration
Think in physical measurements:
- "QR code is 1 inch from bottom-right"
- Not: "QR code is at pixel 2700 at 300 DPI"

### 4. Consistency Across Projects
WPF and Console apps use same inch-based measurements, automatically adapting to their respective DPI settings.

## Migration Guide

### For Existing Configurations

1. **Note your current DPI** (e.g., 200)

2. **Convert pixel values to inches:**
   ```
   XInches = CurrentX ÷ CurrentDPI
   YInches = CurrentY ÷ CurrentDPI
   WidthInches = CurrentWidth ÷ CurrentDPI
   HeightInches = CurrentHeight ÷ CurrentDPI
   ```

3. **Update appsettings.json:**
   - Rename keys (add "Inches" suffix)
   - Use calculated inch values
   - Use decimal numbers (not integers)

4. **Test at current DPI** (should work identically)

5. **Test at different DPI** (verify scaling works)

### Example Migration

**Before (at 200 DPI):**
```json
{
  "QrRegionX": 888,
  "QrRegionY": 1205,
  "QrRegionWidth": 192,
  "QrRegionHeight": 192
}
```

**After:**
```json
{
  "QrRegionXInches": 4.44,
  "QrRegionYInches": 6.025,
  "QrRegionWidthInches": 0.96,
  "QrRegionHeightInches": 0.96
}
```

## Verification

### Console Output Example
When running the Console app with QR scanning enabled:
```
QR scanning enabled: True
QR region (inches): X=4.44, Y=6.03, Width=0.96, Height=0.96
QR region (pixels @ 200 DPI): X=888, Y=1205, Width=192, Height=192
```

This confirms:
- Inch-based configuration is loaded correctly
- Pixel calculation is correct for current DPI
- Both values are displayed for verification

### Testing Different DPIs

**At 200 DPI:**
- Inches stay the same: 4.44, 6.025, 0.96, 0.96
- Pixels: 888, 1205, 192, 192

**At 300 DPI:**
- Inches stay the same: 4.44, 6.025, 0.96, 0.96
- Pixels: 1332, 1808, 288, 288 (automatically scaled!)

## Troubleshooting

### QR Code Not Found After DPI Change

**Possible Cause**: Old pixel-based configuration still present

**Solution**:
1. Check appsettings.json uses "*Inches" keys
2. Verify inch values are decimal (e.g., 4.44, not 4)
3. Rebuild and restart application

### Incorrect Scaling

**Possible Cause**: Inch values calculated at wrong DPI

**Solution**:
1. Re-calculate from known working pixel coordinates
2. Use formula: Inches = Pixels ÷ OriginalDPI
3. Verify with console output

### Configuration Not Loading

**Possible Cause**: Typo in key names

**Solution**:
- Keys must be: `QrRegionXInches`, `QrRegionYInches`, etc.
- Case-sensitive!
- Check for extra spaces or typos

## Best Practices

1. **Measure in Physical Units**
   - Use a ruler to measure QR position on test page
   - Convert measurements to inches
   - Enter directly into config

2. **Use Decimal Values**
   - Inches should be decimal: `4.44`, not `4`
   - Precision to 2 decimal places is usually sufficient

3. **Document Your Measurements**
   - Add comments to appsettings.json (if JSON5 supported)
   - Or maintain separate documentation

4. **Test at Multiple DPIs**
   - Verify QR detection works at 150, 200, 300, 600 DPI
   - Confirm physical location stays consistent

5. **Standard Positions**
   - Consider using standard positions (e.g., always lower-right)
   - Makes troubleshooting easier
   - Simplifies template design

## Related Files Modified

- `TestBookletProcessor.WPF\appsettings.json` - Configuration
- `TestBookletProcessor.Console\appsettings.json` - Configuration
- `TestBookletProcessor.Services\BookletProcessorService.cs` - Service implementation
- `TestBookletProcessor.WPF\MainWindow.xaml.cs` - WPF configuration loading
- `TestBookletProcessor.Console\Program.cs` - Console configuration loading

## Future Enhancements

Potential improvements:
1. Add inch-based settings to SettingsWindow UI
2. Add visual QR region preview in WPF app
3. Support multiple QR codes at different positions
4. Add QR region validation (within page bounds)
5. Support different page sizes (A4, Legal, etc.)
