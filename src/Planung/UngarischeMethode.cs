namespace ShiftDesk.Planung
{
    /// <summary>
    /// Löst das Zuordnungsproblem: jeder Zeile einer quadratischen Kostenmatrix genau eine Spalte so zuordnen,
    /// dass die Summe der Kosten minimal ist (Ungarische Methode, O(n³)).
    /// </summary>
    static class UngarischeMethode
    {
        /// <summary>Berechnet die günstigste Zuordnung.</summary>
        /// <param name="a">Kostenmatrix [Zeile, Spalte] der Grösse n × n.</param>
        /// <param name="n">Anzahl Zeilen und Spalten.</param>
        /// <returns>Für jede Zeile die gewählte Spalte.</returns>
        public static int[] Loesen(double[,] a, int n)
        {
            const double INF = double.MaxValue / 4;
            var u = new double[n + 1];
            var v = new double[n + 1];
            var p = new int[n + 1];
            var weg = new int[n + 1];
            for (int i = 1; i <= n; i++)
            {
                p[0] = i;
                int j0 = 0;
                var minv = new double[n + 1];
                var benutzt = new bool[n + 1];
                for (int j = 0; j <= n; j++) minv[j] = INF;
                do
                {
                    benutzt[j0] = true;
                    int i0 = p[j0], j1 = 0;
                    double delta = INF;
                    for (int j = 1; j <= n; j++)
                    {
                        if (benutzt[j]) continue;
                        double cur = a[i0 - 1, j - 1] - u[i0] - v[j];
                        if (cur < minv[j]) { minv[j] = cur; weg[j] = j0; }
                        if (minv[j] < delta) { delta = minv[j]; j1 = j; }
                    }
                    for (int j = 0; j <= n; j++)
                    {
                        if (benutzt[j]) { u[p[j]] += delta; v[j] -= delta; }
                        else minv[j] -= delta;
                    }
                    j0 = j1;
                } while (p[j0] != 0);
                do
                {
                    int j1 = weg[j0];
                    p[j0] = p[j1];
                    j0 = j1;
                } while (j0 != 0);
            }
            var ergebnis = new int[n];
            for (int j = 1; j <= n; j++)
                if (p[j] != 0) ergebnis[p[j] - 1] = j - 1;
            return ergebnis;
        }
    }
}
