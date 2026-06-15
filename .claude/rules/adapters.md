---
paths:
  - "Main/Adapters/**"
  - "Main/**/I*Adapter.cs"
  - "Main/**/*Adapter.cs"
---

# Adapter Pattern Rules (ADR-007)

## Core Principle
Services NEVER accept sealed TaleWorlds types directly. Always wrap with adapter interfaces.

## Creating New Adapters
1. **Research first** — Decompile the TaleWorlds class with `ilspycmd` before creating the adapter interface
2. **Interface in `Main/Adapters/`** — `I{TypeName}Adapter.cs` with only the properties/methods the feature needs
3. **Implementation in `Main/Adapters/`** — `{TypeName}Adapter.cs` wrapping the sealed type
4. **Recursive wrapping** — If the sealed type exposes other sealed types, wrap those too
5. **Defensive validity** — Check for dead agents, null references in computed properties

## Property Guidelines
- Identify read-only (get-only) vs read-write properties from decompiled source
- Use null-conditional operators (`?.`) for computed properties accessing nested objects
- Cache expensive property lookups where appropriate

**A computed getter throws BEFORE your `!= null` guard can run — guard the inner object, not the result.** Decompile the getter body first: a property defined as `X => A.B` (e.g. `PartyBase.Culture => MapFaction.Culture`, PartyBase.cs:255) dereferences `A` with no guard, so `if (party.Culture != null)` NREs inside the getter when `MapFaction` is null. Write `party.MapFaction?.Culture` instead. Distinguish computed getters (throw) from plain `[SaveableField]` fields (`Hero.Culture`, `Settlement.Culture` — safe once the parent is non-null) by reading the member definition, not by assuming. When several models resolve the same value, funnel them through one null-safe chokepoint (e.g. `CultureFeatAdapter.ResolvePartyCulture`) and never resolve inline — a future inline `?? party.Culture` fallback silently reintroduces the crash. This shipped as a campaign-map NRE: issue #281, RCA `docs/reviews/rca-culturefeat-partyculture-nre-2026-06-15.md`. Note that copying a vanilla helper verbatim (`PartyBaseHelper.HasFeat`) inherits its unstated preconditions — TAOM hit the NRE because it calls the helper on far more parties than vanilla does.

## Testing
- Adapters themselves are thin wrappers — test coverage via service tests that mock the adapter interface
- Use `NSubstitute.Substitute.For<IXxxAdapter>()` in tests

## Modifier-Preserving Overloads (MANDATORY for inventory/equipment adapters)

TaleWorlds' inventory and equipment APIs frequently expose **two parallel overloads**: a simpler `(ItemObject, int)` form and a richer `(EquipmentElement, int)` form. The simpler form internally calls `new EquipmentElement(item)` — discarding any `ItemModifier` (durability state, quality prefix like "Sharp"/"Damaged", cosmetic item, quest-item flag). When the adapter touches a slot that vanilla treats as `EquipmentElement`-shaped, **the richer overload is the correct API surface.**

**Examples of the parallel-overload pattern in v1.4.5:**
- `ItemRoster.AddToCounts(ItemObject, int)` ↔ `ItemRoster.AddToCounts(EquipmentElement, int)`
- `Equipment[EquipmentIndex] = ?` accepts only `EquipmentElement` — already lossless if you pass the captured element through
- `EquipmentHelper.AssignHeroEquipmentFromEquipment` takes `Equipment` directly — preserves modifier
- `ItemRoster.AddToCounts(ItemRosterElement)` and `Add(ItemRosterElement)` carry full element

**Rule:** Before calling a `(ItemObject, ...)` overload, search for the parallel `(EquipmentElement, ...)` form. If it exists, prefer it. Update the adapter's internal data to carry the full `EquipmentElement` (not bare `ItemObject` or `string` ID).

The adapter interface boundary stays ADR-007 compliant — services see opaque snapshot tokens that internally carry the full element. See `Main/Adapters/PartyMountInventoryAdapter.cs` + `Main/Features/SiegeDismount/Models/MountSnapshot.cs` for the canonical pattern.

**Anti-pattern (do NOT ship):** documenting "modifier/quality/cosmetic is lost on round-trip" as a known limitation in the feature doc without first verifying the limitation is inherent in the API. Codex review #34 (SiegeDismount, 2026-05-06) caught exactly this — the modifier-preserving overload existed; the adapter just used the wrong one.
