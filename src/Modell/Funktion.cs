namespace ShiftDesk.Modell
{
    /// <summary>
    /// Eine frei definierbare Funktion (z. B. «Wacht Kdt», «Wache», «Reserve»).
    /// Die Funktion einer Person bestimmt, welche Posten sie besetzen darf.
    /// </summary>
    public class Funktion
    {
        /// <summary>Name der Funktion, wie er bei Personen und Posten ausgewählt wird.</summary>
        public string Name = "";

        /// <summary>
        /// <c>false</c>: Personen mit dieser Funktion werden nicht eingeteilt und stehen nur unten
        /// auf dem Plan bei «Nicht eingeteilt» (z. B. Reserve).
        /// </summary>
        public bool ImPlan = true;
    }
}
