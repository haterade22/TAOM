# Deep dive: release and build integrity of the public 1.4.8 build

Run: 2026-11-04-port148. Lens: release and build integrity. Read-only investigation.
Refs: working tree = c5b84fb41; 1.4.x = origin/bannerlord-1.4.5 (1b00d1acd); public build =
origin/fix/public-armory-ids-145 (8df90444c); 1.5.x = origin/bannerlord-1.5.x (bf3299143).

Findings were appended as they were confirmed (numbered in discovery order; the ranked table is in the Summary at the
end). Status: COMPLETE.

## RELEASE-INTEGRITY-01: the public TAOM.dll and TAOM.Dependencies.dll are unoptimized Debug builds, and no player log says so

**Severity** P2 (constant CPU tax on every TAOM code path; not by itself a freeze cause). **Confidence** high.

**Evidence**
- `build.ps1:4` `[string]$Configuration = "Debug"` and `build.ps1:27` `dotnet build "TAOM.sln" -c $Configuration`
  (working tree c5b84fb41; the public branch's three commits touch no build input, see RELEASE-INTEGRITY-04).
  `Main/TAOM.csproj` and `Dependencies/TAOM.Dependencies.csproj` set no `Optimize` or `DebugType` (grep, 0 hits).
- Read this session with System.Reflection.Metadata (scratchpad script `ri_scripts/read_asm_attrs.ps1`, read-only)
  from this laptop's install `Modules/TAOM/bin/Win64_Shipping_Client/TAOM.dll`:
  InformationalVersion `build.20260930-120651Z+8df90444cc2f1e6d83bb95d2164ab764b4fcae65`, byte-for-byte the stamp
  the Harad player's log prints (E3 line 2, E4 line 2); `AssemblyConfiguration` = `Debug`;
  `DebuggableAttribute` blob `01-00-07-01-00-00-00-00` (DebuggingModes 0x107: Default, IgnoreSymbolStoreSequencePoints,
  EnableEditAndContinue, DisableOptimizations). The laptop's `TAOM.Dependencies.dll` reads the same (Debug, 0x107).
  The 3rd-party DLLs beside it (0Harmony 2.4.2.0, UIExtenderEx 2.13.2, ButterLib 2.11.0, MCMv5 5.12.1) read
  `Release`, blob `01-00-02-00-...` (0x2).
- `Modules/TAOM.zip` on this laptop (894,846,480 bytes, 2026-09-30 08:32) holds a `TAOM/bin/Win64_Shipping_Client/TAOM.dll`
  of the same size (3,230,720) and time (2026-09-30 07:06) as the installed one.
- 1.5.x `docs/reference/release-process.md:77`: "TAOM ships Debug builds on purpose because crash reports need every
  frame; make no Release build without a measured player-facing gain (Mike, 2026-09-30)." This is a maintainer
  decision; this finding does not overrule it.
- No `#if DEBUG` / `[Conditional("DEBUG")]` exists anywhere under `Main/` or `Dependencies/` at c5b84fb41 (grep, 0 hits),
  so a Release build would change no registration: the dev behaviours in RELEASE-INTEGRITY-06 are on in ANY configuration.
- E3/E4 never state the configuration: grep for `jit|optimi|debuggable|Release build|Debug build` = 0 hits, and E4 has
  no managed stack trace at all (0 `:line N` frames), so even PDB line numbers cannot be used as a tell.

**Mechanism** The .NET Framework JIT reads the assembly-level `DebuggableAttribute`; with tracking plus
DisableOptimizations it compiles every TAOM method unoptimized (no inlining, no enregistration, no bounds-check
elimination, locals kept alive to the end of the method so the GC sees longer object lifetimes). That applies to every
Harmony prefix, postfix and transpiled body TAOM injects into engine hot paths (agent damage, mission ticks, formation
orders on the async AI thread, campaign hourly and daily ticks, GameModel overrides), the creature behaviour trees and
every always-on diagnostic. The size of the tax for TAOM is NOT measured (UNVERIFIED); the general .NET expectation is
a small factor on compute-bound managed loops and near zero on code that waits on native calls. It cannot explain the
E3 render-wait freeze (that wait is native), but every frame-time number taken from a public build measures Debug code.

**How it would show / can the logs tell** Not today. It shows only in DLL metadata (as read above), in the 1.5.x
packager's per-DLL `JIT optimization` line (e4c2c159a, package time), or in the 1.5.x MissionPerf context line
(`Main/Features/MissionPerf/Hooks/PerfContextReader.cs:36` on origin/bannerlord-1.5.x reads
`DebuggableAttribute.IsJITOptimizerDisabled` at run time). Nothing on the 1.4.x line reads either attribute.

**Fix direction**
1. Make the state visible without changing it: append `config=<AssemblyConfiguration> jitOpt=<ON|OFF>` for both
   assemblies to the `[BuildStamp]` line (`Main/Core/Diagnostics/BuildStampReport.cs:146`), two attribute reads at boot,
   unit-testable in the existing `BuildStampReportTests`. Then every taom_debug log and crash bundle answers question 2.
2. If the maintainer wants the measured gain his rule asks for: A/B the same saved battle with a Debug and a Release
   TAOM.dll under the 1.5.x mission tick profiler (plan 028, 6eb2e1b90) once it is ported, and compare per-behaviour
   frame time. A Release build keeps line numbers if `DebugType` portable PDBs ship; it loses inlined frames from
   crash stacks, which is the cost the maintainer named.

**1.5.x commits** e4c2c159a (plan 035, packager JIT report, package time only); 6eb2e1b90 / plan 028 (MissionPerf,
whose `PerfContextReader` logs the JIT state at run time). Neither changes the shipped configuration; nothing on 1.5.x
adds the state to `[BuildStamp]`.

**Verify how** Read-only: rerun `ri_scripts/read_asm_attrs.ps1` against the channel folder's DLLs on the desktop
(`E:\LOTRAOM_Releases\public\Modules\TAOM*\bin\Win64_Shipping_Client\`). Unit test for fix 1. In-game A/B for fix 2.

## RELEASE-INTEGRITY-02: the BuildStamp MISMATCH in E3/E4 is a stale, hand-carried TAOM.Dependencies module; harmless this time only because its sources had not changed, and the time-only check cannot tell the difference

**Severity** P2 (a real #371 bind-pose regression the moment any Dependencies change is ported; today a false alarm that
misdirects triage). **Confidence** high on the mechanism; the channel-assembly step itself is UNVERIFIED (the channel
folder is on the desktop).

**Evidence**
- E3 line 2 and E4 line 2: `TAOM=... build.20260930-120651Z+8df90444... TAOM.Dependencies=v0.1.0.0
  build.20260914-032050Z+0c32343758... MISMATCH — built 16d 08h 46m apart` (quoted from the log, ellipses mine).
- What the check compares (`Main/Core/Diagnostics/BuildStampReport.cs`, working tree): `TryParseStamp` (106-125) slices
  only the `yyyyMMdd-HHmmss` timestamp; `Classify` (56-61) compares the two timestamps against `MismatchTolerance` 1 h and
  `RebuildTolerance` 12 h. The commit SHAs are printed (146) but never compared, and nothing compares module
  `<Version>` values or Dependencies content. The line is log-only (`Main/SubModule.cs:120-127`), never on screen.
- 8df90444c was committed 2026-09-30T07:06:43-05:00; TAOM.dll is stamped 12:06:51Z = 07:06:51-05:00, eight seconds later.
  0c3234375 ("fix: resolve warg bite clip issue...") was committed 2026-09-13T21:04:19-05:00 on the 1.4.x line, before the
  fork; the player's TAOM.Dependencies.dll is stamped 03:20:50Z = 22:20:50-05:00 that night.
- This laptop's own build of 8df90444 deployed a PAIRED Dependencies: installed `TAOM.Dependencies.dll` reads
  `build.20260930-120652Z+8df90444...`, one second after TAOM.dll (read this session). So the stale Dependencies the
  player runs did not come from the 8df90444 build.
- `Modules/TAOM.zip` (the 2026-09-30 08:32 artefact on this laptop) holds 2,547 entries, every one under `TAOM/`, and no
  `TAOM.Dependencies/` entry. Its `TAOM/SubModule.xml` reads `<Version value="v2.0.28" />` and pins
  `DependedModuleMetadata id="TAOM.Dependencies" ... version="v2.0.6"` (lines 6 and 30 of that entry).
- `git diff --stat 0c3234375 8df90444c -- Dependencies/ Directory.Build.props GameReferences.targets Stubs/` is EMPTY;
  `git log 0c3234375..8df90444c -- Dependencies/` is empty (15 commits in the range). The player's Dependencies DLL is
  therefore source-identical to what 8df90444 would build.
- The predicted failure did not happen: E3 lines 94-104 show every TableauDiag patch category `applied OK` and the CC
  transpiler `APPLIED`, so the HeroRace preview patches attached despite the MISMATCH wording "expect the preview
  patches to fail".
- The bundled third-party stack the player loads (E3 lines 44-55: 0Harmony 2.4.2.0, ButterLib 2.11.0.0 with
  `Implementation.1.4.5`, UIExtenderEx 2.13.2.0, MCMv5 5.12.1.0, MBOptionScreen v1.4.5 5.12.1.0) matches the 1.4.x line:
  Harmony, UIExtenderEx and MCM are the csproj pins (`Dependencies/TAOM.Dependencies.csproj:70-72`), and ButterLib and
  MBOptionScreen are the copies vendored under `Dependencies/_Module/bin/Win64_Shipping_Client/` (unchanged between the
  two commits, per the empty diff above). Nothing stale reached players through them either.

**Mechanism** The 2026-09-30 public refresh replaced the TAOM module only (the zip is TAOM-only) and left the TAOM.Dependencies
module from the build of the night of 2026-09-13 in the channel. Because the 1.4.x stamp carries a build TIME, and the
check reasons from time alone, a module carried across two packages reads as #371 even when no input differs, while two
DLLs built within an hour of each other from different commits (or a dirty tree, see RELEASE-INTEGRITY-03) read
`pair OK`. The real danger is the next port: 1.5.x changed Dependencies inputs (6027ecfae BUTR refresh, d31026b5b,
2a3f71b12/plan 007 PatchShield, 5ee49d05f, 346f3e093, 6eb2e1b90, f6bf0e588, 5fcf8390c, e6ee8c998, among others in
`git log a4c6d7c30..origin/bannerlord-1.5.x -- Dependencies/`). A 1.4.8 release that ports any of them and is assembled
the same way ships a new TAOM.dll beside an old TAOM.Dependencies.dll, which is exactly the original #371 failure
(member resolution fails during patch application, preview patches never attach, characters render in bind pose).

**Fix direction**
1. Release rule: a 1.4.8 release ships TAOM and TAOM.Dependencies from the same build, every time, even when
   Dependencies did not change (the 1.5.x release-process.md table already says "Every release since v2.0.29, so all
   four modules read one version"). Gate it with the 1.5.x packager's `--require-build` (RELEASE-INTEGRITY-05).
2. Make the runtime check exact instead of temporal: compare the two SHAs first (equal SHA = paired whatever the time),
   and stamp the git tree hash of `Dependencies/` (plus `Directory.Build.props`) into BOTH assemblies at build time
   (`AssemblyMetadata`), so TAOM.dll records the Dependencies tree it was compiled against and the check compares
   that against what TAOM.Dependencies.dll carries. This is the content hash open issue #593 proposes (cited in 1.5.x
   plan 017's Maintenance notes). Keep the timestamp tiers only as the fallback when either hash is absent.
3. Until 2 lands, reword the MISMATCH tail so it does not assert a failure the log contradicts (for example name the two
   SHAs and say "rebuild both if the preview patches below did not apply").

**1.5.x commits** None fixes the check: 743ce2e1d and db1a7166b (plan 017) add the `.dirty` flag and a structured build
field in crash bundles; the 1.5.x `BuildStampReport.Classify` is unchanged (diff c5b84fb41..origin/bannerlord-1.5.x shows
only comment, `EngineReport` and visibility changes). The packager gate that would have refused this package is 1.5.x
`tools/package_release.py --require-build` (plan 017, extended later). Issue #593 (content hash) is still a proposal.

**Verify how** Unit tests in `TAOM.Tests/Core/Diagnostics/BuildStampReportTests.cs` for the SHA-equal and tree-hash
cases. Read-only on the desktop: `ri_scripts/read_asm_attrs.ps1` on `E:\LOTRAOM_Releases\public\Modules\TAOM.Dependencies\bin\Win64_Shipping_Client\TAOM.Dependencies.dll`
should print the `0c323437` stamp, confirming the channel holds the carried-over module.

## RELEASE-INTEGRITY-03: the public build's identity is unresolvable from git: v2.0.29.5 is in no commit, numerically follows a 1.5.2-only release, and the 1.4.x stamp cannot say whether the tree was clean

**Severity** P2 (triage of every public crash report starts wrong; no runtime cost). **Confidence** high.

**Evidence**
- Every TAOM module in the player's load order reads `v2.0.29.5` (E3 line 26 save ModuleCheck, E3 lines 35-42
  MissionDiag active modules; same in E4 lines 23 and 33-40). Git holds `<Version value="v2.0.28" />` at c5b84fb41,
  origin/bannerlord-1.4.5 and origin/fix/public-armory-ids-145 (`Main/_Module/SubModule.xml:6`), and
  `Dependencies/_Module/SubModule.xml:6` reads `v2.0.6` on all three. `git grep 2.0.29.5 origin/fix/public-armory-ids-145`
  finds nothing. The 2026-09-30 TAOM.zip on this laptop still says v2.0.28, so the number was typed into the channel
  folders afterwards (UNVERIFIED where; 1.5.x release-process.md:60-63 documents that the Map and Armory manifests are
  hand-edited "in the install before Mike's editor package; no commit records the change").
- 1.5.x `docs/reference/release-process.md:245-247` already records it: "v2.0.29.5 (patreon and public channels,
  Bannerlord 1.4.8). No commit on any branch holds this number. The channels' TAOM.dll stamp names 8df90444 (branch
  fix/public-armory-ids-145), whose <Version> in git is v2.0.28." The 1.4.x copy of that doc (177 lines) has no such entry.
- Tag `v2.0.29` = a12fec9b8 (2026-09-14), reachable only from 1.5.x refs; its release note
  (`docs/releases/v2.0.29-discord.md`) opens "Bannerlord v1.5.2 required ... It does not run on 1.4.8." So the 1.4.8
  public build carries a number that sorts after a 1.5.2-only release: `git show v2.0.29` and
  `git log --grep 'v2.0.29'` lead a triager to the wrong engine line.
- The `v2.0.28` label spans 116 commits from tag v2.0.28 (8ce42fe85, 2026-09-04) to 8df90444c and 118 to the
  origin/bannerlord-1.4.5 tip (`git rev-list --count`), because the 1.4.x hook-enforced subject label never moved.
- The 1.4.x stamp is `build.<time>Z+<sha>` with no dirty marker (`Directory.Build.props:23-24`, working tree). The
  `TaomStampWorkingTreeState` target that appends `.dirty` / `.nogit` exists only on 1.5.x
  (origin/bannerlord-1.5.x `Directory.Build.props:53-87`, plan 017, added by 057628a2b, tightened by 9cedb92df). So the public
  stamp `+8df90444` cannot prove the DLL holds only committed code. The branch shows why that matters: its RCA was run
  over "the working-tree removal of harad_howdah_crew and hill_troll" (`docs/reviews/rca-public-armory-ids-2026-09-30.md:3-8`
  on origin/fix/public-armory-ids-145), edits committed later as 10a52fb3d. Those landed before 8df90444c, so this build
  was probably clean, but a build made during that review would have carried the same unmarked stamp shape.
- The 1.4.x crash bundle's structured Identity names only `TaomVersion` from SubModule.xml
  (`Main/Features/CrashReport/Collectors/IdentityCollector.cs:15,22`), i.e. `v2.0.29.5` for every public player; the stamp
  survives only because `CrashBundleWriter.cs:45` copies the whole taom_debug.log, whose line 2 is `[BuildStamp]`.
- This laptop's installed TAOM_Map reads `v2.0.27` and LOTRLOME_Armory `v2.0.26` (their `SubModule.xml:4`), not the
  channel's v2.0.29.5, so a data result produced on this laptop does not describe the public build's Armory (the public
  branch's own RCA, finding 2, hit exactly this: "item refs to the new Armory could not be resolved on the laptop's old
  Armory").

**Mechanism** The 1.4.x contract ("`<Version>` changes only in a release commit, and that commit is tagged") was bypassed
by editing versions in the channel folders, and the 1.4.x `check-version-tagged.sh` only checks the version in the repo
(v2.0.28, tagged), so it stayed silent. Without a dirty flag, the SHA in the stamp is the only anchor and it is not proof.

**Fix direction**
1. Before the next 1.4.8 release, the maintainer picks the 1.4.8 line's numbering (open question below); whatever it is,
   it enters git in a `/release` commit on the 1.4.x branch and is tagged there, and no channel version is typed by hand.
2. Port plan 017 whole: the `TaomStampWorkingTreeState` target (057628a2b, 9cedb92df), the crash-bundle build field
   (743ce2e1d), the packager gate (5d1b1bda1) and the review and convergence fixes (db1a7166b, d5033e4e8, docs 9ca99437f;
   merge 6be50e8f7). `Directory.Build.props` is hook-protected, so the maintainer approves that edit.
3. Port the 1.5.x release-process.md "A number a channel carried before git did" section and the `/release` Phase 1
   item 4b (read every channel's `<Version>` before choosing a number) to the 1.4.x copies.
4. Port c80006994 (`[Engine]` line plus `[Shutdown]` clean-unload line, 30 retained logs). Its own body says "trunk
   (v1.4.8) still lacks this ... so its back-port is owed". A `[Shutdown]` line would have told us whether E3's log
   ended in a hang that the player killed or in a crash.

**1.5.x commits** 057628a2b, 9cedb92df, 743ce2e1d, 5d1b1bda1, db1a7166b, d5033e4e8, 9ca99437f, merge 6be50e8f7 (plan
017); c80006994 (#481); the release-process.md phantom-version
section (no single commit; present at bf3299143).

**Verify how** After 2: build a dirty tree and read the stamp (`.dirty`), unit tests from plan 017. After 4: a clean
exit writes `[Shutdown]`; a killed process does not.

## RELEASE-INTEGRITY-04: the code players run and the 1.4.x trunk have diverged both ways; a release cut from origin/bannerlord-1.4.5 alone would regress the public troop tree against the channel's Armory

**Severity** P1 for the next release (a wrong cut ships troops that name item ids out of step with the Armory players
hold, and drops a save-compat row). **Confidence** high on the divergence; the per-id impact is UNVERIFIED (needs the
channel's Armory, which is on the desktop).

**Evidence**
- `git log origin/bannerlord-1.4.5..origin/fix/public-armory-ids-145`: 92a30ba42, 10a52fb3d, 8df90444c (2026-09-30).
  `git log c5b84fb41..origin/bannerlord-1.4.5`: f8c9c8ecb (Load Game preview rider), 55d779012 (Custom Battle commanders,
  #709), d906fc921 (marketplace), ccef172ad (CI workflow removed), 1b00d1acd (1.4.8 supply, desertion and Uruk fixes).
  Neither head contains the other (merge-base is c5b84fb41).
- The public commits change 27 files (`git diff --stat c5b84fb41 origin/fix/public-armory-ids-145`): 17 troop XMLs
  (troops_gondor.xml alone 3,796 lines), the Gondor and enlistment equipment sets, `named_companions.xml`,
  `recruitment_pools/gondor.json`, one C# pool swap
  (`Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.Gondor.cs:141-143`: Glanhir now pools
  `gondor_ring_militia` and `gondor_ring_footman` instead of `gondor_ring_peasant` and the militia), its test, the RCA,
  CHANGELOG, and a 49-line tool-generated churn of `.serena/project.yml` (a personal MCP config that 1.5.x no longer
  tracks: `git ls-tree origin/bannerlord-1.5.x .serena/` is empty).
- Troop item references differ: troops/*.xml at origin/bannerlord-1.4.5 name 2,562 distinct `Item.` ids, at the public
  branch 2,617; 156 appear only in the 1.4.5 tip (for example `dale_halberd_b`, `dunland_caerdh_chainmail_lord_a`) and
  211 only in the public tree (for example `dunland_caerdh_helmet_heavy_a`, `ladder_blackroot_bow_t3`). Computed this
  session from `git show` of every troops file at both refs; the lists are in the scratchpad `ri_scripts/items_*.txt`.
- The public branch's RCA (`docs/reviews/rca-public-armory-ids-2026-09-30.md`) says the tree was copied from 1.5.x for
  the new Armory ids, and that `gondor_ring_peasant` was restored hidden only for save compatibility, pinned by
  `RetiredSaveCompatTroops_StillDefined_SoOldSavesResolveThem`.
- Read-only merge simulation (`git merge-tree --write-tree --name-only origin/bannerlord-1.4.5
  origin/fix/public-armory-ids-145`): one content conflict, `docs/reviews/lessons/data-content-cultures.md`.
  `Main/_Module/ModuleData/troops/troops_isengard.xml` was changed on both sides (1b00d1acd and 92a30ba42) and
  auto-merges, which proves nothing about its data.
- Commit 10a52fb3d's subject carries no version label, which the 1.4.x hook `check-commit-subject-version.sh` requires
  (made outside a Claude session, or the hook did not run).

**Mechanism** The channel holds the new Armory (its modules read v2.0.29.5) and the troop tree that matches it. The
1.4.x trunk holds the older tree. A `/release` run on the 1.4.x trunk (its Phase 1 requires branch `bannerlord-1.4.5`)
without the three commits would ship troops naming the 156 ids the public tree dropped. Any id the new Armory no longer
defines resolves to nothing: the silent naked or unarmed troop that CLAUDE.md's ladder-bow trap describes. It would
also delete `gondor_ring_peasant`, the #670 save hazard the public branch fixed. Folding the public commits in without
re-checking 1b00d1acd's Isengard edits is the opposite error.

**Fix direction** Before the next 1.4.8 release: merge origin/fix/public-armory-ids-145 into the 1.4.x trunk as its own
reviewed commit (drop the `.serena/project.yml` hunk), resolve the lessons doc, then gate the merged tree with
`python tools/validate_moduledata.py --game-modules <public channel Modules>` (`BROKEN_ITEM_REF`) and the troop
reference tests on the desktop, where the channel's Armory exists. The laptop's Armory (v2.0.26) cannot judge it.

**1.5.x commits** None applies directly; the tree itself came from 1.5.x (ee20755a and others named in the RCA). The
gate that would have kept a release from mixing heads by hand is 1.5.x `/release` Phase 8 (`--require-build <tag>`).

**Verify how** On the desktop, read-only: `validate_moduledata.py --game-modules E:/LOTRAOM_Releases/public/Modules
--code BROKEN_ITEM_REF` once against a worktree of origin/bannerlord-1.4.5 and once against the merge. The count of the
156 ids that break is the size of the regression avoided.

## RELEASE-INTEGRITY-05: the public package was hand-assembled with no packager and no gate; it ships 577 MB of Photoshop sources, debug files, a stray ModuleData.zip and retired binaries, and nothing on the 1.4.x line would have refused it

**Severity** P2 (download and install size, stale-binary and stale-module risk; the same absence produced -02 and -03).
**Confidence** high for what `Modules/TAOM.zip` holds; UNVERIFIED that the channel folder holds the same files.

**Evidence** (`Modules/TAOM.zip` on this laptop, 2,547 entries, 1,247,135,089 bytes uncompressed; entry metadata only)
- `TAOM/AssetSources/`: 34 `.psd` (323,583,940 bytes) and 52 `.png` (253,271,526 bytes), 46 % of the module. They are
  git-tracked (86 files under `Main/_Module/AssetSources`) and deployed because `Main/TAOM.csproj` sets
  `<ExcludeSourceFilesFromModule>false</ExcludeSourceFilesFromModule>` (its comment: deploy Assets, AssetSources and
  GUI/SpriteParts to the game folder). The 1.5.x packager excludes them (rule `ASSET_SOURCES`, "editor-only, never
  loaded at runtime"). The 1.4.x packager does not list `AssetSources` in `KNOWN_TOP_DIRS`
  (`tools/package_release.py:59-64`), so it would have reported them as unknown and refused.
- `TAOM/ModuleData.zip` (16,957,562 bytes) at the module root; `git ls-tree` finds no such tracked file, so it is a stray
  from the dev install (deploys never delete).
- 13 `.pdb`, `.exp` and `.lib` files, 22,004,154 bytes, including `TAOM.NativeSkinFixes.pdb` twice (8,605,696 bytes
  each). Both packagers exclude these (`NATIVE_DEBUG_EXT`, 1.4.x `tools/package_release.py:81`).
- `TAOM/bin/Gaming.Desktop.x64_Shipping_Client/` (TAOM.dll, TAOM.pdb, BehaviorTreeWrapper.dll and .pdb, 4,460,116 bytes)
  and `TAOM/bin/Win64_Shipping_wEditor/BehaviorTreeWrapper.dll`: a Game Pass folder and a binary retired on 2026-05-24
  (inlined into TAOM.dll, per `Main/TAOM.csproj`'s own comment). 1.5.x refuses both (`RETIRED_BINARIES`,
  `GAME_PASS_DIR`, e6ee8c998).
- `TAOM.NativeSkinFixes.dll` and `MinHook.x64.dll` ship although the feature's install call is commented out
  (`Main/SubModule.cs:629-635`), so they are inert payload. They are on the 1.4.x vendored allowlist, so this is weight,
  not a defect by the line's own rules.
- Not found (good): no `.bak` sidecars, no `RuntimeDataCache`, no `diag.log` or other runtime state, no `taom_test_`
  troops, no scene backups.
- The 1.4.x release procedure (`docs/reference/release-process.md:65-81`, `.claude/skills/release/SKILL.md` Phases 1-7)
  ends at tag and push. It never builds at the tag, never runs `tools/package_release.py`, and its #371 gate compares the
  two `<Version>` fields in the repo (`SKILL.md:51-60`), never the DLLs that ship. CI is gone from both lines
  (ccef172ad on 1.4.x and c8d69a0d on 1.5.x, both 2026-10-05), so the local `/verify` is the only gate left.

**Mechanism** Nothing between `build.ps1` (which deploys whatever the tree holds) and the editor package checks what
leaves the machine. Whatever the dev install accumulated rides along, and a module the refresh did not touch keeps its
old binaries (RELEASE-INTEGRITY-02).

**Fix direction** Port the 1.5.x release gate as one package of process work (all harness or tools, IN scope):
`tools/package_release.py` with `--require-build` (5d1b1bda1, db1a7166b), the `stale_module_files` untracked-file
refusal, `RETIRED_BINARIES` and the Game Pass refusal (e6ee8c998), the test-troop refusal (a4b5d6258), the JIT and
shader-cache reports (e4c2c159a), and the `/release` Phase 1 item 4b plus Phase 8 text. Adjust two 1.4.x facts while
porting: `TAOM.NativeSkinFixes.dll` and `MinHook.x64.dll` are still on the 1.4.x allowlist (1.5.x lists them as
retired), and `--require-build` refuses any tag whose `Directory.Build.props` lacks the dirty flag, so plan 017 must land
first. Skip the 1.5.x-only parts: `TAOM.Client` rules (03d9db8dc, e0a6970dd), co-op server skeletons (61276c182),
shader-source checks (84f0459bc, d99398f12) and ShaderCacheKeeper packaging (372773d1a), unless those features are ported.

**1.5.x commits** 5d1b1bda1 and db1a7166b (plan 017 gate), e4c2c159a (plan 035), a4b5d6258, e6ee8c998, and the
stale-file check (`stale_module_files`, 1.5.x `tools/package_release.py:550`).

**Verify how** `python -B -m unittest tools.tests.test_package_release` after the port; then a read-only dry run with
`--source` on the public channel folder should refuse this package (stray zip, retired binaries, Game Pass folder, and
the 0c323437 Dependencies stamp under `--require-build`).

## RELEASE-INTEGRITY-06: always-on diagnostics in the public build write 84 % of the log synchronously from the game thread, and one of them walks every agent every frame for the first 5 s of each mission; the named dev pieces are cheap

**Severity** P3 (measurable CPU and I/O on the main thread, large logs and bundles; not a freeze cause on the evidence).
**Confidence** high on what runs; the per-line cost is UNVERIFIED (no in-game timing).

**Evidence**
- `Main/Core/Logging/FileLogger.cs` (working tree) has no level filter: `LogDebug` queues for a background writer, while
  `LogInfo`, `LogWarning` and `LogError` call `Drain()` on the calling thread (lines 79-89), which takes `_writeLock`,
  writes every queued line and calls `Flush()` (93-119). Every INFO line is a synchronous write on whatever thread
  logs it, usually the game thread.
- E4 (35 min, 1,092,378 bytes, 4,123 INFO / 378 DEBUG / 3 ERROR lines): `[BattleLoad]` 3,536 lines and 509,518 bytes
  (46 %), of which the three per-agent phases `AgentEquipBegin`, `AgentEquipOk`, `AgentBuildDone` are 1,093 agents x 3 =
  3,279 INFO lines and 473,267 bytes; `[AutoResolve]` 225 INFO lines and 421,389 bytes (38 %), the longest line 5,205
  bytes. Both default ON: `BattleLoadDiagnosticsSettings.cs:21` `EnableBattleLoadDiagnostics = true`, and
  `Main/Features/TaomSettings.cs:457` `LogAutoResolvedBattles = true` (hint: "On by default"). The per-agent lines are
  emitted from Harmony patches on `Agent.EquipItemsFromSpawnEquipment` and `Mission.BuildAgent`
  (`Main/Features/BattleLoadDiagnostics/Hooks/Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.cs:46,87`,
  `Mission_BuildAgent_BattleLoad_Patch.cs:42`), and AutoResolve writes one JSON record per finished map battle,
  AI-only battles included (E3 lines 172-174 are AI-vs-AI field battles).
- Bound on the load-time cost: in E4's 534-agent tournament (`arena_empire_a`) the agents were built between
  `+109604` and `+110316` ms (lines 3456 to 5135), 0.71 s including 1,602 synchronous INFO lines, so the logging cost of
  that load is a fraction of 0.71 s on this player's machine.
- `Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs:40-45,69-92`: for the first 5 s of every mission,
  every `OnMissionTick` walks `mission.Agents` and, per agent, calls the native `agent.ActionSet.GetName()` and reads
  `agent.Name`, `Character.StringId` and `Monster.StringId` (strings) before the service dedups them. At 60 frames a
  second that is about 300 walks per mission start. 1.5.x plan 030 (e0ad0db76) keys the census on integers and reads
  names only for a new key.
- The pieces the brief named are not where the cost is. `CrashReportDevTriggerMissionBehavior.OnMissionTick`
  (`Main/Features/CrashReport/DevTriggers/CrashReportDevTrigger.cs:21-35`) is one cached settings read and three bool
  checks per tick; its sibling `CrashReportApplicationTickTrigger` (same folder, lines 17-32) is a postfix on
  `Module.OnApplicationTick` doing the same. The 33 `taom.*` console commands (the source declares 33 distinct names; E3
  line 18 confirms the engine registered all 33) cost
  nothing per frame and mutate state only behind `DevConsoleGuard` cheat gates
  (`Main/Features/DevConsole/DevConsoleGuard.cs:32-43`); `taom.rescue_time` and `taom.rescue_service` are player
  recovery tools. The one player-facing hazard: the MCM page shows a `QA — Dev Triggers` group with two toggles that
  crash the game on purpose (`Main/Features/CrashReport/CrashReportSettings.cs:43-51`). 1.5.x keeps them and adds a third.
- Evidence correction for the brief (fact 6): the `t=+111326ms` "slow town load" is the encounter clock. For
  campaign encounters `[BattleLoad]` starts its stopwatch at `EncounterStart` and only restarts at `MissionOpenNew`
  when it is not running (`BattleLoadDiagnosticsService.cs:101-121`, restart guards at 106 and 121, same code on 1.5.x).
  E4 shows the player in `GauntletInventoryScreen` from 20:46:39 to 20:48:15 (lines 3391-3418); the mission itself took 2.9 s from
  `LoadMissionBegin` (+108455) to `FinishMissionLoadingDone` (+111326). Any town or arena `t=` includes menu time.

**Mechanism** Diagnostics built to answer one-off investigations (#352 stalls, balance analysis, action-set mismatches)
ship enabled, and the logger makes every INFO line a synchronous file write so that a native crash cannot lose it.
That durability choice is sound for rare phase lines and costly for per-agent and per-battle volume.

**Fix direction**
1. Port plan 030 (e0ad0db76) for the MissionDiag census, the career hit lines and the dead per-frame patches it deletes
   (check each target exists on 1.4.x first; the banner colour store it deletes is in E3's behaviour list as
   `AgentColorStoreCleanupBehavior`, line 210).
2. Default `LogAutoResolvedBattles` to OFF, or delete the feature as 1.5.x did (6ae924638, an unrelated commit title).
   A default flip needs a renamed property: MCM persists per property, so the old `true` survives in existing
   `TAOM.json` files (CLAUDE.md ShaderPrecompilation trap; `.claude/rules/csharp-architecture.md` "When a compiled MCM
   default CHANGES").
3. Demote the three per-agent `[BattleLoad]` phases to DEBUG unless the previous session left a stale inflight marker
   (E3 line 17 shows the marker machinery already exists), so the durable path is used exactly when a crash is likely.
4. Print a mission-relative `tLoad=` beside `t=` from `MissionOpenNew`, so a town load is not read as 111 s again.

**1.5.x commits** e0ad0db76 (plan 030), 6ae924638 (AutoResolveDiagnostics deleted). None changes FileLogger's durable
INFO path or the per-agent BattleLoad lines (1.5.x `BattleLoadDiagnosticsService.cs:410,444` keep them on `Emit`).

**Verify how** After 1-3: a repeat of E4's session should log well under half the bytes; unit tests exist for each
service (`MissionDiagnosticServiceTests`, `BattleLoadDiagnosticsServiceTests`).

## RELEASE-INTEGRITY-07: the crash-capture sweep and PatchShield wrap the engine's 247 native-to-managed callbacks twice, and PatchShield's finalizer pays a method lookup and garbage on every call of every shielded method

**Severity** P2 (per-call CPU and allocation on the hottest managed entry points in battle, plus boot and first-load
time; a diagnostic and a robustness guard that the player cannot switch off). **Confidence** high on the wiring; the
magnitude on players' machines is UNVERIFIED (the measurements below are the maintainer's).

**Evidence**
- E3 line 4 and E4 line 4: `[CrashReport] Native2Managed: attached 247 Finalizer(s)`, one second after line 3 on this
  player's machine. `Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs:36-95` patches every static method of the
  `*CallbacksGenerated` types with a finalizer that binds only `__exception` (117-123).
- The v1.4.8 dump holds 238 `[MonoPInvokeCallback]` methods in those types
  (`C:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.MountAndBlade.AutoGenerated.cs` `CoreCallbacksGenerated` 111,
  `TaleWorlds.Engine.AutoGenerated.cs` `EngineCallbacksGenerated` 85, `TaleWorlds.DotNet.AutoGenerated.cs`
  `LibraryCallbacksGenerated` 42), among them per-agent hot paths: `Agent_OnAIInputSet` (line 651),
  `Agent_IsInSameFormationWith` (637), `Agent_GetWeaponInaccuracy` (631), `Agent_UpdateAgentStats` (745) and
  `ManagedScriptHolder_TickComponents` (Engine file, line 689).
- `Dependencies/Foundation/PatchShield.cs` (TAOM.Dependencies, working tree) shields every Harmony-patched method except
  three UI namespaces (lines 60-68: `TaleWorlds.GauntletUI`, `TaleWorlds.TwoDimension`,
  `TaleWorlds.MountAndBlade.GauntletUI`); nothing excludes `ManagedCallbacks`. Its finalizers take
  `MethodBase __originalMethod` (lines 244, 255), and its own comment (51-52) records that this makes Harmony's wrapper
  call `MethodBase.GetMethodFromHandle` plus a try/catch on every call. Pass 2 runs at the first game start
  (`Dependencies/SubModule.cs:288`), after TAOM patched the 247 shims, so each shim carries both finalizers.
- 1.5.x measurements: 5ee49d05f (plan 034) gives the old binding as about 64 ns and 241 bytes of garbage per call on one
  thread and about 1.1 microseconds per call with 8 threads calling at once, against 5.4 ns after the fix; plan 007
  records 372 attaches in pass 2, about 247 of them on the callback shims, about 69 s on the maintainer's slow machine
  and about 1.5 s "in the fast mode"; plan 006 records 29 to 33 s for the 247-method sweep on that machine and finds that
  its MCM toggle is read while MCM's settings object is still null, so `?? true` always wins
  (the working tree has the same shape at `Main/SubModule.cs:162` and `:168`, `?.EnableCrashCapture) ?? true` and
  `?.EnableNativeToManagedCapture) ?? true`, with the sweep at `:170`; whether MCM is null at that point on 1.4.8 is
  UNVERIFIED here, plan 006 measured it on the 1.5.x line).
- Agent work in single player runs partly on the asynchronous AI thread and its worker pool
  (`.claude/rules/harmony-patches.md` thread table, #592/#595), so some of these callbacks can be entered from several
  threads at once, the contention case 5ee49d05f measured. Which of the named callbacks the engine enters off the main
  thread is UNVERIFIED (native callers).

**Mechanism** Two safety nets stack on the same native entry points. The crash bridge is cheap per call; PatchShield's
`__originalMethod` binding is not, and it applies to every shielded method (5ee49d05f counts about 160 at the first game
start and close to 300 later), so every shielded call allocates. How much of the gen0 count comes from that cannot be
read from these logs: E3's `gc=` counters (line 217, `3166/785/81` at the first battle) are cumulative for the process.

**Fix direction** Port, in this order: plan 034 (5ee49d05f: finalizers take only the exception, the method is found from
the stack after a throw), plan 007 (2a3f71b12 with follow-ups 578ac7d61, 31a31f16b, 0bf2409e6, 900920dca, b0a93fb4c,
merge d90b4be31: PatchShield skips the callback shims TAOM's crash bridge already covers), plan 006 (merge 5f6a96d2e with
65b691b6a, 707275290, 42624b95b, 8e6b0935c, 8c84fa20e, 9e8c99bdb: the sweep shrinks to a six-entry allowlist, the
toggles work live, four dead finalizers go). Plans 007 and 034 change TAOM.Dependencies, so the release that carries
them must ship both modules from one build (RELEASE-INTEGRITY-02).

**1.5.x commits** as listed in the fix direction, plus 88408869f (shim comments after merging 006 and 007).

**Verify how** Unit tests from each plan; then on a deployed build, diag.log's pass-2 attach count drops by about 247,
and E3 line 4 reads six instead of 247.

## RELEASE-INTEGRITY-08: the public channel pairs the 1.4.8 TAOM build with an Armory carrying 1.5.x-era creature data, and no gate on the 1.4.x line compares the two

**Severity** P3 as measured (36 schema errors per E2 load, unused monster definitions); the class is P2 because nothing
bounds what else a 1.5.x-era Armory carries. **Confidence** high for the two files; UNVERIFIED for anything beyond them.

**Evidence**
- E2 (`rgl_log_22112.txt`): all 36 `Error:` lines are `The 'taom_body_length' attribute is not declared`, 24 from
  `..\..\Modules\LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_animalia.xml` and 12 from
  `.../lotr_monster_elk.xml`, validated against `../../XmlSchemas/Monsters.xsd` (lines 4027-4035 onward).
- The attribute is read only by 1.5.x code: `git grep taom_body_length` finds it in 1.5.x
  `Main/Features/Animalia/AnimaliaConfig.cs`, `Elk/ElkConfig.cs`, `MonsterSize/MonsterSizeConfig.cs`,
  `ElephantLike/ElephantLikeReach.cs` and `SubModule.cs`, and nowhere at c5b84fb41 or on origin/fix/public-armory-ids-145.
  Animalia, Elk and MonsterSize are feature folders new on 1.5.x (`features_new_on_15x.txt`). The public branch's own RCA
  reverted its two elk riders because the elk code "lives in Main/Features/Elk, Animalia and MonsterSize, all absent here".
- This laptop's Armory (v2.0.26, 425 ModuleData XML files) has no `lotr_monster_elk.xml` or `lotr_monster_animalia.xml`
  at all (its `Monsters/LOTR/` holds chariot, elephant, fell_warg, mumakil, spider, war_ram and warg) and no
  `taom_body_length` anywhere, so the channel's Armory is newer than the Armory this 1.4.x machine holds.
- v1.4.8 behaviour: `MBObjectManager`'s loader reads every module XML twice, the second time with
  `ValidationType.Schema`, and its `ValidationEventHandler` only prints (`C:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.ObjectSystem.cs:1397-1420`
  and `1618-1634`). The documents still load; the unknown attribute is ignored. So the direct cost is log noise and a
  set of monsters no 1.4.x troop uses.
- 1.5.x release-process.md (bf3299143) carries eight Armory and TAOM_Map co-shipping rules ("TAOM and TAOM_Map ship X
  together, or neither does" and kin), most with per-channel grep checks (lines 173-204). The 1.4.x copy has none.

**Mechanism** LOTRLOME_Armory and TAOM_Map are unversioned and live only in the installs (CLAUDE.md "A fix in a dependency
module" trap). The public package took its Armory from a dev install that was already tracking the 1.5.x line, while
TAOM came from the 1.4.x branch. With no Armory-to-TAOM compatibility gate on the 1.4.x line, the pairing is whatever the
packaging machine held that day.

**Fix direction** For each 1.4.8 release, record which Armory and TAOM_Map the channel ships (version plus a content hash
or a file list in the release note) and run `python tools/validate_moduledata.py --game-modules <channel Modules>`
against the release tree before publishing. Treat schema errors from the channel's modules as a release check (the
attribute-level ones are cheap to grep in a test load's rgl log). Decide explicitly whether the 1.4.8 channel ships the
1.5.x-era Armory (then the 1.4.x validators must accept its extra attributes) or a 1.4.x-frozen one.

**1.5.x commits** None for the 1.4.8 pairing; the 1.5.x release-process co-shipping rules are a template only.

**Verify how** On the desktop, read-only: diff `E:\LOTRAOM_Releases\public\Modules\LOTRLOME_Armory\ModuleData` against the
Armory a 1.4.x tree was last validated with, and grep the channel's `Monsters/` for other 1.5.x-only attributes.

## RELEASE-INTEGRITY-09: port the release process before the port wave: plan 020 removes the CHANGELOG conflict class and the 1.4.x hooks that would make every cherry-pick hand-edit it

**Severity** P2 (process cost and conflict risk across roughly 1,100 candidate commits; no player effect by itself).
**Confidence** high on the mechanics; the conflict count is the brief's, not re-measured here.

**Evidence**
- The 1.4.x line still enforces a hand-edited CHANGELOG: hooks `.claude/hooks/check-changelog-changed.sh` and
  `check-changelog-updated.sh` exist at c5b84fb41, and its CLAUDE.md says "CHANGELOG.md every session". 1.5.x retired both
  hooks in c91bb95f8 and moved the entry into the commit body (30b54df75), archived the hand-written file (ef5b7ff4e),
  added `tools/changelog_from_commits.py` (merge 55a729152, plan 020, 2026-09-25), and generates the section at
  `/release` (1.5.x release-process.md step 5).
- The brief's merge-tree simulation: 33 commits conflict only in CHANGELOG.md and CHANGELOG.md heads the bookkeeping
  hot spots of 305 docs-only conflicts.
- The 1.4.x subject hook `check-commit-subject-version.sh` requires the current label (v2.0.28); every cherry-pick
  carries a 1.5.x label (v2.0.29 to v2.0.36) and must be re-subjected. A relabelled subject also loses the 1.5.x label a
  reader would use to find the commit on the other line, so the original short hash belongs in the body.

**Mechanism** Pre-2026-09-25 1.5.x commits all touch CHANGELOG.md; post-plan-020 commits do not, but the 1.4.x hooks
would demand an entry for each. Porting plan 020 first turns the CHANGELOG conflict into a non-event and lets the next
1.4.8 release generate its notes from commit bodies (from tag v2.0.28, 118 commits back, plus the ports).

**Fix direction** First package of the port: c91bb95f8, 30b54df75, ef5b7ff4e, the tool and its tests from 55a729152,
and the follow-ups 6e4bfedb7, 971d8e975, 5b242cee6, adapted to the 1.4.x branch name. Then a port convention: subject
relabelled to the 1.4.x version, body keeps the 1.5.x text plus `Ported-from: <1.5.x short hash>`.

**1.5.x commits** c91bb95f8, 30b54df75, ef5b7ff4e, 6e4bfedb7, 971d8e975, 5b242cee6, 55a729152.

**Verify how** `python -m unittest tools.tests.test_changelog_from_commits`; `bash tools/test_hooks.sh` after the hook
retirement; a dry `changelog_from_commits.py --version <next>` run lists the 1.4.x commits since v2.0.28.

## RELEASE-INTEGRITY-10: the bundled BUTR stack runs builds made for game 1.4.5 on 1.4.8; 1.5.x refreshed it with 1.4.6 to 1.4.8 builds

**Severity** P3 (no failure seen in E1 to E4). **Confidence** high on what loads; UNVERIFIED whether any 1.4.8-specific
fix in ButterLib 2.12.0 or MCM 5.12.3 matters to TAOM.

**Evidence**
- E3 lines 44-55: `Bannerlord.ButterLib.Implementation.1.4.5 v2.11.0.0` and `Bannerlord.MBOptionScreen.v1.4.5 v5.12.1.0`
  load on `v1.4.8.119303`. This laptop's `TAOM.Dependencies/bin/Win64_Shipping_Client/` holds implementations 1.4.0 to
  1.4.5 only.
- 6027ecfae (1.5.x, 2026-09-14) "BUTR stack refreshed for the 1.5.x line": "both meta-loaders pick the highest build at
  or below the running engine, which is how 1.4.8 ran on the 1.4.5 builds"; it moves ButterLib to 2.12.0 with 1.4.6,
  1.4.7, 1.4.8 and 1.5.1 builds, MBOptionScreen and MCM.UI.Adapter to 5.12.3, NuGet Bannerlord.MCM 5.12.3 and
  UIExtenderEx 2.13.3 in both csproj files.
- E1 and E2 rgl logs show the BUTR assemblies loading and no BUTR error (grep `butterlib|mcm|uiextender|mboptionscreen`).

**Mechanism** A per-engine implementation built for 1.4.5 is used on 1.4.8 by design of the BUTR loader; it works until
an engine member it touches changed.

**Fix direction** Optional port of 6027ecfae. It changes TAOM.Dependencies (vendored DLLs, csproj pins), so it rides
only in a release that ships both modules from one build (RELEASE-INTEGRITY-02), and it owes an MCM and crash-reporter
smoke on 1.4.8 (its own Not-tested line).

**1.5.x commits** 6027ecfae.

**Verify how** After the port, E3's assembly list shows `Implementation.1.4.8` and `MBOptionScreen.v1.4.8`; MCM opens and a
dev-trigger crash produces a bundle.

## Cross-lens notes (outside this lens, recorded because they bear on the Harad report)

- **The E3 "freeze" has the signature of the shader-compile stall E4 survived.** E4's first tournament
  (`arena_aserai_a`): `WaitingForRender waitedMs=1 shaders=1569` at 20:21:20 (line 2125), then only background
  `[MemSample]` lines at 20:21:49 and 20:22:19, then `waitedMs=68290 shaders=0` at 20:22:29 (line 2131). The 1 Hz
  main-thread marker (`RenderWaitEmitIntervalMs = 1000`, `BattleLoadDiagnosticsService.cs:37`) was silent for 68 s and
  the game recovered. E3: `shaders=2697` at 20:02:50 (line 219), then only `[MemSample]` until 20:04:22, so the player
  stopped it 92 to 122 s into a wait for 2,697 shaders. At E4's rate (about 23 shaders a second) that wait needs about
  117 s. UNVERIFIED, but it matches "combat sounds continue" (audio thread alive, main thread blocked) and the stale
  inflight marker from the 19:34 session on the same scene. Whether a killed session persists any compiled shaders is
  for the shader lens. No `[ShaderPrecompilation]` line appears in E3 or E4 although `EnableShaderPrecompilation`
  defaults true (`Main/Features/TaomSettings.cs:403`).
- **Plan 035's "vanilla ships battle_terrain_031 header-only" does not hold on 1.4.8.** This laptop's v1.4.8 install has
  `Modules/SandBoxCore/SceneObj/battle_terrain_031/ShaderCache/D3D11/compressed_shader_cache.sack` (923,160 bytes) and
  no header-only SandBoxCore scene at all (202 scenes checked). The 1.5.x measurement was taken on the 1.5.x engine.
- **Plan 012 is not owed on 1.4.x unless 502f7cde9 is ported.** The per-frame `LOADING-WINDOW lowered` postfix it fixes
  was introduced on 1.5.x after the fork (502f7cde9, 2026-09-14); E4 has 0 such lines. Port 502f7cde9 only together with
  plan 012 (ed1732eb7 and its follow-ups 15e5475a4, a4492fa89).
- **Brief fact 6 is a misread clock**, corrected in RELEASE-INTEGRITY-06: the 533-agent "town load" spent 96 s in the
  inventory screen; the mission loaded in 2.9 s.

## Summary

**Answers to the four questions**

1. *BuildStamp MISMATCH (E3).* The check compares only the two build timestamps (`BuildStampReport.cs:56-61`), never the
   SHAs, module versions or Dependencies content. TAOM.dll was built from 8df90444 eight seconds after that commit; the
   public channel kept the TAOM.Dependencies module built on the night of 2026-09-13 from 0c3234375, because the
   2026-09-30 refresh replaced the TAOM module only (the laptop's 2026-09-30 TAOM.zip holds nothing but `TAOM/`, while the
   laptop's own deploy of 8df90444 has a paired Dependencies stamped one second later). No file under `Dependencies/`,
   `Stubs/`, `Directory.Build.props` or `GameReferences.targets` changed between the two commits, and the bundled
   Harmony 2.4.2, ButterLib 2.11.0, UIExtenderEx 2.13.2 and MCM 5.12.1 match what the 1.4.x line pins or vendors, so nothing stale reached
   players this time and the preview patches did attach (E3 lines 94-104). The risk is the next release: plans 007 and
   034 and the BUTR refresh 6027ecfae all change TAOM.Dependencies (RELEASE-INTEGRITY-02, -07, -10).
2. *Optimized Release build?* No. The installed TAOM.dll on this laptop carries the player's exact stamp and reads
   `AssemblyConfiguration Debug`, `DebuggableAttribute` 0x107 (JIT optimizer disabled); TAOM.Dependencies.dll the same.
   That is the maintainer's recorded policy (1.5.x release-process.md:77). The player logs cannot show it: no line names
   the configuration and E4 has no managed stack at all. Cheapest fix: add `config=` and `jitOpt=` to `[BuildStamp]`
   (RELEASE-INTEGRITY-01).
3. *What the next 1.4.8 release must fold in.* The three public commits merged into the 1.4.x trunk with the five trunk
   commits players lack (RELEASE-INTEGRITY-04); a numbering decision entered in git and tagged, instead of v2.0.29.5 typed
   into channel folders (-03); plan 017's dirty flag and crash-bundle build field, then the 1.5.x packager gate
   (`--require-build`, stale-file and retired-binary refusals, Game Pass refusal, test-troop refusal a4b5d6258, JIT and
   shader reports e4c2c159a) and `/release` Phase 1 item 4b and Phase 8 (-05); c80006994's `[Engine]` and `[Shutdown]`
   lines (-03); plan 020 ported before the wider port wave (-09); and an explicit Armory and TAOM_Map pairing check,
   since the public Armory already carries 1.5.x-era creature data (-08).
4. *Dev or debug behaviour costing performance.* The named pieces are cheap: `CrashReportDevTriggerMissionBehavior` is a
   cached read and three bool checks per tick, and the 33 console commands cost nothing per frame and are cheat-gated
   (their one hazard is two crash-on-click QA toggles in every player's MCM). The real costs are the always-on
   diagnostics written synchronously (per-agent `[BattleLoad]` lines and `[AutoResolve]` JSON, 84 % of E4's log), the
   MissionDiag census that walks every agent every frame for 5 s (-06), and the crash-capture plus PatchShield double
   wrap of the 247 engine callbacks with a per-call method lookup (-07).

**Ranked findings**

| Rank | Id | Sev | Title | 1.5.x commits to port |
|---|---|---|---|---|
| 1 | RELEASE-INTEGRITY-04 | P1 | Public build and 1.4.x trunk diverged; wrong cut regresses troop tree | none (merge the public branch) |
| 2 | RELEASE-INTEGRITY-07 | P2 | 247 engine callbacks double-wrapped; shield pays a lookup on every call | 5ee49d05f; 2a3f71b12 + follow-ups, d90b4be31; 5f6a96d2e + follow-ups |
| 3 | RELEASE-INTEGRITY-02 | P2 | MISMATCH is a carried-over Dependencies; time-only check | 5d1b1bda1, db1a7166b (gate); issue #593 not built |
| 4 | RELEASE-INTEGRITY-01 | P2 | Debug build, JIT optimizer off, invisible in logs | e4c2c159a (package report); 6eb2e1b90 (runtime context) |
| 5 | RELEASE-INTEGRITY-03 | P2 | v2.0.29.5 in no commit, sorts after a 1.5.2 release; no dirty flag | plan 017 (057628a2b, 9cedb92df, 743ce2e1d, 5d1b1bda1, db1a7166b, d5033e4e8; merge 6be50e8f7); c80006994 |
| 6 | RELEASE-INTEGRITY-05 | P2 | Hand-assembled package: 577 MB PSD/PNG sources, PDBs, stray zip, retired binaries | 5d1b1bda1, db1a7166b, e4c2c159a, a4b5d6258, e6ee8c998 |
| 7 | RELEASE-INTEGRITY-09 | P2 | Port plan 020 before the port wave | c91bb95f8, 30b54df75, ef5b7ff4e, 6e4bfedb7, 971d8e975, 5b242cee6, 55a729152 |
| 8 | RELEASE-INTEGRITY-06 | P3 | Always-on diagnostics: synchronous log volume, per-frame census | e0ad0db76, 6ae924638 |
| 9 | RELEASE-INTEGRITY-08 | P3 | Public Armory carries 1.5.x-era creature data; no pairing gate | none |
| 10 | RELEASE-INTEGRITY-10 | P3 | BUTR stack built for 1.4.5 runs on 1.4.8 | 6027ecfae |

**Ordering constraint for the planner:** RELEASE-INTEGRITY-02's release rule (ship TAOM and TAOM.Dependencies from one
build) must be in force before any port that touches `Dependencies/` (plans 007 and 034, 6027ecfae, d31026b5b and the
rest of `git log a4c6d7c30..origin/bannerlord-1.5.x -- Dependencies/`). Plan 017 must land before the packager's
`--require-build` can gate a 1.4.x tag.

**Not covered:** the desktop's channel folders (`E:\LOTRAOM_Releases\*`), so the channel's TAOM.Dependencies stamp, its
TAOM_Map and its full Armory contents are inferred, not read; diag.log (PatchShield timings), which the player did not
send; no dotnet build, test or in-game timing (forbidden in this run); the per-call cost of the Debug JIT and of the
shield on players' hardware.

Status: COMPLETE (2026-11-04).
