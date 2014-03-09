using System;
using System.IO;
using System.Windows.Forms;
using log4net.Config;

namespace d2mp
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            XmlConfigurator.Configure();
            D2MP.main();
        }
    }
}
