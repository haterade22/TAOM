export const meta = {
  name: 'improve-review',
  description: 'Review executed improve branches: deep-review lenses in waves, a lead that verifies, fixes and records, then up to maxRounds convergence rounds, each a convergence review and, while it finds defects and rounds remain, a fix pass',
  phases: [
    { title: 'Lenses', detail: 'deep-reviewer lenses per branch, defect lenses first, at most args.pool agents at once' },
    { title: 'Lead', detail: 'verifies lens and Codex findings, fixes, report, RCA, lessons, REVIEW-LOG, commit' },
    { title: 'Converge', detail: 'a convergence reviewer on the fix diff; a fix pass while it finds defects and rounds remain' },
  ],
}

// args, built by python tools/improve_ctl.py args review --items <json> ...:
//   rules, repo, runRoot, scratch, tmp, date, version, model { lead, convergence, fix }, pool (default 4)
//   maxRounds (default 2): convergence rounds after the lead; never a third unless the maintainer asks
//   items: [{ num, planSlug, branch, wt, base, head, title, lenses, files, codexOut?, tag?, note?, ref? }]
//     lenses: the deep-review routing improve_ctl.py computed ('1' to '7', 'tooling'); files: the
//     changed files grouped by kind, repo-relative; head: the branch tip the lenses review; tag: a
//     suffix for a second review (for example decisions); ref: how commits name the work (default
//     "plan <num>"); note: binding orchestrator text for every agent of the item. num plus tag names
//     an item (its labels and scratch folders), so a repeated pair is refused.
//   A top-level field rides in an items object: --items takes { "items": [...], "maxRounds": 1 } as
//   well as a plain array (--max-rounds does the same).
// Returns one entry per item: { num, branch, status: CLEAN | RESIDUAL | BLOCKED | FAILED, verdict,
//   report, lenses, lead, rounds: [{ round, fromRef, convergence, fix }], residual, needs_mike,
//   failures }. CLEAN only when the lead was READY FOR COMMIT and a convergence round returned CLEAN
//   with no finding. residual holds every finding the loop did not close: the last round's findings
//   when rounds ran out, what a fix pass left unfixed, a lead verdict other than READY FOR COMMIT,
//   and a DEFECTS verdict that listed no finding. BLOCKED: the lead or a fix pass stopped (its reason
//   is in needs_mike or not_fixed); a lead or fix pass that committed nothing is FAILED. Nothing is
//   dropped; the orchestrator takes residual findings to the maintainer.

// Every value that changes between runs comes from args. The standing rules for dispatched agents
// (references/dispatch-rules.md) arrive verbatim as args.rules, because a Workflow script cannot
// read files, and every prompt opens with them.
if (typeof args !== 'object' || args === null || typeof args.rules !== 'string' || args.rules.trim() === '') {
  throw new Error('review.js needs args.rules, the dispatch rules text: build its args with python tools/improve_ctl.py args review')
}
const RULES = args.rules
const DEFAULT_MODEL = 'claude-opus-5-5'
const POOL = Math.floor(Number(args.pool)) > 0 ? Math.floor(Number(args.pool)) : 4
const ITEMS = Array.isArray(args.items) ? args.items : []
const NO_RESULT = 'the agent returned no result (it died, was stopped or hit a usage limit); resume the run with the same script and resumeFromRunId, or dispatch it again. Never read this as a pass'

function need(obj, fields, where) {
  const missing = fields.filter(f => obj === null || typeof obj !== 'object' || obj[f] === undefined || obj[f] === null || obj[f] === '')
  if (missing.length) throw new Error(`${where}: missing ${missing.join(', ')}`)
}

// deep-review Step 2: the lens files, and "Waves of four": the defect lenses first, then the rest.
const LENSES = {
  1: ['Agent 1 Standards', '1-standards.md'],
  2: ['Agent 2 Engine compatibility', '2-engine-compat.md'],
  3: ['Agent 3 Efficiency', '3-efficiency.md'],
  4: ['Agent 4 Completeness', '4-completeness.md'],
  5: ['Agent 5 Data flow', '5-data-flow.md'],
  6: ['Agent 6 Design & Elegance', '6-design.md'],
  7: ['Agent 7 XML & ModuleData', '7-xml.md'],
  tooling: ['Tooling correctness', 'tooling.md'],
}
const WAVES = [['1', '2', '5', '7'], ['tooling', '3', '4', '6']]

const tagOf = it => (it.tag ? `-${String(it.tag).split('-').filter(Boolean).join('-')}` : '')
// num plus tag: it names the item's labels, and so its scratch folders.
const idOf = it => `${it.num}${tagOf(it)}`

need(args, ['repo', 'runRoot', 'scratch', 'tmp', 'date', 'version'], 'review.js args')
if (!ITEMS.length) throw new Error('review.js: args.items is empty')
ITEMS.forEach((it, i) => {
  const where = `review.js items[${i}]`
  need(it, ['num', 'planSlug', 'branch', 'wt', 'base', 'head', 'title', 'lenses'], where)
  if (!Array.isArray(it.lenses) || !it.lenses.length) throw new Error(`${where}: lenses must be a non-empty list`)
  const unknown = it.lenses.map(String).filter(k => !Object.prototype.hasOwnProperty.call(LENSES, k))
  if (unknown.length) throw new Error(`${where}: unknown lens ${unknown.map(k => `"${k}"`).join(', ')}; the lens ids are ${Object.keys(LENSES).join(', ')}`)
  if (ITEMS.findIndex(o => idOf(o) === idOf(it)) !== i) throw new Error(`${where}: item ${idOf(it)} is listed twice; give a second review of the same num a tag`)
})
const rounds = Math.floor(Number(args.maxRounds))
const MAX_ROUNDS = args.maxRounds !== undefined && rounds >= 0 ? rounds : 2

const modelFor = role => (args.model && typeof args.model[role] === 'string' && args.model[role]) || DEFAULT_MODEL

// Joins path parts with the separator the base already uses.
function join(base, ...parts) {
  const sep = String(base).includes('\\') ? '\\' : '/'
  let out = String(base)
  while (out.endsWith('\\') || out.endsWith('/')) out = out.slice(0, -1)
  for (const p of parts) out += sep + String(p).split('\\').join('/').split('/').join(sep)
  return out
}

// One pool for every agent this script starts, so no more than POOL run at once.
let running = 0
const waiting = []
function pump() {
  while (running < POOL && waiting.length) {
    const job = waiting.shift()
    running++
    Promise.resolve().then(job.start).then(
      v => { running--; job.resolve(v); pump() },
      e => { running--; job.reject(e); pump() },
    )
  }
}
const limited = start => new Promise((resolve, reject) => { waiting.push({ start, resolve, reject }); pump() })

// The role comes first, right after the standing rules, because it decides which of their sections
// bind the agent; then the values of the rules' placeholders.
const EDITING = 'YOUR ROLE: EDITING. You edit and commit, so the standing rules\' section "Editing roles add" applies to you as well.'
const READ_ONLY = 'YOUR ROLE: READ-ONLY. You never edit, stage or commit anything in the repository, so the standing rules\' section "Read-only roles add" applies to you as well; your only writable paths are the ones this prompt names.'
function runValues(o) {
  return [
    o.editing ? EDITING : READ_ONLY,
    '',
    'RUN VALUES (they fill the placeholders in the standing rules above):',
    `- <repo>, the main checkout: ${args.repo}`,
    `- <worktree>: ${o.worktree || 'none: read the repository through git as your task says, and run shell commands from your scratch folder'}`,
    `- <base>: ${o.base || 'the commit your task names'}`,
    `- <scratch>: ${args.scratch}; <your label>: ${o.label}; your scratch folder: ${join(args.scratch, o.label)}`,
    `- <tmp>: ${args.tmp}`,
    `- <version>: ${args.version}; today: ${args.date}`,
    '- ORCHESTRATOR NOTE (it overrides the standing rule "Workspace" for these paths only): a file or folder this prompt names as yours to write may lie inside the main checkout, where the run folder lives; write it there, and touch nothing else in the main checkout.',
  ].join('\n')
}

// The only doors to agent(): the prompt opens with the standing rules and the run values, and the
// call waits for a pool slot. Each settles to { ok, value } or { ok: false, failure }, so a dead
// agent (a null or empty result) or a failed call is reported, never passed on as data.
const settle = (label, p) => p.then(
  v => (v === null || v === undefined || (typeof v === 'string' && !v.trim())
    ? { ok: false, failure: `${label}: ${NO_RESULT}` }
    : { ok: true, value: v }),
  e => ({ ok: false, failure: `${label}: the agent call failed: ${String(e)}` }),
)
function runAgent(role, text, o) {
  return settle(o.label, limited(() => agent(`${RULES}\n\n${runValues(o)}\n\n${text}`, {
    label: o.label, phase: o.phase, schema: o.schema, model: modelFor(role), effort: o.effort || 'high',
  })))
}
// A deep-reviewer's definition pins its model and effort, so this call never passes either. Its
// report is text in the lens file's format, read by the lead.
function runLens(text, o) {
  return settle(o.label, limited(() => agent(`${RULES}\n\n${runValues(o)}\n\n${text}`, {
    label: o.label, phase: o.phase, agentType: 'deep-reviewer',
  })))
}

const LEAD_SCHEMA = {
  type: 'object',
  properties: {
    verdict: { type: 'string', enum: ['READY FOR COMMIT', 'NEEDS FIXES', 'BLOCKED'] },
    confirmed: { type: 'number' },
    false_positives: { type: 'number' },
    needs_mike: { type: 'array', items: { type: 'string' } },
    applied: { type: 'number' },
    not_applied: { type: 'number' },
    commit: { type: 'string' },
    suite_totals: { type: 'string' },
    sweep: { type: 'string' },
    codex: { type: 'string', enum: ['included', 'pending', 'not run'] },
  },
  required: ['verdict', 'confirmed', 'false_positives', 'needs_mike', 'applied', 'not_applied', 'commit', 'suite_totals', 'sweep', 'codex'],
}

const CONVERGENCE_SCHEMA = {
  type: 'object',
  properties: {
    findings: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          severity: { type: 'string', enum: ['HIGH', 'MED', 'LOW'] },
          location: { type: 'string' },
          claim: { type: 'string' },
          proof: { type: 'string' },
          fix: { type: 'string' },
        },
        required: ['severity', 'location', 'claim', 'proof', 'fix'],
      },
    },
    evidence_summary: { type: 'string' },
    verdict: { type: 'string', enum: ['CLEAN', 'DEFECTS'] },
  },
  required: ['findings', 'evidence_summary', 'verdict'],
}

const FIX_SCHEMA = {
  type: 'object',
  properties: {
    status: { type: 'string', enum: ['DONE', 'BLOCKED'] },
    commit: { type: 'string' },
    fixed: { type: 'array', items: { type: 'string' } },
    false_positives: { type: 'array', items: { type: 'string' } },
    not_fixed: { type: 'array', items: { type: 'string' } },
    suite_totals: { type: 'string' },
    sweep: { type: 'string' },
  },
  required: ['status', 'commit', 'fixed', 'false_positives', 'not_fixed', 'suite_totals', 'sweep'],
}

const SKILLS = join(args.repo, '.claude', 'skills')
const refOf = it => String(it.ref || `plan ${it.num}`)
const reportOf = it => `docs/reviews/deep-review-${it.num}-${it.planSlug}${tagOf(it)}-${args.date}.md`
const rcaOf = it => `docs/reviews/rca-${it.planSlug}${tagOf(it)}-${args.date}.md`
const planFile = it => join(it.wt, 'plans', `${it.num}-${it.planSlug}.md`)
const lensesOf = it => it.lenses.map(String)
const noteOf = it => (it.note ? `ORCHESTRATOR NOTE (binding; it overrides the instructions here where it says so):\n${it.note}\n` : '')

function fileList(it) {
  const groups = it.files && typeof it.files === 'object' ? Object.values(it.files) : []
  const paths = groups.flatMap(g => (Array.isArray(g) ? g : []))
  return paths.length
    ? paths.map(p => join(it.wt, p)).join('\n')
    : `(no list given: take it from git -C "${it.wt}" diff --name-only ${it.base}..${it.head})`
}

// The standing rules give the hook suite's command; this adds the worktree it must judge.
const HOOK_SUITE = it => `When you run the hook suite (standing rule "Build and test"), run it from ${it.wt} with CLAUDE_PROJECT_DIR="${it.wt}" set, so the hooks it starts judge this worktree and not the main checkout.`
// The skill's stop rule: a gate change is proved against the old gate before it ships.
const GATE_SWEEP = 'If the diff changes a gate (a hook, a validator or a CI step), run a differential sweep of the old gate (git show <base>:<path> into your scratch folder) against the new one, over a corpus seeded from the fix\'s own mechanism, under both the Bash and PowerShell tool names, and report in sweep every command the old gate refused and the new one allows (sweep is empty when no gate changed).'

// A later wave carries the earlier waves' lines that name a CRITICAL or HIGH finding (deep-review
// Step 2), so the design lens does not polish code about to be restructured. They are leads only.
function highLines(reports) {
  const lines = []
  for (const r of reports) {
    const hits = String(r.text).split('\n').filter(l => l.includes('CRITICAL') || l.includes('HIGH')).slice(0, 12)
    for (const h of hits) lines.push(`- ${LENSES[r.key][0]}: ${h.trim().slice(0, 300)}`)
  }
  return lines.length
    ? `EARLIER WAVES' CRITICAL AND HIGH LINES (verbatim leads, unverified; the lead verifies them):\n${lines.join('\n')}\n`
    : ''
}

const secondReview = it => (it.tag
  ? `THIS IS A SECOND REVIEW: the branch was reviewed before, and this diff applies the maintainer decisions listed in the "## Maintainer decisions applied" section of the branch's earlier review record (docs/reviews/deep-review-${it.num}-${it.planSlug}*.md). Those behaviour changes are decided, not findings: review whether each is implemented correctly and completely. `
  : '')

const lensPrompt = (it, key, high) => `Lens: ${LENSES[key][0]}. Read ${join(SKILLS, 'deep-review', 'lenses', LENSES[key][1])} first; it is your whole task and its output format.
FILES (inside the worktree ${it.wt}; review THESE copies, never the main checkout's working tree, which holds other sessions' edits):
${fileList(it)}
SCOPE NOTES: this change is "${it.title}" (${refOf(it)}) on branch ${it.branch} in the worktree ${it.wt}. The diff under review: git -C "${it.wt}" diff ${it.base}..${it.head} . Its intent, scope and STOP conditions are in ${planFile(it)} when that file exists, otherwise in that range's commit messages. ${secondReview(it)}Only the changed hunks are in scope for defects; pre-existing issues you notice go under FOLLOW-UP. Path-scoped rules do not load for files outside the main checkout: read the relevant ones from ${join(it.wt, '.claude', 'rules')} yourself. Engine signatures: pwsh tools/taom-src.ps1 path <Type> (run it from ${args.repo}; it only reads).
${high}${noteOf(it)}`

// deep-review Step 2b: a CRITICAL violation from Agent 1 calls for one more, adversarial reviewer,
// which only the orchestrator can start; the lead confirms the finding and asks for it.
const ESCALATE = 'ADVERSARIAL ESCALATION: the Agent 1 Standards report has a line naming CRITICAL. If it reports a CRITICAL violation (deep-review Step 2b: a sealed TaleWorlds type in a service, a Harmony patch touching game state without an adapter, an entry point over 150 lines doing business logic), deep-review runs one more deep-reviewer on the offending files with lenses/adversarial.md, which this workflow cannot start: make your first needs_mike line "ADVERSARIAL ESCALATION: <the violation and its files>" so the orchestrator runs it.\n'

const leadPrompt = (it, reports, critical) => `ROLE: the REVIEW LEAD for "${it.title}" (${refOf(it)}) on branch ${it.branch} in the worktree ${it.wt} (diff ${it.base}..${it.head}). You act as the /deep-review orchestrator's delegate for Steps 3, 3e and 4, and as /review-codex Phase 3.
Read first: ${join(SKILLS, 'deep-review', 'SKILL.md')} (Steps 3, 3e, 4 and "HIGH findings") and ${join(SKILLS, 'review-codex', 'SKILL.md')} (Phase 3).

INPUTS
1. The deep-review lens reports, verbatim, below.
2. The Codex adversarial review: ${it.codexOut ? `${it.codexOut}. It is complete only if it holds the line "END OF CODEX REVIEW". If the file is missing or incomplete, record "Codex pending" in the report and continue without it.` : 'none was run for this item; record "Codex not run" in the report.'}
${noteOf(it)}${critical ? ESCALATE : ''}
YOUR JOB
A. Verify every finding (lens and Codex) against the code in the worktree before acting on it: a finding is a hypothesis. Classify each as CONFIRMED, FALSE POSITIVE (say why) or NEEDS MIKE (a design or product decision).
B. Fix every CONFIRMED defect in the changed code, test first where it is testable. HIGH findings are fixed by default; one that truly cannot be fixed here gets a 'Deferred: <reason>' trailer and a line in the report.
C. Step 4 improvements (Agent 3 APPLY-scoped fixes and Agent 6 KEEP proposals, changed code only): apply the behaviour-PRESERVING ones with a characterisation test green before and after. Do not apply behaviour-CHANGING ones: list each under NOT APPLIED and in needs_mike. FOLLOW-UP proposals (pre-existing code) are listed, never applied.
D. Run the full suite in the worktree and match the base's totals (standing rule "Build and test"). ${HOOK_SUITE(it)} ${GATE_SWEEP}
E. Write, inside the worktree: ${reportOf(it)} (the Step 3 DEEP REVIEW REPORT with the IMPROVEMENTS lists and the VERDICT, plus a CODEX REVIEW section with the Phase 3d assessment table); for any confirmed finding, ${rcaOf(it)} (Step 3e: summary, findings table, why each agent missed it) and one '### <rule>' entry per systemic lesson appended to the matching docs/reviews/lessons/<category>.md; and one entry appended to docs/reviews/REVIEW-LOG.md (review-codex Phase 3i), dated ${args.date}, whose heading carries no review number (the orchestrator assigns them). Do not edit AGENTS.md or any file under .ai/: list the Codex lessons you would add under "AGENTS.md lessons (pending)" in the report; the orchestrator consolidates them at wrap-up.
F. Commit on the branch (standing rule "Commit"): subject 'fix(<scope>): ${args.version} - review follow-ups for ${refOf(it)}' (adjust the scope; at most 72 characters), a body naming the report and RCA paths. If nothing needed fixing, still commit the record files, with subject 'docs(reviews): ${args.version} - deep review for ${refOf(it)}'.

LENS REPORTS:
${reports}

Return the structured result: the verdict, the counts, every NEEDS MIKE item (one line each), the commit (the branch HEAD after your last commit), the final full-suite totals line, the gate sweep result, and whether Codex was included, pending or not run. The verdict is READY FOR COMMIT when every confirmed defect is fixed and committed, NEEDS FIXES when one is left open, and BLOCKED when you had to stop (a STOP rule, a refused commit): then put the reason first in needs_mike and return the commit you reached, or an empty one.`

const convergePrompt = (it, round, fromRef, earlier) => `ROLE: convergence reviewer, round ${round} of at most ${MAX_ROUNDS} (deep-review Step 4 item 6) for "${it.title}" (${refOf(it)}). You write no repository file.
Read ${join(SKILLS, 'deep-review', 'SKILL.md')} Step 4 first.
Review ONLY the fix diff: git -C "${it.wt}" diff ${fromRef}..HEAD (branch ${it.branch}; read files only from ${it.wt}). The review record is ${join(it.wt, reportOf(it))}: decisions it lists as the maintainer's are binding, not findings.
Check the standards and behaviour parity of the applied fixes and improvements, and that each fix removes the defect it names. Report DEFECTS only, each with file:line, the claim, a proving read or command, and the fix; try to refute each of your own findings before you report it. You may NOT open a new design round.
${earlier.length ? `The previous round's findings, which a fix pass then addressed and recorded in the report's "Convergence round ${round - 1}" section (check that each fix holds and that each false-positive claim is right; do not report a fixed one again):\n${earlier.map(f => `- ${JSON.stringify(f)}`).join('\n')}\n` : ''}${noteOf(it)}
Return the structured result; the verdict is CLEAN only if you have no finding.`

const fixPrompt = (it, round, fromRef, findings) => `ROLE: the review lead's fix pass after convergence round ${round}, for "${it.title}" (${refOf(it)}) on branch ${it.branch} in the worktree ${it.wt}.
The convergence reviewer found defects in git -C "${it.wt}" diff ${fromRef}..HEAD. Verify each against the code; fix the confirmed ones, test first where testable; run the full suite and match the base's totals (standing rule "Build and test"). ${HOOK_SUITE(it)} ${GATE_SWEEP}
Append a section "Convergence round ${round}" to ${reportOf(it)}: each finding as fixed (with the commit), false positive (with why) or not fixed (with why). Commit (standing rule "Commit"): subject 'fix(<scope>): ${args.version} - convergence fixes for ${refOf(it)}' (adjust the scope; at most 72 characters). Do not open new design work.
${noteOf(it)}
CONVERGENCE FINDINGS:
${findings.map(f => `- ${JSON.stringify(f)}`).join('\n')}

Return the structured result: the status (DONE, or BLOCKED when you had to stop before committing, with the reason in not_fixed), the commit (the branch HEAD after your commit), what you fixed, what was a false positive and why, what you did not fix and why, the final full-suite totals line, and the gate sweep result.`

async function reviewOne(it) {
  const id = idOf(it)
  const res = {
    num: it.num, branch: it.branch, status: '', verdict: '', report: reportOf(it), lenses: [], lead: null,
    rounds: [], residual: [], needs_mike: [], failures: [],
  }
  const stop = (status, failure) => { res.failures.push(failure); res.status = status; return res }
  const fail = failure => stop('FAILED', failure)

  // Lenses, wave by wave; a dead lens stops the item before the lead, whose review would be partial.
  const reports = []
  let high = ''
  for (const wave of WAVES) {
    const keys = wave.filter(k => lensesOf(it).includes(k))
    if (!keys.length) continue
    const got = await Promise.all(keys.map(async key => ({
      key,
      r: await runLens(lensPrompt(it, key, high), { label: `lens-${id}-${key}`, phase: 'Lenses', worktree: it.wt, base: it.base }),
    })))
    const waveReports = []
    for (const g of got) {
      res.lenses.push(g.key)
      if (!g.r.ok) res.failures.push(g.r.failure)
      else waveReports.push({ key: g.key, text: typeof g.r.value === 'string' ? g.r.value : JSON.stringify(g.r.value) })
    }
    if (res.failures.length) { res.status = 'FAILED'; return res }
    reports.push(...waveReports)
    high += highLines(waveReports)
  }

  const critical = reports.some(r => r.key === '1' && r.text.includes('CRITICAL'))
  const lead = await runAgent('lead', leadPrompt(it, reports.map(r => `===== ${LENSES[r.key][0]} =====\n${r.text}`).join('\n\n'), critical), {
    label: `lead-${id}`, phase: 'Lead', schema: LEAD_SCHEMA, editing: true, worktree: it.wt, base: it.base,
  })
  if (!lead.ok) return fail(lead.failure)
  res.lead = lead.value
  res.verdict = lead.value.verdict
  res.needs_mike = lead.value.needs_mike || []
  // A lead that stopped, or committed nothing, leaves nothing for convergence to review.
  if (lead.value.verdict === 'BLOCKED') return stop('BLOCKED', `lead-${id}: the lead stopped (BLOCKED); its reason is first in needs_mike`)
  if (!lead.value.commit) return fail(`lead-${id}: the lead returned no commit, so its fixes and records are not on the branch`)
  if (lead.value.verdict !== 'READY FOR COMMIT') res.residual.push({ note: `the lead's verdict is ${lead.value.verdict}, not READY FOR COMMIT: its report lists what it left open` })

  // Convergence: each round reviews what the previous step committed. A fix pass runs only when a
  // later round will review it, so no fix ships unreviewed; findings still open when the rounds run
  // out, and what a fix pass left unfixed, go to residual.
  let fromRef = it.head
  let reviewedTo = lead.value.commit
  let earlier = []
  if (MAX_ROUNDS === 0) res.residual.push({ note: `no convergence round ran (maxRounds 0): the lead's fixes in ${it.head}..HEAD are unreviewed` })
  for (let round = 1; round <= MAX_ROUNDS; round++) {
    const conv = await runAgent('convergence', convergePrompt(it, round, fromRef, earlier), {
      label: `converge-${id}-r${round}`, phase: 'Converge', schema: CONVERGENCE_SCHEMA, effort: 'max', worktree: it.wt, base: it.base,
    })
    if (!conv.ok) {
      res.residual.push({ round, note: `convergence round ${round} did not run: ${fromRef}..HEAD is unreviewed` })
      return fail(conv.failure)
    }
    const entry = { round, fromRef, convergence: conv.value, fix: null }
    res.rounds.push(entry)
    const findings = conv.value.findings || []
    if (conv.value.verdict === 'CLEAN' && !findings.length) break
    // A verdict other than CLEAN with nothing to fix gives a fix pass nothing to act on.
    if (!findings.length) {
      res.residual.push({ round, note: `convergence round ${round} returned verdict ${conv.value.verdict} with no findings listed: read its evidence_summary` })
      break
    }
    if (round === MAX_ROUNDS) {
      res.residual.push(...findings.map(f => ({ round, ...f })))
      break
    }
    const fix = await runAgent('fix', fixPrompt(it, round, fromRef, findings), {
      label: `fix-${id}-r${round}`, phase: 'Converge', schema: FIX_SCHEMA, editing: true, worktree: it.wt, base: it.base,
    })
    if (!fix.ok) {
      res.residual.push(...findings.map(f => ({ round, ...f })))
      return fail(fix.failure)
    }
    entry.fix = fix.value
    if (fix.value.status === 'BLOCKED' || !fix.value.commit) {
      res.residual.push(...findings.map(f => ({ round, ...f })))
      return fix.value.status === 'BLOCKED'
        ? stop('BLOCKED', `fix-${id}-r${round}: the fix pass stopped (BLOCKED); its reason is in not_fixed`)
        : fail(`fix-${id}-r${round}: the fix pass returned no commit, so its fixes are not on the branch`)
    }
    res.residual.push(...(fix.value.not_fixed || []).map(nf => ({ round, not_fixed: nf })))
    // The next round reviews only this fix pass.
    fromRef = reviewedTo
    reviewedTo = fix.value.commit
    earlier = findings
  }
  res.status = res.residual.length ? 'RESIDUAL' : 'CLEAN'
  return res
}

phase('Lenses')
// One item's exception (a result that breaks its schema) fails that item only.
const results = await Promise.all(ITEMS.map(it => reviewOne(it).catch(e => ({
  num: it.num, branch: it.branch, status: 'FAILED', rounds: [], residual: [], failures: [`review of ${idOf(it)} threw: ${String(e)}`],
}))))
for (const r of results) {
  log(`${r.num}: ${r.status}${r.verdict ? `, lead ${r.verdict}` : ''}, ${r.rounds.length} convergence round(s), ${r.residual.length} residual${r.failures.length ? `; ${r.failures.join('; ')}` : ''}`)
}
return results
