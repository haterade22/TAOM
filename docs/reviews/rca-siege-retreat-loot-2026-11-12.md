# RCA: Siege Retreat Loot (#1121), deep-review of 2026-11-12

## Top-line

A tier 1 `/deep-review` (Lens 5 Data flow, Lens 2 Engine compatibility) of the first build of Patch146 found **1 HIGH, 6 MED and 2 LOW**
items. No API was incompatible with v1.5.7. Every finding was fixed before the commit: the design shrank (the item and
prisoner steps, the winner override, the party screen, the hero-prisoner rule and two data classes were removed), and
the check, the manifest, the co-op gate, the reflection catalogue and four doc sentences were corrected.

A second look at the fixed state (Lens 5, effort high) found no HIGH or MED item and four LOW items. Three were fixed
(the pending roster is cleared only after the screen opens, the docs say what the engine proves about the stale result,
the manifest comment is exact). The fourth needs vanilla's own handler to throw after the prefix; no throw site is known,
so it was left.

The Codex pass did not run: the account returned "You've hit your usage limit ... try again at Nov 14th, 2026 6:56 AM".
That pass is owed on the committed diff after that time. The full C# suite in a clean worktree had 3 failures in
unrelated features (the `TaomClanFinanceModel` 150-line gate, missing loot-key translation rows, mixed writing
systems); none names a file of this change. They were not run on a clean HEAD, so "already failing" rests on the failure
text and the diff, not on a baseline run.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | HIGH | `tools/ingame_checks/1121.toml` had `fixed_in = "UNCOMMITTED"`; the loader needs a 7 to 40 digit sha, so `RepoManifestGateTests` fails and no verdict can run for #1121 | Gate the suite never ran | The 39,019-test C# run does not include `tools/tests`; my first Python run timed out and I read nothing from it | Run `tools.tests.test_ingame_verdict` after any manifest edit; use the trunk commit before the work as `fixed_in` |
| F2 | MED | The adapter forced `DefenderVictory` and restored the old state. On the real path the state is already `DefenderVictory`, so both writes do nothing. On a stale `EnemyRetreated` result (state `None`) the write runs `OnBattleWon` and creates rewards that vanilla then commits | Unlisted writer of an engine state | I read vanilla's `SetOverrideWinner(None)` at `SiegeAssaultEventComponent.cs:161` and took the state at the handler to be `None`; nobody listed what writes it before | List every writer of a state a patch sets and restores, with citations (`CampaignMissionComponent.OnMissionResultReady`, `PlayerEncounter.SetPlayerVictorious`); gate on the state instead of forcing it |
| F3 | MED | The item step takes about half the baggage of each besieging party (`DefaultBattleRewardModel.cs:313`), and those parties survive and keep the siege; the prisoner step frees their captives | Vanilla step reused on a path that breaks its assumption | The steps assume the loser is destroyed (`CaptureDefeatedPartyMembers` follows them in vanilla). I copied the three steps as a unit | State what the reused step assumes about the loser before copying it; here only the casualty step fits |
| F4 | MED | The outcome line was written in the prefix (collection), not when the screen opened (delivery). Three error texts were outside the manifest's `fail` pattern, and a `ReadState` throw gave "not reached" forever | Check measures the wrong step | I placed the line where the data was easiest to count | Write the pass line where the effect lands; one tag in every error text and one `fail` pattern, with a test that scans the source for `LogError` texts |
| F5 | MED | `SiegeRetreatLootTally.Prisoners` could never be nonzero: freed prisoners land in the members roster and `CaptureDefeatedPartyMembers` never runs | Field with no producer | The service test faked a value (3, 2, 1) the adapter cannot produce | Trace every writer of a counted roster before adding a field for it (resolved by removing the field) |
| F6 | MED | No co-op client gate: a client would run the roster writes the host owns | Missing policy gate | The sibling siege code uses `CoopSessionPolicy.MayWriteSaveBackedState`; I did not search for it | Search the feature neighbours for the policy predicate before writing a service that writes rosters |
| F7 | LOW | The private step had no row in `reflection-sites.md` Category B and no `[DataRow]`; `patch-targets.md` had no Patch146 row | Procedure step skipped | `taleworlds-api-snapshot/README.md` states the rule, and my own binding test already guarded the member, so the gap looked closed | A new `AccessTools.Method` on an engine member gets the catalogue row in the same change |
| F8 | LOW | Four texts said the loot steps "cannot run while the winner is none" | Wrong premise copied into docs | The premise was the F2 assumption | Fixed with F2 |

## Root-cause pattern: a state the patch forces was never traced to its writers

F2, F3 and F5 share one habit: I designed from the vanilla handler's own lines and from the shape of the vanilla win path,
and did not trace what the engine had already written before the seam. `csharp-architecture.md` already has the rule
("Writers before gates": an engine flag a plan relies on lists every writer, vanilla first, with citations). It names a
gate that reads a flag. It does not name a patch that **sets and restores** one, which is the case here.

The unifying rule: before a patch writes engine state around a call, list every writer of that state that runs before
the seam. If the state already holds the wanted value, delete the write and gate on it.

## Why each lens caught or missed it

- **Lens 5 Data flow** found F1, F3, F4, F5 and the F2 trace, by following `BattleState` and the loot rosters end to end.
- **Lens 2 Engine compatibility** found F2 (the stale-result trigger), F6, F7 and the wrong engine claims in the docs, and
  cleared every signature, the Harmony binding and the thread question.
- **Lenses 1, 3, 4 and 6** did not run: tier 1 launches 5 and 2 only (`deep-review` Step 2). Nothing in the findings was a
  standards, efficiency or completeness item that those lenses would have owned, except F7, which Lens 2 also caught.
- **My own pre-review checks** (full suite, focused gates) could not see F1 (no Python run), F2 (no test models the
  state at the seam) or F3 (the tests fake the adapter).

## Feedback memories to codify

None. The lessons below carry the rules (`docs/reviews/lessons/campaign-mechanics.md` and `build-tooling-workflow.md`).

## Open

- The Codex pass on the committed diff, after 2026-11-14 06:56 (usage limit). No issue: it is a review step, tracked here.
- The in-game check (#1121, label `triage-needs-ingame`): the inventory loot screen, the siege intact, and no loot on a
  second assault.
- UNVERIFIED: whether a non-enlisted defender can reach the stale-result branch, and whether the co-op siege mission
  produces an `EnemyRetreated` result. The guards cover both without needing the answer.
