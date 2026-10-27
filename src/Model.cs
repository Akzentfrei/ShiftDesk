using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace ShiftDesk
{
    // Wachtkdt gibt es nur noch, damit ältere Dateien gelesen werden können.
    // Fahrer: nur für Fahrer (z. B. Presto), und Fahrer sind nur auf solchen Posten oder in Ruhe.
    public enum PostenTyp { Normal, Wachtkdt, Auffang, Fahrer }

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
        public bool Fahrer;   // nur auf Posten vom Typ Fahrer oder in Ruhe, im Plan mit Auto markiert
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
        public bool Draussen;                               // im Freien: niemand soll zu lange am Stück draussen sein
        public List<string> Funktionen = new List<string>(); // leer = alle Funktionen

        // Wacht Kdt und Stv bleiben möglichst am Stück: der Posten Wacht Kdt und jeder Posten nur für Kdt-Funktionen.
        // Alle Posten müssen voll besetzt sein; bei zu wenig Leuten entscheidet die Reihenfolge.
        public bool AmStueck
        {
            get
            {
                return string.Equals((Name ?? "").Trim(), Konfiguration.Pflichtposten[0], StringComparison.OrdinalIgnoreCase)
                    || (Funktionen.Count > 0 && Funktionen.All(f => f.IndexOf("Kdt", StringComparison.OrdinalIgnoreCase) >= 0));
            }
        }

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
        // Vorgaben für neue Dateien und das Beispiel
        public static readonly string[] StandardGrade = { "Rekr", "Sdt", "Gfr", "Obgfr", "Kpl", "Wm", "Obwm" };

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

        // Diese Posten gibt es in jeder Datei; sie werden ergänzt, wenn sie fehlen, und lassen sich nicht löschen
        public static readonly string[] Pflichtposten = { "Wacht Kdt", "Presto", "Subito" };

        public static bool IstPflichtposten(string name)
        {
            return Pflichtposten.Any(p => string.Equals(p, (name ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // Wacht Kdt Stv: ruht immer dann, wenn der Wacht Kdt im Dienst ist (braucht keine eigene Gruppe)
        public static bool IstStv(string funktion)
        {
            funktion = funktion ?? "";
            return funktion.IndexOf("Kdt", StringComparison.OrdinalIgnoreCase) >= 0 && funktion.IndexOf("Stv", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IstKdt(string funktion)
        {
            return (funktion ?? "").IndexOf("Kdt", StringComparison.OrdinalIgnoreCase) >= 0 && !IstStv(funktion);
        }

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

        public static Konfiguration Neu()
        {
            var k = new Konfiguration { Start = DateTime.Today, Ende = DateTime.Today.AddDays(1),
                Funktionen = StandardFunktionen(), Grade = StandardGrade.ToList() };
            k.PflichtpostenErgaenzen();
            return k;
        }

        // Dateiversion: 0 = von vor den frei definierbaren Funktionen und Graden (Feld fehlt in der Datei),
        // 2 = vor dem Schalter «Pflicht» (den es nicht mehr gibt, alle Posten sind Pflicht),
        // 3 = Einheit und Zweck waren noch im Namen zusammengefasst
        public const int AktuelleVersion = 4;
        public int Version;

        public string Name = "Wachtablösung";
        public string Einheit = "";
        public string Zweck;               // nur noch zum Lesen älterer Dateien, wird in Name übernommen
        public string Sprache = "de";      // Sprache des Ausdrucks: de, fr, it, en
        public DateTime Start;
        public DateTime Ende;
        public bool Tagesplan;             // ein Blatt von 00:00 bis 24:00 ohne Datum, gilt jeden Tag (Start und Ende zählen dann nicht)
        public int AbloesungStunden = 1;   // Posten wechseln alle X Stunden (ab Mitternacht gerechnet)
        public int StuendlichVon;          // in diesem Zeitfenster wird trotzdem jede Stunde abgelöst (von == bis: kein Fenster)
        public int StuendlichBis;
        public bool Zweierteams;           // je zwei Personen mit gleicher Funktion und Gruppe sind immer zusammen eingeteilt
        public int DraussenMaxStunden = 2; // höchstens so viele Stunden am Stück auf Posten im Freien (0 = keine Grenze)
        public List<Funktion> Funktionen = new List<Funktion>();
        public List<string> Grade = new List<string>();
        public List<Person> Personen = new List<Person>();
        public List<Gruppe> Gruppen = new List<Gruppe>();
        public List<Posten> Posten = new List<Posten>();
        public List<Essenszeit> Essenszeiten = new List<Essenszeit>();

        // Alle wie viele Stunden wird um diese Stunde abgelöst? Im stündlichen Fenster (inklusive seiner Endstunde,
        // damit der letzte stündliche Block dort endet) ist es 1, sonst die eingestellte Ablösung.
        public int TaktUm(int stunde)
        {
            bool stuendlich = StuendlichVon != StuendlichBis && (Zeit.InFenster(stunde, StuendlichVon, StuendlichBis) || stunde == StuendlichBis);
            return stuendlich ? 1 : Math.Max(1, AbloesungStunden);
        }

        // Darf diese Person diesen Posten besetzen? Funktion muss erlaubt sein; Fahrer nur auf Fahrerposten.
        // Auf dem Fahrerposten dürfen auch Nicht-Fahrer sein, aber nur neben einem Fahrer: der erste Platz
        // ist für einen Fahrer reserviert, das regelt der Planer.
        public static bool Darf(Person p, Posten k)
        {
            return k.Erlaubt(p.Funktion) && (!p.Fahrer || k.Typ == PostenTyp.Fahrer);
        }

        public bool ImPlan(Person p)
        {
            var f = Funktionen.FirstOrDefault(x => x.Name == p.Funktion);
            return f == null || f.ImPlan;
        }

        public void Speichern(string pfad)
        {
            pfad = Path.GetFullPath(pfad);
            Directory.CreateDirectory(Path.GetDirectoryName(pfad));
            var ser = new XmlSerializer(typeof(Konfiguration));
            Zweck = null;
            Version = AktuelleVersion;
            // erst ganz in eine Hilfsdatei schreiben und dann austauschen: bricht das Schreiben ab
            // (Absturz, voller Datenträger), bleibt die bisherige Datei unversehrt
            string neu = pfad + ".neu";
            using (var fs = File.Create(neu))
                ser.Serialize(fs, this);
            if (!File.Exists(pfad))
            {
                File.Move(neu, pfad);
                return;
            }
            try { File.Replace(neu, pfad, null); }
            catch (Exception ex)
            {
                // z. B. auf Netzlaufwerken oder USB-Sticks ohne NTFS
                if (!(ex is IOException || ex is PlatformNotSupportedException || ex is UnauthorizedAccessException)) throw;
                File.Copy(neu, pfad, true);
                File.Delete(neu);
            }
        }

        public static Konfiguration Laden(string pfad)
        {
            var ser = new XmlSerializer(typeof(Konfiguration));
            Konfiguration k;
            using (var fs = File.OpenRead(pfad))
                k = (Konfiguration)ser.Deserialize(fs);
            if (k.Version < 4)
            {
                if (!string.IsNullOrEmpty(k.Zweck) || !string.IsNullOrEmpty(k.Einheit))
                    k.Name = string.Join(" ", new[] { k.Zweck, k.Einheit }.Where(s => !string.IsNullOrEmpty(s)));
                k.Einheit = "";
            }
            k.Zweck = null;
            if (k.Einheit == null) k.Einheit = "";
            if (Druck.Sprachen.All(x => x.Kuerzel != k.Sprache)) k.Sprache = "de";
            if (k.Version < 2) k.AeltereVersionUebernehmen();
            k.StvGruppenEntfernen();
            k.Version = AktuelleVersion;
            k.PflichtpostenErgaenzen();
            return k;
        }

        // Der Wacht Kdt Stv ruht immer im Gegenteil zum Wacht Kdt und braucht keine Gruppe: Gruppen, die nur
        // noch von Stv-Personen benutzt werden, fallen weg
        void StvGruppenEntfernen()
        {
            var weg = Gruppen.Where(g => Personen.Any(p => p.Gruppe == g.Name)
                && Personen.Where(p => p.Gruppe == g.Name).All(p => IstStv(p.Funktion))).ToList();
            foreach (var g in weg)
            {
                Gruppen.Remove(g);
                foreach (var p in Personen.Where(p => p.Gruppe == g.Name)) p.Gruppe = "";
            }
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
            k.Zweierteams = true;
            k.Funktionen = StandardFunktionen();

            k.Gruppen.Add(new Gruppe { Name = "Kdt", RuheVon = 3, RuheBis = 15 });
            k.Gruppen.Add(new Gruppe { Name = "Gruppe 1", RuheVon = 23, RuheBis = 7 });
            k.Gruppen.Add(new Gruppe { Name = "Gruppe 2", RuheVon = 7, RuheBis = 15 });
            k.Gruppen.Add(new Gruppe { Name = "Gruppe 3", RuheVon = 15, RuheBis = 23 });

            // erfundene Namen
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
