export const meta = {
  name: 'opus-night-exec',
  description: 'Overnight: execute improve plans in their own worktrees (one Opus executor per plan, at most 4 in flight)',
  phases: [{ title: 'Execute', detail: 'one executor per plan, isolated worktree, commit on its branch only' }],
}

// args: { items: [{ num, slug, wt, branch, base }] }
const MODEL = 'claude-opus-5-5'
const ITEMS = (args && args.items) || []

const SCHEMA = {
  type: 'object',
  properties: {
    status: { type: 'string', enum: ['DONE', 'BLOCKED', 'PARTIAL'] },
    commit: { type: 'string' },
    summary: { type: 'string' },
    files_changed: { type: 'array', items: { type: 'string' } },
    tests: { type: 'string' },
    stop_reason: { type: 'string' },
    deviations: { type: 'string' },
    owed: { type: 'string' },
  },
  required: ['status', 'summary', 'files_changed', 'tests'],
}

const prompt = it => `You are the EXECUTOR for one TAOM improvement plan. You implement it exactly, verify it, and commit it on its own branch. You cannot invoke skills or spawn agents.

YOUR WORKSPACE: the git worktree ${it.wt} (branch ${it.branch}, based on ${it.base}). Every file you read or edit is under ${it.wt}. Every shell command starts with: cd "${it.wt}" && ... . NEVER edit, build, stage or commit anything in E:\\repos\\TAOM (another live session's uncommitted work is there). Never touch the game install under E:\\Steam except to READ. NEVER create git-archive extractions, clones or copies of the repository anywhere (read other revisions with git show / git grep instead); the C: drive is nearly full. Never create large temp output on C:; if you need scratch space use E:\\repos\\taom-improve\\scratch.

NAME SUBSTITUTION (important): the plan was written before the orchestrator chose names, so any worktree path it names (for example E:/repos/wt-${it.num}-..., E:/repos/wt-plan-${it.num}) and any branch name it names (plan-${it.num}..., plan/${it.num}-...) are placeholders. The real worktree is ${it.wt} and the real branch is ${it.branch}, which already exist and are checked out; do NOT create another worktree or branch. Wherever a plan command or check uses its placeholder path or branch, use the real one instead (a branch check must expect ${it.branch}). Scratch folders a plan names next to its placeholder worktree (for example E:/repos/wt-${it.num}-logs) may be created under E:\\repos\\taom-improve\\scratch\\${it.num} instead.

YOUR CONTRACT: ${it.wt}\\plans\\${it.num}-${it.slug}.md. Read it completely before doing anything. Follow its steps in order, run every verification command and compare with the expected result. It is written for an executor with no context; it is the only authority for scope. If any STOP condition fires, or reality differs from its "Current state" excerpts, or a step's verification fails twice after a reasonable fix, STOP: do not improvise, do not commit, and return status BLOCKED with the exact reason and what you had done.
Path-scoped rules do not auto-load for files outside E:\\repos\\TAOM, so read the rules the plan names yourself from ${it.wt}\\.claude\\rules\\ (and csharp-architecture.md for any C# change).

HOUSE RULES THAT BIND YOU:
- TDD: write the failing test first and SEE it fail (quote the failure), then implement, then see it pass.
- Build and test only in the worktree, only with the non-deploying forms: dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=  and  dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= . NEVER ./build.ps1. Keep build and test temp files off C: by prefixing every dotnet command with TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp (mkdir -p it first).
- Known baseline failures you must NOT chase: exactly two tests fail at the base because another session is editing the live Armory: TheElkItem_DeclaresTheScaleTheReachIsTunedFor and AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist. Everything else must pass. Two [Ignore] tests are expected.
- Architecture: ADR-002 thin entry points, ADR-007 adapters, no #region, no [Obsolete], no #if DEBUG outside IoC.cs registration. Prose you write (comments aside) has no em or en dashes.
- Do NOT edit plans/README.md (the orchestrator maintains the index). Do not create GitHub issues. Never push, merge, rebase, stash, reset, switch branches or delete branches.
- When the plan's done criteria all hold: add a CHANGELOG.md entry at the top of the newest date section of ${it.wt}\\CHANGELOG.md in the house style (read the first 40 lines to match it; heading '### <type>(<scope>): v2.0.30 - <description>' under a '## 2026-09-24' date heading, creating that heading above the newest one if absent; plain prose, no em or en dashes), then stage EXPLICIT paths only (git add <path> ...; never -A, never .), and commit with subject '<type>(<scope>): v2.0.30 - <description>' (72 characters max, version from Main/_Module/SubModule.xml), a body wrapped at 72 that says what changed and why and names plan ${it.num}, and NO Co-Authored-By or other AI attribution trailer. Add a 'Not-tested:' trailer for anything structurally untestable (live Harmony, in-game behaviour). If a commit hook denies the commit, read its reason, fix the cause if it is yours, and never bypass hooks (--no-verify is forbidden); if the hook is confused by the worktree, report it as BLOCKED with the hook text.
- Evidence: quote the exact test totals line of the final full-suite run in your return.

Return the structured result: status, commit hash, summary (what you did, in order), files changed, final full-suite totals, any stop reason, deviations from the plan (with why), and what is owed (in-game checks, CI runs, Mike's decisions).`

phase('Execute')
const res = await parallel(ITEMS.map(it => () =>
  agent(prompt(it), { label: `exec-${it.num}`, phase: 'Execute', schema: SCHEMA, model: MODEL, effort: 'high' })
    .then(r => ({ num: it.num, ...(r || { status: 'BLOCKED', summary: 'executor returned nothing', files_changed: [], tests: '' }) }))
))
return res
