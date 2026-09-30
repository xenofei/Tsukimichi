using System.Globalization;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Tests.Localization;

/// <summary>
/// The "qps" layout check (V2-19): every English string stretched by at least 40 % and bracketed, with its
/// placeholders, printf specifiers and ImGui ids intact, so the plugin still formats and finds its windows in it.
/// </summary>
public class PseudoTextTests
{
    [Theory]
    [InlineData("Journal")]
    [InlineData("Characters")]
    [InlineData("Lv")]
    [InlineData("Search quests, rewards or ids")]
    public void Every_string_grows_by_at_least_forty_percent_and_is_bracketed(string english)
    {
        var pseudo = PseudoText.Stretch(english);
        Assert.StartsWith("[", pseudo, StringComparison.Ordinal);
        Assert.EndsWith("]", pseudo, StringComparison.Ordinal);
        Assert.True(pseudo.Length >= english.Length * 1.4, $"\"{pseudo}\" is not 40 % longer than \"{english}\"");
        Assert.NotEqual(english, pseudo);
    }

    [Fact]
    public void Placeholders_and_printf_specifiers_survive_and_still_format()
    {
        var pseudo = PseudoText.Stretch("{0:N0} quests · showing {1:N0} of {2:N0}");
        Assert.Contains("{0:N0}", pseudo, StringComparison.Ordinal);
        Assert.Contains("{2:N0}", pseudo, StringComparison.Ordinal);
        Assert.Contains("1,234", string.Format(CultureInfo.InvariantCulture, pseudo, 1234, 5, 6), StringComparison.Ordinal);

        Assert.Contains("%d", PseudoText.Stretch("%d days"), StringComparison.Ordinal);
        Assert.Contains("%.2f", PseudoText.Stretch("%.2f×"), StringComparison.Ordinal);
    }

    [Fact]
    public void Imgui_ids_stay_outside_the_brackets()
    {
        var pseudo = PseudoText.Stretch("Tsukimichi Help###TsukimichiHelp");
        Assert.EndsWith("]###TsukimichiHelp", pseudo, StringComparison.Ordinal);
        Assert.Equal("##actions", PseudoText.Stretch("##actions"));
    }

    [Fact]
    public void Combo_items_stretch_one_by_one()
    {
        var pseudo = PseudoText.Stretch("Inherit\0On\0Off\0");
        var items = pseudo.Split('\0');
        Assert.Equal(4, items.Length);
        Assert.All(items.Take(3), static item => Assert.StartsWith("[", item, StringComparison.Ordinal));
        Assert.Equal(string.Empty, items[3]);
    }

    [Fact]
    public void Machine_keys_are_left_alone()
    {
        Assert.True(PseudoText.IsMachineKey("Core.Culture"));
        Assert.True(PseudoText.IsMachineKey("Core.Seasonal.DateFormat"));
        Assert.False(PseudoText.IsMachineKey("TabJournal"));
    }

    [Fact]
    public void Every_english_resource_string_survives_the_stretch()
    {
        // The whole English file through the stretch: placeholders equal, formats still format, ids intact.
        var args = Enumerable.Range(0, 10).Select(static i => (object)(i + 1)).ToArray();
        foreach (var (key, value) in ResxFiles.Load(string.Empty))
        {
            if (PseudoText.IsMachineKey(key))
            {
                continue;
            }

            var pseudo = PseudoText.Stretch(value);
            Assert.True(ResxFiles.Placeholders(value).SequenceEqual(ResxFiles.Placeholders(pseudo)), $"{key}: \"{pseudo}\"");
            if (value.Contains('{', StringComparison.Ordinal))
            {
                _ = string.Format(CultureInfo.InvariantCulture, pseudo, args);
            }
        }
    }
}
