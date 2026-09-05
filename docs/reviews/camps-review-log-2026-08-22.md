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

## Round B + Codex round 2 (landed 2026-08-23, overnight autonomous run)

Round B: 6 fresh no-narrative reviewers (the 5 round-A dimensions + a late-diff dimension over
the fix range 7f4cf559..HEAD), 3-lens adversarial verification, completeness critic. First pass
lost 28 verify agents + the critic to the 5h usage limit; resumed after reset with full cache
replay, 97/97 agents, 0 errors. Result: 30 raised, **28 confirmed + 3 critic findings, 2 refuted
with recorded reasons** (raw: docs/reviews/raw/roundB-final-result.json, gitignored). One HIGH
(clan-screen wage control live but non-functional), one critic HIGH (both prefabs style body text
with Popup.Text.Medium/.Small, which a Brushes-directory sweep of the entire install proves exist
NOWHERE; pre-existing BattleActionBar.xml shares the defect and is deliberately out of scope).

Codex round 2 (dispatched against 16a58b51, overlapping the closing usage window since it bills
externally): **4 P1 / 8 P2 / 3 P3** plus a round-1 re-verification table (4 FIXED, the rest
PARTIALLY FIXED or follow-ups; raw: docs/reviews/raw/codex-camps-round2-report.txt). The headline
P1: the batch-1 contributor-ordering fix REGRESSED DIFFERENTLY into a DryIoc startup cycle
(CampService materializes the contributor collection; Refuge's contributor needs IRefugeService;
RefugeService needs ICampService), invisible to the suite because nothing resolved the finished
container: the source-scan gates checked registration STYLE. Proven RED with a new
CampsContainerWiringTests (DryIoc Validate, the EnlistmentContainerWiringTests shape:
Error.RecursiveDependencyDetected on the exact predicted path), fixed GREEN by lazy contributor
injection (IEnumerable<Lazy<ICampOverlayContributor>>).

Orchestrator verification calls on contested claims: Codex P2#5 CONFIRMED against Hero.cs
(heroes register only with CampaignObjectManager; the 7 GetObject<Hero> sites miss runtime/loaded
heroes); Codex P3#15 REFUTED by raw bytes (added language rows end 0d 0a, no trailing spaces;
git diff --check flags the bare-CR convention itself); round B's charge-vs-quote and
ResolveParty-linear-scan findings refuted by the verify lenses.

A round-B dataflow finding exposed a prefix collision: taom_fc_ already belonged to
FieldCommission (10 keys re-registered as duplicates, one double-escaped and able to shadow the
correct row), plus 2 dead keys. Since the camps keys are still untranslated English fallbacks
(#508), FieldCamp's 58 keys are being renamed to taom_fcamp_ at zero translation cost; the
round-trip gate is hardened (registration XMLs excluded from the code-default scan, numeric
character references unescaped) and the regeneration script purges every taom_fc_ row from
taom_module_strings.xml. The language-file rebuild had also resurrected the stale '(E)' hotkey
suffix on taom_extra_fast_forward_hint from the translation cache in all 12 languages (trunk
removed it when the key became rebindable); the 12 cache rows are corrected, one line per file.

Fix batch 2 fixers are running (workflow wf_cbd92536-72e, same disjoint 3-fixer shape) against
the two raw finding files, with orchestrator-verified engine facts pinned in the briefs
(KillCharacterAction disband branch, DisbandPartyAction.CancelDisband, ExplainedNumber.Add
mutating BaseNumber, the verified-existing brush list). Orchestrator has already landed: the IoC
cycle fix + container gate, Patch73's registry section + the Patch74 Initialize-location
correction + the category count (79), the roadmap stable anchor, the cache fix, and the gate
hardening.

Backlog: the 12-language translation for the camps keys is issue #508 (user decision 2026-08-22;
needs ANTHROPIC_API_KEY; run on or after the branch merge, now with the taom_fcamp_ prefixes).

## Fix batch 2 (LANDED 2026-08-23 morning)

Workflow wf_cbd92536-72e, 3 disjoint fixers, 0 errors, ~47 min. Everything above (round B 28+3,
Codex 4P1/8P2/3P3) fixed or recorded-deferred; **suite 7469 passed / 2 skipped / 0 failed** on
the first post-integration run, validate_moduledata PASS, lint clean. Orchestrator integration:
strings regenerated (70 stale taom_fc_ rows purged, 63 current keys registered, parity verified
in both directions), languages rebuilt, the Refuge MCM hint updated to the shipped toggle
behavior, one comment reworded so the retired taom_rf_promote_entry key stops tripping the scan.

Recorded-deferred with reasons (fixer reports carry the detail): supply-order PRESERVATION on
refuge founding (needs a cross-feature seam on ISupplyOrderService; explicit-forfeiture warning
shipped instead, seam recorded in refuge.md); the wage-figure language-change residual
(cosmetic, needs a TaomPartyWageModel branch); BattleActionBar.xml's pre-existing phantom
brushes; ADR-007 conversion; external-battle auto-resolve militia.

Ordering caveat worth keeping: the warden-death disband-cancel relies on vanilla
DisbandPartyCampaignBehavior's OnPartyDisbandStarted listener running before TAOM's on the same
dispatch (vanilla behaviors register first; its handler only queues a 1-day-later flip, and
CancelDisband dequeues). The OnGameLoaded belt covers saves written inside the wait window.

In-game smoke additions the fixers recommended (for the #505/#507 checklists): a lord-sourced
order end to end (it could never work before the Find<Hero> fix); the destroyed-caravan loss
message after an AI battle; the faction banner on the caravan; the restyled order screen; kill a
lone warden while garrison survives (expect the alert, no disband march, dismantle-only refuge);
toggle Refuges off mid-build and wait out the build; a building row in the clan screen with a
wage line of 0.

## Round C (targeted, LANDED 2026-08-23): the fix-batch-regression hunt earned itself again

Scope: the batch-2 diff only (16a58b51..HEAD), 3 finders (regression, engine, consistency) +
3-lens verify, 27 agents. **8 raised, 8 confirmed, 0 refuted.** The two HIGHs converged from two
independent dimensions on the same defect: the warden-death disband-cancel relied on a listener
ordering that is BACKWARDS (MbEvent dispatches LIFO: AddNonSerializedListener head-inserts,
verified MbEvent`1.cs:28; TAOM registers after vanilla, so our cancel ran against a not-yet-
populated queue and the refuge still disbanded a day later in a continuous session, self-healing
only across save/load). Fix: an idempotent hourly re-cancel pass for every booked refuge
(state-protecting, runs with raids and the toggle off; vanilla's 1-day wait guarantees the race),
plus corrected comments and a pinning test. Also confirmed and fixed: the clan-screen wage
suppression did not survive the engine's UpdateProperties (re-applied on every postfix pass for
existing rows); Frame1.Broken was ITSELF a phantom root brush on both prefabs (only .Left/.Right
exist; my earlier substring grep vouched for it, the round-C exact-match check caught it; swapped
to the verified Popup.Frame); RepairLoadedRow now also resets a future BuildStartTime (the
mirror absorbing state); the RefugeDamageReduction docstring gained its unclamped-input
precondition with a pre-clamped pin test; two stale owed-items passages and one lessons count
corrected.

Suite after round C: **7472 passed / 2 skipped / 0 failed.** A test-harness detour worth its own
lesson (testing-qa): every CampaignTime factory returns default in tests because the tick
statics only initialize with a live campaign; CampaignTime.Never is the only nonzero
constructible value.

## Field test (2026-08-23 to 2026-08-25): what three review rounds could not see

The first real play sessions after the trunk merge surfaced four things no reviewer could,
because none of them modeled a player's first-run flow or the shipped module folder:

1. **ButterLib/MCM dead at startup** (08859a85): TAOM.Dependencies shipped System.Memory 4.0.1.1
   beside Unsafe 6.0.0.0; the exact 4.0.4.1 bind failed in the System.Memory cctor on ButterLib's
   first Trace.WriteLine, every tick, since the DR3 migration. Mod Options hung at open and
   NRE'd at close. Pin moved to package 4.5.3; DependenciesPairingTests gates both vendored
   variants and every build output. Verified in-game.
2. **Establishing a camp "froze" the game** (4ed42d04): establish switched into a standard
   sub-menu (source parity), which stops time; MapState persisted the open menu so loads came
   back frozen. Three static hypotheses failed; `taom.time_status` (new) showed MenuContext
   taom_fc_camp / AtMenu true with everything else clean. Establish now exits to the map;
   `taom.rescue_time` recovered the save in place. Verified 2026-08-25: first camp raised.
3. **Forage toggle re-entered the same trap** (this commit): the sub-menu is now a WAIT menu
   (time runs with the panel open, status ticks live); forage refreshes in place; break camp
   exits to the map. In-game verification owed.
4. **Six raw `{=taom_sl_*}` tokens on the Supply Order buttons** (this commit): literal prefab
   text is not localized by Gauntlet; labels moved to VM properties; a module-wide prefab sweep
   test guards the class. In-game verification owed.

Open from the field: #509, a ~10-minute save-load stall between AllBehaviorDataLoaded and
session launch (one occurrence, busy thread not captured).

## Remaining queue

1. DONE: round A verified -> merged with the Codex batch -> fixed -> suite 7421 green ->
   committed -> pushed.
2. DONE: round B (28 confirmed + 3 critic, 2 refuted).
3. DONE: Codex round 2 (4 P1 / 8 P2 / 3 P3; startup-cycle P1 fixed RED->GREEN same night).
4. DONE: RCA + lessons + rules + CHANGELOG. Still owed: final status report after round B/Codex 2.
5. USER: in-game smoke checklists on #505/#506/#507; decide the trunk merge. (Translation
   moved to backlog issue #508, user decision 2026-08-22.)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
