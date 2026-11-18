# RCA: naval land-pathfind guard review (PR #1437, 2026-11-18)

## Top-line

The maintainer review of PR #1437 (a Prefix that replaces the native nearest-land search, which faults
without NavalDLC) ran `/deep-review` tier 1: lens 5 data flow and lens 2 engine compatibility, at the
definition's effort. Neither found a CRITICAL or HIGH, so no second wave ran. Lens 2 verified the
target binding, every managed API and the native root cause in Ghidra. The findings are wrong claims
in the docs and comments, plus one behaviour change for an invalid face. Codex did not run: it is
out of usage. The patch was also renumbered from Patch159 to Patch168, because another branch owns
Patch159 to Patch167.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| R1 | MEDIUM | The doc said the A* path bodies do not read the region map. Only the non-region search avoids it; the region-switch-cost variant reads it with no NavalDLC check. TAOM is safe only while both region-switch costs are 0 (lens 2). | Engine claim scoped too wide | The PR traced the path TAOM uses today. | Doc corrected; a campaign trap sheet row keeps both costs at 0. |
| R2 | MEDIUM | The doc and the PR's in-game pass condition said the party gets a land point and walks to land. Vanilla's navigation model makes water invalid, so `ComputePath` returns early, and the hourly caller uses the gate only for a distance lookup that has no water-face entry (lens 2, lens 5). | Expected effect not traced to the consumer | The trace stopped at the returned point. | Doc says the crash stops and the party may stay at sea. FOLLOW-UP: a one-time repair at load that moves parties found at sea to land, no issue yet (owner waived new issues for this work). |
| R3 | LOW | An invalid face returned (NaN, NaN), and the registry said the native wrapper does the same. The native wrapper returns (0,0) before it searches (lens 2). | Engine claim from a name | The early return is native only. | The prefix now runs the native wrapper for an invalid face; registry and doc corrected. |
| R4 | LOW | The log line blamed TAOM_Map and said "nearest settlement gate" even when the result was invalid (lens 2). | Log text | Written before the cause was known. | The line names the missing NavalDLC region map and prints the result. |
| R5 | LOW | The text said TAOM never loads NavalDLC. BLSE or an edited module list can load both; TAOM declares it incompatible (lens 2). | Overstated premise | | Wording corrected. No NavalDLC gate on the patch: an unsupported setup gets a worse answer, not a crash. |
| R6 | LOW | The prefix can run on the engine's AI worker (`Campaign.LateAITick`), and nothing said so; the catch logged on every failure (lens 5). | Thread not documented | | The summary, registry and doc name the thread; both log lines are once per process. |
| R7 | LOW | Three comments (`INavalTravelService`, Patch57, `CaravanGateDiagnosticsService`) said no party can reach the sea or the move-to-land state (lens 5). | Stale premise | The PR changed what is reachable. | Comments updated. FOLLOW-UP: model `MoveToNearestLandOrPort` in the caravan diagnosis, no issue yet (owner waived new issues for this work). |
| R8 | LOW | The co-op classification said peers agree, which is true but not why it is safe: Coop runs the AI think only on the authority (lens 5). | Reason text | | Reason reworded. |
