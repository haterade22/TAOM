# Codex pre-review: no warg rage in a Fords of War match (2026-11-07)

Your role is reviewer ([.ai/roles/reviewer.md](../../.ai/roles/reviewer.md)). This is a review only:

- Do not edit, create or delete any file. Do not stage, commit or stash.
- Do not run `dotnet build`, `dotnet test` or `build.ps1`. A Bannerlord match runs on this machine. The orchestrator ran the FordsOfWar, gate, architecture and warg tests after the change: 2,587 passed, 0 failed, 5 skipped.
- Read files, `git diff`, and the installed engine's decompiled sources: `pwsh tools/taom-src.ps1 path <Type>`, one type at a time.

## The request (Mike, 2026-11-07)

"When in fords of war, disable the rage mechanic as it is not needed for this".

## The change (worktree `E:/repos/taom-fords-creatures`, branch `fix/fords-riderless-creatures`, uncommitted)

The worktree holds two changes. Review only this one; the riderless sweep (`RiderlessMountRule`, `FordsBattlefieldAdapter.RemoveRiderlessMounts`, `tools/fords_match_review.py`) was reviewed already.

- `Main/Features/Warg/BehaviorTreeElements/WargTryToGoRage.cs`: a field `_rageAllowed = !FordsOfWarMission.IsActive(Mission.Current)`, set when the listener is built with the warg's tree (`WargBehaviorTree.BuildTree`, from `WargMissionBehavior.TryAttachWargTree` on `OnAgentBuild` and its start pass); `Notify` returns at once when it is false.
- `TAOM.Tests/Features/Warg/WargRageFordsTests.cs`: source reads, and a scan that `RageAttackAmount.SetValue(` appears only in the four warg tree files.
- Docs: `docs/features/warg-combat.md` "Rage Mode Flow", `docs/features/fords-of-war.md` check 12 and the soldier-control note.

## Questions (file:line evidence; missing engine evidence is UNVERIFIED)

1. Is `WargTryToGoRage.Notify` the only way a rage starts, for AI wargs and for a warg the player rides (Fords soldier control, `FordsAgentControl`)? Look for any other path: the rider-hand manager, the autonomous movement controller, `PrepareOnFirstCall`, `RageAttackStartTime`, `AgentHitBy`, `FirstAttack`, or another feature.
2. When is the listener constructed: does `BehaviorTreeAgentComponent` build the tree at attach time, inside the mission, so that `Mission.Current` and `MissionState.Current.CurrentMission` are the Fords mission? Can a tree be built before the mission state is current (a warg spawned in `AfterStart`, a deployment pass), so that the field reads false in a Fords match? Is any tree reused across missions (`BTRegister` keeps the first factory per process)?
3. Outside Fords, does anything change (campaign battles, custom battles, the Long Defeat)?
4. With no rage in Fords, does any Fords code now misbehave: check 12 (`FordsVerdicts.Combat.cs` `Check12`), `WargRageLog`, `FordsAgentControl`, the round robin's verdict reading of NOT SEEN?
5. Anything else wrong in this part of the diff.

## Output

Per finding: severity (CRITICAL, HIGH, MEDIUM, LOW), location, failure scenario, evidence. Then a one-line verdict per question.
