internal sealed class Args
{
    private readonly Dictionary<string, string?> _v = new(StringComparer.OrdinalIgnoreCase);

    public static Args Parse(string[] argv)
    {
        var a = new Args();
        for (int i = 1; i < argv.Length; i++)
        {
            string k = argv[i];
            if (!k.StartsWith("--")) continue;

            string key = k[2..];
            bool hasValue = i + 1 < argv.Length && !argv[i + 1].StartsWith("--");
            a._v[key] = hasValue ? argv[++i] : null;
        }
        return a;
    }

    public string? Get(string key) => _v.TryGetValue(key, out var v) ? v : null;

    public string Require(string key) =>
        Get(key) ?? throw new ArgumentException($"brak --{key}");

    public bool Has(string key) => _v.ContainsKey(key);

    public int Int(string key) =>
        int.TryParse(Require(key), out int v) ? v : throw new ArgumentException($"złe --{key}");

    public int? IntOrNull(string key) =>
        Get(key) is string s && int.TryParse(s, out int v) ? v : null;
}