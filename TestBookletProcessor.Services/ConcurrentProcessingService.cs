using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;

namespace TestBookletProcessor.Services;

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
    private readonly BookletProcessorOptions _options;
    private readonly ILoggingService? _loggingService;

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
    /// <param name="options">Processing settings; MaxConcurrency bounds the number of parallel jobs.</param>
    /// <param name="loggingService">Optional logging service for job events.</param>
    public ConcurrentProcessingService(BookletProcessorOptions options, ILoggingService? loggingService = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _loggingService = loggingService;
        _maxConcurrency = Math.Max(1, options.MaxConcurrency);
        _concurrencyLimiter = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);

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
                }, CancellationToken.None);
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

        // Determine processing mode
        string processingMode = (!string.IsNullOrEmpty(_options.ScannedSheets.TemplateName) &&
                                Path.GetFileName(job.TemplateFilePath).Equals(_options.ScannedSheets.TemplateName, StringComparison.OrdinalIgnoreCase))
            ? "ScannedSheets"
            : "Booklet";

        // Log job started
        if (_loggingService != null)
        {
            await _loggingService.LogJobStartedAsync(
                job,
                _options.DefaultDpi,
                _options.EnableRedPixelRemover,
                _options.QrScanner.EnableQrScanning,
                processingMode);
        }

        try
        {
            // Wait for file to be fully written (important for FileSystemWatcher);
            // throws if the file never becomes readable
            await WaitForFileReadyAsync(job.InputFilePath, cancellationToken);

            // Fresh service instances per job keep managed state isolated across parallel jobs
            var bookletProcessor = ProcessorFactory.CreateBookletProcessor(_options, _loggingService);

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

                // Log job completed
                if (_loggingService != null)
                {
                    await _loggingService.LogJobCompletedAsync(
                        job,
                        _options.DefaultDpi,
                        _options.EnableRedPixelRemover,
                        _options.QrScanner.EnableQrScanning,
                        processingMode);
                }

                JobCompleted?.Invoke(this, new ProcessingJobEventArgs(job));
            }
            else
            {
                Console.WriteLine($"[Job {job.JobId:N}] ? Failed: {result.ErrorMessage}");

                // Log job failed
                if (_loggingService != null)
                {
                    await _loggingService.LogJobFailedAsync(
                        job,
                        _options.DefaultDpi,
                        _options.EnableRedPixelRemover,
                        _options.QrScanner.EnableQrScanning,
                        processingMode);
                }

                JobFailed?.Invoke(this, new ProcessingJobEventArgs(job));
            }
        }
        catch (Exception ex)
        {
            job.Status = ProcessingJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedTime = DateTime.Now;

            Console.WriteLine($"[Job {job.JobId:N}] ? Exception: {ex}");

            // Log job failed with full exception detail
            if (_loggingService != null)
            {
                await _loggingService.LogErrorAsync($"Job {job.JobId:N} failed for '{job.InputFilePath}'", ex);
                await _loggingService.LogJobFailedAsync(
                    job,
                    _options.DefaultDpi,
                    _options.EnableRedPixelRemover,
                    _options.QrScanner.EnableQrScanning,
                    processingMode);
            }

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
    /// Waits for a file to be fully written and ready for processing. Files often arrive
    /// through slow channels (scanner spooling, Dropbox sync), so this waits generously
    /// and fails the job rather than processing a partially written file.
    /// </summary>
    private static async Task WaitForFileReadyAsync(string filePath, CancellationToken cancellationToken)
    {
        const int maxAttempts = 120;
        const int delayMs = 500; // 60 seconds max

        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                // Try to open file exclusively
                using (File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
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

        throw new TimeoutException(
            $"File was still locked or missing after {maxAttempts * delayMs / 1000} seconds: {filePath}. " +
            "It may still be copying; it will be processed if it is detected again.");
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
    /// Stops the background processor. In-flight jobs are allowed to finish;
    /// queued jobs that have not started are abandoned.
    /// </summary>
    public void Dispose()
    {
        Console.WriteLine("[ConcurrentProcessor] Disposing...");
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        // The semaphore is intentionally not disposed: in-flight jobs still Release() it
        // as they finish, and SemaphoreSlim holds no unmanaged resources unless its
        // wait handle was accessed (it never is here).
        Console.WriteLine("[ConcurrentProcessor] Disposed");
    }
}
