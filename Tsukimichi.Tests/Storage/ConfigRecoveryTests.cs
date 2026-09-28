using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

public sealed class ConfigRecoveryTests : IDisposable
{
    private static readonly DateTime Stamp = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    [Fact]
    public void Corrupt_path_keeps_the_extension_after_the_stamp()
    {
        var path = tmp.File("Tsukimichi.json");
        Assert.Equal(tmp.File("Tsukimichi.corrupt-20260928120000.json"), ConfigRecovery.CorruptPathFor(path, Stamp));
    }

    [Fact]
    public void Copies_a_corrupt_file_aside_and_leaves_the_original_in_place()
    {
        var path = tmp.File("Tsukimichi.json");
        File.WriteAllText(path, "{ not json");

        Assert.True(ConfigRecovery.TryCopyAside(path, out var copiedTo, out var error, Stamp));

        Assert.Null(error);
        Assert.Equal(tmp.File("Tsukimichi.corrupt-20260928120000.json"), copiedTo);
        Assert.Equal("{ not json", File.ReadAllText(copiedTo!));
        Assert.True(File.Exists(path), "the original stays for Dalamud to overwrite with defaults");
        Assert.Equal("{ not json", File.ReadAllText(path));
    }

    [Fact]
    public void Second_copy_with_the_same_stamp_gets_a_suffix()
    {
        var path = tmp.File("Tsukimichi.json");
        File.WriteAllText(path, "first");
        Assert.True(ConfigRecovery.TryCopyAside(path, out var first, out _, Stamp));
        File.WriteAllText(path, "second");

        Assert.True(ConfigRecovery.TryCopyAside(path, out var second, out _, Stamp));

        Assert.Equal(tmp.File("Tsukimichi.corrupt-20260928120000-1.json"), second);
        Assert.Equal("first", File.ReadAllText(first!));
        Assert.Equal("second", File.ReadAllText(second!));
    }

    [Fact]
    public void Missing_file_is_not_an_error()
    {
        Assert.False(ConfigRecovery.TryCopyAside(tmp.File("none.json"), out var copiedTo, out var error, Stamp));
        Assert.Null(copiedTo);
        Assert.Null(error);
    }

    [Fact]
    public void Locked_file_reports_the_error_instead_of_throwing()
    {
        var path = tmp.File("Tsukimichi.json");
        File.WriteAllText(path, "{}");
        using var handle = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.False(ConfigRecovery.TryCopyAside(path, out var copiedTo, out var error, Stamp));
        Assert.Null(copiedTo);
        Assert.NotNull(error);
    }
}
