using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Dialoge;

namespace ShiftDesk.Oberflaeche.Tabellen
{
    // Zeilen hinzufügen, entfernen und verschieben; Werte aus Zeilen lesen
    public partial class Tabelle
    {
        /// <summary>Zeilen, die sich nicht löschen lassen (z. B. die Pflichtposten); null = alle löschbar.</summary>
        public Func<int, bool> Geschuetzt;

        bool IstGeschuetzt(int zeile)
        {
            return Geschuetzt != null && zeile >= 0 && zeile < g.Rows.Count && Geschuetzt(zeile);
        }

        /// <summary>
        /// Knöpfe am Ende jeder Zeile: Verschieben (wo die Reihenfolge zählt) und Entfernen.
        /// Tastatur: Entf entfernt die markierten Zeilen, Alt+Pfeil verschiebt die aktuelle Zeile.
        /// </summary>
        /// <param name="verschiebbar">Mit Pfeilen zum Verschieben (die Reihenfolge zählt, z. B. bei Personen und Posten).</param>
        public void Zeilenknoepfe(bool verschiebbar)
        {
            var knoepfe = new List<ZeilenKnopf>();
            if (verschiebbar)
            {
                knoepfe.Add(new ZeilenKnopf("chevron-up", i => Verschieben(i, -1), i => i > 0));
                knoepfe.Add(new ZeilenKnopf("chevron-down", i => Verschieben(i, 1), i => i < g.RowCount - 1));
            }
            knoepfe.Add(new ZeilenKnopf("trash-2", Entfernen, i => !IstGeschuetzt(i)) { Gefahr = true });
            Aktionen(knoepfe.ToArray());
            g.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete)
                {
                    MarkierteEntfernen();
                    e.Handled = true;
                }
                else if (verschiebbar && e.Alt && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) && g.CurrentCell != null)
                {
                    Verschieben(g.CurrentCell.RowIndex, e.KeyCode == Keys.Up ? -1 : 1);
                    e.Handled = true;
                }
            };
        }

        /// <summary>Hängt eine Zeile an, markiert sie und öffnet ihren Namen zum Tippen.</summary>
        /// <param name="werte">Werte der Zellen in Spaltenreihenfolge.</param>
        /// <returns>Index der neuen Zeile (z. B. um noch Tags zu setzen).</returns>
        public int Hinzufuegen(params object[] werte)
        {
            Schliessen(true);
            Benutzt = true;
            int i = g.Rows.Add(werte);
            int name = g.Columns.Contains("Name") ? g.Columns["Name"].Index : 0;
            g.ClearSelection();
            g.CurrentCell = g.Rows[i].Cells[name];
            g.Rows[i].Selected = true;
            // erst nach dem Klick auf den Knopf: sonst nimmt der Knopf dem neuen Eingabefeld gleich den Fokus
            g.BeginInvoke(new Action(() => Oeffnen(name, i)));
            return i;
        }

        /// <summary>Entfernt die markierten Zeilen (ohne die geschützten), nach Rückfrage.</summary>
        public void MarkierteEntfernen()
        {
            Schliessen(true);
            var zeilen = g.SelectedCells.Cast<DataGridViewCell>().Select(c => c.RowIndex).Distinct().OrderByDescending(i => i).ToList();
            if (zeilen.Count == 0 && g.CurrentCell != null) zeilen.Add(g.CurrentCell.RowIndex);
            zeilen = zeilen.Where(i => !IstGeschuetzt(i)).ToList();
            if (zeilen.Count == 0 || !LoeschenBestaetigt(zeilen)) return;
            foreach (int i in zeilen) g.Rows.RemoveAt(i);
        }

        /// <summary>Entfernt eine Zeile, nach Rückfrage.</summary>
        public void Entfernen(int zeile)
        {
            Schliessen(true);
            if (zeile < 0 || zeile >= g.Rows.Count || IstGeschuetzt(zeile) || !LoeschenBestaetigt(new List<int> { zeile })) return;
            g.Rows.RemoveAt(zeile);
        }

        /// <summary>Rückfrage vor dem Löschen; mit gedrückter Ctrl-Taste wird ohne Nachfrage gelöscht.</summary>
        bool LoeschenBestaetigt(List<int> zeilen)
        {
            if ((Control.ModifierKeys & Keys.Control) != 0) return true;
            string name = zeilen.Count == 1 ? Wert(g.Rows[zeilen[0]], "Name") : "";
            string titel = zeilen.Count > 1 ? zeilen.Count + " Zeilen löschen?" : name != "" ? "«" + name + "» löschen?" : "Diese Zeile löschen?";
            return Rueckfrage.Fragen(g.FindForm(), titel, "Tipp: Mit gedrückter Ctrl-Taste wird ohne Nachfrage gelöscht.", "Löschen", "trash-2");
        }

        /// <summary>Tauscht eine Zeile mit ihrer Nachbarin, mit Werten und Tags (Farbe, Funktionen).</summary>
        /// <param name="i">Die Zeile.</param>
        /// <param name="richtung">-1 = nach oben, 1 = nach unten.</param>
        public void Verschieben(int i, int richtung)
        {
            int j = i + richtung;
            if (i < 0 || i >= g.Rows.Count || j < 0 || j >= g.Rows.Count) return;
            int spalte = g.CurrentCell != null ? g.CurrentCell.ColumnIndex : 0;
            Schliessen(true);
            Benutzt = true;
            for (int c = 0; c < g.Columns.Count; c++)
            {
                DataGridViewCell a = g.Rows[i].Cells[c], b = g.Rows[j].Cells[c];
                object wert = a.Value, tag = a.Tag;
                a.Value = b.Value;
                a.Tag = b.Tag;
                b.Value = wert;
                b.Tag = tag;
            }
            g.ClearSelection();
            g.CurrentCell = g.Rows[j].Cells[spalte];
            g.Rows[j].Selected = true;
            g.Invalidate();
        }

        // ---------------------------------------------------------------- Werte lesen

        /// <summary>Alle Zeilen mit einem Namen (leere Zeilen zählen nicht).</summary>
        public IEnumerable<DataGridViewRow> Zeilen
        {
            get { return g.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow && Wert(r, "Name") != ""); }
        }

        /// <summary>Die Namen aller Zeilen, ohne Doppelte (z. B. als Auswahl in einer anderen Tabelle).</summary>
        public List<string> Namen()
        {
            return Zeilen.Select(r => Wert(r, "Name")).Distinct().ToList();
        }

        /// <summary>Ersetzt in einer Spalte jeden Wert <paramref name="alt"/> durch <paramref name="neu"/> (Umbenennen nachführen).</summary>
        public void Ersetzen(string spalte, string alt, string neu)
        {
            foreach (DataGridViewRow r in g.Rows)
                if (Wert(r, spalte) == alt) r.Cells[spalte].Value = neu;
        }

        /// <summary>Text einer Zelle ohne Leerzeichen am Rand ("" statt null).</summary>
        public static string Wert(DataGridViewRow r, string spalte)
        {
            object v = r.Cells[spalte].Value;
            return v == null ? "" : v.ToString().Trim();
        }

        /// <summary>Stellung eines Schalters.</summary>
        public static bool Schalter(DataGridViewRow r, string spalte)
        {
            object v = r.Cells[spalte].Value;
            return v is bool && (bool)v;
        }

        /// <summary>Stunde aus einer Uhrzeit-Zelle («07:00» → 7), 0 wenn unlesbar.</summary>
        public static int Stunde(DataGridViewRow r, string spalte)
        {
            string s = Wert(r, spalte);
            int h;
            return s.Length >= 2 && int.TryParse(s.Substring(0, 2), out h) && h >= 0 && h < 24 ? h : 0;
        }
    }
}
