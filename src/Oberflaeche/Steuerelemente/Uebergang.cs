using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;

namespace ShiftDesk.Oberflaeche.Steuerelemente
{
    /// <summary>
    /// Blendet beim Seitenwechsel weich von der alten zur neuen Seite über: erst <see cref="Abdecken"/> mit einem
    /// Bild der alten Seite, darunter umschalten, dann <see cref="Starten"/> mit einem Bild der neuen.
    /// </summary>
    public class Uebergang : Control
    {
        const int Dauer = 160;   // Millisekunden
        Bitmap vorher, nachher;
        readonly Timer timer = new Timer { Interval = 10 };
        DateTime start;
        float fortschritt;

        public Uebergang()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Visible = false;
            timer.Tick += (s, e) =>
            {
                float t = (float)((DateTime.Now - start).TotalMilliseconds / Dauer);
                if (t >= 1) { Beenden(); return; }
                fortschritt = 1 - (1 - t) * (1 - t) * (1 - t);   // bremst zum Ende hin ab
                Refresh();
            };
        }

        /// <summary>Zeigt sofort das alte Bild, damit der Wechsel darunter unsichtbar passiert. Das Bild gehört danach dem Übergang.</summary>
        public void Abdecken(Bitmap alt, Rectangle bereich)
        {
            Beenden();
            vorher = alt;
            fortschritt = 0;
            Bounds = bereich;
            Visible = true;
            BringToFront();
            Refresh();
        }

        /// <summary>Blendet das neue Bild ein und verschwindet danach. Das Bild gehört danach dem Übergang.</summary>
        public void Starten(Bitmap neu)
        {
            nachher = neu;
            start = DateTime.Now;
            timer.Start();
        }

        void Beenden()
        {
            timer.Stop();
            Visible = false;
            if (vorher != null) { vorher.Dispose(); vorher = null; }
            if (nachher != null) { nachher.Dispose(); nachher = null; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (vorher != null) g.DrawImageUnscaled(vorher, 0, 0);
            if (nachher == null) return;
            int versatz = (int)Math.Round(Thema.S(10) * (1 - fortschritt));
            var matrix = new ColorMatrix { Matrix33 = fortschritt };
            using (var attr = new ImageAttributes())
            {
                attr.SetColorMatrix(matrix);
                g.DrawImage(nachher, new Rectangle(0, versatz, nachher.Width, nachher.Height), 0, 0, nachher.Width, nachher.Height, GraphicsUnit.Pixel, attr);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { Beenden(); timer.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
