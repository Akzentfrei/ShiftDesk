using System.Collections.Generic;
using System.Drawing;
using System.Text;
using ShiftDesk.Modell;

namespace ShiftDesk.Ausgabe
{
    /// <summary>
    /// Zellformate der Excel-Datei (xl/styles.xml): feste Stile für Titel, Kopf, Namen usw. und ein Stil pro
    /// Posten in dessen Farbe. Die Konstanten sind die Nummern in <c>cellXfs</c>.
    /// </summary>
    static class ExcelStile
    {
        public const int Normal = 0, Titel = 1, Untertitel = 2, Kopf = 3, Tag = 4, Name = 5, Grad = 6, Ruhe = 7, Frei = 8, Zahl = 9;

        /// <summary>Stil des ersten Postens; Posten k hat den Stil <c>ErsterPosten + k</c>.</summary>
        public const int ErsterPosten = 10;

        /// <summary>Das ganze Stylesheet für die Posten dieser Konfiguration.</summary>
        public static string Xml(Konfiguration cfg)
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
                Xf(0, 0, 0, null),                                              // Normal
                Xf(1, 0, 0, null),                                              // Titel
                Xf(2, 0, 0, null),                                              // Untertitel
                Xf(3, 0, 2, "horizontal=\"center\" vertical=\"center\""),       // Kopf
                Xf(4, 0, 0, "vertical=\"center\""),                             // Tag
                Xf(0, 0, 2, "vertical=\"center\""),                             // Name
                Xf(2, 0, 2, "vertical=\"center\""),                             // Grad
                Xf(7, 2, 1, "horizontal=\"center\" vertical=\"center\""),       // Ruhe
                Xf(0, 0, 1, null),                                              // Frei
                Xf(0, 0, 2, "horizontal=\"center\" vertical=\"center\""),       // Zahl
            };
            for (int k = 0; k < cfg.Posten.Count; k++)
                xfs.Add(Xf(Dunkel(Color.FromArgb(cfg.Posten[k].Farbe)) ? 5 : 6, 3 + k, 1, "horizontal=\"center\" vertical=\"center\" shrinkToFit=\"1\""));
            sb.Append("<cellXfs count=\"" + xfs.Count + "\">" + string.Concat(xfs) + "</cellXfs>");
            sb.Append("<cellStyles count=\"1\"><cellStyle name=\"Standard\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
            sb.Append("</styleSheet>");
            return sb.ToString();
        }

        /// <summary>Ein Zellformat aus Schrift, Füllung, Rahmen und optionaler Ausrichtung.</summary>
        static string Xf(int font, int fill, int rand, string ausrichtung)
        {
            return "<xf numFmtId=\"0\" fontId=\"" + font + "\" fillId=\"" + fill + "\" borderId=\"" + rand + "\" xfId=\"0\"" +
                   " applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"" + (ausrichtung != null ? " applyAlignment=\"1\"><alignment " + ausrichtung + "/></xf>" : "/>");
        }

        static string Hex(Color c)
        {
            return "FF" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        /// <summary>Dunkle Postenfarbe: darauf steht weisse Schrift.</summary>
        static bool Dunkel(Color c)
        {
            return (c.R * 299 + c.G * 587 + c.B * 114) / 1000 < 150;
        }
    }
}
