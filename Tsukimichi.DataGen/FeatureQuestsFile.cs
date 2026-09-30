using System.Text.Json;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.DataGen;

/// <summary>
/// Writes <c>curated/feature_quests.json</c>: <c>{ "questRowIds": [..], "note" }</c>, ids ascending, the shape
/// <see cref="CuratedData"/> reads. The ids come from <c>FeaturePresets.Derive</c> over the mapped catalog, the other
/// curated files and the generated entries, so the file is a product of a regeneration, never a hand edit.
/// </summary>
internal static class FeatureQuestsFile
{
    public const string Note =
        "Written by Tsukimichi.DataGen (tools/regen.ps1); do not edit by hand. Derived by FeaturePresets.Derive over the refiled catalog: quests with the blue EventIconType 8 journal icon or the quasi-quest type 10 that shares it, quests in system_unlocks.json or duty_unlocks.json, quests the unique-reward data credits with an unlock, and quests whose own rewards unlock something; never main scenario, repeatable, retired or hidden progress-tracker quests. Seeds the Unlock quests virtual category; CuratedInvariantsTests fails when this file differs from the derived set.";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static void Write(string path, IEnumerable<uint> questRowIds)
    {
        var file = new Shape(questRowIds.Distinct().OrderBy(id => id).ToArray(), Note);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(file, Options) + "\n");
        File.Move(tmp, path, overwrite: true);
    }

    private sealed record Shape(uint[] questRowIds, string note);
}
