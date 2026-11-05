# Input pack: WP 108 missing-body-load-hang

Title (draft): Guard the missing collision-body infinite wait (Patch90) and gate missing bodies and meshes at the desk
Wave: 1   Lane: C   Draft priority: P0   Groups: 5   Port commits: 7
Depends on (WP ids): 103, 105

## Orchestrator notes for this WP (binding)
- V02: the missing-body spin starts about 11 frames AFTER BattlePlayable (MissionScreen), so it fits 'froze just as the loading screen disappeared', not E3.
- K2: also add the two Patch90 lessons from 1571c2f91 to docs/reviews/lessons/harmony-il.md.
- Take 30abf550b's validate_mesh_refs loose-first hunk; b23a27ef4 and 7bbe6f4de; d5986f87f only if the public Armory dropped those troll items.
- Regenerate any audit report against the PUBLIC Armory (refreshed laptop per D20, or the desktop); body and mesh verdicts from the old v2.0.26 laptop Armory are void.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/V02-e3-wedge-or-compile.md
- plans/_audit/2026-11-04-port148/verify/K2-skip-other-critic.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Deep-dive findings and non-port items this WP owns
- LOADING-HANG-02 [P1/high] Missing collision body spins PreloadHelper.WaitForMeshesToBeLoaded forever right after the loading screen drops; no guard on 1.4.x and TAOM diagnostics are blind to it | port: fb8d73145,05bd0a730,ef6afeb67,332f2ac71  (report: plans/_audit/2026-11-04-port148/deepdive/loading-hang.md)
- XML-DATA-01 [P1/medium] No guard on the missing-collision-body infinite wait; the Harad first-battle freeze (E3) stops exactly where it runs | port: fb8d73145  (report: plans/_audit/2026-11-04-port148/deepdive/xml-data.md)
- XML-DATA-06 [P2/high] Desk-side body and mesh gates from #599, #622 and #633 are absent on 1.4.x | port: 05bd0a730,30abf550b,ef6afeb67,332f2ac71,b23a27ef4,7bbe6f4de,d5986f87f  (report: plans/_audit/2026-11-04-port148/deepdive/xml-data.md)

## Port groups owned by this WP (from the commit classification)

### group `issue-599-armory-ref-audit`  P1  track=data-integrity  commits=2  sim: clean=1 conflict=1
symptoms: loading-hang
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/_Module/ModuleData/special_resources/troop_resource_costs.xml
- `05bd0a730` 2026-09-15 PORT P1 pick=clean :: feat(tools): v2.0.29 - the Armory reference audit (#599)
  - why: One command finds every Armory body_name or mesh that no tpac ships (the #352 infinite mission-load class), names the troops carrying each and writes a committed report; the maintainer's 1.4.8 infinite loads include wrong mesh ids.
  - port notes: Pick is clean, and its four imports (validate_mesh_refs, generate_armory_catalogue, audit_deleted_mesh_impact, _gamedir) are byte-identical between origin/bannerlord-1.4.5 and this commit (empty git diff). Take tools/audit_armory_refs.py, tools/tests/test_audit_armory_refs.py, docs/features/armory-ref-audit.md and the two small doc edits. Do NOT copy docs/audits/armory-ref-audit.md or the catalogue.tsv regen: regenerate both against the live Armory the public 1.4.8 build uses (new Armory ids, #697). The tool falls back to cooked AssetPackages (this laptop has no loose Armory Assets); UNVERIFIED whether validate_mesh_refs' TOC scan parses cooked packs, so check before trusting a CLEAN here.
  - issues: 352, 599
- `ef6afeb67` 2026-09-15 PORT-PARTIAL P1 pick=conflict :: Refactor validation and auditing tools; enhance troop cost definitions
  - why: Adds a MISSING_COLLISION_BODY error and MISSING_VISUAL_MESH warning to validate_moduledata.py, the /armory-audit skill, the session-start Armory drift alarm and the Patch90 docs; its SpecialResources half already reached 1.4.x in c5b84fb41.
  - port notes: Take: tools/validate_moduledata.py MISSING_COLLISION_BODY/MISSING_VISUAL_MESH + tools/tests/test_validate_moduledata.py; .claude/hooks/check-moduledata-validation.sh (--code MISSING_COLLISION_BODY); .claude/rules/moduledata-validation.md rows; the .claude/hooks/session-start.sh Armory drift block (its context lines exist on 1.4.x; it scans loose LOTRLOME_Armory/Assets, so on this cooked laptop install it prints UNCHECKED); .claude/skills/armory-audit/SKILL.md; the CLAUDE.md Armory-art-drop trap row and session-start hook-contract row; doc rows (features/moduledata-validation, audits/README, INDEX, doc-lookup, feature-map PreloadBodyGuard + ArmoryRefAudit, hooks-catalog, tools/README); the Patch90 registry section, patch-targets count and Review 111. Leave (already on 1.4.x via c5b84fb41, which names ef6afeb6, or present there): RecruitCartGrouping, RecruitmentVM_ExecuteDone_Patch, RecruitGate edits, troop_resource_costs.xml, wire_black_numenorean_troops.py, both co-op veto rows, Gondor RCA/lessons/Codex prompt, Reviews 112-113, the two .ai/review-reference.md entries, the stuck-vote CLAUDE.md row.
  - prereqs: 05bd0a730, fb8d73145
  - conflicts: Main/_Module/ModuleData/special_resources/troop_resource_costs.xml
  - issues: 352, 599

### group `issue-601-preload-body-guard`  P0  track=load-hang  commits=1  sim: clean=0 conflict=1
symptoms: loading-hang
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/IoC.cs, Main/SubModule.cs
- `fb8d73145` 2026-09-15 PORT P0 pick=conflict :: feat(preload): v2.0.29 - Patch90 guards missing-body load hangs (#601)
  - why: v1.4.8's PreloadHelper.WaitForMeshesToBeLoaded is the same unbounded do/while over _uniqueDynamicPhysicsShapeName (checked on the installed DLL via taom-src), so one unresolvable body_name freezes the main thread when a battle, siege, tournament or custom battle starts rendering; this prefix drains such names after a 5 s budget and logs them.
  - port notes: No 1.5.x dependency: field name and type and the loop match v1.4.8 (C:\Users\mikew\.taom-src\v1.4.8\TaleWorlds.MountAndBlade.View.PreloadHelper.cs:14, 58-74); no existing guard on 1.4.x; Patch90 is free there. Campaign-battle caller is MissionPreloadView.OnSceneRenderingStarted (_modules_build SandBox.View.cs:4978). Conflicts: Main/IoC.cs and Main/SubModule.cs registration (1.5.x places it beside Patch89), plus the CoopVetoClassificationTests ReviewedSafe row this commit adds. Its registry section, feature-map row and Review 111 ride in ef6afeb67. E3 caveat: MissionScreen calls OnSceneRenderingStarted only after _loadingScreenFramesLeft = 15 render-ready frames (Native View dump :12583, :16474-16486), while TickMission, and with it BattlePlayable, runs from the first render-ready frame; E3 never logged BattlePlayable, so the Harad freeze probably is NOT this loop. Leave 6d7e67f3f (later co-op split).
  - conflicts: Main/IoC.cs, Main/SubModule.cs
  - issues: 352, 599, 601

### group `collision-body-gate`  P1  track=load-hang  commits=2  sim: clean=0 conflict=2
symptoms: loading-hang
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/Mumakil/TaomMumakilPlatform.cs, Main/Features/Mumakil/TaomMumakilStandingPoint.cs, TAOM.Tests/Features/Mumakil/MumakilPlatformTests.cs, tools/tests/test_validate_moduledata.py, tools/tests/test_validate_xml_schemas.py, tools/validate_moduledata.py
- `332f2ac71` 2026-09-18 PORT P1 pick=conflict :: fix(tools): v2.0.29 - collision-body gate runs, TAOM_Map pinned (#622)
  - why: Makes validate_moduledata.py actually run MISSING_COLLISION_BODY, the commit-time guard for the #352/#599 infinite mission load, bounds its tpac scan under the hook timeout, and fixes a validator crash on a partial install.
  - port notes: The pass (missing_collision_body_issues) is absent on the 1.4.x tip; it arrived post-fork in ef6afeb67: take only that commit's validate_moduledata.py pass, tests, hook --code line and rule row, not its SpecialResources or troop-cost changes. validate_mesh_refs.py on 1.4.x already has the MISSING_BODY classifier (classify(..., scan_bodies) at :689), so the wiring ports. The partial-install crash holds on 1.4.x by itself (taom_schema.py:218 stores strings, validate_moduledata.py:188 calls .parent on them) and can land first. The test_validate_xml_schemas.py pin of TAOM_Map's registrations needs fa8c06f82's tool and reads the LIVE TAOM_Map: on a 1.4.8 install the seven dead registrations (#619) may still be there, so treat a failure as an install finding and never edit the install from the repo. armory-audit SKILL.md and armory-ref-audit.md lines exist only if ef6afeb67's skill is ported.
  - prereqs: ef6afeb67, fa8c06f82
  - conflicts: tools/tests/test_validate_moduledata.py, tools/tests/test_validate_xml_schemas.py, tools/validate_moduledata.py
  - issues: 352, 619, 622, 623
- `b23a27ef4` 2026-09-21 PORT-PARTIAL P2 pick=conflict :: Add unit tests for collision body handling in Blender tools
  - why: Adds the COLLISION_BODY_BORROWED gate after the #633 Rhun longbow load hang and fixes validate_mesh_refs' item regex, which never matched <CraftingPiece> (the 1.4.x tip has the same regex at validate_mesh_refs.py:210).
  - port notes: Take the validate_mesh_refs.py CraftingPiece regex fix and its tests, the COLLISION_BODY_BORROWED pass in validate_moduledata.py with test_collision_body_borrowed.py, the hook and rule rows, tools/blender/add_collision_body.py with its tests (the tool for authoring a bo_ twin), the moduledata-validation.md and weapon-creation-workflow.md sections, the RCA and lesson. Leave the Mumakil platform and howdah seat changes, LegacyHowdahPrefabTests, docs/reference/troop-rosters.html (generated from 1.5.x data; generate_troop_roster_page.py optional), catalogue rows, and the preload-body-guard.md section unless fb8d73145 (PreloadBodyGuard, Patch90) is ported. The RCA records the player's hang cause as still open.
  - prereqs: ef6afeb67, 332f2ac71
  - conflicts: Main/Features/Mumakil/TaomMumakilPlatform.cs, Main/Features/Mumakil/TaomMumakilStandingPoint.cs, TAOM.Tests/Features/Mumakil/MumakilPlatformTests.cs, tools/validate_moduledata.py

### group `armoury-mesh-ref-audit`  P3  track=tests-ci  commits=1  sim: clean=0 conflict=1
- `d5986f87f` 2026-09-22 PORT-PARTIAL P3 pick=conflict :: chore(armoury): v2.0.30 - audit XML references and clean up dead mesh entries
  - why: Empties the dead-mesh allowlist after the troll armour art returned and makes the allowlist tests own a fixture instead of reading production entries.
  - port notes: Take the test_validate_mesh_refs.py fixture change unconditionally. Retire lotr_troll_armor, lotr_troll_bracers and lotr_troll_helmet from KNOWN_DEAD_MESHES (still listed at validate_mesh_refs.py:104-114 on the 1.4.x tip) only if the Armory paired with the 1.4.8 build ships LOME_troll_armor_geo.tpac; a full validate_mesh_refs.py run reports STALE_DEAD_MESH_ALLOWLIST when the art is back (main() passes check_allowlist=True at :1159). The catalogue README counts describe the desktop's live tree; leave them.

### group `tools-mesh-name-check`  P2  track=dev-tools  commits=1  sim: clean=0 conflict=1
- `7bbe6f4de` 2026-09-29 PORT-PARTIAL P2 pick=conflict :: feat(tooling): v2.0.32 - /new-map-prop and a mesh name check
  - why: validate_mesh_refs.py --check-name checks that a new mesh name is free in every module's loose Assets and AssetPackages, and it is a shared dev tool. The /new-map-prop skill depends on docs that exist only on 1.5.x.
  - port notes: Take the --check-name option in tools/validate_mesh_refs.py (find_mesh_name_owners and check_names, which reuse scan_tpac_metameshes; the 1.4.x copy already has that), the tools/tests/test_validate_mesh_refs.py additions and the tools/README.md row. Leave the /new-map-prop skill and its CLAUDE.md row unless docs/reference/tpac-static-prop-authoring.md and the MithrilForge adoption review come too (both are absent on 1.4.x). The cherry-pick simulation found no code conflicts.

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 1, batch 3, batch 12
