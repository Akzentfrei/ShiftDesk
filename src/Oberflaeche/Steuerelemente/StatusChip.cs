using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>Kleines farbiges Status-Etikett mit Symbol; klickbar = klappt etwas auf (Pfeil rechts).</summary>
    public class StatusChip : ThemaControl
    {
        string icon = "circle-check";
        Color farbe = Color.Green;
        bool klickbar, offen;

        public StatusChip()
        {
            Font = Schrift.KleinFett;
            Height = Thema.S(30);
        }

        /// <summary>Setzt Inhalt und Farbe; die Breite passt sich dem Text an. Leerer Text = unsichtbar.</summary>
        public void Setzen(string text, string icon, Color farbe, bool klickbar = false)
        {
            Text = text;
            this.icon = icon;
            this.farbe = farbe;
            this.klickbar = klickbar;
            Cursor = klickbar ? Cursors.Hand : Cursors.Default;
            Width = Thema.S(12 + 16 + 8 + 14) + TextRenderer.MeasureText(text, Font).Width + (klickbar ? Thema.S(14 + 4) : 0);
            Visible = !string.IsNullOrEmpty(text);
            Invalidate();
        }

        /// <summary>Aufgeklappt: der Pfeil zeigt nach oben.</summary>
        public bool Offen
        {
            get { return offen; }
            set { offen = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            Formen.Fuellen(g, r, Height / 2f, Thema.Toenung(farbe, klickbar && (Hover || offen)));
            int s = Thema.S(16);
            Lucide.Zeichne(g, icon, new RectangleF(Thema.S(12), (Height - s) / 2f, s, s), farbe);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Thema.S(12) + s + Thema.S(8), 0, Width, Height), farbe,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            if (klickbar)
            {
                int p = Thema.S(14);
                Lucide.Zeichne(g, offen ? "chevron-up" : "chevron-down", new RectangleF(Width - Thema.S(14) - p, (Height - p) / 2f, p, p), farbe);
            }
        }
    }
}
