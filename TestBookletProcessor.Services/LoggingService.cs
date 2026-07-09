using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;

namespace TestBookletProcessor.Services;

/// <summary>
/// Log format options.
/// </summary>
public enum LogFormat
{
    /// <summary>
    /// Comma-separated values format (easy to open in Excel).
    /// </summary>
    CSV,
    
    /// <summary>
    /// JSON format (structured data for analysis).
    /// </summary>
    JSON
}

/// <summary>
/// Configuration for the logging service.
/// </summary>
public class LoggingServiceConfig
{
    /// <summary>
    /// Gets or sets the directory where log files will be stored.
    /// </summary>
    public string LogDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TestBookletProcessor",
        "Logs");
    
    /// <summary>
    /// Gets or sets the log format.
    /// </summary>
    public LogFormat Format { get; set; } = LogFormat.CSV;
    
    /// <summary>
    /// Gets or sets whether to enable log rotation.
    /// </summary>
    public bool EnableLogRotation { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the maximum log file size in bytes before rotation (default: 10MB).
    /// </summary>
    public long MaxLogFileSizeBytes { get; set; } = 10 * 1024 * 1024;
    
    /// <summary>
    /// Gets or sets the maximum number of rotated log files to keep.
    /// </summary>
    public int MaxLogFiles { get; set; } = 10;
}

/// <summary>
/// Service for logging application and job processing events to file.
/// Thread-safe implementation with support for CSV and JSON formats.
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly LoggingServiceConfig _config;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;
    private string _currentLogFilePath;
    private bool _headerWritten;
    
    /// <summary>
    /// Initializes a new instance of the LoggingService.
    /// </summary>
    /// <param name="config">Configuration for logging service.</param>
    public LoggingService(LoggingServiceConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        // Ensure log directory exists
        Directory.CreateDirectory(_config.LogDirectory);
        
        // Initialize current log file path
        _currentLogFilePath = GetLogFilePath();
        _headerWritten = File.Exists(_currentLogFilePath);
        
        // Configure JSON serialization
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
    }
    
    /// <inheritdoc/>
    public async Task LogApplicationStartAsync(string? applicationVersion = null, int? maxConcurrency = null)
    {
        var entry = new LogEntry
        {
            EntryType = LogEntryType.ApplicationStart,
            ApplicationVersion = applicationVersion,
            MaxConcurrency = maxConcurrency
        };
        
        await LogEntryAsync(entry);
    }
    
    /// <inheritdoc/>
    public async Task LogApplicationStopAsync()
    {
        var entry = new LogEntry
        {
            EntryType = LogEntryType.ApplicationStop
        };
        
        await LogEntryAsync(entry);
    }
    
    /// <inheritdoc/>
    public async Task LogJobStartedAsync(
        ProcessingJob job,
        int? dpi = null,
        bool? redPixelRemovalEnabled = null,
        bool? qrScanningEnabled = null,
        string? processingMode = null)
    {
        var entry = CreateJobLogEntry(job, LogEntryType.JobStarted, dpi, redPixelRemovalEnabled, qrScanningEnabled, processingMode);
        await LogEntryAsync(entry);
    }
    
    /// <inheritdoc/>
    public async Task LogJobCompletedAsync(
        ProcessingJob job,
        int? dpi = null,
        bool? redPixelRemovalEnabled = null,
        bool? qrScanningEnabled = null,
        string? processingMode = null)
    {
        var entry = CreateJobLogEntry(job, LogEntryType.JobCompleted, dpi, redPixelRemovalEnabled, qrScanningEnabled, processingMode);
        await LogEntryAsync(entry);
    }
    
    /// <inheritdoc/>
    public async Task LogJobFailedAsync(
        ProcessingJob job,
        int? dpi = null,
        bool? redPixelRemovalEnabled = null,
        bool? qrScanningEnabled = null,
        string? processingMode = null)
    {
        var entry = CreateJobLogEntry(job, LogEntryType.JobFailed, dpi, redPixelRemovalEnabled, qrScanningEnabled, processingMode);
        await LogEntryAsync(entry);
    }
    
    /// <inheritdoc/>
    public async Task LogWarningAsync(string message)
    {
        await LogEntryAsync(new LogEntry
        {
            EntryType = LogEntryType.Warning,
            ErrorMessage = message
        });
    }

    /// <inheritdoc/>
    public async Task LogErrorAsync(string message, Exception? exception = null)
    {
        await LogEntryAsync(new LogEntry
        {
            EntryType = LogEntryType.Error,
            ErrorMessage = exception == null ? message : $"{message} | {exception}"
        });
    }

    /// <inheritdoc/>
    public async Task LogEntryAsync(LogEntry entry)
    {
        await _writeLock.WaitAsync();
        try
        {
            // Roll over to a new file when the date changes (the app can run for days)
            var expectedPath = GetLogFilePath();
            if (!string.Equals(expectedPath, _currentLogFilePath, StringComparison.OrdinalIgnoreCase))
            {
                _currentLogFilePath = expectedPath;
                _headerWritten = File.Exists(expectedPath);
            }

            // Check for log rotation
            if (_config.EnableLogRotation && File.Exists(_currentLogFilePath))
            {
                var fileInfo = new FileInfo(_currentLogFilePath);
                if (fileInfo.Length >= _config.MaxLogFileSizeBytes)
                {
                    RotateLogFile();
                }
            }
            
            // Write the log entry
            if (_config.Format == LogFormat.CSV)
            {
                await WriteCsvEntryAsync(entry);
            }
            else
            {
                await WriteJsonEntryAsync(entry);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }
    
    private LogEntry CreateJobLogEntry(
        ProcessingJob job,
        LogEntryType entryType,
        int? dpi,
        bool? redPixelRemovalEnabled,
        bool? qrScanningEnabled,
        string? processingMode)
    {
        var entry = new LogEntry
        {
            EntryType = entryType,
            JobId = job.JobId,
            InputFilePath = job.InputFilePath,
            InputFileName = job.InputFileName,
            TemplateFilePath = job.TemplateFilePath,
            TemplateFileName = Path.GetFileName(job.TemplateFilePath),
            OutputFilePath = job.OutputPath,
            OutputFileName = job.OutputPath != null ? Path.GetFileName(job.OutputPath) : null,
            QueuedTime = job.QueuedTime,
            StartedTime = job.StartedTime,
            CompletedTime = job.CompletedTime,
            ElapsedTime = job.Duration,
            Success = job.Status == ProcessingJobStatus.Completed,
            ErrorMessage = job.ErrorMessage,
            PagesProcessed = job.Result?.PagesProcessed,
            Dpi = dpi,
            RedPixelRemovalEnabled = redPixelRemovalEnabled,
            QrScanningEnabled = qrScanningEnabled,
            ProcessingMode = processingMode,
            ExtractedFilesCount = job.Result?.ExtractedFiles?.Count
        };
        
        // Get file sizes if files exist
        if (File.Exists(job.InputFilePath))
        {
            entry.InputFileSizeBytes = new FileInfo(job.InputFilePath).Length;
        }
        
        if (job.OutputPath != null && File.Exists(job.OutputPath))
        {
            entry.OutputFileSizeBytes = new FileInfo(job.OutputPath).Length;
        }
        
        return entry;
    }
    
    private async Task WriteCsvEntryAsync(LogEntry entry)
    {
        // Write header if this is a new file
        if (!_headerWritten)
        {
            var header = GetCsvHeader();
            await File.AppendAllTextAsync(_currentLogFilePath, header + Environment.NewLine);
            _headerWritten = true;
        }
        
        var line = FormatCsvLine(entry);
        await File.AppendAllTextAsync(_currentLogFilePath, line + Environment.NewLine);
    }
    
    private async Task WriteJsonEntryAsync(LogEntry entry)
    {
        var json = JsonSerializer.Serialize(entry, _jsonOptions);
        await File.AppendAllTextAsync(_currentLogFilePath, json + Environment.NewLine);
    }
    
    private string GetCsvHeader()
    {
        return string.Join(",",
            "Timestamp",
            "EntryType",
            "MachineName",
            "ApplicationVersion",
            "MaxConcurrency",
            "JobId",
            "InputFilePath",
            "InputFileName",
            "InputFileSizeMB",
            "TemplateFilePath",
            "TemplateFileName",
            "OutputFilePath",
            "OutputFileName",
            "OutputFileSizeMB",
            "QueuedTime",
            "StartedTime",
            "CompletedTime",
            "ElapsedTimeSeconds",
            "Success",
            "ErrorMessage",
            "PagesProcessed",
            "Dpi",
            "RedPixelRemovalEnabled",
            "QrScanningEnabled",
            "ProcessingMode",
            "ExtractedFilesCount",
            "PagesPerMinute",
            "SecondsPerPage");
    }
    
    private string FormatCsvLine(LogEntry entry)
    {
        return string.Join(",",
            EscapeCsv(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")),
            EscapeCsv(entry.EntryType.ToString()),
            EscapeCsv(entry.MachineName),
            EscapeCsv(entry.ApplicationVersion),
            entry.MaxConcurrency?.ToString() ?? "",
            EscapeCsv(entry.JobId?.ToString()),
            EscapeCsv(entry.InputFilePath),
            EscapeCsv(entry.InputFileName),
            entry.InputFileSizeBytes.HasValue ? (entry.InputFileSizeBytes.Value / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture) : "",
            EscapeCsv(entry.TemplateFilePath),
            EscapeCsv(entry.TemplateFileName),
            EscapeCsv(entry.OutputFilePath),
            EscapeCsv(entry.OutputFileName),
            entry.OutputFileSizeBytes.HasValue ? (entry.OutputFileSizeBytes.Value / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture) : "",
            entry.QueuedTime?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "",
            entry.StartedTime?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "",
            entry.CompletedTime?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "",
            entry.ElapsedTime?.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture) ?? "",
            entry.Success?.ToString() ?? "",
            EscapeCsv(entry.ErrorMessage),
            entry.PagesProcessed?.ToString() ?? "",
            entry.Dpi?.ToString() ?? "",
            entry.RedPixelRemovalEnabled?.ToString() ?? "",
            entry.QrScanningEnabled?.ToString() ?? "",
            EscapeCsv(entry.ProcessingMode),
            entry.ExtractedFilesCount?.ToString() ?? "",
            entry.PagesPerMinute?.ToString("F2", CultureInfo.InvariantCulture) ?? "",
            entry.SecondsPerPage?.ToString("F2", CultureInfo.InvariantCulture) ?? "");
    }
    
    private string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        
        // Escape quotes and wrap in quotes if contains comma, quote, or newline
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        
        return value;
    }
    
    private string GetLogFilePath()
    {
        var extension = _config.Format == LogFormat.CSV ? "csv" : "jsonl";
        var date = DateTime.Now.ToString("yyyy-MM-dd");
        return Path.Combine(_config.LogDirectory, $"booklet-processor-{date}.{extension}");
    }
    
    private void RotateLogFile()
    {
        try
        {
            // Rename current log file with timestamp
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var extension = _config.Format == LogFormat.CSV ? "csv" : "jsonl";
            var rotatedPath = Path.Combine(
                _config.LogDirectory,
                $"booklet-processor-{timestamp}.{extension}");
            
            if (File.Exists(_currentLogFilePath))
            {
                File.Move(_currentLogFilePath, rotatedPath);
            }
            
            // Reset for new file
            _currentLogFilePath = GetLogFilePath();
            _headerWritten = false;
            
            // Clean up old log files
            CleanupOldLogFiles();
        }
        catch (Exception ex)
        {
            // Log rotation failure should not break the application
            Console.WriteLine($"[LoggingService] Failed to rotate log file: {ex.Message}");
        }
    }
    
    private void CleanupOldLogFiles()
    {
        try
        {
            var extension = _config.Format == LogFormat.CSV ? "*.csv" : "*.jsonl";
            var logFiles = Directory.GetFiles(_config.LogDirectory, extension);
            
            if (logFiles.Length > _config.MaxLogFiles)
            {
                // Sort by creation time and delete oldest files
                Array.Sort(logFiles, (a, b) => File.GetCreationTime(a).CompareTo(File.GetCreationTime(b)));
                
                int filesToDelete = logFiles.Length - _config.MaxLogFiles;
                for (int i = 0; i < filesToDelete; i++)
                {
                    File.Delete(logFiles[i]);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoggingService] Failed to cleanup old log files: {ex.Message}");
        }
    }
}
