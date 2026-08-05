# RCA — Castle Recruitment (2026-05-31)

**Feature:** `CastleRecruitment` — player + AI recruit volunteer troops from castles (previously towns/villages only).
**Review:** `/deep-review CastleRecruitment` — 5 parallel agents (Standards, Compatibility, Efficiency, Completeness, Data Flow). Pure-C# changeset, 20 files, 3 Harmony patches (2 transpilers + 1 postfix).
**Outcome:** 30/30 TaleWorlds APIs verified, 0 incompatibilities, 0 hard data-flow gaps. 5 confirmed findings, **all fixed in-session**. 1 latent out-of-scope item documented (not fixed).

The review *succeeded* — it caught every finding before commit. This RCA extracts why each finding was *authored* and codifies preventive rules so the category can't recur.

## Findings

| # | Sev | Bug | Category | Why authored / missed at write-time | Preventive action |
|---|-----|-----|----------|--------------------------------------|-------------------|
| 1 | HIGH | `Patch42_HourlyTickParty_Postfix` used `MethodInfo.Invoke(__instance, new object[]{...})` per-AI-party-per-hour — fresh argument-array alloc + slow reflection on a hot path | Perf / hot-path alloc | The `harmony-patches.md` rule mandates caching the `AccessTools.Method` *lookup* (done), but I read that as "cache the MethodInfo" and stopped — it doesn't mention the per-call argument array or `Invoke`-vs-delegate. Correctness focus crowded out the alloc audit. | **Fixed:** bound an open-instance delegate once via `Delegate.CreateDelegate(typeof(Action<TBehavior,TArg1,TArg2>), mi)`; call it directly — zero alloc, no reflection dispatch. New rule candidate (see below). |
| 2 | HIGH (latent) | `CastleAiTranspiler` selected "first `get_IsCastle` that *has* the anchor in-window." `AiHourlyTick` has TWO `get_IsCastle` (line 269 recruit gate, line 317 reform-score gate). Correct for v1.4.5, but if a future engine refactor moved the anchor near line 317 the loop could swap the **wrong** gate and silently corrupt the reform multiplier. | IL fragility | Authored the disambiguation as "scan all `get_IsCastle`, take the first with the anchor" — which *allows fall-through* to a later (wrong) candidate. The recruit gate is always the FIRST `get_IsCastle`; I didn't pin to that. | **Fixed:** target only the **first** `get_IsCastle`; require the anchor to follow it; if not, **bail** (fail-safe to vanilla) — never search past it. Wrong-target is now structurally impossible. |
| 3 | MED | Transpiler `AnchorWindow = 16` was tight for `FillSettlements`' `!settlement.IsCastle && settlement.MapFaction != mobileParty.MapFaction && IsSettlementSuitableForVisitingCondition(...)` — the `!=` operator + two `get_MapFaction` calls expand to ~8-10 IL instrs; a release-build optimizer could push the anchor past 16, silently disabling AI castle targeting. | IL fragility | Estimated the IL gap by eyeballing source, not by counting the compiled `!=` expansion. | **Fixed:** widened to 24. The fail-safe cost of a too-WIDE window is ~nil; a too-NARROW window silently no-ops the patch. Size anchor windows generously. |
| 4 | MED | `ToOccupation` mapped unknown `CastleNotableOccupation` values to `GangLeader` via `default:` — a future enum addition would silently misroute instead of failing. | Defensive coding | Used `default:` for the happy-path occupation (GangLeader) out of habit. | **Fixed:** explicit `case` per value + `default: throw ArgumentOutOfRangeException`. Enum→engine mapping switches must force a compile/runtime signal on extension. |
| 5 | Standards (ADR-002) | `CastleRecruitmentBehavior` was 206 lines (>150 ceiling). Logic *was* correctly delegated to the service; the breach was accreted engine glue (menu + spawn + fill + issue) in one class. | Architecture | Built the behavior incrementally — event wiring, then menu, then spawn, then fill, then issue suppression — without watching the line count; each addition was individually small. | **Fixed:** extracted the notable spawn + volunteer-fill engine glue to `CastleNotableMaintainer`; the behavior is now a thin event router (~120 lines). |
| 6 | LOW (latent, out-of-scope) | `TaomVolunteerModel.GetDailyVolunteerProductionProbability` calls `base`, which NREs for a castle settlement (`settlement.Village.TradeBound` — castle `.Village` is null). | Pre-existing latent NRE | Not introduced by this feature and **unreachable**: vanilla's `UpdateVolunteersOfNotablesInSettlement` gates castles out before calling it, and our `CastleNotableMaintainer.FillCastleVolunteers` deliberately uses the service's pure `GetSlotProductionProbability` instead. | **Not fixed** (edit-scope discipline — different feature, unreachable). Documented here + in memory as a known latent that *this feature moves closer to reachable*. If any future code calls `GetDailyVolunteerProductionProbability` for a castle, add a castle guard then. |

## Root-cause pattern: widening a settlement-type gate exposes castle-unsafe dereferences downstream

The two load-bearing crash risks for this feature (`DefaultVolunteerModel.GetDailyVolunteerProductionProbability` line 103 `settlement.Village.TradeBound`, and `GetBasicVolunteer` line 113 `sellerHero.CurrentSettlement.Village.Bound` for rural notables) are **latent NREs that only exist because castles have no `.Village`**. Vanilla never hits them because it gates castles out *before* these methods. The moment you make castles behave like towns/villages (here: give them notables and a recruit path), every method those now-castle-eligible objects flow through must be audited for `settlement.Village.X` / `IsRuralNotable`-style assumptions.

This is the sibling of `feedback_replicate_vanilla_safety_gates_in_prefix.md` (memory; distilled in [lessons/harmony-il.md](lessons/harmony-il.md)): that rule says "when you skip vanilla with a Prefix, replicate its safety gates." This one says "when you *remove/widen* a vanilla settlement-type gate, audit what the gate was *protecting* downstream." Both were caught here at design time **because research traced the full downstream call chain before widening** — which is exactly why Findings on castle-NRE were 0 (the fill path was authored castle-safe from the start) and the only NRE that survived (Finding 6) is the one path the feature deliberately doesn't use.

## Why the review caught everything (what worked)

- **Data-flow agent (sonnet)** was the high-value agent again: it independently decompiled `AiHourlyTick`, counted the two `get_IsCastle` calls, and flagged the wrong-target risk (Finding 2) + confirmed the castle-NRE paths are all safe (Trace 6) — the class of bug per-file agents miss.
- **Compatibility agent** verified all 30 API claims against installed v1.4.5 DLLs and independently raised the tight-anchor-window concern (Finding 3).
- **Efficiency agent** caught the hot-path alloc (Finding 1) that correctness-focused authoring missed.
- **Standards agent** caught the ADR-002 ceiling (Finding 5) while confirming the delegation was correct.

No agent was blind-sided; the findings were authoring gaps, not review gaps.

## Preventive rules to codify

1. **Hot-path private-method invocation via open delegate, never `MethodInfo.Invoke`.** When a Harmony Prefix/Postfix on a per-tick / per-party / per-frame method must call a private engine method, bind `Delegate.CreateDelegate(typeof(Action<TInstance,...>), methodInfo)` once in `Initialize()` and call the delegate. `MethodInfo.Invoke` allocates a `params object[]` per call and dispatches reflectively. → memory `feedback_hotpath_private_method_open_delegate.md`.
2. **Transpiler that swaps one call among N identical calls must pin to ORDINAL position (first/Nth) AND a nearby unique landmark, and fail-safe (bail, never fall through to a different candidate).** Selecting "first match with anchor" allows a future IL shift to silently retarget. → memory `feedback_transpiler_ordinal_plus_anchor_failsafe.md`.
3. **Widening a vanilla settlement-type gate (IsTown/IsVillage → +IsCastle) requires auditing the full downstream call chain for `settlement.Village.X` and `IsRuralNotable`-style castle-unsafe dereferences** before shipping. → memory `feedback_widening_settlement_type_gate_audit.md`.
4. (Reinforce existing) Enum→engine mapping switches use explicit cases + `default: throw`; size transpiler anchor windows generously; extract CampaignBehavior engine glue to a helper before the ADR-002 ceiling, not after.

## Verification

- `dotnet build Main/TAOM.csproj` — 0 errors.
- `dotnet test TAOM.Tests` — **2760 passed, 0 failed, 2 skipped** (24 new CastleRecruitment tests: 16 service + 8 config provider).
- All 5 fixes re-built + re-tested green. In-game smoke test (player recruit at castle, AI castle recruitment over campaign days, no broken castle-notable quests) is the remaining live-game gate — patches are "Not-tested" in unit tests by convention.
