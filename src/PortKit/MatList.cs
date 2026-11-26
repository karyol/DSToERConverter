using SoulsFormats;

internal static class MatList
{
    private static readonly string[] BndNames =
    {
        "allmaterial.matbinbnd.dcx", "allmaterial.matbinbnd",
        "allmaterial_dlc01.matbinbnd.dcx", "allmaterial_dlc02.matbinbnd.dcx"
    };

    public static int Run(string[] argv)
    {
        var a = Args.Parse(argv);
        string er = a.Require("er");
        string? filter = a.Get("filter");

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

                var b = Util.Open(p);
                var hits = b.Files
                    .Select(f => Path.GetFileName(f.Name.Replace('\\', '/')))
                    .Where(n => filter == null || n.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                Console.WriteLine($"{Path.GetFileName(p)}: {b.Files.Count} wpisów, trafień {hits.Count}");
                foreach (string h in hits.Take(200))
                    Console.WriteLine($"  {h}");
                return 0;
            }
        }

        Console.Error.WriteLine("nie znaleziono allmaterial.matbinbnd.dcx");
        return 1;
    }
}