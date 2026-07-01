using System.Diagnostics;
using Microsoft.Win32;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (args.Length > 0)
        {
            MainForm.HandleElevated(args);
            return;
        }

        Application.Run(new MainForm());
    }
}

enum EntrySource { HKCU, HKLM, HKLMX86, StartupFolder, CommonStartup, DisabledStore }

class StartupEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool Enabled { get; set; }
    public EntrySource Source { get; set; }
    public string SourceLabel => Source switch
    {
        EntrySource.HKCU => "HKCU",
        EntrySource.HKLM => "HKLM",
        EntrySource.HKLMX86 => "HKLM(x86)",
        EntrySource.StartupFolder => "Startup",
        EntrySource.CommonStartup => "Common",
        EntrySource.DisabledStore => "Disabled",
        _ => ""
    };
    public string RegValueName { get; set; } = "";
}

class MainForm : Form
{
    const string REG_RUN = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string REG_RUN_HKLM = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string REG_RUN_HKLMX86 = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    const string REG_LSTARTLAI = @"Software\LStartlai\Disabled";

    static readonly string StartupFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup));
    static readonly string CommonStartup = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup));
    static readonly string DisabledFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LStartlai", "DisabledShortcuts");

    readonly Color bg = Color.FromArgb(26, 26, 46);
    readonly Color card = Color.FromArgb(30, 30, 56);
    readonly Color accent = Color.FromArgb(233, 69, 96);
    readonly Color text = Color.FromArgb(220, 220, 240);
    readonly Color muted = Color.FromArgb(140, 140, 170);
    readonly Color border = Color.FromArgb(50, 50, 80);

    FlowLayoutPanel listPanel;
    Label statusLabel;
    TextBox pathInput;
    Button pathAddBtn;
    readonly List<StartupEntry> entries = [];
    readonly List<EntryCard> cards = [];

    public MainForm()
    {
        Text = "LStartlai";
        Size = new Size(720, 580);
        MinimumSize = new Size(700, 400);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = bg;
        ForeColor = text;
        Font = new Font("Segoe UI", 10);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        KeyPreview = true;
        AllowDrop = true;

        var titleBar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(22, 22, 40) };
        var titleLbl = new Label
        {
            Text = "LStartlai",
            ForeColor = text,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            Location = new Point(20, 8), AutoSize = true
        };
        var subtitle = new Label
        {
            Text = "Startup Manager",
            ForeColor = accent,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            Location = new Point(108, 17), AutoSize = true
        };
        var closeBtn = new Button
        {
            Text = "\u2715", FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = accent },
            ForeColor = muted, Font = new Font("Segoe UI", 12),
            Size = new Size(36, 36), Location = new Point(Width - 46, 4),
            Anchor = AnchorStyles.Top | AnchorStyles.Right, Cursor = Cursors.Hand
        };
        closeBtn.Click += (_, _) => Close();

        pathAddBtn = new Button
        {
            Text = "+", FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = accent },
            ForeColor = accent, Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Size = new Size(30, 30), Location = new Point(Width - 82, 7),
            Anchor = AnchorStyles.Top | AnchorStyles.Right, Cursor = Cursors.Hand, Visible = false
        };
        pathInput = new TextBox
        {
            Text = "", ForeColor = Color.FromArgb(180, 180, 200),
            BackColor = Color.FromArgb(35, 35, 60),
            BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9),
            Size = new Size(220, 24), Location = new Point(Width - 308, 9),
            Anchor = AnchorStyles.Top | AnchorStyles.Right, Visible = false
        };
        pathInput.Enter += (_, _) => { if (pathInput.Text == "Paste app path...") { pathInput.Text = ""; pathInput.ForeColor = Color.White; } };
        pathInput.Leave += (_, _) => { if (string.IsNullOrWhiteSpace(pathInput.Text)) { pathInput.Text = "Paste app path..."; pathInput.ForeColor = Color.FromArgb(180, 180, 200); } };
        pathInput.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { DoQuickAdd(); e.Handled = true; e.SuppressKeyPress = true; } };
        pathAddBtn.Click += (_, _) => DoQuickAdd();

        titleBar.Controls.AddRange([titleLbl, subtitle, pathAddBtn, pathInput, closeBtn]);

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = bg };
        var header = new Label
        {
            Text = "Programs that run when Windows starts",
            ForeColor = muted, Font = new Font("Segoe UI", 9),
            Location = new Point(20, 12), AutoSize = true
        };
        var addBtn = new Button();
        addBtn.Text = "  +  Add Program";
        addBtn.Location = new Point(20, 38);
        addBtn.Size = new Size(150, 34);
        addBtn.FlatStyle = FlatStyle.Flat;
        addBtn.BackColor = accent;
        addBtn.ForeColor = Color.White;
        addBtn.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        addBtn.Cursor = Cursors.Hand;
        addBtn.FlatAppearance.BorderSize = 0;
        addBtn.Click += AddEntry;
        var refreshBtn = new Button();
        refreshBtn.Text = "\u21bb  Refresh";
        refreshBtn.Location = new Point(180, 38);
        refreshBtn.Size = new Size(110, 34);
        refreshBtn.FlatStyle = FlatStyle.Flat;
        refreshBtn.BackColor = card;
        refreshBtn.ForeColor = text;
        refreshBtn.Font = new Font("Segoe UI", 9);
        refreshBtn.Cursor = Cursors.Hand;
        refreshBtn.FlatAppearance.BorderSize = 0;
        refreshBtn.Click += (_, _) => LoadEntries();
        toolbar.Controls.AddRange([header, addBtn, refreshBtn]);

        statusLabel = new Label
        {
            Dock = DockStyle.Bottom, Height = 28,
            BackColor = card, ForeColor = muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            Font = new Font("Segoe UI", 9)
        };

        listPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true,
            BackColor = bg, Padding = new Padding(12, 6, 12, 6)
        };

        Controls.AddRange([statusLabel, listPanel, toolbar, titleBar]);
        Load += (_, _) => LoadEntries();
        Shown += (_, _) => { BringToFront(); Activate(); };
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.N) { AddEntry(null, EventArgs.Empty); e.Handled = true; }
            if (e.Control && e.KeyCode == Keys.R) { LoadEntries(); e.Handled = true; }
        };
        DragEnter += (_, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        };
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
                foreach (var f in files)
                    AddDroppedFile(f);
        };
    }

    Button MakeButton(string text, Color bg, Color fg, int w, int h, int x, int y)
    {
        var b = new Button
        {
            Text = text, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = fg,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Size = new Size(w, h), Location = new Point(x, y), Cursor = Cursors.Hand,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = ControlPaint.Light(bg) }
        };
        return b;
    }

    static void AddEntriesFromReg(RegistryKey? key, List<StartupEntry> list, EntrySource source)
    {
        if (key == null) return;
        foreach (var name in key.GetValueNames())
        {
            var val = key.GetValue(name)?.ToString() ?? "";
            if (!string.IsNullOrEmpty(val))
                list.Add(new StartupEntry { Name = name, Path = val, Enabled = true, Source = source, RegValueName = name });
        }
    }

    static void AddEntriesFromFolder(string folder, List<StartupEntry> list, EntrySource source)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var f in Directory.GetFiles(folder, "*.lnk"))
        {
            var name = Path.GetFileNameWithoutExtension(f);
            list.Add(new StartupEntry { Name = name, Path = f, Enabled = true, Source = source, RegValueName = f });
        }
        foreach (var f in Directory.GetFiles(folder, "*.url"))
        {
            var name = Path.GetFileNameWithoutExtension(f);
            list.Add(new StartupEntry { Name = name, Path = f, Enabled = true, Source = source, RegValueName = f });
        }
    }

    void LoadEntries()
    {
        entries.Clear();
        statusLabel.Text = "Scanning...";

        try
        {
            // Enabled: registry
            using var hkcu = Registry.CurrentUser.OpenSubKey(REG_RUN);
            AddEntriesFromReg(hkcu, entries, EntrySource.HKCU);

            using var hklm = Registry.LocalMachine.OpenSubKey(REG_RUN_HKLM);
            AddEntriesFromReg(hklm, entries, EntrySource.HKLM);

            using var hklmX86 = Registry.LocalMachine.OpenSubKey(REG_RUN_HKLMX86);
            AddEntriesFromReg(hklmX86, entries, EntrySource.HKLMX86);

            // Enabled: startup folders
            AddEntriesFromFolder(StartupFolder, entries, EntrySource.StartupFolder);
            AddEntriesFromFolder(CommonStartup, entries, EntrySource.CommonStartup);

            // Disabled: LStartlai storage (registry)
            using var disabled = Registry.CurrentUser.OpenSubKey(REG_LSTARTLAI);
            if (disabled != null)
            {
                foreach (var name in disabled.GetValueNames())
                {
                    var val = disabled.GetValue(name)?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(val))
                        entries.Add(new StartupEntry { Name = name, Path = val, Enabled = false, Source = EntrySource.DisabledStore, RegValueName = name });
                }
            }

            // Disabled: LStartlai folder (backup shortcuts)
            if (Directory.Exists(DisabledFolder))
            {
                foreach (var f in Directory.GetFiles(DisabledFolder, "*.lnk"))
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    if (!entries.Exists(e => e.Name == name && e.Enabled))
                        entries.Add(new StartupEntry { Name = name, Path = f, Enabled = false, Source = EntrySource.DisabledStore, RegValueName = f });
                }
            }
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Error: {ex.Message}";
            return;
        }

        entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        Render();
        var enabledCount = entries.Count(e => e.Enabled);
        var disabledCount = entries.Count(e => !e.Enabled);
        statusLabel.Text = $"{entries.Count} startup program{(entries.Count == 1 ? "" : "s")} ({enabledCount} enabled, {disabledCount} disabled)";
    }

    void Render()
    {
        listPanel.Controls.Clear();
        cards.Clear();
        foreach (var e in entries)
        {
            var c = new EntryCard(e, card, text, muted, accent, border);
            c.OnToggle += ToggleEntry;
            c.OnRemove += RemoveEntry;
            c.Width = listPanel.ClientSize.Width - 24;
            listPanel.Controls.Add(c);
            cards.Add(c);
        }
    }

    void AddEntry(object? sender, EventArgs e)
    {
        // Step 1: Get file path via simple dialog
        var filePath = BrowseForFile();
        if (filePath == null) return;

        // Step 2: Get name from user
        var defaultName = Path.GetFileNameWithoutExtension(filePath);
        var nameResult = InputBox(this, "Enter a name for this startup entry:", "Add to Startup", defaultName);
        if (nameResult == null) return;
        var name = nameResult;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(REG_RUN, true);
            if (key == null)
            {
                MessageBox.Show("Could not open the startup registry key.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            key.SetValue(name, filePath);
            LoadEntries();
            statusLabel.Text = $"Added \"{name}\" to startup";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to add: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void QuickAddPath(string path)
    {
        if (!File.Exists(path)) { statusLabel.Text = "File not found"; return; }
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".exe" && ext != ".lnk" && ext != ".url")
        {
            MessageBox.Show($"Unsupported file type: {ext}\n\nOnly .exe, .lnk, and .url files are supported.",
                "LStartlai", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var defaultName = Path.GetFileNameWithoutExtension(path);
        var nameResult = InputBox(this, "Enter a name for this startup entry:", "Add to Startup", defaultName);
        if (nameResult == null) return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(REG_RUN, true);
            if (key == null) return;
            key.SetValue(nameResult, path);
            LoadEntries();
            statusLabel.Text = $"Added \"{nameResult}\" to startup";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to add: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void DoQuickAdd()
    {
        var text = pathInput.Text?.Trim();
        if (string.IsNullOrEmpty(text) || text == "Paste app path...") return;
        QuickAddPath(text);
        pathInput.Text = "";
    }

    void AddDroppedFile(string path) => QuickAddPath(path);

    void ToggleEntry(StartupEntry entry)
    {
        try
        {
            if (entry.Source == EntrySource.DisabledStore)
            {
                // Enable: restore to original location
                if (entry.RegValueName.EndsWith(".lnk") || entry.RegValueName.EndsWith(".url"))
                {
                    var originalFolder = entry.Path.Contains(CommonStartup) ? CommonStartup : StartupFolder;
                    var dest = Path.Combine(originalFolder, Path.GetFileName(entry.RegValueName));
                    if (File.Exists(entry.RegValueName))
                        File.Move(entry.RegValueName, dest);
                }
                else
                {
                    var stored = GetDisabledPath(entry.RegValueName);
                    if (stored == null) return;

                    if (stored.Contains('|'))
                    {
                        // HKLM entry: stored as "path|subKey|type"
                        var parts = stored.Split('|', 3);
                        var path = parts[0];
                        var subKey = parts.Length > 1 ? parts[1] : REG_RUN_HKLM;
                        using var hive = Registry.LocalMachine;
                        using var run = hive.OpenSubKey(subKey, true);
                        if (run == null)
                        {
                            RunElevated($"--hklm-enable \"{entry.RegValueName}\" \"{path}\" \"{subKey}\"");
                            RemoveDisabled(entry.RegValueName);
                            LoadEntries();
                            return;
                        }
                        run.SetValue(entry.RegValueName, path);
                        RemoveDisabled(entry.RegValueName);
                    }
                    else
                    {
                        // HKCU entry
                        using var run = Registry.CurrentUser.OpenSubKey(REG_RUN, true);
                        run?.SetValue(entry.RegValueName, stored);
                        RemoveDisabled(entry.RegValueName);
                    }
                }
            }
            else
            {
                // Disable: move to LStartlai storage
                if (entry.Source == EntrySource.StartupFolder || entry.Source == EntrySource.CommonStartup)
                {
                    // Move shortcut file
                    Directory.CreateDirectory(DisabledFolder);
                    var dest = Path.Combine(DisabledFolder, Path.GetFileName(entry.RegValueName));
                    if (File.Exists(entry.RegValueName))
                        File.Move(entry.RegValueName, dest, true);
                }
                else if (entry.Source == EntrySource.HKCU)
                {
                    // Move from HKCU Run to LStartlai storage
                    using var run = Registry.CurrentUser.OpenSubKey(REG_RUN, true);
                    var path = run?.GetValue(entry.RegValueName)?.ToString();
                    if (path == null) return;
                    run?.DeleteValue(entry.RegValueName, false);
                    StoreDisabled(entry.RegValueName, path);
                }
                else
                {
                    // HKLM / HKLMX86
                    var subKey = entry.Source == EntrySource.HKLM ? REG_RUN_HKLM : REG_RUN_HKLMX86;
                    var hive = Registry.LocalMachine;
                    using var run = hive.OpenSubKey(subKey, true);
                    if (run == null)
                    {
                        // Need admin — elevate
                        var args = $"--hklm-disable \"{entry.RegValueName}\" \"{entry.Path}\" \"{subKey}\"";
                        RunElevated(args);
                        StoreDisabled(entry.RegValueName, $"{entry.Path}|{subKey}|{(entry.Source == EntrySource.HKLM ? "HKLM" : "HKLMX86")}");
                        LoadEntries();
                        return;
                    }
                    var val = run.GetValue(entry.RegValueName)?.ToString();
                    if (val == null) return;
                    run.DeleteValue(entry.RegValueName, false);
                    StoreDisabled(entry.RegValueName, $"{val}|{subKey}|{(entry.Source == EntrySource.HKLM ? "HKLM" : "HKLMX86")}");
                }
            }
            LoadEntries();
            statusLabel.Text = entry.Enabled ? $"Disabled \"{entry.Name}\"" : $"Enabled \"{entry.Name}\"";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to toggle: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void RemoveEntry(StartupEntry entry)
    {
        if (MessageBox.Show($"Remove \"{entry.Name}\" from startup?",
            "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        try
        {
            if (entry.Source == EntrySource.StartupFolder || entry.Source == EntrySource.CommonStartup)
            {
                if (File.Exists(entry.RegValueName))
                    File.Delete(entry.RegValueName);
            }
            else if (entry.Source == EntrySource.HKCU || entry.Source == EntrySource.DisabledStore)
            {
                // Remove from Run key
                using var run = Registry.CurrentUser.OpenSubKey(REG_RUN, true);
                run?.DeleteValue(entry.RegValueName, false);
                // Remove from Disabled store too
                RemoveDisabled(entry.RegValueName);
            }
            else
            {
                // HKLM / HKLMX86
                var subKey = entry.Source == EntrySource.HKLM ? REG_RUN_HKLM : REG_RUN_HKLMX86;
                using var run = Registry.LocalMachine.OpenSubKey(subKey, true);
                if (run == null)
                {
                    RunElevated($"--hklm-remove \"{entry.RegValueName}\" \"\" \"{subKey}\"");
                    RemoveDisabled(entry.RegValueName);
                    LoadEntries();
                    return;
                }
                run.DeleteValue(entry.RegValueName, false);
                RemoveDisabled(entry.RegValueName);
            }

            LoadEntries();
            statusLabel.Text = $"Removed \"{entry.Name}\" from startup";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to remove: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    static void StoreDisabled(string name, string path)
    {
        using var key = Registry.CurrentUser.CreateSubKey(REG_LSTARTLAI);
        key?.SetValue(name, path);
    }

    static string? GetDisabledPath(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(REG_LSTARTLAI);
        return key?.GetValue(name)?.ToString();
    }

    static string? BrowseForFile()
    {
        try
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select program to add to startup",
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                CheckFileExists = true,
                AutoUpgradeEnabled = false,
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() == DialogResult.OK)
                return dialog.FileName;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open file dialog: {ex.Message}\n\nTry typing the path directly.",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        return null;
    }

    static void RemoveDisabled(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(REG_LSTARTLAI, true);
        key?.DeleteValue(name, false);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        foreach (var c in cards)
            if (!c.IsDisposed) c.Width = listPanel.ClientSize.Width - 24;
        var wide = Width >= 870;
        if (pathInput != null) { pathInput.Visible = wide; pathInput.Width = Math.Max(120, Width - 600); }
        if (pathAddBtn != null) pathAddBtn.Visible = wide;
    }

    internal static void HandleElevated(string[] args)
    {
        if (args.Length < 2) return;
        var action = args[0];
        var name = args[1];
        var value = args.Length > 2 ? args[2] : "";
        var subKey = args.Length > 3 ? args[3] : REG_RUN_HKLM;

        try
        {
            using var run = Registry.LocalMachine.OpenSubKey(subKey, true);
            if (run == null) { return; }

            switch (action)
            {
                case "--hklm-disable":
                    var origVal = run.GetValue(name)?.ToString();
                    if (origVal != null) { run.DeleteValue(name, false); }
                    break;
                case "--hklm-enable":
                    run.SetValue(name, value);
                    break;
                case "--hklm-remove":
                    run.DeleteValue(name, false);
                    break;
            }
        }
        catch { }
    }

    static void RunElevated(string args)
    {
        try
        {
            var exe = Application.ExecutablePath;
            if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LStartlai.exe");
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                Verb = "runas",
                UseShellExecute = true
            });
        }
        catch { }
    }

    static string? InputBox(IWin32Window owner, string prompt, string title, string defaultValue)
    {
        var form = new Form
        {
            Text = title, Width = 400, Height = 160,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
            BackColor = Color.FromArgb(26, 26, 46), ForeColor = Color.FromArgb(220, 220, 240),
            Font = new Font("Segoe UI", 10)
        };
        var lbl = new Label { Text = prompt, Left = 16, Top = 16, Width = 350, AutoSize = true };
        var txt = new TextBox { Left = 16, Top = 44, Width = 350, Text = defaultValue,
            BackColor = Color.FromArgb(40, 40, 70), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        var ok = new Button { Text = "OK", Left = 210, Top = 80, Width = 80, Height = 28,
            BackColor = Color.FromArgb(233, 69, 96), ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 } };
        var cancel = new Button { Text = "Cancel", Left = 296, Top = 80, Width = 80, Height = 28,
            BackColor = Color.FromArgb(50, 50, 80), ForeColor = Color.FromArgb(200, 200, 220),
            FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 } };

        string result = defaultValue;
        ok.Click += (_, _) => { result = txt.Text; form.DialogResult = DialogResult.OK; form.Close(); };
        cancel.Click += (_, _) => { form.DialogResult = DialogResult.Cancel; form.Close(); };
        txt.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) ok.PerformClick(); };
        form.Controls.AddRange([lbl, txt, ok, cancel]);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        if (form.ShowDialog(owner) != DialogResult.OK) return null;
        return result;
    }
}

class EntryCard : Panel
{
    public event Action<StartupEntry>? OnToggle;
    public event Action<StartupEntry>? OnRemove;

    readonly StartupEntry entry;
    readonly Color accent;
    readonly Color muted;

    public EntryCard(StartupEntry entry, Color card, Color text, Color muted, Color accent, Color border)
    {
        this.entry = entry;
        this.accent = accent;
        this.muted = muted;

        Height = 54;
        BackColor = card;
        Margin = new Padding(0, 0, 0, 6);
        Padding = new Padding(12, 0, 12, 0);
        Cursor = Cursors.Hand;

        var nameLbl = new Label
        {
            Text = entry.Name,
            ForeColor = text,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(12, 7),
            AutoSize = true,
            MaximumSize = new Size(280, 20)
        };

        var pathText = entry.Path;
        if (pathText.Length > 55) pathText = pathText[..52] + "...";
        var pathLbl = new Label
        {
            Text = pathText,
            ForeColor = muted,
            Font = new Font("Segoe UI", 8),
            Location = new Point(12, 28),
            AutoSize = true,
            MaximumSize = new Size(340, 16)
        };

        var statusLbl = new Label
        {
            Text = entry.Enabled ? "ON" : "OFF",
            ForeColor = entry.Enabled ? accent : muted,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Location = new Point(420, 8),
            AutoSize = true
        };

        var locationLbl = new Label
        {
            Text = entry.SourceLabel,
            ForeColor = muted,
            Font = new Font("Segoe UI", 7, FontStyle.Bold),
            Location = new Point(420, 28),
            AutoSize = true
        };

        var toggleBtn = new Button
        {
            Text = entry.Enabled ? "\u25D6" : "\u25D7",
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(50, 50, 80) },
            ForeColor = entry.Enabled ? accent : muted,
            Font = new Font("Segoe UI Symbol", 16),
            Size = new Size(34, 34),
            Location = new Point(480, 10),
            Cursor = Cursors.Hand
        };
        toggleBtn.Click += (_, _) => OnToggle?.Invoke(entry);

        var removeBtn = new Button
        {
            Text = "\u2715",
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(80, 30, 30) },
            ForeColor = Color.FromArgb(180, 80, 80),
            Font = new Font("Segoe UI", 12),
            Size = new Size(30, 30),
            Location = new Point(520, 12),
            Cursor = Cursors.Hand
        };
        removeBtn.Click += (_, _) => OnRemove?.Invoke(entry);

        Controls.AddRange([nameLbl, pathLbl, statusLbl, locationLbl, toggleBtn, removeBtn]);
        DoubleClick += (_, _) => OnToggle?.Invoke(entry);

        Paint += (_, e) =>
        {
            using var p = new Pen(border, 1);
            e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        };
    }
}
