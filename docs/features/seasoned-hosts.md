# Seasoned Hosts

## Overview

Seasoned Hosts (#890) is the XP economy of AI lords. AI lord parties train their troops faster, AI lords' skills climb faster, the learning rate never falls to zero past a skill's learning limit, and a switch removes the Steward XP a finished settlement project grants. Every number is an MCM setting under "World/Seasoned Hosts", and the player's own clan never gets a bonus.

## Why This Exists

TAOM's troop trees run to tier 10, so the same daily training XP buys far fewer promotions than it does in vanilla. An AI lord has a hard time fielding the top tiers without the player's help, and the AiPartySize relief only fixes numbers, not quality.

- **Vanilla behavior:** a lord party's troops earn `10 + 2 x tier` XP a day (`15 + 3 x tier` for a clan leader), plus perk XP. A skill's learning rate gets a factor of `-1 - 0.1 n` for a skill n points over its learning limit and is then cut off at 0: the rate falls 1.375 at the first point over (1.25 for the flat `-1`, 0.125 for the first 0.1) and 0.125 per point after, so a skill freezes `4a + 10f` points past the limit (a is the average of the skill's attributes, f the focus). A finished building project always grants 1000 Steward XP.
- **TAOM requirement:** a tunable, opt-out bonus for AI hosts that cannot reach the player's clan, a way to keep skills growing past the limit, and a way to drop the free Steward XP.
- **Without this feature:** AI armies stay green, and a hero past the limit stops learning entirely.

## Architecture

### Design Challenge

- The training and skill XP seams are GameModels, but the Steward XP grant has no model seam: it is one public call into `DefaultSkillLevelingManager`.
- "Not the player's clan" must hold under co-op, where a remote player's clan is not `Clan.PlayerClan`, and for the player's spouse and children, who are lords of the player's clan. A companion made a lord leaves the player's clan and does get the bonus.
- The learning rate signature carries no hero, so the floor cannot spare the player.

### Solution Approach

One pure service decides everything. Three campaign models and one Harmony prefix hand it facts read through an adapter, and apply the answer. The models inherit the `Default*` models, call base first, and then add one delegate call (`gamemodels.md` rule 4: no `if` in an override body). No behavior, no save data, no config file.

| Seam | What it does |
|---|---|
| `TaomGenericXpModel : DefaultGenericXpModel` | `GetXpMultiplier(hero)` is base times the service's skill XP multiplier. `HeroDeveloper.AddSkillXp` (`:198-222`) asks at `:208`, after `GainRawXp` (`:224-235`) has banked the raw XP toward the level, so the multiplier speeds skills and not the hero level. |
| `TaomPartyTrainingModel : DefaultPartyTrainingModel` | `GetEffectiveDailyExperience` and `CalculateXpGainFromBattles` call base, then add one factor to the `ExplainedNumber`. A factor of 0 is a no-op. The total is base plus flat adds times one plus the factors, so perk XP is scaled as well. The daily member then adds the career `TroopTraining` flat term (`ICareerPassiveService.ApplyFlat`, #769), after the factor because `ApplyFlat` divides out only the factors already on the number; the engine has one training slot, so this model holds both. |
| `TaomCharacterDevelopmentModel : DefaultCharacterDevelopmentModel` | `CalculateLearningRate` calls base, then `LimitMin(service.LearningRateFloor(rate.LimitMinValue))`. On, the minimum becomes 0.05; off, the same minimum is set again. |
| `Patch118_ProjectStewardXp` | A skipping prefix on `DefaultSkillLevelingManager.OnSettlementProjectFinished(Settlement)`: `return !service.SkipsProjectStewardXp();`. Category `Patch118_SeasonedHostsStewardXp`, applied at GameInit. |

Who is eligible, from `SeasonedHostsService`:

| Decision | True when |
|---|---|
| Skill XP multiplier | the hero is a lord, is not the main hero, the hero's clan reads `NotPlayer`, and this peer does not defer to the co-op host |
| Training factor | a lord party, not the main party, the owner's clan reads `NotPlayer`, the army leader's party reads `NotPlayer` (no army counts as `NotPlayer`), and this peer does not defer to the co-op host |
| Learning floor | the setting is on; the result is the larger of the current minimum and 0.05. It never asks about the co-op host |
| Steward skip | the setting is on and this peer does not defer to the co-op host |

"Reads" means `IPlayerControlQuery.OfClan` and `OfParty`. `Player` is this peer's own or a registered co-op player's; `Unknown` (a live session whose player registry is unreadable) counts as a player, so a doubtful answer is no bonus. The adapter asks no question for a non-lord, the main hero or the main party.

### Component Diagram

```
TaomSettings (MCM "World/Seasoned Hosts")
        |
 SeasonedHostsSettingsProvider (four validated properties, cached accessor)
        |
 SeasonedHostsService  <--  ICoopSessionProvider (ShouldDeferToHost)
     /        \
Models x3     Patch118 prefix
     \
 ISeasonedHostsAdapter --> IPlayerControlQuery (OfClan, OfParty)
 (Hero, MobileParty -> LordXpFacts, TrainingPartyFacts)
```

## Configuration

There is no config file: the four MCM settings are the whole configuration. Each is a property of `ISeasonedHostsSettingsProvider`, validated in `SeasonedHostsSettingsProvider`: a value that is not a finite number reverts to the compiled default (`SettingClamp`), and a number outside the range is clamped to it, so no setting can become a penalty. A hand-edited MCM json is covered by the same clamp. The result is rounded to the hundredth the slider shows. Before MCM is up, the compiled defaults apply.

| Setting | Property | Range | Default | Effect |
|---|---|---|---|---|
| AI Lord Party Training Bonus | `SeasonedHostsTrainingBonus` | 0 to 1.0 | 0.25 | Extra daily and battle training XP for eligible AI lord parties; 0 is vanilla |
| AI Lord Skill XP Multiplier | `SeasonedHostsSkillXpMultiplier` | 1.0 to 2.0 | 1.25 | Multiplier on an eligible AI lord's skill XP; 1 is vanilla |
| Learning Never Stops | `SeasonedHostsLearningNeverStops` | on or off | on | The learning rate never drops below 0.05, for every hero |
| No Steward XP From Finished Projects | `SeasonedHostsNoStewardXp` | on or off | off | A finished project grants no Steward XP, in every settlement |

All four apply without a restart and are simulation-relevant: they count toward the co-op settings fingerprint, and the server settings file can carry them. The labels and hints are `{=taom_seasoned_hosts_*}` keys. The training factor carries no explainer line: both engine consumers read only the result (`MobilePartyTrainingBehavior`, `MapEventParty`), so no screen would show one. The two bonus hints say that in a co-op session only the host's value applies, and that they are off for every player while a co-op mod TAOM cannot probe (BannerlordTogether) is enabled; the steward hint says the same for the skip.

## Co-op

- The models run where the campaign runs, on every peer, like every other model.
- The skill XP and training eligibility come from `IPlayerControlQuery`, so a remote player's clan, party and army read as human-driven.
- The two bonuses defer to the host: `SkillXpMultiplier` and `TrainingFactor` return the neutral answer while `ICoopSessionProvider.ShouldDeferToHost`, so a co-op client gets no bonus and the host's campaign is the one that applies it. They are also off on every peer, solo included, while a co-op mod TAOM cannot probe (BannerlordTogether) is enabled. `Unknown` from the control query counts as a player, so a doubtful registry is no bonus either way ([coop-interop.md](coop-interop.md)).
- The learning floor does not defer. `CalculateLearningRate` receives no hero, so it runs on every peer from that peer's own setting. It does shape replicated state: `HeroDeveloper.AddSkillXp` scales the skill XP it writes by the focus factor, which reads the learning rate. Under BannerlordCoop only the host writes skill XP, so the host's setting decides. Under a co-op mod TAOM cannot probe, each peer applies its own floor; whether that mod syncs skill XP is UNVERIFIED.
- The patch is co-op gated. A prefix that skips a replicated mutation (XP on a hero's developer) from a per-peer MCM value can desync two campaigns, so it is `GatedForCoop` in `CoopVetoClassificationTests`, and the service returns false while `ShouldDeferToHost`: a co-op client, and every peer when the co-op mod's role cannot be probed, runs vanilla. A solo player with no co-op mod and a BannerlordCoop host apply it; with BannerlordTogether enabled the skip is off for every peer, solo included.
- Settings are not exchanged between peers. Two peers with different values simulate differently; the settings fingerprint reports it, as for every simulation-relevant setting.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SeasonedHosts/SeasonedHostsModule.cs` | Module: the three model declarations, the patch category, the registrations |
| `Main/Features/SeasonedHosts/SeasonedHostsService.cs` | Every decision, no engine type |
| `Main/Features/SeasonedHosts/ISeasonedHostsSettingsProvider.cs`, `SeasonedHostsSettingsProvider.cs` | The four validated MCM values, one property each, cached accessor |
| `Main/Features/SeasonedHosts/Models/TaomGenericXpModel.cs` | Skill XP multiplier |
| `Main/Features/SeasonedHosts/Models/TaomPartyTrainingModel.cs` | Daily and battle training factor, plus the career `TroopTraining` flat term on the daily member |
| `Main/Features/SeasonedHosts/Models/TaomCharacterDevelopmentModel.cs` | Learning rate floor |
| `Main/Features/SeasonedHosts/Hooks/Patch118_ProjectStewardXp.cs` | Steward XP skip |
| `Main/Adapters/ISeasonedHostsAdapter.cs`, `SeasonedHostsAdapter.cs` | Hero and party to facts, through `IPlayerControlQuery` |
| `Main/Adapters/SeasonedHostsFacts.cs` | `LordXpFacts` and `TrainingPartyFacts` |
| `Main/Features/TaomSettings.cs` | The four settings, group "World/Seasoned Hosts" (GroupOrder 73) |

## Dependencies

- `IPlayerControlQuery` (CoopInterop): who drives a clan or party.
- `ICoopSessionProvider` (CoopInterop): `ShouldDeferToHost` for the two bonuses and the patch.
- `ISeasonedHostsAdapter` (Adapters): reads `Hero` and `MobileParty`.
- `ICareerPassiveService` (CareerSystem): the `TroopTraining` passive on the daily training member.

## Tests

- `SeasonedHostsSettingsProviderTests` (25): the compiled defaults with no settings, the MCM defaults against the fallback, pass-through, range clamps, non-finite values to the compiled default, the slider step, the group order 73 appearing once, and each property's group, restart flag, key prefix and slider range.
- `SeasonedHostsServiceTests` (27): the decision matrix for the multiplier (AI lord, main hero, player clan, `Unknown`, non-lord, setting 1, deferring), the training factor (lord party, main party, player-owned, player-led army, caravan or garrison, bonus 0, deferring), the floor (off, on, higher minimum, unset minimum, deferring still floors) and the steward skip (on, off, deferring).
- `SeasonedHostsAdapterTests` (5): null answers ask no one, control comes from the query and never from `Clan.PlayerClan`, and each cheap guard is present and comes before any query.
- `Patch118ProjectStewardXpTests` (5): through the module's `InitializeStatics`, the prefix returns false with the switch on and not deferring, true with it off, true when deferring, true with exactly one error log when the settings provider throws, and true with no service.
- `SeasonedHostsWiringTests` (16): module listed once, three campaign models on the right slots over their `Default*` bases, one patch category at GameInit, no behavior, not parked, no save data, no branch in any override body, the container validates, the XP and training model factories build their models, the development model factory reaches its constructor (vanilla's base constructor needs a campaign), and `InitializeStatics` hands the patch its service.
- `SeasonedHostsBindingTests` (11): the steward target resolves with its single overload, the category attaches one bool prefix and the JIT accepts it, each override matches a virtual of its base, and each engine consumer still calls the model member.
- `CampaignHotPathSettingsProvidersTests`: three rows for the provider (one lazy settings accessor, resolves from a real container, reads through the cached settings). `CoopVetoClassificationTests`: the `Patch118_ProjectStewardXp` entry, listed in the pinned gated set (`GatedVetoes_AreExactlyThePinnedSet`). `LearningFloorCalculatorTests`: vanilla's rate with a floor of 0 and of 0.05 gives the same marker level. `CoopPlayerControlQueryTests` and `CoopMissionBindingTests`: the registry probe compiles for the fake and for the installed BannerlordCoop. `PatchCategoryNumberTests` and the settings count tests read the new category, registry heading and settings.

## How to verify in game

**Preconditions:** a new campaign or an existing save; no restart is needed. Open MCM, TAOM, "World/Seasoned Hosts" and keep the defaults (training bonus 0.25, skill XP 1.25, Learning Never Stops on, No Steward XP off).

1. Set "AI Lord Skill XP Multiplier" to 2, note an AI lord's skill values in the encyclopedia, wait a few days near a war, and open the lord's page again. The skills that lord used (Leadership, Tactics, a weapon skill) rise faster than with the multiplier at 1.
2. Open the clan screen and note your own heroes' skill values over the same days. They rise at the vanilla rate: the multiplier never reaches your clan.
3. Pick a hero skill far past its learning limit (its tooltip on the character screen shows a learning rate of 0 in vanilla). With Learning Never Stops on, the rate reads 0.05 and the skill still gains XP; switch it off and the rate reads 0 again.
4. Turn on "No Steward XP From Finished Projects", give one of your fiefs a governor, and let a building project finish there. The governor gains no Steward XP from it; with the switch off, the game grants the governor 1000.
5. The training bonus has no screen: an AI lord party's tier mix climbs faster over weeks. A balance check compares an AI host's tiers after 60 days with the bonus at 0 and at 0.25.

## How to Change a Value

1. A default: change the constant in `SeasonedHostsSettingsProvider`. A persisted MCM value keeps its old number (json2), so a new default needs a renamed setting.
2. A range: change the constants; the slider attributes in `TaomSettings` read them, and a test pins that they match.
3. The floor: `SeasonedHostsService.LearningRateFloorValue` (0.05, a constant, not a setting).

## Balance Notes

- Skills only, not hero level. `AddSkillXp` banks the raw XP toward the level before it asks for the multiplier, so the skill XP multiplier makes skills climb faster and does not make lords level up faster.
- The floor applies to every hero, the player included, because `CalculateLearningRate` receives no hero. Past the limit the player's rate is 0.05 where vanilla's is 0, a slow trickle. The Loremaster's Line marker does not move: past the limit the rate falls 0.125 per point, so the first level at or under 0.05 is the level where vanilla's rate reached 0, and only the band tooltip text changes ([loremasters-line.md](loremasters-line.md)). Enlistment's Leadership gates only get easier ([enlistment.md](enlistment.md)).
- The training bonus is a factor on the total, so it scales the perk XP and a clan leader's higher base alike. With +25%, a tier 5 troop earns 25 XP a day instead of 20 in a party not led by its own clan's leader, and 37.5 instead of 30 in a party led by its own clan's leader, before perks.
- The training bonus also raises AI lords' Leadership. A troop that cannot absorb all of a battle's XP hands the excess to the party's leader as 2.5% Leadership XP (`DefaultSkillLevelingManager.OnBattleEnded`, reached from `MapEventParty.CommitXpGain`), so more battle XP means more excess, and the skill XP multiplier compounds it.
- The Steward skip also removes the XP from the player's own governors, because the grant has no clan filter. The grant goes to the settlement's governor, else the owner clan's leader if he is in the settlement, else nobody (`OnSettlementSkillExercised`). Skipping it also removes the 1000 character-level XP that `AddSkillXp` banks with it, and the notifications. It is off by default.

## Known Limits

- The three models extend the `Default*` models, as every TAOM model does, and call base by rule, so a model an earlier module put in the same slot is replaced, not composed. NavalDLC fills `PartyTrainingModel` (`NavalDLCSubModule.cs:186`) and `CharacterDevelopmentModel` (`:209`) and not `GenericXpModel`; TAOM declares NavalDLC incompatible. StoryMode fills `GenericXpModel` (`StoryModeSubModule.cs:121`) only in a story campaign, which TAOM hides. Both are moot for TAOM.
- `HeroDeveloper.ChangeSkillLevel` also goes through `AddSkillXp`, so the multiplier applies there. The only callers are the education of a player-clan child and character creation, neither of which an AI lord reaches, so it is latent.
- BannerlordTogether: the bonuses and the steward skip are off for every peer, solo included, while it is enabled, because TAOM cannot probe it and yields everywhere. The learning floor still runs.
- The patch skips the grant for every settlement the moment the setting is on and the peer does not defer; it does not distinguish the player's fiefs.
- An in-game A/B is owed: compare the counts of tier 7 to tier 10 troops in AI lord parties after a few campaign years with the bonus on and off, logging party sizes next to the tier counts, and confirm the player's clan, its spouse and its children get no bonus.

## Performance

`GetXpMultiplier` runs on every skill XP grant and the training members run for every troop element of every party each day. The settings are read through one cached accessor with no allocation, a non-lord, the main hero and the main party return before any control query, and the control query costs one compiled delegate call only under a live co-op session.

## Changelog

- 2026-10-30: the training model also applies the career `TroopTraining` flat term (#769), merged from the career forks work.
- 2026-10-24: first version (#890). Three campaign models, the Steward XP prefix (Patch118) and four MCM settings. Review fixes: the two bonuses defer to the co-op host, settings are four validated properties that fall back to the compiled default, the training label is gone, the registry probe is compiled, and the group order is 73.

## GitHub Issue

- **Issue:** [#890](https://github.com/haterade22/TAOM/issues/890): XP economy: tunable AI lord training bonus, learning floor, no free steward XP
- **Status:** Open
