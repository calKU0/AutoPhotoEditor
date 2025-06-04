using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;

namespace AutoPhotoEditor.Helpers
{
    public static class ImageUtils
    {
        // Converts CR3 to ImageSharp Image<Rgba32> using Magick.NET as intermediate
        public static Image<Rgba32> ConvertCr3ToImage(string cr3Path)
        {
            if (!File.Exists(cr3Path))
                throw new FileNotFoundException("Input CR3 file not found.", cr3Path);

            if (IsFileLocked(cr3Path))
                throw new IOException("File is locked: " + cr3Path);

            var settings = new MagickReadSettings
            {
                Width = 1920,
                Height = 1080,
                Density = new Density(72)
            };

            using (var magickImage = new MagickImage(cr3Path, settings))
            {
                magickImage.Resize(1920, 1080);

                using var ms = new MemoryStream();
                magickImage.Write(ms, MagickFormat.Png);
                ms.Position = 0;

                return Image.Load<Rgba32>(ms);
            }
        }
        public static Image<Rgba32> LoadAndResizeImage(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Image file not found.", path);

            using var image = Image.Load<Rgba32>(path);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(1920, 1080),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            }));

            var result = new Image<Rgba32>(1920, 1080);
            result.Mutate(x => x.DrawImage(image, new Point(0, 0), 1f));
            return result;
        }

        public static bool IsFileLocked(string filePath)
        {
            try
            {
                using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }
    }
}
