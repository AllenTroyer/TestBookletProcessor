# Scanned Sheet Alignment Workflow - Complete Guide

## Overview
The Scanned Sheet Alignment workflow is a new processing mode that handles individual scanned pages where each page may be a different type of form. Unlike booklet mode (where all pages use the same template), scanned sheet mode aligns each page to its specific template page based on the page's QR code content.

## Key Concepts

### Booklet Mode vs Scanned Sheet Mode

| Feature | Booklet Mode | Scanned Sheet Mode |
|---------|--------------|-------------------|
| **Processing Unit** | Booklet (multiple pages) | Individual pages |
| **Template Usage** | Same template for all pages in booklet | Different template page per sheet |
| **Page Identification** | By position in booklet | By QR code content |
| **Use Case** | Test booklets with identical structure | Mixed forms, surveys, varied documents |
| **Mode Selection** | Default | Auto-detected by template name |

### When to Use Scanned Sheet Mode

? **Use Scanned Sheet Mode When:**
- You have mixed document types in one PDF
- Each page is a different form (e.g., survey responses, mixed assessments)
- Pages can be in any order
- Each page type needs alignment to a specific template
- QR codes identify the page type

? **Don't Use Scanned Sheet Mode When:**
- All pages are the same type (use booklet mode)
- Pages don't have QR codes
- Processing speed is critical (booklet mode is faster)

## Architecture

### Components

#### 1. **ScannedSheetConfig** (`TestBookletProcessor.Core/Models`)
Configuration model for scanned sheet settings.

```csharp
public class ScannedSheetConfig
{
    public string TemplateName { get; set; }
    public Dictionary<string, int> QrToPageMapping { get; set; }
}
```

#### 2. **IScannedSheetProcessor** (`TestBookletProcessor.Core/Interfaces`)
Interface defining the scanned sheet processing contract.

```csharp
public interface IScannedSheetProcessor
{
    Task<ProcessingResult> ProcessScannedSheetsAsync(...);
}
```

#### 3. **ScannedSheetProcessorService** (`TestBookletProcessor.Services`)
Implementation that processes pages individually based on QR codes.

**Key Features:**
- Sequential page processing
- QR code scanning on each page
- Template page matching via wildcard patterns
- Handles unmapped QR codes gracefully
- Same deskewing and red removal as booklet mode

#### 4. **BookletProcessorService** (Enhanced)
Now includes auto-detection and routing logic.

**Mode Detection:**
```csharp
private bool IsScannedSheetMode(string templatePdf)
{
    var templateFileName = Path.GetFileName(templatePdf);
    return templateFileName.Equals(_scannedSheetTemplateName, OrdinalIgnoreCase);
}
```

### Processing Flow

```
Input PDF
    ?
Check Template Name
    ?
    ?? Matches Scanned Sheet Template? ? YES ? Scanned Sheet Mode
    ?                                              ?
    ?                                      Split into Individual Pages
    ?                                              ?
    ?                                      For Each Page:
    ?                                          1. Convert to Image
    ?                                          2. Deskew
    ?                                          3. Scan QR Code
    ?                                          4. Match QR to Template Page
    ?                                          5. If Matched:
    ?                                             - Apply Red Removal (if needed)
    ?                                             - Align to Template
    ?                                          6. If Not Matched:
    ?                                             - Keep page unchanged
    ?                                          7. Convert to PDF
    ?                                              ?
    ?                                      Merge All Pages
    ?                                              ?
    ?                                      Output PDF
    ?
    ?? NO ? Booklet Mode (existing workflow)
```

## Configuration

### appsettings.json Structure

```json
{
  "BookletProcessor": {
    "ScannedSheets": {
      "TemplateName": "Template_ScannedSheets.pdf",
      "QrToPageMapping": {
        "FORM-A-*": 0,
        "FORM-B-*": 1,
        "FORM-C-*": 2,
        "ANSWER-SHEET-*": 3,
        "COVER-*": 4,
        "INSTRUCTIONS-*": 5,
        "CONSENT-*": 6,
        "DEMOGRAPHICS-*": 7,
        "ADDITIONAL-*": 8,
        "NOTES-*": 9
      }
    }
  }
}
```

### Configuration Fields

#### TemplateName
**Type:** String  
**Purpose:** Filename of template that triggers scanned sheet mode  
**Example:** `"Template_ScannedSheets.pdf"`

**How It Works:**
- When processing starts, system compares template filename
- If match found ? Scanned Sheet Mode
- If no match ? Booklet Mode

**Important:** This is the **filename only**, not the full path.

#### QrToPageMapping
**Type:** Dictionary<string, int>  
**Purpose:** Maps QR code patterns to template page indices  
**Format:** `"QR_PATTERN": page_index`

**Key Points:**
- Supports wildcard patterns (`*`)
- Page indices are zero-based (first page = 0)
- Patterns are case-insensitive
- First match wins (order matters)

**Example Mappings:**

| QR Pattern | Template Page | Description |
|------------|---------------|-------------|
| `"FORM-A-*"` | `0` | Any QR starting with "FORM-A-" ? Page 0 |
| `"FORM-B-*"` | `1` | Any QR starting with "FORM-B-" ? Page 1 |
| `"*-ANSWER"` | `3` | Any QR ending with "-ANSWER" ? Page 3 |
| `"CONSENT-FORM"` | `6` | Exact match ? Page 6 |

### QR Code Matching Logic

#### Pattern Matching Order
1. **Exact Match**: Check for exact QR code match
2. **Wildcard Patterns**: Try each pattern in order
3. **No Match**: Page remains unchanged

#### Wildcard Syntax
- `*` matches one or more characters
- Must match at least one character
- Case-insensitive

**Examples:**

| QR Code | Pattern | Matches? |
|---------|---------|----------|
| `FORM-A-001` | `FORM-A-*` | ? Yes |
| `FORM-A-` | `FORM-A-*` | ? No (nothing after *) |
| `form-a-001` | `FORM-A-*` | ? Yes (case-insensitive) |
| `TEST-FORM-A-001` | `FORM-A-*` | ? No (doesn't start with) |
| `FORM-A-001-TEST` | `FORM-A-*` | ? Yes (starts with) |

## Usage

### Setup

#### 1. Create Multi-Page Template PDF
Create a template PDF with one page for each form type.

**Example:**
```
Template_ScannedSheets.pdf:
- Page 0: Form A template
- Page 1: Form B template
- Page 2: Form C template
- Page 3: Answer Sheet template
- Page 4: Cover Page template
```

#### 2. Configure QR Code Patterns
Edit `appsettings.json`:

```json
{
  "ScannedSheets": {
    "TemplateName": "Template_ScannedSheets.pdf",
    "QrToPageMapping": {
      "FORM-A-*": 0,
      "FORM-B-*": 1,
      "FORM-C-*": 2,
      "ANSWER-*": 3,
      "COVER-*": 4
    }
  }
}
```

#### 3. Place Template in Template Folder
```
C:\TestBooklets\Templates\Template_ScannedSheets.pdf
```

#### 4. Process Documents
Use the template in WPF app or console app:
- **WPF**: Select `Template_ScannedSheets.pdf` as template
- **Console**: Set `templatePdf` variable to point to this template
- **Folder Monitor**: Configure folder with this template

### WPF Application

1. Open Test Booklet Processor
2. **Input PDF**: Select PDF with scanned sheets
3. **Template**: Browse to `Template_ScannedSheets.pdf`
4. Click **Process**

**What Happens:**
- System detects scanned sheet mode automatically
- Status shows "Processing Page X/Y"
- Each page aligned to its specific template
- Output PDF contains all processed pages in order

### Console Application

```csharp
string templatePdf = @"C:\TestBooklets\Templates\Template_ScannedSheets.pdf";
string inputPdf = @"C:\TestBooklets\Input\scanned_forms.pdf";
string outputPdf = @"C:\TestBooklets\Output\aligned_forms.pdf";
```

**Console Output:**
```
=== Scanned Sheet Processing Mode ===
Input: C:\TestBooklets\Input\scanned_forms.pdf
Template: C:\TestBooklets\Templates\Template_ScannedSheets.pdf
QR Mappings: 5 patterns

Splitting input PDF into pages...
Total pages to process: 10

--- Processing Page 1/10 ---
  QR Code: FORM-A-001
  ? Mapped to template page 0
  ? Applying red pixel removal
  ? Aligning to template

--- Processing Page 2/10 ---
  QR Code: FORM-B-012
  ? Mapped to template page 1
  ? Aligning to template

--- Processing Page 3/10 ---
  ? No QR code found - page will remain unchanged

...

? Processing complete!
  Output: C:\TestBooklets\Output\aligned_forms.pdf
  Pages: 10
  Time: 45.23s
```

### Settings Window

**Location:** Settings ? Scanned Sheet Settings

**Fields:**
- **Scanned Sheet Template Name**: Enter filename (e.g., `Template_ScannedSheets.pdf`)

**Note:** QR mappings must be edited in `appsettings.json` directly (too complex for UI editor).

## Handling Edge Cases

### Unmapped QR Codes

**Scenario:** QR code doesn't match any pattern

**Behavior:**
- Page kept in sequence
- No alignment performed
- No red removal performed
- Original (deskewed) page included in output

**Console Output:**
```
--- Processing Page 5/10 ---
  QR Code: UNKNOWN-FORM-123
  ? QR code not mapped - page will remain unchanged
```

**Use Case:** Mixed documents where some pages shouldn't be processed

### Missing QR Codes

**Scenario:** No QR code found on page

**Behavior:**
- Same as unmapped QR codes
- Page kept unchanged in output
- Processing continues to next page

**Console Output:**
```
--- Processing Page 7/10 ---
  ? No QR code found - page will remain unchanged
```

**Common Causes:**
- QR code outside scan region
- QR code too faded/damaged
- Page has no QR code
- Wrong scan region configuration

### QR Scan Errors

**Scenario:** Error during QR scanning

**Behavior:**
- Error logged to console
- Page kept unchanged
- Processing continues

**Console Output:**
```
--- Processing Page 3/10 ---
  ? QR scan error: Image format not supported - page will remain unchanged
```

## Performance Considerations

### Sequential Processing
- Pages processed one at a time
- Slower than booklet mode
- More reliable for varied content

### Optimization Tips
1. **Reduce DPI** if quality allows (200 vs 300)
2. **Disable red removal** for forms that don't need it
3. **Process smaller batches** rather than huge PDFs
4. **Use specific QR patterns** (exact matches faster than wildcards)

### Expected Processing Times

| Pages | DPI | Red Removal | Approx. Time |
|-------|-----|-------------|--------------|
| 10 | 200 | Disabled | ~15-20 sec |
| 10 | 300 | Enabled | ~30-40 sec |
| 50 | 200 | Disabled | ~1.5-2 min |
| 50 | 300 | Enabled | ~3-4 min |
| 100 | 300 | Enabled | ~6-8 min |

*Times vary based on hardware and image complexity*

## Examples

### Example 1: Survey Forms

**Scenario:** Mixed survey pages (demographics, questions, consent)

**Template Structure:**
```
Template_Survey.pdf:
- Page 0: Demographics form
- Page 1: Question Page 1
- Page 2: Question Page 2
- Page 3: Consent form
```

**Configuration:**
```json
{
  "ScannedSheets": {
    "TemplateName": "Template_Survey.pdf",
    "QrToPageMapping": {
      "DEMO-*": 0,
      "Q1-*": 1,
      "Q2-*": 2,
      "CONSENT-*": 3
    }
  }
}
```

**Input Pages:**
```
Page 1: QR = "DEMO-P001" ? Aligned to template page 0
Page 2: QR = "Q1-P001" ? Aligned to template page 1
Page 3: QR = "Q2-P001" ? Aligned to template page 2
Page 4: QR = "CONSENT-P001" ? Aligned to template page 3
Page 5: QR = "DEMO-P002" ? Aligned to template page 0
...
```

### Example 2: Test Assessment Bundle

**Scenario:** Multiple test forms in one PDF

**Template Structure:**
```
Template_Tests.pdf:
- Page 0: Test A (multiple choice)
- Page 1: Test B (essay)
- Page 2: Test C (matching)
- Page 3: Answer key
- Page 4: Cover sheet
```

**Configuration:**
```json
{
  "ScannedSheets": {
    "TemplateName": "Template_Tests.pdf",
    "QrToPageMapping": {
      "TEST-A-*": 0,
      "TEST-B-*": 1,
      "TEST-C-*": 2,
      "ANSWER-KEY-*": 3,
      "COVER-*": 4
    }
  }
}
```

### Example 3: Registration Packet

**Scenario:** Registration forms with varying content

**Template Structure:**
```
Template_Registration.pdf:
- Page 0: Personal info
- Page 1: Medical history
- Page 2: Emergency contact
- Page 3: Payment form
- Page 4: Signature page
```

**Configuration:**
```json
{
  "ScannedSheets": {
    "TemplateName": "Template_Registration.pdf",
    "QrToPageMapping": {
      "PERSONAL-*": 0,
      "MEDICAL-*": 1,
      "EMERGENCY-*": 2,
      "PAYMENT-*": 3,
      "SIGNATURE-*": 4
    }
  }
}
```

## Troubleshooting

### Problem: Mode Not Detected

**Symptoms:**
- Booklet mode runs instead of scanned sheet mode
- Error: "Template and input PDF must have the same number of pages"

**Solutions:**
1. Check template filename matches exactly: `Template_ScannedSheets.pdf`
2. Verify `TemplateName` in appsettings.json
3. Check template file exists in template folder
4. Restart application after config changes

### Problem: All Pages Remain Unchanged

**Symptoms:**
- Output PDF has all original pages
- Console shows "QR code not mapped" for all pages

**Solutions:**
1. Verify QR codes exist on pages
2. Check QR scan region configuration (same as booklet mode)
3. Test QR codes match configured patterns
4. Verify wildcard syntax (`*` placement)
5. Check QR region is in correct location

### Problem: Wrong Template Page Used

**Symptoms:**
- Page aligned to incorrect template
- Alignment looks wrong

**Solutions:**
1. Verify QR pattern mapping
2. Check template page indices (remember: zero-based!)
3. Review pattern matching order
4. Test with exact QR code match first
5. Check for overlapping patterns (first match wins)

### Problem: Some Pages Missing

**Symptoms:**
- Output PDF has fewer pages than input

**Solutions:**
- This shouldn't happen - unmapped pages should remain in output
- Check console output for errors
- Verify no exceptions during processing
- Check temp folder for intermediate files

## Best Practices

### 1. QR Code Design
- ? Use consistent naming scheme
- ? Include type identifier at start or end
- ? Use separators (hyphens, underscores)
- ? Test QR codes scan reliably
- ? Don't use complex patterns unnecessarily

**Good:**
```
FORM-A-001
FORM-A-002
FORM-B-001
ANSWER-SHEET-001
```

**Bad:**
```
001
FormA
test_page_1
RANDOM-XYZ
```

### 2. Template Organization
- ? Name template clearly (e.g., `Template_ScannedSheets.pdf`)
- ? Order template pages logically
- ? Document which page is which
- ? Keep template file version controlled
- ? Don't reuse template names for different purposes

### 3. Configuration Management
- ? Document QR patterns and meanings
- ? Keep backup of working configuration
- ? Version control appsettings.json
- ? Test configuration with sample data first
- ? Don't change patterns without testing

### 4. Testing
- ? Test with small sample first (5-10 pages)
- ? Verify each QR pattern works
- ? Test unmapped QR handling
- ? Check output alignment quality
- ? Don't process production data without testing

## Migration from Booklet Mode

### When to Migrate

Consider scanned sheet mode if:
- You're processing mixed documents types
- Pages can be in any order
- Each page needs different template
- QR codes already identify page types

### Migration Steps

1. **Analyze Current Setup**
   - How many different page types?
   - Do pages have QR codes?
   - Are QR codes consistent?

2. **Create Multi-Page Template**
   - Extract one example of each page type
   - Create single PDF with all templates
   - Number pages starting from 0

3. **Configure QR Mapping**
   - List all QR code patterns
   - Map to template page indices
   - Test patterns with sample QR codes

4. **Update Configuration**
   - Set `TemplateName`
   - Add `QrToPageMapping`
   - Save and test

5. **Test Thoroughly**
   - Process sample documents
   - Verify alignment quality
   - Check unmapped page handling
   - Compare with booklet mode output

6. **Deploy**
   - Update production configuration
   - Train users on new template
   - Monitor for issues

## API Reference

### ScannedSheetProcessorService Constructor

```csharp
public ScannedSheetProcessorService(
    IPdfService pdfService,
    IDeskewer deskewer,
    IImageAligner aligner,
    IRedPixelRemoverService? redPixelRemover = null,
    byte redThreshold = 200,
    RegionQrScanner? qrScanner = null,
    bool enableQrScanning = true,
    double qrRegionXInches = 6.5,
    double qrRegionYInches = 9.0,
    double qrRegionWidthInches = 2.0,
    double qrRegionHeightInches = 2.0,
    int dpi = 300,
    List<string>? qrValuesExcludingRedRemoval = null)
```

### ProcessScannedSheetsAsync Method

```csharp
Task<ProcessingResult> ProcessScannedSheetsAsync(
    string inputPdf,
    string templatePdf,
    Dictionary<string, int> qrMapping,
    string outputFolder,
    string outputPdf,
    int dpi,
    Action<int, int>? progressCallback = null)
```

**Parameters:**
- `inputPdf`: Path to input PDF with scanned sheets
- `templatePdf`: Path to multi-page template PDF
- `qrMapping`: Dictionary of QR patterns to template page indices
- `outputFolder`: Working folder for temporary files
- `outputPdf`: Path for final output PDF
- `dpi`: DPI for image conversion
- `progressCallback`: Optional progress callback (current, total)

**Returns:** `ProcessingResult` with success status, output path, pages processed, and timing

## Summary

The Scanned Sheet Alignment workflow provides:

? **Flexibility** - Handle mixed document types in one PDF  
? **Automatic Detection** - No manual mode selection needed  
? **QR-Based Routing** - Each page aligned to correct template  
? **Graceful Handling** - Unmapped pages kept unchanged  
? **Sequential Processing** - Reliable, predictable behavior  
? **Same Quality** - Uses same deskewing/alignment as booklets  
? **Easy Configuration** - Simple JSON setup  

This workflow is ideal for surveys, mixed assessments, registration packets, and any scenario where individual pages need different template alignment based on their content type.
