using System;

namespace TestBookletProcessor.Core.Models;

/// <summary>
/// Status of a processing job in the concurrent processing queue.
/// </summary>
public enum ProcessingJobStatus
{
    /// <summary>
    /// Job is waiting in the queue to be processed.
    /// </summary>
    Queued,
    
    /// <summary>
    /// Job is currently being processed.
    /// </summary>
    Processing,
    
    /// <summary>
    /// Job completed successfully.
    /// </summary>
    Completed,
    
    /// <summary>
    /// Job failed with an error.
    /// </summary>
    Failed,
    
    /// <summary>
    /// Job was cancelled before completion.
    /// </summary>
    Cancelled
}

/// <summary>
/// Represents a document processing job in the concurrent processing queue.
/// </summary>
public class ProcessingJob
{
    /// <summary>
    /// Gets or sets the unique identifier for this job.
    /// </summary>
    public Guid JobId { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Gets or sets the full path to the input PDF file.
    /// </summary>
    public string InputFilePath { get; set; } = "";
    
    /// <summary>
    /// Gets or sets the full path to the template PDF file.
    /// </summary>
    public string TemplateFilePath { get; set; } = "";
    
    /// <summary>
    /// Gets or sets the output folder path where processed files will be saved.
    /// </summary>
    public string OutputFolder { get; set; } = "";
    
    /// <summary>
    /// Gets or sets the time when the job was added to the queue.
    /// </summary>
    public DateTime QueuedTime { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Gets or sets the time when the job started processing.
    /// </summary>
    public DateTime? StartedTime { get; set; }
    
    /// <summary>
    /// Gets or sets the time when the job completed (successfully or with error).
    /// </summary>
    public DateTime? CompletedTime { get; set; }
    
    /// <summary>
    /// Gets or sets the current status of the job.
    /// </summary>
    public ProcessingJobStatus Status { get; set; } = ProcessingJobStatus.Queued;
    
    /// <summary>
    /// Gets or sets the error message if the job failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Gets or sets the processing result once the job completes.
    /// </summary>
    public ProcessingResult? Result { get; set; }
    
    /// <summary>
    /// Gets the input filename without path.
    /// </summary>
    public string InputFileName => System.IO.Path.GetFileName(InputFilePath);
    
    /// <summary>
    /// Gets the output path from the result, if available.
    /// </summary>
    public string? OutputPath => Result?.OutputPath;
    
    /// <summary>
    /// Gets the processing duration, if the job has started.
    /// </summary>
    public TimeSpan? Duration
    {
        get
        {
            if (!StartedTime.HasValue)
                return null;
            
            var endTime = CompletedTime ?? DateTime.Now;
            return endTime - StartedTime.Value;
        }
    }
}
