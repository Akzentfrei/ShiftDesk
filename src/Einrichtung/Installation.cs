using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Dialoge;

namespace ShiftDesk.Einrichtung
{
    /// <summary>
    /// Installation nur für den aktuellen Benutzer (ohne Administratorrechte): Programmordner,
    /// Startmenü-Verknüpfung (Windows-Suche), Dateizuordnung und Eintrag unter «Apps» zum Deinstallieren.
    /// </summary>
    /// <remarks>
    /// Damit auf dem PC nur eine Version liegt, räumt <see cref="KopienAufraeumen"/> liegengebliebene
    /// ShiftDesk-Dateien in Downloads, auf dem Desktop und in Dokumente weg.
    /// </remarks>
    public static class Installation
    {
        /// <summary>Befehlszeilen-Schalter: <c>ShiftDesk.exe /entfernen "Pfad"</c> löscht eine liegengebliebene Kopie.</summary>
        public const string EntfernenSchalter = "/entfernen";

        /// <summary>Programmordner <c>%LOCALAPPDATA%\Programs\ShiftDesk</c>.</summary>
        public static string Ordner
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ShiftDesk"); }
        }

        /// <summary>Die installierte exe.</summary>
        public static string Exe
        {
            get { return Path.Combine(Ordner, "ShiftDesk.exe"); }
        }

        /// <summary>Läuft gerade die installierte exe (und nicht eine Kopie)?</summary>
        public static bool LaeuftInstalliert
        {
            get { return string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(Exe), StringComparison.OrdinalIgnoreCase); }
        }

        static Version EigeneVersion
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        /// <summary>Version der installierten exe, null wenn keine da ist.</summary>
        internal static Version InstallierteVersion
        {
            get
            {
                if (!File.Exists(Exe)) return null;
                return ShiftDeskVersion(Exe) ?? new Version(0, 0);
            }
        }

        /// <summary>Version einer ShiftDesk-exe, null wenn die Datei keine ist.</summary>
        internal static Version ShiftDeskVersion(string pfad)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(pfad);
                if (info.ProductName != "ShiftDesk") return null;
                Version ergebnis;
                return Version.TryParse(info.FileVersion, out ergebnis) ? ergebnis : new Version(0, 0);
            }
            catch { return null; }
        }

        static bool AndereInstanzLaeuft()
        {
            int ich = Process.GetCurrentProcess().Id;
            return Process.GetProcessesByName("ShiftDesk").Any(p => p.Id != ich);
        }

        // ---------------------------------------------------------------- Start

        /// <summary>
        /// Beim Programmstart: läuft die installierte exe, räumt sie im Hintergrund liegengebliebene Kopien weg.
        /// Beim Start aus einem fremden Ordner (Downloads, Zip, USB-Stick) wird die Installation bzw. Aktualisierung
        /// angeboten; ist schon dieselbe oder eine neuere Version installiert, öffnet sich stattdessen diese.
        /// </summary>
        /// <param name="datei">Datei, die geöffnet werden soll (Doppelklick im Explorer), sonst null.</param>
        /// <returns>true, wenn jetzt die installierte Kopie läuft – dann endet diese hier.</returns>
        public static bool BeimStart(string datei)
        {
            if (LaeuftInstalliert)
            {
                new Thread(KopienAufraeumen.AlteKopienEntfernen) { IsBackground = true, Priority = ThreadPriority.BelowNormal }.Start();
                return false;
            }
            Version alt = InstallierteVersion, neu = EigeneVersion;
            bool liegengeblieben = KopienAufraeumen.LiegtInAblage(Application.ExecutablePath);
            // eine ältere Kopie, oder eine doppelte in Downloads, auf dem Desktop usw.: die installierte Version öffnen
            if (alt != null && (alt > neu || alt == neu && liegengeblieben))
            {
                KopienAufraeumen.AlteKopienEntfernen();
                return InstallierteStarten(datei, liegengeblieben);
            }
            if (datei != null) return false;
            const string wegraeumen = " Übrige ShiftDesk-Dateien in Downloads und auf dem Desktop werden danach entfernt, damit es nur eine Version gibt.";
            bool ja;
            if (alt == null)
                ja = Rueckfrage.Vorschlagen(null, "ShiftDesk auf diesem PC installieren?",
                    "ShiftDesk wird in dein Benutzerprofil kopiert und ins Startmenü eingetragen. Das braucht keine Administratorrechte." + wegraeumen +
                    " Du kannst das Programm aber auch ohne Installation direkt benutzen.", "Installieren", "download", "Ohne Installation starten");
            else if (alt < neu)
                ja = Rueckfrage.Vorschlagen(null, "Installiertes ShiftDesk aktualisieren?",
                    "Auf diesem PC ist ShiftDesk " + alt.ToString(3) + " installiert. Diese Datei ist die neuere Version " + neu.ToString(3) + "." + wegraeumen,
                    "Aktualisieren", "sparkles", "Ohne Installation starten");
            else
                return false;   // dieselbe Version ausserhalb von Downloads usw. (z. B. auf einem USB-Stick): einfach starten
            if (!ja) return false;
            if (!Installieren(true)) return false;
            return InstallierteStarten(null, liegengeblieben);
        }

        /// <summary>Startet die installierte exe; mit <paramref name="wegraeumen"/> löscht sie diese Kopie, sobald sie beendet ist.</summary>
        static bool InstallierteStarten(string datei, bool wegraeumen)
        {
            string argumente = "";
            if (datei != null) argumente += "\"" + Path.GetFullPath(datei) + "\" ";
            if (wegraeumen) argumente += EntfernenSchalter + " \"" + Path.GetFullPath(Application.ExecutablePath) + "\"";
            try
            {
                Process.Start(new ProcessStartInfo(Exe, argumente.Trim()) { WorkingDirectory = Ordner });
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Nach «Auf diesem PC installieren» in der Seitenleiste: diese Kopie beim Schliessen wegräumen lassen.</summary>
        public static void BeimBeendenWegraeumen()
        {
            if (LaeuftInstalliert || !KopienAufraeumen.LiegtInAblage(Application.ExecutablePath)) return;
            string ich = Path.GetFullPath(Application.ExecutablePath);
            Application.ApplicationExit += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(Exe, EntfernenSchalter + " \"" + ich + "\" /quiet") { WorkingDirectory = Ordner }); }
                catch { }
            };
        }

        // ---------------------------------------------------------------- Installieren und Deinstallieren

        /// <summary>Kopiert diese exe in den Programmordner (falls sie nicht schon dort läuft) und trägt sie bei Windows ein.</summary>
        /// <param name="leise">Ohne Erfolgsmeldung (Fehler werden immer gemeldet).</param>
        /// <returns>true, wenn installiert wurde.</returns>
        public static bool Installieren(bool leise)
        {
            if (AndereInstanzLaeuft() && !LaeuftInstalliert)
            {
                bool installierteOffen = Process.GetProcessesByName("ShiftDesk").Any(p =>
                {
                    try { return string.Equals(p.MainModule.FileName, Exe, StringComparison.OrdinalIgnoreCase); }
                    catch { return false; }
                });
                if (installierteOffen)
                {
                    Meldung.Info(null, "ShiftDesk ist noch geöffnet", "Bitte zuerst schliessen und dann erneut installieren.");
                    return false;
                }
            }
            try
            {
                Directory.CreateDirectory(Ordner);
                if (!LaeuftInstalliert)
                {
                    File.Copy(Application.ExecutablePath, Exe, true);
                    Windowseintraege.HerkunftEntfernen(Exe);
                }
                Windowseintraege.VerknuepfungErstellen(Windowseintraege.Startmenue);
                Windowseintraege.DateizuordnungEintragen();
                Windowseintraege.AppEintragen(EigeneVersion.ToString(3));
            }
            catch (Exception ex)
            {
                Meldung.Fehler(null, "Die Installation ist fehlgeschlagen", ex.Message);
                return false;
            }
            KopienAufraeumen.AlteKopienEntfernen();
            if (!leise)
                Meldung.Erfolg(null, "ShiftDesk ist installiert", "Du findest es jetzt im Startmenü und über die Windows-Suche. " +
                    "Deinstallieren kannst du es unter Einstellungen → Apps.");
            return true;
        }

        /// <summary>
        /// Entfernt Verknüpfung, Einträge und Programmordner; auf Wunsch auch die zuletzt verwendeten Eingaben.
        /// Selbst gespeicherte .shiftdesk-Dateien bleiben immer erhalten.
        /// </summary>
        /// <param name="leise">Ohne Rückfragen und Meldungen (<c>/uninstall /quiet</c>).</param>
        public static void Deinstallieren(bool leise)
        {
            if (!leise && !Rueckfrage.Fragen(null, "ShiftDesk wirklich deinstallieren?",
                    "Selbst gespeicherte .shiftdesk-Dateien bleiben erhalten.", "Deinstallieren"))
                return;
            if (AndereInstanzLaeuft())
            {
                if (leise) return;
                Meldung.Info(null, "ShiftDesk ist noch geöffnet", "Bitte zuerst schliessen und dann erneut deinstallieren.");
                return;
            }
            string datenordner = Speicherorte.Datenordner;
            bool datenLoeschen = !leise && Directory.Exists(datenordner) &&
                Rueckfrage.Fragen(null, "Sollen auch deine zuletzt verwendeten Eingaben gelöscht werden?",
                    "Selbst gespeicherte .shiftdesk-Dateien bleiben in jedem Fall erhalten.", "Löschen", "trash-2", "Behalten");

            try { if (File.Exists(Windowseintraege.Startmenue)) File.Delete(Windowseintraege.Startmenue); } catch { }
            Windowseintraege.AppAustragen();
            Windowseintraege.DateizuordnungEntfernen();
            if (datenLoeschen)
                try { Directory.Delete(datenordner, true); } catch { }

            // Die laufende exe kann sich nicht selbst löschen: den Ordner kurz nach dem Beenden entfernen lassen
            if (Directory.Exists(Ordner))
                Process.Start(new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"" + Ordner + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetTempPath(),
                });

            if (!leise)
                Meldung.Erfolg(null, "ShiftDesk wurde deinstalliert");
        }
    }
}
