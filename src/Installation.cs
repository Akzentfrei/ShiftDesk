using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ShiftDesk
{
    // Installation nur für den aktuellen Benutzer (ohne Administratorrechte):
    // Programmordner, Startmenü-Verknüpfung (Windows-Suche) und Eintrag unter «Apps» zum Deinstallieren.
    public static class Installation
    {
        const string RegistryPfad = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ShiftDesk";

        public static string Ordner
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ShiftDesk"); }
        }

        public static string Exe
        {
            get { return Path.Combine(Ordner, "ShiftDesk.exe"); }
        }

        static string Verknuepfung
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "ShiftDesk.lnk"); }
        }

        static string Datenordner
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShiftDesk"); }
        }

        public static bool LaeuftInstalliert
        {
            get { return string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(Exe), StringComparison.OrdinalIgnoreCase); }
        }

        static bool AndereInstanzLaeuft()
        {
            int ich = Process.GetCurrentProcess().Id;
            return Process.GetProcessesByName("ShiftDesk").Any(p => p.Id != ich);
        }

        static Version EigeneVersion
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        // Version einer ShiftDesk-exe, null wenn es keine ist
        static Version ShiftDeskVersion(string pfad)
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

        // Version der installierten exe, null wenn keine da ist
        static Version InstallierteVersion
        {
            get
            {
                if (!File.Exists(Exe)) return null;
                return ShiftDeskVersion(Exe) ?? new Version(0, 0);
            }
        }

        // Läuft die installierte exe, räumt sie im Hintergrund liegengebliebene Kopien weg.
        // Beim Start aus einem fremden Ordner (Downloads, Zip, USB-Stick): Installation bzw. Aktualisierung anbieten;
        // ist schon dieselbe oder eine neuere Version installiert, öffnet sich stattdessen diese.
        // Gibt true zurück, wenn jetzt die installierte Kopie läuft – dann endet diese hier.
        public static bool BeimStart(string datei)
        {
            if (LaeuftInstalliert)
            {
                new Thread(AlteKopienEntfernen) { IsBackground = true, Priority = ThreadPriority.BelowNormal }.Start();
                return false;
            }
            Version alt = InstallierteVersion, neu = EigeneVersion;
            bool liegengeblieben = LiegtInAblage(Application.ExecutablePath);
            // eine ältere Kopie, oder eine doppelte in Downloads, auf dem Desktop usw.: die installierte Version öffnen
            if (alt != null && (alt > neu || alt == neu && liegengeblieben))
            {
                AlteKopienEntfernen();
                return InstallierteStarten(datei, liegengeblieben);
            }
            if (datei != null) return false;
            const string wegraeumen = " Übrige ShiftDesk-Dateien in Downloads und auf dem Desktop werden danach entfernt, damit es nur eine Version gibt.";
            bool ja;
            if (alt == null)
                ja = GefahrDialog.Vorschlagen(null, "ShiftDesk auf diesem PC installieren?",
                    "ShiftDesk wird in dein Benutzerprofil kopiert und ins Startmenü eingetragen. Das braucht keine Administratorrechte." + wegraeumen +
                    " Du kannst das Programm aber auch ohne Installation direkt benutzen.", "Installieren", "download", "Ohne Installation starten");
            else if (alt < neu)
                ja = GefahrDialog.Vorschlagen(null, "Installiertes ShiftDesk aktualisieren?",
                    "Auf diesem PC ist ShiftDesk " + alt.ToString(3) + " installiert. Diese Datei ist die neuere Version " + neu.ToString(3) + "." + wegraeumen,
                    "Aktualisieren", "sparkles", "Ohne Installation starten");
            else
                return false;   // dieselbe Version ausserhalb von Downloads usw. (z. B. auf einem USB-Stick): einfach starten
            if (!ja) return false;
            if (!Installieren(true)) return false;
            return InstallierteStarten(null, liegengeblieben);
        }

        // Startet die installierte exe; mit «wegraeumen» löscht sie diese Kopie, sobald sie beendet ist
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

        // Nach «Auf diesem PC installieren» in der Seitenleiste: diese Kopie beim Schliessen wegräumen lassen
        public static void BeimBeendenWegraeumen()
        {
            if (LaeuftInstalliert || !LiegtInAblage(Application.ExecutablePath)) return;
            string ich = Path.GetFullPath(Application.ExecutablePath);
            Application.ApplicationExit += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(Exe, EntfernenSchalter + " \"" + ich + "\" /quiet") { WorkingDirectory = Ordner }); }
                catch { }
            };
        }

        // ---- Liegengebliebene Kopien ----
        // Wer neue Versionen per Mail oder Download bekommt, hat sonst bald mehrere ShiftDesk.exe in Downloads und
        // auf dem Desktop, und die Windows-Suche zeigt alle. Weggeräumt werden nur ShiftDesk-Dateien, die nicht neuer
        // sind als die installierte Version, und nur in diesen Ordnern; Zip-Dateien bleiben liegen.

        public const string EntfernenSchalter = "/entfernen";

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int SHGetKnownFolderPath([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStruct)] Guid ordner,
            uint flags, IntPtr token, out string pfad);

        static string DownloadOrdner
        {
            get
            {
                string pfad;
                try
                {
                    if (SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out pfad) == 0) return pfad;
                }
                catch { }
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }
        }

        static string Desktop
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory).TrimEnd('\\'); }
        }

        // Ordner, in denen weitergegebene Dateien typischerweise landen
        static List<string> Ablageordner
        {
            get
            {
                return new[] { DownloadOrdner, Desktop, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) }
                    .Where(o => !string.IsNullOrEmpty(o) && Directory.Exists(o))
                    .Select(o => Path.GetFullPath(o).TrimEnd('\\'))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        static string TempOrdner
        {
            get { return Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\'); }
        }

        static bool IstUnter(string pfad, string ordner)
        {
            return Path.GetFullPath(pfad).StartsWith(ordner + "\\", StringComparison.OrdinalIgnoreCase);
        }

        // In Downloads, Desktop, Dokumente oder im Temp-Ordner (Doppelklick in einer Zip-Datei oder einem Mail-Anhang)
        static bool LiegtInAblage(string pfad)
        {
            return Ablageordner.Concat(new[] { TempOrdner }).Any(o => IstUnter(pfad, o));
        }

        // ShiftDesk*.exe im Ordner und bis zu «tiefe» Unterordner darunter (entpackte Zip-Dateien)
        static List<string> KopienIn(string ordner, int tiefe)
        {
            var liste = new List<string>();
            try { liste.AddRange(Directory.GetFiles(ordner, "ShiftDesk*.exe")); } catch { }
            if (tiefe > 0)
                try
                {
                    foreach (string unter in Directory.GetDirectories(ordner))
                        if ((File.GetAttributes(unter) & (FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System)) == 0)
                            liste.AddRange(KopienIn(unter, tiefe - 1));
                }
                catch { }
            return liste;
        }

        static void AlteKopienEntfernen()
        {
            try
            {
                Version installiert = InstallierteVersion;
                if (installiert == null) return;
                string ich = Path.GetFullPath(Application.ExecutablePath);
                foreach (string ablage in Ablageordner)
                    foreach (string pfad in KopienIn(ablage, 2))
                    {
                        if (string.Equals(pfad, ich, StringComparison.OrdinalIgnoreCase)) continue;   // läuft gerade, das macht /entfernen
                        Version v = ShiftDeskVersion(pfad);
                        if (v != null && v <= installiert) KopieLoeschen(pfad, ablage);
                    }
            }
            catch { }
        }

        // Gibt true zurück, wenn die Datei gelöscht wurde
        static bool KopieLoeschen(string pfad, string ablage)
        {
            pfad = Path.GetFullPath(pfad);
            if (string.Equals(pfad, Path.GetFullPath(Exe), StringComparison.OrdinalIgnoreCase)) return false;
            try { File.Delete(pfad); }
            catch { return false; }
            string ordner = Path.GetDirectoryName(pfad);
            // wer die exe direkt auf dem Desktop hatte, bekommt dort eine Verknüpfung zur installierten Version
            if (string.Equals(ordner, Desktop, StringComparison.OrdinalIgnoreCase))
                try
                {
                    string lnk = Path.Combine(ordner, "ShiftDesk.lnk");
                    if (!File.Exists(lnk)) VerknuepfungErstellen(lnk);
                }
                catch { }
            // den entpackten Ordner mitnehmen, wenn darin nur noch die Anleitung liegt
            while (IstUnter(ordner, ablage))
            {
                try
                {
                    if (Directory.EnumerateFileSystemEntries(ordner).Any(e => !IstAnleitung(e))) break;
                    Directory.Delete(ordner, true);
                }
                catch { break; }
                ordner = Path.GetDirectoryName(ordner);
            }
            return true;
        }

        static bool IstAnleitung(string pfad)
        {
            try
            {
                return string.Equals(Path.GetFileName(pfad), "Anleitung.txt", StringComparison.OrdinalIgnoreCase) &&
                    (File.ReadLines(pfad).FirstOrDefault() ?? "").StartsWith("ShiftDesk");
            }
            catch { return false; }
        }

        // ShiftDesk.exe /entfernen "Pfad": die Kopie löschen, die gerade die installierte Version gestartet hat, sobald
        // sie beendet ist. Mit «warten» (/quiet) ohne Fenster, sonst im Hintergrund, während ShiftDesk normal startet.
        public static void KopieEntfernen(string pfad, bool warten)
        {
            var t = new Thread(() =>
            {
                try
                {
                    string ablage = Ablageordner.Concat(new[] { TempOrdner }).FirstOrDefault(o => IstUnter(pfad, o));
                    if (ablage == null || ShiftDeskVersion(pfad) == null) return;
                    for (int i = 0; i < 100 && File.Exists(pfad); i++)
                    {
                        if (KopieLoeschen(pfad, ablage)) return;
                        Thread.Sleep(300);
                    }
                }
                catch { }
            }) { IsBackground = !warten };
            t.Start();
            if (warten) t.Join();
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        static extern bool DeleteFile(string pfad);

        // Die Herkunftsmarkierung («aus dem Internet») der kopierten exe entfernen,
        // sonst warnt Windows auch beim Start aus dem Startmenü noch einmal.
        static void HerkunftEntfernen(string pfad)
        {
            try { DeleteFile(pfad + ":Zone.Identifier"); } catch { }
        }

        // Gibt true zurück, wenn installiert wurde
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
                    HerkunftEntfernen(Exe);
                }

                VerknuepfungErstellen(Verknuepfung);
                DateizuordnungEintragen();

                string version = EigeneVersion.ToString(3);
                using (var k = Registry.CurrentUser.CreateSubKey(RegistryPfad))
                {
                    k.SetValue("DisplayName", "ShiftDesk");
                    k.SetValue("DisplayVersion", version);
                    k.SetValue("DisplayIcon", Exe + ",0");
                    k.SetValue("Publisher", "ShiftDesk");
                    k.SetValue("InstallLocation", Ordner);
                    k.SetValue("InstallDate", DateTime.Today.ToString("yyyyMMdd"));
                    k.SetValue("UninstallString", "\"" + Exe + "\" /uninstall");
                    k.SetValue("QuietUninstallString", "\"" + Exe + "\" /uninstall /quiet");
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    k.SetValue("EstimatedSize", (int)(new FileInfo(Exe).Length / 1024), RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                Meldung.Fehler(null, "Die Installation ist fehlgeschlagen", ex.Message);
                return false;
            }
            AlteKopienEntfernen();
            if (!leise)
                Meldung.Erfolg(null, "ShiftDesk ist installiert", "Du findest es jetzt im Startmenü und über die Windows-Suche. " +
                    "Deinstallieren kannst du es unter Einstellungen → Apps.");
            return true;
        }

        static void VerknuepfungErstellen(string pfad)
        {
            Type typ = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(typ);
            try
            {
                object lnk = typ.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { pfad });
                Type lt = lnk.GetType();
                lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { Exe });
                lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { Ordner });
                lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { "Ablöseplanung" });
                lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { Exe + ",0" });
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
            }
        }

        // .shiftdesk-Dateien per Doppelklick mit ShiftDesk öffnen (nur für den aktuellen Benutzer)
        const string ProgId = "ShiftDesk.Datei";

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        static extern void SHChangeNotify(int ereignis, uint flags, IntPtr a, IntPtr b);

        static void DateizuordnungEintragen()
        {
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.shiftdesk"))
            {
                k.SetValue("", ProgId);
                k.SetValue("Content Type", "application/x-shiftdesk");
                using (var mit = k.CreateSubKey("OpenWithProgids"))
                    mit.SetValue(ProgId, new byte[0], RegistryValueKind.None);
            }
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId))
            {
                k.SetValue("", "ShiftDesk-Plan");
                k.SetValue("FriendlyTypeName", "ShiftDesk-Plan");
                using (var icon = k.CreateSubKey("DefaultIcon"))
                    icon.SetValue("", "\"" + Exe + "\",0");
                using (var befehl = k.CreateSubKey(@"shell\open\command"))
                    befehl.SetValue("", "\"" + Exe + "\" \"%1\"");
            }
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);   // Explorer: Symbole und Zuordnungen neu laden
        }

        static void DateizuordnungEntfernen()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.shiftdesk", true))
                    if (k != null && (k.GetValue("") as string) == ProgId)
                    {
                        k.Close();
                        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.shiftdesk", false);
                    }
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + ProgId, false);
                SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }

        public static void Deinstallieren(bool leise)
        {
            if (!leise && !GefahrDialog.Fragen(null, "ShiftDesk wirklich deinstallieren?",
                    "Selbst gespeicherte .shiftdesk-Dateien bleiben erhalten.", "Deinstallieren"))
                return;
            if (AndereInstanzLaeuft())
            {
                if (leise) return;
                Meldung.Info(null, "ShiftDesk ist noch geöffnet", "Bitte zuerst schliessen und dann erneut deinstallieren.");
                return;
            }
            bool datenLoeschen = !leise && Directory.Exists(Datenordner) &&
                GefahrDialog.Fragen(null, "Sollen auch deine zuletzt verwendeten Eingaben gelöscht werden?",
                    "Selbst gespeicherte .shiftdesk-Dateien bleiben in jedem Fall erhalten.", "Löschen", "trash-2", "Behalten");

            try { if (File.Exists(Verknuepfung)) File.Delete(Verknuepfung); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(RegistryPfad, false); } catch { }
            DateizuordnungEntfernen();
            if (datenLoeschen)
                try { Directory.Delete(Datenordner, true); } catch { }

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
