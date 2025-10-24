using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Services;

namespace TestBookletProcessor.WPF
{
    public partial class MainWindow : Window
    {
        private readonly IPdfService _pdfService = new PdfService();
        private readonly IImageProcessor _imageProcessor = new ImageProcessor();
        private readonly BookletProcessorService _bookletProcessor;
        private IConfigurationRoot _config;

        public MainWindow()
        {
            InitializeComponent();
            _bookletProcessor = new BookletProcessorService(_pdfService, _imageProcessor);
            _config = ConfigurationHelper.LoadConfiguration();

            // Set default folders from config
            InputPdfTextBox.Text = _config["BookletProcessor:DefaultInputFolder"];
            TemplatePdfTextBox.Text = _config["BookletProcessor:DefaultTemplateFolder"];
            // You can use _config["BookletProcessor:DefaultOutputFolder"] where needed
        }

        private void BrowseInputPdf_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PDF files (*.pdf)|*.pdf" };
            if (dlg.ShowDialog() == true)
            {
                InputPdfTextBox.Text = dlg.FileName;
            }
        }

        private void BrowseTemplatePdf_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PDF files (*.pdf)|*.pdf" };
            if (dlg.ShowDialog() == true)
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
    }
}
