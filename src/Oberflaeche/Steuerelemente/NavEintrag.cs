using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>Eintrag der Seitenleiste mit Symbol und Text; schmal = nur das Symbol (der Text erscheint als Kurzinfo).</summary>
    public class NavEintrag : ThemaControl
    {
        /// <summary>Lucide-Symbol des Eintrags.</summary>
        public string Icon;

        bool aktiv, schmal;

        // Fertig gezeichnetes Bild des Eintrags: beim Ein-/Ausfahren der Seitenleiste wird nur noch kopiert
        Bitmap bild;
        string bildSchluessel;

        /// <param name="text">Beschriftung (eingeklappt als Kurzinfo).</param>
        /// <param name="icon">Lucide-Symbol.</param>
        /// <param name="hoehe">Höhe bei 96 dpi.</param>
        public NavEintrag(string text, string icon, int hoehe = 42)
        {
            Text = text;
            Icon = icon;
            Cursor = Cursors.Hand;
            Font = Schrift.Eingabe;
            Height = Thema.S(hoehe);
        }

        /// <summary>Die Seite dieses Eintrags wird gerade angezeigt (blau hinterlegt).</summary>
        public bool Aktiv
        {
            get { return aktiv; }
            set { aktiv = value; Invalidate(); }
        }

        /// <summary>Nur das Symbol zeigen (eingeklappte Seitenleiste).</summary>
        public bool Schmal
        {
            get { return schmal; }
            set
            {
                if (schmal == value) return;
                schmal = value;
                Tipp = value ? Text : null;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Hintergrund und Symbol (geglättete Pfade, teuer) kommen aus dem Zwischenbild; die Schrift wird direkt
            // gezeichnet, denn auf einer Bitmap glättet Windows sie nicht
            string schluessel = Width + "|" + Height + "|" + aktiv + "|" + Hover + "|" + Icon + "|"
                + Untergrund.ToArgb() + "|" + Thema.Akzent.ToArgb() + "|" + Thema.Auswahl.ToArgb();
            if (bild == null || schluessel != bildSchluessel)
            {
                if (bild != null) bild.Dispose();
                bild = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(bild))
                {
                    g.Clear(Untergrund);
                    HintergrundUndSymbol(g);
                }
                bildSchluessel = schluessel;
            }
            e.Graphics.DrawImageUnscaled(bild, 0, 0);
            if (schmal) return;
            TextRenderer.DrawText(e.Graphics, Text, aktiv ? Schrift.Ueberschrift : Font,
                new Rectangle(Thema.S(44), 0, Width - Thema.S(48), Height), aktiv ? Thema.Akzent : Thema.Text,
                aktiv ? Thema.Auswahl : Hover ? Thema.Hover : Untergrund,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        void HintergrundUndSymbol(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0, Thema.S(2), Width, Height - Thema.S(4));
            if (aktiv) Formen.Fuellen(g, r, Thema.RadiusFeld, Thema.Auswahl);
            else if (Hover) Formen.Fuellen(g, r, Thema.RadiusFeld, Thema.Hover);
            // Symbol immer an derselben Stelle (mittig in der schmalen Leiste), damit beim Ein-/Ausklappen nichts springt
            int icon = Thema.S(18);
            float x = Thema.S(16);
            Lucide.Zeichne(g, Icon, new RectangleF(x, (Height - icon) / 2f, icon, icon), aktiv ? Thema.Akzent : Thema.Gedaempft);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && bild != null) bild.Dispose();
            base.Dispose(disposing);
        }
    }
}
