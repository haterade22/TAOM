export const meta = {
  name: 'opus-night-review',
  description: 'Overnight: /deep-review (+ Codex pre-review results) on each executed improve branch, apply fixes, RCA, convergence',
  phases: [
    { title: 'Lenses', detail: 'deep-reviewer lenses per branch, at most 4 in flight overall' },
    { title: 'Fix', detail: 'review lead verifies lens + Codex findings, applies, RCA, commits on the branch' },
    { title: 'Converge', detail: 'one convergence deep-reviewer per branch (the checker slot)' },
  ],
}

// args: { items: [{ num, slug, wt, branch, base, head, title, lenses: ['1','2',...], files: {cs:[],xml:[],scripts:[],harness:[],docs:[]}, codexOut }] }
const ITEMS = (args && args.items) || []
const MODEL = 'claude-opus-5-5'
const LENS_FILES = {
  '1': ['Agent 1 Standards', '1-standards.md'],
  '2': ['Agent 2 Engine compatibility', '2-engine-compat.md'],
  '3': ['Agent 3 Efficiency', '3-efficiency.md'],
  '4': ['Agent 4 Completeness', '4-completeness.md'],
  '5': ['Agent 5 Data flow', '5-data-flow.md'],
  '6': ['Agent 6 Design & Elegance', '6-design.md'],
  '7': ['Agent 7 XML & ModuleData', '7-xml.md'],
  't': ['Tooling correctness', 'tooling.md'],
}
// Defect lenses first (skill: first wave 1, 2, 5, 7), then the rest.
const ORDER = ['1', '2', '5', '7', 't', '3', '4', '6']

function makePool(n) {
  let active = 0
  const q = []
  const next = () => {
    while (active < n && q.length) {
      active++
      const { fn, res, rej } = q.shift()
      fn().then(v => { active--; res(v); next() }, e => { active--; rej(e); next() })
    }
  }
  return fn => new Promise((res, rej) => { q.push({ fn, res, rej }); next() })
}
const pool = makePool((args && args.pool) || 4)      // lenses and review leads
const checker = makePool(1)   // convergence reviewers

const fileList = it => {
  const f = it.files || {}
  const all = [...(f.cs || []), ...(f.xml || []), ...(f.scripts || []), ...(f.harness || []), ...(f.docs || [])]
  return all.map(p => `${it.wt}\\${p.replace(/\//g, '\\')}`).join('\n')
}

const lensPrompt = (it, key) => `Lens: ${LENS_FILES[key][0]}. Read E:\\repos\\TAOM\\.claude\\skills\\deep-review\\lenses\\${LENS_FILES[key][1]} first; it is your whole task and its output format.
FILES (all inside the worktree ${it.wt}; review THESE copies, never E:\\repos\\TAOM's working tree, which holds another session's unrelated edits):
${fileList(it)}
SCOPE NOTES: this change implements plan ${it.num} ("${it.title}") on branch ${it.branch}, committed in the worktree ${it.wt}. The diff under review is: git -C "${it.wt}" diff ${it.base}..${it.head} . The intent, scope and STOP conditions are in ${it.wt}\\plans\\${it.num}-${it.slug}.md: read it. ${it.tag ? 'THIS IS A SECOND REVIEW: the branch was already reviewed, and this diff applies the maintainer decisions listed in the ## Maintainer decisions applied section of ' + it.wt + '\\docs\\reviews\\deep-review-' + it.num + '-' + it.slug + '-2026-09-24.md. Those behaviour changes are decided, not findings: review whether each is implemented correctly and completely. ' : ''}Only the changed hunks are in scope for defects; pre-existing issues you notice go under FOLLOW-UP. Path-scoped rules do not auto-load for files outside E:\\repos\\TAOM: read the relevant ones from ${it.wt}\\.claude\\rules\\ yourself. Engine signatures: pwsh tools/taom-src.ps1 path <Type> (run from E:\\repos\\TAOM; it only reads). DISK RULE: the C: drive is nearly full; never write scratch files, decompile dumps or repository copies to C: (no scratchpad folder, no temp folder there); if you must save a file, use E:\\repos\\taom-improve\\scratch\\review-${it.num}.`

const leadPrompt = (it, reports) => `You are the REVIEW LEAD for one executed TAOM improvement: plan ${it.num} ("${it.title}") on branch ${it.branch} in the worktree ${it.wt} (diff ${it.base}..${it.head}). You act as the /deep-review orchestrator's delegate for Steps 3, 3e and 4, and as /review-codex Phase 3. You cannot invoke skills or spawn agents.
Read first: E:\\repos\\TAOM\\.claude\\skills\\deep-review\\SKILL.md (Steps 3, 3e, 4 and "HIGH findings") and E:\\repos\\TAOM\\.claude\\skills\\review-codex\\SKILL.md (Phase 3). Work ONLY inside ${it.wt}; every shell command starts with cd "${it.wt}" && . Never touch E:\\repos\\TAOM's working tree, never push, merge, rebase, stash or reset. NEVER create git-archive extractions, clones or copies of the repository anywhere (read other revisions with git show / git grep instead); the C: drive is nearly full.

INPUTS
1. The deep-review lens reports, verbatim, below.
2. The Codex adversarial review, if it has finished: ${it.codexOut}. It is complete only if it contains the line "END OF CODEX REVIEW". If the file is missing or incomplete, record "Codex pending" in the report and continue without it (the orchestrator will run a second pass).

YOUR JOB
A. Verify every finding (lens and Codex) against the code in the worktree before acting on it (evidence-over-claims: a finding is a hypothesis). Classify each: CONFIRMED, FALSE POSITIVE (say why), or NEEDS MIKE (design or product decision).
B. Fix every CONFIRMED defect in the changed code, TDD (a failing test first where the defect is testable). HIGH findings are fixed by default; if one truly cannot be fixed here, record it with a 'Deferred: <reason>' trailer and in the report.
C. Step 4 improvements (Agent 3 APPLY-scoped fixes and Agent 6 KEEP proposals, changed code only): apply the behaviour-PRESERVING ones with a characterisation test green before and after. Do NOT apply behaviour-CHANGING ones: the skill requires asking Mike first; list each under NOT APPLIED with 'needs Mike'. FOLLOW-UP proposals (pre-existing code) are listed, never applied.
D. Run the full suite in the worktree: dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= (never ./build.ps1). Keep build and test temp files off C: by prefixing every dotnet command with TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp (mkdir -p it first). Only the two known live-Armory failures (TheElkItem_DeclaresTheScaleTheReachIsTunedFor, AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist) may fail, and only on a branch based before a39a9c86; on a39a9c86 or later nothing may fail.
E. Write, inside the worktree: docs/reviews/deep-review-${it.num}-${it.slug}${it.tag || ''}-2026-09-24.md (the Step 3 DEEP REVIEW REPORT with the IMPROVEMENTS lists and VERDICT, plus a CODEX REVIEW section with the Phase 3d assessment table); for any confirmed finding, docs/reviews/rca-${it.slug}${it.tag || ''}-2026-09-24.md (Step 3e format: summary, findings table, why each agent missed it) and one '### <rule>' entry per systemic lesson appended to the matching docs/reviews/lessons/<category>.md; one entry appended to docs/reviews/REVIEW-LOG.md for the Codex review (review-codex Phase 3i). Do NOT edit AGENTS.md (Phase 3h is consolidated later for all branches to avoid conflicts; list the lessons you would add in the report under 'AGENTS.md lessons (pending)'). Prose: no em or en dashes.
F. Commit on the branch: stage explicit paths only; subject 'fix(<scope>): v2.0.30 - review follow-ups for plan ${it.num}' (72 characters max, adjust scope), body wrapped at 72 naming the report and RCA paths, no AI attribution trailer, never --no-verify. If nothing needed fixing, still commit the report files with subject 'docs(reviews): v2.0.30 - deep review and Codex for plan ${it.num}'.

LENS REPORTS:
${reports}

Return: verdict (READY FOR COMMIT or NEEDS FIXES), counts (confirmed, false positives, needs Mike, applied improvements, not applied), the commit hash, the full-suite totals line, whether Codex was included or pending, and the list of NEEDS MIKE items (one line each).`

const convergePrompt = (it, fromRef) => `Lens: convergence pass (deep-review Step 4 item 6). Read E:\\repos\\TAOM\\.claude\\skills\\deep-review\\SKILL.md Step 4 and E:\\repos\\TAOM\\.claude\\skills\\deep-review\\lenses\\1-standards.md for the output format.
Review ONLY the review-fix diff: git -C "${it.wt}" diff ${fromRef}..HEAD (branch ${it.branch}, plan ${it.num}, worktree ${it.wt}; never read E:\\repos\\TAOM's working tree copies). Check standards and behaviour parity of the applied fixes and improvements. Report DEFECTS only (with file:line and a proving read); you may NOT open a new design round. End with a line 'CONVERGENCE: CLEAN' or 'CONVERGENCE: DEFECTS <n>'.`

const fixAgainPrompt = (it, report) => `You are the REVIEW LEAD's second pass for plan ${it.num} on branch ${it.branch} in ${it.wt} (work only there; every command starts with cd "${it.wt}" && ; never touch E:\\repos\\TAOM's working tree; never push). The convergence reviewer found defects in the review-fix commit. Verify each against the code, fix the confirmed ones (test first where testable), run the full suite (dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=; only the two known live-Armory failures may fail, and only on a branch based before a39a9c86), append a 'Convergence' section to docs/reviews/deep-review-${it.num}-${it.slug}${it.tag || ''}-2026-09-24.md, and commit (explicit paths, subject 'fix(<scope>): v2.0.30 - convergence fixes for plan ${it.num}', no attribution trailer, never --no-verify). Do not open new design work.
CONVERGENCE REPORT:
${report}
Return: commit hash, what was fixed, what was a false positive, the suite totals line.`

async function reviewOne(it) {
  const keys = ORDER.filter(k => (it.lenses || []).includes(k))
  const lensResults = await Promise.all(keys.map(k =>
    pool(() => agent(lensPrompt(it, k), { label: `lens-${it.num}-${k}`, phase: 'Lenses', agentType: 'deep-reviewer' }))
      .then(r => ({ k, r }))
  ))
  const reports = lensResults.map(x => `===== ${LENS_FILES[x.k][0]} =====\n${x.r || '(no result: the agent returned nothing; say so in the report)'}`).join('\n\n')
  const lead = await pool(() => agent(leadPrompt(it, reports), { label: `lead-${it.num}`, phase: 'Fix', model: MODEL, effort: 'high' }))
  const conv = await checker(() => agent(convergePrompt(it, it.head), { label: `converge-${it.num}`, phase: 'Converge', agentType: 'deep-reviewer' }))
  let second = null
  if (typeof conv === 'string' && /CONVERGENCE:\s*DEFECTS/i.test(conv)) {
    second = await pool(() => agent(fixAgainPrompt(it, conv), { label: `lead2-${it.num}`, phase: 'Fix', model: MODEL, effort: 'high' }))
  }
  log(`plan ${it.num}: lenses ${keys.join(',')} done, lead done, convergence ${second ? 'had defects (second pass run)' : 'clean or not parsed'}`)
  return { num: it.num, lenses: keys, lead, convergence: conv, second }
}

phase('Lenses')
const results = await Promise.all(ITEMS.map(it => reviewOne(it).catch(e => ({ num: it.num, error: String(e) }))))
return results
