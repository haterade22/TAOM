# RCA: the yotthani camps port review batch (SupplyLines, FieldCamp, Refuge)

**Scope.** The merged findings of deep-review round A (37 confirmed + 5 critic, adversarially
verified, reviewers given code and diff only) and Codex round 1 (4 P1 / 14 P2 / 2 P3) over the
three-feature port on `feat/yotthani-camps`. Per the review discipline every confirmed finding
gets a why-missed and a preventive action; this RCA groups them by ROOT CAUSE CLASS, because the
goal is that the class never ships again, not that these instances were patched.

**Fix state.** Batch-1 fixers were dispatched from the raw finding files; orchestrator-owned fixes
(localization regeneration, IoC restructure, seam pins) landed first. The fixed/deferred column is
finalized in the review log at integration.

---

## Class 1: per-campaign state in process-lifetime singletons (2 CRITICAL + 6 HIGH/MED)

**Instances.** All three feature books survive into a NEW campaign (SyncData only fires when a save
record exists, so nothing ever resets them, and campaign B can then SAVE campaign A's state); the
caravan tracker cache drives GHOST parties from the previous session after any load; the
ambush-scan clock, move-guard latch and visual shown-maps survive loads and silently disable their
mechanisms.

**Why missed.** The builders followed TAOM's singleton-service precedent (RacePositionStore et
al.), where process lifetime is CORRECT because the state is config, not campaign data. The pinned
contracts specified the SyncData plumbing (LoadFrom/SaveInto) but never asked the no-record
question: what happens when a session starts and SyncData never fires? The Entity State Matrix rule
is written for behaviors mutating heroes on load, not for service lifetime, so no checklist
contained "new campaign after quit-to-menu".

**Prevent.**
1. Rule addition (csharp-architecture.md, sibling of "Config Providers MUST Validate"): a process
   singleton holding per-campaign state MUST have a session-reset story: ResetForNewSession()
   called by its behavior when OnSessionLaunched arrives without a SyncData load, AND every
   transient cache (trackers, clocks, latches, shown-maps) cleared on LoadFrom, because a loaded
   save has NEW engine objects under old ids.
2. Test pattern: each such service gets SessionReset tests pinning both paths (reset-when-no-record,
   transient-clear-on-load). The batch-1 fixers write these for all three features.
3. Review prompt: the lifecycle dimension permanently gains "walk a second campaign in the same
   process, and a load with the same ids but new objects". Round A had it; that is why these were
   caught. Codified so future reviews keep it.

## Class 2: engine-owned lifecycle crossed with raw mutations (1 CRITICAL + 2 HIGH)

**Instances.** Refuge dismantle moved heroes by raw roster copy+clear; the engine's
AddToCountsAtIndex fires OnHeroAdded/OnHeroRemoved, and the CLEAR unconditionally nulls
Hero.PartyBelongedTo AFTER the copy re-parented him, a persisted desync. Supply delivery could
destroy a caravan attached to a live MapEvent (vanilla always detaches before destroying). The
IsRaid loss input could never fire for a field battle, so the Lose path was dead code.

**Why missed.** The builders ported the source's raw-copy dismantle faithfully; nothing in the
briefs or house rules said hero roster rows move only through engine Actions. The MapEvent
ownership contract (detach before destroy) is visible only by decompiling vanilla's own destroy
sites, which nobody had reason to read until a reviewer asked what vanilla does there.

**Prevent.**
1. Lessons entry (Adapters and TaleWorlds API): heroes and prisoners cross parties ONLY through
   engine Actions (AddHeroToPartyAction, captivity transfers); raw TroopRoster copy+clear is for
   regular troops only. A hero row is not data, it is an entity binding with unordered side-channel
   callbacks.
2. Lessons entry: a party attached to a MapEvent is engine-owned. Never destroy, deliver from, or
   teleport it; wait for the event to release it (CaravanExists going false is the honest loss
   signal).
3. Review prompt: the engine dimension permanently asks, for every DestroyPartyAction or roster
   bulk move, what vanilla does at its own equivalent site.

## Class 3: orchestrator brief bias contaminating builders (2 findings, REPEAT OFFENDER)

**Instances.** The Phase 2 brief told the builder the source ambush "broke camp, halved morale, no
battle start"; the source actually shows an inquiry then StartBattleAction.ApplyStartBattle. The
terrain builder's "the source ordinals decode against a drifted enum" justification was reviewed
as false; the tables were silently rebalanced. Both briefs PARAPHRASED load-bearing source
behaviour as fact, and the builders, correctly per their briefs, implemented the paraphrase.

**Why missed.** Same failure shape as the Patch72 "faithful port of dead code" lesson one release
earlier: an authority substituted for verification. The briefs pointed at line ranges AND asserted
what those lines said; builders trusted the assertion over the pointer. Second occurrence of the
fidelity class in two features: a pattern, not an accident.

**Prevent.**
1. Briefing convention change (agent-teams.md, parallel-builder section): briefs may point at
   source lines; they must not paraphrase load-bearing source behaviour. Where a brief needs to
   describe behaviour it phrases a hypothesis: "the orchestrator believes X; verify against lines
   N-M before implementing, and report if wrong."
2. The unbiased-review design (reviewers given code and diff, never the port narrative, plus a
   dedicated source-fidelity dimension holding the decompiled sources) CAUGHT both instances and
   is now the house shape for port reviews. Recorded so the next port does not regress to
   narrative-primed reviewers.

## Class 4: lossy generated registrations with no round-trip check (1 HIGH, self-inflicted)

**Instance.** The orchestrator's localization-registration extraction stopped at the first
placeholder brace, truncating 20 defaults; the truncated text shipped into the strings XML and all
12 language files, silently, because English rendering still worked from the inline default.

**Why missed.** The generation step had no verification step: the count matched ("39 keys
registered"), so it LOOKED complete. Nothing compared registered text against code text.

**Prevent.**
1. Harness test (lands with batch-1 integration): for every {=taom_*} key, the registered row's
   text must EQUAL the longest inline default found in code. A mechanical round-trip gate in the
   localization test family, so a lossy extractor can never ship silently again.
2. Lessons entry (Build/Tooling): a generator's output is unverified until something DIFFS it
   against its input; a matching count is not verification.

## Class 5: eager resolution materializing extension collections (1 HIGH)

**Instance.** FieldCampIoC eagerly resolved ICampService to Initialize a patch static; DryIoc
materialized its IEnumerable of overlay contributors at that moment, so Refuge's contributor,
registered later, never reached it: the "a refuge stands here" camp-block was permanently inert.

**Why missed.** The eager-resolve-with-Initialize idiom is house precedent (HeroRaceIoC) and is
safe for scalar dependencies. Nobody had combined it with a collection injection; the ordering
hazard is invisible at the call site.

**Prevent.**
1. Structural fix landed: ALL eager patch-static initialisation now lives in one post-registration
   block (IoC.InitializePatchStatics) after the last feature registration. New features add their
   Initialize there, never in their own IoC.
2. Harness test (batch-1 integration): a source-scan test asserting no container.Resolve call
   inside any feature Register*Feature method, so the pattern is mechanically enforced.

## Class 6: decision inputs that cannot vary (2 findings)

**Instances.** caravanInRaidEvent could never be true for a field battle (IsRaid is the
settlement-raid battle type only), so the loss branch and its four tests guarded nothing.
AvoidHostileActions was believed to stop AI attacks; on 1.4.8 it gates only narrative text and a
relation penalty.

**Why missed.** Both inputs LOOK meaningful, and each had tests, but the tests fed the input
values directly: they proved the branch logic, never that the input can occur. This is the DTO
"are non-empty values actually produced?" lesson one level down: does this input ever vary on the
real engine?

**Prevent.** Lessons entry (Testing and QA): for every boolean or enum input a decision consumes,
identify the real producer and confirm each consumed value is producible on the installed engine.
A test that injects an unproducible value pins dead code and reads as coverage.

## Class 7: claims in docs and comments ahead of evidence (3 LOW/MED)

**Instances.** field-camp.md promised fortified camps raise in double the hours while fortification
was effectively instant; two "source behaviour" comments attributed port deviations to the source;
the review log's own defect count lagged.

**Why missed / prevent.** evidence-over-claims already governs; the recurrence rode on Class 3
(the comments trusted the briefs). No new rule: Class 3's fix removes the source of false claims,
and the batch-1 fixers align every touched doc and comment with the code as part of each fix.

---

## Batch-2 addendum (round B + Codex round 2, 2026-08-23)

Two updates from the second review wave; instances and fixes are in the review log.

### Class 5, deepened: style gates cannot see the resolved graph

The batch-1 contributor fix REGRESSED DIFFERENTLY (Codex's words): moving the eager resolves into
a post-registration block removed the ordering hazard and created a construction cycle
(CampService materializes its contributor collection; Refuge's contributor needs IRefugeService;
RefugeService needs ICampService). Both batch-1 preventive tests passed while the module could not
start, because both scan SOURCE TEXT for a banned pattern; neither resolves the finished
container. The corrected preventive artifact: CampsContainerWiringTests (DryIoc Validate over the
three-feature graph, the EnlistmentContainerWiringTests shape), proven RED on the cycle before the
fix (lazy contributor injection) turned it GREEN. Lesson: a wiring gate must exercise the wiring;
a text scan only pins the idiom, and an idiom can be satisfied by a broken graph.

### Class 8 (new): generated registrations claimed a prefix another feature owned

The camps localization batch registered 161 keys under taom_sl_/taom_fc_/taom_rf_; taom_fc_ was
ALREADY FieldCommission's registered prefix (taom_enlistment_strings.xml), so the generator swept
10 of FieldCommission's inline defaults into a second registration file (one row double-escaped
and able to shadow the correct one) and nobody noticed for two review rounds because the
round-trip gate let one registration file vouch for another. Fixes: FieldCamp's 58 keys renamed
to taom_fcamp_ (zero cost while the keys are untranslated English fallbacks, #508); the gate now
excludes ALL registration XMLs from its code-default scan and decodes numeric character
references; the regeneration script purges the foreign rows. Prevent (rule of thumb, recorded in
the localization lessons): before a new feature claims a key prefix, grep the registration XMLs
for that prefix; a prefix is an ownership claim, not a naming convention.

## Field-test addendum (2026-08-23 to 2026-08-25)

### Class 9 (new): a faithfully ported UX trap

The source switched into the camp sub-menu after establishing; so did the port. A standard game
menu stops campaign time unconditionally and MapState persists the open menu id, so the first
camp ever established read as a frozen game and the freeze survived save/load. The forage toggle
carried the same shape. Why missed: every review dimension verified code against source and
engine; none walked the FIRST-RUN PLAYER FLOW ("establish, then wait for it"). Source parity
was treated as correctness. Prevent: (1) a port's smoke checklist opens with the first-run flow,
run by a human before the feature is called verified; (2) rule (campaign-mechanics lesson): after
starting a timed process, land the player on the map or in a wait menu, never a standard menu;
(3) `taom.time_status` exists so the next freeze is a one-command diagnosis.

### Class 10 (new): a shipped-module pairing nobody verified

TAOM.Dependencies vendored System.Memory 4.0.1.1 beside Unsafe 6.0.0.0 for three months; the
mismatch killed ButterLib at startup and nobody had opened Mod Options since. Why missed: the
suite exercises source and build output, never the shipped module folder's assembly pairings,
and the csproj pin overwrote a correct vendored file on every deploy. Prevent:
DependenciesPairingTests (vendored variants + build outputs) and the build-tooling lesson on
package-vs-assembly versions.

### Class 11 (new): literal localization tokens in prefabs

Six Supply Order button labels were literal Text="{=key}Label"; Gauntlet renders those raw. Why
missed: the keys were registered, so every localization gate passed; no gate looked at HOW a
prefab consumes a key. Prevent: the module-wide prefab sweep test (no literal {= in any prefab)
and the localization-ui lesson.

## Where each class was caught, honestly

Round A's no-narrative design earned its cost: Classes 3 and 6 are structurally invisible to a
narrative-primed reviewer (they require distrusting the narrative), and Class 1's worst instance
(saving campaign A's books INTO campaign B) came from the lifecycle dimension's second-campaign
walk, which the Patch72-era review shape did not do. Codex independently converged on Classes 1, 2
and 5 from a cold start: the strongest external signal that those three are objective defect
classes rather than reviewer taste.

## Preventive artifacts checklist

- [x] csharp-architecture.md: singleton session-reset rule (Class 1)
- [x] agent-teams.md: no-paraphrase briefing convention (Class 3)
- [x] Lessons entries: state-lifecycle-save (Class 1), adapters/TaleWorlds API (Class 2, two
      entries), build/tooling (Class 4), testing-qa (Class 6), cross-ref from gamemodels-services
      to the Patch72 fidelity lesson (Class 3)
- [x] Localization round-trip test (Class 4) - TAOM.Tests/Infrastructure/Localization/RegisteredDefaultRoundTripTests.cs; red until the lossy registration was regenerated, green after
- [x] IoC no-eager-resolve source-scan test (Class 5) - TAOM.Tests/Infrastructure/IoCRegistrationDisciplineTests.cs, 3-file verified baseline + stale-entry companion
- [x] Per-feature SessionReset tests (Class 1) - SupplyLinesCampaignBehaviorTests, FieldCampBehaviorSessionResetTests, RefugeCampaignBehaviorTests; both reset paths pinned per feature

All six artifacts landed with the batch-1 integration commit; suite 7421 green at that point.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
