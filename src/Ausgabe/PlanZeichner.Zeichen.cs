using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ShiftDesk.Planung;

namespace ShiftDesk.Ausgabe
{
    // Unter der Tabelle: Erklärung der Zeichen und die nicht eingeteilten Personen.
    public sealed partial class PlanZeichner
    {
        /// <summary>
        /// Zeichen unter der Tabelle: Beschriftung in der Namensspalte, Muster ab dem Raster wie im Plan.
        /// Erklärt wird nur, was auf dieser Seite vorkommt.
        /// </summary>
        void ZeichenErklaeren()
        {
            float zy0 = tabUnten + 5 * u;
            float mw = 22 * u, mh = 13 * u, ms = mh * 0.7f;
            var musterFarbe = cfg.Posten.Count > 0 ? Druckzeichen.Pastell(cfg.Posten[0].Farbe) : Druckzeichen.Leer;
            string musterZeichen = cfg.Posten.Count > 0 ? Druckzeichen.Kuerzel(0) : "";
            Action<RectangleF, Color, string> muster = (feld, farbe, text) =>
            {
                using (var f = new SolidBrush(farbe)) g.FillRectangle(f, feld);
                g.DrawRectangle(gitter, feld.X, feld.Y, feld.Width, feld.Height);
                if (text != "") g.DrawString(text, fKleinFett, tText, feld, mitte);
            };
            bool frei = Enumerable.Range(0, plan.Zeilen.Count).Any(r => Enumerable.Range(0, stunden).Any(c => plan.Z[r, erste + c] == Plan.FREI));

            var zeichen = new List<KeyValuePair<string, Action<RectangleF>>>();
            Action<string, Action<RectangleF>> dazu = (text, zeichnen) => zeichen.Add(new KeyValuePair<string, Action<RectangleF>>(text, zeichnen));
            dazu(t.Ruhezeit, feld => muster(feld, Druckzeichen.Leer, "-"));
            if (frei)
                dazu(t.OhnePosten, feld => muster(feld, Color.White, ""));
            if (essenVorDa)
                dazu(t.EssenVor, feld =>
                {
                    muster(feld, musterFarbe, "");
                    Druckzeichen.Mittig(g, musterZeichen, fKleinFett, tText, RectangleF.FromLTRB(feld.Left + ms * 0.6f, feld.Top, feld.Right, feld.Bottom));
                    Druckzeichen.EssenSymbol(g, new RectangleF(feld.Left + 0.6f * u, feld.Top + 0.6f * u, ms, ms));
                });
            if (essenNachDa)
                dazu(t.EssenNach, feld =>
                {
                    muster(feld, musterFarbe, "");
                    Druckzeichen.Mittig(g, musterZeichen, fKleinFett, tText, RectangleF.FromLTRB(feld.Left, feld.Top, feld.Right - ms * 0.6f, feld.Bottom));
                    Druckzeichen.EssenSymbol(g, new RectangleF(feld.Right - ms - 0.6f * u, feld.Top + 0.6f * u, ms, ms));
                });
            if (essenRuheDa)
                dazu(t.EssenRuhe, feld =>
                {
                    muster(feld, Druckzeichen.Leer, "");
                    Druckzeichen.EssenSymbol(g, RectangleF.Inflate(feld, -0.5f * u, -0.5f * u));
                });
            if (essenReserveDa)
                dazu(t.EssenReserve, feld =>
                {
                    muster(feld, musterFarbe, "");
                    Druckzeichen.Mittig(g, musterZeichen, fKleinFett, tText, RectangleF.FromLTRB(feld.Left, feld.Top, feld.Right - ms * 0.6f, feld.Bottom));
                    Druckzeichen.EssenSymbol(g, new RectangleF(feld.Right - ms - 0.6f * u, feld.Top + 0.6f * u, ms, ms), true);
                });
            if (cfg.Posten.Any(p => p.Draussen))
                dazu(t.Draussen, feld =>
                    Druckzeichen.SchneeSymbol(g, new RectangleF(feld.Left + (feld.Width - 10 * u) / 2, feld.Top + (feld.Height - 10 * u) / 2, 10 * u, 10 * u)));
            if (plan.Zeilen.Any(p => p.Fahrer))
                dazu(t.Fahrer, feld =>
                    Druckzeichen.AutoSymbol(g, new RectangleF(feld.Left + (feld.Width - 12 * u) / 2, feld.Top + (feld.Height - 12 * u) / 2, 12 * u, 12 * u)));

            g.DrawString(t.Zeichen, fKleinFett, tText, Beschriftung(zy0), links);
            float zx = xs;
            foreach (var z in zeichen)
            {
                z.Value(new RectangleF(zx, zy0 + (zeichenH - mh) / 2, mw, mh));
                float w = g.MeasureString(z.Key, fKlein).Width;
                g.DrawString(z.Key, fKlein, tText, new RectangleF(zx + mw + 4 * u, zy0, w + 2 * u, zeichenH), links);
                zx += mw + 4 * u + w + 16 * u;
            }
        }

        /// <summary>Nicht eingeteilte Personen (z. B. Reserve), nach Funktion, in der Zeile unter den Zeichen.</summary>
        void NichtEingeteilte()
        {
            if (plan.NichtEingeteilt.Count == 0) return;
            float ry = tabUnten + 5 * u + zeichenH;
            g.DrawString(t.NichtEingeteilt, fKleinFett, tText, Beschriftung(ry), links);
            float nx = xs;
            foreach (var gr in plan.NichtEingeteilt.GroupBy(p => p.Funktion))
            {
                string titel = gr.Key + ":";
                string namen = string.Join(", ", gr.Select(p => p.Anzeigename));
                float tw = g.MeasureString(titel, fKleinFett).Width;
                g.DrawString(titel, fKleinFett, tGrau, new RectangleF(nx, ry, tw + 2 * u, zeichenH), links);
                float nw = Math.Min(b.Right - nx - tw - 2 * u, g.MeasureString(namen, fKlein).Width);
                if (nw <= 0) break;
                g.DrawString(namen, fKlein, tText, new RectangleF(nx + tw + 2 * u, ry, nw + 2 * u, zeichenH), links);
                nx += tw + 2 * u + nw + 16 * u;
            }
        }

        /// <summary>Feld für eine Beschriftung in der Namensspalte unter der Tabelle.</summary>
        RectangleF Beschriftung(float y)
        {
            return new RectangleF(b.Left + 4 * u, y, xs - b.Left - 8 * u, zeichenH);
        }
    }
}
