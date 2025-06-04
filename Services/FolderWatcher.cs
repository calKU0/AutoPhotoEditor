using AutoPhotoEditor.Interfaces;
using System.IO;

namespace AutoPhotoEditor.Services
{
    public class FolderWatcher : IFolderWatcher
    {
        private readonly FileSystemWatcher _watcher;
        private readonly Action<string> _onFileCreated;

        public FolderWatcher(string folderPath, Action<string> onFileCreated)
        {
            _onFileCreated = onFileCreated;

            _watcher = new FileSystemWatcher(folderPath, "*.*")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            _watcher.Created += OnCreated;
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            // Delay to avoid premature access
            Task.Delay(500).ContinueWith(_ => _onFileCreated(e.FullPath));
        }

        public void Dispose()
        {
            _watcher.Created -= OnCreated;
            _watcher.Dispose();
        }
    }
}
