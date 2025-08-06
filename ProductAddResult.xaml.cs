using AutoPhotoEditor.Interfaces;
using AutoPhotoEditor.Models;
using System.Windows;
using System.Windows.Input;
using MessageBox = ModernWpf.MessageBox;

namespace AutoPhotoEditor
{
    /// <summary>
    /// Interaction logic for ProductAddResult.xaml
    /// </summary>
    public partial class ProductAddResult : Window
    {
        private readonly IXlService _xlService;
        private readonly Product _product;

        public ProductAddResult(IXlService xlService, Product product)
        {
            InitializeComponent();
            _xlService = xlService;
            _product = product;
            DataContext = product;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OpenList_Click(object sender, RoutedEventArgs e)
        {
            if (!_xlService.IsLogged)
                _xlService.Login();

            _xlService.OpenProductList(_product.Id);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
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

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                DialogResult = true;
        }
    }
}