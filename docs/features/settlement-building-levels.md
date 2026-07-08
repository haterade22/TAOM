# Settlement Building Levels

## Overview

Hand-curated **starting building levels** for every town (12 buildings) and castle (11 buildings) in the
LIVE `TAOM_Map` map module, so a new campaign begins with each fief's buildings at a level that fits its
lore identity and strategic role — instead of the semi-random scatter TAOM shipped. Capitals and legendary
fortresses start maxed; ordinary fiefs moderate; remote holds sparse but still a real siege.

## Why This Exists

Bannerlord seeds each settlement's building levels once, at new-campaign creation, from `<Building level="N" />`
entries in `settlements.xml` (`Town.Deserialize`, consumed only when `CampaignGameLoadingType != SavedCampaign`;
existing saves are unaffected). TAOM's 221 fiefs carried levels uncorrelated with prosperity or importance —
the lowest-prosperity town (Vargfell, 1700) had *higher* fortifications/barracks than the highest (Minas Morgul,
5600), and only Minas Tirith was hand-set. The world therefore started economically and militarily arbitrary.

This feature replaces that scatter with a deliberate, location-aware pass: the Black Gate is a maxed fortress
despite its low prosperity; Cirith Ungol, Cair Andros, Helm's Deep, Erebor, Orthanc and Dol Guldur read as the
great strongholds they are; a remote steppe hold like Klagûl is a bare wall + a granary.

## Architecture

**Design challenge.** ~221 fiefs × 11–12 buildings = ~2,500 values that must (a) reflect per-fief lore judgment,
(b) stay internally consistent across 19 cultures, (c) never exceed the engine's valid range (0–3; fortifications
floors at 1, or the engine `Debug.FailedAssert`s), and (d) be applied to the LIVE external file safely and idempotently.

**Solution.** A single-author, three-script pipeline (modeled on the prosperity `analyze`/`rebalance` pair):

1. **`author_settlement_buildings.py`** — the source of truth. Every fief gets an explicit hand-assigned **role
   tier** + one-line rationale, with per-building **overrides** where identity beats prosperity. A pinned
   deterministic **expander** turns `(tier, culture-flavor, overrides)` into the full roster, so the *judgment*
   is hand-made per fief while the *numbers* stay consistent. Tiers: towns = capital / fortress_town / trade_town
   / major / standard / minor; castles = great_fortress / major / standard / minor / watchtower. **Culture flavor**
   (auto): military (orc factions) = +siege/+barracks, −civic; trade (Umbar) = +market/+warehouse/+tax; dwarven
   (Erebor) = +mason/+fortifications/+craftmans; elven = +mason/+waterworks/+roads. **fort3 is rationed** —
   reserved for capitals and legendary fortresses, never lifted by flavor.
2. **`dump_settlement_buildings.py`** — read-only; parses the live file for accurate current levels (the "was"
   source + before/after verification).
3. **`apply_settlement_buildings.py`** — writer. Two-level regex (unique `<Settlement id>` block → specific
   `<Building id>` inside it, since building ids repeat across every fief), exactly-once assertion per
   (settlement, building), validates range + fort-floor + correct town/castle id-set, byte-level UTF-8 round-trip
   (BOM + CRLF preserved), feature-named timestamped `.bak`, dry-run default, idempotent.

Applied 2026-07-08: 221 fiefs, 1,363 building levels altered. Reviewed by a 7-bloc adversarial workflow
(3 low-severity consistency fixes incorporated: Barad Wath / Barad Nûrn fort3→2; Ardûvar fort2→3).

## Configuration

| What | Where |
|------|-------|
| Per-fief decisions (tier + overrides + rationale) | `tools/author_settlement_buildings.py` (`DECISIONS`) |
| Generated per-culture rosters (applier input) | `tools/data/settlement_building_levels/<culture>.json` |
| Current-state snapshot | `tools/reports/settlement-buildings/current_state.json` |
| Per-fief audit artifact (current→proposed + rationale) | `docs/reviews/settlement-buildings-audit-2026-07-08.md` |
| LIVE target (engine-loaded) | `<game>/Modules/TAOM_Map/ModuleData/settlements.xml` |

## Key Files

| File | Purpose |
|------|---------|
| `tools/author_settlement_buildings.py` | Source of truth: hand decisions + pinned expander → JSONs + audit doc |
| `tools/dump_settlement_buildings.py` | Read-only current-level dumper + `current_state.json` |
| `tools/apply_settlement_buildings.py` | Safe two-level-regex applier (dry-run/apply, `.bak`, validation, idempotent) |
| `tools/data/settlement_building_levels/*.json` | 19 per-culture decision records |
| `docs/reviews/settlement-buildings-audit-2026-07-08.md` | Reviewable per-fief blocks |

## Dependencies

- The LIVE `TAOM_Map/ModuleData/settlements.xml` (external, not in repo). The repo's
  `Main/_Module/ModuleData/settlements.xml` is a stale shadow — never edited here.
- Building type ids + max levels grounded in installed vanilla `DefaultBuildingTypes` (per
  `.claude/rules/vanilla-data-comparison.md`).
- Independent of, but coexists with, the #317 prosperity rebaseline on the same file (feature-named `.bak`
  avoids clobbering the prosperity tool's `.bak`; building tiers are lore-driven, not prosperity-derived).

## Tests

Enforced by construction rather than a unit suite: the author script asserts full coverage (all 221 fiefs
decided, none missing/extra) and the applier hard-validates every level (0–3, fort≥1, correct id-set,
settlement exists) and fails loud before any write. Idempotency verified live (re-run after `--apply` = 0 changes).

## How-To

**Re-tune a fief:** edit its `DECISIONS` entry in `author_settlement_buildings.py` (change tier or add an
override like `fort=3`), run `python tools/author_settlement_buildings.py`, then
`python tools/apply_settlement_buildings.py --culture <culture>` (dry-run) → `--apply`.

**Add a tier or culture flavor:** edit `TIER_TOWN` / `TIER_CASTLE` / `CULTURE_FLAVOR` in the author script.

**Re-apply after a TAOM_Map settlement is added/removed:** re-dump (`--all --json`), add the new fief's
decision, re-author, re-apply. The applier no-ops fiefs already at target.

## Performance

N/A — offline data tooling; no runtime cost. Building levels are read once at new-campaign creation.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/settlement-economy.md](./settlement-economy.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/engine/settlement-economy-food-prosperity.md](../reference/engine/settlement-economy-food-prosperity.md)

<!-- backlinks-end -->
