# Input File Renaming and Archiving Feature

## Overview
Automatically rename and archive input scanned sheet files based on secondary QR code values, matching the naming convention used for output files.

## Feature Description

When processing scanned sheets with secondary QR codes:
1. **Output file** is renamed: `SchoolCityState_001.pdf` ? `Lincoln_Elementary_001_aligned.pdf`
2. **Input file** is renamed and moved to archive folder: `SchoolCityState_001.pdf` ? `ToArchive\Lincoln_Elementary_001.pdf`

This keeps naming consistent between input and output files and organizes original scans into an archive folder.

## Configuration

### appsettings.json

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
        "FileNameReplacementPattern": "SchoolCityState",
        "RenameInputFiles": true,
        "ArchiveFolder": "C:\\Users\\allen\\Dropbox\\Data\\Catforms\\Scans\\TestScans\\ToArchive"
      }
    }
  }
}
```

### Configuration Fields

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `RenameInputFiles` | boolean | Enable/disable input file renaming | `true` |
| `ArchiveFolder` | string | Destination folder for renamed input files | `C:\Users\allen\Dropbox\Data\Catforms\Scans\TestScans\ToArchive` |

**Other fields** (TriggerQrCode, Region*, FileNameReplacementPattern) work as documented in SECONDARY_QR_FILE_NAMING_GUIDE.md

## How It Works

### Processing Flow

```
1. File detected: SchoolCityState_001.pdf
   ?
2. Process scanned sheets
   ?
3. Find secondary QR: "Lincoln_Elementary:District_5"
   ?
4. Extract value: "Lincoln_Elementary"
   ?
5. Create output: Lincoln_Elementary_001_aligned.pdf
   ?
6. IF RenameInputFiles = true:
   - Rename input: Lincoln_Elementary_001.pdf
   - Move to: ToArchive\Lincoln_Elementary_001.pdf
```

### Timing

**Input file is renamed and moved:**
- ? **After** processing completes successfully
- ? **After** secondary QR is found and extracted
- ? **After** output file is created
- ? **Not moved** if processing fails
- ? **Not moved** if no secondary QR found

This ensures the original file remains in place if anything goes wrong.

## File Naming Examples

### Example 1: Standard Processing

**Configuration:**
```json
{
  "FileNameReplacementPattern": "SchoolCityState",
  "RenameInputFiles": true,
  "ArchiveFolder": "C:\\Archive"
}
```

**Input:**
- File: `C:\Input\SchoolCityState_001.pdf`
- Secondary QR: `Lincoln_Elementary:Extra`

**Output:**
- Processed: `C:\Output\Lincoln_Elementary_001_aligned.pdf`
- Archived: `C:\Archive\Lincoln_Elementary_001.pdf`

**Console Output:**
```
  ? Output file renamed to: Lincoln_Elementary_001_aligned.pdf

  ? Input Archive:
    Original: SchoolCityState_001.pdf
    Archived as: Lincoln_Elementary_001.pdf
    Location: C:\Archive
```

### Example 2: Pattern Not in Filename

**Input:**
- File: `CustomScan_002.pdf`
- Secondary QR: `Washington_High:Data`

**Output:**
- Processed: `C:\Output\Washington_High_CustomScan_002_aligned.pdf`
- Archived: `C:\Archive\Washington_High_CustomScan_002.pdf`

### Example 3: File Conflict Handling

If `Lincoln_Elementary_001.pdf` already exists in archive folder:

**Result:**
- Archived as: `Lincoln_Elementary_001_20231216153045.pdf`
  (timestamp appended: `yyyyMMddHHmmss`)

**Console Output:**
```
  ? Input Archive:
    Original: SchoolCityState_001.pdf
    Archived as: Lincoln_Elementary_001_20231216153045.pdf
    Location: C:\Archive
```

## Disabling Input File Renaming

To keep input files in their original location without renaming:

```json
{
  "SecondaryQrScan": {
    "RenameInputFiles": false,
    // ... other settings
  }
}
```

**Result:**
- Output file: Renamed as usual
- Input file: Remains in original location with original name

## Folder Management

### Archive Folder Creation

The archive folder is automatically created if it doesn't exist:
- No manual setup required
- Parent folders must exist

**Example:**
- `C:\Archive` exists ? `ToArchive` subfolder created automatically
- `C:\DoesNotExist\Archive` ? Error (parent doesn't exist)

### Recommended Folder Structure

```
C:\Scans\
??? Input\              (monitored folder)
?   ??? SchoolCityState_001.pdf  ? Detected here
?   ??? SchoolCityState_002.pdf
??? Output\             (processed files)
?   ??? Lincoln_Elementary_001_aligned.pdf
?   ??? Washington_High_002_aligned.pdf
??? ToArchive\          (archive folder)
    ??? Lincoln_Elementary_001.pdf  ? Moved here
    ??? Washington_High_002.pdf
```

## Error Handling

### Archive Folder Not Accessible

**Scenario:** Archive folder path is invalid or not accessible

**Behavior:**
- Processing completes successfully
- Output file created normally
- Input file remains in original location
- Warning logged to console

**Console Output:**
```
  ? Failed to rename/move input file: Access to path denied
    Input file remains at: C:\Input\SchoolCityState_001.pdf
```

### File Already Exists

**Scenario:** File with same name already in archive folder

**Behavior:**
- Timestamp appended to filename
- File archived with unique name
- Processing continues normally

### No Secondary QR Found

**Scenario:** CHECKLISTQR-01 found but no secondary QR in region

**Behavior:**
- Input file not renamed (no value to extract)
- Input file remains in original location
- Output file also not renamed

### Processing Fails

**Scenario:** Exception during processing

**Behavior:**
- Input file not touched
- Remains in original location
- Can be reprocessed

## Use Cases

### 1. School Test Booklets

**Scenario:** Scanner produces generic filenames, school name in QR code

**Configuration:**
```json
{
  "RenameInputFiles": true,
  "ArchiveFolder": "\\\\NetworkShare\\Archives\\TestBooklets"
}
```

**Benefits:**
- Original scans archived with meaningful names
- Network backup of originals
- Easy to identify which school's test

### 2. Multi-Site Processing

**Scenario:** Central processing center handles multiple locations

**Configuration:**
```json
{
  "RenameInputFiles": true,
  "ArchiveFolder": "C:\\Archives\\ByLocation"
}
```

**Benefits:**
- Automatic organization by extracted location name
- Original scans preserved
- Processed and originals match for audit trail

### 3. Compliance/Archiving

**Scenario:** Regulatory requirement to keep original scans

**Configuration:**
```json
{
  "RenameInputFiles": true,
  "ArchiveFolder": "C:\\ComplianceArchive\\Originals"
}
```

**Benefits:**
- Automatic compliance archiving
- Renamed for easy search/retrieval
- Clear separation from processed files

## Concurrent Processing Behavior

When multiple files are processed concurrently:

? **Thread-safe**: Each job moves its own input file independently  
? **No conflicts**: Unique filenames (scanner counter + timestamp if needed)  
? **Archive folder**: Shared safely across concurrent jobs  
? **Automatic retry**: File move retries if temporarily locked  

**Example with 4 concurrent jobs:**
```
[Job abc123] ? Input Archive:
  Archived as: Lincoln_Elementary_001.pdf
  Location: C:\Archive

[Job def456] ? Input Archive:
  Archived as: Washington_High_002.pdf
  Location: C:\Archive

[Job ghi789] ? Input Archive:
  Archived as: Jefferson_MS_003.pdf
  Location: C:\Archive

[Job jkl012] ? Input Archive:
  Archived as: Roosevelt_HS_004.pdf
  Location: C:\Archive
```

All 4 files safely archived without conflicts.

## Monitoring and Verification

### Success Indicators

Look for these in console output:

```
  ? Output file renamed to: Lincoln_Elementary_001_aligned.pdf

  ? Input Archive:
    Original: SchoolCityState_001.pdf
    Archived as: Lincoln_Elementary_001.pdf
    Location: C:\Archive

? Processing complete!
```

### Warning Indicators

```
  ? Failed to rename/move input file: [error message]
    Input file remains at: [original path]
```

### Verification Script

PowerShell script to verify archive matches output:

```powershell
# Check that archived files match output files
$outputFolder = "C:\Output"
$archiveFolder = "C:\Archive"

$outputs = Get-ChildItem -Path $outputFolder -Filter "*_aligned.pdf"
foreach ($output in $outputs) {
    # Derive expected archive name
    $baseName = $output.BaseName -replace "_aligned$", ""
    $archiveName = "$baseName.pdf"
    $archivePath = Join-Path $archiveFolder $archiveName
    
    if (Test-Path $archivePath) {
        Write-Host "? $archiveName" -ForegroundColor Green
    } else {
        Write-Host "? Missing: $archiveName" -ForegroundColor Red
    }
}
```

## Troubleshooting

### Input Files Not Being Archived

**Check:**
1. `RenameInputFiles` = `true` in config
2. Archive folder path exists and is accessible
3. Secondary QR was successfully scanned
4. No console warnings about file move failure

### Archive Folder Filling Up

**Solutions:**
1. Implement periodic cleanup of old files
2. Move to network storage with more space
3. Consider compression for old archives
4. Set up automated backup and purge process

### Filename Conflicts

**Behavior:** System automatically adds timestamp to prevent overwrites

**If undesired:**
- Review scanner settings (ensure unique counters)
- Manually clean duplicate archives
- Implement pre-processing filename validation

### Permission Issues

**Error:** "Access denied" when moving to archive folder

**Solutions:**
1. Ensure application has write permissions to archive folder
2. Check network share permissions (if using UNC path)
3. Verify folder is not read-only
4. Check antivirus isn't blocking file moves

## Best Practices

### 1. Archive Folder Location

**Recommended:**
- ? Local SSD for performance
- ? Network share for backup
- ? Separate physical drive from processing
- ? Same folder as input (risk of confusion)
- ? Temp folders (risk of deletion)

### 2. Folder Organization

```
C:\Scans\
??? Input\           (hot folder - cleared after processing)
??? Output\          (processed files - cleared after delivery)
??? ToArchive\       (permanent archive - backed up regularly)
    ??? 2023\
    ?   ??? December\
    ??? 2024\
        ??? January\
```

Consider date-based subfolders for large volumes.

### 3. Monitoring

Set up monitoring for:
- Archive folder disk space
- Failed file moves (parse console logs)
- Missing archives (compare output to archive counts)

### 4. Backup

- Regular backup of archive folder
- Test restore procedures
- Keep backup separate from processing server

## Summary

? **Automatic**: Input files renamed and archived automatically  
? **Consistent**: Matches output file naming convention  
? **Safe**: Only moved after successful processing  
? **Configurable**: Enable/disable and set destination  
? **Reliable**: Handles conflicts and errors gracefully  
? **Thread-safe**: Works with concurrent processing  

**Configuration Impact:**
- Storage: Archive folder gradually fills (plan disk space)
- Performance: <50ms per file to rename and move
- Organization: Automatic archiving reduces manual work

**Default Behavior:**
- `RenameInputFiles` = `true` (feature enabled)
- Archive folder = `C:\Users\allen\Dropbox\Data\Catforms\Scans\TestScans\ToArchive`
- Timestamp added if file exists in archive
- Errors logged but don't fail processing
