using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ShiftDesk.Modell
{
    /// <summary>Vorgaben für neue Dateien («Neu») und das mitgelieferte Beispiel («Beispiel laden»).</summary>
    public static class Vorlagen
    {
        /// <summary>Grade, die eine neue Datei schon kennt.</summary>
        public static readonly string[] StandardGrade = { "Rekr", "Sdt", "Gfr", "Obgfr", "Kpl", "Wm", "Obwm" };

        /// <summary>Funktionen, die eine neue Datei schon kennt (jedes Mal eine neue Liste).</summary>
        public static List<Funktion> StandardFunktionen()
        {
            return new List<Funktion>
            {
                new Funktion { Name = "Wacht Kdt" },
                new Funktion { Name = "Wacht Kdt Stv" },
                new Funktion { Name = "Wache" },
                new Funktion { Name = "Reserve", ImPlan = false },
            };
        }

        /// <summary>Leere Planung für heute: nur Standard-Funktionen, -Grade und die Pflichtposten.</summary>
        public static Konfiguration Neu()
        {
            var k = new Konfiguration { Start = DateTime.Today, Ende = DateTime.Today.AddDays(1),
                Funktionen = StandardFunktionen(), Grade = StandardGrade.ToList() };
            k.PflichtpostenErgaenzen();
            return k;
        }

        /// <summary>
        /// Vollständiges Beispiel einer Wache mit erfundenen Namen: Wacht Kdt und Stv, drei Gruppen mit
        /// je fünf Wachen und einem Fahrer, neun Posten und drei Essenszeiten.
        /// </summary>
        public static Konfiguration Beispiel()
        {
            var k = new Konfiguration();
            k.Name = "Wachtablösung";
            k.Start = DateTime.Today;
            k.Ende = DateTime.Today.AddDays(1);
            k.Grade = StandardGrade.ToList();
            k.Zweierteams = true;
            k.Funktionen = StandardFunktionen();

            k.Gruppen.Add(new Gruppe { Name = "Kdt", RuheVon = 3, RuheBis = 15 });
            k.Gruppen.Add(new Gruppe { Name = "Gruppe 1", RuheVon = 23, RuheBis = 7 });
            k.Gruppen.Add(new Gruppe { Name = "Gruppe 2", RuheVon = 7, RuheBis = 15 });
            k.Gruppen.Add(new Gruppe { Name = "Gruppe 3", RuheVon = 15, RuheBis = 23 });

            k.Personen.Add(new Person { Grad = "Wm", Name = "Meier", Funktion = "Wacht Kdt", Gruppe = "Kdt" });
            k.Personen.Add(new Person { Grad = "Rekr", Name = "Keller", Funktion = "Wacht Kdt Stv" });
            string[][] gruppen =
            {
                new[] { "Frei", "Huber", "Brunner", "Gerber", "Baumann" },
                new[] { "Fischer", "Weber", "Graf", "Moser", "Kunz" },
                new[] { "Steiner", "Zürcher", "Wyss", "Bühler", "Egli" },
            };
            for (int g = 0; g < gruppen.Length; g++)
                foreach (string name in gruppen[g])
                    k.Personen.Add(new Person { Grad = "Rekr", Name = name, Funktion = "Wache", Gruppe = "Gruppe " + (g + 1) });
            // Fahrer: je einer pro Gruppe, so sind immer zwei im Dienst
            string[] fahrer = { "Lüthi", "Schmid", "Roth" };
            for (int g = 0; g < fahrer.Length; g++)
                k.Personen.Add(new Person { Grad = "Rekr", Name = fahrer[g], Funktion = "Wache", Gruppe = "Gruppe " + (g + 1), Fahrer = true });

            var wache = new List<string> { "Wache" };
            k.Posten.Add(new Posten { Name = "Wacht Kdt", Farbe = Color.FromArgb(79, 173, 234).ToArgb(), Anzahl = 1, Funktionen = new List<string> { "Wacht Kdt", "Wacht Kdt Stv" } });
            k.Posten.Add(new Posten { Name = "Presto", Farbe = Color.FromArgb(234, 88, 12).ToArgb(), Anzahl = 2, Typ = PostenTyp.Fahrer, EssenMoeglich = true, Funktionen = wache.ToList() });
            k.Posten.Add(new Posten { Name = "Subito", Farbe = Color.FromArgb(13, 148, 136).ToArgb(), Anzahl = 2, EssenMoeglich = true, Funktionen = wache.ToList() });
            k.Posten.Add(new Posten { Name = "Kdo", Farbe = Color.FromArgb(46, 112, 190).ToArgb(), Anzahl = 1, Funktionen = wache.ToList() });
            k.Posten.Add(new Posten { Name = "Haupteingang", Farbe = Color.FromArgb(178, 34, 34).ToArgb(), Anzahl = 2, OffenVon = 7, OffenBis = 22, Draussen = true, Funktionen = wache.ToList() });
            k.Posten.Add(new Posten { Name = "Amiti", Farbe = Color.FromArgb(76, 168, 80).ToArgb(), Anzahl = 2, OffenVon = 7, OffenBis = 23, Funktionen = wache.ToList() });
            k.Posten.Add(new Posten { Name = "Patrouille", Farbe = Color.FromArgb(112, 48, 160).ToArgb(), Anzahl = 2, Draussen = true, Funktionen = wache.ToList() });
            k.Posten.Add(new Posten { Name = "Reserve", Farbe = Color.FromArgb(247, 193, 67).ToArgb(), Anzahl = 1, EssenMoeglich = true, Funktionen = wache.ToList() });
            k.Posten.Add(new Posten { Name = "Pikett", Farbe = Color.FromArgb(166, 166, 166).ToArgb(), Anzahl = 0, Typ = PostenTyp.Auffang, EssenMoeglich = true, Funktionen = wache.ToList() });

            k.Essenszeiten.Add(new Essenszeit { Name = "Frühstück", Von = 6, Bis = 10 });
            k.Essenszeiten.Add(new Essenszeit { Name = "Mittagessen", Von = 10, Bis = 14 });
            k.Essenszeiten.Add(new Essenszeit { Name = "Abendessen", Von = 16, Bis = 20 });
            return k;
        }
    }
}
