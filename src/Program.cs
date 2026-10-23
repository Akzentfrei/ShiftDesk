using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("ShiftDesk")]
[assembly: AssemblyProduct("ShiftDesk")]
[assembly: AssemblyDescription("Ablöseplanung")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace ShiftDesk
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        // ShiftDesk.exe /install [/quiet]    installiert für den aktuellen Benutzer
        // ShiftDesk.exe /uninstall [/quiet]  deinstalliert wieder
        [STAThread]
        static void Main(string[] args)
        {
            if (Environment.OSVersion.Version.Major >= 6) SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var a = args.Select(x => x.ToLowerInvariant()).ToList();
            bool leise = a.Contains("/quiet");
            if (a.Contains("/uninstall"))
            {
                Installation.Deinstallieren(leise);
                return;
            }
            if (a.Contains("/install"))
            {
                Installation.Installieren(leise);
                return;
            }
            Application.Run(new MainForm());
        }
    }
}
