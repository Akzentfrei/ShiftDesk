using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>Weisse (bzw. dunkle) Fläche mit runden Ecken und feinem Rand, z. B. um Tabellen und Einstellungen.</summary>
    public class Karte : Panel
    {
        public Karte()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Padding = new Padding(Thema.S(14));
            BackColor = Thema.Karte;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Thema.Hintergrund);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Formen.Fuellen(g, r, Thema.RadiusKarte, Thema.Karte);
            Formen.Umrandung(g, r, Thema.RadiusKarte, Thema.Rand);
        }
    }
}
