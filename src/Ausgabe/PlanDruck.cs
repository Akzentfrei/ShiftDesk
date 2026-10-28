using System.Drawing.Printing;
using ShiftDesk.Planung;

namespace ShiftDesk.Ausgabe
{
    /// <summary>Druckauftrag für den Plan: A4 quer, eine Seite pro 24 Stunden (auch für «Als PDF»).</summary>
    public static class PlanDruck
    {
        /// <summary>Der Drucker, über den Windows PDF-Dateien schreibt.</summary>
        public const string PdfDrucker = "Microsoft Print to PDF";

        /// <summary>Erstellt das Druckdokument; der Aufrufer gibt es nach dem Drucken frei.</summary>
        /// <param name="plan">Der Plan; derselbe bis zur letzten Seite, auch wenn inzwischen ein neuer erstellt wird.</param>
        /// <param name="drucker">Name des Druckers, null = Standarddrucker.</param>
        public static PrintDocument Dokument(Plan plan, string drucker)
        {
            var pd = new PrintDocument { DocumentName = "Wachtplan (ShiftDesk)" };
            if (drucker != null) pd.PrinterSettings.PrinterName = drucker;
            pd.PrintController = new StandardPrintController();
            pd.DefaultPageSettings.Landscape = true;
            pd.DefaultPageSettings.Margins = new Margins(35, 35, 35, 35);
            foreach (PaperSize ps in pd.PrinterSettings.PaperSizes)
                if (ps.Kind == PaperKind.A4) { pd.DefaultPageSettings.PaperSize = ps; break; }
            int s = 0;
            pd.BeginPrint += (o, e) => s = 0;
            pd.PrintPage += (o, e) =>
            {
                PlanZeichner.Seite(e.Graphics, e.MarginBounds, plan, s);
                s++;
                e.HasMorePages = s < plan.Seiten;
            };
            return pd;
        }
    }
}
