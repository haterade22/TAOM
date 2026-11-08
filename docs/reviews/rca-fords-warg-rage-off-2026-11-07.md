# RCA: no warg rage in a Fords of War match (/deep-review, 2026-11-07)

## Top-line

The change: `WargTryToGoRage`, the only code that starts a warg rage, reads `FordsOfWarMission.IsActive(Mission.Current)` once when
its tree is built and starts no rage in a Fords match (Mike, 2026-11-07: "When in fords of war, disable the rage mechanic as it is not
needed for this"). Worktree `E:/repos/taom-fords-creatures`, branch `fix/fords-riderless-creatures`, base `6791dbc75`, as the second
of the branch's commits.

- **Tier 1** (a behaviour-tree listener on the mission's hit callback). First wave: Lens 5 Data flow and Lens 2 Engine compatibility
  at `max`, Codex at `ultra`. No CRITICAL or HIGH, so no second wave: Lens 1 Standards, 3 Efficiency, 4 Completeness and 6 Design are
  SKIPPED BY TIER, Lens 7 XML and Tooling are NOT IN SCOPE, and Step 4 is N/A.
- **Result:** 0 CRITICAL, 0 HIGH, 1 MEDIUM, 5 LOW, 0 API incompatibilities (Lens 2: 8 members verified in the installed v1.5.6 DLLs).
- **Gate timing (Lens 2, Lens 5, Codex):** a warg tree is built only at the warg scan one second into the mission or in a later
  `OnAgentBuild`, both after `MissionState.Current` and `Mission.Current` point at the Fords mission, so the field is false for every
  warg there. Every other mission opens under its own name, so rage is unchanged outside Fords.
- **Proof:** FordsOfWar, gate, architecture, warg, co-op seam and behaviour-tree tests: 2,618 passed, 0 failed, 5 skipped.
  `lint_docs.py --fail-on-drift` exit 0. Nothing ran in game.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The rage instrument (`WargRageLog`, the telemetry's warg watch, check 12) is armed only in Fords, where no rage can start now, so it records nothing in any mission, and four doc rows still described live output. (Lens 5) | Dead diagnostics | The change was read as a gate, not as the end of the only thing the instrument measured. | Kept as a tripwire (finding 2), and the four doc rows say it records something only when the gate breaks. Deleting it is the alternative. FOLLOW-UP: no issue: Mike waived issues for Fords of War, 2026-10-31. |
| 2 | LOW | Check 12 graded a rage start as PASS and printed "(field a warg rider)", while the doc said a start means the gate broke; the runner fails only FAIL rows, so a broken gate passed every automated check. (Codex, Lens 5) | Verdict rule | The check was written when a rage was the behaviour under test. | A start now FAILs the row and the hint is gone. The verdict contract gains a `FailOnly` kind (no PASS branch) with its own test, and the clean scenario S1 no longer plays a rage. |
| 3 | LOW | The gate test checked the condition's prefix, not its `return`, and the setter scan read only `Main` and the property spelling, with `Contains` per file. A guard that did nothing, or a second positive write in one of the four files, passed. (Codex, Lens 5) | Test oracle | The tests pinned presence, not the effect. | The test pins the whole statement through `return;`, and the scan lists every write of the count in core and client, property or backing field, with its argument. |
| 4 | LOW | `FordsAgentControl.Take`'s summary still said a rage turns vanilla's controller off during a take. (Lens 5) | Stale comment | The class was not in the edit list. | Reworded: the call is a guard only. |
| 5 | LOW | `warg-combat.md` step 1 said a rage rolls on a hit "from enemy within 10 units"; the code uses 20 m and checks no team. Older than this change, in the section it edits. (Lens 2, Lens 5) | Doc accuracy | The step predates the config table beside it. | Rewritten from `WargConfig` and `Notify`. |
| 6 | LOW | A dead warg's listener stays subscribed until mission end, so each hit walks the list. Older than this change; the saving is under a microsecond per hit. (Lens 5) | Efficiency | Not in scope. | None: the code would not be simpler. |

## Open (UNVERIFIED)

- **In game.** No match has run on this build. The proof is a Fords match with warg riders that writes no `[Warg][Rage]` start line
  and leaves check 12 at NOT SEEN with `started=0`.
- **Berserk.** The Race Abilities "Berserk" still runs in Fords. Mike's request is read as the warg rage only; asked on 2026-11-07.
