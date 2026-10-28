using System;
using System.Drawing;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Steuerelemente;

namespace ShiftDesk.Oberflaeche.Popups
{
    /// <summary>
    /// Inhalt des Info-Popups: Titel und umbrochener Erklärungstext.
    /// </summary>
    public class InfoText : ThemaControl
    {
        static readonly Font TitelSchrift = Schrift.Ueberschrift, TextSchrift = Schrift.Text;
        const TextFormatFlags Umbruch = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
        readonly string titel, text;
        readonly int rand = Thema.S(14);
        readonly Size titelGroesse, textGroesse;

        InfoText(string titel, string text)
        {
            this.titel = titel ?? "";
            this.text = text;
            int innen = Thema.S(360);
            titelGroesse = this.titel == "" ? Size.Empty : TextRenderer.MeasureText(this.titel, TitelSchrift, new Size(innen, 0), Umbruch);
            textGroesse = TextRenderer.MeasureText(text, TextSchrift, new Size(innen, 0), Umbruch);
            int abstand = this.titel == "" ? 0 : Thema.S(6);
            Size = new Size(Math.Max(titelGroesse.Width, textGroesse.Width) + 2 * rand, rand + titelGroesse.Height + abstand + textGroesse.Height + rand);
        }

        /// <summary>Unter dem Feld des Ankers zeigen; ein zweiter Klick auf denselben Besitzer schliesst wieder.</summary>
        public static Popup Zeigen(Control anker, Rectangle feld, object besitzer, string titel, string text)
        {
            var p = new Popup(new InfoText(titel, text), besitzer);
            p.ZeigenBei(anker, feld);
            return p;
        }

        /// <summary>Das Info-Zeichen, gleich für Knopf und Tabellenkopf.</summary>
        public static void Symbol(Graphics g, RectangleF feld, bool hover)
        {
            float s = Math.Min(feld.Width, feld.Height) - Thema.S(2);
            Lucide.Zeichne(g, "info", new RectangleF(feld.X + (feld.Width - s) / 2, feld.Y + (feld.Height - s) / 2, s, s), hover ? Thema.Akzent : Thema.Gedaempft, 2f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Thema.Karte);
            int y = rand;
            if (titel != "")
            {
                TextRenderer.DrawText(g, titel, TitelSchrift, new Rectangle(rand, y, Width - 2 * rand, titelGroesse.Height), Thema.Text, Umbruch);
                y += titelGroesse.Height + Thema.S(6);
            }
            TextRenderer.DrawText(g, text, TextSchrift, new Rectangle(rand, y, Width - 2 * rand, textGroesse.Height), Thema.Text, Umbruch);
            Popup.Rand(g, Size);
        }
    }
}
