using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Steuerelemente;

namespace ShiftDesk.Oberflaeche.Popups
{
    /// <summary>
    /// Liste zum Auswählen, für Aufklappfelder und Menüs. Mit <c>mehrfach</c> eine Liste mit Häkchen, die
    /// offen bleibt; mit <c>symbol</c> steht vor jedem Eintrag ein Icon (Menü, z. B. die Datei-Befehle).
    /// Tippen springt zum nächsten Eintrag mit diesem Anfangsbuchstaben.
    /// </summary>
    public class AuswahlListe : ThemaControl
    {
        readonly string[] eintraege;
        readonly HashSet<string> gewaehlt;
        readonly bool mehrfach;
        readonly Func<string, string> anzeige, symbol;
        readonly int zeileH = Thema.S(36), rand = Thema.S(6);
        readonly Timer scrollen = new Timer { Interval = 12 };
        int hover = -1;
        float versatz, ziel;

        /// <summary>Ein Eintrag wurde gewählt (nur ohne <c>mehrfach</c>).</summary>
        public event Action<string> Gewaehlt;
        /// <summary>Die Häkchen haben sich geändert: alle gewählten Einträge in Listenreihenfolge (nur mit <c>mehrfach</c>).</summary>
        public event Action<List<string>> Geaendert;

        /// <param name="eintraege">Die Werte der Liste.</param>
        /// <param name="aktuell">Gewählte Werte (hinterlegt bzw. mit Häkchen); die Liste beginnt beim ersten davon.</param>
        /// <param name="mehrfach">Liste mit Häkchen, die beim Wählen offen bleibt.</param>
        /// <param name="breite">Mindestbreite, meist die des Felds darunter.</param>
        /// <param name="anzeige">Text für einen Wert (z. B. "" → «Keine»), null = der Wert selbst.</param>
        /// <param name="symbol">Icon für einen Wert (Menü), null = keine Icons.</param>
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
            Size = new Size(Math.Max(breite, textBreite + Thema.S(mehrfach || symbol != null ? 80 : 60)), sichtbar * zeileH + 2 * rand);
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

        /// <summary>Tippen springt zum nächsten Eintrag mit diesem Anfangsbuchstaben bzw. dieser Ziffer.</summary>
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
            g.Clear(Thema.Karte);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.SetClip(new Rectangle(0, rand, Width, Height - 2 * rand));
            int icon = Thema.S(16);
            var fett = Schrift.Ueberschrift;
            for (int i = 0; i < eintraege.Length; i++)
            {
                float y = rand + i * zeileH - versatz;
                if (y + zeileH < 0 || y > Height) continue;
                var r = new RectangleF(rand, y + 1, Width - 2 * rand, zeileH - 2);
                bool an = gewaehlt.Contains(eintraege[i]);
                // gewählter Eintrag bleibt farbig hinterlegt, Maus und Tastatur zeigen sich dezent
                if (an && !mehrfach) Formen.Fuellen(g, r, Thema.S(7), Thema.Auswahl);
                else if (i == hover) Formen.Fuellen(g, r, Thema.S(7), Thema.Hover);
                int x = (int)r.X + Thema.S(10);
                if (mehrfach)
                {
                    // Kästchen mit Häkchen
                    float k = Thema.S(18);
                    var box = new RectangleF(x, r.Y + (r.Height - k) / 2, k, k);
                    if (an)
                    {
                        Formen.Fuellen(g, box, Thema.S(5), Thema.Akzent);
                        Lucide.Zeichne(g, "check", RectangleF.Inflate(box, -Thema.S(3), -Thema.S(3)), Color.White, 3f);
                    }
                    else
                    {
                        Formen.Fuellen(g, box, Thema.S(5), Thema.Eingabe);
                        Formen.Umrandung(g, box, Thema.S(5), Thema.EingabeRand, 1.5f);
                    }
                    x += (int)k + Thema.S(10);
                }
                else if (symbol != null)
                {
                    Lucide.Zeichne(g, symbol(eintraege[i]), new RectangleF(x, r.Y + (r.Height - icon) / 2, icon, icon), Thema.Gedaempft);
                    x += icon + Thema.S(12);
                }
                string text = anzeige(eintraege[i]);
                bool leer = eintraege[i] == "";
                TextRenderer.DrawText(g, text, an && !mehrfach ? fett : Font,
                    Rectangle.FromLTRB(x, (int)r.Y, (int)r.Right - icon - Thema.S(14), (int)r.Bottom),
                    leer ? Thema.Gedaempft : (an && !mehrfach ? Thema.Akzent : Thema.Text),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                if (an && !mehrfach)
                    Lucide.Zeichne(g, "check", new RectangleF(r.Right - icon - Thema.S(10), r.Y + (r.Height - icon) / 2, icon, icon), Thema.Akzent);
            }
            g.ResetClip();

            // sanfter Verlauf oben/unten, wenn es dort weitergeht (statt Bildlaufleiste)
            int verlauf = Thema.S(22);
            if (versatz > 0.5f)
                using (var b = new LinearGradientBrush(new Rectangle(0, 0, Width, verlauf + 1), Thema.Karte, Color.FromArgb(0, Thema.Karte), 90f))
                    g.FillRectangle(b, 0, 0, Width, verlauf);
            if (versatz < MaxVersatz - 0.5f)
                using (var b = new LinearGradientBrush(new Rectangle(0, Height - verlauf - 1, Width, verlauf + 1), Color.FromArgb(0, Thema.Karte), Thema.Karte, 90f))
                    g.FillRectangle(b, 0, Height - verlauf, Width, verlauf);
            Popup.Rand(g, Size);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) scrollen.Dispose();
            base.Dispose(disposing);
        }
    }
}
