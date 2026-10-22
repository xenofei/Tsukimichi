export const meta = {
  name: 'moonfall-refit-v2',
  description: 'Refit Moonfall MR1-MR6 to the Peggle rules with the new tools (generate, tune, check, level, pack, diff); parallel fixers; no round 2 on Minor-only reviews',
  phases: [
    { title: 'Author', detail: 'one author per level in its own worktree: generate, polish, tune, level' },
    { title: 'Integrate', detail: 'merge, tune --stage, stage, convert, Aces, gates, pack (mf-integrator)' },
    { title: 'Review', detail: 'game designer, UX/UI, level critic, from the pack only' },
    { title: 'Fold-in', detail: 'record; parallel per-level fixers in worktrees; stage fixer merges and checks' },
    { title: 'Verify', detail: 'round 2 from the diffs, only when round 1 had a Blocker or Major' },
  ],
}

const WT = args.worktree
const COMMON = `Read docs/team/agent-brief-common.md first and follow it. The level method is tools/moonfall-levels/README.md; the rules are docs/design/v9/levels/charter.md section 1 (Peggle-match rules of 9 October 2026: density 95-115 pieces with pegs r 10 and bricks t 20 by default, brick share 30-45% where the subject allows, orange candidates 60-90% of pieces with at most 30% in the lower third, the stage-mean band from Peggle's measured curve, the finale harder than the mean of its stage's other four levels, the playability floor 2 of 48, every Ace at the top 40% of wins; there is no longer any step rule between the other levels) and the owner's answers sections. Owner rules: every level is an illustrated scene whose pegs trace or emphasise its subject; rich and detailed; figures from game art; game art read at runtime, never shipped; nothing from Peggle (code, layouts, art, text) ever enters the repo. Tools: \`mfl.py check <id>\` is the 3-second inner loop (--json names each fault's piece, place and hint); \`mfl.py generate\`, \`tune\`, \`tune --hold\`, \`tune --stage\`, \`level\`, \`status\`, \`pack\`, \`diff\`, \`sheet\` are described in the README. Caches are shared across worktrees; if game textures are missing run \`py -3 tools/moonfall-levels/mfl.py fetch\`. Send long output to files in your scratchpad and print only verdicts.`

const LATER_FAILS = st => `Until every stage is refitted, the gates may fail ONLY these known, expected tests for levels of stages not yet refitted (${st.later.join(', ') || 'none'}): Every_shipped_level_passes_the_greedy_player and Shipped_Aces for those level ids. If MoonfallSceneStartTests.The_menus_thumbnail_is_the_levels_own_scene_scaled_down fails with "never settled", rerun that test alone (dotnet test --filter); it counts as passing only if it passes alone, and say so (its load sensitivity is being root-caused separately). Any other failure, or one of the expected ones for a level of this stage or an earlier stage, is a real failure.`

const AUTHOR_SCHEMA = { type: 'object', properties: { level: { type: 'string' }, branch: { type: 'string' }, commit: { type: 'string' }, levelPassed: { type: 'boolean' }, heldOutWins: { type: 'number' }, pieces: { type: 'number' }, notes: { type: 'string' } }, required: ['level', 'branch', 'commit', 'levelPassed', 'notes'] }
const INTEG_SCHEMA = { type: 'object', properties: { commit: { type: 'string' }, stagePassed: { type: 'boolean' }, convertPassed: { type: 'boolean' }, selftestPassed: { type: 'boolean' }, gatesPassed: { type: 'boolean' }, pack: { type: 'string' }, summary: { type: 'string' }, problems: { type: 'string' } }, required: ['commit', 'stagePassed', 'convertPassed', 'selftestPassed', 'gatesPassed', 'pack', 'summary', 'problems'] }
const LEVELFIX_SCHEMA = { type: 'object', properties: { level: { type: 'string' }, branch: { type: 'string' }, commit: { type: 'string' }, changed: { type: 'boolean' }, levelPassed: { type: 'boolean' }, fixed: { type: 'string' }, notFixed: { type: 'string' } }, required: ['level', 'branch', 'commit', 'changed', 'levelPassed', 'fixed', 'notFixed'] }
const STAGEFIX_SCHEMA = { type: 'object', properties: { commit: { type: 'string' }, gatesPassed: { type: 'boolean' }, stagePassed: { type: 'boolean' }, convertPassed: { type: 'boolean' }, selftestPassed: { type: 'boolean' }, diffs: { type: 'string' }, summary: { type: 'string' }, notFixed: { type: 'string' } }, required: ['commit', 'gatesPassed', 'stagePassed', 'convertPassed', 'selftestPassed', 'diffs', 'summary', 'notFixed'] }

const ROLES = [
  { key: 'game-designer', name: 'GAME DESIGNER', focus: 'fun, feel, pacing and spectacle under Peggle\'s rules, whether each dense board still reads as its subject and feels lavish, the stage as a set (theme, variety, curve, finale)' },
  { key: 'ux', name: 'UX/UI SPECIALIST', focus: 'readability of every peg kind at the larger sizes and higher density against each scene incl. colour-blind margins (F6, F9), motion near pieces, the launcher and aim guide, both window sizes (1280 and 640)' },
  { key: 'level-critic', name: 'LEVEL-DESIGN CRITIC', focus: 'playability under Peggle\'s rules (dead first shots, stuck balls, holdouts, the stage mean and finale on fresh seeds), whether the dense layout still traces its subject, the in-engine art against the pipeline composites, the pipeline checks' },
]
const verdict = t => (t || '').split('\n').find(l => /VERDICT/i.test(l)) || '(no verdict line)'
const count = (line, word) => { const m = line.match(new RegExp('(\\d+)\\s*' + word, 'i')); return m ? Number(m[1]) : null }
function severity(texts) {
  let blockers = 0, majors = 0, minors = 0, unclear = false
  for (const t of texts) {
    const v = verdict(t)
    const b = count(v, 'blocker'), M = count(v, 'major'), m = count(v, 'minor')
    if (b === null || M === null) unclear = true
    blockers += b || 0; majors += M || 0; minors += m || 0
  }
  return { blockers, majors, minors, unclear, approved: texts.every(t => /VERDICT:\s*APPROVE/i.test(verdict(t))) }
}
const allPass = f => f && f.gatesPassed && f.stagePassed && f.convertPassed && f.selftestPassed

// Sibling reviewers share one long identical prefix; only the role block at the end differs.
function reviewPrompt(role, st, pack, round, extra) {
  return `${COMMON}
You supervise Moonfall's levels (a game in the Tsukimichi FFXIV plugin that plays by Peggle's real rules). Review the REFIT of stage ${st.key} (levels ${st.levels.join(', ')}; ${st.codes.join(', ')}): the same subjects, made dense with Peggle-like orange shares and tuned to Peggle's measured curve. This is ${round}.
Work read-only in ${WT}: do not edit, build, commit or stash; run no dotnet build/test and no mfl.py build/convert/stage/selftest/generate/tune. Judge from the review pack at ${pack} (it holds every composite, engine render at 1280 and 640, trace sheet, report and the stage table) plus \`py -3 tools/moonfall-levels/mfl.py status --stage ${st.key}\`. Background: this stage's earlier approved reviews (docs/design/v9/levels/supervisor/*-${st.key}-round-*.md) show what was approved before the refit; findings there about the old step rule between levels are obsolete.
${extra}
Return your full report as your final message (it is saved verbatim). First line exactly: VERDICT: APPROVE|REVISE (B blockers, M majors, m minors), with the three numbers filled in. Then a findings table (id, severity, level, one-line issue, evidence). Then per-level verdicts and detail. Under about 12 KB, no preamble.
YOUR ROLE: ${role.name}. Your focus: ${role.focus}.`
}

function runAuthors(st) {
  if (st.authored || st.round3) return Promise.resolve(st.levels.map((l, i) => ({ level: l, branch: '(already merged)', commit: '', levelPassed: true, notes: 'authored earlier and merged' })))
  return pipeline(st.levels, (lvl, _, i) => st.given && st.given[lvl] ? Promise.resolve(Object.assign({ level: lvl, levelPassed: true, notes: 'authored outside the workflow' }, st.given[lvl])) : retryOnce(`author ${st.codes[i]}`, () => agent(`${COMMON}
You refit Moonfall level ${lvl} (${st.codes[i]}) of stage ${st.key} to the Peggle-match rules, in your own worktree on its own branch.
- First run \`git merge --no-edit moonfall-peggle\` (your worktree starts from main; you need the Peggle engine, rules and tools). Then \`py -3 tools/moonfall-levels/mfl.py fetch\` if textures are missing.${st.salvage && st.salvage[lvl] ? `
- An earlier author of this level was interrupted; its partial, unchecked work is on branch ${st.salvage[lvl]}. Merge it next (\`git merge --no-edit ${st.salvage[lvl]}\`; drop any build outputs or stray files it committed) and continue from it if it is on track, or start over if not.` : ''}
- Keep the level's subject, technique, source and crop, place tag and jewels. Start from \`mfl.py generate ${lvl} --variants 3\` (build/generate/${lvl}/, contact.png): pick the variant that best traces the subject, or densify the current layout if it traces better, and polish so the pegs plainly draw the subject. Iterate with \`mfl.py check ${lvl}\` (seconds), then \`mfl.py tune ${lvl}\` into this stage's band (charter 1.1, ${st.key}); ${st.codes[i].endsWith('-5') ? 'this is the finale: it must win fewer games than the mean of the stage\'s other four levels, so tune it to the lower part of the band.' : 'aim near the stage mean.'}${st.extra && st.extra[i] ? ' ' + st.extra[i] : ''}
- Touch only this level's own files (layout, scene recipe, painter if ours, asset). Rebuild the dress so nothing prints or crowds the larger pieces. Look at your composite and trace sheet (crop to 1568 px), also at 640.
- Finish with \`py -3 tools/moonfall-levels/mfl.py level ${lvl}\`; it must print PASS.
- Keep your context small; if it passes about 300k, commit what passes and return with notes.
Commit. Return branch (git branch --show-current), commit, whether \`level\` passed, held-out wins/48, piece count, short notes.`,
    { label: `author ${st.codes[i]}`, phase: 'Author', isolation: 'worktree', effort: 'high', model: st.authorModel, schema: AUTHOR_SCHEMA })))
}

async function retryOnce(label, run) {
  try { return await run() } catch (e) { log(`${label} failed to start (${String(e).slice(0, 160)}); one retry`); return run() }
}

async function record(st, texts, n, roles = ROLES) {
  return agent(`You save text byte for byte and commit; change nothing else, never push, and return only the commit hash. Worktree: ${WT} (branch moonfall-peggle). Save each text below verbatim as docs/design/v9/levels/supervisor/<role>-${st.key}-refit-round-${n}.md (role = the name in the ===== header), git add them, and commit with the message "docs: Moonfall ${st.key} refit supervisor round ${n} (verbatim)" followed by a blank line and "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>".
${roles.map((role, i) => `===== ${role.key} =====\n${texts[i] || '(missing)'}`).join('\n\n')}`, { label: `record r${n} ${st.key}`, phase: n === 1 ? 'Fold-in' : 'Verify', model: 'haiku', effort: 'low' })
}

async function foldIn(st, integCommit, records, round2, round = 1) {
  const lf = await parallel(st.levels.map((lvl, i) => () => agent(`${COMMON}
You fix level ${lvl} (${st.codes[i]}) of the ${st.key} refit after review round ${round}, in your own worktree on its own branch. First run \`git merge --no-edit moonfall-peggle\`. Read ${records}; fix every Blocker, Major and Minor about ${st.codes[i]} or ${lvl} (Nits where cheap); leave other levels and stage-wide items to the stage fixer. If no finding concerns this level, change nothing and return changed=false. Hold the difficulty: after any layout change run \`mfl.py tune ${lvl} --hold\`. Iterate with \`mfl.py check ${lvl}\`; finish with \`mfl.py level ${lvl}\` (must PASS). Touch only this level's files. Commit. Return level, branch, commit, changed, levelPassed, fixed, not fixed and why.`,
    { label: `fix${round > 1 ? ' r' + round : ''} ${st.codes[i]}`, phase: 'Fold-in', isolation: 'worktree', model: 'sonnet', effort: 'medium', schema: LEVELFIX_SCHEMA })))
  const changed = lf.filter(f => f && f.changed)
  const task = `Merge these per-level fixer branches (--no-ff; each touches only its level): ${changed.map(f => `${f.branch} @ ${f.commit} (${f.level}; level passed: ${f.levelPassed})`).join(', ') || '(none changed)'}. Their reports:
${lf.map((f, i) => `${st.codes[i]}: fixed: ${(f && f.fixed || '(no result)').slice(0, 400)} | not fixed: ${(f && f.notFixed || '').slice(0, 300)}`).join('\n')}
Then fix the stage-wide findings in ${records}; if the stage mean or finale rule fails, use \`mfl.py tune --stage ${st.key}\`. Re-convert changed levels and re-suggest Aces (MoonfallAces.Suggest, 576 games, into Modes/aces.json) for every level whose layout changed. Write docs/design/v9/levels/supervisor/response-${st.key}-refit-round-${round}.md answering every finding (fixed how, or why not).${round2 ? ` Refresh the pack with \`mfl.py pack --stage ${st.key} --changed-since ${integCommit}\` and, for each changed level, \`mfl.py diff <id> --rev ${integCommit}\` into the pack; return the diff PNG paths in diffs.` : ' Refresh the pack with `mfl.py pack --stage ' + st.key + ' --changed-since ' + integCommit + '`; diffs may be empty.'}`
  const prompt = `${COMMON}
You are the stage fixer for the refit of ${st.key}, in the worktree ${WT} (branch moonfall-peggle; cd there). ${task}
Then run in order and fix until each passes: \`py -3 tools/moonfall-levels/mfl.py stage ${st.key}\`, \`mfl.py convert ${st.levels.join(' ')} --check\`, \`mfl.py selftest\`, \`pwsh tools/gate.ps1\`. ${LATER_FAILS(st)} Never change a threshold or rule. Commit (never leave uncommitted changes).
Return the commit, a boolean per check (true only if run after your last edit and passing, allowing only the exceptions named above), diffs, a short summary, and what remains unfixed.`
  let sf = await agent(prompt, { label: `stage fix${round > 1 ? ' r' + round : ''} ${st.key}`, phase: 'Fold-in', model: 'sonnet', effort: 'medium', schema: STAGEFIX_SCHEMA })
  if (!allPass(sf)) {
    log(`${st.key}: stage fixer not all green (${sf ? sf.summary.slice(0, 120) : 'no result'}); one fresh retry`)
    sf = await agent(prompt + `\nA previous stage fixer stopped before every check passed: ${sf ? JSON.stringify(sf).slice(0, 1500) : '(none)'}. Check git status and git log first and finish its work.`, { label: `stage fix ${st.key} retry`, phase: 'Fold-in', model: 'sonnet', effort: 'medium', schema: STAGEFIX_SCHEMA })
  }
  return sf
}

// One targeted extra round when round 2 left Majors but no Blocker: fix only the dissenting reviewers' findings,
// and only those reviewers verify.
async function round3(st, baseCommit, dissent) {
  const records = dissent.map(r => `docs/design/v9/levels/supervisor/${r.key}-${st.key}-refit-round-2.md`).join(', ')
  const sf = await foldIn(st, baseCommit, records, true, 2)
  if (!allPass(sf)) return { approved: false, stopped: 'round 3 fixer checks failed', sf }
  const r3 = await parallel(dissent.map(role => () => agent(reviewPrompt(role, st, `docs/design/v9/levels/review/${st.key}/`, `round 3: verify the fix of your round-2 findings. The response is docs/design/v9/levels/supervisor/response-${st.key}-refit-round-2.md; the changes are the diff images ${sf.diffs || '(see the pack)'} and \`git diff ${baseCommit}..${sf.commit} --stat\`. Judge only your open findings and regressions in what changed; measure stuck and win figures on fresh seeds where a finding is about them`, `Your round-2 findings are in docs/design/v9/levels/supervisor/${role.key}-${st.key}-refit-round-2.md.`), { label: `r3 ${st.key} ${role.key}`, phase: 'Verify', effort: 'high' })))
  await record(st, r3, 3, dissent)
  const r3Lines = dissent.map((r, i) => `${r.key}: ${verdict(r3[i])}`)
  log(`${st.key} round 3: ${r3Lines.join(' | ')}`)
  return { approved: severity(r3).approved, round3: r3Lines }
}

const out = []
let prev = Promise.resolve()
const authorRuns = args.stages.map(st => { const r = prev.then(() => runAuthors(st)); prev = r; return r })
for (let k = 0; k < args.stages.length; k++) {
  const st = args.stages[k]
  st.later = args.stages.slice(k + 1).flatMap(s => s.levels)
  if (st.round3) {
    const r = await round3(st, st.round3.base, ROLES.filter(r => st.round3.roles.includes(r.key)))
    out.push(Object.assign({ stage: st.key }, r))
    if (!r.approved) return out
    continue
  }
  const authored = await authorRuns[k]
  const ok = authored.filter(a => a && a.levelPassed)
  if (ok.length < st.levels.length) { out.push({ stage: st.key, stopped: 'authors', authors: authored.map((a, i) => a ? { level: a.level, ok: a.levelPassed, wins: a.heldOutWins, notes: (a.notes || '').slice(0, 240) } : { level: st.levels[i], ok: false, notes: 'no result' }) }); return out }
  log(`${st.key} authored: ${ok.map(a => `${a.level} ${a.heldOutWins ?? '?'}/48 ${a.pieces ?? '?'}p`).join(', ')}`)
  const merges = st.authored && st.mergedNote ? st.mergedNote : st.authored ? 'Stage 1 refit authors are already merged on moonfall-peggle (b061b1fc..6414a39d); nothing to merge.' : `Merge these refit branches (--no-ff, keep both sides; the refit's level files win over the old ones): ${ok.map(a => a.branch + ' @ ' + a.commit).join(', ')}.`
  const integ = await agent(`${COMMON}
You integrate the refit of stage ${st.key} (${st.levels.join(', ')}) in the worktree ${WT} (branch moonfall-peggle; cd there; work and commit there).
1. ${merges}
2. \`py -3 tools/moonfall-levels/mfl.py sources\`, then \`mfl.py stage ${st.key} --json\` must PASS (band, finale rule, jewels). If it fails on the mean or the finale, run \`mfl.py tune --stage ${st.key}\` and re-check; say what moved.
3. \`mfl.py convert ${st.levels.join(' ')}\` then \`mfl.py convert ${st.levels.join(' ')} --check\` must PASS; refresh the runtime recipes, plates and level JSON under Tsukimichi.Core/Moonfall/Levels/; re-suggest these levels' Aces with MoonfallAces.Suggest (576 games) into Modes/aces.json.
4. \`mfl.py selftest\` and \`pwsh tools/gate.ps1\` must pass. ${LATER_FAILS(st)}
5. \`mfl.py pack --stage ${st.key}\` (the review pack in docs/design/v9/levels/review/${st.key}/). Commit.
Return the commit, booleans for stage, convert, selftest and gates (per the rule above), the pack path, a short summary and problems.`,
    { label: `integrate ${st.key}`, phase: 'Integrate', model: 'sonnet', effort: 'medium', schema: INTEG_SCHEMA })
  if (!integ || !(integ.stagePassed && integ.convertPassed && integ.selftestPassed && integ.gatesPassed)) { out.push({ stage: st.key, stopped: 'integrate', integ }); return out }
  log(`${st.key} integrated @ ${integ.commit.slice(0, 8)}: ${integ.summary.slice(0, 140)}`)

  const r1 = await parallel(ROLES.map(role => () => agent(reviewPrompt(role, st, integ.pack, 'round 1', st.reviewNote || ''), { label: `r1 ${st.key} ${role.key}`, phase: 'Review', effort: 'high' })))
  const s1 = severity(r1)
  const r1Lines = ROLES.map((r, i) => `${r.key}: ${verdict(r1[i])}`)
  log(`${st.key} round 1: ${r1Lines.join(' | ')}`)
  await record(st, r1, 1)
  const RECORDS = ROLES.map(r => `docs/design/v9/levels/supervisor/${r.key}-${st.key}-refit-round-1.md`).join(', ')

  if (s1.approved && s1.minors === 0) { out.push({ stage: st.key, approved: true, round1: r1Lines }); continue }
  const needRound2 = s1.unclear || s1.blockers + s1.majors > 0
  const sf = await foldIn(st, integ.commit, RECORDS, needRound2)
  if (!allPass(sf)) { out.push({ stage: st.key, stopped: 'stage fixer checks failed', round1: r1Lines, sf }); return out }
  if (!needRound2) { log(`${st.key}: Minor-only round 1 folded in; checks green, no round 2`); out.push({ stage: st.key, approved: true, round1: r1Lines, foldIn: sf.summary.slice(0, 300) }); continue }

  const r2 = await parallel(ROLES.map(role => () => agent(reviewPrompt(role, st, integ.pack, `round 2: verify the fold-in. The response is docs/design/v9/levels/supervisor/response-${st.key}-refit-round-1.md; the changes since round 1 are the diff images ${sf.diffs || '(see the pack)'} and \`git diff ${integ.commit}..${sf.commit} --stat\`. Judge only what changed and whether it resolved the findings; do not re-review unchanged levels`, `Your round-1 findings are in docs/design/v9/levels/supervisor/${role.key}-${st.key}-refit-round-1.md; check each and look for regressions.`), { label: `r2 ${st.key} ${role.key}`, phase: 'Verify', effort: 'high' })))
  await record(st, r2, 2)
  const r2Lines = ROLES.map((r, i) => `${r.key}: ${verdict(r2[i])}`)
  log(`${st.key} round 2: ${r2Lines.join(' | ')}`)
  const s2 = severity(r2)
  if (s2.approved) { out.push({ stage: st.key, approved: true, round1: r1Lines, round2: r2Lines }); continue }
  if (s2.unclear || s2.blockers > 0) { out.push({ stage: st.key, approved: false, round1: r1Lines, round2: r2Lines }); return out }
  const r3 = await round3(st, sf.commit, ROLES.filter((r, i) => !/VERDICT:\s*APPROVE/i.test(verdict(r2[i]))))
  out.push(Object.assign({ stage: st.key, round1: r1Lines, round2: r2Lines }, r3))
  if (!r3.approved) return out
}
return out