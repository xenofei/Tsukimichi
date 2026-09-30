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
    public const string PayoffPrefix = "Before you continue: ";

    /// <summary>The closed disclosure; the reason shows only after a click, and stays open for that character until closed.</summary>
    public const string PayoffWhyClosed = "why? (spoiler)";

    public const string PayoffWhyOpen = "hide why";

    public const string PayoffWhyTooltip = "Shows why this matters. It names what the story ahead does with it: a spoiler.";

    /// <summary>{0} = content quests done, {1} = content quests in all.</summary>
    public const string PayoffProgressFormat = "Optional content that changes a scene in the story ahead. {0} of {1} done. Click to select the first one left.";

    // ---- Chat ----
    /// <summary>After the instruction, before the link to the first content quest left.</summary>
    public const string PayoffChatNextPrefix = " Next: ";

    // ---- Settings › Spoilers ----
    public const string PayoffConfigShow = "Show \"Before you continue\" notes";
    public const string PayoffConfigShowHint = "When your story reaches a point where optional content changes a scene, a line under the MSQ line on the Characters tab and in the Tonight card says what to do first. Off hides the line and the chat notice.";

    public const string PayoffConfigNotice = "Chat line when optional content pays off in the story ahead";
    public const string PayoffConfigNoticeHint = "\"Before you continue: Finish the Eden raid series first.\", once per character, when the main scenario reaches the point where it matters. It never says why; the Characters tab and the Tonight card keep the line, with the reason behind \"why? (spoiler)\".";
}
