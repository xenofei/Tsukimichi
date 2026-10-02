using Tsukimichi.Core.Text;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Tests.Text;

public class MessageSplitterTests
{
    [Fact]
    public void Text_that_fits_is_one_part_and_empty_text_none()
    {
        Assert.Equal(["# Title\n- one"], MessageSplitter.Split("# Title\r\n- one\r\n\r\n"));
        Assert.Empty(MessageSplitter.Split(string.Empty));
        Assert.Empty(MessageSplitter.Split("\n\n"));
    }

    [Fact]
    public void Long_text_splits_at_line_breaks_and_every_part_fits()
    {
        var lines = Enumerable.Range(1, 400).Select(i => $"- Reward number {i} — Quest number {i}").ToList();
        var text = "# Missing\n\n## Mounts (400)\n" + string.Join("\n", lines) + "\n\n_footer_";

        var parts = MessageSplitter.Split(text);

        Assert.True(parts.Count > 1);
        Assert.All(parts, p => Assert.True(p.Length <= MessageSplitter.DiscordLimit, $"part of {p.Length} characters"));

        // Nothing is lost or cut: every line arrives whole, in order.
        var body = parts.SelectMany(p => p.Split('\n')).Where(l => l.StartsWith("- ", StringComparison.Ordinal)).ToList();
        Assert.Equal(lines, body);
        Assert.EndsWith("_footer_", parts[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void A_part_that_continues_a_section_repeats_its_heading_and_no_part_ends_on_a_heading()
    {
        var text = "# Title\n\n## First (3)\n- aaaaaaaaaa\n- bbbbbbbbbb\n- cccccccccc\n\n## Second (2)\n- dddddddddd\n- eeeeeeeeee";

        var parts = MessageSplitter.Split(text, 40);

        Assert.All(parts, p => Assert.True(p.Length <= 40, p));
        Assert.All(parts, p => Assert.False(p.Split('\n')[^1].StartsWith('#'), p));
        Assert.Contains(parts, p => p.StartsWith("## First (3)\n- bbbbbbbbbb", StringComparison.Ordinal) || p.StartsWith("## First (3)\n- cccccccccc", StringComparison.Ordinal));
        Assert.Contains(parts, p => p.StartsWith("## Second (2)\n- dddddddddd", StringComparison.Ordinal));
        Assert.Contains(parts, p => p.Contains("- eeeeeeeeee", StringComparison.Ordinal));
    }

    [Fact]
    public void A_line_longer_than_the_limit_is_cut_at_a_space()
    {
        var word = new string('x', 9);
        var line = string.Join(' ', Enumerable.Repeat(word, 12));

        var parts = MessageSplitter.Split(line, 32);

        Assert.All(parts, p => Assert.True(p.Length <= 32, p));
        Assert.Equal(line.Replace(" ", string.Empty, StringComparison.Ordinal), string.Concat(parts).Replace(" ", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void Copy_missing_is_plain_bullets_under_section_headings_with_names_escaped()
    {
        var text = MoonlitMarkdown.Write(
        [
            new MoonlitMissingLine("Mounts", "Magitek Armor", "The Steps of Faith", string.Empty),
            new MoonlitMissingLine("Minions", "Wind-up *Moogle*", "Main scenario quest (Lv 83)", "Gone for good"),
            new MoonlitMissingLine("Mounts", "Company Chocobo", "My Little Chocobo", "Event running (ends Oct 5)"),
        ]);

        Assert.StartsWith("# Moonlit: missing rewards (3)\n", text, StringComparison.Ordinal);
        Assert.Contains("## Mounts (2)\n- Magitek Armor — The Steps of Faith\n- Company Chocobo — My Little Chocobo · Event running (ends Oct 5)\n", text, StringComparison.Ordinal);
        Assert.Contains("## Minions (1)\n- Wind-up \\*Moogle\\* — Main scenario quest (Lv 83) · Gone for good\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[ ]", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("## Mounts", StringComparison.Ordinal) < text.IndexOf("## Minions", StringComparison.Ordinal));
    }
}
