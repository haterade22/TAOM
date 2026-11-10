# Kingdom Allegiance

## Overview

AI lords keep to their side of the war. An AI clan of the Free Peoples never joins, defects to or hires on as mercenaries with an Evil realm, and an Evil clan never does so with a Free realm. A new campaign also starts with no friendships between Free and Evil lords: each one becomes enmity of the same strength. Neutral clans and realms deal with either side, and the player keeps the base game's freedom.

## Why This Exists

- **Vanilla behavior:** `DiplomaticBartersBehavior.DailyTickClan` (v1.5.6, unchanged in v1.5.7) lets any clan weigh joining any realm (`ConsiderClanJoin`), defecting to one (`ConsiderDefection`) or hiring on (`ConsiderClanJoinAsMercenary`). The scores come from `DiplomacyModel.GetScoreOfClanToJoinKingdom` and `GetScoreOfMercenaryToJoinKingdom`, where culture is only a weight: 15% of the score, and a 10 to 1 pick of a realm of the clan's own culture. At new-game creation `CharacterRelationCampaignBehavior.UpdateFriendshipAndEnemies` rolls a random relation for pairs of lords, alive and dead, weighted by distance and clan tier. Two rulers of the same culture always start as enemies, but the roll never reads the Free/Evil side.
- **TAOM requirement:** A player reported about three orc lords serving Gondor in the public build (#1111), and Free lords show Evil lords among their friends. The start data is correct (`characters/clans.xml` and `spclans.xslt` put only `Culture.gondor` clans in Gondor), so the drift happens during play.
- **Without this feature:** Orc clans drift into Gondor and Rohan over a campaign, and the encyclopedia shows a Gondor lord as the friend of a Mordor lord.

## Architecture

### Design Challenge

During play an AI clan changes realm through four engine paths (join, defection, mercenary hire, a mercenary moving on), all of them barters scored by the diplomacy model, so a gate on one action would miss the others. The start scenarios, cheats and StoryMode quests move clans without a score (Known Limits). The mercenary join score has a second reader: vanilla's `GetScoreOfMercenaryToLeaveKingdom` subtracts the join score of the clan's own realm (`10000 * days factor - 5000 - join`), so a veto must never answer for the realm a clan already serves. The side of a clan has to be the clan's own people, not its realm: an orc clan already inside Gondor reads as Free through `Hero.MapFaction`. "Is this a player" comes from `IPlayerControlQuery`, which knows co-op peers, never from `Clan.PlayerClan`. A TAOM listener on `OnNewGameCreatedEvent` would run before vanilla's relation roll, because listeners run newest first.

### Solution Approach

One feature module, one service, two entry points, one MCM switch. Nothing is saved.

- **The join veto.** `TaomDiplomacyModel` overrides `GetScoreOfClanToJoinKingdom` and `GetScoreOfMercenaryToJoinKingdom` as `service.JoinVeto(reader.Read(clan, kingdom)) ?? base`. `AllegianceFactsReader`, a boundary reader (ADR-007; an adapter interface may not name `Clan`), reads the clan's side pair (`FactionSides.Of(clan)`: the clan id, then the clan's culture), the realm's pair, who plays each through `IPlayerControlQuery`, and whether the clan already serves the realm. `KingdomAllegianceService.JoinVeto` returns -100000000, the value vanilla's own score gives a realm's ruling clan, when the clan does not already serve the realm, the clan is AI, the realm's leader is AI, the peer does not defer to a co-op host, the switch is on, and `FactionSideRules.AreOpposed` says the two sides are Free and Evil. Anything else returns null, and vanilla's score stands. `PlayerControl.Unknown` counts as a player, so doubt keeps vanilla. A fault in the reader goes to `IOutcomeLog.Swallowed` (one line, then a count) and keeps vanilla.
- **Why scores and not "constant war".** `DiplomaticBartersBehavior` also checks `IsAtConstantWar(clan, kingdom)` before a join or defection, but a constant war would also lock peace between those factions.
- **What the veto covers.** The clan's value in `JoinKingdomAsClanBarterable` is the join score, and `ConsiderClanJoin` and `ConsiderDefection` add it to the realm's value, so -100000000 makes both sums negative. `MercenaryJoinKingdomBarterable` adds the mercenary join score the same way, plus the leave score of the clan's current realm. Because the clan's own realm is never vetoed, that leave score stays vanilla's, so it cannot cancel the veto on a move into a second opposed realm, and an opposed mercenary that already serves is not pushed out. The values are cast to `int`, and -100000000 plus a realm's score stays far inside `int`.
- **The friendship flip.** `Patch145_FriendshipSeeding_Postfix` runs after `UpdateFriendshipAndEnemies` and calls `FlipOpposedFriendships`. `AllegianceRelationAdapter` lists the lords vanilla's roll covers, alive and dead (a lord, not the main hero, with a map faction the main hero does not lead), each sided by their own clan (`FactionSides.OfOwnClan`). Dead lords count because a dead lord's encyclopedia page lists their living friends. The adapter is a process singleton, so the service releases its hero list when the run ends, fault or not. The service resolves each lord's side once, then for each Free-vs-Evil pair reads the raw relation and writes its negative when it is above 0. Same-side rivalries and Neutral lords keep their roll. The write is `Hero.SetPersonalRelation`: no event, no notice. It writes one `[Check]` line with the counts.

### Component Diagram

```
TaomSettings (MCM "World/Kingdom Allegiance")
        |
KingdomAllegianceSettingsProvider
        |
KingdomAllegianceService  <-- IAlignmentService (execution/alignment.json), ICoopSessionProvider, IOutcomeLog
   |                    |
JoinVeto            FlipOpposedFriendships
   |                    |
TaomDiplomacyModel   Patch145 (postfix on UpdateFriendshipAndEnemies)
(two join scores)       |
   |                AllegianceRelationAdapter
AllegianceFactsReader (IPlayerControlQuery)
```

## Configuration

No config file. The sides come from `Main/_Module/ModuleData/execution/alignment.json`, the table every alignment feature reads ([id cheatsheet](../modding/id-cheatsheet.md)). An unlisted clan id falls back to the clan's culture, and an unknown culture is Neutral.

MCM "World/Kingdom Allegiance", "Enable Kingdom Allegiance", default on, no restart. The friendship flip applies to new campaigns only.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/KingdomAllegiance/KingdomAllegianceService.cs` | Both rules, pure over facts and the relation adapter |
| `Main/Features/KingdomAllegiance/AllegianceFacts.cs` | The facts of one join score |
| `Main/Features/KingdomAllegiance/KingdomAllegianceModule.cs` | Feature module: services and the patch category |
| `Main/Features/KingdomAllegiance/KingdomAllegianceSettingsProvider.cs` | The MCM switch |
| `Main/Features/KingdomAllegiance/Hooks/Patch145_FriendshipSeeding_Postfix.cs` | Postfix on the new-game relation roll |
| `Main/Features/Diplomacy/Models/TaomDiplomacyModel.cs` | The two join score overrides |
| `Main/Features/KingdomAllegiance/Models/AllegianceFactsReader.cs` | Clan and realm side pairs, player control |
| `Main/Adapters/AllegianceRelationAdapter.cs` | Lords and raw relations at new game |
| `Main/Adapters/FactionSides.cs` | `OfOwnClan`, a lord sided by their own clan |
| `tools/ingame_checks/1111.toml` | In-game check manifest |

## Dependencies

- `IAlignmentService` (Execution): sides from `alignment.json`
- `FactionSideRules.AreOpposed` (Execution): Free against Evil; Neutral never
- `IPlayerControlQuery` (CoopInterop): whether a human plays a clan or leads a realm
- `ICoopSessionProvider` (CoopInterop): a peer that defers to the host keeps vanilla
- `IOutcomeLog` (Core): the veto counter and the flip check line

## Tests

- `TAOM.Tests/Features/KingdomAllegiance/KingdomAllegianceServiceTests.cs`: the veto over the side matrix, the clan's own realm, player and doubt exclusions, co-op deferral, the switch, the counter; the flip over opposed, same-side and Neutral pairs, idempotence, the release of the lords (a fault included), the skip and pass lines.
- `TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs`: the switch is read through one lazy settings reference.
- `TAOM.Tests/Features/KingdomAllegiance/TaomDiplomacyModelAllegianceTests.cs`: both overrides return the veto.
- `TAOM.Tests/Features/KingdomAllegiance/KingdomAllegianceWiringTests.cs`: module, registrations, SubModule arguments, the postfix and its fault path, the MCM default.

## How to verify in game

**Preconditions:** Kingdom Allegiance on (default). The flip needs a new campaign; the veto works in any save.

1. Start a new campaign. The log shows `[Check] id=taom.kingdom-allegiance.friendships-flipped result=pass lords=N opposed=N flipped=N`. In the encyclopedia, no Gondor, Rohan or Elf lord lists a Mordor, Isengard or orc lord as a friend.
2. Run the campaign at speed for 60 or more days. Gondor's, Rohan's and each Free realm's clan list holds no Evil clan, and the reverse. The `[Session] counter id=taom.kingdom-allegiance.join-vetoed` line shows how many scores were vetoed.

## Known Limits

- **Clans that already serve stay.** Only new joins, defections and hires are vetoed (Mike, 2026-11-10). In an old save, an orc clan already inside Gondor stays until vanilla's own leave check takes it out.
- **The player is free.** The player's clan and a realm the player leads keep vanilla's scores, so the player can still recruit an orc lord into a Free realm the player rules, and an Evil realm can still take the player as a vassal. A player who is only a vassal cannot talk an opposed lord into the AI-led realm: that is an AI clan joining an AI-led realm, so the lord refuses. Sworn Anew (prisoners) and wanderer hiring keep their own alignment gates. Sworn Anew sides a lord by their realm (`FactionSides.OfHero`), this feature by their own clan.
- **Clans moved without a score.** The vanilla start scenarios Last Stand and the invasions (offered only while War of the Ring is off, `TaomStartOptionsProvider`) move AI clans with `AdvancedStartWorldOptionsCampaignBehavior.ChangeKingdomInternal`, which reads no score. So do the `campaign.create_clan` and SandBox mercenary cheats, and two StoryMode quests (StoryMode campaigns are hidden in TAOM).
- **Relations after the start are vanilla.** Battles, executions and other deeds can still make a Free lord like an Evil one later.
- **Co-op:** Coop runs `CharacterRelationCampaignBehavior.RegisterEvents` on the server only (`DisableCharacterRelationCampaignBehavior`), so the roll and Patch145 run at the host's new game, and Coop sends each relation write to the clients. Coop also turns off `DiplomaticBartersBehavior` on every peer, so in a Coop session AI clans never weigh joins and the veto meets only a player's recruit barter, which the host re-scores. Not exercised in a co-op session.
- **Unverified in game.** The [manifest](../../tools/ingame_checks/1111.toml) lists the checks.
