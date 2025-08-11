namespace AutoPhotoEditor.Interfaces
{
    public interface IXlService
    {
        public bool Login();

        public bool Logout();

        public int OpenProductList(int productId = -1);

        public bool IsLogged { get; }
        public string OpeIdent { get; }
    }
}