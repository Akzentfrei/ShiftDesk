using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ShiftDesk.Modell;
using ShiftDesk.Oberflaeche.Steuerelemente;
using ShiftDesk.Oberflaeche.Tabellen;

namespace ShiftDesk.Oberflaeche.Seiten
{
    /// <summary>
    /// Seite «Posten»: Farbe, Anzahl Personen, erlaubte Funktionen, Öffnungszeit, Typ, Draussen und Essen.
    /// Die Reihenfolge entscheidet, welche Posten zuerst leer bleiben; die Pflichtposten lassen sich nicht löschen.
    /// </summary>
    public class PostenSeite : Seite
    {
        static readonly string[] TypListe = { "Normal", "Auffang", "Fahrer" };

        /// <summary>Die Posten. Die Farbe steht als <see cref="Color"/>, die Funktionen als <c>List&lt;string&gt;</c> im Zellen-Tag.</summary>
        public readonly Tabelle Tabelle = new Tabelle();

        /// <param name="funktionen">Tabelle der Funktionen (Auswahl bei «Funktionen»).</param>
        public PostenSeite(Tabelle funktionen)
            : base("Posten", "map-pin",
                "Alle Posten sollen immer voll besetzt sein. Reichen die Leute nicht, entscheidet die Reihenfolge: unten bleibt zuerst leer. Wacht Kdt, Presto und Subito gibt es immer; Wacht Kdt und Stv bleiben möglichst am Stück auf ihrem Posten.")
        {
            var t = Tabelle;
            t.Spalte("Name", "Posten", 105, new SpaltenInfo(SpaltenArt.Text) { Symbol = r => Tabelle.Schalter(r, "Draussen") ? "snowflake" : null }, 120);
            t.Spalte("Farbe", "Farbe", 55, new SpaltenInfo(SpaltenArt.Farbe), 74);
            t.Spalte("Anzahl", "Personen", 48, new SpaltenInfo(SpaltenArt.Zahl));
            t.Spalte("Funktionen", "Funktionen", 125, new SpaltenInfo(SpaltenArt.Mehrfach, () => funktionen.Namen(), s => s == "" ? "Alle" : s), 116);
            t.Spalte("Von", "Offen von", 58, Stunden());
            t.Spalte("Bis", "Offen bis", 58, Stunden());
            t.Spalte("Typ", "Typ", 62, new SpaltenInfo(SpaltenArt.Auswahl, () => TypListe) { Fest = true }, 112);
            t.Spalte("Draussen", "Draussen", 42, new SpaltenInfo(SpaltenArt.Schalter));
            t.Spalte("Essen", "Essen", 42, new SpaltenInfo(SpaltenArt.Schalter));
            t.Geschuetzt = zeile => Konfiguration.IstPflichtposten(Tabelle.Wert(t.Grid.Rows[zeile], "Name"));
            t.Zeilenknoepfe(true);
            t.Erklaeren("Funktionen", "Wer den Posten besetzen darf. «Alle» = jede Funktion, die im Plan eingeteilt wird.");
            t.Erklaeren("Von", "Von wann bis wann der Posten besetzt ist, auch über Mitternacht. Gleiche Zeit bei «von» und «bis» = immer offen.");
            t.Erklaeren("Typ", "«Normal»: gewöhnlicher Posten. «Auffang» (z. B. Pikett): bekommt alle übrigen Leute. «Fahrer» (z. B. Presto): der erste Platz ist für einen Fahrer, die weiteren Plätze bekommen Fahrer, wenn welche da sind, sonst andere Leute; Fahrer selbst sind nur dort oder in Ruhe.");
            t.Erklaeren("Draussen", "Posten im Freien, im Plan mit Schneeflocke. Niemand bleibt länger am Stück draussen als auf der Plan-Seite bei «Am Stück draussen» eingestellt.");
            t.Erklaeren("Essen", "Auf diesem Posten kann man essen: hier bekommen die Leute ihre Essensstunde (Seite «Essenszeiten»).");
            Aufbauen(t.Karte, Knopf.Mit("Posten hinzufügen", "plus", NeuerPosten, true));
        }

        void NeuerPosten()
        {
            int i = Tabelle.Hinzufuegen("", "", "1", "", Zeit.Text(0), Zeit.Text(0), "Normal", false, false);
            Tabelle.Grid.Rows[i].Cells["Farbe"].Tag = Color.FromArgb(148, 163, 184);
            Tabelle.Grid.Rows[i].Cells["Funktionen"].Tag = new List<string>();
        }

        /// <summary>Eine umbenannte Funktion in den erlaubten Funktionen aller Posten nachführen.</summary>
        public void FunktionUmbenennen(string alt, string neu)
        {
            foreach (DataGridViewRow r in Tabelle.Grid.Rows)
            {
                var liste = r.Cells["Funktionen"].Tag as List<string>;
                if (liste != null && liste.Contains(alt))
                    r.Cells["Funktionen"].Tag = liste.Select(x => x == alt ? neu : x).ToList();
            }
        }

        public override IEnumerable<Tabelle> Tabellen
        {
            get { return new[] { Tabelle }; }
        }

        public override void Anzeigen(Konfiguration k)
        {
            var g = Tabelle.Grid;
            g.Rows.Clear();
            foreach (var p in k.Posten)
            {
                int i = g.Rows.Add(p.Name, "", p.Anzahl.ToString(), "", Zeit.Text(p.OffenVon), Zeit.Text(p.OffenBis),
                    p.Typ == PostenTyp.Auffang ? "Auffang" : p.Typ == PostenTyp.Fahrer ? "Fahrer" : "Normal", p.Draussen, p.EssenMoeglich);
                g.Rows[i].Cells["Farbe"].Tag = Color.FromArgb(p.Farbe);
                g.Rows[i].Cells["Funktionen"].Tag = p.Funktionen.ToList();
            }
        }

        public override void Uebernehmen(Konfiguration k)
        {
            foreach (var r in Tabelle.Zeilen)
            {
                int anzahl;
                int.TryParse(Tabelle.Wert(r, "Anzahl"), out anzahl);
                PostenTyp typ;
                if (!Enum.TryParse(Tabelle.Wert(r, "Typ"), out typ)) typ = PostenTyp.Normal;
                object farbe = r.Cells["Farbe"].Tag;
                k.Posten.Add(new Posten
                {
                    Name = Tabelle.Wert(r, "Name"),
                    Farbe = (farbe is Color ? (Color)farbe : Color.Gray).ToArgb(),
                    Anzahl = Math.Max(0, anzahl),
                    OffenVon = Tabelle.Stunde(r, "Von"),
                    OffenBis = Tabelle.Stunde(r, "Bis"),
                    Typ = typ,
                    EssenMoeglich = Tabelle.Schalter(r, "Essen"),
                    Draussen = Tabelle.Schalter(r, "Draussen"),
                    Funktionen = (r.Cells["Funktionen"].Tag as List<string> ?? new List<string>()).ToList(),
                });
            }
        }
    }
}
