using AutoPhotoEditor.Helpers;
using AutoPhotoEditor.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using SixLabors.ImageSharp.Formats.Png;
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

        public async Task<(string withWatermark, string withoutWatermark)> ProcessImageAsync(string filePath, Action<string> statusCallback, CancellationToken token)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found.", filePath);

            string fileBase = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            statusCallback("Przygotowanie obrazu...");

            // Step 1: Load and resize image based on extension
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

            // Step 2: Upload to Cloudinary from MemoryStream directly
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

            // Step 3: Download transparent image (background removed)
            string transformedUrl = _cloudinary.Api.UrlImgUp
                .Transform(new Transformation().Named("BG+Watermark"))
                .BuildUrl(uploadResult.PublicId + ".png");

            using var httpClient = new HttpClient();
            byte[] cloudPng = await httpClient.GetByteArrayAsync(transformedUrl, token);

            string bgRemovedPath = Path.Combine(_tempFolder, fileBase + "_bg_removed.png");
            await File.WriteAllBytesAsync(bgRemovedPath, cloudPng, token);

            statusCallback("Kadrowanie...");
            // Step 4: Crop only → without watermark
            string croppedPath = Path.Combine(_outputFolderWithoutWatermark, fileBase + "_cropped.jpg");
            RunPythonCrop(bgRemovedPath, croppedPath);

            // Step 5: Crop + watermark
            string croppedWatermarkedPath = Path.Combine(_outputFolder, fileBase + "_watermarked.jpg");
            RunPythonCrop(bgRemovedPath, croppedWatermarkedPath, _watermarkPath, 0.5f);

            // Step 6: Archive original
            string archivedPath = Path.Combine(_archiveFolder, Path.GetFileName(filePath));
            if (File.Exists(archivedPath)) File.Delete(archivedPath);
            File.Move(filePath, archivedPath);

            statusCallback("Zakończono.");

            // Step 7: Clean up Cloudinary
            _cloudinary.Destroy(new DeletionParams(uploadResult.PublicId));

            return (croppedWatermarkedPath, croppedPath);
        }


        private void RunPythonCrop(string input, string output, string? watermark = null, float opacity = 0.3f)
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
            var argsList = new List<string>
            {
                Quote(_pythonScriptPath),
                Quote(input),
                Quote(output)
            };

            if (watermark != null)
            {
                argsList.Add(Quote(watermark));
                argsList.Add(opacity.ToString(CultureInfo.InvariantCulture));
            }

            psi.Arguments = string.Join(" ", argsList);

            using var process = Process.Start(psi);
            var error = process!.StandardError.ReadToEnd();
            var outputMsg = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Debug.WriteLine(error);
                throw new Exception($"Python error: {error}");
            }
        }
    }
}
