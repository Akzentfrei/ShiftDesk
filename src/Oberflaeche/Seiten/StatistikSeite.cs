using System;
using System.Collections.Generic;
using System.Drawing;
using ShiftDesk.Modell;
using ShiftDesk.Oberflaeche.Tabellen;
using ShiftDesk.Planung;

namespace ShiftDesk.Oberflaeche.Seiten
{
    /// <summary>Seite «Statistik»: wie viele Stunden jede Person auf welchem Posten eingeteilt ist.</summary>
    public class StatistikSeite : Seite
    {
        readonly Tabelle tabelle = new Tabelle(true);

        public StatistikSeite()
            : base("Statistik", "chart-column",
                "Wie viele Stunden jede Person auf welchem Posten eingeteilt ist. So siehst du auf einen Blick, ob die Verteilung gerecht ist.")
        {
            tabelle.LeerHinweis = new[] { "chart-column", "Noch kein Plan erstellt", "Auf der Seite «Plan» auf «Plan erstellen» klicken." };
            Aufbauen(tabelle.Karte);
        }

        public override IEnumerable<Tabelle> Tabellen
        {
            get { return new[] { tabelle }; }
        }

        /// <summary>Neue Eingaben: bis zum nächsten Plan ist die Statistik leer.</summary>
        public override void Anzeigen(Konfiguration k)
        {
            tabelle.Grid.Rows.Clear();
            tabelle.Grid.Columns.Clear();
        }

        /// <summary>Zeigt die Statistik eines neu erstellten Plans: eine Spalte pro Posten, dazu Essen und Dienststunden.</summary>
        public void Zeigen(Plan plan)
        {
            var g = tabelle.Grid;
            g.Rows.Clear();
            g.Columns.Clear();
            Func<SpaltenInfo> text = () => new SpaltenInfo(SpaltenArt.Anzeige);
            Func<Color?, SpaltenInfo> zahl = farbe => new SpaltenInfo(SpaltenArt.Anzeige) { Fest = true, Farbe = farbe };
            tabelle.Spalte("Grad", "Grad", 50, text());
            tabelle.Spalte("Name", "Name", 150, text(), 110);
            tabelle.Spalte("Gruppe", "Gruppe", 80, text(), 80);
            for (int k = 0; k < plan.Cfg.Posten.Count; k++)
                tabelle.Spalte("P" + k, plan.Cfg.Posten[k].Name, 60, zahl(Color.FromArgb(plan.Cfg.Posten[k].Farbe)));
            tabelle.Spalte("Essen", "Essen", 60, zahl(null));
            tabelle.Spalte("Total", "Dienst-Std.", 60, zahl(null));

            for (int r = 0; r < plan.Zeilen.Count; r++)
            {
                var p = plan.Zeilen[r];
                var werte = new List<object> { p.Grad, p.Name, Konfiguration.IstStv(p.Funktion) ? PersonenSeite.GegenteilKdt : p.Gruppe };
                int total = 0;
                for (int k = 0; k < plan.Cfg.Posten.Count; k++)
                {
                    int c = plan.StundenAuf(r, k);
                    total += c;
                    werte.Add(c == 0 ? "–" : c.ToString());
                }
                int essen = plan.Essensstunden(r);
                werte.Add(essen == 0 ? "–" : essen.ToString());
                werte.Add(total);
                g.Rows.Add(werte.ToArray());
            }
            tabelle.Zuruecksetzen();
        }
    }
}
