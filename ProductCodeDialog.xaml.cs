using AutoPhotoEditor.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MessageBox = ModernWpf.MessageBox;

namespace AutoPhotoEditor
{
    /// <summary>
    /// Interaction logic for ProductCodeDialog.xaml
    /// </summary>
    public partial class ProductCodeDialog : Window
    {
        public int? ProductId { get; private set; }
        private readonly IDatabaseService _databaseService;
        private readonly IXlService _xlService;
        private int productId = 0;
        public ProductCodeDialog(IDatabaseService databaseService, IXlService xlService)
        {
            InitializeComponent();
            _databaseService = databaseService;
            _xlService = xlService;
            _xlService.Login();
        }
        private async void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (productId <= 0)
            {
                productId = await _databaseService.FindProductByEANOrCode(ProductCodeTextBox.Text);
            }

            if (productId > 0)
            {
                ProductId = productId;
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
            int selectedId = _xlService.OpenProductList();

            if (selectedId > 0)
            {
                string code = await _databaseService.FindProductById(selectedId);
                ProductCodeTextBox.Text = code;
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
            if (productId != 0)
            {
                productId = 0;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                _xlService.Logout();
            }
            catch (Exception logoutEx)
            {
                MessageBox.Show($"Nie udało się wylogować z XLa. {logoutEx.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
