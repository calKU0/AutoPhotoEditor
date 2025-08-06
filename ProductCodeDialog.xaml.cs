using AutoPhotoEditor.Interfaces;
using AutoPhotoEditor.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessageBox = ModernWpf.MessageBox;

namespace AutoPhotoEditor
{
    /// <summary>
    /// Interaction logic for ProductCodeDialog.xaml
    /// </summary>
    public partial class ProductCodeDialog : Window
    {
        public Product Product { get; private set; } = new();
        private readonly IDatabaseService _databaseService;
        private readonly IXlService _xlService;

        public ProductCodeDialog(IDatabaseService databaseService, IXlService xlService)
        {
            InitializeComponent();
            _databaseService = databaseService;
            _xlService = xlService;
        }

        private async void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (Product.Id <= 0)
            {
                Product = await _databaseService.FindProductByEANOrCodeAsync(ProductCodeTextBox.Text);
            }

            if (Product is not null && Product.Id > 0)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Nie znaleziono produktu w bazie danych.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void OpenList_Click(object sender, RoutedEventArgs e)
        {
            _xlService.Login();
            int selectedId = _xlService.OpenProductList();

            if (selectedId > 0)
            {
                Product = await _databaseService.FindProductByIdAsync(selectedId);
                ProductCodeTextBox.Text = Product.Code;
            }
            else
            {
                MessageBox.Show("Nie wybrano żadnego produktu.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ProductCodeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Product.Id != 0)
            {
                Product = new();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ProductCodeTextBox.Focus();
        }

        private async void ProductCodeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Product.Id <= 0)
                {
                    Product = await _databaseService.FindProductByEANOrCodeAsync(ProductCodeTextBox.Text);
                }

                if (Product is not null && Product.Id > 0)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Nie znaleziono produktu w bazie danych.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}