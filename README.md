# ShiftDesk

Windows-Programm für die Ablöseplanung (Wachtablösungsliste). ShiftDesk teilt die verfügbaren Personen
stündlich oder alle paar Stunden fair auf die Posten auf, berücksichtigt Ruhezeiten und Essenszeiten und
gibt den Plan als Ausdruck, PDF oder Excel-Datei aus.

## Funktionen

- **Funktionen & Grade** frei anlegen (z. B. Wacht Kdt, Wacht Kdt Stv, Wache, Reserve); Funktionen ohne
  «Im Plan einteilen» erscheinen nur als «Nicht eingeteilt» unten auf dem Plan
- **Personen** mit Grad, Funktion und Ruhegruppe; «Fahrer» haben im Plan nur den Posten vom Typ «Fahrer»
  (z. B. Presto) und Ruhezeit, keine Essensstunde (sie essen in der Ruhezeit) und sind mit einem Auto markiert;
  der erste Platz des Fahrerpostens ist für einen Fahrer, die weiteren Plätze bekommen Fahrer, wenn welche
  da sind, sonst andere Leute
- **Gruppen & Ruhezeiten**: tägliche Ruhefenster, auch über Mitternacht
- **Posten** mit Farbe, Anzahl Personen, Öffnungszeiten, erlaubten Funktionen, «Auffang» (übrige Leute),
  Typ «Fahrer» (nur für Fahrer), «Pflicht» (muss immer voll besetzt sein, wird vor allen anderen besetzt),
  die Pflichtposten Wacht Kdt, Presto und Subito (immer vorhanden),
  «Draussen» (Posten im Freien, in der Legende mit Schneeflocke) und «Am Stück» (gleiche Person bleibt möglichst
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
- **Ausdruck** im Aufbau des Formulars «Ablösungsliste» mit Armee-Logo, auf Deutsch, Französisch, Italienisch
  oder Englisch: Kopf mit Einheit, Für, Datum und Legende, Posten als
  Buchstaben (A, B, C …) auf hellen Postenfarben, ein Feld pro Ablösung, «-» für Ruhezeit, dicke Linien
  zwischen Zweierteams und Blöcken, Stunden senkrecht auf den Linien (0000 bis 2400)
- Vorschau mit Zoom (Mausrad, Doppelklick), Drucken, PDF (Microsoft Print to PDF) und Excel (.xlsx)
- Statistik pro Person und Posten
- Modernes Design, folgt dem Windows-Farbmodus (hell/dunkel)

## Bauen

Es braucht nur Windows 10/11; der C#-Compiler des .NET Framework 4 ist bereits dabei.

```bat
build.bat
```

Das erzeugt `ShiftDesk.exe` (eine einzelne Datei, keine Installation nötig).

## Weitergeben

```bat
release.bat
```

baut die exe und packt sie mit `Anleitung.txt` in `ShiftDesk-<Version>.zip`. Diese Zip-Datei kann man
verschicken (Mail-Anbieter blockieren oft .exe-Anhänge, auch in Zips; dann per OneDrive, Google Drive,
WeTransfer o. ä. teilen). Vor jeder Weitergabe die Version in `src/Program.cs` erhöhen, damit die bereits
installierte Version die Aktualisierung anbietet.

Die exe ist nicht signiert. Beim ersten Start einer heruntergeladenen Datei warnt Windows SmartScreen
(«Der Computer wurde durch Windows geschützt»); «Weitere Informationen → Trotzdem ausführen» startet sie.
Die Anleitung in der Zip erklärt das.

## Installieren

Wird `ShiftDesk.exe` aus einem fremden Ordner (Downloads, Zip, USB-Stick) gestartet, bietet sie selbst an,
sich zu installieren; ist schon eine ältere Version installiert, bietet sie die Aktualisierung an. Das
Angebot lässt sich mit «Ohne Installation starten» ablehnen oder mit `ShiftDesk.exe /portable`
unterdrücken. Installieren geht auch später in der Seitenleiste über **«Auf diesem PC installieren»**
oder mit `ShiftDesk.exe /install`. Installiert wird nur für den aktuellen Benutzer, ohne
Administratorrechte:

- Programm: `%LOCALAPPDATA%\Programs\ShiftDesk` (die Herkunftsmarkierung «aus dem Internet» wird dabei
  entfernt, damit SmartScreen nicht noch einmal warnt)
- Startmenü-Eintrag (Windows-Suche)
- Dateizuordnung für `.shiftdesk`
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
| `src/Installation.cs` | Installieren / Aktualisieren / Deinstallieren |
| `src/Dialoge.cs` | Rückfragen (Gefahr rot, Vorschlag blau) |
| `release.bat`, `Anleitung.txt` | Zip zum Weitergeben |

## Lizenzen

Icons: [Lucide](https://lucide.dev) (lucide-static 0.452.0), ISC-Lizenz.
Copyright (c) for portions of Lucide are held by Cole Bemis 2013-2022 as part of Feather (MIT).
All other copyright (c) for Lucide are held by Lucide Contributors 2022.
