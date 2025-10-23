using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestBookletProcessor.Core.Models
{
    public class ProcessingSettings
    {
        public string InputFolder { get; set; } = string.Empty;
        public string OutputFolder { get; set; } = string.Empty;
        public string TemplatePath { get; set; } = string.Empty;
        public int DPI { get; set; } = 300;
        public bool MonitorFolder { get; set; } = false;
    }
}
