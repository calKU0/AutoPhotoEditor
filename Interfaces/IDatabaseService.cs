using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPhotoEditor.Interfaces
{
    public interface IDatabaseService
    {
        public Task<bool> AttachImageToProduct(int productId, string extension, byte[] imageBytes);
        public Task<int> FindProductByEANOrCode(string code);
        public Task<string> FindProductById(int id);
    }
}
