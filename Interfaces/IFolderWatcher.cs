using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPhotoEditor.Interfaces
{
    public interface IFolderWatcher : IDisposable
    {
        // No methods to expose here, since the constructor does all the setup.
        // Optional: You can add Start/Stop or IsWatching if needed.
    }
}
