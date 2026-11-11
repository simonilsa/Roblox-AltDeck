using System.Net;
using System.Text;
using System.Text.Json;

namespace RobloxAltManager;

public static class RobloxApi
{
    // UseCookies = false so each request carries exactly the account cookie we give it.
    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.All
    })
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    // ---------- Low-level helpers ----------

    /// <summary>
    /// Sends a request. Roblox answers state-changing requests with 403 + a CSRF token first;
    /// in that case the request is repeated once with the token.
    /// </summary>
    private static async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? cookie, object? body = null)
    {
        // Logins never leave this PC except over HTTPS to Roblox's own servers.
        var uri = new Uri(url);
        if (uri.Scheme != Uri.UriSchemeHttps ||
            !(uri.Host == "roblox.com" || uri.Host.EndsWith(".roblox.com", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Blocked a request to {uri.Host}: the app only talks to roblox.com over HTTPS.");

        string? csrf = null;
        bool retriedRateLimit = false;
        for (int attempt = 0; ; attempt++)
        {
            await ThrottleAsync();

            var req = new HttpRequestMessage(method, url);
            if (!string.IsNullOrEmpty(cookie)) req.Headers.Add("Cookie", ".ROBLOSECURITY=" + cookie);
            req.Headers.Referrer = new Uri("https://www.roblox.com/");
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AltDeck/2.0");

            if (method == HttpMethod.Post)
            {
                req.Headers.Add("Origin", "https://www.roblox.com");
                req.Content = new StringContent(body == null ? "" : JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            }
            if (csrf != null) req.Headers.Add("x-csrf-token", csrf);

            var res = await Http.SendAsync(req);
            if (csrf == null && res.StatusCode == HttpStatusCode.Forbidden &&
                res.Headers.TryGetValues("x-csrf-token", out var token))
            {
                csrf = token.First();
                res.Dispose();
                continue;
            }

            // Rate limited: wait as long as Roblox asks (capped at 30 s), then try once more.
            if ((int)res.StatusCode == 429 && !retriedRateLimit)
            {
                retriedRateLimit = true;
                var wait = res.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                if (wait > TimeSpan.FromSeconds(30)) wait = TimeSpan.FromSeconds(30);
                res.Dispose();
                await Task.Delay(wait);
                continue;
            }
            return res;
        }
    }

    // At most ~4 requests per second from the whole app, so it never hammers Roblox's servers.
    private static readonly SemaphoreSlim ThrottleLock = new(1, 1);
    private static DateTime _nextRequestAt = DateTime.MinValue;
    private static readonly TimeSpan MinGap = TimeSpan.FromMilliseconds(250);

    private static async Task ThrottleAsync()
    {
        await ThrottleLock.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;
            if (_nextRequestAt > now) await Task.Delay(_nextRequestAt - now);
            _nextRequestAt = DateTime.UtcNow + MinGap;
        }
        finally
        {
            ThrottleLock.Release();
        }
    }

    private static async Task<JsonDocument> JsonAsync(HttpMethod method, string url, string? cookie, object? body = null)
    {
        using var res = await SendAsync(method, url, cookie, body);
        if (res.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException(L.T("api.expired"));
        if ((int)res.StatusCode == 429)
            throw new InvalidOperationException(L.T("api.rateLimited"));
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException(L.F("api.http", (int)res.StatusCode, new Uri(url).Host));
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync());
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static RobloxUser ParseUser(JsonElement u)
    {
        string name = Str(u, "name");
        string display = Str(u, "displayName");
        return new RobloxUser(u.GetProperty("id").GetInt64(), name, display.Length > 0 ? display : name);
    }

    // ---------- Accounts & users ----------

    /// <summary>Returns the account a login cookie belongs to.</summary>
    public static async Task<RobloxUser> GetUserAsync(string cookie)
    {
        using var doc = await JsonAsync(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated", cookie);
        return ParseUser(doc.RootElement);
    }

    public static async Task<RobloxUser?> GetUserByUsernameAsync(string username)
    {
        using var doc = await JsonAsync(HttpMethod.Post, "https://users.roblox.com/v1/usernames/users", null,
            new { usernames = new[] { username }, excludeBannedUsers = true });
        var data = doc.RootElement.GetProperty("data");
        return data.GetArrayLength() > 0 ? ParseUser(data[0]) : null;
    }

    public static async Task<List<RobloxUser>> GetUsersAsync(IEnumerable<long> ids)
    {
        var result = new List<RobloxUser>();
        foreach (var chunk in ids.Distinct().Chunk(100))
        {
            using var doc = await JsonAsync(HttpMethod.Post, "https://users.roblox.com/v1/users", null,
                new { userIds = chunk, excludeBannedUsers = false });
            foreach (var u in doc.RootElement.GetProperty("data").EnumerateArray())
                result.Add(ParseUser(u));
        }
        return result;
    }

    // ---------- Friends & presence ----------

    public static async Task<List<RobloxUser>> GetFriendsAsync(long userId, string cookie)
    {
        var ids = new List<long>();
        var known = new Dictionary<long, RobloxUser>();

        try
        {
            using var doc = await JsonAsync(HttpMethod.Get, $"https://friends.roblox.com/v1/users/{userId}/friends", cookie);
            foreach (var f in doc.RootElement.GetProperty("data").EnumerateArray())
            {
                long id = f.GetProperty("id").GetInt64();
                ids.Add(id);
                if (Str(f, "name").Length > 0) known[id] = ParseUser(f);
            }
        }
        catch (HttpRequestException) { /* fall back to the paged endpoint below */ }

        if (ids.Count == 0)
        {
            string? cursor = null;
            do
            {
                string url = $"https://friends.roblox.com/v1/users/{userId}/friends/find?limit=50" +
                             (cursor != null ? "&cursor=" + Uri.EscapeDataString(cursor) : "");
                using var doc = await JsonAsync(HttpMethod.Get, url, cookie);
                var root = doc.RootElement;
                if (root.TryGetProperty("PageItems", out var items) || root.TryGetProperty("pageItems", out items))
                    foreach (var f in items.EnumerateArray()) ids.Add(f.GetProperty("id").GetInt64());

                cursor = Str(root, "NextCursor");
                if (cursor.Length == 0) cursor = Str(root, "nextCursor");
                if (cursor.Length == 0) cursor = null;
            } while (cursor != null && ids.Count < 1000);
        }

        var missing = ids.Where(id => !known.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            foreach (var u in await GetUsersAsync(missing)) known[u.Id] = u;

        return ids.Distinct().Where(known.ContainsKey).Select(id => known[id]).ToList();
    }

    public static async Task<Dictionary<long, Presence>> GetPresenceAsync(IEnumerable<long> ids, string cookie)
    {
        var map = new Dictionary<long, Presence>();
        foreach (var chunk in ids.Distinct().Chunk(50))
        {
            using var doc = await JsonAsync(HttpMethod.Post, "https://presence.roblox.com/v1/presence/users", cookie,
                new { userIds = chunk });
            foreach (var p in doc.RootElement.GetProperty("userPresences").EnumerateArray())
            {
                long id = p.GetProperty("userId").GetInt64();
                int type = p.TryGetProperty("userPresenceType", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : 0;
                long? place = p.TryGetProperty("placeId", out var pl) && pl.ValueKind == JsonValueKind.Number ? pl.GetInt64() : null;
                string gameId = Str(p, "gameId");
                map[id] = new Presence(type, Str(p, "lastLocation"), place, gameId.Length > 0 ? gameId : null);
            }
        }
        return map;
    }

    // ---------- Games ----------

    public static async Task<string?> GetGameNameAsync(long placeId)
    {
        long universeId;
        using (var doc = await JsonAsync(HttpMethod.Get, $"https://apis.roblox.com/universes/v1/places/{placeId}/universe", null))
        {
            if (!doc.RootElement.TryGetProperty("universeId", out var u) || u.ValueKind != JsonValueKind.Number) return null;
            universeId = u.GetInt64();
        }

        using var games = await JsonAsync(HttpMethod.Get, $"https://games.roblox.com/v1/games?universeIds={universeId}", null);
        var data = games.RootElement.GetProperty("data");
        return data.GetArrayLength() > 0 ? Str(data[0], "name") : null;
    }

    /// <summary>Turns a roblox.com/share?code=…&amp;type=Server link into a private server place + link code.</summary>
    public static async Task<(long PlaceId, string LinkCode)> ResolveServerShareLinkAsync(string code, string cookie)
    {
        using var doc = await JsonAsync(HttpMethod.Post, "https://apis.roblox.com/sharelinks/v1/resolve-link", cookie,
            new { linkId = code, linkType = "Server" });
        if (doc.RootElement.TryGetProperty("privateServerInviteData", out var d) && d.ValueKind == JsonValueKind.Object &&
            d.TryGetProperty("placeId", out var p) && p.ValueKind == JsonValueKind.Number)
            return (p.GetInt64(), Str(d, "linkCode"));

        throw new InvalidOperationException(L.T("api.shareInvalid"));
    }

    // ---------- Launching ----------

    /// <summary>One-time login ticket, the same thing the website requests when you press Play.</summary>
    public static async Task<string> GetAuthTicketAsync(string cookie)
    {
        if (string.IsNullOrEmpty(cookie))
            throw new InvalidOperationException(L.T("api.noLogin"));

        using var res = await SendAsync(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket", cookie);
        if (res.Headers.TryGetValues("rbx-authentication-ticket", out var ticket))
            return ticket.First();
        if (res.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException(L.T("api.expired"));
        throw new HttpRequestException(L.F("api.ticketFailed", (int)res.StatusCode));
    }

    /// <summary>Builds the roblox-player: link the browser normally opens.</summary>
    public static string BuildLaunchUri(string ticket, LaunchTarget target)
    {
        long tracker = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        const string baseUrl = "https://www.roblox.com/Game/PlaceLauncher.ashx?";

        string launcher = target.Kind switch
        {
            TargetKind.Server =>
                $"{baseUrl}request=RequestGameJob&browserTrackerId={tracker}&placeId={target.PlaceId}&gameId={Uri.EscapeDataString(target.JobId ?? "")}&isPlayTogetherGame=false",
            TargetKind.PrivateServer =>
                $"{baseUrl}request=RequestPrivateGame&browserTrackerId={tracker}&placeId={target.PlaceId}&linkCode={Uri.EscapeDataString(target.LinkCode ?? "")}",
            TargetKind.Follow =>
                $"{baseUrl}request=RequestFollowUser&browserTrackerId={tracker}&userId={target.UserId}&isPlayTogetherGame=false",
            _ =>
                $"{baseUrl}request=RequestGame&browserTrackerId={tracker}&placeId={target.PlaceId}&isPlayTogetherGame=false",
        };

        return "roblox-player:1+launchmode:play" +
               $"+gameinfo:{ticket}" +
               $"+launchtime:{now}" +
               $"+placelauncherurl:{Uri.EscapeDataString(launcher)}" +
               $"+browsertrackerid:{tracker}" +
               "+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";
    }
}
