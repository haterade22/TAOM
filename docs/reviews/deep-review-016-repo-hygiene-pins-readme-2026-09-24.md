# Deep review: plan 016, repo hygiene, MCP pins and README (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 016, untrack personal and generated files, pin the MCP servers, correct the README
         (#657), branch improve/016-repo-hygiene-pins-readme, diff bec0389d..8a638831
Date: 2026-09-25 (review opened 2026-09-24)

Scope:   harness config (.claude/settings.json, .mcp.json, .codex/config.toml, .gitignore),
         two SKILL.md files, docs, README, CHANGELOG; no C#, XML or XSLT
Waves:   wave 1: Agent 1 Standards, Agent 4 Completeness, Agent 5 Data flow, Agent 6 Design;
         Codex adversarial (gpt-6-astra, ultra) in parallel; review lead verified and fixed

STANDARDS:     FAIL, 3 LOW violations + 1 NIT (all fixed)
COMPATIBILITY: NOT IN SCOPE (no engine-facing code)
EFFICIENCY:    NOT IN SCOPE (no code)
COMPLETENESS:  INCOMPLETE at review, complete after fixes: pin policy undocumented, two unpinned
               copies of launch strings, stale activation step, entries undated and unlinked
DATA FLOW:     FAIL, 0 gaps in changed lines (3 pre-existing), 5 LOW inconsistencies (all fixed)
DESIGN:        7 KEEP proposals (6 apply, 1 follow-up): 5 applied, 1 needs Mike, 1 follow-up
XML:           NOT IN SCOPE
TOOLING:       PASS (hook suite 392/0, reviewctl 43 OK, audit HIGH:1 is the known fixture)
```

## Verification of every finding

A finding is a hypothesis until re-read. Each row below was checked against the worktree at
`8a638831` (or at `b2e387db` / `bec0389d` / `origin/bannerlord-1.4.5` where it says so).

| # | Source | Finding | Verdict | Evidence read this pass |
|---|---|---|---|---|
| D1 | A1#1, A4 C6 | Both plan 016 CHANGELOG entries sit under `## 2026-09-24`; the commits are dated 2026-09-25 | CONFIRMED LOW | `git log`: `470ee64e` 2026-09-25 08:44, `8a638831` 08:45; `## 2026-09-25` existed at line 222 |
| D2 | A1#2, A4 C6 | Neither entry nor commit names #657 | CONFIRMED LOW | `gh issue view 657`: OPEN, created 2026-09-24T18:52Z |
| D3 | A1#3, A4 C3, A6 P5 | The pin policy (bump every copy together, audit only sees `npx -y` in `.mcp.json`, re-derive the deny list) lives only in the plan | CONFIRMED LOW | `mcp-servers.md` Configuration and deny sections said nothing about pins; `readOnlyHint: false` spot-checked in the cached filesystem (4) and git (5) servers |
| D4 | A1#4 | "Culture-specific" is the only headline bullet capitalised after the dash | CONFIRMED NIT | README lines 119 to 141: every other bullet is lowercase after the dash |
| D5 | A5 F1, A4 C1, C2 | `.vscode/mcp.json.example` (filesystem, git) and the `.mcp.json` snippet in `kingdom-voices.md` (elevenlabs) stayed unpinned | CONFIRMED LOW | read both files; `.mcp.json:27,37,61` carry the pins |
| D6 | A5 F2, A4 C4 | `development-machines.md` recounted "Two things", omitting `.codex/config.toml`, and said "each machine" where the file is per checkout | CONFIRMED LOW | `.codex/config.toml:17-22` hardcode `E:\` paths |
| D7 | A5 F3, A4 FU6 | A lesson's **Prevent:** still places the deny list in `settings.local.json` | CONFIRMED LOW | `lessons/build-tooling-workflow.md:1817` |
| D8 | A5 F4, A4 C5(b)(c) | The migration note omits that switching to `bannerlord-1.4.5` overwrites the ignored copy (and switching back removes it), and that a new worktree starts without one | CONFIRMED LOW [Likely for the switch mechanics, per git's documented handling of ignored files] | `git ls-tree origin/bannerlord-1.4.5` still lists `.claude/settings.local.json` (blob `ca241bc3`) |
| D9 | A5 F5, A4 C5(a), A6 P2 | The restore command `git show ... > file` is shell-dependent | CONFIRMED LOW | Proved: Windows PowerShell 5.1.26100 `git show b2e387db:crashz/manifest.txt > file` wrote `FF FE` + UTF-16LE. `git restore --source=b2e387db -- crashz/manifest.txt` exit 0, path stays `!!` ignored, `git ls-files crashz` empty |
| D10 | Codex 1, A6 P7 | ModuleData MCP activation step says the server is already enabled in `settings.local.json`, false on a fresh clone | CONFIRMED LOW | `moduledata-validation.md:686` |
| O1 | Codex 2 | The plan's drift assumptions were stale (the base already carried strict runsettings); literal Step 11 would have dropped them | AGREE as a plan observation, not a defect | `agent-operating-manual.md:43` keeps `--settings TAOM.Tests/binding-gate.runsettings`; the implementation is right, the plan file is a historical record |
| N1 | A1 H2 | No `harness-facts.md` row proves Claude Code honours `permissions.deny` from the shared file; `/permissions` may not show the source file | NEEDS MIKE (post-merge check) | UNVERIFIED; Codex cites the settings docs for array merge and deny precedence |
| N2 | A6 P3 | Both skills cite `crashz/report.json` as the cost of lacking a native image, but it is a managed crash | NEEDS MIKE (behaviour-changing skill text) | `git show b2e387db:crashz/report.txt`: `System.TypeInitializationException` for `System.MemoryExtensions`; 0 hits for `TaleWorlds.Native`, `AccessViolation`, `0xC0000005`, `SEHException` in all four files; #385 is the open native CTD |

**False positives:** none. Every lens and Codex claim that reached a verdict held.

Also re-verified: the nine denies in `.claude/settings.json` equal the base `settings.local.json`
list in order, `permissions` holds only `deny`, and the rest of `settings.json` is unchanged from
`bec0389d`. `git check-ignore -v` maps the three paths to `.gitignore:242,245,248`.

## DETAILS

**Agent 1, Standards.** Checks 1 to 10 N/A (no C# or C++). Violations D1, D2, D3, NIT D4. Harness
checks: H1 pass, H2 not engaged (N1 raised), H3 not engaged, H4 pass (two pre-existing em dashes on
partly rewritten lines, `README.md:121` and `mcp-servers.md:68`, exempt as existing prose), H5 pass
with no growth, H6 pass.

**Agent 4, Completeness.** Tests N/A (no code); regression gates re-run green. Gaps C1 to C6, which
map to D2, D3, D5, D6, D8, D9 and D1. Follow-ups FU1 to FU6 below.

**Agent 5, Data flow.** 23 flows traced: 15 connected, 5 inconsistent (F1 to F5 = D5, D6, D7, D8,
D9), 3 gaps, all pre-existing or cross-branch (serena write tools, Codex deny list, CHANGELOG
generator on `improve/020`).

**Agent 6, Design.** 7 KEEP proposals. P1 (`git clone -b`) and P4 (both copy flags) applied as
improvements; P2, P5 and P7 applied as the fixes for D9, D3 and D10; P3 needs Mike (N2); P6 is a
follow-up.

**Codex.** See the CODEX REVIEW section.

## ACTION ITEMS

All confirmed items are fixed on the branch. Remaining, in priority order:

1. Mike: decide the `bannerlord-1.4.5` port (FU1), the serena write-tool denies (FU-S) and N2.
2. Post-merge: `/permissions` lists the nine denies with no local deny list present, and `/mcp`
   shows serena v1.7.0 connected (N1). Record the result as a `harness-facts.md` row.
3. Orchestrator: one convergence `deep-reviewer` pass on the fix diff (Step 4.6); this lead cannot
   spawn agents.

## IMPROVEMENTS (Step 4)

APPLIED (docs only; characterisation gates green before and after: `tools/tests/test_ai_documentation.py`
4 passed, `lint_docs.py --summary --fail-on-drift` exit 0, full suite below):

- `README.md:48-49` (P1): `git clone -b bannerlord-1.5.x ...` replaces clone plus `git switch`.
  Plan Step 10 greps still hold: `v1\.5\.3` on 4 lines, `v1\.4\.8|v1\.5\.2` only on line 15.
- `docs/ai-includes/agent-operating-manual.md:41` (P4): the Build row says both flags do work
  (`DeployTAOMDependenciesStubs` at `Dependencies/TAOM.Dependencies.csproj:90-92` gates only on
  `DisableModuleCopy`).
- P2, P5, P7: applied as the D9, D3 and D10 fixes.
- Defect fixes: D1 and D2 (`CHANGELOG.md`, entries moved under 2026-09-25 with `(#657)`), D3
  (`mcp-servers.md` deny-list derivation sentence and a **Pins.** paragraph), D4 (`README.md:121`),
  D5 (`.vscode/mcp.json.example`, `docs/features/kingdom-voices.md:404,417`), D6 and D8
  (`development-machines.md`: "Three things", a `.codex/config.toml` bullet, per checkout, new
  worktrees, the 1.4.5 switch), D7 (`lessons/build-tooling-workflow.md:1817`, a parenthetical; the
  lesson is not rewritten), D8 and D9 (`CHANGELOG.md` restore note now `git restore --source=...`),
  D10 (`moduledata-validation.md:686`).

NOT APPLIED:

- `.claude/skills/engine-bump/SKILL.md:29-31`, `.claude/skills/native-crash-triage/SKILL.md:74-75`,
  `docs/migration/v1.4.8-impact.md:136` (P3): behaviour-changing for agents running both skills;
  needs Mike (N2).

FOLLOW-UP (pre-existing, not applied; no issue filed here because filing is a public action this
assignment does not authorise; #657 lists several of them):

- **FU1 MED:** `bannerlord-1.4.5` (GitHub default) still tracks the settings file, the pickle and
  `crashz/`, with unpinned servers; port the change or change the default branch. Removes D8's
  switch hazard.
- **FU-S MED:** serena v1.7.0's write tools (`replace_content`, `replace_in_files`, the symbol
  editors, the memory writers) pass no hook; `.serena/project.yml` has `read_only: false`. Adding
  denies edits the protected `settings.json`.
- **FU5 LOW-MED:** Codex has no equivalent of the nine denies; whether its config can filter a
  server's tools is UNVERIFIED.
- **P6 / FU2:** a `lint_docs.py` `check_file` for the README Bannerlord version, and audit rules for
  unpinned `uvx`/`git+`, `.codex/config.toml` and the `.vscode` example.
- **FU3:** `TRANSLATOR_GUIDE.md:376`, `tools/lint_docs.py:99-105`, `tools/tests/test_lint_docs.py:526-530`
  still call `bannerlord-1.4.5` the active branch.
- **FU4:** `mcp-servers.md` table and Configuration list omit `elevenlabs`; `:50` says "(v1.4.7) DLL";
  `docs/INDEX.md` "9 servers" is hand-kept.
- README quick start runs `build.ps1` right after `setup-dev-env.ps1`, which sets only the User-scope
  variable; `build.ps1:11` checks that scope but `dotnet` inherits the old process environment
  [Likely a first-run failure in the same terminal].
- Other agent-facing commands still deploy (`refactoring-specialist.md`, `agent-teams.md`,
  `build-fix/SKILL.md`, `deslop/SKILL.md`, `new-culture-authoring.md`, `testing-guide.md`,
  `check-verification-evidence.sh:46`).
- `scan.sh:429,431` tool counts (13, ~14) disagree with the pinned servers (14, 12); `*.dmp` is not
  ignored; transitive dependencies are not pinned (rejected for a dev tool).
- Skill descriptions summarise instead of "Use when..."; `Main/Features/TrollBruteForce` has no
  feature doc; five files cite `b2e387db`, so a history purge must update them.
- Merge order: `improve/020` regenerates the CHANGELOG from commit bodies, and the restore command
  lives only in the CHANGELOG (the `470ee64e` body says "see CHANGELOG"); `impl-005` edits the same
  `.mcp.json` lines; `improve/011` makes "the safety hooks match Bash only" stale.

## CODEX REVIEW

`docs/reviews/raw/codex-adversarial-016-repo-hygiene-pins-readme-2026-09-24.md`, complete
(`END OF CODEX REVIEW` present), 141,589 tokens. Codex reviewed through git refs, quoted no engine
code (none applies), cross-referenced every changed key, pin and path against its source, and
checked each pin against published registry metadata. It answered all ten Known Suspects: none
confirmed as a defect; #3 confirmed as plan drift (O1).

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P3 | LOW | Yes | `moduledata-validation.md:686` states per-user enablement as done; fixed (D10) |
| 2 | P3 (plan observation) | none | Yes, as an observation | The plan was stale; the implementation kept the strict runsettings. No change |

- **Confirmed bugs:** D10.
- **False positives:** none.
- **Design questions:** none raised by Codex.
- **Things Codex missed:** D1 to D9 (the unpinned copies, the shell-dependent restore, the branch
  switch and new-worktree effects, the recount, the stale lesson, the undocumented pin policy, the
  date and issue link, the NIT), plus N2 and FU-S.

Root cause table for the Codex-confirmed bug (Phase 3e):

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | Activation step claims per-user enablement is done | Other: stale statement after a moved fact | The plan prescribed unlinking the filename, not re-reading the sentence around it | Lesson "When a fact moves, grep every statement of it" in `lessons/build-tooling-workflow.md` |

## AGENTS.md lessons (pending)

Not edited here (Phase 3h is consolidated across branches). Proposed:

- **Bugs Codex typically misses:** other copies of a changed config value outside the file the
  audit reads (templates, doc snippets); a documented command whose result depends on the shell
  (PowerShell 5.1 `>` writes UTF-16); what untracking a file does to branch switches and new
  worktrees when another live branch still tracks it.
- **What Codex does well:** checking each pinned version against published registry metadata and
  cross-referencing every moved settings key against the base blob, in order.

## Verification

- Full suite, worktree at the fix tree (branch contains `a39a9c86`, so nothing may fail):
  `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` gave
  `Passed! - Failed: 0, Passed: 10629, Skipped: 2, Total: 10631`, exit 0.
- `bash tools/test_hooks.sh` (TMPDIR on E:): 392 passed, 0 failed.
- `python -m unittest tools.tests.test_reviewctl`: 43 tests OK.
- `python tools/audit_claude_config.py`: `HIGH:1 INFO:7`, the HIGH is the known fixture; no
  `mcp-npx-unpinned`. `.mcp.json` and `.vscode/mcp.json.example` parse.
- Process note: while proving D9 the lead removed the ignored `crashz/` leftovers in this worktree
  and recreated them with `git restore --source=b2e387db -- crashz` (checkout form, CRLF under
  `core.autocrlf=true`). Nothing tracked changed. The same `autocrlf` applies to a restored settings
  file, which stays valid JSON.

VERDICT: READY FOR COMMIT (Step 4 complete except the convergence pass, which is owed to the
orchestrator; full suite green)

## Convergence

The convergence reviewer read the review-fix diff `8a638831..1ce8c370` and reported four LOW
defects, all documentation, plus two uncounted wording issues. The lead re-read each against the
worktree before changing anything.

| # | Finding | Verdict | Evidence read this pass | Fix |
|---|---|---|---|---|
| C1 | `mcp-servers.md` says the deny list is "exactly the tools the pinned server versions annotate `readOnlyHint: false`", which is false for the pinned serena | CONFIRMED LOW | cached serena `949a27ef` (the `.mcp.json:8` pin): `src/serena/mcp.py:110` sets `readOnlyHint=not can_edit`; editing tools at `file_tools.py:173,218`, `symbol_tools.py:585,618,670`, `memory_tools.py:9`; `.serena/project.yml` has `read_only: false`, `excluded_tools: []`; none is among the nine denies in `.claude/settings.json` | Sentence scoped to the pinned `git` and `filesystem` versions; added that serena's editing tools are `readOnlyHint: false`, not denied, and an open decision for Mike (FU-S) |
| C2 | "Every auto-fetched server runs an exact version" is false for the user-level servers named just above it | CONFIRMED LOW | `~/.claude/.mcp/user.json`: `@modelcontextprotocol/server-sequential-thinking` (no version) and `@upstash/context7-mcp@latest` | "Every auto-fetched project server (`.mcp.json`)"; added that the user-level servers are machine-local and not pinned |
| C3 | `docs/INDEX.md` still lists two things that ignore the env vars; `development-machines.md` now lists three | CONFIRMED LOW | `development-machines.md:31-41` names the decompile scripts, `.mcp.json` and `.codex/config.toml` | INDEX line now names all three |
| C4 | `LESSONS-LEARNED.md` counts stale after the fix added three lessons | CONFIRMED LOW | `grep -c '^### ' docs/reviews/lessons/*.md`: build-tooling-workflow 202, all thirteen files sum to 924 | 199 to 202, 921 to 924 |
| W1 | `development-machines.md` says `.codex/config.toml` hardcodes "the same" `E:\` paths | CONFIRMED wording | `.codex/config.toml:17-22` list `E:\repos\TAOM`, the four module `ModuleData` folders and `E:\Decompiled_Bannerlord`; `.mcp.json:28-30` lists different ones | "its own list of desktop `E:\` paths" |
| W2 | REVIEW-LOG says "the other nine confirmed findings" but lists eight (D4 missing) | CONFIRMED wording | the entry names D1, D2, D3, D5, D6, D7, D8, D9 | Added "a README capitalisation NIT" |

**False positives:** none.

**Verification after the fixes:**

- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` (branch contains `a39a9c86`,
  so nothing may fail): `Passed! - Failed: 0, Passed: 10629, Skipped: 2, Total: 10631`, exit 0.
- `python tools/lint_docs.py --summary --fail-on-drift --dash-base 1ce8c370`: exit 0,
  `ai_dashes: 0`.
- `python -m pytest -q tools/tests/test_ai_documentation.py`: 4 passed, 152 subtests passed.

CONVERGENCE: CLEAN after these fixes (no runtime file changed).
