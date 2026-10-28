using System;
using System.Collections.Generic;
using ShiftDesk.Modell;

namespace ShiftDesk.Planung
{
    /// <summary>
    /// Ergebnis des <see cref="Planer"/>: für jede eingeteilte Person (Zeile) und jede Stunde der Posten,
    /// dazu Essensstunden, Blöcke, Zweierteams und Hinweise. Eine Seite des Ausdrucks zeigt 24 Stunden.
    /// </summary>
    public class Plan
    {
        /// <summary>Wert in <see cref="Z"/>: im Dienst, aber ohne Posten.</summary>
        public const int FREI = -1;

        /// <summary>Wert in <see cref="Z"/>: Ruhezeit.</summary>
        public const int RUHE = -2;

        /// <summary>Die Eingaben, aus denen der Plan entstanden ist (Name, Einheit und Sprache ändern sich mit der Plan-Seite).</summary>
        public Konfiguration Cfg;

        /// <summary>Erste Stunde des Plans.</summary>
        public DateTime Start;

        /// <summary>Länge des Plans in Stunden.</summary>
        public int Stunden;

        /// <summary>Eingeteilte Personen in der Reihenfolge des Plans (Fahrer zuunterst, sonst nach Funktion und Gruppe).</summary>
        public List<Person> Zeilen = new List<Person>();

        /// <summary>Je Zeile die Blocknummer: Zeilen mit gleichen erlaubten Posten bilden einen Block (dicke Linie dazwischen).</summary>
        public int[] Block;

        /// <summary>Je Zeile die Teamnummer: Zeilen mit gleicher Nummer bilden ein Zweierteam und sind wenn möglich zusammen eingeteilt.</summary>
        public int[] Team;

        /// <summary>[Zeile, Stunde] → Index in <see cref="Konfiguration.Posten"/>, <see cref="FREI"/> oder <see cref="RUHE"/>.</summary>
        public int[,] Z;

        /// <summary>[Zeile, Stunde] → isst in dieser Stunde (auf einem Posten mit Essen oder in der Ruhezeit).</summary>
        public bool[,] Essen;

        /// <summary>[Zeile, Stunde] → isst, und eine Reserve löst kurz ab (kein Posten mit Essen war frei).</summary>
        public bool[,] EssenAbloesung;

        /// <summary>Personen, deren Funktion nicht eingeteilt wird (z. B. Reserve); stehen unten auf dem Plan.</summary>
        public List<Person> NichtEingeteilt = new List<Person>();

        /// <summary>Hinweise zum Plan (unbesetzte Posten, zu lange draussen, fehlende Gruppen …).</summary>
        public List<string> Warnungen = new List<string>();

        /// <summary>Anzahl Seiten des Ausdrucks (je 24 Stunden).</summary>
        public int Seiten
        {
            get { return (Stunden + 23) / 24; }
        }

        /// <summary>Wie viele Stunden ist diese Zeile auf diesem Posten eingeteilt?</summary>
        public int StundenAuf(int zeile, int posten)
        {
            int c = 0;
            for (int t = 0; t < Stunden; t++)
                if (Z[zeile, t] == posten) c++;
            return c;
        }

        /// <summary>In wie vielen Stunden isst diese Zeile (mit und ohne Ablösung durch eine Reserve)?</summary>
        public int Essensstunden(int zeile)
        {
            int c = 0;
            for (int t = 0; t < Stunden; t++)
                if (Essen[zeile, t] || EssenAbloesung[zeile, t]) c++;
            return c;
        }
    }
}
