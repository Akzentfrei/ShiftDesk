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
    // Maus, Kurzinfo für abgeschnittene Texte und Bearbeiten der Zellen
    public partial class Tabelle
    {
        /// <summary>Hervorheben unter der Maus: Info-Zeichen im Kopf, Zeile und Zeilenknopf.</summary>
        void MausBewegt(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex == -1)
            {
                InfoHover(AufInfo(e) ? g.Columns[e.ColumnIndex] : null);
                return;
            }
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            Hover(g[e.ColumnIndex, e.RowIndex]);
            int k = KnopfBei(e);
            if (k != hoverKnopf)
            {
                hoverKnopf = k;
                g.InvalidateCell(e.ColumnIndex, e.RowIndex);
            }
            g.Cursor = k >= 0 ? Cursors.Hand : Cursors.Default;
        }

        /// <summary>Beim Darüberfahren immer ganze Zeilen neu zeichnen, damit der Zeilenhintergrund einheitlich bleibt.</summary>
        void Hover(DataGridViewCell z)
        {
            if (z == hoverZelle) return;
            var alt = hoverZelle;
            int alteZeile = hoverZeile;
            hoverZelle = z;
            hoverKnopf = -1;
            hoverZeile = z != null ? z.RowIndex : -1;
            TippPlanen(z);
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

        /// <summary>
        /// Abgeschnittener Zellentext erscheint nach kurzem Verweilen ganz (eigener Tooltip statt dem von Windows)
        /// </summary>
        readonly ThemaTipp tipp = new ThemaTipp();
        readonly Timer tippTimer = new Timer { Interval = 500 };
        DataGridViewCell tippZelle;

        /// <summary>Kurzinfo für diese Zelle nach kurzem Verweilen zeigen (null = keine).</summary>
        void TippPlanen(DataGridViewCell z)
        {
            tippTimer.Stop();
            tipp.Verbergen(g);
            tippZelle = z;
            if (z != null) tippTimer.Start();
        }

        /// <summary>Zeigt den ganzen Text, wenn er in der Zelle abgeschnitten ist.</summary>
        void TippZeigen()
        {
            tippTimer.Stop();
            var z = tippZelle;
            if (z == null || z.RowIndex < 0 || z.RowIndex >= g.RowCount || z == editZelle || z == offeneZelle) return;
            var info = Info(z.OwningColumn);
            if (info == null || info.Art == SpaltenArt.Aktionen || info.Art == SpaltenArt.Schalter || info.Art == SpaltenArt.Farbe) return;
            string wert = WertText(z);
            string text = info.Anzeige != null ? info.Anzeige(wert) : wert;
            if (string.IsNullOrEmpty(text)) return;
            // gleiche Aufteilung wie beim Zeichnen
            var zelle = g.GetCellDisplayRectangle(z.ColumnIndex, z.RowIndex, false);
            Rectangle text0 = TextFeld(info, zelle, z.OwningRow);
            if (TextRenderer.MeasureText(text, g.DefaultCellStyle.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width <= text0.Width) return;
            tipp.Zeigen(text, g, new Point(zelle.X + Thema.S(6), zelle.Bottom - Thema.S(4)));
        }

        /// <summary>Text einer Zelle; bei Mehrfach-Spalten die gewählten Werte mit Komma.</summary>
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

        /// <summary>Klick in eine Zelle: Zeilenknopf ausführen oder die Zelle bearbeiten.</summary>
        void Klick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var info = Info(g.Columns[e.ColumnIndex]);
            if (info != null && info.Art == SpaltenArt.Aktionen)
            {
                // erst nach dem Klick ausführen: Zeilen entfernen/umstellen mitten im Mausereignis mag die Tabelle nicht
                int k = KnopfBei(e), zeile = e.RowIndex;
                if (k >= 0) g.BeginInvoke(new Action(() => { info.Knoepfe[k].Ausfuehren(zeile); Hover(null); }));
                return;
            }
            if ((Control.ModifierKeys & (Keys.Control | Keys.Shift)) != 0) return;   // Mehrfachauswahl von Zeilen
            Oeffnen(e.ColumnIndex, e.RowIndex);
        }

        /// <summary>
        /// Zelle bearbeiten: Schalter umlegen, Eingabefeld öffnen oder Liste bzw. Farbwahl aufklappen.
        /// Feste Texte, Anzeige- und Knopfspalten lassen sich nicht bearbeiten.
        /// </summary>
        public void Oeffnen(int spalte, int zeile)
        {
            // die Zeile kann inzwischen entfernt worden sein (Aufruf über BeginInvoke)
            if (spalte < 0 || spalte >= g.ColumnCount || zeile < 0 || zeile >= g.RowCount) return;
            var info = Info(g.Columns[spalte]);
            if (info == null || info.Art == SpaltenArt.Aktionen || info.Art == SpaltenArt.Anzeige) return;
            if (info.FesterText != null && info.FesterText(g.Rows[zeile]) != null) return;
            Schliessen(true);
            TippPlanen(null);
            ZelleZeigen(spalte, zeile);
            var zelle = g[spalte, zeile];
            var feld = Feld(g.GetCellDisplayRectangle(spalte, zeile, false));

            switch (info.Art)
            {
                case SpaltenArt.Schalter:
                    zelle.Value = !(zelle.Value is bool && (bool)zelle.Value);
                    g.InvalidateRow(zeile);   // andere Zellen der Zeile können davon abhängen (Symbol)
                    break;

                case SpaltenArt.Text:
                case SpaltenArt.Zahl:
                    editZelle = zelle;
                    editor = new Eingabefeld { Bounds = feld, NurZahlen = info.Art == SpaltenArt.Zahl };
                    editor.Untergrundfarbe = zelle.OwningRow.Selected ? Thema.Auswahl : Thema.Karte;
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
                    // die ganze Zeile neu zeichnen: andere Zellen können davon abhängen (z. B. Gruppe beim Stv)
                    liste.Gewaehlt += w => { zelle.Value = w; popup.Close(); g.InvalidateRow(zeile); };
                    liste.Geaendert += l => { zelle.Tag = l; g.InvalidateCell(zelle); };
                    AufklappenBei(zelle, popup, feld);
                    break;
                }

                case SpaltenArt.Farbe:
                {
                    if (Popup.GeradeGeschlossen(zelle)) break;
                    FarbPalette(zelle, feld);
                    break;
                }
            }
        }

        /// <summary>Farbe einer Farb-Zelle (aus dem Tag).</summary>
        static Color Farbe(DataGridViewCell zelle)
        {
            return zelle.Tag is Color ? (Color)zelle.Tag : Color.Gray;
        }

        /// <summary>Farbpalette unter der Zelle; «Eigene Farbe…» wechselt zum Farbmischer.</summary>
        void FarbPalette(DataGridViewCell zelle, Rectangle feld)
        {
            var palette = new FarbAuswahl(Farbe(zelle));
            var popup = new Popup(palette);
            palette.Gewaehlt += c => { zelle.Tag = c; popup.Close(); };
            palette.WeitereFarben += () => { popup.Close(); FarbMischen(zelle, feld); };
            AufklappenBei(zelle, popup, feld);
        }

        /// <summary>«Eigene Farbe…»: freie Wahl am gleichen Ort, «Palette» führt zurück.</summary>
        void FarbMischen(DataGridViewCell zelle, Rectangle feld)
        {
            var mischer = new FarbMischer(Farbe(zelle));
            var popup = new Popup(mischer);
            mischer.Gewaehlt += c => { zelle.Tag = c; popup.Close(); };
            mischer.Zurueck += () => { popup.Close(); FarbPalette(zelle, feld); };
            AufklappenBei(zelle, popup, feld);
        }

        /// <summary>Zeigt ein Popup unter der Zelle; solange es offen ist, ist die Zelle als aktiv gezeichnet.</summary>
        void AufklappenBei(DataGridViewCell zelle, Popup popup, Rectangle feld)
        {
            offeneZelle = zelle;
            popup.Besitzer = zelle;
            g.InvalidateCell(zelle);
            popup.Closed += (s, e) =>
            {
                if (offeneZelle == zelle) offeneZelle = null;
                if (zelle.RowIndex >= 0 && zelle.RowIndex < g.RowCount) g.InvalidateCell(zelle);
            };
            popup.ZeigenBei(g, feld);
        }

        /// <summary>Tab: zum nächsten Textfeld in der Tabelle.</summary>
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

        /// <summary>Offenes Eingabefeld schliessen.</summary>
        /// <param name="uebernehmen">Den eingegebenen Text in die Zelle übernehmen (sonst verwerfen, z. B. mit Esc).</param>
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
                }
            }
            g.Controls.Remove(e);
            e.Dispose();
            if (zelle.RowIndex >= 0) g.InvalidateCell(zelle);
        }
    }
}
