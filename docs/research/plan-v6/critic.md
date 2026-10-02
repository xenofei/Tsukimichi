**Completeness critique of the 1.11–1.15 plan**

**Owner items not fully covered**

1. **Item 6 (icon) and item 7 (moons) wait for 1.12, and the stated reason doesn't fit G4.** G4 (the icon) is rated S, has no code dependencies and touches only assets and the packaging script, so it can ship in 1.11. The owner wrote "I hate that" about the moons. G2's tokens (Kinpaku #E0B860, Torinoko and the rest) should also land in 1.11, because U5's moon-gold bar, U7's MoonToggle and S1's armed fill are new 1.11 UI built in the old gold. G5 would then have to restyle them in 1.12.
2. **Item 6 has no approval step.** The decisions cover only the export size. The owner never gets to approve the composition itself, and the dropped "luminous moon over a road of light on water" proposal was never offered as an alternative. Add an icon approval decision.
3. **Item 7 has no approval checkpoint before G1 merges.** G3's A/B window exists, but no step says "approval build, then G1 merges".
4. **Item 2 may look unaddressed after 1.11.**
   - All the theme moments (M1, M2, M3) are in 1.12. 1.11 only adds 120–180 ms hover and fades, and if the owner's Windows "Show animations" is off, nothing moves at all.
   - The Reduce motion decision has to be settled before 1.11 ships. The 1.11 changelog or a one-time notice should point at the override.
   - Pulling one theme moment into 1.11 would help, for example the M1 waxing moon on completion.
   - Two motion proposals were dropped with no entry in notDoing: "Short, Reduce-motion-aware fade when switching zones (Flight)" and "Gallery v2 … tasteful motion".
5. **Item 8 is only half done in 1.11.**
   - "Step {0} of {1} in {2}" is in Core: `Tsukimichi.Core/GamePanels/QuestVerdict.cs:177-178`. It is a copy-only change but sits in M2 (1.12). Move that string into U5.
   - The theme promises "never N of M done", but U5 only fixes PathChart and the chain line. Many other done/total strings remain:
     - `Strings.cs:73/75/135/137`
     - `Strings.Panes.cs:196` (Flight currents)
     - `Strings.Jobs.cs:28/45`
     - `Strings.Payoff.cs:24`
     - `Strings.WelcomeBack.cs:79`
     - `Strings.Collector.cs:25/78`
     - `RequirementDetail.cs:145-146` ("N of M prerequisites done")
   - Either narrow the theme to the path and chain lines, or add an audit task.
   - Later items bring tallies back: C9 "Journal 27/30", P4 "42 left · 6 set aside", C8 "…cover 74%", P8 "90% · 143 quests". Each needs checking against the "what is left, not what's done" rule.
6. **Item 3 is limited to the Journal tab and the named surfaces.**
   - Other tabs with filter rows are not audited: My blues, Nearby, Characters and Flight.
   - Later items break the static rule unless they are made to follow ChromeBands:
     - P4 tier chips and P7 kind chips (new filter rows)
     - A2's "card appears in … the overlay" (an inserted overlay row, which is what U4c removes)
     - C9's overlay journal count "from 25" (a line that appears)
     - C10 warnings and P5's new-chapter line
   - Add a cross-release constraint that any new band or overlay line must pass the ChromeBands test.
7. **Item 3: the floating layers can collide and can hide what the player is reading.**
   - The NoticeDock (bottom right), UndoToast (bottom centre), U3's "Selected quest hidden · Show" and S1's nudge hint have no stacking or collision rule.
   - Nothing stops the dock from covering the selected row.
   - Add a rule: one floating slot manager, and never cover the selected row or the status bar.
8. **Item 9's armed cue may break the owner's "no noise glyphs" taste.**
   - The key-cap cue plus the "hold Shift" suffix on every armed button is the kind of glyph the owner dislikes. Consider showing the cue only on hover (this was the dropped "Show the armed state on hover" proposal, which was only partly carried over).
   - Ctrl+click has existing ImGui meaning (it turns sliders into text input), and modifier+click may also mean multi-select in tables. ArmedMenuItem in context menus needs to be checked against both.
9. **Item 5 has internal contradictions.**
   - The owner-item summary says labels up to 32 characters and hints up to 90. U7 says a test caps them at 40 and 110. Pick one.
   - "Hint one sentence" vs "at most 2 lines" also needs reconciling.

**Strong proposals dropped with no reason given**

- **"Hand-strain controls: rebindable shortcuts, adjustable hold, two-click confirm".** S2 makes several actions hold-to-confirm and keeps a 600 ms hold as the controller fallback. Hold duration needs to be adjustable, and a two-click alternative is needed for accessibility. This belongs inside S2.
- **"Walks use vnavmesh's cancellable pathfind … delete PendingWalkStop".** Neither planned nor in notDoing. It may already be covered by 1.10's "walks stop even mid-pathfind"; say so either way.
- **"Read characters from other XIVLauncher folders (multibox roaming paths)".** It is listed in topCommunityInsights but appears in neither the plan nor notDoing.
- **"Trust you can check: build provenance, published hashes".** It directly supports A10's "Local only" trust claim but was dropped without comment.
- **"Read the remaining game gates: Doman Enclave, Eureka level, Cosmic grade, legacy status" and "Check Eureka gates by recording elemental level".** Only implied by C1, never stated.
- Also missing from both the plan and notDoing:
  - "Legibility backplate for text over the game"
  - "Nameplate and game-font polish (a ☾ known to render)", which relates to item 7 in game
  - "One first-run card instead of five prompts / shorter tour", which relates to item 3
  - "Progressive disclosure in the detail pane"
  - "Calmer tooltips"
  - "Story meter"
  - "Fewer teleports: group-by-place ordering"
  - "Remove leftover per-poll allocation in the relic read"
  - "Route window: fixed Next step card", which relates to items 3 and 8

**Risky items without mitigation**

- **C4 (New Game+ may overwrite saved progress) is a possible data-loss bug placed in 1.14.** A cheap guard can ship in 1.11 whether or not the owner has tested NG+: don't commit a loss whose ids are all QuestRedo rows. Losing data matters more than polish.
- **C2: mount-collection quests read Ready / "Get now" from level 1.** This is a correctness bug sitting in 1.14 and should come sooner.
- **A11 (frame budget) is in 1.13, but its own "why" says that budget is needed for the 1.11/1.12 motion.** At least the 5 s CompanionSetupService stall and the per-frame travel snapshot should move to 1.11 or come before M1.
- **S6 extends the allowlist only to 1.14, and C1 is also in 1.14.** If C1 slips, CI breaks again on the 1.14 version bump. Extend to 1.15, or tie the expiry to C1 merging.
- **Config and settings migration is not mentioned for U7.**
  - Section renames, the Flair→Decoration and palette glossary renames, removing the scale controls from the filter panel, and the saved last-opened section index all need a migration.
  - Users who already picked a non-English language need a defined fallback once the picker is hidden.
  - Rewritten hints leave the frozen translations stale; say what non-English users see.
- **The tour has to follow the new layout.** UiRects is noted for U2 only. U7's new Settings layout and U4's Moonlit toolbar also move tour targets.
- **1.11 has no in-game check list** for the owner items: '#' gone at every Flair level, 100% and 150% scale, Windows animations on and off, Enter not saving. "No smoke-test pages" doesn't forbid this; Plan v5 already did it as a doc.

**Sequencing problems**

- **1.11 is overloaded.** It holds two L items (U2, U7), three M items (U4, U5, U8) and eight S items. Shipping in two steps would get owner-visible fixes out sooner:
  - **1.11:** U1, U6, S1, S5, S3, S4, S6, G4.
  - **1.11.x:** U2/U3/U4, U7, U8.
- **P9 (API 16) sits in 1.15, but the deadline is a date (January 2027) and four M/L releases come first.** Track the API 16 branch work as a parallel track starting now, with a date trigger and a re-check after Fan Fest, instead of tying it to a release slot.
- **A1 (/tsuki stop, rated S) is a safety item in 1.13** even though "one Stop" is part of the safety theme. It could ride with S2 in 1.11.