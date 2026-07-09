using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestBookletProcessor.Core.Interfaces;
using TestBookletProcessor.Core.Models;

namespace TestBookletProcessor.Services
{
    /// <summary>
    /// Watches configured folders for new PDF files and raises <see cref="FileDetected"/>.
    /// Jobs are persisted to the "MonitoredFolders" section of appsettings.json.
    /// </summary>
    public class FolderMonitorJobService : IFolderMonitorJobService, IDisposable
    {
        /// <summary>
        /// FileSystemWatcher can raise several events for one file (and both Created and
        /// Renamed when a file is written via a temp name); events for the same path within
        /// this window are treated as duplicates.
        /// </summary>
        private static readonly TimeSpan DuplicateEventWindow = TimeSpan.FromSeconds(5);

        private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, FolderMonitorJobConfig> _jobs = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTime> _recentEvents = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _configPath;
        private readonly object _persistLock = new();

        public event EventHandler<FolderFileDetectedEventArgs>? FileDetected;

        public FolderMonitorJobService() : this(AppConfig.ConfigFilePath)
        {
        }

        public FolderMonitorJobService(string configPath)
        {
            _configPath = configPath;
            LoadJobsFromConfig();
        }

        private void LoadJobsFromConfig()
        {
            if (!File.Exists(_configPath))
                return;

            JArray? jobsArray;
            try
            {
                var configJson = JObject.Parse(File.ReadAllText(_configPath));
                jobsArray = configJson["MonitoredFolders"] as JArray;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FolderMonitor] Failed to read monitored folders from {_configPath}: {ex.Message}");
                return;
            }

            if (jobsArray == null)
                return;

            foreach (var job in jobsArray)
            {
                var folder = job["InputFolder"]?.ToString();
                var template = job["TemplateFile"]?.ToString();
                var output = job["OutputFolder"]?.ToString();
                if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(template) || string.IsNullOrWhiteSpace(output))
                    continue;

                try
                {
                    AddJob(folder, template, output, persist: false);
                }
                catch (Exception ex)
                {
                    // A missing or invalid folder must not prevent the application (and the
                    // remaining monitors) from starting.
                    Console.WriteLine($"[FolderMonitor] Skipping monitor for '{folder}': {ex.Message}");
                }
            }
        }

        public void AddJob(string folderPath, string templateFilePath, string outputFolder)
        {
            AddJob(folderPath, templateFilePath, outputFolder, persist: true);
        }

        private void AddJob(string folderPath, string templateFilePath, string outputFolder, bool persist)
        {
            folderPath = Path.GetFullPath(folderPath);

            if (_watchers.ContainsKey(folderPath))
                return;

            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Monitored folder does not exist: {folderPath}");

            if (IsSameOrSubPath(folderPath, outputFolder))
                throw new ArgumentException(
                    $"Output folder '{outputFolder}' is inside the monitored folder '{folderPath}'; " +
                    "this would cause processed files to be re-detected.");

            var jobConfig = new FolderMonitorJobConfig
            {
                FolderPath = folderPath,
                TemplateFilePath = templateFilePath,
                OutputFolder = outputFolder
            };

            var watcher = new FileSystemWatcher(folderPath)
            {
                NotifyFilter = NotifyFilters.FileName,
                Filter = "*.pdf"
            };
            // Files are frequently delivered by write-to-temp-then-rename (Dropbox, scanners),
            // which raises Renamed rather than Created — both must be handled.
            watcher.Created += (s, e) => OnFileEvent(folderPath, e.FullPath, templateFilePath, outputFolder);
            watcher.Renamed += (s, e) => OnFileEvent(folderPath, e.FullPath, templateFilePath, outputFolder);
            watcher.Error += (s, e) => Console.WriteLine(
                $"[FolderMonitor] Watcher error for '{folderPath}': {e.GetException()?.Message}. " +
                "Files arriving during this window may have been missed.");

            _watchers[folderPath] = watcher;
            _jobs[folderPath] = jobConfig;
            watcher.EnableRaisingEvents = true;

            if (persist) PersistJobs();
        }

        private void OnFileEvent(string folderPath, string filePath, string templateFilePath, string outputFolder)
        {
            var now = DateTime.UtcNow;
            if (_recentEvents.TryGetValue(filePath, out var lastSeen) && now - lastSeen < DuplicateEventWindow)
                return;
            _recentEvents[filePath] = now;

            // Keep the dedupe map from growing unbounded
            if (_recentEvents.Count > 256)
            {
                foreach (var stale in _recentEvents.Where(kvp => now - kvp.Value > DuplicateEventWindow).ToList())
                    _recentEvents.TryRemove(stale.Key, out _);
            }

            FileDetected?.Invoke(this, new FolderFileDetectedEventArgs
            {
                FolderPath = folderPath,
                FilePath = filePath,
                TemplateFilePath = templateFilePath,
                OutputFolder = outputFolder
            });
        }

        public void RemoveJob(string folderPath)
        {
            folderPath = Path.GetFullPath(folderPath);
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
            lock (_persistLock)
            {
                try
                {
                    // Re-read the file so concurrent edits to other sections (e.g. the
                    // settings dialog) are not clobbered by a stale snapshot.
                    var configJson = File.Exists(_configPath)
                        ? JObject.Parse(File.ReadAllText(_configPath))
                        : new JObject();

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
                    configJson["MonitoredFolders"] = jobsArray;
                    File.WriteAllText(_configPath, configJson.ToString());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FolderMonitor] Failed to persist monitored folders: {ex.Message}");
                }
            }
        }

        private static bool IsSameOrSubPath(string parent, string candidate)
        {
            try
            {
                var parentFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
                var candidateFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
                return candidateFull.Equals(parentFull, StringComparison.OrdinalIgnoreCase) ||
                       candidateFull.StartsWith(parentFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
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
