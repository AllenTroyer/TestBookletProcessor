# Secondary QR Scan for File Naming - Implementation Plan

## Overview
Implement a feature that scans a secondary QR code region on pages with QR code "CHECKLISTQR-01" to dynamically rename the output file based on the scanned value.

## Requirements Analysis

### Current Behavior
- Input file: `SchoolCityState_input.pdf` or similar
- Output file: `SchoolCityState_input_aligned.pdf`
- Fixed naming pattern based on input filename

### New Behavior
- Detect pages with QR code "CHECKLISTQR-01"
- Scan secondary region (0", 0.75", 2" × 1") for another QR code
- Extract text before colon (`:`) from secondary QR value
- Replace "SchoolCityState" in output filename with extracted text
- Example: `SchoolCityState_input.pdf` ? `ExtractedValue_input_aligned.pdf`

## Architecture Analysis

### Current Processing Flow
```
Input PDF
    ?
Split into pages
    ?
For each page:
    - Scan primary QR (identifies page type)
    - Process based on page type
    ?
Merge pages ? Output: {inputName}_aligned.pdf
```

### New Processing Flow
```
Input PDF
    ?
Split into pages
    ?
secondaryQrValue = null
For each page:
    - Scan primary QR
    - If primary QR = "CHECKLISTQR-01":
        - Scan secondary region for QR
        - Store value in secondaryQrValue
    - Process page normally
    ?
Merge pages
    ?
Generate output filename:
    - If secondaryQrValue found:
        - Extract text before ':'
        - Replace "SchoolCityState" with extracted text
    - Else: Use original naming
    ?
Rename/Save output file
```

## Design Decisions

### 1. Scope: Scanned Sheets Only
**Decision**: Feature only applies to scanned sheet processing mode, not booklet mode.
**Reason**: QR code "CHECKLISTQR-01" is in scanned sheets context.

### 2. Single QR Value
**Decision**: Use first found secondary QR value, ignore subsequent ones.
**Reason**: Typically only one checklist page per document.
**Alternative**: Could track multiple values if needed.

### 3. Filename Pattern
**Decision**: Hardcode "SchoolCityState" as the replacement target.
**Reason**: Specific requirement, keeps implementation simple.
**Alternative**: Could make pattern configurable in appsettings.

### 4. Missing QR Handling
**Decision**: If no secondary QR found, use original filename.
**Reason**: Graceful degradation, existing behavior preserved.

### 5. Configuration Storage
**Decision**: Store secondary scan region in appsettings.json under ScannedSheets section.
**Reason**: Keeps related configuration together.

## Implementation Plan

### Phase 1: Configuration & Model

#### Step 1.1: Add Configuration to appsettings.json
Add to both WPF and Console appsettings.json:

```json
{
  "BookletProcessor": {
    "ScannedSheets": {
      "TemplateName": "Template_ScannedSheets.pdf",
      "QrToPageMapping": { ... },
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

#### Step 1.2: Create Configuration Model
Create `SecondaryQrScanConfig.cs` in `TestBookletProcessor.Core\Models`:

```csharp
public class SecondaryQrScanConfig
{
    public string TriggerQrCode { get; set; } = "CHECKLISTQR-01";
    public double RegionXInches { get; set; } = 0.0;
    public double RegionYInches { get; set; } = 0.75;
    public double RegionWidthInches { get; set; } = 2.0;
    public double RegionHeightInches { get; set; } = 1.0;
    public string FileNameReplacementPattern { get; set; } = "SchoolCityState";
    
    public (int X, int Y, int Width, int Height) ToPixelCoordinates(int dpi)
    {
        return (
            (int)(RegionXInches * dpi),
            (int)(RegionYInches * dpi),
            (int)(RegionWidthInches * dpi),
            (int)(RegionHeightInches * dpi)
        );
    }
}
```

### Phase 2: Service Layer Updates

#### Step 2.1: Update ScannedSheetProcessorService Constructor
Add fields and constructor parameters:

```csharp
private readonly SecondaryQrScanConfig? _secondaryQrScanConfig;

public ScannedSheetProcessorService(
    // ... existing parameters ...
    SecondaryQrScanConfig? secondaryQrScanConfig = null)
{
    // ... existing initialization ...
    _secondaryQrScanConfig = secondaryQrScanConfig;
}
```

#### Step 2.2: Add Secondary QR Scanning Logic
In `ProcessScannedSheetsAsync`, add tracking variable:

```csharp
public async Task<ProcessingResult> ProcessScannedSheetsAsync(...)
{
    var result = new ProcessingResult();
    var stopwatch = Stopwatch.StartNew();
    
    // NEW: Track secondary QR value for file naming
    string? secondaryQrValue = null;

    try
    {
        // ... existing code ...
        
        // Process each page individually
        for (int i = 0; i < inputPages.Count; i++)
        {
            var pageNum = i + 1;
            Console.WriteLine($"\n--- Processing Page {pageNum}/{totalPages} ---");
            progressCallback?.Invoke(pageNum, totalPages);

            var inputPage = inputPages[i];
            
            // NEW: Pass secondary QR config and capture result
            var (processedPage, scannedSecondaryQr) = await ProcessSinglePageAsync(
                inputPage,
                templatePages,
                qrMapping,
                workingFolder,
                pageNum,
                dpi);

            processedPages.Add(processedPage);
            
            // NEW: Store first found secondary QR value
            if (scannedSecondaryQr != null && secondaryQrValue == null)
            {
                secondaryQrValue = scannedSecondaryQr;
                Console.WriteLine($"  ? Secondary QR captured for file naming: {secondaryQrValue}");
            }
        }

        // ... merge pages ...
        
        // NEW: Apply dynamic file naming
        string finalOutputPath = outputPdf;
        if (secondaryQrValue != null && _secondaryQrScanConfig != null)
        {
            finalOutputPath = ApplyDynamicFileName(
                outputPdf, 
                secondaryQrValue, 
                _secondaryQrScanConfig.FileNameReplacementPattern);
            
            // Rename if needed
            if (finalOutputPath != outputPdf && File.Exists(outputPdf))
            {
                File.Move(outputPdf, finalOutputPath);
                Console.WriteLine($"  ? Renamed output: {Path.GetFileName(finalOutputPath)}");
            }
        }
        
        result.OutputPath = finalOutputPath;
        // ... rest of method ...
    }
    // ... catch/finally ...
}
```

#### Step 2.3: Update ProcessSinglePageAsync Signature
Change return type to tuple:

```csharp
private async Task<(string processedPagePdf, string? secondaryQrValue)> ProcessSinglePageAsync(
    string inputPagePdf,
    List<string> templatePages,
    Dictionary<string, int> qrMapping,
    string workingFolder,
    int pageNumber,
    int dpi)
{
    // ... existing code ...
    
    string? secondaryQrValue = null;
    
    // After deskewing and primary QR scan
    if (qrCode != null && 
        _secondaryQrScanConfig != null && 
        qrCode.Equals(_secondaryQrScanConfig.TriggerQrCode, StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"  ? Trigger QR detected, scanning secondary region...");
        secondaryQrValue = await ScanSecondaryQrRegion(deskewedImage, dpi);
        
        if (secondaryQrValue != null)
        {
            Console.WriteLine($"  ? Secondary QR found: {secondaryQrValue}");
        }
        else
        {
            Console.WriteLine($"  ? No secondary QR found in region");
        }
    }
    
    // ... rest of processing ...
    
    return (outputPdf, secondaryQrValue);
}
```

#### Step 2.4: Implement Helper Methods

**Secondary QR Scanning:**
```csharp
private async Task<string?> ScanSecondaryQrRegion(string imagePath, int dpi)
{
    if (_qrScanner == null || _secondaryQrScanConfig == null)
        return null;

    try
    {
        var (x, y, width, height) = _secondaryQrScanConfig.ToPixelCoordinates(dpi);
        
        var qrValue = _qrScanner.ScanRegion(imagePath, x, y, width, height);
        
        return qrValue;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ? Secondary QR scan error: {ex.Message}");
        return null;
    }
}
```

**File Name Generation:**
```csharp
private string ApplyDynamicFileName(
    string originalPath, 
    string secondaryQrValue, 
    string replacementPattern)
{
    // Extract portion before colon
    var colonIndex = secondaryQrValue.IndexOf(':');
    var extractedValue = colonIndex >= 0 
        ? secondaryQrValue.Substring(0, colonIndex).Trim()
        : secondaryQrValue.Trim();
    
    // Sanitize for filename use
    extractedValue = SanitizeFileName(extractedValue);
    
    if (string.IsNullOrEmpty(extractedValue))
    {
        Console.WriteLine($"  ? Extracted value is empty, using original filename");
        return originalPath;
    }
    
    // Get directory and filename
    var directory = Path.GetDirectoryName(originalPath) ?? "";
    var fileName = Path.GetFileName(originalPath);
    
    // Replace pattern in filename
    var newFileName = fileName.Replace(
        replacementPattern, 
        extractedValue, 
        StringComparison.OrdinalIgnoreCase);
    
    // If no replacement occurred, prepend extracted value
    if (newFileName == fileName)
    {
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        newFileName = $"{extractedValue}_{fileNameWithoutExt}{extension}";
    }
    
    var newPath = Path.Combine(directory, newFileName);
    
    Console.WriteLine($"  ? Original: {fileName}");
    Console.WriteLine($"  ? New: {newFileName}");
    Console.WriteLine($"  ? Extracted: '{extractedValue}' from '{secondaryQrValue}'");
    
    return newPath;
}

private string SanitizeFileName(string fileName)
{
    // Remove invalid filename characters
    var invalidChars = Path.GetInvalidFileNameChars();
    var sanitized = string.Concat(fileName.Split(invalidChars));
    
    // Replace spaces with underscores
    sanitized = sanitized.Replace(' ', '_');
    
    // Limit length
    if (sanitized.Length > 50)
        sanitized = sanitized.Substring(0, 50);
    
    return sanitized;
}
```

### Phase 3: Configuration Loading

#### Step 3.1: Update MainWindow.xaml.cs
In constructor and settings reload:

```csharp
// Load Secondary QR Scan Configuration
SecondaryQrScanConfig? secondaryQrScanConfig = null;
var secondaryQrSection = _config?.GetSection("BookletProcessor:ScannedSheets:SecondaryQrScan");
if (secondaryQrSection != null && secondaryQrSection.Exists())
{
    secondaryQrScanConfig = new SecondaryQrScanConfig
    {
        TriggerQrCode = secondaryQrSection["TriggerQrCode"] ?? "CHECKLISTQR-01",
        RegionXInches = double.TryParse(secondaryQrSection["RegionXInches"], out var sx) ? sx : 0.0,
        RegionYInches = double.TryParse(secondaryQrSection["RegionYInches"], out var sy) ? sy : 0.75,
        RegionWidthInches = double.TryParse(secondaryQrSection["RegionWidthInches"], out var sw) ? sw : 2.0,
        RegionHeightInches = double.TryParse(secondaryQrSection["RegionHeightInches"], out var sh) ? sh : 1.0,
        FileNameReplacementPattern = secondaryQrSection["FileNameReplacementPattern"] ?? "SchoolCityState"
    };
    
    Console.WriteLine($"Secondary QR scan configured:");
    Console.WriteLine($"  Trigger QR: {secondaryQrScanConfig.TriggerQrCode}");
    Console.WriteLine($"  Region: ({secondaryQrScanConfig.RegionXInches}\", {secondaryQrScanConfig.RegionYInches}\") " +
                      $"{secondaryQrScanConfig.RegionWidthInches}\" × {secondaryQrScanConfig.RegionHeightInches}\"");
}

// Pass to scanned sheet processor
if (!string.IsNullOrEmpty(scannedSheetTemplateName))
{
    scannedSheetProcessor = new ScannedSheetProcessorService(
        _pdfService,
        _deskewer,
        _aligner,
        _enableRedPixelRemover ? _redPixelRemover : null,
        _redThreshold,
        enableQrScanning ? _qrScanner : null,
        enableQrScanning,
        qrXInches,
        qrYInches,
        qrWidthInches,
        qrHeightInches,
        dpi,
        qrValues,
        redPixelExclusionRegions,
        secondaryQrScanConfig); // NEW PARAMETER
}
```

#### Step 3.2: Update Console Program.cs
Same configuration loading logic as MainWindow.

### Phase 4: UI & Documentation

#### Step 4.1: Update SettingsWindow (Optional - Read-Only Display)
Add section to display secondary QR scan settings:

```xaml
<TextBlock Text="Secondary QR Scan (File Naming)" 
           Grid.Row="X" Grid.Column="0" Grid.ColumnSpan="3" 
           FontWeight="Bold" Margin="0,15,0,10"/>

<TextBlock Text="Trigger QR Code:" Grid.Row="Y" Grid.Column="0" 
           VerticalAlignment="Center" Margin="0,0,10,10"/>
<TextBox x:Name="SecondaryQrTriggerTextBox" Grid.Row="Y" Grid.Column="1" Grid.ColumnSpan="2"
         Margin="0,0,0,10" IsReadOnly="True" Background="LightGray"/>

<TextBlock Text="Scan Region:" Grid.Row="Z" Grid.Column="0" 
           VerticalAlignment="Center" Margin="0,0,10,10"/>
<TextBox x:Name="SecondaryQrRegionTextBox" Grid.Row="Z" Grid.Column="1" Grid.ColumnSpan="2"
         Margin="0,0,0,10" IsReadOnly="True" Background="LightGray"/>

<TextBlock Text="Edit in appsettings.json" Grid.Row="Z+1" Grid.Column="0" Grid.ColumnSpan="3"
           FontStyle="Italic" Foreground="Gray" Margin="0,0,0,10"/>
```

Load values in SettingsWindow.xaml.cs:
```csharp
var secondaryQrSection = bp?["ScannedSheets"]?["SecondaryQrScan"];
if (secondaryQrSection != null)
{
    SecondaryQrTriggerTextBox.Text = secondaryQrSection["TriggerQrCode"]?.ToString() ?? "CHECKLISTQR-01";
    
    var x = secondaryQrSection["RegionXInches"]?.ToString() ?? "0";
    var y = secondaryQrSection["RegionYInches"]?.ToString() ?? "0.75";
    var w = secondaryQrSection["RegionWidthInches"]?.ToString() ?? "2";
    var h = secondaryQrSection["RegionHeightInches"]?.ToString() ?? "1";
    SecondaryQrRegionTextBox.Text = $"({x}\", {y}\") {w}\" × {h}\"";
}
```

#### Step 4.2: Create Documentation
Create `SECONDARY_QR_FILE_NAMING.md` with:
- Feature overview
- Configuration reference
- QR value format examples
- Filename transformation examples
- Troubleshooting guide

### Phase 5: Testing

#### Step 5.1: Unit Test Scenarios
1. **Secondary QR Found with Colon**
   - Input: QR value "SchoolName:AdditionalData"
   - Expected: Extract "SchoolName"
   - Filename: `SchoolCityState_input.pdf` ? `SchoolName_input_aligned.pdf`

2. **Secondary QR Found without Colon**
   - Input: QR value "SchoolName"
   - Expected: Use full value
   - Filename: `SchoolCityState_input.pdf` ? `SchoolName_input_aligned.pdf`

3. **No Secondary QR Found**
   - Expected: Use original filename
   - Filename: `SchoolCityState_input.pdf` ? `SchoolCityState_input_aligned.pdf`

4. **Pattern Not in Filename**
   - Input: `CustomName_input.pdf`
   - Expected: Prepend extracted value
   - Filename: `CustomName_input.pdf` ? `SchoolName_CustomName_input_aligned.pdf`

5. **Multiple CHECKLISTQR-01 Pages**
   - Expected: Use first found secondary QR
   - Ignore subsequent values

6. **Invalid Characters in QR Value**
   - Input: QR value "School/Name:Data"
   - Expected: Sanitize to "School_Name"

#### Step 5.2: Integration Test
Full workflow test:
1. Create test PDF with page containing "CHECKLISTQR-01"
2. Add secondary QR at (0", 0.75") with value "TestSchool:Extra"
3. Name input: `SchoolCityState_test.pdf`
4. Process through scanned sheet mode
5. Verify output: `TestSchool_test_aligned.pdf`

## Configuration Examples

### Example 1: Basic Configuration
```json
{
  "SecondaryQrScan": {
    "TriggerQrCode": "CHECKLISTQR-01",
    "RegionXInches": 0.0,
    "RegionYInches": 0.75,
    "RegionWidthInches": 2.0,
    "RegionHeightInches": 1.0,
    "FileNameReplacementPattern": "SchoolCityState"
  }
}
```

### Example 2: Different Trigger QR
```json
{
  "SecondaryQrScan": {
    "TriggerQrCode": "HEADER-PAGE",
    "RegionXInches": 1.0,
    "RegionYInches": 0.5,
    "RegionWidthInches": 3.0,
    "RegionHeightInches": 1.5,
    "FileNameReplacementPattern": "DefaultName"
  }
}
```

## Console Output Examples

### Successful Secondary QR Scan
```
--- Processing Page 1/10 ---
  QR Code: CHECKLISTQR-01
  ? Trigger QR detected, scanning secondary region...
  ? Secondary QR found: Lincoln_Elementary:District_5
  ? Secondary QR captured for file naming: Lincoln_Elementary:District_5
  ? Mapped to template page 0
  ? Aligning to template

...

Merging 10 processed pages...
  ? Original: SchoolCityState_input_aligned.pdf
  ? New: Lincoln_Elementary_input_aligned.pdf
  ? Extracted: 'Lincoln_Elementary' from 'Lincoln_Elementary:District_5'
  ? Renamed output: Lincoln_Elementary_input_aligned.pdf

? Processing complete!
  Output: C:\Output\Lincoln_Elementary_input_aligned.pdf
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
  Output: C:\Output\SchoolCityState_input_aligned.pdf
```

## Edge Cases & Error Handling

### 1. Secondary QR Scan Fails
**Handling**: Log warning, continue processing, use original filename
**Message**: "? Secondary QR scan error: {error}"

### 2. Extracted Value is Empty
**Handling**: Use original filename
**Message**: "? Extracted value is empty, using original filename"

### 3. File Already Exists
**Handling**: File.Move will throw exception
**Solution**: Add check and append suffix if needed
```csharp
if (File.Exists(finalOutputPath) && finalOutputPath != outputPdf)
{
    var counter = 1;
    var directory = Path.GetDirectoryName(finalOutputPath) ?? "";
    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(finalOutputPath);
    var extension = Path.GetExtension(finalOutputPath);
    
    do
    {
        finalOutputPath = Path.Combine(directory, $"{fileNameWithoutExt}_{counter}{extension}");
        counter++;
    }
    while (File.Exists(finalOutputPath));
}
```

### 4. Colon at Start or End
**Examples:**
- `:SchoolName` ? Extract "" (empty) ? Use original
- `SchoolName:` ? Extract "SchoolName" ? Use it

### 5. Multiple Colons
**Example:**
- `School:Name:Extra` ? Extract "School" (first portion only)

### 6. Invalid Filename Characters
**Characters**: `< > : " / \ | ? *`
**Handling**: Remove or replace with underscore

## Performance Considerations

### Additional Processing Time
- Secondary QR scan: ~50-100ms per scan
- Only on pages with trigger QR code
- Filename operation: <1ms

### Memory Impact
- Negligible (single string variable)

### File I/O
- One additional File.Move operation
- Minimal impact

## Backwards Compatibility

### No Configuration Present
**Behavior**: Feature disabled, processing continues normally
**No breaking changes**: Existing workflows unaffected

### Empty Configuration
**Behavior**: Feature disabled
```json
{
  "SecondaryQrScan": {}
}
```

## Alternative Approaches Considered

### Alternative 1: Scan All Pages
**Approach**: Scan secondary region on all pages, not just trigger QR
**Rejected**: Unnecessary overhead, specific requirement for CHECKLISTQR-01

### Alternative 2: Multiple Replacement Patterns
**Approach**: Support array of patterns to replace
**Deferred**: Can be added later if needed, YAGNI principle

### Alternative 3: Regex-Based Pattern Matching
**Approach**: Use regex for filename transformation
**Rejected**: Too complex for current requirement, harder to configure

### Alternative 4: Template-Based Naming
**Approach**: Use template string like `{extracted}_input_aligned.pdf`
**Deferred**: Could be future enhancement

## Implementation Checklist

### Phase 1: Configuration & Model
- [ ] Add SecondaryQrScan configuration to WPF appsettings.json
- [ ] Add SecondaryQrScan configuration to Console appsettings.json
- [ ] Create SecondaryQrScanConfig.cs model class
- [ ] Add unit tests for model methods

### Phase 2: Service Layer
- [ ] Add _secondaryQrScanConfig field to ScannedSheetProcessorService
- [ ] Update constructor to accept SecondaryQrScanConfig
- [ ] Add secondaryQrValue tracking in ProcessScannedSheetsAsync
- [ ] Update ProcessSinglePageAsync return type to tuple
- [ ] Implement ScanSecondaryQrRegion method
- [ ] Implement ApplyDynamicFileName method
- [ ] Implement SanitizeFileName method
- [ ] Add file renaming logic after merge
- [ ] Handle file exists edge case

### Phase 3: Configuration Loading
- [ ] Load secondary QR config in MainWindow.xaml.cs constructor
- [ ] Load secondary QR config in MainWindow.xaml.cs settings reload
- [ ] Load secondary QR config in Console Program.cs
- [ ] Pass config to ScannedSheetProcessorService
- [ ] Add startup logging for configuration

### Phase 4: UI & Documentation
- [ ] Add read-only display to SettingsWindow.xaml
- [ ] Load values in SettingsWindow.xaml.cs
- [ ] Create SECONDARY_QR_FILE_NAMING.md documentation
- [ ] Update SCANNED_SHEET_QUICK_REFERENCE.md
- [ ] Add configuration examples to docs

### Phase 5: Testing
- [ ] Test with QR value containing colon
- [ ] Test with QR value without colon
- [ ] Test with no secondary QR found
- [ ] Test with pattern not in filename
- [ ] Test with multiple trigger pages
- [ ] Test with invalid filename characters
- [ ] Test with empty extracted value
- [ ] Test full integration workflow
- [ ] Verify backwards compatibility

### Phase 6: Polish
- [ ] Add comprehensive logging
- [ ] Add error handling for all edge cases
- [ ] Verify console output is clear
- [ ] Run full build and test
- [ ] Update version documentation

## Estimated Implementation Time

- **Phase 1** (Config & Model): 30 minutes
- **Phase 2** (Service Layer): 2 hours
- **Phase 3** (Config Loading): 30 minutes
- **Phase 4** (UI & Docs): 1 hour
- **Phase 5** (Testing): 1 hour
- **Phase 6** (Polish): 30 minutes

**Total**: ~5-6 hours

## Success Criteria

1. ? Secondary QR successfully scanned on CHECKLISTQR-01 pages
2. ? Text extracted correctly before colon
3. ? Output file renamed with extracted value
4. ? "SchoolCityState" pattern replaced in filename
5. ? Graceful handling when secondary QR not found
6. ? Clear console logging of operations
7. ? No impact on other scanned sheet processing
8. ? Backwards compatible (no config = no change)
9. ? Comprehensive documentation created
10. ? All edge cases handled

## Questions for Clarification

1. **Multiple Trigger Pages**: If multiple pages have "CHECKLISTQR-01", should we use the first secondary QR value found, or the last? (Plan assumes first)

2. **Filename Pattern Location**: Is "SchoolCityState" always at the start of the filename, or can it be anywhere? (Plan handles both)

3. **Missing Pattern**: If "SchoolCityState" is not in the filename, should we prepend the extracted value or skip renaming? (Plan prepends)

4. **File Conflict**: If renamed file already exists, append counter or overwrite? (Plan appends counter)

5. **QR Format**: Is the colon (`:`) always present in the secondary QR value, or optional? (Plan handles both)

6. **Character Limits**: Any restrictions on length of extracted value? (Plan limits to 50 chars)

7. **Case Sensitivity**: Should replacement be case-sensitive? (Plan is case-insensitive)

---

**Ready to proceed?** Please review this plan and provide feedback before implementation begins. Any questions or adjustments can be addressed before coding starts.
