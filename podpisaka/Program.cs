//
// C#
// DigitalCertAndSignMaker
// v 0.28, 12.04.2023
// https://github.com/dkxce/Pospisaka
// en,ru,1251,utf-8
//


using System;
using System.Windows.Forms;

namespace DigitalCertAndSignMaker
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
