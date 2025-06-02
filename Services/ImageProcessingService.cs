using AutoPhotoEditor.Helpers;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using Size = SixLabors.ImageSharp.Size;

namespace AutoPhotoEditor.Services
{
    public class ImageProcessingService
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

        public async Task<(string withWatermark, string withoutWatermark)> ProcessImageAsync(string filePath, CancellationToken token)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found.", filePath);

            string fileBase = Path.GetFileNameWithoutExtension(filePath);
            string tempPngPath = Path.Combine(_tempFolder, fileBase + "_temp.png");

            // Step 1: Convert CR3 to PNG
            using (var image = await Task.Run(() => ImageUtils.ConvertCr3ToImage(_tempFolder, filePath), token))
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(1920, 1080)
                }));

                await using var memStream = new MemoryStream();
                await image.SaveAsync(memStream, new PngEncoder
                {
                    CompressionLevel = PngCompressionLevel.BestCompression,
                    FilterMethod = PngFilterMethod.Adaptive
                }, token);
                memStream.Position = 0;

                await File.WriteAllBytesAsync(tempPngPath, memStream.ToArray(), token);

                // Step 2: Upload to Cloudinary
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(fileBase + ".png", memStream)
                };

                var uploadResult = await Task.Run(() => _cloudinary.Upload(uploadParams), token);
                if (uploadResult.StatusCode != HttpStatusCode.OK || uploadResult.SecureUrl == null)
                    throw new Exception("Cloudinary upload failed.");

                // Step 3: Download transparent image (transformation applies background removal)
                string transformedUrl = _cloudinary.Api.UrlImgUp
                    .Transform(new Transformation().Named("BG+Watermark"))
                    .BuildUrl(uploadResult.PublicId + ".png");

                using var httpClient = new HttpClient();
                byte[] cloudPng = await httpClient.GetByteArrayAsync(transformedUrl, token);

                string bgRemovedPath = Path.Combine(_tempFolder, fileBase + "_bg_removed.png");
                await File.WriteAllBytesAsync(bgRemovedPath, cloudPng, token);

                // Step 4: Run Python crop-only → outputFolderWithoutWatermark
                string croppedPath = Path.Combine(_outputFolderWithoutWatermark, fileBase + "_cropped.png");
                RunPythonCrop(bgRemovedPath, croppedPath);

                // Step 5: Run Python crop+watermark → outputFolder
                string croppedWatermarkedPath = Path.Combine(_outputFolder, fileBase + "_watermarked.png");
                RunPythonCrop(bgRemovedPath, croppedWatermarkedPath, _watermarkPath, 0.3f);

                // Step 6: Archive original file
                string archivedPath = Path.Combine(_archiveFolder, Path.GetFileName(filePath));
                if (File.Exists(archivedPath)) File.Delete(archivedPath);
                File.Move(filePath, archivedPath);

                // Step 7: Clean up Cloudinary
                _cloudinary.Destroy(new DeletionParams(uploadResult.PublicId));

                return (croppedWatermarkedPath, croppedPath);
            }
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

            Debug.WriteLine($"Running Python: python {_pythonScriptPath} {input} {output} {(watermark != null ? watermark : "")} {opacity.ToString(CultureInfo.InvariantCulture)}");
            using var process = Process.Start(psi);
            var error = process.StandardError.ReadToEnd();
            var outputMsg = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            Debug.WriteLine("Python STDOUT:");
            Debug.WriteLine(outputMsg);
            Debug.WriteLine("Python STDERR:");
            Debug.WriteLine(error);

            if (process.ExitCode != 0)
            {
                Debug.WriteLine(error);
                throw new Exception($"Python error: {error}");
            }
        }
    }
}
