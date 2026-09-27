using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

public sealed class AtomicFileTests : IDisposable
{
    private readonly TempDir tmp = new();

    public void Dispose() => tmp.Dispose();

    [Fact]
    public void Write_creates_parent_directories_and_leaves_no_tmp_file()
    {
        var path = tmp.File(Path.Combine("nested", "deeper", "file.json"));

        AtomicFile.Write(path, "{\"a\":1}");

        Assert.Equal("{\"a\":1}", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!));
    }

    [Fact]
    public void Write_overwrites_existing_file()
    {
        var path = tmp.File("file.json");
        AtomicFile.Write(path, "first");

        AtomicFile.Write(path, "second");

        Assert.Equal("second", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Write_replaces_a_stale_tmp_file()
    {
        var path = tmp.File("file.json");
        File.WriteAllText(path + ".tmp", "stale");

        AtomicFile.Write(path, "fresh");

        Assert.Equal("fresh", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Read_returns_null_for_missing_file()
    {
        Assert.Null(AtomicFile.Read(tmp.File("missing.json")));
    }

    [Fact]
    public void Read_returns_contents_written()
    {
        var path = tmp.File("file.json");
        AtomicFile.Write(path, "hello");

        Assert.Equal("hello", AtomicFile.Read(path));
    }

    [Fact]
    public void Quarantine_renames_with_timestamp_suffix()
    {
        var path = tmp.File("bad.json");
        File.WriteAllText(path, "not json");
        var stamp = new DateTime(2026, 9, 27, 13, 45, 10, DateTimeKind.Utc);

        var moved = AtomicFile.Quarantine(path, stamp);

        Assert.Equal(path + ".corrupt-20260927134510", moved);
        Assert.False(File.Exists(path));
        Assert.Equal("not json", File.ReadAllText(moved));
    }

    [Fact]
    public void Quarantine_does_not_clobber_existing_quarantine_file()
    {
        var path = tmp.File("bad.json");
        var stamp = new DateTime(2026, 9, 27, 13, 45, 10, DateTimeKind.Utc);
        File.WriteAllText(path + ".corrupt-20260927134510", "older");
        File.WriteAllText(path, "newer");

        var moved = AtomicFile.Quarantine(path, stamp);

        Assert.NotEqual(path + ".corrupt-20260927134510", moved);
        Assert.Equal("older", File.ReadAllText(path + ".corrupt-20260927134510"));
        Assert.Equal("newer", File.ReadAllText(moved));
    }
}
