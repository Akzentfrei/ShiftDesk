using System.Drawing;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;

namespace ShiftDesk.Oberflaeche.Popups
{
    /// <summary>
    /// Kurzinfo beim Darüberfahren im eigenen Design (statt des Windows-Tooltips).
    /// </summary>
    public class ThemaTipp : ToolTip
    {
        static readonly Font TippSchrift = Schrift.Klein;
        const TextFormatFlags Umbruch = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;   // lange Texte höchstens 420 px breit
        string freierText;   // Text von Zeigen() (nicht über SetToolTip)

        /// <summary>Gemeinsame Kurzinfo für Knöpfe und Seitenleiste (ThemaControl.Tipp).</summary>
        public static readonly ThemaTipp Standard = new ThemaTipp();

        /// <summary>An einer bestimmten Stelle zeigen, z. B. für eine Tabellenzelle.</summary>
        public void Zeigen(string text, Control c, Point p)
        {
            freierText = text;
            Show(text, c, p, 6000);
        }

        /// <summary>Versteckt eine mit <see cref="Zeigen"/> gezeigte Kurzinfo.</summary>
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
                var text = TextRenderer.MeasureText(freierText ?? GetToolTip(e.AssociatedControl), TippSchrift, new Size(Thema.S(420), 0), Umbruch);
                e.ToolTipSize = new Size(text.Width + Thema.S(16), text.Height + Thema.S(10));
            };
            Draw += (s, e) =>
            {
                var g = e.Graphics;
                using (var b = new SolidBrush(Thema.Karte))
                    g.FillRectangle(b, e.Bounds);
                using (var p = new Pen(Thema.Rand))
                    g.DrawRectangle(p, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
                TextRenderer.DrawText(g, e.ToolTipText, TippSchrift, Rectangle.Inflate(e.Bounds, -Thema.S(8), -Thema.S(5)), Thema.Text, Umbruch | TextFormatFlags.VerticalCenter);
            };
        }
    }
}
