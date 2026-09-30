using Tsukimichi.Core.Export;

namespace Tsukimichi.Tests.Export;

/// <summary><c>/tsuki export [quests|moonlit] [json|csv]</c> argument parsing.</summary>
public class ExportRequestTests
{
    [Fact]
    public void Bare_export_writes_both_in_the_settings_format()
    {
        var request = ExportRequest.Parse("", ExportFormat.Csv);

        Assert.Equal([ExportKind.Quests, ExportKind.Moonlit], request.Kinds);
        Assert.Equal(ExportFormat.Csv, request.Format);
        Assert.Null(request.Error);
    }

    [Theory]
    [InlineData("quests json", ExportKind.Quests, ExportFormat.Json)]
    [InlineData("csv moonlit", ExportKind.Moonlit, ExportFormat.Csv)]
    [InlineData("  MOONLIT  ", ExportKind.Moonlit, ExportFormat.Json)]
    [InlineData("quest CSV", ExportKind.Quests, ExportFormat.Csv)]
    public void Kind_and_format_are_read_in_either_order(string arguments, ExportKind kind, ExportFormat format)
    {
        var request = ExportRequest.Parse(arguments, ExportFormat.Json);

        Assert.Equal([kind], request.Kinds);
        Assert.Equal(format, request.Format);
        Assert.Null(request.Error);
    }

    [Fact]
    public void An_unknown_word_is_named_and_nothing_is_exported()
    {
        var request = ExportRequest.Parse("quests xml", ExportFormat.Json);

        Assert.Equal("xml", request.Error);
        Assert.Empty(request.Kinds);
    }
}
