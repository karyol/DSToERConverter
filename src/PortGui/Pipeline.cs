using System.Diagnostics;
using System.Text;

internal sealed class Pipeline
{
    private readonly Settings _s;
    private readonly int _src, _dst;
    private readonly Action<string> _log;
    private readonly CancellationToken _ct;

    private static readonly EnumerationOptions Deep = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        AttributesToSkip = 0
    };

    public Pipeline(Settings s, int src, int dst, Action<string> log, CancellationToken ct)
    {
        _s = s; _src = src; _dst = dst; _log = log; _ct = ct;
    }

    public async Task RunAsync()
    {
        List<string> bad = _s.Validate();
        if (bad.Count > 0)
            throw new InvalidOperationException("konfiguracja: " + string.Join("; ", bad));

        string srcChr = Path.Combine(_s.Ds3Dir, "chr", $"c{_src:D4}.chrbnd.dcx");
        if (!File.Exists(srcChr)) srcChr = srcChr[..^4];
        if (!File.Exists(srcChr)) throw new FileNotFoundException($"brak {srcChr}");

        string modDir = Directory.Exists(Path.Combine(_s.ProjectDir, "mod"))
            ? Path.Combine(_s.ProjectDir, "mod") : _s.ProjectDir;

        string dstReg = FindRegulation(_s.ProjectDir) ?? FindRegulation(_s.ErDir)
            ?? throw new FileNotFoundException(
                "brak regulation.bin w projekcie ani w ER — najpierw utwórz projekt Smithboxa dla ER");

        _log($"regulation docelowy: {dstReg}");

        var paramSrc = ParamSource.Resolve(_s.Ds3Dir, _s.Ds3ParamSource, _log)
            ?? throw new FileNotFoundException("nie znaleziono źródła paramów DS3 (patrz log)");

        Directory.CreateDirectory(_s.LogsDir);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string audit = Path.Combine(_s.LogsDir, $"c{_dst:D4}.{stamp}.audit.txt");
        string runLog = Path.Combine(_s.LogsDir, $"c{_dst:D4}.{stamp}.run.log");
        _log($"audit: {audit}");

        string temp = Path.Combine(Path.GetTempPath(), "PortGui", Guid.NewGuid().ToString("N"));
        string work = Path.Combine(temp, "chr");
        string pack = Path.Combine(temp, "pack");
        bool ok = false;

        try
        {
            Directory.CreateDirectory(work);
            Directory.CreateDirectory(pack);
            _log($"temp: {temp}");

            // 1. model
            string localChr = Path.Combine(work, Path.GetFileName(srcChr));
            File.Copy(srcChr, localChr, true);

            await Tool(_s.WitchyBnd, localChr);
            string unpacked = Directory.EnumerateDirectories(work).FirstOrDefault()
                ?? throw new DirectoryNotFoundException("WitchyBND nie rozpakował chrbnd");
            _log($"rozpakowano do {Path.GetFileName(unpacked)}");

            string srcFlver = Directory.EnumerateFiles(unpacked, "*.flver", Deep).FirstOrDefault()
                ?? throw new FileNotFoundException("brak flver w chrbnd");

            string dstFlver = Path.Combine(pack, $"c{_dst:D4}.flver");
            string attach = _s.Attach == AttachMode.Smart ? "--attach-orphans" : "--attach-master";
            string convOut = await Tool(_s.FlverConv,
                srcFlver, dstFlver, _s.MatMap,
                "--model", _dst.ToString(),
                "--audit", audit,
                "--dump-mats", "--rename-dummies", attach);
            File.AppendAllText(runLog, convOut, new UTF8Encoding(false));

            if (convOut.Contains("FAIL", StringComparison.Ordinal))
                throw new InvalidOperationException($"weryfikacja FLVER nie przeszła (audit: {audit})");

            // 2. tekstury
            string? tpf = Directory.EnumerateFiles(unpacked, "*.tpf", Deep).FirstOrDefault();
            if (tpf != null)
            {
                File.Copy(tpf, Path.Combine(pack, $"c{_dst:D4}.tpf"), true);
                _log($"tekstury -> c{_dst:D4}.tpf");
            }
            else _log("UWAGA: brak tpf");

            // 3. materiały — MatClone czyta źródłowy FLVER DS3, bo klucze matmapy to MTD z DS3
            string matOut = await Tool(_s.PortKit,
                "matclone", "--matmap", _s.MatMap, "--er", _s.ErDir,
                "--out", pack, "--model", _dst.ToString(),
                "--src", srcFlver, "--donor", _s.DonorId.ToString());
            File.AppendAllText(runLog, matOut, new UTF8Encoding(false));

            // 4. szkielet/animacje
            string? srcAni = Path.Combine(_s.Ds3Dir, "chr", $"c{_src:D4}.anibnd.dcx");
            if (!File.Exists(srcAni)) srcAni = Path.Combine(_s.Ds3Dir, "chr", $"c{_src:D4}.anibnd");
            string dstAni = Path.Combine(pack, $"c{_dst:D4}.anibnd.dcx");
            if (File.Exists(srcAni))
            {
                await Tool(_s.PortKit,
                    "anibnd", "--src", srcAni, "--out", dstAni,
                    "--model", _dst.ToString(), "--dst-id", _dst.ToString());
            }
            else _log("UWAGA: brak anibnd w źródle");

            // 5. behavior (opcjonalnie)
            string? srcBeh = Path.Combine(_s.Ds3Dir, "chr", $"c{_src:D4}.behbnd.dcx");
            if (File.Exists(srcBeh))
            {
                string dstBeh = Path.Combine(pack, $"c{_dst:D4}.behbnd.dcx");
                await Tool(_s.PortKit, "behbnd", "--src", srcBeh, "--out", dstBeh,
                    "--donor", _s.DonorId.ToString(), "--er", _s.ErDir);
            }
            else _log("UWAGA: brak behbnd — AI nie zadziała");

            // 6. pakowanie
            await Tool(_s.WitchyBnd, pack);

            // 7. paramy
            string workReg = Path.Combine(temp, "regulation.bin");
            File.Copy(dstReg, workReg, true);
            var report = await ParamPorter.PortAsync(paramSrc, workReg, _src, _dst,
                _s.DonorId, _s.BossName, _log, _ct);

            // 8. dostawa
            Directory.CreateDirectory(Path.Combine(modDir, "chr"));
            foreach (string f in Directory.EnumerateFiles(pack, $"c{_dst:D4}.*"))
                File.Copy(f, Path.Combine(modDir, "chr", Path.GetFileName(f)), true);
            _log($"dostarczono pliki chr dla c{_dst:D4}");

            string matSrc = Path.Combine(pack, "material");
            if (Directory.Exists(matSrc))
            {
                CopyDir(matSrc, Path.Combine(modDir, "material"));
                _log("dostarczono materiały");
            }

            string targetReg = Path.Combine(modDir, "regulation.bin");
            if (File.Exists(targetReg)) File.Copy(targetReg, targetReg + ".bak", true);
            File.Copy(workReg, targetReg, true);
            _log($"dostarczono regulation.bin -> {targetReg}");

            string rep = Path.Combine(_s.ProjectDir, $"raport_{_dst:D4}.csv");
            report.SaveCsv(rep);
            _log($"raport: {rep} (ostrzeżenia: {report.Warnings.Count}, remapy: {report.Remaps.Count})");
            if (report.Warnings.Count > 0)
                foreach (string w in report.Warnings.Take(25)) _log($"  ! {w}");

            ok = true;
            _log("GOTOWE");
        }
        finally
        {
            if (ok)
            {
                try { Directory.Delete(temp, true); } catch { }
            }
            else _log($"temp zachowany: {temp}");
        }
    }

    private async Task<string> Tool(string exe, params string[] args)
    {
        _ct.ThrowIfCancellationRequested();
        if (!File.Exists(exe)) throw new FileNotFoundException($"brak narzędzia {exe}");
        _log($"> {Path.GetFileName(exe)} {string.Join(' ', args)}");

        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string a in args)
            psi.ArgumentList.Add(a);

        var sb = new StringBuilder();
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"nie startuje {exe}");
        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            sb.AppendLine(e.Data);
            _log("  " + e.Data);
        };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) _log("  " + e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        await p.WaitForExitAsync(_ct);

        if (p.ExitCode != 0)
            throw new InvalidOperationException($"{Path.GetFileName(exe)} zwrócił {p.ExitCode}");

        return sb.ToString();
    }

    public static string? FindRegulation(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return null;
        foreach (string rel in new[] { Path.Combine("mod", "regulation.bin"), "regulation.bin" })
        {
            string p = Path.Combine(root, rel);
            if (File.Exists(p)) return p;
        }
        return Directory.EnumerateFiles(root, "regulation.bin", Deep).FirstOrDefault();
    }

    private static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            string dst = Path.Combine(to, Path.GetRelativePath(from, f));
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(f, dst, true);
        }
    }
}