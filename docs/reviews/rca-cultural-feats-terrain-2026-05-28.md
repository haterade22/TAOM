# RCA — Cultural-Feats Terrain Movement-Speed Feats (2026-05-28)

Feature: terrain-based cultural movement-speed feats (issue #248). Review pipeline: `/deep-review` (5 agents) → `/review-codex` (gpt-5.5, xhigh). Deep-review verdict was READY (its one CRITICAL — "TerrainType.Snow doesn't exist" — was a self-contradictory false positive, refuted by direct decompile: `Snow = 3`). Codex found 0 CRITICAL / 1 HIGH / 3 MEDIUM / 1 LOW.

Three confirmed bugs were fixed; two findings were declined with recorded reasons.

## Findings

| # | Sev | Bug | Category | Why Missed | Action |
|---|-----|-----|----------|-----------|--------|
| 2 | MED | Mordor night feat applied at sea, where vanilla applies no night penalty to offset | Missing vanilla gate | Read vanilla night handling but extracted only the `Campaign.Current.IsNight` trigger, not its enclosing `if (!mobileParty.IsCurrentlyAtSea)` guard | **Fixed** — `isNight = IsNight && !IsCurrentlyAtSea` |
| 3 | MED | Culture resolved via `Owner?.Culture` only; vanilla feat checks use leader→party→owner→settlement precedence (missed garrison/militia/caravan + leader-driven parties) | Missing vanilla gate / convention | Inherited the pre-existing `Owner?.Culture` accessor from the old forest-feat code without checking `PartyBaseHelper.HasFeat`'s precedence; all 5 deep-review agents treated the pre-existing pattern as correct | **Fixed** — `ResolvePartyCulture` mirrors `PartyBaseHelper.HasFeat` |
| 5 | LOW | Stacked/orphaned `<summary>` — `CountMountedAndTotal`'s doc left dangling above `MapTerrain` | Mechanical edit error | Inserted `MapTerrain` (summary+body) between the existing method's summary and its body via an anchor Edit | **Fixed** — reordered so each summary sits above its method |
| 1 | HIGH | Snow feats key off `TerrainType.Snow` terrain, not snowy weather (vanilla derives snow from `MapWeatherModel`) | Engine-behavior assumption | N/A — see "Declined" | **Declined (by design)** |
| 4 | MED | Hot path allocates a `CultureFeatAdapter` per speed recalc | Hot-path allocation | N/A — see "Declined" | **Declined (pre-existing, marginal)** |

## Declined findings (recorded per "HIGH findings — no silent deferrals")

- **#1 (HIGH) snow weather:** the `TAOM_Map` navmesh faces around snowy regions are author-painted with terrain id `3` (= `TerrainType.Snow`), so `GetFaceTerrainType` returns `Snow` there and the Erebor/Gundabad bonus fires. Terrain-only detection is intentional and correct for the TAOM map. Codex itself hedged: *"I cannot prove from source that the TAOM map has zero navmesh faces with FaceGroupIndex == 3."* Switching to weather detection would be wrong for how the map is authored. Recorded in `CHANGELOG.md` + `docs/features/cultural-feats.md`. **Not a bug on the TAOM map.**
- **#4 (MED) adapter allocation:** pre-existing pattern (the old forest-feat code allocated the same adapter). Vanilla gates speed recalculation behind `MobileParty.IsLastSpeedCacheInvalid` (only recomputed on nav-face / day-night / wind / prisoner-state change), so `CalculateFinalSpeed` is **not** per-frame. A per-`CultureObject` cache on the shared `CultureFeatAdapter` (used by 16 GameModels) is scope creep for a marginal GC win — rejected under `.claude/rules/simplicity-criterion.md` (tiny win + added complexity). Noted as a possible future optimization.

## Root-cause pattern

Bugs #2 and #3 share one root cause: **when mirroring or offsetting a vanilla modifier, I extracted only the piece I needed from the vanilla method and dropped the surrounding conditions.** #2 dropped the `!IsCurrentlyAtSea` guard around the night penalty; #3 used an ad-hoc culture accessor instead of vanilla's feat-culture precedence. Both came from reading `DefaultPartySpeedCalculatingModel.CalculateFinalSpeed` / `PartyBaseHelper.HasFeat` for the *value* I wanted without replicating the *conditions* under which vanilla applies it.

This is the same family as memory `feedback_replicate_vanilla_safety_gates_in_prefix` — originally scoped to Harmony **Prefix** returns-false. It needs generalizing to **GameModel overrides that add to a vanilla `ExplainedNumber`**: replicate the full guard/precedence that vanilla uses for the modifier you are stacking on or offsetting.

## Why each deep-review agent missed #2 and #3

- **Agent 1 (Standards):** scope is TAOM conventions (ADRs), not vanilla-parity of conditions. Out of scope.
- **Agent 2 (Compatibility):** verified `Campaign.IsNight` and the API *signatures* exist, but the prompt asks "does the API exist / match signature," not "does the override replicate vanilla's application conditions." It confirmed `IsNight` is a valid bool property and stopped.
- **Agent 3 (Efficiency):** flagged nothing because the methods are allocation-light; the adapter alloc (#4) was judged acceptable (correctly — it's cached/non-per-frame). Not a conditions reviewer.
- **Agent 4 (Completeness):** verified tests/docs/wiring exist; doesn't compare against vanilla semantics.
- **Agent 5 (Data Flow):** traced the night feat and culture flow end-to-end and found them internally consistent — but "internally consistent" ≠ "matches vanilla's land-only night gate / culture precedence." The agent had no vanilla-comparison step for the night gate or culture resolution.

The gap: **no deep-review agent compares a GameModel override's *application conditions* against the vanilla method it extends.** Codex's vanilla-decompile-and-diff step is exactly what caught both. This is the known division of labor (Codex does the adversarial vanilla diff), so the deep-review agents aren't being expanded for this — the pipeline worked as designed: deep-review for TAOM-internal correctness, Codex for vanilla-parity.

## Feedback memory to codify

One genuine systemic lesson worth a memory: **GameModel overrides that add to a vanilla `ExplainedNumber` must replicate the vanilla modifier's full guard + the vanilla culture/entity-resolution precedence — not just read the value.** Generalizes `feedback_replicate_vanilla_safety_gates_in_prefix.md` from Prefix-returns-false to additive GameModel overrides. (#5 is a one-off mechanical edit slip — no memory.)

## Verification

- `dotnet test TAOM.Tests` (deploy-skip flags, game running) → 2624 passed / 0 failed / 2 skipped.
- Model fixes (#2 night-at-sea, #3 culture precedence) are boundary logic in the thin GameModel entry point — not unit-tested per the gamemodels rule (require live `MobileParty`/`PartyBase`/`Campaign`); verified by build + in-game.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
