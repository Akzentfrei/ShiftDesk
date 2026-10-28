using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ShiftDesk.Modell
{
    /// <summary>
    /// Alle Eingaben einer Ablöseplanung: Stammdaten (Funktionen, Grade, Personen, Gruppen, Posten,
    /// Essenszeiten) und die Einstellungen der Plan-Seite. Wird als .shiftdesk-Datei gespeichert
    /// (siehe <see cref="KonfigurationsDatei"/>).
    /// </summary>
    /// <remarks>
    /// Die öffentlichen Felder sind das Dateiformat (XML). Reihenfolge und Namen nicht ändern, sonst
    /// lassen sich bestehende Dateien nicht mehr lesen; neue Felder hinten anfügen und bei Bedarf
    /// <see cref="AktuelleVersion"/> erhöhen.
    /// </remarks>
    public class Konfiguration
    {
        /// <summary>
        /// Dateiversion. 0 = von vor den frei definierbaren Funktionen und Graden (Feld fehlt in der Datei),
        /// 2 = vor dem Schalter «Pflicht» (den es nicht mehr gibt, alle Posten sind Pflicht),
        /// 3 = Einheit und Zweck waren noch im Namen zusammengefasst.
        /// </summary>
        public const int AktuelleVersion = 4;

        /// <summary>
        /// Diese Posten gibt es in jeder Datei; sie werden ergänzt, wenn sie fehlen, und lassen sich nicht löschen.
        /// Der erste ist der Posten des Wacht Kdt.
        /// </summary>
        public static readonly string[] Pflichtposten = { "Wacht Kdt", "Presto", "Subito" };

        /// <summary>Version, mit der die Datei geschrieben wurde (siehe <see cref="AktuelleVersion"/>).</summary>
        public int Version;

        /// <summary>Steht auf dem Ausdruck bei «Für».</summary>
        public string Name = "Wachtablösung";

        /// <summary>Steht auf dem Ausdruck bei «Einheit».</summary>
        public string Einheit = "";

        /// <summary>Nur noch zum Lesen älterer Dateien; wird beim Laden in <see cref="Name"/> übernommen.</summary>
        public string Zweck;

        /// <summary>Sprache des Ausdrucks: «de», «fr», «it» oder «en».</summary>
        public string Sprache = "de";

        /// <summary>Beginn des Plans (wird auf die volle Stunde abgerundet).</summary>
        public DateTime Start;

        /// <summary>Ende des Plans (eine angebrochene Stunde zählt mit).</summary>
        public DateTime Ende;

        /// <summary>
        /// Ein Blatt von 00:00 bis 24:00 ohne Datum, das jeden Tag gilt; <see cref="Start"/> und
        /// <see cref="Ende"/> zählen dann nicht.
        /// </summary>
        public bool Tagesplan;

        /// <summary>Die Posten wechseln alle so viele Stunden (ab Mitternacht gerechnet).</summary>
        public int AbloesungStunden = 1;

        /// <summary>
        /// Beginn des Zeitfensters, in dem trotz längerer Ablösung jede Stunde abgelöst wird.
        /// Gleich wie <see cref="StuendlichBis"/> = kein solches Fenster.
        /// </summary>
        public int StuendlichVon;

        /// <summary>Ende des stündlichen Zeitfensters (siehe <see cref="StuendlichVon"/>).</summary>
        public int StuendlichBis;

        /// <summary>Je zwei aufeinanderfolgende Personen mit gleicher Funktion und Gruppe sind wenn möglich zusammen eingeteilt.</summary>
        public bool Zweierteams;

        /// <summary>Höchstens so viele Stunden am Stück auf Posten im Freien (0 = keine Grenze).</summary>
        public int DraussenMaxStunden = 2;

        /// <summary>Funktionen in der Reihenfolge, in der sie im Plan stehen.</summary>
        public List<Funktion> Funktionen = new List<Funktion>();

        /// <summary>Grade, die bei den Personen zur Auswahl stehen.</summary>
        public List<string> Grade = new List<string>();

        /// <summary>Alle Personen; die Reihenfolge bestimmt auch die Zweierteams.</summary>
        public List<Person> Personen = new List<Person>();

        /// <summary>Ruhegruppen.</summary>
        public List<Gruppe> Gruppen = new List<Gruppe>();

        /// <summary>Posten; weiter oben = wichtiger, wenn die Leute nicht für alle reichen.</summary>
        public List<Posten> Posten = new List<Posten>();

        /// <summary>Essensfenster.</summary>
        public List<Essenszeit> Essenszeiten = new List<Essenszeit>();

        // ---------------------------------------------------------------- Regeln

        /// <summary>Ist das einer der <see cref="Pflichtposten"/> (Gross-/Kleinschreibung egal)?</summary>
        public static bool IstPflichtposten(string name)
        {
            return Pflichtposten.Any(p => string.Equals(p, (name ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Ist das die Funktion des Wacht Kdt Stv («Kdt» und «Stv» im Namen)? Der Stv ruht immer dann,
        /// wenn der Wacht Kdt im Dienst ist, und braucht keine eigene Gruppe.
        /// </summary>
        public static bool IstStv(string funktion)
        {
            funktion = funktion ?? "";
            return funktion.IndexOf("Kdt", StringComparison.OrdinalIgnoreCase) >= 0 && funktion.IndexOf("Stv", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Ist das die Funktion des Wacht Kdt («Kdt» im Namen, aber nicht Stv)?</summary>
        public static bool IstKdt(string funktion)
        {
            return (funktion ?? "").IndexOf("Kdt", StringComparison.OrdinalIgnoreCase) >= 0 && !IstStv(funktion);
        }

        /// <summary>
        /// Darf diese Person diesen Posten besetzen? Die Funktion muss erlaubt sein; Fahrer nur auf Fahrerposten.
        /// Auf dem Fahrerposten dürfen auch Nicht-Fahrer sein, aber nur neben einem Fahrer: der erste Platz ist
        /// für einen Fahrer reserviert, das regelt der Planer.
        /// </summary>
        public static bool Darf(Person p, Posten k)
        {
            return k.Erlaubt(p.Funktion) && (!p.Fahrer || k.Typ == PostenTyp.Fahrer);
        }

        /// <summary>Wird diese Person eingeteilt? Nein, wenn ihre Funktion «Im Plan einteilen» ausgeschaltet hat.</summary>
        public bool ImPlan(Person p)
        {
            var f = Funktionen.FirstOrDefault(x => x.Name == p.Funktion);
            return f == null || f.ImPlan;
        }

        /// <summary>
        /// Alle wie viele Stunden wird um diese Stunde abgelöst? Im stündlichen Fenster (inklusive seiner
        /// Endstunde, damit der letzte stündliche Block dort endet) ist es 1, sonst <see cref="AbloesungStunden"/>.
        /// </summary>
        public int TaktUm(int stunde)
        {
            bool stuendlich = StuendlichVon != StuendlichBis && (Zeit.InFenster(stunde, StuendlichVon, StuendlichBis) || stunde == StuendlichBis);
            return stuendlich ? 1 : Math.Max(1, AbloesungStunden);
        }

        /// <summary>Beginnt um diese Stunde eine neue Ablösung? Sonst bleibt jeder auf seinem Posten.</summary>
        public bool NeueAbloesungUm(int stunde)
        {
            int takt = TaktUm(stunde);
            return takt <= 1 || stunde % takt == 0;
        }

        /// <summary>Fehlende <see cref="Pflichtposten"/> mit ihren Standardwerten an ihrer Stelle einfügen.</summary>
        public void PflichtpostenErgaenzen()
        {
            for (int i = 0; i < Pflichtposten.Length; i++)
            {
                string name = Pflichtposten[i];
                if (Posten.Any(p => string.Equals(p.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))) continue;
                Posten neu;
                switch (i)
                {
                    case 0:
                        neu = new Posten { Name = name, Farbe = Color.FromArgb(79, 173, 234).ToArgb(), Anzahl = 1,
                            Funktionen = Funktionen.Where(f => f.Name.IndexOf("Kdt", StringComparison.OrdinalIgnoreCase) >= 0).Select(f => f.Name).ToList() };
                        break;
                    case 1:
                        neu = new Posten { Name = name, Farbe = Color.FromArgb(234, 88, 12).ToArgb(), Anzahl = 2, Typ = PostenTyp.Fahrer, EssenMoeglich = true };
                        break;
                    default:
                        neu = new Posten { Name = name, Farbe = Color.FromArgb(13, 148, 136).ToArgb(), Anzahl = 2, EssenMoeglich = true };
                        break;
                }
                Posten.Insert(Math.Min(i, Posten.Count), neu);
            }
        }
    }
}
