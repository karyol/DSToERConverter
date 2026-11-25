using System.Text.Json;

internal enum AttachMode
{
    Smart,
    Master
}

internal sealed class Settings
{
    private static readonly EnumerationOptions Shallow = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        AttributesToSkip = 0
    };

    public string Ds3Dir { get; set; } = "";
    public string Ds3ParamSource { get; set; } = "";
    public string ErDir { get; set; } = "";
    public string ProjectDir { get; set; } = "";
    public string WitchyBnd { get; set; } = "witchyBND.exe";
    public string FlverConv { get; set; } = "";
    public string PortKit { get; set; } = "";
    public string DataDir { get; set; } = "";
    public string MatMap { get; set; } = "";
    public AttachMode Attach { get; set; } = AttachMode.Smart;
    public int LastSrcId { get; set; }
    public int IdFrom { get; set; } = 5000;
    public int IdTo { get; set; } = 5999;
    public int DonorId { get; set; } = 2130;
    public string BossName { get; set; } = "Pontiff Sulyvahn";

    public string LogsDir => DataDir.Length > 0
        ? Path.Combine(DataDir, "logs")
        : Path.Combine(Path.GetTempPath(), "PortGui");

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PortGui", "settings.json");

    public static Settings Load()
    {
        Settings s = new();
        try
        {
            if (File.Exists(FilePath))
                s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch { }

        s.Resolve();
        return s;
    }

    // Wszystkie ścieżki do zasobów repo są absolutne i zakotwiczone w katalogu, w którym
    // leży matmap.csv — niezależnie od katalogu roboczego procesu.
    public void Resolve()
    {
        try
        {
            if (DataDir.Length == 0 || !Directory.Exists(DataDir))
            {
                string? mm = FindUp(AppContext.BaseDirectory, "matmap.csv");
                DataDir = mm != null ? Path.GetDirectoryName(mm)! : "";
            }

            if (FlverConv.Length == 0 || !File.Exists(FlverConv))
                FlverConv = FindUp(AppContext.BaseDirectory, "FlverConv.exe") ?? "";

            if (PortKit.Length == 0 || !File.Exists(PortKit))
                PortKit = FindUp(AppContext.BaseDirectory, "PortKit.exe") ?? "";

            if (MatMap.Length == 0 || !File.Exists(MatMap))
                MatMap = DataDir.Length > 0 ? Path.Combine(DataDir, "matmap.csv") : "";
        }
        catch { }
    }

    public List<string> Validate()
    {
        var e = new List<string>();
        if (!File.Exists(FlverConv)) e.Add("FlverConv.exe nie znaleziony");
        if (!File.Exists(PortKit)) e.Add("PortKit.exe nie znaleziony");
        if (!File.Exists(MatMap)) e.Add("matmap.csv nie znaleziony");
        if (DataDir.Length == 0) e.Add("katalog data nie znaleziony");
        if (!Directory.Exists(Ds3Dir)) e.Add("katalog DS3 nie istnieje");
        if (!Directory.Exists(ErDir)) e.Add("katalog ER nie istnieje");
        return e;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static string? FindUp(string start, string file)
    {
        var d = new DirectoryInfo(start);
        for (int i = 0; i < 10 && d != null; i++, d = d.Parent)
        {
            string direct = Path.Combine(d.FullName, file);
            if (File.Exists(direct))
                return direct;

            try
            {
                foreach (string sub in Directory.EnumerateDirectories(d.FullName, "*", Shallow))
                {
                    string? hit = SearchUnder(sub, file, 4);
                    if (hit != null)
                        return hit;
                }
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException) { }
        }
        return null;
    }

    private static string? SearchUnder(string dir, string file, int depth)
    {
        if (depth <= 0)
            return null;

        try
        {
            string direct = Path.Combine(dir, file);
            if (File.Exists(direct))
                return direct;

            foreach (string sub in Directory.EnumerateDirectories(dir, "*", Shallow))
            {
                string? hit = SearchUnder(sub, file, depth - 1);
                if (hit != null)
                    return hit;
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException) { }

        return null;
    }
}