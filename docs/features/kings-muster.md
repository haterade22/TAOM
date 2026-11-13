# The King's Muster

## Overview

One rule for the AI's armies: a kingdom's ruler calls lords into an army for a fraction of the influence a vassal pays (half by default), so Gondor, Rohan or Mordor can raise their great hosts when it matters. One MCM switch ("War of the Ring/Armies", default on) and one tuning number in `army_targeting.json`. Singleplayer only.

Keeping a companion or family party out of AI armies needs no TAOM code: the base game already has a per-party box for it, described under "Keeping your household out of AI armies" below.

## Why This Exists

- **Vanilla behavior:** `DefaultArmyManagementCalculationModel.CalculatePartyInfluenceCost` (v1.5.5 `:58-117`) prices each called party the same for a ruler and a vassal, apart from two policies: Royal Commissions (`:84-86`, minus 0.3 for the ruler) and Marshals (`:80`, minus 0.1 for a clan of tier 5 or more).
- **TAOM requirement:** the War of the Ring is built on great hosts, and a ruler summoning lords should be able to do it more cheaply than a vassal.
- **Without this feature:** rulers raise armies as rarely as any vassal.

## Architecture

### Design Challenge

- The influence is charged per joining party in `Army.OnAddPartyInternal` (`Army.cs:921-924`, `CalculatePartyInfluenceCost`), and the same per-party cost feeds the army's cohesion boost price (`CalculateTotalInfluenceCost`, `Army.cs:305`; only an army the player is not in pays a quarter of the sum, `DefaultArmyManagementCalculationModel.cs:200-203`). A discount on the per-party method therefore also makes an AI ruler's cohesion boosts cheaper. That is accepted and documented (decision 5).
- **The discounted cost is also vanilla's candidate ranking key.** `CanLordCreateArmy` (`:119-195`) prices every candidate through this method (`:156`), scores it as `strength / (cost + 0.1) * healthy fraction` (`:159`), sorts by that score (`:163`) and cuts the list to the room the kingdom has for more armies (`:169-171`). A ruler's candidates are therefore ranked with the discounted cost: a uniform factor keeps the order apart from near-ties (vanilla's 0.1 is not scaled) and truncation, but a cost of 1 truncates to 0 and its score is multiplied by 11, so the order can change. A factor of 0 would flatten every cost to 0 and rank by strength alone. That is why the factor has a floor of 0.25; at 0.25 and above only the small costs round, and the effect on who is called is small.
- The model slot is already filled by `TaomArmyManagementModel` (CulturalFeats), and `SubModule.cs` builds it with `new TaomArmyManagementModel(culturalFeats)`. The model resolves `KingsMusterService` itself, once, in its constructor (the pattern `TaomPartySizeModel` uses for the castle patrol hook), so its constructor signature and `Main/SubModule.cs` stay unchanged.
- The influence budget vanilla computes inside `CanLordCreateArmy` (`GetInfluenceBudgetWhileCreatingArmy`) is discarded, and only `Clan.Influence > 100` gates the call (`:123`). So the discount changes what armies cost to run, not whether a ruler may form one.

### Solution Approach

`TaomArmyManagementModel` gains no new override: its existing `CalculatePartyInfluenceCost` wraps the culture feats' result in `_muster.ApplyRulerDiscount(RulerFactsReader.IsKingdomRuler(leader), RulerFactsReader.IsLedByMainHero(leader), <the culture feats' result over the base cost>)`. The discount is the outer call, so it applies after the culture feats. The model holds no branch (`.claude/rules/gamemodels.md` rule 4).

`RulerFactsReader` (internal, static, in the feature folder, like `SiegeAvoidanceFactsReader`) reads two facts from the engine party and is null-safe: `IsKingdomRuler` (`party?.LeaderHero?.IsKingdomLeader == true`, `Hero.cs:608-618`) and `IsLedByMainHero` (the leader is `Hero.MainHero`). `KingsMusterService` is pure (no TaleWorlds type, ADR-007) and decides "an AI ruler" itself: a ruler who is not the player's hero.

| Case | Verdict |
|---|---|
| Leader is a kingdom ruler and not the player's hero, switch on, singleplayer | `(int)(cost * factor)`, vanilla's truncating cast; 0 and negative costs stay as they are |
| The player leads (as ruler or vassal), a vassal, a clan or bandit party leader | the base cost (culture feats included) |
| Factor 1 | the base cost |
| Factor NaN, infinite or outside 0.25 to 1 (a validated config cannot hold one) | read once in the service constructor as 1: the base cost, never a surcharge and never a free call |

### Stacking with policies

The discount multiplies whatever vanilla and the culture feats already produced. A ruler with both Royal Commissions and Marshals pays 0.6 of the no-policy cost before the factor, so about 0.3 of it with the default 0.5.

### Co-op

Singleplayer only. `KingsMusterService` applies the rule only when its switch is on and `!ICoopSessionProvider.IsSessionActive && !ShouldDeferToHost && !IDedicatedServerProvider.IsDedicatedServer`, read live on each call (the same gate as `RedArrowService.Applies`). In every other case the method returns the cost it was given. The feature declares no campaign behavior, so `CoopGateDocCoverageTests` asks for no row; the note is in [coop-interop.md](coop-interop.md).

### Component Diagram

```
army_targeting.json (RulerCallCostFactor) ─> ArmyTargetingConfigProvider (validated once)
                                                 │ read and sanitized once in the service constructor
MCM "War of the Ring/Armies" ─> KingsMusterSettingsProvider (captured once, lazy)
                                                 │
TaomArmyManagementModel ──────> KingsMusterService (pure)
 (resolved once in ctor)              ^
 CalculatePartyInfluenceCost ─> RulerFactsReader (ruler? main hero?)
```

### Rejected

An alignment-specific discount (more code for little gain); a surcharge above 1; a factor below 0.25 (it reorders vanilla's ranking, see above); a runtime log line per army (decision 6); a TAOM rule that drops the player's clan from AI candidate lists (see the next section).

## Keeping your household out of AI armies

Not a TAOM feature, and deliberately not code. An earlier draft of this feature filtered the candidate list to drop every party led by a member of the player's clan. The base game already has a per-party switch for it, and the filter overrode that switch and skipped vanilla's slot refill and strength check, so it was removed.

- `CanLordCreateArmy`'s own filter skips any candidate whose leader has `CanJoinArmy` false (`DefaultArmyManagementCalculationModel.cs:131`).
- `Hero.CanJoinArmy` reads `_partyConfiguration?.CanJoinArmy ?? true` (`Hero.cs:780-794`); `PartyConfiguration.CanJoinArmy` is a saved property that defaults to true (`PartyConfiguration.cs:8-9`).
- The clan screen sets it per party: the box "Allow joining other armies" on a party's row writes `Leader.CanJoinArmy` (`ClanPartyItemVM.cs:503`, `:556-562`).

To keep a companion's or family member's party out of AI armies, open the clan screen, select the party and untick "Allow joining other armies". It is stored on the party's leader and survives saving; if the party changes leader, untick it again for the new one.

## Configuration

### Config File: `Main/_Module/ModuleData/configs/army_targeting.json`

| Field | Type | Description |
|-------|------|-------------|
| `RulerCallCostFactor` | number | The fraction of the cost an AI ruler pays for each called party. Finite, from 0.25 to 1 inclusive (1 is no discount). |

### Current Values

`RulerCallCostFactor` is 0.5. `ArmyTargetingConfigProvider.Validate` checks it with `FiniteFloatValidator.IsFiniteInRange(value, 0.25f, 1f)`: a bad value (NaN, infinite, below 0.25, above 1) reverts to 0.5 alone with a warning and the provider's summary warning, and leaves the other fields as they are. A missing key keeps the compiled default 0.5. The file loads once per process, and the service reads the factor once in its constructor: an edit needs a full game restart. The service sanitizes it again (anything outside the range becomes 1, no discount).

### MCM

"War of the Ring/Armies" (the group order 76 is carried by `EnableRedArrow`): "Rulers call hosts for less influence" (`EnableKingsMusterDiscount`). Default on, `RequireRestart = false`; it is read on each call, so a change applies at once. It counts among the simulation-relevant settings in the co-op fingerprint.

When the switch is off, the cost is what it was before this feature: culture feats still apply, the policies still apply, only the factor is skipped.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/KingsMuster/KingsMusterModule.cs` | feature module: services only (no model, patch or behavior) |
| `Main/Features/KingsMuster/KingsMusterService.cs` | the rule and the co-op gate (a concrete class: nothing fakes it) |
| `Main/Features/KingsMuster/RulerFactsReader.cs` | the two facts about the leader of an engine party |
| `Main/Features/KingsMuster/KingsMusterSettingsProvider.cs`, `IKingsMusterSettingsProvider.cs` | the MCM switch, captured once |
| `Main/Features/ArmyTargeting/ArmyTargetingConfig.cs`, `ArmyTargetingConfigProvider.cs` | `RulerCallCostFactor` and its validation |
| `Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs` | the discount wrapper around the culture feats |
| `Main/_Module/ModuleData/configs/army_targeting.json` | tuning |

## Dependencies

- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop) for the gate.
- `IArmyTargetingConfigProvider` (ArmyTargeting) for the factor.
- `TaomSettings` (MCM) for the switch.

## Tests

- `TAOM.Tests/Features/KingsMuster/KingsMusterServiceTests.cs`: the discount (ruler, non-ruler, the player as ruler and as vassal, truncation, zero and negative cost, stacking on a feat-boosted cost, `int.MaxValue`, the switch read live, three co-op cases, the floor factor 0.25 paying a quarter, factor 1, factors outside the range, the factor read once).
- `TAOM.Tests/Features/ArmyTargeting/RulerCallCostFactorConfigTests.cs`: 0.25, 0.5 and 1 kept, NaN, infinities, 0, 0.2499, negatives and values above 1 reverted with warnings, siblings untouched, the shipped file.
- `TAOM.Tests/Features/KingsMuster/KingsMusterWiringTests.cs`: module listed once and declaring nothing else, the service resolves from a real container, the model keeps its two trunk overrides and its constructor, resolves the service once in the constructor, applies the discount after the feats, holds no branch, and `IoC.cs` and `SubModule.cs` name nothing of the feature.
- `TAOM.Tests/Features/KingsMuster/KingsMusterSettingsProviderTests.cs`: one switch, its group, no restart, the hint names the file, the default factor and no gendered words; plus the rows in `CampaignHotPathSettingsProvidersTests.cs`.
- `TAOM.Tests/Features/KingsMuster/RulerFactsReaderTests.cs`: a null party gives false and false (needs the game assemblies).
- `RedArrowWiringTests` pins only that `CheckPartyEligibility` is not overridden; `SettingsFingerprintTests` and `ServerSettingsSchemaTests` count one more setting.

## How to Retune

1. Edit `RulerCallCostFactor` in `Main/_Module/ModuleData/configs/army_targeting.json` (0.25 to 1; 1 turns the discount off without the switch).
2. Restart the game fully; the file is cached for the process.

## Performance

The call cost runs on the AI's hourly think on the main thread, once per army candidate of a lord that is allowed to raise an army. The wrapper is O(1) per call: two reads off the leader hero, one multiplication, no allocation. The MCM `Instance` is walked once per provider, not per call (`CampaignHotPathSettingsProvidersTests`).

## Balance

- More ruler-led armies and longer-lasting ones: a ruler's per-party cost, and with it the cohesion boost price of the ruler's army, is halved by default. A ruler's clan keeps more influence for votes and for its own spending.
- The discount lets a ruler's clan stay above the `Influence > 100` gate longer, so armies can come more often.
- Every kingdom is affected, Good and Evil alike (decision 1); the player as ruler gets no discount.
- It does not stack with the Realms liege army discount (`docs/features/realms.md`). When a ruler calls a vassal of their own title, the larger of the two discounts applies, half by default, not a quarter. `KingsMusterService.AppliedFactor` tells the Realms postfix what this discount already took off.
- The discounted cost is vanilla's ranking key, so a ruler's candidate order can shift slightly (see "Design Challenge"); the floor of 0.25 bounds it.

**Owed:** a 60-day balance check by hand reading, with the switch on and then off: the ruler's clan influence on the kingdom screen on days 0, 30 and 60, and the ruler's armies seen at fixed checkpoints. No census line is added.

## Changelog

- 2026-10-24: first version (#874).
- 2026-11-13: no longer stacks with the Realms liege army discount (conflict audit); the MCM hint says what turning it off leaves on.

## How to verify in game

All owed; none done yet.

- An AI ruler's clan influence drops less per party gathered than with the switch off (watch it on the kingdom screen while the ruler gathers).
- As the ruler, your own call costs are unchanged.
- Switch off mid-campaign: the cost returns to what it was before this feature, at once.
- Optional: untick "Allow joining other armies" on a companion's party in the clan screen; no AI army calls it, while you can still call it into your own army from Army Management.
- The balance check above.

## GitHub Issue

- **Issue:** #874, [The King's Muster: AI rulers call hosts for less influence](https://github.com/haterade22/TAOM/issues/874)
- **Status:** Shipped on `bannerlord-1.5.x` in `f61c2f355` (v2.0.35). Issue open: the in-game checks and the balance check are owed.
