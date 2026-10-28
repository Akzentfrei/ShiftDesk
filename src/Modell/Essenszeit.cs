namespace ShiftDesk.Modell
{
    /// <summary>
    /// Ein Essensfenster (z. B. Mittagessen 10:00 bis 14:00). Jede Person, die das ganze Fenster
    /// im Dienst ist, bekommt darin eine Stunde zum Essen.
    /// </summary>
    public class Essenszeit
    {
        /// <summary>Bezeichnung, z. B. «Mittagessen».</summary>
        public string Name = "";

        /// <summary>Beginn des Fensters (Stunde 0–23).</summary>
        public int Von;

        /// <summary>Ende des Fensters (Stunde 0–23, exklusiv).</summary>
        public int Bis;
    }
}
