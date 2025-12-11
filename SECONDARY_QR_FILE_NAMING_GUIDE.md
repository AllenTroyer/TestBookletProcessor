# Secondary QR File Naming Feature - User Documentation

## Overview
The Secondary QR File Naming feature enables dynamic renaming of output files based on a second QR code scanned from a specific region on designated pages. This allows files to be automatically renamed based on information contained in the documents themselves.

## Use Case
When processing scanned sheets that contain a page with QR code "CHECKLISTQR-01", the system can scan a secondary region on that page to extract naming information (e.g., school name) and automatically rename the output file accordingly.

**Example:**
- Input file: `SchoolCityState_001.pdf`
- Page 1 has primary QR: `CHECKLISTQR-01`
- Secondary region contains QR: `Lincoln_Elementary:District_5`
- Output file: `Lincoln_Elementary_001_aligned.pdf` ?

## How It Works

### Processing Flow
```
1. Scan each page for primary QR code
2. If primary QR = "CHECKLISTQR-01":
   - Scan secondary region (0", 0.75", 2" × 1")
   - Store QR value found
3. Continue processing all pages normally
4. After merging:
   - Extract text before colon (:) from secondary QR
   - Replace "SchoolCityState" in filename with extracted text
   - Rename output file
```

### Key Behaviors
- **First Match Wins**: If multiple pages have the trigger QR, only the first secondary QR found is used
- **Colon Separator**: Text before the colon (`:`) is extracted for the filename
- **Pattern Replacement**: "SchoolCityState" at the start of the filename is replaced
- **Fallback**: If pattern not found, extracted value is prepended to filename
- **Graceful Degradation**: If no secondary QR found, original filename is used

## Configuration

### appsettings.json Location
Both configuration files contain the same settings:
- `TestBookletProcessor.WPF\appsettings.json`
- `TestBookletProcessor.Console\appsettings.json`

### Configuration Structure
```json
{
  "BookletProcessor": {
    "ScannedSheets": {
      "SecondaryQrScan": {
        "TriggerQrCode": "CHECKLISTQR-01",
        "RegionXInches": 0.0,
        "RegionYInches": 0.75,
        "RegionWidthInches": 2.0,
        "RegionHeightInches": 1.0,
        "FileNameReplacementPattern": "SchoolCityState"
      }
    }
  }
}
```

### Configuration Fields

| Field | Type | Description | Default | Example |
|-------|------|-------------|---------|---------|
| `TriggerQrCode` | string | Primary QR code that triggers secondary scan | `"CHECKLISTQR-01"` | `"HEADER-PAGE"` |
| `RegionXInches` | double | X coordinate of scan region (inches) | `0.0` | `1.0` |
| `RegionYInches` | double | Y coordinate of scan region (inches) | `0.75` | `0.5` |
| `RegionWidthInches` | double | Width of scan region (inches) | `2.0` | `3.0` |
| `RegionHeightInches` | double | Height of scan region (inches) | `1.0` | `1.5` |
| `FileNameReplacementPattern` | string | Pattern to replace in filename | `"SchoolCityState"` | `"DefaultName"` |

### Region Coordinates

```
Page Layout (8.5" × 11")
???????????????????????????????????
? (0, 0)                          ?
?   ?                             ?
?   ????????????????              ?
?   ? Secondary QR ?  (0.75" down)?
?   ? Region:      ?              ?
?   ? 2" × 1"      ?              ?
?   ????????????????              ?
?                                 ?
?                                 ?
?                                 ?
?                     Primary QR  ?
?                     (lower right)?
???????????????????????????????????
```

**Coordinate System:**
- Origin: Top-left corner (0, 0)
- Units: Inches
- Direction: X increases right, Y increases down

## QR Code Format

### Secondary QR Value Format
The secondary QR code value **must include a colon** (`:`) separator:

**Format**: `ExtractedValue:AdditionalData`

**Examples:**
- `Lincoln_Elementary:District_5`
  - Extracted: `Lincoln_Elementary`
  - Used in filename
  
- `Washington_High:Grade_12`
  - Extracted: `Washington_High`
  - Used in filename

- `School_Name:Location:Extra`
  - Extracted: `School_Name`
  - Only first portion before colon is used

### Filename Sanitization
Extracted values are automatically sanitized for safe filename use:
- Invalid filename characters removed (`< > : " / \ | ? *`)
- Spaces replaced with underscores
- Length limited to 50 characters
- Non-alphanumeric characters (except `-` and `_`) removed

## Filename Transformation Examples

### Example 1: Pattern Found at Start
**Input:**
- Filename: `SchoolCityState_001_scanned.pdf`
- Secondary QR: `Lincoln_Elementary:District_5`

**Process:**
1. Extract: `Lincoln_Elementary`
2. Find pattern: `SchoolCityState` at start
3. Replace: `SchoolCityState` ? `Lincoln_Elementary`

**Output:** `Lincoln_Elementary_001_scanned_aligned.pdf`

### Example 2: Pattern Not Found
**Input:**
- Filename: `Custom_Document_001.pdf`
- Secondary QR: `Washington_High:Grade_12`

**Process:**
1. Extract: `Washington_High`
2. Pattern `SchoolCityState` not found
3. Prepend extracted value

**Output:** `Washington_High_Custom_Document_001_aligned.pdf`

### Example 3: Sanitization Applied
**Input:**
- Filename: `SchoolCityState_scan.pdf`
- Secondary QR: `School/Name:Extra`

**Process:**
1. Extract: `School/Name`
2. Sanitize: `/` removed ? `School_Name`
3. Replace pattern

**Output:** `School_Name_scan_aligned.pdf`

### Example 4: No Secondary QR Found
**Input:**
- Filename: `SchoolCityState_test.pdf`
- Secondary QR: Not found or not readable

**Process:**
1. No extraction possible
2. Use original filename

**Output:** `SchoolCityState_test_aligned.pdf`

## Console Output

### Startup (Configuration Loaded)
```
=== Booklet Processing Test ===
...
Secondary QR scan configured:
  Trigger QR: CHECKLISTQR-01
  Region: (0", 0.75") 2" × 1"
  Replacement pattern: SchoolCityState
```

### During Processing (Trigger QR Found)
```
--- Processing Page 1/10 ---
  QR Code: CHECKLISTQR-01
  ? Trigger QR detected, scanning secondary region...
  ? Secondary QR found: Lincoln_Elementary:District_5
  ? Secondary QR captured for file naming: Lincoln_Elementary:District_5
  ? Mapped to template page 0
  ? Aligning to template
```

### During Processing (Other QR Code)
```
--- Processing Page 2/10 ---
  QR Code: RAWFORMQR-01
  ? Mapped to template page 2
  ? Aligning to template
```

### After Merging (File Renamed)
```
Merging 10 processed pages...

  ? File Naming:
    Original: SchoolCityState_001_scanned_aligned.pdf
    New: Lincoln_Elementary_001_scanned_aligned.pdf
    Extracted: 'Lincoln_Elementary' from 'Lincoln_Elementary:District_5'
  ? File renamed to: Lincoln_Elementary_001_scanned_aligned.pdf

? Processing complete!
  Output: C:\Output\Lincoln_Elementary_001_scanned_aligned.pdf
  Pages: 10
  Time: 45.23s
```

### No Secondary QR Found
```
--- Processing Page 1/10 ---
  QR Code: CHECKLISTQR-01
  ? Trigger QR detected, scanning secondary region...
  ? No secondary QR found in region
  ? Mapped to template page 0

...

? Processing complete!
  Output: C:\Output\SchoolCityState_001_scanned_aligned.pdf
```

## Troubleshooting

### Problem: File Not Renamed

**Symptoms:**
- Output file has original "SchoolCityState" name
- Secondary QR should have been found

**Check:**
1. Console shows "Trigger QR detected"?
   - NO ? Check primary QR is exactly "CHECKLISTQR-01"
   - YES ? Continue

2. Console shows "Secondary QR found"?
   - NO ? Check QR is in correct region (0, 0.75, 2×1 inches)
   - YES ? Continue

3. Console shows "Secondary QR captured for file naming"?
   - NO ? Multiple pages with trigger QR, first one didn't have secondary
   - YES ? Check extracted value

4. Console shows "File renamed to"?
   - NO ? Check extracted value is not empty
   - YES ? Check output folder

**Solutions:**
- Verify secondary QR region coordinates match actual QR location
- Ensure secondary QR code is readable (not damaged/blurry)
- Check QR code contains colon (`:`) separator
- Verify portion before colon is not empty

### Problem: Wrong Name Extracted

**Symptoms:**
- File renamed but with wrong/unexpected value

**Check:**
1. Console output shows extracted value
2. Verify QR code content is correct
3. Check portion before colon is what you expect

**Example:**
- QR: `School_Name:Extra:Data`
- Extracted: `School_Name` ? (correct - first portion only)

### Problem: Invalid Filename Characters

**Symptoms:**
- Extracted value contains `/`, `\`, `:`, etc.
- File renamed but characters changed

**Behavior:**
- System automatically sanitizes invalid characters
- Console shows both original and sanitized value

**Example:**
```
  ? File Naming:
    Original: SchoolCityState_scan_aligned.pdf
    New: School_Name_scan_aligned.pdf
    Extracted: 'School_Name' from 'School/Name:Data'
```

Note: `/` was automatically sanitized to `_`

### Problem: Secondary QR Scan Error

**Symptoms:**
- Console shows "? Secondary QR scan error"

**Possible Causes:**
- Image file corrupted
- QR scanner initialization failed
- Region coordinates out of bounds

**Solutions:**
- Check image file is valid
- Verify QR scanning is enabled in configuration
- Check region coordinates are within page bounds
- Reduce region size if at edge of page

## Best Practices

### 1. QR Code Placement
- Place secondary QR in consistent location across all documents
- Ensure QR is not near page edge (leave 0.25" margin)
- Use high contrast (black QR on white background)
- Make QR large enough for reliable scanning (minimum 1" × 1")

### 2. QR Code Content
- Always include colon (`:`) separator
- Keep extracted portion concise (under 50 characters)
- Use alphanumeric characters and underscores
- Avoid special characters (`/`, `\`, `<`, `>`, etc.)
- Test QR codes before production use

**Good Examples:**
```
School_Name:Extra
Lincoln_Elementary:District_5
Washington_HS:Grade_12
Institution_123:Location_A
```

**Bad Examples:**
```
School Name                    (no colon separator)
School/Name:Data               (invalid character /)
Very_Long_School_Name_That_Exceeds_The_Maximum_Length_Limit:Data  (too long)
:SchoolName                    (empty before colon)
```

### 3. Testing
- Test with sample documents before production
- Verify QR codes scan correctly
- Check filename transformation is as expected
- Test with various QR code values
- Test missing QR code scenario

### 4. File Naming Convention
- Use consistent input file naming
- Include scanner-generated counter at end
- Pattern should be predictable location (start of filename)

**Example Input Files:**
```
SchoolCityState_001.pdf
SchoolCityState_002.pdf
SchoolCityState_003.pdf
```

**Expected Output Files:**
```
Lincoln_Elementary_001_aligned.pdf
Washington_High_002_aligned.pdf
Jefferson_MS_003_aligned.pdf
```

## Technical Details

### Performance Impact
- Secondary QR scan: ~50-100ms per page (only on trigger pages)
- Filename operation: <1ms
- Overall impact: Negligible (<1% of total processing time)

### Scope
- **Scanned Sheets Only**: Feature only works in scanned sheet processing mode
- **Not for Booklets**: Booklet mode does not support this feature
- **Sequential Processing**: Pages processed one at a time

### Limitations
1. **First Match Only**: Only first found secondary QR value is used
2. **Single Pattern**: Only one replacement pattern per configuration
3. **Static Configuration**: Region and pattern defined at startup
4. **Colon Required**: Secondary QR must contain colon separator
5. **Start of Filename**: Pattern replacement works best when pattern is at filename start

## Advanced Configuration

### Different Trigger QR
To use a different trigger QR code:

```json
{
  "SecondaryQrScan": {
    "TriggerQrCode": "HEADER-PAGE",
    ...
  }
}
```

### Different Scan Region
To scan a different location:

```json
{
  "SecondaryQrScan": {
    "RegionXInches": 1.0,
    "RegionYInches": 0.5,
    "RegionWidthInches": 3.0,
    "RegionHeightInches": 1.5,
    ...
  }
}
```

### Different Replacement Pattern
To replace a different pattern in filename:

```json
{
  "SecondaryQrScan": {
    "FileNameReplacementPattern": "DefaultName",
    ...
  }
}
```

### Disable Feature
To temporarily disable the feature:

Remove or comment out the entire `SecondaryQrScan` section:

```json
{
  "ScannedSheets": {
    "TemplateName": "Template_ScannedSheets.pdf",
    "QrToPageMapping": { ... }
    // "SecondaryQrScan": { ... }  ? Commented out or removed
  }
}
```

## Support

### Log Files
Check console output for detailed processing information:
- Configuration loaded at startup
- Trigger QR detection
- Secondary QR scan results
- Filename transformation details
- Error messages

### Common Issues
1. **File not renamed** ? Check console for secondary QR scan messages
2. **Wrong value extracted** ? Verify QR code content and colon placement
3. **Scan error** ? Check region coordinates and QR code quality
4. **Empty extracted value** ? Ensure text exists before colon

### Getting Help
When reporting issues, include:
- Console output (full processing log)
- Input filename
- Expected output filename
- Secondary QR code value
- Configuration section from appsettings.json

## Summary

? **Automatic file renaming** based on document content  
? **Flexible configuration** for region and pattern  
? **Graceful fallback** when QR not found  
? **Clear logging** of all operations  
? **Automatic sanitization** of filenames  
? **No manual intervention** required

This feature streamlines document processing workflows by automatically incorporating document-specific information into output filenames, eliminating manual renaming steps and reducing errors.
