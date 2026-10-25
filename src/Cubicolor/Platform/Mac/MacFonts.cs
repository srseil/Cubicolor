using System.Collections.Generic;
using Cubicolor.Simulation.Saves;

namespace Cubicolor.Platform.Mac;

// The fonts macOS installs for what Vollkorn has no glyphs for: Chinese, Japanese and Korean, in serif faces
// that sit beside Vollkorn. The language's own comes first, so the characters the scripts share (Han) take
// that language's forms; the others still draw the names in the language list.
internal static class MacFonts
{
    private const string Chinese = "STSongti-SC-Regular", Japanese = "HiraMinProN-W3", Korean = "AppleMyungjo";

    public static IReadOnlyList<string> Fallbacks(Language language) => language switch
    {
        Language.Japanese => [Japanese, Chinese, Korean],
        Language.Korean => [Korean, Chinese, Japanese],
        _ => [Chinese, Japanese, Korean],
    };
}
