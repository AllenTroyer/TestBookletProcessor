using System;
using System.Collections.Generic;

namespace TestBookletProcessor.Core.Interfaces
{
 public interface IFolderMonitorJobService
 {
 void AddJob(string folderPath, string templateFilePath, string outputFolder);
 void RemoveJob(string folderPath);
 IEnumerable<string> GetMonitoredFolders();
 IEnumerable<TestBookletProcessor.Core.Models.FolderMonitorJobConfig> GetAllJobs();
 event EventHandler<FolderFileDetectedEventArgs> FileDetected;
 }

 public class FolderFileDetectedEventArgs : EventArgs
 {
 public string FolderPath { get; set; }
 public string FilePath { get; set; }
 public string TemplateFilePath { get; set; }
 public string OutputFolder { get; set; }
 }
}
