using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>Logo, Name und Version oben in der Seitenleiste; schmal = leer (dort steht dann der Knopf zum Ausklappen).</summary>
    public class Marke : ThemaControl
    {
        /// <summary>Eingeklappte Seitenleiste: nichts zeichnen.</summary>
        public bool Schmal;

        static readonly string Version = "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        public Marke()
        {
            Height = Thema.S(72);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (Schmal) return;
            int kachel = Thema.S(40), y = (Height - kachel) / 2;
            var r = new RectangleF(Thema.S(5), y, kachel, kachel);   // immer an derselben Stelle
            var logo = Bilder.Laden("logo.png");
            if (logo != null)
                g.DrawImage(logo, r);
            else
            {
                Formen.Fuellen(g, r, Thema.S(10), Thema.Akzent);
                Lucide.Zeichne(g, "shield-check", RectangleF.Inflate(r, -Thema.S(9), -Thema.S(9)), Color.White);
            }
            int x = (int)r.Right + Thema.S(14);
            TextRenderer.DrawText(g, "ShiftDesk", Schrift.Titel, new Point(x, y - Thema.S(3)), Thema.Text, TextFormatFlags.NoPadding);
            // die Version klein hinter dem Namen, auf gleicher Grundlinie
            Size titel = TextRenderer.MeasureText(g, "ShiftDesk", Schrift.Titel, Size.Empty, TextFormatFlags.NoPadding);
            int klein = TextRenderer.MeasureText(g, Version, Schrift.Klein, Size.Empty, TextFormatFlags.NoPadding).Height;
            TextRenderer.DrawText(g, Version, Schrift.Klein, new Point(x + titel.Width + Thema.S(6), y - Thema.S(3) + titel.Height - klein - Thema.S(2)),
                Thema.Gedaempft, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "Ablöseplanung", Schrift.Klein, new Point(x, y + Thema.S(21)), Thema.Gedaempft, TextFormatFlags.NoPadding);
        }
    }
}
