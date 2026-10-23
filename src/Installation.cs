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
                    MessageBox.Show("ShiftDesk ist noch geöffnet. Bitte zuerst schliessen und dann erneut installieren.", "ShiftDesk installieren", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
            }
            try
            {
                Directory.CreateDirectory(Ordner);
                if (!LaeuftInstalliert)
                    File.Copy(Application.ExecutablePath, Exe, true);

                VerknuepfungErstellen();
                DateizuordnungEintragen();

                string version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
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
                MessageBox.Show("Die Installation ist fehlgeschlagen:\n" + ex.Message, "ShiftDesk installieren", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (!leise)
                MessageBox.Show("ShiftDesk ist installiert.\n\nDu findest es jetzt im Startmenü und über die Windows-Suche. " +
                    "Deinstallieren kannst du es unter Einstellungen → Apps.", "ShiftDesk installieren", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            if (!leise && MessageBox.Show("ShiftDesk wirklich deinstallieren?", "ShiftDesk deinstallieren",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            if (AndereInstanzLaeuft())
            {
                if (leise) return;
                MessageBox.Show("ShiftDesk ist noch geöffnet. Bitte zuerst schliessen und dann erneut deinstallieren.", "ShiftDesk deinstallieren", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            bool datenLoeschen = !leise && Directory.Exists(Datenordner) &&
                MessageBox.Show("Sollen auch deine zuletzt verwendeten Eingaben gelöscht werden?\n\n(Selbst gespeicherte .shiftdesk-Dateien bleiben in jedem Fall erhalten.)",
                    "ShiftDesk deinstallieren", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

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
                MessageBox.Show("ShiftDesk wurde deinstalliert.", "ShiftDesk deinstallieren", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
