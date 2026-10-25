using System;
using System.IO;
using System.Linq;
using Cubicolor.Game;
using Cubicolor.Platform.Mac;
using Cubicolor.Simulation.Saves;
using Xunit;

namespace Cubicolor.Tests;

public sealed class LanguageTests
{
    [Fact]
    public void The_players_choice_comes_before_steam_and_steam_before_the_system()
    {
        Assert.Equal(Language.Polish, Languages.Choose(Language.Polish, "german", ["ja-JP"]));
        Assert.Equal(Language.German, Languages.Choose(null, "german", ["ja-JP"]));
        Assert.Equal(Language.Japanese, Languages.Choose(null, null, ["ja-JP"]));
        Assert.Equal(Language.Japanese, Languages.Choose(null, "arabic", ["gsw-CH", "ja-JP"]));
        Assert.Equal(Language.English, Languages.Choose(null, null, ["sv-SE"]));
    }

    [Theory]
    [InlineData("schinese", Language.ChineseSimplified)]
    [InlineData("koreana", Language.Korean)]
    [InlineData("brazilian", Language.PortugueseBrazil)]
    [InlineData("latam", Language.Spanish)]
    public void Steams_language_names_map_to_the_tables(string steam, Language expected) =>
        Assert.Equal(expected, Languages.FromSteam(steam));

    [Theory]
    [InlineData("zh-Hans-CN", Language.ChineseSimplified)]
    [InlineData("zh_CN", Language.ChineseSimplified)]
    [InlineData("pt-PT", Language.PortugueseBrazil)]
    [InlineData("de-AT", Language.German)]
    [InlineData("en-GB", Language.English)]
    public void System_locales_map_by_their_language(string tag, Language expected) =>
        Assert.Equal(expected, Languages.FromLocale(tag));

    [Theory]
    [InlineData("zh-Hant-TW")]
    [InlineData("zh-HK")]
    [InlineData("sv-SE")]
    public void Traditional_chinese_and_untranslated_languages_have_no_table(string tag) =>
        Assert.Null(Languages.FromLocale(tag));

    [Fact]
    public void A_chosen_language_is_saved_by_its_code_and_none_is_saved_while_it_follows_steam()
    {
        string chosen = SaveFile.FormatSettings(new Settings { Language = Language.PortugueseBrazil });

        Assert.Contains("language = pt-BR", chosen);
        Assert.Equal(Language.PortugueseBrazil, SaveFile.ParseSettings(chosen).Language);
        Assert.DoesNotContain("language", SaveFile.FormatSettings(new Settings()));
        Assert.Null(SaveFile.ParseSettings("language = tlh").Language);
    }

    // Every table translates the same texts, and each is one the game shows: English in the code, or a difficulty.
    [Fact]
    public void Every_table_translates_every_text_the_game_shows()
    {
        string directory = Repo.Assets("lang");
        string source = string.Concat(Directory.GetFiles(Path.Combine(Repo.Root, "src", "Cubicolor", "Game"), "*.cs").Select(File.ReadAllText));
        var tables = Enum.GetValues<Language>().Where(l => l != Language.English).Select(l => Strings.Load(directory, l)).ToList();
        string[] keys = [.. tables[0].Keys.Order()];

        Assert.NotEmpty(keys);
        foreach (Strings table in tables)
        {
            Assert.NotSame(Strings.English, table);
            Assert.Equal(keys, table.Keys.Order());
            Assert.All(keys, key => Assert.False(string.IsNullOrWhiteSpace(table[key])));
            Assert.All(keys.Where(k => k.Contains("{0}")), key => Assert.Contains("{0}", table[key]));
        }
        // The difficulties show by their names in the code.
        string[] difficulties = Enum.GetNames<Cubicolor.Simulation.Levels.Difficulty>();
        Assert.All(keys.Except(difficulties), key => Assert.Contains($"\"{key}\"", source));
        Assert.Subset(keys.ToHashSet(), difficulties.ToHashSet());
    }

    // The trimmed CJK fonts (tools/subset-fonts.sh) must keep up with the tables: every character a language
    // shows, its name in the language list included, has a glyph in Vollkorn or in one of its fallbacks.
    [Fact]
    public void The_shipped_fonts_draw_every_character_of_every_language()
    {
        string fonts = Repo.Assets("fonts");
        using var vollkorn = new CoreTextFont(Path.Combine(fonts, "Vollkorn-Regular.ttf"), 24);
        foreach (Language language in Enum.GetValues<Language>())
        {
            var fallbacks = GameContext.FallbackFonts(language).Select(f => new CoreTextFont(Path.Combine(fonts, f), 24)).ToList();
            Strings table = Strings.Load(Repo.Assets("lang"), language);
            string text = string.Concat(table.Keys.Select(k => table[k])) + language.NativeName();
            char[] missing = [.. text.Where(c => !char.IsWhiteSpace(c) && vollkorn.GlyphFor(c) == 0 && fallbacks.All(f => f.GlyphFor(c) == 0)).Distinct()];
            foreach (CoreTextFont face in fallbacks) face.Dispose();
            Assert.True(missing.Length == 0, $"{language} has no glyph for: {new string(missing)}");
        }
    }
}
