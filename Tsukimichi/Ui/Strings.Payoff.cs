using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for "Before you continue" payoff gates (P5): the line under the MSQ line on the Characters dashboard and
/// in the Tonight card, its spoiler disclosure, the chat line and its setting. Constant names carry the <c>Payoff</c>
/// prefix so this part of the partial class never collides with the others. Nothing here names a payoff: the gate's
/// own instruction (curated, instruction only) is the only content, and its reason stays behind the closed "why?".
/// </summary>
static partial class Strings
{
    /// <summary>Prefix of the line; the curated instruction follows ("Before you continue: Finish the Eden raid series first.").</summary>
    /// <summary>{0} = the curated instruction ("Finish the Eden raid series first.").</summary>
    public static string PayoffFormat => Loc.Get("PayoffFormat");

    /// <summary>The closed disclosure; the reason shows only after a click, and stays open for that character until closed.</summary>
    public static string PayoffWhyClosed => Loc.Get("PayoffWhyClosed");

    public static string PayoffWhyOpen => Loc.Get("PayoffWhyOpen");

    public static string PayoffWhyTooltip => Loc.Get("PayoffWhyTooltip");

    /// <summary>{0} = content quests left to do, {1} = content quests in all.</summary>
    public static string PayoffProgressFormat => Loc.Get("PayoffProgressFormat");

    // ---- Chat ----
    /// <summary>After the instruction, before the link to the first content quest left.</summary>
    /// <summary>{0} = the curated instruction, {1} = the link to the first content quest left.</summary>
    public static string PayoffChatFormat => Loc.Get("PayoffChatFormat");

    // ---- Settings › Spoilers ----
    public static string PayoffConfigShow => Loc.Get("PayoffConfigShow");
    public static string PayoffConfigShowHint => Loc.Get("PayoffConfigShowHint");

    public static string PayoffConfigNotice => Loc.Get("PayoffConfigNotice");
    public static string PayoffConfigNoticeHint => Loc.Get("PayoffConfigNoticeHint");
}
