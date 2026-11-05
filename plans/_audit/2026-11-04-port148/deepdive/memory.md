# Deep dive: memory footprint on Bannerlord v1.4.8 (run 2026-11-04-port148)

Lens: memory footprint on v1.4.8. Players report 9.4 to 10.8 GB private bytes in battle; the maintainer sees about
15.7 GB. Investigated the 1.4.x line (working tree = c5b84fb41; drifted files read from origin/bannerlord-1.4.5) with
the v1.4.8 decompile dump and taom-src, and the 1.5.x line (origin/bannerlord-1.5.x) as a source of leads and fixes.

Status: complete (2026-11-04). Findings MEMORY-01 to MEMORY-10, then answers to the six questions, a ranking and a
summary.

## Evidence baseline

What the player logs show (E3, E4: the Harad player, 16,321 MB RAM, commit limit 31,681 MB, E3:20; machine name in
E3:32).
`privMB` is process private commit, `heapMB` is `GC.GetTotalMemory(false)`, both from TAOM's `[MemStation]` and
`[MemSample]` lines.

| Stage | E3 (20:00 session) | E4 (20:16 session) | Notes |
|---|---|---|---|
| Main menu, video screen | privMB 6,133, heap 78 (E3:19) | 6,040, heap 78 (E4:18) | managed heap is under 0.1 GB: the floor is native |
| Save read done (`LoadDataOk`) | 6,454 (E3:28) | 6,483 (E4:27) | |
| `GameInitializationFinished` | 6,699 (E3:93) | 6,778 (E4:91) | |
| Loading screen exit | 9,016 (E3:153) | 8,845 (E4:152) | +2.07 to +2.32 GB in the map-screen build (the campaign map scene read) |
| Map, first view settled | 10,006 to 10,015 (E3:157-158) | 10,002 at the first encounter (E4:158) | about +1.0 to +1.1 GB of first-view streaming |
| Battle, `FinishMissionLoadingBegin` | 9,141 (E3:185) | 9,922 (E4:2829) | the engine releases the map scene on mission entry |
| Battle, steady | 9,590 to 9,681 (E3:220-223) | 10,469 to 10,692 (E4:2980-2987) | |
| Town mission, 533 agents | | 10,742 to 10,826 (E4:5137-5164) | |
| Inventory screen, one open | | heap 489 to 2,841 to 3,027, priv 13,965, availPhys 608 MB, memLoad 96% (E4:2755-2782) | MEMORY-01 |

E1/E2 are a different player (different machine name and time zone, RTX 3070: `rgl_log_22112.txt:2-3`, `:68`); their
engine boot line reads
"Mount and Blade II Bannerlord Memory Usage: 4982.980 Mb" (`rgl_log_22112.txt:3830`) and E2 logs "Dump integrity is
compromised due to cheat usage" (`rgl_log_22112.txt:4707` and later), so that player runs with cheats.

What the 1.4.x line already measured on v1.4.8 (maintainer's desktop, 63 GB, 2026-09-12, cheat mode on), all in
`docs/investigations/native-commit-audit-2026-08.md` (working tree):

- Menu floor 6.6 GB, attributed by launcher subtraction (`:685-707`): engine plus vanilla plus TAOM.Dependencies about
  4.4 GB, LOTRLOME_Armory about 0.97 GB, TAOM main about 0.7 GB, TAOM_Map about 0.5 GB. TAOM's three modules add about
  2.2 GB at the menu; the other two thirds is the game itself.
- Map scene about 4.4 GB, released on every mission entry and rebuilt on return (`:428`, `:474-480`).
- Composition is private, not mapped: 85% Private Data, 291 MB mapped (`:443-446`).
- Texture quality is the largest lever: on the settled map, texture quality 2 to 0 freed 2,318 MB, foliage 4 to 0
  freed 769 MB, terrain 2 to 0 freed 199 MB, all on a live switch with no restart (`:649-666`).
- A VMMap region of (texture bytes + 43,664 B) is one texture's full mip chain held in system memory; this was seen for
  the 16K vista and about two dozen 4K textures (`:535-543`).

The maintainer's "about 15.7 GB" fits that same record: menu 6.6 + save load 2.7 + map first settle 3.5 + party
first open 1.0 (cheat-inflated) + drift, with the cheat-mode inventory's 2.9 GB transient on top (`:463-465`). The
players' lower readings (10 to 11 GB) are consistent with no cheat mode in E3's battle, lower graphics settings or the
2026-09-13 map texture downsizing; which of these applies is UNVERIFIED (no engine_config.txt from the player).

Machine caveat for every offline number below: this laptop's installed TAOM_Map is v2.0.27, packed 2026-09-07
(`Modules/TAOM_Map/SubModule.xml`), and its Main_map scene references exactly 2,511,558,440 B of TAOM_Map textures,
the pre-downsizing figure the audit recorded on 2026-09-12 (`native-commit-audit-2026-08.md:524`; re-run this session
with `tools/audit_map_scene_memory.py`'s `build_manifest`, stdout only). Its Armory (v2.0.26, packed 2026-09-07) holds
2,573 textures, 8.24 GiB of full mip chains, 2,061 of them at 2048 px. Players are on newer packs: the public and
patreon channels are v2.0.29.5 on the 1.4.8 line (TAOM.dll stamp `8df90444`,
`origin/bannerlord-1.5.x:docs/reference/release-process.md:245-247`), and the 1.5.x audit read them on 2026-10-02 as
"patreon/public: TAOM_Map 9.8G dated 2026-09-14; Armory dated 2026-09-29"
(`origin/bannerlord-1.5.x:plans/_audit/2026-10-02-perf/evidence/audit/E5_asset_levers.json:89`), with patreon's
Main_map textures at 1.04 GB against 2.34 GB before (`REPORT.md:163`). So players already have the 2026-09-13
downsizing of the Armory (diffuse and normal to 1K, specular and height to 512) and of TAOM_Map (1.4.x
`CHANGELOG.md:432-493`); laptop-derived Armory and map byte counts overstate what players load. TAOM's own
`pack0.tpac` is the same 188,902,921 B on this laptop and in the testing channel, and is built from the repo's
descriptors, identical on HEAD and origin/bannerlord-1.4.5, so the TAOM-module findings hold for players.

## Findings

### MEMORY-01 (P1): with cheat mode on, opening the inventory allocates about 2.4 GB of managed heap on a 16 GB machine

- **Evidence.** E4:2755 `[MemStation] enter screen='GauntletInventoryScreen' privMB=10863 heapMB=489`; E4:2756
  `privMB=13398 heapMB=2841 availPhysMB=804 memLoad=95%`; E4:2780 `privMB=13965 heapMB=3027 availPhysMB=608
  memLoad=96%`; E4:2782 exit at `heapMB=2783`. GC counters across the open: `gc=6671/1411/108` (E4:2728) to
  `gc=8963/1983/124` (E4:2816), that is 16 gen2 collections in about two and a half minutes. The heap is back to 482 MB only after
  the next save (E4:2816). The seven other inventory opens in E4 moved the heap by -15 to +92 MB (E4:257/282,
  354/380, 2699/2723, 2729/2753, 3222/3249, 3341/3366, 3391/3418).
- **Engine mechanism (v1.4.8, read this session).** `Helpers.InventoryScreenHelper.OpenInventoryPresentation`
  (`C:\Decompiled_Bannerlord\_categories_v1.4.8\Campaign\TaleWorlds.CampaignSystem\Helpers\InventoryScreenHelper.cs:174-196`)
  adds every `ItemObject` in the game ten times to the left roster when `Game.Current.CheatMode` is set (`:177-189`).
  Only `OpenScreenAsInventory` (`:271-274`, the player's own inventory) reaches it; loot, trade, stash and warehouse
  opens do not, which is why one open of eight spiked. `PartyScreenHelper.cs:118`, `:180`, `:187` do the same for troops
  on the party screen. TAOM's content makes the list 5,166 rows (E3:109 `ItemPoolAdapter cached 5166 ItemObjects`),
  against about 1,270 for vanilla's own items (1.5.x count, `inventory-open-alloc.md` summary item 2).
- **TAOM trigger.** TAOM itself pushes players toward cheat mode: every `taom.*` console command refuses to run without
  it (HEAD `Main/Features/DevConsole/DevConsoleGuard.cs:59-70`, `TaomConsole.cs:41`), and the rescue commands named in
  CLAUDE.md (`taom.rescue_time`, `taom.time_status`, HEAD `Main/Features/DevConsole/Cheats/TimeControlCheats.cs:14-15`)
  are the advice for a frozen game. A player who enables cheat mode to
  run one keeps paying this spike at every inventory open.
- **Why it matters on a 16 GB machine.** The spike took the player to 608 MB of free physical memory and 96% load
  (E4:2780); the 1.5.x reading of the same path saw the main thread silent for 11 to 14 s per open on a 63 GB machine
  (`origin/bannerlord-1.5.x:plans/_audit/2026-10-02-perf/evidence/memory/campaign-memory-reread.md:61-70`). The memory
  stays rooted until the next game-state change (1.4.x record: `native-commit-audit-2026-08.md:600-609`).
- **Confidence.** Likely, not proven, that E4's player had cheat mode on: the heap rise (+2,352 to +2,538 MB) sits in
  the 2.44 to 2.56 GB range the 1.5.x audit measured for exactly this list, and only the one open spiked. Verify with the
  player's `engine_config.txt` (`cheat_mode = 1`) or the P1 `[InvOpen]` line proposed in
  `origin/bannerlord-1.5.x:plans/_audit/2026-10-02-perf/evidence/memory/inventory-open-alloc.md:68-71`.
- **Fix direction.** Code, small: a TAOM prefix on `InventoryScreenHelper.OpenInventoryPresentation` that builds the
  same `InventoryLogic` without the every-item roster unless an MCM switch asks for it (default off), or that caps it to
  the current culture's items. Player-side: turn `cheat_mode` off when not using a console command. Document the cost in
  the console-command docs.
- **1.5.x.** None in code. Documented only: REPORT M3 and FOR-MIKE item 12 in
  `origin/bannerlord-1.5.x:plans/_audit/2026-10-02-perf/` (af990ca76).
- **Cross-check on the party screen.** `PartyScreenHelper.OpenScreenAsNormal` (`PartyScreenHelper.cs:186-196`) also
  widens under cheat mode, yet E4's four party opens moved the heap by 27 to 48 MB. Three open 2 to 8 s after an
  `EncounterStart` (E4:158/159, 2561/2563, 3290/3291) and one at a battle mission's exit (E4:3000/3003), so they are
  post-battle prisoner screens, not the P key, and the inventory opens right after them are loot screens. That keeps
  the reading consistent: the spiking open (E4:2755) is the only inventory open not directly preceded by an encounter or
  a party screen (it follows a loot screen's close by 8 s, E4:2753).

### MEMORY-02 (P1): the 33 banner icon atlases ship as uncompressed 4096x4096 RGBA8, 64 MiB each; a campaign names 19

- **Evidence (data, 1.4.x).** Decoded this session from the repo's texture descriptors with
  `tools/audit_map_scene_memory.py`'s `parse_texture_meta`: all 33 `Main/_Module/Assets/BannerIcons/*_tex.tpac` on HEAD
  and on origin/bannerlord-1.4.5 read `R8G8B8A8_UNORM, 4096x4096, 1 mip` (64 MiB each, 2,112 MiB for all). The installed
  `Modules/TAOM/AssetPackages/pack0.tpac` on this laptop carries the same 33 at 64.0 MiB each (pixel segments read from
  its table of contents). Vanilla's own banner atlases are 2048x2048 BC7, 4 MiB each
  (`origin/bannerlord-1.5.x:plans/_audit/2026-10-02-perf/evidence/memory/banner-atlases.md:16-18`, read on v1.5.3; not
  re-read on v1.4.8).
- **Which atlases a campaign draws.** Re-ran 1.5.x's `banner_atlas_use.py` (from git, on stdin, read-only) against the
  1.4.x working tree plus the installed TAOM_Map ModuleData: 263 banner keys (`characters/clans.xml` 145,
  `spclans.xslt` 72, `taom_spcultures.xml` 24, `taom_spkingdoms.xml` 14, `spkingdoms.xslt` 8) name **19 of the 33**
  atlases: 1,216 MiB at today's format. The other 14 serve only runtime-built or editor banners. None of these files is
  among the 52 drifted from origin/bannerlord-1.4.5.
- **Engine mechanism.** A banner is drawn by rendering its icon meshes into a 512x512 render target (1024x1024 for a
  large nine-grid banner) through `ThumbnailCacheManager`
  (`C:\Decompiled_Bannerlord\_modules_build\Native__TaleWorlds.MountAndBlade.View.cs:5940-5980`); each icon mesh's
  material samples its atlas, so the first banner that uses an atlas loads all 64 MiB of it.
- **Residency, partly unverified.** 1.4.x's VMMap shows TAOM module textures held as full chains in private memory
  (`native-commit-audit-2026-08.md:535-543`), but its map-baseline histogram lists only "2 x exactly 64.00 MB" regions
  (`:493-495`), not the 19 a fully private-resident atlas set would make. So part of this cost may sit in VRAM (the E1/E2
  player's RTX 3070 has 8 GB) rather than in private bytes. Either way it is about 0.9 to 1.06 GB of texture memory that
  vanilla-sized data would not use. Verify: one campaign load with the DXT atlases against today's, comparing
  `[MemStation] enter screen='MapScreen'` plus 60 s, and dedicated GPU memory in Task Manager.
- **Fix direction.** Data only, a Kit job: re-import the 33 sources with Do Not Compress cleared and Do Not Generate
  Mips kept (24 to DXT1 at 8 MiB, the 9 with alpha to DXT5 at 16 MiB: 2,112 to 336 MiB; the 19 used: 1,216 to roughly
  200 MiB), copy the saved `_tex.tpac` files into the repo before any build (the build otherwise reverts them over the
  rebuilt cache and the sigils render blank), and republish `pack0.tpac`. The 1.5.x descriptors are a byte-for-byte
  guide: HEAD's 33 descriptors equal 1.5.x's at `8cdb11408^`, and after `ab338de2d` exactly those 33 differ; the
  sources and `banner_icons.xml` are identical on both lines. Re-importing in the v1.4.8 Kit is safer than copying
  1.5.x-Kit descriptors (tpac compatibility across Kit versions is UNVERIFIED).
- **1.5.x.** `8cdb11408` (Abanissa sheet, DXT1) and `ab338de2d` (the other 32): port both, data plus Kit. Do not port
  `9985f2db3` as a unit: its sheet packing (34 to 24 textures, saving little once compressed) is bundled with new realm
  banners and palette (content, out of scope), and its `tools/pack_banner_sheets.py --apply` is documented as unsafe to
  run twice. Its deletion of the 39 dead `ui_taom_bannericons_3..41` atlases is install weight only (MEMORY-10).
- **Not measured on either line.** 1.5.x shipped the change with a computed saving, no in-game before and after.

### MEMORY-03 (P2): four AlwaysLoad UI sprite sheets are uncompressed RGBA8 and pinned all session (298.7 MiB)

- **Evidence (data, both lines).** `Main/_Module/GUI/SpriteParts/Config.xml` (HEAD) marks all five TAOM categories
  `<AlwaysLoad/>`. Decoded descriptors (HEAD = origin/bannerlord-1.4.5 = origin/bannerlord-1.5.x for these files):
  `ui_loading_1` and `ui_loading_2` are `R8G8B8A8_UNORM 4096x4096, 13 mips` (85.3 MiB each), `ui_taom_bannericons_1`
  and `_2` are `R8G8B8A8_UNORM 4096x4096, 1 mip` (64 MiB each); `ui_taom_1..5` and `ui_taom_career_system_1` are already
  DXT5 (21.3 MiB each) and the three font sheets BC4. The installed pack0 matches. The 1.4.8 VMMap shows "4 x 85.38 MB"
  regions (`native-commit-audit-2026-08.md:493`), and 85.38 MB is exactly a 4096x4096 RGBA8 13-mip chain plus the
  43,664 B per-texture overhead the same audit identified.
- **Mechanism.** AlwaysLoad categories load at UI start and never unload. `ui_loading` must keep AlwaysLoad: no engine
  code loads it by name, and an unloaded category renders a blank loading screen with no error
  (`native-commit-audit-2026-08.md:755-773`).
- **Fix direction.** Data only, a Kit job: re-save the four sheets as BC7 (16 MiB per 4096x4096 mip level): about 224 MiB
  saved (1.5.x REPORT M6 computes the same). Check the loading art and banner icons by eye afterwards.
- **Residency caveat.** The 1.4.x ladder rung that removed AlwaysLoad from three TAOM categories (about 256 MiB of
  sheets) moved the menu by less than the run-to-run spread (`native-commit-audit-2026-08.md:688`), so the private-bytes
  share of sprite sheets is UNVERIFIED; the VRAM saving is not in doubt.
- **1.5.x.** None: the four descriptors are still RGBA8 at the 1.5.x tip. Only documented (REPORT M6, FOR-MIKE item 13,
  af990ca76).

### MEMORY-04 (P1, player-side, no code): texture quality is the largest lever a player has today, and nothing tells them

- **Evidence (v1.4.8, 1.4.x record).** On the settled campaign map, live switches with no restart:
  texture quality 2 to 0 freed 2,318 MB, foliage 4 to 0 freed 769 MB, terrain 2 to 0 freed 199 MB; all three to 0 took
  the process from 12,810 to 9,734 MB, and VMMap Private Data fell 3.09 GB (`native-commit-audit-2026-08.md:638-666`).
  Measured before the 2026-09-13 map texture downsizing, so on a downsized TAOM_Map the texture-quality share is smaller
  (1.5.x FOR-MIKE item 5 says the map's TAOM textures went from 2.34 to about 1.1 GB). Players are on the downsized map
  (baseline caveat), so the slider saves them less than 2.3 GB; how much less is unmeasured, and FOR-MIKE item 5 asks
  for that re-measure before any advisor ships.
- **Engine options that exist on v1.4.8.** `NativeOptions.NativeOptionsType`
  (`C:\Decompiled_Bannerlord\_categories_v1.4.8\Engine\TaleWorlds.Engine\TaleWorlds.Engine.Options\NativeOptions.cs`)
  has TextureBudget, TextureQuality, FoliageQuality, TerrainQuality, ShadowmapResolution, CharacterDetail,
  ParticleDetail, ParticleQuality, WaterQuality, DecalQuality, ScreenResolution, ResolutionScale and NumberOfRagDolls.
  Only TextureQuality, FoliageQuality and TerrainQuality have TAOM measurements; the rest are UNVERIFIED for memory.
- **Mechanism.** The engine keeps lower mips resident at a lower texture quality and frees the top mips on a live
  switch (the 16K vista region, two 85 MB and two 64 MB regions and about 35 regions of about 32 MB vanished in the
  1.4.8 VMMap, `native-commit-audit-2026-08.md:641-644`).
- **What a player can do now.** On 16 GB: texture quality one step down (largest single saving), foliage one step down,
  and restart the game rather than loading a second save inside one process (the same record: returning to the menu
  kept 3.7 GB, and a second campaign in one process settled 1.8 GB higher, `:611-615`). Keep cheat mode off unless a
  console command is needed (MEMORY-01).
- **Fix direction.** Code, small: an opt-in, shown-once advisor that reads total physical memory and the texture
  quality option and offers one step down on machines at or under 16 GB, re-measured first on the shipped TAOM_Map.
  Documentation: a "16 GB machines" section in the player docs and the release notes.
- **1.5.x.** No texture advisor exists. 1.5.x has `IGraphicsOptionsAdapter.TextureQualityOption`
  (`origin/bannerlord-1.5.x:Main/Adapters/GraphicsOptionsAdapter.cs:34`), read only by the perf context line
  (`Main/Features/MissionPerf/Hooks/PerfContextReader.cs:60`), and a ragdoll and corpse advisor
  (`Main/Features/BattleCorpses/BattleSettingsAdvisor.cs`, `c17541f2c`, #701) that is the natural host for one. The
  advisor was proposed, not built (FOR-MIKE item 5 in `plans/_audit/2026-10-02-perf/`, af990ca76).

### MEMORY-05 (P2): a second game in one process keeps most of the first; the managed heap often stays 4 to 6 times its cold size

- **Evidence (v1.4.8, 1.4.x record).** Reloading an autosave from inside a running campaign settled the new map 1.8 GB
  above the first (14,720 against about 12,900 MB), and returning to the main menu left the process at 10,273 MB
  against the 6.6 GB cold floor (`native-commit-audit-2026-08.md:589-598`, `:611-615`). At that return the menu read
  `heapMB` 523 on enter and 473 on exit, against 74 to 77 MB on a cold menu (the GauntletInitialScreen rows of
  `origin/bannerlord-1.5.x:plans/_audit/2026-10-02-perf/evidence/memory/campaign-memory-reread.md:107-111`, the return
  at `:110`); on v1.5.3, seven later re-entries read 279 to 393 MB (`:114-142`) and two read about 100 MB (`:144`,
  `:149`), so the retention is common but not universal. When it happens, 200 to 450 MB of managed objects stay
  reachable after the campaign that owned them has ended.
- **Mechanism (partly known).** Native: the engine keeps freed memory in its own pools ("what the engine frees stays in
  its allocator pools", `native-commit-audit-2026-08.md:423`). Managed: something static still references the old
  campaign's objects. Candidates on TAOM's side are singleton services that cache game objects across `OnGameEnd`; 1.5.x
  found and fixed three such leaks of session state, as correctness bugs: Enlistment's commander party and army handles
  and latches (`e99d705a3`, `a67792c40`, plan 014) and Special Resources' balances (`4263535a1`). Who roots the rest is
  UNVERIFIED.
- **Player impact.** A player who reloads after a lost battle or a hang (E3's stale inflight marker, E3:17, shows this
  player restarting after a hang) starts the next campaign gigabytes higher on a 16 GB machine; restarting the game is
  the workaround.
- **Fix direction.** Measure first: one PerfView heap snapshot at the main menu after a campaign, rooted-object view,
  to name the static holders. Then code: clear TAOM-held game references on `OnGameEnd`, the pattern of the 1.5.x fixes.
  Port `e99d705a3`, `a67792c40` and `4263535a1` (bug fixes to features on both lines) as part of it.
- **1.5.x.** `e99d705a3`, `a67792c40`, `4263535a1` (correctness, partial). No systematic retention work.

### MEMORY-06 (P2): the campaign map's 16K vista texture costs 341 MB that vanilla's map does not spend

- **Evidence.** TAOM_Map's `SceneObj/Main_map/scene.xscene` sets `vista_diffuse_name="16K_Vista_02"`; vanilla's
  `SandBox/SceneObj/Main_map/scene.xscene` on this v1.4.8 install sets `vista_diffuse_name=""` (both read this
  session). `16K_Vista_02` decodes as DXT5 16384x16384, 15 mips, 341.3 MiB (pack0 table of contents, this session; the
  audit's 357,913,968 B, `native-commit-audit-2026-08.md:530`). The 1.4.8 VMMap shows it as one private region of
  357,957,632 B (`:535-538`), so it is held in system memory, and it is the first region texture quality drops
  (`:641-644`). The public channel still carries it: the 1.5.x audit calls it "a third of what remains" after the
  downsizing (`origin/bannerlord-1.5.x:plans/_audit/2026-10-02-perf/FOR-MIKE.md:151-155`).
- **Fix direction.** Data, TAOM_Map (unversioned), maintainer's art call: 8K saves about 256 MB, 4K about 320 MB, or
  drop the vista as vanilla does. Land an in-repo gate beside it (the dependency-module trap in CLAUDE.md).
- **1.5.x.** None (proposed in FOR-MIKE item 6 only).

### MEMORY-07 (P3, VRAM): every screen opened over the campaign map may strand the map view's render targets until the next mission

- **Engine mechanism, verified on v1.4.8.** The map builds its layer as `new SceneLayer(true, false)`
  (`C:\Decompiled_Bannerlord\_modules_build\SandBox__SandBox.View.cs:14075`), and `SceneLayer.OnDeactivate` disables the
  view only when `AutoToggleSceneView` is set (`...\TaleWorlds.Engine.Screens\SceneLayer.cs:17-44`), so a covered map
  keeps its view. `MapScreen.ClearGPUMemory` (`SandBox__SandBox.View.cs:13931-13942`) is called only from
  `MissionCampaignView.OnMissionScreenPreLoad` when `Mission.NeedsMemoryCleanup` (`:2273-2277`).
  `ScreenLayer.OnLayerActiveStateChanged` exists (`TaleWorlds.ScreenSystem\ScreenLayer.cs:39`, raised at `:116`, `:127`)
  and so does `SceneLayer.ClearRuntimeGPUMemory` (`SceneLayer.cs:145-148`), the two seams 1.5.x uses.
- **The leak itself is UNVERIFIED on 1.4.8.** It was measured by yotthani on 1.5.3 to 1.5.5 (about 40 MB of graphics
  memory per cover at 1920x1080, about 94 MB at 5120x2160, vanilla too), not by TAOM
  (`origin/bannerlord-1.5.x:docs/features/map-view-release.md`). It is dedicated GPU memory, not private bytes, so it
  matters on 8 GB cards. Verify: Task Manager dedicated GPU memory after 20 to 30 menu returns on the map.
- **Fix direction.** Code, port with a re-read of the seams above: subscribe to `OnLayerActiveStateChanged`, count
  covers of the map layer, release on every Nth return. No Harmony patch.
- **1.5.x.** `ecaa67d26` (feature folder `MapViewRelease`, new on 1.5.x, infrastructure). It depends on 1.5.x's
  `Main/Composition` module runner (`MapViewReleaseModule : TaomFeatureModule`,
  `origin/bannerlord-1.5.x:Main/Features/MapViewRelease/MapViewReleaseModule.cs`), so the port needs the IoC wiring
  redone for 1.4.x.

### MEMORY-08 (P3): TAOM_Map's 141 prefab files are parsed at boot, and about 17% of their entities are named nowhere

- **Evidence.** The engine reads all 141 `TAOM_Map/Prefabs/*.xml` during boot (`rgl_log_22112.txt:3606-3746`, about
  1.8 s) and never `Prefabs_Unused` (0 lines in E2). Those files hold 93,830 `<game_entity>` nodes (grep this session),
  against Native's 23,828 and SandBox's 2,847. The 1.4.8 ladder put TAOM_Map's whole menu-time surface at about 500 MB
  (`native-commit-audit-2026-08.md:690`). An offline string sweep this session (6,311 files: every `scene.xscene` and
  ModuleData XML/XSLT of the eight loaded modules, all Prefabs XML, and `Main/**/*.cs` plus `Main/_Module/**/*.xml`)
  found 252 of 1,364 top-level prefabs, holding 15,659 entities (17%), named by none of them; the largest are
  `dwarf_armory.xml` (5,272), `Soisson_Prefabs_1.xml` and `_2.xml` (6,863 together), `shogun_dwarf_prefabs.xml` (1,301)
  and `fink_dwarf_prefabs.xml` (904). A string sweep cannot see a name built at runtime, so these are candidates.
- **Mechanism.** Prefab definitions are kept for the process lifetime once parsed; what each costs natively is
  UNVERIFIED, so the saving is unknown and probably tens of MB at most.
- **Fix direction.** Data, TAOM_Map (unversioned): move confirmed-unreferenced prefab files to `Prefabs_Unused`, prove
  it with a load test, and gate it in the repo. The same move lowers the count `tools/check_prefab_budget.py` guards
  (its docstring places the 131,072 cap in the Modding Kit editor's load queue, not the shipping client). Low priority
  for memory.
- **1.5.x.** None.

### MEMORY-09 (tools, port): instruments that would turn the open questions above into numbers

- **Already on 1.4.x.** `[MemSample]`, `[MemStation]`, the `[BattleLoad]`/`[SaveLoad]` memory fields (all visible in
  E3/E4), `taom.print_memory`, `tools/Invoke-CommitMatrix.ps1` and `tools/audit_map_scene_memory.py`.
- **Port from 1.5.x.**
  - `3dac7ab5b` `tools/audit_battle_equipment_memory.py` (plan 038): offline ranking of the meshes, textures and bodies
    a battle's equipment preloads, per troop, culture or side; it reuses the map audit's pack index. Line-agnostic
    tool, also brings `--loose-assets` to `audit_map_scene_memory.py`. Its first result on the testing channel: a Gondor
    against Mordor battle floor of 2.58 GB of pack bytes (commit body; pack bytes, not measured memory).
  - `67eff55cf` `tools/perf_runs.py` (plan 029): per-mission rows from logs with a memory-pressure confounder flag.
- **Port only with care.** `a9f10688f` (`[AnimMem]` clip-memory probe, plan 036) finds the clip byte counter by a
  signature scan of the v1.5.3 binary and turns itself off on a mismatch; on v1.4.8 it is UNVERIFIED whether it arms. It
  also sits on 1.5.x's MissionPerf and Composition. Its subject (on-demand clips held toward a 12 MiB budget, evicted
  past 15 MiB, read natively on v1.5.3 only) is a frame-time question, not a footprint one: tens of MiB at most.
- **Proposed new instrument (none on 1.5.x).** The P1 `[InvOpen]` line from
  `origin/bannerlord-1.5.x:plans/_audit/2026-10-02-perf/evidence/memory/inventory-open-alloc.md:68-71` (cheat mode,
  inventory mode, row counts) would confirm MEMORY-01 from any player log.

### MEMORY-10 (P3, download size only): dead files in every player's install, no runtime memory

- **Banner sprite atlases.** `TAOMSpriteData.xml:30-34` (HEAD) declares 2 sheets for `ui_taom_bannericons` and
  `SpriteCategory` loads sheets 1 to `SpriteSheetCount`
  (`C:\Decompiled_Bannerlord\_categories_v1.4.8\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs:56`),
  yet `Main/_Module/Assets/GauntletUI/` still carries descriptors for sheets 3 to 41 and the installed pack0 holds all
  41 (2,538.7 MiB decompressed, about 39.6 MB of download per the 1.5.x reading). Port the deletion half of
  `9985f2db3` only (descriptors and `AssetSources/GauntletUI/ui_taom_bannericons_3..41.png`), then republish.
- **Module sounds.** HEAD `Main/_Module/ModuleData/module_sounds.xml` registers 416 paths against 436 files: 21 files
  (53.3 MiB) are never registered, and `module_sounds.xml:5` names `LOTR/Elves/Alert/elf_horn.wav` where the file is
  `horn_elf.wav` (checked this session). 1.5.x read the engine on v1.5.3: sounds are opened only when played, so this
  is download size (not re-checked on v1.4.8).
- **TAOM_Map terrain-authoring leftovers.** `GAEA`, `GAEA02` (256 MiB each decompressed, RGBA8 8193x8193),
  `terrain_heightmap`, `terrain_normal`, `Normal Map`, `Final Height` and others sit in this laptop's TAOM_Map packs; the
  1.5.x audit found 112 such textures (338 MiB of download) unreferenced in the patreon and public builds
  (`FOR-MIKE.md:168-178`). Unreferenced textures never load.
- **1.5.x.** `9985f2db3` (the 39 atlases, bundled with content); the sound and map items are documented only
  (FOR-MIKE 13d, item 6).

## Answers to the six questions

**1. Where does the native memory go on 1.4.8?** Managed heap is 78 MB at the menu and 0.45 to 0.6 GB in play (E3/E4),
so about 95% of private bytes is native. In order:

| Layer | Size (source) | What it is |
|---|---|---|
| Menu floor | 6.0 to 6.2 GB for the player (E3:19, E4:18); 6.6 GB on the maintainer's machine | engine plus vanilla plus TAOM.Dependencies about 4.4 GB; Armory about 0.97 GB; TAOM about 0.7 GB; TAOM_Map about 0.5 GB (`native-commit-audit-2026-08.md:695-707`). In TAOM's share: 435 MiB of AlwaysLoad sprite-sheet texture data (MEMORY-03; private share unverified) and 93,830 boot-parsed prefab entities (MEMORY-08). Not in it: pack tables of contents (Armory 16 MiB, TAOM_Map 3.9 MiB, Native 31.5 MiB of metadata, census this session) and module sounds (opened on play, 1.5.x native reading) |
| Campaign load | +2.1 to +2.3 GB between `GameInitializationFinished` and the loading screen's exit (E3:93/153, E4:91/152) | the campaign map scene read plus the map screen build. Render buffers plus mesh edit data of Main_map would need 2,882 MiB, more than the whole step, so edit data is not loaded in play on 1.4.8 either (consistent with 1.5.x's native reading on v1.5.3, REPORT M4) |
| Map first view | +1.0 to +1.1 GB for the player (E3:154/157, E4:153/158); +2.15 GB on the maintainer's machine after the downsizing, +3.5 GB before | texture streaming; texture quality controls most of it (MEMORY-04); the 16K vista is 341 MB of it (MEMORY-06); banner atlases add up to 1.2 GB of texture memory, split between private bytes and VRAM unverified (MEMORY-02) |
| Battle | map released on entry: -1.0 to -1.2 GB (E3:179/185, E4:2820/2829, tournaments E4:382/388, 3421/3427); battle content +0.5 to +0.7 GB (E3:185/220, E4:2829/2981); town of 533 agents +0.33 GB (E4:3427/5137) | battles sit at or below the map's level on this player's machine; per-battle equipment preload is not the peak |
| UI transients | inventory +2.4 GB managed with cheat mode (MEMORY-01) | the largest single spike in the evidence |
| Engine pools | 1,066 x 4 MB chunks (4.3 GB) on the maintainer's map baseline, opaque (`native-commit-audit-2026-08.md:489-498`) | freed memory stays in these pools, which is why a second game in one process starts higher (MEMORY-05) |

Weighed and found small, or not a footprint lever: animation clips (bounded at about 12 to 15 MiB by the engine's
eviction, read on v1.5.3 only, MEMORY-09); prefab entities (the 131,072 cap is the editor's queue, MEMORY-08); shader
caches (unmeasured; `Utilities.ClearShaderMemory()` runs at mission pre-load, `SandBox__SandBox.View.cs:2276`);
portrait and tableau caches (render targets with capacities of 75 to 100, plus a 1,000-entry persistent banner cache
whose entries are referenced at creation and flushed only on return to the initial screen,
`Native__TaleWorlds.MountAndBlade.View.cs:4972-4978`, `:9850-9900`; VRAM, not measured); the MBObjectManager object
graph (managed, about 200 MB at `GameLoaded`, E3:79, E4:77; its cost is save time, not footprint); FactionMap loose
PNGs (at most about 121 MiB decoded, only in a session that ran character creation; the 631.8 MiB `fullres` set is
loaded by nothing); vanilla's `ui_encyclopedia` and `ui_kingdom` pinned on the map by
`MomentumIndicatorMapView.cs:31-32, :50-51` (one 4096x2048 sheet each).

**2. What 1.5.x did to reduce memory, and what ports.**

| Commit | What | Kind | Engine-specific? | Port to 1.4.8 |
|---|---|---|---|---|
| `8cdb11408`, `ab338de2d` | 33 banner atlases to DXT1/DXT5 | data (Kit) | no | yes, MEMORY-02 (re-import in the v1.4.8 Kit, republish pack0) |
| `9985f2db3` | banner sheets packed 34 to 24, 39 dead sprite atlases deleted | data, bundled with new realm banners | no | only the dead-atlas deletion (MEMORY-10); packing and banners are content |
| `579bb93f0`, `3f8168228` | party screen builds each troop portrait code once | code, new ShadowfaxScreens folder | no | optional: allocation churn and open time, not footprint |
| `d5986f87f` | mesh-reference validator update | tool | no | yes as a validator; no memory effect |
| `103636439` | unused `PreviewPanel.xml` removed | data | no | trivial; negligible memory |
| `a9f10688f` | `[AnimMem]` clip probe | code, diagnostics | yes (v1.5.3 signature) | only with care (MEMORY-09) |
| `3dac7ab5b` | battle equipment memory audit tool | tool | no | yes (MEMORY-09) |
| `ecaa67d26` (its MapViewRelease part) | release the map view's render targets every 20th cover | code, new infrastructure folder | seams verified on 1.4.8, leak not | yes after a VRAM check (MEMORY-07) |
| `e99d705a3`, `a67792c40`, `4263535a1` | session state cleared on a new campaign | code, bug fixes | no | yes (MEMORY-05) |
| 2026-09-13 Armory and TAOM_Map downsizing (CHANGELOG entries on both lines, no commit) | textures to 1K/2K | data, unversioned modules | no | already shipped to the public channel (baseline caveat) |
| `efe326242`, `13f34c1cb`, `74e9172d4`, `5ee49d05f`, `87f9f862e` | fewer allocations per frame or per hit | code | no | runtime-perf lens; garbage, not resident memory |

Nothing on 1.5.x addresses the cheat-mode inventory list, the four uncompressed UI sheets, the 16K vista or a texture
advisor; all four are documented in `origin/bannerlord-1.5.x:plans/_audit/2026-10-02-perf/` (af990ca76) only.

**3. TAOM module assets.** TAOM's installed `pack0.tpac` (188,902,921 B) holds 86 textures decompressing to 4.84 GiB,
74 of them uncompressed RGBA8 (4,778.7 MiB): 33 banner atlases (2,112 MiB, MEMORY-02), 41 `ui_taom_bannericons`
sheets of which 2 load (MEMORY-03, MEMORY-10), and `ui_loading_1/_2` (170.7 MiB, MEMORY-03). The rest is DXT5
(8 sheets, 170.7 MiB), BC4 fonts and one BC7 atlas. No TAOM, TAOM_Map or Armory texture carries the low-resolution stub
segment that vanilla's packs use for streaming (survey this session: 0 of 86 in TAOM pack0, 0 of 41 in TAOM_Map pack0,
0 of 45 in Armory pack8; the 1.5.x audit found the same across all packs), and E2 logs 783 "Trying to make partial
read on compressed asset data. This does not improve performance since partial decompression is not supported"
lines. Whether stub-less packs raise TAOM's residency against vanilla is UNVERIFIED (vanilla's EmAssetPackages segments
are compressed too: 153 of 157 sampled).

**4. Does TAOM force anything resident that vanilla would stream?** Yes: five AlwaysLoad sprite categories (13 sheets,
435 MiB of texture data, against vanilla's 162 MB AlwaysLoad surface per `native-commit-audit-2026-08.md:31`), banner
atlases held whole once drawn (one mip, so nothing to stream), and every module texture shipped as a full chain
without a stub. ShaderPrecompilation does not: on the 1.4.x line it is ACTIVE but runs only when the player picks
"Pre-compile Shaders" at the main menu (HEAD `Main/SubModule.cs:637-665`), and E3/E4 contain no shader-walk line. When
run, each 1,000-character batch preloads its characters' equipment in a hidden custom battle; its memory peak on 1.4.8
is unmeasured (`docs/features/shader-precompilation.md:212`: the cold walk has not run on 1.4.8 since #560), and the
walk ends at the main menu in the same process, so restarting afterwards is the safe advice. 1.5.x parked the walk
again on 2026-09-25 (inside `25995dc91`). TAOM calls no engine preload API itself (grep of `Main/`).

**5. Settings players can change.** MEMORY-04. Measured on v1.4.8 for TAOM: texture quality (2.3 GB on the map,
before the map downsizing), foliage (0.77 GB), terrain (0.2 GB), freed live. Also free: cheat mode off (2.4 GB
transient, MEMORY-01) and restarting the game instead of loading a second save in one process (1.8 GB, MEMORY-05).
Unmeasured for TAOM: texture budget, shadow map resolution, character detail, water, decal and particle quality,
resolution scale.

**6. Ranking by GB saved per unit of effort.**

| Rank | Change | Kind | Effort | Saving (who) | Confidence |
|---|---|---|---|---|---|
| 1 | Tell 16 GB players: texture quality one step down, foliage one step down, cheat mode off, restart before loading another save | docs | XS | up to about 2.3 + 0.8 GB on the map, 2.4 GB per cheat inventory open, 1.8 GB per in-process reload | measured on 1.4.8 (smaller on the downsized map) |
| 2 | Cheat-mode inventory guard (MEMORY-01) | code | S | about 2.4 GB transient per I-key open, for cheat-mode players | Likely |
| 3 | Banner atlases to DXT1/DXT5, port `8cdb11408` + `ab338de2d` (MEMORY-02) | data, Kit | S to M | 1,216 to about 200 MiB of texture memory in a typical campaign (up to 1.8 GB if all 33 load) | computed; private share UNVERIFIED |
| 4 | Texture-quality advisor for 16 GB machines (MEMORY-04) | code | S to M | carries rank 1 to players who never read docs | needs a re-measure on the shipped map |
| 5 | 16K vista to 8K or none (MEMORY-06) | data, TAOM_Map | S (plus a 9.8 GB republish) | 256 to 341 MB on the map | measured residency |
| 6 | Four UI sheets to BC7 (MEMORY-03) | data, Kit | S | about 224 MiB all session | computed; private share UNVERIFIED |
| 7 | Clear TAOM-held game references on game end, after a heap snapshot (MEMORY-05) | code | M | 0.2 to 0.45 GB managed per reload, plus part of 1.8 GB | partly UNVERIFIED |
| 8 | MapViewRelease port (MEMORY-07) | code | M | about 40 MB of VRAM per screen cover at 1080p, until the next mission | UNVERIFIED on 1.4.8 |
| 9 | Move unreferenced TAOM_Map prefabs out of `Prefabs` (MEMORY-08) | data, TAOM_Map | M | unknown, probably tens of MB | candidates only |
| 10 | Dead atlases, unregistered sounds, map leftovers (MEMORY-10) | data | S | download size only | measured |

Data changes need a republish of the module pack; TAOM's own `pack0.tpac` covers ranks 3, 6 and 10 together, so do
them in one Kit session.

## Not covered or unresolved

- The private-bytes versus VRAM split for sprite sheets and banner atlases: the 1.4.x ladder and VMMap readings point
  both ways. One campaign load before and after the Kit change settles it.
- About 3.9 GB of map-only private regions on the maintainer's pre-downsizing map that match no packed texture
  (terrain and flora runtime data is the candidate, `native-commit-audit-2026-08.md:557-565`).
- `UIConfig.DoNotUseGeneratedPrefabs = true` (HEAD `Dependencies/SubModule.cs:28`, same on 1.5.x): forces the dynamic
  prefab path for every Gauntlet movie; its memory cost is unmeasured and UIExtenderEx needs it.
- Whether the engine streams a stub-less module texture or keeps it whole at a given texture budget, and whether the
  Kit can publish vanilla's stub-plus-full-chain split (the FOR-MIKE item 6 question).
- The engine's 4 MB pool chunks (4.3 GB) stay opaque to every offline and VMMap method used so far.
- E1/E2 carry no TAOM memory lines (rgl only), so the RTX 3070 player's memory is known only from the boot line.

## Summary

The largest memory events in the player evidence are, in order: the campaign map (10 to 11.6 GB with the map loaded,
of which the first view's texture streaming is about 1 GB at this player's settings), a 2.4 GB managed-heap spike from
one inventory open that matches vanilla's cheat-mode every-item list over TAOM's 5,166 items (MEMORY-01, Likely:
E4:2755-2782, `InventoryScreenHelper.cs:174-189`), and a 6 GB menu floor of which TAOM's three modules are about
2.2 GB. Battles are not the peak: the engine releases the map scene on mission entry, so E3's battle ran at 9.4 to
9.7 GB, and during E3's freeze private bytes and working set stayed flat for the last 90 s of the log (E3:219-223:
9,443 to 9,681 MB private, 6,302 to 6,396 MB working set) with 2.8 GB free and 8 GB of commit headroom, so memory
exhaustion did not cause that freeze. The cheapest large lever is advice:
texture quality one step down freed 2.3 GB on the 1.4.8 map, foliage 0.77 GB, and cheat mode off removes the 2.4 GB
spike. The largest asset lever is porting 1.5.x's banner-atlas compression (`8cdb11408`, `ab338de2d`): the 33 atlases
are uncompressed 4096 px RGBA8 at 64 MiB each and a campaign names 19 of them, about 1.2 GB of texture memory that
DXT1/DXT5 cuts to about 0.2 GB, though how much of it is private bytes rather than VRAM is unverified. Neither line has
touched the four uncompressed UI sheets (224 MiB pinned), the 16K vista (341 MB) or a texture advisor. Players already
have the 2026-09-13 Armory and map texture downsizing (public packs dated 2026-09-14 and 2026-09-29), so this laptop's
older packs overstate their texture payload; TAOM's own pack is the same file for everyone. A second game in one
process keeps 200 to 450 MB of managed heap and settles 1.8 GB higher; 1.5.x fixed three such leaks as correctness
bugs (`e99d705a3`, `a67792c40`, `4263535a1`) and a heap snapshot would name the rest. ShaderPrecompilation is
player-initiated and did not run in the evidence. Cross-lens note for the save-time lens: the one sample taken
mid-save in E4 held 1,367 MB of managed heap (E4:2556, about +0.9 GB over the map's level).

Skills to recommend to the orchestrator: `/research` on `Helpers.InventoryScreenHelper` before writing the MEMORY-01
prefix; `/investigate` with a PerfView heap snapshot for MEMORY-05; the Kit procedure for MEMORY-02 and MEMORY-03 is
in `origin/bannerlord-1.5.x:docs/reference/banner-icon-generation.md` after `ab338de2d`.

