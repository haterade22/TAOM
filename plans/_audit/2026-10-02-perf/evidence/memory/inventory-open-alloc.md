# Inventory and party screen open cost: the code path, 2026-10-02

Written for: the next engineer on TAOM's UI memory, and Mike. A read-only researcher (taleworlds-researcher,
no write access) produced this trace; the orchestrator saved it and re-checked the load-bearing claims
marked **[re-checked]** against the v1.5.3 decompile and the repo the same evening. Everything else is the
researcher's reading, and UNVERIFIED items stay UNVERIFIED.

## Summary

1. **Cause: cheat mode, not TAOM code.** [re-checked] With `Game.Current.CheatMode` true, vanilla's
   `InventoryScreenHelper.OpenInventoryPresentation` adds every `ItemObject` ten times to the left
   ("Discard") roster before the screen opens (v1.5.3 `Helpers.InventoryScreenHelper.cs:177-188`). Mike's
   `engine_config.txt` line 14 is `cheat_mode = 1`. The party screen does the same with every troop that
   passes the encyclopedia unit filter (`Helpers.PartyScreenHelper.cs:116-195`, researcher's reading).
   `OpenScreenAsTrade` (a town's trade screen) is not widened.
2. **Counts** (static XML of the eight active campaign modules): 5,165 distinct items (SandBoxCore 1,270,
   LOTRLOME_Armory 3,882, TAOM 13) and about 1,271 troops passing the party filter. Vanilla alone would
   list about 1,270 items.
3. **Per row:** 2.44 to 2.56 GB over 5,165 rows is 484 to 507 KB and 2.1 to 2.7 ms per row; each
   inventory row is an `InventoryItemTuple` of 85 widgets, so about 6 KB and 25 to 32 us per widget. The
   party screen's 536 to 756 MB over about 1,271 rows is 430 to 610 KB per row: the two screens agree
   within about 20%.
4. **A player's open without cheat mode** (200 to 300 market rows): about 95 to 150 MB and 0.4 to 0.8 s,
   plus the player's own rows. UNVERIFIED: no log without cheat mode exists. TAOM's testing instructions
   turn cheat mode on (`docs/features/dev-console.md`, `creature-bandits.md`, `animalia-elk-moose.md`), so
   testers and anyone following them pay the full cost.
5. **A possible multiplier on every screen:** [re-checked] `TAOM.Dependencies` sets
   `UIConfig.DoNotUseGeneratedPrefabs = true` (`Dependencies/SubModule.cs:28`), so every Gauntlet movie is
   built through the dynamic prefab path instead of the compiled generated one; UIExtenderEx locks the
   setter. Its size is UNVERIFIED.
6. **TAOM code on the path is O(1) per row or per screen** (`Patch33_SPInventoryVMRefresh`, the `Patch34`
   capture, search and finalize hooks, `PartyCharacterVM_RefreshValues_Patch`, the `Patch35` role
   decorator, the banner colour patches, `TaomInventoryCapacityModel`, the PresetsOverlay layer, the
   FactionUI movie swap). No game-wide scan was found. `ui_inventory`, `ui_partyscreen` and `ui_clan` are
   one 4096 x 1024 sheet each.
7. **After close: held by the closing call stack, briefly, not finalizer-delayed.**
   - [re-checked] TAOM's `[MemStation] enter` line is written from `ScreenManager.OnPushScreen`
     (`ScreenManager.cs:505`), which runs inside `GameStateManager.OnPushState`'s listener loop
     (`GameStateManager.cs:281-284`), before that method's `Common.MemoryCleanupGC()` (`:288`). A
     re-entry reading of 2,64x MB therefore follows one collection (the pop's, `:316`), not two.
   - The pop's collection runs inside the closing tick, with the popped screen or its layer still on the
     stack (`ScreenManager.Tick` holds the screen through `FrameTick`; `ScreenBase`, `GauntletLayer` and
     `GauntletMovie` keep their layers, movie identifiers and root view after release), so one reference
     reaches the whole widget tree. The next collection frees it.
   - Finalizers cannot explain it: none of `ViewModel`, `SPInventoryVM`, `SPItemVM`, `ItemVM`,
     `ScreenBase`, `GauntletLayer`, `GauntletMovie`, `GauntletView`, `Widget`, `UIContext` or the texture
     wrappers declares one; the only finalizer on the UI path (`ImageIdentifierTextureProvider`) holds no
     reference into the VM or widget tree, and the pop's collection kept 81% of the open's growth.
   - So the 2026-09-12 reading "the 2.6 GB survived two engine collections and is rooted"
     (`docs/features/battle-load-diagnostics.md`, "Why there is no heap release on screen close";
     `docs/investigations/native-commit-audit-2026-08.md`, the "Transient UI spikes" row) is wrong on
     the count of collections: it survived one, taken while the screen was still on the stack. A
     collection one frame after the pop would free about 2.2 GB at once. Which stack reference decides
     it is UNVERIFIED.

## Ranked suspects

| Rank | Suspect | Evidence | Scale | Source | Status |
|---|---|---|---|---|---|
| 1 | Cheat-mode all-items list | `InventoryScreenHelper.cs:177-188` | 5,165 rows x about 0.5 MB | vanilla x TAOM content | likely |
| 2 | Cheat-mode all-troops list | `PartyScreenHelper.cs:163-183` | about 1,271 rows x about 0.5 MB | vanilla x TAOM content | likely |
| 3 | Dynamic prefab path | `Dependencies/SubModule.cs:28` | a per-widget multiplier | TAOM environment | size UNVERIFIED |
| 4 | Per-row VM work | `SPItemVM.cs:462-491`, `InventoryTradeVM.cs:406-428` | about 10 to 30 KB per row | vanilla | small, UNVERIFIED |
| 5 | The roster fill before the push | `ItemRoster.AddToCounts` (a linear scan per add, about 13.3 M comparisons) | before `STATE push`, outside the measured stall | vanilla | likely minor |

## The measurements that would settle it (one session each)

- **P1, the attribution:** in the `SPInventoryVM` constructor postfix, one `[InvOpen]` line with
  `Game.Current.CheatMode`, the inventory mode, the left and right row counts and the `ItemObject` count;
  the same for the party screen. Left rows equal to the item count confirm it.
- **P2, VM against widgets:** time and measure allocation (`GC.GetAllocatedBytesForCurrentThread`, bound
  by reflection) around the `SPInventoryVM` constructor, `LoadMovie("Inventory")` and
  `LoadSpriteCategory`, count the widgets, and log
  `[InvCost] sprite_ms= vm_ms= vm_B= movie_ms= movie_B= widgets= gc=d0/d1/d2 rows=L+R`.
- **P3, what holds it after close:** `WeakReference` canaries to the VM and the layer at
  `SPInventoryVM.OnFinalize`, read on the first campaign tick after the pop, then after `GC.Collect()`,
  then after `WaitForPendingFinalizers` and a second collection. Dead after the first collection means
  stack rooting at the pop (and a deferred collection is the fix).
- **P4, the player's cost:** `cheat_mode = 0`, the same town's trade screen and the party screen, with P1
  and P2 on. This is FOR-MIKE item 12.

## Not known

The runtime row counts (static XML only), the VM against widget split, the dynamic-prefab multiplier,
which stack reference pins the graph, the cost without cheat mode, and the 380 to 720 MB of non-heap
private growth per open.
