export const meta = {
  name: 'moonfall-stage',
  description: 'One Moonfall stage through the 22 October workflow: author (or reuse), integrate, one combined review, fix Blockers and Majors only, log Minors',
  whenToUse: 'Owner says "continue Moonfall"; run once per stage, one stage in flight. State comes from the repo, not from earlier runs.',
  phases: [
    { title: 'Author', detail: 'one time-boxed author per level in its own worktree (skipped when the stage is already authored)' },
    { title: 'Integrate', detail: 'merge, tune --stage, stage, convert, Aces, gates, pack' },
    { title: 'Review', detail: 'one combined reviewer, one round, from the pack only' },
    { title: 'Fix', detail: 'record; per-level fixers only for levels with Blockers or Majors; stage fixer merges and checks' },
  ],
}

// args: { worktree, branch, key, levels[], codes[], later[], authored?, generator?, mergedNote?, salvage?{level: branch}, authorModel?, knownFails?, integrated?{commit, pack} }
// generator: true = generator-first (owner, 22 October 2026, after the MR 1 test): `mfl.py make` builds each level, the best passing variant is adopted as is, and only finales get hand polish.
const st = args
const WT = st.worktree
const BR = st.branch || 'moonfall-peggle'

const COMMON = `Read docs/team/agent-brief-common.md first and follow it. Words are defined in docs/design/v9/levels/GLOSSARY.md. The level method is tools/moonfall-levels/README.md; the rules are docs/design/v9/levels/charter.md section 1 (the Peggle-match rules of 9 October 2026) and the owner's answers sections, including "Owner's answers on the workflow review" (22 October 2026). Owner rules: every level is an illustrated scene whose pegs trace or emphasise its subject; rich and detailed; figures from game art; game art read at runtime, never shipped; nothing from Peggle ever enters the repo. Tools: \`mfl.py check <id>\` is the 3-second inner loop (--json names each fault's piece, place and hint); \`generate\`, \`tune\`, \`tune --hold\`, \`tune --stage\`, \`level\`, \`status\`, \`pack\`, \`diff\`, \`sheet\` are in the README. Caches are shared across worktrees; if game textures are missing run \`py -3 tools/moonfall-levels/mfl.py fetch\`.
Run long commands (level, build, convert, stage, selftest, gate) in the FOREGROUND, never in the background with sleep loops; \`tune --stage\` and \`stage\` can take over 20 minutes, so give them no timeout (a killed tune leaves a level half-tuned). Send long output to files in your scratchpad and print only verdicts.`

const LATER_FAILS = `Until every stage is refitted, the gates may fail ONLY these known, expected tests for levels of stages not yet refitted (${(st.later || []).join(', ') || 'none'}): Every_shipped_level_passes_the_greedy_player and Shipped_Aces for those level ids. If MoonfallSceneStartTests.The_menus_thumbnail_is_the_levels_own_scene_scaled_down fails with "never settled", rerun it alone; it counts as passing only if it passes alone, and say so. ${st.knownFails ? 'Also known and handled elsewhere: ' + st.knownFails + '.' : ''} Any other failure is a real failure.`

const AUTHOR_SCHEMA = { type: 'object', properties: { level: { type: 'string' }, branch: { type: 'string' }, commit: { type: 'string' }, levelPassed: { type: 'boolean' }, heldOutWins: { type: 'number' }, pieces: { type: 'number' }, flagged: { type: 'boolean' }, notes: { type: 'string' } }, required: ['level', 'branch', 'commit', 'levelPassed', 'flagged', 'notes'] }
const INTEG_SCHEMA = { type: 'object', properties: { commit: { type: 'string' }, stagePassed: { type: 'boolean' }, convertPassed: { type: 'boolean' }, selftestPassed: { type: 'boolean' }, gatesPassed: { type: 'boolean' }, pack: { type: 'string' }, summary: { type: 'string' }, problems: { type: 'string' } }, required: ['commit', 'stagePassed', 'convertPassed', 'selftestPassed', 'gatesPassed', 'pack', 'summary', 'problems'] }
const REVIEW_SCHEMA = { type: 'object', properties: {
  verdict: { type: 'string', enum: ['APPROVE', 'REVISE'] },
  findings: { type: 'array', items: { type: 'object', properties: { id: { type: 'string' }, severity: { type: 'string', enum: ['Blocker', 'Major', 'Minor', 'Nit'] }, level: { type: 'string' }, issue: { type: 'string' }, evidence: { type: 'string' } }, required: ['id', 'severity', 'level', 'issue'] } },
  report: { type: 'string' } }, required: ['verdict', 'findings', 'report'] }
const LEVELFIX_SCHEMA = { type: 'object', properties: { level: { type: 'string' }, branch: { type: 'string' }, commit: { type: 'string' }, changed: { type: 'boolean' }, levelPassed: { type: 'boolean' }, fixed: { type: 'string' }, notFixed: { type: 'string' } }, required: ['level', 'branch', 'commit', 'changed', 'levelPassed', 'fixed', 'notFixed'] }
const STAGEFIX_SCHEMA = { type: 'object', properties: { commit: { type: 'string' }, gatesPassed: { type: 'boolean' }, stagePassed: { type: 'boolean' }, convertPassed: { type: 'boolean' }, selftestPassed: { type: 'boolean' }, summary: { type: 'string' }, notFixed: { type: 'string' } }, required: ['commit', 'gatesPassed', 'stagePassed', 'convertPassed', 'selftestPassed', 'summary', 'notFixed'] }
const allPass = f => f && f.gatesPassed && f.stagePassed && f.convertPassed && f.selftestPassed
const isFinale = code => /-5$/.test(code)

async function retryOnce(label, run) {
  try { return await run() } catch (e) { log(`${label} failed to start (${String(e).slice(0, 160)}); one retry`); return run() }
}

// ---- Author: time-boxed, one per level ----
phase('Author')
let authored
if (st.authored) {
  authored = st.levels.map(l => ({ level: l, branch: '(already merged)', commit: '', levelPassed: true, flagged: false, notes: 'authored earlier and merged' }))
} else {
  authored = await parallel(st.levels.map((lvl, i) => () => retryOnce(`author ${st.codes[i]}`, () => agent(st.generator ? `${COMMON}
You make Moonfall level ${lvl} (${st.codes[i]}) of stage ${st.key} with the unattended generator, in your own worktree on its own branch. The owner trusts the generator since it passed the MR 1 test (22 October 2026), so its levels ship as made; only finales get hand polish.
- First run \`git merge --no-edit ${BR}\`. Keep the level's subject, technique, source and crop, place tag and jewels from the charter; the scene recipe must exist (write it from the charter if it does not, as earlier stages' recipes do).
- Run \`mfl.py make ${lvl}\` (defaults: 4 variants, stage band${isFinale(st.codes[i]) ? ', finale cap' : ''}). If a variant passes, adopt the top-ranked passing one with \`mfl.py make ${lvl} --adopt K\` (it runs \`level ${lvl}\`, which must PASS).${isFinale(st.codes[i]) ? ' This is the finale: after adopting, polish it by hand (about 45 minutes) so the subject reads plainly and nothing floats without shape, keep it in the finale band, and finish with `mfl.py level ' + lvl + '` (must PASS).' : ' Do not polish it by hand.'}
- If no variant passes, run make once more with \`--variants 8 --seed 2\`. If still none passes, adopt the best-ranked variant, fix only what its refusal names (about 30 minutes, \`mfl.py check\`, \`tune\`, \`level\`), and say so in notes. If level still will not PASS, commit what you have and set flagged=true with the exact reason.
- Touch only this level's own files. Commit. Return branch (git branch --show-current), commit, whether level passed, held-out wins/48, piece count, flagged, and short notes naming the adopted variant and its tracing score.` : `${COMMON}
You make Moonfall level ${lvl} (${st.codes[i]}) of stage ${st.key} under the Peggle-match rules, in your own worktree on its own branch. This is TIME-BOXED authoring (owner, 22 October 2026).
- First run \`git merge --no-edit ${BR}\`.${st.salvage && st.salvage[lvl] ? ` Then merge the earlier partial, unchecked work on ${st.salvage[lvl]} (\`git merge --no-edit ${st.salvage[lvl]}\`; drop build outputs or stray files it committed) and continue from it if it is on track.` : ''}
- Keep the level's subject, technique, source and crop, place tag and jewels from the charter. Run \`mfl.py generate ${lvl} --variants 3\` and pick the variant that best traces the subject (or keep the current layout if it traces better). ${isFinale(st.codes[i]) ? 'This is the finale: polish it by hand so the subject reads plainly, and tune it into the lower part of the band (it must win fewer games than the mean of the other four).' : 'Polish only where the subject does not read; aim near the stage mean.'}
- Budget: about 45 minutes of polish after picking. Iterate with \`mfl.py check ${lvl}\`, then \`mfl.py tune ${lvl}\`, then finish with \`mfl.py level ${lvl}\` (must PASS). If the budget runs out and the subject still does not read, or level will not PASS, stop: commit what you have, set flagged=true, and say exactly why in notes (the owner sees flagged levels with their contact sheet).
- Touch only this level's own files. Commit. Return branch (git branch --show-current), commit, whether level passed, held-out wins/48, piece count, flagged, short notes.`,
    { label: `author ${st.codes[i]}`, phase: 'Author', isolation: 'worktree', effort: 'high', model: st.authorModel || 'sonnet', schema: AUTHOR_SCHEMA }))))
}
const ok = authored.filter(a => a && a.levelPassed && !a.flagged)
if (ok.length < st.levels.length) {
  return { stage: st.key, stopped: 'authors', authors: authored.map((a, i) => a ? { level: a.level, ok: a.levelPassed, flagged: a.flagged, branch: a.branch, commit: a.commit, wins: a.heldOutWins, notes: (a.notes || '').slice(0, 300) } : { level: st.levels[i], ok: false, notes: 'no result' }) }
}

// ---- Integrate ----
phase('Integrate')
const merges = st.authored ? (st.mergedNote || 'The authors are already merged; nothing to merge.') : `Merge these branches (--no-ff, keep both sides; the new level files win over the old ones): ${ok.map(a => a.branch + ' @ ' + a.commit).join(', ')}.`
// args.integrated = { commit, pack, summary } resumes after an integration that only failed on known gate failures.
const integ = st.integrated ? Object.assign({ stagePassed: true, convertPassed: true, selftestPassed: true, gatesPassed: true, summary: 'integrated earlier', problems: '' }, st.integrated) : await agent(`${COMMON}
You integrate stage ${st.key} (${st.levels.join(', ')}) in the worktree ${WT} (branch ${BR}; cd there; work and commit there).
1. ${merges}
2. \`py -3 tools/moonfall-levels/mfl.py sources\`, then \`mfl.py stage ${st.key} --json\` must PASS (band, finale rule, jewels). If it fails on the mean or the finale, run \`mfl.py tune --stage ${st.key}\` and re-check; say what moved.
3. \`mfl.py convert ${st.levels.join(' ')}\` then \`mfl.py convert ${st.levels.join(' ')} --check\` must PASS; refresh the runtime recipes, plates and level JSON under Tsukimichi.Core/Moonfall/Levels/; re-suggest these levels' Aces with MoonfallAces.Suggest (576 games) into Modes/aces.json.
4. \`mfl.py selftest\` and \`pwsh tools/gate.ps1\` must pass. ${LATER_FAILS}
5. \`mfl.py pack --stage ${st.key}\` (the review pack in docs/design/v9/levels/review/${st.key}/). Commit.
Return the commit, booleans for stage, convert, selftest and gates (per the rule above), the pack path, a short summary and problems.`,
  { label: `integrate ${st.key}`, phase: 'Integrate', model: 'sonnet', effort: 'medium', schema: INTEG_SCHEMA })
if (!integ || !(integ.stagePassed && integ.convertPassed && integ.selftestPassed && integ.gatesPassed)) return { stage: st.key, stopped: 'integrate', integ }
log(`${st.key} integrated @ ${integ.commit.slice(0, 8)}: ${integ.summary.slice(0, 140)}`)

// ---- One combined review ----
phase('Review')
const review = await agent(`${COMMON}
You are the single reviewer of Moonfall stage ${st.key} (levels ${st.levels.join(', ')}; ${st.codes.join(', ')}). One combined review, one round (owner, 22 October 2026): only Blockers and Majors will be fixed; Minors go on the Minor list; the owner's in-game play-through is the final sign-off, so flag what would waste the owner's time.
Work read-only in ${WT}: do not edit, build, commit or stash; run no dotnet build/test and no mfl.py build/convert/stage/selftest/generate/tune. Judge from the review pack at ${integ.pack} (composites, engine renders at 1280 and 640, trace sheets, reports, the stage table) plus \`py -3 tools/moonfall-levels/mfl.py status --stage ${st.key}\`.
Cover all three focuses:
- GAME DESIGN: fun, feel, pacing and spectacle under Peggle's rules; whether each dense board reads as its subject and feels lavish; the stage as a set (theme, variety, curve, finale).
- UX/UI: readability of every peg kind against each scene incl. colour-blind margins (F6, F9), motion near pieces, both window sizes (1280 and 640).
- LEVEL DESIGN: playability (dead first shots, stuck balls, holdouts, the stage mean and finale), whether the layout traces its subject, in-engine art against the pipeline composites.
Severity: Blocker = unplayable or broken; Major = the owner would mark the level Redo; Minor = worth polishing later; Nit = taste.
Return verdict (APPROVE when there are no Blockers or Majors), findings (id, severity, level id such as base-07 or "stage", one-line issue, evidence), and report: the full report (first line exactly "VERDICT: APPROVE|REVISE (B blockers, M majors, m minors)", then the findings table, then per-level notes; under about 10 KB).`,
  { label: `review ${st.key}`, phase: 'Review', effort: 'high', schema: REVIEW_SCHEMA })
if (!review) return { stage: st.key, stopped: 'review', integ: integ.commit }
const fs = review.findings || []
const bm = fs.filter(f => f.severity === 'Blocker' || f.severity === 'Major')
const minors = fs.filter(f => f.severity === 'Minor')
log(`${st.key} review: ${review.verdict} (${fs.filter(f => f.severity === 'Blocker').length} B, ${fs.filter(f => f.severity === 'Major').length} M, ${minors.length} m)`)

// ---- Record + Minor list ----
phase('Fix')
const REC = `docs/design/v9/levels/supervisor/review-${st.key}.md`
await agent(`You save text and commit; change nothing else, never push, and return only the commit hash. Worktree: ${WT} (branch ${BR}; cd there).
1. Save the text between the ===== markers byte for byte as ${REC}.
2. Append these Minor findings to docs/design/v9/levels/minor-list.md (create it with the heading "# Moonfall Minor list" and a table header "| Stage | Level | Id | Issue |" if missing), one row each: ${minors.length ? minors.map(m => `| ${st.key} | ${m.level} | ${m.id} | ${m.issue.replace(/\|/g, '/')} |`).join(' ; ') : '(none: skip this step)'}
3. git add both files and commit with the message "docs: Moonfall ${st.key} review (verbatim) and Minor list" followed by a blank line and "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>".
=====
${review.report}
=====`, { label: `record ${st.key}`, phase: 'Fix', model: 'haiku', effort: 'low' })

if (!bm.length) return { stage: st.key, approved: true, integ: integ.commit, verdict: review.verdict, minors: minors.length }

const stageWide = bm.filter(f => !st.levels.includes(f.level))
const perLevel = st.levels.map(lvl => ({ lvl, items: bm.filter(f => f.level === lvl) })).filter(x => x.items.length)
const lf = await parallel(perLevel.map(({ lvl, items }) => () => agent(`${COMMON}
You fix level ${lvl} of stage ${st.key}, in your own worktree on its own branch. First run \`git merge --no-edit ${BR}\`. Fix exactly these review findings (the full review is ${REC}): ${items.map(f => `${f.id} [${f.severity}] ${f.issue}`).join(' ; ')}. Hold the difficulty: after any layout change run \`mfl.py tune ${lvl} --hold\`. Iterate with \`mfl.py check ${lvl}\`; finish with \`mfl.py level ${lvl}\` (must PASS). Touch only this level's files. Commit. Return level, branch, commit, changed, levelPassed, fixed, not fixed and why.`,
  { label: `fix ${lvl}`, phase: 'Fix', isolation: 'worktree', model: 'sonnet', effort: 'medium', schema: LEVELFIX_SCHEMA })))
const changed = lf.filter(f => f && f.changed)
const sfPrompt = `${COMMON}
You are the stage fixer for ${st.key}, in the worktree ${WT} (branch ${BR}; cd there). Merge these per-level fixer branches (--no-ff; each touches only its level): ${changed.map(f => `${f.branch} @ ${f.commit} (${f.level})`).join(', ') || '(none changed)'}.
${stageWide.length ? `Then fix these stage-wide findings: ${stageWide.map(f => `${f.id} [${f.severity}] ${f.issue}`).join(' ; ')}.` : ''} If the stage mean or finale rule fails, use \`mfl.py tune --stage ${st.key}\`. Re-convert changed levels and re-suggest Aces for every level whose layout changed. Write docs/design/v9/levels/supervisor/response-${st.key}.md answering every Blocker and Major (fixed how, or why not). Refresh the pack with \`mfl.py pack --stage ${st.key} --changed-since ${integ.commit}\`.
Then run in order and fix until each passes: \`mfl.py stage ${st.key}\`, \`mfl.py convert ${st.levels.join(' ')} --check\`, \`mfl.py selftest\`, \`pwsh tools/gate.ps1\`. ${LATER_FAILS} Never change a threshold or rule. Commit (never leave uncommitted changes).
Return the commit, a boolean per check (true only if run after your last edit and passing), a short summary, and what remains unfixed.`
let sf = await agent(sfPrompt, { label: `stage fix ${st.key}`, phase: 'Fix', model: 'sonnet', effort: 'medium', schema: STAGEFIX_SCHEMA })
if (!allPass(sf)) {
  log(`${st.key}: stage fixer not all green; one fresh retry`)
  sf = await agent(sfPrompt + `\nA previous stage fixer stopped before every check passed: ${sf ? JSON.stringify(sf).slice(0, 1500) : '(none)'}. Check git status and git log first and finish its work.`, { label: `stage fix ${st.key} retry`, phase: 'Fix', model: 'sonnet', effort: 'medium', schema: STAGEFIX_SCHEMA })
}
if (!allPass(sf)) return { stage: st.key, stopped: 'stage fixer checks failed', sf, findings: bm.map(f => `${f.id} ${f.level}: ${f.issue}`) }
return { stage: st.key, approved: true, integ: integ.commit, fixed: sf.commit, verdict: review.verdict, fixedFindings: bm.length, notFixed: sf.notFixed, minors: minors.length }
