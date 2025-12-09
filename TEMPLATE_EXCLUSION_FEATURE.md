# Template Exclusion Feature - Documentation

## Overview
The Template Exclusion feature allows you to specify template names (with wildcard patterns) that should skip both QR code scanning and red pixel removal during processing. This is useful for templates that are blank forms, samples, or master templates that don't need these processing steps.

## Purpose

**Why Exclude Templates?**
- **Blank Templates**: Forms without student data don't need QR scanning or red removal
- **Sample Templates**: Example documents used for reference
- **Master Templates**: Clean originals used for alignment only
- **Performance**: Skip unnecessary processing for specific template types

## Configuration

### appsettings.json

Add `TemplateExclusionPatterns` array to the `BookletProcessor` section:

```json
{
  "BookletProcessor": {
    "TemplateExclusionPatterns": [ "*TEMPLATE*", "*BLANK*", "*SAMPLE*" ]
  }
}
```

### Pattern Matching

Templates are matched against patterns using **wildcard matching**:
- `*` matches one or more characters
- Matching is **case-insensitive**
- Pattern must match the **template filename without extension**

### Example Patterns

| Pattern | Matches | Examples |
|---------|---------|----------|
| `*TEMPLATE*` | Any filename containing "TEMPLATE" | `APT24-TEMPLATE.pdf`, `BLANK_TEMPLATE.pdf` |
| `*BLANK*` | Any filename containing "BLANK" | `BLANK-FORM.pdf`, `APT24-BLANK.pdf` |
| `*SAMPLE*` | Any filename containing "SAMPLE" | `SAMPLE-TEST.pdf`, `APT24-SAMPLE.pdf` |
| `MASTER-*` | Filenames starting with "MASTER-" | `MASTER-APT24.pdf`, `MASTER-TEST.pdf` |
| `*-ORIG` | Filenames ending with "-ORIG" | `APT24A-ORIG.pdf`, `TEST-ORIG.pdf` |

## Settings Window

### Location
The template exclusion settings are located at the bottom of the Settings window, below all QR scanner settings.

### Field Details

**Label**: "Templates to Exclude from QR Code Scanning and Red Removal"

**Input Type**: Multi-line text box (60px height, scrollable)

**Format**: Comma-separated patterns (can also use semicolons or line breaks)

**Tooltip**: 
```
Enter template name patterns separated by commas. 
Supports wildcards (*). Example: *TEMPLATE*, *BLANK*, *SAMPLE*
Templates matching these patterns will skip both QR scanning and red pixel removal.
```

### Entry Formats

All of these formats are valid:

**Comma-separated:**
```
*TEMPLATE*, *BLANK*, *SAMPLE*
```

**Semicolon-separated:**
```
*TEMPLATE*; *BLANK*; *SAMPLE*
```

**Line-separated:**
```
*TEMPLATE*
*BLANK*
*SAMPLE*
```

**Mixed:**
```
*TEMPLATE*, *BLANK*
*SAMPLE*
```

## How It Works

### Processing Flow

1. **Template Check**: When `ProcessBookletAsync` is called, the system extracts the template filename (without extension)

2. **Pattern Matching**: The template name is checked against all exclusion patterns using wildcard matching

3. **Exclusion Applied**: If the template matches any pattern:
   - **QR Code Scanning**: Skipped entirely
   - **Red Pixel Removal**: Skipped entirely
   - **Console Message**: "Template '{name}' matches exclusion pattern - skipping QR scanning and red pixel removal"

4. **Normal Processing**: If template doesn't match:
   - QR scanning proceeds (if enabled)
   - Red pixel removal proceeds based on QR result or global setting

### Code Implementation

**In BookletProcessorService.cs:**

```csharp
public async Task ProcessBookletAsync(string templatePdf, string inputPdf, ...)
{
    // Extract template filename
    var templateFileName = Path.GetFileNameWithoutExtension(templatePdf);
    
    // Check if excluded
    var isTemplateExcluded = _templateExclusionPatterns.Any(pattern =>
        MatchesWildcard(templateFileName, pattern, ignoreCase: true));
    
    if (isTemplateExcluded)
    {
        Console.WriteLine($"Template '{templateFileName}' matches exclusion pattern - skipping QR scanning and red pixel removal");
    }
    
    // Skip QR scanning if template excluded
    if (_enableQrScanning && _qrScanner != null && !isTemplateExcluded)
    {
        // QR scanning code...
    }
    
    // Skip red removal if template excluded
    var shouldApplyRedRemoval = _redPixelRemover != null && !isTemplateExcluded;
}
```

## Use Cases

### Use Case 1: Blank Form Templates

**Scenario**: You have blank templates that are used for creating new tests but don't contain student responses.

**Setup:**
```json
{
  "TemplateExclusionPatterns": [ "*BLANK*", "*-TEMPLATE" ]
}
```

**Template Files:**
- `APT24A-BLANK.pdf` ? **Excluded** (skips QR & red removal)
- `APT24A-TEMPLATE.pdf` ? **Excluded** (skips QR & red removal)
- `APT24A.pdf` ? **Processed normally**

### Use Case 2: Sample Documents

**Scenario**: Sample or example documents used for training don't need processing.

**Setup:**
```json
{
  "TemplateExclusionPatterns": [ "*SAMPLE*", "*EXAMPLE*", "*DEMO*" ]
}
```

**Template Files:**
- `APT24-SAMPLE.pdf` ? **Excluded**
- `APT24-EXAMPLE.pdf` ? **Excluded**
- `APT24-DEMO.pdf` ? **Excluded**
- `APT24.pdf` ? **Processed normally**

### Use Case 3: Master Templates

**Scenario**: Master or original templates used only for alignment reference.

**Setup:**
```json
{
  "TemplateExclusionPatterns": [ "MASTER-*", "*-ORIGINAL", "*-MASTER" ]
}
```

**Template Files:**
- `MASTER-APT24A.pdf` ? **Excluded**
- `APT24A-ORIGINAL.pdf` ? **Excluded**
- `APT24A-MASTER.pdf` ? **Excluded**
- `APT24A.pdf` ? **Processed normally**

### Use Case 4: Version-Specific Exclusions

**Scenario**: Only process certain versions of templates.

**Setup:**
```json
{
  "TemplateExclusionPatterns": [ "*-V1", "*-V2", "*-DRAFT" ]
}
```

**Template Files:**
- `APT24A-V1.pdf` ? **Excluded** (old version)
- `APT24A-V2.pdf` ? **Excluded** (old version)
- `APT24A-DRAFT.pdf` ? **Excluded** (draft version)
- `APT24A-V3.pdf` ? **Processed normally** (current version)

## Console Output

### Template Excluded

When a template matches an exclusion pattern:

```
Template 'APT24A-BLANK' matches exclusion pattern - skipping QR scanning and red pixel removal
```

### Template Not Excluded

Normal processing messages appear:

```
Page 1: QR code detected: APT24A-001
Page 1: QR code does not match exclusion criteria - applying red pixel removal
```

### Startup Configuration Display

Console app shows loaded patterns at startup:

```
Template exclusion patterns: *TEMPLATE*, *BLANK*, *SAMPLE*
```

## Benefits

### 1. Performance Improvement
- Skip expensive QR scanning for known templates
- Skip red pixel removal for clean templates
- Faster processing for template-heavy batches

### 2. Processing Accuracy
- Avoid false QR detections on blank templates
- Prevent unnecessary modifications to master templates
- Keep original templates unchanged

### 3. Flexibility
- Easy to add new exclusion patterns
- Wildcard support for pattern families
- No code changes needed

### 4. Clearer Intent
- Explicitly mark which templates are excluded
- Self-documenting configuration
- Easy to understand what will be skipped

## Pattern Best Practices

### 1. Be Specific
```
? Good: *BLANK-TEMPLATE*
? Bad: *BLANK*  (too broad, might match unintended files)
```

### 2. Use Consistent Naming
Structure your template filenames to work well with patterns:
```
APT24A.pdf           (standard template - processed)
APT24A-BLANK.pdf     (blank form - excluded)
APT24A-SAMPLE.pdf    (sample - excluded)
APT24A-MASTER.pdf    (master - excluded)
```

### 3. Document Your Patterns
Keep a list of patterns and their purposes:
```json
{
  "TemplateExclusionPatterns": [
    "*BLANK*",      // Blank forms without student data
    "*SAMPLE*",     // Sample/example documents
    "*TEMPLATE*",   // Master templates for reference
    "*-ORIG"        // Original unmodified templates
  ]
}
```

### 4. Test Patterns
Before production use:
1. Test with sample template filenames
2. Verify correct templates are excluded
3. Check console output for confirmation

### 5. Keep List Minimal
Only exclude templates that truly don't need processing:
```
? Good: 3-5 specific patterns
? Bad: 20+ patterns (might indicate poor naming scheme)
```

## Interaction with Other Features

### Relationship to QR Code Exclusions

**Template Exclusions** and **QR Code Exclusions** work differently:

| Feature | When Applied | Effect |
|---------|-------------|--------|
| **Template Exclusions** | Before QR scanning | Skips QR scan AND red removal |
| **QR Code Exclusions** | After QR detected | Skips red removal only |

**Example:**
```
Template: APT24A-BLANK.pdf
Template Exclusion: *BLANK*
Result: No QR scan attempted, no red removal

Template: APT24A.pdf
QR Code Found: APT24A-001-CLEAN
QR Exclusion: *-CLEAN
Result: QR scan performed, red removal skipped
```

### Priority Order

1. **Template Exclusion Check** (highest priority)
   - If template excluded ? Skip everything

2. **Global Red Removal Setting**
   - If red removal disabled ? Skip red removal

3. **QR Code Check** (if enabled and not excluded)
   - Scan QR code

4. **QR Code Exclusion Check**
   - If QR matches exclusion ? Skip red removal
   - If QR doesn't match ? Apply red removal

## Troubleshooting

### Template Not Being Excluded

**Problem**: Template should be excluded but is still processed

**Check:**
1. Verify template filename exactly (case doesn't matter, but spelling does)
2. Check pattern includes proper wildcards
3. Pattern must match filename WITHOUT extension
4. Review console output for template name used

**Example:**
```
Template File: APT24A-BLANK.pdf
Filename Used: APT24A-BLANK (no extension)
Pattern: *BLANK* ? (matches)
Pattern: *BLANK.pdf* ? (doesn't match - includes extension)
```

### Wrong Templates Excluded

**Problem**: Templates being excluded that shouldn't be

**Check:**
1. Pattern might be too broad
2. Use more specific patterns
3. Check for substring matches

**Example:**
```
Pattern: *BLANK*
Unintended Match: APT24A-REBLANK.pdf (contains "BLANK")
Better Pattern: *-BLANK or BLANK-*
```

### Pattern Not Working

**Problem**: Pattern doesn't seem to work at all

**Check:**
1. Verify pattern syntax (wildcards correct)
2. Check for typos in appsettings.json
3. Verify Settings window saved correctly
4. Restart application after config changes

### Console Shows Unexpected Behavior

**Problem**: Console output doesn't match expectations

**Solution:**
1. Check console message when template is loaded
2. Look for: "Template '{name}' matches exclusion pattern"
3. Verify filename shown matches your expectations
4. Test pattern with simple examples first

## Migration Guide

If you're adding this feature to existing configurations:

### Step 1: Identify Templates to Exclude

Review your template files and identify:
- Blank forms
- Master templates
- Sample documents
- Any templates that don't need QR/red removal

### Step 2: Create Patterns

Based on naming convention:
```
Filenames:
- APT24A.pdf (normal - keep processing)
- APT24A-BLANK.pdf (blank - exclude)
- APT24A-SAMPLE.pdf (sample - exclude)

Patterns: [ "*-BLANK", "*-SAMPLE" ]
```

### Step 3: Update Configuration

Add to appsettings.json:
```json
{
  "BookletProcessor": {
    "TemplateExclusionPatterns": [ "*-BLANK", "*-SAMPLE" ]
  }
}
```

### Step 4: Update via Settings Window (Optional)

1. Open Settings window
2. Scroll to bottom
3. Enter patterns in "Templates to Exclude..." textbox
4. Click Save

### Step 5: Test

1. Process documents with both excluded and non-excluded templates
2. Check console output
3. Verify correct templates are skipped
4. Adjust patterns if needed

## Summary

**Template Exclusion** provides:
- ? Skip QR scanning for specific templates
- ? Skip red removal for specific templates
- ? Wildcard pattern support
- ? Easy configuration via Settings UI
- ? Performance optimization
- ? Flexible and powerful

**Default Patterns:**
- `*TEMPLATE*`
- `*BLANK*`
- `*SAMPLE*`

**Configure via:**
- appsettings.json
- Settings Window UI

**Effect:**
- Templates matching patterns skip both QR scanning and red pixel removal
- Non-matching templates process normally
- Clear console messages indicate when templates are excluded

This feature gives you fine-grained control over which templates receive full processing and which are used only for reference or alignment!
