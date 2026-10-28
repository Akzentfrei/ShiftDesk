namespace ShiftDesk.Modell
{
    /// <summary>
    /// Eine Ruhegruppe: alle Personen dieser Gruppe ruhen jeden Tag im selben Zeitfenster,
    /// auch über Mitternacht (z. B. 23:00 bis 07:00).
    /// </summary>
    public class Gruppe
    {
        /// <summary>Name der Gruppe, wie er bei den Personen ausgewählt wird.</summary>
        public string Name = "";

        /// <summary>Erste Ruhestunde (0–23).</summary>
        public int RuheVon;

        /// <summary>Ende der Ruhe (0–23, exklusiv). Gleich wie <see cref="RuheVon"/> = keine Ruhezeit.</summary>
        public int RuheBis;

        /// <summary>Ruht die Gruppe in dieser Stunde des Tages?</summary>
        /// <param name="stunde">Stunde des Tages (0–23).</param>
        public bool RuhtUm(int stunde)
        {
            return RuheVon != RuheBis && Zeit.InFenster(stunde, RuheVon, RuheBis);
        }
    }
}
