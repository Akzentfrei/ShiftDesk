using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShiftDesk
{
    class PufferPanel : Panel
    {
        public PufferPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
    }

    // Vorschau, die den Fokus bekommen kann (für Mausrad und Tastatur)
    class ZoomPanel : PufferPanel
    {
        public ZoomPanel()
        {
            SetStyle(ControlStyles.Selectable, true);
        }
    }

    public class MainForm : Form
    {
        static readonly string AutoPfad = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShiftDesk", "letzte.xml");
        static readonly string[] StundenListe = Enumerable.Range(0, 24).Select(h => Zeit.Text(h)).ToArray();
        static readonly string[] TypListe = { "Normal", "Auffang", "Fahrer" };
        static readonly string[] AbloesungListe = { "Jede Stunde", "Alle 2 Stunden", "Alle 3 Stunden", "Alle 4 Stunden" };
        // Index = erlaubte Stunden am Stück draussen, 0 = keine Grenze
        static readonly string[] DraussenListe = { "Keine Grenze", "Höchstens 1 Stunde", "Höchstens 2 Stunden", "Höchstens 3 Stunden", "Höchstens 4 Stunden", "Höchstens 6 Stunden" };
        static readonly int[] DraussenStunden = { 0, 1, 2, 3, 4, 6 };
        static readonly string[] TeamListe = { "Keine festen Teams", "Feste Zweierteams" };

        string dateiPfad;
        Plan plan;
        int seite;

        // alles, was beim Wechsel hell/dunkel neu eingefärbt werden muss
        readonly List<Action> themen = new List<Action>();
        readonly List<Tabelle> tabellen = new List<Tabelle>();

        Panel inhalt;
        readonly Uebergang uebergang = new Uebergang();
        readonly List<KeyValuePair<NavEintrag, Control>> navigation = new List<KeyValuePair<NavEintrag, Control>>();
        DataGridView gridFunktionen, gridGrade, gridPersonen, gridGruppen, gridPosten, gridEssen;
        Tabelle statistik;
        Panel seitePersonen, seitePlan;

        // Seitenleiste: oben Marke und Seiten, unten die Datei-Befehle (oder ein Datei-Menü, wenn die Höhe nicht reicht)
        PufferPanel seitenleiste;
        SymbolKnopf leisteKnopf;   // Seitenleiste von Hand ein- und ausklappen
        bool? leisteEingeklappt;   // null = je nach Fensterbreite
        readonly Timer leisteTimer = new Timer { Interval = 10 };   // weiches Ein-/Ausfahren
        int leisteVon, leisteZiel;
        DateTime leisteStart;
        const int LeisteDauer = 200;   // Millisekunden
        readonly List<Control> seitenleisteOben = new List<Control>(), seitenleisteUnten = new List<Control>();
        readonly List<Tuple<string, string, Action>> dateiBefehle = new List<Tuple<string, string, Action>>();
        NavEintrag dateiMenue;

        // Plan-Seite
        Eingabefeld txtName, txtEinheit;
        DatumFeld dtStart, dtEnde;
        AuswahlFeld abloesung, draussen, stuendlichVon, stuendlichBis, teams, sprache;
        Panel planKoerper, planRechts;
        Karte einstellungen;
        BildlaufPanel einstellungenBild;
        readonly List<PlanFeld> planFelder = new List<PlanFeld>();
        readonly List<Abschnitt> planAbschnitte = new List<Abschnitt>();
        bool? einstellungenSeitlich;
        ZoomPanel vorschau;
        Label lblZoom, lblSeite;
        ModernButton btnEinpassen, btnZurueck, btnVor;
        ModernButton[] ausgabe;   // Drucken, Als PDF, Excel
        Trenner trennerAnsicht, trennerSeite;
        Karte werkzeugleiste;
        Panel werkzeuge;
        Control[] werkzeugReihe;   // Reihenfolge in der Werkzeugleiste
        StatusChip status;
        float zoom = 1f;
        PointF verschiebung, verschiebungStart;
        bool ziehen;
        Point ziehStart;

        public MainForm(string datei = null)
        {
            Text = "ShiftDesk";
            Font = Schrift.Text;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            var bildschirm = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(Theme.S(1440), bildschirm.Width), Math.Min(Theme.S(920), bildschirm.Height));
            MinimumSize = new Size(Math.Min(Theme.S(800), bildschirm.Width), Math.Min(Theme.S(560), bildschirm.Height));
            StartPosition = FormStartPosition.CenterScreen;

            SuspendLayout();
            inhalt = new Panel { Dock = DockStyle.Fill };
            var seiten = new[]
            {
                Tuple.Create("Funktionen & Grade", "id-card", SeiteStammdaten()),
                Tuple.Create("Personen", "users", seitePersonen = SeitePersonen()),
                Tuple.Create("Gruppen & Ruhezeiten", "moon", SeiteGruppen()),
                Tuple.Create("Posten", "map-pin", SeitePosten()),
                Tuple.Create("Essenszeiten", "utensils", SeiteEssen()),
                Tuple.Create("Plan", "calendar-days", seitePlan = SeitePlan()),
                Tuple.Create("Statistik", "chart-column", SeiteStatistik()),
            };
            foreach (var s in seiten)
            {
                s.Item3.Visible = false;
                inhalt.Controls.Add(s.Item3);
            }
            Controls.Add(uebergang);
            Controls.Add(inhalt);
            Controls.Add(Seitenleiste(seiten));
            Thema(() => { BackColor = Theme.Hintergrund; inhalt.BackColor = Theme.Hintergrund; });
            ResumeLayout(false);

            Konfiguration geladen = null;
            try
            {
                if (File.Exists(AutoPfad)) geladen = Konfiguration.Laden(AutoPfad);
            }
            catch
            {
                // nicht lesbar: aufheben, bevor sie beim Schliessen überschrieben wird
                try { File.Copy(AutoPfad, Path.ChangeExtension(AutoPfad, ".defekt.xml"), true); }
                catch { }
            }
            Anzeigen(geladen ?? Konfiguration.Beispiel());
            if (datei != null) DateiOeffnen(datei);
            Zeigen(navigation.First(n => n.Value == seitePersonen).Key);

            EventHandler themaGeaendert = (s, e) =>
            {
                if (IsHandleCreated) BeginInvoke(new Action(ThemaAnwenden));
            };
            Theme.Geaendert += themaGeaendert;
            HandleCreated += (s, e) => ThemaAnwenden();
            Resize += (s, e) => SeitenleisteBreite();
            KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.S) { Speichern(false); e.SuppressKeyPress = true; }
                if (!e.Control || plan == null || !seitePlan.Visible) return;
                if (e.KeyCode == Keys.Add || e.KeyCode == Keys.Oemplus) { Zoomen(zoom * 1.25f, VorschauMitte()); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Subtract || e.KeyCode == Keys.OemMinus) { Zoomen(zoom / 1.25f, VorschauMitte()); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0) { Einpassen(); e.SuppressKeyPress = true; }
            };
            FormClosing += (s, e) =>
            {
                Theme.Geaendert -= themaGeaendert;
                try { Einlesen().Speichern(AutoPfad); }
                catch { }
            };
        }

        void Thema(Action a)
        {
            themen.Add(a);
            a();
        }

        void ThemaAnwenden()
        {
            foreach (var a in themen) a();
            foreach (var t in tabellen) t.FarbenAnwenden();
            Theme.Titelleiste(this);
            Invalidate(true);
        }

        // ---------------------------------------------------------------- Seitenleiste

        Control Seitenleiste(Tuple<string, string, Panel>[] seiten)
        {
            seitenleiste = new PufferPanel { Dock = DockStyle.Left, Width = Theme.S(248) };
            seitenleiste.Paint += (s, e) =>
            {
                using (var p = new Pen(Theme.Rand))
                    e.Graphics.DrawLine(p, seitenleiste.Width - 1, 0, seitenleiste.Width - 1, seitenleiste.Height);
            };
            Thema(() => seitenleiste.BackColor = Theme.Seitenleiste);

            seitenleisteOben.Add(new Marke());
            seitenleisteOben.Add(new Abschnitt("Planung"));
            foreach (var s in seiten)
            {
                var eintrag = new NavEintrag(s.Item1, s.Item2);
                eintrag.Click += (o, e) => Zeigen((NavEintrag)o);
                navigation.Add(new KeyValuePair<NavEintrag, Control>(eintrag, s.Item3));
                seitenleisteOben.Add(eintrag);
            }

            dateiBefehle.Add(Tuple.Create("Neu", "file-plus", (Action)(() =>
            {
                if (!GefahrDialog.Fragen(this, "Alle Eingaben löschen und neu beginnen?",
                    "Was nicht als Datei gespeichert ist, geht verloren.", "Alles löschen", "trash-2")) return;
                dateiPfad = null;
                Anzeigen(Konfiguration.Neu());
            })));
            dateiBefehle.Add(Tuple.Create("Beispiel laden", "rotate-ccw", (Action)(() =>
            {
                if (!GefahrDialog.Fragen(this, "Alle Eingaben durch das Beispiel ersetzen?",
                    "Was nicht als Datei gespeichert ist, geht verloren.", "Ersetzen")) return;
                dateiPfad = null;
                Anzeigen(Konfiguration.Beispiel());
            })));
            dateiBefehle.Add(Tuple.Create("Öffnen…", "folder-open", (Action)Oeffnen));
            dateiBefehle.Add(Tuple.Create("Speichern", "save", (Action)(() => Speichern(false))));
            dateiBefehle.Add(Tuple.Create("Speichern unter…", "save-all", (Action)(() => Speichern(true))));
            if (!Installation.LaeuftInstalliert)
                dateiBefehle.Add(Tuple.Create("Auf diesem PC installieren", "download", (Action)Installieren));

            seitenleisteUnten.Add(new Abschnitt("Datei"));
            foreach (var b in dateiBefehle)
            {
                var aktion = b.Item3;
                var eintrag = new NavEintrag(b.Item1, b.Item2, 38);
                eintrag.Click += (s, e) => aktion();
                seitenleisteUnten.Add(eintrag);
            }
            // reicht die Höhe nicht für alle Datei-Befehle, stehen sie in einem Menü
            dateiMenue = new NavEintrag("Datei", "folder-open");
            dateiMenue.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) DateiMenueZeigen(); };

            leisteKnopf = new SymbolKnopf("panel-left-close");
            leisteKnopf.Click += (s, e) =>
            {
                // von Hand umschalten; die Wahl gilt dann auch bei anderer Fensterbreite
                leisteEingeklappt = (leisteTimer.Enabled ? leisteZiel : seitenleiste.Width) >= Theme.S(150);
                SeitenleisteBreite(true);
            };
            leisteTimer.Tick += (s, e) =>
            {
                double t = Math.Min(1, (DateTime.Now - leisteStart).TotalMilliseconds / LeisteDauer);
                double weich = 1 - Math.Pow(1 - t, 3);   // bremst zum Ende hin ab
                int breite = leisteVon + (int)Math.Round((leisteZiel - leisteVon) * weich);
                seitenleiste.Width = breite;
                inhalt.Left = breite;
                if (t >= 1) LeisteFertig();
            };

            foreach (var c in seitenleisteOben.Concat(seitenleisteUnten)) seitenleiste.Controls.Add(c);
            seitenleiste.Controls.Add(dateiMenue);
            seitenleiste.Controls.Add(leisteKnopf);
            leisteKnopf.BringToFront();
            seitenleiste.Layout += (s, e) => SeitenleisteAnordnen();
            return seitenleiste;
        }

        // Eingeklappt (von Hand oder im schmalen Fenster): die Seitenleiste zeigt nur noch Symbole
        // animiert: weich hinfahren (beim Klick auf den Knopf); sonst sofort (z. B. beim Ziehen am Fensterrand)
        void SeitenleisteBreite(bool animiert = false)
        {
            if (WindowState == FormWindowState.Minimized) return;
            bool klein = leisteEingeklappt ?? ClientSize.Width < Theme.S(1200);
            int breite = klein ? Theme.S(72) : Theme.S(248);
            if (animiert && Visible)
            {
                if (seitenleiste.Width == breite) return;
                leisteVon = seitenleiste.Width;
                leisteZiel = breite;
                leisteStart = DateTime.Now;
                // Neu anordnen ist teuer: während der Fahrt behält der Inhalt seine Grösse und wird nur verschoben.
                // Beim Ausfahren bekommt er gleich die neue, schmalere Breite, beim Einfahren erst am Schluss die breitere.
                int w = ClientSize.Width;
                inhalt.Dock = DockStyle.None;
                inhalt.Bounds = new Rectangle(leisteVon, 0, Math.Min(w - leisteVon, w - leisteZiel), ClientSize.Height);
                leisteTimer.Start();
                return;
            }
            LeisteFertig();
            if (seitenleiste.Width != breite) seitenleiste.Width = breite;
        }

        // Fahrt beenden: der Inhalt füllt wieder den Platz neben der Seitenleiste
        void LeisteFertig()
        {
            leisteTimer.Stop();
            if (inhalt.Dock != DockStyle.Fill) inhalt.Dock = DockStyle.Fill;
        }

        void SeitenleisteAnordnen()
        {
            bool schmal = seitenleiste.Width < Theme.S(150);
            int rand = schmal ? Theme.S(10) : Theme.S(14);
            int x = rand, w = seitenleiste.ClientSize.Width - 2 * rand - 1, y = Theme.S(8);
            foreach (var c in seitenleisteOben.Concat(seitenleisteUnten).Concat(new[] { dateiMenue }))
            {
                var n = c as NavEintrag;
                if (n != null) n.Schmal = schmal;
                var m = c as Marke;
                if (m != null && m.Schmal != schmal) { m.Schmal = schmal; m.Invalidate(); }
                var a = c as Abschnitt;
                if (a != null && a.Schmal != schmal) { a.Schmal = schmal; a.Invalidate(); }
            }
            int unten = seitenleiste.ClientSize.Height - Theme.S(14);
            // sehr niedriges Fenster: Einträge rücken zusammen, damit nichts unter dem Datei-Menü verschwindet
            int seitenAnzahl = seitenleisteOben.OfType<NavEintrag>().Count();
            bool eng = Theme.S(8 + 72 + 34) + (schmal ? Theme.S(36) : 0) + seitenAnzahl * Theme.S(42) + Theme.S(8) + dateiMenue.Height > unten;
            foreach (var c in seitenleisteOben)
            {
                int h = c is NavEintrag ? Theme.S(eng ? 36 : 42) : c is Marke ? Theme.S(eng ? 56 : 72) : Theme.S(eng ? 22 : 34);
                c.SetBounds(x, y, w, h);
                y += h;
                if (!(c is Marke)) continue;
                // Knopf zum Ein-/Ausklappen: breit rechts neben dem Logo, schmal darunter
                leisteKnopf.Icon = schmal ? "panel-left-open" : "panel-left-close";
                leisteKnopf.Tipp = schmal ? "Seitenleiste ausklappen" : "Seitenleiste einklappen";
                if (schmal)
                {
                    leisteKnopf.Location = new Point((seitenleiste.ClientSize.Width - leisteKnopf.Width) / 2, y);
                    y += leisteKnopf.Height + Theme.S(4);
                }
                else leisteKnopf.Location = new Point(x + w - leisteKnopf.Width, c.Top + (h - leisteKnopf.Height) / 2);
                leisteKnopf.Invalidate();
            }
            int untenH = seitenleisteUnten.Sum(c => c.Height);
            bool passt = y + Theme.S(8) + untenH <= unten;
            foreach (var c in seitenleisteUnten) c.Visible = passt;
            dateiMenue.Visible = !passt;
            if (passt)
            {
                int yy = unten - untenH;
                foreach (var c in seitenleisteUnten)
                {
                    c.SetBounds(x, yy, w, c.Height);
                    yy += c.Height;
                }
            }
            else dateiMenue.SetBounds(x, unten - dateiMenue.Height, w, dateiMenue.Height);
        }

        void DateiMenueZeigen()
        {
            if (Popup.GeradeGeschlossen(dateiMenue)) return;
            var liste = new AuswahlListe(dateiBefehle.Select(b => b.Item1), null, false, Theme.S(250), null,
                n => dateiBefehle.First(b => b.Item1 == n).Item2);
            var popup = new Popup(liste, dateiMenue);
            liste.Gewaehlt += n =>
            {
                popup.Close();
                // erst nach dem Schliessen ausführen: die Befehle zeigen selbst Dialoge
                BeginInvoke(dateiBefehle.First(b => b.Item1 == n).Item3);
            };
            popup.ZeigenBei(dateiMenue, new Rectangle(0, 0, dateiMenue.Width, dateiMenue.Height), true);
        }

        // «Installieren» verschwindet nach erfolgreicher Installation
        void Installieren()
        {
            if (!Installation.Installieren(false)) return;
            dateiBefehle.RemoveAll(b => b.Item2 == "download");
            var eintrag = seitenleisteUnten.OfType<NavEintrag>().FirstOrDefault(n => n.Icon == "download");
            if (eintrag != null)
            {
                seitenleisteUnten.Remove(eintrag);
                eintrag.Dispose();
            }
            seitenleiste.PerformLayout();
        }

        void Zeigen(NavEintrag eintrag)
        {
            EditorenSchliessen();
            var ziel = navigation.First(n => n.Key == eintrag).Value;
            var alt = navigation.Select(n => n.Value).FirstOrDefault(v => v.Visible);
            if (alt == ziel) return;

            // altes Bild festhalten, darunter umschalten, dann weich überblenden
            bool animieren = alt != null && Visible && WindowState != FormWindowState.Minimized && inhalt.Width > 0 && inhalt.Height > 0;
            if (animieren) uebergang.Abdecken(Schnappschuss(), inhalt.Bounds);

            inhalt.SuspendLayout();
            ziel.Visible = true;
            foreach (var n in navigation)
            {
                n.Key.Aktiv = n.Key == eintrag;
                if (n.Value != ziel) n.Value.Visible = false;
            }
            inhalt.ResumeLayout(true);

            if (animieren) uebergang.Starten(Schnappschuss());
        }

        Bitmap Schnappschuss()
        {
            // vormultipliziertes Alpha: das Überblenden zeichnet damit deutlich schneller
            var bmp = new Bitmap(inhalt.Width, inhalt.Height, PixelFormat.Format32bppPArgb);
            inhalt.DrawToBitmap(bmp, new Rectangle(0, 0, inhalt.Width, inhalt.Height));
            return bmp;
        }

        // ---------------------------------------------------------------- Seiten

        // Seite mit Titelzeile; die Erklärung steckt im Info-Knopf neben dem Titel.
        // Im schmalen Fenster wird der Rand kleiner.
        Panel NeueSeite(string titel, string info, Control inhaltControl, params ModernButton[] knoepfe)
        {
            var seitePanel = new Panel { Dock = DockStyle.Fill };
            inhaltControl.Dock = DockStyle.Fill;
            seitePanel.Controls.Add(inhaltControl);
            seitePanel.Controls.Add(Kopfzeile(titel, info, Schrift.Seitentitel, Theme.S(62), Theme.S(14), knoepfe));
            Action rand = () =>
            {
                var p = seitePanel.Width < Theme.S(860)
                    ? new Padding(Theme.S(20), Theme.S(16), Theme.S(20), Theme.S(20))
                    : new Padding(Theme.S(32), Theme.S(24), Theme.S(32), Theme.S(28));
                if (seitePanel.Padding != p) seitePanel.Padding = p;
            };
            seitePanel.Resize += (s, e) => rand();
            rand();
            Thema(() => seitePanel.BackColor = Theme.Hintergrund);
            return seitePanel;
        }

        // Titelzeile einer Seite oder eines Bereichs: Titel, evtl. Info-Knopf, rechts die Knöpfe.
        // Wird es eng, zeigen zuerst die Nebenknöpfe und dann alle nur noch ihr Symbol (Text als Kurzinfo).
        Panel Kopfzeile(string titel, string info, Font schrift, int hoehe, int abstandUnten, ModernButton[] knoepfe)
        {
            var kopf = new Panel { Dock = DockStyle.Top, Height = hoehe };
            int zeile = hoehe - abstandUnten;
            var lbl = new Label { Text = titel, Font = schrift, AutoSize = true, UseMnemonic = false };
            lbl.Location = new Point(-Theme.S(schrift.Size > 15 ? 3 : 1), (zeile - lbl.PreferredHeight) / 2);
            var leiste = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(0, Math.Max(0, (zeile - Theme.KnopfHoehe) / 2), 0, 0) };
            foreach (var k in knoepfe) leiste.Controls.Add(k);
            kopf.Controls.Add(lbl);
            kopf.Controls.Add(leiste);
            Control rechtsVomTitel = lbl;
            if (!string.IsNullOrEmpty(info))
            {
                var knopf = new InfoKnopf(titel, info, 22);
                knopf.Location = new Point(lbl.Left + lbl.PreferredWidth + Theme.S(4), lbl.Top + (lbl.PreferredHeight - knopf.Height) / 2 + Theme.S(2));
                kopf.Controls.Add(knopf);
                rechtsVomTitel = knopf;
            }
            kopf.Resize += (s, e) =>
            {
                int frei = kopf.Width - rechtsVomTitel.Right - Theme.S(16);
                Func<bool, bool, int> breite = (neben, alle) => knoepfe.Sum(k => k.Margin.Horizontal + ((k.Primaer ? alle : neben) ? k.Height : k.VolleBreite));
                bool nebenKompakt = breite(false, false) > frei;
                bool alleKompakt = nebenKompakt && breite(true, false) > frei;
                foreach (var k in knoepfe) k.Kompakt = k.Primaer ? alleKompakt : nebenKompakt;
            };
            Thema(() =>
            {
                kopf.BackColor = leiste.BackColor = lbl.BackColor = Theme.Hintergrund;
                lbl.ForeColor = Theme.Text;
            });
            return kopf;
        }

        ModernButton Knopf(string text, string icon, Action aktion, bool primaer = false)
        {
            var b = new ModernButton(text, icon, primaer);
            b.Click += (s, e) => aktion();
            return b;
        }

        // Tabelle, die beim Wechsel hell/dunkel mit umgefärbt wird
        Tabelle NeueTabelle(bool nurAnzeige = false)
        {
            var t = new Tabelle(nurAnzeige);
            tabellen.Add(t);
            return t;
        }

        Tabelle TabelleVon(DataGridView g)
        {
            return tabellen.First(t => t.Grid == g);
        }

        void EditorenSchliessen(bool uebernehmen = true)
        {
            foreach (var t in tabellen) t.Schliessen(uebernehmen);
        }

        Panel SeiteStammdaten()
        {
            var funktionen = NeueTabelle();
            gridFunktionen = funktionen.Grid;
            funktionen.Spalte("Name", "Funktion", 150, new SpaltenInfo(SpaltenArt.Text));
            funktionen.Spalte("ImPlan", "Im Plan einteilen", 80, new SpaltenInfo(SpaltenArt.Schalter));
            Zeilenknoepfe(funktionen, true);
            funktionen.Erklaeren("ImPlan", "Aus = Personen mit dieser Funktion werden nicht eingeteilt und stehen nur unten auf dem Plan bei «Nicht eingeteilt» (z. B. Reserve).");
            funktionen.Umbenannt += (z, alt, neu) =>
            {
                Ersetzen(gridPersonen, "Funktion", alt, neu);
                foreach (DataGridViewRow r in gridPosten.Rows)
                {
                    var liste = r.Cells["Funktionen"].Tag as List<string>;
                    if (liste != null && liste.Contains(alt))
                        r.Cells["Funktionen"].Tag = liste.Select(x => x == alt ? neu : x).ToList();
                }
            };
            funktionen.LeerHinweis = new[] { "id-card", "Noch keine Funktionen", "Mit «Funktion» oben rechts eine erfassen." };
            var grade = NeueTabelle();
            gridGrade = grade.Grid;
            grade.Spalte("Name", "Grad", 100, new SpaltenInfo(SpaltenArt.Text));
            Zeilenknoepfe(grade, true);
            grade.Umbenannt += (z, alt, neu) => Ersetzen(gridPersonen, "Grad", alt, neu);
            grade.LeerHinweis = new[] { "id-card", "Noch keine Grade", "Mit «Grad» oben rechts einen erfassen." };

            var links = Bereich("Funktionen", funktionen, Knopf("Funktion", "plus", () => Hinzufuegen(gridFunktionen, "", true), true));
            var rechts = Bereich("Grade", grade, Knopf("Grad", "plus", () => Hinzufuegen(gridGrade, ""), true));
            var raster = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = Padding.Empty };
            raster.Controls.Add(links, 0, 0);
            raster.Controls.Add(rechts, 1, 0);
            // nebeneinander, im schmalen Fenster untereinander
            bool? gestapelt = null;
            raster.Resize += (s, e) =>
            {
                bool neu = raster.Width < Theme.S(720);
                if (gestapelt == neu || raster.Width <= 0) return;
                gestapelt = neu;
                raster.SuspendLayout();
                raster.ColumnStyles.Clear();
                raster.RowStyles.Clear();
                raster.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, neu ? 100 : 58));
                raster.ColumnStyles.Add(neu ? new ColumnStyle(SizeType.Absolute, 0) : new ColumnStyle(SizeType.Percent, 42));
                raster.RowStyles.Add(new RowStyle(SizeType.Percent, neu ? 58 : 100));
                raster.RowStyles.Add(neu ? new RowStyle(SizeType.Percent, 42) : new RowStyle(SizeType.Absolute, 0));
                raster.SetCellPosition(rechts, new TableLayoutPanelCellPosition(neu ? 0 : 1, neu ? 1 : 0));
                links.Margin = neu ? new Padding(0, 0, 0, Theme.S(12)) : new Padding(0, 0, Theme.S(12), 0);
                rechts.Margin = neu ? new Padding(0, Theme.S(12), 0, 0) : new Padding(Theme.S(12), 0, 0, 0);
                raster.ResumeLayout(true);
            };
            Thema(() => raster.BackColor = Theme.Hintergrund);
            return NeueSeite("Funktionen & Grade",
                "Lege hier fest, welche Funktionen und Grade es gibt. Bei den Personen wählst du sie dann nur noch aus. Die Reihenfolge der Funktionen ist die Reihenfolge im Plan.",
                raster);
        }

        // Bereich mit eigener Titelzeile und Tabelle (zwei davon auf «Funktionen & Grade»)
        Control Bereich(string titel, Tabelle tabelle, params ModernButton[] knoepfe)
        {
            var p = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            tabelle.Karte.Dock = DockStyle.Fill;
            p.Controls.Add(tabelle.Karte);
            p.Controls.Add(Kopfzeile(titel, null, Schrift.Titel, Theme.S(52), Theme.S(10), knoepfe));
            Thema(() => p.BackColor = Theme.Hintergrund);
            return p;
        }

        Panel SeitePersonen()
        {
            var t = NeueTabelle();
            gridPersonen = t.Grid;
            t.Spalte("Grad", "Grad", 50, new SpaltenInfo(SpaltenArt.Auswahl, () => Namen(gridGrade), Waehlen));
            t.Spalte("Name", "Name", 130, new SpaltenInfo(SpaltenArt.Text) { Symbol = r => Schalter(r, "Fahrer") ? "car" : null }, 120);
            t.Spalte("Funktion", "Funktion", 85, new SpaltenInfo(SpaltenArt.Auswahl, () => Namen(gridFunktionen), Waehlen));
            t.Spalte("Gruppe", "Gruppe (Ruhezeit)", 85, new SpaltenInfo(SpaltenArt.Auswahl, () => new[] { "" }.Concat(Namen(gridGruppen)), Keine));
            t.Spalte("Fahrer", "Fahrer", 42, new SpaltenInfo(SpaltenArt.Schalter));
            Zeilenknoepfe(t, true);
            t.Erklaeren("Grad", "Wählst du aus der Liste der Seite «Funktionen & Grade».");
            t.Erklaeren("Funktion", "Wählst du aus der Liste der Seite «Funktionen & Grade». Die Funktion bestimmt, welche Posten die Person besetzen darf.");
            t.Erklaeren("Gruppe", "Die Gruppe bestimmt die Ruhezeit (Seite «Gruppen & Ruhezeiten»). Ohne Gruppe hat die Person keine Ruhezeit.");
            t.Erklaeren("Fahrer", "Fahrer haben im Plan nur den Posten vom Typ «Fahrer» (z. B. Presto) und Ruhezeit, sonst nichts, auch keine Essensstunde: sie essen in der Ruhezeit. Im Plan sind sie mit einem Auto markiert.");
            t.LeerHinweis = new[] { "users", "Noch keine Personen", "Mit «Person hinzufügen» oben rechts die Wache erfassen." };
            return NeueSeite("Personen",
                "Alle Personen der Wache. Die Reihenfolge änderst du mit den Pfeilen. Bei «Feste Zweierteams» (Plan-Seite) bilden je zwei aufeinanderfolgende Personen mit gleicher Funktion und Gruppe ein Team, das immer zusammen eingeteilt wird.",
                t.Karte,
                Knopf("Person hinzufügen", "plus", NeuePerson, true));
        }

        Panel SeiteGruppen()
        {
            var t = NeueTabelle();
            gridGruppen = t.Grid;
            t.Spalte("Name", "Gruppe", 140, new SpaltenInfo(SpaltenArt.Text));
            t.Spalte("Von", "Ruhe von", 80, StundenInfo());
            t.Spalte("Bis", "Ruhe bis", 80, StundenInfo());
            Zeilenknoepfe(t, false);
            t.Umbenannt += (z, alt, neu) => { if (z.OwningColumn.Name == "Name") Ersetzen(gridPersonen, "Gruppe", alt, neu); };
            t.Erklaeren("Von", "Von wann bis wann die Gruppe ruht, auch über Mitternacht (z. B. 23:00 bis 07:00). Gleiche Zeit bei «von» und «bis» bedeutet keine Ruhezeit.");
            t.LeerHinweis = new[] { "moon", "Noch keine Gruppen", "Mit «Gruppe hinzufügen» oben rechts eine erfassen." };
            return NeueSeite("Gruppen & Ruhezeiten",
                "Jede Gruppe ruht jeden Tag im gleichen Zeitfenster. Auch die Wachtkommandanten brauchen je eine Gruppe.",
                t.Karte,
                Knopf("Gruppe hinzufügen", "plus", () => Hinzufuegen(gridGruppen, "", Zeit.Text(23), Zeit.Text(7)), true));
        }

        Panel SeitePosten()
        {
            var t = NeueTabelle();
            gridPosten = t.Grid;
            t.Spalte("Name", "Posten", 105, new SpaltenInfo(SpaltenArt.Text) { Symbol = r => Schalter(r, "Draussen") ? "snowflake" : null }, 120);
            t.Spalte("Farbe", "Farbe", 55, new SpaltenInfo(SpaltenArt.Farbe), 74);
            t.Spalte("Anzahl", "Personen", 48, new SpaltenInfo(SpaltenArt.Zahl));
            t.Spalte("Funktionen", "Funktionen", 125, new SpaltenInfo(SpaltenArt.Mehrfach, () => Namen(gridFunktionen), s => s == "" ? "Alle" : s), 116);
            t.Spalte("Von", "Offen von", 58, StundenInfo());
            t.Spalte("Bis", "Offen bis", 58, StundenInfo());
            t.Spalte("Typ", "Typ", 62, new SpaltenInfo(SpaltenArt.Auswahl, () => TypListe) { Fest = true }, 112);
            t.Spalte("Draussen", "Draussen", 42, new SpaltenInfo(SpaltenArt.Schalter));
            t.Spalte("Essen", "Essen", 42, new SpaltenInfo(SpaltenArt.Schalter));
            Zeilenknoepfe(t, true);
            t.Erklaeren("Funktionen", "Wer den Posten besetzen darf. «Alle» = jede Funktion, die im Plan eingeteilt wird.");
            t.Erklaeren("Von", "Von wann bis wann der Posten besetzt ist, auch über Mitternacht. Gleiche Zeit bei «von» und «bis» = immer offen.");
            t.Erklaeren("Typ", "«Normal»: gewöhnlicher Posten. «Auffang» (z. B. Pikett): bekommt alle übrigen Leute. «Fahrer» (z. B. Presto): der erste Platz ist für einen Fahrer, die weiteren Plätze bekommen Fahrer, wenn welche da sind, sonst andere Leute; Fahrer selbst sind nur dort oder in Ruhe.");
            t.Erklaeren("Draussen", "Posten im Freien, im Plan mit Schneeflocke. Niemand bleibt länger am Stück draussen als auf der Plan-Seite bei «Am Stück draussen» eingestellt.");
            t.Erklaeren("Essen", "Auf diesem Posten kann man essen: hier bekommen die Leute ihre Essensstunde (Seite «Essenszeiten»).");
            return NeueSeite("Posten",
                "Alle Posten sollen immer voll besetzt sein. Reichen die Leute nicht, entscheidet die Reihenfolge: unten bleibt zuerst leer. Wacht Kdt, Presto und Subito gibt es immer; Wacht Kdt und Stv bleiben möglichst am Stück auf ihrem Posten.",
                t.Karte,
                Knopf("Posten hinzufügen", "plus", () =>
                {
                    int i = Hinzufuegen(gridPosten, "", "", "1", "", Zeit.Text(0), Zeit.Text(0), "Normal", false, false);
                    gridPosten.Rows[i].Cells["Farbe"].Tag = Color.FromArgb(148, 163, 184);
                    gridPosten.Rows[i].Cells["Funktionen"].Tag = new List<string>();
                }, true));
        }

        Panel SeiteEssen()
        {
            var t = NeueTabelle();
            gridEssen = t.Grid;
            t.Spalte("Name", "Essenszeit", 140, new SpaltenInfo(SpaltenArt.Text));
            t.Spalte("Von", "von", 80, StundenInfo());
            t.Spalte("Bis", "bis", 80, StundenInfo());
            Zeilenknoepfe(t, false);
            t.Erklaeren("Von", "Von wann bis wann gegessen wird. Jede Person, die das ganze Fenster im Dienst ist, bekommt darin eine Stunde zum Essen.");
            t.LeerHinweis = new[] { "utensils", "Keine Essenszeiten", "Ohne Essenszeiten plant ShiftDesk keine Essensstunden ein." };
            return NeueSeite("Essenszeiten",
                "In jedem Zeitfenster bekommt jede Person im Dienst eine Stunde zum Essen, auf einem Posten mit «Essen». Wer zur Essenszeit in Ruhe ist, isst in der Ruhezeit. Im Plan steht das Besteck oben links im Postenfeld, wenn vor der Ablösung gegessen wird, oben rechts, wenn danach; mitten in der Ruhezeit steht es gross in der Stunde.",
                t.Karte,
                Knopf("Essenszeit hinzufügen", "plus", () => Hinzufuegen(gridEssen, "", Zeit.Text(11), Zeit.Text(13)), true));
        }

        Panel SeiteStatistik()
        {
            statistik = NeueTabelle(true);
            statistik.LeerHinweis = new[] { "chart-column", "Noch kein Plan erstellt", "Auf der Seite «Plan» auf «Plan erstellen» klicken." };
            return NeueSeite("Statistik",
                "Wie viele Stunden jede Person auf welchem Posten eingeteilt ist. So siehst du auf einen Blick, ob die Verteilung gerecht ist.",
                statistik.Karte);
        }

        // ---------------------------------------------------------------- Plan-Seite

        // Ein Eingabefeld der Plan-Einstellungen mit seiner natürlichen Breite (oben) bzw. ganzer oder halber Spaltenbreite (links)
        class PlanFeld
        {
            public Panel Feld;
            public Control Eingabe;
            public int Breite, EingabeBreite;
            public bool Halb;
        }

        Panel SeitePlan()
        {
            txtName = new Eingabefeld { Width = Theme.S(280) };
            txtEinheit = new Eingabefeld { Width = Theme.S(200) };
            dtStart = new DatumFeld();
            dtEnde = new DatumFeld();
            sprache = new AuswahlFeld(Druck.Sprachen.Select(s => s.Name).ToArray()) { Width = Theme.S(140) };
            abloesung = new AuswahlFeld(AbloesungListe) { Width = Theme.S(170) };
            stuendlichVon = new AuswahlFeld(StundenListe) { Width = Theme.S(100) };
            stuendlichBis = new AuswahlFeld(StundenListe) { Width = Theme.S(100) };
            draussen = new AuswahlFeld(DraussenListe) { Width = Theme.S(190) };
            teams = new AuswahlFeld(TeamListe) { Width = Theme.S(190) };
            Thema(() => { txtName.ThemaAnwenden(); txtEinheit.ThemaAnwenden(); });
            // Name, Einheit und Sprache sind nur für den Ausdruck: sofort in der Vorschau, ohne neuen Plan
            txtName.Box.TextChanged += (s, e) => DruckAngabenUebernehmen();
            txtEinheit.Box.TextChanged += (s, e) => DruckAngabenUebernehmen();
            sprache.WertGeaendert += (s, e) => DruckAngabenUebernehmen();

            var felder = new FlowLayoutPanel { WrapContents = true, Margin = Padding.Empty };
            Action<string> abschnitt = titel =>
            {
                // ab dem zweiten Abschnitt eine feine Trennlinie darüber
                bool linie = planAbschnitte.Count > 0;
                var a = new Abschnitt(titel, 0) { Height = Theme.S(linie ? 36 : 24), Margin = Padding.Empty, Trennlinie = linie };
                planAbschnitte.Add(a);
                felder.Controls.Add(a);
            };
            abschnitt("Zeitraum");
            felder.Controls.Add(Feld("Start", dtStart));
            felder.Controls.Add(Feld("Ende", dtEnde, "Der Plan kann über mehrere Tage gehen (höchstens 31). Je 24 Stunden kommen auf eine Seite."));
            abschnitt("Ablösung");
            felder.Controls.Add(Feld("Ablösung", abloesung, "Wie oft die Posten wechseln, ab Mitternacht gerechnet (z. B. alle 2 Stunden: 00:00, 02:00, 04:00 …)."));
            felder.Controls.Add(Feld("Jede Stunde von", stuendlichVon, "In diesem Zeitfenster (z. B. 00:00 bis 06:00) wird trotz längerer Ablösung jede Stunde abgelöst. Gleiche Zeit bei «von» und «bis» = kein Fenster.", true));
            felder.Controls.Add(Feld("bis", stuendlichBis, null, true));
            felder.Controls.Add(Feld("Am Stück draussen", draussen,
                "So lange bleibt höchstens jemand am Stück auf Posten im Freien (Schalter «Draussen» bei den Posten). Bei längeren Ablösungen tauscht der Planer mitten im Block mit jemandem von drinnen, sonst gibt es einen Hinweis."));
            felder.Controls.Add(Feld("Teams", teams,
                "«Feste Zweierteams»: je zwei aufeinanderfolgende Personen der Personen-Tabelle mit gleicher Funktion und Gruppe sind immer zusammen auf dem gleichen Posten. Posten mit 2 Plätzen bekommen ein Team, einzelne Plätze eine Einzelperson."));
            abschnitt("Ausdruck");
            felder.Controls.Add(Feld("Name", txtName,
                "Steht auf dem Ausdruck bei «Für». Ändert den Plan nicht und ist sofort in der Vorschau zu sehen."));
            felder.Controls.Add(Feld("Einheit", txtEinheit,
                "Steht auf dem Ausdruck bei «Einheit». Ändert den Plan nicht und ist sofort in der Vorschau zu sehen.", true));
            felder.Controls.Add(Feld("Sprache", sprache,
                "Sprache des Ausdrucks (Deutsch, Französisch, Italienisch oder Englisch). Namen von Posten, Funktionen und Personen bleiben so, wie du sie erfasst hast.", true));
            einstellungenBild = new BildlaufPanel(felder) { Dock = DockStyle.Fill, OhneLeiste = true };
            einstellungen = new Karte();
            einstellungen.Controls.Add(einstellungenBild);
            Thema(() => { einstellungen.BackColor = felder.BackColor = einstellungenBild.BackColor = Theme.Karte; });

            // Statuszeile: Hinweise, Zoom, Seitenwahl
            status = new StatusChip { Location = new Point(0, Theme.S(11)) };
            // Hinweise erscheinen als Popup auf Klick auf den Status-Chip
            status.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left || plan == null || plan.Warnungen.Count == 0 || Popup.GeradeGeschlossen(status)) return;
                var popup = HinweisText.Zeigen(status, new Rectangle(0, 0, status.Width, status.Height), status, status.Text, plan.Warnungen);
                status.Offen = true;
                popup.Closed += (o, a) => status.Offen = false;
            };
            // Werkzeugleiste über der Vorschau: links der Status, rechts die Gruppen Ansicht | Seite | Ausgabe
            lblZoom = new Label { AutoSize = false, Width = Theme.S(84), Height = Theme.KnopfHoehe, TextAlign = ContentAlignment.MiddleCenter, Font = Schrift.Fett, Margin = new Padding(Theme.S(4), 0, 0, 0) };
            btnEinpassen = Knopf("", "maximize", Einpassen);
            btnEinpassen.Tipp = "Ganze Seite zeigen";
            lblSeite = new Label { AutoSize = false, Width = Theme.S(110), Height = Theme.KnopfHoehe, TextAlign = ContentAlignment.MiddleCenter, Font = Schrift.Fett, Margin = new Padding(Theme.S(4), 0, 0, 0) };
            btnZurueck = Knopf("", "chevron-left", () => Blaettern(-1));
            btnZurueck.Tipp = "Vorherige Seite";
            btnVor = Knopf("", "chevron-right", () => Blaettern(1));
            btnVor.Tipp = "Nächste Seite";
            ausgabe = new[] { Knopf("Drucken", "printer", Drucken), Knopf("Als PDF", "file-down", AlsPdf), Knopf("Excel", "file-spreadsheet", AlsExcel) };
            trennerAnsicht = new Trenner();
            trennerSeite = new Trenner();
            werkzeuge = new Panel { Dock = DockStyle.Right };
            werkzeugReihe = new Control[] { lblZoom, btnEinpassen, trennerAnsicht, btnZurueck, lblSeite, btnVor, trennerSeite }.Concat(ausgabe).ToArray();
            werkzeuge.Controls.AddRange(werkzeugReihe);

            werkzeugleiste = new Karte { Dock = DockStyle.Top, Height = Theme.KnopfHoehe + Theme.S(20), Padding = new Padding(Theme.S(14), Theme.S(10), Theme.S(10), Theme.S(10)) };
            status.Location = new Point(Theme.S(14), (werkzeugleiste.Height - status.Height) / 2);
            werkzeugleiste.Controls.Add(status);
            werkzeugleiste.Controls.Add(werkzeuge);
            // wird es eng, verschwindet zuerst die Zoom-Anzeige, dann zeigen die Ausgabe-Knöpfe nur ihr Symbol
            werkzeugleiste.Resize += (s, e) => ZoomAnzeigen();
            var leisteAbstand = new Panel { Dock = DockStyle.Top, Height = Theme.S(12) };
            Thema(() =>
            {
                werkzeuge.BackColor = lblSeite.BackColor = lblZoom.BackColor = Theme.Karte;
                lblSeite.ForeColor = lblZoom.ForeColor = Theme.Text;
                leisteAbstand.BackColor = Theme.Hintergrund;
                werkzeugleiste.Invalidate(true);
            });

            // Vorschau: Mausrad zoomt, Ziehen verschiebt, Doppelklick zoomt hinein bzw. passt ein
            vorschau = new ZoomPanel { Dock = DockStyle.Fill };
            vorschau.Paint += VorschauZeichnen;
            vorschau.MouseEnter += (s, e) => { if (plan != null) Fokus.FuerMausrad(vorschau); };
            vorschau.MouseWheel += (s, e) =>
            {
                if (plan == null) return;
                Zoomen(e.Delta > 0 ? zoom * 1.2f : zoom / 1.2f, e.Location);
            };
            vorschau.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left || zoom <= 1) return;
                ziehen = true;
                ziehStart = e.Location;
                verschiebungStart = verschiebung;
                vorschau.Cursor = Cursors.SizeAll;
            };
            vorschau.MouseMove += (s, e) =>
            {
                if (!ziehen) return;
                verschiebung = new PointF(verschiebungStart.X + e.X - ziehStart.X, verschiebungStart.Y + e.Y - ziehStart.Y);
                vorschau.Invalidate();
            };
            vorschau.MouseUp += (s, e) =>
            {
                ziehen = false;
                ZoomAnzeigen();
            };
            vorschau.MouseDoubleClick += (s, e) =>
            {
                if (plan == null || e.Button != MouseButtons.Left) return;
                if (zoom > 1.01f) Einpassen();
                else Zoomen(2.5f, e.Location);
            };
            vorschauTimer.Tick += (s, e) =>
            {
                vorschauTimer.Stop();
                if (plan == null || !vorschau.Visible) return;
                VorschauRendern();
                vorschau.Invalidate();
            };
            Thema(() => vorschau.BackColor = Theme.Hintergrund);

            planRechts = new Panel();
            planRechts.Controls.Add(vorschau);
            planRechts.Controls.Add(leisteAbstand);
            planRechts.Controls.Add(werkzeugleiste);
            planKoerper = new Panel { Dock = DockStyle.Fill };
            planKoerper.Controls.Add(einstellungen);
            planKoerper.Controls.Add(planRechts);
            planKoerper.Layout += (s, e) => PlanAnordnen();
            Thema(() => planKoerper.BackColor = planRechts.BackColor = Theme.Hintergrund);

            return NeueSeite("Plan",
                "Zeitraum wählen und auf «Plan erstellen» klicken. Jeder Klick ergibt eine neue, gleich faire Variante; Hinweise zeigt ein Klick auf den Status. Zoomen mit dem Mausrad oder Doppelklick, den vergrösserten Plan mit der Maus verschieben.",
                planKoerper,
                Knopf("Plan erstellen", "sparkles", Erstellen, true));
        }

        // Eingabe mit Beschriftung darüber; mit info steht ein Info-Knopf neben der Beschriftung
        Panel Feld(string titel, Control eingabe, string info = null, bool halb = false)
        {
            var p = new Panel { Width = eingabe.Width, Height = Theme.S(24) + eingabe.Height, Margin = new Padding(0, 0, Theme.S(18), Theme.S(6)) };
            var l = new Label { Text = titel, AutoSize = true, Location = Point.Empty, Font = Schrift.Beschriftung, UseMnemonic = false };
            if (info != null) p.Width = Math.Max(p.Width, l.PreferredWidth + Theme.S(22));
            eingabe.Location = new Point(0, Theme.S(24));
            p.Controls.Add(l);
            p.Controls.Add(eingabe);
            InfoKnopf knopf = null;
            if (info != null)
            {
                knopf = new InfoKnopf(titel, info, 18);
                knopf.Location = new Point(l.PreferredWidth + Theme.S(2), Math.Max(0, (l.PreferredHeight - knopf.Height) / 2));
                p.Controls.Add(knopf);
            }
            Thema(() =>
            {
                p.BackColor = l.BackColor = Theme.Karte;
                l.ForeColor = Theme.Gedaempft;
                if (knopf != null) knopf.Invalidate();
            });
            planFelder.Add(new PlanFeld { Feld = p, Eingabe = eingabe, Breite = p.Width, EingabeBreite = eingabe.Width, Halb = halb });
            return p;
        }

        // Breites Fenster: Einstellungen als Spalte links, Vorschau rechts daneben (am meisten Platz fürs Blatt).
        // Schmales Fenster: Einstellungen oben (ganz, solange die Vorschau genug Platz behält), Vorschau darunter.
        void PlanAnordnen()
        {
            int w = planKoerper.ClientSize.Width, h = planKoerper.ClientSize.Height;
            if (w <= 0 || h <= 0) return;
            bool seitlich = w >= Theme.S(980);
            EinstellungenAnordnen(seitlich);
            if (seitlich)
            {
                int b = Theme.S(340), abstand = Theme.S(20);
                einstellungen.Bounds = new Rectangle(0, 0, b, h);
                planRechts.Bounds = new Rectangle(b + abstand, 0, w - b - abstand, h);
            }
            else
            {
                int noetig = einstellungenBild.InhaltHoehe(w - einstellungen.Padding.Horizontal) + einstellungen.Padding.Vertical;
                // ganz zeigen (es gibt keine Bildlaufleiste), solange der Vorschau genug Platz bleibt
                int hoehe = Math.Min(noetig, Math.Max(Theme.S(150), h - Theme.S(240)));
                einstellungen.Bounds = new Rectangle(0, 0, w, hoehe);
                planRechts.Bounds = new Rectangle(0, hoehe, w, h - hoehe);
            }
        }

        void EinstellungenAnordnen(bool seitlich)
        {
            if (einstellungenSeitlich == seitlich) return;
            einstellungenSeitlich = seitlich;
            einstellungen.SuspendLayout();
            // ohne Bildlaufleiste (geblättert wird mit dem Mausrad): links und rechts gleicher Rand
            einstellungen.Padding = seitlich
                ? new Padding(Theme.S(18), Theme.S(8), Theme.S(18), Theme.S(10))
                : new Padding(Theme.S(18), Theme.S(14), Theme.S(18), Theme.S(10));
            int voll = Theme.S(340) - Theme.S(18 + 18);
            int luecke = Theme.S(12);
            // halbe Felder stehen paarweise nebeneinander, das linke hat rechts die Lücke
            var linkeHalbe = planFelder.Where(x => x.Halb).Where((x, i) => i % 2 == 0).ToList();
            foreach (var f in planFelder)
            {
                if (seitlich)
                {
                    f.Feld.Width = f.Halb ? (voll - luecke) / 2 : voll;
                    f.Feld.Margin = new Padding(0, 0, linkeHalbe.Contains(f) ? luecke : 0, Theme.S(4));
                    f.Eingabe.Width = f.Feld.Width;
                }
                else
                {
                    f.Feld.Width = f.Breite;
                    f.Feld.Margin = new Padding(0, 0, Theme.S(18), Theme.S(6));
                    f.Eingabe.Width = f.EingabeBreite;
                }
            }
            foreach (var a in planAbschnitte)
            {
                a.Visible = seitlich;
                a.Width = voll;
            }
            einstellungen.ResumeLayout(true);
        }

        // ---------------------------------------------------------------- Tabellen

        static SpaltenInfo StundenInfo()
        {
            return new SpaltenInfo(SpaltenArt.Auswahl, () => StundenListe) { Fest = true };
        }

        static List<string> Namen(DataGridView g)
        {
            return Zeilen(g).Select(r => Wert(r, "Name")).Distinct().ToList();
        }

        static string Waehlen(string s)
        {
            return s == "" ? "Wählen…" : s;
        }

        static string Keine(string s)
        {
            return s == "" ? "Keine" : s;
        }

        // Knöpfe am Ende jeder Zeile: Verschieben (wo die Reihenfolge zählt) und Entfernen.
        // Tastatur: Entf entfernt die markierten Zeilen, Alt+Pfeil verschiebt die aktuelle Zeile.
        void Zeilenknoepfe(Tabelle t, bool verschiebbar)
        {
            var g = t.Grid;
            var knoepfe = new List<ZeilenKnopf>();
            if (verschiebbar)
            {
                knoepfe.Add(new ZeilenKnopf("chevron-up", i => Verschieben(g, i, -1), i => i > 0));
                knoepfe.Add(new ZeilenKnopf("chevron-down", i => Verschieben(g, i, 1), i => i < g.RowCount - 1));
            }
            knoepfe.Add(new ZeilenKnopf("trash-2", i => Entfernen(g, i), i => !Pflicht(g, i)) { Gefahr = true });
            t.Aktionen(knoepfe.ToArray());
            g.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete)
                {
                    Entfernen(g);
                    e.Handled = true;
                }
                else if (verschiebbar && e.Alt && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) && g.CurrentCell != null)
                {
                    Verschieben(g, g.CurrentCell.RowIndex, e.KeyCode == Keys.Up ? -1 : 1);
                    e.Handled = true;
                }
            };
        }

        // Umbenennen von Funktion/Grad/Gruppe auch bei den Personen nachführen
        static void Ersetzen(DataGridView g, string spalte, string alt, string neu)
        {
            foreach (DataGridViewRow r in g.Rows)
                if (Wert(r, spalte) == alt) r.Cells[spalte].Value = neu;
        }

        void NeuePerson()
        {
            // Werte der letzten Person übernehmen, das spart beim Erfassen vieler Personen Klicks
            var letzte = gridPersonen.Rows.Cast<DataGridViewRow>().LastOrDefault();
            string grad = letzte != null ? Wert(letzte, "Grad") : Namen(gridGrade).FirstOrDefault() ?? "";
            string funktion = letzte != null ? Wert(letzte, "Funktion") : Namen(gridFunktionen).FirstOrDefault() ?? "";
            string gruppe = letzte != null ? Wert(letzte, "Gruppe") : "";
            Hinzufuegen(gridPersonen, grad, "", funktion, gruppe, false);
        }

        int Hinzufuegen(DataGridView g, params object[] werte)
        {
            EditorenSchliessen();
            var t = TabelleVon(g);
            t.Benutzt = true;
            int i = g.Rows.Add(werte);
            int name = g.Columns.Contains("Name") ? g.Columns["Name"].Index : 0;
            g.ClearSelection();
            g.CurrentCell = g.Rows[i].Cells[name];
            g.Rows[i].Selected = true;
            // erst nach dem Klick auf den Knopf: sonst nimmt der Knopf dem neuen Eingabefeld gleich den Fokus
            BeginInvoke(new Action(() => t.Oeffnen(name, i)));
            return i;
        }

        void Entfernen(DataGridView g)
        {
            EditorenSchliessen();
            var zeilen = g.SelectedCells.Cast<DataGridViewCell>().Select(c => c.RowIndex).Distinct().OrderByDescending(i => i).ToList();
            if (zeilen.Count == 0 && g.CurrentCell != null) zeilen.Add(g.CurrentCell.RowIndex);
            zeilen = zeilen.Where(i => !Pflicht(g, i)).ToList();   // Pflichtposten bleiben
            if (zeilen.Count == 0 || !LoeschenBestaetigt(g, zeilen)) return;
            foreach (int i in zeilen) g.Rows.RemoveAt(i);
        }

        void Entfernen(DataGridView g, int zeile)
        {
            EditorenSchliessen();
            if (zeile < 0 || zeile >= g.Rows.Count || Pflicht(g, zeile) || !LoeschenBestaetigt(g, new List<int> { zeile })) return;
            g.Rows.RemoveAt(zeile);
        }

        // Wacht Kdt, Presto und Subito gibt es immer
        bool Pflicht(DataGridView g, int zeile)
        {
            return g == gridPosten && zeile >= 0 && zeile < g.Rows.Count && Konfiguration.IstPflichtposten(Wert(g.Rows[zeile], "Name"));
        }

        // Mit gedrückter Ctrl-Taste wird ohne Nachfrage gelöscht
        bool LoeschenBestaetigt(DataGridView g, List<int> zeilen)
        {
            if ((ModifierKeys & Keys.Control) != 0) return true;
            string name = zeilen.Count == 1 ? Wert(g.Rows[zeilen[0]], "Name") : "";
            string titel = zeilen.Count > 1 ? zeilen.Count + " Zeilen löschen?" : name != "" ? "«" + name + "» löschen?" : "Diese Zeile löschen?";
            return GefahrDialog.Fragen(this, titel, "Tipp: Mit gedrückter Ctrl-Taste wird ohne Nachfrage gelöscht.", "Löschen", "trash-2");
        }

        // Zwei Zeilen tauschen, mit Werten und Tags (Farbe, Funktionen)
        void Verschieben(DataGridView grid, int i, int richtung)
        {
            int j = i + richtung;
            if (i < 0 || i >= grid.Rows.Count || j < 0 || j >= grid.Rows.Count) return;
            int spalte = grid.CurrentCell != null ? grid.CurrentCell.ColumnIndex : 0;
            EditorenSchliessen();
            TabelleVon(grid).Benutzt = true;
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                DataGridViewCell a = grid.Rows[i].Cells[c], b = grid.Rows[j].Cells[c];
                object wert = a.Value, tag = a.Tag;
                a.Value = b.Value;
                a.Tag = b.Tag;
                b.Value = wert;
                b.Tag = tag;
            }
            grid.ClearSelection();
            grid.CurrentCell = grid.Rows[j].Cells[spalte];
            grid.Rows[j].Selected = true;
            grid.Invalidate();
        }

        // ---------------------------------------------------------------- Daten <-> Oberfläche

        void Anzeigen(Konfiguration k)
        {
            EditorenSchliessen(false);
            gridFunktionen.Rows.Clear();
            foreach (var f in k.Funktionen)
                gridFunktionen.Rows.Add(f.Name, f.ImPlan);
            gridGrade.Rows.Clear();
            foreach (var gr in k.Grade)
                gridGrade.Rows.Add(gr);

            gridGruppen.Rows.Clear();
            foreach (var g in k.Gruppen)
                gridGruppen.Rows.Add(g.Name, Zeit.Text(g.RuheVon), Zeit.Text(g.RuheBis));

            gridPersonen.Rows.Clear();
            foreach (var p in k.Personen)
                gridPersonen.Rows.Add(p.Grad, p.Name, p.Funktion, p.Gruppe, p.Fahrer);

            gridPosten.Rows.Clear();
            foreach (var p in k.Posten)
            {
                int i = gridPosten.Rows.Add(p.Name, "", p.Anzahl.ToString(), "", Zeit.Text(p.OffenVon), Zeit.Text(p.OffenBis),
                    p.Typ == PostenTyp.Auffang ? "Auffang" : p.Typ == PostenTyp.Fahrer ? "Fahrer" : "Normal", p.Draussen, p.EssenMoeglich);
                gridPosten.Rows[i].Cells["Farbe"].Tag = Color.FromArgb(p.Farbe);
                gridPosten.Rows[i].Cells["Funktionen"].Tag = p.Funktionen.ToList();
            }

            gridEssen.Rows.Clear();
            foreach (var e in k.Essenszeiten)
                gridEssen.Rows.Add(e.Name, Zeit.Text(e.Von), Zeit.Text(e.Bis));

            txtName.Text = k.Name;
            txtEinheit.Text = k.Einheit ?? "";
            sprache.Index = Math.Max(0, Array.FindIndex(Druck.Sprachen, s => s.Kuerzel == k.Sprache));
            dtStart.Wert = k.Start > new DateTime(2000, 1, 1) ? k.Start : DateTime.Today;
            dtEnde.Wert = k.Ende > new DateTime(2000, 1, 1) ? k.Ende : DateTime.Today.AddDays(1);
            abloesung.Index = Math.Min(AbloesungListe.Length, Math.Max(1, k.AbloesungStunden)) - 1;
            stuendlichVon.Index = Math.Max(0, Math.Min(23, k.StuendlichVon));
            stuendlichBis.Index = Math.Max(0, Math.Min(23, k.StuendlichBis));
            teams.Index = k.Zweierteams ? 1 : 0;
            int d = Array.IndexOf(DraussenStunden, k.DraussenMaxStunden);
            if (d < 0) d = k.DraussenMaxStunden <= 0 ? 0 : DraussenStunden.Length - 1;
            draussen.Index = d;

            plan = null;
            seite = 0;
            statistik.Grid.Rows.Clear();
            statistik.Grid.Columns.Clear();
            foreach (var t in tabellen) t.Zuruecksetzen();   // keine automatisch ausgewählte erste Zeile
            StatusAktualisieren();
            Titel();
        }

        Konfiguration Einlesen()
        {
            EditorenSchliessen();

            var k = new Konfiguration
            {
                Name = txtName.Text.Trim(),
                Einheit = txtEinheit.Text.Trim(),
                Sprache = Druck.Sprachen[sprache.Index].Kuerzel,
                Start = dtStart.Wert,
                Ende = dtEnde.Wert,
                AbloesungStunden = abloesung.Index + 1,
                StuendlichVon = stuendlichVon.Index,
                StuendlichBis = stuendlichBis.Index,
                Zweierteams = teams.Index == 1,
                DraussenMaxStunden = DraussenStunden[draussen.Index],
            };
            foreach (var r in Zeilen(gridFunktionen))
                k.Funktionen.Add(new Funktion { Name = Wert(r, "Name"), ImPlan = Schalter(r, "ImPlan") });
            k.Grade = Namen(gridGrade);
            foreach (var r in Zeilen(gridGruppen))
                k.Gruppen.Add(new Gruppe { Name = Wert(r, "Name"), RuheVon = Stunde(r, "Von"), RuheBis = Stunde(r, "Bis") });
            foreach (var r in Zeilen(gridPersonen))
                k.Personen.Add(new Person
                {
                    Grad = Wert(r, "Grad"),
                    Name = Wert(r, "Name"),
                    Funktion = Wert(r, "Funktion"),
                    Gruppe = Wert(r, "Gruppe"),
                    Fahrer = Schalter(r, "Fahrer"),
                });
            foreach (var r in Zeilen(gridPosten))
            {
                int anzahl;
                int.TryParse(Wert(r, "Anzahl"), out anzahl);
                PostenTyp typ;
                if (!Enum.TryParse(Wert(r, "Typ"), out typ)) typ = PostenTyp.Normal;
                object farbe = r.Cells["Farbe"].Tag;
                k.Posten.Add(new Posten
                {
                    Name = Wert(r, "Name"),
                    Farbe = (farbe is Color ? (Color)farbe : Color.Gray).ToArgb(),
                    Anzahl = Math.Max(0, anzahl),
                    OffenVon = Stunde(r, "Von"),
                    OffenBis = Stunde(r, "Bis"),
                    Typ = typ,
                    EssenMoeglich = Schalter(r, "Essen"),
                    Draussen = Schalter(r, "Draussen"),
                    Funktionen = (r.Cells["Funktionen"].Tag as List<string> ?? new List<string>()).ToList(),
                });
            }
            foreach (var r in Zeilen(gridEssen))
                k.Essenszeiten.Add(new Essenszeit { Name = Wert(r, "Name"), Von = Stunde(r, "Von"), Bis = Stunde(r, "Bis") });
            return k;
        }

        // Zeilen mit Namen (leere Zeilen zählen nicht)
        static IEnumerable<DataGridViewRow> Zeilen(DataGridView g)
        {
            return g.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow && Wert(r, "Name") != "");
        }

        static bool Schalter(DataGridViewRow r, string spalte)
        {
            object v = r.Cells[spalte].Value;
            return v is bool && (bool)v;
        }

        static string Wert(DataGridViewRow r, string spalte)
        {
            object v = r.Cells[spalte].Value;
            return v == null ? "" : v.ToString().Trim();
        }

        static int Stunde(DataGridViewRow r, string spalte)
        {
            string s = Wert(r, spalte);
            int h;
            return s.Length >= 2 && int.TryParse(s.Substring(0, 2), out h) && h >= 0 && h < 24 ? h : 0;
        }

        // ---------------------------------------------------------------- Plan

        void Erstellen()
        {
            Plan neu;
            Cursor = Cursors.WaitCursor;
            try
            {
                neu = Planer.Erstelle(Einlesen(), Environment.TickCount);
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                Meldung.Warnung(this, "Plan kann nicht erstellt werden", ex.Message);
                return;
            }
            Cursor = Cursors.Default;
            plan = neu;
            seite = 0;
            StatistikAnzeigen();
            StatusAktualisieren();
        }

        void DruckAngabenUebernehmen()
        {
            if (plan == null) return;
            plan.Cfg.Name = txtName.Text.Trim();
            plan.Cfg.Einheit = txtEinheit.Text.Trim();
            plan.Cfg.Sprache = Druck.Sprachen[sprache.Index].Kuerzel;
            druckStand++;   // Blatt bald neu zeichnen (nicht bei jedem Tastendruck sofort)
            vorschau.Invalidate();
        }

        void Blaettern(int richtung)
        {
            if (plan == null) return;
            seite = Math.Max(0, Math.Min(plan.Seiten - 1, seite + richtung));
            StatusAktualisieren();
        }

        void StatusAktualisieren()
        {
            if (plan == null)
                status.Setzen("", "circle-check", Theme.Erfolg);
            else if (plan.Warnungen.Count > 0)
                status.Setzen(plan.Warnungen.Count == 1 ? "1 Hinweis" : plan.Warnungen.Count + " Hinweise", "triangle-alert", Theme.Warnung, true);
            else
                status.Setzen("Alle Posten besetzt", "circle-check", Theme.Erfolg);
            lblSeite.Text = plan == null ? "" : "Seite " + (seite + 1) + " / " + plan.Seiten;
            btnZurueck.Enabled = seite > 0;
            btnVor.Enabled = plan != null && seite < plan.Seiten - 1;
            if (plan == null)
            {
                zoom = 1f;
                verschiebung = PointF.Empty;
                VorschauBildFreigeben();
            }
            ZoomAnzeigen();
            vorschau.Invalidate();
        }

        // ---------------------------------------------------------------- Vorschau

        // Das Blatt wird als Bild zwischengespeichert: ganz oder, bei starkem Zoom, nur der sichtbare Ausschnitt.
        // Bei Zoom, Verschieben, Grössenänderung und Tippen im Namen zeigt die Vorschau sofort das vorhandene
        // Bild (verschoben oder gestreckt) und zeichnet das scharfe erst, wenn kurz Ruhe ist. So bleibt sie flüssig.
        const long GanzesBlattMax = 12000000;   // Pixel
        readonly Timer vorschauTimer = new Timer { Interval = 120 };
        Bitmap vorschauBild;
        Rectangle vorschauBildBlatt;   // Lage des Blatts beim Zeichnen des Bilds
        Point vorschauBildOrt;         // wo das Bild dabei lag (Blattecke oder Ausschnitt)
        Plan vorschauBildPlan;
        int vorschauBildSeite, vorschauBildStand, druckStand;

        void VorschauBildFreigeben()
        {
            vorschauTimer.Stop();
            if (vorschauBild != null) vorschauBild.Dispose();
            vorschauBild = null;
            vorschauBildPlan = null;
        }

        void VorschauRendern()
        {
            vorschauTimer.Stop();
            var blatt = Rectangle.Round(Blatt());
            Rectangle bereich = (long)blatt.Width * blatt.Height <= GanzesBlattMax
                ? blatt
                : Rectangle.Intersect(new Rectangle(Point.Empty, vorschau.ClientSize), blatt);
            if (bereich.Width <= 0 || bereich.Height <= 0) return;
            var bild = new Bitmap(bereich.Width, bereich.Height, PixelFormat.Format32bppPArgb);
            using (var gb = Graphics.FromImage(bild))
            {
                gb.Clear(Color.White);
                gb.TranslateTransform(-bereich.X, -bereich.Y);
                float rand = blatt.Width * 0.035f;
                Zeichner.Seite(gb, RectangleF.Inflate(blatt, -rand, -rand), plan, seite);
            }
            if (vorschauBild != null) vorschauBild.Dispose();
            vorschauBild = bild;
            vorschauBildBlatt = blatt;
            vorschauBildOrt = bereich.Location;
            vorschauBildPlan = plan;
            vorschauBildSeite = seite;
            vorschauBildStand = druckStand;
        }

        void VorschauZeichnen(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var platz = new RectangleF(0, 0, vorschau.Width - 1, vorschau.Height - 1);
            Formen.Fuellen(g, platz, Theme.RadiusKarte, Theme.Vorschau);

            if (plan == null)
            {
                Formen.Leer(g, vorschau.ClientRectangle, "calendar-days", "Noch kein Plan erstellt", "Zeitraum wählen und oben rechts auf «Plan erstellen» klicken.");
                return;
            }

            var blatt = Blatt();
            if (blatt.Width < 50 || blatt.Height < 50) return;
            using (var p = Formen.Rund(platz, Theme.RadiusKarte))
                g.SetClip(p);
            for (int i = 1; i <= 6; i++)
                Formen.Fuellen(g, new RectangleF(blatt.X - i + 1, blatt.Y - i + 3, blatt.Width + 2 * i - 2, blatt.Height + 2 * i - 2), Theme.S(4) + i, Color.FromArgb(Theme.Dunkel ? 22 : 8, 0, 0, 0));
            Formen.Fuellen(g, blatt, Theme.S(4), Color.White);

            // neuer Plan oder andere Seite: sofort scharf zeichnen
            if (vorschauBild == null || vorschauBildPlan != plan || vorschauBildSeite != seite) VorschauRendern();
            if (vorschauBild == null) return;

            var jetzt = Rectangle.Round(blatt);
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            if (jetzt.Size == vorschauBildBlatt.Size)
            {
                // nur verschoben: das Bild Pixel für Pixel versetzt
                var ziel = new Rectangle(vorschauBildOrt.X + jetzt.X - vorschauBildBlatt.X, vorschauBildOrt.Y + jetzt.Y - vorschauBildBlatt.Y,
                    vorschauBild.Width, vorschauBild.Height);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(vorschauBild, ziel);
                var noetig = Rectangle.Intersect(new Rectangle(Point.Empty, vorschau.ClientSize), jetzt);
                if (!Rectangle.Inflate(ziel, 1, 1).Contains(noetig) || vorschauBildStand != druckStand) Nachzeichnen();
            }
            else
            {
                // Grösse geändert: das vorhandene Bild gestreckt, das scharfe folgt gleich
                float f = blatt.Width / vorschauBildBlatt.Width;
                var ziel = new RectangleF(blatt.X + (vorschauBildOrt.X - vorschauBildBlatt.X) * f, blatt.Y + (vorschauBildOrt.Y - vorschauBildBlatt.Y) * f,
                    vorschauBild.Width * f, vorschauBild.Height * f);
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.DrawImage(vorschauBild, ziel);
                Nachzeichnen();
            }
        }

        void Nachzeichnen()
        {
            vorschauTimer.Stop();
            vorschauTimer.Start();
        }

        // Position und Grösse des Blatts in der Vorschau (zoom 1 = ganzes Blatt sichtbar)
        RectangleF Blatt()
        {
            const float seitenverhaeltnis = 297f / 210f;
            float breite = vorschau.Width, hoehe = vorschau.Height, rand = Theme.S(24);
            float w = Math.Max(1, breite - 2 * rand), h = Math.Max(1, hoehe - 2 * rand);
            if (w / h > seitenverhaeltnis) w = h * seitenverhaeltnis; else h = w / seitenverhaeltnis;
            w *= zoom;
            h *= zoom;
            float maxX = Math.Max(0, (w - breite) / 2 + rand), maxY = Math.Max(0, (h - hoehe) / 2 + rand);
            verschiebung = new PointF(Math.Max(-maxX, Math.Min(maxX, verschiebung.X)), Math.Max(-maxY, Math.Min(maxY, verschiebung.Y)));
            return new RectangleF((breite - w) / 2 + verschiebung.X, (hoehe - h) / 2 + verschiebung.Y, w, h);
        }

        Point VorschauMitte()
        {
            return new Point(vorschau.Width / 2, vorschau.Height / 2);
        }

        // Zoomt so, dass der Punkt unter der Maus an der gleichen Stelle bleibt
        void Zoomen(float neu, Point um)
        {
            if (plan == null) return;
            var vorher = Blatt();
            float ux = (um.X - vorher.X) / vorher.Width, uy = (um.Y - vorher.Y) / vorher.Height;
            zoom = Math.Max(1f, Math.Min(8f, neu));
            var nachher = Blatt();
            verschiebung = new PointF(verschiebung.X + um.X - ux * nachher.Width - nachher.X,
                                      verschiebung.Y + um.Y - uy * nachher.Height - nachher.Y);
            ZoomAnzeigen();
            vorschau.Invalidate();
        }

        void Einpassen()
        {
            zoom = 1f;
            verschiebung = PointF.Empty;
            ZoomAnzeigen();
            vorschau.Invalidate();
        }

        void ZoomAnzeigen()
        {
            bool sichtbar = plan != null;
            bool blaettern = sichtbar && plan.Seiten > 1;
            foreach (var k in ausgabe) k.Enabled = sichtbar;
            // Platz der Leiste: erst fällt die Zoom-Beschriftung weg, dann zeigen die Ausgabe-Knöpfe nur ihr Symbol
            Func<Control, int> breite = c => c.Width + c.Margin.Horizontal;
            int fest = Theme.S(14 + 10 + 16) + status.Width
                + (sichtbar ? breite(btnEinpassen) + breite(trennerAnsicht) + breite(trennerSeite) + breite(lblSeite) : 0)
                + (blaettern ? breite(btnZurueck) + breite(btnVor) : 0);
            int ausgabeVoll = ausgabe.Sum(k => k.VolleBreite + k.Margin.Horizontal);
            int platz = werkzeugleiste.Width;
            bool mitZoom = sichtbar && platz >= fest + breite(lblZoom) + ausgabeVoll;
            bool kompakt = platz < fest + (mitZoom ? breite(lblZoom) : 0) + ausgabeVoll;
            foreach (var k in ausgabe) k.Kompakt = kompakt;
            lblZoom.Text = zoom <= 1.001f ? "Ganze Seite" : Math.Round(zoom * 100) + " %";
            btnEinpassen.Enabled = zoom > 1.001f;

            // Werkzeugleiste selbst anordnen (ein FlowLayoutPanel bringt die Reihenfolge durcheinander,
            // wenn Steuerelemente erst später sichtbar werden)
            var zeigen = new Dictionary<Control, bool>
            {
                { lblZoom, mitZoom }, { btnEinpassen, sichtbar }, { trennerAnsicht, sichtbar },
                { btnZurueck, blaettern }, { lblSeite, sichtbar }, { btnVor, blaettern }, { trennerSeite, sichtbar },
            };
            int x = 0;
            foreach (var c in werkzeugReihe)
            {
                bool an;
                if (!zeigen.TryGetValue(c, out an)) an = true;
                c.Visible = an;
                if (!an) continue;
                x += c.Margin.Left;
                c.Location = new Point(x, (werkzeuge.Height - c.Height) / 2);
                x += c.Width + c.Margin.Right;
            }
            werkzeuge.Width = x;
            vorschau.Cursor = sichtbar && zoom > 1 ? (ziehen ? Cursors.SizeAll : Cursors.Hand) : Cursors.Default;
        }

        void StatistikAnzeigen()
        {
            var g = statistik.Grid;
            g.Rows.Clear();
            g.Columns.Clear();
            Func<SpaltenInfo> text = () => new SpaltenInfo(SpaltenArt.Anzeige);
            Func<Color?, SpaltenInfo> zahl = farbe => new SpaltenInfo(SpaltenArt.Anzeige) { Fest = true, Farbe = farbe };
            statistik.Spalte("Grad", "Grad", 50, text());
            statistik.Spalte("Name", "Name", 150, text(), 110);
            statistik.Spalte("Gruppe", "Gruppe", 80, text(), 80);
            for (int k = 0; k < plan.Cfg.Posten.Count; k++)
                statistik.Spalte("P" + k, plan.Cfg.Posten[k].Name, 60, zahl(Color.FromArgb(plan.Cfg.Posten[k].Farbe)));
            statistik.Spalte("Essen", "Essen", 60, zahl(null));
            statistik.Spalte("Total", "Dienst-Std.", 60, zahl(null));

            for (int r = 0; r < plan.Zeilen.Count; r++)
            {
                var p = plan.Zeilen[r];
                var werte = new List<object> { p.Grad, p.Name, p.Gruppe };
                int total = 0, essen = 0;
                for (int k = 0; k < plan.Cfg.Posten.Count; k++)
                {
                    int c = 0;
                    for (int t = 0; t < plan.Stunden; t++)
                        if (plan.Z[r, t] == k) c++;
                    total += c;
                    werte.Add(c == 0 ? "–" : c.ToString());
                }
                for (int t = 0; t < plan.Stunden; t++)
                    if (plan.Essen[r, t]) essen++;
                werte.Add(essen == 0 ? "–" : essen.ToString());
                werte.Add(total);
                g.Rows.Add(werte.ToArray());
            }
            statistik.Zuruecksetzen();
        }

        // ---------------------------------------------------------------- Ausgabe

        PrintDocument Dokument(string drucker)
        {
            var druckPlan = plan;   // derselbe Plan bis zur letzten Seite
            var pd = new PrintDocument { DocumentName = "Wachtplan (ShiftDesk)" };
            if (drucker != null) pd.PrinterSettings.PrinterName = drucker;
            pd.PrintController = new StandardPrintController();
            pd.DefaultPageSettings.Landscape = true;
            pd.DefaultPageSettings.Margins = new Margins(35, 35, 35, 35);
            foreach (PaperSize ps in pd.PrinterSettings.PaperSizes)
                if (ps.Kind == PaperKind.A4) { pd.DefaultPageSettings.PaperSize = ps; break; }
            int s = 0;
            pd.BeginPrint += (o, e) => s = 0;
            pd.PrintPage += (o, e) =>
            {
                Zeichner.Seite(e.Graphics, e.MarginBounds, druckPlan, s);
                s++;
                e.HasMorePages = s < druckPlan.Seiten;
            };
            return pd;
        }

        bool PlanVorhanden()
        {
            if (plan != null) return true;
            Meldung.Info(this, "Noch kein Plan", "Bitte zuerst auf «Plan erstellen» klicken.");
            return false;
        }

        // Speichern-Dialog für eine Ausgabe des Plans; null = abgebrochen
        string AusgabeDatei(string beschreibung, string endung)
        {
            using (var dlg = new SaveFileDialog
            {
                Filter = beschreibung + " (*" + endung + ")|*" + endung,
                FileName = DateiName(plan.Cfg.Name) + " " + plan.Start.ToString("yyyy-MM-dd") + endung,
            })
                return dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
        }

        void Drucken()
        {
            if (!PlanVorhanden()) return;
            using (var pd = Dokument(null))
            using (var dlg = new PrintDialog { Document = pd, UseEXDialog = true, AllowSomePages = false })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try { pd.Print(); }
                catch (Exception ex) { Meldung.Fehler(this, "Drucken fehlgeschlagen", ex.Message); }
            }
        }

        void AlsPdf()
        {
            if (!PlanVorhanden()) return;
            string pfad = AusgabeDatei("PDF-Datei", ".pdf");
            if (pfad == null) return;
            using (var pd = Dokument("Microsoft Print to PDF"))
            {
                if (!pd.PrinterSettings.IsValid)
                {
                    Meldung.Warnung(this, "PDF-Drucker fehlt",
                        "Der Drucker «Microsoft Print to PDF» ist auf diesem PC nicht installiert. Bitte «Drucken» verwenden.");
                    return;
                }
                pd.PrinterSettings.PrintToFile = true;
                pd.PrinterSettings.PrintFileName = pfad;
                try
                {
                    if (File.Exists(pfad)) File.Delete(pfad);
                    pd.Print();
                }
                catch (Exception ex)
                {
                    Meldung.Fehler(this, "PDF konnte nicht gespeichert werden", ex.Message);
                    return;
                }
            }
            // Die Warteschlange schreibt die Datei erst nach dem Drucken fertig: warten, bis sie da ist und
            // niemand mehr daran schreibt, ohne das Fenster zu blockieren; erst dann «gespeichert» melden
            var start = DateTime.Now;
            var warten = new Timer { Interval = 150 };
            Cursor = Cursors.AppStarting;
            warten.Tick += (s, e) =>
            {
                bool fertig = DateiFertig(pfad);
                if (!fertig && (DateTime.Now - start).TotalSeconds < 15) return;
                warten.Stop();
                warten.Dispose();
                if (IsDisposed) return;
                Cursor = Cursors.Default;
                if (fertig) Gespeichert("PDF gespeichert", pfad);
                else Meldung.Warnung(this, "PDF noch nicht fertig", "Die Datei ist noch nicht geschrieben. Sie erscheint gleich hier:\n\n" + pfad);
            };
            warten.Start();
        }

        static bool DateiFertig(string pfad)
        {
            try
            {
                using (var fs = File.Open(pfad, FileMode.Open, FileAccess.Read, FileShare.None))
                    return fs.Length > 0;
            }
            catch { return false; }
        }

        void AlsExcel()
        {
            if (!PlanVorhanden()) return;
            string pfad = AusgabeDatei("Excel-Datei", ".xlsx");
            if (pfad == null) return;
            try
            {
                ExcelExport.Speichern(plan, pfad);
            }
            catch (Exception ex)
            {
                Meldung.Fehler(this, "Excel-Datei konnte nicht gespeichert werden",
                    ex.Message + "\n\nIst sie vielleicht noch in Excel geöffnet?");
                return;
            }
            Gespeichert("Excel-Datei gespeichert", pfad);
        }

        // Nach dem Speichern einer Ausgabe anbieten, sie gleich zu öffnen
        void Gespeichert(string titel, string pfad)
        {
            if (!GefahrDialog.Vorschlagen(this, titel, pfad + "\n\nJetzt öffnen?", "Öffnen", "file-check")) return;
            try { System.Diagnostics.Process.Start(pfad); }
            catch (Exception ex) { Meldung.Fehler(this, "Datei konnte nicht geöffnet werden", ex.Message); }
        }

        // ---------------------------------------------------------------- Dateien

        void Oeffnen()
        {
            using (var dlg = new OpenFileDialog { Filter = "ShiftDesk-Datei (*.shiftdesk)|*.shiftdesk|Alle Dateien|*.*" })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    DateiOeffnen(dlg.FileName);
            }
        }

        // Eine .shiftdesk-Datei laden (Menü «Öffnen…» oder Doppelklick im Explorer)
        void DateiOeffnen(string pfad)
        {
            try
            {
                var k = Konfiguration.Laden(pfad);
                dateiPfad = pfad;
                Anzeigen(k);
            }
            catch (Exception ex)
            {
                Meldung.Fehler(this, "Datei konnte nicht geöffnet werden", ex.Message);
            }
        }

        void Speichern(bool neuerName)
        {
            string pfad = dateiPfad;
            if (pfad == null || neuerName)
            {
                using (var dlg = new SaveFileDialog { Filter = "ShiftDesk-Datei (*.shiftdesk)|*.shiftdesk", FileName = "Wachtplan.shiftdesk" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    pfad = dlg.FileName;
                }
            }
            try
            {
                Einlesen().Speichern(pfad);
                dateiPfad = pfad;   // erst nach erfolgreichem Speichern
                Titel();
            }
            catch (Exception ex)
            {
                Meldung.Fehler(this, "Speichern fehlgeschlagen", ex.Message);
            }
        }

        // Name als Dateiname (ohne Zeichen, die Windows nicht erlaubt)
        static string DateiName(string name)
        {
            string s = new string((name ?? "").Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim();
            return s == "" ? "Wachtplan" : s;
        }

        void Titel()
        {
            Text = dateiPfad == null ? "ShiftDesk" : Path.GetFileNameWithoutExtension(dateiPfad) + " – ShiftDesk";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                vorschauTimer.Dispose();
                VorschauBildFreigeben();
            }
            base.Dispose(disposing);
        }
    }
}
