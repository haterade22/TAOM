# Input pack: WP 111 battle-freeze-mitigations

Title (draft): Cap battle corpses (#701), end AI battles stuck at N vs 0 (#748), make mission behaviour initialisation run
Wave: 1   Lane: A   Draft priority: P0   Groups: 4   Port commits: 7
Depends on (WP ids): 105, 109

## Orchestrator notes for this WP (binding)
- V04: on v1.4.8 AddMissionBehavior calls only OnCreated, so TAOM's OnBehaviorInitialize overrides on behaviours added in OnMissionBehaviorInitialize never run in a normal start (SiegeDismount is dead code on 1.4.x).
- c17541f2c creates IGraphicsOptionsAdapter/GraphicsOptionsAdapter that WP117 extends; re-verify the native fade and cap numbers on v1.4.8 (they were read from v1.5.3).
- Port 2a5363e90 (ratchet + rules) and fac17e05c + cfca3c5fd + bb85e9284 + 9cb6d49e7 together (K3).

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/V03-skeleton-buffer-v148.md
- plans/_audit/2026-11-04-port148/verify/V04-tick-handshake-and-start-swallow.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md
- plans/_audit/2026-11-04-port148/verify/K1-skip-feature-critic.md

## Deep-dive findings and non-port items this WP owns
- LOADING-HANG-07 [P2/medium] Corpses and ragdolls are a mid-battle frame cost on 1.4.8, not a load-hang path; the per-battle corpse cap ports cleanly | port: c17541f2c  (report: plans/_audit/2026-11-04-port148/deepdive/loading-hang.md)
- FREEZE-THREADING-08 [P2/high] AI map battles stuck at N vs 0 (#748): the engine mechanism holds on v1.4.8, the 1.4.x guard is absent, and the TAOM trigger (#750) is fixed only on the origin tip | port: a913629cd  (report: plans/_audit/2026-11-04-port148/deepdive/freeze-threading.md)

## Port groups owned by this WP (from the commit classification)

### group `issue-606-dead-onbehaviorinitialize`  P1  track=tests-ci  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/SignatureStrikes/Hooks/ISignatureAgentRoster.cs, Main/Features/SignatureStrikes/Hooks/SignatureAgentRoster.cs, Main/Features/SignatureStrikes/Hooks/SignatureMissionGate.cs, Main/Features/SignatureStrikes/Hooks/SignatureStrikesMissionLogic.cs, TAOM.Tests/Migration/MissionBehaviorLifecycleTests.cs
- `2a5363e90` 2026-09-16 PORT-PARTIAL P2 pick=conflict :: fix(combat): v2.0.29 - signature strikes gated in a dead callback (#606)
  - why: v1.4.8's Mission.AfterStart also runs OnBehaviorInitialize before SubModule.OnMissionBehaviorInitialize adds TAOM's behaviors, and AddMissionBehavior calls only OnCreated, so the dead-callback ratchet test and rules apply to 1.4.x; the SignatureStrikes code does not.
  - port notes: Verified in _categories_v1.4.8 Mission.cs: AfterStart :3799, OnBehaviorInitialize loop :3811, submodule OnMissionBehaviorInitialize :3815, AddMissionBehavior ends with OnCreated :4628. Take TAOM.Tests/Migration/MissionBehaviorLifecycleTests.cs (its four KnownOverriders are exactly the four OnBehaviorInitialize overrides on origin/bannerlord-1.4.5: SmartCavalryAI, MixedFormations, SiegeDismount mission behaviors and CustomBattleTeamFixBehavior), the .claude/rules/harmony-patches.md lifecycle table and the deep-review SKILL.md compatibility line, re-citing the v1.4.8 line numbers above instead of v1.5.3's. Leave all SignatureStrikes files and docs. The real 1.4.x bug, the three siblings' setup never running (E3 shows all three added during MissionAfterStart), is fixed by fac17e05c, which should follow as P1.
  - conflicts: Main/Features/SignatureStrikes/Hooks/ISignatureAgentRoster.cs, Main/Features/SignatureStrikes/Hooks/SignatureAgentRoster.cs, Main/Features/SignatureStrikes/Hooks/SignatureMissionGate.cs, Main/Features/SignatureStrikes/Hooks/SignatureStrikesMissionLogic.cs
  - issues: 606
- `fac17e05c` 2026-09-16 PORT P1 pick=conflict :: fix(mission): v2.0.29 - siblings leave the dead OnBehaviorInitialize (#606)
  - why: SiegeDismount, SmartCavalryAI and MixedFormations keep their setup in OnBehaviorInitialize, which v1.4.8 Mission.AfterStart (Mission.cs:3799) dispatches before SubModule.OnMissionBehaviorInitialize adds them (1.4.x SubModule.cs:1811-1815), so Siege Dismount is inert on 1.4.x too.
  - port notes: Take the three behaviour edits (OnBehaviorInitialize to AfterStart) and the three feature-doc notes. Verified on v1.4.8: AddMissionBehavior calls only OnCreated; AfterStart runs after every EarlyStart; DefaultBattleMissionAgentSpawnLogic.AfterStart (:158) only wires the banner bearer and spawns from OnMissionTick, so the mount is still stripped before the player agent is built. 1.4.x has the same four overriders (these three plus CustomBattleTeamFixBehavior). TAOM.Tests/Migration/MissionBehaviorLifecycleTests.cs is new vs 1.4.5: it was created by 2a5363e90, whose SignatureStrikes half is 1.5.x-only; create the ratchet test directly in its post-fac17e05c shape (allowlist CustomBattleTeamFixBehavior only) and take 2a5363e90's harmony-patches.md firing-set note. Re-subject to v2.0.28 and hand-write the CHANGELOG entry. A mounted-siege smoke test is owed (not tested on 1.5.x either).
  - prereqs: 2a5363e90
  - conflicts: TAOM.Tests/Migration/MissionBehaviorLifecycleTests.cs
  - issues: 606

### group `issue-699-siege-dismount-custom-battle`  P1  track=load-hang  commits=3  sim: clean=0 conflict=3
symptoms: loading-hang
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Adapters/BorderRenderAdapter.cs, Main/Adapters/IBorderRenderAdapter.cs, Main/Adapters/MapTerrainAdapter.cs, Main/Adapters/PlayerMountAdapter.cs, Main/Features/CoopInterop/CoopSettingsRelevance.cs, Main/Features/RealmBorders/Cheats/RealmBordersCheats.cs, Main/Features/RealmBorders/Domain/BorderLook.cs, Main/Features/RealmBorders/Domain/RealmLabelPlacer.cs, Main/Features/RealmBorders/IRealmBordersSettings.cs, Main/Features/RealmBorders/RealmBorderService.cs, Main/Features/RealmBorders/RealmBordersSettingsProvider.cs, Main/Features/TaomSettings.cs, Main/_Module/ModuleData/realm_borders/palette.json, TAOM.Tests/Adapters/SiegeDismountAdaptersNoCampaignTests.cs, TAOM.Tests/Features/RealmBorders/RealmBorderServiceTests.cs, TAOM.Tests/Features/RealmBorders/RealmBordersProviderTests.cs, TAOM.Tests/Features/RealmBorders/RealmPaletteTests.cs
- `cfca3c5fd` 2026-09-30 PORT-PARTIAL P1 pick=conflict :: Add realm fill functionality and associated tests
  - why: This Realm Borders commit also carries the SiegeDismount adapter guards. Hero.MainHero and MobileParty.MainParty throw when there is no campaign, which made Custom Battle sieges reload forever on 1.5.x, and the 1.4.x adapters make the same unguarded reads.
  - port notes: Take only Main/Adapters/PlayerMountAdapter.cs, Main/Adapters/PartyMountInventoryAdapter.cs, TAOM.Tests/Adapters/SiegeDismountAdaptersNoCampaignTests.cs (use the bb85e9284 version) and the adapters-taleworlds-api.md lesson line. Leave every RealmBorders, TaomSettings, coop and palette change. The bug is latent on 1.4.x: its SiegeDismountMissionBehavior starts from OnBehaviorInitialize, which v1.4.8 never sends to a TAOM-added behavior (Mission.AfterStart dispatches it at Mission.cs:3811, before submodules add behaviors at :3815, and AddMissionBehavior at :4615 only calls OnCreated). The adapters therefore run only once fac17e05 (#606) moves the call to AfterStart. Land this series with or before fac17e05.
  - conflicts: Main/Adapters/BorderRenderAdapter.cs, Main/Adapters/IBorderRenderAdapter.cs, Main/Adapters/MapTerrainAdapter.cs, Main/Features/CoopInterop/CoopSettingsRelevance.cs, Main/Features/RealmBorders/Cheats/RealmBordersCheats.cs, Main/Features/RealmBorders/Domain/BorderLook.cs, Main/Features/RealmBorders/Domain/RealmLabelPlacer.cs, Main/Features/RealmBorders/IRealmBordersSettings.cs, Main/Features/RealmBorders/RealmBorderService.cs, Main/Features/RealmBorders/RealmBordersSettingsProvider.cs, Main/Features/TaomSettings.cs, Main/_Module/ModuleData/realm_borders/palette.json, TAOM.Tests/Features/RealmBorders/RealmBorderServiceTests.cs, TAOM.Tests/Features/RealmBorders/RealmBordersProviderTests.cs, TAOM.Tests/Features/RealmBorders/RealmPaletteTests.cs
- `9cb6d49e7` 2026-09-30 PORT-PARTIAL P3 pick=conflict :: fix(siege-dismount): prevent exceptions in Custom Battle sieges by guarding adapter calls
  - why: Feature-doc update for the SiegeDismount Custom Battle fix: guarded reads, campaign-only scope and a Custom Battle siege check.
  - port notes: Fold into the bb85e9284 port and reword for 1.4.x. The doc says every Custom Battle siege since v2.0.29 threw out of AfterStart, which becomes true on 1.4.x only after fac17e05. The current 1.4.x doc still claims OnBehaviorInitialize fires (siege-dismount.md line 121), which the v1.4.8 engine contradicts.
  - prereqs: cfca3c5fd
- `bb85e9284` 2026-09-30 PORT P1 pick=conflict :: fix(siege-dismount): guard adapter calls to prevent NRE in Custom Battle sieges
  - why: Moves SiegeDismountService's first adapter read inside its try and finishes the adapter guards. An exception can then no longer escape mission start and make the engine re-run the mission load every frame; the 1.4.x service has the same gap.
  - port notes: Take: SiegeDismountService.cs (the 1.4.x OnMissionStart matches the pre-fix code, with HasMount outside the try), PlayerMountAdapter.HeroBattleEquipmentOf, the rewritten SiegeDismountAdaptersNoCampaignTests, the new SiegeDismountServiceTests case, the .claude/rules/adapters.md and harmony-patches.md paragraphs, the feature doc, the lesson line and docs/reviews/rca-siege-dismount-custom-battle-2026-09-30.md. The bug stays latent until fac17e05 (#606) is ported (see cfca3c5fd). Porting fac17e05 without this series would bring the Custom Battle siege reload loop to 1.4.x.
  - prereqs: cfca3c5fd
  - conflicts: Main/Adapters/PlayerMountAdapter.cs, TAOM.Tests/Adapters/SiegeDismountAdaptersNoCampaignTests.cs

### group `issue-701-battle-corpses`  P0  track=freeze-threading  commits=1  sim: clean=0 conflict=1
symptoms: battle-freeze
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Composition/FeatureModules.cs
- `c17541f2c` 2026-10-01 PORT-PARTIAL P0 pick=conflict :: fix(battle-corpses): v2.0.32 - fade and cap battle corpses (#701)
  - why: Caps and fades battle corpses per mission and offers to lower the ragdoll and corpse options, targeting the mid-battle freezes 1.4.8 players reported. Every engine call it makes exists in v1.4.8.
  - port notes: Take: the Main/Features/BattleCorpses files except BattleCorpsesModule.cs, IGraphicsOptionsAdapter and GraphicsOptionsAdapter, the MountDespawnMissionGate comment, the TaomSettings toggles and sliders, the taom_module_strings.xml notice keys (then translate through /localize, #703), the five test classes and docs/features/battle-corpses.md. Re-wire for 1.4.x, which has no Main/Composition or TaomFeatureModule: register the services in IoC.cs, add BattleCorpseMissionBehavior with AddTaomBehavior in SubModule.OnMissionBehaviorInitialize, and call BattleSettingsAdviceNotifier.OfferIfRisky from SubModule.OnBeforeInitialModuleScreenSetAsRoot next to the stall notice (line 560 on origin/bannerlord-1.4.5). Adapt BattleCorpsesWiringTests to match, and update CoopSettingsRelevance and SettingsFingerprintTests. Verified in the v1.4.8 dump: Mission.SetMissionCorpseFadeOutTimeInSeconds (Mission.cs:1723), SetOverrideCorpseCount (:1728), NativeOptions NumberOfRagDolls with GetConfig, SetConfig and SaveConfig, ManagedOptions.NumberOfCorpses and BannerlordConfig.NumberOfCorpses. MissionThreadGuard.NoteCall, FiniteFloatValidator.IsFiniteInRange and MountDespawnMissionGate.IsEligible exist on 1.4.x. Re-check on v1.4.8 the native behaviour read from the v1.5.3 DLL: the 3,600 s default fade, the 1,021-body Unlimited cap and the mission reset restoring both.
  - conflicts: Main/Composition/FeatureModules.cs
  - issues: 701, 703

### group `issue-748-stuck-battle-guard`  P1  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Composition/FeatureModules.cs
- `a913629cd` 2026-10-06 PORT-PARTIAL P1 pick=conflict :: fix(battles): v2.0.34 - end AI battles stuck at N vs 0 (#748)
  - why: Adds an hourly sweep that ends AI map battles stuck at N vs 0, which freeze a faction's armies; the engine precondition holds on v1.4.8 and MapEventGuard exists on both lines.
  - port notes: Verified on v1.4.8: MapEvent.Update (MapEvent.cs:995) runs a round only while both sides' TroopCount > 0 and BattleState is set only inside a round, so a side reaching 0 between rounds never loses; every member the adapter uses exists (IsFinalized :225, BattleStartTime :227, SetOverrideWinner :1058, MapEventSide.RecalculateMemberCountOfSide :319, MobileParty.IsCurrentlyUsedByAQuest :417). IEnlistmentStateQuery, ICoopSessionProvider and TaomConsole.RunInCampaign exist on 1.4.5. Replace StuckBattleModule (Main/Composition, 1.5.x-only) with IoC.cs registrations plus the campaign behavior added in SubModule's campaign starter (single-owner files). The named TAOM trigger, alignment desertion removing troops from parties in battle (#750, a later follow-up), exists on 1.4.x too.
  - conflicts: Main/Composition/FeatureModules.cs
  - issues: 748, 750

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 1, batch 2, batch 12, batch 14
