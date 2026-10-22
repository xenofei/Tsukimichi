# Response to MR 1 refit round 2 (level critic)

Per-level fixes were made on two branches and merged (base-02 668110f1, base-03 b03fd0e1). base-01, base-04 and base-05 had nothing to fix. After the merge `mfl.py stage mr1` passes (pooled mean 8.05 per 48, band 7.47-9.67; the finale 5.52 is 3.17 below the other four's mean 8.69; no rule changed), `convert base-01..05 --check` passes 10 of 10, and the runtime level files match the pipeline json. Aces were re-suggested over 576 games (MoonfallAces.Suggest via the Shipped_Aces test): base-01 200k, base-03 210k, base-04 210k, base-05 190k unchanged; base-02 180k -> 200k (126 wins of 576), recorded in `aces.json`.

## Level critic
- **M1 (1-3 stuck rule): fixed.** The west crown gaps were widened from 70 to 115, and both east crowns now end at x 640 (the summit moon stays). Stuck-rule fires fell from 98 held-out to 46 per 1728 games, the first-shot sweep from 6 to 2 of 777 aims. Difficulty was held with `tune --hold`: 8.9 per 48 held out, in the band 8.50-9.70. The fresh-seed stuck count was not re-measured by a separate run.
- **m1 (1-2 brick share): fixed.** The west and east road legs were lengthened (trim 45 -> 20): 100 pieces, 30 bricks, 30%. The share has no margin above 30%.
- **m2 (1-2 protan minimum at (542, 487)): fixed.** That brick is excluded from the orange candidates; the readcheck minimum is now 0.115. Re-tuned with `tune --hold --lock 8.7`: 9.2 held out, inside the tolerance and the band 8.10-9.30.
- **m3 (1-3 top-right grid traces nothing): not fixed.** Reshaping it into a moon halo is a larger redesign than this fix, and would force a re-tune. The holdout spread is unchanged (no piece over 16%, the rule's limit passes).
- **m4 (1-4 right-wall column): not fixed.** The critic accepts it as legitimate difficulty; the wall-side rule (20%) passes; a change would disturb the held difficulty.
- **m5 (1-1 brick share 22%): not fixed.** The docstring gives the reason (the subject's slopes); raising it needs a layout change to an approved trail and a re-tune.
- **m6 (densest 90 x 90 window, 1-2 at the 45% edge): not fixed.** A stage-wide threshold recalibration is the coordinator's call; no threshold was changed. 1-2 is still at 45%.
- **m7 (F9 protan minima under 0.10): partly.** 1-2 improved (0.115 after m2). 1-3 is 0.098 at (515, 389). 1-1 (0.075), 1-4 (0.083) and 1-5 (0.098, a brick that is never orange) are UX's call and unchanged.
- **Nit, 1-3 "moon in the gap":** the wording was wrong; no moon was added in the east gap. This response's description is the accurate one.
- **Nit, stage figures:** this response uses the figures from the committed stage run above, not the earlier ones.
- **Nit, `status` shows convert stale for 1-1, 1-2, 1-5:** a git commit-order artefact; the runtime files are current (`convert --check` passes).
