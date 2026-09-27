export const meta = {
  name: 'improve-plans',
  description: 'Write, extend or re-review self-contained handoff plans: a writer, then up to reviewRounds fresh cold reviewers, each followed by a reviser when it finds blocking items or excerpt mismatches',
  phases: [
    { title: 'Write', detail: 'a writer or an extender per plan (review mode skips it)' },
    { title: 'Review', detail: 'a fresh cold reviewer per round' },
    { title: 'Revise', detail: 'a reviser, only after a review with blocking items or excerpt mismatches' },
  ],
}

// args, built by python tools/improve_ctl.py args plans --items <json> ...:
//   rules, repo, runRoot, scratch, tmp, date, version, model { writer, reviewer, reviser }, pool (default 4)
//   base: the commit the plans are written against; baseline: its measured totals, one line
//   planDir: where the plan files live; reviewDir: where review files go (default runRoot)
//   codeRoot (optional): a worktree at base to read code from; without it, code is read through git
//   reviewRounds (default 1; 2 for a plan that changes a safety gate)
//   items: [{ num, slug, title, priority, category, depends_on, brief, mode: 'write'|'extend'|'review',
//             reviewRounds?, safetyGate? }]
//     brief: the orchestrator's verified brief (write) or extension brief (extend); safetyGate: the
//     plan changes a hook that refuses or asks, so its reviewers simulate the planned code.
// Returns one entry per item: { num, slug, mode, status: CLEAN | REVISED | NOT_PLANNED | FAILED,
//   writer, rounds: [{ round, file, review, revise }], residual, failures }. REVISED means the last
//   revision was not re-reviewed; residual lists what its reviser could not fix.

// Every value that changes between runs comes from args. The standing rules for dispatched agents
// (references/dispatch-rules.md) arrive verbatim as args.rules, because a Workflow script cannot
// read files, and every prompt opens with them.
if (typeof args !== 'object' || args === null || typeof args.rules !== 'string' || args.rules.trim() === '') {
  throw new Error('plans.js needs args.rules, the dispatch rules text: build its args with python tools/improve_ctl.py args plans')
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
need(args, ['repo', 'runRoot', 'scratch', 'tmp', 'date', 'version', 'base', 'baseline', 'planDir'], 'plans.js args')
if (!ITEMS.length) throw new Error('plans.js: args.items is empty')
ITEMS.forEach((p, i) => {
  need(p, ['num', 'slug', 'title', 'mode'], `plans.js items[${i}]`)
  if (!['write', 'extend', 'review'].includes(p.mode)) throw new Error(`plans.js items[${i}]: mode must be write, extend or review, not ${p.mode}`)
  if (p.mode !== 'review') need(p, ['brief'], `plans.js items[${i}]`)
})

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

// The only door to agent(): the prompt opens with the standing rules and the run values, and the
// call waits for a pool slot. It settles to { ok, value } or { ok: false, failure }, so a dead agent
// (a null result) or a failed call is reported, never passed on as data.
function runAgent(role, text, o) {
  return limited(() => agent(`${RULES}\n\n${runValues(o)}\n\n${text}`, {
    label: o.label, phase: o.phase, schema: o.schema, model: modelFor(role), effort: o.effort || 'high',
  })).then(
    v => (v === null || v === undefined ? { ok: false, failure: `${o.label}: ${NO_RESULT}` } : { ok: true, value: v }),
    e => ({ ok: false, failure: `${o.label}: the agent call failed: ${String(e)}` }),
  )
}

const WRITER_SCHEMA = {
  type: 'object',
  properties: {
    status: { type: 'string', enum: ['WRITTEN', 'NOT_PLANNED'] },
    path: { type: 'string' },
    steps: { type: 'number' },
    riskiest_assumption: { type: 'string' },
    not_planned_reason: { type: 'string' },
  },
  required: ['status', 'path', 'steps', 'riskiest_assumption', 'not_planned_reason'],
}

const REVIEW_SCHEMA = {
  type: 'object',
  properties: {
    blocking: { type: 'array', items: { type: 'string' } },
    non_blocking: { type: 'array', items: { type: 'string' } },
    excerpt_mismatches: { type: 'array', items: { type: 'string' } },
    executable_by_weak_model: { type: 'boolean' },
  },
  required: ['blocking', 'non_blocking', 'excerpt_mismatches', 'executable_by_weak_model'],
}

const REVISE_SCHEMA = {
  type: 'object',
  properties: {
    summary: { type: 'string' },
    not_fixed: { type: 'array', items: { type: 'string' } },
  },
  required: ['summary', 'not_fixed'],
}

const TEMPLATE = join(args.repo, '.claude', 'skills', 'improve', 'references', 'plan-template.md')
const REVIEW_DIR = args.reviewDir || args.runRoot
const planPath = p => join(args.planDir, `${p.num}-${p.slug}.md`)
// plan-review-<num>.md for the first review of a new plan; a suffix names the mode and later rounds.
const reviewFile = (p, round) => join(REVIEW_DIR, `plan-review-${p.num}${p.mode === 'write' ? '' : `-${p.mode}`}${round > 1 ? `-r${round}` : ''}.md`)
const roundsFor = p => {
  const n = Math.floor(Number(p.reviewRounds !== undefined ? p.reviewRounds : args.reviewRounds))
  return n > 0 ? n : 1
}

const CODE = args.codeRoot
  ? `CODE LOCATION: read every repository file from the worktree ${args.codeRoot}, checked out at ${args.base}; never from the main checkout, whose working tree holds other sessions' uncommitted edits. Paths you write into the plan stay repo-relative.`
  : `CODE LOCATION: read every repository file at ${args.base} through git (git -C "${args.repo}" show ${args.base}:<path>, git -C "${args.repo}" grep ${args.base}); the main checkout's working tree holds other sessions' uncommitted edits, so its files are not evidence. Paths you write into the plan stay repo-relative.`

const COMMANDS = `Use exactly these non-deploying commands in the plan:
- Build: dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=
- Test: dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=
- Filtered test: dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"
- Data: python tools/validate_moduledata.py
- Docs: python tools/lint_docs.py --fail-on-drift
Never ./build.ps1. Baseline at ${args.base}: ${args.baseline}`

const writePrompt = p => `ROLE: plan writer for plan ${p.num}. You WRITE ONE PLAN FILE: ${planPath(p)} (create it). It is your only writable path besides your scratch folder.
${CODE}
Read the plan template ${TEMPLATE} completely and follow it exactly, including its "TAOM additions" and "Quality bar". The run folder ${args.runRoot} holds the audit record (BRIEF.md, REPORT.md, the lane and verify files): leads, not facts.

PLAN ${p.num}: ${p.title}
Priority ${p.priority || 'unset'}; category ${p.category || 'unset'}; depends on ${p.depends_on || 'none'}.
THE ORCHESTRATOR'S VERIFIED BRIEF (the verified facts, the checkers' corrections, the maintainer decisions, and the decisions you must NOT take):
${p.brief}

Rules for writing it:
- The executor has ZERO context: inline every fact, path, excerpt and convention it needs. Never write "see the audit" or "as discussed".
- Open every file you cite and copy excerpts from YOUR OWN reads at ${args.base}. Lane and verify files are leads, not facts.
- Engine signatures: pwsh tools/taom-src.ps1 path <Type> gives a decompiled file; quote the signature you rely on.
- Planned at: commit ${args.base}, ${args.date}. The drift check command names the exact in-scope paths.
- C# work is TDD: the failing test step comes before the implementation step, with the exact test name and file.
- Issue-first: the Status line names the issue the brief gives, or says "Issue: filed by the orchestrator before execution" when the brief names none.
- Name the binding ADRs and rules with one-line summaries (ADR-002 thin entry points under 150 lines, ADR-007 adapters, ADR-008 test coverage, the csharp-architecture.md rules that apply).
- Protected files (.claude/settings.json, .claude/settings.local.json, Directory.Build.props, docs/adrs/*.md): the executor may not edit them. Put such an edit in a Step 0 for the maintainer, with the exact edit and a check the executor runs first (it STOPs if the check fails). Single-owner files (Main/IoC.cs, Main/SubModule.cs, Main/TAOM.csproj): list them in scope with the exact edit, or say "recommend, don't edit" and give the exact line to report.
- Never name a worktree path or a branch name: the orchestrator chooses them at execution time, so write "your worktree" and "your branch".
- No CHANGELOG step: CHANGELOG.md is generated at /release, and the commit body is the changelog entry.
- STOP conditions specific to this plan's real risks. Machine-checkable done criteria.
- Git workflow: commit subject '<type>(<scope>): vX.Y.Z - <description>', where vX.Y.Z is the <Version> in Main/_Module/SubModule.xml when the executor commits; at most 72 characters; no AI attribution trailer; explicit paths only; never push.
- Prose: no em or en dashes (commas, colons, parentheses instead); code spans are exempt. No secret values.
- If the finding turns out wrong or already fixed when you read the code, write no plan: write the evidence to ${planPath(p)} instead and return status NOT_PLANNED with the reason.
${COMMANDS}

Return the structured result: the status, the path, the step count, the riskiest assumption, and the reason when NOT_PLANNED (an empty string otherwise).`

const extendPrompt = p => `ROLE: plan extender for plan ${p.num} ("${p.title}"). You EXTEND one existing, reviewed plan: ${planPath(p)}, your only writable path besides your scratch folder.
${CODE}
Read the plan completely first, then the plan template ${TEMPLATE} and the extension brief below.

THE EXTENSION BRIEF (what to add, the maintainer decisions behind it, and what NOT to decide):
${p.brief}

Integrate the addition the way the plan already handles its existing scope, as one coherent plan for a zero-context executor: Status, Why, Current state (excerpts from YOUR OWN reads at ${args.base}), Scope (the drift check command names every in-scope path), one RED/GREEN cycle per addition in the plan's existing step style with a verification command and an exact expected output for each step, updated literal expected outputs, test counts and the expected full-suite total, STOP conditions for the new risks, done criteria and maintenance notes. Keep every existing, already-reviewed part unless the addition forces a change; renumber steps only if you must, and then fix every cross-reference. Prose: no em or en dashes.
${COMMANDS}

Return the structured result: status WRITTEN, the path, the new step count, the riskiest new assumption, and an empty not_planned_reason.`

const SAFETY = `SAFETY GATE: this plan changes a safety gate (a hook that refuses or asks). A fail-open defect silently disables a guard, so your first job is to find any step whose literal execution would make a gate allow what it refused before, or crash (a hook may fail open only on an internal error, never on a parse slip that drops the command). SIMULATE the planned code: copy it into your scratch folder, run the plan's test rows and adversarial rows of your own under both tool names (Bash and PowerShell payloads) against the planned hooks, and compare the verdicts with the hooks at ${args.base}. Report any row whose verdict loosens, any shape the plan claims to cover that still slips through, and any step without an exact expected output.
`

const reviewPrompt = (p, round, file, earlier) => `ROLE: cold plan reviewer for plan ${p.num}, round ${round}. You have NO context beyond what you read now. Your only writable file is ${file} (plus your scratch folder); do not edit the plan.
${CODE}
Read the plan template ${TEMPLATE} (especially "Quality bar"), then the plan ${planPath(p)} as the weakest plausible executor who has never seen this repository's history.
${earlier.length ? `Earlier reviews of this plan in this run: ${earlier.join(', ')}. ` : ''}Earlier review files for this plan (plan-review-${p.num}*.md in ${REVIEW_DIR}) are context: do not re-report what they record as fixed unless it is still wrong, and check that each earlier blocking item really is fixed.
${p.mode === 'extend' ? 'The plan was just extended: review the WHOLE plan, with extra care on the added parts and on every cross-reference, count and expected output the extension touched.\n' : ''}${p.safetyGate ? SAFETY : ''}
Check, and cite plan line numbers for each issue:
1. Could a model execute it with only the plan and the repo? List every place that needs knowledge the plan does not give.
2. Does every step end in a command with an exact expected result (not a judgment)?
3. Do the "Current state" excerpts match the code at ${args.base}? Open each cited file and compare; list mismatches exactly.
4. TDD order for C#, the issue line, binding ADRs named, protected files only in a maintainer Step 0 with a check, single-owner files handled, STOP conditions specific, done criteria machine-checkable, planned-at commit and drift-check paths consistent with Scope, non-deploying commands with -p:ModuleId=, no worktree path or branch name, no CHANGELOG step.
5. No em or en dashes in prose; no secret values.
Blocking = would make a weak executor fail or do harm${p.safetyGate ? ', or ship a gate that allows what it refused before' : ''}. Write your review to ${file} and return the structured result.`

const revisePrompt = (p, round, file, review) => `ROLE: plan reviser for plan ${p.num}, after review round ${round}. You revise ONE plan file: ${planPath(p)}, your only writable path besides your scratch folder.
${CODE}
Read the plan template ${TEMPLATE} and the cold review ${file}.
STRUCTURED REVIEW: ${JSON.stringify(review)}
Fix every blocking item and every excerpt mismatch from your own reads at ${args.base}; prove each fix you can run (quote the result). Apply non-blocking items only where they make the plan clearer without making it longer for no gain. Keep it self-contained and in the template structure; no em or en dashes in prose.
${COMMANDS}

Return the structured result: a two-line summary of what changed, and every blocking item or mismatch you could not fix, each with why.`

async function planOne(p) {
  const out = { num: p.num, slug: p.slug, mode: p.mode, status: '', writer: null, rounds: [], residual: [], failures: [] }
  const fail = failure => { out.failures.push(failure); out.status = 'FAILED'; return out }
  if (p.mode !== 'review') {
    const w = await runAgent('writer', p.mode === 'extend' ? extendPrompt(p) : writePrompt(p), {
      label: `${p.mode}-${p.num}`, phase: 'Write', schema: WRITER_SCHEMA, worktree: args.codeRoot, base: args.base,
    })
    if (!w.ok) return fail(w.failure)
    out.writer = w.value
    if (w.value.status === 'NOT_PLANNED') { out.status = 'NOT_PLANNED'; return out }
  }
  const rounds = roundsFor(p)
  const earlier = []
  for (let round = 1; round <= rounds; round++) {
    const file = reviewFile(p, round)
    const rv = await runAgent('reviewer', reviewPrompt(p, round, file, earlier), {
      label: `review-${p.num}-r${round}`, phase: 'Review', schema: REVIEW_SCHEMA, worktree: args.codeRoot, base: args.base,
    })
    if (!rv.ok) return fail(rv.failure)
    const entry = { round, file, review: rv.value, revise: null }
    out.rounds.push(entry)
    earlier.push(file)
    if (!(rv.value.blocking || []).length && !(rv.value.excerpt_mismatches || []).length) {
      out.status = 'CLEAN'
      return out
    }
    const rs = await runAgent('reviser', revisePrompt(p, round, file, rv.value), {
      label: `revise-${p.num}-r${round}`, phase: 'Revise', schema: REVISE_SCHEMA, worktree: args.codeRoot, base: args.base,
    })
    if (!rs.ok) return fail(rs.failure)
    entry.revise = rs.value
  }
  // The last revision was not reviewed again: say so, and keep what its reviser could not fix.
  out.status = 'REVISED'
  out.residual = [...(out.rounds[out.rounds.length - 1].revise.not_fixed || [])]
  return out
}

phase('Write')
const results = await Promise.all(ITEMS.map(p => planOne(p)))
for (const r of results) log(`plan ${r.num}: ${r.status}${r.failures.length ? ` (${r.failures.join('; ')})` : ''}`)
return results
