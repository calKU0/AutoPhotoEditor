using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPhotoEditor.Models
{
    public class XlLogin
    {
        public required int ApiVersion { get; set; }
        public required string ProgramName { get; set; }
        public required string Database { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public int WithoutInterface { get; set; } = 1;
    }
}
