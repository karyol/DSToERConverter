using System.Text.Json;

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
    public string MatMap { get; set; } = "matmap.csv";
    public int LastSrcId { get; set; }
    public int IdFrom { get; set; } = 5000;
    public int IdTo { get; set; } = 5999;
    public int DonorId { get; set; } = 2130;
    public string BossName { get; set; } = "Pontiff Sulyvahn";

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PortGui", "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch { }

        var s = new Settings();
        try
        {
            string up = AppContext.BaseDirectory;
            s.FlverConv = FindUp(up, "FlverConv.exe") ?? "";
            s.PortKit = FindUp(up, "PortKit.exe") ?? "";
        }
        catch { }

        return s;
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