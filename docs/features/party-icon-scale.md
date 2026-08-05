# Party Icon Scale

## Overview

Shrinks the campaign-map party-icon **leader figure and its mount** from the vanilla hardcoded `0.3` scale to
an MCM-configurable value (default `0.15` = half). A single Harmony transpiler rewrites both `0.3f` literals in
`MobilePartyVisual.AddCharacterToPartyIcon` into a call that reads the live MCM "Map Figure Scale" slider, so the
figures honour a runtime-tunable size instead of the engine constant.

## Why This Exists

On the campaign map the leader figure standing on each party icon felt oversized relative to settlements — the
`0.3` figure scale is large next to town/castle/village meshes. Halving it (people **and** their mount together,
so the rider stays proportional) makes parties read as map tokens rather than dominating the terrain. The value is
a slider rather than a constant because the "right" size is a visual judgement best eyeballed in-game.

This follows the [bannerlordmodding.lt "Scale World Map Entities" guide](https://docs.bannerlordmodding.lt/guides/scale_world_map_entities/),
adapted to the engine's real type name and TAOM's config conventions.

## Architecture

**Design challenge.** The scale is a hardcoded `ldc.r4 0.3` IL literal in a private engine method — there's no
GameModel or virtual to override. The only seam is a transpiler. But a transpiler can't read a runtime config
value directly; it only edits IL. **Solution:** rewrite each `0.3` literal into `call PartyIconScaleConfig.GetScale()`
(a static, parameterless, `float`-returning method) — a stack-neutral swap (`ldc.r4` and the call each push one
float, pop none). `GetScale()` reads the MCM slider each invocation, so a slider change applies on the next icon
rebuild. This is the same "transpiler calls a static" pattern as `CastleAiToggle` in CastleRecruitment.

**Two scale sites** in `AddCharacterToPartyIcon` (v1.4.6), each uniquely matchable by the instruction that
follows the `ldc.r4 0.3`:

| Site | Vanilla C# | IL shape | Match rule |
|------|-----------|----------|-----------|
| Leader figure | `.Scale(0.3f)` | `ldc.r4 0.3` → `callvirt AgentVisualsData::Scale` | `0.3` immediately before a `Scale` call |
| Mount | `.Scale(item.ScaleFactor * 0.3f)` | `ldc.r4 0.3` → `mul` → `callvirt Scale` | `0.3` immediately before `mul` |

The method's other `0.3` literals feed animation-speed math (`… / 0.3f` = `div`) and are not matched. If either
site is absent after an engine change, that swap is skipped with a warning and vanilla `0.3` is preserved — the
transpiler never throws (so a Harmony category re-apply can't crash).

```
Patch53_PartyIconScale (thin Harmony transpiler entry)
        │ delegates IL surgery to
PartyIconScaleTranspiler.Rewrite   ← pure, synthetic-IL tested
        │ rewrites `ldc.r4 0.3` → `call`
PartyIconScaleConfig.GetScale()    ← static the rewritten IL calls
        │ reads + validates
TaomSettings.MapFigureScale        ← MCM slider
```

Coexists with the BannerColorPersistence **Postfix** on the same method — a transpiler rewrites the body, a
postfix runs after; no conflict.

## Configuration

| Knob | Where | Default | Range | Notes |
|------|-------|---------|-------|-------|
| **Map Figure Scale** | MCM → TAOM → Map UI → Party Icons | `0.15` | `0.05`–`1.0` | Drives people + mounts. Vanilla = `0.30`. Applies on next icon rebuild. |

Validation (`PartyIconScaleConfig.Resolve`): NaN / ±Infinity / out-of-range / null fall back to `Default` (0.15)
via `FiniteFloatValidator`. The slider UI already clamps input to `[Min, Max]`, so the guard only matters for a
hand-corrupted settings JSON; `Resolve` does not log on fallback because `GetScale` runs per icon rebuild and would
spam.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/PartyIconScale/PartyIconScaleConfig.cs` | `GetScale()` (IL call target) + pure validated `Resolve()` + Default/Min/Max consts |
| `Main/Features/PartyIconScale/PartyIconScaleTranspiler.cs` | IL surgery — both `0.3`→`GetScale` swaps, fail-safe |
| `Main/Features/PartyIconScale/Hooks/Patch53_PartyIconScale.cs` | Thin transpiler entry on `MobilePartyVisual.AddCharacterToPartyIcon` |
| `Main/Features/TaomSettings.cs` | `MapFigureScale` MCM slider (Map UI/Party Icons group) |
| `Main/SubModule.cs` | `Patch53_PartyIconScale.Initialize` + `PatchCategory` registration |

## Dependencies

- HarmonyLib transpiler; target type `SandBox.View.Map.Visuals.MobilePartyVisual` (`SandBox.View.dll`).
- MCM (`TaomSettings`) for the slider; `FiniteFloatValidator` for validation.

## Tests

| File | Coverage |
|------|----------|
| `TAOM.Tests/Features/PartyIconScale/PartyIconScaleConfigTests.cs` | `Resolve`: valid mid/boundary pass-through; NaN/±Inf/below-min/above-max/null → Default |
| `TAOM.Tests/Features/PartyIconScale/PartyIconScaleTranspilerTests.cs` | Synthetic IL: people + mount sites swap to `GetScale`; `0.325`/`0.3-before-Div` decoys untouched; labels preserved; null-getScale + missing-site fail-safe |

The transpiler against the live engine method is verified in-game (not unit-tested — Harmony patch invocation needs a running game).

## How-To

**Retune the size:** move the MCM "Map Figure Scale" slider (no rebuild). `0.30` = vanilla parity; `0.05`/`1.0` =
bounds. Because `TaomSettings` is process-cached, the value reads live, but figures only re-render on the next icon
rebuild (e.g., a party moves or composition changes).

**Also scale caravan pack animals (not currently done):** those are built in the *separate*
`MobilePartyVisual.AddMountToPartyIcon` method. Add a second transpiler targeting it (same `ldc.r4 0.3 → mul`
mount-site shape) if caravan animals should match.

## Notes

- Custom TAOM mounts (warg/elephant/spider) need no special handling — the mount swap is `ScaleFactor * GetScale()`,
  so their larger `ScaleFactor` stays proportionally large, just halved.
- Settlements are scaled in the editor, not via code (per the source guide) — out of scope here.

## Changelog

- 2026-06-24 — Added MCM-configurable party-icon figure scale (default `0.15`, half vanilla `0.30`): `Patch53_PartyIconScale` transpiler rewrites both `0.3f` literals (leader figure + mount) in `MobilePartyVisual.AddCharacterToPartyIcon` to `PartyIconScaleConfig.GetScale()`, reading the new "Map Figure Scale" MCM slider. Issue #297.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
