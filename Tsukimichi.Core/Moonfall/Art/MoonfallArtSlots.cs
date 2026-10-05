namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>The pictures the board's art holds (<see cref="MoonfallArtSlots{T}"/>).</summary>
public enum MoonfallArtSlot
{
    Sheet1x,
    Sheet2x,
    Sky,
    Scene1x,
    Scene2x,
}

/// <summary>Where a picture is.</summary>
public enum MoonfallSlotState : byte
{
    /// <summary>Not asked for (or let go).</summary>
    Idle,

    /// <summary>Decoding off the framework thread.</summary>
    Loading,

    /// <summary>Ready to draw.</summary>
    Ready,

    /// <summary>Missing, unreadable or the wrong size: not asked for again until the window closes.</summary>
    Failed,
}

/// <summary>What the board's ground is this frame.</summary>
public enum MoonfallGround : byte
{
    /// <summary>The atlas's flat ground ink: Plain, or no picture ready.</summary>
    Flat,

    /// <summary>The shared night sky.</summary>
    Sky,

    /// <summary>The level's own scene.</summary>
    Scene,
}

/// <summary>
/// The board art's textures and its fallbacks (feature plan v9 G8), as pure state so the rules are tested without the
/// game: the board draws its art only while the atlas manifest has read and a sheet is ready, else the stage 1
/// primitives; the 2x sheet is used when wanted and ready, the 1x standing in while it loads (never the other way, unless
/// the 1x failed); the ground is the level's scene at the wanted tier, the other tier while it loads, else the sky, else
/// the flat ground. The texture owner (the plugin's <c>MoonfallArtTextures</c>) starts loads, lands them and takes back
/// what <see cref="Clear"/> lets go. Framework thread only.
/// </summary>
public sealed class MoonfallArtSlots<T>
    where T : class
{
    private const int Count = (int)MoonfallArtSlot.Scene2x + 1;

    private readonly MoonfallSlotState[] states = new MoonfallSlotState[Count];
    private readonly T?[] held = new T?[Count];

    /// <summary>The manifest, once read and checked; null while it reads, or when it failed (<see cref="ManifestFailed"/>).</summary>
    public MoonfallAtlas? Atlas { get; private set; }

    /// <summary>Whether the manifest was refused: the board keeps the primitives until the window closes.</summary>
    public bool ManifestFailed { get; private set; }

    /// <summary>The scene the level names, or null; its pictures are let go when it changes.</summary>
    public string? Scene { get; private set; }

    public MoonfallSlotState State(MoonfallArtSlot slot) => states[(int)slot];

    /// <summary>Takes the manifest's result; a refused one keeps every texture slot idle.</summary>
    public void SetManifest(MoonfallAtlas? atlas)
    {
        Atlas = atlas;
        ManifestFailed = atlas is null;
    }

    /// <summary>Whether <paramref name="slot"/> should start loading now: a manifest is in and nothing is held or tried.</summary>
    public bool ShouldLoad(MoonfallArtSlot slot) => Atlas is not null && states[(int)slot] == MoonfallSlotState.Idle
        && (slot is not (MoonfallArtSlot.Scene1x or MoonfallArtSlot.Scene2x) || Scene is not null);

    public void Begin(MoonfallArtSlot slot) => states[(int)slot] = MoonfallSlotState.Loading;

    /// <summary>A picture has landed. One that landed after its slot was let go is returned for disposal; else null.</summary>
    public T? Land(MoonfallArtSlot slot, T texture)
    {
        if (states[(int)slot] != MoonfallSlotState.Loading)
        {
            return texture;
        }

        states[(int)slot] = MoonfallSlotState.Ready;
        held[(int)slot] = texture;
        return null;
    }

    public void Fail(MoonfallArtSlot slot)
    {
        states[(int)slot] = MoonfallSlotState.Failed;
        held[(int)slot] = null;
    }

    /// <summary>Lets <paramref name="slot"/> go; returns what it held, for disposal.</summary>
    public T? Clear(MoonfallArtSlot slot)
    {
        var texture = held[(int)slot];
        held[(int)slot] = null;
        states[(int)slot] = MoonfallSlotState.Idle;
        return texture;
    }

    /// <summary>
    /// The level's scene (null for none). Another one lets the old scene's pictures go into <paramref name="released"/>.
    /// </summary>
    public void WantScene(string? scene, List<T> released)
    {
        ArgumentNullException.ThrowIfNull(released);
        if (string.Equals(scene, Scene, StringComparison.Ordinal))
        {
            return;
        }

        Scene = scene;
        Release(MoonfallArtSlot.Scene1x, released);
        Release(MoonfallArtSlot.Scene2x, released);
    }

    /// <summary>The window closed: every picture into <paramref name="released"/>, failures forgotten, the manifest kept.</summary>
    public void ReleaseAll(List<T> released)
    {
        ArgumentNullException.ThrowIfNull(released);
        for (var i = 0; i < Count; i++)
        {
            Release((MoonfallArtSlot)i, released);
        }

        Scene = null;
    }

    private void Release(MoonfallArtSlot slot, List<T> released)
    {
        if (Clear(slot) is { } texture)
        {
            released.Add(texture);
        }
    }

    /// <summary>
    /// The atlas sheet to draw with, and whether it is the 2x one; null when the art cannot draw yet (or at all), so the
    /// board draws its primitives.
    /// </summary>
    public T? Sheet(bool wantTwoX, out bool twoX)
    {
        var one = held[(int)MoonfallArtSlot.Sheet1x];
        var two = held[(int)MoonfallArtSlot.Sheet2x];
        twoX = Atlas is not null && two is not null && (wantTwoX || one is null);
        return Atlas is null ? null : twoX ? two : one;
    }

    /// <summary>
    /// Whether the sky is needed now: for a level without a scene, or once a tier of its scene has failed (so a scene
    /// that loads keeps the sky's memory free).
    /// </summary>
    public bool NeedsSky => Scene is null
        || states[(int)MoonfallArtSlot.Scene1x] == MoonfallSlotState.Failed || states[(int)MoonfallArtSlot.Scene2x] == MoonfallSlotState.Failed;

    /// <summary>
    /// The ground to draw and its picture: the scene at the wanted tier, else at the other, else the sky, else flat (null).
    /// </summary>
    public T? Ground(bool wantTwoX, out MoonfallGround ground)
    {
        var wanted = held[(int)(wantTwoX ? MoonfallArtSlot.Scene2x : MoonfallArtSlot.Scene1x)];
        var other = held[(int)(wantTwoX ? MoonfallArtSlot.Scene1x : MoonfallArtSlot.Scene2x)];
        if ((wanted ?? other) is { } scene)
        {
            ground = MoonfallGround.Scene;
            return scene;
        }

        if (held[(int)MoonfallArtSlot.Sky] is { } sky)
        {
            ground = MoonfallGround.Sky;
            return sky;
        }

        ground = MoonfallGround.Flat;
        return null;
    }
}
