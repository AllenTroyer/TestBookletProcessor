using System.Collections.Generic;

namespace TestBookletProcessor.Core.Models;

/// <summary>
/// Statistics about the concurrent processing queue and job execution.
/// </summary>
public class JobStatistics
{
    /// <summary>
    /// Gets or sets the number of jobs currently in the queue waiting to be processed.
    /// </summary>
    public int QueuedCount { get; set; }
    
    /// <summary>
    /// Gets or sets the number of jobs currently being processed.
    /// </summary>
    public int ActiveCount { get; set; }
    
    /// <summary>
    /// Gets or sets the total number of completed jobs (including failed jobs).
    /// </summary>
    public int CompletedCount { get; set; }
    
    /// <summary>
    /// Gets or sets the maximum number of concurrent jobs that can run simultaneously.
    /// </summary>
    public int MaxConcurrency { get; set; }
    
    /// <summary>
    /// Gets or sets the list of currently active (processing) jobs.
    /// </summary>
    public List<ProcessingJob> ActiveJobs { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the list of recently completed jobs (up to 20 most recent).
    /// </summary>
    public List<ProcessingJob> RecentCompleted { get; set; } = new();
}
