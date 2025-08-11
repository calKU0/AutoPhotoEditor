using AutoPhotoEditor.Interfaces;
using SixLabors.ImageSharp;
using System.Diagnostics;
using System.IO;

namespace AutoPhotoEditor.Services
{
    public class ImageProcessingService : IImageProcessingService
    {
        private readonly string _inputFolder;
        private readonly string _tempFolder;
        private readonly string _outputFolder;
        private readonly string _outputFolderWithoutWatermark;
        private readonly string _archiveFolder;
        private readonly string _pythonScriptPath;
        private readonly string _pythonRemoveBgScriptPath;
        private readonly string _watermarkPath;
        private readonly string _archiveCleanPngFolder;
        private readonly string _pythonCropScriptPath;
        private readonly string _pythonResizeScriptPath;
        private readonly string _pythonWatermarkScriptPath;

        public ImageProcessingService(
            string inputFolder,
            string tempFolder,
            string outputFolder,
            string outputFolderWithoutWatermark,
            string archiveFolder,
            string archiveCleanPngFolder,
            string pythonScriptPath,
            string watermarkPath,
            string pythonRemoveBgScriptPath,
            string pythonCropScriptPath,
            string pythonResizeScriptPath,
            string pythonWatermarkScriptPath)
        {
            _inputFolder = inputFolder;
            _tempFolder = tempFolder;
            _outputFolder = outputFolder;
            _outputFolderWithoutWatermark = outputFolderWithoutWatermark;
            _archiveFolder = archiveFolder;
            _pythonScriptPath = pythonScriptPath;
            _watermarkPath = watermarkPath;
            _pythonRemoveBgScriptPath = pythonRemoveBgScriptPath;
            _archiveCleanPngFolder = archiveCleanPngFolder;
            _pythonCropScriptPath = pythonCropScriptPath;
            _pythonResizeScriptPath = pythonResizeScriptPath;
            _pythonWatermarkScriptPath = pythonWatermarkScriptPath;
        }

        public async Task<(string? withWatermark, string withoutWatermark, string cleanPngImage)> ProcessImageAsync(
            string filePath,
            Action<string> statusCallback,
            CancellationToken token,
            bool removeBg,
            bool crop,
            bool scale,
            bool addWatermark,
            string ext,
            string model)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found.", filePath);

            string fileBase = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            string currentImagePath = filePath;

            statusCallback("Ładuje zdjęcie...");

            if (removeBg)
            {
                statusCallback("Usuwam tło...");
                string bgRemovedPath = Path.Combine(_tempFolder, fileBase + "_bg_removed.png");
                RunPythonRemoveBg(filePath, bgRemovedPath, model);
                currentImagePath = bgRemovedPath;
            }

            // temp working file
            string workingPath = Path.Combine(_tempFolder, fileBase + "_working.png");
            File.Copy(currentImagePath, workingPath, true);

            string cleanPng = string.Empty;

            if (crop)
            {
                statusCallback("Kadruje...");
                string croppedPath = Path.Combine(_tempFolder, fileBase + "_cropped.png");
                RunPython(_pythonCropScriptPath, workingPath, croppedPath);
                workingPath = croppedPath;
                cleanPng = Path.Combine(_archiveCleanPngFolder, Path.GetFileName(workingPath));
                File.Copy(workingPath, cleanPng, true);
            }

            if (scale)
            {
                statusCallback("Skaluje...");
                string resizedPath = Path.Combine(_tempFolder, fileBase + "_resized.png");
                RunPython(_pythonResizeScriptPath, workingPath, resizedPath, "900");
                workingPath = resizedPath;
            }

            string withoutWatermarkPath = Path.Combine(_outputFolderWithoutWatermark, fileBase + "_processed." + ext);
            File.Copy(workingPath, withoutWatermarkPath, true);

            string? withWatermarkPath = null;
            if (addWatermark)
            {
                statusCallback("Nakładam znak wodny...");
                withWatermarkPath = Path.Combine(_outputFolder, fileBase + "_watermarked." + ext);
                RunPython(_pythonWatermarkScriptPath, workingPath, withWatermarkPath, _watermarkPath, "0.5");
            }

            // Archive original
            string archivedPath = Path.Combine(_archiveFolder, Path.GetFileName(filePath));
            if (File.Exists(archivedPath)) File.Delete(archivedPath);
            File.Move(filePath, archivedPath);

            statusCallback("Done.");
            return (withWatermarkPath, withoutWatermarkPath, cleanPng);
        }

        private void RunPython(string scriptPath, params string[] args)
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
            psi.Arguments = string.Join(" ", new[] { Quote(scriptPath) }.Concat(args.Select(Quote)));

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

        private void RunPythonRemoveBg(string inputPath, string outputPath, string model)
        {
            var arguments = $"i -a -m \"{model}\" \"{inputPath}\" \"{outputPath}\"";
            var psi = new ProcessStartInfo
            {
                FileName = "rembg",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            string error = process.StandardError.ReadToEnd();
            string outputLog = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Debug.WriteLine(error);
                Debug.WriteLine(outputLog);
                throw new Exception($"rembg CLI error: {error}");
            }
        }
    }
}