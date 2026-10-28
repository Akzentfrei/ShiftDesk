using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Popups;
using ShiftDesk.Oberflaeche.Seiten;
using ShiftDesk.Oberflaeche.Steuerelemente;

namespace ShiftDesk.Oberflaeche
{
    /// <summary>
    /// Seitenleiste links: oben Logo und die Seiten, unten die Datei-Befehle (oder ein Datei-Menü, wenn die Höhe
    /// nicht reicht).
    /// </summary>
    /// <remarks>
    /// Im schmalen Fenster (oder von Hand mit dem Knopf oben rechts) ist sie eingeklappt und zeigt nur noch
    /// Symbole. Beim Klick auf den Knopf fährt sie weich ein und aus; dafür zeichnet eine eigene Schleife in
    /// <see cref="Application.Idle"/> ein Bild nach dem anderen, gleichmässiger als ein Timer.
    /// </remarks>
    public class Seitenleiste : PufferPanel
    {
        const int Fahrdauer = 220;   // Millisekunden

        // MSG von Windows, nur für PeekMessage (wartet eine Nachricht?)
        [StructLayout(LayoutKind.Sequential)]
        struct Nachricht { public IntPtr Fenster; public uint Art; public IntPtr W, L; public uint Zeit; public Point Ort; }

        [DllImport("user32.dll")]
        static extern bool PeekMessage(out Nachricht n, IntPtr fenster, uint von, uint bis, uint entfernen);

        static int BreiteOffen { get { return Thema.S(248); } }
        static int BreiteZu { get { return Thema.S(72); } }

        readonly List<Control> oben = new List<Control>(), unten = new List<Control>();
        readonly Dictionary<NavEintrag, Seite> seiten = new Dictionary<NavEintrag, Seite>();
        readonly List<Dateibefehl> befehle;
        readonly NavEintrag dateiMenue;
        readonly SymbolKnopf knopf;   // ein- und ausklappen (oben rechts)

        bool? eingeklappt;   // von Hand gewählt; null = je nach Fensterbreite
        bool faehrt;
        int fahrtVon, fahrtZiel;
        readonly Stopwatch uhr = new Stopwatch();

        /// <param name="seiten">Die Seiten in der Reihenfolge der Einträge.</param>
        /// <param name="befehle">Die Datei-Befehle.</param>
        /// <param name="zeigen">Wird beim Klick auf den Eintrag einer Seite aufgerufen.</param>
        public Seitenleiste(IEnumerable<Seite> seiten, IEnumerable<Dateibefehl> befehle, Action<Seite> zeigen)
        {
            this.befehle = befehle.ToList();
            Dock = DockStyle.Left;
            Width = BreiteOffen;
            Farbwechsel.Bei(() => BackColor = Thema.Seitenleiste);

            oben.Add(new Marke());
            oben.Add(new Abschnitt("Planung"));
            foreach (var s in seiten)
            {
                var eintrag = new NavEintrag(s.Titel, s.Icon);
                var seite = s;
                eintrag.Click += (o, e) => zeigen(seite);
                this.seiten.Add(eintrag, s);
                oben.Add(eintrag);
            }

            unten.Add(new Abschnitt("Datei"));
            foreach (var b in this.befehle)
            {
                var aktion = b.Aktion;
                var eintrag = new NavEintrag(b.Text, b.Icon, 38);
                eintrag.Click += (s, e) => aktion();
                unten.Add(eintrag);
            }
            dateiMenue = new NavEintrag("Datei", "folder-open");
            dateiMenue.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) DateiMenueZeigen(); };

            knopf = new SymbolKnopf("panel-left-close");
            knopf.Click += (s, e) =>
            {
                // von Hand umschalten; die Wahl gilt dann auch bei anderer Fensterbreite
                eingeklappt = (faehrt ? fahrtZiel : Width) >= Thema.S(150);
                BreiteAnpassen(true);
            };

            foreach (var c in oben.Concat(unten)) Controls.Add(c);
            Controls.Add(dateiMenue);
            Controls.Add(knopf);
            knopf.BringToFront();
            Layout += (s, e) => Anordnen();
        }

        /// <summary>Hebt den Eintrag der angezeigten Seite hervor.</summary>
        public void Markieren(Seite aktiv)
        {
            foreach (var e in seiten) e.Key.Aktiv = e.Value == aktiv;
        }

        /// <summary>Entfernt einen Datei-Befehl (z. B. «Installieren» nach der Installation).</summary>
        public void BefehlEntfernen(string icon)
        {
            befehle.RemoveAll(b => b.Icon == icon);
            var eintrag = unten.OfType<NavEintrag>().FirstOrDefault(n => n.Icon == icon);
            if (eintrag != null)
            {
                unten.Remove(eintrag);
                eintrag.Dispose();
            }
            PerformLayout();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Thema.Rand))
                e.Graphics.DrawLine(p, Width - 1, 0, Width - 1, Height);
        }

        // ---------------------------------------------------------------- Ein- und Ausklappen

        /// <summary>
        /// Passt die Breite an: eingeklappt, wenn von Hand gewählt oder das Fenster schmal ist.
        /// </summary>
        /// <param name="animiert">Weich hinfahren (beim Klick auf den Knopf); sonst sofort (z. B. beim Ziehen am Fensterrand).</param>
        public void BreiteAnpassen(bool animiert = false)
        {
            var fenster = FindForm();
            if (fenster == null || fenster.WindowState == FormWindowState.Minimized) return;
            bool klein = eingeklappt ?? fenster.ClientSize.Width < Thema.S(1200);
            int breite = klein ? BreiteZu : BreiteOffen;
            if (animiert && fenster.Visible)
            {
                if (Width == breite) return;
                // der Inhalt wächst bzw. schrumpft bei jedem Schritt mit: der rechte Rand bleibt fest, nichts springt
                fahrtVon = Width;
                fahrtZiel = breite;
                uhr.Restart();
                if (!faehrt)
                {
                    faehrt = true;
                    Application.Idle += Fahren;
                }
                return;
            }
            FahrtBeenden();
            if (Width != breite) Width = breite;
        }

        /// <summary>Solange keine Nachricht wartet, ein Bild nach dem anderen zeichnen.</summary>
        void Fahren(object sender, EventArgs e)
        {
            Nachricht n;
            var fenster = FindForm();
            while (faehrt && !PeekMessage(out n, IntPtr.Zero, 0, 0, 0))
            {
                double t = Math.Min(1, uhr.Elapsed.TotalMilliseconds / Fahrdauer);
                double weich = 1 - Math.Pow(1 - t, 3);   // bremst zum Ende hin ab
                int breite = fahrtVon + (int)Math.Round((fahrtZiel - fahrtVon) * weich);
                if (breite != Width)
                {
                    Width = breite;
                    if (fenster != null) fenster.Update();
                }
                else Thread.Sleep(1);
                if (t >= 1) FahrtBeenden();
            }
        }

        void FahrtBeenden()
        {
            if (!faehrt) return;
            faehrt = false;
            Application.Idle -= Fahren;
            PerformLayout();   // jetzt die richtigen Breiten und, wenn eingeklappt, nur die Symbole
        }

        // ---------------------------------------------------------------- Anordnen

        void Anordnen()
        {
            // Während der Fahrt behalten die Einträge ihre volle Breite und ihren Text: die Leiste schneidet sie nur
            // ab, so muss nichts neu gezeichnet werden. Erst am Schluss wird auf die Symbole umgestellt.
            bool schmal = !faehrt && Width < Thema.S(150);
            bool ohneLogo = Width < Thema.S(120);   // dort steht der Knopf zum Ausklappen
            // gleicher Rand in beiden Zuständen, damit beim Ein-/Ausklappen nichts springt
            int rand = Thema.S(10);
            int x = rand, y = Thema.S(8);
            int w = (faehrt ? Math.Max(fahrtVon, fahrtZiel) : ClientSize.Width) - 2 * rand - 1;
            foreach (var c in oben.Concat(unten).Concat(new[] { dateiMenue }))
            {
                var n = c as NavEintrag;
                if (n != null) n.Schmal = schmal;
                var m = c as Marke;
                if (m != null && m.Schmal != ohneLogo) { m.Schmal = ohneLogo; m.Invalidate(); }
                var a = c as Abschnitt;
                if (a != null && a.Schmal != schmal) { a.Schmal = schmal; a.Invalidate(); }
            }
            int boden = ClientSize.Height - Thema.S(14);
            // sehr niedriges Fenster: Einträge rücken zusammen, damit nichts unter dem Datei-Menü verschwindet
            int seitenAnzahl = oben.OfType<NavEintrag>().Count();
            bool eng = Thema.S(8 + 72 + 34) + seitenAnzahl * Thema.S(42) + Thema.S(8) + dateiMenue.Height > boden;
            foreach (var c in oben)
            {
                int h = c is NavEintrag ? Thema.S(eng ? 36 : 42) : c is Marke ? Thema.S(eng ? 56 : 72) : Thema.S(eng ? 22 : 34);
                c.SetBounds(x, y, w, h);
                y += h;
                if (c is Marke) KnopfAnordnen(c.Top, h, schmal);
            }
            int untenH = unten.Sum(c => c.Height);
            bool passt = y + Thema.S(8) + untenH <= boden;
            foreach (var c in unten) c.Visible = passt;
            dateiMenue.Visible = !passt;
            if (passt)
            {
                int yy = boden - untenH;
                foreach (var c in unten)
                {
                    c.SetBounds(x, yy, w, c.Height);
                    yy += c.Height;
                }
            }
            else dateiMenue.SetBounds(x, boden - dateiMenue.Height, w, dateiMenue.Height);
        }

        /// <summary>
        /// Knopf zum Ein-/Ausklappen oben rechts: fester Abstand zum rechten Rand, so fährt er beim Ein-/Ausklappen
        /// gleichmässig mit und steht eingeklappt mittig an der Stelle des Logos.
        /// </summary>
        void KnopfAnordnen(int markeOben, int markeHoehe, bool schmal)
        {
            knopf.Icon = schmal ? "panel-left-open" : "panel-left-close";
            knopf.Tipp = schmal ? "Seitenleiste ausklappen" : "Seitenleiste einklappen";
            knopf.Location = new Point(ClientSize.Width - Thema.S(20) - knopf.Width, markeOben + (markeHoehe - knopf.Height) / 2);
            knopf.Invalidate();
        }

        /// <summary>Die Datei-Befehle als Menü rechts neben dem Eintrag «Datei».</summary>
        void DateiMenueZeigen()
        {
            if (Popup.GeradeGeschlossen(dateiMenue)) return;
            var liste = new AuswahlListe(befehle.Select(b => b.Text), null, false, Thema.S(250), null,
                n => befehle.First(b => b.Text == n).Icon);
            var popup = new Popup(liste, dateiMenue);
            liste.Gewaehlt += n =>
            {
                popup.Close();
                // erst nach dem Schliessen ausführen: die Befehle zeigen selbst Dialoge
                BeginInvoke(befehle.First(b => b.Text == n).Aktion);
            };
            popup.ZeigenBei(dateiMenue, new Rectangle(0, 0, dateiMenue.Width, dateiMenue.Height), true);
        }
    }
}
