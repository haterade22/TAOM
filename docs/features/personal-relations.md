# Personal Relations

## Overview

Personal Relations keeps the player's relation with a lord on that lord. Vanilla maps each hero with a clan to his clan leader when it judges a relation, so a friendship with Faramir counts as a friendship with Denethor. With this feature on, in a single player campaign, a relation between the player and any hero is read and changed on that hero. Relations between AI lords stay vanilla. Four smaller parts go with it: a one-time seeding of an old save, a larger disband cost for an army's leader, an heir who keeps his own relation, and a relation notice that names the lord instead of his clan.

It applies to every dealing the engine credits to the player: the player's own deeds, and also his emissaries, companions and family (a relation change between a player-clan hero and a lord is credited to the player, so it lands on the lord named).

## Why This Exists

- **Vanilla behavior:** `DefaultDiplomacyModel.GetHeroesForEffectiveRelation` maps both heroes to their clan leaders, except for same-clan pairs and player-companion pairs. `GetEffectiveRelation`, `Hero.GetRelation` and `ChangeRelationAction` all go through it, for every pair. Disbanding another lord's army by paying influence costs -4 with the leader and -1 with each member. When a clan's leader changes (death, a leader changing clan, a member elected king), `ChangeClanLeaderAction` gives the heir his raw relation plus `GetRelationChangeAfterClanLeaderIsDead`, which is 0.7 of the dead leader's raw relation, for every living hero.
- **TAOM requirement:** TAOM has named characters with their own temperaments (Faramir and Denethor, Eomer and Theoden). Relation should follow the person the player dealt with, and paying influence to disband a lord's army should cost something real.
- **Without this feature:** One clan leader's number stands for every lord of the clan, and the disband cost is a rounding error.

## Architecture

### Design Challenge

The pair rule runs on every relation read, so it has to be cheap: one bool before anything else. The engine calls it on the main thread only (v1.5.5), but under the installed co-op module `Hero.IsHumanPlayerCharacter` is patched into a scan of every party, and the co-op presence and session reads take locks or use reflection. So the rule never reads co-op state: a behavior reads it once a day and arms or disarms a volatile flag. The model is hand-wired in `SubModule.OnGameStart`, so its service has to be registered before that line runs. An old save holds the player's earned relation on clan leaders and about 0 on every other member, so turning the feature on would read as lost relation unless it is seeded first.

### Solution Approach

- **The gate.** `IPersonalRelationService.IsActive` is `_engaged && settings.Enabled`. `_engaged` is a volatile bool, false at start. Only `PersonalRelationSeedBehavior` calls `Engage`. `Enabled` is one read through `PersonalRelationSettingsProvider`'s lazy `Settings` accessor, the same settings pattern as the other campaign providers.
- **Pair rule.** `TaomDiplomacyModel.GetHeroesForEffectiveRelation` calls `base` first, so every AI-to-AI pair stays vanilla. Then, only after a cheap `IsActive` read, it asks `Resolve(effective1IsPlayer, effective2IsPlayer)`. When exactly one effective hero is the player (`Hero.MainHero`), the OTHER effective hero is put back to the hero the engine was asked about: (player, Denethor) from an original (companion, Faramir) becomes (player, Faramir). The service holds the decision as pure facts and returns `None`, `UnmapFirst` or `UnmapSecond`; the model's two reference compares and three ternaries are the boundary conversion that `.claude/rules/gamemodels.md` rule 4 allows. The player is never read through `IsHumanPlayerCharacter`.
- **Disband cost.** `GetRelationCostOfDisbandingArmy(bool isLeaderParty)` returns `service.DisbandCost(isLeaderParty) ?? base`. The service answers null (vanilla's -4 and -1) unless the service is active and the disband cost is on, and always for a member. Only the army leader rolls: the result is the negative of a roll from 28 to 32 inclusive, drawn through `IRandomSource.Next(min, max + 1)`. The only vanilla caller is `DisbandArmyAction.ApplyInternal` (`DismissalRequestedWithInfluence`), on the main thread. The member cost stays vanilla's 1 because AI armies can call a player-clan companion party, and `CompanionRolesCampaignBehavior` removes a player companion when a negative change leaves the relation below -10. The army leader is never a player-clan hero.
- **Heirs.** `GetRelationChangeAfterClanLeaderIsDead(deadLeader, relationHero)` returns 0 when the service is active and `relationHero` is the player, else `base`. An heir keeps the relation he earned himself and does not inherit 0.7 of the dead leader's.
- **Seeding.** `PersonalRelationSeedBehavior` holds a saved flag. At `OnSessionLaunched`, and on every daily tick, it asks `PersonalRelationSeedService.TrySeed(flag)`, then calls `Engage(flag && IsSinglePlayer)`. The constructor calls `Engage(false)` (the module builds the behavior in every campaign's `OnGameStart`), and a fault calls `Engage(false)` and logs. `TrySeed` runs in this order: (1) on a peer that does not own the save (`ShouldDeferToHost` or a dedicated server) it returns the loaded flag untouched; (2) with the switch off it returns false, so the seeding re-arms; (3) an already set flag returns true; (4) otherwise it reads one `SeedRow` per living lord through `IPersonalRelationSeedAdapter` and writes each clan leader's raw relation onto every other lord of that clan whose own raw relation is 0, through `Hero.SetPersonalRelation`. A raw write fires no event and shows no notice. Then the flag is set. `IsSinglePlayer` is `!IsSessionActive && !ShouldDeferToHost && !IsDedicatedServer`, the predicate the hunt uses, read once a day on the main thread.
- **Notice.** Patch107 is a prefix on the private `DefaultNotificationsCampaignBehavior.OnRelationChanged`. Vanilla names the clan for any hero whose clan is not the player's. While the service is active, the prefix shows vanilla's own `str_your_relation_increased_with_notable` or `_decreased_` text, which names the hero, with the same value, magnitude, sound and notable portrait rule, and skips the original. It returns true, and vanilla runs untouched, when the service is inactive, when vanilla's own gate is closed, or on any fault the prefix catches. No new strings.

### Component Diagram

```
TaomSettings (MCM "Personal Relations")
        |
PersonalRelationSettingsProvider (lazy Settings, clamps, swaps min and max)
        |
        +--------------------------------------+
        |                                      |
PersonalRelationService  <---- Engage ---- PersonalRelationSeedBehavior
 |  IsActive (volatile bool + switch)         (saved flag; session start, daily tick)
 |  Resolve, DisbandCost,                         |
 |  InheritedRelationChange                  PersonalRelationSeedService
 |                                            (TrySeed, IsSinglePlayer, Plan: pure)
 +-- TaomDiplomacyModel (three overrides)         |
 +-- Patch107 prefix (Run: testable)          IPersonalRelationSeedAdapter
                |                              -> Hero.SetPersonalRelation
        IPersonalRelationNoticeAdapter
         (vanilla's own text)
```

## Rules

### Which pairs

| Pair | Active | Not active |
|------|--------|-----------|
| Player and any hero | The other side is kept on the hero asked about | Vanilla (clan leaders) |
| AI hero and AI hero | Vanilla | Vanilla |

Same-clan pairs and player-companion pairs were already kept as they are by vanilla. "Active" means a single player campaign, a seeded save, and the MCM switch on. Co-op sessions, hosts and dedicated servers stay vanilla.

### What it changes for the rest of the game

With the rule active, reads that go through `GetEffectiveRelation` judge the player by the lord named: barter, persuasion, the army-call cost and lord conversations. Votes, defection and clan-level marriage still judge the player by the clan leader, counting only what was earned with him. "Off" returns the pair rule, the disband cost and the inheritance to vanilla, while seeded and earned values stay in the raw store, which vanilla's raw readers (clan scores, conversation tags, romance approval) still use.

### Disband cost

The army's leader: a whole number from 28 to 32 inclusive, lost. Members: vanilla's 1. A range of 0 to 0 costs nothing, which is a deliberate setting and is not mistaken for vanilla. A minimum above the maximum is swapped, so 40 and 30 mean 30 to 40.

### Heirs

An heir keeps the relation he earned himself, whenever a clan's leader changes (death, a leader changing clan, a member elected king). The 0.7 share of the dead leader's relation that vanilla hands every living hero is 0 for the player.

### Seeding, entity state matrix

| Hero state | Seeded? | Why |
|-----------|---------|-----|
| Clan leader | No | The source of the value |
| Lord whose own raw relation is not 0 | No | An earned personal value is kept |
| Lord at 0 whose leader's value is not 0 | Yes | Takes the leader's value |
| Lord at 0 whose leader's value is 0 | No | Nothing to copy |
| Lord of the player's clan | No | The player's own household |
| Clanless hero | No | No leader to copy from |
| Non-lord (notable, wanderer, companion) | No | Only lords are listed |
| Dead hero | No | Only living heroes are listed |
| Prisoner or fugitive lord | Yes | A raw relation write is harmless to either state |
| Clan whose leader is not listed | No | No value to copy |

The write is idempotent (a second run finds the lords no longer at 0).

### Re-seeding

The seeding runs again when the feature has been off on at least one in-game day. With the switch off a daily tick returns false, which clears the flag and disarms the service; when the switch is back on, the next tick seeds again and re-arms. Lords the player has earned a personal value with are kept (they are no longer at 0), so a re-seed fills only the lords still at 0.

## Configuration

MCM group `Personal Relations` (GroupOrder 60). Every value is read where it is used, so nothing needs a restart. The integers are clamped to 0 to 50, so a hand-edited `TAOM.json` cannot carry a negative or huge cost. A leader range given the wrong way round is swapped after clamping. There is no NaN case; the values are integers.

| Setting | Type | Default | Range |
|---------|------|---------|-------|
| Enable Personal Relations | bool | on | |
| Disbanding An Army Costs Relation | bool | on | |
| Disband Cost, Army Leader, Least | int | 28 | 0 to 50 |
| Disband Cost, Army Leader, Most | int | 32 | 0 to 50 |

The disband cost applies only in single player, and only while personal relations are on.

## Co-op

Single player only. The seeding yields to the host and skips a dedicated server (`ICoopSessionProvider.ShouldDeferToHost`, `IDedicatedServerProvider`), and a live co-op session, host included, leaves the service disarmed, so the pair rule, the disband cost, the succession rule and Patch107 are vanilla there. Nothing on the relation hot path reads co-op state.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Relations/RelationsModule.cs` | Feature module: the seam, the services, the behavior, Patch107 |
| `Main/Features/Relations/PersonalRelationService.cs` | `IsActive`, `Resolve`, the disband cost, the succession rule |
| `Main/Features/Relations/PersonalRelationSettingsProvider.cs` | Clamped values, the cached master switch |
| `Main/Features/Relations/PersonalRelationSeedService.cs` | `TrySeed`, `IsSinglePlayer`, `Plan` (pure) |
| `Main/Features/Relations/Hooks/PersonalRelationSeedBehavior.cs` | The saved flag, the triggers, the only caller of `Engage` |
| `Main/Features/Relations/Hooks/DefaultNotificationsCampaignBehavior_OnRelationChanged_Patch.cs` | Patch107 |
| `Main/Features/Diplomacy/Models/TaomDiplomacyModel.cs` | The three overrides |
| `Main/Adapters/PersonalRelationSeedAdapter.cs`, `PersonalRelationNoticeAdapter.cs` | Engine boundary |
| `Main/SubModule.cs` | One line: the model's fourth constructor argument |

## Dependencies

- `IRandomSource` (InitialChildGeneration): the disband roll.
- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop): the seeding's authority gate and `IsSinglePlayer`.
- `IModLogger` (Core): the seeding summary line and fault lines.

## Tests

107 tests in 8 classes under `TAOM.Tests/Features/Relations/`:

- `PersonalRelationServiceTests.cs` (27): `IsActive` (not engaged, live switch, cached switch only), every combination of `Resolve` facts, active and not, the leader-only disband cost and its toggles, the succession rule.
- `PersonalRelationSettingsProviderTests.cs` (12): defaults, clamps, swapped ranges.
- `PersonalRelationSeedServiceTests.cs` (33): the seeding rules, authority gates, the loaded flag kept on a non-owning peer, re-arming, `IsSinglePlayer`.
- `PersonalRelationSeedBehaviorTests.cs` (11): the flag round trip, an old save without the key, never repeating, engaging and disengaging, a fault.
- `PersonalRelationNoticeTests.cs` (6) and `PersonalRelationNoticeBindingTests.cs` (3): the gate and the engine signature.
- `RelationsWiringTests.cs` (10): the module, the seam registered once, the composition order, the model construction.
- `TaomDiplomacyModelPersonalRelationsTests.cs` (5): the disband override, and the IL shape of the pair and succession overrides (the cheap `IsActive` read before `Hero.MainHero` in both, `base` first in the pair override, nothing heavy).

The provider also joins `CampaignHotPathSettingsProvidersTests`. The pair override reads `Hero.MainHero`, so its decision is covered by `Resolve` and its shape by the IL test; the behavior in a campaign is covered by the in-game checklist below.

## How to Change a Rule

1. A number the player should tune: add an MCM property to the `Personal Relations` group and a clamped field in `PersonalRelationSettingsProvider`.
2. A new place where the player's relation should stay on the lord named: it needs no code if it goes through `GetEffectiveRelation` or `ChangeRelationAction`; otherwise see the known limit below.
3. A new reason to arm or disarm the service: change the behavior's `Engage` call, never the model.

## Performance

`GetHeroesForEffectiveRelation` runs for every relation read, on the main thread. Inactive, it costs the base call and one volatile bool plus one cached settings read. Active, it adds two reference compares against `Hero.MainHero` and a `Resolve` call that allocates nothing. It never logs, never resolves from the container and never reads co-op state. The seeding runs once, then costs one `TrySeed` call per day.

## Known Limits and Risks

- **Clan-level writes reach only the clan leader.** Blood feud (-100) and the execution penalties (`ExecutionCampaignBehavior`), the ransom-broker settlement, and the raid and battle aftermath between clan leaders (`CharacterRelationCampaignBehavior`) write between clan leaders, not between the player and the lord named. The follow-up path is `CampaignEvents.OnBloodFeudStateChangedEvent`.
- **The seeding is not undone** when the feature is turned off. Seeded and earned values stay in the raw store, which vanilla's raw readers still use.
- **Co-op and dedicated servers stay vanilla**, by design.
- **A co-op session that goes live mid-campaign** leaves the feature engaged until the next daily tick or load, up to one day, because the single player test is read once a day.
- **Seeding outside single player:** a co-op host, a client and a dedicated server keep the loaded flag and seed nothing.
- **Play test needed:** the rule changes what barter, persuasion and conversations see.

## In-game Checklist

1. Old save: load it with the feature on; the log shows `[PersonalRelations] seeded N of M lord(s)`, and a lord at 0 now shows his clan leader's relation.
2. Reload that save: no second seeding line.
3. Switch the feature on later (start with it off, switch on): the lords are seeded on the next day.
4. Raise relation with one lord of a clan: the notice names the lord, and his clan leader is unchanged.
5. Switch the feature off: the notice names the clan again, relation reads are vanilla.
6. A companion and a same-clan pair read as before.
7. Disband an AI army by paying influence with a companion in it: the army's leader loses 28 to 32, the companion loses 1.
8. Let a clan leader die (or change a clan's leader another way): the heir keeps the relation he earned himself.
9. Co-op or dedicated server: everything above is vanilla.

## Changelog

- 2026-10-19: first version. Deep-review pass: single player only through an armed service, the other effective hero un-mapped, a leader-only disband cost, heirs keep their own relation, the seeding re-arms.

## GitHub Issue

- **Issue:** #862
- **Status:** Shipped on `bannerlord-1.5.x` in `e6204df97` (v2.0.35). Closed with `triage-needs-ingame`: the in-game checks are owed.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/bonds-of-battle.md](./bonds-of-battle.md)
- [docs/features/diplomacy.md](./diplomacy.md)
- [docs/features/lord-courtesies.md](./lord-courtesies.md)
- [docs/features/relations.md](./relations.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/gamemodel-registry.md](../reference/gamemodel-registry.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
