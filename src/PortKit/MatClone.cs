using System.Text;
using SoulsFormats;

internal static class MatClone
{
    public static int Run(string[] argv)
    {
        var a = Args.Parse(argv);
        string mapPath = a.Require("matmap");
        string er = a.Require("er");
        string outDir = a.Require("out");
        string srcFlver = a.Require("src");
        int model = a.Int("model");
        int donor = a.Int("donor");

        if (!File.Exists(mapPath))
            throw new FileNotFoundException($"brak matmap: {mapPath}");
        if (!Directory.Exists(er))
            throw new DirectoryNotFoundException($"brak ER: {er}");
        if (!File.Exists(srcFlver))
            throw new FileNotFoundException($"brak flver: {srcFlver}");

        var map = MatMap.Load(mapPath);
        var flver = FLVER2.Read(srcFlver);

        string matOut = Path.Combine(outDir, "material");
        Directory.CreateDirectory(matOut);

        int ok = 0, fail = 0;

        foreach (var m in flver.Materials)
        {
            string mtd = Path.GetFileName((m.MTD ?? "").Replace('\\', '/'));
            var row = map.Resolve(m.Name, mtd);
            if (row == null)
            {
                Console.Error.WriteLine($"  brak wzorca dla MTD {mtd} (materiał {m.Name})");
                fail++;
                continue;
            }

            string srcPath = Subst(row.Er, donor);
            string outPath = Subst(row.Er, model);

            string matName = Util.Sanitize(Path.GetFileNameWithoutExtension(outPath));
            string outMtd = outPath[..^".matxml".Length];

            byte[]? raw = Locate(er, srcPath);
            if (raw == null)
            {
                Console.Error.WriteLine($"  brak matxml {srcPath} w ER");
                fail++;
                continue;
            }

            string xml = RewriteTextures(Encoding.UTF8.GetString(raw), matName);
            File.WriteAllText(Path.Combine(matOut, matName + ".matxml"), xml, new UTF8Encoding(false));

            Console.WriteLine($"MAT;{m.Name};{outMtd}.matxml;{row.Gx}");
            ok++;
        }

        Console.WriteLine($"gotowe: {ok} materiałów, {fail} błędów -> {matOut}");
        return fail == 0 ? 0 : 1;
    }

    private static string Subst(string t, int id) => t
        .Replace("{CIDU}", $"C{id:D4}")
        .Replace("{CID}", $"c{id:D4}")
        .Replace("{ID}", $"{id:D4}");

    private static string RewriteTextures(string xml, string matName)
    {
        var doc = System.Xml.Linq.XDocument.Parse(xml);
        int i = 0;
        foreach (var t in doc.Descendants("texture"))
        {
            string slot = (string?)t.Attribute("slot") ?? "";
            t.SetAttributeValue("path", $"{matName}{Suffix(slot, i++)}");
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
            if (f != null)
                return f.Bytes;
        }

        return null;
    }

    private static string Suffix(string slot, int index)
    {
        string s = slot.ToLowerInvariant();
        if (s.Contains("albedo")) return "_a";
        if (s.Contains("normal")) return "_n";
        if (s.Contains("metallic")) return "_m";
        if (s.Contains("emissive")) return "_e";
        if (s.Contains("mask1")) return "_mask1";
        if (s.Contains("vector")) return "_v";
        if (s.Contains("distortion")) return "_d";
        return $"_{index}";
    }
}