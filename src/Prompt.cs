namespace RobloxAltManager;

/// <summary>A tiny one-line text input dialog.</summary>
public static class Prompt
{
    public static string? Show(IWin32Window owner, string title, string label, string value)
    {
        using var form = new Form
        {
            Text = title,
            Font = new Font("Segoe UI", 9.75F),
            AutoScaleDimensions = new SizeF(96F, 96F),
            AutoScaleMode = AutoScaleMode.Dpi,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(380, 124)
        };
        var lbl = new Label { Text = label, Left = 12, Top = 12, AutoSize = true };
        var box = new TextBox { Text = value, Left = 12, Top = 38, Width = 356 };
        var ok = new Button { Text = L.T("dlg.ok"), DialogResult = DialogResult.OK, Left = 196, Top = 80, Width = 82, Height = 30 };
        var cancel = new Button { Text = L.T("dlg.cancel"), DialogResult = DialogResult.Cancel, Left = 286, Top = 80, Width = 82, Height = 30 };
        form.Controls.AddRange(new Control[] { lbl, box, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return form.ShowDialog(owner) == DialogResult.OK ? box.Text.Trim() : null;
    }
}
