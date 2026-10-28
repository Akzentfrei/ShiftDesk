using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Steuerelemente;

namespace ShiftDesk.Oberflaeche.Popups
{
    /// <summary>
    /// Farbpalette mit «Eigene Farbe…»; enthält alle Farben des Beispiels (<see cref="Modell.Vorlagen.Beispiel"/>).
    /// </summary>
    public class FarbAuswahl : ThemaControl
    {
        static readonly int[] Palette =
        {
            0x4FADEA, 0x2E70BE, 0x1E3A8A, 0x22B8CF, 0x0D9488, 0x6EE7B7,
            0x4CA850, 0x9ACD32, 0x6B8E23, 0xF7C143, 0xEA580C, 0xFF7F50,
            0xB22222, 0xF87171, 0xE0529C, 0xC026D3, 0x7030A0, 0xA78BFA,
            0x8B5A2B, 0xD6B98C, 0xA6A6A6, 0x6B7280, 0x374151, 0x111827,
        };
        const int Spalten = 6;
        readonly int feld = Thema.S(30), luecke = Thema.S(8), rand = Thema.S(14), knopfH = Thema.S(36);
        readonly Color aktuell;
        int hover = -1;   // 0..23 Farbe, 24 = Weitere Farben

        /// <summary>Eine Farbe der Palette wurde gewählt.</summary>
        public event Action<Color> Gewaehlt;
        /// <summary>«Eigene Farbe…» wurde angeklickt.</summary>
        public event Action WeitereFarben;

        /// <param name="aktuell">Die bisherige Farbe; sie ist markiert.</param>
        public FarbAuswahl(Color aktuell)
        {
            this.aktuell = aktuell;
            Font = Schrift.Text;
            int zeilen = (Palette.Length + Spalten - 1) / Spalten;
            Size = new Size(2 * rand + Spalten * feld + (Spalten - 1) * luecke,
                            2 * rand + zeilen * feld + (zeilen - 1) * luecke + Thema.S(12) + knopfH);
        }

        Rectangle Feld(int i)
        {
            if (i == Palette.Length)
                return new Rectangle(rand - Thema.S(4), Height - rand - knopfH + Thema.S(4), Width - 2 * rand + Thema.S(8), knopfH - Thema.S(2));
            return new Rectangle(rand + (i % Spalten) * (feld + luecke), rand + (i / Spalten) * (feld + luecke), feld, feld);
        }

        int IndexBei(Point p)
        {
            for (int i = 0; i <= Palette.Length; i++)
                if (Feld(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = IndexBei(e.Location);
            if (i != hover) { hover = i; Invalidate(); }
            Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = IndexBei(e.Location);
            if (i == Palette.Length) { var h = WeitereFarben; if (h != null) h(); }
            else if (i >= 0) { var h = Gewaehlt; if (h != null) h(Color.FromArgb(255, Color.FromArgb(Palette[i]))); }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Thema.Karte);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < Palette.Length; i++)
            {
                var r = Feld(i);
                var farbe = Color.FromArgb(255, Color.FromArgb(Palette[i]));
                bool an = farbe.ToArgb() == aktuell.ToArgb();
                // Maus darüber: Feld wird etwas grösser
                var flaeche = i == hover && !an ? RectangleF.Inflate(r, Thema.S(2), Thema.S(2)) : (RectangleF)r;
                if (an)
                    Formen.Umrandung(g, RectangleF.Inflate(r, Thema.S(3), Thema.S(3)), Thema.S(9), Thema.Akzent, 2f);
                Formen.Fuellen(g, flaeche, Thema.S(7), farbe);
                Formen.Umrandung(g, flaeche, Thema.S(7), Color.FromArgb(30, 0, 0, 0));
                if (an)
                    Lucide.Zeichne(g, "check", RectangleF.Inflate(r, -Thema.S(7), -Thema.S(7)), Color.White, 3f);
            }
            var k = Feld(Palette.Length);
            using (var linie = new Pen(Thema.Rand))
                g.DrawLine(linie, rand, k.Y - Thema.S(6), Width - rand, k.Y - Thema.S(6));
            if (hover == Palette.Length) Formen.Fuellen(g, k, Thema.S(7), Thema.Hover);
            int icon = Thema.S(16);
            Lucide.Zeichne(g, "plus", new RectangleF(k.X + Thema.S(8), k.Y + (k.Height - icon) / 2f, icon, icon), Thema.Gedaempft);
            TextRenderer.DrawText(g, "Eigene Farbe…", Font, Rectangle.FromLTRB(k.X + icon + Thema.S(16), k.Y, k.Right, k.Bottom), Thema.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            // aktuelle Farbe, wenn sie nicht aus der Palette stammt
            if (!Palette.Any(p => Color.FromArgb(255, Color.FromArgb(p)).ToArgb() == aktuell.ToArgb()))
            {
                var chip = new RectangleF(k.Right - Thema.S(8) - Thema.S(36), k.Y + (k.Height - Thema.S(20)) / 2f, Thema.S(36), Thema.S(20));
                Formen.Umrandung(g, RectangleF.Inflate(chip, Thema.S(3), Thema.S(3)), Thema.S(7), Thema.Akzent, 2f);
                Formen.Fuellen(g, chip, Thema.S(5), aktuell);
                Formen.Umrandung(g, chip, Thema.S(5), Color.FromArgb(30, 0, 0, 0));
            }
            Popup.Rand(g, Size);
        }
    }
}
