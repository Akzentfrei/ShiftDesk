using System.Collections.Generic;
using ShiftDesk.Modell;
using ShiftDesk.Oberflaeche.Steuerelemente;
using ShiftDesk.Oberflaeche.Tabellen;

namespace ShiftDesk.Oberflaeche.Seiten
{
    /// <summary>Seite «Essenszeiten»: in jedem Fenster bekommt jede Person im Dienst eine Stunde zum Essen.</summary>
    public class EssenSeite : Seite
    {
        /// <summary>Die Essenszeiten mit von/bis.</summary>
        public readonly Tabelle Tabelle = new Tabelle();

        public EssenSeite()
            : base("Essenszeiten", "utensils",
                "In jedem Zeitfenster bekommt jede Person im Dienst eine Stunde zum Essen, auf einem Posten mit «Essen». Wer zur Essenszeit in Ruhe ist, isst in der Ruhezeit. Im Plan steht das Besteck oben links im Postenfeld, wenn vor der Ablösung gegessen wird, oben rechts, wenn danach; mitten in der Ruhezeit steht es gross in der Stunde.")
        {
            var t = Tabelle;
            t.Spalte("Name", "Essenszeit", 140, new SpaltenInfo(SpaltenArt.Text));
            t.Spalte("Von", "von", 80, Stunden());
            t.Spalte("Bis", "bis", 80, Stunden());
            t.Zeilenknoepfe(false);
            t.Erklaeren("Von", "Von wann bis wann gegessen wird. Jede Person, die das ganze Fenster im Dienst ist, bekommt darin eine Stunde zum Essen.");
            t.LeerHinweis = new[] { "utensils", "Keine Essenszeiten", "Ohne Essenszeiten plant ShiftDesk keine Essensstunden ein." };
            Aufbauen(t.Karte, Knopf.Mit("Essenszeit hinzufügen", "plus", () => t.Hinzufuegen("", Zeit.Text(11), Zeit.Text(13)), true));
        }

        public override IEnumerable<Tabelle> Tabellen
        {
            get { return new[] { Tabelle }; }
        }

        public override void Anzeigen(Konfiguration k)
        {
            Tabelle.Grid.Rows.Clear();
            foreach (var e in k.Essenszeiten)
                Tabelle.Grid.Rows.Add(e.Name, Zeit.Text(e.Von), Zeit.Text(e.Bis));
        }

        public override void Uebernehmen(Konfiguration k)
        {
            foreach (var r in Tabelle.Zeilen)
                k.Essenszeiten.Add(new Essenszeit { Name = Tabelle.Wert(r, "Name"), Von = Tabelle.Stunde(r, "Von"), Bis = Tabelle.Stunde(r, "Bis") });
        }
    }
}
