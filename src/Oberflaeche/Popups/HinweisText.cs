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
    /// Hinweise zum Plan als Popup: Warnzeichen und umbrochener Text pro Hinweis; das Mausrad blättert.
    /// </summary>
    public class HinweisText : ThemaControl
    {
        static readonly Font TitelSchrift = Schrift.Ueberschrift, TextSchrift = Schrift.Text;
        const TextFormatFlags Umbruch = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
        readonly string titel;
        readonly List<string> texte;
        readonly List<int> hoehen;
        readonly int rand = Thema.S(14), zeichen = Thema.S(16), abstand = Thema.S(10), titelH, inhaltH, sichtbarH;
        int versatz;

        HinweisText(string titel, IEnumerable<string> hinweise)
        {
            SetStyle(ControlStyles.Selectable, true);
            this.titel = titel;
            texte = hinweise.ToList();
            int breite = Thema.S(540);
            int textW = breite - 2 * rand - zeichen - Thema.S(10);
            hoehen = texte.Select(t => Math.Max(zeichen, TextRenderer.MeasureText(t, TextSchrift, new Size(textW, 0), Umbruch).Height)).ToList();
            titelH = TextRenderer.MeasureText(titel, TitelSchrift).Height + Thema.S(10);
            inhaltH = hoehen.Sum() + abstand * Math.Max(0, texte.Count - 1);
            sichtbarH = Math.Min(inhaltH, Thema.S(380));
            Size = new Size(breite, rand + titelH + sichtbarH + rand);
        }

        /// <summary>Zeigt die Hinweise unter dem Feld des Ankers; ein zweiter Klick auf denselben Besitzer schliesst wieder.</summary>
        public static Popup Zeigen(Control anker, Rectangle feld, object besitzer, string titel, IEnumerable<string> hinweise)
        {
            var p = new Popup(new HinweisText(titel, hinweise), besitzer);
            p.ZeigenBei(anker, feld);
            return p;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            versatz = Math.Max(0, Math.Min(inhaltH - sichtbarH, versatz - Math.Sign(e.Delta) * Thema.S(48)));
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Thema.Karte);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            TextRenderer.DrawText(g, titel, TitelSchrift, new Point(rand, rand), Thema.Text, TextFormatFlags.NoPrefix);
            var liste = new Rectangle(rand, rand + titelH, Width - 2 * rand, sichtbarH);
            g.SetClip(liste);
            int y = liste.Top - versatz;
            for (int i = 0; i < texte.Count; i++)
            {
                if (y + hoehen[i] >= liste.Top && y <= liste.Bottom)
                {
                    Lucide.Zeichne(g, "triangle-alert", new RectangleF(liste.Left, y + Thema.S(1), zeichen, zeichen), Thema.Warnung);
                    TextRenderer.DrawText(g, texte[i], TextSchrift, new Rectangle(liste.Left + zeichen + Thema.S(10), y, liste.Width - zeichen - Thema.S(10), hoehen[i]), Thema.Text, Umbruch);
                }
                y += hoehen[i] + abstand;
            }
            g.ResetClip();
            Popup.Rand(g, Size);
        }
    }
}
