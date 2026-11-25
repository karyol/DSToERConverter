using SoulsFormats;

internal static class AniBnd
{
    public static int Run(string[] argv)
    {
        var a = Args.Parse(argv);
        string src = a.Require("src");
        string outPath = a.Require("out");
        int dst = a.Int("dst-id");
        string? er = a.Get("er");
        int? donor = a.IntOrNull("donor");
        bool keepHkt = a.Has("keep-hkt");

        BND4 bnd;
        int fromId;
        bool donorMode = false;

        if (donor is int d && !string.IsNullOrWhiteSpace(er))
        {
            string dp = Path.Combine(er!, "chr", $"c{d:D4}.anibnd.dcx");
            if (!File.Exists(dp)) dp = Path.Combine(er!, "chr", $"c{d:D4}.anibnd");
            if (!File.Exists(dp)) throw new FileNotFoundException($"brak anibnd donora: {dp}");

            bnd = Util.Open(dp);
            fromId = d;
            donorMode = true;
            Console.WriteLine($"tryb donor: {dp}");
        }
        else
        {
            if (!File.Exists(src)) throw new FileNotFoundException($"brak źródła: {src}");
            bnd = Util.Open(src);
            fromId = ParseId(src);
            Console.WriteLine("tryb kopii źródła — szkielet DS3 bez konwersji hkx, animacje nie ruszą w ER");
        }

        Util.RenameId(bnd, fromId, dst);

        if (!keepHkt)
        {
            int removed = bnd.Files.RemoveAll(f =>
                f.Name.EndsWith(".hkt", StringComparison.OrdinalIgnoreCase) ||
                f.Name.EndsWith(".clm2", StringComparison.OrdinalIgnoreCase));
            if (removed > 0) Console.WriteLine($"usunięto {removed} plików hkt/clm2");
        }

        if (donorMode)
        {
            var skel = bnd.Files.FirstOrDefault(f =>
                f.Name.EndsWith(".hkx", StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileNameWithoutExtension(f.Name).StartsWith("c", StringComparison.OrdinalIgnoreCase));

            if (skel == null) throw new InvalidDataException("donor bez pliku szkieletu hkx");
            Console.WriteLine($"szkielet donora: {skel.Name} — FLVER musi mieć kości o tych samych nazwach i kolejności");
        }

        Util.Save(bnd, outPath);
        Console.WriteLine($"zapisano {outPath} ({bnd.Files.Count} plików)");
        return 0;
    }

    private static int ParseId(string path)
    {
        string n = Path.GetFileName(path);
        if (n.Length >= 5 && n[0] == 'c' && int.TryParse(n.AsSpan(1, 4), out int id)) return id;
        throw new ArgumentException($"nie da się odczytać ID z {n}");
    }
}