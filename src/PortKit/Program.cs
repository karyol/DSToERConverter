internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
            return Help();

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "matclone" => MatClone.Run(args),
                "anibnd" => AniBnd.Run(args),
                "behbnd" => BehBnd.Run(args),
                "check" => Check(),
                "help" or "-h" or "--help" => Help(),
                _ => Unknown(args[0])
            };
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"BŁĄD: {e.Message}");
            return 1;
        }
    }

    private static int Check()
    {
        Console.WriteLine($"PortKit {typeof(Program).Assembly.GetName().Version}");
        Console.WriteLine($"SoulsFormatsNEXT: {typeof(SoulsFormats.BND4).Assembly.GetName().Version}");
        Console.WriteLine($"runtime: {Environment.Version}");
        return 0;
    }

    private static int Help()
    {
        Console.WriteLine("""
            PortKit — narzędzia pomocnicze portu DS3 -> ER

              matclone --matmap <csv> --er <dir> --out <dir> --model <id>
                        Klonuje materiały ER wskazane w matmap.csv i przepisuje sloty tekstur
                        na c<id>_<nazwa>_<slot>. Wynik: <out>\material\*.mtd

              anibnd   --src <ds3> --out <plik> --dst-id <id> [--er <dir> --donor <id>] [--keep-hkt]
                        Buduje anibnd ER. Z --donor bierze szkielet donora i przemianowuje ID.
                        Bez donora kopiuje źródło (szkielet DS3, hkx bez konwersji).

              behbnd   --src <ds3> --out <plik> --donor <id> --er <dir>
                        Buduje behbnd ER na behaviorze donora, przemianowuje ID.

              check    Wersje bibliotek.
            """);
        return 0;
    }

    private static int Unknown(string cmd)
    {
        Console.Error.WriteLine($"nieznana komenda: {cmd}");
        return Help() | 1;
    }
}