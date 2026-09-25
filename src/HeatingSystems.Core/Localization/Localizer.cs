using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace HeatingSystems.Core.Localization;

public enum AppLanguage
{
    English = 0,
    Ukrainian = 1,
}

/// <summary>
/// Application-wide string localisation. String tables are embedded JSON files
/// (<c>strings-en.json</c>, <c>strings-uk.json</c>); English is the default and the fallback.
/// </summary>
public static class Localizer
{
    private static readonly IReadOnlyDictionary<string, string> English = Load("strings-en.json");
    private static readonly IReadOnlyDictionary<string, string> Ukrainian = Load("strings-uk.json");
    private static readonly ConcurrentDictionary<string, byte> Missing = new();
    private static volatile AppLanguage _language = AppLanguage.English;

    public static AppLanguage Language => _language;

    /// <summary>Culture used for number and date formatting of the current language.</summary>
    public static CultureInfo Culture => CultureFor(_language);

    public static event EventHandler? LanguageChanged;

    public static CultureInfo CultureFor(AppLanguage language) =>
        CultureInfo.GetCultureInfo(language == AppLanguage.Ukrainian ? "uk-UA" : "en-US");

    public static void SetLanguage(AppLanguage language)
    {
        if (_language == language) return;
        _language = language;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Returns the translation of <paramref name="key"/> in the current language.</summary>
    public static string T(string key)
    {
        var table = _language == AppLanguage.Ukrainian ? Ukrainian : English;
        if (table.TryGetValue(key, out var value)) return value;
        Missing.TryAdd(key, 0);
        return English.TryGetValue(key, out var fallback) ? fallback : $"[{key}]";
    }

    /// <summary>Formats the translation of <paramref name="key"/> with the current culture.</summary>
    public static string F(string key, params object?[] args) => string.Format(Culture, T(key), args);

    /// <summary>Chooses between an English and a Ukrainian value (bilingual database columns).</summary>
    public static string Pick(string english, string ukrainian) =>
        _language == AppLanguage.Ukrainian && !string.IsNullOrEmpty(ukrainian) ? ukrainian : english;

    /// <summary>Keys requested at runtime that are missing in a string table (for tests and diagnostics).</summary>
    public static IReadOnlyCollection<string> MissingKeys => Missing.Keys.ToList();

    public static IReadOnlyCollection<string> Keys(AppLanguage language) =>
        (language == AppLanguage.Ukrainian ? Ukrainian : English).Keys.ToList();

    private static IReadOnlyDictionary<string, string> Load(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream($"HeatingSystems.Core.Localization.{name}")
                           ?? throw new InvalidOperationException($"String table {name} is not embedded.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
               ?? throw new InvalidOperationException($"String table {name} is empty.");
    }
}
