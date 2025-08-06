namespace AutoPhotoEditor.Models
{
    public class XlLogin
    {
        public int ApiVersion { get; set; }
        public string ProgramName { get; set; }
        public string Database { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int WithoutInterface { get; set; } = 1;
    }
}