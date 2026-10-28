using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ShiftDesk.Ausgabe;
using ShiftDesk.Oberflaeche.Dialoge;
using ShiftDesk.Planung;

namespace ShiftDesk.Oberflaeche.Seiten
{
    /// <summary>Die Ausgabe-Knöpfe der Plan-Seite: Drucken, Als PDF und Excel, mit Dateiauswahl und Meldungen.</summary>
    public static class PlanAusgabe
    {
        /// <summary>Druckerauswahl von Windows, dann drucken.</summary>
        public static void Drucken(Form fenster, Plan plan)
        {
            using (var pd = PlanDruck.Dokument(plan, null))
            using (var dlg = new PrintDialog { Document = pd, UseEXDialog = true, AllowSomePages = false })
            {
                if (dlg.ShowDialog(fenster) != DialogResult.OK) return;
                try { pd.Print(); }
                catch (Exception ex) { Meldung.Fehler(fenster, "Drucken fehlgeschlagen", ex.Message); }
            }
        }

        /// <summary>Speichert den Plan über den Drucker «Microsoft Print to PDF» als PDF-Datei.</summary>
        public static void AlsPdf(Form fenster, Plan plan)
        {
            string pfad = Zieldatei(fenster, plan, "PDF-Datei", ".pdf");
            if (pfad == null) return;
            using (var pd = PlanDruck.Dokument(plan, PlanDruck.PdfDrucker))
            {
                if (!pd.PrinterSettings.IsValid)
                {
                    Meldung.Warnung(fenster, "PDF-Drucker fehlt",
                        "Der Drucker «" + PlanDruck.PdfDrucker + "» ist auf diesem PC nicht installiert. Bitte «Drucken» verwenden.");
                    return;
                }
                pd.PrinterSettings.PrintToFile = true;
                pd.PrinterSettings.PrintFileName = pfad;
                try
                {
                    if (File.Exists(pfad)) File.Delete(pfad);
                    pd.Print();
                }
                catch (Exception ex)
                {
                    Meldung.Fehler(fenster, "PDF konnte nicht gespeichert werden", ex.Message);
                    return;
                }
            }
            AufPdfWarten(fenster, pfad);
        }

        /// <summary>
        /// Die Warteschlange schreibt die PDF-Datei erst nach dem Drucken fertig: warten, bis sie da ist und niemand
        /// mehr daran schreibt (höchstens 15 Sekunden), ohne das Fenster zu blockieren; erst dann «gespeichert» melden.
        /// </summary>
        static void AufPdfWarten(Form fenster, string pfad)
        {
            var start = DateTime.Now;
            var warten = new Timer { Interval = 150 };
            fenster.Cursor = Cursors.AppStarting;
            warten.Tick += (s, e) =>
            {
                bool fertig = DateiFertig(pfad);
                if (!fertig && (DateTime.Now - start).TotalSeconds < 15) return;
                warten.Stop();
                warten.Dispose();
                if (fenster.IsDisposed) return;
                fenster.Cursor = Cursors.Default;
                if (fertig) Gespeichert(fenster, "PDF gespeichert", pfad);
                else Meldung.Warnung(fenster, "PDF noch nicht fertig", "Die Datei ist noch nicht geschrieben. Sie erscheint gleich hier:\n\n" + pfad);
            };
            warten.Start();
        }

        /// <summary>Ist die Datei da, nicht leer und von niemandem mehr geöffnet?</summary>
        static bool DateiFertig(string pfad)
        {
            try
            {
                using (var fs = File.Open(pfad, FileMode.Open, FileAccess.Read, FileShare.None))
                    return fs.Length > 0;
            }
            catch { return false; }
        }

        /// <summary>Speichert den Plan als Excel-Datei (.xlsx).</summary>
        public static void AlsExcel(Form fenster, Plan plan)
        {
            string pfad = Zieldatei(fenster, plan, "Excel-Datei", ".xlsx");
            if (pfad == null) return;
            try
            {
                ExcelExport.Speichern(plan, pfad);
            }
            catch (Exception ex)
            {
                Meldung.Fehler(fenster, "Excel-Datei konnte nicht gespeichert werden",
                    ex.Message + "\n\nIst sie vielleicht noch in Excel geöffnet?");
                return;
            }
            Gespeichert(fenster, "Excel-Datei gespeichert", pfad);
        }

        /// <summary>Speichern-Dialog mit einem Vorschlag aus Name und Startdatum des Plans; null = abgebrochen.</summary>
        static string Zieldatei(Form fenster, Plan plan, string beschreibung, string endung)
        {
            using (var dlg = new SaveFileDialog
            {
                Filter = beschreibung + " (*" + endung + ")|*" + endung,
                FileName = DateiName(plan.Cfg.Name) + " " + plan.Start.ToString("yyyy-MM-dd") + endung,
            })
                return dlg.ShowDialog(fenster) == DialogResult.OK ? dlg.FileName : null;
        }

        /// <summary>Name als Dateiname (ohne Zeichen, die Windows nicht erlaubt); leer = «Wachtplan».</summary>
        static string DateiName(string name)
        {
            string s = new string((name ?? "").Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim();
            return s == "" ? "Wachtplan" : s;
        }

        /// <summary>Nach dem Speichern einer Ausgabe anbieten, sie gleich zu öffnen.</summary>
        static void Gespeichert(Form fenster, string titel, string pfad)
        {
            if (!Rueckfrage.Vorschlagen(fenster, titel, pfad + "\n\nJetzt öffnen?", "Öffnen", "file-check")) return;
            try { System.Diagnostics.Process.Start(pfad); }
            catch (Exception ex) { Meldung.Fehler(fenster, "Datei konnte nicht geöffnet werden", ex.Message); }
        }
    }
}
