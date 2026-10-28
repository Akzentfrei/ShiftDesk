using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>Kleiner Knopf nur mit Symbol (z. B. Seitenleiste ein- und ausklappen); beim Darüberfahren hinterlegt.</summary>
    public class SymbolKnopf : ThemaControl
    {
        /// <summary>Lucide-Symbol.</summary>
        public string Icon;

        /// <param name="icon">Lucide-Symbol.</param>
        public SymbolKnopf(string icon)
        {
            Icon = icon;
            Size = new Size(Thema.S(32), Thema.S(32));
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Hover) Formen.Fuellen(g, new RectangleF(0, 0, Width, Height), Thema.S(8), Thema.Hover);
            float s = Thema.S(18);
            Lucide.Zeichne(g, Icon, new RectangleF((Width - s) / 2, (Height - s) / 2, s, s), Hover ? Thema.Text : Thema.Gedaempft);
        }
    }
}
