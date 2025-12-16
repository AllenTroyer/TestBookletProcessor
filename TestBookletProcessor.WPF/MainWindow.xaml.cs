using Microsoft.Extensions.Configuration;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private bool _enableRedPixelRemover; // Used to decide whether to pass _redPixelRemover to BookletProcessorService
        private IFolderMonitorJobService _folderMonitorJobService;
        private string _tempFolder;

        public MainWindow()
        {
            InitializeComponent();
            // Removed duplicate AUMID call - already set in App.xaml.cs
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
                    secondaryQrScanConfig);
            }

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

            Console.WriteLine($"Red pixel remover enabled: {_enableRedPixelRemover}");
            Console.WriteLine($"QR scanning enabled: {enableQrScanning}");
            
            // Initialize concurrent processor for folder monitoring
            var maxConcurrency = int.TryParse(_config["BookletProcessor:MaxConcurrency"], out var mc) ? mc : 4;
            var concurrentConfig = new ConcurrentProcessingConfig
            {
                RedThreshold = _redThreshold,
                Dpi = dpi,
                EnableRedPixelRemover = _enableRedPixelRemover,
                EnableQrScanning = enableQrScanning,
                QrRegionXInches = qrXInches,
                QrRegionYInches = qrYInches,
                QrRegionWidthInches = qrWidthInches,
                QrRegionHeightInches = qrHeightInches,
                QrValuesExcludingRedRemoval = qrValues,
                TemplateExclusionPatterns = templateExclusionPatterns,
                ScannedSheetQrMapping = scannedSheetQrMapping,
                RedPixelExclusionRegions = redPixelExclusionRegions,
                SecondaryQrScanConfig = secondaryQrScanConfig,
                ScannedSheetTemplateName = scannedSheetTemplateName
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

                if (result.Success)
                {
                    StatusTextBlock.Text = $"Processing complete! {result.PagesProcessed} booklets processed in {result.ProcessingTime.ToString(@"mm\:ss")}. Output: {result.OutputPath}";
                    ProcessingProgressBar.Value = totalBooklets;
                }
                else
                {
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
                        secondaryQrScanConfig);
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
    }
}
