using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ShiftDesk
{
    public static class Formen
    {
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

        public static void Fuellen(Graphics g, RectangleF r, float radius, Color farbe)
        {
            using (var p = Rund(r, radius))
            using (var b = new SolidBrush(farbe))
                g.FillPath(b, p);
        }

        public static void Umrandung(Graphics g, RectangleF r, float radius, Color farbe, float breite = 1f)
        {
            using (var p = Rund(r, radius))
            using (var s = new Pen(farbe, breite))
                g.DrawPath(s, p);
        }

        // Schiebeschalter (statt Checkbox), zentriert im Feld
        public static void Schalter(Graphics g, RectangleF feld, bool an)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float h = Theme.S(20), w = Theme.S(36), rand = Theme.S(3);
            var r = new RectangleF(feld.X + (feld.Width - w) / 2, feld.Y + (feld.Height - h) / 2, w, h);
            Fuellen(g, r, h / 2, an ? Theme.Akzent : Theme.SchalterAus);
            float k = h - 2 * rand;
            float x = an ? r.Right - k - rand : r.X + rand;
            g.FillEllipse(Brushes.White, x, r.Y + rand, k, k);
        }
    }

    // Basis für selbst gezeichnete Steuerelemente
    public class ThemaControl : Control
    {
        public ThemaControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        // feste Farbe hinter runden Ecken (sonst die Farbe des Elternelements)
        public Color? Untergrundfarbe;

        protected Color Untergrund
        {
            get { return Untergrundfarbe ?? (Parent != null ? Parent.BackColor : Theme.Hintergrund); }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Untergrund);
        }
    }

    public class ModernButton : ThemaControl
    {
        public string Icon;
        public bool Primaer;
        bool hover;

        public ModernButton(string text, string icon, bool primaer = false)
        {
            Text = text;
            Icon = icon;
            Primaer = primaer;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI Semibold", 9.5f);
            Height = Theme.S(36);
            Margin = new Padding(Theme.S(8), 0, 0, 0);
            BreiteAnpassen();
        }

        public void BreiteAnpassen()
        {
            if (string.IsNullOrEmpty(Text))
            {
                Width = Height;
                return;
            }
            int text = TextRenderer.MeasureText(Text, Font).Width;
            Width = Theme.S(14) + (Icon != null ? Theme.S(16 + 8) : 0) + text + Theme.S(14);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            float radius = Theme.S(8);
            bool aktiv = hover && Enabled;
            if (Primaer)
                Formen.Fuellen(g, r, radius, aktiv ? Theme.AkzentHover : Theme.Akzent);
            else
            {
                Formen.Fuellen(g, r, radius, aktiv ? Theme.Hover : Theme.Karte);
                Formen.Umrandung(g, r, radius, Theme.EingabeRand);
            }
            Color farbe = !Enabled ? Theme.Gedaempft : Primaer ? Theme.AkzentText : Theme.Text;
            int icon = Theme.S(16), luecke = string.IsNullOrEmpty(Text) || Icon == null ? 0 : Theme.S(8);
            int text = string.IsNullOrEmpty(Text) ? 0 : TextRenderer.MeasureText(g, Text, Font, Size, TextFormatFlags.NoPadding).Width;
            int breite = (Icon != null ? icon : 0) + luecke + text;
            int x = (Width - breite) / 2;
            if (Icon != null)
            {
                Lucide.Zeichne(g, Icon, new RectangleF(x, (Height - icon) / 2f, icon, icon), farbe);
                x += icon + luecke;
            }
            if (text > 0)
                TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, text + 2, Height), farbe,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    public class NavEintrag : ThemaControl
    {
        static readonly Font FettSchrift = new Font("Segoe UI Semibold", 10f);
        public string Icon;
        bool aktiv, hover;

        public NavEintrag(string text, string icon)
        {
            Text = text;
            Icon = icon;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 10f);
            Height = Theme.S(42);
        }

        public bool Aktiv
        {
            get { return aktiv; }
            set { aktiv = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0, Theme.S(2), Width, Height - Theme.S(4));
            if (aktiv) Formen.Fuellen(g, r, Theme.S(8), Theme.Auswahl);
            else if (hover) Formen.Fuellen(g, r, Theme.S(8), Theme.Hover);
            Color farbe = aktiv ? Theme.Akzent : Theme.Text;
            int icon = Theme.S(18);
            Lucide.Zeichne(g, Icon, new RectangleF(Theme.S(12), (Height - icon) / 2f, icon, icon), aktiv ? Theme.Akzent : Theme.Gedaempft);
            TextRenderer.DrawText(g, Text, aktiv ? FettSchrift : Font,
                new Rectangle(Theme.S(42), 0, Width - Theme.S(46), Height), farbe,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    // Logo oben in der Seitenleiste
    public class Marke : ThemaControl
    {
        static Image logo;

        public static Image Logo
        {
            get
            {
                if (logo == null)
                {
                    var s = typeof(Marke).Assembly.GetManifestResourceStream("ShiftDesk.logo.png");
                    if (s != null) logo = Image.FromStream(s);
                }
                return logo;
            }
        }

        public Marke()
        {
            Height = Theme.S(72);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            int kachel = Theme.S(40), y = (Height - kachel) / 2;
            var r = new RectangleF(Theme.S(4), y, kachel, kachel);
            if (Logo != null)
                g.DrawImage(Logo, r);
            else
            {
                Formen.Fuellen(g, r, Theme.S(10), Theme.Akzent);
                Lucide.Zeichne(g, "shield-check", RectangleF.Inflate(r, -Theme.S(9), -Theme.S(9)), Color.White);
            }
            using (var titel = new Font("Segoe UI Semibold", 13f))
            using (var unter = new Font("Segoe UI", 8.5f))
            {
                int x = Theme.S(6) + kachel + Theme.S(12);
                TextRenderer.DrawText(g, "ShiftDesk", titel, new Point(x, y - Theme.S(3)), Theme.Text, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, "Ablöseplanung", unter, new Point(x, y + Theme.S(21)), Theme.Gedaempft, TextFormatFlags.NoPadding);
            }
        }
    }

    // Kleine Abschnittsüberschrift in der Seitenleiste
    public class Abschnitt : ThemaControl
    {
        public Abschnitt(string text)
        {
            Text = text.ToUpperInvariant();
            Font = new Font("Segoe UI Semibold", 7.5f);
            Height = Theme.S(34);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(Theme.S(12), 0, Width, Height - Theme.S(6)),
                Theme.Gedaempft, TextFormatFlags.Bottom | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        }
    }

    // Weisse (bzw. dunkle) Karte mit runden Ecken
    public class Karte : Panel
    {
        public Karte()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Padding = new Padding(Theme.S(14));
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Hintergrund);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Formen.Fuellen(g, r, Theme.S(12), Theme.Karte);
            Formen.Umrandung(g, r, Theme.S(12), Theme.Rand);
        }
    }

    public class Eingabefeld : ThemaControl
    {
        readonly TextBox box;

        public Eingabefeld()
        {
            box = new TextBox { BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10f) };
            box.GotFocus += (s, e) => Invalidate();
            box.LostFocus += (s, e) => Invalidate();
            box.KeyPress += (s, e) => { if (NurZahlen && !char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar)) e.Handled = true; };
            Controls.Add(box);
            Height = Theme.S(38);
            Cursor = Cursors.IBeam;
            ThemaAnwenden();
        }

        public bool NurZahlen;

        public TextBox Box
        {
            get { return box; }
        }

        public override string Text
        {
            get { return box == null ? base.Text : box.Text; }
            set { if (box == null) base.Text = value; else box.Text = value; }
        }

        public void ThemaAnwenden()
        {
            box.BackColor = Theme.Eingabe;
            box.ForeColor = Theme.Text;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            box.Focus();
            base.OnClick(e);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            box.Location = new Point(Theme.S(11), (Height - box.Height) / 2);
            box.Width = Width - Theme.S(22);
            base.OnLayout(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Formen.Fuellen(g, r, Theme.S(8), Theme.Eingabe);
            Formen.Umrandung(g, r, Theme.S(8), box.Focused ? Theme.Akzent : Theme.EingabeRand, box.Focused ? 1.5f : 1f);
        }
    }

    // Datum und Uhrzeit mit Pfeilen (und Mausrad / Pfeiltasten) wählen
    public class DatumFeld : ThemaControl
    {
        static readonly CultureInfo De = new CultureInfo("de-CH");
        DateTime wert = DateTime.Today;
        int hoverZone = -1;

        public event EventHandler WertGeaendert;

        public DatumFeld()
        {
            Font = new Font("Segoe UI", 10f);
            Height = Theme.S(38);
            Width = Theme.S(320);
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        public DateTime Wert
        {
            get { return wert; }
            set
            {
                wert = new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0);
                Invalidate();
                var h = WertGeaendert;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        int Trenner
        {
            get { return (int)(Width * 0.62f); }
        }

        // 0 = Tag zurück, 1 = Tag vor, 2 = Stunde zurück, 3 = Stunde vor, 4 = Kalender, 5 = Stundenliste
        Rectangle Zone(int i)
        {
            int b = Theme.S(26), m = Theme.S(4);
            switch (i)
            {
                case 0: return new Rectangle(m, m, b, Height - 2 * m);
                case 1: return new Rectangle(Trenner - b - m, m, b, Height - 2 * m);
                case 2: return new Rectangle(Trenner + m, m, b, Height - 2 * m);
                case 3: return new Rectangle(Width - b - m, m, b, Height - 2 * m);
                case 4: return Rectangle.FromLTRB(Zone(0).Right + m, m, Zone(1).Left - m, Height - m);
                default: return Rectangle.FromLTRB(Zone(2).Right + m, m, Zone(3).Left - m, Height - m);
            }
        }

        int ZoneBei(Point p)
        {
            for (int i = 0; i < 6; i++)
                if (Zone(i).Contains(p)) return i;
            return -1;
        }

        void Schritt(int zone)
        {
            switch (zone)
            {
                case 0: Wert = wert.AddDays(-1); break;
                case 1: Wert = wert.AddDays(1); break;
                case 2: Wert = wert.AddHours(-1); break;
                case 3: Wert = wert.AddHours(1); break;
                case 4:
                {
                    var k = new Kalender(wert);
                    var popup = new Popup(k);
                    k.Gewaehlt += d => { popup.Close(); Wert = d.Date.AddHours(wert.Hour); };
                    popup.ZeigenBei(this, new Rectangle(0, 0, Width, Height));
                    break;
                }
                case 5:
                {
                    var liste = new AuswahlListe(Enumerable.Range(0, 24).Select(Zeit.Text), new[] { Zeit.Text(wert.Hour) }, false, Zone(5).Width + Theme.S(60));
                    var popup = new Popup(liste);
                    liste.Gewaehlt += s => { popup.Close(); Wert = wert.Date.AddHours(int.Parse(s.Substring(0, 2))); };
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
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            Schritt(ZoneBei(e.Location));
            base.OnMouseDown(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int richtung = e.Delta > 0 ? 1 : -1;
            if (e.X < Trenner) Wert = wert.AddDays(richtung);
            else Wert = wert.AddHours(richtung);
            base.OnMouseWheel(e);
        }

        protected override bool IsInputKey(Keys k)
        {
            return k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down || base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) Schritt(0);
            else if (e.KeyCode == Keys.Right) Schritt(1);
            else if (e.KeyCode == Keys.Down) Schritt(2);
            else if (e.KeyCode == Keys.Up) Schritt(3);
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Formen.Fuellen(g, r, Theme.S(8), Theme.Eingabe);
            Formen.Umrandung(g, r, Theme.S(8), Focused ? Theme.Akzent : Theme.EingabeRand, Focused ? 1.5f : 1f);
            using (var linie = new Pen(Theme.EingabeRand))
                g.DrawLine(linie, Trenner, Theme.S(8), Trenner, Height - Theme.S(8));

            if (hoverZone >= 4) Formen.Fuellen(g, Zone(hoverZone), Theme.S(6), Theme.Hover);
            for (int i = 0; i < 4; i++)
            {
                var z = Zone(i);
                if (i == hoverZone) Formen.Fuellen(g, z, Theme.S(6), Theme.Hover);
                int s = Theme.S(16);
                Lucide.Zeichne(g, i % 2 == 0 ? "chevron-left" : "chevron-right",
                    new RectangleF(z.X + (z.Width - s) / 2f, z.Y + (z.Height - s) / 2f, s, s), Theme.Gedaempft);
            }
            var fl = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g, wert.ToString("ddd, dd. MMM yyyy", De), Font,
                Rectangle.FromLTRB(Zone(0).Right, 0, Zone(1).Left, Height), Theme.Text, fl);
            TextRenderer.DrawText(g, wert.ToString("HH:00"), Font,
                Rectangle.FromLTRB(Zone(2).Right, 0, Zone(3).Left, Height), Theme.Text, fl);
        }
    }

    // Kleines farbiges Status-Etikett mit Icon
    public class StatusChip : ThemaControl
    {
        public string Icon = "circle-check";
        public Color Farbe = Color.Green;

        public StatusChip()
        {
            Font = new Font("Segoe UI Semibold", 9f);
            Height = Theme.S(30);
        }

        public void Setzen(string text, string icon, Color farbe)
        {
            Text = text;
            Icon = icon;
            Farbe = farbe;
            Width = Theme.S(12 + 16 + 8 + 14) + TextRenderer.MeasureText(text, Font).Width;
            Visible = !string.IsNullOrEmpty(text);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            Formen.Fuellen(g, r, Height / 2f, Color.FromArgb(Theme.Dunkel ? 45 : 28, Farbe));
            int s = Theme.S(16);
            Lucide.Zeichne(g, Icon, new RectangleF(Theme.S(12), (Height - s) / 2f, s, s), Farbe);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Theme.S(12) + s + Theme.S(8), 0, Width, Height), Farbe,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }
}

namespace ShiftDesk
{
    // Liste der Hinweise unter dem Plan; scrollt mit dem Mausrad, ohne Bildlaufleiste
    public class HinweisListe : ThemaControl
    {
        readonly System.Collections.Generic.List<string> eintraege = new System.Collections.Generic.List<string>();
        readonly int zeileH = Theme.S(28);
        int versatz;

        public HinweisListe()
        {
            Font = new Font("Segoe UI", 9.5f);
        }

        public void Setzen(System.Collections.Generic.IEnumerable<string> texte)
        {
            eintraege.Clear();
            eintraege.AddRange(texte);
            versatz = 0;
            Invalidate();
        }

        int MaxVersatz
        {
            get { return Math.Max(0, eintraege.Count * zeileH - Height); }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            Focus();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            versatz = Math.Max(0, Math.Min(MaxVersatz, versatz - Math.Sign(e.Delta) * zeileH * 2));
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Karte);
            int s = Theme.S(16);
            for (int i = 0; i < eintraege.Count; i++)
            {
                int y = i * zeileH - versatz;
                if (y + zeileH < 0 || y > Height) continue;
                Lucide.Zeichne(g, "triangle-alert", new RectangleF(Theme.S(2), y + (zeileH - s) / 2f, s, s), Theme.Warnung);
                TextRenderer.DrawText(g, eintraege[i], Font, new Rectangle(s + Theme.S(12), y, Width - s - Theme.S(12), zeileH), Theme.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }
    }
}

namespace ShiftDesk
{
    // Aufklappfeld im Eingabefeld-Stil (eigene Auswahlliste statt Windows-Combobox)
    public class AuswahlFeld : ThemaControl
    {
        readonly string[] optionen;
        string wert;
        bool hover, offen;

        public event EventHandler WertGeaendert;

        public AuswahlFeld(string[] optionen)
        {
            this.optionen = optionen;
            wert = optionen.Length > 0 ? optionen[0] : "";
            Font = new Font("Segoe UI", 10f);
            Height = Theme.S(38);
            Width = Theme.S(190);
            Cursor = Cursors.Hand;
        }

        public string Wert
        {
            get { return wert; }
            set { wert = value; Invalidate(); }
        }

        public int Index
        {
            get { return Math.Max(0, Array.IndexOf(optionen, wert)); }
            set { if (value >= 0 && value < optionen.Length) Wert = optionen[value]; }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            var liste = new AuswahlListe(optionen, new[] { wert }, false, Width);
            var popup = new Popup(liste);
            liste.Gewaehlt += w =>
            {
                popup.Close();
                Wert = w;
                var h = WertGeaendert;
                if (h != null) h(this, EventArgs.Empty);
            };
            popup.Closed += (s, a) => { offen = false; Invalidate(); };
            offen = true;
            Invalidate();
            popup.ZeigenBei(this, new Rectangle(0, 0, Width, Height));
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Formen.Fuellen(g, r, Theme.S(8), Theme.Eingabe);
            Formen.Umrandung(g, r, Theme.S(8), offen ? Theme.Akzent : hover ? Theme.Gedaempft : Theme.EingabeRand, offen ? 1.5f : 1f);
            int icon = Theme.S(14);
            Lucide.Zeichne(g, "chevron-down", new RectangleF(Width - icon - Theme.S(12), (Height - icon) / 2f, icon, icon), Theme.Gedaempft);
            TextRenderer.DrawText(g, wert, Font, new Rectangle(Theme.S(11), 0, Width - icon - Theme.S(28), Height), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}

namespace ShiftDesk
{
    // Blendet beim Seitenwechsel weich von der alten zur neuen Seite über
    public class Uebergang : Control
    {
        const int Dauer = 180;   // Millisekunden
        Bitmap vorher, nachher;
        readonly Timer timer = new Timer { Interval = 10 };
        DateTime start;
        float fortschritt;

        public Uebergang()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Visible = false;
            timer.Tick += (s, e) =>
            {
                float t = (float)((DateTime.Now - start).TotalMilliseconds / Dauer);
                if (t >= 1) { Beenden(); return; }
                fortschritt = 1 - (1 - t) * (1 - t) * (1 - t);   // ease-out
                Refresh();
            };
        }

        // Zeigt sofort das alte Bild, damit der Wechsel darunter unsichtbar passiert
        public void Abdecken(Bitmap alt, Rectangle bereich)
        {
            Beenden();
            vorher = alt;
            fortschritt = 0;
            Bounds = bereich;
            Visible = true;
            BringToFront();
            Refresh();
        }

        public void Starten(Bitmap neu)
        {
            nachher = neu;
            start = DateTime.Now;
            timer.Start();
        }

        void Beenden()
        {
            timer.Stop();
            Visible = false;
            if (vorher != null) { vorher.Dispose(); vorher = null; }
            if (nachher != null) { nachher.Dispose(); nachher = null; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (vorher != null) g.DrawImageUnscaled(vorher, 0, 0);
            if (nachher == null) return;
            int versatz = (int)Math.Round(Theme.S(10) * (1 - fortschritt));
            var matrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = fortschritt };
            using (var attr = new System.Drawing.Imaging.ImageAttributes())
            {
                attr.SetColorMatrix(matrix);
                g.DrawImage(nachher, new Rectangle(0, versatz, nachher.Width, nachher.Height), 0, 0, nachher.Width, nachher.Height, GraphicsUnit.Pixel, attr);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { Beenden(); timer.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
