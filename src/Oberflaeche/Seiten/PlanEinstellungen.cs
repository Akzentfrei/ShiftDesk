using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ShiftDesk.Ausgabe;
using ShiftDesk.Modell;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Popups;
using ShiftDesk.Oberflaeche.Steuerelemente;

namespace ShiftDesk.Oberflaeche.Seiten
{
    /// <summary>
    /// Einstellungen auf der Plan-Seite in drei Abschnitten: Zeitraum, Ablösung und Ausdruck.
    /// Im breiten Fenster eine schmale Spalte (Felder ganz oder halb breit), im schmalen Fenster fliessend
    /// in ihrer natürlichen Breite über der Vorschau.
    /// </summary>
    public class PlanEinstellungen : Karte
    {
        static readonly string[] PlanArtListe = { "Tagesplan (jeden Tag)", "Zeitraum mit Datum" };
        static readonly string[] AbloesungListe = { "Jede Stunde", "Alle 2 Stunden", "Alle 3 Stunden", "Alle 4 Stunden" };
        static readonly string[] TeamListe = { "Keine Teams", "Zweierteams" };

        /// <summary>Auswahl «Am Stück draussen»; Index = Eintrag in <see cref="DraussenStunden"/>.</summary>
        static readonly string[] DraussenListe = { "Keine Grenze", "Höchstens 1 Stunde", "Höchstens 2 Stunden", "Höchstens 3 Stunden", "Höchstens 4 Stunden", "Höchstens 6 Stunden" };

        /// <summary>Erlaubte Stunden am Stück draussen zu jedem Eintrag von <see cref="DraussenListe"/> (0 = keine Grenze).</summary>
        static readonly int[] DraussenStunden = { 0, 1, 2, 3, 4, 6 };

        /// <summary>Breite der Einstellungen im breiten Fenster.</summary>
        public static int SpaltenBreite
        {
            get { return Thema.S(340); }
        }

        /// <summary>Ein Eingabefeld mit seiner natürlichen Breite (oben) bzw. ganzer oder halber Spaltenbreite (links).</summary>
        class PlanFeld
        {
            public Panel Feld;
            public Control Eingabe;
            public int Breite, EingabeBreite;
            public bool Halb;
        }

        readonly Eingabefeld txtName, txtEinheit;
        readonly DatumFeld dtStart, dtEnde;
        readonly AuswahlFeld planArt, abloesung, draussen, stuendlichVon, stuendlichBis, teams, sprache;
        readonly Panel feldStart, feldEnde;
        readonly FlowLayoutPanel felder;
        readonly BildlaufPanel bild;
        readonly List<PlanFeld> planFelder = new List<PlanFeld>();
        readonly List<Abschnitt> abschnitte = new List<Abschnitt>();
        bool? seitlich;

        /// <summary>«Tagesplan» oder «Zeitraum mit Datum» wurde gewählt (Start und Ende werden ein- oder ausgeblendet).</summary>
        public event Action ArtGeaendert;

        /// <summary>Name, Einheit oder Sprache wurden geändert: sie gelten sofort für den Ausdruck, ohne neuen Plan.</summary>
        public event Action DruckAngabenGeaendert;

        public PlanEinstellungen()
        {
            txtName = new Eingabefeld { Width = Thema.S(380) };
            txtEinheit = new Eingabefeld { Width = Thema.S(200) };
            dtStart = new DatumFeld();
            dtEnde = new DatumFeld();
            planArt = new AuswahlFeld(PlanArtListe) { Width = Thema.S(220) };
            planArt.WertGeaendert += (s, e) => PlanArtAnzeigen();
            sprache = new AuswahlFeld(DruckSprache.Sprachen.Select(s => s.Name).ToArray()) { Width = Thema.S(140) };
            abloesung = new AuswahlFeld(AbloesungListe) { Width = Thema.S(170) };
            stuendlichVon = new AuswahlFeld(Zeit.Uhrzeiten) { Width = Thema.S(100) };
            stuendlichBis = new AuswahlFeld(Zeit.Uhrzeiten) { Width = Thema.S(100) };
            draussen = new AuswahlFeld(DraussenListe) { Width = Thema.S(190) };
            teams = new AuswahlFeld(TeamListe) { Width = Thema.S(190) };
            Farbwechsel.Bei(() => { txtName.ThemaAnwenden(); txtEinheit.ThemaAnwenden(); });
            txtName.Box.TextChanged += (s, e) => DruckAngabenMelden();
            txtEinheit.Box.TextChanged += (s, e) => DruckAngabenMelden();
            sprache.WertGeaendert += (s, e) => DruckAngabenMelden();

            felder = new FlowLayoutPanel { WrapContents = true, Margin = Padding.Empty };
            Abschnitt("Zeitraum");
            felder.Controls.Add(Feld("Art", planArt,
                "«Tagesplan»: ein Blatt von 00:00 bis 24:00 ohne Datum, das jeden Tag gilt (jeden Tag hat dieselbe Person zur selben Zeit denselben Posten). «Zeitraum mit Datum»: der Plan läuft von Start bis Ende über mehrere Tage und verteilt die Posten über alle Tage gerecht."));
            felder.Controls.Add(feldStart = Feld("Start", dtStart));
            felder.Controls.Add(feldEnde = Feld("Ende", dtEnde, "Der Plan kann über mehrere Tage gehen (höchstens 31). Je 24 Stunden kommen auf eine Seite."));
            Abschnitt("Ablösung");
            felder.Controls.Add(Feld("Ablösung", abloesung, "Wie oft die Posten wechseln, ab Mitternacht gerechnet (z. B. alle 2 Stunden: 00:00, 02:00, 04:00 …)."));
            felder.Controls.Add(Feld("Jede Stunde von", stuendlichVon, "In diesem Zeitfenster (z. B. 00:00 bis 06:00) wird trotz längerer Ablösung jede Stunde abgelöst. Gleiche Zeit bei «von» und «bis» = kein Fenster.", true));
            felder.Controls.Add(Feld("bis", stuendlichBis, null, true));
            felder.Controls.Add(Feld("Am Stück draussen", draussen,
                "So lange bleibt höchstens jemand am Stück auf Posten im Freien (Schalter «Draussen» bei den Posten). Bei längeren Ablösungen tauscht der Planer mitten im Block mit jemandem von drinnen, sonst gibt es einen Hinweis."));
            felder.Controls.Add(Feld("Teams", teams,
                "«Zweierteams»: je zwei aufeinanderfolgende Personen der Personen-Tabelle mit gleicher Funktion und Gruppe sind wenn möglich zusammen auf dem gleichen Posten. " +
                "Gehen die Leute nicht auf, verteilt sich ein Team auf zwei einzelne Posten (z. B. beide Zuko), und wer übrig bleibt, bildet für eine Ablösung mit jemand anderem ein Paar, damit auch er auf Posten für zwei kommt."));
            Abschnitt("Ausdruck");
            felder.Controls.Add(Feld("Name", txtName,
                "Steht auf dem Ausdruck bei «Für». Ändert den Plan nicht und ist sofort in der Vorschau zu sehen."));
            felder.Controls.Add(Feld("Einheit", txtEinheit,
                "Steht auf dem Ausdruck bei «Einheit». Ändert den Plan nicht und ist sofort in der Vorschau zu sehen.", true));
            felder.Controls.Add(Feld("Sprache", sprache,
                "Sprache des Ausdrucks (Deutsch, Französisch, Italienisch oder Englisch). Namen von Posten, Funktionen und Personen bleiben so, wie du sie erfasst hast.", true));
            bild = new BildlaufPanel(felder) { Dock = DockStyle.Fill };
            Controls.Add(bild);
            Farbwechsel.Bei(() => { BackColor = felder.BackColor = bild.BackColor = Thema.Karte; });
        }

        /// <summary>Abschnittsüberschrift; ab dem zweiten Abschnitt mit einer feinen Trennlinie darüber.</summary>
        void Abschnitt(string titel)
        {
            bool linie = abschnitte.Count > 0;
            var a = new Abschnitt(titel, 0) { Height = Thema.S(linie ? 36 : 24), Margin = Padding.Empty, Trennlinie = linie };
            abschnitte.Add(a);
            felder.Controls.Add(a);
        }

        /// <summary>Eingabe mit Beschriftung darüber; mit <paramref name="info"/> steht ein Info-Knopf neben der Beschriftung.</summary>
        /// <param name="titel">Beschriftung über der Eingabe.</param>
        /// <param name="eingabe">Das Eingabefeld; seine Breite ist die natürliche Breite im schmalen Fenster.</param>
        /// <param name="info">Erklärung hinter dem Info-Knopf, null = keiner.</param>
        /// <param name="halb">Im breiten Fenster halb so breit (zwei Felder nebeneinander).</param>
        Panel Feld(string titel, Control eingabe, string info = null, bool halb = false)
        {
            var p = new Panel { Width = eingabe.Width, Height = Thema.S(24) + eingabe.Height, Margin = new Padding(0, 0, Thema.S(18), Thema.S(6)) };
            var l = new Label { Text = titel, AutoSize = true, Location = Point.Empty, Font = Schrift.Beschriftung, UseMnemonic = false };
            if (info != null) p.Width = Math.Max(p.Width, l.PreferredWidth + Thema.S(22));
            eingabe.Location = new Point(0, Thema.S(24));
            p.Controls.Add(l);
            p.Controls.Add(eingabe);
            InfoKnopf knopf = null;
            if (info != null)
            {
                knopf = new InfoKnopf(titel, info, 18);
                knopf.Location = new Point(l.PreferredWidth + Thema.S(2), Math.Max(0, (l.PreferredHeight - knopf.Height) / 2));
                p.Controls.Add(knopf);
            }
            Farbwechsel.Bei(() =>
            {
                p.BackColor = l.BackColor = Thema.Karte;
                l.ForeColor = Thema.Gedaempft;
                if (knopf != null) knopf.Invalidate();
            });
            planFelder.Add(new PlanFeld { Feld = p, Eingabe = eingabe, Breite = p.Width, EingabeBreite = eingabe.Width, Halb = halb });
            return p;
        }

        /// <summary>Beim Tagesplan zählen Start und Ende nicht: ausblenden.</summary>
        void PlanArtAnzeigen()
        {
            feldStart.Visible = feldEnde.Visible = planArt.Index == 1;
            var h = ArtGeaendert;
            if (h != null) h();
        }

        void DruckAngabenMelden()
        {
            var h = DruckAngabenGeaendert;
            if (h != null) h();
        }

        // ---------------------------------------------------------------- Anordnen

        /// <summary>Höhe, die die Einstellungen bei dieser Breite ganz brauchen.</summary>
        public int NoetigeHoehe(int breite)
        {
            return bild.InhaltHoehe(breite - Padding.Horizontal) + Padding.Vertical;
        }

        /// <summary>Als Spalte neben der Vorschau (<paramref name="seitlich"/>) oder fliessend darüber anordnen.</summary>
        public void Anordnen(bool seitlich)
        {
            if (this.seitlich == seitlich) return;
            this.seitlich = seitlich;
            SuspendLayout();
            // ohne Bildlaufleiste (geblättert wird mit dem Mausrad): links und rechts gleicher Rand
            Padding = seitlich
                ? new Padding(Thema.S(18), Thema.S(8), Thema.S(18), Thema.S(10))
                : new Padding(Thema.S(18), Thema.S(14), Thema.S(18), Thema.S(10));
            int voll = SpaltenBreite - Thema.S(18 + 18);
            int luecke = Thema.S(12);
            // halbe Felder stehen paarweise nebeneinander, das linke hat rechts die Lücke
            var linkeHalbe = planFelder.Where(x => x.Halb).Where((x, i) => i % 2 == 0).ToList();
            foreach (var f in planFelder)
            {
                if (seitlich)
                {
                    f.Feld.Width = f.Halb ? (voll - luecke) / 2 : voll;
                    f.Feld.Margin = new Padding(0, 0, linkeHalbe.Contains(f) ? luecke : 0, Thema.S(4));
                    f.Eingabe.Width = f.Feld.Width;
                }
                else
                {
                    f.Feld.Width = f.Breite;
                    f.Feld.Margin = new Padding(0, 0, Thema.S(18), Thema.S(6));
                    f.Eingabe.Width = f.EingabeBreite;
                }
            }
            foreach (var a in abschnitte)
            {
                a.Visible = seitlich;
                a.Width = voll;
            }
            ResumeLayout(true);
        }

        // ---------------------------------------------------------------- Daten

        /// <summary>Zeigt die Einstellungen einer Konfiguration.</summary>
        public void Anzeigen(Konfiguration k)
        {
            txtName.Text = k.Name;
            txtEinheit.Text = k.Einheit ?? "";
            sprache.Index = Math.Max(0, Array.FindIndex(DruckSprache.Sprachen, s => s.Kuerzel == k.Sprache));
            planArt.Index = k.Tagesplan ? 0 : 1;
            PlanArtAnzeigen();
            dtStart.Wert = k.Start > new DateTime(2000, 1, 1) ? k.Start : DateTime.Today;
            dtEnde.Wert = k.Ende > new DateTime(2000, 1, 1) ? k.Ende : DateTime.Today.AddDays(1);
            abloesung.Index = Math.Min(AbloesungListe.Length, Math.Max(1, k.AbloesungStunden)) - 1;
            stuendlichVon.Index = Math.Max(0, Math.Min(23, k.StuendlichVon));
            stuendlichBis.Index = Math.Max(0, Math.Min(23, k.StuendlichBis));
            teams.Index = k.Zweierteams ? 1 : 0;
            int d = Array.IndexOf(DraussenStunden, k.DraussenMaxStunden);
            if (d < 0) d = k.DraussenMaxStunden <= 0 ? 0 : DraussenStunden.Length - 1;
            draussen.Index = d;
        }

        /// <summary>Schreibt die Einstellungen in <paramref name="k"/>.</summary>
        public void Uebernehmen(Konfiguration k)
        {
            DruckAngabenUebernehmen(k);
            k.Start = dtStart.Wert;
            k.Ende = dtEnde.Wert;
            k.Tagesplan = planArt.Index == 0;
            k.AbloesungStunden = abloesung.Index + 1;
            k.StuendlichVon = stuendlichVon.Index;
            k.StuendlichBis = stuendlichBis.Index;
            k.Zweierteams = teams.Index == 1;
            k.DraussenMaxStunden = DraussenStunden[draussen.Index];
        }

        /// <summary>Schreibt nur Name, Einheit und Sprache (die Angaben, die den Plan nicht ändern) in <paramref name="k"/>.</summary>
        public void DruckAngabenUebernehmen(Konfiguration k)
        {
            k.Name = txtName.Text.Trim();
            k.Einheit = txtEinheit.Text.Trim();
            k.Sprache = DruckSprache.Sprachen[sprache.Index].Kuerzel;
        }
    }
}
