using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using Newtonsoft.Json.Linq;

namespace TestBookletProcessor.WPF
{
 public partial class FolderMonitorJobsWindow : Window
 {
 private readonly IFolderMonitorJobService _jobService;
 public ObservableCollection<FolderMonitorJobConfig> Jobs { get; } = new();
 private JObject _configJson;

 public FolderMonitorJobsWindow(IFolderMonitorJobService jobService)
 {
 InitializeComponent();
 _jobService = jobService;
 LoadConfig();
 LoadJobs();
 JobsListView.ItemsSource = Jobs;
 }

 private void LoadConfig()
 {
 var configPath = "appsettings.json";
 if (File.Exists(configPath))
 {
 var json = File.ReadAllText(configPath);
 _configJson = JObject.Parse(json);
 }
 else
 {
 _configJson = new JObject();
 }
 }

 private string GetDefaultFolder(string key)
 {
 return _configJson?["BookletProcessor"]?[key]?.ToString() ?? string.Empty;
 }

 private void LoadJobs()
 {
 Jobs.Clear();
 foreach (var job in _jobService.GetAllJobs())
 {
 Jobs.Add(new FolderMonitorJobConfig
 {
 FolderPath = job.FolderPath,
 TemplateFilePath = job.TemplateFilePath,
 OutputFolder = job.OutputFolder
 });
 }
 }

 private void BrowseFolder_Click(object sender, RoutedEventArgs e)
 {
 var dlg = new CommonOpenFileDialog { IsFolderPicker = true };
 var defaultInput = GetDefaultFolder("DefaultInputFolder");
 if (!string.IsNullOrWhiteSpace(defaultInput) && Directory.Exists(defaultInput))
 {
 dlg.InitialDirectory = defaultInput;
 }
 if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
 {
 FolderPathTextBox.Text = dlg.FileName;
 }
 }

 private void BrowseTemplate_Click(object sender, RoutedEventArgs e)
 {
 var dlg = new OpenFileDialog { Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*" };
 var defaultTemplate = GetDefaultFolder("DefaultTemplateFolder");
 if (!string.IsNullOrWhiteSpace(defaultTemplate) && Directory.Exists(defaultTemplate))
 {
 dlg.InitialDirectory = defaultTemplate;
 }
 if (dlg.ShowDialog(this) == true)
 {
 TemplateFileTextBox.Text = dlg.FileName;
 }
 }

 private void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
 {
 var dlg = new CommonOpenFileDialog { IsFolderPicker = true };
 var defaultOutput = GetDefaultFolder("DefaultOutputFolder");
 if (!string.IsNullOrWhiteSpace(defaultOutput) && Directory.Exists(defaultOutput))
 {
 dlg.InitialDirectory = defaultOutput;
 }
 if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
 {
 OutputFolderTextBox.Text = dlg.FileName;
 }
 }

 private void AddJob_Click(object sender, RoutedEventArgs e)
 {
 var folder = FolderPathTextBox.Text.Trim();
 var template = TemplateFileTextBox.Text.Trim();
 var output = OutputFolderTextBox.Text.Trim();
 if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(template) || string.IsNullOrWhiteSpace(output))
 {
 MessageBox.Show("Folder path, template file, and output folder are required.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
 return;
 }
 if (Jobs.Any(j => string.Equals(j.FolderPath, folder, StringComparison.OrdinalIgnoreCase)))
 {
 MessageBox.Show("A job for this folder already exists.", "Duplicate Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
 return;
 }
 _jobService.AddJob(folder, template, output);
 Jobs.Add(new FolderMonitorJobConfig { FolderPath = folder, TemplateFilePath = template, OutputFolder = output });
 }

 private void RemoveJob_Click(object sender, RoutedEventArgs e)
 {
 if (sender is FrameworkElement fe && fe.Tag is string folder)
 {
 _jobService.RemoveJob(folder);
 var job = Jobs.FirstOrDefault(j => j.FolderPath == folder);
 if (job != null) Jobs.Remove(job);
 }
 }
 }
}
