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
            Task.Run(async () =>
            {
                const int maxRetries = 10;
                const int delayMs = 500;

                for (int i = 0; i < maxRetries; i++)
                {
                    if (IsFileReady(e.FullPath))
                    {
                        _onFileCreated(e.FullPath);
                        return;
                    }

                    await Task.Delay(delayMs);
                }
            });
        }

        private bool IsFileReady(string filePath)
        {
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return stream.Length > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _watcher.Created -= OnCreated;
            _watcher.Dispose();
        }
    }
}