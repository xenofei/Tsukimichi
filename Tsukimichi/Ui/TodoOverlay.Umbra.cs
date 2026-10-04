using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Config;
using Tsukimichi.Core.Umbra;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The Todo overlay keeps clear of Umbra's toolbar (plan v8 M3; spec-1.22 M3, umbra-clearance-1.22.png): a top edge
/// inside a top bar's band moves just below it, a bottom edge inside a bottom bar's band just above it. The player's own
/// place is never rewritten: it is remembered (<see cref="Configuration.UmbraPlaces"/>) and given back when the bar
/// leaves, unless the player moves the overlay in between. Nothing moves while the player drags it.
/// </summary>
public sealed partial class TodoOverlay
{
    private const string UmbraPlaceKey = "todo";

    private Vector2 lastWindowPos;
    private Vector2 lastWindowSize;
    private bool lastWindowMoving;
    private bool windowPlaced;
    private ClearedPlace umbraPlace;
    private bool umbraPlaceLoaded;

    /// <summary>Draw: where the window stands and whether the player is moving it, for the next PreDraw.</summary>
    private void NoteWindowPlace()
    {
        lastWindowPos = ImGui.GetWindowPos();
        lastWindowSize = ImGui.GetWindowSize();
        lastWindowMoving = ImGui.IsMouseDown(ImGuiMouseButton.Left) && ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        windowPlaced = true;
    }

    /// <summary>PreDraw: moves the window clear of Umbra's bar, or back to the player's place when the bar left.</summary>
    private void KeepClearOfUmbra()
    {
        if (!windowPlaced)
        {
            return;
        }

        LoadUmbraPlace();
        var before = umbraPlace;
        if (UmbraLayout.Place(ref umbraPlace, lastWindowPos, lastWindowSize, lastWindowMoving) is { } to)
        {
            ImGui.SetNextWindowPos(to, ImGuiCond.Always);
            lastWindowPos = to;
        }

        if (umbraPlace != before)
        {
            SaveUmbraPlace();
        }
    }

    private void LoadUmbraPlace()
    {
        if (umbraPlaceLoaded)
        {
            return;
        }

        umbraPlaceLoaded = true;
        if (settings.UmbraPlaces.TryGetValue(UmbraPlaceKey, out var saved) && saved is { Length: 4 })
        {
            umbraPlace = new ClearedPlace(new Vector2(saved[0], saved[1]), new Vector2(saved[2], saved[3]));
        }
    }

    private void SaveUmbraPlace()
    {
        if (umbraPlace is { Original: { } original, Moved: { } moved })
        {
            settings.UmbraPlaces[UmbraPlaceKey] = [original.X, original.Y, moved.X, moved.Y];
        }
        else if (!settings.UmbraPlaces.Remove(UmbraPlaceKey))
        {
            return;
        }

        settings.Save(pluginInterface);
    }
}
