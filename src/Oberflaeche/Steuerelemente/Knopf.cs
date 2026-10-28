using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>
    /// Knopf mit Symbol und Text: hell umrandet, blau gefüllt (<see cref="Primaer"/>) oder rot (<see cref="Gefahr"/>).
    /// Wird es eng, zeigt er nur noch das Symbol (<see cref="Kompakt"/>).
    /// </summary>
    public class Knopf : ThemaControl
    {
        /// <summary>Lucide-Symbol links vom Text (null = keins).</summary>
        public string Icon;

        /// <summary>Hauptknopf einer Seite oder eines Dialogs: blau gefüllt.</summary>
        public bool Primaer;

        /// <summary>Rot gefüllt (Löschen u. ä.).</summary>
        public bool Gefahr;

        /// <summary>Tastaturfokus sichtbar machen (in Dialogen).</summary>
        public bool Fokusrahmen;

        bool kompakt;

        /// <param name="text">Beschriftung ("" = nur Symbol, quadratisch).</param>
        /// <param name="icon">Lucide-Symbol links vom Text (null = keins).</param>
        /// <param name="primaer">Blau gefüllter Hauptknopf.</param>
        public Knopf(string text, string icon, bool primaer = false)
        {
            Text = text;
            Icon = icon;
            Primaer = primaer;
            Cursor = Cursors.Hand;
            Font = Schrift.Fett;
            Height = Thema.KnopfHoehe;
            Margin = new Padding(Thema.S(8), 0, 0, 0);
            Width = VolleBreite;
        }

        /// <summary>Knopf, der beim Klick <paramref name="aktion"/> ausführt.</summary>
        public static Knopf Mit(string text, string icon, Action aktion, bool primaer = false)
        {
            var k = new Knopf(text, icon, primaer);
            k.Click += (s, e) => aktion();
            return k;
        }

        /// <summary>Breite mit Symbol und Text.</summary>
        public int VolleBreite
        {
            get
            {
                if (string.IsNullOrEmpty(Text)) return Height;
                return Thema.S(14) + (Icon != null ? Thema.S(16 + 8) : 0) + TextRenderer.MeasureText(Text, Font).Width + Thema.S(14);
            }
        }

        /// <summary>Nur das Symbol zeigen, wenn es eng wird; der Text erscheint dann als Kurzinfo.</summary>
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

        /// <summary>Enter und Leertaste lösen den Knopf mit Fokus aus.</summary>
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
            float radius = Thema.RadiusFeld;
            bool aktiv = Hover && Enabled;
            if (Gefahr)
                Formen.Fuellen(g, r, radius, aktiv ? Thema.GefahrKnopfHover : Thema.GefahrKnopf);
            else if (Primaer)
                Formen.Fuellen(g, r, radius, aktiv ? Thema.AkzentHover : Thema.Akzent);
            else
            {
                Formen.Fuellen(g, r, radius, aktiv ? Thema.Hover : Thema.Karte);
                Formen.Umrandung(g, r, radius, Thema.EingabeRand);
            }
            if (Fokusrahmen && Focused)
                Formen.Umrandung(g, new RectangleF(1.5f, 1.5f, Width - 3, Height - 3), radius - 1, Thema.Akzent, 2f);
            Color farbe = !Enabled ? Thema.Gedaempft : Primaer || Gefahr ? Thema.AkzentText : Thema.Text;
            string text = kompakt ? "" : Text;
            int icon = Thema.S(16), luecke = string.IsNullOrEmpty(text) || Icon == null ? 0 : Thema.S(8);
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
}
