using System.Collections.Generic;
using System.Windows.Forms;
using ShiftDesk.Modell;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Steuerelemente;
using ShiftDesk.Oberflaeche.Tabellen;

namespace ShiftDesk.Oberflaeche.Seiten
{
    /// <summary>
    /// Eine Seite des Hauptfensters (in der Seitenleiste wählbar): Titelzeile mit Info-Knopf und Knöpfen,
    /// darunter der Inhalt. Im schmalen Fenster wird der Rand kleiner.
    /// </summary>
    /// <remarks>
    /// Jede Seite zeigt einen Teil der <see cref="Konfiguration"/> (<see cref="Anzeigen"/>) und schreibt ihn
    /// beim Speichern oder Erstellen des Plans wieder zurück (<see cref="Uebernehmen"/>).
    /// </remarks>
    public abstract class Seite : Panel
    {
        /// <summary>Titel der Seite (auch in der Seitenleiste).</summary>
        public readonly string Titel;

        /// <summary>Lucide-Symbol in der Seitenleiste.</summary>
        public readonly string Icon;

        readonly string info;

        /// <param name="titel">Titel der Seite.</param>
        /// <param name="icon">Symbol in der Seitenleiste.</param>
        /// <param name="info">Erklärung hinter dem Info-Knopf neben dem Titel.</param>
        protected Seite(string titel, string icon, string info)
        {
            Titel = titel;
            Icon = icon;
            this.info = info;
            Dock = DockStyle.Fill;
            Resize += (s, e) => RandAnpassen();
            RandAnpassen();
            Farbwechsel.Bei(() => BackColor = Thema.Hintergrund);
        }

        /// <summary>Setzt den Inhalt unter die Titelzeile; von der abgeleiteten Klasse am Ende ihres Konstruktors aufgerufen.</summary>
        /// <param name="inhalt">Füllt den Rest der Seite.</param>
        /// <param name="knoepfe">Knöpfe rechts in der Titelzeile.</param>
        protected void Aufbauen(Control inhalt, params Knopf[] knoepfe)
        {
            inhalt.Dock = DockStyle.Fill;
            Controls.Add(inhalt);
            Controls.Add(new Kopfzeile(Titel, info, Schrift.Seitentitel, Thema.S(62), Thema.S(14), knoepfe));
        }

        void RandAnpassen()
        {
            var p = Width < Thema.S(860)
                ? new Padding(Thema.S(10), Thema.S(8), Thema.S(10), Thema.S(10))
                : new Padding(Thema.S(16), Thema.S(12), Thema.S(16), Thema.S(14));
            if (Padding != p) Padding = p;
        }

        /// <summary>Die Tabellen der Seite (um offene Eingabefelder zu schliessen und die Auswahl zurückzusetzen).</summary>
        public virtual IEnumerable<Tabelle> Tabellen
        {
            get { yield break; }
        }

        /// <summary>Zeigt den Teil der Eingaben, der zu dieser Seite gehört.</summary>
        public virtual void Anzeigen(Konfiguration k)
        {
        }

        /// <summary>Schreibt die Eingaben dieser Seite in <paramref name="k"/>.</summary>
        public virtual void Uebernehmen(Konfiguration k)
        {
        }

        /// <summary>Spalte mit festen Uhrzeiten 00:00 bis 23:00 (Ruhezeiten, Öffnungszeiten, Essenszeiten).</summary>
        protected static SpaltenInfo Stunden()
        {
            return new SpaltenInfo(SpaltenArt.Auswahl, () => Zeit.Uhrzeiten) { Fest = true };
        }
    }
}
