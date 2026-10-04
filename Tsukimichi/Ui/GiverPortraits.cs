using System;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Portraits;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// One plate to draw (<see cref="Chrome.Portrait"/>): the giver's portrait for a quest and whether its face may show.
/// A struct over the index's objects, so asking for one every frame allocates nothing.
/// </summary>
/// <param name="Portrait">The index's answer: the face (when there is art), its crop, family and era, and the fallback.</param>
/// <param name="FaceAllowed">The face may show: the setting is on and the spoiler shield allows it (<see cref="PortraitPlate.FaceAllowed"/>).</param>
public readonly record struct PortraitRequest(PortraitRef Portrait, bool FaceAllowed)
{
    /// <summary>A plate with the moon disc and nothing else: an unknown giver.</summary>
    public static readonly PortraitRequest None = new(default, false);

    /// <summary>Whether a face shows on a plate of <paramref name="logicalSize"/> (otherwise a fallback does).</summary>
    public bool ShowsFace(float logicalSize) => PortraitPlate.Choose(Portrait, FaceAllowed, logicalSize) == PortraitShow.Face;
}

/// <summary>
/// Giver portraits for the panes (feature plan v7 F2, F5): the portrait index the plugin warms off the frame
/// (<see cref="Game.IndexWarmer.Portraits"/>), the Giver portraits setting, and the spoiler rule, in one place. Until the
/// index lands every giver gets the moon disc, and the plates fill in when it does. Set up by the plugin at load.
/// </summary>
public static class GiverPortraits
{
    /// <summary>The warmed index; null (or returning null) while it builds.</summary>
    public static Func<PortraitIndex?>? Index { get; set; }

    /// <summary>Settings › General › Look › Giver portraits; null reads as the default, Game art.</summary>
    public static Func<GiverPortraitMode>? Mode { get; set; }

    /// <summary>"Region › Place" of a quest's giver for the hover tooltip, through a spoiler shield; null leaves the place out.</summary>
    public static Func<QuestRecord, SpoilerMask, string?>? PlaceOf { get; set; }

    /// <summary>Whether plates are drawn at all (Settings: anything but Off).</summary>
    public static bool Enabled => (Mode?.Invoke() ?? GiverPortraitMode.GameArt) != GiverPortraitMode.Off;

    /// <summary>
    /// The plate for <paramref name="quest"/>'s giver through <paramref name="spoilers"/>: never a face for a quest the
    /// shield masks, nor, while the shield is on, a face from an expansion the character's story has not reached. A
    /// masked quest's plate never shows its allied society's emblem either (<see cref="PortraitPlate.ForMaskedQuest"/>).
    /// A giver the wider shield hides (1.20.0 N6: a person the story has not introduced) is treated the same way.
    /// </summary>
    public static PortraitRequest For(QuestRecord quest, SpoilerMask spoilers)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(spoilers);
        var index = Index?.Invoke() ?? PortraitIndex.Empty;
        var portrait = index.For(quest);
        var masked = spoilers.IsMasked(quest) || (quest.Issuer is { } issuer && spoilers.IsNameMasked(SpoilerKind.Npc, issuer.Name));
        if (masked)
        {
            portrait = PortraitPlate.ForMaskedQuest(portrait);
        }

        var options = spoilers.Options;
        var allowed = Enabled && portrait.HasArt
            && PortraitPlate.FaceAllowed(portrait.Era, masked, options.HideNames || options.HideArtwork, spoilers.ReachExpansion);
        return new PortraitRequest(portrait, allowed);
    }

    // The tooltip's place line, kept for the last quest asked so a held hover composes nothing.
    private static QuestRecord? placeQuest;
    private static string? placeText;
    private static int placeLanguage = -1;
    private static int placeShield;

    /// <summary>The giver's place for the tooltip through <paramref name="spoilers"/>, composed once per quest (and language and shield).</summary>
    public static string? Place(QuestRecord quest, SpoilerMask spoilers)
    {
        ArgumentNullException.ThrowIfNull(spoilers);
        if (!ReferenceEquals(quest, placeQuest) || placeLanguage != Localization.Loc.Version || placeShield != spoilers.Fingerprint)
        {
            placeQuest = quest;
            placeLanguage = Localization.Loc.Version;
            placeShield = spoilers.Fingerprint;
            placeText = PlaceOf?.Invoke(quest, spoilers);
        }

        return placeText;
    }

    /// <summary>
    /// The giver's name as a tooltip prints it: a person the story has not introduced by the wider shield's placeholder
    /// ("Dawntrail character", 1.20.0 N6), someone met before by name even on a masked quest; without the wider shield
    /// "Hidden giver" for a quest the shield masks; empty for none.
    /// </summary>
    public static string Name(QuestRecord quest, SpoilerMask spoilers)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(spoilers);
        if (quest.Issuer is not { Name.Length: > 0 } issuer)
        {
            return string.Empty;
        }

        return spoilers.IsMasked(quest) && !spoilers.MasksNames ? Strings.GiverHidden : spoilers.Name(SpoilerKind.Npc, issuer.Name);
    }
}
