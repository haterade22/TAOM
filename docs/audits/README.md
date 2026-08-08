# TAOM Feature Audit

A multi-phase audit verifying that every feature under `Main/Features/` is wired correctly and implemented per TAOM conventions.

Motivated by the Messengers crash on 2026-05-13: commit `03a41b6` shipped the Messengers module + tests + docs + localization but skipped IoC + SubModule wiring, and no other gate caught it before in-game. The audit's first job is to make sure no other feature is in the same state.

## Phases

| # | Name | Output | Status |
|---|---|---|---|
| 0 | Manifest — enumerate features, classify archetypes, sketch the wiring expectations | `feature-manifest.md` | **Complete (2026-05-13)** |
| 1 | Wiring matrix — automated probes per feature: IoC, CampaignBehavior, GameModel, Harmony, MCM, ModuleData, docs, tests | `wiring-matrix.md` | **Complete (2026-05-13)** — 1 P2 issue (#122), 1 P3 manifest discrepancy (SiegeDismount), 0 P1 |
| 2 | GameModel cluster review — per-feature semantic review of all `Taom*Model.cs` overrides (11 features, 38 models) | `cluster-gamemodels.md` | **Complete (2026-05-13)** — 10 issues opened (#134, #135, #137, #138, #140, #142, #144, #145, #147, #148); 2 P1 (#134 Siege MobileParty NRE, #135 CulturalFeats PartySpeed NRE); 42 P2; all 38 ctors pass construction integrity (no Messengers-class drops in model layer). Findings overlap with #129 (Diplomacy) and #131 (RaceAge) handed off rather than duplicated. |
| 3 | CampaignBehavior cluster review — `*CampaignBehavior.cs` + adjacent services (16 features × 19 behaviors) | `cluster-campaign-behaviors.md` | **Complete (2026-05-13)** — 16 issues opened (`audit-impl`): #123 Messengers, #124 BannerInjection, #125 CharacterCreation, #126 InitialChildGeneration, #127 NamedCompanions (special target — Review #23 regressed in Prisoner+Fugitive states), #128 CareerSystem, #129 Diplomacy, #130 HeroRace, #131 RaceAge, #132 Siege (empty SyncData = events lost on every load), #133 SpecialResources, #136 StartupResources, #139 CompanionTactics, #141 EquipPresets, #143 FiefManagement, #146 QuickActions. 24 P1 + 33 P2 + 25 P3. **5 recurring patterns identified (R1-R5)** — see cluster doc; Phase 9 should batch-fix by pattern. |
| 4 | Harmony patch cluster review — 134 patch files / 35 categories / 7 manual sites against v1.3.15 vanilla | `cluster-harmony-patches.md` | **Complete (2026-05-13)** — 16 issues opened (#149-164): 2 P1 (#149 Patch35 SetMovementOrder race, #150 MapConversationTableau color no-op), 13 P2, 1 consolidated P3 (#164). Residual gaps queued (v1.3.15 ilspycmd verification on Cluster C/E sites — 6th agent dispatched). Phase 1 carryover noted (#122). |
| 5 | UI / Mixin / Prefab cluster review — CareerSystem, Messengers, SpecialResources, TimeAcceleration + Custom Widgets | `cluster-ui.md` | **Complete (2026-05-13)** — 5 issues opened (`audit-impl`): #165 CareerSystem (4 P1 sprite gaps + 2 P3 localization), #166 Messengers (1 P1 wrong-VM notification + 1 P2 dead property), #167 SpecialResources (1 P1 sprite gap + `SecondaryInfoItems.Add` rule violation downgraded P1→P2 after v1.3.15 verification + 2 P3), #168 TimeAcceleration (1 P2 wrong state signal + 1 P3 tooltip), #169 Custom Widgets (6 P2 perf/threading/locator + 3 P3). **Dominant Phase 5 bug class: silent broken UI from missing sprite assets**, not VM logic. 25 findings total: 6 P1, 12 P2, 7 P3. |
| 6 | Cross-feature handshake review — SmartCavalryAI × MixedFormations × CompanionTactics, CulturalFeats ↔ RevoltTuning, etc. | `cluster-cross-feature.md` | **Complete (2026-05-13)** — 6 issues opened (`audit-impl`): #170 SmartCavalry triplet (handshake test gap + threading asymmetry), #171 CC×HeroRace×RaceAge (stale race ID + Prefix ordering), #172 Banner triplet (all-clan drift block + null-guard + event ordering), #173 CareerPassiveHelper (service locator + race + inline foreach P1 + int truncation), #174 SpecialResources×Career (discount not applied to debit), #175 FactionMap (`_factionVM` stale + `_pendingPins` bleed on CC re-entry). **41 findings: 2 P1 (1 net-new, 1 cross-ref to #122), 13 P2, 26 P3.** Gauntlet threading verdict settled — Phase 5 #15/#16 downgrade to P3; #25 resolved. CulturalFeats×RevoltTuning and TimeAcceleration×MapBar yielded design confirmations only (no issues). Global same-method-patch sweep: 0 net-new collisions. |
| 7 | Test coverage audit — ADR-008 compliance per feature | `test-coverage.md` | **Complete (2026-05-13)** — 20 issues opened (#176–#195): 3 P1 (CulturalFeats 16-models-zero-tests, FiefManagement 5-callbacks-untested, Warg ADR-007-blocks-testing) + 17 P2 (wiring-regression test gaps, cross-feature handshake test gaps, untested GameModel branches). 8 P3 + 16 OK. Phase 0 carryovers resolved: CharacterSelection (transpiler — untestable by design, documented); BattleScenes (disabled — correct absence). Phase 5 #168 verified RESOLVED. Dominant gap: 80% behavior-hook coverage and manual-Harmony patch wiring tests. |
| 8 | Documentation audit — `docs/features/<X>.md` per feature, matches `TEMPLATE.md` | `docs-gaps.md` | **Complete (2026-05-13)** — 4 issues opened (#196–#199): 1 P1 (Execution doc missing — Phase 0 #19 carryover, closed) + 3 P2 (CompanionTactics build-disabled hidden; AdvancedCombat + Warg stale "no tests" claims). ~9 inline-only findings (sibling-doc cross-refs, historical migration context, test-count summary mismatches) per user rule "small typos / single-sentence improvements should NOT become issues." ~31 features pass outright. **Surprise: doc-staleness manifests as stale test-count claims, not v1.2 API drift.** |
| 9 | Triage + fix execution — actionable findings get GitHub issues + closing commits | (issues + commits) | **Complete (2026-05-14)** — see [phase-9-kickoff.md](phase-9-kickoff.md). **All 79 audit issues closed (100%)** across ~33 commits. ~46 fixed in code, ~12 deferred with documented dispositions (CulturalFeats systemic, sprite-asset authoring, etc.). Test count: 1958 → 2018 (+60). Build green throughout. See [phase-9-completion.md](phase-9-completion.md) for the close-out report. |

Each phase is intended to fit a single Claude Code session. Inside a phase, parallel subagents fan out across features or feature batches.

## Other audits filed here

Not part of the phase series — same directory, different subject.

| Doc | Scope | Outcome |
|---|---|---|
| [issue-triage-2026-08-08.md](issue-triage-2026-08-08.md) | All 147 then-open GitHub issues re-checked against HEAD `828bf941` to answer one question: is this still an issue? | **81 closed, 66 left open** (60 kept + 6 escalated for a decision). Per-issue verdict + evidence, the three-stage method (evidence index → 18 cluster agents → 12 adversarial refuters, which killed 2 proposed closures), and the failure-mode table — each trap in it had already produced a wrong answer in this repo at least once. |

## Conventions

- **Source of truth** for "what features exist": top-level directories under `Main/Features/` (43 features as of 2026-05-13). `TaomSettings.cs` at that level is a file, not a feature.
- **Finding tracking**: P1/P2 findings get a GitHub issue with `audit-<phase>` label per the user-approved tracking choice. The audit doc keeps the historical record; the issue tracks the fix.
- **No fixes during audit phases** unless the fix is a one-liner wiring miss in the same class as the Messengers fix (and is logged in the phase output for traceability).
- **Closing a phase**: the phase doc gets a "Phase complete" section listing every gap, every issue opened, and every follow-up phase the findings inform.

## How to consume during a Phase 1+ session

1. Read this README + the manifest.
2. Open the relevant phase doc — it lists the features in scope for that phase.
3. Spawn parallel subagents for the probes/checks.
4. Append findings to the phase doc as the agents return.
5. Open GitHub issues for actionable findings (label: `audit-<phase>`).
6. Update this README's "Phases" table status column at the end of the session.

## Related skills / hooks

- `detect-docs-gaps.sh` (SessionStart hook) — already flags `Main/Features/<X>` directories with no matching `docs/features/*.md`. Useful sanity check before Phase N+2.
- `/scope-check` — when an audit phase tempts you to fix beyond wiring, run this first.
- `/deep-review` — appropriate for Phase 2+ cluster reviews; **not** for Phase 1 (too heavy for mechanical probing).
- `/codex-verify` — appropriate for cross-checking each phase's findings once a draft exists.
