using System.Drawing;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>Senkrechter Strich zwischen Gruppen einer Werkzeugleiste.</summary>
    public class Trenner : ThemaControl
    {
        public Trenner()
        {
            Size = new Size(Thema.S(17), Thema.KnopfHoehe);
            Margin = new Padding(Thema.S(4), 0, 0, 0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Untergrund);
            int x = Width / 2, rand = Thema.S(6);
            using (var p = new Pen(Thema.Rand))
                e.Graphics.DrawLine(p, x, rand, x, Height - rand);
        }
    }
}
