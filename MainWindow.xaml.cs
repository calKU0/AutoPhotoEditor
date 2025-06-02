using AutoPhotoEditor.Helpers;
using AutoPhotoEditor.Services;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Image = SixLabors.ImageSharp.Image;
using ImageResizeMode = SixLabors.ImageSharp.Processing.ResizeMode;
using Path = System.IO.Path;
using Size = SixLabors.ImageSharp.Size;

namespace AutoPhotoEditor
{
    public partial class MainWindow : Window
    {
        #region Secrets
        // Connection string
        public readonly string _connectionString = ConfigurationManager.ConnectionStrings["GaskaConnectionString"].ConnectionString;

        // Cloudinary settings 
        public readonly string _cloudName = ConfigurationManager.AppSettings["CloudinaryCloudName"] ?? "";
        public readonly string _apiKey = ConfigurationManager.AppSettings["CloudinaryApiKey"] ?? "";
        public readonly string _apiSecret = ConfigurationManager.AppSettings["CloudinaryApiSecret"] ?? "";

        // Folders
        public readonly string _archiveFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigurationManager.AppSettings["ArchiveFolder"] ?? "");
        public readonly string _tempFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigurationManager.AppSettings["TempFolder"] ?? "");
        public readonly string _inputFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigurationManager.AppSettings["InputFolder"] ?? "");
        public readonly string _outputFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigurationManager.AppSettings["OutputWithWatermark"] ?? "");
        public readonly string _outputFolderWithoutLogo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigurationManager.AppSettings["OutputWithoutWatermark"] ?? "");

        #endregion

        private readonly Uri placeholder = new Uri("pack://application:,,,/AutoPhotoEditor;component/Resources/placeholder.png");
        private readonly string _pythonScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "cropper.py");
        private readonly string _watermarkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "watermark.png");

        private CancellationTokenSource? _cts;
        private ImageProcessingService _imageService;
        private FolderWatcher? _folderWatcher;

        private string? _lastProcessedImagePath;
        private string? _lastOriginalFilePath;
        private string? _lastCroppedOnlyPath;
        public MainWindow()
        {
            InitializeComponent();
            CheckIfFoldersExists();

            DownloadedImage.Source = new BitmapImage(placeholder);

            var account = new Account(_cloudName, _apiKey, _apiSecret);
            var cloudinary = new Cloudinary(account) { Api = { Timeout = 100000 } };

            _imageService = new ImageProcessingService(cloudinary, _inputFolder, _tempFolder, _outputFolder, _outputFolderWithoutLogo, _archiveFolder, _pythonScriptPath, _watermarkPath);
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_folderWatcher != null)
            {
                MessageBox.Show("Already watching folder.");
                return;
            }

            _cts = new CancellationTokenSource();
            StartButton.IsEnabled = false;
            StopButton.Visibility = Visibility.Visible;

            _folderWatcher = new FolderWatcher(_inputFolder, async filePath =>
            {
                try
                {
                    // Prompt user if previous image was not handled
                    if (_lastProcessedImagePath != null)
                    {
                        var result = MessageBox.Show(
                            "Previous image not saved or deleted. Discard it?",
                            "Unsaved Image",
                            MessageBoxButton.YesNoCancel);

                        if (result == MessageBoxResult.Cancel)
                            return; // Abort this new file processing

                        if (result == MessageBoxResult.Yes)
                        {
                            if (File.Exists(_lastProcessedImagePath))
                                File.Delete(_lastProcessedImagePath);
                            if (_lastCroppedOnlyPath != null && File.Exists(_lastCroppedOnlyPath))
                                File.Delete(_lastCroppedOnlyPath);
                            if (_lastOriginalFilePath != null && File.Exists(_lastOriginalFilePath))
                                File.Delete(_lastOriginalFilePath);
                        }
                        else
                        {
                            return;
                        }

                        _lastProcessedImagePath = null;
                        _lastOriginalFilePath = null;
                        _lastCroppedOnlyPath = null;
                    }

                    Dispatcher.Invoke(() =>
                    {
                        DownloadedImage.Source = new BitmapImage(placeholder);
                        ShowLoading(true);

                        SaveButton.IsEnabled = false;
                        DeleteButton.IsEnabled = false;
                    });

                    (string watermarkedPath, string croppedOnlyPath) = await _imageService.ProcessImageAsync(filePath, _cts.Token);

                    // Save both paths so we can clean up either
                    _lastProcessedImagePath = watermarkedPath;
                    _lastOriginalFilePath = Path.Combine(_archiveFolder, Path.GetFileName(filePath));
                    _lastCroppedOnlyPath = croppedOnlyPath;

                    byte[] imageBytes = await File.ReadAllBytesAsync(watermarkedPath);

                    Dispatcher.Invoke(() =>
                    {
                        DisplayImage(imageBytes);
                        SaveButton.IsEnabled = true;
                        DeleteButton.IsEnabled = true;
                    });
                }
                catch (OperationCanceledException)
                {
                    Dispatcher.Invoke(() => MessageBox.Show("Operation cancelled."));
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show("Error processing file: " + ex.Message));
                }
                finally
                {
                    Dispatcher.Invoke(() => ShowLoading(false));
                }
            });
        }


        private void DisplayImage(byte[] data)
        {
            var bitmap = new BitmapImage();
            using var stream = new MemoryStream(data);
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();

            DownloadedImage.Source = bitmap;
        }


        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            _folderWatcher?.Dispose();
            _folderWatcher = null;

            StartButton.IsEnabled = true;
            StopButton.Visibility = Visibility.Collapsed;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastProcessedImagePath == null)
            {
                MessageBox.Show("No image to save.");
                return;
            }

            // Optionally copy to a permanent user-chosen folder
            MessageBox.Show("Image saved: " + Path.GetFileName(_lastProcessedImagePath));

            // Clear state
            DownloadedImage.Source = new BitmapImage(placeholder);
            SaveButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;
            _lastProcessedImagePath = null;
            _lastCroppedOnlyPath = null;
            _lastOriginalFilePath = null;
        }


        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_lastProcessedImagePath) && File.Exists(_lastProcessedImagePath))
                    File.Delete(_lastProcessedImagePath);

                if (!string.IsNullOrWhiteSpace(_lastCroppedOnlyPath) && File.Exists(_lastCroppedOnlyPath))
                    File.Delete(_lastCroppedOnlyPath);

                if (!string.IsNullOrWhiteSpace(_lastOriginalFilePath) && File.Exists(_lastOriginalFilePath))
                    File.Delete(_lastOriginalFilePath);


                MessageBox.Show("File deleted.");

                SaveButton.IsEnabled = false;
                DeleteButton.IsEnabled = false;
                _lastProcessedImagePath = null;
                _lastCroppedOnlyPath = null;
                _lastOriginalFilePath = null;

                DownloadedImage.Source = new BitmapImage(placeholder);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to delete file: " + ex.Message);
            }
        }


        private void CheckIfFoldersExists()
        {
            if (!Path.Exists(_archiveFolder))
            {
                Directory.CreateDirectory(_archiveFolder);
            }

            if (!Path.Exists(_inputFolder))
            {
                Directory.CreateDirectory(_inputFolder);
            }

            if (!Path.Exists(_tempFolder))
            {
                Directory.CreateDirectory(_tempFolder);
            }

            if (!Path.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
            }

            if (!Path.Exists(_outputFolderWithoutLogo))
            {
                Directory.CreateDirectory(_outputFolderWithoutLogo);
            }
        }

        private void ShowLoading(bool show)
        {
            LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}