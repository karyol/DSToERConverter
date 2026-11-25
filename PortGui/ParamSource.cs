using SoulsFormats;
using System.Reflection;

/// Nośnik parametrów DS3: folder param/, regulation.bin, Data0.bdt albo para BHD5/BDT.
internal sealed class ParamSource
{
    private static readonly EnumerationOptions Deep = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        AttributesToSkip = 0
    };

    private readonly Dictionary<string, byte[]> _params = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _defs = new(StringComparer.OrdinalIgnoreCase);

    public string Origin { get; private set; } = "";
    public int ParamCount => _params.Count;
    public IEnumerable<string> Names => _params.Keys;

    public static ParamSource? Resolve(string ds3Dir, string explicitPath, Action<string> log)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(explicitPath))
            candidates.Add(explicitPath);

        if (!string.IsNullOrWhiteSpace(ds3Dir) && Directory.Exists(ds3Dir))
        {
            candidates.Add(Path.Combine(ds3Dir, "param"));
            candidates.Add(Path.Combine(ds3Dir, "regulation.bin"));
            candidates.Add(Path.Combine(ds3Dir, "Data0.bdt"));
            candidates.Add(Path.Combine(ds3Dir, "Data0.bhd"));

            foreach (string f in SafeFind(ds3Dir, "regulation.bin"))
                candidates.Add(f);
            foreach (string f in SafeFind(ds3Dir, "Data0.bdt"))
                candidates.Add(f);
        }

        foreach (string c in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(c) && !File.Exists(c))
                continue;

            log($"próbuję źródło paramów: {c}");
            try
            {
                var src = Load(c, log);
                if (src != null && src.ParamCount > 0)
                {
                    log($"źródło paramów OK: {src.Origin} ({src.ParamCount} paramów)");
                    return src;
                }
            }
            catch (Exception e) { log($"  odrzucone: {e.Message}"); }
        }

        log("NIE znaleziono źródła paramów DS3. Oczekiwane: <DS3>\\param\\, <DS3>\\regulation.bin, " +
            "<DS3>\\Data0.bdt, <DS3>\\Data0.bhd+.bdt albo Data0.bdt z projektu Smithboxa.");
        log("Najszybciej: otwórz projekt DS3 w Smithboxie, zmień dowolny param, zapisz — " +
            "powstały Data0.bdt wskaż w polu 'Paramy DS3'.");
        return null;
    }

    private static ParamSource Load(string path, Action<string> log)
    {
        var s = new ParamSource();

        if (Directory.Exists(path))
        {
            s.Origin = path;
            foreach (string f in Directory.EnumerateFiles(path, "*.param", Deep))
                s._params[Norm(Path.GetFileNameWithoutExtension(f))] = File.ReadAllBytes(f);

            string defDir = Path.GetFullPath(Path.Combine(path, "..", "paramdef"));
            if (Directory.Exists(defDir))
                foreach (string f in Directory.EnumerateFiles(defDir, "*.paramdef", Deep))
                    s._defs[Path.GetFileNameWithoutExtension(f)] = File.ReadAllBytes(f);

            return s;
        }

        string ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext == ".bhd")
            return LoadBhd5(path, s, log);

        byte[] raw = File.ReadAllBytes(path);
        if (DCX.Is(raw))
            raw = DCX.Decompress(raw);

        var bnd = BND4.Read(raw);
        s.Origin = path;

        foreach (var f in bnd.Files)
        {
            string n = Norm(f.Name);
            if (n.StartsWith("param/"))
                s._params[Path.GetFileNameWithoutExtension(n)] = f.Bytes;
            else if (n.StartsWith("paramdef/"))
                s._defs[Path.GetFileNameWithoutExtension(n)] = f.Bytes;
        }

        return s;
    }

    private static ParamSource LoadBhd5(string bhdPath, ParamSource s, Action<string> log)
    {
        string bdtPath = Path.ChangeExtension(bhdPath, ".bdt");
        if (!File.Exists(bdtPath))
            throw new FileNotFoundException("brak pary .bdt");

        s.Origin = bhdPath + " + " + Path.GetFileName(bdtPath);

        byte[] bhdBytes = File.ReadAllBytes(bhdPath);
        var bhd5 = ReadBhd5(bhdBytes);

        using var bdt = File.OpenRead(bdtPath);
        int n = 0;

        foreach (var f in EnumerateFiles(bhd5))
        {
            byte[]? b;
            try
            { b = ReadBhd5File(f, bdt); }
            catch { continue; }
            if (b == null || b.Length < 4)
                continue;
            if (b[0] != 'P' || b[1] != 'A' || b[2] != 'R' || b[3] != 'A')
                continue;

            string? name;
            try
            { name = PARAM.Read(b).AppliedParamdef?.ParamType; }
            catch { continue; }
            if (string.IsNullOrWhiteSpace(name))
                continue;

            s._params[Norm(name)] = b;
            if (++n % 40 == 0)
                log($"  ...{n} paramów");
        }

        return s;
    }

    private static BHD5 ReadBhd5(byte[] bhdBytes)
    {
        foreach (var m in typeof(BHD5).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (m.Name != "Read")
                continue;

            var ps = m.GetParameters();
            if (ps.Length != 2 || ps[1].ParameterType != typeof(BHD5.Game))
                continue;

            object? arg;
            if (ps[0].ParameterType == typeof(byte[]))
                arg = bhdBytes;
            else if (ps[0].ParameterType == typeof(Stream))
                arg = new MemoryStream(bhdBytes);
            else
                continue;

            try
            { return (BHD5)m.Invoke(null, new[] { arg, BHD5.Game.DarkSouls3 })!; }
            catch { }
        }

        throw new MissingMethodException("BHD5.Read(Stream|byte[], Game) nie istnieje w tej wersji SoulsFormatsNEXT");
    }

    private static IEnumerable<object> EnumerateFiles(BHD5 bhd5)
    {
        var t = bhd5.GetType();
        object? buckets = null;

        foreach (string pn in new[] { "BucketHeaders", "Buckets", "BucketHeader" })
        {
            buckets = t.GetProperty(pn)?.GetValue(bhd5);
            if (buckets is System.Collections.IEnumerable)
                break;
        }

        if (buckets is not System.Collections.IEnumerable be)
            yield break;

        foreach (var bucket in be)
        {
            if (bucket == null)
                continue;

            object? files = null;
            foreach (string pn in new[] { "Files", "FileHeaders", "FileHeader" })
            {
                files = bucket.GetType().GetProperty(pn)?.GetValue(bucket);
                if (files is System.Collections.IEnumerable)
                    break;
            }

            if (files is not System.Collections.IEnumerable fe)
                continue;

            foreach (var f in fe)
                if (f != null)
                    yield return f;
        }
    }

    private static byte[]? ReadBhd5File(object header, Stream bdt)
    {
        var t = header.GetType();

        var m = t.GetMethod("ReadFile", new[] { typeof(Stream) });
        if (m != null)
        {
            try
            { return (byte[]?)m.Invoke(header, new object[] { bdt }); }
            catch { }
        }

        PropertyInfo? offP = null, sizeP = null;
        foreach (string n in new[] { "Offset", "FileOffset" })
            offP ??= t.GetProperty(n);
        foreach (string n in new[] { "Size", "FileSize" })
            sizeP ??= t.GetProperty(n);

        if (offP == null || sizeP == null)
            return null;

        long off = System.Convert.ToInt64(offP.GetValue(header));
        int size = System.Convert.ToInt32(sizeP.GetValue(header));
        if (size <= 0)
            return null;

        var buf = new byte[size];
        bdt.Seek(off, SeekOrigin.Begin);

        int read = 0;
        while (read < size)
        {
            int chunk = bdt.Read(buf, read, size - read);
            if (chunk <= 0)
                break;
            read += chunk;
        }

        return read == size ? buf : null;
    }

    public byte[]? GetParam(string name) =>
        _params.TryGetValue(name, out var b) ? b : null;

    public PARAMDEF? GetDef(string name)
    {
        if (!_defs.TryGetValue(name, out var b))
            return null;
        try
        { return PARAMDEF.Read(b); }
        catch { return null; }
    }

    private static string Norm(string n) => n.Replace('\\', '/').TrimStart('/');

    private static IEnumerable<string> SafeFind(string root, string file)
    {
        try
        {
            return Directory.EnumerateFiles(root, file, new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = 0
            }).ToList();
        }
        catch { return Array.Empty<string>(); }
    }
}