using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TestBookletProcessor.Services;

namespace TestBookletProcessor.WPF
{
    public partial class SettingsWindow : Window
    {
        private readonly string _configPath = AppConfig.ConfigFilePath;
        private JObject _configJson = new();

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _configJson = JObject.Parse(json);
                var bp = _configJson["BookletProcessor"];
                InputFolderTextBox.Text = bp?["DefaultInputFolder"]?.ToString() ?? "";
                TemplateFolderTextBox.Text = bp?["DefaultTemplateFolder"]?.ToString() ?? "";
                OutputFolderTextBox.Text = bp?["DefaultOutputFolder"]?.ToString() ?? "";

                // Load Default DPI
                var dpiStr = bp?["DefaultDpi"]?.ToString();
                DefaultDpiTextBox.Text = int.TryParse(dpiStr, out var dpiVal) ? dpiVal.ToString() : "300";

                // Load RedPixelRemover settings
                var enableRedStr = bp?["EnableRedPixelRemover"]?.ToString();
                EnableRedPixelRemoverCheckBox.IsChecked = enableRedStr != null && enableRedStr.Equals("true", StringComparison.OrdinalIgnoreCase);

                var thresholdStr = bp?["RedPixelThreshold"]?.ToString();
                RedPixelThresholdTextBox.Text = byte.TryParse(thresholdStr, out var val) ? val.ToString() : "200";

                // Load QR Scanner settings
                var qrScanner = bp?["QrScanner"];
                if (qrScanner != null)
                {
                    var enableQrStr = qrScanner["EnableQrScanning"]?.ToString();
                    EnableQrScanningCheckBox.IsChecked = enableQrStr != null && enableQrStr.Equals("true", StringComparison.OrdinalIgnoreCase);

                    QrRegionXTextBox.Text = qrScanner["QrRegionXInches"]?.ToString() ?? "6.5";
                    QrRegionYTextBox.Text = qrScanner["QrRegionYInches"]?.ToString() ?? "9.0";
                    QrRegionWidthTextBox.Text = qrScanner["QrRegionWidthInches"]?.ToString() ?? "2.0";
                    QrRegionHeightTextBox.Text = qrScanner["QrRegionHeightInches"]?.ToString() ?? "2.0";

                    // Load exclusion patterns as comma-separated string
                    var exclusionArray = qrScanner["QrValuesExcludingRedRemoval"] as JArray;
                    if (exclusionArray != null)
                    {
                        var patterns = exclusionArray.Select(t => t.ToString()).ToArray();
                        QrExclusionPatternsTextBox.Text = string.Join(", ", patterns);
                    }
                    else
                    {
                        QrExclusionPatternsTextBox.Text = "*-FRTCVR, CLEAN";
                    }
                }
                else
                {
                    // Default QR scanner settings
                    EnableQrScanningCheckBox.IsChecked = false;
                    QrRegionXTextBox.Text = "6.5";
                    QrRegionYTextBox.Text = "9.0";
                    QrRegionWidthTextBox.Text = "2.0";
                    QrRegionHeightTextBox.Text = "2.0";
                    QrExclusionPatternsTextBox.Text = "*-FRTCVR, CLEAN";
                }

                // Load Template Exclusion Patterns
                var templateExclusionArray = bp?["TemplateExclusionPatterns"] as JArray;
                if (templateExclusionArray != null)
                {
                    var templatePatterns = templateExclusionArray.Select(t => t.ToString()).ToArray();
                    TemplateExclusionPatternsTextBox.Text = string.Join(", ", templatePatterns);
                }
                else
                {
                    TemplateExclusionPatternsTextBox.Text = "*TEMPLATE*, *BLANK*, *SAMPLE*";
                }
                
                // Load Scanned Sheet Settings
                var scannedSheets = bp?["ScannedSheets"];
                if (scannedSheets != null)
                {
                    ScannedSheetTemplateNameTextBox.Text = scannedSheets["TemplateName"]?.ToString() ?? "Template_ScannedSheets.pdf";
                }
                else
                {
                    ScannedSheetTemplateNameTextBox.Text = "Template_ScannedSheets.pdf";
                }
            }
            else
            {
                DefaultDpiTextBox.Text = "300";
                EnableRedPixelRemoverCheckBox.IsChecked = true;
                RedPixelThresholdTextBox.Text = "200";
                EnableQrScanningCheckBox.IsChecked = false;
                QrRegionXTextBox.Text = "6.5";
                QrRegionYTextBox.Text = "9.0";
                QrRegionWidthTextBox.Text = "2.0";
                QrRegionHeightTextBox.Text = "2.0";
                QrExclusionPatternsTextBox.Text = "*-FRTCVR, CLEAN";
                TemplateExclusionPatternsTextBox.Text = "*TEMPLATE*, *BLANK*, *SAMPLE*";
                ScannedSheetTemplateNameTextBox.Text = "Template_ScannedSheets.pdf";
            }
        }

        private void BrowseInputFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog { IsFolderPicker = true };
            if (dlg.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                InputFolderTextBox.Text = dlg.FileName;
            }
        }

        private void BrowseTemplateFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog { IsFolderPicker = true };
            if (dlg.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                TemplateFolderTextBox.Text = dlg.FileName;
            }
        }

        private void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog { IsFolderPicker = true };
            if (dlg.ShowDialog(this) == CommonFileDialogResult.Ok)
            {
                OutputFolderTextBox.Text = dlg.FileName;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate Default DPI
            if (!int.TryParse(DefaultDpiTextBox.Text, out int dpi) || dpi < 72 || dpi > 600)
            {
                MessageBox.Show("Default DPI must be a number between 72 and 600.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate RedPixelThreshold
            if (!byte.TryParse(RedPixelThresholdTextBox.Text, out byte threshold))
            {
                MessageBox.Show("Red Pixel Threshold must be a number between 0 and 255.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate QR region coordinates
            if (!double.TryParse(QrRegionXTextBox.Text, out double qrX) || qrX < 0)
            {
                MessageBox.Show("QR Region X must be a positive number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!double.TryParse(QrRegionYTextBox.Text, out double qrY) || qrY < 0)
            {
                MessageBox.Show("QR Region Y must be a positive number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!double.TryParse(QrRegionWidthTextBox.Text, out double qrWidth) || qrWidth <= 0)
            {
                MessageBox.Show("QR Region Width must be a positive number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!double.TryParse(QrRegionHeightTextBox.Text, out double qrHeight) || qrHeight <= 0)
            {
                MessageBox.Show("QR Region Height must be a positive number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_configJson["BookletProcessor"] == null)
                _configJson["BookletProcessor"] = new JObject();
            var bp = (JObject)_configJson["BookletProcessor"]!;

            bp["DefaultInputFolder"] = InputFolderTextBox.Text;
            bp["DefaultTemplateFolder"] = TemplateFolderTextBox.Text;
            bp["DefaultOutputFolder"] = OutputFolderTextBox.Text;
            bp["DefaultDpi"] = dpi;
            bp["EnableRedPixelRemover"] = EnableRedPixelRemoverCheckBox.IsChecked == true;
            bp["RedPixelThreshold"] = threshold;

            // Save QR Scanner settings
            if (bp["QrScanner"] == null)
                bp["QrScanner"] = new JObject();
            var qrScanner = (JObject)bp["QrScanner"]!;

            qrScanner["EnableQrScanning"] = EnableQrScanningCheckBox.IsChecked == true;
            qrScanner["QrRegionXInches"] = qrX;
            qrScanner["QrRegionYInches"] = qrY;
            qrScanner["QrRegionWidthInches"] = qrWidth;
            qrScanner["QrRegionHeightInches"] = qrHeight;

            // Parse and save exclusion patterns
            var patternsText = QrExclusionPatternsTextBox.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(patternsText))
            {
                var patterns = patternsText.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToArray();
                qrScanner["QrValuesExcludingRedRemoval"] = new JArray(patterns);
            }
            else
            {
                qrScanner["QrValuesExcludingRedRemoval"] = new JArray();
            }

            // Parse and save template exclusion patterns
            var templatePatternsText = TemplateExclusionPatternsTextBox.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(templatePatternsText))
            {
                var templatePatterns = templatePatternsText.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToArray();
                bp["TemplateExclusionPatterns"] = new JArray(templatePatterns);
            }
            else
            {
                bp["TemplateExclusionPatterns"] = new JArray();
            }
            
            // Save Scanned Sheet Settings
            if (bp["ScannedSheets"] == null)
                bp["ScannedSheets"] = new JObject();
            var scannedSheets = (JObject)bp["ScannedSheets"]!;
            
            var scannedSheetTemplateName = ScannedSheetTemplateNameTextBox.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(scannedSheetTemplateName))
            {
                scannedSheets["TemplateName"] = scannedSheetTemplateName;
            }
            else
            {
                scannedSheets["TemplateName"] = "Template_ScannedSheets.pdf";
            }
            // Note: QrToPageMapping is not edited here - users should edit appsettings.json directly

            File.WriteAllText(_configPath, _configJson.ToString());
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void RedPixelThresholdTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow numeric input
            e.Handled = !IsTextNumeric(e.Text);
        }

        private void DecimalTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow numeric input and decimal point
            e.Handled = !IsTextDecimal(e.Text, ((TextBox)sender).Text);
        }

        private static bool IsTextNumeric(string text)
        {
            return Regex.IsMatch(text, "^[0-9]+$");
        }

        private static bool IsTextDecimal(string input, string currentText)
        {
            // Allow digits and one decimal point
            if (input == "." && !currentText.Contains("."))
                return true;
            return Regex.IsMatch(input, "^[0-9]+$");
        }
    }
}
