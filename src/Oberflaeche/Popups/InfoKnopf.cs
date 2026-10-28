using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Steuerelemente;

namespace ShiftDesk.Oberflaeche.Popups
{
    /// <summary>
    /// Kleiner Info-Knopf neben einer Beschriftung: ein Klick zeigt die Erklärung in einem Popup.
    /// </summary>
    public class InfoKnopf : ThemaControl
    {
        readonly string titel, text;

        /// <param name="titel">Titel im Popup (meist die Beschriftung neben dem Knopf).</param>
        /// <param name="text">Die Erklärung.</param>
        /// <param name="groesse">Kantenlänge bei 96 dpi.</param>
        public InfoKnopf(string titel, string text, int groesse = 20)
        {
            this.titel = titel;
            this.text = text;
            Size = new Size(Thema.S(groesse), Thema.S(groesse));
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
}
