using AutoPhotoEditor.Models;

namespace AutoPhotoEditor.Interfaces
{
    public interface IDatabaseService
    {
        public Task<int?> AttachImageToProductAsync(int productId, string extension, byte[] imageBytes);

        public Task<bool> DetachImageFromProductAsync(int imageId);

        public Task<Product?> FindProductByEANOrCodeAsync(string code);

        public Task<Product?> FindProductByIdAsync(int id);
    }
}