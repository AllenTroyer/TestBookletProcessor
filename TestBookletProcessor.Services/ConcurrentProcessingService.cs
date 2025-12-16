using QrRegionScanner;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;

namespace TestBookletProcessor.Services;

/// <summary>
/// Configuration for concurrent processing service.
/// </summary>
public class ConcurrentProcessingConfig
{
    public byte RedThreshold { get; set; } = 200;
    public int Dpi { get; set; } = 300;
    public bool EnableRedPixelRemover { get; set; } = true;
    public bool EnableQrScanning { get; set; } = true;
    public double QrRegionXInches { get; set; } = 7.0;
    public double QrRegionYInches { get; set; } = 9.5;
    public double QrRegionWidthInches { get; set; } = 1.5;
    public double QrRegionHeightInches { get; set; } = 1.5;
    public List<string> QrValuesExcludingRedRemoval { get; set; } = new();
    public List<string> TemplateExclusionPatterns { get; set; } = new();
    public Dictionary<string, int> ScannedSheetQrMapping { get; set; } = new();
    public List<RedPixelExclusionRegion> RedPixelExclusionRegions { get; set; } = new();
    public SecondaryQrScanConfig? SecondaryQrScanConfig { get; set; }
    public string? ScannedSheetTemplateName { get; set; }
}

/// <summary>
/// Service for managing concurrent processing of multiple document files.
/// Provides queue management, bounded concurrency, and isolated service instances per job.
/// </summary>
public class ConcurrentProcessingService : IDisposable
{
    private readonly ConcurrentQueue<ProcessingJob> _jobQueue = new();
    private readonly ConcurrentDictionary<Guid, ProcessingJob> _activeJobs = new();
    private readonly ConcurrentDictionary<Guid, ProcessingJob> _completedJobs = new();
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly int _maxConcurrency;
    private readonly ConcurrentProcessingConfig _config;
    
    /// <summary>
    /// Event raised when a job starts processing.
    /// </summary>
    public event EventHandler<ProcessingJobEventArgs>? JobStarted;
    
    /// <summary>
    /// Event raised when a job completes successfully.
    /// </summary>
    public event EventHandler<ProcessingJobEventArgs>? JobCompleted;
    
    /// <summary>
    /// Event raised when a job fails.
    /// </summary>
    public event EventHandler<ProcessingJobEventArgs>? JobFailed;
    
    /// <summary>
    /// Initializes a new instance of the ConcurrentProcessingService.
    /// </summary>
    /// <param name="config">Configuration for processing settings.</param>
    /// <param name="maxConcurrency">Maximum number of concurrent jobs (default: 4).</param>
    public ConcurrentProcessingService(ConcurrentProcessingConfig config, int maxConcurrency = 4)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _maxConcurrency = maxConcurrency;
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        
        Console.WriteLine($"[ConcurrentProcessor] Initialized with MaxConcurrency={_maxConcurrency}");
        
        // Start background processing loop
        Task.Run(() => ProcessQueueAsync(_cancellationTokenSource.Token));
    }
    
    /// <summary>
    /// Enqueues a job for processing.
    /// </summary>
    /// <param name="inputFile">Path to the input PDF file.</param>
    /// <param name="templateFile">Path to the template PDF file.</param>
    /// <param name="outputFolder">Path to the output folder.</param>
    /// <returns>The unique job ID.</returns>
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
    
    /// <summary>
    /// Background loop that processes jobs from the queue.
    /// </summary>
    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[Queue] Background processor started");
        
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
                
                // Process job in background task
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
                Console.WriteLine("[Queue] Background processor cancelled");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Queue] Error in queue processor: {ex.Message}");
            }
        }
        
        Console.WriteLine("[Queue] Background processor stopped");
    }
    
    /// <summary>
    /// Processes a single job with isolated service instances.
    /// </summary>
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
            
            // Create isolated service instances for this job (thread safety)
            var pdfService = new PdfService();
            var deskewer = new Deskewer();
            var aligner = new ImageAlignerAlt();
            var redPixelRemover = _config.EnableRedPixelRemover ? new RedPixelRemoverService() : null;
            var qrScanner = _config.EnableQrScanning ? new RegionQrScanner() : null;
            
            // Create scanned sheet processor with isolated instances
            IScannedSheetProcessor? scannedSheetProcessor = null;
            if (!string.IsNullOrEmpty(_config.ScannedSheetTemplateName))
            {
                scannedSheetProcessor = new ScannedSheetProcessorService(
                    pdfService,
                    deskewer,
                    aligner,
                    redPixelRemover,
                    _config.RedThreshold,
                    qrScanner,
                    _config.EnableQrScanning,
                    _config.QrRegionXInches,
                    _config.QrRegionYInches,
                    _config.QrRegionWidthInches,
                    _config.QrRegionHeightInches,
                    _config.Dpi,
                    _config.QrValuesExcludingRedRemoval,
                    _config.RedPixelExclusionRegions,
                    _config.SecondaryQrScanConfig);
            }
            
            // Create booklet processor with isolated instances
            var bookletProcessor = new BookletProcessorService(
                pdfService,
                deskewer,
                aligner,
                redPixelRemover,
                _config.RedThreshold,
                _config.Dpi,
                qrScanner,
                _config.EnableQrScanning,
                _config.QrRegionXInches,
                _config.QrRegionYInches,
                _config.QrRegionWidthInches,
                _config.QrRegionHeightInches,
                _config.QrValuesExcludingRedRemoval,
                _config.TemplateExclusionPatterns,
                scannedSheetProcessor,
                _config.ScannedSheetTemplateName,
                _config.ScannedSheetQrMapping);
            
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
                var duration = job.CompletedTime.Value - job.StartedTime.Value;
                Console.WriteLine($"[Job {job.JobId:N}] ? Completed in {duration:mm\\:ss}");
                Console.WriteLine($"[Job {job.JobId:N}] Output: {result.OutputPath}");
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
    
    /// <summary>
    /// Waits for a file to be fully written and ready for processing.
    /// </summary>
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
            catch (IOException) // Catches both IOException and FileNotFoundException
            {
                // File still being written or doesn't exist yet
                await Task.Delay(delayMs, cancellationToken);
            }
        }
        
        Console.WriteLine($"[Warning] File may still be writing after 3 seconds: {filePath}");
    }
    
    /// <summary>
    /// Gets current queue statistics.
    /// </summary>
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
    
    /// <summary>
    /// Disposes of resources and stops the background processor.
    /// </summary>
    public void Dispose()
    {
        Console.WriteLine("[ConcurrentProcessor] Disposing...");
        _cancellationTokenSource.Cancel();
        _concurrencyLimiter.Dispose();
        _cancellationTokenSource.Dispose();
        Console.WriteLine("[ConcurrentProcessor] Disposed");
    }
}
