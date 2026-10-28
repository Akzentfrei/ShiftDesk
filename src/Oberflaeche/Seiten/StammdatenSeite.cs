using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ShiftDesk.Modell;
using ShiftDesk.Oberflaeche.Design;
using ShiftDesk.Oberflaeche.Steuerelemente;
using ShiftDesk.Oberflaeche.Tabellen;

namespace ShiftDesk.Oberflaeche.Seiten
{
    /// <summary>
    /// Seite «Funktionen &amp; Grade»: die Listen, aus denen bei den Personen gewählt wird. Zwei Tabellen
    /// nebeneinander, im schmalen Fenster untereinander.
    /// </summary>
    public class StammdatenSeite : Seite
    {
        /// <summary>Funktionen in Plan-Reihenfolge, mit Schalter «Im Plan einteilen».</summary>
        public readonly Tabelle Funktionen = new Tabelle();

        /// <summary>Grade.</summary>
        public readonly Tabelle Grade = new Tabelle();

        public StammdatenSeite()
            : base("Funktionen & Grade", "id-card",
                "Lege hier fest, welche Funktionen und Grade es gibt. Bei den Personen wählst du sie dann nur noch aus. Die Reihenfolge der Funktionen ist die Reihenfolge im Plan.")
        {
            Funktionen.Spalte("Name", "Funktion", 150, new SpaltenInfo(SpaltenArt.Text));
            Funktionen.Spalte("ImPlan", "Im Plan einteilen", 80, new SpaltenInfo(SpaltenArt.Schalter));
            Funktionen.Zeilenknoepfe(true);
            Funktionen.Erklaeren("ImPlan", "Aus = Personen mit dieser Funktion werden nicht eingeteilt und stehen nur unten auf dem Plan bei «Nicht eingeteilt» (z. B. Reserve).");
            Funktionen.LeerHinweis = new[] { "id-card", "Noch keine Funktionen", "Mit «Funktion» oben rechts eine erfassen." };

            Grade.Spalte("Name", "Grad", 100, new SpaltenInfo(SpaltenArt.Text));
            Grade.Zeilenknoepfe(true);
            Grade.LeerHinweis = new[] { "id-card", "Noch keine Grade", "Mit «Grad» oben rechts einen erfassen." };

            var links = Bereich("Funktionen", Funktionen, Knopf.Mit("Funktion", "plus", () => Funktionen.Hinzufuegen("", true), true));
            var rechts = Bereich("Grade", Grade, Knopf.Mit("Grad", "plus", () => Grade.Hinzufuegen(""), true));
            Aufbauen(Raster(links, rechts));
        }

        public override IEnumerable<Tabelle> Tabellen
        {
            get { return new[] { Funktionen, Grade }; }
        }

        public override void Anzeigen(Konfiguration k)
        {
            Funktionen.Grid.Rows.Clear();
            foreach (var f in k.Funktionen)
                Funktionen.Grid.Rows.Add(f.Name, f.ImPlan);
            Grade.Grid.Rows.Clear();
            foreach (var gr in k.Grade)
                Grade.Grid.Rows.Add(gr);
        }

        public override void Uebernehmen(Konfiguration k)
        {
            foreach (var r in Funktionen.Zeilen)
                k.Funktionen.Add(new Funktion { Name = Tabelle.Wert(r, "Name"), ImPlan = Tabelle.Schalter(r, "ImPlan") });
            k.Grade = Grade.Namen();
        }

        /// <summary>Bereich mit eigener Titelzeile und Tabelle.</summary>
        static Control Bereich(string titel, Tabelle tabelle, params Knopf[] knoepfe)
        {
            var p = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            tabelle.Karte.Dock = DockStyle.Fill;
            p.Controls.Add(tabelle.Karte);
            p.Controls.Add(new Kopfzeile(titel, null, Schrift.Titel, Thema.S(52), Thema.S(10), knoepfe));
            Farbwechsel.Bei(() => p.BackColor = Thema.Hintergrund);
            return p;
        }

        /// <summary>Die beiden Bereiche nebeneinander (58 % / 42 %), im schmalen Fenster untereinander.</summary>
        static Control Raster(Control links, Control rechts)
        {
            var raster = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = Padding.Empty };
            raster.Controls.Add(links, 0, 0);
            raster.Controls.Add(rechts, 1, 0);
            bool? gestapelt = null;
            raster.Resize += (s, e) =>
            {
                bool neu = raster.Width < Thema.S(720);
                if (gestapelt == neu || raster.Width <= 0) return;
                gestapelt = neu;
                raster.SuspendLayout();
                raster.ColumnStyles.Clear();
                raster.RowStyles.Clear();
                raster.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, neu ? 100 : 58));
                raster.ColumnStyles.Add(neu ? new ColumnStyle(SizeType.Absolute, 0) : new ColumnStyle(SizeType.Percent, 42));
                raster.RowStyles.Add(new RowStyle(SizeType.Percent, neu ? 58 : 100));
                raster.RowStyles.Add(neu ? new RowStyle(SizeType.Percent, 42) : new RowStyle(SizeType.Absolute, 0));
                raster.SetCellPosition(rechts, new TableLayoutPanelCellPosition(neu ? 0 : 1, neu ? 1 : 0));
                links.Margin = neu ? new Padding(0, 0, 0, Thema.S(12)) : new Padding(0, 0, Thema.S(12), 0);
                rechts.Margin = neu ? new Padding(0, Thema.S(12), 0, 0) : new Padding(Thema.S(12), 0, 0, 0);
                raster.ResumeLayout(true);
            };
            Farbwechsel.Bei(() => raster.BackColor = Thema.Hintergrund);
            return raster;
        }
    }
}
