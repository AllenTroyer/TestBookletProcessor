using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
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
            Directory.CreateDirectory(outputFolder);
            string inputFileNameNoExt = Path.GetFileNameWithoutExtension(inputPdf);
            string finalOutputPdf = Path.Combine(outputFolder, $"{inputFileNameNoExt}_aligned.pdf");
            StatusTextBlock.Text = "Processing...";
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // Split input PDF into booklets
                var bookletPaths = await _pdfService.SplitIntoBookletsAsync(inputPdf, templatePdf, Path.Combine(outputFolder, "booklets"));
                var processedBookletPaths = new System.Collections.Generic.List<string>();
                int totalBooklets = bookletPaths.Count;
                int bookletIndex = 1;
                foreach (var bookletPath in bookletPaths)
                {
                    StatusTextBlock.Text = $"Processing booklet {bookletIndex} of {totalBooklets}...";
                    await Task.Delay(100); // Let UI update
                    string bookletWorkingFolder = Path.Combine(outputFolder, $"booklet_{bookletIndex}");
                    string processedBookletOutput = Path.Combine(bookletWorkingFolder, "processed_booklet.pdf");
                    await _bookletProcessor.ProcessBookletAsync(templatePdf, bookletPath, bookletWorkingFolder, processedBookletOutput);
                    processedBookletPaths.Add(processedBookletOutput);
                    bookletIndex++;
                }
                // Merge all processed booklets into the final output
                await _pdfService.MergePdfsAsync(processedBookletPaths, finalOutputPdf);
                stopwatch.Stop();
                StatusTextBlock.Text = $"Processing complete! {totalBooklets} booklets processed in {stopwatch.Elapsed.ToString(@"mm\:ss")}. Output: {finalOutputPdf}";
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                StatusTextBlock.Text = $"Error: {ex.Message}";
            }
        }
    }
}
