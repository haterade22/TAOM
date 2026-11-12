# Lord Retinue

## Overview

AI lord parties take one idle adult hero of their own clan along and give that hero an empty party role: scout, surgeon, engineer or quartermaster. Vanilla never does this, so every AI party runs all four roles on the leader's own skills. The feature adds no Harmony patch and no GameModel. Status: built and unit-tested, not yet checked in game.

## Why This Exists

Siblings, grown children and spouses of a lord sit in a settlement while the lord rides. A hero in the party roster fights as a hero agent in battle and can captain a formation. A hero in a role gives the party that role's skill instead of the leader's. Issue [#1122](https://github.com/haterade22/TAOM/issues/1122).

## Architecture

`LordRetinueBehavior` listens to two events and keeps no state. Heroes, rosters and roles are saved by the engine.

- **`DailyTickPartyEvent`:** reads the party through `LordRetinueWorld`, then either releases its retinue or takes one more hero.
- **`OnPartyDisbandStartedEvent`:** takes every hero except the leader out of the party (see "Release rules").

The pure `LordRetinueService` decides. `LordRetinueWorld` (`Main/Features/LordRetinue/Hooks/LordRetinueWorld.cs`) does the engine reads and writes: `AddHeroToPartyAction.Apply`, `MobileParty.SetHeroPartyRole`, `TeleportHeroAction.ApplyImmediateTeleportToSettlement` and `MakeHeroFugitiveAction.Apply`.

### When a party takes a hero

All of these hold:

- The party is an AI lord party that this peer may change: `ICoopSessionProvider.IsAuthority` is true, `ShouldDeferToHost` is false, and `IPlayerControlQuery` says `NotPlayer` for the party and its clan. An unreadable co-op registry counts as a player.
- The party sits in a settlement, has a leader, is in no map event, is not besieging, is not disbanding, and its settlement is not under siege.
- Its clan is at its party limit (`GetPartyLimitForTier`). Below the limit, vanilla's `GetBestAvailableCommander` turns any idle adult lord of the clan into a party leader, and a hero aboard a party is never a candidate for that.
- It carries fewer extra heroes than the MCM cap, and at least one of the four roles is empty.

### Who is taken

An eligible hero is a living, active, adult hero of the party's clan who stands in that same settlement and belongs to no party. The hero is not a prisoner, a player-controlled hero, a template, a notable, a wanderer, wounded, noncombatant, a governor or the clan leader. `Hero.CanMoveToSettlement()` must be true, the check vanilla makes before it moves an idle hero, and `Hero.CanBeGovernorOrHavePartyRole()` must be true, the check vanilla makes before it gives a role (quests and issues veto through both). `IsNoncombatant` is skill based: a hero counts as a combatant at 100 or more in a weapon skill, so it mostly removes spouses.

### Which role

The leader already fills every empty role, so a role only improves when a hero beats the leader. The service takes the eligible hero and empty role with the largest skill gain over the leader (Scouting, Medicine, Engineering, Steward). A hero who is no better than the leader at any empty role is not taken. A weaker quartermaster would shrink the party size limit, because the Steward effect reads the role holder's skill only.

### Release rules

- **Clan drops below its party limit, or the feature is turned off:** the daily tick moves the party's retinue back into the settlement it sits in. The retinue is the same-clan non-leader heroes that hold a party role in that party. Vanilla then sees them as idle again.
- **A party starts to disband** (its leader married away, for example): every hero except the leader leaves the party at once. A party in a settlement puts the hero into that settlement with `TeleportHeroAction`. Away from a settlement, or when the teleport declines, the hero becomes a fugitive through `MakeHeroFugitiveAction`, vanilla's own release for a disbanding party away from a settlement, and returns to a settlement within a few days (a fugitive returns with a 30% chance a day). Without this release, the garrison merge copies the whole roster into the garrison and then clears it, which leaves a hero listed in the garrison with no party (v1.5.7 `DisbandPartyCampaignBehavior.MergeDisbandPartyToFortification`). The release runs at the start of the disband, so it does not depend on listener order, and it does not depend on the enable switch. Cost: vanilla's search for a replacement leader runs after it and only counts parties with 10 or more roster entries, so a release can push a party of 10 or 11 entries under that line.
- **Lowering the cap** releases nobody; heroes already riding along stay.
- **Lord killed:** vanilla promotes the first other hero in the roster. **Party defeated:** vanilla captures or frees every hero. Both are clean.

A role clears itself: `Hero.SetPartyBelongedTo` clears every role of a hero who leaves a party, and each `Effective*` role on `MobileParty` falls back to the leader.

## Configuration

MCM group `World/Lord Retinue` in `TaomSettings`: `EnableLordRetinue` (default on) and `LordRetinueMaxExtraHeroes` (0 to `LordRetinueSettingsProvider.MaxExtraHeroesCap`, which is 2; default 1). Both are read live through `LordRetinueSettingsProvider`, which clamps the cap. The dedicated-server settings file validates the cap against the same bounds. Both settings count as simulation-relevant for the co-op fingerprint.

## Costs to know

- **Wage:** a retinue hero who is not the clan leader costs the clan `2 + 2 * Level` gold a day (`DefaultPartyWageModel`). AI wage relief defaults to 0.
- **Party size:** the hero takes a slot with no limit check. A full party loses one lowest-tier regular to the daily shed.
- **Food:** one more member eats.
- **Churn:** when a clan loses one party, every party of that clan sitting in a settlement releases its retinue that day, and the clan takes heroes again over the following days as vanilla fills its slots.
- **Death:** the hero is a real hero agent. More family members fight, so more can die, and a lord's death promotes the retinue hero to leader instead of disbanding the party.

## Key Files

| File | Role |
|---|---|
| `Main/Features/LordRetinue/LordRetinueService.cs` | Pure decisions |
| `Main/Features/LordRetinue/RetinueModels.cs` | Snapshots and role flags |
| `Main/Features/LordRetinue/LordRetinueSettingsProvider.cs` | MCM reader with clamp |
| `Main/Features/LordRetinue/Hooks/LordRetinueBehavior.cs` | Entry point: two events and the player and co-op gates |
| `Main/Features/LordRetinue/Hooks/LordRetinueWorld.cs` | Engine reads and writes, used by the behavior only (no interface: nothing fakes or re-implements it) |
| `Main/IoC.cs`, `Main/SubModule.cs` | Registration and `AddBehavior` |

## Tests

`TAOM.Tests/Features/LordRetinue/LordRetinueServiceTests.cs` pins the cap, the switch, the party-limit rule, every skip condition of `IsEligible` (one test each), the leader-skill comparison, the gain ranking and the release rule. `RetinueGateTests` pins the co-op and player gate. `CampaignHotPathSettingsProvidersTests` covers the settings reader. The behavior and `LordRetinueWorld` read the engine and have no unit test.

## How to verify in game

1. Load a save with AI lords that have adult relatives in the same castle or town, and a clan at its party limit.
2. Wait a few days. A lord's party that rests in that settlement should gain one clan hero, and only when the hero beats the lord at a role.
3. Read the party roles in the encyclopedia or with the console.
4. Release: marry a retinue party's leader to another clan's leader (`campaign.marry_hero_to_hero`) and wait two days. The retinue hero must not stay listed in a garrison.
5. Size: compare the party size tooltip before and after a Steward-best relative joins a high-Steward lord's party. It must not drop.

Not done yet. The label `triage-needs-ingame` applies to #1122.

## Not covered

- What a leftover garrison entry does in a siege or a capture, if some path other than the disband start leaves one.
- Clan succession when a retinue hero dies in battle, and `ChangeKingdomAction` for a retinue hero (not read).
- Generated retainer heroes for lords with no idle family: a later phase, after the maintainer decides.
