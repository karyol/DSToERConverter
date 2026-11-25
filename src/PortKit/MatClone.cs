using SoulsFormats;

internal static class MatClone
{
    public static int Run(string[] argv)
    {
        var a = Args.Parse(argv);
        string map = a.Require("matmap");
        string er = a.Require("er");
        string outDir = a.Require("out");
        string srcFlver = a.Require("src");
        int model = a.Int("model");
        int donor = a.Int("donor");

        if (!File.Exists(map))
            throw new FileNotFoundException($"brak matmap: {map}");
        if (!Directory.Exists(er))
            throw new DirectoryNotFoundException($"brak ER: {er}");
        if (!File.Exists(srcFlver))
            throw new FileNotFoundException($"brak flver: {srcFlver}");

        var rows = File.ReadAllLines(map)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("#"))
            .Select(l => l.Split(';'))
            .Where(p => p.Length >= 3)
            .Select(p => new Row(p[0].Trim(), p[1].Trim(), int.Parse(p[2].Trim())))
            .ToList();

        if (rows.Count == 0)
            throw new InvalidDataException("matmap bez wpisów");

        var flver = FLVER2.Read(srcFlver);
        string matOut = Path.Combine(outDir, "material");
        Directory.CreateDirectory(matOut);

        int ok = 0, fail = 0;

        foreach (var m in flver.Materials)
        {
            string mtd = Path.GetFileName(m.MTD.Replace('\\', '/'));
            var row = rows.FirstOrDefault(r => Wildcard(r.Ds3, mtd));
            if (row == null)
            {
                Console.Error.WriteLine($"  brak wzorca dla MTD {mtd} (materiał {m.Name})");
                fail++;
                continue;
            }

            string srcPath = row.Er.Replace("{CIDU}", $"C{donor:D4}").Replace("{CID}", $"c{donor:D4}");
            string outPath = row.Er.Replace("{CIDU}", $"C{model:D4}").Replace("{CID}", $"c{model:D4}");

            string matName = Util.Sanitize(Path.GetFileNameWithoutExtension(outPath));
            string outMtd = outPath.Substring(0, outPath.Length - ".matxml".Length);

            byte[]? raw = Locate(er, srcPath);
            if (raw == null)
            {
                Console.Error.WriteLine($"  brak matxml {srcPath} w ER");
                fail++;
                continue;
            }

            var xml = System.Text.Encoding.UTF8.GetString(raw);
            xml = RewriteTextures(xml, matName);

            string dst = Path.Combine(matOut, matName + ".matxml");
            File.WriteAllText(dst, xml, new System.Text.UTF8Encoding(false));

            Console.WriteLine($"MAT;{m.Name};{outMtd}.matxml;{row.Gx}");
            ok++;
        }

        Console.WriteLine($"gotowe: {ok} materiałów, {fail} błędów -> {matOut}");
        return fail == 0 ? 0 : 1;
    }

    private sealed record Row(string Ds3, string Er, int Gx);

    private static bool Wildcard(string pattern, string value)
    {
        string p = pattern.Replace("[ARSN]", "\u0001");
        var rx = new System.Text.RegularExpressions.Regex(
            "^" + System.Text.RegularExpressions.Regex.Escape(p).Replace("\u0001", "[A-Za-z0-9_]+") + "$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return rx.IsMatch(value);
    }

    private static string RewriteTextures(string xml, string matName)
    {
        var doc = System.Xml.Linq.XDocument.Parse(xml);
        int i = 0;
        foreach (var t in doc.Descendants("texture"))
        {
            string slot = (string?)t.Attribute("slot") ?? "";
            string suffix = Suffix(slot, i++);
            t.SetAttributeValue("path", $"{matName}{suffix}");
        }
        return doc.ToString();
    }

    private static byte[]? Locate(string er, string rel)
    {
        string file = Path.GetFileName(rel.Replace('\\', '/'));

        string? loose = Util.FindFile(Path.Combine(er, "material"), file, 3);
        if (loose != null)
            return File.ReadAllBytes(loose);

        string? any = Util.FindFile(er, file, 4);
        if (any != null)
            return File.ReadAllBytes(any);

        foreach (string bndName in new[] { "material.matbinbnd.dcx", "material.matbinbnd" })
        {
            string bnd = Path.Combine(er, bndName);
            if (!File.Exists(bnd))
                bnd = Path.Combine(er, "material", bndName);
            if (!File.Exists(bnd))
                continue;

            var b = Util.Open(bnd);
            var f = b.Files.FirstOrDefault(x =>
                Path.GetFileName(x.Name.Replace('\\', '/')).Equals(file, StringComparison.OrdinalIgnoreCase));
            return f?.Bytes;
        }

        return null;
    }

    private static string Suffix(string slot, int index)
    {
        string s = slot.ToLowerInvariant();
        if (s.Contains("albedo"))
            return "_a";
        if (s.Contains("normal"))
            return "_n";
        if (s.Contains("metallic"))
            return "_m";
        if (s.Contains("emissive"))
            return "_e";
        if (s.Contains("mask1"))
            return "_mask1";
        if (s.Contains("vector"))
            return "_v";
        if (s.Contains("distortion"))
            return "_d";
        return $"_{index}";
    }
}