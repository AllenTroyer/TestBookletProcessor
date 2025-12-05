# QR Code Conditional Red Pixel Removal - Implementation Guide

## Overview
The booklet processor now supports conditional red pixel removal based on QR code content. After deskewing each page, the system scans for a QR code in a specified region. Depending on the QR code value, red pixel removal may or may not be applied to that page.

## How It Works

### Processing Flow
1. **PDF Page Conversion** ? Convert PDF page to image
2. **Deskewing** ? Straighten the image
3. **QR Code Scanning** ? Scan specified region for QR code (NEW)
4. **Conditional Red Pixel Removal** ? Apply only if QR code matches criteria (UPDATED)
5. **Image Alignment** ? Align to template
6. **PDF Conversion** ? Convert back to PDF

### Decision Logic
- If QR scanning is **disabled**: Use the global `EnableRedPixelRemover` setting
- If QR scanning is **enabled**:
  - **QR code found**: Check if value matches any configured trigger values
    - **Matches**: Apply red pixel removal
    - **No match**: Skip red pixel removal
  - **No QR code found**: Use the global `EnableRedPixelRemover` setting

## Configuration

### appsettings.json Structure
```json
{
  "BookletProcessor": {
    "EnableRedPixelRemover": true,
    "RedPixelThreshold": 225,
    "QrScanner": {
      "EnableQrScanning": true,
      "QrRegionX": 1950,
      "QrRegionY": 2700,
      "QrRegionWidth": 600,
      "QrRegionHeight": 600,
      "QrValuesRequiringRedRemoval": [
        "REDPEN",
        "TEACHER_MARKED",
        "MANUAL_GRADE"
      ]
    }
  }
}
```

### Configuration Parameters

#### QR Scanner Settings

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `EnableQrScanning` | boolean | `false` | Master switch for QR scanning feature |
| `QrRegionX` | integer | `1950` | Left coordinate of QR scan region (pixels) |
| `QrRegionY` | integer | `2700` | Top coordinate of QR scan region (pixels) |
| `QrRegionWidth` | integer | `600` | Width of QR scan region (pixels) |
| `QrRegionHeight` | integer | `600` | Height of QR scan region (pixels) |
| `QrValuesRequiringRedRemoval` | array | See config | QR values that trigger red pixel removal |

#### Default Region (Lower Right Corner, 2x2 inches @ 300 DPI)
- **Page size**: 2550 x 3300 pixels (8.5" x 11" @ 300 DPI)
- **QR region**: 600 x 600 pixels (2" x 2" @ 300 DPI)
- **Position**: Lower right corner
  - X: 2550 - 600 = 1950
  - Y: 3300 - 600 = 2700

## Use Cases

### Use Case 1: Teacher-Marked Tests
**Scenario**: Tests marked with red pen by teachers need red ink removal, while machine-scored tests don't.

**Setup**:
- Place QR code with value "TEACHER_MARKED" on teacher-marked tests
- Place QR code with value "MACHINE_SCORED" on machine-scored tests

**Result**:
- Pages with "TEACHER_MARKED" ? Red pixel removal applied
- Pages with "MACHINE_SCORED" ? Red pixel removal skipped

### Use Case 2: Selective Processing by Test Type
**Scenario**: Only certain test types need red ink removal.

**Setup**:
```json
"QrValuesRequiringRedRemoval": [
  "ESSAY_TEST",
  "SHORT_ANSWER",
  "FREE_RESPONSE"
]
```

**Result**:
- Multiple choice tests (no match) ? Skip red pixel removal
- Essay tests (match) ? Apply red pixel removal

### Use Case 3: Student-Specific Processing
**Scenario**: Apply red pixel removal based on student accommodation needs.

**Setup**:
- QR code contains student ID: "STUDENT_12345_REDPEN"
- Configure: `"QrValuesRequiringRedRemoval": ["REDPEN"]`

**Result**:
- System checks if QR value contains "REDPEN" (case-insensitive)
- If found ? Apply red pixel removal

## Console Output Examples

### QR Code Found - Matches Criteria
```
Page 1: QR code detected: TEACHER_MARKED
Page 1: QR code matches removal criteria - applying red pixel removal
```

### QR Code Found - No Match
```
Page 2: QR code detected: MACHINE_SCORED
Page 2: QR code does not match removal criteria - skipping red pixel removal
```

### QR Code Not Found
```
Page 3: No QR code detected - using default red pixel removal setting
```

### QR Scanning Error
```
Page 4: QR scanning error: Region extends beyond image boundaries - using default red pixel removal setting
```

## Customizing QR Code Position

### Different Positions for 8.5" x 11" @ 300 DPI

#### Upper Left Corner (1x1 inch)
```json
{
  "QrRegionX": 0,
  "QrRegionY": 0,
  "QrRegionWidth": 300,
  "QrRegionHeight": 300
}
```

#### Center of Page (1.5x1.5 inch)
```json
{
  "QrRegionX": 1050,
  "QrRegionY": 1425,
  "QrRegionWidth": 450,
  "QrRegionHeight": 450
}
```

#### Upper Right Corner (2x2 inch)
```json
{
  "QrRegionX": 1950,
  "QrRegionY": 0,
  "QrRegionWidth": 600,
  "QrRegionHeight": 600
}
```

#### Lower Left Corner (2x2 inch)
```json
{
  "QrRegionX": 0,
  "QrRegionY": 2700,
  "QrRegionWidth": 600,
  "QrRegionHeight": 600
}
```

### Different DPI Calculations

#### For 150 DPI
```
Page size: 1275 x 1650 pixels
2x2 inch QR: 300 x 300 pixels
Lower right: X=975, Y=1350
```

#### For 600 DPI
```
Page size: 5100 x 6600 pixels
2x2 inch QR: 1200 x 1200 pixels
Lower right: X=3900, Y=5400
```

#### Formula
```csharp
int dpi = 300; // Your DPI
double qrSizeInches = 2.0; // QR code size in inches

int pageWidth = (int)(8.5 * dpi);
int pageHeight = (int)(11 * dpi);
int qrSize = (int)(qrSizeInches * dpi);

// Lower right corner
int qrX = pageWidth - qrSize;
int qrY = pageHeight - qrSize;
```

## Advanced Configuration

### Case-Insensitive Matching
The system uses case-insensitive matching, so these are equivalent:
- "REDPEN" matches "redpen", "RedPen", "REDPEN"
- "Teacher_Marked" matches "TEACHER_MARKED", "teacher_marked"

### Partial Matching
The system checks if the QR value **contains** any of the configured strings:
- QR value: "STUDENT_12345_REDPEN_REQUIRED"
- Config: `["REDPEN"]`
- Result: **Match** (contains "REDPEN")

### Multiple Trigger Values
Any match triggers red pixel removal:
```json
"QrValuesRequiringRedRemoval": [
  "REDPEN",
  "TEACHER_MARKED",
  "MANUAL_GRADE",
  "ACCOMMODATION_RED"
]
```

## Troubleshooting

### QR Code Not Detected

**Check image resolution:**
```
Expected: 2550 x 3300 pixels (8.5x11 @ 300 DPI)
Check: View image properties or use image viewer
```

**Verify QR position:**
- Open image in editor (Paint, Photoshop, etc.)
- Measure QR code position in pixels
- Update config accordingly

**Check QR code quality:**
- Minimum size: 100x100 pixels for reliable detection
- High contrast required (dark QR on light background)
- Avoid excessive blur or distortion

### Red Pixel Removal Not Applied

**Check configuration:**
1. `EnableQrScanning` is `true`
2. `EnableRedPixelRemover` is `true`
3. QR region coordinates are correct
4. QR value matches one of the trigger values

**Check console output:**
- Look for QR scanning messages
- Verify detected QR value
- Check matching logic result

### QR Scanning Error

**"Region extends beyond image boundaries"**
- QR region is outside the image
- Check: `QrRegionX + QrRegionWidth <= ImageWidth`
- Check: `QrRegionY + QrRegionHeight <= ImageHeight`

**Solution**: Adjust region coordinates or ensure images are correct size

## Performance Considerations

### Impact on Processing Time
- QR scanning adds approximately **50-100ms per page**
- Impact is minimal compared to deskewing and alignment
- Can be disabled if not needed via `EnableQrScanning: false`

### Optimization Tips
1. **Minimize scan region**: Smaller regions scan faster
2. **Consistent QR placement**: Reduces failed scans
3. **High contrast QR codes**: Improves detection accuracy

## Testing

### Test Scenario 1: Basic QR Scanning
1. Create test image with QR code containing "REDPEN"
2. Set `EnableQrScanning: true`
3. Run booklet processing
4. Verify console output shows QR detection and red removal

### Test Scenario 2: Multiple Pages
1. Create PDF with mixed pages:
   - Page 1: QR "TEACHER_MARKED" (should remove red)
   - Page 2: QR "MACHINE_SCORED" (should not remove red)
   - Page 3: No QR code (use default setting)
2. Process and verify each page handled correctly

### Test Scenario 3: QR Scanning Disabled
1. Set `EnableQrScanning: false`
2. Process document
3. Verify no QR scanning messages in console
4. Verify red removal uses global setting

## Integration with WPF Application

The WPF application automatically loads QR scanner configuration from `appsettings.json`. The settings window currently controls:
- Enable Red Pixel Remover (global)
- Red Pixel Threshold

**Future Enhancement**: Add QR scanner settings to UI:
- Enable/disable QR scanning
- Configure QR region coordinates
- Edit trigger value list

## Code Architecture

### Key Components

**BookletProcessorService**
- Accepts `RegionQrScanner` as dependency
- Loads QR configuration parameters
- Performs conditional red pixel removal

**RegionQrScanner**
- Scans specified image region for QR codes
- Returns decoded string or null
- Supports file path and byte array input

### Extension Points

To add new QR-based logic:
1. Modify `ProcessBookletAsync` method
2. Use `qrCodeValue` variable (already captured)
3. Implement custom decision logic
4. Add configuration parameters as needed

### Example: Log QR Codes
```csharp
if (qrCodeValue != null)
{
    // Log to database or file
    await LogQrCode(i + 1, qrCodeValue, processedBookletPath);
}
```

## Migration Guide

### Upgrading from Previous Version

**Step 1**: Update `appsettings.json`
- Add `QrScanner` section (see Configuration above)

**Step 2**: No code changes required
- Existing code continues to work
- QR scanning is disabled by default

**Step 3**: Test
- Run with `EnableQrScanning: false` to verify existing behavior
- Enable and test QR scanning when ready

## Best Practices

1. **Start with QR scanning disabled**
   - Test existing workflow first
   - Enable QR scanning after validation

2. **Use consistent QR positioning**
   - Standardize across all documents
   - Document the chosen position

3. **Choose descriptive QR values**
   - Use readable codes: "TEACHER_MARKED" not "TM01"
   - Include documentation

4. **Test with sample documents**
   - Verify QR detection before production
   - Test various lighting/scanning conditions

5. **Monitor console output**
   - Check QR detection messages
   - Verify matching logic works correctly

6. **Keep trigger list updated**
   - Document why each value is included
   - Review periodically

## Support and Troubleshooting

For issues:
1. Check console output for QR scanning messages
2. Verify `appsettings.json` configuration
3. Test with QR Code Test mode (Console app, option 1)
4. Review this documentation

Common solutions:
- **No QR detected**: Check image size and QR position
- **Wrong behavior**: Verify trigger value list and matching logic
- **Performance issues**: Reduce QR region size or disable scanning
