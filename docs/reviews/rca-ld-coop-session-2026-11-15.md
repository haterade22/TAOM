# RCA: The Long Defeat co-op through a Coop session, review of the #1280 slice (2026-11-15)

Scope: TAOM worktree `E:\repos\taom-ld-coop`, branch `feat/1280-ld-coop-session`, uncommitted, on top of trunk.
The slice adds a co-op run of The Long Defeat over BannerlordCoop's campaign-free session API (Coop #4694): TAOM's
`Main/Features/LongDefeat/Coop/`, the follower run and the run host's co-op path, and in `TAOM.CoopCompat` the
session transport, its stub for an older Coop and the session link. Review: `/deep-review` tier 1 (co-op authority).
Wave 1: data flow and engine compatibility. Wave 2 (the wave 1 HIGH): XML, completeness, standards and design at
effort high, then efficiency. Codex: dispatched, ended on its usage limit before any finding
(`raw/codex-prereview-ld-coop-session-2026-11-15.md`); the pass is owed before merge.

## Findings and root causes

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | HIGH | In a Coop session every defender TAOM spawned went to Coop's `DefenderAllyTeam`. The host's run counted no garrison and ended `DefendersDead` when its muster ended; the host could not take a soldier, and a follower's lord stood on the ally team. | Engine team choice | Coop's `CoopPlayerAllyTeamPatch` adds an ally team while its spawn gate is on, and `Mission.GetAgentTeam` (v1.5.7 `Mission.cs:5229-5233`) gives a plain `BasicBattleAgentOrigin` the player's ally team when one exists. The spawn helper relied on the engine's default team, which was right only while the mission had one team a side. The design read Coop's session code, not the mission patches its gate turns on. | `Spawn` sets the team, its colours and the team's own formation when the engine picked another team; `LongDefeatCoopAllyTeamPinTests` pins it. Lesson in `adapters-taleworlds-api.md`. The two-client run is the proof owed. |
| F2 | HIGH | The default build failed (CS0234, CS0246): `TAOM.CoopCompat.Coop` compiled the session transport against the installed Coop, which has no session API. | Build against a dependency | Every build and test in the slice passed `-p:CoopBinDir` for the session Coop build; nobody built with the defaults that `build.ps1` and a contributor use. | The project builds `CoopSessionTransport.NoSessionApi.cs` when `Missions.Sessions.dll` is absent; both builds are run in `/verify` for this change. Lesson in `build-tooling-workflow.md`. |
| F3 | MED | The player's own exits (the end panel's Leave, the escape menu, a retreat, a fault) never closed the session or reported the run, so followers saw a wrong "The host left" after a natural end and nothing after an abandoned one. | Lifecycle exit paths | The co-op leave was wired into one exit (`LongDefeatRunHost.Leave`) while the mission has five; `ShutDown` cleared the director before the result existed. | Every exit passes `ShutDown`, which reports the run and then ends the co-op side (`LongDefeatCoopRuns.MissionEnded`). Tests for an early end on the host and on a follower. |
| F4 | MED | `LongDefeatSessionPayload.TryDecode` could throw on network bytes with a bad string length (`FormatException`, `IOException`). | Untrusted input | The decoder caught only `EndOfStreamException`, the one failure the truncation test produced. | Catches `IOException` and `FormatException`; `TryDecode_RefusesABadStringLength` was RED first. |
| F5 | MED | The game-thread marshal, the host's class question, the speed lock reset, the follower notice values and a failed start after an open were untested; the feature map and the CoopCompat doc were stale. | Test and doc coverage | The slice's tests were written per class; the seams between them had none. | Tests added for each (`CoopSessionApiTests`, `LongDefeatSetupPromptTests`, `LongDefeatSessionTests`, `LongDefeatCoopRunsTests`); both docs updated. |
| F6 | LOW | Dead plumbing (`LongDefeatCoopParts.Leave`, a second report latch, duplicate menu constants, a password parameter nobody set), a TaleWorlds type in `LongDefeatCoopState`, a comment with the wrong `MissionLogic` order, and comments the change made false. | Simplicity, ADR-007, ADR-009 | Parts were added for a design that changed during the slice and not removed after. | Deleted or moved; `MissionLogic` order corrected from `MissionState.cs:281-286`. Test-name renames (`MethodName_State_Expected`) not applied: LOW, naming only. |

Efficiency (lens 3): no finding.

## Lessons

- A spawn helper that relies on the engine's default team is right only while each side has one team. When a
  mission can carry another module's patches, set the team on the build data and read `GetAgentTeam` first.
- When a design uses a dependency's API, read what that API turns on in the shared mission (its gates and the
  patches behind them), not only the API itself.
- A change that builds against a newer dependency build must also build with the defaults; run both before review.
- Wire a co-op end into the one method every exit passes, then list each exit and prove it reaches that method.

## Fixes

All uncommitted on `feat/1280-ld-coop-session`, test first where a test could fail; re-checked by a convergence
pass of the engine and data flow lenses. Follow-up: #1292 (host handoff, wave and shop messages, a mounted
follower, the won or lost notice, co-op runs in the best-run store). A live two-client session run is owed.
