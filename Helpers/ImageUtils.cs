using System;
using System.IO;
using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;

namespace AutoPhotoEditor.Helpers
{
    public static class ImageUtils
    {
        // Converts CR3 to ImageSharp Image<Rgba32> using Magick.NET as intermediate
        public static Image<Rgba32> ConvertCr3ToImage(string tempPath, string cr3Path)
        {
            if (!File.Exists(cr3Path))
                throw new FileNotFoundException("Input CR3 file not found.", cr3Path);

            string tempPngPath = Path.Combine(tempPath, Path.GetFileNameWithoutExtension(cr3Path) + ".png");

            // Use Magick.NET to read CR3 and save PNG temporarily
            using (var magickImage = new MagickImage(cr3Path))
            {
                // Optional: resize or adjust here if you want
                magickImage.Write(tempPngPath, MagickFormat.Png);
            }

            if (!File.Exists(tempPngPath))
                throw new Exception("Conversion failed: output PNG file not created.");

            // Load the PNG into ImageSharp Image<Rgba32>
            var image = SixLabors.ImageSharp.Image.Load<Rgba32>(tempPngPath);

            // Optional: clean up temp file if desired
            File.Delete(tempPngPath);

            return image;
        }
    }
}
