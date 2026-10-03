# A6 decision panel: Questionable "Just this quest" and "Do this next"

Asked by the owner on 2026-10-03: "Get with critics, power users, casual players, and others to determine if this is wanted or not."

**Verdict: build with changes. Effort stays S.** All six personas want it.

## What happens today

- **The Start pill** (`QuestionableActions.StartQuest`, `DetailPane.Actions.cs`) adds the quest to Questionable's priority list and calls `StartQuest`. That puts Questionable in automatic mode. When the list is done, it moves on to the main scenario (`docs/ipc.md`).
- **The risk:** one click on a side quest can turn into an unattended MSQ run, with dialogue auto-advanced.
- **The single-quest gate:** both upstream Questionable and the WigglyMuffin fork register `Questionable.StartSingleQuest`. It runs SingleQuestA, then SingleQuestB. When the quest changes it logs "Single quest is finished" and returns to Manual, with `allowNewMsq: false`.
- **The insert gate:** `Questionable.InsertQuestPriority(int, string)` is also registered. It returns true even for unknown quests, so a path check and an `ExportQuestPriority` read-back are needed.
- **Questionable's own controls:** its own UI has "Stop after current quest" (Questionable#118, closed 2026-05-18). That toggle can't be reached over IPC.

## Community evidence

- **r/ffxivdiscussion 1wf3rnt** (2026-09-13): players use Questionable for targeted catch-up and unlocks. They accept automation that only affects themselves, and object to it in content with other players.
- **r/ffxivdiscussion 1fscur3:** running unattended for hours "is absolutely suspicious looking".
- **Questionable#118:** pausing to do something else after one quest "can be slightly annoying". This is why Questionable added its own stop-after-current.
- **Questionable#5:** the workaround for a level gate is adding the class quest to Priority Quests by hand. "Do this next" is that workaround in one click.
- **Questionable#45** (open): a priority quest started after a teleport reset progress on the current quest. This is the risk for "Do this next" mid-run.
- **AutoDuty#556** (2026-09-29): players asked for single job quests between loops. The author refused, citing too much unattended botting. This shows both the demand and the social limit.

## The panel

1. **Design critic: want, with changes.** The Start pill doesn't do what it says. A three-way split button adds a chevron to every action bar, which goes against the owner's static-layout preference. Better: make Start do one quest, and put the other choices in the "…" menu.
2. **Power user: want, with changes.** "Do this next" fixes level gates on every alt. Keep "keep going" as a menu item. Inserting mid-run must be safe (#45).
3. **Casual story player: want.** "Does this quest, then stops" is what they assumed the button already did, and it protects the story they play for.
4. **Returning player: want.** Hand off one errand, then take control back, all in one click.
5. **Automation skeptic: want, with changes.** Single-quest runs limit session length and can't wander into duties with real players. Conditions: no bulk variant, and "Do this next" must not quietly extend a run that is already going.
6. **Accessibility-minded player: want.** Today this takes four steps with a small toggle; it becomes one click plus a chat line when it finishes. A menu is a bigger target than a split-button chevron.

## Changes to the A6 design

1. **No split button.** Start calls `StartSingleQuest`: "Questionable does this quest, then stops." If the gate is missing, it falls back to the old path and the tooltip says so.
2. **"Start here and keep going"** (the old behaviour) moves to the "…" menu. Sends from a route, chain, pin or My blues keep their "continue" behaviour.
3. **"Do this next"** inserts at index 0. It goes in the "…" menu under "Show Questionable hand-off" and is enabled only when the reason gate shows a path. The list is read back and chat says "#1 on Questionable's list". Test it against Questionable#45 in game; if that reset reproduces, disable it while Questionable runs, with the reason shown.
4. **When the run ends,** chat says "Questionable finished <quest> and stopped." This is the first piece of A4's run receipt.
5. **A4 is narrowed** to: stop after N quests, stop at a set time, the receipt, and stop-after-current for runs started some other way.
6. **Docs:** add both gates to `docs/ipc.md`, plus a changelog line about Start's new default. No new setting.

## Expected usage (an estimate)

- **New Start default:** about 75–85% of per-quest starts.
- **"Start here and keep going":** about 10–20% of per-quest starts.
- **"Do this next":** about 10–15% of Questionable users, mostly people levelling alts.
- **Overall:** about 15–25% of all Tsukimichi users in a month.
