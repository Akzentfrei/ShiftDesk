using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ShiftDesk.Modell;
using ShiftDesk.Oberflaeche.Steuerelemente;
using ShiftDesk.Oberflaeche.Tabellen;

namespace ShiftDesk.Oberflaeche.Seiten
{
    /// <summary>
    /// Seite «Personen»: alle Personen der Wache mit Grad, Funktion, Gruppe und Schalter «Fahrer».
    /// Die Reihenfolge bestimmt die Zweierteams.
    /// </summary>
    public class PersonenSeite : Seite
    {
        /// <summary>Steht beim Wacht Kdt Stv statt einer Gruppe: er ruht, wenn der Wacht Kdt im Dienst ist.</summary>
        public const string GegenteilKdt = "Gegenteil Wacht Kdt";

        /// <summary>Die Personen.</summary>
        public readonly Tabelle Tabelle = new Tabelle();

        readonly Tabelle grade, funktionen;

        /// <param name="grade">Tabelle der Grade (Auswahl und Vorgabe für neue Personen).</param>
        /// <param name="funktionen">Tabelle der Funktionen.</param>
        /// <param name="gruppen">Tabelle der Gruppen.</param>
        public PersonenSeite(Tabelle grade, Tabelle funktionen, Tabelle gruppen)
            : base("Personen", "users",
                "Alle Personen der Wache. Die Reihenfolge änderst du mit den Pfeilen. Bei «Zweierteams» (Plan-Seite) bilden je zwei aufeinanderfolgende Personen mit gleicher Funktion und Gruppe ein Team, das wenn möglich zusammen eingeteilt wird.")
        {
            this.grade = grade;
            this.funktionen = funktionen;
            var t = Tabelle;
            t.Spalte("Grad", "Grad", 50, new SpaltenInfo(SpaltenArt.Auswahl, () => grade.Namen(), Waehlen));
            t.Spalte("Name", "Name", 130, new SpaltenInfo(SpaltenArt.Text) { Symbol = r => Tabelle.Schalter(r, "Fahrer") ? "car" : null }, 120);
            t.Spalte("Funktion", "Funktion", 85, new SpaltenInfo(SpaltenArt.Auswahl, () => funktionen.Namen(), Waehlen));
            t.Spalte("Gruppe", "Gruppe (Ruhezeit)", 85, new SpaltenInfo(SpaltenArt.Auswahl, () => new[] { "" }.Concat(gruppen.Namen()), Keine)
            {
                FesterText = r => Konfiguration.IstStv(Tabelle.Wert(r, "Funktion")) ? GegenteilKdt : null,
            });
            t.Spalte("Fahrer", "Fahrer", 42, new SpaltenInfo(SpaltenArt.Schalter));
            t.Zeilenknoepfe(true);
            t.Erklaeren("Grad", "Wählst du aus der Liste der Seite «Funktionen & Grade».");
            t.Erklaeren("Funktion", "Wählst du aus der Liste der Seite «Funktionen & Grade». Die Funktion bestimmt, welche Posten die Person besetzen darf.");
            t.Erklaeren("Gruppe", "Die Gruppe bestimmt die Ruhezeit (Seite «Gruppen & Ruhezeiten»). Ohne Gruppe hat die Person keine Ruhezeit. Der Wacht Kdt Stv braucht keine: er ruht immer dann, wenn der Wacht Kdt im Dienst ist.");
            t.Erklaeren("Fahrer", "Fahrer haben im Plan nur den Posten vom Typ «Fahrer» (z. B. Presto) und Ruhezeit, sonst nichts, auch keine Essensstunde: sie essen in der Ruhezeit. Im Plan sind sie mit einem Auto markiert.");
            t.LeerHinweis = new[] { "users", "Noch keine Personen", "Mit «Person hinzufügen» oben rechts die Wache erfassen." };
            Aufbauen(t.Karte, Knopf.Mit("Person hinzufügen", "plus", NeuePerson, true));
        }

        static string Waehlen(string s)
        {
            return s == "" ? "Wählen…" : s;
        }

        static string Keine(string s)
        {
            return s == "" ? "Keine" : s;
        }

        /// <summary>Neue Zeile mit Grad, Funktion und Gruppe der letzten Person: das spart beim Erfassen vieler Personen Klicks.</summary>
        void NeuePerson()
        {
            var letzte = Tabelle.Grid.Rows.Cast<DataGridViewRow>().LastOrDefault();
            string grad = letzte != null ? Tabelle.Wert(letzte, "Grad") : grade.Namen().FirstOrDefault() ?? "";
            string funktion = letzte != null ? Tabelle.Wert(letzte, "Funktion") : funktionen.Namen().FirstOrDefault() ?? "";
            string gruppe = letzte != null ? Tabelle.Wert(letzte, "Gruppe") : "";
            Tabelle.Hinzufuegen(grad, "", funktion, gruppe, false);
        }

        public override IEnumerable<Tabelle> Tabellen
        {
            get { return new[] { Tabelle }; }
        }

        public override void Anzeigen(Konfiguration k)
        {
            Tabelle.Grid.Rows.Clear();
            foreach (var p in k.Personen)
                Tabelle.Grid.Rows.Add(p.Grad, p.Name, p.Funktion, p.Gruppe, p.Fahrer);
        }

        public override void Uebernehmen(Konfiguration k)
        {
            foreach (var r in Tabelle.Zeilen)
                k.Personen.Add(new Person
                {
                    Grad = Tabelle.Wert(r, "Grad"),
                    Name = Tabelle.Wert(r, "Name"),
                    Funktion = Tabelle.Wert(r, "Funktion"),
                    Gruppe = Tabelle.Wert(r, "Gruppe"),
                    Fahrer = Tabelle.Schalter(r, "Fahrer"),
                });
        }
    }
}
