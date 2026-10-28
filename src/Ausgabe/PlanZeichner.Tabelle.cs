using System;
using System.Collections.Generic;
using System.Drawing;
using ShiftDesk.Planung;

namespace ShiftDesk.Ausgabe
{
    // Die Tabelle des Plans: eine Zeile pro Person, eine Spalte pro Stunde.
    public sealed partial class PlanZeichner
    {
        // Masse der Tabelle (siehe TabelleAnordnen)
        float zeichenH, tabOben, stundeH, kopfY, zeileH, tabUnten;

        /// <summary>Welche Essens-Zeichen auf dieser Seite vorkommen (nur diese werden unter der Tabelle erklärt).</summary>
        bool essenVorDa, essenNachDa, essenRuheDa, essenReserveDa;

        /// <summary>Höhe der Fusszeile.</summary>
        float FussH
        {
            get { return 12 * u; }
        }

        /// <summary>Oberkante von Zeile r.</summary>
        float ZeileY(int r)
        {
            return kopfY + r * zeileH;
        }

        /// <summary>Linker Rand der Stundenspalte c.</summary>
        float SpalteX(int c)
        {
            return xs + c * spalteW;
        }

        /// <summary>Die Tabelle füllt den Platz zwischen Kopf und Fuss; die Zeilen werden höchstens 30 Einheiten hoch.</summary>
        void TabelleAnordnen()
        {
            int n = plan.Zeilen.Count;
            zeichenH = 16 * u;
            float neH = plan.NichtEingeteilt.Count > 0 ? zeichenH : 0;
            float fuss = 5 * u + zeichenH + neH + 4 * u + FussH;
            tabOben = Math.Max(kopfUnten, oben + 30 * u) + 12 * u;
            stundeH = 30 * u;
            kopfY = tabOben + stundeH;
            zeileH = Math.Min(30 * u, (b.Bottom - fuss - kopfY) / Math.Max(1, n));
            tabUnten = ZeileY(n);
        }

        /// <summary>Tabellenkopf: Diagonale mit «Zeit» und «Wer».</summary>
        void Tabellenkopf()
        {
            g.DrawLine(duenn, b.Left, tabOben, xs, kopfY);
            g.DrawString(t.Zeit, fKopf, tText, new RectangleF(b.Left, tabOben + 2 * u, xs - b.Left - 5 * u, 14 * u), rechtsMitte);
            g.DrawString(t.Wer, fKopf, tText, new RectangleF(b.Left + 4 * u, kopfY - 15 * u, gradW + nameW, 14 * u), links);
        }

        void Zeilen()
        {
            using (var fName = new Font(Schrift, Math.Min(9.5f * u, zeileH * 0.44f), FontStyle.Bold, GraphicsUnit.World))
            using (var fGrad = new Font(Schrift, Math.Min(9.5f * u, zeileH * 0.44f), GraphicsUnit.World))
            using (var fZeichen = new Font(Schrift, Math.Min(Math.Min(11 * u, zeileH * 0.46f), spalteW * 0.42f), FontStyle.Bold, GraphicsUnit.World))
                for (int r = 0; r < plan.Zeilen.Count; r++)
                    Zeile(r, fName, fGrad, fZeichen);
        }

        /// <summary>
        /// Wo in einer Zeile gegessen wird: als Zeichen in der Ecke eines Postenfelds (oben links = vor der Ablösung
        /// essen, oben rechts = nach der Ablösung), orange, wenn eine Reserve zum Essen ablöst, und mitten in der
        /// Ruhezeit gross in der Stunde.
        /// </summary>
        class Essensorte
        {
            public readonly List<RectangleF> Links = new List<RectangleF>(), Rechts = new List<RectangleF>();
            public readonly List<RectangleF> ReserveLinks = new List<RectangleF>(), ReserveRechts = new List<RectangleF>();
            public bool[] Mitte;
        }

        /// <summary>Eine Zeile: Grad und Name (mit Auto bei Fahrern), dann die Felder der Ablösungen mit Buchstaben und Essen.</summary>
        void Zeile(int r, Font fName, Font fGrad, Font fZeichen)
        {
            float zy = ZeileY(r);
            var p = plan.Zeilen[r];
            g.DrawString(p.Grad, fGrad, tText, new RectangleF(b.Left + 4 * u, zy, gradW - 6 * u, zeileH), links);
            var nameFeld = new RectangleF(b.Left + gradW + 4 * u, zy, nameW - 9 * u, zeileH);
            g.DrawString(p.Name, fName, tText, nameFeld, rechtsMitte);
            if (p.Fahrer)
            {
                float ik = Math.Min(zeileH * 0.62f, 12 * u);
                float nw = Math.Min(nameFeld.Width - ik - 3 * u, g.MeasureString(p.Name, fName).Width);
                Druckzeichen.AutoSymbol(g, new RectangleF(nameFeld.Right - nw - ik - 3 * u, zy + (zeileH - ik) / 2, ik, ik));
            }

            int[] feldA, feldE;
            AbloesungsFelder(r, out feldA, out feldE);
            Func<int, RectangleF> feldVon = c => RectangleF.FromLTRB(SpalteX(feldA[c]), zy, SpalteX(feldE[c] + 1), zy + zeileH);
            var essen = EssenEinordnen(r, feldA, feldE, feldVon);

            float es = Math.Min(spalteW, zeileH) * 0.5f;
            for (int c0 = 0; c0 < stunden; c0 = feldE[c0] + 1)
            {
                int k = plan.Z[r, erste + c0];
                var feld = feldVon(c0);
                using (var f = new SolidBrush(k >= 0 ? Druckzeichen.Pastell(cfg.Posten[k].Farbe) : k == Plan.RUHE ? Druckzeichen.Leer : Color.White))
                    g.FillRectangle(f, feld);
                g.DrawRectangle(gitter, feld.X, feld.Y, feld.Width, feld.Height);
                if (k >= 0)
                {
                    // in schmalen Feldern weicht der Buchstabe dem Essens-Zeichen aus
                    var platz = feld;
                    if (feld.Width < spalteW * 1.5f)
                    {
                        if (essen.Links.Contains(feld) || essen.ReserveLinks.Contains(feld)) platz = RectangleF.FromLTRB(platz.Left + es * 0.55f, platz.Top, platz.Right, platz.Bottom);
                        if (essen.Rechts.Contains(feld) || essen.ReserveRechts.Contains(feld)) platz = RectangleF.FromLTRB(platz.Left, platz.Top, platz.Right - es * 0.55f, platz.Bottom);
                    }
                    Druckzeichen.Mittig(g, Druckzeichen.Kuerzel(k), fZeichen, tText, platz);
                }
                else if (essen.Mitte[c0])
                    Druckzeichen.EssenSymbol(g, RectangleF.Inflate(feld, -zeileH * 0.06f, -zeileH * 0.06f));
                else if (k == Plan.RUHE)
                    g.DrawString("-", fZeichen, tText, feld, mitte);
            }

            essenVorDa |= essen.Links.Count > 0;
            essenNachDa |= essen.Rechts.Count > 0;
            foreach (var feld in essen.Links)
                Druckzeichen.EssenSymbol(g, new RectangleF(feld.Left + 1 * u, feld.Top + 1 * u, es, es));
            foreach (var feld in essen.Rechts)
                Druckzeichen.EssenSymbol(g, new RectangleF(feld.Right - es - 1 * u, feld.Top + 1 * u, es, es));
            foreach (var feld in essen.ReserveLinks)
                Druckzeichen.EssenSymbol(g, new RectangleF(feld.Left + 1 * u, feld.Top + 1 * u, es, es), true);
            foreach (var feld in essen.ReserveRechts)
                Druckzeichen.EssenSymbol(g, new RectangleF(feld.Right - es - 1 * u, feld.Top + 1 * u, es, es), true);
        }

        /// <summary>
        /// Felder einer Zeile: ein Posten bleibt bis zur nächsten Ablösung ein einziges Feld (Spalten
        /// <paramref name="feldA"/> bis <paramref name="feldE"/>), Ruhe und Freizeit gehen Stunde für Stunde.
        /// </summary>
        void AbloesungsFelder(int r, out int[] feldA, out int[] feldE)
        {
            feldA = new int[stunden];
            feldE = new int[stunden];
            for (int c0 = 0, c1; c0 < stunden; c0 = c1 + 1)
            {
                int k = plan.Z[r, erste + c0];
                c1 = c0;
                if (k >= 0)
                    while (c1 + 1 < stunden && plan.Z[r, erste + c1 + 1] == k && !cfg.NeueAbloesungUm(von.AddHours(c1 + 1).Hour))
                        c1++;
                for (int c = c0; c <= c1; c++) { feldA[c] = c0; feldE[c] = c1; }
            }
        }

        /// <summary>Ordnet jede Essensstunde der Zeile einem Feld und einer Ecke zu (siehe <see cref="Essensorte"/>).</summary>
        Essensorte EssenEinordnen(int r, int[] feldA, int[] feldE, Func<int, RectangleF> feldVon)
        {
            var essen = new Essensorte { Mitte = new bool[stunden] };
            Func<int, bool> posten = c => c >= 0 && c < stunden && plan.Z[r, erste + c] >= 0;
            for (int c = 0; c < stunden; c++)
            {
                if (plan.EssenAbloesung[r, erste + c] && posten(c))
                {
                    if (c - feldA[c] < feldE[c] - c) essen.ReserveLinks.Add(feldVon(c));
                    else essen.ReserveRechts.Add(feldVon(c));
                    essenReserveDa = true;
                    continue;
                }
                if (!plan.Essen[r, erste + c]) continue;
                if (posten(c))
                {
                    if (c - feldA[c] < feldE[c] - c) essen.Links.Add(feldVon(c));
                    else essen.Rechts.Add(feldVon(c));
                }
                else if (posten(c - 1)) essen.Rechts.Add(feldVon(c - 1));
                else if (posten(c + 1)) essen.Links.Add(feldVon(c + 1));
                else essen.Mitte[c] = essenRuheDa = true;
            }
            return essen;
        }

        /// <summary>Linien: dick zwischen Blöcken, Zweierteams und Tagen, sonst dünn; Rahmen um die Tabelle.</summary>
        void Linien()
        {
            int n = plan.Zeilen.Count;
            var teamGroesse = new Dictionary<int, int>();
            for (int r = 0; r < n; r++)
            {
                int anz;
                teamGroesse.TryGetValue(plan.Team[r], out anz);
                teamGroesse[plan.Team[r]] = anz + 1;
            }
            for (int r = 0; r < n - 1; r++)
            {
                bool trenn = plan.Block[r + 1] != plan.Block[r]
                    || (plan.Team[r + 1] != plan.Team[r] && (teamGroesse[plan.Team[r]] > 1 || teamGroesse[plan.Team[r + 1]] > 1));
                float y = ZeileY(r + 1);
                g.DrawLine(trenn ? dick : gitter, b.Left, y, trenn ? b.Right : xs, y);
            }
            g.DrawLine(duenn, b.Left + gradW, kopfY, b.Left + gradW, tabUnten);
            g.DrawLine(duenn, xs, tabOben, xs, tabUnten);
            for (int c = 1; c < stunden; c++)
                if (von.AddHours(c).Hour == 0)
                    g.DrawLine(dick, SpalteX(c), tabOben, SpalteX(c), tabUnten);
            g.DrawLine(dick, b.Left, kopfY, b.Right, kopfY);
            g.DrawRectangle(dick, b.Left, tabOben, b.Width, tabUnten - tabOben);
        }

        /// <summary>Stunden senkrecht auf den Linien (0000 bis 2400), mit einer Lücke in den Linien dahinter.</summary>
        void Stundenzahlen()
        {
            for (int c = 0; c <= stunden; c++)
            {
                DateTime z = von.AddHours(c);
                string text = c == stunden && c > 0 && z.Hour == 0 ? "2400" : z.ToString("HHmm");
                var gross = g.MeasureString(text, fStunde);
                float th = fStunde.GetHeight(g) * 0.8f;
                var alt = g.Transform;
                g.TranslateTransform(SpalteX(c), kopfY - 3 * u);
                g.RotateTransform(-90);
                g.FillRectangle(Brushes.White, -1.5f * u, -th / 2, gross.Width + 1.5f * u, th);
                g.DrawString(text, fStunde, tText, new RectangleF(0, -6 * u, stundeH, 12 * u), stundeTh);
                g.Transform = alt;
            }
        }
    }
}
