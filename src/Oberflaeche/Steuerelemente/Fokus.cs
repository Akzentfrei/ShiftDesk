using System.Windows.Forms;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>
    /// Steuerelemente, die fürs Mausrad den Fokus brauchen, holen ihn sich beim Darüberfahren – aber nicht,
    /// solange jemand in einem Textfeld tippt (sonst gehen die nächsten Tasten ins Leere).
    /// </summary>
    public static class Fokus
    {
        /// <summary>Gibt dem Steuerelement den Fokus, ausser ein Textfeld hat ihn.</summary>
        public static void FuerMausrad(Control c)
        {
            if (!TextfeldAktiv(c)) c.Focus();
        }

        static bool TextfeldAktiv(Control c)
        {
            var form = c.FindForm();
            Control aktiv = form == null ? null : form.ActiveControl;
            while (aktiv is ContainerControl && ((ContainerControl)aktiv).ActiveControl != null)
                aktiv = ((ContainerControl)aktiv).ActiveControl;
            return aktiv is TextBoxBase;
        }
    }
}
