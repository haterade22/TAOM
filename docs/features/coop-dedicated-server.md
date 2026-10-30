# Co-op dedicated server (operator page)

## Overview

How to stage, boot, stop and diagnose TAOM on the BannerlordCoop dedicated server. This is the operator page: the load work is [ADR-013](../adrs/013-server-safe-core-client-ui-submodule.md) and [taom-headless-server](../research/taom-headless-server.md), and the gating rules are [coop-interop](coop-interop.md). It restates none of them.

## Why This Exists

- **Vanilla behavior:** the v1.5.5 dedicated server loads the vanilla modules and a vanilla world.
- **TAOM requirement:** the server has to load TAOM's simulation, its map and the Armory's creature data.
- **Without this page:** the boot steps, the stop trap and the known log noise lived in a research plan, one stale bullet in coop-interop and a lookup row.

## Status

| Claim | State |
|---|---|
| Bannerlord v1.5.6 (current target, 2026-10-29) | **Blocked on Steam.** The client and TAOM are on v1.5.6, and Coop's `bannerlord-1.5.x` follows it (`2fab86882`, deployed to the dev install); see [v1.5.6-impact](../migration/v1.5.6-impact.md). The dedicated server app (1863440) is still the v1.5.5 build `25805354`, and co-op refuses a client whose game version differs from the server's. TaleWorlds' v1.5.6 server also moves to .NET 8, which the server repo's runtime staging (`scripts/prepare-dotnet-runtime.ps1`, `6.0.x` only) must follow. Every row below was proven on v1.5.5 |
| Vanilla v1.5.5 server | Booted to SERVING, clients joined, battles fought, a client rejoined (2026-10-21 and 2026-10-22) |
| TAOM, spike build | Reached SERVING on 2026-10-22 and again on 2026-10-23 (probe boots, including the skeleton package). The spike is a throwaway worktree (`taom-headless-spike`, branch `spike/headless-local`, never pushed) staged as `stage-spike` |
| TAOM, committed build | Reached SERVING on 2026-10-23 (`0fb0e6c13`, deployed with the `bdef312b4` fix) on save `taomcoop2`: the 107-class `[ClientOnly]` line, exactly two `SKIPPED` screens, `failed=0` in OnSubModuleLoad (31 categories) and GameInit (79), autosaves every 5 minutes, 0 TAOM crash reports |
| Clients joining a TAOM server | One client on the same desktop, 2026-10-23: TAOM character creation, 11 in-game days with time running, Minas Tirith entered, a field battle fought and closed, the server autosaved during the battle. No `set_MapEventSide` exception. Two clients on two machines: owed |

## Parts and owners

| Part | Owner | What it does |
|---|---|---|
| `bin/Win64_Shipping_Server/` mirror, `DsAssetPackages/` | TAOM (this repo) | The assembled client folder mirrored for the server, and the creature skeleton package |
| `windows/run-windows.ps1` | `BannerlordCoop.DedicatedServer` repo (Garrett's) | Stages the engine and the extra modules, data only |
| `MapEventLoadCleaner`, the deserter fix | BannerlordCoop repo | Server-side repairs; nothing in this repo |

## Stage and boot

Stage with the server repo's script (its `-KeepSubModules` option and the rules for extra modules are in [taom-headless-server](../research/taom-headless-server.md), "Staging"). The script's exclude list does not name `DsAssetPackages`, so a module's package folder reaches the server. The script reads TAOM from the game install, not from this repo, so stage after a deploying build.

1. **Server map.** The server loads a stripped copy of TAOM_Map's `Main_map` from `-ModuleAssets`. Build it again after every TAOM_Map scene save: `navmesh.bin` must be byte-identical to the client's, because the server paths on it and the distance cache indexes its faces. The 2026-10-17 copy was stale by 2026-10-23 (navmesh 879,614 against 880,433 bytes).
   ```
   python -I <server repo>\scripts\strip_main_map.py "<game>\Modules\TAOM_Map\SceneObj\Main_map" <assets>\SceneObj\Main_map
   ```
   It prints `verify OK` and writes `extents.json`. `<assets>\DsAssetPackages` takes the server repo's three stock packages (`core.tpac`, `pack_3.tpac`, `pack_19.tpac`, byte-identical to the vanilla set).
2. **Stage** (PowerShell 7; `-KeepSubModules` is a hashtable, so it cannot pass through `pwsh -File` arguments: put the call in a script file):
   ```
   & .\windows\run-windows.ps1 -PrepareOnly -GameRoot <game> -DedicatedServerRoot <ds app> -ModuleAssets <assets> `
       -Modules 'TAOM.Dependencies','Native','SandBoxCore','Sandbox','LOTRLOME_Armory','TAOM_Map','TAOM','Coop','DedicatedServer.Windows' `
       -ExtraModules 'TAOM.Dependencies','LOTRLOME_Armory','TAOM_Map','TAOM' `
       -KeepSubModules @{ 'TAOM.Dependencies' = @('TAOM.Dependencies'); 'TAOM' = @('TAOM') }
   ```
   Check the staged `Modules\TAOM`: only the `TAOM` submodule, no `TAOM.Client.dll`, and `DsAssetPackages\taom_creature_skeletons.tpac` present.
Once server PRs #104 and #105 are merged, step 1 and the `-KeepSubModules` argument fall away: the staging picks TAOM's submodules from its manifest and builds the map from the installed TAOM_Map (see "Distribution").

3. **Data directory:** `server-config.json` (`saveName`, `port` 4200, `steam`, and the same `modules` list as above), `Game Saves\<saveName>.sav`, and `eula.txt`. The save must come from a campaign started with the installed TAOM release (see "Saves" below).

The data directory holds `eula.txt`. The operator accepts the EULA there. Never pass `--accept-eula` for someone else.

Boot, from the staged folder:

```
.\BannerlordCoopServer.exe --data-dir <dir>
```

Add `--no-tui` for a log-only run. A healthy boot reaches LOADED, then SERVING, then an autosave. With no client connected the pulse line reads `timeMode=Stop players=0`.

## Stopping

**Trap:** if the launcher is ended hard (Task Manager, `taskkill /f`, `Stop-Process -Force`), its engine child `dotnet.exe TaleWorlds.Starter.DotNetCore.dll ... /dedicatedcustomserver` keeps running and holds UDP 4200. The next boot cannot bind and exits 3. The launcher kills the child only from managed hooks (Ctrl+C, process exit, the TUI stop path), which a hard kill skips.

- Stop with the TUI stop or Ctrl+C, or
- end the engine process too, then boot.

## Creature skeleton package

`Main/_Module/DsAssetPackages/taom_creature_skeletons.tpac` (40,259 bytes, 6 skeletons: `chariot_skeleton`, `dwarf_skeleton_a`, `elephant_skeleton`, `skeleton_warg`, `spider_skeleton`, `troll_skeleton_a`). The server engine loads `<module>/DsAssetPackages/*.tpac` for every loaded module; the game client never reads that folder. The server stages the Armory's ModuleData but not its art, so without the package every Monsters bone lookup asserts. With it, Monsters objects loaded in 11.32 ms instead of 479,705 ms and the boot logged 0 bone warnings; a control boot without it reproduced the warnings.

| Task | Command |
|---|---|
| Plan only | `python tools/build_ds_skeleton_pack.py` |
| Rebuild | `python tools/build_ds_skeleton_pack.py --apply` |
| Drift gate | `python tools/build_ds_skeleton_pack.py --check` |

`--check` exits 0 when the committed file matches, 1 when it differs or is absent (an Armory art drop changed a skeleton), and 2 when an input is unreadable or a skeleton is ambiguous or missing. Inside `audit_armory_refs.py` a difference is a warning and never exit 1; `/armory-audit` runs it after an Armory sync. `/release` does not run it: run it before packaging ([release-process](../reference/release-process.md)).

It is not the deprecated `tpac_skeleton_extract.py`. That tool reused an item GUID as the package GUID and wrote TOC size 0, and the copy loaded beside the bundled one crashed the client (2026-06-14). The package here carries the cooked-package GUID every cooked tpac shares and the real TOC size, and sits where only the server looks. Never put a skeleton-only package in a path the client loads.

## Saves

**Trap:** a save made before the installed TAOM_Map added settlements (Cardolan and Rhudaur, 2026-10-23) makes the server load loop forever. `SandBoxNavigationCache` looks each cached settlement up with `Settlement.Find`, which returns null for a settlement the save lacks, and the cache read throws a NullReferenceException. TAOM's crash handler on `Module.OnApplicationTick` catches it, the loading state calls `CreateMapScene` again on every tick, and each retry fails with an `IOException` because the first read left the cache file open. The console repeats `CreateMapScene -> DedicatedServerMapScene` and `reading Main_map...` and the engine's memory grows (7 GB after about 580 retries). Fix: start a new campaign with the installed release and boot on that save. A healthy boot logs `settlement distance cache registered` once. Evidence: `E:\CoopDS\boot-play-oldsave.out`. Ered Luin's 31 settlements (2026-10-27) are expected to repeat it for a save made before them once the distance cache is rebuilt with their rows; not reproduced ([ered-luin.md](ered-luin.md) "Save Compatibility", #1041).

## Known log noise

All are pre-existing and unfixed (2026-10-23).

- 75 to 90 "Voice def can not be found" message boxes per TAOM boot (dwarf and uruk voices): a staging gap found by the spike.
- A message box per missing hill troll animation (`anim_hill_troll_combat_walk1`, `anim_hill_troll_walk_*_staff` and more), about one every 0.3 s while trolls load: the server stages no Armory animation clips, only the skeletons.
- Hundreds of `Failed to get id for object of type "TaleWorlds.CampaignSystem.Clan"` per co-op boot.
- Three `[SaveLoad] ... UnknownSaveId` container warnings when the server loads a save made on a client: the server build has no definition for three saved types. Not yet traced; data in those containers may not survive a server save.

## Damaged saves and the co-op load repair

From the BannerlordCoop repo, not this one. `b18e9a7a2` adds `MapEventLoadCleaner`: at CampaignReady, before ServerRunningState, it finalizes or repairs half-finished player map events. Each stage's throw is logged as an Error, and parties that left the campaign are unregistered from the co-op object manager. Before it, one throw exited the server (exit 3).

- **Checked** (`5994a8322`): on a copy of a damaged save, 7 half-finished events were released and 7 parties unregistered; the server reached LOADED, SERVING and an autosave.
- **Checked once time runs** (2026-10-23, TAOM server, one client, 11 in-game days): no `set_MapEventSide` NullReferenceException in the server console. That save was a new campaign, not a damaged one; a damaged save with a client joined is still unchecked.
- **Deserters** (`66b2f7bbb`): v1.5 deserter parties, created with a null `PartyTemplate`, no longer log a server error per spawn.

## Open co-op follow-ups

Recorded in `doc/porting/bannerlord-1.5.x.md` in the BannerlordCoop repo.

- An unlisted live event is finalized without restoring unsaved state.
- The player pass `FinalizeEvent` publishes no `MapEventFinalized`.
- The E2E harness client looter sits at (0,0).
- A bandit component with null init args throws in the send handler.

## Distribution

BannerlordCoop is source-available since 2026-06-17: its `LICENSE` forbids copying, distributing, or using it in another project without written permission from its maintainers. The dedicated server is proprietary software of Bannerlord Coop LLC. TAOM therefore ships neither, and a guide must not tell players to take a build from our checkouts. The Workshop Coop item stays on Bannerlord 1.4.8.

The plan Garrett proposed (2026-10-23): BannerlordCoop publishes a Coop 1.5 build on Steam, and a TAOM compatibility module (`TAOM.CoopCompat`) depends on both Coop and TAOM; the server runs the same modules. Points to settle with him: written permission for the compat module to reference Coop, and the 1.5 item's module id and advance notice of Coop updates (co-op's join check compares exact versions). He asked for the staging work as PRs:

| Change | Where | State |
|---|---|---|
| An extra module without a `-KeepSubModules` entry keeps the submodules the engine loads (a `DedicatedServerType` of `none`, `matchmaker` or `community` drops one; `IsNoRenderModeElement` never does) | [server PR #104](https://github.com/Bannerlord-Coop-Team/BannerlordCoop.DedicatedServer/pull/104) | Open, review requested (2026-10-23) |
| The server map is built from the last map module's own `SceneObj\Main_map` with `strip_main_map.py`, cached and rebuilt when `navmesh.bin` or `scene.xscene` changes | [server PR #105](https://github.com/Bannerlord-Coop-Team/BannerlordCoop.DedicatedServer/pull/105) | Open, review requested; one real staging run built a byte-identical navmesh and a second reused the cache |
| The core `TAOM` submodule carries no client-only tag (B9) | TAOM `316072f40` | On trunk |

With all three, TAOM stages with only `-Modules` and `-ExtraModules`, and TAOM_Map needs no shipped server map. Two questions are open on #104: whether the release launcher should stage mod modules (today only `run-windows.ps1` does), and whether the README's Host-button sentence is stale (the Host button starts `Bannerlord.exe` on both Coop branches). The community install page draft waits for the distribution answer.

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/DsAssetPackages/taom_creature_skeletons.tpac` | The skeleton package |
| `tools/build_ds_skeleton_pack.py` | Builds and checks it |
| `tools/audit_armory_refs.py` | Warns on drift after an Armory sync |
| `Main/TAOM.csproj` | `MirrorWin64ShippingClientToServer` |

## Tests and gates

- `TAOM.Tests/Infrastructure/ServerLoadGateTests.cs` reads a staged server root from `TAOM_DS_STAGE` (default `E:\repos\BannerlordCoop.DedicatedServer\.stage-win\engine`); inconclusive when absent.
- `python tools/build_ds_skeleton_pack.py --check`
- `python tools/audit_armory_refs.py`

## Changelog

- 2026-10-23: page created. Skeleton package (`61276c182`, #972) and the audit check (`12afef4d2`) landed; the TAOM boot to SERVING is recorded for the spike build only.
- 2026-10-23: the committed build reached SERVING and one client played (map, town, battle). Staging steps, the old-save loop, the troll animation and `UnknownSaveId` noise, and the distribution plan recorded.

## GitHub Issue

- **Issue:** #964 epic, #968 ADR-013, #972 skeleton package (closed)
- **Status:** Open (two clients on two machines, a boot staged with server PRs #104 and #105, and the distribution agreement owed)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/orientation.md](../ai-includes/orientation.md)
- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/module-taom.md](../modding/module-taom.md)
- [docs/reference/bannerlord-animation-system-map.md](../reference/bannerlord-animation-system-map.md)
- [docs/reference/bannerlord-engine-and-toolchain.md](../reference/bannerlord-engine-and-toolchain.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/research/taom-headless-server.md](../research/taom-headless-server.md)

<!-- backlinks-end -->
