using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;
using Newtonsoft.Json.Linq;

namespace TestBookletProcessor.Services
{
 public class FolderMonitorJobService : IFolderMonitorJobService, IDisposable
 {
 private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
 private readonly ConcurrentDictionary<string, FolderMonitorJobConfig> _jobs = new();
 private readonly string _configPath;
 private readonly JObject _configJson;
 public event EventHandler<FolderFileDetectedEventArgs>? FileDetected;

 public FolderMonitorJobService(string configPath = "appsettings.json")
 {
 _configPath = configPath;
 if (File.Exists(_configPath))
 {
 var json = File.ReadAllText(_configPath);
 _configJson = JObject.Parse(json);
 LoadJobsFromConfig();
 }
 else
 {
 _configJson = new JObject();
 }
 }

 private void LoadJobsFromConfig()
 {
 var jobsArray = _configJson["MonitoredFolders"] as JArray;
 if (jobsArray != null)
 {
 foreach (var job in jobsArray)
 {
 var folder = job["InputFolder"]?.ToString();
 var template = job["TemplateFile"]?.ToString();
 var output = job["OutputFolder"]?.ToString();
 if (!string.IsNullOrWhiteSpace(folder) && !string.IsNullOrWhiteSpace(template) && !string.IsNullOrWhiteSpace(output))
 {
 AddJob(folder, template, output, false); // Don't persist again
 }
 }
 }
 }

 public void AddJob(string folderPath, string templateFilePath, string outputFolder)
 {
 AddJob(folderPath, templateFilePath, outputFolder, true);
 }

 private void AddJob(string folderPath, string templateFilePath, string outputFolder, bool persist)
 {
 if (_watchers.ContainsKey(folderPath)) return;
 var jobConfig = new FolderMonitorJobConfig { FolderPath = folderPath, TemplateFilePath = templateFilePath, OutputFolder = outputFolder };
 var watcher = new FileSystemWatcher(folderPath)
 {
 EnableRaisingEvents = true,
 NotifyFilter = NotifyFilters.FileName,
 Filter = "*.*"
 };
 watcher.Created += (s, e) =>
 {
 FileDetected?.Invoke(this, new FolderFileDetectedEventArgs
 {
 FolderPath = folderPath,
 FilePath = e.FullPath,
 TemplateFilePath = templateFilePath,
 OutputFolder = outputFolder
 });
 };
 _watchers[folderPath] = watcher;
 _jobs[folderPath] = jobConfig;
 if (persist) PersistJobs();
 }

 public void RemoveJob(string folderPath)
 {
 if (_watchers.TryRemove(folderPath, out var watcher))
 {
 watcher.EnableRaisingEvents = false;
 watcher.Dispose();
 }
 _jobs.TryRemove(folderPath, out _);
 PersistJobs();
 }

 public IEnumerable<FolderMonitorJobConfig> GetAllJobs() => _jobs.Values.ToList();
 public IEnumerable<string> GetMonitoredFolders() => _jobs.Keys.ToList();

 private void PersistJobs()
 {
 var jobsArray = new JArray();
 foreach (var job in _jobs.Values)
 {
 jobsArray.Add(new JObject
 {
 ["InputFolder"] = job.FolderPath,
 ["TemplateFile"] = job.TemplateFilePath,
 ["OutputFolder"] = job.OutputFolder
 });
 }
 _configJson["MonitoredFolders"] = jobsArray;
 File.WriteAllText(_configPath, _configJson.ToString());
 }

 public void Dispose()
 {
 foreach (var watcher in _watchers.Values)
 {
 watcher.EnableRaisingEvents = false;
 watcher.Dispose();
 }
 _watchers.Clear();
 _jobs.Clear();
 }
 }
}
