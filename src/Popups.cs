using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ShiftDesk
{
    // Aufklappfenster im eigenen Design: runde Ecken, Schatten, sanftes Einblenden.
    // Schliesst sich beim Klick daneben, mit Esc – und beim erneuten Klick auf das Feld, das es geöffnet hat.
    public class Popup : ToolStripDropDown
    {
        public const int Radius = 10;
        const int Einblenden = 140;   // Millisekunden

        static object letzterBesitzer;
        static DateTime letzteZeit;

        readonly Control inhalt;
        readonly Timer animation = new Timer { Interval = 12 };
        DateTime animationStart;
        int zielY;

        // Das Feld, zu dem das Popup gehört (für Auf-/Zuklappen per Klick auf das Feld)
        public object Besitzer;

        public Popup(Control inhalt, object besitzer = null)
        {
            this.inhalt = inhalt;
            Besitzer = besitzer;
            AutoSize = false;
            Padding = Padding.Empty;
            Margin = Padding.Empty;
            DropShadowEnabled = true;
            BackColor = Theme.Karte;
            Renderer = new PopupRenderer();
            var host = new ToolStripControlHost(inhalt) { AutoSize = false, Margin = Padding.Empty, Padding = Padding.Empty, Size = inhalt.Size };
            Items.Add(host);
            Size = inhalt.Size;
            using (var pfad = Formen.Rund(new RectangleF(0, 0, inhalt.Width, inhalt.Height), Theme.S(Radius)))
                Region = new Region(pfad);

            animation.Tick += (s, e) =>
            {
                double t = Math.Min(1, (DateTime.Now - animationStart).TotalMilliseconds / Einblenden);
                double weich = 1 - Math.Pow(1 - t, 3);
                Opacity = weich;
                Top = zielY - (int)Math.Round(Theme.S(6) * (1 - weich));
                if (t >= 1) animation.Stop();
            };
            Closed += (s, e) =>
            {
                animation.Stop();
                letzterBesitzer = Besitzer;
                letzteZeit = DateTime.Now;
                // erst nach dem Schliessen aufräumen
                var t = new Timer { Interval = 1 };
                t.Tick += (a, b) => { t.Dispose(); Dispose(); };
                t.Start();
            };
        }

        // true, wenn das Popup dieses Feldes gerade eben (durch den Klick aufs Feld) geschlossen wurde
        public static bool GeradeGeschlossen(object besitzer)
        {
            return besitzer != null && Equals(besitzer, letzterBesitzer) && (DateTime.Now - letzteZeit).TotalMilliseconds < 400;
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            Theme.RundeEcken(Handle);
            inhalt.Focus();
            zielY = Top;
            animationStart = DateTime.Now;
            animation.Start();
        }

        // Unter (oder, wenn kein Platz, über) dem Feld anzeigen. Erst im nächsten Durchgang der Nachrichtenschleife,
        // damit ein gerade schliessendes anderes Popup fertig zu ist und das neue nicht gleich mit schliesst.
        public void ZeigenBei(Control besitzer, Rectangle feld)
        {
            besitzer.BeginInvoke(new Action(() =>
            {
                if (IsDisposed || besitzer.IsDisposed) return;
                var bildschirm = Screen.FromControl(besitzer).WorkingArea;
                var p = besitzer.PointToScreen(new Point(feld.Left, feld.Bottom + Theme.S(6)));
                if (p.Y + Height > bildschirm.Bottom)
                    p.Y = besitzer.PointToScreen(new Point(0, feld.Top)).Y - Height - Theme.S(6);
                p.X = Math.Max(bildschirm.Left, Math.Min(p.X, bildschirm.Right - Width));
                Opacity = 0;
                Show(p);
            }));
        }

        // Rand passend zu den runden Ecken; die Inhalte rufen das am Ende ihres Zeichnens auf
        public static void Rand(Graphics g, Size groesse)
        {
            var glaettung = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Formen.Umrandung(g, new RectangleF(0.5f, 0.5f, groesse.Width - 1, groesse.Height - 1), Theme.S(Radius), Theme.Rand);
            g.SmoothingMode = glaettung;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) animation.Dispose();
            base.Dispose(disposing);
        }

        class PopupRenderer : ToolStripRenderer
        {
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                e.Graphics.Clear(Theme.Karte);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
            }
        }
    }

    // Liste zum Auswählen; mit mehrfach = true als Liste mit Häkchen (bleibt offen)
    public class AuswahlListe : ThemaControl
    {
        readonly string[] eintraege;
        readonly HashSet<string> gewaehlt;
        readonly bool mehrfach;
        readonly Func<string, string> anzeige;
        readonly int zeileH = Theme.S(36), rand = Theme.S(6);
        readonly Timer scrollen = new Timer { Interval = 12 };
        int hover = -1;
        float versatz, ziel;

        public event Action<string> Gewaehlt;
        public event Action<List<string>> Geaendert;

        public AuswahlListe(IEnumerable<string> eintraege, IEnumerable<string> aktuell, bool mehrfach, int breite, Func<string, string> anzeige = null)
        {
            this.eintraege = eintraege.ToArray();
            this.mehrfach = mehrfach;
            this.anzeige = anzeige ?? (s => s);
            gewaehlt = new HashSet<string>(aktuell ?? new string[0]);
            Font = new Font("Segoe UI", 10f);
            int sichtbar = Math.Min(8, Math.Max(1, this.eintraege.Length));
            int textBreite = this.eintraege.Length == 0 ? 0 : this.eintraege.Max(s => TextRenderer.MeasureText(this.anzeige(s), Font).Width);
            Size = new Size(Math.Max(breite, textBreite + Theme.S(mehrfach ? 80 : 60)), sichtbar * zeileH + 2 * rand);
            int start = Array.FindIndex(this.eintraege, gewaehlt.Contains);
            if (start >= 0)
            {
                hover = start;
                versatz = ziel = Begrenzen((start - sichtbar / 2) * zeileH);
            }
            // weiches Scrollen: der Versatz folgt dem Ziel
            scrollen.Tick += (s, e) =>
            {
                versatz += (ziel - versatz) * 0.35f;
                if (Math.Abs(ziel - versatz) < 0.5f) { versatz = ziel; scrollen.Stop(); }
                Invalidate();
            };
        }

        float MaxVersatz
        {
            get { return Math.Max(0, eintraege.Length * zeileH - (Height - 2 * rand)); }
        }

        float Begrenzen(float v)
        {
            return Math.Max(0, Math.Min(MaxVersatz, v));
        }

        void ScrollenNach(float neu)
        {
            ziel = Begrenzen(neu);
            if (!scrollen.Enabled) scrollen.Start();
        }

        int IndexBei(Point p)
        {
            if (p.Y < rand || p.Y > Height - rand) return -1;
            int i = (int)((p.Y - rand + versatz) / zeileH);
            return i >= 0 && i < eintraege.Length ? i : -1;
        }

        void Sichtbar(int i)
        {
            float oben = i * zeileH, unten = oben + zeileH, h = Height - 2 * rand;
            if (oben < ziel) ScrollenNach(oben);
            else if (unten > ziel + h) ScrollenNach(unten - h);
        }

        void Waehlen(int i)
        {
            if (i < 0) return;
            string w = eintraege[i];
            if (mehrfach)
            {
                if (!gewaehlt.Remove(w)) gewaehlt.Add(w);
                Invalidate();
                var h = Geaendert;
                if (h != null) h(eintraege.Where(gewaehlt.Contains).ToList());
            }
            else
            {
                var h = Gewaehlt;
                if (h != null) h(w);
            }
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
            Waehlen(IndexBei(e.Location));
            base.OnMouseClick(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollenNach(ziel - e.Delta / 120f * zeileH * 1.5f);
            base.OnMouseWheel(e);
        }

        protected override bool IsInputKey(Keys k)
        {
            return k == Keys.Up || k == Keys.Down || k == Keys.Enter || k == Keys.Space || base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
            {
                hover = Math.Max(0, Math.Min(eintraege.Length - 1, hover + (e.KeyCode == Keys.Down ? 1 : -1)));
                Sichtbar(hover);
                Invalidate();
            }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                Waehlen(hover);
            base.OnKeyDown(e);
        }

        // Tippen springt zum nächsten Eintrag mit diesem Anfangsbuchstaben bzw. dieser Ziffer
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (char.IsLetterOrDigit(e.KeyChar) && eintraege.Length > 0)
            {
                string zeichen = e.KeyChar.ToString();
                for (int n = 1; n <= eintraege.Length; n++)
                {
                    int i = (Math.Max(hover, -1) + n) % eintraege.Length;
                    if (anzeige(eintraege[i]).StartsWith(zeichen, StringComparison.CurrentCultureIgnoreCase))
                    {
                        hover = i;
                        Sichtbar(i);
                        Invalidate();
                        break;
                    }
                }
                e.Handled = true;
            }
            base.OnKeyPress(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Karte);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.SetClip(new Rectangle(0, rand, Width, Height - 2 * rand));
            int icon = Theme.S(16);
            using (var fett = new Font("Segoe UI Semibold", Font.Size))
                for (int i = 0; i < eintraege.Length; i++)
                {
                    float y = rand + i * zeileH - versatz;
                    if (y + zeileH < 0 || y > Height) continue;
                    var r = new RectangleF(rand, y + 1, Width - 2 * rand, zeileH - 2);
                    bool an = gewaehlt.Contains(eintraege[i]);
                    // gewählter Eintrag bleibt farbig hinterlegt, Maus und Tastatur zeigen sich dezent
                    if (an && !mehrfach) Formen.Fuellen(g, r, Theme.S(7), Theme.Auswahl);
                    else if (i == hover) Formen.Fuellen(g, r, Theme.S(7), Theme.Hover);
                    int x = (int)r.X + Theme.S(10);
                    if (mehrfach)
                    {
                        // Kästchen mit Häkchen
                        float k = Theme.S(18);
                        var box = new RectangleF(x, r.Y + (r.Height - k) / 2, k, k);
                        if (an)
                        {
                            Formen.Fuellen(g, box, Theme.S(5), Theme.Akzent);
                            Lucide.Zeichne(g, "check", RectangleF.Inflate(box, -Theme.S(3), -Theme.S(3)), Color.White, 3f);
                        }
                        else
                        {
                            Formen.Fuellen(g, box, Theme.S(5), Theme.Eingabe);
                            Formen.Umrandung(g, box, Theme.S(5), Theme.EingabeRand, 1.5f);
                        }
                        x += (int)k + Theme.S(10);
                    }
                    string text = anzeige(eintraege[i]);
                    bool leer = eintraege[i] == "";
                    TextRenderer.DrawText(g, text, an && !mehrfach ? fett : Font,
                        Rectangle.FromLTRB(x, (int)r.Y, (int)r.Right - icon - Theme.S(14), (int)r.Bottom),
                        leer ? Theme.Gedaempft : (an && !mehrfach ? Theme.Akzent : Theme.Text),
                        TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    if (an && !mehrfach)
                        Lucide.Zeichne(g, "check", new RectangleF(r.Right - icon - Theme.S(10), r.Y + (r.Height - icon) / 2, icon, icon), Theme.Akzent);
                }
            g.ResetClip();

            // sanfter Verlauf oben/unten, wenn es dort weitergeht (statt Bildlaufleiste)
            int verlauf = Theme.S(22);
            if (versatz > 0.5f)
                using (var b = new LinearGradientBrush(new Rectangle(0, 0, Width, verlauf + 1), Theme.Karte, Color.FromArgb(0, Theme.Karte), 90f))
                    g.FillRectangle(b, 0, 0, Width, verlauf);
            if (versatz < MaxVersatz - 0.5f)
                using (var b = new LinearGradientBrush(new Rectangle(0, Height - verlauf - 1, Width, verlauf + 1), Color.FromArgb(0, Theme.Karte), Theme.Karte, 90f))
                    g.FillRectangle(b, 0, Height - verlauf, Width, verlauf);
            Popup.Rand(g, Size);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) scrollen.Dispose();
            base.Dispose(disposing);
        }
    }

    // Farbpalette mit «Weitere Farben…»
    public class FarbAuswahl : ThemaControl
    {
        static readonly int[] Palette =
        {
            0x4FADEA, 0x2E70BE, 0x1E3A8A, 0x22B8CF, 0x1E9C9C, 0x6EE7B7,
            0x4CA850, 0x9ACD32, 0x6B8E23, 0xF7C143, 0xF28C28, 0xFF7F50,
            0xB22222, 0xF87171, 0xE0529C, 0xC026D3, 0x7030A0, 0xA78BFA,
            0x8B5A2B, 0xD6B98C, 0xA6A6A6, 0x6B7280, 0x374151, 0x111827,
        };
        const int Spalten = 6;
        readonly int feld = Theme.S(30), luecke = Theme.S(8), rand = Theme.S(14), knopfH = Theme.S(36);
        readonly Color aktuell;
        int hover = -1;   // 0..23 Farbe, 24 = Weitere Farben

        public event Action<Color> Gewaehlt;
        public event Action WeitereFarben;

        public FarbAuswahl(Color aktuell)
        {
            this.aktuell = aktuell;
            Font = new Font("Segoe UI", 9.5f);
            int zeilen = (Palette.Length + Spalten - 1) / Spalten;
            Size = new Size(2 * rand + Spalten * feld + (Spalten - 1) * luecke,
                            2 * rand + zeilen * feld + (zeilen - 1) * luecke + Theme.S(12) + knopfH);
        }

        Rectangle Feld(int i)
        {
            if (i == Palette.Length)
                return new Rectangle(rand - Theme.S(4), Height - rand - knopfH + Theme.S(4), Width - 2 * rand + Theme.S(8), knopfH - Theme.S(2));
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
            g.Clear(Theme.Karte);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < Palette.Length; i++)
            {
                var r = Feld(i);
                var farbe = Color.FromArgb(255, Color.FromArgb(Palette[i]));
                bool an = farbe.ToArgb() == aktuell.ToArgb();
                // Maus darüber: Feld wird etwas grösser
                var flaeche = i == hover && !an ? RectangleF.Inflate(r, Theme.S(2), Theme.S(2)) : (RectangleF)r;
                if (an)
                    Formen.Umrandung(g, RectangleF.Inflate(r, Theme.S(3), Theme.S(3)), Theme.S(9), Theme.Akzent, 2f);
                Formen.Fuellen(g, flaeche, Theme.S(7), farbe);
                Formen.Umrandung(g, flaeche, Theme.S(7), Color.FromArgb(30, 0, 0, 0));
                if (an)
                    Lucide.Zeichne(g, "check", RectangleF.Inflate(r, -Theme.S(7), -Theme.S(7)), Color.White, 3f);
            }
            var k = Feld(Palette.Length);
            using (var linie = new Pen(Theme.Rand))
                g.DrawLine(linie, rand, k.Y - Theme.S(6), Width - rand, k.Y - Theme.S(6));
            if (hover == Palette.Length) Formen.Fuellen(g, k, Theme.S(7), Theme.Hover);
            int icon = Theme.S(16);
            Lucide.Zeichne(g, "plus", new RectangleF(k.X + Theme.S(8), k.Y + (k.Height - icon) / 2f, icon, icon), Theme.Gedaempft);
            TextRenderer.DrawText(g, "Weitere Farben…", Font, Rectangle.FromLTRB(k.X + icon + Theme.S(16), k.Y, k.Right, k.Bottom), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            Popup.Rand(g, Size);
        }
    }

    // Monatskalender zum Wählen eines Tages, mit «Heute»
    public class Kalender : ThemaControl
    {
        static readonly CultureInfo De = new CultureInfo("de-CH");
        static readonly string[] Wochentage = { "Mo", "Di", "Mi", "Do", "Fr", "Sa", "So" };
        const int HEUTE = -3, ZURUECK = -1, VOR = -2, NICHTS = -100;
        readonly int rand = Theme.S(12), kopfH = Theme.S(38), tagH = Theme.S(26), zelle = Theme.S(36), fussH = Theme.S(46);
        readonly DateTime gewaehlt;
        DateTime monat;
        int hover = NICHTS;   // 0..41 Tage oder eine der Konstanten

        public event Action<DateTime> Gewaehlt;

        public Kalender(DateTime aktuell)
        {
            gewaehlt = aktuell.Date;
            monat = new DateTime(aktuell.Year, aktuell.Month, 1);
            Font = new Font("Segoe UI", 9.5f);
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
            int s = Theme.S(30);
            return new Rectangle(vor ? Width - rand - s : rand, rand + (kopfH - s) / 2 - Theme.S(4), s, s);
        }

        Rectangle HeuteKnopf
        {
            get { return new Rectangle(rand, Height - rand - Theme.S(34), Width - 2 * rand, Theme.S(34)); }
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
            g.Clear(Theme.Karte);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var mitte = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            int icon = Theme.S(16);

            using (var fett = new Font("Segoe UI Semibold", 10.5f))
                TextRenderer.DrawText(g, monat.ToString("MMMM yyyy", De), fett, new Rectangle(0, rand - Theme.S(4), Width, Theme.S(30)), Theme.Text, mitte);
            foreach (bool vor in new[] { false, true })
            {
                var p = Pfeil(vor);
                if (hover == (vor ? VOR : ZURUECK)) Formen.Fuellen(g, p, Theme.S(7), Theme.Hover);
                Lucide.Zeichne(g, vor ? "chevron-right" : "chevron-left", new RectangleF(p.X + (p.Width - icon) / 2f, p.Y + (p.Height - icon) / 2f, icon, icon), Theme.Gedaempft);
            }
            using (var klein = new Font("Segoe UI Semibold", 8.5f))
                for (int i = 0; i < 7; i++)
                    TextRenderer.DrawText(g, Wochentage[i], klein, new Rectangle(rand + i * zelle, rand + kopfH, zelle, tagH), Theme.Gedaempft, mitte);

            DateTime tag = ErsterTag;
            for (int i = 0; i < 42; i++, tag = tag.AddDays(1))
            {
                var r = RectangleF.Inflate(Zelle(i), -Theme.S(2), -Theme.S(2));
                bool istGewaehlt = tag == gewaehlt, heute = tag == DateTime.Today, imMonat = tag.Month == monat.Month;
                if (istGewaehlt) Formen.Fuellen(g, r, Theme.S(9), Theme.Akzent);
                else if (i == hover) Formen.Fuellen(g, r, Theme.S(9), Theme.Hover);
                if (heute && !istGewaehlt) Formen.Umrandung(g, r, Theme.S(9), Theme.Akzent, 1.5f);
                Color farbe = istGewaehlt ? Color.White : imMonat ? Theme.Text : Theme.Gedaempft;
                TextRenderer.DrawText(g, tag.Day.ToString(), Font, Rectangle.Round(r), farbe, mitte);
            }

            // «Heute»
            var k = HeuteKnopf;
            using (var linie = new Pen(Theme.Rand))
                g.DrawLine(linie, rand, k.Y - Theme.S(6), Width - rand, k.Y - Theme.S(6));
            if (hover == HEUTE) Formen.Fuellen(g, k, Theme.S(7), Theme.Hover);
            using (var fett = new Font("Segoe UI Semibold", 9.5f))
                TextRenderer.DrawText(g, "Heute", fett, k, Theme.Akzent, mitte);
            Popup.Rand(g, Size);
        }
    }
}
