using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace ShiftDesk
{
    public enum SpaltenArt { Text, Zahl, Auswahl, Mehrfach, Farbe, Schalter }

    // Hängt an einer Tabellenspalte (Column.Tag) und sagt, wie sie bearbeitet wird
    public class SpaltenInfo
    {
        public SpaltenArt Art;
        public Func<IEnumerable<string>> Optionen;
        public Func<string, string> Anzeige;   // Text für einen Wert (z. B. "" -> "Keine")

        public SpaltenInfo(SpaltenArt art, Func<IEnumerable<string>> optionen = null, Func<string, string> anzeige = null)
        {
            Art = art;
            Optionen = optionen;
            Anzeige = anzeige;
        }
    }

    // Macht alle bearbeitbaren Zellen als Eingabefelder erkennbar und bearbeitet sie mit eigenen Steuerelementen.
    // Mehrfach-Spalten speichern ihre Werte als List<string> im Zellen-Tag, Farb-Spalten eine Color im Tag.
    public class TabellenEditor
    {
        readonly DataGridView g;
        Eingabefeld editor;
        DataGridViewCell editZelle, hoverZelle, offeneZelle;

        public event Action Geaendert;
        public event Action<DataGridViewCell, string, string> Umbenannt;   // Zelle, alter Text, neuer Text

        public DataGridView Grid
        {
            get { return g; }
        }

        public TabellenEditor(DataGridView grid)
        {
            g = grid;
            g.ReadOnly = true;
            g.EditMode = DataGridViewEditMode.EditProgrammatically;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = true;
            g.RowTemplate.Height = Theme.S(50);
            g.CellBorderStyle = DataGridViewCellBorderStyle.None;
            g.CellPainting += Malen;
            g.SelectionChanged += (s, e) => g.Invalidate();
            g.CellMouseEnter += (s, e) => Hover(e.RowIndex >= 0 && e.ColumnIndex >= 0 ? g[e.ColumnIndex, e.RowIndex] : null);
            g.MouseLeave += (s, e) => Hover(null);
            g.CellMouseClick += Klick;
            g.KeyDown += (s, e) =>
            {
                if (g.CurrentCell == null || editor != null) return;
                if (e.KeyCode == Keys.F2 || e.KeyCode == Keys.Space)
                {
                    Oeffnen(g.CurrentCell.ColumnIndex, g.CurrentCell.RowIndex);
                    e.Handled = e.SuppressKeyPress = true;
                }
            };
            g.Scroll += (s, e) => Schliessen(true);
            g.SizeChanged += (s, e) => Schliessen(true);
            g.ColumnWidthChanged += (s, e) => Schliessen(true);
        }

        static SpaltenInfo Info(DataGridViewColumn c)
        {
            return c.Tag as SpaltenInfo;
        }

        int hoverZeile = -1;

        // Beim Darüberfahren immer ganze Zeilen neu zeichnen, damit der Zeilenhintergrund einheitlich bleibt
        void Hover(DataGridViewCell z)
        {
            if (z == hoverZelle) return;
            var alt = hoverZelle;
            int alteZeile = hoverZeile;
            hoverZelle = z;
            hoverZeile = z != null ? z.RowIndex : -1;
            if (alteZeile != hoverZeile)
            {
                if (alteZeile >= 0 && alteZeile < g.RowCount) g.InvalidateRow(alteZeile);
                if (hoverZeile >= 0) g.InvalidateRow(hoverZeile);
            }
            else
            {
                if (alt != null && alt.RowIndex >= 0 && alt.RowIndex < g.RowCount) g.InvalidateCell(alt);
                if (z != null) g.InvalidateCell(z);
            }
        }

        // Das Eingabefeld innerhalb einer Zelle
        Rectangle Feld(Rectangle zelle)
        {
            return Rectangle.FromLTRB(zelle.Left + Theme.S(6), zelle.Top + Theme.S(8), zelle.Right - Theme.S(6), zelle.Bottom - Theme.S(8));
        }

        public static string WertText(DataGridViewCell z)
        {
            var info = Info(z.OwningColumn);
            if (info != null && info.Art == SpaltenArt.Mehrfach)
            {
                var liste = z.Tag as List<string>;
                return liste == null || liste.Count == 0 ? "" : string.Join(", ", liste);
            }
            return z.Value == null ? "" : z.Value.ToString();
        }

        // Jede Zelle stellt den Zeichenzustand wieder her: sonst bleibt die Kantenglättung an,
        // die nächste Zelle füllt ihren Hintergrund nur halb deckend und alte Linien scheinen durch.
        void Malen(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || Info(g.Columns[e.ColumnIndex]) == null) return;
            var zustand = e.Graphics.Save();
            try { MalenInnen(e); }
            finally { e.Graphics.Restore(zustand); }
        }

        void MalenInnen(DataGridViewCellPaintingEventArgs e)
        {
            var info = Info(g.Columns[e.ColumnIndex]);
            var zelle = g[e.ColumnIndex, e.RowIndex];
            var gr = e.Graphics;
            TabellenStil.Hintergrund(gr, g, e.RowIndex, e.CellBounds, hoverZeile);
            gr.SmoothingMode = SmoothingMode.AntiAlias;

            if (info.Art == SpaltenArt.Schalter)
            {
                Formen.Schalter(gr, new RectangleF(e.CellBounds.X, e.CellBounds.Y, Theme.S(36) + 2 * Theme.S(8), e.CellBounds.Height),
                    e.Value is bool && (bool)e.Value);
                e.Handled = true;
                return;
            }
            if (zelle == editZelle)
            {
                e.Handled = true;   // dort liegt gerade das Eingabefeld
                return;
            }

            var feld = Feld(e.CellBounds);
            bool aktiv = zelle == offeneZelle, hover = zelle == hoverZelle;
            var r = new RectangleF(feld.X + 0.5f, feld.Y + 0.5f, feld.Width - 1, feld.Height - 1);
            Formen.Fuellen(gr, r, Theme.S(7), Theme.Eingabe);
            Formen.Umrandung(gr, r, Theme.S(7), aktiv ? Theme.Akzent : hover ? Theme.Gedaempft : Theme.EingabeRand, aktiv ? 1.5f : 1f);

            int icon = Theme.S(14);
            bool aufklappbar = info.Art == SpaltenArt.Auswahl || info.Art == SpaltenArt.Mehrfach || info.Art == SpaltenArt.Farbe;
            int textRechts = feld.Right - Theme.S(10) - (aufklappbar ? icon + Theme.S(6) : 0);
            if (aufklappbar)
                Lucide.Zeichne(gr, aktiv ? "chevron-up" : "chevron-down", new RectangleF(feld.Right - icon - Theme.S(10), feld.Y + (feld.Height - icon) / 2f, icon, icon), aktiv ? Theme.Akzent : Theme.Gedaempft);

            if (info.Art == SpaltenArt.Farbe)
            {
                var farbe = zelle.Tag is Color ? (Color)zelle.Tag : Color.Gray;
                var chip = new RectangleF(feld.X + Theme.S(6), feld.Y + Theme.S(5), Math.Max(Theme.S(10), Math.Min(Theme.S(56), textRechts - feld.X - Theme.S(8))), feld.Height - Theme.S(10));
                Formen.Fuellen(gr, chip, Theme.S(4), farbe);
                Formen.Umrandung(gr, chip, Theme.S(4), Color.FromArgb(40, 0, 0, 0));
            }
            else
            {
                string wert = WertText(zelle);
                string text = info.Anzeige != null ? info.Anzeige(wert) : wert;
                bool platzhalter = info.Anzeige != null && text != wert;
                TextRenderer.DrawText(gr, text, g.DefaultCellStyle.Font,
                    Rectangle.FromLTRB(feld.X + Theme.S(10), feld.Y, textRechts, feld.Bottom),
                    platzhalter ? Theme.Gedaempft : Theme.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            e.Handled = true;
        }

        void Klick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if ((Control.ModifierKeys & (Keys.Control | Keys.Shift)) != 0) return;   // Mehrfachauswahl von Zeilen
            Oeffnen(e.ColumnIndex, e.RowIndex);
        }

        void Melden()
        {
            var h = Geaendert;
            if (h != null) h();
        }

        public void Oeffnen(int spalte, int zeile)
        {
            var info = Info(g.Columns[spalte]);
            if (info == null) return;
            Schliessen(true);
            var zelle = g[spalte, zeile];
            var feld = Feld(g.GetCellDisplayRectangle(spalte, zeile, false));

            switch (info.Art)
            {
                case SpaltenArt.Schalter:
                    zelle.Value = !(zelle.Value is bool && (bool)zelle.Value);
                    g.InvalidateCell(zelle);
                    Melden();
                    break;

                case SpaltenArt.Text:
                case SpaltenArt.Zahl:
                    editZelle = zelle;
                    editor = new Eingabefeld { Bounds = feld, NurZahlen = info.Art == SpaltenArt.Zahl };
                    editor.Untergrundfarbe = zelle.OwningRow.Selected ? Theme.Auswahl : Theme.Karte;
                    editor.Text = WertText(zelle);
                    var box = editor.Box;
                    box.KeyDown += (s, e) =>
                    {
                        if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Schliessen(true); g.Focus(); }
                        else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Schliessen(false); g.Focus(); }
                        else if (e.KeyCode == Keys.Tab) { e.SuppressKeyPress = true; Naechstes(zelle, e.Shift ? -1 : 1); }
                    };
                    box.PreviewKeyDown += (s, e) => { if (e.KeyCode == Keys.Tab) e.IsInputKey = true; };
                    box.LostFocus += (s, e) =>
                    {
                        var dieser = editor;
                        g.BeginInvoke(new Action(() => { if (editor == dieser) Schliessen(true); }));
                    };
                    g.Controls.Add(editor);
                    g.InvalidateCell(zelle);
                    box.Focus();
                    box.SelectAll();
                    break;

                case SpaltenArt.Auswahl:
                case SpaltenArt.Mehrfach:
                {
                    if (Popup.GeradeGeschlossen(zelle)) break;
                    bool mehrfach = info.Art == SpaltenArt.Mehrfach;
                    var aktuell = mehrfach ? (zelle.Tag as List<string> ?? new List<string>()) : new List<string> { WertText(zelle) };
                    var liste = new AuswahlListe(info.Optionen(), aktuell, mehrfach, feld.Width, info.Anzeige);
                    var popup = new Popup(liste);
                    liste.Gewaehlt += w => { zelle.Value = w; popup.Close(); Melden(); };
                    liste.Geaendert += l => { zelle.Tag = l; g.InvalidateCell(zelle); Melden(); };
                    AufklappenBei(zelle, popup, feld);
                    break;
                }

                case SpaltenArt.Farbe:
                {
                    if (Popup.GeradeGeschlossen(zelle)) break;
                    var aktuell = zelle.Tag is Color ? (Color)zelle.Tag : Color.Gray;
                    var palette = new FarbAuswahl(aktuell);
                    var popup = new Popup(palette);
                    palette.Gewaehlt += c => { zelle.Tag = c; popup.Close(); Melden(); };
                    palette.WeitereFarben += () =>
                    {
                        popup.Close();
                        using (var dlg = new ColorDialog { Color = aktuell, FullOpen = true })
                            if (dlg.ShowDialog(g.FindForm()) == DialogResult.OK)
                            {
                                zelle.Tag = dlg.Color;
                                g.InvalidateCell(zelle);
                                Melden();
                            }
                    };
                    AufklappenBei(zelle, popup, feld);
                    break;
                }
            }
        }

        void AufklappenBei(DataGridViewCell zelle, Popup popup, Rectangle feld)
        {
            offeneZelle = zelle;
            popup.Besitzer = zelle;
            g.InvalidateCell(zelle);
            popup.Closed += (s, e) =>
            {
                if (offeneZelle == zelle) offeneZelle = null;
                if (zelle.RowIndex >= 0) g.InvalidateCell(zelle);
            };
            popup.ZeigenBei(g, feld);
        }

        // Tab: zum nächsten Textfeld in der Tabelle
        void Naechstes(DataGridViewCell von, int richtung)
        {
            Schliessen(true);
            int spalten = g.ColumnCount, index = von.RowIndex * spalten + von.ColumnIndex;
            for (int i = index + richtung; i >= 0 && i < g.RowCount * spalten; i += richtung)
            {
                var info = Info(g.Columns[i % spalten]);
                if (info == null || (info.Art != SpaltenArt.Text && info.Art != SpaltenArt.Zahl)) continue;
                g.CurrentCell = g[i % spalten, i / spalten];
                Oeffnen(i % spalten, i / spalten);
                return;
            }
            g.Focus();
        }

        // Offenes Eingabefeld schliessen (und den Text übernehmen)
        public void Schliessen(bool uebernehmen)
        {
            if (editor == null) return;
            var e = editor;
            var zelle = editZelle;
            editor = null;
            editZelle = null;
            if (uebernehmen && zelle.RowIndex >= 0)
            {
                string neu = e.Text.Trim(), alt = WertText(zelle);
                if (alt != neu)
                {
                    zelle.Value = neu;
                    var h = Umbenannt;
                    if (h != null && alt != "") h(zelle, alt, neu);
                    Melden();
                }
            }
            g.Controls.Remove(e);
            e.Dispose();
            if (zelle.RowIndex >= 0) g.InvalidateCell(zelle);
        }
    }
}

namespace ShiftDesk
{
    // Einheitlicher Zeilenhintergrund ohne Gitterlinien: hervorgehobene Zeilen als abgerundete Fläche über alle Zellen
    public static class TabellenStil
    {
        public static void Hintergrund(Graphics gr, DataGridView g, int zeile, Rectangle zelle, int hoverZeile)
        {
            // Hintergrund immer ohne Kantenglättung füllen, damit die Zelle randscharf deckt
            var glaettung = gr.SmoothingMode;
            gr.SmoothingMode = SmoothingMode.None;
            using (var b = new SolidBrush(Theme.Karte))
                gr.FillRectangle(b, zelle);
            gr.SmoothingMode = glaettung;
            if (zeile < 0 || zeile >= g.RowCount) return;
            bool gewaehlt = g.Rows[zeile].Selected;
            if (!gewaehlt && zeile != hoverZeile) return;
            var r = g.GetRowDisplayRectangle(zeile, false);
            var flaeche = new RectangleF(r.X + 1, r.Y + Theme.S(3), r.Width - 2, r.Height - Theme.S(6));
            var zustand = gr.Save();
            gr.SetClip(zelle);
            gr.SmoothingMode = SmoothingMode.AntiAlias;
            Formen.Fuellen(gr, flaeche, Theme.S(9), gewaehlt ? Theme.Auswahl : Theme.Hover);
            gr.Restore(zustand);
        }
    }
}
