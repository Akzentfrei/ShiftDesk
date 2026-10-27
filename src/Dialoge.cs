using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShiftDesk
{
    public enum MeldungsArt { Info, Erfolg, Warnung, Fehler }

    // Mitteilungen mit nur einem Knopf «OK» (statt der Windows-MessageBox)
    public static class Meldung
    {
        public static void Info(IWin32Window besitzer, string titel, string text = null) { Zeigen(besitzer, titel, text, MeldungsArt.Info); }
        public static void Erfolg(IWin32Window besitzer, string titel, string text = null) { Zeigen(besitzer, titel, text, MeldungsArt.Erfolg); }
        public static void Warnung(IWin32Window besitzer, string titel, string text = null) { Zeigen(besitzer, titel, text, MeldungsArt.Warnung); }
        public static void Fehler(IWin32Window besitzer, string titel, string text = null) { Zeigen(besitzer, titel, text, MeldungsArt.Fehler); }

        public static void Zeigen(IWin32Window besitzer, string titel, string text, MeldungsArt art)
        {
            string symbol = art == MeldungsArt.Erfolg ? "circle-check" : art == MeldungsArt.Warnung ? "triangle-alert" : art == MeldungsArt.Fehler ? "circle-x" : "info";
            Color farbe = art == MeldungsArt.Erfolg ? Theme.Erfolg : art == MeldungsArt.Warnung ? Theme.Warnung : art == MeldungsArt.Fehler ? Theme.Gefahr : Theme.Akzent;
            using (var d = new GefahrDialog(besitzer != null, titel, text, "OK", null, symbol, farbe, false))
                d.ShowDialog(besitzer);
        }
    }

    // Rückfrage vor allem, was Eingaben löscht oder überschreibt: der rote Bestätigungsknopf steht links,
    // «Abbrechen» rechts und hat den Fokus, damit ein schnelles Enter nichts kaputt macht.
    // Derselbe Dialog zeigt auch Vorschläge (blau) und Mitteilungen (nur «OK», siehe Meldung).
    public class GefahrDialog : Form
    {
        public static bool Fragen(IWin32Window besitzer, string titel, string text, string knopf,
            string symbol = "triangle-alert", string abbrechen = "Abbrechen")
        {
            using (var d = new GefahrDialog(besitzer != null, titel, text, knopf, abbrechen, symbol, Theme.Gefahr, true))
                return d.ShowDialog(besitzer) == DialogResult.OK;
        }

        // Gleiche Rückfrage ohne Gefahr: blauer Hauptknopf links, der den Fokus hat (Enter = Ja)
        public static bool Vorschlagen(IWin32Window besitzer, string titel, string text, string knopf,
            string symbol = "circle-check", string abbrechen = "Nicht jetzt")
        {
            using (var d = new GefahrDialog(besitzer != null, titel, text, knopf, abbrechen, symbol, Theme.Akzent, false))
                return d.ShowDialog(besitzer) == DialogResult.OK;
        }

        readonly string symbol;
        readonly Color farbe;
        readonly int fussOben;

        // abbrechen = null: nur ein Knopf (Mitteilung)
        internal GefahrDialog(bool mitBesitzer, string titel, string text, string knopf, string abbrechen, string symbol, Color farbe, bool gefahr)
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
            BackColor = Theme.Karte;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            int rand = Theme.S(24), breite = Theme.S(460);
            int textX = rand + Theme.S(40 + 16), textBreite = breite - textX - rand;

            var lblTitel = TextLabel(titel, Schrift.Titel, Theme.Text, textX, rand, textBreite);
            int unten = lblTitel.Bottom;
            Controls.Add(lblTitel);
            if (!string.IsNullOrEmpty(text))
            {
                var lblText = TextLabel(text, Font, Theme.Gedaempft, textX, unten + Theme.S(6), textBreite);
                unten = lblText.Bottom;
                Controls.Add(lblText);
            }
            else if (lblTitel.Height < Theme.S(40))
            {
                lblTitel.Top = rand + (Theme.S(40) - lblTitel.Height) / 2;   // nur Titel: mittig neben dem Symbol
                unten = lblTitel.Bottom;
            }
            unten = Math.Max(unten, rand + Theme.S(40));

            fussOben = unten + rand;
            int knopfY = fussOben + Theme.S(16);
            var btnOk = new ModernButton(knopf, null) { Gefahr = gefahr, Primaer = !gefahr, Fokusrahmen = true, Untergrundfarbe = Theme.Hintergrund, TabIndex = 0 };
            btnOk.Click += (s, e) => DialogResult = DialogResult.OK;
            if (abbrechen == null)
            {
                btnOk.Width = Math.Max(btnOk.Width, Theme.S(88));
                btnOk.Location = new Point(breite - rand - btnOk.Width, knopfY);
                Controls.Add(btnOk);
                ActiveControl = btnOk;
            }
            else
            {
                var btnAbbrechen = new ModernButton(abbrechen, null) { Fokusrahmen = true, Untergrundfarbe = Theme.Hintergrund, TabIndex = 1 };
                btnAbbrechen.Click += (s, e) => DialogResult = DialogResult.Cancel;
                btnAbbrechen.Location = new Point(breite - rand - btnAbbrechen.Width, knopfY);
                btnOk.Location = new Point(btnAbbrechen.Left - Theme.S(8) - btnOk.Width, knopfY);
                Controls.Add(btnOk);
                Controls.Add(btnAbbrechen);
                ActiveControl = gefahr ? btnAbbrechen : btnOk;
            }
            ClientSize = new Size(breite, knopfY + btnOk.Height + Theme.S(16));
        }

        Label TextLabel(string text, Font font, Color farbe, int x, int y, int breite)
        {
            var l = new Label
            {
                Text = text,
                Font = font,
                ForeColor = farbe,
                BackColor = Theme.Karte,
                AutoSize = false,
                UseMnemonic = false,
                Location = new Point(x, y),
            };
            // so messen, wie das Label selbst umbricht (lange Dateipfade auch mitten im Wort)
            l.Size = new Size(breite, l.GetPreferredSize(new Size(breite, 0)).Height + Theme.S(2));
            return l;
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
            // Symbol im farbigen Kreis (rot = Gefahr, blau = Vorschlag/Info, gelb = Warnung, grün = Erfolg)
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int d = Theme.S(40), icon = Theme.S(20), x = Theme.S(24), y = Theme.S(24);
            using (var b = new SolidBrush(Theme.Toenung(farbe)))
                g.FillEllipse(b, x, y, d, d);
            Lucide.Zeichne(g, symbol, new RectangleF(x + (d - icon) / 2f, y + (d - icon) / 2f, icon, icon), farbe);
        }
    }
}
