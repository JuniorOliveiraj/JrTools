using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Dto
{
    public class ProcessStartInfoGitDataobject
    {
        public string FileName { get; set; }
        public string Arguments { get; set; }
        public bool RedirectStandardOutput { get; set; }
        public bool RedirectStandardError { get; set; }
        public bool UseShellExecute { get; set; }
        public bool CreateNoWindow { get; set; }
        public string WorkingDirectory { get; set; }
    }
}
