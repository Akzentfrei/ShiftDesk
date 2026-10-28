using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace ShiftDesk.Oberflaeche.Design
{
    /// <summary>
    /// Zeichnet die eingebetteten Lucide-Icons (SVG, 24 × 24, nur Linien) mit GDI+. Die SVG-Daten stehen in
    /// <c>LucideDaten.cs</c>; jedes Icon wird beim ersten Gebrauch in einen Pfad umgewandelt und behalten.
    /// </summary>
    static partial class Lucide
    {
        static readonly Dictionary<string, GraphicsPath> Pfade = new Dictionary<string, GraphicsPath>();

        /// <summary>Ein Befehlsbuchstabe oder eine Zahl in SVG-Pfaddaten.</summary>
        static readonly Regex Token = new Regex(@"[A-Za-z]|-?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?");

        /// <summary>Zeichnet ein Icon, quadratisch und mittig im Feld. Unbekannte Namen zeichnen nichts.</summary>
        /// <param name="g">Zeichenfläche.</param>
        /// <param name="name">Name wie auf lucide.dev, z. B. «calendar-days».</param>
        /// <param name="feld">Bereich; das Icon nimmt die kürzere Seite ein.</param>
        /// <param name="farbe">Linienfarbe.</param>
        /// <param name="strich">Linienbreite in Icon-Einheiten (24 = ganze Breite).</param>
        public static void Zeichne(Graphics g, string name, RectangleF feld, Color farbe, float strich = 2f)
        {
            GraphicsPath pfad = Pfad(name);
            if (pfad == null) return;
            float s = Math.Min(feld.Width, feld.Height) / 24f;
            GraphicsState zustand = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(feld.X + (feld.Width - 24 * s) / 2, feld.Y + (feld.Height - 24 * s) / 2);
            g.ScaleTransform(s, s);
            using (var stift = new Pen(farbe, strich) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawPath(stift, pfad);
            g.Restore(zustand);
        }

        /// <summary>Der Pfad eines Icons (aus dem Zwischenspeicher oder neu aus den SVG-Daten), null = unbekannt.</summary>
        static GraphicsPath Pfad(string name)
        {
            GraphicsPath p;
            if (Pfade.TryGetValue(name, out p)) return p;
            string daten;
            if (!Daten.TryGetValue(name, out daten)) return null;

            p = new GraphicsPath();
            var doc = new XmlDocument();
            doc.LoadXml("<svg>" + daten + "</svg>");
            foreach (XmlElement e in doc.DocumentElement.ChildNodes.OfType<XmlElement>())
            {
                switch (e.Name)
                {
                    case "path":
                        PfadLesen(p, e.GetAttribute("d"));
                        break;
                    case "circle":
                        p.StartFigure();
                        float r = Zahl(e, "r");
                        p.AddEllipse(Zahl(e, "cx") - r, Zahl(e, "cy") - r, 2 * r, 2 * r);
                        break;
                    case "rect":
                        p.StartFigure();
                        p.AddPath(Formen.Rund(new RectangleF(Zahl(e, "x"), Zahl(e, "y"), Zahl(e, "width"), Zahl(e, "height")), Zahl(e, "rx")), false);
                        break;
                    case "line":
                        p.StartFigure();
                        p.AddLine(Zahl(e, "x1"), Zahl(e, "y1"), Zahl(e, "x2"), Zahl(e, "y2"));
                        break;
                    case "polyline":
                    case "polygon":
                        var z = Token.Matches(e.GetAttribute("points")).Cast<Match>().Select(m => float.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
                        var punkte = Enumerable.Range(0, z.Length / 2).Select(i => new PointF(z[2 * i], z[2 * i + 1])).ToArray();
                        p.StartFigure();
                        p.AddLines(punkte);
                        if (e.Name == "polygon") p.CloseFigure();
                        break;
                }
            }
            Pfade[name] = p;
            return p;
        }

        /// <summary>Zahl aus einem SVG-Attribut, 0 wenn es fehlt.</summary>
        static float Zahl(XmlElement e, string attr)
        {
            float f;
            return float.TryParse(e.GetAttribute(attr), NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : 0;
        }

        /// <summary>Hängt die Befehle eines SVG-Pfads (Attribut «d»: M, L, H, V, C, S, Q, T, A, Z) an den GDI+-Pfad an.</summary>
        static void PfadLesen(GraphicsPath p, string d)
        {
            var t = Token.Matches(d).Cast<Match>().Select(m => m.Value).ToList();
            int i = 0;
            char befehl = ' ', vorher = ' ';
            var akt = new PointF();
            var start = new PointF();
            var kontrolle = new PointF();
            Func<float> N = () => float.Parse(t[i++], CultureInfo.InvariantCulture);

            while (i < t.Count)
            {
                if (char.IsLetter(t[i][0])) befehl = t[i++][0];
                else if (befehl == ' ' || char.ToUpper(befehl) == 'Z') break;

                bool rel = char.IsLower(befehl);
                char b = char.ToUpper(befehl);
                float ox = rel ? akt.X : 0, oy = rel ? akt.Y : 0;
                switch (b)
                {
                    case 'M':
                        akt = start = new PointF(ox + N(), oy + N());
                        p.StartFigure();
                        befehl = rel ? 'l' : 'L';   // weitere Koordinaten sind Linien
                        break;
                    case 'L':
                    {
                        var q = new PointF(ox + N(), oy + N());
                        p.AddLine(akt, q);
                        akt = q;
                        break;
                    }
                    case 'H':
                    {
                        var q = new PointF(ox + N(), akt.Y);
                        p.AddLine(akt, q);
                        akt = q;
                        break;
                    }
                    case 'V':
                    {
                        var q = new PointF(akt.X, oy + N());
                        p.AddLine(akt, q);
                        akt = q;
                        break;
                    }
                    case 'C':
                    {
                        var c1 = new PointF(ox + N(), oy + N());
                        var c2 = new PointF(ox + N(), oy + N());
                        var q = new PointF(ox + N(), oy + N());
                        p.AddBezier(akt, c1, c2, q);
                        kontrolle = c2;
                        akt = q;
                        break;
                    }
                    case 'S':
                    {
                        var c1 = vorher == 'C' || vorher == 'S' ? Spiegeln(kontrolle, akt) : akt;
                        var c2 = new PointF(ox + N(), oy + N());
                        var q = new PointF(ox + N(), oy + N());
                        p.AddBezier(akt, c1, c2, q);
                        kontrolle = c2;
                        akt = q;
                        break;
                    }
                    case 'Q':
                    case 'T':
                    {
                        var k = b == 'Q' ? new PointF(ox + N(), oy + N())
                            : (vorher == 'Q' || vorher == 'T' ? Spiegeln(kontrolle, akt) : akt);
                        var q = new PointF(ox + N(), oy + N());
                        p.AddBezier(akt,
                            new PointF(akt.X + 2f / 3 * (k.X - akt.X), akt.Y + 2f / 3 * (k.Y - akt.Y)),
                            new PointF(q.X + 2f / 3 * (k.X - q.X), q.Y + 2f / 3 * (k.Y - q.Y)), q);
                        kontrolle = k;
                        akt = q;
                        break;
                    }
                    case 'A':
                    {
                        float rx = N(), ry = N(), winkel = N();
                        bool gross = N() != 0, richtung = N() != 0;
                        var q = new PointF(ox + N(), oy + N());
                        Bogen(p, akt, rx, ry, winkel, gross, richtung, q);
                        akt = q;
                        break;
                    }
                    case 'Z':
                        p.CloseFigure();
                        akt = start;
                        break;
                    default:
                        return;
                }
                vorher = b;
            }
        }

        /// <summary>Kontrollpunkt am aktuellen Punkt gespiegelt (für die Kurzformen S und T).</summary>
        static PointF Spiegeln(PointF k, PointF um)
        {
            return new PointF(2 * um.X - k.X, 2 * um.Y - k.Y);
        }

        /// <summary>SVG-Bogen (Endpunkt-Form) als Folge von Bézierkurven, je höchstens ein Viertelkreis.</summary>
        static void Bogen(GraphicsPath p, PointF p0, double rx, double ry, double winkelGrad, bool gross, bool richtung, PointF p1)
        {
            rx = Math.Abs(rx);
            ry = Math.Abs(ry);
            if (rx == 0 || ry == 0 || (p0.X == p1.X && p0.Y == p1.Y))
            {
                p.AddLine(p0, p1);
                return;
            }
            double phi = winkelGrad * Math.PI / 180, cos = Math.Cos(phi), sin = Math.Sin(phi);
            double dx = (p0.X - p1.X) / 2.0, dy = (p0.Y - p1.Y) / 2.0;
            double x1 = cos * dx + sin * dy, y1 = -sin * dx + cos * dy;
            double lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
            if (lambda > 1)
            {
                rx *= Math.Sqrt(lambda);
                ry *= Math.Sqrt(lambda);
            }
            double zaehler = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
            double nenner = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            double koef = (gross == richtung ? -1 : 1) * Math.Sqrt(Math.Max(0, zaehler / nenner));
            double cxs = koef * rx * y1 / ry, cys = -koef * ry * x1 / rx;
            double cx = cos * cxs - sin * cys + (p0.X + p1.X) / 2.0;
            double cy = sin * cxs + cos * cys + (p0.Y + p1.Y) / 2.0;

            double ux = (x1 - cxs) / rx, uy = (y1 - cys) / ry, vx = (-x1 - cxs) / rx, vy = (-y1 - cys) / ry;
            double th1 = Math.Atan2(uy, ux);
            double dth = Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
            if (!richtung && dth > 0) dth -= 2 * Math.PI;
            else if (richtung && dth < 0) dth += 2 * Math.PI;

            int teile = Math.Max(1, (int)Math.Ceiling(Math.Abs(dth) / (Math.PI / 2)));
            double delta = dth / teile, k = 4.0 / 3 * Math.Tan(delta / 4);
            Func<double, double, PointF> abbilden = (a, b) =>
                new PointF((float)(cx + rx * a * cos - ry * b * sin), (float)(cy + rx * a * sin + ry * b * cos));
            PointF vorher = p0;
            for (int i = 0; i < teile; i++)
            {
                double a1 = th1 + i * delta, a2 = a1 + delta;
                var c1 = abbilden(Math.Cos(a1) - k * Math.Sin(a1), Math.Sin(a1) + k * Math.Cos(a1));
                var c2 = abbilden(Math.Cos(a2) + k * Math.Sin(a2), Math.Sin(a2) - k * Math.Cos(a2));
                var ende = i == teile - 1 ? p1 : abbilden(Math.Cos(a2), Math.Sin(a2));
                p.AddBezier(vorher, c1, c2, ende);
                vorher = ende;
            }
        }
    }
}
