using System;

namespace ShiftDesk.Oberflaeche
{
    /// <summary>Ein Befehl im Bereich «Datei» der <see cref="Seitenleiste"/> (Neu, Öffnen, Speichern …).</summary>
    public class Dateibefehl
    {
        /// <summary>Beschriftung.</summary>
        public readonly string Text;

        /// <summary>Lucide-Symbol; dient auch zum Wiederfinden (<see cref="Seitenleiste.BefehlEntfernen"/>).</summary>
        public readonly string Icon;

        /// <summary>Was der Befehl tut.</summary>
        public readonly Action Aktion;

        /// <param name="text">Beschriftung.</param>
        /// <param name="icon">Lucide-Symbol.</param>
        /// <param name="aktion">Was der Befehl tut.</param>
        public Dateibefehl(string text, string icon, Action aktion)
        {
            Text = text;
            Icon = icon;
            Aktion = aktion;
        }
    }
}
