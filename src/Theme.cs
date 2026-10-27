using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ShiftDesk
{
    // Farben der Oberfläche. Folgt dem Windows-Farbmodus (hell/dunkel), auch während das Programm läuft.
    public static class Theme
    {
        public static bool Dunkel { get; private set; }
        public static float Skala { get; private set; }

        public static Color Hintergrund, Seitenleiste, Karte, Rand, Text, Gedaempft, Hover, Auswahl,
            Akzent, AkzentHover, AkzentText, Eingabe, EingabeRand, Gitter, SchalterAus, Erfolg, Warnung, Gefahr, GefahrKnopf, GefahrKnopfHover, Vorschau;

        public static event EventHandler Geaendert;

        static Theme()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
                Skala = g.DpiX / 96f;
            Laden();
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (SystemDunkel() == Dunkel) return;
                Laden();
                var h = Geaendert;
                if (h != null) h(null, EventArgs.Empty);
            };
        }

        public static int S(float px)
        {
            return (int)Math.Round(px * Skala);
        }

        // Einheitliche Masse: Eingabefelder, Knöpfe, Ecken
        public static int FeldHoehe { get { return S(38); } }
        public static int KnopfHoehe { get { return S(36); } }
        public static int RadiusFeld { get { return S(8); } }
        public static int RadiusKarte { get { return S(12); } }

        // Leichte Hinterlegung in einer Farbe (Symbolkreis, Status, Gefahr beim Darüberfahren)
        public static Color Toenung(Color farbe, bool staerker = false)
        {
            return Color.FromArgb((Dunkel ? 48 : 26) + (staerker ? 18 : 0), farbe);
        }

        static bool SystemDunkel()
        {
            // zum Testen: SHIFTDESK_THEMA=hell oder dunkel erzwingt einen Modus
            string erzwungen = Environment.GetEnvironmentVariable("SHIFTDESK_THEMA");
            if (erzwungen == "dunkel") return true;
            if (erzwungen == "hell") return false;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        static Color Hex(int rgb)
        {
            return Color.FromArgb(255, Color.FromArgb(rgb));
        }

        static void Laden()
        {
            Dunkel = SystemDunkel();
            if (Dunkel)
            {
                Hintergrund = Hex(0x111318);
                Seitenleiste = Hex(0x16181D);
                Karte = Hex(0x1B1E24);
                Rand = Hex(0x2B2F38);
                Text = Hex(0xE6E8EC);
                Gedaempft = Hex(0x9AA1AD);
                Hover = Hex(0x252932);
                Auswahl = Hex(0x1D2B45);
                Akzent = Hex(0x4C8DF6);
                AkzentHover = Hex(0x6A9FF8);
                Eingabe = Hex(0x14161B);
                EingabeRand = Hex(0x343945);
                Gitter = Hex(0x262A32);
                SchalterAus = Hex(0x3C424E);
                Erfolg = Hex(0x34C77B);
                Warnung = Hex(0xF2B13C);
                Gefahr = Hex(0xF87171);
                GefahrKnopf = Hex(0xDC2626);
                GefahrKnopfHover = Hex(0xEF4444);
                Vorschau = Hex(0x0C0D11);
            }
            else
            {
                Hintergrund = Hex(0xF4F5F7);
                Seitenleiste = Hex(0xFFFFFF);
                Karte = Hex(0xFFFFFF);
                Rand = Hex(0xE3E6EB);
                Text = Hex(0x14171C);
                Gedaempft = Hex(0x69707D);
                Hover = Hex(0xF1F3F6);
                Auswahl = Hex(0xEAF1FE);
                Akzent = Hex(0x2563EB);
                AkzentHover = Hex(0x1D4FD0);
                Eingabe = Hex(0xFFFFFF);
                EingabeRand = Hex(0xD3D8E0);
                Gitter = Hex(0xEEF0F3);
                SchalterAus = Hex(0xCDD3DC);
                Erfolg = Hex(0x16A34A);
                Warnung = Hex(0xD97706);
                Gefahr = Hex(0xDC2626);
                GefahrKnopf = Hex(0xDC2626);
                GefahrKnopfHover = Hex(0xB91C1C);
                Vorschau = Hex(0xE6E9EE);
            }
            AkzentText = Color.White;
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int wert, int groesse);

        // Dunkle Titelleiste ab Windows 10 20H1 (Attribut 20), ältere Builds kennen 19.
        public static void Titelleiste(Form f)
        {
            if (!f.IsHandleCreated) return;
            int an = Dunkel ? 1 : 0;
            try
            {
                if (DwmSetWindowAttribute(f.Handle, 20, ref an, 4) != 0)
                    DwmSetWindowAttribute(f.Handle, 19, ref an, 4);
            }
            catch { }
        }

        // Runde Ecken für Aufklappfenster (wirkt ab Windows 11, sonst eckig)
        public static void RundeEcken(IntPtr hwnd)
        {
            int klein = 3;
            try { DwmSetWindowAttribute(hwnd, 33, ref klein, 4); }
            catch { }
        }
    }

    // Schriften: eine feste Abstufung für die ganze Oberfläche (nicht pro Steuerelement neu erzeugen)
    public static class Schrift
    {
        public static readonly Font Seitentitel = new Font("Segoe UI Semibold", 19f);
        public static readonly Font Titel = new Font("Segoe UI Semibold", 12.5f);
        public static readonly Font Ueberschrift = new Font("Segoe UI Semibold", 10f);
        public static readonly Font Eingabe = new Font("Segoe UI", 10f);
        public static readonly Font Text = new Font("Segoe UI", 9.5f);
        public static readonly Font Fett = new Font("Segoe UI Semibold", 9.5f);
        public static readonly Font Klein = new Font("Segoe UI", 9f);
        public static readonly Font KleinFett = new Font("Segoe UI Semibold", 9f);
        public static readonly Font Beschriftung = new Font("Segoe UI Semibold", 8.5f);
        public static readonly Font Abschnitt = new Font("Segoe UI Semibold", 7.5f);
    }

    // Eingebettete Bilder (Logo, Armee-Logo), einmal geladen
    public static class Bilder
    {
        static readonly Dictionary<string, Image> geladen = new Dictionary<string, Image>();

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
