using System;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Models;

namespace TestBookletProcessor.Core.Interfaces;

/// <summary>
/// Service for logging application and job processing events.
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// Logs an application start event.
    /// </summary>
    /// <param name="applicationVersion">Application version.</param>
    /// <param name="maxConcurrency">Maximum concurrency setting.</param>
    Task LogApplicationStartAsync(string? applicationVersion = null, int? maxConcurrency = null);
    
    /// <summary>
    /// Logs an application stop event.
    /// </summary>
    Task LogApplicationStopAsync();
    
    /// <summary>
    /// Logs a job started event.
    /// </summary>
    /// <param name="job">The processing job that started.</param>
    /// <param name="dpi">DPI setting used.</param>
    /// <param name="redPixelRemovalEnabled">Whether red pixel removal is enabled.</param>
    /// <param name="qrScanningEnabled">Whether QR scanning is enabled.</param>
    /// <param name="processingMode">Processing mode (e.g., "Booklet", "ScannedSheets").</param>
    Task LogJobStartedAsync(
        ProcessingJob job,
        int? dpi = null,
        bool? redPixelRemovalEnabled = null,
        bool? qrScanningEnabled = null,
        string? processingMode = null);
    
    /// <summary>
    /// Logs a job completed event.
    /// </summary>
    /// <param name="job">The completed processing job.</param>
    /// <param name="dpi">DPI setting used.</param>
    /// <param name="redPixelRemovalEnabled">Whether red pixel removal was enabled.</param>
    /// <param name="qrScanningEnabled">Whether QR scanning was enabled.</param>
    /// <param name="processingMode">Processing mode (e.g., "Booklet", "ScannedSheets").</param>
    Task LogJobCompletedAsync(
        ProcessingJob job,
        int? dpi = null,
        bool? redPixelRemovalEnabled = null,
        bool? qrScanningEnabled = null,
        string? processingMode = null);
    
    /// <summary>
    /// Logs a job failed event.
    /// </summary>
    /// <param name="job">The failed processing job.</param>
    /// <param name="dpi">DPI setting used.</param>
    /// <param name="redPixelRemovalEnabled">Whether red pixel removal was enabled.</param>
    /// <param name="qrScanningEnabled">Whether QR scanning was enabled.</param>
    /// <param name="processingMode">Processing mode (e.g., "Booklet", "ScannedSheets").</param>
    Task LogJobFailedAsync(
        ProcessingJob job,
        int? dpi = null,
        bool? redPixelRemovalEnabled = null,
        bool? qrScanningEnabled = null,
        string? processingMode = null);
    
    /// <summary>
    /// Logs a non-fatal warning message.
    /// </summary>
    /// <param name="message">Description of the problem.</param>
    Task LogWarningAsync(string message);

    /// <summary>
    /// Logs an error with optional full exception detail.
    /// </summary>
    /// <param name="message">Description of the error.</param>
    /// <param name="exception">The exception, logged with stack trace when provided.</param>
    Task LogErrorAsync(string message, Exception? exception = null);

    /// <summary>
    /// Logs a custom log entry.
    /// </summary>
    /// <param name="entry">The log entry to write.</param>
    Task LogEntryAsync(LogEntry entry);
}
