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
    /// Freie Farbwahl: Fläche für Sättigung/Helligkeit, Leiste für den Farbton, Hex-Eingabe und Vorschau
    /// (links die bisherige, rechts die neue Farbe). «Palette» führt zurück zur <see cref="FarbAuswahl"/>.
    /// </summary>
    public class FarbMischer : ThemaControl
    {
        readonly int rand = Thema.S(14), kopfH = Thema.S(30), flaecheH = Thema.S(150), leisteH = Thema.S(14), zeileH = Thema.S(38), vorschauB = Thema.S(76);
        readonly Color vorher;
        readonly Eingabefeld hex;
        Color neu;
        float farbton, saettigung, helligkeit;   // 0..360, 0..1, 0..1
        int ziehen;   // 0 = nichts, 1 = Fläche, 2 = Farbton-Leiste
        bool hoverZurueck, hexSchreiben;

        /// <summary>«Übernehmen» oder Enter im Hex-Feld: die neue Farbe.</summary>
        public event Action<Color> Gewaehlt;
        /// <summary>«Palette» wurde angeklickt.</summary>
        public event Action Zurueck;

        /// <param name="aktuell">Die bisherige Farbe; sie ist der Ausgangspunkt und bleibt in der Vorschau links stehen.</param>
        public FarbMischer(Color aktuell)
        {
            vorher = neu = Color.FromArgb(255, aktuell);
            BackColor = Thema.Karte;
            Font = Schrift.Text;
            AusFarbe(neu);

            int y = Flaeche.Bottom + Thema.S(12) + leisteH + Thema.S(14);
            hex = new Eingabefeld { Bounds = new Rectangle(rand + vorschauB + Thema.S(10), y, Thema.S(276) - 2 * rand - vorschauB - Thema.S(10), zeileH) };
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

            var uebernehmen = new Knopf("Übernehmen", "check", true) { Untergrundfarbe = Thema.Karte };
            uebernehmen.Bounds = new Rectangle(rand, hex.Bottom + Thema.S(12), hex.Right - rand, uebernehmen.Height);
            uebernehmen.Click += (s, e) => Melden();
            Controls.Add(uebernehmen);

            Size = new Size(Thema.S(276), uebernehmen.Bottom + rand);
            HexSchreiben();
        }

        Rectangle ZurueckKnopf
        {
            get { return new Rectangle(rand - Thema.S(6), rand, TextRenderer.MeasureText("Palette", Font).Width + Thema.S(36), kopfH); }
        }

        Rectangle Flaeche
        {
            get { return new Rectangle(rand, rand + kopfH + Thema.S(8), Thema.S(276) - 2 * rand, flaecheH); }
        }

        Rectangle Leiste
        {
            get { return new Rectangle(rand, Flaeche.Bottom + Thema.S(12), Flaeche.Width, leisteH); }
        }

        Rectangle Vorschau
        {
            get { return new Rectangle(rand, Leiste.Bottom + Thema.S(14), vorschauB, zeileH); }
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

        /// <summary>«#RRGGBB», «RRGGBB» oder kurz «#RGB»</summary>
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
                else if (Rectangle.Inflate(Flaeche, Thema.S(4), Thema.S(4)).Contains(e.Location)) ziehen = 1;
                else if (Rectangle.Inflate(Leiste, Thema.S(4), Thema.S(8)).Contains(e.Location)) ziehen = 2;
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
                Cursor = z || Rectangle.Inflate(Flaeche, Thema.S(4), Thema.S(4)).Contains(e.Location) ||
                         Rectangle.Inflate(Leiste, Thema.S(4), Thema.S(8)).Contains(e.Location) ? Cursors.Hand : Cursors.Default;
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
            using (var weiss = new Pen(Color.White, Thema.S(2.5f)))
                g.DrawEllipse(weiss, r);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Thema.Karte);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int icon = Thema.S(16);

            // Kopf: zurück zur Palette, rechts der Titel
            var z = ZurueckKnopf;
            if (hoverZurueck) Formen.Fuellen(g, z, Thema.S(7), Thema.Hover);
            Lucide.Zeichne(g, "chevron-left", new RectangleF(z.X + Thema.S(6), z.Y + (z.Height - icon) / 2f, icon, icon), Thema.Gedaempft);
            TextRenderer.DrawText(g, "Palette", Font, Rectangle.FromLTRB(z.X + icon + Thema.S(10), z.Y, z.Right, z.Bottom), Thema.Gedaempft,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, "Eigene Farbe", Schrift.Ueberschrift, new Rectangle(rand, rand, Width - 2 * rand, kopfH), Thema.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.SingleLine);

            // Fläche: von links Weiss nach rechts voller Farbton, nach unten zu Schwarz
            var f = Flaeche;
            using (var pfad = Formen.Rund(f, Thema.S(8)))
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
            Griff(g, new PointF(f.X + saettigung * f.Width, f.Y + (1 - helligkeit) * f.Height), Thema.S(14), neu);

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
            Griff(g, new PointF(l.X + farbton / 360f * l.Width, l.Y + l.Height / 2f), leisteH + Thema.S(4), AusHsv(farbton, 1, 1));

            // Vorschau: links die bisherige, rechts die neue Farbe
            var v = Vorschau;
            using (var pfad = Formen.Rund(v, Thema.S(8)))
            {
                var zustand = g.Save();
                g.SetClip(pfad);
                using (var b = new SolidBrush(vorher))
                    g.FillRectangle(b, v.X, v.Y, v.Width / 2, v.Height);
                using (var b = new SolidBrush(neu))
                    g.FillRectangle(b, v.X + v.Width / 2, v.Y, v.Width - v.Width / 2, v.Height);
                g.Restore(zustand);
                using (var p = new Pen(Thema.EingabeRand))
                    g.DrawPath(p, pfad);
            }
            Popup.Rand(g, Size);
        }
    }
}
