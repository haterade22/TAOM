# Alignment Desertion

## Overview

Each day, troops whose culture is opposed in alignment to their lord desert. An Evil-aligned lord
(Mordor, Isengard, Gundabad, Dol Guldur, Easterlings, Khand, Harad, Dunland, the orc kingdoms) sheds
Good (Free) troops; a Good-aligned lord (Gondor, Rohan, the Elf realms, the Dwarf realms, Dale) sheds
Evil troops. Applies to mobile parties (including army members) and settlement garrisons, for both AI
lords and the player. Default rate is 50% of each opposed troop type per day (minimum 1).

## Why This Exists

In a Middle-earth setting, the free peoples and the servants of the Shadow do not fight side by side.
Yet conquest, capture-and-recruit, and mixed garrisons routinely leave a lord holding troops of the
opposing side, which lingers indefinitely.

- **Vanilla behavior:** Troops never desert over cultural/factional opposition; a captured roster stays
  forever.
- **TAOM requirement:** Mixed-alignment forces should self-purge quickly so armies, parties, and
  garrisons settle to a lore-consistent composition.
- **Without this feature:** Evil lords field Good elites (and vice versa) permanently, breaking immersion
  and removing any pressure to recruit from one's own side.

## Architecture

### Design Challenge

The existing `IAlignmentService` (Execution feature) is keyed by the IDs in `execution/alignment.json`,
which a party/garrison **owner** resolves cleanly via its kingdom StringId. A **troop**, however, only
carries a culture StringId — and for the two custom factions the culture id differs from the kingdom id
(Gondor: kingdom `empire_w`, culture `gondor`; Mordor: kingdom `empire_s`, culture `mordor`). Those two
culture ids were absent from `alignment.json`, so a naive lookup would side Gondor/Mordor troops as
Neutral and they would never desert.

### Solution Approach

- A new `CampaignBehavior` subscribes to `DailyTickPartyEvent` (gated to `IsLordParty || IsMainParty` —
  the event fires for ALL mobile parties, so caravans/villagers/militia/bandits/garrisons are excluded)
  and `DailyTickSettlementEvent` (garrisons). It does only engine I/O: snapshot the roster into POCOs, call
  the pure service, apply the returned removals via `TroopRoster.AddToCounts`. No Harmony patch, no
  GameModel, no adapter (the roster I/O stays in the thin behavior, mirroring the SpecialResources
  desertion precedent), no `SyncData` (desertion is recomputed daily from live rosters).
- A pure `AlignmentDesertionService` owns the decision matrix and is 100% unit-tested.
- Side resolution reuses the Execution `IAlignmentService`: owner side via `GetKingdomSide`, troop side
  via a new `GetCultureSide` (same id→side table). `gondor`/`mordor` were added to `alignment.json` so
  every real troop culture resolves; bandit/minor cultures stay absent → Neutral → never desert.

### Decision matrix (service)

Given owner kingdom id, `isPlayerOwned`, `isGarrison`, and a roster snapshot:

1. Master toggle off / empty roster → none.
2. Owner gate: player + `!ApplyToPlayer` → none; AI + `!ApplyToAi` → none.
3. Location gate: garrison + `!ApplyToGarrisons` → none; party + `!ApplyToParties` → none.
4. Owner side Neutral (includes kingdomless/independent) → none. (Mercenary clans keep `Kingdom` set to
   their employer, so they resolve to the employer's side and DO purge — not exempt.)
5. Rate ≤ 0 → none (a 0% slider means "no desertion"; the min-1 floor does not fire at rate 0).
6. Per troop: skip heroes, zero-count, Neutral-culture, and same-side troops. Opposed troops desert
   `min(count, max(1, (int)(count × rate)))`. Symmetric — Free sheds Evil and Evil sheds Free.

The behavior gates the party path to `IsLordParty || IsMainParty` (lord field parties incl. army members
+ the player's main party). Caravans, villagers, militia, bandits, and garrison parties are excluded from
the party path; garrisons desert via the settlement tick under their own MCM gate.

### Component Diagram

```
alignment_desertion_config.json        execution/alignment.json
        |                                       |
 AlignmentDesertionConfigProvider       AlignmentService (GetKingdomSide / GetCultureSide)
        |  (MCM over JSON via                   |
 AlignmentDesertionSettingsProvider)            |
        \______________   ______________________/
                       \ /
            AlignmentDesertionService (pure decision)
                       |
            AlignmentDesertionBehavior (DailyTickParty + DailyTickSettlement; roster I/O)
```

## Configuration

### Config File: `Main/_Module/ModuleData/alignment_desertion/alignment_desertion_config.json`

JSON defaults; MCM overrides at runtime. Validated on load (singleton-cached — edits need an app restart).

| Field | Type | Description |
|-------|------|-------------|
| `Enabled` | bool | Master toggle. False = vanilla (no alignment desertion). |
| `Rate` | float | Fraction (0..1) of each opposed troop type that deserts per day (min 1 above 0). `0` = no desertion. NaN/out-of-range → reverts to 0.5 + warning. |
| `ApplyToAi` | bool | AI-owned parties/garrisons shed opposed troops. |
| `ApplyToPlayer` | bool | The player's party/garrisons shed opposed troops. |
| `ApplyToParties` | bool | Mobile parties are affected. |
| `ApplyToGarrisons` | bool | Settlement garrisons are affected. |

### Current Values

All toggles default **on**, rate **0.5** — aggressive cleanup matching the design goal of clearing mixed
forces quickly. The player can self-exempt (`ApplyToPlayer` off) while AI stays gated, or vice versa; the
master toggle off restores vanilla for everyone.

### Alignment data: `Main/_Module/ModuleData/execution/alignment.json`

Shared with the Execution / AlignmentRecruitment / Diplomacy features. This feature added the two
culture-id keys whose ids differ from their kingdom id:

| Key | Side | Reason |
|-----|------|--------|
| `gondor` | free | Gondor troops are `Culture.gondor` (kingdom is `empire_w`). |
| `mordor` | evil | Mordor troops are `Culture.mordor` (kingdom is `empire_s`). |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/AlignmentDesertion/AlignmentDesertionService.cs` | Pure desertion decision |
| `Main/Features/AlignmentDesertion/IAlignmentDesertionService.cs` | Service interface + DTOs (`DesertionTroopInfo`, `TroopDesertionResult`) |
| `Main/Features/AlignmentDesertion/Hooks/AlignmentDesertionBehavior.cs` | Daily party + garrison ticks, roster I/O, player popup |
| `Main/Features/AlignmentDesertion/AlignmentDesertionConfig.cs` | JSON DTO |
| `Main/Features/AlignmentDesertion/AlignmentDesertionConfigProvider.cs` | Loads + validates JSON |
| `Main/Features/AlignmentDesertion/AlignmentDesertionSettingsProvider.cs` | Merges MCM over JSON |
| `Main/Features/AlignmentDesertion/AlignmentDesertionIoC.cs` | DryIoc registration (3 singletons) |
| `Main/Features/Execution/AlignmentService.cs` | Added `GetCultureSide` (shares the id→side table) |
| `Main/_Module/ModuleData/alignment_desertion/alignment_desertion_config.json` | Defaults |
| `Main/Features/TaomSettings.cs` | MCM group "World/Alignment Desertion" (GroupOrder 38) |

## Dependencies

- `IAlignmentService` (Execution) — Free/Evil/Neutral side per kingdom (`GetKingdomSide`) and per culture (`GetCultureSide`).
- `IAlignmentDesertionSettingsProvider` — live MCM-over-JSON toggles + rate.
- `IModLogger` (Core) — logging.

## Tests

- `TAOM.Tests/Features/AlignmentDesertion/AlignmentDesertionServiceTests.cs` — 19 tests: master toggle,
  each owner/location gate, Evil↔Free symmetric desertion, same-side/Neutral-owner/Neutral-troop/hero
  skips, min-1 floor, cap-at-count, rate-0 no-op, mixed-roster selectivity, kingdomless owner, zero-count.
- `TAOM.Tests/Features/AlignmentDesertion/AlignmentDesertionConfigProviderTests.cs` — 12 tests: one per
  `Rate` validation rule (above-1, below-0, NaN, Infinity revert to 0.5 + warn; 0/1/valid preserved) plus
  missing-file / malformed-JSON / empty-object / cache-identity.
- `TAOM.Tests/Features/Execution/AlignmentServiceTests.cs` — added 5 `GetCultureSide` tests
  (gondor→Free, mordor→Evil, neutral, unknown→Neutral, null→Neutral).

## How to change the desertion rate or gates

1. In-game: MCM → "World/Alignment Desertion" — flip the master toggle, drag "Daily Desertion Rate", or
   toggle any of the four Apply-To gates. Takes effect on the next day's tick.
2. For the shipped defaults: edit `alignment_desertion/alignment_desertion_config.json` (restart the game
   — the provider is `Reuse.Singleton`).
3. To re-side a culture (or fix a missing one): edit `execution/alignment.json`. A troop culture absent
   from that file resolves Neutral and never deserts.

## Performance

Runs on the engine-staggered `DailyTickPartyEvent` / `DailyTickSettlementEvent` (not a once-a-day global
loop). Non-lord parties (no leader clan or no kingdom) early-out before any roster scan. Each affected
roster is scanned once into a small POCO list, decided, and written back.

## Changelog

- 2026-06-27 — Feature created. 50%/day opposed-alignment desertion for parties + garrisons (AI + player),
  four MCM gates + rate slider; added `gondor`/`mordor` to `alignment.json` + `IAlignmentService.GetCultureSide`.

## GitHub Issue

- **Issue:** _pending — open before the closing commit._
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/prisoner-recruitment.md](./prisoner-recruitment.md)

<!-- backlinks-end -->
