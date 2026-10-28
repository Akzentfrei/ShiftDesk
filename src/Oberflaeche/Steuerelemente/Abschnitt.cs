using System.Drawing;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>Kleine Abschnittsüberschrift in Grossbuchstaben (Seitenleiste, Plan-Einstellungen); schmal = nur ein kurzer Strich.</summary>
    public class Abschnitt : ThemaControl
    {
        /// <summary>Eingeklappte Seitenleiste: statt des Texts ein Strich.</summary>
        public bool Schmal;

        /// <summary>Feine Linie oben, als Abgrenzung zum Abschnitt davor.</summary>
        public bool Trennlinie;

        readonly int einzug;

        /// <param name="text">Überschrift (wird in Grossbuchstaben gezeigt).</param>
        /// <param name="einzug">Abstand des Texts vom linken Rand bei 96 dpi.</param>
        public Abschnitt(string text, int einzug = 12)
        {
            Text = text.ToUpperInvariant();
            this.einzug = Thema.S(einzug);
            Font = Schrift.Abschnitt;
            Height = Thema.S(34);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Schmal)
            {
                using (var p = new Pen(Thema.Rand))
                    e.Graphics.DrawLine(p, Thema.S(12), Height - Thema.S(14), Width - Thema.S(12), Height - Thema.S(14));
                return;
            }
            if (Trennlinie)
                using (var p = new Pen(Thema.Rand))
                    e.Graphics.DrawLine(p, einzug, Thema.S(4), Width - 1, Thema.S(4));
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(einzug, 0, Width - einzug, Height - Thema.S(6)),
                Thema.Gedaempft, TextFormatFlags.Bottom | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        }
    }
}
