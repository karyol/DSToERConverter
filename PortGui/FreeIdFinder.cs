using SoulsFormats;

internal static class FreeIdFinder
{
    private static readonly string[] ChrRoots = { "chr" };

    public static List<int> Find(string regulationPath, IEnumerable<string> dirs,
        int from, int to, int max, Action<string> log)
    {
        if (from < 1 || to <= from) throw new ArgumentException("zły zakres ID");

        var used = new HashSet<int>();
        int rows = 0;

        foreach (string paramFile in ParamFiles(regulationPath, log))
        {
            PARAM p;
            try { p = PARAM.Read(paramFile); }
            catch (Exception e) { log($"  pominięto {Path.GetFileName(paramFile)}: {e.Message}"); continue; }

            foreach (var r in p.Rows)
            {
                rows++;
                if (r.ID >= 10000) used.Add(r.ID / 10000);
            }
        }

        int files = 0;
        foreach (string dir in dirs.Where(d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d)))
            foreach (string root in ChrRoots)
            {
                string chr = Path.Combine(dir, root);
                if (!Directory.Exists(chr)) continue;
                foreach (string f in Directory.EnumerateFiles(chr, "c*.*"))
                {
                    string n = Path.GetFileName(f);
                    if (n.Length < 5 || n[0] != 'c') continue;
                    if (!int.TryParse(n.AsSpan(1, 4), out int id)) continue;
                    used.Add(id);
                    files++;
                }
            }

        log($"skan: {rows} wierszy paramów, {files} plików chr, {used.Count} zajętych prefiksów");

        var free = new List<int>();
        for (int id = from; id <= to && free.Count < max; id++)
            if (!used.Contains(id)) free.Add(id);

        return free;
    }

    private static IEnumerable<string> ParamFiles(string regulationPath, Action<string> log)
    {
        if (!File.Exists(regulationPath))
        {
            log($"brak pliku regulation: {regulationPath}");
            yield break;
        }

        byte[] raw = File.ReadAllBytes(regulationPath);
        if (DCX.Is(raw)) raw = DCX.Decompress(raw);

        BND4 bnd;
        try { bnd = BND4.Read(raw); }
        catch (Exception e)
        {
            log($"nie da się otworzyć regulation: {e.Message}");
            yield break;
        }

        string temp = Path.Combine(Path.GetTempPath(), "PortGui", "params", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        foreach (var f in bnd.Files)
        {
            string n = f.Name.Replace('\\', '/');
            if (!n.StartsWith("param/", StringComparison.OrdinalIgnoreCase)) continue;
            if (!n.EndsWith(".param", StringComparison.OrdinalIgnoreCase)) continue;

            string dst = Path.Combine(temp, Path.GetFileName(n));
            File.WriteAllBytes(dst, f.Bytes);
            yield return dst;
        }
    }
}