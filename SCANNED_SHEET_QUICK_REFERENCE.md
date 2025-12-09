# Scanned Sheet Workflow - Quick Reference

## Quick Start

### 1. Enable Scanned Sheet Mode
Edit `appsettings.json`:
```json
{
  "BookletProcessor": {
    "ScannedSheets": {
      "TemplateName": "Template_ScannedSheets.pdf",
      "QrToPageMapping": {
        "FORM-A-*": 0,
        "FORM-B-*": 1,
        "FORM-C-*": 2
      }
    }
  }
}
```

### 2. Create Multi-Page Template
- Page 0: Form A template
- Page 1: Form B template
- Page 2: Form C template

### 3. Process
Select `Template_ScannedSheets.pdf` as template ? Mode auto-detected

## Key Differences

| | Booklet Mode | Scanned Sheet Mode |
|---|---|---|
| **Trigger** | Any other template | Template_ScannedSheets.pdf |
| **Processing** | By booklet | By individual page |
| **Template** | Same for all pages | Different per page via QR |
| **Speed** | Faster | Slower (sequential) |

## QR Mapping Syntax

```json
"QR_PATTERN": template_page_index
```

| Pattern | Meaning | Example |
|---------|---------|---------|
| `"FORM-A-*"` | Starts with "FORM-A-" | FORM-A-001, FORM-A-099 |
| `"*-ANSWER"` | Ends with "-ANSWER" | SHEET-1-ANSWER |
| `"CONSENT"` | Exact match | CONSENT only |

## Unmapped Pages
Pages with missing or unmapped QR codes:
- ? Kept in output (in sequence)
- ? Deskewed only
- ? No alignment
- ? No red removal

## Console Output

```
=== Scanned Sheet Processing Mode ===
Total pages to process: 10

--- Processing Page 1/10 ---
  QR Code: FORM-A-001
  ? Mapped to template page 0
  ? Aligning to template

--- Processing Page 2/10 ---
  ? No QR code found - page will remain unchanged
  
? Processing complete!
  Pages: 10
  Time: 42.31s
```

## Settings Window

**Path**: Settings ? Scanned Sheet Settings  
**Field**: Scanned Sheet Template Name  
**Value**: `Template_ScannedSheets.pdf`

**Note**: QR mappings edited in appsettings.json only

## Common Issues

| Problem | Solution |
|---------|----------|
| Mode not detected | Check template filename exactly matches |
| All pages unchanged | Verify QR scan region and patterns |
| Wrong template used | Check page indices (zero-based!) |
| Processing error | Check template has enough pages |

## Example Configuration

**3 Form Types + Answer Sheet:**
```json
{
  "ScannedSheets": {
    "TemplateName": "Template_ScannedSheets.pdf",
    "QrToPageMapping": {
      "FORM-A-*": 0,
      "FORM-B-*": 1,
      "FORM-C-*": 2,
      "ANSWER-*": 3
    }
  }
}
```

**Template PDF:**
- Page 0: Form A
- Page 1: Form B
- Page 2: Form C
- Page 3: Answer Sheet

## Testing Checklist

- [ ] Template created with all form types
- [ ] TemplateName set correctly
- [ ] QR patterns mapped to correct pages
- [ ] QR scan region configured
- [ ] Test with 5-10 sample pages
- [ ] Verify each QR pattern works
- [ ] Test unmapped QR handling
- [ ] Check alignment quality

## Performance Tips

1. Use DPI 200 instead of 300 if quality allows
2. Process in batches (50 pages max)
3. Disable red removal for clean forms
4. Use exact QR matches when possible

## File Locations

- **Config**: `TestBookletProcessor.WPF\appsettings.json`
- **Template**: `C:\TestBooklets\Templates\Template_ScannedSheets.pdf`
- **Guide**: `SCANNED_SHEET_WORKFLOW_GUIDE.md` (full documentation)

## Quick Commands

**Edit Config:**
```powershell
notepad TestBookletProcessor.WPF\appsettings.json
```

**Test in Console:**
```csharp
string templatePdf = @"C:\TestBooklets\Templates\Template_ScannedSheets.pdf";
```

## Support

For detailed information, see `SCANNED_SHEET_WORKFLOW_GUIDE.md`
