# Bonds of Battle

## Overview

Bonds of Battle makes the player's relations with lords and notables something earned on campaign. Four small rules share one MCM group: a relation bonus with every lord who fights on the player's side, gratitude from nearby notables after the player clears bandits, goodwill from a lord who is handed troops, and a higher cap on what a barter overpay can buy. An optional rule, off by default, takes relation from each lord who fought against the player.

## Why This Exists

- **Vanilla behavior:** Vanilla pays the side leader the player came to help (the thank-you conversation after a win, `BattleRewardModel.GetPlayerGainedRelationAmount`, never inside the player's army), a rescued villager party's owner and notables and a caravan's owner, +5 to a notable after a raid defence, and +2 to one notable per nearby village after a hideout. Other lords who fought beside the player earn nothing, and a barter overpay buys at most 3 points.
- **TAOM requirement:** A campaign that lasts hundreds of days should let a player who keeps riding with the same lords, and keeps the roads clear, be known for it. Relation is also what the later political features read, so earning it from play matters.
- **Without this feature:** Relation stays a number the player raises by clicking through barter screens, and the 3 point cap makes the overpay almost useless late in a game.

## Architecture

### Design Challenge

Three unrelated engine seams (a campaign event, a party screen call, a game model) all end in the same effect, a change of the player's relation with one hero. The new relation code goes through one shared seam so the rules (clamp, skip zero, ignore the dead) are written once and later features can reuse them.

### Solution Approach

- **Shared seam.** `IRelationGrantService.Grant(heroId, delta, reasonForLog)` in `Main/Features/Relations/` is the shared seam for new relation code. It clamps the change to -100..100, skips a zero, a blank id, an unknown or a dead hero and a dedicated server, and returns the change that landed (the relation with the player after the change minus before, which the engine's diplomacy model can make differ from the ask). A caller shows its notice only for a non-zero result, and the seam logs only then, giving the asked and the landed amount. It calls `IHeroRelationAdapter`, which wraps `ChangeRelationAction.ApplyPlayerRelation` with the quick notification off and reads the relation the player sees with `player.GetRelation(hero)` before and after the call. The seam has its own page, [relations.md](relations.md), and `RelationsModule` registers it (Bonds of Battle does not).
- **Battle.** `BondsOfBattleBehavior` listens to `CampaignEvents.OnPlayerBattleEndEvent`, which `PlayerEncounter.DoApplyMapEventResults` raises before the map event commits, so sides and casualties are still readable. `IBattleRelationAdapter` turns the map event into an engine-free `BattleSnapshot`. `BattleRelationService` decides the grants and tells the player through `IBondsNoticeAdapter`.
- **Hand-over.** Patch106 is a prefix and a postfix on `PartyScreenLogic.DoneLogic(bool)`. The prefix keeps the left party's leader and `CurrentData.PartyInfluenceChangeAmount.Item2` in Harmony's `__state`, because vanilla resets that number on success. The postfix acts only when the call returned true. That influence is non-zero only on a donating screen (Donate Troops to another clan's party, or donate to a garrison); only Donate Troops has a left owner party, the receiving party's own, so the garrison screens capture no lord. The patch reads the lord's id, name and whether he is the player or of the player's clan; `TroopHandoverService` decides. The only caller of the Donate Troops screen is `GameMenuOverlay.ExecuteTroopAction`.
- **Barter.** `TaomBarterModel` overrides only `CalculateOverpayRelationIncreaseCosts`. It reads the hero's relation and the Charm Tribute perk at its own boundary and hands plain numbers to `BarterOverpayService`, which runs `OverpayRelationCalculator`. The calculator is the engine's loop with the cap as an argument; at a cap of 3 it matches the engine for the same rolls. When the feature is off or the player is enlisted, the service answers null and the model calls `base`. The loop buys at most 3 points in vanilla (the setting here), before the Tribute perk (x1.2, rounded up) and the Charm relation bonus the engine adds when it applies the change.

### Component Diagram

```
TaomSettings (MCM "Bonds of Battle")
        |
BondsOfBattleSettingsProvider (clamps)
        |
 +------+------------------+------------------+
 |                         |                  |
BattleRelationService   TroopHandoverService  BarterOverpayService
 |  (snapshot in)          |  (id, influence)    |  (relation, overpay, Tribute)
 |                         |                     |
BondsOfBattleBehavior   Patch106 prefix/postfix  TaomBarterModel
 |                         |                     |
 +------------+------------+                     |
              |                                  |
     IRelationGrantService                (relation and perk read
              |                        in the model)
     IHeroRelationAdapter -> ChangeRelationAction
```

## Rules

### Battle

- Nothing happens when the feature is off, while the player is enlisted, or on a co-op client that defers to the host. A dedicated server grants nothing (the grant service refuses it).
- Each lord who led a party on the player's side gains `Relation With Allied Lords` (default 1).
- Each lord who led a party against the player loses `Relation Lost With Enemy Lords` (default 5), only when `Lose Relation With Enemy Lords` is on (default off).
- **Counted once per effective recipient.** The adapter reads, for each leader, the hero the engine would land the change on (`DiplomacyModel.GetHeroesForEffectiveRelation(player, leader)`: his clan leader under the vanilla pair rule, himself under [personal relations](personal-relations.md)). The service keeps one set of those recipients for the battle, ally bonus, enemy penalty and notables alike (a notable's recipient is himself), and still grants to and names the lord who fought. So with Personal Relations engaged it is one bonus per lord, and without it one bonus per clan.
- **An army the player joined earns.** The snapshot carries raw facts and the service decides: `IsInPlayersArmy` (this party is in the army the player is in) and `PlayerLeadsArmy`. A party is excluded only when both hold, so an army the player leads is excluded and, when the player joined an AI lord's army, its members, the leader included, earn the ally bonus.
- **The side leader vanilla thanks is skipped on a win.** Vanilla's thank-you conversation (`PlayerEncounter.DoPlayerVictory`, v1.5.5 :1392) pays the leader party of the player's side, who is not in the player's army, when `BattleRewardModel.GetPlayerGainedRelationAmount` for him is more than 0. The adapter reads the raw facts (`IsSideLeader`, `IsInPlayersArmy`, and the vanilla amount once per capture as `VanillaThanksAmount`, asked only inside a player encounter) and the service applies the predicate: win, player side, side leader, not in the player's army, amount above 0, and bondable (the player's own party is never bondable, so "not the main party" is covered). Such a lord is skipped and his recipient is reserved first, so a kinsman does not pay his clan through the back door. On a loss, or when vanilla's amount is 0 (a low contribution), there is no thank-you, so he earns the bonus.
- Never counted: parties of the player's clan (the main party and the player hero included), parties in an army the player leads, parties with no leader, a settlement's own party, bandit leaders, and leaders who are not lords.
- Gratitude: in the field, against bandits alone (cleared hideouts are thanked by vanilla, whose `HideoutCampaignBehavior.SetCleanHideoutRelations` gives +2 to one random notable per nearby village), when the player's side won and every party on the other side is a bandit party, one random living notable of each settlement within `Bandit Gratitude Distance` map units of the battle gains `max(1, bandit casualties / 10)`, capped at 10. The casualties are the bandits killed, wounded or routed. A settlement without a living notable is skipped. A settlement in the other side's land is skipped too: the adapter reads the settlement owner's faction and culture and the player's as one `SideIds` each (`BattleSnapshot.Player`, `SettlementNotables.Owner`, built by `FactionSides`), and `IAlignmentService.ResolveSide` sides both; one Free and the other Evil skips it, Neutral on either side keeps it (the tournament filter's reading, `TournamentAlignmentFilterService`). In a hideout battle an allied lord still earns the ally bonus.

### Hand-over

A lord handed troops on the Donate Troops screen gains `min(10, influence / 2)` plus a random -1, 0 or +1, where `influence` is what the gift is worth in the screen's own influence number. A gift worth less than 2 earns nothing, and a gift worth 2 or 3 can roll 0 (there is no floor of 1), so across the three rolls the average is half the influence. Hand-overs stop paying a lord once 10 relation has landed with him that day; the Done that reaches 10 asks only for what is left, and Charm can raise it. `TroopHandoverService` keeps, in memory only, a ledger of what landed today per relation owner: the hero the relation lands on (`GetHeroesForEffectiveRelation(player, lord)`, read by Patch106's prefix; his clan leader under the vanilla pair rule, himself under personal relations), falling back to the lord, so kinsmen with Personal Relations off share one day. The grant and the notice still name the lord. `BondsOfBattleBehavior` clears the ledger on every daily tick (`StartNewDay`) and in its constructor (`ResetForNewSession`, because the service is a process singleton and the module builds the behavior fresh in every campaign). Only the change that landed counts. A lord gains at most 10 a day from hand-overs, however the gift is split (Charm can raise the last Done). One large Done can land less than two smaller ones (a gift of 24 rolls 9, 10 or 10 where two of 12 always land 10), so the claim is a ceiling, not an equality. Nothing is granted for a gift worth under 2, to a lord of the player's own clan or the player hero, while enlisted, or on a co-op client that defers to the host. A second caller: a [Lord Courtesies](lord-courtesies.md) gift to a lord outside the player's kingdom calls `TroopHandoverService.OnHandover` itself, with the influence vanilla's donate screen would report for the donated troops, because its screen is not a donating one and so gives Patch106 nothing to read.

### Barter

Each point from the current relation `r` costs `1000 + 100 r^2`. Whole points are paid while the overpay covers them, the last point is a chance of (what is left / its cost), and the Tribute perk multiplies the total, rounded up. The loop buys at most 3 points in vanilla (the setting here, `Most Relation From One Barter Overpay`, default 15), before the Tribute perk (x1.2, rounded up) and the Charm relation bonus the engine adds when it applies the change. The model changes nothing while the player is enlisted or the feature is off. The barter path needs no co-op gate: the installed co-op module routes a client's barter to the server and never calls the model.

## Configuration

MCM group `Bonds of Battle` (GroupOrder 59). Every value is read where it is used, so nothing needs a restart. Out-of-range values from a hand-edited `TAOM.json` are clamped, and a non-finite distance reverts to its default.

| Setting | Type | Default | Range |
|---------|------|---------|-------|
| Enable Bonds of Battle | bool | on | |
| Relation With Allied Lords | int | 1 | 0 to 5 |
| Lose Relation With Enemy Lords | bool | off | |
| Relation Lost With Enemy Lords | int | 5 | 0 to 10 |
| Notables Thank You For Clearing Bandits | bool | on | |
| Bandit Gratitude Distance | float, map units | 25 | 5 to 60 |
| Troop Gifts Win Lords' Favour | bool | on | |
| Most Relation From One Barter Overpay | int | 15 | 3 to 30 |

### Why 25 map units

TAOM_Map's settlements sit a median 17 units from their nearest neighbour, and a median of 3 settlements lie within 30 units (measured on the live `settlements.xml`, 2026-10-18). 25 reaches the one to three settlements around a battle. It can reach another realm's settlements: the closest fortifications of different kingdoms are 14 units apart, and 53 town or village pairs of different kingdoms lie within 25 units of each other (measured the same day). That is why the alignment filter exists: a village that the engine sides as Free or Evil against the player's opposite side does not thank him, whatever its distance.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/BondsOfBattle/BondsOfBattleModule.cs` | Feature module: services, behavior, Patch106 category, barter model |
| `Main/Features/BondsOfBattle/BattleRelationService.cs` | Who earns what from a finished battle |
| `Main/Features/BondsOfBattle/TroopHandoverService.cs` | The hand-over gain, the daily ledger per relation owner |
| `Main/Features/BondsOfBattle/OverpayRelationCalculator.cs` | The overpay loop, with a cap argument |
| `Main/Features/BondsOfBattle/BarterOverpayService.cs` | Gates (feature, enlisted) and runs the calculator |
| `Main/Features/BondsOfBattle/BondsOfBattleSettingsProvider.cs` | Clamped MCM values and the defaults |
| `Main/Features/BondsOfBattle/Hooks/BondsOfBattleBehavior.cs` | Battle end listener, the daily tick, the per-campaign reset |
| `Main/Features/BondsOfBattle/Hooks/PartyScreenLogic_DoneLogic_Patch.cs` | Patch106 |
| `Main/Features/BondsOfBattle/Models/TaomBarterModel.cs` | Barter overpay override |
| `Main/Features/BondsOfBattle/BondsOfBattleSettings.cs` | The clamped values every service reads |
| `Main/Features/Relations/RelationGrantService.cs` | The shared seam for new relation code |
| `Main/Adapters/HeroRelationAdapter.cs` (the landed change, read through `Hero.GetRelation`), `BattleRelationAdapter.cs` (raw battle facts, recipients, notable owners, the vanilla thanks amount), `BondsNoticeAdapter.cs` | Engine boundary |

## Dependencies

- `IEnlistmentStateQuery` (Enlistment): relation bonuses pause while the player serves a lord.
- `IAlignmentService` (Execution): sides a notable's settlement and the player for the enemy-land filter (`ResolveSide(SideIds)`).
- `IRandomSource` (InitialChildGeneration): the notable pick, the hand-over spread and the last barter point.
- `ICoopSessionProvider` (CoopInterop): the battle listener and the hand-over yield to the host.
- `IDedicatedServerProvider` (CoopInterop): the grant service refuses a dedicated server.

## Localization

New lines, all in `taom_module_strings.xml`: `taom_bonds_together`, `taom_bonds_against`, `taom_bonds_bandits`, `taom_bonds_handover`. The two gratitude lines are worded without a gender ("thanks you"). The twelve languages hold the translated text (2026-11-12, #763).

## Tests

- `TAOM.Tests/Features/BondsOfBattle/BattleRelationServiceTests.cs`: ally, enemy toggle, enlisted, feature off, each exclusion, the army rules (a joined army earns, a led army is excluded, a lord outside it earns), one grant per effective recipient, the bandit math and its gates, hideout battles, the alignment filter for each side pairing and for each side's own faction and culture, the side leader vanilla thanks (skipped on a win, earns on a loss, when vanilla pays 0, and in a joined army; the reservation loop's guards), the notice for what landed.
- `TAOM.Tests/Features/BondsOfBattle/TroopHandoverServiceTests.cs`: gain bounds, the rolls and their average, a gift worth under 2 earns nothing, the split-gift property for any gift size, the daily cap (several Dones, the next day, two lords, only what landed, Charm raising what lands, a refused grant), kinsmen sharing one relation owner, each gate (feature, switch, enlisted, co-op, player clan, player hero).
- `TAOM.Tests/Features/BondsOfBattle/TroopHandoverServiceSessionResetTests.cs`: the daily ledger starts empty in a new session and on a new day.
- `TAOM.Tests/Features/BondsOfBattle/OverpayRelationCalculatorTests.cs`: parity with the engine at cap 3, cap 15, the +100 clamp, the Tribute perk, non-finite inputs.
- `TAOM.Tests/Features/BondsOfBattle/BarterOverpayServiceTests.cs`: the cap, the roll, the Tribute bonus, feature off, enlisted.
- `TAOM.Tests/Features/BondsOfBattle/BondsOfBattleSettingsProviderTests.cs`: defaults, clamps, non-finite distance.
- `TAOM.Tests/Features/BondsOfBattle/BondsOfBattleBehaviorTests.cs`: co-op client, feature off, the radius asked for, an unreadable battle, a throwing service, the daily tick clearing the hand-over day, construction resetting it.
- `TAOM.Tests/Features/BondsOfBattle/BondsOfBattleWiringTests.cs`: the module line, category, model slot, DI graph, Patch106 names and fault handling.
- `TAOM.Tests/Features/Relations/RelationGrantServiceTests.cs`: the landed change returned, clamp, zero, blank id, unknown hero, dedicated server, no log when nothing landed.
## In-game Checklist

1. Join an AI lord's army and win a battle: its leader and members each earn the ally bonus (a notice names each). Lead an army yourself: its members earn nothing.
2. A low-contribution win as the side leader's helper: the Bonds notice names him and vanilla's own thank-you does not appear. A high-contribution win: only vanilla's thank-you.
3. With Personal Relations on, two lords of one clan fight beside you: both are paid. With it off, the clan is paid once. Two kinsmen handed troops the same day share one day's 10 with it off, and have one each with it on.
4. Hand a lord 1 influence worth of troops: nothing. Hand 8: about 4 relation, one notice. Keep handing him troops the same day: his total stops at 10 and later notices stop; the next day it counts again. Reload mid-day: the day count starts over.
5. Clear bandits near villages of your own realm: one notable per village thanks you. Clear bandits near a village of the opposite side (Free against Evil): it does not.
6. Fight beside a lord already at relation 100: no notice (nothing landed).
7. Enlist with a lord: no Bonds notice, battle bonus or hand-over bonus fires.

## How to Change a Rule

1. A number the player should tune: add an MCM property in the `Bonds of Battle` group and a clamped field in `BondsOfBattleSettingsProvider`.
2. A new source of relation: call `IRelationGrantService.Grant`, the shared seam for new relation code; do not call `ChangeRelationAction` directly.
3. A new exclusion for battle lords: add a flag to `BattleParticipant`, set it in `BattleRelationAdapter`, and test it in `BattleRelationServiceTests`.

## Known Limits

- The engine runs every change through `DiplomacyModel.GetEffectiveRelationChange`, which can scale or zero the amount. The seam reports what landed, so a notice appears only for a change that moved the relation (see [relations.md](relations.md)).
- The hand-over rule fires where vanilla reports influence for the gift, which is the Donate Troops screen to a lord outside the player's clan, and where [Lord Courtesies](lord-courtesies.md) calls `OnHandover` directly for its own gift screen. In a co-op session the installed co-op module replaces `DoneLogic` with its own prefix, so the captured influence may read 0 and nothing is granted.
- **The daily hand-over count is not saved.** A reload starts the day over, so a lord can be paid up to 10 again that day.
- **A day is the span between daily ticks**, not the clock: the ledger clears on `CampaignEvents.DailyTickEvent`, so the window runs from the last tick.
- **Charm can raise the Done that reaches 10.** The cap counts what landed, and that last Done asks only for what is left, so the day can end a little above 10.

## Changelog

- 2026-10-20 (#957, part 2): the player's and the settlement owner's side ids travel as `SideIds`, not four loose strings.
- 2026-10-19: the six decisions of the review applied: an army the player joined earns, a bonus is counted once per effective recipient, a gift worth under 2 earns nothing and a lord is paid at most 10 a day from hand-overs, a village in the other side's land does not thank, the side leader vanilla thanks is skipped on a win, and the seam returns what landed. See [rca-bonds-of-battle-decisions-2026-10-19.md](../reviews/rca-bonds-of-battle-decisions-2026-10-19.md).
- 2026-10-18: first version.

## GitHub Issue

- **Issue:** #861
- **Status:** Shipped on `bannerlord-1.5.x` in `c34284a73` and `876e32af9` (v2.0.35). Closed with `triage-needs-ingame`: the in-game checks are owed.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/lord-courtesies.md](./lord-courtesies.md)
- [docs/features/relations.md](./relations.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
