using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The Caption and Display type roles (ui-revamp §4.2, T17): game font handles from the plugin's own font atlas
/// (<c>UiBuilder.FontAtlas.NewGameFontHandle</c>), one pair per UI-scale bucket (0.9 / 1.0 / 1.15 / 1.3 / 1.6), each the
/// Axis size nearest to what that bucket draws (<see cref="TypeScale"/>): the game's fonts are pre-baked bitmaps, and
/// one handle scaled bilinearly across every scale would blur (dalamud-developer panel §5). When the UI scale moves to
/// another bucket the old handles are disposed and the new ones built by the atlas in the background; until a handle
/// is <see cref="IFontHandle.Available"/>, its role falls back to the default font drawn at the role's size.
/// <para>
/// A scope (<see cref="Caption"/>, <see cref="Display"/>) pushes the handle and sets the current window's font scale so
/// that <see cref="ImGui.GetFontSize"/> is the role's size: 0.85× the body with a 12 px floor, or 1.2×. Widgets,
/// <c>CalcTextSize</c> and <c>AddText(pos, col, text)</c> inside it all draw at that size. On dispose the window's own
/// scale and font come back. Allocation-free per frame (the handle's push is pooled by Dalamud).
/// </para>
/// </summary>
public static class Typography
{
    private static readonly GameFontFamilyAndSize[] GameFonts =
    [
        GameFontFamilyAndSize.Axis96,
        GameFontFamilyAndSize.Axis12,
        GameFontFamilyAndSize.Axis14,
        GameFontFamilyAndSize.Axis18,
        GameFontFamilyAndSize.Axis36,
    ];

    private static IFontAtlas? atlas;
    private static IPluginLog? log;
    private static int bucket = -1;
    private static IFontHandle? caption;
    private static IFontHandle? display;

    /// <summary>Gives the typography the plugin's font atlas; handles are created on the first <see cref="Update"/>.</summary>
    public static void Initialize(IFontAtlas fontAtlas, IPluginLog? pluginLog)
    {
        atlas = fontAtlas ?? throw new ArgumentNullException(nameof(fontAtlas));
        log = pluginLog;
        bucket = -1;
    }

    /// <summary>
    /// Once per frame after <see cref="UiMetrics.Update"/>, outside any window: when the UI scale entered another bucket,
    /// disposes the handles of the old one and asks the atlas for the new pair. Nothing happens otherwise.
    /// </summary>
    public static void Update()
    {
        if (atlas is null)
        {
            return;
        }

        var next = TypeScale.Bucket(UiMetrics.FontScale);
        if (next == bucket)
        {
            return;
        }

        DisposeHandles();
        bucket = next;

        // The body size at UI scale 1 and global scale 1: the default font's size without Dalamud's global scale.
        var basePx = ImGui.GetFont().FontSize / UiMetrics.GlobalScale;
        try
        {
            caption = atlas.NewGameFontHandle(new GameFontStyle(GameFonts[TypeScale.CaptionGameFont(bucket, basePx)]));
            display = atlas.NewGameFontHandle(new GameFontStyle(GameFonts[TypeScale.DisplayGameFont(bucket, basePx)]));
        }
        catch (Exception ex)
        {
            // No game fonts (a data problem, an atlas that refuses): the roles keep their fallback for good.
            log?.Warning(ex, "Game font handles unavailable; captions and titles use the default font");
            DisposeHandles();
        }
    }

    /// <summary>Disposes the handles (plugin unload).</summary>
    public static void Dispose()
    {
        DisposeHandles();
        atlas = null;
        bucket = -1;
    }

    /// <summary>The caption size in the current window: 0.85× its body size, never under 12 px.</summary>
    public static float CaptionSize => TypeScale.CaptionPx(ImGui.GetFontSize());

    /// <summary>The display size in the current window: 1.2× its body size.</summary>
    public static float DisplaySize => TypeScale.DisplayPx(ImGui.GetFontSize());

    /// <summary>Draws in the caption role until disposed (table headers, pills, the status bar).</summary>
    public static Scope Caption() => new(caption, CaptionSize);

    /// <summary>Draws in the display role until disposed (the hero title, card titles).</summary>
    public static Scope Display() => new(display, DisplaySize);

    private static void DisposeHandles()
    {
        caption?.Dispose();
        display?.Dispose();
        caption = null;
        display = null;
    }

    /// <summary>A pushed role: the game font when it is built, and the window font scale that brings it to the role's size.</summary>
    public struct Scope : IDisposable
    {
        private readonly float ownScale;
        private IDisposable? font;
        private bool active;

        internal Scope(IFontHandle? handle, float targetPx)
        {
            var window = ImGuiP.GetCurrentWindow();
            ownScale = window.FontWindowScale;
            font = handle is { Available: true } ? handle.Push() : null;
            active = true;

            // Whatever font is now current, scale the window so text lands on the target size.
            var now = ImGui.GetFontSize();
            if (now > 0f && targetPx > 0f && float.IsFinite(targetPx))
            {
                ImGui.SetWindowFontScale(ownScale * targetPx / now);
            }
        }

        /// <summary>Whether the game font is in use (false while it is still being built: the default font stands in).</summary>
        public readonly bool GameFont => font is not null;

        public void Dispose()
        {
            if (!active)
            {
                return;
            }

            active = false;
            font?.Dispose();
            font = null;
            ImGui.SetWindowFontScale(ownScale);
        }
    }
}
