# Adversarial review -- TAOM BannerBearers feature

You are performing an adversarial code review of a NEW feature in TAOM (Tales From the Age of Men), a Lord of the Rings total-conversion mod for Mount and Blade II: Bannerlord. Target engine: v1.4.7.

Your job is to find bugs that will bite in a live battle. Be specific and evidence-based. Cite file and line. If you cannot verify a claim, say UNVERIFIED rather than guessing. A confident wrong finding is worse than an honest "I could not check this".

## What the feature does

Bannerlord already ships a complete banner-bearer system (`BannerBearerLogic : MissionLogic`) and the engine already adds it to every field battle, sally-out and siege. It never switches on, because `SetFormationBanner` has only two gameplay callers: `GeneralsAndCaptainsAssignmentLogic` (only when a formation has a hero captain whose `FormationBanner` is non-null) and the player's Order-of-Battle screen. TAOM's lords carry no banner items, so no formation ever gets one.

This feature supplies the missing call plus the policy the engine asks for through a GameModel. It deliberately does NOT reimplement bearer spawning -- the engine's own `UpdateAgent` converts an EXISTING agent in place (never respawns), which is what makes a bearer keep its race (an orc formation's bearer must stay an orc).

Two components:
1. `TaomBattleBannerBearersModel : SandBox.SandboxBattleBannerBearersModel` -- overrides 3 of the 9 members (bearers-per-formation, race gate, minimum formation size).
2. `BannerBearerAssignmentMissionLogic : MissionLogic` -- overrides `OnTeamDeployed(Team)` only, and calls `SetFormationBanner(formation, item)` for formations vanilla skipped.

No Harmony patches. No new art (uses vanilla's 45 banner items). No MCM (JSON config only).

## TAOM ID CHEATSHEET (authoritative -- use this, do not guess)

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa

Culture IDs (TAOM-declared, in taom_spcultures.xml -- 22 total): gondor, gondor_soldiers, erebor, erebor_warriors, rivendell, lothlorien, mirkwood, mirkwood_stalkers, isengard, mordor, dolguldur, gundabad, gundabad_raiders, goblin, mistymountainorcs, umbar, umbar_corsairs, shaghana, abanissa, dunland_raiders, rhun_raiders, harad_raiders

Culture IDs (vanilla ids RE-SKINNED by spcultures.xslt -- the XSLT overrides name but NEVER id): vlandia=Rohirrim, empire=Dunlendings, aserai=Haradrim, khuzait=Easterlings/Rhun, sturgia=Barding/Dale, battania=Variag/Khand

CRITICAL: "rohan" is NOT a valid culture ID -- Rohan uses "vlandia". Likewise "dunland"/"harad"/"rhun"/"dale"/"khand" are NOT culture IDs. "dol_guldur" is NOT valid -- use "dolguldur".

An earlier revision of this feature keyed its culture map on the LOTR display names and all six keys were silently dead. That is already FIXED -- do not re-report it. Verify the fix is correct and complete instead.

## READ FIRST

- docs/features/banner-bearers.md -- the feature doc, including the governing constraint
- docs/reviews/rca-banner-bearers-2026-07-16.md -- the RCA from the internal 5-agent review; lists what was already found and fixed
- Main/_Module/ModuleData/banner_bearers/banner_bearers_config.json -- the shipped config

## KNOWN SUSPECTS -- CONFIRM or DISPUTE each with evidence

S1. THE FREEZE INVARIANT (highest risk in the feature).
`SetFormationBanner` unconditionally calls `UpdateBannerBearersForDeployment`, which promotes agents via `UpdateAgent`, which ends in `agent.SetIsAIPaused(isPaused: true)`. Our claim: the ONLY unpause in a battle is `DeploymentMissionController.FinishDeployment`, which then removes itself from the mission -- so calling `SetFormationBanner` outside the deployment window freezes every bearer for the entire battle (banners appear, bearers never move). Our guard is `if (mission == null || mission.Mode != MissionMode.Deployment) return;` in `OnTeamDeployed`.
HYPOTHESIS: the guard is sufficient for every battle type TAOM ships.
Verify by enumerating EVERY call site of `Mission.OnTeamDeployed` and, for each, determining whether `Mission.Mode == Deployment` holds AND whether a live `DeploymentMissionController` (or subclass) is guaranteed to later call `FinishDeployment`. Pay attention to: field battle, siege, sally-out, hideout, arena/tournament, village/settlement missions, and any battle with no deployment phase. If ANY path can reach `SetFormationBanner` with `Mode == Deployment` but no controller to unpause, that is CRITICAL.

S2. MASTER-TOGGLE FOLD COMPLETENESS.
Config `Enabled=false` promises "exact vanilla". The GameModel stays REGISTERED when disabled and the engine still asks it about every formation -- including formations bannered by vanilla's own hero-captain path or the player's Order-of-Battle screen, which TAOM never intercepts. Every override therefore folds the toggle by deferring to `base`, not by returning a TAOM value or a zero.
HYPOTHESIS: with `Enabled=false`, TAOM's model is behaviourally identical to `SandboxBattleBannerBearersModel` on every code path.
Verify each of the 3 overrides. Find any remaining path where a disabled feature differs from vanilla. (A previous revision returned 0 bearers when disabled, which SUPPRESSED vanilla-assigned banners -- worse than vanilla. That is fixed; check the fix is total.)

S3. THRESHOLD STABILITY vs EXACT-EQUALITY EDGE DETECTION.
`BannerBearerLogic.OnAgentAdded`/`OnAgentRemoved` detect the bearer-eligibility threshold with EXACT equality (`formation.CountOfUnits == minimumFormationTroopCountToBearBanners` and `== minimum - 1`), not `>=`. Our `GetMinimumFormationTroopCountToBearBanners` now returns `_service.IsEnabled ? config value (4) : base (2)`.
HYPOTHESIS: the value cannot change mid-mission, because the config is a `Lazy<T>` inside a `Reuse.Singleton` DryIoc registration (process-lifetime, never reloaded) and there is no MCM surface.
Verify there is no path that reloads the config or flips `Enabled` at runtime. If the value CAN change mid-mission, the exact-equality edges silently stop firing -- report severity.

S4. UNMAPPED CULTURES / FAIL-CLOSED DEFAULT.
38 cultures are registered at runtime; the config maps 28. `DefaultBannerItemId` is deliberately `""` so the other 10 (looters, sea_raiders, forest_bandits, desert_bandits, mountain_bandits, steppe_bandits, nord, vakken, darshi, neutral_culture) field NO banner. A non-empty default would have handed a Gondorian standard to every looter warband.
HYPOTHESIS: `ResolveBannerItemId` returns null for all 10, and `TryAssignBanner` then returns before calling `SetFormationBanner`, so nothing happens for them.
Verify. Also check: is `""` handled identically to a MISSING key? Are the 28 mapped keys each a real culture id (cross-reference taom_spcultures.xml and spcultures.xslt)?

S5. FormationIndex vs PhysicalClass.
`GetDesiredNumberOfBannerBearersForFormation` reads `formation.FormationIndex` to pick the per-class density ratio (Infantry 1-per-20, Ranged 1-per-25, Cavalry 1-per-15, HorseArcher 1-per-15, Other 1-per-25). `Formation` also exposes `PhysicalClass`, which reflects the CURRENT composition of units rather than the formation's assigned slot class.
HYPOTHESIS: `FormationIndex` is correct here (it is the formation's identity, stable for the mission), and `PhysicalClass` would make the ratio drift as casualties change composition.
Assess and give a recommendation with reasoning.

S6. N>1 BEARERS AND ARRANGEMENT INTERACTION.
Vanilla hardcodes exactly 1 bearer per formation. We return up to 6 (capped because the engine's four bearer-position tables are `RelativeFormationPosition[6]`). `UpdateBannerBearersForDeployment` ends with `RepositionFormation()` and uses `Formation.SwitchUnitLocations` to move bearers into position.
HYPOTHESIS: N up to 6 is fully supported and degrades cosmetically above 6, not crashing.
Verify by tracing every consumer of `GetDesiredNumberOfBannerBearersForFormation` for hardcoded single-bearer assumptions. ADDITIONALLY: TAOM ships a MixedFormations feature that patches `Formation.GetOrderPositionOfUnit` (a Prefix, `Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs`) and re-applies its own layout on a 1-second tick (`MixedFormationsMissionBehavior`). Assess whether the engine's bearer `SwitchUnitLocations` / `RepositionFormation` and MixedFormations' layout re-apply can fight each other. This interaction is UNTESTED and we want your read on it.

## VANILLA CODE -- decompile and paste the relevant methods as code blocks

The installed DLLs are AUTHORITATIVE. The dump at E:\Decompiled_Bannerlord\ may lag (its flat build is labelled v1.4.6 while the installed game is v1.4.7). Prefer:
  ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" -t "TaleWorlds.MountAndBlade.BannerBearerLogic"
  ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBox/bin/Win64_Shipping_Client/SandBox.dll" -t "SandBox.SandboxBattleBannerBearersModel"
A pre-decompiled v1.4.7 cache also exists at C:\Users\mikew\.taom-src\v1.4.7\.

Decompile and quote:
- `BannerBearerLogic.SetFormationBanner`, `FormationBannerController.UpdateBannerBearersForDeployment`, `BannerBearerLogic.UpdateAgent`, `BannerBearerLogic.GetMissingBannerCount`, `BannerBearerLogic.SpawnBannerBearer`, `BannerBearerLogic.CreateBannerEquipmentForAgent`, `BannerBearerLogic.OnAgentAdded`, `BannerBearerLogic.OnAgentRemoved`, `BannerBearerLogic.OnDeploymentFinished`, `BannerBearerLogic.OnEndMission`
- `SandBox.SandboxBattleBannerBearersModel` (all 9 members)
- `TaleWorlds.MountAndBlade.ComponentInterfaces.BattleBannerBearersModel`
- `DeploymentMissionController.FinishDeployment` and `SetupTeams`
- `GeneralsAndCaptainsAssignmentLogic.OnTeamDeployed` and its `SetFormationBanner` call site
- `Mission.OnTeamDeployed`

## TAOM FILES TO REVIEW

Feature:
- Main/Features/BannerBearers/IBannerBearerService.cs
- Main/Features/BannerBearers/BannerBearerService.cs
- Main/Features/BannerBearers/IBannerBearerConfigProvider.cs
- Main/Features/BannerBearers/BannerBearerConfigProvider.cs
- Main/Features/BannerBearers/Domain/BannerBearerConfig.cs
- Main/Features/BannerBearers/Models/TaomBattleBannerBearersModel.cs
- Main/Features/BannerBearers/Hooks/BannerBearerAssignmentMissionLogic.cs
- Main/Features/BannerBearers/BannerBearersIoC.cs

Config and data:
- Main/_Module/ModuleData/banner_bearers/banner_bearers_config.json
- Main/_Module/ModuleData/taom_spcultures.xml (the 8 is_bandit cultures gained `banner_bearer_replacement_weapons` in this changeset)
- Main/_Module/ModuleData/spcultures.xslt (the six re-skinned vanilla cultures)

Registration (single-owner convergence files -- review, do not propose large edits):
- Main/SubModule.cs -- the `AddModel<BattleBannerBearersModel>` line in OnGameStart, and the `AddTaomBehavior(new BannerBearerAssignmentMissionLogic())` line in OnMissionBehaviorInitialize
- Main/IoC.cs -- `BannerBearersIoC.RegisterBannerBearersFeature`

Tests:
- TAOM.Tests/Features/BannerBearers/BannerBearerServiceTests.cs
- TAOM.Tests/Features/BannerBearers/BannerBearerConfigProviderTests.cs
- TAOM.Tests/Features/BannerBearers/ShippedBannerBearerConfigTests.cs

Potentially interacting TAOM code:
- Main/Features/MixedFormations/ (patches Formation.GetOrderPositionOfUnit; re-applies layout on a 1s tick)
- Main/Features/BannerColorPersistence/Hooks/Mission_SpawnAgent_Patch.cs and Agent_EquipItemsFromSpawnEquipment_Patch.cs (banner bearers route through both)

## ALSO ANALYSE (feature-specific, concrete scenarios)

A. `BannerBearerAssignmentMissionLogic.OnTeamDeployed` iterates `team.FormationsIncludingEmpty` and calls `TryAssignBanner` for each. Trace: does `OnTeamDeployed` fire for BOTH teams (attacker and defender) in a field battle? Once per team, or once per side? Could it fire twice for the same team, and would a second `SetFormationBanner` on an already-bannered formation be harmful? (Note we guard with `if (bannerLogic.GetFormationBanner(formation) != null) return;`.)

B. What happens if `SetFormationBanner` is called on a formation with `CountOfUnits > 0` but ZERO agents that pass `CanAgentBecomeBannerBearer` (e.g. an all-hero formation, or a formation of an excluded race such as cave trolls)? Trace `UpdateBannerBearersForDeployment` and `FindBannerBearableAgents` for that case. Does it no-op cleanly, NRE, or leave an inconsistent controller?

C. `TryAssignBanner` resolves culture via `formation.GetFirstUnit()?.Character?.Culture?.StringId`. Is the FIRST unit representative of the formation's culture? What happens in a mixed-culture army (a player party with mercenaries, or an allied army)? Is there a better per-formation culture source?

D. The race gate excludes cave_troll and hill_troll. TAOM spawns cave trolls as ordinary battlefield troops. If a formation is ENTIRELY cave trolls, it will get a banner item assigned (culture maps to a banner) but no eligible bearer. Cross-reference with case B. Is that a real problem?

E. Config `MinimumFormationTroopCount = 4` while vanilla's is 2. Does raising it have any consequence beyond fewer banners -- specifically for the exact-equality edge detection in `OnAgentAdded`/`OnAgentRemoved`?

F. `BannerBearerConfigProvider` validates ranges and reverts per-field with a warning. Are there parseable-but-semantically-invalid inputs it accepts? Note all fields are int/string/collection -- no floats, so NaN is not a concern here. Confirm that reasoning.

## CONFIG CROSS-REFERENCE (mandatory)

For banner_bearers_config.json:
1. Every KEY in CultureBanners must be a real culture StringId. Cross-reference against taom_spcultures.xml (22 declared) and spcultures.xslt (6 re-skinned vanilla ids). Report any key that matches nothing.
2. Every VALUE must be a real banner ItemObject id present in "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBoxCore/ModuleData/items/banners.xml" with Type="Banner" and a populated `<Banner>` ItemComponent (required by `BannerBearerLogic.IsBannerItem`).
3. Every race id in ExcludedRaces must exist in "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/skins.xml". Note that file mixes single-line `<race id="dwarf">` with multi-line `<race\n  id="elf">` -- a naive grep misses elf and sauron.
4. Every culture mapped to a non-empty banner must declare `<banner_bearer_replacement_weapons>`, else its bearers spawn UNARMED (vanilla returns null and `CreateBannerEquipmentForAgent` clears the other weapon slots). Verify all 28.

## QUALITY GATES

- Cite file and line for every finding.
- Paste vanilla code blocks as evidence for any engine-behaviour claim.
- Separate CONFIRMED bugs from SPECULATIVE concerns. Mark severity CRITICAL / HIGH / MED / LOW.
- If you cannot verify something, write UNVERIFIED and say what you would need.
- Do not re-report the already-fixed culture-id bug or the already-fixed master-toggle-zero bug except to confirm the fixes are correct and complete.
- Do not flag code that merely matches vanilla behaviour as a bug.
- Do not skip the hard sections (S1 and S6 are the ones that matter most).

## FINDINGS OR OBSERVATIONS

End with an explicit verdict: SHIP / SHIP WITH FIXES / DO NOT SHIP, and a ranked list of what to fix first.

## Lessons from prior reviews

SUCCESSES: Config ID cross-reference caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES to avoid: Codex has previously assumed empire=Rohan (it is Dunland -- Rohan is vlandia). Codex has flagged vanilla-matching code as bugs. Codex has skipped the hard sections. Codex has claimed a method is "missing" without grepping.
