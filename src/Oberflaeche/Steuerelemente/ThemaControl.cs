using System;
using System.Drawing;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Popups;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>
    /// Basis für selbst gezeichnete Steuerelemente: doppelt gepuffert, Hintergrund in der Farbe des
    /// Elternelements (für runde Ecken) und merkt sich, ob die Maus darüber ist.
    /// </summary>
    public class ThemaControl : Control
    {
        public ThemaControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        /// <summary>Feste Farbe hinter runden Ecken; null = die Farbe des Elternelements.</summary>
        public Color? Untergrundfarbe;

        /// <summary>Die Farbe hinter runden Ecken.</summary>
        protected Color Untergrund
        {
            get { return Untergrundfarbe ?? (Parent != null ? Parent.BackColor : Thema.Hintergrund); }
        }

        /// <summary>Ist die Maus gerade über dem Steuerelement?</summary>
        protected bool Hover { get; set; }

        /// <summary>Kurzinfo beim Darüberfahren (null = keine).</summary>
        public string Tipp
        {
            set { ThemaTipp.Standard.SetToolTip(this, value); }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Untergrund);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            Hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            Hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }
    }
}
