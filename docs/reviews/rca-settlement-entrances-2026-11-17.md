# RCA: #1434 settlement entrance audit and unattended console commands (2026-11-17)

**Scope:** Batch C of the port of Silas He's fixes. The audit `taom.audit_settlement_entrances` gains the terrain and
path checks vanilla's distance cache applies (`SettlementEntranceCheats.cs`, new `SettlementEntranceRules.cs`); the
scenario autorun runs armed console commands (`command=` arm key, `run_scenario.py --command`, `commands.txt`); the
drift gate `tools/check_settlement_entrances.py` (data gate `map-entrances`). Branch `fix/silas-port`, on top of
79907e51c, reviewed uncommitted. Tier 1: Lens 5 Data flow, Lens 2 Engine compatibility and the Tooling lens, then a
re-review of the runtime fixes by Lens 5 and Lens 2. No XML changed. Codex skipped (out of usage since 2026-11-15);
its pass is owed before merge.

**Top line:** four MED and many LOW findings. The two MED findings in the audit share one cause: the rewrite added a
path check but took its starting points from the cheap checks, so it tested reachability from points nobody had
shown to be on the main map. Lens 2 confirmed all 15 engine APIs on the installed v1.5.7 DLLs and, from the native
decompile, that the old path cost limit cut searches short.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | References were the nearest town or castle entrances that passed the cheap checks, and one successful path passed an entrance. The `castle_orthanc_gate` and `town_isengard` gates are 2.72 apart and each is the other's nearest reference, so if both sit in one cut-off pocket each vouches for the other. Fixed: `SettlementEntranceRules.ConnectedToMain` path-tests every reference, joins each successful pair into a group, and only the largest group is the main map; a smaller group that no main member was tested against gets one try from the nearest main member. Tests `APocketWhoseMembersReachOnlyEachOther_IsNotConnected`, `AClusterWhoseReferencesAreAllInsideIt_GetsOneTryFromTheMainGroup`, `AGroupAMainMemberAlreadyFailedToReach_GetsNoExtraTry`. (Lens 5; Lens 2 noted it outside its lens.) | Reachability, unanchored reference | The builder asked "does a path reach this entrance" and never "is the start of that path on the main map". | Lesson added: lessons/campaign-mechanics.md, "A reachability check tests from points already shown to reach the main map". |
| 2 | MED | References were assigned only to records that passed the cheap checks, so a suggestion for an off-mesh, other-island or excluded-terrain record had no reference and skipped the path test (null counts as Ok), against the class comment. Fixed: references for every record, from main-map towns and castles. (Lens 5; Lens 2 finding 3.) | Data flow, early `continue` | The assignment sat after the `continue` that skipped failed records. | Covered by finding 1's lesson. |
| 3 | MED | The path cost limit was a hand-picked 2000. Vanilla's cache passes `Campaign.PathFindingMaxCostLimit` (SandBoxNavigationCache.cs:84; Campaign.cs:1120), and the native A* drops a neighbour once its cost estimate passes the limit, so a long detour read as NoPath. Fixed: the vanilla value. (Lens 2; Lens 5 trace 7.) | Engine parity | The comment claimed vanilla's settings after checking three of the four arguments. | Existing rule (AGENTS.md "Research first": quote the line). |
| 4 | MED | `check_settlement_entrances.py` exited 0 with SKIPPED when the live file was missing; a gate that checks nothing must not pass on its own. Fixed: `ERROR` and exit 2, as its three sibling tools do; data_gates reads NOT RUN. Test `test_a_missing_file_exits_two_and_data_gates_reads_not_run`. (Tooling.) | Gate contract | The SKIPPED form was copied from a gate that runs only under data_gates. | Existing lens item (Tooling lens item 9). |
| 5 | LOW | The time the console commands took was charged to the next phase's stall and wall clocks. Fixed: the tick after the commands adds 0. Test `Tick_TheFrameAfterTheCommands_IsNotChargedToTheNextPhase`. (Lens 5.) | Timer | The commands run inside one frame, and that frame's length arrives as the next tick's dt. | No new lesson. |
| 6 | LOW | `FindAccessiblePoint` ran the path query before the cheap checks. The native pathfinder indexes the face table with no bounds check, so an off-mesh candidate could reach it. Fixed: cheap checks first. (Lens 2, Lens 5.) | Native call order | The order looked like an optimisation choice, not a safety one. | Lesson text in finding 1's entry names it. |
| 7 | LOW | The audit used `Position` for settlements that are not towns or castles; the engine uses `GatePosition` for all of them (Settlement.cs:961-967). Same value today. Fixed. (Lens 5, Lens 2.) | Engine parity | Carried from the original audit. | No new lesson. |
| 8 | LOW | Wrong comments: the probe "escapes a small island" (it stays on the input's island, native 0x6DB910); suggestions "pass the same three checks" (finding 2); "the settings vanilla's distance cache uses" (finding 3). Fixed. (Lens 2, Lens 5.) | Engine claims | Written from the method name. | Existing rule (AGENTS.md "Research first"). |
| 9 | LOW | The story printer never printed commands.txt, and a missing commands.txt said nothing. Fixed: both printers call `show_commands`, which names a missing file when the arm file asked for commands. (Tooling 2 and 3, Lens 5 trace 4.) | Output path | Only the campaign printer was edited. | No new lesson. |
| 10 | LOW | The gate's regex passed or crashed on shapes the engine reads differently (a commented-out row first, single quotes, `nan`, `1,5`). No live row has them. Fixed: ElementTree, with a non-number as a finding and a parse error as exit 2. (Tooling 4.) | Parser | A regex matched today's file. | Existing lesson: lessons/xslt-moduledata.md, "A regex of `<tag ` misses `<tag
  attr=...`". |
| 11 | LOW | Tests: `main()` read `sys.argv`; a finding test checked only the count; a refusal test leaked argparse text; nothing pinned `commands.txt` and `command` to the C# names. Fixed. (Tooling 5.) | Tests | | No new lesson. |
| 12 | LOW | Docs: the island-only audit was still described in dev-console.md, settlements.md, taom-map-settlement-naming.md and vanilla-data-comparison.md; scenario-autorun.md and run_scenario.py did not list `command`; the `cheat_mode = 1` need was not stated; `ScenarioPlan.Describe` omitted the commands; the gate said "navmesh island" for entrances on the main island. Fixed. (Lens 5 traces 8, 10, 11; Tooling 6.) | Doc drift | The rewrite edited the code and its own comment only. | Existing rule (feature doc with the change). |
| 13 | LOW | A change to the gate's own tool did not select its data gate. Fixed. (Tooling 7.) | Gate selection | | No new lesson. |
| 14 | LOW | After the finding 1 fix: when no path query succeeded at all, every group was one entrance and the main group was the first in file order, so a village first gave "every entrance is reachable" and a castle first gave almost all NoPath. Fixed: nothing is connected, and the command says no path query succeeded. Test `NoPathSucceedsAnywhere_NothingIsConnected_WhateverComesFirst`. (Lens 5 re-review.) | Degenerate input | The fallback for "no main member to test from" was written for one settlement, not for a failed engine. | Covered by finding 1's lesson. |
| 15 | LOW | An off-mesh entrance can never get a suggestion: the engine returns the input unchanged at every radius and logs 7 native lines. Fixed: off-mesh records are not probed and the report says to move them by hand. The "never another island" comment held only when the probe succeeds; corrected. (Lens 2 re-review.) | Engine claims | | Existing rule (AGENTS.md "Research first"). |

Applied from the Lens 2 re-review's cost note (not a defect): each path query now starts at the entrance under test, so
a cut-off entrance floods only its own pocket instead of the main map. This assumes the navmesh is the same both
ways, as vanilla's one-way neighbour check does (SandBoxNavigationCache.cs:137-140); not proven in the native code.

Decided, not a defect: the gate `map-entrances` fails today, because the live TAOM_Map still holds all seven 2.0.34
values (Lens 5 trace 3). Batch C commits only with the map-editor fix, so the gate ships green with the fix it guards.

## Root-cause pattern

Findings 1 and 2 come from one gap. The audit moved from a property of one face (its island) to a property of the
map (reachability), but kept the per-record structure: each entrance judged on its own from inputs that were
themselves unproven. A reachability answer is only as good as the point it starts from, so the start must be proven
first and the proof must not be circular.

## Why each agent missed these

- The builder (this session) tested the rules with one fake path oracle per test and never with two broken
  entrances side by side.
- Lens 5 found findings 1, 2, 5, 7, 9 and 12; Lens 2 found 3, 6, 7 and 8 and noted 1; the Tooling lens found 4 and
  9 to 13. The Lens 5 re-review found 14; the Lens 2 re-review found 15.
- No unit test fed the audit a pocket, because the original audit had no pure rules to test (dev-console.md said
  so).

## Lessons

- lessons/campaign-mechanics.md: "A reachability check tests from points already shown to reach the main map"
  (findings 1, 2 and 6).
