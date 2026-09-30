using Tsukimichi.Core.Ipc;
using Tsukimichi.Tests.Data;

namespace Tsukimichi.Tests.Ipc;

/// <summary>
/// The quests Tsukimichi never asks Questionable about are the ones its docs name: the labels in
/// <see cref="QuestionableCrossCheck.SideEffectRowIds"/> and docs/ipc.md checked against the frozen catalog.
/// </summary>
public sealed class QuestionableSideEffectFixtureTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    [Theory]
    [InlineData(69617u, "The Adventurer with All the Cards")] // Questionable 4081, Gold Saucer (achievement 2819)
    [InlineData(67923u, "What Lies Beneath")]                 // Questionable 2387, Palace of the Dead floors 51 to 100 (achievement 1580)
    public void Side_effect_quests_are_the_ones_the_docs_name(uint rowId, string name)
    {
        Assert.Contains(rowId, QuestionableCrossCheck.SideEffectRowIds);
        Assert.Equal(name, fixture.Bundle.Catalog.GetByRowId(rowId)?.Name);
    }

    [Fact]
    public void There_are_exactly_two_side_effect_quests()
    {
        Assert.Equal([67923u, 69617u], QuestionableCrossCheck.SideEffectRowIds.Order());
    }
}
