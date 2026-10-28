using System;
using System.Collections.Generic;
using System.Linq;
using ShiftDesk.Modell;

namespace ShiftDesk.Planung
{
    // Zuteilung einer einzelnen Stunde. Der Ablauf über alle Stunden steht in Planer.cs.
    public partial class Planer
    {
        /// <summary>Kosten für «Person darf diesen Platz nicht besetzen».</summary>
        const double SPERRE = 1e6;

        /// <summary>Grundkosten für einen Platz, der unbesetzt bleibt.</summary>
        const double LEER = 1e4;

        /// <summary>Aufschlag für Nicht-Fahrer auf dem Fahrerposten: Fahrer gehen vor.</summary>
        const double NICHTFAHRER = 20;

        /// <summary>Belohnung, mitten im Block auf dem Posten zu bleiben.</summary>
        const double BLEIBEN = 30;

        /// <summary>
        /// Strafe, über die erlaubte Zeit hinaus draussen zu bleiben (&gt; 4 × <see cref="BLEIBEN"/>: auch ein Team,
        /// in dem nur einer zu lange draussen war, tauscht mit einem Team von drinnen).
        /// </summary>
        const double DRAUSSEN = 130;

        /// <summary>Strafe, wenn die Teams für eine Ablösung anders zusammengesetzt werden.</summary>
        const double TEAMWECHSEL = 4;

        /// <summary>Zusammensetzung der laufenden Ablösung (Teams und Einzelne), gilt bis zur nächsten Ablösung.</summary>
        List<int[]> einheitenBlock = new List<int[]>();

        /// <summary>Ein zu besetzender Platz auf einem Posten.</summary>
        class Platz
        {
            /// <summary>Index des Postens.</summary>
            public int Posten;

            /// <summary>2 = Doppelplatz für ein Zweierteam, 1 = Einzelplatz.</summary>
            public int Groesse;

            /// <summary>Kosten, wenn der Platz unbesetzt bleibt (weiter oben in der Tabelle = teurer).</summary>
            public double Leer;

            /// <summary>Der erste Platz eines Fahrerpostens ist für einen Fahrer reserviert.</summary>
            public bool NurFahrer;
        }

        /// <summary>Ergebnis einer Zuteilung, bevor sie in den Plan übernommen wird.</summary>
        class Verteilung
        {
            /// <summary>Teams (zwei Zeilen) und Einzelne (eine Zeile).</summary>
            public List<int[]> Einheiten = new List<int[]>();

            /// <summary>Je Einheit: Postenindex, Auffangposten oder <see cref="Plan.FREI"/>.</summary>
            public int[] Posten = new int[0];

            /// <summary>Je Einheit: hat einen zu besetzenden Platz bekommen.</summary>
            public bool[] HatPlatz = new bool[0];

            /// <summary>Unbesetzte Plätze: Posten und ob es der Fahrerplatz war.</summary>
            public List<KeyValuePair<int, bool>> Unbesetzt = new List<KeyValuePair<int, bool>>();

            /// <summary>Anzahl unbesetzter Einzelplätze (dann lohnt es sich, Teams aufzuteilen).</summary>
            public int LeereEinzelplaetze;

            /// <summary>Gesamtkosten; die günstigste Verteilung gewinnt.</summary>
            public double Kosten;
        }

        /// <summary>
        /// Teilt die verfügbaren Personen einer Stunde den Posten zu und trägt das Ergebnis in den Plan ein.
        /// </summary>
        /// <remarks>
        /// Zweierteams sind so oft wie möglich zusammen. Gehen die Leute nicht in Teams auf, sucht der Planer bei
        /// jeder Ablösung die beste Zusammensetzung: wer übrig bleibt, bildet mit einem anderen Übrigen ein Paar
        /// oder tritt an die Stelle eines Teammitglieds (das dann allein ist), und Teams, für die kein Posten mit
        /// zwei Plätzen frei ist, teilen sich auf zwei Einzelplätze auf (z. B. beide Zuko). Bis zur nächsten
        /// Ablösung bleibt die Zusammensetzung gleich.
        /// </remarks>
        /// <param name="t">Stunde im Plan.</param>
        /// <param name="h">Stunde des Tages.</param>
        /// <param name="zeilen">Zeilen, die in dieser Stunde im Dienst sind.</param>
        /// <param name="unterbesetzt">Sammelt Posten → Stunden mit unbesetzten Plätzen.</param>
        /// <param name="ohneFahrer">Sammelt Fahrerposten → Stunden ohne Fahrer.</param>
        /// <param name="fortsetzen">Mitten in einer Ablösung: möglichst jeder bleibt auf seinem Posten.</param>
        void Zuteilen(int t, int h, List<int> zeilen, Dictionary<int, List<int>> unterbesetzt, Dictionary<int, List<int>> ohneFahrer, bool fortsetzen)
        {
            // Durchschnitt pro Posten unter den Personen, die ihn besetzen dürfen
            var schnitt = new double[cfg.Posten.Count];
            for (int k = 0; k < cfg.Posten.Count; k++)
            {
                var erlaubt = zeilen.Where(r => Konfiguration.Darf(plan.Zeilen[r], cfg.Posten[k])).ToList();
                schnitt[k] = erlaubt.Count == 0 ? 0 : erlaubt.Average(r => (double)anzahl[r, k]);
            }

            var basis = fortsetzen ? EinheitenFortsetzen(zeilen) : Teams(zeilen);
            var beste = Verteilen(t, h, basis, schnitt, fortsetzen);
            if (!fortsetzen && cfg.Zweierteams)
                foreach (var variante in Varianten(basis))
                {
                    var v = Verteilen(t, h, variante, schnitt, fortsetzen);
                    v.Kosten += TEAMWECHSEL;
                    if (v.Kosten < beste.Kosten) beste = v;
                }
            einheitenBlock = beste.Einheiten;

            for (int i = 0; i < beste.Einheiten.Count; i++)
                foreach (int r in beste.Einheiten[i])
                {
                    int k = beste.Posten[i];
                    plan.Z[r, t] = k;
                    if (k >= 0) anzahl[r, k]++;
                }
            // Unbesetzte Plätze melden; der Fahrerplatz eines Fahrerpostens separat («ohne Fahrer»)
            foreach (var u in beste.Unbesetzt)
            {
                var ziel = u.Value ? ohneFahrer : unterbesetzt;
                List<int> liste;
                if (!ziel.TryGetValue(u.Key, out liste))
                    ziel[u.Key] = liste = new List<int>();
                if (liste.Count == 0 || liste[liste.Count - 1] != t) liste.Add(t);
            }
        }

        // ---------------------------------------------------------------- Zusammensetzung

        /// <summary>Die Zweierteams aus der Personen-Tabelle, soweit beide Mitglieder verfügbar sind; alle anderen allein.</summary>
        List<int[]> Teams(List<int> zeilen)
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
            return einheiten;
        }

        /// <summary>
        /// Mitten in einer Ablösung gilt die Zusammensetzung von deren Beginn weiter. Wer seither in die Ruhe
        /// gegangen ist, fehlt; wer aus der Ruhe gekommen ist, kommt mit seinem Team dazu.
        /// </summary>
        List<int[]> EinheitenFortsetzen(List<int> zeilen)
        {
            var frei = new HashSet<int>(zeilen);
            var einheiten = new List<int[]>();
            foreach (var e in einheitenBlock)
            {
                var da = e.Where(frei.Contains).ToArray();
                if (da.Length == 0) continue;
                einheiten.Add(da);
                foreach (int r in da) frei.Remove(r);
            }
            einheiten.AddRange(Teams(zeilen.Where(frei.Contains).ToList()));
            return einheiten;
        }

        /// <summary>
        /// Andere Zusammensetzungen für eine Ablösung: zwei Übrige bilden ein Paar, oder ein Übriger tritt an die
        /// Stelle eines Teammitglieds, das dann allein ist. So kommt auch, wer übrig bleibt, auf Posten für zwei.
        /// </summary>
        IEnumerable<List<int[]>> Varianten(List<int[]> basis)
        {
            Func<int, int, bool> passt = (a, b) => !plan.Zeilen[a].Fahrer && !plan.Zeilen[b].Fahrer
                && plan.Zeilen[a].Funktion == plan.Zeilen[b].Funktion;
            for (int i = 0; i < basis.Count; i++)
            {
                if (basis[i].Length != 1) continue;
                int s = basis[i][0];
                for (int j = 0; j < basis.Count; j++)
                {
                    if (j == i) continue;
                    var e = basis[j];
                    if (e.Length == 1 && j > i && passt(s, e[0]))
                        yield return Ersetzt(basis, i, j, new[] { s, e[0] });
                    else if (e.Length == 2 && passt(s, e[0]) && passt(s, e[1]))
                    {
                        yield return Ersetzt(basis, i, j, new[] { s, e[0] }, new[] { e[1] });
                        yield return Ersetzt(basis, i, j, new[] { s, e[1] }, new[] { e[0] });
                    }
                }
            }
        }

        /// <summary>Kopie der Einheiten ohne die beiden an <paramref name="weg1"/> und <paramref name="weg2"/>, dafür mit <paramref name="dazu"/>.</summary>
        static List<int[]> Ersetzt(List<int[]> einheiten, int weg1, int weg2, params int[][] dazu)
        {
            var neu = einheiten.Where((e, i) => i != weg1 && i != weg2).ToList();
            neu.AddRange(dazu);
            return neu;
        }

        // ---------------------------------------------------------------- Verteilen

        /// <summary>
        /// Rechnet eine Zusammensetzung durch. Bleiben Einzelplätze leer, während Teams keinen Platz haben, werden
        /// so viele dieser Teams aufgeteilt, wie es braucht.
        /// </summary>
        Verteilung Verteilen(int t, int h, List<int[]> einheiten, double[] schnitt, bool fortsetzen)
        {
            var v = Durchrechnen(t, h, einheiten, schnitt, fortsetzen);
            while (v.LeereEinzelplaetze > 0)
            {
                var aufteilen = Enumerable.Range(0, v.Einheiten.Count).Where(i => v.Einheiten[i].Length == 2 && !v.HatPlatz[i])
                    .Take((v.LeereEinzelplaetze + 1) / 2).ToList();
                if (aufteilen.Count == 0) break;
                var neu = new List<int[]>();
                for (int i = 0; i < v.Einheiten.Count; i++)
                {
                    if (aufteilen.Contains(i))
                        neu.AddRange(v.Einheiten[i].Select(r => new[] { r }));
                    else
                        neu.Add(v.Einheiten[i]);
                }
                var w = Durchrechnen(t, h, neu, schnitt, fortsetzen);
                if (w.Kosten >= v.Kosten) break;
                v = w;
            }
            return v;
        }

        /// <summary>
        /// Die Plätze, die in dieser Stunde zu besetzen sind, in der Reihenfolge der Posten-Tabelle (die oberen
        /// bekommen auch die Teams zuerst).
        /// </summary>
        /// <remarks>
        /// Doppelplätze gibt es nur so viele, wie Teams da sind, die den Posten besetzen dürfen (und insgesamt nicht
        /// mehr als Teams); der Rest sind Einzelplätze (z. B. Fahrerposten, wenn die Fahrer einzeln sind).
        /// Alle Posten sollen voll besetzt sein; reichen die Leute nicht, gilt die Reihenfolge (weiter oben =
        /// wichtiger). Der erste Platz eines Fahrerpostens ist für einen Fahrer reserviert (er ist auch der
        /// wichtigste); die weiteren Plätze bekommen Fahrer, wenn welche da sind, sonst andere Leute.
        /// </remarks>
        List<Platz> PlaetzeBilden(int h, List<int[]> einheiten, Func<int[], Posten, bool> erlaubtFuer)
        {
            var plaetze = new List<Platz>();
            int paareUebrig = einheiten.Count(e => e.Length == 2);
            for (int k = 0; k < cfg.Posten.Count; k++)
            {
                var p = cfg.Posten[k];
                if (p.Typ == PostenTyp.Auffang || !p.OffenUm(h)) continue;
                // Doppelplätze auf dem Fahrerposten nur für reine Fahrerteams
                int doppel = Math.Min(Math.Min(p.Anzahl / 2, paareUebrig), einheiten.Count(e => e.Length == 2 && erlaubtFuer(e, p)
                    && (p.Typ != PostenTyp.Fahrer || e.All(r => plan.Zeilen[r].Fahrer))));
                paareUebrig -= doppel;
                for (int i = 0; i < doppel; i++)
                    plaetze.Add(new Platz { Posten = k, Groesse = 2, Leer = p.Typ == PostenTyp.Fahrer && i == 0 ? 2 * LEER : LEER });
                for (int i = 0; i < p.Anzahl - 2 * doppel; i++)
                {
                    bool erster = p.Typ == PostenTyp.Fahrer && doppel == 0 && i == 0;
                    plaetze.Add(new Platz { Posten = k, Groesse = 1, NurFahrer = erster, Leer = erster ? 2 * LEER : LEER });
                }
            }
            for (int j = 0; j < plaetze.Count; j++) plaetze[j].Leer += (plaetze.Count - j) * 100;
            return plaetze;
        }

        /// <summary>
        /// Eine Zuteilung mit der Ungarischen Methode. Spalten: die zu besetzenden Plätze und je ein «Rest»-Platz
        /// pro Einheit (Auffangposten oder frei). Zusätzliche Leer-Zeilen erlauben es, Plätze unbesetzt zu lassen,
        /// wenn Leute fehlen. Ein Zweierteam ist eine Einheit der Grösse 2 und braucht einen Doppelplatz.
        /// </summary>
        /// <remarks>
        /// Mitten in einer Ablösung (<paramref name="fortsetzen"/>) bleibt jeder auf seinem Posten: das Bleiben ist
        /// in den Kosten so stark belohnt, dass nur zwei Dinge einen Wechsel auslösen: wer auf dem Auffangposten
        /// sitzt, rückt auf einen frei gewordenen Platz nach, und wer draussen die erlaubte Zeit erreicht hat,
        /// tauscht mit jemandem von drinnen (Gesunderhaltung geht vor).
        /// </remarks>
        Verteilung Durchrechnen(int t, int h, List<int[]> einheiten, double[] schnitt, bool fortsetzen)
        {
            Func<int[], Posten, bool> erlaubtFuer = (e, p) => e.All(r => Konfiguration.Darf(plan.Zeilen[r], p));
            var plaetze = PlaetzeBilden(h, einheiten, erlaubtFuer);
            var mitPlatz = new HashSet<int>(plaetze.Select(p => p.Posten));

            int n = einheiten.Count, m = plaetze.Count, N = n + m;
            var v = new Verteilung { Einheiten = einheiten, Posten = new int[n], HatPlatz = new bool[n] };
            if (N == 0) return v;

            var rest = einheiten.Select(e => cfg.Posten.FindIndex(p => p.Typ == PostenTyp.Auffang && p.OffenUm(h) && erlaubtFuer(e, p))).ToArray();
            // Kosten je Einheit und Posten (Nicht-Fahrer auf dem Fahrerposten nur, wenn kein Fahrer frei ist: kleiner Aufschlag)
            var kostenPosten = new double[n, cfg.Posten.Count];
            for (int i = 0; i < n; i++)
                for (int k = 0; k < cfg.Posten.Count; k++)
                    if (mitPlatz.Contains(k))
                        kostenPosten[i, k] = einheiten[i].Sum(r => Kosten(r, k, t, schnitt, fortsetzen)
                            + (cfg.Posten[k].Typ == PostenTyp.Fahrer && !plan.Zeilen[r].Fahrer ? NICHTFAHRER : 0));

            var kosten = new double[N, N];
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    if (i < n && j < m)
                        kosten[i, j] = plaetze[j].Groesse == einheiten[i].Length && erlaubtFuer(einheiten[i], cfg.Posten[plaetze[j].Posten])
                            && (!plaetze[j].NurFahrer || einheiten[i].All(r => plan.Zeilen[r].Fahrer))
                            ? kostenPosten[i, plaetze[j].Posten] : SPERRE;
                    else if (i < n)
                        kosten[i, j] = einheiten[i].Sum(r => Kosten(r, rest[i], t, schnitt, fortsetzen));
                    else if (j < m)
                        kosten[i, j] = plaetze[j].Leer;
                    else
                        kosten[i, j] = 0;
                }

            int[] wahl = UngarischeMethode.Loesen(kosten, N);
            for (int i = 0; i < N; i++) v.Kosten += kosten[i, wahl[i]];
            var besetzt = new bool[m];
            for (int i = 0; i < n; i++)
            {
                int j = wahl[i];
                v.HatPlatz[i] = j < m;
                v.Posten[i] = j < m ? plaetze[j].Posten : rest[i];
                if (j < m) besetzt[j] = true;
            }
            for (int j = 0; j < m; j++)
            {
                if (besetzt[j]) continue;
                v.Unbesetzt.Add(new KeyValuePair<int, bool>(plaetze[j].Posten, plaetze[j].NurFahrer));
                if (plaetze[j].Groesse == 1) v.LeereEinzelplaetze++;
            }
            return v;
        }

        /// <summary>
        /// Kosten, wenn Zeile r in Stunde t auf Posten k kommt: Abwechslung und Gerechtigkeit, am Stück bleiben
        /// (Wacht Kdt, mitten im Block), Gesunderhaltung draussen und Essen. Ein kleiner Zufall macht jede
        /// Erstellung zu einer neuen Variante.
        /// </summary>
        /// <param name="r">Zeile (Person).</param>
        /// <param name="t">Stunde im Plan.</param>
        /// <param name="schnitt">Je Posten, wie oft die Verfügbaren ihn im Schnitt schon hatten.</param>
        /// <param name="fortsetzen">Mitten in einer Ablösung.</param>
        /// <param name="k">Postenindex oder kleiner als 0 (kein Posten: nur der Zufall zählt).</param>
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
    }
}
