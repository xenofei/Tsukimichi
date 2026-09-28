using Tsukimichi.Core.Model;

namespace Tsukimichi.Tests.Model;

public sealed class NameCaseTests
{
    [Theory]
    [InlineData("magitek armor", "Magitek Armor")]
    [InlineData("paladin", "Paladin")]
    [InlineData("company chocobo", "Company Chocobo")]
    [InlineData("wind-up cid", "Wind-up Cid")]
    [InlineData("the phoenix", "The Phoenix")]
    [InlineData("lord of the rings", "Lord of the Rings")]
    [InlineData("a bird in the hand", "A Bird in the Hand")]
    [InlineData("chair of an era", "Chair of an Era")]
    [InlineData("ready to run and jump for joy on time", "Ready to Run and Jump for Joy on Time")]
    [InlineData("'tis a fine hat", "'Tis a Fine Hat")]
    public void Capitalises_each_word_except_inner_small_words(string input, string expected)
    {
        Assert.Equal(expected, NameCase.Title(input));
    }

    [Theory]
    [InlineData("CHL P-0005")]
    [InlineData("Magitek Armor")]
    [InlineData("SDS Fenrir")]
    [InlineData("wind-up Cid")]
    public void Names_that_already_carry_an_uppercase_letter_are_unchanged(string input)
    {
        Assert.Same(input, NameCase.Title(input));
    }

    [Fact]
    public void Empty_and_null_come_back_empty()
    {
        Assert.Equal(string.Empty, NameCase.Title(string.Empty));
        Assert.Equal(string.Empty, NameCase.Title(null));
    }

    [Fact]
    public void Extra_spaces_are_preserved()
    {
        Assert.Equal("Double  Space ", NameCase.Title("double  space "));
        Assert.Equal(" Leading", NameCase.Title(" leading"));
    }

    [Fact]
    public void Non_ascii_letters_are_capitalised()
    {
        Assert.Equal("Élan Vital", NameCase.Title("élan vital"));
    }
}
