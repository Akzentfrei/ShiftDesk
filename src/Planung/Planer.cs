using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShiftDesk.Modell;

namespace ShiftDesk.Planung
{
    /// <summary>
    /// Erstellt aus einer <see cref="Konfiguration"/> einen <see cref="Plan"/>: Stunde für Stunde werden die
    /// Personen, die nicht ruhen, fair auf die offenen Posten verteilt.
    /// </summary>
    /// <remarks>
    /// <para>Ablauf (diese Datei): Zeitraum und Zeilen festlegen, dann für jede Stunde Ruhezeiten eintragen,
    /// die Posten zuteilen und die Essensstunden planen; am Schluss die Hinweise sammeln.</para>
    /// <para>Die Zuteilung einer einzelnen Stunde (Zweierteams, Kosten, Ungarische Methode) steht in
    /// <c>Planer.Zuteilung.cs</c>.</para>
    /// <para>Gleiche Eingaben und gleicher <c>seed</c> ergeben immer denselben Plan; ein anderer <c>seed</c>
    /// ergibt eine andere, gleich faire Variante.</para>
    /// </remarks>
    public partial class Planer
    {
        static readonly CultureInfo De = new CultureInfo("de-CH");

        readonly Konfiguration cfg;
        readonly Plan plan;
        readonly Random rnd;

        /// <summary>[Zeile, Posten] → wie oft die Zeile schon auf diesem Posten war (für die Gerechtigkeit).</summary>
        readonly int[,] anzahl;

        /// <summary>Je Zeile der Schlüssel des Essensfensters, in dem sie zuletzt gegessen hat.</summary>
        readonly long[] gegessen;

        /// <summary>Je Zeile: darf auf mindestens einen Posten mit Essen (Fahrer nie, sie essen in der Ruhezeit).</summary>
        readonly bool[] kannEssen;

        /// <summary>Je Zeile die Ruhegruppe (beim Wacht Kdt Stv die des Wacht Kdt), null = ruht nie.</summary>
        Gruppe[] gruppe;

        /// <summary>Je Zeile: ruht im Gegenteil zur Gruppe (Wacht Kdt Stv: ruht, wenn der Wacht Kdt im Dienst ist).</summary>
        bool[] gegenteil;

        // Essensfenster der aktuellen Stunde (siehe EssensfensterBestimmen)
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
            kannEssen = plan.Zeilen.Select(p => !p.Fahrer && cfg.Posten.Any(k => k.EssenMoeglich && Konfiguration.Darf(p, k))).ToArray();
        }

        /// <summary>Erstellt einen Plan.</summary>
        /// <param name="cfg">Die Eingaben; werden nicht verändert.</param>
        /// <param name="seed">Startwert für den Zufall: jeder Wert ergibt eine andere, gleich faire Variante.</param>
        /// <exception cref="Exception">Der Zeitraum ist ungültig oder es gibt niemanden zum Einteilen (Text für den Benutzer).</exception>
        public static Plan Erstelle(Konfiguration cfg, int seed)
        {
            var plan = new Plan { Cfg = cfg };
            ZeitraumFestlegen(plan);
            ZeilenOrdnen(plan);
            BloeckeUndTeamsBilden(plan);
            plan.Z = new int[plan.Zeilen.Count, plan.Stunden];
            plan.Essen = new bool[plan.Zeilen.Count, plan.Stunden];
            plan.EssenAbloesung = new bool[plan.Zeilen.Count, plan.Stunden];

            new Planer(cfg, plan, seed).Rechnen();
            return plan;
        }

        // ---------------------------------------------------------------- Vorbereitung

        /// <summary>Start und Länge auf volle Stunden; der Tagesplan geht immer von 00:00 bis 24:00.</summary>
        static void ZeitraumFestlegen(Plan plan)
        {
            var cfg = plan.Cfg;
            plan.Start = AufStundeRunden(cfg.Start);
            DateTime ende = AufStundeRunden(cfg.Ende);
            if (cfg.Ende > ende) ende = ende.AddHours(1);   // angebrochene Stunde mitnehmen
            if (cfg.Tagesplan)
            {
                // das Datum erscheint nirgends
                plan.Start = DateTime.Today;
                ende = plan.Start.AddDays(1);
            }
            plan.Stunden = (int)Math.Round((ende - plan.Start).TotalHours);
            if (plan.Stunden <= 0)
                throw new Exception("Das Ende muss nach dem Start liegen.");
            if (plan.Stunden > 24 * 31)
                throw new Exception("Ein Plan darf höchstens 31 Tage lang sein.");
        }

        static DateTime AufStundeRunden(DateTime d)
        {
            return new DateTime(d.Year, d.Month, d.Day, d.Hour, 0, 0);
        }

        /// <summary>
        /// Eingeteilte Personen ordnen: Fahrer als eigener Block zuunterst, sonst nach Funktion und Gruppe,
        /// innerhalb davon in der Reihenfolge der Personen-Tabelle.
        /// </summary>
        static void ZeilenOrdnen(Plan plan)
        {
            var cfg = plan.Cfg;
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
            plan.Zeilen = cfg.Personen.Where(cfg.ImPlan)
                .Select((p, i) => new { p, i })
                .OrderBy(x => x.p.Fahrer).ThenBy(x => funktionIndex(x.p)).ThenBy(x => gruppenIndex(x.p)).ThenBy(x => x.i)
                .Select(x => x.p).ToList();
            plan.NichtEingeteilt = cfg.Personen.Where(p => !cfg.ImPlan(p)).ToList();
            if (plan.Zeilen.Count == 0)
                throw new Exception("Es sind keine Personen erfasst, die eingeteilt werden.");
        }

        /// <summary>
        /// Blöcke: aufeinanderfolgende Zeilen mit gleicher Menge erlaubter Posten.
        /// Zweierteams: je zwei aufeinanderfolgende Zeilen mit gleicher Funktion und Gruppe (die Liste ist danach
        /// sortiert, die Reihenfolge innerhalb bestimmt die Personen-Tabelle); wer übrig bleibt, ist allein.
        /// </summary>
        static void BloeckeUndTeamsBilden(Plan plan)
        {
            var cfg = plan.Cfg;
            int n = plan.Zeilen.Count;
            Func<Person, string> schluessel = p => string.Join("|", Enumerable.Range(0, cfg.Posten.Count).Where(k => Konfiguration.Darf(p, cfg.Posten[k])));
            plan.Block = new int[n];
            for (int r = 1; r < n; r++)
                plan.Block[r] = plan.Block[r - 1] + (schluessel(plan.Zeilen[r]) == schluessel(plan.Zeilen[r - 1]) ? 0 : 1);

            plan.Team = new int[n];
            for (int r = 0, team = 0; r < n; team++)
            {
                plan.Team[r] = team;
                if (cfg.Zweierteams && r + 1 < n
                    && plan.Zeilen[r + 1].Funktion == plan.Zeilen[r].Funktion && plan.Zeilen[r + 1].Gruppe == plan.Zeilen[r].Gruppe
                    && plan.Zeilen[r + 1].Fahrer == plan.Zeilen[r].Fahrer)
                    plan.Team[++r] = team;
                r++;
            }
        }

        // ---------------------------------------------------------------- Ablauf

        void Rechnen()
        {
            int n = plan.Zeilen.Count;
            ZeilenPruefen();
            PostenPruefen();

            var unterbesetzt = new Dictionary<int, List<int>>();   // Posten -> Stunden
            var ohneFahrer = new Dictionary<int, List<int>>();     // Fahrerposten -> Stunden ohne Fahrer
            var abloesungen = new int[plan.Stunden];               // wie viele in dieser Stunde für das Essen abgelöst werden

            for (int t = 0; t < plan.Stunden; t++)
            {
                DateTime zeit = plan.Start.AddHours(t);
                int h = zeit.Hour;

                for (int r = 0; r < n; r++)
                    plan.Z[r, t] = Ruht(r, h) ? Plan.RUHE : Plan.FREI;

                EssensfensterBestimmen(zeit);

                var zeilen = Enumerable.Range(0, n).Where(r => plan.Z[r, t] == Plan.FREI).ToList();
                Zuteilen(t, h, zeilen, unterbesetzt, ohneFahrer, t > 0 && !cfg.NeueAbloesungUm(h));

                if (essenAktiv)
                    EssenEintragen(t, zeilen, abloesungen);
            }

            DraussenPruefen();
            LueckenMelden(unterbesetzt, "nicht voll besetzt");
            LueckenMelden(ohneFahrer, "ohne Fahrer");
        }

        /// <summary>Ruhegruppe jeder Zeile bestimmen und unvollständige Angaben (Gruppe, Funktion) melden.</summary>
        void ZeilenPruefen()
        {
            int n = plan.Zeilen.Count;
            gruppe = new Gruppe[n];
            gegenteil = new bool[n];
            var kdtGruppe = plan.Zeilen.Where(p => Konfiguration.IstKdt(p.Funktion))
                .Select(p => cfg.Gruppen.FirstOrDefault(g => g.Name == p.Gruppe)).FirstOrDefault(g => g != null);
            for (int r = 0; r < n; r++)
            {
                var p = plan.Zeilen[r];
                if (Konfiguration.IstStv(p.Funktion))
                {
                    gruppe[r] = kdtGruppe;
                    gegenteil[r] = true;
                    if (kdtGruppe == null || kdtGruppe.RuheVon == kdtGruppe.RuheBis)
                        plan.Warnungen.Add(p.Anzeigename + ": ruht im Gegenteil zum Wacht Kdt, aber der Wacht Kdt hat keine Ruhezeit; ist darum nie in Ruhe.");
                }
                else
                {
                    gruppe[r] = cfg.Gruppen.FirstOrDefault(g => g.Name == p.Gruppe);
                    if (gruppe[r] == null)
                        plan.Warnungen.Add(p.Anzeigename + ": keine gültige Gruppe, ist darum nie in Ruhe.");
                }
                if (!cfg.Funktionen.Any(f => f.Name == p.Funktion))
                    plan.Warnungen.Add(p.Anzeigename + ": unbekannte Funktion «" + p.Funktion + "».");
                else if (!cfg.Posten.Any(k => Konfiguration.Darf(p, k)))
                    plan.Warnungen.Add(p.Anzeigename + (p.Fahrer
                        ? ": ist Fahrer, aber kein Posten vom Typ «Fahrer» erlaubt die Funktion «" + p.Funktion + "»."
                        : ": kein Posten erlaubt die Funktion «" + p.Funktion + "»."));
            }
        }

        /// <summary>Fahrerposten ohne Fahrer und Posten mit gelöschten Funktionen melden (blieben sonst unbemerkt leer).</summary>
        void PostenPruefen()
        {
            foreach (var p in cfg.Posten.Where(k => k.Typ == PostenTyp.Fahrer))
                if (!plan.Zeilen.Any(z => z.Fahrer))
                    plan.Warnungen.Add("Posten «" + p.Name + "» ist vom Typ «Fahrer», aber niemand ist als Fahrer erfasst.");
            foreach (var p in cfg.Posten)
            {
                var fehlt = p.Funktionen.Where(f => !cfg.Funktionen.Any(x => x.Name == f)).ToList();
                if (fehlt.Count == 0) continue;
                plan.Warnungen.Add("Posten «" + p.Name + "»: die Funktion «" + string.Join("», «", fehlt) + "» gibt es nicht mehr" +
                    (fehlt.Count == p.Funktionen.Count ? ", niemand darf den Posten besetzen." : "."));
            }
        }

        /// <summary>Stellt Hinweise zu Posten, die in manchen Stunden nicht voll besetzt waren, an den Anfang der Liste.</summary>
        /// <param name="luecken">Posten → Stunden mit der Lücke.</param>
        /// <param name="was">Art der Lücke, z. B. «ohne Fahrer».</param>
        void LueckenMelden(Dictionary<int, List<int>> luecken, string was)
        {
            foreach (var e in luecken.OrderByDescending(e => e.Key))
            {
                var zeiten = e.Value.Take(8).Select(t => Zeitpunkt(t));
                plan.Warnungen.Insert(0, "Posten «" + cfg.Posten[e.Key].Name + "» ist in " + e.Value.Count +
                    " Stunde(n) " + was + ": " + string.Join(", ", zeiten) + (e.Value.Count > 8 ? " …" : ""));
            }
        }

        /// <summary>Ruht Zeile r um diese Stunde? Der Stv ruht genau dann, wenn der Wacht Kdt im Dienst ist.</summary>
        bool Ruht(int r, int stunde)
        {
            var g = gruppe[r];
            if (g == null || g.RuheVon == g.RuheBis) return false;
            return gegenteil[r] ? !g.RuhtUm(stunde) : g.RuhtUm(stunde);
        }

        /// <summary>Zeitpunkt für Hinweise; beim Tagesplan nur die Uhrzeit (er gilt jeden Tag).</summary>
        string Zeitpunkt(int t)
        {
            return plan.Start.AddHours(t).ToString(cfg.Tagesplan ? "HH:00" : "ddd dd.MM. HH:00", De);
        }

        // ---------------------------------------------------------------- Draussen

        bool Draussen(int r, int t)
        {
            int k = plan.Z[r, t];
            return k >= 0 && cfg.Posten[k].Draussen;
        }

        /// <summary>Wie viele Stunden ist Zeile r unmittelbar vor Stunde t schon am Stück draussen?</summary>
        int DraussenSerie(int r, int t)
        {
            int n = 0;
            while (t - 1 - n >= 0 && Draussen(r, t - 1 - n)) n++;
            return n;
        }

        /// <summary>Zur Gesunderhaltung: wer länger als erlaubt am Stück draussen war, wird gemeldet.</summary>
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
                        faelle.Add(dauer + " Std. ab " + Zeitpunkt(t));
                    t = ende;
                }
                if (faelle.Count > 0)
                    plan.Warnungen.Add(plan.Zeilen[r].Anzeigename + ": länger als " + max + " Std. am Stück draussen (" +
                        string.Join(", ", faelle.Take(4)) + (faelle.Count > 4 ? ", …" : "") + ")");
            }
        }

        // ---------------------------------------------------------------- Essen

        /// <summary>
        /// Liegt diese Stunde in einem Essensfenster? Setzt <c>essenAktiv</c> und dazu Schlüssel, Beginn, Ende
        /// und die restlichen Stunden des Fensters.
        /// </summary>
        void EssensfensterBestimmen(DateTime zeit)
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
                return;
            }
        }

        /// <summary>Trägt nach der Zuteilung einer Stunde im Essensfenster ein, wer jetzt isst.</summary>
        /// <param name="t">Stunde im Plan.</param>
        /// <param name="zeilen">Zeilen, die in dieser Stunde im Dienst sind.</param>
        /// <param name="abloesungen">Je Stunde, wie viele zum Essen von einer Reserve abgelöst werden.</param>
        void EssenEintragen(int t, List<int> zeilen, int[] abloesungen)
        {
            // Wer zur Essenszeit in Ruhe ist, isst dort: in der ersten Ruhestunde des Fensters eintragen
            for (int r = 0; r < plan.Zeilen.Count; r++)
                if (plan.Z[r, t] == Plan.RUHE && gegessen[r] != essenSchluessel)
                {
                    plan.Essen[r, t] = true;
                    gegessen[r] = essenSchluessel;
                }
            foreach (int r in zeilen.Where(r => kannEssen[r] && BrauchtEssen(r, t)))
            {
                int k = plan.Z[r, t];
                if (k >= 0 && cfg.Posten[k].EssenMoeglich && gegessen[r] != essenSchluessel)
                {
                    plan.Essen[r, t] = true;
                    gegessen[r] = essenSchluessel;
                }
            }
            if (essenRestStunden == 1 && essenBeginn >= plan.Start)
                ReserveAbloesungenPlanen(t, abloesungen);
        }

        /// <summary>
        /// In der letzten Stunde eines Essensfensters: wer das ganze Fenster im Dienst war und auf keinem Posten
        /// mit Essen, isst trotzdem – eine Reserve löst kurz ab (im Plan oranges Besteck), in der Stunde des
        /// Fensters mit den wenigsten solchen Ablösungen. Fahrer essen in der Ruhezeit; Wacht Kdt und Stv
        /// werden zum Essen nicht abgelöst.
        /// </summary>
        void ReserveAbloesungenPlanen(int t, int[] abloesungen)
        {
            int von = (int)Math.Round((essenBeginn - plan.Start).TotalHours);
            for (int r = 0; r < plan.Zeilen.Count; r++)
            {
                var person = plan.Zeilen[r];
                if (person.Fahrer || Konfiguration.IstKdt(person.Funktion) || Konfiguration.IstStv(person.Funktion)
                    || gegessen[r] == essenSchluessel || !BrauchtEssen(r, t)) continue;
                int beste = -1;
                for (int s = von; s <= t; s++)
                    if (plan.Z[r, s] >= 0 && (beste < 0 || abloesungen[s] < abloesungen[beste])) beste = s;
                if (beste < 0) continue;
                plan.EssenAbloesung[r, beste] = true;
                abloesungen[beste]++;
                gegessen[r] = essenSchluessel;
            }
        }

        /// <summary>
        /// Muss für Zeile r im aktuellen Essensfenster eine Essensstunde eingeplant werden? Nur, wer das ganze
        /// Fenster im Dienst ist; wer darin Ruhezeit hat, isst in der Ruhezeit.
        /// </summary>
        bool BrauchtEssen(int r, int t)
        {
            for (DateTime z = essenBeginn; z < essenEnde; z = z.AddHours(1))
                if (Ruht(r, z.Hour)) return false;
            return true;
        }

        /// <summary>In wie vielen der restlichen Stunden dieses Essensfensters ist die Person im Dienst?</summary>
        int EssensChancen(int r, int t)
        {
            int chancen = 0;
            for (int s = t; s < plan.Stunden; s++)
            {
                DateTime z = plan.Start.AddHours(s);
                if (z >= essenEnde) break;
                if (!Ruht(r, z.Hour)) chancen++;
            }
            return chancen;
        }
    }
}
