# RCA: plan 003, the BattleBalanceSettingsProvider cache (2026-09-24)

**Summary.** The port of `6eb5955c` (commit `7feca96b`) was correct at runtime: six lenses and Codex
found no defect a player would see today. They did find a latent start-up trap the change introduced
(a constructor read of `TaomSettings.Instance` that pins the compiled defaults for the session if the
provider is ever resolved before MCM initialises), a test suite that could not see the change's
central promise (live MCM edits reach the getters), and docs the change made stale. All three are
fixed on `improve/003-battlebalance-settings-cache`; the GitHub issue is owed to Mike. Report:
`docs/reviews/deep-review-003-hot-path-resolve-and-grid-caching-2026-09-24.md`.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW (latent) | `BattleBalanceSettingsProvider()` read `TaomSettings.Instance` once in its constructor. MCM sets `BaseSettingsProvider.Instance` only in `MCMSubModule.OnBeforeInitialModuleScreenSetAsRoot`, so a resolve during `OnSubModuleLoad` (the `IoC.Configure` eager block or a patch hook `Initialize`) caches null, and all twelve Battle Balance sliders silently stay at their defaults for the session. Safe today only because the one resolve is `SubModule.cs:1144` under `OnGameStart`. | Stale state / lifecycle | The June commit predates the `NameplateRelationSettingsProvider` fix (2026-09-13) that documented this exact trap, and it copied `NameplateFadeSettingsProvider`, the older constructor-read exemplar. The port was a faithful cherry-pick under decision 17, and the builder verified the current wiring by reading it, then wrote the precondition into the CHANGELOG instead of the code. The trap was known only as a class comment in one provider, never as a lesson or rule, so no checklist asked "who resolves this, and when?" | Lazy accessor applied (`_settings ??= TaomSettings.Instance`), IL rule now fails any constructor read. Lesson appended to `docs/reviews/lessons/state-lifecycle-save.md`. |
| 2 | MEDIUM (test gap) | The seven tests ran only with `Instance` null. A constructor that snapshots the twelve values into fields (the regression plan 003 names), a getter wired to the wrong setting, or a constructor that reads `Instance` and discards it, all passed. | Test gap | The IL rule proved WHERE the lookup happens and read as full coverage; the fallback pins proved the null path. Neither ever put a non-null settings object behind the provider, because MCM is not initialised under MSTest and no seam existed. | Internal test constructor plus `Getters_ReadThroughTheSettings_SoLiveMcmEditsApply` (edits all twelve after construction, distinct values; proven by a Tier8-reads-Tier9 mutation). Lesson appended to `docs/reviews/lessons/testing-qa.md`. |
| 3 | LOW | Six of twelve fallbacks unpinned. | Test gap | The June tests pinned the six values the author was tuning. | `EveryFallback_EqualsTheMcmCompiledDefault` (the #559 shape). One-off. |
| 4 | LOW | `docs/features/battle-balance.md:29,103` still said the provider "proxies to `TaomSettings.Instance` per access"; `docs/modding/file-catalogue.md:269` anchored the old line range. | Doc drift on files outside the diff | A cherry-pick carries only the files the June commit touched; neither doc was re-read. The existing lesson (`build-tooling-workflow.md`, "fix the hits in living documentation") covers renames, not a mechanism change. | Fixed. Existing lesson applies ("re-read every file that states the contract", `rca-settlement-nameplate-relation-2026-09-13.md` row 8); no new rule. |
| 5 | LOW | Hot-path comment and CHANGELOG said "per troop per simulation round"; the engine also calls `GetDefaultTroopPower` twice per XP-scored hit in live battles, per casualty and per roster row in every strength sum. Also "leak" for a repeated cost. | Inaccurate comment | The plan's harvest described the simulation path only, and the port copied the June comment. | Comment and CHANGELOG corrected. One-off. |
| 6 | LOW | CHANGELOG "checked in code" (nothing enforced it), "that branch's warg half" (impl-003 has four commits). | Imprecise prose | Written from the plan's summary, not the branch's log. | Corrected. One-off. |
| 7 | MEDIUM (process) | No GitHub issue for plan 003. | Process | Decision 17 authorised the ports without saying whether each needs an issue, and `/issue` is public, so the executor could not file one. | NEEDS MIKE: one issue per port or an umbrella issue. |

## Root-cause pattern

Rows 1 and 2 share one cause: **a caching refactor of an MCM provider was tested and reasoned about
only on the null path**, because MSTest never initialises MCM. The builder, the June author and the
tests all saw `Instance == null` and never a live object, so both the "when is it first non-null"
question (row 1) and the "does a later edit reach the getter" question (row 2) went unasked.

## Why each agent missed these

The lenses did not miss them: rows 1 to 6 were raised by the review. The question is why the build
missed them.

- **Builder (the port):** decision 17 framed the task as "cherry-pick `6eb5955c`", so the builder
  verified the existing commit instead of re-designing it. The reconcile doc had recommended the
  lazy form as optional ("consider amending"), and it was not taken.
- **The June author:** built before `NameplateRelationSettingsProvider` documented the trap, and
  copied the one exemplar that caches in the constructor.
- **Agent 2 (Engine)** raised row 1 but recommended keeping the constructor form, reasoning that the
  lazy form repeats the MCM miss warning per read if `TaomSettings` failed to register. That is the
  pre-change behaviour, and the same for every other TAOM MCM getter, so the lazy form costs nothing
  new.
- **Codex** raised row 2 and rated row 1 an "UNVERIFIED lifecycle contingency" because no current
  path produces the null. It missed rows 4 and 5 (files outside the diff, and call frequency).

## Repeat offender?

Row 1's trap is documented once, as a class comment in
`Main/Features/SettlementNameplateRelation/NameplateRelationSettingsProvider.cs:14-17`, and in
`rca-settlement-nameplate-relation-2026-09-13.md` (MCM facts). It had no lesson entry and no rule,
which is why a port of older code reintroduced it. This is its second appearance, so it now gets a
lesson.

## Feedback memories to codify

None beyond the two lessons. A rule line for `.claude/rules/csharp-architecture.md` ("an MCM settings
provider caches `TaomSettings.Instance` lazily, never in the constructor") is proposed in the deep
review report for the orchestrator to consolidate.
