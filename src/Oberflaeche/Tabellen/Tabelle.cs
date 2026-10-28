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
    /// <summary>
    /// Tabelle im eigenen Design, gleich auf allen Seiten: Karte mit runden Ecken, Kopf mit Info-Zeichen,
    /// Zeilen ohne Gitterlinien und ohne Bildlaufleisten (geblättert wird mit dem Mausrad, mit Shift waagrecht).
    /// </summary>
    /// <remarks>
    /// <para>Bearbeitbare Zellen sehen wie Eingabefelder aus und werden mit eigenen Steuerelementen bearbeitet.
    /// Wie eine Spalte bearbeitet und gezeichnet wird, sagt ihre <see cref="SpaltenInfo"/> (im Column.Tag).
    /// Mehrfach-Spalten speichern ihre Werte als <c>List&lt;string&gt;</c> im Zellen-Tag, Farb-Spalten eine
    /// <see cref="Color"/> im Tag.</para>
    /// <para>Aufgeteilt auf: Aufbau, Spalten und Bildlauf (diese Datei), <c>Tabelle.Zeichnen.cs</c>,
    /// <c>Tabelle.Bearbeiten.cs</c> (Maus, Kurzinfo, Eingabe) und <c>Tabelle.Zeilen.cs</c> (Zeilen hinzufügen,
    /// entfernen, verschieben, Werte lesen).</para>
    /// </remarks>
    public partial class Tabelle
    {
        readonly DataGridView g;
        readonly Karte karte;
        Eingabefeld editor;
        DataGridViewCell editZelle, hoverZelle, offeneZelle;   // Zelle mit Eingabefeld, unter der Maus, mit offenem Popup
        int hoverZeile = -1, hoverKnopf = -1;
        DataGridViewColumn infoHover;   // Spalte, über deren Info-Zeichen die Maus gerade ist

        /// <summary>
        /// Erst wenn der Benutzer in der Tabelle etwas angeklickt oder getippt hat, wird eine Auswahl gezeigt
        /// (sonst wäre beim Öffnen einer Seite immer die erste Zeile markiert).
        /// </summary>
        public bool Benutzt;

        /// <summary>
        /// Ein nicht leerer Text wurde geändert: Zelle, alter Text, neuer Text. So lassen sich Umbenennungen
        /// (z. B. einer Funktion) in anderen Tabellen nachführen.
        /// </summary>
        public event Action<DataGridViewCell, string, string> Umbenannt;

        /// <summary>Hinweis, solange die Tabelle leer ist: Symbol, Titel, Text (null = keiner).</summary>
        public string[] LeerHinweis;

        /// <param name="nurAnzeige">Nur zum Lesen (Statistik): niedrigere Zeilen, keine Mehrfachauswahl.</param>
        public Tabelle(bool nurAnzeige = false)
        {
            g = new DataGridView
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = Thema.S(44),
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                EnableHeadersVisualStyles = false,
                ReadOnly = true,
                EditMode = DataGridViewEditMode.EditProgrammatically,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = !nurAnzeige,
                ScrollBars = ScrollBars.None,   // keine Leisten, siehe Mausrad
                ShowCellToolTips = false,       // abgeschnittene Texte zeigt TippZeigen im eigenen Design
            };
            typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(g, true, null);
            g.RowTemplate.Height = Thema.S(nurAnzeige ? 42 : 50);
            g.DefaultCellStyle.Font = Schrift.Eingabe;
            g.DefaultCellStyle.Padding = new Padding(Thema.S(8), 0, Thema.S(8), 0);
            g.ColumnHeadersDefaultCellStyle.Font = Schrift.KleinFett;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(Thema.S(8), 0, 0, 0);
            g.DataError += (s, e) => { e.ThrowException = false; };

            g.CellMouseDown += (s, e) => Benutzt = true;
            g.KeyDown += (s, e) => Benutzt = true;
            g.SelectionChanged += (s, e) =>
            {
                if (!Benutzt && g.SelectedCells.Count > 0) g.ClearSelection();
                g.Invalidate();
            };
            g.CellPainting += Malen;
            g.CellMouseEnter += (s, e) => Hover(e.RowIndex >= 0 && e.ColumnIndex >= 0 ? g[e.ColumnIndex, e.RowIndex] : null);
            g.CellMouseMove += MausBewegt;
            g.CellMouseLeave += (s, e) => { if (e.RowIndex == -1) InfoHover(null); };
            g.MouseLeave += (s, e) => { Hover(null); InfoHover(null); g.Cursor = Cursors.Default; };
            g.CellMouseDown += KopfKlick;
            g.CellMouseClick += Klick;
            // erste/letzte Zeile ändern sich: Pfeile neu zeichnen
            g.RowsAdded += (s, e) => g.Invalidate();
            g.RowsRemoved += (s, e) => g.Invalidate();
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
            g.SizeChanged += (s, e) => { Schliessen(true); SpaltenVerteilen(); };
            g.ColumnWidthChanged += (s, e) => Schliessen(true);
            g.ColumnAdded += (s, e) =>
            {
                // jeder Spaltentitel soll ganz Platz haben
                e.Column.MinimumWidth = Math.Max(e.Column.MinimumWidth, KopfBreite(e.Column));
                SpaltenVerteilen();
            };
            g.MouseWheel += Mausrad;
            g.Paint += (s, e) =>
            {
                if (g.RowCount == 0 && LeerHinweis != null)
                {
                    int oben = g.ColumnCount > 0 ? g.ColumnHeadersHeight : 0;
                    Formen.Leer(e.Graphics, Rectangle.FromLTRB(0, oben, g.ClientSize.Width, g.ClientSize.Height), LeerHinweis[0], LeerHinweis[1], LeerHinweis[2]);
                }
            };
            tippTimer.Tick += (s, e) => TippZeigen();
            g.Disposed += (s, e) => { tippTimer.Dispose(); tipp.Dispose(); };

            karte = new Karte();
            karte.Controls.Add(g);
            Farbwechsel.Bei(FarbenAnwenden);
        }

        /// <summary>Das zugrunde liegende DataGridView (Zeilen und Werte).</summary>
        public DataGridView Grid
        {
            get { return g; }
        }

        /// <summary>Die Karte mit der Tabelle (das kommt auf die Seite).</summary>
        public Karte Karte
        {
            get { return karte; }
        }

        /// <summary>Die <see cref="SpaltenInfo"/> einer Spalte (null bei fremden Spalten).</summary>
        static SpaltenInfo Info(DataGridViewColumn c)
        {
            return c.Tag as SpaltenInfo;
        }

        /// <summary>Farben nach einem Wechsel hell/dunkel neu setzen (meldet sich selbst beim <see cref="Farbwechsel"/> an).</summary>
        void FarbenAnwenden()
        {
            karte.BackColor = Thema.Karte;
            g.BackgroundColor = Thema.Karte;
            g.GridColor = Thema.Gitter;
            g.DefaultCellStyle.BackColor = Thema.Karte;
            g.DefaultCellStyle.ForeColor = Thema.Text;
            g.DefaultCellStyle.SelectionBackColor = Thema.Auswahl;
            g.DefaultCellStyle.SelectionForeColor = Thema.Text;
            g.ColumnHeadersDefaultCellStyle.BackColor = Thema.Karte;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Thema.Gedaempft;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Thema.Karte;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Thema.Gedaempft;
            g.Invalidate();
            karte.Invalidate(true);
        }

        /// <summary>Keine Zeile ausgewählt, wie frisch geladen.</summary>
        public void Zuruecksetzen()
        {
            Benutzt = false;
            g.ClearSelection();
            g.CurrentCell = null;
            g.HorizontalScrollingOffset = 0;
        }

        // ---------------------------------------------------------------- Spalten

        /// <summary>Fügt eine Spalte hinzu.</summary>
        /// <param name="name">Interner Name (für <see cref="Wert"/> usw.).</param>
        /// <param name="titel">Text im Spaltenkopf.</param>
        /// <param name="gewicht">Anteil am freien Platz im Verhältnis zu den anderen Spalten.</param>
        /// <param name="info">Wie die Spalte bearbeitet und gezeichnet wird.</param>
        /// <param name="mindestens">Eigene Mindestbreite bei 96 dpi; 0 = nach Spaltenart.</param>
        public DataGridViewColumn Spalte(string name, string titel, int gewicht, SpaltenInfo info, int mindestens = 0)
        {
            if (mindestens <= 0)
                switch (info.Art)
                {
                    case SpaltenArt.Schalter: mindestens = 52; break;
                    case SpaltenArt.Zahl: mindestens = 64; break;
                    case SpaltenArt.Farbe: mindestens = 84; break;
                    case SpaltenArt.Mehrfach: mindestens = 130; break;
                    case SpaltenArt.Auswahl: mindestens = 100; break;
                    case SpaltenArt.Anzeige: mindestens = 56; break;
                    default: mindestens = 90; break;
                }
            var c = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = titel,
                FillWeight = Math.Max(1, gewicht),
                Tag = info,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                MinimumWidth = Thema.S(mindestens),
            };
            g.Columns.Add(c);
            return c;
        }

        /// <summary>Spalte mit Knöpfen am Ende jeder Zeile (feste Breite); siehe auch <see cref="Zeilenknoepfe"/>.</summary>
        public void Aktionen(params ZeilenKnopf[] knoepfe)
        {
            var c = Spalte("Aktionen", "", 1, new SpaltenInfo(knoepfe));
            c.MinimumWidth = c.Width = Thema.S(2 * KnopfRand + knoepfe.Length * KnopfGroesse + (knoepfe.Length - 1) * KnopfAbstand);
            SpaltenVerteilen();
        }

        /// <summary>Info-Zeichen im Spaltenkopf mit Erklärung.</summary>
        public void Erklaeren(string spalte, string text)
        {
            var c = g.Columns[spalte];
            Info(c).Erklaerung = text;
            c.MinimumWidth = Math.Max(c.MinimumWidth, KopfBreite(c));
            SpaltenVerteilen();
        }

        static int TitelBreite(DataGridViewColumn c)
        {
            return TextRenderer.MeasureText(c.HeaderText, c.DataGridView.ColumnHeadersDefaultCellStyle.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
        }

        /// <summary>Breite, die der Kopf ganz braucht: Farbquadrat, Titel, Info-Zeichen.</summary>
        static int KopfBreite(DataGridViewColumn c)
        {
            var info = Info(c);
            if (info != null && info.Art == SpaltenArt.Aktionen) return 0;
            return TitelBreite(c) + Thema.S(8 + 8)
                + (info != null && info.Farbe.HasValue ? Thema.S(17) : 0)
                + (info != null && info.Erklaerung != null ? Thema.S(3 + 14 + 5) : 0);
        }

        static bool Fest(DataGridViewColumn c)
        {
            var info = Info(c);
            return info != null && info.Fest;
        }

        /// <summary>
        /// Spaltenbreiten selbst verteilen (der eingebaute Fill-Modus verrechnet sich bei versteckten Seiten):
        /// feste Spalten behalten ihre Mindestbreite, die übrigen teilen den Rest nach Gewicht. Hat nicht alles
        /// Platz, bekommen alle ihre Mindestbreite und die Tabelle lässt sich waagrecht verschieben.
        /// </summary>
        void SpaltenVerteilen()
        {
            if (g.Columns.Count == 0 || g.ClientSize.Width <= 0) return;
            var alle = g.Columns.Cast<DataGridViewColumn>().ToList();
            var spalten = alle.Where(c => !Fest(c)).ToList();
            int verfuegbar = g.ClientSize.Width - 1 - alle.Where(Fest).Sum(c => c.MinimumWidth);
            // Spalten, deren Anteil unter der Mindestbreite läge, bekommen die Mindestbreite;
            // der Rest wird unter den übrigen nach Gewicht verteilt, bis keine mehr anstösst
            var frei = spalten.ToList();
            bool weiter = true;
            while (weiter && frei.Count > 0)
            {
                weiter = false;
                int rest = verfuegbar - spalten.Except(frei).Sum(c => c.MinimumWidth);
                float summe = frei.Sum(c => c.FillWeight);
                foreach (var c in frei.ToList())
                    if (rest * c.FillWeight / summe < c.MinimumWidth) { frei.Remove(c); weiter = true; }
            }
            int uebrig = verfuegbar - spalten.Except(frei).Sum(c => c.MinimumWidth);
            float gewicht = frei.Sum(c => c.FillWeight);
            foreach (var c in alle)
            {
                int w = frei.Contains(c) ? Math.Max(c.MinimumWidth, (int)(uebrig * c.FillWeight / gewicht)) : c.MinimumWidth;
                if (c.Width != w) c.Width = w;
            }
            int max = Math.Max(0, g.Columns.GetColumnsWidth(DataGridViewElementStates.Visible) - g.ClientSize.Width);
            if (g.HorizontalScrollingOffset > max) g.HorizontalScrollingOffset = max;
        }

        // ---------------------------------------------------------------- Bildlauf

        void Waagrecht(int wert)
        {
            int max = Math.Max(0, g.Columns.GetColumnsWidth(DataGridViewElementStates.Visible) - g.ClientSize.Width);
            wert = Math.Max(0, Math.Min(max, wert));
            if (wert != g.HorizontalScrollingOffset) g.HorizontalScrollingOffset = wert;
        }

        void Senkrecht(int ersteZeile)
        {
            if (g.RowCount == 0) return;
            int sichtbar = Math.Max(1, g.DisplayedRowCount(false));
            ersteZeile = Math.Max(0, Math.Min(Math.Max(0, g.RowCount - sichtbar), ersteZeile));
            if (ersteZeile != g.FirstDisplayedScrollingRowIndex) g.FirstDisplayedScrollingRowIndex = ersteZeile;
        }

        /// <summary>Mausrad: zwei Zeilen; mit Shift (oder wenn nur die Breite nicht reicht) waagrecht.</summary>
        void Mausrad(object sender, MouseEventArgs e)
        {
            bool zuBreit = g.Columns.GetColumnsWidth(DataGridViewElementStates.Visible) > g.ClientSize.Width;
            bool zuHoch = g.RowCount > g.DisplayedRowCount(false);
            if ((Control.ModifierKeys & Keys.Shift) != 0 || (zuBreit && !zuHoch))
                Waagrecht(g.HorizontalScrollingOffset - Math.Sign(e.Delta) * Thema.S(80));
            else
                Senkrecht(g.FirstDisplayedScrollingRowIndex - Math.Sign(e.Delta) * 2);
            var h = e as HandledMouseEventArgs;
            if (h != null) h.Handled = true;
        }

        /// <summary>Zelle ganz sichtbar machen (zum Bearbeiten).</summary>
        void ZelleZeigen(int spalte, int zeile)
        {
            int links = 0;
            for (int i = 0; i < spalte; i++)
                if (g.Columns[i].Visible) links += g.Columns[i].Width;
            int rechts = links + g.Columns[spalte].Width, versatz = g.HorizontalScrollingOffset, breite = g.ClientSize.Width;
            if (links < versatz) Waagrecht(links);
            else if (rechts > versatz + breite) Waagrecht(Math.Min(links, rechts - breite));
            int erste = g.FirstDisplayedScrollingRowIndex, sichtbar = Math.Max(1, g.DisplayedRowCount(false));
            if (erste < 0) return;
            if (zeile < erste) Senkrecht(zeile);
            else if (zeile >= erste + sichtbar) Senkrecht(zeile - sichtbar + 1);
        }
    }
}
