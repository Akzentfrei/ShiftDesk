using System.Globalization;
using System.Linq;

namespace ShiftDesk.Ausgabe
{
    /// <summary>
    /// Texte des Ausdrucks in einer Sprache. Namen von Posten, Funktionen und Personen bleiben so,
    /// wie sie erfasst sind; übersetzt werden nur die festen Beschriftungen des Formulars.
    /// </summary>
    public class DruckSprache
    {
        /// <summary>Kürzel, wie es in <see cref="Modell.Konfiguration.Sprache"/> steht («de», «fr», «it», «en»).</summary>
        public string Kuerzel;

        /// <summary>Name der Sprache für die Auswahlliste, z. B. «Français».</summary>
        public string Name;

        /// <summary>Kultur für Wochentage und Monatsnamen.</summary>
        public CultureInfo Kultur;

        /// <summary>Datumsformat für ein ganzes Tagesblatt, z. B. «dddd, dd.MM.yyyy».</summary>
        public string DatumLang;

        /// <summary>Beschriftungen im Kopf des Formulars.</summary>
        public string Titel, Einheit, Fuer, Datum, JedenTag, Legende, Zeit, Wer;

        /// <summary>Erklärung der Zeichen unter der Tabelle und Fusszeile («{0}» = Datum bzw. Seitenzahlen).</summary>
        public string Zeichen, Ruhezeit, OhnePosten, EssenVor, EssenNach, EssenRuhe, EssenReserve, Draussen, Fahrer, NichtEingeteilt, Erstellt, Seite;

        /// <summary>Alle Sprachen des Ausdrucks, Deutsch zuerst.</summary>
        public static readonly DruckSprache[] Sprachen =
        {
            new DruckSprache { Kuerzel = "de", Name = "Deutsch", Kultur = new CultureInfo("de-CH"), DatumLang = "dddd, dd.MM.yyyy",
                Titel = "Ablösungsliste", Einheit = "Einheit", Fuer = "Für", Datum = "Datum", JedenTag = "Jeden Tag", Legende = "Legende", Zeit = "Zeit", Wer = "Wer",
                Zeichen = "Zeichen", Ruhezeit = "Ruhezeit", OhnePosten = "ohne Posten", EssenVor = "Essen vor der Ablösung", EssenNach = "Essen nach der Ablösung", EssenRuhe = "Essen in der Ruhezeit", EssenReserve = "Essen, Reserve löst ab", Draussen = "Draussen", Fahrer = "Fahrer",
                NichtEingeteilt = "Nicht eingeteilt", Erstellt = "Erstellt mit ShiftDesk am {0}", Seite = "Seite {0} von {1}" },
            new DruckSprache { Kuerzel = "fr", Name = "Français", Kultur = new CultureInfo("fr-CH"), DatumLang = "dddd dd.MM.yyyy",
                Titel = "Liste de relève", Einheit = "Unité", Fuer = "Pour", Datum = "Date", JedenTag = "Chaque jour", Legende = "Légende", Zeit = "Heure", Wer = "Qui",
                Zeichen = "Signes", Ruhezeit = "Repos", OhnePosten = "sans poste", EssenVor = "Repas avant la relève", EssenNach = "Repas après la relève", EssenRuhe = "Repas pendant le repos", EssenReserve = "Repas, la réserve relève", Draussen = "À l'extérieur", Fahrer = "Chauffeur",
                NichtEingeteilt = "Non engagés", Erstellt = "Créé avec ShiftDesk le {0}", Seite = "Page {0} sur {1}" },
            new DruckSprache { Kuerzel = "it", Name = "Italiano", Kultur = new CultureInfo("it-CH"), DatumLang = "dddd dd.MM.yyyy",
                Titel = "Lista dei turni", Einheit = "Unità", Fuer = "Per", Datum = "Data", JedenTag = "Ogni giorno", Legende = "Legenda", Zeit = "Ora", Wer = "Chi",
                Zeichen = "Simboli", Ruhezeit = "Riposo", OhnePosten = "senza posto", EssenVor = "Pasto prima del cambio", EssenNach = "Pasto dopo il cambio", EssenRuhe = "Pasto durante il riposo", EssenReserve = "Pasto, la riserva dà il cambio", Draussen = "All'esterno", Fahrer = "Autista",
                NichtEingeteilt = "Non impiegati", Erstellt = "Creato con ShiftDesk il {0}", Seite = "Pagina {0} di {1}" },
            new DruckSprache { Kuerzel = "en", Name = "English", Kultur = new CultureInfo("en-GB"), DatumLang = "dddd, dd.MM.yyyy",
                Titel = "Relief Roster", Einheit = "Unit", Fuer = "For", Datum = "Date", JedenTag = "Every day", Legende = "Legend", Zeit = "Time", Wer = "Who",
                Zeichen = "Symbols", Ruhezeit = "Rest", OhnePosten = "no post", EssenVor = "Meal before relief", EssenNach = "Meal after relief", EssenRuhe = "Meal during rest", EssenReserve = "Meal, reserve relieves", Draussen = "Outdoors", Fahrer = "Driver",
                NichtEingeteilt = "Not assigned", Erstellt = "Created with ShiftDesk on {0}", Seite = "Page {0} of {1}" },
        };

        /// <summary>Die Sprache zu einem Kürzel; unbekannte Kürzel ergeben Deutsch.</summary>
        public static DruckSprache In(string kuerzel)
        {
            return Sprachen.FirstOrDefault(s => s.Kuerzel == kuerzel) ?? Sprachen[0];
        }
    }
}
