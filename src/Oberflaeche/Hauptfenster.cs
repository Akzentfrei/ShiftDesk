using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ShiftDesk.Einrichtung;
using ShiftDesk.Modell;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Dialoge;
using ShiftDesk.Oberflaeche.Seiten;
using ShiftDesk.Oberflaeche.Steuerelemente;
using ShiftDesk.Oberflaeche.Tabellen;

namespace ShiftDesk.Oberflaeche
{
    /// <summary>
    /// Das Hauptfenster: Seitenleiste links, rechts die gewählte <see cref="Seite"/>. Kümmert sich um das
    /// Wechseln der Seiten, die Datei-Befehle und das Zusammenführen der Eingaben aller Seiten zu einer
    /// <see cref="Konfiguration"/>.
    /// </summary>
    /// <remarks>
    /// Beim Schliessen werden die Eingaben nach <see cref="Speicherorte.LetzteEingaben"/> gespeichert und beim
    /// nächsten Start wieder geladen; beim allerersten Start erscheint das Beispiel.
    /// </remarks>
    public class Hauptfenster : Form
    {
        readonly Panel inhalt = new Panel { Dock = DockStyle.Fill };
        readonly Uebergang uebergang = new Uebergang();
        readonly Rueckmeldung rueckmeldung = new Rueckmeldung();   // z. B. «Gespeichert»
        readonly Seitenleiste seitenleiste;

        readonly StammdatenSeite stammdaten;
        readonly PersonenSeite personen;
        readonly GruppenSeite gruppen;
        readonly PostenSeite posten;
        readonly EssenSeite essen;
        readonly PlanSeite planSeite;
        readonly StatistikSeite statistik;
        readonly List<Seite> seiten;

        /// <summary>Die geöffnete bzw. zuletzt gespeicherte .shiftdesk-Datei, null = noch keine.</summary>
        string dateiPfad;

        /// <param name="datei">.shiftdesk-Datei, die gleich geöffnet wird (Doppelklick im Explorer), sonst null.</param>
        public Hauptfenster(string datei = null)
        {
            Text = "ShiftDesk";
            Font = Schrift.Text;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            var bildschirm = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(Thema.S(1440), bildschirm.Width), Math.Min(Thema.S(920), bildschirm.Height));
            MinimumSize = new Size(Math.Min(Thema.S(800), bildschirm.Width), Math.Min(Thema.S(560), bildschirm.Height));
            StartPosition = FormStartPosition.CenterScreen;
            FensterZustand.Wiederherstellen(this);

            SuspendLayout();
            stammdaten = new StammdatenSeite();
            gruppen = new GruppenSeite();
            personen = new PersonenSeite(stammdaten.Grade, stammdaten.Funktionen, gruppen.Tabelle);
            posten = new PostenSeite(stammdaten.Funktionen);
            essen = new EssenSeite();
            planSeite = new PlanSeite(Einlesen);
            statistik = new StatistikSeite();
            seiten = new List<Seite> { stammdaten, personen, gruppen, posten, essen, planSeite, statistik };
            UmbenennenNachfuehren();
            planSeite.PlanErstellt += statistik.Zeigen;
            foreach (var s in seiten)
            {
                s.Visible = false;
                inhalt.Controls.Add(s);
            }
            seitenleiste = new Seitenleiste(seiten, Dateibefehle(), Zeigen);
            Controls.Add(rueckmeldung);
            Controls.Add(uebergang);
            Controls.Add(inhalt);
            Controls.Add(seitenleiste);
            Farbwechsel.Bei(() => { BackColor = Thema.Hintergrund; inhalt.BackColor = Thema.Hintergrund; });
            ResumeLayout(false);

            Anzeigen(LetzteEingabenLaden() ?? Vorlagen.Beispiel());
            if (datei != null) DateiOeffnen(datei);
            Zeigen(personen);

            EventHandler themaGeaendert = (s, e) =>
            {
                if (IsHandleCreated) BeginInvoke(new Action(ThemaAnwenden));
            };
            Thema.Geaendert += themaGeaendert;
            HandleCreated += (s, e) => ThemaAnwenden();
            Resize += (s, e) => seitenleiste.BreiteAnpassen();
            KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.S) { Speichern(false); e.SuppressKeyPress = true; }
                planSeite.Taste(e);
            };
            FormClosing += (s, e) =>
            {
                Thema.Geaendert -= themaGeaendert;
                try { KonfigurationsDatei.Speichern(Einlesen(), Speicherorte.LetzteEingaben); }
                catch { }
                FensterZustand.Merken(this);
            };
        }

        /// <summary>
        /// Wird eine Funktion, ein Grad oder eine Gruppe umbenannt, ändert sich der Name auch bei den Personen
        /// (und bei Funktionen in den erlaubten Funktionen der Posten).
        /// </summary>
        void UmbenennenNachfuehren()
        {
            stammdaten.Funktionen.Umbenannt += (z, alt, neu) =>
            {
                personen.Tabelle.Ersetzen("Funktion", alt, neu);
                posten.FunktionUmbenennen(alt, neu);
            };
            stammdaten.Grade.Umbenannt += (z, alt, neu) => personen.Tabelle.Ersetzen("Grad", alt, neu);
            gruppen.Tabelle.Umbenannt += (z, alt, neu) => { if (z.OwningColumn.Name == "Name") personen.Tabelle.Ersetzen("Gruppe", alt, neu); };
        }

        void ThemaAnwenden()
        {
            Farbwechsel.Anwenden();
            Thema.Titelleiste(this);
            Invalidate(true);
        }

        // ---------------------------------------------------------------- Seiten

        /// <summary>Wechselt zur Seite: das alte Bild wird festgehalten, darunter umgeschaltet und dann weich übergeblendet.</summary>
        void Zeigen(Seite ziel)
        {
            EditorenSchliessen();
            var alt = seiten.FirstOrDefault(s => s.Visible);
            if (alt == ziel) return;

            bool animieren = alt != null && Visible && WindowState != FormWindowState.Minimized && inhalt.Width > 0 && inhalt.Height > 0;
            if (animieren) uebergang.Abdecken(Schnappschuss(), inhalt.Bounds);

            inhalt.SuspendLayout();
            ziel.Visible = true;
            seitenleiste.Markieren(ziel);
            foreach (var s in seiten)
                if (s != ziel) s.Visible = false;
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

        IEnumerable<Tabelle> AlleTabellen
        {
            get { return seiten.SelectMany(s => s.Tabellen); }
        }

        /// <summary>Schliesst offene Eingabefelder in allen Tabellen.</summary>
        /// <param name="uebernehmen">Den eingegebenen Text übernehmen (sonst verwerfen).</param>
        void EditorenSchliessen(bool uebernehmen = true)
        {
            foreach (var t in AlleTabellen) t.Schliessen(uebernehmen);
        }

        // ---------------------------------------------------------------- Daten <-> Oberfläche

        /// <summary>Zeigt eine Konfiguration auf allen Seiten; ein bisheriger Plan und seine Statistik verfallen.</summary>
        void Anzeigen(Konfiguration k)
        {
            EditorenSchliessen(false);
            foreach (var s in seiten) s.Anzeigen(k);
            foreach (var t in AlleTabellen) t.Zuruecksetzen();   // keine automatisch ausgewählte erste Zeile
            Titel();
        }

        /// <summary>Die Eingaben aller Seiten als neue Konfiguration.</summary>
        Konfiguration Einlesen()
        {
            EditorenSchliessen();
            var k = new Konfiguration();
            foreach (var s in seiten) s.Uebernehmen(k);
            return k;
        }

        /// <summary>Die Eingaben vom letzten Mal; null, wenn es keine gibt oder sie unlesbar sind.</summary>
        static Konfiguration LetzteEingabenLaden()
        {
            string pfad = Speicherorte.LetzteEingaben;
            try
            {
                if (File.Exists(pfad)) return KonfigurationsDatei.Laden(pfad);
            }
            catch
            {
                // nicht lesbar: aufheben, bevor sie beim Schliessen überschrieben wird
                try { File.Copy(pfad, Path.ChangeExtension(pfad, ".defekt.xml"), true); }
                catch { }
            }
            return null;
        }

        // ---------------------------------------------------------------- Dateien

        /// <summary>Die Befehle im Bereich «Datei» der Seitenleiste.</summary>
        IEnumerable<Dateibefehl> Dateibefehle()
        {
            yield return new Dateibefehl("Neu", "file-plus", () =>
            {
                if (!Rueckfrage.Fragen(this, "Alle Eingaben löschen und neu beginnen?",
                    "Was nicht als Datei gespeichert ist, geht verloren.", "Alles löschen", "trash-2")) return;
                dateiPfad = null;
                Anzeigen(Vorlagen.Neu());
            });
            yield return new Dateibefehl("Beispiel laden", "rotate-ccw", () =>
            {
                if (!Rueckfrage.Fragen(this, "Alle Eingaben durch das Beispiel ersetzen?",
                    "Was nicht als Datei gespeichert ist, geht verloren.", "Ersetzen")) return;
                dateiPfad = null;
                Anzeigen(Vorlagen.Beispiel());
            });
            yield return new Dateibefehl("Öffnen…", "folder-open", Oeffnen);
            yield return new Dateibefehl("Speichern", "save", () => Speichern(false));
            yield return new Dateibefehl("Speichern unter…", "save-all", () => Speichern(true));
            if (!Installation.LaeuftInstalliert)
                yield return new Dateibefehl("Auf diesem PC installieren", "download", Installieren);
        }

        void Oeffnen()
        {
            using (var dlg = new OpenFileDialog { Filter = "ShiftDesk-Datei (*.shiftdesk)|*.shiftdesk|Alle Dateien|*.*" })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    DateiOeffnen(dlg.FileName);
            }
        }

        /// <summary>Eine .shiftdesk-Datei laden (Menü «Öffnen…» oder Doppelklick im Explorer).</summary>
        void DateiOeffnen(string pfad)
        {
            try
            {
                var k = KonfigurationsDatei.Laden(pfad);
                dateiPfad = pfad;
                Anzeigen(k);
            }
            catch (Exception ex)
            {
                Meldung.Fehler(this, "Datei konnte nicht geöffnet werden", ex.Message);
            }
        }

        /// <summary>Speichert in die geöffnete Datei; ohne Datei oder mit <paramref name="neuerName"/> nach Rückfrage des Namens.</summary>
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
                KonfigurationsDatei.Speichern(Einlesen(), pfad);
                dateiPfad = pfad;   // erst nach erfolgreichem Speichern
                Titel();
                rueckmeldung.Zeigen("Gespeichert: " + Path.GetFileName(pfad), "circle-check", Thema.Erfolg);
            }
            catch (Exception ex)
            {
                Meldung.Fehler(this, "Speichern fehlgeschlagen", ex.Message);
            }
        }

        /// <summary>«Auf diesem PC installieren»; der Befehl verschwindet nach erfolgreicher Installation.</summary>
        void Installieren()
        {
            if (!Installation.Installieren(false)) return;
            Installation.BeimBeendenWegraeumen();
            seitenleiste.BefehlEntfernen("download");
        }

        /// <summary>Fenstertitel mit dem Namen der geöffneten Datei.</summary>
        void Titel()
        {
            Text = dateiPfad == null ? "ShiftDesk" : Path.GetFileNameWithoutExtension(dateiPfad) + " – ShiftDesk";
        }
    }
}
