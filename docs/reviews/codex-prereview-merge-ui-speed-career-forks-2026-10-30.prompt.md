You are reviewing two uncommitted git merge resolutions in the TAOM repo (a Bannerlord total conversion, .NET Framework 4.7.2, Bannerlord v1.5.6 installed). This is a REVIEW-ONLY task: do not edit, create or delete any file, run no git write command, and return the full report as your FINAL MESSAGE (the dispatcher saves stdout; do not write the output file yourself). Follow AGENTS.md and .ai/roles/reviewer.md for the evidence format. Engine signatures: the installed DLLs via `pwsh tools/taom-src.ps1 path <Type>`.

Both merges start from trunk origin/bannerlord-1.5.x at 3d446ac66 and are mid-merge (`git merge --no-ff --no-commit <branch>`), resolved in the working tree. Review the RESOLUTION and the semantic collisions with trunk's later code, not each feature from scratch. Base: `git merge-base HEAD <branch>`. Resolution: `git diff <branch> -- <file>` and `git diff HEAD -- <file>` inside each worktree.

-- MERGE A: E:/repos/taom-merge-ui-speed, branch feat/ui-speed (Shadowfax Screens #851 clan, #852 inventory, #853 party, #854 quest log fast open), base 373 commits behind trunk. Trunk meanwhile split client-only code into TAOM.Client (ADR-013: core TAOM.dll must load and JIT on a dedicated server, where TaleWorlds ViewModelCollection, Gauntlet and SandBox.View are absent).
Resolution work:
- 17 hook classes in Main/Features/ShadowfaxScreens/{Clan,Inventory,Party,Quests}/Hooks/*.cs gained [ClientOnly] (PatchCategoryIndex skips them on a server; gates TAOM.Tests/Infrastructure/ServerLoadGateTests.cs and ServerJitGateTests.cs).
- ClanFastOpenCalls.cs lost BuildSwaps, RefreshPartiesListInRefresh(ClanPartiesVM) and TryRenderHeaders; they moved into ClanPartiesVM_RefreshValues_ClanFastOpen_Patch.cs (a [ClientOnly] Harmony patch class whose transpiler swaps vanilla's RefreshPartiesList() call for this class's RefreshPartiesListInRefresh).
- QuestJournalAdapter moved from Main/Adapters to Client/Adapters unchanged; new Main/Adapters/NullQuestJournalAdapter.cs is registered by ShadowfaxScreensModule.cs, and Client/TaomClientComposition.cs replaces it (applied in Main/IoC.cs after module registration with IfAlreadyRegistered.Replace).
- Categories PatchXXX_* renumbered Patch127_ShadowfaxClan, Patch128_ShadowfaxInventory, Patch129_ShadowfaxParty, Patch130_ShadowfaxQuests (Patch123 retired).
- Conflicts resolved in Main/Composition/FeatureModules.cs, Main/Features/CoopInterop/CoopSettingsRelevance.cs, settings-count tests (TaomSettings 474, BattleLoadDiagnosticsSettings 24, ServerSettingsSchema 509), ReflectionSiteBindingTests.cs, docs.
Known suspects (CONFIRM or DISPUTE with evidence):
S1. Any method TAOM.dll JITs on a dedicated server still names a ViewModelCollection type (signatures, fields, generic args, static constructors, lambdas) in the Shadowfax code.
S2. QuestLogDeferService or anything holding IQuestJournalAdapter is resolved before ClientComposition.Apply, so a client keeps the null adapter and the quest log fast open silently never binds.
S3. On a server, ShadowfaxScreensModule's install/bind lines log a warning or error every boot, or a Shadowfax static is touched in a way that throws.
S4. The moved clan helpers changed behaviour: the CallSwap target, LastSwapped/LogInstall, the co-op-off gate, or Harmony treating a moved public static method as a patch method.

-- MERGE B: E:/repos/taom-merge-career-forks, branch feature/career-forks (#769 Gondor career forks: two-effect options, troop classes, ten campaign career perks E11, generator), base 498 commits behind trunk.
Resolution work:
- Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs: GetDailyHealingForRegulars = trunk's CampRestModelLine.Apply on base, then the branch's ApplyWoundRecovery.
- Main/Features/BattleBalance/Hooks/SurvivalChanceLine.cs (trunk's extraction of GetSurvivalChance): the career TroopSurvival term is now Math.Min(1f, careerPassives.ScaleByPassive(heroId, result, TroopSurvival)) (the branch's bounded scale); SurvivalChanceMath.ApplyCareerSurvival and its 2 tests deleted.
- One PartyTrainingModel slot: trunk's Main/Features/SeasonedHosts/Models/TaomPartyTrainingModel.cs (declared by SeasonedHostsModule) now also applies the career TroopTraining ApplyFlat after its AddFactor; the branch's own CareerSystem TaomPartyTrainingModel and its SubModule.cs AddModel line were removed.
- 26 language files plus taom_career_data_strings.xml and taom_module_strings.xml merged by string id (only additions: 63 and 51 ids).
- tools/translate_with_claude.py kept trunk's (--seed-only requires --sync-ids); tools/generate_career_forks.py now prints --sync-ids --seed-only.
Known suspects:
S5. A GameModel slot is filled twice, or a branch change in Main/SubModule.cs wiring targets a model trunk moved into a FeatureModules GameModelDecl (the second registration wins and drops the other's logic).
S6. The AddFactor-then-ApplyFlat order in the training model, or the CalculateXpGainFromBattles member, gives a wrong XP number for any party (player, AI lord, garrison) compared with each feature alone.
S7. The survival change differs from trunk for a party with no career hero, a zero magnitude, a value above 1, NaN, or a magnitude of -1 or less; or ScaleByPassive's [CareerPerks] log floods on per-casualty rolls.
S8. A localization id was lost, duplicated or mismatched against its English {=KEY} source in the merged files.
S9. Other non-conflicting collisions between the branch's 498-commit-old code and trunk (ADR-013 client moves of career UI, AiPartySize factors, settings counts, save definers).

QUALITY GATES: cite file:line for every claim; mark anything you could not verify UNVERIFIED; try to refute each finding before reporting it. Severity per .ai/review-reference.md. Report sections: SUMMARY, KNOWN SUSPECTS (S1-S9 verdicts), FINDINGS (severity, location, impact, proof), UNVERIFIED.
