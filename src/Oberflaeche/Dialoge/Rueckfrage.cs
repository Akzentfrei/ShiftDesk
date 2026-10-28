using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Steuerelemente;

namespace ShiftDesk.Oberflaeche.Dialoge
{
    /// <summary>
    /// Dialog im Design von ShiftDesk: Symbol im farbigen Kreis, Titel, Text und unten die Knöpfe.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Fragen"/>: Rückfrage vor allem, was Eingaben löscht oder überschreibt. Der rote
    /// Bestätigungsknopf steht links, «Abbrechen» rechts und hat den Fokus, damit ein schnelles Enter nichts
    /// kaputt macht.</para>
    /// <para><see cref="Vorschlagen"/>: gleiche Rückfrage ohne Gefahr, mit blauem Hauptknopf, der den Fokus hat.</para>
    /// <para>Mitteilungen mit nur «OK» zeigt <see cref="Meldung"/> mit demselben Dialog.</para>
    /// </remarks>
    public class Rueckfrage : Form
    {
        /// <summary>Rückfrage vor etwas Gefährlichem (rot); Enter = Abbrechen.</summary>
        /// <param name="besitzer">Fenster, über dem sie erscheint; null = mitten auf dem Bildschirm.</param>
        /// <param name="titel">Die Frage, fett.</param>
        /// <param name="text">Erklärung darunter, null = keine.</param>
        /// <param name="knopf">Text des roten Knopfs, z. B. «Löschen».</param>
        /// <param name="symbol">Lucide-Symbol im Kreis.</param>
        /// <param name="abbrechen">Text des anderen Knopfs.</param>
        /// <returns>true, wenn der rote Knopf gewählt wurde.</returns>
        public static bool Fragen(IWin32Window besitzer, string titel, string text, string knopf,
            string symbol = "triangle-alert", string abbrechen = "Abbrechen")
        {
            using (var d = new Rueckfrage(besitzer != null, titel, text, knopf, abbrechen, symbol, Thema.Gefahr, true))
                return d.ShowDialog(besitzer) == DialogResult.OK;
        }

        /// <summary>Vorschlag (blau); Enter = Ja.</summary>
        /// <param name="besitzer">Fenster, über dem er erscheint; null = mitten auf dem Bildschirm.</param>
        /// <param name="titel">Der Vorschlag, fett.</param>
        /// <param name="text">Erklärung darunter, null = keine.</param>
        /// <param name="knopf">Text des blauen Knopfs, z. B. «Öffnen».</param>
        /// <param name="symbol">Lucide-Symbol im Kreis.</param>
        /// <param name="abbrechen">Text des anderen Knopfs.</param>
        /// <returns>true, wenn der blaue Knopf gewählt wurde.</returns>
        public static bool Vorschlagen(IWin32Window besitzer, string titel, string text, string knopf,
            string symbol = "circle-check", string abbrechen = "Nicht jetzt")
        {
            using (var d = new Rueckfrage(besitzer != null, titel, text, knopf, abbrechen, symbol, Thema.Akzent, false))
                return d.ShowDialog(besitzer) == DialogResult.OK;
        }

        readonly string symbol;
        readonly Color farbe;
        readonly int fussOben;

        /// <param name="mitBesitzer">Über einem Fenster (sonst mitten auf dem Bildschirm, mit Taskleisten-Eintrag).</param>
        /// <param name="titel">Erste Zeile, fett.</param>
        /// <param name="text">Erklärung darunter, null = keine.</param>
        /// <param name="knopf">Text des Hauptknopfs.</param>
        /// <param name="symbol">Lucide-Symbol im Kreis.</param>
        /// <param name="abbrechen">Text des zweiten Knopfs; null = nur ein Knopf (Mitteilung).</param>
        /// <param name="farbe">Farbe von Symbol und Kreis.</param>
        /// <param name="gefahr">Roter Hauptknopf, und «Abbrechen» hat den Fokus.</param>
        internal Rueckfrage(bool mitBesitzer, string titel, string text, string knopf, string abbrechen, string symbol, Color farbe, bool gefahr)
        {
            this.symbol = symbol;
            this.farbe = farbe;
            Text = "ShiftDesk";
            Font = Schrift.Text;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = !mitBesitzer;
            StartPosition = mitBesitzer ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;
            BackColor = Thema.Karte;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            int rand = Thema.S(24), breite = Thema.S(460);
            int textX = rand + Thema.S(40 + 16), textBreite = breite - textX - rand;

            var lblTitel = TextLabel(titel, Schrift.Titel, Thema.Text, textX, rand, textBreite);
            int unten = lblTitel.Bottom;
            Controls.Add(lblTitel);
            if (!string.IsNullOrEmpty(text))
            {
                var lblText = TextLabel(text, Font, Thema.Gedaempft, textX, unten + Thema.S(6), textBreite);
                unten = lblText.Bottom;
                Controls.Add(lblText);
            }
            else if (lblTitel.Height < Thema.S(40))
            {
                lblTitel.Top = rand + (Thema.S(40) - lblTitel.Height) / 2;   // nur Titel: mittig neben dem Symbol
                unten = lblTitel.Bottom;
            }
            unten = Math.Max(unten, rand + Thema.S(40));

            fussOben = unten + rand;
            int knopfY = fussOben + Thema.S(16);
            var btnOk = new Knopf(knopf, null) { Gefahr = gefahr, Primaer = !gefahr, Fokusrahmen = true, Untergrundfarbe = Thema.Hintergrund, TabIndex = 0 };
            btnOk.Click += (s, e) => DialogResult = DialogResult.OK;
            if (abbrechen == null)
            {
                btnOk.Width = Math.Max(btnOk.Width, Thema.S(88));
                btnOk.Location = new Point(breite - rand - btnOk.Width, knopfY);
                Controls.Add(btnOk);
                ActiveControl = btnOk;
            }
            else
            {
                var btnAbbrechen = new Knopf(abbrechen, null) { Fokusrahmen = true, Untergrundfarbe = Thema.Hintergrund, TabIndex = 1 };
                btnAbbrechen.Click += (s, e) => DialogResult = DialogResult.Cancel;
                btnAbbrechen.Location = new Point(breite - rand - btnAbbrechen.Width, knopfY);
                btnOk.Location = new Point(btnAbbrechen.Left - Thema.S(8) - btnOk.Width, knopfY);
                Controls.Add(btnOk);
                Controls.Add(btnAbbrechen);
                ActiveControl = gefahr ? btnAbbrechen : btnOk;
            }
            ClientSize = new Size(breite, knopfY + btnOk.Height + Thema.S(16));
        }

        /// <summary>Umbrochener Text in fester Breite, so hoch wie nötig.</summary>
        Label TextLabel(string text, Font font, Color farbe, int x, int y, int breite)
        {
            var l = new Label
            {
                Text = text,
                Font = font,
                ForeColor = farbe,
                BackColor = Thema.Karte,
                AutoSize = false,
                UseMnemonic = false,
                Location = new Point(x, y),
            };
            // so messen, wie das Label selbst umbricht (lange Dateipfade auch mitten im Wort)
            l.Size = new Size(breite, l.GetPreferredSize(new Size(breite, 0)).Height + Thema.S(2));
            return l;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Thema.Titelleiste(this);
        }

        /// <summary>Esc = Abbrechen.</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            // Fusszeile mit den Knöpfen
            using (var b = new SolidBrush(Thema.Hintergrund))
                g.FillRectangle(b, 0, fussOben, ClientSize.Width, ClientSize.Height - fussOben);
            using (var p = new Pen(Thema.Rand))
                g.DrawLine(p, 0, fussOben, ClientSize.Width, fussOben);
            // Symbol im farbigen Kreis (rot = Gefahr, blau = Vorschlag/Info, gelb = Warnung, grün = Erfolg)
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int d = Thema.S(40), icon = Thema.S(20), x = Thema.S(24), y = Thema.S(24);
            using (var b = new SolidBrush(Thema.Toenung(farbe)))
                g.FillEllipse(b, x, y, d, d);
            Lucide.Zeichne(g, symbol, new RectangleF(x + (d - icon) / 2f, y + (d - icon) / 2f, icon, icon), farbe);
        }
    }
}
