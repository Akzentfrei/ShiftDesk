using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ShiftDesk.Einrichtung;
using ShiftDesk.Oberflaeche;

namespace ShiftDesk
{
    /// <summary>Einstieg: wertet die Befehlszeile aus und öffnet das Hauptfenster.</summary>
    /// <remarks>
    /// <list type="table">
    /// <item><term><c>ShiftDesk.exe</c></term><description>beim ersten Start aus Downloads/Zip wird die Installation angeboten</description></item>
    /// <item><term><c>ShiftDesk.exe "Plan.shiftdesk"</c></term><description>öffnet die Datei (Doppelklick im Explorer)</description></item>
    /// <item><term><c>/portable</c></term><description>startet ohne das Angebot zu installieren</description></item>
    /// <item><term><c>/install [/quiet]</c></term><description>installiert für den aktuellen Benutzer</description></item>
    /// <item><term><c>/uninstall [/quiet]</c></term><description>deinstalliert wieder</description></item>
    /// <item><term><c>/entfernen "Pfad" [/quiet]</c></term><description>löscht die Kopie in Downloads usw., die diese Version gestartet hat</description></item>
    /// </list>
    /// </remarks>
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

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
                KopienAufraeumen.KopieEntfernen(args[entfernen + 1], leise);
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
            Application.Run(new Hauptfenster(datei));
        }
    }
}
