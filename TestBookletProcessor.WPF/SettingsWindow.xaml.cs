using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Windows;

namespace TestBookletProcessor.WPF
{
    public partial class SettingsWindow : Window
    {
        private readonly string _configPath = "appsettings.json";
        private JObject _configJson;

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
            }
            else
            {
                _configJson = new JObject();
            }
        }

        private void BrowseInputFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog { IsFolderPicker = true };
            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                InputFolderTextBox.Text = dlg.FileName;
            }
        }

        private void BrowseTemplateFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog { IsFolderPicker = true };
            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                TemplateFolderTextBox.Text = dlg.FileName;
            }
        }

        private void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog { IsFolderPicker = true };
            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                OutputFolderTextBox.Text = dlg.FileName;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_configJson["BookletProcessor"] == null)
                _configJson["BookletProcessor"] = new JObject();
            var bp = (JObject)_configJson["BookletProcessor"]!;
            bp["DefaultInputFolder"] = InputFolderTextBox.Text;
            bp["DefaultTemplateFolder"] = TemplateFolderTextBox.Text;
            bp["DefaultOutputFolder"] = OutputFolderTextBox.Text;
            File.WriteAllText(_configPath, _configJson.ToString());
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
