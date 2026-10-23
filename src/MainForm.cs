using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        static readonly string[] TypListe = { "Normal", "Auffang" };

        Konfiguration cfg;
        string dateiPfad;
        Plan plan;
        int seite;

        // alles, was beim Wechsel hell/dunkel neu eingefärbt werden muss
        readonly List<Action> themen = new List<Action>();
        readonly List<DataGridView> tabellen = new List<DataGridView>();

        Panel seitenleiste, inhalt;
        readonly Uebergang uebergang = new Uebergang();
        readonly List<KeyValuePair<NavEintrag, Control>> navigation = new List<KeyValuePair<NavEintrag, Control>>();
        DataGridView gridFunktionen, gridGrade, gridPersonen, gridGruppen, gridPosten, gridEssen, gridStatistik;
        readonly List<TabellenEditor> editoren = new List<TabellenEditor>();
        readonly HashSet<DataGridView> benutzteTabellen = new HashSet<DataGridView>();   // Tabellen, in denen der Benutzer schon ausgewählt hat
        Panel seitePersonen, seitePlan;
        Eingabefeld txtName;
        DatumFeld dtStart, dtEnde;
        AuswahlFeld abloesung;
        static readonly string[] AbloesungListe = { "Jede Stunde", "Alle 2 Stunden", "Alle 3 Stunden", "Alle 4 Stunden" };
        ZoomPanel vorschau;
        Label lblZoom;
        ModernButton btnEinpassen;
        float zoom = 1f;
        PointF verschiebung, verschiebungStart;
        bool ziehen;
        Point ziehStart;
        Label lblSeite;
        StatusChip status;
        ModernButton btnZurueck, btnVor;
        Karte warnKarte;
        Panel warnAbstand;
        HinweisListe lstWarnungen;

        public MainForm(string datei = null)
        {
            Text = "ShiftDesk";
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            var bildschirm = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(Theme.S(1440), bildschirm.Width), Math.Min(Theme.S(920), bildschirm.Height));
            MinimumSize = new Size(Theme.S(1100), Theme.S(680));
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
            catch { }
            Anzeigen(geladen ?? Konfiguration.Beispiel());
            if (datei != null) DateiOeffnen(datei);
            Zeigen(navigation.First(n => n.Value == seitePersonen).Key);

            EventHandler themaGeaendert = (s, e) =>
            {
                if (IsHandleCreated) BeginInvoke(new Action(ThemaAnwenden));
            };
            Theme.Geaendert += themaGeaendert;
            HandleCreated += (s, e) => ThemaAnwenden();
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
            foreach (var g in tabellen) GridFarben(g);
            Theme.Titelleiste(this);
            foreach (var c in tabellen.Cast<Control>().Concat(new Control[] { lstWarnungen }))
                BildlaufRekursiv(c);
            Invalidate(true);
        }

        static void BildlaufRekursiv(Control c)
        {
            Theme.Bildlauf(c);
            foreach (Control k in c.Controls) BildlaufRekursiv(k);
        }

        // ---------------------------------------------------------------- Seitenleiste

        Control Seitenleiste(Tuple<string, string, Panel>[] seiten)
        {
            seitenleiste = new PufferPanel { Dock = DockStyle.Left, Width = Theme.S(248), Padding = new Padding(Theme.S(14), Theme.S(8), Theme.S(15), Theme.S(14)) };
            seitenleiste.Paint += (s, e) =>
            {
                using (var p = new Pen(Theme.Rand))
                    e.Graphics.DrawLine(p, seitenleiste.Width - 1, 0, seitenleiste.Width - 1, seitenleiste.Height);
            };
            Thema(() => seitenleiste.BackColor = Theme.Seitenleiste);

            var oben = new List<Control> { new Marke(), new Abschnitt("Planung") };
            foreach (var s in seiten)
            {
                var eintrag = new NavEintrag(s.Item1, s.Item2);
                eintrag.Click += (o, e) => Zeigen((NavEintrag)o);
                navigation.Add(new KeyValuePair<NavEintrag, Control>(eintrag, s.Item3));
                oben.Add(eintrag);
            }
            var aktionen = new List<Tuple<string, string, Action>>
            {
                Tuple.Create("Neu", "file-plus", (Action)(() =>
                {
                    if (!Frage("Alle Eingaben löschen und neu beginnen?")) return;
                    dateiPfad = null;
                    Anzeigen(new Konfiguration { Start = DateTime.Today, Ende = DateTime.Today.AddDays(1) });
                })),
                Tuple.Create("Beispiel laden", "rotate-ccw", (Action)(() =>
                {
                    if (!Frage("Alle Eingaben durch das Beispiel ersetzen?")) return;
                    dateiPfad = null;
                    Anzeigen(Konfiguration.Beispiel());
                })),
                Tuple.Create("Öffnen…", "folder-open", (Action)Oeffnen),
                Tuple.Create("Speichern", "save", (Action)(() => Speichern(false))),
                Tuple.Create("Speichern unter…", "save-all", (Action)(() => Speichern(true))),
            };
            if (!Installation.LaeuftInstalliert)
                aktionen.Add(Tuple.Create("Auf diesem PC installieren", "download", (Action)null));

            // Zwei Darstellungen der Datei-Befehle: Liste (wenn genug Höhe) oder kompakte Symbolleiste
            var unten = new List<Control> { new Abschnitt("Datei") };
            var untenPanel = new Panel { Dock = DockStyle.Bottom };
            var kompakt = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = Theme.S(46), WrapContents = false, Padding = new Padding(Theme.S(4), Theme.S(8), 0, 0), Visible = false };
            var tipp = new ToolTip();
            Action anpassen = null;
            foreach (var a in aktionen)
            {
                var aktion = a.Item3;
                var eintrag = new NavEintrag(a.Item1, a.Item2) { Height = Theme.S(38) };
                var knopf = new ModernButton("", a.Item2) { Margin = new Padding(0, 0, Theme.S(4), 0) };
                knopf.Width = knopf.Height = Theme.S(32);
                tipp.SetToolTip(knopf, a.Item1);
                if (aktion == null)
                {
                    // «Installieren» verschwindet nach erfolgreicher Installation
                    aktion = () =>
                    {
                        if (!Installation.Installieren(false)) return;
                        unten.Remove(eintrag);
                        untenPanel.Height = unten.Sum(c => c.Height);
                        eintrag.Dispose();
                        knopf.Dispose();
                        anpassen();
                    };
                }
                eintrag.Click += (s, e) => aktion();
                knopf.Click += (s, e) => aktion();
                unten.Add(eintrag);
                kompakt.Controls.Add(knopf);
            }
            untenPanel.Height = unten.Sum(c => c.Height);
            var obenPanel = new Panel { Dock = DockStyle.Fill };
            Stapeln(obenPanel, oben);
            Stapeln(untenPanel, unten);
            anpassen = () =>
            {
                int frei = seitenleiste.ClientSize.Height - seitenleiste.Padding.Vertical;
                bool passt = frei >= oben.Sum(c => c.Height) + untenPanel.Height;
                untenPanel.Visible = passt;
                kompakt.Visible = !passt;
            };
            seitenleiste.Resize += (s, e) => anpassen();
            Thema(() => { obenPanel.BackColor = untenPanel.BackColor = kompakt.BackColor = Theme.Seitenleiste; });
            seitenleiste.Controls.Add(obenPanel);
            seitenleiste.Controls.Add(untenPanel);
            seitenleiste.Controls.Add(kompakt);
            return seitenleiste;
        }

        static void Stapeln(Panel ziel, List<Control> elemente)
        {
            // Dock=Top: das zuletzt hinzugefügte Element liegt zuoberst
            for (int i = elemente.Count - 1; i >= 0; i--)
            {
                elemente[i].Dock = DockStyle.Top;
                ziel.Controls.Add(elemente[i]);
            }
        }

        void Zeigen(NavEintrag eintrag)
        {
            foreach (var ed in editoren) ed.Schliessen(true);
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
            if (ziel == seitePlan) cfg = Einlesen();

            if (animieren) uebergang.Starten(Schnappschuss());
        }

        Bitmap Schnappschuss()
        {
            var bmp = new Bitmap(inhalt.Width, inhalt.Height);
            inhalt.DrawToBitmap(bmp, new Rectangle(0, 0, inhalt.Width, inhalt.Height));
            return bmp;
        }

        // ---------------------------------------------------------------- Seiten

        Panel NeueSeite(string titel, string untertitel, Control inhaltControl, params Control[] knoepfe)
        {
            var seitePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Theme.S(32), Theme.S(24), Theme.S(32), Theme.S(28)) };
            var kopf = new Panel { Dock = DockStyle.Top, Height = Theme.S(84) };
            var lblTitel = new Label { Text = titel, Font = new Font("Segoe UI Semibold", 19f), AutoSize = true, Location = new Point(-Theme.S(3), 0), UseMnemonic = false };
            var lblUnter = new Label { Text = untertitel, Font = new Font("Segoe UI", 9.5f), AutoSize = false, Location = new Point(0, Theme.S(42)), Height = Theme.S(40), UseMnemonic = false };
            var leiste = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(0, Theme.S(6), 0, 0) };
            foreach (var k in knoepfe) leiste.Controls.Add(k);
            kopf.Controls.Add(lblUnter);
            kopf.Controls.Add(lblTitel);
            kopf.Controls.Add(leiste);
            kopf.Resize += (s, e) =>
            {
                // Erklärungstext so hoch wie nötig, damit nichts abgeschnitten wird
                int breite = Math.Max(Theme.S(100), kopf.Width - leiste.Width - Theme.S(24));
                int h = TextRenderer.MeasureText(untertitel, lblUnter.Font, new Size(breite, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                lblUnter.Size = new Size(breite, h + Theme.S(2));
                int hoehe = Theme.S(44) + h + Theme.S(12);
                if (kopf.Height != hoehe) kopf.Height = hoehe;
            };
            Thema(() =>
            {
                seitePanel.BackColor = kopf.BackColor = leiste.BackColor = Theme.Hintergrund;
                lblTitel.BackColor = lblUnter.BackColor = Theme.Hintergrund;
                lblTitel.ForeColor = Theme.Text;
                lblUnter.ForeColor = Theme.Gedaempft;
            });
            inhaltControl.Dock = DockStyle.Fill;
            seitePanel.Controls.Add(inhaltControl);
            seitePanel.Controls.Add(kopf);
            return seitePanel;
        }

        Karte KarteMit(Control c)
        {
            var k = new Karte();
            c.Dock = DockStyle.Fill;
            k.Controls.Add(c);
            Thema(() => k.BackColor = Theme.Karte);
            return k;
        }

        ModernButton Knopf(string text, string icon, Action aktion, bool primaer = false)
        {
            var b = new ModernButton(text, icon, primaer);
            b.Click += (s, e) => aktion();
            return b;
        }

        Panel SeitePersonen()
        {
            gridPersonen = NeuesGrid();
            gridPersonen.Columns.Add(Spalte("Grad", "Grad", 50, new SpaltenInfo(SpaltenArt.Auswahl, () => Namen(gridGrade), Waehlen)));
            gridPersonen.Columns.Add(Spalte("Name", "Name", 130, new SpaltenInfo(SpaltenArt.Text)));
            gridPersonen.Columns.Add(Spalte("Funktion", "Funktion", 85, new SpaltenInfo(SpaltenArt.Auswahl, () => Namen(gridFunktionen), Waehlen)));
            gridPersonen.Columns.Add(Spalte("Gruppe", "Gruppe (Ruhezeit)", 85, new SpaltenInfo(SpaltenArt.Auswahl, () => new[] { "" }.Concat(Namen(gridGruppen)), Keine)));
            Zeilenknoepfe(gridPersonen, true);
            Bearbeitbar(gridPersonen);
            return NeueSeite("Personen",
                "Alle Personen der Wache. Grad und Funktion wählst du aus den Listen der Seite «Funktionen & Grade». Die Gruppe bestimmt die Ruhezeit. Zum Ändern einfach ins Feld klicken.",
                KarteMit(gridPersonen),
                Knopf("Person hinzufügen", "plus", NeuePerson, true));
        }

        Panel SeiteGruppen()
        {
            gridGruppen = NeuesGrid();
            gridGruppen.Columns.Add(Spalte("Name", "Gruppe", 140, new SpaltenInfo(SpaltenArt.Text)));
            gridGruppen.Columns.Add(Spalte("Von", "Ruhe von", 80, StundenInfo()));
            gridGruppen.Columns.Add(Spalte("Bis", "Ruhe bis", 80, StundenInfo()));
            Zeilenknoepfe(gridGruppen, false);
            Bearbeitbar(gridGruppen).Umbenannt += (z, alt, neu) => { if (z.OwningColumn.Name == "Name") Ersetzen(gridPersonen, "Gruppe", alt, neu); };
            return NeueSeite("Gruppen & Ruhezeiten",
                "Jede Gruppe ruht jeden Tag im gleichen Zeitfenster, auch über Mitternacht (z. B. 23:00 bis 07:00). Gleiche Zeit bei «von» und «bis» bedeutet keine Ruhezeit. Auch die Wachtkommandanten brauchen je eine Gruppe.",
                KarteMit(gridGruppen),
                Knopf("Gruppe hinzufügen", "plus", () => Hinzufuegen(gridGruppen, "", Zeit.Text(23), Zeit.Text(7)), true));
        }

        Panel SeitePosten()
        {
            gridPosten = NeuesGrid();
            gridPosten.Columns.Add(Spalte("Name", "Posten", 105, new SpaltenInfo(SpaltenArt.Text)));
            gridPosten.Columns.Add(Spalte("Farbe", "Farbe", 55, new SpaltenInfo(SpaltenArt.Farbe)));
            gridPosten.Columns.Add(Spalte("Anzahl", "Personen", 48, new SpaltenInfo(SpaltenArt.Zahl)));
            gridPosten.Columns.Add(Spalte("Funktionen", "Funktionen", 125, new SpaltenInfo(SpaltenArt.Mehrfach, () => Namen(gridFunktionen), s => s == "" ? "Alle" : s)));
            gridPosten.Columns.Add(Spalte("Von", "Offen von", 58, StundenInfo()));
            gridPosten.Columns.Add(Spalte("Bis", "Offen bis", 58, StundenInfo()));
            gridPosten.Columns.Add(Spalte("Typ", "Typ", 62, new SpaltenInfo(SpaltenArt.Auswahl, () => TypListe)));
            gridPosten.Columns.Add(Spalte("AmStueck", "Am Stück", 42, new SpaltenInfo(SpaltenArt.Schalter)));
            gridPosten.Columns.Add(Spalte("Essen", "Essen", 42, new SpaltenInfo(SpaltenArt.Schalter)));
            Zeilenknoepfe(gridPosten, true);
            Bearbeitbar(gridPosten);
            return NeueSeite("Posten",
                "Reihenfolge = Wichtigkeit, unten bleibt bei zu wenig Leuten zuerst leer. «Funktionen» = wer den Posten besetzen darf. «Auffang» (z. B. Pikett) bekommt alle übrigen Leute. «Am Stück» = gleiche Person bleibt möglichst lange.",
                KarteMit(gridPosten),
                Knopf("Posten hinzufügen", "plus", () =>
                {
                    int i = Hinzufuegen(gridPosten, "", "", "1", "", Zeit.Text(0), Zeit.Text(0), "Normal", false, false);
                    gridPosten.Rows[i].Cells["Farbe"].Tag = Color.FromArgb(148, 163, 184);
                    gridPosten.Rows[i].Cells["Funktionen"].Tag = new List<string>();
                }, true));
        }

        Panel SeiteEssen()
        {
            gridEssen = NeuesGrid();
            gridEssen.Columns.Add(Spalte("Name", "Mahlzeit", 140, new SpaltenInfo(SpaltenArt.Text)));
            gridEssen.Columns.Add(Spalte("Von", "von", 80, StundenInfo()));
            gridEssen.Columns.Add(Spalte("Bis", "bis", 80, StundenInfo()));
            Zeilenknoepfe(gridEssen, false);
            Bearbeitbar(gridEssen);
            return NeueSeite("Essenszeiten",
                "In jedem Zeitfenster bekommt jede Person im Dienst eine Stunde zum Essen, auf einem Posten mit «Essen möglich». Im Plan ist diese Stunde mit dem Besteck markiert.",
                KarteMit(gridEssen),
                Knopf("Mahlzeit hinzufügen", "plus", () => Hinzufuegen(gridEssen, "", Zeit.Text(11), Zeit.Text(13)), true));
        }

        Panel SeitePlan()
        {
            // Einstellungen
            txtName = new Eingabefeld { Width = Theme.S(280) };
            dtStart = new DatumFeld();
            dtEnde = new DatumFeld();
            Thema(() => txtName.ThemaAnwenden());
            var felder = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
            felder.Controls.Add(Feld("Name", txtName));
            felder.Controls.Add(Feld("Start", dtStart));
            felder.Controls.Add(Feld("Ende", dtEnde));
            abloesung = new AuswahlFeld(AbloesungListe) { Width = Theme.S(170) };
            felder.Controls.Add(Feld("Ablösung", abloesung));
            var einstellungen = KarteMit(felder);
            einstellungen.Dock = DockStyle.Top;
            einstellungen.Padding = new Padding(Theme.S(18), Theme.S(14), Theme.S(18), Theme.S(10));
            // Höhe der Karte an die (evtl. umbrochenen) Felder anpassen
            Action hoehe = () =>
            {
                int breite = einstellungen.Width - einstellungen.Padding.Horizontal;
                if (breite <= 0) return;
                int h = felder.GetPreferredSize(new Size(breite, 0)).Height + einstellungen.Padding.Vertical;
                if (einstellungen.Height != h) einstellungen.Height = h;
            };
            einstellungen.Height = Theme.S(24 + 38 + 6) + einstellungen.Padding.Vertical;
            einstellungen.SizeChanged += (s, e) => hoehe();
            Thema(() => felder.BackColor = Theme.Karte);

            // Statuszeile mit Seitenwahl
            status = new StatusChip { Location = new Point(0, Theme.S(8)) };
            lblSeite = new Label { AutoSize = false, Width = Theme.S(110), Height = Theme.S(36), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 9.5f) };
            btnZurueck = Knopf("", "chevron-left", () => Blaettern(-1));
            btnVor = Knopf("", "chevron-right", () => Blaettern(1));
            var seitenwahl = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false };
            seitenwahl.Controls.Add(btnZurueck);
            seitenwahl.Controls.Add(lblSeite);
            seitenwahl.Controls.Add(btnVor);
            lblSeite.Margin = new Padding(Theme.S(4), 0, 0, 0);

            // Zoom
            lblZoom = new Label { AutoSize = false, Width = Theme.S(84), Height = Theme.S(36), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 9.5f), Margin = new Padding(Theme.S(4), 0, 0, 0) };
            btnEinpassen = Knopf("", "maximize", Einpassen);
            var zoomwahl = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(0, 0, Theme.S(20), 0) };
            zoomwahl.Controls.Add(lblZoom);
            zoomwahl.Controls.Add(btnEinpassen);

            var statuszeile = new Panel { Dock = DockStyle.Top, Height = Theme.S(52), Padding = new Padding(0, Theme.S(8), 0, Theme.S(8)) };
            statuszeile.Controls.Add(status);
            statuszeile.Controls.Add(zoomwahl);
            statuszeile.Controls.Add(seitenwahl);
            Thema(() =>
            {
                statuszeile.BackColor = seitenwahl.BackColor = zoomwahl.BackColor = lblSeite.BackColor = lblZoom.BackColor = Theme.Hintergrund;
                lblSeite.ForeColor = lblZoom.ForeColor = Theme.Text;
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
                if (ziehen)
                {
                    verschiebung = new PointF(verschiebungStart.X + e.X - ziehStart.X, verschiebungStart.Y + e.Y - ziehStart.Y);
                    vorschau.Invalidate();
                }
                else vorschau.Cursor = plan != null && zoom > 1 ? Cursors.Hand : Cursors.Default;
            };
            vorschau.MouseUp += (s, e) =>
            {
                ziehen = false;
                vorschau.Cursor = plan != null && zoom > 1 ? Cursors.Hand : Cursors.Default;
            };
            vorschau.MouseDoubleClick += (s, e) =>
            {
                if (plan == null || e.Button != MouseButtons.Left) return;
                if (zoom > 1.01f) Einpassen();
                else Zoomen(2.5f, e.Location);
            };
            Thema(() => vorschau.BackColor = Theme.Hintergrund);

            // Hinweise
            lstWarnungen = new HinweisListe();
            warnKarte = KarteMit(lstWarnungen);
            warnKarte.Dock = DockStyle.Bottom;
            warnKarte.Height = Theme.S(150);
            warnKarte.Padding = new Padding(Theme.S(14), Theme.S(10), Theme.S(14), Theme.S(10));
            warnAbstand = new Panel { Dock = DockStyle.Bottom, Height = Theme.S(14) };
            Thema(() => warnAbstand.BackColor = Theme.Hintergrund);

            var koerper = new Panel { Dock = DockStyle.Fill };
            Thema(() => koerper.BackColor = Theme.Hintergrund);
            koerper.Controls.Add(vorschau);
            koerper.Controls.Add(warnAbstand);
            koerper.Controls.Add(warnKarte);
            koerper.Controls.Add(statuszeile);
            koerper.Controls.Add(einstellungen);

            return NeueSeite("Plan",
                "Zeitraum wählen und auf «Plan erstellen» klicken. Jeder Klick ergibt eine neue, gleich faire Variante. Zoomen mit dem Mausrad oder Doppelklick, den vergrösserten Plan mit der Maus verschieben.",
                koerper,
                Knopf("Drucken", "printer", Drucken),
                Knopf("Als PDF", "file-down", AlsPdf),
                Knopf("Excel", "file-spreadsheet", AlsExcel),
                Knopf("Plan erstellen", "sparkles", Erstellen, true));
        }

        Control Feld(string titel, Control eingabe)
        {
            var p = new Panel { Width = eingabe.Width, Height = Theme.S(24) + eingabe.Height, Margin = new Padding(0, 0, Theme.S(18), Theme.S(6)) };
            var l = new Label { Text = titel, AutoSize = true, Location = new Point(0, 0), Font = new Font("Segoe UI Semibold", 8.5f) };
            eingabe.Location = new Point(0, Theme.S(24));
            p.Controls.Add(l);
            p.Controls.Add(eingabe);
            Thema(() => { p.BackColor = l.BackColor = Theme.Karte; l.ForeColor = Theme.Gedaempft; });
            return p;
        }

        Panel SeiteStatistik()
        {
            gridStatistik = NeuesGrid();
            gridStatistik.ReadOnly = true;
            gridStatistik.EditMode = DataGridViewEditMode.EditProgrammatically;
            gridStatistik.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridStatistik.SelectionChanged += (s, e) => gridStatistik.Invalidate();
            gridStatistik.CellMouseEnter += (s, e) =>
            {
                if (e.RowIndex == statistikHover) return;
                if (statistikHover >= 0 && statistikHover < gridStatistik.RowCount) gridStatistik.InvalidateRow(statistikHover);
                statistikHover = e.RowIndex;
                if (statistikHover >= 0) gridStatistik.InvalidateRow(statistikHover);
            };
            gridStatistik.MouseLeave += (s, e) =>
            {
                if (statistikHover >= 0 && statistikHover < gridStatistik.RowCount) gridStatistik.InvalidateRow(statistikHover);
                statistikHover = -1;
            };
            return NeueSeite("Statistik",
                "Wie viele Stunden jede Person auf welchem Posten eingeteilt ist. So siehst du auf einen Blick, ob die Verteilung gerecht ist.",
                KarteMit(gridStatistik));
        }

        // ---------------------------------------------------------------- Tabellen

        DataGridView NeuesGrid()
        {
            var g = new DataGridView
            {
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = Theme.S(44),
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                EditMode = DataGridViewEditMode.EditOnEnter,
            };
            typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(g, true, null);
            g.RowTemplate.Height = Theme.S(42);
            g.DefaultCellStyle.Font = new Font("Segoe UI", 10f);
            g.DefaultCellStyle.Padding = new Padding(Theme.S(8), 0, Theme.S(8), 0);
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9f);
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(Theme.S(8), 0, 0, 0);
            g.DataError += (s, e) => { e.ThrowException = false; };
            g.CellMouseDown += (s, e) => benutzteTabellen.Add(g);
            g.KeyDown += (s, e) => benutzteTabellen.Add(g);
            g.SelectionChanged += (s, e) =>
            {
                if (!benutzteTabellen.Contains(g) && g.SelectedCells.Count > 0) g.ClearSelection();
            };
            g.CellPainting += ZelleZeichnen;
            // Spaltenbreiten selbst nach FillWeight verteilen (der eingebaute Fill-Modus verrechnet sich bei versteckten Seiten)
            g.Resize += (s, e) => SpaltenVerteilen(g);
            g.ColumnAdded += (s, e) => SpaltenVerteilen(g);
            // keine Bildlaufleisten: gescrollt wird mit dem Mausrad
            g.ScrollBars = ScrollBars.None;
            g.MouseWheel += (s, e) =>
            {
                if (g.RowCount == 0) return;
                int sichtbar = Math.Max(1, g.DisplayedRowCount(false));
                int ziel = g.FirstDisplayedScrollingRowIndex - Math.Sign(e.Delta) * 2;
                ziel = Math.Max(0, Math.Min(Math.Max(0, g.RowCount - sichtbar), ziel));
                if (ziel != g.FirstDisplayedScrollingRowIndex) g.FirstDisplayedScrollingRowIndex = ziel;
                var h = e as HandledMouseEventArgs;
                if (h != null) h.Handled = true;
            };
            tabellen.Add(g);
            GridFarben(g);
            return g;
        }

        static void SpaltenVerteilen(DataGridView g)
        {
            if (g.Columns.Count == 0 || g.ClientSize.Width <= 0) return;
            // Spalten mit fester Breite (Zeilenknöpfe) behalten ihre Breite, der Rest wird verteilt
            var spalten = g.Columns.Cast<DataGridViewColumn>().Where(c => !FesteBreite(c)).ToList();
            int verfuegbar = g.ClientSize.Width - 1 - g.Columns.Cast<DataGridViewColumn>().Where(FesteBreite).Sum(c => c.Width);
            float summe = spalten.Sum(c => c.FillWeight);
            foreach (var c in spalten)
                c.Width = Math.Max(c.MinimumWidth, (int)(verfuegbar * c.FillWeight / summe));
        }

        static void GridFarben(DataGridView g)
        {
            g.BackgroundColor = Theme.Karte;
            g.GridColor = Theme.Gitter;
            g.DefaultCellStyle.BackColor = Theme.Karte;
            g.DefaultCellStyle.ForeColor = Theme.Text;
            g.DefaultCellStyle.SelectionBackColor = Theme.Auswahl;
            g.DefaultCellStyle.SelectionForeColor = Theme.Text;
            g.ColumnHeadersDefaultCellStyle.BackColor = Theme.Karte;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Gedaempft;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Karte;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.Gedaempft;
            g.Invalidate();
        }

        void ZelleZeichnen(object sender, DataGridViewCellPaintingEventArgs e)
        {
            var zustand = e.Graphics.Save();
            try { ZelleZeichnenInnen(sender, e); }
            finally { e.Graphics.Restore(zustand); }
        }

        void ZelleZeichnenInnen(object sender, DataGridViewCellPaintingEventArgs e)
        {
            var grid = (DataGridView)sender;
            if (e.ColumnIndex < 0) return;
            var spalte = grid.Columns[e.ColumnIndex];
            var g = e.Graphics;
            var r = e.CellBounds;

            if (e.RowIndex == -1)
            {
                g.SmoothingMode = SmoothingMode.None;
                using (var b = new SolidBrush(Theme.Karte))
                    g.FillRectangle(b, r);
                int x = r.X + Theme.S(8);
                if (spalte.Tag is Color)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    int d = Theme.S(10);
                    Formen.Fuellen(g, new RectangleF(x, r.Y + (r.Height - d) / 2f, d, d), Theme.S(3), (Color)spalte.Tag);
                    x += d + Theme.S(7);
                }
                TextRenderer.DrawText(g, spalte.HeaderText, grid.ColumnHeadersDefaultCellStyle.Font,
                    Rectangle.FromLTRB(x, r.Y, r.Right - Theme.S(4), r.Bottom), Theme.Gedaempft,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                using (var p = new Pen(Theme.Rand))
                    g.DrawLine(p, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
                e.Handled = true;
                return;
            }

            // reine Anzeige-Tabellen (Statistik): gleicher Zeilenhintergrund wie die bearbeitbaren Tabellen
            if (e.RowIndex < 0 || spalte.Tag is SpaltenInfo) return;
            TabellenStil.Hintergrund(g, grid, e.RowIndex, r, grid == gridStatistik ? statistikHover : -1);
            TextRenderer.DrawText(g, Convert.ToString(e.FormattedValue), grid.DefaultCellStyle.Font,
                Rectangle.FromLTRB(r.X + Theme.S(10), r.Y, r.Right - Theme.S(4), r.Bottom),
                e.ColumnIndex < 3 ? Theme.Text : (Convert.ToString(e.FormattedValue) == "–" ? Theme.Gedaempft : Theme.Text),
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            e.Handled = true;
        }

        int statistikHover = -1;

        static DataGridViewTextBoxColumn Spalte(string name, string titel, int gewicht, SpaltenInfo info)
        {
            int mindestens;
            switch (info.Art)
            {
                case SpaltenArt.Schalter: mindestens = 70; break;
                case SpaltenArt.Zahl: mindestens = 72; break;
                case SpaltenArt.Farbe: mindestens = 84; break;
                case SpaltenArt.Mehrfach: mindestens = 130; break;
                case SpaltenArt.Auswahl: mindestens = 100; break;
                default: mindestens = 90; break;
            }
            return new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = titel,
                FillWeight = gewicht,
                Tag = info,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                MinimumWidth = Theme.S(mindestens),
            };
        }

        static SpaltenInfo StundenInfo()
        {
            return new SpaltenInfo(SpaltenArt.Auswahl, () => StundenListe);
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
        void Zeilenknoepfe(DataGridView g, bool verschiebbar)
        {
            var knoepfe = new List<ZeilenKnopf>();
            if (verschiebbar)
            {
                knoepfe.Add(new ZeilenKnopf("chevron-up", i => Verschieben(g, i, -1), i => i > 0));
                knoepfe.Add(new ZeilenKnopf("chevron-down", i => Verschieben(g, i, 1), i => i < g.RowCount - 1));
            }
            knoepfe.Add(new ZeilenKnopf("trash-2", i => Entfernen(g, i)) { Gefahr = true });
            var spalte = Spalte("Aktionen", "", 1, new SpaltenInfo(knoepfe.ToArray()));
            spalte.MinimumWidth = spalte.Width = TabellenEditor.AktionenBreite(knoepfe.Count);
            g.Columns.Add(spalte);
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

        static bool FesteBreite(DataGridViewColumn c)
        {
            var info = c.Tag as SpaltenInfo;
            return info != null && info.Art == SpaltenArt.Aktionen;
        }

        TabellenEditor Bearbeitbar(DataGridView g)
        {
            var ed = new TabellenEditor(g);
            editoren.Add(ed);
            return ed;
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
            Hinzufuegen(gridPersonen, grad, "", funktion, gruppe);
        }

        Panel SeiteStammdaten()
        {
            gridFunktionen = NeuesGrid();
            gridFunktionen.Columns.Add(Spalte("Name", "Funktion", 150, new SpaltenInfo(SpaltenArt.Text)));
            gridFunktionen.Columns.Add(Spalte("ImPlan", "Im Plan einteilen", 80, new SpaltenInfo(SpaltenArt.Schalter)));
            Zeilenknoepfe(gridFunktionen, true);
            Bearbeitbar(gridFunktionen).Umbenannt += (z, alt, neu) =>
            {
                Ersetzen(gridPersonen, "Funktion", alt, neu);
                foreach (DataGridViewRow r in gridPosten.Rows)
                {
                    var liste = r.Cells["Funktionen"].Tag as List<string>;
                    if (liste != null && liste.Contains(alt))
                        r.Cells["Funktionen"].Tag = liste.Select(x => x == alt ? neu : x).ToList();
                }
            };
            gridGrade = NeuesGrid();
            gridGrade.Columns.Add(Spalte("Name", "Grad", 100, new SpaltenInfo(SpaltenArt.Text)));
            Zeilenknoepfe(gridGrade, true);
            Bearbeitbar(gridGrade).Umbenannt += (z, alt, neu) => Ersetzen(gridPersonen, "Grad", alt, neu);

            var raster = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = Padding.Empty };
            raster.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            raster.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            raster.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var links = Bereich("Funktionen", gridFunktionen,
                Knopf("Funktion", "plus", () => Hinzufuegen(gridFunktionen, "", true), true));
            links.Margin = new Padding(0, 0, Theme.S(12), 0);
            var rechts = Bereich("Grade", gridGrade,
                Knopf("Grad", "plus", () => Hinzufuegen(gridGrade, ""), true));
            rechts.Margin = new Padding(Theme.S(12), 0, 0, 0);
            raster.Controls.Add(links, 0, 0);
            raster.Controls.Add(rechts, 1, 0);
            Thema(() => raster.BackColor = Theme.Hintergrund);
            return NeueSeite("Funktionen & Grade",
                "Lege hier fest, welche Funktionen und Grade es gibt. Bei den Personen wählst du sie dann nur noch aus. «Im Plan einteilen» aus = erscheint nicht im Plan (z. B. Reserve). Die Reihenfolge der Funktionen ist die Reihenfolge im Plan.",
                raster);
        }

        Control Bereich(string titel, DataGridView grid, params Control[] knoepfe)
        {
            var p = new Panel { Dock = DockStyle.Fill };
            var kopf = new Panel { Dock = DockStyle.Top, Height = Theme.S(50) };
            var lbl = new Label { Text = titel, Font = new Font("Segoe UI Semibold", 12.5f), AutoSize = true, Location = new Point(0, Theme.S(6)), UseMnemonic = false };
            var leiste = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false };
            foreach (var k in knoepfe) leiste.Controls.Add(k);
            kopf.Controls.Add(lbl);
            kopf.Controls.Add(leiste);
            var karte = KarteMit(grid);
            karte.Dock = DockStyle.Fill;
            p.Controls.Add(karte);
            p.Controls.Add(kopf);
            Thema(() =>
            {
                p.BackColor = kopf.BackColor = leiste.BackColor = lbl.BackColor = Theme.Hintergrund;
                lbl.ForeColor = Theme.Text;
            });
            return p;
        }

        static DataGridViewTextBoxColumn Textspalte(string name, string titel, int gewicht)
        {
            return new DataGridViewTextBoxColumn { Name = name, HeaderText = titel, FillWeight = gewicht };
        }

        int Hinzufuegen(DataGridView g, params object[] werte)
        {
            foreach (var ed in editoren) ed.Schliessen(true);
            benutzteTabellen.Add(g);
            int i = g.Rows.Add(werte);
            int name = g.Columns.Contains("Name") ? g.Columns["Name"].Index : 0;
            g.ClearSelection();
            g.CurrentCell = g.Rows[i].Cells[name];
            g.Rows[i].Selected = true;
            var editor = editoren.FirstOrDefault(x => x.Grid == g);
            if (editor != null) BeginInvoke(new Action(() => editor.Oeffnen(name, i)));
            return i;
        }

        void Entfernen(DataGridView g)
        {
            foreach (var ed in editoren) ed.Schliessen(true);
            var zeilen = g.SelectedCells.Cast<DataGridViewCell>().Select(c => c.RowIndex).Distinct().OrderByDescending(i => i).ToList();
            if (zeilen.Count == 0 && g.CurrentCell != null) zeilen.Add(g.CurrentCell.RowIndex);
            if (zeilen.Count > 1 && !Frage(zeilen.Count + " Zeilen entfernen?")) return;
            foreach (int i in zeilen) g.Rows.RemoveAt(i);
        }

        void Entfernen(DataGridView g, int zeile)
        {
            foreach (var ed in editoren) ed.Schliessen(true);
            if (zeile >= 0 && zeile < g.Rows.Count) g.Rows.RemoveAt(zeile);
        }

        // ---------------------------------------------------------------- Daten <-> Oberfläche

        void Anzeigen(Konfiguration k)
        {
            cfg = k;
            foreach (var ed in editoren) ed.Schliessen(false);
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
                gridPersonen.Rows.Add(p.Grad, p.Name, p.Funktion, p.Gruppe);

            PostenAnzeigen(k.Posten);

            gridEssen.Rows.Clear();
            foreach (var e in k.Essenszeiten)
                gridEssen.Rows.Add(e.Name, Zeit.Text(e.Von), Zeit.Text(e.Bis));

            txtName.Text = k.Name;
            dtStart.Wert = k.Start > new DateTime(2000, 1, 1) ? k.Start : DateTime.Today;
            dtEnde.Wert = k.Ende > new DateTime(2000, 1, 1) ? k.Ende : DateTime.Today.AddDays(1);
            abloesung.Index = Math.Min(AbloesungListe.Length, Math.Max(1, k.AbloesungStunden)) - 1;

            plan = null;
            seite = 0;
            benutzteTabellen.Clear();
            foreach (var t in tabellen) { t.ClearSelection(); t.CurrentCell = null; }   // keine automatisch ausgewählte erste Zeile
            lstWarnungen.Setzen(new string[0]);
            gridStatistik.Columns.Clear();
            StatusAktualisieren();
            Titel();
        }

        void PostenAnzeigen(List<Posten> posten)
        {
            gridPosten.Rows.Clear();
            foreach (var p in posten)
            {
                int i = gridPosten.Rows.Add(p.Name, "", p.Anzahl.ToString(), "", Zeit.Text(p.OffenVon), Zeit.Text(p.OffenBis),
                    p.Typ == PostenTyp.Auffang ? "Auffang" : "Normal", p.AmStueck, p.EssenMoeglich);
                gridPosten.Rows[i].Cells["Farbe"].Tag = Color.FromArgb(p.Farbe);
                gridPosten.Rows[i].Cells["Funktionen"].Tag = p.Funktionen.ToList();
            }
        }

        Konfiguration Einlesen()
        {
            foreach (var ed in editoren) ed.Schliessen(true);

            var k = new Konfiguration
            {
                Name = txtName.Text.Trim(),
                Start = dtStart.Wert,
                Ende = dtEnde.Wert,
                AbloesungStunden = abloesung.Index + 1,
            };
            foreach (var r in Zeilen(gridFunktionen))
                k.Funktionen.Add(new Funktion { Name = Wert(r, "Name"), ImPlan = r.Cells["ImPlan"].Value is bool && (bool)r.Cells["ImPlan"].Value });
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
                });
            k.Posten = PostenEinlesen();
            foreach (var r in Zeilen(gridEssen))
                k.Essenszeiten.Add(new Essenszeit { Name = Wert(r, "Name"), Von = Stunde(r, "Von"), Bis = Stunde(r, "Bis") });
            return k;
        }

        // auchOhneNamen: alle Zeilen der Tabelle (zum Umsortieren), sonst nur benannte Posten
        List<Posten> PostenEinlesen(bool auchOhneNamen = false)
        {
            var liste = new List<Posten>();
            foreach (var r in gridPosten.Rows.Cast<DataGridViewRow>().Where(r => auchOhneNamen || Wert(r, "Name") != ""))
            {
                int anzahl;
                int.TryParse(Wert(r, "Anzahl"), out anzahl);
                PostenTyp typ;
                if (!Enum.TryParse(Wert(r, "Typ"), out typ)) typ = PostenTyp.Normal;
                object tag = r.Cells["Farbe"].Tag;
                liste.Add(new Posten
                {
                    Name = Wert(r, "Name"),
                    Farbe = (tag is Color ? (Color)tag : Color.Gray).ToArgb(),
                    Anzahl = Math.Max(0, anzahl),
                    OffenVon = Stunde(r, "Von"),
                    OffenBis = Stunde(r, "Bis"),
                    Typ = typ,
                    EssenMoeglich = r.Cells["Essen"].Value is bool && (bool)r.Cells["Essen"].Value,
                    AmStueck = r.Cells["AmStueck"].Value is bool && (bool)r.Cells["AmStueck"].Value,
                    Funktionen = (r.Cells["Funktionen"].Tag as List<string> ?? new List<string>()).ToList(),
                });
            }
            return liste;
        }

        static IEnumerable<DataGridViewRow> Zeilen(DataGridView g)
        {
            return g.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow && Wert(r, "Name") != "");
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

        void Verschieben(DataGridView grid, int i, int richtung)
        {
            int j = i + richtung;
            if (i < 0 || j < 0 || j >= grid.Rows.Count) return;
            int spalte = grid.CurrentCell != null ? grid.CurrentCell.ColumnIndex : 0;
            foreach (var ed in editoren) ed.Schliessen(true);
            benutzteTabellen.Add(grid);
            if (grid == gridPosten)
            {
                var liste = PostenEinlesen(true);
                if (i >= liste.Count || j >= liste.Count) return;
                var p = liste[i];
                liste[i] = liste[j];
                liste[j] = p;
                PostenAnzeigen(liste);
            }
            else
            {
                for (int c = 0; c < grid.Columns.Count; c++)
                {
                    object v = grid.Rows[i].Cells[c].Value;
                    grid.Rows[i].Cells[c].Value = grid.Rows[j].Cells[c].Value;
                    grid.Rows[j].Cells[c].Value = v;
                }
            }
            grid.ClearSelection();
            grid.CurrentCell = grid.Rows[j].Cells[spalte];
            grid.Rows[j].Selected = true;
        }

        // ---------------------------------------------------------------- Plan

        void Erstellen()
        {
            try
            {
                cfg = Einlesen();
                plan = Planer.Erstelle(cfg, Environment.TickCount);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Plan erstellen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            seite = 0;
            lstWarnungen.Setzen(plan.Warnungen);
            StatistikAnzeigen();
            StatusAktualisieren();
        }

        void Blaettern(int richtung)
        {
            if (plan == null) return;
            seite = Math.Max(0, Math.Min(plan.Seiten - 1, seite + richtung));
            StatusAktualisieren();
        }

        void StatusAktualisieren()
        {
            bool hinweise = plan != null && plan.Warnungen.Count > 0;
            warnKarte.Visible = warnAbstand.Visible = hinweise;
            if (plan == null)
                status.Setzen("", "circle-check", Theme.Erfolg);
            else if (hinweise)
                status.Setzen(plan.Warnungen.Count == 1 ? "1 Hinweis" : plan.Warnungen.Count + " Hinweise", "triangle-alert", Theme.Warnung);
            else
                status.Setzen(plan.Cfg.Essenszeiten.Count > 0 ? "Alle Posten besetzt, alle haben Essen" : "Alle Posten besetzt", "circle-check", Theme.Erfolg);
            lblSeite.Text = plan == null ? "" : "Seite " + (seite + 1) + " / " + plan.Seiten;
            btnZurueck.Visible = btnVor.Visible = plan != null && plan.Seiten > 1;
            lblSeite.Visible = plan != null;
            btnZurueck.Enabled = seite > 0;
            btnVor.Enabled = plan != null && seite < plan.Seiten - 1;
            if (plan == null) { zoom = 1f; verschiebung = PointF.Empty; }
            ZoomAnzeigen();
            vorschau.Invalidate();
        }

        void VorschauZeichnen(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var platz = new RectangleF(0, 0, vorschau.Width - 1, vorschau.Height - 1);
            Formen.Fuellen(g, platz, Theme.S(12), Theme.Vorschau);

            if (plan == null)
            {
                int s = Theme.S(44);
                float y = platz.Height / 2 - Theme.S(50);
                Lucide.Zeichne(g, "calendar-days", new RectangleF((platz.Width - s) / 2, y, s, s), Theme.Gedaempft, 1.6f);
                using (var f1 = new Font("Segoe UI Semibold", 12f))
                using (var f2 = new Font("Segoe UI", 9.5f))
                {
                    TextRenderer.DrawText(g, "Noch kein Plan erstellt", f1, new Rectangle(0, (int)y + s + Theme.S(14), vorschau.Width, Theme.S(28)), Theme.Text, TextFormatFlags.HorizontalCenter);
                    TextRenderer.DrawText(g, "Zeitraum wählen und oben rechts auf «Plan erstellen» klicken.", f2, new Rectangle(0, (int)y + s + Theme.S(44), vorschau.Width, Theme.S(24)), Theme.Gedaempft, TextFormatFlags.HorizontalCenter);
                }
                return;
            }

            var blatt = Blatt();
            if (blatt.Width < 50 || blatt.Height < 50) return;
            using (var p = Formen.Rund(platz, Theme.S(12)))
                g.SetClip(p);
            for (int i = 1; i <= 6; i++)
                Formen.Fuellen(g, new RectangleF(blatt.X - i + 1, blatt.Y - i + 3, blatt.Width + 2 * i - 2, blatt.Height + 2 * i - 2), Theme.S(4) + i, Color.FromArgb(Theme.Dunkel ? 22 : 8, 0, 0, 0));
            Formen.Fuellen(g, blatt, Theme.S(4), Color.White);
            int bw = (int)Math.Round(blatt.Width), bh = (int)Math.Round(blatt.Height);
            if ((long)bw * bh <= 12000000L)
            {
                // Blatt nur neu zeichnen, wenn sich Plan, Seite oder Grösse ändern; Verschieben kopiert nur das Bild
                if (vorschauBild == null || vorschauPlan != plan || vorschauSeite != seite || vorschauBild.Width != bw || vorschauBild.Height != bh)
                {
                    if (vorschauBild != null) vorschauBild.Dispose();
                    vorschauBild = new Bitmap(bw, bh);
                    using (var gb = Graphics.FromImage(vorschauBild))
                    {
                        gb.Clear(Color.White);
                        float rand = bw * 0.035f;
                        Zeichner.Seite(gb, RectangleF.Inflate(new RectangleF(0, 0, bw, bh), -rand, -rand), plan, seite);
                    }
                    vorschauPlan = plan;
                    vorschauSeite = seite;
                }
                g.DrawImageUnscaled(vorschauBild, (int)Math.Round(blatt.X), (int)Math.Round(blatt.Y));
            }
            else
            {
                // sehr stark gezoomt: direkt zeichnen (nur der sichtbare Ausschnitt kostet Zeit)
                float innen = blatt.Width * 0.035f;
                Zeichner.Seite(g, RectangleF.Inflate(blatt, -innen, -innen), plan, seite);
            }
        }

        Bitmap vorschauBild;
        Plan vorschauPlan;
        int vorschauSeite;

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
            btnEinpassen.Visible = lblZoom.Visible = sichtbar;
            lblZoom.Text = zoom <= 1.001f ? "Ganze Seite" : Math.Round(zoom * 100) + " %";
            btnEinpassen.Enabled = zoom > 1.001f;
            vorschau.Cursor = sichtbar && zoom > 1 ? Cursors.Hand : Cursors.Default;
        }

        void StatistikAnzeigen()
        {
            var g = gridStatistik;
            g.Rows.Clear();
            g.Columns.Clear();
            g.Columns.Add("Grad", "Grad");
            g.Columns.Add("Name", "Name");
            g.Columns.Add("Gruppe", "Gruppe");
            g.Columns["Name"].FillWeight = 150;
            for (int k = 0; k < plan.Cfg.Posten.Count; k++)
            {
                g.Columns.Add("P" + k, plan.Cfg.Posten[k].Name);
                g.Columns[g.Columns.Count - 1].Tag = Color.FromArgb(plan.Cfg.Posten[k].Farbe);
            }
            g.Columns.Add("Essen", "Essen");
            g.Columns.Add("Total", "Dienst-Std.");
            foreach (DataGridViewColumn c in g.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;

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
            g.ClearSelection();
            g.CurrentCell = null;
        }

        PrintDocument Dokument(string drucker)
        {
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
                Zeichner.Seite(e.Graphics, e.MarginBounds, plan, s);
                s++;
                e.HasMorePages = s < plan.Seiten;
            };
            return pd;
        }

        bool PlanVorhanden()
        {
            if (plan != null) return true;
            MessageBox.Show(this, "Bitte zuerst auf «Plan erstellen» klicken.", "ShiftDesk", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        void Drucken()
        {
            if (!PlanVorhanden()) return;
            using (var pd = Dokument(null))
            using (var dlg = new PrintDialog { Document = pd, UseEXDialog = true, AllowSomePages = false })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try { pd.Print(); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Drucken", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        void AlsPdf()
        {
            if (!PlanVorhanden()) return;
            using (var dlg = new SaveFileDialog
            {
                Filter = "PDF-Datei (*.pdf)|*.pdf",
                FileName = DateiName(plan.Cfg.Name) + " " + plan.Start.ToString("yyyy-MM-dd") + ".pdf",
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                using (var pd = Dokument("Microsoft Print to PDF"))
                {
                    if (!pd.PrinterSettings.IsValid)
                    {
                        MessageBox.Show(this, "Der Drucker «Microsoft Print to PDF» ist auf diesem PC nicht installiert. Bitte «Drucken» verwenden.",
                            "Als PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    pd.PrinterSettings.PrintToFile = true;
                    pd.PrinterSettings.PrintFileName = dlg.FileName;
                    try
                    {
                        if (File.Exists(dlg.FileName)) File.Delete(dlg.FileName);
                        pd.Print();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, ex.Message, "Als PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                if (Frage("PDF gespeichert:\n" + dlg.FileName + "\n\nJetzt öffnen?"))
                {
                    for (int i = 0; i < 50 && !File.Exists(dlg.FileName); i++)
                        System.Threading.Thread.Sleep(100);
                    try { System.Diagnostics.Process.Start(dlg.FileName); }
                    catch { }
                }
            }
        }

        void AlsExcel()
        {
            if (!PlanVorhanden()) return;
            using (var dlg = new SaveFileDialog
            {
                Filter = "Excel-Datei (*.xlsx)|*.xlsx",
                FileName = DateiName(plan.Cfg.Name) + " " + plan.Start.ToString("yyyy-MM-dd") + ".xlsx",
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    ExcelExport.Speichern(plan, dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Die Excel-Datei konnte nicht gespeichert werden:\n" + ex.Message + "\n\nIst sie vielleicht noch in Excel geöffnet?",
                        "Excel", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (Frage("Excel-Datei gespeichert:\n" + dlg.FileName + "\n\nJetzt öffnen?"))
                    try { System.Diagnostics.Process.Start(dlg.FileName); }
                    catch { }
            }
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
                MessageBox.Show(this, "Die Datei konnte nicht geöffnet werden:\n" + ex.Message, "Öffnen", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void Speichern(bool neuerName)
        {
            if (dateiPfad == null || neuerName)
            {
                using (var dlg = new SaveFileDialog { Filter = "ShiftDesk-Datei (*.shiftdesk)|*.shiftdesk", FileName = "Wachtplan.shiftdesk" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    dateiPfad = dlg.FileName;
                }
            }
            try
            {
                Einlesen().Speichern(dateiPfad);
                Titel();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Speichern fehlgeschlagen:\n" + ex.Message, "Speichern", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        bool Frage(string text)
        {
            return MessageBox.Show(this, text, "ShiftDesk", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }
    }
}
