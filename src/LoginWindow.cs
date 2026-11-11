using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace RobloxAltManager;

/// <summary>
/// Opens the official Roblox login page in an embedded Edge browser, fills in the username and
/// password, and lets the user finish any captcha or 2-step check. As soon as Roblox has logged
/// the account in, the session cookie is captured and the window closes by itself.
/// </summary>
public class LoginWindow : Form
{
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 800 };
    private readonly string _username;
    private readonly string _password;
    private bool _filled;
    private bool _done;

    private string? _cookie;
    public string? Cookie => _cookie;

    private const string AutofillScript = """
        (function (user, pass) {
          function setValue(el, value) {
            var setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set;
            setter.call(el, value);
            el.dispatchEvent(new Event('input', { bubbles: true }));
            el.dispatchEvent(new Event('change', { bubbles: true }));
          }
          var tries = 0;
          var timer = setInterval(function () {
            var u = document.querySelector('#login-username');
            var p = document.querySelector('#login-password');
            if (u && p) {
              clearInterval(timer);
              setValue(u, user);
              if (pass) {
                setValue(p, pass);
                var btn = document.querySelector('#login-button');
                if (btn) setTimeout(function () { btn.click(); }, 400);
              } else {
                p.focus();
              }
            } else if (++tries > 60) {
              clearInterval(timer);
            }
          }, 250);
        })(__USER__, __PASS__);
        """;

    public LoginWindow(string username, string password)
    {
        _username = username;
        _password = password;

        Text = string.IsNullOrEmpty(username) ? L.T("login.title") : L.F("login.titleUser", username);
        Font = new Font("Segoe UI", 9F);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(520, 760);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(10, 6, 10, 6),
            BackColor = Color.FromArgb(232, 240, 254),
            ForeColor = Color.FromArgb(30, 50, 90),
            Text = L.T("login.hint")
        };

        Controls.Add(_web);
        Controls.Add(hint);

        Load += async (_, _) => await InitAsync();
        _poll.Tick += async (_, _) => await CheckForLoginAsync();
        FormClosed += (_, _) => _poll.Stop();
    }

    private async Task InitAsync()
    {
        try
        {
            string dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AltDeck", "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, dataDir);
            await _web.EnsureCoreWebView2Async(env);

            var core = _web.CoreWebView2;
            core.CookieManager.DeleteAllCookies(); // start every login fresh
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.NavigationCompleted += async (_, _) => await AutofillAsync();
            core.Navigate("https://www.roblox.com/login");
            _poll.Start();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(this,
                L.T("login.noWebView"), L.T("login.noWebViewTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Process.Start(new ProcessStartInfo("https://go.microsoft.com/fwlink/p/?LinkId=2124703") { UseShellExecute = true });
            DialogResult = DialogResult.Cancel;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, L.F("login.failed", ex.Message), L.T("login.title"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
        }
    }

    private async Task AutofillAsync()
    {
        if (_filled || _done || string.IsNullOrEmpty(_username) || _web.CoreWebView2 == null) return;
        if (!_web.Source.AbsolutePath.StartsWith("/login", StringComparison.OrdinalIgnoreCase)) return;

        _filled = true;
        string script = AutofillScript
            .Replace("__PASS__", JsonSerializer.Serialize(_password ?? ""))
            .Replace("__USER__", JsonSerializer.Serialize(_username));
        await _web.CoreWebView2.ExecuteScriptAsync(script);
    }

    private async Task CheckForLoginAsync()
    {
        if (_done || _web.CoreWebView2 == null) return;

        var cookies = await _web.CoreWebView2.CookieManager.GetCookiesAsync("https://www.roblox.com");
        var session = cookies.FirstOrDefault(c => c.Name == ".ROBLOSECURITY" && !string.IsNullOrEmpty(c.Value));
        if (session == null || _done) return;

        _done = true;
        _poll.Stop();
        _cookie = session.Value;

        // Clearing the cookie locally does NOT log the account out; it just keeps the next login clean.
        _web.CoreWebView2.CookieManager.DeleteAllCookies();
        DialogResult = DialogResult.OK;
    }
}
