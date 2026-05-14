# Phase 9 Completion Report — 2026-05-14

All 79 audit issues (#121–#199) closed. The feature audit is complete.

## Headline numbers

| Metric | Value |
|---|---|
| Total audit issues opened | 79 (#121–#199) |
| Closed by fix | ~46 |
| Closed by deferral with disposition | ~12 |
| Closed as stale / already-resolved | ~5 (audit-docs #196–#199 + #170) |
| Closed via #173 unblocker | ~3 (#142 partial, #148 partial, #143 partial) |
| Commits in fix cycle | ~33 since `b4b4de1` |
| Test count delta | 1958 → 2018 (+60) |
| Build status | green throughout |
| Architectural patterns documented | R1–R5 (see cluster docs) |

## Pattern coverage achieved

Phase 9a triage identified 5 recurring patterns (R1–R5). Pattern coverage:

| Pattern | Coverage |
|---|---|
| **R1 — singleton-not-reset-across-campaigns** | Fixed in BannerInjection (#124), CareerSystem ability cache (#128), HeroRace (#130), RaceAge cache (#131), Siege (#132), CompanionTactics presets (#139), FiefManagement (#143). 7 R1 instances closed via explicit `Reset()` / `ResetForNewCampaign()` methods + `OnNewGameCreatedEvent` wiring. |
| **R2 — SyncData broken** | Fixed in CareerSystem IsLoading-gate (#128), Siege full SyncData implementation (#132), BannerInjection absent-key load (#124). 3 instances. |
| **R3 — config validation gaps (NaN/Infinity/ordering)** | Fixed in CareerSystem ParseFloat (#128), Diplomacy WoR config (#129), StartupResources Gold/Influence (#136), BattleBalance TierPower (#140), RaceAge FertilityMod (#131), SpecialResources ParseFloat (#133), Messengers config NaN (earlier session). 7 instances using `FiniteFloatValidator`. |
| **R4 — validate-before-lookup-with-fallback** | Fixed in HeroRace restore (#171), RaceAge GetEntry (#131). Per `feedback_validate_before_lookup_with_fallback.md`. 2 instances. |
| **R5 — vanilla safety gates dropped in Prefix** | Documented (no code violations found in remaining scope). |

## Architectural extractions completed this phase

| Service | Issue | Result |
|---|---|---|
| `ICareerPassiveService.ApplyFactor/ApplyFlat` | #173 | Replaced static `CareerPassiveHelper` (service-locator anti-pattern); 10 GameModels updated to constructor injection; snapshot-swap pattern added. |
| `IExecutionRelationService` | #147 | Replaced injected hook in `TaomExecutionRelationModel`; `ExecutionRelationResult` struct; `IPlayerContextAdapter` replaces `Hero.MainHero.MapFaction.StringId` access. |
| `ITournamentService` | #137 | 4 pure decision functions extracted from `TaomTournamentModel`; rule-4 compliance achieved. |

## Deferred with disposition (not closed silently)

| # | Title | Why deferred |
|---|---|---|
| #144 | CulturalFeats systemic rule-4 (all 16 models) | Multi-session refactor; needs `ICulturalFeatsService` design + 16 model refactors. |
| #176 | CulturalFeats tests | Pair with #144 — tests are limited by sealed-type construction until service exists. |
| #178 | Warg `IAgentBattleAdapter` expansion | Cross-cuts 5+ files; needs careful surface design (recursive RiderAgent wrapping, Team semantics). |
| #180 | TaomPartyWageModel tests | Pair with #148 `IWageModifierService` extraction track. |
| #165 + #167 | Sprite asset gaps (70+8 PNGs) | Binary asset authoring — out-of-band, artist-driven (ComfyUI workflow documented in memory). |
| #169 | Custom Widgets allocation hoist | **Audit was wrong** — autonomous investigation found that `TwoDimensionDrawData` holds `SimpleMaterial` by REFERENCE; queued draws read CURRENT values at end-of-frame. Hoisting outside the loop and mutating per-iter would visually corrupt rendering. Future implementation must use `SimpleMaterial` pool with cache key, not hoist. |
| #142 (3 of 5 P2) | CareerSystem GameModels — 3 service extractions | 2 P2 resolved via #173; remaining 3 need `ICareerAgentStatService` extraction. |
| #143 (P2 perf) | FiefManagement Settlement.All on F6 | Bounded but suboptimal; needs `Clan.PlayerClan?.Settlements` fast-path. |
| #133 (P2 minor) | SpecialResources singleton reset + grace + seed-on-kingdom-change | Multiple R1 patterns + entity state matrix; needs careful test design. |

## Key technical discoveries

### Worktree base-stale agent failure
Spawning 10 parallel agents with `isolation: "worktree"` produced uniformly stale worktrees (~183 commits behind canonical). All agents reported environment failure. Mitigation: work sequentially in main tree, or fix worktree provisioning to branch from current HEAD instead of default.

### v1.3.15 `IMbEvent<T>` lacks Remove-one API
`IMbEvent<T>` exposes only `AddNonSerializedListener` + `ClearListeners`. The Codex audit's repeated suggestion to use `RemoveNonSerializedListener` is invalid for v1.3.15. The correct pattern is `ClearListeners(this)` + separate owner proxies if multiple listeners co-exist on a single object.

### `CampaignBehaviorBase` has no `OnFinalize`/`OnGameEnd` in v1.3.15
For singleton cleanup at campaign teardown, `CampaignEvents.OnGameOverEvent` is the only public lifecycle hook. Best-effort — covers death-of-character flow; doesn't cover main-menu-exit. Orphan listeners become GC-eligible once the campaign's CampaignGameStarter releases.

### `CampaignTime.NumTicks` is `internal`
For round-trip serialization in `SyncData`, the only public APIs are `RemainingHoursFromNow` + `CampaignTime.HoursFromNow(float)`. Float precision is fine for day-granularity deadlines (Siege used this).

### v1.3.15 WarOfTheRing config shipped with latent invariant violation
`Phase2.TriggerDay == Phase1.TriggerDay == 1` only worked because both `if` checks pass on the same daily tick. Now rejected by validation.

### #169 audit recommendation was unsound
The recommended `SimpleMaterial` hoist would corrupt rendering. Documented + closed with corrected design notes. This is a Phase 9b-discovery — not all audit findings are correct, and `/codex-verify` could not have caught this without deeper API tracing.

## Test count progression

| Phase | Tests |
|---|---|
| Phase 9a baseline | 1958 |
| After #173 CareerPassiveHelper | 1966 |
| After #132 Siege | 1972 |
| After #124 #130 | 1977 |
| After #128 #131 | 1982 |
| After #137 #147 + sub-issues | 2004 |
| After #182 #187 #188 source-content | 2018 |

## What's NOT in scope (future work)

The deferred dispositions above represent ~12 issues whose fixes would need their own focused sessions:

1. **CulturalFeats service extraction track** (#144 + #176) — multi-day effort
2. **Warg ADR-007 IAgentBattleAdapter expansion** (#178) — design-first, then implement
3. **Sprite asset authoring** (#165 + #167) — artist-driven, ~78 PNGs total
4. **Wage modifier service extraction** (#148 + #180) — pair refactor + tests
5. **CulturalFeats GameModel surface gaps** (#142 remaining) — 3 service extractions
6. **Widget allocation pool** (#169 corrected design) — needs profiler-driven prioritization first

## Deferred dispositions FULFILLED in follow-up batch (2026-05-14)

After the initial close-out, a parallel-agent batch was dispatched to fulfill the most impactful deferred dispositions:

| Issue | Commit | Result |
|---|---|---|
| **#144 + #176** CulturalFeats systemic | `4431cff` | `ICulturalFeatsService` (19 methods) + `ICultureFeatAdapter` (ADR-007) + concrete service + IoC + all 16 `Taom*Model.cs` refactored to thin boundaries. **+49 tests** in `CulturalFeatsServiceTests`. 26 files, +1437/-367. |
| **#178** Warg ADR-007 | `5a61e17` | `IWargAttackService` now adapter-pure. No new IAgentAdapter surface needed (existing surface sufficient). **+15 tests** (7 → 22; 2 deferred via `[Ignore]` for `ActionIndexCache.Create` engine dependency). |
| **#180 + partial #148** Wage modifiers | `64c5fab` | `IWageModifierService` extracted. Boundary helpers (`ResolveGarrisonInputs`, `ResolvePartyInputs`, etc.) translate sealed `CultureObject.HasFeat` → primitive `WageFeatInputs` struct at the model edge. **+28 tests** covering 33 test executions. |

**Net test count delta (full Phase 9):** 1958 → **2110** total (+152). Of those: 2107 passing, 2 skipped (Warg `[Ignore]`), 1 pre-existing data-integrity test failure unrelated to this work (user disabled 2 career XML entries by comment-out; `EveryJsonEntry_HasMatchingCareerInXml` correctly flags the JSON↔XML inconsistency).

**Deferred dispositions still remaining (NOT fulfilled this batch):**
- #165 + #167 sprite asset authoring (~78 PNGs) — artist-driven, out-of-band
- #169 widget allocation pool — audit was wrong; corrected design notes in close-out comment; future profiler-driven work
- #142 (3 of 5 P2) — CareerSystem GameModel surface (3 service extractions still inline)
- #143 (P2 perf) — FiefManagement Settlement.All on F6 (bounded but suboptimal)
- #133 (P2 minor) — SpecialResources singleton reset + grace period

## Closing commits

`docs/audits/phase-9-completion.md` (this file) marks Phase 9 complete.

The audit data structure (`docs/audits/*.md`) is preserved for historical reference. Cluster docs (`cluster-*.md`) retain the original findings catalog. The fix queue (`phase-9-fix-queue.md`) and triage results (`triage-results.md`) are the historical record of the triage decisions.

## Acknowledgments

This audit ran across 9 sequential phases over 2 days (2026-05-13 to 2026-05-14), with parallel-subagent fan-out per phase. Phase 9b spanned multiple sessions including a final autonomous marathon that closed 33 issues in approximately one continuous run.

Audit complete.
