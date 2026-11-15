# Codex pre-review: Long Defeat co-op through a Coop session (TAOM #1280)

You are an adversarial senior reviewer. Report defects only, with file:line evidence. Do not edit files.

## Scope

Repository: `E:\repos\taom-ld-coop` (branch `feat/1280-ld-coop-session`, from `origin/bannerlord-1.5.x`).
Review every uncommitted change: `git status --short`, `git diff`, and the untracked files it lists.
Ignore `docs/reviews/` and test results.

The Coop side it calls is at `E:\repos\coop-ld-session` (branch `feat/campaign-free-session`, Coop draft PR #4694).
Read, do not review: `source/GameInterface/Services/Sessions/SessionLauncher.cs`, its `Messages/` folder,
`source/Missions/Sessions/` and `source/Coop.Core/Client/Services/Sessions/`.

## What the change does

- A second main-menu option hosts or joins a Long Defeat run inside a Coop session.
- The host's setup is a payload (`Main/Features/LongDefeat/Coop/LongDefeatSessionPayload.cs`); every member opens the same mission from it.
- Coop elects the host (first member whose mission is ready). TAOM sets its role from Coop's host-change event
  (`LongDefeatCoopRuns.OnHostChanged`), never from who pressed Host.
- `LongDefeatRunHost.StartForRole`: Pending waits (60 s limit), Host runs the director, Follower runs `LongDefeatFollowerRun`
  (boot, own lord on foot, finish initial spawn; no HUD, no shop).
- Speed locked at 1 in a co-op run; only the host may take control of another agent.
- `LongDefeatBattlefieldAdapter.Classify`: a non-defender-team death is not a garrison loss.
- The host reports the run end over Coop's session channel (`CoopCompatLdRunEnded`); followers show it.
- `TAOM.dll` names no Coop type. Coop-typed code is in `CoopCompat/Coop/` (a second assembly); the main CoopCompat DLL
  holds `Sessions/LongDefeatSessionLink.cs` and starts the transport from `CoopCompatSubModule.OnBeforeInitialModuleScreenSetAsRoot`.

## Known suspects (check each, say CONFIRMED / REFUTED with evidence)

1. A follower runs TAOM mission behaviours written for one player (morale, hook state, HUD views, the autorun, the
   `OnInitialSpawnCompleted` path). Does any of them act as authority on a follower, or throw?
2. Thread and order: Coop raises events off the game thread; `CoopSessionTransport` marshals with `GameThread.RunSafe`.
   Can `HostChanged` arrive before `MissionOpenRequested`, and does `LongDefeatCoopState` handle it?
3. Epoch and session checks in `LongDefeatCoopRuns`: a stale host change, a run-ended message from an older epoch or another
   session, a double end, a Leave while the host's `Close` is pending.
4. The host reports its end once (`LongDefeatRunHost.ReportEnd`). Any path that ends the run without a report, or twice?
5. Payload decode: truncated, oversized, wrong version, trailing bytes, nullable fields.
6. The ally-team guard in `Classify`: can a defender death be misread as an ally death, or the reverse, in a solo run?
7. `CoopCompatSubModule`: the session start runs once; a missing Coop leaves the module working; the weak broker
   subscriptions are held by fields.
8. The launcher at 144 lines (limit 145): does `LongDefeatCoopMission.For` change solo behaviour in any way?

## Output

Group findings CRITICAL, HIGH, MEDIUM, LOW. For each: file:line, the defect, a concrete failure scenario, and a fix.
Mark anything you could not prove from the code as UNVERIFIED.
