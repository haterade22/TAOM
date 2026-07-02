# RCA — Career Quest System deep-review (2026-06-01)

Deep-review of the new career-tied quest feature (Phase 1). 5 core agents (Standards, API-compat, Efficiency, Completeness, Data-flow). Standards PASS, Completeness COMPLETE (issue created at commit). **1 HIGH (save-load cancel), 1 MED (silent skill-id), 1 LOW (aspirational field), 2 inconsistencies, 6 efficiency notes (all daily-tick/load).** All confirmed findings fixed in-session before commit; full suite 2874 pass after fixes.

## Findings

| # | Sev | Bug | Category | Why missed (during impl) | Preventive action |
|---|-----|-----|----------|--------------------------|-------------------|
| 1 | **HIGH** | `CareerQuest : QuestBase` had no `SpecialQuestType` override → `QuestManager.OnGameLoaded` `CompleteQuestWithCancel`s any ongoing quest with no associated `IssueBase` unless `IsSpecialQuest`. Every career quest would silently die on the first save-load. | Missing engine-lifecycle gate (runtime behavior not visible in a type signature) | The 4-cluster 1.4.5 verification confirmed every `QuestBase` *member signature* compiled — but did NOT trace the **manager's load path** (`QuestManager.OnGameLoaded`), which imposes a contract (`IsSpecialQuest` OR an associated issue) that no signature reveals. Verifying "the API exists" ≠ verifying "the engine keeps my object alive." | **FIXED** (`public override string SpecialQuestType => "taom_career_quest";`). New rule (memory `feedback_questbase_subclass_special_or_issue`): when subclassing an engine type with a **manager-driven lifecycle** (`QuestBase`/`IssueBase`/`MissionBehavior`/etc.), decompile the manager's `OnGameLoaded`/`OnSessionLaunched`/cleanup path and verify how it treats your subclass — not just the base type's signatures. Add to the engine-API verification checklist. |
| 2 | MED | A `SkillThreshold` objective whose `param` doesn't resolve to a real `SkillObject` silently returns 0 forever (`QuestHeroAdapter.GetSkillValue` → `GetObject<SkillObject>` null → `0`, no log) → quest can never complete. | Silent fallback at a boundary lookup (sibling of "validate before lookup") | The config provider validates `param` is non-empty, but the id→`SkillObject` resolution happens at the adapter boundary where a neutral 0 hides the failure. `csharp-architecture.md` "Lookup Functions With Fallbacks" covers exactly this — a fallback is for logging-and-survival, not silent acceptance — but the rule wasn't applied to the new threshold-read path. | **FIXED** — `CareerQuest.OnStartQuest` now resolves each `SkillThreshold` id via `MBObjectManager` once and logs a warning if unknown (diagnosable, not per-poll spam). |
| 3 | LOW | `CareerQuestGiverKind` enum + `giver_kind` field parsed, stored, never consumed (no branch on it; `CultureLord` unimplemented). | Aspirational scaffolding | Added a Phase-2 NPC-giver concept up front. Violates the no-aspirational-enum rule (`feedback_no_aspirational_enum_values`) — **repeat pattern** (EquipPresets review #5 flagged `SlotLocked`/`SkippedLockedSlots` the same way). | **FIXED** — removed the enum + field + parse + XML attr + ctor param (added back in Phase 2 with a real consumer). |
| 4 | LOW (inconsistency) | `VisitSettlementType` enum doc said "N distinct settlements"; code counts each entry. | Doc/code mismatch | Wrote the aspirational "distinct" semantics in the doc, implemented the simpler count-each-entry. | **FIXED** — doc aligned to "counts each entry (not distinct)". |
| 5 | — (reviewed) | Efficiency: per-poll adapter alloc, `AnyActiveCareerQuest` daily QM scan, `GetQuestById` linear scan, `SyncData` Split allocs, `HasType`/`IsDeclined` scans. | Tech-debt (all daily-tick / load, none per-frame) | N/A — none are hot-path. | Cheap ones FIXED (cached the hero adapter; `GetQuestById` now a dict). The daily-tick scans are once/day — left as-is (negligible). |

## Findings reviewed and classified as documented-limitation / non-issue (not bugs)

- **Data-flow GAP 3 — mid-play XML objective-count edit soft-locks an in-progress saved quest** (progress slots don't resize on load; guards prevent any crash). This is a mod-authoring edge case, not a player-facing bug. **Documented** as a known limitation in `career-quest-system.md` ("change objectives only between playthroughs").
- **Data-flow INCONSISTENCY 2 — behavior re-offers a quest for a level-gated tier.** Re-examined: a 2-button `InquiryData` is modal and **forces** an accept/decline, after which the active-quest check or the persisted declined-set suppresses further offers — so there is no actual daily spam (the agent assumed an ignorable inquiry). Left as designed (the quest's unique reward is worth offering even post-level); **documented** the offer logic.

## Root-cause theme

The one HIGH and the one MED share a theme: **"the API compiles" is not "the runtime contract is satisfied."** Finding 1 = an engine *manager* lifecycle contract invisible in signatures; Finding 2 = a boundary lookup whose neutral fallback hides a config error. Both are caught only by tracing runtime behavior (manager load path; what an unresolved id does downstream), not by signature verification. The 1.4.5 verification pass (excellent for signatures + namespace drift) is necessary but not sufficient — engine-managed subclasses need a lifecycle-contract check too. That's the new checklist item + the memory.

## Why each deep-review agent's scope behaved as expected

- **Agent 2 (API-compat) CAUGHT finding 1** — it decompiled `QuestManager.OnGameLoaded`, exactly the lifecycle trace the implementation skipped. This validates routing the save/load-critical engine type through the API agent with a "trace the manager, not just the type" brief.
- **Agent 5 (Data-flow) caught findings 2, 3, 4** + the two documented-limitations — its job is precisely "declared-but-not-consumed / parallel-path / save-load" tracing.
- **Standards / Efficiency / Completeness** correctly PASSED — the code is convention-clean (services pure, boundaries use IoC legitimately), not a hot path, fully tested + documented.

## Feedback memory to codify

`feedback_questbase_subclass_special_or_issue` (new): a `QuestBase` subclass with no associated `IssueBase` MUST set `SpecialQuestType` (non-empty) or it is auto-cancelled by `QuestManager.OnGameLoaded` on load. Generalises to: engine types with a manager-driven lifecycle — verify the manager's load/cleanup handling of your subclass.

## Codex adversarial pass (Phase 2, independent)

`/review-codex` dispatched Codex (gpt-5.5, xhigh) on the same changeset. It **independently CONFIRMED the deep-review fixes by decompiling the installed 1.4.5 engine**: (1) the `SpecialQuestType` save-load fix is sufficient — `QuestManager.OnGameLoaded` routes `IsSpecialQuest` quests through `InitializeQuestOnLoadWithQuestManager` (`RegisterEvents` + `InitializeQuestOnGameLoad`), ticks continue via `DailyTickWithQuestManager`, no `IssueBase` needed; (2) the `[SaveableField] List<JournalLog> _logs` shared-graph assumption holds — SaveSystem dedupes by object identity (`_idsOfChildObjects` / `GetObjectWithId`), so `_logs[i]` and base `_journalEntries[i]` rehydrate to the same instance; (3) the 4th persistence dict did NOT reintroduce the Phase-9b mid-save reconstruct bug. It then found **5 new findings** (none HIGH), all fixed in-session:

| # | Sev | Codex finding | Category | Why the deep-review missed it | Preventive action |
|---|-----|---------------|----------|-------------------------------|-------------------|
| F1 | MED | `KillEnemyLords` counted ANY lord the player killed (incl. allies / peacetime executions) — no at-war check. | Logic — missing predicate | Data-flow agent verified the *event wiring* was correct (handler args, player-as-killer) but didn't question the game-semantics of "enemy" — `victim.IsLord` ≠ "enemy lord". | **FIXED** — gate on `victim.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)` (API verified on 1.4.5). |
| F2 | LOW | `_offerPending` could stick `true` if `InformationManager.ShowInquiry` threw before a callback exists. | Missing guard | The data-flow agent traced both callbacks reset the flag, but assumed `ShowInquiry` itself can't throw before wiring the callbacks. | **FIXED** — try/catch around `ShowInquiry` resets `_offerPending` on exception. |
| F3a | MED | `VisitSettlementType` accepted any non-empty `param`; only `Town`/`Castle`/`Village` ever match → a typo'd param silently never progresses. | Missing validation (validate-before-lookup sibling) | The provider validated `param` non-empty but not against the enum-of-valid-values the runtime emits. Same class as RCA finding 2 (skill-id), one level deeper. | **FIXED** — provider rejects a `VisitSettlementType` whose param ∉ {Town,Castle,Village} + test. |
| F3b | LOW | `UnlockTier` validated range 1-3 but not `== quest.Tier` → a quest could unlock a *different* tier than it gates. | Missing cross-field validation | The provider validated each reward in isolation; the (reward.Amount, quest.Tier) relationship wasn't checked. | **FIXED** — provider coerces `UnlockTier.amount` to the quest's tier with a warning + test. |
| F3c | LOW | `GrantItem` with an invalid item id silently no-ops (adapter returns on null `GetObject<ItemObject>`). | Silent fallback (same class as the skill-id finding) | Same root as RCA finding 2 — a boundary lookup returns a neutral result and hides a config error. | **FIXED** — `CareerQuest.OnStartQuest` warns on an unresolvable `GrantItem` id (alongside the skill-id check). |

**Codex value-add theme:** the deep-review verified *plumbing* (events wired, args correct, signatures match); Codex caught *game-semantics + config-completeness* gaps the plumbing-correct code still has — "the right event fires" ≠ "it should count this kill", and "param is non-empty" ≠ "param is a value the runtime can match". F3a/F3c are the same boundary-lookup-hides-config-error class as the deep-review's skill-id finding, recurring at three sites — reinforces the "validate-before-lookup at the boundary, log on miss" rule (`csharp-architecture.md`).

## Codex self-review pass (Phase 3, on the fix diff)

`/review-codex` dispatched a focused Codex pass over the 5 Phase-2 fixes. Verdicts: **F2/F3a/F3b/F3c CONFIRMED CORRECT** (it re-decompiled `SettlementTypeName` vs the new validator allow-list, the inquiry-throw reset, the tier-coercion contract, and `MBObjectManager.GetObject<T>` null-return). It raised **one** new finding on F1:

| # | Sev | Codex finding | Resolution |
|---|-----|---------------|------------|
| P3-1 | MED | The F1 at-war gate sits on `HeroKilledEvent`, which fires *after* `KillCharacterAction.ApplyInternal` may run `DestroyKingdomAction`/`DestroyClanAction`. `IFaction.IsAtWarWith` returns false once a faction `IsEliminated`, so an enemy-lord kill that *eliminates* the victim's faction would be missed (false-negative). | **SUPERSEDED by the `DefeatEnemyLords` rename (below).** Capture fires at battle end, before any faction-elimination path — no elimination-timing false-negative. |

## Post-review correction — `KillEnemyLords` → `DefeatEnemyLords` (user-driven game-semantics fix)

The user flagged the objective's *semantics*: in Bannerlord, **killing a lord = executing them** (a deliberate, honor/relation-penalty act), and `HeroKilledEvent` essentially only fires on executions (or rare death-in-battle). A "prove yourself" career objective should reward **defeating** a lord — winning the battle and capturing them — not executing prisoners.

- **Enum** `KillEnemyLords` → `DefeatEnemyLords`.
- **Event** `HeroKilledEvent` (`OnHeroKilled`) → `HeroPrisonerTaken` (`OnHeroPrisonerTaken`), verified 1.4.5: `IMbEvent<PartyBase, Hero>`. Gate: `capturer == PartyBase.MainParty && prisoner.IsLord && prisoner.MapFaction.IsAtWarWith(player)` (the F1 at-war guard carries over).
- **Why this also resolves P3-1:** capturing a lord does not eliminate their faction (they're alive, imprisoned, still a clan/kingdom member — that's why ransom works). At `HeroPrisonerTaken` time `prisoner.MapFaction` is intact and still at war, so the at-war check has no elimination-timing false-negative. The rename fixes the game-semantics issue *and* sidesteps the Codex P3-1 timing concern in one change.
- The proof-of-life Gondor quest never used this objective (WinBattles + SkillThreshold + RenownThreshold), so no live-data change; framework-only.

## In-game crash — `SaveableTypeDefiner` SaveId collision (hard crash at `Module.Initialize`)

First live launch after the feature crashed at module init:

```
System.ArgumentException: An item with the same key has already been added.
  at SaveableTypeDefiner.AddClassDefinition(Type, Int32 saveId, ...)
  at CareerQuestSaveableTypeDefiner.DefineClassTypes()  line 20
  at SaveManager.InitializeGlobalDefinitionContext() → Module.Initialize()
```

**Root cause.** The engine global type id is `_saveBaseId + saveId` (decompiled `SaveableTypeDefiner.AddClassDefinition`, 1.4.5). TAOM's definer bases step by **100** (EquipPresets 726900501, FormationPreset 726900601, CareerQuest 726900701), and the sibling definers register their classes at localId **101+** so the id lands in the base+100 block. `CareerQuestSaveableTypeDefiner` used localId **1** → `726900701 + 1 = 726900702`, which **equals** FormationPreset's `726900601 + 101 = 726900702`. Duplicate dictionary key → crash before any save is even touched.

| Category | Why missed | Preventive action |
|----------|-----------|-------------------|
| Save-id range collision (engine type-id math) | The deep-review/Codex passes focused on save *correctness* (field graph, special-quest lifecycle), not the *id arithmetic* across definers. The "TAOM-unique, next in the 7269007xx series" comment looked sufficient but the base-step (100) < localId (101) interaction wasn't checked. This is `Module.Initialize`-time, not save-time, so no unit test exercises it. | **FIXED** — localId `1` → `101` → `726900802` (clear of `726900602/603/702`). Added a `<remarks>` block in the definer documenting the `base + localId` formula + the "localId starts at 101" convention. New memory `feedback_saveable_typedefiner_localid_offset`. |

## Verdict

READY. 1 HIGH (deep-review) + 5 Codex Phase-2 findings + 1 Codex Phase-3 finding (superseded by the rename) + 1 in-game `Module.Initialize` crash — all fixed in-session. The `DefeatEnemyLords` rename corrected the objective's game-semantics (capture, not execute) and the SaveId collision was the only launch-blocker. Final: build 0 err (deploy OK with game closed), suite **2877 pass / 0 fail / 2 skipped**. In-game `Module.Initialize` now clears the save-definer context (user to confirm the relaunch).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
