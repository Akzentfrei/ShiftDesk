using System.Linq;

namespace ShiftDesk.Modell
{
    /// <summary>Hilfen für Stunden des Tages und tägliche Zeitfenster.</summary>
    public static class Zeit
    {
        /// <summary>
        /// Liegt die Stunde im Zeitfenster <paramref name="von"/> bis <paramref name="bis"/>?
        /// Das Fenster darf über Mitternacht gehen; <paramref name="bis"/> zählt nicht mehr dazu.
        /// <c>von == bis</c> bedeutet den ganzen Tag.
        /// </summary>
        public static bool InFenster(int stunde, int von, int bis)
        {
            if (von == bis) return true;
            if (von < bis) return stunde >= von && stunde < bis;
            return stunde >= von || stunde < bis;
        }

        /// <summary>Die Uhrzeiten «00:00» bis «23:00» (für Auswahllisten).</summary>
        public static readonly string[] Uhrzeiten = Enumerable.Range(0, 24).Select(h => Text(h)).ToArray();

        /// <summary>Stunde als Uhrzeit, z. B. 7 → «07:00».</summary>
        public static string Text(int stunde)
        {
            return stunde.ToString("00") + ":00";
        }
    }
}
