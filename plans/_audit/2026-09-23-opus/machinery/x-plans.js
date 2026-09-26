export const meta = {
  name: 'opus-plans',
  description: 'W5: write self-contained handoff plans (write, cold review, revise), 4 plans at a time',
  phases: [
    { title: 'Plans', detail: 'per plan: writer, fresh cold reviewer, reviser' },
  ],
}

// args: { plans: [{ num, slug, title, priority, category, depends_on, brief }] }
// brief = the orchestrator's verified summary of the finding(s): claim, evidence with file:line,
// verification verdicts, fix direction, and which lane/verify files hold the full text.
const RUN = (args && args.run) || 'E:\\repos\\TAOM\\plans\\_audit\\2026-09-23-opus'
const BRIEF = `${RUN}\\BRIEF.md`
// Optional: write plans and reviews into a worktree instead of E:\repos\TAOM, and read code there.
const PLANS_DIR = (args && args.plansDir) || 'E:\\repos\\TAOM\\plans'
const REVIEW_DIR = (args && args.reviewDir) || RUN
const ROOT = (args && args.root) || ''
const ROOT_RULE = ROOT ? `CODE LOCATION (binding): read every repository file from the worktree ${ROOT} (checked out at the planning commit), never from E:\\repos\\TAOM, whose working tree holds another session's uncommitted edits. Paths you write into the plan stay repo-relative.\n` : ''
const TEMPLATE = 'E:\\repos\\TAOM\\.claude\\skills\\improve\\references\\plan-template.md'
const MODEL = 'claude-opus-5-5'
const PLANS = (args && args.plans) || []
const BASE = (args && args.base) || 'b2e387db'
const DATE = (args && args.date) || '2026-09-23'
const BRIEFS = (args && args.briefs) || `${RUN}\\plan-briefs.json`
const BASELINE = (args && args.baseline) || 'Baseline at b2e387db: 10,239 tests, 10,235 pass, 2 fail (live Armory), 2 ignored.'
const planPath = p => `${PLANS_DIR}\\${p.num}-${p.slug}.md`

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

const COMMANDS = `Use exactly these non-deploying commands in the plan (AGENTS.md; the template's older forms lack -p:ModuleId=):
- Build: dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=
- Test: dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=
- Filtered test: dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"
- Data: python tools/validate_moduledata.py
- Docs: python tools/lint_docs.py
Never ./build.ps1. ${BASELINE}`

const DISK = `DISK RULE (binding): the C: drive is nearly full. NEVER create git-archive exports, clones or copies of the repository (read any revision with git show <ref>:<path> or git grep <ref>). Any scratch file goes under E:\\repos\\taom-improve\\scratch\\plans\\<plan number>, never in a temp or scratchpad folder on C:.`
const writePrompt = p => `${DISK}
${ROOT_RULE}Read ${BRIEF} first (you are no longer an auditor: in this task you WRITE ONE PLAN FILE, and that file is your only writable path). Then read the plan template ${TEMPLATE} completely and follow it exactly, including its "TAOM additions" and "Quality bar".

PLAN ${p.num}: ${p.title}
Priority ${p.priority}; category ${p.category}; depends on ${p.depends_on || 'none'}.
The orchestrator's verified brief for this plan is the "brief" field of the entry with num "${p.num}" in ${BRIEFS}: read it completely first; it carries the verified facts, the checkers' corrections, the decisions you must NOT take, and pointers to the lane and verify files.

Rules for writing it:
- The executor has ZERO context: inline every fact, path, excerpt and convention it needs. Never write "see the audit" or "as discussed".
- Open every file you cite and copy excerpts from YOUR OWN reads at commit ${BASE} (for Main/SubModule.cs and Main/IoC.cs use git show ${BASE}:<path>, because the working tree holds another session's uncommitted edits). Lane and verify files are leads, not facts.
- Engine signatures: pwsh tools/taom-src.ps1 path <Type> gives a decompiled file; quote the signature you rely on.
- Planned at: commit ${BASE}, ${DATE}. The drift check command names the exact in-scope paths.
- C# work is TDD: the failing test step comes before the implementation step, with the exact test name and file.
- Issue-first: Status says "Issue: create before implementation lands (orchestrator)".
- Name the binding ADRs and rules with one-line summaries (ADR-002 thin entry points under 150 lines, ADR-007 adapters, ADR-008 test coverage, csharp-architecture.md rules as relevant).
- Single-owner files (Main/IoC.cs, Main/SubModule.cs, Main/TAOM.csproj, Directory.Build.props): either list them explicitly in scope with the exact edit, or say "recommend, don't edit" and give the exact line to report.
- STOP conditions specific to this plan's real risks. Machine-checkable done criteria.
- Git workflow section: branch in a worktree, commit subject format "<type>(<scope>): v<version from Main/_Module/SubModule.xml> - <description>", 72 characters max, no AI attribution trailer, stage explicit paths only, never push.
- Prose: no em or en dashes (commas, colons, parentheses instead); code spans are exempt. No secret values.
- If the finding turns out wrong or already fixed when you read the code, do not write a plan: write a file stating that with evidence, and return "NOT PLANNED: <reason>".
${COMMANDS}

Write the plan to ${planPath(p)} (create it). That is your only writable path. Return a 3-line summary: path, step count, and the riskiest assumption.`

const reviewPrompt = p => `${DISK}
${ROOT_RULE}You are a cold reviewer with NO context beyond what you read now. Read the plan template ${TEMPLATE} (especially "Quality bar"), then read the plan ${planPath(p)} as if you were the weakest plausible executor who has never seen this repository's history.
Check, and cite line numbers of the plan for each issue:
1. Could a model execute it with only the plan and the repo? List every place that needs knowledge the plan does not give.
2. Does every step end in a command with an expected result (not a judgment)?
3. Do the "Current state" excerpts match the code at commit ${BASE}? Open each cited file and compare (for Main/SubModule.cs and Main/IoC.cs use git show ${BASE}:<path>). List mismatches exactly.
4. TDD order for C#, issue-first note, binding ADRs named, single-owner files handled, STOP conditions specific, done criteria machine-checkable, planned-at SHA and drift-check paths consistent with Scope, non-deploying commands with -p:ModuleId=.
5. No em or en dashes in prose; no secret values.
Blocking = would make a weak executor fail or do harm. Write your review to ${REVIEW_DIR}\\plan-review-${p.num}.md (your only writable path) and return the structured result. Do not edit the plan.`

const revisePrompt = (p, review) => `${DISK}
${ROOT_RULE}Read ${BRIEF} first. You revise ONE plan file: ${planPath(p)} (your only writable path). Read the plan template ${TEMPLATE} and the cold review ${REVIEW_DIR}\\plan-review-${p.num}.md.
Structured review: ${JSON.stringify(review)}
Fix every blocking item and every excerpt mismatch (re-read the code at ${BASE} yourself; for Main/SubModule.cs and Main/IoC.cs use git show). Apply non-blocking items where they make the plan clearer without making it longer for no gain. Keep it self-contained, keep the template structure, no em or en dashes in prose.
${COMMANDS}
Return a 2-line summary of what changed.`

phase('Plans')
const out = []
const CHUNK = (args && args.chunk) || 4   // plans in flight at once (each runs writer, reviewer, reviser in turn)
for (let i = 0; i < PLANS.length; i += CHUNK) {
  const chunk = PLANS.slice(i, i + CHUNK)
  const res = await pipeline(
    chunk,
    p => agent(writePrompt(p), { label: `write-${p.num}`, phase: 'Plans', model: MODEL, effort: 'high' }),
    (w, p) => (typeof w === 'string' && w.includes('NOT PLANNED'))
      ? { skipped: true, writer: w }
      : agent(reviewPrompt(p), { label: `review-${p.num}`, phase: 'Plans', schema: REVIEW_SCHEMA, model: MODEL, effort: 'high' }).then(r => ({ writer: w, review: r })),
    (wr, p) => wr.skipped || !wr.review
      ? { num: p.num, ...wr }
      : agent(revisePrompt(p, wr.review), { label: `revise-${p.num}`, phase: 'Plans', model: MODEL, effort: 'high' }).then(rv => ({ num: p.num, writer: wr.writer, review: wr.review, revise: rv })),
  )
  out.push(...res)
  log(`plans chunk ${i / CHUNK + 1}/${Math.ceil(PLANS.length / CHUNK)} done`)
}
return out
