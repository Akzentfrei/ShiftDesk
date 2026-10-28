using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShiftDesk.Oberflaeche
{
    /// <summary>
    /// Merkt sich Lage und Grösse des Hauptfensters beim Schliessen (<see cref="Speicherorte.Fensterlage"/>,
    /// eine Zeile «x y breite höhe maximiert») und stellt sie beim nächsten Start wieder her.
    /// </summary>
    static class FensterZustand
    {
        /// <summary>
        /// Wieder in der Grösse und an der Stelle vom letzten Mal aufgehen. Ist der Bildschirm dafür zu klein
        /// (z. B. ein anderer Bildschirm), geht das Fenster in der kleinsten Grösse mittig auf. Noch nie
        /// geschlossen oder unlesbar: das Fenster behält seine Standardgrösse.
        /// </summary>
        public static void Wiederherstellen(Form f)
        {
            int[] w;
            try { w = File.ReadAllText(Speicherorte.Fensterlage).Trim().Split(' ').Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray(); }
            catch { return; }
            if (w.Length < 5 || w[2] <= 0 || w[3] <= 0) return;
            var r = new Rectangle(w[0], w[1], w[2], w[3]);
            var bereich = Screen.FromRectangle(r).WorkingArea;
            if (r.Width <= bereich.Width && r.Height <= bereich.Height)
            {
                // ganz auf den Bildschirm schieben, falls es teilweise daneben liegt
                r.X = Math.Max(bereich.Left, Math.Min(r.X, bereich.Right - r.Width));
                r.Y = Math.Max(bereich.Top, Math.Min(r.Y, bereich.Bottom - r.Height));
            }
            else
            {
                var klein = new Size(Math.Min(f.MinimumSize.Width, bereich.Width), Math.Min(f.MinimumSize.Height, bereich.Height));
                r = new Rectangle(bereich.Left + (bereich.Width - klein.Width) / 2, bereich.Top + (bereich.Height - klein.Height) / 2, klein.Width, klein.Height);
            }
            f.StartPosition = FormStartPosition.Manual;
            f.Bounds = r;
            if (w[4] == 1) f.WindowState = FormWindowState.Maximized;
        }

        /// <summary>Speichert Lage und Grösse; maximiert oder minimiert die des normalen Fensters.</summary>
        public static void Merken(Form f)
        {
            var r = f.WindowState == FormWindowState.Normal ? f.Bounds : f.RestoreBounds;
            int maximiert = f.WindowState == FormWindowState.Maximized ? 1 : 0;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Speicherorte.Fensterlage));
                File.WriteAllText(Speicherorte.Fensterlage, string.Join(" ", new[] { r.X, r.Y, r.Width, r.Height, maximiert }.Select(x => x.ToString(CultureInfo.InvariantCulture))));
            }
            catch { }
        }
    }
}
