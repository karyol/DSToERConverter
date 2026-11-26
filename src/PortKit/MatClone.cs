using System.Text;
using System.Text.RegularExpressions;
using SoulsFormats;

internal static class MatClone
{
    private static readonly string[] BndNames =
    {
        "allmaterial.matbinbnd.dcx", "allmaterial.matbinbnd",
        "allmaterial_dlc01.matbinbnd.dcx", "allmaterial_dlc02.matbinbnd.dcx"
    };

    private static readonly string[] DonorFallback =
    {
        "fabric", "Cloak", "fabric_cloth", "Cloak_Cloth", "fabric_norich"
    };

    private static IBinder? _bnd;
    private static List<string>? _names;

    public static int Run(string[] argv)
    {
        var a = Args.Parse(argv);
        string mapPath = a.Require("matmap");
        string er = a.Require("er");
        string outDir = a.Require("out");
        string srcFlver = a.Require("src");
        int model = a.Int("model");
        int donor = a.Int("donor");
        string ext = a.Get("ext") ?? "matbin";

        if (!File.Exists(mapPath))
            throw new FileNotFoundException($"brak matmap: {mapPath}");
        if (!Directory.Exists(er))
            throw new DirectoryNotFoundException($"brak ER: {er}");
        if (!File.Exists(srcFlver))
            throw new FileNotFoundException($"brak flver: {srcFlver}");

        Console.WriteLine($"ER: {Path.GetFullPath(er)}");
        Console.WriteLine($"donor: c{donor:D4}   wyjście: *.{ext}");

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

            string outPath = Subst(row.Er, model);
            string matName = MatName(outPath);
            string outStem = outPath[..^".matxml".Length];

            (byte[] Bytes, string Name)? hit = null;
            foreach (string cand in CandidateNames(Path.GetFileName(outPath), donor))
            {
                hit = Locate(er, cand);
                if (hit != null)
                    break;
            }

            if (hit == null)
            {
                Console.Error.WriteLine($"  brak materiału donora dla {matName} (materiał {m.Name})");
                fail++;
                continue;
            }

            string srcName = Path.GetFileName(hit.Value.Name);
            byte[] blob = hit.Value.Bytes;

            if (srcName.EndsWith(".matbin", StringComparison.OrdinalIgnoreCase))
            {
                blob = ReplaceBytes(blob, $"c{donor:D4}", $"c{model:D4}");
                Console.WriteLine($"  {m.Name}: {srcName} -> {matName}.{ext} (matbin)");
            }
            else
            {
                blob = Encoding.UTF8.GetBytes(
                    RewriteTextures(Encoding.UTF8.GetString(blob), matName));
                Console.WriteLine($"  {m.Name}: {srcName} -> {matName}.{ext} (matxml)");
            }

            File.WriteAllBytes(Path.Combine(matOut, $"{matName}.{ext}"), blob);
            Console.WriteLine($"MAT;{m.Name};{outStem}.{ext};{row.Gx}");
            ok++;
        }

        Console.WriteLine($"gotowe: {ok} materiałów, {fail} błędów -> {matOut}");
        if (fail > 0)
            DumpCandidates(donor);

        return fail == 0 ? 0 : 1;
    }

    private static string Subst(string t, int id) => t
        .Replace("{CIDU}", $"C{id:D4}")
        .Replace("{CID}", $"c{id:D4}")
        .Replace("{ID}", $"{id:D4}");

    // zachowuje nawiasy formy C[cXXXX]_nazwa
    private static string MatName(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        var sb = new StringBuilder(stem.Length);
        foreach (char ch in stem)
            sb.Append(char.IsLetterOrDigit(ch) || ch is '_' or '-' or '[' or ']' ? ch : '_');
        return sb.ToString();
    }

    private static IEnumerable<string> CandidateNames(string outFile, int donor)
    {
        string d = $"c{donor:D4}";
        string stem = Path.GetFileNameWithoutExtension(outFile);
        var names = new List<string>();

        foreach (string ext in new[] { ".matbin", ".matxml" })
        {
            names.Add(Regex.Replace(stem, @"c\d{4}", d) + ext);
            foreach (string fb in DonorFallback)
            {
                names.Add($"C[{d}]_{fb}{ext}");
                names.Add($"{d}_{fb}{ext}");
            }
            foreach (string slot in new[] { "_000", "_100", "_200" })
                names.Add($"{d}{slot}{ext}");
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static byte[] ReplaceBytes(byte[] data, string from, string to)
    {
        byte[] f = Encoding.ASCII.GetBytes(from);
        byte[] t = Encoding.ASCII.GetBytes(to);
        if (f.Length != t.Length)
            throw new ArgumentException("podmiana musi mieć równą długość");

        for (int i = 0; i <= data.Length - f.Length; i++)
        {
            bool same = true;
            for (int j = 0; j < f.Length; j++)
                if (data[i + j] != f[j]) { same = false; break; }
            if (!same)
                continue;

            for (int j = 0; j < t.Length; j++)
                data[i + j] = t[j];
            i += f.Length - 1;
        }
        return data;
    }

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

    private static (byte[] Bytes, string Name)? Locate(string er, string file)
    {
        foreach (string sub in new[] { "material", "Material" })
        {
            string root = Path.Combine(er, sub);
            if (!Directory.Exists(root))
                continue;

            string? loose = Util.FindFile(root, file, 4);
            if (loose != null)
                return (File.ReadAllBytes(loose), loose);
        }

        string? any = Util.FindFile(er, file, 5);
        if (any != null)
            return (File.ReadAllBytes(any), any);

        var bnd = OpenBnd(er);
        if (bnd == null)
            return null;

        var entry = bnd.Files.FirstOrDefault(x =>
            Path.GetFileName(x.Name.Replace('\\', '/'))
                .Equals(file, StringComparison.OrdinalIgnoreCase));

        return entry?.Bytes is byte[] bytes ? (bytes, entry.Name) : null;
    }

    private static IBinder? OpenBnd(string er)
    {
        if (_bnd != null)
            return _bnd;

        foreach (string name in BndNames)
        {
            foreach (string p in new[]
                     {
                         Path.Combine(er, "material", name),
                         Path.Combine(er, "Material", name),
                         Path.Combine(er, name)
                     })
            {
                if (!File.Exists(p))
                    continue;

                try
                {
                    _bnd = Util.Open(p);
                    _names = _bnd.Files.Select(f => f.Name).ToList();
                    Console.WriteLine($"archiwum: {Path.GetFileName(p)} ({_names.Count} wpisów)");
                    return _bnd;
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"  nie otwiera {p}: {e.Message}");
                }
            }
        }

        return null;
    }

    private static void DumpCandidates(int donor)
    {
        if (_names == null)
            return;

        string d = donor.ToString();
        var hits = _names
            .Select(n => Path.GetFileName(n.Replace('\\', '/')))
            .Where(n => n.Contains(d, StringComparison.Ordinal))
            .Take(25)
            .ToList();

        Console.Error.WriteLine($"wpisy donora: {hits.Count}");
        foreach (string h in hits)
            Console.Error.WriteLine($"  {h}");
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