# Refuge

Raise a ready field or fortified camp into a persistent, garrisonable player base (#507): store
troops, prisoners and goods, assign a warden, upgrade to a stronghold, defend it with a damage
bonus and a rallying militia. Port of the commissioned yotthani Refuge module (Bannerlord 1.4.5,
behaviourally ported 2026-08-22); provenance:
[provenance-register.md](../reference/provenance-register.md) "Yotthani commissioned modules".

## Architecture

```
taom_fc_camp menu, index 4: "Establish a refuge here"
   -> IWardenService.Candidates (companions, then promotable soldiers) -> picker
   -> IRefugeService.Found: spawn RefugePartyComponent party -> warden -> charge -> break camp -> raise
refuge book (SyncData "_taomRefuges") -- RefugeData: tier, warden, build timestamps,
   PERSISTED militia bookkeeping (MilitiaAdded/MilitiaTroopId/MilitiaPreRallyCount)
manage: taom_refuge_menu (vanilla party screen for garrison+prisoners, stash screen for goods)
defence: IRefugeDefenseService <- TaomCombatMechanicsModel.ApplyDamageReductions (real-time)
                               <- TaomCombatSimulationModel.SimulateHit (auto-resolve)
overlay/menus: RefugeCampContributor : ICampOverlayContributor (FieldCamp's seam; the source
   assigned three mutable static delegates instead)
clan screen + click-to-manage: Patch75 (registry entry has the co-op disposition)
```

## Key decisions

- **Nobody is killed.** The source's dismantle ran `KillCharacterAction.ApplyByRemove` on a
  promoted warden and refunded the troop. Here a promoted warden stays a clan companion (the
  soldier became somebody); companion and promoted warden alike rejoin the party via
  `AddHeroToPartyAction`; a captured warden is left where fate put him and the dismantle
  proceeds. Contract-pinned in `IWardenService`. The ONE surviving ApplyByRemove is
  `UnwindPromotion`: a founding that failed after the promotion (engine spawn refusal) rolls the
  never-attached minted hero back and refunds the soldier.
- **No hero row ever rides a raw roster copy.** Dismantle moves every hero member with
  `AddHeroToPartyAction` and every hero prisoner with `TransferPrisonerAction` BEFORE the bulk
  troop/item merge: `TroopRoster.Add` + `Clear` fires the engine's `OnHeroRemoved`, which nulls
  `Hero.PartyBelongedTo` unconditionally (1.4.8 `Hero.cs:2165`), and the desync persists into the
  save. Ordering pinned by `Dismantle_MovesEveryHeroAndHeroPrisonerByActionBeforeTheBulkCopy`.
- **Militia bookkeeping is persisted, delta-based, and baseline-aware.** The source's transient
  dict baked militia into the garrison on a mid-battle save, and stand-down deleted every troop of
  that type including player-garrisoned ones. `MilitiaPreRallyCount` (SaveableField 115) records
  the garrison's own count of the militia troop before the rally; stand-down removes
  min(recorded, present - baseline), attributing casualties to militia first. The raid path
  rallies BEFORE `StartBattleAction` because `MapEventParty`'s constructor freezes
  `NumberOfHealthyMembers` for auto-resolve; for battles other parties start, the
  `MapEventStarted` rally reaches player-fought missions but not a frozen auto-resolve count (an
  engine ordering limit).
- **A refuge in a map event is untouchable.** Manage/enter/dismantle all gate on the party not
  being in a `MapEvent` (`NearestManageable`/`NearestDismantlable` + the encounter prefix), so the
  player cannot mutate a live battle participant's rosters or destroy it mid-fight.
- **The engine's destroy is observed.** A lost defense destroys the party from
  `MapEventSide.HandleMapEventEnd` AFTER `OnMapEventEnded` dispatched; the behavior listens to
  `CampaignEvents.MobilePartyDestroyed` and drops the row, cap slot and visuals immediately with a
  player message (the warden stays a clan companion and is logged, never silently orphaned).
- **Session reset.** `RefugeCampaignBehavior` marks the session synced only when SyncData runs in
  loading mode; `OnSessionLaunched` calls `RefugeService.ResetForNewSession()` when it did not
  (fresh campaign, or a pre-feature save), so the process-lifetime singleton cannot leak campaign
  A's book into campaign B. `LoadFrom` also scrubs null rows and clears every transient.
- **Orphans get an exit.** A party adopted on load without a book row (`Established=false`,
  `Building=false`) is dismantlable (menu + encounter open for it, everything else greyed), so it
  cannot eat a refuge-cap slot forever.
- **Peace releases refuge prisoners.** Vanilla's `PrisonerReleaseCampaignBehavior` enumerates
  caravans, war parties, villages and garrisons only; the behavior listens to
  `CampaignEvents.MakePeace` and runs `EndCaptivityAction.ApplyByPeace` for eligible hero
  prisoners held in refuges.
- **Combat patches deleted.** Both source targets are TAOM-owned model slots; the reduction rides
  the model chain, shared between real-time and auto-resolve so the two cannot drift.
- **An upgrading refuge is not ready** (defence, militia, raids, manage all drop until the rebuild
  finishes). Deliberate difference from the source, pinned by tests.
- **AI re-pinned on load** (`SetMoveModeHold` + `SetDoNotMakeNewDecisions`): the source pinned only
  at spawn, so refuges could wander after a reload.
- **Raids ship OFF** (experimental in the source, same default here).

## Configuration

MCM group **Refuge** (GroupOrder 47), 14 settings, all validated in `RefugeSettingsProvider`, all
coop simulation-relevant: master toggle, found/upgrade costs (2000/5000), build hours (6), hard cap
(3, live limit min(1 + clanTier/2, cap)), manage range (4), town distances (16/26), defence bonuses
(0.20/0.35), militia base/max (6/40), raids toggle + range (off/6). Toggle off stops founding and
menus; standing refuges persist and can be entered and dismantled.

The source's two remaining knobs, `RefugeBuildingMesh` ("empire_street_tent_02") and
`RefugeBuildingScale` (0.4), exist on `IRefugeSettingsProvider` but are pinned to the source
defaults in the provider: `TaomSettings.cs` is single-owner and carries no MCM rows for them yet.
Wiring MCM later needs no change on the consuming side (`RefugeVisualService`).

## Food

The refuge party is a REAL party to the engine's food model: a custom `PartyComponent` is none of
the exempt vanilla kinds (`DoesPartyConsumeFood` excludes only IsGarrison/IsCaravan/IsBandit/
IsMilitia/IsPatrolParty), and the player-clan warden satisfies its leader clause, so **the
garrison eats daily from the refuge's stash (`ItemRoster`) and starves (regulars take -25%/day
attrition) when it runs dry**. Deliberately kept and surfaced rather than exempted: the stash IS
the larder, said in the refuge menu status ({=taom_rf_food_note}), and an upkeep-free standing
garrison would be strictly better than vanilla's garrisons. Exempting via
`TaomFoodConsumptionModel.DoesPartyConsumeFood` remains a one-line change if playtesting
disagrees.

## Key files

| File | Purpose |
|---|---|
| `Domain/RefugeData.cs` + `RefugeSaveDefiner.cs` | Persisted record incl. militia bookkeeping; definer base 726901201 |
| `Components/RefugePartyComponent.cs` | The persisted party identity (warden as Leader) |
| `RefugeService.cs` | Lifecycle state machine; campaign statics behind protected virtuals |
| `WardenService.cs` | Candidates, promotion, the no-kill release policy |
| `RefugeDefenseService.cs` (+ `IRefugeBook`) | Hot-path tier factors; one singleton serves both faces |
| `Visuals/RefugeVisualService.cs` | refuge tpacs (scale 4/4.8, palisade 4.6/5.4) over CampLayoutBuilder, vanilla fallback |
| `Hooks/RefugeCampaignBehavior.cs` + `RefugeMenuController.cs` | Events, SyncData, menus, index-4 insertions |
| `Hooks/RefugeCampContributor.cs` | Overlay caption/blocked-reason/status through FieldCamp's seam |
| `Hooks/RefugeClanScreenPatch.cs` + `RefugeEncounterPatch.cs` | Patch75 |

## Traps

- `RefugeService` is registered as BOTH `IRefugeService` and `IRefugeBook` from ONE singleton;
  registering them separately gives the combat hot path an empty second book.
- `Found` returning null with reason `None` means an engine spawn refusal (the pinned enum has no
  EngineFailure value); the menu unwinds a promotion and shows the block reason.
- Militia rally is skipped while `MilitiaAdded != 0`, so a refuge cannot double-boost across
  stacked map events (and the raid path's pre-battle rally is not doubled by MapEventStarted).
- `RefugePartyComponent.AvoidHostileActions` is NOT a combat-prevention gate: 1.4.8 consults it at
  exactly two sites (PlayerEncounter narrative text, ApplyEncounterHostileAction's
  relation-penalty gate). Hostile AI can and will attack a refuge; the refuge's own passivity
  comes from the pinned AI, its defense from the model-chain damage reduction.
- `RefugePartyComponent` overrides `OnChangePartyLeader` to keep `_warden` in sync; without it a
  dead or dismissed warden stays cached as Leader while vanilla disband flows move the party.

## Known limitations

- Militia added from `MapEventStarted` (a battle the raid path did not start) reaches player-fought
  missions but not auto-resolve's frozen participant count (`MapEventParty` captures
  `NumberOfHealthyMembers` at construction, before the event dispatches).
- A warden who dies or is captured while the refuge stands leaves the party leaderless until
  dismantle; there is no succession picker (the component nulls its cached leader; the refuge
  still garrisons and defends).

## Owed

- In-game smoke per #507 checklist (found/build/enter/store/upgrade/dismantle, defence bonus both
  paths, militia rally + stand-down, save/load with refuge under attack, warden capture path,
  refuge wiped by a hostile lord mid-session, orphan-row dismantle).
- 12-language translation run for the 51 `{=taom_rf_*}` keys.
