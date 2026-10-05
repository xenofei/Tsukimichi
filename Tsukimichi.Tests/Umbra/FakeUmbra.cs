using Tsukimichi.Core.Umbra;

namespace Tsukimichi.Tests.Umbra;

/// <summary>
/// Umbra as the runner sees it: custom plugins, the add-on's repository entry, add-ons stored while custom plugins were
/// off (dormant: Umbra reads them only once custom plugins are on), whether the add-on is loaded (only a restart loads or
/// unloads it, as Umbra's own), the widgets on the active toolbar profile (shown only while the add-on is loaded), and
/// the configuration profile it runs on. Calls can be made to fail, throw before or after changing, or wait.
/// </summary>
internal sealed class FakeUmbra : IUmbraControl, IUmbraSession
{
    private bool undoing;

    public bool CustomPluginsOn { get; set; }

    public bool Listed { get; set; }

    public bool AddonLoaded { get; set; }

    /// <summary>Other add-ons in memory (what Umbra counts).</summary>
    public int OtherAddons { get; set; }

    /// <summary>Other add-ons stored while custom plugins were off; turning them on loads them (LoadPluginEntries).</summary>
    public int DormantAddons { get; set; }

    public string Profile { get; set; } = UmbraSettings.DefaultProfile;

    public ulong CharacterId { get; set; } = 7;

    public List<(string Id, string Widget)> Widgets { get; } = [];

    public UmbraFailure PrepareResult { get; set; }

    public UmbraFailure AddResult { get; init; }

    public bool AddListsAnyway { get; init; }

    public UmbraFailure RestartResult { get; set; }

    public bool RestartLoads { get; init; } = true;

    public bool PlaceFails { get; init; }

    /// <summary>A restart waits for this before it completes (one run at a time).</summary>
    public TaskCompletionSource? HoldRestart { get; set; }

    /// <summary>A call that throws, before changing anything, while adding.</summary>
    public string? ThrowOn { get; init; }

    /// <summary>A call that throws after it changed Umbra (an Umbra event handler failing), while adding.</summary>
    public string? ThrowAfter { get; init; }

    /// <summary>A call that throws while putting back.</summary>
    public string? ThrowOnUndo { get; init; }

    public List<string> Calls { get; } = [];

    public Task<UmbraOpened> Open()
    {
        Call("Open");
        return Task.FromResult(new UmbraOpened(PrepareResult, PrepareResult == UmbraFailure.None ? this : null));
    }

    public Task<UmbraLook> Look()
    {
        if (Calls.Count == 0 || Calls[^1] != "Look")
        {
            Calls.Add("Look");
        }

        IReadOnlyList<string> ours = AddonLoaded ? [.. Widgets.Where(w => w.Widget == UmbraAddon.WidgetId).Select(w => w.Id)] : [];
        return Task.FromResult(new UmbraLook(CustomPluginsOn, Listed, AddonLoaded, ours.Count > 0, OtherAddons, Profile, CharacterId, ours));
    }

    public Task SetCustomPlugins(bool on)
    {
        if (!on)
        {
            undoing = true;
        }

        var name = $"SetCustomPlugins({on})";
        Call(name);
        CustomPluginsOn = on;
        if (on)
        {
            // LoadPluginEntries: the stored list comes into memory.
            OtherAddons += DormantAddons;
            DormantAddons = 0;
        }

        After(name);
        return Task.CompletedTask;
    }

    public Task<UmbraFailure> AddRepository()
    {
        Call("AddRepository");
        if (AddResult != UmbraFailure.None)
        {
            Listed = AddListsAnyway;
            return Task.FromResult(AddResult);
        }

        Listed = true;
        After("AddRepository");
        return Task.FromResult(UmbraFailure.None);
    }

    public Task<int> RemoveRepository()
    {
        undoing = true;
        Call("RemoveRepository");
        var was = Listed;
        Listed = false;
        return Task.FromResult(was ? 1 : 0);
    }

    public async Task<UmbraFailure> Restart()
    {
        Call("Restart");
        if (HoldRestart is { } hold)
        {
            await hold.Task.ConfigureAwait(false);
        }

        if (RestartResult != UmbraFailure.None)
        {
            return RestartResult;
        }

        AddonLoaded = Listed && CustomPluginsOn && RestartLoads;
        return UmbraFailure.None;
    }

    public Task<bool> PlaceWidget(string widgetId)
    {
        Call("PlaceWidget");
        if (PlaceFails || !AddonLoaded)
        {
            return Task.FromResult(false);
        }

        Widgets.Add((widgetId, UmbraAddon.WidgetId));
        After("PlaceWidget");
        return Task.FromResult(true);
    }

    public Task<bool> RemoveWidget(string widgetId)
    {
        undoing = true;
        Call("RemoveWidget");
        return Task.FromResult(Widgets.RemoveAll(w => w.Id == widgetId) > 0);
    }

    private void Call(string name)
    {
        Calls.Add(name);
        var throwing = undoing ? ThrowOnUndo : ThrowOn;
        if (throwing is not null && name.StartsWith(throwing, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Umbra threw in {name}");
        }
    }

    private void After(string name)
    {
        if (!undoing && ThrowAfter is not null && name.StartsWith(ThrowAfter, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"An Umbra handler threw after {name}");
        }
    }
}
