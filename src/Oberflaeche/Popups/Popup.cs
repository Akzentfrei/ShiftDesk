using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;

namespace ShiftDesk.Oberflaeche.Popups
{
    /// <summary>
    /// Aufklappfenster im eigenen Design: runde Ecken, Schatten, sanftes Einblenden. Schliesst sich beim
    /// Klick daneben, mit Esc – und beim erneuten Klick auf das Feld, das es geöffnet hat (siehe <see cref="GeradeGeschlossen"/>).
    /// </summary>
    public class Popup : ToolStripDropDown
    {
        /// <summary>Eckenradius bei 96 dpi.</summary>
        public const int Radius = 10;
        const int Einblenden = 140;   // Millisekunden

        static object letzterBesitzer;
        static DateTime letzteZeit;

        readonly Control inhalt;
        readonly Timer animation = new Timer { Interval = 12 };
        DateTime animationStart;
        int zielY;

        /// <summary>Das Feld, zu dem das Popup gehört (für Auf-/Zuklappen per Klick auf das Feld).</summary>
        public object Besitzer;

        /// <param name="inhalt">Was im Popup steht; seine Grösse ist die des Popups.</param>
        /// <param name="besitzer">Das Feld, zu dem es gehört (siehe <see cref="GeradeGeschlossen"/>).</param>
        public Popup(Control inhalt, object besitzer = null)
        {
            this.inhalt = inhalt;
            Besitzer = besitzer;
            AutoSize = false;
            Padding = Padding.Empty;
            Margin = Padding.Empty;
            DropShadowEnabled = true;
            BackColor = Thema.Karte;
            Renderer = new PopupRenderer();
            var host = new ToolStripControlHost(inhalt) { AutoSize = false, Margin = Padding.Empty, Padding = Padding.Empty, Size = inhalt.Size };
            Items.Add(host);
            Size = inhalt.Size;
            using (var pfad = Formen.Rund(new RectangleF(0, 0, inhalt.Width, inhalt.Height), Thema.S(Radius)))
                Region = new Region(pfad);

            animation.Tick += (s, e) =>
            {
                double t = Math.Min(1, (DateTime.Now - animationStart).TotalMilliseconds / Einblenden);
                double weich = 1 - Math.Pow(1 - t, 3);
                Opacity = weich;
                Top = zielY - (int)Math.Round(Thema.S(6) * (1 - weich));
                if (t >= 1) animation.Stop();
            };
            Closed += (s, e) =>
            {
                animation.Stop();
                letzterBesitzer = Besitzer;
                letzteZeit = DateTime.Now;
                // erst nach dem Schliessen aufräumen
                var t = new Timer { Interval = 1 };
                t.Tick += (a, b) => { t.Dispose(); Dispose(); };
                t.Start();
            };
        }

        /// <summary>true, wenn das Popup dieses Feldes gerade eben (durch den Klick aufs Feld) geschlossen wurde.</summary>
        public static bool GeradeGeschlossen(object besitzer)
        {
            return besitzer != null && Equals(besitzer, letzterBesitzer) && (DateTime.Now - letzteZeit).TotalMilliseconds < 400;
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            Thema.RundeEcken(Handle);
            inhalt.Focus();
            zielY = Top;
            animationStart = DateTime.Now;
            animation.Start();
        }

        /// <summary>
        /// Unter (oder, wenn kein Platz, über) dem Feld anzeigen; daneben = rechts neben dem Feld (Seitenleiste).
        /// Erst im nächsten Durchgang der Nachrichtenschleife, damit ein gerade schliessendes anderes Popup
        /// fertig zu ist und das neue nicht gleich mit schliesst.
        /// </summary>
        public void ZeigenBei(Control besitzer, Rectangle feld, bool daneben = false)
        {
            besitzer.BeginInvoke(new Action(() =>
            {
                if (IsDisposed || besitzer.IsDisposed || !besitzer.IsHandleCreated) return;
                var bildschirm = Screen.FromControl(besitzer).WorkingArea;
                Point p;
                if (daneben)
                {
                    p = besitzer.PointToScreen(new Point(feld.Right + Thema.S(8), feld.Top));
                    if (p.Y + Height > bildschirm.Bottom)
                        p.Y = besitzer.PointToScreen(new Point(0, feld.Bottom)).Y - Height;
                }
                else
                {
                    p = besitzer.PointToScreen(new Point(feld.Left, feld.Bottom + Thema.S(6)));
                    if (p.Y + Height > bildschirm.Bottom)
                        p.Y = besitzer.PointToScreen(new Point(0, feld.Top)).Y - Height - Thema.S(6);
                }
                p.X = Math.Max(bildschirm.Left, Math.Min(p.X, bildschirm.Right - Width));
                p.Y = Math.Max(bildschirm.Top, p.Y);
                Opacity = 0;
                Show(p);
            }));
        }

        /// <summary>Rand passend zu den runden Ecken; die Inhalte rufen das am Ende ihres Zeichnens auf.</summary>
        public static void Rand(Graphics g, Size groesse)
        {
            var glaettung = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Formen.Umrandung(g, new RectangleF(0.5f, 0.5f, groesse.Width - 1, groesse.Height - 1), Thema.S(Radius), Thema.Rand);
            g.SmoothingMode = glaettung;
        }

        /// <summary>
        /// AltGr (Ctrl+Alt, z. B. für # und @ auf der Schweizer Tastatur) ist keine Menütaste: beim Tippen offen bleiben.
        /// </summary>
        protected override bool ProcessDialogKey(Keys keyData)
        {
            if ((keyData & Keys.Control) == Keys.Control && (keyData & Keys.Alt) == Keys.Alt) return false;
            return base.ProcessDialogKey(keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) animation.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>Hintergrund in der Kartenfarbe, kein Rahmen (den zeichnet der Inhalt mit <see cref="Rand"/>).</summary>
        class PopupRenderer : ToolStripRenderer
        {
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                e.Graphics.Clear(Thema.Karte);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
            }
        }
    }
}
