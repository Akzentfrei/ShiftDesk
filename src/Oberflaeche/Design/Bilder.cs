using System.Collections.Generic;
using System.Drawing;

namespace ShiftDesk.Oberflaeche.Design
{
    /// <summary>In die exe eingebettete Bilder (siehe build.bat), einmal geladen und behalten.</summary>
    public static class Bilder
    {
        static readonly Dictionary<string, Image> geladen = new Dictionary<string, Image>();

        /// <summary>Lädt ein eingebettetes Bild, z. B. «logo.png» oder «armee-logo.png».</summary>
        /// <returns>Das Bild oder null, wenn es nicht eingebettet ist.</returns>
        public static Image Laden(string name)
        {
            Image bild;
            if (geladen.TryGetValue(name, out bild)) return bild;
            // der Strom muss offen bleiben, solange das Bild lebt
            var s = typeof(Bilder).Assembly.GetManifestResourceStream("ShiftDesk." + name);
            bild = s == null ? null : Image.FromStream(s);
            geladen[name] = bild;
            return bild;
        }
    }
}
