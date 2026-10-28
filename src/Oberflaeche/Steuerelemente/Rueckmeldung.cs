using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>Kurze Rückmeldung unten im Fenster (z. B. «Gespeichert»); verschwindet nach ein paar Sekunden von selbst.</summary>
    public class Rueckmeldung : ThemaControl
    {
        readonly Timer timer = new Timer { Interval = 2600 };
        string icon = "circle-check";
        Color farbe = Color.Green;

        public Rueckmeldung()
        {
            Visible = false;
            Font = Schrift.Fett;
            Height = Thema.S(42);
            timer.Tick += (s, e) => { timer.Stop(); Visible = false; };
        }

        /// <summary>Zeigt die Meldung unten in der Mitte des Elternelements.</summary>
        public void Zeigen(string text, string icon, Color farbe)
        {
            Text = text;
            this.icon = icon;
            this.farbe = farbe;
            if (Parent == null) return;
            int breite = Math.Min(Parent.ClientSize.Width - Thema.S(40), Thema.S(16 + 18 + 10 + 18) + TextRenderer.MeasureText(text, Font).Width);
            Bounds = new Rectangle((Parent.ClientSize.Width - breite) / 2, Parent.ClientSize.Height - Height - Thema.S(28), breite, Height);
            using (var pfad = Formen.Rund(new RectangleF(0, 0, Width, Height), Thema.S(10)))
                Region = new Region(pfad);
            Visible = true;
            BringToFront();
            Invalidate();
            timer.Stop();
            timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Formen.Fuellen(g, r, Thema.S(10), Thema.Karte);
            Formen.Umrandung(g, r, Thema.S(10), Thema.Rand);
            float s = Thema.S(18);
            Lucide.Zeichne(g, icon, new RectangleF(Thema.S(16), (Height - s) / 2, s, s), farbe);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Thema.S(16 + 18 + 10), 0, Width - Thema.S(16 + 18 + 10 + 12), Height), Thema.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
