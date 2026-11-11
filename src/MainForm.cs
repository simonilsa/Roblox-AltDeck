using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RobloxAltManager;

public class MainForm : Form
{
    private static readonly Color LaunchReady = Color.FromArgb(0, 150, 84);
    private static readonly Color LaunchIdle = Color.FromArgb(140, 148, 158);

    private readonly AccountStore _store = AccountStore.Load();
    private readonly List<Action> _texts = new();     // re-run when the language changes
    private readonly List<Control> _lockable = new(); // disabled while busy

    // Header
    private readonly Label _subtitle = new() { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 0, 3, 0) };
    private readonly Label _langLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = SystemColors.ControlText, Margin = new Padding(2, 9, 6, 0), Cursor = Cursors.Hand };
    private readonly ComboBox _langBox = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed,
        ItemHeight = 28, Width = 230, Font = new Font("Segoe UI", 10F), Margin = new Padding(0, 3, 0, 0), Cursor = Cursors.Hand
    };

    // Warning bar + steps
    private readonly TableLayoutPanel _banner = new()
    {
        Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Visible = false,
        BackColor = Color.FromArgb(255, 241, 196), Padding = new Padding(14, 10, 14, 10), Margin = new Padding(0, 0, 0, 8)
    };
    private readonly Label _bannerTitle = new() { AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.FromArgb(120, 53, 0) };
    private readonly Label _bannerBody = new() { AutoSize = true, ForeColor = Color.FromArgb(90, 50, 10), Margin = new Padding(3, 4, 3, 0) };
    private Button _bannerButton = null!;
    private readonly StepCard _step1 = new(), _step2 = new(), _step3 = new();

    // Accounts
    private readonly ListView _accounts = new()
    {
        Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true,
        HideSelection = false, MultiSelect = true, BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Label _emptyHint = new()
    {
        Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(90, 96, 104),
        BackColor = SystemColors.Window, Font = new Font("Segoe UI", 11F), Cursor = Cursors.Hand
    };
    private readonly Label _countLabel = new() { AutoSize = true, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 4, 0, 0) };

    // Destination
    private readonly RadioButton _modeGame = new() { AutoSize = true, Checked = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
    private readonly RadioButton _modeFriend = new() { AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
    private readonly ComboBox _gameBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly Label _gameInfo = InfoLabel();
    private readonly TextBox _jobBox = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _friendBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly Label _friendInfo = InfoLabel();
    private readonly NumericUpDown _delay = new() { Minimum = 0, Maximum = 60, Value = 5, Width = 60 };
    private readonly Button _launch = new()
    {
        Dock = DockStyle.Fill, Height = 50, MinimumSize = new Size(0, 50),
        Font = new Font("Segoe UI", 12F, FontStyle.Bold), BackColor = LaunchIdle,
        ForeColor = Color.White, FlatStyle = FlatStyle.Flat
    };

    // Log + status bar
    private readonly TextBox _log = new()
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
        BackColor = SystemColors.Window, BorderStyle = BorderStyle.None
    };
    private readonly ToolStripStatusLabel _status = new() { Font = new Font("Segoe UI", 9.75F, FontStyle.Bold) };

    private readonly System.Windows.Forms.Timer _watch = new() { Interval = 1500 };
    private List<FriendItem> _friends = new();
    private Func<string> _gameInfoFn = () => L.T("dest.gameHint");
    private Func<string> _friendInfoFn = () => L.T("dest.friendHint");
    private MultiState _multiState = MultiState.RobloxRunning;
    private bool _multiOn;
    private bool _busy;
    private bool _loadingList;
    private string _lastLookup = "";

    public MainForm()
    {
        L.Set(_store.Language ?? L.DetectDefault());

        Font = new Font("Segoe UI", 9.75F);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1080, 760);
        MinimumSize = new Size(960, 680);
        StartPosition = FormStartPosition.CenterScreen;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        OnLang(() => Text = "AltDeck – " + L.T("app.window"));

        _launch.FlatAppearance.BorderSize = 0;
        _launch.Click += async (_, _) => await LaunchAsync(CheckedAccounts());
        _lockable.Add(_launch);

        // Everything stacks in one table so hidden rows (the warning bar) collapse cleanly.
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12, 8, 12, 6) };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // header
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // warning bar
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // steps
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // accounts | destination
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));  // activity log

        outer.Controls.Add(BuildHeader(), 0, 0);
        outer.Controls.Add(BuildBanner(), 0, 1);
        outer.Controls.Add(BuildSteps(), 0, 2);

        var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.Controls.Add(BuildAccountsPanel(), 0, 0);
        main.Controls.Add(BuildDestinationPanel(), 1, 0);
        outer.Controls.Add(main, 0, 3);

        var logGroup = Tx(new GroupBox { Dock = DockStyle.Fill, Padding = new Padding(8) }, "log.group");
        logGroup.Controls.Add(_log);
        outer.Controls.Add(logGroup, 0, 4);

        var strip = new StatusStrip { SizingGrip = false };
        var disclaimer = new ToolStripStatusLabel { ForeColor = SystemColors.GrayText };
        OnLang(() => disclaimer.Text = L.T("status.disclaimer"));
        strip.Items.Add(_status);
        strip.Items.Add(new ToolStripStatusLabel { Spring = true });
        strip.Items.Add(disclaimer);

        // Fill control first, then edge-docked controls.
        Controls.Add(outer);
        Controls.Add(strip);

        Load += OnLoad;
        Shown += (_, _) => AskLanguageOnFirstStart();
        _watch.Tick += (_, _) => WatchRoblox();
        FormClosing += (_, _) =>
        {
            _watch.Stop();
            SaveSettings();
        };
    }

    // ================= Layout =================

    private Control BuildHeader()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 8) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var left = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
        left.Controls.Add(new Label { Text = "AltDeck", AutoSize = true, Font = new Font("Segoe UI", 17F, FontStyle.Bold) });
        left.Controls.Add(_subtitle);
        OnLang(() => _subtitle.Text = L.T("app.subtitle"));

        // Language picker: a globe, the label, and a menu that shows flags.
        var right = new FlowLayoutPanel
        {
            AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Margin = new Padding(0, 4, 0, 0), Padding = new Padding(0, 5, 0, 5)
        };
        var globe = new PictureBox
        {
            Image = Flags.Globe, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(28, 28),
            Margin = new Padding(0, 3, 4, 0), Cursor = Cursors.Hand
        };
        right.Controls.Add(globe);
        right.Controls.Add(_langLabel);
        right.Controls.Add(_langBox);

        void OpenMenu()
        {
            _langBox.Focus();
            _langBox.DroppedDown = true;
        }
        globe.Click += (_, _) => OpenMenu();
        _langLabel.Click += (_, _) => OpenMenu();
        _langBox.DrawItem += (_, e) =>
            Flags.DrawLanguageRow(e, e.Index >= 0 ? _langBox.Items[e.Index] : _langBox.SelectedItem, _langBox.Font);

        // Always include the English word, so anyone stuck in the wrong language can find the picker.
        OnLang(() => _langLabel.Text = L.Code == "en" ? "Language" : $"{L.T("lang.label")} (Language)");

        t.Controls.Add(left, 0, 0);
        t.Controls.Add(right, 1, 0);
        return t;
    }

    private Control BuildBanner()
    {
        _banner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _banner.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var text = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
        text.Controls.Add(_bannerTitle);
        text.Controls.Add(_bannerBody);

        _bannerButton = Btn("banner.button", async (_, _) => await FixMultiInstanceAsync(askFirst: true));
        _bannerButton.BackColor = Color.FromArgb(196, 43, 28);
        _bannerButton.ForeColor = Color.White;
        _bannerButton.FlatStyle = FlatStyle.Flat;
        _bannerButton.FlatAppearance.BorderSize = 0;
        _bannerButton.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        _bannerButton.MinimumSize = new Size(0, 40);
        _bannerButton.Anchor = AnchorStyles.Right;

        _banner.Controls.Add(text, 0, 0);
        _banner.Controls.Add(_bannerButton, 1, 0);
        _banner.Resize += (_, _) => WrapBanner();
        return _banner;
    }

    private void WrapBanner()
    {
        int width = Math.Max(240, _banner.ClientSize.Width - (_bannerButton.Visible ? _bannerButton.Width : 0) - 60);
        _bannerTitle.MaximumSize = new Size(width, 0);
        _bannerBody.MaximumSize = new Size(width, 0);
    }

    private Control BuildSteps()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 1, Margin = new Padding(0) };
        for (int i = 0; i < 3; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        _step3.Margin = new Padding(0, 0, 0, 8);
        t.Controls.Add(_step1, 0, 0);
        t.Controls.Add(_step2, 1, 0);
        t.Controls.Add(_step3, 2, 0);
        return t;
    }

    private Control BuildAccountsPanel()
    {
        var group = Tx(new GroupBox { Dock = DockStyle.Fill, Padding = new Padding(8) }, "acc.group");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
        var add = Btn("acc.add", (_, _) => AddAccount());
        add.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold);
        bar.Controls.Add(add);
        bar.Controls.Add(Btn("acc.relogin", (_, _) => ReloginSelected()));
        bar.Controls.Add(Btn("acc.rename", (_, _) => RenameSelected()));
        bar.Controls.Add(Btn("acc.remove", (_, _) => RemoveSelected()));
        bar.Controls.Add(Btn("acc.selectAll", (_, _) => SetAllChecked(true)));
        bar.Controls.Add(Btn("acc.selectNone", (_, _) => SetAllChecked(false)));

        // The list, with a big "add your first account" hint on top of it while it's empty.
        var listHost = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        listHost.Controls.Add(_emptyHint);
        listHost.Controls.Add(_accounts);
        _emptyHint.Click += (_, _) => AddAccount();
        OnLang(() => _emptyHint.Text = L.T("acc.empty"));

        _accounts.Columns.Add("", 210);
        _accounts.Columns.Add("", 160);
        _accounts.Columns.Add("", 150);
        OnLang(() =>
        {
            _accounts.Columns[0].Text = L.T("acc.colAccount");
            _accounts.Columns[1].Text = L.T("acc.colUsername");
            _accounts.Columns[2].Text = L.T("acc.colLast");
        });

        layout.Controls.Add(bar, 0, 0);
        layout.Controls.Add(listHost, 0, 1);
        layout.Controls.Add(_countLabel, 0, 2);
        group.Controls.Add(layout);

        var menu = new ContextMenuStrip();
        MenuItem(menu, "menu.launchOne", async (_, _) => await LaunchAsync(SelectedAccounts()));
        menu.Items.Add(new ToolStripSeparator());
        MenuItem(menu, "menu.rename", (_, _) => RenameSelected());
        MenuItem(menu, "menu.relogin", (_, _) => ReloginSelected());
        MenuItem(menu, "menu.up", (_, _) => MoveSelected(-1));
        MenuItem(menu, "menu.down", (_, _) => MoveSelected(1));
        menu.Items.Add(new ToolStripSeparator());
        MenuItem(menu, "menu.remove", (_, _) => RemoveSelected());
        _accounts.ContextMenuStrip = menu;

        _accounts.ItemChecked += (_, e) =>
        {
            if (_loadingList || e.Item.Tag is not Account acc) return;
            acc.Selected = e.Item.Checked;
            _store.Save();
            UpdateSteps();
        };
        _accounts.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };

        return group;
    }

    private Control BuildDestinationPanel()
    {
        var group = Tx(new GroupBox { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 10) }, "dest.group");
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Tx(_modeGame, "dest.modeGame");
        Tx(_modeFriend, "dest.modeFriend");
        OnLang(() => _jobBox.PlaceholderText = L.T("dest.jobPlaceholder"));

        var lookup = Btn("dest.lookup", async (_, _) => await LookupGameAsync());
        var loadFriends = Btn("dest.loadFriends", async (_, _) => await LoadFriendsAsync());
        var closeAll = Btn("dest.closeAll", async (_, _) => await FixMultiInstanceAsync(askFirst: true));
        closeAll.Dock = DockStyle.Fill;

        AddRow(t, _modeGame);
        AddRow(t, Caption("dest.gameCaption"));
        AddRow(t, Pair(_gameBox, lookup));
        AddRow(t, _gameInfo);
        AddRow(t, Caption("dest.serverCaption"));
        AddRow(t, _jobBox);
        AddRow(t, new Label { Height = 12 });
        AddRow(t, _modeFriend);
        AddRow(t, Caption("dest.friendCaption"));
        AddRow(t, Pair(_friendBox, loadFriends));
        AddRow(t, _friendInfo);

        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        t.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, t.RowCount++);

        var delayRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
        delayRow.Controls.Add(Caption("dest.wait"));
        delayRow.Controls.Add(_delay);
        delayRow.Controls.Add(Caption("dest.seconds"));
        AddRow(t, delayRow);
        AddRow(t, _launch);
        AddRow(t, closeAll);

        group.Controls.Add(t);

        // Typing in a section switches to that mode.
        _gameBox.Enter += (_, _) => _modeGame.Checked = true;
        _jobBox.Enter += (_, _) => _modeGame.Checked = true;
        _friendBox.Enter += (_, _) => _modeFriend.Checked = true;
        _modeGame.CheckedChanged += (_, _) => UpdateSteps();
        _gameBox.TextChanged += (_, _) => UpdateSteps();
        _friendBox.TextChanged += (_, _) => UpdateSteps();

        _gameBox.SelectedIndexChanged += (_, _) =>
        {
            if (_gameBox.SelectedItem is SavedGame g)
            {
                SetGameInfo(() => "✔ " + g.Name);
                _lastLookup = g.ToString();
            }
        };
        _gameBox.Leave += async (_, _) =>
        {
            if (_gameBox.Text.Trim() != _lastLookup) await LookupGameAsync();
        };
        _gameBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await LookupGameAsync(); }
        };

        return group;
    }

    // ================= Startup, language, settings =================

    private void OnLoad(object? sender, EventArgs e)
    {
        _langBox.Items.AddRange(L.Languages.Cast<object>().ToArray());
        _langBox.SelectedItem = L.Languages.FirstOrDefault(l => l.Code == L.Code);
        _langBox.SelectedIndexChanged += (_, _) =>
        {
            if (_langBox.SelectedItem is L.Language lang && lang.Code != L.Code)
                SwitchLanguage(lang.Code);
        };

        PopulateGames();
        _gameBox.Text = _store.LastPlace ?? "";
        _jobBox.Text = _store.LastJob ?? "";
        _friendBox.Text = _store.LastFriend ?? "";
        (_store.FriendMode ? _modeFriend : _modeGame).Checked = true;
        _delay.Value = Math.Clamp(_store.Delay, 0, 60);

        var saved = FindSavedGame(ParsePlaceId(_gameBox.Text));
        if (saved != null && !_gameBox.Text.Contains("LinkCode", StringComparison.OrdinalIgnoreCase))
        {
            SetGameInfo(() => "✔ " + saved.Name);
            _lastLookup = _gameBox.Text.Trim();
        }
        else SetGameInfo(_gameInfoFn);
        SetFriendInfo(_friendInfoFn);

        RefreshAccounts();

        var state = MultiInstance.Enable(out var error);
        ApplyMultiState(state, error, startup: true);
        if (_store.Accounts.Count == 0) Log(L.T("log.firstRun"));
        _watch.Start();
    }

    private void SwitchLanguage(string code)
    {
        L.Set(code);
        _store.Language = L.Code;
        _store.Save();
        var item = L.Languages.FirstOrDefault(l => l.Code == L.Code);
        if (item != null && !Equals(_langBox.SelectedItem, item)) _langBox.SelectedItem = item;
        ApplyLanguage();
    }

    /// <summary>The first time the app opens, ask for the language with a big list of flags.</summary>
    private void AskLanguageOnFirstStart()
    {
        if (_store.Language != null || L.Languages.Count < 2) return;
        using var dialog = new LanguageDialog();
        dialog.ShowDialog(this);
        SwitchLanguage(dialog.SelectedCode); // also remembers the choice, so it's only asked once
    }

    private void ApplyLanguage()
    {
        foreach (var apply in _texts) apply();
        _gameInfo.Text = _gameInfoFn();
        _friendInfo.Text = _friendInfoFn();
        PopulateFriends();
        UpdateMultiUi();
        UpdateSteps();
    }

    private void SaveSettings()
    {
        _store.LastPlace = _gameBox.Text.Trim();
        _store.LastJob = _jobBox.Text.Trim();
        _store.LastFriend = _friendBox.Text.Trim();
        _store.FriendMode = _modeFriend.Checked;
        _store.Delay = (int)_delay.Value;
        _store.Save();
    }

    // ================= Multi-instance =================

    private void ApplyMultiState(MultiState state, string? error, bool startup)
    {
        bool wasOn = _multiOn;
        _multiState = state;
        _multiOn = state == MultiState.On;

        if (startup)
            Log(state switch
            {
                MultiState.On => L.T("log.multiOn"),
                MultiState.RobloxRunning => L.T("log.multiOff"),
                _ => L.F("log.multiFailed", error ?? "")
            });
        else if (_multiOn && !wasOn)
            Log(L.T("log.multiNowOn"));

        UpdateMultiUi();
    }

    private void UpdateMultiUi()
    {
        bool failed = _multiState == MultiState.Failed;

        _status.Text = L.T(_multiOn ? "status.multiOn" : "status.multiOff");
        _status.ForeColor = _multiOn ? Color.ForestGreen : Color.Firebrick;

        _banner.Visible = !_multiOn;
        _bannerTitle.Text = L.T(failed ? "banner.failTitle" : "banner.title");
        _bannerBody.Text = L.T(failed ? "banner.failText" : "banner.text");
        _bannerButton.Visible = !failed;
        WrapBanner();

        _step1.Set(L.T("step1.title"),
            L.T(_multiOn ? "step1.ok" : failed ? "step1.failed" : "step1.bad"),
            _multiOn ? StepCard.State.Done : StepCard.State.Problem);
    }

    /// <summary>While multi-instance is off, turn it on by itself as soon as every Roblox window is closed.</summary>
    private void WatchRoblox()
    {
        if (_multiOn || _busy) return;

        if (MultiInstance.IsRobloxRunning())
        {
            if (_multiState != MultiState.RobloxRunning) ApplyMultiState(MultiState.RobloxRunning, null, startup: false);
            return;
        }

        var state = MultiInstance.Enable(out var error);
        if (state != _multiState || state == MultiState.On) ApplyMultiState(state, error, startup: false);
    }

    private async Task<bool> FixMultiInstanceAsync(bool askFirst)
    {
        if (!await CloseRobloxAsync(askFirst)) return false;
        if (_multiOn) return true;
        if (MultiInstance.IsRobloxRunning()) return false;

        var state = MultiInstance.Enable(out var error);
        ApplyMultiState(state, error, startup: false);
        if (!_multiOn) Log(L.F("log.multiFailed", error ?? ""));
        return _multiOn;
    }

    private async Task<bool> CloseRobloxAsync(bool askFirst)
    {
        var procs = Process.GetProcessesByName("RobloxPlayerBeta");
        try
        {
            if (procs.Length == 0)
            {
                Log(L.T("log.noRoblox"));
                return true;
            }
            if (askFirst && MessageBox.Show(this, L.F("banner.confirm", procs.Length), L.T("banner.confirmTitle"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return false;

            foreach (var p in procs)
            {
                try { p.Kill(); } catch { /* already closed */ }
            }

            // Give the windows a few seconds to actually exit.
            for (int i = 0; i < 30 && MultiInstance.IsRobloxRunning(); i++)
                await Task.Delay(200);

            Log(L.F("log.closed", procs.Length));
            return true;
        }
        finally
        {
            foreach (var p in procs) p.Dispose();
        }
    }

    // ================= Launching =================

    private async Task LaunchAsync(List<Account> accounts)
    {
        if (_busy) return;
        if (accounts.Count == 0)
        {
            Log(L.T("log.noneTicked"));
            return;
        }

        if (!_multiOn && accounts.Count > 1)
        {
            var answer = MessageBox.Show(this, L.T("dlg.launchOff"), L.T("dlg.launchOffTitle"),
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (answer == DialogResult.Cancel) return;
            if (answer == DialogResult.Yes && !await FixMultiInstanceAsync(askFirst: false))
            {
                Log(L.T("log.stillOff"));
                return;
            }
            if (answer == DialogResult.No) Log(L.T("log.warnOff"));
        }

        SaveSettings();
        SetBusy(true);
        try
        {
            var target = await ResolveTargetAsync(accounts[0]);
            if (target == null) return;

            Log(L.F("log.launching", accounts.Count, target.Description));

            for (int i = 0; i < accounts.Count; i++)
            {
                var acc = accounts[i];
                string who = $"[{i + 1}/{accounts.Count}] {acc.DisplayLabel}:";

                if (target.Kind == TargetKind.Follow && acc.UserId == target.UserId)
                {
                    Log(L.F("log.skipSelf", who));
                    continue;
                }

                try
                {
                    string ticket = await RobloxApi.GetAuthTicketAsync(acc.Cookie);
                    Process.Start(new ProcessStartInfo(RobloxApi.BuildLaunchUri(ticket, target)) { UseShellExecute = true });
                    acc.LastLaunched = DateTime.Now.ToString("g");
                    Log(L.F("log.launched", who));
                }
                catch (Win32Exception)
                {
                    Log(L.F("log.notInstalled", who));
                    break;
                }
                catch (Exception ex)
                {
                    Log(L.F("log.failed", who, ex.Message));
                }

                if (i < accounts.Count - 1 && _delay.Value > 0)
                    await Task.Delay(TimeSpan.FromSeconds((double)_delay.Value));
            }
            Log(L.T("log.done"));
        }
        catch (Exception ex)
        {
            Log(L.F("log.stopped", ex.Message));
        }
        finally
        {
            _store.Save();
            RefreshAccounts();
            SetBusy(false);
        }
    }

    private async Task<LaunchTarget?> ResolveTargetAsync(Account account)
    {
        if (_modeFriend.Checked)
        {
            var friend = await ResolveFriendAsync();
            return friend == null ? null : new LaunchTarget(TargetKind.Follow, L.F("target.follow", friend.Name), UserId: friend.Id);
        }

        string text = _gameBox.Text.Trim();
        if (text.Length == 0)
        {
            Log(L.T("log.needGame"));
            return null;
        }

        // New-style share links: roblox.com/share?code=...&type=Server
        if (IsShareLink(text))
        {
            var code = Regex.Match(text, @"[?&]code=([\w-]+)", RegexOptions.IgnoreCase);
            var type = Regex.Match(text, @"[?&]type=(\w+)", RegexOptions.IgnoreCase);
            if (!code.Success)
            {
                Log(L.T("log.shareNoCode"));
                return null;
            }
            if (type.Success && !type.Groups[1].Value.Equals("Server", StringComparison.OrdinalIgnoreCase))
            {
                Log(L.T("log.shareOnlyServer"));
                return null;
            }
            Log(L.T("log.resolvingShare"));
            var (placeId, linkCode) = await RobloxApi.ResolveServerShareLinkAsync(code.Groups[1].Value, account.Cookie);
            return new LaunchTarget(TargetKind.PrivateServer, L.F("target.private", GameLabel(placeId)), placeId, LinkCode: linkCode);
        }

        long? place = ParsePlaceId(text);
        if (place == null)
        {
            Log(L.T("log.badPlace"));
            return null;
        }

        // Old-style private server links: ...?privateServerLinkCode=XXXX
        var priv = Regex.Match(text, @"privateServerLinkCode=([\w-]+)", RegexOptions.IgnoreCase);
        if (priv.Success)
            return new LaunchTarget(TargetKind.PrivateServer, L.F("target.private", GameLabel(place.Value)), place.Value, LinkCode: priv.Groups[1].Value);

        string job = _jobBox.Text.Trim();
        return job.Length > 0
            ? new LaunchTarget(TargetKind.Server, L.F("target.server", GameLabel(place.Value), job), place.Value, JobId: job)
            : new LaunchTarget(TargetKind.Place, GameLabel(place.Value), place.Value);
    }

    private async Task<RobloxUser?> ResolveFriendAsync()
    {
        if (_friendBox.SelectedItem is FriendItem f && _friendBox.Text == f.ToString())
        {
            if (!f.InGame) Log(L.F("log.friendOffline", f.User.Name));
            return f.User;
        }

        string text = _friendBox.Text.Trim();
        if (text.Length == 0)
        {
            Log(L.T("log.needFriend"));
            return null;
        }

        var profile = Regex.Match(text, @"users/(\d+)", RegexOptions.IgnoreCase);
        if (profile.Success)
        {
            long id = long.Parse(profile.Groups[1].Value);
            var users = await RobloxApi.GetUsersAsync(new[] { id });
            return users.FirstOrDefault() ?? new RobloxUser(id, id.ToString(), "");
        }

        string username = FriendNameFromText(text);
        var user = await RobloxApi.GetUserByUsernameAsync(username);
        if (user == null)
        {
            Log(L.F("log.noUser", username));
            return null;
        }
        SetFriendInfo(() => L.F("dest.willJoin", $"{user.DisplayName} (@{user.Name})"));
        return user;
    }

    // ================= Games =================

    private async Task LookupGameAsync()
    {
        string text = _gameBox.Text.Trim();
        _lastLookup = text;
        if (text.Length == 0)
        {
            SetGameInfo(() => L.T("dest.gameHint"));
            return;
        }

        if (IsShareLink(text))
        {
            SetGameInfo(() => L.T("dest.shareLink"));
            return;
        }

        long? placeId = ParsePlaceId(text);
        if (placeId == null)
        {
            SetGameInfo(() => L.T("dest.noPlaceId"));
            return;
        }

        bool isPrivateLink = text.Contains("privateServerLinkCode", StringComparison.OrdinalIgnoreCase);
        SetGameInfo(() => L.T("dest.lookingUp"));
        try
        {
            string name = await RobloxApi.GetGameNameAsync(placeId.Value) ?? $"Place {placeId}";
            var saved = RememberGame(placeId.Value, name);

            if (isPrivateLink)
            {
                SetGameInfo(() => "✔ " + L.F("dest.privateLink", name));
            }
            else
            {
                _gameBox.SelectedItem = saved;
                SetGameInfo(() => "✔ " + name);
                _lastLookup = _gameBox.Text.Trim();
            }
            UpdateSteps();
        }
        catch (Exception ex)
        {
            string message = ex.Message;
            SetGameInfo(() => L.F("dest.lookupFailed", message));
        }
    }

    private SavedGame RememberGame(long placeId, string name)
    {
        _store.Games.RemoveAll(g => g.PlaceId == placeId);
        var game = new SavedGame { PlaceId = placeId, Name = name };
        _store.Games.Insert(0, game);
        if (_store.Games.Count > 30) _store.Games.RemoveRange(30, _store.Games.Count - 30);
        _store.Save();

        string text = _gameBox.Text;
        PopulateGames();
        _gameBox.Text = text;
        return game;
    }

    private void PopulateGames()
    {
        _gameBox.BeginUpdate();
        _gameBox.Items.Clear();
        _gameBox.Items.AddRange(_store.Games.Cast<object>().ToArray());
        _gameBox.EndUpdate();
    }

    private SavedGame? FindSavedGame(long? placeId) =>
        placeId == null ? null : _store.Games.FirstOrDefault(g => g.PlaceId == placeId);

    private string GameLabel(long placeId) => FindSavedGame(placeId)?.Name ?? L.F("target.place", placeId);

    // ================= Friends =================

    private async Task LoadFriendsAsync()
    {
        var acc = CheckedAccounts().FirstOrDefault() ?? SelectedAccounts().FirstOrDefault() ?? _store.Accounts.FirstOrDefault();
        if (acc == null)
        {
            Log(L.T("log.needAccount"));
            return;
        }

        SetBusy(true);
        try
        {
            string owner = acc.Username;
            SetFriendInfo(() => L.F("dest.friendsLoading", owner));
            var friends = await RobloxApi.GetFriendsAsync(acc.UserId, acc.Cookie);

            Dictionary<long, Presence> presence = new();
            try { presence = await RobloxApi.GetPresenceAsync(friends.Select(f => f.Id), acc.Cookie); }
            catch (Exception ex) { Log(L.F("log.presenceFailed", ex.Message)); }

            _friends = friends
                .Select(f => new FriendItem(f, presence.GetValueOrDefault(f.Id)))
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.User.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            PopulateFriends();

            int total = _friends.Count, inGame = _friends.Count(i => i.InGame);
            SetFriendInfo(() => L.F("dest.friendsLoaded", total, owner, inGame));
            Log(L.F("log.friendsLoaded", total, owner, inGame));
            _modeFriend.Checked = true;
        }
        catch (Exception ex)
        {
            SetFriendInfo(() => L.T("dest.friendsFailed"));
            Log(L.F("log.friendsFailed", ex.Message));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void PopulateFriends()
    {
        string typed = _friendBox.Text;
        _friendBox.BeginUpdate();
        _friendBox.Items.Clear();
        _friendBox.Items.AddRange(_friends.Cast<object>().ToArray());
        _friendBox.EndUpdate();
        _friendBox.Text = typed;
    }

    // ================= Account management =================

    private async void AddAccount()
    {
        if (_busy) return;
        using var dlg = new AccountDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        string? cookie = dlg.UseCookie ? CleanCookie(dlg.Cookie) : RunLogin(dlg.Username, dlg.Password);
        if (string.IsNullOrEmpty(cookie)) return;

        await SaveAccountAsync(cookie, dlg.Alias, dlg.RememberPassword ? dlg.Password : null);
    }

    private async void ReloginSelected()
    {
        var acc = SelectedAccounts().FirstOrDefault();
        if (acc == null)
        {
            Log(L.T("log.selectFirst"));
            return;
        }

        string? cookie = RunLogin(acc.Username, acc.Password);
        if (string.IsNullOrEmpty(cookie)) return;
        await SaveAccountAsync(cookie, acc.Alias, null);
    }

    private string? RunLogin(string username, string password)
    {
        using var win = new LoginWindow(username, password);
        return win.ShowDialog(this) == DialogResult.OK ? win.Cookie : null;
    }

    private async Task SaveAccountAsync(string cookie, string alias, string? password)
    {
        SetBusy(true);
        try
        {
            var user = await RobloxApi.GetUserAsync(cookie);
            var acc = _store.Accounts.FirstOrDefault(a => a.UserId == user.Id);
            bool isNew = acc == null;
            acc ??= new Account { Selected = true };

            acc.UserId = user.Id;
            acc.Username = user.Name;
            if (!string.IsNullOrWhiteSpace(alias)) acc.Alias = alias;
            else if (isNew) acc.Alias = user.DisplayName;
            acc.Cookie = cookie;
            if (!string.IsNullOrEmpty(password)) acc.Password = password;
            if (isNew) _store.Accounts.Add(acc);

            _store.Save();
            RefreshAccounts();
            Log(L.F(isNew ? "log.added" : "log.updated", acc.DisplayLabel));
        }
        catch (Exception ex)
        {
            Log(L.F("log.addFailed", ex.Message));
            MessageBox.Show(this, ex.Message, L.T("add.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RenameSelected()
    {
        var acc = SelectedAccounts().FirstOrDefault();
        if (acc == null)
        {
            Log(L.T("log.selectFirst"));
            return;
        }
        var name = Prompt.Show(this, L.T("dlg.renameTitle"), L.F("dlg.renameLabel", acc.Username), acc.Alias);
        if (name == null) return;
        acc.Alias = name;
        _store.Save();
        RefreshAccounts();
    }

    private void RemoveSelected()
    {
        var selected = SelectedAccounts();
        if (selected.Count == 0)
        {
            Log(L.T("log.selectFirst"));
            return;
        }

        string names = string.Join(", ", selected.Select(a => a.Username));
        if (MessageBox.Show(this, L.F("dlg.removeConfirm", names), L.T("dlg.removeTitle"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        foreach (var a in selected) _store.Accounts.Remove(a);
        _store.Save();
        RefreshAccounts();
        Log(L.F("log.removed", names));
    }

    private void MoveSelected(int direction)
    {
        var acc = SelectedAccounts().FirstOrDefault();
        if (acc == null) return;
        int i = _store.Accounts.IndexOf(acc);
        int j = i + direction;
        if (j < 0 || j >= _store.Accounts.Count) return;
        (_store.Accounts[i], _store.Accounts[j]) = (_store.Accounts[j], _store.Accounts[i]);
        _store.Save();
        RefreshAccounts();
        foreach (ListViewItem item in _accounts.Items) item.Selected = false;
        _accounts.Items[j].Selected = true;
    }

    private void SetAllChecked(bool value)
    {
        _loadingList = true;
        foreach (ListViewItem item in _accounts.Items)
        {
            item.Checked = value;
            ((Account)item.Tag!).Selected = value;
        }
        _loadingList = false;
        _store.Save();
        UpdateSteps();
    }

    // ================= UI state =================

    private void UpdateSteps()
    {
        int n = _accounts.CheckedItems.Count;
        _step2.Set(L.T("step2.title"),
            n == 0 ? L.T("step2.none") : L.F("step2.some", n),
            n == 0 ? StepCard.State.Todo : StepCard.State.Done);

        string? destination = DestinationSummary();
        _step3.Set(L.T("step3.title"),
            destination ?? L.T("step3.none"),
            destination == null ? StepCard.State.Todo : StepCard.State.Done);

        _launch.Text = n == 0 ? L.T("dest.launch") : L.F("dest.launchN", n);
        _launch.BackColor = n > 0 && destination != null && !_busy ? LaunchReady : LaunchIdle;
        _countLabel.Text = L.F("acc.count", n, _accounts.Items.Count);
        _emptyHint.Visible = _accounts.Items.Count == 0;
    }

    private string? DestinationSummary()
    {
        if (_modeFriend.Checked)
        {
            if (_friendBox.SelectedItem is FriendItem f && _friendBox.Text == f.ToString())
                return L.F("step3.friend", f.User.DisplayName);
            string typed = _friendBox.Text.Trim();
            return typed.Length == 0 ? null : L.F("step3.friend", FriendNameFromText(typed));
        }

        string text = _gameBox.Text.Trim();
        if (text.Length == 0) return null;
        if (IsShareLink(text)) return L.T("step3.private");
        long? id = ParsePlaceId(text);
        if (id == null) return null;
        string name = GameLabel(id.Value);
        return text.Contains("privateServerLinkCode", StringComparison.OrdinalIgnoreCase)
            ? $"{name} · {L.T("step3.private")}"
            : name;
    }

    private List<Account> CheckedAccounts() =>
        _accounts.CheckedItems.Cast<ListViewItem>().Select(i => (Account)i.Tag!).ToList();

    private List<Account> SelectedAccounts() =>
        _accounts.SelectedItems.Cast<ListViewItem>().Select(i => (Account)i.Tag!).ToList();

    private void RefreshAccounts()
    {
        var selectedIds = SelectedAccounts().Select(a => a.UserId).ToHashSet();
        _loadingList = true;
        _accounts.BeginUpdate();
        _accounts.Items.Clear();
        foreach (var a in _store.Accounts)
        {
            _accounts.Items.Add(new ListViewItem(new[]
            {
                string.IsNullOrWhiteSpace(a.Alias) ? a.Username : a.Alias,
                a.Username,
                string.IsNullOrEmpty(a.LastLaunched) ? "—" : a.LastLaunched
            })
            {
                Tag = a,
                Checked = a.Selected,
                Selected = selectedIds.Contains(a.UserId)
            });
        }
        _accounts.EndUpdate();
        _loadingList = false;
        UpdateSteps();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        foreach (var c in _lockable) c.Enabled = !busy;
        UseWaitCursor = busy;
        UpdateSteps();
    }

    private void SetGameInfo(Func<string> text)
    {
        _gameInfoFn = text;
        _gameInfo.Text = text();
    }

    private void SetFriendInfo(Func<string> text)
    {
        _friendInfoFn = text;
        _friendInfo.Text = text();
    }

    private void Log(string text) =>
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}]  {text}{Environment.NewLine}");

    // ================= Small helpers =================

    private static string FriendNameFromText(string text)
    {
        var handle = Regex.Match(text, @"\(@(\w+)\)");
        return handle.Success ? handle.Groups[1].Value : text.Split('–')[0].Trim().TrimStart('@');
    }

    private static long? ParsePlaceId(string text)
    {
        text = text.Trim();
        foreach (var pattern in new[] { @"games/(\d+)", @"[?&]placeId=(\d+)", @"\((\d+)\)\s*$", @"^(\d+)$" })
        {
            var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (m.Success && long.TryParse(m.Groups[1].Value, out var id)) return id;
        }
        return null;
    }

    private static bool IsShareLink(string text) =>
        Regex.IsMatch(text, @"roblox\.com/share", RegexOptions.IgnoreCase);

    private static string CleanCookie(string raw)
    {
        raw = raw.Trim().Trim('"');
        const string prefix = ".ROBLOSECURITY=";
        if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) raw = raw[prefix.Length..];
        return raw.TrimEnd(';').Trim();
    }

    /// <summary>Registers a translatable text so it updates when the language changes.</summary>
    private void OnLang(Action apply)
    {
        _texts.Add(apply);
        apply();
    }

    private TControl Tx<TControl>(TControl control, string key) where TControl : Control
    {
        OnLang(() => control.Text = L.T(key));
        return control;
    }

    private void MenuItem(ContextMenuStrip menu, string key, EventHandler onClick)
    {
        var item = new ToolStripMenuItem();
        item.Click += onClick;
        OnLang(() => item.Text = L.T(key));
        menu.Items.Add(item);
    }

    private Button Btn(string key, EventHandler onClick)
    {
        var b = new Button { AutoSize = true, Height = 32, Padding = new Padding(6, 0, 6, 0) };
        b.Click += onClick;
        Tx(b, key);
        _lockable.Add(b);
        return b;
    }

    private Label Caption(string key) =>
        Tx(new Label { AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, key);

    private static Label InfoLabel() =>
        new() { AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(440, 0), Padding = new Padding(0, 0, 0, 4) };

    private static void AddRow(TableLayoutPanel t, Control c)
    {
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(c, 0, t.RowCount);
        t.RowCount++;
    }

    private static Control Pair(Control main, Control side)
    {
        var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0) };
        p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        main.Margin = new Padding(0, 3, 6, 3);
        p.Controls.Add(main, 0, 0);
        p.Controls.Add(side, 1, 0);
        return p;
    }
}
