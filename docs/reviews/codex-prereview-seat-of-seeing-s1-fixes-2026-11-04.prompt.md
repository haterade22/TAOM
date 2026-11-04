# Codex re-review: Seat of Seeing, stage S1, the review fixes, 2026-11-04

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Work only from this worktree and the engine cache: do
not search the rest of the disk. The whole of S1 is still uncommitted on top of `a8aac6399` (`git diff`
and `git status`), so the fixes have no diff of their own: judge them from the code named below. The
contract is `docs/features/seat-of-seeing/build-spec.md`: sections 2 to 5 are built, and sections 6 to 12
are later stages, so their absence is not a finding. Deviations row 12 lists what the review changed.

Engine source for v1.5.6 is in `C:\Users\mikew\.taom-src\v1.5.6\` (for example
`TaleWorlds.MountAndBlade.View.Screens.MissionScreen.cs`); `pwsh tools/taom-src.ps1 path <FullTypeName>`
decompiles a type that is not cached yet. Mark anything you cannot prove from code as UNVERIFIED. Nothing
here has run in the game. Your earlier report on S1 is at the end of
`docs/reviews/raw/codex-prereview-seat-of-seeing-s1-2026-11-03.md` (from line 26156). Do not repeat a
finding from it unless the fix did not close it or the fix made it worse.

## What changed since that report

1. **Warg swap.** `AutonomousMovementPlayerController.SwitchMainAgentController`: the swap back now runs only
   after a swap in (`else if (HoldsHero)`). `SeatScreenAdapter.RageHoldsHero` also needs a mounted main agent.
2. **Stand fast.** Writes to the hero run only from `SeatSession.OnMissionTick`. `Enter` and
   `OnMainAgentChanged` set a volatile flag. `SeatHeroAdapter` has no queue and no pump.
3. **Leave.** `WriteLook` writes bearing and elevation on every leave except `Finish`. `BeginGlide` takes no
   arguments and does not call the listener when `GetSpectatingData(...).AgentToFollow` is null.
4. **Teardown.** `SeatView.OnRemoveBehavior` also calls `Finish`, which is safe to call twice.
5. **Battle start.** `SeatView.OnMissionModeChange` stores the Deployment to Battle edge, and the session
   consumes it at the next screen tick. The per-frame facts read and the latch are gone.
6. **Height limit.** The Seat honours `camera_height_limiter`, read once at `Enter`: `SeatCameraMath.CapFlight`,
   `CapHeight`, and `AboveGround` still last.
7. **Ground.** `BodyFlags.CommonCollisionExcludeFlags`, no `NoGround`; `ClampToBoundary` results pass
   `FiniteFloatValidator`. `SuppressSpectatorCyclingThisFrame` is gone.
8. **Keys and text.** Key 530 defaults to `Q`. The contract narrows the input claim: the drop key, the career key and
   the perspective key still act. From first person the return is at once.
9. **Check lines.** Refused key presses and the reason for a leave write lines (contract 5.10), and the manifest is
   `tools/ingame_checks/0.toml`.

## Questions to answer with evidence

- **Does each fix close its defect?** For the warg swap, trace every caller of `SwitchMainAgentController` and
  every writer of the vanilla controller's `_activated`, and say whether the Seat's input exclusion now holds.
  Does `else if (HoldsHero)` change any behaviour outside the Seat, including a mission in which the swap in ran before
  this field existed in the flow, or a mission restart?
- **Stand fast timing.** Is there any path where the Seat is up and the flag is never consumed? Is there a frame
  in which vanilla's last input still moves the hero, and does it matter? Does a main agent change during Rising work?
- **Leave.** `MissionScreen.GetSpectatingData` is public and also runs inside the listener. Does the public call have any
  side effect (spectator cycling, `LastFollowedAgent`, a consumed request)? Can the answer differ between that call
  and the one inside the listener in the same frame, and what happens if it does? Is it right to write the look in
  a non-Battle mode, a conversation for example?
- **Teardown.** `Finish` can now run from `OnRemoveBehavior` during `Mission.OnMissionStateFinalize`. Does anything it
  calls (`RageHoldsHero`, `GetMissionBehavior`, `MissionMainAgentController.Enable`, `Mission.OnMainAgentChanged` removal,
  `IOutcomeLog`) misbehave at that point? Can `OnMissionScreenTick`, `UpdateOverridenCamera` or `OnMissionTick` run after `Finish`?
- **Battle start.** Is `OnMissionModeChange` raised before the first screen tick of Battle in every Deployment path? What
  happens when `Mission.Mode` went to Battle with `atStart` true? Can a stored edge fire a stale entry?
- **Height limit.** Is the order right on a scene whose limit is below the ground under the camera, and during the entry blend?
  Can `CapFlight` fight `SeatCameraMath.Step`'s clamp, so that the wheel or lift behaves oddly at the limit?
- **New defects.** Read the changed files whole: `Client/Features/SeatOfSeeing/SeatSession.cs`, `SeatView.cs`, `SeatCameraMath.cs`,
  `Client/Adapters/SeatScreenAdapter.cs`, `Main/Adapters/SeatHeroAdapter.cs`, `ISeatScreenAdapter.cs`, `NullSeatScreenAdapter.cs`.
- **Key choice.** Does anything in the engine, or in TAOM, read `Q` on the mission scene layer? Check the dump and the repo.
- **Manifest.** Does `tools/ingame_checks/0.toml` match what the session writes, line by line?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the proving code
or a reproduction. Say PASS for a question you checked and found sound.
