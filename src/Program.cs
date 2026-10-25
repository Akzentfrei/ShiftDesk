using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("ShiftDesk")]
[assembly: AssemblyProduct("ShiftDesk")]
[assembly: AssemblyDescription("Ablöseplanung")]
[assembly: AssemblyCompany("ShiftDesk")]
[assembly: AssemblyCopyright("© 2026 Ronny Riner")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

namespace ShiftDesk
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        // ShiftDesk.exe                      beim ersten Start aus Downloads/Zip wird die Installation angeboten
        // ShiftDesk.exe /portable            startet ohne dieses Angebot
        // ShiftDesk.exe /install [/quiet]    installiert für den aktuellen Benutzer
        // ShiftDesk.exe /uninstall [/quiet]  deinstalliert wieder
        // ShiftDesk.exe "Plan.shiftdesk"     öffnet die Datei (Doppelklick im Explorer)
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
            string datei = args.FirstOrDefault(x => !x.StartsWith("/") && System.IO.File.Exists(x));
            // Wer die exe frisch bekommen hat, soll sie nicht aus dem Download-Ordner heraus benutzen müssen
            if (datei == null && !a.Contains("/portable") && Installation.BeimStartAnbieten())
                return;
            Application.Run(new MainForm(datei));
        }
    }
}
