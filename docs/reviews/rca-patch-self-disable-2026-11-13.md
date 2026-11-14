# RCA: the self-disable notice (#1185), 2026-11-13

## Top line

`/deep-review` at risk tier 1 (a new call in `SubModule.OnApplicationTick`, report calls inside 19 Harmony patch
sites, and engine reads on the client tick) ran one Codex pass (`gpt-6-astra` at `ultra`, dispatched from the
worktree under Mike's standing word for tier 1; prompt
[codex-prereview-patch-self-disable-2026-11-14.prompt.md](codex-prereview-patch-self-disable-2026-11-14.prompt.md),
output under `raw/`), then lenses 5 Data flow and 2 Engine compatibility at `max`, with lens 7 XML and the Tooling
lens for the string row and the manifest. No CRITICAL, HIGH or P1, so no second wave. Lenses 1, 3, 4 and 6 were
skipped by tier.

The reviews confirmed 4 P2 findings from Codex, 3 MEDIUM findings and 14 LOW findings. Two pairs are the same
defect seen by two reviewers (rows 5 and 9, rows 11 and 13). All are fixed beside this RCA except the two items
under "Not applied". Two mutation scripts proved that the new tests fail without the fixes: 10 failures for the
Codex fixes, 6 for the lens fixes.

Nothing ran in game. Blast radius: `python tools/graphify_taom.py affected` on `SelfDisables`, `SelfDisableLedger`
and `SelfDisableNotice` names only the report sites, `SubModule.cs` and the new tests. Live sweep: this change wrote
nothing to the game folder.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED (lens 5 T1) | The engine's chat filter (v1.5.7 `MPChatVM.IsChatAllowedByOptions`, lines 1181-1195) drops a line with the single-player chat box off, or in a mission with Hide Battle UI on, and keeps no copy. The notice still wrote `[Check] ... shown=chat`, so the manifest read PASS for a line the player never saw. All four Marshal's Word ids report inside a mission | a claim of delivery without the engine's own filter | The builder read `InformationManager.DisplayMessage` and stopped at the call: it returns normally whether or not a listener keeps the line | Fixed: `SelfDisableNotice.Gate` holds the line in a mission with Hide Battle UI on, and writes `result=skip reason=chat-off` with the chat box off. Lesson in `localization-ui.md` |
| 2 | MED (lens 2 F1) | On a new campaign the gate (`Game.Current` set, no loading window) opened on the first frame of character creation or the intro video, so the line could fade before the map appeared | a gate on the wrong screen | The gate was written for a saved campaign, which goes straight to `MapState`; nobody traced `GameLoadingState.OnLoadFinished` for a new game | Fixed: the gate also needs `MapState` or `MissionState`, read in `ClientGate`, a `NoInlining` method a server never calls (CampaignSystem is not in the server bin) |
| 3 | MED (lens 5 T2) | A Hidden Hosts field that does not bind turns the nameplate or tooltip hook off for the process after one fault, and the 32-fault budget that reports never fills | a latch outside the inventory | The site inventory searched for fault budgets and catch latches, not for a null bind that returns early on every call | Fixed: each `ReportMissingField` reports `Patch105_HiddenHosts`; `HiddenHostsBindMissSelfDisableTests` call it through reflection |
| 4 | P2 (Codex F1) | The Hidden Hosts budget report sat inside the logger's `try`, so a logger that threw cost the report | report coupled to a log line | The report was put beside the existing log call, as the doc's "how to add a site" says, without asking what the log call can do | Fixed: the report runs outside the `try` on the first spent transition; three budget tests |
| 5 | P2 (Codex F2) | A chat line that threw lost the whole batch: `Drain` took every entry, then the throw left nothing to show | state cleared before the side effect | The drain was one step, so "taken" and "shown" were the same moment | Fixed with row 7: entries wait in `Unshown` until the line returns; `ShowFailed` stops a retry every frame, and the next new entry tries again |
| 6 | LOW (lens 5 T3) | The `[SelfDisable]` warning waited for the chat gate, so a session that quit at the main menu or crashed in a load had no log line. Three sites (Patch23 scene notification, Patch93, Patch66) have no other log line | log tied to display | Same cause as row 5 | Fixed: `Record` writes the warnings on the first tick after a report; `Show` waits for the gate |
| 7 | LOW (lens 5 T4) | `Unshown` filled only when the chat line threw, a case nothing in the repo produces | dead branch | Same cause as row 5 | Fixed: `Unshown` now holds every logged entry that is not shown yet |
| 8 | P2 (Codex F3) | Several report sites had no test that takes the branch: the party icon primary-only and secondary-only colour misses, a `RemoveRange` stream without `Ble`, and the gated tick | site tests incomplete | The builder wrote one test per transpiler class, not one per failure branch | Fixed: four tests in `SelfDisableTranspilerSiteTests`, and source pins on the order of the steps in `Tick` |
| 9 | P2 (Codex F4) | The new manifest had no C# test that renders the production line and no Python test of pass, fail, pass beside fail and empty | manifest test gap | The lesson from #1184 asks for both; the builder wrote the Python side only | Fixed: `RenderedLines_AreTheOnesThe1185ManifestAndFixturesRead` renders six fixtures through the real `OutcomeLog` |
| 10 | LOW (Tooling F1) | The three NOT REACHED tests stayed green when the fixture lost its `[BuildStamp]` line and the session was never judged | test pins the verdict, not its reason | The tests were copied from siblings that check only the verdict and the exit code | Fixed: each test also asserts the reason text and the evidence line |
| 11 | LOW (Tooling F2) | Two manifest edits (drop ` shown=chat` from `pass`; widen `fail` to any `[Swallowed]`) kept every test green and changed verdicts | manifest guards not pinned | Same cause as row 10 | Fixed: fixture `self_disable_near_misses.txt` and its test, and the C# twin |
| 12 | LOW (Tooling F3) | The manifest's `needs` line left out "start the game"; the config is read once per launch | instruction gap | The line was shortened from the doc's step | Fixed: "close the game, set ... true ..., then start the game and load a saved campaign" |
| 13 | LOW (Tooling F4) | Fixture timestamps out of order; a skip line in a client log, which only a server writes | fixture realism | Fixtures were edited by hand | Fixed: restamped; the server fixture has no client composition line |
| 14 | LOW (lens 5 T5, lens 2 F2) | Code and doc said `Report` takes no lock; `ConcurrentDictionary.TryAdd` takes a short internal lock | wrong engine-adjacent claim | The builder knew the type as lock-free from its name | Fixed: "takes only the short internal lock of `ConcurrentDictionary.TryAdd`" in both places |
| 15 | LOW (lens 5 T6, lens 2 F3) | The doc said Patch15 reports on every load (it reports once per launch), and that the thrower stand is lost "for the mission" (after a failed field bind it stays off until the game restarts) | doc claim against code | The builder wrote each row from the site's own comment, not from its latch lifetime | Fixed: the verify steps and the ThrowerStand row |
| 16 | LOW (lens 2 F4) | Four sites had no real-IL test, so a miscount would show a red line to every player | binding coverage | The site tests feed synthetic streams; nothing ran four of the transpilers over the installed IL | Fixed: four "reports nothing" tests in `TranspilerSiteBindingTests` (scene notification, body generator, order banner, Patch15 with the default config) |
| 17 | LOW (lens 2 F5) | The registry said every anchor miss and every latch reports | universal claim | The sentence was written before the exclusions list | Fixed: line 5 now points at the doc's list and the server case |
| 18 | LOW (lens 2 F6) | Two line cites went stale when a `using` line moved code down (`reflection-sites.md` `:69`, `character-selection.md` `:16`) | stale cite outside the gate | Category D rows are outside the line-label gate | Fixed: `:70` and `:17`; the six gated labels the same `using` lines moved were repointed when `EveryLineLabel_PointsAtALineThatNamesItsMember` failed |

Lens 5 T7 (nothing checks that the ids, the doc table and the registry headings match) is under "Not applied".
Lens 7 found no defect in this change.

## Root-cause pattern

Rows 1, 5, 6 and 7 are one pattern: the notice treated "the call returned" as "the player saw it", and did the log,
the display and the bookkeeping in one step. The engine does not work that way. `DisplayMessage` invokes a
delegate; the chat view model then applies the player's options and may drop the line with no trace. A `[Check]
result=pass` line is evidence for a verdict manifest, so a false one costs more than a missing one: it closes an
issue.

The rule that follows: a line that claims something reached the player names the engine filter it passed, or it is
written only after the engine's own state shows the effect. Log at once; show when the screen can show it.

Rows 2 and 3 are inventory gaps: the gate and the site list were built from the common case (a saved campaign; a
budget latch). Lens 2 found row 2 by tracing every path out of `GameLoadingState`. Lens 5 found row 3 by asking of
each early return whether it skips every later call.

This class has shipped before for outcome lines: `rca-muster-captain-2026-11-13.md` (pass written before the start
call) and the source-pin lessons in `testing-qa.md`. Those were about where the pass is written; this one is about
what the pass may claim.

## Why each reviewer missed or found these

- **Codex** found rows 4, 5, 8 and 9 by reading the failure paths of each step and by checking the #1184 manifest
  lesson against the new tests.
- **Lens 5 Data flow** found rows 1, 3, 6, 7, 14, 15 and T7 by following the line past `DisplayMessage` into
  `MPChatVM` and by checking each site's latch lifetime against the doc.
- **Lens 2 Engine compatibility** found rows 2 and 14 to 18 by tracing the load sequence for a saved game, a new game
  and a mission, and by reading every transpiler anchor in the installed v1.5.7 IL. It confirmed that a healthy
  start reports nothing.
- **The Tooling lens** found rows 10 to 13 by mutating the manifest and the fixtures and running the tests.
- **The builder** tested the notice against fakes of its own interfaces, so the engine's filter was outside every
  test.

## Improvements (Step 4)

Lenses 3 and 6 did not run (tier 1, first wave only), so there is no design proposal to apply.

APPLIED:
- `Main/Core/Diagnostics/SelfDisableNotice.cs`: `Record` and `Show` replace `Drain`; `Gate` and `ClientGate` read the
  screen and the two chat options.
- `Main/Core/Diagnostics/SelfDisables.cs`: `Unshown` and `ShowFailed`; the lock wording.
- `Main/Features/HiddenHosts/`: the budget report outside the `try`, and a report in each `ReportMissingField`.
- Tests: the rewritten `SelfDisableNoticeTests`, `HiddenHostsBindMissSelfDisableTests`, three budget tests, four site
  tests, four real-IL tests, seven `Issue1185Tests` with the near-miss fixture.
- Docs: `patch-failure-notice.md`, `harmony-patch-registry.md`, `reflection-sites.md`, `character-selection.md`,
  `1185.toml`.
- Proof: the full C# suite and `python -m unittest discover -s tools/tests -t .` on the fixed tree, with only the
  failures that are also red on trunk; `mutate1185.py` (10 failures with the Codex fixes reverted) and
  `mutate1185b.py` (6 failures with the lens fixes reverted).

NOT APPLIED:
- A guard for `MarshalSpacingPatch.Prepare` returning false without a report (lens 5): it needs `IoC.Resolve` of a
  registered singleton to throw, which nothing in production does. A speculative guard is Mike's call
  (`simplicity-criterion.md`).
- A test that the report ids, the doc table and the registry headings match (lens 5 T7): they match today (18 ids, 18
  rows, every category has a heading). The test is a new gate with an upkeep cost; Mike decides whether it earns it.

Deferred, each with its issue:
- FOLLOW-UP #1223: four feature latches outside a patch class (custom attack blow binding, action index repair,
  Player Switcher session latch, map view release) do not report yet.

## Lessons codified

- `docs/reviews/lessons/localization-ui.md`: "A notice that writes `shown` names the engine's display filter".
