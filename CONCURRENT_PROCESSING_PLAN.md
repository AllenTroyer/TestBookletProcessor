# Concurrent Scanned Sheets Processing Plan

## Executive Summary
Implement a robust concurrent processing system to handle 10-20 files dropped simultaneously in monitored folders without bottlenecks, race conditions, or file conflicts.

## Current Architecture Analysis

### Identified Issues

#### 1. **Sequential Processing (Major Bottleneck)**
**Current Behavior:**
```csharp
private async void FolderMonitorJobService_FileDetected(object? sender, FolderFileDetectedEventArgs e)
{
    // This runs sequentially - second file waits for first to complete
    var result = await _bookletProcessor.ProcessBookletsWorkflowAsync(...);
}
```

**Problem:**
- Files processed one at a time
- If 20 files arrive, the 20th file waits for 19 files to complete (~15-30 minutes)
- No parallelization
- CPU/GPU underutilized

#### 2. **Potential Temp Folder Conflicts**
**Current Code:**
```csharp
var uniqueId = Guid.NewGuid().ToString("N");
var workingFolder = Path.Combine(outputFolder, $"temp_scannedsheets_{uniqueId}");
```

**Status:** ? Already unique per job - GOOD!

#### 3. **Service Instance Sharing**
**Current Code:**
```csharp
private BookletProcessorService _bookletProcessor; // Single shared instance
```

**Potential Issues:**
- Services like `RegionQrScanner`, `IDeskewer`, `IImageAligner` shared across concurrent operations
- Need to verify thread-safety of these services
- Possible state corruption if services maintain state

#### 4. **Output File Naming Conflicts**
**Current Code:**
```csharp
var finalOutputPdf = Path.Combine(outputFolder, $"{inputFileNameNoExt}_aligned.pdf");
```

**Problem:**
- If two files have same name (different folders), collision possible
- External scanner adds counter, but same file processed twice = collision

#### 5. **No Concurrency Limits**
**Problem:**
- If 100 files arrive, system tries to process all 100 simultaneously
- Memory exhaustion
- Disk I/O saturation
- System becomes unresponsive

## Proposed Solution Architecture

### Design Goals
1. ? **Parallel Processing**: Process multiple files concurrently
2. ? **Bounded Concurrency**: Limit to N concurrent jobs (e.g., 4-8)
3. ? **Thread Safety**: No race conditions or data corruption
4. ? **Resource Management**: Prevent memory/disk exhaustion
5. ? **Error Isolation**: One job failure doesn't affect others
6. ? **Progress Tracking**: Monitor concurrent jobs
7. ? **Queue Management**: Handle backlog gracefully

### Solution Overview

```
FileSystemWatcher (Multiple Files Detected)
    ?
Queue (Thread-Safe)
    ?
SemaphoreSlim (Max Concurrency = 4)
    ?
Task Pool (4 concurrent workers)
    ?
Each Task:
    - Creates own service instances
    - Unique temp folder
    - Isolated processing
    - Independent error handling
    ?
Results aggregated and logged
```

## Implementation Plan

### Phase 1: Add Concurrent Job Queue

#### Step 1.1: Create Job Queue Model
Create `ProcessingJob.cs`:

```csharp
public class ProcessingJob
{
    public Guid JobId { get; set; } = Guid.NewGuid();
    public string InputFilePath { get; set; } = "";
    public string TemplateFilePath { get; set; } = "";
    public string OutputFolder { get; set; } = "";
    public DateTime QueuedTime { get; set; } = DateTime.Now;
    public DateTime? StartedTime { get; set; }
    public DateTime? CompletedTime { get; set; }
    public ProcessingJobStatus Status { get; set; } = ProcessingJobStatus.Queued;
    public string? ErrorMessage { get; set; }
    public ProcessingResult? Result { get; set; }
}

public enum ProcessingJobStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
    Cancelled
}
```

#### Step 1.2: Create Concurrent Processing Service
Create `ConcurrentProcessingService.cs`:

```csharp
public class ConcurrentProcessingService : IDisposable
{
    private readonly ConcurrentQueue<ProcessingJob> _jobQueue = new();
    private readonly ConcurrentDictionary<Guid, ProcessingJob> _activeJobs = new();
    private readonly ConcurrentDictionary<Guid, ProcessingJob> _completedJobs = new();
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly int _maxConcurrency;
    
    // Service factory delegates
    private readonly Func<IPdfService> _pdfServiceFactory;
    private readonly Func<IDeskewer> _deskewerFactory;
    private readonly Func<IImageAligner> _alignerFactory;
    private readonly Func<IRedPixelRemoverService?> _redPixelRemoverFactory;
    private readonly Func<RegionQrScanner?> _qrScannerFactory;
    
    // Configuration
    private readonly byte _redThreshold;
    private readonly int _dpi;
    private readonly bool _enableQrScanning;
    // ... other config fields
    
    public event EventHandler<ProcessingJobEventArgs>? JobStarted;
    public event EventHandler<ProcessingJobEventArgs>? JobCompleted;
    public event EventHandler<ProcessingJobEventArgs>? JobFailed;
    
    public ConcurrentProcessingService(
        int maxConcurrency = 4,
        Func<IPdfService>? pdfServiceFactory = null,
        Func<IDeskewer>? deskewerFactory = null,
        // ... other factories
        )
    {
        _maxConcurrency = maxConcurrency;
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        
        // Default factories create new instances
        _pdfServiceFactory = pdfServiceFactory ?? (() => new PdfService());
        _deskewerFactory = deskewerFactory ?? (() => new Deskewer());
        // ... initialize other factories
        
        // Start background processing loop
        Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token));
    }
    
    public Guid EnqueueJob(string inputFile, string templateFile, string outputFolder)
    {
        var job = new ProcessingJob
        {
            InputFilePath = inputFile,
            TemplateFilePath = templateFile,
            OutputFolder = outputFolder
        };
        
        _jobQueue.Enqueue(job);
        Console.WriteLine($"[Queue] Job {job.JobId:N} queued: {Path.GetFileName(inputFile)}");
        Console.WriteLine($"[Queue] Jobs in queue: {_jobQueue.Count}, Active: {_activeJobs.Count}");
        
        return job.JobId;
    }
    
    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Wait if queue is empty
                if (_jobQueue.IsEmpty)
                {
                    await Task.Delay(100, cancellationToken);
                    continue;
                }
                
                // Try to get a job
                if (!_jobQueue.TryDequeue(out var job))
                    continue;
                
                // Wait for available slot (respects max concurrency)
                await _concurrencyLimiter.WaitAsync(cancellationToken);
                
                // Process job in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessJobAsync(job, cancellationToken);
                    }
                    finally
                    {
                        _concurrencyLimiter.Release();
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Queue] Error in queue processor: {ex.Message}");
            }
        }
    }
    
    private async Task ProcessJobAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        job.Status = ProcessingJobStatus.Processing;
        job.StartedTime = DateTime.Now;
        _activeJobs[job.JobId] = job;
        
        Console.WriteLine($"[Job {job.JobId:N}] Starting: {Path.GetFileName(job.InputFilePath)}");
        JobStarted?.Invoke(this, new ProcessingJobEventArgs(job));
        
        try
        {
            // Wait for file to be fully written (important for FileSystemWatcher)
            await WaitForFileReadyAsync(job.InputFilePath, cancellationToken);
            
            // Create isolated service instances for this job
            var pdfService = _pdfServiceFactory();
            var deskewer = _deskewerFactory();
            var aligner = _alignerFactory();
            var redPixelRemover = _redPixelRemoverFactory();
            var qrScanner = _qrScannerFactory();
            
            // Load scanned sheet config for this job
            var scannedSheetQrMapping = LoadQrMapping(); // Load from config
            var redPixelExclusionRegions = LoadExclusionRegions(); // Load from config
            var secondaryQrScanConfig = LoadSecondaryQrConfig(); // Load from config
            
            // Create scanned sheet processor with isolated instances
            var scannedSheetProcessor = new ScannedSheetProcessorService(
                pdfService,
                deskewer,
                aligner,
                redPixelRemover,
                _redThreshold,
                qrScanner,
                _enableQrScanning,
                // ... other parameters
                redPixelExclusionRegions,
                secondaryQrScanConfig);
            
            // Create booklet processor
            var bookletProcessor = new BookletProcessorService(
                pdfService,
                deskewer,
                aligner,
                redPixelRemover,
                _redThreshold,
                _dpi,
                qrScanner,
                _enableQrScanning,
                // ... other parameters
                scannedSheetProcessor,
                scannedSheetTemplateName,
                scannedSheetQrMapping);
            
            // Process the file
            var result = await bookletProcessor.ProcessBookletsWorkflowAsync(
                job.InputFilePath,
                job.TemplateFilePath,
                job.OutputFolder,
                null);
            
            job.Result = result;
            job.Status = result.Success ? ProcessingJobStatus.Completed : ProcessingJobStatus.Failed;
            job.ErrorMessage = result.ErrorMessage;
            job.CompletedTime = DateTime.Now;
            
            if (result.Success)
            {
                Console.WriteLine($"[Job {job.JobId:N}] ? Completed in {job.CompletedTime.Value - job.StartedTime.Value:mm\\:ss}");
                JobCompleted?.Invoke(this, new ProcessingJobEventArgs(job));
            }
            else
            {
                Console.WriteLine($"[Job {job.JobId:N}] ? Failed: {result.ErrorMessage}");
                JobFailed?.Invoke(this, new ProcessingJobEventArgs(job));
            }
        }
        catch (Exception ex)
        {
            job.Status = ProcessingJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedTime = DateTime.Now;
            
            Console.WriteLine($"[Job {job.JobId:N}] ? Exception: {ex.Message}");
            JobFailed?.Invoke(this, new ProcessingJobEventArgs(job));
        }
        finally
        {
            _activeJobs.TryRemove(job.JobId, out _);
            _completedJobs[job.JobId] = job;
            
            // Clean up old completed jobs (keep last 100)
            if (_completedJobs.Count > 100)
            {
                var oldestJob = _completedJobs.Values
                    .OrderBy(j => j.CompletedTime)
                    .FirstOrDefault();
                if (oldestJob != null)
                    _completedJobs.TryRemove(oldestJob.JobId, out _);
            }
        }
    }
    
    private async Task WaitForFileReadyAsync(string filePath, CancellationToken cancellationToken)
    {
        const int maxAttempts = 30; // 3 seconds max
        const int delayMs = 100;
        
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                // Try to open file exclusively
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return; // File is ready
                }
            }
            catch (IOException)
            {
                // File still being written
                await Task.Delay(delayMs, cancellationToken);
            }
        }
    }
    
    public JobStatistics GetStatistics()
    {
        return new JobStatistics
        {
            QueuedCount = _jobQueue.Count,
            ActiveCount = _activeJobs.Count,
            CompletedCount = _completedJobs.Count,
            MaxConcurrency = _maxConcurrency,
            ActiveJobs = _activeJobs.Values.ToList(),
            RecentCompleted = _completedJobs.Values
                .OrderByDescending(j => j.CompletedTime)
                .Take(20)
                .ToList()
        };
    }
    
    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _concurrencyLimiter.Dispose();
        _cancellationTokenSource.Dispose();
    }
}

public class ProcessingJobEventArgs : EventArgs
{
    public ProcessingJob Job { get; }
    public ProcessingJobEventArgs(ProcessingJob job) => Job = job;
}

public class JobStatistics
{
    public int QueuedCount { get; set; }
    public int ActiveCount { get; set; }
    public int CompletedCount { get; set; }
    public int MaxConcurrency { get; set; }
    public List<ProcessingJob> ActiveJobs { get; set; } = new();
    public List<ProcessingJob> RecentCompleted { get; set; } = new();
}
```

### Phase 2: Update MainWindow Integration

#### Step 2.1: Replace Direct Processing with Queue

**Before:**
```csharp
private async void FolderMonitorJobService_FileDetected(object? sender, FolderFileDetectedEventArgs e)
{
    var result = await _bookletProcessor.ProcessBookletsWorkflowAsync(...);
}
```

**After:**
```csharp
private ConcurrentProcessingService _concurrentProcessor;

private void InitializeConcurrentProcessor()
{
    // Load configuration
    var maxConcurrency = int.TryParse(_config["BookletProcessor:MaxConcurrency"], out var mc) 
        ? mc : 4; // Default to 4 concurrent jobs
    
    _concurrentProcessor = new ConcurrentProcessingService(
        maxConcurrency,
        // Factories create new instances for each job
        pdfServiceFactory: () => new PdfService(),
        deskewerFactory: () => new Deskewer(),
        alignerFactory: () => new ImageAlignerAlt(),
        redPixelRemoverFactory: () => _enableRedPixelRemover ? new RedPixelRemoverService() : null,
        qrScannerFactory: () => _enableQrScanning ? new RegionQrScanner() : null
    );
    
    // Wire up events
    _concurrentProcessor.JobStarted += OnJobStarted;
    _concurrentProcessor.JobCompleted += OnJobCompleted;
    _concurrentProcessor.JobFailed += OnJobFailed;
}

private void FolderMonitorJobService_FileDetected(object? sender, FolderFileDetectedEventArgs e)
{
    // Just enqueue - no waiting
    var jobId = _concurrentProcessor.EnqueueJob(
        e.FilePath,
        e.TemplateFilePath,
        e.OutputFolder);
    
    // Show queued notification
    new ToastContentBuilder()
        .AddText("File Detected")
        .AddText($"Queued for processing: {Path.GetFileName(e.FilePath)}")
        .Show(toast => { toast.ExpirationTime = DateTime.Now.AddSeconds(3); });
}

private void OnJobStarted(object? sender, ProcessingJobEventArgs e)
{
    Dispatcher.Invoke(() =>
    {
        new ToastContentBuilder()
            .AddText("Processing Started")
            .AddText($"File: {Path.GetFileName(e.Job.InputFilePath)}")
            .Show(toast => { toast.ExpirationTime = DateTime.Now.AddSeconds(3); });
    });
}

private void OnJobCompleted(object? sender, ProcessingJobEventArgs e)
{
    Dispatcher.Invoke(() =>
    {
        var duration = e.Job.CompletedTime!.Value - e.Job.StartedTime!.Value;
        new ToastContentBuilder()
            .AddText("Processing Complete")
            .AddText($"{Path.GetFileName(e.Job.InputFilePath)} - {duration:mm\\:ss}")
            .AddText($"Output: {e.Job.Result?.OutputPath}")
            .Show(toast => { toast.ExpirationTime = DateTime.Now.AddSeconds(5); });
    });
}

private void OnJobFailed(object? sender, ProcessingJobEventArgs e)
{
    Dispatcher.Invoke(() =>
    {
        new ToastContentBuilder()
            .AddText("Processing Failed")
            .AddText($"{Path.GetFileName(e.Job.InputFilePath)}")
            .AddText($"Error: {e.Job.ErrorMessage}")
            .Show(toast => { toast.ExpirationTime = DateTime.Now.AddSeconds(10); });
    });
}
```

### Phase 3: Add Monitoring Dashboard (Optional)

Create `ProcessingQueueWindow.xaml`:

```xaml
<Window x:Class="TestBookletProcessor.WPF.ProcessingQueueWindow"
        Title="Processing Queue Monitor" Height="600" Width="900">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        
        <!-- Statistics Panel -->
        <Border Grid.Row="0" Background="#f0f0f0" Padding="10" Margin="5">
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="Queued: " FontWeight="Bold"/>
                <TextBlock x:Name="QueuedCountText" Text="0" Margin="0,0,20,0"/>
                
                <TextBlock Text="Active: " FontWeight="Bold"/>
                <TextBlock x:Name="ActiveCountText" Text="0" Foreground="Green" Margin="0,0,20,0"/>
                
                <TextBlock Text="Completed: " FontWeight="Bold"/>
                <TextBlock x:Name="CompletedCountText" Text="0" Margin="0,0,20,0"/>
                
                <TextBlock Text="Max Concurrency: " FontWeight="Bold"/>
                <TextBlock x:Name="MaxConcurrencyText" Text="4" Margin="0,0,20,0"/>
            </StackPanel>
        </Border>
        
        <!-- Job List -->
        <TabControl Grid.Row="1" Margin="5">
            <TabItem Header="Active Jobs">
                <DataGrid x:Name="ActiveJobsGrid" AutoGenerateColumns="False" IsReadOnly="True">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="File" Binding="{Binding InputFileName}" Width="*"/>
                        <DataGridTextColumn Header="Status" Binding="{Binding Status}" Width="100"/>
                        <DataGridTextColumn Header="Started" Binding="{Binding StartedTime, StringFormat={}{0:HH:mm:ss}}" Width="100"/>
                        <DataGridTextColumn Header="Duration" Binding="{Binding Duration}" Width="100"/>
                    </DataGrid.Columns>
                </DataGrid>
            </TabItem>
            
            <TabItem Header="Completed Jobs">
                <DataGrid x:Name="CompletedJobsGrid" AutoGenerateColumns="False" IsReadOnly="True">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="File" Binding="{Binding InputFileName}" Width="*"/>
                        <DataGridTextColumn Header="Status" Binding="{Binding Status}" Width="100"/>
                        <DataGridTextColumn Header="Duration" Binding="{Binding Duration}" Width="100"/>
                        <DataGridTextColumn Header="Output" Binding="{Binding OutputPath}" Width="*"/>
                    </DataGrid.Columns>
                </DataGrid>
            </TabItem>
        </TabControl>
        
        <!-- Action Buttons -->
        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="5">
            <Button Content="Refresh" Width="80" Height="28" Margin="5" Click="Refresh_Click"/>
            <Button Content="Close" Width="80" Height="28" Margin="5" Click="Close_Click"/>
        </StackPanel>
    </Grid>
</Window>
```

### Phase 4: Configuration

Add to `appsettings.json`:

```json
{
  "BookletProcessor": {
    "MaxConcurrency": 4,
    "JobQueueTimeout": 30,
    "FileReadyWaitSeconds": 3,
    // ... existing config
  }
}
```

**Recommended Concurrency Levels:**
- **2-4 cores**: MaxConcurrency = 2
- **4-8 cores**: MaxConcurrency = 4
- **8-16 cores**: MaxConcurrency = 6-8
- **16+ cores**: MaxConcurrency = 8-12

## Thread Safety Analysis

### Service Thread Safety

#### ? **Thread-Safe (Can Share)**
- `PdfService` - Uses file I/O, inherently isolated per file
- `Deskewer` - Stateless image operations
- `ImageAligner` - Stateless alignment operations
- `RedPixelRemoverService` - Stateless pixel operations

#### ?? **Unknown/Potentially Unsafe (Create New Instance)**
- `RegionQrScanner` - Unknown internal state
- **Recommendation**: Create new instance per job (safer)

#### ? **Already Isolated**
- Temp folders: Unique per job ?
- Output files: Unique filenames ?
- Image processing: In-memory, isolated ?

### Solution: Factory Pattern

**Strategy**: Create fresh service instances for each job

```csharp
// Instead of:
private readonly RegionQrScanner _qrScanner; // Shared - risky

// Use:
private Func<RegionQrScanner> _qrScannerFactory = () => new RegionQrScanner(); // New per job
```

## Performance Projections

### Current (Sequential Processing)

| Files | Avg Time/File | Total Time |
|-------|---------------|------------|
| 1 | 45s | 45s |
| 10 | 45s | 7.5 min |
| 20 | 45s | 15 min |

### Proposed (Concurrent, MaxConcurrency=4)

| Files | Avg Time/File | Concurrent Time | Speedup |
|-------|---------------|-----------------|---------|
| 1 | 45s | 45s | 1x |
| 4 | 45s | 45s | 4x |
| 10 | 45s | ~2 min | 3.75x |
| 20 | 45s | ~4 min | 3.75x |

**Key Benefits:**
- 10 files: 7.5 min ? 2 min (75% faster)
- 20 files: 15 min ? 4 min (73% faster)
- First file completes in 45s regardless of queue size
- Continuous throughput instead of sequential delays

## Risk Assessment & Mitigation

### Risk 1: Memory Exhaustion
**Problem**: Processing 10 large PDFs simultaneously could use 8-10 GB RAM

**Mitigation:**
- Limit MaxConcurrency to 4-8
- Monitor memory usage
- Implement memory threshold check before starting jobs
- Consider adding memory-based concurrency adjustment

### Risk 2: Disk I/O Saturation
**Problem**: Multiple concurrent writes could saturate disk

**Mitigation:**
- Temp folders already on separate paths (good)
- Use SSD for temp folders if possible
- Monitor disk queue length
- Consider async I/O patterns (already using async/await)

### Risk 3: QR Scanner Thread Safety
**Problem**: Unknown if RegionQrScanner is thread-safe

**Mitigation:**
- Create new instance per job (factory pattern)
- Test concurrent scanning
- Add lock if issues found
- Consider scanning libraries' thread-safety docs

### Risk 4: File Name Collisions
**Problem**: Two files with same name processed concurrently

**Mitigation:**
- Already have unique temp folders ?
- Output filename includes input name (unique from scanner)
- Secondary QR scan adds dynamic naming
- If collision still possible, add timestamp suffix

### Risk 5: Error Handling
**Problem**: One job crash affects others

**Mitigation:**
- Each job in isolated try-catch
- Errors logged per job
- Failed jobs don't block queue
- Cleanup in finally blocks ? (already implemented)

## Testing Strategy

### Unit Tests
1. Test queue with single job
2. Test queue with 2 concurrent jobs
3. Test queue with 10 jobs, MaxConcurrency=4
4. Test job failure doesn't affect others
5. Test cancellation

### Integration Tests
1. Drop 20 files simultaneously
2. Verify all processed
3. Verify correct output
4. Verify no file corruption
5. Monitor resource usage

### Stress Tests
1. 100 files dropped
2. Verify queue doesn't overflow
3. Verify memory stays reasonable
4. Verify disk I/O acceptable
5. System remains responsive

## Migration Path

### Phase 1: Implementation (No Breaking Changes)
1. Create `ConcurrentProcessingService`
2. Create `ProcessingJob` model
3. Add to project
4. Unit test in isolation

### Phase 2: Integration (Side-by-Side)
1. Add concurrent processor to MainWindow
2. Keep old code path as fallback
3. Add toggle in config: `"UseConcurrentProcessing": true`
4. Test with production data

### Phase 3: Monitoring
1. Add queue monitoring window
2. Log metrics (queue size, duration, errors)
3. Monitor resource usage
4. Tune MaxConcurrency

### Phase 4: Full Deployment
1. Remove old sequential code
2. Set concurrent as default
3. Update documentation
4. Train users on new behavior

## Configuration Tuning Guide

### Finding Optimal MaxConcurrency

**Test Process:**
1. Start with MaxConcurrency = 2
2. Drop 20 test files
3. Monitor:
   - CPU usage (should be 60-80%)
   - Memory usage (should be < 80% total RAM)
   - Disk queue (should be < 10)
   - Total processing time

4. Increase MaxConcurrency by 2
5. Repeat test
6. Stop when:
   - Processing time stops improving
   - CPU reaches 90%+
   - Memory reaches 80%+
   - System becomes sluggish

**Example Results:**
- **4-core, 8GB RAM, HDD**: MaxConcurrency = 2-3
- **8-core, 16GB RAM, SSD**: MaxConcurrency = 4-6
- **16-core, 32GB RAM, NVMe**: MaxConcurrency = 8-12

## Console Output Examples

### Sequential (Current)
```
File detected: School1_001.pdf
Processing School1_001.pdf... (45s)
? Complete

File detected: School2_001.pdf
Processing School2_001.pdf... (45s)
? Complete

Total: 90 seconds for 2 files
```

### Concurrent (Proposed)
```
[Queue] Job abc123 queued: School1_001.pdf
[Queue] Job def456 queued: School2_001.pdf
[Queue] Job ghi789 queued: School3_001.pdf
[Queue] Jobs in queue: 3, Active: 0

[Job abc123] Starting: School1_001.pdf
[Job def456] Starting: School2_001.pdf
[Queue] Jobs in queue: 1, Active: 2

[Job abc123] ? Completed in 00:45
[Job ghi789] Starting: School3_001.pdf
[Job def456] ? Completed in 00:47
[Job ghi789] ? Completed in 00:44

Total: 91 seconds for 3 files (2x speedup)
```

## Success Criteria

1. ? Process 20 files in < 5 minutes (vs 15 minutes currently)
2. ? No file corruption or data loss
3. ? No race conditions or deadlocks
4. ? Memory usage stays reasonable (< 80% RAM)
5. ? System remains responsive during processing
6. ? All files processed successfully
7. ? Error isolation (one failure doesn't affect others)
8. ? Queue monitoring and visibility
9. ? Configurable concurrency limits
10. ? Graceful shutdown and cleanup

## Next Steps

1. **Review & Approve Plan**
   - Confirm approach
   - Confirm MaxConcurrency defaults
   - Confirm monitoring requirements

2. **Implementation Phase 1** (Core)
   - Create `ProcessingJob` model
   - Create `ConcurrentProcessingService`
   - Unit tests

3. **Implementation Phase 2** (Integration)
   - Update MainWindow
   - Wire up events
   - Add configuration

4. **Implementation Phase 3** (Testing)
   - Test with 2, 4, 10, 20 files
   - Monitor resources
   - Tune concurrency

5. **Implementation Phase 4** (Polish)
   - Add queue monitoring window
   - Update documentation
   - Deploy

## Estimated Timeline

- **Phase 1 (Core)**: 4-6 hours
- **Phase 2 (Integration)**: 2-3 hours
- **Phase 3 (Testing)**: 2-3 hours
- **Phase 4 (Polish)**: 2-3 hours

**Total**: 10-15 hours for complete implementation

---

## Summary

This plan transforms your sequential file processing into a robust concurrent system that:

? **Processes 4 files simultaneously** (configurable)  
? **Reduces 20-file processing from 15 min to ~4 min** (73% faster)  
? **Maintains thread safety** with isolated service instances  
? **Prevents resource exhaustion** with bounded concurrency  
? **Handles errors gracefully** with per-job isolation  
? **Provides monitoring** with real-time queue statistics  
? **Configurable** via appsettings.json  
? **Backwards compatible** with existing architecture  

**Ready to implement?** Let me know if you want me to proceed with the implementation or if you have any questions or adjustments to the plan!
