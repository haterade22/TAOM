# Relations

## Overview

The shared seam for new relation code. `IRelationGrantService.Grant` clamps and filters the request, and `IHeroRelationAdapter` makes the engine call. [Bonds of Battle](bonds-of-battle.md) is the first user, [Tokens of Esteem](tokens-of-esteem.md) the second, [Good Deeds](good-deeds.md) the third and [Long Memory](long-memory.md) the fourth.

## Why This Exists

- **Vanilla behavior:** `ChangeRelationAction.ApplyPlayerRelation` takes whatever it is given, and each caller decides for itself whether a zero, a dead hero or a huge number is sensible.
- **TAOM requirement:** Several features will earn or spend relation. Writing the rules once keeps them identical for the code that uses the seam, and puts its log line in one place.
- **Without this seam:** Each feature repeats the same guards, and one of them eventually forgets one.

## Architecture

### Solution Approach

`RelationGrantService.Grant(heroId, delta, reasonForLog)`:

1. A blank hero id, a zero delta or a dedicated server returns 0 and touches nothing.
2. The delta is clamped to -100..100.
3. `IHeroRelationAdapter.ApplyPlayerRelation` applies it with the on-screen quick notification off. It returns what landed, the relation with the player after the change minus before (`player.GetRelation(hero)` read before and after the engine call), and 0 for an unknown hero, a dead hero, the player hero, a zero delta, or an engine fault (logged by the adapter). The call's own `affectRelatives` argument is not forwarded by the v1.5.6 engine, so the seam does not offer it.
4. `Grant` returns that number. A non-zero change is written to the TAOM log as `[Relations] <hero id> asked +N, landed +M (<reason>)`, and nothing is logged when nothing landed. A caller shows its notice only for a non-zero result.

### What the engine does with a change

- Every change runs through `DiplomacyModel.GetEffectiveRelationChange` first. For a positive change the default model adds the Charm skill bonus and the Charm and trait perks (and a personality factor), so the amount that lands can be larger than the one granted.
- The change lands on the clan leaders (`GetHeroesForEffectiveRelation`) under the default model, not necessarily on the hero named, unless both are the same person or one is the player's companion.
- The result is clamped to -100..100.
- Because of this the amount that lands can differ from the ask, which is why the seam returns what landed.

### Personal relations and the grant

With personal relations engaged, a player grant lands on the lord named: `GetHeroesForEffectiveRelation` keeps the other side of a pair that includes the player. With it off, the grant lands on the clan leader. So Bonds of Battle's battle grants count once per recipient (one per lord with Personal Relations engaged, one per clan without it), and its hand-overs count per relation owner, the hero the relation lands on, so kinsmen share one day's cap without it.

### Rules for callers

- **Key any throttle on the relation owner.** Ask `DiplomacyModel.GetHeroesForEffectiveRelation(player, hero, ...)` and key a cooldown, a daily cap or a "once per" ledger on the second hero it returns (the hero itself when it returns none). With Personal Relations off a change lands on the clan leader, so two kinsmen share one relation and a throttle keyed on the hero lets the player repeat it once per kinsman. Bonds of Battle's hand-over cap and Tokens of Esteem's gift cooldown both do this.
- **List the heroes, then call `GrantStrongestPerOwner`.** A caller that asks several heroes at once builds a `RelationAsk(RelationTarget?, delta)` for each (a null target, a dead hero, the player and a zero delta are skipped) and calls `grants.GrantStrongestPerOwner(asks, reasonForLog)` (`RelationGrantServiceExtensions`). It grants the strongest ask once per relation owner (the largest `|delta|`, the first listed on a tie, owners in the order they first appear, a blank owner falling back to the hero) and returns how many landed. It is an extension, not a member of the interface, so a fake of the seam needs no change. `RelationTargets.Of(player, hero)` in `Main/Adapters` builds a `RelationTarget` from an engine hero; `SideMultipliers` is the shared record of three multipliers (`Same`, `Neutral`, `Opposite`), and its `For(first, second)` picks one for a pair of sides (Neutral when either side is Neutral, then Same, else Opposite); the sides themselves come from `IAlignmentService.ResolveSide(SideIds)` over a `FactionSides` pair. Good Deeds and Long Memory use all three.
- **The landed change can differ from the ask.** The Charm bonus and perks can add to a positive change, and the engine clamps the result to -100..100, so the number `Grant` returns is the one to show, to log and to count against a cap. Never assume the ask landed.

### Callers outside the seam

Six older callers still change relation directly and are migration candidates: `CommanderLordAdapter.ApplyPlayerRelation`, `ArmouryPlayerAdapter.ChangeRelationWith`, `SiegeDefenseService.GrantReward`, `AshesWorldAdapter`, `HardQuestionsActionAdapter` and `NamedCompanionAdapter`. An amount that a game model returns (the barter overpay, and Good Deeds' donation bonus) is applied by vanilla, not by the seam.

### One registration only

Never register a second `IRelationGrantService` or `IHeroRelationAdapter`. A second unkeyed default makes every resolve of the interface throw (see the note above the UncapturableHeroes registration in `Main/IoC.cs`).

### Component Diagram

```
Feature service
      |
IRelationGrantService (RelationGrantService)
      |
IHeroRelationAdapter (HeroRelationAdapter)
      |
ChangeRelationAction
```

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Relations/IRelationGrantService.cs` | The seam |
| `Main/Features/Relations/RelationGrantService.cs` | Clamp, filter, log |
| `Main/Features/Relations/RelationTarget.cs` | `RelationTarget` (a hero and the owner a change to it lands on) and `RelationAsk` (a target and a signed delta) |
| `Main/Features/Relations/RelationGrantServiceExtensions.cs` | `GrantStrongestPerOwner`: the strongest ask once per relation owner, counted by what landed |
| `Main/Features/Relations/SideMultipliers.cs` | The multiplier for each way two sides can stand, with the pairing rule |
| `Main/Adapters/RelationTargets.cs` | Builds a `RelationTarget` from an engine hero |
| `Main/Adapters/IHeroRelationAdapter.cs`, `HeroRelationAdapter.cs` | Engine boundary |

## Dependencies

- `IHeroRelationAdapter` (Adapters): the engine call.
- `IDedicatedServerProvider` (CoopInterop): a dedicated server grants nothing.
- `IModLogger` (Core): the log line.

## Registration

`RelationsModule` (`Main/Features/Relations/RelationsModule.cs`) registers both services, and also Personal Relations ([personal-relations.md](personal-relations.md)). Bonds of Battle used to carry the registration; it no longer does, and a feature module must not register the seam again.

## Tests

- `TAOM.Tests/Features/Relations/RelationGrantServiceTests.cs`: the landed change returned, zero, dedicated server, clamp at both ends and at `int.MinValue` and `int.MaxValue`, blank ids, unknown hero, log line.
- `TAOM.Tests/Features/Relations/RelationGrantServiceExtensionsTests.cs`: the strongest gain, the strongest loss, a gain against a loss, a tie, owner order, a blank owner, null, dead, player and zero asks skipped, a grant that returns 0 not counted, `int.MinValue`.
- `TAOM.Tests/Features/Relations/SideMultipliersTests.cs`: the nine ordered pairings of three sides, each by its own row, with three different multipliers.
- `TAOM.Tests/Features/Relations/PersonalRelationServiceTests.cs`, `PersonalRelationSettingsProviderTests.cs`, `PersonalRelationSeedServiceTests.cs`, `PersonalRelationSeedBehaviorTests.cs`, `PersonalRelationNoticeTests.cs`, `PersonalRelationNoticeBindingTests.cs`, `RelationsWiringTests.cs`, `TaomDiplomacyModelPersonalRelationsTests.cs`: Personal Relations, listed in [personal-relations.md](personal-relations.md).

## Known Limits

The seam reports the change that landed, so a diplomacy model that scaled or zeroed the amount is visible to the caller. The seam has six direct callers it does not cover, and it does not handle amounts a game model returns.

## Changelog

- 2026-10-21: the pieces Good Deeds and Long Memory shared move here: `RelationTarget` and `RelationAsk`, `GrantStrongestPerOwner`, `SideMultipliers.For` and `RelationTargets.Of` (Good Deeds' own copies are gone, behavior unchanged).
- 2026-10-21: Long Memory is the fourth user ([long-memory.md](long-memory.md), #865): relation lost for raiding a village or forcing it to give goods or recruits, grouped per relation owner before each grant and counted by what landed. Its asks are negative, and the clamp at -100 applies to them as it does to a gain.
- 2026-10-20: Good Deeds is the third user ([good-deeds.md](good-deeds.md), #864): release, tournament and (through a game model) donation rewards, grouped per relation owner before each grant and counted by what landed.
- 2026-10-19: the seam returns what landed (an int, the relation the player sees after the change minus before, read with `Hero.GetRelation` around the engine call), 0 for any refusal, and logs the asked and the landed amount only when something landed.
- 2026-10-18: first version, with Bonds of Battle.

## GitHub Issue

- **Issue:** #861
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/bonds-of-battle.md](./bonds-of-battle.md)
- [docs/features/good-deeds.md](./good-deeds.md)
- [docs/features/long-memory.md](./long-memory.md)
- [docs/features/tokens-of-esteem.md](./tokens-of-esteem.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
