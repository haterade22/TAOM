# Input pack: WP 144 docs-engine-sync

Title (draft): Port engine-research docs whose claims hold on v1.4.8, re-verified claim by claim
Wave: 5   Lane: I   Draft priority: P2   Groups: 19   Port commits: 26
Depends on (WP ids): 141

## Orchestrator notes for this WP (binding)
- Takes every docs-engine group (see the pack). Re-cite every engine line to the v1.4.8 dump or taom-src; drop claims that do not hold.
- Never carry '1.5.x MBGlobals.GetActionSet returns the default set' (v1.4.8 MBGlobals.cs:33-45 throws).
- K2: 42e48bbe5's SetDoNotMakeNewDecisions lesson citing v1.4.8 MobilePartyAi.cs:486.
- Fix docs/reference/bannerlord-engine-and-toolchain.md:186 ('managed code runs on Mono'): the installed Bannerlord.exe is a managed .NET Framework v4.7.2 exe, and v1.4.8 ships three hosts (V01, V05).

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/D4-patch88-and-binding-health.md
- plans/_audit/2026-11-04-port148/verify/K2-skip-other-critic.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md
- plans/_audit/2026-11-04-port148/verify/K1-skip-feature-critic.md

## Port groups owned by this WP (from the commit classification)

### group `docs-engine-formations-team-ai`  P3  track=docs-engine  commits=1  sim: clean=0 conflict=1
- `578770e03` 2026-09-17 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.29 - doctrine docs catch up; v1.5.3 named as current
  - why: Mostly doctrine docs and v1.5.3 version naming, but it corrects engine facts in formations-and-team-ai.md that hold on v1.4.8 (Team.FormationsIncludingSpecialAndEmpty is a 10-slot list at Team.cs:36/267, and TeamAIGeneral registers 24 single-player field behaviours).
  - port notes: Take the FormationsIncludingSpecialAndEmpty sentence for the Team paragraph, which exists on 1.4.x (re-cite Team.cs:36/:267 for v1.4.8; the 1.5.x text cites :279-290). Take the 24-behaviour and TransferUnits correction only together with the 'tactic layer' table from 5dfa8f4cb, because the '27' row it fixes was born on 1.5.x. The TransferUnitsAux to Agent.Formation setter to AddUnit claim is UNVERIFIED on v1.4.8. Leave culture-doctrine.md, analysis-battle-ai, feature-map, the lessons row and every v1.5.2 to v1.5.3 version edit (agent-operating-manual, native-skin-fixes, module-armory, module-map).
  - prereqs: 5dfa8f4cb

### group `docs-kit-animation-authoring`  P3  track=docs-engine  commits=1  sim: clean=0 conflict=1
- `2a9e8bc49` 2026-09-18 PORT-PARTIAL P3 pick=conflict :: feat(animation): update ENGINE skeleton requirements for animation authoring
  - why: CHANGELOG entries for 1.5.x-only creature work plus a refreshed CLAUDE.md trap row of Modding Kit animation facts that apply to any Kit authoring.
  - port notes: Take only the CLAUDE.md 'Animation must be authored on the ENGINE skeleton' row (root motion zeroed at frame 0, bone tracks in FBX node order against the skeleton's list order); its backing text comes with 4520af802's docs/reference/bannerlord-skeleton-authoring.md changes. The CHANGELOG entries (#618 ram head-butt, LOME troll mesh) are 1.5.x-only. Keep the row inside the 400-character linter budget.
  - prereqs: 4520af802

### group `engine-fact-isinbeingstruck-band`  P3  track=docs-engine  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/check_rdc_entries.py
- `4520af802` 2026-09-18 PORT-PARTIAL P3 pick=conflict :: feat(creatures): v2.0.29 - ram hits one foe, elephant hit capsules
  - why: Mostly a war-ram design change and elephant hit-capsule art data (out of scope), but it corrects an engine fact that the 1.4.x docs still state wrongly.
  - port notes: Take only the IsInBeingStruckAction correction: MBMath.IsBetween is half-open, so the 48..52 band reads 48 to 51 and actt_mount_strike (52) is NOT read as being struck (verified on v1.4.8: MBMath.cs:295-299, Agent.cs:1338-1346). git grep on origin/bannerlord-1.4.5 finds the wrong claim at CLAUDE.md:214, docs/features/war-ram.md:175-176, docs/ai-includes/creature-mount-authoring.md:66, .claude/skills/new-creature-mount/SKILL.md:37 (not fixed by this commit), the docs/community custom_creature guides, docs/modding/recipe-add-a-race-or-creature.md and lessons/animation-skeleton.md; fix every site. Optionally take the Kit-authoring updates to docs/reference/bannerlord-skeleton-authoring.md and bannerlord-animation-clip-flags.md (both exist on 1.4.x). Leave SingleVictimPick, the WarRam config and behaviour, elephant capsule data, the ram bardings test and the Kit tools (skeleton_hit_capsules.py and check_rdc_entries.py are post-fork 1.5.x tools).
  - conflicts: tools/check_rdc_entries.py
  - issues: 618, 624

### group `issue-635-herorace-ctd-triage`  P3  track=docs-engine  commits=1  sim: clean=0 conflict=1
- `aed9dea8d` 2026-09-22 PORT P3 pick=conflict :: docs(herorace): v2.0.30 - #635 CTD triage, voice-over doc corrected
  - why: Docs-only triage of the #635 silent CTD: an engine doc section on the encyclopedia layer and conversation tableau, a hero-race note on the unguarded MapConversationTableau render path and a corrected kingdom-voices claim; all six files exist on the 1.4.x line.
  - port notes: The voice-over correction holds on v1.4.8 (DefaultVoiceOverModel.cs:90 builds ".+" + accentClass + "_.+", read in the dump). Re-check the gauntletui-viewmodel-screen.md claims (encyclopedia idles the map state, the tableau ticks only while its widget renders, latent OnTick NRE) against the v1.4.8 dump before porting. CHANGELOG and the LESSONS-LEARNED counts are bookkeeping conflicts.
  - issues: 635

### group `race-face-morph-docs`  P3  track=docs-engine  commits=1  sim: clean=0 conflict=1
symptoms: crash
- `0934f65d2` 2026-09-27 PORT-PARTIAL P3 pick=conflict :: Add unit tests for eye follow and skin restoration functionality
  - why: Mostly face-morph authoring tools, a female dwarf art restore and a 'saruman' race entry (content), plus a reference doc naming a native crash class (the static face morph reading a null morph buffer) with a v1.4.7 offset that corrects the 1.4.x diagnosis of the female dwarf crash.
  - port notes: Take only the engine sections of docs/reference/race-face-and-hand-morphs.md (face parts picked by tag; the static face morph AV, given as +0x58232C on v1.4.7 and +0x57070C on v1.5.3, where the faulting address divided by 6 is the vertex count naming the mesh), re-deriving the v1.4.8 offset, plus the #385 correction in docs/features/arena.md and the snapshot README (1.4.x arena.md:25-28 still blames the underwear mesh name). Trim the doc's tool table to tools present on 1.4.x (tools/blender/add_face_morph_channels.py is not). Leave the cultures.json 'saruman' entry, eye_follow.py, fit_eye_morphs.py, check_eye_follow.py, export_face_morphs.ps1, tools/oneoff/restore_adult_woman_dwarf.py (an art restore; the crash stopgap dccbb9a3b is already an ancestor of origin/bannerlord-1.4.5), the skins snapshot and the community guides.

### group `adopt-mithrilforge`  P2  track=docs-engine  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/tpac_clone_metamesh.py
- `991f415f4` 2026-09-29 PORT-PARTIAL P2 pick=conflict :: docs(mithrilforge): v2.0.32 - adopt prop recipe, gate the camp tpacs
  - why: Adopts MithrilForge's static-prop tpac recipe and clip findings into the engine docs, and adds a gate that every PlaceCenteredPrefab mesh is packaged once in a sound tpac; the four camp tpacs (same blobs) and the mesh constants are identical on origin/bannerlord-1.4.5.
  - port notes: Take tools/tests/test_prefab_asset_packages.py together with tools/tpac_clone_metamesh.py (only that file and its test from the Spider-mount feature commit e1a940ac9, plus this commit's lazy lz4/xxhash import; the gate needs tcm.parse and HEADER_SIZE only). Take docs/reference/tpac-static-prop-authoring.md and the additions to clip-flags, engine-and-toolchain, the UE pipeline doc and the provenance register after re-checking their engine claims on v1.4.8. The bannerlord-animation-system-map.md hunk needs that doc, which 30abf550b created; port it only with that doc. Port the field-camp.md limitation that the prefabs render nowhere only after confirming it on a 1.4.8 build ('[FieldCamp] placed 1' means the prefab stood): this laptop's installed v2.0.28 TAOM has all four camp tpacs in AssetPackages beside pack0.tpac, plus a loose Assets/ tree. The adoption review and RCA records are optional.
  - prereqs: e1a940ac9, 30abf550b
  - conflicts: tools/tpac_clone_metamesh.py

### group `docs-armory-mirror-policy`  P3  track=docs-engine  commits=1  sim: clean=0 conflict=1
- `8af881f2b` 2026-09-30 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.32 - the mirror is Mike's to sync; elephant skeleton path
  - why: Only the toolchain doc correction applies on 1.4.x: elephant_skeleton lives in adod_elephant_geo.tpac, and elephant_harad_armor_01_geo.tpac is a stub. The other three files are either absent on 1.4.x or lack the step being reworded.
  - port notes: Take the docs/reference/bannerlord-engine-and-toolchain.md fix; the stale claim is at line 260 on origin/bannerlord-1.4.5. Verify the tpac facts on the desktop's loose Armory first. Leave the armory-audit skill and docs/features/armory-ref-audit.md (absent on 1.4.x), and the ranged-ladders.md step 7 rewrite (the 1.4.x recipe has no 'commit the mirror' step).

### group `docs-arena-tournament-armour`  P3  track=docs-engine  commits=1  sim: clean=0 conflict=1
- `fe6619615` 2026-10-05 PORT-PARTIAL P3 pick=conflict :: docs(arena): v2.0.34 - vanilla tournament armour, v1.5.4 checks
  - why: Corrects the Arena docs: vanilla dresses fighters in the host culture's practice kit only in arena practice fights, while a tournament match uses each fighter's own RandomBattleEquipment.
  - port notes: The claims hold on v1.4.8. DefaultTournamentModel.cs:86-93 returns the gear_practice_dummy equipment only when the CampaignMission mode is not Tournament; TournamentBehavior.cs:166 sets MissionMode 7 (Tournament); TournamentFightMissionController.cs:358 copies GetParticipantArmor. TournamentGame.GetParticipantCharacters also has six direct callers on v1.4.8. Take arena.md, tournament-armor-assignment.md, the NPC handbook line, the Patch69 registry entry and the gamemodels-services lesson, after checking the text about TAOM's override against 1.4.x's TaomTournamentModel. Skip race-abilities.md, tournament-rewards.md and the v1.5.4 notes in the Armory snapshot README.
  - issues: 733

### group `docs-helmet-hair-beard-cover`  P3  track=docs-engine  commits=1  sim: clean=0 conflict=1
- `75b7b6661` 2026-10-05 PORT-PARTIAL P3 pick=conflict :: Refactor code structure and remove redundant sections for improved readability and maintainability
  - why: Misnamed commit: it adds a reference for helmet hair_cover_type and beard_cover_type with figures, an Armory beard-cover change ledger, an updated Patch69 caller comment and translation-cache churn for Arnor helmet names.
  - port notes: Take docs/reference/helmet-hair-beard-cover.md with its two images and the INDEX row; its engine facts hold on v1.4.8 (ArmorComponent.cs:190-191 parses both attributes with ignoreCase). The six-caller list in the Patch69 comment also holds on v1.4.8 (callers verified in TournamentBehavior, FightTournamentGame twice, TournamentManager and HeroKnownInformationCampaignBehavior); reword its v1.5.3/v1.5.4 reference and drop the Tournament Rewards mention. Skip docs/reference/lotrlome-beard-cover-changes.md (an Armory content ledger), the items-armor.md line if it points at that ledger, and the translation_cache edits (content).

### group `issue-407-patch69-comment`  P3  track=docs-engine  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/Arena/Hooks/Patch69_TournamentRosterGuard.cs
- `2b6f6216c` 2026-10-05 PORT-PARTIAL P3 pick=conflict :: docs(arena): v2.0.34 - Patch69 comment points to its registry entry
  - why: Comment-only fix that makes Patch69's class comment point at the registry instead of carrying a drifting caller list; the 1.4.5 comment still claims four callers where v1.4.8 has six.
  - port notes: Take the Patch69_TournamentRosterGuard comment rewrite plus the registry's 'Call frequency' paragraph, which on 1.5.x arrived inside 75b7b6661 and is missing from the 1.4.5 Patch69 row (docs/reference/harmony-patch-registry.md:611-625). Retag it for v1.4.8: the six callers exist in C:\Decompiled_Bannerlord\_categories_v1.4.8 (HeroKnownInformationCampaignBehavior.cs:51, FightTournamentGame.cs:80 and :332, TournamentManager.cs:144, TournamentBehavior.cs:105 and :115). Lesson and RCA files are optional bookkeeping.
  - prereqs: 75b7b6661
  - conflicts: Main/Features/Arena/Hooks/Patch69_TournamentRosterGuard.cs
  - issues: 407

### group `docs-engine-mithrilforge-findings`  P2  track=docs-engine  commits=1  sim: clean=0 conflict=1
symptoms: save-time, memory, loading-hang
- `b5e1d0b3d` 2026-10-06 PORT-PARTIAL P2 pick=conflict :: docs(engine): v2.0.34 - fold in MithrilForge's newer engine findings
  - why: Engine knowledge-base update whose facts bear on the brief's symptoms (save cost and the post-save forced GC, a map-view GPU leak, an AfterStart throw that loops the loading screen, off-screen skeleton posing), tagged v1.5.3/v1.5.4, so each claim needs a v1.4.8 re-check before it lands.
  - port notes: Verified on v1.4.8 this session: MissionState.FinishMissionLoading (MissionState.cs:333-352) clears _missionInitializing before Mission.AfterStart (:345) and TickLoading (:221-233) then re-runs LoadMission, so an AfterStart throw reloads forever on the managed side; Mission.GetDefendCollisionResults is [MBCallback(null, true)] (Mission.cs:6455), so the new harmony-patches.md thread row applies to 1.4.x's TaomCombatMechanicsModel.DecideCrushedThrough. Not re-checked on v1.4.8: save numbers and Game.OnSaveCompleted's Common.MemoryCleanupGC, the map render-target leak and BannerTexturedMaterialCache growth, visibility-gated posing, limb-ray and clip-reader facts. mission-frame-threads-and-native-costs.md comes from af990ca76; the orientation.md AfterStart row belongs in the 1.4.x CLAUDE.md Traps table; leave mission-perf-heartbeat.md, scripted-melee-strikes.md, bannerlord-animation-system-map.md and the plans/_audit file unless their parents come over. CustomAttacksUtils.cs change is a comment.
  - prereqs: af990ca76
  - issues: 739

### group `docs-lords-xml-merge`  P3  track=docs-engine  commits=1  sim: clean=0 conflict=1
- `369eb0306` 2026-10-08 PORT P3 pick=conflict :: docs(lords): v2.0.34 - lords in both files merge, not override
  - why: Corrects the lord docs' claim that a lord in both lords.xslt and characters/lords.xml uses only the lords.xml copy, describing the engine's per-attribute merge and equipment-set union instead; the 1.4.5 copies still carry the wrong claim.
  - port notes: Wrong claim on 1.4.5 at docs/ai-includes/lord-skills-authoring.md:48, docs/modding/skill-sets.md and the lord-skills SKILL.md; MBObjectManager.MergeElements exists on v1.4.8 (MBObjectManager.cs:820). The link to lesson #644 in docs/reviews/lessons/xslt-moduledata.md has no target on 1.4.5: bring the lesson or drop the link. If d6b537092 lands first, the SKILL.md hunk disappears.
  - issues: 644

### group `refuge-ai-pin-docs`  P3  track=docs-engine  commits=1  sim: clean=1 conflict=0
- `63630e8a0` 2026-10-14 PORT P3 pick=clean :: fix(refuge): v2.0.34 - the engine saves the AI pin
  - why: 1.4.x RefugeService.cs:627 also claims SetDoNotMakeNewDecisions is not persisted, but v1.4.8 saves it (MobilePartyAi.DoNotMakeNewDecisions is [SaveableProperty(11)], MobilePartyAi.cs:124-125); comment, test-message and doc correction with no behaviour change.
  - port notes: Clean pick. Replace the v1.5.5 citation in comments and commit text with v1.4.8 MobilePartyAi.cs:124-125.
  - issues: 792

### group `yotthani-adopt-2026-10-15-docs`  P3  track=docs-engine  commits=5  sim: clean=0 conflict=5
symptoms: crash, save-time, memory
- `2b8f5ff36` 2026-10-15 PORT-PARTIAL P3 pick=conflict :: docs(animation): v2.0.34 - companion clips, agent-less figures, Kit
  - why: Engine and Modding Kit facts including a CTD trap for the shared live Armory (a rope-flagged clip with no {clip}_weapon companion is an access violation), but the trap was verified only on v1.5.3 and v1.5.5 native code.
  - port notes: Take the bannerlord-animation-clip-flags.md note, the tools/README and animation-lesson rope-flag lines, and the agent-spawn-and-render-pipeline.md and animation-binding-and-playback.md additions, tagged v1.5.x-verified and v1.4.8 UNVERIFIED (entry 0x701450 is a v1.5.5 native address). Skip tpac-static-prop-authoring.md and bannerlord-animation-system-map.md (absent on 1.4.x) and the kitbash runes and Gondor armory audit lines.
- `d690ff8d2` 2026-10-15 PORT-PARTIAL P3 pick=conflict :: docs(battle): v2.0.34 - measured battle AI and morale constants
  - why: The formations-and-team-ai.md additions (first tactic decision, the 1.5x switch bar, casualty-shock constants) describe engine code largely shared with v1.4.8, while the other four files document 1.5.x-only features.
  - port notes: Take only docs/reference/engine/formations-and-team-ai.md. The morale table's AgentMoraleInteractionLogic refs (:12, :53, :64) match the v1.4.8 file; the TeamAIComponent and CustomBattleMoraleModel/SandboxBattleMoraleModel line refs still need a v1.4.8 re-read; keep yotthani's measurements tagged v1.5.5. Skip culture-doctrine, melee-damage-model, race-abilities and signature-strikes docs (absent on 1.4.x).
- `4d79eca85` 2026-10-15 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.34 - adopt yotthani 2026-10-15, traps and lessons
  - why: Its lifecycle rules hold on v1.4.8 (a throw in the main-menu hook repeats every tick; removing a behaviour inside OnMissionTick double-ticks or skips a neighbour), and the second describes live 1.4.x code, where ShaderPrecompilation is active and ShaderPrecompilePlayerAgentGuard.OnMissionTick calls FinishDeployment at line 94.
  - port notes: Take the mission-and-missionbehavior-lifecycle.md section with v1.4.8 refs (count-down loop Mission.cs:3741-3744, RemoveMissionBehavior :4643-4658, FinishDeployment DeploymentMissionController.cs:41-80), the submodule-lifecycle row with v1.4.8 refs (Module.cs:500-509, :750-759) and the general doc-batch lesson. On 1.4.x turn the shader-precompilation 'before un-parking' note into a fix item (set a flag in OnMissionTick, finish deployment next frame), since the feature is active there. Skip the VanillaTuning coexistence section, shader-compile-notice.md (1.5.x-only feature), the adopt-yotthani records and the provenance passes.
- `7eac30153` 2026-10-15 PORT-PARTIAL P3 pick=conflict :: docs(engine): v2.0.34 - 1.5.5 perf, native and coexistence findings
  - why: yotthani's v1.5.3 campaign and save measurements (a save costs about 4 s of still frame; server GC cuts the save work to 0.5-0.95 s at 1.4-1.7 GB more private RAM; settlement nameplates are the largest steady map cost) are leads for the 1.4.x save-time and memory deep-dive, unverified on v1.4.8.
  - port notes: Take the campaign-tick-time-and-party-ai.md, save-system.md, gauntletui-viewmodel-screen.md and diplomacy.md additions, tagged v1.5.3 [yotthani]. The save-system.md server-GC bullet this extends is itself absent on 1.4.x (no GC mention on origin/bannerlord-1.4.5), so port that earlier section with it. Skip the docs of 1.5.x-only features (butter-lib-distance-matrix, map-view-release, mission-perf-heartbeat, mission-start-guard, nameplate-cull, skeleton-buffer-guard) and mission-frame-threads-and-native-costs.md unless those features are ported.
- `0bb1f3cae` 2026-10-15 PORT-PARTIAL P3 pick=conflict :: docs(engine): v2.0.34 - doc review fixes for the yotthani batch
  - why: Verified corrections to the 567-570 doc batch (a form feed in a registry path, restored upstream hedges, the removal-during-tick clarification, corrected counts and anchors).
  - port notes: Apply only the hunks that touch text ported from 567-570; skip hunks in files absent on 1.4.x and the adoption record.
  - prereqs: 2b8f5ff36, d690ff8d2, 4d79eca85, 7eac30153

### group `yotthani-adoption`  P3  track=docs-engine  commits=1  sim: clean=0 conflict=1
- `8dd76ddb3` 2026-10-16 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.34 - adopt yotthani 2026-10-16, RCA and lessons
  - why: An adoption-review docs commit whose engine facts (siege map event types and fief finalization order, clan wage payment and garrison recruitment, TacticHoldChokePoint scoring, texture first-draw costs) are cited against v1.5.5 and help 1.4.x only after re-verification.
  - port notes: Take the engine-fact hunks in docs/features/fief-granting.md, docs/reference/engine/settlement-economy-food-prosperity.md, formations-and-team-ai.md, gauntletui-viewmodel-screen.md (minus the MapViewRelease link) and the shader-precompilation.md note on unattended runs, each re-read against the v1.4.8 dump with line numbers updated; check the TAOM file:line cites (TaomPartyWageModel.cs:37, FiefSiegeCaptureRules.cs, SiegeDefenseBehavior.cs) against 1.4.x code. Leave docs/features/map-view-release.md (MapViewRelease is 1.5.x-only), both adopt-yotthani records, the upstream-review RCA, upstream-watch.json, and registry or provenance rows that point at 1.5.x code.

### group `docs-yotthani-adoption-oct29`  P3  track=docs-engine  commits=2  sim: clean=0 conflict=2
symptoms: memory, save-time, battle-freeze, shader-stall
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/lint_docs.py
- `2ac85fbe7` 2026-10-29 PORT-PARTIAL P3 pick=conflict :: docs(yotthani): v2.0.36 - engine and campaign findings, Oct 29
  - why: Engine and campaign findings from yotthani's repos; most claims are tagged 1.5.3 to 1.5.6, and several are leads for the 1.4.x memory, save-time, freeze and shader deep dive.
  - port notes: Docs only. Port the claims that are re-checked on v1.4.8. The listener-order paragraph in docs/reference/engine/campaign-tick-time-and-party-ai.md already says 'v1.4.8 the same' (verified: MbEvent.cs:24-30 prepends). Bring these as hedged leads, with their 1.5.x version tags, until the 1.4.x deep dive checks them: the map-view render-target leak; server GC and the forced collect after a save; shaders built on first use after a graphics change; the battle-start hang with reserves near the agent limit; the 242,272-line physics-contact log burst. Several target docs do not exist on 1.4.x and depend on earlier 1.5.x docs commits: mission-frame-threads-and-native-costs.md, map-view-release.md, map-perf-profiler.md, external-mod-couplings.md and head-mesh-and-groom-authoring.md. The save-system.md 'what a save costs' section that the new bullet extends is also absent on 1.4.x. Do not carry the 'a missing action set returns the default set' corrections without a v1.4.8 native check (see batch notes). Skip the guide edits for 1.5.x-only creatures and the adoption record, unless the whole yotthani adoption is ported.
- `ac1d8edbc` 2026-10-30 PORT-PARTIAL P3 pick=conflict :: docs(harness): v2.0.36 - two more trap lines in the orientation
  - why: Raises entry-doc budgets that exist only on 1.5.x and adds two trap rows; only the event-listener-order fact (true on v1.4.8, MbEvent.cs:24-30) carries over.
  - port notes: The lint_docs.py budget bumps, ADR-011 and docs/ai-includes/orientation.md exist only on 1.5.x. ADR-011 and orientation.md arrived in c79a58521, and 1.4.x's lint_docs.py has no ENTRY_DOCS_MAX_BYTES or TRAP_INDEX constants. Port only the 'Event listener order: newest first' fact, as a row in 1.4.x's CLAUDE.md Traps table pointing at the campaign-tick paragraph from 2ac85fbe7. Skip the 'Other mods' row unless external-mod-couplings.md is ported: its four yotthani mods were checked against TAOM v2.0.36 on 1.5.6, and many of its rows name 1.5.x-only features.
  - prereqs: 2ac85fbe7
  - conflicts: tools/lint_docs.py

### group `harness-destructible-stage-rule`  P3  track=docs-engine  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Client/ClientSubModule.cs, Client/TaomClientComposition.cs, Main/Adapters/FordsBattlefieldAdapter.cs, Main/Adapters/FordsSceneAdapter.cs, Main/Adapters/FordsTowerAdapter.cs, Main/Adapters/FordsUnitSample.cs, Main/Adapters/IFordsSceneAdapter.cs, Main/Adapters/IFordsTowerAdapter.cs, Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs, Main/Features/FordsOfWar/Config/FordsMatchConfigReader.cs, Main/Features/FordsOfWar/Diagnostics/FordsFieldSample.cs, Main/Features/FordsOfWar/Diagnostics/FordsLogLine.cs, Main/Features/FordsOfWar/Diagnostics/FordsRunEvidence.cs, Main/Features/FordsOfWar/Diagnostics/FordsRunReport.Mission.cs, Main/Features/FordsOfWar/Diagnostics/FordsRunReport.Start.cs, Main/Features/FordsOfWar/Diagnostics/FordsRunReport.cs, Main/Features/FordsOfWar/Diagnostics/FordsTowerSample.cs, Main/Features/FordsOfWar/Diagnostics/FordsVerdicts.Combat.cs, Main/Features/FordsOfWar/Diagnostics/FordsVerdicts.Field.cs, Main/Features/FordsOfWar/Diagnostics/FordsVerdicts.Start.cs, Main/Features/FordsOfWar/Diagnostics/FordsVerdicts.cs, Main/Features/FordsOfWar/Domain/FordsLaneConfig.cs, Main/Features/FordsOfWar/Domain/FordsMatch.cs, Main/Features/FordsOfWar/Domain/FordsMatchConfig.cs, Main/Features/FordsOfWar/Domain/LaneGeometry.cs, Main/Features/FordsOfWar/Domain/LaneMarchRules.cs, Main/Features/FordsOfWar/Domain/LaneSituation.cs, Main/Features/FordsOfWar/Domain/LaneSnapshot.cs, Main/Features/FordsOfWar/FordsBaseFinder.cs, Main/Features/FordsOfWar/FordsFieldProbe.cs ...
- `db8f7c10e` 2026-11-01 PORT-PARTIAL P3 pick=conflict :: feat(fords-of-war): v2.0.36 - towers fall, unattended test runs
  - why: A Fords of War feature commit whose only portable part is engine research: a DestructableComponent's shown damage stage must only move forward, which matches the v1.4.8 dump.
  - port notes: Take ONLY the DestructableComponent paragraph of docs/reference/engine/usable-machines.md (re-cite v1.4.8: DestructableComponent.cs:87 declares OnCalculateDestructionStateIndex, :399-412 CalculateNextDestructionLevel consults it, :498 reads _destructionStates[_currentStateIndex - 1]) and the 'Never drive a one-way engine state backward' entry of docs/reviews/lessons/adapters-taleworlds-api.md, with the 1.5.x-only RCA links removed. Everything else is Fords of War (OUT), including the new CrashReportPatchHelper.Swallowed event, whose only subscriber is the Fords run report, and tools/run_fords_autorun.py.
  - conflicts: Client/ClientSubModule.cs, Client/TaomClientComposition.cs, Main/Adapters/FordsBattlefieldAdapter.cs, Main/Adapters/FordsSceneAdapter.cs, Main/Adapters/FordsTowerAdapter.cs, Main/Adapters/FordsUnitSample.cs, Main/Adapters/IFordsSceneAdapter.cs, Main/Adapters/IFordsTowerAdapter.cs, Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs, Main/Features/FordsOfWar/Config/FordsMatchConfigReader.cs, Main/Features/FordsOfWar/Diagnostics/FordsFieldSample.cs, Main/Features/FordsOfWar/Diagnostics/FordsLogLine.cs, Main/Features/FordsOfWar/Diagnostics/FordsRunEvidence.cs, Main/Features/FordsOfWar/Diagnostics/FordsRunReport.Mission.cs, Main/Features/FordsOfWar/Diagnostics/FordsRunReport.Start.cs, Main/Features/FordsOfWar/Diagnostics/FordsRunReport.cs, Main/Features/FordsOfWar/Diagnostics/FordsTowerSample.cs, Main/Features/FordsOfWar/Diagnostics/FordsVerdicts.Combat.cs, Main/Features/FordsOfWar/Diagnostics/FordsVerdicts.Field.cs, Main/Features/FordsOfWar/Diagnostics/FordsVerdicts.Start.cs
- `c2c3a6f28` 2026-11-01 PORT P3 pick=conflict :: chore(rules): v2.0.36 - destructible damage stages move forward only
  - why: Adds an adapters rule: never raise a damaged DestructableComponent's hit points unless OnCalculateDestructionStateIndex keeps the shown stage moving forward; the engine claim holds in the v1.4.8 dump.
  - port notes: Applies cleanly to 1.4.x .claude/rules/adapters.md (no destructible section there today). Its link target is the usable-machines.md paragraph taken from db8f7c10e, so port the two together. Preventive only: git grep on origin/bannerlord-1.4.5 Main finds no TAOM write to a destructible's hit points.
  - prereqs: db8f7c10e

### group `feature-long-defeat`  P3  track=docs-engine  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/LongDefeat/Spike/LongDefeatSpikeOptions.cs, Main/Features/LongDefeat/Spike/LongDefeatSpikeRunner.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs, tools/run_long_defeat_spike.py, tools/tests/test_run_long_defeat_spike.py
- `985b0d54e` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: fix(long-defeat): v2.0.36 - waves end, and the unattended spike passes
  - why: The code is Long Defeat only, but one lesson it adds states an engine fact that holds on v1.4.8: the siege team AI picks InnerGate by tag without checking visibility, and a hidden CastleGate never opens, which can hold a gate lane closed in vanilla sieges as well.
  - port notes: Take only the lesson 'The engine's gates ignore visibility...' into docs/reviews/lessons/adapters-taleworlds-api.md, which exists on 1.4.x. Verified in the v1.4.8 dump:
- TeamAISiegeComponent.cs:56-57 picks OuterGate and InnerGate by tag from all active gates.
- CastleGate.cs:619-621 returns from OnTick while the entity is hidden.
- TeamAISiegeComponent.cs:184 counts a gate lane as open only when InnerGate is null or open.
Whether the Helm's Deep scene the 1.4.x campaign uses hides an inner gate is UNVERIFIED; the 1.5.x run found one at height 32.5. Leave all Long Defeat code, tests, tools and docs.
  - conflicts: Main/Features/LongDefeat/Spike/LongDefeatSpikeOptions.cs, Main/Features/LongDefeat/Spike/LongDefeatSpikeRunner.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs, tools/run_long_defeat_spike.py, tools/tests/test_run_long_defeat_spike.py
  - issues: 1079
skipped in this group: `10e2efb49` SKIP-FEATURE fix(long-defeat): v2.0.36 - soldiers spawn where the engine places the; `8cbfa8333` SKIP-FEATURE docs(long-defeat): v2.0.36 - the hidden gate fix proved, Phase 1 plan; `a05500f61` SKIP-FEATURE feat(long-defeat): v2.0.36 - Phase 1: waves, gold, shop at Helm's Deep

### group `feature-longdefeat`  P3  track=docs-engine  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/LongDefeat/Domain/FieldFacts.cs, TAOM.Tests/Features/LongDefeat/Domain/SurvivalRunTests.cs
- `b54179389` 2026-11-04 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.36 - which scenes hide an inner gate
  - why: Adds a reach note to a LongDefeat lesson: 5 of 30 TAOM_Map castle-gate scenes hide an inner gate, which 1.4.x campaign sieges share, and the engine half holds on v1.4.8 (TeamAISiegeComponent.cs:56-57 picks gates by tag with no visibility check; CastleGate.cs:619-621 skips OnTick for a hidden gate).
  - port notes: The parent lesson (Long Defeat spike, rca-long-defeat-spike-2026-11-03.md) is 1.5.x-only, so port the finding as a standalone lesson or trap entry about campaign sieges. Re-scan the installed TAOM_Map scenes on a full-content machine first: the five scene ids come from the desktop's live install. Whether a campaign siege actually stalls on taom_rohan_castle_helms_deep_forceatmo or taom_gondor_castle_001_forceatmo (no other inner gate) is UNVERIFIED on either line.
- `44058ce4b` 2026-11-04 PORT-PARTIAL P3 pick=conflict :: docs(long-defeat): v2.0.36 - the review's lessons and last leftovers
  - why: LongDefeat review leftovers plus three lessons, two of them general enough for 1.4.x work: Gauntlet colours parse #RRGGBBAA (red first, alpha last; v1.4.8 Color.cs:180-191), and a fake that returns 0 hides its seam.
  - port notes: Take the localization-ui.md and testing-qa.md lessons, re-anchored because rca-long-defeat-phase1-2026-11-04.md is absent on 1.4.x, with the colour cite corrected to v1.4.8 Color.cs:180-191 (the lesson cites 172-183). The modal-root HitTest claim (EventManager.AnyWidgetsAt, GauntletLayer.HitTest) is UNVERIFIED on v1.4.8. The adapters lesson (copy a sibling's vetoes, IsPhotoModeAllowed) is anchored in FordsOfWar and LongDefeat; take only its generic prevent rule, if anything. None of the three lessons exists on 1.4.x. Leave FieldFacts.cs and SurvivalRunTests.cs (LongDefeat code).
  - conflicts: Main/Features/LongDefeat/Domain/FieldFacts.cs, TAOM.Tests/Features/LongDefeat/Domain/SurvivalRunTests.cs
skipped in this group: `758c8f6fc` SKIP-FEATURE feat(long-defeat): v2.0.36 - the Phase 0 spike for wave survival; `3564d9782` SKIP-FEATURE docs(long-defeat): v2.0.36 - correct the proof run's folder name; `8b7961dd7` SKIP-FEATURE feat(long-defeat): v2.0.36 - the garrison rests between waves; `0195d0fd7` SKIP-FEATURE docs(long-defeat): v2.0.36 - a boon pick every 3 waves; `eab59cbd4` SKIP-FEATURE fix(long-defeat): v2.0.36 - a wave no longer waits on its last few; `744159870` SKIP-FEATURE docs(long-defeat): v2.0.36 - what the runs measured, Phase 1 landing

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 2, batch 3, batch 10, batch 11, batch 12, batch 13, batch 14, batch 16, batch 18, batch 19, batch 30, batch 32, batch 34, batch 35
