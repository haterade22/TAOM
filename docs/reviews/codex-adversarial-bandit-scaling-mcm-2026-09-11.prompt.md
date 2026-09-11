ADVERSARIAL REVIEW: #559 bandit scaling MCM (commits 2652fe2f and 37411388 on bannerlord-1.4.5)

You are reviewing two commits. Use `git show 2652fe2f` and `git show 37411388` for the exact content, and `git show 37411388:<path>` to read any file as committed. The WORKING TREE carries unrelated uncommitted edits from other sessions (SpecialResources, ShaderPrecompilation, SubModule.cs, CoopSettingsRelevance.cs, tools/, Languages/, translation_cache/). Those are OUT OF SCOPE: do not review them, do not report them, do not let them confuse a `git diff` (use `git diff 2652fe2f~1 37411388 -- <path>` for the reviewed change).

FEATURE: A player reported that switching Bandit Scaling off in MCM did nothing. Three defects were found and fixed. (1) 157 of the 217 value settings in Main/Features/TaomSettings.cs (plus 12 in three small settings classes) omitted `RequireRestart = false`; MCM's `BaseSettingPropertyAttribute` defaults it to true, and with it true, pressing Done raises a "Game Needs to Restart" inquiry whose Cancel delegate is empty and is followed by `return`, so the change never reaches TAOM.json (it still applies in-session through MCM's undo stack, which is why it looked applied). 166 attributes gained the flag; 3 keep true by allowlist. A reflection test now enforces the posture. (2) `ModuleData/bandit_management/bandit_scaling_config.json` was the `??` fallback behind MCM knobs and could never be read while MCM is loaded; deleted with its config provider; constants moved into `BanditScalingSettingsProvider`. (3) Defaults: `BanditInitialHideoutsPerFaction` 14 to 7 (8 bandit factions x 14 put 112 hideouts on a fresh map; read once at world-gen) and `BanditMaxPartiesPerHideout` 3 to 6 (3 equalled vanilla's base and `TaomBanditDensityModel.Cap()` takes max(base, cap) as the ceiling, so Density Curve never moved it).

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
- docs/features/bandit-management.md (as committed at 37411388)
- docs/reviews/rca-bandit-scaling-mcm-2026-09-11.md (two review passes already ran; six agents each; do not repeat their findings, go past them)
- CHANGELOG.md, the entry "fix(mcm): 166 settings told players to restart" under 2026-09-11
- .claude/rules/csharp-architecture.md "Config Providers MUST Validate" (the rule the change touches)

KNOWN SUSPECTS (CONFIRM or DISPUTE each, with decompiled or grepped evidence):

S1. The load-bearing claim. Decompile Bannerlord.MBOptionScreen.v1.4.5.dll (path below), type MCM.UI.GUI.ViewModels.ModOptionsVM, the Done handler (ExecuteDone / ExecuteDoneInternal). CONFIRM or DISPUTE: when any changed settings VM reports RestartRequired(), the inquiry's negative (Cancel) delegate performs no SaveSettings and the method returns before the save loop; when none does, every changed VM gets SaveSettings() and no inquiry appears. Then decompile MCMv5.dll MCM.UI.Actions is NOT there; instead find in MBOptionScreen the UndoRedoStack.Do and confirm the slider edit writes through to the live settings instance before Done. If any of this is wrong, the whole rationale for the sweep is wrong; say so plainly.

S2. The allowlist is complete. The posture test keeps RequireRestart = true only for TaomSettings.EnableNativeSkinFixes (parked), CrashReportSettings.EnableCrashCapture and CrashReportSettings.EnableNativeToManagedCapture (both read once in SubModule.OnSubModuleLoad to gate patch install). Hypothesis to attack: some OTHER setting among the 166 flipped is consumed only once at process start, so the flag now lies in the other direction. Look for: a `*SettingsProvider` constructor that copies a VALUE (not the settings object) into a field; a `Reuse.Singleton` service whose constructor reads `TaomSettings.Instance?.X` into a readonly field; any `_harmony.PatchCategory(...)` or `IoC.Resolve<...>()` in SubModule.cs / IoC.cs whose execution depends on a settings VALUE at that moment; a static initializer; a hotkey registered once from `MixedFormationsCycleHotkey`. Name the property and the line if you find one.

S3. Natural attrition. The docs and hints say hideouts already on the map "stay until the player clears them" because vanilla only ADDS hideouts while below the max. Decompile TaleWorlds.CampaignSystem.Settlements.Hideout (IsInfested is computed from bandit parties present) and BanditSpawnCampaignBehavior (v1.4.8). CONFIRM or DISPUTE: can an infested hideout become un-infested WITHOUT the player, e.g. its parties destroyed by lords or wandering off, so that the count drifts down toward the max by itself? If yes, the doc sentence overstates; say exactly which sentence and what the accurate statement is.

S4. Spawn math with the new cap. BanditSpawnCampaignBehavior._numberOfMaxBanditCountPerClanHideout combines NumberOfMaximumBanditPartiesAroundEachHideout (unscaled vanilla 3) with NumberOfMaximumBanditPartiesInEachHideout (now up to 6 at endgame under the default curve). Trace SpawnBanditsAroundHideout and HourlyTickClan and state the per-hour spawn expectation at PlayerProgress 0, 0.5, 1.0 with 8 factions and the new defaults versus the old ones. Is anything in that path reading the value in a way that the doc's Cap() table (3 / 5 / 6) does not describe? Also: Patch39_BanditPartySize scales each roster toward the template's stack MaxValue. Does the combination create a party-count x party-size product that the hint texts under-describe?

S5. Deletion blast radius through reflection or dynamic resolution. The commit removed IBanditScalingConfigProvider and its registration. Grep for anything that enumerates DryIoc registrations, resolves by convention, or names the removed type in a string: CoopInterop settings fingerprint, CrashReport collectors, wiring tests that assert registration counts, any `ResolveMany` / `IsRegistered` by name. Also `Main/_Module/ModuleData/` references and `tools/*.py` that listed bandit_management/. Anything left dangling?

S6. The posture test's reflection filter. It matches attribute types by `GetType().Name.StartsWith("SettingProperty")`, excludes SettingPropertyGroupAttribute and SettingPropertyButtonAttribute by exact name, then reads a `RequireRestart` property by reflection and asserts it. MCMv5 also ships a legacy `MCM.Abstractions.Attributes.v1.SettingPropertyAttribute`. Does TAOM use the v1 attribute anywhere? If it did, would the test throw (no RequireRestart property) rather than report? Is the `seen > 200` floor sound? Could a Dropdown property whose type is `Dropdown<string>` fail the `new TaomSettings()` construction path the test relies on?

FILES (all as committed at 37411388):
Feature: Main/Features/BanditManagement/BanditManagementIoC.cs, BanditScalingSettingsProvider.cs, IBanditScalingSettingsProvider.cs, BanditScalingService.cs, IBanditScalingService.cs, Models/TaomBanditDensityModel.cs, Hooks/Patch39_BanditPartySize.cs, Hooks/Patch40_HideoutDescription.cs (unchanged, for context)
Settings: Main/Features/TaomSettings.cs, Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs, Main/Features/CrashReport/CrashReportSettings.cs, Main/Features/BlowDiagnostics/BlowDiagnosticsSettings.cs
Registration: Main/SubModule.cs lines 160-171 (CrashReport gates), 864 (AiPartySizeSettingsWatcher), 978 (TaomBanditDensityModel), 1357 (Patch39 category); Main/IoC.cs line 135
Tests: TAOM.Tests/Features/BanditManagement/BanditScalingSettingsProviderTests.cs, TaomBanditDensityModelTests.cs, BanditScalingServiceTests.cs; TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs; TAOM.Tests/Features/AiPartySize/AiPartySizeSettingsWatcherTests.cs (context)
Deleted (read via `git show 2652fe2f~1:<path>`): Main/Features/BanditManagement/BanditScalingConfig.cs, IBanditScalingConfigProvider.cs, BanditScalingConfigProvider.cs, Main/_Module/ModuleData/bandit_management/bandit_scaling_config.json, TAOM.Tests/Features/BanditManagement/BanditScalingConfigProviderTests.cs
Data: Main/_Module/ModuleData/taom_spcultures.xml (count is_bandit="true")
Docs: docs/features/bandit-management.md, docs/reviews/rca-bandit-scaling-mcm-2026-09-11.md, docs/reviews/lessons/localization-ui.md (last three entries), docs/reviews/lessons/build-tooling-workflow.md (last entry)

VANILLA AND LIBRARY TARGETS (decompile with ilspycmd; the installed DLLs are authoritative, E:\Decompiled_Bannerlord is a dump):
- "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll": TaleWorlds.CampaignSystem.GameComponents.DefaultBanditDensityModel, TaleWorlds.CampaignSystem.CampaignBehaviors.BanditSpawnCampaignBehavior, TaleWorlds.CampaignSystem.Settlements.Hideout, TaleWorlds.CampaignSystem.GameComponents.DefaultPartySizeLimitModel (FindAppropriateInitialRosterForMobileParty), TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiLandBanditPatrollingBehavior
- "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM.Dependencies/bin/Win64_Shipping_Client/MCMv5.dll": MCM.Abstractions.Attributes.BaseSettingPropertyAttribute, MCM.Abstractions.Base.Global.GlobalSettings`1, MCM.Abstractions.Base.BaseSettings (SaveTriggered), MCM.Implementation.BaseSettingsContainer`1 (SaveSettings raising SAVE_TRIGGERED)
- "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM.Dependencies/bin/Win64_Shipping_Client/Bannerlord.MBOptionScreen.v1.4.5.dll": MCM.UI.GUI.ViewModels.ModOptionsVM, MCM.UI.GUI.ViewModels.SettingsVM (RestartRequired, SaveSettings), MCM.UI.Actions.UndoRedoStack
Paste the decisive decompiled lines as code blocks. A claim about engine or library behaviour without a pasted line is an observation, not a finding.

REQUIRED SECTIONS:
1. VANILLA CODE: the decompiled lines for S1, S3, S4 as code blocks.
2. KNOWN SUSPECTS: S1 to S6, each CONFIRMED or DISPUTED with evidence.
3. SETTINGS SWEEP: your own independent scan of every `[SettingProperty*]` in the four classes for a one-shot consumer (S2 in full; list what you checked, not only what you found).
4. CONFIG CROSS-REFERENCE: the six bandit hints in TaomSettings.cs against DefaultBanditDensityModel's constants and taom_spcultures.xml; the feature doc's two tables against the code.
5. FINDINGS OR OBSERVATIONS: severity P1 (ships broken) / P2 (wrong but survivable) / P3 (quality). For each: file:line, what is wrong, the evidence, the minimal fix. If you find nothing at a severity, say so; do not manufacture findings, and do not report the six items already in the RCA's second pass unless you dispute a fix.

QUALITY GATES: every finding cites a file and line in the commit; every engine or library claim has a pasted decompiled line; every "missing" claim shows the grep you ran; you state which of S1 to S6 you could not resolve and why.

PRIOR REVIEW LESSONS:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches. On this feature in 2026-05-27 you caught a Harmony patch with no category (dead in production) and an XML comment with `--` inside it.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections. Codex has reported a method as absent after grepping the wrong directory.

OUTPUT: write the review to docs/reviews/raw/codex-adversarial-bandit-scaling-mcm-2026-09-11.md (stdout is captured there; start with a one-paragraph verdict, then the sections above).
