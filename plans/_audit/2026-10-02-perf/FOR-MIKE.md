# FOR-MIKE: 2026-10-02-perf

## The night in brief

- **Built, each on its own branch, nothing merged, pushed, filed or paid:** all 15 plans (028 to 042)
  built, verified and deep reviewed, each with its convergence rounds run and its records closed
  (the last records commit, 035's `e403dc7c`, at 12:54 on 2026-10-03). Every review ended READY FOR COMMIT; the open choices are item
  16. `evidence/merge-map.md` gives the merge order for 10 of the 15 branches; 16g has the rest.
- **Measured or read from the engine tonight:** half of every load is the module XML merge, and plan 042
  (built and reviewed) merges it in one document per type: on your install every type comes out identical
  to the engine's, at 27 to 35 percent of the engine's time for the four heaviest types (the fair
  offline figure; about 26 s of a new campaign's 50 s load are those types in game). The engine drops
  its shader cache on a module-list or game change; whether a player then pays the 40 s compile once or
  at every start is open again (item 1). PatchShield cost 64 ns and 241 bytes per call (plan 034 removes it);
  the 1,090 ms battle hitch is silent in both logs (plans 036 and 041 instrument the leading suspect,
  clip loading).
- **The biggest lever found:** TAOM's 33 banner icon atlases are uncompressed 4096 px textures, 64 MiB
  each against vanilla's 4 MiB. The campaign's own banners use 19 of them: up to 1.2 GiB once all are
  drawn, 76 MiB in vanilla's format (item 13c, a Kit change).
- **Also found in the data and the binary:** five clans' banners draw without some or all of their
  emblems (13e); module sounds load per play, not at startup, but 21 unregistered files ship in every
  download (13d); and the hitch probe's one safety question has a good answer (16l). Plan 038's new
  equipment memory audit ranks the battle assets: its first run puts the crewed
  mumak's platform mesh `sk_mumakil_platform_a1` on top at 65 MiB, over the LOD0 face budget. Its review found that the Uruk-hai champion and berserker never wear their skirt (16s).

## Start here (priority order; the numbers point at the items below)

1. **Five minutes, any time:** the cheat-mode-off check of the I-key inventory and party screens (item 12).
   It tells us whether players pay the 2.5 GB, 11 to 14 s inventory open your logs show, or about 100 MB.
   With the Modding Kit closed, a second game start afterwards also answers item 1's open question.
2. **A yes or no that unblocks landing:** file the issue drafts (item 9); nothing lands on a trunk
   without them. Before filing: a label choice (six labels the drafts use do not exist) and four drafts
   brought in line with their branches.
3. **Three Kit changes, low risk (item 13):** the UI sprite sheets: 40 MB off every download, about
   224 MB less texture memory all session; and the banner icon atlases, 4096 px uncompressed at 64 MiB
   each against vanilla's 4 MiB: the 19 your campaign's banners use come to 1.2 GiB once drawn, likely
   the largest memory lever found tonight (its in-game share is unmeasured). Plus two repo fixes: 21 sound files
   that no `module_sounds.xml` entry names, 53 MiB off every download (13d), and five clans whose
   banners lose emblems to icon ids no module defines (13e).
4. **When 028, 029, 036 and 041 are merged into a build:** the measurement session (item 3, protocol
   under Detail). It answers the battle hitch question and ranks every battle plan by measured cost.
   With 042 in the same build, start one new campaign and one custom battle as well: the `[XmlMerge]`
   lines in taom_debug.log and the loading screen against today's 50 s show the load-time win in game.
5. **Your design calls, no rush:** area effects that skip mounts (item 8), the diagnostics cost (item 7),
   shader compiles, about 39 to 43 s of a cold game start, which may be paid at every start rather than
   once per install (item 1, open again), the map content levers (item 6),
   the texture advisor (item 5), trunk CI (item 14), and a latent PatchShield gap that can strip TAOM's
   own patches (item 15: a small plan, since the one-line fix has a cost).
6. **The review decisions (item 16).** One has a stability edge: plan 028 takes `Mission.OnTick` out of
   PatchShield for every player, and a broken foreign patch there can stop a battle from ever closing
   (16a, best decided together with item 15). The rest are small. Not in this list: items 2, 4, 10 and
   11 (no deadline), and 16u (owed after the merges).

## Waiting on you

1. **Shader sacks (answered 2026-10-02: you do not ship them, they have been problematic for players).**
   No action recommended. For the record: every channel still carries 52 to 53 per-scene sacks from the
   editor (all format `0x0783`; only Main_map, the Edoras town and Backups lack one in testing) and no
   module-level sack. Plan 035's packager report (built 2026-10-03, report only, never refuses) reads
   the same: the patreon channel ships 45 of 45 TAOM_Map scenes with a sack, Main_map included, while the
   testing channel and your dev install lack one for Main_map and the Edoras town. If first-session map loads ever draw complaints, the deciding measurement is one
   cold-cache campaign load with and without a Main_map sack (back up and empty
   `C:\ProgramData\Mount and Blade II Bannerlord\Shaders` first), compared on `[MapLoad]` time to
   ready, `compile_shader` count and whether map props render. If you can say what breaks for players
   with a sack, it belongs in docs/features/shader-precompilation.md, which has no record of it.
   **What the engine does, read from v1.5.3 on 2026-10-03** (`evidence/load/shader-cache-native.md`):
   - **Startup:** two 2026-10-02 sessions compiled the same 1,335 `pbr_metallic` shaders at startup, 32.5
     to 34.3 s of a roughly 53 s start (those logs have rotated out; `evidence/load/startup-shader-compiles.txt`
     keeps their counts), and both 2026-10-03 starts (07:59 and 09:02) compiled 1,336 again, in 38.9 s and
     42.5 s. They are the Armory's materials (sessions without the Armory
     compiled 0 and 6): 40 material flag combinations, eight of which the engine compiles in 104 contexts
     each (weather, quality, SSR and others), 888 of the 1,335.
   - **When players pay it (open again on 2026-10-03):** the engine drops its runtime shader cache when
     the set of active module ids or the game build changes ("A change in your active mods was detected.
     Deleting the runtime shader cache"), so a player who plays vanilla or another mod in between pays it
     at every return. But the two 2026-10-03 starts had the same game build (122374) and the same modules,
     and the second still compiled all 1,336: the cache folder
     (`C:\ProgramData\Mount and Blade II Bannerlord\Shaders\CoreShaders\D3D11`) holds only a 129-byte
     `shader_mapping.bin`. The Modding Kit (its own build, 122516) was open throughout and shares that
     folder, so on your machine a Kit session likely drops the game's cache (a dev cost, not a player's).
     The 2026-10-02 log that showed only 46 compiles after a full one has rotated out. **So "a player who
     plays only TAOM pays once per install or game update" is unproven.** The deciding test is two game
     starts in a row with the Kit closed (item 12's check can supply the first): if the second still
     compiles about 1,336, every player pays about 40 s at every start, and this item becomes a priority.
   - **Sacks:** your install has a Kit-written Armory sack (12:33 on 2026-10-02), and the 12:40 and 13:16
     starts still compiled all 1,335, though the Armory's own sack index lists every one of them. So a
     module sack as the Kit writes it did not remove the cost here. The engine's sack reader also stops
     the game with "a corrupted game file" message on a damaged sack, which fits sacks being trouble for
     players. Your policy stands on both counts.
   - **The campaign map (a correction):** the 2.97 s terrain step at the 12:45 load compiled nothing (its
     terrain shaders came from the runtime terrain cache), so it mixes shader loading with terrain setup;
     "a Main_map sack would save about 3 s" was an upper bound, not a measurement.
   - **A small cleanup, yours:** every channel ships the Kit's `shader_mapping.bin` and
     `shader_compile_report.log` for the Armory and TAOM_Map (about 26 MB) without a sack beside them.
     In what I traced they most likely do nothing in the game, which is not proven. The deciding test is
     one cold start (empty `C:\ProgramData\Mount and Blade II Bannerlord\Shaders\CoreShaders`) with those
     files moved out of the Armory, comparing the `compile_shader` count with today's 1,335.
2. **Clip residency lever, after plan 036's numbers.** Recommended: run one troll-heavy and one large
   vanilla-troop battle with the profiler, the clip probe (036) and the hitch probe (041) on. The engine
   schedules an eviction pass only once on-demand clip bytes pass 15 MiB, and the pass trims back toward
   the 12 MiB budget, so `[AnimMem]` readings between 100 and 125% of the budget are normal. If they sit
   near 125% with frequent drops, raise the budget (a guarded four-byte write, engine-wide; the 15 MiB
   trigger is a separate code constant the write does not move) and consider Loading Type 0 for TAOM's
   24 troll and 8 elephant clips (unique keys first). Build a raise together with a change to plan 036's
   probe, which recognises only the stock 12 MiB value and switches itself off on any other. Evidence:
   the engine reference page section 6 as corrected on the 036 branch; the program branch's copy and
   REPORT.md "Verified engine facts" still say "evicted past 12 MiB" and credit per-frame clip-load
   sampling to plan 028, where it is plan 041's.
3. **The first measurement session**, once plans 028, 029, 036 and 041 are merged into a build: the
   protocol is under "Detail" below. Its nine five-minute battles are 45 minutes of play, so about an
   hour to an hour and a quarter at the desk with loads and restarts, plus an hour of unattended map time
   for the memory soak (step 6).
4. **Debug versus Release**: no change proposed. The profiler makes the measurement you asked for
   affordable: the same battle on Debug and Release builds, three runs each, compared with
   `perf_runs.py compare --allow-mixed` (it refuses mixed builds without that flag). Seeing what a crash
   report loses needs a dev crash trigger, which no console command provides today, so that half needs
   a small addition first. Your call whether to run it.
5. **Texture-quality advisor for 16 GB machines** (extend the #701 advisor): on 2026-09-12, before the
   texture downsizing shipped, 2.3 GB of the campaign map's 4.4 GB moved with that one slider; today the
   map's TAOM textures are 1.1 GB, so the slider's effect is smaller and worth re-measuring in the
   measurement session. Recommended: yes, opt-in, shown once, if the re-measurement still shows a
   gigabyte or more.
6. **Content levers for memory and the map's GPU load**, each yours: the 2026-09-13 texture downsizing
   already shipped (patreon's Main_map textures measure 1.04 GB against 2.34 GB before it); next,
   downsize the 341 MB 16K vista (a third of what remains; 8K would save about 256 MB, 4K about 320 MB;
   vanilla's campaign map sets no vista texture at all, `SandBox/SceneObj/Main_map/scene.xscene`
   `vista_diffuse_name=""`), LODs for the
   six LOD-less heavy map meshes (`wildling_village_large` 98,508 faces placed 42 times first), and the
   lights and particles above vanilla (map 70 lights against 0 and 322 particles against 4; the Mordor
   scenes 429 to 531 lights against vanilla's maximum of 305; since trunk `d9a8f46f` re-enabled TAOM's
   field-battle scenes on 2026-10-03, `taom_mordor_battle_003_forceatmo`, 527 lights, serves ordinary
   field battles in 17 Mordor map cells). **One question only you can answer:**
   vanilla splits its textures across two package trees, a small low-resolution stub in
   `AssetPackages` (about 175 KB for a 4096 px texture) and the full mip chain in `EmAssetPackages`
   (1,038 packs, 28 GB, for Native), and the engine has texture streaming (a `disable_streaming`
   material flag, a `mipmap_streaming_tex` parameter). Every TAOM release channel ships each texture as a
   full chain in `AssetPackages` (the Armory 8.5 GB, the map 9.8 to 11 GB) and no `EmAssetPackages`.
   Whether the engine streams a full chain from the base packs or keeps it whole is not knowable
   offline. Does the Kit's publish offer a split like vanilla's? If it does, one test publish of the
   Armory would show the memory difference directly. **Also in the map packs:** 116 textures in the
   testing channel (349 MiB of its download, 1.05 GiB decompressed; 112 of them, 338 MiB, in the patreon
   and public downloads) that nothing references, as far as offline reading can
   tell. No material or other item in any TAOM, Armory, Native or SandBox pack points at them, and no
   scene, prefab, atmosphere, module data, GUI or source file names them. The largest are
   terrain-authoring leftovers: `terrain_heightmap` (93 MB in the download), `Normal Map`,
   `terrain_normal`, `Final Height`, `GAEA02`, `terrain_materialmal_layer55`. Unreferenced textures never
   load, so this is download size, not memory. They are candidates, not proven unused: confirm with
   one test publish without them, then a campaign load and a battle. The count is a lower bound: a name
   that happens to occur in compressed scene data counts as a reference, which is all that keeps `GAEA`
   (8.7 MB stored, 256 MiB decompressed) off the list. List: `evidence/memory/map-texture-orphans.txt`.
7. **Costly diagnostics: keep the information, cut the cost** (your instruction: taom_debug is a
   critical log). The candidates are EnableHowdahDiagnostics (per-seat per-frame MCM reads, which plan
   031 already caches, and a durable line every 5 s per platform), LogAutoResolvedBattles (a synchronous
   flush per world battle), the `[MapLoad]` heartbeat and SceneReady trace (a synchronous line every 5 s
   each after the map is ready; the SceneReady trace keeps writing inside battles too, 80 lines in one
   2026-10-02 Custom Battle), and the ungated spider, troll-smash and signature-hero INFO lines. Recommended: keep all of them on, and in
   a follow-up plan make each cheap without losing content: cached settings reads, aggregated or
   on-change lines instead of fixed-cadence repeats, and summaries at mission or session end. No default
   changes, so the persisted-MCM rename trap does not arise.
8. **Area effects never touch mounts or riderless creatures.** `GetNearbyAgents` and its enemy and ally
   variants return only active humanoid agents (verified natively: `AgentFlag.IsHumanoid` and
   `AgentState.Active`). So the elephant-like trample (`ElephantLikeAttackTasks.cs:59`, `ElephantLikeEngageDecorator.cs:52`; it
   serves the elephant, mumak, war ram, elk and moose), the troll smash ring (`BruteForceRing.cs:41`,
   `BruteForceReadyDecorator.cs:48`), the signature strikes (`SignatureStrikeRunner.cs:81`) and the dread
   aura (`DreadPulseRunner.cs:58`) reach riders but never a horse, warg or elk, and never a riderless
   creature (creature bandits, a riderless warg). They also skip agents already removed from the field
   (killed, unconscious, or routed off the map); a soldier who is fleeing but still on the field is
   Active and is hit. The troll smash and the signature strikes skip mounts in their own code as well
   (`victim.IsMount`, `BruteForceRing.cs:54`, `SignatureStrikeRunner.cs:98`), so for them it is already
   the design; the question stays open for the trample and the dread aura. Where it is not the design,
   the effect should add `agent.MountAgent` for riders and query mounts separately. Outside the
   question: the career morale burst and ally buffs (`GetNearbyAllyAgents` at 50 m, once per activation;
   they already handle mounts) and the grid-based strikes (spider and warg scans), which do reach
   mounts. All listed radii are at or under 15 m (the engine's grid), except the dread aura above its
   12 m default (it allows 30 m, which by the programme's native read makes the engine walk every agent
   per pulse; trunk's `DreadAuraConfigProvider.cs:35-38` comment and `dread-aura.md` say the opposite,
   so one side needs correcting). Evidence: engine reference page section 4.
9. **Issues**: one draft per plan is in `issue-drafts.md`. Filing is public and waits for your word; per
   `plans/README.md` an issue must exist before a branch lands on a trunk. Before filing (re-checked
   2026-10-03): the repo has 20 labels and none of `perf`, `diagnostics`, `tooling`, `release`, `memory`
   or `crash-safety`, and every draft uses at least one of them, so either create them or use
   `enhancement`, `bug` and `feature`; add `triage-needs-ingame` where a review asks for an in-game check.
   All 15 drafts were brought in line with their branches' final state on 2026-10-03 (028, 030, 032,
   034, 035, 036, 037, 038, 040 and 042 changed most), each now carries a Status section, 036, 040 and
   042 gained `triage-needs-ingame` (their reviews ask for it), and the public-text check exits 0. Only
   the label choice is left.
10. **yotthani**: ask for MithrilForge `docs/perf-audit/` (limb-ray-given-agent.md, clip-reader-lock.md)
    and the `feat/perf-audit` branch, which his commits cite but which are not pushed, and for a licence
    line on `yotthani/bannerlord` (the provenance register row is still `uncleared`).
11. **Trap index lines** (the orientation index is at its 45-row cap, and the entry docs have about
    600 bytes left under their hard cap): "Nearby-agent queries return only active humanoids" and
    "On-demand clips lock, block and are trimmed to 12 MiB once past 15 MiB". Recommended: add both,
    each replacing a row you consider settled (35 of the 45 date from the index's creation on
    2026-09-23, so age does not pick them; a row whose rule a named gate already enforces is the natural
    candidate).
12. **One 5-minute check only you can run: the inventory and party screens with cheat mode off.** Every
    logged inventory open (16 of 16 since 2026-09-12) added about 2.5 GB of managed memory, peaking near
    17 GB private, and the five since 2026-09-28 (the only ones whose log can show it) froze the game for
    11 to 14 s; the party screen added about 0.7 GB. The cause is your `cheat_mode = 1`: with cheat mode,
    vanilla fills the inventory's left list with every item in the game, ten of each (about 5,165 rows
    with the Armory), and the party screen with every troop (about 1,271), at about 0.5 MB per row
    (verified in the v1.5.3 code, `InventoryScreenHelper.cs:177-188`). A player without cheat mode should
    see 95 to 150 MB and under a second, but no log proves it yet. The check (amended 2026-10-03: the trade
    screen has no cheat-mode branch, `InventoryScreenHelper.cs:333`, so the I-key inventory is the screen
    to compare): with the game and the Modding Kit closed, set `cheat_mode = 0`; start the game, load a
    save, open the I-key inventory, a town's trade screen and the party screen once each, closing each;
    wait 30 s, quit, set it back. The `[MemStation]` lines then give the player's cost, and the next game
    start with the Kit still closed answers item 1. If it is small, nothing
    to do for players; for your own testing, the freeze is the price of the all-items list. Evidence:
    `evidence/memory/inventory-open-alloc.md`, `evidence/memory/campaign-memory-reread.md`.
13. **Content changes, all low risk: three in the Kit (a to c) and two in the repo (d, e).** (a) and (b)
    are TAOM's UI sprite sheets. Correction (2026-10-03): their sources are in the repo too
    (`Main/_Module/AssetSources` and `Assets`, byte-identical to the dev install), and `./build.ps1` copies
    `Main/_Module` over the install without deleting anything, so after the Kit work the changed files
    (and the 39 deletions) must be copied back into the repo before the next build, or the build puts the
    old ones back. `evidence/memory/kit-worklist.md` lists every file, a backup script and the checks. (a) Delete the 39 leftover banner-icon sheets `ui_taom_bannericons_3` to
    `_41`: the sprite data has declared only 2 sheets since 2026-08-08, so the engine never loads them,
    but they are 39.6 MB of every player's 180 MB `pack0.tpac`. No risk. (b) Re-save the four
    uncompressed 4096 px sheets (`ui_loading_1`, `_2`, `ui_taom_bannericons_1`, `_2`) as BC7: all five
    UI categories are always loaded, so these four hold 299 MB of texture memory for the whole session,
    and BC7 cuts that by about 224 MB with near-lossless quality and alpha kept. Look at a loading screen
    and the banner icons afterwards. Recommended: both. Evidence: `evidence/memory/ui-sprite-sheets.md`.
    **(c), found 2026-10-03, likely the largest of the three:** the 33 banner icon atlases that
    `banner_icons.xml` uses (`taom_banners_<culture>_alpha_NN` and the four ornament sheets) are each
    4096 x 4096 uncompressed RGBA with no mips, 64 MiB apiece and 2.1 GiB for all 33, where vanilla's
    own banner atlases are 2048 x 2048 BC7 at 4 MiB. Each loads when a banner using it is drawn, so a
    campaign showing many TAOM banners holds many of them. Re-exporting them as BC7 (4096: 528 MiB for
    all 33; vanilla's 2048: 132 MiB) keeps the alpha. How many a campaign holds, and how much shows in
    private bytes against VRAM, is not knowable offline: one campaign load before and after, read from
    `[MemStation]` after the map's first view, settles it. Computed from the data since: the campaign's
    own clan, kingdom and culture banners name 19 of the 33 atlases (1,216 MiB today if a session draws
    them all, 76 MiB at vanilla's format); the other 14 load only for a banner built at runtime, so
    those 19 are the ones to re-export first. Evidence: `evidence/memory/banner-atlases.md`.
    **(d), found 2026-10-03, repo content, download size only:** module sounds cost no memory until
    played (read in the engine binary: each file is opened when an event plays it and freed after), so
    they are not part of the main-menu floor. But `Main/_Module/ModuleSounds` ships 21 files that
    `module_sounds.xml` never names (53.3 MiB: eight `Native/OST` MP3s, the `LOTR/Dwarf/D1_*` set and
    others), and three registered music tracks under `LOTR/OST` (43 MiB) that nothing in TAOM, TAOM_Map
    or the Armory plays. One entry, `LOTR/Elves/Alert/elf_horn.wav`, names a file that does not exist
    (the file is `horn_elf.wav`); nothing plays it today. **Recommended: delete the 21, fix or drop the
    horn entry, and keep or drop the three tracks as you intend them**; if a feature should play long
    music, note each play decodes the whole track into memory (27 to 46 MiB each). A small gate in
    `validate_moduledata.py` (every `module_sounds.xml` path exists) would catch the horn case. Evidence:
    `evidence/memory/module-sounds.md`.
    **(e), found 2026-10-03, repo content, visible to players:** five clans' banners name icon ids that
    no module defines (17104, 17281, 17299, 17358, 17371). The engine skips such a layer without an
    error, so `clan_khuzait_16` (Zorian) shows a bare background, `clan_mirkwood_5` and `_6` lose 5 of
    their 7 emblem layers, and `clan_rivendell_2` and `clan_lothlorien_2` lose 2 of 38. **Recommended:
    pick replacement icons (a design call: yours) and add a `validate_moduledata.py` check that every
    banner key's icon ids exist**, which would have caught all five. Evidence: the "Banners that name
    icons no module defines" section of `evidence/memory/banner-atlases.md`.
14. **For your awareness, not this programme's work: trunk's C# CI has failed on every one of its last
    30 runs** (back to 2026-09-28). At `dffdf879` (run 37080081643) three tests fail:
    `EveryLanguage_DeclaresARowForEveryEnglishKey` (the translation run you own) and, under the CI's
    reference assemblies only, `Patch93_HasTheSevenPatchesInItsCategory` and
    `Patch94_HasTheMapIconNoParleyAndNoJoinPatches`, which cannot load `TaleWorlds.MountAndBlade.View`.
    The perf branches inherit exactly these three and add none (in local runs; no perf branch has been
    pushed, so CI never ran on them). Re-checked 2026-10-03: the latest run, `37131281649` at
    `7f0c8446`, fails the same three. Trunk's Python tool tests job is red too, at `9e2a39f4`, `dffdf879`,
    `d9a8f46f` and `7f0c8446`, on `test_ShippedTree_GeneratedFilesMatchTheirSources`. A small fix (tag
    the two wiring tests or give the reference set that assembly) would make the C# job meaningful again;
    say the word and it gets a plan.
15. **A stability gap in PatchShield, found by the review of plan 028 and verified by me: it does not
    protect TAOM's own patches.** Its protected owners are matched by case-insensitive prefix
    (`Dependencies/Foundation/PatchShieldPolicy.cs:25-66`, `:198-208`): `TAOM`, the BUTR and MCM ids,
    Harmony and the co-op ids, plus extras from `coop-modules.txt`. TAOM's Harmony owner is
    `com.taom.mod` (`Main/SubModule.cs:204`), which none of them matches. So when a missing-API exception
    (MissingMethod, MissingField or TypeLoad) escapes an engine method that TAOM also patches, even one
    thrown by another mod's patch, PatchShield unpatches TAOM's own prefixes, postfixes and transpilers
    on that method for the session (`PatchShield.cs:370-393`), and says so only in diag.log. The code
    comment there ("Filter now covers TAOM") and `docs/reference/engine/submodule-lifecycle-and-harmony.md:29`
    both say TAOM is meant to be protected; the tests pin only `TAOM.Dependencies.Foundation.SaveShield`.
    It only fires on a missing-API exception (after an engine update, or with another mod), so it is
    latent, not active. **Correction (2026-10-03): the one-line fix I first recommended has a cost.**
    Protecting `com.taom.mod` also stops PatchShield from stripping TAOM's own broken patch, which then
    keeps throwing and being swallowed on every call; `UncapturableHeroesBindingTests` describes that
    case as making every hero uncapturable for the session, where today the strip falls back to
    vanilla. The rescue strips every unprotected owner on the method, not only the one that threw
    (`PatchShield.cs`, its owner loop), so the choice today is between collateral stripping (a foreign
    mod's failure disables TAOM's patch there) and keeping a broken TAOM patch. **Recommended: strip only
    the owner whose patch threw** (read the patch method from the exception's stack and match it to
    `Harmony.GetPatchInfo`), then protect `com.taom.mod` as well. That is a small plan, not a one-liner,
    and it changes what PatchShield does, so it waits for your word. The same gap exists on
    `bannerlord-1.4.5` (`com.taom.mod` at its `Main/SubModule.cs:161`), so the plan needs a 1.4.5
    adaptation too.
16. **Decisions from the reviews of plans 028 to 042 (029's follow-ups too)** (2026-10-03; review records on each branch under
    `docs/reviews/`). No branch is blocked on them. Each is yours; my recommendation is in bold.
    - **a. `Mission.OnTick` on PatchShield's exclusion list (028): the one with a stability edge.** The
      exclusion saves one reflection lookup and a try/catch once per frame on the main thread. What it
      gives up, read in the v1.5.3 engine: if another mod's patch on `Mission.OnTick` throws a
      missing-API exception, in a process's first game the throw unwinds the whole application tick each
      frame (every module's `OnApplicationTick`, the job manager and the game handlers are skipped), and
      a postfix that throws after the mission has ended stops `MissionState.OnTick` from popping it, so
      the battle never closes. **Recommended: drop `Mission.OnTick` from the list, together with item
      15's fix** (strip only the owner whose patch threw), since a rescue there would otherwise also
      strip TAOM's own Patch35, Patch97 and Patch98's prefix (plan 030 deletes Patch35, so after the
      merges only Patch97 and Patch98 patch `Mission.OnTick`; four comments and docs still name Patch35
      and are fixed at the merge). Keep `TickAgentsAndTeamsImp` (a swallow there
      hangs the agent tick, so its exclusion is a fix). `OnPreTick`'s stated reason (only the profiler
      patches it) goes once 041 merges: its hitch probe (Patch98) patches `OnPreTick` and `Mission.OnTick`
      for every player with a prefix and a finalizer, and a strip removes the prefix but never the
      finalizer, which would leave the probe half-bracketed: one more reason to pair this with item 15. That narrows the rule in
      `.claude/rules/harmony-patches.md` from "per frame or per agent" to per unit and per agent, plus
      per-frame targets where a swallow is unsafe. **Measured by plan 034 (2026-10-03, this machine,
      Lib.Harmony 2.4.2, .NET Framework):** the shield's finalizer as shipped costs 64 ns and 241 bytes
      of garbage per call on one thread and 1,146 ns per call with 8 threads contending; the same
      finalizer without `__originalMethod` costs about 5.4 ns per call and no garbage as shipped
      (TAOM.Dependencies is a Debug build; 1.3 ns optimized, the figure first quoted here, corrected by
      034's review). Nearly all of the cost is that one binding. Plan 034's Branch B (reviewed) resolves
      the original only when an exception arrives, which makes the shield cheap: with it, the cost case
      for every exclusion is
      gone, and only the correctness one (`TickAgentsAndTeamsImp`) stays. Plan 041 adds three entries
      under the current rule (`WaitTickCompletion` and `TickComponents` per frame, `SpawnAgent` per
      agent); its review judged each: keep `WaitTickCompletion` (a swallowed throw there skips the wait
      loop, so the next frame's pre-tick would overlap the agent tick: a correctness reason, like
      `TickAgentsAndTeamsImp`), `TickComponents` neutral, and `SpawnAgent` the weakest case (a small
      per-spawn saving that gives up the missing-API rescue for Patch23 and any other mod's patch there,
      for every player). **The same decision covers two more plans:** plan 039 (built and reviewed): its option A (the
      default, the house rule, which it was built with) excludes five map-frame methods; three are ones
      other code already patches for every player (`Campaign.RealTick`, `MapState.OnTick`,
      `MapScreen.OnFrameTick`; a broken foreign patch there could then stop campaign time or the map UI),
      and two (`Campaign.Tick`, `CampaignEvents.Tick`) carry only the profiler. Its option B keeps the
      shield on the three; and plan 040
      (built tonight) excludes seven load-time methods, each to keep today's exception behaviour, none for
      cost. 039 was built with option A. Its review (2026-10-03) verified what option A gives up on those
      three map methods: Patch37's crash-capture finalizer runs before PatchShield's; with crash capture off,
      PatchShield strips the outer method's patches instead of the culprit's, and an exception escaping
      `MapScreen.OnFrameTick` stops the campaign's party AI task for as long as it recurs. The per-target
      table is in its review report. **Recommended: option B for those three** (keep their shield), since
      players already patch them and the shield now costs about 5.4 ns a call. The 039 review also found
      that TAOM's own Patch89, Patch36 and Patch43 postfixes reference engine members directly, so after an
      engine update their own try cannot catch a missing-member failure; option B keeps PatchShield as the
      backstop there. Two texts it found false belong with item 15's plan: `Patch37_CrashReport.cs:23-26`
      (Harmony 2.4.2 runs the highest-priority finalizer first) and `harmony-il.md:379` (TAOM's patches are
      not protected from the strip today).
    - **b. Settings that fall back silently (028).** The profiler now logs a reason line when its two MCM
      values are out of range; the provider's two older ones (stall watchdog seconds, memory sample
      interval) still fall back silently, and only a hand-edited settings file can reach them, since MCM
      clamps its own input. **Recommended: accept.**
    - **c. Hitch counts once 028 and 029 are both in (029).** `perf_runs.py` counts `[Hitch]` lines, and
      028 now writes only the first 100 per mission. **Recommended: read `hitches=` from `[TickSummary]`
      when it is present**, at the merge (small, test first). The tool's other design calls are not
      urgent: how `[TickProfiler]` warnings reach a row, exit codes for "nothing measured" and "crashed",
      where the 30 s steady anchor starts, closing a mission's span at `ExitBegin`, using `[TickSummary]`'s
      exact totals for the tick table, and whether `compare` checks diagnostics differences.
    - **d. Plan 030's cost and timing changes.** About four new INFO lines per mission (the census open
      and close, the troll and no-creatures lines), the career hit tally written only at mission end (a
      crash mid-battle loses the counts after each combination's first hit), and troll trackers that can
      start up to 0.5 s earlier. **Recommended: accept all three.**
    - **e. Two behaviour changes the 031 review proposes.** Issue #706's fix (Patch25's English overrides
      apply in every language, so 313 strings show English to every player; the fix checks the active
      language first in the localization prefix): **recommended yes, as its own branch**. A native, rebindable key for the formation cycle instead of the free-text
      MCM hotkey: your call. With it, a third the review asked: hotkey strings such as "3" or "L, K"
      parse into InputKey values that do not exist and reach the engine every frame (its handling
      unverified); rejecting them with `Enum.IsDefined` changes which settings are accepted. Pre-existing
      small items it found, for issues on your word: the nameplate
      fade provider reads its settings in its constructor, the Patch35 catch log says the feature is
      disabled when it is not, `module-dependencies.md` names MCM 5.12.1 where 5.12.3 is installed, and a
      NaN `VictimMaxHealth` counts as owned (reachability unverified).
    - **f. The review harness.** The review workflow's fix pass records a convergence round only in the
      report, so every branch's RCA and REVIEW-LOG entry missed it; I closed that by hand on every
      reviewed branch.
      **Recommended: one sentence in `review.js`'s fix prompt** asking the fix pass to update the RCA and
      the REVIEW-LOG entry too (harness code in your checkout, so not touched tonight).
    - **h. Plan 042's open choices (the XML merge fast path; reviewed 2026-10-03, two convergence
      rounds).** `lords.xslt` still costs about 1.0 to 1.1 s on each game process's first load (first-run
      JIT of the compiled transform) and 0.05 to 0.1 s on later loads in the same process: replacing it
      with a generated XML file needs a regeneration step at every engine update, **recommended: not
      now** (the review's cheaper idea, unmeasured: pre-warm the transform off the loading screen). A build step that merges
      TAOM's per-culture files into fewer files is worth much less once the fast path lands,
      **recommended: no**. The fast path has no player-facing off switch (it turns itself off on its
      own fault and stands aside for any other mod that patches the same methods); adding one moves the
      pinned settings counts, **recommended: none unless a player report asks for it**. The review's
      other calls: keep the compiled-XSLT cache (about 1.1 s saved per later load in a process, about 11 MB
      held; **recommended: keep**) or call the engine's `ApplyXslt`; drop the two never-written schema
      counters from the `[XmlMerge]` lines (a log format change); make the live equivalence harness
      opt-in, since it adds about 40 s to every unfiltered local test run, and whether `/verify-bindings`
      should run it (+45 s); and a DEBUG stack line per fallback. One has a stability edge: PatchShield
      wraps `CreateMergedXmlFile`, so a missing-API exception inside the engine's own merge would load that
      data type empty instead of crashing as the unpatched game does. Plan 040 already puts
      `CreateMergedXmlFile` on the exclusion list, so **recommended: merge 040 with or before 042**, or add
      the exclusion in 042 if it lands alone. After merge: `/verify-bindings` adds `CreateMergedXmlFile` to
      `patch-targets.md`.
    - **i. Plan 033's open items (creature battles; reviewed 2026-10-03, two convergence rounds, the
      second clean).** The hunt change (the branch's commit 4) is reverted: the saving it claimed does not
      exist on v1.5.3, because the reads it avoided are inlined field reads, not native calls.
      **Recommended: leave it reverted**, and re-land it only with a plan 028 profiler measurement before
      and after contact. Creature-tree timers move from `DateTime.Now` to `DateTime.UtcNow`, the same
      wall clock. The review proposes timing every creature-tree sleep, wait, cooldown and rage from
      `Mission.CurrentTime`, as Troll Brute Force already does, so they stop in a paused battle and slow
      down in slow motion: **your call; it changes behaviour, so its own plan if yes**. The spatial grid's
      mission-end summary and an injected logger (your logging instruction) need
      `AdvancedCombatBehavior.cs`, which plan 033 left out of scope: **recommended: a small follow-up
      plan**. Two things were examined and left as they are: the crewed elephant's per-frame skeleton
      read (`TaomHowdahMachine.cs:274`; caching the wrapper is safe only if nothing replaces a mount's
      skeleton in battle, which no evidence rules out, and reading it less often makes the platform
      step; **recommended: leave it**), and the grid's 2 s rebuild interval (shortening it changes what
      wargs and spiders see: a gameplay call). Follow-up candidates, none filed: delete the Alt-key grid
      overlay (it draws nothing in any TAOM build, Debug included: the engine's sphere call is
      `[Conditional("_RGL_KEEP_ASSERTS")]`, a symbol no TAOM project defines; yet holding LeftAlt
      re-arms grid rebuilds), retired
      trees' event listeners that are never unsubscribed, and a possible duplicate agent at a
      `GetNearbyAgentsAux` page boundary that could touch trample, Dread Aura and Signature Strikes (an
      `/investigate` first). In-game after merge: a warg battle (bites land, rage triggers), a
      creature-bandit battle (the spider hunts the nearest soldier from the start) and an elephant battle
      (trample and side-attack cooldowns feel unchanged).
    - **j. Plan 036's open items (the clip memory probe; reviewed 2026-10-03, two convergence rounds).**
      The probe ships on for every player: per mission, one native walk over the clip records each
      second and an INFO line every 5 s, plus a one-time 10 MB scan per game session. The setting is kept
      in MCM's json2 file, so the first default a release ships sticks; changing it later means renaming
      the setting. **Recommended: keep it on for the measurement builds, time the walk in the first
      session, then decide before the first public release.** A per-frame counter read (about 5 ns a
      frame) would let `drops`, min and max see a load and an eviction inside one second: **recommended:
      decide after the first battle logs.** In-game after merge: one Custom Battle, then check the armed
      header, one 5 s line, the tail line and the summary in `taom_debug`.
    - **l. Plan 041's open items (the hitch probe, on by default; reviewed 2026-10-03, two convergence
      rounds).** The review's one safety question, before the probe ships on: every frame it calls the
      engine's `IsAnyAnimationLoadingFromDisk`, which walks the clip record list with no lock, and a
      native fault there cannot be caught. Read in the binary since (`evidence/native/
      clip-list-walk-safety.md`): the walk only reads; vanilla itself calls it every frame while a
      mission's loading screen waits for clips to load (`MissionScreen.BeforeMissionTick`, v1.5.3); and
      the engine indexes the same list without a lock from about 90 places, so a list that moved during
      a battle would endanger far more than the probe. What stays an inference: that no clip record is
      added after a mission loads. **Recommended: keep it on, and let the first measurement session's
      battles confirm it.** Three smaller calls, all yours: whether to time the clip sample in place
      each frame (a behaviour change the review did not apply); the install line prints the
      bookkeeping-only cost next to "target 0.50%", which reads as the whole cost (a pinned text, so a
      test changes with it); and whether ADR-002's 150-line limit counts a partial class's files
      separately (`HitchProbeHooks` is 284 lines over three files, three files sit at 148 or 149).
      After merge: `/verify-bindings` (its patch-target list names neither Patch97 nor Patch98), and
      the `TickComponents` thread line from a real log.
    - **m. Plan 034's open items (PatchShield's per-call cost; reviewed 2026-10-03, two convergence
      rounds).** The review found and fixed one real defect: another mod patching PatchShield's own
      finalizer made the lookup name the finalizer. Your calls, recommendation in bold:
      - **The swallow line on a lookup miss: rate-limit it to once per method.** When the shield cannot
        tell which method threw, it no longer strips the throwing foreign patch, so that patch keeps
        throwing and the diag.log line repeats on every call. It changes a documented log format, so it
        is yours; the in-game check below says whether misses happen at all.
      - Port Branch B to `bannerlord-1.4.5`: an adaptation, not a cherry-pick (that line has no
        `RethrowStackPreserver`). **Recommended: after 1.5.x has played a while with it.**
      - The save-load diagnostics' finalizers (`HeaderLoadData_Readers_Patch`,
        `ContainerLoadData_Fill_Patch`) still bind `__originalMethod`, per object and per container on
        the parallel workers. **Recommended: count the calls in one save load first.**
      - With the shield at about 5.4 ns a call, revisit the co-op skip and the hot-target exclusions
        (16a); `PatchShield.cs:135` still calls it a "finalizer tax".
      - Merging the two shield finalizers renames methods you know from crash bundles. **Recommended:
        no.**
      - Pre-existing bugs it found, for issues on your word: `EarlyLog.DrainTo` has no caller, so
        TAOM.Dependencies' early log lines never reach any log; `TryUnpatchOffendingPatches` counts an
        owner that had only a finalizer as unpatched; four docs still show the old finalizer shape.
      - In game after merge: after a session with a swallow or a rethrow marker, diag.log should show no
        "could not tell which shielded method" line and no "unknown N time(s)" suffix. The Mono fix
        (Proton players) cannot run on .NET Framework; confirm it only if such a player's diag.log
        shows misses.
    - **n. Plan 037 (campaign hot paths; reviewed 2026-10-03, second convergence pass clean).** Every
      lens rejected Stage D (the caravan distance hand-off) under the simplicity criterion, so the lead
      dropped it (`b282aaf2`): the branch keeps the settings read once, the cheap filters first and the
      marketplace index. **Recommended: confirm the drop**; to bring Stage D back, revert `b282aaf2` once plan
      039's map profiler shows caravan re-thinks matter, then fix the review's F1, S1, S3 and D3 first. One
      behaviour-changing follow-up: count mounted troops with `PartyBase.NumberOfMenWithHorse` instead of a
      roster walk (a hero changing horse without a roster change would read stale until the next roster
      change); your call.
    - **o. Plan 040's open items (load-time stamps; reviewed 2026-10-03, two convergence rounds).**
      Four behaviour choices the review raised and left to you, each small: (1) write every
      per-category `[PatchApply]` line at DEBUG for every player and delete the hold-and-replay, which
      reverses two plan decisions and edits `SubModule.cs`; (2) with the toggle off, still write the
      `[Lifecycle]` dispatch total, so every player's log brackets the new-game fan-out
      (**recommended: yes**, it is one line per dispatch and answers "what took the 3 s" for players'
      logs; the review's alternative is to apply the Lifecycle category only when the toggle is on); (3)
      whether a restore fault should switch per-handler timing off for the session; (4)
      `[HarmonyPriority(Priority.First)]` on the `LoadXML` prefix, so another mod's skipping prefix
      cannot drop that call's line (changes patch order). After merge: `/verify-bindings` (seven new
      targets) and the in-game check (a new campaign and a save load, toggle on and off); if every
      `[LoadXml]` line reads `merge_ms=none`, the merge finalizer is not firing.
    - **p. Plan 032's open items (the worker-thread formation patch; reviewed 2026-10-03, second
      convergence pass clean).** The fallback log now counts every throw per mission instead of
      dropping all but the first. Your calls: keep the read-only cavalry check on Patch30's gate (two
      lenses split; kept), and accept that a laid-out formation that turns mostly cavalry keeps its
      layout until the cached flag refreshes, about 2.5 s at most (**recommended: accept**). The plan
      also left the two native queries per laid-out unit (`Scene.GetGroundHeightAtPosition`,
      `Mission.IsFormationUnitPositionAvailable`) uncached, since caching them could return a stale slot.
      The laid-out path still evaluates the formation's class-ratio queries on the worker (through
      `UnitPitch`); making those read-only is a follow-up, so the branch's thread-safety claim holds only
      for formations without a TAOM layout (item 9's draft fix). Later: build the slot cache on the main
      thread when a layout is set, retire `IFormationLayoutService` once 031 and 032 are merged (one
      implementer, no fake), and (pre-existing) Patch30 keeps its service in a static with no
      `ResetForUnload`. In game after merge: a mixed infantry and archer formation of 10 or more on Hold
      still forms its layout, the `L` hotkey cycles it, AI formations are unchanged, and a spider brood
      battle runs.
    - **q. Plan 029's follow-ups (reviewed 2026-10-03, two more convergence passes).** Two parser
      choices, both small: plan 040's bare `total` marker joins the value before it (`phase=GameInit
      total`); **recommended: fix it in 040's writer** (`scope=total`), not by bending 029's rule, which
      would also cut a unit word after a number (`vram=2.00 GB` would read `2.00`). And whether an
      unclosed bracket runs its value to the end of the line (today) and whether brackets should match by
      kind (today one shared depth): **recommended: keep both as they are** until a real log line needs
      otherwise. Two parser gaps before the measurement session: summary lines whose body starts with a
      word (036's `[AnimMem] summary:`, 040's `[LoadXml] summary`, 042's `[XmlMerge] summary`) never
      reach the tool's extra tags, and its copy of the profiler status line must be re-synced when 041
      merges. At the rewrite, rewrap `df40fee6`'s body (lines of 75 and 90 characters).
    - **r. Plan 039's other open items (map profiler; reviewed 2026-10-03, two convergence rounds).** Two
      small calls: classify the frame's speed from `Campaign.GetSimplifiedTimeControlMode()` so waiting frames
      read Stop (a behaviour change, not applied), and move the out-of-range top-N warning into the settings
      provider (outside 039's file scope; plan 028's profiler already logs its own fallback, so this would
      only gather two warnings in one place). **Recommended: yes to the first, as a small follow-up after
      merge; the second is an optional tidy-up.** In game after merge: turn on the map profiler, restart, and check the
      install line, the 5 s `[MapProfile]` lines and one `[MapProfileSummary]` at quit.
    - **s. Plan 038's open items (equipment memory audit; reviewed 2026-10-03, second convergence pass
      clean).** A content bug it found: `sk_uruk_hai_skirt_a1` is body armour placed in the Cape slot of
      `urukhai_champion` and `urukhai_berserker` (`troops_isengard.xml`), so the engine never equips it;
      **recommended: move it to Body or drop it.** Four small modelling calls, none urgent: count the skin
      eyebrow, face and tattoo assets (about 159 MB on testing, an upper bound) or keep them a named gap;
      whether a loose `Assets` copy wins over a packed one under `--loose-assets` (the plan chose packed
      first, the Armory guide says the engine does the opposite: **recommended: follow the engine**); model
      slot fit; and the `fallback first=` line's content. Owed at merge: a feature doc and its feature-map
      row for the tool.
    - **t. Plan 035's open items (the release packager's sack and JIT reports; reviewed 2026-10-03, two
      convergence rounds, both fixed).** Report only, as D8 requires: no path refuses a release, changes
      the exit code or alters what is copied. What it cannot see: the stale-format WARNING compares the
      shipped scene sacks with each other, so a whole set one format behind the engine raises nothing,
      and #448's own case (three module-level `Shaders/D3D11` sacks) is never read at all. Your calls:
      (1) whether D8's "I don't ship them" covers the 52 to 53 editor scene sacks every channel carries
      and the about 166 MB of module-level sacks the packager would copy from the dev install, which the
      report never mentions; **recommended: report module-level sacks too (one line each, still never
      refusing)**, since that is the only way a #448 repeat shows. (2) Compare scene sacks against vanilla
      Native's under `--source`; useful only together with (1). (3) Print `ON (no DebuggableAttribute on
      the assembly)` instead of a bare `ON`, which today looks like a misparse; **recommended: yes**, one
      line. (4) The plan's "and its log" has no target: the packager prints to stdout only. Your answer
      to (1) also decides a stale-text sweep the review lists: `shader-precompilation.md` lines 5, 130,
      140, 146 and 253, `precompile_scenes.txt`, `PrecompileSceneProvider.cs` lines 18 and 19,
      `v1.4.8-impact.md` line 64, and #448's premise. Neither of the issue draft's labels exists in
      the repo (item 9). Commit `893f9428`'s body says five
      reader slips read ON; four did (the next commit's body corrects it; a rewrite at merge can too).
      Found on disk on 2026-10-03, to clean before the next packaging: testing's
      `TAOM_Map/SceneObj/Backups` (116 MB), the old `BehaviorTreeWrapper.dll` and `BehaviorTrees.dll` under
      `TAOM/bin` in patreon and public, three temp or wip scenes in every channel, and 9 `.xml.bak` files in
      testing's Armory ModuleData (the last found by the 038 review). And under (1): if module-level sacks
      are not to ship, the packager should also stop copying them (about 166 MB from the dev install).
    - **k. A measurement gap on the campaign map, no action yet.** The `[MapLoad]` heartbeat's `tickMs`
      is a five-second average, so a single slow tick (a day rollover running every TAOM daily
      handler) never shows in the logs on disk. Plan 039 adds the worst frame per window; if the first
      map session shows day-rollover spikes, **recommended: a small follow-up plan that times each
      hourly and daily handler**, reusing plan 040's listener swap (039 lists it as deferred).
    - **g. For the merge, no decision needed:** the settings counts (028 adds three settings; 036, 039, 040
      and 041 one each: `SettingsFingerprintTests` and the co-op docs' totals), 028's "three data literals" line
      once 029 lands, the two animation adapters both 036 and 041 add (keep the first merged), and commit
      messages that break the spec: a body line over 72 characters, a trailer block that does not parse
      (unindented `Not-tested:` continuations) or no trailers at all. The 2026-10-03 re-check found them in
      most branches' commits, far beyond the list first given here, plus program-docs commits whose
      subjects pass 72, so the history rewrite before merge (the only fix) should re-run the spec check
      over every commit; `evidence/commit-spec-check.txt` holds the full result. Six branches carry commit bodies with a false claim, which the rewrite should
      correct, since `/release` builds the release note from commit bodies: 036 (its review report's NEEDS
      MIKE 4: the `2f560c6c` trailers and first sentence, two sentences of `78f654d9`), 033 (the reverted
      hunt commit `d45774a5`, whose body states the false cost premise), 032 (`a00363cc`'s `Constraint:`
      trailer: the laid-out path still evaluates the query group on the worker), 039 (`5714149b`:
      `SiegeEventManager.Tick` follows party movement in `RealTick`), 030 (`494eec47`: "no log line
      changes") and 035 (`893f9428`: five reader slips read ON, where four did); 039's, 030's and 035's
      carry a correction in a later commit's body. The merge order and every conflict, for all 15 branches since the 2026-10-03 afternoon probe:
      `evidence/merge-map.md`: the program branch, then 028, 039, 041, 036, 040, 042, 029, 034, 030, 031,
      032, 033, 035, 037, 038. It has 76 conflicted files; 41 of its 87 hunks are mechanical appends to
      REVIEW-LOG.md and the lessons files, and the steps for 041, 036, 040 and 032 need hand resolutions,
      written out there. In this order neither `Main/SubModule.cs` nor `Main/IoC.cs` conflicts (042 before
      040 would conflict in `SubModule.cs`). The map also names three spots where plain keep-both is wrong
      and the settings recount (16 properties). Trunk's two new commits add one append-only lessons
      conflict. The integration trial (D9) replays this order. One overlap no text conflict
      shows, and neither review examined: 040 and 042 both patch `MBObjectManager.CreateMergedXmlFile` (042
      replaces the merge in a prefix, 040 observes in a finalizer and also puts the method on PatchShield's
      exclusion list, see 16h); on the first merged build, check that the `[LoadXml]` lines still carry
      merge times with 042's fast path on. Owed after the merges: one `/verify-bindings` on the merged tree
      (the reviews of 028, 030, 039, 040, 041 and 042 each list it), and the "AGENTS.md lessons (pending)"
      the review reports end with, folded into `.ai/review-reference.md`'s "Look harder here" list (none
      were folded during the run).
    - **u. Owed after the merges, nothing to decide now.** In-game checks for the plans this item does
      not already cover (030, 031, 037, and 028 with 041: profiler and probe off against on, `avgMs`
      within noise): each plan file's "After merge" section names the lines to look for. The reviews of
      028, 030, 032, 033 and 037 found pre-existing defects too, as 031's and 034's did (16e, 16m); each
      report's FOLLOW-UP section lists them for issues on your word, and one is a recommendation for
      single-owner `IoC.cs` (028: `IGraphicsOptionsAdapter`'s registration). The 036 review found that
      `tools/native_sig_author.py xref` stops decoding at RVA `0x48B38` and reports zero references without
      a warning: worth an `/investigate` before the tool is trusted again. And two REPORT findings have no
      plan yet: M8 (the 7.6 GB main-menu floor, partly named) and C7 (PatchShield's pass 2 at every game
      start, 1 to 3 s on player machines).

## Detail

### The first measurement session (after plans 028, 029, 036 and 041 are in a build)

1. Turn on the tick profiler (MCM page Battle Load Diagnostics, group Mission Performance, Enable Tick
   Profiler; off by default, it takes effect after a restart), then restart the game. The anim memory probe (036) and the hitch probe (041) ship on by default.
2. Check the game is not frame-capped outside the engine (most battles on disk plateau at 116 to 117
   fps, an overlay or driver cap, though the 2026-10-02 14:30 battle ran at about 256 fps, so the cap
   comes and goes). Close other memory-heavy programs (one 2026-10-02 session ran at
   82 to 84% memory load).
3. Custom Battle, the same scene each time (pick it by id: trunk's `d9a8f46f` rewrote the field-battle
   scene list on 2026-10-03), 500 against 500, field battle, spring, noon, F6 at once.
   Run A: two infantry cultures with no creatures. Run B: the same with a troll and a warg company.
   Run C: Harad with crewed mumakil. Three runs each, five minutes each, quitting between runs.
4. `python tools/perf_runs.py <the session's taom_debug log>` gives one row per battle;
   `python tools/perf_runs.py compare --a <logs> --b <logs>` gives the A/B.
5. What the numbers decide: the top TAOM behaviours by ms per second (which plans matter most), the
   hitch frames' phase (TAOM code, the agent tick, the main thread waiting for it, or native otherMs),
   and whether clip loading coincides with the hitches.
6. Campaign map soak (memory): load a mid-game save, open no menus, and leave the map running 30 minutes
   at normal speed, then 30 at fast forward. The `[MemSample]` lines then say whether the map leaks
   slowly (the logs on disk hold only two 16-minute stretches; the 2026-09-12 audit saw 60 to 100 MB a
   minute over five minutes at normal speed).
