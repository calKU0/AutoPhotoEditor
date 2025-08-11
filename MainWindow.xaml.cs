using AutoPhotoEditor.Interfaces;
using AutoPhotoEditor.Models;
using AutoPhotoEditor.Services;
using Microsoft.Win32;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Path = System.IO.Path;
using Point = System.Windows.Point;

namespace AutoPhotoEditor
{
    public partial class MainWindow : Window
    {
        #region Secrets

        // Connection string
        public readonly string _connectionString = ConfigurationManager.ConnectionStrings["GaskaConnectionString"].ConnectionString;

        // XL settings
        public readonly int _xlApiVersion = Convert.ToInt32(ConfigurationManager.AppSettings["XLApiVersion"]);

        public readonly string _xlProgramName = ConfigurationManager.AppSettings["XLProgramName"] ?? "";
        public readonly string _xlDatabase = ConfigurationManager.AppSettings["XLDatabase"] ?? "";
        public readonly string _xlUsername = ConfigurationManager.AppSettings["XLUsername"] ?? "";
        public readonly string _xlPassword = ConfigurationManager.AppSettings["XLPassword"] ?? "";

        // Folders
        public readonly string _tempFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp");

        public readonly string _archiveFolder = ConfigurationManager.AppSettings["ArchiveFolder"] ?? "";
        public readonly string _archiveCleanPngFolder = ConfigurationManager.AppSettings["ArchiveCleanPngFolder"] ?? "";
        public readonly string _inputFolder = ConfigurationManager.AppSettings["InputFolder"] ?? "";
        public readonly string _outputFolder = ConfigurationManager.AppSettings["OutputWithWatermark"] ?? "";
        public readonly string _outputFolderWithoutLogo = ConfigurationManager.AppSettings["OutputWithoutWatermark"] ?? "";
        public readonly string _manualEditsFolder = ConfigurationManager.AppSettings["ManualEditsFolder"] ?? "";
        public readonly string _pathToPhotoshop = ConfigurationManager.AppSettings["PathToPhotoshop"] ?? "";

        #endregion Secrets

        private readonly Uri placeholder = new Uri("pack://application:,,,/AutoPhotoEditor;component/Resources/placeholder.png");
        private readonly string _pythonScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "cropper.py");
        private readonly string _pythonRemoveBgScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "bgRemover.py");
        private readonly string _pythonCropScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "crop.py");
        private readonly string _pythonResizeScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "resize.py");
        private readonly string _pythonWatermarkScriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "apply_watermark.py");
        private readonly string _watermarkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "watermark.png");

        private IDatabaseService _databaseService;
        private IXlService _xlService;
        private IImageProcessingService _imageService;
        private IFolderWatcher _folderWatcher;
        private IFolderWatcher _manualEditWatcher;

        private string? _lastOriginalFilePath;
        private string? _lastCleanPngImagePath;
        private string? _lastProcessedImagePath;
        private string? _lastCroppedOnlyPath;

        private CancellationTokenSource? _cts;
        private Point _scrollMousePoint;
        private double _hOffset, _vOffset;
        private bool _isDragging = false;
        private double _initialImageScale = 1.0;

        private bool _isFading = false;
        private string _lastMessage = "";

        public MainWindow()
        {
            InitializeComponent();
            CheckIfFoldersExists();
            LoadFolderPaths();

            DownloadedImage.Source = new BitmapImage(placeholder);

            ImageScaleTransform.ScaleX = 1;
            ImageScaleTransform.ScaleY = 1;

            var xlLogin = new XlLogin
            {
                ApiVersion = _xlApiVersion,
                ProgramName = _xlProgramName,
                Database = _xlDatabase,
                WithoutInterface = 1
            };

            _imageService = new ImageProcessingService(_inputFolder, _tempFolder, _outputFolder, _outputFolderWithoutLogo, _archiveFolder, _archiveCleanPngFolder, _pythonScriptPath, _watermarkPath, _pythonRemoveBgScriptPath, _pythonCropScriptPath, _pythonResizeScriptPath, _pythonWatermarkScriptPath);
            _xlService = new XlService(xlLogin);
            _databaseService = new DatabaseService(_connectionString);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _xlService.Login();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się zalogować do Xl'a. {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }

            if (_folderWatcher != null)
            {
                MessageBox.Show("Już nasłuchuje folder.", "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _folderWatcher = new FolderWatcher(_inputFolder, async filePath =>
            {
                await ProcessImage(filePath);
            });

            _manualEditWatcher = new FolderWatcher(_manualEditsFolder, async manualFilePath =>
            {
                try
                {
                    // Prompt user if previous image was not handled
                    if (_lastProcessedImagePath != null)
                    {
                        MessageBoxResult? result = MessageBoxResult.None;

                        Dispatcher.Invoke(() =>
                        {
                            result = MessageBox.Show("Czy na pewno chcesz usunąć zdjęcie?", "Potwierdź",
                                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                        });

                        if (result is MessageBoxResult.Yes)
                        {
                            if (File.Exists(_lastProcessedImagePath))
                                File.Delete(_lastProcessedImagePath);

                            if (File.Exists(_lastCleanPngImagePath))
                                File.Delete(_lastCleanPngImagePath);

                            if (File.Exists(_lastCroppedOnlyPath))
                                File.Delete(_lastCroppedOnlyPath);
                        }
                        else
                        {
                            return;
                        }

                        _lastProcessedImagePath = null;
                        _lastCroppedOnlyPath = null;
                        _lastCleanPngImagePath = null;
                    }

                    byte[] imageBytes = await File.ReadAllBytesAsync(manualFilePath);

                    _lastProcessedImagePath = manualFilePath;
                    Dispatcher.Invoke(() =>
                    {
                        DisplayImage(imageBytes);
                        ChangeButtonsActive(true);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Błąd przy wyświetlaniu obrazu z folderu ręcznych edycji. {ex}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
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
            Dispatcher.Invoke(() => ResizeImageToFit(), DispatcherPriority.Loaded);
        }

        private async void UpdateLoadingStatus(string message)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(message) || message == _lastMessage)
                    return;

                _lastMessage = message;

                if (_isFading)
                    return; // Prevent reentry

                _isFading = true;

                // Fade out old message if it's visible
                if (LoadingStatusText.Opacity > 0)
                {
                    var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                    LoadingStatusText.BeginAnimation(OpacityProperty, fadeOut);
                    await Task.Delay(200); // Wait for fade out
                }

                // Update text and fade in
                LoadingStatusText.Text = message;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                LoadingStatusText.BeginAnimation(OpacityProperty, fadeIn);

                _isFading = false;
            });
        }

        private void ShowLoading(bool show)
        {
            LoadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (!show)
            {
                _lastMessage = "";
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                LoadingStatusText.BeginAnimation(OpacityProperty, fadeOut);
            }
        }

        private async void SaveButtonToXl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastProcessedImagePath == null)
                {
                    MessageBox.Show("Brak zdjęcia do zapisania.", "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new ProductCodeDialog(_databaseService, _xlService)
                {
                    Owner = this
                };

                bool? result = dialog.ShowDialog();
                Product product = dialog.Product;
                if (result != true || product.Id == 0)
                {
                    MessageBox.Show("Anulowano lub nie podano kodu produktu.", "Anulowano", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (product.Id != 0 && File.Exists(_lastProcessedImagePath))
                {
                    List<(byte[] ImageData, bool Watermarked)> imagesToAdd = new();
                    string extension = Path.GetExtension(_lastProcessedImagePath);
                    byte[] imageBytes = File.ReadAllBytes(_lastProcessedImagePath);
                    string opeIdent = _xlService.OpeIdent;

                    imagesToAdd.Add((imageBytes, true));

                    if (File.Exists(_lastCroppedOnlyPath))
                    {
                        byte[] imageWithoutWatermarkBytes = File.ReadAllBytes(_lastCroppedOnlyPath);
                        imagesToAdd.Add((imageWithoutWatermarkBytes, false));
                    }

                    List<int?> imageIds = await _databaseService.AttachImagesToProductAsync(product.Id, extension, imagesToAdd, opeIdent);

                    if (imageIds is null)
                    {
                        MessageBox.Show("Nie udało się podpiąć zdjęcia do karty towarowej.", "Niepowodzenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var resultDialog = new ProductAddResult(_xlService, product)
                    {
                        Owner = this
                    };

                    bool? resultAdd = resultDialog.ShowDialog();
                    if (resultAdd == false)
                    {
                        bool success = await _databaseService.DetachImagesFromProductAsync(imageIds);
                        if (success)
                        {
                            MessageBox.Show("Odpięto zdjecie od karty towarowej.", "Niepowodzenie", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Nie udało się odpiąć zdjęcia od karty towarowej. Spróbuj zrobić to ręcznie", "Niepowodzenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }

                        return;
                    }
                }

                // Clear state
                DownloadedImage.Source = new BitmapImage(placeholder);
                ChangeButtonsActive(false);
                _lastProcessedImagePath = null;
                _lastCroppedOnlyPath = null;
                _lastCleanPngImagePath = null;

                ResetImageScaleAndScroll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd. {ex}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastProcessedImagePath == null)
                {
                    MessageBox.Show("Brak zdjęcia do zapisania.", "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                MessageBox.Show("Zapisano zdjęcie do folderu.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);

                // Clear state
                DownloadedImage.Source = new BitmapImage(placeholder);
                ChangeButtonsActive(false);
                _lastProcessedImagePath = null;
                _lastCroppedOnlyPath = null;
                _lastCleanPngImagePath = null;

                ResetImageScaleAndScroll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd. {ex}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_lastProcessedImagePath) && File.Exists(_lastProcessedImagePath))
                    File.Delete(_lastProcessedImagePath);

                if (!string.IsNullOrWhiteSpace(_lastCroppedOnlyPath) && File.Exists(_lastCroppedOnlyPath))
                    File.Delete(_lastCroppedOnlyPath);

                if (!string.IsNullOrWhiteSpace(_lastCleanPngImagePath) && File.Exists(_lastCleanPngImagePath))
                    File.Delete(_lastCleanPngImagePath);

                if (!string.IsNullOrWhiteSpace(_lastOriginalFilePath) && File.Exists(_lastOriginalFilePath))
                    File.Delete(_lastOriginalFilePath);

                MessageBox.Show("Usunięto zdjęcie.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);

                ChangeButtonsActive(false);

                _lastProcessedImagePath = null;
                _lastCroppedOnlyPath = null;
                _lastCleanPngImagePath = null;

                DownloadedImage.Source = new BitmapImage(placeholder);

                ResetImageScaleAndScroll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd przy próbie usunięcia zdjęcia. {ex.Message}", "Błąd");
            }
        }

        private void OpenWithPs_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(_pathToPhotoshop))
            {
                MessageBox.Show("Nie znaleziono Photoshopa.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!File.Exists(_lastCleanPngImagePath))
            {
                MessageBox.Show("Nie znaleziono zdjęcia do otwarcia.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                Process.Start(_pathToPhotoshop, $"\"{_lastCleanPngImagePath}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił błąd przy próbie otwarcia photoshopa: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetImageScaleAndScroll()
        {
            // Reset zoom scale
            ImageScaleTransform.ScaleX = 1.0;
            ImageScaleTransform.ScaleY = 1.0;
            _initialImageScale = 1.0;

            // Reset scroll offsets
            ImageScrollViewer.ScrollToHorizontalOffset(0);
            ImageScrollViewer.ScrollToVerticalOffset(0);

            // Call resize logic on placeholder as well
            Dispatcher.InvokeAsync(() => ResizeImageToFit(), DispatcherPriority.Loaded);
        }

        private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DownloadedImage.Source == null)
                return;

            const double zoomFactor = 1.05;
            double oldScale = ImageScaleTransform.ScaleX;
            double newScale = oldScale;

            if (e.Delta > 0)
                newScale = oldScale * zoomFactor;
            else
                newScale = oldScale / zoomFactor;

            // Prevent zooming out too far
            if (newScale < _initialImageScale)
                newScale = _initialImageScale;

            // Position of mouse relative to ScrollViewer
            var scrollViewer = ImageScrollViewer;
            var mousePos = e.GetPosition(scrollViewer);

            // Current scroll offset and mouse position as a % of total image
            double relativeX = (mousePos.X + scrollViewer.HorizontalOffset) / scrollViewer.ExtentWidth;
            double relativeY = (mousePos.Y + scrollViewer.VerticalOffset) / scrollViewer.ExtentHeight;

            // Apply zoom
            ImageScaleTransform.ScaleX = newScale;
            ImageScaleTransform.ScaleY = newScale;

            // Delay scroll adjustment until layout is updated
            scrollViewer.Dispatcher.InvokeAsync(() =>
            {
                double newOffsetX = scrollViewer.ExtentWidth * relativeX - mousePos.X;
                double newOffsetY = scrollViewer.ExtentHeight * relativeY - mousePos.Y;

                scrollViewer.ScrollToHorizontalOffset(newOffsetX);
                scrollViewer.ScrollToVerticalOffset(newOffsetY);
            }, System.Windows.Threading.DispatcherPriority.Loaded);

            e.Handled = true;
        }

        private void ImageGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                _scrollMousePoint = e.GetPosition(ImageScrollViewer);
                _hOffset = ImageScrollViewer.HorizontalOffset;
                _vOffset = ImageScrollViewer.VerticalOffset;
                ((UIElement)sender).CaptureMouse();
            }
        }

        private void ImageGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Point currentPoint = e.GetPosition(ImageScrollViewer);
                double deltaX = currentPoint.X - _scrollMousePoint.X;
                double deltaY = currentPoint.Y - _scrollMousePoint.Y;

                double newHOffset = _hOffset - deltaX;
                double newVOffset = _vOffset - deltaY;

                // Let ScrollViewer clamp offsets internally
                ImageScrollViewer.ScrollToHorizontalOffset(newHOffset);
                ImageScrollViewer.ScrollToVerticalOffset(newVOffset);
            }
        }

        private void ImageGrid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ((UIElement)sender).ReleaseMouseCapture();
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

            if (!Path.Exists(_manualEditsFolder))
            {
                Directory.CreateDirectory(_manualEditsFolder);
            }

            if (!Path.Exists(_archiveCleanPngFolder))
            {
                Directory.CreateDirectory(_archiveCleanPngFolder);
            }
        }

        private void LoadFolderPaths()
        {
            InputFolderText.Text = ConfigurationManager.AppSettings["InputFolder"];
            ArchiveFolderText.Text = ConfigurationManager.AppSettings["ArchiveFolder"];
            ArchiveCleanPngFolderText.Text = ConfigurationManager.AppSettings["ArchiveCleanPngFolder"];
            OutputWithWatermarkText.Text = ConfigurationManager.AppSettings["OutputWithWatermark"];
            OutputWithoutWatermarkText.Text = ConfigurationManager.AppSettings["OutputWithoutWatermark"];
            ManualEditsFolderText.Text = ConfigurationManager.AppSettings["ManualEditsFolder"];
        }

        private void DownloadedImage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DownloadedImage.Source is not BitmapSource bitmap)
                return;

            ImageScrollViewer.Dispatcher.InvokeAsync(() =>
            {
                double imageWidth = bitmap.PixelWidth / (bitmap.DpiX / 96.0);
                double imageHeight = bitmap.PixelHeight / (bitmap.DpiY / 96.0);

                double containerWidth = ImageScrollViewer.ViewportWidth;
                double containerHeight = ImageScrollViewer.ViewportHeight;

                // Fallback in case Viewport is not set yet
                if (containerWidth <= 0 || containerHeight <= 0)
                {
                    containerWidth = ImageScrollViewer.ActualWidth;
                    containerHeight = ImageScrollViewer.ActualHeight;
                }

                if (containerWidth <= 0 || containerHeight <= 0)
                    return;

                double scaleX = containerWidth / imageWidth;
                double scaleY = containerHeight / imageHeight;
                double scale = Math.Min(scaleX, scaleY);

                if (scale < 1.0)
                {
                    ImageScaleTransform.ScaleX = scale;
                    ImageScaleTransform.ScaleY = scale;
                    _initialImageScale = scale;
                }
                else
                {
                    ImageScaleTransform.ScaleX = 1.0;
                    ImageScaleTransform.ScaleY = 1.0;
                    _initialImageScale = 1.0;
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void ResizeImageToFit()
        {
            if (DownloadedImage.Source is not BitmapSource bitmap)
                return;

            double imageWidth = bitmap.PixelWidth / (bitmap.DpiX / 96.0);
            double imageHeight = bitmap.PixelHeight / (bitmap.DpiY / 96.0);

            double containerWidth = ImageScrollViewer.ViewportWidth;
            double containerHeight = ImageScrollViewer.ViewportHeight;

            if (containerWidth <= 0 || containerHeight <= 0)
            {
                containerWidth = ImageScrollViewer.ActualWidth;
                containerHeight = ImageScrollViewer.ActualHeight;
            }

            if (containerWidth <= 0 || containerHeight <= 0)
                return;

            double scaleX = containerWidth / imageWidth;
            double scaleY = containerHeight / imageHeight;
            double scale = Math.Min(scaleX, scaleY);

            Debug.WriteLine($"Image Size: {imageWidth}x{imageHeight}");
            Debug.WriteLine($"Container Size: {containerWidth}x{containerHeight}");
            Debug.WriteLine($"ScaleX: {scaleX}, ScaleY: {scaleY}, Chosen Scale: {scale}");

            if (scale < 1.0)
            {
                ImageScaleTransform.ScaleX = scale;
                ImageScaleTransform.ScaleY = scale;
                _initialImageScale = scale;
            }
            else
            {
                ImageScaleTransform.ScaleX = 1.0;
                ImageScaleTransform.ScaleY = 1.0;
                _initialImageScale = 1.0;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _cts?.Cancel();

            _folderWatcher?.Dispose();
            _folderWatcher = null;

            _manualEditWatcher?.Dispose();
            _manualEditWatcher = null;

            try
            {
                if (_xlService.IsLogged)
                {
                    _xlService.Logout();
                }
            }
            catch (Exception logoutEx)
            {
                MessageBox.Show($"Nie udało się wylogować z XLa. {logoutEx.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_lastOriginalFilePath) || !File.Exists(_lastOriginalFilePath))
                {
                    MessageBox.Show("Brak zdjęcia do ponownego przeprocesowania lub plik został usunięty.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                string destinationPath = Path.Combine(_inputFolder, Path.GetFileName(_lastOriginalFilePath));
                File.Move(_lastOriginalFilePath, destinationPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ponownego przeprocesowania obrazu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangeButtonsActive(bool enable)
        {
            SaveButton.IsEnabled = enable;
            SaveToXlButton.IsEnabled = enable;
            DeleteButton.IsEnabled = enable;
            OpenWithPsButton.IsEnabled = enable;
            RefreshButton.IsEnabled = enable;
        }

        private async Task ProcessImage(string filePath, bool isNew = true)
        {
            _cts = new CancellationTokenSource();
            bool removeBg = false;
            bool crop = false;
            bool addWatermark = false;
            bool scale = false;
            string extension = "jpg";
            string model = "u2net";

            try
            {
                // Prompt user if previous image was not handled
                if (_lastProcessedImagePath != null)
                {
                    MessageBoxResult? result = MessageBoxResult.None;

                    Dispatcher.Invoke(() =>
                    {
                        result = MessageBox.Show("Czy na pewno chcesz usunąć zdjęcie?", "Potwierdź",
                            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    });

                    if (result is MessageBoxResult.Yes)
                    {
                        if (File.Exists(_lastProcessedImagePath))
                            File.Delete(_lastProcessedImagePath);

                        if (File.Exists(_lastCleanPngImagePath))
                            File.Delete(_lastCleanPngImagePath);

                        if (File.Exists(_lastCroppedOnlyPath))
                            File.Delete(_lastCroppedOnlyPath);
                    }
                    else
                    {
                        return;
                    }

                    _lastProcessedImagePath = null;
                    _lastCroppedOnlyPath = null;
                    _lastCleanPngImagePath = null;
                }

                Dispatcher.Invoke(() =>
                {
                    ShowLoading(true);
                    ChangeButtonsActive(false);

                    removeBg = RemoveBackgroundCheckbox.IsChecked ?? false;
                    crop = CropCheckbox.IsChecked ?? false;
                    scale = ScaleCheckBox.IsChecked ?? false;
                    addWatermark = WatermarkCheckbox.IsChecked ?? false;
                    var selectedExtension = FileExtensionCombobox.SelectedItem as ComboBoxItem;
                    extension = (selectedExtension?.Content?.ToString() ?? "jpg").ToLowerInvariant();
                    var selectedModel = AiModelCombobox.SelectedItem as ComboBoxItem;
                    model = (selectedModel?.Tag?.ToString() ?? "jpg").ToLowerInvariant();
                });

                (string? watermarkedPath, string nonWatermarkedPath, string cleanPngImagePath) = await _imageService.ProcessImageAsync(
                    filePath,
                    UpdateLoadingStatus,
                    _cts.Token,
                    removeBg,
                    crop,
                    scale,
                    addWatermark,
                    extension,
                    model
                );

                // Save both paths so we can clean up either
                _lastProcessedImagePath = watermarkedPath ?? nonWatermarkedPath;
                if (isNew)
                    _lastOriginalFilePath = Path.Combine(_archiveFolder, Path.GetFileName(filePath));
                else
                    _lastOriginalFilePath = filePath;

                _lastCroppedOnlyPath = nonWatermarkedPath;
                _lastCleanPngImagePath = cleanPngImagePath;

                byte[] imageBytes = await File.ReadAllBytesAsync(_lastProcessedImagePath);

                Dispatcher.Invoke(() =>
                {
                    DisplayImage(imageBytes);
                    ChangeButtonsActive(true);
                });
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Zatrzymano.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Błąd przy przetwarzaniu pliku. {ex}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                Dispatcher.Invoke(() => ShowLoading(false));
            }
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string key)
            {
                var dialog = new OpenFolderDialog
                {
                    InitialDirectory = ConfigurationManager.AppSettings[key] ?? "",
                    Title = $"Wybierz folder dla: {key}"
                };

                if (dialog.ShowDialog() == true)
                {
                    UpdateAppConfig(key, dialog.FolderName);
                    LoadFolderPaths();
                }
            }
        }

        private void UpdateAppConfig(string key, string value)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (config.AppSettings.Settings[key] != null)
                config.AppSettings.Settings[key].Value = value;
            else
                config.AppSettings.Settings.Add(key, value);

            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        private void ImageContainerGrid_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                DragDropOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void ImageContainerGrid_DragLeave(object sender, DragEventArgs e)
        {
            DragDropOverlay.Visibility = Visibility.Collapsed;
        }

        private void ImageContainerGrid_Drop(object sender, DragEventArgs e)
        {
            DragDropOverlay.Visibility = Visibility.Collapsed;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var filePath in files)
                {
                    try
                    {
                        string fileName = Path.GetFileName(filePath);
                        string destPath = Path.Combine(_inputFolder, fileName);

                        File.Move(filePath, destPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd podczas przenoszenia pliku: {ex.Message}");
                    }
                }
            }
        }
    }
}