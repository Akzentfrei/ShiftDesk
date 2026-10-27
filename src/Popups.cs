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

        // Unter (oder, wenn kein Platz, über) dem Feld anzeigen; daneben = rechts neben dem Feld (Seitenleiste).
        // Erst im nächsten Durchgang der Nachrichtenschleife, damit ein gerade schliessendes anderes Popup
        // fertig zu ist und das neue nicht gleich mit schliesst.
        public void ZeigenBei(Control besitzer, Rectangle feld, bool daneben = false)
        {
            besitzer.BeginInvoke(new Action(() =>
            {
                if (IsDisposed || besitzer.IsDisposed || !besitzer.IsHandleCreated) return;
                var bildschirm = Screen.FromControl(besitzer).WorkingArea;
                Point p;
                if (daneben)
                {
                    p = besitzer.PointToScreen(new Point(feld.Right + Theme.S(8), feld.Top));
                    if (p.Y + Height > bildschirm.Bottom)
                        p.Y = besitzer.PointToScreen(new Point(0, feld.Bottom)).Y - Height;
                }
                else
                {
                    p = besitzer.PointToScreen(new Point(feld.Left, feld.Bottom + Theme.S(6)));
                    if (p.Y + Height > bildschirm.Bottom)
                        p.Y = besitzer.PointToScreen(new Point(0, feld.Top)).Y - Height - Theme.S(6);
                }
                p.X = Math.Max(bildschirm.Left, Math.Min(p.X, bildschirm.Right - Width));
                p.Y = Math.Max(bildschirm.Top, p.Y);
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

        // AltGr (Ctrl+Alt, z. B. für # und @ auf der Schweizer Tastatur) ist keine Menütaste: beim Tippen offen bleiben
        protected override bool ProcessDialogKey(Keys keyData)
        {
            if ((keyData & Keys.Control) == Keys.Control && (keyData & Keys.Alt) == Keys.Alt) return false;
            return base.ProcessDialogKey(keyData);
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

    // Kleiner Info-Knopf neben einer Beschriftung: ein Klick zeigt die Erklärung in einem Popup
    public class InfoKnopf : ThemaControl
    {
        readonly string titel, text;

        public InfoKnopf(string titel, string text, int groesse = 20)
        {
            this.titel = titel;
            this.text = text;
            Size = new Size(Theme.S(groesse), Theme.S(groesse));
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !Popup.GeradeGeschlossen(this))
                InfoText.Zeigen(this, new Rectangle(0, 0, Width, Height), this, titel, text);
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            InfoText.Symbol(e.Graphics, new RectangleF(0, 0, Width, Height), Hover);
        }
    }

    // Inhalt des Info-Popups: Titel und umbrochener Erklärungstext
    public class InfoText : ThemaControl
    {
        static readonly Font TitelSchrift = Schrift.Ueberschrift, TextSchrift = Schrift.Text;
        const TextFormatFlags Umbruch = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
        readonly string titel, text;
        readonly int rand = Theme.S(14);
        readonly Size titelGroesse, textGroesse;

        InfoText(string titel, string text)
        {
            this.titel = titel ?? "";
            this.text = text;
            int innen = Theme.S(360);
            titelGroesse = this.titel == "" ? Size.Empty : TextRenderer.MeasureText(this.titel, TitelSchrift, new Size(innen, 0), Umbruch);
            textGroesse = TextRenderer.MeasureText(text, TextSchrift, new Size(innen, 0), Umbruch);
            int abstand = this.titel == "" ? 0 : Theme.S(6);
            Size = new Size(Math.Max(titelGroesse.Width, textGroesse.Width) + 2 * rand, rand + titelGroesse.Height + abstand + textGroesse.Height + rand);
        }

        // Unter dem Feld des Ankers zeigen; ein zweiter Klick auf denselben Besitzer schliesst wieder
        public static Popup Zeigen(Control anker, Rectangle feld, object besitzer, string titel, string text)
        {
            var p = new Popup(new InfoText(titel, text), besitzer);
            p.ZeigenBei(anker, feld);
            return p;
        }

        // Das Info-Zeichen, gleich für Knopf und Tabellenkopf
        public static void Symbol(Graphics g, RectangleF feld, bool hover)
        {
            float s = Math.Min(feld.Width, feld.Height) - Theme.S(2);
            Lucide.Zeichne(g, "info", new RectangleF(feld.X + (feld.Width - s) / 2, feld.Y + (feld.Height - s) / 2, s, s), hover ? Theme.Akzent : Theme.Gedaempft, 2f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Karte);
            int y = rand;
            if (titel != "")
            {
                TextRenderer.DrawText(g, titel, TitelSchrift, new Rectangle(rand, y, Width - 2 * rand, titelGroesse.Height), Theme.Text, Umbruch);
                y += titelGroesse.Height + Theme.S(6);
            }
            TextRenderer.DrawText(g, text, TextSchrift, new Rectangle(rand, y, Width - 2 * rand, textGroesse.Height), Theme.Text, Umbruch);
            Popup.Rand(g, Size);
        }
    }

    // Hinweise zum Plan als Popup: Warnzeichen und umbrochener Text pro Hinweis, das Mausrad blättert
    public class HinweisText : ThemaControl
    {
        static readonly Font TitelSchrift = Schrift.Ueberschrift, TextSchrift = Schrift.Text;
        const TextFormatFlags Umbruch = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
        readonly string titel;
        readonly List<string> texte;
        readonly List<int> hoehen;
        readonly int rand = Theme.S(14), zeichen = Theme.S(16), abstand = Theme.S(10), titelH, inhaltH, sichtbarH;
        int versatz;

        HinweisText(string titel, IEnumerable<string> hinweise)
        {
            SetStyle(ControlStyles.Selectable, true);
            this.titel = titel;
            texte = hinweise.ToList();
            int breite = Theme.S(540);
            int textW = breite - 2 * rand - zeichen - Theme.S(10);
            hoehen = texte.Select(t => Math.Max(zeichen, TextRenderer.MeasureText(t, TextSchrift, new Size(textW, 0), Umbruch).Height)).ToList();
            titelH = TextRenderer.MeasureText(titel, TitelSchrift).Height + Theme.S(10);
            inhaltH = hoehen.Sum() + abstand * Math.Max(0, texte.Count - 1);
            sichtbarH = Math.Min(inhaltH, Theme.S(380));
            Size = new Size(breite, rand + titelH + sichtbarH + rand);
        }

        public static Popup Zeigen(Control anker, Rectangle feld, object besitzer, string titel, IEnumerable<string> hinweise)
        {
            var p = new Popup(new HinweisText(titel, hinweise), besitzer);
            p.ZeigenBei(anker, feld);
            return p;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            versatz = Math.Max(0, Math.Min(inhaltH - sichtbarH, versatz - Math.Sign(e.Delta) * Theme.S(48)));
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Karte);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            TextRenderer.DrawText(g, titel, TitelSchrift, new Point(rand, rand), Theme.Text, TextFormatFlags.NoPrefix);
            var liste = new Rectangle(rand, rand + titelH, Width - 2 * rand, sichtbarH);
            g.SetClip(liste);
            int y = liste.Top - versatz;
            for (int i = 0; i < texte.Count; i++)
            {
                if (y + hoehen[i] >= liste.Top && y <= liste.Bottom)
                {
                    Lucide.Zeichne(g, "triangle-alert", new RectangleF(liste.Left, y + Theme.S(1), zeichen, zeichen), Theme.Warnung);
                    TextRenderer.DrawText(g, texte[i], TextSchrift, new Rectangle(liste.Left + zeichen + Theme.S(10), y, liste.Width - zeichen - Theme.S(10), hoehen[i]), Theme.Text, Umbruch);
                }
                y += hoehen[i] + abstand;
            }
            g.ResetClip();
            // Bildlaufleiste wie überall, wenn nicht alles Platz hat (blättern mit dem Mausrad)
            if (inhaltH > sichtbarH)
            {
                float h = Math.Max(Theme.S(28), liste.Height * (float)sichtbarH / inhaltH);
                float top = (liste.Height - h) * versatz / (inhaltH - sichtbarH);
                Bildlaufleiste.Zeichnen(g, new RectangleF(Width - Theme.S(12), liste.Top, Bildlaufleiste.Dicke, liste.Height), top, h, false, false);
            }
            Popup.Rand(g, Size);
        }
    }

    // Kurzinfo beim Darüberfahren im eigenen Design (statt des Windows-Tooltips)
    public class ThemaTipp : ToolTip
    {
        static readonly Font TippSchrift = Schrift.Klein;
        const TextFormatFlags Umbruch = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;   // lange Texte höchstens 420 px breit
        string freierText;   // Text von Zeigen() (nicht über SetToolTip)

        // Gemeinsame Kurzinfo für Knöpfe und Seitenleiste (ThemaControl.Tipp)
        public static readonly ThemaTipp Standard = new ThemaTipp();

        // An einer bestimmten Stelle zeigen, z. B. für eine Tabellenzelle
        public void Zeigen(string text, Control c, Point p)
        {
            freierText = text;
            Show(text, c, p, 6000);
        }

        public void Verbergen(Control c)
        {
            freierText = null;
            Hide(c);
        }

        public ThemaTipp()
        {
            OwnerDraw = true;
            InitialDelay = 400;
            Popup += (s, e) =>
            {
                var text = TextRenderer.MeasureText(freierText ?? GetToolTip(e.AssociatedControl), TippSchrift, new Size(Theme.S(420), 0), Umbruch);
                e.ToolTipSize = new Size(text.Width + Theme.S(16), text.Height + Theme.S(10));
            };
            Draw += (s, e) =>
            {
                var g = e.Graphics;
                using (var b = new SolidBrush(Theme.Karte))
                    g.FillRectangle(b, e.Bounds);
                using (var p = new Pen(Theme.Rand))
                    g.DrawRectangle(p, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
                TextRenderer.DrawText(g, e.ToolTipText, TippSchrift, Rectangle.Inflate(e.Bounds, -Theme.S(8), -Theme.S(5)), Theme.Text, Umbruch | TextFormatFlags.VerticalCenter);
            };
        }
    }

    // Liste zum Auswählen; mit mehrfach = true als Liste mit Häkchen (bleibt offen).
    // Mit symbol steht vor jedem Eintrag ein Icon (Menü, z. B. die Datei-Befehle).
    public class AuswahlListe : ThemaControl
    {
        readonly string[] eintraege;
        readonly HashSet<string> gewaehlt;
        readonly bool mehrfach;
        readonly Func<string, string> anzeige, symbol;
        readonly int zeileH = Theme.S(36), rand = Theme.S(6);
        readonly Timer scrollen = new Timer { Interval = 12 };
        int hover = -1;
        float versatz, ziel;

        public event Action<string> Gewaehlt;
        public event Action<List<string>> Geaendert;

        public AuswahlListe(IEnumerable<string> eintraege, IEnumerable<string> aktuell, bool mehrfach, int breite,
            Func<string, string> anzeige = null, Func<string, string> symbol = null)
        {
            this.eintraege = eintraege.ToArray();
            this.mehrfach = mehrfach;
            this.anzeige = anzeige ?? (s => s);
            this.symbol = symbol;
            gewaehlt = new HashSet<string>(aktuell ?? new string[0]);
            Font = Schrift.Eingabe;
            int sichtbar = Math.Min(8, Math.Max(1, this.eintraege.Length));
            int textBreite = this.eintraege.Length == 0 ? 0 : this.eintraege.Max(s => TextRenderer.MeasureText(this.anzeige(s), Font).Width);
            Size = new Size(Math.Max(breite, textBreite + Theme.S(mehrfach || symbol != null ? 80 : 60)), sichtbar * zeileH + 2 * rand);
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
            var fett = Schrift.Ueberschrift;
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
                else if (symbol != null)
                {
                    Lucide.Zeichne(g, symbol(eintraege[i]), new RectangleF(x, r.Y + (r.Height - icon) / 2, icon, icon), Theme.Gedaempft);
                    x += icon + Theme.S(12);
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

    // Farbpalette mit «Eigene Farbe…»; enthält alle Farben des Beispiels (Konfiguration.Beispiel)
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
        readonly int feld = Theme.S(30), luecke = Theme.S(8), rand = Theme.S(14), knopfH = Theme.S(36);
        readonly Color aktuell;
        int hover = -1;   // 0..23 Farbe, 24 = Weitere Farben

        public event Action<Color> Gewaehlt;
        public event Action WeitereFarben;

        public FarbAuswahl(Color aktuell)
        {
            this.aktuell = aktuell;
            Font = Schrift.Text;
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
            TextRenderer.DrawText(g, "Eigene Farbe…", Font, Rectangle.FromLTRB(k.X + icon + Theme.S(16), k.Y, k.Right, k.Bottom), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            // aktuelle Farbe, wenn sie nicht aus der Palette stammt
            if (!Palette.Any(p => Color.FromArgb(255, Color.FromArgb(p)).ToArgb() == aktuell.ToArgb()))
            {
                var chip = new RectangleF(k.Right - Theme.S(8) - Theme.S(36), k.Y + (k.Height - Theme.S(20)) / 2f, Theme.S(36), Theme.S(20));
                Formen.Umrandung(g, RectangleF.Inflate(chip, Theme.S(3), Theme.S(3)), Theme.S(7), Theme.Akzent, 2f);
                Formen.Fuellen(g, chip, Theme.S(5), aktuell);
                Formen.Umrandung(g, chip, Theme.S(5), Color.FromArgb(30, 0, 0, 0));
            }
            Popup.Rand(g, Size);
        }
    }

    // Freie Farbwahl: Fläche für Sättigung/Helligkeit, Leiste für den Farbton, Hex-Eingabe und Vorschau (vorher/neu)
    public class FarbMischer : ThemaControl
    {
        readonly int rand = Theme.S(14), kopfH = Theme.S(30), flaecheH = Theme.S(150), leisteH = Theme.S(14), zeileH = Theme.S(38), vorschauB = Theme.S(76);
        readonly Color vorher;
        readonly Eingabefeld hex;
        Color neu;
        float farbton, saettigung, helligkeit;   // 0..360, 0..1, 0..1
        int ziehen;   // 0 = nichts, 1 = Fläche, 2 = Farbton-Leiste
        bool hoverZurueck, hexSchreiben;

        public event Action<Color> Gewaehlt;
        public event Action Zurueck;

        public FarbMischer(Color aktuell)
        {
            vorher = neu = Color.FromArgb(255, aktuell);
            BackColor = Theme.Karte;
            Font = Schrift.Text;
            AusFarbe(neu);

            int y = Flaeche.Bottom + Theme.S(12) + leisteH + Theme.S(14);
            hex = new Eingabefeld { Bounds = new Rectangle(rand + vorschauB + Theme.S(10), y, Theme.S(276) - 2 * rand - vorschauB - Theme.S(10), zeileH) };
            hex.Box.MaxLength = 7;
            hex.Box.CharacterCasing = CharacterCasing.Upper;
            hex.Box.PreviewKeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) e.IsInputKey = true; };
            hex.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Melden(); } };
            hex.Box.TextChanged += (s, e) =>
            {
                if (hexSchreiben) return;
                Color c;
                if (!HexLesen(hex.Text, out c)) return;
                neu = c;
                AusFarbe(c);
                Invalidate();
            };
            Controls.Add(hex);

            var uebernehmen = new ModernButton("Übernehmen", "check", true) { Untergrundfarbe = Theme.Karte };
            uebernehmen.Bounds = new Rectangle(rand, hex.Bottom + Theme.S(12), hex.Right - rand, uebernehmen.Height);
            uebernehmen.Click += (s, e) => Melden();
            Controls.Add(uebernehmen);

            Size = new Size(Theme.S(276), uebernehmen.Bottom + rand);
            HexSchreiben();
        }

        Rectangle ZurueckKnopf
        {
            get { return new Rectangle(rand - Theme.S(6), rand, TextRenderer.MeasureText("Palette", Font).Width + Theme.S(36), kopfH); }
        }

        Rectangle Flaeche
        {
            get { return new Rectangle(rand, rand + kopfH + Theme.S(8), Theme.S(276) - 2 * rand, flaecheH); }
        }

        Rectangle Leiste
        {
            get { return new Rectangle(rand, Flaeche.Bottom + Theme.S(12), Flaeche.Width, leisteH); }
        }

        Rectangle Vorschau
        {
            get { return new Rectangle(rand, Leiste.Bottom + Theme.S(14), vorschauB, zeileH); }
        }

        void Melden()
        {
            var h = Gewaehlt;
            if (h != null) h(neu);
        }

        // ---- Umrechnung RGB <-> HSV

        void AusFarbe(Color c)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            helligkeit = max;
            saettigung = max <= 0 ? 0 : (max - min) / max;
            if (max - min > 0.0001f) farbton = c.GetHue();   // bei Grau den bisherigen Farbton behalten
        }

        static Color AusHsv(float h, float s, float v)
        {
            h = ((h % 360) + 360) % 360 / 60f;
            int i = (int)Math.Floor(h) % 6;
            float f = h - (float)Math.Floor(h);
            float p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
            float r, g, b;
            switch (i)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
            return Color.FromArgb((int)Math.Round(r * 255), (int)Math.Round(g * 255), (int)Math.Round(b * 255));
        }

        // «#RRGGBB», «RRGGBB» oder kurz «#RGB»
        static bool HexLesen(string text, out Color farbe)
        {
            farbe = Color.Empty;
            string s = (text ?? "").Trim().TrimStart('#');
            if (s.Length == 3) s = new string(new[] { s[0], s[0], s[1], s[1], s[2], s[2] });
            int wert;
            if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out wert)) return false;
            farbe = Color.FromArgb(255, Color.FromArgb(wert));
            return true;
        }

        void HexSchreiben()
        {
            hexSchreiben = true;
            hex.Text = string.Format("#{0:X2}{1:X2}{2:X2}", neu.R, neu.G, neu.B);
            hexSchreiben = false;
        }

        // ---- Maus

        void Ziehen(Point p)
        {
            if (ziehen == 1)
            {
                var f = Flaeche;
                saettigung = Math.Max(0, Math.Min(1, (p.X - f.X) / (float)f.Width));
                helligkeit = 1 - Math.Max(0, Math.Min(1, (p.Y - f.Y) / (float)f.Height));
            }
            else if (ziehen == 2)
            {
                var l = Leiste;
                farbton = Math.Max(0, Math.Min(359.9f, (p.X - l.X) / (float)l.Width * 360));
            }
            else return;
            neu = AusHsv(farbton, saettigung, helligkeit);
            HexSchreiben();
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (ZurueckKnopf.Contains(e.Location)) { var h = Zurueck; if (h != null) h(); }
                else if (Rectangle.Inflate(Flaeche, Theme.S(4), Theme.S(4)).Contains(e.Location)) ziehen = 1;
                else if (Rectangle.Inflate(Leiste, Theme.S(4), Theme.S(8)).Contains(e.Location)) ziehen = 2;
                Ziehen(e.Location);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (ziehen > 0) Ziehen(e.Location);
            else
            {
                bool z = ZurueckKnopf.Contains(e.Location);
                if (z != hoverZurueck) { hoverZurueck = z; Invalidate(); }
                Cursor = z || Rectangle.Inflate(Flaeche, Theme.S(4), Theme.S(4)).Contains(e.Location) ||
                         Rectangle.Inflate(Leiste, Theme.S(4), Theme.S(8)).Contains(e.Location) ? Cursors.Hand : Cursors.Default;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            ziehen = 0;
            base.OnMouseUp(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoverZurueck = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        // ---- Zeichnen

        static void Griff(Graphics g, PointF mitte, float d, Color farbe)
        {
            var r = new RectangleF(mitte.X - d / 2, mitte.Y - d / 2, d, d);
            using (var schatten = new Pen(Color.FromArgb(90, 0, 0, 0), 1f))
                g.DrawEllipse(schatten, RectangleF.Inflate(r, 1.5f, 1.5f));
            using (var b = new SolidBrush(farbe))
                g.FillEllipse(b, r);
            using (var weiss = new Pen(Color.White, Theme.S(2.5f)))
                g.DrawEllipse(weiss, r);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Karte);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int icon = Theme.S(16);

            // Kopf: zurück zur Palette, rechts der Titel
            var z = ZurueckKnopf;
            if (hoverZurueck) Formen.Fuellen(g, z, Theme.S(7), Theme.Hover);
            Lucide.Zeichne(g, "chevron-left", new RectangleF(z.X + Theme.S(6), z.Y + (z.Height - icon) / 2f, icon, icon), Theme.Gedaempft);
            TextRenderer.DrawText(g, "Palette", Font, Rectangle.FromLTRB(z.X + icon + Theme.S(10), z.Y, z.Right, z.Bottom), Theme.Gedaempft,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, "Eigene Farbe", Schrift.Ueberschrift, new Rectangle(rand, rand, Width - 2 * rand, kopfH), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.SingleLine);

            // Fläche: von links Weiss nach rechts voller Farbton, nach unten zu Schwarz
            var f = Flaeche;
            using (var pfad = Formen.Rund(f, Theme.S(8)))
            {
                var zustand = g.Save();
                g.SetClip(pfad);
                using (var b = new SolidBrush(AusHsv(farbton, 1, 1)))
                    g.FillRectangle(b, f);
                var breiter = Rectangle.Inflate(f, 1, 1);
                using (var b = new LinearGradientBrush(breiter, Color.White, Color.FromArgb(0, Color.White), 0f))
                    g.FillRectangle(b, f);
                using (var b = new LinearGradientBrush(breiter, Color.FromArgb(0, Color.Black), Color.Black, 90f))
                    g.FillRectangle(b, f);
                g.Restore(zustand);
                using (var p = new Pen(Color.FromArgb(30, 0, 0, 0)))
                    g.DrawPath(p, pfad);
            }
            Griff(g, new PointF(f.X + saettigung * f.Width, f.Y + (1 - helligkeit) * f.Height), Theme.S(14), neu);

            // Farbton-Leiste (Regenbogen)
            var l = Leiste;
            using (var b = new LinearGradientBrush(Rectangle.Inflate(l, 1, 0), Color.Red, Color.Red, 0f))
            {
                b.InterpolationColors = new ColorBlend
                {
                    Colors = new[] { Color.Red, Color.Yellow, Color.Lime, Color.Cyan, Color.Blue, Color.Magenta, Color.Red },
                    Positions = new[] { 0f, 1 / 6f, 2 / 6f, 3 / 6f, 4 / 6f, 5 / 6f, 1f },
                };
                using (var pfad = Formen.Rund(l, l.Height / 2f))
                    g.FillPath(b, pfad);
            }
            Griff(g, new PointF(l.X + farbton / 360f * l.Width, l.Y + l.Height / 2f), leisteH + Theme.S(4), AusHsv(farbton, 1, 1));

            // Vorschau: links die bisherige, rechts die neue Farbe
            var v = Vorschau;
            using (var pfad = Formen.Rund(v, Theme.S(8)))
            {
                var zustand = g.Save();
                g.SetClip(pfad);
                using (var b = new SolidBrush(vorher))
                    g.FillRectangle(b, v.X, v.Y, v.Width / 2, v.Height);
                using (var b = new SolidBrush(neu))
                    g.FillRectangle(b, v.X + v.Width / 2, v.Y, v.Width - v.Width / 2, v.Height);
                g.Restore(zustand);
                using (var p = new Pen(Theme.EingabeRand))
                    g.DrawPath(p, pfad);
            }
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

            TextRenderer.DrawText(g, monat.ToString("MMMM yyyy", De), Schrift.Ueberschrift, new Rectangle(0, rand - Theme.S(4), Width, Theme.S(30)), Theme.Text, mitte);
            foreach (bool vor in new[] { false, true })
            {
                var p = Pfeil(vor);
                if (hover == (vor ? VOR : ZURUECK)) Formen.Fuellen(g, p, Theme.S(7), Theme.Hover);
                Lucide.Zeichne(g, vor ? "chevron-right" : "chevron-left", new RectangleF(p.X + (p.Width - icon) / 2f, p.Y + (p.Height - icon) / 2f, icon, icon), Theme.Gedaempft);
            }
            for (int i = 0; i < 7; i++)
                TextRenderer.DrawText(g, Wochentage[i], Schrift.Beschriftung, new Rectangle(rand + i * zelle, rand + kopfH, zelle, tagH), Theme.Gedaempft, mitte);

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
            TextRenderer.DrawText(g, "Heute", Schrift.Fett, k, Theme.Akzent, mitte);
            Popup.Rand(g, Size);
        }
    }
}
