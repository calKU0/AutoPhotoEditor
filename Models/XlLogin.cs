namespace AutoPhotoEditor.Models
{
    public class XlLogin
    {
        public int ApiVersion { get; set; }
        public string ProgramName { get; set; }
        public string Database { get; set; }
        public string OpeIdent { get; set; }
        public int WithoutInterface { get; set; } = 0;
    }
}