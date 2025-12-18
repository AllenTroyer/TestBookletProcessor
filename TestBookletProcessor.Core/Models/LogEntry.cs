using System;

namespace TestBookletProcessor.Core.Models;

/// <summary>
/// Type of log entry.
/// </summary>
public enum LogEntryType
{
    /// <summary>
    /// Application started.
    /// </summary>
    ApplicationStart,
    
    /// <summary>
    /// Application stopped.
    /// </summary>
    ApplicationStop,
    
    /// <summary>
    /// Job processing started.
    /// </summary>
    JobStarted,
    
    /// <summary>
    /// Job completed successfully.
    /// </summary>
    JobCompleted,
    
    /// <summary>
    /// Job failed with an error.
    /// </summary>
    JobFailed
}

/// <summary>
/// Represents a log entry for application and job processing events.
/// </summary>
public class LogEntry
{
    /// <summary>
    /// Gets or sets the timestamp when this entry was created.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Gets or sets the type of log entry.
    /// </summary>
    public LogEntryType EntryType { get; set; }
    
    /// <summary>
    /// Gets or sets the machine name where the processing occurred.
    /// </summary>
    public string MachineName { get; set; } = Environment.MachineName;
    
    /// <summary>
    /// Gets or sets the application version.
    /// </summary>
    public string? ApplicationVersion { get; set; }
    
    // Application-level properties
    
    /// <summary>
    /// Gets or sets the max concurrency setting.
    /// </summary>
    public int? MaxConcurrency { get; set; }
    
    // Job-level properties
    
    /// <summary>
    /// Gets or sets the unique job identifier.
    /// </summary>
    public Guid? JobId { get; set; }
    
    /// <summary>
    /// Gets or sets the full path to the input file.
    /// </summary>
    public string? InputFilePath { get; set; }
    
    /// <summary>
    /// Gets or sets the input filename without path.
    /// </summary>
    public string? InputFileName { get; set; }
    
    /// <summary>
    /// Gets or sets the size of the input file in bytes.
    /// </summary>
    public long? InputFileSizeBytes { get; set; }
    
    /// <summary>
    /// Gets or sets the full path to the template file.
    /// </summary>
    public string? TemplateFilePath { get; set; }
    
    /// <summary>
    /// Gets or sets the template filename without path.
    /// </summary>
    public string? TemplateFileName { get; set; }
    
    /// <summary>
    /// Gets or sets the full path to the output file.
    /// </summary>
    public string? OutputFilePath { get; set; }
    
    /// <summary>
    /// Gets or sets the output filename without path.
    /// </summary>
    public string? OutputFileName { get; set; }
    
    /// <summary>
    /// Gets or sets the size of the output file in bytes.
    /// </summary>
    public long? OutputFileSizeBytes { get; set; }
    
    /// <summary>
    /// Gets or sets when the job was queued.
    /// </summary>
    public DateTime? QueuedTime { get; set; }
    
    /// <summary>
    /// Gets or sets when the job started processing.
    /// </summary>
    public DateTime? StartedTime { get; set; }
    
    /// <summary>
    /// Gets or sets when the job completed.
    /// </summary>
    public DateTime? CompletedTime { get; set; }
    
    /// <summary>
    /// Gets or sets the elapsed processing time.
    /// </summary>
    public TimeSpan? ElapsedTime { get; set; }
    
    /// <summary>
    /// Gets or sets whether the job succeeded.
    /// </summary>
    public bool? Success { get; set; }
    
    /// <summary>
    /// Gets or sets the error message if the job failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Gets or sets the number of pages processed.
    /// </summary>
    public int? PagesProcessed { get; set; }
    
    /// <summary>
    /// Gets or sets the DPI setting used for processing.
    /// </summary>
    public int? Dpi { get; set; }
    
    /// <summary>
    /// Gets or sets whether red pixel removal was enabled.
    /// </summary>
    public bool? RedPixelRemovalEnabled { get; set; }
    
    /// <summary>
    /// Gets or sets whether QR scanning was enabled.
    /// </summary>
    public bool? QrScanningEnabled { get; set; }
    
    /// <summary>
    /// Gets or sets the processing mode (e.g., "Booklet", "ScannedSheets").
    /// </summary>
    public string? ProcessingMode { get; set; }
    
    /// <summary>
    /// Gets the processing rate in pages per minute.
    /// </summary>
    public double? PagesPerMinute
    {
        get
        {
            if (PagesProcessed.HasValue && ElapsedTime.HasValue && ElapsedTime.Value.TotalMinutes > 0)
            {
                return PagesProcessed.Value / ElapsedTime.Value.TotalMinutes;
            }
            return null;
        }
    }
    
    /// <summary>
    /// Gets the average time per page in seconds.
    /// </summary>
    public double? SecondsPerPage
    {
        get
        {
            if (PagesProcessed.HasValue && PagesProcessed.Value > 0 && ElapsedTime.HasValue)
            {
                return ElapsedTime.Value.TotalSeconds / PagesProcessed.Value;
            }
            return null;
        }
    }
}
