using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;

namespace ShiftDesk
{
    // Zeichnet eine Seite (24 Stunden) des Plans, für Vorschau und Drucker gleich. Immer hell (Papier).
    public static class Zeichner
    {
        static readonly CultureInfo De = new CultureInfo("de-CH");
        const string Schrift = "Segoe UI";
        const string SchriftFett = "Segoe UI Semibold";

        static readonly Color TextFarbe = Color.FromArgb(17, 24, 39);
        static readonly Color Grau = Color.FromArgb(107, 114, 128);
        static readonly Color HellGrau = Color.FromArgb(156, 163, 175);
        static readonly Color Linie = Color.FromArgb(229, 231, 235);
        static readonly Color Leer = Color.FromArgb(243, 244, 246);

        public static void Seite(Graphics g, RectangleF b, Plan plan, int seite)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            var cfg = plan.Cfg;
            float u = b.Width / 1000f;

            int erste = seite * 24;
            int stunden = Math.Min(24, plan.Stunden - erste);
            DateTime von = plan.Start.AddHours(erste);
            DateTime bis = von.AddHours(stunden);

            var links = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            var mitte = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            var rechts = new StringFormat { Alignment = StringAlignment.Far };

            using (var tText = new SolidBrush(TextFarbe))
            using (var tGrau = new SolidBrush(Grau))
            using (var tHell = new SolidBrush(HellGrau))
            using (var linie = new Pen(Linie, u * 0.8f))
            using (var fTitel = new Font(SchriftFett, 20 * u, GraphicsUnit.World))
            using (var fUnter = new Font(Schrift, 9.5f * u, GraphicsUnit.World))
            using (var fFett = new Font(SchriftFett, 9.5f * u, GraphicsUnit.World))
            using (var fKlein = new Font(Schrift, 8 * u, GraphicsUnit.World))
            using (var fKleinFett = new Font(SchriftFett, 8 * u, GraphicsUnit.World))
            {
                // Kopf
                g.DrawString("Wachtablösungsliste", fTitel, tText, b.Left - 1.5f * u, b.Top);
                string unter = cfg.Name ?? "";
                if (unter != "") g.DrawString(unter, fUnter, tGrau, b.Left, b.Top + 29 * u);
                g.DrawString(von.ToString("ddd dd.MM.yyyy HH:00", De) + "  –  " + bis.ToString("ddd dd.MM.yyyy HH:00", De),
                    fFett, tText, b.Right, b.Top + 6 * u, rechts);
                if (plan.Seiten > 1)
                    g.DrawString("Seite " + (seite + 1) + " von " + plan.Seiten, fKlein, tGrau, b.Right, b.Top + 22 * u, rechts);

                // Masse
                var balken = Enumerable.Range(0, cfg.Posten.Count)
                    .Where(k => !cfg.Posten[k].Durchgehend && cfg.Posten[k].Typ != PostenTyp.Auffang).ToList();
                float balkenH = 14 * u, legendeH = 16 * u, fussH = 12 * u;
                float reserveH = plan.NichtEingeteilt.Count > 0 ? 18 * u : 0;
                float fuss = (balken.Count > 0 ? balken.Count * balkenH + 12 * u : 0) + 14 * u + legendeH + reserveH + fussH;
                float oben = b.Top + 60 * u;
                float gradW = 40 * u, nameW = 126 * u;
                float xs = b.Left + gradW + nameW;
                float spalteW = (b.Right - xs) / 24f;
                float datumH = 15 * u, stundeH = 18 * u, lueckeH = 10 * u;
                int n = plan.Zeilen.Count;
                int bloecke = n == 0 ? 0 : plan.Block[n - 1];
                float platz = b.Bottom - fuss - oben - datumH - stundeH - bloecke * lueckeH;
                float zeileH = Math.Min(25 * u, platz / Math.Max(1, n));
                float kopfY = oben + datumH;
                Func<int, float> zeileY = r => kopfY + stundeH + r * zeileH + plan.Block[r] * lueckeH;
                Func<int, float> spalteX = c => xs + c * spalteW;
                float tabUnten = zeileY(n - 1) + zeileH;
                float spalt = Math.Max(0.8f * u, Math.Min(spalteW, zeileH) * 0.07f);
                float radius = 2.2f * u;

                // Spaltenköpfe
                g.DrawString("Grad", fKleinFett, tGrau, new RectangleF(b.Left, kopfY, gradW, stundeH), links);
                g.DrawString("Name", fKleinFett, tGrau, new RectangleF(b.Left + gradW, kopfY, nameW, stundeH), links);
                for (int c = 0; c < stunden; c++)
                {
                    DateTime z = von.AddHours(c);
                    g.DrawString(z.ToString("HH:00"), fKlein, tGrau, new RectangleF(spalteX(c), kopfY, spalteW, stundeH), mitte);
                    if (c == 0 || z.Hour == 0)
                        g.DrawString(z.ToString("dddd, dd.MM.yyyy", De), fKleinFett, tText, spalteX(c) + spalt, oben);
                }
                g.DrawLine(linie, b.Left, kopfY + stundeH, b.Right, kopfY + stundeH);

                using (var fName = new Font(Schrift, Math.Min(10 * u, zeileH * 0.46f), GraphicsUnit.World))
                {
                    // Namen und Posten-Kacheln
                    for (int r = 0; r < n; r++)
                    {
                        float zy = zeileY(r);
                        var p = plan.Zeilen[r];
                        g.DrawString(p.Grad, fName, tGrau, new RectangleF(b.Left, zy, gradW, zeileH), links);
                        g.DrawString(p.Name, fName, tText, new RectangleF(b.Left + gradW, zy, nameW - 4 * u, zeileH), links);
                        if (r < n - 1 && plan.Block[r + 1] == plan.Block[r])
                            g.DrawLine(linie, b.Left, zy + zeileH, xs - spalt, zy + zeileH);
                        for (int c = 0; c < stunden; c++)
                        {
                            var kachel = new RectangleF(spalteX(c) + spalt / 2, zy + spalt / 2, spalteW - spalt, zeileH - spalt);
                            int k = plan.Z[r, erste + c];
                            if (k == Plan.RUHE) continue;
                            Formen.Fuellen(g, kachel, radius, k >= 0 ? Color.FromArgb(cfg.Posten[k].Farbe) : Leer);
                            if (plan.Essen[r, erste + c])
                                EssenSymbol(g, kachel);
                        }
                    }

                    // Ruhezeiten als zusammenhängende Blöcke pro Gruppe
                    int r0 = 0;
                    while (r0 < n)
                    {
                        int r1 = r0;
                        while (r1 + 1 < n && plan.Block[r1 + 1] == plan.Block[r0] && plan.Zeilen[r1 + 1].Gruppe == plan.Zeilen[r0].Gruppe)
                            r1++;
                        int c0 = 0;
                        while (c0 < stunden)
                        {
                            if (!AlleInRuhe(plan, r0, r1, erste + c0)) { c0++; continue; }
                            int c1 = c0;
                            while (c1 + 1 < stunden && AlleInRuhe(plan, r0, r1, erste + c1 + 1)) c1++;
                            var block = RectangleF.FromLTRB(spalteX(c0) + spalt / 2, zeileY(r0) + spalt / 2,
                                spalteX(c1 + 1) - spalt / 2, zeileY(r1) + zeileH - spalt / 2);
                            Formen.Fuellen(g, block, radius * 1.6f, Leer);
                            TextEinpassen(g, "Ruhezeit", block, tHell, u);
                            c0 = c1 + 1;
                        }
                        r0 = r1 + 1;
                    }

                    // Tageswechsel
                    using (var tag = new Pen(HellGrau, u * 1.2f))
                        for (int c = 1; c < stunden; c++)
                            if (von.AddHours(c).Hour == 0)
                                g.DrawLine(tag, spalteX(c), kopfY, spalteX(c), tabUnten);
                }

                // Öffnungszeiten der Posten
                float by = tabUnten + 12 * u;
                foreach (int k in balken)
                {
                    var posten = cfg.Posten[k];
                    var farbe = Color.FromArgb(posten.Farbe);
                    g.DrawString(posten.Name, fKlein, tGrau, new RectangleF(b.Left + gradW, by, nameW, balkenH), links);
                    int c0 = 0;
                    while (c0 < stunden)
                    {
                        if (!posten.OffenUm(von.AddHours(c0).Hour)) { c0++; continue; }
                        int c1 = c0;
                        while (c1 + 1 < stunden && posten.OffenUm(von.AddHours(c1 + 1).Hour)) c1++;
                        var feld = RectangleF.FromLTRB(spalteX(c0) + spalt / 2, by + 2 * u, spalteX(c1 + 1) - spalt / 2, by + balkenH - 2 * u);
                        Formen.Fuellen(g, feld, feld.Height / 2, farbe);
                        using (var t = new SolidBrush(TextFarbeAuf(farbe)))
                            g.DrawString("offen " + Zeit.Text(posten.OffenVon) + " – " + Zeit.Text(posten.OffenBis),
                                fKlein, t, feld, mitte);
                        c0 = c1 + 1;
                    }
                    by += balkenH;
                }

                // Legende
                float ly = by + 14 * u;
                g.DrawString("Legende", fKleinFett, tGrau, new RectangleF(b.Left + gradW, ly, nameW, legendeH), links);
                float lx = xs;
                float q = 10 * u;
                for (int k = 0; k <= cfg.Posten.Count; k++)
                {
                    bool essen = k == cfg.Posten.Count;
                    if (essen && cfg.Essenszeiten.Count == 0) break;
                    string name = essen ? "Essen" : cfg.Posten[k].Name;
                    var feld = new RectangleF(lx, ly + (legendeH - q) / 2, q, q);
                    if (essen)
                    {
                        var gross = RectangleF.Inflate(feld, 2.5f * u, 2.5f * u);
                        g.FillEllipse(new SolidBrush(Leer), gross);
                        EssenSymbol(g, gross);
                    }
                    else Formen.Fuellen(g, feld, 2 * u, Color.FromArgb(cfg.Posten[k].Farbe));
                    float w = g.MeasureString(name, fKlein).Width;
                    g.DrawString(name, fKlein, tText, new RectangleF(lx + q + 4 * u, ly, w + 2 * u, legendeH), links);
                    lx += q + 4 * u + w + 16 * u;
                }

                // Nicht eingeteilte Personen (z. B. Reserve), nach Funktion
                if (plan.NichtEingeteilt.Count > 0)
                {
                    float ry = ly + legendeH + 4 * u;
                    string text = string.Join("     ", plan.NichtEingeteilt.GroupBy(p => p.Funktion)
                        .Select(gr => gr.Key + ":  " + string.Join(", ", gr.Select(p => (p.Grad + " " + p.Name).Trim()))));
                    g.DrawString("Nicht eingeteilt", fKleinFett, tGrau, new RectangleF(b.Left + gradW, ry, nameW, 14 * u), links);
                    g.DrawString(text, fKlein, tText, new RectangleF(xs, ry, b.Right - xs, 14 * u), links);
                }

                g.DrawString("Erstellt mit ShiftDesk am " + DateTime.Now.ToString("dd.MM.yyyy HH:mm"), fKlein, tHell, b.Right, b.Bottom - fussH + 2 * u, rechts);
            }
        }

        static bool AlleInRuhe(Plan plan, int r0, int r1, int t)
        {
            for (int r = r0; r <= r1; r++)
                if (plan.Z[r, t] != Plan.RUHE) return false;
            return true;
        }

        static void TextEinpassen(Graphics g, string text, RectangleF feld, Brush pinsel, float u)
        {
            float groesse = Math.Min(Math.Min(feld.Height * 0.42f, 26 * u), feld.Width / (text.Length * 0.62f));
            if (groesse < 7 * u) return;
            using (var f = new Font(SchriftFett, groesse, GraphicsUnit.World))
                g.DrawString(text, f, pinsel, feld, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap });
        }

        static Color TextFarbeAuf(Color hintergrund)
        {
            int hell = (hintergrund.R * 299 + hintergrund.G * 587 + hintergrund.B * 114) / 1000;
            return hell < 150 ? Color.White : TextFarbe;
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
