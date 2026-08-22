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
   PERSISTED militia bookkeeping (MilitiaAdded/MilitiaTroopId)
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
  soldier became somebody); a companion warden rejoins the party; a captured warden is left where
  fate put him and the dismantle proceeds. Contract-pinned in `IWardenService`.
- **Militia bookkeeping is persisted and delta-based.** The source's transient dict baked militia
  into the garrison on a mid-battle save, and stand-down deleted every troop of that type
  including player-garrisoned ones. Stand-down now removes min(recorded, present) of the recorded
  troop only.
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
  EngineFailure value); the menu treats it as a silent no-op with a log line.
- Militia rally is skipped while `MilitiaAdded != 0`, so a refuge cannot double-boost across
  stacked map events.

## Owed

- In-game smoke per #507 checklist (found/build/enter/store/upgrade/dismantle, defence bonus both
  paths, militia rally + stand-down, save/load with refuge under attack, warden capture path).
- 12-language translation run for the 48 `{=taom_rf_*}` keys.
