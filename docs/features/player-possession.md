# Player Possession (multiplayer join reconciliation)

## Overview

Detects the moment a multiplayer session replaces the hero the player created with a host-authored
one, and re-applies TAOM's character-creation grants — race, culture starting gold, career pick and
special-resource seed — to the hero the player actually ends up controlling. Inert in single-player.

## Why This Exists

- **Vanilla behavior:** irrelevant — vanilla has no campaign co-op. This is about how third-party
  co-op mods hand a joining player their character.
- **What every co-op base does:** character creation runs locally and produces a real `Hero`. At the
  join hand-off the joining client's campaign is replaced wholesale with the host's world, and the
  player is given a hero the HOST authored. The locally created hero is discarded.
- **TAOM requirement:** TAOM's character creation grants a lot to `Hero.MainHero` at finalize —
  `SetPlayerRace`, `AssignCareer`, `GrantPlayerStartupGold`, the special-resource seed. Every one of
  those ran against the hero that is about to be thrown away.
- **Without this feature:** a joiner spawns as the wrong race (human on a TAOM-less headless host,
  the HOST's race on Bannerlord Together), with no career, and with the native starting gold instead
  of their culture's. Field-confirmed 2026-08-03: a Mirkwood player received 1000 instead of
  1000+4000, and the +4000 grant is visible in their client log immediately before the hero it
  applied to ceased to exist.

## Architecture

### Design Challenge

Three constraints shaped this:

1. **No co-op dependency is acceptable.** TAOM must behave identically with no co-op mod, with
   BannerlordCoop, with Bannerlord Together, and with a co-op mod that does not exist yet. That rules
   out hooking any co-op assembly's join message.
2. **The data must outlive its campaign.** The choices are recorded in the character-creation
   campaign and consumed in the campaign that REPLACES it. Anything campaign-scoped is disposed in
   between.
3. **`Hero.MainHero` changing is not unique to joining.** It also changes in ordinary single-player
   when the player dies and continues as an heir. A naive "MainHero changed" detector hands every
   heir a fresh starting package.

### Solution Approach

**Detection is pure engine state.** Record `Hero.MainHero.StringId` at session launch / game load;
when it later differs, the hand-off has happened. No co-op assembly is referenced, which is what
makes it base-agnostic. (This seam was proven in the field first — the reporters' own compatibility
module used the same MainHero-id comparison successfully across three configurations.)

**Lifetime is solved with a process-lifetime singleton**, not statics: DryIoc singletons live for the
process because `IoC.Configure()` runs once in `OnSubModuleLoad`, so `IPlayerPossessionService`
survives the campaign switch while staying injectable and unit-testable.

**The heir problem is solved by three independent guards**, any one of which is sufficient:

| Guard | Effect |
|---|---|
| `ICoopPresenceProvider.IsCoopActive` | Solo play never reaches the re-grant at all |
| Single consumption | A co-op player's heir, inheriting hours after the join, finds the choices already consumed |
| `SyncData` marker per hero id | A reconnect cannot be handed a second package |

### Component Diagram

```
OnCharacterCreationIsOver          OnSessionLaunched / OnGameLoaded
        |                                       |
        v                                       v
PlayerPossessionBehavior  ------------->  RecordBaselineHero(id)
  (boundary: sealed Hero -> ids)
        |
        | HourlyTickEvent
        v
IPlayerPossessionService.TryConsumePossession(currentHeroId)
        |  true, at most once
        v
IJoinReconciliationService.ReapplyCharacterCreationPackage
        |
        +--> IHeroRosterAdapter.SetHeroRace           (race)
        +--> IPlayerStartupGoldService                (culture gold)
        +--> ICareerCreationHandler.OnCareerSelected  (career)
        +--> ISpecialResourceService.InitializeHero   (resource seed)
```

Nothing in the reconciliation path is new logic — every grant already existed and already took a
hero id. This feature only re-invokes them against the correct hero.

## Key Decisions

**The CC-chosen culture drives the grants, not the live hero's culture.** The player picked Mirkwood
and earned Mirkwood's package; arriving under the host's culture is the bug, not a new source of
truth. The hero's LIVE kingdom is still used for the special-resource seed, because only the host
knows which kingdom the joining hero landed in.

**Each grant is independently guarded.** A joiner losing their career because the gold grant threw
would be a worse outcome than the bug this fixes.

**`ResetForNewCampaign` deliberately does NOT clear the choices.** Character creation completes and
raises its event BEFORE the joining client's campaign is replaced, so clearing on new-campaign would
discard exactly the data being carried across that boundary. It clears only the baseline.

**Race is validated before it is set.** `GetRaceNameFromId` coerces unknown ids to `"human"`, so a
race id from a module set this client lacks would be written as a valid-looking race and cached for
the session (`.claude/rules/csharp-architecture.md`, validate-before-lookup).

## Files

| Path | Role |
|---|---|
| `Main/Features/PlayerPossession/PlayerCharacterCreationChoices.cs` | Immutable, campaign-free record of the picks |
| `Main/Features/PlayerPossession/IPlayerPossessionService.cs` / `PlayerPossessionService.cs` | Baseline tracking, detection, single-consumption |
| `Main/Features/PlayerPossession/IJoinReconciliationService.cs` / `JoinReconciliationService.cs` | Re-invokes the existing grant paths |
| `Main/Features/PlayerPossession/PlayerPossessionBehavior.cs` | Boundary + `SyncData` marker list |
| `Main/Features/PlayerPossession/PlayerPossessionIoC.cs` | Singleton registrations (required, not stylistic) |
| `TAOM.Tests/Features/PlayerPossession/` | 23 tests, including explicit single-player inertness |

## Testing

Unit-tested end to end at the service layer. The tests that matter most are the negative ones:

- `TryConsumePossession_NoCoopModule_NeverFires` — the heir-succession guard
- `TryConsumePossession_CoopActiveButHeirSucceedsAfterJoin_DoesNotReFire`
- `TryConsumePossession_CalledRepeatedly_ReturnsTrueOnlyOnce`
- `Reapply_OneGrantThrows_TheOthersStillApply`
- `ResetForNewCampaign_KeepsCharacterCreationChoices`

**Not unit-testable (requires a live multiplayer session):** the hand-off itself. Verifying this
end to end needs a two-client co-op session where a joiner creates a non-human character of a culture
with a non-zero startup-gold grant, and confirms race, gold, career and resource seed after joining.

## Related

- Field report: `C:\Users\mikew\Downloads\TAOM-Report-Bundle-2026-08-03` §1 and §7
- [coop-interop.md](coop-interop.md) — the presence/authority signals this builds on
- [hero-race.md](hero-race.md) — `RacePersistenceService`, whose degenerate-legend guard fixes the
  other half of the race loss
- [startup-resources.md](startup-resources.md) — the gold grant being re-invoked
