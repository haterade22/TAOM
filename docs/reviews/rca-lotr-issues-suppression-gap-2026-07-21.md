# RCA — LotrIssues suppression gap + review findings (2026-07-21)

**Top line:** a Patreon crash report (TAOM v2.0.12, new Rhûn campaign) revealed the 7 SandBox-module
vanilla issue behaviors were never suppressed in-game — `Type.GetType("…, SandBox")` silently returns
null for LoadFrom-context module assemblies — and one of them (`NotableWantsDaughterFound`) CTDs at
quest-accept on TAOM data (its rogue-template lookup goes through the XSLT-deleted `steppe_bandits`
clan → `CreateSpecialHero(null, …)` NRE). Fix: loaded-assembly-scan resolution + `LogError` on
under-count + an `OnGameLoaded` sweep cancelling uncommitted vanilla issues in existing saves.
Deep-review (5 agents) confirmed the fix and produced 4 findings, all fixed in-session; 1 finding
disputed. GitHub issue #355.

## Findings table

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 0 | CRIT (the original bug) | `Type.GetType("T, SandBox")` returns null in-game → 7 vanilla issues live in every campaign since 2026-06-20; daughter quest CTDs on accept | Assembly load-context interop | Designed for the SandBox-less test host; graceful degradation (a WARNING log) masked total in-game failure; the "suppressed 36/43" line was never read during in-game verification. The test host also masks in the opposite direction — SandBox.dll IS loadable there, so nothing failed where anyone looked. | Lesson appended (`lessons/adapters-taleworlds-api.md`): scan loaded assemblies by simple name, never partial-name `Type.GetType` for module types; degradation logs that mean "feature partially OFF" are `LogError` and MUST be read during in-game verification; `…RealSandBoxAssembly_ResolvesAll7` canary test pins the 7 names against the installed DLL. |
| 1 | MED | Sweep cancelled issues mid-**alternative solution** / lord solution (`IssueQuest == null` for those too) — recalls a player's committed companion+troops | Load-path state mutation | Equated `IssueQuest != null` with "player commitment exists". `IssueState` has three in-progress solving states; only one was checked. This is exactly the **Entity State Matrix** rule (`csharp-architecture.md`, from review #23's `EnsureCompanionsPlaced`) — the rule exists, it wasn't applied to `IssueBase`. Repeat of the category. | Fixed: skip `IssueQuest != null \|\| IsSolvingWithAlternative \|\| IsSolvingWithLordSolution`. No new rule — scope reminder: the OnGameLoaded state-matrix discipline applies to ANY engine entity you mutate on load, issues included. Caught by the API-compat agent reading `CompleteIssueWithCancel`'s branches. |
| 2 | LOW | `SuppressAll`'s `removed` counter counted "Invoke didn't throw", but `RemoveBehaviors<T>` is a silent no-op for unregistered types — the log could claim removals that never happened (future engine-bump silence) | Log-claim vs verified state | Assumed API-call success == state change. `evidence-over-claims` applied to runtime logs: a log that claims an outcome must observe the outcome. | Fixed: count actual `CampaignBehaviors.Count` shrinkage per type; warn when a resolved type wasn't registered. |
| 3 | LOW | Resolver null-arg guards (`assemblySimpleName`/`typeFullNames`/`loadedAssemblies`) untested | Test coverage | Guards written after the tests, defensively. `tests.md` skip-guard-exhaustion rule not re-applied after adding guards. | Fixed: 3 dedicated tests. Habit: adding a guard clause re-triggers the one-test-per-guard rule. |
| 4 | LOW (reported MED) | `issue.GetType()` called 3× per swept issue; `SandBoxIssueTypeFullNames` re-allocated per access | Micro-efficiency | Once-per-load / once-per-process paths; written for clarity first. | Fixed (hoisted local; cached list) — cheap, no complexity cost. |

**Disputed (no action):** the efficiency agent's HIGH on `new LotrIssueGiverAdapter(hero)` per
`OnCheckForIssue`. Verified this turn: issue polling fires from `DailyTickClanEvent` /
`DailyTickSettlementEvent` (`IssuesCampaignBehavior.cs:50-56`) — daily cadence, not per-frame; one
small allocation per notable per day is noise, and the code is pre-existing (outside this changeset,
edit-scope discipline). Recorded here per the no-silent-deferral rule.

## Root-cause pattern

Both finding 0 and finding 1 are **"the safe-looking degradation hid the failure"**: `Type.GetType`
degraded to a shorter list instead of failing loudly, and `IssueQuest == null` degraded to
"sweepable" instead of enumerating states. When a fallback path exists, the review question is
"what does the SYSTEM look like when the fallback silently engages, and who notices?"

## Why each agent missed / caught what

- **Standards (Agent 1):** nothing in scope for it — correctly passed.
- **API-compat (Agent 2):** caught finding 1 by reading the engine method's branches instead of
  trusting the guard's comment — the model behavior for verify-the-consumer reviews.
- **Efficiency (Agent 3):** findings 4 (real, trivial) + the disputed HIGH — it did not check the
  event's cadence before assigning severity. Severity claims about "hot paths" need the tick source.
- **Completeness (Agent 4):** caught finding 3.
- **Data-flow (Agent 5):** caught finding 2; affirmatively cleared the highest-risk flows
  (cache-latch timing, OnGameLoadedEvent gating = save-loads only, classifier coverage 43/43,
  collect-then-cancel necessity, dead-owner impossibility).

## Feedback memories to codify

None beyond the lessons entry already appended for finding 0 — findings 1–3 are scope-applications
of existing rules (entity state matrix, evidence-over-claims, skip-guard exhaustion), called out
above rather than duplicated as new rules.

## Verification owed (in-game, Mike)

1. Pre-fix repro (optional, confirms root cause in the field): current shipped build, new campaign →
   `Logs/taom_debug_*.log` should read `suppressed 36/36 … (intended 43)`.
2. Post-fix: new campaign → `suppressed 43/43`; several in-game days of notable visits offer only
   `taom_lotr_*` issues.
3. Sweep: load any pre-fix save → `swept lingering vanilla issue` log lines; the reporting player's
   `UnstoppablePlay` autosave no longer offers the daughter quest at Varlek.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
