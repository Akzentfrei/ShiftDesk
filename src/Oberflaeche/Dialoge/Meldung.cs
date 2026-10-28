using System.Drawing;
using System.Windows.Forms;
using ShiftDesk.Oberflaeche.Design;

namespace ShiftDesk.Oberflaeche.Dialoge
{
    /// <summary>Art einer <see cref="Meldung"/>; bestimmt Symbol und Farbe.</summary>
    public enum MeldungsArt
    {
        /// <summary>Blaues Info-Zeichen.</summary>
        Info,

        /// <summary>Grünes Häkchen.</summary>
        Erfolg,

        /// <summary>Gelbes Warndreieck.</summary>
        Warnung,

        /// <summary>Rotes Kreuz.</summary>
        Fehler,
    }

    /// <summary>Mitteilungen mit nur einem Knopf «OK» im Design von ShiftDesk (statt der Windows-MessageBox).</summary>
    public static class Meldung
    {
        /// <summary>Blaues Info-Zeichen.</summary>
        public static void Info(IWin32Window besitzer, string titel, string text = null) { Zeigen(besitzer, titel, text, MeldungsArt.Info); }

        /// <summary>Grünes Häkchen.</summary>
        public static void Erfolg(IWin32Window besitzer, string titel, string text = null) { Zeigen(besitzer, titel, text, MeldungsArt.Erfolg); }

        /// <summary>Gelbes Warndreieck.</summary>
        public static void Warnung(IWin32Window besitzer, string titel, string text = null) { Zeigen(besitzer, titel, text, MeldungsArt.Warnung); }

        /// <summary>Rotes Kreuz.</summary>
        public static void Fehler(IWin32Window besitzer, string titel, string text = null) { Zeigen(besitzer, titel, text, MeldungsArt.Fehler); }

        /// <summary>Zeigt die Mitteilung und wartet auf «OK».</summary>
        /// <param name="besitzer">Fenster, über dem sie erscheint; null = mitten auf dem Bildschirm, mit eigenem Taskleisten-Eintrag.</param>
        /// <param name="titel">Fett gedruckte erste Zeile.</param>
        /// <param name="text">Erklärung darunter, null = keine.</param>
        /// <param name="art">Symbol und Farbe.</param>
        public static void Zeigen(IWin32Window besitzer, string titel, string text, MeldungsArt art)
        {
            string symbol = art == MeldungsArt.Erfolg ? "circle-check" : art == MeldungsArt.Warnung ? "triangle-alert" : art == MeldungsArt.Fehler ? "circle-x" : "info";
            Color farbe = art == MeldungsArt.Erfolg ? Thema.Erfolg : art == MeldungsArt.Warnung ? Thema.Warnung : art == MeldungsArt.Fehler ? Thema.Gefahr : Thema.Akzent;
            using (var d = new Rueckfrage(besitzer != null, titel, text, "OK", null, symbol, farbe, false))
                d.ShowDialog(besitzer);
        }
    }
}
