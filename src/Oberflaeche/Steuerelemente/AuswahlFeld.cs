using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Popups;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>Aufklappfeld im Eingabefeld-Stil mit fester Liste (eigene <see cref="AuswahlListe"/> statt Windows-Combobox).</summary>
    public class AuswahlFeld : ThemaControl
    {
        readonly string[] optionen;
        string wert;
        bool offen;

        /// <summary>Der Benutzer hat einen Eintrag gewählt (nicht beim Setzen von <see cref="Wert"/> oder <see cref="Index"/>).</summary>
        public event EventHandler WertGeaendert;

        /// <param name="optionen">Die Einträge der Liste; der erste ist anfangs gewählt.</param>
        public AuswahlFeld(string[] optionen)
        {
            this.optionen = optionen;
            wert = optionen.Length > 0 ? optionen[0] : "";
            Font = Schrift.Eingabe;
            Height = Thema.FeldHoehe;
            Width = Thema.S(190);
            Cursor = Cursors.Hand;
        }

        /// <summary>Der gewählte Eintrag.</summary>
        public string Wert
        {
            get { return wert; }
            set { wert = value; Invalidate(); }
        }

        /// <summary>Position des gewählten Eintrags in der Liste (0, wenn er nicht darin steht).</summary>
        public int Index
        {
            get { return Math.Max(0, Array.IndexOf(optionen, wert)); }
            set { if (value >= 0 && value < optionen.Length) Wert = optionen[value]; }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || Popup.GeradeGeschlossen(this)) { base.OnMouseDown(e); return; }
            var liste = new AuswahlListe(optionen, new[] { wert }, false, Width);
            var popup = new Popup(liste, this);
            liste.Gewaehlt += w =>
            {
                popup.Close();
                Wert = w;
                var h = WertGeaendert;
                if (h != null) h(this, EventArgs.Empty);
            };
            popup.Closed += (s, a) => { offen = false; Invalidate(); };
            offen = true;
            Invalidate();
            popup.ZeigenBei(this, new Rectangle(0, 0, Width, Height));
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Formen.Eingabe(g, r, offen, Hover);
            Formen.Aufklapppfeil(g, r, offen);
            TextRenderer.DrawText(g, wert, Font, new Rectangle(Thema.S(11), 0, Width - Thema.S(11) - Formen.PfeilPlatz, Height), Thema.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
