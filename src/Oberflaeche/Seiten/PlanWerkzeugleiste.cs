using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Popups;
using ShiftDesk.Oberflaeche.Steuerelemente;
using ShiftDesk.Planung;

namespace ShiftDesk.Oberflaeche.Seiten
{
    /// <summary>
    /// Leiste über der Vorschau: links der Status (ein Klick zeigt die Hinweise), rechts die Gruppen
    /// Ansicht (Zoom, ganze Seite) | Seite (blättern) | Ausgabe (Drucken, PDF, Excel).
    /// </summary>
    /// <remarks>
    /// Wird es eng, verschwindet zuerst die Zoom-Anzeige, dann zeigen die Ausgabe-Knöpfe nur ihr Symbol.
    /// Die Leiste ordnet ihre Knöpfe selbst an: ein FlowLayoutPanel bringt die Reihenfolge durcheinander,
    /// wenn Steuerelemente erst später sichtbar werden.
    /// </remarks>
    public class PlanWerkzeugleiste : Karte
    {
        readonly StatusChip status;
        readonly Label lblZoom, lblSeite;
        readonly Knopf btnEinpassen, btnZurueck, btnVor;
        readonly Knopf[] ausgabe;
        readonly Trenner trennerAnsicht, trennerSeite;
        readonly Panel werkzeuge;
        readonly Control[] reihe;   // Reihenfolge rechts in der Leiste
        Plan plan;
        float zoom = 1f;

        /// <param name="einpassen">«Ganze Seite zeigen».</param>
        /// <param name="blaettern">Vorherige (-1) oder nächste (1) Seite.</param>
        /// <param name="drucken">«Drucken».</param>
        /// <param name="alsPdf">«Als PDF».</param>
        /// <param name="alsExcel">«Excel».</param>
        public PlanWerkzeugleiste(Action einpassen, Action<int> blaettern, Action drucken, Action alsPdf, Action alsExcel)
        {
            Dock = DockStyle.Top;
            Height = Thema.KnopfHoehe + Thema.S(20);
            Padding = new Padding(Thema.S(14), Thema.S(10), Thema.S(10), Thema.S(10));

            status = new StatusChip();
            status.MouseDown += (s, e) => HinweiseZeigen(e);
            lblZoom = new Label { AutoSize = false, Width = Thema.S(84), Height = Thema.KnopfHoehe, TextAlign = ContentAlignment.MiddleCenter, Font = Schrift.Fett, Margin = new Padding(Thema.S(4), 0, 0, 0) };
            btnEinpassen = Knopf.Mit("", "maximize", einpassen);
            btnEinpassen.Tipp = "Ganze Seite zeigen";
            lblSeite = new Label { AutoSize = false, Width = Thema.S(110), Height = Thema.KnopfHoehe, TextAlign = ContentAlignment.MiddleCenter, Font = Schrift.Fett, Margin = new Padding(Thema.S(4), 0, 0, 0) };
            btnZurueck = Knopf.Mit("", "chevron-left", () => blaettern(-1));
            btnZurueck.Tipp = "Vorherige Seite";
            btnVor = Knopf.Mit("", "chevron-right", () => blaettern(1));
            btnVor.Tipp = "Nächste Seite";
            ausgabe = new[] { Knopf.Mit("Drucken", "printer", drucken), Knopf.Mit("Als PDF", "file-down", alsPdf), Knopf.Mit("Excel", "file-spreadsheet", alsExcel) };
            trennerAnsicht = new Trenner();
            trennerSeite = new Trenner();
            werkzeuge = new Panel { Dock = DockStyle.Right };
            reihe = new Control[] { lblZoom, btnEinpassen, trennerAnsicht, btnZurueck, lblSeite, btnVor, trennerSeite }.Concat(ausgabe).ToArray();
            werkzeuge.Controls.AddRange(reihe);

            status.Location = new Point(Thema.S(14), (Height - status.Height) / 2);
            Controls.Add(status);
            Controls.Add(werkzeuge);
            Resize += (s, e) => Anordnen();
            Farbwechsel.Bei(() =>
            {
                werkzeuge.BackColor = lblSeite.BackColor = lblZoom.BackColor = Thema.Karte;
                lblSeite.ForeColor = lblZoom.ForeColor = Thema.Text;
                Invalidate(true);
            });
        }

        /// <summary>Zeigt Status und Seitenzahl für diesen Plan (null = noch keiner) und ordnet die Leiste neu.</summary>
        public void Anzeigen(Plan plan, int seite, float zoom)
        {
            this.plan = plan;
            this.zoom = zoom;
            if (plan == null)
                status.Setzen("", "circle-check", Thema.Erfolg);
            else if (plan.Warnungen.Count > 0)
                status.Setzen(plan.Warnungen.Count == 1 ? "1 Hinweis" : plan.Warnungen.Count + " Hinweise", "triangle-alert", Thema.Warnung, true);
            else
                status.Setzen("Alle Posten besetzt", "circle-check", Thema.Erfolg);
            lblSeite.Text = plan == null ? "" : "Seite " + (seite + 1) + " / " + plan.Seiten;
            btnZurueck.Enabled = seite > 0;
            btnVor.Enabled = plan != null && seite < plan.Seiten - 1;
            Anordnen();
        }

        /// <summary>Neuer Zoom der Vorschau.</summary>
        public void ZoomAnzeigen(float zoom)
        {
            this.zoom = zoom;
            Anordnen();
        }

        /// <summary>Hinweise als Popup unter dem Status.</summary>
        void HinweiseZeigen(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || plan == null || plan.Warnungen.Count == 0 || Popup.GeradeGeschlossen(status)) return;
            var popup = HinweisText.Zeigen(status, new Rectangle(0, 0, status.Width, status.Height), status, status.Text, plan.Warnungen);
            status.Offen = true;
            popup.Closed += (o, a) => status.Offen = false;
        }

        /// <summary>Was zu sehen ist, hängt vom Plan und vom Platz ab: ohne Plan nur die Ausgabe-Knöpfe (grau).</summary>
        void Anordnen()
        {
            bool sichtbar = plan != null;
            bool blaettern = sichtbar && plan.Seiten > 1;
            foreach (var k in ausgabe) k.Enabled = sichtbar;
            Func<Control, int> breite = c => c.Width + c.Margin.Horizontal;
            int fest = Thema.S(14 + 10 + 16) + status.Width
                + (sichtbar ? breite(btnEinpassen) + breite(trennerAnsicht) + breite(trennerSeite) + breite(lblSeite) : 0)
                + (blaettern ? breite(btnZurueck) + breite(btnVor) : 0);
            int ausgabeVoll = ausgabe.Sum(k => k.VolleBreite + k.Margin.Horizontal);
            bool mitZoom = sichtbar && Width >= fest + breite(lblZoom) + ausgabeVoll;
            bool kompakt = Width < fest + (mitZoom ? breite(lblZoom) : 0) + ausgabeVoll;
            foreach (var k in ausgabe) k.Kompakt = kompakt;
            lblZoom.Text = zoom <= 1.001f ? "Ganze Seite" : Math.Round(zoom * 100) + " %";
            btnEinpassen.Enabled = zoom > 1.001f;

            var zeigen = new Dictionary<Control, bool>
            {
                { lblZoom, mitZoom }, { btnEinpassen, sichtbar }, { trennerAnsicht, sichtbar },
                { btnZurueck, blaettern }, { lblSeite, sichtbar }, { btnVor, blaettern }, { trennerSeite, sichtbar },
            };
            int x = 0;
            foreach (var c in reihe)
            {
                bool an;
                if (!zeigen.TryGetValue(c, out an)) an = true;
                c.Visible = an;
                if (!an) continue;
                x += c.Margin.Left;
                c.Location = new Point(x, (werkzeuge.Height - c.Height) / 2);
                x += c.Width + c.Margin.Right;
            }
            werkzeuge.Width = x;
        }
    }
}
