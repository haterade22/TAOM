# RCA: War of the Ring session reset, #764 (2026-10-08)

**Summary.** Milestone M0 of the War Chronicle work fixes #764: the process-lifetime
`WarOfTheRingService` carried the previous campaign's phase and outcome into a new campaign started in
the same process. The first cut followed the session-reset rule in
`.claude/rules/csharp-architecture.md` and reset the service from `WarOfTheRingBehavior.OnSessionLaunched`
behind a `_syncedThisSession` latch. The first `/deep-review` wave (Standards, Engine compatibility,
Data flow, Efficiency) found that this ran too late: three lenses showed, independently, that two
readers saw the stale phase before the reset. The fix moved the reset into the behavior's
constructor. The second wave (Data flow and Engine compatibility again, plus Completeness and Design)
confirmed the HIGH closed on every path; its remaining findings were fixed in the same change. Nothing
had been committed before the review.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The `OnSessionLaunched` reset ran after two readers. (a) Campaign-event listeners run newest-first (`MbEvent<T>.AddNonSerializedListener` inserts at the head and `Invoke` walks from it, v1.5.4 ``MbEvent`1.cs:24-44``), so `WarOfTheRingMomentumBehavior`, added after this behavior (`SubModule.cs:1181`, `:1185`), ran `SweepEnrollment` on the stale FullWar first (`MomentumEnrollmentService.cs:35`): it enrolled every kingdom, started the momentum war on day 0 (`:66-71`) and the first save persisted it. With victory on, an early `EndWar` would have repeated #764 itself. (b) Vanilla `CampaignFactionManagerBehaviour.OnNewGameCreated` cached each kingdom's at-war list through `TaomDiplomacyModel.IsAtConstantWar` on the stale phase, and `OnNewGameCreated` is dispatched before `OnSessionStart` (`Campaign.cs:1709-1710`). | Lifecycle order | The house rule prescribes exactly this shape ("calls the reset from `OnSessionLaunched`"), and the brief copied `FiefGrantingCampaignBehavior`'s latch. The LIFO dispatch trap was already recorded in `lessons/harmony-il.md` (#557, 2026-09-07) but never reached the session-reset rule, and `lessons/state-lifecycle-save.md` still taught an `OnSessionLaunched` reset. The new tests exercised the behavior alone, so the order between behaviors could not show. | The reset moved to the constructor. The behavior is built with `new` in every campaign's `OnGameStart` (`Campaign.cs:1410`), before `LoadBehaviorData` (`:1448`) and before every campaign event; a load's `SyncData` restores the saved state over it. New lesson; rule amendment as a follow-up commit. |
| 2 | MEDIUM | Nothing pinned the invariant the fix relies on: the behavior must stay built with `new` per campaign. The momentum behavior two lines below is a container singleton; in that shape the constructor runs once per process, and every behavior test would still pass because each constructs the behavior directly. | Missing gate | The fix's correctness rests on a wiring fact outside the class. | `WarOfTheRingWiringTests` (untagged, so the reference-assembly build runs it) and a comment at the construction site in `SubModule.cs`. |
| 3 | LOW | The load test proved call order, not restored values: a substitute `IDataStore` leaves the refs at Peace and None, the same values the reset writes. | Weak test | NSubstitute does not round-trip `ref` values. | A save-then-load round trip with a real service and a keyed fake store (WarEnded and EvilVictory survive), plus a new campaign after a WarEnded campaign; the old test renamed to the missing-key case it proves. |
| 4 | LOW | The reset logged "session reset for a new campaign" on every load too, just before "Phase restored from save". | Misleading log | The text described the first design. | "phase reset to Peace at campaign start; a load restores the saved phase next". |
| 5 | LOW | Docs: wrong test counts (31 for 34; `PeaceActionHookTests` 3 for 5, pre-existing); "SyncData never runs for a brand-new game" (it runs in saving mode on every save); no in-game step for a second campaign; the Key Files rows described the behavior only as the daily timer. | Doc drift | Counts written by hand; the wording copied from FiefGranting's comment. | Counts re-measured with grep; "never runs in loading mode"; How to Test step 6; both Key Files rows. |
| 6 | LOW | The first cut's co-op comment said a client "always" has a loaded record. A first-time BannerlordCoop joiner first runs a local new campaign, then loads the host save. | Over-claim | Carried over from the 2026-08-01 note that a co-op join is a save-load. | Removed with the latch. The constructor reset is harmless on that local campaign, and the host save restores the phase. |
| 7 | LOW | The old `OnNewGameCreated` listener reset the fresh instance's own two ints, a no-op: the original reset, aimed at the wrong object. | Dead code | It looked like a reset. | Deleted. |

## Root-cause pattern

**A reset placed by event name, not by reader.** The house rule and the precedent it points to choose
the reset point by event (`OnSessionLaunched`) and never ask what reads the singleton before that event
fires. With newest-first dispatch, every behavior added later reads first, and so does every vanilla
handler on an earlier event such as `OnNewGameCreated`. The constructor of a per-campaign behavior is
the one point TAOM owns that precedes every campaign event on both the new-game and the load path.
`RegisterEvents` is not such a point: on a load it runs after `LoadBehaviorData` and would wipe the
restored record.

## Why each agent missed it, or caught it

- **First wave:** Standards, Engine compatibility and Data flow each caught finding 1 on their own, by
  opening ``MbEvent`1`` and the momentum behavior. Efficiency found the dead listener (7) and routed a
  related momentum-store gap to the follow-ups.
- **The orchestrating session** missed finding 1 when it briefed and checked M0: it compared the diff
  with the FiefGranting pattern, not with the readers of the singleton.
- **Second wave:** Completeness and Design caught findings 2 and 3. Data flow re-traced 20 flows and
  confirmed the fix. Engine compatibility verified 20 claims (11 API usages, 9 engine claims) with none
  incompatible.

## Codex review (gpt-6-astra at max, after the deep-review fixes)

Prompt `docs/reviews/codex-adversarial-wotr-session-reset-2026-10-08.prompt.md`; raw output in
`docs/reviews/raw/` (gitignored). Changed scope: 0 CRITICAL, 0 HIGH, 1 MEDIUM, 0 LOW, 0 false
positives. S1, S2 and S5 disputed with quoted v1.5.4 code (an IL scan of 95 assemblies found no
mid-campaign path to `OnGameStart`); S3 and S4 confirmed; S6 confirmed as a pre-existing HIGH.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| C1 | MEDIUM | The first `WarOfTheRingWiringTests` passed a cached construction (`_cachedWotr ??= new WarOfTheRingBehavior(...)`): it only checked that `new WarOfTheRingBehavior(` appeared and that no `Resolve<...>` did. Codex evaluated the predicates on that mutant in memory and all four held. | Weak guard | The pin was written in the contains-check shape of `FieldCampWiringTests`, and nobody, the design lens included, tried the cheapest regression that keeps the predicate true. | The pin now requires the construction inline as `campaignStarter.AddBehavior`'s argument, exactly once, scans every production file for any other mention, and carries the cached, static, duplicated and container-resolved shapes as negative rows. Lesson in `lessons/testing-qa.md`. |
| S6 | HIGH, pre-existing | At Phase 2, `TransitionToPhase` sets `CurrentPhase = FullWar` before `DeclareHostileTierWars`, so `AreAtWar` answers true through `TaomDiplomacyModel.IsAtConstantWar` and every declaration is skipped. The real stances stay neutral, no `OnWarDeclared` fires, and AI target selection reads `FactionsAtWarWith`, which nothing rebuilds. `AllianceAdapter.DeclareWar` repeats the same guard. | Logic order | Every test fakes `AreAtWar` independently of the phase, so the feedback loop through the model cannot show. | Its own issue (#772) and fix (milestone M0b): guard each declaration on the real stance (`HasDeclaredWar`), reconcile existing Full War saves, and a test whose `AreAtWar` follows the phase. |

## Follow-ups (outside this change; issues on Mike's word)

- Amend the session-reset rule in `.claude/rules/csharp-architecture.md`: reset persisted per-campaign
  state in the constructor of a behavior built with `new` per campaign; never in `OnSessionLaunched`,
  `OnNewGameCreated` or `RegisterEvents`. The 2026-09-24 RCA
  (`rca-cross-campaign-singleton-resets-2026-09-24.md`) deferred a similar amendment once already.
- The momentum store resets only on `OnNewGameCreated`, so a save with no momentum record (written
  before #327) keeps the previous campaign's momentum, and with victory on its `OnSessionLaunched` can
  call `EndWar` with the old victor. The behavior is a container singleton, so the fix makes it
  per-campaign with a constructor reset.
- FiefGranting, SupplyLines, FieldCamp and Refuge keep the latch shape. FiefGranting converts as a pure
  deletion; the other three first need a check that their visuals reset is safe at `OnGameStart`.
  TournamentRewards and Enlistment also reset in `OnSessionLaunched`; whether anything reads them
  earlier is unchecked.
- Phase 2 never declared the Hostile-pair wars (Codex S6 above, confirmed): filed as #772 and fixed as milestone M0b of #765; see `rca-wotr-phase2-wars-2026-10-08.md`.

## Lesson recorded

`docs/reviews/lessons/state-lifecycle-save.md`: "Reset a per-campaign singleton in the constructor of a
behavior built per campaign, not in a campaign event", with a pointer under the four earlier lessons
that taught an `OnSessionLaunched` reset.
