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
    public void A_read_refused_while_another_writer_replaces_the_file_is_tried_again()
    {
        // Windows answers "access denied" to an open while another client's rename replaces the file (seen on the
        // release runner in SharedFileTests); like a sharing violation, it passes in a moment.
        Assert.True(AtomicFile.ShouldRetryRead(new UnauthorizedAccessException(), attempt: 1));
        Assert.True(AtomicFile.ShouldRetryRead(new IOException("sharing violation"), attempt: 1));

        // A missing file is an answer, not a refusal; other failures are not retried at all.
        Assert.False(AtomicFile.ShouldRetryRead(new FileNotFoundException(), attempt: 1));
        Assert.False(AtomicFile.ShouldRetryRead(new DirectoryNotFoundException(), attempt: 1));
        Assert.False(AtomicFile.ShouldRetryRead(new InvalidOperationException(), attempt: 1));

        // A file that stays refused fails after a short, bounded wait instead of hanging the caller.
        var attempts = 1;
        while (AtomicFile.ShouldRetryRead(new UnauthorizedAccessException(), attempts))
        {
            attempts++;
        }

        Assert.InRange(attempts, 2, 20);
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
    public void Write_uses_a_temporary_file_of_its_own_and_ignores_a_leftover_one()
    {
        var path = tmp.File("file.json");
        // A leftover of an older version, or another client's save in flight: never moved into place, never deleted here.
        File.WriteAllText(path + ".tmp", "stale");

        AtomicFile.Write(path, "fresh");

        Assert.Equal("fresh", File.ReadAllText(path));
        Assert.Equal([path + ".tmp"], Directory.GetFiles(tmp.Path, "*" + AtomicFile.TempSuffix));
    }

    [Fact]
    public void Temporary_names_are_unique_and_carry_the_process()
    {
        var path = tmp.File("file.json");

        var a = AtomicFile.TempPathFor(path);
        var b = AtomicFile.TempPathFor(path);

        Assert.NotEqual(a, b);
        Assert.StartsWith(path + "." + Environment.ProcessId + "-", a);
        Assert.EndsWith(AtomicFile.TempSuffix, a);
    }

    [Fact]
    public void Write_waits_out_a_read_in_another_client_instead_of_failing()
    {
        var path = tmp.File("file.json");
        AtomicFile.Write(path, "first");

        // Windows refuses to replace an open file; the reader lets go after 50 ms and the write's retry gets through.
        using (HeldFile.ReleasedAfter(path, TimeSpan.FromMilliseconds(50)))
        {
            AtomicFile.Write(path, "second");
        }

        Assert.Equal("second", AtomicFile.Read(path));
    }

    [Fact]
    public void Read_does_not_block_a_rename_over_the_file()
    {
        var path = tmp.File("file.json");
        AtomicFile.Write(path, "first");

        Assert.Equal("first", AtomicFile.Read(path));
        File.WriteAllText(path + ".next", "second");
        File.Move(path + ".next", path, overwrite: true);

        Assert.Equal("second", AtomicFile.Read(path));
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
