using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace RobloxAltManager;

/// <summary>
/// UI translations. Each language is a JSON file in src/Languages (key → text), embedded in the exe.
/// To add a language, copy en.json, translate the values, and rebuild – it shows up automatically.
/// Missing keys fall back to English, so a partial translation never shows blank text.
/// </summary>
public static class L
{
    public sealed record Language(string Code, string Name)
    {
        public override string ToString() => Name;
    }

    // Order in the language picker; any extra JSON files are listed after these.
    private static readonly string[] PreferredOrder = { "en", "es", "pt", "fil", "id", "fr", "de", "ru", "tr", "vi" };

    private static readonly Dictionary<string, Dictionary<string, string>> Tables = LoadAll();
    private static readonly Dictionary<string, string> English = Tables.GetValueOrDefault("en") ?? new();
    private static Dictionary<string, string> _current = English;

    public static string Code { get; private set; } = "en";

    public static IReadOnlyList<Language> Languages { get; } = Tables
        .Select(kv => new Language(kv.Key, kv.Value.GetValueOrDefault("_name") ?? kv.Key))
        .OrderBy(l => Array.IndexOf(PreferredOrder, l.Code) is var i && i >= 0 ? i : int.MaxValue)
        .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static void Set(string? code)
    {
        if (code != null && Tables.TryGetValue(code, out var table))
        {
            Code = code;
            _current = table;
        }
        else
        {
            Code = "en";
            _current = English;
        }
    }

    /// <summary>The Windows display language if we have it, otherwise English.</summary>
    public static string DetectDefault()
    {
        var ui = CultureInfo.CurrentUICulture;
        if (Tables.ContainsKey(ui.TwoLetterISOLanguageName)) return ui.TwoLetterISOLanguageName;
        if (ui.Name.StartsWith("fil", StringComparison.OrdinalIgnoreCase) || ui.Name.StartsWith("tl", StringComparison.OrdinalIgnoreCase))
            return Tables.ContainsKey("fil") ? "fil" : "en";
        return "en";
    }

    public static string T(string key) =>
        _current.TryGetValue(key, out var s) ? s :
        English.TryGetValue(key, out var e) ? e : key;

    public static string F(string key, params object?[] args)
    {
        try { return string.Format(T(key), args); }
        catch (FormatException) { return string.Format(English.GetValueOrDefault(key) ?? key, args); }
    }

    private static Dictionary<string, Dictionary<string, string>> LoadAll()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var asm = Assembly.GetExecutingAssembly();
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.StartsWith("lang.", StringComparison.Ordinal) || !name.EndsWith(".json", StringComparison.Ordinal)) continue;
            string code = name["lang.".Length..^".json".Length];
            try
            {
                using var stream = asm.GetManifestResourceStream(name);
                if (stream == null) continue;
                var table = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
                if (table != null) result[code] = table;
            }
            catch { /* a broken language file just isn't offered */ }
        }
        return result;
    }
}
