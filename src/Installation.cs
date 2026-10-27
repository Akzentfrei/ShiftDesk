using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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

        // Version der installierten exe, null wenn keine da ist
        static Version InstallierteVersion
        {
            get
            {
                if (!File.Exists(Exe)) return null;
                try
                {
                    string v = FileVersionInfo.GetVersionInfo(Exe).FileVersion;
                    Version ergebnis;
                    return Version.TryParse(v, out ergebnis) ? ergebnis : new Version(0, 0);
                }
                catch { return new Version(0, 0); }
            }
        }

        // Beim Start aus einem fremden Ordner (Downloads, Zip, USB-Stick): Installation bzw. Aktualisierung anbieten.
        // Gibt true zurück, wenn installiert wurde und die installierte Kopie läuft – dann endet diese hier.
        public static bool BeimStartAnbieten()
        {
            if (LaeuftInstalliert) return false;
            Version alt = InstallierteVersion, neu = EigeneVersion;
            bool ja;
            if (alt == null)
                ja = GefahrDialog.Vorschlagen(null, "ShiftDesk auf diesem PC installieren?",
                    "ShiftDesk wird in dein Benutzerprofil kopiert und ins Startmenü eingetragen. Das braucht keine Administratorrechte. " +
                    "Du kannst das Programm aber auch ohne Installation direkt benutzen.", "Installieren", "download", "Ohne Installation starten");
            else if (alt < neu)
                ja = GefahrDialog.Vorschlagen(null, "Installiertes ShiftDesk aktualisieren?",
                    "Auf diesem PC ist ShiftDesk " + alt.ToString(3) + " installiert. Diese Datei ist die neuere Version " + neu.ToString(3) + ".",
                    "Aktualisieren", "sparkles", "Ohne Installation starten");
            else
                return false;   // dieselbe oder eine neuere Version ist schon installiert: einfach starten
            if (!ja) return false;
            if (!Installieren(true)) return false;
            try
            {
                Process.Start(Exe);
                return true;
            }
            catch
            {
                return false;
            }
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

                VerknuepfungErstellen();
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
            if (!leise)
                Meldung.Erfolg(null, "ShiftDesk ist installiert", "Du findest es jetzt im Startmenü und über die Windows-Suche. " +
                    "Deinstallieren kannst du es unter Einstellungen → Apps.");
            return true;
        }

        static void VerknuepfungErstellen()
        {
            Type typ = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(typ);
            try
            {
                object lnk = typ.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { Verknuepfung });
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
