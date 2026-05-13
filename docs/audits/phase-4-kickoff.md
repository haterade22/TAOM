# Phase 4 Kickoff — Harmony Patch Cluster Review

For the next session. Read this + [feature-manifest.md](feature-manifest.md) + [wiring-matrix.md](wiring-matrix.md). Phase 1 is now complete (2026-05-13) and supersedes the earlier "standalone" framing of this doc — Phase 4 can rely on Phase 1's outputs as authoritative for wiring/lifecycle/orphan checks and focus its budget on semantic correctness. Phase 2 ([cluster-gamemodels.md](cluster-gamemodels.md)) and Phase 3 ([cluster-campaign-behaviors.md](cluster-campaign-behaviors.md)) are now also complete (both 2026-05-13). Phase 4 can use either's findings as cross-reference but does not depend on them — patch-side review is independent.

## Goal

Audit every Harmony patch in TAOM (134 files / 35 categories / 8 manual `_harmony.Patch` sites) against vanilla v1.3.15 source. Identify missing safety gates, threading hazards, signature drift, recursion risk, and Postfix↔Prefix mistakes. Output: `docs/audits/cluster-harmony-patches.md` + GitHub issues with `audit-impl` label for every P1/P2 finding.

This phase is a **semantic correctness review** — not a wiring check. Phase 1's wiring matrix already validated "is the patch even applied"; Phase 4 assumes it is and asks "does the patch do the right thing."

## Carryover from Phase 1 (do NOT re-do)

Phase 1's wiring-matrix.md already established:

- **All 36 patch categories balance correctly** (Probe 5): 35 active `_harmony.PatchCategory(...)` calls + 1 commented-out (`Patch0_BattleScenes` — intentional). Zero orphaned `[HarmonyPatchCategory]` attributes, zero dead `PatchCategory` calls.
- **All 6 early-phase categories cleared for `Mission.Current` / `Campaign.Current` cctor-NRE risk** (Probe 5 Part B): Patch25_LocalizationOverride, Patch18_CulturalFeats, Patch19_CustomBattles, Patch21_ShaderPrecompilation, Patch22_ArmyTargeting, Patch30_MixedFormations — none crash JIT prep on `OnSubModuleLoad`. **One smell to follow up:** Patch30_MixedFormations reads `Mission.Current?.Scene` from the early phase via `?.`; works today but could be cleaner if deferred to `OnMissionBehaviorInitialize` like `Patch_MissionTime_SetMovementOrder`. Phase 4 Cluster D should re-examine and decide.
- **All 7 manual `_harmony.Patch(...)` sites verified wired** (Probe 1): SettlementGuards ×2, BannerColorPersistence ×4, plus a CompanionTactics captain-tooltip site. **One P2 already filed:** issue #122 — `MobilePartyVisual_AddCharacterToPartyIcon_Patch` is Harmony-bound but its `Initialize(...)` is never called → silent no-op. Phase 4 does NOT need to re-find that. (The Phase 4 Cluster C signature-drift review is still valuable; it's the v1.3.15-vs-source check that Phase 1 did not perform.)
- **SiegeDismount misclassified in manifest** (P3 doc note): manifest says "Patch cat = manual" but the feature has NO `_harmony.Patch(...)` site; it's a `MissionBehavior` added at `SubModule.cs:493`. Phase 4 Cluster C can skip SiegeDismount (it's not a Harmony patch feature). The manifest correction lands in a Phase N+2 docs pass.

Net effect: Phase 4 Clusters A, B, C, E are unchanged in scope. **Cluster D's lifecycle-correctness sub-check can be deprioritized** (Phase 1 cleared cctor-NRE for the 6 early categories); focus Cluster D's budget on the cross-feature handshake angle (SmartCavalryAI × CompanionTactics × MixedFormations on `Patch_MissionTime_SetMovementOrder`) which Phase 1 did not cover.

## Surface

Enumerated 2026-05-13 from `Main/Features/**/Hooks/*.cs` + `Main/SubModule.cs`:

| Metric | Count |
|---|---|
| Patch files under `Main/Features/**/Hooks/` | 134 |
| `_harmony.PatchCategory(...)` calls in `SubModule.cs` | 35 |
| Manual `_harmony.Patch(AccessTools.Method(...))` calls | 7 sites (lines 417-471) — original count "8" included a SiegeDismount entry the manifest classified as manual; Phase 1 found SiegeDismount has no `_harmony.Patch(...)` site, it's a MissionBehavior |
| Files containing `return false;` (Prefix-skip-vanilla candidates) | 24 |
| Files using `[ThreadStatic]` / `ThreadLocal<>` / `lock(...)` | 1 (`Patch34_SellAllItemsMenu.cs`) |

## Risk patterns (all 4 enforced)

1. **Prefix-returns-false safety-gate replication** — replicate every vanilla null/bounds/`IsValid` check (memory: `feedback_replicate_vanilla_safety_gates_in_prefix.md`).
2. **Worker-thread / `_MT` signature audit** — Formation/Mission/Scene patches may fire from worker threads (memory: `feedback_detect_engine_threading_via_mt_suffix.md`).
3. **Recursion-guard for state-mutating hot paths** — Postfix re-entry needs a thread-static depth counter (template: SmartCavalryAI `SmartCavalryRecursionGuard`).
4. **Postfix vs Prefix correctness + manual-patch signature drift** — manual `_harmony.Patch(AccessTools.Method(...))` binds by reflection to v1.3.15. **Verify signatures with `ilspycmd` on installed DLLs**, NEVER `E:\Decompiled_Bannerlord\` (v1.4).

## 5-cluster fanout

One `feature-dev:code-reviewer` agent per cluster. No worktree isolation needed (review-only). Each agent calls `ilspycmd` directly, may spawn `taleworlds-researcher` sub-agents for decompilation it can't do itself, and returns a structured findings table.

### Cluster A — Prefix-returns-false / vanilla-skip patches (HIGHEST RISK)
~15-18 real Prefix-skip patches (filtered from the 24-file `return false;` grep). See plan file for the candidate list. Per file: confirm Prefix returns false, decompile vanilla, enumerate every safety gate including helpers 1-2 levels deep, verify each gate is replicated.

### Cluster B — Formation/Mission/Scene threading audit
Every patch whose target is `Formation`, `Mission`, `Scene`, `MBMapScene`, `Agent`. Search vanilla for `_MT` suffix helpers + `TWSharedMutexReadLock` usage. Verify state mutations are lock-protected, thread-static, or pure-read.

### Cluster C — Manual `_harmony.Patch(...)` signature drift (7 sites)
Every site in `Main/SubModule.cs` lines 417-471 (CompanionTactics tooltip, SettlementGuards x2, BannerColorPersistence x4). Run `ilspycmd` against v1.3.15 DLL per site. Verify method exists, parameter types match, optional `__instance`/`__result`/`___field` use vanilla field names.

**Phase 1 carryover:** All 7 sites are wired (Probe 1 confirmed). One site is wired-but-uninitialized (issue #122, deferred to Phase 9). Phase 4's job is the v1.3.15 signature verification Phase 1 did NOT do — drift would manifest as `AccessTools.Method(...)` returning null and the patch silently skipping (the "logger warns" path in SubModule.cs lines 442/451). Worth re-grepping for `LogWarning("[<feature>]" ... "not found"` to confirm those branches aren't being hit at runtime.

SiegeDismount is NOT in this cluster despite the manifest's "Patch cat = manual" — it's a MissionBehavior, not a Harmony patch (per Phase 1 finding).

### Cluster D — Shared deferred categories + cross-feature handshake
- `Patch_MissionTime_SetMovementOrder` cross-feature handshake (SmartCavalryAI + CompanionTactics share) — **primary Cluster D target**; Phase 1 did not review the handshake semantics
- `Late_Transpiler` (CharacterSelection)
- `Late_ActionSetOverride` (HeroRace)
- 6 `OnSubModuleLoad`-phase categories: **Phase 1 already cleared cctor-NRE risk** — DO NOT re-verify cctor reads of `Mission.Current` / `Campaign.Current`. Only re-examine Patch30_MixedFormations to decide whether the smell (reads `Mission.Current?.Scene` from early phase via `?.`) warrants deferring to `OnMissionBehaviorInitialize` like the SetMovementOrder pattern

### Cluster E — Postfix-only / Transpiler-only sanity sweep (LOW RISK)
Everything not in A/B/C/D. Target-method-exists check via `ilspycmd` per file is mandatory; deeper review only if anything looks off. Promote anything misclassified to A/B/C/D in findings.

## Output

`docs/audits/cluster-harmony-patches.md` with:
- Per-cluster findings table: `Patch | Severity (P1/P2/P3) | Issue | Vanilla source line | Fix sketch`
- Cross-cluster summary
- GitHub-issues-opened table

Per finding severity:
- **P1** (patch is broken / vanilla side effect lost): open issue, label `audit-impl`
- **P2** (degraded behavior / silent inert path): open issue, label `audit-impl`
- **P3** (cosmetic / observation): doc only

## Constraints

- **No fixes during Phase 4.** Even 2-line wiring fixes are deferred to Phase 9.
- **Don't trust v1.4 decompiles.** Use `ilspycmd "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\<dll>" -t "<type>"` for every signature claim.
- **Cluster E target-method-exists check is mandatory** even if no other finding is reported.

## Done condition

1. `docs/audits/cluster-harmony-patches.md` exists with 5 cluster sections.
2. Every category in the manifest's `Patch cat` column is covered (except SiegeDismount per Phase 1 finding — see Carryover).
3. P1/P2 findings have GitHub issues (`audit-impl` label).
4. `docs/audits/phase-5-kickoff.md` written for the next session (UI cluster).
5. `docs/audits/README.md` Phases table updated.
6. `/context-save` snapshot written with descriptor `phase4-patches-complete`.
