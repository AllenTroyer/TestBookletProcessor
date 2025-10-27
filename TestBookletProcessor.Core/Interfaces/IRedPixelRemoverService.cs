namespace TestBookletProcessor.Core.Interfaces
{
    public interface IRedPixelRemoverService
    {
        Task RemoveRedPixelsAsync(string inputImagePath, string outputImagePath, byte redThreshold, int dpi);
    }
}