using Microsoft.Extensions.Configuration;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
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
        private BookletProcessorService _bookletProcessor;
        private IConfigurationRoot _config;
        private byte _redThreshold;
        private bool _enableRedPixelRemover;
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

            _bookletProcessor = new BookletProcessorService(
                _pdfService,
                _deskewer,
                _aligner,
                _redPixelRemover,
                _redThreshold,
                _enableRedPixelRemover,
                dpi);

            Console.WriteLine($"Red pixel remover enabled: {_enableRedPixelRemover}");

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

        private async void FolderMonitorJobService_FileDetected(object? sender, FolderFileDetectedEventArgs e)
        {
            new ToastContentBuilder()
            .AddText("Alignment Started")
            .AddText($"Aligning detected file: {e.FilePath} with template: {e.TemplateFilePath}")
            .Show(toast =>
            {
                toast.ExpirationTime = DateTime.Now.AddSeconds(5);
            });
            //MessageBox.Show($"Aligning detected file:\n{e.FilePath}\nwith template:\n{e.TemplateFilePath}", "Alignment Started", MessageBoxButton.OK, MessageBoxImage.Information);
            // add a toast message here instead of message box

            // Use detected file and template for full booklet processing
            var result = await _bookletProcessor.ProcessBookletsWorkflowAsync(
                e.FilePath,
                e.TemplateFilePath,
                e.OutputFolder,
                null);
            //Dispatcher.Invoke(() =>
            //{
            if (result.Success)
            {
                var message = $"Processing complete! {result.PagesProcessed} booklets processed in {result.ProcessingTime:mm\\:ss}. Output: {result.OutputPath}";

                new ToastContentBuilder()
                    .AddText("Alignment Complete")
                    .AddText(message)
                    .Show(toast =>
                    {
                        toast.ExpirationTime = DateTime.Now.AddSeconds(5);
                    });
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error: {result.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
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
                // Re-enable buttons and hide progress bar
                ProcessButton.IsEnabled = true;
                BrowseInputButton.IsEnabled = true;
                BrowseTemplateButton.IsEnabled = true;
                ProcessingProgressBar.Visibility = Visibility.Collapsed;
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
            // Re-enable buttons and hide progress bar
            ProcessButton.IsEnabled = true;
            BrowseInputButton.IsEnabled = true;
            BrowseTemplateButton.IsEnabled = true;
            ProcessingProgressBar.Visibility = Visibility.Collapsed;

            if (result.Success)
            {
                StatusTextBlock.Text = $"Processing complete! {result.PagesProcessed} booklets processed in {result.ProcessingTime.ToString(@"mm\:ss")}. Output: {result.OutputPath}";
                ProcessingProgressBar.Value = totalBooklets;
            }
            else
            {
                StatusTextBlock.Text = $"Error: {result.ErrorMessage}";
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

                // Recreate the booklet processor with new settings
                _bookletProcessor = new BookletProcessorService(
                    _pdfService,
                    _deskewer,
                    _aligner,
                    _redPixelRemover,
                    _redThreshold,
                    _enableRedPixelRemover,
                    dpi);

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
