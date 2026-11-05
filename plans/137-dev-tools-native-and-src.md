# Plan 137: Give the 1.4.x line the 1.5.x native triage, engine-bump and taom-src tools, and a reproducible API snapshot

> **Executor instructions**: Follow this plan step by step in your own worktree (see "Git workflow").
> Run every verification command and confirm the expected result before moving on. If anything in
> "STOP conditions" occurs, stop and report; do not improvise. The plan has five Parts, each a clean
> hand-off point with its own done criteria: A (taom-src per build), B (native toolchain), C (engine-bump
> checkers and the dead TAOM.Dependencies guard), D (API snapshot and catalogue), E (hygiene). Plan on two
> or three sessions. Do not edit `plans/README.md`: the integrator owns it during this programme. Your
> hand-off reports status per Part, every commit hash, every count you measured and every result marked
> "record", and names the old-plan row the integrator should close (plan 005, see Maintainer step 6).
>
> **Drift check (run first, in the worktree)**:
> `git diff --stat 1b00d1acd..HEAD -- tools/taom-src.ps1 tools/decompile_bannerlord.ps1 tools/native_crash_triage.py tools/native_sig_author.py tools/snapshot_api_surface.ps1 tools/process_faction_map.py tools/dump_engine_skeleton.ps1 tools/extract_human_anims_tpac.ps1 tools/read_anim_keyframes_tpac.ps1 tools/tests/test_native_crash_triage_dump.py .claude/skills/taom-src .claude/skills/engine-bump .claude/skills/native-crash-triage .claude/skills/research .claude/skills/investigate .claude/skills/new-creature-mount .claude/skills/deep-review/SKILL.md .claude/agents .agents/skills/taom-research .ai/review-reference.md docs/ai-includes docs/reference/development-machines.md docs/reference/provenance-register.md docs/reference/bannerlord-engine-and-toolchain.md docs/reference/doc-lookup.md docs/reference/mcp-servers.md docs/reference/taleworlds-api-snapshot docs/migration/dr3-maintenance.md docs/migration/v1.4.8-impact.md docs/features/bannerlord-together-compat.md Dependencies TAOM.Tests/Migration/ReflectionSiteBindingTests.cs Main/ManualPatchApplicator.cs Main/Properties/launchSettings.json TAOM.sln .ai/verification.md .claude/skills/verify/SKILL.md`
> Earlier WPs of this programme change several of these (WP103 command lines and gates; WP107
> `Dependencies/`; WP109 engine-bump Phase 4 item 7; WP121 `IncompatibleModDetector.cs`; WP122
> `ReflectionSiteBindingTests.cs`, the snapshot README, the agent manual, development-machines; WP107, WP120
> or WP125 may have regenerated `patch-targets.md`; WP127 deletes the `PathReuseCache` row; WP102, WP103 and WP122 `/verify`; WP110 and WP122 `.ai/verification.md`). Each step edits
> through text anchors and says what to do when an anchor is gone. STOP only where a step says so.

## Status

- **Priority**: P2
- **Effort**: L (5 Parts, 14 commits)
- **Risk**: MED overall. LOW for Parts A, B, D and E (tools, tests, docs, skill text). MED for Part C2,
  which deletes a class from `TAOM.Dependencies` and therefore needs a paired release (#371).
- **Depends on**: `plans/103-executor-safety-harness.md` (Step 0 checks its markers). Wave 5 also assumes
  the earlier waves are merged; where a later step depends on WP122 or WP127 it checks and branches.
- **Category**: dx (developer tools), with one tech-debt deletion
- **Wave / lane**: wave 5, lane B
- **Executor**: laptop
- **Decisions applied**: D1 (never stage `CHANGELOG.md`; the commit body is the entry), D7 (co-op stays
  out; the Coop mentions in ported tool text are harmless), D8 (subjects carry `v2.0.28`), D9 (no CI: every
  gate here runs locally), D10 (a plain session executes this plan), D19 (git through the Bash tool only).
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04. (The 1.5.x ref has since moved to `c075c92f0`; of the files this plan reads,
  only the 1.5.x snapshot docs changed, and those are never copied. Every SHA below is a commit, so stable.)
- **Issue**: port of #688 (native decompile), #997 (native build match), #1004 (native archive), #1038
  (taom-src short names), the tools half of #736 (v1.5.5 bump), and plan 005 (1.5.x filed and closed it as
  #668). New work (snapshot refresh and catalogue fixes from verify report D4 WP-D, `TAOM_GHIDRA_PROJECTS`,
  the v1.4.8 pins and negative control): the orchestrator creates the issue before implementation lands.

## Why this matters

- **Freezes and hangs have no procedure on this line.** The battle freeze with sound, the Harad first-battle
  wedge (E3: two `WaitingForRender` samples, then silence) and E4's silent stalls are hangs, not crashes,
  and V02 found the 1.4.x watchdog cannot tell a wedge from a slow compile. The 1.4.x
  `/native-crash-triage` covers crashes only. This plan ports the hang-dump procedure (confirm the process,
  spot the spin, procdump, a WinDbgX stack, then read each native frame) and the native tools behind it,
  each checked on the installed v1.4.8 binaries during planning.
- **The v1.4.8 native image is archived nowhere.** Steam overwrote 1.4.7 and the open crash report at
  `crashz/report.json` can no longer be read against its binary. `tools/native_archive.py` plus one
  maintainer run keeps build 119303, its engine-method map and its matcher index for later triage.
- **taom-src is wrong for how everyone calls it.** Rules and plans write `taom-src path <ShortName>`; on
  1.4.x a short name probes every DLL for minutes and fails, and the cache keys on the version string, so a
  Steam hotfix that keeps `v1.4.8` would serve stale sources with no warning.
- **Engine-bump readiness.** `check_engine_member_refs.ps1` resolved the public build's `TAOM.dll` (3,257
  engine refs) and `TAOM.Dependencies.dll` (7) against v1.4.8 with 0 unresolved during planning;
  `engine_body_diff.py` parsed every row of this line's API snapshot (230, 108, 47 rows, 0 unparsed).
- **The API snapshot is stale and its check can never pass.** `patch-targets.md` says `Patches: 229` over
  230 rows and lacks six classes (D4 G1); `-Check` fails on the backlinks footer by design today.
- **Hygiene**: plan 005 has been TODO since June (a quote in a path runs as Python in
  `process_faction_map.py`); `SubModuleConstructionGuard` installs after every SubModule was constructed,
  so it can never fire; the default launch profile attaches the slow mixed debugger; the solution does not
  list `TAOM.Dependencies`; three tpac tools point at a TpacTool folder that no longer exists.

## Current state

### Files and roles (`origin/bannerlord-1.4.5` unless marked)

| Path | Role and state |
|---|---|
| `tools/taom-src.ps1` (395 lines, blob `d66064a16128`, equal to `857734c64^`) | `:49` `$IndexPath = Join-Path $CacheRoot 'dll-index.json'` (shared across versions); `:101-102` module order guessed from the presence of `TaleWorlds.*.dll`; `:132` `$script:VersionDir = Join-Path $CacheRoot $detected` (version string only); `:210` `& ilspycmd $DllPath -t $TypeFqn` (no reference path). No short-name lookup. |
| `tools/decompile_bannerlord.ps1` (blob `145fbc3d37e6`, equal to `f39b399b4^`) | no UTF-8 console setting |
| `.claude/skills/taom-src/SKILL.md` | documents `~/.taom-src/<version>/`, `~/.taom-src/dll-index.json` and the old module order. Deleted on 1.5.x, so its 1.4.x text is written fresh here. |
| `tools/native_crash_triage.py` (blob `467ebc8855f6`, equal to `30abf550b^`) and `tools/tests/test_native_crash_triage_dump.py` (blob `b18b9e69a2f6`, 22 tests) | no register dump, no decompile line; a leaf RVA exits with `check the base/offset math` only |
| `tools/native_decompile.py`, `native_engine_methods.py`, `native_build_match.py`, `native_archive.py`, `check_engine_member_refs.ps1`, `engine_body_diff.py` | absent |
| `.claude/skills/native-crash-triage/SKILL.md` (136 lines) | Phase 1 steps 1 to 7, Phase 2 (name the site, hand-decode), Phase 3 (debugger), Phase 4. No hang procedure. |
| `.claude/skills/engine-bump/SKILL.md` (143 lines) | Phase 1 items 1 to 3 (`:15-32`: item 2 says copy the native DLLs aside by hand, item 3 says `~/.taom-src/v<new>/`); Phase 4 item 1 (`:91-97`) says `-Check` reports DRIFT by design. WP109 appends Phase 4 item 7; WP122 edits item 1's first sentence. |
| `tools/snapshot_api_surface.ps1` (blob `036636c1cb17`) | `:250-260` `-Check` compares the generated text with the committed file including the backlinks footer, so it always reports DRIFT; `:21` and `:234` say targets are "resolved the way Harmony resolves it" while `:191-194` use `AccessTools` (base types searched, D4 G2) |
| `docs/reference/taleworlds-api-snapshot/{patch-targets,gamemodel-bases,reflection-sites,README}.md` | `patch-targets.md:7` `Patches: 229` over 230 rows; missing `Patch37_CrashReport`, `DecisionItemBaseVM_ExecuteDone_Patch`, `KingdomDecisionsVM_RefreshWith_AutoResolved_Patch`, and `Patch88_LordPartyTemplate`, `Patch88_SpawnLordPartyScope`, `Patch88_InitializeLordPartyPropertiesScope`. README `:19` and `:41` say 220 patches, `:42` 46 GameModels, `:43` 32 reflection members. |
| `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` | 45 `[DataRow]`s; `:64-65` the `TaleWorlds.Engine.PathReuseCache` row (a TAOM type, D4 G5; WP127 deletes it with its scaffold); no row for the two `TAOM.Dependencies` probes |
| `Dependencies/Foundation/SubModuleConstructionGuard.cs` (261 lines) | Harmony finalizers on `MBSubModuleBase` constructors and `Module.AddSubModule`; installed from `Dependencies/SubModule.cs:231-232` inside `OnSubModuleLoad`; named in comments at `AliasStubSubModule.cs:20,47`, `ReflectionUtils.cs:9`, `CoopPresence.cs:125`, `Dependencies/SubModule.cs:206` and in `Dependencies/_Module/THIRD-PARTY-LICENSES.txt:110`. No test references it. |
| `.ai/verification.md`, `.claude/skills/verify/SKILL.md` | no `api-snapshot` check row and no Step 2d on `1b00d1acd`; earlier WPs add other rows and steps to both. WP112 hands `6ae924638`'s and `0002cb561`'s hunks for them to this plan's Step D3, and WP138's condition K7 reads Step 2d. |
| `Main/ManualPatchApplicator.cs:10-16` | header says each patch "fail-safes with a logged warning"; true only for a target that is not found (D4 G4) |
| `tools/process_faction_map.py` (blob `8b9934db01d9`) | `:260` `read_png_rgba(r'{filepath}')` and `:383` `crop_and_save(r'{input_path}', r'{output_path}', ...)` paste paths into `python -c` source |
| `Main/Properties/launchSettings.json` (blob `f8cd346e3e7c`) | profile `Bannerlord` has `"debugEngines": "managed-framework,native"` |
| `TAOM.sln` (blob `3cb907378568`, 48 lines) | lists `TAOM` and `TAOM.Tests` only; `Main/TAOM.csproj:89` builds Dependencies through a `ProjectReference` |
| `tools/dump_engine_skeleton.ps1:37`, `tools/extract_human_anims_tpac.ps1:39`, `tools/read_anim_keyframes_tpac.ps1:31` | `-TpacToolBin` default `E:\Bannerlord_Art\TpacTool_0.4.0\TpacTool\bin`; `tools/Audit-MeshRefs.ps1:44` already defaults to `E:\Release_v0.5.1\bin` and has no provenance row for that fork |

### Engine, binary and machine facts (verified 2026-11-04; do not re-derive)

- **Module load order on v1.4.8** (taom-src, `C:\Users\mikew\.taom-src\v1.4.8\TaleWorlds.MountAndBlade.Module.cs`):
  `Initialize()` calls `LoadSubModules(modules, loadNewModules: false)` (`:267`); `LoadSubModules` (`:1026`)
  constructs every SubModule through `AddSubModule` (`:141-153`, `constructor.Invoke(new object[0])` at
  `:149`, no try/catch), skips one whose tags exclude it (`:1040`), shows a message box and continues for a
  missing DLL (`:1082`), and only then calls `InitializeSubModuleBases()` (`:1102`), whose loop calls each
  `OnSubModuleLoad` (`:201-208`, no catch). The only later construction is the native callback
  `LoadSingleModule` (`:236-243`). So a guard installed from `OnSubModuleLoad` cannot see a normal
  construction failure, and v1.4.8 does not catch one either.
- `public static List<ModuleInfo> GetActiveModules()` at `TaleWorlds.ModuleManager.ModuleHelper.cs:196`;
  `public static ApplicationVersion FromParametersFile(string customParameterFilePath = null)` at
  `TaleWorlds.Library.ApplicationVersion.cs:40` (taom-src). On 1.4.x they are reflected by name at
  `Dependencies/Foundation/IncompatibleModDetector.cs:210` and `VersionProbe.cs:78`.
- Installed client `bin\Win64_Shipping_Client\TaleWorlds.Native.dll`: 14,185,944 bytes, SHA-256 prefix
  `2a5e0e0b15513ebb`; wEditor DLL: 26,269,144 bytes, prefix `b87f2439e0869945`; both `Version.xml` say
  `v1.4.8`. `TaleWorlds.Library.dll` UTC write time `202609061157`, so the new taom-src cache folder is
  `C:\Users\mikew\.taom-src\v1.4.8-202609061157` (today only `v1.4.8` exists).
- **Engine-method map on v1.4.8** (tip `native_engine_methods.py`, run in memory): client 2,261 methods
  (MountAndBlade 686, Engine 1,542, DotNet 33), no one-sided id; registration functions `0x643720`,
  `0x391E90`, `0x751540`. wEditor 2,262 (687, 1,542, 33), no problem. Client addresses:
  `MBAgent.get_current_action_type` `0x6F5DC0`, `set_attack_state` `0x6F3AE0` (id 176),
  `set_scripted_target_entity` `0x6F6950`, `weapon_equipped` `0x6F4560` (id 252),
  `IPhysicsShape.ProcessPreloadQueue` `0x4B6A80` (Engine id 835, a `jmp 0x170FE0`); the REX `48 FF 25`
  jump to `KERNEL32!AcquireSRWLockShared` is at `0x9E3130`. An ASCII scan finds 52 of the 2,261 engine
  names inside `IMono_` assert strings.
- **native_build_match.py on v1.4.8** (in memory, no cache): client to wEditor, 2,043 single-owner
  methods, correct 149 (hash 146, strings 3), wrong 0, ambiguous 421, unknown 1,473, exit 0, 109 s;
  wEditor to client, 2,137 methods, correct 153 (hash 149, strings 4), wrong 0, ambiguous 413, unknown
  1,571, 133 s.
- **native_crash_triage.py (tip) on v1.4.8**: `--rva 0x6F5DC0` exits 1 with `not inside any .pdata
  function: a leaf function ...` and then the `decompile:` line; `--rva 0x643720` exits 0 with
  `crash function: 0x643720 .. 0x64676B (size 0x304B, crash at +0x0)` and ends with the `decompile:` line.
- **check_engine_member_refs.ps1 (tip) on v1.4.8**: a Debug build of `1b00d1acd` `TAOM.dll` 3,267 refs and
  `TAOM.Dependencies.dll` 7, the installed public build 3,257 and 7, all `0 unresolved`.
- **Laptop toolchain**: Python 3.14.7 with capstone 5.0.7 and pefile; `pwsh` 7; `ilspycmd`; no Ghidra
  (`GHIDRA_INSTALL_DIR` unset), no pyghidra, no procdump, WinDbgX or cdb on PATH, no E: drive; dump at
  `C:\Decompiled_Bannerlord` (`_shipping_build`, `_editor_build`, `_modules_build`, `_categories_v1.4.8`,
  no older baseline); no BUTR reference assemblies in the NuGet cache; no `Dependencies/.vendor-source/`.
- Types the taom-src tests decompile exist on v1.4.8: `BuildInfo` (global, `TaleWorlds.Library.cs:68`),
  `AgentVisuals` and `TaleWorlds.MountAndBlade.View.MissionViews.Order.OrderTroopPlacer`
  (`Native__TaleWorlds.MountAndBlade.View.cs:584`, `:28604`), `DefaultPartyWageModel`.

### Conventions that bind this work

- TDD: every new tool lands after its test module was run red. ADR-007 adapters, ADR-002 thin entry points
  (under 150 lines), ADR-003 no `#region`, ADR-004 no `[Obsolete]`, ADR-005 no `#if DEBUG` outside IoC
  registration: this plan adds no game code, and its C# edits are a deletion, comments and test rows.
- The MCM json2 rule (rename a persisted setting, never flip its default): not touched here.
- `.claude/rules/harmony-patches.md`: not touched; no patch is added.
- Docs and skills you write: no em or en dash; new docs keep the TAOM feature-doc shape; a provenance row
  travels in the same commit as the code it describes (`.claude/rules/provenance.md`).
- Single-owner files `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`: not edited.
- `.claude/settings.json`, `.claude/settings.local.json`, `Directory.Build.props`, `docs/adrs/*`: not edited.

## Commands you will need

Every command runs through the Bash tool as `cd /c/Users/mikew/source/repos/taom-port/137 && export MSYS_NO_PATHCONV=1 && <command>`
(the shell does not keep `cd` between calls; `MSYS_NO_PATHCONV=1` keeps `git show <ref>:.claude/...` working).
Never `./build.ps1`. "S" means the scratch folder `/c/Users/mikew/source/repos/taom-port/scratch/137`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)` |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0` |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<Class>"` | `Failed: 0` |
| Binding gate | the Test command plus `--filter "TestCategory=BindingVerification"`, and `--settings TAOM.Tests/binding-gate.runsettings` before `--filter` when that file exists (WP122) | `Failed: 0` |
| Tool module | `python -m unittest tools.tests.<module> -v` | `OK`, skips only as the step says |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no failure that Step 0 did not have |
| Docs | `python tools/lint_docs.py` after staging and before committing | no finding naming a file you touched (dash lines included) |
| Hooks | `bash tools/test_hooks.sh` | no new failure vs Step 0 |
| Config audit | `python tools/audit_claude_config.py --min HIGH` | no HIGH finding Step 0 lacked |

C# build and full test run at Step 0, after every commit that touches C#, a project, the solution or the
snapshot (C2, D1, D2, E3), and at the end of each Part. A commit that changes only tools, tests under
`tools/tests` or docs cannot change the C# build; its gate is the tool module, Docs and Hooks rows.

## Scope

**In scope** (create or modify):
- Part A: `tools/taom-src.ps1`, `tools/decompile_bannerlord.ps1`, new `tools/tests/test_taom_src_short_names.py`,
  `.claude/skills/taom-src/SKILL.md`, `.claude/skills/engine-bump/SKILL.md`,
  `docs/ai-includes/{taleworlds-research-guide,codex-operating-guide,agent-operating-manual}.md`, `docs/reference/mcp-servers.md`.
- Part B: `tools/native_crash_triage.py`, `tools/native_sig_author.py` (docstring), new `tools/{native_decompile,native_engine_methods,native_build_match,native_archive}.py`,
  `tools/tests/test_native_crash_triage_dump.py`, new `tools/tests/test_native_{decompile,engine_methods,build_match,archive}.py`,
  new `docs/features/{ghidra-native-decompile,native-build-match}.md`, new `docs/reviews/{adopt-ghidra-hindsight-2026-09-26,adopt-rea-2026-10-24}.md`,
  `docs/reference/{development-machines,provenance-register,bannerlord-engine-and-toolchain,doc-lookup,mcp-servers}.md`,
  `docs/ai-includes/{agent-operating-manual,taleworlds-research-guide}.md`, `.ai/review-reference.md`,
  `.agents/skills/taom-research/SKILL.md`, `.claude/agents/{taleworlds-researcher,debugger,error-detective}.md`,
  `.claude/skills/{research,investigate,new-creature-mount,native-crash-triage,engine-bump}/SKILL.md`, `.claude/skills/deep-review/SKILL.md`.
- Part C: new `tools/{check_engine_member_refs.ps1,engine_body_diff.py}`, new `tools/tests/test_{check_engine_member_refs,engine_body_diff}.py`,
  new `tools/tests/fixtures/build_assemblies.ps1`, `tools/tests/test_taom_src_short_names.py`, `.claude/skills/engine-bump/SKILL.md`;
  delete `Dependencies/Foundation/SubModuleConstructionGuard.cs`; `Dependencies/{SubModule,AliasStubSubModule}.cs`,
  `Dependencies/Foundation/{CoopPresence,ReflectionUtils}.cs`, `Dependencies/_Module/THIRD-PARTY-LICENSES.txt`,
  `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`, `docs/reference/taleworlds-api-snapshot/reflection-sites.md`,
  `docs/migration/dr3-maintenance.md`, `docs/features/bannerlord-together-compat.md`, `docs/reference/provenance-register.md`.
- Part D: `tools/snapshot_api_surface.ps1`, `docs/reference/taleworlds-api-snapshot/{patch-targets,gamemodel-bases,README,reflection-sites}.md`,
  `.claude/skills/engine-bump/SKILL.md`, `docs/migration/v1.4.8-impact.md`, `Main/ManualPatchApplicator.cs` (comment),
  `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` (only if the `PathReuseCache` row still exists), `.ai/verification.md`, `.claude/skills/verify/SKILL.md`.
- Part E: `tools/process_faction_map.py`, new `tools/tests/test_process_faction_map.py`, `docs/ai-includes/external-repo-adoption.md`,
  `Main/Properties/launchSettings.json`, `TAOM.sln`, `tools/{dump_engine_skeleton,extract_human_anims_tpac,read_anim_keyframes_tpac}.ps1`,
  `docs/reference/{development-machines,provenance-register}.md`.

**Out of scope** (do not touch):
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, any patch, service, adapter or ModuleData file.
- `.claude/settings*.json`, `.claude/hooks/**` (no session-start wiring for the archive, see Maintenance),
  `tools/test_hooks.sh`, `Directory.Build.props`, `docs/adrs/**`, `GameReferences.targets`.
- Bookkeeping that WP142, WP143 and WP144 sync once: `CHANGELOG.md`, `CLAUDE.md`, `AGENTS.md`, `tools/README.md`,
  `docs/INDEX.md`, `docs/reference/feature-map.md`, `docs/reviews/lessons/*`, `docs/reviews/LESSONS-LEARNED.md`,
  `docs/reviews/REVIEW-LOG.md`. The exact lines they owe are listed in Maintenance notes.
- `tools/check_handbook_attributes.py` and its manifest (the stale-manifest half of `4e408e0e0` is skipped, see the disposition table).
- `.mcp.json`, `.codex/config.toml` (plan 005's MCP pins are plan 016, WP142), `.gitignore`, `Dependencies/.vendor-source/`.
- Never vendor `MCMv5.dll` (nothing in this plan adds a binary; `Main/_Module/bin/Win64_Shipping_Client/` stays as it is).
- 1.5.x-only content: TrollBruteForce, hill-troll tools, community guides, Animalia, armour classes, the
  Harad starter rosters, TAOM.Client rows, BUTR reference assemblies, version pins and impact docs.

## Git workflow

- Worktree (from the main checkout, Bash tool):
  `cd /c/Users/mikew/source/repos/TAOM && git worktree add /c/Users/mikew/source/repos/taom-port/137 -b port148/137-dev-tools-native-and-src bannerlord-1.4.5 && mkdir -p /c/Users/mikew/source/repos/taom-port/scratch/137`.
  Work only there. Never push, open a PR, tag, or merge into `bannerlord-1.4.5`. Git through the Bash tool only (D19).
- Methods (binding procedure from the decisions brief): `git cherry-pick -n <sha>` where a commit applies
  with only bookkeeping conflicts (`37ff41e4a`, `6b34fd00f`, `4ab005dd1`); never let cherry-pick,
  `--continue`, `-C`, `-c` or `--amend --no-edit` create a commit (`GIT_EDITOR=true` on this laptop commits
  the 1.5.x message). A series whose files are new on 1.4.x is an **end-state transplant**:
  `git show <sha>:<path> > <path>` from the commit this plan names (each source was checked equal to the
  1.5.x tip unless the step says otherwise). Hand edits use the helper `S/rx.py` (Step 0), which changes
  each anchor exactly once or stops.
- Drop bookkeeping hunks: a conflicted one `git checkout --ours <path> && git add <path>`, a cleanly changed
  one `git checkout HEAD <path>`. This WP keeps its `.claude/skills` and `.claude/agents` hunks because it
  carries the `harness-hang-triage` group and the skill text that drives the tools it ports.
- Stage explicit paths (`git add <path> ...`, never `-A`), then run
  `git commit -F /c/Users/mikew/source/repos/taom-port/scratch/137/msg.txt` in the NEXT, separate Bash call.
  Never stage `CHANGELOG.md` (D1).
- Commit message template (overwrite `S/msg.txt` per commit):

```
<type>(<scope>): v2.0.28 - <description>      (at most 72 characters)

<Body for 1.4.x readers, wrapped at 72: what changed on the 1.4.8 line
and why. Never paste a 1.5.x body. No em or en dashes. No Co-Authored-By
or "Generated with" line.>

Research: <what was verified on v1.4.8>
Not-tested: <what needs a maintainer or in-game check>
Ported-from: <40-hex sha>     (one line per included 1.5.x commit; omit for new work)
```

  If Step 0 shows a release commit moved `Main/_Module/SubModule.xml` past `v2.0.28`, use that version (D8).

## Steps

### Step 0: Base checks, baselines and the edit helper (no commit)

1. Create the worktree and scratch folder (Git workflow). In the worktree, each must print as shown:
   - `git status --porcelain | wc -l` prints `0`.
   - `test -f .githooks/commit-msg && test -f tools/check_commit_msg.py && git grep -q hookSpecificOutput -- .claude/hooks && echo WP103-OK` prints `WP103-OK`. Missing: STOP (WP103 not merged).
   - `grep -o '<Version value="[^"]*"' Main/_Module/SubModule.xml` prints `<Version value="v2.0.28"` (or the released version, D8).
   - `for f in tools/native_decompile.py tools/native_engine_methods.py tools/native_build_match.py tools/native_archive.py tools/check_engine_member_refs.ps1 tools/engine_body_diff.py tools/tests/test_taom_src_short_names.py tools/tests/test_process_faction_map.py docs/features/ghidra-native-decompile.md docs/features/native-build-match.md; do test -e $f && echo "PRESENT $f"; done` prints nothing. Any `PRESENT`: STOP.
   - `for f in tools/taom-src.ps1 tools/decompile_bannerlord.ps1 tools/native_crash_triage.py tools/tests/test_native_crash_triage_dump.py tools/process_faction_map.py Main/Properties/launchSettings.json TAOM.sln; do echo "$(git rev-parse --short=12 HEAD:$f) $f"; done`
     prints `d66064a16128`, `145fbc3d37e6`, `467ebc8855f6`, `b18b9e69a2f6`, `8b9934db01d9`, `f8cd346e3e7c`, `3cb907378568` in that order. A different blob: STOP for the step that overwrites that file (an earlier WP changed it).
   - `cat "$BANNERLORD_GAME_DIR/bin/Win64_Shipping_Client/Version.xml"` shows `v1.4.8`, and `stat -c %s "$BANNERLORD_GAME_DIR/bin/Win64_Shipping_Client/TaleWorlds.Native.dll"` prints `14185944`. Otherwise STOP and recommend `/engine-bump`.
   - `pwsh -NoProfile -Command '$PSVersionTable.PSVersion.Major'` prints `7`; `ilspycmd --version` exits 0; `python -c "import capstone, pefile; print('ok')"` prints `ok`. A missing tool is an environment failure: report it, do not install anything.
2. Record the state earlier WPs left (later steps branch on it):
   `test -f TAOM.Tests/binding-gate.runsettings && echo STRICT`; `test -f TAOM.Tests/Migration/HarmonyTargetResolver.cs && echo WP122-A2`;
   `grep -c PathReuseCache TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` (0 means WP127 removed it);
   `grep -c EveryLineLabel_PointsAtALineThatNamesItsMember TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` (1 means WP122's label gate exists).
3. Baselines (record for the hand-off): Restore, Build, Test (T0 Total, S0 Skipped); Binding gate (BV0);
   Tool tests (`Ran` TT0 and the failure list); Hooks (H0); `python tools/lint_docs.py --summary` (L0);
   Config audit (A0). Any `Failed:` above 0 in Test: STOP.
4. Write the edit helper (outside the repo):

```bash
cat > /c/Users/mikew/source/repos/taom-port/scratch/137/rx.py <<'EOF'
"""edit(path, [(regex, new_text), ...]): each regex (DOTALL) must match exactly once; line endings kept."""
import re, sys

def edit(path, subs):
    raw = open(path, "rb").read().decode("utf-8")
    crlf = "\r\n" in raw
    text = raw.replace("\r\n", "\n")
    for pattern, new in subs:
        hits = len(re.findall(pattern, text, flags=re.S))
        if hits != 1:
            sys.exit(f"STOP: {path}: {hits} matches for {pattern[:80]!r}")
        text = re.sub(pattern, lambda m: new, text, count=1, flags=re.S)
    if crlf:
        text = text.replace("\n", "\r\n")
    open(path, "wb").write(text.encode("utf-8"))
    print(f"edited {path} ({len(subs)} change(s))")

lit = re.escape
EOF
```

   Step scripts below run as `python - <<'EOF'` blocks that start with
   `import sys; sys.path.insert(0, r"C:\Users\mikew\source\repos\taom-port\scratch\137"); from rx import edit, lit`.
   A `STOP:` line from the helper is a STOP condition unless the step gives a fallback.

---

## Part A: taom-src takes short names and caches per game build

### Step A1: Transplant taom-src at its 1.5.x end state (`857734c64`, `97219836c`, the taom-src and UTF-8 hunks of `f39b399b4`, the cache lines of `fe20eddff`)

Sources: `857734c64` fix(tools): v2.0.36 - taom-src takes short names, reads the game first; `97219836c`
fix(tools): v2.0.36 - taom-src's cache follows the game build; `f39b399b4` chore(engine): v2.0.36 - pins for
the v1.5.6 hotfix, changeset 125270 (tool hunks only); `fe20eddff` docs: v2.0.36 - the hotfix, the taom-src
cache and the leaderboard plan (two doc lines only). Simulation onto `1b00d1acd`: `857734c64` conflicts in
the engine-bump skill only; `97219836c` in the skill, `taom-src.ps1` and the absent test; `f39b399b4` in five
1.5.x pin files; `fe20eddff` in eight Long Defeat and bookkeeping paths. Method: end-state transplant of the
three tool files (the 1.4.x blobs equal the parents), hand edits for docs.

1. RED. Write the test without its `ModuleOrder` class (that class reads `tools/engine_body_diff.py`, which
   arrives in C1):

```bash
git show f39b399b4:tools/tests/test_taom_src_short_names.py > tools/tests/test_taom_src_short_names.py && python - <<'EOF'
import sys; sys.path.insert(0, r"C:\Users\mikew\source\repos\taom-port\scratch\137"); from rx import edit, lit
edit("tools/tests/test_taom_src_short_names.py",
     [(r"class ModuleOrder\(unittest\.TestCase\):.*?\n\n\n(?=@unittest\.skipUnless\(shutil\.which\(\"pwsh\"\))", "")])
EOF
python -m unittest tools.tests.test_taom_src_short_names.AnUpdateThatKeepsTheVersion -v
```

   **Verify**: the run ends `FAILED (failures=1)` (the same cache folder before and after). Run only this
   class red: the other classes probe every DLL for minutes on the old script.
2. GREEN. `git show f39b399b4:tools/taom-src.ps1 > tools/taom-src.ps1 && git show f39b399b4:tools/decompile_bannerlord.ps1 > tools/decompile_bannerlord.ps1`.
3. Skill and doc edits:

```bash
python - <<'EOF'
import sys; sys.path.insert(0, r"C:\Users\mikew\source\repos\taom-port\scratch\137"); from rx import edit, lit
edit(".claude/skills/engine-bump/SKILL.md", [(r"3\. `taom-src` auto-detects the new version.*?caches remain but are unused\.", r"""3. `taom-src` auto-detects the new build: a fresh cache and DLL index under `~/.taom-src/v<new>-<stamp>/`,
   and the old caches stay unused. The stamp is `TaleWorlds.Library.dll`'s UTC write time, because a Steam
   hotfix can keep the version string. Pass a short or fully qualified type name. A mod's own copy of an
   engine assembly (`Modules\Coop`) searches last (#1038), so only a type that copy alone still holds
   resolves from it. The tool prints only the cache path; `~/.taom-src/sources.json` names the DLL it read.""")])
S = ".claude/skills/taom-src/SKILL.md"
edit(S, [
 (lit('argument-hint: "[path|list|remove|clean] <FullyQualifiedType>"'), 'argument-hint: "[path|list|remove|clean] <Type or FullyQualifiedType>"'),
 (r"Wraps `ilspycmd` against the \*\*installed DLLs\*\*[^\n]*", r"""Wraps `ilspycmd` against the **installed DLLs** (auto-detects the engine version from `Version.xml`, currently v1.4.8) and caches per game build at `~/.taom-src/<version>-<stamp>/`, where `<stamp>` is `TaleWorlds.Library.dll`'s UTC write time, so a Steam hotfix that keeps the version string still gets a fresh cache (the old folder stays for any session reading it). Inspired by [vercel-labs/opensrc](https://github.com/vercel-labs/opensrc). Use this **before** every `[HarmonyPatch]`, `GameModel` override, or adapter that touches a TaleWorlds type; never guess signatures from the `E:\Decompiled_Bannerlord\` dump (it can lag the installed engine after a bump)."""),
 (lit("pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel\n\n"),
  "pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel\n\n# A short name works when one engine type holds it; an ambiguous one lists the full names\npwsh tools/taom-src.ps1 path DefaultPartyWageModel\n\n"),
 (lit("| `path <Type>` | Print absolute path to cached `.cs`. Decompiles on miss. |"),
  "| `path <Type>` | Print absolute path to cached `.cs`. Decompiles on miss. A name with no dot resolves through the build folder's `engine-types.tsv`; an ambiguous one throws and lists the candidates. |"),
 (lit("| `path <Type> -Dll <Name>` | Skip namespace heuristic; force a specific DLL. |"),
  "| `path <FullType> -Dll <Name>` | Skip the lookup; force a specific DLL (needs the full name). |"),
 (r"1\. \*\*DLL index cache\*\*[^\n]*", r"""0. **Short-name index** (a name with no dot): `engine-types.tsv` in the build folder lists the short name, full name and DLL of every top-level type in the `TaleWorlds*`, `SandBox*`, `StoryMode*` and `NavalDLC*` assemblies, built once per build from assembly metadata in a few seconds. One match resolves; several throw and list them; a miss rebuilds the index once. The row's DLL is only the first probe, so a moved type still falls through to steps 1 to 3.
1. **DLL index cache**: `dll-index.json` in the build folder (per build since #1038; a shared one in the cache root outlived an engine bump) remembers `type -> dll` from prior lookups. Instant hit."""),
 (r"that holds DLLs \u2014 modules shipping `TaleWorlds\.\*`\nassemblies before third-party ones\.", r"""that holds DLLs: the game's own modules (`$VanillaModules` in the script:
`Native`, `SandBox`, `SandBoxCore`, `StoryMode`, `CustomBattle`, `BirthAndDeath`, `Multiplayer`, `NavalDLC`,
`FastMode`) before every other module, so a mod's own copy of an engine assembly (Coop ships about 40, and
they go stale) is read last (#1038)."""),
 (lit('DLL" for all of them.'), 'DLL" for all of them. `ilspycmd` runs with the game\'s bin as its reference path (`-r`), so a type in a\nmodule\'s own bin decompiles against the engine instead of leaving `//IL_` lines.'),
 (lit("| Looking for a class but don't know the FQN | `grep -r \"ClassName\" E:/Decompiled_Bannerlord/` to find namespace, **then** `taom-src path` to verify against the installed engine |"),
  "| Looking for a class but don't know the FQN | pass the short name (`taom-src path Formation`); an ambiguous one lists the full names to pick from |"),
])
edit("docs/ai-includes/taleworlds-research-guide.md", [
 (lit("takes a fully qualified\ntype and locates its assembly"), "takes a short or fully\nqualified type name and locates its assembly"),
 (lit("It writes a versioned cache under the user's `.taom-src` directory."), "It writes a cache per game build under the user's `.taom-src` directory.")])
edit("docs/ai-includes/codex-operating-guide.md", [
 (lit("`tools/taom-src.ps1 path Fully.Qualified.Type` is the"), "`tools/taom-src.ps1 path <Type>` (a short or fully qualified name) is the"),
 (lit("It writes a versioned cache under the user's profile"), "It writes a cache per game build under the user's profile")])
edit("docs/reference/mcp-servers.md", [
 (lit("(version auto-detected from `Version.xml`) and caches under `~/.taom-src/<version>/`."),
  "(version from `Version.xml`, build stamp from `TaleWorlds.Library.dll`) and caches under `~/.taom-src/<version>-<stamp>/`.")])
edit("docs/ai-includes/agent-operating-manual.md", [
 (lit("`pwsh tools/taom-src.ps1 path <FullTypeName>`"), "`pwsh tools/taom-src.ps1 path <Type>`"),
 (lit("on cache miss, prints an absolute `.cs` path."), "on cache miss, prints an absolute `.cs` path. Takes a short or fully qualified name; the cache is per game build.")])
EOF
```

   A `STOP:` on the agent manual or the guides means an earlier WP reworded that sentence: apply the same
   meaning by hand to the live sentence, and note it in the hand-off.

**Verify**:
- `python -m unittest tools.tests.test_taom_src_short_names -v`: `Ran 12 tests`, `OK`, skips at most 2
  (`test_a_mods_copy_of_an_engine_assembly_comes_last` skips without a `Modules\Coop`; the ambiguity test
  skips only if no ambiguous short name exists). Takes a few minutes.
- `pwsh -NoProfile -File tools/taom-src.ps1 path DefaultPartyWageModel 2>/dev/null | tail -1` ends
  `\v1.4.8-202609061157\TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel.cs`.
- `grep -c "//IL_" "$(pwsh -NoProfile -File tools/taom-src.ps1 path TaleWorlds.MountAndBlade.View.MissionViews.Order.OrderTroopPlacer 2>/dev/null | tail -1)"` prints `0`.
- `git diff --stat HEAD -- tools/taom-src.ps1 tools/decompile_bannerlord.ps1` shows both files; `git rev-parse --short=12 :tools/taom-src.ps1` after staging prints `765331fc591d`.
- Docs, Hooks, Config audit as in Commands.

**Commit A1** (stage `tools/taom-src.ps1 tools/decompile_bannerlord.ps1 tools/tests/test_taom_src_short_names.py .claude/skills/taom-src/SKILL.md .claude/skills/engine-bump/SKILL.md docs/ai-includes/taleworlds-research-guide.md docs/ai-includes/codex-operating-guide.md docs/ai-includes/agent-operating-manual.md docs/reference/mcp-servers.md`):
`fix(tools): v2.0.28 - taom-src takes short names, caches per game build`. Body: a short type name now
resolves through a per-build index (`engine-types.tsv`, from assembly metadata) instead of probing every DLL
and failing; the game's own modules are searched before any other module; the cache folder carries the
build stamp (`v1.4.8-202609061157` here) so a same-version hotfix gets fresh sources; module-bin types
decompile against the game bin; `decompile_bannerlord.ps1` forces UTF-8 output. The first run after this
decompiles into the new folder; the old `v1.4.8` folder stays unused. Trailers:
`Research: taom-src test types on v1.4.8 (BuildInfo, AgentVisuals, OrderTroopPlacer, DefaultPartyWageModel)`,
`Not-tested: none (tool, test and docs only)`, `Ported-from:` `857734c64f3588959ce46e6b74ba734aa57de721`,
`97219836c5184fbe0dc5fb3549342b290ce904b9`, `f39b399b49e8d2249ca3933ece419c07823d5be4`, `fe20eddff233417935d6e0d6faa59fbd20298250`.

### Part A done criteria

- [ ] `git log --format=%s bannerlord-1.4.5..HEAD` lists the A1 subject with `v2.0.28 - ` and at most 72 characters.
- [ ] `python -m unittest tools.tests.test_taom_src_short_names` is `OK`; Tool tests `Ran` = TT0 + 12, no new failure.
- [ ] Build exit 0 and Test `Total` = T0, `Failed: 0` (no C# changed; run once at the Part end).

---

## Part B: native triage, decompiler, engine-method map, build match and archive

### Step B1: Crash triage registers and decompile line, the decompiler and the engine-method map (`30abf550b` triage hunk, `719074072` and `d51e43028` tools)

Sources: `30abf550b` "Add unit tests for various tools and functionalities" (shared; only the
`native_crash_triage.py` register dump); `719074072` "Add unit tests for animation clip generation and
native decompilation" (first `native_decompile.py`, the triage `decompile_hint`, the Ghidra doc and records);
`d51e43028` feat(tools): v2.0.30 - read native engine calls and strings as C (`--engine-method`,
`--string`, `native_engine_methods.py`). Simulation: `719074072` conflicts in 50 paths (TrollBruteForce,
hill-troll tooling, community guides), so it is never picked; `d51e43028` in 11. Method: end-state
transplant per file. Sources (equal to the 1.5.x tip): `native_crash_triage.py` and its test at `719074072`,
`native_decompile.py` and `native_engine_methods.py` at `d51e43028`, their tests at `f39b399b4`, the Ghidra
doc at `d51e43028` (its `/research` wording fits 1.4.x, which keeps that skill).

1. RED: write the three test modules, then edit their pins and add the project-folder test:

```bash
git show 719074072:tools/tests/test_native_crash_triage_dump.py > tools/tests/test_native_crash_triage_dump.py && \
git show f39b399b4:tools/tests/test_native_decompile.py > tools/tests/test_native_decompile.py && \
git show f39b399b4:tools/tests/test_native_engine_methods.py > tools/tests/test_native_engine_methods.py && python - <<'EOF'
import sys; sys.path.insert(0, r"C:\Users\mikew\source\repos\taom-port\scratch\137"); from rx import edit, lit
edit("tools/tests/test_native_decompile.py", [
 (lit("the others pin v1.5.3 answers\nof each mode and skip on any other engine."), "the others pin per-build answers\n(PINS) and skip on an unlisted build."),
 (r"    # Client addresses per engine version\. Add a row at every engine bump.*?\n    \}\n", r'''    # Client addresses per engine version. Add a row at every engine bump (derive the values with
    # native_engine_methods / this CLI); an unlisted version skips rather than passing silently.
    # The stub is the REX `48 FF 25` jump to KERNEL32!AcquireSRWLockShared.
    # v1.4.8 (client 14,185,944 bytes) candidates, read 2026-11-04 from the engine-method map and a
    # capstone scan but not yet run under Ghidra, so no row: set_attack_state 0x6F3AE0,
    # weapon_equipped 0x6F4560 (MountAndBlade id 252), process_preload_queue 0x4B6A80 (a jmp to
    # thunk_target 0x170FE0), import_stub 0x9E3130, get_current_action_type 0x6F5DC0.
    PINS = {}
'''),
 (r"\n\n(?=class ReportTests\(unittest\.TestCase\):)", r'''


class ProjectDirTests(unittest.TestCase):
    """The Ghidra project and cache folder: $TAOM_GHIDRA_PROJECTS on a machine with no E: drive."""

    def test_the_variable_names_the_folder(self):
        self.assertEqual(nd.default_project_dir({"TAOM_GHIDRA_PROJECTS": r"C:\ghidra\TAOM"}), r"C:\ghidra\TAOM")

    def test_a_blank_variable_keeps_the_desktop_folder(self):
        self.assertEqual(nd.default_project_dir({"TAOM_GHIDRA_PROJECTS": "   "}), r"E:\ghidra\TAOM")

    def test_no_variable_keeps_the_desktop_folder(self):
        self.assertEqual(nd.default_project_dir({}), r"E:\ghidra\TAOM")


'''),
])
edit("tools/tests/test_native_engine_methods.py", [
 (lit("then the v1.5.3 addresses, pinned only on that build."), "then the addresses and counts pinned per build (the pins table)."),
 (r"        # Client addresses and mapped-method counts per engine version.*?\n        \}\n", r'''        # Client addresses and mapped-method counts per engine version; add a row at every engine
        # bump. v1.4.8 (client 14,185,944 bytes) was read on 2026-11-04 from the installed binary.
        pins = {
            "v1.4.8": {"addresses": (("get_current_action_type", 0x6F5DC0), ("set_attack_state", 0x6F3AE0),
                                     ("set_scripted_target_entity", 0x6F6950)),
                       "counts": {"MountAndBlade": 686, "Engine": 1542, "DotNet": 33}},
        }
'''),
])
EOF
for m in test_native_crash_triage_dump test_native_decompile test_native_engine_methods; do python -m unittest tools.tests.$m 2>&1 | tail -3; done
```

   **Verify**: the triage module fails (the new register and decompile-hint tests); the other two error on
   import (`No module named 'native_decompile'` / `'native_engine_methods'`).
2. GREEN:

```bash
git show 719074072:tools/native_crash_triage.py > tools/native_crash_triage.py && \
git show d51e43028:tools/native_engine_methods.py > tools/native_engine_methods.py && \
git show d51e43028:tools/native_decompile.py > tools/native_decompile.py && python - <<'EOF'
import sys; sys.path.insert(0, r"C:\Users\mikew\source\repos\taom-port\scratch\137"); from rx import edit, lit
edit("tools/native_decompile.py", [
 (lit('DEFAULT_PROJECT_DIR = r"E:\\ghidra\\TAOM"\n'), r'''PROJECTS_ENV = "TAOM_GHIDRA_PROJECTS"
DESKTOP_PROJECT_DIR = r"E:\ghidra\TAOM"


def default_project_dir(env=None):
    """$TAOM_GHIDRA_PROJECTS when set (the laptop has no E: drive), else the desktop folder. The
    Ghidra projects, the engine-method maps and native_build_match's indexes all live here."""
    env = os.environ if env is None else env
    return env.get(PROJECTS_ENV, "").strip() or DESKTOP_PROJECT_DIR


DEFAULT_PROJECT_DIR = default_project_dir()
'''),
 (lit("(default E:\\\\ghidra\\\\TAOM)"), "(default $TAOM_GHIDRA_PROJECTS, else E:\\\\ghidra\\\\TAOM)"),
])
EOF
```

   The second pattern matches the module docstring text `(default E:\\ghidra\\TAOM)` exactly as written in
   the file (doubled backslashes).
   **Verify**: `git show d51e43028:tools/native_decompile.py > /c/Users/mikew/source/repos/taom-port/scratch/137/nd.orig.py && git diff --no-index --stat --ignore-cr-at-eol /c/Users/mikew/source/repos/taom-port/scratch/137/nd.orig.py tools/native_decompile.py`
   shows one file with only the two edits (about 13 changed lines).
3. Docs for the decompiler (all new text below has no long dash):
   - `git show d51e43028:docs/features/ghidra-native-decompile.md > docs/features/ghidra-native-decompile.md`, then:

```bash
python - <<'EOF'
import sys; sys.path.insert(0, r"C:\Users\mikew\source\repos\taom-port\scratch\137"); from rx import edit, lit
edit("docs/features/ghidra-native-decompile.md", [
 (lit("`/research`, the `taleworlds-researcher` agent, the deep-review engine lens |"), "`/research`, the `taleworlds-researcher` agent, the `/deep-review` compatibility agent |"),
 (lit("e.g. Win64_Shipping_Client-45be32c57c451f78"), "e.g. Win64_Shipping_Client-2a5e0e0b15513ebb (v1.4.8)"),
 (lit("has none of it; the tool exits 2 there naming that page. The map also needs `ilspycmd` on PATH."), r"""has none of it; the tool exits 2 there naming that page. The map also needs `ilspycmd` on PATH.
The engine-method map and `tools/native_build_match.py` need only capstone and `ilspycmd`, so they run
on the laptop; set `TAOM_GHIDRA_PROJECTS` there, because the default project and cache folder is the
desktop's `E:\ghidra\TAOM` and the laptop has no E: drive."""),
 (r"- Opt-in integration \(`GHIDRA_INSTALL_DIR` and `TAOM_GHIDRA_IT=1`.*?About 30 s for all six\.", r"""- Opt-in integration (`TAOM_GHIDRA_IT=1`): the installed-engine test in `test_native_engine_methods.py`
  needs only the install, no Ghidra. It asserts a complete map on any build, then the addresses and counts
  of its build's row (v1.4.8: 686 + 1,542 + 33 methods; `get_current_action_type` `0x6F5DC0`,
  `set_attack_state` `0x6F3AE0`, `set_scripted_target_entity` `0x6F6950`). The Ghidra-backed tests also
  need `GHIDRA_INSTALL_DIR`: Ghidra's entries equal `.pdata` starts for three functions; a lone `.gpr`
  exits 2; and the per-build `PINS` (no v1.4.8 row yet; the candidates are in the test file's comment)
  decompile `IMBAgent.SetAttackState` at its address, lead `IMono_MBAgent::weapon_equipped` to
  `IMBAgent_WeaponEquipped`, follow a thunked engine method, survive an import stub, and decompile
  inside a function only seeding created."""),
 (lit("## Changelog\n\n"), "## Changelog\n\n- 2026-11 (1.4.x line): ported by plan 137; `$TAOM_GHIDRA_PROJECTS` names the project and cache\n  folder on a machine without E:, and the engine-method map was checked on v1.4.8 (2,261 methods, no\n  one-sided id).\n"),
 (lit("- **Status:** Open"), "- **Status:** Open (the 1.5.x work); the 1.4.x port is tracked by plan 137's issue"),
])
EOF
```

   - `git show 719074072:docs/reviews/adopt-ghidra-hindsight-2026-09-26.md > docs/reviews/adopt-ghidra-hindsight-2026-09-26.md`,
     then insert after its first line (`# Adoption Review: Ghidra and Hindsight`) a blank line and
     `> Ported to the 1.4.x line with tools/native_decompile.py by plan 137 (2026-11). Ghidra is installed on the desktop only.`
   - `docs/reference/provenance-register.md`: add the two table rows that `git show 719074072 -- docs/reference/provenance-register.md`
     adds (`| Ghidra | ...` and `| Hindsight | ...`) directly above `<!-- provenance-register-end -->`, and its
     two detail sections (`### Ghidra` and `### Hindsight`, with their paragraphs) directly above `## Adding a row`.
   - `docs/reference/development-machines.md`: after the table row that starts ``| `TAOM_PYBIN` |`` add:

```
| `GHIDRA_INSTALL_DIR` | Windows user env var, desktop only | `tools/native_decompile.py` and PyGhidra (see "Ghidra" below) |
| `TAOM_GHIDRA_PYTHON` | optional; unset on the desktop | `tools/native_decompile.py`: the interpreter that has PyGhidra, default `E:\Tools\ghidra-venv\Scripts\python.exe` |
| `TAOM_GHIDRA_PROJECTS` | Windows user env var on the laptop; the desktop uses the default | `tools/native_decompile.py`, `native_build_match.py` and `native_archive.py`: the folder for Ghidra projects, engine-method maps and matcher indexes, default `E:\ghidra\TAOM` |
```

     and insert directly above the line `## Writing docs and tools on either machine` the 14 lines
     `git show d51e43028:docs/reference/development-machines.md | sed -n '99,112p'` (they start
     `## Ghidra, desktop only (installed 2026-09-26, #688)` and end with a blank line), followed by this paragraph and a blank line:

```
On the laptop none of this is installed (checked 2026-11-04: `GHIDRA_INSTALL_DIR` unset, and neither
procdump nor WinDbgX on PATH), so `tools/native_decompile.py` exits 2 there naming this page and
`/native-crash-triage` falls back to hand-decoding the disassembly. The engine-method map and
`tools/native_build_match.py` need only capstone and `ilspycmd`, which the laptop has, so they run there
with `TAOM_GHIDRA_PROJECTS` set (verified 2026-11-04 on v1.4.8: 2,261 engine methods mapped with no
one-sided id, and a client-to-wEditor oracle with 0 wrong).
```

**Verify**:
- `for m in test_native_crash_triage_dump test_native_decompile test_native_engine_methods; do python -m unittest tools.tests.$m 2>&1 | tail -2; done`:
  `Ran 29 tests` OK, `Ran 63 tests` OK (Ghidra integration skipped), `Ran 34 tests` OK (integration skipped).
- `TAOM_GHIDRA_IT=1 python -m unittest tools.tests.test_native_engine_methods.InstalledEngineTests -v`: `Ran 1 test`, `OK`.
- `python tools/native_crash_triage.py --rva 0x6F5DC0; echo rc=$?`: prints the leaf-function message, then
  `decompile: python tools/native_decompile.py --rva 0x6F5DC0 --dll "..."`, `rc=1`.
  `python tools/native_crash_triage.py --rva 0x643720 | grep -E "crash function|decompile:"` shows
  `crash function: 0x643720 .. 0x64676B` and the `decompile:` line.
- `python tools/native_decompile.py --rva 0x643720; echo rc=$?` (no `GHIDRA_INSTALL_DIR`): stderr names
  `docs/reference/development-machines.md`, `rc=2`.
- Docs (`ghidra-native-decompile.md` and the adoption record are new files: no dash lines), Hooks.

**Commit B1** (stage the three tools, the three test modules, `docs/features/ghidra-native-decompile.md`,
`docs/reviews/adopt-ghidra-hindsight-2026-09-26.md`, `docs/reference/provenance-register.md`, `docs/reference/development-machines.md`):
`feat(tools): v2.0.28 - native decompile and engine-method map`. Body: crash triage prints the faulting
thread's registers from a minidump and ends every site with the command that decompiles it, also for a
leaf function with no unwind entry; `native_decompile.py` reads a crash site, an `[EngineMethod]` call or
a string's users as C through headless Ghidra (desktop); `native_engine_methods.py` joins the managed ids
with the native registration table (capstone, runs on the laptop: 2,261 methods on v1.4.8, none one-sided);
`TAOM_GHIDRA_PROJECTS` moves the project and cache folder off the desktop's E: drive. Trailers:
`Research: engine-method map, triage and decompile CLI run on the installed v1.4.8 client and wEditor DLLs`,
`Not-tested: the Ghidra-backed decompile (no Ghidra on the laptop)`, `Ported-from:`
`30abf550b7fa5613984bc3ec55cde80aa86f4ba1`, `719074072ab5777bd614cda70afc330f8346f1c1`, `d51e430287cf0d063bd07f006aea7c1cd1fc67a2`,
`f39b399b49e8d2249ca3933ece419c07823d5be4` (test files only).

### Step B2: Carry native addresses across builds and archive each build (`fc5408508`, `09030511a`, `e1dffb138`, `2838b25c2`)

Sources: `fc5408508` feat(tools): v2.0.35 - carry native addresses across engine builds; `09030511a` docs:
v2.0.35 - native build match doc shows its issue closed; `e1dffb138` feat(tools): v2.0.35 - each native
engine build is archived on its own; `2838b25c2` feat(tools): v2.0.35 - a new native build arrives with its
matcher scored. Simulation: 9, 1, 6 and 6 conflicted paths (absent files, skills, bookkeeping,
`session-start.sh`, `test_hooks.sh`). Method: end-state transplant (`native_build_match.py` and its test at
`fc5408508`, `native_archive.py`, its test and the feature doc at `2838b25c2`, the adoption record at
`fc5408508`). The session-start wiring is NOT ported (it needs the 1.5.x session-start shared deadline,
which WP103 deliberately left out); the archive runs by hand.

1. RED:

```bash
git show fc5408508:tools/tests/test_native_build_match.py > tools/tests/test_native_build_match.py && \
git show 2838b25c2:tools/tests/test_native_archive.py > tools/tests/test_native_archive.py && python - <<'EOF'
import sys; sys.path.insert(0, r"C:\Users\mikew\source\repos\taom-port\scratch\137"); from rx import edit, lit
edit("tools/tests/test_native_build_match.py", [
 (r'WEDITOR_DLL = \(r"E:\\Steam.*?TaleWorlds\.Native\.dll"\)\n', ""),
 (r"    def test_client_against_weditor_is_a_negative_control\(self\):.*?(?=\n\n\nif __name__)", r'''    def test_installed_client_against_installed_weditor_is_a_negative_control(self):
        """The installed client and wEditor builds of one version (another compile, where neighbour code
        collides): the carry may say AMBIGUOUS or UNKNOWN, never a wrong address."""
        game = os.environ.get("BANNERLORD_GAME_DIR", "").strip()
        if not game:
            self.skipTest("BANNERLORD_GAME_DIR is not set")
        client = Path(game, "bin", "Win64_Shipping_Client", "TaleWorlds.Native.dll")
        weditor = Path(game, "bin", "Win64_Shipping_wEditor", "TaleWorlds.Native.dll")
        for dll in (client, weditor):
            if not dll.is_file():
                self.skipTest(f"{dll} is not on this machine")
        with tempfile.TemporaryDirectory() as cache:
            maps = [bm.engine_map_for(dll, cache)[0] for dll in (client, weditor)]
            indexes = [bm.load_index(dll, cache, False) for dll in (client, weditor)]
        results = bm.match(indexes[0].rows, indexes[1].rows)
        report = bm.oracle(maps[0], maps[1], indexes[0], results)
        self.assertEqual(report.wrong, [])
        self.assertGreater(report.correct_hash + report.correct_strings, 100)'''),
])
EOF
for m in test_native_build_match test_native_archive; do python -m unittest tools.tests.$m 2>&1 | tail -2; done
```

   **Verify**: both error on import (`No module named 'native_build_match'` / `'native_archive'`).
2. GREEN: `git show fc5408508:tools/native_build_match.py > tools/native_build_match.py && git show 2838b25c2:tools/native_archive.py > tools/native_archive.py`.
3. Docs:
   - `git show 2838b25c2:docs/features/native-build-match.md > docs/features/native-build-match.md`, then:

```bash
python - <<'EOF'
import sys; sys.path.insert(0, r"C:\Users\mikew\source\repos\taom-port\scratch\137"); from rx import edit, lit
edit("docs/features/native-build-match.md", [
 (r"- \*\*The need:\*\* the session-start hook archives each new client.*?older build than the one installed\.", r"""- **The need:** `tools/native_archive.py` (#1004) keeps one copy of each client and wEditor DLL in
  `$TAOM_NATIVE_ARCHIVE` (default `E:\Decompiled_Bannerlord\_native`) with its engine-method map and this
  tool's index. On the 1.5.x line the session-start hook runs it; on the 1.4.x line it runs by hand
  (`/engine-bump` Phase 1). A player's crash report can come from an older build than the one installed."""),
 (lit("prints 5.0.7). No venv re-run is needed."), "prints 5.0.7). No venv re-run is needed.\nThe index and map cache defaults to the desktop's `E:\\ghidra\\TAOM`; on the laptop set\n`TAOM_GHIDRA_PROJECTS` or pass `--cache-dir`."),
 (r"- Opt-in integration \(`TAOM_NATIVE_MATCH_IT=1`.*?must also\n  give zero wrong\.", r"""- Opt-in integration (`TAOM_NATIVE_MATCH_IT=1`, skips when a DLL or its cached map is missing):
  the archived v1.5.4 to v1.5.5 and v1.5.3 to v1.5.4 pairs (1.5.x archives) must give zero wrong,
  and so must a negative control on whatever version is installed: the installed client against the
  installed wEditor DLL (another compile, where neighbour code collides). That control is the one
  that runs on the 1.4.x line."""),
 (r"\*\*Check a new pair of builds\.\*\* The session-start archive.*?By hand, run `--oracle` once per new build\.", r"""**Check a new pair of builds.** `python tools/native_archive.py` (no flags; on the 1.5.x line the
session-start hook runs it) archives a new client or wEditor build and runs `--oracle` against the
previous archived build of the same kind; `python tools/native_archive.py --status` shows the stored
result. By hand, run `--oracle` once per new build."""),
 (lit("| Units per binary |"), "| Negative control, v1.4.8 client to v1.4.8 wEditor (laptop, 2026-11-04, no cache, 109 s) | 2,043 methods: correct 149 (hash 146, strings 3), **wrong 0**, ambiguous 421, unknown 1,473 |\n| The same pair the other way, wEditor to client (laptop, 2026-11-04, no cache, 133 s) | 2,137 methods: correct 153 (hash 149, strings 4), **wrong 0**, ambiguous 413, unknown 1,571 |\n| Units per binary |"),
 (lit("- 2026-10-24: added (#997)."), "- 2026-11 (1.4.x line): ported by plan 137, with the archive run by hand and a negative control on the installed builds.\n- 2026-10-24: added (#997)."),
 (lit("- **Status:** Closed (merged in fc5408508)"), "- **Status:** Closed on the 1.5.x line (merged in fc5408508); on the 1.4.x line since plan 137"),
])
EOF
```

     The `Units per binary` anchor is the row directly after the v1.5.5 negative-control row, so the two v1.4.8 rows land after that row.
   - `git show fc5408508:docs/reviews/adopt-rea-2026-10-24.md > docs/reviews/adopt-rea-2026-10-24.md`; after its
     first line add a blank line and `> Ported to the 1.4.x line with tools/native_build_match.py by plan 137 (2026-11). The five rule lines this record mentions were not ported to this line.`
   - `docs/reference/provenance-register.md`: above `<!-- provenance-register-end -->` add
     ``| rea | `rea` `morluto/rea` `rea-agents` | MIT | behavioural-port | `tools/native_build_match.py` (the matching method, from its documentation; see detail) | cleared |``;
     above `## Adding a row` add the `### rea` section exactly as `git show fc5408508 -- docs/reference/provenance-register.md`
     adds it, but replace its sentence that starts `Five rule lines restate ideas` (through `(comparison-only).`)
     with `The rule lines the 1.5.x line also took from it are not on this line.`
   - `tools/native_sig_author.py`: in the module docstring, after the line that ends `values ship.` add the line
     `To carry an RVA from one engine build to another, use tools/native_build_match.py.`
   - `docs/reference/development-machines.md`: after the ``| `TAOM_GHIDRA_PROJECTS` |`` row add
     ``| `TAOM_NATIVE_ARCHIVE` | Windows user env var on the laptop (for example `C:\Decompiled_Bannerlord\_native`) | `tools/native_archive.py`: the native DLL archive, default `E:\Decompiled_Bannerlord\_native` |``.
   - `docs/features/ghidra-native-decompile.md` Key Files: after the row that starts ``| `tools/native_crash_triage.py` |`` add
     ``| `tools/native_build_match.py` | carries an RVA from an archived older build to the one being decompiled ([native-build-match.md](native-build-match.md)) |``.

**Verify**:
- `python -m unittest tools.tests.test_native_build_match tools.tests.test_native_archive 2>&1 | tail -2`: `Ran 108 tests`, `OK` (integration skipped).
- `TAOM_NATIVE_MATCH_IT=1 python -m unittest tools.tests.test_native_build_match.IntegrationTests -v`: the installed
  control passes (about 2 minutes); the two archive-pair tests skip (`... is not on this machine`). Record its time.
  Any WRONG match: STOP.
- `python tools/native_archive.py --status --archive-dir /c/Users/mikew/source/repos/taom-port/scratch/137/empty-archive`
  prints `native archive: nothing archived yet (...)`, exit 0 (it writes nothing).
- `grep -n "session-start" tools/native_archive.py | head -3` is informational only; no hook file is staged
  (`git diff --cached --name-only | grep -c "\.claude/hooks"` prints `0`).
- Docs, Hooks.

**Commit B2** (stage `tools/native_build_match.py tools/native_archive.py tools/native_sig_author.py tools/tests/test_native_build_match.py tools/tests/test_native_archive.py docs/features/native-build-match.md docs/features/ghidra-native-decompile.md docs/reviews/adopt-rea-2026-10-24.md docs/reference/provenance-register.md docs/reference/development-machines.md`):
`feat(tools): v2.0.28 - native build match and the native build archive`. Body: an RVA from an
archived build now carries to the installed one (`MATCHED ... (hash)`, or `AMBIGUOUS`/`UNKNOWN`, never
"removed"), scored by `--oracle` against both builds' engine-method maps; on v1.4.8 the client-to-wEditor
control makes 0 wrong matches. `native_archive.py` copies each client and wEditor DLL by SHA-256 with its map
and index; on this line it runs by hand during `/engine-bump`, not from the session-start hook. Trailers:
`Research: oracle runs on the installed v1.4.8 client and wEditor DLLs (2,043 and 2,137 methods, 0 wrong)`,
`Not-tested: a real old-to-new carry (this line has one engine build)`, `Ported-from:`
`fc5408508687c9f813123b6b1db006dd6ab0cee7`, `09030511ae4f34dab7ead3068b11d725770de554`,
`e1dffb13807448f4b8faf091e54f8c546b58ead6`, `2838b25c2f6cd2677425212aca6cee5b8e2226fb`.

### Step B3: Research, review and agents read native code as C (wiring of `d51e43028`)

Method: a path-limited 3-way merge of `d51e43028` onto HEAD (equivalent to picking only these paths), then
three hand edits. The 12 paths below merged cleanly onto `1b00d1acd` in the planning simulation with these
added/deleted counts: `.agents/skills/taom-research/SKILL.md` 3/0, `.claude/agents/debugger.md` 1/0,
`.claude/agents/error-detective.md` 3/0, `.claude/agents/taleworlds-researcher.md` 7/0,
`.claude/skills/investigate/SKILL.md` 6/0, `.claude/skills/new-creature-mount/SKILL.md` 3/1,
`.claude/skills/research/SKILL.md` 11/0, `docs/ai-includes/agent-operating-manual.md` 2/0,
`docs/ai-includes/taleworlds-research-guide.md` 24/0, `docs/reference/bannerlord-engine-and-toolchain.md` 15/2,
`docs/reference/doc-lookup.md` 1/0, `docs/reference/mcp-servers.md` 1/0. None of the added lines has a long dash.

1. Merge and take the 12 paths:

```bash
T=$(git merge-tree --write-tree --no-messages --merge-base=d51e43028^ HEAD d51e43028 | head -1); echo "$T"
for p in .agents/skills/taom-research/SKILL.md .claude/agents/debugger.md .claude/agents/error-detective.md .claude/agents/taleworlds-researcher.md .claude/skills/investigate/SKILL.md .claude/skills/new-creature-mount/SKILL.md .claude/skills/research/SKILL.md docs/ai-includes/agent-operating-manual.md docs/ai-includes/taleworlds-research-guide.md docs/reference/bannerlord-engine-and-toolchain.md docs/reference/doc-lookup.md docs/reference/mcp-servers.md; do git show "$T:$p" > "$p"; done
grep -l "^<<<<<<<" .agents/skills/taom-research/SKILL.md .claude/agents/*.md .claude/skills/investigate/SKILL.md .claude/skills/new-creature-mount/SKILL.md .claude/skills/research/SKILL.md docs/ai-includes/agent-operating-manual.md docs/ai-includes/taleworlds-research-guide.md docs/reference/bannerlord-engine-and-toolchain.md docs/reference/doc-lookup.md docs/reference/mcp-servers.md
git diff --numstat -- .agents .claude/agents .claude/skills/investigate .claude/skills/new-creature-mount .claude/skills/research docs/ai-includes/agent-operating-manual.md docs/ai-includes/taleworlds-research-guide.md docs/reference/bannerlord-engine-and-toolchain.md docs/reference/doc-lookup.md docs/reference/mcp-servers.md
```

   **Verify**: the `grep -l` prints nothing (no conflict markers; if a file has them, keep both sides, delete
   the markers, and say so in the hand-off). `git diff --numstat` shows the counts above, line endings aside.
2. Hand edits (new text has no long dash):
   - `docs/reference/bannerlord-engine-and-toolchain.md`: after the line that ends
     `instruction form it does not read; answers for that assembly are unverified until it reads every id.` add the bullet
     `- **On v1.4.8** (client \`TaleWorlds.Native.dll\`, 14,185,944 bytes, read 2026-11-04) the join maps all 2,261 methods with no one-sided id: 686 MountAndBlade (registration function \`0x643720\`), 1,542 Engine (\`0x391E90\`) and 33 DotNet (\`0x751540\`). An ASCII scan finds 52 of the 2,261 engine names inside \`IMono_\` assert strings.`
     (write the backticks plainly; the backslashes here only escape this plan's Markdown).
   - `docs/reference/doc-lookup.md`: in the row the merge added, replace ``crashes and hangs go through `/native-crash-triage` |`` with
     ``crashes and hangs go through `/native-crash-triage`; an address from an older build: [native-build-match.md](../features/native-build-match.md) |``.
   - `.ai/review-reference.md`: after the table row that starts ``| 2. **ILSpy MCP** |`` add
     ``| 3. **`python tools/native_decompile.py`** | `--engine-method <name>`, `--string <text>` or `--rva <offset>`: native engine code as C (Ghidra, desktop only) | When the managed trail ends at an `[EngineMethod]` or in natively parsed data. A native claim with no decompile behind it is UNVERIFIED |``.
   - `.claude/skills/deep-review/SKILL.md` (the 1.4.x skill has no lens files; this re-expresses `d51e43028`'s
     engine-lens sentence in the Agent 2 prompt):

```bash
python - <<'EOF'
import sys; sys.path.insert(0, r"C:\Users\mikew\source\repos\taom-port\scratch\137"); from rx import edit, lit
edit(".claude/skills/deep-review/SKILL.md", [(lit("\n\nOUTPUT FORMAT:\nFor each API usage:"), r"""
4. **NATIVE CLAIMS.** A claim in the change about native behaviour (below an `[EngineMethod]`, or how the engine parses action sets, monster usage or skins) is checked in the native code: `python tools/native_decompile.py --engine-method <name>` or `--string <text>` (Ghidra, desktop only). With no decompile behind it, report it UNVERIFIED, never verified from a method's name.

OUTPUT FORMAT:
For each API usage:""")])
EOF
```

**Verify**: Docs (no finding in these 14 files), Hooks, Config audit; `git diff --cached --name-only` (after staging) lists exactly the 14 files: the 12 paths of item 1 plus `.ai/review-reference.md` and `.claude/skills/deep-review/SKILL.md`.

**Commit B3** (stage those 14 files): `docs(skills): v2.0.28 - research and review read native code as C`. Body: `/research`,
`/investigate`, the researcher, debugger and error-detective agents, the research guide, the agent manual,
the lookup tables and the review reference now send a trail that ends at an `[EngineMethod]` or in natively
parsed data to `native_decompile.py`, and a native claim with no decompile behind it is reported
UNVERIFIED; the engine-and-toolchain page records the v1.4.8 engine-method numbers. The CLAUDE.md and
AGENTS.md lines of the same 1.5.x commit are left to the rules sync (WP143). Trailers:
`Research: v1.4.8 registration functions and counts from the engine-method map`, `Not-tested: none (docs)`,
`Ported-from: d51e430287cf0d063bd07f006aea7c1cd1fc67a2`, `Ported-from: fc5408508687c9f813123b6b1db006dd6ab0cee7` (doc-lookup clause).

### Step B4: Native crash triage gains the hang procedure; engine-bump archives and carries (`9c6d4962b`, skill text of `30abf550b`, `719074072`, `d51e43028`, `fc5408508`, `e1dffb138`, `2838b25c2`)

Simulation: `9c6d4962b` conflicts in the triage skill and a lessons file (the 1.4.x skill lacks the hang
section it edits). Method: hand edits; the hang section is copied from `9c6d4962b`'s blob (lines 125 to 153,
checked dash-free), so it already includes step 1 "Confirm the process is the game".

```bash
python - <<'EOF'
import sys, subprocess; sys.path.insert(0, r"C:\Users\mikew\source\repos\taom-port\scratch\137"); from rx import edit, lit
blob = subprocess.run(["git", "show", "9c6d4962b:.claude/skills/native-crash-triage/SKILL.md"], capture_output=True, encoding="utf-8").stdout.splitlines()
hang = "\n".join(blob[124:153]) + "\n\n"
assert hang.startswith("## Phase 2b: A hang") and "\u2014" not in hang and "\u2013" not in hang
hang = hang.replace("(Sysinternals, on PATH; about 11 GB)", "(Sysinternals, on the desktop's PATH, not on the laptop's as of 2026-11-04; about 11 GB)")
hang = hang.replace("   then find what should have set it.", "   then find what should have set it. Without Ghidra (the laptop), name each frame with\n   `python tools/native_crash_triage.py --rva 0x<off>` instead.")
T = ".claude/skills/native-crash-triage/SKILL.md"
edit(T, [
 (r"description: Root-cause native Bannerlord CTDs[^\n]*", "description: Root-cause native Bannerlord CTDs (AccessViolation in TaleWorlds.Native.dll) and hangs, via Event Log offsets, hang dumps, Ghidra decompile and live debugger forensics. No symbols needed."),
 (lit("(and `/investigate` may hand off here)."), "(and `/investigate` may hand off here).\nA hang (the game spins, nothing crashes) goes to Phase 2b."),
 (r"   exonerated\. Caveat: a crash held by a debugger never reaches WER \u2014 no Event Log entry\.", r"""   exonerated. Caveat: a crash held by a debugger never reaches WER: no Event Log entry. That
   includes Visual Studio (Detach All at the exception, never Stop, or the process dies unrecorded;
   its `$exception._ip` still gives the offset) and TaleWorlds' own `Watchdog.exe -p <pid>`
   (`bin\Win64_Shipping_Client\Watchdog\` on v1.4.8), which the launcher starts and which blocked
   procdump from attaching on the desktop (2026-09-26)."""),
 (lit("   dump-side module path and the local `--dll` it disassembles, so a mismatch is visible."), r"""   dump-side module path and the local `--dll` it disassembles, so a mismatch is visible.
   On a mismatch, pass `--dll` the archived DLL of the report's build (step 7), then carry the fault RVA
   to the installed build: `python tools/native_build_match.py --old <that DLL> --oracle` first
   (exit 1: do not carry on this pair; exit 2, no map for that build: the pair is unscored), then
   `--rva 0x<rva>`. Only `MATCHED ... (hash)` carries the exact offset, and it means identical code,
   not proven identity: decompile both addresses and compare before you name the site
   ([native-build-match.md](../../../docs/features/native-build-match.md))."""),
 (lit("   Copy both `TaleWorlds.Native.dll` files aside before the next update lands."), r"""   Archive both before the next update lands: `python tools/native_archive.py` copies each installed
   client and wEditor DLL by SHA-256 into `$TAOM_NATIVE_ARCHIVE` (default `E:\Decompiled_Bannerlord\_native`)
   with its engine-method map and matcher index, and `--status` lists what is saved. On the laptop set
   `TAOM_NATIVE_ARCHIVE` and `TAOM_GHIDRA_PROJECTS` first. A plain copy of both DLLs is the fallback."""),
 (lit("Hand-decode the few instructions around the\ncrash row (the tool shows them); the common patterns:"), r"""Then decompile
the function with `python tools/native_decompile.py --rva 0x<fault_offset>` (the triage output ends
with the exact line, also when the RVA is in a leaf function that has no `.pdata` entry, which
triage cannot bound; `--callers 1` adds the callers' C; the first run on a new binary analyses it
for minutes, once: [ghidra-native-decompile.md](../../../docs/features/ghidra-native-decompile.md)).
Read the crash row against the C, and hand-decode the instructions only when Ghidra is absent (the
laptop has none). When the site implements a managed engine call, the output says which
(`engine method: IMBAgent.X = x`): that is the managed call TAOM can see, and `/research` can follow
it from there. The common patterns:"""),
 (lit("  a key. Fix: make the table TOTAL (see `feedback_engine_lookup_total_key_coverage` memory)"), r"""  a key. Fix: make the table TOTAL (see `feedback_engine_lookup_total_key_coverage` memory)
  **The missing key is usually still in a register:** `--dump` prints the faulting thread's registers
  (on the 1.5.x line the melee-table miss at `+0x6590B9` kept its clip index in `r9`). Then read the
  table's BUILDER before changing any data: xref the table global and name the owning class from its
  vtable's RTTI (`tools/native_sig_author.py xref` / `rtti`), then read the insert's condition in the
  builder's C (`native_decompile.py --rva <builder>`)."""),
 (r"\n(?=## Phase 3)", "\n" + hang),
])
E = ".claude/skills/engine-bump/SKILL.md"
edit(E, [
 (r"   Steam overwrites in place and nothing archives native modules.*?can no longer be triaged against a local binary\.", r"""   Steam overwrites in place, so **archive both builds**: `python tools/native_archive.py` (no flags)
   copies each installed client and wEditor `TaleWorlds.Native.dll` by SHA-256 into `$TAOM_NATIVE_ARCHIVE`
   (default `E:\Decompiled_Bannerlord\_native`; the laptop sets it) as
   `TaleWorlds.Native.v<ver>.{client,wEditor}.dll`, caches its engine-method map while that build's managed
   DLLs are still installed, pre-builds the matcher index, and scores `--oracle` against the previous
   archived build of the same kind. On the 1.5.x line the session-start hook runs it; on this line run it
   as soon as the update is noticed. Then `python tools/native_archive.py --status`: it shows saved state
   only, one row per install path, and does not re-check the installed DLL, so compare each row's `sha16`
   with the installed file
   (`python -c "import hashlib,sys;print(hashlib.sha256(open(sys.argv[1],'rb').read()).hexdigest()[:16])" <dll>`).
   The OLD pair is in the archive only if it was archived before the update (v1.4.8 build 119303 by the
   maintainer run of plan 137); if it is missing it is gone: record that and expect `oracle=unscored`. The
   cost of not having one is already booked: the open player report at `crashz/report.json`
   (`BannerlordVersion v1.4.7.117484`) can no longer be triaged against a local binary."""),
 (lit("\n\n## Phase 2"), r"""
4. **Warm the native decompiler, and read its map check.** Each new binary gets a fresh Ghidra project,
   so on a machine with Ghidra run `python tools/native_decompile.py --engine-method get_current_action_type`
   once for the client and once with `--dll` on the wEditor DLL: the first crash then decompiles in
   seconds, not minutes. Any `WARNING: engine-method map:` line means the map is incomplete for that
   binary, most likely because the registration sweep met an instruction form it does not read:
   `--engine-method` refuses that assembly and the project is not seeded until
   `tools/native_engine_methods.py` reads every id. Fix the sweep before trusting native answers for that
   build. Without Ghidra (the laptop) the same completeness check runs as
   `TAOM_GHIDRA_IT=1 python -m unittest tools.tests.test_native_engine_methods.InstalledEngineTests`; its
   address and count pins need a row for the new build (v1.4.8: 686, 1,542 and 33 methods). Native
   offsets quoted in docs and RCAs belong to their old binary; re-derive one by engine method or string,
   not by offset ([ghidra-native-decompile.md](../../../docs/features/ghidra-native-decompile.md)), or carry
   it: `native_archive.py --status` shows the stored oracle result (`oracle=ok vs <old>`, or `oracle=wrong`
   with counts); without one, run `python tools/native_build_match.py --old <archived old client DLL> --oracle`
   once (exit 1, a wrong match: do not use it on this pair; exit 2, no map for the old build: the pair is
   unscored), then `--rva <old offset>`. Only `MATCHED ... (hash)` gives an exact offset, and it means
   identical code, not proven identity: read the new address with `native_decompile.py --rva` before it
   goes into a pin ([native-build-match.md](../../../docs/features/native-build-match.md)).

## Phase 2"""),
])
EOF
```

**Verify**:
- `grep -c "^## Phase 2b: A hang" .claude/skills/native-crash-triage/SKILL.md` prints `1`;
  `grep -c "Confirm the process is the game" .claude/skills/native-crash-triage/SKILL.md` prints `1`.
- `sed -n 's/^description: //p' .claude/skills/native-crash-triage/SKILL.md | wc -w` prints a number up to 30 (24 expected).
- `grep -c "^4\. \*\*Warm the native decompiler" .claude/skills/engine-bump/SKILL.md` prints `1`.
- Docs (no finding in either skill), Hooks, Config audit.

**Commit B4** (stage `.claude/skills/native-crash-triage/SKILL.md .claude/skills/engine-bump/SKILL.md`): `docs(skills): v2.0.28 - native crash triage gains the hang procedure`. Body: a hang (the
game spins, nothing crashes) now has a procedure: confirm the process is the game and not the Modding Kit,
spot the spinning thread, take a full dump, read the stack in WinDbgX, then read each native frame as C (or
name it with triage on the laptop). Triage decompiles the site and reads the faulting registers, carries an
RVA from an archived build, and archives builds with `native_archive.py`; `/engine-bump` archives, warms the
decompiler and checks the map. Trailers: `Research: v1.4.8 ships bin\Win64_Shipping_Client\Watchdog\Watchdog.exe and a wEditor Launcher.exe`,
`Not-tested: the hang procedure end to end (procdump and WinDbgX are not on the laptop)`, `Ported-from:`
`9c6d4962b9b72ff0401cebd2658b4bb5cd8e8f4d`, `d51e430287cf0d063bd07f006aea7c1cd1fc67a2`, `719074072ab5777bd614cda70afc330f8346f1c1`,
`30abf550b7fa5613984bc3ec55cde80aa86f4ba1`, `fc5408508687c9f813123b6b1db006dd6ab0cee7`, `e1dffb13807448f4b8faf091e54f8c546b58ead6`,
`2838b25c2f6cd2677425212aca6cee5b8e2226fb`.

### Part B done criteria

- [ ] Four commits with `v2.0.28 - ` subjects of at most 72 characters.
- [ ] Tool tests: `Ran` = TT0 + 12 + 212, no new failure; the two opt-in runs above passed (record their output).
- [ ] `git grep -n "native_decompile.py" -- .claude/skills/native-crash-triage/SKILL.md .claude/skills/research/SKILL.md .claude/skills/engine-bump/SKILL.md | wc -l` is at least 4.
- [ ] Build exit 0, Test `Total` = T0, `Failed: 0` (Part end).

---

## Part C: engine-bump checkers, and the construction guard that never ran

### Step C1: Shipped-reference checker and bound-member body diff (`4f9985852`, `4e408e0e0` tool halves)

Sources: `4f9985852` chore(engine): v2.0.34 - Bannerlord v1.5.5 (introduces both tools); `4e408e0e0`
fix(engine): v2.0.34 - v1.5.5 review fixes, dead mod guard removed (hardens them; their final shape).
Simulation: 19 and 27 conflicted paths (pins, SubModule.xml, impact docs, BUTR targets). Method: end-state
transplant from `4e408e0e0` (equal to the 1.5.x tip).

1. RED: `git show 4e408e0e0:tools/tests/test_check_engine_member_refs.py > tools/tests/test_check_engine_member_refs.py && git show 4e408e0e0:tools/tests/test_engine_body_diff.py > tools/tests/test_engine_body_diff.py && git show 4e408e0e0:tools/tests/fixtures/build_assemblies.ps1 > tools/tests/fixtures/build_assemblies.ps1 && git show f39b399b4:tools/tests/test_taom_src_short_names.py > tools/tests/test_taom_src_short_names.py`
   (the last restores the `ModuleOrder` class removed in A1; nothing else in that file differs). Run the three
   modules: they fail or error (scripts absent; `ModuleOrder` cannot read `engine_body_diff.py`).
2. GREEN: `git show 4e408e0e0:tools/check_engine_member_refs.ps1 > tools/check_engine_member_refs.ps1 && git show 4e408e0e0:tools/engine_body_diff.py > tools/engine_body_diff.py`.
3. Skill text (`.claude/skills/engine-bump/SKILL.md`): item 5 before Phase 2, and a paragraph at the end of Phase 3:

```bash
python - <<'EOF'
import sys; sys.path.insert(0, r"C:\Users\mikew\source\repos\taom-port\scratch\137"); from rx import edit, lit
edit(".claude/skills/engine-bump/SKILL.md", [
 (lit("\n\n## Phase 2"), r"""
5. **Resolve the shipped DLLs against the new engine, not just a fresh build.** A fresh compile binds
   whatever overload the new engine offers, so it hides a removed member that a released `TAOM.dll`
   still calls (on the 1.5.x line v1.5.4 removed a `TooltipProperty` constructor and every gate stayed
   green). Run `pwsh tools/check_engine_member_refs.ps1 -Dll <every shipped engine-referencing DLL, comma-joined>`:
   the `TAOM.dll` and `TAOM.Dependencies.dll` players run (the release package, or
   `<game>\Modules\{TAOM,TAOM.Dependencies}\bin\Win64_Shipping_Client\` when that is the public build).
   This line keeps no old reference assemblies, so there is no `-Baseline` and every miss counts as new
   (exit 1): a miss means that build needs a rebuild before players take the update. Negative control: a
   fresh build of HEAD (`TAOM.Tests/bin/Debug/net472/TAOM.dll`) must report `0 unresolved` (v1.4.8,
   2026-11-04: 3,267 refs for a build of `1b00d1acd`, 3,257 for the installed public build). The checker
   reads member and type references only, so it cannot see an override of a changed abstract member (the
   source compile of HEAD catches that) or an enum or const value the compiler baked into a caller (only a
   type diff shows it; a rebuild fixes it).

## Phase 2"""),
 (lit("\n\n## Phase 4"), r"""

**Then diff the bodies of every member TAOM binds.** A signature gate proves a bound member still exists,
not that its body still does what the patch assumed. Regenerate the API snapshot first (Phase 4 item 1's
generator, after a TAOM.Tests build) so new members are compared, then run
`python tools/engine_body_diff.py --old-suffix _v<OLD> --out <root>\_diff_<OLD>_to_<NEW>\bound_members`
(`--root` defaults to `E:\Decompiled_Bannerlord`; the laptop passes `--root C:\Decompiled_Bannerlord`). It
reads the bound members from `docs/reference/taleworlds-api-snapshot/{patch-targets,gamemodel-bases,reflection-sites}.md`
plus every `Deserialize(MBObjectManager` loader, writes one `.diff` per changed member, and prints
`rows_in_file=N parsed=N unparsed=N` per input. It exits 1 on any unparsed or unresolved row (a changed
row is information, not a failure) and 2 on bad input, such as the same `BuildInfo.GameVersion` on both
sides. Read every CHANGED row against the TAOM code that binds it, full-replacement GameModel overrides
first: an additive override inherits a body change, a replacement does not. A `TargetMethods()` patch
appears in the snapshot by its first target only, so diff its other targets by hand. (On 2026-11-04 the
tool parsed every row of this line's snapshot: 230, 108 and 47 rows, 0 unparsed.)

## Phase 4"""),
])
EOF
```

**Verify** (after a Build, so `TAOM.Tests/bin/Debug/net472/TAOM.dll` exists):
- `python -m unittest tools.tests.test_check_engine_member_refs tools.tests.test_engine_body_diff tools.tests.test_taom_src_short_names 2>&1 | tail -2`:
  `Ran 60 tests`, `OK`; the only expected skip is `test_old_engine_reports_missing_members` (no
  `1.5.3.122374-beta` reference assemblies here) plus the taom-src skips of A1. If the fixture tests fail
  because pwsh cannot load `Microsoft.CodeAnalysis.CSharp`, report it as an environment failure.
- `RealEngineTests.test_fresh_build_resolves_against_installed_engine` passed (shown with `-v`), and
  `pwsh -NoProfile -File tools/check_engine_member_refs.ps1 -Dll "$BANNERLORD_GAME_DIR/Modules/TAOM/bin/Win64_Shipping_Client/TAOM.dll"`
  prints `checked N distinct engine refs, 0 unresolved (0 new)` (record N; 3,257 in planning). A miss: STOP.
- Docs, Hooks, Config audit.

**Commit C1** (stage `tools/check_engine_member_refs.ps1 tools/engine_body_diff.py tools/tests/test_check_engine_member_refs.py tools/tests/test_engine_body_diff.py tools/tests/fixtures/build_assemblies.ps1 tools/tests/test_taom_src_short_names.py .claude/skills/engine-bump/SKILL.md`): `feat(tools): v2.0.28 - engine bump checks shipped refs and bound bodies`. Body: two engine-bump
tools: `check_engine_member_refs.ps1` resolves every engine member and type a shipped DLL references
against the installed engine (no assembly loading; the public build resolves all 3,257 on v1.4.8), and
`engine_body_diff.py` diffs the body of every member the API snapshot says TAOM binds, old decompile
against new, reading engine code only from vanilla modules. `/engine-bump` Phase 1 item 5 and Phase 3
call them. Trailers: `Research: both tools run read-only on v1.4.8 (0 unresolved; 385 snapshot rows parsed, 0 unparsed)`,
`Not-tested: a real body diff (no older decompile on the laptop)`, `Ported-from:`
`4f9985852086d5498e0a1c0357f8371a0a521dcf`, `4e408e0e07b93b72ab40bb8aa75bf7148bd7722a`.

### Step C2: Delete `SubModuleConstructionGuard` and gate the two Dependencies probes (C# half of `4e408e0e0`)

Simulation: `4e408e0e0` conflicts in `CoopPresence.cs` and `SubModuleConstructionGuard.cs` (1.5.x had first
trimmed the guard for v1.5.5) and in `ReflectionSiteBindingTests.cs`. Method: hand edits; every engine
claim is rewritten for v1.4.8 (v1.4.8 does NOT catch a SubModule constructor exception, unlike v1.5.5).

1. Pin the bindings first. In `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`, directly above the line
   `    public void ReflectionSite_ResolvesAgainstInstalledEngine(`, add (fill each `<N>` with
   `grep -n 'GetMethod("GetActiveModules"' Dependencies/Foundation/IncompatibleModDetector.cs | cut -d: -f1` and
   `grep -n -m1 'GetMethod("FromParametersFile"' Dependencies/Foundation/VersionProbe.cs | cut -d: -f1`; 210 and 78 on `1b00d1acd`):

```csharp
    // --- Dependencies foundation probes, resolved by string through Type.GetMethod. A GetActiveModules
    // miss sends IncompatibleModDetector to a directory scan but makes CoopPresence read co-op as absent;
    // a FromParametersFile miss leaves the game version undetected.
    [DataRow("TaleWorlds.ModuleManager.ModuleHelper", "ModuleHelper", "GetActiveModules", "Method", "IncompatibleModDetector.cs:<N>")]
    [DataRow("TaleWorlds.Library.ApplicationVersion", "ApplicationVersion", "FromParametersFile", "Method", "VersionProbe.cs:<N>")]
```

   In `docs/reference/taleworlds-api-snapshot/reflection-sites.md`: add as the last two rows of the
   Category B table (above the blank line before its first `Status (` line):
   ``| `TaleWorlds.ModuleManager.ModuleHelper` | `GetActiveModules` | method (public static) | `IncompatibleModDetector.cs:<N>` | TAOM.Dependencies reads the launcher's active-module list by name (`CoopPresence` uses the same read). Missing: `IncompatibleModDetector` falls back to a directory scan, but `CoopPresence` has no fallback and reads co-op as absent |``
   and ``| `TaleWorlds.Library.ApplicationVersion` | `FromParametersFile` | method (public static) | `VersionProbe.cs:<N>` | TAOM.Dependencies detects the game version through it. Missing: the version stays undetected and is logged as such; nothing crashes |``;
   above that first `Status (` line add
   ``Status (<commit date>): plan 137 adds two TAOM.Dependencies rows (`ModuleHelper.GetActiveModules`, `ApplicationVersion.FromParametersFile`); both resolve against installed v1.4.8 (`ModuleHelper.cs:196`, `ApplicationVersion.cs:40`, taom-src).``;
   and as the last row of the Category C table
   ``| `Dependencies/Foundation/IncompatibleModDetector.cs:<line of GetProperty("Id")>` | `moduleInfo.GetType().GetProperty("Id")` | `ModuleInfo.Id` is read off each element `ModuleHelper.GetActiveModules` returns (the method itself is a Category B row); the element type comes from the live list |``.
   **Verify**: Build; Filtered `ReflectionSiteBindingTests` `Failed: 0` with 2 more results than before
   (and with the strict settings if `STRICT`; WP122's label and catalogue tests, if present, pass).
2. Delete the guard and its mentions:

```bash
git rm -q Dependencies/Foundation/SubModuleConstructionGuard.cs && python - <<'EOF'
import sys; sys.path.insert(0, r"C:\Users\mikew\source\repos\taom-port\scratch\137"); from rx import edit, lit
edit("Dependencies/SubModule.cs", [
 (r"\n        try \{ DiagLog\.Log\(\"Dependencies\", \"OnSubModuleLoad: [^\n]*SubModuleConstructionGuard\.Install\(\); \}\n        catch \(Exception ex\) \{[^\n]*SubModuleConstructionGuard[^\n]*\n", "\n"),
 (lit("// CollectAssemblyTypesShim + SubModuleConstructionGuard + RunEarlyPhase were"), "// CollectAssemblyTypesShim + RunEarlyPhase were"),
])
edit("Dependencies/AliasStubSubModule.cs", [
 (r"/// BetaDeps\.Foundation\.AliasStubSubModule\. Future phases will append additional\n.*?/// ctor as those classes are implemented in Dependencies/Foundation/\.", "/// BetaDeps.Foundation.AliasStubSubModule. The ctor installs the assembly resolve\n/// handler and runs IncompatibleModDetector.RunEarlyPhase; the Harmony-based shields\n/// install later, from Dependencies/SubModule.OnSubModuleLoad."),
 (r"        // NOTE: CollectAssemblyTypesShim\.Install \+ SubModuleConstructionGuard\.Install\n.*?They run from", "        // NOTE: CollectAssemblyTypesShim.Install is intentionally NOT called here: it needs Harmony fully\n        // initialised, which isn't reliable at stub-ctor time. It runs from"),
])
edit("Dependencies/Foundation/ReflectionUtils.cs", [(r"/// VersionProbe, SubModuleConstructionGuard\)\. All methods are exception-safe \u2014 they", "/// VersionProbe). All methods are exception-safe: they")])
edit("Dependencies/Foundation/CoopPresence.cs", [(r"    // KNOWN GAP, deliberately not papered over:.*?\"Known limitations\"\.\n", r"""    // Detection reads the launcher's ACTIVE-module list. On Bannerlord v1.4.8 Module.AddSubModule does
    // not catch a SubModule constructor exception (Module.cs:141-153), so it leaves Module.LoadSubModules
    // before any OnSubModuleLoad runs (:1026-1103, InitializeSubModuleBases :201-208); nothing in TAOM can
    // swallow it. An active co-op module can still be unconstructed: LoadSubModules only shows a message
    // box for a missing SubModule DLL (:1082) and skips a SubModule its tags exclude (:1040).
    // See docs/features/bannerlord-together-compat.md "Known limitations".
""")])
edit("Dependencies/_Module/THIRD-PARTY-LICENSES.txt", [(lit("SubModuleConstructionGuard, CollectAssemblyTypesShim) are TAOM"), "CollectAssemblyTypesShim) are TAOM")])
EOF
```

   If a `Dependencies/` pattern stops (WP107 reworded it), make the same change by hand: remove the guard's
   `try`/`catch` pair and every comment mention, keep everything else.
3. Docs (rewrite each touched line without a long dash):
   - `docs/migration/dr3-maintenance.md`: on the line that starts `TAOM.Dependencies ships`, lower the class
     count by one (17 becomes 16 on `1b00d1acd`; it counts classes, not files, so never use a file count), change `Eleven came from DR3 Phase 4` to `Ten came from DR3 Phase 4`, and replace its
     ` \u2014 see` with `, see` (the long dash before `see`). On the roster line (starts `The DR3 Phase 4 roster:`)
     remove ``\`SubModuleConstructionGuard\`, ``, replace its long dash before `see the diagnostic-logs table` with a comma,
     and append ``The eleventh, `SubModuleConstructionGuard`, was deleted in 2026-11 (plan 137): it installed from `OnSubModuleLoad`, after the engine had constructed every SubModule, so it could never catch a construction failure.``
     Replace the bullet that starts ``- `SubModuleConstructionGuard` `` with
     ``- **Removed 2026-11 (plan 137):** `SubModuleConstructionGuard`, a Harmony Finalizer on `MBSubModuleBase` constructors and `Module.AddSubModule`. It never protected anything in a normal start: `Install()` ran from `OnSubModuleLoad`, after `Module.LoadSubModules` had constructed every SubModule (v1.4.8 `Module.cs:1026-1103`, `InitializeSubModuleBases` `:201-208`). On v1.4.8 `Module.AddSubModule` (`:141-153`) does not catch a constructor exception either, so a throwing third-party constructor ends the load as it always did.``
     Replace the line that starts `(No opt-out for` with
     ``(No opt-out for `CollectAssemblyTypesShim` / `IncompatibleModDetector`: these are detection-only or have no destructive effect.)``
   - `docs/features/bannerlord-together-compat.md`: replace the whole row that starts
     `| **Detection reflects what the player enabled, not what constructed** |` with
     ``| **Detection reflects what the player enabled, not what constructed** | `CoopPresence` reads the launcher's active-module list. A co-op module whose SubModule DLL is missing (the launcher shows a message box and goes on) or whose tags exclude it still reads as active, so TAOM spends the session in co-op mode (PatchShield withholding its rescue, SaveShield rethrowing save-load faults) for a co-op layer that never came up. A throwing SubModule constructor is not caught on v1.4.8 (`Module.AddSubModule`), so it ends the load instead | Check the log for the co-op mod's own init lines. Closing this needs a check that each active co-op module id has a constructed SubModule, plus a suppression set that survives re-probing |``
   - `docs/reference/provenance-register.md` BetaDeps row: replace ``FailedModsCatalog,SubModuleConstructionGuard,CollectAssemblyTypesShim}.cs` `` with
     ``FailedModsCatalog,CollectAssemblyTypesShim}.cs` (`SubModuleConstructionGuard.cs` removed 2026-11, plan 137) ``.

**Verify**: Build `0 Error(s)`; `git grep -n "SubModuleConstructionGuard" -- '*.cs'` prints nothing;
Test `Total` = previous + 2, `Failed: 0`; Binding gate `Failed: 0`; Docs (no finding in the three docs).

**Commit C2** (stage the deletion, the four `Dependencies/*.cs` files, `THIRD-PARTY-LICENSES.txt`, the test,
`reflection-sites.md`, the three docs): `fix(dependencies): v2.0.28 - delete the dead SubModule guard`. Body:
`SubModuleConstructionGuard` installed from `OnSubModuleLoad`, which v1.4.8 calls only after
`Module.LoadSubModules` has constructed every SubModule, so it could never catch a construction failure; the
class, its install and its comments are gone, and CoopPresence's note now states what v1.4.8 does (a
throwing constructor ends the load). Two Dependencies reflection probes become gate rows. This changes
TAOM.Dependencies.dll: ship it with TAOM from one build (#371). Trailers:
`Research: taom-src v1.4.8 Module.cs 141-153, 201-208, 236-243, 267, 1026-1103; ModuleHelper.cs:196; ApplicationVersion.cs:40`,
`Not-tested: a third-party SubModule constructor that throws (no such mod here)`, `Save-compat: none`,
`Ported-from: 4e408e0e07b93b72ab40bb8aa75bf7148bd7722a`.

### Part C done criteria

- [ ] Two commits with valid subjects; Tool tests `Ran` = previous + 48, no new failure.
- [ ] Build exit 0; Test `Total` = T0 + 2, `Failed: 0`; Binding gate `Failed: 0`.
- [ ] `test ! -e Dependencies/Foundation/SubModuleConstructionGuard.cs && echo gone` prints `gone`.

---

## Part D: a reproducible API snapshot and a correct catalogue (`4ab005dd1` plus verify report D4 WP-D)

### Step D1: `-Check` ignores the backlinks footer; regenerate the snapshot; README without hard counts

Source: `4ab005dd1` fix(tools): v2.0.35 - API snapshot check passes on the committed docs. Simulation: the
script hunk applies; `patch-targets.md` conflicts (the 1.5.x row comes from `7f3636b2e`, absent here), so
the doc is regenerated on 1.4.x instead.

1. RED: Build, then `pwsh -NoProfile -File tools/snapshot_api_surface.ps1 -Check; echo rc=$?` prints `DRIFT`
   lines and `rc=1` (the footer). Record whether only the footer differs.
2. `git cherry-pick -n 4ab005dd1`; for the conflicted doc run
   `git checkout --ours docs/reference/taleworlds-api-snapshot/patch-targets.md && git add docs/reference/taleworlds-api-snapshot/patch-targets.md`;
   if `CHANGELOG.md` is listed, the same. `git diff --cached --stat` must show `tools/snapshot_api_surface.ps1` only.
3. Generator wording (D4 G2: the generator uses `AccessTools`, which searches base types):

```bash
python - <<'EOF'
import sys; sys.path.insert(0, r"C:\Users\mikew\source\repos\taom-port\scratch\137"); from rx import edit, lit
edit("tools/snapshot_api_surface.ps1", [
 (lit("the engine method it patches (resolved the way Harmony resolves it)."), "the engine method it patches (resolved through HarmonyLib AccessTools, base types included)."),
 (lit("engine method it patches, resolved the way Harmony resolves it.\")"), "engine method it patches, resolved through HarmonyLib AccessTools (base types included).\")"),
])
EOF
```

4. Regenerate and restore the footers:

```bash
D=/c/Users/mikew/source/repos/taom-port/scratch/137
cp docs/reference/taleworlds-api-snapshot/patch-targets.md $D/patch-targets.before.md && cp docs/reference/taleworlds-api-snapshot/gamemodel-bases.md $D/gamemodel-bases.before.md
pwsh -NoProfile -File tools/snapshot_api_surface.ps1
python - <<'EOF'
D = r"C:\Users\mikew\source\repos\taom-port\scratch\137"
for name in ("patch-targets.md", "gamemodel-bases.md"):
    path = "docs/reference/taleworlds-api-snapshot/" + name
    before = open(D + "\\" + name.replace(".md", ".before.md"), "rb").read().decode("utf-8").replace("\r\n", "\n")
    start = before.rfind("<!-- backlinks-start")
    if start == -1:
        continue
    footer = before[start:]
    text = open(path, "rb").read().decode("utf-8").replace("\r\n", "\n")
    if "<!-- backlinks-start" not in text:
        open(path, "wb").write((text + footer).encode("utf-8"))
        print("footer restored:", name)
EOF
pwsh -NoProfile -File tools/snapshot_api_surface.ps1 -Check; echo rc=$?
```

   **Verify**: two `OK: ... reproduces.` lines and `rc=0`. Then
   `python -c "import re;t=open('docs/reference/taleworlds-api-snapshot/patch-targets.md',encoding='utf-8').read();n=int(re.search(r'Patches: (\d+)',t).group(1));r=len(re.findall(r'(?m)^\| \x60TAOM',t));print(n,r,n==r)"` prints `N N True`;
   `for c in Patch37_CrashReport DecisionItemBaseVM_ExecuteDone_Patch KingdomDecisionsVM_RefreshWith_AutoResolved_Patch Patch88_LordPartyTemplate Patch88_SpawnLordPartyScope Patch88_InitializeLordPartyPropertiesScope; do grep -c "$c\`" docs/reference/taleworlds-api-snapshot/patch-targets.md; done` prints six `1`s;
   `grep -c -E "\(unresolved\)|\(indeterminate\)|returned null" docs/reference/taleworlds-api-snapshot/patch-targets.md` prints `0`.
   A row present in `patch-targets.before.md` that is gone now, or any unresolved marker: STOP.
5. Negative control: append ` X` to the first `| \`TAOM.` row of `patch-targets.md` with the Edit tool, run
   `-Check` (expect `rc=1`, a `DRIFT:` line and up to 20 `- committed :` / `+ generated :` lines), then undo
   the edit and confirm `-Check` gives `rc=0` again.
6. README (`docs/reference/taleworlds-api-snapshot/README.md`), counts out, wording right:

```bash
python - <<'EOF'
import os, sys; sys.path.insert(0, r"C:\Users\mikew\source\repos\taom-port\scratch\137"); from rx import edit, lit
faithful = os.path.exists("TAOM.Tests/Migration/HarmonyTargetResolver.cs")
gate = ("resolved and bound the way Lib.Harmony 2.4.2 applies it" if faithful else
        "resolved with HarmonyLib `AccessTools` (base types searched; Harmony itself resolves the declared type only)")
edit("docs/reference/taleworlds-api-snapshot/README.md", [
 (r"Auto-generated: each of the 220 `\[HarmonyPatch\]` / `TargetMethod` classes and the engine method it patches, resolved as Harmony resolves it\.",
  "Auto-generated: each `[HarmonyPatch]` / `TargetMethod` class and the engine method it patches, resolved through HarmonyLib `AccessTools` (which also searches base types). The file's `Patches:` header holds the current count. A `TargetMethods()` class is recorded by its first target only."),
 (r"All 220 `\[HarmonyPatch\]` / `TargetMethod` targets, resolved as Harmony does at `PatchAll`\.",
  "Every `[HarmonyPatch]` / `TargetMethod` target, " + gate + " (count: the `patch-targets.md` header)."),
 (r"All 46 GameModels: registered in `SubModule\.cs`, override [^ ]+ base virtual, no shadow-without-`override`\.",
  "Every GameModel (count: the `gamemodel-bases.md` header): registered with `AddModel`, overrides at least one base virtual, no shadow without `override`."),
 (lit("The 32 auxiliary static-engine reflection members from `reflection-sites.md` Category B."),
  "The auxiliary static-engine reflection members from `reflection-sites.md` Category B, one `[DataRow]` each."),
 (lit("- CI/local reproducibility check: `pwsh tools/snapshot_api_surface.ps1 -Check` (exits non-zero if the committed files don't reproduce)."),
  "- Local reproducibility check: `pwsh tools/snapshot_api_surface.ps1 -Check` (exits non-zero if the committed files don't reproduce; it ignores the backlinks footer and prints up to 20 differing lines)."),
])
edit(".claude/skills/engine-bump/SKILL.md", [(r"and `-Check` reports \*\*DRIFT against the committed files by design\*\*.*?the API surface genuinely moved\.", r"""and `-Check` exits 1 with DRIFT, printing up to 20 differing lines, when a patch target or GameModel
   base moved, which after a bump is expected until you regenerate. It ignores the backlinks footer that
   `tools/build_backlinks.py` maintains.""")])
EOF
```

   Then, with the Edit tool, insert directly after the line that starts `- **Known tooling conflict:**` in
   `docs/migration/v1.4.8-impact.md` the line
   ``  - **Resolved** 2026-11 by the port of `4ab005dd1` (plan 137): `-Check` drops the backlinks footer before comparing, so it exits 0 on the committed files and prints the differing lines on real drift.``
   If a README pattern stops because WP122 or another WP already reworded that row, leave the row alone
   when it already has no hard count; otherwise apply the same meaning by hand.

**Verify**: `-Check` `rc=0`; Build; Test `Total` unchanged, `Failed: 0`; Docs (no finding in the README,
the impact doc, the skill; the regenerated snapshot files may show only table-row changes); Hooks.

**Commit D1** (stage the script, both snapshot files, the README, the skill, the impact doc):
`fix(tools): v2.0.28 - API snapshot check passes; snapshot regenerated`. Body: `-Check` drops the backlinks
footer before comparing and prints the differing lines, so it exits 0 on committed files; the snapshot is
regenerated (header count equals the rows, six missing classes added) and the README points at each
file's header instead of hard-coded counts; the generator says how it resolves (AccessTools). Trailers:
`Research: regenerated against installed v1.4.8; -Check 0 on the committed files and 1 on an edited row`,
`Not-tested: none (tool and docs)`, `Ported-from: 4ab005dd10202c7b17d2bcacfd6a423a8245784e`.

### Step D2: Correct the manual-patch header and the reflection catalogue (D4 G4, G5)

1. `Main/ManualPatchApplicator.cs`: replace
   `/// <c>AccessTools</c> at apply time and fail-safes with a logged warning (vanilla behavior) when the`
   `/// engine method has moved. Extracted verbatim from <c>SubModule.OnGameInitializationFinished</c>` with
   `/// <c>AccessTools</c> at apply time and, when the target is not found, logs a warning and keeps`
   `/// vanilla behaviour. An exception thrown by <c>harmony.Patch</c> itself is not caught here; it`
   `/// propagates to the caller. Extracted verbatim from <c>SubModule.OnGameInitializationFinished</c>`.
   If the header was already corrected by another WP, skip this item.
2. `PathReuseCache` (only if Step 0 counted it): delete the two lines
   `    // --- EditorCacheRebuild path cache (PersistentPathCache.cs) ---` and the `TaleWorlds.Engine.PathReuseCache` `[DataRow]`
   from `ReflectionSiteBindingTests.cs`; delete the `TaleWorlds.Engine.PathReuseCache` row from Category B of
   `reflection-sites.md`; add as the last row of Category D
   ``| `EditorCacheRebuild/Caching/PersistentPathCache.cs:149` | `typeof(PathReuseCache).GetField("_store")` | `PathReuseCache` is TAOM's own type (`Main/Features/EditorCacheRebuild/Caching/PathReuseCache.cs`); until 2026-11 it was gated as `TaleWorlds.Engine.PathReuseCache`, a type that does not exist, and passed only through the simple-name fallback |``.
   If Step 0 counted 0 (WP127 deleted the scaffold), skip this item.

**Verify**: Build; Filtered `ReflectionSiteBindingTests` `Failed: 0` (one result fewer if item 2 ran);
Test `Total` = previous (minus 1 if item 2 ran); Docs.

**Commit D2** (stage `Main/ManualPatchApplicator.cs`, and if item 2 ran `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs docs/reference/taleworlds-api-snapshot/reflection-sites.md`): `docs(bindings): v2.0.28 - manual patch header, reflection catalogue`. Body: the
ManualPatchApplicator header no longer claims every manual patch fails safe (only a missing target does),
and (if item 2 ran) the `PathReuseCache` row moves from the engine gate to the TAOM-internal catalogue,
since that type is TAOM's own. Trailers: `Research: verify report D4 G4, G5`, `Not-tested: none (comment and catalogue)`.

### Step D3: `/verify` and `.ai/verification.md` run the snapshot check (`6ae924638`, `0002cb561`, shared hunks)

Sources: `6ae924638` "Add tests for NRE containment finalizers and improve documentation" (Step 2d and the
output row of `.claude/skills/verify/SKILL.md`; the `api-snapshot` row it adds was lost in a 1.5.x merge);
`0002cb561` fix(crash-guards): v2.0.35 - crash guards contain only their own fault (restores that row with
the Debug-build wording, which is the wording taken). The rest of both commits is WP112's and its
disposition table's. Method: extract the text from those commits and insert it through text anchors, since
earlier WPs add other rows and steps to both files. These hunks wait for this step because the check they
describe cannot pass before D1, and Step 2d names `tools/engine_body_diff.py`, which C1 brings.

1. Precheck: Build, then `pwsh -NoProfile -File tools/snapshot_api_surface.ps1 -Check; echo rc=$?` prints
   `rc=0`. Otherwise STOP: D1 did not hold, and this step would ship a check that fails on arrival.
2. Insert the row, the step and the output line:

```bash
python - <<'EOF'
import re, subprocess, sys; sys.path.insert(0, r"C:\Users\mikew\source\repos\taom-port\scratch\137"); from rx import edit, lit
def show(spec):
    return subprocess.run(["git", "show", spec], capture_output=True, check=True).stdout.decode("utf-8").replace("\r\n", "\n")
row = re.search(r"^\| `api-snapshot` \|[^\n]*\n", show("0002cb561:.ai/verification.md"), flags=re.M).group(0)
step = re.search(r"^## Step 2d: API snapshot reproduces\n.*?(?=^## Step 3)", show("6ae924638:.claude/skills/verify/SKILL.md"), flags=re.M | re.S).group(0)
edit(".ai/verification.md", [(r"(?m)^(?=\| `moduledata` \|)", row)])
edit(".claude/skills/verify/SKILL.md", [
 (r"(?m)^(?=## Step 3: Git Status\n)", step),
 (lit("Tool tests: [N ran, F failed, S skipped]\n"), "Tool tests: [N ran, F failed, S skipped]\nAPI snapshot: [REPRODUCES/DRIFT/NOT RUN]\n"),
])
EOF
```

   If an anchor stops because an earlier WP reworded it, make the same insertion by hand: the row above the
   `moduledata` row, the step above Step 3, the output line after the tool-tests line.

**Verify**:
- `grep -c '^## Step 2d: API snapshot reproduces' .claude/skills/verify/SKILL.md` prints `1` (this is what
  WP138's K7 reads); `grep -c 'API snapshot: \[REPRODUCES/DRIFT/NOT RUN\]' .claude/skills/verify/SKILL.md` prints `1`;
  `grep -c '^| `api-snapshot` |' .ai/verification.md` prints `1`.
- `grep -c 'dotnet test TAOM.Tests .*--no-build' .claude/skills/verify/SKILL.md` prints `0`: Step 2d says Step 2
  built the test DLL, which holds only with WP103's `/verify` Step 2.
- `python -m unittest tools.tests.test_reviewctl tools.tests.test_ai_documentation 2>&1 | tail -1` shows no
  failure that Step 0's Tool tests list lacked.
- `git diff --cached --stat` after staging lists the two files only. Docs, Hooks.

**Commit D3** (stage `.ai/verification.md .claude/skills/verify/SKILL.md`):
`docs(verify): v2.0.28 - /verify and .ai run the API snapshot check`. Body: now that `-Check` exits 0 on the
committed snapshot, `/verify` Step 2d and the `api-snapshot` check in `.ai/verification.md` run it, so a patch
target or GameModel base that changes without a regenerated snapshot shows as DRIFT instead of being missed
by `engine_body_diff.py` at the next engine bump. Trailers:
`Research: -Check exits 0 on the regenerated snapshot; the script prefers TAOM.Tests/bin/Debug/net472`,
`Not-tested: none (docs and skill text)`, `Ported-from: 6ae924638ca76cd3296acc0aad70399fbfb721a3`,
`Ported-from: 0002cb5614e52fbdd638b402d302613f04678cf5`.

### Part D done criteria

- [ ] `pwsh -NoProfile -File tools/snapshot_api_surface.ps1 -Check` exits 0.
- [ ] `grep -c -E "Patches: |Models: " docs/reference/taleworlds-api-snapshot/{patch-targets,gamemodel-bases}.md` shows both headers (each file `1`); `grep -c -E "of the 220|All 220|All 46|The 32 auxiliary" docs/reference/taleworlds-api-snapshot/README.md` prints `0`.
- [ ] Build exit 0; Test `Failed: 0`; Binding gate `Failed: 0`.
- [ ] `grep -c '^## Step 2d: API snapshot reproduces' .claude/skills/verify/SKILL.md` and `grep -c '^| `api-snapshot` |' .ai/verification.md` each print `1`.

---

## Part E: hygiene (plan 005, the debug profile, the solution, TpacTool)

### Step E1: Faction-map helpers take paths as arguments; the credential sweep (`6b34fd00f`, `a0fa3cff2`, `01b970185`)

Closes the 1.4.x plan 005 the way 1.5.x did: item C (the injection) is fixed; item A is accepted with the
checklist line as the guard (1.5.x maintainer decision 52; this laptop has no `Dependencies/.vendor-source/`
at all, checked 2026-11-04); item B (MCP pins) is plan 016's, ported by WP142. Simulation: `6b34fd00f`
conflicts in `CHANGELOG.md` only; `a0fa3cff2` and `01b970185` in the checklist line and review records.

1. RED: `git show a0fa3cff2:tools/tests/test_process_faction_map.py > tools/tests/test_process_faction_map.py && python -m unittest tools.tests.test_process_faction_map -v`
   prints `FAILED` with the quote and Python-text cases failing and `test_plain_folder` passing.
2. GREEN: `git cherry-pick -n 6b34fd00f`; then `git checkout --ours CHANGELOG.md && git add CHANGELOG.md` if listed.
3. Replace the checklist line the pick added to `docs/ai-includes/external-repo-adoption.md` (it starts
   `   - Grep every vendored drop for inline package credentials`) with the final line:
   `git show 01b970185:docs/ai-includes/external-repo-adoption.md | grep "packageSourceCredentials"` (one line,
   which starts `   - Before porting any file from a vendored drop` and has no long dash).

**Verify**: `python -m unittest tools.tests.test_process_faction_map -v`: `Ran 3 tests`, `OK`;
`grep -n "r'{filepath}'\|r'{input_path}'\|r'{output_path}'" tools/process_faction_map.py` prints nothing (the `print(f"...{output_path}...")` lines elsewhere in the file are fine);
`python -m py_compile tools/process_faction_map.py` exits 0;
`grep -c "packageSourceCredentials" docs/ai-includes/external-repo-adoption.md` prints `1`;
`git diff --cached --stat` lists the tool, the test and the doc only. Docs.

**Commit E1** (stage `tools/process_faction_map.py tools/tests/test_process_faction_map.py docs/ai-includes/external-repo-adoption.md`): `fix(tools): v2.0.28 - faction-map helpers take paths as arguments`. Body: both child
`python -c` scripts read their paths and numbers from `sys.argv`, so a quote in a folder name no longer
breaks them or runs as code; the adoption checklist sweeps vendored drops, archives included, for inline
package credentials. Closes plan 005 on this line (item A accepted with this guard, item B is plan 016).
Trailers: `Research: none (stdlib tool)`, `Not-tested: the full faction-map pipeline on the real region art`,
`Ported-from:` `6b34fd00f4f1800a2c98aa3cf185100b8e7828be`, `a0fa3cff234b9cbe411e9825b5028844bb3c237e`, `01b9701852ae2387ae12397615f36175383a272e`.

### Step E2: Managed-only default launch profile (`37ff41e4a`)

`git cherry-pick -n 37ff41e4a`; `CHANGELOG.md` conflict: keep ours. **Verify**:
`python -c "import json;p=json.load(open('Main/Properties/launchSettings.json',encoding='utf-8-sig'))['profiles'];print(list(p));print([p[k].get('debugEngines') for k in p])"`
prints `['Bannerlord', 'Bannerlord (mixed native debugger)', 'Bannerlord (L5 no TAOM main)', 'BLSE Bannerlord']`
and `['managed-framework', 'managed-framework,native', 'managed-framework,native', None]`.
**Commit E2** (stage `Main/Properties/launchSettings.json`): `chore(debug): v2.0.28 - managed-only default launch profile`. Body: the `Bannerlord` profile
attaches only the managed debugger; `Bannerlord (mixed native debugger)` keeps managed plus native for
native crash work. The speed-up on Harmony patching (PatchShield pass 2 in `diag.log`) is UNVERIFIED and is
the maintainer's next Play to check. Trailers: `Not-tested: a Visual Studio launch`,
`Ported-from: 37ff41e4afb0b426aa874462e7b0de4c26b6afc6`.

### Step E3: The solution lists TAOM.Dependencies (`1118bc2e8`, adapted)

Simulation: `TAOM.sln` conflicts (1.5.x rows for TAOM.Client). Method: hand edit. Deviation, decided here:
map TAOM.Dependencies to `Any CPU` in every solution configuration, not to `x64` as 1.5.x does, so its build
platform and output folder stay what the `ProjectReference` produces today.

```bash
python - <<'EOF'
import sys; sys.path.insert(0, r"C:\Users\mikew\source\repos\taom-port\scratch\137"); from rx import edit, lit
G = "{957E5EB5-8B28-4AD8-AFC3-19773E784DA2}"
rows = "".join(f"\t\t{G}.{c}|{p}.{k} = {c}|Any CPU\n" for c in ("Debug", "Release") for p in ("Any CPU", "x64") for k in ("ActiveCfg", "Build.0"))
edit("TAOM.sln", [
 (lit('"TAOM.Tests", "TAOM.Tests\\TAOM.Tests.csproj", "{C9D3E4F5-6A7B-8C9D-0E1F-2A3B4C5D6E7F}"\nEndProject\n'),
  '"TAOM.Tests", "TAOM.Tests\\TAOM.Tests.csproj", "{C9D3E4F5-6A7B-8C9D-0E1F-2A3B4C5D6E7F}"\nEndProject\n'
  'Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Dependencies", "Dependencies", "{609F40C5-C560-40E6-76C7-691311A42DF0}"\nEndProject\n'
  'Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "TAOM.Dependencies", "Dependencies\\TAOM.Dependencies.csproj", "' + G + '"\nEndProject\n'),
 (lit("\t\t{C9D3E4F5-6A7B-8C9D-0E1F-2A3B4C5D6E7F}.Release|x64.Build.0 = Release|x64\n"), "\t\t{C9D3E4F5-6A7B-8C9D-0E1F-2A3B4C5D6E7F}.Release|x64.Build.0 = Release|x64\n" + rows),
 (lit("\t\t{C9D3E4F5-6A7B-8C9D-0E1F-2A3B4C5D6E7F} = {B8C72D15-7A8E-4F21-9C3D-A5D6E7F8B9C1}\n"), "\t\t{C9D3E4F5-6A7B-8C9D-0E1F-2A3B4C5D6E7F} = {B8C72D15-7A8E-4F21-9C3D-A5D6E7F8B9C1}\n\t\t" + G + " = {609F40C5-C560-40E6-76C7-691311A42DF0}\n"),
])
EOF
```

**Verify**: `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tee /c/Users/mikew/source/repos/taom-port/scratch/137/e3-build.log | tail -3`
shows `0 Error(s)`; `grep -c "TAOM.Dependencies -> " /c/Users/mikew/source/repos/taom-port/scratch/137/e3-build.log` prints `1`
and that line's path has no `\x64\`; no new warning compared with the Step 0 build; Test `Total` unchanged,
`Failed: 0`. Two `TAOM.Dependencies ->` lines, an `x64` path, a new MSB3270 warning or any test change:
`git checkout HEAD TAOM.sln`, skip E3 and record why.
**Commit E3** (stage `TAOM.sln`): `build: v2.0.28 - solution lists the TAOM.Dependencies project`. Body: Visual Studio now shows
and builds TAOM.Dependencies as a solution project instead of only through TAOM's reference; it builds as
Any CPU, as before. This does not explain E3's BuildStamp mismatch (the reference already built it).
Trailers: `Rejected: the 1.5.x x64 mapping (changes the Dependencies output folder for no gain)`,
`Not-tested: a Visual Studio solution rebuild`, `Ported-from: 1118bc2e8a930ab47fb53017f2c7b629607b6172`.

### Step E4: The tpac tools default to TpacTool v0.5.1 (`776054c0c`, three default lines)

Simulation: 33 conflicted paths (content, armour classes, Animalia, tools absent here). Method: hand edits.
1. In `tools/dump_engine_skeleton.ps1`, `tools/extract_human_anims_tpac.ps1` and `tools/read_anim_keyframes_tpac.ps1`
   replace `[string]$TpacToolBin = "E:\Bannerlord_Art\TpacTool_0.4.0\TpacTool\bin",` with
   `[string]$TpacToolBin = "E:\Release_v0.5.1\bin",` (one line each; `tools/Audit-MeshRefs.ps1` already has it).
2. `docs/reference/development-machines.md`: directly above `## Writing docs and tools on either machine` add
   the paragraph ``**TpacTool** (the PowerShell tpac tools) is set up on the desktop; the laptop has not been checked. They load ShortJake's TpacTool v0.5.1 (MIT, .NET Framework 4.6.2) from `E:\Release_v0.5.1\bin` by default (`-TpacToolBin`), since the old `E:\Bannerlord_Art\TpacTool_0.4.0` was found gone (2026-10-14, 1.5.x line). Unblock the folder once after a fresh download: the zip's Internet zone mark makes .NET Framework refuse the DLLs (`0x80131515` in Windows PowerShell 5.1; pwsh ignores it).`` and a blank line.
3. `docs/reference/provenance-register.md`: above `<!-- provenance-register-end -->` add
   ``| TpacTool (ShortJake fork) | `ShortJake/TpacTool` | MIT | interop-only | the `tools/*.ps1` tpac tools load its v0.5.1 `TpacTool.Lib.dll` at run time (`-TpacToolBin`); never shipped, nothing derived (see the TpacTool detail) | cleared |``;
   at the end of the `### TpacTool` detail section add
   ``Since 2026-10-14 the PowerShell tpac tools load `TpacTool.Lib.dll` from ShortJake's fork, https://github.com/ShortJake/TpacTool (MIT, a fork of hunharibo/TpacTool), release v0.5.1, through `-TpacToolBin`. That is `interop-only`: the library is loaded at run time on the desktop, never shipped, and no code is derived from it.``

**Verify**: `git grep -n "TpacTool_0.4.0" -- tools/*.ps1` prints nothing; Docs.
**Commit E4** (stage the three `.ps1` files, `docs/reference/development-machines.md docs/reference/provenance-register.md`): `chore(tools): v2.0.28 - tpac tools default to TpacTool v0.5.1`. Body: three tpac tools pointed at
a TpacTool 0.4.0 folder that no longer exists; they now default to the v0.5.1 fork `Audit-MeshRefs.ps1`
already used, and the fork has its provenance row. Desktop-only tools. Trailers:
`Not-tested: a run of the three tools with v0.5.1 (desktop; skeleton positions are Vector4 there)`,
`Ported-from: 776054c0cac5db933d9035ec412efb809a6fc226`.

### Part E done criteria

- [ ] Four commits (three if E3 was skipped, with the reason recorded); Tool tests `Ran` = previous + 3.
- [ ] Build exit 0; Test `Total` unchanged since Part D, `Failed: 0`.

---

## Disposition table (every commit in the pack, plus shared hunks and new work)

| Commit | Subject | Disposition | Step |
|---|---|---|---|
| `6b34fd00f` | fix(tools): faction-map helpers take paths as arguments | Ported (cherry-pick -n); checklist line replaced by its final form | E1 |
| `a0fa3cff2` | fix(tools): review follow-ups for plan 005 | Partial: test module; review record, RCA and three lessons skipped (1.5.x review records; lessons listed in Maintenance) | E1 |
| `01b970185` | fix(docs): convergence fixes for plan 005 | Partial: final checklist wording; record and count edits skipped | E1 |
| `337372d84` | docs(reviews): Codex prompt for plan 005 | Skipped: prompt record of the 1.5.x review | - |
| `37ff41e4a` | chore(debug): managed-only default launch profile | Ported (cherry-pick -n) | E2 |
| `719074072` | Add unit tests for animation clip generation and native decompilation | Transplanted in part: triage decompile hint, first decompiler (via the `d51e43028` end state), Ghidra doc, adoption record, provenance rows, dev-machines rows. Skipped: TrollBruteForce, hill-troll tools and tests, community guides and zip, armory-audit skill, `validate_moduledata.py` borrowed-body hunk, lessons (listed) | B1, B4 |
| `d51e43028` | feat(tools): read native engine calls and strings as C | Transplanted: tools, tests (v1.5.x pins replaced by a v1.4.8 row), hang procedure, wiring. Adapted: the lens sentence into the deep-review Agent 2 prompt. Deferred: CLAUDE.md and AGENTS.md lines (WP143), lessons and tools/README (listed) | B1, B3, B4 |
| `9c6d4962b` | docs(skills): hang triage confirms the process is the game | Ported (inside the hang section); lesson listed | B4 |
| `4f9985852` | chore(engine): Bannerlord v1.5.5 | Partial: the two tools and their skill steps; skipped: pins, `SubModule.xml`, impact doc, version text, 1.5.x test pins, dump path | C1 |
| `4e408e0e0` | fix(engine): v1.5.5 review fixes, dead mod guard removed | Partial: tool hardening and tests (end state), fixture, guard deletion, two reflection rows. Skipped: `GameReferences.targets` and its test, `SubModule.xml`, impact doc, native pins, and the handbook stale-manifest report: on this line both machines read the dump directly and there is no CI (D9), so the manifest is read by nobody, and a simulation on the laptop dump found 1 of 152 markers (`HorseComponent.Deserialize` helpers) that differs by decompiler build, a machine-dependent red | C1, C2 |
| `776054c0c` | Add clip measurement JSON files ... orc and goblin locomotion | Partial: three `-TpacToolBin` defaults, dev-machines paragraph, fork provenance row. Skipped: content, armour classes, locomotion tool, Animalia, gen_/wire_ tools, the tools/README paragraph (listed), the triage naming note (its sentence is not on this line) | E4 |
| `4ab005dd1` | fix(tools): API snapshot check passes on the committed docs | Ported (script); snapshot regenerated on 1.4.x instead of copying the 1.5.x doc | D1 |
| `1118bc2e8` | build: solution lists the TAOM.Dependencies project | Adapted: no TAOM.Client rows; Any CPU mapping instead of x64 | E3 |
| `fc5408508` | feat(tools): carry native addresses across engine builds | Transplanted: tool, test (plus a v1.4.8 negative control), feature doc, adoption record, provenance row, sig_author pointer, skill text, doc-lookup clause. Skipped: the adoption-precedents bullet (1.5.x removed it later); testing-qa lesson, feature-map and INDEX rows listed | B2, B3, B4 |
| `09030511a` | docs: native build match doc shows its issue closed | Folded into the doc's status line | B2 |
| `e1dffb138` | feat(tools): each native engine build is archived on its own | Partial: tool and test (end state); skipped: `session-start.sh` wiring and its `test_hooks.sh` rows (need the 1.5.x session-start shared deadline WP103 left out), hooks-catalog row | B2, B4 |
| `2838b25c2` | feat(tools): a new native build arrives with its matcher scored | Ported through the tool and test end state; hooks-catalog and README skipped | B2 |
| `857734c64` | fix(tools): taom-src takes short names, reads the game first | Transplanted (end state); docs adapted | A1, C1 |
| `f07d70a7c` | docs(reviews): RCA of the taom-src and banner review | Skipped: RCA addendum describes 1.5.x work; its lesson is listed for the lessons sync | - |
| `97219836c` | fix(tools): taom-src's cache follows the game build | Transplanted (end state) | A1 |
| `f39b399b4` | chore(engine): pins for the v1.5.6 hotfix, changeset 125270 | Partial: taom-src `-r` hunk, `decompile_bannerlord.ps1` UTF-8 hunk, the taom-src test; skipped: SkeletonBuffer and ClipBudget rows, v1.5.6 changeset pins | A1 |
| `fe20eddff` (shared) | docs: the hotfix, the taom-src cache and the leaderboard plan | Partial: the research-guide and mcp-servers cache lines; tools/README row and lesson listed; Long Defeat docs skipped | A1 |
| `30abf550b` (shared) | Add unit tests for various tools and functionalities | Partial: `native_crash_triage.py` register dump, its tests, the skill's register sentence and WER caveat; the rest belongs to WP107, WP108, WP116, WP130, WP143 | B1, B4 |
| `6ae924638` (shared) | Add tests for NRE containment finalizers and improve documentation | Partial: `/verify` Step 2d and its output row (the `.ai/verification.md` row comes from `0002cb561`, which restored it); the rest belongs to WP112, WP119, WP123, WP129 and WP142 or is skipped, per WP112's disposition | D3 |
| `0002cb561` (shared) | fix(crash-guards): crash guards contain only their own fault | Partial: the `api-snapshot` row of `.ai/verification.md` (Debug-build wording); the rest is WP112's | D3 |
| New (D4 WP-D) | snapshot refresh, README counts, generator wording, PathReuseCache row, ManualPatchApplicator header | New work | D1, D2 |
| New | `TAOM_GHIDRA_PROJECTS`; v1.4.8 engine-method pins; installed-build negative control; taom-src skill rewrite | New work | B1, B2, A1 |

## Test plan

- New tool tests (all from 1.5.x, with the edits named in each step): `test_taom_src_short_names.py` (13),
  `test_native_decompile.py` (63, three new `ProjectDirTests`), `test_native_engine_methods.py` (34),
  `test_native_build_match.py` (59, one replaced by the installed-build control), `test_native_archive.py`
  (49), `test_native_crash_triage_dump.py` (29, was 22), `test_check_engine_member_refs.py` (19),
  `test_engine_body_diff.py` (28), `test_process_faction_map.py` (3). Total `Ran` grows by 275.
- Opt-in runs on the laptop: `TAOM_GHIDRA_IT=1 ... InstalledEngineTests` (v1.4.8 row) and
  `TAOM_NATIVE_MATCH_IT=1 ... IntegrationTests` (installed control). Record both.
- C#: two new `ReflectionSiteBindingTests` rows (C2), minus one if D2 item 2 runs. No other C# test changes.
- Structural pattern: the 1.5.x test modules themselves; ProjectDirTests follows `EngineVersionTests` in
  the same file.
- Untestable here (for `Not-tested:`): Ghidra-backed decompile and the v1.4.8 PINS row (no Ghidra on the
  laptop); a real old-to-new carry and body diff (one engine build on this line); the hang procedure (no
  procdump or WinDbgX here); a Visual Studio launch; a third-party SubModule constructor that throws.

## Done criteria (whole WP)

- [ ] `git log --format=%s bannerlord-1.4.5..HEAD` lists 13 or 14 subjects, each with `v2.0.28 - ` (or the released version) and at most 72 characters; no commit touches `CHANGELOG.md` (`git diff --name-only bannerlord-1.4.5..HEAD | grep -c "^CHANGELOG.md$"` prints `0`).
- [ ] Build exit 0; Test `Failed: 0`, `Total` = T0 + 2 (or + 1 if D2 item 2 ran); Binding gate `Failed: 0`.
- [ ] Tool tests: `Ran` = TT0 + 275, no failure Step 0 lacked; Hooks no new failure; Config audit no new HIGH.
- [ ] `pwsh -NoProfile -File tools/snapshot_api_surface.ps1 -Check` exits 0.
- [ ] `pwsh -NoProfile -File tools/taom-src.ps1 path DefaultPartyWageModel` resolves into `v1.4.8-<stamp>`.
- [ ] `git diff --name-only bannerlord-1.4.5..HEAD | grep -E "^(Main/IoC.cs|Main/SubModule.cs|Main/TAOM.csproj|\.claude/settings|\.claude/hooks/|tools/README.md|CLAUDE.md|AGENTS.md|docs/reviews/lessons/)"` prints nothing.
- [ ] `git diff -U0 bannerlord-1.4.5..HEAD -- '*.md' | python -c "import sys;t=sys.stdin.buffer.read().decode('utf-8','replace').splitlines();print(sum(1 for l in t if l.startswith('+') and (chr(0x2014) in l or chr(0x2013) in l)))"` prints `0` (a long dash on an added line of any changed doc).

## STOP conditions

- Step 0: WP103 markers missing; any tool this plan creates already exists; a whole-file transplant target's
  blob differs from the value listed (for that step); the installed game is not v1.4.8 or the client DLL
  is not 14,185,944 bytes (recommend `/engine-bump`).
- `InstalledEngineTests` reports a map problem or counts other than 686, 1,542 and 33 (the binary moved or
  the sweep missed a form: report, do not edit the tool).
- The installed-build negative control reports any WRONG match.
- `check_engine_member_refs.ps1` reports an unresolved reference for the fresh build or the installed public build.
- After regeneration a row of the old `patch-targets.md` is gone, a row says `(unresolved)` or
  `(indeterminate)`, or `-Check` still reports DRIFT.
- A `Dependencies/` anchor is gone and the guard's install cannot be identified by hand; or any step seems
  to need `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `.claude/settings*.json`, a hook, or `Directory.Build.props`.
- A `Ported-from` source file differs from what the step says it equals (`git show <sha>:<path>` fails or
  the 1.5.x line rewrote history): report.
- A verification fails twice after a reasonable fix, or C# `Total` moves by anything other than the counts above.
- Another session is editing the same worktree or branch.

## Maintainer steps

1. **Issue**: the orchestrator creates the tracking issue for the new work before implementation lands;
   the port issues are #688, #997, #1004, #1038, #736 and (1.5.x) #668.
2. **Laptop environment** (Windows user variables; executors do not set them): `TAOM_NATIVE_ARCHIVE`, for
   example `C:\Decompiled_Bannerlord\_native`, and `TAOM_GHIDRA_PROJECTS`, for example `C:\Decompiled_Bannerlord\_ghidra`.
3. **One-time archive of v1.4.8 build 119303** (after Part B merges and step 2): `python tools/native_archive.py`,
   then `python tools/native_archive.py --status`. Expect two rows, `v1.4.8 client` with `sha16=2a5e0e0b15513ebb`
   and `v1.4.8 wEditor` with `sha16=b87f2439e0869945`, `map=ok index=ok`, `oracle=unscored` (no older build).
   This is the copy a future player crash from v1.4.8 is triaged against.
4. **Release**: Part C2 changes `TAOM.Dependencies.dll`; the next release (v2.4.0, D8) ships TAOM and
   TAOM.Dependencies from one build (#371).
5. **Optional**: install Ghidra on the laptop per `docs/reference/development-machines.md`, then run
   `TAOM_GHIDRA_IT=1 python -m unittest tools.tests.test_native_decompile -v` and add the v1.4.8 `PINS` row
   from the candidates in that file's comment.
6. **Index** (integrator, from the executor's hand-off; the executor does not edit the file): mark plan 005
   DONE in `plans/README.md` ("via plan 137: C fixed, A accepted with the checklist guard, B superseded by
   plan 016") and record this WP's row.
7. Codex review of this branch is the maintainer's call (it costs money); recommend `/deep-review` (C# in C2, D2) and `/security-scan` after Part B.

## In-game checks owed (maintainer)

1. **After a build with Part C2 runs the game**: `<game>\Modules\TAOM.Dependencies\diag.log` shows the
   `OnSubModuleLoad` shield sequence with `CollectAssemblyTypesShim.Install` followed by
   `PatchShield.Install (pass 1)` and no `SubModuleConstructionGuard` line; the main menu opens and a save loads.
2. **E2**: press Play in Visual Studio with the `Bannerlord` profile; the session attaches the managed
   debugger only; compare the PatchShield pass 2 duration in `diag.log` with an earlier mixed-mode run
   (the speed-up is UNVERIFIED).
3. **E4 (desktop)**: `pwsh tools/dump_engine_skeleton.ps1 -List` and one `extract_human_anims_tpac.ps1` run
   with TpacTool v0.5.1 (skeleton body positions are `Vector4` in v0.5.1; the extractor needs
   `win-x64\native\assimp.dll` in that folder, UNVERIFIED).
4. **The next freeze or hang report**: follow `/native-crash-triage` Phase 2b on the desktop (procdump and
   WinDbgX are there) and record whether the frames named the stuck loop.

## Maintenance notes

- **taom-src cache moved.** Every session after Part A reads `C:\Users\mikew\.taom-src\v1.4.8-202609061157`;
  file paths that plans and reports quote under `...\v1.4.8\` still open (the folder stays) but are no
  longer refreshed. A Steam hotfix of v1.4.8 now gets a fresh folder; the session-start drift banner still
  compares the version string only (batch 35 note), so that blind spot remains outside taom-src.
- **For WP138**: its member-level body-diff paragraph (`43fe5ebe8`) lands beside this plan's Phase 3
  paragraph: merge them, do not duplicate the `engine_body_diff.py` command. Its lens transplant supersedes
  this plan's deep-review Agent 2 item 4. Step D3 is what makes its condition K7 true (WP138 depends on this plan).
- **For WP142**: `04c3c4540`'s sentence that taom-src "needs a fully qualified name" is obsolete after Part A
  and must not be ported; its snapshot README count fix is done here; its tools/README rows for
  `engine_body_diff.py`, `check_engine_member_refs.ps1` and `native_archive.py` now apply (the tools exist),
  but its engine_body_diff row's "exit 1 when any row changed" is wrong: the tool exits 1 only on unparsed
  or unresolved rows.
- **For WP143**: the `d51e43028` CLAUDE.md routing change (`/native-crash-triage` also for "a hang") and the
  AGENTS.md "Research first" native clause are owed; 1.4.x `session-start.sh` still does not archive native
  DLLs, so WP143's note to keep the manual-copy advice holds (the skill now names `native_archive.py`).
- **tools/README.md rows owed** (to the sync WP): `native_decompile.py`, `native_engine_methods.py`,
  `native_build_match.py`, `native_archive.py` (manual run on this line), `engine_body_diff.py`,
  `check_engine_member_refs.ps1`, a `taom-src.ps1` row (short names, per-build cache, `-r`), and the
  `776054c0c` paragraph "Which TpacTool the PowerShell tools load". Feature-map and INDEX rows owed for
  `docs/features/ghidra-native-decompile.md` and `docs/features/native-build-match.md`.
- **Lessons owed** (all in `docs/reviews/lessons/build-tooling-workflow.md` unless noted), by source:
  `719074072` (three: the Store-Python JVM sandbox, the non-daemon JVM thread, Ghidra's CRLF in a test
  fake); `d51e43028` (auto-analysis misses registration-only functions); `9c6d4962b` (a "hung game" may be
  the Modding Kit); `4e408e0e0` (three: read engine types only from vanilla modules; "installed" is not
  "effective"; fail on unreadable input); `fc5408508` (`testing-qa.md`: an oracle over near-identical
  inputs proves little); `f07d70a7c` (a fast path goes in front of the path it replaces); `a0fa3cff2`
  (three: absence checks where the thing lives, code in a string is tested only by running it, documented
  patterns in `-E` form); `fe20eddff` (a cache keyed on the version string, rewritten without its
  `v1.5.6-impact.md` link).
- **Deferred by decision**: the archive's session-start wiring (needs the 1.5.x shared probe deadline; the
  line is pinned to one build, so the one-time run captures most of the value); the handbook stale-manifest
  report (see the disposition table); the Ghidra `PINS` row (maintainer step 5); D11 ShaderCacheKeeper is
  not part of this WP.
- **Reviewer focus**: the `CoopPresence.cs` and doc rewrites state v1.4.8 behaviour, not v1.5.5's; the
  regenerated snapshot only adds rows; `default_project_dir` is the one line that diverges from the 1.5.x
  `native_decompile.py`; the E3 solution mapping is `Any CPU`, unlike 1.5.x.
