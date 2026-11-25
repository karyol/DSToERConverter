using System.Text.RegularExpressions;

internal sealed class MatMap
{
    internal sealed record Row(string Er, int Gx);

    private readonly Dictionary<string, Row> _mtd = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Row> _name = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(Regex Rx, Row Row)> _mtdRx = new();
    private readonly List<(Regex Rx, Row Row)> _nameRx = new();

    // DS3 trzyma w MTD literalne nawiasy (C[ARSN].mtd) — normalizujemy po obu stronach.
    private static string Norm(string s) => s.Replace("[", "").Replace("]", "");

    public static MatMap Load(string path)
    {
        var map = new MatMap();
        int loaded = 0;

        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            string[] p = line.Split(';');
            if (p.Length < 3)
                continue;
            if (!int.TryParse(p[2].Trim(), out int gx))
                continue;

            string key = Path.GetFileName(p[0].Trim());
            if (key.Length == 0)
                continue;

            var row = new Row(p[1].Trim(), gx);
            bool byMtd = key.EndsWith(".mtd", StringComparison.OrdinalIgnoreCase);

            if (byMtd)
                Add(map._mtd, map._mtdRx, key, row);
            else
                Add(map._name, map._nameRx, key, row);

            loaded++;
        }

        if (loaded == 0)
            throw new InvalidDataException("matmap bez wpisów");

        return map;
    }

    private static void Add(Dictionary<string, Row> exact, List<(Regex, Row)> rx, string key, Row row)
    {
        exact[Norm(key)] = row;

        if (key.Contains('['))
            rx.Add((new Regex("^" + Regex.Escape(key)
                .Replace(@"\[", "[").Replace(@"\]", "]") + "$", RegexOptions.IgnoreCase), row));

        if (key.IndexOfAny(new[] { '*', '?' }) >= 0)
            rx.Add((new Regex("^" + Regex.Escape(key)
                .Replace(@"\*", ".*").Replace(@"\?", ".") + "$", RegexOptions.IgnoreCase), row));
    }

    // Nazwa ma priorytet: część może dzielić MTD z inną (wings i emissive), a różnić się nazwą.
    public Row? Resolve(string? materialName, string mtd) =>
        Match(_name, _nameRx, materialName) ?? Match(_mtd, _mtdRx, mtd);

    private static Row? Match(Dictionary<string, Row> exact, List<(Regex Rx, Row Row)> rx, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (exact.TryGetValue(Norm(value), out Row? hit))
            return hit;

        foreach (var (r, row) in rx)
            if (r.IsMatch(value))
                return row;

        return null;
    }
}