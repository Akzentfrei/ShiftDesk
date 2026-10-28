using System.Collections.Generic;
using ShiftDesk.Modell;
using ShiftDesk.Oberflaeche.Steuerelemente;
using ShiftDesk.Oberflaeche.Tabellen;

namespace ShiftDesk.Oberflaeche.Seiten
{
    /// <summary>Seite «Gruppen &amp; Ruhezeiten»: jede Gruppe ruht jeden Tag im gleichen Zeitfenster.</summary>
    public class GruppenSeite : Seite
    {
        /// <summary>Die Gruppen mit Ruhe von/bis.</summary>
        public readonly Tabelle Tabelle = new Tabelle();

        public GruppenSeite()
            : base("Gruppen & Ruhezeiten", "moon",
                "Jede Gruppe ruht jeden Tag im gleichen Zeitfenster. Auch der Wacht Kdt braucht eine Gruppe; der Wacht Kdt Stv ruht automatisch, wenn der Wacht Kdt im Dienst ist.")
        {
            var t = Tabelle;
            t.Spalte("Name", "Gruppe", 140, new SpaltenInfo(SpaltenArt.Text));
            t.Spalte("Von", "Ruhe von", 80, Stunden());
            t.Spalte("Bis", "Ruhe bis", 80, Stunden());
            t.Zeilenknoepfe(false);
            t.Erklaeren("Von", "Von wann bis wann die Gruppe ruht, auch über Mitternacht (z. B. 23:00 bis 07:00). Gleiche Zeit bei «von» und «bis» bedeutet keine Ruhezeit.");
            t.LeerHinweis = new[] { "moon", "Noch keine Gruppen", "Mit «Gruppe hinzufügen» oben rechts eine erfassen." };
            Aufbauen(t.Karte, Knopf.Mit("Gruppe hinzufügen", "plus", () => t.Hinzufuegen("", Zeit.Text(23), Zeit.Text(7)), true));
        }

        public override IEnumerable<Tabelle> Tabellen
        {
            get { return new[] { Tabelle }; }
        }

        public override void Anzeigen(Konfiguration k)
        {
            Tabelle.Grid.Rows.Clear();
            foreach (var g in k.Gruppen)
                Tabelle.Grid.Rows.Add(g.Name, Zeit.Text(g.RuheVon), Zeit.Text(g.RuheBis));
        }

        public override void Uebernehmen(Konfiguration k)
        {
            foreach (var r in Tabelle.Zeilen)
                k.Gruppen.Add(new Gruppe { Name = Tabelle.Wert(r, "Name"), RuheVon = Tabelle.Stunde(r, "Von"), RuheBis = Tabelle.Stunde(r, "Bis") });
        }
    }
}
