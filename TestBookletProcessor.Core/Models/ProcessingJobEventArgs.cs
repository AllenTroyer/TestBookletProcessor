using System;

namespace TestBookletProcessor.Core.Models;

/// <summary>
/// Event arguments for processing job events (started, completed, failed).
/// </summary>
public class ProcessingJobEventArgs : EventArgs
{
    /// <summary>
    /// Gets the processing job associated with this event.
    /// </summary>
    public ProcessingJob Job { get; }
    
    /// <summary>
    /// Initializes a new instance of the ProcessingJobEventArgs class.
    /// </summary>
    /// <param name="job">The processing job.</param>
    public ProcessingJobEventArgs(ProcessingJob job)
    {
        Job = job ?? throw new ArgumentNullException(nameof(job));
    }
}
