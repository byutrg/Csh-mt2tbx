using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Csh_mt2tbx
{
    public class Methods
    {
        public static string readFile(string type)
        {
            string fn = "";

            OpenFileDialog dlg = new OpenFileDialog();

            dlg.Title = "Please select your " + type + " file.";

            bool? result = dlg.ShowDialog();

            if (result == true)
            {
                fn = dlg.FileName;
            }


            return fn;
        }
    }
}
