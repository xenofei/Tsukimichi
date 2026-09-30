using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Tsukimichi.Config;
using Tsukimichi.Localization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Settings (spec §7): poll interval (with the measured cost of a poll under it), display scale sliders
/// (<see cref="Configuration.UiScale"/>, <see cref="Configuration.IconScale"/>), Reduce motion and the journal
/// filing, chat notices, the Removed from the game node, the spoiler shield (T19: names ahead, how many to reveal,
/// artwork, the viewed character's override), the todo overlay (on/off, lock, opacity, sections, reset position), item hints, the Wotsit
/// integration, help (open it, start the tutorial, offer it on first run), the user's Moonlit verdicts with Restore
/// and a hold-to-confirm Restore all, data deletion with a double confirm, and an About section with the plugin,
/// reward-data and catalog stamps plus the poll timing. Every change is saved as it happens; sliders save when
/// released.
/// </summary>
public sealed partial class ConfigWindow : Window
{
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(8);
    private static string RestoreAllLabel => restoreAllLabelText.Value;

    private static readonly Localization.LocText restoreAllLabelText = new(static () => Strings.ConfigVerdictRestoreAll + Chrome.HoldIdSuffix);

    private static readonly LocText UnpinPlanLabel = new(static () => Strings.Unpin + "##todoPlanUnpin");

    private readonly Configuration settings;
    private readonly SessionState session;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly Action<bool> onShowUnlistedChanged;

    private readonly LocText pluginVersionLine;
    private readonly string dataStampLine;
    private readonly string? dataVersionWarning;
    private readonly LocText curatedLine;

    private CatalogBundle? aboutBundle;
    private int aboutLanguage = -1;
    private string catalogLine = Strings.ConfigCatalogLoading;
    private string? catalogError;
    private int catalogErrorLanguage = -1;

    private float pollSeconds;
    private bool pollDirty;
    private bool scaleDirty;
    private bool todoDirty;
    private bool spoilerAheadDirty;
    private bool openSecondConfirm;

    // Spoilers: the "N names hidden" line, rebuilt once per session version.
    private int spoilerCountVersion = -1;
    private string spoilerCountLine = string.Empty;

    // Spoilers: the "Shield for {name}" label, rebuilt when the viewed character or its name changes.
    private ulong spoilerLabelContentId;
    private string? spoilerLabelName;
    private string spoilerLabel = string.Empty;

    // Todo overlay: the "Pinned: {expansion}" line, rebuilt when the pinned expansion or the names change.
    private int planPinnedExpansion = int.MinValue;
    private Core.Evaluation.BlockerNames? planPinnedNames;
    private int planPinnedLanguage = -1;
    private string planPinnedLabel = string.Empty;
    private string? toast;
    private DateTime toastUntilUtc;

    // "Your Moonlit verdicts": one row per override, rebuilt when the overrides or the quest catalog change.
    private readonly ConfirmGate restoreAllGate = new();
    private VerdictRow[] verdictRows = [];
    private int verdictVersion = -1;
    private CatalogBundle? verdictBundle;
    private int verdictSpoilers;
    private string verdictsHeader = string.Empty;

    // Poll timing lines, rebuilt only when another poll completed.
    private int pollTimingCount = -1;
    private int pollTimingLanguage = -1;
    private string pollTimingLine = Strings.ConfigPollTimingNone;
    private string pollCostLine = Strings.ConfigPollCostUnknown;

    /// <param name="diagnostics">Owns the data stamp and the game-version warning the About section shows.</param>
    /// <summary>The UI language service (V2-19); Settings › Display › Plugin language applies through it. Null in no build.</summary>
    public LocService? Language { get; set; }

    public ConfigWindow(Configuration settings, SessionState session, IDalamudPluginInterface pluginInterface, DiagnosticBuilder diagnostics, Action<bool> onShowUnlistedChanged)
        : base(Strings.ConfigWindowTitle)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        ArgumentNullException.ThrowIfNull(diagnostics);
        this.onShowUnlistedChanged = onShowUnlistedChanged ?? throw new ArgumentNullException(nameof(onShowUnlistedChanged));

        Size = new Vector2(480f, 640f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(400f, 320f) };

        var pluginVersion = diagnostics.PluginVersion.Length > 0 ? diagnostics.PluginVersion : "unknown";
        pluginVersionLine = new LocText(() => string.Format(CultureInfo.CurrentCulture, Strings.ConfigPluginVersionFormat, pluginVersion));
        dataStampLine = diagnostics.DataStampLine;
        dataVersionWarning = diagnostics.VersionMismatchWarning;

        var curated = session.Curated;
        curatedLine = new LocText(() => string.Format(
            CultureInfo.CurrentCulture,
            Strings.ConfigCuratedFormat,
            curated.SystemUnlocks.Count,
            curated.DutyUnlocks.Count,
            curated.FeatureQuests.Count,
            curated.Festivals.Count));

        ReadSettings();
    }

    /// <summary>Opens the help window; set by the plugin once the help window exists. Null hides the button.</summary>
    public Action? ShowHelp { get; set; }

    /// <summary>Starts the interactive tutorial; set by the plugin once the overlay exists. Null hides the button.</summary>
    public Action? StartTutorial { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.WotsitIntegration"/> is toggled and saved; the plugin points it at the Wotsit IPC.</summary>
    public Action<bool>? WotsitToggled { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.JournalFiling"/> changes and is saved; the plugin rebuilds the catalog.</summary>
    public Action<JournalFiling>? JournalFilingChanged { get; set; }

    /// <summary>Moves the todo overlay back to its default place; set by the plugin once the overlay exists. Null hides the button.</summary>
    public Action? ResetTodoPosition { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.ItemHintsEnabled"/> is toggled and saved; the item-hint feature wires it.</summary>
    public Action<bool>? ItemHintsToggled { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.ItemContextMenuEnabled"/> is toggled and saved; the item-hint feature wires it.</summary>
    public Action<bool>? ItemContextMenuToggled { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.NpcContextMenuEnabled"/> is toggled and saved; the NPC menu hook wires it.</summary>
    public Action<bool>? NpcContextMenuToggled { get; set; }

    /// <summary>Called with the new value after <see cref="Configuration.DutyFinderHintEnabled"/> is toggled and saved; the Duty Finder hint wires it.</summary>
    public Action<bool>? DutyFinderHintToggled { get; set; }

    /// <summary>
    /// The shared addon kill switch (T20) behind Integrations' paused notice and "Enable game hooks on this untested
    /// version"; set by the plugin. Null hides both.
    /// </summary>
    public HookGate? HookGate { get; set; }

    /// <summary>Settings › Data › Export (P12); set by the plugin once the export service exists. Null hides it.</summary>
    public ExportSection? Export { get; set; }

    /// <summary>The user's Moonlit verdicts for the Data section; set by the plugin once the Moonlit pane exists. Null shows a placeholder.</summary>
    public IUniqueOverrides? Overrides { get; set; }

    public override void OnOpen()
    {
        ReadSettings();
    }

    public override void OnClose()
    {
        if (pollDirty || scaleDirty || todoDirty || spoilerAheadDirty)
        {
            pollDirty = false;
            scaleDirty = false;
            todoDirty = false;
            spoilerAheadDirty = false;
            Save();
        }
    }

    public override void Draw()
    {
        DrawPolling();
        ImGui.Spacing();
        DrawDisplay();
        ImGui.Spacing();
        DrawKeyboard();
        ImGui.Spacing();
        DrawNotices();
        ImGui.Spacing();
        DrawJournal();
        ImGui.Spacing();
        DrawJournalText();
        ImGui.Spacing();
        DrawSpoilers();
        ImGui.Spacing();
        DrawTodoOverlay();
        ImGui.Spacing();
        DrawItemHints();
        ImGui.Spacing();
        DrawIntegrations();
        ImGui.Spacing();
        DrawHelp();
        ImGui.Spacing();
        DrawData();
        ImGui.Spacing();
        DrawAbout();
    }

    private void DrawPolling()
    {
        Header(Strings.ConfigSectionPolling);
        bool moved;
        ImGui.SetNextItemWidth(SliderWidth());
        using (ImRaii.PushId(Strings.ConfigPollInterval))
        {
            moved = ImGui.SliderFloat("##slider", ref pollSeconds, (float)Configuration.MinPollIntervalSeconds, (float)Configuration.MaxPollIntervalSeconds, "%.1f s", ImGuiSliderFlags.AlwaysClamp);
        }

        if (moved)
        {
            settings.PollIntervalSeconds = Math.Round(pollSeconds, 1);
            pollDirty = true;
        }

        if (pollDirty && ImGui.IsItemDeactivatedAfterEdit())
        {
            pollDirty = false;
            Save();
        }

        Chrome.TrailingLabel(Strings.ConfigPollInterval);
        Chrome.Hint(Strings.ConfigPollIntervalHint);
        RefreshPollTiming();
        Chrome.Hint(pollCostLine);
    }

    /// <summary>Copies the slider-backed values out of the configuration (on construction and each time the window opens).</summary>
    private void ReadSettings()
    {
        pollSeconds = (float)settings.PollInterval.TotalSeconds;
        pollDirty = false;
        scaleDirty = false;
    }

    /// <summary>
    /// Two sliders written to the configuration as they move and saved when released. They read the configuration
    /// every frame (through the same clamps as the main window's filter panel, which edits the same values), so the
    /// two places never disagree and a corrupt value shows as the default rather than NaN.
    /// </summary>
    private void DrawDisplay()
    {
        Header(Strings.ConfigSectionDisplay);
        DrawLook();
        DrawLanguage();

        var uiScale = ScaleMetrics.ClampUiScale(settings.UiScale);
        bool moved;
        ImGui.SetNextItemWidth(SliderWidth());
        using (ImRaii.PushId(Strings.ConfigUiScale))
        {
            moved = ImGui.SliderFloat("##slider", ref uiScale, ScaleMetrics.MinUiScale, ScaleMetrics.MaxUiScale, "%.2f", ImGuiSliderFlags.AlwaysClamp);
        }

        if (moved)
        {
            settings.UiScale = uiScale;
            scaleDirty = true;
        }

        SaveWhenReleased();
        Chrome.TrailingLabel(Strings.ConfigUiScale);
        Chrome.Hint(Strings.ConfigUiScaleHint);

        var iconScale = ScaleMetrics.ClampIconScale(settings.IconScale);
        ImGui.SetNextItemWidth(SliderWidth());
        using (ImRaii.PushId(Strings.ConfigIconScale))
        {
            moved = ImGui.SliderFloat("##slider", ref iconScale, ScaleMetrics.MinIconScale, ScaleMetrics.MaxIconScale, "%.2f", ImGuiSliderFlags.AlwaysClamp);
        }

        if (moved)
        {
            settings.IconScale = iconScale;
            scaleDirty = true;
        }

        SaveWhenReleased();
        Chrome.TrailingLabel(Strings.ConfigIconScale);
        Chrome.Hint(Strings.ConfigIconScaleHint);

        // Density: the quest table's row height only (T12).
        ImGui.TextUnformatted(Strings.ConfigDensity);
        Chrome.SameLineOrWrap(RadioWidth(Strings.ConfigDensityComfortable));
        if (ImGui.RadioButton(Strings.ConfigDensityComfortable, settings.Density == RowDensity.Comfortable))
        {
            settings.Density = RowDensity.Comfortable;
            Save();
        }

        Chrome.SameLineOrWrap(RadioWidth(Strings.ConfigDensityDense));
        if (ImGui.RadioButton(Strings.ConfigDensityDense, settings.Density == RowDensity.Dense))
        {
            settings.Density = RowDensity.Dense;
            Save();
        }

        Chrome.Hint(Strings.ConfigDensityHint);

        // The main window's rail: 64 px stations with labels, or 44 px icons (feature plan v4 L7).
        var compactRail = settings.CompactRail;
        if (ImGui.Checkbox(Strings.ConfigCompactRail, ref compactRail))
        {
            settings.CompactRail = compactRail;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ConfigCompactRailHint);
        }

        // One layout, two palettes (T13): Night, or the surfaces mapped from the user's Dalamud style.
        var followDalamud = settings.FollowDalamudColours;
        if (ImGui.Checkbox(Strings.ConfigFollowDalamudColours, ref followDalamud))
        {
            settings.FollowDalamudColours = followDalamud;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ConfigFollowDalamudColoursHint);
        }

        DrawGlyphPalette();
        DrawJournalFiling();
    }

    /// <summary>
    /// Settings › Display › Look (moon-road proposal §7.9, feature plan v4 V1/V3): Flair (Full / Quiet / Plain), Game
    /// fonts for headings (off under Plain, which is the look before 1.4) and Reduce motion. Saved at once and applied
    /// from the next frame (<see cref="Theme.Refresh"/>, <see cref="Typography.Update"/>).
    /// </summary>
    private void DrawLook()
    {
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextUnformatted(Strings.ConfigLook);
        }

        ImGui.TextUnformatted(Strings.ConfigFlair);
        FlairRadio(Strings.ConfigFlairFull, Flair.Full);
        FlairRadio(Strings.ConfigFlairQuiet, Flair.Quiet);
        FlairRadio(Strings.ConfigFlairPlain, Flair.Plain);
        DrawFlairPreviews();

        using (ImRaii.Disabled(settings.Flair == Flair.Plain))
        {
            var headingFonts = settings.GameHeadingFonts;
            if (ImGui.Checkbox(Strings.ConfigGameHeadingFonts, ref headingFonts))
            {
                settings.GameHeadingFonts = headingFonts;
                Save();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.ConfigGameHeadingFontsHint);
        }

        var reduceMotion = settings.ReduceMotion;
        if (ImGui.Checkbox(Strings.ConfigReduceMotion, ref reduceMotion))
        {
            settings.ReduceMotion = reduceMotion;
            settings.ReduceMotionChosen = true;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ConfigReduceMotionHint);
        }

        ImGui.Spacing();
    }

    /// <summary>
    /// Live previews of the three Flair levels (proposal §7.9), side by side when the window is wide enough and stacked
    /// otherwise: each a section heading and a small journal row drawn exactly as that level draws them (the pane
    /// gradient behind at Full; the heading's sigil and rule, the orbit, the road and the Numeral count at Full and
    /// Quiet; the filling moon and the plain heading at Plain). Under the high-contrast palette Full previews as Quiet,
    /// as it draws. The level in use is outlined; a click on a preview chooses it. Drawn only while the window is open.
    /// </summary>
    private void DrawFlairPreviews()
    {
        var room = ImGui.GetContentRegionAvail().X;
        var gap = UiMetrics.Px(10f);
        var side = room >= UiMetrics.Px(480f);
        var cell = side ? MathF.Floor((room - (2f * gap)) / 3f) : MathF.Min(room, UiMetrics.Px(260f));
        var origin = ImGui.GetCursorScreenPos();
        var contentRight = origin.X + room;
        var bottom = origin.Y;
        var dl = ImGui.GetWindowDrawList();
        var pad = UiMetrics.Px(6f);
        var levels = PreviewLevels;
        for (var i = 0; i < levels.Length; i++)
        {
            var level = levels[i];
            var flair = FlairRules.Effective(level, Theme.Glyphs.HighContrast);
            var min = side ? new Vector2(origin.X + (i * (cell + gap)), origin.Y) : new Vector2(origin.X, bottom);
            ImGui.PushID(i);
            dl.ChannelsSplit(2);
            dl.ChannelsSetCurrent(1);
            ImGui.SetCursorScreenPos(min + new Vector2(pad));
            ImGui.BeginGroup();
            using (Typography.Caption())
            {
                ImGui.TextDisabled(level switch
                {
                    Flair.Full => Strings.ConfigFlairFull,
                    Flair.Quiet => Strings.ConfigFlairQuiet,
                    _ => Strings.ConfigFlairPlain,
                });
            }

            SectionHeading.Draw(Strings.CharactersSectionCompletion, null, contentRight - (min.X + cell - pad), true, flair);
            PreviewRow(dl, flair, cell - (2f * pad));
            ImGui.EndGroup();
            var max = new Vector2(min.X + cell, ImGui.GetItemRectMax().Y + pad);

            // Behind the content: the pane gradient at Full, the outline of the level in use.
            dl.ChannelsSetCurrent(0);
            dl.AddRectFilled(min, max, Theme.U32(Theme.Surface.Window), UiMetrics.Px(4f));
            if (FlairRules.PaneGradient(flair))
            {
                Ornament.PaneGradient(dl, min, max);
            }

            var chosen = settings.Flair == level;
            dl.AddRect(min, max, chosen ? Theme.U32(Theme.Surface.Text) : Theme.U32(Theme.Surface.Line), UiMetrics.Px(4f), ImDrawFlags.None, chosen ? MathF.Max(1.5f, UiMetrics.Px(1.5f)) : UiMetrics.Hairline);
            dl.ChannelsMerge();

            ImGui.SetCursorScreenPos(min);
            if (ImGui.InvisibleButton("##flairPreview", max - min) && !chosen)
            {
                settings.Flair = level;
                Save();
            }

            HintOnHover(Strings.ConfigFlairHint);
            ImGui.PopID();
            bottom = side ? MathF.Max(bottom, max.Y) : max.Y + gap;
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, bottom + (side ? gap : 0f)));
        ImGui.Dummy(Vector2.Zero);
    }

    private static readonly Flair[] PreviewLevels = [Flair.Full, Flair.Quiet, Flair.Plain];

    /// <summary>A small journal row drawn as <paramref name="flair"/> draws it: an orbit, the name, the count and the road, or the filling moon at Plain.</summary>
    private static void PreviewRow(ImDrawListPtr dl, Flair flair, float width)
    {
        const float Fraction = 0.65f;
        var line = ImGui.GetTextLineHeight();
        var box = MathF.Round(MathF.Max(line, UiMetrics.Icon(22f)));
        var road = MathF.Max(1f, UiMetrics.Px(2f));
        var min = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(MathF.Max(1f, width), box + (2f * road)));
        var art = FlairRules.Rules(flair);
        var textures = Plugin.TextureProvider;
        if (art && textures is not null)
        {
            Orbit.Draw(dl, textures, min, box, NodeIcon.Of(OrnamentGlyph.AllQuests), Fraction, highContrast: Theme.Glyphs.HighContrast);
        }
        else
        {
            MoonGlyph.DrawHalo(dl, min + new Vector2(box * 0.5f), box * 0.5f, Fraction);
        }

        var s = Theme.Surface;
        var textX = min.X + box + UiMetrics.Px(6f);
        var textY = min.Y + MathF.Max(0f, (box - line) * 0.5f);
        const string Count = "65%";
        float countWidth;
        if (art)
        {
            using var numeral = Typography.Numeral(Count);
            countWidth = ImGui.CalcTextSize(Count).X;
            dl.AddText(new Vector2(min.X + width - countWidth, textY), Theme.U32(s.TextSecondary), Count);
        }
        else
        {
            countWidth = ImGui.CalcTextSize(Count).X;
            dl.AddText(new Vector2(min.X + width - countWidth, textY), Theme.U32(s.TextSecondary), Count);
        }

        Chrome.EllipsisTextAt(dl, new Vector2(textX, textY), MathF.Max(0f, min.X + width - countWidth - UiMetrics.Px(6f) - textX), Strings.CharactersAllQuests, Theme.U32(s.Text));
        if (!art)
        {
            return;
        }

        // The road under the row: the walked part gold, the rest the track (solid colours under high contrast).
        var y = min.Y + box + road;
        var walked = textX + ((min.X + width - textX) * Fraction);
        var highContrast = Theme.Glyphs.HighContrast;
        dl.AddRectFilled(new Vector2(textX, y), new Vector2(min.X + width, y + road), highContrast ? Theme.VeilLineU32 : Theme.NightLineU32);
        if (highContrast)
        {
            dl.AddRectFilled(new Vector2(textX, y), new Vector2(walked, y + road), Theme.MoonU32);
        }
        else
        {
            dl.AddRectFilledMultiColor(new Vector2(textX, y), new Vector2(walked, y + road), Theme.MoonDeepU32, Theme.MoonU32, Theme.MoonU32, Theme.MoonDeepU32);
        }
    }

    private void FlairRadio(string label, Flair flair)
    {
        Chrome.SameLineOrWrap(RadioWidth(label));
        if (ImGui.RadioButton(label, settings.Flair == flair))
        {
            settings.Flair = flair;
            Save();
        }

        HintOnHover(Strings.ConfigFlairHint);
    }

    /// <summary>
    /// Settings › Keyboard (T17, accessibility A7): what is always bound, the warning that the game sees the keys too,
    /// and the opt-in shortcuts, all off by default. Saved at once.
    /// </summary>
    private void DrawKeyboard()
    {
        Header(Strings.ConfigSectionKeyboard);
        using (ImRaii.TextWrapPos(0f))
        {
            Chrome.Hint(Strings.ConfigKeyboardAlwaysOn);
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextWrapped(Strings.ConfigKeyboardGameSeesKeys);
            }
        }

        var tabs = settings.ShortcutTabs;
        if (ImGui.Checkbox(Strings.ConfigShortcutTabs, ref tabs))
        {
            settings.ShortcutTabs = tabs;
            Save();
        }

        HintOnHover(Strings.ConfigShortcutTabsHint);

        var flag = settings.ShortcutFlag;
        if (ImGui.Checkbox(Strings.ConfigShortcutFlag, ref flag))
        {
            settings.ShortcutFlag = flag;
            Save();
        }

        HintOnHover(Strings.ConfigShortcutFlagHint);

        var reveal = settings.ShortcutReveal;
        if (ImGui.Checkbox(Strings.ConfigShortcutReveal, ref reveal))
        {
            settings.ShortcutReveal = reveal;
            Save();
        }

        HintOnHover(Strings.ConfigShortcutRevealHint);

        var pin = settings.ShortcutPin;
        if (ImGui.Checkbox(Strings.ConfigShortcutPin, ref pin))
        {
            settings.ShortcutPin = pin;
            Save();
        }

        HintOnHover(Strings.ConfigShortcutPinHint);
    }

    /// <summary>
    /// Settings › Display › Plugin language (V2-19): follow Dalamud (the default) or English; the pseudo-localization
    /// layout check shows only while Shift is held (or while it is on). Applied at once. A draft translation says so,
    /// with its coverage; a translation whose resource file did not load says that instead.
    /// </summary>
    private void DrawLanguage()
    {
        var dalamud = Loc.Resolve(Language?.DalamudLanguage);
        if (followLabelLanguage != Loc.Version || !string.Equals(followLabelDalamud, dalamud, StringComparison.Ordinal))
        {
            followLabelLanguage = Loc.Version;
            followLabelDalamud = dalamud;
            followLabel = string.Format(CultureInfo.CurrentCulture, Strings.ConfigLanguageFollowFormat, Loc.NativeName(dalamud));
        }

        // Each choice moves to the next line when it would run past the edge (French at the 400 px minimum width).
        ImGui.TextUnformatted(Strings.ConfigLanguage);
        Chrome.SameLineOrWrap(RadioWidth(followLabel));
        if (ImGui.RadioButton(followLabel, settings.PluginLanguage == PluginLanguage.FollowDalamud))
        {
            SetLanguage(PluginLanguage.FollowDalamud);
        }

        HintOnHover(Strings.ConfigLanguageHint);
        var english = Strings.ConfigLanguageEnglish;
        Chrome.SameLineOrWrap(RadioWidth(english));
        if (ImGui.RadioButton(english, settings.PluginLanguage == PluginLanguage.English))
        {
            SetLanguage(PluginLanguage.English);
        }

        HintOnHover(Strings.ConfigLanguageHint);
        if (settings.PluginLanguage == PluginLanguage.Pseudo || ImGui.GetIO().KeyShift)
        {
            var pseudo = Strings.ConfigLanguagePseudo;
            Chrome.SameLineOrWrap(RadioWidth(pseudo));
            if (ImGui.RadioButton(pseudo, settings.PluginLanguage == PluginLanguage.Pseudo))
            {
                SetLanguage(PluginLanguage.Pseudo);
            }
        }

        if (Loc.Language is Loc.Japanese or Loc.German or Loc.French)
        {
            if (Loc.TranslatedCount == 0)
            {
                Chrome.Hint(Strings.ConfigLanguageNotLoaded);
            }
            else if (Loc.IsDraft)
            {
                if (languageNoteVersion != Loc.Version)
                {
                    languageNoteVersion = Loc.Version;
                    var percent = Loc.KeyCount == 0 ? 0 : (int)Math.Floor(100.0 * Loc.TranslatedCount / Loc.KeyCount);
                    languageNote = string.Format(CultureInfo.CurrentCulture, Strings.ConfigLanguageDraftFormat, Loc.NativeName(Loc.Language), percent);
                }

                Chrome.Hint(languageNote);
            }
        }
    }

    private int languageNoteVersion = -1;
    private string languageNote = string.Empty;

    // "Follow Dalamud (Deutsch)", rebuilt when the plugin's or Dalamud's language changes.
    private int followLabelLanguage = -1;
    private string? followLabelDalamud;
    private string followLabel = string.Empty;

    /// <summary>A setting slider's width: 220 px, or the room left when the window is narrower.</summary>
    private static float SliderWidth() => Chrome.FitWidth(220f * ImGuiHelpers.GlobalScale);

    /// <summary>The Moonlit verdicts table's column plan (<see cref="PaneFit.VerdictColumns"/>).</summary>
    private readonly ColumnFit verdictColumns = new(5);

    /// <summary>A radio button's width: the circle, the inner spacing and the label.</summary>
    private static float RadioWidth(string label) =>
        ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.CalcTextSize(label).X;

    private void SetLanguage(PluginLanguage language)
    {
        if (settings.PluginLanguage == language)
        {
            return;
        }

        settings.PluginLanguage = language;
        Save();
        if (Language is not { } service)
        {
            return;
        }

        // The switch renames every window and raises the session's change events: done on the next framework tick,
        // outside this ImGui frame, when the plugin provides one.
        if (RunNextTick is { } defer)
        {
            defer(service.Apply);
        }
        else
        {
            service.Apply();
        }
    }

    /// <summary>Runs an action on the next framework tick, outside the ImGui frame; set by the plugin. Null runs it at once.</summary>
    public Action<Action>? RunNextTick { get; set; }

    /// <summary>The last item's hint as a Night tooltip while it is hovered.</summary>
    private static void HintOnHover(string hint)
    {
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(hint);
        }
    }

    /// <summary>States in the order the glyph palette preview shows them (the Help legend's order).</summary>
    private static readonly QuestState[] PalettePreviewStates =
    [
        QuestState.Completed, QuestState.Accepted, QuestState.Ready, QuestState.ReadyOnOtherJob,
        QuestState.DoneThisCycle, QuestState.Blocked, QuestState.Foreclosed, QuestState.Unknown,
    ];

    /// <summary>
    /// Settings › Display › Glyph palette (accessibility panel §2.2): Standard or High contrast, saved at once and
    /// applied from the next frame (<see cref="Theme.Refresh"/>), with the eight state moons drawn beside it in the
    /// palette in effect so the choice can be seen before closing the window. Each moon's tooltip names its shape.
    /// </summary>
    private void DrawGlyphPalette()
    {
        ImGui.TextUnformatted(Strings.ConfigGlyphPalette);
        Chrome.SameLineOrWrap(RadioWidth(Strings.ConfigGlyphPaletteStandard));
        if (ImGui.RadioButton(Strings.ConfigGlyphPaletteStandard, settings.GlyphPalette == GlyphPaletteKind.Standard))
        {
            settings.GlyphPalette = GlyphPaletteKind.Standard;
            Save();
        }

        HintOnHover(Strings.ConfigGlyphPaletteHint);
        Chrome.SameLineOrWrap(RadioWidth(Strings.ConfigGlyphPaletteHighContrast));
        if (ImGui.RadioButton(Strings.ConfigGlyphPaletteHighContrast, settings.GlyphPalette == GlyphPaletteKind.HighContrast))
        {
            settings.GlyphPalette = GlyphPaletteKind.HighContrast;
            Save();
        }

        HintOnHover(Strings.ConfigGlyphPaletteHint);

        var glyph = UiMetrics.InlineGlyphSize(ImGui.GetTextLineHeight());
        var gap = 4f * ImGuiHelpers.GlobalScale;
        for (var i = 0; i < PalettePreviewStates.Length; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine(0f, gap);
            }

            MoonGlyph.DrawInline(PalettePreviewStates[i], glyph);
            HintOnHover(Strings.StateTooltip(PalettePreviewStates[i]));
        }

        ImGui.SameLine(0f, gap * 2f);
        Marks.DrawInline(Mark.Check, glyph);
        ImGui.SameLine(0f, gap);
        Marks.DrawInline(Mark.Cross, glyph);
        ImGui.SameLine(0f, gap * 2f);
        MoonGlyph.DrawHaloInline(0.6f, MathF.Max(glyph, 16f * ImGuiHelpers.GlobalScale));
    }

    /// <summary>
    /// The journal filing radio: Refiled (the 0.6.1 rules) or Legacy (the genre-less quests stay in the removed
    /// bucket, as before). Saved at once; the plugin rebuilds the catalog through <see cref="JournalFilingChanged"/>.
    /// </summary>
    private void DrawJournalFiling()
    {
        ImGui.Spacing();
        ImGui.TextUnformatted(Strings.ConfigJournalFiling);
        var filing = settings.JournalFiling;
        var changed = false;
        if (ImGui.RadioButton(Strings.ConfigJournalFilingRefiled, filing == JournalFiling.Refiled))
        {
            filing = JournalFiling.Refiled;
            changed = true;
        }

        Chrome.SameLineOrWrap(RadioWidth(Strings.ConfigJournalFilingLegacy));
        if (ImGui.RadioButton(Strings.ConfigJournalFilingLegacy, filing == JournalFiling.Legacy))
        {
            filing = JournalFiling.Legacy;
            changed = true;
        }

        if (changed && filing != settings.JournalFiling)
        {
            settings.JournalFiling = filing;
            Save();
            JournalFilingChanged?.Invoke(filing);
        }

        Chrome.Hint(Strings.ConfigJournalFilingHint);
    }

    private void SaveWhenReleased()
    {
        if (scaleDirty && ImGui.IsItemDeactivatedAfterEdit())
        {
            scaleDirty = false;
            Save();
        }
    }

    private void DrawHelp()
    {
        Header(Strings.ConfigSectionHelp);
        if (StartTutorial is { } startTutorial)
        {
            if (ImGui.Button(Strings.ConfigStartTutorial))
            {
                startTutorial();
            }

            ImGui.SameLine();
        }

        if (ShowHelp is { } showHelp)
        {
            if (ImGui.Button(Strings.ConfigShowHelp))
            {
                showHelp();
            }
        }

        var offer = !settings.TutorialCompleted && settings.TutorialLaterCount < TutorialOverlay.LaterLimit;
        if (ImGui.Checkbox(Strings.ConfigOfferTutorial, ref offer))
        {
            settings.TutorialCompleted = !offer;
            settings.TutorialLaterCount = 0;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ConfigOfferTutorialHint);
        }
    }

    /// <summary>"Last poll … · average … · n polls" and the slider hint, once per completed poll.</summary>
    private void RefreshPollTiming()
    {
        var count = session.PollCount;
        if (count == pollTimingCount && pollTimingLanguage == Loc.Version)
        {
            return;
        }

        pollTimingCount = count;
        pollTimingLanguage = Loc.Version;
        if (count == 0)
        {
            pollTimingLine = Strings.ConfigPollTimingNone;
            pollCostLine = Strings.ConfigPollCostUnknown;
            return;
        }

        pollTimingLine = string.Format(CultureInfo.InvariantCulture, Strings.ConfigPollTimingFormat, session.LastPollMs, session.AveragePollMs, count);
        pollCostLine = string.Format(CultureInfo.InvariantCulture, Strings.ConfigPollCostFormat, session.AveragePollMs);
    }

    private bool welcomeBackDaysDirty;

    private void DrawNotices()
    {
        Header(Strings.ConfigSectionNotices);
        var notice = settings.ChatNoticeNewlyAvailable;
        if (ImGui.Checkbox(Strings.ConfigChatNotice, ref notice))
        {
            settings.ChatNoticeNewlyAvailable = notice;
            Save();
        }

        using (ImRaii.PushIndent())
        using (ImRaii.Disabled(!notice))
        {
            var msq = settings.IncludeMsqInNotices;
            if (ImGui.Checkbox(Strings.ConfigIncludeMsq, ref msq))
            {
                settings.IncludeMsqInNotices = msq;
                Save();
            }
        }

        var nudge = settings.JobQuestNudge;
        if (ImGui.Checkbox(Strings.JobsConfigNudge, ref nudge))
        {
            settings.JobQuestNudge = nudge;
            Save();
        }

        var abandoned = settings.ChatNoticeAbandoned;
        if (ImGui.Checkbox(Strings.AbandonedConfigNotice, ref abandoned))
        {
            settings.ChatNoticeAbandoned = abandoned;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.AbandonedConfigNoticeHint);
        }

        var seasonal = settings.ChatNoticeSeasonal;
        if (ImGui.Checkbox(Strings.SeasonalConfigNotice, ref seasonal))
        {
            settings.ChatNoticeSeasonal = seasonal;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.SeasonalConfigNoticeHint);
        }

        using (ImRaii.Disabled(!settings.ShowPayoffGates))
        {
            var payoff = settings.ChatNoticePayoffGates;
            if (ImGui.Checkbox(Strings.PayoffConfigNotice, ref payoff))
            {
                settings.ChatNoticePayoffGates = payoff;
                Save();
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.PayoffConfigNoticeHint);
        }

        DrawWelcomeBackDays();
    }

    /// <summary>"Show 'Since you were away' after N days" (P7): 0 turns the card off; saved when the slider is let go.</summary>
    private void DrawWelcomeBackDays()
    {
        var days = Math.Clamp(settings.WelcomeBackDays, 0, Core.Return.WelcomeBackTrigger.MaxDays);
        ImGui.SetNextItemWidth(Chrome.FitWidth(160f * ImGuiHelpers.GlobalScale));
        if (ImGui.SliderInt("##welcomeBackDays", ref days, 0, Core.Return.WelcomeBackTrigger.MaxDays, days == 0 ? Strings.WelcomeBackConfigOff : Strings.WelcomeBackConfigDaysFormat, ImGuiSliderFlags.AlwaysClamp))
        {
            settings.WelcomeBackDays = days;
            welcomeBackDaysDirty = true;
        }

        if (welcomeBackDaysDirty && ImGui.IsItemDeactivatedAfterEdit())
        {
            welcomeBackDaysDirty = false;
            Save();
        }

        HintOnHover(Strings.WelcomeBackConfigHint);

        // The label beside the slider, or under it wrapped between words when it would run past the edge.
        Chrome.TrailingLabel(Strings.WelcomeBackConfigDays);
        HintOnHover(Strings.WelcomeBackConfigHint);
    }

    private void DrawJournal()
    {
        Header(Strings.ConfigSectionJournal);
        var unlisted = settings.ShowUnlisted;
        if (ImGui.Checkbox(Strings.ConfigShowUnlisted, ref unlisted))
        {
            settings.ShowUnlisted = unlisted;
            Save();
            onShowUnlistedChanged(unlisted);
        }

        Chrome.Hint(Strings.ConfigShowUnlistedHint);
    }

    /// <summary>
    /// Spoilers (T19): hide main scenario names ahead of the character, how many quests ahead keep their names (saved
    /// when the slider is released), hide journal artwork until a quest is in the journal, whether "Before you
    /// continue" notes show (P5), and an override for the character shown. Every change bumps the session so each surface re-reads the mask at once.
    /// </summary>
    private void DrawSpoilers()
    {
        Header(Strings.SettingsSpoilers);
        var hideNames = settings.SpoilerHideMsqNames;
        if (ImGui.Checkbox(Strings.SpoilerHideNames, ref hideNames))
        {
            settings.SpoilerHideMsqNames = hideNames;
            SpoilersChanged();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.SpoilerHideNamesHelp);
        }

        // The slider applies whenever the viewed character's effective options hide names: the global setting, or
        // that character's override (Always shield hides them even with the global setting off). Mirrors
        // Configuration.SpoilerOptionsFor without building the options record each frame.
        var effectiveHide = session.ViewedContentId is { } viewedId && settings.SpoilerShieldByCharacter.TryGetValue(viewedId, out var shielded)
            ? shielded
            : hideNames;
        using (ImRaii.PushIndent())
        using (ImRaii.Disabled(!effectiveHide))
        {
            var ahead = Math.Clamp(settings.SpoilerRevealAhead, 0, SpoilerOptions.MaxAhead);
            bool moved;
            ImGui.SetNextItemWidth(Chrome.FitWidth(160f * ImGuiHelpers.GlobalScale));
            using (ImRaii.PushId(Strings.SpoilerAhead))
            {
                moved = ImGui.SliderInt("##slider", ref ahead, 0, SpoilerOptions.MaxAhead, "%d", ImGuiSliderFlags.AlwaysClamp);
            }

            if (moved)
            {
                settings.SpoilerRevealAhead = ahead;
                spoilerAheadDirty = true;
                session.RefreshSpoilers();
            }

            if (spoilerAheadDirty && ImGui.IsItemDeactivatedAfterEdit())
            {
                spoilerAheadDirty = false;
                Save();
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.SpoilerAheadHelp);
            }

            Chrome.TrailingLabel(Strings.SpoilerAhead);
            HintOnHover(Strings.SpoilerAheadHelp);
        }

        var hideArtwork = settings.SpoilerHideArtwork;
        if (ImGui.Checkbox(Strings.SpoilerHideArtwork, ref hideArtwork))
        {
            settings.SpoilerHideArtwork = hideArtwork;
            SpoilersChanged();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.SpoilerHideArtworkHelp);
        }

        var payoffNotes = settings.ShowPayoffGates;
        if (ImGui.Checkbox(Strings.PayoffConfigShow, ref payoffNotes))
        {
            settings.ShowPayoffGates = payoffNotes;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.PayoffConfigShowHint);
        }

        DrawSpoilerOverride();
        if (spoilerCountVersion != session.Version)
        {
            spoilerCountVersion = session.Version;
            spoilerCountLine = session.Bundle is null
                ? string.Empty
                : string.Format(CultureInfo.CurrentCulture, Strings.SpoilerMaskedCountFormat, session.Spoilers.MaskedCount);
        }

        if (spoilerCountLine.Length > 0)
        {
            Chrome.Hint(spoilerCountLine);
        }
    }

    /// <summary>The viewed character's own shield: follow the settings, always shield, or show everything.</summary>
    private void DrawSpoilerOverride()
    {
        if (session.ViewedContentId is not { } contentId)
        {
            Chrome.Hint(Strings.SpoilerCharacterNone);
            return;
        }

        var name = session.ViewedSnapshot?.Name;
        if (spoilerLabel.Length == 0 || spoilerLabelContentId != contentId || !string.Equals(spoilerLabelName, name, StringComparison.Ordinal))
        {
            spoilerLabelContentId = contentId;
            spoilerLabelName = name;
            spoilerLabel = string.IsNullOrEmpty(name) ? Strings.SpoilerCharacterLabel : string.Format(CultureInfo.CurrentCulture, Strings.SpoilerCharacterFormat, name);
        }

        ImGui.TextUnformatted(spoilerLabel);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.SpoilerCharacterHelp);
        }

        var current = settings.SpoilerShieldByCharacter.TryGetValue(contentId, out var shielded) ? (shielded ? 1 : 2) : 0;
        using var indent = ImRaii.PushIndent();
        var choice = current;
        if (ImGui.RadioButton(Strings.SpoilerCharacterDefault, current == 0))
        {
            choice = 0;
        }

        Chrome.SameLineOrWrap(RadioWidth(Strings.SpoilerCharacterOn));
        if (ImGui.RadioButton(Strings.SpoilerCharacterOn, current == 1))
        {
            choice = 1;
        }

        Chrome.SameLineOrWrap(RadioWidth(Strings.SpoilerCharacterOff));
        if (ImGui.RadioButton(Strings.SpoilerCharacterOff, current == 2))
        {
            choice = 2;
        }

        if (choice == current)
        {
            return;
        }

        if (choice == 0)
        {
            settings.SpoilerShieldByCharacter.Remove(contentId);
        }
        else
        {
            settings.SpoilerShieldByCharacter[contentId] = choice == 1;
        }

        SpoilersChanged();
    }

    /// <summary>Saves a spoiler setting and makes every surface re-read the mask.</summary>
    private void SpoilersChanged()
    {
        Save();
        session.RefreshSpoilers();
    }

    /// <summary>The Todo overlay's "Clear my blues" section (P3): its toggle, which expansion is pinned and Unpin.</summary>
    private void DrawTodoPlanToggle()
    {
        var plan = settings.TodoShowPlan;
        if (ImGui.Checkbox(Strings.PlanTodoConfig, ref plan))
        {
            settings.TodoShowPlan = plan;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.PlanTodoConfigHint);
        }

        var expansion = settings.TodoPlanExpansion;
        var pinned = expansion is >= 0 and <= byte.MaxValue;
        if (expansion != planPinnedExpansion || !ReferenceEquals(session.Names, planPinnedNames) || planPinnedLanguage != Loc.Version)
        {
            planPinnedExpansion = expansion;
            planPinnedLanguage = Loc.Version;
            planPinnedNames = session.Names;
            planPinnedLabel = pinned
                ? string.Format(CultureInfo.CurrentCulture, Strings.PlanTodoConfigPinnedFormat, session.Names.Expansion((byte)expansion))
                : Strings.PlanTodoConfigNone;
        }

        Chrome.SameLineOrWrap(ImGui.CalcTextSize(planPinnedLabel).X);
        Chrome.Hint(planPinnedLabel);
        if (!pinned)
        {
            return;
        }

        Chrome.SameLineOrWrap(ImGui.CalcTextSize(UnpinPlanLabel.Value).X + (ImGui.GetStyle().FramePadding.X * 2f));
        if (ImGui.SmallButton(UnpinPlanLabel.Value))
        {
            settings.TodoPlanExpansion = -1;
            Save();
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.PlanUnpinTooltip);
        }
    }

    /// <summary>
    /// The Todo overlay: on/off, lock, compact mode, an opacity slider (saved when released), the section toggles and a
    /// "Reset position" button. The overlay reads the configuration every frame, so every change shows at once.
    /// </summary>
    private void DrawTodoOverlay()
    {
        Header(Strings.TodoConfigSection);
        var enabled = settings.TodoOverlayEnabled;
        if (ImGui.Checkbox(Strings.TodoConfigEnabled, ref enabled))
        {
            settings.TodoOverlayEnabled = enabled;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.TodoConfigEnabledHint);
        }

        using var indent = ImRaii.PushIndent();
        using var disabled = ImRaii.Disabled(!enabled);

        var locked = settings.TodoOverlayLocked;
        if (ImGui.Checkbox(Strings.TodoConfigLocked, ref locked))
        {
            settings.TodoOverlayLocked = locked;
            if (!locked)
            {
                // Unlocked: the upgrade notice has done its job.
                settings.TodoLockNoticeDue = false;
            }

            Save();
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.TodoConfigLockedHint);
        }

        if (settings.TodoLockNoticeDue && settings.TodoOverlayLocked)
        {
            using var accent = Theme.PushText(Theme.Accent);
            ImGui.TextWrapped(Strings.TodoLockUpgradeNotice);
        }

        var compact = settings.TodoOverlayCompact;
        if (ImGui.Checkbox(Strings.TodoConfigCompact, ref compact))
        {
            settings.TodoOverlayCompact = compact;
            Save();
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.TodoConfigCompactHint);
        }

        var opacity = TodoOverlay.ClampOpacity(settings.TodoOverlayOpacity);
        bool moved;
        ImGui.SetNextItemWidth(SliderWidth());
        using (ImRaii.PushId(Strings.TodoConfigOpacity))
        {
            moved = ImGui.SliderFloat("##slider", ref opacity, TodoOverlay.MinOpacity, TodoOverlay.MaxOpacity, "%.2f", ImGuiSliderFlags.AlwaysClamp);
        }

        if (moved)
        {
            settings.TodoOverlayOpacity = opacity;
            todoDirty = true;
        }

        if (todoDirty && ImGui.IsItemDeactivatedAfterEdit())
        {
            todoDirty = false;
            Save();
        }

        Chrome.TrailingLabel(Strings.TodoConfigOpacity);

        ImGui.TextDisabled(Strings.TodoConfigSectionsLabel);
        var pins = settings.TodoShowPins;
        if (ImGui.Checkbox(Strings.TodoConfigShowPins, ref pins))
        {
            settings.TodoShowPins = pins;
            Save();
        }

        var seasonal = settings.TodoShowSeasonal;
        if (ImGui.Checkbox(Strings.TodoConfigShowSeasonal, ref seasonal))
        {
            settings.TodoShowSeasonal = seasonal;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.TodoConfigShowSeasonalHint);
        }

        DrawTodoPlanToggle();

        var nearby = settings.TodoShowNearbyFeature;
        if (ImGui.Checkbox(Strings.TodoConfigShowNearby, ref nearby))
        {
            settings.TodoShowNearbyFeature = nearby;
            Save();
        }

        var msq = settings.TodoShowMsq;
        if (ImGui.Checkbox(Strings.TodoConfigShowMsq, ref msq))
        {
            settings.TodoShowMsq = msq;
            Save();
        }

        var jobs = settings.TodoShowJobQuests;
        if (ImGui.Checkbox(Strings.TodoConfigShowJobQuests, ref jobs))
        {
            settings.TodoShowJobQuests = jobs;
            Save();
        }

        if (ResetTodoPosition is not { } reset)
        {
            return;
        }

        if (ImGui.Button(Strings.TodoConfigResetPosition))
        {
            reset();
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.TodoConfigResetPositionHint);
        }
    }

    /// <summary>Item hints: the hover hint and the context-menu entry. The feature itself listens through the two callbacks.</summary>
    private void DrawItemHints()
    {
        Header(Strings.ConfigSectionItemHints);
        var hints = settings.ItemHintsEnabled;
        if (ImGui.Checkbox(Strings.ConfigItemHints, ref hints))
        {
            settings.ItemHintsEnabled = hints;
            Save();
            ItemHintsToggled?.Invoke(hints);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ConfigItemHintsHint);
        }

        var contextMenu = settings.ItemContextMenuEnabled;
        if (ImGui.Checkbox(Strings.ConfigItemContextMenu, ref contextMenu))
        {
            settings.ItemContextMenuEnabled = contextMenu;
            Save();
            ItemContextMenuToggled?.Invoke(contextMenu);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ConfigItemContextMenuHint);
        }
    }

    private void DrawIntegrations()
    {
        Header(Strings.ConfigSectionIntegrations);
        var wotsit = settings.WotsitIntegration;
        if (ImGui.Checkbox(Strings.ConfigWotsitIntegration, ref wotsit))
        {
            settings.WotsitIntegration = wotsit;
            Save();
            WotsitToggled?.Invoke(wotsit);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ConfigWotsitIntegrationHint);
        }

        var npcMenu = settings.NpcContextMenuEnabled;
        if (ImGui.Checkbox(Strings.ConfigNpcContextMenu, ref npcMenu))
        {
            settings.NpcContextMenuEnabled = npcMenu;
            Save();
            NpcContextMenuToggled?.Invoke(npcMenu);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ConfigNpcContextMenuHint);
        }

        var dutyHint = settings.DutyFinderHintEnabled;
        if (ImGui.Checkbox(Strings.DutyHintSetting, ref dutyHint))
        {
            settings.DutyFinderHintEnabled = dutyHint;
            Save();
            DutyFinderHintToggled?.Invoke(dutyHint);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.DutyHintSettingHint);
        }

        // Read per use by the detail pane, so no callback is needed.
        var questionable = settings.QuestionableHandoff;
        if (ImGui.Checkbox(Strings.ConfigQuestionableHandoff, ref questionable))
        {
            settings.QuestionableHandoff = questionable;
            Save();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ConfigQuestionableHandoffHint);
        }

        DrawHookGate();
    }

    /// <summary>
    /// The addon kill switch (T20): the paused notice while the game hooks wait for a tested update, and "Enable game
    /// hooks on this untested version", scoped to the running game version, which re-registers them at once through
    /// <see cref="HookGate.Changed"/>.
    /// </summary>
    private void DrawHookGate()
    {
        if (HookGate is not { } gate)
        {
            return;
        }

        if (gate.IsPaused)
        {
            using var eclipse = Theme.PushText(Theme.Eclipse);
            ImGui.TextWrapped(Strings.HooksPausedNotice);
        }

        // Ticked only when the stored override names the running version: one from an earlier patch reads unticked.
        // Ticking stores the running version, unticking clears it. Without a known running version there is nothing
        // to store (and the gate already allows the hooks), so the box is disabled.
        var anyway = gate.EnableAnywayApplies;
        using (ImRaii.Disabled(gate.RunningVersion.Length == 0))
        {
            if (ImGui.Checkbox(Strings.HooksEnableUntested, ref anyway))
            {
                var version = anyway ? gate.RunningVersion : string.Empty;
                settings.EnableHooksOnUntestedVersion = version;
                Save();
                gate.SetEnableAnyway(version);
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.HooksEnableUntestedHint);
        }

        if (gate.Decision.Verdict == HookGateVerdict.Overridden)
        {
            Chrome.Hint(Strings.HooksRunningUntested);
        }
    }

    private void DrawData()
    {
        Header(Strings.ConfigSectionData);
        ImGui.TextWrapped(Strings.ConfigDataRetention);
        ImGui.Spacing();
        DrawVerdicts();
        ImGui.Spacing();
        if (Export is { } export)
        {
            export.Draw();
            ImGui.Spacing();
        }


        using (Theme.PushDestructiveButton())
        {
            if (ImGui.Button(Strings.ConfigDeleteAll))
            {
                ImGui.OpenPopup(Strings.ConfigDeleteStep1Popup);
            }
        }

        DrawDeleteConfirms();
        DrawToast();
    }

    /// <summary>
    /// "Your Moonlit verdicts (N)": quest, verdict, note and date per stored override with a Restore button each, and
    /// Restore all behind the same hold-to-confirm gate the verdict popups use. Restoring goes through the Moonlit
    /// pane, so its catalog rebuilds exactly as after a verdict.
    /// </summary>
    private void DrawVerdicts()
    {
        if (Overrides is not { } overrides)
        {
            ImGui.TextDisabled(Strings.ConfigVerdictsUnavailable);
            return;
        }

        RefreshVerdictRows(overrides);
        ImGui.TextUnformatted(verdictsHeader);
        if (verdictRows.Length == 0)
        {
            ImGui.TextDisabled(Strings.ConfigVerdictsNone);
            return;
        }

        // Restore always shows: the note hides first, then the date, and the quest and the note end in an ellipsis
        // (feature plan v4 L6).
        var style = ImGui.GetStyle();
        var restoreWidth = ImGui.CalcTextSize(Strings.ConfigVerdictRestore).X + (style.FramePadding.X * 2f);
        var verdictWidth = MathF.Max(ImGui.CalcTextSize(Strings.ConfigVerdictUnique).X, ImGui.CalcTextSize(Strings.ConfigVerdictNotUnique).X);
        Span<ColumnSpec> specs = stackalloc ColumnSpec[5];
        PaneFit.VerdictColumns(UiMetrics.Px(LayoutBudgets.RowNameMinLogical), verdictWidth, ImGui.CalcTextSize("0000-00-00").X, restoreWidth, specs);
        ColumnFit.FitHeader(specs, 1, Strings.ConfigVerdictColumnVerdict);
        ColumnFit.FitHeader(specs, 3, Strings.ConfigVerdictColumnDate);
        verdictColumns.Plan(ImGui.GetContentRegionAvail().X, specs);
        const ImGuiTableFlags Flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingFixedFit;
        using (var table = verdictColumns.Begin("##verdicts", Flags))
        {
            if (table.Success)
            {
                verdictColumns.Setup(0, Strings.ConfigVerdictColumnQuest);
                verdictColumns.Setup(1, Strings.ConfigVerdictColumnVerdict);
                verdictColumns.Setup(2, Strings.ConfigVerdictColumnNote);
                verdictColumns.Setup(3, Strings.ConfigVerdictColumnDate);
                verdictColumns.Setup(4, Strings.ConfigVerdictColumnRestore, ImGuiTableColumnFlags.NoHeaderLabel);
                ImGui.TableHeadersRow();

                foreach (var row in verdictRows)
                {
                    using var id = ImRaii.PushId((int)row.RowId);
                    ImGui.TableNextRow();
                    if (verdictColumns.Next(0))
                    {
                        Chrome.FitText(row.QuestName, ImGui.GetColorU32(ImGuiCol.Text));
                    }

                    if (verdictColumns.Next(1))
                    {
                        using (Theme.PushText(row.Color))
                        {
                            ImGui.TextUnformatted(row.Verdict);
                        }
                    }

                    if (verdictColumns.Next(2))
                    {
                        Chrome.FitText(row.Note, ImGui.GetColorU32(ImGuiCol.Text));
                    }

                    if (verdictColumns.Next(3))
                    {
                        ImGui.TextDisabled(row.Date);
                    }

                    if (!verdictColumns.Next(4))
                    {
                        continue;
                    }

                    if (ImGui.SmallButton(Strings.ConfigVerdictRestore))
                    {
                        // The row cache refreshes next frame from the bumped version; the array is not touched here.
                        overrides.Clear(row.RowId);
                    }

                    if (ImGui.IsItemHovered())
                    {
                        UiMetrics.Tooltip(Strings.ConfigVerdictRestoreTooltip);
                    }
                }
            }
        }

        if (Chrome.HoldButton(RestoreAllLabel, restoreAllGate))
        {
            overrides.ClearAll();
            toast = Strings.ConfigVerdictsRestored;
            toastUntilUtc = DateTime.UtcNow + ToastDuration;
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ConfigVerdictRestoreAllTooltip);
        }
    }

    /// <summary>Rebuilds the verdict rows (sorted by quest name) when the overrides or the catalog changed.</summary>
    private int verdictLanguage = -1;

    private void RefreshVerdictRows(IUniqueOverrides overrides)
    {
        var bundle = session.Bundle;
        var spoilers = session.Spoilers;
        if (overrides.Version == verdictVersion && ReferenceEquals(bundle, verdictBundle) && spoilers.Fingerprint == verdictSpoilers && verdictLanguage == Localization.Loc.Version)
        {
            return;
        }

        verdictLanguage = Localization.Loc.Version;
        verdictVersion = overrides.Version;
        verdictBundle = bundle;
        verdictSpoilers = spoilers.Fingerprint;
        var all = overrides.All;
        var list = new List<VerdictRow>(all.Count);
        foreach (var (rowId, stored) in all)
        {
            var name = bundle?.Catalog.GetByRowId(rowId) is { } quest ? spoilers.DisplayName(quest) : string.Format(CultureInfo.InvariantCulture, Strings.MoonlitQuestFormat, rowId);
            list.Add(new VerdictRow(
                rowId,
                name,
                stored.Unique ? Strings.ConfigVerdictUnique : Strings.ConfigVerdictNotUnique,
                stored.Unique ? Theme.Silver : Theme.Dusk,
                stored.Note ?? string.Empty,
                stored.MarkedUtc?.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty));
        }

        list.Sort(static (a, b) => string.Compare(a.QuestName, b.QuestName, StringComparison.CurrentCultureIgnoreCase));
        verdictRows = list.ToArray();
        verdictsHeader = string.Format(CultureInfo.InvariantCulture, Strings.ConfigVerdictsHeaderFormat, verdictRows.Length);
    }

    /// <summary>Two modals in a row: the first explains, the second asks again; only the second deletes.</summary>
    private void DrawDeleteConfirms()
    {
        using (var first = ImRaii.PopupModal(Strings.ConfigDeleteStep1Popup, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (first)
            {
                ImGui.TextWrapped(Strings.ConfigDeleteStep1Text);
                ImGui.Spacing();
                if (ImGui.Button(Strings.ConfigDeleteContinue))
                {
                    openSecondConfirm = true;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();
                if (ImGui.Button(Strings.ConfigCancel))
                {
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        if (openSecondConfirm)
        {
            openSecondConfirm = false;
            ImGui.OpenPopup(Strings.ConfigDeleteStep2Popup);
        }

        using var second = ImRaii.PopupModal(Strings.ConfigDeleteStep2Popup, ImGuiWindowFlags.AlwaysAutoResize);
        if (!second)
        {
            return;
        }

        ImGui.TextWrapped(Strings.ConfigDeleteStep2Text);
        ImGui.Spacing();
        using (Theme.PushDestructiveButton())
        {
            if (ImGui.Button(Strings.ConfigDeleteConfirm))
            {
                session.DeleteAllData();
                toast = Strings.ConfigDeleteDone;
                toastUntilUtc = DateTime.UtcNow + ToastDuration;
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button(Strings.ConfigCancel))
        {
            ImGui.CloseCurrentPopup();
        }
    }

    private void DrawToast()
    {
        if (toast is null)
        {
            return;
        }

        if (DateTime.UtcNow >= toastUntilUtc)
        {
            toast = null;
            return;
        }

        using (Theme.PushText(Theme.Silver))
        {
            ImGui.TextUnformatted(toast);
        }
    }

    private void DrawAbout()
    {
        Header(Strings.ConfigSectionAbout);
        RefreshCatalogLine();
        ImGui.TextUnformatted(pluginVersionLine.Value);
        ImGui.TextUnformatted(dataStampLine);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.ConfigDataStampTooltip);
        }

        if (dataVersionWarning is { } warning)
        {
            using var eclipse = Theme.PushText(Theme.Eclipse);
            ImGui.TextWrapped(warning);
        }

        ImGui.TextUnformatted(curatedLine.Value);
        ImGui.TextUnformatted(catalogLine);
        RefreshPollTiming();
        ImGui.TextUnformatted(pollTimingLine);
    }

    /// <summary>Catalog language and size, rebuilt when the bundle (or the error) changes.</summary>
    private void RefreshCatalogLine()
    {
        var bundle = session.Bundle;
        if (bundle is not null)
        {
            if (!ReferenceEquals(bundle, aboutBundle) || aboutLanguage != Loc.Version)
            {
                aboutBundle = bundle;
                aboutLanguage = Loc.Version;
                catalogLine = string.Format(CultureInfo.CurrentCulture, Strings.ConfigCatalogFormat, bundle.Catalog.Count, bundle.Language);
            }

            return;
        }

        if (session.CatalogError is { } error)
        {
            if (error != catalogError || catalogErrorLanguage != Loc.Version)
            {
                catalogError = error;
                catalogErrorLanguage = Loc.Version;
                catalogLine = string.Format(CultureInfo.CurrentCulture, Strings.ConfigCatalogUnavailableFormat, error);
            }

            return;
        }

        catalogLine = Strings.ConfigCatalogLoading;
    }

    private static void Header(string title)
    {
        ImGui.TextDisabled(title);
        ImGui.Separator();
    }

    private void Save() => settings.Save(pluginInterface);

    private readonly record struct VerdictRow(uint RowId, string QuestName, string Verdict, Vector4 Color, string Note, string Date);
}
