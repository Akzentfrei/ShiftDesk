using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace ShiftDesk
{
    // Anzeige: nur Text, nicht bearbeitbar (Statistik)
    public enum SpaltenArt { Text, Zahl, Auswahl, Mehrfach, Farbe, Schalter, Aktionen, Anzeige }

    // Hängt an einer Tabellenspalte (Column.Tag) und sagt, wie sie bearbeitet und gezeichnet wird
    public class SpaltenInfo
    {
        public SpaltenArt Art;
        public Func<IEnumerable<string>> Optionen;
        public Func<string, string> Anzeige;   // Text für einen Wert (z. B. "" -> "Keine")
        public Func<DataGridViewRow, string> Symbol;   // Icon rechts im Feld, abhängig von der Zeile (null = keins)
        public Func<DataGridViewRow, string> FesterText;   // fester Text statt Eingabe, abhängig von der Zeile (null = normal bearbeitbar)
        public ZeilenKnopf[] Knoepfe;          // nur bei Aktionen
        public string Erklaerung;              // Text hinter dem Info-Zeichen im Spaltenkopf (null = keins)
        public Color? Farbe;                   // farbiges Quadrat vor dem Titel (Postenfarbe in der Statistik)
        public bool Fest;                      // behält die Mindestbreite, statt mit der Tabelle mitzuwachsen

        public SpaltenInfo(SpaltenArt art, Func<IEnumerable<string>> optionen = null, Func<string, string> anzeige = null)
        {
            Art = art;
            Optionen = optionen;
            Anzeige = anzeige;
            Fest = art == SpaltenArt.Schalter || art == SpaltenArt.Zahl || art == SpaltenArt.Farbe || art == SpaltenArt.Aktionen;
        }

        public SpaltenInfo(params ZeilenKnopf[] knoepfe) : this(SpaltenArt.Aktionen)
        {
            Knoepfe = knoepfe;
        }
    }

    // Symbolknopf am Ende jeder Zeile (z. B. Verschieben, Entfernen)
    public class ZeilenKnopf
    {
        public string Icon;
        public Action<int> Ausfuehren;      // bekommt den Zeilenindex
        public Func<int, bool> Bedingung;   // null = immer möglich
        public bool Gefahr;                 // rot beim Darüberfahren

        public ZeilenKnopf(string icon, Action<int> ausfuehren, Func<int, bool> bedingung = null)
        {
            Icon = icon;
            Ausfuehren = ausfuehren;
            Bedingung = bedingung;
        }

        public bool Moeglich(int zeile)
        {
            return Bedingung == null || Bedingung(zeile);
        }
    }

    // Tabelle im eigenen Design, gleich auf allen Seiten: Karte mit runden Ecken, Kopf mit Info-Zeichen,
    // Zeilen ohne Gitterlinien, keine Bildlaufleisten: geblättert wird mit dem Mausrad
    // (mit Shift waagrecht). Bearbeitbare Zellen sehen wie Eingabefelder aus und werden mit eigenen
    // Steuerelementen bearbeitet. Mehrfach-Spalten speichern ihre Werte als List<string> im Zellen-Tag,
    // Farb-Spalten eine Color im Tag.
    public class Tabelle
    {
        readonly DataGridView g;
        readonly Karte karte;
        Eingabefeld editor;
        DataGridViewCell editZelle, hoverZelle, offeneZelle;
        int hoverZeile = -1, hoverKnopf = -1;
        DataGridViewColumn infoHover;   // Spalte, über deren Info-Zeichen die Maus gerade ist

        // Erst wenn der Benutzer in der Tabelle etwas angeklickt oder getippt hat, wird eine Auswahl gezeigt
        // (sonst wäre beim Öffnen einer Seite immer die erste Zeile markiert)
        public bool Benutzt;

        public event Action<DataGridViewCell, string, string> Umbenannt;   // Zelle, alter Text, neuer Text

        // Hinweis, solange die Tabelle leer ist: Symbol, Titel, Text (null = keiner)
        public string[] LeerHinweis;

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
                ColumnHeadersHeight = Theme.S(44),
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
            g.RowTemplate.Height = Theme.S(nurAnzeige ? 42 : 50);
            g.DefaultCellStyle.Font = Schrift.Eingabe;
            g.DefaultCellStyle.Padding = new Padding(Theme.S(8), 0, Theme.S(8), 0);
            g.ColumnHeadersDefaultCellStyle.Font = Schrift.KleinFett;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(Theme.S(8), 0, 0, 0);
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
            FarbenAnwenden();
        }

        public DataGridView Grid
        {
            get { return g; }
        }

        // Die Karte mit der Tabelle (das kommt auf die Seite)
        public Karte Karte
        {
            get { return karte; }
        }

        static SpaltenInfo Info(DataGridViewColumn c)
        {
            return c.Tag as SpaltenInfo;
        }

        public void FarbenAnwenden()
        {
            karte.BackColor = Theme.Karte;
            g.BackgroundColor = Theme.Karte;
            g.GridColor = Theme.Gitter;
            g.DefaultCellStyle.BackColor = Theme.Karte;
            g.DefaultCellStyle.ForeColor = Theme.Text;
            g.DefaultCellStyle.SelectionBackColor = Theme.Auswahl;
            g.DefaultCellStyle.SelectionForeColor = Theme.Text;
            g.ColumnHeadersDefaultCellStyle.BackColor = Theme.Karte;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Gedaempft;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Karte;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.Gedaempft;
            g.Invalidate();
            karte.Invalidate(true);
        }

        // Keine Zeile ausgewählt, wie frisch geladen
        public void Zuruecksetzen()
        {
            Benutzt = false;
            g.ClearSelection();
            g.CurrentCell = null;
            g.HorizontalScrollingOffset = 0;
        }

        // ---------------------------------------------------------------- Spalten

        // mindestens: eigene Mindestbreite (0 = nach Spaltenart)
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
                MinimumWidth = Theme.S(mindestens),
            };
            g.Columns.Add(c);
            return c;
        }

        // Knöpfe am Ende jeder Zeile (feste Breite)
        public void Aktionen(params ZeilenKnopf[] knoepfe)
        {
            var c = Spalte("Aktionen", "", 1, new SpaltenInfo(knoepfe));
            c.MinimumWidth = c.Width = Theme.S(2 * KnopfRand + knoepfe.Length * KnopfGroesse + (knoepfe.Length - 1) * KnopfAbstand);
            SpaltenVerteilen();
        }

        // Info-Zeichen im Spaltenkopf mit Erklärung
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

        // Breite, die der Kopf ganz braucht: Farbquadrat, Titel, Info-Zeichen
        static int KopfBreite(DataGridViewColumn c)
        {
            var info = Info(c);
            if (info != null && info.Art == SpaltenArt.Aktionen) return 0;
            return TitelBreite(c) + Theme.S(8 + 8)
                + (info != null && info.Farbe.HasValue ? Theme.S(17) : 0)
                + (info != null && info.Erklaerung != null ? Theme.S(3 + 14 + 5) : 0);
        }

        static bool Fest(DataGridViewColumn c)
        {
            var info = Info(c);
            return info != null && info.Fest;
        }

        // Spaltenbreiten selbst verteilen (der eingebaute Fill-Modus verrechnet sich bei versteckten Seiten):
        // feste Spalten behalten ihre Mindestbreite, die übrigen teilen den Rest nach Gewicht. Hat nicht alles
        // Platz, bekommen alle ihre Mindestbreite und die Tabelle lässt sich waagrecht verschieben.
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

        // Mausrad: zwei Zeilen; mit Shift (oder wenn nur die Breite nicht reicht) waagrecht
        void Mausrad(object sender, MouseEventArgs e)
        {
            bool zuBreit = g.Columns.GetColumnsWidth(DataGridViewElementStates.Visible) > g.ClientSize.Width;
            bool zuHoch = g.RowCount > g.DisplayedRowCount(false);
            if ((Control.ModifierKeys & Keys.Shift) != 0 || (zuBreit && !zuHoch))
                Waagrecht(g.HorizontalScrollingOffset - Math.Sign(e.Delta) * Theme.S(80));
            else
                Senkrecht(g.FirstDisplayedScrollingRowIndex - Math.Sign(e.Delta) * 2);
            var h = e as HandledMouseEventArgs;
            if (h != null) h.Handled = true;
        }

        // Zelle ganz sichtbar machen (zum Bearbeiten)
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

        // ---------------------------------------------------------------- Kopf mit Info-Zeichen

        // Info-Zeichen im Spaltenkopf, gleich hinter dem Titel (relativ zur Kopfzelle)
        Rectangle InfoFeld(int spalte, Size zelle)
        {
            var c = g.Columns[spalte];
            var info = Info(c);
            int x = Theme.S(8) + (info != null && info.Farbe.HasValue ? Theme.S(17) : 0);
            int d = Theme.S(14);
            int ix = Math.Max(x, Math.Min(x + TitelBreite(c) + Theme.S(3), zelle.Width - d - Theme.S(4)));
            return new Rectangle(ix, (zelle.Height - d) / 2, d, d);
        }

        bool AufInfo(DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex < 0) return false;
            var info = Info(g.Columns[e.ColumnIndex]);
            if (info == null || info.Erklaerung == null) return false;
            var zelle = g.GetCellDisplayRectangle(e.ColumnIndex, -1, false);
            return Rectangle.Inflate(InfoFeld(e.ColumnIndex, zelle.Size), Theme.S(3), Theme.S(3)).Contains(e.Location);
        }

        void InfoHover(DataGridViewColumn neu)
        {
            if (neu == infoHover) return;
            if (infoHover != null && infoHover.DataGridView == g) g.InvalidateCell(infoHover.Index, -1);
            infoHover = neu;
            if (neu != null) g.InvalidateCell(neu.Index, -1);
            g.Cursor = neu != null ? Cursors.Hand : Cursors.Default;
        }

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

        void KopfMalen(DataGridViewCellPaintingEventArgs e)
        {
            var spalte = g.Columns[e.ColumnIndex];
            var info = Info(spalte);
            var gr = e.Graphics;
            var r = e.CellBounds;
            gr.SmoothingMode = SmoothingMode.None;
            using (var b = new SolidBrush(Theme.Karte))
                gr.FillRectangle(b, r);
            int x = r.X + Theme.S(8);
            if (info != null && info.Farbe.HasValue)
            {
                gr.SmoothingMode = SmoothingMode.AntiAlias;
                int d = Theme.S(10);
                Formen.Fuellen(gr, new RectangleF(x, r.Y + (r.Height - d) / 2f, d, d), Theme.S(3), info.Farbe.Value);
                x += d + Theme.S(7);
            }
            int rechts = r.Right - Theme.S(4);
            if (info != null && info.Erklaerung != null)
            {
                var feld = InfoFeld(e.ColumnIndex, r.Size);
                feld.Offset(r.Location);
                gr.SmoothingMode = SmoothingMode.AntiAlias;
                InfoText.Symbol(gr, feld, spalte == infoHover);
                rechts = feld.Left - Theme.S(2);
            }
            TextRenderer.DrawText(gr, spalte.HeaderText, g.ColumnHeadersDefaultCellStyle.Font,
                Rectangle.FromLTRB(x, r.Y, rechts, r.Bottom), Theme.Gedaempft,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            using (var p = new Pen(Theme.Rand))
                gr.DrawLine(p, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
            e.Handled = true;
        }

        // ---------------------------------------------------------------- Maus

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

        // Beim Darüberfahren immer ganze Zeilen neu zeichnen, damit der Zeilenhintergrund einheitlich bleibt
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

        // Abgeschnittener Zellentext erscheint nach kurzem Verweilen ganz (eigener Tooltip statt dem von Windows)
        readonly ThemaTipp tipp = new ThemaTipp();
        readonly Timer tippTimer = new Timer { Interval = 500 };
        DataGridViewCell tippZelle;

        void TippPlanen(DataGridViewCell z)
        {
            tippTimer.Stop();
            tipp.Verbergen(g);
            tippZelle = z;
            if (z != null) tippTimer.Start();
        }

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
            tipp.Zeigen(text, g, new Point(zelle.X + Theme.S(6), zelle.Bottom - Theme.S(4)));
        }

        // Das Eingabefeld innerhalb einer Zelle
        static Rectangle Feld(Rectangle zelle)
        {
            return Rectangle.FromLTRB(zelle.Left + Theme.S(6), zelle.Top + Theme.S(8), zelle.Right - Theme.S(6), zelle.Bottom - Theme.S(8));
        }

        // Wo der Text einer Zelle steht (ohne Pfeil und Symbol)
        static Rectangle TextFeld(SpaltenInfo info, Rectangle zelle, DataGridViewRow zeile)
        {
            if (info.Art == SpaltenArt.Anzeige)
                return Rectangle.FromLTRB(zelle.X + Theme.S(8), zelle.Y, zelle.Right - Theme.S(4), zelle.Bottom);
            var feld = Feld(zelle);
            int rechts = feld.Right - Theme.S(10);
            if (info.Art == SpaltenArt.Auswahl || info.Art == SpaltenArt.Mehrfach) rechts = feld.Right - Formen.PfeilPlatz;
            if (info.Symbol != null && zeile != null && info.Symbol(zeile) != null) rechts -= Theme.S(14 + 6);
            return Rectangle.FromLTRB(feld.X + Theme.S(10), feld.Y, rechts, feld.Bottom);
        }

        // Zeilenknöpfe: rechtsbündig nebeneinander
        const int KnopfGroesse = 32, KnopfAbstand = 2, KnopfRand = 10;

        static RectangleF KnopfFeld(Rectangle zelle, int anzahl, int k)
        {
            float d = Theme.S(KnopfGroesse), abstand = Theme.S(KnopfAbstand);
            float x = zelle.Right - Theme.S(KnopfRand) - anzahl * d - (anzahl - 1) * abstand + k * (d + abstand);
            return new RectangleF(x, zelle.Y + (zelle.Height - d) / 2f, d, d);
        }

        // Index des (ausführbaren) Knopfs unter der Maus, sonst -1
        int KnopfBei(DataGridViewCellMouseEventArgs e)
        {
            var info = Info(g.Columns[e.ColumnIndex]);
            if (info == null || info.Art != SpaltenArt.Aktionen || e.RowIndex < 0 || e.RowIndex >= g.RowCount) return -1;
            var zelle = new Rectangle(0, 0, g.Columns[e.ColumnIndex].Width, g.Rows[e.RowIndex].Height);
            for (int k = 0; k < info.Knoepfe.Length; k++)
                if (KnopfFeld(zelle, info.Knoepfe.Length, k).Contains(e.Location) && info.Knoepfe[k].Moeglich(e.RowIndex)) return k;
            return -1;
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

        // ---------------------------------------------------------------- Zeichnen

        // Jede Zelle stellt den Zeichenzustand wieder her: sonst bleibt die Kantenglättung an,
        // die nächste Zelle füllt ihren Hintergrund nur halb deckend und alte Linien scheinen durch.
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

        // Einheitlicher Zeilenhintergrund ohne Gitterlinien: hervorgehobene Zeilen als abgerundete Fläche über alle Zellen
        void Hintergrund(Graphics gr, int zeile, Rectangle zelle)
        {
            // Hintergrund ohne Kantenglättung füllen, damit die Zelle randscharf deckt
            gr.SmoothingMode = SmoothingMode.None;
            using (var b = new SolidBrush(Theme.Karte))
                gr.FillRectangle(b, zelle);
            if (zeile < 0 || zeile >= g.RowCount) return;
            bool gewaehlt = (g.Rows.GetRowState(zeile) & DataGridViewElementStates.Selected) != 0;
            if (!gewaehlt && zeile != hoverZeile) return;
            var r = g.GetRowDisplayRectangle(zeile, false);
            var flaeche = new RectangleF(r.X + 1, r.Y + Theme.S(3), r.Width - 2, r.Height - Theme.S(6));
            var zustand = gr.Save();
            gr.SetClip(zelle);
            gr.SmoothingMode = SmoothingMode.AntiAlias;
            Formen.Fuellen(gr, flaeche, Theme.S(9), gewaehlt ? Theme.Auswahl : Theme.Hover);
            gr.Restore(zustand);
        }

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
                    int symbolGroesse = Theme.S(16);
                    for (int k = 0; k < info.Knoepfe.Length; k++)
                    {
                        var knopf = info.Knoepfe[k];
                        var kf = KnopfFeld(e.CellBounds, info.Knoepfe.Length, k);
                        bool moeglich = knopf.Moeglich(e.RowIndex), darueber = moeglich && zelle == hoverZelle && k == hoverKnopf;
                        if (darueber)
                            Formen.Fuellen(gr, kf, Theme.S(7), knopf.Gefahr ? Theme.Toenung(Theme.Gefahr) : Theme.Rand);
                        var farbe = !moeglich ? Color.FromArgb(70, Theme.Gedaempft) : darueber ? (knopf.Gefahr ? Theme.Gefahr : Theme.Text) : Theme.Gedaempft;
                        Lucide.Zeichne(gr, knopf.Icon, Formen.Mitte(kf, symbolGroesse), farbe);
                    }
                    return;
                }
                case SpaltenArt.Schalter:
                    Formen.Schalter(gr, new RectangleF(e.CellBounds.X, e.CellBounds.Y, Theme.S(36) + 2 * Theme.S(8), e.CellBounds.Height),
                        e.Value is bool && (bool)e.Value);
                    return;
                case SpaltenArt.Anzeige:
                {
                    string text = WertText(zelle);
                    TextRenderer.DrawText(gr, text, g.DefaultCellStyle.Font, TextFeld(info, e.CellBounds, null),
                        text == "–" ? Theme.Gedaempft : Theme.Text,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    return;
                }
            }
            if (zelle == editZelle) return;   // dort liegt gerade das Eingabefeld

            string fest = info.FesterText != null ? info.FesterText(g.Rows[e.RowIndex]) : null;
            if (fest != null)
            {
                // ergibt sich von selbst: grauer Text ohne Eingabefeld
                TextRenderer.DrawText(gr, fest, g.DefaultCellStyle.Font, TextFeld(info, e.CellBounds, null), Theme.Gedaempft,
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
                var chip = new RectangleF(feld.X + Theme.S(6), feld.Y + Theme.S(5),
                    Math.Max(Theme.S(10), Math.Min(Theme.S(56), feld.Width - Formen.PfeilPlatz - Theme.S(8))), feld.Height - Theme.S(10));
                Formen.Fuellen(gr, chip, Theme.S(4), farbe);
                Formen.Umrandung(gr, chip, Theme.S(4), Color.FromArgb(40, 0, 0, 0));
                return;
            }
            var zeile = info.Symbol != null ? g.Rows[e.RowIndex] : null;
            var textFeld = TextFeld(info, e.CellBounds, zeile);
            string symbol = zeile != null ? info.Symbol(zeile) : null;
            if (symbol != null)
            {
                int icon = Theme.S(14);
                Lucide.Zeichne(gr, symbol, new RectangleF(textFeld.Right + Theme.S(6), feld.Y + (feld.Height - icon) / 2f, icon, icon), Theme.Akzent);
            }
            string wert = WertText(zelle);
            string anzeige = info.Anzeige != null ? info.Anzeige(wert) : wert;
            bool platzhalter = info.Anzeige != null && anzeige != wert;
            TextRenderer.DrawText(gr, anzeige, g.DefaultCellStyle.Font, textFeld, platzhalter ? Theme.Gedaempft : Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        // ---------------------------------------------------------------- Bearbeiten

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

        static Color Farbe(DataGridViewCell zelle)
        {
            return zelle.Tag is Color ? (Color)zelle.Tag : Color.Gray;
        }

        void FarbPalette(DataGridViewCell zelle, Rectangle feld)
        {
            var palette = new FarbAuswahl(Farbe(zelle));
            var popup = new Popup(palette);
            palette.Gewaehlt += c => { zelle.Tag = c; popup.Close(); };
            palette.WeitereFarben += () => { popup.Close(); FarbMischen(zelle, feld); };
            AufklappenBei(zelle, popup, feld);
        }

        // «Eigene Farbe…»: freie Wahl am gleichen Ort, «Palette» führt zurück
        void FarbMischen(DataGridViewCell zelle, Rectangle feld)
        {
            var mischer = new FarbMischer(Farbe(zelle));
            var popup = new Popup(mischer);
            mischer.Gewaehlt += c => { zelle.Tag = c; popup.Close(); };
            mischer.Zurueck += () => { popup.Close(); FarbPalette(zelle, feld); };
            AufklappenBei(zelle, popup, feld);
        }

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
                }
            }
            g.Controls.Remove(e);
            e.Dispose();
            if (zelle.RowIndex >= 0) g.InvalidateCell(zelle);
        }
    }
}
