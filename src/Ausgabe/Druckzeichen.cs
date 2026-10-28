using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ShiftDesk.Oberflaeche.Design;

namespace ShiftDesk.Ausgabe
{
    /// <summary>
    /// Farben, Kürzel und Symbole des ausgedruckten Plans. Das Papier ist immer hell, unabhängig vom
    /// Farbmodus der Oberfläche.
    /// </summary>
    static class Druckzeichen
    {
        public static readonly Color TextFarbe = Color.FromArgb(17, 17, 17);
        public static readonly Color Grau = Color.FromArgb(90, 90, 90);
        public static readonly Color Rahmen = Color.FromArgb(34, 34, 34);
        public static readonly Color Gitter = Color.FromArgb(200, 200, 200);

        /// <summary>Hintergrund der Ruhezeit.</summary>
        public static readonly Color Leer = Color.FromArgb(243, 244, 246);

        static readonly Color Schnee = Color.FromArgb(37, 99, 235);
        static readonly Color Orange = Color.FromArgb(234, 88, 12);

        /// <summary>Buchstabe eines Postens: A, B, C … Z, dann AA, AB …</summary>
        public static string Kuerzel(int k)
        {
            return k < 26 ? ((char)('A' + k)).ToString() : Kuerzel(k / 26 - 1) + (char)('A' + k % 26);
        }

        /// <summary>Postenfarbe stark aufgehellt, damit die Buchstaben gut lesbar sind.</summary>
        public static Color Pastell(int farbe)
        {
            var c = Color.FromArgb(farbe);
            const float a = 0.42f;
            return Color.FromArgb((int)(c.R * a + 255 * (1 - a)), (int)(c.G * a + 255 * (1 - a)), (int)(c.B * a + 255 * (1 - a)));
        }

        /// <summary>Auto: Fahrer.</summary>
        public static void AutoSymbol(Graphics g, RectangleF feld)
        {
            Lucide.Zeichne(g, "car", feld, Grau, 2.2f);
        }

        /// <summary>Schneeflocke: Posten im Freien.</summary>
        public static void SchneeSymbol(Graphics g, RectangleF feld)
        {
            Lucide.Zeichne(g, "snowflake", feld, Schnee, 2.4f);
        }

        /// <summary>Besteck auf weissem Kreis; <paramref name="reserve"/> = oranger Kreis mit weissem Besteck (eine Reserve löst zum Essen ab).</summary>
        public static void EssenSymbol(Graphics g, RectangleF zelle, bool reserve = false)
        {
            float s = Math.Min(zelle.Width, zelle.Height) * 0.78f;
            var kreis = new RectangleF(zelle.X + (zelle.Width - s) / 2, zelle.Y + (zelle.Height - s) / 2, s, s);
            using (var f = new SolidBrush(reserve ? Orange : Color.White))
                g.FillEllipse(f, kreis);
            Lucide.Zeichne(g, "utensils", RectangleF.Inflate(kreis, -s * 0.21f, -s * 0.21f), reserve ? Color.White : TextFarbe, 2.4f);
        }

        /// <summary>
        /// Text genau in die Mitte des Felds, nach dem sichtbaren Umriss der Zeichen (DrawString rückt
        /// wegen der Schrift-Innenabstände etwas zur Seite).
        /// </summary>
        public static void Mittig(Graphics g, string text, Font schrift, Brush pinsel, RectangleF feld)
        {
            if (string.IsNullOrEmpty(text)) return;
            using (var pfad = new GraphicsPath())
            {
                pfad.AddString(text, schrift.FontFamily, (int)schrift.Style, schrift.Size, PointF.Empty, StringFormat.GenericTypographic);
                var umriss = pfad.GetBounds();
                using (var m = new Matrix())
                {
                    m.Translate(feld.X + (feld.Width - umriss.Width) / 2 - umriss.X, feld.Y + (feld.Height - umriss.Height) / 2 - umriss.Y);
                    pfad.Transform(m);
                }
                g.FillPath(pinsel, pfad);
            }
        }
    }
}
