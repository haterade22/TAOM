# Triage C: June 2026 /improve harvest, bucket C (DOCS, GAMEDATA, DIRECTION), harvest lines 602 to 859, re-checked at `b2e387db`

Keyed by the line number of each `### [` heading in `plans/_audit/2026-06-12-harvest.md`.

### L605 DOCS-01: CLAUDE.md Harmony Patch Categories table missing 11 categories
- **Verdict:** FIXED. **Impact:** LOW.
- **Evidence:** CLAUDE.md (105 lines at HEAD) no longer carries the table; it moved to `docs/reference/harmony-patch-registry.md` in `5c1051c9` (2026-07-12). A `## ` section exists there for all 11 named categories (Patch25, 30, 33, 35, 36, 37, 39, 41, 43, Late_ActionSetOverride, Late_Transpiler). Measured: `git grep -h -o 'HarmonyPatchCategory("[^"]*")' b2e387db -- Main` gives 94 unique categories; `comm` against the registry's `## ` headings leaves only three `Patch89_MapLoadDiagnostics_*` sub-categories without their own heading, and they are covered by the `## Patch89_MapLoadDiagnostics` section (registry line 1020).
- **Note:** Agree with June verifier REAL at the time. Residual: no gate checks this registry (the GameModel one has `lint_docs.py` check_model_registry); the registry header itself says it "can lag".

### L614 DOCS-02: CLAUDE.md GameModel Overrides table missing 7 Taom*Model classes
- **Verdict:** FIXED. **Impact:** LOW.
- **Evidence:** Table now lives in `docs/reference/gamemodel-registry.md`; `caefb07c` (2026-08-07, "the GameModel catalogues drifted, and nothing could say so") added the missing rows and a `check_model_registry` gate in `tools/lint_docs.py` with `tools/tests/test_model_registry.py`. Measured: all 50 `class Taom*Model` names in `Main/` at HEAD appear in the registry (loop over `git grep -o "class Taom[A-Za-z0-9]*Model"`, zero missing), including all 7 named.
- **Note:** Agree with June REAL; now gated.

### L648 DOCS-01 (dup): registry tables drift, 7 models and 11 patch categories
- **Verdict:** FIXED. **Impact:** LOW.
- **Evidence:** Same as L605 and L614. The side claim ("all 70 feature docs" at CLAUDE.md:259) is gone: `git grep -E "[0-9]+ feature docs"` over CLAUDE.md, AGENTS.md, docs/ai-includes, docs/INDEX.md returns nothing at HEAD.
- **Note:** Duplicate of L605 + L614.

### L624 DOCS-03: ADR-001 (XML-only config) contradicted by shipped JSON configs
- **Verdict:** STILL_VALID. **Impact:** MED.
- **Evidence:** `docs/adrs/001-xml-config.md:3,9,43,47` at HEAD still reads Status Accepted, "All configuration files for the mod must use the XML format", "Prohibited: No JSON files for game configuration". `docs/adrs/README.md:9,36` repeats "All config files use XML (no JSON)" and "Use XML for game config, not JSON (ADR-001)"; `docs/ai-includes/architecture.md:427` cites it too. Measured `git ls-tree -r --name-only b2e387db Main/_Module/ModuleData | grep -c "\.json$"` = 68 (June: 49), so the gap grew. No superseding ADR exists (ADRs 001 to 011; 011 is knowledge tiers). No exception in `.ai/` or `.claude/` (grep `ADR-001` finds none).
- **Note:** Agree with June REAL. A reviewer following the ADR would flag every JSON config; a one-page amendment fixes it.

### L632 DOCS-04: CLAUDE.md career starter-equipment claims "Gondor only"
- **Verdict:** FIXED. **Impact:** LOW.
- **Evidence:** `git log -S "Gondor only as of 2026-05-19" -- CLAUDE.md` shows the line removed in `5c1051c9` (2026-07-12). The owning doc now states the truth: `docs/features/career-system.md:407` "78 across 13 cultures, each culture's lowest troop gear (#629)". Committed roster file (read via `git show b2e387db:`, the working copy is another session's) has exactly 13 cultures x 6 rosters.
- **Note:** Agree with June REAL. Residue: `docs/migration/templates/equipment-rosters.md:558` still says "Gondor only authored; 15 other cultures fall through", and `.claude/skills/improve/references/audit-playbook.md:119` still uses "career starting equipment: Gondor only" as its example of unfinished intent (both LOW).

### L640 DOCS-05: skill docs pin stale engine versions (taom-src v1.3.15; migration-status v1.2 to v1.3)
- **Verdict:** STILL_VALID (partly fixed). **Impact:** LOW.
- **Evidence:** taom-src half FIXED: `.claude/skills/taom-src/SKILL.md:3` description is now version-neutral, and `:9` says the script "auto-detects the engine version from `Version.xml`" and caches at `~/.taom-src/<version>/`, consistent with AGENTS.md "Research first" (`git log -S "v1.3.15 types"` shows `0fc9b8c1`, 2026-07-08). Tiny residue: `:9` still says "currently v1.5.2" while the pin is v1.5.3. migration-status half STILL_VALID: `.claude/skills/migration-status/SKILL.md:3` description "Check and summarize the v1.2 to v1.3 Bannerlord migration progress"; `:14-19` hardcodes the old checklist; `:25` points at `v1.3-api-changes.md`. `docs/migration/TRACKING.md:3-5` is now a 1.3.15 to 1.4.5 record plus bump chain to v1.5.3.
- **Note:** Agree with June REAL. Mitigated: CLAUDE.md lists `/migration-status` under "Never auto-invoke", and the old "v1.2 -> v1.3" CLAUDE.md rows are gone.

### L657 DOCS-02 (dup): taom-src SKILL.md pins v1.3.15 and inverts decompile guidance
- **Verdict:** FIXED. **Impact:** LOW.
- **Evidence:** `.claude/skills/taom-src/SKILL.md:3` (no version) and `:9` ("never guess signatures from the `E:\Decompiled_Bannerlord\` dump (it can lag the installed engine after a bump)") now match AGENTS.md "Signatures: the installed DLLs only". Fixed in `0fc9b8c1` (2026-07-08).
- **Note:** Only residue is ":9 currently v1.5.2" (pin is v1.5.3), trivial.

### L665 DOCS-03: doc-linter backlog 56; five dead howdah links, 4 orphans, missing mcm.md
- **Verdict:** FIXED. **Impact:** LOW.
- **Evidence:** All five howdah links now use `../../features/elephant/howdah-crew-mechanism.md` (`docs/reference/engine/usable-machines.md:5,67,88`, `mount-and-rider-runtime.md:86`, `scene-gameentity-scriptcomponent.md:86`), fixed in `9454fdee` (2026-08-05). `docs/features/mcm.md` exists (added `5c1051c9`). `python tools/lint_docs.py --summary` this run (working tree): dead_links 0, stale_versions 0, orphan_features 0, missing_features 0, model_registry 0; the non-zero rows are context_budget 7 (total_findings 34).
- **Note:** Agree with June REAL; closed since. Linter ran on the working tree, which includes another session's uncommitted docs.

### L673 DOCS-04: migration-status skill and CLAUDE.md rows describe the finished v1.2 to v1.3 migration
- **Verdict:** STILL_VALID. **Impact:** LOW.
- **Evidence:** `.claude/skills/migration-status/SKILL.md:3,14-19,25` unchanged (v1.2 to v1.3 description, old Settlement/Troop/Item/Equipment checklist, `v1.3-api-changes.md`). The CLAUDE.md half is fixed: CLAUDE.md at HEAD only names `/migration-status` in the "Never auto-invoke" row, with no version text.
- **Note:** Agree with June REAL for the skill. The June fix sketch (retire the skill; `/engine-bump` and TRACKING.md cover the job) still applies.

### L681 DOCS-07: scene-script spec asserts v1.3.15 engine behaviour from never-refreshed signature snapshots
- **Verdict:** STILL_VALID. **Impact:** LOW.
- **Evidence:** `git ls-tree b2e387db docs/scene-scripts/sigs/` lists 9 files, all `*-v1.3.15.txt`, none newer. `docs/scene-scripts/specs/cs-road.md:7,9,103` still anchor on `scriptcomponentbehavior-v1.3.15.txt`, "Bannerlord v1.3.15 scans fields, not properties" and "Bannerlord v1.3.15 `Mesh` API". The engine is now v1.5.3, three bumps on. `lint_docs.py` reports 0 stale versions only because its per-line rule (`tools/lint_docs.py:162-200`) now flags a version only when phrased as the current target, so these lines no longer trip it.
- **Note:** Agree with June REAL. Blast radius is small: CS_Road is an editor scene script; its pure geometry is unit-tested (`TAOM.Tests/SceneScripts/Roads/*`), the engine-facing field and Mesh surface is not. Whether the surface drifted is UNVERIFIED (no taom-src diff run here).

### L688 DOCS-05 (dup): scene-script specs direct verification at v1.3.15 sig dumps
- **Verdict:** STILL_VALID. **Impact:** LOW.
- **Evidence:** As L681, plus `docs/features/scene-scripts.md:44` ("verified against `docs/scene-scripts/sigs/scriptcomponentbehavior-v1.3.15.txt` lines 339-398") and `:97` ("Pinned v1.3.15 ilspycmd outputs").
- **Note:** June left this unmatched (never verified); it is true at HEAD. Duplicate of L681.

### L696 DOCS-06: no ADR for the creature-as-mount architecture
- **Verdict:** STALE. **Impact:** LOW.
- **Evidence:** Still no ADR (`docs/adrs/` holds 001 to 011, none on creatures), but the decision is now recorded as a standing rule in two always-routed homes: `docs/ai-includes/creature-mount-authoring.md:10-14` at HEAD ("a creature mount is a vanilla cavalry spawn ... **No spawn patches, no detached combatants** ... that architecture was built twice and deleted twice") and `.claude/skills/new-creature-mount/SKILL.md:13-17` ("Architecture (never deviate)"), which CLAUDE.md's skills table gates on any new creature. Both read via `git show b2e387db:` (the working copies are another session's).
- **Note:** June REAL was fair then; ADR-011 routes a procedure-with-traps to a skill, and the skill now carries the decision, so the re-reversal risk the finding named is covered. An ADR would only add a pointer.

### L704 DOCS-06 (second): Mcm has no feature doc; 4 orphan feature docs; "70 feature docs" count
- **Verdict:** FIXED. **Impact:** LOW.
- **Evidence:** `docs/features/mcm.md` exists (added `5c1051c9`, 2026-07-12). `python tools/lint_docs.py --summary` this run: orphan_features 0, missing_features 0. `battle-load-diagnostics` and `clan-heraldry` are now linked from `docs/INDEX.md`; `career-quest-system` and `new-factions-misty-mountains-lindon` are not in INDEX but are linked from `docs/features/career-system.md` and `docs/features/culture-playability-wiring.md`, so they are not orphans. The "70 feature docs" claim is gone from CLAUDE.md (156 `.md` files in `docs/features/` at HEAD).
- **Note:** Agree with June REAL; closed since.

### L715 GAMEDATA-01: all 19 Custom Battle scene entries point at deleted scene folders
- **Verdict:** FIXED. **Impact:** LOW.
- **Evidence:** `d6977692` (2026-06-12, "taom_* scene-id rename + drop broken Iron Hills from picker") repointed the file. At HEAD `git show b2e387db:Main/_Module/ModuleData/custom_battle_scenes.xml` holds 26 scene ids, all `taom_*`; each was matched case-insensitively against the 652 folder names under `Modules/*/SceneObj/` on this machine: 26 OK, 0 missing.
- **Note:** June left it unmatched; it was real (the fix commit is the same day). Residual gap: no tool or test reads `custom_battle_scenes.xml` against SceneObj (`git grep -l -i "custom_battle_scenes" -- tools TAOM.Tests` finds nothing), so the next rename wave can break it silently again. The working copy has staged #603 edits by another session; not assessed.

### L724 GAMEDATA-02: live TAOM_Map settlements.xml, 3 town center scenes point at deleted folders
- **Verdict:** FIXED. **Impact:** LOW.
- **Evidence:** Live `Modules/TAOM_Map/ModuleData/settlements.xml` (mtime 2026-09-15): `town_EW9` Calembel (line 3106) center is now `empire_town_h`, `town_V2` Helm's Deep (6132) is `taom_rohan_castle_helms_deep_forceatmo`, `town_isengard` (9925) is `taom_isengard_town_orthanc_forceatmo`. `grep -n` for `lotrtaom_hat_gondor_town_calembel|Helms_Deep_Town_forceatmo|HART_isengard` returns nothing. Measured: all 199 distinct `scene_name` values in the file match a folder under `Modules/*/SceneObj/` (lowercased `comm`, 0 missing).
- **Note:** June left it unmatched; it was real then. The file is unversioned, so no commit to cite. `tools/audit_scene_names.py` covers this file but is manual (not wired into a hook or CI by grep).

### L733 GAMEDATA-04: Armory prefix-to-canonical-folder table wrong for `sk_dwarf_dain_*` and `urukscout_*`
- **Verdict:** STILL_VALID. **Impact:** LOW.
- **Evidence:** The table moved from CLAUDE.md to `docs/reference/armory-guide.md` (committed version read; the working copy is in the skip list). `armory-guide.md:27` still maps `urukscout_*`, `clo_urukscout_*` to `isengard/`; `:33` still maps `sk_dwarf_dain_*` to `erebor/`. Live Armory, counted with `grep -c 'id="<prefix>'` per folder: `sk_dwarf_dain_*` erebor 0, iron_hills 38; `urukscout_*` isengard 0, mordor 4; `clo_urukscout_*` 0 everywhere. The mismatch is already documented as a known discrepancy in `docs/modding/id-cheatsheet.md:195-196,205,434`, and `docs/features/armor-balance.md:293` calls urukscout "misfiled in mordor/", yet the guide the orientation trap index routes to ("Armory item ids ... Grep every `LOTRLOME_items/*/`") was never corrected.
- **Note:** Agree with June REAL. The trap-index rule (grep every folder first) mitigates the harm; the fix is two table rows.

### L741 GAMEDATA-05: heroes.xslt carries 3 dead rules for TAOM-only hero ids; spouse links one-directional
- **Verdict:** FIXED. **Impact:** LOW.
- **Evidence:** Dead-rule half fixed: `git show bc5b5d71 -- Main/_Module/ModuleData/heroes.xslt` (2026-09-14, "dead faction data") deletes the three `match="Hero[@id='lord_1_9_5'|'lord_1_52_4'|'lord_1_71_1']"` templates; none remain at HEAD. Spouse half was never a defect: the vanilla-side rules still set `spouse` (`heroes.xslt:607` lord_1_9 to lord_1_9_5, `:802` lord_1_52 to lord_1_52_4, `:879` to lord_1_71_1), and the installed v1.5.3 engine makes that reciprocal: `Hero.Spouse` setter assigns `_spouse.Spouse = this` (`~/.taom-src/v1.5.3/TaleWorlds.CampaignSystem.Hero.cs:892-914`), and `Deserialize` only reads the attribute `if (Spouse == null)` (`:1908-1911`), so the TAOM-side rows without `spouse` (`characters/heroes.xml:1516-1528`) neither block nor undo the marriage in either load order.
- **Note:** June left it unmatched. Dead rules were real; the "spouses start unmarried" risk is refuted by the engine source.

### L748 GAMEDATA-03: about 320 player-facing C# localization keys never harvested into string tables
- **Verdict:** FIXED. **Impact:** LOW.
- **Evidence:** `76c82cb6` (2026-08-09, "317 unreachable keys, now in 12 languages (#434)") harvested them via `tools/harvest_literal_loc_keys.py` and added a ratchet gate, `TAOM.Tests/Infrastructure/Localization/UnregisteredLocalizationKeyBaselineTests.cs` with a baseline that is empty at HEAD. Measured with a scratch script (all `{=KEY}` literals in `Main/**/*.cs` at `b2e387db` vs every `<string id=` in `Main/_Module/ModuleData/**/*.xml` at HEAD plus every vanilla string id and inline key in installed non-TAOM modules): 803 code keys, 4 unregistered. `taom_feat_` 260/0 missing, `taom_ep_` 34/0, `taom_fief_` 11/0, `taom_qa_` 8/0.
- **Note:** June left it unmatched; it was real (the fix commit counts 317). Residue: the 3 `TAOM_CrashReport_*` keys at `Main/Features/CrashReport/UI/CrashNotifier.cs:23,27,28` are still unregistered (the 4th hit, `VANILLA_ID`, is a doc-comment placeholder). The gate misses them because it tracks `{=taom_*}` literals, and these use an upper-case `TAOM_` prefix. `str_party_list_label_without_max` is no longer in the code.

### L760 DIRECTION-06: resolve SiegeMountBehavior mode 1 "RESERVED" dead value
- **Verdict:** STILL_VALID. **Impact:** LOW.
- **Evidence:** `Main/Features/TaomSettings.cs:433-435` still exposes `SettingPropertyInteger(... "1=Reserved" ..., 0, 3)` with hint `1 = RESERVED (currently equivalent to Vanilla — full implementation deferred ...)`; `Main/Features/SiegeDismount/SiegeDismountService.cs:68-75` still treats `DismountKeepOnMap` as a no-op that logs a warning; enum member at `Models/SiegeMountBehaviorType.cs:6`.
- **Note:** Direction item, not a defect; no decision recorded since June. The honest hint text limits player harm; the default is 3.

### L783 DIR-05 (dup): finish or retire SiegeDismount mode 1 (DismountKeepOnMap)
- **Verdict:** STILL_VALID. **Impact:** LOW.
- **Evidence:** Same as L760: `TaomSettings.cs:433-435`, `SiegeDismountService.cs:68-75`.
- **Note:** Agree with June REAL. Duplicate of L760; the June "Option A retire" (clamp the slider, keep the enum for save-compat) is still the cheap path.

### L766 DIR-01: close the last 3 cultures in the career starting-equipment matrix (Lothlorien, Umbar, Khand)
- **Verdict:** STILL_VALID. **Impact:** LOW.
- **Evidence:** Committed `taom_career_starting_equipment.xml:28-29` (read via `git show b2e387db:`, working copy is another session's) still says "Lothlorien, Umbar and Khand (battania) have no career rosters and fall through to the culture-default roster"; roster cultures counted by `grep -o 'culture="Culture\.[a-z_]*"' | uniq -c` are the same 13, 6 each. Careers for all three exist (`career_system/taom_careers.xml:301-355` battania, `:726-780` lothlorien, `:1061+` umbar).
- **Note:** Agree with June REAL on the gap, but its fix sketch is stale: since #629 (open, "Career kits: lowest troop equipment") kits come from the culture's own lowest troops via `tools/generate_career_kits.py`, so no new Armory folders are needed. The generator only fills rosters the file already carries (`generate_career_kits.py:132,205`), so the work is adding 18 roster shells and re-running it.

### L775 DIR-02: refresh docs/roadmap.md and implement its Tier 1 race-stat goal via TaomAgentStatCalculateModel
- **Verdict:** STILL_VALID. **Impact:** MED.
- **Evidence:** `docs/roadmap.md:4` still says "**31 of 121** available GameModels overridden" (the registry catalogues 50 `Taom*Model` classes at HEAD); `:28` still lists `AgentStatCalculateModel` as the Tier 1 open item with no note that `TaomAgentStatCalculateModel` (`Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs:35`) already occupies the slot; `:51` recommends it first. Race bonuses do not exist: no `Main/` file that touches `AgentDrivenProperties` mentions "race" (loop over `git grep -l AgentDrivenProperties`), and no `RaceStat` type exists. Half-fixed: `:46` now marks `MapVisibilityModel` "SLOT TAKEN: `TaomMapVisibilityModel`".
- **Note:** Agree with June REAL. The roadmap was last touched `97f40b07` (2026-08-28, backlinks only).

### L790 DIRECTION-01 (dup): race stat bonuses through TaomAgentStatCalculateModel; refresh roadmap
- **Verdict:** STILL_VALID. **Impact:** MED.
- **Evidence:** As L775. The MapVisibility sub-claim (roadmap line 46 lists it as remaining) is FIXED at `docs/roadmap.md:46`; the count and the Tier 1 row are not.
- **Note:** Duplicate of L775.

### L808 DIR-03: re-enable the deferred elephant howdah crew and spine bone-tracking
- **Verdict:** FIXED (crew); bone-tracking still parked. **Impact:** LOW.
- **Evidence:** Crew is back: `Main/Features/Elephant/HowdahCrewSpawner.cs:23-27` "Re-enabled 2026-09-19 (#627, Mike) for the howdah harness only ... The rebuilt floor's underside now sits 0.33 m above that capsule", `CrewSpawnEnabled = true`, wired at `ElephantMissionBehavior.cs:35-48,140`; the queued-spawn-from-tick pattern avoids the #595 re-entry. Mumakil has its own tower crew, `Main/Features/Mumakil/MumakilCrewSpawner.cs:27` `CrewSpawnEnabled = true` (#627 phase 2). Bone-tracking is still off: `Main/Features/Elephant/TaomHowdahMachine.cs:237-242` `BoneTrackingEnabled = false` with the 2026-06-10 DEFERRED note, gate at `:256`.
- **Note:** June verifier said NOT-REAL; either way the player-facing gap (empty howdah) is closed, by raising the floor above the capsule, not by the shared-FaceGroupId fix the harvest named. In-game confirmation of no slide is not something this read can show (UNVERIFIED here). The remaining bone-tracking is polish with the fixed-offset path working.

### L844 DIRECTION-05 (dup): finish the mumakil set-piece, crew spawn plus spine bone-tracking
- **Verdict:** FIXED (crew); bone-tracking still parked. **Impact:** LOW.
- **Evidence:** As L808: `HowdahCrewSpawner.cs:27`, `MumakilCrewSpawner.cs:27` both `true`; `TaomHowdahMachine.cs:242` bone-tracking `false`.
- **Note:** Duplicate of L808; June verifier NOT-REAL.

### L816 DIR-06: generalize the Gondor-only JSON recruitment-pool loader to per-culture files
- **Verdict:** STILL_VALID. **Impact:** LOW.
- **Evidence:** `Main/_Module/ModuleData/recruitment_pools/` still holds only `gondor.json`; the loader is still `internal static class GondorRecruitmentJsonLoader` (`Main/Features/TroopProgression/GondorRecruitmentJsonLoader.cs:15`); `docs/features/volunteer-recruitment.md:42` "for Gondor only as of 2026-05-23". Measured `git grep -c "static void Initialize"` over `Main/Features/TroopProgression/`: 37 hand-written initializers. What changed: `0f738f88` (2026-07-01) split the pools into 17 per-culture partial files under `TroopProgression/RecruitmentPools/`, and `VolunteerRecruitmentService.cs` is now 252 lines (June: 988), so the "hotspot monolith" part of the case is weaker.
- **Note:** Agree with June REAL as a direction; no decision for or against is recorded. With the partial-file split, the per-culture C# edit is already localized, so the payoff is smaller than June priced it.

### L825 DIRECTION-02 (dup): migrate the 17-culture hand-written pools to per-culture JSON
- **Verdict:** STILL_VALID. **Impact:** LOW.
- **Evidence:** As L816. The "988-line hotspot" and `VolunteerRecruitmentService.cs:412-433` citations are stale: the file is 252 lines at HEAD and the pools live in `RecruitmentPools/VolunteerRecruitmentService.<Culture>.cs`. The reachability guard the harvest cites is still documented at `docs/features/volunteer-recruitment.md:48`.
- **Note:** Duplicate of L816.

### L834 DIRECTION-03: field the cave troll as the third creature through the mount pipeline
- **Verdict:** FIXED. **Impact:** LOW.
- **Evidence:** `2958f618` (2026-06-14, "enable cave_troll unit + prove ARP retarget pipeline") re-enabled the troop: `Main/_Module/ModuleData/troops/troops_mordor.xml:1988-1998` ("Re-enabled 2026-06-13: live Mordor troll race unit", `is_basic_troop="false"`), fielded by `taom_partyTemplates.xml:802` (`cave_troll`, 0 to 7, in `kingdom_hero_party_mordor_template`). Engine-facing support exists: `CombatMechanicsConfig.cs:36,57,132,141`, `SettlementGuardService.cs:16`, `BannerBearerConfig.cs:45`. The design question the harvest left open was decided the other way: `docs/features/troll-race.md:5-9` "It is **NOT a rideable mount** ... a big bipedal body ... `monster_usage="human"`", animated by retargeting the human library.
- **Note:** June REAL was right about the gap; it closed two days later by a different route (race unit, not mount). Not recruitable from a volunteer pool (party templates only), which the troop comment records as intended.

### L852 DIR-04: revive the disabled Mordor cave troll using the creature pipeline
- **Verdict:** FIXED. **Impact:** LOW.
- **Evidence:** As L834: the `DISABLED 2026-05-14` comment the harvest cites (`troops_mordor.xml:1967-2045` in June) is replaced by the re-enable note at `troops_mordor.xml:1988-1992`, which names the sole cause of the disable (vanilla `GetBasicVolunteer` picking an L51 troll) and its fix (`is_basic_troop="false"`).
- **Note:** Agree with June REAL at the time; duplicate of L834.

### L799 DIRECTION-04 (dup): close the last 3 career starting-equipment cells and fix the stale CLAUDE.md row
- **Verdict:** STILL_VALID. **Impact:** LOW.
- **Evidence:** Gap as L766: committed `taom_career_starting_equipment.xml:28-29` still has Lothlorien, Umbar and Khand falling through; 13 cultures x 6 rosters. The CLAUDE.md half is FIXED (row removed in `5c1051c9`, see L632).
- **Note:** Duplicate of L766. The "author starter_armors.xml via /author-armor" sketch is superseded by #629's troop-derived kits (`tools/generate_career_kits.py`).

## What I did not cover

- Harvest lines 602 to 859 only (30 findings). The category preambles at lines 603, 713 and 758 ("Not audited / considered and dropped") were not re-triaged.
- Files in BRIEF.md's skip list were read only at `b2e387db` via `git show` (career starting equipment XML, creature-mount skill, `docs/reference/armory-guide.md`, `docs/ai-includes/creature-mount-authoring.md`); their working-tree edits were not assessed. `python tools/lint_docs.py --summary` necessarily ran on the working tree.
- No in-game checks: howdah and mumakil crew "no slide" (L808, L844) and the Custom Battle scene loads (L715) are verified only as data and code state, not behaviour.
- L681/L688: I did not re-dump the 8 to 9 scene-script engine types with taom-src to see whether the v1.3.15 surface actually drifted.
- L724 and L733 read the live, unversioned TAOM_Map and LOTRLOME_Armory installs on this desktop; a reinstall could change them.
- GitHub issue state was checked only for #629 and #630 (`gh issue view`); #278/#272 (elephant) and #96 (localization) were not re-read.
- No `dotnet build` or `dotnet test` (per brief).
