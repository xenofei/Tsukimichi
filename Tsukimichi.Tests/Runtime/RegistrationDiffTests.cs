using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Runtime;

public sealed class RegistrationDiffTests
{
    private static bool SameText(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);

    [Fact]
    public void Lists_only_the_positions_whose_entry_changed()
    {
        // One MSQ completion unmasks one quest: its own entry and a reward entry naming it change, nothing else.
        string[] current = ["Main scenario quest (Lv 83)", "Hunt", "Reward · Main scenario quest (Lv 83)", "Other"];
        string[] next = ["The Gift of Mercy", "Hunt", "Reward · The Gift of Mercy", "Other"];

        var changed = RegistrationDiff.ChangedIndices(current, next, SameText);

        Assert.Equal([0, 2], changed);
    }

    [Fact]
    public void Identical_lists_change_nothing()
    {
        string[] entries = ["a", "b", "c"];

        var changed = RegistrationDiff.ChangedIndices(entries, [.. entries], SameText);

        Assert.NotNull(changed);
        Assert.Empty(changed);
    }

    [Fact]
    public void A_length_change_asks_for_a_full_rebuild()
    {
        Assert.Null(RegistrationDiff.ChangedIndices(["a", "b"], ["a", "b", "c"], SameText));
        Assert.Null(RegistrationDiff.ChangedIndices(["a", "b"], ["a"], SameText));
    }
}
