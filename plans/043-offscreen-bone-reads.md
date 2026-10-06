# Plan 043: Find out whether TAOM's own bone reads see a frozen pose off the screen, and force the pose where they do

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in a worktree on its own branch (`fix/739-offscreen-bone-reads`),
> never in the main checkout. The orchestrator keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**:
> `git diff --stat f7e9bd0d..HEAD -- Main/Features/AdvancedCombat/BoneCheck.cs Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs Main/Features/Elephant/TaomHowdahMachine.cs Main/Features/Mumakil/TaomMumakilPlatform.cs Main/Adapters/AgentAdapter.cs TAOM.Tests/Features/AdvancedCombat`
> If an in-scope file changed, compare the "Current state" excerpts with the live code; a mismatch is
> a STOP condition. Re-find anchors by their text, not their line numbers.
>
> Also run `cat .claude/pinned-game-version.txt` (expect `v1.5.4`) and
> `pwsh tools/taom-src.ps1 path TaleWorlds.Engine.Skeleton`, then grep the printed file for
> `public void ForceUpdateBoneFrames()`. Missing is a STOP.

## Status

- **Priority**: P2
- **Effort**: S to M (one shared helper, two call sites, one diagnostic, tests, one in-game session)
- **Risk**: LOW (forcing the pose is the evaluation vanilla's own melee sweep forces; where the
  skeleton was already posed this frame the call returns at once)
- **Depends on**: none
- **Category**: bug / creature combat / elephant
- **Planned at**: commit `f7e9bd0d`, 2026-10-06
- **Baseline**: not measured by the writer; Step 1 records it.
- **Issue**: #739

## Why this matters

yotthani decompiled the engine's skeleton tick (MithrilForge `docs/engine/offscreen-pose.md`,
Bannerlord v1.5.3):

- An agent's skeleton is posed only while the agent is visible in at least one view (shadow views
  count; rider and mount count as one).
- For an agent nobody sees, the renderer advances the animation time and action state but not the
  bones, except for an occasional timer-based update (`do_timer_based_skeleton_forced_updates`,
  on by default).
- In between, `Skeleton.GetBoneEntitialFrameWithIndex` returns the same frame tick after tick, while
  `Agent.GetCurrentActionProgress` keeps moving.
- Vanilla's melee sweep forces the attacker's pose before reading it, so vanilla is fine. Code that
  reads bones itself is not.

The lever is `Skeleton.ForceUpdateBoneFrames()` (native `enm_IMono_Skeleton_force_update_bone_frames`).
On v1.5.3 its first instruction tests a "posed this frame" bit and returns if set; otherwise it runs
the same pose evaluation the vanilla sweep uses, at the time the renderer already advanced to. Do
not use `TickAnimationsAndForceUpdate`: it advances the animation a second time.

DualWield hit this exact bug: 26 of 87 off-screen left-hand strikes had a tip that barely moved and
made no contact.

On v1.5.4, nothing here has been re-checked and nobody has seen the symptom in TAOM. Phase A
therefore measures before Phase B changes behaviour.

## Current state (at `f7e9bd0d`)

| Site | Reads | Effect if stale |
|---|---|---|
| `Main/Features/AdvancedCombat/BoneCheck.cs` `CheckBoneCollision` (`agentSkeleton.GetBoneEntitialFrameWithIndex(bone)`) | The attacker's strike bones, every tick inside the hit window | A bite tests a frozen jaw |
| `BoneCheck.cs` `FindBoneInRange` (`targetSkeleton.GetBoneEntitialFrameWithIndex((sbyte)i)`) | Every bone of each target inside the range gate | A target off the screen is tested at an old pose |
| `Main/Features/Elephant/TaomHowdahMachine.cs` `TryReadAnchorBoneWorld` | `Spine1_05`. Used live by the spine-height branch of `RepositionToFixedOffset`; the full-frame path is off (`BoneTrackingEnabled = false`) | Howdah height stops following the bob; crew sink or float, then jump |
| `Main/Features/Mumakil/TaomMumakilPlatform.cs` `BoneProbe` | The spine bone, for the status log line only | Wrong diagnostic; no gameplay effect. **Out of scope.** |

Custom attacks reach `BoneCheck` only through `AgentAdapter.CustomAttack` → `BoneCheckDuringAnimation`,
and the only caller of `CustomAttack` today is `Main/Features/Warg/WargAttackService.cs`. So the
combat half of this is warg bites.

`BoneCheckDuringAnimation.Tick` fetches the attacker's skeleton once per tick inside the window and
passes it to `CheckBoneCollision`; keep that (the `GetSkeleton` wrapper cost is #659's fix).

## Steps

### Step 1: Baseline

`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` and record the result line. Known
failures on trunk are not this plan's; note them and move on.

### Phase A: measure (no behaviour change)

**Step A1. A pure staleness rule, test first.** Add `internal static class BonePoseStaleness` in
`Main/Features/AdvancedCombat/`, with one method. It returns true when the bone origin this tick
equals the one last tick (exact float equality on x, y, z) **and** the action progress moved by more
than a small epsilon. NaN in any input returns false. Tests in
`TAOM.Tests/Features/AdvancedCombat/BonePoseStalenessTests.cs` cover:

- same origin with progress moved → stale;
- origin moved → not stale;
- progress not moved → not stale (a held pose is legitimate);
- NaN → not stale.

Red, then green.

**Step A2. Count, don't change.**

- In `CheckBoneCollision`, keep the first strike bone's agent-local origin and the action progress
  from the previous tick on the `BoneCheck` instance. `BoneCheckDuringAnimation` passes the progress
  in; add a parameter, don't read it twice.
- Count ticks inside the window and stale ticks.
- On expiry, log one line through the existing logger:
  `[BoneCheck] pose agent=<name> ticks=<n> stale=<m>`.
- The howdah does the same in `TryReadAnchorBoneWorld`, against `HowdahSeatMotion`'s existing status
  cadence. It uses the elephant's movement as "progress": stale when the bone is unchanged while the
  agent position moved more than 1 cm. Log the counts on the existing status line.

Keep the counters as plain fields; no allocation per tick.

**Step A3. Build, deploy only on Mike's word, and hand him this check:**

1. Custom Battle with a warg pack against infantry. Watch one fight for 30 s, then look straight up
   at the sky for 30 s.
2. Same battle with an elephant and archers aboard. Follow it, then turn the camera fully away for
   30 s.
3. Send `taom_debug` logs.

**Step A4. Read the counts and report to the orchestrator. STOP here.** Decision rule for Phase B:

- **Off-screen stale share at or above 10% while on-screen is near 0:** confirmed. Do Phase B for
  that site.
- **Both near 0:** the timer update covers it on v1.5.4. Remove the diagnostic, record the result in
  the issue, close #739 as not reproduced, and skip Phase B.

### Phase B: fix (only for the sites Phase A confirmed)

**Step B1. Force the pose before the read.** One helper on the existing bone-read path:
`static void EnsurePosed(Skeleton s)`, which calls `s.ForceUpdateBoneFrames()`. Call it:

- once per tick on the attacker skeleton in `CheckBoneCollision` before the bone loop;
- once per in-range target in `CheckTargets` after the range gate and before `FindBoneInRange`, so
  out-of-range targets pay nothing;
- once in `TryReadAnchorBoneWorld` before `GetBoneEntitialFrameWithIndex`.

Call it unconditionally rather than only after a stale read. The native call returns at once on a
skeleton already posed this frame. The upstream alternative (read, compare, force, read again)
saves nothing on a visible agent and doubles the read on an invisible one.

**Step B2. Keep the Phase A counters one more session.** They should now read 0 stale off screen.
Then delete the counters and the `BonePoseStaleness` class and its tests, unless they found a
second use. A diagnostic that can never fire again is dead code.

**Step B3. Docs.**

- Add a section on off-screen poses to `docs/reference/engine/agent-spawn-and-render-pipeline.md`.
  It covers the posed-only-when-visible rule, the lever, `v1.5.3 decompile, v1.5.4 confirmed in game
  (#739)`, and the credit to yotthani and MithrilForge.
- Add one line to the "AdvancedCombat" notes in `docs/features/` for the warg and the elephant.
- Add a lesson in `docs/reviews/lessons/adapters-taleworlds-api.md`: "A bone read outside vanilla's
  sweep forces the pose first."

**Step B4. Verify.**

- Run the full suite and quote the result line.
- Run `/deep-review` on the diff.
- Hand Mike the in-game re-check: Step A3 again, expecting 0 stale and wargs landing bites off
  screen.

## STOP conditions

- `ForceUpdateBoneFrames` is missing from the installed `Skeleton`, or its signature differs.
- A "Current state" excerpt no longer matches the code.
- Phase A finished: always stop and report before Phase B.
- Forcing the pose changes on-screen behaviour (a howdah or bite that looked right now looks wrong).
- Any native AV or hang in a session with Phase B: revert, run `/native-crash-triage`.

## Out of scope

- `TaomMumakilPlatform.BoneProbe`, which is diagnostic only. If the mumakil platform ever reads bones
  for placement, apply the same helper.
- The animation LOD, where far agents update at 18 to 21 Hz. That is a coarser pose, not a frozen
  one; measure separately if bites at range look wrong.
- `RayCastForGivenAgentsLimbs`, a separate MithrilForge finding.
