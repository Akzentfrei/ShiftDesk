# ShiftDesk

Windows-Programm für die Ablöseplanung (Wachtablösungsliste). ShiftDesk teilt die verfügbaren Personen
stündlich oder alle paar Stunden fair auf die Posten auf, berücksichtigt Ruhezeiten und Essenszeiten und
gibt den Plan als Ausdruck, PDF oder Excel-Datei aus.

## Download

**[ShiftDesk.zip herunterladen](https://github.com/Akzentfrei/ShiftDesk/releases/latest/download/ShiftDesk.zip)**
(neueste Version), entpacken und `ShiftDesk.exe` starten. Die beiliegende `Anleitung.txt` erklärt die
Installation; alle Versionen stehen unter [Releases](https://github.com/Akzentfrei/ShiftDesk/releases).

## Funktionen

- **Funktionen & Grade** frei anlegen (z. B. Wacht Kdt, Wacht Kdt Stv, Wache, Reserve); Funktionen ohne
  «Im Plan einteilen» erscheinen nur als «Nicht eingeteilt» unten auf dem Plan
- **Personen** mit Grad, Funktion und Ruhegruppe; «Fahrer» haben im Plan nur den Posten vom Typ «Fahrer»
  (z. B. Presto) und Ruhezeit, keine Essensstunde (sie essen in der Ruhezeit) und sind mit einem Auto markiert;
  der erste Platz des Fahrerpostens ist für einen Fahrer, die weiteren Plätze bekommen Fahrer, wenn welche
  da sind, sonst andere Leute
- **Gruppen & Ruhezeiten**: tägliche Ruhefenster, auch über Mitternacht; der Wacht Kdt Stv braucht keine Gruppe,
  er ruht immer dann, wenn der Wacht Kdt im Dienst ist
- **Posten** mit Farbe, Anzahl Personen, Öffnungszeiten, erlaubten Funktionen, «Auffang» (übrige Leute),
  Typ «Fahrer» (nur für Fahrer) und «Draussen» (Posten im Freien, in der Legende mit Schneeflocke);
  die Posten Wacht Kdt, Presto und Subito gibt es immer. Alle Posten sollen voll besetzt sein, reichen die
  Leute nicht, entscheidet die Reihenfolge (unten bleibt zuerst leer). Wacht Kdt und Stv bleiben möglichst
  am Stück auf ihrem Posten
- **Zweierteams** (wahlweise): je zwei aufeinanderfolgende Personen mit gleicher Funktion und Gruppe
  sind wenn möglich zusammen auf dem gleichen Posten. Gehen die Leute nicht auf, verteilt sich ein Team auf
  zwei einzelne Posten (z. B. beide Zuko), und wer übrig bleibt, bildet für eine Ablösung mit jemand anderem
  ein Paar
- **Gesunderhaltung**: niemand ist länger als eingestellt (z. B. 2 Stunden) am Stück draussen; bei längeren
  Ablöseblöcken tauscht der Planer mitten im Block mit jemandem von drinnen, sonst gibt es einen Hinweis
- **Essenszeiten**: jede Person, die das ganze Fenster im Dienst ist, bekommt eine Essensstunde auf einem
  Posten mit «Essen»; wer zur Essenszeit in Ruhe ist, isst in der Ruhezeit (im Plan ebenfalls markiert); wer auf
  keinem Posten mit Essen war, bekommt ein oranges Besteck: eine Reserve löst zum Essen ab (nicht beim Wacht Kdt
  und Stv und nicht bei Fahrern)
- **Plan** als Tagesplan (ein Blatt von 00:00 bis 24:00 ohne Datum, gilt jeden Tag) oder über mehrere Tage mit
  Datum, Ablösung jede Stunde oder alle 2–4 Stunden, wahlweise mit einem Zeitfenster
  (z. B. 00:00 bis 06:00), in dem trotzdem jede Stunde abgelöst wird; jede Erstellung ergibt eine neue
  faire Variante (Zuteilung mit der Ungarischen Methode)
- **Ausdruck** im Aufbau des Formulars «Ablösungsliste» mit Armee-Logo, auf Deutsch, Französisch, Italienisch
  oder Englisch: Kopf mit Einheit, Für, Datum und Legende, Posten als
  Buchstaben (A, B, C …) auf hellen Postenfarben, ein Feld pro Ablösung, «-» für Ruhezeit, dicke Linien
  zwischen Zweierteams und Blöcken, Stunden senkrecht auf den Linien (0000 bis 2400)
- Vorschau mit Zoom (Mausrad, Doppelklick), Drucken, PDF (Microsoft Print to PDF) und Excel (.xlsx)
- Merkt sich die Fenstergrösse; die Seitenleiste lässt sich auf Symbole einklappen
- Statistik pro Person und Posten
- Passt sich der Fenstergrösse an: im schmalen Fenster zeigt die Seitenleiste nur Symbole (Datei-Befehle im
  Menü, wenn die Höhe nicht reicht), Knöpfe nur ihr Symbol, die Plan-Einstellungen stehen über statt neben der
  Vorschau, und Tabellen lassen sich mit eigenen Bildlaufleisten (oder Shift+Mausrad) verschieben
- Modernes Design, folgt dem Windows-Farbmodus (hell/dunkel); Meldungen, Rückfragen, Aufklapplisten,
  Farbwähler (Palette und eigene Farbe mit Farbfläche und Hex-Eingabe) und Tooltips sind selbst gezeichnet,
  nur die Datei- und Druckerauswahl sind Windows-Dialoge

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

Auf GitHub veröffentlichen: Version in `src/Program.cs` erhöhen, committen und eine passende Marke pushen,
z. B. `git tag v1.2.0` und `git push origin main v1.2.0` (oder auf GitHub unter Actions → Release → «Run workflow»). Die GitHub Action (`.github/workflows/release.yml`)
baut dann die exe und hängt `ShiftDesk.zip` und `ShiftDesk.exe` an ein neues Release; der Download-Link oben
zeigt immer auf die neueste Version.

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

Damit auf dem PC nur eine Version liegt (und die Windows-Suche nur eine findet), räumt ShiftDesk
liegengebliebene Kopien weg: `ShiftDesk*.exe` in Downloads, auf dem Desktop und in Dokumente (bis zwei
Ordner tief), die nicht neuer sind als die installierte Version, beim Installieren und bei jedem Start der
installierten Version. Ein entpackter Ordner, in dem danach nur noch die `Anleitung.txt` liegt, verschwindet mit;
eine exe direkt auf dem Desktop wird durch eine Verknüpfung ersetzt; Zip-Dateien bleiben liegen. Startet man eine
Kopie, obwohl dieselbe oder eine neuere Version installiert ist, öffnet sich die installierte Version, und die
Kopie wird danach gelöscht (`ShiftDesk.exe /entfernen "<Pfad>"`). Kopien ausserhalb dieser Ordner (z. B. auf
einem USB-Stick) laufen bei gleicher Version weiter selbst.

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
| `src/Steuerelemente.cs`, `src/Popups.cs` | Eigene Steuerelemente, Aufklapplisten, Farbwähler und Tooltips |
| `src/Theme.cs` | Farben hell/dunkel |
| `src/Lucide.cs`, `src/LucideDaten.cs` | Icon-Zeichner und eingebettete Icons |
| `src/Installation.cs` | Installieren / Aktualisieren / Deinstallieren |
| `src/Dialoge.cs` | Rückfragen (Gefahr rot, Vorschlag blau) und Meldungen (Info, Erfolg, Warnung, Fehler) |
| `release.bat`, `Anleitung.txt` | Zip zum Weitergeben |

## Lizenzen

Icons: [Lucide](https://lucide.dev) (lucide-static 0.452.0), ISC-Lizenz.
Copyright (c) for portions of Lucide are held by Cole Bemis 2013-2022 as part of Feather (MIT).
All other copyright (c) for Lucide are held by Lucide Contributors 2022.
