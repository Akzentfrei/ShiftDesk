using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShiftDesk
{
    // Rückfrage vor allem, was Eingaben löscht oder überschreibt: der rote Bestätigungsknopf steht links,
    // «Abbrechen» rechts und hat den Fokus, damit ein schnelles Enter nichts kaputt macht.
    public class GefahrDialog : Form
    {
        public static bool Fragen(IWin32Window besitzer, string titel, string text, string knopf,
            string symbol = "triangle-alert", string abbrechen = "Abbrechen")
        {
            using (var d = new GefahrDialog(besitzer != null, titel, text, knopf, symbol, abbrechen, true))
                return d.ShowDialog(besitzer) == DialogResult.OK;
        }

        // Gleiche Rückfrage ohne Gefahr: blauer Hauptknopf links, der den Fokus hat (Enter = Ja)
        public static bool Vorschlagen(IWin32Window besitzer, string titel, string text, string knopf,
            string symbol = "circle-check", string abbrechen = "Nicht jetzt")
        {
            using (var d = new GefahrDialog(besitzer != null, titel, text, knopf, symbol, abbrechen, false))
                return d.ShowDialog(besitzer) == DialogResult.OK;
        }

        readonly string symbol;
        readonly bool gefahr;
        readonly int fussOben;

        GefahrDialog(bool mitBesitzer, string titel, string text, string knopf, string symbol, string abbrechen, bool gefahr)
        {
            this.symbol = symbol;
            this.gefahr = gefahr;
            Text = "ShiftDesk";
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = !mitBesitzer;
            StartPosition = mitBesitzer ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;
            BackColor = Theme.Karte;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            int rand = Theme.S(24), breite = Theme.S(460);
            int textX = rand + Theme.S(40 + 16), textBreite = breite - textX - rand;

            var lblTitel = TextLabel(titel, new Font("Segoe UI Semibold", 12f), Theme.Text, textX, rand, textBreite);
            int unten = lblTitel.Bottom;
            Controls.Add(lblTitel);
            if (!string.IsNullOrEmpty(text))
            {
                var lblText = TextLabel(text, Font, Theme.Gedaempft, textX, unten + Theme.S(6), textBreite);
                unten = lblText.Bottom;
                Controls.Add(lblText);
            }
            unten = Math.Max(unten, rand + Theme.S(40));

            var btnOk = new ModernButton(knopf, null) { Gefahr = gefahr, Primaer = !gefahr, Fokusrahmen = true, Untergrundfarbe = Theme.Hintergrund, TabIndex = 0 };
            var btnAbbrechen = new ModernButton(abbrechen, null) { Fokusrahmen = true, Untergrundfarbe = Theme.Hintergrund, TabIndex = 1 };
            btnOk.Click += (s, e) => DialogResult = DialogResult.OK;
            btnAbbrechen.Click += (s, e) => DialogResult = DialogResult.Cancel;

            fussOben = unten + rand;
            int knopfY = fussOben + Theme.S(16);
            btnAbbrechen.Location = new Point(breite - rand - btnAbbrechen.Width, knopfY);
            btnOk.Location = new Point(btnAbbrechen.Left - Theme.S(8) - btnOk.Width, knopfY);
            Controls.Add(btnOk);
            Controls.Add(btnAbbrechen);
            ClientSize = new Size(breite, knopfY + btnAbbrechen.Height + Theme.S(16));
            ActiveControl = gefahr ? btnAbbrechen : btnOk;
        }

        Label TextLabel(string text, Font font, Color farbe, int x, int y, int breite)
        {
            int h = TextRenderer.MeasureText(text, font, new Size(breite, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            return new Label
            {
                Text = text,
                Font = font,
                ForeColor = farbe,
                BackColor = Theme.Karte,
                AutoSize = false,
                UseMnemonic = false,
                Location = new Point(x, y),
                Size = new Size(breite, h + Theme.S(2)),
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.Titelleiste(this);
        }

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
            using (var b = new SolidBrush(Theme.Hintergrund))
                g.FillRectangle(b, 0, fussOben, ClientSize.Width, ClientSize.Height - fussOben);
            using (var p = new Pen(Theme.Rand))
                g.DrawLine(p, 0, fussOben, ClientSize.Width, fussOben);
            // Symbol im roten (Gefahr) oder blauen Kreis
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int d = Theme.S(40), icon = Theme.S(20), x = Theme.S(24), y = Theme.S(24);
            Color farbe = gefahr ? Theme.Gefahr : Theme.Akzent;
            using (var b = new SolidBrush(Color.FromArgb(Theme.Dunkel ? 48 : 26, farbe)))
                g.FillEllipse(b, x, y, d, d);
            Lucide.Zeichne(g, symbol, new RectangleF(x + (d - icon) / 2f, y + (d - icon) / 2f, icon, icon), farbe);
        }
    }
}
