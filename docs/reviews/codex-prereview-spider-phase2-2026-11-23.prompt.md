# Codex pre-review: spider phase 2, clip key times, the slide and the body capsule (2026-11-23)

You are an independent, adversarial reviewer. Read AGENTS.md and `.ai/roles/reviewer.md` first. Review only; change
no file. Report findings as P1 (wrong result, data loss, or a gate that passes a wrong state), P2, P3, each with
file, line, the failing input and the evidence you read. Missing engine evidence stays UNVERIFIED.

## Scope

Uncommitted work in this checkout (`git status`, `git diff`, base `435e5644a`), branch `feat/spider-phase2`.
Issues #1550 (slice A), #1558, #1559, #1560. The incident report is
`docs/reviews/rca-spider-key-times-slide-capsule-2026-11-23.md`.

Scripts and their tests:

- `tools/elephant_write_masters.py`: key times from 0, `close_loop`, `rekey`, the `--rekey` mode.
- `tools/gen_elephant_anim_clips.ps1`: `Source2` is the last key time, the keys expectation, the in-place rewrite
  of a record that differs only in its range or limits, `-BackupDir`, `-Templates` as a folder list.
- `tools/check_gait_stride.py`: `range_finding`, `played_keys`, `range_findings`, their use in `main`.
- `tools/spider_gait.py`: the `keyed` and `hold` kinds, `pose_at`, `keyed_motion`, `keyed_target`, the 13 new clips,
  the `loop` and `keys` fields.
- `tools/bind_spider_clips.py`: the new `REBIND` rows, the ninth `NEW` action, `OVERLAYS`, `HEAD_ACTION`,
  `_spider_usage`, `_block`, `plan_usage`, the overlay part of `planned_problems`.
- `tools/verify_mount_assets.py`: `read_body_capsule`, `capsule_findings`, CHECK 4.
- Tests under `tools/tests/`: `test_elephant_gait_tools.py`, `test_spider_gait.py`, `test_bind_spider_clips.py`,
  `test_verify_mount_assets.py`.

C#:

- `Main/Features/Spider/SpiderSlideMeter.cs`, `SpiderTrialRules.cs`.
- `Client/Features/Spider/Trial/CommandTrialHost.Spider.cs`, `CommandTrialScript.Spider.cs`.
- Tests under `TAOM.Tests/Features/Spider/`.

Data: `docs/reference/lotrlome-armory-snapshot/action_types.xml`, `action_sets.xml`, `monster_usage_sets.xml` (the
live Armory copies are equal to these; `python tools/bind_spider_clips.py --check` proves it on this machine).

## Questions to attack

1. `rekey`: can a second run change a master again, or can it leave a loop open? Which masters would it corrupt
   (several clips in one master, tracks with different key counts, position keys)? Is the "already closed" test
   right for a one-shot whose last pose equals its first (a hit ends on the stand pose) when someone passes `loop`?
2. `new_payload` now never keeps the old master's key times. Which caller relied on that (an in-place rewrite of a
   master whose records play a sub-range)?
3. `gen_elephant_anim_clips.ps1`: a report row with no `keys` field is read as a loop. Which existing report or
   role breaks? Does the in-place rewrite ever touch a record that also differs in something else? Is the strafe's
   `Duration` right with one more key?
4. `check_gait_stride.py`: `played_keys` drops the key at the upper source. Is that wrong for a record that plays a
   sub-range of a long master, or for a one-shot? Can `range_findings` pass a clip it could not read?
5. `bind_spider_clips.py`: `plan_usage` edits the opening tag with a regex and replaces two blocks. Find an input
   the live file could hold (an attribute on the tag's last line, a comment between attributes, a second spider
   set, a self-closing block) that it corrupts or silently skips. Is every overlay action bound in `as_spider` and
   in both rider sets? Does `--check` fail when one overlay row is missing?
6. The overlay rows reuse movement actions (`act_spider_walk_forward` on channel 1). Name any engine or rider-side
   effect of one action on a movement row and on an overlay row at once. UNVERIFIED is an acceptable answer; say
   what would verify it.
7. `SpiderSlideMeter` and the host: can a healthy gait be counted as a slide (a rider in a melee, a mount that
   turns in place at speed, a low frame rate, a paused mission)? Can a real slide be missed (the window reset)?
   Is reading `GetBoneEntitialFrameWithIndex` for eight bones of each mount in the mission tick safe on an agent
   whose visuals are gone? `IsLive` guards the mount before the call.
8. `capsule_findings`: does it pass a capsule that does not hold the body (a capsule off the centre, a tilted
   one, a skeleton whose forward axis is not Y)? Is the regex reader of the monster file safe on the live file?
9. Tests: name each new test that would still pass with its production code deleted or inverted.

## Verification you may run (none deploys)

`python -m unittest tools.tests.test_elephant_gait_tools tools.tests.test_spider_gait tools.tests.test_bind_spider_clips tools.tests.test_verify_mount_assets`
and `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~TAOM.Tests.Features.Spider"`.
Never run `./build.ps1`, and never pass `--apply` to any tool.
