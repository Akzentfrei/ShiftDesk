using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ShiftDesk.Ausgabe;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Steuerelemente;
using ShiftDesk.Planung;

namespace ShiftDesk.Oberflaeche.Seiten
{
    /// <summary>
    /// Vorschau einer Seite des Plans als Blatt (A4 quer). Mausrad und Doppelklick zoomen, Ziehen verschiebt das
    /// vergrösserte Blatt.
    /// </summary>
    /// <remarks>
    /// Das Blatt wird als Bild zwischengespeichert: ganz oder, bei starkem Zoom, nur der sichtbare Ausschnitt.
    /// Bei Zoom, Verschieben, Grössenänderung und Tippen im Namen zeigt die Vorschau sofort das vorhandene Bild
    /// (verschoben oder gestreckt) und zeichnet das scharfe erst, wenn kurz Ruhe ist. So bleibt sie flüssig.
    /// </remarks>
    public class PlanVorschau : PufferPanel
    {
        const long GanzesBlattMax = 12000000;   // Pixel
        const float Seitenverhaeltnis = 297f / 210f;

        readonly Timer nachzeichnen = new Timer { Interval = 120 };
        Plan plan;
        int seite;

        // Zoom (1 = ganzes Blatt sichtbar) und Verschiebung des vergrösserten Blatts
        float zoom = 1f;
        PointF verschiebung, verschiebungStart;
        bool ziehen;
        Point ziehStart;

        // Zwischenbild: Lage des Blatts beim Zeichnen, wo das Bild dabei lag (Blattecke oder Ausschnitt), für welchen Stand
        Bitmap bild;
        Rectangle bildBlatt;
        Point bildOrt;
        Plan bildPlan;
        int bildSeite, bildStand, druckStand;

        /// <summary>Der Zoom hat sich geändert (Mausrad, Doppelklick, Tastatur).</summary>
        public event Action ZoomGeaendert;

        public PlanVorschau()
        {
            SetStyle(ControlStyles.Selectable, true);   // für Mausrad und Tastatur
            nachzeichnen.Tick += (s, e) =>
            {
                nachzeichnen.Stop();
                if (plan == null || !Visible) return;
                BildZeichnen();
                Invalidate();
            };
            Farbwechsel.Bei(() => BackColor = Thema.Hintergrund);
        }

        /// <summary>Aktueller Zoom (1 = ganze Seite).</summary>
        public float Zoom
        {
            get { return zoom; }
        }

        /// <summary>Zeigt eine Seite eines Plans; ohne Plan einen Hinweis und den Zoom zurückgesetzt.</summary>
        public void Zeigen(Plan plan, int seite)
        {
            this.plan = plan;
            this.seite = seite;
            if (plan == null)
            {
                zoom = 1f;
                verschiebung = PointF.Empty;
                BildFreigeben();
            }
            CursorSetzen();
            Invalidate();
        }

        /// <summary>Name, Einheit oder Sprache haben sich geändert: das Blatt bald neu zeichnen (nicht bei jedem Tastendruck sofort).</summary>
        public void DruckAngabenGeaendert()
        {
            druckStand++;
            Invalidate();
        }

        /// <summary>Ganze Seite zeigen.</summary>
        public void Einpassen()
        {
            zoom = 1f;
            verschiebung = PointF.Empty;
            ZoomMelden();
            Invalidate();
        }

        /// <summary>Um die Mitte hineinzoomen (Tastatur: Ctrl + Plus).</summary>
        public void Hineinzoomen()
        {
            Zoomen(zoom * 1.25f, Mitte);
        }

        /// <summary>Um die Mitte herauszoomen (Tastatur: Ctrl + Minus).</summary>
        public void Herauszoomen()
        {
            Zoomen(zoom / 1.25f, Mitte);
        }

        Point Mitte
        {
            get { return new Point(Width / 2, Height / 2); }
        }

        // ---------------------------------------------------------------- Maus

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (plan != null) Fokus.FuerMausrad(this);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (plan == null) return;
            Zoomen(e.Delta > 0 ? zoom * 1.2f : zoom / 1.2f, e.Location);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || zoom <= 1) return;
            ziehen = true;
            ziehStart = e.Location;
            verschiebungStart = verschiebung;
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!ziehen) return;
            verschiebung = new PointF(verschiebungStart.X + e.X - ziehStart.X, verschiebungStart.Y + e.Y - ziehStart.Y);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            ziehen = false;
            CursorSetzen();
        }

        /// <summary>Doppelklick: hineinzoomen bzw. wieder die ganze Seite zeigen.</summary>
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (plan == null || e.Button != MouseButtons.Left) return;
            if (zoom > 1.01f) Einpassen();
            else Zoomen(2.5f, e.Location);
        }

        /// <summary>Zoomt so, dass der Punkt unter der Maus an der gleichen Stelle bleibt.</summary>
        void Zoomen(float neu, Point um)
        {
            if (plan == null) return;
            var vorher = Blatt();
            float ux = (um.X - vorher.X) / vorher.Width, uy = (um.Y - vorher.Y) / vorher.Height;
            zoom = Math.Max(1f, Math.Min(8f, neu));
            var nachher = Blatt();
            verschiebung = new PointF(verschiebung.X + um.X - ux * nachher.Width - nachher.X,
                                      verschiebung.Y + um.Y - uy * nachher.Height - nachher.Y);
            ZoomMelden();
            Invalidate();
        }

        void ZoomMelden()
        {
            CursorSetzen();
            var h = ZoomGeaendert;
            if (h != null) h();
        }

        /// <summary>Hand über dem vergrösserten Blatt (verschiebbar), beim Ziehen das Verschieben-Kreuz.</summary>
        void CursorSetzen()
        {
            Cursor = plan != null && zoom > 1 ? (ziehen ? Cursors.SizeAll : Cursors.Hand) : Cursors.Default;
        }

        // ---------------------------------------------------------------- Zeichnen

        /// <summary>Position und Grösse des Blatts in der Vorschau; die Verschiebung wird dabei auf den sinnvollen Bereich begrenzt.</summary>
        RectangleF Blatt()
        {
            float breite = Width, hoehe = Height, rand = Thema.S(24);
            float w = Math.Max(1, breite - 2 * rand), h = Math.Max(1, hoehe - 2 * rand);
            if (w / h > Seitenverhaeltnis) w = h * Seitenverhaeltnis; else h = w / Seitenverhaeltnis;
            w *= zoom;
            h *= zoom;
            float maxX = Math.Max(0, (w - breite) / 2 + rand), maxY = Math.Max(0, (h - hoehe) / 2 + rand);
            verschiebung = new PointF(Math.Max(-maxX, Math.Min(maxX, verschiebung.X)), Math.Max(-maxY, Math.Min(maxY, verschiebung.Y)));
            return new RectangleF((breite - w) / 2 + verschiebung.X, (hoehe - h) / 2 + verschiebung.Y, w, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var platz = new RectangleF(0, 0, Width - 1, Height - 1);
            Formen.Fuellen(g, platz, Thema.RadiusKarte, Thema.Vorschau);

            if (plan == null)
            {
                Formen.Leer(g, ClientRectangle, "calendar-days", "Noch kein Plan erstellt", "Zeitraum wählen und oben rechts auf «Plan erstellen» klicken.");
                return;
            }

            var blatt = Blatt();
            if (blatt.Width < 50 || blatt.Height < 50) return;
            using (var p = Formen.Rund(platz, Thema.RadiusKarte))
                g.SetClip(p);
            // weicher Schatten unter dem Blatt
            for (int i = 1; i <= 6; i++)
                Formen.Fuellen(g, new RectangleF(blatt.X - i + 1, blatt.Y - i + 3, blatt.Width + 2 * i - 2, blatt.Height + 2 * i - 2), Thema.S(4) + i, Color.FromArgb(Thema.Dunkel ? 22 : 8, 0, 0, 0));
            Formen.Fuellen(g, blatt, Thema.S(4), Color.White);

            // neuer Plan oder andere Seite: sofort scharf zeichnen
            if (bild == null || bildPlan != plan || bildSeite != seite) BildZeichnen();
            if (bild == null) return;

            var jetzt = Rectangle.Round(blatt);
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            if (jetzt.Size == bildBlatt.Size)
            {
                // nur verschoben: das Bild Pixel für Pixel versetzt
                var ziel = new Rectangle(bildOrt.X + jetzt.X - bildBlatt.X, bildOrt.Y + jetzt.Y - bildBlatt.Y, bild.Width, bild.Height);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(bild, ziel);
                var noetig = Rectangle.Intersect(new Rectangle(Point.Empty, ClientSize), jetzt);
                if (!Rectangle.Inflate(ziel, 1, 1).Contains(noetig) || bildStand != druckStand) Nachzeichnen();
            }
            else
            {
                // Grösse geändert: das vorhandene Bild gestreckt, das scharfe folgt gleich
                float f = blatt.Width / bildBlatt.Width;
                var ziel = new RectangleF(blatt.X + (bildOrt.X - bildBlatt.X) * f, blatt.Y + (bildOrt.Y - bildBlatt.Y) * f, bild.Width * f, bild.Height * f);
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.DrawImage(bild, ziel);
                Nachzeichnen();
            }
        }

        /// <summary>Zeichnet das Blatt scharf ins Zwischenbild (ganz oder nur den sichtbaren Ausschnitt).</summary>
        void BildZeichnen()
        {
            nachzeichnen.Stop();
            var blatt = Rectangle.Round(Blatt());
            Rectangle bereich = (long)blatt.Width * blatt.Height <= GanzesBlattMax
                ? blatt
                : Rectangle.Intersect(new Rectangle(Point.Empty, ClientSize), blatt);
            if (bereich.Width <= 0 || bereich.Height <= 0) return;
            var neu = new Bitmap(bereich.Width, bereich.Height, PixelFormat.Format32bppPArgb);
            using (var gb = Graphics.FromImage(neu))
            {
                gb.Clear(Color.White);
                gb.TranslateTransform(-bereich.X, -bereich.Y);
                float rand = blatt.Width * 0.035f;
                PlanZeichner.Seite(gb, RectangleF.Inflate(blatt, -rand, -rand), plan, seite);
            }
            if (bild != null) bild.Dispose();
            bild = neu;
            bildBlatt = blatt;
            bildOrt = bereich.Location;
            bildPlan = plan;
            bildSeite = seite;
            bildStand = druckStand;
        }

        /// <summary>Das scharfe Bild neu zeichnen, sobald kurz Ruhe ist.</summary>
        void Nachzeichnen()
        {
            nachzeichnen.Stop();
            nachzeichnen.Start();
        }

        void BildFreigeben()
        {
            nachzeichnen.Stop();
            if (bild != null) bild.Dispose();
            bild = null;
            bildPlan = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                nachzeichnen.Dispose();
                BildFreigeben();
            }
            base.Dispose(disposing);
        }
    }
}
