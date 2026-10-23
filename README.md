# ShiftDesk

Windows-Programm für die Ablöseplanung (Wachtablösungsliste). ShiftDesk teilt die verfügbaren Personen
stündlich oder alle paar Stunden fair auf die Posten auf, berücksichtigt Ruhezeiten und Essenszeiten und
gibt den Plan als Ausdruck, PDF oder Excel-Datei aus.

## Funktionen

- **Funktionen & Grade** frei anlegen (z. B. Wacht Kdt, Wacht Kdt Stv, Wache, Reserve); Funktionen ohne
  «Im Plan einteilen» erscheinen nur als «Nicht eingeteilt» unten auf dem Plan
- **Personen** mit Grad, Funktion und Ruhegruppe; «Fahrer» haben im Plan nur den Posten vom Typ «Fahrer»
  (z. B. Presto) und Ruhezeit, keine Essensstunde (sie essen in der Ruhezeit) und sind mit einem Auto markiert;
  die Anzahl des Fahrerpostens ist das Soll, gemeldet wird erst, wenn gar kein Fahrer da ist
- **Gruppen & Ruhezeiten**: tägliche Ruhefenster, auch über Mitternacht
- **Posten** mit Farbe, Anzahl Personen, Öffnungszeiten, erlaubten Funktionen, «Auffang» (übrige Leute),
  Typ «Fahrer» (nur für Fahrer), «Pflicht» (muss immer voll besetzt sein, wird vor allen anderen besetzt),
  die Pflichtposten Wacht Kdt, Presto und Subito (immer vorhanden),
  «Draussen» (Posten im Freien, im Plan mit Schneeflocke) und «Am Stück» (gleiche Person bleibt möglichst
  lange); die Reihenfolge ist die Wichtigkeit
- **Feste Zweierteams** (wahlweise): je zwei aufeinanderfolgende Personen mit gleicher Funktion und Gruppe
  sind immer zusammen auf dem gleichen Posten; Posten mit 2 Personen bekommen ein Team, einzelne Plätze eine
  Einzelperson
- **Gesunderhaltung**: niemand ist länger als eingestellt (z. B. 2 Stunden) am Stück draussen; bei längeren
  Ablöseblöcken tauscht der Planer mitten im Block mit jemandem von drinnen, sonst gibt es einen Hinweis
- **Essenszeiten**: jede Person, die das ganze Fenster im Dienst ist, bekommt eine Essensstunde auf einem
  Posten mit «Essen möglich»; wer zur Essenszeit in Ruhe ist, isst in der Ruhezeit (im Plan ebenfalls markiert)
- **Plan** über mehrere Tage, Ablösung jede Stunde oder alle 2–4 Stunden, wahlweise mit einem Zeitfenster
  (z. B. 00:00 bis 06:00), in dem trotzdem jede Stunde abgelöst wird; jede Erstellung ergibt eine neue
  faire Variante (Zuteilung mit der Ungarischen Methode)
- Vorschau mit Zoom (Mausrad, Doppelklick), Drucken, PDF (Microsoft Print to PDF) und Excel (.xlsx)
- Statistik pro Person und Posten
- Modernes Design, folgt dem Windows-Farbmodus (hell/dunkel)

## Bauen

Es braucht nur Windows 10/11; der C#-Compiler des .NET Framework 4 ist bereits dabei.

```bat
build.bat
```

Das erzeugt `ShiftDesk.exe` (eine einzelne Datei, keine Installation nötig).

## Installieren

`ShiftDesk.exe` starten und in der Seitenleiste auf **«Auf diesem PC installieren»** klicken
(oder `ShiftDesk.exe /install`). Installiert wird nur für den aktuellen Benutzer, ohne Administratorrechte:

- Programm: `%LOCALAPPDATA%\Programs\ShiftDesk`
- Startmenü-Eintrag (Windows-Suche)
- Eintrag unter *Einstellungen → Apps* zum Deinstallieren (`ShiftDesk.exe /uninstall`)

Die zuletzt verwendeten Eingaben liegen unter `%APPDATA%\ShiftDesk\letzte.xml`.

## Aufbau

| Datei | Inhalt |
|---|---|
| `src/Model.cs` | Datenmodell und Beispieldaten |
| `src/Planer.cs` | Einteilung (Ruhezeiten, Ablösung, Essen, faire Verteilung) |
| `src/Zeichner.cs` | Plan-Seite für Vorschau, Druck und PDF |
| `src/ExcelExport.cs` | Excel-Export (Office Open XML, ohne Zusatzbibliotheken) |
| `src/MainForm.cs` | Hauptfenster |
| `src/TabellenEditor.cs` | Bearbeitbare Tabellenfelder |
| `src/Steuerelemente.cs`, `src/Popups.cs` | Eigene Steuerelemente und Aufklapplisten |
| `src/Theme.cs` | Farben hell/dunkel |
| `src/Lucide.cs`, `src/LucideDaten.cs` | Icon-Zeichner und eingebettete Icons |
| `src/Installation.cs` | Installieren / Deinstallieren |

## Lizenzen

Icons: [Lucide](https://lucide.dev) (lucide-static 0.452.0), ISC-Lizenz.
Copyright (c) for portions of Lucide are held by Cole Bemis 2013-2022 as part of Feather (MIT).
All other copyright (c) for Lucide are held by Lucide Contributors 2022.
