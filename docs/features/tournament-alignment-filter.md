# Tournament Alignment Filter

## Overview

A tournament in a town of the Free Peoples seats no lord, wanderer or troop of the Shadow, and a tournament of the Shadow seats none of the Free Peoples. Each entrant from the other side is replaced by one of the host town's own troops, so the bracket stays full. Neutral lords and wanderers, the troops the town itself recruits, the player and the player's clan always compete. One MCM switch, on by default.

## Why This Exists

- **Vanilla behavior:** `FightTournamentGame.GetParticipantCharacters` (v1.5.4) fills the 16 slots in order from the player (when entering), the leaders of the parties in town, partyless lords, partyless wanderers, the player's clan heroes in those parties, tier 3 to 5 garrison troops, and finally the host culture's troop tree. None of those steps checks culture or side.
- **TAOM requirement:** a player reported Mordor orcs competing in a Minas Tirith tournament (#744). An orc wanderer in the tavern or a Mordor lord visiting during a peace both qualify under vanilla's rules. The Old Realms (Warhammer) answered the same report by disabling tournaments; TAOM keeps them and keeps the other side out.
- **Without this feature:** lore-breaking brackets, and Shadow lords winning Free tournaments off-screen.

## Architecture

### Design Challenge

The roster must stay exactly 16 entries with no nulls: `TournamentBehavior.CreateParticipants` copies it into a fixed 16-slot array, and `TournamentMatch.AddParticipant` NREs on a null slot before the tournament opens. The method has six direct callers: `GetMenuText` and `GetTournamentPrize` (the join menu, the `TournamentGame` constructor, game load and the off-screen prize), `TournamentManager.ResolveTournament` (off-screen resolution), `HeroKnownInformationCampaignBehavior.OnPlayerJoinedTournament`, and SandBox's `TournamentBehavior.CreateParticipants` (the bracket the player fights) and `GetAllPossibleParticipants` (the preload). So the filter has to apply wherever the roster is read, not only when the player enters.

### Solution Approach

The filter rides `Patch69_TournamentRosterGuard`, the existing postfix on `GetParticipantCharacters` that already replaces entrants in place with the host culture's `EliteBasicTroop ?? BasicTroop`. `TournamentAlignmentPass` runs first, with its own catch, and the crash guard runs after it on the updated roster, so a fault in the alignment pass can never skip the guard, and the filler is guard-checked too. With no filler troop the pass changes nothing.

The rule, in `TournamentAlignmentFilterService` over the `execution/alignment.json` table (`IAlignmentService.ResolveSide`, which tries an id and falls back to a culture when the id is unlisted or Neutral):

| Who | Side read from |
|---|---|
| Host town | Its owner: `Settlement.MapFaction` (the owner kingdom, or the owner clan when it has none), else that owner's own culture. A captured town follows its conqueror, a town held by a Neutral realm bars nobody, and a player-founded kingdom (unlisted) takes its own culture's side |
| Hero entrant | The kingdom of the hero's clan, else the hero's culture. A Gondor-born lord of Mordor reads Evil |
| Troop entrant | Its culture |

An entrant is replaced only when both sides are Free or Evil and they differ. Never replaced:

- Neutral heroes. The Neutral realms are Khand (`battania`), Umbar, Shaghana and Abanissa. Dunland (`empire`) is Evil in the table. Khand's troops are `khuzait` and the Harad realms' are `aserai`, both Evil, so only their lords and wanderers are Neutral.
- Anyone in a town whose owner is Neutral.
- The player and the player's clan.
- An entrant with no culture and no kingdom (vanilla stands).
- A troop of the town's own culture, or of the culture of its basic or elite troop. Vanilla pads from the basic troop's upgrade tree and then the basic and elite troops, and Patch69 fills with the elite troop (else the basic one), so these are exactly the troops a town puts in its own bracket; without the exemption Mordor-held Minas Tirith would bar its Gondor filler and replace it with itself. The troop ids are needed because seven cultures borrow another culture's troops: Lothlorien from `rivendell`, Khand from `khuzait`, Shaghana and Abanissa from `aserai`, Umbar's basic troop from `aserai` (its elite troop is its own), and the two goblin realms from `goblin`. A hero of those cultures is still judged by side.

`IAlignmentService.AreEnemyAlignments` is not used, because it treats Neutral as everyone's enemy; this is the reading WandererAllegiance and MarriageAlignment give the table.

### Component Diagram

```
FightTournamentGame.GetParticipantCharacters
        |  postfix
Patch69_TournamentRosterGuard
        |-- TournamentEntrantMapper (CharacterObject -> TournamentEntrant; Settlement -> TournamentHost; filler)
        |-- TournamentAlignmentPass --> TournamentAlignmentFilterService --> IAlignmentService
        |                                         \--> ITournamentAlignmentSettingsProvider (MCM)
        \-- ITournamentRosterGuardService (crash guard, after the pass)
```

## Configuration

### MCM: Tournaments, "Keep Enemy Sides Out"

| Setting | Default | Effect |
|---|---|---|
| `TournamentKeepEnemySidesOut` | on | Off restores vanilla's roster. Read live, no restart |

The sides themselves come from `Main/_Module/ModuleData/execution/alignment.json`, shared with every alignment feature. The setting is simulation-relevant for co-op (it changes who wins off-screen tournaments) and is counted in `SettingsFingerprint`. On a co-op host, `Clan.PlayerClan` is the host's clan only, so a peer's clan lords are judged like any other lord (unverified in game).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Arena/TournamentAlignmentFilterService.cs` | The rule: which roster indices are barred |
| `Main/Features/Arena/TournamentAlignmentPass.cs` | Boundary: applies the swap to the engine roster, logs it |
| `Main/Features/Arena/ITournamentAlignmentSettingsProvider.cs`, `TournamentAlignmentSettingsProvider.cs` | The MCM switch, read live |
| `Main/Features/Arena/TournamentEntrant.cs` | `TournamentEntrant` (kingdom, culture and player-clan fields) and `TournamentHost` |
| `Main/Features/Arena/TournamentEntrantMapper.cs` | Engine types to entrant and host; the filler troop |
| `Main/Features/Arena/Hooks/Patch69_TournamentRosterGuard.cs` | The seam: alignment pass, then crash guard |
| `Main/Features/Arena/ArenaIoC.cs` | Registration |
| `Main/Features/TaomSettings.cs` | The "Keep Enemy Sides Out" toggle |

## Dependencies

- `IAlignmentService` (Execution): the Free, Evil and Neutral table.

## Tests

- `TAOM.Tests/Features/Arena/TournamentAlignmentFilterServiceTests.cs`: 26 tests over the real `AlignmentService`, covering both directions, Neutral heroes, Neutral and unlisted owners, kingdomless owners, the player-clan exemption, kingdom over culture for heroes, captured towns, the town-culture and borrowed-troop exemptions (Khand's and Umbar's shapes), fail-open on missing data, the switch and its compiled default, and `Describe`.
- `TAOM.Tests/Features/Arena/ArenaWiringTests.cs`: the filter's graph closes across `ArenaIoC` and `ExecutionIoC`.

In game: a Minas Tirith tournament with an orc wanderer or a Mordor lord present at peace shows no Evil entrants, and the log carries `[TournamentDiag] <town>: N of the other side replaced with <troop>: ...`.

## How to move a culture or kingdom to the other side

1. Edit its row in `Main/_Module/ModuleData/execution/alignment.json` (`free`, `evil` or `neutral`).
2. Restart Bannerlord. `AlignmentService` is a singleton that reads the file once, so a new campaign or a save load does not pick up the edit.
3. The change also moves executions, recruitment, desertion, prisoner morale, marriage, wanderer hiring, caravans, diplomacy, momentum and the realm-borders alignment map, which read the same table. No code change is needed.

## Consequences

A replaced lord becomes a troop, so the join menu's "N lords present" gossip, the prize tier and the Tournament Rewards renown and influence all count one hero fewer. That matches the field. Off-screen, a barred lord cannot win, gain the leaderboard entry or the prize. A barred hero is also not marked known to the player when the player joins.

## Changelog

- 2026-10-06: added (#744).

## GitHub Issue

- **Issue:** #744: [Tournaments keep Free and Evil entrants apart (no orcs at Minas Tirith)](https://github.com/haterade22/TAOM/issues/744)
- **Status:** Open
