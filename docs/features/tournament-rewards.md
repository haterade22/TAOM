# Tournament Rewards

## Overview

Tournaments pay more, and the player chooses how. A **bet cap** set in MCM (unlimited by default) replaces
vanilla's 150 per round. A winner's **renown and influence** grow with the lords and heroes in the field and with
the winner's culture. At **Join**, the player picks one of up to **three prizes** and the **combat skill** the
tournament trains, earning experience in it for every round won. Designed with Mike on 2026-10-02.

## Why This Exists

- **Vanilla behavior:** a bet is capped at 150 a round (300 with Roguery's Deep Pockets); a win is worth 3
  renown and, from the model, 0 influence whatever the field (the mission itself adds 1 influence for a player
  who serves a kingdom he does not rule, `TournamentBehavior.OnPlayerWinTournament`); the prize is one random
  item; and a tournament the player fights trains only the skills his hits use, at a third of the battle rate
  (`TournamentFightMissionController.OnScoreHit`; `DefaultCombatXpModel`, Tournament 0.33), while the model's
  500 XP to a random skill reaches only heroes (lords and wanderers) in off-screen tournaments
  (`TournamentManager.SimulateTournament`).
- **TAOM requirement:** tournaments are a main road of player development (Mike), so they should reward the
  player's choices, scale with how grand the tournament is, and reflect who values glory in Middle-earth:
  the Rohirrim, Gondor's court, Dale, and the orc hosts.
- **Without this feature:** big tournaments pay the same as empty ones, a betting player hits a 150 ceiling,
  and a tournament fought with a polearm trains Polearm only through its hits, at a third of the battle rate.

## Architecture

### Design Challenge

- `TournamentBehavior.GetMaximumBet` returns a `const` 150 from a non-virtual method; no model exposes it.
- `TournamentModel.GetRenownReward(Hero, Town)` and `GetInfluenceReward(Hero, Town)` are asked by town only,
  twice per tournament: at the award (`TournamentCampaignBehavior.OnTournamentFinished`) and again for the winner
  panel (`TournamentGame.TournamentWinRenown`). Neither call carries the participants. `GetRenownReward` has a third
  caller: a new campaign seeds the tournament leaderboard with 500 past wins and asks once per win with a null town
  (`TournamentCampaignBehavior.InitializeTournamentLeaderboard`); TAOM answers vanilla's renown there, so starting
  renown does not move with the culture factors or the MCM multiplier.
- `GetSkillXpGainFromTournament` is never called for a tournament the player plays or watches.
- `TournamentGame.Prize` has a private setter, and the prize roll is `FightTournamentGame`'s protected override of
  the abstract `TournamentGame.GetTournamentPrize`, which only a new `TournamentGame` subclass (a new saved type)
  could replace.
- The prize is handed over inside `TournamentBehavior.EndCurrentMatch`, beside the exit-hang and winner-panel
  code Patch60, Patch62 and Patch69 guard, so no choice is offered there.

### Solution Approach

A feature module (`TournamentRewardsModule`, registered in `FeatureModules.All`) owns the services, Patch96 and
one campaign behavior. The renown and influence overrides live on the Arena feature's `TaomTournamentModel`,
which reaches the reward service through `ITournamentService`.

| Piece | What it does |
|---|---|
| `Patch96_TournamentMaxBet` | Postfix on `TournamentBehavior.GetMaximumBet`: 0 in MCM means the 50,000,000 safety ceiling; any other cap keeps vanilla's perk factor (Deep Pockets doubles it) |
| `TaomTournamentModel.GetRenownReward` / `GetInfluenceReward` | Vanilla's answer (perks included) plus the hero bonus, times the winner's culture factor and the MCM multiplier; influence only in a town of the winner's own kingdom |
| `Patch96_TournamentJoinChoices` | Prefix on the private "Join" consequence: shows the prize dialog, then the skill dialog, then re-enters vanilla's whole Join through a thread-static bypass. Closing a dialog joins nothing |
| `TournamentRewardsBehavior` | `TournamentFinished` first notes the town and its hero count, before vanilla's handler asks the model, so the award and the winner panel agree (the listener order is the engine's, traced on the behavior), then pays the chosen skill for a player win (four rounds plus the bonus); `PlayerEliminatedFromTournament` pays the rounds won before it; one line then names the trained skill, with no number. A fault in the `TournamentFinished` listener is logged and never skips vanilla's award |

**The numbers** (`TournamentRewardRules`):

| Reward | Formula |
|---|---|
| Bet cap per round | MCM value, 0 = unlimited (50,000,000); Deep Pockets doubles a finite cap |
| Renown | (vanilla + 1 per hero) x winner's culture factor x MCM multiplier |
| Influence | in the winner's kingdom's own town: (vanilla + 2 + 1 per 4 heroes) x culture x MCM; elsewhere vanilla; plus vanilla's unscaled +1 for a player who serves a kingdom he does not rule, which the winner panel does not show |
| Skill XP | 125 per round won + 250 for the win (750 for a win), x the player's culture factor |

**The 50,000,000 ceiling:** a won bet pays odds of up to 4 and the four rounds' payouts add up in an `int`
(`TournamentBehavior.OverallExpectedDenars`), so a cap above about 134 million per round overflows it. At the
default of 0 the tournament screen's own wager text ("you can wager {MAX_AMOUNT} each time", filled from the cap
at `TournamentVM.cs:922`) therefore shows 50,000,000 as the per-round limit. Rewording vanilla's text would need
another patch, so it stays.

**The three prizes** (`TournamentPrizeRules.PickChoices`): the advertised prize first, then up to two
alternatives from its own band, drawn with a seed of the town and the tournament's creation time. A heavy prize
means a big tournament's pool ([arena.md](arena.md#prize-pools)); an elite, lord or named prize, which an old save
or vanilla's fallback list can still advertise, draws that pool too (`TournamentPrizeRules.AdvertisedBand`, Mike,
2026-10-04). The alternatives come from anywhere in the band, not from the value slice vanilla rolls the advertised
prize from (a quarter of the regular band by hero count, a half of the elite band below or from ten heroes), so a
small tournament often offers a dearer pick than its size suggests: kept on purpose, tournaments pay more (Mike,
2026-10-04). Reopening the menu, even after a reload, offers the same
three while the advertised prize stands; vanilla re-rolls that prize at the join menu when lords arrive or leave
(`TournamentCampaignBehavior.game_menu_tournament_join_on_init`), and the alternatives follow it. The pick becomes
`TournamentGame.Prize`, so the mission's prize display and the award both carry it, and an AI winner is paid its
value in gold as before.

**Player only, every winner:** the renown and influence rules apply to AI winners too (Mike's choice); the
dialogs and the skill XP are the local player's. Nothing runs the skill award on a dedicated server, whose main
hero is an idle world-gen hero.

### Component Diagram

```
tournament_rewards.json ── TournamentRewardsConfigProvider ─┐
MCM "Tournaments" ──────── TournamentRewardsSettingsProvider ┤
                                                             v
Patch96 max bet ──────────────────────────────> TournamentRewardsService <── TournamentService (Arena)
TournamentRewardsBehavior (count) ────────────>   (rules, hero count,          ^
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

The multipliers run from 0 to 5 (`TournamentRewardRules.MaxMultiplier`) and the bet cap from 0 to 1,000,000. MCM's
slider enforces that range but its settings file loader does not, so `TournamentRewardsSettingsProvider` clamps a
hand-edited value to it, and the rules refuse a multiplier above 5 or a result past what an `int` holds (renown and
influence fall back to vanilla's answer, skill XP to the unscaled award).

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
| `mordor`, `isengard`, `gundabad`, `goblin`, `mistymountainorcs`, `bluecraig`, `dolguldur` | 1.5 | 1.0 | 1.40 | might makes right; orcs live to fight |
| everyone else (`default`) | 1.0 | 1.0 | 1.0 | |

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/TournamentRewards/TournamentRewardRules.cs` | Pure arithmetic: bet cap, renown, influence, skill XP, the combat skill list |
| `Main/Features/TournamentRewards/TournamentRewardsService.cs` | Settings and culture factors applied; hero count and skill choice state |
| `Main/Features/TournamentRewards/TournamentJoinService.cs` | The Join flow: prize dialog, skill dialog, then vanilla's Join |
| `Main/Features/TournamentRewards/TournamentSkillAwardService.cs` | Pays the chosen skill when the player's tournament ends |
| `Main/Features/TournamentRewards/TournamentRewardsBehavior.cs` | Campaign events into the services: the hero count, the skill award, the session reset |
| `Main/Features/TournamentRewards/TournamentRewardsConfigProvider.cs`, `TournamentRewardsCatalog.cs` | The culture factor file and its validation |
| `Main/Features/TournamentRewards/TournamentRewardsSettingsProvider.cs` | The MCM group, clamped to the sliders' ranges |
| `Main/Features/TournamentRewards/Hooks/Patch96_TournamentRewards.cs` | The two patches (bet cap, Join choices) |
| `Main/Features/TournamentRewards/TournamentRewardsModule.cs` | Services, patch category (GameInit) and behavior |
| `Main/Features/Arena/TournamentPrizeRules.cs` | `PickChoices`: the seeded three prizes; `AdvertisedBand`: which band they come from |
| `Main/Adapters/ITournamentJoinAdapter.cs`, `TournamentJoinAdapter.cs` | The current tournament (its advertised prize and that prize's engine tier included), and its private `Prize` setter |
| `Main/Adapters/ITournamentChoicePresenter.cs`, `TournamentChoicePresenter.cs` | The two dialogs and the skill-trained message (no number: the engine scales the XP by the learning rate) |

## Dependencies

- `ITournamentService` (Arena): the prize pools and the model's path to the reward service.
- `IArmourGateService` (ArmourAcquisition): prize classes, through the Arena service.
- `IHeroSkillXpAdapter` (Adapters): `HeroDeveloper.AddSkillXp`.
- `IDedicatedServerProvider` (CoopInterop): no skill award on a dedicated server.

## Tests

- `TournamentRewardRulesTests`: every formula, the ceiling, NaN, negative and out-of-range factors and
  multipliers, and the three int casts.
- `TournamentRewardsServiceTests`: hero count per town, culture and multiplier, own-kingdom influence, vanilla's
  renown for a null town (the leaderboard seeding), the skill choice's life, the session reset.
- `TournamentJoinServiceTests`: dialog order, cancels, re-validation of each pick, the skips, the prize's tier
  passed to the Arena service.
- `TournamentSkillAwardServiceTests`: win, elimination, dedicated server.
- `TournamentRewardsConfigProviderTests`, `ShippedTournamentRewardsConfigTests`: one test per validation rule
  (the empty key and the repeated key included); the shipped factors and their culture ids.
- `TournamentRewardsSettingsProviderTests`: the fallbacks against the compiled MCM defaults, the slider ranges
  against the rules' range, and the clamp on an out-of-range or NaN setting.
- `TournamentPrizeRulesTests` (Arena): `PickChoices`, and `AdvertisedBand` over all seven armour classes, no class
  and an unknown tier.
- `TournamentRewardsWiringTests`: the module listed once, its patch category and behavior declarations, and a real
  DryIoc graph (Arena's `TournamentService` included) that shares one `TournamentRewardsService`; the behavior's
  session reset and hero count by source pin.
- `TournamentRewardsBehaviorTests` (RequiresGame): a fault in the `TournamentFinished` listener is logged, not thrown.
- `Patch96TournamentMaxBetTests`: the bet postfix applies the cap, and on a fault keeps vanilla's with one warning.
- `TournamentRewardsBindingTests` (BindingVerification): Patch96's targets, parameter names and category, the
  `Prize` setter (also a `ReflectionSiteBindingTests` row), and the one link of the `TournamentFinished` listener
  order a test can pin: an `MbEvent` runs its last-registered listener first.
- `CoopVetoClassificationTests`: the Join prefix is classed ReviewedSafe for co-op.

## How to Change a Culture's Rewards

1. Edit its row in `tournament_rewards.json` (add one keyed by the culture's StringId if it has none).
2. Run `dotnet test TAOM.Tests --filter FullyQualifiedName~ShippedTournamentRewardsConfigTests`; it pins the
   approved values, so update the expected row there in the same change.
3. Restart the game. No code changes.

## Changelog

- 2026-10-04: reviewed after it shipped in the v2.0.33 testing build (its code is in the v2.0.34 tag too) and fixed
  ([rca-tournament-rewards-2026-10-04.md](../reviews/rca-tournament-rewards-2026-10-04.md)). Mike's decisions: Blue
  Craig gets the orc factors; the new-game leaderboard keeps vanilla's renown; the prize dialog offers "a choice of
  prizes" and the skill message names the skill without a number; an elite, lord or named prize draws
  heavy-band alternatives; the alternatives keep drawing from the whole band.
- 2026-10-02: Added. MCM bet cap (default unlimited), renown and influence scaled by heroes and the winner's
  culture, the prize and skill chosen at Join, skill XP per round won with a culture factor.

## GitHub Issue

- **Issue:** #732, [Tournament Rewards](https://github.com/haterade22/TAOM/issues/732); the dialog strings' translations: #731
- **Status:** shipped in the v2.0.33 testing build, tagged in v2.0.34; reviewed 2026-10-04; #732 closed with
  `triage-needs-ingame`, the in-game checks it lists still owed
