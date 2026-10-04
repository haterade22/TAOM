# Campaign memory, re-read 2026-10-02

Written for: Mike and the next engineer on TAOM's memory work. It replaces the first-pass
"outside-mission growth" figures in this run (82 to 534 MB/min), which mixed campaign loading, the
map's entry streaming and inventory opens into one rate.

**Sources.** The 30 `taom_debug` logs still on disk (2026-09-28 to 2026-10-02, Bannerlord v1.5.3, TAOM
Debug builds) and the 2026-09-12 archive `E:\taom-memory-2026-09-02\` (Bannerlord v1.4.8). All on
Mike's desktop (63 GB RAM). `privMB` is process private commit, `heapMB` is `GC.GetTotalMemory(false)`
(`MemorySampleReader.cs:81`), both from TAOM's `[MemSample]` (10 s timer thread) and `[MemStation]`
(one line per screen enter and exit) lines. The scripts that produced every number are in this
folder: `mem_growth2.py` (growth per top screen), `map_drift.py` (map drift after entry),
`inv_open_scan.py` (inventory-open stall), `stations_table.py` (the tables below).

**Cheat mode.** The desktop's current `engine_config.txt` (OneDrive Documents, Configs) has
`cheat_mode = 1` on line 14, and the 2026-09-12 audit recorded cheat mode on
(`docs/investigations/native-commit-audit-2026-08.md:398`). With cheat mode the inventory lists every
item in the game and the party screen every troop, so the UI rows below are upper bounds, not a
player's cost.

## 1. Where the private bytes go

| Stage | 2026-09-12 (v1.4.8) | Since 2026-09-28 (v1.5.3) | Reading |
|---|---|---|---|
| Main menu, `GauntletInitialScreen` enter at session start | 6,706 to 6,788 MB (4 sessions) | 7,463 to 7,860 MB (29 sessions) | About +0.8 GB, across an engine version change and three weeks of content; the menu-floor subtraction ladder (`native-commit-audit-2026-08.md`, "Menu floor") is what would split it |
| First campaign load of a session, `GameLoadingScreen` enter to exit | +2.7 to +3.0 GB in 119 to 123 s | +2.5 to +2.7 GB in 32 to 50 s (22 loads; the 17 to 22 s loads of +0.6 to +0.7 GB are a different game type and are left out) | About the same memory, three to four times faster; about 2.1 GB of it is the campaign map scene's read (below) |
| Map entry, `MapScreen` enter to 5 minutes later | +3,481 MB in 39 s with no input, then flat (audit) | 10-01 14:18: 10,945 to 13,095 MB (+2,150), then flat; 10-01 08:11: 12,891 to 13,034 MB | Texture streaming on first view, once per load (it tracks TAOM_Map's texture payload, below) |
| Map, after the first 5 minutes | 60 to 100 MB/min at normal speed with 447 auto-resolves in 5 minutes, managed +30 MB of it (audit) | +16 and -7 MB/min (the only two 10+ minute map stretches on disk, 16 minutes each) | No steady leak in what is on disk. The 2026-09-02 session that showed +8.4 GB in 37 minutes with no mission is no longer on disk, so it is neither confirmed nor ruled out |
| A battle | 2 to 3 GB taken and returned (`battle-load-diagnostics.md:420-427`) | not re-measured here | |

**The campaign map costs memory twice, and neither step loads mesh edit data.**

- *At load:* v1.5.3 `Campaign.DoLoadingForGameType` runs `Game.Initialize` (which ends in
  `OnGameInitializationFinished`), then `LoadMapScene`, which reads the whole `Main_map` scene
  (`MapScene.Load`: `Scene.Read("Main_map", ...)`, then `SetAllocationAlwaysValidScene` and
  `OptimizeScene`). In the logs, private memory rises **+2,069 MB within 5 s** of the
  `[SaveLoad] phase=GameInitializationFinished` line (2026-10-01: 8,158 to 10,227 MB at 14:15:29 to
  14:15:34, then flat to the loading screen's exit), +2,210 MB on 2026-10-02.
- *At the first look at the map:* the map-entry growth is texture streaming. It fell from +3,481 MB
  (2026-09-12) to +2,150 MB (2026-10-01), a drop of 1,331 MB, while TAOM_Map's texture payload in the
  same install fell by 1,332.0 MiB with the 2026-09-13 downsizing: 2,511,558,440 B on 2026-09-12
  (`E:\taom-memory-2026-09-02\map-manifest\totals.tsv`) to 1,114,855,208 B today. Today's figure is
  read from the live install, whose TAOM_Map is now the Kit's loose layout (1,934 tpacs under `Assets`,
  an 18 GB RuntimeDataCache, no `AssetPackages`), with a scratch copy of
  `tools/audit_map_scene_memory.py` that also indexes `Assets` (output `evidence/memory/live/totals.tsv`;
  texture bytes come from each header, so
  they hold for loose files; the patreon pack's 1,119,748,536 B is 4.9 MB more). In the loose layout
  a mesh tpac carries only its edit data; the render buffers live in the RuntimeDataCache.
- *Edit data:* the scene's mesh render buffers are 1,283 MiB (TAOM_Map 770,921,754 B and the Native
  meshes it references 575,027,216 B); their edit data would add 1,599 MiB (1,090,829,692 B and
  586,198,724 B). Render buffers plus edit data would need at least 2,882 MiB at the scene read, more
  than the measured +2,069 to +2,210 MB, which also has to hold terrain, navmesh, flora and physics.
  So the logs agree with the engine code: static meshes' edit data is not loaded in play (engine
  reference page section 7; `evidence/native/native-mesh-editdata.txt`).

So the campaign map holds about 4.2 GB on Mike's machine today (about 2.1 GB read at load, about
2.15 GB streamed on first view), down from about 6.1 GB on 2026-09-12.

## 2. UI screens (cheat mode on)

- **Inventory:** every open, 16 of 16 across both periods, ends with 2.7 to 3.3 GB of managed heap
  (exit lines 2,694 to 3,297 MB; the two opens with no exit line read 3,014 and 3,032 MB 15 s in),
  from 475 to 570 MB before a session's first open. Private bytes rise 2.9 to 3.4 GB on a first open
  and peak at 16.0 to 17.3 GB. Since
  2026-09-28 the log also shows the main thread silent for **11 to 14 s** after
  `[MapLoad] STATE push InventoryState` (5 of 5 opens), while the timer thread's samples show the heap
  climbing at roughly 150 to 200 MB/s; the next main-thread line is the character tableau's first
  tick. The same ~2.5 GB appears in an Erebor town as the player and with lord `lord_1_75` as the character on another
  day, so it does not follow the visible roster (town markets are capped near 200 distinct items,
  `MarketplaceTuning.cs:29`). Cheat mode's every-item list is the likely reason.
- **Party screen:** every open ends with 1.10 to 1.24 GB of managed heap, from 474 to 606 MB before a
  session's first open; private bytes rise 1.2 to 1.4 GB on a first open.
- **Clan, character developer:** no material change.

## 3. After the inventory closes

The heap drops only about 0.5 GB at close and stays near 2.6 to 2.8 GB until the next game-state
change (2026-09-28: inventory exit 3,293 MB, clan screen enter 2,746 MB, clan screen exit 502 MB;
2026-09-29: inventory exit 3,297 MB, 2,761 MB four seconds later, 495 MB at the next mission open).
The engine collects on every state push and pop (`GameStateManager.OnPushState`/`OnPopState`, through
`Common.MemoryCleanupGC`), and in v1.5.3 that function is a bare `GC.Collect()`
(`TaleWorlds.Library.Common.cs:242-246`), with no wait for finalizers.

**Settled by the code trace (`inventory-open-alloc.md`): held by the closing call stack, not by a
finalizer.** TAOM's `[MemStation] enter` line is written before the push's own collection
(`GameStateManager.cs:281-288`), so a re-entry reading follows one collection, the pop's, and that one
runs while the closing screen is still on the stack. No type on the UI graph has a finalizer that
reaches it. The next collection frees it; one taken a frame after the pop would free about 2.2 GB at
once. And the open itself is large because cheat mode makes vanilla list every item in the game
(`InventoryScreenHelper.cs:177-188`): about 5,165 rows at about 0.5 MB each.

## 4. Not known yet

1. The player's cost of an inventory and a party open, with cheat mode off (FOR-MIKE item 12);
   estimated at 95 to 150 MB and 0.4 to 0.8 s from the per-row cost, UNVERIFIED.
2. What the extra ~0.8 GB at the main menu is.

## Tables

All `[MemStation]` enters and exits for the screens above, both periods. An empty exit means the log
ended with the screen open (no crash marker was found in the next session's log).

### GauntletInitialScreen

| Log | Enter | privMB enter | heapMB enter | privMB exit | heapMB exit | Open s |
|---|---|---|---|---|---|---|
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 11:31:38 | 6,788 | 77 | 6,895 | 301 | 353 |
| `taom_debug_2026-09-12_11-22-17.log` | 09-12 11:23:40 | 6,773 | 77 | 6,482 | 74 | 50 |
| `taom_debug_2026-09-12_16-00-19.log` | 09-12 16:01:11 | 6,767 | 77 | 6,505 | 74 | 3513 |
| `taom_debug_2026-09-12_16-00-19.log` | 09-12 17:31:59 | 11,951 | 523 | 10,273 | 473 | 2 |
| `taom_debug_2026-09-12_19-26-45.log` | 09-12 19:28:08 | 6,706 | 77 | 6,405 | 74 | 1639 |
| `taom_debug_2026-09-28_20-54-06.log` | 09-28 20:54:13 | 7,564 | 74 | 7,562 | 82 | 6 |
| `taom_debug_2026-09-29_06-38-12.log` | 09-29 06:38:19 | 7,640 | 74 | 7,592 | 81 | 500 |
| `taom_debug_2026-09-29_06-38-12.log` | 09-29 06:59:06 | 11,566 | 393 | 10,114 | 346 | 36 |
| `taom_debug_2026-09-29_08-07-26.log` | 09-29 08:07:34 | 7,634 | 74 | 7,628 | 82 | 36 |
| `taom_debug_2026-09-29_08-07-26.log` | 09-29 08:13:12 | 10,818 | 348 | 9,542 | 302 | 4 |
| `taom_debug_2026-09-29_08-29-26.log` | 09-29 08:29:32 | 7,586 | 74 | 7,557 | 82 | 139 |
| `taom_debug_2026-09-29_08-29-26.log` | 09-29 08:51:42 | 11,675 | 383 | 10,363 | 331 | 3 |
| `taom_debug_2026-09-29_09-58-29.log` | 09-29 09:59:08 | 7,546 | 74 | 7,542 | 82 | 20 |
| `taom_debug_2026-09-29_10-15-28.log` | 09-29 10:15:49 | 7,860 | 74 | 7,668 | 81 | 709 |
| `taom_debug_2026-09-29_10-46-24.log` | 09-29 10:49:03 | 7,741 | 74 | 7,671 | 81 | 130 |
| `taom_debug_2026-09-29_12-24-09.log` | 09-29 12:24:16 | 7,606 | 74 | 7,562 | 81 | 275 |
| `taom_debug_2026-09-29_13-09-29.log` | 09-29 13:11:15 | 7,688 | 74 | 7,630 | 81 | 39 |
| `taom_debug_2026-09-29_19-22-43.log` | 09-29 19:23:19 | 7,488 | 74 | 7,448 | 73 | 25 |
| `taom_debug_2026-09-29_19-29-26.log` | 09-29 19:30:11 | 7,463 | 74 | 7,426 | 73 | 41 |
| `taom_debug_2026-09-29_19-47-09.log` | 09-29 19:47:51 | 7,475 | 74 | 7,416 | 73 | 17 |
| `taom_debug_2026-09-30_05-47-25.log` | 09-30 05:48:07 | 7,494 | 74 | 7,400 | 73 | 28 |
| `taom_debug_2026-09-30_12-41-24.log` | 09-30 12:42:02 | 7,530 | 74 | 7,487 | 82 | 13 |
| `taom_debug_2026-09-30_12-41-24.log` | 09-30 12:55:23 | 11,376 | 356 | 10,332 | 302 | 2 |
| `taom_debug_2026-09-30_13-13-05.log` | 09-30 13:13:13 | 7,549 | 74 | 7,542 | 82 | 39 |
| `taom_debug_2026-09-30_13-13-05.log` | 09-30 13:18:24 | 10,920 | 356 | 9,800 | 302 | 3 |
| `taom_debug_2026-09-30_18-19-42.log` | 09-30 18:20:19 | 7,527 | 74 | 7,497 | 82 | 7 |
| `taom_debug_2026-09-30_20-43-46.log` | 09-30 20:43:53 | 7,560 | 74 | 7,557 | 82 | 11 |
| `taom_debug_2026-10-01_08-03-05.log` | 10-01 08:03:11 | 7,584 | 74 | 7,551 | 82 | 39 |
| `taom_debug_2026-10-01_08-09-53.log` | 10-01 08:09:59 | 7,597 | 74 | 7,568 | 82 | 13 |
| `taom_debug_2026-10-01_08-36-01.log` | 10-01 08:36:08 | 7,550 | 74 | 7,541 | 82 | 43 |
| `taom_debug_2026-10-01_14-14-01.log` | 10-01 14:14:09 | 7,702 | 77 | 7,699 | 83 | 54 |
| `taom_debug_2026-10-01_20-43-36.log` | 10-01 20:43:44 | 7,512 | 75 | 7,500 | 83 | 18 |
| `taom_debug_2026-10-02_07-24-19.log` | 10-02 07:24:37 | 7,607 | 76 | 7,589 | 83 | 231 |
| `taom_debug_2026-10-02_11-05-27.log` | 10-02 11:06:05 | 7,537 | 76 | 7,531 | 83 | 85 |
| `taom_debug_2026-10-02_11-05-27.log` | 10-02 11:14:03 | 11,082 | 334 | 9,886 | 279 | 2 |
| `taom_debug_2026-10-02_11-05-27.log` | 10-02 11:17:46 | 10,556 | 300 |  |  |  |
| `taom_debug_2026-10-02_11-38-06.log` | 10-02 11:38:51 | 7,493 | 76 | 7,468 | 74 | 19 |
| `taom_debug_2026-10-02_11-38-06.log` | 10-02 11:51:22 | 9,951 | 104 | 9,412 | 111 | 2 |
| `taom_debug_2026-10-02_11-56-06.log` | 10-02 11:56:47 | 7,553 | 76 | 7,549 | 83 | 15 |
| `taom_debug_2026-10-02_12-40-04.log` | 10-02 12:40:44 | 7,562 | 76 | 7,562 | 83 | 255 |
| `taom_debug_2026-10-02_13-16-25.log` | 10-02 13:17:01 | 7,606 | 76 |  |  |  |
| `taom_debug_2026-10-02_14-30-27.log` | 10-02 14:30:35 | 7,586 | 76 | 7,560 | 74 | 180 |
| `taom_debug_2026-10-02_14-30-27.log` | 10-02 14:41:51 | 9,594 | 103 | 8,810 | 101 | 4 |

### GameLoadingScreen

| Log | Enter | privMB enter | heapMB enter | privMB exit | heapMB exit | Open s |
|---|---|---|---|---|---|---|
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 11:37:31 | 6,895 | 300 | 9,634 | 342 | 119 |
| `taom_debug_2026-09-12_11-22-17.log` | 09-12 11:24:30 | 6,578 | 74 | 9,588 | 320 | 121 |
| `taom_debug_2026-09-12_16-00-19.log` | 09-12 16:59:44 | 6,595 | 74 | 9,571 | 349 | 123 |
| `taom_debug_2026-09-12_16-00-19.log` | 09-12 17:29:05 | 14,104 | 577 | 11,758 | 545 | 53 |
| `taom_debug_2026-09-12_19-26-45.log` | 09-12 19:55:27 | 6,498 | 74 | 9,528 | 318 | 122 |
| `taom_debug_2026-09-28_20-54-06.log` | 09-28 20:54:20 | 7,562 | 81 | 10,187 | 374 | 33 |
| `taom_debug_2026-09-29_06-38-12.log` | 09-29 06:46:39 | 7,595 | 81 | 10,233 | 397 | 35 |
| `taom_debug_2026-09-29_06-38-12.log` | 09-29 06:59:42 | 10,200 | 346 | 12,073 | 605 | 29 |
| `taom_debug_2026-09-29_08-07-26.log` | 09-29 08:08:10 | 7,629 | 81 | 10,358 | 352 | 33 |
| `taom_debug_2026-09-29_08-07-26.log` | 09-29 08:13:16 | 9,627 | 302 |  |  |  |
| `taom_debug_2026-09-29_08-29-26.log` | 09-29 08:31:51 | 7,557 | 81 | 10,161 | 396 | 35 |
| `taom_debug_2026-09-29_08-29-26.log` | 09-29 08:51:46 | 10,449 | 331 | 11,783 | 590 | 32 |
| `taom_debug_2026-09-29_09-58-29.log` | 09-29 09:59:28 | 7,542 | 81 | 10,191 | 397 | 37 |
| `taom_debug_2026-09-29_10-15-28.log` | 09-29 10:27:38 | 7,672 | 81 | 10,158 | 354 | 33 |
| `taom_debug_2026-09-29_10-46-24.log` | 09-29 10:51:13 | 7,671 | 81 | 10,361 | 322 | 34 |
| `taom_debug_2026-09-29_12-24-09.log` | 09-29 12:28:51 | 7,568 | 81 | 10,268 | 397 | 33 |
| `taom_debug_2026-09-29_13-09-29.log` | 09-29 13:11:54 | 7,630 | 81 | 10,326 | 324 | 32 |
| `taom_debug_2026-09-29_19-22-43.log` | 09-29 19:23:44 | 7,448 | 72 | 8,054 | 162 | 19 |
| `taom_debug_2026-09-29_19-29-26.log` | 09-29 19:30:53 | 7,426 | 72 | 8,102 | 162 | 19 |
| `taom_debug_2026-09-29_19-47-09.log` | 09-29 19:48:08 | 7,416 | 72 | 8,102 | 114 | 19 |
| `taom_debug_2026-09-30_05-47-25.log` | 09-30 05:48:35 | 7,400 | 72 | 8,023 | 182 | 17 |
| `taom_debug_2026-09-30_12-41-24.log` | 09-30 12:42:15 | 7,487 | 81 | 10,212 | 321 | 36 |
| `taom_debug_2026-09-30_12-41-24.log` | 09-30 12:55:25 | 10,418 | 301 | 10,534 | 386 | 16 |
| `taom_debug_2026-09-30_13-13-05.log` | 09-30 13:13:52 | 7,542 | 82 | 10,132 | 353 | 34 |
| `taom_debug_2026-09-30_13-13-05.log` | 09-30 13:18:27 | 9,885 | 301 | 10,025 | 498 | 16 |
| `taom_debug_2026-09-30_18-19-42.log` | 09-30 18:20:26 | 7,497 | 81 | 10,128 | 355 | 33 |
| `taom_debug_2026-09-30_20-43-46.log` | 09-30 20:44:04 | 7,557 | 81 | 10,157 | 353 | 33 |
| `taom_debug_2026-10-01_08-03-05.log` | 10-01 08:03:50 | 7,551 | 82 | 10,153 | 353 | 33 |
| `taom_debug_2026-10-01_08-09-53.log` | 10-01 08:10:12 | 7,568 | 81 | 10,194 | 353 | 33 |
| `taom_debug_2026-10-01_08-36-01.log` | 10-01 08:36:51 | 7,541 | 81 | 10,140 | 355 | 33 |
| `taom_debug_2026-10-01_14-14-01.log` | 10-01 14:15:03 | 7,707 | 83 | 10,289 | 395 | 39 |
| `taom_debug_2026-10-01_20-43-36.log` | 10-01 20:44:02 | 7,532 | 83 | 10,190 | 401 | 36 |
| `taom_debug_2026-10-02_07-24-19.log` | 10-02 07:28:28 | 7,607 | 83 | 10,247 | 398 | 38 |
| `taom_debug_2026-10-02_11-05-27.log` | 10-02 11:07:30 | 7,563 | 83 | 10,186 | 353 | 35 |
| `taom_debug_2026-10-02_11-05-27.log` | 10-02 11:14:05 | 9,971 | 278 | 10,105 | 363 | 16 |
| `taom_debug_2026-10-02_11-38-06.log` | 10-02 11:39:10 | 7,504 | 74 | 8,159 | 116 | 22 |
| `taom_debug_2026-10-02_11-38-06.log` | 10-02 11:51:24 | 9,497 | 110 | 11,027 | 363 | 37 |
| `taom_debug_2026-10-02_11-56-06.log` | 10-02 11:57:02 | 7,581 | 83 | 10,264 | 344 | 42 |
| `taom_debug_2026-10-02_12-40-04.log` | 10-02 12:45:00 | 7,580 | 83 | 10,272 | 344 | 50 |
| `taom_debug_2026-10-02_14-30-27.log` | 10-02 14:33:35 | 7,570 | 74 | 8,224 | 185 | 17 |

### GauntletInventoryScreen

| Log | Enter | privMB enter | heapMB enter | privMB exit | heapMB exit | Open s |
|---|---|---|---|---|---|---|
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:10:15 | 14,314 | 475 | 17,259 | 3,159 | 20 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:10:39 | 16,601 | 2,649 | 17,136 | 3,156 | 19 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:11:03 | 16,548 | 2,644 | 17,199 | 3,152 | 16 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:11:24 | 16,555 | 2,648 | 17,115 | 3,157 | 18 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:11:49 | 16,581 | 2,648 | 17,129 | 3,156 | 18 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:12:12 | 16,664 | 2,645 | 17,238 | 3,156 | 16 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:12:36 | 16,554 | 2,647 | 17,258 | 3,152 | 15 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:12:55 | 16,834 | 2,644 | 17,260 | 3,156 | 18 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:13:15 | 16,848 | 2,649 | 17,129 | 3,153 | 23 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:13:41 | 16,841 | 2,647 | 17,263 | 3,152 | 16 |
| `taom_debug_2026-09-12_16-00-19.log` | 09-12 17:26:58 | 14,281 | 489 | 17,285 | 2,694 | 17 |
| `taom_debug_2026-09-28_20-54-06.log` | 09-28 20:55:19 | 12,676 | 570 | 16,055 | 3,293 | 81 |
| `taom_debug_2026-09-29_06-38-12.log` | 09-29 06:52:27 | 13,055 | 567 | 16,135 | 3,281 | 154 |
| `taom_debug_2026-09-29_08-29-26.log` | 09-29 08:45:01 | 13,513 | 545 | 16,920 | 3,297 | 268 |
| `taom_debug_2026-09-29_09-58-29.log` | 09-29 10:05:47 | 12,708 | 565 |  |  |  |
| `taom_debug_2026-10-02_07-24-19.log` | 10-02 07:44:27 | 12,955 | 477 |  |  |  |

### GauntletPartyScreen

| Log | Enter | privMB enter | heapMB enter | privMB exit | heapMB exit | Open s |
|---|---|---|---|---|---|---|
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:05:42 | 13,402 | 478 | 14,759 | 1,234 | 11 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:05:55 | 14,291 | 768 | 14,724 | 1,101 | 12 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:06:08 | 14,341 | 770 | 14,767 | 1,103 | 13 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:06:23 | 14,339 | 767 | 14,824 | 1,233 | 13 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:06:50 | 14,221 | 768 | 14,897 | 1,101 | 9 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:07:01 | 14,309 | 771 | 14,754 | 1,101 | 12 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:07:14 | 14,335 | 768 | 14,801 | 1,236 | 11 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:07:26 | 14,347 | 768 | 14,762 | 1,101 | 13 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:07:43 | 14,261 | 770 | 14,789 | 1,106 | 11 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 12:07:55 | 14,329 | 769 | 14,811 | 1,236 | 12 |
| `taom_debug_2026-10-01_20-43-36.log` | 10-01 20:47:19 | 12,535 | 474 | 13,762 | 1,230 | 7 |
| `taom_debug_2026-10-02_11-05-27.log` | 10-02 11:11:40 | 12,753 | 606 | 13,923 | 1,204 | 133 |

### GauntletClanScreen

| Log | Enter | privMB enter | heapMB enter | privMB exit | heapMB exit | Open s |
|---|---|---|---|---|---|---|
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 11:58:06 | 13,793 | 477 | 13,795 | 550 | 13 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 14:35:58 | 14,276 | 482 | 14,514 | 482 | 3 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 14:36:05 | 14,297 | 484 | 14,444 | 483 | 4 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 14:36:12 | 14,377 | 483 | 14,430 | 479 | 4 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 14:36:20 | 14,332 | 482 | 14,390 | 484 | 4 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 14:36:28 | 14,278 | 481 | 14,415 | 482 | 6 |
| `taom_debug_2026-09-12_19-26-45.log` | 09-12 19:59:54 | 12,797 | 473 | 13,183 | 617 | 58 |
| `taom_debug_2026-09-28_20-54-06.log` | 09-28 20:57:22 | 15,729 | 2,746 | 13,630 | 502 | 16 |
| `taom_debug_2026-09-29_06-38-12.log` | 09-29 06:58:40 | 13,135 | 526 | 13,899 | 549 | 21 |
| `taom_debug_2026-10-02_11-05-27.log` | 10-02 11:13:57 | 13,227 | 769 | 13,150 | 471 | 2 |
| `taom_debug_2026-10-02_11-38-06.log` | 10-02 11:54:44 | 13,161 | 610 | 13,774 | 516 | 41 |

### GauntletCharacterDeveloperScreen

| Log | Enter | privMB enter | heapMB enter | privMB exit | heapMB exit | Open s |
|---|---|---|---|---|---|---|
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 14:35:10 | 14,349 | 480 | 14,288 | 486 | 10 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 14:35:24 | 14,303 | 486 | 14,450 | 482 | 3 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 14:35:28 | 14,321 | 483 | 14,367 | 483 | 5 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 14:35:37 | 14,255 | 487 | 14,366 | 485 | 3 |
| `taom_debug_2026-09-12_11-30-46.log` | 09-12 14:35:46 | 14,236 | 482 | 14,433 | 484 | 6 |
| `taom_debug_2026-10-01_20-43-36.log` | 10-01 20:45:42 | 12,735 | 616 | 12,844 | 478 | 81 |
