using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Popups;
using ShiftDesk.Oberflaeche.Steuerelemente;

namespace ShiftDesk.Oberflaeche.Tabellen
{
    // Zeichnen: Kopf mit Info-Zeichen, Zellen als Eingabefelder, Schalter, Farben und Zeilenknöpfe
    public partial class Tabelle
    {
        /// <summary>Info-Zeichen im Spaltenkopf, gleich hinter dem Titel (relativ zur Kopfzelle).</summary>
        Rectangle InfoFeld(int spalte, Size zelle)
        {
            var c = g.Columns[spalte];
            var info = Info(c);
            int x = Thema.S(8) + (info != null && info.Farbe.HasValue ? Thema.S(17) : 0);
            int d = Thema.S(14);
            int ix = Math.Max(x, Math.Min(x + TitelBreite(c) + Thema.S(3), zelle.Width - d - Thema.S(4)));
            return new Rectangle(ix, (zelle.Height - d) / 2, d, d);
        }

        /// <summary>Ist die Maus über dem Info-Zeichen eines Spaltenkopfs?</summary>
        bool AufInfo(DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex < 0) return false;
            var info = Info(g.Columns[e.ColumnIndex]);
            if (info == null || info.Erklaerung == null) return false;
            var zelle = g.GetCellDisplayRectangle(e.ColumnIndex, -1, false);
            return Rectangle.Inflate(InfoFeld(e.ColumnIndex, zelle.Size), Thema.S(3), Thema.S(3)).Contains(e.Location);
        }

        /// <summary>Hebt das Info-Zeichen dieser Spalte hervor (null = keins).</summary>
        void InfoHover(DataGridViewColumn neu)
        {
            if (neu == infoHover) return;
            if (infoHover != null && infoHover.DataGridView == g) g.InvalidateCell(infoHover.Index, -1);
            infoHover = neu;
            if (neu != null) g.InvalidateCell(neu.Index, -1);
            g.Cursor = neu != null ? Cursors.Hand : Cursors.Default;
        }

        /// <summary>Klick auf ein Info-Zeichen: Erklärung als Popup.</summary>
        void KopfKlick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !AufInfo(e)) return;
            var spalte = g.Columns[e.ColumnIndex];
            if (Popup.GeradeGeschlossen(spalte)) return;
            var zelle = g.GetCellDisplayRectangle(e.ColumnIndex, -1, false);
            var feld = InfoFeld(e.ColumnIndex, zelle.Size);
            feld.Offset(zelle.Location);
            InfoText.Zeigen(g, feld, spalte, spalte.HeaderText, Info(spalte).Erklaerung);
        }

        /// <summary>Spaltenkopf: Farbquadrat (Statistik), Titel, Info-Zeichen und Linie unten.</summary>
        void KopfMalen(DataGridViewCellPaintingEventArgs e)
        {
            var spalte = g.Columns[e.ColumnIndex];
            var info = Info(spalte);
            var gr = e.Graphics;
            var r = e.CellBounds;
            gr.SmoothingMode = SmoothingMode.None;
            using (var b = new SolidBrush(Thema.Karte))
                gr.FillRectangle(b, r);
            int x = r.X + Thema.S(8);
            if (info != null && info.Farbe.HasValue)
            {
                gr.SmoothingMode = SmoothingMode.AntiAlias;
                int d = Thema.S(10);
                Formen.Fuellen(gr, new RectangleF(x, r.Y + (r.Height - d) / 2f, d, d), Thema.S(3), info.Farbe.Value);
                x += d + Thema.S(7);
            }
            int rechts = r.Right - Thema.S(4);
            if (info != null && info.Erklaerung != null)
            {
                var feld = InfoFeld(e.ColumnIndex, r.Size);
                feld.Offset(r.Location);
                gr.SmoothingMode = SmoothingMode.AntiAlias;
                InfoText.Symbol(gr, feld, spalte == infoHover);
                rechts = feld.Left - Thema.S(2);
            }
            TextRenderer.DrawText(gr, spalte.HeaderText, g.ColumnHeadersDefaultCellStyle.Font,
                Rectangle.FromLTRB(x, r.Y, rechts, r.Bottom), Thema.Gedaempft,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            using (var p = new Pen(Thema.Rand))
                gr.DrawLine(p, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
            e.Handled = true;
        }

        /// <summary>Das Eingabefeld innerhalb einer Zelle.</summary>
        static Rectangle Feld(Rectangle zelle)
        {
            return Rectangle.FromLTRB(zelle.Left + Thema.S(6), zelle.Top + Thema.S(8), zelle.Right - Thema.S(6), zelle.Bottom - Thema.S(8));
        }

        /// <summary>Wo der Text einer Zelle steht (ohne Pfeil und Symbol).</summary>
        static Rectangle TextFeld(SpaltenInfo info, Rectangle zelle, DataGridViewRow zeile)
        {
            if (info.Art == SpaltenArt.Anzeige)
                return Rectangle.FromLTRB(zelle.X + Thema.S(8), zelle.Y, zelle.Right - Thema.S(4), zelle.Bottom);
            var feld = Feld(zelle);
            int rechts = feld.Right - Thema.S(10);
            if (info.Art == SpaltenArt.Auswahl || info.Art == SpaltenArt.Mehrfach) rechts = feld.Right - Formen.PfeilPlatz;
            if (info.Symbol != null && zeile != null && info.Symbol(zeile) != null) rechts -= Thema.S(14 + 6);
            return Rectangle.FromLTRB(feld.X + Thema.S(10), feld.Y, rechts, feld.Bottom);
        }

        /// <summary>Zeilenknöpfe: rechtsbündig nebeneinander.</summary>
        const int KnopfGroesse = 32, KnopfAbstand = 2, KnopfRand = 10;

        /// <summary>Lage von Knopf k (von anzahl) in einer Zelle der Aktionen-Spalte.</summary>
        static RectangleF KnopfFeld(Rectangle zelle, int anzahl, int k)
        {
            float d = Thema.S(KnopfGroesse), abstand = Thema.S(KnopfAbstand);
            float x = zelle.Right - Thema.S(KnopfRand) - anzahl * d - (anzahl - 1) * abstand + k * (d + abstand);
            return new RectangleF(x, zelle.Y + (zelle.Height - d) / 2f, d, d);
        }

        /// <summary>Index des (ausführbaren) Knopfs unter der Maus, sonst -1.</summary>
        int KnopfBei(DataGridViewCellMouseEventArgs e)
        {
            var info = Info(g.Columns[e.ColumnIndex]);
            if (info == null || info.Art != SpaltenArt.Aktionen || e.RowIndex < 0 || e.RowIndex >= g.RowCount) return -1;
            var zelle = new Rectangle(0, 0, g.Columns[e.ColumnIndex].Width, g.Rows[e.RowIndex].Height);
            for (int k = 0; k < info.Knoepfe.Length; k++)
                if (KnopfFeld(zelle, info.Knoepfe.Length, k).Contains(e.Location) && info.Knoepfe[k].Moeglich(e.RowIndex)) return k;
            return -1;
        }

        /// <summary>
        /// Jede Zelle stellt den Zeichenzustand wieder her: sonst bleibt die Kantenglättung an,
        /// die nächste Zelle füllt ihren Hintergrund nur halb deckend und alte Linien scheinen durch.
        /// </summary>
        void Malen(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex < 0 || (e.RowIndex >= 0 && Info(g.Columns[e.ColumnIndex]) == null)) return;
            var zustand = e.Graphics.Save();
            try
            {
                if (e.RowIndex == -1) KopfMalen(e);
                else if (e.RowIndex >= 0) ZelleMalen(e);
            }
            finally { e.Graphics.Restore(zustand); }
        }

        /// <summary>
        /// Einheitlicher Zeilenhintergrund ohne Gitterlinien: hervorgehobene Zeilen als abgerundete Fläche über alle Zellen.
        /// </summary>
        void Hintergrund(Graphics gr, int zeile, Rectangle zelle)
        {
            // Hintergrund ohne Kantenglättung füllen, damit die Zelle randscharf deckt
            gr.SmoothingMode = SmoothingMode.None;
            using (var b = new SolidBrush(Thema.Karte))
                gr.FillRectangle(b, zelle);
            if (zeile < 0 || zeile >= g.RowCount) return;
            bool gewaehlt = (g.Rows.GetRowState(zeile) & DataGridViewElementStates.Selected) != 0;
            if (!gewaehlt && zeile != hoverZeile) return;
            var r = g.GetRowDisplayRectangle(zeile, false);
            var flaeche = new RectangleF(r.X + 1, r.Y + Thema.S(3), r.Width - 2, r.Height - Thema.S(6));
            var zustand = gr.Save();
            gr.SetClip(zelle);
            gr.SmoothingMode = SmoothingMode.AntiAlias;
            Formen.Fuellen(gr, flaeche, Thema.S(9), gewaehlt ? Thema.Auswahl : Thema.Hover);
            gr.Restore(zustand);
        }

        /// <summary>Eine Zelle je nach Spaltenart: Knöpfe, Schalter, reiner Text oder Eingabefeld mit Wert, Farbe oder Symbol.</summary>
        void ZelleMalen(DataGridViewCellPaintingEventArgs e)
        {
            var info = Info(g.Columns[e.ColumnIndex]);
            var zelle = g[e.ColumnIndex, e.RowIndex];
            var gr = e.Graphics;
            Hintergrund(gr, e.RowIndex, e.CellBounds);
            gr.SmoothingMode = SmoothingMode.AntiAlias;
            e.Handled = true;

            switch (info.Art)
            {
                case SpaltenArt.Aktionen:
                {
                    int symbolGroesse = Thema.S(16);
                    for (int k = 0; k < info.Knoepfe.Length; k++)
                    {
                        var knopf = info.Knoepfe[k];
                        var kf = KnopfFeld(e.CellBounds, info.Knoepfe.Length, k);
                        bool moeglich = knopf.Moeglich(e.RowIndex), darueber = moeglich && zelle == hoverZelle && k == hoverKnopf;
                        if (darueber)
                            Formen.Fuellen(gr, kf, Thema.S(7), knopf.Gefahr ? Thema.Toenung(Thema.Gefahr) : Thema.Rand);
                        var farbe = !moeglich ? Color.FromArgb(70, Thema.Gedaempft) : darueber ? (knopf.Gefahr ? Thema.Gefahr : Thema.Text) : Thema.Gedaempft;
                        Lucide.Zeichne(gr, knopf.Icon, Formen.Mitte(kf, symbolGroesse), farbe);
                    }
                    return;
                }
                case SpaltenArt.Schalter:
                    Formen.Schalter(gr, new RectangleF(e.CellBounds.X, e.CellBounds.Y, Thema.S(36) + 2 * Thema.S(8), e.CellBounds.Height),
                        e.Value is bool && (bool)e.Value);
                    return;
                case SpaltenArt.Anzeige:
                {
                    string text = WertText(zelle);
                    TextRenderer.DrawText(gr, text, g.DefaultCellStyle.Font, TextFeld(info, e.CellBounds, null),
                        text == "–" ? Thema.Gedaempft : Thema.Text,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    return;
                }
            }
            if (zelle == editZelle) return;   // dort liegt gerade das Eingabefeld

            string fest = info.FesterText != null ? info.FesterText(g.Rows[e.RowIndex]) : null;
            if (fest != null)
            {
                // ergibt sich von selbst: grauer Text ohne Eingabefeld
                TextRenderer.DrawText(gr, fest, g.DefaultCellStyle.Font, TextFeld(info, e.CellBounds, null), Thema.Gedaempft,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                return;
            }
            var feld = Feld(e.CellBounds);
            bool aktiv = zelle == offeneZelle;
            var r = new RectangleF(feld.X + 0.5f, feld.Y + 0.5f, feld.Width - 1, feld.Height - 1);
            Formen.Eingabe(gr, r, aktiv, zelle == hoverZelle);
            if (info.Art == SpaltenArt.Auswahl || info.Art == SpaltenArt.Mehrfach || info.Art == SpaltenArt.Farbe)
                Formen.Aufklapppfeil(gr, r, aktiv);

            if (info.Art == SpaltenArt.Farbe)
            {
                var farbe = Farbe(zelle);
                var chip = new RectangleF(feld.X + Thema.S(6), feld.Y + Thema.S(5),
                    Math.Max(Thema.S(10), Math.Min(Thema.S(56), feld.Width - Formen.PfeilPlatz - Thema.S(8))), feld.Height - Thema.S(10));
                Formen.Fuellen(gr, chip, Thema.S(4), farbe);
                Formen.Umrandung(gr, chip, Thema.S(4), Color.FromArgb(40, 0, 0, 0));
                return;
            }
            var zeile = info.Symbol != null ? g.Rows[e.RowIndex] : null;
            var textFeld = TextFeld(info, e.CellBounds, zeile);
            string symbol = zeile != null ? info.Symbol(zeile) : null;
            if (symbol != null)
            {
                int icon = Thema.S(14);
                Lucide.Zeichne(gr, symbol, new RectangleF(textFeld.Right + Thema.S(6), feld.Y + (feld.Height - icon) / 2f, icon, icon), Thema.Akzent);
            }
            string wert = WertText(zelle);
            string anzeige = info.Anzeige != null ? info.Anzeige(wert) : wert;
            bool platzhalter = info.Anzeige != null && anzeige != wert;
            TextRenderer.DrawText(gr, anzeige, g.DefaultCellStyle.Font, textFeld, platzhalter ? Thema.Gedaempft : Thema.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
