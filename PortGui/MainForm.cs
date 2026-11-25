internal sealed class MainForm : Form
{
    private readonly Settings _s = Settings.Load();
    private readonly TextBox _ds3 = new();
    private readonly TextBox _paramSrc = new();
    private readonly TextBox _er = new();
    private readonly TextBox _proj = new();
    private readonly TextBox _src = new();
    private readonly TextBox _dst = new();
    private readonly TextBox _name = new();
    private readonly TextBox _from = new();
    private readonly TextBox _to = new();
    private readonly ListBox _free = new();
    private readonly TextBox _log = new();
    private readonly TextBox _witchy = new();
    private readonly TextBox _conv = new();
    private readonly TextBox _kit = new();
    private readonly Button _run = new();
    private readonly Button _cancel = new();
    private CancellationTokenSource? _cts;
    private int _freeSeq;

    public MainForm()
    {
        Text = "PortGui — DS3 -> ER";
        Width = 980;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        int y = 12;
        AddPath("DS3 (rozpakowane)", _ds3, ref y, _s.Ds3Dir, false);
        AddPath("Paramy DS3 (regulation/Data0.bdt/folder)", _paramSrc, ref y, _s.Ds3ParamSource, true);
        AddPath("Elden Ring (rozpakowane)", _er, ref y, _s.ErDir, false);
        AddPath("Projekt Smithbox", _proj, ref y, _s.ProjectDir, false);
        _proj.TextChanged += (_, _) => RefreshFree();
        _paramSrc.TextChanged += (_, _) => RefreshFree();
        _er.TextChanged += (_, _) => RefreshFree();

        Label l1 = new() { Text = "ID źródłowe (DS3)", Left = 12, Top = y + 4, Width = 150 };
        _src.Left = 170;
        _src.Top = y;
        _src.Width = 100;
        _src.Text = _s.LastSrcId > 0 ? _s.LastSrcId.ToString() : "";
        y += 30;

        Label lr = new() { Text = "Zakres ID", Left = 12, Top = y + 4, Width = 80 };
        _from.Left = 100;
        _from.Top = y;
        _from.Width = 70;
        _from.Text = _s.IdFrom.ToString();
        _to.Left = 180;
        _to.Top = y;
        _to.Width = 70;
        _to.Text = _s.IdTo.ToString();

        Button reload = new() { Text = "Szukaj wolnych ID", Left = 270, Top = y, Width = 150 };
        reload.Click += (_, _) => RefreshFree();

        Button boss = new() { Text = "Blok bossów", Left = 430, Top = y, Width = 110 };
        boss.Click += (_, _) => { _from.Text = "2000"; _to.Text = "2999"; RefreshFree(); };
        y += 30;

        _free.Left = 12;
        _free.Top = y;
        _free.Width = 300;
        _free.Height = 150;
        _free.Click += (_, _) => PickFree();
        _free.DoubleClick += (_, _) => PickFree();

        Label l3 = new() { Text = "Nowe ID (ER)", Left = 330, Top = y + 4, Width = 110 };
        _dst.Left = 440;
        _dst.Top = y;
        _dst.Width = 110;

        Label l4 = new() { Text = "Nazwa bossa", Left = 330, Top = y + 34, Width = 110 };
        _name.Left = 440;
        _name.Top = y + 30;
        _name.Width = 420;
        _name.Text = _s.BossName;

        Label hint = new()
        {
            Left = 330,
            Top = y + 64,
            Width = 530,
            Height = 84,
            Text = "Docelowy NpcParam: <ID>0000\nModel: chr\\c<ID>.chrbnd.dcx\n" +
                   "Paramy portowane z domknięciem referencji (Atk/Bullet/Behavior/SpEffect).\n" +
                   "regulation.bin w projekcie jest kopiowany do .bak przed zapisem."
        };
        y += 155;

        _run.Text = "Portuj";
        _run.Left = 12;
        _run.Top = y;
        _run.Width = 120;
        _run.Height = 30;
        _run.Click += async (_, _) => await RunAsync();

        _cancel.Text = "Anuluj";
        _cancel.Left = 142;
        _cancel.Top = y;
        _cancel.Width = 90;
        _cancel.Height = 30;
        _cancel.Enabled = false;
        _cancel.Click += (_, _) => _cts?.Cancel();
        y += 38;

        GroupBox g = new() { Text = "Narzędzia", Left = 12, Top = y, Width = 940, Height = 96 };
        AddTool(g, "witchyBND", _witchy, _s.WitchyBnd, 18);
        AddTool(g, "FlverConv", _conv, _s.FlverConv, 46);
        AddTool(g, "PortKit", _kit, _s.PortKit, 74);
        y += 104;

        _log.Left = 12;
        _log.Top = y;
        _log.Width = 940;
        _log.Height = 240;
        _log.Multiline = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.ReadOnly = true;
        _log.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

        Controls.AddRange(new Control[]
        {
            l1, _src, lr, _from, _to, reload, boss, _free, l3, _dst, l4, _name, hint, _run, _cancel, g, _log
        });

        _free.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        _name.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        FormClosing += (_, _) => Save();
        Shown += (_, _) => RefreshFree();
    }

    private void PickFree()
    {
        if (_free.SelectedItem is FreeItem fi)
            _dst.Text = fi.Id.ToString();
    }

    private void AddPath(string label, TextBox box, ref int y, string value, bool allowFile)
    {
        Label l = new() { Text = label, Left = 12, Top = y + 4, Width = 200 };
        box.Left = 220;
        box.Top = y;
        box.Width = 620;
        box.Text = value;
        box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        Button folder = new() { Text = "...", Left = 844, Top = y - 1, Width = 44 };
        folder.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        folder.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog();
            if (d.ShowDialog() == DialogResult.OK)
                box.Text = d.SelectedPath;
        };
        Controls.AddRange(new Control[] { l, box, folder });

        if (allowFile)
        {
            Button file = new() { Text = "plik", Left = 892, Top = y - 1, Width = 44 };
            file.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            file.Click += (_, _) =>
            {
                using var d = new OpenFileDialog { Filter = "kontener|*.bin;*.bdt;*.bhd|wszystko|*.*" };
                if (d.ShowDialog() == DialogResult.OK)
                    box.Text = d.FileName;
            };
            Controls.Add(file);
        }

        y += 30;
    }

    private void AddTool(GroupBox g, string label, TextBox box, string value, int top)
    {
        g.Controls.Add(new Label { Text = label, Left = 10, Top = top + 4, Width = 80 });
        box.Left = 95;
        box.Top = top;
        box.Width = 760;
        box.Text = value;
        box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        g.Controls.Add(box);

        Button b = new() { Text = "...", Left = 865, Top = top - 1, Width = 40 };
        b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        b.Click += (_, _) =>
        {
            using var d = new OpenFileDialog { Filter = "exe|*.exe" };
            if (d.ShowDialog() == DialogResult.OK)
                box.Text = d.FileName;
        };
        g.Controls.Add(b);
    }

    private void Save()
    {
        _s.Ds3Dir = _ds3.Text;
        _s.Ds3ParamSource = _paramSrc.Text;
        _s.ErDir = _er.Text;
        _s.ProjectDir = _proj.Text;
        _s.WitchyBnd = _witchy.Text;
        _s.FlverConv = _conv.Text;
        _s.PortKit = _kit.Text;
        _s.BossName = _name.Text;
        _s.LastSrcId = int.TryParse(_src.Text, out int v) ? v : 0;
        if (int.TryParse(_from.Text, out int a))
            _s.IdFrom = a;
        if (int.TryParse(_to.Text, out int b))
            _s.IdTo = b;
        _s.Save();
    }

    private void Ui(Action a)
    {
        if (IsDisposed)
            return;
        if (InvokeRequired)
            BeginInvoke(a);
        else
            a();
    }

    private void RefreshFree()
    {
        int seq = ++_freeSeq;
        _free.Items.Clear();

        string? reg = Pipeline.FindRegulation(_proj.Text);
        if (reg == null)
            reg = Pipeline.FindRegulation(_er.Text);

        if (reg == null)
        {
            Log("brak regulation.bin w projekcie ani w ER — lista ID niedostępna");
            return;
        }
        if (!int.TryParse(_from.Text, out int from) || !int.TryParse(_to.Text, out int to))
        {
            Log("zły zakres ID");
            return;
        }

        var dirs = new[] { _proj.Text, _er.Text };
        Log($"szukam wolnych ID w zakresie {from}-{to} na podstawie {reg}");

        _ = Task.Run(() =>
        {
            try
            {
                var list = FreeIdFinder.Find(reg, dirs, from, to, 60, s => Ui(() => Log(s)));
                Ui(() =>
                {
                    if (seq != _freeSeq)
                        return;
                    _free.Items.Clear();
                    foreach (int id in list)
                        _free.Items.Add(new FreeItem(id));
                    Log(list.Count == 0
                        ? "brak wolnych ID w tym zakresie — zmień zakres lub sprawdź źródło regulation"
                        : $"wolne ID: {list.Count}");
                });
            }
            catch (Exception e)
            {
                Ui(() => Log($"błąd wyszukiwania ID: {e.Message}"));
            }
        });
    }

    private async Task RunAsync()
    {
        if (!int.TryParse(_src.Text, out int src) || src <= 0)
        { Log("podaj ID źródłowe"); return; }
        if (!int.TryParse(_dst.Text, out int dst) || dst <= 0)
        { Log("wybierz nowe ID"); return; }
        if (!Directory.Exists(_ds3.Text))
        { Log("zła ścieżka DS3"); return; }
        if (!Directory.Exists(_er.Text))
        { Log("zła ścieżka ER"); return; }
        if (!Directory.Exists(_proj.Text))
        { Log("zła ścieżka projektu"); return; }

        string modDir = Directory.Exists(Path.Combine(_proj.Text, "mod"))
            ? Path.Combine(_proj.Text, "mod") : _proj.Text;

        if (File.Exists(Path.Combine(modDir, "chr", $"c{dst:D4}.chrbnd.dcx")) &&
            MessageBox.Show($"chr\\c{dst:D4}.chrbnd.dcx już istnieje. Nadpisać?",
                "Kolizja", MessageBoxButtons.YesNo) != DialogResult.Yes)
            return;

        Save();
        _cts = new CancellationTokenSource();
        _run.Enabled = false;
        _cancel.Enabled = true;

        try
        {
            await new Pipeline(_s, src, dst, Log, _cts.Token).RunAsync();
        }
        catch (OperationCanceledException) { Log("anulowano"); }
        catch (Exception e) { Log($"BŁĄD: {e.Message}"); }
        finally
        {
            _run.Enabled = true;
            _cancel.Enabled = false;
            _cts?.Dispose();
            _cts = null;
            RefreshFree();
        }
    }

    private void Log(string line) => Ui(() =>
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}"));

    private sealed class FreeItem
    {
        public int Id { get; }
        public FreeItem(int id) => Id = id;
        public override string ToString() => $"{Id:D4}  ->  chr\\c{Id:D4}";
    }
}