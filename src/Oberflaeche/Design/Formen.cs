using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShiftDesk.Oberflaeche.Design
{
    /// <summary>Zeichenbausteine, die überall gleich aussehen sollen: runde Flächen, Eingabefelder, Pfeile, Schalter.</summary>
    public static class Formen
    {
        /// <summary>Rechteck mit runden Ecken als Pfad (der Aufrufer gibt ihn frei).</summary>
        public static GraphicsPath Rund(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>Füllt ein Rechteck mit runden Ecken.</summary>
        public static void Fuellen(Graphics g, RectangleF r, float radius, Color farbe)
        {
            using (var p = Rund(r, radius))
            using (var b = new SolidBrush(farbe))
                g.FillPath(b, p);
        }

        /// <summary>Umrandet ein Rechteck mit runden Ecken.</summary>
        public static void Umrandung(Graphics g, RectangleF r, float radius, Color farbe, float breite = 1f)
        {
            using (var p = Rund(r, radius))
            using (var s = new Pen(farbe, breite))
                g.DrawPath(s, p);
        }

        /// <summary>Quadrat der Kantenlänge <paramref name="s"/> mitten im Feld (für Symbole).</summary>
        public static RectangleF Mitte(RectangleF feld, float s)
        {
            return new RectangleF(feld.X + (feld.Width - s) / 2, feld.Y + (feld.Height - s) / 2, s, s);
        }

        /// <summary>Eingabefeld: gleich für Textfelder, Aufklappfelder, Datum und Tabellenzellen.</summary>
        /// <param name="g">Zeichenfläche.</param>
        /// <param name="r">Rand des Felds.</param>
        /// <param name="aktiv">Fokus oder aufgeklappt (blauer Rand).</param>
        /// <param name="hover">Maus darüber (dunklerer Rand).</param>
        public static void Eingabe(Graphics g, RectangleF r, bool aktiv, bool hover)
        {
            Fuellen(g, r, Thema.RadiusFeld, Thema.Eingabe);
            Umrandung(g, r, Thema.RadiusFeld, aktiv ? Thema.Akzent : hover ? Thema.Gedaempft : Thema.EingabeRand, aktiv ? 1.5f : 1f);
        }

        /// <summary>Platz, den der Pfeil rechts in einem Aufklappfeld braucht.</summary>
        public static int PfeilPlatz
        {
            get { return Thema.S(14 + 10 + 6); }
        }

        /// <summary>Pfeil rechts in einem Aufklappfeld (zeigt nach oben, solange die Liste offen ist).</summary>
        public static void Aufklapppfeil(Graphics g, RectangleF feld, bool offen)
        {
            int icon = Thema.S(14);
            Lucide.Zeichne(g, offen ? "chevron-up" : "chevron-down",
                new RectangleF(feld.Right - icon - Thema.S(10), feld.Y + (feld.Height - icon) / 2f, icon, icon), offen ? Thema.Akzent : Thema.Gedaempft);
        }

        /// <summary>Hinweis in einer leeren Fläche (noch kein Plan, leere Tabelle): Symbol, Titel und Text, mittig.</summary>
        public static void Leer(Graphics g, Rectangle bereich, string icon, string titel, string text)
        {
            int s = Thema.S(40);
            int hoehe = s + Thema.S(14 + 26 + 4 + 22);
            int y = bereich.Top + Math.Max(Thema.S(8), (bereich.Height - hoehe) / 2);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Lucide.Zeichne(g, icon, new RectangleF(bereich.Left + (bereich.Width - s) / 2f, y, s, s), Thema.Gedaempft, 1.6f);
            var fl = TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(g, titel, Schrift.Titel, new Rectangle(bereich.Left, y + s + Thema.S(14), bereich.Width, Thema.S(26)), Thema.Text, fl);
            TextRenderer.DrawText(g, text, Schrift.Text, new Rectangle(bereich.Left, y + s + Thema.S(44), bereich.Width, Thema.S(22)), Thema.Gedaempft, fl);
        }

        /// <summary>Schiebeschalter (statt Checkbox), zentriert im Feld.</summary>
        public static void Schalter(Graphics g, RectangleF feld, bool an)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float h = Thema.S(20), w = Thema.S(36), rand = Thema.S(3);
            var r = new RectangleF(feld.X + (feld.Width - w) / 2, feld.Y + (feld.Height - h) / 2, w, h);
            Fuellen(g, r, h / 2, an ? Thema.Akzent : Thema.SchalterAus);
            float k = h - 2 * rand;
            float x = an ? r.Right - k - rand : r.X + rand;
            g.FillEllipse(Brushes.White, x, r.Y + rand, k, k);
        }
    }
}
