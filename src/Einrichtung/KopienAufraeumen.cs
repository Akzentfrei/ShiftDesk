using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ShiftDesk.Einrichtung
{
    /// <summary>
    /// Räumt liegengebliebene ShiftDesk-Kopien weg. Wer neue Versionen per Mail oder Download bekommt, hat sonst
    /// bald mehrere ShiftDesk.exe in Downloads und auf dem Desktop, und die Windows-Suche zeigt alle.
    /// </summary>
    /// <remarks>
    /// Weggeräumt werden nur ShiftDesk-Dateien, die nicht neuer sind als die installierte Version, und nur in den
    /// <see cref="Ablageordner"/>; Zip-Dateien bleiben liegen. Ein entpackter Ordner, in dem danach nur noch die
    /// Anleitung liegt, verschwindet mit; eine exe direkt auf dem Desktop wird durch eine Verknüpfung ersetzt.
    /// </remarks>
    public static class KopienAufraeumen
    {
        static readonly Guid DownloadsId = new Guid("374DE290-123F-4565-9164-39C4925E467B");

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid ordner, uint flags, IntPtr token, out string pfad);

        static string DownloadOrdner
        {
            get
            {
                string pfad;
                try
                {
                    if (SHGetKnownFolderPath(DownloadsId, 0, IntPtr.Zero, out pfad) == 0) return pfad;
                }
                catch { }
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }
        }

        static string Desktop
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory).TrimEnd('\\'); }
        }

        /// <summary>Ordner, in denen weitergegebene Dateien typischerweise landen: Downloads, Desktop, Dokumente.</summary>
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

        /// <summary>Liegt die Datei in Downloads, Desktop, Dokumente oder im Temp-Ordner (Doppelklick in einer Zip-Datei oder einem Mail-Anhang)?</summary>
        public static bool LiegtInAblage(string pfad)
        {
            return Ablageordner.Concat(new[] { TempOrdner }).Any(o => IstUnter(pfad, o));
        }

        /// <summary>ShiftDesk*.exe im Ordner und bis zu <paramref name="tiefe"/> Unterordner darunter (entpackte Zip-Dateien).</summary>
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

        /// <summary>Löscht alle Kopien in den Ablageordnern, die nicht neuer sind als die installierte Version.</summary>
        public static void AlteKopienEntfernen()
        {
            try
            {
                Version installiert = Installation.InstallierteVersion;
                if (installiert == null) return;
                string ich = Path.GetFullPath(Application.ExecutablePath);
                foreach (string ablage in Ablageordner)
                    foreach (string pfad in KopienIn(ablage, 2))
                    {
                        if (string.Equals(pfad, ich, StringComparison.OrdinalIgnoreCase)) continue;   // läuft gerade, das macht /entfernen
                        Version v = Installation.ShiftDeskVersion(pfad);
                        if (v != null && v <= installiert) KopieLoeschen(pfad, ablage);
                    }
            }
            catch { }
        }

        /// <summary>
        /// <c>ShiftDesk.exe /entfernen "Pfad"</c>: die Kopie löschen, die gerade die installierte Version gestartet hat,
        /// sobald sie beendet ist.
        /// </summary>
        /// <param name="pfad">Die Kopie; nur in einem Ablage- oder Temp-Ordner und nur, wenn es eine ShiftDesk-exe ist.</param>
        /// <param name="warten">Ohne Fenster warten, bis es erledigt ist (<c>/quiet</c>); sonst im Hintergrund, während ShiftDesk normal startet.</param>
        public static void KopieEntfernen(string pfad, bool warten)
        {
            var t = new Thread(() =>
            {
                try
                {
                    string ablage = Ablageordner.Concat(new[] { TempOrdner }).FirstOrDefault(o => IstUnter(pfad, o));
                    if (ablage == null || Installation.ShiftDeskVersion(pfad) == null) return;
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

        /// <summary>Löscht eine Kopie und räumt danach auf (Verknüpfung auf dem Desktop, leerer entpackter Ordner).</summary>
        /// <returns>true, wenn die Datei gelöscht wurde.</returns>
        static bool KopieLoeschen(string pfad, string ablage)
        {
            pfad = Path.GetFullPath(pfad);
            if (string.Equals(pfad, Path.GetFullPath(Installation.Exe), StringComparison.OrdinalIgnoreCase)) return false;
            try { File.Delete(pfad); }
            catch { return false; }
            string ordner = Path.GetDirectoryName(pfad);
            // wer die exe direkt auf dem Desktop hatte, bekommt dort eine Verknüpfung zur installierten Version
            if (string.Equals(ordner, Desktop, StringComparison.OrdinalIgnoreCase))
                try
                {
                    string lnk = Path.Combine(ordner, "ShiftDesk.lnk");
                    if (!File.Exists(lnk)) Windowseintraege.VerknuepfungErstellen(lnk);
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

        /// <summary>Ist das die mitgelieferte Anleitung.txt (erste Zeile beginnt mit «ShiftDesk»)?</summary>
        static bool IstAnleitung(string pfad)
        {
            try
            {
                return string.Equals(Path.GetFileName(pfad), "Anleitung.txt", StringComparison.OrdinalIgnoreCase) &&
                    (File.ReadLines(pfad).FirstOrDefault() ?? "").StartsWith("ShiftDesk");
            }
            catch { return false; }
        }
    }
}
