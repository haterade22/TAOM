# Plan 108: Guard the missing collision-body infinite wait (Patch90) and gate missing bodies and meshes at the desk

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. When done, report status, commit hashes and
> measured counts in your hand-off (Step C1). `plans/README.md` belongs to the
> integrator: never edit it.
>
> **Drift check (run first, Step A0)**: `git diff --stat 1b00d1acd..HEAD -- <in-scope paths>`.
> Earlier WPs of this programme are expected to have changed `Main/IoC.cs`,
> `Main/SubModule.cs`, `.claude/hooks/check-moduledata-validation.sh`,
> `docs/reviews/lessons/harmony-il.md` and `docs/reference/harmony-patch-registry.md`.
> The scripts in this plan find their anchors by exact
> text and stop on a mismatch, so a changed file is a STOP only when a script
> says `STOP:` or when Step A0 lists a file that must be unchanged.

## Status

- **Priority**: P0 (Part A, the runtime guard); P1 (Part B, the desk gates)
- **Effort**: M (Part A S, Part B M)
- **Risk**: MED. Part A is LOW (a prefix that never returns `false`). Part B is MED: the new
  validator errors gate every later ModuleData commit, so a defect in the public Armory blocks
  data work until it is fixed, and the two new passes lengthen the commit hook's run.
- **Depends on**: WP103 (executor-safety harness), WP105 (composition root); through them WP101,
  WP102, WP104. STOP if Step A0 finds their marks absent.
- **Category**: bug (load hang) and dx/tests (desk gates)
- **Wave / lane**: Wave 1, lane C
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the entry), D2 (Patch90 has no
  `*Module.cs` on 1.5.x either, so it stays hand-wired in `IoC.cs`/`SubModule.cs`), D7 (co-op
  split `6d7e67f3f` not ported), D8 (subjects carry `v2.0.28`), D10, D19 (git only through the
  Bash tool), D20 (body and mesh verdicts only against the public Armory)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04
- **Issue**: Port of #601 (Patch90), #599 (Armory reference audit), #622 (validator wiring) and
  #633 (borrowed bodies); mechanism #352. Step B3 is new work with no issue: the orchestrator
  creates one before it lands.

## Why this matters

On the installed v1.4.8, `PreloadHelper.WaitForMeshesToBeLoaded` loops until every registered
collision-body name resolves, sleeping 1 ms per pass, with no exit (taom-src
`TaleWorlds.MountAndBlade.View.PreloadHelper.cs:58-75`). One `body_name`, holster body or
collision body that no loaded tpac ships freezes the game on the first frame after the loading
screen drops: no exception, no crash bundle, no TAOM log line. The 1.4.x line shipped this
already (#352: two Armory `body_name` typos, one of which hung every siege with Dunland troops),
and the maintainer's 1.4.8 history of infinite loading screens includes wrong XML data and mesh
ids. Players run TAOM 1.4.x with the 2026-09-29 Armory publish (`LOTRLOME_Armory:v2.0.29.5` in
the 2026-11-01 player log), whose ids the public troop tree was re-pointed to in `92a30ba42`, and
no gate on this line has checked that Armory's bodies and meshes: the laptop still has `v2.0.26`,
and the xml-data lens could not check the 22 Gondor weapons and shields the public tree adds.

This WP ports the 1.5.x runtime guard (Patch90 drains unresolvable body names after a 5 s
budget, logs them as `[PreloadGuard]`, and lets the load finish) and the desk gates that catch
the defect before a release (`tools/audit_armory_refs.py`, `MISSING_COLLISION_BODY`,
`COLLISION_BODY_BORROWED`, `validate_mesh_refs.py --check-name`). It is **not** the fix for the
2026-11-01 Harad first-battle freeze: that load stopped before `BattlePlayable`, and this wait
runs about eleven frames after it (V02).

## Current state

Files (all read on `origin/bannerlord-1.4.5` at `1b00d1acd`):

- `Main/SubModule.cs`: applies patch categories. In `OnSubModuleLoad` the Patch61 save-load block
  ends at :334 and `// Patch62 <em dash> containment guard (#339)` is :336 (the comment contains a
  U+2014 dash; the scripts build it with `chr(0x2014)`). After WP104 the calls are
  `TryPatchCategory("...")`. Nothing names Patch90.
- `Main/IoC.cs`: `using TAOM.Features.BattleLoadDiagnostics;` at :66,
  `BattleLoadDiagnosticsIoC.RegisterBattleLoadDiagnosticsFeature(container);` at :158.
- `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs`: every bool-returning prefix
  must have a registry row (`EveryBoolPrefix_HasACoopDisposition`, 5 test methods). The last
  ReviewedSafe row is `LordConversationsAllyThanksConditionPatch` at :267-268, then
  `// --- Parked` at :270.
- `tools/validate_moduledata.py` (230 lines): imports at :41-45 (no `re`), the only extra pass
  `generator_item_ref_issues` at :94-132, `main()` at :135-226 calling it at :195-200. No
  collision-body pass.
- `tools/taom_schema.py:218`: `self.missing_ref_roots = [str(r) for r in requested if not r.exists()]`,
  while `validate_moduledata.py:188` calls `root.parent.name` on each, so a Modules folder missing
  one live module crashes `main()`.
- `tools/validate_mesh_refs.py`: `DEFAULT_GAME` is the desktop literal `E:\Steam\...` (:82-84), so
  every run on the laptop passes `--game` and `--items`. `KNOWN_DEAD_MESHES` (:105-115) holds
  `lotr_troll_armor`, `lotr_troll_bracers`, `lotr_troll_helmet`. `_ITEM_OPEN_RE` (:210) matches
  `<Item>`/`<CraftedItem>` only. `tpac_paths_for_modules` (:422) prefers a cooked tree wherever one
  exists. `main()` runs `classify(..., check_allowlist=True)` (:1159).
- `tools/generate_armory_catalogue.py:279-287`: `build_rows` globs `<module>/Assets/**/*.tpac`
  only, so on a cooked-only install it returns no rows.
- `.claude/hooks/check-moduledata-validation.sh:97-107`: the `--code` allowlist ends with
  `        --code MOUNT_WITHOUT_HARNESS \`; on timeout (:109-114) it asks instead of passing.
  `tools/tests/test_validate_moduledata.py` `CommitGateCoverageTests` (:2071, last class in the
  file) pins every ERROR code to that list.
- `tools/tests/test_validate_moduledata.py`: imports :19-25 (no `io`, `mock`, `redirect_*`),
  `self.assertEqual([Path(p) for p in v.missing_ref_roots], [gone])` at :609, `ExtraRefRootTests`
  :1659, `MountWithoutHarnessTests` :1931. 154 `def test_` methods.
- `tools/tests/test_validate_mesh_refs.py`: 60 `def test_` methods; the allowlist tests read the
  shipped troll entries.
- `.claude/rules/moduledata-validation.md:90-91`: `GENERATOR_RETIRED_ITEM_REF` row then
  `FORTIFICATION_WITHOUT_VILLAGE` row.
- Wrong on 1.4.x today, left to WP113: `Main/Features/BattleLoadDiagnostics/Hooks/MissionState_TickLoading_BattleLoad_Patch.cs:12-16`,
  `docs/features/battle-load-diagnostics.md:94`, `docs/reference/harmony-patch-registry.md:316` and
  `tools/triage_battle_load.py:1104-1113` attribute a `polls=1` stall inside `TickLoading` to the
  #352 wait, which runs after loading (below).

Laptop facts: the installed Armory and TAOM_Map are cooked (`AssetPackages/`, no `Assets/`). The
public build is cooked too: a player's 2026-11-01 rgl log loads
`$BASE/Modules/LOTRLOME_Armory/AssetPackages...`. Before WP101's refresh the laptop Armory is
`v2.0.26`; the public one reports `v2.0.29.5`. On the laptop the hook's own validator command
took 22.1 s in WP103's planning (`plans/103-executor-safety-harness.md`; the xml-data lens reported
41 s for a full run, not re-run here). The hook's inner bound is 45 s, and the two new passes add
about two `extract_refs` runs (about 3.5 s each on the 1.5.x desktop).

Engine facts (verified this session; do not re-derive):

```
// taom-src v1.4.8 TaleWorlds.MountAndBlade.View.PreloadHelper.cs
public class PreloadHelper                                                        // :10
private readonly HashSet<string> _uniqueDynamicPhysicsShapeName = new HashSet<string>();  // :14
public void WaitForMeshesToBeLoaded()                                             // :58-75
//   do { num = 0; foreach mesh num += CheckResources();
//        foreach name num += GetFromResource(name, true) == null ? 1 : 0;
//        Thread.Sleep(1); } while (num != 0);
private void RegisterPhysicsBodyUsageIfValid(HashSet<string> uniquePhysicsShapeName, string physicsShape)  // :230-236
// taom-src v1.4.8 TaleWorlds.Engine.PhysicsShape.cs
public sealed class PhysicsShape : Resource                                       // :8
public static PhysicsShape GetFromResource(string bodyName, bool mayReturnNull = false)  // :10
// taom-src v1.4.8 TaleWorlds.Engine.MetaMesh.cs
public static MetaMesh GetCopy(string metaMeshName, bool showErrors = true, bool mayReturnNull = false)  // :91
```

- Callers of the wait, v1.4.8 module dump `C:\Decompiled_Bannerlord\_modules_build`:
  `SandBox__SandBox.View.cs:4978` (`MissionPreloadView.OnSceneRenderingStarted`, every troop of
  every party in the player's map event), `:6628` (`ArenaPreloadView`, tournaments and practice),
  `Native__TaleWorlds.MountAndBlade.View.cs:26761` (custom battle), `SandBox__SandBox.GauntletUI.cs:1575`
  (education screen, behind its `_startedRendering` latch), `Multiplayer__...Multiplayer.View.cs:201`,
  `NavalDLC__NavalDLC.View.cs:1691`.
- Ordering: `MissionScreen` counts `_loadingScreenFramesLeft = 15` frames (Native View dump
  :12583, :16474-16488), then `OnSceneRenderingStarted` drops the loading window and calls every
  view's `OnSceneRenderingStarted` (:13189-13197). `MissionState` runs `TickMission` only when
  `Handler.RenderIsReady()` (taom-src `MissionState.cs:110-113`) and calls `Mission.OnTick` only
  when `_missionTickCount > 2` (:215-218). So `BattleLoadPhaseBehavior.OnMissionTick`
  (`Main/Features/BattleLoadDiagnostics/Hooks/BattleLoadPhaseBehavior.cs:22-38`) logs
  `BattlePlayable`, closes the watchdog window and clears the in-flight marker before the wait
  starts.
- `FightTournamentGame.GetParticipantCharacters` adds `CharacterObject.PlayerCharacter` first
  when `includePlayer` is true, its default
  (`_categories_v1.4.8\Campaign\...\FightTournamentGame.cs:105-111`), so the player's kit is in
  the preload set of a tournament the player enters.
- A missing visual mesh cannot hang the wait: `MetaMesh.GetCopy(name, false, true)` returns null
  and `RegisterMetaMeshUsageIfValid` drops it (`PreloadHelper.cs:211-228`).

What the 1.5.x line has (all read this session):

- `fb8d73145` adds `Main/Features/PreloadBodyGuard/` (4 files), 2 test files, a CoopVeto row,
  the feature doc and an RCA. On the 1.5.x tip `e45f6df2c` the feature is still hand-wired
  (`Main/IoC.cs:70,180`, `Main/SubModule.cs:436-452`, using `TryPatchCategory`); no
  `Main/Composition` file names it.
- The 1.5.x tool files descend from the fork point unchanged on 1.4.x: `git diff a4c6d7c30
  origin/bannerlord-1.4.5` is empty for `validate_mesh_refs.py`, `validate_moduledata.py`,
  `taom_schema.py`, both test files, the hook and the rule. So an end state of a 1.5.x commit equals
  the 1.4.x file plus exactly that commit series.

Conventions that bind this change (one line each):

- ADR-007: services never touch sealed TaleWorlds types. `PreloadBodyGuardService` is pure (the
  resolver, clock and sleep are injected); only the hook class touches `PreloadHelper`/`PhysicsShape`.
- ADR-002: thin entry points under 150 lines; the patch class (92 lines) delegates to the service.
- ADR-003 no `#region`; ADR-004 no `[Obsolete]`; ADR-005 no `#if DEBUG` outside IoC registration.
- TDD: a failing test first for new work (Step B3); ported code arrives with its 1.5.x tests.
- MCM json2 rule (rename a persisted setting, never flip its default): no setting is added here;
  the guard has none on purpose (a guard against a frozen game has no reason to be off).
- `.claude/rules/harmony-patches.md`: MovementOrder postfixes route through the shared deferred
  category; not applicable (Patch90 is a prefix on a view helper).
- D2 / WP105: a 1.5.x feature that has a `*Module.cs` arrives as that module; Patch90 has none, so
  it is hand-wired exactly as on the 1.5.x tip.

## Commands

Run every command from the worktree root, `WT=/c/Users/mikew/source/repos/taom-port/108`, with
`cd "$WT" && ...` in each Bash call. `PY=/c/Python314/python.exe` (the laptop's interpreter). Shell
variables do not persist between tool calls, so start each call with
`WT=/c/Users/mikew/source/repos/taom-port/108; PY=/c/Python314/python.exe;`. Wherever this plan
reads `plans/README.md` (a prerequisite, a baseline, a test count) it means the main checkout's copy,
`/c/Users/mikew/source/repos/TAOM/plans/README.md`, read-only.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~PreloadBodyGuard\|FullyQualifiedName~CoopVetoClassification\|FullyQualifiedName~HarmonyPatchBindingTests"` | 0 failed, 0 skipped among the PreloadBodyGuard tests |
| Tool tests | `"$PY" -m unittest discover -s tools/tests -t .` | no new failure vs Step B0 |
| One tool test file | `"$PY" -m unittest tools.tests.test_validate_moduledata` (or another module) | OK |
| Hooks | `bash tools/test_hooks.sh` | no new failure vs Step B0 |
| Data | `"$PY" tools/validate_moduledata.py` | no new ERROR vs the WP101 baseline |
| Docs | `"$PY" tools/lint_docs.py` | no new finding in touched files |

Both `-p:` flags are mandatory on every build and test (`Main/TAOM.csproj:133-136` does not gate
CopyModule on DisableModuleCopy). Never `./build.ps1`. Never two builds in one worktree. In the
Filtered row the `\|` is markdown escaping: type a plain `|` inside the quotes.

## Scope

**In scope** (the only files to create or modify):

- Part A: `Main/Features/PreloadBodyGuard/IPreloadBodyGuardService.cs`,
  `.../PreloadBodyGuardService.cs`, `.../PreloadBodyGuardIoC.cs`,
  `.../Hooks/PreloadHelper_WaitForMeshesToBeLoaded_Patch.cs` (new);
  `TAOM.Tests/Features/PreloadBodyGuard/PreloadBodyGuardServiceTests.cs`,
  `.../PreloadBodyGuardBindingTests.cs` (new);
  `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs` (one row);
  **`Main/IoC.cs`** (single-owner: one `using`, one registration line) and **`Main/SubModule.cs`**
  (single-owner: one 18-line block before Patch62); `docs/features/preload-body-guard.md`,
  `docs/reviews/rca-preload-body-guard-2026-09-15.md` (new);
  `docs/reference/harmony-patch-registry.md` (one section);
  `docs/reviews/lessons/harmony-il.md` (two lessons, orchestrator note K2).
- Part B: `tools/validate_mesh_refs.py`, `tools/tests/test_validate_mesh_refs.py`,
  `tools/audit_deleted_mesh_impact.py` (comment), `tools/audit_armory_refs.py` (new),
  `tools/tests/test_audit_armory_refs.py` (new), `tools/validate_moduledata.py`,
  `tools/taom_schema.py` (one line), `tools/tests/test_validate_moduledata.py`,
  `tools/tests/test_collision_body_borrowed.py` (new), `tools/blender/add_collision_body.py` (new),
  `tools/tests/test_add_collision_body.py` (new), `.claude/hooks/check-moduledata-validation.sh`
  (two codes and a comment: `CommitGateCoverageTests` requires them),
  `.claude/rules/moduledata-validation.md` (three rows), `docs/features/armory-ref-audit.md` (new),
  `docs/features/mesh-ref-validation.md`, `docs/features/moduledata-validation.md`,
  `docs/reference/armory-guide.md`, `docs/modding/items-weapons-and-crafting.md` (from the pick),
  `docs/ai-includes/weapon-creation-workflow.md`, `docs/features/ranged-ladders.md`,
  `docs/reviews/rca-rhun-longbow-collision-body-2026-09-21.md` (new); Step B5 only:
  `docs/audits/armory-ref-audit.md` (new, generated), `docs/audits/README.md` (one row).

`Main/TAOM.csproj` needs no edit (SDK-style, default compile globbing). Nothing under
`Dependencies/` changes, so the #371 pairing rule does not apply.

**Out of scope** (do not touch): `plans/README.md` (the integrator's), `CHANGELOG.md`, `CLAUDE.md`, `AGENTS.md`, `docs/INDEX.md`,
`docs/reference/feature-map.md`, `docs/reference/doc-lookup.md`, `docs/reference/hooks-catalog.md`,
`tools/README.md`, `docs/reviews/REVIEW-LOG.md`, `docs/reviews/LESSONS-LEARNED.md`, every other
lessons file, `.claude/settings*.json`, `.claude/hooks/session-start.sh`, `.claude/skills/**`,
`docs/reference/armory-catalogue/catalogue.tsv` (never regenerated from a cooked install),
`docs/reference/taleworlds-api-snapshot/*` (regenerated later by WP137 Part D),
`Main/Features/BattleLoadDiagnostics/**` and `tools/triage_battle_load.py` (WP113),
`tools/validate_xml_schemas.py` and its test (WP136), `Main/Features/Mumakil/**`, anything in
the game install (executors never write there; the Armory is the maintainer's).

## Git workflow

- Worktree and branch (Bash tool only, D19):
  `mkdir -p /c/Users/mikew/source/repos/taom-port && git -C /c/Users/mikew/source/repos/TAOM worktree add C:/Users/mikew/source/repos/taom-port/108 -b port148/108-missing-body-load-hang bannerlord-1.4.5`.
  Work only in the worktree. Never push, open a PR, tag, or merge into `bannerlord-1.4.5`.
- Helper scripts and commit messages live OUTSIDE the worktree in
  `/c/Users/mikew/source/repos/taom-port/wp108-scripts/`. Save every script block below to that
  folder **with your file-writing tool, not a shell heredoc** (shell quoting mangles backslashes and
  escaped quotes), then run it with `cd "$WT" && "$PY" ../wp108-scripts/<name>.py`. A script that
  prints `STOP:` and exits non-zero is a STOP condition.
- Per-port procedure (binding): apply a 1.5.x commit with `git cherry-pick -n <sha>` only; never
  let cherry-pick, `--continue`, `-C`, `-c` or `--amend --no-edit` create a commit. Drop
  bookkeeping hunks. Stage explicit paths (`git add <paths>`, never `-A`), then in the NEXT
  sequential Bash call run `git commit -F <msgfile>`. If
  `test -f "$(git rev-parse --git-path CHERRY_PICK_HEAD)"` succeeds after staging, run
  `git cherry-pick --quit` first (it keeps the staged changes) so the commit is authored now.
  Build and test after each commit; full suite at the end of each part.
- Commit template (each message below is complete; fill only the `<...>` measured values, never
  invent them; wrap at 72; no em or en dashes; no Co-Authored-By or "Generated with" line):

```
<type>(<scope>): v2.0.28 - <description>        (at most 72 characters)

<body for 1.4.x readers>

Research: <what was verified on v1.4.8>
Not-tested: <what needs an in-game check>
Ported-from: <40-hex sha>                       (one line per included 1.5.x commit)
```

## Part A: the Patch90 runtime guard

### Step A0: Worktree, prerequisites, drift check, helper module

1. Create the worktree (Git workflow above), then `cd "$WT" && dotnet restore TAOM.sln` (exit 0).
2. Prerequisite marks; each must print a non-zero count or a listing, else STOP:
   - WP104: `cd "$WT" && grep -c "TryPatchCategory(" Main/SubModule.cs`
   - WP105: `cd "$WT" && ls Main/Composition/`
   - WP103: `cd "$WT" && grep -c "hookSpecificOutput" .claude/hooks/check-moduledata-validation.sh`
   - WP102: `cd "$WT" && test ! -f .claude/hooks/check-changelog-changed.sh && echo gone` (expected
     `gone`: WP102 ports `c91bb95f8`, which deletes that hook. If nothing prints, read WP102's row in
     `plans/README.md` and STOP unless it says DONE; this plan never stages `CHANGELOG.md`)
3. Must not exist yet (else STOP: someone ported part of this already):
   `cd "$WT" && ls Main/Features/PreloadBodyGuard tools/audit_armory_refs.py 2>&1 | head -3`
   (expected: two "No such file" lines) and `cd "$WT" && git grep -n "MISSING_COLLISION_BODY" -- tools .claude`
   (expected: no output).
4. Must be unchanged since planning (the Part B end-state transplants would overwrite another WP's
   edit), else STOP:
   `cd "$WT" && git diff --stat 1b00d1acd..HEAD -- tools/validate_mesh_refs.py tools/tests/test_validate_mesh_refs.py tools/validate_moduledata.py tools/tests/test_validate_moduledata.py tools/taom_schema.py tools/audit_deleted_mesh_impact.py tools/generate_armory_catalogue.py`
   (expected: no output).
5. Informational drift listing (expected changes are listed in the header box):
   `cd "$WT" && git diff --stat 1b00d1acd..HEAD -- Main/IoC.cs Main/SubModule.cs TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs .claude/hooks/check-moduledata-validation.sh .claude/rules/moduledata-validation.md docs/reference/harmony-patch-registry.md docs/reviews/lessons/harmony-il.md docs/features docs/reference/armory-guide.md docs/ai-includes/weapon-creation-workflow.md docs/modding/items-weapons-and-crafting.md docs/audits/README.md`
6. Save the helper module as `wp108-scripts/wp108_edit.py`:

```python
"""WP108 edit helpers. Lives outside the worktree; delete the folder when the WP has landed."""
import os
import pathlib
import re
import subprocess
import sys


def show(spec):
    """`git show <spec>` as text with LF line ends (run from the worktree root)."""
    out = subprocess.run(["git", "show", spec], capture_output=True, check=True).stdout
    return out.decode("utf-8").replace("\r\n", "\n")


def load(path):
    """(text with LF line ends, the file's own newline). A UTF-8 BOM stays in the text."""
    raw = pathlib.Path(path).read_bytes().decode("utf-8")
    return raw.replace("\r\n", "\n"), ("\r\n" if "\r\n" in raw else "\n")


def save(path, text, nl):
    """Write back with the file's own newline and BOM, so the diff is only the edit."""
    pathlib.Path(path).write_bytes(text.replace("\n", nl).encode("utf-8"))


def stop(msg):
    sys.exit("STOP: " + msg)


def once(text, old, new):
    n = text.count(old)
    if n != 1:
        stop("expected exactly one match, found %d: %r" % (n, old[:80]))
    return text.replace(old, new)


def cut(text, start, end=None):
    """From the only occurrence of `start` up to (not including) the next `end`, or to EOF."""
    if text.count(start) != 1:
        stop("start marker not unique: %r" % start[:80])
    i = text.index(start)
    if end is None:
        return text[i:]
    j = text.find(end, i + len(start))
    if j < 0:
        stop("end marker not found: %r" % end[:80])
    return text[i:j]


def replace_line(text, prefix, new_lines):
    """Replace the only line that starts with `prefix` by the list `new_lines`."""
    lines = text.split("\n")
    hits = [k for k, line in enumerate(lines) if line.startswith(prefix)]
    if len(hits) != 1:
        stop("expected one line starting with %r, found %d" % (prefix[:80], len(hits)))
    lines[hits[0]:hits[0] + 1] = new_lines
    return "\n".join(lines)


if __name__ == "__main__" and sys.argv[1:] == ["armory-version"]:
    # The same markers WP101 Step A2 uses: the version string is hand-typed in the channel, so the
    # two monster files and a public-tree ladder id are the strong evidence.
    mod = pathlib.Path(os.environ["BANNERLORD_GAME_DIR"]) / "Modules" / "LOTRLOME_Armory"
    m = re.search(r'<Version value="([^"]+)"', (mod / "SubModule.xml").read_text(encoding="utf-8"))
    monsters = mod / "ModuleData" / "Monsters" / "LOTR"
    ladder = any('id="ladder_gondor_bow_t4"' in p.read_text(encoding="utf-8", errors="replace")
                 for p in (mod / "ModuleData").rglob("*.xml"))
    print("LOTRLOME_Armory version:", m.group(1) if m else "UNKNOWN")
    print("elk and animalia monster files:", (monsters / "lotr_monster_elk.xml").is_file()
          and (monsters / "lotr_monster_animalia.xml").is_file())
    print("ladder_gondor_bow_t4 defined:", ladder)
    print("cooked packs:", any((mod / "AssetPackages").glob("*.tpac")),
          " loose tree:", any((mod / "Assets").rglob("*.tpac")))
```

**Verify**: `cd "$WT" && "$PY" ../wp108-scripts/wp108_edit.py armory-version` prints four lines
(version, monster files, ladder id, then the two tree flags on one line). Record them for Step B0.

### Step A1: Port `fb8d73145` (Patch90) with cherry-pick, place the registration by hand

`fb8d73145` "feat(preload): v2.0.29 - Patch90 guards missing-body load hangs (#601)". Method:
cherry-pick. Simulated this session (`git merge-tree --write-tree --name-only --no-messages
--merge-base=fb8d73145^ origin/bannerlord-1.4.5 fb8d73145`): exit 1, conflicts in `Main/IoC.cs`
and `Main/SubModule.cs` only (1.5.x context names MapLoadDiagnostics/Patch89, absent on 1.4.x).
Everything else applies cleanly, including the CoopVeto row.

0. Re-check the bound engine members on the installed DLLs (Research First):
   `cd "$WT" && P=$(pwsh -NoProfile -File tools/taom-src.ps1 path TaleWorlds.MountAndBlade.View.PreloadHelper | tail -1) && grep -n -e "_uniqueDynamicPhysicsShapeName = " -e "public void WaitForMeshesToBeLoaded()" "$P"`
   prints `14:` (the `private readonly HashSet<string>` field) and `58:`;
   `cd "$WT" && P=$(pwsh -NoProfile -File tools/taom-src.ps1 path TaleWorlds.Engine.PhysicsShape | tail -1) && grep -n -e "GetFromResource(string bodyName, bool mayReturnNull = false)" "$P"`
   prints a `10:` line. A different type, name or parameter list is a STOP (line numbers may move).
1. `cd "$WT" && git cherry-pick -n fb8d73145`. Expected: CONFLICT in exactly `Main/IoC.cs` and
   `Main/SubModule.cs`. Any other conflicted path is a STOP.
2. Take our side of both files, then place the registration by script:
   `cd "$WT" && git checkout --ours Main/IoC.cs Main/SubModule.cs`
3. Save and run `wp108-scripts/wp108_a1_register.py`:

```python
"""WP108 Step A1: place the Patch90 registration in Main/IoC.cs and Main/SubModule.cs."""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from wp108_edit import cut, load, once, save, show, stop

t, nl = load("Main/IoC.cs")
t = once(t, "using TAOM.Features.BattleLoadDiagnostics;\n",
         "using TAOM.Features.BattleLoadDiagnostics;\nusing TAOM.Features.PreloadBodyGuard;\n")
t = once(t, "        BattleLoadDiagnosticsIoC.RegisterBattleLoadDiagnosticsFeature(container);\n",
         "        BattleLoadDiagnosticsIoC.RegisterBattleLoadDiagnosticsFeature(container);\n"
         "        PreloadBodyGuardIoC.RegisterPreloadBodyGuardFeature(container);\n")
save("Main/IoC.cs", t, nl)

t, nl = load("Main/SubModule.cs")
if "TryPatchCategory(" not in t:
    stop("Main/SubModule.cs has no TryPatchCategory: WP104 is not merged")
if "Patch90_PreloadBodyGuard" in t:
    stop("Main/SubModule.cs already names Patch90_PreloadBodyGuard")
anchor = "        // Patch62 " + chr(0x2014) + " containment guard (#339)"
block = cut(show("e45f6df2c:Main/SubModule.cs"),
            "        // Patch90_PreloadBodyGuard (#601)", "        // Patch62 ")
t = once(t, anchor, block + anchor)
save("Main/SubModule.cs", t, nl)
print("OK: Patch90 registered in Main/IoC.cs and Main/SubModule.cs")
```

   The inserted block is the 1.5.x tip's (`e45f6df2c:Main/SubModule.cs:436-452`): a comment, then
   `try { ...PreloadHelper_WaitForMeshesToBeLoaded_Patch.Initialize(IoC.Resolve<...IPreloadBodyGuardService>(), IoC.Resolve<IModLogger>()); TryPatchCategory("Patch90_PreloadBodyGuard"); }`
   and a `catch` that logs `[PreloadGuard] init failed, ...`.
4. Check the new C# files are byte-identical to the 1.5.x commit (no conflict touched them):
   `cd "$WT" && git diff --cached --stat fb8d73145 -- Main/Features/PreloadBodyGuard TAOM.Tests/Features/PreloadBodyGuard`
   (expected: no output).

**Verify**: `cd "$WT" && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` exits 0
with 0 errors and no new warning naming `PreloadHelper` or `PreloadBodyGuard` (if one appears,
record it and STOP; do not suppress it). Then the Filtered test row: the 7
`PreloadBodyGuardServiceTests`, the 1 `PreloadBodyGuardBindingTests` (must PASS, not be skipped or
inconclusive: an inconclusive binding test means the game assemblies did not load, an environment
failure to report), the 5 `CoopVetoClassificationTests` and `HarmonyPatchBindingTests` all pass.

### Step A2: 1.4.x docs for Patch90

The cherry-pick brought `docs/features/preload-body-guard.md` and the RCA at their `fb8d73145`
shape. This step adapts them, adds the registry section (from `ef6afeb67`), the #633 paragraph
(from `b23a27ef4`) and the two Patch90 lessons (from `1571c2f91`; orchestrator note K2).
Save and run `wp108-scripts/wp108_a2_docs.py`:

```python
"""WP108 Step A2: 1.4.x docs for Patch90."""
import datetime
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from wp108_edit import cut, load, once, save, show, stop

TODAY = datetime.date.today().isoformat()

# 1. Feature doc.
p = "docs/features/preload-body-guard.md"
d, nl = load(p)
d = once(d, "Vanilla's wait (installed v1.5.3, same shape as v1.4.8):",
         "Vanilla's wait (installed v1.4.8, `TaleWorlds.MountAndBlade.View.PreloadHelper.cs:58-75` in the\n"
         "taom-src cache; the 1.5.x line found the same shape on v1.5.3):")
p633 = cut(show("b23a27ef4:docs/features/preload-body-guard.md"),
           "**#633 (2026-09-21)", "Six callers, all on the main thread")
p633 = once(p633, "`/armory-audit` included (CLEAN, 2026-09-20)",
            "`audit_armory_refs.py` included (CLEAN, 2026-09-20)")
p633 = once(p633, "on a build that had it (v2.0.30,\n`bannerlord-1.5.x` only; the player's version is not recorded)",
            "on a build that had it (the player's version is not recorded)")
old599 = cut(d, "#599 built the desk-side gates (`/armory-audit`", "\nSix callers, all on the main thread")
new599 = ("#599 built the desk-side gates. On the 1.4.x line they are `tools/audit_armory_refs.py` and the\n"
          "`MISSING_COLLISION_BODY` and `COLLISION_BODY_BORROWED` errors in `validate_moduledata.py` (WP108;\n"
          "the 1.5.x `/armory-audit` skill and `ARMORY ART DRIFT` startup line were not ported). None of them\n"
          "reaches a player whose Armory copy already carries a bad pair, and the artists will keep combining,\n"
          "renaming and deleting meshes. This guard is the only protection at the point of failure.\n")
d = once(d, old599, new599 + "\n" + p633.rstrip("\n") + "\n")
callers_end = "`NavalShipsPreloadView`, and `GauntletEducationScreen.OnFrameTick`.\n"
where = (
    "\n### Where it runs on the 1.4.x line, and what the log shows\n\n"
    "`MissionScreen` drops the global loading window and only then calls each view's\n"
    "`OnSceneRenderingStarted`, once a 15-frame countdown has run after the scene became ready to render\n"
    "(v1.4.8 module dump `Native__TaleWorlds.MountAndBlade.View.cs:16474-16488` and `:13189-13197`). The\n"
    "mission ticks from the first render-ready frame, and behaviours get `OnMissionTick` from the fourth\n"
    "`TickMission` (taom-src `MissionState.cs:110-113` and `:215-218`), so `BattleLoadPhaseBehavior` has\n"
    "already logged `[BattleLoad] BattlePlayable`, closed the stall-watchdog window and cleared the\n"
    "in-flight marker about eleven frames before this wait starts. Before Patch90 a missing body froze\n"
    "the first frame after the loading screen with `BattlePlayable` as the last `[BattleLoad]` line, no\n"
    "watchdog line and no next-session notice. With Patch90 the frame holds for the 5 s budget and one\n"
    "`[PreloadGuard]` ERROR follows.\n\n"
    "Two stalls that are not this mechanism: a load that ends on `WaitingForRender` with no\n"
    "`BattlePlayable` (the 2026-11-01 Harad report) stops before this wait runs; and the `polls=1`,\n"
    "large `waitMs` reading on `FinishMissionLoadingBegin` is written when loading finishes, before this\n"
    "wait.\n")
d = once(d, callers_end, callers_end + where)
wrapped = ("- **Everything wrapped.** On any failure in the guard itself it logs once and returns `true`; a\n"
           "  guard that throws inside the load it protects is worse than the hang.\n")
loud = ("- **Loud, not quiet.** The 2026-07-16 RCA (`docs/reviews/rca-siege-load-hang-2026-07-16.md`)\n"
        "  rejected a timeout-and-drop prefix because it turns a findable hang into quiet missing collision.\n"
        "  This guard logs every drop at ERROR with the body names and the repair command, so the defect\n"
        "  still reports itself, and the desk gates catch it before a release.\n")
d = once(d, wrapped, wrapped + loud)
d = once(d, "- **Applied in `OnSubModuleLoad`** beside Patch89, own try/catch, because the education screen\n"
            "  and the custom-battle preload call the wait before any campaign initialises.\n",
         "- **Applied in `OnSubModuleLoad`** through `TryPatchCategory`, after the Patch61 save-load block and\n"
         "  before Patch62, own try/catch, because the education screen and the custom-battle preload call the\n"
         "  wait before any campaign initialises. Hand-wired in `IoC.cs` and `SubModule.cs`: the feature has no\n"
         "  module on the 1.5.x line either.\n")
d = once(d, "- 2026-09-15: created (#601), after #599's data repair and desk-side gates.\n",
         "- 2026-09-15: created (#601), after #599's data repair and desk-side gates.\n"
         "- " + TODAY + ": ported to the 1.4.x line (WP108, from `fb8d73145`); the field, the wait and\n"
         "  `PhysicsShape.GetFromResource(string, bool)` re-verified on the installed v1.4.8 with taom-src.\n")
save(p, d, nl)

# 2. RCA: a port note under the title; the 1.5.x record stays as written.
p = "docs/reviews/rca-preload-body-guard-2026-09-15.md"
c, nl = load(p)
c = once(c, "# RCA: Patch90_PreloadBodyGuard deep review (2026-09-15)\n",
         "# RCA: Patch90_PreloadBodyGuard deep review (2026-09-15)\n\n"
         "> Ported to the 1.4.x line with Patch90 (WP108, " + TODAY + "). This record describes the 1.5.x\n"
         "> review on v1.5.3; the engine members were re-verified on the installed v1.4.8 for the port.\n")
save(p, c, nl)

# 3. Registry section, before the generated backlinks footer.
p = "docs/reference/harmony-patch-registry.md"
r, nl = load(p)
if "## Patch90_PreloadBodyGuard" in r:
    stop("the registry already has a Patch90 section")
sec = cut(show("ef6afeb67:docs/reference/harmony-patch-registry.md"),
          "## Patch90_PreloadBodyGuard\n", "<!-- backlinks-start")
sec = once(sec, "Applied in `OnSubModuleLoad` beside Patch89 (the education screen and custom battle call the "
                "wait before any campaign initialises), own try/catch.",
           "Applied in `OnSubModuleLoad` through `TryPatchCategory`, after the Patch61 save-load block and before "
           "Patch62 (the education screen and custom battle call the wait before any campaign initialises), own "
           "try/catch. On this line `BattleLoadPhaseBehavior` logs `BattlePlayable` about eleven frames before the "
           "wait runs, so an unguarded hang here left `BattlePlayable` as the last `[BattleLoad]` line.")
sec = once(sec, "**ACTIVE** (2026-09-15).",
           "**ACTIVE** (1.5.x 2026-09-15; ported to 1.4.x by WP108 on " + TODAY
           + ", engine members re-verified on v1.4.8).")
r = once(r, "<!-- backlinks-start", sec + "<!-- backlinks-start")
save(p, r, nl)

# 4. The two Patch90 lessons from 1571c2f91, appended (orchestrator note K2).
p = "docs/reviews/lessons/harmony-il.md"
h, nl = load(p)
if "Cost a guard's failure path" in h:
    stop("the Patch90 lessons are already present")
lessons = cut(show("1571c2f91:docs/reviews/lessons/harmony-il.md"),
              "### Cost a guard's failure path as the loop it is")
save(p, h.rstrip("\n") + "\n\n" + lessons, nl)
print("OK: Part A docs adapted")
```

**Verify**: `cd "$WT" && git grep -c "Cost a guard's failure path" -- docs/reviews/lessons/harmony-il.md`
prints `docs/reviews/lessons/harmony-il.md:1`; `cd "$WT" && git grep -n "## Patch90_PreloadBodyGuard" -- docs/reference/harmony-patch-registry.md`
prints one line; `cd "$WT" && "$PY" tools/lint_docs.py` reports no new finding in the four touched
docs (in particular no dead link and no orphan `preload-body-guard.md`: the registry links it).

### Step A3: Commit Part A

1. `cd "$WT" && git status --porcelain` lists only the Part A in-scope paths (the 6 new code and
   test files, `CoopVetoClassificationTests.cs`, `Main/IoC.cs`, `Main/SubModule.cs`, the 2 new docs,
   the registry and the lessons file). Anything else: STOP.
2. Stage explicitly:
   `cd "$WT" && git add Main/Features/PreloadBodyGuard TAOM.Tests/Features/PreloadBodyGuard TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs Main/IoC.cs Main/SubModule.cs docs/features/preload-body-guard.md docs/reviews/rca-preload-body-guard-2026-09-15.md docs/reference/harmony-patch-registry.md docs/reviews/lessons/harmony-il.md`
   then the CHERRY_PICK_HEAD check from the Git workflow.
3. Save this message as `wp108-scripts/msg-A.txt`, then in the next Bash call
   `cd "$WT" && git commit -F ../wp108-scripts/msg-A.txt`:

```
feat(preload): v2.0.28 - Patch90 guards missing-body load hangs (#601)

On v1.4.8 PreloadHelper.WaitForMeshesToBeLoaded is a do/while with no
exit. Every pass counts each registered collision-body name that
PhysicsShape.GetFromResource(name, true) returns null for, and null
means both "not loaded yet" and "no such body". One body_name that no
loaded tpac ships freezes the game on the first frame after the
loading screen drops, with no error and no crash. Two Armory typos did
it to sieges on this line (#352).

Patch90 repairs the precondition instead of replacing the method. The
prefix receives the private name set by Harmony injection, polls only
the body names for a bounded 5 s through a pure service, removes what
still does not resolve, logs each drop at ERROR as [PreloadGuard] with
the repair command, and returns true so vanilla's loop runs against a
set that can now empty. A healthy load pays one pass of lookups; a
broken one waits 5 s once and loads with that item carrying no
collision shape.

BattleLoadPhaseBehavior logs BattlePlayable about eleven frames before
this wait runs, so until now such a hang left BattlePlayable as the
last [BattleLoad] line and no watchdog line. It is not the 2026-11-01
Harad freeze, which stalls before BattlePlayable.

Hand-wired in IoC.cs and in OnSubModuleLoad before Patch62, through
TryPatchCategory in its own try/catch (the feature has no module on
1.5.x either). The co-op veto registry lists the prefix as
ReviewedSafe: it never returns false. Docs: the feature doc, the
registry section, the 1.5.x review RCA and its two harmony-il lessons.

Research: taom-src v1.4.8 PreloadHelper (_uniqueDynamicPhysicsShapeName
  at :14, WaitForMeshesToBeLoaded at :58-75),
  PhysicsShape.GetFromResource(string, bool) at :10; the six callers in
  the v1.4.8 module dump
Not-tested: the in-game probe (a bogus body_name loads after about 5 s
  with one [PreloadGuard] line)
Save-compat: none, no campaign state touched
Ported-from: fb8d731451880c84969fa455e9d74873f9a65c6b
Ported-from: ef6afeb6729b50756c2b7352603c9870a91e5fda
Ported-from: b23a27ef45b3dd060c9729e63a26f65388e12a6f
Ported-from: 1571c2f915f40d79a02d12013ad647009aed6473
```

**Verify**: Build row exits 0; Test row: 0 failed, total = the count the previous merged WP
recorded in `plans/README.md` plus 8, skipped unchanged (3 at the WP101 baseline:
`EveryAllowedRaceIsARealRegisteredRace`, `WargAttack_FastWarg_InvokesRunningAttack`,
`WargAttack_SlowWarg_InvokesStandingAttack`). `cd "$WT" && git log -1 --format=%s` prints the
subject above.

### Part A done criteria

- [ ] One commit on `port148/108-missing-body-load-hang` with the subject above and four
      `Ported-from:` lines.
- [ ] Build exit 0; full suite 0 failed, +8 tests, skipped unchanged.
- [ ] `git grep -n -F 'TryPatchCategory("Patch90_PreloadBodyGuard")' -- Main/SubModule.cs` prints one
      line, and `git grep -n -F 'HarmonyPatchCategory("Patch90_PreloadBodyGuard")' -- Main` prints one
      line (the hook class).
- [ ] `git grep -n "RegisterPreloadBodyGuardFeature" -- Main/IoC.cs` shows one line.
- [ ] `git diff --name-only bannerlord-1.4.5..HEAD -- CHANGELOG.md CLAUDE.md` prints nothing.

## Part B: the desk gates

### Step B0: Armory state and baselines

1. `cd "$WT" && "$PY" ../wp108-scripts/wp108_edit.py armory-version`. Write down **PUBLIC** when
   the version is not `v2.0.26` (the channel copy reads `v2.0.29.5`), both monster files are
   `True` and `ladder_gondor_bow_t4 defined: True` (WP101 Step A2's markers; its integrator record
   in `plans/README.md` should say the same). Otherwise write **NOT PUBLIC**. NOT PUBLIC does not
   stop B1 to B4 (pure code); it skips Step B5 (D20).
2. Tool-test baseline: `cd "$WT" && "$PY" -m unittest discover -s tools/tests -t . > ../wp108-scripts/tooltests-before.txt 2>&1; tail -3 ../wp108-scripts/tooltests-before.txt`
   (record "Ran N tests" and the OK or FAILED line). The suite rewrites
   `tools/settlement_economy_floor.json` during one test (`.ai/verification.md`), so that one path can
   show as modified afterwards, here and at every later full run: restore it with
   `git checkout tools/settlement_economy_floor.json` and never stage it. It is not a drift finding.
3. Hook baseline: `cd "$WT" && bash tools/test_hooks.sh > ../wp108-scripts/hooks-before.txt 2>&1; echo "exit=$?"; tail -3 ../wp108-scripts/hooks-before.txt`.

### Step B1: `validate_mesh_refs.py` at its 1.5.x end state (`b23a27ef4`, `d5986f87f`, `30abf550b`, `719074072`, `7bbe6f4de`)

Method: end-state transplant. On 1.5.x the file and its test changed only in `b23a27ef4`
(CraftingPiece owns its refs), `d5986f87f` (allowlist test fixture; allowlist retirement),
`30abf550b` (loose tree first, `module_tpacs`), `719074072` (a comment) and `7bbe6f4de`
(`--check-name`) before `d6b537092` (WP138's). The 1.4.x file equals the fork version, so
`7bbe6f4de`'s file is the 1.4.x file plus exactly those hunks. Simulation: `7bbe6f4de` cherry-picks
with conflicts only in bookkeeping (`CLAUDE.md`, `tools/README.md`) and two 1.5.x-only docs; the
transplant avoids replaying the other four commits' unrelated payloads. Two adaptations: the
docstring stops naming the `/new-map-prop` skill and `tpac-static-prop-authoring.md` (absent on
1.4.x), and `KNOWN_DEAD_MESHES` keeps its three 1.4.x troll entries until Step B5 checks the
public Armory (orchestrator note: `d5986f87f`'s retirement only if the public Armory no longer
needs them). The fixture-based allowlist tests pass either way.

1. Transplant and apply the comment-only hunk:
   `cd "$WT" && git show 7bbe6f4de:tools/validate_mesh_refs.py > tools/validate_mesh_refs.py && git show 7bbe6f4de:tools/tests/test_validate_mesh_refs.py > tools/tests/test_validate_mesh_refs.py && git show b23a27ef4 -- tools/audit_deleted_mesh_impact.py | git apply`
2. Save and run `wp108-scripts/wp108_b1_meshrefs.py`:

```python
"""WP108 Step B1: adapt the transplanted validate_mesh_refs.py; 1.4.x docs for the tree order."""
import datetime
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from wp108_edit import cut, load, once, replace_line, save, show

TODAY = datetime.date.today().isoformat()

p = "tools/validate_mesh_refs.py"
v, nl = load(p)
v = once(v, 'packages are listed), 2 when a package could not be read and "free" cannot be claimed. The\n'
            '/new-map-prop skill runs it before a prop is built.\n',
         'packages are listed), 2 when a package could not be read and "free" cannot be claimed. Run it\n'
         'before naming a new mesh.\n')
v = once(v, "    prop may be delivered through either (docs/reference/tpac-static-prop-authoring.md). The\n"
            "    engine lowercases a requested name, so the match ignores case.\n",
         "    prop may be delivered through either. The engine lowercases a requested name, so the match\n"
         "    ignores case.\n")
# Keep the three 1.4.x troll entries; Step B5 retires them only if the public Armory proves it.
old_dict = cut(v, "KNOWN_DEAD_MESHES = {", "\n}\n") + "\n}\n"
keep_dict = cut(show("1b00d1acd:tools/validate_mesh_refs.py"), "KNOWN_DEAD_MESHES = {", "\n}\n") + "\n}\n"
v = once(v, old_dict, keep_dict)
save(p, v, nl)

p = "docs/features/mesh-ref-validation.md"
m, nl = load(p)
m = replace_line(m, "- **Corrected 2026-09-01: for a module with NO cooked tree", [
    "- **Corrected for the 1.4.x line by WP108 (from 1.5.x `30abf550b`): it resolves against a module's "
    "loose `Assets/**/*.tpac` whenever the module has one, and against cooked `AssetPackages/*.tpac` only for "
    "a module with no loose tree.** That is the engine's own order ([armory-guide.md](../reference/armory-guide.md) "
    "'Two asset trees'). Before this, a module with both trees was read from its cooked packs, so a clean run "
    "could sit on art already deleted from `Assets/` (on 2026-08-28 one sat on top of 179 deleted meshes, "
    "[armoury-mesh-cleanup.md](./armoury-mesh-cleanup.md)), and on the 1.5.x desktop stale packs left by a cook "
    "read a newly imported hammer as `MISSING_COLLISION_BODY` (2026-09-26). The public 1.4.8 release ships the "
    "Armory cooked only, so on such an install the tool reads the packs the game loads. `validate_moduledata.py` "
    "and `audit_armory_refs.py` take their package lists from the same function. A named module that contributes "
    "no `.tpac` at all gets one WARNING naming it. The 2026-09-01 fallback came first: "
    "[black-numenorean.md](./black-numenorean.md) noted that the glob had never seen an Armory body, and 744 "
    "collision-body refs were being checked against a directory that did not exist."])
m = replace_line(m, "- **Where a cooked tree DOES exist, it resolves against the COOKED packs", [
    "- **A `PASS` is clean against the tree the game loads.** To diff a module's two trees, use "
    "`tools/audit_deleted_mesh_impact.py`, which compares `AssetPackages/*.tpac` against `Assets/**/*.tpac` and "
    "joins the difference through to items and troops."])
m = once(m, "**Interpreting the result:**\n",
         "**Is a new mesh name free?** (WP108, from 1.5.x `7bbe6f4de`.) "
         "`python tools/validate_mesh_refs.py --game <install> --check-name <name>` (repeatable) skips the audit and "
         "reports whether any package in any module's loose `Assets/` or `AssetPackages/` already holds that name, "
         "case ignored: exit 0 free, 1 taken (the holding packages are listed), 2 when a package could not be read. "
         "Pass `--game`: the default install path is the desktop's.\n\n**Interpreting the result:**\n")
m = once(m, "## Changelog\n\n",
         "## Changelog\n\n- " + TODAY + " (WP108): ported from the 1.5.x line: each module's loose tree first "
         "(`module_tpacs`), a `<CraftingPiece>` owns its refs, and `--check-name`. See Known Scope above.\n")
save(p, m, nl)

p = "docs/reference/armory-guide.md"
g, nl = load(p)
g = once(g, "## Two asset trees, and why a clean validator run can lie\n\n",
         "## Two asset trees, and why a clean validator run can lie\n\n"
         "> **1.4.x, WP108: the public 1.4.8 release ships the Armory cooked, and the tools now follow the\n"
         "> engine's order everywhere.** A player's rgl log of 2026-11-01 loads\n"
         "> `$BASE/Modules/LOTRLOME_Armory/AssetPackages...`, and the laptop's install is cooked as well (no\n"
         "> `Assets/`). The engine reads a module's loose `Assets/` tree when it has one and its cooked packs\n"
         "> otherwise, so on those installs the packs are what the game loads. `validate_mesh_refs.py`\n"
         "> (`tpac_paths_for_modules`, `module_tpacs`) now applies the same order per module, and\n"
         "> `validate_moduledata.py` and `audit_armory_refs.py` take their package lists from it.\n"
         "> `generate_armory_catalogue.py` inventories the loose tree only, so `audit_armory_refs.py` skips\n"
         "> catalogue drift on a cooked-only install. The 2026-09-01 note below describes the desktop's loose\n"
         "> install.\n\n")
g = once(g, "| `AssetPackages/pack0-9.tpac` | the **cooked** packs the running game loads | `tools/validate_mesh_refs.py` |\n",
         "| `AssetPackages/*.tpac` | the **cooked** packs; the game loads them only for a module with no loose "
         "`Assets/` tree (the public 1.4.8 Armory) | `tools/validate_mesh_refs.py` for such a module; "
         "`tools/audit_deleted_mesh_impact.py` |\n")
g = once(g, "| `Assets/**/*.tpac` | the **authoring** tree, one tpac per asset | `tools/audit_deleted_mesh_impact.py` |\n",
         "| `Assets/**/*.tpac` | the **authoring** tree, one tpac per asset; the game loads it over a cooked tree "
         "when both exist | `tools/validate_mesh_refs.py`, `tools/generate_armory_catalogue.py`, "
         "`tools/audit_deleted_mesh_impact.py` |\n")
old_par = (cut(g, "Packs are rebuilt only on an explicit re-cook. **Art deleted from `Assets/` keeps shipping",
               "(../features/armoury-mesh-cleanup.md).\n") + "(../features/armoury-mesh-cleanup.md).\n")
g = once(g, old_par,
         "Packs are rebuilt only on an explicit re-cook, so on a machine with both trees they go stale, but\n"
         "the game does not read them while `Assets/` exists. Until WP108 `validate_mesh_refs.py` preferred a\n"
         "cooked tree wherever one existed, so it could `PASS` over art already deleted from `Assets/` and\n"
         "miss art imported after the last cook. It now reads `Assets/` first, so a `PASS` is against the\n"
         "tree the game loads. Full case from when the tool read the packs:\n"
         "[`docs/features/armoury-mesh-cleanup.md`](../features/armoury-mesh-cleanup.md).\n")
save(p, g, nl)
print("OK: B1 edits applied")
```

**Verify**: `cd "$WT" && "$PY" -m unittest tools.tests.test_validate_mesh_refs` prints `Ran 69 tests`
and `OK` (was 60). `cd "$WT" && "$PY" tools/validate_mesh_refs.py --game "$BANNERLORD_GAME_DIR" --check-name taom_wp108_free_name_probe; echo "exit=$?"`
prints `FREE  taom_wp108_free_name_probe` with exit 0, or exit 2 with an `UNVERIFIED` list of
packages it could not read (record them). Exit 1 (`TAKEN`) for this made-up name is a STOP. Then
commit (stage
`tools/validate_mesh_refs.py tools/tests/test_validate_mesh_refs.py tools/audit_deleted_mesh_impact.py docs/features/mesh-ref-validation.md docs/reference/armory-guide.md`,
message `wp108-scripts/msg-B1.txt`):

```
fix(tools): v2.0.28 - mesh refs: loose tree, piece owners, --check-name

Three changes to tools/validate_mesh_refs.py from the 1.5.x line,
taken as one end state:

- The package list follows the engine's order per module
  (module_tpacs): the loose Assets tree when the module has one, its
  cooked AssetPackages otherwise. The old order preferred a cooked
  tree wherever one existed, so a stale pack could pass art already
  deleted from Assets. The public 1.4.8 release ships the Armory
  cooked only, and there the engine and the tool both read the packs.
- A <CraftingPiece> now owns its mesh and BladeData body_name refs.
  The item regex never matched it, so every piece ref carried item id
  "", which the borrowed-body gate in this change needs (#633).
- --check-name NAME says whether a new mesh name is already held in
  any module's loose or cooked packages (exit 0 free, 1 taken, 2 when
  a package could not be read).

The allowlist tests now own a fixture instead of reading the shipped
KNOWN_DEAD_MESHES, whose three troll entries stay until the public
Armory is checked. Docs: mesh-ref-validation and the armory guide's
"Two asset trees".

Research: engine tree order from a player's 2026-11-01 rgl log, which
  loads $BASE/Modules/LOTRLOME_Armory/AssetPackages; no engine API bound
Ported-from: b23a27ef45b3dd060c9729e63a26f65388e12a6f
Ported-from: d5986f87f853e0a1d8f36b54a88add9e4212571c
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
Ported-from: 719074072ab5777bd614cda70afc330f8346f1c1
Ported-from: 7bbe6f4de154127b9c61e517b4ad5a74c2d4bcf0
```

### Step B2: Port `05bd0a730` (the Armory reference audit) with its `30abf550b` package list

`05bd0a730` "feat(tools): v2.0.29 - the Armory reference audit (#599)". Method: cherry-pick.
Simulated: exit 0, clean. Its four imports (`validate_mesh_refs`, `generate_armory_catalogue`,
`audit_deleted_mesh_impact`, `_gamedir`) plus `taom_schema` and the two generators it runs are
byte-identical between `05bd0a730` and 1.4.x (`git diff --stat` empty). Drop the 1.5.x report and
catalogue (they describe the 1.5.x Armory), apply `30abf550b`'s one-hunk change to the package list
(its parent version of the file is `05bd0a730`'s, so `git apply` fits), and replace the feature doc
with the 1.4.x text (from `332f2ac71`'s version).

1. `cd "$WT" && git cherry-pick -n 05bd0a730` (expected: no conflict).
2. `cd "$WT" && git rm -f docs/audits/armory-ref-audit.md && git checkout HEAD docs/reference/armory-catalogue/catalogue.tsv && git show 30abf550b -- tools/audit_armory_refs.py | git apply`
3. Save and run `wp108-scripts/wp108_b2_auditdoc.py`:

```python
"""WP108 Step B2: the 1.4.x text of docs/features/armory-ref-audit.md (from 332f2ac71's version)."""
import datetime
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from wp108_edit import once, save, show

TODAY = datetime.date.today().isoformat()
d = show("332f2ac71:docs/features/armory-ref-audit.md")
d = once(d, "Its report is\n`docs/audits/armory-ref-audit.md`; its skill is `/armory-audit`; its trigger is the\n"
            "`ARMORY ART DRIFT` line `session-start.sh` prints when a `*_geo.tpac` under the live Armory is\n"
            "newer than the committed catalogue.\n",
         "Its report is\n`docs/audits/armory-ref-audit.md`. The 1.4.x line has no skill and no startup trigger for it "
         "(the\n1.5.x `/armory-audit` skill and `ARMORY ART DRIFT` session-start line were not ported): run it after\n"
         "any Armory sync or install.\n")
d = once(d, "The Armory is unversioned in this repo and loads loose\n`Assets/**/*.tpac`, so a sync touches nothing "
            "the commit hook or CI can see, and the gates had\nto be remembered. This feature makes them one command, "
            "one report, one startup line, and one\nvalidator error.\n",
         "The Armory is unversioned in this repo, so a sync touches nothing the commit hook can see, and the\n"
         "gates had to be remembered. This feature makes them one command and one report, and the validator\n"
         "carries the hang class as an error.\n")
d = once(d, "  that every `*_issues` pass is reached from `main()`, and the MCP tool and `/verify` still do not\n"
            "  run it (#623).\n",
         "  that every `*_issues` pass is reached from `main()`, and the MCP tool and `/verify` still do not\n"
         "  run it (#623). `COLLISION_BODY_BORROWED` (ERROR, #633) followed: a body that is another kit's `bo_`\n"
         "  twin resolves, so `MISSING_COLLISION_BODY` passes it ([moduledata-validation.md](moduledata-validation.md)).\n")
lines = d.split("\n")
i = next(k for k, line in enumerate(lines) if line.startswith("- **A startup line that fires after a sync.**"))
j = next(k for k in range(i, len(lines)) if lines[k].endswith('prints "UNCHECKED".'))
lines[i:j + 1] = [
    "- **A cooked install skips the catalogue (1.4.x).** `generate_armory_catalogue.build_rows`",
    "  inventories the loose `Assets/` tree only, and the public 1.4.8 release ships the Armory cooked",
    "  (`AssetPackages/` only). On such an install the drift section says SKIPPED with the reason, the",
    "  verdict comes from the other checks, and `--regen-catalogue` is refused, so the committed",
    "  catalogue is never overwritten with an empty one (`has_loose_tree`, `Summary.catalogue_checked`).",
]
lines = [ln for ln in lines
         if ".claude/hooks/session-start.sh" not in ln and ".claude/skills/armory-audit/SKILL.md" not in ln]
d = "\n".join(lines)
old_prefix = 'session-start.sh  --"ARMORY ART DRIFT"-->  /armory-audit  -->  '
new_prefix = "after any Armory sync, by hand  ".ljust(len(old_prefix) - 5, "-") + "-->  "
d = once(d, old_prefix + "tools/audit_armory_refs.py\n", new_prefix + "tools/audit_armory_refs.py\n")
d = once(d, "| `tools/tests/test_audit_armory_refs.py` | seven synthetic tests, no install |",
         "| `tools/tests/test_audit_armory_refs.py` | ten synthetic tests, no install |")
d = once(d, "| `.claude/hooks/check-moduledata-validation.sh` | `--code MISSING_COLLISION_BODY` |\n",
         "| `.claude/hooks/check-moduledata-validation.sh` | `--code MISSING_COLLISION_BODY`, "
         "`--code COLLISION_BODY_BORROWED` |\n"
         "| `tools/validate_mesh_refs.py` | the engine: `extract_refs`, `tpac_paths_for_modules` / `module_tpacs` "
         "(loose tree first), `build_present_set`, `classify` |\n")
d = once(d, "| `tools/validate_moduledata.py` | `missing_collision_body_issues`, `BODY_CODE`, `MESH_CODE`, "
            "`_loaded_tpacs` |\n",
         "| `tools/validate_moduledata.py` | `missing_collision_body_issues`, `borrowed_body_issues`, `BODY_CODE`, "
         "`MESH_CODE`, `BORROWED_BODY_CODE`, `_loaded_tpacs` |\n")
d = once(d, "- `python -m unittest tools/tests/test_audit_armory_refs.py` (7):",
         "- `python -m unittest tools/tests/test_audit_armory_refs.py` (10):")
d = once(d, "troop on one row and is byte-stable; exit code semantics; generator drift warns.\n",
         "troop on one row and is byte-stable; exit code semantics; generator drift warns; a cooked install\n"
         "  skips the catalogue and refuses `--regen-catalogue`.\n")
d = once(d, "  tools.tests.test_validate_moduledata.CommitGateCoverageTests` (5).\n",
         "  tools.tests.test_validate_moduledata.CommitGateCoverageTests` (11), and\n"
         "  `python -m unittest tools.tests.test_collision_body_borrowed` (14).\n")
d = once(d, "- `bash tools/test_hooks.sh` (208) covers the hook's timeout and inner bound.\n",
         "- `bash tools/test_hooks.sh` covers the hook's timeout and inner bound.\n")
d = once(d, "python tools/audit_armory_refs.py --regen-catalogue\n",
         "python tools/audit_armory_refs.py --regen-catalogue   # loose tree only; refused on a cooked install\n")
d = once(d, "applied to both the live Armory and\nthe `lotraom-assets` mirror.\n",
         "applied to the live Armory and, at the time, the `lotraom-assets` mirror.\n")
d = once(d, "45 s inner bound. The startup probe is\none `find`, under a second, bounded at 4 s.\n",
         "45 s inner bound. These figures are from the 1.5.x desktop's loose tree; the WP108 commits record\n"
         "the laptop's times on its cooked install.\n")
d = once(d, "- 2026-09-15: created with #599.",
         "- " + TODAY + ": ported to the 1.4.x line (WP108) from `05bd0a730`, with the loose-tree-first package\n"
         "  list (`30abf550b`) and a cooked-install guard for the catalogue section (new on 1.4.x).\n"
         "- 2026-09-15: created with #599.")
d = once(d, "The runtime guard (a prefix on\n`PreloadHelper.WaitForMeshesToBeLoaded` that logs and drops "
            "unresolvable body names so a player's\nmission still loads) is not built; it is the only protection "
            "for an install that already has a\nbad pair.",
         "The runtime guard is Patch90\n([preload-body-guard.md](preload-body-guard.md)), the only protection for "
         "an install that already\nhas a bad pair.")
save("docs/features/armory-ref-audit.md", d, "\n")
print("OK: docs/features/armory-ref-audit.md written")
```

**Verify**: `cd "$WT" && "$PY" -m unittest tools.tests.test_audit_armory_refs` prints `Ran 7 tests`
and `OK`. `cd "$WT" && git status --porcelain` shows `tools/audit_armory_refs.py` and
`docs/features/armory-ref-audit.md` as `AM` (added by the pick, then edited),
`A  tools/tests/test_audit_armory_refs.py`, `M  docs/features/mesh-ref-validation.md`,
`M  docs/modding/items-weapons-and-crafting.md`, and no `docs/audits/armory-ref-audit.md` or
`catalogue.tsv` entry. Commit (stage those five paths,
message `msg-B2.txt`):

```
feat(tools): v2.0.28 - the Armory reference audit (#599)

tools/audit_armory_refs.py runs the Armory checks that each had to be
remembered separately as one command and writes a committed report,
so the next run's git diff is the change log: mesh and collision-body
refs over the whole Armory ModuleData plus this repo's, catalogue
drift flagged by what the live XML names, every broken item joined to
the troops carrying it, dead Item ids, and the two scaffolders'
--verify. Exit 1 on a missing body or mesh, a dead id or a referenced
delete; generator drift is a warning.

The 1.5.x line wrote it after an Armory art drop renamed the elven bow
bodies and every elf start hung on its first tournament (#599, the
#352 class). Its package list comes from
validate_mesh_refs.tpac_paths_for_modules, so it reads the tree the
engine loads. The committed report and catalogue are not copied: both
describe the 1.5.x Armory, and the report is regenerated against the
public 1.4.8 Armory later in this change. The feature doc describes
the whole change; the cooked-install guard and the validator passes
land in the next two commits.

Research: none bound; the tools it imports are byte-identical to the
  versions it was written against
Ported-from: 05bd0a7301663369bcccbc3249e34de7229d2b37
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
Ported-from: 332f2ac71414ea39d252ee5e3bea9886576f5071
```

### Step B3: New work, TDD: the audit skips catalogue drift on a cooked-only install

Why: `generate_armory_catalogue.build_rows` reads `<module>/Assets/**/*.tpac` only (1.4.x
:279-287). On the public build's cooked Armory (and the laptop) it returns no rows, so the
drift section marks every committed row deleted, the verdict is BROKEN by construction, and
`--regen-catalogue` would write an empty catalogue over the committed one. The 1.5.x line never
met this (its desktop has a loose tree).

1. RED. Save and run `wp108-scripts/wp108_b3_tests.py`, which adds three tests:

```python
"""WP108 Step B3 (RED): tests for the cooked-install guard in audit_armory_refs.py."""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from wp108_edit import load, once, save

p = "tools/tests/test_audit_armory_refs.py"
t, nl = load(p)
t = once(t, "import os\nimport sys\nimport unittest\nfrom pathlib import Path\n",
         "import io\nimport os\nimport sys\nimport tempfile\nimport unittest\n"
         "from contextlib import redirect_stderr, redirect_stdout\nfrom pathlib import Path\nfrom unittest import mock\n")
tests = '''class CookedInstallSkipsTheCatalogue(unittest.TestCase):
    """The catalogue inventories a module's loose Assets/ tree only
    (generate_armory_catalogue.build_rows). The public 1.4.8 release ships the Armory
    cooked, so there every committed row would read as deleted: the audit must say the
    drift was not checked, never call it breakage, and never regenerate the catalogue."""

    def test_has_loose_tree_needs_a_tpac_under_assets(self):
        with tempfile.TemporaryDirectory() as d:
            mod = Path(d) / "LOTRLOME_Armory"
            (mod / "AssetPackages").mkdir(parents=True)
            (mod / "AssetPackages" / "pack0.tpac").write_bytes(b"x")
            self.assertFalse(aa.has_loose_tree(mod))
            (mod / "Assets" / "weapons").mkdir(parents=True)
            self.assertFalse(aa.has_loose_tree(mod))          # an empty Assets/ is not a tree
            (mod / "Assets" / "weapons" / "bow_geo.tpac").write_bytes(b"x")
            self.assertTrue(aa.has_loose_tree(mod))

    def _skipped(self):
        return aa.Summary(catalogue={"rename": [], "move": [], "delete": [], "moved_same_name": [], "new": []},
                          issues=[], impact={}, dead_item_refs=[], generator_checks=[],
                          unreferenced_new=[], catalogue_checked=False)

    def test_report_says_the_catalogue_was_skipped_and_stays_clean(self):
        s = self._skipped()
        text = aa.render_report(s, when="2026-11-05", head="abc1234")
        self.assertIn("SKIPPED", text)
        self.assertIn("Assets/", text)
        self.assertNotIn("RENAME:", text)                     # no counts that read as "nothing changed"
        self.assertEqual(aa.exit_code(s), 0)

    def test_regen_catalogue_is_refused_without_a_loose_tree(self):
        s = self._skipped()
        out, err = io.StringIO(), io.StringIO()
        argv = ["audit_armory_refs.py", "--game", "", "--report", "-", "--regen-catalogue", "--no-generators"]
        with tempfile.TemporaryDirectory() as d:
            argv[2] = d
            with mock.patch.object(aa, "build_summary", return_value=s):
                with mock.patch.object(aa, "_git_head", return_value="abc1234"):
                    with mock.patch.object(aa.subprocess, "run") as run:
                        with mock.patch.object(sys, "argv", argv):
                            with redirect_stdout(out), redirect_stderr(err):
                                rc = aa.main()
        self.assertEqual(rc, 0)
        run.assert_not_called()
        self.assertIn("refused", err.getvalue())


'''
t = once(t, 'if __name__ == "__main__":', tests + 'if __name__ == "__main__":')
save(p, t, nl)
print("OK: three RED tests added")
```

   **Verify RED**: `cd "$WT" && "$PY" -m unittest tools.tests.test_audit_armory_refs` reports
   `Ran 10 tests` with 3 errors or failures (no `has_loose_tree`, no `catalogue_checked`). If the
   three pass already, STOP.
2. GREEN. Save and run `wp108-scripts/wp108_b3_impl.py`:

```python
"""WP108 Step B3 (GREEN): the cooked-install guard in tools/audit_armory_refs.py."""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from wp108_edit import cut, load, once, save

p = "tools/audit_armory_refs.py"
t, nl = load(p)
t = once(t, "def count_breaking(flagged: dict) -> int:\n",
         'def has_loose_tree(module_root: Path) -> bool:\n'
         '    """True when the module ships a loose Assets/ tree holding at least one .tpac.\n'
         '\n'
         '    generate_armory_catalogue.build_rows inventories that loose tree only. The public\n'
         '    1.4.8 release ships the Armory cooked (AssetPackages/ alone): on such an install the\n'
         '    drift comparison would read every committed catalogue row as deleted, and\n'
         '    --regen-catalogue would write an empty catalogue over the committed one."""\n'
         '    return any((Path(module_root) / "Assets").rglob("*.tpac"))\n'
         '\n'
         '\n'
         'def count_breaking(flagged: dict) -> int:\n')
t = once(t, "    body_count: int = 0\n",
         "    body_count: int = 0\n"
         "    catalogue_checked: bool = True  # False: no loose Assets/ tree, drift not checked\n")
t = once(t, '        verdict = f"CLEAN, {warning_count(s)} warning(s) (generator drift, see below)"\n',
         '        verdict = f"CLEAN, {warning_count(s)} warning(s) (generator drift, see below)"\n'
         '    if not s.catalogue_checked:\n'
         '        verdict += " (catalogue drift not checked: cooked install)"\n')
block = cut(t, "    c = s.catalogue\n", '    L.append("")\n    # 6: new art\n')
skipped = ('    if not s.catalogue_checked:\n'
           '        L.append("- SKIPPED: this install ships the module cooked (`AssetPackages/` only), and the "\n'
           '                 "catalogue inventories the loose `Assets/` tree only, so drift was not checked. "\n'
           '                 "Run the audit on a machine with the loose tree to check it.")\n'
           '    else:\n')
indented = "".join(("    " + ln) if ln.strip() else ln for ln in block.splitlines(True))
t = once(t, block, skipped + indented)
t = once(t, "    rows = gac.build_rows(module_root, live_names)\n",
         "    catalogue_checked = has_loose_tree(module_root)\n"
         "    rows = gac.build_rows(module_root, live_names) if catalogue_checked else []\n")
t = once(t, "    old = gac.parse_existing(gac.OUT_TSV)\n",
         "    old = gac.parse_existing(gac.OUT_TSV) if catalogue_checked else {}\n")
t = once(t, "                   body_count=len(present.physicsshapes))\n",
         "                   body_count=len(present.physicsshapes), catalogue_checked=catalogue_checked)\n")
t = once(t, "    if args.regen_catalogue:\n",
         '    if args.regen_catalogue and not summary.catalogue_checked:\n'
         '        print("WARNING: --regen-catalogue refused: this install has no loose Assets/ tree for the "\n'
         '              "module, and regenerating from it would write an empty catalogue over the committed one.",\n'
         '              file=sys.stderr)\n'
         '    elif args.regen_catalogue:\n')
save(p, t, nl)
print("OK: guard implemented")
```

**Verify GREEN**: `cd "$WT" && "$PY" -m unittest tools.tests.test_audit_armory_refs` prints
`Ran 10 tests` and `OK`. (Planning dry-ran these exact edits against `30abf550b`'s file in memory:
the 3 new and 7 ported tests passed.) Commit (stage `tools/audit_armory_refs.py tools/tests/test_audit_armory_refs.py`,
message `msg-B3.txt`; no `Ported-from:`, this is new work):

```
fix(tools): v2.0.28 - Armory audit skips catalogue drift when cooked

generate_armory_catalogue.build_rows inventories a module's loose
Assets tree only. The public 1.4.8 release, and this laptop, install
the Armory cooked (AssetPackages only), so there the drift section
read every committed catalogue row as deleted, the verdict was BROKEN
by construction, and --regen-catalogue would have written an empty
catalogue over the committed one.

The audit now checks for a loose tree first (has_loose_tree). Without
one it reports catalogue drift as SKIPPED with the reason, marks the
verdict "catalogue drift not checked", leaves the exit code to the
other checks and refuses --regen-catalogue. Three tests, written
first.

Not-tested: a run on a machine with both trees (the desktop)
```

### Step B4: The validator passes (`ef6afeb67`, `332f2ac71`, `b23a27ef4`, `30abf550b`, `719074072`)

Method: end-state transplant of the collision-body block, hand-wired into the 1.4.x `main()`.
Simulations: `ef6afeb67` conflicts in `troop_resource_costs.xml` and bookkeeping (its
SpecialResources half is already on 1.4.x via `c5b84fb41`); `332f2ac71` conflicts in
`validate_moduledata.py` and both test files because its context carries `fa8c06f82`'s
`schema_invalid_issues` (WP136); `b23a27ef4` conflicts in the Mumakil files (modify/delete) and
`validate_moduledata.py`. So the block `BODY_CODE` .. end of `borrowed_body_issues` is taken from
`719074072` (identical to the 1.5.x tip's: compared this session), `main()` gets the two calls and
the SKIPPED line without `schema_invalid_issues`, `re` is imported (it came with `fa8c06f82` on
1.5.x), and the tests are taken class by class at their `719074072` shape. The reachability test
needs at least three `*_issues` passes; 1.4.x has exactly three after this step
(`generator_item_ref_issues`, `missing_collision_body_issues`, `borrowed_body_issues`).
Planning ran these exact edits in memory against the 1.4.x files: 177 tests (163 in
`test_validate_moduledata`, 14 in `test_collision_body_borrowed`), 0 failures.

1. New files: `cd "$WT" && git show 719074072:tools/tests/test_collision_body_borrowed.py > tools/tests/test_collision_body_borrowed.py && git show b23a27ef4:tools/blender/add_collision_body.py > tools/blender/add_collision_body.py && git show b23a27ef4:tools/tests/test_add_collision_body.py > tools/tests/test_add_collision_body.py`
2. Save and run `wp108-scripts/wp108_b4_validator.py`:

```python
"""WP108 Step B4: collision-body passes, tests, hook codes, rule rows, docs."""
import datetime
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from wp108_edit import cut, load, once, replace_line, save, show, stop

TODAY = datetime.date.today().isoformat()
BS = chr(92)  # one backslash, built so no shell or copy step can double or drop it

# 1. tools/validate_moduledata.py
p = "tools/validate_moduledata.py"
v, nl = load(p)
if "missing_collision_body_issues" in v:
    stop("validate_moduledata.py already has the collision-body pass")
v = once(v, "import os\nimport sys\n", "import os\nimport re\nimport sys\n")
doc = cut(show("ef6afeb67:tools/validate_moduledata.py"), "  MISSING_COLLISION_BODY     an item", "\nUsage:")
doc += ("  COLLISION_BODY_BORROWED    a weapon or crafting piece whose body_name is another kit's\n"
        "                             bo_<mesh> twin: it resolves, on art the item does not own\n"
        "                             (#633; needs the install; reuses validate_mesh_refs.py)\n")
v = once(v, "                             check_generator_item_refs.py)\n\nUsage:",
         "                             check_generator_item_refs.py)\n" + doc + "\nUsage:")
block = cut(show("719074072:tools/validate_moduledata.py"),
            '\nBODY_CODE = "MISSING_COLLISION_BODY"', "\n\n\nTEMPLATE_CODE = ").strip("\n")
v = once(v, "    return issues\n\n\ndef main() -> int:", "    return issues\n\n\n" + block + "\n\n\ndef main() -> int:")
v = once(v, "        issues += generator_item_ref_issues(registries.items)\n",
         "        issues += generator_item_ref_issues(registries.items)\n"
         "        # Until #622 nothing called this pass, so MISSING_COLLISION_BODY never fired and the\n"
         "        # commit hook's --code line for it blocked nothing.\n"
         "        issues += missing_collision_body_issues(game_modules, moduledata)\n"
         "        issues += borrowed_body_issues(game_modules, moduledata)\n")
v = once(v, '              f"         checked against the live install.", file=sys.stderr)\n',
         '              f"         checked against the live install.", file=sys.stderr)\n'
         '        print(f"WARNING: {BODY_CODE} SKIPPED: collision bodies can only be checked against the'
         + BS + 'n"\n'
         '              f"         live install' + "'" + 's tpacs.", file=sys.stderr)\n')
save(p, v, nl)

# 2. tools/taom_schema.py: missing roots as Paths (main() calls .parent on them)
p = "tools/taom_schema.py"
s, nl = load(p)
s = once(s, "self.missing_ref_roots = [str(r) for r in requested if not r.exists()]",
         "self.missing_ref_roots = [r for r in requested if not r.exists()]")
save(p, s, nl)

# 3. tools/tests/test_validate_moduledata.py, class by class at the 719074072 shape
p = "tools/tests/test_validate_moduledata.py"
t, nl = load(p)
src = show("719074072:tools/tests/test_validate_moduledata.py")
t = once(t, "import json\nimport os\nimport re\nimport sys\nimport tempfile\nimport unittest\nfrom pathlib import Path\n",
         "import io\nimport json\nimport os\nimport re\nimport sys\nimport tempfile\nimport unittest\n"
         "from contextlib import redirect_stderr, redirect_stdout\nfrom pathlib import Path\nfrom unittest import mock\n")
run_main = cut(src, "def _run_main(*args) -> tuple:", "\n\n\nclass ValidatorContractTests")
t = once(t, "\n\n\nclass ValidatorContractTests", "\n\n\n" + run_main + "\n\n\nclass ValidatorContractTests")
t = once(t, "        self.assertEqual([Path(p) for p in v.missing_ref_roots], [gone])\n",
         "        self.assertEqual(v.missing_ref_roots, [gone])   # Paths, like extra_ref_roots beside it\n")
warns = cut(src, "    def test_main_warns_about_a_missing_root_instead_of_crashing(self):",
            "\n\n\nclass ArmoryStructuralAssumptionTests")
t = once(t, "\n\n\nclass ArmoryStructuralAssumptionTests", "\n\n" + warns + "\n\n\nclass ArmoryStructuralAssumptionTests")
mcb = cut(src, "class MissingCollisionBodyTests(unittest.TestCase):", "\n\n\n_FACTIONS_XSD = ")
gate = cut(src, "class CommitGateCoverageTests(unittest.TestCase):")
t = once(t, cut(t, "class CommitGateCoverageTests(unittest.TestCase):"), mcb + "\n\n\n" + gate)
save(p, t, nl)

# 4. The commit hook: both new ERROR codes (CommitGateCoverageTests requires them) and a cost note
p = ".claude/hooks/check-moduledata-validation.sh"
k, nl = load(p)
k = once(k, "        --code MOUNT_WITHOUT_HARNESS " + BS + "\n",
         "        --code MOUNT_WITHOUT_HARNESS --code MISSING_COLLISION_BODY " + BS + "\n"
         "        --code COLLISION_BODY_BORROWED " + BS + "\n")
timing = "# The validator itself now runs in ~4s, not 27s:"
if k.count(timing) == 1:
    k = k.replace(timing,
                  "# WP108 adds MISSING_COLLISION_BODY and COLLISION_BODY_BORROWED: each runs one\n"
                  "# validate_mesh_refs.extract_refs pass over the Armory XML (about 3.5 s each on the 1.5.x\n"
                  "# desktop, 2026-09-21). The laptop's measured total is in the WP108 validator commit.\n"
                  "#\n" + timing)
else:
    print("NOTE: the hook's timing comment changed; the cost note was not added (not a stop)")
save(p, k, nl)

# 5. The rule rows, before FORTIFICATION_WITHOUT_VILLAGE
p = ".claude/rules/moduledata-validation.md"
r, nl = load(p)
rows = [ln for ln in show("b23a27ef4:.claude/rules/moduledata-validation.md").split("\n")
        if ln.startswith("| `MISSING_COLLISION_BODY` (error)") or ln.startswith("| `MISSING_VISUAL_MESH` (warn)")
        or ln.startswith("| `COLLISION_BODY_BORROWED` (error)")]
if len(rows) != 3:
    stop("expected 3 rule rows in b23a27ef4, found %d" % len(rows))
fort = [ln for ln in r.split("\n") if ln.startswith("| `FORTIFICATION_WITHOUT_VILLAGE` (warn) |")]
r = replace_line(r, "| `FORTIFICATION_WITHOUT_VILLAGE` (warn) |", rows + fort)
save(p, r, nl)

# 6. docs/features/moduledata-validation.md
p = "docs/features/moduledata-validation.md"
m, nl = load(p)
missing = (
    "## Missing collision body (`MISSING_COLLISION_BODY`, `MISSING_VISUAL_MESH`)\n\n"
    "**ERROR** for a body, **WARNING** for a visual mesh: an item or crafting piece whose `body_name`,\n"
    "holster body or collision body names a `PhysicsShape` that no loaded tpac ships. On v1.4.8\n"
    "`PreloadHelper.WaitForMeshesToBeLoaded` counts every registered body name that\n"
    "`PhysicsShape.GetFromResource(name, true)` returns null for, on every pass of a do/while with no\n"
    "exit (`TaleWorlds.MountAndBlade.View.PreloadHelper.cs:58-75` in the taom-src cache), so one such\n"
    "name on any item a mission preloads freezes the main thread on the first frame after the loading\n"
    "screen drops (#352 on this line, #599 on the 1.5.x line). Patch90\n"
    "([preload-body-guard.md](preload-body-guard.md)) is the runtime guard; this is the desk gate. A\n"
    "missing visual mesh cannot hang: `MetaMesh.GetCopy(name, false, true)` returns null and the preload\n"
    "drops it (`PreloadHelper.cs:211-228`), so the item only renders invisible.\n\n"
    "`validate_mesh_refs.py` owns the ref extraction and the tpac TOC scan (Tier B meshes, Tier C\n"
    "PhysicsShapes); this pass turns its `MISSING_BODY` into an error and its `MISSING_MESH` into a\n"
    "warning, over the Armory's whole `ModuleData` plus this repo's, and leaves its `KNOWN_DEAD_MESH`\n"
    "allowlist in that tool. Only a pack whose TOC failed to parse is byte-scanned for bodies: a parsed\n"
    "TOC already lists them, and scanning every pack took 110 to 119 s on the 1.5.x desktop, past the\n"
    "commit hook's 45 s bound (#622). Skipped, never faked, without the install; a run that finds no\n"
    "packs, or a scan that raises, is one NOT-verified ERROR. `main()` calls it, and\n"
    "`CommitGateCoverageTests.test_every_issue_pass_is_reached_from_main` pins that (on the 1.5.x line\n"
    "the pass sat uncalled for three days, #622). Repair the ref to the art that ships, never restore a\n"
    "tpac; `python tools/audit_armory_refs.py` names the troops carrying the item\n"
    "([armory-ref-audit.md](armory-ref-audit.md)).\n\n")
borrowed = cut(show("b23a27ef4:docs/features/moduledata-validation.md"),
               "## Borrowed collision body (`COLLISION_BODY_BORROWED`)", "## Key Files").rstrip("\n") + "\n"
packs = (
    "**Which packs count as shipped.** This gate, `MISSING_COLLISION_BODY` and `MISSING_VISUAL_MESH`\n"
    "take their package list from `validate_mesh_refs.module_tpacs`, as `audit_armory_refs.py` does\n"
    "through `tpac_paths_for_modules`: per module, the loose `Assets/**/*.tpac` when it has any, and the\n"
    "cooked `AssetPackages/*.tpac` only for a module with no loose tree. That is the engine's order\n"
    "([armory-guide.md](../reference/armory-guide.md), 'Two asset trees'). The public 1.4.8 release ships\n"
    "the Armory cooked only, so there the gates read the packs the game loads. On the 1.5.x desktop the\n"
    "old cooked-first order read stale packs left by a cook and reported a newly imported hammer as\n"
    "`MISSING_COLLISION_BODY` (2026-09-26). Test: `test_loose_assets_win_when_both_trees_exist` in\n"
    "`tools/tests/test_validate_mesh_refs.py`.\n\n")
m = once(m, "## Key Files\n", missing + borrowed + "\n" + packs + "## Key Files\n")
m = once(m, "| `tools/tests/test_taom_query.py` | unittest cases (query API) |\n",
         "| `tools/tests/test_taom_query.py` | unittest cases (query API) |\n"
         "| `tools/validate_mesh_refs.py` | engine of the three collision-body and mesh codes: `extract_refs`, "
         "`module_tpacs`, `build_present_set`, `classify` |\n"
         "| `tools/tests/test_collision_body_borrowed.py` | 14 synthetic cases for `COLLISION_BODY_BORROWED`: the "
         "#633 shape, own twin, own capsule, variant name, same-kit share, the Rhun family, vanilla, shields, "
         "missing body, per-piece pairing, no packs, a raising scan, `_kit`, `_twin_owner` |\n"
         "| `tools/tests/test_add_collision_body.py` | 9 cases for the twin-authoring tool's pure parts (`bpy` "
         "stubbed): the three required arguments, the report path under bad arguments, the round-trip comparison |\n")
m = once(m, "## Changelog\n\n",
         "## Changelog\n\n- " + TODAY + ": `MISSING_COLLISION_BODY` (error), `MISSING_VISUAL_MESH` (warn) and\n"
         "  `COLLISION_BODY_BORROWED` (error) ported from the 1.5.x line (#599, #622, #633; WP108), wired into\n"
         "  `main()` and the commit hook's `--code` list, with the loose-tree-first package list. A Modules\n"
         "  folder missing one of the live modules no longer crashes `main()` (`Validator.missing_ref_roots`\n"
         "  now holds Paths).\n")
save(p, m, nl)

# 7. mesh-ref-validation.md: the commit gate now exists
p = "docs/features/mesh-ref-validation.md"
x, nl = load(p)
x = replace_line(x, "- **No MCP tool / no pre-commit hook (yet).**", [
    "- **No MCP tool of its own.** Since WP108 its Tier B and Tier C engine feeds two commit-gated codes in "
    "`validate_moduledata.py` (`MISSING_COLLISION_BODY`, `COLLISION_BODY_BORROWED`), so a ModuleData commit is "
    "checked against it; a run of this tool itself is still on demand."])
save(p, x, nl)

# 8. weapon-creation-workflow.md and ranged-ladders.md: the #633 guidance from b23a27ef4
p = "docs/ai-includes/weapon-creation-workflow.md"
w, nl = load(p)
sec = cut(show("b23a27ef4:docs/ai-includes/weapon-creation-workflow.md"),
          "- **A borrowed `bo_` is a placeholder with a deadline.**", "\n## Step E")
w = once(w, "  that piece won't collide correctly in-game.\n\n## Step E",
         "  that piece won't collide correctly in-game.\n" + sec.rstrip("\n") + "\n\n## Step E")
save(p, w, nl)
p = "docs/features/ranged-ladders.md"
g, nl = load(p)
par = cut(show("b23a27ef4:docs/features/ranged-ladders.md"),
          "**A clone inherits the donor's art too, so fix the donor and regenerate.**", "Names are `{=<id>}")
g = once(g, "Names are `{=<id>}", par + "Names are `{=<id>}")
save(p, g, nl)

# 9. The #633 RCA with a port note
c = show("b23a27ef4:docs/reviews/rca-rhun-longbow-collision-body-2026-09-21.md")
first = c.index("\n") + 1
c = (c[:first] + "\n> Ported to the 1.4.x line with `COLLISION_BODY_BORROWED` (WP108). This is the 1.5.x record;\n"
     "> its version history (v2.0.28 tags, the September Nexus build) describes the 1.5.x line.\n" + c[first:])
save("docs/reviews/rca-rhun-longbow-collision-body-2026-09-21.md", c, "\n")
print("OK: B4 edits applied")
```

3. **Verify**:
   - `cd "$WT" && "$PY" -m unittest tools.tests.test_validate_moduledata tools.tests.test_collision_body_borrowed tools.tests.test_add_collision_body`
     prints `Ran 186 tests` (163 + 14 + 9) and `OK`.
   - Tool tests (full): no new failure against `tooltests-before.txt`; "Ran" grows by 51 over the
     B0 count (9 + 10 + 9 + 14 + 9 new tests across Part B).
   - Hooks: `cd "$WT" && bash tools/test_hooks.sh` shows no new failure against `hooks-before.txt`.
   - Measure (record both numbers for the commit body):
     `cd "$WT" && "$PY" tools/validate_moduledata.py --code MISSING_COLLISION_BODY --code COLLISION_BODY_BORROWED --code MISSING_VISUAL_MESH; echo "exit=$?"`
     and the warm wall time of a second full run:
     `cd "$WT" && time "$PY" tools/validate_moduledata.py > ../wp108-scripts/validator-after.txt 2>&1; echo "exit=$?"`.
     On the PUBLIC Armory the first command is expected to exit 0 with no ERROR. Any
     `MISSING_COLLISION_BODY` or `COLLISION_BODY_BORROWED` ERROR there is a STOP (see STOP
     conditions); a `MISSING_VISUAL_MESH` warning is recorded, not a stop. On a **NOT PUBLIC** Armory
     the counts are void (D20): errors there are not a STOP (the `v2.0.26` laptop Armory measured 3
     `COLLISION_BODY_BORROWED` and 0 `MISSING_COLLISION_BODY` in review), so commit B4 and write
     "void under D20, not the public Armory" after the counts in the commit body.
4. Commit (stage `tools/validate_moduledata.py tools/taom_schema.py tools/tests/test_validate_moduledata.py tools/tests/test_collision_body_borrowed.py tools/blender/add_collision_body.py tools/tests/test_add_collision_body.py .claude/hooks/check-moduledata-validation.sh .claude/rules/moduledata-validation.md docs/features/moduledata-validation.md docs/features/mesh-ref-validation.md docs/ai-includes/weapon-creation-workflow.md docs/features/ranged-ladders.md docs/reviews/rca-rhun-longbow-collision-body-2026-09-21.md`,
   message `msg-B4.txt`, filling the measured line):

```
fix(tools): v2.0.28 - collision-body gates run in the validator (#622)

validate_moduledata.py gains three passes from the 1.5.x line, built
on validate_mesh_refs.py and all called from main():

- MISSING_COLLISION_BODY (error): a body_name, holster or collision
  body that no loaded tpac ships, the hang Patch90 guards at run time.
  Only packs whose table of contents failed to parse are byte-scanned;
  a run with no packs, or a scan that raises, is one NOT-verified
  error, and without the install the pass says SKIPPED.
- MISSING_VISUAL_MESH (warning): the same for mesh and holster_mesh;
  the item renders invisible.
- COLLISION_BODY_BORROWED (error): a weapon or piece whose body is
  another kit's bo_ twin. It resolves, so the first pass lets it
  through, but it ties the item to art it does not own, which an art
  drop can rename (#633). tools/blender/add_collision_body.py authors
  the item's own twin.

The commit hook's --code list names both errors, which
CommitGateCoverageTests requires, and a new test fails if any
*_issues pass is not called from main() (#622: on 1.5.x the first
pass sat uncalled for three days). A Modules folder missing a live
module no longer crashes main(): Validator.missing_ref_roots holds
Paths. Docs: moduledata-validation, the rule rows, the weapon
workflow, ranged ladders and the #633 RCA.

Measured on LOTRLOME_Armory <version>: MISSING_COLLISION_BODY <n>,
COLLISION_BODY_BORROWED <n>, MISSING_VISUAL_MESH <n>; the whole
validator takes <s> s warm on this laptop.

Not-tested: a commit-time run through the real hook
Ported-from: ef6afeb6729b50756c2b7352603c9870a91e5fda
Ported-from: 332f2ac71414ea39d252ee5e3bea9886576f5071
Ported-from: b23a27ef45b3dd060c9729e63a26f65388e12a6f
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
Ported-from: 719074072ab5777bd614cda70afc330f8346f1c1
```

### Step B5: Data-dependent: the report and the allowlist against the public Armory (D20)

Run only if Step B0 recorded **PUBLIC**. Otherwise skip to Step C1 and record "B5 deferred (D20:
laptop Armory is not the public build)". Body and mesh verdicts from the old `v2.0.26` laptop
Armory are void (orchestrator note).

1. The audit (writes `docs/audits/armory-ref-audit.md`):
   `cd "$WT" && "$PY" tools/audit_armory_refs.py; echo "exit=$?"`.
   Expected on the cooked install: the report's catalogue section says `SKIPPED`, its
   "Broken mesh and body refs" table has **no `MISSING_BODY` row**, and the last line starts
   `Verdict:`. A `MISSING_BODY` row is a STOP. `MISSING_MESH` rows, dead item ids and generator
   drift are recorded in the commit body, not stops (dead ids are the same class as WP101's
   `BROKEN_ITEM_REF` baseline: say whether the count matches).
2. The allowlist: `cd "$WT" && "$PY" tools/validate_mesh_refs.py --game "$BANNERLORD_GAME_DIR" --items "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/ModuleData" --tpac-modules LOTRLOME_Armory Native SandBoxCore SandBox --no-rgl-log --code STALE_DEAD_MESH_ALLOWLIST --json ../wp108-scripts/stale.json > ../wp108-scripts/stale-out.txt; "$PY" -c "import json; print(sorted({i['entry_id'] for i in json.load(open('../wp108-scripts/stale.json'))}))"`.
   If it prints `['lotr_troll_armor', 'lotr_troll_bracers', 'lotr_troll_helmet']`, save and run
   `wp108-scripts/wp108_b5_allowlist.py <armory version>`; for any other output keep the entries.
   Then check that the cooked packs really parsed (the audit's CLEAN rests on it):
   `grep "pack(s) unparsed" ../wp108-scripts/stale-out.txt` is expected to print `0 pack(s) unparsed`
   (it did on the cooked `v2.0.26` laptop Armory, 160 tpacs). A non-zero count is recorded in the commit
   body and the hand-off, and the mesh verdict is reported as not established for those packs.

```python
"""WP108 Step B5: retire the three troll allowlist entries (only when the stale check named all three)."""
import datetime
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from wp108_edit import cut, load, once, save, stop

if len(sys.argv) != 2:
    stop("usage: wp108_b5_allowlist.py <LOTRLOME_Armory version>")
TODAY = datetime.date.today().isoformat()
p = "tools/validate_mesh_refs.py"
v, nl = load(p)
old = cut(v, "KNOWN_DEAD_MESHES = {", "\n}\n") + "\n}\n"
if '"lotr_troll_armor"' not in old:
    stop("KNOWN_DEAD_MESHES no longer holds lotr_troll_armor")
new = ("KNOWN_DEAD_MESHES = {\n"
       "    # Empty by design. An entry here downgrades a MISSING_MESH error to a warning, so it\n"
       "    # is an accepted loss of art, never a mute: add one only with a date and a reason,\n"
       "    # and retire it the moment the art returns (STALE_DEAD_MESH_ALLOWLIST says so).\n"
       "    #\n"
       "    # Retired " + TODAY + " (WP108): lotr_troll_armor / lotr_troll_bracers / lotr_troll_helmet.\n"
       "    # The art was deleted 2026-09-01 and the items were kept so the cave troll kept its\n"
       "    # armour. The stale check against the public 1.4.8 Armory (" + sys.argv[1] + ") reported\n"
       "    # all three entries stale, so with them gone, losing that art again errors.\n"
       "}\n")
save(p, once(v, old, new), nl)
print("OK: troll entries retired")
```

3. The audits index row (only when the report exists). Save and run `wp108-scripts/wp108_b5_readme.py`:

```python
"""WP108 Step B5: index the generated report in docs/audits/README.md."""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from wp108_edit import load, once, save

p = "docs/audits/README.md"
t, nl = load(p)
t = once(t, "## Other audits filed here\n\n",
         "## Other audits filed here\n\n"
         "- [armory-ref-audit.md](armory-ref-audit.md): GENERATED by `tools/audit_armory_refs.py`; re-run after any "
         "Armory sync or install. The committed state of every mesh and collision-body ref, catalogue drift (skipped "
         "on a cooked install), dead item ids and generator drift; its `git diff` is the change log. Feature doc: "
         "[armory-ref-audit.md](../features/armory-ref-audit.md) (#599).\n\n")
save(p, t, nl)
print("OK: audits README row added")
```

**Verify**: `cd "$WT" && "$PY" -m unittest tools.tests.test_validate_mesh_refs` still `Ran 69 tests`,
`OK`; `cd "$WT" && "$PY" tools/lint_docs.py` reports no dead link in `docs/audits/`. Commit (stage
`docs/audits/armory-ref-audit.md docs/audits/README.md` and, if retired, `tools/validate_mesh_refs.py`;
message `msg-B5.txt`, keeping the `d5986f87f` trailer only if the entries were retired):

```
chore(armoury): v2.0.28 - audit the public 1.4.8 Armory

The first committed run of tools/audit_armory_refs.py on this line,
against LOTRLOME_Armory <version> as the public 1.4.8 build ships it
(installed cooked, so catalogue drift is skipped). Verdict: <verdict
line>. Missing bodies: 0. Missing meshes: <n>. Dead item ids: <n>
(<matches / differs from> the WP101 BROKEN_ITEM_REF baseline of <n>).
Generators: <summary of the two --verify lines>.

<One of: "The stale check named all three troll entries, so
KNOWN_DEAD_MESHES is empty again and losing that art errors." or
"KNOWN_DEAD_MESHES keeps its three troll entries: the stale check did
not name all three.">

Ported-from: d5986f87f853e0a1d8f36b54a88add9e4212571c
```

### Step C1: Final verification, hand-off, cleanup

1. Full Build and Test rows once more on the final tree (0 failed; total = previous WP's
   recorded count + 8; skipped unchanged), the full tool suite (no new failure; +51), and
   `"$PY" tools/lint_docs.py` (no new finding in touched files).
2. Report in your hand-off (do not edit `plans/README.md`; the integrator records these after the
   merge): status (DONE, or BLOCKED with a one-line reason), the commit hashes, and the numbers in the
   format WP101 used: C# total/passed/skipped, tool tests ran/failures, the Armory version, the three
   body-pass counts, the warm validator time, the B5 verdict and the allowlist decision. No old-plan row
   is affected.
3. `rm -rf /c/Users/mikew/source/repos/taom-port/wp108-scripts` once the integrator has merged.

### Part B done criteria

- [ ] Commits B1, B2, B3, B4 (and B5 when PUBLIC) on the branch with the subjects above; B1, B2,
      B4 and B5 carry their `Ported-from:` lines; B3 carries none.
- [ ] `"$PY" -m unittest tools.tests.test_validate_mesh_refs tools.tests.test_audit_armory_refs tools.tests.test_validate_moduledata tools.tests.test_collision_body_borrowed tools.tests.test_add_collision_body`
      prints `Ran 265 tests` (69 + 10 + 163 + 14 + 9) and `OK`.
- [ ] `git grep -c -e "--code MISSING_COLLISION_BODY" -e "--code COLLISION_BODY_BORROWED" -- .claude/hooks/check-moduledata-validation.sh`
      prints `.claude/hooks/check-moduledata-validation.sh:2`.
- [ ] `git grep -n -e "def has_loose_tree" -e "catalogue_checked: bool" -- tools/audit_armory_refs.py`
      prints 2 lines.
- [ ] `"$PY" tools/validate_moduledata.py --code MISSING_COLLISION_BODY --code COLLISION_BODY_BORROWED`
      exits 0 on the PUBLIC Armory (or B5 is recorded as deferred under D20).
- [ ] `git diff --name-only bannerlord-1.4.5..HEAD` lists only in-scope paths; no `CHANGELOG.md`,
      `tools/README.md`, `CLAUDE.md`, `catalogue.tsv` or `.claude/skills/` path.

## Disposition table

| 1.5.x commit | Method | Taken (step) | Left, with the reason |
|---|---|---|---|
| `fb8d73145` feat(preload): v2.0.29 - Patch90 guards missing-body load hangs (#601) | cherry-pick -n; registration placed by hand | 4 feature files, 2 tests, CoopVeto row (the 1.4.x test requires one per bool prefix), feature doc (adapted), RCA with a port note (A1, A2) | nothing dropped; Patch89 neighbour absent on 1.4.x, so the block sits before Patch62 and uses `TryPatchCategory` as on the 1.5.x tip |
| `ef6afeb67` Refactor validation and auditing tools; enhance troop cost definitions | hand port, end state | the `MISSING_COLLISION_BODY`/`MISSING_VISUAL_MESH` pass and docstring lines, its tests (as the `719074072` classes), the hook code, the rule rows, the Patch90 registry section (A2, B4) | SpecialResources half, `troop_resource_costs.xml`, `wire_black_numenorean_troops.py`, both co-op rows, the `.ai` entries: already on 1.4.x via `c5b84fb41`; `patch-targets.md` hunk (it is the SpecialResources `RecruitmentVM_ExecuteDone_Patch` row, already there); `/armory-audit` skill, `session-start.sh` drift block, CLAUDE.md rows, INDEX, doc-lookup, feature-map, hooks-catalog, tools/README, REVIEW-LOG, lessons, Gondor RCA and prompt: bookkeeping or harness (per-port procedure step 2; WP142/WP143). Of these, the CLAUDE.md Armory trap row, the `/armory-audit` skill and the `session-start.sh` drift line go to WP143, which takes each or records a skip with the reason |
| `05bd0a730` feat(tools): v2.0.29 - the Armory reference audit (#599) | cherry-pick -n (clean) | tool, 7 tests, mesh-ref-validation paragraph, items-weapons example; feature doc replaced by the 1.4.x text (B2) | `docs/audits/armory-ref-audit.md` and the `catalogue.tsv` regen: they describe the 1.5.x Armory (report regenerated in B5; the catalogue only on a loose-tree machine) |
| `332f2ac71` fix(tools): v2.0.29 - collision-body gate runs, TAOM_Map pinned (#622) | hand port, end state | `main()` wiring and SKIPPED line, unparsed-only byte scan, NOT-verified error, `taom_schema` Path fix, its 4 tests, the ExtraRef main test, the reachability test, `_run_main`, the doc corrections (B2, B4) | `test_validate_xml_schemas.py` TAOM_Map pin and the `SchemaInvalidPassTests` refactor: need `fa8c06f82`'s tool (WP136); hook timing comment (1.5.x numbers; a 1.4.x note instead); armory-audit and deep-review SKILL lines; LESSONS-LEARNED, testing-qa lesson, deep-review RCA, CHANGELOG, tools/README (bookkeeping) |
| `b23a27ef4` Add unit tests for collision body handling in Blender tools | hand port, end state, plus `git apply` of one comment | CraftingPiece regex and test (B1), `COLLISION_BODY_BORROWED` pass and tests, `add_collision_body.py` and tests, hook code, rule row, moduledata-validation section, weapon workflow section, ranged-ladders paragraph, the #633 RCA (B4), the preload-body-guard #633 paragraph (A2), `audit_deleted_mesh_impact.py` comment (B1) | Mumakil platform and howdah seat, `LegacyHowdahPrefabTests`, the snapshot prefab: features absent or different on 1.4.x; `troop-rosters.html` and `generate_troop_roster_page.py`: generated from 1.5.x data; catalogue rows and the report: 1.5.x Armory; CHANGELOG, CLAUDE.md, INDEX, REVIEW-LOG, LESSONS-LEARNED, xslt-moduledata lesson (bookkeeping, WP142) |
| `d5986f87f` chore(armoury): v2.0.30 - audit XML references and clean up dead mesh entries | end state (fixture); conditional (allowlist) | the allowlist test fixture, unconditionally: the tests pass either way (B1); the retirement only if the public Armory's stale check names all three (B5) | `docs/reference/armory-catalogue/README.md` counts (the desktop tree); CHANGELOG |
| `7bbe6f4de` feat(tooling): v2.0.32 - /new-map-prop and a mesh name check | end state | `--check-name` (`find_mesh_name_owners`, `check_names`) and 4 tests, docstring adapted (B1) | `/new-map-prop` skill, its CLAUDE.md row, `tpac-static-prop-authoring.md`, the MithrilForge review: absent on 1.4.x; tools/README row (WP142/WP143) |
| `30abf550b` shared hunk (orchestrator note) | hand port of the loose-first hunk | `module_tpacs`, loose-first `tpac_paths_for_modules` and help text, `TpacModuleFallbackTests` (B1); `_loaded_tpacs` and its test (B4); the audit's package list (B2); doc corrections rewritten for 1.4.x (B1, B4) | ExcludedTargetMethods (WP107), config-protection (WP116), BannerBearers gate (WP130), native_crash_triage registers (WP137), lint regex (WP143), troll content; its rule gate-table row (the table is absent on 1.4.x); its armory-guide and armoury-mesh-cleanup text (desktop-only claims; 1.4.x wording written instead) |
| `719074072` shared hunk (not in the pack) | end state | `borrowed_body_issues` lists the Armory through `vmr.module_tpacs` and the matching test and comment edits (B1, B4) | everything else is WP137's; WP137's notes leave this hunk because the check was absent on 1.4.x, and this WP brings the check |
| `1571c2f91` shared hunk (orchestrator note K2) | append | the two Patch90 lessons in `harmony-il.md` (A2) | the rest is already on 1.4.x as `d8e3777cd` |
| `6d7e67f3f` feat(coop): server leaves client-only patch classes out | skip | | D7; 1.4.x has no `ClientOnlyAttribute` |
| `12afef4d2`, `f1675c058` (later audit tool changes) | skip | | they call 1.5.x-only companions (server skeleton pack, troll wiring and others); WP136 must not bring those parts |
| `d6b537092` (WP138) | not here | | its `validate_mesh_refs.py` docstring hunk will conflict with B1's rewording: keep B1's line |
| `8ce80c2ff`, `cd1fbdb6f` (docs truth pass, backlinks) | skip | | docs sync WPs |

## Test plan

- C# (Part A, ported with `fb8d73145`): `PreloadBodyGuardServiceTests` (7: all resolve on the first
  pass with no sleep; a never-resolving name dropped after the budget; a name resolving on the third
  pass kept; empty set; budget measured on the injected clock; a throwing resolver counted as
  unresolved; a non-positive budget). `PreloadBodyGuardBindingTests` (1, BindingVerification: the
  private field's name and type and the parameterless public target on the installed engine).
  `HarmonyPatchBindingTests` covers the target automatically; `CoopVetoClassificationTests` covers
  the new row. Structural pattern: the 1.5.x files are taken as they are.
- Python (Part B): `test_validate_mesh_refs` 60 to 69; `test_audit_armory_refs` 10 (7 ported, 3 new
  in B3: loose-tree detection, the SKIPPED report with exit 0, the refused regeneration);
  `test_validate_moduledata` 154 to 163 (7 `MissingCollisionBodyTests`, the reachability test, the
  missing-root `main()` test); `test_collision_body_borrowed` 14; `test_add_collision_body` 9.
- Untestable here, for `Not-tested:` and the maintainer: the live prefix on a real missing body
  (in-game probe below); a commit through the real hook; a run on a machine with both trees.

## Done criteria

- [ ] Part A and Part B done criteria above all hold (or Part B is recorded BLOCKED with the
      STOP that caused it, and Part A alone is reported DONE).
- [ ] `cd "$WT" && git log --format=%s bannerlord-1.4.5..HEAD` shows only subjects containing
      `v2.0.28 - `, each at most 72 characters.
- [ ] Build exit 0; full test suite 0 failed; tool suite no new failure; hooks no new failure;
      `lint_docs.py` no new finding in touched files.
- [ ] The hand-off reports status, commit hashes and the measured numbers (Step C1), and
      `git diff --name-only bannerlord-1.4.5..HEAD -- plans/README.md` prints nothing.

## STOP conditions

Stop and report (do not improvise) if:

1. A Step A0 prerequisite mark is absent (`TryPatchCategory` in `Main/SubModule.cs`,
   `Main/Composition/`, `hookSpecificOutput` in the moduledata hook, the retired CHANGELOG hook),
   or a "must not exist yet" path exists.
2. Any of the seven tool files in Step A0 item 4 changed since `1b00d1acd`.
3. A script prints `STOP:` (an anchor is missing or not unique: the file drifted).
4. `git cherry-pick -n fb8d73145` conflicts outside `Main/IoC.cs` and `Main/SubModule.cs`, or
   `git cherry-pick -n 05bd0a730` conflicts at all.
5. taom-src on the installed DLLs no longer shows `HashSet<string> _uniqueDynamicPhysicsShapeName`
   or a parameterless public `WaitForMeshesToBeLoaded` on `PreloadHelper`, or
   `PreloadBodyGuardBindingTests` fails. If it is inconclusive (game assemblies not loaded), report
   it as an environment failure; never count it green.
6. A new build warning names `PreloadHelper` or `PreloadBodyGuard`.
7. On the PUBLIC Armory, `validate_moduledata.py` reports any `MISSING_COLLISION_BODY` or
   `COLLISION_BODY_BORROWED` ERROR, or the audit lists a `MISSING_BODY` row. Commit B4 as planned,
   do not run B5's commit, do not edit or allowlist anything, and report the item ids, body names
   and file:line rows. Part B stays unmerged until the maintainer decides (with the hook live, every
   later ModuleData commit would be denied); Part A can merge.
8. A step's verification fails twice after a reasonable fix attempt, or a tool test that passed in
   `tooltests-before.txt` fails afterwards.
9. A change appears to need an out-of-scope file (for example `Main/TAOM.csproj`,
   `.claude/settings.json`, `CHANGELOG.md`, `tools/README.md`).
10. Report, do not stop: the warm full validator time exceeds 40 s (the hook kills at 45 s and then
    asks instead of passing); record it in the B4 body and the hand-off for the harness lane.

## In-game checks owed (maintainer)

1. **Patch90 applied.** Start the game with the merged build. In the newest `taom_debug_*.log`
   there is no `[PreloadGuard] init failed` line and no failure reported for
   `Patch90_PreloadBodyGuard`; in the developer console `taom.print_patches Patch90` lists
   `Patch90_PreloadBodyGuard` as applied.
2. **Healthy loads pay nothing.** A campaign field battle, a siege if available, a tournament and a
   custom battle each load as before, and `[PreloadGuard]` appears nowhere in the log.
3. **The guard fires on a real missing body** (maintainer only; executors never write the install).
   Back up one Armory item file, set the `body_name` of one shield or bow carried by a troop you can
   pick in Custom Battle to `bo_this_body_does_not_exist`, restart the game (item XML loads at
   startup), and start that custom battle. Expect: the loading screen drops, the first frame holds
   about 5 s, then the battle runs; `taom_debug` has exactly one ERROR
   `[PreloadGuard] dropped 1 unresolvable collision body name(s) after 5.0s so the mission can load: bo_this_body_does_not_exist ...`
   (the seconds may read a little above 5.0);
   the engine's `rgl_log_errors_<pid>.txt` may carry `get_object failed for body: bo_this_body_does_not_exist`.
   While the probe is in place, `python tools/validate_moduledata.py --code MISSING_COLLISION_BODY`
   reports one ERROR naming the body and the item. Restore the file, restart, and confirm a normal
   custom battle logs no `[PreloadGuard]` line.
4. **Triage note for player reports.** "Froze just as the loading screen disappeared" with
   `BattlePlayable` as the last `[BattleLoad]` line is this class: ask for `taom_debug` (a
   `[PreloadGuard]` line names the body) and `rgl_log_errors_<pid>.txt`. A stall on
   `WaitingForRender` with no `BattlePlayable` (the 2026-11-01 Harad report) is not.
5. D20 maintainer step, if not done in WP101: install the public 2026-09-29 Armory on the laptop so
   Step B5 can run.

## Maintenance notes

- **WP113** owns the BattleLoad triage: correct the comment at
  `MissionState_TickLoading_BattleLoad_Patch.cs:12-16`, `docs/features/battle-load-diagnostics.md:94`,
  `docs/reference/harmony-patch-registry.md:316` and the `#352 WaitForMeshesToBeLoaded shape` wording
  in `tools/triage_battle_load.py:1104-1113`: the v1.4.8 wait runs from `OnSceneRenderingStarted`
  after loading, so a `polls=1` stall inside `TickLoading` is something else.
- **WP136** (XSD gate `fa8c06f82` and later validator codes) will meet this WP's insertions on
  context: the test import block (`validate_xml_schemas as vx`), `xml.etree.ElementTree as ET` in
  `validate_moduledata.py`, `schema_invalid_issues` in `main()`. Keep both sides. Its
  `LiveTaomMapRegistrationTests` pin belongs there. Do not bring the `12afef4d2`/`f1675c058`
  companion checks into `audit_armory_refs.py`.
- **WP137** must skip `719074072`'s borrowed-body and `module_tpacs` hunks (applied here).
  **WP138**'s `d6b537092` hunk in `validate_mesh_refs.py` conflicts with B1's docstring line: keep B1's.
- **WP142/WP143** sync what this WP left as bookkeeping: tools/README rows (`audit_armory_refs.py`,
  `--check-name`, `add_collision_body.py`, the three codes), feature-map rows (PreloadBodyGuard,
  ArmoryRefAudit), INDEX, doc-lookup, hooks-catalog, CLAUDE.md's patch-category count and the
  #599/#633/#622 lessons. **WP143** owns the CLAUDE.md Armory trap row, the `/armory-audit` skill and
  the `session-start.sh` drift line: it takes each or records a skip with the reason. WP143's
  `.githooks` pre-commit code list should keep
  `MISSING_COLLISION_BODY` and `COLLISION_BODY_BORROWED`: they exist on 1.4.x after this WP.
- **WP115** (release integrity): run `python tools/validate_moduledata.py --code MISSING_COLLISION_BODY --code COLLISION_BODY_BORROWED`
  and `python tools/audit_armory_refs.py` against the Armory each release ships: the public
  channel ships an Armory publish this line does not version (#633 is what an unchecked Armory
  publish did on the 1.5.x line).
- **WP137** Part D regenerates `docs/reference/taleworlds-api-snapshot/patch-targets.md`
  so it lists Patch90.
- Hook budget: both body passes call `validate_mesh_refs.extract_refs` on the same Armory XML. If
  the laptop's warm total nears 45 s, the harness lane can raise the hook's inner bound (below the
  registered 60 s) or cache the extraction between the two passes.
- If Part A merges while Part B waits, the feature doc and the `[PreloadGuard]` log line already
  name `tools/audit_armory_refs.py`, which arrives with Part B.
- Residual risk: the guard drains body names only; a mesh whose `CheckResources()` never reaches
  zero still spins the same loop (native, unverified, no known trigger).
- PatchShield (TAOM.Dependencies) adds a finalizer to every patched engine method, this one
  included; the prefix catches its own exceptions, so no interaction is expected.
- `/deep-review` is owed on Part A (C#, 6 code files) before the integrator merges; probe: the
  four-underscore injection name against the v1.4.8 field, that the prefix never returns `false`,
  the hoisted `RemoveAll` predicate, and the placement before Patch62. On Part B probe: the
  indentation transform in `render_report`, the `main()` wiring against `test_every_issue_pass_is_reached_from_main`,
  and the hook code list.
