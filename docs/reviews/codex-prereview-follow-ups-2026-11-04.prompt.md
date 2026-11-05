# Codex pre-review: the RCA follow-up fixes, 2026-11-04

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls, and run no game.
Read `AGENTS.md` and `.ai/review-reference.md` first. Scope is the committed diff of this worktree:
`git diff 9edc3a88b..HEAD` on branch `chore/follow-ups` (`git log --no-merges 9edc3a88b..HEAD` lists 27
commits, each with its stated intent). `9edc3a88b` is the trunk tip the branch merged; ignore commits that
came from trunk. Engine source for the installed build (v1.5.6, changeset 125270) is cached in
`C:\Users\mikew\.taom-src\v1.5.6-202611041234\`; `pwsh tools/taom-src.ps1 path <Type>` adds a missing type.
`E:\Decompiled_Bannerlord\_shipping_build_v1.5.6.125053\` is whole-assembly source of the build before the
hotfix, for searching callers; say which you read. The game install is
`E:\Steam\steamapps\common\Mount & Blade II Bannerlord`, and its `TAOM_Map` and `LOTRLOME_Armory` modules are
unversioned data this repo depends on. Do not write to any of those folders, or under `E:\LOTRAOM_Releases`.
Mark anything you cannot prove from code as UNVERIFIED.

The known red test `BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` is trunk's state and not a finding.

## Questions to answer with evidence

**Warg rage (`Main/Features/Warg/`, tier 1, mission tick).** A rage takes an AI rider's formation; the end gives
it back. The change stores the rider at rage start, adds `CleanIfEnemyDied.RiderToRestore`, adds
`RestoreDismountedRider` to the "no rider" branch and clears `AgentsFormation` when a rage closes. Trace
`PrepareOnFirstCall`, `CleanIfEnemyDied`, `OnWargDied`, `FinishRageMode`, `RestoreDismountedRider` and the tree in
`WargBehaviorTree.cs` for every order of events: the rider leaves a living warg and remounts it or another mount,
the warg dies mid-rage or after it, a second rage starts, the rider dies, the agent slot is recycled, the rider
is the player, a co-op peer holds a puppet. Can a rider end with no formation, a wrong formation or scripted
movement left on? Does `FirstAttack` re-arm so a second rage stores again? Does the new task break the "no rider"
sequence's result or its sleep?

**GameModels.** `TaomHeroCreationModel` loses its `GetCharacterTemplateForOffspring` override; is vanilla's
result the same for every caller, and does anything between the two classes or `Patch13_RaceAge` depend on the
override? `TaomPregnancyModel` moves its guards and formula into `RaceAgeService.GetDailyPregnancyBaseChance`:
compare old and new for every input, including a negative base chance and the Virile perk.

**Patch deletion.** `ClanPartyItemVM_GetCharacterCode_Patch` is deleted with its `Main/SubModule.cs` line, on the
claim that it could never change a result. Prove or refute that from `ClanPartyItemVM.GetCharacterCode`,
`CampaignUIHelper.GetCharacterCode` and TAOM's other banner colour patches.

**Hot-path settings.** `NameplateFadeSettingsProvider` now caches `TaomSettings.Instance` on the first non-null read.
Is that safe on its threads, and does it pick up the instance MCM really uses?

**Careers.** `ability_sprite` on 42 cloned careers in `career_system/taom_careers.xml`, read by
`CareerConfigProvider`, used by `CareerScreenVM`, and written by `tools/insert_new_faction_careers.py`. Does each name a
sprite registered in `TAOMSpriteData.xml`? Does anything else read `AbilityTemplateId` for the icon? Could the attribute
break a schema or validator?

**Ladder quests.** `LadderQuests.Start` now builds its id with `CareerQuestService.NewQuestStringId`. Does anything
look a ladder quest up by the old `taom_cq_<def>` id, including `Find`, the save load path and any co-op sync?

**Data removal.** 46 `kingdom_hero_party_mercenary_*` and `kingdom_hero_party_outlaw_*` templates and 22 `_e` lord rosters are
deleted, on the claim that nothing reads them. Search TAOM's XML, XSLT and JSON, the live `TAOM_Map` and
`LOTRLOME_Armory` modules, C#, vanilla's data and the engine, for a reader: a string-built id, a culture binding, an `All` walk, a
co-op registry, a save reference. Are the generators and `tools/promote_borrowed_cultures.py` now consistent, and can
a later regeneration bind a removed id?

**GUI data.** `GoldIcon.xml` is deleted; `GameMenu.xml` and `PartyScreen.xml` lose visual definitions and constants;
`BattleActionBar.xml` loses its root `DataSource`. Does anything still name a deleted item, including a vanilla prefab, a
C# string or another module? Does Gauntlet read a root `DataSource`?

**Validator and gates.** `tools/taom_schema.py` gains `BROKEN_ROSTER_REF`, `BROKEN_SETTLEMENT_REF` and a cached
`_read_stripped`; `tools/validate_moduledata.py` shares a ref walk; `tools/check_release_pair.py` is new. Can a stale cached read
give a wrong verdict, in the CLI, the commit hooks or the MCP server (`tools/taom_query.py`)? Can the pair check pass a
channel that disagrees? Are the skip list and the registries right? Check `.githooks/pre-commit`,
`.claude/hooks/check-moduledata-validation.sh`, and `.claude/hooks/block-no-verify.sh`: find a `git` spelling that skips a
hook and passes this hook, or a harmless command it refuses. Check `tools/context_budget_scan.sh`.

**Tools.** `process_faction_map.py`, `assemble_faction_map.py`, `realm_troops.self_check`, `add_map_villages.py`,
`add_map_fortifications.py`, `rebalance_ranged_ladders.py`, `run_fords_autorun.py`, `run_long_defeat.py`,
`rebuild_translation_files.py`, `translate_with_claude.py`, `audit_mount_parity.py`, `audit_claude_config.py`,
`skeleton_hit_capsules.py` and `blender/fbx_remap_materials.py`: for each changed path, can it write a wrong file, report a false pass
or crash on input it used to accept?

**Tests that gate.** `CoopPresenceReadGateTests`, `EveryBrushSprite_ResolvesOrIsNamedByTheGamesOwnBrushes`,
`EveryVisualDefinition_IsNamedByAWidgetInItsPrefab`, `HexColorParser.TryParseRgb`: can each pass with the fault it
names present?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the proving code or a
reproduction. Say PASS for a question you checked and found sound.
