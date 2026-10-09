# Adopting yotthani's newer code: VanillaTuning, PerfProbe, MithrilForge and the HoN features (2026-10-08)

Written for: TAOM maintainers deciding what to take from yotthani's two repositories, and for yotthani (the
"For yotthani" section is meant to be forwarded). Procedure: `/adopt-external`
([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)), sixth pass on these sources. Mike's
request: review all of it and bring in whatever is more effective, elegant, efficient or useful than TAOM's own.

## Sources, and what was read

| Source | Commit | Read |
|---|---|---|
| `yotthani/bannerlord` (private, shared with Mike by its author) | `1ad701bd` (2026-10-08 19:49) | `HoN/VanillaTuning`, `VanillaTuning.Tests`, `VanillaTuning_TAOM` in full; `HoN/PerfProbe` and `HoN/SaveBench` feature by feature; every yotthani commit on FieldCamp, Refuge and SupplyLines since 2026-08-22; the 2026-09-30 restored source of MixedFormations, SmartCavalryAI, SiegeDismount, CompanionTactics, EquipPresets, FiefManagement and TransferbuttonMenu; file lists, READMEs and commit logs of every other folder; `docs/balance-sim/taom-formulas.md` sections 0 and "UNKNOWN" |
| `yotthani/MithrilForge` (private, MIT) | `4fab7e19` (2026-10-08 19:49) | the product code's project references and the new library, writer, physics, clip and cloth code since `91149e11`; the docs changed since `2e6fe979` |
| Not read in full | | MithrilForge `docs/engine/perf.md` (about 2,100 lines) and `docs/engine/modding-kit.md` (1,790 lines): Tier 2 item 8 |

Three read-only agents did the file-by-file reading through `gh api`; every claim this review relies on was
checked against the files or the binary named below.

## Security

- **Safe to learn from:** yes. Nothing was cloned, built or run.
- **Licences:** VanillaTuning is MIT since `1b9ce3ba` (2026-10-07, its own `LICENSE`, (c) 2026 yotthani), so its
  code can be ported with the notice. ShaderCacheKeeper is MIT (vendored 2026-10-06). MithrilForge is MIT. On
  2026-10-08 every other folder of `yotthani/bannerlord` had no licence file, so only behaviour was ported from
  them. On 2026-10-09 Mike stated that yotthani made everything MIT; the repository has no root licence file yet,
  so yotthani is asked to add one ("For yotthani", item 5).
- **Runnable surface seen, not taken:** PerfProbe's alloc watch detours `ntdll!NtAllocateVirtualMemory` through
  MinHook with a hand-written stub, and its texture watch overwrites a D3D11 method-table slot; MithrilForge's
  perf scripts start and stop the game and write a `Bannerlord.exe.config` beside it; its PhysX cooker loads the
  game's PhysX DLLs. No network access, credential read or install hook in any of it.

## What TAOM verified (2026-10-08)

| Claim | Evidence |
|---|---|
| The guard's site and resume bytes exist on TAOM's engine | Mike's installed v1.5.4 `TaleWorlds.Native.dll` (14,209,888 bytes, SHA-256 prefix `1e6f5ef562cd2010`) holds the 13 bytes at RVA `0x69D14` and the 10 at `0x69D21` exactly; the watch load at `0x69CE1` decodes to the global `0xD9D160` |
| The skeleton buffer has no bounds check | `python tools/native_decompile.py --rva 0x69D1C`: `FUN_180069aa0` (entry `0x69AA0`) reserves with one locked add and derives the block as `fill >> 11` with no compare against 32 blocks; a waiting thread spins in `movzx eax,[rbx]; test al,al; jnz` at `0x69DA4` to `0x69DAA` |
| One reservation site | of 210 RIP-relative references to `0xD9D160` in `.text`, one has `+0x9D0` within 40 bytes (`0x69CE1`, same function) |
| One throw skips the mission's start | v1.5.4 `Mission.AfterStart` sets `CurrentState = State.Continuing` only after the submodule callbacks and every behaviour's `OnBehaviorInitialize`, `EarlyStart` and `AfterStart` loop (cache `Mission.cs:3815-3852`); every behaviour enters through `Mission.AddMissionBehavior` (`:4686`) |
| TAOM's formation presets are half-built | `OOBButtonsVM.cs:149-153` (Load is a "Phase-1 stub") and `:185-193` (Save stores a name only) |
| Custom Battle ran none of TAOM's Combat Mechanics damage rules (fixed by #788) | `Main/SubModule.cs:1350-1362` registered only the morale, stat and banner-bearer models off a `BasicGameStarter`, plus one creature damage model (yotthani's balance notes say the same) |
| The `external developer drop` is yotthani's HoN code | identical SaveSystem ids (726900501 with classes 101 and 102 for equipment presets, 726900601 with class 101 for formation presets), identical class and field names, the same layout maths and minority thresholds, and his 2026-09-30 restore commits from his 2026-04-29 release, a week before TAOM's 2026-05-07 port. The drop folder itself no longer exists, so this is a fit, not a byte comparison |

## Decisions

Mike approved the plan on 2026-10-08. Tier 1 is approved; Tier 2 waits for his word item by item.

| # | Item | Source | Decision |
|---|---|---|---|
| 1 | Mission-start guard: a behaviour that throws while a battle starts no longer reloads the mission forever | VanillaTuning `MissionStartGuardPatch.cs` (MIT), adapted | Tier 1 |
| 2 | Skeleton-buffer guard and watch | VanillaTuning `FrameBufferGuardPatch.cs`, `FrameBufferWatch.cs` (MIT), adapted | Tier 1; **Mike: both ON by default**; after the review found the second pool, **Mike: guard it too** (TAOM's own code block, mirroring the first); the engine's nine other pools of this design are documented, not guarded (decided in this session under the maintainer's "full control" instruction, 2026-10-08; draft 9) |
| 3 | Settlement nameplate cull on the campaign map | VanillaTuning `NameplateCullPatch.cs`, `Core/NameplateCull.cs` (MIT), adapted | Tier 1; **Mike: ON now, his MapPerf A/B before the next release** (the default can still flip before release: no player has it saved) |
| 4 | Map-view release (GPU memory left by closed menus) | VanillaTuning `MapViewReleasePatch.cs` (MIT), adapted | Tier 1, ON, interval 20; an engine event instead of a Harmony patch (review) |
| 5 | Formation presets that really save and load | yotthani's HoN `FormationPresetManager.cs`, behaviour only | Tier 1, built 2026-10-09 (#779), off by default until the in-game check; troop shares, filters and single-class swaps in #787 |
| 6 | This review, engine knowledge, provenance | | Tier 1, committed with items 1 to 4 (its links point at their files) |
| 7 | Focus-ray throttle (0.2 to 0.3 ms per battle frame) | VanillaTuning `FocusRayPatch.cs` (MIT) | Tier 2, after a TAOM measurement |
| 8 | Read MithrilForge's `perf.md` and `modding-kit.md` against TAOM's engine docs | MithrilForge (MIT) | Tier 2 |
| 9 | Equipment-preset item locking | yotthani's `PresetManager.cs` (MIT since 2026-10-09) | Tier 2 |
| 10 | Time and GC per save in SaveLoadDiagnostics | PerfProbe `SaveDiagnostics.cs` (MIT since 2026-10-09) | Tier 2 |
| 11 | Auto-assign scoring extras; party-screen role icon | CompanionTactics (MIT since 2026-10-09) | Tier 2 |

Where TAOM's version beats the upstream one (decided in the plan, built with each item):

- **Item 1** wraps the six start calls inside `Mission.AfterStart` (the two submodule callbacks, the three
  behaviour start methods, `MissionObject.AfterMissionStart`) with one transpiler, through TAOM's soft-failing
  call-site swap (`TickProfilerTranspiler.Rewrite`). Every behaviour, submodule and mission object of every
  module is covered whatever the load order, with one patch instead of one per behaviour type; upstream
  protects only the behaviours present when its own submodule runs, and not the submodule callbacks.
- **Item 2** finds its site with a single-match wildcard scan plus the exact-byte check (the
  `MissionPerf/AnimMemory/ClipBudgetSignature.cs` approach) instead of fixed RVAs, makes the code page
  execute-read after writing with its counter on a separate read-write page, and reports "already guarded" when
  another module (VanillaTuning) holds the site. It also guards and watches a second per-frame pool with the
  same missing bound, which the review found, with a code block of TAOM's own (see "The deep review" below).
- **Item 3** adds two skip conditions upstream lacks (a plate whose widget was never parked off screen, a plate
  the tutorial targets) and reads its settings once per frame through a cached provider.
- **Item 4** needs no Harmony patch: it listens to the engine's own `ScreenLayer.OnLayerActiveStateChanged`,
  raised right after `OnDeactivate`, and releases through vanilla's own `SceneLayer.ClearRuntimeGPUMemory(false)`.
- **Item 5** reads and drives vanilla's public Order of Battle view models only, where the HoN code sets the class
  selector by reflection: a class change goes through the formation's class selector, a hero through vanilla's
  select-then-accept click path, and each step is checked afterwards. Class changes repeat in passes while vanilla's
  `IsAdjustable` rule allows them, a hero goes only into a formation whose saved class is in place, a siege maps
  mounted classes the way vanilla's own layout load does, and only the general can save or load, in a campaign. A
  preset stores formation classes, captains and hero troops; it does not restore troop shares or filters, and it
  cannot swap two formations that are each the only one of a class (#787).
- **One rule for VanillaTuning running beside TAOM:** detect its effect, not its presence. The skeleton guard
  finds VanillaTuning's jump at the site (it always installs first, at `OnSubModuleLoad`); the cull relies on
  Harmony skipping a second replacing prefix; the start guard catches only what VanillaTuning's per-method
  finalizers did not, so it always shows its own notice.

## Not taken

| Item | Reason |
|---|---|
| xml-merge-fast | TAOM's `XmlMerge` keeps one document the same way and adds an XSLT cache, stand-aside on foreign patches, a session fallback and byte-equality tests against the engine; two prefixes on the same method would make TAOM's stand aside |
| butterlib-lazy-distance | TAOM switches ButterLib's Distance Matrix off at the source (#740) without a Harmony patch |
| shader-compile-notice | TAOM's port (2026-10-06) is the same design, localized and tested |
| skip-logo-video | TAOM shows its own splash there on purpose (#704) |
| particle-step-chunk, limb-ray-shadow | v1.5.3 only and unmeasured, or a research tool with no effect |
| save-fast, save-no-forced-gc, war-score-cache | rejected by yotthani's own measurements, or no zero-difference shadow run yet |
| battle-size | a custom-battle test switch; a battle with reserves hangs |
| ash-emission, VanillaTuning_TAOM | a runtime swap of TAOM_Map's ash prefab; the content fix is #738 (yotthani measured 21.0 to 12.2 ms per frame at the Black Gate, ash unchanged) |
| PerfProbe: hang watch, probe slots, map, load and menu probes, alloc, texture and emitter watches, autopilot; SaveBench | TAOM's stall watchdog (10, 20 and 40 s, two probes, exit stand-down) and its MissionPerf, MapPerf and LoadTimeStamps cover the same ground with a small fixed probe set; the rest is one-off engine diagnosis or a test harness. Its frame-buffer gauge is the same code as item 2's watch |
| The four camp `.tpac` packages rewritten by MithrilForge | only version numbers, checksums and bounds changed; TAOM's `AssetPackages` folder is not loaded at all while the module has a loose `Assets/` tree, so the delivery decision owed since 2026-09-29 comes first |
| MithrilForge as a tool | its product code no longer references the unreadable `TpacTool-bannerlord` fork (only its tests do), so TAOM can now read and build it; no task needs it today. When one does (camp props after the delivery decision, a `bo_` body built from a mesh's own triangles for a MISSING_BODY row, resident creature clips), install it as a standalone tool under `E:\Tools`, the Ghidra precedent |
| SmartCavalryAI friendly collision avoidance | a `GetNearbyAgents` query per cavalry agent per tick through reflection, for little gain over TAOM's planner |
| SupplyLines reinforcement dialog | cut by Mike (`docs/features/supply-lines.md:176`) |
| MixedFormations, SiegeDismount, FiefManagement, QuickActions, the camps | TAOM is ahead on every comparison (navmesh checks, slot reclaim, snapshot, siege detection, modal guards, locked-item skip, the camps' RCA fixes) |
| DualWield, the Armory family (Armory, Armory_TAOM, Armory.Sim), RingSystem, TroopStatus, VirtualCaptains, LayeredArmor, ArmorCosmetics, SmithingExtended, BattlePresets, TAOM_UI, TAOM_RacePortraits, DynamicPOIs, FaceLearner, FactionMap, ThemeSwitcher, KoMModpack | separate mods, not optimisations of TAOM code; each would be its own product decision |

## Findings for Mike (no code here)

1. **Custom Battle ran none of TAOM's Combat Mechanics damage rules** (crush-through, charge knockdown,
   unstoppable, stagger, cleave): Custom Battle had one TAOM damage model, which carried only the creature,
   race ability and siege rules. Mike asked for the fix on 2026-10-09: #788, branch
   `fix/custom-battle-damage-models`, RCA `rca-custom-battle-damage-2026-10-09.md`. The troll health bonus and
   the career passives stay campaign only.
2. **`tools/native_sig_author.py xref` scans 2.8 % of `.text`.** Its single capstone linear sweep stops at the
   first byte it cannot decode (RVA `0x48B36` on v1.5.4), so it reported 16 references to `0xD9D160` where a
   byte scan finds 210, and it missed the one at `0x69CE1`. A fix (sweep function by function, or a byte
   scan for RIP-relative operands) wants its own issue.
3. **`Main/Features/FiefManagement/UI/FiefManagementNavItemVM.cs`** had no reference outside its own file: it
   was meant for a "Fiefs (F6)" map-bar entry that was never built. Mike asked for the entry on 2026-10-09:
   #789, branch `feat/fief-nav-button`, which adds the button and deletes the unused view model.
4. **Licence settled (2026-10-09):** Mike stated that yotthani made everything MIT. That clears the seven
   features ported from his drop in May (QuickActions among them, which the register also lists under
   `TransferbuttonMenu`) and the `yotthani/bannerlord` row; their MIT notice is in
   `Main/_Module/THIRD-PARTY-LICENSES.txt`. The repository has no root licence file yet, so the ask to yotthani
   ("For yotthani", item 5) is now for that file.

## Issue drafts (filed 2026-10-08 as #775 to #786, under the maintainer's "full control" instruction)

1. **feat: battle start survives a mission behaviour that throws (mission-start guard).** (#775) A behaviour (any
   module's) that throws in `OnBehaviorInitialize`, `EarlyStart` or `AfterStart`, or a submodule that throws in
   `OnMissionBehaviorInitialize`, leaves the mission initializing, and the engine reloads it every frame for
   ever (#699's shape). After yotthani's VanillaTuning `mission-start-guard` (MIT): one transpiler wraps the six
   start calls of `Mission.AfterStart`, so a throw is logged with its stack, one message shows, and the battle
   starts without that part. Labels: enhancement, perf, triage-needs-ingame.
2. **feat: a full skeleton buffer no longer freezes a big battle (skeleton-buffer guard and watch).** (#776) The
   engine's per-frame skeleton buffer holds 65,536 entries (2,340 skeletons in view) and has no bounds check;
   one more and the worker threads wait for ever (verified on v1.5.4, RVA `0x69D1C` and `0x69DA4`). Port of
   VanillaTuning's `frame-buffer-guard` and `frame-buffer-watch` (MIT): 13 engine bytes jump to a check that
   takes the overflowing reservation back; the second per-frame pool (262,144 entries, `0x6AF58`), which has
   the same missing bound, gets the same guard with a TAOM code block; the watch logs each battle's peak and
   refusals for both pools. Both ON by default (Mike, 2026-10-08). Labels: bug, perf, triage-needs-ingame.
3. **perf: the campaign map skips hidden settlement nameplates (nameplate cull).** (#777) yotthani measured the
   nameplate update at 4 ms of a 14 ms map frame in fast forward on TAOM's map (1,002 settlements in his count), and 5.5 to
   2.3 ms per frame with the cull. Port of VanillaTuning's `nameplate-cull` (MIT). Default on now (Mike,
   2026-10-08); his MapPerf A/B runs before the next release, and if it shows no gain the default flips before
   that release (once a release ships the setting, a persisted MCM default cannot flip without renaming it).
   Labels: enhancement, perf, triage-needs-ingame.
4. **perf: closed menus stop leaking the map's render targets (map-view release).** (#778) Every menu closed over
   the map leaves four render targets (about 40 MB at 1080p) until the next battle (TAOM audit item M3a).
   After VanillaTuning's `map-view-release` (MIT): on every twentieth deactivation of the map's scene layer
   (the engine's `ScreenLayer.OnLayerActiveStateChanged` event, no Harmony patch) the layer releases its GPU
   memory through vanilla's `SceneLayer.ClearRuntimeGPUMemory(false)`. Labels: bug, perf, triage-needs-ingame.
5. **feat: formation presets save and load the order of battle.** (#779) Save stores only a name and Load is a stub
   (`OOBButtonsVM.cs:149-193`). Capture and apply formation classes, captains and hero-troops through the
   public OOB API, after yotthani's HoN `FormationPresetManager` (behaviour only). Labels: enhancement. Built
   2026-10-09; its review's follow-up is draft 13 (#787).
6. **fix(tools): `native_sig_author.py xref` reads only the start of `.text`.** (#780) Its one capstone linear sweep
   stops at the first byte capstone cannot decode (RVA `0x48B36`, 2.8 % of `.text` on v1.5.4), so it reported
   16 references to `0xD9D160` where there are 210, and missed `0x69CE1`. Sweep function by function, or turn on
   capstone's skipdata, and add a test with a reference past the first undecodable byte. Labels: bug.
7. **fix(tools): `harvest_literal_loc_keys.py --apply` adds a blank line per run.** (#781) `lines[at:at] = block`
   (line 163) inserts the block in front of the trailing blank line it means to drop, so every run strands one
   more (four already sit mid-file in `taom_module_strings.xml`). `lines[at:close] = block` does what the comment
   says. Labels: bug.
8. **chore(localization): translate the two new notices.** (#782) `taom_mission_start_guard_notice` and
   `taom_skeleton_buffer_warning` have no rows in the 12 languages, nor have the 21 `taom_oob_*` keys of item 5
   (added 2026-10-09); one `--sync-ids` pass also covers the 32
   untranslated race-ability keys (#754 is the precedent). A paid run, on the maintainer's word. Labels:
   enhancement, triage-blocked-decision.
9. **perf: measure the engine's other per-frame pools before guarding more.** (#783) The engine's per-frame allocator
   at `[0xD9D160]` has at least eleven pools with the same missing bound (engine doc section 10); TAOM guards the
   two that skeletons fill. Two more sit on the same draw path: `+0xB8` (262,144 entries) and `+0xD6B8`
   (131,072 entries, one per object drawn). Extend the watch to read their fill (read only), run the largest
   battles and sieges, and guard a pool only if its peak comes near its size. Labels: perf,
   triage-blocked-decision.
10. **perf(tools): a first-byte prefilter in `ClipBudgetSignature.Find`.** (#784) The skeleton guard's install scans the
    10.6 MB `.text` six times with a byte-by-byte loop: 152 to 173 ms per launch in TAOM's Debug build (the
    2026-10-08 review's bench). Jumping to the next occurrence of the pattern's first byte with `Array.IndexOf<byte>`
    (mscorlib's optimised `Buffer.IndexOfByte`) gives the same hits in 48 to 58 ms. The loop predates this change,
    so it is a follow-up. Labels: perf.
11. **fix: isolate TAOM's hand-wired mission behaviour registrations.** (#785) With Patch103, a throw in
    `SubModule.OnMissionBehaviorInitialize` is survived, and every TAOM behaviour registered after it is missing from
    that mission (the BattleLoad closer is now registered in a `finally`). Wrapping each registration, as `ModuleRunner` does per
    feature module, would drop only the culprit; it rewrites about 30 lines that other changes also edit, so it is
    its own change. Labels: bug.
12. **decision: how co-op classifies crash-containment toggles.** (#786) `EnableCrashCapture`,
    `EnableNativeToManagedCapture` and `SurviveMissionStartFailures` change what runs after an exception, and none of
    `CoopSettingsRelevance`'s four exclusion reasons fits them; they are filed as instrumentation. Either add a fifth
    reason or count them as simulation-relevant, which moves every peer's settings fingerprint. Labels:
    triage-blocked-decision.
13. **feat: formation presets restore troop shares, filters and single-class swaps.** (#787, filed 2026-10-09 after
    item 5's review) A preset stores no class weights and no filters, so a class that another formation already has
    loads at 0 percent; a swap between formations that are each the only one of a class cannot apply under vanilla's
    `IsAdjustable` rule; a dead hero's assignment stays and counts as skipped. Weights and filters change the preset's
    save format. Labels: enhancement, triage-blocked-decision.

## The deep review

The `/deep-review` of items 1 to 4 (seven lenses, 2026-10-08) found no HIGH or CRITICAL defect. What it found
and what was done about each finding is in [rca-yotthani-adoption-2026-10-08.md](rca-yotthani-adoption-2026-10-08.md).

The `/deep-review` of item 5 (seven lenses in two waves, 2026-10-09) found no HIGH or CRITICAL defect either. It
found one apply defect, two scope limits, a Custom Battle reach gap and a verification gap; the findings, fixes and
the two limits left for the maintainer (#787) are in
[rca-formation-presets-2026-10-09.md](rca-formation-presets-2026-10-09.md).

## For yotthani

Thank you; TAOM takes mission-start-guard, frame-buffer-guard and frame-buffer-watch, nameplate-cull and
map-view-release (with your MIT notice). What we changed while porting, in case it helps yours:

1. **frame-buffer-guard** finds its site by the fixed RVAs `0x69D14` and `0x69D21`. The bytes check makes that
   safe, but a scan for the 13-byte sequence (with the displacement bytes as wildcards) requiring exactly one
   match would survive a rebuild that only moves the function. The code cave is a `PAGE_EXECUTE_READWRITE` page
   for its whole life because the counter lives in it; putting the counter on a second, read-write page lets
   the code page drop to execute-read after writing.
2. **The same function has a second pool with the same missing bound.** Before the reservation you guard,
   `FUN_180069aa0` reserves from `[0xD9D160] + 0xC28` through `FUN_18006af30`: `lock xadd` at RVA `0x6AF58`,
   block index `fill >> 13`, blocks of `0x80000` bytes, 32 of them, ready bytes at `+0x108`, and only the first
   and last block compared. Its waits spin at `0x6AFD7` to `0x6AFDD` and `0x6AFF0` to `0x6AFF6`. A skeleton without a remap table
   (`cmp qword [rdi+0x10], 0` at `0x69B16`) reserves only there, which may be why horses, wargs and the mumak
   measured 0 in your pool. TAOM guards the second pool too, with the same shape: `mov [rsp+20h], r15;
   mov r15d, edx; lock xadd [rcx], r15d; lea eax, [rdx+r15]; cmp eax, 40000h; jbe back; lock sub [rcx], edx;
   lock inc [counter]; xor r15d, r15d; jmp back` (the function then returns start index 0). When TAOM and
   VanillaTuning run together yours installs first (`OnSubModuleLoad`) and owns the first pool's site; TAOM finds
   your jump, reports it, and still guards the second pool. The same global holds at least nine more pools of
   this design, none bounded, two of them on the same draw path (`+0xB8` and `+0xD6B8`); the table is in section
   10 of TAOM's `docs/reference/engine/mission-frame-threads-and-native-costs.md`.
3. **map-view-release** releases on the cover, inside `ScreenBase.HandlePause`, before `MapScreen.OnPause`. Native
   `ClearAll` zeroes the view's ready word (`mov word ptr [rbx+0x8A0], 0` at RVA `0x34B769`; `ReadyToRender` and
   `CheckSceneReadyToRender` return that byte at `0x50AB20`), so `OnPause` sees the view not ready and raises the
   global loading window over the screen being opened. The map cannot lower it while covered, and Options,
   Save/Load and other screens that never call `DisableGlobalLoadingWindow` leave it up until they close. TAOM
   releases on the map layer's return instead (`ScreenBase.HandleActivate` and `HandleResume` activate the layers
   before the screen's own `OnActivate` and `OnResume`), so the map lowers its own loading window. With both mods
   loaded both release the same view; players should turn one off.
4. **mission-start-guard** protects the behaviours present when VanillaTuning's own submodule callbacks run.
   Behaviours added by modules that load after VanillaTuning, in their `OnMissionBehaviorInitialize`, get
   their finalizers only from the next mission on, and a throw in another module's
   `OnMissionBehaviorInitialize` itself still loops. TAOM instead swaps the six start calls inside
   `Mission.AfterStart` for helpers with a `try` and an exception filter: one transpiler, every module in any
   load order, the submodule callbacks and `MissionObject.AfterMissionStart` included.
5. Thank you for making all of it MIT (Mike passed it on, 2026-10-09). Would you add a `LICENSE` file at the root
   of `yotthani/bannerlord`, so the grant is written down where the code lives? Today only `HoN/VanillaTuning` and
   `HoN/ShaderCacheKeeper` carry one. TAOM carries seven features that began as your HoN mods (SiegeDismount,
   MixedFormations, SmartCavalryAI, FiefManagement, QuickActions, EquipPresets, CompanionTactics) and credits them
   to you under MIT in its `THIRD-PARTY-LICENSES.txt`.
