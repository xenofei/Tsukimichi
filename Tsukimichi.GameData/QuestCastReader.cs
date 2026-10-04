using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;

namespace Tsukimichi.GameData;

/// <summary>
/// Reads who each quest features (feature plan v7 N10) and builds the <see cref="StoryCast"/>. A quest's people are the
/// ENpcResident rows its script names: <c>Quest.QuestParams</c> constants <c>ACTORn</c> and <c>LOC_*</c> (the script's
/// actors and its cutscene cast, "LOC_TATA = 1035457") whose value is an ENpcResident row, then the rows of
/// <c>Quest.QuestListenerParams</c> (who the steps talk to). Names are keyed in English, whatever the client's
/// language, and printed in <paramref name="language"/>. Standalone (takes an <see cref="ExcelModule"/>) so tests and
/// tools run it without Dalamud. About 0.2 s over the whole Quest sheet.
/// </summary>
public static class QuestCastReader
{
    /// <summary>Script constants naming an actor of the quest.</summary>
    public const string ActorPrefix = "ACTOR";

    /// <summary>Script constants of the quest's cutscene cast and other placed people (and other things: the row range tells).</summary>
    public const string LocalPrefix = "LOC_";

    /// <summary>ENpcResident rows sit in [1000000, 2000000): a constant whose value is outside names something else.</summary>
    private const uint FirstResident = 1_000_000;
    private const uint EndResident = 2_000_000;

    /// <summary>The recurring story characters over <paramref name="catalog"/>'s quests.</summary>
    public static StoryCast Build(ExcelModule excel, Language language, QuestCatalog catalog, StoryCastCuration? curation = null)
    {
        ArgumentNullException.ThrowIfNull(excel);
        ArgumentNullException.ThrowIfNull(catalog);
        return StoryCast.Build(catalog, Read(excel, language), curation);
    }

    /// <summary>Each quest's named people, in script order (actors and placed people, then listeners), keyed by Quest row.</summary>
    public static IReadOnlyDictionary<uint, IReadOnlyList<CastName>> Read(ExcelModule excel, Language language)
    {
        ArgumentNullException.ThrowIfNull(excel);
        var english = excel.GetSheet<ENpcResident>(Language.English);
        var local = language == Language.English ? english : excel.GetSheet<ENpcResident>(language);
        var names = new Dictionary<uint, CastName?>();
        CastName? NameOf(uint id)
        {
            if (names.TryGetValue(id, out var known))
            {
                return known;
            }

            CastName? name = null;
            if (english.GetRowOrDefault(id) is { } row && row.Singular.ExtractText() is { Length: > 0 } key)
            {
                var display = ReferenceEquals(local, english) ? key : local.GetRowOrDefault(id)?.Singular.ExtractText() ?? key;
                name = new CastName(id, key, display.Length > 0 ? display : key);
            }

            names[id] = name;
            return name;
        }

        var result = new Dictionary<uint, IReadOnlyList<CastName>>();
        var ids = new List<uint>();
        foreach (var quest in excel.GetSheet<Quest>(Language.English))
        {
            ids.Clear();
            foreach (var param in quest.QuestParams)
            {
                if (param.ScriptArg is < FirstResident or >= EndResident)
                {
                    continue;
                }

                var constant = param.ScriptInstruction.ExtractText();
                if (constant.StartsWith(ActorPrefix, StringComparison.Ordinal) || constant.StartsWith(LocalPrefix, StringComparison.Ordinal))
                {
                    ids.Add(param.ScriptArg);
                }
            }

            foreach (var listener in quest.QuestListenerParams)
            {
                if (listener.Listener is >= FirstResident and < EndResident)
                {
                    ids.Add(listener.Listener);
                }
            }

            List<CastName>? cast = null;
            foreach (var id in ids)
            {
                if (NameOf(id) is { } name)
                {
                    (cast ??= []).Add(name);
                }
            }

            if (cast is not null)
            {
                result[quest.RowId] = cast;
            }
        }

        return result;
    }
}
