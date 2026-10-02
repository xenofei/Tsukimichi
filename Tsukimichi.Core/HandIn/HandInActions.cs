using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.HandIn;

/// <summary>Whether a hand-off button can run, and if not, why (decision 1: a button that needs a missing plugin stays visible, disabled, naming it).</summary>
public enum HandOffState
{
    /// <summary>The plugin is loaded and there is something to hand it.</summary>
    Ready,

    /// <summary>The plugin it hands to is not loaded.</summary>
    PluginMissing,

    /// <summary>The plugin is loaded but still working on something.</summary>
    Busy,

    /// <summary>Nothing to hand over: no recipe makes the item, no node or fishing hole yields it.</summary>
    NotApplicable,

    /// <summary>The character already holds enough of the item: nothing to craft.</summary>
    Enough,
}

/// <summary>Which gathering plugin a gather hand-off goes through.</summary>
public enum GatherPlugin
{
    None,
    GatherBuddy,
    GatherBuddyReborn,
}

/// <summary>
/// The rules behind the detail pane's hand-off buttons (feature plan v5 decision 1; research C7 A and B): "Craft with
/// Artisan" calls Artisan's <c>Artisan.CraftItem(ushort recipeId, int amount)</c>; "Gather with GatherBuddy" runs
/// GatherBuddy's own chat command (<c>/gather</c>, <c>/gatherfish</c> for fish), which both GatherBuddy and GatherBuddy
/// Reborn register. Pure, so the plugin's buttons and the tests read the same answers.
/// </summary>
public static class HandInActions
{
    /// <summary>ClassJob row of the first crafter (Carpenter); CraftType n is ClassJob row 8 + n.</summary>
    public const byte FirstCrafterJob = 8;

    /// <summary>ClassJob row of the last crafter (Culinarian).</summary>
    public const byte LastCrafterJob = 15;

    public const string GatherCommand = "/gather";
    public const string GatherFishCommand = "/gatherfish";

    /// <summary>
    /// The Craft button: no recipe first (nothing any plugin could do), then enough held (<paramref name="enough"/>:
    /// the row's count covers the quest), then Artisan missing, then Artisan busy (a list or Endurance running: a second
    /// request would be queued behind it unseen).
    /// </summary>
    public static HandOffState Craft(HandInItem item, bool artisanLoaded, bool artisanBusy, bool enough = false)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (CraftableRecipe(item) is null)
        {
            return HandOffState.NotApplicable;
        }

        return enough ? HandOffState.Enough
            : !artisanLoaded ? HandOffState.PluginMissing
            : artisanBusy ? HandOffState.Busy
            : HandOffState.Ready;
    }

    /// <summary>The Gather button: shown only for a gatherable item; either GatherBuddy registers the command.</summary>
    public static HandOffState Gather(HandInItem item, GatherPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Gather == GatherKind.None)
        {
            return HandOffState.NotApplicable;
        }

        return plugin == GatherPlugin.None ? HandOffState.PluginMissing : HandOffState.Ready;
    }

    /// <summary>GatherBuddy Reborn when it is loaded (the maintained one), else GatherBuddy, else none.</summary>
    public static GatherPlugin GatherPluginOf(bool gatherBuddyLoaded, bool rebornLoaded) =>
        rebornLoaded ? GatherPlugin.GatherBuddyReborn : gatherBuddyLoaded ? GatherPlugin.GatherBuddy : GatherPlugin.None;

    /// <summary>The chat command for the item: <c>/gatherfish name</c> for a fish, <c>/gather name</c> otherwise; null when not gatherable.</summary>
    public static string? GatherCommandFor(HandInItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Gather == GatherKind.None || item.Name.Trim().Length == 0)
        {
            return null;
        }

        return (item.Gather == GatherKind.Fish ? GatherFishCommand : GatherCommand) + " " + item.Name.Trim();
    }

    /// <summary>The first recipe Artisan can take (its gate takes a ushort recipe id); null when none.</summary>
    public static HandInRecipe? CraftableRecipe(HandInItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        foreach (var recipe in item.Recipes)
        {
            if (recipe.RecipeId is > 0 and <= ushort.MaxValue)
            {
                return recipe;
            }
        }

        return null;
    }

    /// <summary>
    /// The recipe to hand Artisan: the one for the crafter the character is on, else the one for its highest-levelled
    /// crafter that has a recipe (<paramref name="jobLevels"/>, ClassJob row to level; ties go to the earlier crafter),
    /// else the first. Null when no recipe fits Artisan's gate.
    /// </summary>
    public static HandInRecipe? ChooseRecipe(HandInItem item, byte currentJob, IReadOnlyDictionary<byte, short>? jobLevels)
    {
        ArgumentNullException.ThrowIfNull(item);
        HandInRecipe? first = null;
        HandInRecipe? best = null;
        var bestLevel = 0;
        foreach (var recipe in item.Recipes)
        {
            if (recipe.RecipeId is 0 or > ushort.MaxValue)
            {
                continue;
            }

            first ??= recipe;
            var job = CrafterJob(recipe.CraftType);
            if (job == currentJob && job != 0)
            {
                return recipe;
            }

            var level = jobLevels is not null && jobLevels.TryGetValue(job, out var l) ? l : (short)0;
            if (level > bestLevel)
            {
                best = recipe;
                bestLevel = level;
            }
        }

        return best ?? first;
    }

    /// <summary>ClassJob row of a CraftType (0 Carpenter … 7 Culinarian); 0 for a type outside the eight.</summary>
    public static byte CrafterJob(byte craftType) => craftType <= LastCrafterJob - FirstCrafterJob ? (byte)(FirstCrafterJob + craftType) : (byte)0;

    /// <summary>
    /// How many items the quest still needs: what it asks for less what counts toward it (<paramref name="usable"/>,
    /// <see cref="HandInCount.Usable"/>: HQ only for an HQ item; null reads as none), never below zero.
    /// </summary>
    public static int MissingCount(HandInItem item, int? usable)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Math.Max(0, item.Needed - Math.Max(0, usable ?? 0));
    }

    /// <summary>
    /// How many crafts to ask Artisan for (its amount counts crafts, not items): the items still missing
    /// (<see cref="MissingCount"/>) over what one craft of <paramref name="recipe"/> makes, rounded up, so a recipe that
    /// yields three is asked once for three items. Zero when nothing is missing.
    /// </summary>
    public static int CraftAmount(HandInItem item, HandInRecipe? recipe, int? usable)
    {
        var missing = MissingCount(item, usable);
        var yield = Math.Max(1, (int)(recipe?.Yield ?? 1));
        return (missing + yield - 1) / yield;
    }

    /// <summary>
    /// A Moonlit gear row's owned state from Allagan Tools' count (1.6.0, research C4 #1): owned when it counts any,
    /// otherwise unknown (an item sold, desynthesised or never tracked reads 0 too). Null while Allagan Tools is absent
    /// or not ready.
    /// </summary>
    public static bool? OwnedFromCount(uint? count) => count is > 0 ? true : null;
}
