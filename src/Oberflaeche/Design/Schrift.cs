using System.Drawing;

namespace ShiftDesk.Oberflaeche.Design
{
    /// <summary>Schriften: eine feste Abstufung für die ganze Oberfläche (nicht pro Steuerelement neu erzeugen).</summary>
    public static class Schrift
    {
        /// <summary>Titel einer Seite («Personen», «Plan» …).</summary>
        public static readonly Font Seitentitel = new Font("Segoe UI Semibold", 19f);

        /// <summary>Titel von Bereichen und Dialogen.</summary>
        public static readonly Font Titel = new Font("Segoe UI Semibold", 12.5f);

        /// <summary>Hervorgehobener Text (aktiver Eintrag, Popup-Titel).</summary>
        public static readonly Font Ueberschrift = new Font("Segoe UI Semibold", 10f);

        /// <summary>Eingabefelder, Listen und Tabellenzellen.</summary>
        public static readonly Font Eingabe = new Font("Segoe UI", 10f);

        /// <summary>Fliesstext.</summary>
        public static readonly Font Text = new Font("Segoe UI", 9.5f);

        /// <summary>Knöpfe und Beschriftungen in Leisten.</summary>
        public static readonly Font Fett = new Font("Segoe UI Semibold", 9.5f);

        /// <summary>Kurzinfos und Nebentexte.</summary>
        public static readonly Font Klein = new Font("Segoe UI", 9f);

        /// <summary>Spaltenköpfe und Status.</summary>
        public static readonly Font KleinFett = new Font("Segoe UI Semibold", 9f);

        /// <summary>Beschriftung über Eingabefeldern.</summary>
        public static readonly Font Beschriftung = new Font("Segoe UI Semibold", 8.5f);

        /// <summary>Abschnittsüberschriften in Grossbuchstaben.</summary>
        public static readonly Font Abschnitt = new Font("Segoe UI Semibold", 7.5f);
    }
}
