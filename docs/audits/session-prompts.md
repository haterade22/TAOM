# Session Prompts for TAOM Feature Audit

Copy-paste prompts for each remaining audit session. Each is self-contained and tells the fresh session what to do, where to find authoritative inputs, what to produce, and when to stop.

**Convention:** every phase's session writes the NEXT phase's kickoff doc before stopping (so the audit can run unattended across many sessions). Phase 1 is the only one with a pre-written kickoff (`phase-1-kickoff.md`) — written in Phase 0.

**Combination guide:**
- Phases 2-6 (cluster reviews) can run as **5 separate sessions** (most thorough) OR be **batched into 2-3 sessions** if you want to move faster. Each cluster is independent; batching just costs context window.
- Phases 7 + 8 (tests + docs) are mechanical — can be a single combined session.
- Phase 9 is the only one that writes code. May span multiple sessions depending on fix volume.

---

## Phase 1 — Wiring Matrix

```
Continue the TAOM feature audit. Run /context-restore to load the latest snapshot.

Then read docs/audits/feature-manifest.md and docs/audits/phase-1-kickoff.md end to end. The kickoff doc has the full procedure — follow it.

Before starting Phase 1's probes, check git status. If Main/IoC.cs + Main/SubModule.cs show the uncommitted Messengers wiring fix from Phase 0, commit them now as their own atomic commit with: (a) CHANGELOG.md entry under today's date, (b) a retroactive GitHub issue per CLAUDE.md mandate ("Messengers shipped without IoC/CampaignBehavior wiring; encyclopedia crash on any hero click"), (c) issue # referenced in the commit message. Use /commit-split if multiple concerns are staged.

Then execute Phase 1 per phase-1-kickoff.md: 5 parallel Explore subagents, aggregate to docs/audits/wiring-matrix.md, open GitHub issues for P1/P2 findings (label: audit-wiring).

Constraint: NO fixes this session. Phase 1 is enumeration only. The Messengers fix is the only code change permitted (and only because it's pre-existing from Phase 0).

When wiring-matrix.md is complete and all issues are opened: write docs/audits/phase-2-kickoff.md for the next session, update docs/audits/README.md phases table, run /context-save with descriptor "phase1-wiring-complete", and stop.
```

---

## Phase 2 — GameModel Cluster Review

```
Continue the TAOM feature audit. Run /context-restore to load the latest snapshot.

Read docs/audits/feature-manifest.md, docs/audits/wiring-matrix.md (Phase 1 output), and docs/audits/phase-2-kickoff.md (written by the Phase 1 session). Follow the kickoff. Also re-read CLAUDE.md sections on GameModel pattern + .claude/rules/gamemodels.md.

Phase 2 = GameModel cluster review. Target: every feature with Model > 0 in the manifest. 12 features, ~39 model classes. CulturalFeats (17 models) is the highest-value target.

Per feature, spawn one feature-dev:code-reviewer or deep-review agent. Each agent:
1. Reads every Main/Features/<X>/Models/Taom*Model.cs in that feature.
2. Verifies override correctness: ?. on computed TaleWorlds properties, no inline if/foreach/switch/yield-branching per memory feedback_gamemodel_inline_logic.md, base.X() fallback chain.
3. Verifies the inline construction in SubModule.cs (or IoC wiring) doesn't drop a service dependency. This is the Messengers-class risk repeated — the original of which was a service registered with no caller; the GameModel variant is a model whose ctor expects a service but is constructed with `new Taom...(otherDep)` losing the service.
4. Cross-references TAOM.Tests/Features/<X>/ for model test coverage; flag gaps.

Use Agent calls with isolation: "worktree" if any agent will edit single-owner files. Phase 2 should not edit code (review only), so isolation isn't strictly needed — but if a model's deps surface a wiring bug, do NOT fix it; queue for Phase 9.

Aggregate to docs/audits/cluster-gamemodels.md. Open GitHub issues for P1/P2 findings (label: audit-impl). When complete, write docs/audits/phase-3-kickoff.md, update README phases, /context-save with descriptor "phase2-gamemodels-complete", stop.
```

---

## Phase 3 — CampaignBehavior Cluster Review

```
Continue the TAOM feature audit. Run /context-restore. Read feature-manifest.md, wiring-matrix.md, and docs/audits/phase-3-kickoff.md.

Phase 3 = CampaignBehavior cluster review. Target: every feature with Behavior > 0 in the manifest. ~15 features. Critical issues to look for:
- Missing OnSessionLaunched re-init (after save load, are fields reset?)
- Missing SyncData implementation (does state persist across saves?)
- Missing RegisterEvents (does behavior receive campaign events?)
- Idempotency on load (does the OnGameLoaded path mutate state that's already correct?)
- Reuse.Singleton + per-campaign state bugs (cross-campaign state leak)

Per feature, spawn one feature-dev:code-reviewer agent against Main/Features/<X>/*CampaignBehavior.cs + adjacent service. Read .claude/rules/csharp-architecture.md "Entity State Matrix" rule (MANDATORY for OnGameLoaded behaviors). Apply that matrix to each behavior's mutation paths.

Special targets from memory:
- MessengerCampaignBehavior — newly wired, full review wanted
- NamedCompanionBehavior — has been flagged before for OnSessionLaunched state mismatch (memory: review #23 EnsureCompanionsPlaced)

Aggregate to docs/audits/cluster-campaign-behaviors.md. P1/P2 to issues (label: audit-impl). Write phase-4-kickoff.md. /context-save "phase3-behaviors-complete". Stop.
```

---

## Phase 4 — Harmony Patch Cluster Review

```
Continue the TAOM feature audit. Run /context-restore. Read feature-manifest.md, wiring-matrix.md, phase-4-kickoff.md.

Phase 4 = Harmony patch cluster review. Target: every feature with Patch cat populated in the manifest (~27 features, 35 patch categories). Focus on the higher-risk patterns:

1. Prefix-returns-false patches MUST replicate every vanilla safety gate (memory: feedback_replicate_vanilla_safety_gates_in_prefix.md). Find every Prefix that returns false and audit against vanilla source via ilspycmd.
2. Patches on Formation/Mission/Scene types may fire from worker threads (memory: feedback_detect_engine_threading_via_mt_suffix.md). Check for unsynchronized state mutation.
3. Recursion-guard patterns — any patch that mutates state read by its own hot path needs a thread-static guard.
4. Postfix vs Prefix correctness — Prefix runs before vanilla logic; Postfix runs after. Some hooks need both.
5. Manual `_harmony.Patch(...)` calls — verify target method signatures match v1.3.15 via ilspycmd (NOT v1.4 decompiled folder).

Per patch category, spawn a taleworlds-researcher subagent to fetch the v1.3.15 vanilla signature + body for the patch target, plus a feature-dev:code-reviewer for the TAOM patch class. Cross-reference.

Aggregate to docs/audits/cluster-harmony-patches.md. Issues with label audit-impl. Write phase-5-kickoff.md. /context-save "phase4-patches-complete". Stop.
```

---

## Phase 5 — UI / Mixin / Prefab Cluster Review

```
Continue the TAOM feature audit. Run /context-restore. Read feature-manifest.md, wiring-matrix.md, phase-5-kickoff.md.

Phase 5 = UI cluster review. Targets: CareerSystem, Messengers, SpecialResources, TimeAcceleration (the 4 features with [ViewModelMixin] or [PrefabExtension] per manifest). Also include any Custom Widget classes.

Per feature, spawn a feature-dev:code-reviewer agent. Apply .claude/rules/gui-ui.md rules:
- Sprite name verification against TAOMSpriteData.xml (use grep, do NOT trust hardcoded paths)
- UIExtenderEx PrefabExtension safety: vanilla container indexing assumptions
- VM property setter no-op early returns (memory feedback_taleworlds_vm_setter_decompile.md)
- VM property notification: prefer public setter over reflected field+notify (memory feedback_prefer_public_setter_over_reflected_notify.md)
- @PropertyName binding case-sensitivity
- Localization {=key}Text via TextObject().ToString() (memory feedback_localization_textobject.md)

Also verify the prefab extensions are still finding their target containers in v1.3.15 (run ilspycmd to confirm the target VM still has the property the extension binds to).

Aggregate to docs/audits/cluster-ui.md. Issues label audit-impl. Write phase-6-kickoff.md. /context-save "phase5-ui-complete". Stop.
```

---

## Phase 6 — Cross-Feature Handshake Review

```
Continue the TAOM feature audit. Run /context-restore. Read feature-manifest.md, wiring-matrix.md, phase-6-kickoff.md.

Phase 6 = cross-feature collision and handshake review. Phase 2-5 reviewed features in isolation; this phase looks at the GAPS between features. Known and suspected collision pairs:

- SmartCavalryAI × MixedFormations × CompanionTactics — all patch Formation.SetMovementOrder or read formation layout state. Memory: feedback_cross_feature_handshake_via_shared_adapter.md. Verify cross-feature precedence is explicit.
- CulturalFeats × RevoltTuning — RevoltTuning feeds TaomSettlementLoyaltyModel which is "owned" by CulturalFeats. Verify the data-flow path.
- CharacterCreation × HeroRace × RaceAge — all touch Hero racial state. Patch3_SetRace, Patch5_FaceGen, Patch9_RaceFilter, Patch29_CCBodyProperties all interact.
- BannerColorPersistence × BannerInjection × Patch24_BannerDriftGuard — three features mutate banner colors.
- CareerSystem × TroopProgression — careers modify TroopWages / PartySize / PartyMovementSpeed which TroopProgression's models also touch.

Spawn one feature-dev:code-explorer agent per collision pair to trace the data flow. Plus one error-detective agent for the global "do any two features patch the same method?" sweep.

Aggregate to docs/audits/cluster-cross-feature.md. P1 (silent overwrite) and P2 (race-condition risk) to issues, label audit-impl. Write phase-7-kickoff.md. /context-save "phase6-crossfeature-complete". Stop.
```

---

## Phase 7 — Test Coverage Audit

```
Continue the TAOM feature audit. Run /context-restore. Read feature-manifest.md and phase-7-kickoff.md. Read ADR-008 (test coverage rules: 100% services, 100% engines, 80% hooks, entry points not required).

Phase 7 = test coverage audit per feature. For each of the 43 features:
1. Identify all *Service.cs and *Engine.cs files under Main/Features/<X>/.
2. Check TAOM.Tests/Features/<X>/ for corresponding test files.
3. Run `dotnet test --filter "FullyQualifiedName~<X>" --list-tests` to enumerate actual test methods.
4. For services with public methods: estimate coverage. ADR-008 mandates 100% on services.
5. Flag features with missing test directories (manifest already lists 2: BattleScenes intentional, CharacterSelection unclear).
6. Flag features where the test count is suspiciously low for the surface area (e.g., 30-method service with 5 tests).

Spawn one Explore subagent per feature batch (e.g., 5 batches of ~8 features). Each batch agent returns a coverage table.

Aggregate to docs/audits/test-coverage.md with a master table + the "below ADR-008 threshold" list. Open issues (label audit-tests) for each P1/P2 gap. Write phase-8-kickoff.md. /context-save "phase7-tests-complete". Stop.
```

---

## Phase 8 — Documentation Audit

```
Continue the TAOM feature audit. Run /context-restore. Read feature-manifest.md, phase-8-kickoff.md, and docs/features/TEMPLATE.md.

Phase 8 = documentation audit. For each of the 43 features:
1. Confirm docs/features/<x>.md exists. Manifest already flags Execution as missing.
2. Read the existing doc and check it has every section from TEMPLATE.md: Overview, Why This Exists, Architecture, Configuration, Key Files, Dependencies, Tests, How-To, Performance (if applicable).
3. Spot-check accuracy: pick 3 random claims (file path, MCM toggle name, save-compat note) and verify against current code. Stale docs are a known failure mode.
4. Flag docs that are pre-v1.3.15 and reference v1.2 APIs.

Spawn one Explore subagent per feature batch. Each returns a per-feature doc-status row.

Aggregate to docs/audits/docs-gaps.md. Open issues (label audit-docs) for missing or significantly stale docs. NOTE: small typos / single-sentence improvements should NOT become issues — note them inline in the audit doc only.

Write phase-9-kickoff.md (the big one — triage + fix). /context-save "phase8-docs-complete". Stop.
```

---

## Phase 9 — Triage + Fix Execution

```
Continue the TAOM feature audit. Run /context-restore. Read feature-manifest.md, every cluster doc, test-coverage.md, docs-gaps.md, and phase-9-kickoff.md.

Phase 9 = triage + fix execution. This is the FIRST phase that writes code (other than the Phase 0/1 Messengers fix). Process:

1. Pull every open GitHub issue with label `audit-wiring`, `audit-impl`, `audit-tests`, or `audit-docs`. `gh issue list -l audit-wiring -l audit-impl -l audit-tests -l audit-docs --state open --limit 200`
2. Triage by severity:
   - P1 (feature non-functional): fix this phase, one PR per fix or batched if mechanical
   - P2 (degraded / silently inert): fix this phase if simple, otherwise queue for next session
   - P3 (cosmetic): leave open, label `wontfix-now` and move on
3. Sort fixes by category — wiring fixes batch well (multiple feature wirings in one commit), implementation fixes don't (one per commit).
4. Per fix: branch from current head if multiple in parallel via worktree isolation OR fix sequentially in this branch. Use /verify after every fix. Use /codex-verify on non-trivial fixes (P1 impl bugs especially).
5. Mandatory completion workflow for every fix: build green → deep-review → fix issues → codex review → fix issues → final verify → close issue → CHANGELOG entry → docs/features update if behavior changed.

This phase MAY span multiple sessions. If the queue is large, work the top N% per session, /context-save, stop, resume.

Stop conditions per session:
- All P1 fixes for this session's category are landed AND verified AND issues closed.
- /context-save with descriptor "phase9-fix-<batch-name>" (e.g., "phase9-fix-wiring", "phase9-fix-models").
- Update README phases table to reflect % complete on Phase 9.

When the entire audit-* issue queue is empty: write docs/audits/audit-complete.md with the final summary, /context-save "phase9-complete", stop.
```

---

## After the audit

When `audit-complete.md` is written, the audit project is done. Consider:
- Closing out the audit branch into master
- Adding a `/skill-stocktake` cron schedule so this kind of drift gets caught next time before manual intervention
- Recording lessons learned as a memory entry under `~/.claude/projects/.../memory/` (the audit itself surfacing dozens of bugs is a feedback signal worth preserving)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/audits/phase-2-kickoff.md](./phase-2-kickoff.md)

<!-- backlinks-end -->
