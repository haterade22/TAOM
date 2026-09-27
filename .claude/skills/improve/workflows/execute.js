export const meta = {
  name: 'improve-execute',
  description: 'Execute improve work in existing worktrees: a plan, a list of maintainer decisions, or ordered stages that each commit and stop at the first that is not DONE',
  phases: [
    { title: 'Execute', detail: 'one executor per item, at most args.pool at once; the stages of an item run in order' },
  ],
}

// args, built by python tools/improve_ctl.py args execute --items <json> ...:
//   rules, repo, scratch, tmp, date, version, model { executor }, pool (default 4)
//   knownFailures (optional): ["<test>: <why it is expected to fail on this base>", ...]
//   items: [{ num, slug, wt, branch, base, contract: 'plan'|'decisions'|'stages', decisions?, stages?,
//             note?, issue?, ref?, knownFailures?, effort? }]
//     wt and branch already exist (the orchestrator made them); decisions: the maintainer's decision
//     texts; stages: [{ key, title?, prompt, effort? }]; note: binding orchestrator text (an
//     amendment, a resume from uncommitted work); ref: how commits name the work (default "plan <num>").
// Returns one entry per item: { num, contract, status: DONE | BLOCKED | PARTIAL | FAILED, ... the
//   executor's result }, or for stages { num, contract, status, stoppedAt?, notRun?, stages: [...] }.

// Every value that changes between runs comes from args. The standing rules for dispatched agents
// (references/dispatch-rules.md) arrive verbatim as args.rules, because a Workflow script cannot
// read files, and every prompt opens with them.
if (typeof args !== 'object' || args === null || typeof args.rules !== 'string' || args.rules.trim() === '') {
  throw new Error('execute.js needs args.rules, the dispatch rules text: build its args with python tools/improve_ctl.py args execute')
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
need(args, ['repo', 'scratch', 'tmp', 'date', 'version'], 'execute.js args')
if (!ITEMS.length) throw new Error('execute.js: args.items is empty')
ITEMS.forEach((it, i) => {
  const where = `execute.js items[${i}]`
  need(it, ['num', 'slug', 'wt', 'branch', 'base', 'contract'], where)
  if (it.contract === 'decisions') {
    if (!Array.isArray(it.decisions) || !it.decisions.length) throw new Error(`${where}: the decisions contract needs a non-empty decisions list`)
  } else if (it.contract === 'stages') {
    if (!Array.isArray(it.stages) || !it.stages.length) throw new Error(`${where}: the stages contract needs a non-empty stages list`)
    it.stages.forEach((st, k) => need(st, ['key', 'prompt'], `${where}.stages[${k}]`))
  } else if (it.contract !== 'plan') {
    throw new Error(`${where}: contract must be plan, decisions or stages, not ${it.contract}`)
  }
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

const SCHEMA = {
  type: 'object',
  properties: {
    status: { type: 'string', enum: ['DONE', 'BLOCKED', 'PARTIAL'] },
    base_before: { type: 'string' },
    commit: { type: 'string' },
    summary: { type: 'string' },
    files_changed: { type: 'array', items: { type: 'string' } },
    red_evidence: { type: 'string' },
    tests: { type: 'string' },
    sweep: { type: 'string' },
    stop_reason: { type: 'string' },
    deviations: { type: 'string' },
    owed: { type: 'string' },
  },
  required: ['status', 'base_before', 'commit', 'summary', 'files_changed', 'red_evidence', 'tests', 'stop_reason', 'deviations', 'owed'],
}

const refOf = it => String(it.ref || `plan ${it.num}`)
const planFile = it => join(it.wt, 'plans', `${it.num}-${it.slug}.md`)
const issueOf = it => (it.issue ? `#${String(it.issue).split('#').join('')}` : '')
const issueLine = it => (it.issue ? ` and ${issueOf(it)}` : '')

function knownFailures(it) {
  const list = Array.isArray(it.knownFailures) ? it.knownFailures : Array.isArray(args.knownFailures) ? args.knownFailures : []
  return list.length ? `\nThe orchestrator expects these failures on this base (each with its reason); any other failure the base did not have is yours:\n${list.map(f => `- ${f}`).join('\n')}` : ''
}

// The standing rules give the hook suite's command; this adds the worktree it must judge.
const HOOK_SUITE = it => `When you run the hook suite (standing rule "Build and test"), run it from ${it.wt} with CLAUDE_PROJECT_DIR="${it.wt}" set, so the hooks it starts judge this worktree and not the main checkout.`

const workspace = it => `WORKSPACE: the git worktree ${it.wt} on branch ${it.branch}, based on ${it.base}. Both already exist and the branch is checked out: never create another worktree or branch. Every file you read or edit is under ${it.wt}. Record git rev-parse --short HEAD before your first edit and return it as base_before.
Path-scoped rules do not load for files outside the main checkout, so read the rules your task names yourself from ${join(it.wt, '.claude', 'rules')} (csharp-architecture.md for any C# change).
${HOOK_SUITE(it)}
${it.note ? `ORCHESTRATOR NOTE (binding; it overrides your contract and the standing rules where it says so):\n${it.note}\n` : ''}`

const RETURN = 'Return the structured result: the status, base_before, the commit (the branch HEAD after your last commit; empty if you committed nothing), what you did in order, the files changed, the RED evidence (the failing test output you saw before the fix), the final full-suite totals line, any stop reason, deviations from the contract with why, and what is owed (in-game checks, CI runs, maintainer decisions).'

const planPrompt = it => `ROLE: the EXECUTOR for improve ${refOf(it)}. You implement it exactly, verify it, and commit it on its own branch.
${workspace(it)}
YOUR CONTRACT: ${planFile(it)}. Read it completely before doing anything. It is written for an executor with no context and is the only authority for scope. Follow its steps in order, run every verification command and compare with the expected result. If it has a Step 0 for the maintainer (a protected-file edit), run its check first and STOP if the check fails. If any STOP condition fires, reality differs from its "Current state" excerpts, or a step's verification fails twice after a reasonable fix: STOP, do not improvise, commit nothing further, and return BLOCKED with the exact reason and what you had done.
BASELINE: before your first edit, run the full suite once and record the base's own totals and failing tests (standing rule "Build and test"); at the end, match them.${knownFailures(it)}
COMMIT when the done criteria all hold (standing rule "Commit"), with a body that names ${refOf(it)}${issueLine(it)}.
${RETURN}`

const decisionsPrompt = it => `ROLE: you apply the maintainer's decisions to one already-reviewed branch, ${refOf(it)}.
${workspace(it)}
CONTEXT: read the plan ${planFile(it)} if it exists, and the branch's review record (the newest docs/reviews/deep-review-${it.num}-${it.slug}*.md in the worktree), whose NEEDS MIKE items these decisions answer.${it.issue ? ` The public issue is ${issueOf(it)}.` : ''}
DECISIONS TO APPLY (taken by the maintainer; each is binding; if one turns out impossible or unsafe on reading the code, STOP and return BLOCKED instead of improvising):
${it.decisions.map((d, i) => `${i + 1}. ${d}`).join('\n')}
BASELINE: before your first edit, run the full suite once and record the base's own totals and failing tests (standing rule "Build and test"); at the end, match them.${knownFailures(it)}
Every behaviour change is TDD. Update the branch's docs, and append a section "## Maintainer decisions applied (${args.date})" to the review record listing each decision and the commit that applies it.
COMMIT (standing rule "Commit"): subject 'fix(<scope>): ${args.version} - apply maintainer decisions for ${refOf(it)}' (at most 72 characters; adjust the scope, shorten the tail if needed), a body that lists each decision${it.issue ? ` and names ${issueOf(it)}` : ''}.
${RETURN}`

const stagePrompt = (it, st, i, prior) => `ROLE: a BUILDER for stage ${i + 1} of ${it.stages.length} ("${st.title || st.key}") of ${refOf(it)}. Each stage is one TDD commit on the branch, and a later stage builds on it.
${workspace(it)}
EARLIER STAGES (their commits are on the branch; read git log and their diffs before you start):
${prior || '- none: you are the first stage'}
BASELINE: ${i === 0 ? 'before your first edit, run the full suite once and record the base\'s own totals and failing tests (standing rule "Build and test")' : 'the first stage recorded the base\'s totals (its tests line above)'}; at the end, match them.${knownFailures(it)}

STAGE TASK:
${st.prompt}

Commit this stage on its own before you return (standing rule "Commit"), with a body that names ${refOf(it)}${issueLine(it)}. If the stage cannot be finished, commit nothing further and return BLOCKED with the reason: the stages after it will not run.
${RETURN}`

async function executeOne(it) {
  if (it.contract === 'stages') {
    const results = []
    for (let i = 0; i < it.stages.length; i++) {
      const st = it.stages[i]
      const prior = results.map(r => `- ${r.stage}: ${r.status} ${r.commit || ''} :: ${String(r.summary || '').slice(0, 600)}\n  tests: ${String(r.tests || '').slice(0, 300)}`).join('\n')
      const r = await runAgent('executor', stagePrompt(it, st, i, prior), {
        label: `stage-${it.num}-${st.key}`, phase: 'Execute', schema: SCHEMA, effort: st.effort || it.effort, editing: true, worktree: it.wt, base: it.base,
      })
      const res = r.ok ? { stage: st.key, ...r.value } : { stage: st.key, status: 'FAILED', failure: r.failure }
      results.push(res)
      log(`${refOf(it)} stage ${st.key}: ${res.status} ${res.commit || res.failure || ''}`)
      if (res.status !== 'DONE') {
        return { num: it.num, contract: it.contract, status: res.status, stoppedAt: st.key, notRun: it.stages.slice(i + 1).map(s => s.key), stages: results }
      }
    }
    return { num: it.num, contract: it.contract, status: 'DONE', stages: results }
  }
  const prompt = it.contract === 'plan' ? planPrompt(it) : decisionsPrompt(it)
  const label = `${it.contract === 'plan' ? 'exec' : 'decide'}-${it.num}`
  const r = await runAgent('executor', prompt, { label, phase: 'Execute', schema: SCHEMA, effort: it.effort, editing: true, worktree: it.wt, base: it.base })
  const res = r.ok ? { num: it.num, contract: it.contract, ...r.value } : { num: it.num, contract: it.contract, status: 'FAILED', failure: r.failure }
  log(`${refOf(it)}: ${res.status} ${res.commit || res.failure || ''}`)
  return res
}

phase('Execute')
return await Promise.all(ITEMS.map(it => executeOne(it)))
