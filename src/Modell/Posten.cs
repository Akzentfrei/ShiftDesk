using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ShiftDesk.Modell
{
    /// <summary>Art eines Postens.</summary>
    public enum PostenTyp
    {
        /// <summary>Gewöhnlicher Posten.</summary>
        Normal,

        /// <summary>Gibt es nur noch, damit ältere Dateien gelesen werden können (heute: Funktion «Wacht Kdt»).</summary>
        Wachtkdt,

        /// <summary>Bekommt alle übrigen Leute (z. B. Pikett); hat keine feste Anzahl Plätze.</summary>
        Auffang,

        /// <summary>
        /// Posten für Fahrer (z. B. Presto): der erste Platz ist für einen Fahrer reserviert, die weiteren bekommen
        /// Fahrer, wenn welche da sind, sonst andere Leute. Fahrer selbst sind nur auf solchen Posten oder in Ruhe.
        /// </summary>
        Fahrer,
    }

    /// <summary>Ein Posten, der während seiner Öffnungszeit mit <see cref="Anzahl"/> Personen besetzt wird.</summary>
    public class Posten
    {
        /// <summary>Name des Postens, wie er in der Legende steht.</summary>
        public string Name = "";

        /// <summary>Farbe als ARGB-Wert (<see cref="Color.ToArgb"/>); der Ausdruck zeigt sie stark aufgehellt.</summary>
        public int Farbe = Color.LightGray.ToArgb();

        /// <summary>Anzahl Plätze, die gleichzeitig besetzt sein sollen.</summary>
        public int Anzahl = 1;

        /// <summary>Beginn der Öffnungszeit (Stunde 0–23).</summary>
        public int OffenVon;

        /// <summary>Ende der Öffnungszeit (exklusiv). Gleich wie <see cref="OffenVon"/> = immer offen.</summary>
        public int OffenBis;

        /// <summary>Art des Postens.</summary>
        public PostenTyp Typ = PostenTyp.Normal;

        /// <summary>Auf diesem Posten kann man essen: hier bekommen die Leute ihre Essensstunde.</summary>
        public bool EssenMoeglich;

        /// <summary>Posten im Freien: niemand soll zu lange am Stück draussen sein.</summary>
        public bool Draussen;

        /// <summary>Namen der Funktionen, die den Posten besetzen dürfen; leer = alle Funktionen.</summary>
        public List<string> Funktionen = new List<string>();

        /// <summary>
        /// Soll die Besetzung möglichst am Stück bleiben? Gilt für den Posten «Wacht Kdt» und jeden Posten,
        /// der nur für Kdt-Funktionen ist: Wacht Kdt und Stv wechseln nicht jede Ablösung.
        /// </summary>
        public bool AmStueck
        {
            get
            {
                return string.Equals((Name ?? "").Trim(), Konfiguration.Pflichtposten[0], StringComparison.OrdinalIgnoreCase)
                    || (Funktionen.Count > 0 && Funktionen.All(f => f.IndexOf("Kdt", StringComparison.OrdinalIgnoreCase) >= 0));
            }
        }

        /// <summary>Ist der Posten in dieser Stunde des Tages offen?</summary>
        public bool OffenUm(int stunde)
        {
            return Zeit.InFenster(stunde, OffenVon, OffenBis);
        }

        /// <summary>Ist der Posten rund um die Uhr offen?</summary>
        public bool Durchgehend
        {
            get { return OffenVon == OffenBis; }
        }

        /// <summary>Darf eine Person mit dieser Funktion den Posten besetzen?</summary>
        public bool Erlaubt(string funktion)
        {
            return Funktionen.Count == 0 || Funktionen.Contains(funktion);
        }
    }
}
