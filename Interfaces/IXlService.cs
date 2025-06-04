using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPhotoEditor.Interfaces
{
    public interface IXlService
    {
        public bool Login();
        public bool Logout();
        public int OpenProductList(int productId = -1);
    }
}
