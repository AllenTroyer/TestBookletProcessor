using Microsoft.Extensions.Configuration;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using QrRegionScanner;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;
using TestBookletProcessor.Services;

namespace TestBookletProcessor.WPF
{
    public partial class MainWindow : Window
    {
        private readonly IPdfService _pdfService = new PdfService();
        private readonly IDeskewer _deskewer = new Deskewer();
        private readonly IImageAligner _aligner = new ImageAlignerAlt();
        private readonly IRedPixelRemoverService _redPixelRemover = new RedPixelRemoverService();
        private readonly RegionQrScanner _qrScanner = new RegionQrScanner();
        private BookletProcessorService _bookletProcessor;
        private ConcurrentProcessingService? _concurrentProcessor;
        private IConfigurationRoot _config;
        private byte _redThreshold;
        private bool _enableRedPixelRemover;
        private IFolderMonitorJobService _folderMonitorJobService;
        private string _tempFolder;
        private ILoggingService _loggingService;
        private int _dpi;
        private bool _enableQrScanning;
        private string? _scannedSheetTemplateName;

        public MainWindow()
        {
            InitializeComponent();
            
            _config = ConfigurationHelper.LoadConfiguration();
            
            // Initialize logging service
            _loggingService = CreateLoggingService(_config);
            
            // Log application start
            var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            var maxConcurrency = int.TryParse(_config["BookletProcessor:MaxConcurrency"], out var mc) ? mc : 4;
            _ = _loggingService.LogApplicationStartAsync(appVersion, maxConcurrency);
            
            // Log config file location for troubleshooting
            var configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            Console.WriteLine($"Using configuration file: {configFilePath}");
            
            // Removed duplicate AUMID call - already set in App.xaml.cs
            var thresholdStr = _config?["BookletProcessor:RedPixelThreshold"];
            _redThreshold = byte.TryParse(thresholdStr, out var val) ? val : (byte)200;
            var enableRedStr = _config?["BookletProcessor:EnableRedPixelRemover"];
            _enableRedPixelRemover = enableRedStr != null && enableRedStr.Equals("true", StringComparison.OrdinalIgnoreCase);

            // Get DPI setting
            var dpiStr = _config?["BookletProcessor:DefaultDpi"];
            _dpi = int.TryParse(dpiStr, out var dpiVal) ? dpiVal : 300;

            // Load QR scanner configuration
            var enableQrStr = _config?["BookletProcessor:QrScanner:EnableQrScanning"];
            _enableQrScanning = enableQrStr != null && enableQrStr.Equals("true", StringComparison.OrdinalIgnoreCase);
            
            double qrXInches = double.TryParse(_config?["BookletProcessor:QrScanner:QrRegionXInches"], out var xi) ? xi : 6.5;
            double qrYInches = double.TryParse(_config?["BookletProcessor:QrScanner:QrRegionYInches"], out var yi) ? yi : 9.0;
            double qrWidthInches = double.TryParse(_config?["BookletProcessor:QrScanner:QrRegionWidthInches"], out var wi) ? wi : 2.0;
            double qrHeightInches = double.TryParse(_config?["BookletProcessor:QrScanner:QrRegionHeightInches"], out var hi) ? hi : 2.0;
            
            var qrValuesSection = _config?.GetSection("BookletProcessor:QrScanner:QrValuesExcludingRedRemoval");
            var qrValues = qrValuesSection?.GetChildren().Select(c => c.Value ?? "").ToList() ?? 
                          new List<string> { "MACHINE_SCORED", "NO_RED_INK", "CLEAN" };
            
            // Load Template Exclusion Patterns
            var templateExclusionSection = _config?.GetSection("BookletProcessor:TemplateExclusionPatterns");
            var templateExclusionPatterns = templateExclusionSection?.GetChildren().Select(c => c.Value ?? "").ToList() ??
                          new List<string> { "*TEMPLATE*", "*BLANK*", "*SAMPLE*" };
            
            // Load Scanned Sheet Configuration
            _scannedSheetTemplateName = _config?["BookletProcessor:ScannedSheets:TemplateName"];
            var scannedSheetQrMappingSection = _config?.GetSection("BookletProcessor:ScannedSheets:QrToPageMapping");
            var scannedSheetQrMapping = new Dictionary<string, int>();
            if (scannedSheetQrMappingSection != null)
            {
                foreach (var child in scannedSheetQrMappingSection.GetChildren())
                {
                    if (child.Key != null && int.TryParse(child.Value, out var pageIndex))
                    {
                        scannedSheetQrMapping[child.Key] = pageIndex;
                    }
                }
            }
            
            // Load Red Pixel Exclusion Regions
            var exclusionRegionsSection = _config?.GetSection("BookletProcessor:RedPixelExclusionRegions");
            var redPixelExclusionRegions = new List<RedPixelExclusionRegion>();
            if (exclusionRegionsSection != null)
            {
                foreach (var child in exclusionRegionsSection.GetChildren())
                {
                    var region = new RedPixelExclusionRegion
                    {
                        Name = child["Name"] ?? "",
                        XInches = double.TryParse(child["XInches"], out var x) ? x : 0,
                        YInches = double.TryParse(child["YInches"], out var y) ? y : 0,
                        WidthInches = double.TryParse(child["WidthInches"], out var w) ? w : 0,
                        HeightInches = double.TryParse(child["HeightInches"], out var h) ? h : 0
                    };
                    
                    var patternsSection = child.GetSection("QrCodePatterns");
                    if (patternsSection != null)
                    {
                        region.QrCodePatterns = patternsSection.GetChildren()
                            .Select(c => c.Value ?? "")
                            .Where(v => !string.IsNullOrEmpty(v))
                            .ToList();
                    }
                    
                    if (region.QrCodePatterns.Any())
                    {
                        redPixelExclusionRegions.Add(region);
                    }
                }
            }
            
            if (redPixelExclusionRegions.Any())
            {
                Console.WriteLine($"Loaded {redPixelExclusionRegions.Count} red pixel exclusion region(s)");
                foreach (var region in redPixelExclusionRegions)
                {
                    Console.WriteLine($"  - {region.Name}: QR patterns: {string.Join(", ", region.QrCodePatterns)}");
                }
            }
            
            // Load Secondary QR Scan Configuration
            SecondaryQrScanConfig? secondaryQrScanConfig = null;
            var secondaryQrSection = _config?.GetSection("BookletProcessor:ScannedSheets:SecondaryQrScan");
            if (secondaryQrSection != null && secondaryQrSection.Exists())
            {
                secondaryQrScanConfig = new SecondaryQrScanConfig
                {
                    TriggerQrCode = secondaryQrSection["TriggerQrCode"] ?? "CHECKLISTQR-01",
                    RegionXInches = double.TryParse(secondaryQrSection["RegionXInches"], out var sx) ? sx : 0.0,
                    RegionYInches = double.TryParse(secondaryQrSection["RegionYInches"], out var sy) ? sy : 0.75,
                    RegionWidthInches = double.TryParse(secondaryQrSection["RegionWidthInches"], out var sw) ? sw : 2.0,
                    RegionHeightInches = double.TryParse(secondaryQrSection["RegionHeightInches"], out var sh) ? sh : 1.0,
                    FileNameReplacementPattern = secondaryQrSection["FileNameReplacementPattern"] ?? "SchoolCityState",
                    RenameInputFiles = secondaryQrSection["RenameInputFiles"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true,
                    ArchiveFolder = secondaryQrSection["ArchiveFolder"] ?? @"C:\Users\allen\Dropbox\Data\Catforms\Scans\TestScans\ToArchive"
                };
                
                Console.WriteLine($"Secondary QR scan configured:");
                Console.WriteLine($"  Trigger QR: {secondaryQrScanConfig.TriggerQrCode}");
                Console.WriteLine($"  Region: ({secondaryQrScanConfig.RegionXInches}\", {secondaryQrScanConfig.RegionYInches}\") " +
                                  $"{secondaryQrScanConfig.RegionWidthInches}\" × {secondaryQrScanConfig.RegionHeightInches}\"");
                Console.WriteLine($"  Replacement pattern: {secondaryQrScanConfig.FileNameReplacementPattern}");
                Console.WriteLine($"  Rename input files: {secondaryQrScanConfig.RenameInputFiles}");
                if (secondaryQrScanConfig.RenameInputFiles)
                {
                    Console.WriteLine($"  Archive folder: {secondaryQrScanConfig.ArchiveFolder}");
                }
            }
            
            // Load Raw Form Extraction Configuration
            RawFormExtractionConfig? rawFormExtractionConfig = null;
            var rawFormSection = _config?.GetSection("BookletProcessor:ScannedSheets:RawFormExtraction");
            if (rawFormSection != null && rawFormSection.Exists())
            {
                rawFormExtractionConfig = new RawFormExtractionConfig
                {
                    Enabled = rawFormSection["Enabled"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true,
                    ExtractToSeparateFolder = rawFormSection["ExtractToSeparateFolder"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false,
                    ExtractionFolder = rawFormSection["ExtractionFolder"] ?? @"C:\TestBooklets\Output\RawForms",
                    FileNameSuffix = rawFormSection["FileNameSuffix"] ?? "RawForm",
                    IncludePageNumberInFileName = rawFormSection["IncludePageNumberInFileName"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true,
                    SkipRedRemoval = rawFormSection["SkipRedRemoval"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true
                };

                var triggerQrSection = rawFormSection.GetSection("TriggerQrCodes");
                if (triggerQrSection != null)
                {
                    rawFormExtractionConfig.TriggerQrCodes = triggerQrSection.GetChildren()
                        .Select(c => c.Value ?? "")
                        .Where(v => !string.IsNullOrEmpty(v))
                        .ToList();
                }

                if (rawFormExtractionConfig.Enabled)
                {
                    Console.WriteLine($"Raw form extraction enabled:");
                    Console.WriteLine($"  Trigger QR codes: {string.Join(", ", rawFormExtractionConfig.TriggerQrCodes)}");
                    Console.WriteLine($"  Skip red removal: {rawFormExtractionConfig.SkipRedRemoval}");
                }
            }
            
            // Create scanned sheet processor
            IScannedSheetProcessor? scannedSheetProcessor = null;
            if (!string.IsNullOrEmpty(_scannedSheetTemplateName))
            {
                scannedSheetProcessor = new ScannedSheetProcessorService(
                    _pdfService,
                    _deskewer,
                    _aligner,
                    _enableRedPixelRemover ? _redPixelRemover : null,
                    _redThreshold,
                    _enableQrScanning ? _qrScanner : null,
                    _enableQrScanning,
                    qrXInches,
                    qrYInches,
                    qrWidthInches,
                    qrHeightInches,
                    _dpi,
                    qrValues,
                    redPixelExclusionRegions,
                    secondaryQrScanConfig,
                    rawFormExtractionConfig);
            }

            _bookletProcessor = new BookletProcessorService(
                _pdfService,
                _deskewer,
                _aligner,
                _enableRedPixelRemover ? _redPixelRemover : null,
                _redThreshold,
                _dpi,
                _enableQrScanning ? _qrScanner : null,
                _enableQrScanning,
                qrXInches,
                qrYInches,
                qrWidthInches,
                qrHeightInches,
                qrValues,
                templateExclusionPatterns,
                scannedSheetProcessor,
                _scannedSheetTemplateName,
                scannedSheetQrMapping);

            Console.WriteLine($"Red pixel remover enabled: {_enableRedPixelRemover}");
            Console.WriteLine($"QR scanning enabled: {_enableQrScanning}");
            
            // Initialize concurrent processor for folder monitoring
            var concurrentConfig = new ConcurrentProcessingConfig
            {
                RedThreshold = _redThreshold,
                Dpi = _dpi,
                EnableRedPixelRemover = _enableRedPixelRemover,
                EnableQrScanning = _enableQrScanning,
                QrRegionXInches = qrXInches,
                QrRegionYInches = qrYInches,
                QrRegionWidthInches = qrWidthInches,
                QrRegionHeightInches = qrHeightInches,
                QrValuesExcludingRedRemoval = qrValues,
                TemplateExclusionPatterns = templateExclusionPatterns,
                ScannedSheetQrMapping = scannedSheetQrMapping,
                RedPixelExclusionRegions = redPixelExclusionRegions,
                SecondaryQrScanConfig = secondaryQrScanConfig,
                RawFormExtractionConfig = rawFormExtractionConfig,
                ScannedSheetTemplateName = _scannedSheetTemplateName,
                LoggingService = _loggingService
            };
            _concurrentProcessor = new ConcurrentProcessingService(concurrentConfig, maxConcurrency);
            _concurrentProcessor.JobStarted += ConcurrentProcessor_JobStarted;
            _concurrentProcessor.JobCompleted += ConcurrentProcessor_JobCompleted;
            _concurrentProcessor.JobFailed += ConcurrentProcessor_JobFailed;
            Console.WriteLine($"Concurrent processor initialized with MaxConcurrency={maxConcurrency}");

            // Set default folders from config
            InputPdfTextBox.Text = _config["BookletProcessor:DefaultInputFolder"];
            TemplatePdfTextBox.Text = _config["BookletProcessor:DefaultTemplateFolder"];

            _tempFolder = _config["BookletProcessor:TempFolder"] ?? Path.GetTempPath();
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            _folderMonitorJobService = new FolderMonitorJobService(configPath);
            _folderMonitorJobService.FileDetected += FolderMonitorJobService_FileDetected;
            LoadFolderMonitorJobsFromConfig();
        }

        private void LoadFolderMonitorJobsFromConfig()
        {
            var jobsSection = _config.GetSection("MonitoredFolders");
            foreach (var job in jobsSection.GetChildren())
            {
                var folder = job["InputFolder"];
                var template = job["TemplateFile"];
                var output = job["OutputFolder"];
                if (!string.IsNullOrWhiteSpace(folder) && !string.IsNullOrWhiteSpace(template) && !string.IsNullOrWhiteSpace(output))
                {
                    _folderMonitorJobService.AddJob(folder, template, output);
                }
            }
        }

        private void FolderMonitorJobService_FileDetected(object? sender, FolderFileDetectedEventArgs e)
        {
            // Enqueue job for concurrent processing (non-blocking)
            if (_concurrentProcessor != null)
            {
                var jobId = _concurrentProcessor.EnqueueJob(
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
            var defaultInputFolder = _config["BookletProcessor:DefaultInputFolder"];
            var dlg = new OpenFileDialog { Filter = "PDF files (*.pdf)|*.pdf" };
            if (!string.IsNullOrWhiteSpace(defaultInputFolder) && Directory.Exists(defaultInputFolder))
            {
                dlg.InitialDirectory = defaultInputFolder;
            }
            if (dlg.ShowDialog(this) == true)
            {
                InputPdfTextBox.Text = dlg.FileName;
            }
        }

        private void BrowseTemplatePdf_Click(object sender, RoutedEventArgs e)
        {
            var defaultTemplateFolder = _config["BookletProcessor:DefaultTemplateFolder"];
            var dlg = new OpenFileDialog { Filter = "PDF files (*.pdf)|*.pdf" };
            if (!string.IsNullOrWhiteSpace(defaultTemplateFolder) && Directory.Exists(defaultTemplateFolder))
            {
                dlg.InitialDirectory = defaultTemplateFolder;
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
                    Status = ProcessingJobStatus.Processing
                };
                
                job.StartedTime = DateTime.Now;
                
                // Determine processing mode
                string processingMode = (!string.IsNullOrEmpty(_scannedSheetTemplateName) && 
                                        Path.GetFileName(templatePdf).Equals(_scannedSheetTemplateName, StringComparison.OrdinalIgnoreCase))
                    ? "ScannedSheets"
                    : "Booklet";
                
                // Log job started
                await _loggingService.LogJobStartedAsync(job, _dpi, _enableRedPixelRemover, _enableQrScanning, processingMode);

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
                    await _loggingService.LogJobCompletedAsync(job, _dpi, _enableRedPixelRemover, _enableQrScanning, processingMode);
                    StatusTextBlock.Text = $"Processing complete! {result.PagesProcessed} booklets processed in {result.ProcessingTime.ToString(@"mm\:ss")}. Output: {result.OutputPath}";
                    ProcessingProgressBar.Value = totalBooklets;
                }
                else
                {
                    await _loggingService.LogJobFailedAsync(job, _dpi, _enableRedPixelRemover, _enableQrScanning, processingMode);
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

        private ILoggingService CreateLoggingService(IConfigurationRoot config)
        {
            var loggingConfig = new LoggingServiceConfig();
            
            var logDir = config["Logging:LogDirectory"];
            if (!string.IsNullOrWhiteSpace(logDir))
            {
                loggingConfig.LogDirectory = logDir;
            }
            
            var format = config["Logging:Format"];
            if (Enum.TryParse<LogFormat>(format, true, out var logFormat))
            {
                loggingConfig.Format = logFormat;
            }
            
            var enableRotation = config["Logging:EnableLogRotation"];
            if (bool.TryParse(enableRotation, out var rotation))
            {
                loggingConfig.EnableLogRotation = rotation;
            }
            
            var maxSize = config["Logging:MaxLogFileSizeBytes"];
            if (long.TryParse(maxSize, out var size))
            {
                loggingConfig.MaxLogFileSizeBytes = size;
            }
            
            var maxFiles = config["Logging:MaxLogFiles"];
            if (int.TryParse(maxFiles, out var files))
            {
                loggingConfig.MaxLogFiles = files;
            }
            
            return new LoggingService(loggingConfig);
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.Topmost = true;
            var result = settingsWindow.ShowDialog();
            if (result == true)
            {
                // Reload configuration and update UI
                _config = ConfigurationHelper.LoadConfiguration();
                var thresholdStr = _config?["BookletProcessor:RedPixelThreshold"];
                _redThreshold = byte.TryParse(thresholdStr, out var val) ? val : (byte)200;
                var enableRedStr = _config?["BookletProcessor:EnableRedPixelRemover"];
                _enableRedPixelRemover = enableRedStr != null && enableRedStr.Equals("true", StringComparison.OrdinalIgnoreCase);

                // Get DPI setting
                var dpiStr = _config?["BookletProcessor:DefaultDpi"];
                var dpi = int.TryParse(dpiStr, out var dpiVal) ? dpiVal : 300;

                // Load QR scanner configuration
                var enableQrStr = _config?["BookletProcessor:QrScanner:EnableQrScanning"];
                bool enableQrScanning = enableQrStr != null && enableQrStr.Equals("true", StringComparison.OrdinalIgnoreCase);
                
                double qrXInches = double.TryParse(_config?["BookletProcessor:QrScanner:QrRegionXInches"], out var xi) ? xi : 6.5;
                double qrYInches = double.TryParse(_config?["BookletProcessor:QrScanner:QrRegionYInches"], out var yi) ? yi : 9.0;
                double qrWidthInches = double.TryParse(_config?["BookletProcessor:QrScanner:QrRegionWidthInches"], out var wi) ? wi : 2.0;
                double qrHeightInches = double.TryParse(_config?["BookletProcessor:QrScanner:QrRegionHeightInches"], out var hi) ? hi : 2.0;
                
                var qrValuesSection = _config?.GetSection("BookletProcessor:QrScanner:QrValuesExcludingRedRemoval");
                var qrValues = qrValuesSection?.GetChildren().Select(c => c.Value ?? "").ToList() ?? 
                              new List<string> { "MACHINE_SCORED", "NO_RED_INK", "CLEAN" };
                
                // Load Template Exclusion Patterns
                var templateExclusionSection = _config?.GetSection("BookletProcessor:TemplateExclusionPatterns");
                var templateExclusionPatterns = templateExclusionSection?.GetChildren().Select(c => c.Value ?? "").ToList() ??
                              new List<string> { "*TEMPLATE*", "*BLANK*", "*SAMPLE*" };
                
                // Load Scanned Sheet Configuration
                var scannedSheetTemplateName = _config?["BookletProcessor:ScannedSheets:TemplateName"];
                var scannedSheetQrMappingSection = _config?.GetSection("BookletProcessor:ScannedSheets:QrToPageMapping");
                var scannedSheetQrMapping = new Dictionary<string, int>();
                if (scannedSheetQrMappingSection != null)
                {
                    foreach (var child in scannedSheetQrMappingSection.GetChildren())
                    {
                        if (child.Key != null && int.TryParse(child.Value, out var pageIndex))
                        {
                            scannedSheetQrMapping[child.Key] = pageIndex;
                        }
                    }
                }
                
                // Load Red Pixel Exclusion Regions
                var exclusionRegionsSection = _config?.GetSection("BookletProcessor:RedPixelExclusionRegions");
                var redPixelExclusionRegions = new List<RedPixelExclusionRegion>();
                if (exclusionRegionsSection != null)
                {
                    foreach (var child in exclusionRegionsSection.GetChildren())
                    {
                        var region = new RedPixelExclusionRegion
                        {
                            Name = child["Name"] ?? "",
                            XInches = double.TryParse(child["XInches"], out var x) ? x : 0,
                            YInches = double.TryParse(child["YInches"], out var y) ? y : 0,
                            WidthInches = double.TryParse(child["WidthInches"], out var w) ? w : 0,
                            HeightInches = double.TryParse(child["HeightInches"], out var h) ? h : 0
                        };
                        
                        var patternsSection = child.GetSection("QrCodePatterns");
                        if (patternsSection != null)
                        {
                            region.QrCodePatterns = patternsSection.GetChildren()
                                .Select(c => c.Value ?? "")
                                .Where(v => !string.IsNullOrEmpty(v))
                                .ToList();
                        }
                        
                        if (region.QrCodePatterns.Any())
                        {
                            redPixelExclusionRegions.Add(region);
                        }
                    }
                }
                
                // Load Secondary QR Scan Configuration
                SecondaryQrScanConfig? secondaryQrScanConfig = null;
                var secondaryQrSection = _config?.GetSection("BookletProcessor:ScannedSheets:SecondaryQrScan");
                if (secondaryQrSection != null && secondaryQrSection.Exists())
                {
                    secondaryQrScanConfig = new SecondaryQrScanConfig
                    {
                        TriggerQrCode = secondaryQrSection["TriggerQrCode"] ?? "CHECKLISTQR-01",
                        RegionXInches = double.TryParse(secondaryQrSection["RegionXInches"], out var sx) ? sx : 0.0,
                        RegionYInches = double.TryParse(secondaryQrSection["RegionYInches"], out var sy) ? sy : 0.75,
                        RegionWidthInches = double.TryParse(secondaryQrSection["RegionWidthInches"], out var sw) ? sw : 2.0,
                        RegionHeightInches = double.TryParse(secondaryQrSection["RegionHeightInches"], out var sh) ? sh : 1.0,
                        FileNameReplacementPattern = secondaryQrSection["FileNameReplacementPattern"] ?? "SchoolCityState",
                        RenameInputFiles = secondaryQrSection["RenameInputFiles"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true,
                        ArchiveFolder = secondaryQrSection["ArchiveFolder"] ?? @"C:\Users\allen\Dropbox\Data\Catforms\Scans\TestScans\ToArchive"
                    };
                }
                
                // Load Raw Form Extraction Configuration
                RawFormExtractionConfig? rawFormExtractionConfig = null;
                var rawFormSection = _config?.GetSection("BookletProcessor:ScannedSheets:RawFormExtraction");
                if (rawFormSection != null && rawFormSection.Exists())
                {
                    rawFormExtractionConfig = new RawFormExtractionConfig
                    {
                        Enabled = rawFormSection["Enabled"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true,
                        ExtractToSeparateFolder = rawFormSection["ExtractToSeparateFolder"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false,
                        ExtractionFolder = rawFormSection["ExtractionFolder"] ?? @"C:\TestBooklets\Output\RawForms",
                        FileNameSuffix = rawFormSection["FileNameSuffix"] ?? "RawForm",
                        IncludePageNumberInFileName = rawFormSection["IncludePageNumberInFileName"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true,
                        SkipRedRemoval = rawFormSection["SkipRedRemoval"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true
                    };

                    var triggerQrSection = rawFormSection.GetSection("TriggerQrCodes");
                    if (triggerQrSection != null)
                    {
                        rawFormExtractionConfig.TriggerQrCodes = triggerQrSection.GetChildren()
                            .Select(c => c.Value ?? "")
                            .Where(v => !string.IsNullOrEmpty(v))
                            .ToList();
                    }

                    if (rawFormExtractionConfig.Enabled)
                    {
                        Console.WriteLine($"Raw form extraction enabled:");
                        Console.WriteLine($"  Trigger QR codes: {string.Join(", ", rawFormExtractionConfig.TriggerQrCodes)}");
                        Console.WriteLine($"  Skip red removal: {rawFormExtractionConfig.SkipRedRemoval}");
                    }
                }
                
                // Create scanned sheet processor
                IScannedSheetProcessor? scannedSheetProcessor = null;
                if (!string.IsNullOrEmpty(scannedSheetTemplateName))
                {
                    scannedSheetProcessor = new ScannedSheetProcessorService(
                        _pdfService,
                        _deskewer,
                        _aligner,
                        _enableRedPixelRemover ? _redPixelRemover : null,
                        _redThreshold,
                        enableQrScanning ? _qrScanner : null,
                        enableQrScanning,
                        qrXInches,
                        qrYInches,
                        qrWidthInches,
                        qrHeightInches,
                        dpi,
                        qrValues,
                        redPixelExclusionRegions,
                        secondaryQrScanConfig,
                        rawFormExtractionConfig);
                }

                // Recreate the booklet processor with new settings
                _bookletProcessor = new BookletProcessorService(
                    _pdfService,
                    _deskewer,
                    _aligner,
                    _enableRedPixelRemover ? _redPixelRemover : null,
                    _redThreshold,
                    dpi,
                    enableQrScanning ? _qrScanner : null,
                    enableQrScanning,
                    qrXInches,
                    qrYInches,
                    qrWidthInches,
                    qrHeightInches,
                    qrValues,
                    templateExclusionPatterns,
                    scannedSheetProcessor,
                    scannedSheetTemplateName,
                    scannedSheetQrMapping);

                InputPdfTextBox.Text = _config["BookletProcessor:DefaultInputFolder"];
                TemplatePdfTextBox.Text = _config["BookletProcessor:DefaultTemplateFolder"];
                // Optionally update other UI elements if needed
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
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                
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
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                var folder = Path.GetDirectoryName(configPath);
                
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
            // Log application stop
            _ = _loggingService.LogApplicationStopAsync();
            base.OnClosed(e);
        }
    }
}
