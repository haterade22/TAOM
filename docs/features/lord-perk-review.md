# Lord Stats & Perk Review

## Overview

Two READ-ONLY tools that review every lord's stats per culture and map each skill level to the **perks**
(concrete gameplay bonuses) it unlocks. The lord analog of the troop balance review
([troop-skill-balance.md](troop-skill-balance.md)). `tools/extract_perks.py` builds the perk catalog from the
decompiled engine; `tools/analyze_lord_balance.py` emits one HTML report per culture (+ an index) showing every
lord's authoritative skills and the full set of perks those skills unlock. Neither modifies lord data.

## Why This Exists

- **Lords are richer than troops:** they carry the full 18-skill hero set (not 8 combat skills), and each skill
  level unlocks **perks** at tiers 25, 50, 75 … 300 that grant real bonuses (more troops, lower wages, faster
  healing, garrison/party effects). Raw skill numbers are meaningless without the perks they translate to.
- **The authoritative skills are not the obvious ones:** the engine assigns lord skills from
  `skill_template="SkillSet.X"` → `taom_lord_skill_sets.xml`. The inline `<skills>` block in
  `lords.xml`/`lords.xslt` is **documentation only** — the engine ignores it. A correct review must resolve via
  the SkillSet.
- **Goal:** a per-culture "where every lord stands" snapshot (stats + unlocked perks) to inform a future lord
  rebalance, the way the troop overview preceded the troop rebaseline.

## The perk catalog — `tools/extract_perks.py`

Parses the decompiled `DefaultPerks.cs` (v1.4.6, via `pwsh tools/taom-src.ps1 path
TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks`). Each perk is one `_field.Initialize(...)` call:
name, skill, `GetTierCost(N)` (→ `TierSkillRequirements[N-1]` = {25..300}), the alternative perk, and a
primary + (optional) secondary effect — each {role (Personal/Captain/PartyLeader/Governor/Quartermaster/…),
numeric bonus, `EffectIncrementType` (AddFactor → ×100%, Add → raw, Invalid → toggle)}. The `{VALUE}` in the
description is rendered to the in-game number.

- Handles the **8-arg** (primary-only — capstones + Crafting), **12-arg** (primary+secondary), and **13/14-arg**
  (+trailing `TroopUsageFlags`) signatures. **374 perks across 18 skills.**
- Perks come in **pairs** at each tier; a hero with `skill ≥ tier level` unlocks the tier and the engine
  (`CharacterDevelopmentModel.GetNextPerkToChoose`) picks one for AI lords — so the catalog/report show **both**.
- **Output:** `tools/data/bannerlord_perks.json` (committed source data) + `tools/reports/lord-balance/perks.html`
  (the full human-readable reference, grouped by attribute → skill → tier).

## The lord report — `tools/analyze_lord_balance.py` (read-only)

Imports the lord-reading + archetype/culture/legendary helpers from `tools/rebalance_lords.py`, the perk catalog
from `bannerlord_perks.json`, and resolves authoritative skills from `taom_lord_skill_sets.xml`.

- **Resolve skills:** each lord's `skill_template` → SkillSet (18 skills). Falls back to the inline `<skills>` if
  the SkillSet isn't found (a vanilla `spc_*` template or a missing set) and **flags** it; also flags
  inline-vs-SkillSet **mismatches** (stale documentation).
- **Per culture, one HTML** (`<culture>.html`): a **flat table, one row per lord** — name, age, archetype,
  legendary/rookie tag, the 18 skills, combat/non-combat subtotals, and the total (colored by magnitude). Each
  lord links to its profile's **unlocked-perk** block: every perk every skill unlocks, grouped by skill, both
  alternatives + effects. Perk blocks are **deduplicated by SkillSet** (lords with the same `skill_template` have
  identical skills ⇒ identical perks), so a 150-lord/3-profile culture is small while a 115-lord/46-profile
  culture (Gondor's canonical heroes) is larger but still browsable.
- **`index.html`:** per-culture lord/profile/legendary/avg-total table + the data-quality summary.

### No single-formula "parity" lens (deliberate)
The troop report colored each troop vs a single formula curve. Lords have **two** skill systems —
`rebalance_lords.py` (a 12-archetype baseline + cultural mod + age, which writes the *inline* `<skills>`) and
`apply_culture_skills_traits.py` (35 archetypes → the *SkillSets* the engine actually uses). Comparing the
authoritative SkillSet skills against the `rebalance_lords` baseline compares two different systems (and the
legendary baseline = ruler×2.5 ≈ 7975 is unreachable: 18 skills × 330 cap = 5940). So the report colors the total
by **raw magnitude** instead of a misleading cross-system delta. The "curve" is established when a lord rebalance
is actually run (a separate later pass).

## Key Files

| File | Purpose |
|------|---------|
| `tools/extract_perks.py` | Parse `DefaultPerks.cs` → perk catalog. `--defaultperks <path>`, `--stdout` |
| `tools/data/bannerlord_perks.json` | **Committed** perk catalog (374 perks); read by the analyzer |
| `tools/analyze_lord_balance.py` | Read-only per-culture lord+perk report. `--stdout`, `--culture <name>` |
| `tools/rebalance_lords.py` | Read/import only — lord reading + archetype/culture/legendary helpers |
| `Main/_Module/ModuleData/taom_lord_skill_sets.xml` | Authoritative lord skills (SkillSets) |
| `Main/_Module/ModuleData/characters/lords.xml`, `lords.xslt` | Lord defs (id/name/culture/age/template/traits) |
| `tools/reports/lord-balance/{index,perks,<culture>}.html` | Generated reports (gitignored, regenerate-able) |

## How to run

1. `python tools/extract_perks.py` — (re)build the perk catalog (only needed after an engine bump).
2. `python tools/analyze_lord_balance.py` — write `index.html` + a `<culture>.html` per culture + `perks.html`.
   Open `tools/reports/lord-balance/index.html`. Use `--culture gondor` for one culture, `--stdout` for a summary.

## First-run findings (2026-06-25)

1,391 lords across 19 cultures, 119 distinct SkillSet profiles. Data-quality flags surfaced:
- **93 lords reference a SkillSet not in `taom_lord_skill_sets.xml`** — mostly vanilla `spc_*_rookie` templates
  (young lords whose template was never swapped to a TAOM SkillSet, so they get vanilla skills). A real gap to review.
- **149 lords whose inline `<skills>` ≠ their SkillSet** — stale documentation (harmless to the engine, which uses
  the SkillSet, but the inline blocks are misleading).

## Changelog

- 2026-06-25 — Added `extract_perks.py` (perk catalog: 374 perks → `bannerlord_perks.json` + `perks.html`) and the
  read-only `analyze_lord_balance.py` (per-culture lord stats + every unlocked perk; resolves authoritative
  SkillSet skills; flags unresolved templates + inline/SkillSet drift). Review-only — no lord data changed.

## Related

- [troop-skill-balance.md](troop-skill-balance.md) — the troop analog (review → rebaseline).
- [lord-skills.md](lord-skills.md) + [../ai-includes/lord-skills-authoring.md](../ai-includes/lord-skills-authoring.md) — the TAOM SkillSet/archetype system that authors the lord skills this report reviews.
