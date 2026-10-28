using System;

namespace ShiftDesk.Oberflaeche.Tabellen
{
    /// <summary>Symbolknopf am Ende jeder Zeile einer <see cref="Tabelle"/> (z. B. Verschieben, Entfernen).</summary>
    public class ZeilenKnopf
    {
        /// <summary>Lucide-Symbol.</summary>
        public string Icon;

        /// <summary>Was der Knopf tut; bekommt den Zeilenindex.</summary>
        public Action<int> Ausfuehren;

        /// <summary>Ist der Knopf in dieser Zeile möglich? null = immer (sonst grau und ohne Wirkung).</summary>
        public Func<int, bool> Bedingung;

        /// <summary>Rot beim Darüberfahren (Entfernen).</summary>
        public bool Gefahr;

        /// <param name="icon">Lucide-Symbol.</param>
        /// <param name="ausfuehren">Was der Knopf tut; bekommt den Zeilenindex.</param>
        /// <param name="bedingung">Ist der Knopf in einer Zeile möglich? null = immer.</param>
        public ZeilenKnopf(string icon, Action<int> ausfuehren, Func<int, bool> bedingung = null)
        {
            Icon = icon;
            Ausfuehren = ausfuehren;
            Bedingung = bedingung;
        }

        /// <summary>Ist der Knopf in dieser Zeile möglich?</summary>
        public bool Moeglich(int zeile)
        {
            return Bedingung == null || Bedingung(zeile);
        }
    }
}
