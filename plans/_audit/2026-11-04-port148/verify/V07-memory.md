# V07 verification: MEMORY-01, MEMORY-02, MEMORY-05 and the player-side levers

Run 2026-11-04-port148, verifier V07 (read-only, adversarial). Subject:
`plans/_audit/2026-11-04-port148/deepdive/memory.md` (cited below as memory.md:N).

Refs: working tree = HEAD = c5b84fb41; origin/bannerlord-1.4.5 = 1b00d1acd; origin/bannerlord-1.5.x = e45f6df2c.
Engine: the v1.4.8 dump (`C:\Decompiled_Bannerlord\_categories_v1.4.8`, `_modules_build`), with the two Harmony
targets re-read from the installed DLLs through `tools/taom-src.ps1` (cache `C:\Users\mikew\.taom-src\v1.4.8\`).
Evidence E1 to E4 (run scratchpad `evidence\`), read as text only. Line numbers are grep line numbers. Player
machine and user names in the logs are deliberately not reproduced here.

Nothing was built, tested, installed or modified. Texture headers were decoded with
`tools/audit_map_scene_memory.py`'s `iter_tpac_items` / `parse_texture_meta` in stdin scripts (table of contents and
metadata only, never pixel payloads), on the repo, on git blobs and on the laptop's installed packs.

## Verdicts at a glance

| Claim | Verdict | What is actually true |
|---|---|---|
| MEMORY-01, engine path | CONFIRMED | v1.4.8 `OpenInventoryPresentation` adds every `ItemObject` once, stacked to 10, when `Game.Current.CheatMode`; only the own-inventory opener reaches it |
| MEMORY-01, "E4's player had cheat mode on" | PARTLY-CONFIRMED (Likely, not proven) | E4 carries no cheat-mode line. The spiking open's signature (no encounter before it, +2,352 MB managed in 29 s, a 31 s main-thread gap) matches the measured cheat-mode cost, and no other v1.4.8 path builds a list that size |
| MEMORY-01, "E2's player runs with cheats" (memory.md:31-32) | REFUTED as stated | The rgl line means the campaign's saved, sticky `EnabledCheatsBefore` flag is set: cheats were on at some point, not necessarily in E2. E1/E2 is a different machine from E3/E4 |
| MEMORY-02, formats and sizes | CONFIRMED | 33 x `R8G8B8A8_UNORM` 4096x4096, 1 mip, 64 MiB on both 1.4.x refs and in the installed pack; ab338de2d makes them 24 DXT1 + 9 DXT5 (2,112 to 336 MiB) |
| MEMORY-02, "19 named, 1,216 to roughly 200 MiB" (memory.md:136) | PARTLY-CONFIRMED | 19 of 33 re-measured. All 19 named sheets become DXT1, so 152 MiB, not about 200 |
| MEMORY-02, residency | UNVERIFIED, leans VRAM | The 1.4.8 VMMap lists no 64 MB private region for a banner material atlas |
| MEMORY-02, port path | PARTLY-CONFIRMED | Inputs are byte-identical across the lines; the player deliverable is a republished `pack0.tpac` from a v1.4.8 Kit, which on this laptop first needs the TAOM.Dependencies editor mirror |
| 9985f2db3 dead-file removal | CONFIRMED independent | The 39 `ui_taom_bannericons_3..41` descriptors and PNGs are dead on 1.4.x and not entangled with realm-banner content |
| MEMORY-05, numbers | CONFIRMED | 1.8 GB higher second-game settle, 10,273 MB at the menu return, 279 to 523 MB managed heap at menu returns |
| MEMORY-05, "port e99d705a3, a67792c40, 4263535a1 as part of the fix" | PARTLY-CONFIRMED | Port them as correctness fixes; the 1.5.x record shows the managed retention persisted after all three landed. The lens missed SAVE-TIME-01, the same "survives Game.Destroy" family |
| Levers | PARTLY-CONFIRMED | The texture and foliage savings were measured top-to-minimum (2 levels and 4 levels), not "one step down" (memory.md:420) |

## 1. MEMORY-01: the cheat-mode inventory

### 1.1 What E4 shows about cheat mode: nothing direct

- `grep -i cheat` over E4 finds only E4:2172 and E4:2897: `GauntletCustomBattleMissionCheatView` in two missions'
  behaviour lists. That is CustomBattle's override of `MissionCheatView`, which is a `[DefaultView]` added to every
  mission (`_modules_build\Native__TaleWorlds.MountAndBlade.View.cs:21211-21219`); the override's
  `GetIsCheatsAvailable()` returns false (`_modules_build\CustomBattle__TaleWorlds.MountAndBlade.CustomBattle.cs:4403-4417`).
  It says nothing about cheat mode.
- E4:17 `[DevConsole] Console discovery PROVEN` is written at every boot. TAOM logs a console command only when it
  throws (HEAD `Main/Features/DevConsole/TaomConsole.cs:66-89`), so the absence of command lines proves nothing.
- E4's session snapshot (E4:30-55) logs modules and assemblies, not engine config: eight modules, all vanilla or TAOM,
  no third-party mod (E4:32-40).
- The "Dump integrity is compromised due to cheat usage" lines are in E1 (E1:4704) and E2 (E2:4707; 81 in E2), not in
  E3/E4. A programmatic comparison this session found the E1/E2 rgl machine name absent from E3/E4 (E3:32, E4:31), so
  this is the other player. The line is printed when `Campaign.Current.EnabledCheatsBefore` is true
  (`_categories_v1.4.8\Modules\SandBox\SandBox.CampaignBehaviors\DumpIntegrityCampaignBehavior.cs:200-210`), a
  `[SaveableProperty(83)]` that is set once and saved (`...\TaleWorlds.CampaignSystem\Campaign.cs:211-212`). It
  proves that campaign had cheat mode on at some save or config change, not that cheat mode was on in E2.

### 1.2 The circumstantial case, re-derived from E4

| Open (enter/exit) | What precedes it | heapMB enter to exit | First main-thread line after enter |
|---|---|---|---|
| E4:257/282 | party screen exit E4:256 (post-battle) | 502 to 487 | +1 s |
| E4:354/380 | EncounterStart E4:352, town_A1 menu E4:353, tournament after (E4:381) | 484 to 548 | +1 s |
| E4:2699/2723 | party screen exit E4:2698 (post-battle) | 514 to 506 | +0 s |
| E4:2729/2753 | EncounterStart E4:2728, 6 s before | 493 to 494 | +1 s |
| **E4:2755/2782** | the previous inventory's close, 8 s earlier; no encounter | **489 to 2,783** (2,841 at +29 s E4:2756; 3,027 at +57 s E4:2780) | **+31 s** (tableau census E4:2757) |
| E4:3222/3249 | party screen exit E4:3221 (post-battle) | 546 to 561 | +1 s |
| E4:3341/3366 | party screen exit E4:3340 (post-battle) | 523 to 532 | +1 s |
| E4:3391/3418 | EncounterStart E4:3390, Methir tournament after (E4:3420) | 515 to 607 | +1 s |

Only E4:2755 is not adjacent to an encounter or a party screen, which fits an own-inventory open (I key or the
navigation bar). At its peak (E4:2780) the process held 13,965 MB private, 608 MB of physical memory was free,
memLoad was 96% and system commit was 28,089 of 31,681 MB. The 31 s gap is the main thread building the list; the
1.5.x reading saw the same gap at 11 to 14 s on the maintainer's 63 GB machine with cheat mode on
(`origin/bannerlord-1.5.x:plans/_audit/2026-10-02-perf/evidence/memory/campaign-memory-reread.md:61-70`). The heap
path matches the maintainer's v1.4.8 cheat-mode open, heap 489 to 2,694 at close
(`docs/investigations/native-commit-audit-2026-08.md:600-602`, working tree; cheat mode on per `:398`).

Two small corrections to memory.md:73-75: the "16 gen2 collections" window (E4:2728 to E4:2816, 146 s) also contains
two other inventory transitions and a save (SaveBegin E4:2794), and the engine runs a full collection at every state
push and pop (audit `:454`), so not all of them are pressure from this open. "Back to 482 MB only after the next save"
is not shown: there is no heap reading between the exit (E4:2782, 20:40:21) and E4:2816 (20:40:49).

Verdict: Likely, not proven. One `cheatMode=` field in future logs settles it (1.5).

### 1.3 The v1.4.8 path and its allocation

- `Helpers.InventoryScreenHelper.OpenInventoryPresentation`
  (`_categories_v1.4.8\Campaign\TaleWorlds.CampaignSystem\Helpers\InventoryScreenHelper.cs:174-196`, identical in the
  installed-DLL decompile `C:\Users\mikew\.taom-src\v1.4.8\Helpers.InventoryScreenHelper.cs:174-177`): under
  `Game.Current.CheatMode` (`:177`) and no test harness it calls `itemRoster.AddToCounts(item, 10)` once per
  `ItemObject` (`:186`). That is one row per item type with an amount of 10, so about 5,166 rows for this player
  (E3:109 and E4:107; `ItemPoolAdapter` iterates the same `GetObjectTypeList<ItemObject>()`,
  `Main/Adapters/ItemPoolAdapter.cs:62-80`). memory.md:79 "every ItemObject ten times" should read "every item once,
  stacked to 10".
- `Game.CheatMode` returns `NativeConfig.CheatMode`, the engine config's `cheat_mode` (`Core\...\Game.cs:67`,
  `MountAndBlade\...\MBGameManager.cs:22`, `Engine\...\NativeConfig.cs:7`).
- Only `OpenScreenAsInventory` (`:271-274`) reaches it. Its callers: the map's `OpenInventory`
  (`_modules_build\SandBox__SandBox.View.cs:16036`), the inventory navigation element (`:22497`, `:22504`) and the I
  key in a mission that allows inventory access (`:5104`). The loot, stash, warehouse and trade openers never read
  `CheatMode`.
- Per row the VM builds an `SPItemVM` and evaluates price, usability and the filter
  (`...\Inventory\SPInventoryVM.cs:4480-4534`); the widget cost per row is not derivable offline (the 1.5.x
  researcher's reading is 85 widgets and about 0.5 MB per row, `origin/bannerlord-1.5.x:.../inventory-open-alloc.md:19-22`).
  Measured with cheat mode on: v1.4.8 desktop +2.9 GB private per open, managed 475 to 3,15x MB at each exit
  (audit `:420`); v1.5.3, 2.7 to 3.3 GB managed at exit over 16 opens (reread.md:61-64); E4 +2,352 MB managed at
  +29 s, +2,538 MB at peak, +3,102 MB private at peak.
- Party screen twin confirmed: `PartyScreenHelper.OpenScreenAsNormal` routes to `OpenScreenAsCheat` under cheat mode
  (dump `...\Helpers\PartyScreenHelper.cs:116-196`; installed cache lines 43, 116, 185), measured +450 MB per open and
  +1.0 GB kept once (audit `:418`).
- After close: the 1.4.x record says the graph is held 45 s or more (audit `:600-609`); the later 1.5.x reading says
  it survives only the pop's own collection, taken while the screen is still on the stack, with the pinning reference
  UNVERIFIED (`inventory-open-alloc.md:36-54`). memory.md:92 states the older reading as settled; it is contested.

### 1.4 Alternative explanations if cheat mode was off

1. A trade screen with a huge roster: town rosters are capped at 200 (`Main/Features/CultureMarketplace/Domain/MarketplaceTuning.cs:28-29`),
   and the same encounter's earlier open (E4:2729) did not spike. Unlikely.
2. A stash or warehouse holding thousands of item types: no evidence; TAOM opens a stash only for its refuge, with
   the party's own roster (`Main/Features/Refuge/Hooks/RefugeMenuController.cs:99`). Unlikely.
3. A TAOM hook scanning the item table on open: the seven TAOM patches on the inventory path (`Patch33_*`,
   `Patch34_*`, `SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch`, HEAD; none among the 52 drifted files)
   contain no `GetObjectTypeList` and no game-wide loop. Ruled out.
4. A third-party mod: none loaded (E4:32-40). Ruled out.
5. The player's own party roster: it is the right-hand list of every loot screen too, and those opens moved the heap
   by -8 to +15 MB (E4:2699, 3222, 3341). Ruled out.
6. Uncollected garbage (`heapMB` is `GC.GetTotalMemory(false)`): the heap stayed at 2,780 to 3,027 MB for 88 s through
   several collections, so the bulk was live while the screen was open. Ruled out as the main effect.

### 1.5 Proposed 1.4.x fix, in order

1. Probe (S): add `cheatMode=` (from `NativeConfig.CheatMode`) and the texture, foliage and terrain quality and texture
   budget option values to the session snapshot (`Main/Features/MissionDiagnostic/MissionDiagnosticService.cs:23-66`),
   read through an adapter. 1.5.x has one, `IGraphicsOptionsAdapter` (`origin/bannerlord-1.5.x:Main/Adapters/GraphicsOptionsAdapter.cs`,
   57 lines, introduced by c17541f2c, which WP 109 also ports: one owner). Optionally the `[InvOpen]` line of
   `inventory-open-alloc.md:68-71` (cheat flag, inventory mode, left and right row counts) in a `SPInventoryVM`
   constructor postfix. The next player log then answers MEMORY-01.
2. Docs (XS): wherever TAOM sends players to a `taom.*` command (for example
   `docs/releases/2026-09-12-fortnight-discord.md:31`, `taom.rescue_time`, with no mention of cheat mode), say it needs
   `cheat_mode = 1` in `engine_config.txt` and to set it back to 0 afterwards, because cheat mode makes the own
   inventory list every item (about 2.5 GB and up to half a minute on a 16 GB machine) and the party screen every troop.
3. Guard (S, NEEDS MIKE for the default): a Harmony transpiler on the private static
   `Helpers.InventoryScreenHelper.OpenInventoryPresentation(TextObject, Action)` replacing its single
   `Game::get_CheatMode` read with a TAOM gate (cheat mode AND an MCM option); optionally a prefix on
   `PartyScreenHelper.OpenScreenAsNormal` that calls the private `OpenPartyScreen(false)` when the option is off.
   Prefer this to memory.md:97's "prefix that builds the same InventoryLogic", which would copy the private
   `GetCurrentMarketDataForPlayer` and the 11-argument `Initialize`. Default OFF suits players who turned cheat mode
   on only for a TAOM rescue command; default ON keeps vanilla behaviour for players who want the item list.
4. Root alternative (NEEDS MIKE): let the read-only and rescue tiers (`taom.time_status`, `taom.rescue_time`) run
   without cheat mode. Vanilla's `mission.*` cheats need none (`Main/Features/DevConsole/DevConsoleGuard.cs:11-15`).
   It changes gate policy and `ConsoleCommandBindingTests`.

Test: unit tests for the gate (cheat x option); a BindingVerification test that resolves the private target on the
installed DLL and asserts exactly one call was replaced; in game on the laptop with `cheat_mode = 1`, an I-key open
with the option off keeps the `[MemStation]` enter-to-exit heap delta under about 100 MB and the tableau census within
about 1 s, and with it on the spike reproduces. STOP if the v1.4.8 body holds other than one `get_CheatMode` call or
the private method does not resolve.

### 1.6 Do TAOM's cheat-gated commands make this common?

They create a route; prevalence is UNVERIFIABLE (no telemetry). Every `taom.*` command needs cheat mode
(`TaomConsole.cs:31-41`: `RunInCampaign` uses vanilla `CampaignCheats.CheckCheatUsage`, `RunInMission` and
`RunAnywhere` use `CheckCheatMode`, `DevConsoleGuard.cs:35-76`). The public 2026-09-12 note sends players to
`taom.rescue_time` without saying so, and `docs/features/dev-console.md:193-194` already assumes players enable cheat
mode for gold. Both players in this evidence set show a cheat trace (the E1/E2 campaign's saved flag; E4's signature).
Two of two suggests it is not rare; it is not a rate.

## 2. MEMORY-02: banner atlases

### 2.1 Exactly what 8cdb11408 and ab338de2d change

- 8cdb11408 (2026-10-21, Refs #913): one file, `Main/_Module/Assets/BannerIcons/taom_banners_abanissa_alpha_01_tex.tpac`,
  545 to 534 B, RGBA8 to DXT1 (blob b6c1e0be2 to 1b50b5792).
- ab338de2d (2026-10-21, Refs #913): the other 32 `taom_banners_*_tex.tpac` descriptors, each 11 B smaller (for
  example `gondor_alpha_01` 541 to 530 B, `mistymountainorcs_alpha_01` 584 to 573 B), plus one hunk in
  `docs/reference/banner-icon-generation.md` (Step 4a import settings; "copy the saved `_tex.tpac` into the repo
  before anyone builds") and one row of `docs/ai-includes/orientation.md`.
- Decoded on ab338de2d: 24 DXT1 4096x4096 1 mip (8 MiB) and 9 DXT5 4096x4096 1 mip with `has_alpha` (16 MiB):
  dolguldur_alpha_02, dunland_alpha_02, goblin_alpha_01, gundabad_alpha_02, isengard_alpha_02, misc_alpha_01,
  mistymountainorcs_alpha_01, mistymountainorcs_alpha_02, mordor_alpha_02. 336 MiB against 2,112 MiB.
- Byte diff of one pair (gondor_alpha_01, 91ebcef65 against ab338de2d): the format string, its length, the metadata
  size, several 8-byte hash fields and one flag byte change; the texture item guid does not, so the `_mtl.tpac`
  reference stays valid. Each descriptor carries two metadata segments of 114 and 38 B and no pixels.
- Nothing else changes: the `_mtl.tpac` set (tree hash f182dfa779dc), the 33 PSD sources (365af9034d96),
  `ModuleData/banner_icons.xml` (blob 76faa0c88), `GUI/TAOMSpriteData.xml` (73edf51d3) and `GUI/SpriteParts/Config.xml`
  (84a78f7e5) are identical on HEAD, origin/bannerlord-1.4.5, 91ebcef65 (8cdb11408^) and ab338de2d. HEAD's descriptor
  set equals 91ebcef65's (8554c38ac7f4), and ab338de2d differs in exactly the 33.

### 2.2 Where the uncompressed atlases live on 1.4.x

- Repo, both refs: settings only. `Main/_Module/Assets/BannerIcons/` has 33 `_tex.tpac` (all `R8G8B8A8_UNORM`
  4096x4096, 1 mip; 9 with `has_alpha`), 33 `_mtl.tpac` and one stray Kit temp file
  `taom_banners_dunland_alpha_01_tex.tpac.ptemp` (the 34th "_tex" entry, untouched by ab338de2d, deleted by
  9985f2db3). Sources: 33 PSDs in `Main/_Module/AssetSources/BannerIcons/`. The repo does not track `pack0.tpac`
  (`git ls-files Main/_Module/AssetPackages` lists only the four camp packs).
- Compiled pixels: the Kit's `RuntimeDataCache\*.rdc` on a dev install (git-ignored; this laptop's installed TAOM has
  none) and the published `Modules/TAOM/AssetPackages/pack0.tpac`.
- What players load is the pack: E1 and E2 name only `Modules/TAOM/AssetPackages` for TAOM assets (E2:195), never
  `Modules/TAOM/Assets`. The laptop's installed `pack0.tpac` (188,902,921 B, 2026-09-07) holds the 33 atlases as
  `R8G8B8A8_UNORM` 4096x4096, 1 mip, 67,108,864 B decoded each (2,112 MiB), stored compressed in 370,957 to 2,540,557 B
  each (43.3 MiB). All 86 repo descriptors match that pack's format, size and mip count exactly. The 1.5.x audit found
  the dev, testing, patreon and public copies of `pack0.tpac` identical on 2026-10-03, 188,902,921 B with one MD5
  (`origin/bannerlord-1.5.x:plans/_audit/2026-10-02-perf/evidence/memory/kit-worklist.md:21-23`), so players very
  likely carry the same RGBA8 atlases (the MD5 was not re-read here).
- Vanilla on v1.4.8, re-read this session: 16 `custom_banner_icons*` textures, BC7 2048x2048, 1 mip, 4 MiB each
  (`Modules/Native/AssetPackages/core.tpac`), plus `ui_bannericons_1` BC7 4096x1024 (`gauntlet_ui.tpac`). The deep
  dive's v1.5.3 figure holds on v1.4.8 (8cdb11408's body says "a single 4 MB sheet"; v1.4.8 has 16).

### 2.3 Which atlases a campaign names

Re-ran `origin/bannerlord-1.5.x:plans/_audit/2026-10-02-perf/evidence/memory/banner_atlas_use.py` from git on stdin
against the working tree plus the installed TAOM_Map ModuleData: 368 icons on 33 atlases; 263 banner keys
(`characters/clans.xml` 145, `spclans.xslt` 72, `taom_spcultures.xml` 24, `taom_spkingdoms.xml` 14,
`spkingdoms.xslt` 8) name 19 atlases, 1,216 MiB as RGBA8. The 14 never named include all nine DXT5 sheets, so the 19
named sheets all become DXT1: **152 MiB**, a 1,064 MiB saving. The same run reports 15 layer references to icon ids
at or above 10000 with no `banner_icons.xml` row (17299 x8; 17104, 17371, 17358 x2 each; 17281 x1): not memory, a lead
for the xml-data lens.

### 2.4 Residency: probably VRAM, not private bytes (UNVERIFIED)

The maintainer's 1.4.8 map-baseline VMMap lists exactly two 64.00 MB private regions and attributes them to the two
`ui_taom_bannericons` UI sprite sheets (audit `:493-495`); a texture kept CPU-side shows as its chain plus 43,664 B
(`:535-543`). With banners all over the map, 19 more such regions would appear if the material atlases kept CPU
copies, and none is listed. So the 1,064 MiB is most likely dedicated GPU memory (and shared memory on cards that
spill), not process private bytes. memory.md:128-133 flags this; its summary (memory.md:421-423) still quotes "about
1.2 GB of texture memory" beside private-bytes figures. Its verify step (privMB at MapScreen + 60 s and dedicated GPU
memory, before and after) is the right one.

### 2.5 Port path

- Byte for byte? The inputs are identical (PSD sources, pre-image descriptors, materials, `banner_icons.xml`, so the
  atlas layouts too), and both lines' descriptors parse as TPAC container version 2 with texture metadata version 3.
  But a descriptor is only settings. What players get is the pixel data in a republished `pack0.tpac`, which no 1.5.x
  commit carries and only a Kit produces. A new descriptor dropped into an install whose `.rdc` still holds the RGBA8
  compile is the header and cache mismatch that ab338de2d's doc hunk warns about (blank sigils; editor crash at
  `TaleWorlds.Native.dll+0x4F626D`). So: re-import in a v1.4.8 Kit with ab338de2d's settings, copy the Kit-saved
  descriptors to the repo, and use ab338de2d's 33 blobs as a parse-level oracle (format, mip count and size must
  match; hash bytes may differ). Copying the blobs is acceptable only on an install with no `.rdc` for those 33, and
  the Kit must still compile and publish.
- Which machine: a v1.4.8 Kit is required; a pack built by a v1.5.x Kit loading on v1.4.8 is UNVERIFIED and must not
  be assumed. This laptop has one: `bin/Win64_Shipping_wEditor/Version.xml` reads `v1.4.8` (2026-08-22), and the
  installed TAOM has the 33 PSDs under `AssetSources/BannerIcons`. But here the Kit probably cannot load TAOM yet:
  `Modules/TAOM.Dependencies/bin` has no `Win64_Shipping_wEditor` and the four `Bannerlord.*` community module folders
  are one-file stubs, the 1.5.x condition under which the Kit showed a "Cannot find" box per DLL and aborted
  (`origin/bannerlord-1.5.x:docs/reviews/rca-banner-packing-2026-10-28.md`, finding 7). The desktop published every
  earlier pack (`E:/LOTRAOM_Releases`, kit-worklist.md), but whether it still has a v1.4.8 install is unknown. NEEDS MIKE.
- Prerequisite (dev tooling, no player effect): from 9985f2db3 the `MirrorWin64ShippingClientToEditor` target in
  `Dependencies/TAOM.Dependencies.csproj` (1.4.x has only the server mirror, HEAD `:115-124`) with the comment as
  corrected in b41de91e8; from b41de91e8 `IncompatibleModDetector.IsEditorBin`, its early return in `RunEarlyPhase`
  (1.4.x has the detector without it, `Dependencies/Foundation/IncompatibleModDetector.cs:40-42`) and
  `TAOM.Tests/Infrastructure/IncompatibleModDetectorTests.cs`.

### 2.6 What else must change

Nothing in `banner_icons.xml`, the `_mtl.tpac` files, `TAOMSpriteData.xml`, the sprite parts or the banner icon ids.
Port the two doc lines and add a gate (plan inputs). Republish through the editor's Publish Module and
`tools/package_release.py`, which drops `.rdc` and `AssetSources` (`tools/package_release.py:121-135`). Leave the
`ui_taom_bannericons_NN` UI atlases alone in this job: the 1.4.x guide requires them uncompressed and unmipped
(`docs/reference/banner-icon-generation.md:81`), which MEMORY-03's BC7 proposal contradicts (NEEDS MIKE before
MEMORY-03 is planned; side check: the installed pack's sheets `_1` and `_2` are RGBA8 1 mip as MEMORY-03 says; the
only DXT5 ones are dead sheets `_24` and `_25`).

### 2.7 9985f2db3 and b41de91e8 (#1047)

- 9985f2db3 (212 files: 38 A, 136 D, 38 M) mixes four things: the 39 dead GUI sheets (39 descriptors under
  `Assets/GauntletUI`, 39 PNGs under `AssetSources/GauntletUI`, sheets 3 to 41); the BannerIcons repack (19 sheets
  deleted with materials and sources plus the `.ptemp`, 10 packed sheets added); a sprite rebake with realm-banner
  content (`ui_taom_1..5`, `ui_taom_bannericons_1/_2`, fonts, `TAOMSpriteData.xml`, six new sprite parts) and clan and
  kingdom data; and the editor mirror in `Dependencies/`.
- The dead-file removal ports on its own. On 1.4.x `ui_taom_bannericons` declares 2 sheets
  (`Main/_Module/GUI/TAOMSpriteData.xml:29-35`); `SpriteCategory.Load` loads sheets 1 to `SpriteSheetCount` only
  (`_categories_v1.4.8\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs:50-62`, loop at `:56`;
  the other two loops at `:116`, `:127` are bounded the same way); `git grep` finds no other reference to sheets 3 to
  41; they are leftovers of the earlier 41-to-2 rebake (`CHANGELOG.md:12023-12030`). Runtime effect: none. Pack
  effect: 39 textures that decode to 2,410.7 MiB and occupy 39.6 MiB stored leave `pack0.tpac` once it is republished
  from an install where they are also deleted (the build's copy never deletes; delete each `.rdc` entry before its
  descriptor, the order the packing RCA learned). memory.md:296's 2,538.7 MiB counts all 41 sheets. The 39 PNGs
  (70,479,496 B on this laptop's dev deploy) are not in a player download, because `package_release.py` excludes
  `AssetSources` (`:134-135`); the classifier note calling them part of "the public build" is UNVERIFIED.
- Do not port: the BannerIcons repack, `tools/pack_banner_sheets.py`, the rebake, the realm colours (b41de91e8's
  `clans.xml`, `factions.json`, `taom_spcultures.xml`). Packing saves little once the sheets are DXT1 and it is tied
  to 1.5.x realm content.
- From b41de91e8 take only `IsEditorBin` with its test and the csproj comment (2.5); re-check its Kit facts in
  `docs/reference/bannerlord-engine-and-toolchain.md` and `docs/features/scene-scripts.md` against the v1.4.8 editor
  build before porting them.

## 3. MEMORY-05: a second game in one process

### 3.1 What the three 1.5.x commits fix

- e99d705a3 (2026-09-24): Enlistment `OnNewGameCreated` calls `ResetSessionCaches()`; on 1.4.x it only clears the
  store (HEAD `Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs:124-130`).
- a67792c40 (2026-09-24, 17 files): plan 014 decisions, including `SubModule.OnGameEnd` calling `ResetSessionCaches`
  (1.5.x `Main/SubModule.cs:884`), latch clears and a store clear on a load without Enlistment data.
- 4263535a1 (2026-09-24): SpecialResources reads the load into a null local and wipes storage on a new game. Both
  1.4.x refs still seed the load from the live singleton
  (`origin/bannerlord-1.4.5:Main/Features/SpecialResources/SpecialResourcesBehavior.cs:102-108`). The store is a flat
  `Dictionary<string, float>`, so its memory effect is nil. 1b00d1acd separately fixed a static
  `ScreenManager.OnPushScreen` subscription that kept every earlier behavior instance subscribed across loads; that
  fix is on origin/bannerlord-1.4.5 only, not in the public build.

Memory relevance: only a67792c40's `OnGameEnd` line can release a sizeable graph. On 1.4.x the Enlistment army adapter
"holds a live Army REFERENCE on a singleton that outlives the campaign" and is cleared only on the next load (HEAD
`Main/Features/Enlistment/ServiceMaintenanceService.cs:217-241`, comment `:227-232`), so for a player in an army the
finished campaign stays reachable at the menu and into the next load. 1.4.x already clears the two ArmyTargeting
singletons in `OnGameEnd` for the same reason (HEAD `Main/SubModule.cs:668-695`).

### 3.2 What remains unexplained

- Managed: the 1.5.x record shows the retention persisted after all three commits landed. Of nine menu returns
  dated 2026-09-29 to 10-02, seven entered at 300 to 393 MB and exited at 279 to 346 MB after the push's collection,
  two at about 100 MB, against 73 to 83 MB cold (`campaign-memory-reread.md:114-149`; "after" is by date, the table
  carries no build stamps). So the ports are not the memory fix, and the holder of 200 to 450 MB is unnamed.
- Native: menu returns sit 2 to 4 GB above the cold menu (10,273 against about 6.6 GB on v1.4.8, audit `:612-614`;
  exits of 8,810 to 10,363 MB against 7.4 to 7.7 GB on v1.5.3). The 4 MB allocator chunks are opaque to VMMap
  (audit `:489-492`, `:543`). TAOM probably cannot reach this; a restart does.
- The lens missed SAVE-TIME-01 (`deepdive/save-time.md:44-120`), the strongest evidence yet of state surviving
  `Game.Destroy`. E2 reloaded one byte-identical save eight times (`[GameData.Read]` header 81,638,791 B, objects
  318,515,982 B, containers 610,793,860 B, at E2:20190 through E2:27174 and on to E2:30024); the next save wrote
  1,418.9 MB instead of about 966 (E2:31622). Saves after one to three loads took 91 to 262 s; after the burst
  948 to 1,461 s (E2:31625, E2:32143, E2:33650, E2:34377). Neither E3 nor E4 reloads in-process (one `LoadRequested`
  each, E3:24, E4:21), so MEMORY-05 and SAVE-TIME-01 both bite the E2 player, not the E3/E4 one.

### 3.3 Cheapest probe

One laptop session, no code: launch the public build, load a save, return to the main menu, note the
`[MemStation] enter/exit screen='GauntletInitialScreen'` heapMB, take a PerfView heap snapshot (free Microsoft tool,
supports .NET Framework; not checked as installed here), reload the same save three times, save, return to the menu,
snapshot again. Compare counts of `Campaign`, `Hero`, `MobileParty`, `Army`, `CharacterObject` and `ItemObject` and
read the GC-root paths; in the same run read `[SaveDataSizeRecord]` and `[GameData.Write]` in the rgl for SAVE-TIME-01.
Cheapest code probe: a `WeakReference` to the ending `Campaign` taken in `OnGameEnd` and checked after the next
main-menu push's collection, logged as one line (`previous campaign collected=true|false`). STOP: if the old campaign
is collected and the heap is still over 300 MB at the menu, the holder is not the campaign graph, so look at process
caches rather than TAOM singletons.

## 4. Player-facing numbers and levers

### 4.1 Numbers an answer can quote

| Number | Source | Caveat |
|---|---|---|
| Menu 6.0 to 6.2 GB; campaign map 8.8 to 9.0 GB at the loading screen's exit and about 10.0 GB once settled; battles 9.4 to 10.7 GB; a 533-agent town 10.7 to 10.8 GB | E3:19, E4:18, E3:153, E4:152, E3:157-158, E4:158, E3:219-223, E4:2980-2987, E4:5137-5164 | one player, 16,321 MB RAM, public build v2.0.29.5, settings unknown |
| The .NET heap is 0.45 to 0.55 GB of that | same lines | the rest is the engine: textures, scenes, meshes |
| Of a 6.6 GB menu, TAOM's three modules are about 2.2 GB (Armory 0.97, TAOM 0.7, TAOM_Map 0.5); the game and its libraries about 4.4 | audit `:685-707` | maintainer's desktop, 2026-09-12 |
| Texture quality highest (2) to lowest (0): -2.3 GB on the map; foliage highest (4) to lowest (0): -0.77 GB; terrain -0.2 GB; all live, no restart | audit `:638-666` | maintainer's desktop before the 2026-09-13 map downsizing, so smaller on today's map (not re-measured); v1.4.8 has 3 texture and 5 foliage levels (`...\TaleWorlds.Engine.Options\NativeSelectionOptionData.cs:39`, `:52`) |
| A second load in one process settles 1.8 GB higher; returning to the menu keeps 3.7 GB | audit `:597`, `:612-614` | maintainer's desktop |
| Every reload in one session adds about 88,000 objects to all later saves of that campaign; E2's saves grew 857 to 1,515 MB and took 4 to 6 times longer | E2 save statistics; save-time.md:44-120 | mechanism UNVERIFIED |
| With cheat mode on, opening your own inventory costs about 2.5 GB and up to half a minute on a 16 GB machine | E4:2755-2782; audit `:420` | E4's cheat mode is inferred (1.2) |

Not quotable: "TAOM sits at 15.7 GB" (the maintainer's figure; the 1.4.x record on that machine reads 13.2 to 14.8 GB
settled and 16.8 to 17.3 GB during cheat-mode inventory opens, audit `:402-435`, measuring method unknown); "texture
quality one step down frees 2.3 GB" (memory.md:420; the measurement was two levels with foliage and terrain at 0);
"the banner fix frees 1.2 GB of RAM" (probably VRAM, 2.4); "about 4 GB of the process is paged out" (private minus
working set also counts compressed and never-touched pages).

### 4.2 Levers and the evidence for each

| Lever | Evidence | Strength |
|---|---|---|
| Texture quality down | audit `:638-666` (2.3 GB, top to minimum) | measured, old map; one level unmeasured |
| Foliage down | same (0.77 GB) | measured |
| Terrain down | same (0.2 GB) | measured, small |
| Quit to desktop before loading another save | 1.8 GB settle and 3.7 GB kept (audit); E2's reload-driven save growth and slower saves (SAVE-TIME-01) | measured; growth already in a save stays |
| `cheat_mode = 0` unless a command needs it | 1.3 | cost measured; E4's cause inferred |
| Page file on, system-managed | E4's machine: commit limit 31,681 MB on 16,321 MB RAM (about a 15 GB page file); system commit peaked at 28,089 MB (E4:2780), about 11.8 GB above RAM | protective only: without it that session runs out of commit; it does not remove stutter; no crash in the evidence shows this failure |
| Close other programs | E3:19: at the menu Bannerlord held 6,133 MB of 19,876 MB system commit, and about 7 GB of RAM was in use outside its working set | computed from the log; what the other software is, is unknown |

## 5. Corrections to the memory lens

1. memory.md:31-32: the E2 "cheat usage" line proves a saved flag, not cheat mode now (1.1).
2. memory.md:79: one row per item type, amount 10; not ten copies.
3. memory.md:73-75: the gen2 window mixes three state transitions and a save; "only after the next save" is not shown.
4. memory.md:92: "rooted until the next game-state change" is contested by the later 1.5.x reading.
5. memory.md:136: the 19 named atlases become 152 MiB (all DXT1), not about 200.
6. memory.md:296: 2,538.7 MiB is all 41 sprite sheets; the 39 dead ones are 2,410.7 MiB decoded, 39.6 MiB stored.
7. memory.md:197-219: the three 1.5.x ports did not remove the retention on 1.5.x; the finding should cross-link
   SAVE-TIME-01 and share its probe.
8. memory.md:184-186 and :420: savings were measured top-to-minimum, not one level down.
9. memory.md:421-423: the banner saving is probably VRAM; do not set it beside private-bytes numbers.
10. Missing from the lens: the 31 s main-thread gap on the spiking open (E4:2757), the stray `.ptemp`, the Kit
    prerequisite (editor mirror) for any 1.4.8 Kit job, and the 1.4.x rule that keeps UI atlases uncompressed
    (`banner-icon-generation.md:81`).

## 6. Recommendations to the orchestrator

- `/research` on `Helpers.InventoryScreenHelper` and `Helpers.PartyScreenHelper` before the guard; `/verify-bindings`
  after it.
- `/investigate` for the shared MEMORY-05 and SAVE-TIME-01 probe.
- `/deep-review` on the guard commit and on the Dependencies mirror commit (C#, more than one file).
- Side note outside this topic: the session snapshot writes `Environment.MachineName` into every player log
  (`MissionDiagnosticService.cs:31`); whoever edits that line for the probe may want to drop it.
