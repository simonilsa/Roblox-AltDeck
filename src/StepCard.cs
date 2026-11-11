namespace RobloxAltManager;

/// <summary>One of the numbered "1 / 2 / 3" cards at the top of the window.</summary>
internal sealed class StepCard : Panel
{
    public enum State { Todo, Done, Problem }

    private readonly Panel _accent = new() { Dock = DockStyle.Left, Width = 6 };
    private readonly Label _title = new() { Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true, Font = new Font("Segoe UI", 9.75F) };

    public StepCard()
    {
        Height = 64;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 8, 8);

        var inner = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 8, 6) };
        inner.Controls.Add(_status); // fill first…
        inner.Controls.Add(_title);  // …then the top-docked title
        Controls.Add(inner);
        Controls.Add(_accent);
    }

    public void Set(string title, string status, State state)
    {
        _title.Text = title;
        _status.Text = state switch
        {
            State.Done => "✔  " + status,
            State.Problem => "⚠  " + status,
            _ => status
        };

        (Color back, Color accent, Color fore) = state switch
        {
            State.Done => (Color.FromArgb(231, 246, 236), Color.FromArgb(0, 150, 84), Color.FromArgb(20, 108, 46)),
            State.Problem => (Color.FromArgb(253, 234, 231), Color.FromArgb(196, 43, 28), Color.FromArgb(160, 30, 20)),
            _ => (Color.FromArgb(242, 243, 245), Color.FromArgb(176, 182, 190), Color.FromArgb(80, 86, 94)),
        };
        BackColor = back;
        _accent.BackColor = accent;
        _status.ForeColor = fore;
        _title.ForeColor = Color.FromArgb(28, 30, 34);
    }
}
