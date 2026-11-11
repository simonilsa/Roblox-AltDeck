using System.Drawing.Drawing2D;
using System.Reflection;

namespace RobloxAltManager;

/// <summary>
/// Small flag pictures for the language menu (Windows can't show flag emoji, so they're
/// PNGs in src/Flags, embedded as "flag.&lt;code&gt;.png"). A language without a flag gets the globe.
/// </summary>
internal static class Flags
{
    private static readonly Dictionary<string, Image> Cache = Load();

    public static Image? Globe => Cache.GetValueOrDefault("globe");

    public static Image? Get(string code) => Cache.GetValueOrDefault(code) ?? Globe;

    /// <summary>Draws "[flag]  Language name" for one row of a language list or menu.</summary>
    public static void DrawLanguageRow(DrawItemEventArgs e, object? item, Font fallbackFont)
    {
        e.DrawBackground();
        if (item is not L.Language lang) return;

        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int flagHeight = Math.Max(10, e.Bounds.Height - 10);
        int flagWidth = flagHeight * 3 / 2;
        var flagRect = new Rectangle(e.Bounds.Left + 6, e.Bounds.Top + (e.Bounds.Height - flagHeight) / 2, flagWidth, flagHeight);

        var flag = Get(lang.Code);
        if (flag != null)
        {
            g.DrawImage(flag, flagRect);
            using var border = new Pen(Color.FromArgb(70, 0, 0, 0));
            g.DrawRectangle(border, flagRect);
        }

        var textRect = new Rectangle(flagRect.Right + 10, e.Bounds.Top, e.Bounds.Right - flagRect.Right - 12, e.Bounds.Height);
        bool selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
        TextRenderer.DrawText(g, lang.Name, e.Font ?? fallbackFont, textRect,
            selected ? SystemColors.HighlightText : SystemColors.ControlText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        e.DrawFocusRectangle();
    }

    private static Dictionary<string, Image> Load()
    {
        var result = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        var asm = Assembly.GetExecutingAssembly();
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.StartsWith("flag.", StringComparison.Ordinal) || !name.EndsWith(".png", StringComparison.Ordinal)) continue;
            try
            {
                using var stream = asm.GetManifestResourceStream(name);
                if (stream == null) continue;
                using var temp = Image.FromStream(stream);
                result[name["flag.".Length..^".png".Length]] = new Bitmap(temp); // copy, so the stream can close
            }
            catch { /* a broken picture just isn't shown */ }
        }
        return result;
    }
}

/// <summary>Shown on the very first start: a big, obvious list of languages with flags.</summary>
internal sealed class LanguageDialog : Form
{
    private readonly ListBox _list = new()
    {
        Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 36,
        BorderStyle = BorderStyle.None, IntegralHeight = false, Font = new Font("Segoe UI", 11F)
    };
    private readonly Label _title = new()
    {
        Dock = DockStyle.Top, Height = 54, TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Segoe UI", 13F, FontStyle.Bold), Padding = new Padding(58, 0, 8, 0)
    };
    private readonly Button _ok = new() { DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(110, 36), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };

    public string SelectedCode => (_list.SelectedItem as L.Language)?.Code ?? L.Code;

    public LanguageDialog()
    {
        Text = "AltDeck";
        Font = new Font("Segoe UI", 9.75F);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(380, 36 * Math.Max(4, L.Languages.Count) + 120);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        // Globe drawn at the left of the title, so the dialog is recognisable in any language.
        _title.Paint += (_, e) =>
        {
            if (Flags.Globe is { } globe)
            {
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                int size = _title.Height - 18;
                e.Graphics.DrawImage(globe, new Rectangle(14, (_title.Height - size) / 2, size, size));
            }
        };

        _list.Items.AddRange(L.Languages.Cast<object>().ToArray());
        _list.SelectedItem = L.Languages.FirstOrDefault(l => l.Code == L.Code);
        _list.DrawItem += (_, e) =>
        {
            if (e.Index >= 0) Flags.DrawLanguageRow(e, _list.Items[e.Index], _list.Font);
        };
        _list.SelectedIndexChanged += (_, _) => { L.Set(SelectedCode); UpdateTexts(); };
        _list.DoubleClick += (_, _) => { DialogResult = DialogResult.OK; };

        var listHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 0, 10, 0) };
        listHost.Controls.Add(_list);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(10) };
        buttons.Controls.Add(_ok);
        AcceptButton = _ok;

        Controls.Add(listHost);
        Controls.Add(buttons);
        Controls.Add(_title);
        UpdateTexts();
    }

    private void UpdateTexts()
    {
        _title.Text = L.Code == "en" ? L.T("lang.choose") : $"{L.T("lang.choose")} (Language)";
        _ok.Text = L.T("dlg.ok");
    }
}
