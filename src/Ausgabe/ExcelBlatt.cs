using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;

namespace ShiftDesk.Ausgabe
{
    /// <summary>Baut das XML eines Tabellenblatts (xl/worksheets/sheetN.xml) aus Zeilen zusammen.</summary>
    static class ExcelBlatt
    {
        /// <summary>Spaltenbuchstabe zum Index: 0 → A, 25 → Z, 26 → AA …</summary>
        public static string Spalte(int i)
        {
            string s = "";
            for (i++; i > 0; i = (i - 1) / 26)
                s = (char)('A' + (i - 1) % 26) + s;
            return s;
        }

        /// <summary>Das ganze Blatt.</summary>
        /// <param name="ansicht">Inhalt von <c>sheetView</c>, z. B. fixierte Zeilen und Spalten.</param>
        /// <param name="spalten">Das <c>cols</c>-Element mit den Spaltenbreiten.</param>
        /// <param name="zeilen">Die Zeilen in aufsteigender Reihenfolge.</param>
        /// <param name="verbunden">Verbundene Bereiche, z. B. «D4:AA4».</param>
        /// <param name="quer">Querformat beim Drucken.</param>
        public static string Xml(string ansicht, string spalten, IEnumerable<ExcelZeile> zeilen, IEnumerable<string> verbunden, bool quer)
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
    }

    /// <summary>Eine Zeile eines Tabellenblatts; die Zellen werden der Reihe nach angehängt.</summary>
    class ExcelZeile
    {
        readonly StringBuilder sb = new StringBuilder();
        readonly int nr;
        readonly double? hoehe;

        /// <param name="nr">Zeilennummer ab 1.</param>
        /// <param name="hoehe">Feste Höhe in Punkt, null = Standard.</param>
        public ExcelZeile(int nr, double? hoehe = null)
        {
            this.nr = nr;
            this.hoehe = hoehe;
        }

        /// <summary>Zelle mit Text.</summary>
        public ExcelZeile Text(int spalte, string text, int stil)
        {
            sb.Append("<c r=\"" + ExcelBlatt.Spalte(spalte) + nr + "\" s=\"" + stil + "\" t=\"inlineStr\"><is><t xml:space=\"preserve\">" +
                      SecurityElement.Escape(text ?? "") + "</t></is></c>");
            return this;
        }

        /// <summary>Zelle mit einer Zahl.</summary>
        public ExcelZeile Zahl(int spalte, int wert, int stil)
        {
            sb.Append("<c r=\"" + ExcelBlatt.Spalte(spalte) + nr + "\" s=\"" + stil + "\"><v>" + wert + "</v></c>");
            return this;
        }

        /// <summary>Leere Zelle, nur mit Format (z. B. Farbe der Ruhezeit).</summary>
        public ExcelZeile Leer(int spalte, int stil)
        {
            sb.Append("<c r=\"" + ExcelBlatt.Spalte(spalte) + nr + "\" s=\"" + stil + "\"/>");
            return this;
        }

        /// <summary>Das <c>row</c>-Element.</summary>
        public override string ToString()
        {
            return "<row r=\"" + nr + "\"" + (hoehe.HasValue ? " ht=\"" + hoehe.Value.ToString(CultureInfo.InvariantCulture) + "\" customHeight=\"1\"" : "") + ">" + sb + "</row>";
        }
    }
}
