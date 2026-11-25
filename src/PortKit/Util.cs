using SoulsFormats;

internal static class Util
{
    public static BND4 Open(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        if (DCX.Is(b)) b = DCX.Decompress(b);
        return BND4.Read(b);
    }

    public static void Save(BND4 bnd, string path)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        bnd.Write(full);
    }

    public static void RenameId(BND4 bnd, int from, int to)
    {
        string cf = $"c{from:D4}", ct = $"c{to:D4}";
        string af = $"a{from:D4}_", at = $"a{to:D4}_";

        foreach (var f in bnd.Files)
        {
            if (string.IsNullOrEmpty(f.Name)) continue;
            string n = f.Name;
            n = n.Replace(af, at, StringComparison.OrdinalIgnoreCase);
            n = n.Replace(cf, ct, StringComparison.OrdinalIgnoreCase);
            f.Name = n;
        }
    }

    public static string Sanitize(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in s.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString().Trim('_');
    }

    public static string? FindFile(string root, string fileName, int depth = 5)
    {
        if (!Directory.Exists(root)) return null;

        string direct = Path.Combine(root, fileName);
        if (File.Exists(direct)) return direct;
        if (depth <= 0) return null;

        try
        {
            foreach (string d in Directory.EnumerateDirectories(root))
            {
                string? hit = FindFile(d, fileName, depth - 1);
                if (hit != null) return hit;
            }
        }
        catch { }

        return null;
    }
}