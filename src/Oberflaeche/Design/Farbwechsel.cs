using System;
using System.Collections.Generic;

namespace ShiftDesk.Oberflaeche.Design
{
    /// <summary>
    /// Sammelt alles, was beim Wechsel hell/dunkel neu eingefärbt werden muss (Hintergründe von Panels, Labels,
    /// Tabellen …). Selbst gezeichnete Steuerelemente lesen die Farben beim Zeichnen und brauchen das nicht.
    /// </summary>
    public static class Farbwechsel
    {
        static readonly List<Action> aktionen = new List<Action>();

        /// <summary>Färbt jetzt ein und merkt sich die Aktion für jeden späteren Wechsel.</summary>
        public static void Bei(Action einfaerben)
        {
            aktionen.Add(einfaerben);
            einfaerben();
        }

        /// <summary>Färbt alles neu ein (im UI-Thread aufrufen).</summary>
        public static void Anwenden()
        {
            foreach (var a in aktionen) a();
        }
    }
}
