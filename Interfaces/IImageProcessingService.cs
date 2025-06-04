using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        /// <param name="token">Cancellation token</param>
        /// <returns>Tuple of (withWatermarkPath, withoutWatermarkPath)</returns>
        Task<(string withWatermark, string withoutWatermark)> ProcessImageAsync(string filePath, Action<string> statusCallback, CancellationToken token);
    }
}
