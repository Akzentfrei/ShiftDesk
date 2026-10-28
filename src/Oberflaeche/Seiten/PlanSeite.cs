using System;
using System.Drawing;
using System.Windows.Forms;
using ShiftDesk.Modell;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Dialoge;
using ShiftDesk.Oberflaeche.Steuerelemente;
using ShiftDesk.Planung;

namespace ShiftDesk.Oberflaeche.Seiten
{
    /// <summary>
    /// Seite «Plan»: Einstellungen, «Plan erstellen» und die Vorschau mit Werkzeugleiste (Zoom, Seiten, Ausgabe).
    /// </summary>
    /// <remarks>
    /// Breites Fenster: Einstellungen als Spalte links, Vorschau rechts daneben (am meisten Platz fürs Blatt).
    /// Schmales Fenster: Einstellungen oben (ganz, solange die Vorschau genug Platz behält), Vorschau darunter.
    /// </remarks>
    public class PlanSeite : Seite
    {
        readonly Func<Konfiguration> einlesen;
        readonly PlanEinstellungen einstellungen = new PlanEinstellungen();
        readonly PlanVorschau vorschau = new PlanVorschau { Dock = DockStyle.Fill };
        readonly PlanWerkzeugleiste werkzeugleiste;
        readonly Panel koerper, rechts;
        Plan plan;
        int seite;

        /// <summary>Ein neuer Plan wurde erstellt (für die Statistik).</summary>
        public event Action<Plan> PlanErstellt;

        /// <param name="einlesen">Liefert alle aktuellen Eingaben (alle Seiten) für einen neuen Plan.</param>
        public PlanSeite(Func<Konfiguration> einlesen)
            : base("Plan", "calendar-days",
                "Zeitraum wählen und auf «Plan erstellen» klicken. Jeder Klick ergibt eine neue, gleich faire Variante; Hinweise zeigt ein Klick auf den Status. Zoomen mit dem Mausrad oder Doppelklick, den vergrösserten Plan mit der Maus verschieben.")
        {
            this.einlesen = einlesen;
            werkzeugleiste = new PlanWerkzeugleiste(vorschau.Einpassen, Blaettern,
                () => Ausgeben(PlanAusgabe.Drucken), () => Ausgeben(PlanAusgabe.AlsPdf), () => Ausgeben(PlanAusgabe.AlsExcel));
            einstellungen.ArtGeaendert += () => { if (koerper != null) koerper.PerformLayout(); };
            einstellungen.DruckAngabenGeaendert += DruckAngabenUebernehmen;
            vorschau.ZoomGeaendert += () => werkzeugleiste.ZoomAnzeigen(vorschau.Zoom);

            var abstand = new Panel { Dock = DockStyle.Top, Height = Thema.S(12) };
            rechts = new Panel();
            rechts.Controls.Add(vorschau);
            rechts.Controls.Add(abstand);
            rechts.Controls.Add(werkzeugleiste);
            koerper = new Panel { Dock = DockStyle.Fill };
            koerper.Controls.Add(einstellungen);
            koerper.Controls.Add(rechts);
            koerper.Layout += (s, e) => Anordnen();
            Farbwechsel.Bei(() => koerper.BackColor = rechts.BackColor = abstand.BackColor = Thema.Hintergrund);

            Aufbauen(koerper, Knopf.Mit("Plan erstellen", "sparkles", Erstellen, true));
        }

        /// <summary>Zeigt die Einstellungen; ein bisheriger Plan gilt nicht mehr.</summary>
        public override void Anzeigen(Konfiguration k)
        {
            einstellungen.Anzeigen(k);
            plan = null;
            seite = 0;
            StatusAktualisieren();
        }

        public override void Uebernehmen(Konfiguration k)
        {
            einstellungen.Uebernehmen(k);
        }

        /// <summary>Tastenkürzel der Vorschau (vom Hauptfenster weitergereicht): Ctrl + Plus / Minus zoomt, Ctrl + 0 zeigt die ganze Seite.</summary>
        public void Taste(KeyEventArgs e)
        {
            if (!e.Control || plan == null || !Visible) return;
            if (e.KeyCode == Keys.Add || e.KeyCode == Keys.Oemplus) { vorschau.Hineinzoomen(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Subtract || e.KeyCode == Keys.OemMinus) { vorschau.Herauszoomen(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0) { vorschau.Einpassen(); e.SuppressKeyPress = true; }
        }

        /// <summary>Erstellt einen neuen Plan; jeder Klick ergibt eine neue Variante (anderer Zufall).</summary>
        void Erstellen()
        {
            var fenster = FindForm();
            Plan neu;
            fenster.Cursor = Cursors.WaitCursor;
            try
            {
                neu = Planer.Erstelle(einlesen(), Environment.TickCount);
            }
            catch (Exception ex)
            {
                fenster.Cursor = Cursors.Default;
                Meldung.Warnung(fenster, "Plan kann nicht erstellt werden", ex.Message);
                return;
            }
            fenster.Cursor = Cursors.Default;
            plan = neu;
            seite = 0;
            var h = PlanErstellt;
            if (h != null) h(plan);
            StatusAktualisieren();
        }

        /// <summary>Name, Einheit und Sprache gelten sofort für den vorhandenen Plan (sie ändern die Einteilung nicht).</summary>
        void DruckAngabenUebernehmen()
        {
            if (plan == null) return;
            einstellungen.DruckAngabenUebernehmen(plan.Cfg);
            vorschau.DruckAngabenGeaendert();
        }

        void Blaettern(int richtung)
        {
            if (plan == null) return;
            seite = Math.Max(0, Math.Min(plan.Seiten - 1, seite + richtung));
            StatusAktualisieren();
        }

        void StatusAktualisieren()
        {
            vorschau.Zeigen(plan, seite);
            werkzeugleiste.Anzeigen(plan, seite, vorschau.Zoom);
        }

        /// <summary>Drucken, PDF oder Excel; ohne Plan ein Hinweis.</summary>
        void Ausgeben(Action<Form, Plan> ausgabe)
        {
            var fenster = FindForm();
            if (plan == null)
            {
                Meldung.Info(fenster, "Noch kein Plan", "Bitte zuerst auf «Plan erstellen» klicken.");
                return;
            }
            ausgabe(fenster, plan);
        }

        void Anordnen()
        {
            int w = koerper.ClientSize.Width, h = koerper.ClientSize.Height;
            if (w <= 0 || h <= 0) return;
            bool seitlich = w >= Thema.S(980);
            einstellungen.Anordnen(seitlich);
            if (seitlich)
            {
                int b = PlanEinstellungen.SpaltenBreite, abstand = Thema.S(20);
                einstellungen.Bounds = new Rectangle(0, 0, b, h);
                rechts.Bounds = new Rectangle(b + abstand, 0, w - b - abstand, h);
            }
            else
            {
                // ganz zeigen (es gibt keine Bildlaufleiste), solange der Vorschau genug Platz bleibt
                int hoehe = Math.Min(einstellungen.NoetigeHoehe(w), Math.Max(Thema.S(150), h - Thema.S(240)));
                einstellungen.Bounds = new Rectangle(0, 0, w, hoehe);
                rechts.Bounds = new Rectangle(0, hoehe, w, h - hoehe);
            }
        }
    }
}
