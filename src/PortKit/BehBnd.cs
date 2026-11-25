using SoulsFormats;

internal static class BehBnd
{
    public static int Run(string[] argv)
    {
        var a = Args.Parse(argv);
        string outPath = a.Require("out");
        int donor = a.Int("donor");
        string er = a.Require("er");

        string dp = Path.Combine(er, "chr", $"c{donor:D4}.behbnd.dcx");
        if (!File.Exists(dp)) dp = Path.Combine(er, "chr", $"c{donor:D4}.behbnd");
        if (!File.Exists(dp)) throw new FileNotFoundException($"brak behbnd donora: {dp}");

        var bnd = Util.Open(dp);
        Console.WriteLine($"donor behavior: {dp} ({bnd.Files.Count} plików)");

        Util.RenameId(bnd, donor, a.Int("dst-id"));

        foreach (var f in bnd.Files)
            Console.WriteLine($"  {f.Name}");

        Util.Save(bnd, outPath);
        Console.WriteLine($"zapisano {outPath}");
        Console.WriteLine("UWAGA: behavior donora ma własne ID animacji i eventów — " +
                          "ataki Pontyfika będą odtwarzać animacje donora do czasu retargetu c-animów.");
        return 0;
    }
}