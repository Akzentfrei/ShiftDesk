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
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

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
        // ShiftDesk.exe /entfernen "Pfad" [/quiet]  löscht die Kopie in Downloads usw., die diese Version gestartet hat
        [STAThread]
        static void Main(string[] args)
        {
            if (Environment.OSVersion.Version.Major >= 6) SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var a = args.Select(x => x.ToLowerInvariant()).ToList();
            bool leise = a.Contains("/quiet");
            int entfernen = a.IndexOf(Installation.EntfernenSchalter);
            if (entfernen >= 0 && entfernen + 1 < args.Length)
            {
                Installation.KopieEntfernen(args[entfernen + 1], leise);
                if (leise) return;
                args = args.Where((x, i) => i != entfernen && i != entfernen + 1).ToArray();
            }
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
            // Wer die exe frisch bekommen hat, soll sie nicht aus dem Download-Ordner heraus benutzen müssen,
            // und auf dem PC soll nur die neueste Version liegen
            if (!a.Contains("/portable") && Installation.BeimStart(datei))
                return;
            Application.Run(new MainForm(datei));
        }
    }
}
