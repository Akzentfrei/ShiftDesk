using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Steuerelemente;

namespace ShiftDesk.Oberflaeche.Popups
{
    /// <summary>
    /// Monatskalender zum Wählen eines Tages, mit «Heute»; das Mausrad blättert die Monate.
    /// </summary>
    public class Kalender : ThemaControl
    {
        static readonly CultureInfo De = new CultureInfo("de-CH");
        static readonly string[] Wochentage = { "Mo", "Di", "Mi", "Do", "Fr", "Sa", "So" };
        const int HEUTE = -3, ZURUECK = -1, VOR = -2, NICHTS = -100;
        readonly int rand = Thema.S(12), kopfH = Thema.S(38), tagH = Thema.S(26), zelle = Thema.S(36), fussH = Thema.S(46);
        readonly DateTime gewaehlt;
        DateTime monat;
        int hover = NICHTS;   // 0..41 Tage oder eine der Konstanten

        /// <summary>Ein Tag (oder «Heute») wurde gewählt.</summary>
        public event Action<DateTime> Gewaehlt;

        /// <param name="aktuell">Der bisherige Tag; er ist markiert, und der Kalender zeigt seinen Monat.</param>
        public Kalender(DateTime aktuell)
        {
            gewaehlt = aktuell.Date;
            monat = new DateTime(aktuell.Year, aktuell.Month, 1);
            Font = Schrift.Text;
            Size = new Size(2 * rand + 7 * zelle, 2 * rand + kopfH + tagH + 6 * zelle + fussH);
        }

        DateTime ErsterTag
        {
            get { return monat.AddDays(-(((int)monat.DayOfWeek + 6) % 7)); }
        }

        Rectangle Zelle(int i)
        {
            return new Rectangle(rand + (i % 7) * zelle, rand + kopfH + tagH + (i / 7) * zelle, zelle, zelle);
        }

        Rectangle Pfeil(bool vor)
        {
            int s = Thema.S(30);
            return new Rectangle(vor ? Width - rand - s : rand, rand + (kopfH - s) / 2 - Thema.S(4), s, s);
        }

        Rectangle HeuteKnopf
        {
            get { return new Rectangle(rand, Height - rand - Thema.S(34), Width - 2 * rand, Thema.S(34)); }
        }

        int IndexBei(Point p)
        {
            if (Pfeil(false).Contains(p)) return ZURUECK;
            if (Pfeil(true).Contains(p)) return VOR;
            if (HeuteKnopf.Contains(p)) return HEUTE;
            for (int i = 0; i < 42; i++)
                if (Zelle(i).Contains(p)) return i;
            return NICHTS;
        }

        void Blaettern(int monate)
        {
            monat = monat.AddMonths(monate);
            Invalidate();
        }

        void Melden(DateTime tag)
        {
            var h = Gewaehlt;
            if (h != null) h(tag);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = IndexBei(e.Location);
            if (i != hover) { hover = i; Invalidate(); }
            Cursor = i != NICHTS ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = NICHTS;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = IndexBei(e.Location);
            if (i == ZURUECK) Blaettern(-1);
            else if (i == VOR) Blaettern(1);
            else if (i == HEUTE) Melden(DateTime.Today);
            else if (i >= 0) Melden(ErsterTag.AddDays(i));
            base.OnMouseClick(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Blaettern(e.Delta > 0 ? -1 : 1);
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Thema.Karte);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var mitte = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            int icon = Thema.S(16);

            TextRenderer.DrawText(g, monat.ToString("MMMM yyyy", De), Schrift.Ueberschrift, new Rectangle(0, rand - Thema.S(4), Width, Thema.S(30)), Thema.Text, mitte);
            foreach (bool vor in new[] { false, true })
            {
                var p = Pfeil(vor);
                if (hover == (vor ? VOR : ZURUECK)) Formen.Fuellen(g, p, Thema.S(7), Thema.Hover);
                Lucide.Zeichne(g, vor ? "chevron-right" : "chevron-left", new RectangleF(p.X + (p.Width - icon) / 2f, p.Y + (p.Height - icon) / 2f, icon, icon), Thema.Gedaempft);
            }
            for (int i = 0; i < 7; i++)
                TextRenderer.DrawText(g, Wochentage[i], Schrift.Beschriftung, new Rectangle(rand + i * zelle, rand + kopfH, zelle, tagH), Thema.Gedaempft, mitte);

            DateTime tag = ErsterTag;
            for (int i = 0; i < 42; i++, tag = tag.AddDays(1))
            {
                var r = RectangleF.Inflate(Zelle(i), -Thema.S(2), -Thema.S(2));
                bool istGewaehlt = tag == gewaehlt, heute = tag == DateTime.Today, imMonat = tag.Month == monat.Month;
                if (istGewaehlt) Formen.Fuellen(g, r, Thema.S(9), Thema.Akzent);
                else if (i == hover) Formen.Fuellen(g, r, Thema.S(9), Thema.Hover);
                if (heute && !istGewaehlt) Formen.Umrandung(g, r, Thema.S(9), Thema.Akzent, 1.5f);
                Color farbe = istGewaehlt ? Color.White : imMonat ? Thema.Text : Thema.Gedaempft;
                TextRenderer.DrawText(g, tag.Day.ToString(), Font, Rectangle.Round(r), farbe, mitte);
            }

            // «Heute»
            var k = HeuteKnopf;
            using (var linie = new Pen(Thema.Rand))
                g.DrawLine(linie, rand, k.Y - Thema.S(6), Width - rand, k.Y - Thema.S(6));
            if (hover == HEUTE) Formen.Fuellen(g, k, Thema.S(7), Thema.Hover);
            TextRenderer.DrawText(g, "Heute", Schrift.Fett, k, Thema.Akzent, mitte);
            Popup.Rand(g, Size);
        }
    }
}
