using System;
using System.IO;

namespace ShiftDesk
{
    /// <summary>Wo ShiftDesk seine eigenen Daten ablegt (nicht die vom Benutzer gespeicherten .shiftdesk-Dateien).</summary>
    public static class Speicherorte
    {
        /// <summary>Ordner <c>%APPDATA%\ShiftDesk</c>.</summary>
        public static string Datenordner
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShiftDesk"); }
        }

        /// <summary>Die zuletzt verwendeten Eingaben: werden beim Schliessen gespeichert und beim Start wieder geladen.</summary>
        public static string LetzteEingaben
        {
            get { return Path.Combine(Datenordner, "letzte.xml"); }
        }

        /// <summary>Lage und Grösse des Hauptfensters beim letzten Schliessen.</summary>
        public static string Fensterlage
        {
            get { return Path.Combine(Datenordner, "fenster.txt"); }
        }
    }
}
