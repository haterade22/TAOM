# Caravan / bandit strength parity

## Overview

Bandit raider warbands and caravans are now sized against each other in the unit the engine actually
compares, so a caravan meeting a warband on the road no longer runs. Two halves of one change: a
power-budget retune of the 50 bandit and caravan party templates
(`tools/rebalance_template_power.py`), and a caravan member-cap bonus in `TaomPartySizeModel` so a
caravan may hold the roster its template spawns.

Bandit warbands drop from up to 200 men to 56-80, sized so every culture's warband lands at the same
**power** rather than the same headcount. Caravans rise from 20-36 men to 60-88, sized so the weakest
roster a caravan can spawn still out-powers the strongest warband.

## Why This Exists

Reported from play: bandit parties reaching 200 while caravans field 20-36, leaving caravans
permanently running away.

The report was right, and the consequence is worse than the flight itself.

### The flee decision is a cliff, not a slope

`MobilePartyAi.GetBehaviors` delegates to `MobilePartyAIModel.GetBestInitiativeBehavior` and switches
behaviour when the returned score clears `1f`. In
`DefaultMobilePartyAIModel.CalculateInitiativeScoresForEnemy`:

```csharp
float num4 = MBMath.ClampFloat((localAdvantage < 1f) ? MBMath.ClampFloat(1f / localAdvantage, 0.05f, 3f) : 0f, 0.05f, 3f);
avoidScore = 0.9433963f * num20 * num18 * ((num2 > 0.01f) ? 1f : 0f) * num4;
```

`localAdvantage` is own strength over threat strength. At `localAdvantage >= 1` the term collapses to
its `0.05` floor and `avoidScore` peaks at about `0.19`, which can never reach 1. Below 1 it saturates
at 3 almost immediately, so `avoidScore` reaches roughly 3.8 and the party runs.

**That is why "make caravans somewhat bigger" was never going to work.** A caravan eight times
outmatched flees exactly as hard as one twice outmatched; only crossing 1.0 changes anything. The
transition is 3.81 to 0.19 across a hairline.

The attack gate is the exact complement: `num3 > num4` reduces to `L(1 + L) > 2`, i.e. `L > 1`. So at
parity the warband also stops choosing to attack.

### A fleeing caravan stops trading

Fleeing sets `IsAlerted = true` (`MobilePartyAi.cs:557-560`), and
`CaravansCampaignBehavior.HourlyTickParty` will not pick a new destination while a caravan is alerted
or fleeing. `MobileParty.CheckExitingSettlementParallel` will not release a party whose short-term
target is its current settlement. So a caravan in a bandit-dense world does not merely run: it parks.

That is the economic damage, and it is why this is upstream of
[#396](https://github.com/haterade22/TAOM/issues/396), which treats permanently parked caravans and
explicitly assumes `Alerted` is transient. At 200-man warband density it was not transient.

Bandits also hunt caravans from twice the normal reach: `GetInitiativeDistanceForAttack` returns `2f`
when `enemyParty.IsCaravan && mobileParty.IsBandit`.

### Where the 200 came from

`tools/raise_party_template_maxes.py` (commit `94e874fd`, [#315](https://github.com/haterade22/TAOM/issues/315),
2026-07-02) set `max_value="50"` on every stack of the eight bandit cultures' raider and boss
templates. Raider templates carry four stacks, so the ceiling was exactly `4 x 50 = 200`, and
`Patch39_BanditPartySize` multiplies each stack by `1 + 1.5 x PlayerProgress` and clamps at
`stack.MaxValue`, so it is built to drive stacks there. The changelog for #315 states the intent
verbatim: "up to ~200 endgame."

Two later changes removed what had been masking it. The lord-template walk-back
(`tools/rebalance_party_template_maxes.py`, 2026-09-01) excludes bandit templates by regex, and the
2026-08-07 TroopWeight leaderless-shed fix removed the daily trim that had been cutting warbands back.

For scale: vanilla bandit templates cap at 45, vanilla hideout bosses at 6. TAOM's untouched
`looters` template still caps at vanilla's 36.

## Architecture

### Strength is power, not headcount

`PartyBase.EstimatedStrength` resolves to `MilitaryPowerModel.GetPowerOfParty`:

```csharp
num += (element.Number - element.WoundedNumber) * GetTroopPower(character, side, context, leaderModifier);
...
if (context == MapEvent.PowerCalculationContext.Estimated)
    num2 = MBMath.Map(party.MobileParty.Morale, 20f, 40f, 0.7f, 1f);
return num * num2;
```

so `sum(healthy x tierPower) x moraleFactor x (1 + leaderPowerModifier)`. Wounded troops do not count.
Tier is `clamp(ceil((level - 5) / 5), 0, MaxCharacterTier)`, a pure function of `level=`; a missing
`level=` deserializes to 1, which is tier 0.

`TaomMilitaryPowerModel` is in the path with `EnableCustomTroopPower` shipped `true`. It keeps
vanilla's `(2 + tier) * (10 + tier) * 0.02` for tiers 0-6 (`OverrideVanillaTierPower` ships false)
and applies a `MountedMultiplier` of 1.2 that vanilla does not have. Tiers 7-10 come from the
MCM-settable `TaomSettings.Tier7Power..Tier10Power`, NOT from
`configs/battle_balance_config.json` as an earlier draft of this doc claimed; the tool reads the
JSON, whose values equal those compiled defaults, and no troop in these 50 templates exceeds
tier 5, so the two agree today. For a non-hero troop `IsMounted` is assigned from
`DefaultFormationClass.IsMounted()` during `Deserialize`, so `default_group` decides it outright.

| Tier | T0 | T1 | T2 | T3 | T4 | T5 |
|---|---|---|---|---|---|---|
| Power | 0.40 | 0.66 | 0.96 | 1.30 | 1.68 | 2.10 |

**This is why a flat `max_value` cannot balance these templates.** The eight raider cultures run from
T1,T2,T2,T3 to T2,T3,T4,T4, so at a shared per-stack count their warbands spanned 64 to 112 power, a
1.75x spread. One caravan number cannot sit above both.

### The retune

`tools/rebalance_template_power.py` solves each template for a power budget and writes the per-stack
counts. Budgets live in `DEFAULT_BUDGETS` at the top of the tool.

| Group | Budget | Result | Early game |
|---|---|---|---|
| raider | 78 power, floor at 12.5% of max | 76-79 power, 56-80 bodies | 12-32 bodies |
| boss | 105 power, floor at 12.5% of max | 97-108 power, 67-97 bodies | in hideout, never roams |
| caravan | 94 power floor, +15% spread | 93-109 power, 60-80 bodies | same, no ratio spread |
| elite caravan | 110 power floor, +15% spread | 110-126 power, 66-88 bodies | same |

### The bandit floor, and why it needed lowering too

Cutting the ceiling squeezed the early game twice, which the first cut of this work missed.
Vanilla gives a land bandit party the ratio `(0.4 + 0.8 * PlayerProgress) * U(0.2, 0.8)`, so
at `PlayerProgress = 0` it spans only 0.08 to 0.32. With `spawn = min + (max - min) * r` the
early game is a narrow band sitting just above `min`, and its width is `(max - min) * 0.24`.
Dropping `max` from 200 to ~78 power therefore cut the early spread from about 44 bodies to
12, and pinned every early warband near 21 men.

`min_frac` puts the floor back at 12.5% of the ceiling. Early parties now run 12-32 bodies
with a spread of 12-17, against 20-33 immediately after the first cut and 31-75 before any of
it. **Lowering `min` cannot restore the ORIGINAL spread** and was never going to: that needs
the original `max`. What it buys is a vanilla-like early floor (vanilla looters run 4 to 36)
instead of a flat wall of same-sized warbands.

One knock-on worth knowing: because the ceiling is so much lower, `BanditPartySizeCurve`'s
useful range is now much shorter. `Patch39` clamps the scaled roster at `stack.MaxValue`, and
that clamp is reached around a quarter of the way through a campaign, after which raising the
slider does nothing. Recorded in [bandit-management.md](bandit-management.md).

Two solver shapes, because the two families need different things:

- **Bandit templates are flat by construction** (N stacks on one shared max, plus a pinned `1/1` hero
  stack on a boss template), so `solve_flat` solves for the single shared count. Scaling each stack
  from its own current value is not a fixed point: when the budget falls between two reachable values
  the tool oscillates, and `gundabad_raiders_boss_party_template` flipped between 18 and 19 per stack
  on alternate runs before this was fixed.
- **Caravans are solved as a band.** A non-player caravan spawns at `min + (max - min) * r` with one
  uniform `r` per party (`GetInitialPartySizeRatioForMobileParty` returns `party.RandomFloat()`), so
  the MIN is the roster an unlucky caravan actually gets. `floor_power` is therefore applied to the
  min, not the midpoint. Targeting the midpoint would leave roughly half of all caravans below the
  line and still parked.

The caravan shape is normalised rather than scaled from each template's own values, because Rohan,
Dale and Dunland shipped a `1/1` armed-trader stack where the other fourteen carried `12/15` and were
consequently about half the bodies for no recorded reason.

**Result: the weakest caravan roster is 93.2 power against the strongest roaming warband's 78.7, an
`L` of 1.18.** The 18% surplus is deliberate headroom for the morale factor, which ranges 0.7 to 1.0
and applies to each side independently.

Boss templates are excluded from that comparison on purpose:
`BanditSpawnCampaignBehavior.AddBossParty` calls `.Ai.DisableAi()` on them, so they never leave their
hideout and a caravan cannot meet one on the road.

### The caravan cap, and why it is not optional

`DefaultPartySizeLimitModel.CalculateMobilePartyMemberSizeLimit` opens at 20, and its clan-tier and
Steward branch is guarded `!party.IsCaravan`:

```csharp
if (party.LeaderHero != null && party.LeaderHero.Clan != null && !party.IsCaravan) { /* tier + Steward */ }
else if (party.IsCaravan)
{
    if (party.Party.Owner == Hero.MainHero) result.Add(IsElite ? 30 : 10);
    else if (owner != null && owner.IsNotable) result.Add(10 * (Power < 100f ? 1 : Power < 200f ? 2 : 3));
}
```

So every caravan is capped at 20-50, and `AiPartySizeService.IsScalableAiLordParty` gates on
`isLordParty`, so the #461 scaling layer never reached them either. (v1.4.8 adds one sub-branch
the snippet omits: a PLAYER-owned caravan with `CanHaveNavalNavigationCapability` gets `46 : 33`
instead of `30 : 10`, so it caps at 66. NavalTravel is parked and every TAOM caravan template is
land-based, so it is unreachable here, and it does not touch the floor of 30 the tests rely on.)

An over-cap caravan is not merely held there, it is drained. `DesertionCampaignBehavior.DailyTickParty`
gates on `(IsLordParty || IsCaravan || IsGarrison)` and sheds `max(1, excess x 0.25)` per day with no
morale condition, and `DefaultPartySpeedCalculatingModel.GetOverPartySizeEffect` is
`1/(count/limit) - 1`, which is a -0.5 speed factor at twice the cap. **Raising the templates without
raising the cap ships a strictly worse game than changing nothing**, which is why the two halves are
one change.

`AiPartySizeService.ApplyCaravanScaling` adds a flat `DefaultCaravanFlatBonus = 70`, giving 90 / 100 /
120 by notable Power band and 90 / 120 for the player. Flat rather than a multiplier so vanilla's
10-man steps between Power bands survive rather than being stretched. It routes through
`AddResultFrameBonus`, which divides the factor frame back out, because
`CulturalFeatsService.ApplyPartySizeFeats` runs earlier on the same frame and is not gated by party
type, so a caravan of an evil culture really does arrive with a factor in play.

**Ordering.** The call sits between `ApplyAiLordScaling` and `ApplyPartySizeWeightPenalty`, for the
same reason the lord scaling does: the TroopWeight call snapshots `(int)limit.ResultNumber` as the
party's "true base", the budget the daily shed later trims to. Applied afterwards, the shed would keep
trimming to vanilla's 30-50 and the caravans would bleed back down over a week with every unit test
still green. Pinned by a source-order assertion in `AiPartySizeOrderingTests`.

### Deliberately no MCM knob

Unlike every other member of `IAiPartySizeService`, the caravan bonus is not gated by
`EnableAiPartyScaling` and has no setting. The cap and the shipped template maxima are two halves of
one balance change, so a switch that reverted the cap while the XML stayed large would ship precisely
the daily shed and the speed penalty it exists to prevent, and a player would have no way to revert
the other half. One constant, no surfaces, no drift. `csharp-architecture.md`'s "one surface, one
clamp" taken to zero.

## What this does NOT do

- **Caravans do not start hunting bandits.** `CaravanPartyComponent`'s constructor sets
  `MobileParty.Aggressiveness = 0f`, and the attack score multiplies by that aggressiveness, so
  `attackScore` is 0 no matter how strong the caravan gets. Nothing flees *from* a caravan either:
  `ShouldConsiderAvoiding(x, caravan)` returns `caravan.IsGarrison`, which is false. At parity both
  parties simply ignore each other.
- **Packs of bandits still take caravans.** The threat term sums nearby hostile parties, so two
  warbands together put the caravan back below 1 and it flees as before. Lone warbands can no longer
  farm caravans; groups still can, which is the mechanic worth keeping.
- **Villagers are untouched, on purpose.** `villager_<culture>` is level 1, tier 0, 0.40 power, in a
  15-30 template, so a villager party is about 9 power and flees everything. It did in vanilla too.
  Villagers are meant to run from bandits; the smaller warbands are the whole benefit they need.
- **Patrol, mercenary and outlaw templates** are out of scope; the 34 mercenary and outlaw
  templates still carry the flat 50 from #315.
- **SupplyLines is no longer coupled to these templates at all.** It used to build the
  player's supply caravans from `culture.CaravanPartyTemplates[0]`, so this retune took its
  escort from 20-29 troops to 60-70 and its provisioning cost with it (#549, found by the
  deep-review data-flow agent). It now has its own `supply_caravan_template_*` crew templates,
  sized 4-8 against the flat 20-man cap a supply caravan actually gets. See
  [supply-lines.md](supply-lines.md).

## Second-order effects, each measured

| Effect | Finding |
|---|---|
| Caravan wages | **Notable-owned caravans pay nothing.** `DefaultClanFinanceModel.AddExpensesFromPartiesAndGarrisons` iterates only `clan.AliveLords` and `clan.Companions`, and a notable has no clan, so the overwhelming majority of AI caravans have no wage bill at all. Clan-owned and player caravans pay from `PartyTradeGold`; a player elite caravan rises roughly 50%. `initialTradeGold` was deliberately left alone rather than raised speculatively. |
| Caravan speed | `CalculateBaseSpeedForParty(n) = BaseSpeed * (200/(200+n))^0.4`, so 36 to 60 men costs about 5%. Negligible **provided the cap moves**; without it the over-size term is -0.5. |
| Caravan growth | Vanilla `RecruitmentCampaignBehavior.HourlyTickParty` already accepts AI caravan parties, and TAOM's `Patch42` extends that to castles, so caravans climb toward the raised cap over time rather than only at spawn. |
| Battle load | An 80-man warband against an 80-man caravan is ~160 agents, well inside what TAOM already fields for lord battles. |

## Key Files

| File | Role |
|---|---|
| `tools/rebalance_template_power.py` | The power-budget solver and the template writer |
| `tools/tests/test_rebalance_template_power.py` | 54 tests: tier maths, all three solvers, byte-faithful IO |
| `tools/generate_supply_caravan_templates.py` | The 17 SupplyLines crew templates (#549) |
| `Main/Features/SupplyLines/SupplyCaravanService.cs` | `PickCaravanTemplate` resolves the crew template |
| `TAOM.Tests/Features/SupplyLines/SupplyCaravanTemplateTests.cs` | The decoupling invariants |
| `Main/_Module/ModuleData/taom_partyTemplates.xml` | 50 retuned templates (16 bandit, 34 caravan) |
| `Main/_Module/ModuleData/characters/npcs_rohan.xml` | The four repaired caravan NPCs |
| `Main/Features/AiPartySize/AiPartySizeService.cs` | `ApplyCaravanScaling`, `ApplyCaravanCapBonus`, `DefaultCaravanFlatBonus` |
| `Main/Features/AiPartySize/IAiPartySizeService.cs` | Contract, including why this member is ungated |
| `Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs` | The call site, ordered before the TroopWeight tax |
| `TAOM.Tests/Core/CaravanBanditParityTests.cs` | The shipped-data invariants |
| `TAOM.Tests/Features/AiPartySize/CaravanPartySizeTests.cs` | The cap arithmetic |

## The Rohan defect, found on the way

`armed_trader_rohan`, `caravan_master_rohan`, `caravan_guard_rohan` and
`veteran_caravan_guard_rohan` shipped with **no `level=` attribute and no `<skills>` block at all**.
They were the only four `CaravanGuard` NPCs in the repo in that state. The engine reads a missing
`level=` as 1, so tier 0, so 0.40 power: a Rohan elite caravan was about 9.4 strength, the same as a
villager party, and its troops had no weapon proficiency in the actual battle either.

Nothing could see it. No reference was broken, no file failed to parse, and every validator and test
passed. `CaravanBanditParityTests.EveryCaravanTroop_DeclaresAnExplicitLevel` is the sentinel now.

Levels and skills were set to the convention every other culture uses (16 / 21 / 26 / 26).

Related but deliberately left alone: `harad`, `rhun` and `isengard` field a `caravan_guard` and a
`veteran_caravan_guard` one tier below everyone else. That is a legitimate difference rather than a
defect, and the power budget compensates automatically by buying those cultures more bodies (69-80
regular, 77-88 elite against 60-70 and 66-75).

## Tests

- **`TAOM.Tests/Core/CaravanBanditParityTests.cs`** (9) reads the shipped XML and recomputes the
  engine's own number. The load-bearing one is
  `EveryCaravanTemplate_AtItsWeakestDraw_OutpowersTheStrongestWarband`, stated at the worst case for
  the caravan against the best case for the bandit. `TheLargestCaravanRoster_FitsUnderTheSmallestCapTheEngineCanGiveIt`
  is the coupling between the C# constant and the XML, which is the failure a normal review misses
  because the two halves are in different languages. `TroopIndex_AndTemplateIndex_AreNotEmpty` exists
  because a renamed folder would empty the sweep silently and every other assertion would then pass
  against nothing.
- **`TAOM.Tests/Features/AiPartySize/CaravanPartySizeTests.cs`** (6) covers the frame arithmetic,
  including that the bonus is not amplified by a culture feat already in the frame.
- **`AiPartySizeOrderingTests`** gains a source-order pin for the caravan call.
- **`TAOM.Tests/Features/SupplyLines/SupplyCaravanTemplateTests.cs`** (5) pins the decoupling:
  every bound caravan template has a supply crew sibling, and crew plus the default escort
  fits under the flat 20-man cap. `SupplyCrewTemplates_AreMuchSmallerThanTheAiCaravanTemplatesTheyReplaced`
  fails rather than quietly restoring #549 if anyone regenerates these from the AI numbers.
- **`tools/tests/test_rebalance_template_power.py`** (54) covers tier derivation, all three solvers, the
  `min + 1` floor that prevents the 2026-09-04 stack-deletion class, refusal to emit `min > max`,
  idempotency, BOM and line-ending preservation, and a parse-after-transform assertion.

## How to verify in game

`taom.print_caravans` prints every caravan and names the engine gate holding it. The `Fleeing` and
`Alerted` counts in that histogram are the success metric: before this change they dominate in any
bandit-dense region, after it they should be rare and transient. `taom.print_party_size` prints the
cap chain.

Re-running `python tools/rebalance_template_power.py` with no `--apply` prints the parity line
(`weakest caravan roster ... vs strongest roaming warband ... -> L = ...`) without touching anything.

## Save Compatibility

| Surface | Effect |
|---|---|
| Template min/max edits | New parties only; the roster is drawn at spawn. Existing 200-man warbands and 30-man caravans persist in an old save until destroyed. Caravans turn over in days; warbands turn over as they are cleared. |
| `npcs_rohan.xml` levels | Applied at next campaign load. `CharacterObject.Tier` is computed, not stored, and no new item or equipment file was added, so no full process restart is needed. |
| The caravan cap | Immediate. `PartyBase.PartySizeLimit` recomputes when `MemberRoster.VersionNo` changes, and `AiPartySizeSettingsWatcher` already sweeps every party with no lord filter. Existing under-cap caravans stop shedding; they do not retroactively refill. |
| SaveSystem | No new saveable types, no `SyncData`, no id renames. |

## GitHub Issue

[#543](https://github.com/haterade22/TAOM/issues/543) (the parity work) and [#544](https://github.com/haterade22/TAOM/issues/544) (the Rohan caravan-troop defect).

## Related

- [bandit-management.md](bandit-management.md) : `Patch39_BanditPartySize` and the density knobs
- [caravan-trade.md](caravan-trade.md) : where caravans go, as distinct from whether they can move
- [ai-party-size.md](ai-party-size.md) : the lord-party half of `TaomPartySizeModel`
- [economy-diagnostics.md](economy-diagnostics.md) : `taom.print_caravans`
- [../reference/party-template-sizing.md](../reference/party-template-sizing.md) : what `max_value` controls
- [#396](https://github.com/haterade22/TAOM/issues/396) : permanently parked caravans, of which this removes a major driver
- [#315](https://github.com/haterade22/TAOM/issues/315) : the closed issue this partially reverses
