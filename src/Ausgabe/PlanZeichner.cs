using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using ShiftDesk.Modell;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Planung;

namespace ShiftDesk.Ausgabe
{
    /// <summary>
    /// Zeichnet eine Seite (24 Stunden) des Plans, für Vorschau und Drucker gleich. Immer hell (Papier).
    /// </summary>
    /// <remarks>
    /// <para>Aufbau nach dem Formular «Ablösungsliste»: Kopf mit Feldern und Legende (diese Datei), Tabelle mit
    /// Rahmen, Stunden auf den Linien, Posten als Buchstaben auf hellen Farben und «-» für Ruhe
    /// (<c>PlanZeichner.Tabelle.cs</c>), darunter die Erklärung der Zeichen (<c>PlanZeichner.Zeichen.cs</c>).</para>
    /// <para>Alle Masse sind Tausendstel der Seitenbreite (<see cref="u"/>), so sieht die Seite in jeder
    /// Grösse gleich aus.</para>
    /// </remarks>
    public sealed partial class PlanZeichner : IDisposable
    {
        const string Schrift = "Arial";

        // Textausrichtungen, einmal erzeugt und für jede Seite wiederverwendet
        static readonly StringFormat links = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        static readonly StringFormat rechtsMitte = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        static readonly StringFormat mitte = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        static readonly StringFormat stundeTh = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

        readonly Graphics g;
        readonly RectangleF b;   // bedruckbarer Bereich
        readonly Plan plan;
        readonly Konfiguration cfg;
        readonly DruckSprache t;
        readonly int seite;

        /// <summary>Masseinheit: ein Tausendstel der Seitenbreite.</summary>
        readonly float u;

        /// <summary>Erste Stunde dieser Seite im Plan und Anzahl Stunden auf der Seite (höchstens 24).</summary>
        readonly int erste, stunden;
        readonly DateTime von, bis;

        readonly SolidBrush tText, tGrau;
        readonly Pen dick, duenn, gitter;
        readonly Font fTitel, fFeld, fWert, fKopf, fStunde, fKlein, fKleinFett;

        // Spalten: Grad, Name, dann das Raster mit einer Spalte pro Stunde ab xs
        readonly float gradW, nameW, xs, spalteW;

        // vom Kopf berechnet: Oberkante und Höhe der Felder, linker Rand von Feldern und Legende, Breite der Feldnamen, Unterkante des Kopfs
        float oben, feldH, fx0, labelW, kopfUnten;

        /// <summary>Zeichnet eine Seite des Plans in den Bereich <paramref name="b"/>.</summary>
        /// <param name="g">Zeichenfläche (Bildschirm, Bitmap oder Drucker).</param>
        /// <param name="b">Bedruckbarer Bereich; die Seite ist für das Seitenverhältnis A4 quer gemacht.</param>
        /// <param name="plan">Der Plan.</param>
        /// <param name="seite">Seite ab 0 (je 24 Stunden).</param>
        public static void Seite(Graphics g, RectangleF b, Plan plan, int seite)
        {
            using (var z = new PlanZeichner(g, b, plan, seite))
                z.Zeichnen();
        }

        PlanZeichner(Graphics g, RectangleF b, Plan plan, int seite)
        {
            this.g = g;
            this.b = b;
            this.plan = plan;
            this.seite = seite;
            cfg = plan.Cfg;
            t = DruckSprache.In(cfg.Sprache);
            u = b.Width / 1000f;

            erste = seite * 24;
            stunden = Math.Min(24, plan.Stunden - erste);
            von = plan.Start.AddHours(erste);
            bis = von.AddHours(stunden);

            tText = new SolidBrush(Druckzeichen.TextFarbe);
            tGrau = new SolidBrush(Druckzeichen.Grau);
            dick = new Pen(Druckzeichen.Rahmen, u * 1.3f);
            duenn = new Pen(Druckzeichen.Rahmen, u * 0.6f);
            gitter = new Pen(Druckzeichen.Gitter, u * 0.5f);
            fTitel = new Font(Schrift, 19 * u, FontStyle.Bold, GraphicsUnit.World);
            fFeld = new Font(Schrift, 9.5f * u, GraphicsUnit.World);
            fWert = new Font(Schrift, 10.5f * u, GraphicsUnit.World);
            fKopf = new Font(Schrift, 9.5f * u, FontStyle.Bold, GraphicsUnit.World);
            fStunde = new Font(Schrift, 8.5f * u, FontStyle.Bold, GraphicsUnit.World);
            fKlein = new Font(Schrift, 8 * u, GraphicsUnit.World);
            fKleinFett = new Font(Schrift, 8 * u, FontStyle.Bold, GraphicsUnit.World);

            gradW = 40 * u;
            nameW = 140 * u;
            xs = b.Left + gradW + nameW;
            spalteW = (b.Right - xs) / 24f;
        }

        void Zeichnen()
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            Kopf();
            Legende();
            TabelleAnordnen();
            Tabellenkopf();
            Zeilen();
            Linien();
            Stundenzahlen();
            ZeichenErklaeren();
            NichtEingeteilte();
            Fusszeile();
        }

        /// <summary>Logo oben links, darunter der Titel; rechts davon die Felder Einheit, Für und Datum.</summary>
        void Kopf()
        {
            oben = b.Top;
            var logo = Bilder.Laden("armee-logo.png");
            if (logo != null)
            {
                float lh = 30 * u;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(logo, new RectangleF(b.Left, b.Top, lh * logo.Width / logo.Height, lh));
                oben += lh + 12 * u;
            }
            g.DrawString(t.Titel, fTitel, tText, b.Left - 1.5f * u, oben - 2 * u);
            fx0 = Math.Max(xs, b.Left + g.MeasureString(t.Titel, fTitel).Width + 10 * u);
            float feldW = b.Right - fx0;

            feldH = 22 * u;
            string datum = cfg.Tagesplan ? t.JedenTag
                : von.Hour == 0 && stunden == 24 ? von.ToString(t.DatumLang, t.Kultur)
                : von.ToString("ddd dd.MM.yyyy HH:00", t.Kultur) + " – " + bis.ToString("ddd dd.MM.yyyy HH:00", t.Kultur);
            var felder = new[]
            {
                new { Name = t.Einheit, Wert = cfg.Einheit ?? "", X = 0f, W = 0.24f },
                new { Name = t.Fuer, Wert = cfg.Name ?? "", X = 0.24f, W = 0.41f },
                new { Name = t.Datum, Wert = datum, X = 0.65f, W = 0.35f },
            };
            labelW = felder.Max(f => g.MeasureString(f.Name, fFeld).Width) + 8 * u;
            foreach (var f in felder)
            {
                float fx = fx0 + f.X * feldW;
                g.DrawString(f.Name, fFeld, tText, new RectangleF(fx, oben, labelW, feldH), links);
                // lange Werte (z. B. der Name des Plans) werden kleiner geschrieben statt abgeschnitten
                var wertFeld = new RectangleF(fx + labelW, oben, f.W * feldW - labelW - 4 * u, feldH);
                float breite = g.MeasureString(f.Wert, fWert).Width;
                if (breite <= wertFeld.Width || f.Wert == "")
                    g.DrawString(f.Wert, fWert, tText, wertFeld, links);
                else
                    using (var klein = new Font(Schrift, Math.Max(6.5f * u, fWert.Size * wertFeld.Width / breite), GraphicsUnit.World))
                        g.DrawString(f.Wert, klein, tText, wertFeld, links);
            }
            g.DrawLine(duenn, fx0, oben + feldH, b.Right, oben + feldH);
        }

        /// <summary>Legende: Posten mit Buchstaben, spaltenweise wie im Formular, mit Schneeflocke und Öffnungszeit.</summary>
        void Legende()
        {
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
                using (var f = new SolidBrush(Druckzeichen.Pastell(posten.Farbe))) g.FillRectangle(f, feld);
                Druckzeichen.Mittig(g, Druckzeichen.Kuerzel(k), fKleinFett, tText, feld);
                float tx = x + q + 4 * u, rest = lSpalteW - q - 8 * u;
                float w = Math.Min(rest, g.MeasureString(posten.Name, fFeld).Width);
                g.DrawString(posten.Name, fFeld, tText, new RectangleF(tx, y, w + 2 * u, lZeileH), links);
                tx += w;
                if (posten.Draussen && rest - w > q)
                {
                    Druckzeichen.SchneeSymbol(g, new RectangleF(tx + 2 * u, y + (lZeileH - 9 * u) / 2, 9 * u, 9 * u));
                    tx += 11 * u;
                }
                if (!posten.Durchgehend)
                    g.DrawString(Zeit.Text(posten.OffenVon) + "–" + Zeit.Text(posten.OffenBis), fKlein, tGrau,
                        new RectangleF(tx + 3 * u, y, Math.Max(0, x + lSpalteW - tx - 7 * u), lZeileH), links);
            }
            kopfUnten = ly + lZeilen * lZeileH + 4 * u;
            g.DrawLine(duenn, fx0, kopfUnten, b.Right, kopfUnten);
        }

        /// <summary>Erstellungsdatum unten links, bei mehreren Seiten die Seitenzahl in der Mitte.</summary>
        void Fusszeile()
        {
            float fy = b.Bottom - FussH;
            g.DrawString(string.Format(t.Erstellt, DateTime.Now.ToString("dd.MM.yyyy HH:mm")), fKlein, tGrau, new RectangleF(b.Left, fy, b.Width / 2, FussH), links);
            if (plan.Seiten > 1)
                g.DrawString(string.Format(t.Seite, seite + 1, plan.Seiten), fKlein, tGrau, new RectangleF(b.Left, fy, b.Width, FussH), mitte);
        }

        /// <summary>Gibt Pinsel, Stifte und Schriften frei.</summary>
        public void Dispose()
        {
            foreach (IDisposable d in new IDisposable[] { tText, tGrau, dick, duenn, gitter, fTitel, fFeld, fWert, fKopf, fStunde, fKlein, fKleinFett })
                d.Dispose();
        }
    }
}
