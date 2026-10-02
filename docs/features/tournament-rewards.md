# Tournament Rewards

## Overview

Tournaments pay more, and the player chooses how. A **bet cap** set in MCM (unlimited by default) replaces
vanilla's 150 per round. A winner's **renown and influence** grow with the lords and heroes in the field and with
the winner's culture. At **Join**, the player picks one of **three prizes** and the **combat skill** the
tournament trains, earning experience in it for every round won. Designed with Mike on 2026-10-02.

## Why This Exists

- **Vanilla behavior:** a bet is capped at 150 a round (300 with Roguery's Deep Pockets); a win is worth 3
  renown and 0 influence whatever the field; the prize is one random item; and a tournament the player fights
  trains no skill directly (vanilla's 500 XP to a random skill reaches only lords in off-screen tournaments,
  `TournamentManager.SimulateTournament`).
- **TAOM requirement:** tournaments are a main road of player development (Mike), so they should reward the
  player's choices, scale with how grand the tournament is, and reflect who values glory in Middle-earth:
  the Rohirrim, Gondor's court, Dale, and the orc hosts.
- **Without this feature:** big tournaments pay the same as empty ones, a betting player hits a 150 ceiling,
  and a tournament fought with a polearm trains nothing in particular.

## Architecture

### Design Challenge

- `TournamentBehavior.GetMaximumBet` returns a `const` 150 from a non-virtual method; no model exposes it.
- `TournamentModel.GetRenownReward(Hero, Town)` and `GetInfluenceReward(Hero, Town)` are asked by town only,
  twice: at the award (`TournamentCampaignBehavior.OnTournamentFinished`) and again for the winner panel
  (`TournamentGame.TournamentWinRenown`). Neither call carries the participants.
- `GetSkillXpGainFromTournament` is never called for a tournament the player plays or watches.
- `TournamentGame.Prize` has a private setter, and the engine's prize roll is not virtual.
- The prize is handed over inside `TournamentBehavior.EndCurrentMatch`, beside the exit-hang and winner-panel
  code Patch60, Patch62 and Patch69 guard, so no choice is offered there.

### Solution Approach

A feature module (`TournamentRewardsModule`, registered in `FeatureModules.All`) owns the services, Patch96 and
one campaign behavior. The renown and influence overrides live on the Arena feature's `TaomTournamentModel`,
which reaches the reward service through `ITournamentService`.

| Piece | What it does |
|---|---|
| `Patch96_TournamentMaxBet` | Postfix on `TournamentBehavior.GetMaximumBet`: 0 in MCM means the 50,000,000 safety ceiling; any other cap keeps vanilla's perk factor (Deep Pockets doubles it) |
| `Patch96_TournamentFinishedHeroCount` | Prefix on `TournamentCampaignBehavior.OnTournamentFinished`: notes the town and its hero count before vanilla asks the model, so the award and the winner panel agree |
| `TaomTournamentModel.GetRenownReward` / `GetInfluenceReward` | Vanilla's answer (perks included) plus the hero bonus, times the winner's culture factor and the MCM multiplier; influence only in a town of the winner's own kingdom |
| `Patch96_TournamentJoinChoices` | Prefix on the private "Join" consequence: shows the prize dialog, then the skill dialog, then re-enters vanilla's whole Join through a thread-static bypass. Closing a dialog joins nothing |
| `TournamentRewardsBehavior` | `TournamentFinished` (a player win: four rounds plus the bonus) and `PlayerEliminatedFromTournament` (the rounds won before it) pay the chosen skill and print the gain |

**The numbers** (`TournamentRewardRules`):

| Reward | Formula |
|---|---|
| Bet cap per round | MCM value, 0 = unlimited (50,000,000); Deep Pockets doubles a finite cap |
| Renown | (vanilla + 1 per hero) x winner's culture factor x MCM multiplier |
| Influence | in the winner's kingdom's own town: (vanilla + 2 + 1 per 4 heroes) x culture x MCM; elsewhere vanilla |
| Skill XP | 125 per round won + 250 for the win (750 for a win), x the player's culture factor |

**The 50,000,000 ceiling:** a won bet pays odds of up to 4 and the four rounds' payouts add up in an `int`
(`TournamentBehavior.OverallExpectedDenars`), so a cap above about 134 million per round overflows it.

**The three prizes** (`TournamentPrizeRules.PickChoices`): the advertised prize first, then two alternatives
from its own band (a heavy prize means a big tournament's pool, [arena.md](arena.md#prize-pools)), drawn with a
seed of the town and the tournament's creation time. Reopening the menu, even after a reload, offers the same
three. The pick becomes `TournamentGame.Prize`, so the mission's prize display and the award both carry it, and
an AI winner is paid its value in gold as before.

**Player only, every winner:** the renown and influence rules apply to AI winners too (Mike's choice); the
dialogs and the skill XP are the local player's. Nothing runs the skill award on a dedicated server, whose main
hero is an idle world-gen hero.

### Component Diagram

```
tournament_rewards.json ── TournamentRewardsConfigProvider ─┐
MCM "Tournaments" ──────── TournamentRewardsSettingsProvider ┤
                                                             v
Patch96 max bet ──────────────────────────────> TournamentRewardsService <── TournamentService (Arena)
Patch96 hero count ───────────────────────────>   (rules, hero count,          ^
                                                   skill choice per town)      | RenownReward / InfluenceReward
TournamentRewardsBehavior ─> TournamentSkillAwardService ─┘                TaomTournamentModel
Patch96 join choices ─> TournamentJoinService ─> ITournamentJoinAdapter (prize setter)
                                              └> ITournamentChoicePresenter (dialogs, message)
```

## Configuration

### MCM: "Tournaments"

| Setting | Default | Meaning |
|---|---|---|
| Max Bet Per Round | 0 | 0 = unlimited (up to the purse); vanilla is 150 |
| Renown Multiplier | 1.00 | scales every winner's renown |
| Influence Multiplier | 1.00 | scales every winner's influence in their own kingdom's towns |

MCM keeps a value once saved, so a changed default never reaches an existing install: rename the property to
change one ([orientation trap](../ai-includes/orientation.md), "Persisted MCM defaults").

### Config File: `Main/_Module/ModuleData/tournament_rewards/tournament_rewards.json`

| Field | Type | Description |
|---|---|---|
| `cultures.<id>.renown` | float, 0 to 10 | the winner's culture's renown factor |
| `cultures.<id>.influence` | float, 0 to 10 | the winner's culture's influence factor |
| `cultures.<id>.skill_xp` | float, 0 to 10 | the player's culture's skill XP factor |

Keys are culture StringIds (Rohan is `vlandia`, Dale `sturgia`). A row's missing field inherits the `default`
row. A bad value reverts with a warning; read once per process.

### Current Values

| Culture | Renown | Influence | Skill XP | Why |
|---|---|---|---|---|
| Rohan (`vlandia`) | 1.5 | 1.25 | 1.0 | horse-lords; deeds of arms make a name |
| `gondor` | 1.25 | 1.5 | 1.10 | courtly nobility; standing at court |
| Dale (`sturgia`) | 1.25 | 1.0 | 1.0 | festival and games culture |
| `mordor`, `isengard`, `gundabad`, `goblin`, `mistymountainorcs`, `dolguldur` | 1.5 | 1.0 | 1.40 | might makes right; orcs live to fight |
| everyone else (`default`) | 1.0 | 1.0 | 1.0 | |

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/TournamentRewards/TournamentRewardRules.cs` | Pure arithmetic: bet cap, renown, influence, skill XP, the combat skill list |
| `Main/Features/TournamentRewards/TournamentRewardsService.cs` | Settings and culture factors applied; hero count and skill choice state |
| `Main/Features/TournamentRewards/TournamentJoinService.cs` | The Join flow: prize dialog, skill dialog, then vanilla's Join |
| `Main/Features/TournamentRewards/TournamentSkillAwardService.cs` | Pays the chosen skill when the player's tournament ends |
| `Main/Features/TournamentRewards/TournamentRewardsBehavior.cs` | Campaign events into the award service; session reset |
| `Main/Features/TournamentRewards/TournamentRewardsConfigProvider.cs`, `TournamentRewardsCatalog.cs` | The culture factor file and its validation |
| `Main/Features/TournamentRewards/TournamentRewardsSettingsProvider.cs` | The MCM group |
| `Main/Features/TournamentRewards/Hooks/Patch96_TournamentRewards.cs` | The three patches |
| `Main/Features/TournamentRewards/TournamentRewardsModule.cs` | Services, patch category (GameInit) and behavior |
| `Main/Features/Arena/TournamentPrizeRules.cs` | `PickChoices`: the seeded three prizes |
| `Main/Adapters/ITournamentJoinAdapter.cs`, `TournamentJoinAdapter.cs` | The current tournament, and its private `Prize` setter |
| `Main/Adapters/ITournamentChoicePresenter.cs`, `TournamentChoicePresenter.cs` | The two dialogs and the XP message |

## Dependencies

- `ITournamentService` (Arena): the prize pools and the model's path to the reward service.
- `IArmourGateService` (ArmourAcquisition): prize classes, through the Arena service.
- `IHeroSkillXpAdapter` (Adapters): `HeroDeveloper.AddSkillXp`.
- `IDedicatedServerProvider` (CoopInterop): no skill award on a dedicated server.

## Tests

- `TournamentRewardRulesTests`: every formula, the ceiling, NaN and negative factors.
- `TournamentRewardsServiceTests`: hero count per town, culture and multiplier, own-kingdom influence, the
  skill choice's life, the session reset.
- `TournamentJoinServiceTests`: dialog order, cancels, re-validation of each pick, the skips.
- `TournamentSkillAwardServiceTests`: win, elimination, dedicated server.
- `TournamentRewardsConfigProviderTests`, `ShippedTournamentRewardsConfigTests`: one test per validation rule;
  the shipped factors and their culture ids.
- `TournamentPrizeRulesTests` (Arena): `PickChoices`.
- `TournamentRewardsBindingTests` (RequiresGame): Patch96's targets, parameter names and the `Prize` setter.
- `CoopVetoClassificationTests`: the Join prefix is classed ReviewedSafe for co-op.

## How to Change a Culture's Rewards

1. Edit its row in `tournament_rewards.json` (add one keyed by the culture's StringId if it has none).
2. Run `dotnet test TAOM.Tests --filter FullyQualifiedName~ShippedTournamentRewardsConfigTests`; it pins the
   approved values, so update the expected row there in the same change.
3. Restart the game. No code changes.

## Changelog

- 2026-10-02: Added. MCM bet cap (default unlimited), renown and influence scaled by heroes and the winner's
  culture, the prize and skill chosen at Join, skill XP per round won with a culture factor.

## GitHub Issue

- **Issue:** not filed yet (drafted for Mike).
- **Status:** built, in-game check owed.
