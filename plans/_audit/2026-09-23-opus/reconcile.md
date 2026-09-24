# Reconcile pass: June plans 001 to 005 at `b2e387db`

Read-only reconcile of `plans/README.md` and plans 001 to 005 (written 2026-06-13 against `141b749`),
plus the four unmerged June branches `impl-001`, `impl-002`, `impl-003`, `impl-005`. Every merge-base
is `141b7494` (`git merge-base HEAD impl-00N`); none is an ancestor of HEAD. All code reads are
`git show b2e387db:<path>` unless stated.

## Plan 001: reset CareerSystem + SpecialResources singletons on new campaign

**Verdict: PARTIAL.** The CareerSystem half landed on trunk, re-implemented differently; the
SpecialResources half (storage clear on new game, null-local load branch) did not.

- **Landed (Career):** `f4273639` "fix(career): phantom career ate the first point" (2026-09-02, on
  trunk per `git merge-base --is-ancestor`). `CareerPersistenceBehavior.RegisterEvents()` now registers
  `CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, _ => _dataService.ResetForNewCampaign())`
  (`Main/Features/CareerSystem/CareerPersistenceBehavior.cs:20-35`), and
  `CareerDataService.ResetForNewCampaign()` swaps in a fresh dictionary
  (`Main/Features/CareerSystem/CareerDataService.cs:98-103`, interface `ICareerDataService.cs:31`).
  Test coverage lives in `TAOM.Tests/Features/CareerSystem/CareerDataServiceTests.cs` (only test file
  under that folder matching `ResetForNewCampaign|OnNewGameCreated`, via `git grep -ln`).
  This differs from the plan (a new interface method rather than `RestoreData(null)`), which is fine.
- **Not landed (SpecialResources):**
  - Storage is still `Reuse.Singleton` (`Main/Features/SpecialResources/SpecialResourcesIoC.cs:11`).
  - `SpecialResourcesBehavior.OnNewGameCreated` (`Main/Features/SpecialResources/SpecialResourcesBehavior.cs:118-132`)
    calls only `_service.ResetSessionState()`; `ResetSessionState` (`SpecialResourceService.cs:402-417`)
    clears `_pendingSpend`, `_inSession`, `_loggedResolveKeys`, never storage. No `RestoreData(null)`
    or storage clear exists in the feature (`git grep -n "RestoreData(null"` returns nothing there).
  - `SyncData` still hands the live dict as the `ref` on load (`SpecialResourcesBehavior.cs:107-109`),
    so a save lacking `_taom_specialResources` re-installs the prior campaign's dict, and the
    `OnGameLoaded` seed gate `_storage.Contains(...)` (`:168`) then suppresses legitimate seeding.
  - Premise shift worth noting: seeding moved from `OnNewGameCreated` to `OnCharacterCreationIsOver`
    phase 9 (`:138-153`, `InitializeHero` unconditional). New-game leakage of every OTHER
    `(hero, resource)` key survives and is written into campaign B's first save. The fix shape is
    unchanged: clear storage in `OnNewGameCreated`, null local on the load branch.
  - No `SpecialResourcesBehaviorTests.cs` at HEAD (`git ls-tree` of `TAOM.Tests/Features/SpecialResources/`);
    `SpecialResourcesBehaviorPhaseGuardTests.cs` exists and is the natural home.
  - Constructor drift: `SpecialResourcesBehavior` now takes six parameters, adding `ITroopWeightService`
    and `IDedicatedServerProvider` (`:26-32`), so the plan's Step 2 test code no longer compiles as written.

**Proposed README status cell:**
`PARTIAL (2026-09-23): Career reset landed in f4273639 (ResetForNewCampaign); SpecialResources storage still leaks, SpecialResourcesBehavior.cs:118-132 and live-dict load at :107-109`

## Plan 002: reject NaN/Infinity in TroopWeight + Career mutation loaders

**Verdict: PARTIAL.** The lead holds: the TroopWeight half landed, `MutationParams` did not.

- **Landed (TroopWeight):** `bee07b48` "refactor(troop-weight): weight the party-size limit, not the
  count" (2026-07-11; found with `git log -S"FiniteFloatValidator.IsFinite(weight)"`). The guard is
  folded into one line, `if (!FiniteFloatValidator.IsFinite(weight) || weight <= 0)`
  (`Main/Features/TroopWeight/TroopWeightXmlLoader.cs:86`), so zero stays rejected as the plan
  required. Test: `GetTroopWeights_NaNOrInfinityWeight_SkipsEntryWithWarning`
  (`TAOM.Tests/Features/TroopWeight/TroopWeightXmlLoaderTests.cs:121-129`) covers `NaN` and
  `Infinity`; no `-Infinity` case (grep of the file for `NaN|Infinity`), which the combined guard
  would still reject via `<= 0`.
- **Not landed (MutationParams):** `GetFloat` is byte-identical to the plan's excerpt
  (`Main/Features/CareerSystem/Mutations/MutationParams.cs:15-20`, no `TAOM.Core.Validation` using;
  `git diff --stat 141b7494..HEAD` on the file is empty). No `MutationParamsTests.cs` exists at HEAD.
  No downstream guard either: `MutationService.ApplyMutation` writes the calculator result straight
  into the template property (`Main/Features/CareerSystem/Mutations/MutationService.cs:88-96`), and
  `git grep "Finite|IsNaN"` over `Main/Features/CareerSystem/Mutations/` returns nothing. All five
  built-in calculators read `GetFloat` (`BuiltInCalculators.cs:8,11,14,17,20`). Premise still valid.
- `FiniteFloatValidator.IsFinite(float)` still exists (`Main/Core/Validation/FiniteFloatValidator.cs:22`).

**Proposed README status cell:**
`PARTIAL (2026-09-23): TroopWeight guard landed in bee07b48 (TroopWeightXmlLoader.cs:86); MutationParams.GetFloat still unguarded at MutationParams.cs:15-20, fix is on impl-002 cfc47206`

## Plan 003: hot-path IoC/MCM resolves + SpatialGrid cadence

**Verdict: TODO (with one sub-fix STALE).** Three of four sub-fixes are still open; PERF-03 is STALE.
Nothing from this plan landed on trunk; `bee07b48` removed most of PERF-03's target.

- **PERF-01 (grid cadence): TODO, still valid, but a decision not a mechanical fix.**
  `private const float GridUpdateInterval = 2f;` is unchanged (`Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs:16`;
  `git log 141b7494..HEAD` on the file shows only `f75288eb` and `0c323437`, neither touches the constant).
  The dead-branch premise still holds: `AdvancedCombatBehavior` is added before `WargMissionBehavior`
  (`Main/SubModule.cs:1958,1960` at `b2e387db`), and the warg's 0.1f branch stays behind
  `_managesCombatInfrastructure` (`Main/Features/Warg/WargMissionBehavior.cs:23,34,60,89-97`).
  What changed: `SpatialGrid.ApplyPendingRemovals()` now runs every tick (`AdvancedCombatBehavior.cs:33`),
  so dead agents leave between rebuilds; the range test uses live `agent.Position`
  (`Main/Features/AdvancedCombat/SpatialGrid.cs:122-124`) and PERF-06 (cells-in-radius iteration)
  landed (`SpatialGrid.cs:99-114`). Staleness now means only "agent moved into a cell since the last
  rebuild is missed" (cell size 20, `SpatialGrid.cs:22`). The feature doc still records 2f as the
  setting (`docs/features/advanced-combat.md:44`). The harvest's own caveat stands: 0.1f without a
  creature-presence gate costs a 10 Hz full-agent rebuild in every mission. UNVERIFIED in game.
- **PERF-03 (8 TroopWeight gates): STALE.** Six of the eight target patch files no longer exist
  (`git diff --stat 141b7494..HEAD` shows `PartyBase_NumberOfAllMembers_Patch.cs`,
  `PartyBase_NumberOfRegularMembers_Patch.cs`, `PartyVM_PopulatePartyListLabel_Patch.cs`,
  `PartyBaseHelper_GetPartySizeText_Patch.cs`, `GameMenuPartyItemVM_RefreshCounts_Patch.cs`,
  `CampaignUIHelper_GetPartyHealthTooltip_Patch.cs` deleted, by `bee07b48` which moved troop weight
  onto the party-size limit). The engine-wide per-tick member-count getters were the hot path; what
  remains are six UI-refresh or daily sites still reading `TaomSettings.Instance?.EnableTroopWeight`
  (`git grep` at HEAD: `CampaignUIHelper_GetMainPartyHealthTooltip_Patch.cs:24`,
  `ClanPartyItemVM_UpdateProperties_Patch.cs:34`, `PartyCharacterVM_RefreshValues_Patch.cs:20`,
  `PartyUpgraderUpgradeReadyTroops_Patch.cs:24`, `PartyVM_RefreshPartyInformation_Patch.cs:22`,
  `RecruitmentVM_RefreshPartyProperties_Patch.cs:23`, plus `TroopWeightService.cs:105`). None is a
  hot path; caching them fails `simplicity-criterion.md` (tiny win, new helper type).
- **PERF-04 (BattleBalanceSettingsProvider): TODO, still valid.** The provider is byte-identical to the
  plan's excerpt (`Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs:5-17`, twelve
  `TaomSettings.Instance?.X` getters) and `TaomMilitaryPowerModel.GetDefaultTroopPower` still reads up
  to seven of them per call (`Main/Features/BattleBalance/Models/TaomMilitaryPowerModel.cs:19-33`).
  No `BattleBalanceSettingsProviderTests.cs` at HEAD. The house has since settled on the lazy form
  `_settings ??= TaomSettings.Instance` (`Main/Features/SettlementNameplateRelation/NameplateRelationSettingsProvider.cs:37`),
  which is safer than the plan's ctor-cache if the provider is ever resolved before MCM populates
  `Instance` (it is resolved in `RegisterBattleBalanceAndTargeting`, `Main/SubModule.cs:1142-1144`).
- **PERF-07 (Warg BT decorators): TODO, still valid.** Both static resolve-getters remain
  (`Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs:15,45`) with
  `GetAgentAdapter(warg)` re-adapted inside each loop (`:29-30`, `:59-60`). This is seed F4 in the
  brief; F4 also names `WargAiControlledIsNotFacingEnemy.cs:16`, `WargAttackTask.cs:30-31` and
  `WargRiderHandManager.cs:14`, which a rewritten plan should fold in.

**Proposed README status cell:**
`TODO (2026-09-23): nothing landed; PERF-03 STALE (6 of 8 patch files deleted in bee07b48); PERF-04 and PERF-07 still TODO (impl-003 6eb5955c, 4962f3ee apply clean); PERF-01 grid 2f at AdvancedCombatBehavior.cs:16 needs a gate decision`

## Plan 004: three live town-center scene crashes (Isengard, Helm's Deep, Calembel)

**Verdict: DONE, still holds at 2026-09-23.**

- `tools/audit_scene_names.py` is read-only: no argparse, no write, rename or unlink calls (read in
  full, 128 lines); it imports only `game_dir` from `tools/_gamedir.py` (read; path resolution only).
  Ran `python tools/audit_scene_names.py` (exit 0, output kept in the session scratchpad as
  `scene_audit.txt`). The CRASH SUSPECTS section has **no `[TAOM_Map]` block**; TAOM_Map references
  217 distinct scene names, all present among 624 on-disk SceneObj folders.
- Live `TAOM_Map/ModuleData/settlements.xml` (read-only greps counting every `scene_name(_N)` slot):
  `HART_isengard` 0, `Helms_Deep_Town_forceatmo` 0, `lotrtaom_hat_gondor_town_calembel` 0;
  `taom_isengard_town_orthanc_forceatmo` 4 (center at line 9945), `taom_rohan_castle_helms_deep_forceatmo`
  4 (center at line 6152), and `town_EW9` (Calembel) center is `empire_town_h` in all four slots
  (line 3126). Line numbers moved from the plan's (9867, 6100, 3074) because the file grew.
- The three REMAP entries are in the tracked tool (`tools/remap_stale_scene_names.py:48-50`).
- Not re-checked or not holding: the `settlements.xml.bak_scenes` backup from the done criteria is no
  longer beside the live file (directory listing shows only `settlements.xml` and `settlements.xslt`);
  the in-game entry smoke was never recorded as done; Calembel is still the `empire_town_h` stopgap
  (no `*calembel*` SceneObj folder under any module). The `[TAOM_shadow]` block (9 misses) is the
  deployed stale shadow, a decided non-finding per the brief.
- Standing risk: TAOM_Map is unversioned, so a reinstall would revert the fix; the gate is the audit
  script, referenced only from `.claude/rules/vanilla-data-comparison.md`, the improve playbook,
  `tools/remap_stale_scene_names.py` and `tools/triage_battle_load.py` (`git grep -ln audit_scene_names`
  over `.github`, `.claude`, `tools`); no workflow or hook runs it.

**Proposed README status cell:**
`DONE (2026-06-13; re-verified 2026-09-23: audit_scene_names.py shows 0 TAOM_Map misses; Calembel still on the empire_town_h stopgap; .bak_scenes backup gone)`

## Plan 005: vendored credential, MCP pins, faction-map `python -c` injection

**Verdict: TODO.** None of the three items landed on trunk; the premise of item A shifted.

- **Item A (vendored credential): premise changed, still open.** The extracted
  `Dependencies/.vendor-source/Bannerlord.UIExtenderEx-2.13.2/` tree is gone (`ls` fails), so impl-005's
  on-disk scrub no longer exists either. What is on disk now are six gitignored tarballs
  (`.gitignore:151` via `git check-ignore -v`; `git ls-files Dependencies/.vendor-source` is empty).
  Streaming each tarball's `*nuget.config` through `grep -c ClearTextPassword` (values never printed):
  `uiextenderex-v2.13.2.tar.gz` 1, `butterlib-v2.10.4.tar.gz` 1, `mcm-v5.11.4.tar.gz` 1, the other
  three 0. Three BUTR archives, not one, carry a `ClearTextPassword` line; whether the value is a
  live token is UNVERIFIED (not decoded, by rule). Not in TAOM history. The vet-checklist bullet is
  not on trunk: `grep packageSourceCredentials` over `docs/ai-includes/external-repo-adoption.md` and
  `.claude/skills/adopt-external/SKILL.md` finds nothing. Rotation remains BUTR's call.
- **Item B (MCP pins): not landed; drift.** `.mcp.json` still runs serena from unpinned
  `git+https://github.com/oraios/serena` (`.mcp.json:8`), filesystem via `npx -y` unpinned
  (`:24-27`, seed F6), `uvx mcp-server-git` unpinned (`:36-37`), and a newer `uvx elevenlabs-mcp`
  unpinned (`:60-61`). `.codex/config.toml` at HEAD: unpinned filesystem (`:13-16`), unpinned
  `mcp-server-git` (`:26-27`), `inherit = "all"` (`:10`). `tools/audit_claude_config.py` grew by 494
  lines since `141b7494` but its MCP rule still flags only `npx -y` without `@\d`
  (`tools/audit_claude_config.py:418-421`) and reads only JSON `mcpServers` (`:395-404`): no `uvx`
  rule, no `.codex/config.toml` scan.
- **Item C (faction-map injection): not landed, unchanged.** `tools/process_faction_map.py` has no
  commits since `141b7494` (`git diff --stat` empty); the interpolations sit at the plan's exact lines:
  `read_png_rgba(r'{filepath}')` (`:260`) and `crop_and_save(r'{input_path}', r'{output_path}', ...)`
  (`:383`), both inside `f"""` child sources opened at `:161` and `:284`.

**Proposed README status cell:**
`TODO (2026-09-23): nothing landed; faction-map injection unchanged at process_faction_map.py:260,383 (impl-005 4310aa6e applies clean); MCP pins still absent (.mcp.json:8,27,37,61); credential now in 3 gitignored BUTR tarballs, vet line not added`

## Branch impl-001

Commits (`git log --oneline HEAD..impl-001`): one, 909 commits behind HEAD (`git rev-list --count impl-001..HEAD`).

| Commit | Changes (`git show --stat`) | On trunk already? | Trunk drift since merge-base `141b7494` |
|---|---|---|---|
| `e7043a3e` fix(career,specres): reset singleton state on new-campaign boundary (2026-06-13) | `CareerPersistenceBehavior.cs` +12, `SpecialResourcesBehavior.cs` +28/-5, `CareerPersistenceTests.cs` +21, new `SpecialResourcesBehaviorTests.cs` +64 | Career half: yes, differently (`f4273639`, `ResetForNewCampaign` via a lambda listener). SpecRes half: no. | `CareerPersistenceBehavior.cs` 15 lines, `SpecialResourcesBehavior.cs` 302 lines (221+/96-) |

Predicted cherry-pick outcome (read-only `git merge-tree e7043a3e^ HEAD e7043a3e`, trivial mode, no
objects written): one textual conflict in `CareerPersistenceBehavior.cs` (both sides filled
`RegisterEvents()`); `SpecialResourcesBehavior.cs` and `CareerPersistenceTests.cs` merge textually.
It would still not compile: the new `SpecialResourcesBehaviorTests.cs` calls a four-argument
constructor (`e7043a3e:TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorTests.cs:26`) while
HEAD's takes six (`SpecialResourcesBehavior.cs:26-32`), and the added `CareerPersistenceTests` case
calls `_behavior.OnNewGameCreated(null)`, a public method that exists only if the conflict is resolved
to the branch side, which would duplicate trunk's listener.

**Recommendation: rewrite fresh.** Only the SpecialResources half is still owed. Port its two
production hunks by hand (storage `RestoreData(null)` first thing in `OnNewGameCreated`; null local on
the `SyncData` load branch, `SpecialResourcesBehavior.cs:104-110` at HEAD), write the tests against
the six-argument constructor (beside `SpecialResourcesBehaviorPhaseGuardTests.cs`), then delete
`impl-001`. Subject in the house format, for example `fix(specres): v2.0.30 - ...`.

## Branch impl-002

Commits: two, 909 behind HEAD.

| Commit | Changes | On trunk already? | Trunk drift since merge-base |
|---|---|---|---|
| `cfc47206` fix(config): reject NaN/Infinity in troop-weight and mutation float loaders | `MutationParams.cs` +2, `TroopWeightXmlLoader.cs` +7, new `MutationParamsTests.cs` +42, `TroopWeightXmlLoaderTests.cs` +46 | TroopWeight part: yes (`bee07b48`, `TroopWeightXmlLoader.cs:86`, test at `TroopWeightXmlLoaderTests.cs:121`). MutationParams part: no. | `TroopWeightXmlLoader.cs` 9 lines, `TroopWeightXmlLoaderTests.cs` 23 lines; `MutationParams.cs` unchanged; `MutationParamsTests.cs` absent at HEAD |
| `621c1cf2` test(troopweight): pin -Infinity rejection to the finite guard | `TroopWeightXmlLoaderTests.cs` +3/-1 | Moot: refines a test that exists only on the branch | as above |

Predicted conflicts (`git merge-tree cfc47206^ HEAD cfc47206`): `TroopWeightXmlLoader.cs` and
`TroopWeightXmlLoaderTests.cs`; `MutationParams.cs` and the new `MutationParamsTests.cs` apply clean.
The `MutationParams` hunk is exactly the plan's Step 3 (`&& FiniteFloatValidator.IsFinite(result)`
plus the using), and the test file holds the plan's five cases.

**Recommendation: cherry-pick `cfc47206` only**, resolving both TroopWeight conflicts to trunk's side
(trunk's combined guard and NaN/Infinity test already cover them; the branch side would add a second
finiteness branch and duplicate tests). Skip `621c1cf2` (it only patches a branch-only test). Reword
the subject to the house format (`fix(career): v2.0.30 - ...`), since its TroopWeight half no longer
applies. Then delete `impl-002`.

## Branch impl-003

Commits: four, 909 behind HEAD.

| Commit | Changes | On trunk already? | Trunk drift since merge-base |
|---|---|---|---|
| `bdf18039` perf(advancedcombat): rebuild spatial grid at 100ms cadence (#219) | `AdvancedCombatBehavior.cs` +4/-1 (const 2f to 0.1f) | No (`AdvancedCombatBehavior.cs:16` still `2f`) | +43 lines (`f75288eb`, `0c323437`); predicted conflict, trunk inserted fields next to the constant |
| `463fccbc` perf(troopweight): cache TaomSettings ref across 8 Patch17 sites | 8 hook files, 1 line each; new `TroopWeightSettingsCache.cs` +23; new test +15 | No, and six of its eight target files were deleted on trunk | Predicted modify/delete on 6 files (`removed in local`) plus content conflicts in `CampaignUIHelper_GetMainPartyHealthTooltip_Patch.cs` and `RecruitmentVM_RefreshPartyProperties_Patch.cs` |
| `6eb5955c` perf(battlebalance): cache TaomSettings ref in settings provider | `BattleBalanceSettingsProvider.cs` 21+/12-, new `BattleBalanceSettingsProviderTests.cs` +35 | No | None: provider unchanged since `141b7494`, test file absent at HEAD; predicted clean |
| `4962f3ee` perf(warg): lazy-cache adapter factory in BT decorators | `PeriodicallyCheckIfCanAttackAnyone.cs` 8+/6- | No (`:15,45` still static resolve-getters) | None; predicted clean |

Review notes on the clean pair: `4962f3ee` matches the plan's target shape (instance field, `??=`
inside `Evaluate()`, warg adapter hoisted, skip conditions preserved). `6eb5955c` caches in the
constructor (`_settings = TaomSettings.Instance`); the later house pattern is the lazy
`_settings ??= TaomSettings.Instance` (`NameplateRelationSettingsProvider.cs:37`), which cannot pin a
null if the provider is resolved before MCM populates `Instance`. It is resolved in
`RegisterBattleBalanceAndTargeting` (`Main/SubModule.cs:1142-1144`); whether `Instance` is always
non-null there is UNVERIFIED.

**Recommendation: cherry-pick `6eb5955c` and `4962f3ee`** (no conflicts expected; consider amending
`6eb5955c` to the lazy `??=` form). Drop `463fccbc` (STALE target, six modify/delete conflicts, and
the surviving sites are UI refreshes, not hot paths). Drop `bdf18039` (the cadence change needs the
creature-presence gate decision first; redo it inside a fresh PERF-01 plan, together with the other F4
Warg sites `WargAiControlledIsNotFacingEnemy.cs:16`, `WargAttackTask.cs:30-31`,
`WargRiderHandManager.cs:14`). Reword subjects to `perf(...): v2.0.30 - ...`. Then delete `impl-003`.

## Branch impl-005

Commits: three, 909 behind HEAD.

| Commit | Changes | On trunk already? | Trunk drift since merge-base |
|---|---|---|---|
| `4bc520a1` chore(security): scrub vendored BUTR credential + add vet-checklist grep | `docs/ai-includes/external-repo-adoption.md` +1 bullet (the on-disk scrub was untracked, and that extracted tree no longer exists) | No | Doc +20 lines on trunk; predicted clean |
| `1d566be6` chore(security): pin MCP servers | `.codex/config.toml` 2 lines, `.mcp.json` 3 lines: serena `@22c135a8...`, filesystem `@2026.1.14`, mcp-server-git `@2026.6.4` | No | `.mcp.json` 22 lines, `.codex/config.toml` 12 lines (paths moved from the old `source\repos\TAOM` root to `E:\repos\TAOM`); predicted conflict in both |
| `4310aa6e` fix(tools): pass paths as argv to process_faction_map child interpreters | `tools/process_faction_map.py` 12+/11- | No | File unchanged since `141b7494`; predicted clean |

Review notes: `4310aa6e` converts all three doubled-brace child `print`/`raise` f-strings to single
braces (an `awk` scan of lines 160-390 of the branch file finds no `{{` left), moves both paths and
the five ints to argv, and the branch's file byte-compiles (`python -m py_compile` on a scratchpad
copy, exit 0). `4bc520a1`'s bullet contains an em dash, which AGENTS.md "Human prose" bans in new
prose, and names only the UIExtenderEx archive while three tarballs now match. `1d566be6`'s pins are
June versions and do not cover the later `elevenlabs` server.

**Recommendation: cherry-pick `4310aa6e` and `4bc520a1`** (no conflicts expected; amend `4bc520a1` to
drop the em dash). Drop `1d566be6`: re-resolve current pins fresh (network lookups the executor must
run, never guessed) against HEAD's `.mcp.json` and `.codex/config.toml`, including `elevenlabs-mcp`.
Reword subjects to the `v2.0.30 -` house format. Then delete `impl-005`.

## What I did not cover

- No build or test was run (brief forbids it); every "predicted clean" or "predicted conflict" comes
  from the trivial-mode `git merge-tree <c>^ HEAD <c>` (no `--write-tree`, no objects written), not
  from compiling the result. A cherry-picked commit still needs `dotnet build`/`dotnet test` with
  `-p:DisableModuleCopy=true -p:ModuleId=`.
- Did not re-derive the harvest's other ~70 backlog findings; only plans 001 to 005.
- Did not verify MCM `TaomSettings.Instance` timing at `RegisterBattleBalanceAndTargeting`, nor
  whether MCM ever replaces the `Instance` reference (both bear on `6eb5955c`). UNVERIFIED.
- Did not decode or inspect the `ClearTextPassword` values in the three BUTR tarballs, so whether they
  are live tokens is UNVERIFIED (by rule).
- Did not look up current MCP server versions (network, and not an audit action).
- No in-game check of plan 004's three town centers; the audit script proves only reference versus
  on-disk consistency.
- Did not read `plans/_audit/2026-06-12-harvest.md` beyond the PERF-01 lines found by grep.
- Did not check whether GitHub issues exist or are closed for any of the five plans.
