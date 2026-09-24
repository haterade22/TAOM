# Baseline metrics at `b2e387db` (2026-09-23)

Measured in a detached worktree of `b2e387db` in the session scratchpad, so another live session's
uncommitted files did not enter the numbers. Every step ran sequentially (no timing overlap).
Build commands never deploy (`-p:DisableModuleCopy=true -p:ModuleId=`). Raw logs stay in the
scratchpad (`raw\`); this file is the digest.

## Build, test, gates

| Metric | Value | How measured |
|---|---|---|
| Build `Main/TAOM.csproj`, fresh worktree (restore + compile) | 8 s, 0 errors | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` |
| Build, no-op incremental | 4 s | same command again |
| Build warnings as shipped | **2**: `BHA0001` x1, `BHA0006` x1 (BUTR Harmony analyzer) | deduplicated by file, line, code |
| `BHA0001` | `Main/Features/LordPartyTemplates/Hooks/Patch88_InitializeLordPartyPropertiesScope.cs:22`: "Member 'InitializeLordPartyProperties' does not exist in Type 'InitializationArgs'" | lead for Lane 3: settle against `taom-src` |
| `BHA0006` | `Main/Features/CultureDoctrine/Hooks/TeamTacticProbe.cs:40`: generic `List<BehaviorComponent>` name mismatch on `FormationAI` | likely analyzer false positive; Lane 3 confirms |
| Test suite (build + run) | 40 s wall, 30 s test duration | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` with a trx logger |
| Test results | 10,239 total: **10,235 passed, 2 failed, 2 not executed, 0 inconclusive** | trx `ResultSummary/Counters` and per-result outcomes |
| The 2 failures | `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` (expected `body_length` 200, live Armory has 100) and `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist` | both assert on the **live, unversioned Armory**, which another session is editing right now. A HEAD checkout's suite verdict depends on install state. |
| The 2 not executed | `WargAttack_FastWarg_InvokesRunningAttack`, `WargAttack_SlowWarg_InvokesStandingAttack` | deliberate `[Ignore]` with a documented reason (`WargAttackServiceTests.cs:349,372`) |
| Nullable warnings hidden by `NoWarn` | **2,028** (CS8618 756, CS8603 408, CS8604 331, CS8625 285, CS8600 108, CS8601 78, CS8602 62) | scratch build with `-p:NoWarn= --no-incremental`; nothing committed |
| Nullable, top folders | Adapters 216; Enlistment 145; CulturalFeats 140; CareerSystem 139; SpecialResources 85; Warg 84; LotrIssues 81; SupplyLines 79; DevConsole 70; CharacterCreation 57; HeroRace 57; Refuge 55 | per `Main/<dir>` or `Main/Features/<Name>`; full table in `raw\nowarn-digest.json` |
| `validate_moduledata.py` | 31 s; **0 errors**, 1,591 warnings (ARMOUR_MESH_TIER_LADDER 1,348; MELEE_LADDER_INVERSION 139; INCONSISTENT_ARMOUR_SLOT 93; CROSS_CULTURE_ARMOUR_INVERSION 7; UPGRADE_ARMOUR_REGRESSION 4) | known roster backlog (#609, #631) |
| `lint_docs.py --fail-on-drift` | 9 s, exit 0; 0 dead links, 0 untracked targets, 0 config drift, 0 registry drift; 7 path-scoped rules over the 12,288 B cap | the size warns are ADR-011 territory, not re-audited |
| Clone detection (jscpd, C#, min 70 tokens) | 1,672 sources, 160,344 lines; **76 clones, 1,114 duplicated lines (0.69%)** | `jscpd/jscpd-report.json` (118 KB) |

## CI

| Metric | Value | Evidence |
|---|---|---|
| Workflows | `build.yml`, `doc-budget.yml` | `.github/workflows/` |
| `build.yml` triggers | push and pull_request on **`bannerlord-1.4.5` only**, plus manual | `build.yml:3-8` |
| C# compile job | `runs-on: [self-hosted, windows]`, skipped unless `vars.BANNERLORD_GAME_DIR` is set, never on pull_request | `build.yml:253-259` |
| Net effect on `bannerlord-1.5.x` | no job ever compiles C#; only `doc-budget.yml` runs | triggers above |

## Harness cost per Bash call

| Metric | Value | How measured |
|---|---|---|
| Hook processes per Bash call | **13**: 10 PreToolUse(Bash) + 1 PreToolUse(any) + 2 PostToolUse(Bash) | `.claude/settings.json` |
| Per-hook time on a non-git `ls` payload | 219 to 360 ms each (11 timed, 5 runs each); serial sum **2,834 ms** | `time_hooks.sh` in the scratchpad; `suggest-compact.sh` and `mark-verification-run.sh` not timed (they write state) |
| Floor for comparison | bare `bash` spawn 49 ms; one `python` JSON parse 58 ms | 5-run averages |

Hooks run in parallel in the harness, so wall time per call is closer to the slowest hook plus spawn
contention than to the serial sum. Every one of them still pays its full startup for a command that is
not `git`.

## Repository weight and history

| Metric | Value | How measured |
|---|---|---|
| HEAD tree | **1,210 MB**: .png 501 MB (1,413 files), .psd 324 MB (34), .wav 112 MB (342), .mp3 62 MB (93), .xml 43 MB, .pkl 37 MB (1), .tpac 36 MB (124) | `git ls-tree -r -l HEAD` summed by extension |
| Pack size | 3.27 GiB in 3 packs | `git count-objects -vH` |
| Git LFS | none (`.gitattributes` handles line endings only) | `.gitattributes` |
| `CHANGELOG.md` | 1,759,092 B; touched by **545 of 809** commits since 2026-07-01 | `git log --since=2026-07-01 -- CHANGELOG.md` |
| Commit subjects since the branches split | **10 of 95** miss `type(scope): vX.Y.Z - desc` | merge-base `a4c6d7c3` with `bannerlord-1.4.5`; regex `^[a-z]+(\(...\))?!?: v\d+\.\d+\.\d+ - ` |
