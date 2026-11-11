using System.Text.Json;
using System.Text.Json.Serialization;

namespace RobloxAltManager;

public class Account
{
    public string Alias { get; set; } = "";
    public string Username { get; set; } = "";
    public long UserId { get; set; }
    public string EncryptedCookie { get; set; } = "";
    public string EncryptedPassword { get; set; } = "";
    public bool Selected { get; set; }
    public string LastLaunched { get; set; } = "";

    [JsonIgnore]
    public string Cookie
    {
        get => Dpapi.Unprotect(EncryptedCookie);
        set => EncryptedCookie = Dpapi.Protect(value);
    }

    [JsonIgnore]
    public string Password
    {
        get => Dpapi.Unprotect(EncryptedPassword);
        set => EncryptedPassword = Dpapi.Protect(value);
    }

    [JsonIgnore]
    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(Alias) || Alias == Username ? Username : $"{Alias} ({Username})";
}

public class AccountStore
{
    public List<Account> Accounts { get; set; } = new();
    public List<SavedGame> Games { get; set; } = new();
    public string? LastPlace { get; set; }
    public string? LastJob { get; set; }
    public string? LastFriend { get; set; }
    public bool FriendMode { get; set; }
    public int Delay { get; set; } = 5;
    public string? Language { get; set; }

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AltDeck");

    private static string FilePath => Path.Combine(Dir, "accounts.json");

    public static AccountStore Load()
    {
        if (!File.Exists(FilePath)) return new AccountStore();
        try
        {
            return JsonSerializer.Deserialize<AccountStore>(File.ReadAllText(FilePath)) ?? new AccountStore();
        }
        catch
        {
            // Keep a copy of an unreadable file instead of silently overwriting it.
            File.Copy(FilePath, FilePath + ".corrupt.bak", overwrite: true);
            return new AccountStore();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, FilePath, overwrite: true);
    }
}
