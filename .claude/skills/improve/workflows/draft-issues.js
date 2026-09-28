export const meta = {
  name: 'improve-draft-issues',
  description: 'Draft public GitHub issue bodies in the TAOM issue template for improve items, one file per item; files nothing',
  phases: [
    { title: 'Draft', detail: 'one drafter per item, at most args.pool at once; each writes one draft file' },
  ],
}

// args, built by python tools/improve_ctl.py args draft-issues --items <json> ...:
//   rules, repo, runRoot, scratch, tmp, date, version, model { drafter }, pool (default 4)
//   outDir (optional): where the drafts go (default <scratch>/issues)
//   items: [{ num, title, template: 'bug'|'feature', label, slug?, branch?, base?, tip?, blocked?, related?, note? }]
//     With branch, base and tip the work is implemented on a local branch; with blocked it is not
//     implemented and blocked says why; with neither it is planned and awaits execution (issues are
//     filed before execution). A repeated num is refused (both drafts would write one file).
//   outDir rides in an items object: --items takes { "items": [...], "outDir": "<folder>" }.
// Each draft starts with 'TITLE: ...' and 'LABEL: ...' lines, then a blank line and the body, the
// shape python tools/improve_ctl.py file-issue reads. Before anything is filed (on the maintainer's
// word only), the orchestrator runs python tools/check_public_text.py on every draft.
// Returns { drafts: [{ num, ok, title, label, body_file, notes } | { num, ok: false, failure }], failures, check }.

// Every value that changes between runs comes from args. The standing rules for dispatched agents
// (references/dispatch-rules.md) arrive verbatim as args.rules, because a Workflow script cannot
// read files, and every prompt opens with them.
if (typeof args !== 'object' || args === null || typeof args.rules !== 'string' || args.rules.trim() === '') {
  throw new Error('draft-issues.js needs args.rules, the dispatch rules text: build its args with python tools/improve_ctl.py args draft-issues')
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
need(args, ['repo', 'runRoot', 'scratch', 'tmp', 'date', 'version'], 'draft-issues.js args')
if (!ITEMS.length) throw new Error('draft-issues.js: args.items is empty')
ITEMS.forEach((it, i) => {
  const where = `draft-issues.js items[${i}]`
  need(it, ['num', 'title', 'template', 'label'], where)
  if (!['bug', 'feature'].includes(it.template)) throw new Error(`${where}: template must be bug or feature, not ${it.template}`)
  if (it.branch) need(it, ['base', 'tip'], where)
  if (ITEMS.findIndex(o => String(o.num) === String(it.num)) !== i) throw new Error(`${where}: num ${it.num} is listed twice`)
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
    title: { type: 'string' },
    label: { type: 'string' },
    body_file: { type: 'string' },
    notes: { type: 'string' },
  },
  required: ['title', 'label', 'body_file', 'notes'],
}

const OUT_DIR = args.outDir || join(args.scratch, 'issues')
const outFile = it => join(OUT_DIR, `${it.num}.md`)
const git = `git -C "${args.repo}"`
const ref = it => (it.branch ? it.tip : 'HEAD')
const planText = it => (it.slug
  ? `${git} show ${ref(it)}:plans/${it.num}-${it.slug}.md`
  : `the plans/${it.num}-*.md file at ${ref(it)} (${git} ls-tree -r --name-only ${ref(it)} -- plans lists it)`)

function state(it) {
  if (it.branch) {
    return `Implemented on the local branch ${it.branch} (based on ${it.base}, tip ${it.tip}; not pushed yet). Read ${git} log --format='%h %s%n%b' ${it.base}..${it.tip} and ${git} diff --stat ${it.base}..${it.tip}. The branch's review records are the deep-review-${it.num}-*.md and rca-*.md files under docs/reviews at ${it.tip} (${git} ls-tree -r --name-only ${it.tip} -- docs/reviews lists them): the deep review with its CODEX REVIEW section, IMPROVEMENTS lists, NEEDS MIKE items and VERDICT.`
  }
  if (it.blocked) return `NOT implemented: ${it.blocked}`
  return 'NOT implemented yet: the plan is written and awaits execution, and the issue is filed first. Describe the planned change, and say in Status that the branch and the review results will be added to this issue as they land.'
}

const SECTIONS = {
  bug: '## Problem (the symptom and its concrete cost, with numbers), ## Analysis (the root cause with file:line evidence at the base commit, what was examined, the engine facts involved), ## Solution (what the change does and why this approach; for work not yet executed or blocked, the planned change and what it waits on or what blocks it), ## Files Changed (a table: file, one-line change; group many similar files into one row; planned files for work not yet executed), ## Testing (the tests added or planned, and the suite results with the exact totals line, the deep review and Codex outcome, what is still owed in game or on CI)',
  feature: '## Motivation (why, with the measured cost), ## Design (the approach, extension points, alternatives considered and rejected), ## Implementation (key files, patterns, a Files Changed table; for work not yet executed or blocked, the planned change and what it waits on or what blocks it), ## Testing (the tests added or planned, and the suite results with the exact totals line, the deep review and Codex outcome, what is still owed in game or on CI)',
}

const draftPrompt = it => `ROLE: you draft ONE public GitHub issue for the TAOM repository (a Lord of the Rings mod for Bannerlord). You file nothing and push nothing: you write one markdown file, ${outFile(it)}, your only writable path. Read code and docs through git (${git} show <ref>:<path>, git log, git diff); never create repository copies or archives.

THE WORK: ${it.title} (item ${it.num}). The plan: ${planText(it)}; read "Why this matters", "Status", "Current state" and "Done criteria". ${state(it)}
The finding behind it, with evidence and verdicts: ${join(args.runRoot, 'REPORT.md')} and the lane files it names, when they exist.
Related open issues to cross-reference by number (say how each relates, in one line): ${it.related || 'none'}.
Extra facts from the orchestrator: ${it.note || 'none'}

WRITE ${outFile(it)}. Line 1: 'TITLE: <one-line title, under 90 characters, plain, no plan number>'. Line 2: 'LABEL: ${it.label}'. Line 3 blank. Then the body, in TAOM's mandatory issue template for a ${it.template} issue:
${SECTIONS[it.template]}
Then two more sections: '## Status' (${it.branch ? 'the branch name and tip, not pushed; the review verdict; the plan path' : 'the plan path; planned, not implemented; for blocked work, what blocks it'}) and '## Decisions needed' (every NEEDS MIKE item from the review record, one line each, numbered, plus any blocker; 'None.' if there are none).
Include all the useful information: numbers, file:line evidence, commit hashes, test totals, review findings and how each was resolved. Rules for a public TAOM issue: human prose; no em or en dashes (use commas, colons, parentheses); do not name AI models or agents as authors or reviewers of the work (a second automated review is "the adversarial review"), although naming the Claude Code harness is fine when the issue is about its hooks, skills or settings; no secret values; no local absolute paths (repo-relative paths and branch names only); nothing you did not read. The orchestrator runs python tools/check_public_text.py on your file before anything is filed.

Return the structured result: the title, the label, the body file path and a one-line note.`

phase('Draft')
const drafts = await Promise.all(ITEMS.map(async it => {
  const r = await runAgent('drafter', draftPrompt(it), { label: `draft-${it.num}`, phase: 'Draft', schema: SCHEMA, base: it.base })
  return r.ok ? { num: it.num, ok: true, ...r.value } : { num: it.num, ok: false, failure: r.failure }
}))
const failures = drafts.filter(d => !d.ok).map(d => d.failure)
const written = drafts.filter(d => d.ok).map(d => d.body_file)
log(`drafted ${written.length} of ${drafts.length}`)
return {
  drafts,
  failures,
  check: written.length ? `python tools/check_public_text.py ${written.map(f => `"${f}"`).join(' ')}` : '',
}
