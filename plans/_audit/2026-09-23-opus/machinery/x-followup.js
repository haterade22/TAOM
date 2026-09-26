export const meta = {
  name: 'opus-followup-decisions',
  description: "Apply Mike's recorded decisions to an already-reviewed improve branch (one executor per branch, in its worktree)",
  phases: [{ title: 'Follow-up', detail: 'one executor per branch; commit on its branch only' }],
}

// args: { items: [{ num, slug, wt, branch, issue, decisions: ['...'] }] }
const MODEL = 'claude-opus-5-5'
const ITEMS = (args && args.items) || []
const SCHEMA = {
  type: 'object',
  properties: {
    status: { type: 'string', enum: ['DONE', 'BLOCKED', 'PARTIAL'] },
    commit: { type: 'string' }, base_before: { type: 'string' }, summary: { type: 'string' },
    files_changed: { type: 'array', items: { type: 'string' } }, tests: { type: 'string' },
    stop_reason: { type: 'string' }, owed: { type: 'string' },
  },
  required: ['status', 'summary', 'files_changed', 'tests'],
}

const prompt = it => `You apply the maintainer's decisions to one already-reviewed TAOM branch. You cannot invoke skills or spawn agents.

WORKSPACE: the worktree ${it.wt}, branch ${it.branch} (already checked out; do not create branches or worktrees). Every shell command starts with cd "${it.wt}" && . Never edit, build, stage or commit anything in E:\\repos\\TAOM. NEVER create git-archive extractions, clones or copies of the repository. The C: drive is nearly full: prefix every dotnet command with TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp, and keep any scratch under E:\\repos\\taom-improve\\scratch\\${it.num}.

CONTEXT: read ${it.wt}\\plans\\${it.num}-${it.slug}.md (the plan) and ${it.wt}\\docs\\reviews\\deep-review-${it.num}-${it.slug}-2026-09-24.md (the review record whose NEEDS MIKE items these decisions answer). The public issue is #${it.issue}. Record the git rev-parse --short HEAD before you start and return it as base_before.

DECISIONS TO APPLY (taken by the maintainer on 2026-09-24; each is binding, and if one turns out impossible or unsafe on reading the code, STOP and report instead of improvising):
${it.decisions.map((d, i) => `${i + 1}. ${d}`).join('\n')}

HOUSE RULES: TDD for every behaviour change (write the failing test, SEE it fail and quote the failure, then implement, then see it pass). Read the rules that apply from ${it.wt}\\.claude\\rules\\ yourself (path rules do not auto-load outside E:\\repos\\TAOM). Build and test only in the worktree: dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= and dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= (never ./build.ps1); the only allowed full-suite failures are TheElkItem_DeclaresTheScaleTheReachIsTunedFor and AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist, and only on a branch based before a39a9c86 (on a39a9c86 or later nothing may fail). For hook edits also run bash tools/test_hooks.sh. ADR-002/ADR-007; no #region, no [Obsolete]; no em or en dashes in prose. Update the branch's docs and its review record (append a '## Maintainer decisions applied (2026-09-24)' section to the deep-review report listing each decision and its commit); never edit CHANGELOG.md (generated at /release since plan 020): the commit body is the changelog entry. Do NOT edit plans/README.md; no GitHub issues or comments; never push, merge, rebase, stash, reset or switch branches.
COMMIT: explicit paths only; subject 'fix(<scope>): v2.0.30 - apply maintainer decisions for plan ${it.num}' (72 characters max; adjust scope), body wrapped at 72 listing each decision and naming #${it.issue}; no AI attribution trailer; never --no-verify.
Return the structured result with the final full-suite totals line quoted.`

phase('Follow-up')
const res = await parallel(ITEMS.map(it => () =>
  agent(prompt(it), { label: `followup-${it.num}`, phase: 'Follow-up', schema: SCHEMA, model: MODEL, effort: 'high' })
    .then(r => ({ num: it.num, ...(r || { status: 'BLOCKED', summary: 'returned nothing', files_changed: [], tests: '' }) }))
))
return res
