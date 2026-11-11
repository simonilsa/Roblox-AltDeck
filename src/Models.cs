namespace RobloxAltManager;

public record RobloxUser(long Id, string Name, string DisplayName);

/// <summary>Type: 0 offline, 1 online (website/app), 2 in a game, 3 in Studio.</summary>
public record Presence(int Type, string Location, long? PlaceId, string? GameId);

public enum TargetKind { Place, Server, PrivateServer, Follow }

/// <summary>Where the launched accounts should go.</summary>
public sealed record LaunchTarget(
    TargetKind Kind,
    string Description,
    long PlaceId = 0,
    string? JobId = null,
    string? LinkCode = null,
    long UserId = 0);

/// <summary>A game remembered in the "Game" dropdown.</summary>
public class SavedGame
{
    public long PlaceId { get; set; }
    public string Name { get; set; } = "";
    public override string ToString() => $"{Name}  ({PlaceId})";
}

/// <summary>An entry in the friends dropdown.</summary>
public sealed class FriendItem
{
    public FriendItem(RobloxUser user, Presence? presence)
    {
        User = user;
        Presence = presence;
    }

    public RobloxUser User { get; }
    public Presence? Presence { get; }
    public bool InGame => Presence?.Type == 2;
    public int SortOrder => Presence?.Type switch { 2 => 0, 1 => 1, 3 => 2, _ => 3 };

    public override string ToString()
    {
        string status = Presence?.Type switch
        {
            2 => string.IsNullOrWhiteSpace(Presence!.Location) ? L.T("friend.inGame") : L.F("friend.playing", Presence.Location),
            1 => L.T("friend.online"),
            3 => L.T("friend.studio"),
            _ => L.T("friend.offline")
        };
        string name = string.IsNullOrEmpty(User.DisplayName) || User.DisplayName == User.Name
            ? User.Name
            : $"{User.DisplayName} (@{User.Name})";
        return $"{name}  –  {status}";
    }
}
