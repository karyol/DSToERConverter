// FlverConv — konwersja FLVER 0x20014 (DS3) -> 0x2001A (ER)
// Użycie: FlverConv <in.flver> <out.flver> <matmap.csv> [--model 9010] [--audit raport.txt]
//         [--no-hash] [--rename-dummies] [--attach-orphans|--attach-master] [--rebuild-links] [--dump-mats]
using SoulsFormats;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

internal static class Program
{
    private const int SrcVersion = 0x20014;
    private const int DstVersion = 0x2001A;

    private static readonly string[] BoneOnlyHints =
    {
        "Finger", "Collar", "Lip", "Cheek", "Jaw", "Tongue", "Blink", "Brow",
        "Nostril", "Eye", "Ear", "Shoulder", "Elbow", "Wing"
    };

    private static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine(
                "FlverConv <in.flver> <out.flver> <matmap.csv> [--model 9010] [--audit raport.txt] " +
                "[--no-hash] [--rename-dummies] [--attach-orphans|--attach-master] [--rebuild-links] [--dump-mats]");
            return 2;
        }

        string inPath = args[0];
        string outPath = args[1];
        string mapPath = args[2];

        if (!File.Exists(inPath))
        {
            Console.Error.WriteLine($"brak pliku wejściowego: {inPath}");
            return 2;
        }

        int model;
        try
        {
            string? modelArg = GetOpt(args, "--model");
            model = modelArg != null
                ? ParseModel(modelArg)
                : ParseModel(Path.GetFileNameWithoutExtension(outPath));
        }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine(e.Message);
            return 2;
        }

        string? auditPath = GetOpt(args, "--audit");
        bool hashDummies = !args.Contains("--no-hash");
        bool renameDummies = args.Contains("--rename-dummies");
        bool attachSmart = args.Contains("--attach-orphans");
        bool attachMaster = args.Contains("--attach-master");
        bool attach = attachSmart || attachMaster;
        bool rebuildLinks = args.Contains("--rebuild-links") || attach;
        bool dumpMats = args.Contains("--dump-mats");

        FLVER2 f = FLVER2.Read(inPath);
        if (f.Header.Version != SrcVersion)
            Console.Error.WriteLine($"uwaga: wersja {f.Header.Version:X}, oczekiwano {SrcVersion:X}");

        Console.WriteLine($"model docelowy: c{model:D4}");
        Console.WriteLine($"tryb stringów wejścia: Unicode={f.Header.Unicode}");

        if (f.Nodes.Count > short.MaxValue)
        {
            Console.Error.WriteLine(
                $"błąd: {f.Nodes.Count} węzłów przekracza zakres short ({short.MaxValue})");
            return 3;
        }

        int repaired = NormalizeNames(f);
        if (repaired > 0)
            Console.WriteLine($"naprawiono nazw: {repaired}");

        var srcMtd = f.Materials.Select(m => m.MTD ?? "").ToArray();

        f.Header.Version = DstVersion;
        f.Header.Unicode = true;
        f.Header.BigEndian = false;

        var map = LoadMatMap(mapPath);

        var meshNodes = new HashSet<int>(f.Meshes.Select(m => m.NodeIndex));
        var dummyParents = new HashSet<int>(f.Dummies
            .SelectMany(d => new[] { (int)d.ParentBoneIndex, (int)d.AttachBoneIndex })
            .Where(i => i >= 0));

        if (renameDummies)
            RenameDummyNodes(f);

        int disabledFirst = FindDisabledBlockStart(f);

        for (int i = 0; i < f.Nodes.Count; i++)
        {
            FLVER.Node n = f.Nodes[i];

            if (disabledFirst >= 0 && i >= disabledFirst)
            {
                n.Flags = FLVER.Node.NodeFlags.Disabled;
                continue;
            }

            bool leafByName = BoneOnlyHints.Any(h =>
                n.Name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0);
            bool branching = f.Nodes.Any(o => o.ParentIndex == i) || dummyParents.Contains(i);

            if (meshNodes.Contains(i))
            {
                n.Flags = FLVER.Node.NodeFlags.Mesh;
                if (!leafByName && branching)
                    n.Flags |= FLVER.Node.NodeFlags.DummyOwner;
                continue;
            }

            n.Flags = FLVER.Node.NodeFlags.Bone;
            if (!leafByName && branching)
                n.Flags |= FLVER.Node.NodeFlags.DummyOwner;
        }

        // attach musi iść po flagach, inaczej nie rozpozna siatek
        if (attach)
        {
            int moved = attachMaster
                ? AttachOrphansToMaster(f)
                : AttachOrphans(f, meshNodes);
            Console.WriteLine($"podpięto osieroconych węzłów: {moved}");
        }
        if (rebuildLinks)
            RebuildLinks(f);

        f.GXLists.Clear();
        int gxCount = 1;
        foreach (var e in map.Entries)
            if (e.GX + 1 > gxCount)
                gxCount = e.GX + 1;
        for (int i = 0; i < gxCount; i++)
            f.GXLists.Add(new FLVER2.GXList());

        if (dumpMats)
        {
            Console.WriteLine("materiały źródłowe:");
            for (int i = 0; i < f.Materials.Count; i++)
                Console.WriteLine($"  [{i}] name='{f.Materials[i].Name}' mtd='{srcMtd[i]}'");
        }

        string[] keys = f.Materials.Select((m, i) => Path.GetFileName(srcMtd[i])).ToArray();
        var resolvedMtd = new string?[f.Materials.Count];
        var resolvedGx = new int[f.Materials.Count];
        int unmatched = 0;

        for (int i = 0; i < f.Materials.Count; i++)
        {
            FLVER2.Material m = f.Materials[i];

            MapEntry? hit = null;
            string via = "";
            if (map.TryMatchName(m.Name ?? "", out MapEntry ne))
            {
                hit = ne;
                via = "nazwa";
            }
            else if (map.TryMatchMtd(keys[i], out MapEntry me))
            {
                hit = me;
                via = "mtd";
            }

            if (hit != null)
            {
                string raw = Resolve(hit.Path, model);
                resolvedMtd[i] = NormalizeMtd(raw, out bool changed);
                if (changed)
                    Console.WriteLine($"  [{i}] ścieżka poprawiona: {raw} -> {resolvedMtd[i]}");
                resolvedGx[i] = hit.GX;
                Console.WriteLine($"  [{i}] {keys[i]} -> {resolvedMtd[i]} (gx {resolvedGx[i]}, po {via})");
            }
            else
            {
                unmatched++;
                resolvedMtd[i] = Resolve(
                    $"N:\\GR\\data\\Material\\mtd\\Chr\\matxml\\C[{{CID}}]_{i:D3}.matxml", model);
                resolvedGx[i] = 0;
                Console.Error.WriteLine(
                    $"  brak wpisu: [{i}] name='{m.Name}' mtd='{srcMtd[i]}' key='{keys[i]}'");
            }
        }

        // matbin per wynikowe MTD: duplikaty MTD dzielą slot, różne MTD dostają własny
        var matbin = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < f.Materials.Count; i++)
        {
            string mtd = resolvedMtd[i]!;
            if (!matbin.TryGetValue(mtd, out int idx))
            {
                idx = matbin.Count;
                matbin[mtd] = idx;
            }

            FLVER2.Material m = f.Materials[i];
            m.MTD = mtd;
            m.Index = idx;
            m.GXIndex = resolvedGx[i] >= 0 && resolvedGx[i] < gxCount ? resolvedGx[i] : 0;
        }

        // ER trzyma ścieżki tekstur puste — materiał definiuje matbin, nie FLVER
        foreach (FLVER2.Material m in f.Materials)
            foreach (FLVER2.Texture t in m.Textures)
                t.Path = "";

        if (unmatched > 0)
            Console.Error.WriteLine(
                $"UWAGA: {unmatched} materiałów bez wpisu w matmap.csv — użyto ścieżek zastępczych");

        if (hashDummies)
            HashDummies(f);

        BuildSkeletons(f);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        f.Write(outPath);

        int disabled = f.Nodes.Count(n => n.Flags == FLVER.Node.NodeFlags.Disabled);
        Console.WriteLine($"zapisano: {outPath}");
        Console.WriteLine($"  węzły={f.Nodes.Count} (Disabled={disabled}) siatki={f.Meshes.Count} " +
                          $"materiały={f.Materials.Count} dummies={f.Dummies.Count} GXLists={f.GXLists.Count}");

        if (auditPath != null)
        {
            WriteAudit(auditPath, f, srcMtd, meshNodes);
            Console.WriteLine($"audit: {auditPath}");
        }

        Console.WriteLine("weryfikacja round-trip:");
        bool ok = Verify(outPath, f.Nodes.Count, disabled, f.Dummies.Count, hashDummies, meshNodes);
        return ok ? 0 : 1;
    }

    private static short S(int v)
    {
        if (v < short.MinValue || v > short.MaxValue)
            throw new InvalidOperationException($"indeks {v} nie mieści się w short");
        return (short)v;
    }

    private static string Resolve(string template, int model) => template
        .Replace("{CID}", $"c{model:D4}")
        .Replace("{CIDU}", $"C{model:D4}")
        .Replace("{ID}", $"{model:D4}");

    private static string NormalizeMtd(string path, out bool changed)
    {
        changed = false;
        string dir = Path.GetDirectoryName(path) ?? "";
        string file = Path.GetFileName(path);
        string ext = Path.GetExtension(file);
        string stem = Path.GetFileNameWithoutExtension(file);

        // forma ER: C[c9010]_nazwa — nawiasy zostają
        Match er = Regex.Match(stem, @"^C\[c(\d{1,4})\](_.+)?$", RegexOptions.IgnoreCase);
        string norm;
        if (er.Success)
        {
            norm = $"C[c{er.Groups[1].Value.PadLeft(4, '0')}]{er.Groups[2].Value}";
        }
        else
        {
            string stripped = stem.Replace("[", "").Replace("]", "");
            Match m = Regex.Match(stripped, @"^[Cc]+(\d{1,4})(_.*)?$");
            norm = m.Success
                ? "c" + m.Groups[1].Value.PadLeft(4, '0') + (m.Groups[2].Value ?? "")
                : stripped.ToLowerInvariant();
        }

        string result = Path.Combine(dir, norm + ext).Replace('/', '\\');
        changed = !string.Equals(result, path, StringComparison.Ordinal);
        return result;
    }

    private static int ParseModel(string s)
    {
        string t = s.Trim().TrimStart('c', 'C');
        if (!int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            || v <= 0 || v > 9999)
            throw new ArgumentException($"zły numer modelu: '{s}' (oczekiwano 1..9999 lub cXXXX)");
        return v;
    }

    private static string? GetOpt(string[] args, string key)
    {
        int i = Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static bool Verify(string path, int expectedNodes, int expectedDisabled,
                               int expectedDummies, bool expectHashes, HashSet<int> meshNodes)
    {
        FLVER2 r = FLVER2.Read(path);
        bool ok = true;

        void Check(string label, bool cond)
        {
            Console.WriteLine($"  {(cond ? "OK  " : "FAIL")} {label}");
            if (!cond)
                ok = false;
        }

        Check($"węzły={r.Nodes.Count} (oczekiwano {expectedNodes})", r.Nodes.Count == expectedNodes);
        Check("0 węzłów z flagami 0", r.Nodes.All(n => n.Flags != 0));
        Check($"Disabled={r.Nodes.Count(n => n.Flags == FLVER.Node.NodeFlags.Disabled)} (oczekiwano {expectedDisabled})",
            r.Nodes.Count(n => n.Flags == FLVER.Node.NodeFlags.Disabled) == expectedDisabled);
        Check("siatki mają parent 0",
            r.Meshes.All(m => m.NodeIndex < r.Nodes.Count
                              && r.Nodes[m.NodeIndex].ParentIndex == 0));
        Check("siatki nie mają DummyOwner",
            r.Meshes.All(m => m.NodeIndex < r.Nodes.Count
                              && !r.Nodes[m.NodeIndex].Flags.HasFlag(FLVER.Node.NodeFlags.DummyOwner)));
        Check("SkeletonSet: BaseSkeleton == AllSkeletons == Nodes",
            r.Skeletons != null
            && r.Skeletons.BaseSkeleton.Count == r.Nodes.Count
            && r.Skeletons.AllSkeletons.Count == r.Nodes.Count);
        Check("SkeletonSet: NodeIndex zgodny z pozycją",
            r.Skeletons != null
            && r.Skeletons.BaseSkeleton.Select((b, i) => b.NodeIndex == i).All(x => x)
            && r.Skeletons.AllSkeletons.Select((b, i) => b.NodeIndex == i).All(x => x));
        Check("SkeletonSet: indeksy w zakresie lub -1",
            r.Skeletons != null
            && r.Skeletons.BaseSkeleton.All(b =>
                b.ParentIndex < r.Nodes.Count && b.FirstChildIndex < r.Nodes.Count
                && b.NextSiblingIndex < r.Nodes.Count && b.PreviousSiblingIndex < r.Nodes.Count));
        Check($"GXIndex w zakresie 0..{r.GXLists.Count - 1}",
            r.GXLists.Count > 0 && r.Materials.All(m => m.GXIndex >= 0 && m.GXIndex < r.GXLists.Count));
        Check("wszystkie MTD w katalogu Chr\\matxml",
            r.Materials.All(m => m.MTD.Contains("\\Material\\mtd\\Chr\\matxml\\")));
        Check("MTD w formacie C[cXXXX]_nazwa.matxml",
            r.Materials.All(m => Regex.IsMatch(Path.GetFileName(m.MTD),
                @"^C\[c\d{4}\]_[a-z0-9_]+\.matxml$", RegexOptions.IgnoreCase)));
        Check("ścieżki tekstur puste",
            r.Materials.All(m => m.Textures.All(t => t.Path.Length == 0)));
        Check("brak dwóch materiałów z tym samym MTD w różnym matbinie",
            r.Materials.GroupBy(m => m.Index).All(g => g.Select(x => x.MTD).Distinct().Count() == 1));
        Check($"dummies={r.Dummies.Count} (oczekiwano {expectedDummies})",
            r.Dummies.Count == expectedDummies);
        if (expectHashes)
        {
            Check("NameHash niezerowy", r.Dummies.Select(d => d.NameHash).All(h => h != 0));
            Check("NameHash zgodny w obrębie (parent, refID)",
                r.Dummies.GroupBy(d => (d.ParentBoneIndex, d.ReferenceID))
                    .All(g => g.Select(x => x.NameHash).Distinct().Count() == 1));
        }
        Check("rodzice dummy'ów w zakresie węzłów",
            r.Dummies.All(d => d.ParentBoneIndex < 0 || d.ParentBoneIndex < r.Nodes.Count));
        Check("AttachBoneIndex w zakresie węzłów",
            r.Dummies.All(d => d.AttachBoneIndex < 0 || d.AttachBoneIndex < r.Nodes.Count));
        Check("Mesh NodeIndex w zakresie węzłów",
            r.Meshes.All(m => m.NodeIndex >= 0 && m.NodeIndex < r.Nodes.Count));
        Check("brak NUL w nazwach węzłów", r.Nodes.All(n => !n.Name.Contains('\0')));
        Check("węzeł 0 nadal nazywa się Master",
            r.Nodes.Count > 0 && r.Nodes[0].Name.Equals("Master", StringComparison.OrdinalIgnoreCase));
        Check("wszystkie dzieci mają zgodne prev/next",
            Enumerable.Range(0, r.Nodes.Count).All(p =>
            {
                FLVER.Node pn = r.Nodes[p];
                if (pn.FirstChildIndex < 0)
                    return true;
                int prev = -1;
                for (int c = pn.FirstChildIndex; c >= 0 && c < r.Nodes.Count;
                     c = r.Nodes[c].NextSiblingIndex)
                {
                    if (r.Nodes[c].ParentIndex != p || r.Nodes[c].PreviousSiblingIndex != prev)
                        return false;
                    prev = c;
                }
                return true;
            }));
        return ok;
    }

    private static void WriteAudit(string path, FLVER2 f, string[] srcMtd, HashSet<int> meshNodes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# audit model cXXX nodes={f.Nodes.Count} meshes={f.Meshes.Count} " +
                      $"materials={f.Materials.Count} dummies={f.Dummies.Count}");
        sb.AppendLine("# węzły: idx | flags | parent | child | next | prev | name");

        for (int i = 0; i < f.Nodes.Count; i++)
        {
            FLVER.Node n = f.Nodes[i];
            sb.AppendLine($"{i} | {n.Flags} | {n.ParentIndex} | {n.FirstChildIndex} | " +
                          $"{n.NextSiblingIndex} | {n.PreviousSiblingIndex} | {n.Name}");
        }

        sb.AppendLine("# dummies: idx | refID | nameHash | parent | attach | usesAttach");
        for (int i = 0; i < f.Dummies.Count; i++)
        {
            FLVER.Dummy d = f.Dummies[i];
            sb.AppendLine($"{i} | {d.ReferenceID} | 0x{d.NameHash:X8} | " +
                          $"{d.ParentBoneIndex} | {d.AttachBoneIndex} | {d.UsesAttachBoneIndex}");
        }

        sb.AppendLine("# materiały: matbin | gx | mtd_out | mtd_src | name");
        for (int i = 0; i < f.Materials.Count; i++)
        {
            FLVER2.Material m = f.Materials[i];
            sb.AppendLine($"{m.Index} | {m.GXIndex} | {m.MTD} | {srcMtd[i]} | {m.Name}");
        }

        sb.AppendLine("# --- ostrzeżenia ---");
        var badNames = f.Nodes.Select((n, i) => (n, i))
            .Where(t => t.n.Name.Any(c => c < 0x20 || c == '\uFFFD'))
            .Select(t => t.i).ToList();
        sb.AppendLine($"nazwy z podejrzanymi znakami: {badNames.Count}" +
                      (badNames.Count > 0 ? " -> " + string.Join(", ", badNames) : ""));

        var orphans = f.Nodes.Select((n, i) => (n, i))
            .Where(t => t.n.ParentIndex < 0
                        && !t.n.Flags.HasFlag(FLVER.Node.NodeFlags.Disabled)
                        && t.i != 0)
            .Select(t => t.i).ToList();
        sb.AppendLine($"osierocone węzły (parent -1): {orphans.Count}" +
                      (orphans.Count > 0 ? " -> " + string.Join(", ", orphans) : ""));

        var gxHist = f.Materials.GroupBy(m => m.GXIndex)
            .OrderBy(g => g.Key).Select(g => $"gx{g.Key}={g.Count()}");
        sb.AppendLine($"rozkład gx: {string.Join(", ", gxHist)}");

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static int FindDisabledBlockStart(FLVER2 f)
    {
        int i = f.Nodes.Count - 1;
        while (i >= 0 && f.Nodes[i].Flags.HasFlag(FLVER.Node.NodeFlags.Disabled))
            i--;
        return i + 1;
    }

    private static int AttachOrphansToMaster(FLVER2 f)
    {
        int changed = 0;
        for (int i = 1; i < f.Nodes.Count; i++)
        {
            FLVER.Node n = f.Nodes[i];
            if (n.ParentIndex >= 0 || n.Flags.HasFlag(FLVER.Node.NodeFlags.Disabled))
                continue;
            n.ParentIndex = 0;
            changed++;
        }
        return changed;
    }

    private static int AttachOrphans(FLVER2 f, HashSet<int> meshNodes)
    {
        int head = f.Nodes.FindIndex(n => n.Name.Equals("Head", StringComparison.OrdinalIgnoreCase));
        int spine2 = f.Nodes.FindIndex(n => n.Name.Equals("Spine2", StringComparison.OrdinalIgnoreCase));
        int pelvis = f.Nodes.FindIndex(n => n.Name.Equals("Pelvis", StringComparison.OrdinalIgnoreCase));
        const int master = 0;
        int changed = 0;

        for (int i = 1; i < f.Nodes.Count; i++)
        {
            FLVER.Node n = f.Nodes[i];
            if (n.ParentIndex >= 0 || n.Flags.HasFlag(FLVER.Node.NodeFlags.Disabled))
                continue;

            int p;
            if (meshNodes.Contains(i))
                p = master;
            else if (n.Name.Contains("hood", StringComparison.OrdinalIgnoreCase))
                p = head >= 0 ? head : master;
            else if (n.Name.StartsWith("fga", StringComparison.OrdinalIgnoreCase)
                     || n.Name.StartsWith("オブジェクト", StringComparison.Ordinal))
                p = spine2 >= 0 ? spine2 : (pelvis >= 0 ? pelvis : master);
            else if (n.Name.StartsWith("Dummy", StringComparison.OrdinalIgnoreCase))
                p = pelvis >= 0 ? pelvis : master;
            else
                p = pelvis >= 0 ? pelvis : master;

            if (p != i)
            {
                n.ParentIndex = S(p);
                changed++;
            }
        }
        return changed;
    }

    private static void RebuildLinks(FLVER2 f)
    {
        foreach (FLVER.Node n in f.Nodes)
        {
            n.FirstChildIndex = -1;
            n.NextSiblingIndex = -1;
            n.PreviousSiblingIndex = -1;
        }

        var kids = new List<int>[f.Nodes.Count];
        for (int i = 0; i < kids.Length; i++)
            kids[i] = new List<int>();

        for (int i = 0; i < f.Nodes.Count; i++)
        {
            int p = f.Nodes[i].ParentIndex;
            if (p >= 0 && p < f.Nodes.Count && p != i)
                kids[p].Add(i);
        }

        for (int p = 0; p < f.Nodes.Count; p++)
        {
            List<int> k = kids[p];
            if (k.Count == 0)
                continue;

            f.Nodes[p].FirstChildIndex = S(k[0]);
            for (int j = 0; j < k.Count; j++)
            {
                f.Nodes[k[j]].PreviousSiblingIndex = S(j == 0 ? -1 : k[j - 1]);
                f.Nodes[k[j]].NextSiblingIndex = S(j == k.Count - 1 ? -1 : k[j + 1]);
            }
        }
    }

    private static void BuildSkeletons(FLVER2 f)
    {
        var set = new FLVER2.SkeletonSet
        {
            BaseSkeleton = new List<FLVER2.SkeletonSet.Bone>(f.Nodes.Count),
            AllSkeletons = new List<FLVER2.SkeletonSet.Bone>(f.Nodes.Count),
        };

        for (int i = 0; i < f.Nodes.Count; i++)
        {
            FLVER.Node n = f.Nodes[i];

            short p = S(n.ParentIndex);
            short c = S(n.FirstChildIndex);
            short nx = S(n.NextSiblingIndex);
            short pv = S(n.PreviousSiblingIndex);

            set.BaseSkeleton.Add(new FLVER2.SkeletonSet.Bone(i)
            {
                ParentIndex = p,
                FirstChildIndex = c,
                NextSiblingIndex = nx,
                PreviousSiblingIndex = pv,
            });
            set.AllSkeletons.Add(new FLVER2.SkeletonSet.Bone(i)
            {
                ParentIndex = p,
                FirstChildIndex = c,
                NextSiblingIndex = nx,
                PreviousSiblingIndex = pv,
            });
        }

        f.Skeletons = set;
    }

    private static string RepairName(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return "";

        string stripped = s.Replace("\0", "").Trim();
        if (!s.Contains('\0'))
            return stripped;

        var bytes = new byte[s.Length];
        for (int i = 0; i < s.Length; i++)
            bytes[i] = (byte)(s[i] & 0xFF);

        string decoded = Encoding.Unicode.GetString(bytes, 0, bytes.Length & ~1)
            .Replace("\0", "").Trim();

        return Score(decoded) < Score(stripped) ? decoded : stripped;
    }

    private static int Score(string s) => s.Count(ch =>
        (ch >= 0x3400 && ch <= 0x4DBF) || (ch >= 0xFF61 && ch <= 0xFF9F) || ch < 0x20);

    private static int NormalizeNames(FLVER2 f)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding sj = Encoding.GetEncoding("shift_jis");

        int repaired = 0;

        string Fix(string? s, out bool did)
        {
            did = false;
            if (string.IsNullOrEmpty(s))
                return "";
            string r = RepairName(s);
            if (r != s)
                did = true;

            if (!r.Contains('\0') && r.Any(ch => ch > 0xFF) && !r.Contains('\uFFFD'))
            {
                var b = new byte[r.Length * 2];
                for (int i = 0; i < r.Length; i++)
                {
                    b[2 * i] = (byte)(r[i] & 0xFF);
                    b[2 * i + 1] = (byte)(r[i] >> 8);
                }
                string alt = sj.GetString(b).Trim('\0');
                if (alt.Length > 0 && Score(alt) < Score(r))
                {
                    r = alt;
                    did = true;
                }
            }
            return r;
        }

        for (int i = 0; i < f.Nodes.Count; i++)
        {
            string v = Fix(f.Nodes[i].Name, out bool d);
            f.Nodes[i].Name = v;
            if (d) repaired++;
        }
        for (int i = 0; i < f.Materials.Count; i++)
        {
            f.Materials[i].Name = Fix(f.Materials[i].Name, out _);
            f.Materials[i].MTD = Fix(f.Materials[i].MTD, out _);
        }
        foreach (var t in f.Materials.SelectMany(m => m.Textures))
        {
            t.Path = Fix(t.Path, out _);
            t.ParamName = Fix(t.ParamName, out _);
        }
        return repaired;
    }

    private static void RenameDummyNodes(FLVER2 f)
    {
        int n = 0;
        for (int i = 0; i < f.Nodes.Count; i++)
        {
            FLVER.Node node = f.Nodes[i];
            if (!node.Name.StartsWith("Dummy", StringComparison.OrdinalIgnoreCase))
                continue;
            string old = node.Name;
            node.Name = $"Dummy_r{n++:D2}";
            Console.WriteLine($"  rename [{i}] {old} -> {node.Name}");
        }
    }

    private static void HashDummies(FLVER2 f)
    {
        foreach (FLVER.Dummy d in f.Dummies)
        {
            string parent = d.ParentBoneIndex >= 0 && d.ParentBoneIndex < f.Nodes.Count
                ? f.Nodes[d.ParentBoneIndex].Name
                : "root";
            uint h = Crc32($"{parent}_{d.ReferenceID}");
            d.NameHash = h == 0 ? 1 : unchecked((int)h);
        }
    }

    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }

    private static uint Crc32(string s)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte b in Encoding.UTF8.GetBytes(s))
            c = Crc32Table[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    private static bool DummyIs(FLVER.Dummy d, string name) => (uint)d.NameHash == Crc32(name);

    private sealed record MapEntry(string Path, int GX);

    private sealed class MatMap
    {
        private readonly Dictionary<string, MapEntry> mtdExact = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MapEntry> nameExact = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<(Regex Rx, MapEntry Entry)> mtdRules = new();
        private readonly List<(Regex Rx, MapEntry Entry)> nameRules = new();

        public IEnumerable<MapEntry> Entries => mtdExact.Values.Concat(nameExact.Values)
            .Concat(mtdRules.Select(r => r.Entry)).Concat(nameRules.Select(r => r.Entry));

        // DS3 trzyma w MTD literalne nawiasy (C[ARSN].mtd) — normalizujemy po obu stronach
        private static string Norm(string s) => s.Replace("[", "").Replace("]", "");

        public bool TryMatchMtd(string key, out MapEntry entry)
        {
            if (mtdExact.TryGetValue(Norm(key), out entry!))
                return true;
            foreach (var (rx, e) in mtdRules)
            {
                if (rx.IsMatch(key))
                {
                    entry = e;
                    return true;
                }
            }
            entry = null!;
            return false;
        }

        public bool TryMatchName(string key, out MapEntry entry)
        {
            if (nameExact.TryGetValue(key, out entry!))
                return true;
            foreach (var (rx, e) in nameRules)
            {
                if (rx.IsMatch(key))
                {
                    entry = e;
                    return true;
                }
            }
            entry = null!;
            return false;
        }

        public void AddMtd(string key, MapEntry entry)
        {
            if (key.IndexOfAny(new[] { '*', '?' }) >= 0)
            {
                mtdRules.Add((new Regex("^" + Regex.Escape(key)
                    .Replace(@"\[", "[").Replace(@"\]", "]")
                    .Replace(@"\*", ".*").Replace(@"\?", ".") + "$", RegexOptions.IgnoreCase), entry));
            }
            else
            {
                mtdExact[Norm(key)] = entry;
                if (key.Contains('['))
                {
                    string rx = "^" + Regex.Escape(key)
                        .Replace(@"\[", "[").Replace(@"\]", "]") + "$";
                    mtdRules.Add((new Regex(rx, RegexOptions.IgnoreCase), entry));
                }
            }
        }

        public void AddName(string key, MapEntry entry)
        {
            if (key.IndexOfAny(new[] { '*', '?' }) >= 0)
            {
                nameRules.Add((new Regex("^" + Regex.Escape(key)
                    .Replace(@"\*", ".*").Replace(@"\?", ".") + "$", RegexOptions.IgnoreCase), entry));
            }
            else
            {
                nameExact[key] = entry;
            }
        }
    }

    private static MatMap LoadMatMap(string path)
    {
        var map = new MatMap();

        if (!File.Exists(path))
        {
            File.WriteAllText(path,
                "# lewa kolumna: nazwa MTD (konczy sie .mtd) albo nazwa materialu\n" +
                "# wildcardy: [ARSN], *, ?\n" +
                "# tokeny w prawej kolumnie: {CID} = cXXXX, {CIDU} = CXXXX, {ID} = XXXX\n" +
                "# gxIdx: 0 = default (materialy bossow ER uzywaja 0)\n" +
                "C[ARSN].mtd;N:\\GR\\data\\Material\\mtd\\Chr\\matxml\\C[{CID}]_body.matxml;0\n");
            Console.Error.WriteLine($"brak {path} — utworzono szablon");
            return map;
        }

        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            string[] p = line.Split(';');
            if (p.Length < 3)
                continue;
            if (!int.TryParse(p[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int gx))
                continue;

            string key = Path.GetFileName(p[0].Trim());
            if (key.Length == 0)
                continue;

            var entry = new MapEntry(p[1].Trim(), gx);
            if (key.EndsWith(".mtd", StringComparison.OrdinalIgnoreCase))
                map.AddMtd(key, entry);
            else
                map.AddName(key, entry);
        }

        if (!map.Entries.Any())
            Console.Error.WriteLine($"UWAGA: {path} nie zawiera żadnego wpisu");

        return map;
    }
}