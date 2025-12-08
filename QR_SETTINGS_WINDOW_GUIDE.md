# QR Scanner Settings in SettingsWindow - User Guide

## Overview
The SettingsWindow now includes comprehensive QR scanner configuration options, allowing you to control QR code scanning behavior directly from the UI without manually editing appsettings.json.

## New Settings Added

### QR Scanner Section
The settings window has been expanded with a dedicated "QR Scanner Settings" section that includes:

1. **Enable QR Scanning** - Checkbox to turn QR scanning on/off
2. **QR Region X (inches)** - Horizontal position of QR scan region
3. **QR Region Y (inches)** - Vertical position of QR scan region
4. **QR Region Width (inches)** - Width of QR scan region
5. **QR Region Height (inches)** - Height of QR scan region
6. **QR Exclusion Patterns** - Multi-line text box for wildcard patterns

## Accessing Settings

### From Main Window
1. Click the **Settings** button (or menu item)
2. The SettingsWindow opens with current configuration loaded
3. Scroll down to see "QR Scanner Settings" section
4. Make changes
5. Click **Save** to apply changes
6. Application automatically reloads configuration

## Field Details

### Enable QR Scanning
**Type:** Checkbox
**Default:** Unchecked (disabled)
**Description:** Master switch for QR code scanning feature

**When Enabled:**
- System scans for QR codes in specified region after deskewing
- QR code content determines if red pixel removal is applied
- Console shows QR detection messages

**When Disabled:**
- No QR scanning performed
- Red pixel removal uses global `EnableRedPixelRemover` setting
- Faster processing (skips QR scan step)

### QR Region X (inches)
**Type:** Decimal number
**Default:** 6.5
**Valid Range:** 0 to 8.5 (page width)
**Description:** Horizontal distance from left edge of page to left edge of QR scan region

**Examples:**
- `0.5` = Half inch from left edge
- `4.25` = 4.25 inches from left edge
- `6.5` = 6.5 inches from left (for 2" QR in lower right of 8.5" page)

**For 8.5" x 11" Page:**
- Lower right corner (2x2" QR): `6.5` (8.5 - 2.0)
- Upper right corner (2x2" QR): `6.5`
- Center (1.5x1.5" QR): `3.5` ((8.5 - 1.5) / 2)
- Upper left corner (1x1" QR): `0.5` (0.5" margin)

### QR Region Y (inches)
**Type:** Decimal number
**Default:** 9.0
**Valid Range:** 0 to 11.0 (page height)
**Description:** Vertical distance from top edge of page to top edge of QR scan region

**Examples:**
- `0.5` = Half inch from top
- `5.25` = 5.25 inches from top (middle of page)
- `9.0` = 9 inches from top (for 2" QR in lower right of 11" page)

**For 8.5" x 11" Page:**
- Lower right corner (2x2" QR): `9.0` (11.0 - 2.0)
- Upper right corner (2x2" QR): `0.5` (0.5" margin)
- Center (1.5x1.5" QR): `4.75` ((11.0 - 1.5) / 2)
- Lower left corner (1x1" QR): `10.0` (11.0 - 1.0)

### QR Region Width (inches)
**Type:** Decimal number
**Default:** 2.0
**Valid Range:** > 0, should not exceed page width
**Description:** Width of QR scan region

**Recommendations:**
- Match actual QR code size on your documents
- Add small margin for positioning tolerance (e.g., 0.1-0.2")
- Common sizes:
  - Small QR: `1.0` inch
  - Medium QR: `1.5` inches
  - Large QR: `2.0` inches
  - Extra large QR: `3.0` inches

**Note:** Larger region = slower scanning, but more tolerant to positioning

### QR Region Height (inches)
**Type:** Decimal number
**Default:** 2.0
**Valid Range:** > 0, should not exceed page height
**Description:** Height of QR scan region

**Recommendations:**
- Usually same as width for square QR codes
- Can be different for rectangular QR codes
- Common sizes match width recommendations above

### QR Exclusion Patterns
**Type:** Multi-line text box
**Default:** `*-FRTCVR, CLEAN`
**Format:** Comma-separated patterns (can also use semicolons or newlines)
**Description:** List of wildcard patterns for QR codes that should SKIP red pixel removal

**Wildcard Syntax:**
- `*` = Matches one or more characters
- Patterns are case-insensitive
- No wildcards = exact match required

**Entry Formats (all valid):**
```
Comma-separated:
*-FRTCVR, *-BACKCOVER, CLEAN

Semicolon-separated:
*-FRTCVR; *-BACKCOVER; CLEAN

One per line:
*-FRTCVR
*-BACKCOVER
CLEAN

Mixed:
*-FRTCVR, *-BACKCOVER
CLEAN
```

**Example Patterns:**

| Pattern | Matches | Use Case |
|---------|---------|----------|
| `*-FRTCVR` | `APT24A-FRTCVR`, `TEST-FRTCVR` | Front cover pages |
| `*-BACKCOVER` | `APT24B-BACKCOVER`, `X-BACKCOVER` | Back cover pages |
| `CLEAN` | `CLEAN` only | Exact match for clean templates |
| `TEMPLATE-*` | `TEMPLATE-A`, `TEMPLATE-MASTER` | Template variations |
| `APT2*-0*` | `APT24A-01`, `APT25B-02` | Specific test series |

**Tooltip:**
Hover over the text box to see: "Enter patterns separated by commas. Supports wildcards (*). Example: *-FRTCVR, CLEAN"

## Validation

### Input Validation
The settings window validates your input before saving:

**Red Pixel Threshold:**
- Must be 0-255
- Error: "Red Pixel Threshold must be a number between 0 and 255."

**QR Region X:**
- Must be positive number
- Error: "QR Region X must be a positive number."

**QR Region Y:**
- Must be positive number
- Error: "QR Region Y must be a positive number."

**QR Region Width:**
- Must be positive number (> 0)
- Error: "QR Region Width must be a positive number."

**QR Region Height:**
- Must be positive number (> 0)
- Error: "QR Region Height must be a positive number."

**QR Exclusion Patterns:**
- No validation - any text accepted
- Empty patterns are filtered out automatically
- Leading/trailing whitespace trimmed

### Input Restrictions

**Numeric Fields (Red Pixel Threshold):**
- Only digits 0-9 allowed
- No decimal points
- Paste blocked if non-numeric

**Decimal Fields (QR Region coordinates):**
- Digits 0-9 allowed
- One decimal point allowed
- Examples: `6.5`, `1.25`, `10.0`, `2`

**Text Fields (Exclusion Patterns):**
- Any text allowed
- Multi-line supported
- Special characters allowed

## Usage Scenarios

### Scenario 1: Default Lower Right Corner
**Use Case:** QR code in lower right corner of standard letter page

**Settings:**
- Enable QR Scanning: ? Checked
- QR Region X: `6.5`
- QR Region Y: `9.0`
- QR Region Width: `2.0`
- QR Region Height: `2.0`
- Exclusion Patterns: `*-FRTCVR, CLEAN`

**Result:** Scans 2x2 inch region in lower right, skips red removal for front covers

### Scenario 2: Upper Left Small QR
**Use Case:** 1 inch QR code in upper left corner

**Settings:**
- Enable QR Scanning: ? Checked
- QR Region X: `0.5`
- QR Region Y: `0.5`
- QR Region Width: `1.0`
- QR Region Height: `1.0`
- Exclusion Patterns: `TEMPLATE-*, CLEAN`

**Result:** Scans small region in upper left, skips red removal for templates

### Scenario 3: Center QR with Multiple Patterns
**Use Case:** 1.5 inch QR code centered on page, multiple exclusion types

**Settings:**
- Enable QR Scanning: ? Checked
- QR Region X: `3.5`
- QR Region Y: `4.75`
- QR Region Width: `1.5`
- QR Region Height: `1.5`
- Exclusion Patterns: 
  ```
  *-COVER
  CLEAN
  TEMPLATE-*
  NO_RED_INK
  ```

**Result:** Scans center region, multiple exclusion criteria

### Scenario 4: Disable QR Scanning
**Use Case:** Temporarily disable QR feature without losing settings

**Settings:**
- Enable QR Scanning: ? Unchecked
- (Other settings preserved but ignored)

**Result:** QR scanning skipped, settings remain for future use

## DPI Independence

**Important:** All QR region measurements are in inches, not pixels!

This means:
- Settings automatically adapt when you change `DefaultDpi`
- Same physical location on page regardless of scan resolution
- No need to recalculate coordinates for different DPIs

**Example:**
```
Settings: X=6.5", Y=9.0", Width=2.0", Height=2.0"

At 150 DPI: 975 × 1350 pixels, size 300 × 300 pixels
At 200 DPI: 1300 × 1800 pixels, size 400 × 400 pixels
At 300 DPI: 1950 × 2700 pixels, size 600 × 600 pixels
At 600 DPI: 3900 × 5400 pixels, size 1200 × 1200 pixels

Physical location: Always 6.5" from left, 9.0" from top ?
```

## Workflow

### Initial Setup
1. Open Settings window
2. Check "Enable QR Scanning"
3. Measure QR code position on physical document with ruler
4. Enter measurements in inches
5. Enter exclusion patterns based on QR code scheme
6. Click Save
7. Test with sample document

### Adjusting Settings
1. If QR codes not detected:
   - Increase region size slightly
   - Verify measurements with ruler
   - Check console output for detection messages

2. If wrong pages excluded:
   - Review exclusion patterns
   - Make patterns more specific
   - Test with sample QR codes

3. If processing too slow:
   - Reduce QR region size if possible
   - Only enable for documents that need it

### Testing Changes
1. Save settings
2. Process test document
3. Check console output:
   ```
   Page 1: QR code detected: APT24A-FRTCVR
   Page 1: QR code matches exclusion criteria - skipping red pixel removal
   ```
4. Verify correct behavior
5. Adjust if needed

## Tips and Best Practices

### 1. Measure Carefully
- Use ruler on actual printed page
- Measure from page edges to QR code edges
- Add small margin (0.1-0.2") for tolerance

### 2. Test Thoroughly
- Test with sample documents before production
- Verify QR detection in console output
- Check both matching and non-matching cases

### 3. Use Specific Patterns
- Good: `*-FRTCVR` (specific suffix)
- Bad: `*CVR` (too generic)

### 4. Document Your Scheme
- Keep list of QR codes and their meanings
- Document which patterns exclude red removal
- Share with team

### 5. Consider Performance
- Smaller regions = faster scanning
- Only enable QR scanning when needed
- Disable if all documents need same treatment

### 6. Backup Configuration
- appsettings.json is updated directly
- Keep backup of working configuration
- Version control recommended

## Troubleshooting

### Settings Not Saving
**Problem:** Changes not persisted after restart

**Check:**
1. Verify clicking "Save" button (not X)
2. Check appsettings.json file permissions
3. Verify file not locked by another process
4. Look for error messages

### QR Not Detected
**Problem:** Console shows "No QR code detected"

**Solutions:**
1. Verify "Enable QR Scanning" is checked
2. Check region coordinates match QR location
3. Increase region size slightly
4. Verify image quality/DPI
5. Test QR code with phone camera

### Wrong Pattern Matching
**Problem:** Exclusion not working as expected

**Solutions:**
1. Check pattern syntax (use wildcards correctly)
2. Verify case doesn't matter (already case-insensitive)
3. Test pattern in isolation
4. Check for typos in pattern
5. Review console output for actual QR values

### Validation Errors
**Problem:** Can't save settings due to validation

**Solutions:**
1. Verify all numeric fields have valid values
2. Check no negative numbers (except allowed)
3. Ensure decimal format correct (use period, not comma)
4. Clear and re-enter problematic field

### Settings Window Too Small
**Problem:** Can't see all controls

**Solutions:**
- Window is set to 650 height, 550 width
- Should show all controls with scrolling if needed
- Resize window if needed (draggable borders)

## Technical Details

### File Updated
Settings are saved directly to:
```
TestBookletProcessor.WPF\appsettings.json
```

### JSON Structure
```json
{
  "BookletProcessor": {
    "QrScanner": {
      "EnableQrScanning": true,
      "QrRegionXInches": 7.0,
      "QrRegionYInches": 9.5,
      "QrRegionWidthInches": 1.5,
      "QrRegionHeightInches": 1.5,
      "QrValuesExcludingRedRemoval": [
        "APT2*-0*",
        "*CVR",
        "CLEAN"
      ]
    }
  }
}
```

### Automatic Reload
When you save settings:
1. Settings window writes to appsettings.json
2. Returns `DialogResult = true`
3. MainWindow detects dialog result
4. Reloads configuration from file
5. Recreates BookletProcessorService with new settings
6. New settings take effect immediately

**No restart required!**

### Field Mapping

| UI Field | JSON Key | Type | Default |
|----------|----------|------|---------|
| Enable QR Scanning | `EnableQrScanning` | bool | false |
| QR Region X | `QrRegionXInches` | double | 6.5 |
| QR Region Y | `QrRegionYInches` | double | 9.0 |
| QR Region Width | `QrRegionWidthInches` | double | 2.0 |
| QR Region Height | `QrRegionHeightInches` | double | 2.0 |
| QR Exclusion Patterns | `QrValuesExcludingRedRemoval` | array | ["*-FRTCVR", "CLEAN"] |

## Window Layout

```
???????????????????????????????????????????????
? Settings                              [_][?][X]?
???????????????????????????????????????????????
?  Default Input Folder:  [________] [Browse] ?
?  Default Template Folder: [______] [Browse] ?
?  Default Output Folder: [________] [Browse] ?
?  Enable Red Pixel Remover: [?]              ?
?  Red Pixel Threshold (0-255): [90]          ?
?                                              ?
?  ?????????????????????????????????????????  ?
?                                              ?
?  QR Scanner Settings                         ?
?                                              ?
?  Enable QR Scanning: [?]                    ?
?  QR Region X (inches): [7.0]                ?
?  QR Region Y (inches): [9.5]                ?
?  QR Region Width (inches): [1.5]            ?
?  QR Region Height (inches): [1.5]           ?
?  QR Exclusion Patterns:                      ?
?  ?????????????????????????????????????????? ?
?  ? APT2*-0*, *CVR, CLEAN                  ? ?
?  ?                                        ? ?
?  ?????????????????????????????????????????? ?
?                                              ?
?                            [Save] [Cancel]   ?
???????????????????????????????????????????????
```

## Related Documentation

- **QR_WILDCARD_PATTERNS.md** - Wildcard pattern syntax and examples
- **QR_INCH_BASED_CONFIGURATION.md** - Inch-based configuration details
- **QR_EXCLUSION_MODEL_GUIDE.md** - Exclusion logic explanation

## Summary

The SettingsWindow now provides:
- ? Full QR scanner configuration in UI
- ? No need to manually edit JSON files
- ? Input validation prevents errors
- ? Immediate effect (no restart needed)
- ? Inch-based coordinates (DPI independent)
- ? Wildcard pattern support
- ? Intuitive interface with tooltips
- ? Multi-line pattern entry

This makes QR scanner configuration accessible to all users, not just those comfortable editing configuration files!
