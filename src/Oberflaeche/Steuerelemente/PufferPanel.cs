using System.Windows.Forms;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>Panel, das selbst zeichnet und dabei doppelt gepuffert ist (kein Flackern).</summary>
    public class PufferPanel : Panel
    {
        public PufferPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
    }
}
