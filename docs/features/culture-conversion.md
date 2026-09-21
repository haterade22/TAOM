# Culture Conversion

## Overview

When a town or castle is conquered by a clan of a different culture, it **gradually adopts the new owner's culture** — after the new owner holds it for a configurable number of days, the settlement (and its bound villages) flips its `Settlement.Culture`, **replaces its foreign-culture notables with new-culture ones**, begins recruiting the new owner's troops, spawns the new culture's militia, and loses the vanilla "foreign occupier" loyalty penalty. Conquering it back to its original culture reverts the change.

## Why This Exists

In vanilla Bannerlord (and therefore in TAOM before this feature), `Settlement.Culture` never changes on conquest. A Gondor city captured by Mordor keeps producing Gondor recruits **forever**, which breaks the LOTR fantasy of factions warring over Middle-earth. Two TAOM-specific wrinkles made the naive "just set the culture" fix insufficient:

1. **TAOM recruitment never reads `settlement.Culture`.** [`VolunteerRecruitmentService.GetVolunteerTroopId`](../../Main/Features/TroopProgression/VolunteerRecruitmentService.cs) resolves troops by a cascade of per-settlement → per-clan → notable-culture pools. ~81 settlements have hard-coded per-settlement pools that shadow everything, so changing the culture alone would not flip the troops. The feature adds a *converted-settlement branch* to that cascade.
2. **`Settlement.Culture` is not an engine-saved field.** It is re-read from XML on every load, so a runtime change reverts unless re-applied. The feature persists its own conversion records and re-applies them on `OnGameLoadedEvent`.

Most other TAOM systems (militia spawn chance, prosperity, wages, the loyalty *penalty*) already key on `OwnerClan.Culture` and follow the new owner automatically; the gaps this feature closes are **recruitment**, **vanilla militia troop *types*** (which read `settlement.Culture`), **notable spawn density**, and the settlement's **visible cultural identity**.

## Architecture

### Design challenge

Make conquered fiefs produce the new owner's troops, gradually, persistently, without (a) the stale per-settlement pools blocking the change, (b) losing the conversion across save/load, or (c) instantly pacifying every conquest (which removing the loyalty penalty would do if conversion were instant).

### Solution

A new feature module plus a surgical recruitment hook. Standard TAOM layering (ADR-002/007): thin behavior → service (all logic) → adapter (all TaleWorlds access).

```
OnSettlementOwnerChanged ─┐
DailyTickEvent ───────────┤→ CultureConversionBehavior ─→ ICultureConversionService ─→ ICultureConversionAdapter ─→ TaleWorlds
OnGameLoaded ─────────────┘   (thin, SyncData)              (pure decisions)              (Settlement/Culture/Notables)
                                     │
                                     └─ ICultureConversionStore  ←──── IsConverted() ──── VolunteerContextAdapter → recruitment branch
                                        (records, save format)
```

### Conversion model

Each managed town/castle has a [`SettlementConversionRecord`](../../Main/Features/CultureConversion/Domain/SettlementConversionRecord.cs):

- `OriginalCultureId` — the authored culture, captured the first time the fief is queued (before any override). Persisted so a reconquest *back* to the original is unambiguous.
- `AppliedCultureId` — the override currently applied (`null` = currently the original culture).
- `PendingStartDays` + `PendingTargetCultureId` — an in-progress conversion timer.

Flow:

1. **Conquest** (`OnSettlementOwnerChanged`, towns/castles only): if the new owner's culture differs from the fief's effective culture, start a hold-timer toward it — **gated** on the target being a recruitable culture (has a `CultureMap` pool) and, optionally, on not being player-owned. Same-culture transfers / rebellions cancel any timer. **Timer continuity (2026-07-07):** an owner change whose culture matches the already-pending target — the capture→grant double-fire, kingdom re-grants, barters, same-culture recaptures — leaves the running timer untouched instead of restarting it. Without this, a contested frontier fief re-queued forever and never converted (`castle_E6` queued 16× with zero completions in play-testing). A *different* culture taking over still restarts the timer toward the new target, and a recapture by the fief's effective culture still cancels it.
2. **Daily sweep** (`DailyTickEvent`): when `now − start ≥ RequiredHoldDays` (and loyalty ≥ floor if `RequireStableLoyalty`), apply the conversion — set `Settlement.Culture` on the town + bound villages, replace foreign-culture notables (below), clear notable volunteer slots (so recruits repopulate from the new pool), record the override.
3. **Restore**: if the target equals the original culture (reconquest by the original culture), the override record is *removed* entirely, restoring vanilla same-culture loyalty.
4. **Load** (`OnGameLoadedEvent`): re-apply every completed override (the engine reverted `Settlement.Culture` to XML on load).

### Recruitment integration

`Settlement.Culture` changing is necessary but not sufficient — recruitment ignores it. Two new fields on [`VolunteerContext`](../../Main/Features/TroopProgression/VolunteerContext.cs) (`IsConvertedSettlement`, `SettlementCultureId`) are populated by [`VolunteerContextAdapter`](../../Main/Adapters/VolunteerContextAdapter.cs) (which queries `ICultureConversionStore.IsConverted` for the settlement **or its bound parent**). In `GetVolunteerTroopId`, a converted settlement resolves `CultureMap[SettlementCultureId]` *before* the settlement/clan pools (which hold the original culture's regional troops), falling back to the standard cascade only if that culture somehow has no pool.

`IVolunteerRecruitmentService.HasCulturePool(cultureId)` reports whether a culture has a recruitment pool (`CultureMap` entry) so the conversion service never converts a fief to a culture it can't recruit for — minor/bandit cultures, and **playable cultures whose troop set isn't authored yet** (see "Known limitations" below). Rohan (`vlandia`) and Harad (`aserai`) culture-level pools were added 2026-06-02 (Codex review) so their conquests convert.

### Notable replacement (2026-07-03, issue #325)

Without it, existing notables keep their `Hero.Culture` forever: nothing in vanilla changes a living notable's culture, a notable dying at `Power >= NotableDisappearPowerLimit` (100) is replaced by a relative that **copies the dead notable's culture** (`NotablesCampaignBehavior.OnHeroKilled` → `CreateRelativeNotableHero`), and only rare low-power propertyless notables disappear for the weekly deficit refill to backfill from the (converted) `settlement.Culture.NotableTemplates`. So a Mordor-held Gondor town stayed run by Gondorians indefinitely.

At conversion completion, **after** `Settlement.Culture` flips (templates come from the NEW culture), the service replaces each still-alive notable whose culture ≠ target culture, in the town/castle and each bound village. Per notable, `CultureConversionAdapter.ReplaceNotable` runs an order-critical sequence:

1. Resolve + guard (`IsAlive`, `IsNotable`, `CurrentSettlement != null`).
2. **Template pre-check** — `HeroCreator.CreateNotable` NREs when the culture has no template for the occupation (`GetRandomTemplateByOccupation` returns null on an empty filtered list); skip + warn instead, keeping the old notable. Audited 2026-07-03: every conversion-eligible culture (all `taom_spcultures.xml` cultures + the 6 vanilla-id cultures re-templated in `spcultures.xslt`) covers all 5 notable occupations, so this fail-safe is a pure safety net.
3. Spawn the same-occupation replacement (`HeroCreator.CreateNotable` — engine places it in the settlement, grants gold/power, empty volunteer slots).
4. **Transfer property before removal** — workshops (`ChangeOwnerOfWorkshopAction.ApplyByDeath`), alleys (`Alley.SetOwner`), caravans (`CaravanPartyComponent.TransferCaravanOwnership`). `OnHeroKilled` destroys any caravans the victim still owns and the engine's death listeners reassign/null unmoved workshops/alleys.
5. Cancel (not transfer) any issue/quest — `IssueBase.CompleteIssueWithCancel()` returns alternative-solution troops, ends with `Issue == null`, so `ApplyByRemove`'s notable-has-quest assert and `IssueManager`'s death handling both no-op. Relations are deliberately **not** transferred — fresh standing with the occupiers.
6. **Zero power** (`AddPower(-Power)`) — otherwise removal of a power-≥100 notable spawns an old-culture relative heir, silently defeating the replacement.
7. `KillCharacterAction.ApplyByRemove` — vanilla's own notable-disappear path, safe on a property-less, issue-less, power-0 notable. Note: the default `isForced: true` bypasses `Hero.CanDie`, a deliberate divergence from vanilla's disappear path (which checks it) — replacement is an occupation-regime change, not a death roll.

A skipped notable never blocks the conversion itself. Replacement is a one-shot at conversion time — the on-load re-apply never repeats it. Restore-to-original conversions replace symmetrically (orc notables give way to returning Gondorians). Gated by `replaceNotablesOnConversion` / the MCM "Replace Notables On Conversion" toggle (default on).

### Why nulling volunteer slots is required

The vanilla daily refill (`RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement`) only fills `null` slots and *upgrades existing troops within their own tree* — it never re-rolls a populated base type. Without clearing the slots, the old culture's troops would persist (and keep upgrading) indefinitely. `ResetVolunteers` empties the 6 slots; they refill from the converted-culture pool on the next daily tick.

**Castle caveat:** vanilla `UpdateVolunteersOfNotablesInSettlement` early-returns for castles (`IsTown=false && IsVillage=false`) — vanilla never refills castle notables. For converted **towns/villages**, vanilla refills the cleared slots. For converted **castles**, the refill comes from [CastleRecruitment](castle-recruitment.md)'s `CastleNotableMaintainer` instead, which only ticks while `EnableCastleRecruitment` is on. So a castle converted with CastleRecruitment **disabled** has empty recruit slots until it's re-enabled — harmless, because castle recruitment (player menu + AI) is itself gated off when the toggle is off, and vanilla castles have no recruitment at all. The four cases (town/castle × CastleRecruitment on/off) are all safe; only the castle-off case leaves slots unfilled, and those slots are unused in that state.

### Garrison and militia replacement

Flipping `Settlement.Culture` changes what the fief *produces*. It does nothing about the troops already standing in it, and vanilla does nothing either.

**What vanilla actually does, verified against the v1.5.3 decompile.** `ChangeOwnerOfSettlementAction.ApplyInternal` special-cases exactly one route: a settlement taken **by siege** has its garrison party destroyed (`DestroyPartyAction.Apply`) and a brand-new empty one created, which is uncontroversial because `MapEvent.CaptureDefeatedPartyMembers` already moved the survivors into the captor's prisoner roster. **Every other route keeps the old garrison verbatim**: gift, barter, a king's decision, clan destruction, leaving a faction, and the plain default. A Gondor garrison stays Gondor under a Mordor owner indefinitely. The only recurring refill is `GarrisonRecruitmentCampaignBehavior`, which adds `town.MapFaction.BasicTroop` a few at a time: the owner faction's **tier-1 basic troop only**, no spread, alongside auto-recruitment drawing the settlement's own (old-culture) volunteers and whatever passing lords donate. `CultureObject` has no garrison party template at all, and `DefaultPartyTemplate` seeds a garrison exactly once, at new-game init.

Militia is simpler and more stubborn. No owner change touches `Settlement.MilitiaPartyComponent`, and the daily top-up in `Settlement.AddMilitiasToParty` reads `Culture.MeleeMilitiaTroop` / `RangedMilitiaTroop` and their elite pair. Once this feature writes `Settlement.Culture`, *new* militia already spawn correctly; the standing stack just persists and decays slowly.

So conversion re-mans both. Inside `ApplyConversion`, after the culture flip and the notable work, [`IGarrisonCultureSwapService`](../../Main/Features/CultureConversion/GarrisonSwap/IGarrisonCultureSwapService.cs) runs once for the town or castle and once for each bound village (a village has no garrison, so only its militia moves).

**Candidates come from the culture's recruitment pool, not from its troop tag.** This is the part that is easy to get wrong, and the first implementation did. Grouping `CharacterObject.Culture` looks equivalent and is not, for two reasons measured on 2026-09-21. Several cultures deliberately field another's line: Lothlorien recruits the Rivendell troops, Khand (`battania`) the Rhun line, Shaghana and Abanissa the Harad line. Their own culture tag carries almost nothing, and what it does carry is not a troop. Lothlorien's only non-hero `Soldier` is `gear_practice_dummy_lothlorien`, so a culture-tag index re-manned an entire captured city with Practice Dummies, and Khand's six were all guards and arena dummies. Separately, `CharacterObject.All` is every loaded module's characters, so a culture-tag index also swept in 73 arena dummies, settlement guards, caravan guards and the spider creature, plus vanilla Calradian troops for the six ids TAOM retags.

The index is therefore the upgrade-closure of `CultureMap[culture]`, minus militia troops. That is the same authority `OnSettlementConquered` already gates conversion on (`HasCulturePool`), so the set of cultures with a garrison index and the set a fief can convert to are identical by construction rather than by coincidence. It also gives the rule a plain meaning: **a fief's garrison is made of the troops that fief can recruit.** All 22 conversion targets resolve a usable index this way, with no junk in any of them.

Because a borrowing culture's troops carry the donor's tag, "already the right culture" is tested as *does the target culture field this troop* rather than *do the culture ids match*. Without that, every Lothlorien conversion would churn a correct Rivendell garrison into a different Rivendell garrison.

**Matching is like-for-like, and strength-neutral by construction.** [`TroopSwap`](../../Main/Features/CultureConversion/GarrisonSwap/Domain/TroopSwap.cs) carries one head count used for both halves of the swap, with the wounded proportion preserved, so a swap can neither farm nor gut a fief. [`TroopCultureMapper`](../../Main/Features/CultureConversion/GarrisonSwap/TroopCultureMapper.cs) walks a five-rung ladder against a per-culture index of `(role, tier)` cells:

1. Exact tier and role.
2. Same role, nearest tier **within two**, preferring the lower on a tie so a swap never hands out a free upgrade.
3. Same tier, walking a role fallback chain (`HorseArcher` to `Cavalry` to `Ranged` to `Infantry`; `Cavalry` to `Infantry`; `Ranged` to `Infantry`; `Infantry` to `Ranged`).
4. Same role at any tier distance.
5. Nearest populated tier, any role.

Nothing found means the stack is **kept exactly as it is** and logged, the same stance `ReplaceForeignNotables` takes for a missing occupation template. A hole in a culture's roster must never cost a fief its men.

The ladder is not decoration. Measured 2026-09-20 across the 16 `troops_*.xml` files: Mirkwood fields nothing at all at tiers 4, 5 or 6; Goblin, Blue Craig and the Misty Mountain orcs field no cavalry at any tier; Dunland, Dale and Umbar stop at tier 6, so a captured tier-8 Gondor stack has no same-tier target in their rosters. `GarrisonCultureCoverageTests` is the gate: it rebuilds each culture's index from the shipped XML and asserts the real mapper resolves every tier-and-role combination for every culture that is a legal conversion target (the same `HasCulturePool` predicate `OnSettlementConquered` uses), so a silent no-swap becomes a build failure.

Within a chosen cell the pick is a stable FNV-1a hash of `(settlementId, tier, role)`. Deliberately not `string.GetHashCode`, which is not guaranteed stable across processes: a garrison that re-rolled its troops on every game start would be a save-visible bug. Two towns converting to the same culture get different troops; the same town asked twice gets the same answer.

**Militia maps slot-for-slot** instead, which is far more reliable: of the 24 cultures in `taom_spcultures.xml`, the 16 that own settlements author the full four-slot set, so a culture's melee-basic militia simply becomes the new culture's melee-basic militia. The other 8 are minor factions that author none and have no recruitment pool, so they are never conversion targets; a resolved `CultureMilitiaTroops` for one of them is non-null with four empty slots, which is why the service tests `IsEmpty` rather than null. Only a stack that is not one of the old culture's four slots falls through to the ladder. This is why `ApplyConversion` captures the **pre-flip** culture: by the time the swap runs, `Settlement.Culture` already holds the new value, and matching against it would reduce every militia swap to the ladder.

Roster writes follow the engine's own idiom, in [`GarrisonCultureSwapAdapter`](../../Main/Adapters/GarrisonCultureSwapAdapter.cs). `TroopRoster.GetTroopRoster()` hands back the roster's live cached list, rebuilt only on the next call, so a mutating `foreach` does not throw on v1.5.3: it silently keeps enumerating pre-mutation copies, which is a worse failure than an exception. The apply therefore uses an index loop with `GetElementCopyAtIndex` plus `AddToCountsAtIndex(..., removeDepleted: false)` and one `RemoveZeroCounts()` in a `finally`, which is the idiom vanilla's own `Settlement.RemoveMilitiasFromParty` uses against these very rosters. Each swap adds before it removes, so a throw mid-loop leaves the roster equal or over, never short. `TroopRoster.RemoveTroop` is never called: it forwards a not-found index of -1 straight through and throws.

**What is left alone:** prisoners (in the settlement or the garrison party) are captives, not defenders; heroes in any roster; troops already of the target culture, which is the common case because the conquering lord donates his own; and any troop template carrying no culture at all.

**Co-op** needs nothing new. `CultureConversionBehavior.OnDailyTick` is already `IIsAuthority`-gated, so roster writes only ever happen on the host.

## Cross-feature interactions

Because conversion flips the shared `Settlement.Culture` field (and because other features read owner-vs-settlement culture), several intentional couplings are worth knowing — surfaced in the deep-review + Codex review:

- **RevoltTuning loyalty penalty (`TaomSettlementLoyaltyModel`).** Vanilla's `DefaultSettlementLoyaltyModel` applies `SettlementOwnerDifferentCultureLoyaltyEffect` (TAOM-tuned via [revolt-tuning.md](revolt-tuning.md), default −1.0) **only while `OwnerClan.Culture != Settlement.Culture`**. So a conquered fief keeps the "foreign occupier" loyalty penalty **for the entire hold period**, then loses it **the instant the conversion completes** (when `ApplyConversion` sets `Settlement.Culture`). This is intended — assimilation pacifies the fief — but the removal is unconditional, so a large negative `SettlementOwnerDifferentCultureLoyaltyEffect` set in RevoltTuning stops biting that settlement once it converts. Gradual conversion (the default) means the penalty phases out naturally; instant conversion (`requiredHoldDays: 1`) removes it almost immediately. No code couples the two features — the loyalty model simply reads the live `Settlement.Culture` that this feature mutates.

- **CultureMarketplace goods (during the hold window).** [CultureMarketplace](culture-marketplace.md) keys its daily item injection on `OwnerClan.Culture` (via `TownRosterAdapter.GetCurrentCultureId`), **not** `Settlement.Culture`. `OwnerClan.Culture` becomes the conqueror's culture *immediately on capture*, while this feature only flips `Settlement.Culture` after the hold period. So during the hold window a captured fief already stocks the **new** owner's market goods even though its **troops and loyalty** still reflect the original culture. After conversion both agree. This window is by design (CultureMarketplace deliberately tracks live ownership so conquest instantly shifts market identity) — recorded here so the goods-vs-troops lag isn't mistaken for a bug.

  **Reading a marketplace log line: `GetCurrentCultureId` means two different things.** `TownRosterAdapter.GetCurrentCultureId(Settlement)` returns `settlement?.OwnerClan?.Culture?.StringId` — the **owner's** culture. The identically-named `CultureConversionAdapter.GetCurrentCultureId(string)` returns `Settlement.Find(id)?.Culture?.StringId` — the **settlement's** own. So a `[CultureMarketplace] <settlement> (<culture>)` line names the owner clan's culture and is **not** evidence that a conversion has applied; during the hold window — or permanently, on a day-1 mismatch (see Known limitations) — the two disagree by design. That misread cost real time in the 2026-08-04 Khand investigation, where `[CultureMarketplace] town_K1 (battania)` looked like proof the town had already converted while its `Settlement.Culture` was still `khuzait`. The per-town lines have since been rolled into one daily `MarketplaceDailyDigest` line, but the two same-named adapter methods remain — check which adapter emitted a culture id before treating it as the settlement's.

- **Other live `Settlement.Culture` readers that follow conversion.** Once converted, these vanilla/TAOM systems pick up the new culture automatically (intended — the fief's *identity* changed): vanilla **militia troop types** (`Settlement.Culture.MeleeMilitiaTroop` etc.); vanilla **Citizenship policy** loyalty, which keys on the same `OwnerClan.Culture == Settlement.Culture` comparison as the penalty above and flips between +0.5 and −0.5 on conversion; `TaomNotableSpawnModel` **notable-spawn density** (keyed on `settlement.Culture`); and `TaomTournamentModel` **tournament reward pools** (built from `Town.Culture` → `Settlement.Culture`, so a converted town's tournament prizes become the new culture's). None of these need code changes — they read the field this feature mutates.

## Known limitations

- **A day-1 culture/owner mismatch never converts — the feature only ever sees *changes*.** Records are seeded exclusively by `OnSettlementConquered` (fired off `OnSettlementOwnerChangedEvent`), and `RunDailyChecks` only drains records the store already holds. A settlement whose culture differed from its owner's **from game start** never fires an owner-change event, so it is never enqueued and never converts — no matter how low "Days To Convert" goes, and regardless of every other gate. This is a blind spot in the trigger, not a tuning problem. It is what let TAOM's entire Khand cluster sit as `khuzait`-culture (Easterling) settlements under `battania` (Variag) owners indefinitely: the 2026-08-04 crash bundle shows 90+ in-game days with zero conversion lines. **The fix for a day-1 mismatch is authored data, not conversion** — the 26 mistagged K-series settlements were retagged to `battania` in the live `TAOM_Map/ModuleData/settlements.xml` on 2026-08-04, and `python tools/validate_moduledata.py` now raises `LANDLESS_CULTURE` so an authored culture that owns no settlement fails validation instead of shipping. The same mismatch left the Variag culture landless, which is what crashed vanilla's `HeroSpawnCampaignBehavior.SpawnLordParty` — see [lord-spawn-guard.md](./lord-spawn-guard.md) and issue #374.
- **The garrison and militia swap is a one-shot, so older saves are not retrofitted.** It fires inside `ApplyConversion` only, never in the on-load re-apply, which is what stops a reload re-manning a garrison the player has since rebuilt by hand. A fief that converted before this shipped (2026-09-21) keeps its old-culture troops until it is reconquered and re-converted. Same rule notable replacement already follows.
- **A few cultures now promote low-tier stacks rather than demote them.** Because candidates come from the recruitment pool, a culture whose pool starts high has no low cells at all: Mirkwood's index is tiers 7 to 10, Lindon's and Rivendell's start at 4. A tier-0 stack converting to Mirkwood is therefore promoted to tier 7 by the ladder's last rung. Under the older culture-tag index those low cells existed, but what filled them was `guard_mirkwood` and its siblings, which are not troops a culture garrisons a fief with. Promotion is the better of the two, and it is bounded by how few such stacks exist.
- **A conversion target with a hole in its roster keeps those stacks.** The mapper's last rung fails safe, so a stack it cannot place anywhere in the new culture is left exactly as it is and logged as a warning. `GarrisonCultureCoverageTests` proves every current conversion target can place every tier and role, so this is a guard against future data, not a live gap.
- **Cross-culture interruption resets conversion progress.** The hold is *uninterrupted* by design: if the fief's effective culture retakes it mid-timer, the timer cancels, and a later reconquest starts the full `RequiredHoldDays` again. Same-culture ownership churn no longer resets the clock (see Timer continuity above), but a fief that genuinely flips between two *cultures* faster than the hold period never converts — lower "Days To Convert" in MCM if your borders are that hot.
- **Pre-feature converted saves keep their old-culture notables.** Notable replacement fires only inside `ApplyConversion`; the on-load re-apply deliberately never replaces. A settlement converted before this shipped (2026-07-03) is not retroactively fixed — it catches up only if reconquered and re-converted.
- **A culture missing an occupation template keeps that notable.** The template pre-check skips-with-log rather than crash (currently unreachable for real cultures — see the coverage audit note above).
- **The old notable's gold evaporates; the replacement starts with vanilla's 10000.** Notables have no clan, so `ApplyByRemove` routes their gold nowhere. Acceptable — notable gold is cosmetic.
- **Cultures without an authored recruitment pool don't convert.** Conversion only triggers when the new owner's culture has a `CultureMap` recruitment pool (otherwise the converted fief couldn't produce that culture's troops). Mirkwood and Umbar were gapped until 2026-06-10, when the recruitment-reachability fix wired `CultureMap["mirkwood"]` = `mirkwood_recruit` and `CultureMap["umbar"]` = `aux_basic` + `umbar_elite`. **Khand (`battania`) was the last gap and closed 2026-08-04**: `CultureMap["battania"]` now aliases the khuzait/Rhun pool (`VolunteerRecruitmentService.Rhun.cs`), so a fief a Variag clan takes can finally convert — Khand has no roster of its own, so it recruits Rhun troops until one is authored. Every playable culture now converts; the `HasCulturePool_PlayableCultureWithoutTroopSet_ReturnsFalse_KnownGap` test that pinned the gap was retired and `battania` moved into `HasCulturePool_PlayableCultureWithTroops_ReturnsTrue`. See [volunteer-recruitment.md](./volunteer-recruitment.md).

## Configuration

`Main/_Module/ModuleData/culture_conversion/culture_conversion_config.json` (loaded + validated by [`CultureConversionConfigProvider`](../../Main/Features/CultureConversion/CultureConversionConfigProvider.cs), `Reuse.Singleton` → changes need a full game restart):

| Field | Default | Meaning |
|-------|---------|---------|
| `enabled` | `true` | Master toggle for new conversions (existing overrides still re-apply). |
| `requiredHoldDays` | `1` | Days the new owner must hold a cross-culture fief before it converts (`[1, 100000]`). Shipped at 1 = near-instant conversion on capture; raise it for slower, more gradual assimilation. |
| `requireStableLoyalty` | `false` | If true, also wait for loyalty ≥ `minLoyaltyToConvert`. |
| `minLoyaltyToConvert` | `50` | Loyalty floor (`[0, 100]`, NaN/∞-guarded) when the loyalty gate is on. |
| `convertPlayerOwnedSettlements` | `true` | If false, the player's own conquests never convert (AI conquests still do). |
| `replaceNotablesOnConversion` | `true` | If false, conversion flips culture/recruitment but leaves the existing notables in place. |
| `replaceGarrisonOnConversion` | `true` | If false, the standing garrison keeps its old culture's troops. |
| `replaceMilitiaOnConversion` | `true` | If false, the standing militia keeps its old culture's troops (newly spawned militia still follow the new culture). |
| `replaceGarrisonInPlayerFiefs` | `true` | If false, neither swap touches a fief owned by the player's clan. AI fiefs still swap, and the player's fief still changes culture. |

MCM knobs (merged over JSON by [`CultureConversionSettingsProvider`](../../Main/Features/CultureConversion/CultureConversionSettingsProvider.cs), group "Culture Conversion"): **Enable Culture Conversion**, **Days To Convert** (1-365), **Require Stable Loyalty**, **Replace Notables On Conversion**, **Replace Garrison On Conversion**, **Replace Militia On Conversion**, **Replace Garrison In Your Own Fiefs**. `minLoyaltyToConvert` and `convertPlayerOwnedSettlements` are JSON-only (advanced).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CultureConversion/Domain/SettlementConversionRecord.cs` | Per-fief state + composite save serialization (R-format, NaN-guarded). |
| `Main/Features/CultureConversion/CultureConversionStore.cs` | Singleton record store + save round-trip; `IsConverted` query for recruitment. |
| `Main/Features/CultureConversion/CultureConversionService.cs` | All conversion logic (queue / daily complete / restore / re-apply). |
| `Main/Features/CultureConversion/Hooks/CultureConversionBehavior.cs` | Thin `CampaignBehaviorBase` — events + SyncData + new-campaign reset guard. |
| `Main/Features/CultureConversion/GarrisonSwap/TroopCultureMapper.cs` | Pure tier-and-role matching ladder; no TaleWorlds types. |
| `Main/Features/CultureConversion/GarrisonSwap/GarrisonCultureSwapService.cs` | Reads both rosters, applies the mapper's plan, owns the three toggles. |
| `Main/Features/CultureConversion/GarrisonSwap/Domain/CultureTroopIndex.cs` | Per-culture `(role, tier)` troop index the mapper matches against. |
| `Main/Adapters/GarrisonCultureSwapAdapter.cs` | Boundary: garrison + militia rosters, culture troop/militia data, the player notice. |
| `Main/Adapters/CultureConversionAdapter.cs` | Boundary: `Settlement.Find`, `Culture`, `BoundVillages`, `Town.Loyalty`, notable `VolunteerTypes`, notable snapshot + replacement sequence. |
| `Main/Features/CultureConversion/Domain/ConvertibleNotable.cs` | Notable snapshot DTO (`HeroId`, `CultureId`, `IsAlive`) crossing the adapter boundary. |
| `Main/Features/CultureConversion/CultureConversionConfig*.cs` | Config POCO + validating provider. |
| `Main/Features/CultureConversion/CultureConversionSettingsProvider.cs` | MCM-over-JSON merge. |
| `Main/Features/CultureConversion/CultureConversionIoC.cs` | DryIoc registrations (all `Reuse.Singleton`). |
| `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` | `HasCulturePool` + converted-settlement branch in `GetVolunteerTroopId`. |
| `Main/Adapters/VolunteerContextAdapter.cs` | Populates `IsConvertedSettlement` / `SettlementCultureId`. |
| `Main/_Module/ModuleData/culture_conversion/culture_conversion_config.json` | Default config. |

## Dependencies

- `IVolunteerRecruitmentService.HasCulturePool` (gates conversion targets to recruitable cultures).
- `ICultureObjectAdapter` (resolves a culture StringId → `CultureObject`).
- Reuses `Settlement.Find`, `CampaignEvents.OnSettlementOwnerChangedEvent` / `DailyTickEvent` / `OnGameLoadedEvent`.
- Persistence pattern mirrors `MessengerStateStore` / `PendingMessenger`.

## Tests

| File | Coverage |
|------|----------|
| `TAOM.Tests/Features/CultureConversion/CultureConversionServiceTests.cs` | Queue / cancel / gate (minor-culture, player-owned, disabled) / daily complete / loyalty gate / stale-timer drop / reconquest-to-original removal / re-apply. Notable replacement: town+villages, culture-flip-first ordering, skips (same-culture, dead, toggle-off), fail-continue, restore symmetry, no re-apply replacement. |
| `TAOM.Tests/Features/CultureConversion/SettlementConversionRecordTests.cs` | Effective culture, pending lifecycle, serialize round-trip, NaN/∞ pending drop, structural-failure reject. |
| `TAOM.Tests/Features/CultureConversion/CultureConversionStoreTests.cs` | Put/get/remove, `IsConverted`, serialize round-trip, malformed-entry drop. |
| `TAOM.Tests/Features/CultureConversion/CultureConversionConfigProviderTests.cs` | Valid/missing/malformed/out-of-range/NaN-loyalty validation. |
| `TAOM.Tests/Features/CultureConversion/TroopCultureMapperTests.cs` | Every rung of the matching ladder, tie-break preferring the lower tier, role fallback chains, head-count and wounded preservation with clamping, determinism and index-order independence, militia slot mapping and its fallbacks, fail-safe on an empty culture. |
| `TAOM.Tests/Features/CultureConversion/GarrisonCultureSwapServiceTests.cs` | Each of the three toggles in isolation, the player-fief toggle, the notice firing only for player fiefs and only when troops moved, degraded culture data, unmapped stacks reported and left alone. |
| `TAOM.Tests/Features/CultureConversion/CultureConversionShippedConfigTests.cs` | Pins the shipped `culture_conversion_config.json` against the compiled defaults, keys included. `lint_docs.py`'s config-drift check only reads fenced json blocks and this doc uses a table, so its clean result was vacuous here. |
| `TAOM.Tests/Features/CultureConversion/GarrisonCultureCoverageTests.cs` | Data gate, seeded from the recruitment pools rather than the troop XML: every conversion target resolves every tier and role, no target indexes a practice dummy or a guard, militia slot sets complete with real troops, minor factions pinned as non-targets. |
| `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentConversionTests.cs` | `HasCulturePool` + converted-branch bypass of settlement/clan pools + non-converted/no-pool fallback. |

**Not unit-tested (in-game verification):** `Settlement.Culture` write surviving save/load via re-apply; militia troop-type change; `VolunteerTypes` reset → daily refill flipping recruits; MCM wiring; the adapter's `ReplaceNotable` engine sequence (spawn/transfer/cancel/remove — verify workshops keep owners, caravans survive, quest cancels cleanly, no old-culture heir appears).

## How-To

**Make conquest convert faster/slower:** edit `requiredHoldDays` in the JSON (or the "Days To Convert" MCM knob). Restart the game for JSON changes (singleton provider).

**Only convert pacified cities:** set `requireStableLoyalty: true` and tune `minLoyaltyToConvert`.

**Stop the player's own conquests from converting:** set `convertPlayerOwnedSettlements: false`.

**Disable entirely:** "Enable Culture Conversion" off (or `enabled: false`). Already-converted settlements stay converted (re-applied on load); only *new* conversions stop.

## Performance

`OnSettlementConquered` fires only on ownership changes; the daily sweep iterates **only records with a pending timer** (typically a handful), each a dictionary lookup + a few adapter reads. No per-frame or per-settlement-per-day global scan. Conversion records are the only persisted state, serialized as one `Dictionary<string,string>`.

## Changelog

- 2026-09-21: **Garrison and militia re-manned on conversion** (issue #632). A converted fief's standing garrison, its militia and each bound village's militia are now replaced with the new culture's equivalent troops, matched by tier and battlefield role at identical head count (wounded included). Five-rung fallback ladder for the ragged cells in TAOM's rosters, slot-for-slot mapping for militia, deterministic per-settlement pick, and a fail-safe that keeps any stack it cannot place. Three new toggles: `replaceGarrisonOnConversion`, `replaceMilitiaOnConversion` and a dedicated `replaceGarrisonInPlayerFiefs`. Motivated by the engine behaviour documented above: only a siege clears a garrison, so every peaceful transfer left the old culture's troops in place indefinitely.
- 2026-08-04 — **Khand becomes a conversion target; the day-1 blind spot documented.** `CultureMap["battania"]` (aliasing the khuzait/Rhun pool) landed with the Khand settlement retag, so `HasCulturePool` no longer gates Variag conquests out. No conversion logic changed: the retag itself was needed because a settlement whose culture never matched its owner is never enqueued by `OnSettlementConquered` and so never converts. See [lord-spawn-guard.md](./lord-spawn-guard.md) and issue #374.
- 2026-07-27 — **Log trim.** The `already pending toward X — timer continues` DEBUG is gone (the timer-continuity guard and its early-return are unchanged): it existed to prove the clock wasn't restarting across a **45**-day hold, and the shipped default is now **1** day, so there is no clock left to protect. `queued for conversion` now logs only when `requiredHoldDays > 1` — at the default it duplicates the `converted`/`restored` INFO that follows one campaign day later. Both terminal lines stay at INFO. A 4-hour session showed 571 queues against 361 conversions + 204 restorations (a 99% completion rate), which is the direct evidence the 2026-07-07 continuity fix holds. Measured 1,697 → 565 lines/session.
- 2026-07-07 — **Timer continuity**: same-culture ownership changes (fief grant after capture, re-grants, barters, same-culture recaptures) no longer restart the hold-timer — the clock continues from the original capture. Cancel/stale-drop paths now log at DEBUG for diagnosability. Root-caused from a play-test where `castle_E6` queued 16× toward `khuzait` without ever converting.
- 2026-07-03 — **Notable replacement** (issue #325): conversion now replaces foreign-culture notables with same-occupation notables from the new culture's templates (a Mordor-converted Gondor town gets orc merchants/gang leaders). Property (workshops/alleys/caravans) transfers to the replacements; relations reset; active issues cancel; power zeroed pre-removal to suppress the vanilla old-culture heir spawn. New `replaceNotablesOnConversion` JSON field + "Replace Notables On Conversion" MCM toggle (default on).
- 2026-06-02 — Introduced the `Main/Features/CultureConversion/` module: conquered cross-culture towns/castles (and bound villages) gradually flip `Settlement.Culture` after a configurable hold period, recruiting the new owner's troops and dropping the foreign-occupier loyalty penalty; reconquest-to-original reverts. Adds a converted-settlement recruitment branch (`HasCulturePool` gate + `VolunteerContext` fields), persisted records re-applied on load, JSON + MCM "Culture Conversion" config. Includes deep-review + Codex fixes (stale-record purge on culture-removal, `HasCulturePool` playable-culture gate adding Rohan/Harad).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/culture-marketplace.md](./culture-marketplace.md)
- [docs/features/revolt-tuning.md](./revolt-tuning.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/settlements.md](../modding/settlements.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
