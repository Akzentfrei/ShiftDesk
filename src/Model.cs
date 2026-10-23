using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace ShiftDesk
{
    // Wachtkdt gibt es nur noch, damit ältere Dateien gelesen werden können
    public enum PostenTyp { Normal, Wachtkdt, Auffang }

    public class Funktion
    {
        public string Name = "";
        public bool ImPlan = true;   // false = wird nicht eingeteilt (z. B. Reserve)
    }

    public class Person
    {
        public string Grad = "";
        public string Name = "";
        public string Funktion = "";
        public string Gruppe = "";
    }

    // Eine Gruppe ruht jeden Tag im selben Zeitfenster (RuheVon bis RuheBis, Stunden 0-23).
    public class Gruppe
    {
        public string Name = "";
        public int RuheVon;
        public int RuheBis;

        public bool RuhtUm(int stunde)
        {
            return RuheVon != RuheBis && Zeit.InFenster(stunde, RuheVon, RuheBis);
        }
    }

    public class Posten
    {
        public string Name = "";
        public int Farbe = Color.LightGray.ToArgb();
        public int Anzahl = 1;
        public int OffenVon;
        public int OffenBis;
        public PostenTyp Typ = PostenTyp.Normal;
        public bool EssenMoeglich;
        public bool AmStueck;                               // gleiche Person bleibt möglichst auf dem Posten
        public bool Draussen;                               // im Freien: niemand soll zu lange am Stück draussen sein
        public List<string> Funktionen = new List<string>(); // leer = alle Funktionen

        public bool OffenUm(int stunde)
        {
            return Zeit.InFenster(stunde, OffenVon, OffenBis);
        }

        public bool Durchgehend
        {
            get { return OffenVon == OffenBis; }
        }

        public bool Erlaubt(string funktion)
        {
            return Funktionen.Count == 0 || Funktionen.Contains(funktion);
        }
    }

    public class Essenszeit
    {
        public string Name = "";
        public int Von;
        public int Bis;
    }

    public class Konfiguration
    {
        public static readonly string[] StandardGrade =
            { "Rekr", "Sdt", "Gfr", "Obgfr", "Kpl", "Wm", "Obwm", "Fw", "Four", "Hptfw", "Adj Uof", "Lt", "Oblt", "Hptm" };

        // Dateiversion: 0 = von vor den frei definierbaren Funktionen und Graden (Feld fehlt in der Datei)
        public const int AktuelleVersion = 2;
        public int Version;

        public string Name = "Wachtablösung";
        // nur noch zum Lesen älterer Dateien, werden in Name übernommen
        public string Einheit;
        public string Zweck;
        public DateTime Start;
        public DateTime Ende;
        public int AbloesungStunden = 1;   // Posten wechseln alle X Stunden (ab Mitternacht gerechnet)
        public int DraussenMaxStunden = 2; // höchstens so viele Stunden am Stück auf Posten im Freien (0 = keine Grenze)
        public List<Funktion> Funktionen = new List<Funktion>();
        public List<string> Grade = new List<string>();
        public List<Person> Personen = new List<Person>();
        public List<Gruppe> Gruppen = new List<Gruppe>();
        public List<Posten> Posten = new List<Posten>();
        public List<Essenszeit> Essenszeiten = new List<Essenszeit>();

        public bool ImPlan(Person p)
        {
            var f = Funktionen.FirstOrDefault(x => x.Name == p.Funktion);
            return f == null || f.ImPlan;
        }

        public void Speichern(string pfad)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pfad)));
            var ser = new XmlSerializer(typeof(Konfiguration));
            Einheit = Zweck = null;
            Version = AktuelleVersion;
            using (var fs = File.Create(pfad))
                ser.Serialize(fs, this);
        }

        public static Konfiguration Laden(string pfad)
        {
            var ser = new XmlSerializer(typeof(Konfiguration));
            Konfiguration k;
            using (var fs = File.OpenRead(pfad))
                k = (Konfiguration)ser.Deserialize(fs);
            if (!string.IsNullOrEmpty(k.Zweck) || !string.IsNullOrEmpty(k.Einheit))
                k.Name = string.Join(" ", new[] { k.Zweck, k.Einheit }.Where(s => !string.IsNullOrEmpty(s)));
            k.Einheit = k.Zweck = null;
            if (k.Version < 2) k.AeltereVersionUebernehmen();
            k.Version = AktuelleVersion;
            return k;
        }

        // Dateien von vor den frei definierbaren Funktionen und Graden umwandeln.
        // Nur für Dateien ohne Versionsfeld: eine neue Datei darf absichtlich ohne Funktionen oder Grade sein.
        void AeltereVersionUebernehmen()
        {
            if (Funktionen.Count == 0)
            {
                Funktionen.Add(new Funktion { Name = "Wacht Kdt" });
                Funktionen.Add(new Funktion { Name = "Wache" });
                foreach (var p in Personen)
                    p.Funktion = p.Funktion == "Wachtkdt" ? "Wacht Kdt" : "Wache";
                foreach (var p in Posten)
                {
                    if (p.Typ == PostenTyp.Wachtkdt)
                    {
                        p.Typ = PostenTyp.Normal;
                        p.AmStueck = true;
                        p.Funktionen = new List<string> { "Wacht Kdt" };
                    }
                    else if (p.Funktionen.Count == 0)
                        p.Funktionen = new List<string> { "Wache" };
                }
            }
            if (Grade.Count == 0)
                Grade = StandardGrade.Concat(Personen.Select(p => p.Grad)).Where(g => g != "").Distinct().ToList();
        }

        public static Konfiguration Beispiel()
        {
            var k = new Konfiguration();
            k.Name = "Wachtablösung";
            k.Start = DateTime.Today;
            k.Ende = DateTime.Today.AddDays(1);
            k.Grade = StandardGrade.ToList();

            k.Funktionen.Add(new Funktion { Name = "Wacht Kdt" });
            k.Funktionen.Add(new Funktion { Name = "Wacht Kdt Stv" });
            k.Funktionen.Add(new Funktion { Name = "Wache" });
            k.Funktionen.Add(new Funktion { Name = "Reserve", ImPlan = false });

            k.Gruppen.Add(new Gruppe { Name = "Kdt", RuheVon = 3, RuheBis = 15 });
            k.Gruppen.Add(new Gruppe { Name = "Kdt Stv", RuheVon = 15, RuheBis = 3 });
            k.Gruppen.Add(new Gruppe { Name = "Gruppe 1", RuheVon = 23, RuheBis = 7 });
            k.Gruppen.Add(new Gruppe { Name = "Gruppe 2", RuheVon = 7, RuheBis = 15 });
            k.Gruppen.Add(new Gruppe { Name = "Gruppe 3", RuheVon = 15, RuheBis = 23 });

            // erfundene Namen
            k.Personen.Add(new Person { Grad = "Wm", Name = "Meier", Funktion = "Wacht Kdt", Gruppe = "Kdt" });
            k.Personen.Add(new Person { Grad = "Rekr", Name = "Keller", Funktion = "Wacht Kdt Stv", Gruppe = "Kdt Stv" });
            string[][] gruppen =
            {
                new[] { "Frei", "Huber", "Brunner", "Gerber", "Baumann" },
                new[] { "Fischer", "Weber", "Graf", "Moser", "Kunz" },
                new[] { "Steiner", "Zürcher", "Wyss", "Bühler", "Egli" },
            };
            for (int g = 0; g < gruppen.Length; g++)
                foreach (string name in gruppen[g])
                    k.Personen.Add(new Person { Grad = "Rekr", Name = name, Funktion = "Wache", Gruppe = "Gruppe " + (g + 1) });

            var wache = new List<string> { "Wache" };
            k.Posten.Add(new Posten { Name = "Wacht Kdt", Farbe = Color.FromArgb(79, 173, 234).ToArgb(), Anzahl = 1, AmStueck = true, Funktionen = new List<string> { "Wacht Kdt", "Wacht Kdt Stv" } });
            k.Posten.Add(new Posten { Name = "Kdo", Farbe = Color.FromArgb(46, 112, 190).ToArgb(), Anzahl = 1, Funktionen = wache.ToList() });
            k.Posten.Add(new Posten { Name = "Haupteingang", Farbe = Color.FromArgb(178, 34, 34).ToArgb(), Anzahl = 2, OffenVon = 7, OffenBis = 22, Draussen = true, Funktionen = wache.ToList() });
            k.Posten.Add(new Posten { Name = "Amiti", Farbe = Color.FromArgb(76, 168, 80).ToArgb(), Anzahl = 2, OffenVon = 7, OffenBis = 23, Funktionen = wache.ToList() });
            k.Posten.Add(new Posten { Name = "Patrouille", Farbe = Color.FromArgb(112, 48, 160).ToArgb(), Anzahl = 2, Draussen = true, Funktionen = wache.ToList() });
            k.Posten.Add(new Posten { Name = "Reserve", Farbe = Color.FromArgb(247, 193, 67).ToArgb(), Anzahl = 2, EssenMoeglich = true, Funktionen = wache.ToList() });
            k.Posten.Add(new Posten { Name = "Pikett", Farbe = Color.FromArgb(166, 166, 166).ToArgb(), Anzahl = 0, Typ = PostenTyp.Auffang, EssenMoeglich = true, Funktionen = wache.ToList() });

            k.Essenszeiten.Add(new Essenszeit { Name = "Frühstück", Von = 6, Bis = 10 });
            k.Essenszeiten.Add(new Essenszeit { Name = "Mittagessen", Von = 10, Bis = 14 });
            k.Essenszeiten.Add(new Essenszeit { Name = "Abendessen", Von = 16, Bis = 20 });
            return k;
        }
    }

    public static class Zeit
    {
        // Zeitfenster von-bis (bis exklusiv), darf über Mitternacht gehen. von == bis heisst ganzer Tag.
        public static bool InFenster(int stunde, int von, int bis)
        {
            if (von == bis) return true;
            if (von < bis) return stunde >= von && stunde < bis;
            return stunde >= von || stunde < bis;
        }

        public static string Text(int stunde)
        {
            return stunde.ToString("00") + ":00";
        }
    }
}
