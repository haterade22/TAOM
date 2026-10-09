# RCA: War of the Ring Phase 2 never declared its wars, #772 (2026-10-08)

**Summary.** Milestone M0b of the War Chronicle work (#765) fixes #772: at Phase 2 (Full War) no
Hostile-pair war was ever declared. `TransitionToPhase` set `CurrentPhase = FullWar` first, every
declaration was guarded by `IAllianceAdapter.AreAtWar`, and `AreAtWar` goes through
`TaomDiplomacyModel.IsAtConstantWar`, which answers "already at war" for every Hostile pair once the
phase is Full War. The real stances stayed neutral and no `OnWarDeclared` fired. The Codex review of the
#764 change had confirmed it as S6. The fix guards every declaration with `HasDeclaredWar` (the stored
stance link), turns the phase first and then declares, counts a war only when the stance confirms it,
repairs saves already at Full War with `ReconcileDeclaredWars` at host session launch, and drops null,
blank and self pairs in config validation. An eight-lens `/deep-review` (two adversarial verifiers per
finding) of the first cut found 2 MEDIUM, 18 LOW and 2 INFO defects, all applied, plus 1 LOW, 1 UNVERIFIED
and 3 INFO follow-ups. Codex (gpt-6-astra, xhigh) then found 2 MEDIUM and 1 LOW: one fixed with a test, one fixed
with a warning, one confirmed in mechanism and left as documented behaviour. Nothing had been committed
before the review.

## Findings (deep review, applied)

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F01 | MEDIUM | The first cut moved `CurrentPhase = newPhase` after the declarations. While the phase was still IsengardWar, vanilla's `AllianceCampaignBehavior.OnWarDeclared` saw no constant war and proposed call-to-war agreements for allies of pairs the same loop declared moments later: 294 proposals on the shipped data (253 for Hostile pairs), 41 and none Hostile with the phase first. TAOM's FullWar peace gates were also off in that window. | Lifecycle order | Once the guard read the real stance, nobody asked what the other listeners of `OnWarDeclared` read mid-loop; the order looked free. | The phase turns first again, the comment gives the reason, and the transition test asserts `FullWar` at declaration time. |
| F02 | MEDIUM | Only one transition test armed a stub that answers `AreAtWar` the way the game does, and with the wrong order it answered false anyway. No `ReconcileDeclaredWars` test armed it, so `!HasDeclaredWar && !AreAtWar` passed all 48 tests. | Weak test | The stub was written for one test; RCA #764 S6 had already named the blind spot. | `UseHostilePairs` arms the stub for every test; the review specified a mutation proof against the three reconcile tests. |
| F03 | LOW | The `HasDeclaredWar` doc said it ignores the model. `Kingdom.GetStanceWith` creates a missing link typed from `GetDefaultDiplomaticStance`, so a link-less Hostile pair reads true at Full War. | Over-claim | The doc described the intent, not `FactionManager.cs:78-87`. | Doc fixed in the adapter, the service interface and the feature doc, with the repair's limit stated (a pair first linked during Full War is stored as War without an event). |
| F04 | LOW | The "declared N missing wars" line counted `DeclareWar` calls, not stances changed; a vetoed or unknown pair was counted again on every load. | Over-count | The count copied the call, not the effect. | `TryDeclareWar` re-reads the stance, counts only a confirmed war and warns on a no-op. |
| F05 | LOW | A configured war with attacker equal to defender created and saved a stance link from a kingdom to itself. | Missing validation | `ValidateConfig` checked neither list. | Null, blank and self pairs are dropped with a warning. |
| F06 | LOW | The configured-war arm resolved ids over every kingdom, the Hostile arm over live ones, so an eliminated or unknown id was re-declared and logged on every load. | Inconsistent filter | Two arms, two definitions of "live". | Both arms use `GetAllKingdomIds`; tests for an eliminated and an unknown id. |
| F07 | LOW | The reconcile re-declared any listed pair at peace, including a legitimate peace and a run with `blockPeaceBetweenHostileTiers` off. | Wrong predicate | The repair did not use the predicate that creates the phantom war. | It declares only a pair the model calls at war (`ShouldBlockPeace`) whose stance is not War; the transition is unfiltered. |
| F08 | LOW | The reconcile's enable and auto-war gates differ from the model's at-war gate. | Gate mismatch | Two gates, one concept. | Gates kept (the repair reproduces what the transition declares); the two remaining cases documented. |
| F09 | LOW | No test turned the MCM enable toggle off; `Setup` pins the JSON branch only. | Missing test | Same blind spot the feature doc warns about. | MCM-off and MCM-on-JSON-off tests, with a mutated-gate check specified. |
| F10 | LOW | `ReconcileDeclaredWars` and `CheckPhaseTransition` were absent from `DivergenceProneMutators`. | Missing gate | The list was built for `DiplomacyBehavior`. | Both added to the list. |
| F11 | LOW | A launch that crosses Phase 2 ran the Full War scan twice (transition, then reconcile). | Redundant work | The reconcile was added after the phase check. | The reconcile runs first, so it sees only the phase the save restored; the comment says why. |
| F12 | LOW | A comment in the behavior called the replay "idempotent (AreAtWar guards)". | Stale text | The guard changed, the comment did not. | Comment rewritten, including "never guard with `AreAtWar`". |
| F13 | LOW | `war-of-the-ring.md` still said "(`AreAtWar` guard)". | Stale text | Same premise, second copy. | Reworded to the stored-stance guard. |
| F14 | LOW | `diplomacy.md` diagram and adapter list named `AreAtWar` and lacked `HasDeclaredWar`. | Stale text | File outside the change list. | Diagram and list updated. |
| F15 | LOW | The doc said the missing wars are declared "once at session launch"; the check runs at every host launch. | Over-claim | Wording from the first design. | Rewritten to describe a per-launch check that finds nothing after the first repair. |
| F16 | LOW | `AreAtWar` had no doc comment beside its new counterpart. | Missing doc | Not required by any standard. | One-sentence summary that warns against guarding a declaration with it. |
| F17 | LOW | No in-game step proved #772; "all Hostile pairs are at war" passes with the bug because the model reports it. | Missing test step | The old step trusted the model's answer. | Step 4 rewritten to read the chat and History entries; new step 7 for an older Full War save. |
| F18 | LOW | `coop-interop.md` said `OnSessionLaunched` calls only `CheckPhaseTransition`. | Stale text | Same cause as F14. | Row updated; hunk staged on its own. |
| F19 | LOW | The #764 RCA still called the Phase 2 bug an unverified lead. | Stale text | Written before the bug was confirmed. | Bullet replaced. |
| F20 | LOW | `"wars": null` or a `null` entry throws from the Phase 1 or 2 declaration, and now repeats on every host load. | Missing validation | The provider null-guarded the phases, not their lists. | Both lists normalised, one test per rule. |
| F23 | INFO | The orchestrator's scope notes said the at-war cache was "never rebuilt" and that `DeclareWar` skips "only on a shallow stance". | Wrong premise | Written from memory. | Wording corrected in the commit body; see "why each agent missed it". |
| F27 | INFO | A co-op note rested on an unverified replication claim. | Over-claim | The claim was relayed, not read. | Rewritten from BannerlordCoop source: the transition replicates, the load-time repair reaches clients through the join snapshot; the host-only gate is limited to BannerlordCoop. |

## Not applied

- **F21 (LOW):** `FindKingdom` allocates per call, about 140 calls per reconcile pass. Rejected, no change:
  old code outside #772, a few hundred allocations per launch (simplicity criterion, edit scope).
- **F22 (UNVERIFIED):** up to 70 `DeclareWarAction`s in one daily tick may hitch the map. No code change
  until measured; the review's plan is the map profiler across the Phase 2 day on an IsengardWar save.
- **F24 (INFO):** the #772 issue body misses three file rows, still says "Planned" and describes the old
  order in Solution bullet 1. An issue edit is an external write: done at close-out on Mike's word.
- **F25 (INFO), maintainer decision:** the fix fires `OnWarDeclared` at Phase 2 for the first time, so
  vanilla asks Permanent allies that are not Hostile with the enemy to join (41 proposals with the phase
  first, 20 ally and enemy pairs). Allies called to war at Phase 2 stays; documented in
  `docs/features/war-of-the-ring.md`.
- **F26 (INFO), maintainer decision:** a repaired save replays `DeclareWarAction`, so the History entries
  and map notices are dated at the load day. Kept; documented in the same file.

## Codex review (gpt-6-astra at xhigh, after the deep-review fixes)

Prompt `docs/reviews/codex-adversarial-wotr-phase2-wars-2026-10-08.prompt.md`; raw output in
`docs/reviews/raw/` (gitignored). Changed scope: 0 CRITICAL, 0 HIGH, 2 MEDIUM, 1 LOW, 0 false positives.
Codex ran the real service and behavior fixtures in memory (56 of 56 and 6 of 6 passed).

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| R1 | MEDIUM | The repair runs in `OnSessionLaunched`, before `Campaign.OnSessionStart` registers `KingdomManager`'s `WarDeclared` listener and `CampaignInformationManager`'s chat notices (v1.5.4 `Campaign.cs` `OnSessionStart`, `KingdomManager.cs:61-66` and `:90-100`, `CampaignInformationManager.cs:61-70` and `:79-83`). Confirmed mechanism; no code change. Faction Aggressiveness is only written (`KingdomManager.cs:94/98`, the daily decay at `:111/:121` and `AllianceCampaignBehavior.cs:630`) and read nowhere in the v1.5.4 decompile, so the lost increase changes nothing; the only visible loss is the chat line, while History entries and map notices remain. | Lifecycle order | The review had met the same mechanism as F26 and rated it a product choice. | Rejected by the simplicity criterion: a delayed repair needs a tick handler, a flag and tests, and could put about 67 chat lines on screen at once. The behavior is stated in the feature doc (Phase 2 section, in-game step 7). The same limit applies to any transition fired at session launch. |
| R2 | MEDIUM | No test failed if the host session launch stopped calling the repair, or called it after the phase check. | Weak test | `OnSessionLaunched` reads `Campaign.Current`, so every earlier test covered only the client path (absence). | `WarOfTheRingBehavior` takes an optional `Func<float>` clock; `OnSessionLaunched_Host_ReconcilesBeforeThePhaseCheck` (`Received.InOrder`) and `OnDailyTick_Host_ChecksThePhaseWithoutTheRepair`. Mutation proof: removing the call and moving it after the phase check each fail the new test (1 failed, 7 passed). |
| R3 | LOW | An unknown kingdom id in a scripted war passed validation and was skipped silently by the F06 live filter. | Silent skip | F06 and F05 fixed the loop and the blank and self cases, not the diagnostic. | The service warns once per process per scripted pair skipped for an unknown or eliminated kingdom, naming `war_of_the_ring.json`; two tests, both seen failing first. |

Suspect verdicts: S1 (a declaration short-circuit) disputed, the path declares through an existing neutral
link; S2 (side effects) mechanism confirmed, `GetStanceWith` creates links, but no common path, because a
normal campaign creates its neutral links during Peace; S3 (session repair) confirmed as R1, the ordinary
client gate omission disputed; S4 (counting and veto) disputed for the shipped configuration; S5 (calls to
war) confirmed, 41 static candidates (a static count, not a guaranteed live one); S6 (tests) confirmed as
R2; S7 (config validation) confirmed as R3. Evidence: the filtered suite (Diplomacy, WarOfTheRing,
CoopVeto, CoopAuthorityGate) 375 passed, 0 failed.

## Root-cause pattern

**A model-aware query used as the guard for the action the model pretends already happened.**
`AreAtWar` asks the game's model, and a TAOM override of that model answers "at war" for exactly the pairs
the feature is about to declare. Any guard of the form "skip if the model says it is done" turns the
override into a switch that disables the action. The #764 review found the loop (S6); the fix is to guard
by the stored state the action itself writes (`HasDeclaredWar`), and to fake the model in tests the way the
override answers it, following the phase, not as a constant.

**Work placed in a session event that runs before vanilla's managers register.** `OnSessionLaunched`
listeners run inside `Campaign.OnSessionStart`'s event dispatch, before `KingdomManager.RegisterEvents`
and `CampaignInformationManager.RegisterEvents`. A repair that raises `OnWarDeclared` there reaches the
campaign-event listeners but not those two managers. The loss was real but harmless here (a chat line, an
unread number); the pattern is that nobody had listed which listeners exist at that point.

**A host path that read `Campaign.Current` and so had no test.** The call sat after
`Campaign.Current.Models.CampaignTimeModel...`, which a unit test cannot build, so the suite tested only
the client branch (absence) and the service. Deleting the call, or moving it after the phase check, kept
all six behavior tests green. Injecting the clock makes the host branch constructible.

**One stale premise copied into many places.** F03, F12 to F16, F18 and F19 are the same sentence in
eight files: the guard was `AreAtWar`, or the call replayed "once". A premise that changes needs a grep for
its old wording across `docs/features/`, comments and the RCAs, not only the files in the diff.

## Why each agent missed it, or caught it

- **The #764 Codex review** found the bug (S6) and described the cure; it was out of that diff's scope.
- **Data flow** found F01 with a simulation of the shipped `diplomacy.json` (294 against 41 call-to-war
  proposals, stable across 500 shuffled kingdom orders) and the F25 count; **Engine** gave the upper bound
  for F01 and the F26 and F27 engine and co-op source reads; **Standards**, **Engine** and **Efficiency**
  each looked at F03 and disagreed on its reach; **Efficiency** found F11; **Design** supplied the F02 and
  F11 fix shapes; **Completeness** found F09. The verifiers narrowed several fixes (F03, F06, F07): a
  proposed fix was often wider than its evidence.
- **The orchestrator** missed F01 when it briefed M0b: it put the phase after the declarations to keep the
  old guard working, and never asked which other listeners read the phase mid-loop. It wrote the
  "never rebuilt" and "skips only on a shallow stance" notes from memory (F23), and it left the host call
  to a path no test could reach (R2).
- **Codex** found R2 and R3 with a deletion and reorder mutation and a probe; R1 is a rediscovery of F26
  with a harsher severity and a different cure.

## Follow-ups (outside this change; issues on Mike's word)

- **F21:** `FindKingdom` allocations stay as they are; revisit only if a profile shows the adapter hot.
- **F22:** measure the Phase 2 burst on the map with the map profiler before touching the declaration
  loop.
- **F24 and the issue body:** at close-out, with Mike's word, update #772 (past-tense Solution with the
  phase-first order, three Files Changed rows, the Testing section).
- **F25 and F26, for Mike:** whether allies are called to war at Phase 2, and whether a repaired save
  replays the declaration at the load day or is repaired silently. Both are documented as accepted.

## Lessons recorded

`docs/reviews/lessons/gamemodels-services.md`: guard an action by the stored state, not by a model-aware
query. `docs/reviews/lessons/state-lifecycle-save.md`: a session-launch write that raises a campaign event
fires before vanilla's managers register. `docs/reviews/lessons/testing-qa.md`: a host path that reads
`Campaign.Current` needs an injected clock to be tested.
