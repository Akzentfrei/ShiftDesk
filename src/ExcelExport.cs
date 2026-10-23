using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;

namespace ShiftDesk
{
    // Schreibt den Plan als Excel-Datei (.xlsx, Office Open XML) – ohne Excel oder Zusatzbibliotheken.
    // Blatt 1: Plan mit Postenfarben, Blatt 2: Statistik.
    public static class ExcelExport
    {
        static readonly CultureInfo De = new CultureInfo("de-CH");
        const int ErsteStundenSpalte = 3;   // A Grad, B Name, C Funktion, ab D die Stunden

        // Stil-Nummern (Reihenfolge in cellXfs)
        const int sNormal = 0, sTitel = 1, sUntertitel = 2, sKopf = 3, sTag = 4, sName = 5, sGrad = 6, sRuhe = 7, sFrei = 8, sZahl = 9, sErsterPosten = 10;

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
                Eintrag(zip, "xl/styles.xml", Stile(plan.Cfg));
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

        // ---------------------------------------------------------------- Stile

        static string Hex(Color c)
        {
            return "FF" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        static bool Dunkel(Color c)
        {
            return (c.R * 299 + c.G * 587 + c.B * 114) / 1000 < 150;
        }

        static string Stile(Konfiguration cfg)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            // Schriften: 0 normal, 1 Titel, 2 grau, 3 grau fett, 4 fett, 5 weiss klein, 6 dunkel klein, 7 hellgrau klein
            sb.Append("<fonts count=\"8\">");
            sb.Append("<font><sz val=\"10\"/><color rgb=\"FF111827\"/><name val=\"Segoe UI\"/></font>");
            sb.Append("<font><b/><sz val=\"16\"/><color rgb=\"FF111827\"/><name val=\"Segoe UI\"/></font>");
            sb.Append("<font><sz val=\"10\"/><color rgb=\"FF6B7280\"/><name val=\"Segoe UI\"/></font>");
            sb.Append("<font><b/><sz val=\"9\"/><color rgb=\"FF6B7280\"/><name val=\"Segoe UI\"/></font>");
            sb.Append("<font><b/><sz val=\"10\"/><color rgb=\"FF111827\"/><name val=\"Segoe UI\"/></font>");
            sb.Append("<font><sz val=\"8\"/><color rgb=\"FFFFFFFF\"/><name val=\"Segoe UI\"/></font>");
            sb.Append("<font><sz val=\"8\"/><color rgb=\"FF111827\"/><name val=\"Segoe UI\"/></font>");
            sb.Append("<font><sz val=\"8\"/><color rgb=\"FF9CA3AF\"/><name val=\"Segoe UI\"/></font>");
            sb.Append("</fonts>");
            // Füllungen: 0 keine, 1 gray125 (Pflicht), 2 Ruhe, ab 3 Posten
            sb.Append("<fills count=\"" + (3 + cfg.Posten.Count) + "\">");
            sb.Append("<fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>");
            sb.Append("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF3F4F6\"/><bgColor indexed=\"64\"/></patternFill></fill>");
            foreach (var p in cfg.Posten)
                sb.Append("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"" + Hex(Color.FromArgb(p.Farbe)) + "\"/><bgColor indexed=\"64\"/></patternFill></fill>");
            sb.Append("</fills>");
            // Rahmen: 0 keiner, 1 weisse Trennlinien (Kachel-Optik), 2 feine Linie unten
            sb.Append("<borders count=\"3\">");
            sb.Append("<border><left/><right/><top/><bottom/><diagonal/></border>");
            sb.Append("<border><left style=\"thin\"><color rgb=\"FFFFFFFF\"/></left><right style=\"thin\"><color rgb=\"FFFFFFFF\"/></right>" +
                      "<top style=\"thin\"><color rgb=\"FFFFFFFF\"/></top><bottom style=\"thin\"><color rgb=\"FFFFFFFF\"/></bottom><diagonal/></border>");
            sb.Append("<border><left/><right/><top/><bottom style=\"thin\"><color rgb=\"FFE5E7EB\"/></bottom><diagonal/></border>");
            sb.Append("</borders>");
            sb.Append("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");

            var xfs = new List<string>
            {
                Xf(0, 0, 0, null),                                    // sNormal
                Xf(1, 0, 0, null),                                    // sTitel
                Xf(2, 0, 0, null),                                    // sUntertitel
                Xf(3, 0, 2, "horizontal=\"center\" vertical=\"center\""),        // sKopf
                Xf(4, 0, 0, "vertical=\"center\""),                   // sTag
                Xf(0, 0, 2, "vertical=\"center\""),                   // sName
                Xf(2, 0, 2, "vertical=\"center\""),                   // sGrad
                Xf(7, 2, 1, "horizontal=\"center\" vertical=\"center\""),        // sRuhe
                Xf(0, 0, 1, null),                                    // sFrei
                Xf(0, 0, 2, "horizontal=\"center\" vertical=\"center\""),        // sZahl
            };
            for (int k = 0; k < cfg.Posten.Count; k++)
                xfs.Add(Xf(Dunkel(Color.FromArgb(cfg.Posten[k].Farbe)) ? 5 : 6, 3 + k, 1, "horizontal=\"center\" vertical=\"center\" shrinkToFit=\"1\""));
            sb.Append("<cellXfs count=\"" + xfs.Count + "\">" + string.Concat(xfs) + "</cellXfs>");
            sb.Append("<cellStyles count=\"1\"><cellStyle name=\"Standard\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
            sb.Append("</styleSheet>");
            return sb.ToString();
        }

        static string Xf(int font, int fill, int rand, string ausrichtung)
        {
            return "<xf numFmtId=\"0\" fontId=\"" + font + "\" fillId=\"" + fill + "\" borderId=\"" + rand + "\" xfId=\"0\"" +
                   " applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"" + (ausrichtung != null ? " applyAlignment=\"1\"><alignment " + ausrichtung + "/></xf>" : "/>");
        }

        // ---------------------------------------------------------------- Blätter

        static string Spalte(int i)
        {
            string s = "";
            for (i++; i > 0; i = (i - 1) / 26)
                s = (char)('A' + (i - 1) % 26) + s;
            return s;
        }

        class Zeile
        {
            readonly StringBuilder sb = new StringBuilder();
            readonly int nr;
            readonly double? hoehe;

            public Zeile(int nr, double? hoehe = null)
            {
                this.nr = nr;
                this.hoehe = hoehe;
            }

            public Zeile Text(int spalte, string text, int stil)
            {
                sb.Append("<c r=\"" + Spalte(spalte) + nr + "\" s=\"" + stil + "\" t=\"inlineStr\"><is><t xml:space=\"preserve\">" +
                          SecurityElement.Escape(text ?? "") + "</t></is></c>");
                return this;
            }

            public Zeile Zahl(int spalte, int wert, int stil)
            {
                sb.Append("<c r=\"" + Spalte(spalte) + nr + "\" s=\"" + stil + "\"><v>" + wert + "</v></c>");
                return this;
            }

            public Zeile Leer(int spalte, int stil)
            {
                sb.Append("<c r=\"" + Spalte(spalte) + nr + "\" s=\"" + stil + "\"/>");
                return this;
            }

            public override string ToString()
            {
                return "<row r=\"" + nr + "\"" + (hoehe.HasValue ? " ht=\"" + hoehe.Value.ToString(CultureInfo.InvariantCulture) + "\" customHeight=\"1\"" : "") + ">" + sb + "</row>";
            }
        }

        static string Blatt(string ansicht, string spalten, IEnumerable<Zeile> zeilen, IEnumerable<string> verbunden, bool quer)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            sb.Append("<sheetPr><pageSetUpPr fitToPage=\"1\"/></sheetPr>");
            sb.Append("<sheetViews><sheetView workbookViewId=\"0\" showGridLines=\"0\">" + ansicht + "</sheetView></sheetViews>");
            sb.Append("<sheetFormatPr defaultRowHeight=\"15\"/>");
            sb.Append(spalten);
            sb.Append("<sheetData>" + string.Concat(zeilen) + "</sheetData>");
            var liste = verbunden.ToList();
            if (liste.Count > 0)
                sb.Append("<mergeCells count=\"" + liste.Count + "\">" + string.Concat(liste.Select(m => "<mergeCell ref=\"" + m + "\"/>")) + "</mergeCells>");
            sb.Append("<pageMargins left=\"0.4\" right=\"0.4\" top=\"0.5\" bottom=\"0.5\" header=\"0.3\" footer=\"0.3\"/>");
            sb.Append("<pageSetup paperSize=\"9\" orientation=\"" + (quer ? "landscape" : "portrait") + "\" fitToWidth=\"1\" fitToHeight=\"0\"/>");
            sb.Append("</worksheet>");
            return sb.ToString();
        }

        static string PlanBlatt(Plan plan)
        {
            var cfg = plan.Cfg;
            var zeilen = new List<Zeile>();
            var verbunden = new List<string>();
            int letzte = ErsteStundenSpalte + plan.Stunden - 1;
            DateTime ende = plan.Start.AddHours(plan.Stunden);

            zeilen.Add(new Zeile(1, 26).Text(0, "Wachtablösungsliste", sTitel));
            zeilen.Add(new Zeile(2).Text(0, (string.IsNullOrEmpty(cfg.Name) ? "" : cfg.Name + "   ·   ") +
                plan.Start.ToString("ddd dd.MM.yyyy HH:00", De) + " – " + ende.ToString("ddd dd.MM.yyyy HH:00", De), sUntertitel));

            // Tage (verbunden über ihre Stunden) und Stunden
            var tage = new Zeile(4, 20);
            for (int t = 0; t < plan.Stunden; t++)
            {
                DateTime z = plan.Start.AddHours(t);
                if (t != 0 && z.Hour != 0) continue;
                int bis = t;
                while (bis + 1 < plan.Stunden && plan.Start.AddHours(bis + 1).Hour != 0) bis++;
                tage.Text(ErsteStundenSpalte + t, z.ToString("dddd, dd.MM.yyyy", De), sTag);
                if (bis > t) verbunden.Add(Spalte(ErsteStundenSpalte + t) + "4:" + Spalte(ErsteStundenSpalte + bis) + "4");
            }
            zeilen.Add(tage);
            var kopf = new Zeile(5, 20).Text(0, "Grad", sKopf).Text(1, "Name", sKopf).Text(2, "Funktion", sKopf);
            for (int t = 0; t < plan.Stunden; t++)
                kopf.Text(ErsteStundenSpalte + t, plan.Start.AddHours(t).ToString("HH:00"), sKopf);
            zeilen.Add(kopf);

            // Personen; zwischen den Blöcken (z. B. Kdt / Wache) eine schmale Leerzeile
            int nr = 6;
            for (int r = 0; r < plan.Zeilen.Count; r++)
            {
                if (r > 0 && plan.Block[r] != plan.Block[r - 1])
                    zeilen.Add(new Zeile(nr++, 8));
                var p = plan.Zeilen[r];
                var z = new Zeile(nr++, 22).Text(0, p.Grad, sGrad).Text(1, p.Name + (p.Fahrer ? " 🚗" : ""), sName).Text(2, p.Funktion, sGrad);
                for (int t = 0; t < plan.Stunden; t++)
                {
                    int k = plan.Z[r, t];
                    if (k == Plan.RUHE) z.Leer(ErsteStundenSpalte + t, sRuhe);
                    else if (k < 0) z.Leer(ErsteStundenSpalte + t, sFrei);
                    else z.Text(ErsteStundenSpalte + t, cfg.Posten[k].Name + (plan.Essen[r, t] ? " 🍴" : ""), sErsterPosten + k);
                }
                zeilen.Add(z);
            }

            // Legende und nicht eingeteilte Personen
            nr++;
            var legende = new Zeile(nr++, 20).Text(1, "Legende", sKopf);
            int spalte = ErsteStundenSpalte;
            for (int k = 0; k < cfg.Posten.Count; k++, spalte += 3)
            {
                legende.Text(spalte, cfg.Posten[k].Name + (cfg.Posten[k].Draussen ? " ❄" : ""), sErsterPosten + k);
                verbunden.Add(Spalte(spalte) + (nr - 1) + ":" + Spalte(spalte + 2) + (nr - 1));
            }
            if (cfg.Essenszeiten.Count > 0) { legende.Text(spalte, "🍴 = Essen", sNormal); spalte += 3; }
            if (cfg.Posten.Any(p => p.Draussen)) { legende.Text(spalte, "❄ = Draussen", sNormal); spalte += 3; }
            if (plan.Zeilen.Any(p => p.Fahrer)) legende.Text(spalte, "🚗 = Fahrer", sNormal);
            zeilen.Add(legende);
            if (plan.NichtEingeteilt.Count > 0)
                zeilen.Add(new Zeile(nr++, 20).Text(1, "Nicht eingeteilt", sKopf).Text(ErsteStundenSpalte,
                    string.Join("     ", plan.NichtEingeteilt.GroupBy(p => p.Funktion)
                        .Select(g => g.Key + ": " + string.Join(", ", g.Select(p => (p.Grad + " " + p.Name).Trim())))), sNormal));
            zeilen.Add(new Zeile(nr + 1).Text(1, "Erstellt mit ShiftDesk am " + DateTime.Now.ToString("dd.MM.yyyy HH:mm"), sUntertitel));

            string spalten = "<cols><col min=\"1\" max=\"1\" width=\"7\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"24\" customWidth=\"1\"/>" +
                             "<col min=\"3\" max=\"3\" width=\"14\" customWidth=\"1\"/>" +
                             "<col min=\"4\" max=\"" + (letzte + 1) + "\" width=\"10\" customWidth=\"1\"/></cols>";
            string ansicht = "<pane xSplit=\"3\" ySplit=\"5\" topLeftCell=\"D6\" activePane=\"bottomRight\" state=\"frozen\"/>";
            return Blatt(ansicht, spalten, zeilen, verbunden, true);
        }

        static string StatistikBlatt(Plan plan)
        {
            var cfg = plan.Cfg;
            var zeilen = new List<Zeile>();
            zeilen.Add(new Zeile(1, 26).Text(0, "Statistik", sTitel));
            zeilen.Add(new Zeile(2).Text(0, "Stunden pro Person und Posten", sUntertitel));
            var kopf = new Zeile(4, 22).Text(0, "Grad", sKopf).Text(1, "Name", sKopf).Text(2, "Funktion", sKopf);
            for (int k = 0; k < cfg.Posten.Count; k++) kopf.Text(3 + k, cfg.Posten[k].Name, sErsterPosten + k);
            kopf.Text(3 + cfg.Posten.Count, "Essen", sKopf).Text(4 + cfg.Posten.Count, "Dienst-Std.", sKopf);
            zeilen.Add(kopf);
            for (int r = 0; r < plan.Zeilen.Count; r++)
            {
                var p = plan.Zeilen[r];
                var z = new Zeile(5 + r, 20).Text(0, p.Grad, sGrad).Text(1, p.Name + (p.Fahrer ? " 🚗" : ""), sName).Text(2, p.Funktion, sGrad);
                int total = 0, essen = 0;
                for (int k = 0; k < cfg.Posten.Count; k++)
                {
                    int c = 0;
                    for (int t = 0; t < plan.Stunden; t++) if (plan.Z[r, t] == k) c++;
                    total += c;
                    z.Zahl(3 + k, c, sZahl);
                }
                for (int t = 0; t < plan.Stunden; t++) if (plan.Essen[r, t]) essen++;
                z.Zahl(3 + cfg.Posten.Count, essen, sZahl).Zahl(4 + cfg.Posten.Count, total, sZahl);
                zeilen.Add(z);
            }
            string spalten = "<cols><col min=\"1\" max=\"1\" width=\"7\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"24\" customWidth=\"1\"/>" +
                             "<col min=\"3\" max=\"3\" width=\"14\" customWidth=\"1\"/>" +
                             "<col min=\"4\" max=\"" + (5 + cfg.Posten.Count) + "\" width=\"14\" customWidth=\"1\"/></cols>";
            return Blatt("<pane ySplit=\"4\" topLeftCell=\"A5\" activePane=\"bottomLeft\" state=\"frozen\"/>", spalten, zeilen, new string[0], false);
        }
    }
}
