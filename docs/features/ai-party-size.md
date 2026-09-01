# AI Party Size

## Overview

Lets AI lord parties actually hold the roster their party template spawns them with, instead of
being trimmed back to the vanilla party size limit within a day. Ships as one service consumed by
three existing GameModels, with six MCM knobs and no new Harmony patch.

## Why This Exists

Reported from play: lord parties spawn with 500 to 3000 troops, then collapse to 50 to 150 after a
couple of campaign ticks. The obvious suspects are wrong. **In vanilla 1.4.8 neither starvation nor
unpaid wages removes a single troop**: `FoodConsumptionBehavior.PartyConsumeFood` never touches
`MemberRoster`, and the wage branch of `DefaultPartyDesertionModel` is hard capped at 20 men a day.
Both are morale inputs only.

Three facts explain it:

1. **The 50 to 150 destination is the stock vanilla cap.**
   `DefaultPartySizeLimitModel.CalculateMobilePartyMemberSizeLimit:126` is pure addition with no
   clamp: base 20, plus `25 x clan tier` for a clan leader (`15 x tier` otherwise), plus 0.25 per
   Steward point, plus perks and policies. A tier-2 non-leader lands near 60, a tier-4 clan leader
   near 145.
2. **The collapse is TAOM's own TroopWeight shed, and it is a one-tick event.** `Patch17_TroopWeight`
   postfixes `PartyUpgraderCampaignBehavior.UpgradeReadyTroops`, which vanilla drives from
   `DailyTickPartyEvent` for every party not in a map event, every day, whether or not any upgrade
   happened (`PartyUpgraderCampaignBehavior.cs:44-50`). `PlanShed` removes the entire overflow in a
   single pass.
3. **Spawn size is unclamped.** `FindAppropriateInitialRosterForMobileParty` fills each stack to
   `min + (max - min) * r` with one uniform ratio per party and never consults `PartySizeLimit`.
   Commit `4f72e160` raised 193 lord template maxes on 2026-08-14 and deliberately left the cap
   alone, which `docs/reference/party-template-sizing.md:250-279` recorded as an open question.

## Architecture

### Design Challenge

Raising the cap is necessary but not sufficient. Four mechanisms shrink an over-sized party and the
cap only governs two:

| Mechanism | Trigger | Fixed by raising the cap? |
|---|---|---|
| TAOM TroopWeight shed | raw count over the deflated limit, daily | Yes, it sheds to `GetTrueBaseSizeLimit` |
| Vanilla desertion rule B | `max(1, excess x 0.25)` per day, uncapped | Yes |
| Vanilla desertion rule A | party morale under 10, up to 14.87% a day | **No** |
| Garrison dump | entering a friendly fortification | **No** |

Rule A is driven by starvation (a flat -30 morale) and unpaid wages (-20), both of which a large
party incurs automatically. So the cap alone would have moved the collapse from the shed to the
morale path without fixing anything visible.

The wage arithmetic decides it. `docs/features/startup-resources.md` grants AI startup gold as
`K x runwayDays x avgTroopWage`. Rearranged, the real runway is `K x runwayDays / N` for a party of
N, so **K is the assumed party size**. At the old `K = 52.5437` the stated 70 to 270 day runways
held near 53 men, which is exactly the band vanilla allowed. At 1000 men they would have become
about four days, after which `ClanVariablesCampaignBehavior.MakeClanFinancialEvaluation` drops every
clan to its lowest wage bracket and `HasUnpaidWages` pins to 1.0.

### Solution Approach

One service, `IAiPartySizeService`, consumed by three models that already existed. No new patch.

- **Party size** is scaled in `TaomPartySizeModel`, gated to leader-run lord parties that are not the
  main party.
- **Food and wage relief** are applied in `TaomFoodConsumptionModel` and `TaomPartyWageModel` behind
  the same gate, closing the morale path.
- **Garrisons** scale through a `CalculateGarrisonPartySizeLimit` override, because lords fielding
  thousands would otherwise walk over garrisons still capped near vanilla's 200.
- **Startup gold** was re-derived at `K = 100`, which is `targetPartySize x (1 - wageRelief)`
  = `1000 x 0.10`.

### The ordering requirement (read before editing `TaomPartySizeModel`)

`ApplyAiLordScaling` **must** run before `ITroopWeightService.ApplyPartySizeWeightPenalty`. That call
snapshots `(int)limit.ResultNumber` and caches it as the party's "true base", which is the budget the
daily shed later trims a heavy party back to. Applied after, the shed keeps trimming to the unscaled
limit and the entire feature silently does nothing while every arithmetic unit test still passes.

That failure is invisible to an ordinary test: both orderings produce the same `ExplainedNumber`, and
the divergence only appears a tick later inside a hook that takes a sealed `PartyBase`. It is pinned
by a source-order assertion in `AiPartySizeOrderingTests`, following the existing
`BannerTripletOrderingTests` precedent.

### Frames: why the flat bonus is not a plain `Add`

`ExplainedNumber` resolves as `BaseNumber * (1 + SumOfFactors)`. `Add` moves the base, so a raw
`Add(300)` alongside a 10x factor would be worth 3000 men rather than 300. `AddResultFrameBonus`
divides the factor back out so the knob means what it says. Same idiom as
`TroopWeightService.SubtractResultFramePenalty`.

Relief uses a factor, which scales magnitude and preserves sign, so one helper serves both wages
(positive) and food consumption (negative). Its gate is on the **projected** factor frame rather than
the incoming one: factors sum rather than compose, so a relief landing on top of an existing negative
factor could otherwise flip a wage bill into a wage rebate.

## Configuration

Six MCM knobs under "AI Party Size", no JSON file. MCM only is deliberate: a value settable from both
JSON and MCM has to enforce the same invariants at both surfaces or they drift, which
`.claude/rules/csharp-architecture.md` records as a shipped bug class. One surface, one clamp.

| Setting | Default | Effect |
|---|---|---|
| Enable AI Party Scaling | `true` | Master toggle; off restores vanilla caps exactly |
| AI Lord Party Size Multiplier | `5.0` | Multiplies the limit, preserving clan-tier progression |
| AI Lord Party Size Flat Bonus | `150` | Added after the multiplier, worth exactly this many men |
| Garrison Size Multiplier | `3.0` | Every garrison, player-owned included |
| AI Food Relief | `0.90` | Fraction of consumption waived |
| AI Wage Relief | `0.90` | Fraction of the wage bill waived |

Both knobs exist because they answer different halves of the mismatch. The multiplier keeps clan tier
meaningful; the flat bonus exists because template spawn is tier-independent, so under a multiplier
alone the low-tier lords still shed while high-tier ones sit under their cap.

**The two lord knobs move together or not at all.** `AddResultFrameBonus` divides the flat bonus by
`1 + SumOfFactors` precisely so the multiplier does not amplify it, which means the effective limit is
`base x multiplier + flat bonus`. Halving only the multiplier lands at 60% of the old limit, not 50%.
Both shipped defaults live as constants on `AiPartySizeService` (`DefaultLordFactor`,
`DefaultLordFlatBonus`); `TaomSettings` uses them as its property initializers and the service uses
them as its MCM-absent fallbacks, so the number exists once.

**Changing the compiled default does not move an existing install.** MCM applies a default only when
the key is absent from its json, and every install that has run v2.0.23 already persists
`AiLordPartySizeFactor` and `AiLordPartySizeFlatBonus`. Existing players keep the old values until
they move the sliders themselves. Same persistence trap CLAUDE.md records for ShaderPrecompilation.

### What the defaults actually produce

Resulting size limit, by lord:

Resulting size limit from the two lord knobs alone, `base x multiplier + flat bonus`:

| Lord | Vanilla | Old defaults (10.0 / 300) | Shipped defaults (5.0 / 150) |
|---|---|---|---|
| Tier-1 non-leader, Steward 20 | 40 | 700 | 350 |
| Tier-3 clan leader, Steward 60 | 110 | 1400 | 700 |
| Tier-4 Mordor non-leader, Steward 100 | 126 | 1560 | 780 |
| Tier-4 Goblin clan leader, Steward 100 | 203 | 2330 | 1165 |

The tier-4 Mordor row is pinned by
`AiPartySizeShippedDefaultsTests.ShippedDefaults_ProduceTheDocumentedTierFourLimit`.

Two caveats on reading that table. It is the knob arithmetic only, so a culture party-size feat in the
same `ExplainedNumber` frame pushes the real number up, and the TroopWeight elite tax pushes it back
down where the roster is heavy. Before the 2026-09-01 halving this table's last two rows carried 1371
and 1808 instead of 1560 and 2330, which are measurements with the elite tax already in the frame, not
knob arithmetic; the derivation was never recorded and could not be reproduced, so they are quoted
here as history rather than restated.

**Stale, re-measure owed.** The retention table below was measured against those 1371 / 1808 limits.
Under the halved defaults every culture's limit is roughly 45-50% of what it was, so the retained
column and its percentages are all optimistic and the two 100% rows almost certainly are not 100% any
more. The qualitative finding underneath it (average troop weight is far lower than an orc or elf
roster looks) is unaffected and still holds. Re-measure from `taom_partyTemplates.xml` and
`troop_weights.xml` before leaning on these numbers for another balance pass.

| Culture template | Expected spawn | Weighted | Avg weight | Retained |
|---|---|---|---|---|
| gondor | 755 | 809 | 1.07 | 755 (100%) |
| rohan | 768 | 768 | 1.00 | 768 (100%) |
| rivendell | 530 | 1024 | 1.93 | 530 (100%) |
| erebor | 1051 | 1420 | 1.35 | 1002 (95%) |
| goblin | 2277 | 2277 | 1.00 | 1808 (79%) |
| isengard | 1777 | 1777 | 1.00 | 1371 (77%) |
| mordor | 1774 | 2120 | 1.20 | 1024 (58%) |

**Do not assume an orc or elf culture is a wall of weight-2.0 troops.** That assumption was made
while designing this and it is wrong: only 12.9% of Mordor's expected spawn is weight-2 by body
count, giving an average of 1.20, and Isengard, Goblin-town and Rohan average exactly 1.00 because
nothing in their culture-default templates carries a weight entry at all. Rivendell is the only
genuinely heavy roster at 1.93, and its spawn is small enough (530) that it fits under the cap
anyway. The elite tax therefore costs far less than a naive reading suggests.

Mordor is the one culture that still sheds meaningfully at the defaults, and raising either slider
closes that gap. Caveat: this measures the seven culture-DEFAULT templates, and 176 of the 193
lord-party templates are per clan, so an individual clan's mix can differ.

Out-of-range or non-finite values are **ignored rather than clamped**, leaving vanilla behaviour. A
knob that has drifted outside its slider range is a fault, and coercing it into a plausible-looking
number hides that.

## Key Files

| File | Role |
|---|---|
| `Main/Features/AiPartySize/AiPartySizeService.cs` | Gate plus the pure frame arithmetic |
| `Main/Features/AiPartySize/IAiPartySizeService.cs` | Contract, including the ordering requirement |
| `Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs` | Party-size and garrison call sites |
| `Main/Features/CulturalFeats/Models/TaomFoodConsumptionModel.cs` | Food relief call site |
| `Main/Features/TroopProgression/Models/TaomPartyWageModel.cs` | Wage relief call site |
| `Main/_Module/ModuleData/startup_resources/startup_resources_config.xml` | Re-derived at `K = 100` |

Wage relief is applied in `GetTotalWage` and deliberately **not** in `GetCharacterWage`, because
`Campaign.AverageWage` is built from the latter and the garrison-donation math divides by it.
Discounting per-head wage would inflate the number of troops the AI thinks it can afford to leave
behind.

## Tests

**28 tests** under `TAOM.Tests/Features/AiPartySize/` (24 service, 4 ordering), plus **4** in
`CareerPassiveServiceTests` for the flat-passive frame fix. `AiPartySizeServiceTests` covers the frame arithmetic (flat bonus not amplified by the
factor, relief preserving sign on negative consumption, relief refusing to invert a wage bill),
the gating predicate, and one non-finite case per knob per the engine-float gate rule.
`AiPartySizeOrderingTests` pins the call ordering and the three call sites.

## Known Limitations

- **The garrison dump is not fully closed.** `GarrisonTroopsCampaignBehavior` sizes a lord's ideal
  party from `min(PaymentLimit / AverageWage, PartySizeLimit)` against a food-derived term with a
  floor of 30, so a lord entering a friendly fief with a thin food stock can still donate down toward
  that floor. The gate is narrow (same faction, non-player clan, garrison headroom available, and
  suppressed when the garrison's own wage limit is exceeded), so it is intermittent rather than
  constant. Closing it properly needs a Harmony patch on a private behaviour method, which is
  deliberately out of this pass. Raising Garrison Size Multiplier gives lords **more** room to
  donate, so the two knobs pull against each other.
- **Battle and campaign-map load at this scale is unprofiled.** Two large armies meeting is exactly
  what `party-template-sizing.md:259-267` deferred this change over. Control battles are required
  before trusting the default multiplier.
- **The income deficit is untouched.** Startup gold is a one-time cushion and TAOM's fief income per
  lord still spans 26.1x. Parties may still decay late-campaign once the runway is spent.
- **The player is excluded from the lord knobs, but not from the garrison one.** Party size, food
  relief and wage relief all skip both the main party AND any party belonging to the player's clan.
  That second test is load-bearing and easy to lose: a party the player raises for a companion is a
  `LordPartyComponent`, so it is `IsLordParty` and is not `IsMainParty`. Deep review 2026-08-18
  caught it collecting the full AI treatment. The garrison multiplier deliberately does NOT have a
  player test, because it is siege balance: your own settlements defend as well as everyone else's.
- **Career flat passives are now literal (fixed here).** `CareerPassiveService.ApplyFlat` used to add
  its magnitude in the BASE frame, so it was multiplied by every factor on the number: with this
  feature's 3x garrison multiplier in play, a "+4 party size" perk was worth about +13 and the career
  screen's promise stopped being true. `ApplyFlat` now divides the factor frame back out, so an
  authored count is worth exactly that count. The Health call site is unaffected because vanilla
  `DefaultCharacterStatsModel.MaxHitpoints` uses only `Add` and never `AddFactor`, so its frame has no
  factors and the conversion is a no-op there. Pinned by four tests in `CareerPassiveServiceTests`.

## How to verify in game

- `taom.print_party_size` prints the whole chain for the main party (raw, weighted, true base,
  penalty, final limit).
- The one-shot `[TroopWeight][diag] Shed N bodies from '<party>'` line should stop appearing for AI
  lords once the cap clears their spawn size. It fires once per process.
- Vanilla prints `[High Desertion Alert]` whenever more than 40% of a party deserts in one tick. If
  it still fires after the cap is raised, the remaining driver is morale, meaning the food or wage
  relief is not yet enough.
- Turning the master toggle off is a clean one-day A/B.

## GitHub Issue

[#461](https://github.com/haterade22/TAOM/issues/461)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/startup-resources.md](./startup-resources.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/party-template-sizing.md](../reference/party-template-sizing.md)

<!-- backlinks-end -->
