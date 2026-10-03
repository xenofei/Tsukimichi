# Implementation review: Decoration v13 on `main`

**Reviewer:** art supervisor
**Code reviewed:** `main` at `ba9ff9f` ("three distinct Decoration looks"): `BannerGrade.cs` (Core), `BannerGrading.cs`, `Ornament.cs`, `Chrome.cs`, `DetailPane.Hero.cs`, `Flair.cs`, `FlairTones.cs`, `MedalTokens.cs`, `MedalArt.cs` (Plain), and `ConfigWindow.General.cs` (the preview).

## How I checked

### Banner grade harness
- **Setup:** a throwaway .NET 10 console in `scratchpad/sup-flair/harness/` that references `Tsukimichi.Core`.
- **Inputs:** each banner cover-cropped to the hero size, 380 × 156. That is every plugin banner in `Tsukimichi/assets/ui/banners/` (23 files), plus the daylight `docs/plan-site/mock/scene-day.jpg`.
- **Run, exactly as the plugin does:**
  - `BannerGrade.MeanRgb` → `Strength` → `GradeInPlace`;
  - then the scrim to `Night` (`ScrimAlpha`) and the `MoonHigh` wash, composited as `DrawOver` draws them.
- **Measured:** mean and peak luma. Side by side before and after: `harness/grade_cmp.png`.

### Plain medals
- My round-5 exporter (`scratchpad/sup-g1/exporter/`), extended to `MedalArt.Medal(state, MedalTokens.Plain, px)` at 12, 16 and 64 px on the Plain ground (`plain_mesh.png`).
- Salience by mean |L − ground| at 12 px.

### Everything else
- Code review: brass card, corner marks, hero halo gate, Quiet tones, stars and the Settings preview.

## Verdict: CHANGES REQUIRED (one item)

Everything drawn matches the approved spec and mock except one thing: the banner grade treats the plugin's own banners as if they were daylight art.

## Required fix

### 1. The night grade crushes banners that are already night scenes

**Files and code:**
- `Tsukimichi.Core/Ui/BannerGrade.cs`: `MinStrength = 0.55f`, `Strength(Vector3)`, `Desaturation = 0.35f`, `GradeInPlace` and `Grade`.

**Measured:**

| Banner | Mean L in | Peak in | Strength | Mean L out | Peak out |
|---|---|---|---|---|---|
| `scene-day.jpg` (daylight) | 107.6 | 246 | 0.55 | **50.8** | 132 |
| `msq-dt` | 45.2 | 221 | 0.55 | **26.4** | 118 |
| `msq-sb` | 34.0 | 223 | 0.55 | **22.4** | 116 |
| `side-coerthas` (brightest plugin banner) | 64.0 | 220 | 0.55 | 33.1 | 117 |
| All 23 plugin banners | 34–64 | 200–237 | 0.55 (every one) | **22–33** | 103–133 |

**What is wrong:**
- The daylight case is right: 50.8 ≤ 55 and peak 132 ≤ 170.
- Every banner the plugin ships is already a moonlit scene (mean L 34–64). Each still gets at least a 0.55 multiply plus 35% desaturation, because `Strength` never goes below `MinStrength`.
- The result sits at mean L 22–33, against the Night pane at about L 20. The art becomes an indistinct murk.
- In `grade_cmp.png`:
  - each banner's painted warm moon turns into a grey disc;
  - the warm horizon afterglows (msq-dt's gold band, Thanalan's amber) go mud-brown;
  - the silhouettes barely separate from the sky.
- In colour terms, the value range is crushed to a few levels above the pane, and the one warm accent each banner carries is desaturated away.
- The grade exists to put *daylight* art into the moonlit window (fix 1 of the design review). Night art is already there.

**Change:**
- **Range:** let the strength fall to zero. Set `MinStrength = 0f` and keep `MaxStrength = 0.75f`.
- **Strength:** choose it as the least strength that meets *both* targets:
  - mean L ≤ `TargetMeanL` (55);
  - the 99.5th-percentile luma ≤ `TargetPeakL` (170).
  - Use the percentile rather than the single brightest pixel, so a painted moon or a lantern of a few pixels is not what decides the grade.
  - `MeanRgb` already walks the pixels; collect a 256-bin luma histogram in the same pass.
  - Peak luma scales by about `1 − 0.777·s` (the luma of the multiply's tint at strength `s`), so the peak gives a strength of its own: `s ≥ (1 − 170/p99.5) / 0.777`. Take the larger of that and the mean-target solve.
- **Desaturation:** scale it with the strength, `Desaturation × (s / DefaultStrength)` (35% at 0.70, 0 at 0), in both `Grade` and `GradeInPlace`. Unchanged art keeps its colour.
- **Expected result:**
  - The plugin's night banners come out at a strength of about 0.25–0.35. Their peaks (200–237 at p100) fall to ≤ 170, and their means stay about 30–50.
  - The daylight art still lands at about 0.55–0.70.
  - The scrim, the wash and the moon road apply at every strength, as now.
- **Tests:** update `BannerGrade` tests so that:
  - a mean-L-45, peak-221 banner yields a strength in 0.2–0.4;
  - `scene-day`'s statistics still yield ≥ 0.5;
  - both graded outputs meet mean ≤ 55 and p99.5 ≤ 170.

## Checks that pass

### Full
- **Brass card** (`Chrome.CardSurface`, `Ornament.BrassBorder`):
  - The fill gradient goes from lighter `Raised` at the top to a darker foot.
  - A `MoonHigh` 0.07 highlight sits on the top inner edge only.
  - The drop shadow falls straight down (3, 10, 0.38).
  - The border's brass ramp runs along 160°, so the top and left edges sit at the highlight end and the bottom and right at the shadow end. This matches the approved mock samples.
- **Corner marks** (`Ornament.CornerMarks`): 1.5 px, their centre line set so they cover the frame's pixel (the 1 px overlap). The top marks are `CornerLit` and the bottom marks `CornerShaded`. Correct.
- **Hero halo** (`FlairRules.HeroHalo`): `Glow(flair) && state is Ready or ReadyOnOtherJob`. Correct.
- **Stars:** they appear only under `ShowStars` (Full): the rail sky, the tree's empty foot, the table title band and the preview.
  - They are seeded (`StarField.Generate`), with the 70/25/5 magnitude split, and avoid labels through the `avoid` rectangle.
  - There are none on the column header.
- **Banner grade passes:**
  - The multiply colour is `#2A3768`.
  - The scrim stops are 0 / 0.25 at 45% / 0.85.
  - The wash is `MoonHigh` 0.06 from the upper left.
  - The six-dash moon road runs along the bottom right.
  - Until the read-back lands, the art draws with the default-strength tint, so daylight never flashes ungraded. Good engineering.
  - Fix 1 changes only how strong the grade is.

### Quiet
- **Tones** (`FlairTones`): tree `#0E1323`, table `#131929`, detail `#182033`, cards `#1F273C`, rule `#262D42`. That is about 3% L per step, as approved.
- **Cards:** tonal planes, with a VeilLine border only under high contrast. Correct.

### Plain (exporter, 12 and 16 px)
- **Orientation:**
  - Ready, Ready on another job and In journal are lit on the right, with the limb toward the lower right (shipped `PhaseOutline`, terminator −0.18, tilt 28°).
  - Done is a waning half lit on the left, with its gilt arc on the right.
- **Emblems:**
  - Locked out is a `DalamudShade` disc with two dark cracks meeting at the upper left.
  - Not checked is the Mist "?" with its dot.
  - Blocked is a cloud band over a `MoonstoneDeep` disc.
  - Completed is a small `MoonstoneMid` moon with a gilt check.
- **Salience at 12 px:** Ready 96.8, next 58.8, so **1.65×** (≥ 1.3). Completed 52.6, so **0.54× Ready** (≤ 0.8).
- The meshes match `mock.html#glyphs`.

## Notes (not realism; for the coordinator)

1. **Settings preview, against spec §2:** the spec asks for "a 3-up miniature of these three looks, not today's single swatch, so the choice is visible before you click it".
   - `ConfigWindow.General.cs` `DrawFlairPreview` (line 258) still draws one sample, for the selected level only.
   - The sample itself renders each level faithfully (`Theme.PushFlair(level)`), so this is a spec-fidelity gap, not an art error.
   - Decide whether to hold 1.13.0 for it.
2. **Polish:** each corner mark draws a soft `Moon` 0.12 glow line under it (`Ornament.Mark`, line 343). Brass catches light; it does not emit it.
   - At 0.12 the glow is barely visible, and it softens the fitting's edge.
   - Dropping it would hold the "glow is light, not paint" rule strictly.

## Re-review after the fix: APPROVED

I re-ran the harness on `main` (`db31651`).
- **The strength call:** the new `Strength(mean, p99.5)`, with the 256-bin histogram from `MeanRgb` and `LumaPercentile(…, 0.995)`.
- **The passes, as `DrawOver` draws them:** `GradeInPlace` (desaturation scaled by strength), then the scrim and the wash.
- **Inputs:** all 23 plugin banners and `scene-day.jpg`, at 380 × 156.
- **Before and after:** `scratchpad/sup-flair/harness/grade_cmp_v2.png`.

### Measured after the full grade

| Banner | Strength | Mean L | p99.5 L |
|---|---|---|---|
| Plugin night banners (23) | 0.00–0.27 (most 0.22–0.26) | 28–43 | 77–156 |
| `scene-day` (daylight) | 0.64 | 46.0 | 111 |

- **Targets:** every banner meets mean ≤ 55 and p99.5 ≤ 170.
- **Single brightest pixels:** a few reach 177–197 (the painted moon cores of `class-job`, `hildibrand` and `other`), which is the reason for the percentile rule. They are still below the hero medal's moon (L about 230).
- **Light-touch cases:**
  - `hildibrand` (0.00) and `class-job`/`seasonal` (0.02–0.03) are already within target, and are left essentially untouched apart from the scrim. That is correct.

### Visual judgement
- **Night banners keep their character:**
  - The painted moons stay warm cream discs.
  - msq-dt's gold horizon band and Thanalan's amber afterglow keep their warmth.
  - Silhouettes separate cleanly from the sky.
  - The value range is intact above the pane.
  - The light multiply only pulls the brightest highlights under the medal's moon, which is the grade's job.
- **Daylight still reads as moonlit:**
  - Dawntrail comes out as a cool, desaturated dusk-blue scene, with its warm lanterns surviving as small accents.
  - It sits under the hero medal's moon.

### On the pre-scrim deviation
**Accepted.** Evaluating the mean target on the graded art before the scrim is *stricter* than evaluating the composite:
- daylight gets 0.64 instead of about 0.40–0.45, and lands at a composite mean of 46 (with headroom), not at 55;
- the scrim then darkens further only where it should, toward the bottom edge.

My "≥ 0.5 for daylight" expectation was an estimate of what meeting the targets would take, not a target in itself. The targets are met, with daylight graded firmly and night art graded lightly.

### Also verified
- **3-up Settings preview:** `DrawFlairPreview` now shows the three looks side by side. The spec gap is closed.
- **Corner marks:** the glow line under them is removed, so the brass now catches light rather than emitting it.

**Verdict: the Decoration v13 implementation is faithful to the approved spec and mock, and realistic as UI art. APPROVED.**
