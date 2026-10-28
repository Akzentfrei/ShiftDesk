using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>Einzeiliges Textfeld im Eingabefeld-Stil: ein randloses Windows-Textfeld in einem selbst gezeichneten Rahmen.</summary>
    public class Eingabefeld : ThemaControl
    {
        readonly TextBox box;

        /// <summary>Nur Ziffern annehmen (z. B. Anzahl Personen).</summary>
        public bool NurZahlen;

        public Eingabefeld()
        {
            box = new TextBox { BorderStyle = BorderStyle.None, Font = Schrift.Eingabe };
            box.GotFocus += (s, e) => Invalidate();
            box.LostFocus += (s, e) => Invalidate();
            // das Textfeld deckt fast alles ab: Darüberfahren auch dort zählen
            box.MouseEnter += (s, e) => HoverPruefen();
            box.MouseLeave += (s, e) => HoverPruefen();
            box.KeyPress += (s, e) => { if (NurZahlen && !char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar)) e.Handled = true; };
            Controls.Add(box);
            Height = Thema.FeldHoehe;
            Cursor = Cursors.IBeam;
            ThemaAnwenden();
        }

        /// <summary>Das eigentliche Textfeld (für Tasten- und Textereignisse).</summary>
        public TextBox Box
        {
            get { return box; }
        }

        /// <summary>Der eingegebene Text.</summary>
        public override string Text
        {
            get { return box == null ? base.Text : box.Text; }
            set { if (box == null) base.Text = value; else box.Text = value; }
        }

        /// <summary>Farben des inneren Textfelds nach einem Wechsel hell/dunkel nachführen.</summary>
        public void ThemaAnwenden()
        {
            box.BackColor = Thema.Eingabe;
            box.ForeColor = Thema.Text;
            Invalidate();
        }

        void HoverPruefen()
        {
            bool h = ClientRectangle.Contains(PointToClient(Cursor.Position));
            if (h != Hover) { Hover = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            HoverPruefen();
        }

        protected override void OnClick(EventArgs e)
        {
            box.Focus();
            base.OnClick(e);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            box.Location = new Point(Thema.S(11), (Height - box.Height) / 2);
            box.Width = Width - Thema.S(22);
            base.OnLayout(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Formen.Eingabe(e.Graphics, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), box.Focused, Hover);
        }
    }
}
