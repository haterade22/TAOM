export const meta = {
  name: 'improve-fanout',
  description: 'Parallel read-only agents (audit lanes, verifiers or design agents), each appending to its own file, then an optional single checker that tries to overturn the most consequential verdicts',
  phases: [
    { title: 'Fan out', detail: 'one read-only agent per item, at most args.pool at once' },
    { title: 'Check', detail: 'one checker re-reads the selected verdicts' },
  ],
}

// args, built by python tools/improve_ctl.py args fanout --items <json> ...:
//   rules, repo, runRoot, scratch, tmp, date, version, model { lane, checker }, pool (default 4)
//   items: [{ key, label?, prompt, out, schema?, effort?, model?, wt? }]
//     prompt is the task text; out is the one file the agent appends to as it goes; wt, when set,
//     is a worktree holding the code under study; schema defaults to a summary schema.
//   checker (optional): { prompt, out?, schema?, selectFrom?, effort?, model?, label? }
//     selectFrom: { field, match? } takes the rows of each item's result[field]; a row is taken when
//     it equals every key of any one match object (an array value lists the allowed values).
//     Without selectFrom the checker gets each item's whole result. The checker is held back when
//     any item returned nothing, so it never judges a partial set.
// Returns { lanes: [{ key, out, ok, result | failure }], checker, failures }.

// Every value that changes between runs comes from args. The standing rules for dispatched agents
// (references/dispatch-rules.md) arrive verbatim as args.rules, because a Workflow script cannot
// read files, and every prompt opens with them.
if (typeof args !== 'object' || args === null || typeof args.rules !== 'string' || args.rules.trim() === '') {
  throw new Error('fanout.js needs args.rules, the dispatch rules text: build its args with python tools/improve_ctl.py args fanout')
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
need(args, ['repo', 'runRoot', 'scratch', 'tmp', 'date', 'version'], 'fanout.js args')
if (!ITEMS.length) throw new Error('fanout.js: args.items is empty')
ITEMS.forEach((it, i) => need(it, ['key', 'prompt', 'out'], `fanout.js items[${i}]`))
const keys = ITEMS.map(it => String(it.key))
const dup = keys.find((k, i) => keys.indexOf(k) !== i)
if (dup !== undefined) throw new Error(`fanout.js: two items share the key ${dup}`)
if (args.checker) need(args.checker, ['prompt'], 'fanout.js checker')

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
    label: o.label, phase: o.phase, schema: o.schema, model: o.model || modelFor(role), effort: o.effort || 'high',
  })).then(
    v => (v === null || v === undefined ? { ok: false, failure: `${o.label}: ${NO_RESULT}` } : { ok: true, value: v }),
    e => ({ ok: false, failure: `${o.label}: the agent call failed: ${String(e)}` }),
  )
}

const SUMMARY_SCHEMA = {
  type: 'object',
  properties: {
    summary: { type: 'string' },
    details: { type: 'string' },
    unverified: { type: 'array', items: { type: 'string' } },
    not_covered: { type: 'string' },
  },
  required: ['summary', 'details', 'unverified', 'not_covered'],
}

const CHECK_SCHEMA = {
  type: 'object',
  properties: {
    checks: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          from: { type: 'string' },
          id: { type: 'string' },
          original: { type: 'string' },
          checked: { type: 'string' },
          overturned: { type: 'boolean' },
          evidence: { type: 'string' },
        },
        required: ['from', 'id', 'original', 'checked', 'overturned', 'evidence'],
      },
    },
    summary: { type: 'string' },
  },
  required: ['checks', 'summary'],
}

const labelOf = it => String(it.label || it.key)

const lanePrompt = it => `ROLE: fan-out agent "${labelOf(it)}" of an /improve run (an audit lane, a verifier or a design agent). You never edit, stage or commit anything in the repository.
YOUR ONE WRITABLE FILE: ${it.out}. Create it and append to it as you go, so a run that is cut short keeps your partial work. Write nowhere else except your scratch folder.
RUN FOLDER: ${args.runRoot}. Its BRIEF.md, when present, sets the evidence standard, the skip list and the decided tradeoffs: read it first. Other agents' files there are leads, not facts.
CODE: ${it.wt ? `read the code under study in the worktree ${it.wt}.` : `read the repository through git at the commit your task names (git -C "${args.repo}" show <ref>:<path>, git -C "${args.repo}" grep <ref>); the main checkout's working tree holds other sessions' uncommitted edits, so its files are not evidence.`}
Do not run dotnet build or dotnet test unless your task says to.

TASK:
${it.prompt}

End your file with a section "## What I did not cover". Return the structured result.`

const checkerPrompt = (c, rows) => `ROLE: the one checker of this fan-out. You never edit, stage or commit anything in the repository.${c.out ? ` YOUR ONE WRITABLE FILE: ${c.out}; append one block per row as you go.` : ' You write no file outside your scratch folder.'}
RUN FOLDER: ${args.runRoot}; read its BRIEF.md first, when present. Each row below names the agent that produced it ("from") and that agent's file ("file"), which holds the full text.
Decide every verdict independently from your own reads. Default to overturning a verdict when its evidence does not hold on re-reading, and try hardest on the verdicts that drop a finding: a real defect wrongly dismissed is the costliest error.

TASK:
${c.prompt}

THE ROWS TO CHECK (JSON, one per line):
${rows.map(r => JSON.stringify(r)).join('\n')}

Return the structured result.`

// A row is taken when it matches every key of any one match object.
function matches(row, match) {
  if (!Array.isArray(match) || !match.length) return true
  return match.some(m => Object.keys(m).every(k => (Array.isArray(m[k]) ? m[k].includes(row[k]) : row[k] === m[k])))
}

function pick(lanes, sel) {
  const rows = []
  for (const l of lanes) {
    const list = sel && sel.field ? l.result[sel.field] : [l.result]
    if (!Array.isArray(list)) {
      log(`checker: ${l.key}'s result has no array "${sel.field}", so it gives the checker no rows`)
      continue
    }
    for (const row of list) {
      if (row && typeof row === 'object' && matches(row, sel && sel.match)) rows.push({ from: l.key, file: l.out, ...row })
    }
  }
  return rows
}

phase('Fan out')
const lanes = await Promise.all(ITEMS.map(async it => {
  const r = await runAgent('lane', lanePrompt(it), {
    label: labelOf(it), phase: 'Fan out', schema: it.schema || SUMMARY_SCHEMA, effort: it.effort, model: it.model, worktree: it.wt,
  })
  return r.ok ? { key: it.key, out: it.out, ok: true, result: r.value } : { key: it.key, out: it.out, ok: false, failure: r.failure }
}))
const failures = lanes.filter(l => !l.ok).map(l => l.failure)
log(`fan-out: ${lanes.length - failures.length} of ${lanes.length} agents returned a result`)

let checker = null
if (args.checker) {
  const c = args.checker
  if (failures.length) {
    checker = { ok: false, skipped: `held back: ${failures.length} agent(s) returned nothing, so the checker would judge a partial set; resume the run` }
  } else {
    const rows = pick(lanes, c.selectFrom)
    if (!rows.length) {
      checker = { ok: true, skipped: 'no row matched checker.selectFrom', rows: 0 }
    } else {
      phase('Check')
      const r = await runAgent('checker', checkerPrompt(c, rows), {
        label: String(c.label || 'checker'), phase: 'Check', schema: c.schema || CHECK_SCHEMA, effort: c.effort || 'xhigh', model: c.model,
      })
      if (!r.ok) failures.push(r.failure)
      checker = r.ok ? { ok: true, rows: rows.length, result: r.value } : { ok: false, rows: rows.length, failure: r.failure }
    }
  }
  log(`checker: ${checker.skipped || (checker.ok ? `checked ${checker.rows} rows` : checker.failure)}`)
}

return { lanes, checker, failures }
