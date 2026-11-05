# Plan 115: Make a 1.4.x release reproducible: build identity, a gated packager, paired Dependencies, a pinned TAOM_Map

> **Executor instructions**: Follow this plan step by step, in your own worktree (see "Git workflow").
> Run every verification command and confirm the expected result before moving on. If anything in
> "STOP conditions" occurs, stop and report; do not improvise. The plan has three Parts. Each Part ends
> with its own done criteria and is a clean hand-off point: Part A (C# build identity), Part B (the
> release gate), Part C (release process, notices, ledger, pairing check; needs Part B). Do not edit
> `plans/README.md`: the integrator owns it during this programme. Put the final counts in your hand-off.
>
> **Drift check (run first, in the worktree)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/Core/Diagnostics Main/Core/Logging/FileLogger.cs Main/Features/CrashReport Main/SubModule.cs Main/TAOM.csproj Dependencies/TAOM.Dependencies.csproj Main/_Module/SubModule.xml Main/_Module/THIRD-PARTY-LICENSES.txt Directory.Build.props tools/package_release.py tools/tests/test_package_release.py TAOM.Tests/Core TAOM.Tests/Features/CrashReport TAOM.Tests/Infrastructure/Dependencies TAOM.Tests/TAOM.Tests.csproj .claude/skills/release docs/reference/release-process.md docs/reference/provenance-register.md docs/features/crash-report.md docs/modding`
> Earlier WPs of this programme are expected to have changed several of these (SubModule.cs, the crash
> report files, THIRD-PARTY-LICENSES.txt, the release skill). Compare the live files with the excerpts in
> "Current state" and STOP only on a real mismatch. Three files must be untouched since `1b00d1acd`:
> `git diff 1b00d1acd -- tools/package_release.py tools/tests/test_package_release.py Directory.Build.props`
> prints nothing.

## Status

- **Priority**: P2. Release gate: this WP must be merged before the first player release that contains
  any wave-1 work (#371: TAOM and TAOM.Dependencies ship from one build).
- **Effort**: L (three Parts, 16 commits; plan on three executor sessions).
- **Risk**: MED. Two build-file edits (a maintainer-applied `Directory.Build.props` target, the Game
  Pass staging removal in both csproj files) and a stricter packager. A deploying build in an old
  checkout fails until its Game Pass staging folder is deleted (maintainer step M2).
- **Depends on**: `plans/107-crash-capture-and-patchshield.md`, `plans/108-missing-body-load-hang.md`,
  `plans/109-mission-start-and-skeleton-guards.md` (and through them every wave-0 WP, 101 to 105, and 106).
  WP108 supplies `tools/audit_armory_refs.py` and the two validator codes the step C5 release text runs.
- **Category**: dx (release tooling) with build-identity diagnostics.
- **Wave / lane**: wave 1, lane B.
- **Executor**: laptop.
- **Decisions applied**: D1 (no `CHANGELOG.md` staging; the commit body is the entry), D7 (no co-op or
  `TAOM.Client` parts), D8 (v2.4.0 numbering; packager and release docs refuse v2.0.29 to v2.0.36),
  D10, D11 (no ShaderCacheKeeper packaging step), D12 (the shader gate catches `taom_` shaders; no
  ShaderSourceInstall), D15 (`taom_body_length` is a reported pairing signal, never allowlisted), D19
  (git through the Bash tool only), D20 (laptop data caveat for the live smoke runs).
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04.
- **Issue**: port of #481, #371, #658, #448 and #717, #736, #968 and #969, #1010. New work (BuildStamp
  configuration tokens and commit-aware pairing, the release-number gate, the pairing check):
  orchestrator creates the issue before implementation lands.

## Why this matters

The public 1.4.8 build cannot be traced to a commit and was assembled by hand. E3 (Harad player) line 2:

```
[BuildStamp] TAOM=v2.0.0.0 build.20260930-120651Z+8df90444cc2f1e6d83bb95d2164ab764b4fcae65 TAOM.Dependencies=v0.1.0.0 build.20260914-032050Z+0c32343758068cdd60dcb92cd99412b00664dc6e MISMATCH — built 16d 08h 46m apart. These modules were not built together; update BOTH from the same release or expect the preview patches to fail (issue #371).
```

The 2026-09-30 refresh replaced the TAOM module only and kept a TAOM.Dependencies built on 2026-09-13.
Their sources were identical, so nothing broke this time, but wave 1 changes TAOM.Dependencies (WP107)
and the same hand assembly would ship the original #371 failure (patches fail to attach, characters in
bind pose). The time-only check cannot tell a benign carry-over from a real one. Every module reads the
hand-typed `v2.0.29.5` (E3 lines 26 and 35-42), which is in no commit and sorts after the 1.5.2-only tag
`v2.0.29`; the stamp has no dirty flag; crash bundles carry only `TaomVersion`; no log names its engine or
says whether the game exited (0 `[Engine]` or `[Shutdown]` lines in E3/E4). The public DLLs are Debug
builds with the JIT optimizer off, which is the recorded policy but invisible in logs. The laptop's
2026-09-30 `TAOM.zip` (a package of the 8df90444 build players run) ships retired `BehaviorTrees.dll` and
`BehaviorTreeWrapper.dll` and a Game Pass folder, and no 1.4.x step runs the packager. TAOM_Map and
LOTRLOME_Armory are unversioned: the public Armory carries the 1.5.x Monster attribute `taom_body_length`
(36 rgl errors in E2), the desktop's live TAOM_Map exported after 2026-10-29 carries 1.5.x ids, and the
laptop's TAOM_Map lacks all 15 villages the 1.4.x tool table defines.

After this plan: every log names its build, configuration, engine and ending, and the BuildStamp verdict
pairs DLLs by commit; crash bundles carry the build stamp and `diag.log`; `/release` rebuilds at the tag
and a dry run refuses a dirty, off-tag, mismatched, mis-numbered or polluted package; the TAOM_Map and
Armory pairing is checked and pinned in git.

## Current state

All excerpts are `origin/bannerlord-1.4.5` (`1b00d1acd`) unless marked. The public merge done by WP101
touches none of these files (`git diff --stat c5b84fb41 origin/fix/public-armory-ids-145` over them is empty).

**C# files**

- `Main/Core/Diagnostics/BuildStampReport.cs` (166 lines): the `[BuildStamp]` line. Time-only pairing:
  ```
  56  public static BuildPairVerdict Classify(DateTime a, DateTime b)
  81      case BuildPairVerdict.Mismatched:
  82          return $" MISMATCH — built {gap} apart. These modules were not built together; " +
  83                 "update BOTH from the same release or expect the preview patches to fail " +
  84                 "(issue #371).";
  131 public static string BuildReport(Assembly? main, Assembly? dependencies)
  146     return $"[BuildStamp] TAOM={mainVer} TAOM.Dependencies={depsVer}{verdict}";
  149 private static string ReadInformationalVersion(Assembly? asm)
  ```
  No `EngineReport`, no commit comparison, no configuration read.
- `Main/SubModule.cs` (single-owner, 1969 lines at the tip): `OnSubModuleLoad` logs the stamp at
  116-127 (`IoC.Resolve<IModLogger>().LogInfo(Core.Diagnostics.BuildStampReport.BuildReport(typeof(SubModule).Assembly, typeof(TAOM.Dependencies.Foundation.PatchShield).Assembly));` inside `try { } catch { }`),
  then the save-definer preflight comment. `protected override void OnSubModuleUnloaded()` at 1933 starts
  with `base.OnSubModuleUnloaded();` then the AppDomain hook unsubscribe. Earlier WPs add blocks; find
  these anchors by content, not line number.
- `Main/Core/Logging/FileLogger.cs:28`: `private const int DefaultRetainedLogs = 10;`
- Crash report: `Main/Features/CrashReport/Domain/IdentitySnapshot.cs` (6 positional fields, last
  `LanguageCode`), `Collectors/IdentityCollector.cs`, `Collectors/LogTailCollector.cs`,
  `Domain/LogTailSnapshot.cs` (4 fields), `CrashReportIoC.cs`, `CrashReportService.cs:183` (fallback
  `new IdentitySnapshot("?", "?", "?", "?", originatingPatchTarget, "?")`),
  `Rendering/CrashBundleWriter.cs`, `Rendering/PlainTextCrashReportRenderer.cs`. Exactly four
  `new IdentitySnapshot(` sites exist: IdentityCollector, CrashReportService:183,
  `CrashBundleWriterTests.cs:43`, `PlainTextCrashReportRendererTests.cs:139`.
- Every file c80006994 touches equals its 1.5.x parent except `Main/SubModule.cs`. It needs no
  Dependencies change (`RuntimeLog.Path` exists, `Dependencies/Foundation/RuntimeLog.cs:27`) and creates
  `LogTailCollectorTests.cs`, which uses only BCL types and `LogTailCollector`.

**Build files**

- `Directory.Build.props:23-24`: `<TaomBuildStamp ...>$([System.DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))Z</TaomBuildStamp>`
  and `<InformationalVersion>build.$(TaomBuildStamp)</InformationalVersion>`. The .NET SDK appends
  `+<HEAD sha>` (E3 shows it). No dirty marker. Config-protected: the maintainer applies its change.
  The plan 017 end state (`9ca99437f`) differs from the 1.4.x file only by one added target
  (`git diff origin/bannerlord-1.4.5 9ca99437f -- Directory.Build.props`).
- `Main/TAOM.csproj` (single-owner): no `Optimize` or `DebugType`, so builds are Debug.
  `CopyBinariesToModuleFolder` (146-158) stages `TAOM.dll` and `.pdb` into
  `_Module\bin\Gaming.Desktop.x64_Shipping_Client\` (148, 151-153) and `Win64_Shipping_Client`.
- `Dependencies/TAOM.Dependencies.csproj:75-82`: the same staging for TAOM.Dependencies (76, 78, 80 name
  Gaming.Desktop). These lines equal e6ee8c998's pre-image.

**Packager and its tests**

- `tools/package_release.py` (397 lines) and `tools/tests/test_package_release.py` (403 lines) are
  byte-identical to their state before plan 017 on 1.5.x (`git diff 5d1b1bda1^ origin/bannerlord-1.4.5`
  over them is empty). Plan 017 touched them only in 5d1b1bda1, db1a7166b and d5033e4e8; the series tip
  `9ca99437f` and e4c2c159a's parent hold identical copies.
- 1.4.x already excludes `AssetSources` (`classify`, :134-135), backups, scene backups, runtime state, VS
  state and `.pdb/.exp/.lib` under `bin/` (:142-143), and marks unknown top-level entries (a stray
  `ModuleData.zip`) UNKNOWN. It has no `--require-build`, no reports, no retired-binary, Game Pass,
  test-troop or shader-source refusal. `main()` order: plan, `_report`, list unknowns, `--json`,
  `--dry-run` return 0, refuse unknowns, copy.
- Helpers in the test file: `act`, `rule`, `_write(root, rel, size)` (NUL bytes), `_fixture(tmp)`
  (writes `TAOM/SubModule.xml` as 100 NUL bytes and an unknown `TAOM/Mystery/thing.bin`), `_run(*args)`.

**Module manifest and notices**

- `Main/_Module/SubModule.xml:10-33`: `DependedModules` lists Native, SandBoxCore, Sandbox, CustomBattle,
  then the #371 comment and `TAOM.Dependencies`; `DependedModuleMetadatas` lists Native (`v1.4.8.*`), the
  pin comment, TAOM.Dependencies (`v2.0.6`), then a 3-space-indented SandBoxCore line (31). The pin
  comment says "the DependedModule above" about an entry that is below it.
- `Main/_Module/THIRD-PARTY-LICENSES.txt` (117 lines at the tip): MinHook block 22-49, Scene scripts,
  warg assets, audio, fonts, NativeSkinFixes 107-110, `TAOM.dll and the remaining binaries` 114-117. No
  notice for `DryIoc.dll`, `Newtonsoft.Json.dll` or `System.Runtime.CompilerServices.Unsafe.dll`, which
  the laptop's `TAOM.zip` ships in every TAOM `bin` folder (listed 2026-11-04). WP109 adds a
  VanillaTuning section carrying the full MIT text (V03 line 392).
- `docs/reference/provenance-register.md`: row 73 `external developer drop` (UNKNOWN, uncleared), row 75
  `TransferbuttonMenu` (UNKNOWN, uncleared), sections at 290 and 317, end marker
  `<!-- provenance-register-end -->` at 88.

**Release process**

- `.claude/skills/release/SKILL.md` (117 lines at the tip): Phases 1-7, ends at tag and push; never
  builds at the tag, never runs the packager. WP102 (plan 020, `plans/102-changelog-at-release.md` edits
  E23 to E31) lands first: Phase 1 gains item 5 (the D8 numbering, pointing at release-process.md
  "Version numbers on the 1.4.x line"), Phase 4 generates `CHANGELOG.md`, Phase 5 is the release note,
  Phase 6 commits, Phase 7 tags by SHA.
- `docs/reference/release-process.md` (177 lines): "The three version fields", "Cutting a release",
  "Resolving a crash report to a commit", the backfill record. After WP102 (E44 to E46) its tag-and-push
  step is numbered 8 and a "**Version numbers on the 1.4.x line.**" paragraph closes "Cutting a release".

**Engine and BCL facts (verified this session)**

- taom-src v1.4.8 (`C:\Users\mikew\.taom-src\v1.4.8\`):
  - `TaleWorlds.ModuleManager.ModuleHelper.cs:35` `public static ModuleInfo GetModuleInfo(string moduleId)`
    (returns null for an unknown id); `:196` `public static List<ModuleInfo> GetActiveModules()`
    (`GetModules(x => x.IsActive)` over a dictionary).
  - `TaleWorlds.ModuleManager.ModuleInfo.cs:23` `public string Id { get; private set; }`;
    `:35` `public ApplicationVersion Version { get; private set; }`.
  - `TaleWorlds.Library.ApplicationVersion.cs:9` `public struct ApplicationVersion`; `:162`
    `public override string ToString()`.
- v1.4.8 dump: `ModuleHelper.GetSortedModules` (`C:\Decompiled_Bannerlord\_categories_v1.4.8\Platform\TaleWorlds.ModuleManager\TaleWorlds.ModuleManager\ModuleHelper.cs:271`,
  a `MBMath.TopologySort` over `DependedModules`) is called only from `TaleWorlds.MountAndBlade.Diamond`
  (`CustomBattleServer.cs:208`, `LobbyClient.cs:474`). The order of `<DependedModule>` entries therefore
  sets no single-player load order.
- .NET Framework 4.7.2 reference docs on this laptop (`Reference Assemblies\...\v4.7.2\mscorlib.xml`)
  declare `DebuggableAttribute.IsJITOptimizerDisabled`, `DebuggableAttribute.IsJITTrackingEnabled` and
  `AssemblyConfigurationAttribute.Configuration`. The 1.5.x runtime reader
  (`Main/Features/MissionPerf/Hooks/PerfContextReader.cs` on 1.5.x) uses
  `GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled ?? false`; this plan uses the same rule.

**Laptop facts (read 2026-11-04)**

- `dotnet --version` is 10.0.401 (the SDK plan 017 was verified on). No `global.json`. `core.autocrlf` is
  true. Python is `C:\Python314\python.exe`. `git --no-optional-locks status --porcelain
  --untracked-files=normal -- ... GameReferences.targets` exits 0 although that file does not exist on 1.4.x.
- The main checkout holds ignored Game Pass staging folders `Main/_Module/bin/Gaming.Desktop.x64_Shipping_Client/`
  and `Dependencies/_Module/bin/Gaming.Desktop.x64_Shipping_Client/` (`.gitignore:82` and `:48`).
- The installed TAOM (the 8df90444 build): `bin/Gaming.Desktop.x64_Shipping_Client` holds
  `BehaviorTreeWrapper.dll`; `bin/Win64_Shipping_wEditor` holds `BehaviorTrees.dll` and
  `BehaviorTreeWrapper.dll`. TAOM.Dependencies' `Win64_Shipping_Client` holds 42 files: the 36 the repo
  tracks under `Dependencies/_Module/bin` plus `TAOM.Dependencies.dll`, `.pdb`, `0Harmony.dll`,
  `Bannerlord.UIExtenderEx.dll`, `MCMv5.dll`, `System.Runtime.CompilerServices.Unsafe.dll`.
  `Main/_Module/bin` tracks 2 files (`MinHook.x64.dll`, `TAOM.NativeSkinFixes.dll`).
- Installed TAOM_Map `v2.0.27`, `ModuleData/settlements.xml` dated 2026-09-07: none of the 15 `VILLAGES`
  ids in `tools/add_map_villages.py` has a row; 159 hideouts, every bandit culture covered
  (dunland_raiders 19, umbar_corsairs 36, gundabad_raiders 20, harad_raiders 34, rhun_raiders 20,
  gondor_soldiers 10, erebor_warriors 10, mirkwood_stalkers 10). `recruitment_pools/gondor.json` names
  `village_EW10_3` and `village_EW11_1` to `_3`.
- Installed LOTRLOME_Armory `v2.0.26` (before WP101's D20 update): no Monster attribute outside the 147
  that `XmlSchemas/Monsters.xsd` declares; `Shaders/D3D11/shader_compile_report.log` names 10 shaders, all
  native, all with a `.rs` in `<game>/Shaders/Sources/`. Splitting its rows on runs of two spaces skips
  168 rows; a right-anchored row regex reads all 49,746 and their variants sum to the `Total variants:`
  trailer (13,262,228).
- No `taom_test_` id under `Main/_Module` (`git grep -l taom_test_ -- Main/_Module` is empty).
- Neither repo `SubModule.xml` holds a build-time token, and the installed `Modules/TAOM/SubModule.xml`
  equals `git show 8df90444c:Main/_Module/SubModule.xml` apart from line endings, so an installed manifest
  can be compared with the release commit's copy (B7).

**Conventions that bind this work**

- ADR-007: services use adapters, never sealed TaleWorlds types. The only engine reads here are in
  `SubModule.cs` (an entry point) and the existing `IdentityCollector`, which already reads `ModuleHelper`;
  no service gains a TaleWorlds type.
- ADR-002: entry points stay thin; the new `SubModule` blocks are logging one-liners in try/catch.
- ADR-003 no `#region`; ADR-004 no `[Obsolete]`; ADR-005 no `#if DEBUG` outside IoC registration.
- TDD: a failing test before every new behaviour (A3, A4, B4 to B7, C2 to C4).
- MCM json2 rule (rename a persisted setting, never flip its default): no setting changes here.
- `.claude/rules/provenance.md`: a redistributed binary needs a `THIRD-PARTY-LICENSES.txt` entry and a
  register row; a shipped notice states terms only when they are established.

## Commands you will need

| Purpose | Command (from the worktree root) | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (BHA0001 on Patch88 is a known false positive) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<A>\|FullyQualifiedName~<B>"` | 0 failed |
| Tool test, one file | `python -m unittest tools.tests.test_package_release` (or `test_ingame_ledger`, `test_check_release_pairing`) | `OK` |
| Tool tests, all | `python -m unittest discover -s tools/tests -t .` | no failure the step-0 baseline lacks |
| Docs | `python tools/lint_docs.py` | no new finding in touched files |
| Stamp of a built DLL | `python -c "import re,sys;d=open(sys.argv[1],'rb').read();print(sorted({m.group(0).decode() for m in re.finditer(rb'build\.\d{8}-\d{6}Z[+.](?:[0-9a-f]{40}\|nogit)(?:\.(?:dirty\|nogit))?',d)}))" Main/bin/Debug/net472/TAOM.dll` | one stamp |

Both `-p:` flags on every build and test: on 1.4.x `Main/TAOM.csproj:133-136` does not gate CopyModule
on DisableModuleCopy, so a build without `-p:ModuleId=` deploys into the game install. Never `./build.ps1`.
In Bash, drop the backslashes shown before `|` in the table (they are markdown escapes). In the steps,
"Filtered `A|B`" means `--filter "FullyQualifiedName~A|FullyQualifiedName~B"` with the Filtered command.

## Scope

**In scope** (the only files you modify):

- Part A: `Main/Core/Diagnostics/BuildStampReport.cs`, `Main/Core/Logging/FileLogger.cs`,
  `Main/Features/CrashReport/{CrashReportIoC.cs,CrashReportService.cs}`,
  `Main/Features/CrashReport/Collectors/{IdentityCollector.cs,LogTailCollector.cs}`,
  `Main/Features/CrashReport/Domain/{IdentitySnapshot.cs,LogTailSnapshot.cs}`,
  `Main/Features/CrashReport/Rendering/{CrashBundleWriter.cs,PlainTextCrashReportRenderer.cs}`,
  `Main/SubModule.cs` (**single-owner**: exactly two blocks, the `[Engine]` line and the `[Shutdown]`
  line), `TAOM.Tests/Core/Diagnostics/BuildStampReportTests.cs`, `TAOM.Tests/Core/Logging/FileLoggerTests.cs`,
  `TAOM.Tests/Features/CrashReport/{CrashBundleWriterTests.cs,LogTailCollectorTests.cs (new),PlainTextCrashReportRendererTests.cs}`,
  `TAOM.Tests/TAOM.Tests.csproj` (two references), `docs/features/crash-report.md`,
  `docs/reviews/rca-crash-log-readability-2026-08-19.md` (new, from c80006994).
- Part B: `Directory.Build.props` (**maintainer applies**, step B1), `tools/package_release.py`,
  `tools/tests/test_package_release.py`, `Main/TAOM.csproj` (**single-owner**: the
  `CopyBinariesToModuleFolder` target and one new target), `Dependencies/TAOM.Dependencies.csproj` (the
  same two places), `docs/modding/module-map.md`, `docs/modding/module-taom.md`, `docs/modding/modules-overview.md`.
- Part C: `Main/_Module/SubModule.xml`, `Main/_Module/THIRD-PARTY-LICENSES.txt`,
  `TAOM.Tests/Infrastructure/Dependencies/BundledDependencyManifestTests.cs`,
  `docs/reference/provenance-register.md`, `docs/features/companion-tactics.md`,
  `docs/ai-includes/external-repo-adoption.md`, `docs/reviews/adopt-agent-reach-2026-10-26.md` (new),
  `tools/ingame_ledger.py` (new), `tools/tests/test_ingame_ledger.py` (new),
  `tools/check_release_pairing.py` (new), `tools/tests/test_check_release_pairing.py` (new),
  `docs/features/moduledata-validation.md` (one paragraph), `.claude/skills/release/SKILL.md`,
  `docs/reference/release-process.md`.

**Out of scope** (do not touch):

- `Main/IoC.cs` (nothing here needs it), `.claude/settings*.json`, `docs/adrs/*`.
- `CHANGELOG.md` (D1), `docs/reviews/lessons/*`, `docs/reviews/LESSONS-LEARNED.md`,
  `docs/reviews/REVIEW-LOG.md`, `docs/INDEX.md`, `docs/reference/feature-map.md`, `tools/README.md`
  (WPs 142 to 144 sync them; Maintenance notes list the rows they owe).
- Any ModuleData XML, `Client/`, ShaderSourceInstall, ShaderCacheKeeper, the 1.5.x engine pins.
- `tools/release_pins.json`: the maintainer records it from the package the line ships (M3).
- The game install: read it, never write it. Never run a tool with `--apply` or `--record` on real data.

## Git workflow

- Worktree and branch (from the main checkout, through the Bash tool, D19):
  `git worktree add "C:/Users/mikew/source/repos/taom-port/115" -b port148/115-release-integrity bannerlord-1.4.5`.
  Work only there (`cd /c/Users/mikew/source/repos/taom-port/115 && ...` in each Bash call). Keep commit
  message files and temporary module trees in `C:/Users/mikew/source/repos/taom-port/115-scratch/`,
  outside the repo. Never push, open a PR, tag, or merge into `bannerlord-1.4.5`.
- Per-port procedure (binding):
  1. Apply a 1.5.x commit with `git cherry-pick -n <sha>`. Never let `cherry-pick`, `--continue`, `-C`,
     `-c` or `--amend --no-edit` create the commit (`GIT_EDITOR=true` here commits the 1.5.x message).
  2. Drop bookkeeping: for a conflicted path you do not keep, `git checkout --ours <path>` then
     `git add <path>`; for a cleanly changed one, `git checkout HEAD <path>`; for a file the pick adds and
     you do not keep, `git rm -q -f <path>`; for a file the pick modifies that does not exist here
     ("deleted by us"), `git rm -q <path>`. Never `git checkout --`, `git restore` or `git checkout .`
     (they trigger the confirm gate).
  3. Resolve code conflicts as the step says, keeping 1.4.x behaviour where told.
  4. Stage with explicit `git add <paths>`, then run `git commit -F <msgfile>` in the NEXT sequential Bash
     call. If `git status` still reports a cherry-pick in progress after the commit, run `git cherry-pick --quit`.
  5. After every commit: Build, then the step's filtered tests. At the end of each Part: full Test, full
     tool tests, Docs lint.
- Commit message (write it to a file in the scratch folder):
  ```
  <type>(<scope>): v2.0.28 - <description, at most 72 characters in all>

  <Body for 1.4.x readers, wrapped at 72: what changed on the 1.4.8 line
  and why; for a partial port, what was left out and why. Name the issue
  number (for example "Port of #658") so tools/ingame_ledger.py can see
  it. No em or en dashes. No Co-Authored-By or "Generated with" line.>

  Research: <what was verified on v1.4.8>
  Not-tested: <what needs an in-game check>
  Ported-from: <40-hex sha>          <- one line per included 1.5.x commit
  ```
  A trailer value that wraps continues on lines indented by one space.
- Before each commit that changes C# in two or more files (A1, A2, A4), run `/deep-review` if your
  session can invoke skills; otherwise list it as owed in the hand-off. Paid reviewers are not authorized.

## Steps

### Step 0: Set up and record baselines (no commit)

1. Create the worktree (above) and run the drift check.
2. Preconditions (each must hold, else STOP: a dependency WP is not merged):
   `test -f Dependencies/Foundation/RethrowStackPreserver.cs` (WP107);
   `test -f tools/audit_armory_refs.py` (WP108);
   `grep -c "VANILLATUNING" Main/_Module/THIRD-PARTY-LICENSES.txt` prints 1 or more (WP109);
   `test -f tools/changelog_from_commits.py` and
   `grep -c "Version numbers on the 1.4.x line" .claude/skills/release/SKILL.md docs/reference/release-process.md`
   prints 1 or more for each file (WP102).
3. `dotnet restore TAOM.sln`, Build, Test. Record the totals as the baseline. If they differ from the
   count the integrator recorded in the main checkout's `plans/README.md`, keep your measured count and report both.
4. `python -m unittest discover -s tools/tests -t .` and record any failures as the tool baseline.

**Verify**: every precondition holds; Build exit 0; Test 0 failed.

---

## Part A: build identity in logs and crash bundles

### Step A1: port c80006994 (`[Engine]` line, `[Shutdown]` line, 30 logs, diag.log in the bundle)

Method: `git cherry-pick -n c8000699480b59f24e42f2e992ad39b6be7fde26`.

Expected (merge-tree this session onto `1b00d1acd`): conflict only in `CHANGELOG.md`. On the real base,
`Main/SubModule.cs` may also conflict where earlier WPs added blocks; `CrashBundleWriterTests.cs` and
`PlainTextCrashReportRendererTests.cs` may conflict with WP107's test additions. Resolve:

- `CHANGELOG.md`: `git checkout --ours CHANGELOG.md` and `git add CHANGELOG.md`.
- `docs/reviews/lessons/build-tooling-workflow.md`: drop (procedure item 2).
- Keep `docs/reviews/rca-crash-log-readability-2026-08-19.md` (new) and `docs/features/crash-report.md`.
- `Main/SubModule.cs`: keep everything from both sides. The `[Engine]` try/catch block (it builds
  `"{mod.Id} v{mod.Version}"` descriptors from `ModuleHelper.GetActiveModules()` and logs
  `BuildStampReport.EngineReport(ModuleHelper.GetModuleInfo("Native")?.Version.ToString(), descriptors)`)
  goes immediately after the `[BuildStamp]` try/catch. The `[Shutdown]` line
  (`try { IoC.Resolve<IModLogger>().LogInfo("[Shutdown] clean module unload"); } catch { }` with its
  comment) goes immediately after `base.OnSubModuleUnloaded();`.
- Test files: two sets of added tests; keep both.

Tests that come with it (14): BuildStampReportTests +4 (EngineReport), FileLoggerTests +1,
CrashBundleWriterTests +3, LogTailCollectorTests +5 (new file), PlainTextCrashReportRendererTests +1.

Commit: `fix(diagnostics): v2.0.28 - a log names its engine and its ending (#481)`. Body: a log now names
the Bannerlord build and every active module with its version (so a player log states the TAOM_Map and
Armory versions too), a clean exit writes `[Shutdown] clean module unload` (a log ending without it means
the process died; E3 cannot tell), 30 logs are kept instead of 10, and the crash bundle carries
TAOM.Dependencies' `diag.log`. Trailers: `Research: taom-src ModuleHelper.GetModuleInfo (:35),
GetActiveModules (:196), ModuleInfo.Id (:23), ModuleInfo.Version (:35)`; `Not-tested: the [Engine] and
[Shutdown] lines in game`; `Ported-from: c8000699480b59f24e42f2e992ad39b6be7fde26`.

**Verify**: Build exit 0; Filtered with `BuildStampReportTests|FileLoggerTests|CrashBundleWriterTests|LogTailCollectorTests|PlainTextCrashReportRendererTests`
0 failed; `git grep -c "EngineReport(" -- Main/SubModule.cs` reports `Main/SubModule.cs:1`;
`git grep -c "\[Shutdown\] clean module unload" -- Main/SubModule.cs` reports `Main/SubModule.cs:1`;
`git grep -c "DefaultRetainedLogs = 30" -- Main/Core/Logging/FileLogger.cs` reports a count of 1.

### Step A2: port 743ce2e1d (the build stamp in crash bundles) with its plan 017 follow-ups

Method: `git cherry-pick -n 743ce2e1d892a312f8f2ff91a759ac91606eb23a`. Expected: clean after A1 (chain
simulation this session). If `PlainTextCrashReportRendererTests.cs` conflicts, it is two adjacent test
additions (WP107's last-segment tests and `Render_Identity_PrintsTheTaomBuildStamp`): keep both. A
conflict in `CrashReportService.cs` comes from WP107's nearby edits: keep WP107's code and change only the
fallback to seven arguments, `new IdentitySnapshot("?", "?", "?", "?", originatingPatchTarget, "?", "?")`.

Then hand-apply the C# and doc parts of db1a7166b and ff84e1b88:

1. `BuildStampReport.cs`, the comment inside `TryParseStamp` that begins "Fixed-width slice, NOT a trim.":
   replace its first two lines with
   ```
   // Fixed-width slice, NOT a trim. The .NET SDK appends the commit SHA to InformationalVersion
   // ("+{sha}", or ".{sha}" when the string already holds a '+', as the 2026-08-02 form did),
   // so the real string is "build.20260802-013132Z.46ce6436…", now "build.…Z+{sha}[.dirty]".
   ```
2. `BuildStampReportTests.cs`, in `TryParseStamp_RealBuildOutput_WithCommitShaSuffix_Parses`, replace the
   two comment lines that credit Bannerlord.BuildResources with
   ```
   // The .NET SDK appends the commit SHA ('.' here, because the string then began "+build."),
   // so the stamp is NOT at the end of the string. The first version of this parser did TrimEnd('Z') and failed on
   ```
3. `CrashBundleWriterTests.cs`: rename `BuildManifest_CarriesTheTaomBuildStamp` to
   `BuildManifest_IdentityWithBuildStamp_PrintsTaomBuildLine`; in `MakeContext` change the fixture's
   `"v1.5.2", "1.5.2.x"` to `"v1.4.8", "1.4.8.x"` (test data only; no assertion reads them).
4. `docs/features/crash-report.md`: replace the `| Identity |` row with the first line below, and add the
   second line as the first bullet under `## Changelog` (put the real date in place of `<today>`):
   ```
   | Identity | `Native` + `TAOM` module versions, Bannerlord.exe FileVersion, TAOM.dll SHA1, TAOM build stamp (`InformationalVersion`: build time, commit SHA, `.dirty` or `nogit` flag; also in `manifest.txt`), language code |
   - <today>: **Added the TAOM build stamp to the Identity section** (port of plan 017, #658). `report.txt` prints a `Build:` line, `manifest.txt` a `TAOM build:` line and `report.json` gains `TaomBuild`: the assembly's `InformationalVersion`, which names the commit and carries `.dirty` or `nogit` when the build could not prove a clean tree. `TaomVersion` alone is shared by every commit since the last version bump.
   ```

Tests that come with it (4): `TryParseStamp_DirtyAndNoGitSuffixes_StillParse`,
`ReadInformationalVersion_TaomAssembly_CarriesTheBuildStamp`, the renamed manifest test,
`Render_Identity_PrintsTheTaomBuildStamp`.

Commit: `feat(crash): v2.0.28 - put the TAOM build stamp in crash bundles`. Body: `IdentitySnapshot`
gains a last field `TaomBuild` (the `InformationalVersion`), printed as `Build:` in report.txt and
`TAOM build:` in manifest.txt and serialised in report.json, so triage no longer depends on the bundled
log; the field goes last so the positional fallback cannot shift. Port of #658. Trailers:
`Not-tested: IdentityCollector reads ModuleHelper in game only`; `Ported-from:` 743ce2e1d892a312f8f2ff91a759ac91606eb23a,
db1a7166b974b4d6b927fadfb0fd077278e914e9, ff84e1b88fe01e21fabc920dc8b922c1c474a270 (one line each).

**Verify**: Build exit 0; Filtered `BuildStampReportTests|CrashBundleWriterTests|PlainTextCrashReportRendererTests`
0 failed; `git grep -l "TaomBuild" -- Main/Features/CrashReport` lists exactly IdentityCollector.cs,
IdentitySnapshot.cs, CrashBundleWriter.cs and PlainTextCrashReportRenderer.cs; the per-file counts of
`git grep -c "new IdentitySnapshot(" -- Main TAOM.Tests` sum to 4 and every site passes seven arguments.

### Step A3 (new, TDD): the `[BuildStamp]` line names each DLL's configuration and JIT state

RELEASE-INTEGRITY-01: the public DLLs are Debug with the JIT optimizer off (DebuggableAttribute 0x107 on
the laptop's installed TAOM.dll and TAOM.Dependencies.dll) and no log says so. Shipping Debug is the
recorded policy (maintainer, 2026-09-30); this only makes it visible.

RED: add to `BuildStampReportTests.cs` (6 tests):

1. `DescribeBuildConfiguration_DebugWithOptimizerDisabled_RendersOff`: `("Debug", true)` gives `config=Debug jitOpt=OFF`.
2. `DescribeBuildConfiguration_ReleaseOrNoAttribute_RendersOn`: `("Release", false)` gives `config=Release jitOpt=ON`.
3. `DescribeBuildConfiguration_UnknownParts_RenderUnknown`: `(null, null)` gives `config=unknown jitOpt=unknown`;
   `("My Config", true)` gives `config=My_Config jitOpt=OFF` (one token).
4. `ReadBuildConfiguration_TaomAssembly_MatchesItsDebuggableAttribute`: for `typeof(BuildStampReport).Assembly`
   the result matches `^config=\S+ jitOpt=(ON|OFF)$`, and `jitOpt=OFF` exactly when
   `GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled == true` (do not hard-code Debug).
5. `BuildReportFromParts_PlacesConfigTokensAfterEachStamp`: with E3's two version strings and
   `config=Debug jitOpt=OFF` for both, the line matches
   `^\[BuildStamp\] TAOM=\S+ build\.\S+ config=Debug jitOpt=OFF TAOM\.Dependencies=\S+ build\.\S+ config=Debug jitOpt=OFF `.
6. `BuildReportFromParts_StillMatchesTheVerdictToolPattern`: the same line matches the 1.5.x
   `tools/ingame_verdict.py` pattern `\[BuildStamp\] TAOM=\S+ build\.\S*?\+([0-9a-f]{7,40})(\.dirty)?(?:\s|$)`
   with group 1 = `8df90444cc2f1e6d83bb95d2164ab764b4fcae65` (WP139 ports that tool).

GREEN: in `BuildStampReport.cs` (add `using System.Diagnostics;`):

```csharp
public static string DescribeBuildConfiguration(string? configuration, bool? jitOptimizerDisabled)
{
    string config = string.IsNullOrWhiteSpace(configuration)
        ? "unknown"
        : string.Join("_", configuration!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    string jit = jitOptimizerDisabled is null ? "unknown" : (jitOptimizerDisabled.Value ? "OFF" : "ON");
    return $"config={config} jitOpt={jit}";
}

internal static string ReadBuildConfiguration(Assembly? asm)
{
    if (asm == null) return DescribeBuildConfiguration(null, null);
    try
    {
        string? config = asm.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
        // No DebuggableAttribute: the JIT optimizes. Same rule as the 1.5.x PerfContextReader.
        bool jitOff = asm.GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled ?? false;
        return DescribeBuildConfiguration(config, jitOff);
    }
    catch { return DescribeBuildConfiguration(null, null); }
}

// The pure composition, unit-tested with literal strings. BuildReport becomes:
// BuildReportFromParts(ReadInformationalVersion(main), ReadBuildConfiguration(main),
//                      ReadInformationalVersion(dependencies), ReadBuildConfiguration(dependencies))
internal static string BuildReportFromParts(string mainVer, string mainConfig, string depsVer, string depsConfig)
{
    // move the existing verdict computation from BuildReport here, unchanged
    return $"[BuildStamp] TAOM={mainVer} {mainConfig} TAOM.Dependencies={depsVer} {depsConfig}{verdict}";
}
```

The tokens sit after each stamp so `TAOM=\S+ build\.` and the ` TAOM.Dependencies=` boundary still parse.

Commit: `feat(diagnostics): v2.0.28 - BuildStamp names each DLL's configuration`. Body: why (E3/E4
cannot tell a Debug DLL; every frame time from a public build measures unoptimized code), the token
format, and that the shipped configuration is unchanged. Trailers: `Research: net472 reference docs,
DebuggableAttribute.IsJITOptimizerDisabled and AssemblyConfigurationAttribute.Configuration`;
`Not-tested: the line in a player log`. No `Ported-from` (new work).

**Verify**: RED first (the 6 tests fail to compile or fail); then Build exit 0 and Filtered
`BuildStampReportTests` 0 failed.

### Step A4 (new, TDD): the BuildStamp verdict pairs DLLs by commit

RELEASE-INTEGRITY-02: equal clean commits are a pair whatever the clock says; different commits keep the
time tiers, and MISMATCH names both commits instead of asserting a failure E3 contradicts (its lines
94-104 show every preview patch applied).

RED: add to `BuildStampReportTests.cs` (7 tests):

1. `TryParseCommit_CleanStamp_ReturnsTheCommitAndNoFlag`: E3's TAOM string gives true, `8df90444cc2f1e6d83bb95d2164ab764b4fcae65`, flagged false.
2. `TryParseCommit_DirtyOrNoGitFlag_IsFlagged`: `+<sha>.dirty` and `+<sha>.nogit` give true, flagged true.
3. `TryParseCommit_NoCommit_ReturnsFalse`: `build.20260923-184249Z+nogit`, `build.20260801-143000Z`, null, `2.0.0.0`.
4. `TryParseCommit_OlderDotSeparator_Parses`: `build.20260802-013132Z.46ce6436e1b538a7734a23713ec818a23afec93d`.
5. `BuildReportFromParts_SameCleanCommit_IsPairedWhateverTheGap`: both strings carry `8df90444…`, 16 days
   apart: contains `(pair OK: both built from 8df90444)` and not `MISMATCH`.
6. `BuildReportFromParts_DifferentCommitsDaysApart_IsMismatchNamingBothCommits`: E3's exact two strings:
   contains ` MISMATCH — built 16d 08h 46m apart from different commits (8df90444, 0c323437)` and does not
   contain `expect the preview patches to fail`.
7. `BuildReportFromParts_SameCommitButDirty_FallsBackToTheClock`: same sha, TAOM `.dirty`, 3 days apart:
   contains `MISMATCH`, not `from different commits`.

GREEN:

```csharp
private static readonly System.Text.RegularExpressions.Regex CommitRx = new(
    @"build\.\d{8}-\d{6}Z[+.](?<sha>[0-9a-fA-F]{40})(?<flag>\.(?:dirty|nogit))?",
    System.Text.RegularExpressions.RegexOptions.CultureInvariant);

public static bool TryParseCommit(string? informationalVersion, out string commit, out bool flagged)
{
    commit = string.Empty; flagged = false;
    if (string.IsNullOrEmpty(informationalVersion)) return false;
    var m = CommitRx.Match(informationalVersion);
    if (!m.Success) return false;
    commit = m.Groups["sha"].Value.ToLowerInvariant();
    flagged = m.Groups["flag"].Success;
    return true;
}
```

In `BuildReportFromParts`, when both stamps parse: if both commits parse, neither is flagged and they are
equal, the verdict is `$" (pair OK: both built from {commit.Substring(0, 8)})"`; otherwise call
`DescribeVerdict(Classify(...), a, b, mainShort, depsShort)`, passing the two 8-character commits only when
both parse and differ. `DescribeVerdict` gains `string? mainCommit = null, string? depsCommit = null` and
its Mismatched case becomes (keep the prefix byte for byte: `tools/perf_runs.py` on 1.5.x matches
`MISMATCH\W+built (.+?) apart`):

```csharp
string commits = mainCommit != null && depsCommit != null
    ? $" from different commits ({mainCommit}, {depsCommit})" : "";
return $" MISMATCH — built {gap} apart{commits}. These modules were not built together: install " +
       "BOTH from one release. If a preview patch below did not apply, this pairing is the likely " +
       "cause (issue #371).";
```

Add one sentence to the class summary (A2 already updated its stamp description): equal clean commits
are a pair whatever the time between the builds; the time tiers remain the fallback.

Commit: `fix(diagnostics): v2.0.28 - BuildStamp pairs DLLs by commit, not clock`. Body: E3's MISMATCH is
a carried-over Dependencies whose sources were identical; equal clean commits now read paired and
different commits are named. No `Ported-from` (new work).

**Verify**: RED first; then Build exit 0; Filtered `BuildStampReportTests` 0 failed;
`git grep -c "expect the preview patches to fail" -- Main` prints nothing.

### Part A done criteria

- [ ] Build exit 0; Test 0 failed; total = baseline + 31 (14 + 4 + 6 + 7).
- [ ] The A1, A2 and A4 grep checks above hold.
- [ ] `git diff --name-only bannerlord-1.4.5...HEAD` (three dots: the integrator may have moved the base since the worktree was cut) lists only Part A in-scope files.
- [ ] Four commits, each subject carries `v2.0.28`, none stages `CHANGELOG.md`: `git log --format=%s bannerlord-1.4.5..HEAD | grep -c "v2\.0\.28 - "` prints 4; `git diff --name-only bannerlord-1.4.5...HEAD -- CHANGELOG.md` prints nothing.

---

## Part B: the release gate

### Step B1 (maintainer applies; executor verifies and commits): the dirty flag in the stamp

Ports 057628a2b, 9cedb92df and 3c747f4fb's `Directory.Build.props` hunks as their end state. Agents
may not write this file (config-protection) and must not route around that with git.

1. Executor: confirm `git diff 1b00d1acd -- Directory.Build.props` prints nothing, then STOP and ask the
   maintainer to run, in the worktree: `git checkout 9ca99437fc3879c9a981d8430ff8d44873c209a0 Directory.Build.props`.
2. Executor, once the maintainer has done that: `git diff --cached Directory.Build.props` must show only one added target,
   `TaomStampWorkingTreeState` (`AfterTargets="InitializeSourceControlInformation"`,
   `BeforeTargets="AddSourceRevisionToInformationalVersion"`), whose `Exec` runs
   `git --no-optional-locks status --porcelain --untracked-files=normal -- Main Dependencies Stubs Directory.Build.props GameReferences.targets 2&gt;nul`
   and appends `.dirty`, `.nogit` or writes `nogit` to `SourceRevisionId`. `GameReferences.targets` stays in
   the pathspec for parity with 1.5.x (git exits 0 for a missing pathspec; checked this session).
3. Commit: `feat(build): v2.0.28 - mark dirty and git-less builds in the stamp`. Body: the SHA in the
   stamp could not prove the DLL held only committed code (the public `+8df90444`); the target marks
   `.dirty`, `.nogit` or `nogit`, and `--require-build` refuses all three; the maintainer applied the
   file. Trailers: `Research: dotnet --version 10.0.401 here; E3 line 2 shows the SDK +sha suffix on a
   1.4.x build`; `Ported-from:` 057628a2b1464cff588a429c2b0ed7f58878e1e5, 9cedb92df241a24a2ba65d5aa3f5dc47805484b1,
   3c747f4fb7ff809c66697b62f00ad71a67a51de5.

**Verify** (after the commit): Build, then the "Stamp of a built DLL" command on
`Main/bin/Debug/net472/TAOM.dll` and on `Dependencies/bin/Debug/net472/TAOM.Dependencies.dll`: each prints
one stamp ending `+<git rev-parse HEAD>` with no suffix. Then `echo probe > Main/zz_dirty_probe.txt`,
Build, the TAOM.dll stamp ends `.dirty`; `rm Main/zz_dirty_probe.txt`, Build, the stamp is clean again.

### Step B2 (end-state transplant): the plan 017 packager gate

Ports 5d1b1bda1, db1a7166b and d5033e4e8's tool hunks. 1.4.x holds the exact pre-plan-017 files, so the
series tip copy is their exact sum.

Method: `git checkout 9ca99437fc3879c9a981d8430ff8d44873c209a0 tools/package_release.py tools/tests/test_package_release.py`.

> **Do not use that checkout now (integrator note, 2026-11-05).** B4 to B7 landed before B2, so it would silently
> remove their gates and tests. After M1, three-way merge each file with base `1b00d1acd`, ours HEAD and theirs
> `9ca99437f` (`git merge-file`), keep both sides of each conflict, drop `Gaming.Desktop` from `TestRequireBuildCli`
> and `Game Pass` from the `SHIPPED_DLLS` comment, then run the B2 verifies and B3. The STOP condition's
> `git diff 1b00d1acd` check on the two tool files now prints by design; this note answers it for B2.

Adds `--require-build REV`: reads every `bin/<platform>/` copy of `TAOM.dll` and `TAOM.Dependencies.dll`
(the Win64 client copy must exist), casefolds module names, and refuses a stamp that is `.dirty`,
`nogit` or `.nogit`, names another commit, or is unreadable; a requested module missing from `--source`;
an empty or unresolvable REV; and a REV whose `Directory.Build.props` lacks `TaomStampWorkingTreeState`.

Tests that come with it: `TestBuildStamp`, `TestRequireBuildCli` (including the predates and
shallow-clone cases).

**Verify** (before committing, HEAD = the B1 commit): `python -m unittest tools.tests.test_package_release`
prints `OK`. Real-DLL check: copy the B1 build's `Main/bin/Debug/net472/TAOM.dll` to
`../115-scratch/Modules/TAOM/bin/Win64_Shipping_Client/TAOM.dll`, then
`python tools/package_release.py --source ../115-scratch/Modules --dest ../115-scratch/out --modules TAOM --dry-run --require-build HEAD`
prints `build stamp OK` (exit 0), and the same with `--require-build HEAD~1` exits 2 with `predates`.
Commit: `feat(release): v2.0.28 - refuse to package a dirty or off-tag DLL`. Body: the gate that would
have refused the public pair (E3: TAOM `+8df90444`, Dependencies `+0c323437`); port of #658.
`Ported-from:` 5d1b1bda12271de7383a1c2ca0ccbbd32698541a, db1a7166b974b4d6b927fadfb0fd077278e914e9, d5033e4e897f3a0500ec5808ba495d8217211c42.

### Step B3: port e4c2c159a's three reports, keep module sacks shipping

Method: `git cherry-pick -n e4c2c159a93fd8dd5680638e0c4da664cbf33f9e`. The tool files apply cleanly
(their base is byte-identical to the B2 copies). Resolve the rest:

- `.claude/skills/release/SKILL.md`, `docs/reference/release-process.md` (C5 writes them),
  `docs/reviews/REVIEW-LOG.md`, `docs/reviews/lessons/build-tooling-workflow.md`,
  `docs/reviews/lessons/testing-qa.md`: keep ours.
- `plans/035-release-shader-cache-check.md` (deleted by us): `git rm -q` it.
- `docs/reviews/deep-review-035-release-shader-cache-check-2026-10-02.md` and
  `docs/reviews/rca-release-shader-cache-check-2026-10-02.md` (added): `git rm -q -f` them.
- Keep `docs/modding/module-map.md` but replace everything from "For this module it drops" through
  "a scene without a sack ships as it is." (the end of step 5) with: "For this module it drops `RuntimeDataCache`,
  `AssetSources`, `Prefabs_Unused` and `*.xml.bak`, and keeps `ModuleData`, `SceneObj`, `SceneEditData`,
  `Assets`, `AssetPackages`, `Atmospheres`, `NavMeshPrefabs`, `Prefabs`, `Shaders` and `bin`. It lists the
  module's own `Shaders/D3D11/compressed_shader_cache.sack` with its size and format under one policy line
  and ships it: on the 1.4.x line, leaving module sacks out waits on a v1.4.8 test that the game uses a
  Kit-built sack. It also lists, for every shipped scene, whether `SceneObj/<scene>/ShaderCache/D3D11/`
  holds `terrain_shaders_header_data.bin` and `compressed_shader_cache.sack` and the sack's format, and
  prints a `WARNING` for a sack whose format differs from the other shipped sacks' majority. That listing
  is a report: it never refuses, and a scene without a sack ships as it is."

Do not port the module-sack exclusion (1.5.x decision rested on an untested Kit-sack assumption, and E3
shows 2,720 shaders pending at battle start). Edit `tools/package_release.py`:

1. `MODULE_SACK_POLICY = ("module shader sacks ship as they are on the 1.4.x line: leaving them out waits on a v1.4.8 test that the game uses a Kit-built sack")`; rewrite the comment above it to match.
2. Add
   ```python
   def is_module_sack(rel: str) -> bool:
       """True for Shaders/D3D11/compressed_shader_cache.sack (any case): a module's own sack, not a scene's."""
       parts = PurePosixPath(rel).parts
       return (len(parts) == 3 and parts[0].casefold() == "shaders"
               and parts[1].casefold() == "d3d11" and parts[2].casefold() == SHADER_SACK)
   ```
3. Delete the `classify` block headed `# --- policy exclusion` that returns `MODULE_SHADER_SACK`.
4. In `plan_module`, delete the `if d.rule == "MODULE_SHADER_SACK":` lines and, in the COPY branch after
   `plan._copy_list.append((rel, size))`, add `if is_module_sack(rel): plan.module_sacks.append((rel, size))`.
   The `ModulePlan.module_sacks` comment says "each module-level shader sack that ships".
5. Docstring: replace the paragraph "Module-level sacks are left out of the copy by policy..." with "Module
   sacks ship on the 1.4.x line; the report lists each one with its size and format under one policy
   line." In `main()` delete "(classify leaves the module sacks out, not their report, so a report that
   fails cannot ship one.)"; in `_module_sack_report`'s docstring say they ship and the report only reads.

Edit `tools/tests/test_package_release.py`: set `POLICY` to the new text and rewrite the comment above
`MODULE_SACK`; in `TestModuleShaderSackRule` the first test becomes
`test_the_module_sack_ships_on_the_1_4_x_line` (`act(MODULE_SACK) == pr.COPY`), the depth test asserts
`pr.is_module_sack(rel)` is false for its three paths and true for `MODULE_SACK`, the case test asserts
`pr.is_module_sack(rel)` true; in `TestModuleShaderSackPlan` the first test becomes
`test_a_module_sack_is_recorded_and_enters_the_copy_list` (copy list `[MODULE_SACK, "Shaders/D3D11/shader_mapping.bin", "SubModule.xml"]`,
bytes `(57, 9, 0)`, `by_rule == {"ASSET_SOURCES": 9}`); in `TestModuleShaderSackCli`,
`test_the_policy_is_stated_once_and_nothing_is_dropped_for_it` (policy once, `"MODULE_SHADER_SACK"` not in
stdout), `test_a_real_run_ships_every_module_sack_and_the_rest` (each `dest/module/MODULE_SACK` exists) and
`test_a_failing_report_changes_neither_the_exit_code_nor_what_ships` (the TAOM_Map sack exists in dest).
In `test_issue_448s_module_level_sack_is_listed_not_compared` change only the comment ("the copy ships
it"). Write these edited tests first and watch them fail before editing the tool.

Commit: `feat(release): v2.0.28 - packager reports sacks and JIT state`. Body: three read-only reports
(scene shader caches with a format WARNING, module sacks, JIT optimization per shipped DLL copy: OFF is
expected for the Debug builds TAOM ships); module sacks keep shipping on 1.4.x and why. Port of #717 and
#448. `Ported-from: e4c2c159a93fd8dd5680638e0c4da664cbf33f9e`.

**Verify**: `python -m unittest tools.tests.test_package_release` prints `OK`;
`git grep -c "MODULE_SHADER_SACK" -- tools/package_release.py` prints nothing; Build exit 0.

### Step B4 (TDD): refuse retired binaries and Custom Battle test troops

Ports 9c310bc23's `RETIRED_BINARIES` mechanism with the 1.4.x list (RELEASE-INTEGRITY-05; the laptop's
`TAOM.zip` ships both retired files) and a4b5d6258's test-troop refusal. `MinHook.x64.dll` and
`TAOM.NativeSkinFixes.dll` stay allowed on 1.4.x (vendored allowlist, CLAUDE.md).

RED: add to the test file, before `class TestBuildStamp`:

- `TestRetiredBinaries`: a dry run refuses `TAOM/bin/Win64_Shipping_wEditor/BehaviorTrees.dll` (exit 2, path
  in stderr); a real run refuses `TAOM/bin/Win64_Shipping_Client/BehaviorTreeWrapper.dll` and writes
  nothing; `retired_binaries` matches case-insensitively and only under `bin/`
  (`TAOM/bin/Gaming.Desktop.x64_Shipping_Client/BehaviorTreeWrapper.DLL` yes, `TAOM/ModuleData/behaviortrees.dll`
  no); `test_the_1_4_x_vendored_natives_are_not_retired` (`MinHook.x64.dll` and `TAOM.NativeSkinFixes.dll`
  under `TAOM/bin/Win64_Shipping_Client/` give `[]` and a dry run exits 0); and 9c310bc23's
  `test_no_retired_name_is_tracked_in_the_repo` verbatim (add `PurePosixPath` to the import).
- a4b5d6258's `_write_text`, `_RIDER` and `TestTestTroops` verbatim (4 tests;
  `test_the_repo_module_ships_none` reads `Main/_Module`).

GREEN in `tools/package_release.py`: after `NATIVE_DEBUG_EXT` add
`RETIRED_BINARIES = frozenset({"behaviortrees.dll", "behaviortreewrapper.dll"})` with a comment (inlined
into TAOM.dll 2026-05-24; deploys never delete, so installs and channels keep them); add
`retired_binaries(plans)` (9c310bc23's body) and `_TEST_TROOP_ID_RE` plus `test_troop_files(plans)`
(a4b5d6258's bodies) after `stamps_dirty_trees`; in `main()`, between the unknown-entries listing and
`if args.require_build is not None:`, add the retired-binaries refusal then the test-troop refusal
(their print-and-return-2 blocks from the two commits). Extend the docstring's exit-code line.

Commit: `fix(tools): v2.0.28 - packager refuses retired DLLs and test troops`. `Ported-from:`
9c310bc238fa1441f4a1344c5cc3fe5dccadb6a2 (the mechanism; its MinHook and NativeSkinFixes entries are
1.5.x-only), a4b5d625863f80f542eb8240ee061ae16386e9f3.

**Verify**: RED first; `python -m unittest tools.tests.test_package_release` prints `OK`; Build exit 0.
Optional real-data check (read-only): `python tools/package_release.py --source "$BANNERLORD_GAME_DIR/Modules" --dest ../115-scratch/out --dry-run`
exits 2 naming `TAOM/bin/Gaming.Desktop.x64_Shipping_Client/BehaviorTreeWrapper.dll`,
`TAOM/bin/Win64_Shipping_wEditor/BehaviorTreeWrapper.dll` and `TAOM/bin/Win64_Shipping_wEditor/BehaviorTrees.dll`.

### Step B5 (TDD): refuse Armory shaders players have no source for

Ports d99398f12's `missing_armory_shaders` gate (the shared hunk WP113 hands to this WP), re-implemented
for 1.4.x under D12: 1.4.x installs no shader sources, so any `taom_` shader is a gap; a native shader
needs `<game-root>/Shaders/Sources/<shader>.rs`. Read the report with a right-anchored row regex and check
its `Total variants:` trailer (the two-space split skips 168 rows of the laptop's report). Fail closed when
it cannot check. `--game-root` (default: the parent of `--source`) lets a channel folder be checked; 1.5.x
later added the same option name.

RED: `TestArmoryShaderSources` (9 tests), adapted from d99398f12: every row parsed and the total checked;
a report whose rows do not add up is refused; native shaders with sources pass (`shader sources OK`); a
`taom_` shader is refused even with `.rs` files in the game folder and in `TAOM/Shaders/Sources` (stderr
names it and `D12`); a native shader with no source is refused and names its materials; a missing report
is refused (`ships no Shaders/D3D11/shader_compile_report.log`); an unplanned Armory is not checked;
`--game-root` points the native check at another folder; a missing `<game-root>/Shaders/Sources` says to
pass `--game-root`. Fixture: `_fixture(Path(td))` plus `LOTRLOME_Armory/SubModule.xml` and a report written
with `_write_text`; game folder = `Path(td)`.

GREEN: add `ARMORY_MODULE`, `SHADER_REPORT = "Shaders/D3D11/shader_compile_report.log"`,
`_REPORT_ROW = re.compile(r"^(?P<mesh>.*\S)\s+(?P<material>\S+)\s+(?P<shader>\S+)\s+(?P<variants>\d+)\s*$")`,
`_REPORT_TOTAL = re.compile(r"^Total variants:\s*(\d+)\s*$")`, `parse_shader_report(text) -> (usage, problem)`
(problem when the trailer is missing or the row variants do not sum to it) and
`missing_armory_shaders(plans, game_root) -> (info_lines, problems)` as described above; a `--game-root`
argument; in `main()`, after the test-troop refusal: problems print as
`ERROR: shader sources: ...` lines and return 2, otherwise print `shader sources OK:` and the info lines.

Commit: `fix(tools): v2.0.28 - packager refuses Armory shaders with no source`. Body: D12 rationale, the
fail-closed rule, the parser fix. `Ported-from: d99398f1210eeabc67a5158c56479fb9a5f6749f` (gate hunk only;
its triage hunk is WP113's; ShaderSourceInstall not ported, D12).

**Verify**: RED first; tool test `OK`; Build exit 0. Real-data check (read-only):
`python tools/package_release.py --source "$BANNERLORD_GAME_DIR/Modules" --dest ../115-scratch/out --modules LOTRLOME_Armory --dry-run`
exits 0 and prints `shader sources OK` (the laptop's v2.0.26 Armory names 10 native shaders, all with a
source). If WP101's D20 update installed the public Armory and the run refuses, that is a real finding:
report it, do not change the gate.

### Step B6: drop Game Pass staging and refuse a Game Pass bin

Ports e6ee8c998 for TAOM and TAOM.Dependencies. Decided under the delegated authority: the maintainer's
2026-10-22 "Game Pass is not supported" applies to 1.4.x too (every binaries path in the player logs is
`Win64_Shipping_Client`: E1 and E2 39 each, E3 1; none names `Gaming.Desktop`; and the laptop's TAOM.zip
Game Pass folder carries a retired `BehaviorTreeWrapper.dll`). The `TAOM.Client` guard and
`ClientDeploymentTests` stay out (D7).

1. `Main/TAOM.csproj`: in `CopyBinariesToModuleFolder` delete the Gaming.Desktop `MakeDir` line, the
   `<!-- Copy to Gaming.Desktop.x64_Shipping_Client -->` comment and its two `Copy` lines. Insert before that
   target (tabs, as the neighbours):
   ```xml
   <!-- Game Pass is not supported (2026-10-22), but Bannerlord.BuildResources' CopyModule deploys everything
        under _Module verbatim, so a Gaming.Desktop.x64_Shipping_Client folder left in this project's
        staging by an older checkout would reach the game install with an old TAOM.dll. Fail loudly rather
        than delete. Same condition as CopyModule's, not DisableModuleCopy (see FailOnIdeStateInModule). -->
   <Target Name="FailOnGamePassStagingInModule" BeforeTargets="PostBuildCopyToModules"
           Condition="'$(ModuleId)' != '' AND Exists('$(GameFolder)')">
       <Error Condition="Exists('$(ProjectDir)_Module\bin\Gaming.Desktop.x64_Shipping_Client\')"
              Text="Game Pass is not supported, but $(ProjectDir)_Module\bin\Gaming.Desktop.x64_Shipping_Client\ exists and CopyModule would deploy it. Delete that folder (and from &lt;game&gt;\Modules\$(ModuleId)\bin\, deployment is additive), then build again." />
   </Target>
   ```
2. `Dependencies/TAOM.Dependencies.csproj`: delete lines 76, 78 and 80 (the Gaming.Desktop `MakeDir` and
   two `Copy` lines) and insert the same target before `CopyBinariesToModuleFolder`.
3. Packager, tests first: e6ee8c998's `TestGamePassBinIsRefused` minus
   `test_the_folder_is_not_a_client_platform_bin` (5 tests); then `GAME_PASS_DIR`, `game_pass_bins(plans)`
   and the refusal block in `main()` right after the retired-binaries refusal (texts from e6ee8c998). In
   `TestRequireBuildCli.test_accepts_every_copy_when_all_are_clean` drop `"Gaming.Desktop.x64_Shipping_Client"`
   from the folder tuple; in the `SHIPPED_DLLS` comment drop "Game Pass".
4. Docs: `docs/modding/module-taom.md` row for `bin/Gaming.Desktop.x64_Shipping_Client/` says nothing
   writes it (Game Pass not supported, 2026-10-22; an older checkout may hold ignored output there, which
   `FailOnGamePassStagingInModule` refuses to deploy); the `CopyBinariesToModuleFolder` bullet names only
   `Win64_Shipping_Client` and names the target instead of line numbers; the sentence about "the
   Gaming.Desktop sibling is ignored whole" adds that nothing writes it any more; the install count row
   drops "4 Gaming.Desktop" and notes a stale folder on installs built before this change.
   `docs/modding/modules-overview.md` lines 68 and 531: drop the Gaming.Desktop counts the same way.

Commit: `fix(build): v2.0.28 - drop Game Pass staging and refuse its bin`. Body: decision and evidence
above, the maintainer cleanup this forces (M2), what was left out (D7). Port of #968.
`Ported-from: e6ee8c9985beee1b63dafb2cda11c1a9b5879ae4`. `Not-tested: FailOnGamePassStagingInModule in a deploying build`.

**Verify**: Build exit 0 (the new target is skipped under `-p:ModuleId=`); Test 0 failed; tool test `OK`;
every remaining `git grep -n "Gaming.Desktop" -- Main/TAOM.csproj Dependencies/TAOM.Dependencies.csproj`
hit is inside `FailOnGamePassStagingInModule` or its comment.

### Step B7 (TDD): gate the release number and the installed manifests

Re-implements e6ee8c998's second packager check (compare the installed `SubModule.xml` with the release
commit's) for 1.4.x's two DLLs, and adds D8: a 1.4.x release number is never a 1.5.x tag or four-part.

RED:

- `TestReleaseNumber` (unit): `v2.0.29` to `v2.0.36` each give a problem containing `1.5.x`; `v2.0.29.5`
  gives one containing `three-part`; `v2.0.28`, `v2.4.0`, `v2.4.11` give None; None gives a problem.
- `TestTagOnAnotherLine` (unit, git; skip a case when its tag is absent locally): `v2.1.0` (tags
  `5472dd339`, on no branch) against HEAD gives a problem containing `another line`; `v2.0.28` (an ancestor
  of HEAD on this line) gives None; `v9.9.9` (no such tag) gives None.
- `TestInstalledManifest` (CLI, `--require-build HEAD`, skip when not a git checkout): an installed
  `TAOM/SubModule.xml` equal to `git show HEAD:Main/_Module/SubModule.xml` except `<Version value="v2.0.29.5" />`
  is refused, stderr names `v2.0.29.5` and HEAD's version (read it from git, never hard-code it); one naming
  another `DLLName` is refused (`names`); one that does not parse is refused (`does not parse`); an installed
  `TAOM.Dependencies/SubModule.xml` with another `<Version>` is refused; HEAD's exact bytes plus a clean
  `+HEAD` DLL pass with `build stamp OK`.

GREEN: `REPO_MANIFESTS = {"taom": "Main/_Module/SubModule.xml", "taom.dependencies": "Dependencies/_Module/SubModule.xml"}`;
`FIVE_X_RELEASES = frozenset(f"v2.0.{n}" for n in range(29, 37))` (1.5.x tags, decision D8);
`release_number_problem(version)` (None; not `^v\d+\.\d+\.\d+$` gives "is not a three-part vX.Y.Z (a
hand-typed fourth part, like v2.0.29.5, is in no commit)"; in `FIVE_X_RELEASES` gives "is a 1.5.x
release number (decision D8: the 1.4.x line uses v2.4.x)"); `tag_on_another_line(version, sha)` (when
`git rev-parse -q --verify refs/tags/<version>^{commit}` resolves and `git merge-base --is-ancestor <that> <sha>`
fails, "<version> is a tag on another line (<short sha>); pick the next v2.4.x number"; this also covers the
`v2.1.0` and `v2.1.1` tags WP102's numbering note forbids); `_git_bytes(*args)` (e6ee8c998's helper);
a manifest reader returning `(version, sorted DLLName values)` from bytes or None; and in `require_build`,
after the dirty-flag check: refuse when the commit's `Main/_Module/SubModule.xml` cannot be read or its
version has a `release_number_problem` or a `tag_on_another_line`; for each planned TAOM or TAOM.Dependencies module whose installed
`SubModule.xml` exists, refuse when it does not parse or its version or DLL set differs from the commit's
(casefolded). An absent installed manifest is not this check's finding (the existing CLI tests write none).

Commit: `fix(release): v2.0.28 - gate the release number and installed manifests`. Body: the public
channel's hand-typed v2.0.29.5 would now be refused twice; D8. Port of #658 and #968.
`Ported-from: e6ee8c9985beee1b63dafb2cda11c1a9b5879ae4` (manifest check only).

**Verify**: RED first; tool test `OK`; Build exit 0.

### Part B done criteria

- [ ] `git log --oneline -S TaomStampWorkingTreeState -- Directory.Build.props` lists exactly the B1 commit.
- [ ] `python -m unittest tools.tests.test_package_release` prints `OK`; full tool tests show no new failure.
- [ ] Build exit 0; Test 0 failed (count unchanged from Part A).
- [ ] `git grep -n -E "def (require_build|retired_binaries|game_pass_bins|test_troop_files|missing_armory_shaders|release_number_problem|is_module_sack|jit_optimization)\(" -- tools/package_release.py` shows all eight.
- [ ] Docs lint: no new finding in `docs/modding/*` touched files.

---

## Part C: release process, notices, ledger, pairing check (needs Part B)

### Step C1: TAOM.Dependencies first in both manifest blocks

Hand re-implementation of de385cab9 (launcher display only: `GetSortedModules` is multiplayer-only on
v1.4.8, see Current state). Keep the base's pins (`v1.4.8.*`, `v2.0.6` or whatever the base holds). Target
shape of the two blocks:

```xml
  <DependedModules>
    <!-- Issue #371. ...existing comment unchanged through "...actually blocks a broken pairing."
         Listed first on purpose: the launcher shows TAOM.Dependencies at the top of TAOM's
         dependency list, and the version-pinned mirror below keeps the same order. -->
    <DependedModule Id="TAOM.Dependencies" />
    <DependedModule Id="Native" />
    <DependedModule Id="SandBoxCore" />
    <DependedModule Id="Sandbox" />
    <DependedModule Id="CustomBattle" />
  </DependedModules>
  <DependedModuleMetadatas>
    <!-- Version-pinned mirror of the DependedModule above, ...existing comment unchanged -->
    <DependedModuleMetadata id="TAOM.Dependencies" order="LoadBeforeThis" version="v2.0.6" />
    <DependedModuleMetadata id="Native" order="LoadBeforeThis" version="v1.4.8.*" />
    <DependedModuleMetadata id="SandBoxCore" order="LoadBeforeThis" />
```

Commit: `fix(module): v2.0.28 - TAOM.Dependencies listed first in both blocks`. Port of #371.
`Research: v1.4.8 dump ModuleHelper.cs:271 GetSortedModules is called only by Diamond (multiplayer)`.
`Ported-from: de385cab9f149ad86cf2aa83539137f5f6326129`.

**Verify**: Build exit 0; Filtered `BundledDependencyManifestTests|CustomBattleLordStubsTests` 0 failed;
the entries only moved:
`diff <(git show HEAD~1:Main/_Module/SubModule.xml | tr -d '\r' | grep -E "<DependedModule(Metadata)? " | sed 's/^ *//' | sort) <(tr -d '\r' < Main/_Module/SubModule.xml | grep -E "<DependedModule(Metadata)? " | sed 's/^ *//' | sort)`
prints nothing.

### Step C2 (TDD): notices for the NuGet runtime libraries and yotthani's HoN code

Ports 9c310bc23's licence notices and register rows (RELEASE-INTEGRITY-05; the notices are owed today:
the laptop's `TAOM.zip` ships all three DLLs) and eed20591a's HoN notice.

RED: in `BundledDependencyManifestTests.cs` add `MainLicensesFile` (`Main\_Module\THIRD-PARTY-LICENSES.txt`)
and `MainThirdPartyLicenses_NameTheShippedRuntimeLibraries`: the text contains
`$"DryIoc {GetPackageVersion(MainCsproj, "DryIoc.dll")}"`, `$"Json.NET {GetPackageVersion(MainCsproj, "Newtonsoft.Json")}"`
and `System.Runtime.CompilerServices.Unsafe.dll`.

GREEN, `Main/_Module/THIRD-PARTY-LICENSES.txt`:

1. Insert 9c310bc23's `RUNTIME LIBRARIES  --  _Module/bin/<platform>/` block (the DryIoc 4.8.8, Json.NET
   13.0.3 and Unsafe entries and the full MIT text, from `git show 9c310bc23:Main/_Module/THIRD-PARTY-LICENSES.txt`)
   after the MinHook block's `----`, followed by a blank line and its own `----`. Keep the MinHook and
   NativeSkinFixes blocks (both still ship on 1.4.x). This insert sits below the MinHook heading, so it does
   not move the MinHook line `docs/modding/module-taom.md` cites (WP109's scope sentence already moved it by one).
2. WP109's VANILLATUNING section: if it carries the full MIT permission text, replace its line
   `Licensed under the MIT License:` and the 19 MIT lines below it with the one line
   `Licensed under the MIT License (full text above, under RUNTIME LIBRARIES).` so the file holds it once.
3. After the VANILLATUNING section insert eed20591a's `HEIR OF NUMENOR (HoN) PORTIONS  --  inside TAOM.dll`
   section verbatim (`git show eed20591a:Main/_Module/THIRD-PARTY-LICENSES.txt`; its seven folders all
   exist on 1.4.x).
4. Scope paragraph: where WP109 added the VanillaTuning sentence, make it "...the parts of TAOM.dll adapted
   from yotthani's VanillaTuning and from his Heir of Numenor (HoN) mods (MIT)."; otherwise append "It also
   covers the parts of TAOM.dll ported from yotthani's Heir of Numenor (HoN) mods (MIT)." Never add the
   ShaderCacheKeeper `xinput9_1_0.dll` sentence (D11).
5. Closing paragraph: WP109 wrote `TAOM.dll, apart from the VanillaTuning portions named above,`; make it
   `TAOM.dll, apart from the VanillaTuning and HoN portions named above,`. Left alone, the file would call
   the HoN code original TAOM work directly below its own HoN section.

`docs/reference/provenance-register.md`: extend the `.NET Foundation` row's paths with "and TAOM's own copy
of `System.Runtime.CompilerServices.Unsafe.dll`, which the build writes into the install's
`Modules/TAOM/bin/<platform>/`"; after it add 9c310bc23's `DryIoc (Maksim Volkau)` and
`Json.NET (James Newton-King)` rows ("notice ... since <today>"); replace the `external developer drop` row (line 73 at planning) with eed20591a's
`Yotthani HoN mods (external developer drop)` row (tokens `Features_fixed` `HoN` `yotthani/bannerlord`,
MIT, cleared, same paths) and the `TransferbuttonMenu` row (line 75 at planning) with its `Yotthani TransferbuttonMenu` row; retitle the section
"external developer drop, `Downloads/Features_fixed/` (UNCLEARED)" to "Yotthani HoN mods, the external
developer drop `Downloads/Features_fixed/`", replace its "No license, grant, or terms..." lead-in and
"**To resolve:**" paragraph with eed20591a's "Until 2026-10-09..." and "**Resolved 2026-10-09**..." text;
retitle "TransferbuttonMenu (UNCLEARED)" to "TransferbuttonMenu" and replace "No terms recorded." with
eed20591a's text. Leave the MinHook and NativeSkinFixes rows as they are (1.4.x keeps both).
`docs/features/companion-tactics.md`: after the "Ported from `Downloads/Features_fixed/CompanionTactics/`"
line add "The drop is yotthani's Heir of Numenor (HoN) code, MIT by the maintainer's statement of
2026-10-09; its notice is the HoN section of `Main/_Module/THIRD-PARTY-LICENSES.txt`
([provenance-register.md](../reference/provenance-register.md))."

Commit: `docs(provenance): v2.0.28 - notices for NuGet libraries and HoN code`. Port of #736.
`Ported-from:` 9c310bc238fa1441f4a1344c5cc3fe5dccadb6a2 (notices and rows only),
eed20591a15ac41ce66f744b98561961528cfdbb.

**Verify**: RED first; Build exit 0; Filtered `BundledDependencyManifestTests` 0 failed;
`grep -c "full text above, under RUNTIME LIBRARIES" Main/_Module/THIRD-PARTY-LICENSES.txt` prints 2 or
more; `grep -c "Permission is hereby granted" Main/_Module/THIRD-PARTY-LICENSES.txt` prints 1.

### Step C3 (TDD): each release lists its owed in-game checks, filtered to this line

Ports 715530875. On 1.4.x the date cutoff is wrong (a ported 1.5.x fix closed months earlier is new to
this line, and 1.5.x-only issues would be listed), so an issue counts when a commit message in
`<tag>..HEAD` names it (`#N`).

Method: `git cherry-pick -n 715530875ca12d3f3058aa7cc748738f73ca5745`. Keep `tools/ingame_ledger.py`,
`tools/tests/test_ingame_ledger.py` and `docs/reviews/adopt-agent-reach-2026-10-26.md` (new). Keep ours
for the release SKILL.md, release-process.md and `tools/README.md`. For `docs/ai-includes/external-repo-adoption.md`
and `docs/reference/provenance-register.md` keep ours, then add by hand: the Agent Reach bullet as the
last bullet of the adoption list, without "56 since v2.0.35" and ending "...without blocking; on the 1.4.x
line it counts only issues a commit since the last tag names"; the `Agent Reach` row before
`<!-- provenance-register-end -->`, citing "`/release` Phase 1 (owed in-game checks) and Phase 5".

RED, in `test_ingame_ledger.py`: `FakeRunner` gains `range_log` (answer for a `git log` whose arguments
contain `..HEAD`; default text naming `#1` to `#2000`); new tests
`test_only_issues_named_since_the_tag_count` (range log names #2 and #5, gh returns 1 to 5: new [5, 2],
other 3), `test_a_longer_number_does_not_match_a_shorter_one` (#10 named: issue 10 counts, issue 1 does
not), `test_the_range_is_tag_to_head` (when describe returns `v2.0.35`, the recorded range call contains
the argument `v2.0.35..HEAD`), `test_range_log_failure_exits_3`; rewrite
`test_cutoff_is_strict_and_timezone_aware` into the named-issue case and update the text expectations below.

GREEN, `tools/ingame_ledger.py`: `referenced_issue_numbers(runner, tag)` runs
`git log --format=%B <tag>..HEAD` and returns `{int(n) for n in re.findall(r"#(\d+)\b", out)}` (rc != 0
raises `LedgerError(..., 3)`); `main()` selects `new` as rows whose number is in that set (sorted
descending) and `older` as the rest; text line `in-game ledger: {n} issues named by commits since {tag}
({date}) still owe an in-game check; {m} other labelled issues are in the backlog`; md empty line
`Nothing named since {tag} owes an in-game check.` and `Other owed checks: {m} (label triage-needs-ingame).`
Update the module docstring to describe the 1.4.x selection.

Commit: `feat(tools): v2.0.28 - each release lists its owed in-game checks`. Body: the tool, the 1.4.x
filter and why; never blocks. Port of #1010. `Ported-from: 715530875ca12d3f3058aa7cc748738f73ca5745`.

**Verify**: RED first; `python -m unittest tools.tests.test_ingame_ledger` prints `OK`; no command calls
the real `gh` in tests.

### Step C4 (new, TDD): check the TAOM_Map and Armory pairing and pin it

RELEASE-INTEGRITY-08, XML-DATA-04 with D15, K2's dens and villages, and the TAOM_Map pin. New
`tools/check_release_pairing.py` (read-only except `--record`):

```
python tools/check_release_pairing.py --modules "<Modules root>" [--game-root "<game>"] [--pin tools/release_pins.json] [--record PATH]
exit 0 no finding, 1 findings, 2 bad input (missing module folder, settlements.xml or Monsters.xsd)
```

Defaults: `--game-root` is the parent of `--modules` (as in B5); `--pin` is `tools/release_pins.json` beside the tool.

Functions (each unit-tested with temp-dir fixtures):

- `module_identity(module_root) -> {"version", "files", "fingerprint"}`: version from `SubModule.xml`
  `<Version value>`; fingerprint `"sha256:<hex>"` over the sorted lines `relpath\0sha256(bytes)\n` of every
  file under `ModuleData/` that `package_release.classify("ModuleData/" + rel)` would COPY (backups do not
  count); `SubModule.xml` stays outside, so setting the release number does not move the pin.
- version check: `package_release.release_number_problem` on TAOM_Map's and LOTRLOME_Armory's versions
  (finding `MODULE_VERSION`).
- `undeclared_monster_attributes(armory_root, xsd)`: names of every `xs:attribute` under the
  `xs:element name="Monster"` of `<game-root>/XmlSchemas/Monsters.xsd`; for every Armory ModuleData XML whose
  root is `Monsters`, each `Monster` attribute outside that set, with its files. Printed as `PAIRING SIGNAL
  (decision D15, never an error): ...` every run and stored in the pin; never suppressed, never an exit-code change.
- `village_findings(map_root)`: `import add_map_villages as amv`; call `amv.check(amv.VILLAGES, master,
  scene, locs)` with TAOM_Map's `settlements.xml`, `SceneObj/Main_map/scene.xscene` and `locs`, a dict of
  `{lang: text}` read from `ModuleData/Languages/<lang>/loc_settlements.xml` for each of `amv.LANGS` (12); a missing language file is a finding; when the scene file is absent, drop
  the "no entity in scene.xscene" findings and print a note (finding `VILLAGE`).
- `hideout_findings(clans_xml, settlements_xml)`: every `Faction` with `is_bandit="true"` in the repo's
  `Main/_Module/ModuleData/characters/clans.xml` needs one `Settlement` of its culture with a `Hideout`
  component (finding `HIDEOUT`). Same rule as WP112's `BanditHideoutLiveDataTests`, which reads the dev
  install; this one reads the package being released.
- pin: without `--record`, a missing pin file is `PIN_MISSING` and a fingerprint differing from it is
  `PIN_MISMATCH`. With `--record PATH`, refuse (exit 1) when any non-pin finding exists, else write
  `{"recorded_utc", "modules": {name: identity}, "pairing_signals": {...}}` and exit 0.
- Output starts with one line the release note can quote:
  `modules: TAOM_Map <version> <fingerprint[:19]> (<n> files), LOTRLOME_Armory <version> <fingerprint[:19]> (<n> files)`.

RED: `tools/tests/test_check_release_pairing.py`: identity (stable; changes with a ModuleData byte; ignores
a `SubModule.xml` version edit and a `.bak` file); version (`v2.0.29.5` and `v2.0.31` give findings,
`v2.0.27` none); signals (a fixture XSD declaring `id` and a Monster with `taom_body_length` give one signal
naming the file, exit code unaffected); hideouts (one of two bandit cultures without a den gives one
finding naming it); villages (an empty fixture gives a finding naming `amv.VILLAGES[0].id`; a complete
fixture built from `amv.VILLAGES` gives none; an absent scene gives the note and no scene findings); pin
(missing, matching, mismatching); CLI (exit 0 on a complete fixture with a matching pin, 1 on findings, 2 on
a missing TAOM_Map; `--record` writes the JSON and refuses when a finding exists); and a repo check that
`clans.xml` defines at least one bandit clan. A complete fixture writes, per `amv.VILLAGES` entry, a
`<Settlement id="..." posX="1.000" posY="2.000" ...>` row, a `<game_entity name="..."><transform position="1.000, 2.000, 0.000"`
scene entry and a `id="Settlements.Settlement.name.<id>"` row in each of the 12 loc files, plus one hideout
per bandit culture the real `clans.xml` names.

Also add to `docs/features/moduledata-validation.md`, at the end of the "Coverage boundary" XSD paragraph:
"A player rgl log that says `The 'taom_body_length' attribute is not declared` (Monsters.xsd) is not a
1.4.x data defect: the attribute is the 1.5.x MonsterSize feature's, nothing on 1.4.x reads it, and the
engine logs the line and loads the file. It means the LOTRLOME_Armory beside TAOM is a 1.5.x-era build;
`tools/check_release_pairing.py` reports it as a pairing signal (decision D15) and never allowlists it."

Commit: `feat(release): v2.0.28 - check the TAOM_Map and Armory pairing`. Body: the four checks, the pin
and D15. No `Ported-from` (new work).

**Verify**: RED first; `python -m unittest tools.tests.test_check_release_pairing` prints `OK`. Live smoke
(read-only, informational; record the output in the hand-off):
`python tools/check_release_pairing.py --modules "$BANNERLORD_GAME_DIR/Modules"` exits 1 with
`PIN_MISSING`, `VILLAGE` lines naming the 15 table ids, no `HIDEOUT` line, and, if WP101's D20 update put
the public Armory on this laptop, `PAIRING SIGNAL` lines for `taom_body_length` and possibly a
`MODULE_VERSION` line for a hand-typed `v2.0.29.5`.

### Step C5: the release skill and release-process.md describe the gated 1.4.x release

Hand-written for 1.4.x from the doc hunks of ff84e1b88, db1a7166b, d5033e4e8, 9cedb92df, 3c747f4fb,
9ca99437f, e4c2c159a, a4b5d6258, e6ee8c998, 715530875 and 709649c3f's pre-flight 5, plus D8, D15 and the
pairing check. Never carry TAOM.Client, ShaderCacheKeeper (D11), `check_external_loc_coverage.py`,
`check_mcm_default_flips.py`, `ingame_verdict.py` or `--regenerate-shipped-troop-ids` text: none exists on 1.4.x.

`.claude/skills/release/SKILL.md`:

1. Frontmatter `description:` becomes exactly: `Use when cutting a player release: version bump, generated
   CHANGELOG, release note, commit, tag, push, then rebuild at the tag and gate the packaged DLL stamps.
   Enforces #371 pairing.` (29 words).
2. Phase 1: WP102's item 5 (the D8 numbering) gains one sentence: "Never a fourth component either (the
   public channel's hand-typed v2.0.29.5 is in no commit); Phase 8's gate refuses both forms." Then add:
   - `6.` 709649c3f's item without the Animalia sentence: "`git grep -l taom_test_ -- Main/_Module` prints
     nothing. Custom-Battle test riders show in every player's Custom Battle picker; delete them (with their
     SubModule node and their ladder and recruitment exemptions) before a release."
   - `7.` "Owed in-game checks (never blocks): `python tools/ingame_ledger.py` lists the closed issues still
     labelled `triage-needs-ingame` that a commit since the last tag names. Tell the maintainer the count;
     Phase 5 hands the list to the testers. Exit 3 is a `gh` or `git` failure: report it and say in the
     release note that the list is missing."
3. Phase 5 (release note), append: the testers' checklist (`python tools/ingame_ledger.py --since <previous tag> --format md`
   into `docs/releases/vX.Y.Z-testers.md` under a first line naming the release; issue titles are
   developer text, so they stay out of the Discord note), and one line in the release note naming the
   TAOM_Map and LOTRLOME_Armory the release ships, copied from the `modules:` line of Phase 8 step 3, plus
   any `PAIRING SIGNAL` it printed.
4. Phase 6: the staging list names `docs/releases/vX.Y.Z-testers.md` too.
5. New section before `## Gotchas`, numbered one past the last Phase (expected `## Phase 8: Build and package at the tag`):
   - Intro: the Phase 2 build ran before the release commit, so its DLLs carry the parent's SHA; rebuild
     at the tag; TAOM and TAOM.Dependencies always ship from this one build, even when Dependencies did not
     change (#371).
   - Step 1: `git status --porcelain` empty and `git rev-parse HEAD` equals `git rev-parse vX.Y.Z^{commit}`,
     then `./build.ps1` (it compiles and deploys every file in the tree, committed or not); if another
     session is editing, build a clean worktree of the tag (`git worktree add ../taom-release-vX.Y.Z vX.Y.Z`,
     `./build.ps1` there, `git worktree remove ../taom-release-vX.Y.Z`).
   - Step 2: `python tools/package_release.py --source "<game>/Modules" --dest <out> --game-root "<game>" --require-build vX.Y.Z --dry-run`
     prints `build stamp OK` and exits 0. List what it refuses (a `.dirty`/`nogit` copy or one from another
     commit, a missing requested module, a tag that predates the dirty flag, an installed TAOM or
     TAOM.Dependencies `SubModule.xml` whose `<Version>` or DLL list differs from the tag's, a release
     number in the 1.5.x range, with four parts or held by another line's tag; on every run: retired binaries, a
     `Gaming.Desktop.x64_Shipping_Client` bin, a shipped ModuleData file defining a `taom_test_` id, an
     Armory shader with no source players have). Its scene sack, module sack and `JIT optimization`
     sections only report: JIT optimization OFF is expected (Debug builds ship on purpose, maintainer
     2026-09-30); module sacks ship on 1.4.x until a v1.4.8 Kit-sack test says otherwise.
   - Step 3: `python tools/check_release_pairing.py --modules "<game>/Modules" --game-root "<game>"` exits 0
     and `python tools/validate_moduledata.py --game-modules "<game>/Modules"` exits 0 (its codes include WP108's
     `MISSING_COLLISION_BODY` and `COLLISION_BODY_BORROWED`); `python tools/audit_armory_refs.py` (from WP108) reports
     no finding against the Armory the release ships. The desktop's live
     TAOM_Map, exported after 2026-10-29, carries 1.5.x ids: package TAOM_Map and LOTRLOME_Armory from the copies
     `tools/release_pins.json` pins, never from that install. A new pin is recorded with `--record` only
     after the other checks pass, and committed on its own.
   - Step 4: prune per the release-process.md "Cutting a release" step this commit adds (expected 9); the
     dry run refuses what remains.
   - Step 5: the maintainer packages (an outward-facing action). Then re-run steps 2 and 3 with `--source`
     and `--modules` pointing at the channel's `Modules` folder (desktop: `E:\LOTRAOM_Releases\<channel>\Modules`)
     and `--game-root "<game>"` before the maintainer uploads it.
6. Gotchas, the "Version ≠ build stamp" bullet: after "freezes `AssemblyVersion` deliberately." add "The
   SDK appends `+<commit SHA>`, and a build of a tree with uncommitted changes (untracked files included)
   under `Main`, `Dependencies`, `Stubs` or `Directory.Build.props` appends `.dirty` after it (`nogit` or
   `.nogit` when git could not tell)."

`docs/reference/release-process.md`:

1. "Assembly identity" paragraph: after "mismatched pair is one line in the log." add db1a7166b's two
   sentences (SDK `+<commit SHA>`, `.dirty`, `nogit` or `.nogit`).
2. WP102's "**Version numbers on the 1.4.x line.**" paragraph gains: "A fourth component is never written
   either (the public channel's hand-typed `v2.0.29.5`). `tools/package_release.py --require-build` refuses
   a release commit numbered in the 1.5.x range, with four parts or with a number another line's tag holds
   (`v2.1.0`, `v2.1.1`), and an installed TAOM or
   TAOM.Dependencies `SubModule.xml` whose `<Version>` differs from the tag's; `tools/check_release_pairing.py`
   refuses either form in TAOM_Map's or LOTRLOME_Armory's `SubModule.xml`."
3. "Cutting a release", step 1 becomes db1a7166b's wording for this branch only: "Tree clean (`git status --porcelain`
   empty; if another session's edits are present, stop, because `build.ps1` compiles and deploys every file
   in the tree, committed or not; only the Phase 8 build moves to a detached worktree of the tag), on
   `bannerlord-1.4.5`, current version already tagged." After the tag-and-push step (WP102 numbers it 8)
   add the next step (expected 9): "Build at the tag, gate the DLLs and the pairing, prune, package (the
   skill's Phase 8)" with the step 2 gate description, then 3c747f4fb's
   three prune bullets with 1.4.x facts: TAOM outside `bin/` keeps what `Main/_Module/` holds at the tag
   (`git ls-tree -r --name-only vX.Y.Z -- Main/_Module`), compared case-insensitively (the tag spells
   `GUI/PreFabs/`), leaving `RuntimeDataCache*` to the packager, and removing a leftover `taom_test_` file;
   TAOM.Dependencies outside `bin/`: prune nothing (MCM's install-only assets, module-dependencies.md
   "Five folders"); `bin/<platform>/` of both: keep a name the tag tracks under `_Module/bin/` (2 files for
   TAOM, 36 for TAOM.Dependencies) or the build writes (TAOM: `TAOM.dll`, `TAOM.pdb`, `DryIoc.dll`,
   `Newtonsoft.Json.dll`, `System.Runtime.CompilerServices.Unsafe.dll`; TAOM.Dependencies:
   `TAOM.Dependencies.dll`, `TAOM.Dependencies.pdb`, `0Harmony.dll`, `Bannerlord.UIExtenderEx.dll`,
   `MCMv5.dll`, `System.Runtime.CompilerServices.Unsafe.dll`), in the folders the `MirrorWin64ShippingClientTo*`
   targets of each csproj write; remove anything else, `Gaming.Desktop.x64_Shipping_Client` folders and
   retired binaries included; `.pdb`, `.exp` and `.lib` may stay (never shipped). Then e4c2c159a's report
   paragraph rewritten for 1.4.x (module sacks are listed and ship).
4. "Resolving a crash report to a commit": add ff84e1b88's paragraph on the bundle's `Build:` and
   `TAOM build:` lines, then "Reading the `[BuildStamp]` line": each DLL's stamp is followed by
   `config=<configuration> jitOpt=<ON|OFF|unknown>`; `(pair OK: both built from <sha>)` means one clean
   commit; `MISMATCH ... from different commits (<a>, <b>)` means the modules were not installed from one
   release. A log ending without `[Shutdown] clean module unload` ended in a crash or a kill; the `[Engine]`
   line names the Bannerlord build and every active module with its version.
5. New section `## TAOM_Map and LOTRLOME_Armory ship with a recorded identity`: both are unversioned
   installs outside git; each release records their version and ModuleData fingerprint (the `modules:`
   line) and the line's approved copies live in `tools/release_pins.json`; a `PAIRING SIGNAL` such as
   `taom_body_length` means a 1.5.x-era Armory and is named in the release note, never allowlisted (D15);
   `VILLAGE` findings mean TAOM data names villages the map lacks (`recruitment_pools/gondor.json` names
   four); `HIDEOUT` findings predict the new-game hideout throw WP112 guards.

Commit: `docs(release): v2.0.28 - build, gate and pair a 1.4.x release`. `Ported-from:` one line each for
ff84e1b88fe01e21fabc920dc8b922c1c474a270, db1a7166b974b4d6b927fadfb0fd077278e914e9,
d5033e4e897f3a0500ec5808ba495d8217211c42, 9cedb92df241a24a2ba65d5aa3f5dc47805484b1,
3c747f4fb7ff809c66697b62f00ad71a67a51de5, 9ca99437fc3879c9a981d8430ff8d44873c209a0,
e4c2c159a93fd8dd5680638e0c4da664cbf33f9e, a4b5d625863f80f542eb8240ee061ae16386e9f3,
e6ee8c9985beee1b63dafb2cda11c1a9b5879ae4, 715530875ca12d3f3058aa7cc748738f73ca5745,
709649c3f920dfe86d8cfc1ee0d09a835f2c7eec.

**Verify**: Docs lint shows no new finding in the two files;
`grep -c -E "require-build|check_release_pairing|v2\.4\.0|taom_test_|ingame_ledger" .claude/skills/release/SKILL.md`
prints 5 or more; `grep -c -E "ShaderCacheKeeper|TAOM\.Client|xinput9" .claude/skills/release/SKILL.md docs/reference/release-process.md`
prints 0 for each file; `grep -c "description:" .claude/skills/release/SKILL.md` prints 1.

### Part C done criteria

- [ ] Build exit 0; Test 0 failed; total = Part A total + 1.
- [ ] `python -m unittest tools.tests.test_ingame_ledger tools.tests.test_check_release_pairing tools.tests.test_package_release` prints `OK`; full tool tests show no new failure against step 0.
- [ ] Docs lint: no new finding in touched files; no em or en dash in any added prose line.
- [ ] `git diff --name-only bannerlord-1.4.5...HEAD` lists only in-scope files; 16 commits, every subject
      carries `v2.0.28`, none stages `CHANGELOG.md`: `git log --format=%s bannerlord-1.4.5..HEAD | grep -c "v2\.0\.28 - "`
      prints 16; `git diff --name-only bannerlord-1.4.5...HEAD -- CHANGELOG.md` prints nothing.

## Disposition (every commit in the pack, plus the shared hunks)

| 1.5.x commit | Subject (short) | Disposition | Where |
|---|---|---|---|
| c80006994 | log names its engine and its ending (#481) | ported (cherry-pick -n; CHANGELOG and lessons dropped) | A1 |
| de385cab9 | TAOM.Dependencies listed first | adapted by hand, 1.4.x pins kept | C1 |
| 743ce2e1d | build stamp in crash bundles | ported | A2 |
| 057628a2b | dirty and git-less stamp | end state, maintainer-applied | B1 |
| 5d1b1bda1 | refuse a dirty or off-tag DLL | end-state transplant | B2 |
| ff84e1b88 | build and package at the tag | adapted: crash-report.md row (A2), release docs (C5); CHANGELOG dropped (D1) | A2, C5 |
| db1a7166b | review follow-ups, plan 017 | split: C# comments and test rename (A2), tool (B2), docs (C5); review report, prompt, RCA, REVIEW-LOG and lessons dropped (optional records) | A2, B2, C5 |
| d5033e4e8 | convergence fixes, plan 017 | test fix (B2), doc step (C5); review report dropped | B2, C5 |
| 9cedb92df | untracked files mark dirty | props end state (B1), doc (C5); CHANGELOG dropped | B1, C5 |
| 3c747f4fb | prune only what the tag owns | props comment (B1), prune text recounted for 1.4.x (C5) | B1, C5 |
| 9ca99437f | compare the prune case-insensitively | doc (C5) | C5 |
| ffa80908a | plan 017 merged | skipped: 1.5.x plans bookkeeping | none |
| 6be50e8f7 (merge) | plan 017 merge | nothing to port: its own resolutions touch only CHANGELOG, lessons, LESSONS-LEARNED, REVIEW-LOG and crash-report.md (remerge-diff); code end state = 9ca99437f | B1, B2 |
| e4c2c159a | sack and JIT reports | partial: reports ported, module-sack exclusion not (no v1.4.8 Kit-sack test); plans/035 and review records dropped | B3 |
| 9c310bc23 | Bannerlord v1.5.4, NativeSkinFixes removed | partial: runtime-library notices and rows (C2), RETIRED_BINARIES mechanism with the 1.4.x list (B4, RELEASE-INTEGRITY-05); engine bump, NativeSkinFixes removal and the rest skipped (1.5.x-only) | B4, C2 |
| 5b241c46d | remove native DLL check command | skipped (SKIP-1.5-ENGINE per pack) | none |
| eed20591a | yotthani's HoN code is MIT | partial: notice, register rows and sections, a companion-tactics sentence; shader-compile-notice.md, adopt-yotthani-2026-10-08.md and the external-repo-adoption line skipped (absent or 1.5.x-only on 1.4.x); the `Yotthani bannerlord repository` register row skipped too (1.4.x has no such row, and it is a comparison-only record of facts in docs absent here: `scripted-melee-strikes.md`, `head-mesh-and-groom-authoring.md`), its `yotthani/bannerlord` token travels in the HoN row | C2 |
| 46fe58b7d | record two adoption fixes | skipped (bookkeeping) | none |
| a4b5d6258 | refuse Custom Battle test troops | hand-inserted | B4, C5 |
| e6ee8c998 | drop Game Pass, guard VS deploys | partial: csproj staging and fail target (B6), packager Game Pass refusal re-implemented (B6), manifest check re-implemented (B7), docs (B6, C5); TAOM.Client guard and ClientDeploymentTests skipped (D7) | B6, B7, C5 |
| 715530875 | release lists owed in-game checks | adapted: tag-to-HEAD reference filter replaces the date cutoff; register row, adoption bullet and review record kept | C3, C5 |
| d99398f12 (shared) | missing_armory_shaders gate | gate re-implemented for D12 (taom_ always refused, right-anchored parse, fail closed, `--game-root`); triage hunk is WP113's; ShaderSourceInstall not ported (D12) | B5 |
| 709649c3f (shared) | release pre-flight 5 | the `taom_test_` pre-flight item ported; its other hunks belong to WP143, WP136 or are OUT | C5 |
| 84f0459bc | bow shader sources | not ported (D12); only its `--game-root` option name is mirrored in B5 | none |
| 6eb2e1b90 (named by RELEASE-INTEGRITY-01) | per-behaviour tick profiler | not ported here: WP117 owns it; A3 and B3 make the JIT state visible without it | none |
| 0799af376 (K2, named by the pack's pairing note) | Feral Orcs | content skipped; the live-den rule is re-implemented against the shipped package as `HIDEOUT` (C4); its test `BanditHideoutLiveDataTests` is WP112's | C4 |
| 4b207f951 (K2, named by the pack's pairing note) | Dorwinion and South Rhovanion | content skipped; `VILLAGE` (C4) calls the 1.4.x `amv.check`; the tool's culture and bound checks are WP141's | C4 |
| 62157c7e7 (hand-off) | channel numbers, version bumps | not here: WP142 owns it; D8's refusal is B7 | none |

New work in this plan: A3 (config and JIT tokens, RELEASE-INTEGRITY-01), A4 (commit-aware pairing,
RELEASE-INTEGRITY-02), B7 (D8 release-number gate), C4 (pairing check and pin, RELEASE-INTEGRITY-08,
XML-DATA-04 with D15), the C2 licence test, the C3 line filter. RELEASE-INTEGRITY-03 is covered by plan 017
(B1, B2, A2), c80006994 (A1) and D8 (B7, C5); RELEASE-INTEGRITY-05 by B2 to B7 (AssetSources and PDB/EXP/LIB
were already excluded on 1.4.x; the untracked-file refusal is WP140's).

## Test plan

- C#, 32 new tests: A1 14, A2 4, A3 6, A4 7 (BuildStampReportTests model: the `Classify_*` tests), C2 1
  (model: `ThirdPartyLicenses_NameTheActuallyShippedVersions`). Expected total: baseline + 32.
- Python: `test_package_release.py` (plan 017 tests, e4c2c159a's with the sack tests rewritten, B4, B5 9,
  B6 5, B7), `test_ingame_ledger.py` (715530875's plus 4 filter tests), `test_check_release_pairing.py` (new).
- Untestable here (for `Not-tested:`): the `[Engine]` and `[Shutdown]` lines and `IdentityCollector` in game,
  `FailOnGamePassStagingInModule` in a deploying build, both gates on a real channel folder, a packaged build played.

## Done criteria (whole WP)

- [ ] Parts A, B and C done criteria all hold on the branch tip; `python tools/package_release.py --help`
      lists `--require-build` and `--game-root`; `check_release_pairing.py --help` and `ingame_ledger.py --help` exit 0.
- [ ] The hand-off reports the status of each Part, every commit hash, the final C# and tool test totals, the B4, B5 and C4 live outputs, and any owed `/deep-review`. The integrator records them in `plans/README.md` (row 115); no older plan's row is affected.

## STOP conditions

Stop and report (do not improvise) if:

- A step-0 precondition fails (WP107, WP108, WP109 or WP102 not merged), or
  `git diff 1b00d1acd -- tools/package_release.py tools/tests/test_package_release.py Directory.Build.props`
  prints anything before B1 or B2 (the transplant assumes the 1.4.x files).
- A cherry-pick conflicts in a code path the step does not name, or a resolution would need code the
  step does not describe.
- The build fails after A1 or A2 with a missing type or member (a hidden 1.5.x dependency).
- `ModuleHelper.GetModuleInfo`, `GetActiveModules`, `ModuleInfo.Id` or `ModuleInfo.Version` differ from the
  signatures in Current state (`pwsh -NoProfile -File tools/taom-src.ps1 path TaleWorlds.ModuleManager.ModuleHelper`).
- After B1 the built DLL's stamp has no `+<40-hex>` (the SDK set no `SourceRevisionId`), or the dirty probe
  does not produce `.dirty`.
- The maintainer has not applied B1: do not write `Directory.Build.props` yourself and do not apply it
  with git; hand off at the Part A boundary.
- `python -m unittest tools.tests.test_package_release` fails right after the B2 transplant, before any edit.
- WP109's THIRD-PARTY-LICENSES.txt section is shaped so that step C2's two cases do not fit.
- A hook denies a commit whose subject carries `v2.0.28`, or asks to confirm a git command this plan gives.
- Any step seems to need `Main/IoC.cs`, `.claude/settings*.json`, `docs/adrs/*`, ModuleData XML, the game
  install, or a tool run with `--apply` or `--record` on real data.
- A verification fails twice after a reasonable fix attempt.

## Maintainer steps

- **M1 (during Part B)**: in the WP worktree run
  `git checkout 9ca99437fc3879c9a981d8430ff8d44873c209a0 Directory.Build.props` (step B1). WP122 later
  edits the same file on top of this target.
- **M2 (before the next deploying build in any checkout, after this WP merges)**: delete
  `Main/_Module/bin/Gaming.Desktop.x64_Shipping_Client/` and `Dependencies/_Module/bin/Gaming.Desktop.x64_Shipping_Client/`
  in every checkout (both exist in this laptop's main checkout), and `Modules/TAOM/bin/Gaming.Desktop.x64_Shipping_Client/`
  and `Modules/TAOM.Dependencies/bin/Gaming.Desktop.x64_Shipping_Client/` in every install and channel
  folder; also delete `BehaviorTrees.dll` and `BehaviorTreeWrapper.dll` from every TAOM `bin` folder.
  Until then `./build.ps1` stops with "Game Pass is not supported" and the packager refuses.
- **M3 (before the first v2.4.0 release, desktop)**: choose the TAOM_Map and LOTRLOME_Armory the 1.4.x
  channel ships. Recommendation: the public 2026-09-29 Armory and the public channel's TAOM_Map (what
  players run and what the #697 troop tree was built for), after setting their `<Version>` to the release
  number; never the live desktop TAOM_Map (exported after 2026-10-29, it carries 1.5.x ids). Run
  `python tools/check_release_pairing.py --modules "<channel>\Modules" --game-root "<game>"`; if it reports
  `VILLAGE` findings, the map lacks the 15 table villages that `recruitment_pools/gondor.json` partly names:
  decide whether to add them (`tools/add_map_villages.py` on a 1.4.x map) before pinning. Then
  `--record tools/release_pins.json` and commit that file on its own.
- **M4**: the orchestrator creates the issue for the new work before this branch merges.
- **M5**: the first release after this WP runs the new Phase 8 end to end (build at the tag, both gates,
  prune, package, re-gate the channel folder).

## In-game checks owed (maintainer)

| Check | Where to look | Pass |
|---|---|---|
| Engine line | `Logs/taom_debug_*.log`, top lines | an `[Engine] Bannerlord=v1.4.8... modules(N)=[...]` line beside `[BuildStamp]`, naming TAOM_Map and LOTRLOME_Armory with versions |
| BuildStamp tokens | same log, `[BuildStamp]` line | `config=Debug jitOpt=OFF` after each stamp and `(pair OK: both built from <sha8>)` for a paired build |
| Shutdown line | end of the log after quit to desktop; then after killing the process | present after a quit, absent after a kill |
| Crash bundle | trigger a bundle with the QA dev trigger in MCM | the zip holds `diag.log`; `report.txt` Identity has `Build:`; `manifest.txt` has `TAOM build:` |
| Log retention | `Logs/` after more than 10 launches | up to 30 `taom_debug_*.log` kept |
| Game Pass target | `./build.ps1` in a checkout that still has the staging folder, then after M2 | first fails with the Game Pass message, then succeeds and no Gaming.Desktop folder reappears under `<game>/Modules/TAOM/bin` |
| Release gate | Phase 8 dry runs on the install and on the channel folder | exit 0 with `build stamp OK`; the pairing check exits 0 |

## Maintenance notes

- **Format contract.** The `[BuildStamp]` line keeps `TAOM=<ver> build.<time>Z+<sha>[.dirty]` followed by
  a space, `config=` and `jitOpt=` tokens after each stamp, and the ` MISMATCH — built <gap> apart` prefix.
  WP139's `ingame_verdict.py` and WP117's `perf_runs.py` match these; test A3.6 pins the first. WP139's pack
  cites `BuildStampReport.cs:146`; the line number moves, the format does not.
- **WP118** needs a guard against a stale TAOM.Dependencies.dll for its XML-merge patch (V08): use a
  capability probe as V08 describes; `BuildStampReport.TryParseCommit` and the verdict text are available
  for its log line.
- **WP122** edits `Directory.Build.props` after B1; **WP140** adds the untracked-module-file refusal on top
  of B2 to B7; **WP139** builds on `tools/ingame_ledger.py`; **WP113** also edits `Main/TAOM.csproj`
  (WinForms references, another region).
- **WP142** (62157c7e7) owns the release-number channel check (Phase 1 item 4b), the Dependencies-version
  policy and the `v2.0.29.5` phantom-version record; it must also add `tools/README.md` rows for
  `tools/ingame_ledger.py` and `tools/check_release_pairing.py` (1.4.x-only: "checks that the TAOM_Map and
  LOTRLOME_Armory a release ships pair with this TAOM; pins their ModuleData fingerprints in
  tools/release_pins.json"). **WP143** must keep this plan's 1.4.x release flow when it syncs the release skill.
- **Deferred, on purpose**: a Dependencies tree hash in both assemblies (issue #593) would make the
  runtime pairing exact across docs-only commits; with `--require-build` forcing one build per release the
  gain is small, so it waits. A `Settlement.` reference sweep of TAOM data against the shipped TAOM_Map
  would generalise the VILLAGE check; it belongs with WP136's validator codes. If 84f0459bc
  (ShaderSourceInstall) is ever ported, B5's unconditional `taom_` refusal must change to 1.5.x's rule. If a
  v1.4.8 test shows the game uses a Kit-built module sack, revisit B3's shipping policy.
- **Reviewer focus**: A1's two `SubModule.cs` blocks sit in the right places and swallow everything; the
  A4 verdict never says `pair OK` for a flagged stamp; B3 removed every trace of the exclusion; B5 fails
  closed in each no-check case; B7's manifest comparison casefolds and tolerates a BOM; C4 never writes
  anything without `--record` and never treats a signal as an error.
