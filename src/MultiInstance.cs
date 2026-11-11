using System.Diagnostics;

namespace RobloxAltManager;

internal enum MultiState { On, RobloxRunning, Failed }

/// <summary>
/// Roblox allows one client at a time through a named mutex (older clients) and a named
/// event (newer clients). If this app creates and holds both BEFORE any Roblox client starts,
/// the clients can't claim them and stop closing each other, so every account stays open.
/// Nothing inside Roblox's own process is touched.
/// </summary>
internal static class MultiInstance
{
    private const string MutexName = "ROBLOX_singletonMutex";
    private const string EventName = "ROBLOX_singletonEvent";

    // Held in static fields so the handles stay open for as long as the app runs.
    private static Mutex? _mutex;
    private static bool _ownsMutex;
    private static EventWaitHandle? _event;

    public static bool IsRobloxRunning()
    {
        var procs = Process.GetProcessesByName("RobloxPlayerBeta");
        foreach (var p in procs) p.Dispose();
        return procs.Length > 0;
    }

    /// <summary>Tries to take the singleton lock. Safe to call again later (e.g. after Roblox closes).</summary>
    public static MultiState Enable(out string? error)
    {
        error = null;
        Release();
        try
        {
            _mutex = new Mutex(true, MutexName, out bool createdMutex);
            _ownsMutex = createdMutex;
            _event = new EventWaitHandle(false, EventResetMode.ManualReset, EventName, out bool createdEvent);

            if (createdMutex && createdEvent) return MultiState.On;

            // Don't keep Roblox's objects alive with our own handles, or a retry could never succeed.
            Release();
            return IsRobloxRunning() ? MultiState.RobloxRunning : MultiState.Failed;
        }
        catch (Exception ex)
        {
            Release();
            error = ex.Message;
            return MultiState.Failed;
        }
    }

    private static void Release()
    {
        if (_mutex != null)
        {
            if (_ownsMutex)
            {
                try { _mutex.ReleaseMutex(); } catch { /* not owned by this thread */ }
            }
            _mutex.Dispose();
            _mutex = null;
            _ownsMutex = false;
        }
        _event?.Dispose();
        _event = null;
    }
}
