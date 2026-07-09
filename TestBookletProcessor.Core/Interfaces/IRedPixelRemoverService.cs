using System.Collections.Generic;
using System.Threading.Tasks;
using TestBookletProcessor.Core.Models;

namespace TestBookletProcessor.Core.Interfaces
{
    public interface IRedPixelRemoverService
    {
        /// <summary>
        /// Removes red pixels from an image.
        /// </summary>
        Task RemoveRedPixelsAsync(string inputImagePath, string outputImagePath, byte redThreshold, int dpi);
        
        /// <summary>
        /// Removes red pixels from an image while excluding specified regions.
        /// Pixels in exclusion regions are preserved and not modified.
        /// </summary>
        Task RemoveRedPixelsAsync(string inputImagePath, string outputImagePath, byte redThreshold, int dpi, 
            List<RedPixelExclusionRegion> exclusionRegions);
    }
}