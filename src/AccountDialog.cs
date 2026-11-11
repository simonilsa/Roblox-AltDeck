namespace RobloxAltManager;

/// <summary>"Add account" form: username + password (or a cookie for advanced users) and a nickname.</summary>
public class AccountDialog : Form
{
    private readonly TextBox _username = new() { Dock = DockStyle.Fill, PlaceholderText = L.T("add.usernamePh") };
    private readonly TextBox _password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true, PlaceholderText = L.T("add.password") };
    private readonly CheckBox _remember = new() { Text = L.T("add.remember"), AutoSize = true, Checked = true };
    private readonly TextBox _alias = new() { Dock = DockStyle.Fill, PlaceholderText = L.T("add.nicknamePh") };
    private readonly CheckBox _useCookie = new() { Text = L.T("add.useCookie"), AutoSize = true };
    private readonly TextBox _cookie = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true, Enabled = false, PlaceholderText = "_|WARNING:-DO-NOT-SHARE-THIS..." };
    private readonly Button _ok = new() { Text = L.T("add.login"), DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(100, 32) };

    public string Username => _username.Text.Trim();
    public string Password => _password.Text;
    public bool RememberPassword => !UseCookie && _remember.Checked && Password.Length > 0;
    public string Alias => _alias.Text.Trim();
    public bool UseCookie => _useCookie.Checked;
    public string Cookie => _cookie.Text;

    public AccountDialog()
    {
        Text = L.T("add.title");
        Font = new Font("Segoe UI", 9.75F);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(540, 370);

        var info = new Label
        {
            AutoSize = false,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Height = 58,
            Text = L.T("add.info")
        };

        var cancel = new Button { Text = L.T("dlg.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(90, 32) };
        AcceptButton = _ok;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(_ok);

        var t = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14, 12, 14, 10), ColumnCount = 2 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void Row(Control? label, Control field, bool span = false)
        {
            int r = t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (span)
            {
                t.Controls.Add(field, 0, r);
                t.SetColumnSpan(field, 2);
            }
            else
            {
                if (label != null) t.Controls.Add(label, 0, r);
                t.Controls.Add(field, 1, r);
            }
        }

        Row(null, info, span: true);
        Row(Caption(L.T("add.username")), _username);
        Row(Caption(L.T("add.password")), _password);
        Row(null, _remember);
        Row(Caption(L.T("add.nickname")), _alias);
        Row(null, new Label { Height = 8 }, span: true);
        Row(null, _useCookie, span: true);
        Row(Caption(L.T("add.cookie")), _cookie);
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        t.Controls.Add(buttons, 0, t.RowCount);
        t.SetColumnSpan(buttons, 2);
        t.RowCount++;

        Controls.Add(t);

        _useCookie.CheckedChanged += (_, _) =>
        {
            bool c = _useCookie.Checked;
            _cookie.Enabled = c;
            _username.Enabled = _password.Enabled = _remember.Enabled = !c;
            _ok.Text = c ? L.T("add.add") : L.T("add.login");
        };

        FormClosing += (_, e) =>
        {
            if (DialogResult == DialogResult.OK && UseCookie && string.IsNullOrWhiteSpace(Cookie))
            {
                MessageBox.Show(this, L.T("add.pasteCookie"), L.T("add.title"));
                e.Cancel = true;
            }
        };
    }

    private static Label Caption(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left };
}
