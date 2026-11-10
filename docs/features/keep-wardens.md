# Keep Wardens

## Overview

A castle with a built Guard House fields one patrol party of its owner's culture at a time, the way a town already does. The patrol grows stronger with the Guard House level (levels 1, 2 and 3 use the culture's weak, moderate and strong patrol template) and its size limit is 25, 40 or 56 before the party size feats. A lost patrol is replaced after 10, 8 or 6 days. Every town and castle land patrol spawns, and refills after a fight, at its size limit, so a patrol is never left under the 90 percent that keeps it at home. A castle with a live patrol gains 1, 1.5 or 2 security a day, and the AI builds a Guard House ahead of other buildings. The Reeve's Ledger shows the Patrol row for a castle with a live patrol or one that may have a new patrol. A castle Guard House upgrade refreshes the patrol's limit at once. Since #888, extended by #889, #991 and #992. The MCM group is "Fief Management/Keep Wardens" (two switches).

## Why This Exists

- **Vanilla behavior:** `DefaultSettlementPatrolModel` allows patrol parties for towns only and looks only for `DefaultBuildingTypes.SettlementGuardHouse`. The castle Guard House (`building_castle_guard_house`) raises militia and the prisoner limit and nothing else. Vanilla's `GetPatrolPartySpawnDuration` also reads the town Guard House without a null check, so asking it about a castle throws. `DefaultPartySizeLimitModel.CalculatePatrolPartySizeLimit` reads only the town Guard House too, so a patrol whose home is a castle gets a size limit of 0. For a town the figures are `10 + 5 x level` men (15, 20, 25), `0.5 + 0.5 x level` security a day under the label "Patrolling Parties" (`str_patrol_parties`), and a spawn delay of `10 - (level - 1) x 2` days.
- **TAOM requirement:** castles are the frontier (Helm's Deep, Henneth Annun, Cirith Ungol). A castle that builds its Guard House should field riders or rangers.
- **Without this feature:** a castle can build its Guard House and still get no patrol, and an AI fief picks its Guard House by chance among every other building.
- **Roster against limit (#991):** vanilla fills each template stack to its maximum (`DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty`, called at spawn and by `PatrolPartiesCampaignBehavior.ReplenishParty`), and the patrol AI gives no score to a patrol sitting in its home settlement with `PartySizeRatio` below 0.9. TAOM's templates were authored without the limit in mind, so some patrols came out well under it (Arthedain, Cardolan and Rhudaur: 11, 14 and 14 men against 15, 20 and 25) and stayed home for ever once they returned.

## Architecture

### Design Challenge

- Vanilla's `PatrolPartiesCampaignBehavior` already queues, spawns and refills patrols for any settlement the campaign `SettlementPatrolModel` allows, so no patch and no patrol behavior is needed. The model is the whole seam, plus one small listener (below) for the castle Guard House upgrade.
- The castle branch has to answer before the base model, because vanilla's spawn-duration method dereferences a null Guard House for a castle, and its template method returns null for every castle.
- A patrol that spawns with a size limit of 0 would show "25/0" and never refill, so the party size model needs a second seam.

### Solution Approach

- `TaomSettlementPatrolModel : DefaultSettlementPatrolModel` (campaign slot `SettlementPatrolModel`, declared by `KeepWardensModule` through `GameModelDecl`, so `SubModule.cs` and `IoC.cs` are untouched). Each override is one expression: when the castle plan has an answer it gives it, otherwise the base model (`DefaultSettlementPatrolModel`) does. Towns and naval requests keep vanilla's answer.
- `CastlePatrolService` (pure) turns an engine-free `CastlePatrolSnapshot` into a plan. The template answers any castle patrol: a castle that is not naval, whose owner culture has the patrol template for the level clamped to 1 to 3, gets that strength whatever the switch, the level or the owner says. Vanilla's hourly refill (`HourlyTickParty`, then `ReplenishParty`) asks for the template of a live patrol without asking whether the settlement may have one, and the base model returns null for every castle, which would throw. Only a NEW patrol honours the switch, the owner and the level: `Allowed` needs the setting on, an owner clan that is not a rebel clan and a Guard House of level 1 or more. A castle owned by no clan has no culture and so no template.
- `ICastlePatrolAdapter` reads the settlement into the snapshot. It finds the castle Guard House in `Town.Buildings` by building type; it does not use `TownBuildingsAdapter.GetLevel`, which looks the settlement up on every call.
- `ICastlePatrolSizeHook` is the second seam. `TaomPartySizeModel.GetPartyMemberSizeLimit` calls it right after the base call and before the culture feats. For a patrol party whose home is a castle, when the base limit is 0, it replaces the number with the configured limit for the Guard House level, 25, 40 or 56 (a fresh `ExplainedNumber`, as vanilla's own town code returns). The hook ignores the MCM switch on purpose: a live patrol must never drop to a limit of 0. The model resolves the hook once in its constructor (`IoC.Resolve<ICastlePatrolSizeHook>()`), because a constructor parameter would need an edit to `SubModule.cs`. `CastlePatrolSizeHookTests` pins the source order.
- `IPatrolRosterHook` is the third seam (#991). `TaomPartySizeModel` overrides `FindAppropriateInitialRosterForMobileParty` as one expression, `_patrolRoster.FitToLimit(party, base.FindAppropriateInitialRosterForMobileParty(party, partyTemplate))`. Base runs first, so TAOM's Patch39 postfix and the co-op postfix on the base keep running on the roster they patch. The hook leaves any party that is not a land patrol alone (`IsPatrolParty`, and not `PatrolPartyComponent.IsNaval`: a naval patrol's template carries ships). For a land patrol it reads a fresh `GetPartyMemberSizeLimit(party.Party, party.IsCurrentlyAtSea)` from the model slot, not `PartyBase.PartySizeLimit`, which is cached on `MemberRoster.VersionNo`, and hands the stack counts to `PatrolRosterService.Fit`, which scales each stack by `limit / total` (rounded down) and then gives the remainder out one man at a time in stack order, so the sum equals the limit and the template's proportions hold. A limit of 0 or less, an empty roster or a roster already at the limit comes back unchanged. The hook rebuilds the roster with the same characters in the same order. It applies to every town and castle land patrol, the town ones to vanilla's own 15, 20 and 25 (so a Dale level 2 patrol goes from 36 men to 20, and Arthedain level 1 from 11 to 15). No template data was edited. The model resolves the hook once in its constructor, like the size hook. `PatrolRosterHookTests` pins the base-first expression, the constructor resolve, the patrol and not-naval guard and the fresh limit.
- `TaomSettlementSecurityModel : DefaultSettlementSecurityModel` (#889, slot `SettlementSecurityModel`). `CalculateSecurityChange` calls base first and then adds `CastlePatrolService.SecurityPerDay` under vanilla's own "Patrolling Parties" text (`str_patrol_parties`). Vanilla already adds `0.5 + 0.5 x level` for a town with a live patrol and a built town Guard House and nothing for a castle; the service gives `securityPerDay` (1, 1.5, 2) for a castle with a live patrol (`Settlement.PatrolParty != null`) and a built castle Guard House of level 1 or more (levels above 3 count as 3), and 0 for a town, so vanilla's line is never doubled. `ExplainedNumber.Add(0)` is a no-op, so the override has no branch. There is no switch: the bonus exists only while a castle patrol does, and turning Castle Patrols off removes the patrols.
- `TaomBuildingScoreModel : DefaultBuildingScoreCalculationModel` (#889, slot `BuildingScoreCalculationModel`). Only `GetNextBuilding` is overridden: `service.PicksGuardHouse(adapter.Read(town)) ? adapter.GuardHouse(town) : base.GetNextBuilding(town)`. `IGuardHouseAdapter.GuardHouse` finds `SettlementGuardHouse` (town) or `CastleGuardHouse` (castle) in `Town.Buildings`, and `Read` marks it eligible with the engine's own filter (not a daily project, level below 3, not in `BuildingsInProgress`) and draws one `MBRandom.RandomFloat` roll, so the service stays pure. The service picks the Guard House when `EnableGuardHouseBuildPriority` is on, it is eligible and it is either unbuilt (always) or the roll is under `guardHousePriorityChance` (0.6). Any other call falls to the base random pick, and the daily project pick (`GetNextDailyBuilding`) stays vanilla's. The engine asks in two places (`BuildingsCampaignBehavior`): the daily tick, only for a fief whose owner clan is not the player's clan, with a 10 percent chance when its queue is empty; and the new-game pass `BuildDevelopmentsAtGameStart`, which asks for every fortification with no owner check, so at campaign start every fief queues its first build once. After that the player's own build choices are untouched.
- The Reeve's Ledger Patrol row (#992) is gated by `ReevesLedgerService.ShowsPatrolRow`, which shows it for every town, and for a castle with a live patrol (`Settlement.PatrolParty != null`) or one for which `SettlementPatrolModel.CanSettlementHavePatrolParties` answers yes (Castle Patrols on, an owner clan that is not a rebel clan, a built Guard House and the owner culture's template). A castle with a built Guard House but the switch off and no live patrol gets no row; see [reeves-ledger.md](reeves-ledger.md).
- `CastleGuardHouseUpgradeBehavior` (a `CampaignBehaviorBase`, declared by `KeepWardensModule`) is the castle twin of a vanilla refresh. `PatrolPartiesCampaignBehavior.OnBuildingLevelChanged` (v1.5.5 `:55-68`) calls `Settlement.PatrolParty.Party.MemberRoster.UpdateVersion()` only for `SettlementGuardHouse`, and `PartyBase.PartySizeLimit` is cached on that version. The listener on `CampaignEvents.OnBuildingLevelChangedEvent` does the same for `CastleGuardHouse` (`PatrolParty?.Party.MemberRoster.UpdateVersion()`), so the limit and the N/M readout refresh at the upgrade, and a full patrol refills to the new size at home through the roster hook. It has no settings gate (the bump only invalidates a cache), saves nothing, and logs one error if it throws. It does not copy vanilla's first-patrol queue call (`UpdateSettlementQueue` is private), so a castle that builds its first Guard House still waits for the next spawn check.
- No new templates: every culture that owns a town or castle already points at patrol templates (`CulturePartyTemplateTests`), and the cultures without templates are bandit cultures that own no town or castle (hideout-only cultures own hideouts).

### Component Diagram

```
castle_patrols.json
        |
CastlePatrolConfigProvider   TaomSettings.EnableCastlePatrols
        |                          |
        +-------- CastlePatrolService --------+
                       |                      |
      TaomSettlementPatrolModel        CastlePatrolSizeHook
      (SettlementPatrolModel slot)     (TaomPartySizeModel, after base)
      TaomSettlementSecurityModel               |
      (SettlementSecurityModel slot)   PatrolRosterHook + PatrolRosterService
      TaomBuildingScoreModel           (TaomPartySizeModel, roster, base first)
      (BuildingScoreCalculationModel)
      CastleGuardHouseUpgradeBehavior (OnBuildingLevelChangedEvent, roster version bump)
                       |                      |
        ICastlePatrolAdapter, IGuardHouseAdapter (Settlement, Town, MobileParty to snapshots)
```

## Configuration

### Config File: `Main/_Module/ModuleData/configs/castle_patrols.json`

| Field | Type | Description |
|-------|------|-------------|
| `spawnDays` | three numbers | Days between spawns at Guard House level 1, 2 and 3. Finite, above 0, at most 60. |
| `sizeLimits` | three whole numbers | The castle patrol's size limit at level 1, 2 and 3. 1 to 500. Towns keep vanilla's 15, 20 and 25. |
| `securityPerDay` | three numbers | Daily security for a castle with a live patrol at level 1, 2 and 3. Finite, 0 to 10. |
| `guardHousePriorityChance` | one number | The chance the AI picks an upgradable Guard House ahead of a random building. Finite, 0 to 1. |

Each field reverts to its default on its own with one summary warning (a missing field, a wrong length, NaN, infinity, out of range, a fractional size). A missing or malformed file reverts all four. The provider is a singleton, so an edit needs a full game restart.

### Current Values

| Level | Template | Spawn days | Castle size limit | Security per day |
|---|---|---|---|---|
| 1 | weak | 10 | 25 | 1 |
| 2 | moderate | 8 | 40 | 1.5 |
| 3 | strong | 6 | 56 | 2 |

The spawn days are vanilla's own town numbers (`10 - (level - 1) * 2`). A town patrol keeps vanilla's `10 + 5 * level` troops (15, 20, 25); a castle patrol is larger (#889), since it guards a frontier hold with no garrison town beside it. Security is vanilla's town formula (`0.5 + 0.5 x level`). The size limits are before the culture and feat factors that `TaomPartySizeModel` applies afterwards, and the roster is fitted to the limit after them. The party size feats are factors of at most +10 percent (Gondor's is +2.5 percent), and the result is truncated to a whole number, so Gondor's castle patrols come to 25, 41 and 57. The build chance is 0.6 once the Guard House is built; an unbuilt one is always picked.

### MCM

Group "Fief Management/Keep Wardens" (GroupOrder 4), two switches, both simulation-relevant and counted in the co-op settings fingerprint:

- `EnableCastlePatrols` (Order 0), default on. Label key `taom_keep_wardens_label`, hint key `taom_keep_wardens_hint`.
- `EnableGuardHouseBuildPriority` (Order 1, "Guard House Build Priority"), default on. Label key `taom_keep_wardens_build_label`, hint key `taom_keep_wardens_build_hint`. English in all 12 languages until the paid translation run.

The settings provider reads both through one cached accessor (`CampaignHotPathSettingsProvidersTests`).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/KeepWardens/KeepWardensModule.cs` | Module: the model and behavior declarations and the registrations |
| `Main/Features/KeepWardens/CastleGuardHouseUpgradeBehavior.cs` | Castle Guard House upgrade: bumps the patrol roster version |
| `Main/Features/KeepWardens/Models/TaomSettlementPatrolModel.cs` | The three overrides, castle branch first |
| `Main/Features/KeepWardens/Models/TaomSettlementSecurityModel.cs` | `CalculateSecurityChange`: base, then the castle patrol line |
| `Main/Features/KeepWardens/Models/TaomBuildingScoreModel.cs` | `GetNextBuilding`: the Guard House pick, else base |
| `Main/Features/KeepWardens/PatrolRosterService.cs`, `PatrolRosterHook.cs`, `IPatrolRosterHook.cs` | The roster fit and its `TaomPartySizeModel` seam |
| `Main/Features/KeepWardens/CastlePatrolService.cs` | Plan and size-limit decisions |
| `Main/Features/KeepWardens/CastlePatrolConfigProvider.cs` | Validating loader for the config |
| `Main/Features/KeepWardens/CastlePatrolSettingsProvider.cs` | MCM switch, cached accessor |
| `Main/Features/KeepWardens/CastlePatrolSizeHook.cs` | The `TaomPartySizeModel` seam |
| `Main/Adapters/CastlePatrolAdapter.cs` | Settlement and party to snapshot (with the live patrol), template lookup |
| `Main/Adapters/GuardHouseAdapter.cs`, `IGuardHouseAdapter.cs`, `GuardHouseCandidate.cs` | The Guard House in a town or castle, vanilla's eligibility filter, the roll |
| `Main/_Module/ModuleData/configs/castle_patrols.json` | Spawn days, size limits, security and the build chance |

## Dependencies

- `ICastlePatrolAdapter` (Adapters): the settlement, its owner culture's patrol templates and the castle Guard House.
- `TaomPartySizeModel` (CulturalFeats): carries the two hook lines, the size hook and the roster hook.
- `CampaignEvents.OnBuildingLevelChangedEvent`, `DefaultBuildingTypes.CastleGuardHouse` and `TroopRoster.UpdateVersion` (engine; `CastleGuardHouseUpgradeBehaviorTests` pins the bindings).
- `ReevesLedgerService` and `FiefLedgerAdapter` (Reeve's Ledger): the castle Patrol row reads `Settlement.PatrolParty` and the patrol model.

## Tests

- `TAOM.Tests/Features/KeepWardens/CastlePatrolServiceTests.cs`: 24 tests, the plan matrix (town, castle levels 0 to 3 and above, rebel, setting off, naval, missing template, clamp; a refused castle keeps its template strength) and the size override, NaN included.
- `CastlePatrolConfigProviderTests.cs`: 57 tests, one per validation rule for all four fields (a float-overflow value, a wrong length that reverts only its own field), values beyond the long range, and a path service that throws. `ShippedCastlePatrolConfigTests.cs`: 2 tests on the shipped file.
- `CastlePatrolSizeHookTests.cs`: 7 tests, the boundary (ignores the setting), the source-order pin in `TaomPartySizeModel` and the one constructor resolution.
- `KeepWardensWiringTests.cs`: 11 tests, module listed once, three campaign models of the right slots and bases, exactly one behavior and no patch, not parked, no save data, no `if` in any override body, castle branch before base, the template named by strength, the security model calling base first with vanilla's label, the build model deciding with the service and falling back to `base.GetNextBuilding`, and the container validating.
- `CastlePatrolAdapterTests.cs`: 4 tests, the shared not-a-castle-patrol constant, a source pin on the template mapping and the castle Guard House, and a pin that `PatrolAlive` is read from `Settlement.PatrolParty`.
- `CastlePatrolSettingsProviderTests.cs`: 4 tests, plus the rows in `CampaignHotPathSettingsProvidersTests.cs`.
- `PatrolRosterServiceTests.cs`: 17 tests, the Arthedain 6/3/2 case to 15, 25 and 56, scaling down, no-ops (the same instance comes back), a single stack, a zero stack beside two others, and sum equal to the limit with no negative count for every limit from 1 to 120.
- `PatrolRosterHookTests.cs`: 5 tests, source pins for the base-first expression, the constructor resolve, the land-patrol guard and the fresh limit, and the container.
- `CastlePatrolSecurityAndBuildTests.cs`: 15 tests, security per level, no patrol, no Guard House, a town; the Guard House pick (unbuilt, the roll against the chance, ineligible, setting off).
- `GuardHouseAdapterTests.cs`: 4 tests, source pins for the building type per settlement kind, vanilla's eligibility filter, the single roll and the single `GuardHouse` lookup.
- `CastleGuardHouseUpgradeBehaviorTests.cs`: 4 tests, source pins for the listener, the castle-only building check before the null-safe version bump, the guard and the lack of a gate or save data, and an engine binding check (`BindingVerification`).
- `CulturePartyTemplateTests.cs`: one more test, every level 1 to 3 patrol template (69 templates, 23 patrol families in `taom_partyTemplates.xml`; the test only asserts a floor of 54) has a stack with a positive maximum.
- `ShippedCastleOwnerCultureTests.cs`: 1 test (`LiveInstall`), no castle owner clan has a hideout-only or settlementless culture. Inconclusive without the install.

## Co-op

Models and one small listener, nothing to gate. BannerlordCoop already runs patrol spawning on the host only; the clients receive the parties it creates. The roster hook runs after the base call, so a co-op postfix on `FindAppropriateInitialRosterForMobileParty` still sees the base roster it patches, and the roster is fitted on the host that spawns the party. The security and build-pick models change simulation on the host. Both settings are simulation-relevant and part of the fingerprint. The TAOM co-op docs list no patch on settlement security or on the AI's building choice. The local BannerlordCoop checkout (`GameInterface/Services/Settlements/Patches/CalculateSecurityChangePatch.cs`) prefixes `DefaultSettlementSecurityModel.CalculateInfestedHideoutEffectsOnSecurity` (host only) and `CalculateIssueEffectsOnSecurity` (never runs), not `CalculateSecurityChange`, so those prefixes still apply to the part our override takes from base; its town handlers send `Town.Security` changes from the host to the clients. Whether a client also runs our castle line on its own is UNVERIFIED, and so is BannerlordTogether's handling of `GetNextBuilding`, `CalculateSecurityChange` and the roster version bump.

## Save Compatibility

No save data. The patrols are vanilla `PatrolPartyComponent` parties, saved by the engine. Turning the feature off removes no building. Parking Keep Wardens removes the models and the behavior but not the two hook lines in `TaomPartySizeModel` (the size hook and the roster hook), and with castle patrols alive that reopens the null-template crash on the hourly refill; parking it also means removing both hook lines from `TaomPartySizeModel`.

## How to

**Change the spawn days or the size limits.**

1. Edit `Main/_Module/ModuleData/configs/castle_patrols.json`: `spawnDays`, `sizeLimits` and `securityPerDay` each hold three values, for Guard House level 1, 2 and 3; `guardHousePriorityChance` is one number from 0 to 1.
2. Restart the game, because the provider loads once per process. A value that fails validation reverts that field to its default with one warning in the log.
3. No code change.

**Turn the AI Guard House priority off.** In MCM, "Fief Management/Keep Wardens", untick "Guard House Build Priority". The AI goes back to vanilla's random pick at the next build decision; Guard Houses already queued or built stay.

**Turn castle patrols off.** In MCM, "Fief Management/Keep Wardens", untick "Castle Patrols". No new castle patrol is queued, and vanilla's daily check (`DailyTickSettlement`, then `UpdateSettlementParties`) removes the castle patrols already out.

## Loaded saves

There is no save data and no migration. On a save made before this version, no castle patrol exists yet: at the next daily check (`DailyTickSettlement`) each castle with a built Guard House queues a spawn `spawnDays` away, and its patrol appears at the first daily check after that. Existing town patrols keep the roster they were saved with until their next refill at home (`ReplenishParty`), which fits it to the limit; a town's limit refreshes at once when its Guard House changes level, and a castle's now does too. The Guard House priority starts at the next AI build decision (a daily 10 percent chance per AI fief with an empty queue), and the castle security line is added from the next daily tick for castles whose patrol is alive.

## Performance

In the live `TAOM_Map` settlements there are 156 castles (`is_castle="true"`) and 243 fortifications in all, 87 of them towns. 28 castles start with a Guard House at level 1 and 128 at level 0, so a new game gains 28 patrol parties, and at most 156 if every castle builds one (about 1 to 8 percent of the 2,000 to 2,500 parties on the map). The 128 unbuilt ones are picked first by the AI build priority, so the count grows over a campaign. The model reads a castle's buildings only for a castle. The size hook costs two property reads for a party that is not a patrol, and the roster hook one property read for any party created or refilled that is not a land patrol; a patrol spawn or refill adds one limit calculation and a short loop over its stacks. The security model reads a castle's snapshot once a day per fief and returns at the first check for a town. The build model scans one fief's short building list when an AI fief picks its next building (rare: a daily 10 percent chance for an AI fief whose queue is empty, and the new-game pass).

## Known Limits

- Vanilla queues castle spawns on its daily queue and its `OnBuildingLevelChanged` reacts only to the town Guard House, so a castle's first patrol waits for the queue (about 10 days at level 1). TAOM's own upgrade listener (`CastleGuardHouseUpgradeBehavior`) does not copy that queue call.
- Turning the setting off lets vanilla remove the live castle patrols at the next daily tick. Keeping them would need reflection into private behavior methods, which this feature does not use.
- A new game spawns the 28 seeded castle patrols at once. A castle that builds its Guard House later waits for the daily queue.
- The town-window Patrol row is TAOM's Reeve's Ledger. It shows for every town, and for a castle with a live patrol or one that the patrol model allows a new patrol (#992: Castle Patrols on, a non-rebel owner clan, a built Guard House and the owner culture's template); any other castle has no row, so the engine's "No Guard House" text never shows there. Vanilla's own fief lists stay town-only.
- Patrol rosters follow the limit, not the template (#991), so a template's head count only sets the mix of troops. The roster is fitted to the limit as the party size model computes it at the call, culture and feat factors included, so a culture with a party size bonus can field more than 25, 40 or 56 (Gondor's +2.5 percent gives 25, 41 and 57). After a Guard House upgrade the limit and the N/M readout refresh at once (`CastleGuardHouseUpgradeBehavior`), but the roster keeps its old head count until the patrol is back at home and refills, which fits it to the new limit.
- Town patrols were resized by this change (for example Dale level 2 from 36 to 20, Arthedain level 1 from 11 to 15); the maintainer approved it for every town and castle.
- The AI build priority makes the Guard House a likely early build for every AI town and castle, so those fiefs spend their early projects there first.
- `patrol_party_mordor` is TAOM data, not vanilla.

## Changelog

- 2026-10-23: castle patrols, the size hook and the MCM switch (#888).
- 2026-10-24: castle limits 25, 40 and 56, castle security, the AI Guard House build priority and its switch (#889); every land patrol's roster fitted to its limit (#991); the Reeve's Ledger Patrol row for castles (#992).
- 2026-10-26: review fixes (the single `GuardHouse` lookup, config range checks through `FiniteFloatValidator`) and the castle Guard House upgrade refresh (`CastleGuardHouseUpgradeBehavior`).

## GitHub Issue

- **Issues:** #888, [Castle Guard House patrols](https://github.com/haterade22/TAOM/issues/888); #889, [castle patrol strength, security and AI build priority](https://github.com/haterade22/TAOM/issues/889); #991, [patrol roster against limit](https://github.com/haterade22/TAOM/issues/991); #992, [Reeve's Ledger castle Patrol row](https://github.com/haterade22/TAOM/issues/992)
- **Status:** Open
