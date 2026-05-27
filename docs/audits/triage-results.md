# Phase 9a Triage Results — Master Aggregation

Verified by: 5 parallel `general-purpose` agents, Phase 9a, 2026-05-13
HEAD at verification: `b4b4de1 fix(messengers): wire IoC + CampaignBehavior (#121)`
Audit-of-audits inputs: `triage-input-batch-{A1,A2,B,C,D}.json` + cluster docs
Per-batch detail: [`triage-results-A1.md`](triage-results-A1.md), [`triage-results-A2.md`](triage-results-A2.md), [`triage-results-B.md`](triage-results-B.md), [`triage-results-C.md`](triage-results-C.md), [`triage-results-D.md`](triage-results-D.md)

## Master summary

| Verdict | Count | Issues |
|---|---|---|
| **VALID** (clean) | 74 | #122–#153, #155–#166, #168–#175, #176–#192, #194–#196, #198–#199 |
| **VALID** (with one sub-finding FALSE-POSITIVE) | 1 | #167 (P2 ordering claim about `IntValue/Value` order is FP; other 5 sub-findings VALID) |
| **STALE** | 1 | #154 (GauntletFiefManagementScreen already implements `IGameStateListener` since initial port `1cad3a7`) |
| **SEVERITY-DRIFT** | 2 | #193 (mechanism is `AddMissionBehavior`, not manual Harmony — fix scope changes); #197 (premise stale, severity P2→P3) |
| **FALSE-POSITIVE** (whole issue) | 0 | — |
| **DUPLICATE** (whole issue) | 0 | — |
| **Total** | 78 | — |

**Verification verdict on the audit overall:** The audit Phases 1–8 are 95% accurate. Only 1 issue is genuinely STALE (a "verify"-type request whose verification turned out positive), 2 have actionable severity/mechanism drift, and 1 has a single mis-stated sub-finding inside a multi-finding issue. **No whole-issue false positives, no duplicates.** This is a strong signal that the audit can be trusted as a queue.

### Severity recommendations (in-VALID advisory drifts — not formal SEVERITY-DRIFT verdicts)

Two issues are VALID at their current severity but the agents flagged upward-bump recommendations worth surfacing to the user:

| # | Current severity | Recommendation | Reason |
|---|---|---|---|
| **#191** Messengers wiring regression test | P2 | **P1 candidate** | This is the canonical regression class — the audit-motivating crash (#121) wouldn't have been catchable without this test. The fact that no `MessengerCampaignBehaviorTests.cs` exists IS the gap that allowed the crash. Treat as P1 for fix-queue priority. |
| **#134** Siege MobileParty NRE | P1 | Confirm fix scope | Audit cited two specific perks (`SiegeWorks`/`Counterweights`); current code uses different perk names (`Stonecutters`/`SiegeEngineer`) on the same unguarded `party.MobileParty.HasPerk(...)` path. Same NRE class, different perk references. Update issue body before fix lands. |

## Per-batch summaries

### Batch A1 (#122–#133) — 12 VALID

All twelve Phase 1 + early Phase 3 (Wiring + CampaignBehavior) findings re-confirmed against current HEAD. The only commit between audit and verification (`b4b4de1`) does not touch any of the implementation sites. See [`triage-results-A1.md`](triage-results-A1.md) for per-issue code quotes + proposed fix scopes.

Cross-issue dependency notes from A1:
- **#125** (CharacterCreation) has an additional `Hero.MainHero.IsFemale` direct access at line 202 not in the issue body — already covered by the `IPlayerHeroAdapter` recommendation; just widens the adapter surface.
- **#131** (RaceAge) `_raceIdCache` poisoning and the validate-before-lookup gap are linked — a bad lookup caches the "human" fallback for the cache's lifetime. Fix needs BOTH the reset AND the validation together.

### Batch A2 (#134–#148) — 15 VALID

All fifteen Phase 2 (GameModels) + remaining Phase 3 (CampaignBehavior) findings re-confirmed. Two textual drifts noted (verdict unaffected): #134 perk names, #147 field path. Both are surface-level — the underlying bugs (unguarded `MobileParty` NRE, sealed-static access in model body) are unchanged.

Cross-issue dependency notes from A2:
- **`CareerPassiveHelper` static deletion** ties together #142, #144, and #148 — a single refactor (extract to instance service + DI through `IoC.cs`) closes all three.
- **Concrete-cast pattern** in #141 (P1) and #146 (P3) can share one adapter-widening commit.

### Batch B (#149–#164) — 15 VALID + 1 STALE

- **#154 STALE** — `GauntletFiefManagementScreen` already implements `IGameStateListener`. Initial port commit `1cad3a7` shipped with the interface — the Phase 4 agent flagged it as "verify" (not as a confirmed bug). Verification answer: yes, it does. **Close with verification comment.**
- Both P1s (#149 Patch35 team filter, #150 MapConversationTableau color writes) re-confirmed.
- #157, #159, #160, #162 had `v1.3.15-unverified` flags in the original audit; the cluster doc's Cluster F closure already resolved the signature uncertainty for these but the secondary concerns (bare-catch, runtime-binding, hard-throw, defensive null-guard) remain VALID.
- #164 is a consolidated multi-cleanup tracker; sub-items all VALID.

### Batch C (#165–#175 + #196–#199) — 13 VALID + 1 VALID-with-sub-FP + 1 SEVERITY-DRIFT

- **#167 sub-finding FALSE-POSITIVE** (P2 #8 ordering): the audit claimed `_resourceInfo.IntValue` was assigned before `_resourceInfo.Value`, causing a one-frame stale render. Current code at `SpecialResourceMapBarMixin.cs:64–65` shows `Value` is assigned at line 64 BEFORE `IntValue` at line 65 — the order is already correct. Other 5 sub-findings in #167 (sprite gaps, `SecondaryInfoItems.Add` rule, PrefabExtension fragility, localization, diagnostic flag) all VALID.
- **#197 SEVERITY-DRIFT** (P2 → P3): the audit said CompanionTactics is build-disabled and the disclosure is buried. Commit `0cc457f` (2026-05-07) restored CompanionTactics integration BEFORE the audit date — meaning the audit was already operating on stale state. The doc still has a stale "build-disabled" note that should be removed (1-line fix), but the underlying premise (feature non-functional) was already false at audit time. P3 doc-staleness.
- Sprite gap claims (#165, #167) numerically exact: 21 portraits + 9 abilities + 0 choice icons → matches audit's "29 portraits / 41 abilities / all choice icons missing" exactly. No asset additions since 2026-05-13.
- Cross-feature handshakes (#170–#175) all VALID; test-gap claims confirmed by grep.
- Docs #196 (Execution missing) and #198/#199 (stale "no tests" claims) all VALID at HEAD.

### Batch D (#176–#195) — 19 VALID + 1 SEVERITY-DRIFT

- **#193 SEVERITY-DRIFT** (mechanism-drift, not pure severity-drift): the audit says SiegeDismount uses `manual _harmony.Patch(...)` and asks for a Harmony-patch wiring test. Current code shows SiegeDismount wires via `mission.AddMissionBehavior(new SiegeDismountMissionBehavior())` at `Main/SubModule.cs:493` — NOT a manual Harmony patch. The wiring-test gap is real (the audit-motivating regression class), but the FIX SCOPE changes from "Harmony patch binding test" to "MissionBehavior wiring smoke test." Update issue body before Phase 9b begins coding.
- **#191 advisory severity drift** (see "Severity recommendations" above): canonical regression-class root, no `MessengerCampaignBehaviorTests.cs` exists. P2 → P1 candidate.
- **#186 Spider** audit body slightly overstates the gap — `ComputeSpawnPosition` math IS tested at lines 84-114; actual gap is team-assignment and monster lookup. Still VALID; fix scope narrows.

`git log --oneline --since="2026-05-13" -- TAOM.Tests/` returned no commits — no test additions could STALE any issue.

## Cross-batch dependency clusters

These dependency clusters are pre-Codex inputs to fix-queue construction (Step 7):

| Cluster | Issues | Recommended grouping |
|---|---|---|
| **CareerPassiveHelper service extraction** | #142, #144, #148, #173 | Single refactor commit closes 4 issues; affects 8 GameModels' override bodies |
| **R1 — Singleton state reset across campaigns** | #124, #127, #128, #130, #131, #132, #133, #136, #143 | Coordinated `OnNewGameCreatedEvent.AddNonSerializedListener` retrofit across 9 features; per Phase 3 R1 pattern |
| **R2 — Empty / drop-on-load SyncData** | #128, #132, #133, #136, #141 (FiefManagement: transient), #146 | Per-feature SyncData implementations; pattern from existing `RacePersistenceBehavior.SyncData` |
| **R3 — Config provider validation** | #126, #128, #129, #131, #132, #133, #136 | Add `FiniteFloatValidator` + range/ordering invariants at deserialization boundaries |
| **R5 — Vanilla safety gates dropped in Prefix** | #149, #150 (P1 pair), #157, #160 | Re-replicate vanilla safety gates per `feedback_replicate_vanilla_safety_gates_in_prefix.md` |
| **Banner triplet** | #122 (closed STALE? — VALID per Batch A1), #172, #187 | Existing Phase 6 cluster; #122 is the wiring-init root |
| **SmartCavalry triplet handshake** | #170, #182, #189, #190 | Add cross-feature contract tests for cavalry-exclusion handshake |
| **CC × HeroRace × RaceAge race-ID** | #171, #181, #183 | Coordinated race-ID round-trip test + Patch ordering decision |
| **NamedCompanions Review #23 regression class** | #127, #184 | Prisoner + Fugitive state branch addition + state-matrix test |
| **Sprite asset authoring** | #165 (50 careers × 3 sprite types = ~120 sprite IDs), #167 (8 of 11 resources) | Asset authoring effort + atlas registration; not pure code work |

## Closing-comment drafts (for STALE / FALSE-POSITIVE / DUPLICATE / SEVERITY-DRIFT)

These drafts can be pasted verbatim into `gh issue close --comment` or `gh issue comment` calls in Step 6 (after Codex reconciliation). Full text lives in the per-batch detail files; below is the index.

| # | Action | Closing comment location |
|---|---|---|
| #154 | `gh issue close 154 --comment "$(cat ...)"` | [`triage-results-B.md`](triage-results-B.md) — "Closing comment draft" under #154 detail |
| #197 | `gh issue edit 197 ...` (severity relabel) + body update | [`triage-results-C.md`](triage-results-C.md) — under #197 detail |
| #167 | (no whole-issue close; sub-finding correction goes in fix-PR body) | [`triage-results-C.md`](triage-results-C.md) — under #167 detail |
| #193 | `gh issue comment 193 ...` (mechanism correction) | [`triage-results-D.md`](triage-results-D.md) — under #193 detail |

## Verification meta-notes

- **No issues were re-classified DOWNWARD in priority** by the verification — the audit's severity calibration held up.
- **No interim commits to production code** between audit (2026-05-13) and HEAD (`b4b4de1`) touched any issue's referenced files. The only intervening commit is the Messengers wiring fix itself, which closed #121.
- **No tests added** since the audit (`git log --oneline --since="2026-05-13" -- TAOM.Tests/` empty).
- **No `docs/audits/` content** was in HEAD at audit time — all phase outputs are untracked. Phase 9a is the first read of those files into git's awareness.

## Open items for Codex review (Step 5)

Codex should specifically adversarially check:

1. **The 5 STALE-or-altered verdicts** (#154, #167's sub-FP, #197, #193, advisory drift on #191) — these are the only places verification disagreed with the audit. False conviction of FP/STALE here would re-introduce real bugs into the fix queue. False conviction of VALID where the bug is actually fixed wastes a Phase 9b session.
2. **Quote completeness** — every VALID verdict must have a code quote that matches the current file. A drift in the quote (line shift, refactor not noticed) could mask a STALE.
3. **Cross-batch dependency assertions** — the "Cross-batch dependency clusters" section above is the agents' synthesis, not pulled from any single doc. Codex should re-derive whether the cluster groupings are accurate.

## Step 5 — Codex adversarial review result

Codex was invoked via `codex exec --skip-git-repo-check` with a focused prompt covering the 4 non-VALID verdicts + 1 advisory severity bump. Session: `019e22dc-1242-7940-80f3-8a9e6565782f`. Tokens used: 29,881.

Codex independently re-read each cited file and returned:

| Verdict | Codex result | Codex's quoted proof |
|---|---|---|
| #154 STALE | **AGREES** | `16: public class GauntletFiefManagementScreen : ScreenBase, IGameStateListener` |
| #167 P2 #8 ordering sub-FP | **AGREES** | `64: _resourceInfo.Value = intAmount.ToString(); 65: _resourceInfo.IntValue = intAmount;` (Value DOES precede IntValue) |
| #193 mechanism-drift | **AGREES** | `493: mission.AddMissionBehavior(new SiegeDismountMissionBehavior());` (not `_harmony.Patch`) |
| #197 P2→P3 severity-drift | **AGREES** | `0cc457f 2026-05-07 fix(companion-tactics): restore Patch35 integration after parallel-port revert` (predates 2026-05-13 audit) |
| #191 advisory P2→P1 | **AGREES** | `TAOM.Tests/Features/Messengers/` contains `MessengerConfigProviderTests.cs; MessengerServiceTests.cs; MessengerStateStoreTests.cs` — no `MessengerCampaignBehaviorTests.cs` |

**Zero disagreements.** Codex confirmed every non-VALID and advisory drift verdict the agents proposed. Phase 9a Step 6 reconciliation has nothing to flip — proceed to issue closures.

Scope note: Codex was NOT asked to re-verify every single VALID verdict (74 issues) because the working-tree review previously hit ENOBUFS on Windows spawnSync max-buffer when handed the full doc set. The focused 5-verdict scan is the actionable subset: it covers every place verification disagreed with the audit. The remaining 74 VALID verdicts have been mechanically cross-checked by the per-batch agent quotes (each VALID row has a verbatim code quote in its detail file).

## Next step

Step 6: close non-VALID issues with the drafted comments, edit #197 label + body, comment on #193 with mechanism correction, and confirm advisory #191 with the user during checkpoint reporting.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/audits/phase-9-fix-queue.md](./phase-9-fix-queue.md)

<!-- backlinks-end -->
