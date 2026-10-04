using System.Globalization;
using System.Text;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Text;

/// <summary>One of the eight compass points, clockwise from north.</summary>
public enum CompassPoint : byte
{
    North,
    NorthEast,
    East,
    SouthEast,
    South,
    SouthWest,
    West,
    NorthWest,
}

/// <summary>
/// One chat line of <c>/tsuki msq</c>, <c>/tsuki next</c> or <c>/tsuki go</c>: <see cref="Before"/>, the quest's name
/// (a chat link whose text is the plain name, so a reader speaks it) and <see cref="After"/>. A line without a quest
/// has an empty <see cref="QuestName"/> and a <see cref="QuestRowId"/> of 0.
/// </summary>
public sealed record GuidanceLine(string Before, string QuestName, string After, uint QuestRowId)
{
    /// <summary>The whole line as a reader hears it.</summary>
    public string Text => Before + QuestName + After;

    /// <summary>A line with no quest link.</summary>
    public static GuidanceLine Plain(string text) => new(text, string.Empty, string.Empty, 0);
}

/// <summary>
/// Where a chat line points: the zone's name (null when the spoiler shield hides it), the map coordinates, and, only
/// when the player stands in the same zone, the distance in raw world units (about yalms) and its compass point.
/// </summary>
public sealed record GuidancePlace(string? Zone, float? MapX, float? MapY, bool SameZone = false, float? Yalms = null, CompassPoint? Direction = null);

/// <summary>
/// The plain sentences of <c>/tsuki msq</c>, <c>/tsuki next</c>, <c>/tsuki go</c> and "Say what's next in chat" (plan
/// v7, 1.21.0 P8), written for text-to-speech: full stops, numbers as digits, coordinates as "X 27, Y 34.8" (one
/// decimal, a trailing .0 dropped), a distance only in the same zone, rounded to tens of yalms with one of eight compass
/// words, and no glyphs, symbols, brackets, tallies or percentages. Text the game supplies (an objective, a name) goes
/// through <see cref="Speakable"/>, which keeps letters, digits and sentence punctuation only. Pure; the plugin prints
/// the lines to its own echo channel and makes <see cref="GuidanceLine.QuestName"/> a quest link.
/// </summary>
public static class GuidanceText
{
    /// <summary>Marks the quest name's place in a composed line; never part of any text shown.</summary>
    private const char NameMark = '\u0001';

    // ------------------------------------------------------------------ numbers and directions

    /// <summary>A map coordinate as read aloud: one decimal, a trailing .0 dropped ("27", "34.8").</summary>
    public static string Number(float value) =>
        Math.Round(value, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>"X 27, Y 34.8".</summary>
    public static string Coordinates(float x, float y) => F("Core.Guidance.Coordinates", "X {0}, Y {1}", Number(x), Number(y));

    /// <summary>
    /// The compass point from the player toward a target, in raw world units (north is towards negative z, east
    /// towards positive x, as on the game's map).
    /// </summary>
    public static CompassPoint Compass(float fromX, float fromZ, float toX, float toZ)
    {
        var dx = toX - fromX;
        var dz = toZ - fromZ;
        var degrees = Math.Atan2(dx, -dz) * 180.0 / Math.PI;
        var index = (int)Math.Round(degrees / 45.0, MidpointRounding.AwayFromZero);
        return (CompassPoint)(((index % 8) + 8) % 8);
    }

    /// <summary>The compass word: "north", "south-west".</summary>
    public static string CompassWord(CompassPoint point) => point switch
    {
        CompassPoint.North => T("Core.Guidance.North", "north"),
        CompassPoint.NorthEast => T("Core.Guidance.NorthEast", "north-east"),
        CompassPoint.East => T("Core.Guidance.East", "east"),
        CompassPoint.SouthEast => T("Core.Guidance.SouthEast", "south-east"),
        CompassPoint.South => T("Core.Guidance.South", "south"),
        CompassPoint.SouthWest => T("Core.Guidance.SouthWest", "south-west"),
        CompassPoint.West => T("Core.Guidance.West", "west"),
        _ => T("Core.Guidance.NorthWest", "north-west"),
    };

    /// <summary>A distance rounded to tens of yalms, never under 10.</summary>
    public static int RoundYalms(float yalms) =>
        Math.Max(10, (int)Math.Round(yalms / 10f, MidpointRounding.AwayFromZero) * 10);

    /// <summary>"about 60 yalms south-west of you".</summary>
    public static string Distance(float yalms, CompassPoint point) =>
        F("Core.Guidance.Distance", "about {0} yalms {1} of you", RoundYalms(yalms), CompassWord(point));

    // ------------------------------------------------------------------ speakable text

    /// <summary>
    /// <paramref name="text"/> with everything but letters, digits, spaces and sentence punctuation (. , ; : ! ? ' -)
    /// turned into spaces, and runs of spaces closed up: what a game objective or name keeps in a spoken line.
    /// </summary>
    public static string Speakable(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text)
        {
            var keep = IsSpeakableChar(c) && !char.IsWhiteSpace(c);
            if (!keep)
            {
                space = builder.Length > 0;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(c);
        }

        // A symbol between a word and its full stop leaves "word ." behind: close it up.
        return builder.ToString().Replace(" .", ".", StringComparison.Ordinal).Replace(" ,", ",", StringComparison.Ordinal);
    }

    /// <summary>Whether every character of <paramref name="text"/> reads aloud cleanly (<see cref="Speakable"/> would keep it).</summary>
    public static bool IsSpeakable(string? text)
    {
        if (text is null)
        {
            return true;
        }

        foreach (var c in text)
        {
            if (!IsSpeakableChar(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSpeakableChar(char c) =>
        char.IsLetterOrDigit(c) || c == ' ' || c is '.' or ',' or ';' or ':' or '!' or '?' or '\'' or '’' or '-';

    /// <summary>The text with a full stop at its end unless it already ends a sentence.</summary>
    public static string Sentence(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length == 0 || trimmed[^1] is '.' or '!' or '?' ? trimmed : trimmed + ".";
    }

    // ------------------------------------------------------------------ /tsuki msq

    /// <summary>
    /// "Main scenario: Dawntrail, at The Long Road to Xak Tural. 48 quests left in this expansion, and 91 to the latest
    /// story, levels 95 to 100." When everything left is in this expansion the counts say it once.
    /// </summary>
    public static GuidanceLine Msq(string expansion, string questName, uint questRowId, int leftInExpansion, int leftToLatest, int minLevel, int maxLevel)
    {
        var levels = minLevel >= maxLevel
            ? F("Core.Guidance.Level", "level {0}", maxLevel)
            : F("Core.Guidance.Levels", "levels {0} to {1}", minLevel, maxLevel);
        var counts = leftInExpansion >= leftToLatest
            ? F("Core.Guidance.MsqLeftLatest", "{0} left to the latest story, {1}.", Quests(leftToLatest), levels)
            : F("Core.Guidance.MsqLeftBoth", "{0} left in this expansion, and {1} to the latest story, {2}.", Quests(leftInExpansion), leftToLatest, levels);
        return Linked(F("Core.Guidance.MsqAt", "Main scenario: {0}, at {1}.", Speakable(expansion), NameMark) + " " + counts, questName, questRowId);
    }

    /// <summary>"Main scenario: caught up. The story continues in a later patch."</summary>
    public static GuidanceLine MsqCaughtUp() =>
        GuidanceLine.Plain(T("Core.Guidance.MsqCaughtUp", "Main scenario: caught up. The story continues in a later patch."));

    private static string Quests(int count) =>
        count == 1 ? T("Core.Guidance.OneQuest", "1 quest") : F("Core.Guidance.Quests", "{0} quests", count);

    // ------------------------------------------------------------------ /tsuki next

    /// <summary>
    /// For a quest in the journal: "Next: The Long Road to Xak Tural, step 3. Speak with Erenville. Shaaloani, X 27,
    /// Y 34.8. You are in Tuliyollal." <paramref name="objective"/> is the game's text of the current step only.
    /// </summary>
    public static GuidanceLine NextInJournal(string questName, uint questRowId, int step, string? objective, GuidancePlace? place, string? playerZone)
    {
        var text = F("Core.Guidance.NextStep", "Next: {0}, step {1}.", NameMark, step);
        return Linked(Join(text, ObjectiveSentence(objective), StepPlaceSentence(place, playerZone)), questName, questRowId);
    }

    /// <summary>
    /// For a quest to pick up: "Next: Caught in the Act. Talk to Elaisse in The Pillars, X 7.8, Y 10.8, about 60 yalms
    /// south-west of you."
    /// </summary>
    public static GuidanceLine NextReady(string questName, uint questRowId, string? giver, GuidancePlace? place, string? playerZone)
    {
        var text = F("Core.Guidance.Next", "Next: {0}.", NameMark);
        return Linked(Join(text, TalkSentence(giver, place, playerZone)), questName, questRowId);
    }

    /// <summary>"Next: nothing is Ready on this job. 4 quests are Ready on another job."</summary>
    public static GuidanceLine NextNothing(int readyOnOtherJob)
    {
        var text = T("Core.Guidance.NextNothing", "Next: nothing is Ready on this job.");
        if (readyOnOtherJob == 1)
        {
            text += " " + T("Core.Guidance.OtherJobOne", "1 quest is Ready on another job.");
        }
        else if (readyOnOtherJob > 1)
        {
            text += " " + F("Core.Guidance.OtherJobMany", "{0} quests are Ready on another job.", readyOnOtherJob);
        }

        return GuidanceLine.Plain(text);
    }

    /// <summary>
    /// A quest the spoiler shield hides, in words and never by name: "Next: a main scenario quest at level 83, in a zone
    /// ahead of your story."
    /// </summary>
    public static GuidanceLine NextMasked(bool mainScenario, int level, bool zoneHidden)
    {
        var text = mainScenario
            ? F("Core.Guidance.MaskedMsq", "Next: a main scenario quest at level {0}", level)
            : F("Core.Guidance.MaskedQuest", "Next: a quest at level {0}", level);
        return GuidanceLine.Plain(text + (zoneHidden ? T("Core.Guidance.MaskedZone", ", in a zone ahead of your story.") : "."));
    }

    /// <summary>"Step done. Next: step 4. Speak with Erenville again. X 26.7, Y 31, about 70 yalms north of you."</summary>
    public static GuidanceLine StepDone(int step, string? objective, GuidancePlace? place, string? playerZone)
    {
        var text = F("Core.Guidance.StepDone", "Step done. Next: step {0}.", step);
        return GuidanceLine.Plain(Join(text, ObjectiveSentence(objective), StepPlaceSentence(place, playerZone)));
    }

    /// <summary>"Quest done. " before the <c>/tsuki next</c> line.</summary>
    public static GuidanceLine QuestDone(GuidanceLine next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return next with { Before = T("Core.Guidance.QuestDone", "Quest done.") + " " + next.Before };
    }

    // ------------------------------------------------------------------ /tsuki go

    /// <summary>What <c>/tsuki go</c> started.</summary>
    public enum GoAction : byte
    {
        /// <summary>A teleport first, then a walk where it can.</summary>
        TeleportFirst,

        /// <summary>A teleport only.</summary>
        Teleport,

        /// <summary>A walk (or a ride or flight) in this zone.</summary>
        Walk,

        /// <summary>The map flag only (the automation level shows no travel, or travel cannot start now).</summary>
        Flag,
    }

    /// <summary>
    /// "Going to step 3 of The Long Road to Xak Tural: Erenville, Shaaloani, X 27, Y 34.8. Teleporting first." With no
    /// step, the giver: "Going to the giver of Caught in the Act: Elaisse, The Pillars, X 7.8, Y 10.8. Walking there."
    /// </summary>
    public static GuidanceLine Go(string questName, uint questRowId, int step, string? target, GuidancePlace? place, GoAction action)
    {
        var head = step > 0
            ? F("Core.Guidance.GoStep", "Going to step {0} of {1}:", step, NameMark)
            : F("Core.Guidance.GoGiver", "Going to the giver of {0}:", NameMark);
        var parts = new List<string>(4);
        if (Speakable(target) is { Length: > 0 } name)
        {
            parts.Add(name);
        }

        if (place is not null)
        {
            parts.Add(place.Zone is { } zone ? Speakable(zone) : T("Core.Guidance.ZoneAhead", "a zone ahead of your story"));
            if (place is { MapX: { } x, MapY: { } y })
            {
                parts.Add(Coordinates(x, y));
            }
        }

        var where = parts.Count > 0 ? " " + string.Join(", ", parts) + "." : string.Empty;
        var how = action switch
        {
            GoAction.TeleportFirst => T("Core.Guidance.GoTeleportFirst", "Teleporting first."),
            GoAction.Teleport => T("Core.Guidance.GoTeleport", "Teleporting."),
            GoAction.Walk => T("Core.Guidance.GoWalk", "Walking there."),
            _ => T("Core.Guidance.GoFlag", "Flagged on the map."),
        };
        return Linked(head + where + " " + how, questName, questRowId);
    }

    // ------------------------------------------------------------------ parts

    private static string ObjectiveSentence(string? objective) =>
        Speakable(objective) is { Length: > 0 } text ? Sentence(text) : string.Empty;

    /// <summary>
    /// Where a step is: in the player's zone the coordinates and the distance ("X 26.7, Y 31, about 70 yalms north of
    /// you."); elsewhere the zone first and where the player is ("Shaaloani, X 27, Y 34.8. You are in Tuliyollal.").
    /// </summary>
    private static string StepPlaceSentence(GuidancePlace? place, string? playerZone)
    {
        if (place is null)
        {
            return T("Core.Guidance.NoPlace", "This step has no place on the map.");
        }

        if (place.Zone is null)
        {
            return T("Core.Guidance.InZoneAhead", "It is in a zone ahead of your story.");
        }

        var coordinates = place is { MapX: { } x, MapY: { } y } ? Coordinates(x, y) : null;
        if (place.SameZone)
        {
            var distance = place is { Yalms: { } yalms, Direction: { } point } ? Distance(yalms, point) : null;
            var text = string.Join(", ", new[] { coordinates, distance }.Where(static s => s is not null));
            return text.Length > 0 ? Sentence(text) : string.Empty;
        }

        var where = Sentence(coordinates is null ? Speakable(place.Zone) : Speakable(place.Zone) + ", " + coordinates);
        return Join(where, YouAreIn(playerZone));
    }

    /// <summary>"Talk to Elaisse in The Pillars, X 7.8, Y 10.8, about 60 yalms south-west of you."</summary>
    private static string TalkSentence(string? giver, GuidancePlace? place, string? playerZone)
    {
        var who = Speakable(giver) is { Length: > 0 } name ? name : T("Core.Guidance.TheGiver", "the giver");
        if (place is null)
        {
            return F("Core.Guidance.TalkTo", "Talk to {0}.", who);
        }

        if (place.Zone is null)
        {
            return F("Core.Guidance.TalkToAhead", "Talk to {0} in a zone ahead of your story.", who);
        }

        var parts = new List<string>(3) { F("Core.Guidance.TalkToIn", "Talk to {0} in {1}", who, Speakable(place.Zone)) };
        if (place is { MapX: { } x, MapY: { } y })
        {
            parts.Add(Coordinates(x, y));
        }

        if (place is { SameZone: true, Yalms: { } yalms, Direction: { } point })
        {
            parts.Add(Distance(yalms, point));
        }

        var sentence = Sentence(string.Join(", ", parts));
        return place.SameZone ? sentence : Join(sentence, YouAreIn(playerZone));
    }

    private static string YouAreIn(string? playerZone) =>
        Speakable(playerZone) is { Length: > 0 } zone ? F("Core.Guidance.YouAreIn", "You are in {0}.", zone) : string.Empty;

    private static string Join(params string[] sentences) =>
        string.Join(" ", sentences.Where(static s => s.Length > 0));

    /// <summary>Splits a composed line at the name mark into the text before and after the quest's name.</summary>
    private static GuidanceLine Linked(string composed, string questName, uint questRowId)
    {
        var at = composed.IndexOf(NameMark, StringComparison.Ordinal);
        var name = Speakable(questName);
        return at < 0
            ? new GuidanceLine(composed, string.Empty, string.Empty, 0)
            : new GuidanceLine(composed[..at], name, composed[(at + 1)..], questRowId);
    }

    private static string T(string key, string english) => CoreText.T(key, english);

    private static string F(string key, string english, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, CoreText.T(key, english), args);
}
