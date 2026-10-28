using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Popups;
using ShiftDesk.Oberflaeche.Steuerelemente;

namespace ShiftDesk.Oberflaeche.Seiten
{
    /// <summary>
    /// Titelzeile einer Seite oder eines Bereichs: Titel, evtl. Info-Knopf mit Erklärung, rechts die Knöpfe.
    /// Wird es eng, zeigen zuerst die Nebenknöpfe und dann alle nur noch ihr Symbol (Text als Kurzinfo).
    /// </summary>
    public class Kopfzeile : Panel
    {
        /// <param name="titel">Titel links.</param>
        /// <param name="info">Erklärung hinter dem Info-Knopf neben dem Titel (null = kein Info-Knopf).</param>
        /// <param name="schrift">Schrift des Titels.</param>
        /// <param name="hoehe">Höhe der ganzen Zeile.</param>
        /// <param name="abstandUnten">Abstand unter Titel und Knöpfen.</param>
        /// <param name="knoepfe">Knöpfe rechts, von links nach rechts.</param>
        public Kopfzeile(string titel, string info, Font schrift, int hoehe, int abstandUnten, Knopf[] knoepfe)
        {
            Dock = DockStyle.Top;
            Height = hoehe;
            int zeile = hoehe - abstandUnten;
            var lbl = new Label { Text = titel, Font = schrift, AutoSize = true, UseMnemonic = false };
            lbl.Location = new Point(-Thema.S(schrift.Size > 15 ? 3 : 1), (zeile - lbl.PreferredHeight) / 2);
            var leiste = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(0, Math.Max(0, (zeile - Thema.KnopfHoehe) / 2), 0, 0) };
            foreach (var k in knoepfe) leiste.Controls.Add(k);
            Controls.Add(lbl);
            Controls.Add(leiste);
            Control rechtsVomTitel = lbl;
            if (!string.IsNullOrEmpty(info))
            {
                var knopf = new InfoKnopf(titel, info, 22);
                knopf.Location = new Point(lbl.Left + lbl.PreferredWidth + Thema.S(4), lbl.Top + (lbl.PreferredHeight - knopf.Height) / 2 + Thema.S(2));
                Controls.Add(knopf);
                rechtsVomTitel = knopf;
            }
            Resize += (s, e) =>
            {
                int frei = Width - rechtsVomTitel.Right - Thema.S(16);
                Func<bool, bool, int> breite = (neben, alle) => knoepfe.Sum(k => k.Margin.Horizontal + ((k.Primaer ? alle : neben) ? k.Height : k.VolleBreite));
                bool nebenKompakt = breite(false, false) > frei;
                bool alleKompakt = nebenKompakt && breite(true, false) > frei;
                foreach (var k in knoepfe) k.Kompakt = k.Primaer ? alleKompakt : nebenKompakt;
            };
            Farbwechsel.Bei(() =>
            {
                BackColor = leiste.BackColor = lbl.BackColor = Thema.Hintergrund;
                lbl.ForeColor = Thema.Text;
            });
        }
    }
}
