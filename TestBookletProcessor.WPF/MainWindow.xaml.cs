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

        public MainWindow()
        {
            InitializeComponent();
            _bookletProcessor = new BookletProcessorService(_pdfService, _imageProcessor);
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
            string inputPdf = InputPdfTextBox.Text;
            string templatePdf = TemplatePdfTextBox.Text;
            if (!File.Exists(inputPdf) || !File.Exists(templatePdf))
            {
                StatusTextBlock.Text = "Please select valid input and template PDF files.";
                return;
            }
            string outputFolder = Path.Combine(Path.GetDirectoryName(inputPdf)!, "BookletOutput");
            StatusTextBlock.Text = "Processing...";
            var result = await _bookletProcessor.ProcessBookletsWorkflowAsync(
                inputPdf,
                templatePdf,
                outputFolder,
                (current, total) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusTextBlock.Text = $"Processing booklet {current} of {total}...";
                    });
                });
            if (result.Success)
            {
                StatusTextBlock.Text = $"Processing complete! {result.PagesProcessed} booklets processed in {result.ProcessingTime.ToString(@"mm\:ss")}. Output: {result.OutputPath}";
            }
            else
            {
                StatusTextBlock.Text = $"Error: {result.ErrorMessage}";
            }
        }
    }
}
