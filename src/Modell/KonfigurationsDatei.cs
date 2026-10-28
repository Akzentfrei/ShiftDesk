using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using ShiftDesk.Ausgabe;

namespace ShiftDesk.Modell
{
    /// <summary>
    /// Liest und schreibt eine <see cref="Konfiguration"/> als XML-Datei (.shiftdesk und die zuletzt
    /// verwendeten Eingaben). Ältere Dateiversionen werden beim Laden auf den aktuellen Stand gebracht.
    /// </summary>
    public static class KonfigurationsDatei
    {
        /// <summary>
        /// Speichert die Konfiguration. Es wird erst ganz in eine Hilfsdatei geschrieben und dann ausgetauscht:
        /// bricht das Schreiben ab (Absturz, voller Datenträger), bleibt die bisherige Datei unversehrt.
        /// </summary>
        /// <param name="k">Was gespeichert wird; <see cref="Konfiguration.Version"/> wird dabei auf den aktuellen Stand gesetzt.</param>
        /// <param name="pfad">Zieldatei; der Ordner wird bei Bedarf angelegt.</param>
        public static void Speichern(Konfiguration k, string pfad)
        {
            pfad = Path.GetFullPath(pfad);
            Directory.CreateDirectory(Path.GetDirectoryName(pfad));
            var ser = new XmlSerializer(typeof(Konfiguration));
            k.Zweck = null;
            k.Version = Konfiguration.AktuelleVersion;
            string neu = pfad + ".neu";
            using (var fs = File.Create(neu))
                ser.Serialize(fs, k);
            if (!File.Exists(pfad))
            {
                File.Move(neu, pfad);
                return;
            }
            try { File.Replace(neu, pfad, null); }
            catch (Exception ex)
            {
                // z. B. auf Netzlaufwerken oder USB-Sticks ohne NTFS
                if (!(ex is IOException || ex is PlatformNotSupportedException || ex is UnauthorizedAccessException)) throw;
                File.Copy(neu, pfad, true);
                File.Delete(neu);
            }
        }

        /// <summary>Lädt eine Datei und bringt ältere Versionen auf den aktuellen Stand.</summary>
        /// <exception cref="IOException">Die Datei lässt sich nicht lesen.</exception>
        /// <exception cref="InvalidOperationException">Die Datei ist keine gültige ShiftDesk-Datei.</exception>
        public static Konfiguration Laden(string pfad)
        {
            var ser = new XmlSerializer(typeof(Konfiguration));
            Konfiguration k;
            using (var fs = File.OpenRead(pfad))
                k = (Konfiguration)ser.Deserialize(fs);
            if (k.Version < 4)
            {
                if (!string.IsNullOrEmpty(k.Zweck) || !string.IsNullOrEmpty(k.Einheit))
                    k.Name = string.Join(" ", new[] { k.Zweck, k.Einheit }.Where(s => !string.IsNullOrEmpty(s)));
                k.Einheit = "";
            }
            k.Zweck = null;
            if (k.Einheit == null) k.Einheit = "";
            if (DruckSprache.Sprachen.All(x => x.Kuerzel != k.Sprache)) k.Sprache = "de";
            if (k.Version < 2) FunktionenUndGradeUebernehmen(k);
            StvGruppenEntfernen(k);
            k.Version = Konfiguration.AktuelleVersion;
            k.PflichtpostenErgaenzen();
            return k;
        }

        /// <summary>
        /// Der Wacht Kdt Stv ruht immer im Gegenteil zum Wacht Kdt und braucht keine Gruppe: Gruppen, die nur
        /// noch von Stv-Personen benutzt werden, fallen weg.
        /// </summary>
        static void StvGruppenEntfernen(Konfiguration k)
        {
            var weg = k.Gruppen.Where(g => k.Personen.Any(p => p.Gruppe == g.Name)
                && k.Personen.Where(p => p.Gruppe == g.Name).All(p => Konfiguration.IstStv(p.Funktion))).ToList();
            foreach (var g in weg)
            {
                k.Gruppen.Remove(g);
                foreach (var p in k.Personen.Where(p => p.Gruppe == g.Name)) p.Gruppe = "";
            }
        }

        /// <summary>
        /// Dateien von vor den frei definierbaren Funktionen und Graden umwandeln. Nur für Dateien ohne
        /// Versionsfeld: eine neue Datei darf absichtlich ohne Funktionen oder Grade sein.
        /// </summary>
        static void FunktionenUndGradeUebernehmen(Konfiguration k)
        {
            if (k.Funktionen.Count == 0)
            {
                k.Funktionen.Add(new Funktion { Name = "Wacht Kdt" });
                k.Funktionen.Add(new Funktion { Name = "Wache" });
                foreach (var p in k.Personen)
                    p.Funktion = p.Funktion == "Wachtkdt" ? "Wacht Kdt" : "Wache";
                foreach (var p in k.Posten)
                {
                    if (p.Typ == PostenTyp.Wachtkdt)
                    {
                        p.Typ = PostenTyp.Normal;
                        p.Funktionen = new List<string> { "Wacht Kdt" };
                    }
                    else if (p.Funktionen.Count == 0)
                        p.Funktionen = new List<string> { "Wache" };
                }
            }
            if (k.Grade.Count == 0)
                k.Grade = Vorlagen.StandardGrade.Concat(k.Personen.Select(p => p.Grad)).Where(g => g != "").Distinct().ToList();
        }
    }
}
