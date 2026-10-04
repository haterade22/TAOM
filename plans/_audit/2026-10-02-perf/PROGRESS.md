# PROGRESS: 2026-10-02-perf

Baseline `dffdf879` on `bannerlord-1.5.x`, 2026-10-02. Program branch `perf/engine-performance`
(worktree `E:\repos\taom-perf\program`). Root `E:\repos\taom-perf` (worktrees; scratch
`E:\repos\taom-perf\scratch`, temp `E:\repos\taom-perf\scratch\tmp`). Models: every role
`claude-opus-5-5` (the session default); `deep-reviewer` spawns never pass `model`. Pool 4.
Codex: not asked. Second Codex pass on fix diffs: replaced by convergence.

What this run is: an engine-performance and memory programme for TAOM at the depth of yotthani's
Ghidra work (MithrilForge `docs/engine/*`), with large-battle frame rate first, taken end to end
through fixes. Every commit lands on `perf/*` branches; nothing touches `bannerlord-1.5.x`.

## Authorizations

| Date | The maintainer's words | Scope | Until |
|---|---|---|---|
| 2026-10-02 | "End to end through fixes" (scope question) | analysis, instruments, measurement protocol, and the ranked fixes | this programme |
| 2026-10-02 | "Large-battle FPS" first (priority question) | order of work: battle frame time, then load times, campaign map, memory | this programme |
| 2026-10-02 | "We can copy it locally and look through it if needed" | read-only clones of yotthani/MithrilForge and a sparse yotthani/bannerlord in the session scratchpad | this run |
| 2026-10-02 | "You have permission to work throughout the night. You are the senior engineer here on the team so do what is best for the long term stablity. Memory and performance are of the upmost important!" | unattended work through the night: worktrees, builds, tests, reviews, local commits on `perf/*` branches | the morning of 2026-10-03 |
| 2026-10-02 | "Its own branch for this is critical!" | all work on `perf/engine-performance` and `perf/NNN-*` branches; the main checkout and `bannerlord-1.5.x` are never written | this programme |
| 2026-10-02 | "Dont worry about limits on files, this is critical work and we will clean up after. The analysis is the most critical part." | evidence files committed in the run folder; analysis depth first | this programme |
| 2026-10-02 | "Ensure when writing code we implement comprehensive logging to get all of the information we want and need! Taom debug is a critical log" | DECISIONS D6: comprehensive taom_debug logging in every plan; cost cuts keep all information | this programme |

Not authorized this run (each waits for the maintainer's word): merging into a trunk, pushing,
filing or commenting on GitHub issues (drafts go to FOR-MIKE.md), any paid call (Codex, the
translator), edits to the live LOTRLOME_Armory or TAOM_Map installs, protected files.

## Status

| Phase or batch | State | Workflow or agent ids | Commits, tips | Notes |
|---|---|---|---|---|
| Understand (read-only audit) | DONE | wf_0b69e595-be2 | none | five explorers and a critic; reports in `E:\repos\taom-perf\scratch\understand\` |
| Baseline | DONE | background shell | `dffdf879` | dotnet 12,348: 12,345 passed, 1 failed (`EveryLanguage_DeclaresARowForEveryEnglishKey`, untranslated keys, paid run owed), 2 skipped |
| Plans 028-035 | DONE | wf_33730896-173 (plans.js, pool 4; 24 agents) | plans commit (next) | all REVISED after one cold review (`plan-review-NNN.md`); amendments appended to 028 (mission summary line), 029 (generic tags, tool output), 030 (career aggregate, D6), 035 (report only, D8) |
| Plans commit | DONE | | `0d1e91f0` on `perf/engine-performance` | 028-035 with amendments, index rows 028-041 |
| Plans 036, 041 | DONE | wf_868ceb65-3c8 (plans.js, pool 1) | `06681779` | both REVISED after one cold review (036: two blocking items, 041: one), every non-blocking item applied; 041 waits for 028's review and re-anchors on its tip |
| Campaign memory re-read | DONE | orchestrator; read-only researcher (inventory path) | `5b7f5b1b`, `0b2944ad`, `d50bf962` | REPORT.md M3; the inventory spike is cheat mode's all-items list; the 2026-09-12 "two collections" reading corrected in the feature doc, the investigation and the RCA, lesson appended |
| UI sprite sheets, mesh edit data | DONE | orchestrator (tpac headers, Ghidra) | `0862bfc3`, `4a909f20` | REPORT.md M6, M7 (FOR-MIKE item 13); M4 closed: edit data stays on disk in play |
| Battle frame-time baseline | DONE | orchestrator (`perf_runs.py` over 30 logs) | `9efd3230` | baseline.md |
| Execute wave B1 (033) | DONE | wf_7619e9f2-02c (execute.js, pool 1) | `d45774a5` (4 commits) on `perf/033-creature-battle-allocations` from `d50bf962` | executor: 12,385 passed of 12,388, RefAsm the base's three failures; orchestrator spec check clean; review owed (note for it: the D6 logging grows BehaviorTreeMissionLogic.cs, already over ADR-002's 150 lines) |
| Plans run 3 (042, 040, 037, 039, 038) | DONE | wf_505d5bc3-2a3 (plans.js, pool 1; 14 agents) | `efb70763` (042, 040), `81a62844` (037, 038, 039) | one cold review each: 037 clean, the other four revised (every blocking item fixed); 039 re-anchors on 028's reviewed tip before it runs |
| Execute wave B6 (042) | DONE | wf_45feb8ab-aef (execute.js, pool 1) | `19be93be` on `perf/042-xml-merge-load-time` from `efb70763` | Stage 1 only (Stage 2's schema cache predicted 251 to 280 ms, under its 300 ms bar). Live harness on this install: every type identical to the engine for Campaign and CustomGame; heavy four 17.5 to 2.9 s and 12.9 to 1.2 s in the test host, where the engine path runs about twice as slow as offline (fair ratio 27 to 35 percent). Executor and orchestrator re-run: 12,413 of 12,416 (EveryLanguage); reference-assembly step the 3 known, binding gate 374; spec check clean |
| Review wave C3 (042) | DONE | wf_893f1d2e-6a2 (review.js, pool 1, maxRounds 2) | lead `4a67f13f`, convergence `98a69d4f`, records `af50ab17` | READY FOR COMMIT. A session restart at about 07:50 stopped the workflow before its round-2 pass, and its resume missed the cache (it restarted lens 1), so the orchestrator stopped it and ran round 2 as one deep-reviewer: every IL pin matched installed v1.5.3, 3 LOW (a "not pinned" list missing branch structure, a red-first claim, a seam test whose fake could not tell methods apart), closed by the orchestrator, the seam test proven red against the mutant. Full suite at `af50ab17`: 12,440 passed of 12,443 (EveryLanguage only) |
| Execute wave A (028, 029, 030, 031) | DONE | wf_50638d3e-ecd (execute.js, pool 3) | 028 `b3bac1a9`; 029 `63cc79a7`; 030 `494eec47` (6 commits); 031 `bf1898c3` (3 commits); all from `0d1e91f0` | logging note D6 on every item. Orchestrator spec check: clean trees, subjects valid, no attribution, SubModule hunks as planned (028 two insertions, 030 the four colour-store removals). Suites re-run by the orchestrator: 028 12,423 passed of 12,426; 029 dotnet unchanged, Python 3,020 run with the base's 3 failures; 030 12,374 of 12,377; 031 12,414 of 12,417; the one dotnet failure is EveryLanguage in each |
| Review wave A | DONE | wf_50c52b3b-17b (review.js, pool 3, maxRounds 2; 35 agents) | 028 `7ca09cc2` (lead `e64529b3`); 029 `fc141af2` (lead `06584dd9`); 030 `8fee9625` (lead `b812bb82`); 031 `3dc3739c` (lead) | lenses 1-6 for 028, 030, 031; 1, 4 and tooling for 029 (its C# edits are comments). No HIGH or CRITICAL in any lens. Convergence: 031 clean in one round; 028, 029 and 030 two rounds each, and the second round's one LOW each is a residual, since the workflow runs no fix pass after its last round |
| Review wave A: 030, 031 | DONE | wf_50c52b3b-17b | 030 `8fee9625` (lead `b812bb82`, convergence fix); 031 `3dc3739c` (lead; convergence clean) | no HIGH or CRITICAL in any lens; orchestrator re-ran the suites: 030 12,383 passed of 12,386, 031 12,423 of 12,426, the one failure EveryLanguage in each; spec check: only trailer lines over 72 characters, no new single-owner edits |
| Review wave A: closing work | DONE | orchestrator | 028 `9d52bb86`, `765d3759`; 029 `50028d26`, `43f74556`, `f92a0bb4`; 030 `37dab128`; 031 `6d17e69d` | Suites re-run: 028 12,453 passed of 12,456 at `7ca09cc2` (the later commits are docs); 029 Python 3,037 run with the base's 3 failures, the parser's 75 tests OK; 030 reference-assembly step 10,053 passed with the 3 known failures. Residuals: 028's understated first-game cost of the `Mission.OnTick` exclusion traced in the v1.5.3 engine and written into the docs, the decision to FOR-MIKE item 16; 029's three parser observations fixed test first (`50028d26`, review owed) and its unparsed trailer left for the merge; 030's and every branch's convergence rounds written into the RCA and REVIEW-LOG entry, which the workflow had left in the report only |
| Execute wave B2 (036) | DONE | wf_a8434dea-e8b (execute.js, pool 1) | `2f560c6c` on `perf/036-anim-memory-probe` from `141a20f1` | executor: 12,416 passed of 12,419, reference-assembly step the base's 3 failures, both installed-binary signature tests passed; under review (wave B) |
| Execute wave B3 (032) | DONE | wf_210d4d96-eb4 (execute.js, pool 1) | `4823b241`, `a00363cc` on `perf/032-worker-thread-formation-patch` from `80176e2a` | orchestrator re-ran the suite: 12,394 passed of 12,397 (EveryLanguage); spec check clean (its one flag is "every AI unit", the game's AI); one addition beyond the plan for the review to judge: a once-per-session WARNING from Patch30's catch (D6 rule 3); review owed |
| Review wave B (033, 036) | DONE | wf_8d9f0d7f-980 (review.js, pool 2, maxRounds 2; 20 agents) | 033 `066e129f` (lead `7021101b`; round 2 clean), records `06307997`; 036 `89f1efea` (lead `78f654d9`; round 2: 2 LOW in the records), records `50df8cbf` | both READY FOR COMMIT, no HIGH or MEDIUM left open. 033: the hunt commit `d45774a5` reverted by the lead (its saving does not exist on v1.5.3). 036: the engine notes' eviction, probe and residency text corrected from the orchestrator's own decompiles of `0x21DEA0`, `0x6EAAE0` and `0x5911A0`. Orchestrator re-ran both suites at the reviewed tips: 033 12,385 passed of 12,388, 036 12,424 of 12,427 (EveryLanguage only, as at each base). Decisions in FOR-MIKE 16i and 16j |
| Execute wave B4 (041) | DONE | wf_b994f116-7a7 (execute.js, pool 1) | `56b2b327` on `perf/041-profiler-extensions-and-hitch-probe` from `1639147d` (the plan file) on 028's `9d52bb86` | re-anchored by the orchestrator (`1a702d25`): 028's review moved the installer's once-guard into SubModule, renamed `BehaviorTickTable` and `WindowTop`, and capped `[Hitch]` at 100 per mission; the plan would have stopped at Step 1 without it. Orchestrator re-ran the suite: 12,536 passed of 12,540 (EveryLanguage; one skipped benchmark); binding gate 411; reference-assembly step the 3 known failures; benchmark 0.43 us per frame against 50, so the probe stays on by default; spec check: one body line over 72; review owed |
| Execute wave B5 (034) | DONE | wf_dae40b0e-d38 (execute.js, pool 1) | `31f59582` on `perf/034-patchshield-per-call-cost` from `64dbcac8` | Branch B: the benchmark put the shield's `__originalMethod` binding at 63 ns and 241 bytes per call (1,145 ns contended), so the finalizers no longer bind it and the original is resolved only on a throw. Orchestrator re-ran the suite: 12,357 passed of 12,360 (EveryLanguage); spec check clean; review owed (wave C2) |
| Review wave C1 (041) | DONE | wf_afdae428-cb3 (review.js, pool 1, maxRounds 2; 10 agents) | lead `0d030997`, convergence `2ab27cdd`, records `d7208235` | READY FOR COMMIT: 4 MEDIUM and 15 LOW fixed by the lead; round 1 found 3 LOW (fixed), round 2 found 1 LOW (a comment, fixed by the orchestrator in the records commit). Orchestrator re-ran the suite with that fix: 12,554 passed of 12,558 (EveryLanguage only; Skipped 3, as at its base). The review's native safety question (the per-frame `IsAnyAnimationLoadingFromDisk` walk) researched by the orchestrator: `evidence/native/clip-list-walk-safety.md`. Decisions in FOR-MIKE 16a and 16l; `0d030997` and `2ab27cdd` carry trailers that do not parse (16g) |
| Execute wave B8 (039) | DONE | wf_cc93ef10-7b9 (execute.js, pool 1) | `a3c219d4` on `perf/039-campaign-map-frame-profiler` from `c4ee9373` (028's tip plus its plan) | Patch101, off by default, PatchShield option A (five exclusions). Orchestrator re-ran the suite: 12,538 passed of 12,541 (EveryLanguage only; base 12,453 of 12,456); RefAsm 3 known; binding gate 416. Spec check: the `Not-tested:` trailer is one 141-character line and `Refs:` sits outside the trailer block (16g). Review staged (`scratch\review-args-C5.json`) |
| Review wave C4 (040) | DONE | wf_220d12a8-0b1 (review.js; resumed after the restart) | lead `f1267a98`, convergence `e6dcb83f`, records `4e4e65e5` | READY FOR COMMIT: 15 confirmed (no HIGH or MEDIUM), round 1 6 LOW fixed, round 2 2 LOW closed by the orchestrator (a test whose setup no longer made the fault its name claims, proven red against the shared-latch regression; three stale comments). Full suite at `4e4e65e5`: 12,487 passed of 12,490 (EveryLanguage only). Decisions in FOR-MIKE 16o |
| Review wave C7 (038) | DONE | wf_fd4ba656-154 (review.js, pool 1, maxRounds 2; 7 agents) | lead `666d5249`, convergence `9546c167` (round 2 clean), records `072d46dc` | READY FOR COMMIT: 1 HIGH (texture size fallbacks with no reason line) and 17 more fixed by the lead; round 1 3 LOW fixed. Found a content bug: `sk_uruk_hai_skirt_a1` in the Cape slot is never equipped (FOR-MIKE 16s) |
| Review wave C2a (034) | DONE | wf_c8c19dd3-5b6 (review.js, pool 1, maxRounds 2; 10 agents) | lead `4f8f3018`, convergence `bbe7fe9f`, records `04894078` | ended 07:47, READY FOR COMMIT: one real defect fixed (another mod patching PatchShield's finalizer made the lookup name the finalizer); the as-shipped cost re-measured at about 5.4 ns per call (Debug; 1.3 ns was a Release figure), corrected in REPORT B6 and FOR-MIKE 16a. Round 1 found 3 LOW (fixed), round 2 found 1 LOW in the record (fixed by the orchestrator). Orchestrator re-ran the suite at `bbe7fe9f`: 12,361 passed of 12,364 (EveryLanguage only). Decisions in FOR-MIKE 16m; `4f8f3018`'s trailers do not parse (16g) |
| Execute wave B9 (037) | DONE | wf_d9edafa9-472 (execute.js, pool 1; resumed after the restart) | `2fd5645c`, `4b6bbf80`, `ae7eff12`, `f9e277ce` on `perf/037-campaign-hot-paths` from `ba3f2a57` | four stages (settings read once; cheap filters first; marketplace id sets per culture; caravan distance reuse). Orchestrator re-ran the suite: 12,423 passed of 12,426 (EveryLanguage only; base 12,345). Stage D reopens a reviewed design choice (FOR-MIKE 16n). All four commits' trailers do not parse (16g). Review owed |
| Execute wave B7 (040) | DONE | wf_cc11e52e-692 (execute.js, pool 1) | `4daf7d80`, `fb796e0d`, `a07546a7` on `perf/040-load-time-stamps` from `67fa6c1a` | three stages (patch groups and load hooks; every module XML load per type, Patch100; each campaign handler of a load). Seven PatchShield exclusions, each for unchanged exception behaviour, none for cost. Orchestrator re-ran the suite: 12,465 passed of 12,468 (EveryLanguage only; the base 12,345 of 12,348); spec check clean; RefAsm 3 known; binding gate 386. Review owed |
| Review wave C2b (032, 029 follow-ups) | DONE | wf_0dfb28db-dcd (review.js; resumed after the restart; 17 agents) | 032: lead `1fb072c5`, convergence `c46ef0c1` (round 2 clean), records closed by the orchestrator; 029: lead `df40fee6`, convergence `973f35d9`, records `76878ffd` | both READY FOR COMMIT. 032: 3 MEDIUM fixed by the lead (a fallback log that dropped every throw after the first; claims that the worker never evaluates queries; a test passing for the wrong reason); suite at `c46ef0c1` 12,383 of 12,386 (EveryLanguage only). 029: two record gaps closed by the orchestrator; `Ran 82 tests ... OK`. Decisions in FOR-MIKE 16p and 16q |
| Execute wave B11 (035) | DONE | wf_2306fdbf-886 (execute.js, pool 1) | `a2e46e29` on `perf/035-release-shader-cache-check` from `d50bf962` | report only (D8): each scene's sack and format, a stale-format WARNING, each DLL's JIT state; never refuses. Orchestrator re-ran its tests: packager `OK` (85); commit clean. Dry runs: patreon 45 of 45 scenes with a sack, Main_map included. Review running (C8) |
| Review wave C8 (035) | DONE | wf_1ab0c717-404 (review.js, pool 1, maxRounds 2) | lead `6cc7fbfe`, convergence `893f9428`, records `e403dc7c` | READY FOR COMMIT: 3 MEDIUM and 6 LOW fixed by the lead; round 1 1 MEDIUM, 1 LOW fixed; round 2 1 MEDIUM (no fixture took the PE32 channel copy's header; a HasConstant slip read the patreon and public Debug TAOM.dll as ON) and 2 LOW, fixed by the orchestrator with `test_the_channel_taom_dll_shape`, the mutant re-run killing X1. Packager `Ran 109 tests`, `OK`; Python `Ran 3014 tests`, `FAILED (failures=3, skipped=8)`, the three known base failures; no C# changed. Open choices: FOR-MIKE 16t |
| Execute wave B10 (038) | DONE | wf_23f6b117-1ec (execute.js, pool 1) | `ede09280`, `65f640fa` on `perf/038-battle-equipment-memory-audit` from `ba3f2a57` | `tools/audit_battle_equipment_memory.py` (70 tests) and a `--loose-assets` option on the map audit (default outputs byte-identical). Testing channel run: 1,657 troops, 2,671 items, largest asset `sk_mumakil_platform_a1` (65 MiB, over the LOD0 face budget), largest troop `rohan_edoras_golden_hall_supreme_rider`. Orchestrator re-ran its tests: `Ran 70 tests ... OK`; commits clean. Review owed |
| Review wave C6 (037) | DONE | wf_5fbbe7ee-470 (review.js, pool 1, maxRounds 2) | `b282aaf2` (Stage D dropped), lead `f58ddb09`, convergence `212d397d` (round 2 clean), records `c8c96c09` | READY FOR COMMIT. Every lens rejected Stage D under the simplicity criterion. Full suite at `212d397d`: 12,400 of 12,403 (EveryLanguage only). Decision in FOR-MIKE 16n |
| Review wave C5 (039) | DONE | wf_abcce7e9-750 (review.js, pool 1, maxRounds 2) | lead `69ed4a36`, convergence `5714149b`, records `db777487` | READY FOR COMMIT: 3 MEDIUM fixed by the lead (option A's text, an escape from MapScreen.OnFrameTick, a fault path); round 1 2 LOW fixed; round 2 1 LOW (a commit body fact), corrected in the records commit's body. Full suite at `5714149b`: 12,549 of 12,552 (EveryLanguage only). Decisions in FOR-MIKE 16a and 16r |

## Log

- 2026-10-02 evening: scope, priority and overnight authorization recorded above.
- 2026-10-02: read-only audit workflow wf_0b69e595-be2 done (E1 yotthani knowledge, E2 mission hot
  paths, E3 campaign and load, E4 measurement baseline and native tooling, E5 asset levers, critic).
- 2026-10-02: engine facts verified this session with TAOM's own Ghidra project and the v1.5.3
  decompile (see REPORT.md "Verified engine facts").
- 2026-10-02: correction recorded: the game's managed runtime is the .NET Framework desktop CLR, not
  Mono (REPORT.md).
- 2026-10-02: program worktree and branch created from `dffdf879`; baseline suite run.

- 2026-10-02 20:34: docs commit `761b20fe` on `perf/engine-performance` (engine reference page, Mono
  correction, provenance, adoption record, run folder with evidence). Times in this log are from
  `git log`; an earlier draft of this entry carried wrong clock times.
- 2026-10-02, before 20:48: native analysis by the orchestrator (Ghidra): on-demand clip acquire, release,
  eviction and the 12 MiB budget; GetNearbyAgentsAux filter and 15 m grid; agent cap; timer resolution;
  IsAnyAnimationLoadingFromDisk. Release-pack memory audits (testing, patreon): Main_map textures 1.03 to
  1.04 GB (the 2026-09-13 downsizing shipped), the 16K vista 341 MB, meshes 1.73 GB. Static Harmony
  census. Briefs 036 (clip memory probe), 037 (campaign hot paths), 038 (battle equipment memory audit)
  written. Issue drafts in `issue-drafts.md` (public-text check exit 0).
- 2026-10-02 20:48: second docs commit `e9cd8b39` (Kit texture formats verified, hitch analysis, memory
  audits, D6, issue drafts, evidence).
- 2026-10-02 21:42: plans commit `0d1e91f0` (plans 028 to 035 reviewed and revised, the amendments to
  028, 029, 030 and 035, D7 and D8, the engine thread layout, briefs 036 to 041, index rows 028 to 041).
- 2026-10-02 ~21:44: wave A execution (wf_50638d3e-ecd) and plans run 2 (wf_868ceb65-3c8) launched; a
  liveness watch polls both every 5 minutes.
- 2026-10-02 ~21:50 onward: campaign memory analysis from the existing logs (REPORT.md M3,
  `evidence/memory/campaign-memory-reread.md`); a read-only researcher traces the inventory-open
  allocation. Commit `5b7f5b1b`.
- 2026-10-02 ~22:05: UI sprite sheets measured from the tpac headers (REPORT.md M6, M7;
  `evidence/memory/ui-sprite-sheets.md`). Trunk CI found red on its last 30 runs (FOR-MIKE item 14).
- 2026-10-02 ~22:20: wave A done and verified (status table); review wf_50c52b3b-17b launched.
- 2026-10-02 ~22:30 to 23:20: load time measured from the engine's rgl logs (`evidence/load/load-time-rgl.md`):
  the module-XML merge is about 28 of a new campaign's 50 loading seconds. Then measured offline with the
  engine's own `CreateMergedXmlFile` (Windows PowerShell 5.1 loading the installed DLLs,
  `evidence/load/merge-bench.md`): about 8.8 s for the four biggest types against 2.4 to 3.1 s for a prototype
  that keeps one XDocument, with identical output. Brief 042 written and amended; plans 036 and 041 committed
  (`06681779`); plan 033 executing (wf_7619e9f2-02c).
- 2026-10-03 01:44: plan 036 executed (`2f560c6c`). About 01:45 review wave A ended: 031 clean, 028, 029
  and 030 two convergence rounds each with one LOW left.
- 2026-10-03 ~01:50: plan 032's execution (wf_210d4d96-eb4) and review wave B for 033 and 036
  (wf_8d9f0d7f-980) launched; 032 committed at 01:59.
- 2026-10-03 01:58 to 02:17: wave A's closing work (status table). The 028 residual was read against the
  v1.5.3 engine: with `Mission.OnTick` unshielded, a foreign mod's broken patch there unwinds the whole
  application tick in a process's first game, and a postfix that throws after the mission has ended
  stops `MissionState.OnTick` from popping it, so the battle never closes (FOR-MIKE item 16). Plan 041
  re-anchored on 028's tip and launched (wf_b994f116-7a7).
- 2026-10-03 02:20 to 02:45: shader cache read from the engine (`evidence/load/shader-cache-native.md`): the
  runtime cache drops on a module-id or game-build change, never on a TAOM update alone; the startup
  compiles are 40 material combinations of the Armory's; a Kit-written Armory sack did not serve them.
  The 1,090 ms battle hitch window holds no engine log line at all. Commits `b7c1b49a` to `47ac3b8d`.
- 2026-10-03 ~02:50: plan 041 executed (`56b2b327`, verified); plan 034 launched (wf_dae40b0e-d38).
- 2026-10-03 ~03:00 to 03:10: plan 034 executed (`31f59582`, verified) and review C1 for 041 launched
  (wf_afdae428-cb3). From the release packs: TAOM's 33 banner icon atlases are 4096 px uncompressed RGBA,
  64 MiB each against vanilla's 4 MiB (REPORT M9, FOR-MIKE 13c); no TAOM texture carries vanilla's
  low-resolution stub (M2); 116 map textures look unreferenced (349 MiB of download, FOR-MIKE 6). The
  FOR-MIKE 15 recommendation was corrected (strip only the owner that threw). A read-only merge probe
  maps every conflict between the branches (`evidence/merge-map.md`).
- 2026-10-03 ~03:40 to 04:20: plans run 3 done and committed; plan 042 executed (`19be93be`), its review
  launched (wf_893f1d2e-6a2).
- 2026-10-03 04:59 to 05:18: review B ended (033 and 036 READY FOR COMMIT); review C2a (034) and the 040
  execution launched in its two slots. Both reviewed tips re-verified; the convergence records closed on
  each branch (`06307997`, `50df8cbf`); 036's two round-2 findings fixed in its records commit, the engine
  notes from the orchestrator's own decompiles. In this folder: the measurement session's step 1 (the
  probes ship on; only the tick profiler needs turning on and a restart), the 033 and 036 issue drafts
  (no howdah or hunt promise; the probe on by default), and FOR-MIKE 16i and 16j. The 033 review's
  "the grid overlay draws nothing" was checked before relaying: the engine's sphere call is
  `[Conditional("_RGL_KEEP_ASSERTS")]`, which no TAOM project defines.
- 2026-10-03 05:18 to 05:28, while the four jobs ran: module sounds read in the binary
  (`evidence/memory/module-sounds.md`). FMOD is linked into the engine DLL; the module-sound parser only
  registers entries, and the one file-path `createSound` is the Studio programmer-sound callback, with no
  stream flag, so each play decodes the whole file and the destroy callback frees it. REPORT M8 corrected
  (module sounds are not in the main-menu floor); FOR-MIKE 13d: 21 unregistered files (53.3 MiB), three
  unplayed music tracks (43 MiB), one entry naming a missing file (`elf_horn.wav`). My first size
  estimate misread three ADPCM and OGG files as MP3 (a false frame sync); the script now reads the kind
  from each file's header and the numbers are from that run.
- 2026-10-03 05:28 to 05:40: plan 040 executed (`a07546a7`, verified); review C2b launched in its slot.
  (The times of this entry and the two above were first written as estimates, up to 90 minutes late;
  corrected from the commit times and the clock.)
  Banner atlases computed from the data (`evidence/memory/banner-atlases.md`): the campaign's 265 banner
  keys name 19 of the 33 atlases (1,216 MiB today; the other 14 only serve runtime-built banners), and
  five clans' banners name icon ids no module defines, so those layers draw nothing (FOR-MIKE 13e). The
  first count missed the XSLT overrides' keys and keys with a negative rotation; the script now checks
  every carrier parses.
- 2026-10-03 06:05 to 06:30: FOR-MIKE read through and brought current; the merge probe re-ran with 040
  (`evidence/merge-map.md`). Review C1 (041) ended at 06:19, READY FOR COMMIT; its records closed in
  `d7208235` (06:23) with the round-2 comment fix, and the suite re-ran green. The 039 execution took its
  slot. The review's native safety question researched: vanilla polls the same clip-list walk every
  frame of a mission's loading screen, and the engine indexes that list without a lock from about 90
  places (`evidence/native/clip-list-walk-safety.md`, FOR-MIKE 16l).
- 2026-10-03 06:40 to 07:55: 039 built and verified (`a3c219d4`); review C4 (040) launched. Mike asked for
  a status at about 06:58 and was given one. Review C2a (034) ended at 07:47, READY FOR COMMIT; the 037
  execution took its slot; 034's records closed in `04894078` with the round-2 record fix and the suite
  re-ran green. Its review re-measured the shield's as-shipped cost at about 5.4 ns per call (the 1.3 ns
  quoted in REPORT B6 and FOR-MIKE 16a was a Release figure; both corrected).

- 2026-10-03 07:50 to 08:20: the Claude Code session ended by accident (Mike) and every running workflow stopped. Resumed C2b, C4 and the 037 execution from their run ids (cache hits); C3's resume restarted lens 1, so it was stopped and its last step run directly (see its row). 037 built and verified; 038's execution and 039's review took the free slots.
- 2026-10-03 12:45 to 12:56: review C8 (035) ended READY FOR COMMIT with round-2 residuals; fixed with a
  fixture built from the patreon and public `TAOM.dll` header (the reviewer's HasConstant mutant now
  fails it), records closed in `e403dc7c`. The convergence reviewer's stray interactive-Python output
  (64 MB on C:, 609 MB on E:, error-loop text only) was deleted. Every plan is now reviewed.

## Next

1. Nothing is running. All 15 plans are built, reviewed and closed; the programme waits on Mike:
   FOR-MIKE "Start here", then item 16's decisions, issue filing (item 9), and the merge order in
   `evidence/merge-map.md`. A history rewrite before merge fixes the trailers and bodies in 16g.
2. After merge: the first measurement session (FOR-MIKE Detail), then delete the memory resume card.

## Blockers

None. Paid calls, issues, merges and pushes wait on the maintainer (Authorizations).
