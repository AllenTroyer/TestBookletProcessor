using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;
using TestBookletProcessor.Services;

namespace TestBookletProcessor.WPF
{
    public partial class MainWindow : Window
    {
        private readonly ILoggingService _loggingService;
        private readonly IFolderMonitorJobService _folderMonitorJobService;
        private BookletProcessorOptions _options = new();
        private BookletProcessorService _bookletProcessor = null!;
        private ConcurrentProcessingService? _concurrentProcessor;

        public MainWindow()
        {
            InitializeComponent();

            Console.WriteLine($"Using configuration file: {AppConfig.ConfigFilePath}");
            var appOptions = AppConfig.Load();

            _loggingService = new LoggingService(appOptions.Logging);

            var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            _ = _loggingService.LogApplicationStartAsync(appVersion, appOptions.BookletProcessor.MaxConcurrency);

            ApplySettings(appOptions.BookletProcessor);

            // The service loads its jobs from the MonitoredFolders config section itself
            _folderMonitorJobService = new FolderMonitorJobService(AppConfig.ConfigFilePath);
            _folderMonitorJobService.FileDetected += FolderMonitorJobService_FileDetected;
        }

        /// <summary>
        /// Builds (or rebuilds) the processing pipeline from settings. Called at startup and
        /// after the settings dialog saves, so folder-monitor jobs also pick up new settings.
        /// </summary>
        private void ApplySettings(BookletProcessorOptions options)
        {
            _options = options;
            _bookletProcessor = ProcessorFactory.CreateBookletProcessor(options, _loggingService);

            if (_concurrentProcessor != null)
            {
                _concurrentProcessor.JobStarted -= ConcurrentProcessor_JobStarted;
                _concurrentProcessor.JobCompleted -= ConcurrentProcessor_JobCompleted;
                _concurrentProcessor.JobFailed -= ConcurrentProcessor_JobFailed;
                _concurrentProcessor.Dispose();
            }

            _concurrentProcessor = new ConcurrentProcessingService(options, _loggingService);
            _concurrentProcessor.JobStarted += ConcurrentProcessor_JobStarted;
            _concurrentProcessor.JobCompleted += ConcurrentProcessor_JobCompleted;
            _concurrentProcessor.JobFailed += ConcurrentProcessor_JobFailed;

            InputPdfTextBox.Text = options.DefaultInputFolder;
            TemplatePdfTextBox.Text = options.DefaultTemplateFolder;

            Console.WriteLine($"Red pixel remover enabled: {options.EnableRedPixelRemover}");
            Console.WriteLine($"QR scanning enabled: {options.QrScanner.EnableQrScanning}");
            Console.WriteLine($"Concurrent processor initialized with MaxConcurrency={options.MaxConcurrency}");
        }

        private void FolderMonitorJobService_FileDetected(object? sender, FolderFileDetectedEventArgs e)
        {
            // Enqueue job for concurrent processing (non-blocking)
            if (_concurrentProcessor != null)
            {
                _concurrentProcessor.EnqueueJob(
                    e.FilePath,
                    e.TemplateFilePath,
                    e.OutputFolder);

                // Show queued notification
                new ToastContentBuilder()
                    .AddText("File Detected")
                    .AddText($"Queued for processing: {Path.GetFileName(e.FilePath)}")
                    .Show(toast =>
                    {
                        toast.ExpirationTime = DateTime.Now.AddSeconds(3);
                    });
            }
        }

        private void ConcurrentProcessor_JobStarted(object? sender, ProcessingJobEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                new ToastContentBuilder()
                    .AddText("Processing Started")
                    .AddText($"File: {Path.GetFileName(e.Job.InputFilePath)}")
                    .Show(toast =>
                    {
                        toast.ExpirationTime = DateTime.Now.AddSeconds(3);
                    });
            });
        }

        private void ConcurrentProcessor_JobCompleted(object? sender, ProcessingJobEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var duration = e.Job.Duration ?? TimeSpan.Zero;
                var message = $"{Path.GetFileName(e.Job.InputFilePath)} completed in {duration:mm\\:ss}";
                if (e.Job.Result != null)
                {
                    message += $"\nOutput: {Path.GetFileName(e.Job.Result.OutputPath)}";
                    if (e.Job.Result.Warnings.Count > 0)
                    {
                        message += $"\n{e.Job.Result.Warnings.Count} page warning(s) - see log";
                    }
                }

                new ToastContentBuilder()
                    .AddText("Processing Complete")
                    .AddText(message)
                    .Show(toast =>
                    {
                        toast.ExpirationTime = DateTime.Now.AddSeconds(5);
                    });
            });
        }

        private void ConcurrentProcessor_JobFailed(object? sender, ProcessingJobEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                new ToastContentBuilder()
                    .AddText("Processing Failed")
                    .AddText($"{Path.GetFileName(e.Job.InputFilePath)}")
                    .AddText($"Error: {e.Job.ErrorMessage}")
                    .Show(toast =>
                    {
                        toast.ExpirationTime = DateTime.Now.AddSeconds(10);
                    });
            });
        }

        private void BrowseInputPdf_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PDF files (*.pdf)|*.pdf" };
            if (!string.IsNullOrWhiteSpace(_options.DefaultInputFolder) && Directory.Exists(_options.DefaultInputFolder))
            {
                dlg.InitialDirectory = _options.DefaultInputFolder;
            }
            if (dlg.ShowDialog(this) == true)
            {
                InputPdfTextBox.Text = dlg.FileName;
            }
        }

        private void BrowseTemplatePdf_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PDF files (*.pdf)|*.pdf" };
            if (!string.IsNullOrWhiteSpace(_options.DefaultTemplateFolder) && Directory.Exists(_options.DefaultTemplateFolder))
            {
                dlg.InitialDirectory = _options.DefaultTemplateFolder;
            }
            if (dlg.ShowDialog(this) == true)
            {
                TemplatePdfTextBox.Text = dlg.FileName;
            }
        }

        private async void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable buttons and show progress bar
                ProcessButton.IsEnabled = false;
                BrowseInputButton.IsEnabled = false;
                BrowseTemplateButton.IsEnabled = false;
                ProcessingProgressBar.Visibility = Visibility.Visible;
                ProcessingProgressBar.Value = 0;

                string inputPdf = InputPdfTextBox.Text;
                string templatePdf = TemplatePdfTextBox.Text;
                if (!File.Exists(inputPdf) || !File.Exists(templatePdf))
                {
                    StatusTextBlock.Text = "Please select valid input and template PDF files.";
                    return;
                }

                string outputFolder = Path.Combine(Path.GetDirectoryName(inputPdf)!, "BookletOutput");
                StatusTextBlock.Text = "Processing...";
                int totalBooklets = 0;

                // Create job for logging
                var job = new ProcessingJob
                {
                    InputFilePath = inputPdf,
                    TemplateFilePath = templatePdf,
                    OutputFolder = outputFolder,
                    Status = ProcessingJobStatus.Processing,
                    StartedTime = DateTime.Now
                };

                // Determine processing mode
                string processingMode = (!string.IsNullOrEmpty(_options.ScannedSheets.TemplateName) &&
                                        Path.GetFileName(templatePdf).Equals(_options.ScannedSheets.TemplateName, StringComparison.OrdinalIgnoreCase))
                    ? "ScannedSheets"
                    : "Booklet";

                // Log job started
                await _loggingService.LogJobStartedAsync(job, _options.DefaultDpi, _options.EnableRedPixelRemover,
                    _options.QrScanner.EnableQrScanning, processingMode);

                var result = await _bookletProcessor.ProcessBookletsWorkflowAsync(
                    inputPdf,
                    templatePdf,
                    outputFolder,
                    (current, total) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            StatusTextBlock.Text = $"Processing booklet {current} of {total}...";
                            ProcessingProgressBar.Maximum = total;
                            ProcessingProgressBar.Value = current;
                        });
                        totalBooklets = total;
                    });

                job.Result = result;
                job.Status = result.Success ? ProcessingJobStatus.Completed : ProcessingJobStatus.Failed;
                job.ErrorMessage = result.ErrorMessage;
                job.CompletedTime = DateTime.Now;

                // Log job result
                if (result.Success)
                {
                    await _loggingService.LogJobCompletedAsync(job, _options.DefaultDpi, _options.EnableRedPixelRemover,
                        _options.QrScanner.EnableQrScanning, processingMode);
                    var warningSuffix = result.Warnings.Count > 0
                        ? $" {result.Warnings.Count} page warning(s) - see log."
                        : "";
                    StatusTextBlock.Text = $"Processing complete! {result.PagesProcessed} booklets processed in {result.ProcessingTime:mm\\:ss}.{warningSuffix} Output: {result.OutputPath}";
                    ProcessingProgressBar.Value = totalBooklets;
                }
                else
                {
                    await _loggingService.LogJobFailedAsync(job, _options.DefaultDpi, _options.EnableRedPixelRemover,
                        _options.QrScanner.EnableQrScanning, processingMode);
                    StatusTextBlock.Text = $"Error: {result.ErrorMessage}";
                    ProcessingProgressBar.Value = 0;
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Processing failed: {ex.Message}";
                ProcessingProgressBar.Value = 0;
                MessageBox.Show($"An error occurred during processing:\n\n{ex.Message}", "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Always re-enable buttons and hide progress bar
                ProcessButton.IsEnabled = true;
                BrowseInputButton.IsEnabled = true;
                BrowseTemplateButton.IsEnabled = true;
                ProcessingProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow
            {
                Owner = this,
                Topmost = true
            };
            if (settingsWindow.ShowDialog() == true)
            {
                // Rebuild the whole pipeline from the saved configuration
                ApplySettings(AppConfig.Load().BookletProcessor);
            }
        }

        private void OpenFolderMonitorJobs_Click(object sender, RoutedEventArgs e)
        {
            var jobsWindow = new FolderMonitorJobsWindow(_folderMonitorJobService);
            jobsWindow.Owner = this;
            jobsWindow.ShowDialog();
        }

        private void OpenConfigFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var configPath = AppConfig.ConfigFilePath;

                if (!File.Exists(configPath))
                {
                    MessageBox.Show(
                        $"Configuration file not found at:\n{configPath}",
                        "File Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Open with default JSON editor or Notepad
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = configPath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to open configuration file:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OpenConfigFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folder = Path.GetDirectoryName(AppConfig.ConfigFilePath);

                if (!Directory.Exists(folder))
                {
                    MessageBox.Show(
                        $"Configuration folder not found at:\n{folder}",
                        "Folder Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Open folder in Windows Explorer
                System.Diagnostics.Process.Start("explorer.exe", folder!);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to open configuration folder:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _concurrentProcessor?.Dispose();
            (_folderMonitorJobService as IDisposable)?.Dispose();

            // Flush the stop entry with a bounded wait; Task.Run avoids deadlocking on the
            // dispatcher while the window is tearing down.
            try
            {
                Task.Run(() => _loggingService.LogApplicationStopAsync()).Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Losing the stop log entry must not block shutdown
            }

            base.OnClosed(e);
        }
    }
}
