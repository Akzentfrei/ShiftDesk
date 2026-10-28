using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ShiftDesk.Einrichtung
{
    /// <summary>
    /// Was die Installation bei Windows einträgt, alles unter dem aktuellen Benutzer: Verknüpfungen,
    /// die Dateizuordnung für .shiftdesk und den Eintrag unter Einstellungen → Apps.
    /// </summary>
    static class Windowseintraege
    {
        const string AppSchluessel = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ShiftDesk";
        const string ProgId = "ShiftDesk.Datei";

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool DeleteFile(string pfad);

        [DllImport("shell32.dll")]
        static extern void SHChangeNotify(int ereignis, uint flags, IntPtr a, IntPtr b);

        /// <summary>Verknüpfung im Startmenü (darüber findet auch die Windows-Suche ShiftDesk).</summary>
        public static string Startmenue
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "ShiftDesk.lnk"); }
        }

        /// <summary>
        /// Die Herkunftsmarkierung («aus dem Internet») der kopierten exe entfernen,
        /// sonst warnt Windows auch beim Start aus dem Startmenü noch einmal.
        /// </summary>
        public static void HerkunftEntfernen(string pfad)
        {
            try { DeleteFile(pfad + ":Zone.Identifier"); } catch { }
        }

        /// <summary>Legt eine Verknüpfung zur installierten exe an (über WScript.Shell).</summary>
        public static void VerknuepfungErstellen(string pfad)
        {
            Type typ = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(typ);
            try
            {
                object lnk = typ.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { pfad });
                Type lt = lnk.GetType();
                lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { Installation.Exe });
                lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { Installation.Ordner });
                lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { "Ablöseplanung" });
                lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { Installation.Exe + ",0" });
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
            }
            finally
            {
                Marshal.ReleaseComObject(shell);
            }
        }

        /// <summary>Eintrag unter Einstellungen → Apps, mit Deinstallieren über <c>ShiftDesk.exe /uninstall</c>.</summary>
        public static void AppEintragen(string version)
        {
            string exe = Installation.Exe;
            using (var k = Registry.CurrentUser.CreateSubKey(AppSchluessel))
            {
                k.SetValue("DisplayName", "ShiftDesk");
                k.SetValue("DisplayVersion", version);
                k.SetValue("DisplayIcon", exe + ",0");
                k.SetValue("Publisher", "ShiftDesk");
                k.SetValue("InstallLocation", Installation.Ordner);
                k.SetValue("InstallDate", DateTime.Today.ToString("yyyyMMdd"));
                k.SetValue("UninstallString", "\"" + exe + "\" /uninstall");
                k.SetValue("QuietUninstallString", "\"" + exe + "\" /uninstall /quiet");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
            }
        }

        /// <summary>Entfernt den Eintrag unter Einstellungen → Apps.</summary>
        public static void AppAustragen()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(AppSchluessel, false); } catch { }
        }

        /// <summary>.shiftdesk-Dateien per Doppelklick mit ShiftDesk öffnen (nur für den aktuellen Benutzer).</summary>
        public static void DateizuordnungEintragen()
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
                    icon.SetValue("", "\"" + Installation.Exe + "\",0");
                using (var befehl = k.CreateSubKey(@"shell\open\command"))
                    befehl.SetValue("", "\"" + Installation.Exe + "\" \"%1\"");
            }
            SymboleNeuLaden();
        }

        /// <summary>Entfernt die Dateizuordnung, sofern .shiftdesk noch auf ShiftDesk zeigt.</summary>
        public static void DateizuordnungEntfernen()
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
                SymboleNeuLaden();
            }
            catch { }
        }

        /// <summary>Explorer: Symbole und Dateizuordnungen neu laden.</summary>
        static void SymboleNeuLaden()
        {
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
