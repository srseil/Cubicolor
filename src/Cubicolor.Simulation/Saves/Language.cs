using System;

namespace Cubicolor.Simulation.Saves;

// The languages the game is translated into: the ones most of Steam's players read (docs/port-plan.md §9).
public enum Language
{
    English,
    French,
    German,
    Spanish,
    ChineseSimplified,
    Russian,
    PortugueseBrazil,
    Japanese,
    Korean,
    Polish,
}

public static class Languages
{
    public static readonly int Count = Enum.GetValues<Language>().Length;

    // The BCP 47 tag, which also names the language's string table (assets/lang/<code>.txt).
    public static string Code(this Language language) => language switch
    {
        Language.English => "en",
        Language.French => "fr",
        Language.German => "de",
        Language.Spanish => "es",
        Language.ChineseSimplified => "zh-Hans",
        Language.Russian => "ru",
        Language.PortugueseBrazil => "pt-BR",
        Language.Japanese => "ja",
        Language.Korean => "ko",
        _ => "pl",
    };

    // How the language calls itself, which is how a player looks for it in a list.
    public static string NativeName(this Language language) => language switch
    {
        Language.English => "English",
        Language.French => "Français",
        Language.German => "Deutsch",
        Language.Spanish => "Español",
        Language.ChineseSimplified => "简体中文",
        Language.Russian => "Русский",
        Language.PortugueseBrazil => "Português (Brasil)",
        Language.Japanese => "日本語",
        Language.Korean => "한국어",
        _ => "Polski",
    };

    // Steam's API language names (ISteamApps::GetCurrentGameLanguage). Latin American Spanish reads the
    // Spanish table; any other language Steam may name falls back to English.
    public static Language? FromSteam(string name) => name switch
    {
        "english" => Language.English,
        "french" => Language.French,
        "german" => Language.German,
        "spanish" or "latam" => Language.Spanish,
        "schinese" => Language.ChineseSimplified,
        "russian" => Language.Russian,
        "brazilian" or "portuguese" => Language.PortugueseBrazil,
        "japanese" => Language.Japanese,
        "koreana" => Language.Korean,
        "polish" => Language.Polish,
        _ => null,
    };

    // A system locale or language tag ("de-AT", "zh-Hans-CN", "pt_PT"), by its language and, for Chinese, its
    // script: Traditional Chinese has no table, and readers of it get English rather than the other script.
    public static Language? FromLocale(string tag)
    {
        string[] parts = tag.Replace('_', '-').Split('-');
        string language = parts[0].ToLowerInvariant();
        if (language == "zh")
        {
            bool traditional = Array.Exists(parts, p => p.Equals("Hant", StringComparison.OrdinalIgnoreCase)
                                                        || p is "TW" or "HK" or "MO");
            return traditional ? null : Language.ChineseSimplified;
        }
        foreach (Language l in Enum.GetValues<Language>())
            if (l.Code().Split('-')[0] == language) return l;
        return null;
    }

    // The language to show: the player's own choice; else the one Steam runs the game in (the player's Steam
    // language, or the game's own language setting in the Steam library); else the first of the system's
    // preferred languages there is a table for; else English.
    public static Language Choose(Language? chosen, string? steam, System.Collections.Generic.IEnumerable<string> system)
    {
        if (chosen is { } language) return language;
        if (steam != null && FromSteam(steam) is { } fromSteam) return fromSteam;
        foreach (string tag in system)
            if (FromLocale(tag) is { } fromSystem) return fromSystem;
        return Language.English;
    }

    // The value in the settings file: the code, which stays the same whatever the enum is called.
    public static Language? FromCode(string code)
    {
        foreach (Language l in Enum.GetValues<Language>())
            if (l.Code().Equals(code, StringComparison.OrdinalIgnoreCase)) return l;
        return null;
    }
}
