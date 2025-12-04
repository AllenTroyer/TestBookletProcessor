using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace TestBookletProcessor.WPF
{
    public partial class SettingsWindow : Window
    {
        private readonly string _configPath = "appsettings.json";
        private JObject _configJson;

        public SettingsWindow()
        {
            InitializeComponent();
            //Topmost = true;
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
                
                // Load RedPixelRemover settings
                var enableRedStr = bp?["EnableRedPixelRemover"]?.ToString();
                EnableRedPixelRemoverCheckBox.IsChecked = enableRedStr != null && enableRedStr.Equals("true", StringComparison.OrdinalIgnoreCase);
                
                var thresholdStr = bp?["RedPixelThreshold"]?.ToString();
                RedPixelThresholdTextBox.Text = byte.TryParse(thresholdStr, out var val) ? val.ToString() : "200";
            }
            else
            {
                _configJson = new JObject();
                EnableRedPixelRemoverCheckBox.IsChecked = true;
                RedPixelThresholdTextBox.Text = "200";
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
            // Validate RedPixelThreshold
            if (!byte.TryParse(RedPixelThresholdTextBox.Text, out byte threshold))
            {
                MessageBox.Show("Red Pixel Threshold must be a number between 0 and 255.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_configJson["BookletProcessor"] == null)
                _configJson["BookletProcessor"] = new JObject();
            var bp = (JObject)_configJson["BookletProcessor"]!;
            bp["DefaultInputFolder"] = InputFolderTextBox.Text;
            bp["DefaultTemplateFolder"] = TemplateFolderTextBox.Text;
            bp["DefaultOutputFolder"] = OutputFolderTextBox.Text;
            bp["EnableRedPixelRemover"] = EnableRedPixelRemoverCheckBox.IsChecked == true;
            bp["RedPixelThreshold"] = threshold;
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

        private static bool IsTextNumeric(string text)
        {
            return Regex.IsMatch(text, "^[0-9]+$");
        }
    }
}
