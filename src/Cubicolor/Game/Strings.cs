using System.Collections.Generic;
using System.IO;
using Cubicolor.Simulation.Saves;
using Cubicolor.Utilities;

namespace Cubicolor.Game;

// The game's text in one language. The English text is the key: each language's table
// (assets/lang/<code>.txt, `English = translation` lines) maps it to the translation, and text without one
// shows as it is. English needs no table, and the UI's widgets keep the English text as their id, so focus
// and scripts are the same in every language.
public sealed class Strings
{
    private readonly Dictionary<string, string> _table;

    public Language Language { get; }

    private Strings(Language language, Dictionary<string, string> table) => (Language, _table) = (language, table);

    public static readonly Strings English = new(Language.English, []);

    public static Strings Load(string directory, Language language)
    {
        if (language == Language.English) return English;
        string path = Path.Combine(directory, language.Code() + ".txt");
        try
        {
            return new Strings(language, SaveFile.Parse(File.ReadAllText(path)));
        }
        catch (IOException e)
        {
            Log.Warn($"No {language} text: {e.Message}");
            return English;
        }
    }

    public string this[string english] => _table.TryGetValue(english, out string? text) ? text : english;

    // Every English text the table translates.
    public IEnumerable<string> Keys => _table.Keys;
}
