# Review log: feat/yotthani-camps (SupplyLines, FieldCamp, Refuge)

Working record for the three-feature port (#505/#506/#507), kept current so any session can resume
without re-deriving state. Companion docs: the three feature docs, the provenance register entry,
and `docs/reviews/codex-camps-round1-2026-08-22.prompt.md`.

## State at last update (2026-08-22, late)

| Item | State |
|---|---|
| Branch | `feat/yotthani-camps`, worktree `E:/repos/taom-camps`, pushed to origin |
| Commits | `30d46e60` provenance/roadmap, `2244fed5` SupplyLines, `60d5da8b` SL docs, `24cce287` FieldCamp, `03bcd465` Refuge |
| Suite | 7323 passed / 0 failed / 2 skipped at `03bcd465` |
| Issues | #505 / #506 / #507 open, close on in-game smoke |
| Deep review round A | 5 unbiased finders + 3-lens verify + critic; first run died on the weekly usage limit, RESUMED (run id `wf_ed88c686-725`) |
| Codex review 1 | DONE: 4 P1 / 14 P2 / 2 P3, VERDICT ISSUES FOUND. Raw: `docs/reviews/raw/codex-camps-round1-report.txt` (extracted from the 3.8MB session log, both gitignored) |
| Fix batch | Written into the plan file; STARTS after round A lands so both reviews merge into one batch |
| Translation run | FAILED: `ANTHROPIC_API_KEY` not set (environment; user action). English fallbacks registered in all 12 language files, suite green regardless |
| Trunk merge | Deliberately NOT done; awaiting user |

## Codex round 1, condensed

**P1** (all verified against source before acceptance):
1. Cross-campaign singleton leak: the three campaign books (orders/camps/refuges) live in
   process-lifetime singletons and only load through `SyncData` when a save record exists, so a
   NEW campaign after quit-to-menu inherits the previous campaign's state.
2. Caravan tracker cache survives loads and can bind a loaded save's orders (ids collide from
   `taom_so_0`) to the PREVIOUS campaign's party objects.
3. Dismantle copies hero/prisoner roster rows with plain `AddToCounts`, corrupting hero ownership
   bindings; promoted-warden release path compounds it.
4. Null persisted records (hand-corrupted saves) crash `OnGameLoaded`/`FrameTick` in all three
   features.

**P2** (14): contributor-ordering bug (FieldCamp's eager resolve materializes the overlay
contributor list BEFORE Refuge registers its contributor: an integration-side bug, not a builder
bug); timeout delivery grants the ORDER's cargo even when the caravan was raided down (deliver
from live rosters instead); caravan component has null `Leader` so clicking a caravan opens
conversation with an arbitrary troop; lord-source routes re-read the lord's CURRENT position
instead of the dispatch origin; ambush-scan clock not reset across loads; disabling Field Camps
freezes guards while leaving a standing lookout's sight bonus active; refuge stash food is eaten
as party provisions (`DoesPartyConsumeFood`); militia rallies after auto-resolve counts freeze;
militia stand-down can delete player-garrisoned survivors of the same troop stack; a refuge stays
manageable/dismantlable during its own MapEvent; a defeated refuge leaves a stale book row until
reload; `RefugePartyComponent` skips the `OnChangePartyLeader` contract; peace does not release
hero prisoners stored in a refuge; founding is not transactional (warden can be promoted, then a
spawn refusal orphans the promotion).

**P3** (2): ADR-007 adapter bypass across the boundary services (repo precedent exists; document);
nameplate patch is a 180-line entry point (extract helper).

**Clean probes**: Harmony targets/signatures on installed 1.4.8; menus incl. the reserved index 4;
save definer ids and container shapes; localization ids + placeholders across all 12 languages;
all four tpacs (deploy path, mesh-name match, guarded fallbacks); both combat-model integrations
and the visibility contributor chain; vanilla finance/desertion/army machinery vs the pinned
parties; pricing/rollback/charging flows; **no source mechanic silently lost**; no re-entrancy
defect beyond the MapEvent item above.

## Process notes worth keeping

- Reviewer prompts carried NO port narrative: commit messages, CHANGELOG and feature docs were
  explicitly framed as hypotheses to check. Codex was steered by QUESTIONS (10 probe areas), not
  by claimed answers.
- Session/weekly usage limits killed one builder wave and one review wave mid-run; both resumed
  cleanly via `Workflow resumeFromRunId` after reset. Builders that died had written nothing
  (verified by file inventory before resuming).
- The port pipeline per phase: pinned contracts first, disjoint builders (no builds allowed),
  orchestrator integrates/builds/tests. Compile errors at integration across all three phases:
  five (two ctor-arity, one missing using, one ambiguous TestContext, one my own wiring guess).

## Round A results (landed)

41 raised, **37 confirmed + 5 critic** after 3-lens adversarial verification (129 agents; full
JSON: `docs/reviews/raw/roundA-result.json`, gitignored). Highlights beyond the Codex overlap:
3 CRITICAL (dismantle strands heroes via raw roster copy+clear nulling `PartyBelongedTo`; the
cross-campaign singleton-book leak, independently re-found; camp books saved INTO the wrong
campaign), delivery-vs-MapEvent gate (`IsRaid` never fires for field battles, so the Lose path was
dead code), break-camp cancelling town orders, the contributor-collection materialization bug, 17
localization rows truncated at their first placeholder by the orchestrator's own registration
script, the ambush battle-start dropped on a WRONG orchestrator brief (source truth re-verified:
inquiry + StartBattleAction), and terrain tables silently rebalanced under a false "drifted enum"
justification. Two findings therefore indict orchestrator briefs, not builder work: exactly the
bias class the unbiased-review design existed to catch.

## Fix batch 1 (LANDED 2026-08-22 evening)

Orchestrator-owned fixes DONE: localization rows regenerated with placeholders intact + languages
rebuilt; eager patch-inits moved to a single post-registration block (`IoC.InitializePatchStatics`);
new seams pinned (`ResetForNewSession` x3, `CancelCampOrders` + `SupplyOrder.PlacedFromCamp`,
`Patch73_SupplyLines` category). Three per-feature fixers running (workflow `wf_ac6a0e94-9b9`)
against the raw finding files, incl. the shared reset pattern, the dismantle hero-move rework, the
delivery redesign, the source-faithful ambush battle restore and terrain re-derivation.

### Fixer results (workflow wf_ac6a0e94-9b9, 3 agents, 0 errors, ~33 min)

All three fixers returned full result sets (raw: the workflow output file; summaries folded into
the CHANGELOG fix entry). Cross-feature seams the fixers correctly left at the boundary, closed
by the orchestrator at integration:

- Camp menu now calls `SupplyOrderScreens.Open(fromCamp: true)` (the flag existed on both sides
  but no caller passed it; camp-scoped cancellation was inert without this).
- `IRefugeVisualService.TickWind()` added and driven from `RefugeService.FrameTick` BEFORE the
  game-time throttle: a refuge standing alone had no steady-state wind driver (the camp-side
  driver only runs while a player camp stands). `CampLayoutBuilder.TickWind` re-applies a
  constant forced wind, so double-driving is idempotent.
- 9 new localization keys regenerated into the strings XML + 12 language files (English
  fallback), key sets verified equal in both directions.
- `SupplyCaravanEncounterPatch` classified ReviewedSafe in `CoopVetoClassificationTests` (the
  gate caught it: one suite failure, honest local-UI-redirect rationale mirrors the refuge
  sibling's entry).

**Suite: 7421 passed / 2 skipped / 0 failed** (was 7323 pre-batch; +98 tests). Both new
preventive gates ran green after demonstrating they can fail. RCA preventive-artifacts checklist:
all six boxes ticked (`rca-yotthani-camps-2026-08-23.md`).

Deliberately not fixed (recorded by the fixers with reasons): camp visuals at 0% raise (source
parity, pinned by the brief); the ADR-007 boundary-sliver precedent (P3, cross-feature
architecture change, deferred); orphan refuges still count toward the cap until dismantled
(chosen exit design); auto-resolve militia for battles not started by the raid path (unreachable
without patching MapEvent construction; documented limitation); warden succession picker
(documented limitation); Refuge BuildingMesh/BuildingScale stay provider-pinned source defaults
with no MCM rows (TaomSettings single-owner; wiring later needs no consumer change).

## Remaining queue

1. DONE: round A verified -> merged with the Codex batch -> fixed -> suite 7421 green ->
   committed -> pushed.
2. Deep review round B (fresh agents over the fixed tree).
3. Codex round 2.
4. DONE: RCA + lessons + rules + CHANGELOG. Still owed: final status report after round B/Codex 2.
5. USER: set `ANTHROPIC_API_KEY`, rerun the 12-language translation; in-game smoke checklists on
   #505/#506/#507; decide the trunk merge.
