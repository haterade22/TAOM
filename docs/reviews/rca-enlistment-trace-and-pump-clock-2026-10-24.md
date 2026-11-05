# RCA: The enlistment trace ships off, and the service upkeep runs once a frame (#840, #841, deep-review, 2026-10-24)

## Top-line

`/deep-review` on the two commits for #840 and #841 ran lenses 1, 2, 3 and 5 in a first wave and lenses 4
and 6 in a second, then an engine research pass and a two-lens review of the guard it led to. The first
wave found no code defect: the installed MCM runtime keys every saved value on the property name, so the
renamed setting reaches every player off, and the wait menu's campaign delta is exactly zero only while
time is stopped. The design lens then found that **#841 widened an existing risk** (MEDIUM): on a running
service menu every upkeep-raised join now came from the campaign tick, and in that part of the frame the
engine still updates a raid that is the player's own event. The change now holds such a raid join for the
next wait-menu frame. The other findings were LOW, in comments, docs, release notes and tests; all were
fixed before the push.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Design, confirmed by research) | `MapEventManager.Tick` updates the player's own map event only when it is a raid (`MapEventManager.cs:59-61`), and it runs after `TickEvent` and the hourly tick in the same frame (`Campaign.cs:980-982`). A raid the soldier joins on the defending side from either tick can run a simulation session in that frame (`MapEvent.cs:889-900`), with his troops taking casualties. Before #841 about half of the running-menu joins came from the wait-menu tick, which runs before time advances; after it, all of them came from the campaign tick. | A change of WHICH caller performs an action, made for cost, that also changed WHEN in the frame the action runs | The issue and the first wave treated the two feeders as interchangeable sources of the same budget; the feature doc already listed the raid window as Open risk 1, but no lens connected it to the feeder. | A raid join raised outside the wait-menu frame, while the service menu is current, is held and performed at the start of the next menu frame; field battles, sieges and joins off the menu are unchanged. One service entry point (`OnWaitMenuFrame`) owns the menu frame. Lesson appended to `lessons/campaign-mechanics.md`. |
| F2 | LOW (Data flow, Engine compat) | The docs gave the upkeep's rate on the map and on a stopped menu, not on the running wait menu where an enlisted player sits. | A cadence stated for the wrong screen | Derived from the budget arithmetic, not the screen the player watches. | Cadence stated in campaign time and per speed; the owed check is a latency a tester can see. |
| F3 | LOW (Completeness, Engine compat) | The release notes said the trace shipped on in v2.0.34 and that "every player" had it saved; it shipped on from v2.0.24 through v2.0.35, and MCM writes its file only when a player confirms a setting change. | Release history from memory | Written from the trap's summary, not from the tags. | Read from the release tags; the body and comments say who has the value saved. |
| F4 | LOW (Design) | The stopped-menu step clamp did nothing: the pump resets both budgets to zero when they fire, so any step fires each tier at most once. | Dead safeguard | Kept from the earlier design without checking the consumer. | Clamp and its policy file deleted; a test pins the reset. |
| F5 | LOW (Standards, Data flow, Completeness) | Stale comments outside the diff (latency, position-sync driver, the reconciler's subscriber), a duplicated sentence, wrong literal counts, a source-scan test with a false reason, test names outside the house form, the line pin not lowered, the guard's reconciler route and three drop checks untested, a doc sentence that the main hero can be a simulation casualty (he never is, `MapEventSide.cs:765`). | Comment, doc and test accuracy | Moved facts not grepped; tests written for the happy path. | All fixed; the wait-menu hook is now under the generic line gate. |

Noted, not changed:

- In the first two ticks after a load (`CurrentTickCount < 3`) the engine updates map events even with time
  stopped, so a raid joined from the first menu frame after a load is updated with the soldier inside.
  A session needs a save taken between a relief's arrival and the next update; older than #841,
  unverified, no code guard.
- A raid joined while the soldier is off the service menu keeps the original window (the trade-off that
  keeps a join from waiting on a frame that never comes).
- When a raider reaches the village the soldier stands in, the engine replaces the encounter the battle-start
  join opened; consequences unverified, `/investigate` only on a player report.
- At a fast-forward multiplier above 32 the status board rebuilds faster than 4 Hz (older, unmeasured);
  the interval constants keep "Seconds" in their names although they are budget units.
- The persisted-MCM-default trap is still guarded by convention only; a script comparing defaults
  against the last release tag would make it a gate.

## Root-cause pattern

F1 and F2 share one cause: a timing change was judged by how often the work runs, not by where in the
engine's frame it runs and which screen the player is on. The research pass that settled F1 read the
frame order line by line; that is the check a feeder change needs before review.

## Feedback memories to codify

None; the lesson covers it.
