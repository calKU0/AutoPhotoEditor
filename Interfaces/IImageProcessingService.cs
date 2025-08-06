namespace AutoPhotoEditor.Interfaces
{
    public interface IImageProcessingService
    {
        /// <summary>
        /// Processes the input image and returns paths to:
        /// - image with watermark
        /// - image without watermark
        /// </summary>
        /// <param name="filePath">Input CR3 file path</param>
        /// <param name="statusCallback">Status callback</param>
        /// <param name="token">Cancellation token</param>
        /// <param name="removeBg">Should image remove bg</param>
        /// <param name="crop">Should image be cropped</param>
        /// <returns>Tuple of (withWatermarkPath, withoutWatermarkPath)</returns>
        Task<(string? withWatermark, string withoutWatermark)> ProcessImageAsync(string filePath,
            Action<string> statusCallback,
            CancellationToken token,
            bool removeBg,
            bool crop,
            bool addWatermark,
            string extension);
    }
}