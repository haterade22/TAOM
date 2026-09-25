export const meta = {
  name: 'draft-improve-issues-a',
  description: 'Draft GitHub issue bodies (TAOM /issue template) for improve plans whose branches are fully reviewed or blocked; files nothing',
  phases: [{ title: 'Draft', detail: 'one drafter at a time; each writes one body file' }],
}
const OUT = 'E:\\repos\\taom-improve\\issues'
const MODEL = 'claude-opus-5-5'
const ITEMS = (args && args.items) || []
const SCHEMA = {
  type: 'object',
  properties: { title: { type: 'string' }, label: { type: 'string' }, body_file: { type: 'string' }, notes: { type: 'string' } },
  required: ['title', 'label', 'body_file'],
}
const prompt = it => `You draft ONE public GitHub issue for the TAOM repository (a Lord of the Rings mod for Bannerlord). You file nothing and push nothing: you only write a markdown file. You cannot invoke skills or spawn agents. Read code and docs through git (git -C E:\\repos\\TAOM show <ref>:<path>, git log, git diff); never create repo copies or archives; the C: drive is nearly full, so write only the one file named below.

THE WORK: plan ${it.num} (${it.title}). Plan text: E:\\repos\\TAOM\\plans\\${it.num}-${it.slug}.md (read "Why this matters", "Status", "Current state" and "Done criteria"). ${it.branch ? `Implemented on local branch ${it.branch} (based on ${it.base}, tip ${it.tip}; not pushed yet). Read git -C E:\\repos\\TAOM log --format='%h %s%n%b' ${it.base}..${it.tip} and git diff --stat ${it.base}..${it.tip}. The branch's review record: git show ${it.tip}:docs/reviews/deep-review-${it.num}-${it.slug}-2026-09-24.md (the deep review with its CODEX REVIEW section, IMPROVEMENTS lists, NEEDS MIKE items and VERDICT), and the RCA if one exists (git show ${it.tip}:docs/reviews/rca-${it.slug}-2026-09-24.md).` : `NOT implemented: ${it.blocked}`}
The finding behind it, with evidence and verdicts: E:\\repos\\TAOM\\plans\\_audit\\2026-09-23-opus\\REPORT.md and the lane files it names; the brief in plan-briefs.json (num "${it.num}").
Related open issues to cross-reference by number (say how each relates in one line): ${it.related || 'none'}.
Extra facts from the orchestrator: ${it.note || 'none'}

WRITE the file ${OUT}\\${it.num}.md. Line 1: 'TITLE: <one-line title, under 90 characters, plain, no plan number>'. Line 2: 'LABEL: ${it.label}'. Line 3 blank. Then the body, using TAOM's mandatory issue template for a ${it.template} issue:
${it.template === 'bug' ? '## Problem (the symptom and its concrete cost, with numbers), ## Analysis (root cause with file:line evidence at the base commit, what was examined, the engine facts involved), ## Solution (what the branch changes and why this approach; for a blocked plan, the planned change and exactly what blocks it), ## Files Changed (a table: file, one-line change; group many similar files into one row), ## Testing (tests added and the suite results with the exact totals line, the deep review and Codex outcome, what is still owed in game or on CI)' : '## Motivation (why, with the measured cost), ## Design (the approach, extension points, alternatives considered and rejected), ## Implementation (key files, patterns, a Files Changed table; for a blocked plan, the planned change and exactly what blocks it), ## Testing (tests added and the suite results with the exact totals line, the deep review and Codex outcome, what is still owed in game or on CI)'}
Then two more sections: '## Status' (branch name and tip, not pushed; review verdict; the plan path) and '## Decisions needed' (every NEEDS MIKE item from the review record, one line each, numbered; plus any blocker; write 'None.' if there are none).
Include ALL the useful information: numbers, file:line evidence, commit hashes, test totals, review findings and how each was resolved. Rules for a public TAOM issue: human prose; NO em or en dashes (use commas, colons, parentheses); no mention of AI, models, agents, Claude or Codex by name except that 'a second automated review' may be named as 'the adversarial review'; no secret values; no local absolute paths (use repo-relative paths and branch names); do not claim anything you did not read. Return the title, label, body file path and a one-line note.`

phase('Draft')
const out = []
for (const it of ITEMS) {
  const r = await agent(prompt(it), { label: `draft-${it.num}`, phase: 'Draft', schema: SCHEMA, model: MODEL, effort: 'high' })
  out.push({ num: it.num, ...(r || { title: '', label: '', body_file: '', notes: 'drafter returned nothing' }) })
  log(`drafted ${it.num}`)
}
return out
