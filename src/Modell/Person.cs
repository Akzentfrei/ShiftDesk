namespace ShiftDesk.Modell
{
    /// <summary>Eine Person der Wache, wie sie in der Personen-Tabelle erfasst ist.</summary>
    public class Person
    {
        /// <summary>Militärischer Grad (z. B. «Rekr», «Wm»), aus der Liste <see cref="Konfiguration.Grade"/>.</summary>
        public string Grad = "";

        /// <summary>Name, wie er auf dem Plan steht.</summary>
        public string Name = "";

        /// <summary>Name der <see cref="Modell.Funktion"/>; bestimmt die erlaubten Posten.</summary>
        public string Funktion = "";

        /// <summary>
        /// Name der <see cref="Modell.Gruppe"/>, die die Ruhezeit bestimmt. Leer = keine Ruhezeit
        /// (der Wacht Kdt Stv braucht keine, er ruht im Gegenteil zum Wacht Kdt).
        /// </summary>
        public string Gruppe = "";

        /// <summary>
        /// Fahrer sind nur auf Posten vom Typ <see cref="PostenTyp.Fahrer"/> oder in Ruhe,
        /// bekommen keine Essensstunde (sie essen in der Ruhezeit) und sind im Plan mit einem Auto markiert.
        /// </summary>
        public bool Fahrer;

        /// <summary>Grad und Name für Hinweise und Listen, z. B. «Wm Meier».</summary>
        public string Anzeigename
        {
            get { return (Grad + " " + Name).Trim(); }
        }
    }
}
