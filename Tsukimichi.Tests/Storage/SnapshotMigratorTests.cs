using System.Text.Json.Nodes;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Tests.Storage;

public sealed class SnapshotMigratorTests
{
    [Fact]
    public void Current_version_passes_through_untouched()
    {
        var migrator = new SnapshotMigrator();
        var node = JsonNode.Parse("""{ "schemaVersion": 1, "name": "a" }""")!;

        var result = migrator.Migrate(node, out var fromVersion);

        Assert.Equal(CharacterSnapshot.CurrentSchemaVersion, fromVersion);
        Assert.Same(node, result);
    }

    [Fact]
    public void Missing_version_is_treated_as_version_one()
    {
        var migrator = new SnapshotMigrator();
        var node = JsonNode.Parse("""{ "name": "a" }""")!;

        var result = migrator.Migrate(node, out var fromVersion);

        Assert.Equal(1, fromVersion);
        Assert.Equal(CharacterSnapshot.CurrentSchemaVersion, (int)result["schemaVersion"]!);
    }

    [Fact]
    public void Steps_are_chained_in_order_and_version_is_stamped()
    {
        var migrator = new SnapshotMigrator();
        var order = new List<int>();
        migrator.Register(-1, n => { order.Add(-1); n["a"] = 1; return n; });
        migrator.Register(0, n => { order.Add(0); n["b"] = 2; return n; });
        var node = JsonNode.Parse("""{ "schemaVersion": -1 }""")!;

        var result = migrator.Migrate(node, out var fromVersion);

        Assert.Equal(-1, fromVersion);
        Assert.Equal(new[] { -1, 0 }, order);
        Assert.Equal(1, (int)result["a"]!);
        Assert.Equal(2, (int)result["b"]!);
        Assert.Equal(CharacterSnapshot.CurrentSchemaVersion, (int)result["schemaVersion"]!);
    }

    [Fact]
    public void Missing_step_throws_InvalidDataException()
    {
        var migrator = new SnapshotMigrator();
        var node = JsonNode.Parse("""{ "schemaVersion": 0 }""")!;

        Assert.Throws<InvalidDataException>(() => migrator.Migrate(node, out _));
    }

    [Fact]
    public void Newer_version_than_current_throws_InvalidDataException()
    {
        var migrator = new SnapshotMigrator();
        var node = JsonNode.Parse("""{ "schemaVersion": 999 }""")!;

        Assert.Throws<InvalidDataException>(() => migrator.Migrate(node, out _));
    }

    [Fact]
    public void Non_object_root_throws_InvalidDataException()
    {
        var migrator = new SnapshotMigrator();
        var node = JsonNode.Parse("[1, 2]")!;

        Assert.Throws<InvalidDataException>(() => migrator.Migrate(node, out _));
    }

    [Fact]
    public void Registering_same_version_twice_throws()
    {
        var migrator = new SnapshotMigrator();
        migrator.Register(0, n => n);

        Assert.Throws<ArgumentException>(() => migrator.Register(0, n => n));
    }
}
