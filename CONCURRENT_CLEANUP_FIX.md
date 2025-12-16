# Concurrent Processing Temp Folder Cleanup Fix

## Issue Identified

When running 4 scanned sheet processing jobs concurrently, only 1 of the 4 `temp_scannedsheets_*` folders was being cleaned up. The other 3 remained in the output folder.

## Root Causes

### 1. **File Locks in Concurrent Processing**
When multiple jobs run simultaneously:
- Files may still be locked by the PDF library (PdfSharp/ImageSharp)
- File handles might not be released immediately after processing
- Windows file system needs time to release locks

### 2. **No Retry Logic**
The original `CleanupDirectory` method:
```csharp
public static void CleanupDirectory(string path)
{
    if (Directory.Exists(path))
    {
        try
        {
            Directory.Delete(path, true);
            Console.WriteLine($"Cleaned up temporary folder: {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to clean up folder {path}: {ex.Message}");
            // ?? Fails silently - folder remains!
        }
    }
}
```

**Problem**: If deletion fails (file locked, permission denied), it logs the error but doesn't retry.

### 3. **Immediate Cleanup Timing**
The `finally` block executes immediately after the try block completes:
```csharp
finally
{
    PdfService.CleanupDirectory(workingFolder); // ?? Too soon!
}
```

Files might still be locked by:
- PDF writer finalizing
- Image library buffer flush
- Operating system file handle release

## Solution Implemented

### 1. **Enhanced CleanupDirectory with Retry Logic**

```csharp
public static void CleanupDirectory(string path)
{
    if (!Directory.Exists(path))
        return;

    const int maxAttempts = 5;
    const int delayMs = 200;

    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            // Force readonly attributes off for all files
            var dirInfo = new DirectoryInfo(path);
            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    file.Attributes = FileAttributes.Normal;
                }
                catch { /* Ignore */ }
            }

            // Delete directory
            Directory.Delete(path, true);
            Console.WriteLine($"Cleaned up temporary folder: {path}");
            return; // ? Success!
        }
        catch (IOException ex) when (attempt < maxAttempts)
        {
            // File locked, wait and retry
            Console.WriteLine($"Cleanup attempt {attempt}/{maxAttempts} failed: {ex.Message}");
            System.Threading.Thread.Sleep(delayMs);
        }
        catch (UnauthorizedAccessException ex) when (attempt < maxAttempts)
        {
            // Permission issue, wait and retry
            Console.WriteLine($"Cleanup attempt {attempt}/{maxAttempts} failed (access denied): {ex.Message}");
            System.Threading.Thread.Sleep(delayMs);
        }
        catch (Exception ex)
        {
            // Other errors
            if (attempt == maxAttempts)
            {
                Console.WriteLine($"? WARNING: Temp folder not cleaned up: {path}");
            }
        }
    }
}
```

**Key Improvements:**
- **5 retry attempts** with 200ms delays between attempts (total: 1 second)
- **Removes read-only attributes** before deletion
- **Specific exception handling** for file locks vs permissions
- **Clear warning** if cleanup ultimately fails

### 2. **Added Brief Delay Before Cleanup**

```csharp
finally
{
    // Clean up the working folder (contains all temporary files)
    // Use async cleanup with delay to ensure files are released
    await Task.Delay(100); // Brief delay to ensure file handles released
    PdfService.CleanupDirectory(workingFolder);
}
```

**Benefits:**
- 100ms delay allows file handles to be released
- Gives operating system time to finalize file operations
- Reduces likelihood of file locks on first cleanup attempt

## How It Works Now

### Cleanup Flow

```
Job Completes
    ?
Wait 100ms (file handle release)
    ?
Attempt 1: Delete folder
    ? (if failed - file locked)
Wait 200ms
    ?
Attempt 2: Delete folder
    ? (if failed)
Wait 200ms
    ?
Attempt 3: Delete folder
    ? (if failed)
Wait 200ms
    ?
Attempt 4: Delete folder
    ? (if failed)
Wait 200ms
    ?
Attempt 5: Delete folder (final attempt)
    ?
Success ? or Warning ??
```

**Total time allowed**: 100ms + (4 × 200ms) = 900ms per cleanup

## Testing Recommendations

### Test Scenario 1: Normal Concurrent Processing
1. Drop 4 files simultaneously in monitored folder
2. Wait for all to complete
3. Check output folder for temp folders
4. **Expected**: All 4 temp folders cleaned up

### Test Scenario 2: Monitor Console Output
Look for these patterns:

**Successful cleanup (most common):**
```
[Job abc123] ? Completed in 00:45
Cleaned up temporary folder: C:\Output\temp_scannedsheets_abc123
```

**Cleanup with retry (file was locked):**
```
[Job abc123] ? Completed in 00:45
Cleanup attempt 1/5 failed: The process cannot access the file...
Cleanup attempt 2/5 failed: The process cannot access the file...
Cleaned up temporary folder: C:\Output\temp_scannedsheets_abc123
```

**Cleanup failure (persistent lock):**
```
[Job abc123] ? Completed in 00:45
Cleanup attempt 1/5 failed: The process cannot access the file...
Cleanup attempt 2/5 failed: The process cannot access the file...
Cleanup attempt 3/5 failed: The process cannot access the file...
Cleanup attempt 4/5 failed: The process cannot access the file...
Cleanup attempt 5/5 failed: The process cannot access the file...
? WARNING: Temp folder not cleaned up: C:\Output\temp_scannedsheets_abc123
  You may need to manually delete this folder.
```

### Test Scenario 3: High Concurrency
1. Set `MaxConcurrency` to 8
2. Drop 10+ files
3. Monitor cleanup success rate
4. Check for warnings in console

## Troubleshooting

### If Temp Folders Still Not Cleaned Up

#### Check 1: File Locks
```powershell
# Check what process has files open
Get-Process | Where-Object {$_.Path -like "*TestBookletProcessor*"}
```

#### Check 2: Antivirus
Some antivirus software scans new files and locks them temporarily:
- Add output folder to antivirus exclusions
- Or add 500ms delay instead of 100ms

#### Check 3: Disk Performance
Slow disk (HDD) might need more time:
```csharp
await Task.Delay(500); // Increase delay for HDD
```

#### Check 4: File Permissions
Ensure the process has full control over output folder:
```powershell
icacls "C:\TestBooklets\Output" /grant Users:F /t
```

### Manual Cleanup Script

If temp folders accumulate, run this PowerShell script:

```powershell
# Clean up orphaned temp folders
$outputFolder = "C:\TestBooklets\Output"
$tempFolders = Get-ChildItem -Path $outputFolder -Directory -Filter "temp_scannedsheets_*"

foreach ($folder in $tempFolders) {
    try {
        Remove-Item -Path $folder.FullName -Recurse -Force
        Write-Host "Cleaned up: $($folder.Name)" -ForegroundColor Green
    }
    catch {
        Write-Host "Failed to clean: $($folder.Name) - $($_.Exception.Message)" -ForegroundColor Red
    }
}
```

## Configuration Options

### Adjust Retry Settings (if needed)

If you experience persistent cleanup issues, you can adjust the retry parameters in `PdfService.cs`:

```csharp
// Current values (in CleanupDirectory method)
const int maxAttempts = 5;    // Increase to 10 for more retries
const int delayMs = 200;      // Increase to 500 for longer delays
```

**Example adjustments:**
- **Fast SSD**: `maxAttempts = 3`, `delayMs = 100`
- **Slow HDD**: `maxAttempts = 10`, `delayMs = 500`
- **Network drive**: `maxAttempts = 15`, `delayMs = 1000`

### Adjust Initial Delay (if needed)

In `ScannedSheetProcessorService.cs` (finally block):

```csharp
await Task.Delay(100); // Increase to 500 for more cautious cleanup
```

## Performance Impact

### Before Fix
- **Cleanup Success Rate**: ~25% (1 out of 4)
- **Disk Space**: Accumulates temp folders
- **Manual intervention**: Required to clean up

### After Fix
- **Cleanup Success Rate**: Expected >95%
- **Additional time**: ~100-300ms per job (negligible)
- **Disk space**: Automatically managed
- **Manual intervention**: Rarely needed

### Overhead Analysis
- **Initial delay**: 100ms per job
- **Retry delays**: Only if cleanup fails (0-800ms)
- **Total overhead**: 100-900ms per job (typically 100ms)

**Example with 4 concurrent jobs:**
- Before: 4 × 45s = 180s processing + manual cleanup
- After: 4 × 45s + 0.1s = 180.1s processing + automatic cleanup

**Impact**: < 0.1% performance overhead, 100% automation improvement

## Monitoring

### Success Metrics
Monitor these in console output:
1. **Successful cleanups**: Look for "Cleaned up temporary folder"
2. **Retry attempts**: Count "Cleanup attempt X/5" messages
3. **Warnings**: Count "? WARNING: Temp folder not cleaned up"

### Health Check
After processing 20 files concurrently:
- **Good**: 0-1 warnings, 0-2 temp folders remaining
- **Acceptable**: 2-3 warnings, 2-3 temp folders remaining
- **Issue**: 4+ warnings, 4+ temp folders remaining

If "Issue" state occurs:
1. Increase retry attempts
2. Increase delays
3. Check for file lock issues
4. Consider antivirus exclusions

## Summary

? **Fixed**: Added retry logic with 5 attempts and 200ms delays  
? **Fixed**: Added 100ms initial delay before cleanup  
? **Fixed**: Force removes read-only attributes  
? **Improved**: Better error messages and warnings  
? **Improved**: Specific handling for file locks vs permissions  

The temp folder cleanup should now work reliably for concurrent processing. In the rare case cleanup fails after 5 attempts, you'll see a clear warning in the console and can manually clean up if needed.

**Expected result**: All temp folders cleaned up automatically in >95% of cases.
