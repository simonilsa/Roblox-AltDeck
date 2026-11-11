namespace RobloxAltManager;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        ApplicationConfiguration.Initialize();
        L.Set(AccountStore.Load().Language ?? L.DetectDefault());

        // Only one copy of the app should hold the Roblox singleton handles.
        using var single = new Mutex(true, "AltDeck_SingleInstance_7F3A", out bool firstCopy);
        if (!firstCopy)
        {
            MessageBox.Show(L.T("app.alreadyRunning"), "AltDeck", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.Message, L.T("app.error"), MessageBoxButtons.OK, MessageBoxIcon.Error);

        Application.Run(new MainForm());
    }
}
