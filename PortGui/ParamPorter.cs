using SoulsFormats;
using System.Globalization;
using System.Text;

internal sealed class PortReport
{
    public List<string> Warnings { get; } = new();
    public List<string> Written { get; } = new();
    public List<(string Param, int From, int To)> Remaps { get; } = new();

    public void SaveCsv(string path)
    {
        var sb = new StringBuilder("typ;param;z;na\n");
        foreach (var w in Warnings)
            sb.Append("ostrzezenie;;").Append(w.Replace(';', ',')).Append('\n');
        foreach (var r in Remaps)
            sb.Append("remap;").Append(r.Param).Append(';').Append(r.From).Append(';').Append(r.To).Append('\n');
        foreach (var w in Written)
            sb.Append("zapis;").Append(w.Replace(';', ',')).Append('\n');
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }
}

internal static class ParamPorter
{
    private static readonly string[] Scoped =
    {
        "NpcParam", "NpcThinkParam", "NpcName", "NpcAiParam",
        "AtkParam_Npc", "Bullet", "BulletCreateLimitParam",
        "BehaviorParam", "BehaviorParam_PC", "ChrModelParam"
    };

    private static readonly string[] Referenced =
    {
        "SpEffectParam", "SpEffectVfxParam", "SpEffectSetParam"
    };

    public static async Task<PortReport> PortAsync(ParamSource src, string dstRegPath,
        int srcId, int dstId, int donorId, string bossName, Action<string> log, CancellationToken ct)
    {
        var report = new PortReport();

        byte[] dstRaw = File.ReadAllBytes(dstRegPath);
        if (DCX.Is(dstRaw))
            dstRaw = DCX.Decompress(dstRaw);
        var dstBnd = BND4.Read(dstRaw);

        var dstParams = new Dictionary<string, PARAM>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in dstBnd.Files)
        {
            string n = f.Name.Replace('\\', '/');
            if (!n.StartsWith("param/", StringComparison.OrdinalIgnoreCase))
                continue;

            string key = Path.GetFileNameWithoutExtension(n);
            try
            { dstParams[key] = PARAM.Read(f.Bytes); }
            catch (Exception e) { report.Warnings.Add($"dst {key}: {e.Message}"); }
        }

        // 1. zbierz wiersze prefiksowe
        var plan = new List<(string Param, PARAM Src, List<PARAM.Row> Rows)>();
        foreach (string name in Scoped)
        {
            ct.ThrowIfCancellationRequested();

            byte[]? b = src.GetParam(name);
            if (b == null)
            { report.Warnings.Add($"źródło bez paramu {name}"); continue; }

            PARAM p;
            try
            { p = PARAM.Read(b); }
            catch (Exception e) { report.Warnings.Add($"{name}: {e.Message}"); continue; }

            var rows = p.Rows.Where(r => r.ID >= 10000 && r.ID / 10000 == srcId).ToList();
            if (rows.Count == 0)
            { report.Warnings.Add($"{name}: brak wierszy dla prefiksu {srcId}"); continue; }

            plan.Add((name, p, rows));
            log($"{name}: {rows.Count} wierszy do przeniesienia");
        }

        // 2. domknij referencje SpEffect (kaskadowo, maks. 3 przejścia)
        var wanted = new HashSet<int>();
        var added = new HashSet<int>();

        for (int pass = 0; pass < 3; pass++)
        {
            int before = wanted.Count;

            foreach (var (_, _, rows) in plan)
                foreach (var r in rows)
                    foreach (var c in r.Cells)
                    {
                        if (!LooksLikeSpEffect(c.Def) || c.Value == null)
                            continue;
                        int v = System.Convert.ToInt32(c.Value, CultureInfo.InvariantCulture);
                        if (v > 0)
                            wanted.Add(v);
                    }

            if (wanted.Count == before && pass > 0)
                break;

            foreach (string name in Referenced)
            {
                byte[]? b = src.GetParam(name);
                if (b == null)
                    continue;

                PARAM p;
                try
                { p = PARAM.Read(b); }
                catch { continue; }

                var fresh = p.Rows.Where(r => wanted.Contains(r.ID) && added.Add(r.ID)).ToList();
                if (fresh.Count == 0)
                    continue;

                var existing = plan.FirstOrDefault(x => x.Param == name);
                if (existing.Rows == null)
                    plan.Add((name, p, fresh));
                else
                    existing.Rows.AddRange(fresh);
            }
        }

        if (wanted.Count > 0)
            log($"referencje SpEffect: {wanted.Count} ID do przeniesienia");

        // 3. alokuj remapy SpEffect przed kopiowaniem
        var remap = new Dictionary<int, int>();
        var usedSp = new HashSet<int>(
            dstParams.TryGetValue("SpEffectParam", out var sp)
                ? sp.Rows.Select(r => r.ID)
                : Enumerable.Empty<int>());

        foreach (int id in wanted.OrderBy(x => x))
        {
            int nv = id;
            while (usedSp.Contains(nv))
                nv++;
            usedSp.Add(nv);
            remap[id] = nv;
            if (nv != id)
                report.Remaps.Add(("SpEffectParam", id, nv));
        }

        // 4. zbuduj wiersze docelowe (jeszcze bez zapisu)
        var pending = new List<(PARAM Dst, PARAM.Row Row)>();

        foreach (var (name, _, rows) in plan)
        {
            ct.ThrowIfCancellationRequested();

            if (!dstParams.TryGetValue(name, out var dstParam))
            {
                report.Warnings.Add($"ER nie ma paramu {name} — pominięty");
                continue;
            }

            if (dstParam.AppliedParamdef == null)
            {
                report.Warnings.Add($"ER: {name} bez paramdefu — pominięty");
                continue;
            }

            // donor ze STRONY DOCELOWEJ, żeby zgadzał się paramdef
            var donorRows = dstParam.Rows
                .Where(r => r.ID >= 10000 && r.ID / 10000 == donorId)
                .ToList();

            bool scoped = Scoped.Contains(name, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < rows.Count; i++)
            {
                var s = rows[i];
                int newId = scoped
                    ? (dstId * 10000) + (s.ID % 10000)
                    : (remap.TryGetValue(s.ID, out int r) ? r : s.ID);

                dstParam.Rows.RemoveAll(x => x.ID == newId);

                PARAM.Row? donor = donorRows.Count > 0 ? donorRows[i % donorRows.Count] : null;

                var row = donor != null
                    ? new PARAM.Row(newId, donor.Name, dstParam.AppliedParamdef)
                    : new PARAM.Row(newId, s.Name, dstParam.AppliedParamdef);

                if (donor != null)
                    for (int k = 0; k < row.Cells.Count && k < donor.Cells.Count; k++)
                        row.Cells[k].Value = donor.Cells[k].Value;

                foreach (var sc in s.Cells)
                {
                    var dc = row.Cells.FirstOrDefault(c =>
                        string.Equals(c.Def.InternalName, sc.Def.InternalName, StringComparison.OrdinalIgnoreCase));

                    if (dc == null)
                    {
                        if (sc.Value != null && !IsDefault(sc.Value))
                            report.Warnings.Add($"{name} #{s.ID}: pole '{sc.Def.InternalName}' nie istnieje w ER (wartość {sc.Value})");
                        continue;
                    }

                    if (!Compatible(dc.Def, sc.Def))
                    {
                        report.Warnings.Add($"{name} #{s.ID}: pole '{sc.Def.InternalName}' inny typ/rozmiar");
                        continue;
                    }

                    object? v = sc.Value;

                    if (LooksLikeSpEffect(sc.Def) && v != null)
                    {
                        int old = System.Convert.ToInt32(v, CultureInfo.InvariantCulture);
                        if (remap.TryGetValue(old, out int nv))
                            v = nv;
                    }

                    dc.Value = Clamp(Conv.ByDef(v, sc.Def, dc.Def), dc.Def);
                }

                if (name.Equals("NpcName", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(bossName))
                    SetText(row, "name", bossName);

                dstParam.Rows.Add(row);
                pending.Add((dstParam, row));
                report.Written.Add($"{name} {newId}");
            }

            dstParam.Rows.Sort((a, b) => a.ID.CompareTo(b.ID));
            log($"{name}: przygotowano {rows.Count}");
        }

        // 5. flagi bossa
        if (dstParams.TryGetValue("NpcParam", out var np))
        {
            int bossRowId = dstId * 10000;
            var row = np.Rows.FirstOrDefault(r => r.ID == bossRowId);

            if (row == null)
                report.Warnings.Add($"NpcParam #{bossRowId}: brak wiersza do ustawienia flag");
            else
                foreach (string f in new[] { "isBoss", "npcType", "teamType", "bossType" })
                {
                    var c = row.Cells.FirstOrDefault(x =>
                        string.Equals(x.Def.InternalName, f, StringComparison.OrdinalIgnoreCase));
                    if (c == null)
                        continue;

                    if (f == "isBoss" && c.Def.DisplayType == PARAMDEF.DefType.b32)
                        c.Value = true;
                    else if (f == "npcType" && c.Def.DisplayType == PARAMDEF.DefType.s8)
                        c.Value = (sbyte)1;

                    log($"NpcParam #{bossRowId}: ustawiono {f}");
                }
        }

        // 6. dopiero teraz zapis do BND
        foreach (var group in pending.GroupBy(p => p.Dst))
        {
            var name = dstParams.First(kv => ReferenceEquals(kv.Value, group.Key)).Key;
            var file = dstBnd.Files.First(f =>
                Path.GetFileNameWithoutExtension(f.Name.Replace('\\', '/'))
                    .Equals(name, StringComparison.OrdinalIgnoreCase));

            file.Bytes = group.Key.Write();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(dstRegPath)!);
        File.WriteAllBytes(dstRegPath, dstBnd.Write());
        log($"zapisano regulation: {dstRegPath}");

        await Task.CompletedTask;
        return report;
    }

    private static bool LooksLikeSpEffect(PARAMDEF.Field f) =>
        f.InternalName.Contains("spEffect", StringComparison.OrdinalIgnoreCase) ||
        f.InternalName.Contains("refId", StringComparison.OrdinalIgnoreCase);

    private static bool IsDefault(object v) =>
        v is int i ? i == 0 || i == -1
        : v is uint u ? u == 0 || u == uint.MaxValue
        : v is short s ? s == 0 || s == -1
        : v is float f ? Math.Abs(f) < 1e-6
        : v is bool b && !b;

    private static bool Compatible(PARAMDEF.Field a, PARAMDEF.Field b) =>
        a.BitSize == b.BitSize && a.ArrayLength == b.ArrayLength && a.DisplayType == b.DisplayType;

    private static object? Clamp(object? v, PARAMDEF.Field def)
    {
        if (v == null || v is string)
            return v;
        if (v is not (int or uint or short or ushort or sbyte or byte or float or bool))
            return v;

        double d = System.Convert.ToDouble(v, CultureInfo.InvariantCulture);
        d = Math.Clamp(d, Bound(def.Minimum, double.MinValue), Bound(def.Maximum, double.MaxValue));

        return def.DisplayType switch
        {
            PARAMDEF.DefType.s8 => (sbyte)d,
            PARAMDEF.DefType.u8 => (byte)d,
            PARAMDEF.DefType.s16 => (short)d,
            PARAMDEF.DefType.u16 => (ushort)d,
            PARAMDEF.DefType.s32 => (int)d,
            PARAMDEF.DefType.u32 => (uint)d,
            PARAMDEF.DefType.f32 => (float)d,
            PARAMDEF.DefType.b32 => d != 0,
            _ => v
        };
    }

    private static double Bound(object? value, double fallback)
    {
        if (value == null)
            return fallback;
        try
        { return System.Convert.ToDouble(value, CultureInfo.InvariantCulture); }
        catch { return fallback; }
    }

    private static void SetText(PARAM.Row row, string field, string text)
    {
        var c = row.Cells.FirstOrDefault(x =>
            string.Equals(x.Def.InternalName, field, StringComparison.OrdinalIgnoreCase));
        if (c != null)
            c.Value = text;
    }

    private static class Conv
    {
        public static object? ByDef(object? v, PARAMDEF.Field from, PARAMDEF.Field to)
        {
            if (v == null || v is string)
                return v;
            if (from.ArrayLength > 1)
                return v;
            if (from.DisplayType == to.DisplayType)
                return v;

            double d = System.Convert.ToDouble(v, CultureInfo.InvariantCulture);

            return to.DisplayType switch
            {
                PARAMDEF.DefType.s8 => (sbyte)d,
                PARAMDEF.DefType.u8 => (byte)d,
                PARAMDEF.DefType.s16 => (short)d,
                PARAMDEF.DefType.u16 => (ushort)d,
                PARAMDEF.DefType.s32 => (int)d,
                PARAMDEF.DefType.u32 => (uint)d,
                PARAMDEF.DefType.f32 => (float)d,
                PARAMDEF.DefType.b32 => d != 0,
                _ => v
            };
        }
    }
}