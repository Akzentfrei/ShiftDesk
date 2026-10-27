using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;

namespace ShiftDesk
{
    // Texte des Ausdrucks in einer Sprache. Namen von Posten, Funktionen und Personen bleiben, wie sie erfasst sind.
    public class Druck
    {
        public string Kuerzel, Name;
        public CultureInfo Kultur;
        public string DatumLang;
        public string Titel, Einheit, Fuer, Datum, Legende, Zeit, Wer;
        public string Zeichen, Ruhezeit, OhnePosten, EssenVor, EssenNach, EssenRuhe, Draussen, Fahrer, NichtEingeteilt, Erstellt, Seite;

        public static readonly Druck[] Sprachen =
        {
            new Druck { Kuerzel = "de", Name = "Deutsch", Kultur = new CultureInfo("de-CH"), DatumLang = "dddd, dd.MM.yyyy",
                Titel = "Ablösungsliste", Einheit = "Einheit", Fuer = "Für", Datum = "Datum", Legende = "Legende", Zeit = "Zeit", Wer = "Wer",
                Zeichen = "Zeichen", Ruhezeit = "Ruhezeit", OhnePosten = "ohne Posten", EssenVor = "Essen vor der Ablösung", EssenNach = "Essen nach der Ablösung", EssenRuhe = "Essen in der Ruhezeit", Draussen = "Draussen", Fahrer = "Fahrer",
                NichtEingeteilt = "Nicht eingeteilt", Erstellt = "Erstellt mit ShiftDesk am {0}", Seite = "Seite {0} von {1}" },
            new Druck { Kuerzel = "fr", Name = "Français", Kultur = new CultureInfo("fr-CH"), DatumLang = "dddd dd.MM.yyyy",
                Titel = "Liste de relève", Einheit = "Unité", Fuer = "Pour", Datum = "Date", Legende = "Légende", Zeit = "Heure", Wer = "Qui",
                Zeichen = "Signes", Ruhezeit = "Repos", OhnePosten = "sans poste", EssenVor = "Repas avant la relève", EssenNach = "Repas après la relève", EssenRuhe = "Repas pendant le repos", Draussen = "À l'extérieur", Fahrer = "Chauffeur",
                NichtEingeteilt = "Non engagés", Erstellt = "Créé avec ShiftDesk le {0}", Seite = "Page {0} sur {1}" },
            new Druck { Kuerzel = "it", Name = "Italiano", Kultur = new CultureInfo("it-CH"), DatumLang = "dddd dd.MM.yyyy",
                Titel = "Lista dei turni", Einheit = "Unità", Fuer = "Per", Datum = "Data", Legende = "Legenda", Zeit = "Ora", Wer = "Chi",
                Zeichen = "Simboli", Ruhezeit = "Riposo", OhnePosten = "senza posto", EssenVor = "Pasto prima del cambio", EssenNach = "Pasto dopo il cambio", EssenRuhe = "Pasto durante il riposo", Draussen = "All'esterno", Fahrer = "Autista",
                NichtEingeteilt = "Non impiegati", Erstellt = "Creato con ShiftDesk il {0}", Seite = "Pagina {0} di {1}" },
            new Druck { Kuerzel = "en", Name = "English", Kultur = new CultureInfo("en-GB"), DatumLang = "dddd, dd.MM.yyyy",
                Titel = "Relief Roster", Einheit = "Unit", Fuer = "For", Datum = "Date", Legende = "Legend", Zeit = "Time", Wer = "Who",
                Zeichen = "Symbols", Ruhezeit = "Rest", OhnePosten = "no post", EssenVor = "Meal before relief", EssenNach = "Meal after relief", EssenRuhe = "Meal during rest", Draussen = "Outdoors", Fahrer = "Driver",
                NichtEingeteilt = "Not assigned", Erstellt = "Created with ShiftDesk on {0}", Seite = "Page {0} of {1}" },
        };

        public static Druck In(string kuerzel)
        {
            return Sprachen.FirstOrDefault(s => s.Kuerzel == kuerzel) ?? Sprachen[0];
        }
    }

    // Zeichnet eine Seite (24 Stunden) des Plans, für Vorschau und Drucker gleich. Immer hell (Papier).
    // Aufbau nach dem Formular «Ablösungsliste»: Kopf mit Feldern und Legende, Tabelle mit Rahmen,
    // Stunden auf den Linien, Posten als Buchstaben auf hellen Farben, «-» für Ruhe.
    public static class Zeichner
    {
        const string Schrift = "Arial";

        static readonly Color TextFarbe = Color.FromArgb(17, 17, 17);
        static readonly Color Grau = Color.FromArgb(90, 90, 90);
        static readonly Color HellGrau = Color.FromArgb(150, 150, 150);
        static readonly Color Rahmen = Color.FromArgb(34, 34, 34);
        static readonly Color Gitter = Color.FromArgb(200, 200, 200);
        static readonly Color Leer = Color.FromArgb(243, 244, 246);
        static readonly Color Schnee = Color.FromArgb(37, 99, 235);

        public static void Seite(Graphics g, RectangleF b, Plan plan, int seite)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            var cfg = plan.Cfg;
            var t = Druck.In(cfg.Sprache);
            float u = b.Width / 1000f;

            int erste = seite * 24;
            int stunden = Math.Min(24, plan.Stunden - erste);
            DateTime von = plan.Start.AddHours(erste);
            DateTime bis = von.AddHours(stunden);

            var links = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            var rechtsMitte = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            var mitte = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

            using (var tText = new SolidBrush(TextFarbe))
            using (var tGrau = new SolidBrush(Grau))
            using (var tHell = new SolidBrush(HellGrau))
            using (var dick = new Pen(Rahmen, u * 1.3f))
            using (var duenn = new Pen(Rahmen, u * 0.6f))
            using (var gitter = new Pen(Gitter, u * 0.5f))
            using (var fTitel = new Font(Schrift, 19 * u, FontStyle.Bold, GraphicsUnit.World))
            using (var fFeld = new Font(Schrift, 9.5f * u, GraphicsUnit.World))
            using (var fWert = new Font(Schrift, 10.5f * u, GraphicsUnit.World))
            using (var fKopf = new Font(Schrift, 9.5f * u, FontStyle.Bold, GraphicsUnit.World))
            using (var fStunde = new Font(Schrift, 8.5f * u, FontStyle.Bold, GraphicsUnit.World))
            using (var fKlein = new Font(Schrift, 8 * u, GraphicsUnit.World))
            using (var fKleinFett = new Font(Schrift, 8 * u, FontStyle.Bold, GraphicsUnit.World))
            {
                float gradW = 40 * u, nameW = 140 * u;
                float xs = b.Left + gradW + nameW;
                float rasterW = b.Right - xs;
                float spalteW = rasterW / 24f;

                // Logo oben links, darunter der Titel; Felder und Legende über dem Raster
                float oben = b.Top;
                var logo = ArmeeLogo;
                if (logo != null)
                {
                    float lh = 30 * u;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(logo, new RectangleF(b.Left, b.Top, lh * logo.Width / logo.Height, lh));
                    oben += lh + 12 * u;
                }
                g.DrawString(t.Titel, fTitel, tText, b.Left - 1.5f * u, oben - 2 * u);
                float fx0 = Math.Max(xs, b.Left + g.MeasureString(t.Titel, fTitel).Width + 10 * u);
                float feldW = b.Right - fx0;

                float feldH = 22 * u;
                string datum = von.Hour == 0 && stunden == 24
                    ? von.ToString(t.DatumLang, t.Kultur)
                    : von.ToString("ddd dd.MM.yyyy HH:00", t.Kultur) + " – " + bis.ToString("ddd dd.MM.yyyy HH:00", t.Kultur);
                var felder = new[]
                {
                    new { Name = t.Einheit, Wert = cfg.Einheit ?? "", X = 0f, W = 0.28f },
                    new { Name = t.Fuer, Wert = cfg.Name ?? "", X = 0.28f, W = 0.32f },
                    new { Name = t.Datum, Wert = datum, X = 0.60f, W = 0.40f },
                };
                float labelW = felder.Max(f => g.MeasureString(f.Name, fFeld).Width) + 8 * u;
                foreach (var f in felder)
                {
                    float fx = fx0 + f.X * feldW;
                    g.DrawString(f.Name, fFeld, tText, new RectangleF(fx, oben, labelW, feldH), links);
                    g.DrawString(f.Wert, fWert, tText, new RectangleF(fx + labelW, oben, f.W * feldW - labelW - 4 * u, feldH), links);
                }
                g.DrawLine(duenn, fx0, oben + feldH, b.Right, oben + feldH);

                // Legende: Posten mit Buchstaben, spaltenweise wie im Formular
                int pn = cfg.Posten.Count;
                int lSpalten = pn <= 9 ? 3 : pn <= 16 ? 4 : 5;
                int lZeilen = Math.Max(1, (pn + lSpalten - 1) / lSpalten);
                float lZeileH = 12.5f * u, q = 10 * u;
                float ly = oben + feldH + 5 * u;
                float lx0 = fx0 + Math.Max(labelW, g.MeasureString(t.Legende, fFeld).Width + 8 * u);
                float lSpalteW = (b.Right - lx0) / lSpalten;
                g.DrawString(t.Legende, fFeld, tText, new RectangleF(fx0, ly, lx0 - fx0, lZeileH), links);
                for (int k = 0; k < pn; k++)
                {
                    var posten = cfg.Posten[k];
                    float x = lx0 + (k / lZeilen) * lSpalteW, y = ly + (k % lZeilen) * lZeileH;
                    var feld = new RectangleF(x, y + (lZeileH - q) / 2, q, q);
                    using (var f = new SolidBrush(Pastell(posten.Farbe))) g.FillRectangle(f, feld);
                    g.DrawString(Kuerzel(k), fKleinFett, tText, feld, mitte);
                    float tx = x + q + 4 * u, rest = lSpalteW - q - 8 * u;
                    float w = Math.Min(rest, g.MeasureString(posten.Name, fFeld).Width);
                    g.DrawString(posten.Name, fFeld, tText, new RectangleF(tx, y, w + 2 * u, lZeileH), links);
                    tx += w;
                    if (posten.Draussen && rest - w > q)
                    {
                        SchneeSymbol(g, new RectangleF(tx + 2 * u, y + (lZeileH - 9 * u) / 2, 9 * u, 9 * u));
                        tx += 11 * u;
                    }
                    if (!posten.Durchgehend)
                        g.DrawString(Zeit.Text(posten.OffenVon) + "–" + Zeit.Text(posten.OffenBis), fKlein, tGrau,
                            new RectangleF(tx + 3 * u, y, Math.Max(0, x + lSpalteW - tx - 7 * u), lZeileH), links);
                }
                float kopfUnten = ly + lZeilen * lZeileH + 4 * u;
                g.DrawLine(duenn, fx0, kopfUnten, b.Right, kopfUnten);

                // Masse der Tabelle
                int n = plan.Zeilen.Count;
                bool frei = Enumerable.Range(0, n).Any(r => Enumerable.Range(0, stunden).Any(c => plan.Z[r, erste + c] == Plan.FREI));
                float zeichenH = 16 * u;
                float neH = plan.NichtEingeteilt.Count > 0 ? zeichenH : 0;
                float fussH = 12 * u;
                float fuss = 5 * u + zeichenH + neH + 4 * u + fussH;
                float tabOben = Math.Max(kopfUnten, oben + 30 * u) + 12 * u;
                float stundeH = 30 * u;
                float kopfY = tabOben + stundeH;
                float zeileH = Math.Min(30 * u, (b.Bottom - fuss - kopfY) / Math.Max(1, n));
                Func<int, float> zeileY = r => kopfY + r * zeileH;
                Func<int, float> spalteX = c => xs + c * spalteW;
                float tabUnten = zeileY(n);

                // Tabellenkopf: Diagonale mit «Zeit» und «Wer», Stunden senkrecht auf den Linien
                g.DrawLine(duenn, b.Left, tabOben, xs, kopfY);
                g.DrawString(t.Zeit, fKopf, tText, new RectangleF(b.Left, tabOben + 2 * u, xs - b.Left - 5 * u, 14 * u), rechtsMitte);
                g.DrawString(t.Wer, fKopf, tText, new RectangleF(b.Left + 4 * u, kopfY - 15 * u, gradW + nameW, 14 * u), links);
                bool essenVorDa = false, essenNachDa = false, essenRuheDa = false;
                using (var fName = new Font(Schrift, Math.Min(9.5f * u, zeileH * 0.44f), FontStyle.Bold, GraphicsUnit.World))
                using (var fGrad = new Font(Schrift, Math.Min(9.5f * u, zeileH * 0.44f), GraphicsUnit.World))
                using (var fZeichen = new Font(Schrift, Math.Min(Math.Min(11 * u, zeileH * 0.46f), spalteW * 0.42f), FontStyle.Bold, GraphicsUnit.World))
                {
                    for (int r = 0; r < n; r++)
                    {
                        float zy = zeileY(r);
                        var p = plan.Zeilen[r];
                        g.DrawString(p.Grad, fGrad, tText, new RectangleF(b.Left + 4 * u, zy, gradW - 6 * u, zeileH), links);
                        var nameFeld = new RectangleF(b.Left + gradW + 4 * u, zy, nameW - 9 * u, zeileH);
                        g.DrawString(p.Name, fName, tText, nameFeld, rechtsMitte);
                        if (p.Fahrer)
                        {
                            float ik = Math.Min(zeileH * 0.62f, 12 * u);
                            float nw = Math.Min(nameFeld.Width - ik - 3 * u, g.MeasureString(p.Name, fName).Width);
                            AutoSymbol(g, new RectangleF(nameFeld.Right - nw - ik - 3 * u, zy + (zeileH - ik) / 2, ik, ik));
                        }

                        // Posten pro Ablösung als ein Feld (Spalten feldA bis feldE), Ruhe und Freizeit Stunde für Stunde
                        var feldA = new int[stunden];
                        var feldE = new int[stunden];
                        for (int c0 = 0, c1; c0 < stunden; c0 = c1 + 1)
                        {
                            int k = plan.Z[r, erste + c0];
                            c1 = c0;
                            if (k >= 0)
                                while (c1 + 1 < stunden && plan.Z[r, erste + c1 + 1] == k && !NeueAbloesung(cfg, von.AddHours(c1 + 1).Hour))
                                    c1++;
                            for (int c = c0; c <= c1; c++) { feldA[c] = c0; feldE[c] = c1; }
                        }
                        Func<int, bool> posten = c => c >= 0 && c < stunden && plan.Z[r, erste + c] >= 0;
                        Func<int, RectangleF> feldVon = c => RectangleF.FromLTRB(spalteX(feldA[c]), zy, spalteX(feldE[c] + 1), zy + zeileH);

                        // Essen als Zeichen in der Ecke eines Postenfelds: oben links = vor der Ablösung essen,
                        // oben rechts = nach der Ablösung; mitten in der Ruhezeit gross in der Stunde
                        var essenLinks = new List<RectangleF>();
                        var essenRechts = new List<RectangleF>();
                        var essenMitte = new bool[stunden];
                        for (int c = 0; c < stunden; c++)
                        {
                            if (!plan.Essen[r, erste + c]) continue;
                            if (posten(c))
                            {
                                if (c - feldA[c] < feldE[c] - c) essenLinks.Add(feldVon(c));
                                else essenRechts.Add(feldVon(c));
                            }
                            else if (posten(c - 1)) essenRechts.Add(feldVon(c - 1));
                            else if (posten(c + 1)) essenLinks.Add(feldVon(c + 1));
                            else essenMitte[c] = essenRuheDa = true;
                        }

                        float es = Math.Min(spalteW, zeileH) * 0.5f;
                        for (int c0 = 0; c0 < stunden; c0 = feldE[c0] + 1)
                        {
                            int k = plan.Z[r, erste + c0];
                            var feld = feldVon(c0);
                            using (var f = new SolidBrush(k >= 0 ? Pastell(cfg.Posten[k].Farbe) : k == Plan.RUHE ? Leer : Color.White))
                                g.FillRectangle(f, feld);
                            g.DrawRectangle(gitter, feld.X, feld.Y, feld.Width, feld.Height);
                            if (k >= 0)
                            {
                                // in schmalen Feldern weicht der Buchstabe dem Essens-Zeichen aus
                                var platz = feld;
                                if (feld.Width < spalteW * 1.5f)
                                {
                                    if (essenLinks.Contains(feld)) platz = RectangleF.FromLTRB(platz.Left + es * 0.55f, platz.Top, platz.Right, platz.Bottom);
                                    if (essenRechts.Contains(feld)) platz = RectangleF.FromLTRB(platz.Left, platz.Top, platz.Right - es * 0.55f, platz.Bottom);
                                }
                                g.DrawString(Kuerzel(k), fZeichen, tText, platz, mitte);
                            }
                            else if (essenMitte[c0])
                                EssenSymbol(g, RectangleF.Inflate(feld, -zeileH * 0.06f, -zeileH * 0.06f));
                            else if (k == Plan.RUHE)
                                g.DrawString("-", fZeichen, tText, feld, mitte);
                        }

                        essenVorDa |= essenLinks.Count > 0;
                        essenNachDa |= essenRechts.Count > 0;
                        foreach (var feld in essenLinks)
                            EssenSymbol(g, new RectangleF(feld.Left + 1 * u, feld.Top + 1 * u, es, es));
                        foreach (var feld in essenRechts)
                            EssenSymbol(g, new RectangleF(feld.Right - es - 1 * u, feld.Top + 1 * u, es, es));
                    }
                }

                // Linien: dick zwischen Blöcken und Zweierteams, sonst dünn
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
                    float y = zeileY(r + 1);
                    g.DrawLine(trenn ? dick : gitter, b.Left, y, trenn ? b.Right : xs, y);
                }
                g.DrawLine(duenn, b.Left + gradW, kopfY, b.Left + gradW, tabUnten);
                g.DrawLine(duenn, xs, tabOben, xs, tabUnten);
                for (int c = 1; c < stunden; c++)
                    if (von.AddHours(c).Hour == 0)
                        g.DrawLine(dick, spalteX(c), tabOben, spalteX(c), tabUnten);
                g.DrawLine(dick, b.Left, kopfY, b.Right, kopfY);
                g.DrawRectangle(dick, b.Left, tabOben, b.Width, tabUnten - tabOben);

                // Stunden erst jetzt, mit einer Lücke in den Linien dahinter (Anfang, Mitternacht, Ende)
                var stundeTh = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
                for (int c = 0; c <= stunden; c++)
                {
                    DateTime z = von.AddHours(c);
                    string text = c == stunden && c > 0 && z.Hour == 0 ? "2400" : z.ToString("HHmm");
                    var gross = g.MeasureString(text, fStunde);
                    float th = fStunde.GetHeight(g) * 0.8f;
                    var alt = g.Transform;
                    g.TranslateTransform(spalteX(c), kopfY - 3 * u);
                    g.RotateTransform(-90);
                    g.FillRectangle(Brushes.White, -1.5f * u, -th / 2, gross.Width + 1.5f * u, th);
                    g.DrawString(text, fStunde, tText, new RectangleF(0, -6 * u, stundeH, 12 * u), stundeTh);
                    g.Transform = alt;
                }

                // Zeichen unter der Tabelle: Beschriftung in der Namensspalte, Muster ab dem Raster wie im Plan
                float zy0 = tabUnten + 5 * u;
                float mw = 22 * u, mh = 13 * u, ms = mh * 0.7f;
                var musterFarbe = cfg.Posten.Count > 0 ? Pastell(cfg.Posten[0].Farbe) : Leer;
                string musterZeichen = cfg.Posten.Count > 0 ? Kuerzel(0) : "";
                Action<RectangleF, Color, string> muster = (feld, farbe, text) =>
                {
                    using (var f = new SolidBrush(farbe)) g.FillRectangle(f, feld);
                    g.DrawRectangle(gitter, feld.X, feld.Y, feld.Width, feld.Height);
                    if (text != "") g.DrawString(text, fKleinFett, tText, feld, mitte);
                };
                var zeichen = new List<KeyValuePair<string, Action<RectangleF>>>();
                zeichen.Add(new KeyValuePair<string, Action<RectangleF>>(t.Ruhezeit, feld => muster(feld, Leer, "-")));
                if (frei)
                    zeichen.Add(new KeyValuePair<string, Action<RectangleF>>(t.OhnePosten, feld => muster(feld, Color.White, "")));
                if (essenVorDa)
                    zeichen.Add(new KeyValuePair<string, Action<RectangleF>>(t.EssenVor, feld =>
                    {
                        muster(feld, musterFarbe, "");
                        g.DrawString(musterZeichen, fKleinFett, tText, RectangleF.FromLTRB(feld.Left + ms * 0.6f, feld.Top, feld.Right, feld.Bottom), mitte);
                        EssenSymbol(g, new RectangleF(feld.Left + 0.6f * u, feld.Top + 0.6f * u, ms, ms));
                    }));
                if (essenNachDa)
                    zeichen.Add(new KeyValuePair<string, Action<RectangleF>>(t.EssenNach, feld =>
                    {
                        muster(feld, musterFarbe, "");
                        g.DrawString(musterZeichen, fKleinFett, tText, RectangleF.FromLTRB(feld.Left, feld.Top, feld.Right - ms * 0.6f, feld.Bottom), mitte);
                        EssenSymbol(g, new RectangleF(feld.Right - ms - 0.6f * u, feld.Top + 0.6f * u, ms, ms));
                    }));
                if (essenRuheDa)
                    zeichen.Add(new KeyValuePair<string, Action<RectangleF>>(t.EssenRuhe, feld =>
                    {
                        muster(feld, Leer, "");
                        EssenSymbol(g, RectangleF.Inflate(feld, -0.5f * u, -0.5f * u));
                    }));
                if (cfg.Posten.Any(p => p.Draussen))
                    zeichen.Add(new KeyValuePair<string, Action<RectangleF>>(t.Draussen, feld =>
                        SchneeSymbol(g, new RectangleF(feld.Left + (feld.Width - 10 * u) / 2, feld.Top + (feld.Height - 10 * u) / 2, 10 * u, 10 * u))));
                if (plan.Zeilen.Any(p => p.Fahrer))
                    zeichen.Add(new KeyValuePair<string, Action<RectangleF>>(t.Fahrer, feld =>
                        AutoSymbol(g, new RectangleF(feld.Left + (feld.Width - 12 * u) / 2, feld.Top + (feld.Height - 12 * u) / 2, 12 * u, 12 * u))));

                var beschriftung = new RectangleF(b.Left + 4 * u, zy0, xs - b.Left - 8 * u, zeichenH);
                g.DrawString(t.Zeichen, fKleinFett, tText, beschriftung, links);
                float zx = xs;
                foreach (var z in zeichen)
                {
                    z.Value(new RectangleF(zx, zy0 + (zeichenH - mh) / 2, mw, mh));
                    float w = g.MeasureString(z.Key, fKlein).Width;
                    g.DrawString(z.Key, fKlein, tText, new RectangleF(zx + mw + 4 * u, zy0, w + 2 * u, zeichenH), links);
                    zx += mw + 4 * u + w + 16 * u;
                }

                // Nicht eingeteilte Personen (z. B. Reserve), nach Funktion
                if (plan.NichtEingeteilt.Count > 0)
                {
                    float ry = zy0 + zeichenH;
                    beschriftung.Y = ry;
                    g.DrawString(t.NichtEingeteilt, fKleinFett, tText, beschriftung, links);
                    float nx = xs;
                    foreach (var gr in plan.NichtEingeteilt.GroupBy(p => p.Funktion))
                    {
                        string titel = gr.Key + ":";
                        string namen = string.Join(", ", gr.Select(p => (p.Grad + " " + p.Name).Trim()));
                        float tw = g.MeasureString(titel, fKleinFett).Width;
                        g.DrawString(titel, fKleinFett, tGrau, new RectangleF(nx, ry, tw + 2 * u, zeichenH), links);
                        float nw = Math.Min(b.Right - nx - tw - 2 * u, g.MeasureString(namen, fKlein).Width);
                        if (nw <= 0) break;
                        g.DrawString(namen, fKlein, tText, new RectangleF(nx + tw + 2 * u, ry, nw + 2 * u, zeichenH), links);
                        nx += tw + 2 * u + nw + 16 * u;
                    }
                }

                float fy = b.Bottom - fussH;
                g.DrawString(string.Format(t.Erstellt, DateTime.Now.ToString("dd.MM.yyyy HH:mm")), fKlein, tGrau, new RectangleF(b.Left, fy, b.Width / 2, fussH), links);
                if (plan.Seiten > 1)
                    g.DrawString(string.Format(t.Seite, seite + 1, plan.Seiten), fKlein, tGrau, new RectangleF(b.Left, fy, b.Width, fussH), mitte);
            }
        }

        static Image armeeLogo;

        static Image ArmeeLogo
        {
            get
            {
                if (armeeLogo == null)
                {
                    var s = typeof(Zeichner).Assembly.GetManifestResourceStream("ShiftDesk.armee-logo.png");
                    if (s != null) armeeLogo = Image.FromStream(s);
                }
                return armeeLogo;
            }
        }

        // Beginnt um diese Stunde eine neue Ablösung? Sonst bleibt der Posten im selben Feld.
        static bool NeueAbloesung(Konfiguration cfg, int stunde)
        {
            int takt = cfg.TaktUm(stunde);
            return takt <= 1 || stunde % takt == 0;
        }

        // A, B, C … Z, dann AA, AB …
        static string Kuerzel(int k)
        {
            return k < 26 ? ((char)('A' + k)).ToString() : Kuerzel(k / 26 - 1) + (char)('A' + k % 26);
        }

        // Postenfarbe stark aufgehellt, damit die Buchstaben gut lesbar sind
        static Color Pastell(int farbe)
        {
            var c = Color.FromArgb(farbe);
            const float a = 0.42f;
            return Color.FromArgb((int)(c.R * a + 255 * (1 - a)), (int)(c.G * a + 255 * (1 - a)), (int)(c.B * a + 255 * (1 - a)));
        }

        // Auto: Fahrer
        static void AutoSymbol(Graphics g, RectangleF feld)
        {
            Lucide.Zeichne(g, "car", feld, Grau, 2.2f);
        }

        // Schneeflocke: Posten im Freien
        static void SchneeSymbol(Graphics g, RectangleF feld)
        {
            Lucide.Zeichne(g, "snowflake", feld, Schnee, 2.4f);
        }

        // Lucide-Besteck auf weissem Kreis
        static void EssenSymbol(Graphics g, RectangleF zelle)
        {
            float s = Math.Min(zelle.Width, zelle.Height) * 0.78f;
            var kreis = new RectangleF(zelle.X + (zelle.Width - s) / 2, zelle.Y + (zelle.Height - s) / 2, s, s);
            g.FillEllipse(Brushes.White, kreis);
            Lucide.Zeichne(g, "utensils", RectangleF.Inflate(kreis, -s * 0.21f, -s * 0.21f), TextFarbe, 2.4f);
        }
    }
}
