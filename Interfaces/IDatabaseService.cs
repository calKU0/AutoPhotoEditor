using AutoPhotoEditor.Models;

namespace AutoPhotoEditor.Interfaces
{
    public interface IDatabaseService
    {
        public Task<List<int?>> AttachImagesToProductAsync(int productId, string extension, List<(byte[] ImageData, bool Watermarked)> imageBytes, string opeIdent);

        public Task<bool> DetachImagesFromProductAsync(List<int?> imageId);

        public Task<Product?> FindProductByEANOrCodeAsync(string code);

        public Task<Product?> FindProductByIdAsync(int id);
    }
}