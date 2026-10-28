using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ShiftDesk.Oberflaeche.Tabellen
{
    /// <summary>Wie eine Spalte der <see cref="Tabelle"/> bearbeitet und gezeichnet wird.</summary>
    public enum SpaltenArt
    {
        /// <summary>Freier Text im Eingabefeld.</summary>
        Text,

        /// <summary>Nur Ziffern.</summary>
        Zahl,

        /// <summary>Ein Wert aus einer Liste (<see cref="SpaltenInfo.Optionen"/>).</summary>
        Auswahl,

        /// <summary>Mehrere Werte aus einer Liste, mit Häkchen; als <c>List&lt;string&gt;</c> im Zellen-Tag.</summary>
        Mehrfach,

        /// <summary>Farbe aus Palette oder Farbmischer; als <see cref="Color"/> im Zellen-Tag.</summary>
        Farbe,

        /// <summary>Schiebeschalter (bool).</summary>
        Schalter,

        /// <summary>Knöpfe am Ende der Zeile (<see cref="SpaltenInfo.Knoepfe"/>).</summary>
        Aktionen,

        /// <summary>Nur Text, nicht bearbeitbar (Statistik).</summary>
        Anzeige,
    }

    /// <summary>Hängt an einer Tabellenspalte (<c>DataGridViewColumn.Tag</c>) und beschreibt sie.</summary>
    public class SpaltenInfo
    {
        /// <summary>Art der Spalte.</summary>
        public SpaltenArt Art;

        /// <summary>Werte für Auswahl- und Mehrfach-Spalten (bei jedem Aufklappen neu abgefragt).</summary>
        public Func<IEnumerable<string>> Optionen;

        /// <summary>Text für einen Wert, z. B. "" → «Keine»; null = der Wert selbst.</summary>
        public Func<string, string> Anzeige;

        /// <summary>Icon rechts im Feld, abhängig von der Zeile (null = keins), z. B. ein Auto bei Fahrern.</summary>
        public Func<DataGridViewRow, string> Symbol;

        /// <summary>Fester grauer Text statt Eingabe, abhängig von der Zeile (null = normal bearbeitbar).</summary>
        public Func<DataGridViewRow, string> FesterText;

        /// <summary>Die Knöpfe einer Aktionen-Spalte.</summary>
        public ZeilenKnopf[] Knoepfe;

        /// <summary>Text hinter dem Info-Zeichen im Spaltenkopf (null = kein Info-Zeichen).</summary>
        public string Erklaerung;

        /// <summary>Farbiges Quadrat vor dem Titel (Postenfarbe in der Statistik).</summary>
        public Color? Farbe;

        /// <summary>Behält die Mindestbreite, statt mit der Tabelle mitzuwachsen (Vorgabe für Schalter, Zahlen, Farben, Knöpfe).</summary>
        public bool Fest;

        /// <param name="art">Art der Spalte.</param>
        /// <param name="optionen">Werte für Auswahl- und Mehrfach-Spalten.</param>
        /// <param name="anzeige">Text für einen Wert, null = der Wert selbst.</param>
        public SpaltenInfo(SpaltenArt art, Func<IEnumerable<string>> optionen = null, Func<string, string> anzeige = null)
        {
            Art = art;
            Optionen = optionen;
            Anzeige = anzeige;
            Fest = art == SpaltenArt.Schalter || art == SpaltenArt.Zahl || art == SpaltenArt.Farbe || art == SpaltenArt.Aktionen;
        }

        /// <summary>Aktionen-Spalte mit diesen Knöpfen.</summary>
        public SpaltenInfo(params ZeilenKnopf[] knoepfe) : this(SpaltenArt.Aktionen)
        {
            Knoepfe = knoepfe;
        }
    }
}
