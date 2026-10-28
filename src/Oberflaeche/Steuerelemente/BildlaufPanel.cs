using System;
using System.Drawing;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>
    /// Bereich mit senkrechtem Bildlauf (Mausrad, ohne Leiste) für einen Inhalt, der höher werden kann als der Platz.
    /// Die Höhe des Inhalts kommt aus dessen <see cref="Control.GetPreferredSize"/>.
    /// </summary>
    public class BildlaufPanel : Panel
    {
        readonly Control inhalt;
        int versatz;

        /// <param name="inhalt">Der Inhalt; er bekommt die ganze Breite und die Höhe, die er braucht.</param>
        public BildlaufPanel(Control inhalt)
        {
            this.inhalt = inhalt;
            DoubleBuffered = true;
            Controls.Add(inhalt);
        }

        /// <summary>Höhe, die der Inhalt bei dieser Breite ganz braucht.</summary>
        public int InhaltHoehe(int breite)
        {
            return inhalt.GetPreferredSize(new Size(Math.Max(1, breite), 0)).Height;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            Anordnen();
        }

        void Anordnen()
        {
            int breite = ClientSize.Width, hoehe = ClientSize.Height;
            if (breite <= 0 || hoehe <= 0) return;
            int h = InhaltHoehe(breite);
            versatz = Math.Max(0, Math.Min(Math.Max(0, h - hoehe), versatz));
            var ziel = new Rectangle(0, -versatz, breite, h);
            if (inhalt.Bounds != ziel) inhalt.Bounds = ziel;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int vorher = versatz;
            versatz -= Math.Sign(e.Delta) * Thema.S(60);
            Anordnen();
            var h = e as HandledMouseEventArgs;
            if (h != null) h.Handled = versatz != vorher || InhaltHoehe(ClientSize.Width) > ClientSize.Height;
            base.OnMouseWheel(e);
        }
    }
}
