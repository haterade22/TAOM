# Save growth after in-game reloads (SAVE-TIME-01): probes and repro matrix

> Hand this to a fresh session: "Read `docs/investigations/save-growth-2026-11.md` and run the next unrun cell."
> Trigger: E2 `rgl_log_22112`, public v1.4.8 build: one player's saves went from 91.5 s (17:44) to 1,460.8 s (the 21:18 autosave) within one 4.5-hour session.
> No fix exists yet. This file holds the instruments and the plan to name the carrier; the fix is a follow-up plan.

## What is known

Source: `plans/_audit/2026-11-04-port148/verify/V01-save-growth.md`, which re-derived 12 loads and 15 saves from that log.

- Object and container counts stay flat across about 80 campaign days of play and step up only across in-game loads, by about 71,500 containers (85,000 to 90,000 objects) per reload, 12 increments by 21:18.
- The 21:18 autosave held 2,343,646 objects and 1,884,752 containers against 1,302,023 and 1,026,584 in the file the session started from.
- A first load in a fresh process adds nothing (E1, and E2 load #1).

| Save (E2) | Loads in the process | Objects | Containers | Whole save s |
|---|---|---|---|---|
| 17:56 | 1 | 1,301,837 | 1,027,153 | 137.3 |
| 18:10 | 2 | 1,386,205 | 1,099,176 | 131.8 |
| 18:16 | 3 | 1,475,675 | 1,171,631 | 220.2 |
| 19:40 | 11 | 2,181,266 | 1,742,139 | 1092.4 |
| 21:18 | 12 | 2,343,646 | 1,884,752 | 1460.8 |

Open points:

- **Permanence is unproven.** Every carry-forward in the logs is inside one process, which cannot separate "carried in the file" from "re-attached from process state at each save". Cell 3 separates them.
- **One step per load is unproven.** Eight of the reloads have no save between them, so only their total is visible.
- **Stale XML objects are refuted** as the carrier: on v1.4.8 those types save only `StringId`, `Id` and `IsRegistered`.
- **H1:** SaveShield swallows a load-callback fault on a later load. The existing `phase=LoadFault` stamp and `Modules/TAOM.Dependencies/diag.log` show it.
- **H2:** an in-game load behaves differently from a main-menu load.
- The cells' alternatives: the vanilla engine (cell 4) or a third-party mod (cell 5).

## The probes

All of these are `[SaveLoad]` lines in `Logs/taom_debug_*.log`, always on and observe only.

| Line | Comes from | Read it for |
|---|---|---|
| `phase=Runtime` | `Main/SubModule.cs`, once at module load | `framework=`, `clr=`, `serverGC=`, `latency=`, `is64Bit=`: which runtime host this player runs. It is never a reason to advise server GC |
| `phase=LoadRequested` | `SandBoxSaveHelper.TryLoadSave` Prefix | `inGame=` (a game was running) and `loadsThisProcess=`, which counts load attempts, a load cancelled at the module prompt included; `inGameLoads=` is the in-game subset |
| `phase=LoadCounts` | `LoadContext.Load` Postfix | What the load read: `objects=` and `containers=` equal rgl `[GameData.Read] Object Length` and `Container Length`. `strings=` is a count with no rgl twin (rgl's string figure is a byte size) |
| `phase=GameLoaded`, `phase=GameInitializationFinished` | `SubModule.cs` overrides | `prevCampaign=none, collected or alive` plus `gen2Since=N`. Read it here: at least two full collections ran after the old game left the stack. Treat `alive` with `gen2Since=0` as no evidence |
| `phase=SaveBegin` | `SaveManager.Save` Prefix | Process context and memory at the start of a save |
| `phase=SaveBehaviorData` | `CampaignBehaviorDataStore.SaveBehaviorData` Prefix and Postfix, written after `SaveBegin` | One summary line (`behaviors= records= elements= syncMs= maxStringBytes=`), then a row for a behavior over 10,000 elements, 30,000 string bytes or 250 ms |
| `phase=SaveCollected` | `SaveContext.CollectObjects()` Prefix and Postfix | What the save is about to write: `objects=` and `containers=` equal rgl `[GameData.Write] ObjectData Length` and `ContainerData Length`; `strings=` has no rgl twin; `collectMs=` sits near rgl `SaveContext::CollectObjects completed in N seconds` |
| `phase=SaveCensus` | the same Postfix, on the first save after any load that is not the first in the process (`reason=afterReload`) or after `taom.arm_save_census` (`reason=console`) | A summary line, then `obj n=` and `ctr n= elements=` rows per type, with `unregistered=` and `dupIds=` for engine objects. `python tools/diff_save_census.py <taom_debug log>` compares two blocks |
| `phase=SaveCompleted` | `SaveOutput.PrintStatus` Postfix | Process context and memory at the end of a save, before the engine's own `Common.MemoryCleanupGC()` |

## Repro matrix

Laptop or desktop, v1.4.8, any TAOM campaign save S. BetterTime is not installed on the laptop, so every TAOM cell there is also a no-BetterTime cell.

| Cell | Steps (fresh process = start the game from the launcher) | Read | Result means |
|---|---|---|---|
| 1 in-game reload | Fresh process. Main menu, load S. Save to new slot A. Open the save/load screen over the map, load S, save to slot B. Load S again the same way, save to slot C. | `LoadRequested inGame=true loadsThisProcess=`; `SaveCollected` for A, B, C; `SaveCensus` blocks at B and C (reason=afterReload); then `python tools/diff_save_census.py <taom_debug log>` | B and C each above the save before by tens of thousands of containers: reproduced, and the diff names the growing types |
| 2 main-menu reload | Fresh process. Load S, save A. Exit to the main menu. Load S, save B. | second `LoadRequested` reads `inGame=false loadsThisProcess=2`; `SaveCollected` A vs B | B about one increment above A: any second load in a process grows saves. B about equal to A: only in-game loads do |
| 3 does a restart repair? | Quit to the desktop. Fresh process. Load slot C from cell 1, save at once to slot D. | `LoadCounts` (C as read) vs `SaveCollected` (D) | D about C: the growth is in the file, permanent. D about A: process state re-attached it, and a restart repairs a save |
| 4 vanilla control | Only Native, SandBoxCore, Sandbox, StoryMode, CustomBattle; a vanilla save; cell 1 steps. | rgl `[GameData.Write] ObjectData Length` / `ContainerData Length` per save | Growth here: TaleWorlds behaviour, mitigation only. None: a mod carries it |
| 5 BetterTime | Only if cells 1 to 4 do not reproduce: cell 1 with BetterTime added (the player's list) | as cell 1 | Growth only with BetterTime: third-party carrier |
| 6 ButterLib Save System (optional, SAVE-TIME-09) | On a throwaway copy of a save only (saves written with the subsystem on may not load with it off), compare `collectMs` with ButterLib's Save System subsystem on and off. Where ButterLib exposes the toggle on 1.4.8 is UNVERIFIED. | `SaveCollected collectMs` | a large difference: ButterLib's `IsContainer` prefix is a measurable save cost (secondary) |

In every TAOM cell also: `grep -n 'phase=LoadFault' <taom_debug log>` and look for SaveShield swallow lines in `Modules/TAOM.Dependencies/diag.log` (H1). The console baseline is optional: `taom.arm_save_census` needs cheat mode; do not open the inventory with cheat mode on during a memory reading (one cheat-mode open added about 2.4 GB in E4, MEMORY-01).

## Decision table

| Observation | Conclusion | Next (a follow-up plan, never this WP) |
|---|---|---|
| Census rows for engine types with `dupIds` or `unregistered` above 0 that grow by about one increment per reload | stale copies of registered objects | name the holder (referrer probe, see `plans/114-save-growth-investigation.md` Maintenance notes), then fix; consider a load-time dedupe only if the copies are unreachable from live play |
| `phase=LoadFault` on a second load, or SaveShield swallow lines in diag.log | H1: a load callback fails on later loads and SaveShield hides it | fix the throwing callback, re-run cell 1 |
| Cell 1 grows, cell 2 does not | in-game load path | research `SandBoxSaveHelper` in-game load path on v1.4.8 |
| Cell 4 grows | engine behaviour | player advice plus a TAOM mitigation plan; report to TaleWorlds |
| Cell 3: D about A | a restart repairs a save | tell players to restart, load and save once |
| Growing rows are containers of a TAOM type | TAOM carrier | plan scoped to that feature |
| `prevCampaign=alive` at GameInitializationFinished | the previous campaign is still reachable (MEMORY-05) | name the static holder with a heap snapshot; if it reads `collected` and the menu `heapMB` stays above 300, look at process caches, not TAOM singletons (V07) |

## Player advice that is safe today

From V01 section 7.

- Do not reload inside a running session. To retry, exit to the desktop, start the game, then load. Quitting only to the main menu is untested.
- Do not count on a large save shrinking.
- Older saves of the campaign are smaller but lose progress.
- Lengthen the autosave interval and save by hand at quiet moments.
- Send `rgl_log_*.txt` and `taom_debug_*.log` with any report.

Not advised: server GC or any `.exe.config` edit, removing BetterTime from a running campaign, editing save files.

## Engine notes

The save system's general notes are in [`docs/reference/engine/save-system.md`](../reference/engine/save-system.md). The v1.4.8 save-cost mechanics (Containers phase, per-element allocations) are documented by the 1.5.x section WP144 ports, re-checked in `plans/_audit/2026-11-04-port148/deepdive/save-time.md` (SAVE-TIME-02) with V01's correction that equal-content `TextObject`s do not collapse in the save.
