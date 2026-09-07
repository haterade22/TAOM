# Marriage Alignment

## Overview

A Free-aligned hero cannot marry an Evil-aligned one. A lord of Gondor, Rohan, Dale, Erebor or the
Elves will not wed a hero of Mordor, Isengard, Gundabad, Dol Guldur or any orc culture, and the
reverse holds too. Neutral cultures (Umbar, Shaghana, Abanissa, Dunland) marry anyone.

The rule keys on **culture**, and a companion Harmony transpiler narrows the AI's partner search so
Free factions do not lose most of their marriages as a side effect.

## Why This Exists

- **Vanilla behavior:** `RomanceCampaignBehavior.CheckNpcMarriages` picks the partner clan with
  `Clan.All[MBRandom.RandomInt(Clan.All.Count)]`, a uniform random draw over every clan in the
  campaign. Kingdom membership is only a `0.5x` multiplier on the odds, never a gate. The single
  hard cross-faction filter is "not at war and clan relation at least -50".
- **TAOM requirement:** Middle-earth has a moral axis the base game does not model. The free peoples
  do not intermarry with Sauron's servants.
- **Without this feature:** Issue #542. In a live campaign Boromir, Noble of Gondor, married Nurzga
  (`lord_MM12_9`, `race="orc"`, `culture="Culture.mistymountainorcs"`) in Spring 1086 and had two
  children by her. The pairing became reachable the moment Gondor was no longer at war with the
  Misty Mountain Orcs. There are **149 unmarried orc-culture female lords** in the day-0 candidate
  pool, so this was never a fluke.

This is not a regression. TAOM had no cross-alignment marriage rule of any kind before #542.

## Architecture

### Design challenge

Two separate problems, and conflating them produces a fix that makes the game worse.

**1. Where to block.** Marriage has four distinct entry points (AI daily tick, player courtship
dialogue, the marriage barter, and AI offers to the player clan). Gating only the apply step would
let lords court a partner they can never marry.

**2. The rate collapse.** The clan pool is 53% Evil, 25% Free, 16% Neutral. Blocking alone leaves a
Free lord's single daily draw landing on a usable clan only about 41% of the time, so Gondor, Rohan
and Dale would marry at roughly 40% of their current rate and run short of heirs. A correct block
with no compensation is a slow demographic bug.

### Solution

**The block** is one overridden method. `IsCoupleSuitableForMarriage` is the single chokepoint every
path funnels through, verified in the v1.4.8 decompile:

| Path | Call site |
|---|---|
| AI daily tick | `CheckNpcMarriages` to `NpcCoupleMarriageChance`, whose first line is `if (IsCoupleSuitableForMarriage(...))` (`DefaultMarriageModel:51`) |
| Player courtship and the marriage barter | `RomanceCampaignBehavior.MarriageCourtshipPossibility:1433` |
| AI proposing to the player clan | `MarriageOfferCampaignBehavior` |
| Apply | `MarriageAction.ApplyInternal:11` |

`IsSuitableForMarriage` stays wraith-only: it is a single-hero predicate and cannot express a pair
rule.

**The steering** is `Patch81_MarriageAlignment`, a transpiler that swaps both `Clan.All` reads in
`CheckNpcMarriages` for an alignment-filtered pool. Vanilla's `.Count` and indexer then operate on
the filtered list with no further IL change, and every vanilla filter after the draw (war, clan
relation, romance state, the model) still runs.

### Why culture and not kingdom

`AlignmentRecruitment` keys on kingdom because it asks "whose settlement is this". Marriage asks
about peoples, so it keys on culture:

- Culture is stable across a hero's career; kingdom is not.
- Culture is present on a clanless hero.
- `GetClanAfterMarriage` moves the bride into the groom's clan and kingdom, so kingdom is the thing
  the marriage itself changes.

All 1,184 lords in `lords.xml` resolve to 22 cultures and every one of them is classified in
`execution/alignment.json` (725 evil, 261 free, 198 neutral). The 8 cultures absent from that file
are minor and bandit cultures that seed no lords.

There is deliberately **no runtime kingdom fallback** for an unclassified culture. An unclassified
culture resolves Neutral, which means "may marry anyone", so the gap is a silent permit rather than
a visible failure. Rather than paper over it at runtime with a second lookup that carries its own
defection semantics, `ShippedCultureAlignmentCoverageTests` makes the gap impossible at build time.

### Component diagram

```
marriage_alignment/marriage_alignment_config.json
        |
  MarriageAlignmentConfigProvider  (Lazy, Reuse.Singleton)
        |
  MarriageAlignmentSettingsProvider  (MCM over JSON)
        |
  MarriageAlignmentService  ..... IAlignmentService.GetCultureSide (execution/alignment.json)
       /                    \
      /                      \
TaomMarriageModel        Patch81_MarriageClanDraw
(the block, all paths)   (transpiler + Clan boundary)
                                  |
                         MarriageClanPoolCache  (per-culture candidate pools)
                                  |
                         MarriageClanPoolStamp  (when those pools go stale)
```

## The single MarriageModel slot

`TaomMarriageModel` lives in `Main/Features/NazgulFamily/Models/` and now carries **two independent
rules**: the Ringwraith block and this one. That is not an accident of history.

The engine resolves exactly one model per type through a backwards scan over registered models, so
`AddModel` does **not** compose. A second `AddModel(new SomeMarriageModel(...))` would silently
shadow whichever rule registered first, with no error and no log. Any future marriage rule goes into
the same class.

This mirrors `TaomVolunteerModel`, which lives in `TroopProgression` and takes
`IRecruitmentAlignmentService` from another feature for the same reason.

## Configuration

### Config file: `Main/_Module/ModuleData/marriage_alignment/marriage_alignment_config.json`

| Field | Type | Default | Description |
|---|---|---|---|
| `enabled` | bool | `true` | Master toggle. When false nothing is blocked and the AI draw stays vanilla. |
| `applyToAi` | bool | `true` | When false, AI lords marry unrestricted. |
| `applyToPlayer` | bool | `true` | When false, the player clan marries unrestricted. |
| `steerAiPartnerSearch` | bool | `true` | Narrows the AI partner-clan draw. Turning this off keeps the block but restores vanilla's uniform draw, which is the rate collapse described above. |

Every field is a bool, so there is no parseable-but-invalid value to reject. If a numeric or string
field is ever added, the "Config Providers MUST Validate" rule applies to it in full.

`Reuse.Singleton`, so a JSON edit needs a full Bannerlord restart, not a save reload. MCM is live.

### MCM

Group `World/Marriage Alignment` (GroupOrder 50): Enable Marriage Alignment Block, Apply To Player,
Apply To AI Lords, Steer AI Partner Search. MCM overrides JSON at runtime.

Alignment data itself lives in `execution/alignment.json`, shared with Execution, Diplomacy,
AlignmentRecruitment, AlignmentDesertion, CaravanTrade and PrisonerRecruitment. This feature adds no
entries to it.

### Neutral semantics

The service deliberately does **not** call `IAlignmentService.AreEnemyAlignments`, whose Neutral
handling is inverted for this purpose: it returns `true` when either side is Neutral, so it treats
Neutral as an enemy of everyone and would bar every Umbar, Shaghana, Abanissa and Dunland hero from
marrying anybody. Four other TAOM services refuse to call it for the same reason.

`IsMarriageBlocked` is defined in terms of `AreCulturesCompatible` so the rule the model enforces
and the rule the AI draw is narrowed by cannot drift apart.

## Scope: future marriages only

The fix blocks **new** marriages. It does not annul existing ones, mutate hero state on load, or
touch children. A save where the pairing already happened keeps it.

That makes the fix hard to confirm by eye, since the evidence is an absence, so
`taom.print_marriages` exists as the measurement: it lists every married couple whose cultures sit
on opposite sides, read-only. Take a count, run the campaign forward, confirm it has not grown.

## Key files

| File | Purpose |
|---|---|
| `Main/Features/MarriageAlignment/MarriageAlignmentService.cs` | The pure side rule. No TaleWorlds types. |
| `Main/Features/MarriageAlignment/MarriageAlignmentConfig.cs` | JSON DTO. |
| `Main/Features/MarriageAlignment/MarriageAlignmentConfigProvider.cs` | Loads the JSON, falls back to defaults with a log line. |
| `Main/Features/MarriageAlignment/MarriageAlignmentSettingsProvider.cs` | Merges MCM over JSON. |
| `Main/Features/MarriageAlignment/MarriageAlignmentIoC.cs` | Three `Reuse.Singleton` registrations. |
| `Main/Features/MarriageAlignment/Hooks/Patch81_MarriageClanDraw.cs` | The transpiler plus its `Clan` boundary. |
| `Main/Features/MarriageAlignment/MarriageClanPoolCache.cs` | The per-culture candidate pools the draw picks from. |
| `Main/Features/MarriageAlignment/MarriageClanPoolStamp.cs` | When those pools go stale. Pure and tested. |
| `Main/Features/MarriageAlignment/Cheats/MarriageAlignmentCheats.cs` | `taom.print_marriages`. |
| `Main/Features/NazgulFamily/Models/TaomMarriageModel.cs` | The block, alongside the Ringwraith rule. |
| `Main/_Module/ModuleData/marriage_alignment/marriage_alignment_config.json` | Default config. |
| `Main/Features/TaomSettings.cs` | The 4 MCM knobs. |
| `Main/IoC.cs`, `Main/SubModule.cs` | Registration. |

## Patch81 fail-safes

`CandidateClansFor` returns vanilla's `Clan.All` unchanged on every degenerate path, so the draw is
never starved and the worst case is vanilla behaviour with the model still blocking:

1. Null clan or no `Campaign.Current`.
2. Feature disabled, AI half disabled, or steering disabled.
3. The considering clan's culture resolves Neutral or unknown (it may marry anyone anyway).
4. The filtered pool is empty. `MBRandom.RandomInt(0)` returns 0 and the indexer would throw.
5. Any exception.

The transpiler itself self-bails: if it does not find exactly two `Clan.All` reads it logs and
returns the instructions untouched. Cross-alignment marriages stay blocked in that case; only the
steering is lost.

The pool cache lives in `MarriageClanPoolCache`, keyed on the considering clan's culture, and
`MarriageClanPoolStamp` decides when it is stale from `Campaign.UniqueGameId` plus the clan count
plus the campaign day. The stamp keys on the id STRING rather than the `Campaign` object so a
finished campaign's whole object graph is not pinned alive by a static field
(`plans/001-cross-campaign-singleton-resets.md`).

Clan culture is only ever assigned at clan CREATION (vanilla sets it in `CreateSettlementRebelClan`
and `CreateCompanionToLordClan`; nothing reassigns an existing clan's culture), and every creation
path moves the clan count, so the count is what actually covers culture churn. This is **not** the
CultureConversion feature, which converts settlement cultures and leaves clan cultures alone.

The stamp is a separate pure class because it is the only real decision in the cache, and inside a
Harmony patch class no test could reach it.

## Tests

`TAOM.Tests/Features/MarriageAlignment/`:

- `MarriageAlignmentServiceTests` (28 cases). The full 3x3 side matrix for both
  `IsMarriageBlocked` and `AreCulturesCompatible`, one test per skip-guard branch, null and unknown
  culture ids, and the three-toggle truth table for `ShouldSteerAiPartnerSearch`. Both sides of every
  pairing are stubbed explicitly because `default(FactionSide)` is `Free`, so an unconfigured
  NSubstitute call silently returns Free.
- `MarriageAlignmentConfigProviderTests` (8 cases). Valid load, missing file, malformed JSON, empty
  object, the JSON `null` literal, all-toggles-off, caching, and the shipped file.
- `ShippedCultureAlignmentCoverageTests` (3 cases). Every culture used by a lord in `lords.xml` is
  classified in `alignment.json`, plus a floor on the extracted count so a stale regex cannot make
  the assertion vacuous, plus a pin on the exact #542 pairing.
- `MarriageAlignmentBindingTests` (2 cases, `TestCategory("BindingVerification")`). The target is
  still an instance method taking a `Clan` (the transpiler emits `Ldarg_1`), and `Clan.All` is still
  read exactly twice in `CheckNpcMarriages`. Reads the installed engine's IL through Harmony's
  offline `PatchProcessor.ReadMethodBody`.
- `MarriageClanPoolStampTests` (8 cases). First observation always invalidates, a repeat of the same
  triple does not, day advance / clan created / clan eliminated each do, a second campaign in the
  same process does, and the `-1` sentinel meeting a first real observation of `0` still invalidates
  (the shape behind the shader-precompilation sentinel-collision RCA).

`TaomMarriageModel` and the transpiler splice itself are thin engine boundaries, validated in game
per ADR-008.

## How to verify in game

1. `taom.print_patches` and confirm `Patch81_MarriageAlignment` applied rather than self-bailed.
2. `taom.print_marriages` for a baseline count of existing cross-alignment couples. Its header
   also reports the current toggle state, so a count taken with the feature off is not misread as
   the fix having broken.
3. Run several campaign years forward. The cross-alignment count must not grow, and Free clans must
   still be producing marriages (that is what the steering exists to preserve).

## Changelog

- **2026-09-06** Feature added. Issue #542.
- **2026-09-06** 5-agent `/deep-review`, 0 HIGH. The candidate-pool cache moved out of the patch
  into `MarriageClanPoolCache` + `MarriageClanPoolStamp` (the stamp now keys on
  `Campaign.UniqueGameId` rather than the `Campaign` object, which was pinning a finished campaign's
  object graph alive), an unreachable `Campaign.Current` guard was removed, and
  `taom.print_marriages` gained the toggle-state header. RCA:
  [rca-marriage-alignment-2026-09-06.md](../reviews/rca-marriage-alignment-2026-09-06.md).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/dev-console.md](./dev-console.md)
- [docs/features/nazgul-family.md](./nazgul-family.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
