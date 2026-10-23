using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShiftDesk
{
    public class Plan
    {
        public const int FREI = -1;
        public const int RUHE = -2;

        public Konfiguration Cfg;
        public DateTime Start;
        public int Stunden;
        public List<Person> Zeilen = new List<Person>();
        public int[] Block;       // Zeilen mit gleichen erlaubten Posten bilden einen Block (Abstand im Plan)
        public int[] Team;        // Zeilen mit gleicher Nummer bilden ein festes Team und sind immer zusammen eingeteilt
        public int[,] Z;          // [Zeile, Stunde] -> Postenindex, FREI oder RUHE
        public bool[,] Essen;     // [Zeile, Stunde] -> isst in dieser Stunde
        public List<Person> NichtEingeteilt = new List<Person>();
        public List<string> Warnungen = new List<string>();

        public int Seiten
        {
            get { return (Stunden + 23) / 24; }
        }
    }

    public class Planer
    {
        static readonly CultureInfo De = new CultureInfo("de-CH");
        const double SPERRE = 1e6;    // Person darf diesen Posten nicht besetzen
        const double LEER = 1e4;      // Platz bleibt unbesetzt

        readonly Konfiguration cfg;
        readonly Plan plan;
        readonly Random rnd;
        readonly int[,] anzahl;     // wie oft war Zeile r schon auf Posten k
        readonly long[] gegessen;   // Schlüssel des Essensfensters, in dem Zeile r zuletzt gegessen hat
        readonly bool[] kannEssen;  // Funktion darf auf mindestens einen Posten mit Essen
        Gruppe[] gruppe;

        // Essensfenster der aktuellen Stunde
        bool essenAktiv;
        long essenSchluessel;
        int essenRestStunden;
        DateTime essenBeginn, essenEnde;

        Planer(Konfiguration cfg, Plan plan, int seed)
        {
            this.cfg = cfg;
            this.plan = plan;
            rnd = new Random(seed);
            anzahl = new int[plan.Zeilen.Count, cfg.Posten.Count];
            gegessen = new long[plan.Zeilen.Count];
            kannEssen = plan.Zeilen.Select(p => cfg.Posten.Any(k => k.EssenMoeglich && Konfiguration.Darf(p, k))).ToArray();
        }

        public static Plan Erstelle(Konfiguration cfg, int seed)
        {
            var plan = new Plan();
            plan.Cfg = cfg;
            plan.Start = AufStundeRunden(cfg.Start);
            DateTime ende = AufStundeRunden(cfg.Ende);
            if (cfg.Ende > ende) ende = ende.AddHours(1);   // angebrochene Stunde mitnehmen
            plan.Stunden = (int)Math.Round((ende - plan.Start).TotalHours);
            if (plan.Stunden <= 0)
                throw new Exception("Das Ende muss nach dem Start liegen.");
            if (plan.Stunden > 24 * 31)
                throw new Exception("Ein Plan darf höchstens 31 Tage lang sein.");

            Func<Person, int> funktionIndex = p =>
            {
                int i = cfg.Funktionen.FindIndex(f => f.Name == p.Funktion);
                return i < 0 ? int.MaxValue : i;
            };
            Func<Person, int> gruppenIndex = p =>
            {
                int i = cfg.Gruppen.FindIndex(g => g.Name == p.Gruppe);
                return i < 0 ? int.MaxValue : i;
            };
            // Fahrer stehen als eigener Block zuunterst
            plan.Zeilen = cfg.Personen.Where(cfg.ImPlan)
                .Select((p, i) => new { p, i })
                .OrderBy(x => x.p.Fahrer).ThenBy(x => funktionIndex(x.p)).ThenBy(x => gruppenIndex(x.p)).ThenBy(x => x.i)
                .Select(x => x.p).ToList();
            plan.NichtEingeteilt = cfg.Personen.Where(p => !cfg.ImPlan(p)).ToList();
            if (plan.Zeilen.Count == 0)
                throw new Exception("Es sind keine Personen erfasst, die eingeteilt werden.");

            // Blöcke: gleiche Menge erlaubter Posten
            Func<Person, string> schluessel = p => string.Join("|", Enumerable.Range(0, cfg.Posten.Count).Where(k => Konfiguration.Darf(p, cfg.Posten[k])));
            plan.Block = new int[plan.Zeilen.Count];
            for (int r = 1; r < plan.Zeilen.Count; r++)
                plan.Block[r] = plan.Block[r - 1] + (schluessel(plan.Zeilen[r]) == schluessel(plan.Zeilen[r - 1]) ? 0 : 1);

            // Zweierteams: je zwei aufeinanderfolgende Zeilen mit gleicher Funktion und Gruppe (die Liste ist
            // danach sortiert, die Reihenfolge innerhalb bestimmt die Personen-Tabelle); wer übrig bleibt, ist allein
            plan.Team = new int[plan.Zeilen.Count];
            for (int r = 0, team = 0; r < plan.Zeilen.Count; team++)
            {
                plan.Team[r] = team;
                if (cfg.Zweierteams && r + 1 < plan.Zeilen.Count
                    && plan.Zeilen[r + 1].Funktion == plan.Zeilen[r].Funktion && plan.Zeilen[r + 1].Gruppe == plan.Zeilen[r].Gruppe
                    && plan.Zeilen[r + 1].Fahrer == plan.Zeilen[r].Fahrer)
                    plan.Team[++r] = team;
                r++;
            }

            plan.Z = new int[plan.Zeilen.Count, plan.Stunden];
            plan.Essen = new bool[plan.Zeilen.Count, plan.Stunden];

            new Planer(cfg, plan, seed).Rechnen();
            return plan;
        }

        static DateTime AufStundeRunden(DateTime d)
        {
            return new DateTime(d.Year, d.Month, d.Day, d.Hour, 0, 0);
        }

        void Rechnen()
        {
            int n = plan.Zeilen.Count;
            gruppe = new Gruppe[n];
            for (int r = 0; r < n; r++)
            {
                var p = plan.Zeilen[r];
                gruppe[r] = cfg.Gruppen.FirstOrDefault(g => g.Name == p.Gruppe);
                if (gruppe[r] == null)
                    plan.Warnungen.Add(Name(r) + ": keine gültige Gruppe, ist darum nie in Ruhe.");
                if (!cfg.Funktionen.Any(f => f.Name == p.Funktion))
                    plan.Warnungen.Add(Name(r) + ": unbekannte Funktion «" + p.Funktion + "».");
                else if (!cfg.Posten.Any(k => Konfiguration.Darf(p, k)))
                    plan.Warnungen.Add(Name(r) + (p.Fahrer
                        ? ": ist Fahrer, aber kein Posten vom Typ «Fahrer» erlaubt die Funktion «" + p.Funktion + "»."
                        : ": kein Posten erlaubt die Funktion «" + p.Funktion + "»."));
            }
            foreach (var p in cfg.Posten.Where(k => k.Typ == PostenTyp.Fahrer))
                if (!plan.Zeilen.Any(z => z.Fahrer))
                    plan.Warnungen.Add("Posten «" + p.Name + "» ist vom Typ «Fahrer», aber niemand ist als Fahrer erfasst.");
            // Posten, die eine inzwischen gelöschte Funktion erlauben (bleibt sonst unbemerkt unbesetzt)
            foreach (var p in cfg.Posten)
            {
                var fehlt = p.Funktionen.Where(f => !cfg.Funktionen.Any(x => x.Name == f)).ToList();
                if (fehlt.Count == 0) continue;
                plan.Warnungen.Add("Posten «" + p.Name + "»: die Funktion «" + string.Join("», «", fehlt) + "» gibt es nicht mehr" +
                    (fehlt.Count == p.Funktionen.Count ? ", niemand darf den Posten besetzen." : "."));
            }

            var unterbesetzt = new Dictionary<int, List<int>>();   // Posten -> Stunden
            var essenGesehen = new Dictionary<long, HashSet<int>>();
            var essenName = new Dictionary<long, string>();

            for (int t = 0; t < plan.Stunden; t++)
            {
                DateTime zeit = plan.Start.AddHours(t);
                int h = zeit.Hour;

                for (int r = 0; r < n; r++)
                    plan.Z[r, t] = gruppe[r] != null && gruppe[r].RuhtUm(h) ? Plan.RUHE : Plan.FREI;

                EssensfensterBestimmen(zeit, essenName);

                var zeilen = Enumerable.Range(0, n).Where(r => plan.Z[r, t] == Plan.FREI).ToList();
                int takt = cfg.TaktUm(h);
                Zuteilen(t, h, zeilen, unterbesetzt, takt > 1 && t > 0 && h % takt != 0);

                if (essenAktiv)
                {
                    HashSet<int> gesehen;
                    if (!essenGesehen.TryGetValue(essenSchluessel, out gesehen))
                        essenGesehen[essenSchluessel] = gesehen = new HashSet<int>();
                    foreach (int r in zeilen.Where(r => kannEssen[r] && BrauchtEssen(r, t)))
                    {
                        gesehen.Add(r);
                        int k = plan.Z[r, t];
                        if (k >= 0 && cfg.Posten[k].EssenMoeglich && gegessen[r] != essenSchluessel)
                        {
                            plan.Essen[r, t] = true;
                            gegessen[r] = essenSchluessel;
                        }
                    }
                    if (essenRestStunden == 1 && essenBeginn >= plan.Start)
                        foreach (int r in gesehen.Where(r => gegessen[r] != essenSchluessel))
                            plan.Warnungen.Add(Name(r) + ": kein Essen bei " + essenName[essenSchluessel]);
                }
            }

            DraussenPruefen();

            foreach (var e in unterbesetzt.OrderByDescending(e => e.Key))
            {
                var zeiten = e.Value.Take(8).Select(t => plan.Start.AddHours(t).ToString("ddd dd.MM. HH:00", De));
                plan.Warnungen.Insert(0, "Posten «" + cfg.Posten[e.Key].Name + "» ist in " + e.Value.Count +
                    " Stunde(n) nicht voll besetzt: " + string.Join(", ", zeiten) + (e.Value.Count > 8 ? " …" : ""));
            }
        }

        bool Draussen(int r, int t)
        {
            int k = plan.Z[r, t];
            return k >= 0 && cfg.Posten[k].Draussen;
        }

        // Wie viele Stunden ist Zeile r unmittelbar vor Stunde t schon am Stück draussen?
        int DraussenSerie(int r, int t)
        {
            int n = 0;
            while (t - 1 - n >= 0 && Draussen(r, t - 1 - n)) n++;
            return n;
        }

        // Zur Gesunderhaltung: wer länger als erlaubt am Stück draussen war, wird gemeldet
        void DraussenPruefen()
        {
            int max = cfg.DraussenMaxStunden;
            if (max <= 0) return;
            for (int r = 0; r < plan.Zeilen.Count; r++)
            {
                var faelle = new List<string>();
                for (int t = 0; t < plan.Stunden; t++)
                {
                    if (!Draussen(r, t) || (t > 0 && Draussen(r, t - 1))) continue;
                    int ende = t;
                    while (ende + 1 < plan.Stunden && Draussen(r, ende + 1)) ende++;
                    int dauer = ende - t + 1;
                    if (dauer > max)
                        faelle.Add(dauer + " Std. ab " + plan.Start.AddHours(t).ToString("ddd dd.MM. HH:00", De));
                    t = ende;
                }
                if (faelle.Count > 0)
                    plan.Warnungen.Add(Name(r) + ": länger als " + max + " Std. am Stück draussen (" +
                        string.Join(", ", faelle.Take(4)) + (faelle.Count > 4 ? ", …" : "") + ")");
            }
        }

        void EssensfensterBestimmen(DateTime zeit, Dictionary<long, string> namen)
        {
            essenAktiv = false;
            int h = zeit.Hour;
            for (int i = 0; i < cfg.Essenszeiten.Count; i++)
            {
                var e = cfg.Essenszeiten[i];
                if (e.Von == e.Bis || !Zeit.InFenster(h, e.Von, e.Bis))
                    continue;
                DateTime beginn = zeit.Date.AddHours(e.Von);
                if (h < e.Von) beginn = beginn.AddDays(-1);
                essenAktiv = true;
                essenSchluessel = beginn.Ticks + i + 1;
                essenRestStunden = (e.Bis - h + 24) % 24;
                essenBeginn = beginn;
                essenEnde = zeit.AddHours(essenRestStunden);
                namen[essenSchluessel] = e.Name + " " + beginn.ToString("ddd dd.MM.", De);
                return;
            }
        }

        // Teilt die verfügbaren Personen einer Stunde den Posten zu (Ungarische Methode).
        // Spalten: zu besetzende Plätze + je ein «Rest»-Platz pro Person (Auffangposten oder frei).
        // Zusätzliche Leer-Zeilen erlauben es, Plätze unbesetzt zu lassen, wenn Leute fehlen.
        // Zugeteilt werden Einheiten: ein Zweierteam, dessen beide Mitglieder verfügbar sind, zählt als eine Einheit
        // der Grösse 2 und braucht auf dem Posten einen Doppelplatz; alle anderen sind Einheiten der Grösse 1.
        void Zuteilen(int t, int h, List<int> zeilen, Dictionary<int, List<int>> unterbesetzt, bool fortsetzen)
        {
            var einheiten = new List<int[]>();
            var offen = new HashSet<int>(zeilen);
            foreach (int r in zeilen)
            {
                if (!offen.Remove(r)) continue;
                int partner = offen.FirstOrDefault(x => plan.Team[x] == plan.Team[r]);
                if (offen.Contains(partner) && plan.Team[partner] == plan.Team[r] && offen.Remove(partner))
                    einheiten.Add(new[] { r, partner });
                else
                    einheiten.Add(new[] { r });
            }
            Func<int[], Posten, bool> erlaubtFuer = (e, p) => e.All(r => Konfiguration.Darf(plan.Zeilen[r], p));

            // Plätze: Posten k mit Grösse g (2 = Doppelplatz für ein Team, 1 = Einzelplatz).
            // Doppelplätze nur so viele, wie Teams da sind, die den Posten besetzen dürfen; der Rest sind Einzelplätze
            // (z. B. Fahrerposten, wenn die Fahrer einzeln sind).
            var plaetze = new List<int>();
            var groesse = new List<int>();
            for (int k = 0; k < cfg.Posten.Count; k++)
            {
                var p = cfg.Posten[k];
                if (p.Typ == PostenTyp.Auffang || !p.OffenUm(h)) continue;
                int doppel = Math.Min(p.Anzahl / 2, einheiten.Count(e => e.Length == 2 && erlaubtFuer(e, p)));
                for (int i = 0; i < doppel; i++) { plaetze.Add(k); groesse.Add(2); }
                for (int i = 0; i < p.Anzahl - 2 * doppel; i++) { plaetze.Add(k); groesse.Add(1); }
            }

            // Mitten in einer Ablösung (fortsetzen) bleibt jeder auf seinem Posten: das Bleiben ist in den Kosten
            // so stark belohnt, dass nur zwei Dinge einen Wechsel auslösen: wer auf dem Auffangposten sitzt, rückt
            // auf einen frei gewordenen Platz nach, und wer draussen die erlaubte Zeit erreicht hat, tauscht mit
            // jemandem von drinnen (Gesunderhaltung geht vor).
            int n = einheiten.Count, m = plaetze.Count, N = n + m;
            if (N == 0) return;

            // Durchschnitt pro Posten unter den Personen, die ihn besetzen dürfen
            var schnitt = new double[cfg.Posten.Count];
            for (int k = 0; k < cfg.Posten.Count; k++)
            {
                var erlaubt = zeilen.Where(r => Konfiguration.Darf(plan.Zeilen[r], cfg.Posten[k])).ToList();
                schnitt[k] = erlaubt.Count == 0 ? 0 : erlaubt.Average(r => (double)anzahl[r, k]);
            }
            var rest = einheiten.Select(e => cfg.Posten.FindIndex(p => p.Typ == PostenTyp.Auffang && p.OffenUm(h) && erlaubtFuer(e, p))).ToArray();
            Func<int[], int, double> kostenEinheit = (e, k) => e.Sum(r => Kosten(r, k, t, schnitt, fortsetzen));

            var kosten = new double[N, N];
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    if (i < n && j < m)
                        kosten[i, j] = groesse[j] == einheiten[i].Length && erlaubtFuer(einheiten[i], cfg.Posten[plaetze[j]])
                            ? kostenEinheit(einheiten[i], plaetze[j]) : SPERRE;
                    else if (i < n)
                        kosten[i, j] = kostenEinheit(einheiten[i], rest[i]);
                    else if (j < m)
                        kosten[i, j] = LEER + (m - j) * 100;  // wichtigere Posten (weiter oben) immer zuerst besetzen
                    else
                        kosten[i, j] = 0;
                }

            int[] wahl = Ungarisch(kosten, N);
            var besetzt = new bool[m];
            for (int i = 0; i < n; i++)
            {
                int j = wahl[i];
                int k = j < m ? plaetze[j] : rest[i];
                if (j < m) besetzt[j] = true;
                foreach (int r in einheiten[i])
                {
                    plan.Z[r, t] = k;
                    if (k >= 0) anzahl[r, k]++;
                }
            }
            for (int j = 0; j < m; j++)
            {
                if (besetzt[j]) continue;
                List<int> liste;
                if (!unterbesetzt.TryGetValue(plaetze[j], out liste))
                    unterbesetzt[plaetze[j]] = liste = new List<int>();
                if (liste.Count == 0 || liste[liste.Count - 1] != t) liste.Add(t);
            }
        }

        const double BLEIBEN = 30;    // Belohnung, mitten im Block auf dem Posten zu bleiben
        const double DRAUSSEN = 80;   // Strafe, über die erlaubte Zeit hinaus draussen zu bleiben (> 2 × BLEIBEN, damit ein Tausch lohnt)

        double Kosten(int r, int k, int t, double[] schnitt, bool fortsetzen)
        {
            double c = rnd.NextDouble() * 0.3;
            if (k < 0) return c;
            int vorher = t > 0 ? plan.Z[r, t - 1] : Plan.RUHE;
            int takt = cfg.TaktUm(plan.Start.AddHours(t).Hour);
            int vorvorher = t > takt ? plan.Z[r, t - 1 - takt] : Plan.RUHE;   // Posten vor der letzten Ablösung
            var posten = cfg.Posten[k];

            if (fortsetzen)
            {
                // mitten im Block: bleiben, Gerechtigkeit zählt hier kaum
                if (vorher == k && posten.Typ != PostenTyp.Auffang) c -= BLEIBEN;
                c += 0.5 * (anzahl[r, k] - schnitt[k]);
            }
            else if (posten.AmStueck)
            {
                // z. B. Wacht Kdt: möglichst am Stück
                if (vorher == k) c -= 5;
                c += 0.5 * (anzahl[r, k] - schnitt[k]);
            }
            else
            {
                // jede Stunde ein anderer Posten, und gerecht verteilt
                if (vorher == k) c += 10;
                if (vorvorher == k) c += 3;
                c += 4 * (anzahl[r, k] - schnitt[k]);
            }

            // Gesunderhaltung: nicht zu lange am Stück draussen. Kein hartes Verbot, damit der Posten
            // notfalls trotzdem besetzt wird; dann gibt es eine Warnung.
            if (posten.Draussen && cfg.DraussenMaxStunden > 0)
            {
                int serie = DraussenSerie(r, t);
                if (serie >= cfg.DraussenMaxStunden)
                    c += DRAUSSEN + 20 * (serie - cfg.DraussenMaxStunden);
                else if (serie > 0)
                    c += 1.5 * serie / cfg.DraussenMaxStunden;   // leicht lieber jemanden nehmen, der noch drin war
            }

            if (essenAktiv && posten.EssenMoeglich && kannEssen[r] && BrauchtEssen(r, t))
            {
                if (gegessen[r] != essenSchluessel)
                    c -= 6 + 20.0 / Math.Max(1, EssensChancen(r, t));
                else
                    c += 1;
            }
            return c;
        }

        // Wer im Essensfenster Ruhezeit hat, isst in der Ruhezeit; eingeplant werden nur die, die das ganze Fenster im Dienst sind
        bool BrauchtEssen(int r, int t)
        {
            if (gruppe[r] == null) return true;
            for (DateTime z = essenBeginn; z < essenEnde; z = z.AddHours(1))
                if (gruppe[r].RuhtUm(z.Hour)) return false;
            return true;
        }

        // In wie vielen der restlichen Stunden dieses Essensfensters ist die Person im Dienst?
        int EssensChancen(int r, int t)
        {
            int chancen = 0;
            for (int s = t; s < plan.Stunden; s++)
            {
                DateTime z = plan.Start.AddHours(s);
                if (z >= essenEnde) break;
                if (gruppe[r] == null || !gruppe[r].RuhtUm(z.Hour)) chancen++;
            }
            return chancen;
        }

        string Name(int r)
        {
            var p = plan.Zeilen[r];
            return (p.Grad + " " + p.Name).Trim();
        }

        // Ungarische Methode: minimale Gesamtkosten, liefert für jede Zeile die gewählte Spalte.
        static int[] Ungarisch(double[,] a, int n)
        {
            const double INF = double.MaxValue / 4;
            var u = new double[n + 1];
            var v = new double[n + 1];
            var p = new int[n + 1];
            var weg = new int[n + 1];
            for (int i = 1; i <= n; i++)
            {
                p[0] = i;
                int j0 = 0;
                var minv = new double[n + 1];
                var benutzt = new bool[n + 1];
                for (int j = 0; j <= n; j++) minv[j] = INF;
                do
                {
                    benutzt[j0] = true;
                    int i0 = p[j0], j1 = 0;
                    double delta = INF;
                    for (int j = 1; j <= n; j++)
                    {
                        if (benutzt[j]) continue;
                        double cur = a[i0 - 1, j - 1] - u[i0] - v[j];
                        if (cur < minv[j]) { minv[j] = cur; weg[j] = j0; }
                        if (minv[j] < delta) { delta = minv[j]; j1 = j; }
                    }
                    for (int j = 0; j <= n; j++)
                    {
                        if (benutzt[j]) { u[p[j]] += delta; v[j] -= delta; }
                        else minv[j] -= delta;
                    }
                    j0 = j1;
                } while (p[j0] != 0);
                do
                {
                    int j1 = weg[j0];
                    p[j0] = p[j1];
                    j0 = j1;
                } while (j0 != 0);
            }
            var ergebnis = new int[n];
            for (int j = 1; j <= n; j++)
                if (p[j] != 0) ergebnis[p[j] - 1] = j - 1;
            return ergebnis;
        }
    }
}
