# Triage check: June-harvest re-triage (32 removal verdicts)

Checker for run `2026-09-23-opus`, baseline `b2e387db`. Scope: every FIXED or STALE verdict from
triage-A/B/C (listed in the orchestrator's `checker-items.json`), each re-read against the original
finding in `plans/_audit/2026-06-12-harvest.md` and against HEAD. Then an impact calibration over the
67 STILL_VALID entries. Read-only; no build or test was run.

Format per item: bucket, harvest heading line, id, original verdict, my verdict, overturned, evidence.

### 1. A L11 CORRECTNESS-01 (CareerDataService never reset on new campaign)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** `git show b2e387db:Main/Features/CareerSystem/CareerPersistenceBehavior.cs` lines 20-35 register `CampaignEvents.OnNewGameCreatedEvent` calling `_dataService.ResetForNewCampaign()`; `CareerDataService.cs:98-103` swaps in a fresh dictionary. Ordering checked in the installed engine: `~/.taom-src/v1.5.3/TaleWorlds.CampaignSystem.Campaign.cs:1709` calls `OnNewGameCreated` inside the `NewCampaign` branch of the loading state machine, before `GameManager.OnAfterGameInitializationFinished` (the step that opens character creation), so the reset cannot wipe the CC career. `f4273639` is an ancestor of HEAD.
- **Residual (not a reason to overturn):** loading a save with no `_taom_career*` keys still keeps the prior campaign's dictionary; that is the CORRECTNESS-03 (L56) class, which stays STILL_VALID in triage-A.

### 2. A L30 CORRECTNESS-04 (TroopWeight count hooks: GetHashCode cache, no eviction, swallowed exceptions)
- **Original:** STALE. **Mine:** STALE. **Overturned:** no.
- **Evidence:** `git ls-tree -r --name-only HEAD Main/Features/TroopWeight/` lists no `PartyBaseNumberOfAllMembersHook.cs` or `PartyBaseNumberOfRegularMembersHook.cs`; `git grep -E "NumberOfAllMembers|NumberOfRegularMembers" HEAD -- Main` shows only plain reads (no patch), and `SettlementFood/TownFoodSnapshot.cs:13` documents the getter is raw again after the count-to-limit rework (`bee07b48`). Surviving party caches are `ConditionalWeakTable` (`TroopWeightService.cs:23,101`). All 10 `catch (Exception ex)` blocks in `Main/Features/TroopWeight` log a warning or error (`git grep -A2 catch HEAD -- Main/Features/TroopWeight`), so the swallow half is gone too.
- **Nit:** `TroopWeightService.cs:22` comment still contrasts with "the hashcode-dict caches in the count hooks", which no longer exist.

### 3. A L39 CORRECTNESS-05 (TroopWeightXmlLoader admits NaN/Infinity)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** `git show HEAD:Main/Features/TroopWeight/TroopWeightXmlLoader.cs` line 86 `if (!FiniteFloatValidator.IsFinite(weight) || weight <= 0)` sits between `TryParse` (76) and the store (95). Test `TAOM.Tests/Features/TroopWeight/TroopWeightXmlLoaderTests.cs:121-139` feeds `weight="NaN"` and `"Infinity"` and asserts the entries are skipped with a warning. The downstream consumer also guards the cast (`TroopWeightService.cs` `ApplyPartySizeWeightPenalty`, `IsFinite(limit.ResultNumber)`).

### 4. A L102 SEC-02 (vendored UIExtenderEx nuget.config holds an upstream credential)
- **Original:** STALE. **Mine:** STALE. **Overturned:** no.
- **Evidence:** `ls -la Dependencies/.vendor-source/` holds six `.tar.gz` files only (22 MB by `du -sh`); no extracted tree and no loose `nuget.config` anywhere in the working tree (`find . -iname nuget.config` outside `.git`: none). The file survives only inside the archive (`tar -tzf uiextenderex-v2.13.2.tar.gz | grep -i nuget.config` gives `Bannerlord.UIExtenderEx-2.13.2/src/nuget.config`); the directory is ignored (`git check-ignore -v`: `.gitignore:151`). Tracked mentions of `ClearTextPassword` are only in `plans/005-security-hygiene.md` and the two harvest files, and I checked that they carry placeholders or descriptions, not the value (redacted grep; value not reproduced).
- **Residual:** the June side ask (a `packageSourceCredentials|ClearTextPassword` sweep in the `/adopt-external` vet) was never added (`git grep` over `.claude/skills/adopt-external/`: 0 hits). Re-extracting the archive restores the June state. Hardening only; rotation is upstream's (BUTR) call.

### 5. A L124 SEC-04 (guard the CI build job against fork-PR execution)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** `git show HEAD:.github/workflows/build.yml`: `workflow_dispatch:` declared at line 8; the C# job is `runs-on: [self-hosted, windows]` (253) with `if: ${{ vars.BANNERLORD_GAME_DIR != '' && github.event_name != 'pull_request' }}` (259) and a comment explaining PRs are excluded (255-258). The only other workflow, `doc-budget.yml`, runs on `ubuntu-latest` (GitHub-hosted), so no PR-triggered job reaches the workstation runner. No `pull_request_target` or `workflow_run` in either file.
- **Residual:** a collaborator's push to `bannerlord-1.4.5` would still run on the self-hosted runner; that is outside this finding (fork PRs). The "no C# CI on 1.5.x" gap is seed F1.

### 6. A L158 PERF-03 (TroopWeight postfixes re-resolve TaomSettings.Instance per member-count read)
- **Original:** STALE. **Mine:** STALE. **Overturned:** no.
- **Evidence:** The two hot count-getter patches are gone (item 2). I checked whether the defect moved with the code: the successor read is `TroopWeightService.ApplyPartySizeWeightPenalty` (`TaomSettings.Instance?.EnableTroopWeight` at `TroopWeightService.cs:105`), reached from `TaomPartySizeModel.GetPartyMemberSizeLimit`. The engine caches that call per roster version: `~/.taom-src/v1.5.3/TaleWorlds.CampaignSystem.Party.PartyBase.cs:356-367` recomputes `PartySizeLimit` only when `MemberRoster.VersionNo` changes. So the settings read is no longer per member-count read; it did not move to an equally hot path. The six remaining reads are UI refresh and upgrade patches.

### 7. A L175 PERF-05 (TroopWeight count-hook caches GetHashCode-keyed, unbounded)
- **Original:** STALE. **Mine:** STALE. **Overturned:** no.
- **Evidence:** Same deletion as item 2 (`bee07b48`); the two party caches that exist at HEAD are `ConditionalWeakTable<PartyBase, ...>` (`TroopWeightService.cs:23` `_healthCache`, `:101` `_lastBaseLimit`). No `GetHashCode` call in `Main/Features/TroopWeight` (only the stale comment at `:22`).

### 8. A L193 PERF-06 (SpatialGrid.GetAgentsInRadius scans all cells and allocates per query)
- **Original:** FIXED. **Mine:** STILL_VALID (partly fixed). **Overturned:** yes. **Impact:** LOW.
- **Evidence:** The scan half is fixed: `git show HEAD:Main/Features/AdvancedCombat/SpatialGrid.cs` lines 99-129 enumerate only the bounding-box cells with `TryGetValue`. The allocation half is not: the buffer overload (`:99`, `:142`) exists, but three of the June finding's own cited callers still use the allocating overload, which does `new List<Agent>()` per call (`:86-91`): `Main/Features/Warg/BehaviorTreeElements/NoEnemyCloseDecorator.cs:17` (radius 60, every `Evaluate`, no throttle), `PeriodicallyCheckIfCanAttackAnyone.cs:23` (0.2 s throttled) and `:52` (`CheckOnceIfCanAttackEnemy`, unthrottled). `Main/Adapters/AgentAdapter.cs:207` and `SpatialGridDebugService.cs:16` also allocate. Only the spider moved to the buffer form (`SpiderEngageDecorator.cs:50`, and `AgentAdapter.cs:255`). Measured with `git grep -n -E "GetAgentsInRadius|GetNearAliveAgentsInRange" HEAD -- Main`. triage-A itself flagged this as unchecked.
- **Note:** GC churn only, no correctness risk. Fold into PERF-07 / seed F4: the same three warg files need touching for the per-eval `IoC.Resolve` anyway.

### 9. B L449 DEPS-06 (abandoned 89 MB `.vendor-source` tree, UIExtenderEx 2.13.2 source)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** `ls -la Dependencies/.vendor-source/` (dir mtime 2026-08-05): six tarballs, 22 MB (`du -sh`), no extracted source trees, so Glob and Grep no longer land in 2.13.2 source (the June harm). Still ignored by `.gitignore:151`. Residue: tarballs lag the pins (UIExtenderEx 2.13.3, MCM 5.12.3 at `Dependencies/TAOM.Dependencies.csproj:71-72`); untracked local disk, user action only.

### 10. B L464 DEPS-03 (taom-src SKILL.md describes v1.3.15 and inverts the dump guidance)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** `git show HEAD:.claude/skills/taom-src/SKILL.md` line 3 (eager description) names no version; line 9 says the script auto-detects from `Version.xml`, caches at `~/.taom-src/<version>/`, and warns against guessing from the dump because it "can lag the installed engine", matching AGENTS.md "Research first". Rewrite in `0fc9b8c1` (ancestor of HEAD).
- **Residual:** line 9 still says "currently v1.5.2" while `.claude/pinned-game-version.txt` is `v1.5.3`. That lag is carried by L423 (STILL_VALID in triage-B), so nothing is dropped.

### 11. B L472 DEPS-05 (dr3 stub rows contradict v99; csproj comment cites MCM 5.11.3)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no (with a caveat).
- **Evidence:** Stub rows gone: `git show HEAD:docs/migration/dr3-maintenance.md` line 219 says the `<Version>` values "are NOT listed here" and points at the stubs and the v99 rule (231-240, enforced by `BundledDependencyManifestTests`). Stubs at HEAD: Harmony `v2.4.99.0`, UIExtenderEx `v2.13.99.0`, ButterLib `v2.12.99.0`, MBOptionScreen `v5.12.99.0`. The literal "5.11.3" is gone from the csproj.
- **Caveat:** the comment-drift defect recurred with new numbers: `Dependencies/TAOM.Dependencies.csproj:59-60` names "ButterLib 2.11.0, UIExtenderEx 2.13.2, MCMv5 5.12.1" against pins UIExtenderEx 2.13.3 and MCM 5.12.3 (`:71-72`) and a ButterLib 2.12 stub. triage-B logged this under L440 (STILL_VALID), and L472 is a June duplicate of L440, so the live residue stays in the backlog through L440. If L440 is ever closed, this comment must go with it.

### 12. B L495 DEPS-04 (migration #210 closeout: 7 runtime punch-list sections unchecked; TRACKING.md self-references stale)
- **Original:** STALE. **Mine:** STILL_VALID (partly fixed). **Overturned:** yes. **Impact:** LOW.
- **Evidence:** Only the bookkeeping premise lapsed: `git show HEAD:docs/migration/TRACKING.md` line 8 records #210 closed 2026-08-08 as `obsolete-premise`, and line 57 fixes the "issue pending" row. But the same line 8 says the close "performed no validation" and "the residual in-game checks stay open and unticked in `s6-runtime-punchlist.md`". The punch list is unchanged since 2026-05-31 (`git log` on the file: `5a12eb54`, `41258657`), every box is `- [ ]` (lines 20-50), and with #210 closed nothing else owns it. The machine-local plan path the June finding cited is still at `TRACKING.md:21` and `:191`. The defect (unverified runtime risks with no owner) did not go away; only the issue that pointed at it did.
- **Per-item state read this run:** item 2 is half done (the CC prefab `Main/_Module/GUI/PreFabs/FacGen/PreBuildCharacterSelection.xml:38-81` is all `VerticalTopToBottom`, flipped in `ad836d11`; `Main/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs:24` still injects `VerticalBottomToTop`). Item 4 is partly addressed for the player only (`Main/Features/Diplomacy/DiplomacyService.cs:49-52` adds 1000 to clear the `CanMakeAlliance` 50-point threshold when the player is involved; AI-to-AI alliances unchanged). Item 6 is a documented choice in code (`SpecialResourcesBehavior.cs:376-381` ignores `battleEndState`). Items 1 and 7 are exercised de facto by months of play but not recorded. Items 3 and 5 are untouched.
- **Why overturn:** a STALE here removes the only backlog entry for the ruler-gear, AI-alliance and naval checks, which TRACKING.md itself says are still open.

### 13. B L504 DEPS-01 (installed engine v1.4.6, repo targets v1.4.5)
- **Original:** STALE. **Mine:** STALE. **Overturned:** no.
- **Evidence:** Installed `E:/Steam/.../bin/Win64_Shipping_Client/Version.xml` reads `v1.5.3`; `.claude/pinned-game-version.txt` is `v1.5.3`; `git show HEAD:Main/_Module/SubModule.xml` line 32 declares `DependedModuleMetadata id="Native" ... version="v1.5.3.*"`. The decompile dump has a matching `E:/Decompiled_Bannerlord/_categories_v1.5.3`, and `TRACKING.md:6` records each bump through `/engine-bump`. No `v1.4.5`/`v1.4.6` current-target claim remains in AGENTS.md, CLAUDE.md or `docs/ai-includes/agent-operating-manual.md` (`git grep`). The surviving same-class drift (skills saying v1.5.2) is L423's.

### 14. B L513 DEPS-04 (#210 open with a 7-item human punch list)
- **Original:** STALE. **Mine:** STILL_VALID (duplicate of L495, count once). **Overturned:** yes. **Impact:** LOW.
- **Evidence:** Same substance as item 12: the punch list (`docs/migration/s6-runtime-punchlist.md:20-50`) is the finding's content and is still fully unticked, now with no open issue. `TRACKING.md:49` in June is `:57` at HEAD and is fixed; nothing else in this finding is.

### 15. B L524 DX-01 (pre-commit build gate never blocks yet builds on every commit)
- **Original:** STALE. **Mine:** STALE. **Overturned:** no.
- **Evidence:** `git ls-tree --name-only HEAD .claude/hooks/` has no `check-build-before-commit.sh`; `block-no-verify.sh:8` says it shipped as that file. `git show HEAD:docs/reference/hooks-catalog.md` line 22 records the decision (build half dropped on 2026-08-20: hooks run in the main tree's cwd, and the build lacked `-p:DisableModuleCopy=true`). The only remaining text promising a build block is in `docs/changelog-archive/CHANGELOG-2026-H1.md:10111`, a historical archive. No live doc still promises the gate, so the "false sense of safety" half is gone too.

### 16. B L556 DX-07 (hooks hardcode the absolute repo path)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** `git grep -n -i -E "mikew|/repos/TAOM|c:/Users|E:/repos" HEAD -- .claude/hooks/` returns one hit, `_pybin.sh:6`, which is a comment naming the Store python alias, not a working path. The six hooks the finding named use `${CLAUDE_PROJECT_DIR:-$(pwd)}` (`session-start.sh:19`, `session-stop.sh:7`, `pre-compact.sh:5`, `post-compact.sh:6`, `detect-docs-gaps.sh:16`, `log-agent.sh:18`); the seventh (the build hook) was deleted. `.claude/settings.json` wires hooks by relative path (`.claude/hooks/...`).

### 17. C L605 DOCS-01 (Harmony Patch Categories table missing 11 categories)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** Re-measured: `git grep -h -o 'HarmonyPatchCategory("[^"]*")' b2e387db -- Main | sort -u` gives 94 categories; each checked against `git show b2e387db:docs/reference/harmony-patch-registry.md`. All 11 June-named categories have a `## ` heading (Patch25 at line 167, Patch30 197, Patch33 237, Patch35 249, Patch36 260, Patch37 266, Patch39 280, Patch41 292, Patch43 306, Late_ActionSetOverride 1006, Late_Transpiler 1012). The only 3 names absent from the file are `Patch89_MapLoadDiagnostics_{Lifecycle,MapScreen,SceneReady}`, whose targets are described under `## Patch89_MapLoadDiagnostics` (line 1020-1022, `_Lifecycle:` etc.).
- **Residual:** no gate checks this registry (unlike the GameModel one); drift can recur silently.

### 18. C L614 DOCS-02 (GameModel Overrides table missing 7 Taom*Model classes)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** Re-measured: 50 distinct `class Taom*Model` names in `Main/` at `b2e387db`, each found as a whole word in `git show b2e387db:docs/reference/gamemodel-registry.md` (0 missing). The drift is now gated: `tools/lint_docs.py:1130` `check_model_registry`, run by `lint_docs.py --fail-on-drift` in `.github/workflows/doc-budget.yml:30-35` on every branch.

### 19. C L632 DOCS-04 (career starter-equipment claims "Gondor only")
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** No "Gondor" career line in `git show b2e387db:CLAUDE.md`; the owning doc `docs/features/career-system.md:407` says "78 across 13 cultures, each culture's lowest troop gear (#629)".
- **Residual (LOW, not the cited site):** `docs/migration/templates/equipment-rosters.md:558` still says "Gondor only authored; 15 other cultures fall through", and `.claude/skills/improve/references/audit-playbook.md:119` uses it as an example. Neither is session-loaded text like the June CLAUDE.md row was.

### 20. C L648 DOCS-01 dup (registry drift, 7 models and 11 categories, plus "70 feature docs")
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** Items 17 and 18. The "all 70 feature docs" claim is gone from CLAUDE.md, AGENTS.md, `docs/ai-includes` and `docs/INDEX.md` (`git grep -E "[0-9]+ feature docs|all [0-9]+ feature"`). The one surviving count, `README.md:17` "90 feature docs", is the README drift carried by DX-06 (L549, STILL_VALID) and seed F8.

### 21. C L657 DOCS-02 dup (taom-src SKILL.md pins v1.3.15 and inverts the guidance)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** As item 10: `.claude/skills/taom-src/SKILL.md:3,9` at HEAD. The "currently v1.5.2" lag is L423's.

### 22. C L665 DOCS-03 (doc-linter backlog 56; five dead howdah links, 4 orphans, missing mcm.md)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** At `b2e387db` every howdah link in `docs/reference/engine/` resolves as `../../features/elephant/howdah-crew-mechanism.md` (`usable-machines.md:5,67,88`, `mount-and-rider-runtime.md:86`, `scene-gameentity-scriptcomponent.md:86`), and the target exists (`git ls-tree`). `docs/features/mcm.md` exists. Inbound links at HEAD (`git grep -l <name>.md -- docs`): battle-load-diagnostics 24, career-quest-system 5, clan-heraldry 14, new-factions-misty-mountains-lindon 8. `python tools/lint_docs.py --summary` run this pass (working tree, which includes another session's docs): dead_links 0, stale_versions 0, orphan_features 0, missing_features 0; non-zero rows are context_budget 7 and ai_dashes 5 (not this finding).

### 23. C L696 DOCS-06 (no ADR for the creature-as-mount architecture)
- **Original:** STALE. **Mine:** STALE. **Overturned:** no.
- **Evidence:** The harm the finding named (the decision scattered, so a later creature re-reverses it) is closed: `git show b2e387db:docs/ai-includes/creature-mount-authoring.md` lines 10-14 state the architecture and "No spawn patches, no detached combatants ... built twice and deleted twice", and `.claude/skills/new-creature-mount/SKILL.md:14-18` repeats it as "Architecture (never deviate)"; CLAUDE.md's skills table gates every new creature on that skill. That doc was added in `cdef46a3` (2026-06-12), which is not after the audit commit `141b749`, so the June agent likely missed a doc landing the same day. ADR-011 routes a procedure with traps to a skill, so the missing ADR is a form preference, not a live defect.

### 24. C L704 DOCS-06 (Mcm has no feature doc; 4 orphan docs; "70 feature docs")
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** Same reads as items 20 and 22: `docs/features/mcm.md` exists, all four former orphans have inbound links, the count claim is gone from the session-loaded files, and the linter reports orphan_features 0 and missing_features 0.

### 25. C L715 GAMEDATA-01 (all Custom Battle scene entries point at deleted scene folders)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** Re-measured: `git show b2e387db:Main/_Module/ModuleData/custom_battle_scenes.xml` has 26 distinct `id=` values (all `taom_*`); lowercased and compared with `comm` against the 652 folder names under `E:/Steam/.../Modules/*/SceneObj/`: 0 missing. Repoint landed in `d6977692`.
- **Residual:** a folder that exists can still CTD on load (the Iron Hills precedent in the same file); only in-game selection proves loadability. No tool or test reads this file against SceneObj, so the next rename wave can break it silently (worth a gate, LOW).

### 26. C L724 GAMEDATA-02 (live TAOM_Map settlements.xml: 3 town centers point at deleted scenes)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** Live file (mtime 2026-09-15): the center location of `town_EW9` (line 3106) is `empire_town_h`, `town_V2` (6132) `taom_rohan_castle_helms_deep_forceatmo`, `town_isengard` (9925) `taom_isengard_town_orthanc_forceatmo`; the three old ids have 0 hits. Stronger than triage-C's check: I included the level-slot attributes (`scene_name` 1,612, `scene_name_1/2/3` 442 each), 217 distinct values, and all 217 match a SceneObj folder (0 missing). Unversioned data; a reinstall could revert it.

### 27. C L741 GAMEDATA-05 (heroes.xslt dead rules for TAOM-only ids; one-directional spouse links)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** No `match` template for `lord_1_9_5`, `lord_1_52_4` or `lord_1_71_1` remains in `git show b2e387db:Main/_Module/ModuleData/heroes.xslt`; those ids appear only as attribute values on vanilla-side rows (spouse at 607, 802, 879; mother at 588, 626, 636, 812, 823, 889). Spouse half refuted by the installed engine: `~/.taom-src/v1.5.3/TaleWorlds.CampaignSystem.Hero.cs:892-914`, the `Spouse` setter assigns `_spouse.Spouse = this`; and `Deserialize` reads the `spouse` attribute only `if (Spouse == null)` (`:1908-1911`). Either load order therefore leaves both heroes married.

### 28. C L748 GAMEDATA-03 (about 320 C# localization keys never harvested)
- **Original:** FIXED. **Mine:** STILL_VALID (mostly fixed). **Overturned:** yes. **Impact:** LOW.
- **Evidence:** The bulk is fixed (`76c82cb6` harvested 317 keys; triage-C measured 0 missing for the `taom_feat_`, `taom_ep_`, `taom_fief_`, `taom_qa_` families). But one of the June finding's own cited sites is unchanged: `git show b2e387db:Main/Features/CrashReport/UI/CrashNotifier.cs` lines 23, 27, 28 use `{=TAOM_CrashReport_Title}`, `{=TAOM_CrashReport_Continue}`, `{=TAOM_CrashReport_OpenBundle}`, and `git grep -l 'id="<key>"' b2e387db -- Main/_Module/ModuleData` returns 0 files for each, so the crash popup stays English in all 12 languages. The ratchet gate cannot see them: `TAOM.Tests/Infrastructure/Localization/UnregisteredLocalizationKeyBaselineTests.cs:56` matches `\{\{?=(taom_[A-Za-z0-9_]+)\}` case-sensitively and `:25` scopes it to `taom_` deliberately. triage-C recorded this residue in its note but still returned FIXED.
- **Remaining scope:** register 3 keys, and either widen the gate's regex to `(?i)taom_` or rename the keys.

### 29. C L808 DIR-03 (re-enable the deferred howdah crew and spine bone-tracking)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no (checked the "one of two halves" risk).
- **Evidence:** Crew: `git show b2e387db:Main/Features/Elephant/HowdahCrewSpawner.cs:23-27` "Re-enabled 2026-09-19 (#627)", `CrewSpawnEnabled = true`, gated per harness at `ElephantMissionBehavior.cs:140` (`HowdahHarness.CarriesCrew`); `Main/Features/Mumakil/MumakilCrewSpawner.cs:27,74` true and queued. Spine tracking: full-frame bone tracking is still off (`TaomHowdahMachine.cs:242` `BoneTrackingEnabled = false`), but the goal it served is now met another way: `RepositionToFixedOffset` (`:346-375`) follows the live spine bone's HEIGHT since 2026-09-22 (`_placement = "spine-height"`), and `:319-321` records why the full-frame path stays off (bone axes do not match the animal's, so the prefab would need re-authoring). Neither `Elephant/` nor `Mumakil/` has working-tree changes (`git status --short`). Not overturned because the remaining full-frame path is a documented design choice, not a deferred defect. In-game "no slide" is not provable from a read (UNVERIFIED).

### 30. C L834 DIRECTION-03 (field the cave troll through the creature pipeline)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** `git show b2e387db:Main/_Module/ModuleData/troops/troops_mordor.xml` lines 1988-2000: "Re-enabled 2026-06-13: live Mordor troll race unit", `id="cave_troll"`, `race="cave_troll"`, `is_basic_troop="false"`; fielded by `taom_partyTemplates.xml:802` (`min_value="0" max_value="7"`) inside `kingdom_hero_party_mordor_template` (opens at line 762). Also carried in `TroopWeights/troop_weights.xml:13`. The mount-vs-unit question the finding left open was decided (`docs/features/troll-race.md:5-9`, bipedal race unit, not a mount), so the "field it" direction is delivered.
- **Residual (not a defect):** the Gundabad `cave_troll_master` career lore still has no troll in Gundabad parties; the troll is Mordor-only.

### 31. C L844 DIRECTION-05 dup (finish the mumakil set-piece: crew plus spine tracking)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** As item 29 (crew on for both howdah and mumakil tower; spine height tracked live; full-frame tracking off by documented design).

### 32. C L852 DIR-04 (revive the disabled Mordor cave troll)
- **Original:** FIXED. **Mine:** FIXED. **Overturned:** no.
- **Evidence:** As item 30: the `DISABLED 2026-05-14` comment is gone and the re-enable note at `troops_mordor.xml:1988-1992` names the disable's cause (vanilla `GetBasicVolunteer` picking an L51 troll) and its fix (`is_basic_troop="false"`); `2958f618` is an ancestor of HEAD.

## Verdict summary

32 removal verdicts checked. Upheld 28. Overturned 4, all to STILL_VALID at LOW impact:

| # | Bucket | Line | Id | Was | Now | Why |
|---|---|---|---|---|---|---|
| 8 | A | 193 | PERF-06 | FIXED | STILL_VALID (partly) | Scan fixed; 3 cited warg callers still allocate per query |
| 12 | B | 495 | DEPS-04 | STALE | STILL_VALID (partly) | #210 closed without validation; punch list unticked, no owner |
| 14 | B | 513 | DEPS-04 | STALE | STILL_VALID (dup of 495) | Same punch list; count once |
| 28 | C | 748 | GAMEDATA-03 | FIXED | STILL_VALID (mostly) | 3 cited `TAOM_CrashReport_*` keys unregistered, invisible to the gate |

Upheld with a carried residue (no overturn because a STILL_VALID sibling already holds it): item 10 and 21 (skill "currently v1.5.2", held by L423), item 11 (csproj comment versions, held by L440), item 1 (load without career keys, held by L56).

Totals after this check: FIXED 21, STALE 7, STILL_VALID 71 (was 23, 9, 67), REFUTED 0.

## Impact calibration

Question: does any of the 67 STILL_VALID entries (71 after the overturns above) deserve HIGH today, meaning crash-class, save-corruption or security with real reach? I skimmed every STILL_VALID block in triage-A, B and C and re-read the code behind the closest candidates.

**Verdict: none reaches HIGH.** The triage's "nothing HIGH" holds. The two entries nearest the line, both correctly MED:

- **A L21 CORRECTNESS-02 (SpecialResources storage survives campaign boundaries), MED.** It is the only STILL_VALID entry whose wrong state is written into a save, but the leak is narrow: every earn and seed path keys on `Hero.MainHero` (`git grep` over `Main/Features/SpecialResources/*.cs`: `SpecialResourcesBehavior.cs:151,168,171,324,326,343,361,372,387`), and character-creation finalize reseeds the player's current resource unconditionally (`:138-153`). What leaks is the main hero's non-current resource pairs from the previous campaign (and any hero a Player Switcher swap controlled), which only matter after a kingdom change; `SyncData` still passes the live dict as the `ref` (`:104-110`). Wrong balances, not an unloadable or corrupted save.
- **A L69 / L93 SEC-01 (unpinned MCP servers), MED.** Real reach (serena runs `uvx --from git+https://github.com/oraios/serena` at upstream HEAD, `.mcp.json:3-9` at `b2e387db`, with repo write access every session), but supply-chain only, with no known compromise. I checked the `ELEVENLABS_API_KEY` entry in this public repo's tracked `.mcp.json` at HEAD and at both commits that touched it (`7571e1dd`, `812f4a38`): it is an environment reference (`${...}`), never a literal, so there is no committed secret to escalate.

Checked and not HIGH: PERF-01 (L140, 2 s grid cadence: stale targeting, no crash); CORRECTNESS-03 (L56, needs a warm process plus a pre-feature save); CORRECTNESS-06 (L47, needs a hand-typo'd career XML); SEC-02/03/04 native (L77, L109, L131: NativeSkinFixes is parked at the wiring, a decided tradeoff); TEST-03 transpiler IL (L282: engine bumps make it MED, but no current mismatch is evidenced and `TranspilerSiteBindingTests` covers the two hottest transpilers); the four overturns above are all LOW.

## What I did not cover

- No `dotnet build` or `dotnet test` (brief). Test files cited as covering a fix (item 3) were read, not run.
- `python tools/lint_docs.py --summary` ran on the working tree, which carries another session's uncommitted docs; the doc reads for items 17 to 24 were made at `b2e387db` via `git show`.
- In-game behaviour is unverified: Custom Battle scene loads (item 25), town-center visits (item 26), howdah and mumakil "no slide" (items 29, 31), and every punch-list item (items 12, 14).
- The live TAOM_Map `settlements.xml` and SceneObj listing are this desktop's unversioned install; a reinstall could change items 25 and 26.
- Item 12: I did not check GitHub for any issue that might have taken over the punch list after #210 closed (no `gh` calls); the repo side shows none (`TRACKING.md:8` names the punch-list file as the owner).
- Calibration: I skimmed all STILL_VALID blocks but re-read code only for L21, L69/L93 and L140; the other MED ratings are the triage agents' and I accepted them without re-deriving call frequencies.
- Item 8: I did not measure BT evaluation frequency for `NoEnemyCloseDecorator`; "every Evaluate, no throttle" is from the class shape (`BTReturnFalseDecorator`, no wait decorator in the file).
