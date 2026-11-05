# Deep-dive: XML data and asset reference integrity (1.4.x line on Bannerlord v1.4.8)

Run id: 2026-11-04-port148. Lens: XML data and asset reference integrity (mesh ids, prefabs, action sets, schema).
Machine: maintainer LAPTOP (cooked TAOM_Map / LOTRLOME_Armory installs). Working tree = c5b84fb41.
Read-only investigation: no source edits, no write-mode tools.

Findings are appended as they are confirmed. Ids: XML-DATA-NN.

## Findings

### XML-DATA-01 (P1): no guard on the missing-collision-body infinite wait; the Harad freeze stops exactly where it runs

**Evidence**
- v1.4.8 `PreloadHelper.WaitForMeshesToBeLoaded` (taom-src, `C:\Users\mikew\.taom-src\v1.4.8\TaleWorlds.MountAndBlade.View.PreloadHelper.cs:58-75`) is a `do { ... Thread.Sleep(1); } while (num != 0)` with no exit. Each pass adds 1 for every name in `_uniqueDynamicPhysicsShapeName` (field at :14, `HashSet<string>`) whose `PhysicsShape.GetFromResource(name, true)` is null.
- `RegisterPhysicsBodyUsageIfValid` (:230-236) adds any non-blank `CollisionBodyName` / `BodyName` / `HolsterBodyName` (:160-162) without checking that the shape exists. A missing VISUAL mesh is harmless here, because `MetaMesh.GetCopy` returns null and `RegisterMetaMeshUsageIfValid` (:221-227) drops it.
- Callers in the v1.4.8 dump: `SandBox.View.MissionPreloadView.OnSceneRenderingStarted` (`C:\Decompiled_Bannerlord\_modules_build\SandBox__SandBox.View.cs:4976-4979`, preloads every troop of every party in `MapEvent.PlayerMapEvent`, :4955-4967), `ArenaPreloadView` (:6593-6628), `MissionCustomBattlePreloadView` (`Native__TaleWorlds.MountAndBlade.View.cs:26731-26761`), and the SandBox.GauntletUI education screen (`SandBox__SandBox.GauntletUI.cs:1575`).
- `MissionScreen.OnFrameTick` (`Native__TaleWorlds.MountAndBlade.View.cs:16468-16487`) calls `OnSceneRenderingStarted` on the main thread once the scene is ready to render, the loading-frame countdown has run and `AreViewsReady()`; :13189-13196 then dispatches every view's `OnSceneRenderingStarted`.
- E3 (Harad player): `FinishMissionLoadingDone` at +7909 ms (log line 217), then `WaitingForRender` at +7911 and +10168 ms (lines 218-219), then only `[MemSample]` until 20:04:22. `WaitingForRender` is emitted at 1 Hz from a `MissionState.OnTick` postfix (`Main/Features/BattleLoadDiagnostics/Hooks/MissionState_OnTick_RenderWait_Patch.cs:52-75`, working tree), while `[MemSample]` runs on a `System.Threading.Timer` (`MemoryPressureSampler.cs:54`). Ticks stopping while the timer keeps going means the main thread stopped ticking.
- The encounter repeats deterministically: E3 line 17 warns that the PREVIOUS session's load of the same scene `battle_terrain_031` also never reached playable; lines 177-178 show the same scene against "Gondor Soldiers" (enemy=79, main party=220).
- The scene is not the problem: `battle_terrain_031` exists in `SandBoxCore/SceneObj` on the laptop, and all 161 `sp_battle_scenes` rows (80 SandBox, 81 TAOM; `Campaign.InitializeScenes` appends every active module's file, `Campaign.cs:1328-1345`, `GameSceneDataManager.cs:76-150`) resolve to SandBoxCore scenes.
- The 1.4.x line has no guard: `git grep WaitForMeshesToBeLoaded` on the working tree and on origin/bannerlord-1.4.5 finds only comments in BattleLoadDiagnostics.
- The stall watchdog cannot catch it in the player's window: it fires only after `StallWatchdogSeconds` (default 300, `BattleLoadDiagnosticsSettings.cs:36`) from window open (`BattleLoadStallWatchdog.cs:155-156`), and E3 ends about 100 s after the window opened.

**Mechanism.** One `body_name` / collision body that no loaded tpac ships pins the main thread forever, right after the loading screen drops. CPU stays low (`Thread.Sleep(1)`), and the window pump and audio threads keep running. That matches "freezes ... Task Manager looks normal" and the shape of E3. Two other explanations fit E3 equally well: a cold shader-cache flood (2,697 shaders in flight) and a mesh-side `MetaMesh.CheckResources` stall, which sits in the same loop. **E3 alone cannot discriminate the three.**

**Is the public Armory affected?** UNVERIFIED. The laptop's cooked Armory is v2.0.26 (09-07), not the 2026-09-29 publish players run (XML-DATA-02). On that older cooked copy `validate_mesh_refs.py --scan-bodies` finds 0 missing bodies among 334 unique body refs and 0 missing visual meshes (3 accepted `KNOWN_DEAD_MESH`). The desktop's loose-Assets audits on 1.5.x (`docs/audits/armory-ref-audit.md` at 799e189e8, run 2026-09-28, and 98ca32e0c, run 2026-09-30) are CLEAN. Those audits read the desktop's loose tree, though, not the cooked packs players get. #633 (1.5.x `docs/features/preload-body-guard.md`) records a player hang that every desk gate passed, with "packs older than the XML" as the leading hypothesis.

**Narrowing for the E3 battle.** E3 line 56 shows MainHero `Marzuk`, culture `aserai` (Harad); line 178 shows `Marzuk Party*:h220/a220` against `Gondor Soldiers:h79/a79`. So the preload set is the public branch's Harad and Gondor troop trees.
- Of the 75 distinct item ids in the public `troops_harad.xml`, 70 exist on the laptop with all bodies resolving. The 5 absent ones are `ladder_harad_bow_t2`..`t6`.
- Of the 434 in the public `troops_gondor.xml`, 80 are absent on the laptop (per-tier `ladder_*` bows, `numenorean_bastard_elite_*` and others).
- Ladder bows are `copy.deepcopy` clones of a donor (98ca32e0c `tools/generate_ranged_ladder_items.py:94-100`), so they carry the donor's mesh and body. The 09-30 spec's donors (`wm_harad_bow_a01`/`a02`, `wm_gondor_bow`, `gondor_steel_bow`/`_b`, `crossbow_c`, `wm_ithilien_bow`/`_b`/`_c`) all exist on the laptop, and their bodies (`bo_cap_wm_harad_bow_a01`/`a02`, `bo_cap_wm_ithilien_bow`, `bo_short_bow_a`, `bo_cross_bow_heavy`) are all in its cooked packs.

The unverifiable remainder is the **57 non-ladder Gondor items** the laptop lacks (of the 80 absent, 23 are ladder bows): 26 `sk_gd_lam_*` (the #669 Lamedon kit), 9 `sk_gd_rin_*`, 4 `wm_gondor_light_*`, 3 `wm_gondor_spear_*`, 10 Numenorean bastard/greatsword/poleaxe weapons, 2 `sm_gd_shield_*`, 2 `wm_gondor_lossarnach_*` and 1 `wm_gondor_lamedon_*`. The 26 `sk_gd_lam_*` are the Lamedon kit that 998f054c0 ported from KEYforce's drop on 2026-09-25.

Armour cannot trigger this hang. In the laptop Armory none of the 2,948 BodyArmor, Cape, HandArmor, HeadArmor, LegArmor, HorseHarness and Horse items carries a body reference, while all 224 shields, 35 bows, 3 crossbows, 28 arrows and 2 bolts do; crafted weapons carry bodies through their crafting pieces. So the 35 `sk_gd_*` items drop out.

**The candidate set is 22 Gondor weapons and shields:** `wm_gondor_light_*` (4), `wm_gondor_spear_*` (3), Numenorean bastard/greatsword/poleaxe (10), `sm_gd_shield_*` (2), `wm_gondor_lossarnach_*` (2) and `wm_gondor_lamedon_*` (1). Check their `body_name`s, and their crafting pieces' bodies, first on a machine with the public Armory.

**Fix direction.**
1. Port Patch90 PreloadBodyGuard: a prefix that drains unresolvable body names after a bounded 5 s poll, logs `[PreloadGuard]` at ERROR, and returns true so vanilla's loop runs on a set that can empty. Its claim "same shape on v1.4.8" checks out against the dump above (field name and type identical).
   - Drop the 1.5.x-only `[ClientOnly]` attribute (absent on 1.4.x: `git grep ClientOnlyAttribute -- Main` is empty).
   - The `IoC.cs` and `SubModule.cs` registration belongs to the orchestrator.
   - Keep the binding test, which pins the field name against the installed engine.
2. Ask the Harad player for the matching `rgl_log` (`C:\ProgramData\Mount and Blade II Bannerlord\logs`). The shipping native DLL can print `get_object failed for body: %s` (string present in `bin\Win64_Shipping_Client\TaleWorlds.Native.dll`), and E1/E2 (another player) have none.
3. Verify the public Armory's cooked packs against its own XML on a machine that has the 2026-09-29 publish: `python tools/validate_mesh_refs.py --scan-bodies` (Tier C reads cooked TOCs).

**1.5.x commits:** fb8d73145 (Patch90, #601), 6d7e67f3f (co-op client-only marking, do not port). Desk side: 05bd0a730 (`tools/audit_armory_refs.py`, #599). See XML-DATA-06 for the validator side (`MISSING_COLLISION_BODY`, `COLLISION_BODY_BORROWED`).

**Verify how.** Unit tests: port `PreloadBodyGuardServiceTests` and `PreloadBodyGuardBindingTests` (the binding test runs on the laptop's 1.4.8). In-game on the laptop: put a bogus `body_name` on a starter item in a scratch Armory copy and start a battle. Expect one `[PreloadGuard]` line and a load after about 5 s.

### XML-DATA-02 (P2, process): the laptop's data is half trustworthy. TAOM_Map is complete; the Armory is an old v2.0.26 copy

**Evidence**
- `python tools/validate_moduledata.py` (working tree c5b84fb41, 41 s) swept `Main/_Module/ModuleData` plus `Modules\LOTRLOME_Armory\ModuleData` and `Modules\TAOM_Map\ModuleData` (its own header). Registry: 5,900 items, 5,291 NPCCharacters, 40 cultures, 495 party templates, 121 body properties.
- Result: **3,423 errors, 117 warnings.**
  - Errors: `BROKEN_ITEM_REF` 3,423.
  - Warnings: `INCONSISTENT_ARMOUR_SLOT` 94, `UPGRADE_ARMOUR_REGRESSION` 9, `CROSS_CULTURE_ARMOUR_INVERSION` 9, `FORTIFICATION_WITHOUT_VILLAGE` 3, `GENERATOR_RETIRED_ITEM_REF` 2.
  - **0 `LANDLESS_CULTURE` and 0 `SETTLEMENT_ECONOMY_FLOOR`**, against 414 and 90 on 2026-09-06 (`docs/reference/development-machines.md`).
- The 3,423 refs point at 220 distinct ids:
  - 2,744 sit in the three player starter-roster files (`taom_char_creation_equipment.xml` 2,230, `taom_player_start_vanilla_override.xml` 280, `taom_career_starting_equipment.xml` 234), all to 127 `starter_` ids.
  - 679 sit in the 16 troop files, mostly generated `ladder_*` bows plus Gondor Lossarnach/Lamedon and Numenorean weapons.
- The installed Armory is `<Version value="v2.0.26" />`, SubModule.xml and pack0-8.tpac dated 2026-09-07.
  - It defines 79 `starter_` ids and **0 `ladder_` ids**.
  - It has no `Monsters/LOTR/lotr_monster_elk.xml` or `lotr_monster_animalia.xml`, both of which the players' Armory loads (E2 rgl lines 4026-4040).
  - 92a30ba42's body dates the players' Armory as "the 2026-09-29 Armory publish".
- TAOM_Map installed: `v2.0.27`, dated 2026-09-07, 49 ModuleData files including the live `settlements.xml`. It loads 141 prefab files, the same count the players' client loads (E1/E2 rgl).
  - It is complete in file set but content-stale: `FORTIFICATION_WITHOUT_VILLAGE` fires for `town_EW10`, `town_EW11` and `castle_G4`, which `.claude/rules/moduledata-validation.md` records as fixed on the desktop (#597, and `castle_G4`'s villages placed 2026-09-13).

**Mechanism.** The 2026-09-06 numbers came from a TAOM_Map with no `settlements.xml`. That gap is closed, so culture and scene verdicts on this laptop are trustworthy; settlement-binding verdicts reflect the 09-07 map. The Armory is not representative of either reference point:
- it lacks the desktop's generated `ladder_` and `starter_` items, so it cannot judge the working tree;
- it predates the 2026-09-29 publish, so it cannot judge the public build either.

Every item, mesh and body verdict on this laptop is therefore a verdict about v2.0.26 only. The same artefact makes `audit_polearm_shield_parity.py` FAIL on 8 "stale" ratchet entries, all naming `starter_wm_mordor_set1_polearm_a01`, an id this Armory lacks.

**Fix direction.**
1. Install the public 2026-09-29 Armory (cooked) on the laptop, or a desktop live copy, before trusting any item verdict here.
2. Have `validate_moduledata.py` print each swept module's `SubModule.xml` `<Version>` and newest file date in its header. The current header names the roots but not their vintage, which is exactly what this run needed.
3. Update the CLAUDE.md "laptop is incomplete" trap: TAOM_Map is now complete; the Armory is stale.

**1.5.x commits:** none found that prints module versions in the validator header. 05bd0a730's audit writes a dated report but reads the desktop's loose tree. **Verify how:** re-run the validator after an Armory refresh; expect `BROKEN_ITEM_REF` near 0 for the public branch (92a30ba42 reports 0 against the release modules).

### XML-DATA-03 (P1, release integrity): origin/bannerlord-1.4.5 does not contain the public troop tree (#697)

**Evidence**
- `git merge-base --is-ancestor 8df90444c origin/bannerlord-1.4.5` is false; the merge base is c5b84fb41.
- The 1.4.x tip's 16 troop files name the same 2,562 distinct item ids as c5b84fb41. The 5 origin commits touch only `troops_isengard.xml` among troop files, with no id change.
- The public branch names 2,617 ids. **156** ids in the 1.4.x troop files appear nowhere in the public troop tree: 76 `ladder_*` bows, Lamedon (`wm_gondor_lamedon_*`), Swan/Pelargir, Dunland Caerdh and Wulf lord/elite armour, Numenorean bastard swords, Uruk lord chests, `lrd_horse_armour_5`, `mordor_horse_armour_a`, among others.
- 92a30ba42's body: the 2026-09-29 Armory "retired 159 item, piece and monster ids the 1.4.5 troop data still named ... Against it this branch had 589 broken item refs, so archers would have lost their bows". The 589 figure is the maintainer's 09-30 measurement; I could not re-measure it on this laptop.
- Conversely, the public branch lacks the 5 origin commits (1b00d1acd, ccef172ad, d906fc921, 55d779012, f8c9c8ecb).

**Mechanism.** In v1.4.8 an undefined item id is an empty slot, not a crash or hang:
- `Equipment.DeserializeNode` uses `MBObjectManager.Instance.GetObject<ItemObject>` (`TaleWorlds.Core.Equipment.cs:204-223`), which returns null for an undefined id;
- `IsItemFitsToSlot(slot, null)` returns true (:445-450), so the slot is set to an empty `EquipmentElement`.

So a release cut from origin/bannerlord-1.4.5 against the public Armory would ship bowless archers and naked lords, silently. Any port work based on origin/bannerlord-1.4.5 inherits the same regression.

**Fix direction.** Merge `fix/public-armory-ids-145` into `bannerlord-1.4.5` FIRST, before any port package lands. Expect conflicts in `troops_isengard.xml` (1b00d1acd's Uruk fix), `CHANGELOG.md` and `.serena/project.yml`. Then re-run `validate_moduledata.py` against the public Armory (needs XML-DATA-02's refresh).

**1.5.x commits:** 92a30ba42 took the 1.5.x tree as of 2026-09-30 (#617 per-tier bows, #669 Lamedon swaps, 998f054c0 fallback). Later 1.5.x troop changes are content (OUT). **Verify how:** `git log origin/bannerlord-1.4.5..origin/fix/public-armory-ids-145` empty after the merge; validator BROKEN_ITEM_REF 0 on a machine with the public Armory.

### XML-DATA-04 (P3): "The 'taom_body_length' attribute is not declared" is harmless noise from the public Armory's 1.5.x monster files

**Evidence**
- E2 rgl: 36 errors, 3 per game init over 12 inits (first-error timestamps from 17:22 to 20:33). Each init reports `lotr_monster_animalia.xml` Line 26 (Value 100) and Line 33 (Value 150), and `lotr_monster_elk.xml` Line 45 (Value 110), validated against `../../XmlSchemas/Monsters.xsd` (E2 lines 4026-4040). E1 shows the same 3 once.
- The engine logs the error and still loads the file. `MBObjectManager.LoadXmlWithValidation` (`C:\Decompiled_Bannerlord\_categories_v1.4.8\Core\TaleWorlds.ObjectSystem\TaleWorlds.ObjectSystem\MBObjectManager.cs:1061-1129`) attaches `ValidationEventHandler` (:1324-1341), which only `Debug.Print`s, and the document still loads (:1111-1122).
- `Monster.Deserialize` (`TaleWorlds.Core.Monster.cs:189` onward) reads attributes by name, so the extra one is ignored.
- Nothing on the 1.4.x line reads it: `git grep taom_body_length` is empty on the working tree and on origin/bannerlord-1.4.5. On 1.5.x it is the MonsterSize feature (#646, `Main/Features/MonsterSize/MonsterSizeConfig.cs:15`), and 1.5.x docs call the line expected ("one expected 'taom_body_length ... not declared' line per sized Monster", `docs/ai-includes/quadruped-pack-to-horse-skeleton-workflow.md:182`).
- The 1.4.x public build keeps no troop on these monsters: 10a52fb3d put the two Mirkwood elk riders back on horses.

**Fix direction.** None in game. Document it as expected noise in the log-triage docs, so a reader does not chase it. A schema checker (XML-DATA-07) should allowlist it.

**1.5.x commits:** none needed. MonsterSize (#646) is a 1.5.x feature (OUT). **Verify how:** read-only; the citations above.

### XML-DATA-05 (P3): the prefab budget gate hardcodes the desktop path and counts one module; the cap is an editor hazard, not a player one

**Evidence**
- `tools/check_prefab_budget.py:22-25` defaults to `E:\...\TAOM_Map\Prefabs` and does not use `tools/_gamedir.py`. On the laptop it prints `ERROR: not a directory` and returns 2 (:36-38).
- With `--dir`: TAOM_Map 93,830 entities in 141 files, "OK" against warn 120,000.
- Summing every installed module with the same regex: Native 23,828; NavalDLC 9,867; SandBox 2,847; LOTRLOME_Armory 48; StoryMode 39; TAOM 9; TAOM_Map 93,830. **Total 130,468 of 131,072 (604 spare).** The players' mod list (E1/E2 command line) excludes NavalDLC, which leaves about 120,601.
- The cap and its assert strings (`rglConcurrentQueue.h`, `chunk_index < max_chunk_count`, `prefab_init_data`, `rglEntity_reader_writer.cpp`) are present in `bin\Win64_Shipping_wEditor\TaleWorlds.Native.dll` and **absent** from `bin\Win64_Shipping_Client\TaleWorlds.Native.dll`. `docs/investigations/editor-rglconcurrentqueue-assert-2026-07.md` measured it on the wEditor build only.
- The player's client loads all 141 TAOM_Map prefab files in under 2 s, and the whole prefab phase in about 23 s (E2 rgl lines 2883-3747).
- The file is identical on 1.5.x (no diff c5b84fb41..origin/bannerlord-1.5.x).

**Mechanism.** For the Modding Kit, the margin depends on whether NavalDLC is loaded; the gate counts only TAOM_Map and so reports OK at 99.5% of the cap. For players there is no evidence of a cap in the shipping client, and prefab loading completes.

**Fix direction.**
1. Resolve the default through `_gamedir.game_dir`.
2. Add `--all-modules` (or make summing the default) over the module set the editor actually loads.
3. Print the per-module breakdown.

This is not a candidate cause for the player freezes. **1.5.x commits:** none (53a4e4e52 and 69b47d00b are Gauntlet UI prefab tests, a different "prefab"; see XML-DATA-08). **Verify how:** run the patched gate on the laptop; expect a 130,468 total with NavalDLC loaded.

### XML-DATA-06 (P2): the desk-side body and mesh gates from #599, #622 and #633 are absent on 1.4.x

**Evidence**
- On the working tree, `git grep` finds none of `MISSING_COLLISION_BODY`, `COLLISION_BODY_BORROWED`, `ARMORY ART DRIFT` or `--check-name` under `tools/`, `.claude/` or `Main/`.
- `tools/audit_armory_refs.py`, `docs/features/armory-ref-audit.md` and `docs/audits/armory-ref-audit.md` exist only on origin/bannerlord-1.5.x.
- The 1.4.x `validate_mesh_refs.py` already has the engine of all of them (`extract_refs`, `tpac_paths_for_modules`, `build_present_set`, `classify`; Tier C reads PhysicsShapes from cooked TOCs). It lacks only `module_tpacs`, `find_mesh_name_owners` and `check_names` (diff c5b84fb41..origin/bannerlord-1.5.x), and it runs only when someone remembers to.
- What 1.5.x added:
  - 05bd0a730 (#599): `audit_armory_refs.py`, one command, committed dated report.
  - ef6afeb67 and 332f2ac71 (#622): a `MISSING_COLLISION_BODY` (ERROR) / `MISSING_VISUAL_MESH` (WARNING) pass inside `validate_moduledata.py`. 332f2ac71 records that the pass existed for days with `main()` never calling it, and adds a structural test that every `*_issues` pass is called.
  - b23a27ef4 (#633): `COLLISION_BODY_BORROWED`, a body that is another kit's `bo_<mesh>` twin. It resolves, so every "does it resolve" gate passes it, and it is tied to someone else's art.
  - 7bbe6f4de: `--check-name`, is a new mesh name free across loose and cooked trees.
  - d5986f87f: retires the three troll `KNOWN_DEAD_MESH` entries after the Armory dropped the items on 2026-09-22.
- The 1.5.x body pass reads `_ART_MODULES = ("LOTRLOME_Armory", "Native", "SandBoxCore", "SandBox")` and only byte-scans packs whose TOC failed to parse (the 45 s hook budget, 332f2ac71).
- On the laptop, `validate_mesh_refs.py` warns that SandBoxCore ships no tpac at all, yet 3,871 of 3,874 Armory visual meshes resolve. The 3 misses are the accepted troll meshes.

**Mechanism.** XML-DATA-01's guard protects players. These gates catch the defect before it ships, which matters on this line because the Armory is unversioned and re-synced from the shared art tree that 1.5.x keeps changing.

**Fix direction.**
1. Port the validator pass (ef6afeb67 body pass plus the 332f2ac71 wiring and structural test) into the 1.4.x `validate_moduledata.py`, adapting `module_tpacs` (take it from 7bbe6f4de, or use `tpac_paths_for_modules`). Add its code to the commit hook's `--code` list; `CommitGateCoverageTests` fails otherwise.
2. Port `COLLISION_BODY_BORROWED` (b23a27ef4, validator part and `tools/tests/test_collision_body_borrowed.py` only). Re-derive `_SHARED_BODY_BY_DESIGN` against the public Armory.
3. Port `audit_armory_refs.py` at its **05bd0a730** shape, not the tip. The tip (12afef4d2, f1675c058) calls 1.5.x-only companions (`build_ds_skeleton_pack.py`, `wire_hill_troll_race.py`, `register_armory_names.py`, `bind_orc_goblin_locomotion.py`, `audit_fbx_lods.py`, `CREATURE_GEAR_OBTAINABLE`). Its 05bd0a730 dependencies all exist on 1.4.x: `generate_armory_catalogue.py`, `generate_starter_kit.py`, `generate_ranged_ladder_items.py`, `audit_deleted_mesh_impact.py`, `docs/reference/armory-catalogue/catalogue.tsv`.
4. Take d5986f87f only after confirming the public Armory dropped the troll items.

**1.5.x commits:** 05bd0a730, 30abf550b (tests), ef6afeb67, 332f2ac71, b23a27ef4, 7bbe6f4de, d5986f87f. **Verify how:** `python -m pytest tools/tests/test_validate_moduledata.py tools/tests/test_collision_body_borrowed.py` (python tests, no dotnet). Then `python tools/validate_moduledata.py --code MISSING_COLLISION_BODY` on the laptop (expect 0 on the v2.0.26 Armory, matching the `--scan-bodies` run above).

### XML-DATA-07 (P3): no XSD gate on 1.4.x; the 1.5.x tool runs unchanged against 1.4.8 and finds the repo clean

**Evidence**
- `tools/validate_xml_schemas.py` does not exist on the working tree. On 1.5.x it arrived with fa8c06f82 (#621) and was extended by 709649c3f, 48d66b59a, c1ce78207, bc30b76da, 09f3126a7 and f1675c058.
- I exec'd the 1.5.x source read-only (`git show` into `exec`, `__file__` pointed at the repo's `tools/` so the 1.4.x `_gamedir.py` resolves) with `--live` against the laptop's 1.4.8 `XmlSchemas`. Result: **214 files validated, 1 failed, 0 registrations resolve to nothing**, plus `NO SCHEMA` for one ModuleSounds file.
  - The failure: the laptop Armory's `LOTRLOME_items/LOTRAOM_horses.xml` L441, L470, L510, `family_type` on `<Horse>` (monsters `taom_war_elephant`, `taom_mumakil`, `chariot`).
  - v1.4.8 `HorseComponent.Deserialize` (`TaleWorlds.Core.HorseComponent.cs:147-160`) never reads `family_type`, so the attribute is inert.
  - The players' logs show no such error: E1/E2 hold only the 3x and 36x `taom_body_length` XSD errors. So the public Armory dropped it and is otherwise XSD-clean in managed loading.
- The engine validates on load but only prints (XML-DATA-04 citation), so an XSD defect is silent in game. The tool's docstring cites `clan_umbar_3` missing `initial_home_settlement`, one half of the #374 CTD.

**Mechanism.** Nothing on 1.4.x checks data against the engine's own schemas. Today the data is clean, so this is a regression gate, not a fix.

**Fix direction.**
1. Port `validate_xml_schemas.py` and `tools/tests/test_validate_xml_schemas.py` at the tip, plus the `SCHEMA_INVALID` pass in `validate_moduledata.py` (fa8c06f82).
2. Re-check the TAOM-attribute allowlist (`(schema id, element) -> attributes`) against 1.4.8 XSDs.
3. Allowlist `taom_body_length` on Monster (XML-DATA-04).

The tool's engine claims cite the v1.5.3 decompile. `LoadXmlWithValidation` and the `_replaceWhileMerging` injection are confirmed identical in shape on v1.4.8 (`MBObjectManager.cs:1061-1129`, :1092). The rest (`GetMergedXmlForManaged`, `HandleXsltList`) is UNVERIFIED on v1.4.8 and needs a pass over the dump.

**1.5.x commits:** fa8c06f82, 709649c3f, 48d66b59a, c1ce78207, bc30b76da, 09f3126a7, f1675c058 (validator-codes part only). **Verify how:** the read-only exec above is the acceptance run; after the port, `python tools/validate_xml_schemas.py --live` should print the same 214/1 on the laptop.

### XML-DATA-08 (P3): Gauntlet prefab insert gates (69b47d00b, 53a4e4e52) port with an assembly-list change; current 1.4.x inserts pass

**Evidence**
- 69b47d00b adds `TAOM.Tests/Infrastructure/PrefabInsertPositionTests.cs`, checking every UIExtenderEx insert against three silent-misplacement rules:
  - a `Child` insert with no `Index` lands first;
  - a `Child` insert must select a `Children` element;
  - `Append`/`Prepend` on a `Children` element is never loaded.
  It reads TAOM.dll and TAOM.Client.dll; TAOM.Client is 1.5.x-only.
- 53a4e4e52 fixes path case in `TAOM.Tests/Infrastructure/GauntletStretchInCoverRatchetTests.cs`, a test that exists only on 1.5.x (created e6dbea3e0).
- 1.4.x has 5 insert patches:
  - `CareerEnergyBarPrefab` (Child, no Index, `CareerEnergyBarPrefab.cs:24-28`; 69b47d00b only states Index 0, the position it already takes);
  - `CareerButtonPrefab`, `MessengerEncyclopediaPrefabExtension` and the TimeAcceleration patches (Append or SetAttribute on widgets, none on a `Children` element);
  - `SpecialResourcePrefab` (Replace).

**Mechanism.** These are UI-XML placement gates: a misplaced insert silently drops a widget. Not a load hazard.

**Fix direction.** Port `PrefabInsertPositionTests` with a single-assembly list. Skip 53a4e4e52 unless the stretch-in-cover ratchet (e6dbea3e0 and its follow-ups) is ported too. Lowest priority of this lens.

**1.5.x commits:** 69b47d00b, 53a4e4e52, e6dbea3e0. **Verify how:** dotnet test in an executor worktree (not in this run).

### XML-DATA-09 (P3): generated-block upsert on 1.4.x can corrupt a file; f963c6188's guard ports cleanly

**Evidence.** `tools/generate_new_faction_kingdoms.py:97-102` (working tree) is the pre-#947 shape:
- It deletes every `MARKER:BEGIN ... MARKER:END` span with a non-greedy DOTALL `re.sub`, then inserts at `text.rfind(close_tag)` with no check.
- With the closing tag absent, `rfind` returns -1 and the block lands one character before end of file.
- A lone `BEGIN` ahead of a real region makes the non-greedy match swallow every row between them.

1.5.x replaced this with `tools/_marker_region.py` (f963c6188, after bd457ee6e): replace in place, refuse two regions, a lone marker, or no closing tag. `tools/insert_new_factions.py` and `tools/promote_borrowed_cultures.py` also write `BEGIN -->` markers on 1.4.x and were not audited here.

**Fix direction.** Port `_marker_region.py`, `tools/tests/test_marker_region.py` and the `generate_new_faction_kingdoms.py` hunk. Skip the `generate_arthedain_troops.py` hunk (the file and Arthedain are 1.5.x content). Then check the two other marker writers.

**1.5.x commits:** f963c6188, bd457ee6e. **Verify how:** `python -m pytest tools/tests/test_marker_region.py`.

### XML-DATA-10 (P3): the remaining rgl asset warnings are vanilla or cosmetic; one TAOM tool reports false crash suspects

**Evidence**
- `Unable to find particle system with name ...` (E1 from line 3745, right after TAOM_Map's prefabs load). Each name appears in vanilla Native prefabs on the laptop (`waterfall_splash` 3 Native files, `psys_blaze_small` 1, `outdoor_fire_sparks_small` 3, `psys_torch_fire` 2, `prt_small_smoke` 2, `active_volcano_smoke` 1, `light_house_flame` 1). Only `waterfall_splash` is also in 1 TAOM_Map prefab file. Engine noise, no effect drawn.
- `Unable to find item to add dependency(depender battania_light_armor_c ...)` fires while Native's own packages register (E1 lines 88-173, after `Loading packages $BASE/Modules/Native/AssetPackages`), before any TAOM data loads. Vanilla noise; UNVERIFIED without a vanilla-only rgl log.
- `Missing shader from sack pbr_terrain` (E2: 3,086, plus 978 `_gbuffer`, 26 `_shadowmap`, 21 `_pointlight`; E1: 368 and 107). A shader-cache matter, handed to the shader lens.
- `tools/audit_scene_names.py` lists 9 "CRASH SUSPECTS" (`castle_orthanc_gate`, `town_isengard`, `sturgia_house_a_interior_house` and others). All come from the installed TAOM module's own `ModuleData/settlements.xml`. The engine never loads that file: the installed TAOM `SubModule.xml` registers no `Settlements` XmlName, and only TAOM_Map does. Every scene the live TAOM_Map settlements name exists (212 distinct names). The tool is unchanged on 1.5.x.
- `audit_mbproj_registration.py`: 0 errors, 1 warning, `soln_voice_definitions` x4 in TAOM's `project.mbproj` (MERGE-RISK). Shipped unchanged since 86f13b80d (2026-01-29), and players boot, so it is a known-safe warning.

**Fix direction.** None in game. Teach `audit_scene_names.py` to skip a `settlements.xml` its module never registers, and add these lines to a log-triage ignore list. **1.5.x commits:** none.

## Checks run (laptop, 2026-11-04, working tree c5b84fb41, all read-only)

| Check | How | Result |
|---|---|---|
| ModuleData refs | `python tools/validate_moduledata.py` | 3,423 errors, all `BROKEN_ITEM_REF` (220 ids); 117 warnings; 0 `LANDLESS_CULTURE`, 0 `SETTLEMENT_ECONOMY_FLOOR` |
| Prefab budget | `tools/check_prefab_budget.py --dir <TAOM_Map\Prefabs>` + per-module sum | TAOM_Map 93,830 "OK"; all installed modules 130,468 / 131,072; players' set about 120,601 |
| Action sets | `tools/audit_action_set_parity.py --native ... --live ...` | 0 humanoid gaps, 0 root-level `<action>`, exit 0 |
| Polearm + shield | `tools/audit_polearm_shield_parity.py --game-modules ...` | exit 1 from 8 stale ratchet rows (Armory-vintage artefact); 73-roster WARN; 6 undefined `<AvailablePiece>` ids |
| Settlement scenes | `tools/audit_scene_names.py` | live TAOM_Map: 212 names, all present; 9 false suspects from an unloaded file |
| Battle scenes | ad hoc parse of every active `sp_battle_scenes.xml` | 161 rows, all present (SandBoxCore) |
| Mesh + body refs | `tools/validate_mesh_refs.py --scan-bodies --no-rgl-log` | 5,783 refs; 0 missing bodies (334 unique), 0 missing meshes, 3 `KNOWN_DEAD_MESH` |
| mbproj registration | `tools/audit_mbproj_registration.py` | 0 errors, 1 known-safe warning |
| XSLT | `tools/check_external_xslt.py` | 17 stylesheets PASS |
| Engine XSD | 1.5.x `validate_xml_schemas.py --live`, exec'd from `git show` | 214 files, 1 fail (inert `family_type` on 3 Horse items in the laptop Armory) |
| Creature chain | ad hoc | 94 Monsters, 0 broken `action_set` / `base_monster` / `monster_usage`; 43 Horse items, 0 broken `monster` |
| Crafted items | ad hoc | 808, 0 undefined pieces or templates |
| Races | ad hoc (XML parse; grep misses the multi-line `<race`) | 14 Armory races + human; every race the public branch names resolves |
| Player rgl logs E1/E2 | grep | only XSD error is `taom_body_length`; no `get_object failed for body`; no skeleton, action-set or monster errors |

Not run: `tools/audit_deleted_mesh_impact.py`, which writes a report by default (`--out`) and diffs loose `Assets/` this laptop does not have.

## Summary

**The most important conclusion.** TAOM's XML and asset references are not a demonstrated cause of the player freezes, but the 1.4.x line has no protection against the one data defect that produces exactly the Harad symptom: a collision body no shipped pack contains. In v1.4.8, `PreloadHelper.WaitForMeshesToBeLoaded` waits forever for it on the main thread, right after the loading screen drops. That is precisely where E3 goes silent, twice on the same encounter. 1.5.x fixed the class with a 5-second guard (Patch90, fb8d73145) that binds unchanged on v1.4.8. Port it first (XML-DATA-01); it also turns the next occurrence into a named `[PreloadGuard]` log line instead of a silent freeze.

**Ranked by player impact:**
- XML-DATA-01 (P1, port Patch90).
- XML-DATA-03 (P1, merge the public troop tree into bannerlord-1.4.5 before any port; otherwise the next release regresses to bowless archers).
- XML-DATA-02 (P2, refresh the laptop Armory; every item verdict here is about v2.0.26).
- XML-DATA-06 (P2, desk-side body gates).
- XML-DATA-07 to -10 and -04 / -05 (P3, gates, tooling and noise).

**Answers to the five questions.**
1. **Validator.** It swept the repo plus the live Armory and TAOM_Map: 3,423 `BROKEN_ITEM_REF`, nothing else at error. TAOM_Map is now complete in file set (0 landless and 0 economy errors, against 414 and 90 on 2026-09-06), but it is a 09-07 copy. The Armory is v2.0.26 from 2026-09-07: older than the 2026-09-29 Armory players run, and without the desktop's generated `ladder_` and `starter_` items. Trust the laptop for culture and scene data, not for items, meshes or bodies (XML-DATA-02).
2. **Auditors.** All ran clean except two environment artefacts: the polearm ratchet FAIL and the prefab gate's hardcoded `E:\` path. The prefab sum across every installed module is 130,468 of 131,072; the cap and its assert strings exist only in the Modding Kit's native DLL (XML-DATA-05). Action sets, scenes, meshes, bodies, XSLT and mbproj are clean.
3. **`taom_body_length`.** It comes from the public Armory's `Monsters/LOTR/lotr_monster_elk.xml` (L45) and `lotr_monster_animalia.xml` (L26, L33), against the vanilla `XmlSchemas/Monsters.xsd`. It is the 1.5.x MonsterSize attribute. v1.4.8 logs it and loads the file, `Monster.Deserialize` ignores it, and nothing on 1.4.x reads it. Harmless; document it (XML-DATA-04).
4. **1.5.x data-integrity work.** Port 05bd0a730 at its original shape plus the validator body pass (ef6afeb67, 332f2ac71) and `COLLISION_BODY_BORROWED` (b23a27ef4) (XML-DATA-06). Port 7bbe6f4de (needed for `module_tpacs`; `--check-name` is a bonus). Port d5986f87f only once the public Armory is confirmed to have dropped the troll items. Port the XSD gate (fa8c06f82 through c1ce78207, XML-DATA-07), f963c6188 minus the Arthedain hunk (XML-DATA-09), and 69b47d00b with a one-assembly list (XML-DATA-08). Skip 53a4e4e52 unless its ratchet test is ported. **The public-branch question:** yes. origin/bannerlord-1.4.5's troop files are byte-for-byte the pre-#697 id set (2,562 ids), and 156 of them are absent from the public troop tree. The maintainer measured 589 broken refs against the public Armory (XML-DATA-03).
5. **Concrete hang or crash references on 1.4.8.** None found on the data this laptop has: 0 missing bodies, meshes, scenes, monsters, action sets, usages, crafting pieces, templates or races. Undefined item ids are empty slots, not hangs (`Equipment.cs:204-223`, :445-450). The hang-capable class (a missing body) cannot be checked against the public Armory here. For the E3 battle, the unverified candidates narrow to 22 Gondor weapons and shields from the public troop tree (XML-DATA-01).

**Player workarounds.**
- For a battle that freezes after the loading screen with low CPU, choose "Send troops" (auto-resolve). It simulates the battle without loading a mission (v1.4.8 `Helpers/MenuHelper.cs:554-582`: `PlayerEncounter.InitSimulation` then `MapState.StartBattleSimulation()`), so `MissionPreloadView` never runs.
- On the first battles after an install, give the shader cache several minutes before calling it a hang. E3 was abandoned about 100 s in, and TAOM's own stall watchdog waits 300 s.
- Send the matching `rgl_log` from `C:\ProgramData\Mount and Blade II Bannerlord\logs` with any freeze report.

**Recommended skills for the orchestrator:** `/investigate` on the Harad freeze once the player's rgl log is in hand; `/verify-bindings` and `/deep-review` for the Patch90 port package; `/ship` to close it.
