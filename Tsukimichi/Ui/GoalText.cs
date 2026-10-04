using System;
using System.Globalization;
using Tsukimichi.Core.Characters;
using Tsukimichi.Core.Evaluation;

namespace Tsukimichi.Ui;

/// <summary>
/// How an alt goal reads (plan v7, 1.21.0 N11): its title on the roster ("Match Michiru's unlocks"), its phrase inside a
/// sentence ("Your goal: match Michiru's unlocks"), and what is left ("13 left · 9 Ready", "5 zones left", "Goal
/// reached"). Counts say what is left, never done/total. The other character's name is its first name.
/// </summary>
internal static class GoalText
{
    /// <summary>"Story to 7.0", "Match Michiru's unlocks", "Flying in Endwalker", "All duty roulettes open".</summary>
    public static string Title(AltGoal goal, RosterSource? source, Func<byte, string>? expansion = null) => goal.Kind switch
    {
        AltGoalKind.Story => Format(Strings.GoalStoryTitleFormat, goal.Patch ?? string.Empty),
        AltGoalKind.MatchCharacter => Format(Strings.GoalMatchTitleFormat, OtherName(goal, source)),
        AltGoalKind.Flying => Format(Strings.GoalFlyingTitleFormat, ExpansionName(goal.Expansion ?? 0, expansion)),
        AltGoalKind.Roulettes => Strings.GoalRoulettesTitle,
        _ => string.Empty,
    };

    /// <summary>"story to 7.0", "match Michiru's unlocks": the title as it reads inside a sentence.</summary>
    public static string Phrase(AltGoal goal, RosterSource? source, Func<byte, string>? expansion = null) => goal.Kind switch
    {
        AltGoalKind.Story => Format(Strings.GoalStoryPhraseFormat, goal.Patch ?? string.Empty),
        AltGoalKind.MatchCharacter => Format(Strings.GoalMatchPhraseFormat, OtherName(goal, source)),
        AltGoalKind.Flying => Format(Strings.GoalFlyingPhraseFormat, ExpansionName(goal.Expansion ?? 0, expansion)),
        AltGoalKind.Roulettes => Strings.GoalRoulettesPhrase,
        _ => string.Empty,
    };

    /// <summary>"48 left", "5 zones left", "2 duties left", with " · 9 Ready" when some can be done now; "Goal reached"; a goal not read yet.</summary>
    public static string Left(AltGoalProgress? progress)
    {
        if (progress is null)
        {
            return Strings.GoalBeingRead;
        }

        if (!progress.Known)
        {
            return progress.Kind == AltGoalKind.MatchCharacter ? Strings.GoalOtherUnread : Strings.GoalUnknown;
        }

        if (progress.Reached)
        {
            return Strings.GoalReached;
        }

        var left = progress.Kind switch
        {
            AltGoalKind.Flying => progress.Left == 1 ? Strings.GoalZonesLeftOne : Format(Strings.GoalZonesLeftFormat, progress.Left),
            AltGoalKind.Roulettes => RoulettesLeft(progress),
            _ => Format(Strings.GoalLeftFormat, progress.Left),
        };
        return progress.Doable > 0 ? left + Strings.GoalSeparator + Format(Strings.GoalReadyFormat, progress.Doable) : left;
    }

    /// <summary>"2 duties left", "Lv 50 needed", or both: a roulette's level is never counted as a duty.</summary>
    public static string RoulettesLeft(AltGoalProgress progress)
    {
        var duties = progress.Left switch
        {
            0 => string.Empty,
            1 => Strings.GoalDutiesLeftOne,
            _ => Format(Strings.GoalDutiesLeftFormat, progress.Left),
        };
        if (progress.LevelNeeded <= 0)
        {
            return duties;
        }

        var level = Format(Strings.RosterLevelNeededFormat, progress.LevelNeeded);
        return duties.Length == 0 ? level : duties + Strings.GoalSeparator + level;
    }

    /// <summary>The other character's first name, or the words for one no list has.</summary>
    public static string OtherName(AltGoal goal, RosterSource? source)
    {
        if (goal.Other is { } other && source?.RowOf(other) is { } row)
        {
            return FirstName(row.Name);
        }

        return Strings.GoalAnotherCharacter;
    }

    /// <summary>A character's first name ("Michiru" of "Michiru Tsukikage").</summary>
    public static string FirstName(string name)
    {
        var space = name.IndexOf(' ', StringComparison.Ordinal);
        return space > 0 ? name[..space] : name;
    }

    private static string ExpansionName(byte id, Func<byte, string>? expansion) =>
        expansion?.Invoke(id) is { Length: > 0 } named ? named : Expansions.Name(id);

    private static string Format(string format, params object[] args) => string.Format(CultureInfo.CurrentCulture, format, args);
}
