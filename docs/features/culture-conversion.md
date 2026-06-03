# Culture Conversion

## Overview

When a town or castle is conquered by a clan of a different culture, it **gradually adopts the new owner's culture** — after the new owner holds it for a configurable number of days, the settlement (and its bound villages) flips its `Settlement.Culture`, begins recruiting the new owner's troops, spawns the new culture's militia, and loses the vanilla "foreign occupier" loyalty penalty. Conquering it back to its original culture reverts the change.

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

1. **Conquest** (`OnSettlementOwnerChanged`, towns/castles only): if the new owner's culture differs from the fief's effective culture, start a hold-timer toward it — **gated** on the target being a recruitable culture (has a `CultureMap` pool) and, optionally, on not being player-owned. Same-culture transfers / rebellions cancel any timer.
2. **Daily sweep** (`DailyTickEvent`): when `now − start ≥ RequiredHoldDays` (and loyalty ≥ floor if `RequireStableLoyalty`), apply the conversion — set `Settlement.Culture` on the town + bound villages, clear notable volunteer slots (so recruits repopulate from the new pool), record the override.
3. **Restore**: if the target equals the original culture (reconquest by the original culture), the override record is *removed* entirely, restoring vanilla same-culture loyalty.
4. **Load** (`OnGameLoadedEvent`): re-apply every completed override (the engine reverted `Settlement.Culture` to XML on load).

### Recruitment integration

`Settlement.Culture` changing is necessary but not sufficient — recruitment ignores it. Two new fields on [`VolunteerContext`](../../Main/Features/TroopProgression/VolunteerContext.cs) (`IsConvertedSettlement`, `SettlementCultureId`) are populated by [`VolunteerContextAdapter`](../../Main/Adapters/VolunteerContextAdapter.cs) (which queries `ICultureConversionStore.IsConverted` for the settlement **or its bound parent**). In `GetVolunteerTroopId`, a converted settlement resolves `CultureMap[SettlementCultureId]` *before* the settlement/clan pools (which hold the original culture's regional troops), falling back to the standard cascade only if that culture somehow has no pool.

`IVolunteerRecruitmentService.HasCulturePool(cultureId)` reports whether a culture has a recruitment pool (`CultureMap` entry) so the conversion service never converts a fief to a culture it can't recruit for — minor/bandit cultures, and **playable cultures whose troop set isn't authored yet** (see "Known limitations" below). Rohan (`vlandia`) and Harad (`aserai`) culture-level pools were added 2026-06-02 (Codex review) so their conquests convert.

### Why nulling volunteer slots is required

The vanilla daily refill (`RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement`) only fills `null` slots and *upgrades existing troops within their own tree* — it never re-rolls a populated base type. Without clearing the slots, the old culture's troops would persist (and keep upgrading) indefinitely. `ResetVolunteers` empties the 6 slots; they refill from the converted-culture pool on the next daily tick.

**Castle caveat:** vanilla `UpdateVolunteersOfNotablesInSettlement` early-returns for castles (`IsTown=false && IsVillage=false`) — vanilla never refills castle notables. For converted **towns/villages**, vanilla refills the cleared slots. For converted **castles**, the refill comes from [CastleRecruitment](castle-recruitment.md)'s `CastleNotableMaintainer` instead, which only ticks while `EnableCastleRecruitment` is on. So a castle converted with CastleRecruitment **disabled** has empty recruit slots until it's re-enabled — harmless, because castle recruitment (player menu + AI) is itself gated off when the toggle is off, and vanilla castles have no recruitment at all. The four cases (town/castle × CastleRecruitment on/off) are all safe; only the castle-off case leaves slots unfilled, and those slots are unused in that state.

## Cross-feature interactions

Because conversion flips the shared `Settlement.Culture` field (and because other features read owner-vs-settlement culture), several intentional couplings are worth knowing — surfaced in the deep-review + Codex review:

- **RevoltTuning loyalty penalty (`TaomSettlementLoyaltyModel`).** Vanilla's `DefaultSettlementLoyaltyModel` applies `SettlementOwnerDifferentCultureLoyaltyEffect` (TAOM-tuned via [revolt-tuning.md](revolt-tuning.md), default −1.0) **only while `OwnerClan.Culture != Settlement.Culture`**. So a conquered fief keeps the "foreign occupier" loyalty penalty **for the entire hold period**, then loses it **the instant the conversion completes** (when `ApplyConversion` sets `Settlement.Culture`). This is intended — assimilation pacifies the fief — but the removal is unconditional, so a large negative `SettlementOwnerDifferentCultureLoyaltyEffect` set in RevoltTuning stops biting that settlement once it converts. Gradual conversion (the default) means the penalty phases out naturally; instant conversion (`requiredHoldDays: 1`) removes it almost immediately. No code couples the two features — the loyalty model simply reads the live `Settlement.Culture` that this feature mutates.

- **CultureMarketplace goods (during the hold window).** [CultureMarketplace](culture-marketplace.md) keys its daily item injection on `OwnerClan.Culture` (via `TownRosterAdapter.GetCurrentCultureId`), **not** `Settlement.Culture`. `OwnerClan.Culture` becomes the conqueror's culture *immediately on capture*, while this feature only flips `Settlement.Culture` after the hold period. So during the hold window a captured fief already stocks the **new** owner's market goods even though its **troops and loyalty** still reflect the original culture. After conversion both agree. This window is by design (CultureMarketplace deliberately tracks live ownership so conquest instantly shifts market identity) — recorded here so the goods-vs-troops lag isn't mistaken for a bug.

- **Other live `Settlement.Culture` readers that follow conversion.** Once converted, these vanilla/TAOM systems pick up the new culture automatically (intended — the fief's *identity* changed): vanilla **militia troop types** (`Settlement.Culture.MeleeMilitiaTroop` etc.); vanilla **Citizenship policy** loyalty, which keys on the same `OwnerClan.Culture == Settlement.Culture` comparison as the penalty above and flips between +0.5 and −0.5 on conversion; `TaomNotableSpawnModel` **notable-spawn density** (keyed on `settlement.Culture`); and `TaomTournamentModel` **tournament reward pools** (built from `Town.Culture` → `Settlement.Culture`, so a converted town's tournament prizes become the new culture's). None of these need code changes — they read the field this feature mutates.

## Known limitations

- **Cultures without an authored recruitment pool don't convert.** Conversion only triggers when the new owner's culture has a `CultureMap` recruitment pool (otherwise the converted fief couldn't produce that culture's troops). As of 2026-06-02 three fief-owning playable cultures lack a recruitable basic-troop set and are therefore **intentionally gated off** as conversion targets: **Khand (`battania`)** — no troop file authored; **Mirkwood** — troops exist but none flagged `is_basic_troop`; **Umbar** — only boss/elite troops authored. A fief these factions conquer keeps its original culture until their recruitment pools are authored (a recruitment-system task, tracked separately from this feature). All other playable cultures convert. The `HasCulturePool_PlayableCultureWithoutTroopSet_ReturnsFalse_KnownGap` test pins this gap so it's visible.

## Configuration

`Main/_Module/ModuleData/culture_conversion/culture_conversion_config.json` (loaded + validated by [`CultureConversionConfigProvider`](../../Main/Features/CultureConversion/CultureConversionConfigProvider.cs), `Reuse.Singleton` → changes need a full game restart):

| Field | Default | Meaning |
|-------|---------|---------|
| `enabled` | `true` | Master toggle for new conversions (existing overrides still re-apply). |
| `requiredHoldDays` | `45` | Days the new owner must hold a cross-culture fief before it converts (`[1, 100000]`). |
| `requireStableLoyalty` | `false` | If true, also wait for loyalty ≥ `minLoyaltyToConvert`. |
| `minLoyaltyToConvert` | `50` | Loyalty floor (`[0, 100]`, NaN/∞-guarded) when the loyalty gate is on. |
| `convertPlayerOwnedSettlements` | `true` | If false, the player's own conquests never convert (AI conquests still do). |

MCM knobs (merged over JSON by [`CultureConversionSettingsProvider`](../../Main/Features/CultureConversion/CultureConversionSettingsProvider.cs), group "Culture Conversion"): **Enable Culture Conversion**, **Days To Convert** (1–365), **Require Stable Loyalty**. `minLoyaltyToConvert` and `convertPlayerOwnedSettlements` are JSON-only (advanced).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CultureConversion/Domain/SettlementConversionRecord.cs` | Per-fief state + composite save serialization (R-format, NaN-guarded). |
| `Main/Features/CultureConversion/CultureConversionStore.cs` | Singleton record store + save round-trip; `IsConverted` query for recruitment. |
| `Main/Features/CultureConversion/CultureConversionService.cs` | All conversion logic (queue / daily complete / restore / re-apply). |
| `Main/Features/CultureConversion/Hooks/CultureConversionBehavior.cs` | Thin `CampaignBehaviorBase` — events + SyncData + new-campaign reset guard. |
| `Main/Adapters/CultureConversionAdapter.cs` | Boundary: `Settlement.Find`, `Culture`, `BoundVillages`, `Town.Loyalty`, notable `VolunteerTypes`. |
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
| `TAOM.Tests/Features/CultureConversion/CultureConversionServiceTests.cs` | Queue / cancel / gate (minor-culture, player-owned, disabled) / daily complete / loyalty gate / stale-timer drop / reconquest-to-original removal / re-apply. |
| `TAOM.Tests/Features/CultureConversion/SettlementConversionRecordTests.cs` | Effective culture, pending lifecycle, serialize round-trip, NaN/∞ pending drop, structural-failure reject. |
| `TAOM.Tests/Features/CultureConversion/CultureConversionStoreTests.cs` | Put/get/remove, `IsConverted`, serialize round-trip, malformed-entry drop. |
| `TAOM.Tests/Features/CultureConversion/CultureConversionConfigProviderTests.cs` | Valid/missing/malformed/out-of-range/NaN-loyalty validation. |
| `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentConversionTests.cs` | `HasCulturePool` + converted-branch bypass of settlement/clan pools + non-converted/no-pool fallback. |

**Not unit-tested (in-game verification):** `Settlement.Culture` write surviving save/load via re-apply; militia troop-type change; `VolunteerTypes` reset → daily refill flipping recruits; MCM wiring.

## How-To

**Make conquest convert faster/slower:** edit `requiredHoldDays` in the JSON (or the "Days To Convert" MCM knob). Restart the game for JSON changes (singleton provider).

**Only convert pacified cities:** set `requireStableLoyalty: true` and tune `minLoyaltyToConvert`.

**Stop the player's own conquests from converting:** set `convertPlayerOwnedSettlements: false`.

**Disable entirely:** "Enable Culture Conversion" off (or `enabled: false`). Already-converted settlements stay converted (re-applied on load); only *new* conversions stop.

## Performance

`OnSettlementConquered` fires only on ownership changes; the daily sweep iterates **only records with a pending timer** (typically a handful), each a dictionary lookup + a few adapter reads. No per-frame or per-settlement-per-day global scan. Conversion records are the only persisted state, serialized as one `Dictionary<string,string>`.
