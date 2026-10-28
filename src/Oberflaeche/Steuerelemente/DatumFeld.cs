using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using ShiftDesk.Modell;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Popups;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>
    /// Datum und volle Stunde wählen: links der Tag, rechts die Stunde, je mit Pfeilen zum Blättern.
    /// Ein Klick auf das Datum öffnet einen <see cref="Kalender"/>, auf die Stunde eine Stundenliste.
    /// Mausrad (mit Fokus) und Pfeiltasten blättern ebenfalls.
    /// </summary>
    public class DatumFeld : ThemaControl
    {
        static readonly CultureInfo De = new CultureInfo("de-CH");

        // Bereiche im Feld
        const int TagZurueck = 0, TagVor = 1, StundeZurueck = 2, StundeVor = 3, KalenderZone = 4, StundenListe = 5;

        DateTime wert = DateTime.Today;
        int hoverZone = -1, offeneZone = -1;

        public DatumFeld()
        {
            Font = Schrift.Eingabe;
            Height = Thema.FeldHoehe;
            Width = Thema.S(320);
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        /// <summary>Gewählter Zeitpunkt, immer auf die volle Stunde.</summary>
        public DateTime Wert
        {
            get { return wert; }
            set
            {
                wert = new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0);
                Invalidate();
            }
        }

        /// <summary>x-Position der Trennlinie zwischen Datum und Stunde.</summary>
        int Trenner
        {
            get { return (int)(Width * 0.62f); }
        }

        Rectangle Zone(int i)
        {
            int b = Thema.S(26), m = Thema.S(4);
            switch (i)
            {
                case TagZurueck: return new Rectangle(m, m, b, Height - 2 * m);
                case TagVor: return new Rectangle(Trenner - b - m, m, b, Height - 2 * m);
                case StundeZurueck: return new Rectangle(Trenner + m, m, b, Height - 2 * m);
                case StundeVor: return new Rectangle(Width - b - m, m, b, Height - 2 * m);
                case KalenderZone: return Rectangle.FromLTRB(Zone(TagZurueck).Right + m, m, Zone(TagVor).Left - m, Height - m);
                default: return Rectangle.FromLTRB(Zone(StundeZurueck).Right + m, m, Zone(StundeVor).Left - m, Height - m);
            }
        }

        int ZoneBei(Point p)
        {
            for (int i = 0; i < 6; i++)
                if (Zone(i).Contains(p)) return i;
            return -1;
        }

        /// <summary>Merkt sich, welche Zone aufgeklappt ist, bis das Popup wieder zu ist.</summary>
        void Offen(Popup popup, int zone)
        {
            offeneZone = zone;
            Invalidate();
            popup.Closed += (s, e) => { offeneZone = -1; Invalidate(); };
        }

        /// <summary>Führt aus, was ein Klick auf die Zone bewirkt.</summary>
        void Schritt(int zone)
        {
            switch (zone)
            {
                case TagZurueck: Wert = wert.AddDays(-1); break;
                case TagVor: Wert = wert.AddDays(1); break;
                case StundeZurueck: Wert = wert.AddHours(-1); break;
                case StundeVor: Wert = wert.AddHours(1); break;
                case KalenderZone:
                {
                    if (Popup.GeradeGeschlossen(Tuple.Create(this, KalenderZone))) break;
                    var k = new Kalender(wert);
                    var popup = new Popup(k, Tuple.Create(this, KalenderZone));
                    k.Gewaehlt += d => { popup.Close(); Wert = d.Date.AddHours(wert.Hour); };
                    Offen(popup, KalenderZone);
                    popup.ZeigenBei(this, new Rectangle(0, 0, Width, Height));
                    break;
                }
                case StundenListe:
                {
                    if (Popup.GeradeGeschlossen(Tuple.Create(this, StundenListe))) break;
                    var liste = new AuswahlListe(Zeit.Uhrzeiten, new[] { Zeit.Text(wert.Hour) }, false, Width - Trenner);
                    var popup = new Popup(liste, Tuple.Create(this, StundenListe));
                    liste.Gewaehlt += s => { popup.Close(); Wert = wert.Date.AddHours(int.Parse(s.Substring(0, 2))); };
                    Offen(popup, StundenListe);
                    popup.ZeigenBei(this, new Rectangle(Trenner, 0, Width - Trenner, Height));
                    break;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int z = ZoneBei(e.Location);
            if (z != hoverZone) { hoverZone = z; Cursor = z >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoverZone = -1;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            Schritt(ZoneBei(e.Location));
            base.OnMouseDown(e);
        }

        /// <summary>Das Mausrad ändert den Wert nur, wenn das Feld den Fokus hat; sonst blättert es die Seite weiter.</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (Focused)
            {
                int richtung = e.Delta > 0 ? 1 : -1;
                Wert = e.X < Trenner ? wert.AddDays(richtung) : wert.AddHours(richtung);
                var h = e as HandledMouseEventArgs;
                if (h != null) h.Handled = true;
            }
            base.OnMouseWheel(e);
        }

        protected override bool IsInputKey(Keys k)
        {
            return k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down || base.IsInputKey(k);
        }

        /// <summary>Links/rechts: Tag, oben/unten: Stunde.</summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) Schritt(TagZurueck);
            else if (e.KeyCode == Keys.Right) Schritt(TagVor);
            else if (e.KeyCode == Keys.Down) Schritt(StundeZurueck);
            else if (e.KeyCode == Keys.Up) Schritt(StundeVor);
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Formen.Eingabe(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Focused || offeneZone >= 0, Hover);
            using (var linie = new Pen(Thema.EingabeRand))
                g.DrawLine(linie, Trenner, Thema.S(8), Trenner, Height - Thema.S(8));

            if (offeneZone >= KalenderZone) Formen.Fuellen(g, Zone(offeneZone), Thema.S(6), Thema.Auswahl);
            else if (hoverZone >= KalenderZone) Formen.Fuellen(g, Zone(hoverZone), Thema.S(6), Thema.Hover);
            for (int i = TagZurueck; i <= StundeVor; i++)
            {
                var z = Zone(i);
                if (i == hoverZone) Formen.Fuellen(g, z, Thema.S(6), Thema.Hover);
                Lucide.Zeichne(g, i % 2 == 0 ? "chevron-left" : "chevron-right", Formen.Mitte(z, Thema.S(16)), Thema.Gedaempft);
            }
            var fl = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(g, wert.ToString("ddd, dd. MMM yyyy", De), Font,
                Rectangle.FromLTRB(Zone(TagZurueck).Right, 0, Zone(TagVor).Left, Height), Thema.Text, fl);
            TextRenderer.DrawText(g, wert.ToString("HH:00"), Font,
                Rectangle.FromLTRB(Zone(StundeZurueck).Right, 0, Zone(StundeVor).Left, Height), Thema.Text, fl);
        }
    }
}
