using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestBookletProcessor.Core.Models
{
    public class ProcessingResult
    {
        public bool Success { get; set; }
        public string OutputPath { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public int PagesProcessed { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }
}
