# RCA — Settlement Nameplate Fade (2026-05-25)

## Top-line

`/deep-review` on the new `SettlementNameplateFade` feature surfaced 1 HIGH + 1 MED + 1 LOW finding before commit. All three resolved in-session. No HIGH findings shipped.

The substantive finding was a hot-path performance bug — `TaomSettings.Instance` accessed 3× per call at ~3000 calls/sec (~9000 redundant singleton lookups/sec). The pattern is project-wide across TAOM's settings providers; this is the first feature where the consumer's frequency makes it matter.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | `NameplateFadeSettingsProvider` read `TaomSettings.Instance` 3× per `ComputeAlphaMultiplier` call. At ~3000 calls/sec that's 9000 redundant static-singleton dereferences. | Performance / hot-path access pattern | Existing settings providers (`EncyclopediaSettingsProvider`, `RevoltTuningConfigProvider`, etc.) use the same `TaomSettings.Instance?.X ?? default` pattern but their consumers run per-event, not per-frame. The pattern was copy-pasted from `Encyclopedia` without re-evaluating consumer call-frequency. No project rule captured "if the consumer is in `OnParallelUpdate` or any per-frame path, cache the singleton reference." | Cached `TaomSettings.Instance` in the provider constructor. New feedback memory below to codify the rule. |
| 2 | MED | Patch used `Lazy<INameplateFadeService>` for service capture; project convention elsewhere (`BannerColorPersistence`, `SettlementGuards`) is `public static void Initialize(IService svc) => _service = svc;` + static-field reads in the postfix. | Convention drift | `Lazy<T>` is idiomatic .NET but adds a per-access `IsValueCreated` check that doesn't always inline. The simpler static-field pattern is both faster and consistent with how every other TAOM Harmony patch caches services. I introduced `Lazy<>` because it's terser to write — but the TAOM project pattern matters more than terseness on a 5-line difference. | Switched to `Initialize(svc)` + static field. Matches every other hot-path patch in the codebase. |
| 3 | LOW | Test suite covered `ComputeAlphaMultiplier_InfinityFarDistance_ReturnsOne` but not the symmetrical `ComputeAlphaMultiplier_InfinityNearDistance_ReturnsOne`. | Coverage gap (asymmetric) | When validating two parallel fields (near, far) with the same NaN/Infinity guard, I tested one and not the other. The production code DID guard both; only the test was missing. Manual review of test names would have caught this — Codex Agent 5's "pair-symmetry" check is the cross-system tool that did catch it. | Added the missing test. Coverage now symmetric across `Near` / `Far` for `NaN` and `Infinity` inputs. |

## Root-cause pattern

Hot-path settings access. The TAOM convention of `=> TaomSettings.Instance?.X ?? default` is fine for per-event consumers but breaks down for per-frame consumers. The first per-frame settings consumer in TAOM was this one, so the pattern hadn't been stress-tested before.

The cost: each `TaomSettings.Instance` access is a static-accessor + null-check. Looking at MCMv5 source — `AttributeGlobalSettings<T>.Instance` reads through `BaseSettingsProvider.GetSettings(id)` which does a registry lookup, not a static-field read. Per access this is in the low-nanoseconds, but at 9000 calls/sec on a hot path it accumulates.

The fix is to capture the reference once in the constructor — values are still read through the reference at each property access, so live MCM edits still propagate.

## Why each agent missed (where applicable)

The deep-review agents DID catch all three findings:

- **Agent 1 (Standards):** PASSED — correct, no standards violation. Architectural pattern is sound.
- **Agent 2 (Compat):** PASSED — correct, all v1.4.5 API references verified. Threading note (advisory) about `OnParallelUpdate` is worth keeping.
- **Agent 3 (Performance):** CAUGHT HIGH + MED. Flagged the per-frame singleton access and the Lazy-vs-static-field convention drift.
- **Agent 4 (Completeness):** CAUGHT — missing docs, CHANGELOG, CLAUDE.md table row. Process gaps, addressed in close-out.
- **Agent 5 (Data Flow):** CAUGHT LOW — test asymmetry between `InfinityFar` and `InfinityNear`.

No agent silently missed a finding. The HIGH was caught at deep-review time, which is the point of the workflow.

## Feedback memories to codify

**`feedback_hotpath_settings_provider_caches_instance.md`** — new memory:

> When a settings provider (`X?.Property ?? default` bridge to `TaomSettings.Instance`) is consumed by a per-frame, per-tick, or per-agent hot path, cache `TaomSettings.Instance` in the provider's constructor. The pattern `EncyclopediaSettingsProvider`-style is correct for per-event consumers but accumulates ~3000 lookups/sec when wired into `OnParallelUpdate`, `MissionTick`, or `AgentStatCalculate` paths.
>
> **Why:** RCA `docs/reviews/rca-settlement-nameplate-fade-2026-05-25.md`. First per-frame settings consumer in TAOM exposed the pattern's frequency assumption.
>
> **How to apply:** Before writing a settings provider, check the call site. If the consumer is in a Harmony patch on `OnParallelUpdate`, `MissionLogic.OnMissionTick`, `AgentStatCalculateModel.UpdateAgentStats`, any GameModel method called more than once per game tick, or any `OnApplicationTick` path, cache the singleton reference. Otherwise, the trivial bridge pattern is fine.

I'll commit this memory together with the closing commit.

## Constraint

`OnParallelUpdate` is multi-threaded (per `feedback_detect_engine_threading_via_mt_suffix.md`). The cached `TaomSettings.Instance` reference is read-only from multiple threads — safe since `_settings` is a single immutable reference and the `Bool`/`float` property reads on the singleton are atomic on .NET 4.7.2 x64. The values themselves can change via MCM UI on the main thread; readers will see torn writes only on the float properties (`NameplateFadeNearDistance`, `NameplateFadeFarDistance`). Worst-case effect of a torn float read: one frame of a corrupted distance value, immediately corrected next frame. Acceptable.

## Files

- New: `docs/features/settlement-nameplate-fade.md`
- New: `docs/reviews/rca-settlement-nameplate-fade-2026-05-25.md` (this file)
- Modified (perf fix): `Main/Features/SettlementNameplateFade/NameplateFadeSettingsProvider.cs`, `Main/Features/SettlementNameplateFade/Hooks/SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs`, `Main/SubModule.cs`
- New test: `TAOM.Tests/Features/SettlementNameplateFade/NameplateFadeServiceTests.cs` — `ComputeAlphaMultiplier_InfinityNearDistance_ReturnsOne`
