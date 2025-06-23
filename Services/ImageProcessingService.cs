using AutoPhotoEditor.Helpers;
using AutoPhotoEditor.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using Size = SixLabors.ImageSharp.Size;

namespace AutoPhotoEditor.Services
{
    public class ImageProcessingService : IImageProcessingService
    {
        private readonly Cloudinary _cloudinary;
        private readonly string _inputFolder;
        private readonly string _tempFolder;
        private readonly string _outputFolder;
        private readonly string _outputFolderWithoutWatermark;
        private readonly string _archiveFolder;
        private readonly string _pythonScriptPath;
        private readonly string _watermarkPath;

        public ImageProcessingService(
            Cloudinary cloudinary,
            string inputFolder,
            string tempFolder,
            string outputFolder,
            string outputFolderWithoutWatermark,
            string archiveFolder,
            string pythonScriptPath,
            string watermarkPath)
        {
            _cloudinary = cloudinary;
            _inputFolder = inputFolder;
            _tempFolder = tempFolder;
            _outputFolder = outputFolder;
            _outputFolderWithoutWatermark = outputFolderWithoutWatermark;
            _archiveFolder = archiveFolder;
            _pythonScriptPath = pythonScriptPath;
            _watermarkPath = watermarkPath;
        }

        public async Task<(string? withWatermark, string withoutWatermark)> ProcessImageAsync(
            string filePath,
            Action<string> statusCallback,
            CancellationToken token,
            bool removeBg,
            bool crop,
            bool addWatermark,
            string ext)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found.", filePath);

            string fileBase = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            statusCallback("Przygotowanie obrazu...");

            using var image = await Task.Run(() =>
            {
                return extension switch
                {
                    ".cr3" => ImageUtils.ConvertCr3ToImage(filePath),
                    ".jpg" or ".jpeg" or ".png" => ImageUtils.LoadAndResizeImage(filePath),
                    _ => throw new NotSupportedException($"Unsupported file type: {extension}")
                };
            }, token);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(100);

            string currentImagePath = filePath;
            string? bgRemovedPath = null;
            string? withoutWatermarkPath = null;
            string? withWatermarkPath = null;
            string? cloudinaryPublicId = null;

            if (removeBg)
            {
                await using var memStream = new MemoryStream();
                await image.SaveAsync(memStream, new PngEncoder
                {
                    CompressionLevel = PngCompressionLevel.BestCompression,
                    FilterMethod = PngFilterMethod.Adaptive
                }, token);
                memStream.Position = 0;

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(fileBase + ".png", memStream)
                };

                statusCallback("Usuwanie tła...");
                var uploadResult = await Task.Run(() => _cloudinary.Upload(uploadParams), token);
                if (uploadResult.StatusCode != HttpStatusCode.OK || uploadResult.SecureUrl == null)
                    throw new Exception("Cloudinary upload failed.");

                cloudinaryPublicId = uploadResult.PublicId;

                string transformedUrl = _cloudinary.Api.UrlImgUp
                    .Transform(new Transformation().Named("BG+Watermark"))
                    .BuildUrl(uploadResult.PublicId + ".png");

                using var httpClient = new HttpClient();
                byte[] cloudPng = await httpClient.GetByteArrayAsync(transformedUrl, token);

                bgRemovedPath = Path.Combine(_tempFolder, fileBase + "_bg_removed.png");
                await File.WriteAllBytesAsync(bgRemovedPath, cloudPng, token);

                currentImagePath = bgRemovedPath;
            }

            // Define output paths
            withoutWatermarkPath = Path.Combine(_outputFolderWithoutWatermark, fileBase + $"_processed.{ext}");
            withWatermarkPath = Path.Combine(_outputFolder, fileBase + $"_watermarked.{ext}");

            statusCallback("Kadrowanie...");

            if (crop || addWatermark)
            {
                // If cropping or watermarking needed, run python crop (with crop flag!)
                RunPythonCrop(currentImagePath, withoutWatermarkPath, null, 0.3f, crop);

                if (addWatermark)
                {
                    RunPythonCrop(currentImagePath, withWatermarkPath, _watermarkPath, 0.5f, crop);
                }
            }
            else
            {
                // No cropping, no watermarking — just copy original or resized
                if (removeBg)
                {
                    // Flatten transparency to white before saving to JPG
                    using var imgWithAlpha = Image.Load<Rgba32>(currentImagePath);
                    var flattened = imgWithAlpha.Clone(ctx => ctx.BackgroundColor(Color.White));
                    await flattened.SaveAsJpegAsync(withoutWatermarkPath);
                }
                else
                {
                    using var originalImage = ImageUtils.LoadAndResizeImage(currentImagePath);
                    await originalImage.SaveAsJpegAsync(withoutWatermarkPath);
                }
            }

            // Archive original
            string archivedPath = Path.Combine(_archiveFolder, Path.GetFileName(filePath));
            if (File.Exists(archivedPath)) File.Delete(archivedPath);
            File.Move(filePath, archivedPath);

            statusCallback("Zakończono.");

            if (cloudinaryPublicId != null)
                _cloudinary.Destroy(new DeletionParams(cloudinaryPublicId));

            return (addWatermark ? withWatermarkPath : null, withoutWatermarkPath);
        }

        private void RunPythonCrop(string input, string output, string? watermark = null, float opacity = 0.3f, bool doCrop = true)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "python",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            string Quote(string s) => $"\"{s}\"";
            var args = new List<string>
            {
                Quote(_pythonScriptPath),
                Quote(input),
                Quote(output),
                Quote(watermark ?? "NONE"),
                doCrop ? "1" : "0",
                opacity.ToString(CultureInfo.InvariantCulture)
            };

            psi.Arguments = string.Join(" ", args);

            using var process = Process.Start(psi);
            string error = process.StandardError.ReadToEnd();
            string outputLog = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Debug.WriteLine(error);
                Debug.WriteLine(outputLog);
                throw new Exception($"Python error: {error}");
            }
        }
    }
}
