# RCA: at-sea repair review (2026-11-18)

## Top-line

The load-time repair that moves parties a save left flagged at sea back to land (the follow-up of
PR #1437) ran `/deep-review` tier 1: lens 5 data flow and lens 2 engine compatibility, at the
definition's effort. Neither found a CRITICAL or HIGH, so no second wave ran. Both lenses found the
same MEDIUM: the first draft moved an army leader but left its attached parties on the water. Every
finding below except R4 was fixed before the commit. Codex did not run: it is out of usage.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| R1 | MEDIUM | The draft set `IsCurrentlyAtSea = false` and `Position` on the leader. The flag setter passes only the flag to attached parties, so army members stayed at their sea positions with the land flag, and a member that left the army before it moved stayed stranded (lens 5, lens 2). | Hand-rolled engine move | The setter's cascade looked like the whole move; vanilla's own `DisembarkToPosition` was not searched for. | Uses `DisembarkToPosition(gate)`, which moves attached parties. Before writing `Position` on a party, search for the vanilla method that relocates it. |
| R2 | MEDIUM | The repair keyed on the flag alone, but its premise is the parked naval model. Re-enabled by the RE-ENABLE recipe, every load would have teleported every sailing party, the player too (lens 5). | Premise not encoded | The premise was true today and lived only in the doc. | `Decide` takes `HasNavalNavigationCapability`, with a test. |
| R3 | LOW | A party saved mid-transition: vanilla's transition model returns `CampaignTime.Never`, so the first tick finishes the transition by flipping the flag, which would undo the repair (lens 2). | Engine tick undoes a load write | The transition state was not in the trace. | `Decide` takes `IsTransitionInProgress` and leaves the party to vanilla, with a test. |
| R4 | LOW | The nearest gate is chosen by straight-line distance, so a gate among the unreachable entrances of #1434 can strand the party on land (lens 2). | Bad map data upstream | Not missed: the entrance audit needs native path queries per party. | Not fixed here; the doc names the limit and #1434 owns the map fix. |
| R5 | LOW | The doc and comment said the AI picks a new goal on its next tick; a non-army party thinks only every six hours (lens 5, lens 2). | Engine claim unchecked | Inferred from `SetMoveModeHold`. | Sets `Ai.RethinkAtNextHourlyTick = true`, as vanilla does after a forced hold; text corrected. |
| R6 | LOW | The count line did not add up when an army was at sea, and the catch message was wrong after a partial run (lens 5). | Log accuracy | Written before the attached case was traced. | Count line names each bucket; the catch line gives the moves done. |
| R7 | LOW | The co-op row said Coop syncs party positions; Coop has no live sync for them. Clients get the repaired state from the join save (`ApplyJoinState`) (lens 5, lens 2). | Wrong mechanism in doc | Assumed from Coop's other syncs. | Row corrected. |
| R8 | LOW | The doc's tests section did not name the new tests (lens 5). | Doc drift | New test class added after the section. | Section updated. |
