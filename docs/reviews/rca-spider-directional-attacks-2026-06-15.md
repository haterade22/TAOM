# RCA — Spider directional-attack model + crash-fix guard (deep-review, 2026-06-15)

## Top-line

`/deep-review` over the uncommitted spider directional-attack feature (Part B) + the shared
`CustomAttacksUtils` NaN-geometry guard (Part A). 5 core agents (Standards / Compatibility /
Efficiency / Completeness / Data-flow). Result: **clean feature** — Standards PASS, Compatibility
PASS (37/0/0), Completeness COMPLETE (0 stale refs), Data-flow PASS (0 gaps). **One confirmed
finding**, MEDIUM, fixed in-session. One design divergence noted (not a bug).

## Findings

| # | Sev | Bug | Category | Why missed (by which agents) | Preventive action |
|---|-----|-----|----------|------------------------------|-------------------|
| 1 | MED (Agent 3 said HIGH; calibrated down) | `SpiderEngageDecorator.Evaluate` allocated a fresh `List<Agent>` every scan via `SpatialGrid.GetNearAliveAgentsInRange` (~5×/sec per spider). | Hot-path allocation | Not missed — Agent 3 (Efficiency) caught it. The *pattern* pre-existed: the deleted `SpiderCanBiteDecorator` and the shipping warg `NoEnemyCloseDecorator` do the identical thing. | Fixed: added an additive zero-alloc fill-overload to `SpatialGrid.GetAgentsInRadius`/`GetNearAliveAgentsInRange(range, target, buffer)`; `SpiderEngageDecorator` now fills a reusable `_scratch` field. Existing allocating overloads delegate to the buffer path (no behavioral change; warg unaffected). The warg can adopt the same overload later. |

### Severity calibration (evidence)

Agent 3 rated this HIGH ("per-frame"). Verified against source: it is **not** per-frame — the engage
decorator runs on the BT idle `SleepTask(200ms)` cadence (~5/sec per spider). Verified
`MissionAdapterFactory.GetAgentAdapter` **caches** (`_agentCache.GetOrAdd(agent.Index, …)`), so the
adapter wrappers are not re-allocated — the only per-eval allocation was the single `List<Agent>`.
Identical to the battle-proven shipping warg decorator. Honest severity is MEDIUM (per-eval,
cadence-bounded, single small List), not HIGH. Fixed anyway because the fix is clean + additive.

## Design divergence (noted, NOT a bug — no fix)

Agent 5 (Data-flow) flagged that `SpiderEngageDecorator` picks the **nearest** engageable enemy for
the bearing, whereas the sibling elephant `EnemyInTrampleRangeDecorator` picks the **best-facing**
(highest dot-product) enemy. Both produce a valid signed bearing; the spider's bone-collision swipe
hits whatever its fang/leg bones overlap during the animation, so the bearing only selects which clip
(left vs right) to play. Picking the nearest threat is a defensible warg-lineage choice. The
`[Spider][diag] ATTACK fire` log prints `bearing` + the resolved clip every fire, so if the swipe
direction feels wrong in-game it is a one-line swap (nearest → best-facing, or the two `Swing*` clip
consts). Recorded so it is not re-litigated; deliberately not "fixed" (YAGNI; the diag log is the
validation mechanism per [[feedback_comprehensive_diag_logging_then_remove]]).

## Root-cause pattern

**When porting a feature from a sibling but keeping a *different* subsystem, the sibling's hot-path
optimizations do not automatically transfer.** The spider is a warg→elephant mirror, but it kept the
**warg's `SpatialGrid` scan** (consistent with its bone-collision damage path) rather than adopting
the **elephant's `Mission.GetNearbyAgents(scratch)` scan**. The elephant's per-eval-allocation fix
lived in the scan API it switched to — so mirroring the elephant's *structure* did not inherit the
elephant's *allocation discipline*. The fix brings the optimization to the kept subsystem
(`SpatialGrid` now has a buffer overload) rather than forcing a scan-source change.

## Why each agent's result was correct

- **Agent 1 (Standards):** PASS — correct. The feature is a faithful elephant mirror; service is
  adapter-pure; BT nodes are accepted boundary classes; IoC use is lazy-cached `??=`.
- **Agent 2 (Compatibility):** PASS — correct, and valuable: independently confirmed the reflected
  private `Mission.RegisterBlow` 7-param signature still matches v1.4.x, and noted `Agent.Velocity`
  is a *computed* property that can propagate NaN mid-transition — corroborating the Part-A crash
  mechanism the `IsBlowGeometrySafe` guard addresses.
- **Agent 3 (Efficiency):** Caught the one finding. Over-rated severity (HIGH→MED) by assuming
  per-frame; the cadence is ~5/sec. Conclusion (fix it, elephant-parity) was right.
- **Agent 4 (Completeness):** COMPLETE — correct. All pure-helper branches tested (per-cell), 10
  NaN-guard tests, 0 stale references to the deleted classes/config. CHANGELOG/issue/feature-doc are
  pre-commit TODOs, not code gaps.
- **Agent 5 (Data-flow):** PASS — correct and thorough. Verified the load-bearing trace (clip-name →
  `ForName` cache → `CustomAttack`; anti-chain `IsSpiderAttack` covers all 4 clips; bearing sign
  consistent write↔read; cooldown kind→stamp mapping not swapped; both BT branches reachable).

## Feedback memories to codify

None new. The finding is a known perf pattern, not a correctness class, and the fix is local +
additive. The porting lesson (audit the *kept* subsystem's hot-path profile separately when mirroring
a sibling) is recorded here in the root-cause section; it does not rise to an always-loaded memory
(context-tax filter). No NaN-gate / data-flow / API-misread class recurred.

## Verification

- `dotnet build Main/TAOM.csproj` — succeeds.
- `dotnet test TAOM.Tests` — 3169 passed, 0 failed, 2 skipped (pre-existing). `SpatialGrid` +
  `BoneCollision` suites green (additive overload did not regress shared infra).
- In-game battle-test (AI-ridden spiders, 5+ min) remains the final gate for Part A (no `0x3` AV) and
  Part B (correct clip per bearing/cooldown via `[Spider][diag]`).

---

## Second deep-review pass (2026-06-15, post Patch48 + damage tuning)

A second `/deep-review` after adding Patch48 (dismount-on-hit guard), the damage/crit tuning, and the
temp recruit-weight edit. Standards / API-compat / data-flow all PASS again (Patch48's `HandleBlowAux`
target verified unique + private; crit threshold identical at both read sites; all 27 SpiderConfig
consts consumed; category string matches SubModule). Two confirmed findings, both fixed:

| # | Sev | Finding | Why missed / why it existed | Fix |
|---|-----|---------|------------------------------|-----|
| 3 | MED | `SpatialGrid.GetAgentsInRadius(buffer)` iterated **every occupied grid cell** + filtered by key, instead of enumerating only the radius bounding-box cells. | **Pre-existing** (the original allocating `GetAgentsInRadius` had the same `foreach (Grid)` + key-filter; this session's buffer-overload extraction preserved it). The first-pass efficiency agent flagged the per-eval *allocation* but not the *scan shape*; the buffer overload removed the alloc, leaving the O(all-cells) scan more visible. | Replaced with a `minX..maxX × minY..maxY × minZ..maxZ` loop + `Grid.TryGetValue` per cell. Bbox is ≤~27 cells at the creature scan ranges vs hundreds of occupied cells in a battle. Behavior-equivalent; benefits warg too. |
| 4 | LOW (cosmetic, diag log) | `HandleSpiderTargetHit` could log `damage=20 CRIT` when the dead-rider self-damage fallback overrides the crit-scaled value to 20 (the `isCrit` bool was computed before the override). | Sequencing: `isCrit` set at the crit roll, then the fallback overrides `damage` without touching `isCrit`. Near-unreachable in practice (fallback needs the spider *itself* at ≤0 HP at bite time). | Set `isCrit = false` in the fallback branch so the log stays honest. |

**Note on the diag logs:** Agent 3 asked whether the `[Spider][diag]` HIT/ATTACK logs spam in Release.
They are **intentional, attack-gated telemetry** for in-game tuning, removable after sign-off (the
standing comprehensive-logging-then-remove approach). Not a finding.

**Why no feedback memory:** finding #3 is the same "audit the kept subsystem" lesson already recorded
above (the buffer overload was that fix; this is its completion). #4 is a one-off sequencing nit.

**Verification (second pass):** build + tests deferred — the game was running during this pass and held
`TAOM.Dependencies\…\0Harmony.dll`, so the post-build deploy-copy failed (environment, not a compile
error). Both fixes are trivial (a nested `for` + `TryGetValue`; a one-line `bool` assignment). Re-run
`./build.ps1 -RunTests` once the game is closed to confirm green.
