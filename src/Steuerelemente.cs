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

        // Quadrat der Kantenlänge s mitten im Feld (für Symbole)
        public static RectangleF Mitte(RectangleF feld, float s)
        {
            return new RectangleF(feld.X + (feld.Width - s) / 2, feld.Y + (feld.Height - s) / 2, s, s);
        }

        // Eingabefeld: gleich für Textfelder, Aufklappfelder, Datum und Tabellenzellen.
        // aktiv = Fokus oder aufgeklappt (blauer Rand), hover = Maus darüber (dunklerer Rand)
        public static void Eingabe(Graphics g, RectangleF r, bool aktiv, bool hover)
        {
            Fuellen(g, r, Theme.RadiusFeld, Theme.Eingabe);
            Umrandung(g, r, Theme.RadiusFeld, aktiv ? Theme.Akzent : hover ? Theme.Gedaempft : Theme.EingabeRand, aktiv ? 1.5f : 1f);
        }

        // Platz, den der Pfeil rechts in einem Aufklappfeld braucht
        public static int PfeilPlatz
        {
            get { return Theme.S(14 + 10 + 6); }
        }

        // Pfeil rechts in einem Aufklappfeld (zeigt nach oben, solange die Liste offen ist)
        public static void Aufklapppfeil(Graphics g, RectangleF feld, bool offen)
        {
            int icon = Theme.S(14);
            Lucide.Zeichne(g, offen ? "chevron-up" : "chevron-down",
                new RectangleF(feld.Right - icon - Theme.S(10), feld.Y + (feld.Height - icon) / 2f, icon, icon), offen ? Theme.Akzent : Theme.Gedaempft);
        }

        // Hinweis in einer leeren Fläche (noch kein Plan, leere Tabelle): Symbol, Titel und Text, mittig
        public static void Leer(Graphics g, Rectangle bereich, string icon, string titel, string text)
        {
            int s = Theme.S(40);
            int hoehe = s + Theme.S(14 + 26 + 4 + 22);
            int y = bereich.Top + Math.Max(Theme.S(8), (bereich.Height - hoehe) / 2);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Lucide.Zeichne(g, icon, new RectangleF(bereich.Left + (bereich.Width - s) / 2f, y, s, s), Theme.Gedaempft, 1.6f);
            var fl = TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(g, titel, Schrift.Titel, new Rectangle(bereich.Left, y + s + Theme.S(14), bereich.Width, Theme.S(26)), Theme.Text, fl);
            TextRenderer.DrawText(g, text, Schrift.Text, new Rectangle(bereich.Left, y + s + Theme.S(44), bereich.Width, Theme.S(22)), Theme.Gedaempft, fl);
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

    // Basis für selbst gezeichnete Steuerelemente: doppelt gepuffert, merkt sich, ob die Maus darüber ist
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

        protected bool Hover { get; set; }

        // Kurzinfo beim Darüberfahren (null = keine)
        public string Tipp
        {
            set { ThemaTipp.Standard.SetToolTip(this, value); }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Untergrund);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            Hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            Hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }
    }

    // Steuerelemente, die fürs Mausrad den Fokus brauchen, holen ihn sich beim Darüberfahren –
    // aber nicht, solange jemand in einem Textfeld tippt (sonst gehen die nächsten Tasten ins Leere)
    public static class Fokus
    {
        static bool TextfeldAktiv(Control c)
        {
            var form = c.FindForm();
            Control aktiv = form == null ? null : form.ActiveControl;
            while (aktiv is ContainerControl && ((ContainerControl)aktiv).ActiveControl != null)
                aktiv = ((ContainerControl)aktiv).ActiveControl;
            return aktiv is TextBoxBase;
        }

        public static void FuerMausrad(Control c)
        {
            if (!TextfeldAktiv(c)) c.Focus();
        }
    }

    public class ModernButton : ThemaControl
    {
        public string Icon;
        public bool Primaer;
        public bool Gefahr;        // rot gefüllt (Löschen u. ä.)
        public bool Fokusrahmen;   // Tastaturfokus sichtbar machen (in Dialogen)
        bool kompakt;

        public ModernButton(string text, string icon, bool primaer = false)
        {
            Text = text;
            Icon = icon;
            Primaer = primaer;
            Cursor = Cursors.Hand;
            Font = Schrift.Fett;
            Height = Theme.KnopfHoehe;
            Margin = new Padding(Theme.S(8), 0, 0, 0);
            Width = VolleBreite;
        }

        // Breite mit Symbol und Text
        public int VolleBreite
        {
            get
            {
                if (string.IsNullOrEmpty(Text)) return Height;
                return Theme.S(14) + (Icon != null ? Theme.S(16 + 8) : 0) + TextRenderer.MeasureText(Text, Font).Width + Theme.S(14);
            }
        }

        // Nur das Symbol zeigen, wenn es eng wird; der Text erscheint dann als Kurzinfo
        public bool Kompakt
        {
            get { return kompakt; }
            set
            {
                if (Icon == null || kompakt == value) return;
                kompakt = value;
                Width = value ? Height : VolleBreite;
                Tipp = value ? Text : null;
                Invalidate();
            }
        }

        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        // Enter und Leertaste lösen den Knopf mit Fokus aus
        protected override bool IsInputKey(Keys k)
        {
            return k == Keys.Enter || k == Keys.Space || base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                e.Handled = e.SuppressKeyPress = true;
                OnClick(EventArgs.Empty);
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            float radius = Theme.RadiusFeld;
            bool aktiv = Hover && Enabled;
            if (Gefahr)
                Formen.Fuellen(g, r, radius, aktiv ? Theme.GefahrKnopfHover : Theme.GefahrKnopf);
            else if (Primaer)
                Formen.Fuellen(g, r, radius, aktiv ? Theme.AkzentHover : Theme.Akzent);
            else
            {
                Formen.Fuellen(g, r, radius, aktiv ? Theme.Hover : Theme.Karte);
                Formen.Umrandung(g, r, radius, Theme.EingabeRand);
            }
            if (Fokusrahmen && Focused)
                Formen.Umrandung(g, new RectangleF(1.5f, 1.5f, Width - 3, Height - 3), radius - 1, Theme.Akzent, 2f);
            Color farbe = !Enabled ? Theme.Gedaempft : Primaer || Gefahr ? Theme.AkzentText : Theme.Text;
            string text = kompakt ? "" : Text;
            int icon = Theme.S(16), luecke = string.IsNullOrEmpty(text) || Icon == null ? 0 : Theme.S(8);
            int textBreite = string.IsNullOrEmpty(text) ? 0 : TextRenderer.MeasureText(g, text, Font, Size, TextFormatFlags.NoPadding).Width;
            int x = (Width - ((Icon != null ? icon : 0) + luecke + textBreite)) / 2;
            if (Icon != null)
            {
                Lucide.Zeichne(g, Icon, new RectangleF(x, (Height - icon) / 2f, icon, icon), farbe);
                x += icon + luecke;
            }
            if (textBreite > 0)
                TextRenderer.DrawText(g, text, Font, new Rectangle(x, 0, textBreite + 2, Height), farbe,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    // Eintrag der Seitenleiste; schmal = nur das Symbol (der Text erscheint als Kurzinfo)
    public class NavEintrag : ThemaControl
    {
        public string Icon;
        bool aktiv, schmal;

        public NavEintrag(string text, string icon, int hoehe = 42)
        {
            Text = text;
            Icon = icon;
            Cursor = Cursors.Hand;
            Font = Schrift.Eingabe;
            Height = Theme.S(hoehe);
        }

        public bool Aktiv
        {
            get { return aktiv; }
            set { aktiv = value; Invalidate(); }
        }

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

        // Fertig gezeichnetes Bild des Eintrags: beim Ein-/Ausfahren der Seitenleiste wird nur noch kopiert
        Bitmap bild;
        string bildSchluessel;

        protected override void OnPaint(PaintEventArgs e)
        {
            // Hintergrund und Symbol (geglättete Pfade, teuer) kommen aus dem Zwischenbild; die Schrift wird direkt
            // gezeichnet, denn auf einer Bitmap glättet Windows sie nicht
            string schluessel = Width + "|" + Height + "|" + aktiv + "|" + Hover + "|" + Icon + "|"
                + Untergrund.ToArgb() + "|" + Theme.Akzent.ToArgb() + "|" + Theme.Auswahl.ToArgb();
            if (bild == null || schluessel != bildSchluessel)
            {
                if (bild != null) bild.Dispose();
                bild = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(bild))
                {
                    g.Clear(Untergrund);
                    Zeichnen(g);
                }
                bildSchluessel = schluessel;
            }
            e.Graphics.DrawImageUnscaled(bild, 0, 0);
            if (schmal) return;
            TextRenderer.DrawText(e.Graphics, Text, aktiv ? Schrift.Ueberschrift : Font,
                new Rectangle(Theme.S(44), 0, Width - Theme.S(48), Height), aktiv ? Theme.Akzent : Theme.Text,
                aktiv ? Theme.Auswahl : Hover ? Theme.Hover : Untergrund,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && bild != null) bild.Dispose();
            base.Dispose(disposing);
        }

        void Zeichnen(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0, Theme.S(2), Width, Height - Theme.S(4));
            if (aktiv) Formen.Fuellen(g, r, Theme.RadiusFeld, Theme.Auswahl);
            else if (Hover) Formen.Fuellen(g, r, Theme.RadiusFeld, Theme.Hover);
            // Symbol immer an derselben Stelle (mittig in der schmalen Leiste), damit beim Ein-/Ausklappen nichts springt
            int icon = Theme.S(18);
            float x = Theme.S(16);
            Lucide.Zeichne(g, Icon, new RectangleF(x, (Height - icon) / 2f, icon, icon), aktiv ? Theme.Akzent : Theme.Gedaempft);
        }
    }

    // Logo oben in der Seitenleiste; schmal = nur das Logo
    public class Marke : ThemaControl
    {
        public bool Schmal;

        public Marke()
        {
            Height = Theme.S(72);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (Schmal) return;   // eingeklappt steht hier der Knopf zum Ausklappen
            int kachel = Theme.S(40), y = (Height - kachel) / 2;
            var r = new RectangleF(Theme.S(5), y, kachel, kachel);   // immer an derselben Stelle
            var logo = Bilder.Laden("logo.png");
            if (logo != null)
                g.DrawImage(logo, r);
            else
            {
                Formen.Fuellen(g, r, Theme.S(10), Theme.Akzent);
                Lucide.Zeichne(g, "shield-check", RectangleF.Inflate(r, -Theme.S(9), -Theme.S(9)), Color.White);
            }
            if (Schmal) return;
            int x = (int)r.Right + Theme.S(14);
            TextRenderer.DrawText(g, "ShiftDesk", Schrift.Titel, new Point(x, y - Theme.S(3)), Theme.Text, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "Ablöseplanung", Schrift.Klein, new Point(x, y + Theme.S(21)), Theme.Gedaempft, TextFormatFlags.NoPadding);
        }
    }

    // Kurze Rückmeldung unten im Fenster (z. B. «Gespeichert»); verschwindet nach ein paar Sekunden von selbst
    public class Rueckmeldung : ThemaControl
    {
        readonly Timer timer = new Timer { Interval = 2600 };
        string icon = "circle-check";
        Color farbe = Color.Green;

        public Rueckmeldung()
        {
            Visible = false;
            Font = Schrift.Fett;
            Height = Theme.S(42);
            timer.Tick += (s, e) => { timer.Stop(); Visible = false; };
        }

        public void Zeigen(string text, string icon, Color farbe)
        {
            Text = text;
            this.icon = icon;
            this.farbe = farbe;
            if (Parent == null) return;
            int breite = Math.Min(Parent.ClientSize.Width - Theme.S(40), Theme.S(16 + 18 + 10 + 18) + TextRenderer.MeasureText(text, Font).Width);
            Bounds = new Rectangle((Parent.ClientSize.Width - breite) / 2, Parent.ClientSize.Height - Height - Theme.S(28), breite, Height);
            using (var pfad = Formen.Rund(new RectangleF(0, 0, Width, Height), Theme.S(10)))
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
            Formen.Fuellen(g, r, Theme.S(10), Theme.Karte);
            Formen.Umrandung(g, r, Theme.S(10), Theme.Rand);
            float s = Theme.S(18);
            Lucide.Zeichne(g, icon, new RectangleF(Theme.S(16), (Height - s) / 2, s, s), farbe);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Theme.S(16 + 18 + 10), 0, Width - Theme.S(16 + 18 + 10 + 12), Height), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }

    // Kleiner Knopf nur mit Symbol (z. B. Seitenleiste ein- und ausklappen); beim Darüberfahren hinterlegt
    public class SymbolKnopf : ThemaControl
    {
        public string Icon;

        public SymbolKnopf(string icon)
        {
            Icon = icon;
            Size = new Size(Theme.S(32), Theme.S(32));
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Hover) Formen.Fuellen(g, new RectangleF(0, 0, Width, Height), Theme.S(8), Theme.Hover);
            float s = Theme.S(18);
            Lucide.Zeichne(g, Icon, new RectangleF((Width - s) / 2, (Height - s) / 2, s, s), Hover ? Theme.Text : Theme.Gedaempft);
        }
    }

    // Kleine Abschnittsüberschrift (Seitenleiste, Einstellungen); schmal = nur ein kurzer Strich
    public class Abschnitt : ThemaControl
    {
        public bool Schmal;
        public bool Trennlinie;   // feine Linie oben, als Abgrenzung zum Abschnitt davor
        readonly int einzug;

        public Abschnitt(string text, int einzug = 12)
        {
            Text = text.ToUpperInvariant();
            this.einzug = Theme.S(einzug);
            Font = Schrift.Abschnitt;
            Height = Theme.S(34);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Schmal)
            {
                using (var p = new Pen(Theme.Rand))
                    e.Graphics.DrawLine(p, Theme.S(12), Height - Theme.S(14), Width - Theme.S(12), Height - Theme.S(14));
                return;
            }
            if (Trennlinie)
                using (var p = new Pen(Theme.Rand))
                    e.Graphics.DrawLine(p, einzug, Theme.S(4), Width - 1, Theme.S(4));
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(einzug, 0, Width - einzug, Height - Theme.S(6)),
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
            BackColor = Theme.Karte;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Hintergrund);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Formen.Fuellen(g, r, Theme.RadiusKarte, Theme.Karte);
            Formen.Umrandung(g, r, Theme.RadiusKarte, Theme.Rand);
        }
    }

    public class Eingabefeld : ThemaControl
    {
        readonly TextBox box;

        public Eingabefeld()
        {
            box = new TextBox { BorderStyle = BorderStyle.None, Font = Schrift.Eingabe };
            box.GotFocus += (s, e) => Invalidate();
            box.LostFocus += (s, e) => Invalidate();
            // das Textfeld deckt fast alles ab: Darüberfahren auch dort zählen
            box.MouseEnter += (s, e) => HoverPruefen();
            box.MouseLeave += (s, e) => HoverPruefen();
            box.KeyPress += (s, e) => { if (NurZahlen && !char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar)) e.Handled = true; };
            Controls.Add(box);
            Height = Theme.FeldHoehe;
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

        void HoverPruefen()
        {
            bool h = ClientRectangle.Contains(PointToClient(Cursor.Position));
            if (h != Hover) { Hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            HoverPruefen();
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
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Formen.Eingabe(e.Graphics, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), box.Focused, Hover);
        }
    }

    // Datum und Uhrzeit mit Pfeilen (und Mausrad / Pfeiltasten) wählen
    public class DatumFeld : ThemaControl
    {
        static readonly CultureInfo De = new CultureInfo("de-CH");
        DateTime wert = DateTime.Today;
        int hoverZone = -1, offeneZone = -1;

        public DatumFeld()
        {
            Font = Schrift.Eingabe;
            Height = Theme.FeldHoehe;
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

        void Offen(Popup popup, int zone)
        {
            offeneZone = zone;
            Invalidate();
            popup.Closed += (s, e) => { offeneZone = -1; Invalidate(); };
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
                    if (Popup.GeradeGeschlossen(Tuple.Create(this, 4))) break;
                    var k = new Kalender(wert);
                    var popup = new Popup(k, Tuple.Create(this, 4));
                    k.Gewaehlt += d => { popup.Close(); Wert = d.Date.AddHours(wert.Hour); };
                    Offen(popup, 4);
                    popup.ZeigenBei(this, new Rectangle(0, 0, Width, Height));
                    break;
                }
                case 5:
                {
                    if (Popup.GeradeGeschlossen(Tuple.Create(this, 5))) break;
                    var liste = new AuswahlListe(Enumerable.Range(0, 24).Select(Zeit.Text), new[] { Zeit.Text(wert.Hour) }, false, Width - Trenner);
                    var popup = new Popup(liste, Tuple.Create(this, 5));
                    liste.Gewaehlt += s => { popup.Close(); Wert = wert.Date.AddHours(int.Parse(s.Substring(0, 2))); };
                    Offen(popup, 5);
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

        // Das Mausrad ändert den Wert nur, wenn das Feld den Fokus hat; sonst blättert es die Seite weiter
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
            Formen.Eingabe(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Focused || offeneZone >= 0, Hover);
            using (var linie = new Pen(Theme.EingabeRand))
                g.DrawLine(linie, Trenner, Theme.S(8), Trenner, Height - Theme.S(8));

            if (offeneZone >= 4) Formen.Fuellen(g, Zone(offeneZone), Theme.S(6), Theme.Auswahl);
            else if (hoverZone >= 4) Formen.Fuellen(g, Zone(hoverZone), Theme.S(6), Theme.Hover);
            for (int i = 0; i < 4; i++)
            {
                var z = Zone(i);
                if (i == hoverZone) Formen.Fuellen(g, z, Theme.S(6), Theme.Hover);
                Lucide.Zeichne(g, i % 2 == 0 ? "chevron-left" : "chevron-right", Formen.Mitte(z, Theme.S(16)), Theme.Gedaempft);
            }
            var fl = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(g, wert.ToString("ddd, dd. MMM yyyy", De), Font,
                Rectangle.FromLTRB(Zone(0).Right, 0, Zone(1).Left, Height), Theme.Text, fl);
            TextRenderer.DrawText(g, wert.ToString("HH:00"), Font,
                Rectangle.FromLTRB(Zone(2).Right, 0, Zone(3).Left, Height), Theme.Text, fl);
        }
    }

    // Senkrechter Strich zwischen Gruppen einer Werkzeugleiste
    public class Trenner : ThemaControl
    {
        public Trenner()
        {
            Size = new Size(Theme.S(17), Theme.KnopfHoehe);
            Margin = new Padding(Theme.S(4), 0, 0, 0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Untergrund);
            int x = Width / 2, rand = Theme.S(6);
            using (var p = new Pen(Theme.Rand))
                e.Graphics.DrawLine(p, x, rand, x, Height - rand);
        }
    }

    // Kleines farbiges Status-Etikett mit Icon; klickbar = klappt etwas auf (Pfeil rechts)
    public class StatusChip : ThemaControl
    {
        string icon = "circle-check";
        Color farbe = Color.Green;
        bool klickbar, offen;

        public StatusChip()
        {
            Font = Schrift.KleinFett;
            Height = Theme.S(30);
        }

        public void Setzen(string text, string icon, Color farbe, bool klickbar = false)
        {
            Text = text;
            this.icon = icon;
            this.farbe = farbe;
            this.klickbar = klickbar;
            Cursor = klickbar ? Cursors.Hand : Cursors.Default;
            Width = Theme.S(12 + 16 + 8 + 14) + TextRenderer.MeasureText(text, Font).Width + (klickbar ? Theme.S(14 + 4) : 0);
            Visible = !string.IsNullOrEmpty(text);
            Invalidate();
        }

        // Aufgeklappt: der Pfeil zeigt nach oben
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
            Formen.Fuellen(g, r, Height / 2f, Theme.Toenung(farbe, klickbar && (Hover || offen)));
            int s = Theme.S(16);
            Lucide.Zeichne(g, icon, new RectangleF(Theme.S(12), (Height - s) / 2f, s, s), farbe);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Theme.S(12) + s + Theme.S(8), 0, Width, Height), farbe,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            if (klickbar)
            {
                int p = Theme.S(14);
                Lucide.Zeichne(g, offen ? "chevron-up" : "chevron-down", new RectangleF(Width - Theme.S(14) - p, (Height - p) / 2f, p, p), farbe);
            }
        }
    }

    // Aufklappfeld im Eingabefeld-Stil (eigene Auswahlliste statt Windows-Combobox)
    public class AuswahlFeld : ThemaControl
    {
        readonly string[] optionen;
        string wert;
        bool offen;

        public event EventHandler WertGeaendert;

        public AuswahlFeld(string[] optionen)
        {
            this.optionen = optionen;
            wert = optionen.Length > 0 ? optionen[0] : "";
            Font = Schrift.Eingabe;
            Height = Theme.FeldHoehe;
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || Popup.GeradeGeschlossen(this)) { base.OnMouseDown(e); return; }
            var liste = new AuswahlListe(optionen, new[] { wert }, false, Width);
            var popup = new Popup(liste, this);
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
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Formen.Eingabe(g, r, offen, Hover);
            Formen.Aufklapppfeil(g, r, offen);
            TextRenderer.DrawText(g, wert, Font, new Rectangle(Theme.S(11), 0, Width - Theme.S(11) - Formen.PfeilPlatz, Height), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    // Bereich mit senkrechtem Bildlauf (Mausrad, ohne Leiste) für Inhalte, die höher werden
    // können als der Platz. Die Höhe des Inhalts kommt aus dessen GetPreferredSize.
    public class BildlaufPanel : Panel
    {
        readonly Control inhalt;
        int versatz;

        public BildlaufPanel(Control inhalt)
        {
            this.inhalt = inhalt;
            DoubleBuffered = true;
            Controls.Add(inhalt);
        }

        // Höhe, die der Inhalt bei dieser Breite ganz braucht
        public int InhaltHoehe(int breite)
        {
            return inhalt.GetPreferredSize(new Size(Math.Max(1, breite), 0)).Height;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            Anordnen();
        }

        void Anordnen()
        {
            int breite = ClientSize.Width, hoehe = ClientSize.Height;
            if (breite <= 0 || hoehe <= 0) return;
            int h = InhaltHoehe(breite);
            versatz = Math.Max(0, Math.Min(Math.Max(0, h - hoehe), versatz));
            var ziel = new Rectangle(0, -versatz, breite, h);
            if (inhalt.Bounds != ziel) inhalt.Bounds = ziel;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int vorher = versatz;
            versatz -= Math.Sign(e.Delta) * Theme.S(60);
            Anordnen();
            var h = e as HandledMouseEventArgs;
            if (h != null) h.Handled = versatz != vorher || InhaltHoehe(ClientSize.Width) > ClientSize.Height;
            base.OnMouseWheel(e);
        }
    }

    // Blendet beim Seitenwechsel weich von der alten zur neuen Seite über
    public class Uebergang : Control
    {
        const int Dauer = 160;   // Millisekunden
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
