# Adversarial review: War of the Ring Momentum (#327)

You are an adversarial reviewer. Assume there ARE bugs; find them. The feature is a port of LOTRAOM 1.2.12's "Momentum" system into TAOM (Bannerlord 1.4.6). It tracks Evil-vs-Good war progress from battles/sieges/raids/army-gatherings/daily-strength, shows an on-map slider + detail popup, and ENDS the War of the Ring at a victory threshold. It was already reviewed by 5 internal agents and 6 findings were fixed; find what they missed. Be specific with file:line. Report CONFIRMED vs DISPUTED for each Known Suspect. Use -- not em-dash.

## TAOM ID CHEATSHEET
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa, goblin, mistymountainorcs, bluecraig, lindon
NOTE: "rohan" is NOT a valid ID (use vlandia). "dol_guldur" is NOT valid (use dolguldur). empire=Dunland (NOT Rohan).

## READ FIRST
- docs/reviews/rca-wotr-momentum-2026-07-03.md -- the 6 already-fixed findings + 3 accepted-with-note items. Do NOT re-report these unless the fix is wrong.
- Main/_Module/ModuleData/momentum/momentum.json -- tuning config
- Main/_Module/ModuleData/execution/alignment.json -- the Free/Evil/Neutral side source of truth (keyed on kingdom StringId; gondor/mordor are culture-only aliases)
- docs/features/war-of-the-ring.md -- the EXISTING WotR phase machine this integrates with

## Known Suspects (CONFIRM or DISPUTE each with file:line evidence)

1. **Scale bug in battle momentum.** MomentumEventService.CalculateBattleMomentum computes `round(casualties/loserSideStrength * MaxBattleMomentum * 100)`. If loserSideStrength is a small float and casualties large, could this overflow int or produce absurd momentum? Also: is `casualties/loserSideStrength` ever >1 (more casualties than total side strength), and does that break the "cap at MaxBattleMomentum" intent (there is no explicit Min clamp on the ratio)?

2. **Victory ordering / re-entrancy.** MomentumVictoryService.CheckAndApplyVictory calls state.MarkWarEnded -> _wotrService.EndWar -> PeaceOutCrossSidePairs (MakePeace). MakePeaceAction fires OnPeaceDeclared events. Could any of those events re-enter the momentum behavior (e.g. via a campaign event handler) while we're mid-victory, causing double-processing or a modified-collection-during-iteration on state.Free.KingdomIds/state.Evil.KingdomIds (PeaceOutCrossSidePairs iterates both while MakePeace may mutate diplomacy)?

3. **SyncData / save-compat.** MomentumStateStore serializes to Dictionary<string,string> under key "_taom_wotr_momentum"; WarOfTheRingBehavior adds a NEW int SyncData key "WarOfTheRing_Outcome". Is adding a new SyncData key to an existing behavior safe for OLD saves that lack it (does dataStore.SyncData default-init the ref, or throw)? Check the load path in both behaviors. Also: the momentum store serializes event descriptions containing arbitrary faction/hero names -- can a name contain the delimiter that breaks round-trip? (the store splits on '|' with limit 3 and ',' for lists -- can a kingdom StringId or a comma-joined list entry contain those?)

4. **Enrollment vs alignment coherence.** MomentumEnrollmentService enrolls every kingdom whose IAlignmentService.GetKingdomSide is Free/Evil. Cross-check alignment.json against the enrolled-set assumptions: are shaghana/abanissa/umbar (neutral) correctly excluded? Is there any kingdom in the live game (taom_spkingdoms) NOT in alignment.json that would resolve Neutral and silently never enroll -- is that intended? Does the player's own kingdom (custom StringId, not in alignment.json) resolve Neutral and thus never enroll, making the whole feature invisible to a player who founds their own kingdom?

5. **Master-toggle / meter lifecycle.** The RCA claims the MomentumEnabled retract-meter bug was fixed by running RefreshMapMeter before the enabled-guard in OnDailyTick and folding MomentumEnabled into IsIndicatorVisible. Verify the fix is complete: are there OTHER entry points (OnSessionLaunched, OnGameLoadFinished, OnMapEventEnded) where the meter can be ADDED while MomentumEnabled is false? Trace every AddMapMeter call site.

6. **Player-kingdom side detection.** PlayerMomentumService.IsPlayerOnStrongerSide uses IPlayerContextAdapter.GetPlayerKingdomId then checks membership in state.Free/state.Evil. If the player is a mercenary or has no kingdom, GetPlayerKingdomId returns "" -- handled? If the player's kingdom is enrolled but GetPlayerKingdomId returns a stale id mid-transition, any risk?

## Files (read all)
Backend: Main/Features/WarOfTheRingMomentum/*.cs, Domain/*.cs, Snapshots/*.cs, Models/*.cs
UI: Main/Features/WarOfTheRingMomentum/UI/*.cs
Adapters: Main/Adapters/WarEventSnapshotAdapter.cs, Main/Adapters/KingdomStrengthAdapter.cs
Diplomacy integration: Main/Features/Diplomacy/Models/WarPhase.cs, WarOutcome.cs, IWarOfTheRingService.cs, WarOfTheRingService.cs, WarOfTheRingBehavior.cs, and the 3 peace-block layers (Models/TaomDiplomacyModel.cs, Models/TaomKingdomDecisionPermissionModel.cs, Hooks/MakePeaceAction_ApplyInternal_Patch.cs + PeaceActionHook.cs)
Prefabs: Main/_Module/GUI/PreFabs/MomentumView/MomentumView.xml, MomentumMapIndicator.xml, Relationship.xml
Tests: TAOM.Tests/Features/WarOfTheRingMomentum/*.cs, TAOM.Tests/Features/Diplomacy/WarOfTheRingServiceTests.cs
Wiring: Main/IoC.cs, Main/SubModule.cs (grep WarOfTheRingMomentum)

## REQUIRED SECTIONS

### VANILLA CODE
For any finding touching a CampaignEvent signature, MakePeaceAction, MBObjectManager.GetObject, MapEvent/MapEventSide, or GauntletLayer/MapView -- paste the relevant decompiled 1.4.6 signature you relied on (from E:\Decompiled_Bannerlord\ or note you could not verify). We already verified the CampaignEvents delegate signatures and Kingdom.CurrentTotalStrength/MapEventSide.TroopCasualties -- focus elsewhere.

### DEEP ANALYSIS
Trace one full campaign scenario end-to-end and report any state that desyncs: new game -> day 14 FullWar -> enrollment sweep -> 3 Evil sieges (player uninvolved) -> save -> reload -> 2 player-involved battles -> Evil hits threshold -> victory inquiry -> peace-out -> reload post-victory. At which step, if any, does momentum/phase/meter/diplomacy diverge from intent?

### CONFIG CROSS-REFERENCE
Cross-check every kingdom id the code special-cases (empire_w/empire_s leader banners in MomentumPopupVM) and every id in alignment.json against the cheatsheet. Flag any typo or any id that no longer exists in taom_spkingdoms.

### FINDINGS OR OBSERVATIONS
List each finding with severity (HIGH/MED/LOW), file:line, the failure scenario (concrete inputs -> wrong output), and a suggested fix. If you find nothing beyond the already-fixed set, say so explicitly per Known Suspect.

## QUALITY GATES
- Do not flag vanilla-matching code as a bug.
- Do not assume empire=Rohan (it is Dunland); do not invent ids.
- Verify "missing X" claims by grepping before reporting.
- Distinguish donor-parity intentional deviations (documented in code comments + the RCA) from real bugs.

## Prior review lessons
SUCCESSES: config ID cross-ref caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland); Codex flagged vanilla-matching code as bugs; Codex skipped hard sections.

Output your review below.
