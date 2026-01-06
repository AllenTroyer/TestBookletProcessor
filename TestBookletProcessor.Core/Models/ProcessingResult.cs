using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestBookletProcessor.Core.Models
{
    /// <summary>
    /// Result of a booklet processing operation.
    /// </summary>
    public class ProcessingResult
    {
        /// <summary>
        /// Gets or sets whether the processing was successful.
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Gets or sets the path to the output file.
        /// </summary>
        public string OutputPath { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the error message if processing failed.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the number of pages processed.
        /// </summary>
        public int PagesProcessed { get; set; }
        
        /// <summary>
        /// Gets or sets the processing time elapsed.
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }
        
        /// <summary>
        /// Gets or sets the processing mode used (e.g., "Booklet", "ScannedSheets").
        /// </summary>
        public string? ProcessingMode { get; set; }
        
        /// <summary>
        /// Gets or sets the DPI setting used for processing.
        /// </summary>
        public int? Dpi { get; set; }
        
        /// <summary>
        /// Gets or sets whether red pixel removal was enabled.
        /// </summary>
        public bool? RedPixelRemovalEnabled { get; set; }
        
        /// <summary>
        /// Gets or sets whether QR scanning was enabled.
        /// </summary>
        public bool? QrScanningEnabled { get; set; }
        
        /// <summary>
        /// Gets or sets the list of extracted file paths (e.g., raw forms).
        /// Used in scanned sheet processing when sheets are extracted as separate files.
        /// </summary>
        public List<string> ExtractedFiles { get; set; } = new();
    }
}
