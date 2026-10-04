using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui.Themes;

/// <summary>
/// The per-state mix's rules (plan v7 T10; spec-1.17 §A3, §A4 and the realism supervisor's rulings), over the shipped
/// numbers: cross-set pairs warn per vision mode, a set's own pairs never do, Ready must lead by 1.25× and Completed stay
/// at 0.80× of Ready at both tiers, and Fix it proposes one change that never touches the state just picked.
/// </summary>
public sealed class MixRulesTests
{
    private static readonly GlyphSetId[] Measured = [.. MixTable.Sets];

    private static GlyphSetId[] Pure(GlyphSetId set) => [.. Enumerable.Repeat(set, AppearanceStates.Count)];

    private static GlyphSetId[] With(GlyphSetId[] column, QuestState state, GlyphSetId set)
    {
        var copy = column.ToArray();
        copy[AppearanceStates.Index(state)] = set;
        return copy;
    }

    [Fact]
    public void A_column_from_one_set_never_warns()
    {
        foreach (var set in Measured)
        {
            var verdict = MixRules.Evaluate(Pure(set));
            Assert.False(verdict.Mixed);
            Assert.True(verdict.Measured);
            Assert.True(verdict.Ok);
            Assert.True(verdict.LeastLead >= MixTable.ReadyLeadBar, $"{set} leads {verdict.LeastLead}");
        }
    }

    [Fact]
    public void Ready_from_Ishgard_Glass_in_a_Medallion_look_stops_leading_at_large_sizes_and_Fix_it_keeps_the_pick()
    {
        var mix = With(Pure(GlyphSetId.Medallion), QuestState.Ready, GlyphSetId.IshgardGlass);
        var verdict = MixRules.Evaluate(mix);
        Assert.True(verdict.Mixed);
        Assert.Equal(1.20f, verdict.Lead(MixTier.Hero));
        Assert.Equal(1.28f, verdict.Lead(MixTier.Row));
        Assert.Equal(QuestState.Completed, verdict.Next(MixTier.Hero));
        Assert.Equal(0.83f, verdict.CompletedOfReady(MixTier.Hero));
        Assert.Equal([MixWarningKind.ReadyLead, MixWarningKind.CompletedRecedes], verdict.Warnings.Select(static w => w.Kind));
        Assert.Equal(MixTier.Hero, verdict.Warnings[0].Tier);
        Assert.Equal(QuestState.Completed, verdict.Warnings[0].B);

        // The notes: Ready's row names the louder state; Completed's row says it is almost as loud.
        Assert.Equal(MixWarningKind.ReadyLead, verdict.NoteFor(QuestState.Ready)!.Value.Kind);
        Assert.Equal(MixWarningKind.CompletedRecedes, verdict.NoteFor(QuestState.Completed)!.Value.Kind);
        Assert.Null(verdict.NoteFor(QuestState.Blocked));

        // Fix it keeps Ready's pick and takes Completed from the set Ready already uses ("… for Completed too").
        var fix = MixRules.Fix(verdict, GlyphSetId.Medallion, QuestState.Ready);
        Assert.NotNull(fix);
        Assert.Equal(QuestState.Completed, fix.State);
        Assert.Equal(GlyphSetId.Medallion, fix.From);
        Assert.Equal(GlyphSetId.IshgardGlass, fix.To);
        Assert.True(fix.ClearsAll);
        Assert.True(fix.After.Ok);
        Assert.True(fix.After.Lead(MixTier.Hero) >= MixTable.ReadyLeadBar);
    }

    [Fact]
    public void Cross_set_pairs_warn_per_vision_mode_and_name_the_mode()
    {
        // Ishgard Glass's Blocked beside Medallion's Locked out is under 12 in greyscale.
        var mix = With(Pure(GlyphSetId.Medallion), QuestState.Blocked, GlyphSetId.IshgardGlass);
        var verdict = MixRules.Evaluate(mix);
        var close = Assert.Single(verdict.Warnings);
        Assert.Equal(MixWarningKind.Close, close.Kind);
        Assert.Equal((QuestState.Blocked, QuestState.Foreclosed), (close.A, close.B));
        Assert.True(MixTable.Under(close.Value, MixTable.CloseBar(close.Mode)));
        Assert.False(MixTable.Under(close.Value, MixTable.HardBar));
        Assert.True(MixTable.TryPair(GlyphSetId.IshgardGlass, QuestState.Blocked, GlyphSetId.Medallion, QuestState.Foreclosed, close.Mode, out var recorded));
        Assert.Equal(recorded, close.Value);
        Assert.Equal(close, verdict.NoteFor(QuestState.Blocked));
        Assert.Equal(close, verdict.NoteFor(QuestState.Foreclosed));

        // Fix it never changes Blocked, the pick: it takes Locked out from Glass too.
        var fix = MixRules.Fix(verdict, GlyphSetId.Medallion, QuestState.Blocked);
        Assert.NotNull(fix);
        Assert.Equal((QuestState.Foreclosed, GlyphSetId.IshgardGlass), (fix.State, fix.To));
        Assert.True(fix.After.Ok);
    }

    [Fact]
    public void Every_warning_is_a_value_under_its_bar_and_only_cross_set_pairs_warn()
    {
        foreach (var column in OneChangeMixes())
        {
            var verdict = MixRules.Evaluate(column);
            foreach (var w in verdict.Warnings)
            {
                switch (w.Kind)
                {
                    case MixWarningKind.Hard:
                        Assert.True(MixTable.Under(w.Value, MixTable.HardBar));
                        Assert.NotEqual(verdict.SetFor(w.A), verdict.SetFor(w.B));
                        break;
                    case MixWarningKind.Close:
                        Assert.True(MixTable.Under(w.Value, MixTable.CloseBar(w.Mode)));
                        Assert.False(MixTable.Under(w.Value, MixTable.HardBar));
                        Assert.NotEqual(verdict.SetFor(w.A), verdict.SetFor(w.B));
                        break;
                    case MixWarningKind.ReadyLead:
                        Assert.True(MixTable.RatioUnder(w.Value, MixTable.ReadyLeadBar));
                        break;
                    default:
                        Assert.True(MixTable.RatioOver(w.Value, MixTable.CompletedOfReadyBar));
                        break;
                }
            }

            // A pair under its bar in some mode always warns when its moons come from two sets.
            var states = AppearanceStates.All;
            for (var i = 0; i < states.Count; i++)
            {
                for (var j = i + 1; j < states.Count; j++)
                {
                    var under = MixTable.Modes.Any(m => MixTable.TryPair(column[i], states[i], column[j], states[j], m, out var v) && MixTable.Under(v, MixTable.CloseBar(m)));
                    var warned = verdict.Warnings.Any(w => w.IsPair && w.A == states[i] && w.B == states[j]);
                    Assert.Equal(under && column[i] != column[j], warned);
                }
            }
        }
    }

    [Fact]
    public void A_sets_own_pair_never_warns_even_under_a_bar_of_another_mode()
    {
        // Medallion's Blocked and Locked out read 11.1 under Machado protanopia: apart at its bar of 11, and inside one set.
        Assert.True(MixTable.TryPair(GlyphSetId.Medallion, QuestState.Blocked, GlyphSetId.Medallion, QuestState.Foreclosed, VisionMode.MachadoProt, out var own));
        Assert.True(own < 12f);
        var mix = With(Pure(GlyphSetId.Medallion), QuestState.Ready, GlyphSetId.AetherCrystal);
        var verdict = MixRules.Evaluate(mix);
        Assert.DoesNotContain(verdict.Warnings, static w => w.IsPair && w.A == QuestState.Blocked && w.B == QuestState.Foreclosed);

        // The heat table reads it as the set's gates did.
        Assert.True(MixRules.TryHeat(mix, QuestState.Foreclosed, QuestState.Blocked, VisionMode.MachadoProt, out var cell));
        Assert.Equal(PairReading.Apart, cell.Reading);
        Assert.False(cell.CrossSet);
    }

    [Fact]
    public void Fix_it_never_touches_the_kept_state_and_its_change_clears_everything()
    {
        var proposals = 0;
        foreach (var column in OneChangeMixes())
        {
            var verdict = MixRules.Evaluate(column);
            if (verdict.Ok)
            {
                Assert.Null(MixRules.Fix(verdict, column[1], null));
                continue;
            }

            foreach (var keep in AppearanceStates.All)
            {
                var fix = MixRules.Fix(verdict, column[^1], keep);
                if (fix is null)
                {
                    continue;
                }

                proposals++;
                Assert.NotEqual(keep, fix.State);
                Assert.Equal(verdict.SetFor(fix.State), fix.From);
                Assert.NotEqual(fix.From, fix.To);
                if (fix.ClearsAll)
                {
                    Assert.True(fix.After.Ok);
                    Assert.Contains(MixRules.Choices, c => c.Id == fix.To);
                }
            }
        }

        Assert.True(proposals > 0);
    }

    [Fact]
    public void Fix_it_prefers_the_set_already_used_most_then_the_earliest_state()
    {
        // Ready from Glass in a Medallion look: Completed from Glass (covering two) beats any one-state set.
        var verdict = MixRules.Evaluate(With(Pure(GlyphSetId.Medallion), QuestState.Ready, GlyphSetId.IshgardGlass));
        var fix = MixRules.Fix(verdict, GlyphSetId.Medallion, QuestState.Ready)!;
        var cover = fix.After.Column.Count(s => s == fix.To);
        foreach (var state in AppearanceStates.All.Where(static s => s != QuestState.Ready))
        {
            foreach (var choice in MixRules.Choices.Where(c => c.Id != verdict.SetFor(state)))
            {
                var after = MixRules.WhatIf(verdict.Column, state, choice.Id);
                if (after.Ok)
                {
                    var c = after.Column.Count(s => s == choice.Id);
                    Assert.True(c < cover || (c == cover && AppearanceStates.Index(state) >= AppearanceStates.Index(fix.State)), $"{state} from {choice.Id} covers {c}");
                }
            }
        }
    }

    [Fact]
    public void When_no_single_change_clears_it_Fix_it_offers_the_themes_own_moon_or_nothing()
    {
        // An Ishgard Glass look with Locked out from Medallion: close to both Blocked and Not checked, and the pick stays.
        var verdict = MixRules.Evaluate(With(Pure(GlyphSetId.IshgardGlass), QuestState.Foreclosed, GlyphSetId.Medallion));
        Assert.Equal(2, verdict.Warnings.Count(static w => w.Kind == MixWarningKind.Close));
        Assert.Null(MixRules.Fix(verdict, GlyphSetId.IshgardGlass, QuestState.Foreclosed));

        // With no pick to keep, the theme's own Locked out clears it in one change.
        var free = MixRules.Fix(verdict, GlyphSetId.IshgardGlass, null);
        Assert.NotNull(free);
        Assert.True(free.ClearsAll);

        // Two problems no one change clears (Ready from Glass loses the lead, Blocked from Glass is close to Locked out),
        // keeping Ready: the theme's own moon for the state the first warning names, Blocked.
        var both = With(With(Pure(GlyphSetId.Medallion), QuestState.Ready, GlyphSetId.IshgardGlass), QuestState.Blocked, GlyphSetId.IshgardGlass);
        var bothVerdict = MixRules.Evaluate(both);
        Assert.Equal(MixWarningKind.Close, bothVerdict.Warnings[0].Kind);
        Assert.Contains(bothVerdict.Warnings, static w => w.Kind == MixWarningKind.ReadyLead);
        var fallback = MixRules.Fix(bothVerdict, GlyphSetId.Medallion, QuestState.Ready);
        Assert.NotNull(fallback);
        Assert.False(fallback.ClearsAll);
        Assert.Equal((QuestState.Blocked, GlyphSetId.IshgardGlass, GlyphSetId.Medallion), (fallback.State, fallback.From, fallback.To));
        Assert.False(fallback.After.Ok);
    }

    [Fact]
    public void An_option_says_what_it_would_add_before_it_is_picked()
    {
        var medallion = MixRules.Evaluate(Pure(GlyphSetId.Medallion));
        var added = MixRules.Added(medallion, MixRules.WhatIf(medallion.Column, QuestState.Ready, GlyphSetId.IshgardGlass));
        Assert.NotNull(added);
        Assert.Equal(MixWarningKind.ReadyLead, added.Value.Kind);
        Assert.Equal(MixTier.Hero, added.Value.Tier);

        var close = MixRules.Added(medallion, MixRules.WhatIf(medallion.Column, QuestState.Blocked, GlyphSetId.IshgardGlass));
        Assert.Equal(MixWarningKind.Close, close!.Value.Kind);

        // What is already warned about is not news; an option that changes nothing adds nothing.
        var mixed = MixRules.Evaluate(With(Pure(GlyphSetId.Medallion), QuestState.Ready, GlyphSetId.IshgardGlass));
        Assert.Null(MixRules.Added(mixed, MixRules.WhatIf(mixed.Column, QuestState.Blocked, GlyphSetId.Medallion)));
        Assert.Null(MixRules.Added(medallion, MixRules.WhatIf(medallion.Column, QuestState.Ready, GlyphSetId.AetherCrystal)));
    }

    [Fact]
    public void The_lists_offer_every_measured_mixable_set_and_never_Classic()
    {
        // Sumi to Kinpaku joins with its measured numbers (1.17 T15).
        var offered = MixRules.Choices.Select(static c => c.Id).ToArray();
        Assert.Equal([GlyphSetId.Medallion, GlyphSetId.AetherCrystal, GlyphSetId.IshgardGlass, GlyphSetId.Orrery, GlyphSetId.Sumi], offered.Order());
        Assert.DoesNotContain(GlyphSetId.Classic, offered);
    }

    [Fact]
    public void High_contrast_and_Classic_are_not_mixes()
    {
        Assert.False(MixRules.Applies(AppearanceResolver.Resolve(new AppearanceConfig { HighContrast = true })));
        Assert.False(MixRules.Applies(AppearanceResolver.Resolve(new AppearanceConfig { Theme = "classic" })));
        Assert.True(MixRules.Applies(ResolvedAppearance.Default));
    }

    [Fact]
    public void A_set_without_numbers_is_not_judged()
    {
        // Every offered mixable set is measured, so an id this build does not know stands for one without numbers.
        var column = With(Pure(GlyphSetId.Medallion), QuestState.Ready, (GlyphSetId)7);
        var verdict = MixRules.Evaluate(column);
        Assert.False(verdict.Measured);
        Assert.True(verdict.Ok);
        Assert.Null(MixRules.Fix(verdict, GlyphSetId.Medallion, null));
    }

    [Fact]
    public void The_column_is_the_resolved_appearances_sets()
    {
        var config = new AppearanceConfig { Theme = "medallion", Glyphs = new Dictionary<string, string> { ["ready"] = "aether-crystal" } };
        var column = MixRules.Column(AppearanceResolver.Resolve(config));
        Assert.Equal(GlyphSetId.AetherCrystal, column[AppearanceStates.Index(QuestState.Ready)]);
        Assert.All(column.Skip(1), static s => Assert.Equal(GlyphSetId.Medallion, s));
    }

    [Fact]
    public void A_pick_saves_the_set_and_From_theme_or_the_themes_own_set_removes_it()
    {
        var config = new AppearanceConfig { Theme = "medallion" };
        AppearanceEdits.SetGlyph(config, QuestState.Ready, GlyphSets.AetherCrystal);
        Assert.Equal("aether-crystal", config.Glyphs!["ready"]);
        Assert.True(AppearanceEdits.HasMix(config));

        // A differently spelt saved key for the same state is replaced, not doubled.
        config.Glyphs.Remove("ready");
        config.Glyphs["Ready "] = "aether-crystal";
        AppearanceEdits.SetGlyph(config, QuestState.Ready, GlyphSets.IshgardGlass);
        Assert.Equal(new Dictionary<string, string> { ["ready"] = "ishgard-glass" }, config.Glyphs);

        AppearanceEdits.SetGlyph(config, QuestState.Completed, GlyphSets.Medallion);
        Assert.False(config.Glyphs.ContainsKey("completed"));
        AppearanceEdits.SetGlyph(config, QuestState.Ready, null);
        Assert.Null(config.Glyphs);
        Assert.False(AppearanceEdits.HasMix(config));

        AppearanceEdits.SetGlyph(config, QuestState.Blocked, GlyphSets.Orrery);
        AppearanceEdits.ResetMix(config);
        Assert.Null(config.Glyphs);
        Assert.Equal("medallion", config.Theme);
    }

    [Fact]
    public void The_heat_table_reads_every_pair_against_its_modes_bar()
    {
        var mix = With(Pure(GlyphSetId.Medallion), QuestState.Blocked, GlyphSetId.IshgardGlass);
        Assert.True(MixRules.TryHeat(mix, QuestState.Foreclosed, QuestState.Blocked, null, out var worst));
        Assert.True(worst.CrossSet);
        Assert.Equal(PairReading.Close, worst.Reading);

        foreach (var mode in MixTable.Modes)
        {
            Assert.True(MixRules.TryHeat(mix, QuestState.Foreclosed, QuestState.Blocked, mode, out var cell));
            Assert.Equal(mode, cell.Mode);
            Assert.Equal(MixTable.Under(cell.Value, MixTable.CloseBar(mode)) ? PairReading.Close : PairReading.Apart, cell.Reading);
        }

        Assert.False(MixRules.TryHeat(mix, QuestState.Ready, QuestState.Ready, null, out _));
    }

    [Fact]
    public void A_preview_look_follows_the_saved_frames_and_theme_the_moment_they_change()
    {
        // The Mix section's faces are drawn in a look per set built from the saved look, never the frame's resolved one:
        // a frames pick or a theme card in the same frame must not leave the previews in the old kit until the next edit.
        var looks = new MixLooks();
        var saved = new AppearanceConfig { Theme = ThemePresets.IshgardGlass.Key };
        var crystal = looks.For(saved, GlyphSetId.AetherCrystal);
        Assert.Equal(FrameKitId.Came, crystal.Frames);
        Assert.Equal(PaletteId.IshgardSnow, crystal.Palette);
        Assert.All(AppearanceStates.All, state => Assert.Equal(GlyphSetId.AetherCrystal, crystal.SetFor(state)));
        Assert.Same(crystal, looks.For(saved, GlyphSetId.AetherCrystal));
        Assert.Equal(1, looks.Resolutions);

        // A frames pick: the next preview is in the new kit, with nothing else edited in between.
        AppearanceEdits.SetFrames(saved, FrameKits.Get(FrameKitId.Silver));
        Assert.Equal(FrameKitId.Silver, looks.For(saved, GlyphSetId.AetherCrystal).Frames);
        Assert.Equal(FrameKitId.Silver, looks.For(saved, GlyphSetId.Medallion).Frames);

        // "From theme" again: the theme's own kit.
        AppearanceEdits.SetFrames(saved, null);
        Assert.Equal(FrameKitId.Came, looks.For(saved, GlyphSetId.AetherCrystal).Frames);

        // A theme card: its kit and palette, at once.
        AppearanceEdits.ApplyTheme(saved, ThemePresets.Sumi);
        var sumi = looks.For(saved, GlyphSetId.Orrery);
        Assert.Equal(FrameKitId.Kirikane, sumi.Frames);
        Assert.Equal(PaletteId.KuganeLacquer, sumi.Palette);
        Assert.All(AppearanceStates.All, state => Assert.Equal(GlyphSetId.Orrery, sumi.SetFor(state)));

        // The saved look is only read: the one-set look is a new config, and an unchanged saved look resolves nothing more.
        Assert.Null(saved.Glyphs);
        var resolutions = looks.Resolutions;
        Assert.Same(sumi, looks.For(saved.Clone(), GlyphSetId.Orrery));
        Assert.Equal(resolutions, looks.Resolutions);
    }

    /// <summary>Every column one state away from a measured set: 4 sets × 8 states × 3 other sets.</summary>
    private static IEnumerable<GlyphSetId[]> OneChangeMixes()
    {
        foreach (var base_ in Measured)
        {
            foreach (var state in AppearanceStates.All)
            {
                foreach (var other in Measured.Where(s => s != base_))
                {
                    yield return With(Pure(base_), state, other);
                }
            }
        }
    }
}
