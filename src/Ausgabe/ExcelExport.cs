using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using ShiftDesk.Planung;
using S = ShiftDesk.Ausgabe.ExcelStile;

namespace ShiftDesk.Ausgabe
{
    /// <summary>
    /// Schreibt den Plan als Excel-Datei (.xlsx, Office Open XML) – ohne Excel oder Zusatzbibliotheken.
    /// Blatt 1: Plan mit Postenfarben, Blatt 2: Statistik.
    /// </summary>
    public static class ExcelExport
    {
        static readonly CultureInfo De = new CultureInfo("de-CH");

        /// <summary>Spalte A Grad, B Name, C Funktion, ab D die Stunden.</summary>
        const int ErsteStundenSpalte = 3;

        /// <summary>Speichert den Plan; eine vorhandene Datei wird ersetzt.</summary>
        /// <exception cref="IOException">Die Datei ist z. B. noch in Excel geöffnet.</exception>
        public static void Speichern(Plan plan, string pfad)
        {
            if (File.Exists(pfad)) File.Delete(pfad);
            using (var fs = File.Create(pfad))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                Eintrag(zip, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                    "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                    "<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                    "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                    "</Types>");
                Eintrag(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");
                Eintrag(zip, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                    "<sheets><sheet name=\"Plan\" sheetId=\"1\" r:id=\"rId1\"/><sheet name=\"Statistik\" sheetId=\"2\" r:id=\"rId2\"/></sheets>" +
                    "<definedNames><definedName name=\"_xlnm.Print_Titles\" localSheetId=\"0\">Plan!$A:$C,Plan!$4:$5</definedName></definedNames>" +
                    "</workbook>");
                Eintrag(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                    "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/>" +
                    "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                    "</Relationships>");
                Eintrag(zip, "xl/styles.xml", S.Xml(plan.Cfg));
                Eintrag(zip, "xl/worksheets/sheet1.xml", PlanBlatt(plan));
                Eintrag(zip, "xl/worksheets/sheet2.xml", StatistikBlatt(plan));
            }
        }

        static void Eintrag(ZipArchive zip, string name, string inhalt)
        {
            var e = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false)))
                w.Write(inhalt);
        }

        /// <summary>
        /// Blatt «Plan»: Titel, Tage und Stunden als Kopf, dann eine Zeile pro Person mit dem Posten jeder Stunde
        /// in seiner Farbe; darunter Legende und nicht eingeteilte Personen.
        /// </summary>
        static string PlanBlatt(Plan plan)
        {
            var cfg = plan.Cfg;
            var zeilen = new List<ExcelZeile>();
            var verbunden = new List<string>();
            int letzte = ErsteStundenSpalte + plan.Stunden - 1;
            DateTime ende = plan.Start.AddHours(plan.Stunden);

            zeilen.Add(new ExcelZeile(1, 26).Text(0, "Wachtablösungsliste", S.Titel));
            zeilen.Add(new ExcelZeile(2).Text(0, (string.IsNullOrEmpty(cfg.Name) ? "" : cfg.Name + "   ·   ") + (cfg.Tagesplan ? "Jeden Tag" :
                plan.Start.ToString("ddd dd.MM.yyyy HH:00", De) + " – " + ende.ToString("ddd dd.MM.yyyy HH:00", De)), S.Untertitel));

            // Tage (verbunden über ihre Stunden) und Stunden
            var tage = new ExcelZeile(4, 20);
            for (int t = 0; t < plan.Stunden; t++)
            {
                DateTime z = plan.Start.AddHours(t);
                if (t != 0 && z.Hour != 0) continue;
                int bis = t;
                while (bis + 1 < plan.Stunden && plan.Start.AddHours(bis + 1).Hour != 0) bis++;
                tage.Text(ErsteStundenSpalte + t, cfg.Tagesplan ? "Jeden Tag" : z.ToString("dddd, dd.MM.yyyy", De), S.Tag);
                if (bis > t) verbunden.Add(ExcelBlatt.Spalte(ErsteStundenSpalte + t) + "4:" + ExcelBlatt.Spalte(ErsteStundenSpalte + bis) + "4");
            }
            zeilen.Add(tage);
            var kopf = new ExcelZeile(5, 20).Text(0, "Grad", S.Kopf).Text(1, "Name", S.Kopf).Text(2, "Funktion", S.Kopf);
            for (int t = 0; t < plan.Stunden; t++)
                kopf.Text(ErsteStundenSpalte + t, plan.Start.AddHours(t).ToString("HH:00"), S.Kopf);
            zeilen.Add(kopf);

            // Personen; zwischen den Blöcken (z. B. Kdt / Wache) eine schmale Leerzeile
            int nr = 6;
            for (int r = 0; r < plan.Zeilen.Count; r++)
            {
                if (r > 0 && plan.Block[r] != plan.Block[r - 1])
                    zeilen.Add(new ExcelZeile(nr++, 8));
                var p = plan.Zeilen[r];
                var z = new ExcelZeile(nr++, 22).Text(0, p.Grad, S.Grad).Text(1, p.Name + (p.Fahrer ? " 🚗" : ""), S.Name).Text(2, p.Funktion, S.Grad);
                for (int t = 0; t < plan.Stunden; t++)
                {
                    int k = plan.Z[r, t];
                    if (k == Plan.RUHE && plan.Essen[r, t]) z.Text(ErsteStundenSpalte + t, "🍴", S.Ruhe);
                    else if (k == Plan.RUHE) z.Leer(ErsteStundenSpalte + t, S.Ruhe);
                    else if (k < 0) z.Leer(ErsteStundenSpalte + t, S.Frei);
                    else z.Text(ErsteStundenSpalte + t, cfg.Posten[k].Name + (plan.Essen[r, t] ? " 🍴" : plan.EssenAbloesung[r, t] ? " 🍴*" : ""), S.ErsterPosten + k);
                }
                zeilen.Add(z);
            }

            // Legende und nicht eingeteilte Personen
            nr++;
            var legende = new ExcelZeile(nr++, 20).Text(1, "Legende", S.Kopf);
            int spalte = ErsteStundenSpalte;
            for (int k = 0; k < cfg.Posten.Count; k++, spalte += 3)
            {
                legende.Text(spalte, cfg.Posten[k].Name + (cfg.Posten[k].Draussen ? " ❄" : ""), S.ErsterPosten + k);
                verbunden.Add(ExcelBlatt.Spalte(spalte) + (nr - 1) + ":" + ExcelBlatt.Spalte(spalte + 2) + (nr - 1));
            }
            if (cfg.Essenszeiten.Count > 0) { legende.Text(spalte, "🍴 = Essen", S.Normal); spalte += 3; }
            if (plan.EssenAbloesung.Cast<bool>().Any(x => x)) { legende.Text(spalte, "🍴* = Essen, Reserve löst ab", S.Normal); spalte += 3; }
            if (cfg.Posten.Any(p => p.Draussen)) { legende.Text(spalte, "❄ = Draussen", S.Normal); spalte += 3; }
            if (plan.Zeilen.Any(p => p.Fahrer)) legende.Text(spalte, "🚗 = Fahrer", S.Normal);
            zeilen.Add(legende);
            if (plan.NichtEingeteilt.Count > 0)
                zeilen.Add(new ExcelZeile(nr++, 20).Text(1, "Nicht eingeteilt", S.Kopf).Text(ErsteStundenSpalte,
                    string.Join("     ", plan.NichtEingeteilt.GroupBy(p => p.Funktion)
                        .Select(g => g.Key + ": " + string.Join(", ", g.Select(p => p.Anzeigename)))), S.Normal));
            zeilen.Add(new ExcelZeile(nr + 1).Text(1, "Erstellt mit ShiftDesk am " + DateTime.Now.ToString("dd.MM.yyyy HH:mm"), S.Untertitel));

            string spalten = "<cols><col min=\"1\" max=\"1\" width=\"7\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"24\" customWidth=\"1\"/>" +
                             "<col min=\"3\" max=\"3\" width=\"14\" customWidth=\"1\"/>" +
                             "<col min=\"4\" max=\"" + (letzte + 1) + "\" width=\"10\" customWidth=\"1\"/></cols>";
            string ansicht = "<pane xSplit=\"3\" ySplit=\"5\" topLeftCell=\"D6\" activePane=\"bottomRight\" state=\"frozen\"/>";
            return ExcelBlatt.Xml(ansicht, spalten, zeilen, verbunden, true);
        }

        /// <summary>Blatt «Statistik»: Stunden pro Person und Posten, Essensstunden und Dienststunden.</summary>
        static string StatistikBlatt(Plan plan)
        {
            var cfg = plan.Cfg;
            var zeilen = new List<ExcelZeile>();
            zeilen.Add(new ExcelZeile(1, 26).Text(0, "Statistik", S.Titel));
            zeilen.Add(new ExcelZeile(2).Text(0, "Stunden pro Person und Posten", S.Untertitel));
            var kopf = new ExcelZeile(4, 22).Text(0, "Grad", S.Kopf).Text(1, "Name", S.Kopf).Text(2, "Funktion", S.Kopf);
            for (int k = 0; k < cfg.Posten.Count; k++) kopf.Text(3 + k, cfg.Posten[k].Name, S.ErsterPosten + k);
            kopf.Text(3 + cfg.Posten.Count, "Essen", S.Kopf).Text(4 + cfg.Posten.Count, "Dienst-Std.", S.Kopf);
            zeilen.Add(kopf);
            for (int r = 0; r < plan.Zeilen.Count; r++)
            {
                var p = plan.Zeilen[r];
                var z = new ExcelZeile(5 + r, 20).Text(0, p.Grad, S.Grad).Text(1, p.Name + (p.Fahrer ? " 🚗" : ""), S.Name).Text(2, p.Funktion, S.Grad);
                int total = 0;
                for (int k = 0; k < cfg.Posten.Count; k++)
                {
                    int c = plan.StundenAuf(r, k);
                    total += c;
                    z.Zahl(3 + k, c, S.Zahl);
                }
                z.Zahl(3 + cfg.Posten.Count, plan.Essensstunden(r), S.Zahl).Zahl(4 + cfg.Posten.Count, total, S.Zahl);
                zeilen.Add(z);
            }
            string spalten = "<cols><col min=\"1\" max=\"1\" width=\"7\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"24\" customWidth=\"1\"/>" +
                             "<col min=\"3\" max=\"3\" width=\"14\" customWidth=\"1\"/>" +
                             "<col min=\"4\" max=\"" + (5 + cfg.Posten.Count) + "\" width=\"14\" customWidth=\"1\"/></cols>";
            return ExcelBlatt.Xml("<pane ySplit=\"4\" topLeftCell=\"A5\" activePane=\"bottomLeft\" state=\"frozen\"/>", spalten, zeilen, new string[0], false);
        }
    }
}
