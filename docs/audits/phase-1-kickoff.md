# Phase 1 Kickoff — Wiring Matrix

For the next session. Read this + [feature-manifest.md](feature-manifest.md) and start there — do NOT re-derive scope from CLAUDE.md or session memory.

## Goal

Verify every wiring claim in Phase 0's manifest by actual code inspection, and identify Messengers-class gaps where a feature is declared but a wiring touchpoint is silently missing. Output: `docs/audits/wiring-matrix.md` with a pass/fail cell for every (feature × dimension) pair, plus GitHub issues for actionable misses (label `audit-wiring`).

This phase is mechanical/grep-based. **It is NOT a code review.** Semantic correctness ("does the patch logic make sense") is Phase 2+. Phase 1 is "is the patch even applied at all."

## Inputs

- [feature-manifest.md](feature-manifest.md) — master matrix, 43 features × 11 columns, 17 queued items in "Open questions / Phase 1 targets" section
- [Main/IoC.cs](../../Main/IoC.cs) — feature IoC registrations
- [Main/SubModule.cs](../../Main/SubModule.cs) — AddBehavior, AddModel, PatchCategory, IoC.Resolve consumption
- [Main/Features/](../../Main/Features/) — feature directories, `*IoC.cs` files, `Hooks/Patch*.cs` files
- [Main/_Module/ModuleData/](../../Main/_Module/ModuleData/) — config files
- [Main/_Module/SubModule.xml](../../Main/_Module/SubModule.xml) — XML registrations
- [Main/Features/TaomSettings.cs](../../Main/Features/TaomSettings.cs) — MCM sections

## The 5 Phase 1 probe categories (one subagent per)

Spawn 5 parallel `Explore` subagents in a single message. Each probes one category across all 43 features. Each returns a finding table.

### Probe 1 — Manual-patch feature verification

**Target features:** SettlementGuards, SiegeDismount, BannerColorPersistence partial

These bypass the `_harmony.PatchCategory("...")` mechanism — they call `_harmony.Patch(AccessTools.Method(...))` directly. Verify the manual patches actually fire.

**Procedure per feature:**
1. Grep `Main/SubModule.cs` for the feature name in `_harmony.Patch(...)` calls.
2. For each call site, identify the target method (AccessTools.Method second arg).
3. Confirm the patch class exists under `Main/Features/<Feature>/Hooks/` and has matching `Prefix`/`Postfix`/`Transpiler` methods.
4. Verify the call is reachable (not behind `false` conditional, not commented out).

**Report:** Per feature: ✅ if all manual patches confirmed wired; ❌ with specific call-site + missing piece otherwise.

### Probe 2 — Inline-constructed GameModel dependency audit

**Target features:** CulturalFeats (17 models), Arena (1 model), Encyclopedia (1 model), any feature with `AddModel(new TaomXxx(...))` constructed inline without IoC

**Procedure per model:**
1. Open `Main/Features/<Feature>/Models/TaomXxx.cs`.
2. List constructor parameters.
3. For each parameter: is it a primitive (constant-OK), a sealed TaleWorlds type (boundary-OK), or an interface (`Ixxx`)?
4. If interface: confirm the inline construction in `SubModule.cs` passes a real service (resolved via `IoC.Resolve<Ixxx>()`), not `null` or a placeholder.

**Report:** Per model: ✅ if all deps are primitives or correctly-injected services; ❌ with the specific param + line otherwise. This is the highest-value probe — Messengers-class risk repeated.

### Probe 3 — Hook interface consumer/producer balance

**Target:** All `IOnXxx` interfaces resolved in `SubModule.cs` lifecycle methods (lines 124-138, 141-145, 157-159, 174, 182, 399-400, 423, 502-513)

**Procedure per interface:**
1. Grep `Main/` for `IOn<HookName>` to find the interface definition.
2. Grep for implementations: `class \w+ : IOn<HookName>` or `, IOn<HookName>`.
3. Confirm at least one implementation exists and is registered in some `*IoC.cs` (RegisterMany or Register).
4. Cross-reference: the patch class that uses `IoC.ResolveAll<IOn<HookName>>()` should find ≥1 implementation at runtime.

**Report:** Per hook interface: count of implementations, list of `*IoC.cs` files that register them. ❌ if zero implementations or zero registrations.

### Probe 4 — Service consumption audit (registered but never used?)

**Target features flagged in manifest:** AdvancedCombat, Spider, Warg + any others with IoC ✅ but no obvious Harmony/CampaignBehavior consumer

**Procedure per feature:**
1. List all services registered in `<Feature>IoC.cs`.
2. For each service interface, grep `Main/` for `IoC.Resolve<I<Service>>` or `IoC.ResolveAll<I<Service>>` or `: I<Service>` (constructor injection).
3. Confirm at least one non-test caller exists.

**Report:** Per service: ✅ if consumed somewhere; ❌ if registered-but-orphaned. Orphans are not necessarily bugs (e.g., used only from MCM lambdas), but each ❌ needs a one-line "consumed via X" justification.

### Probe 5 — Patch category orphans + lifecycle correctness

**Procedure:**
1. List every `[HarmonyPatchCategory("PatchN_Xxx")]` attribute across `Main/Features/**/Hooks/*.cs`.
2. Cross-reference against the `_harmony.PatchCategory(...)` calls in `SubModule.cs` (35 entries, per manifest).
3. Find category strings that exist in either set but not the other:
   - Category in code with no `PatchCategory` call = orphaned patch (never applied)
   - `PatchCategory` call with no matching attribute = dead category string
4. ALSO: for each applied category, verify the lifecycle phase is appropriate. The 6 early-phase categories (in `OnSubModuleLoad`) should NOT depend on `Mission.Current`, `Campaign.Current`, or any campaign-scoped object.

**Report:** Orphan list + lifecycle warnings. The `Patch_MissionTime_SetMovementOrder` deferred-to-OnMissionBehaviorInitialize pattern (memory: `feedback_movementorder_cctor_mission_current`) is the known-good template; any new patch on a type whose `cctor` reads `Mission.Current` must follow it.

## Output format

Create `docs/audits/wiring-matrix.md`:

```markdown
# Wiring Matrix — Phase 1

Last updated: <date>
Inputs: feature-manifest.md (43 features × 5 probe categories)

## Master matrix

| Feature | Probe 1 (manual patch) | Probe 2 (inline model deps) | Probe 3 (hook impl count) | Probe 4 (service consumption) | Probe 5 (patch lifecycle) | Issues opened |
|---|---|---|---|---|---|---|
| AdvancedCombat | N/A | N/A | N/A | ⚠ TBD | N/A | — |
| ... |

## Findings — Probe 1 (manual patches)
...

## Findings — Probe 2 (inline models)
...

(etc.)

## GitHub issues opened

| # | Title | Probe | Severity |
|---|---|---|---|
| ... |

## Phase 1 complete
- N features fully ✅
- M issues opened
- K Phase 2+ targets surfaced
```

## What counts as "open a GitHub issue"

| Severity | Open issue? | Examples |
|---|---|---|
| **P1 — feature non-functional** | YES, immediate | Messengers-class miss (IoC registered nowhere; CampaignBehavior never added; patch class with no PatchCategory call) |
| **P2 — feature degraded or silently inert** | YES | Hook interface with zero implementations; manual patch with mismatched signature; service registered + orphaned but doc claims it's wired |
| **P3 — cosmetic / nice-to-have** | NO, note in audit doc only | Cosmetic name mismatches (SiegeDefenseIoC under `Siege/` dir); registration order inside `IoC.cs` |

## Constraint: no fixes during Phase 1

Even if a finding is a 2-line wiring fix (like Messengers), DO NOT fix it in Phase 1. Phase 1's job is enumeration. Phase N+3 (triage + fix) batches the fixes so each gets its own commit with proper CHANGELOG + issue. Exception: a phase-blocking miss (e.g., the audit itself can't run because something is missing) — flag to user, ask whether to break the constraint.

## Done condition

Phase 1 is complete when:
- All 5 probes have run across all 43 features (where applicable).
- `wiring-matrix.md` is written with a populated master matrix + per-probe findings.
- Every P1/P2 finding has a GitHub issue (`audit-wiring` label).
- The `docs/audits/README.md` "Phases" table is updated with Phase 1 status.

Then `/context-save` again before closing out the session.

## Pre-flight checklist for the new session

1. `/context-restore` to load `phase0-audit-complete` snapshot.
2. Confirm git state matches expected (Messengers fix uncommitted OR already committed).
3. Read `docs/audits/feature-manifest.md` end-to-end. Read this brief.
4. Decide commit-Messengers-now vs. defer (recommended: commit first so audit branch is single-concern).
5. Spawn the 5 parallel probes.
