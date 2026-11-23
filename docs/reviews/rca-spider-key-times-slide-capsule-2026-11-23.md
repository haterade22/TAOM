# RCA: clip key times, the spider's slide and its body capsule (2026-11-23)

## Top-line

Three faults came out of one day of work on the giant spider (#1550), and none of them was new that day. The
Modding Kit's log named the first when it cooked 13 new clips. The maintainer saw the other two in a custom battle.

| # | Fault | Since | Issue |
|---|---|---|---|
| 1 | Clip records played a key their master does not have: 49 bound clips of the spider, the mumak and the war elephant | 2026-11-19 (#1474), copied from ADOD's elephant clips | #1558 |
| 2 | Spider mounts slid in a charge with still legs | June 2026 | #1559 |
| 3 | Spider mounts ran into each other | June 2026 | #1560 |

All three are fixed in the tools and in the live `LOTRLOME_Armory`, each with a gate that fails on the old state.
No C# game code was wrong. The command trial's spider step gained a measurement, because it had passed while the
spiders slid.

## 1. Key times (#1558)

**Symptom.** `rgl_log_errors_47348.txt` (the Kit start of 2026-11-23, 09:29) held 412 lines for the 13 new clips,
one per bone:

```
spider_hit_front resources' bone(9)[1.00, 37.00] quat ipo does not fit with spider_hit_front anim_clip's sources 0.00 and 37.00
```

The Kit start of 2026-11-22, 18:33, which cooked the 15 clips of #1541, printed the same line 154 times. Nobody
read that log then.

**Root cause.** `tools/elephant_write_masters.py` numbered a new master's keys 1 to N, and
`tools/gen_elephant_anim_clips.ps1` wrote sources 0 to N ("0 and the master's key count, as the Kit-made elephant
clips have them"). ADOD's elephant clips do have that form, and they draw the same warning. The editor accepts a
track only when its first key time is at or under the lower source and its last key time is at or over the upper
one: `FUN_180b8b810` in the wEditor `TaleWorlds.Native.dll`, read on 2026-11-23, lines 126 to 128 of the
decompile. The Kit's own masters key from 0, and a Kit loop's last source key repeats its first: `warg_canter`
holds keys 0 to 17 under sources 1 to 17, and key 17 differs from key 1 by 6e-4 where one step is 5e-2.

In the game a loop stood on its first key for one key time in every loop, about 2% of a stride. `spider_trot` and
its twin had a second form: the writer kept the template master's own key times when the key count matched (30),
so they held keys 0 to 29 under sources 0 to 30.

**Fix.** Frame f sits at key time f. A clip that loops gets one more key, at time N, that repeats frame 0, so
sources 0 to N hold exactly N steps and no loop record changed. A one-shot has n keys at 0 to n-1 and sources 0 to
n-1. `elephant_write_masters.py --rekey loop|shot` brought 56 live masters to that form with their GUIDs kept: 28
spider clips, 20 mumak and war elephant clips, ADOD's bound `elephant_stand_2`, and ADOD's seven unbound elephant
gaits, which the gate's own list names. Nine spider one-shot records took an upper source one lower, in place.

**Proof.** The 14 new spider masters equal the fixed writer's output byte for byte; the 14 of #1541 have the same
key times and values within 1.4e-14. The Kit start of 10:02 left `rgl_log_errors_4232.txt` with no "does not fit"
line. `check_gait_stride.py` reads the key range of 34 bound spider clips and 37 bound elephant clips: 0 findings.
A scan of all 1,019 clip records in the Armory finds no bound clip out of range.

**A defect of the fix, caught before it wrote.** The migration's test for "this loop is already closed" compared
the first and the last rotation by their dot product. The elephant's stored rotations are not unit length to nine
places, so a second dry run planned a second closing key for 19 masters. The test now compares the values
exactly, and a unit test holds a closed loop of such rotations.

## 2. The slide (#1559)

**Symptom.** In a charge the spiders walked, slid with still legs, and stepped again. The step passed.

**Root cause.** The engine lays the clip of a `monster_usage_upper_body_movement` row, and of the set's
`gallop_acceleration_head_action`, over the whole body at a weight of up to 1. The spider's rows played the
turn-in-place actions at every moving pace, and its gallop acceleration action was `act_spider_idle`. The live
file said why: "Spider has no head-turn clips; turn rows use the full-body turn actions, pace-1 rows idle."

**How it was found.** Three hypotheses, one run. A temporary log of each mount's two action channels showed
channel 0 on `act_none` through the whole charge, so the quick stop and the jump start were not it, and channel 1
on `act_spider_idle` at 7 m/s with a weight of up to 1.00. A second run measured the leg tips against the body,
summed over 11 spiders at speeds above 4 m/s:

| Overlay on channel 1 | Mount-seconds | Tip speed against the body |
|---|---|---|
| none | 89.8 | 6.5 to 6.8 m/s |
| idle, weight 0.8 or more | 19.9 | 1.0 to 1.3 m/s |
| turn in place, weight 0.8 or more | 8.6 | 2.1 to 3.8 m/s |

One idea was wrong. The spider's `neck_root_bone` is `spine2_m`, and the two front leg pairs hang under it, so
the overlay might have held only those. The back legs slowed as much as the front ones.

**Fix.** Each moving pace's two overlay rows play that pace's own gait action, as the warg's rows do
(`warg_trot_r`, 0.73 s, beside `warg_trot`, 0.73 s) and the elephant's. The spider's set names no gallop
acceleration action, as the warg's names none. `tools/bind_spider_clips.py` owns both and `--check` holds them.

**Proof.** The step's new `slide` fact: run `20261123-103046` on the unfixed data failed with `slide=3.5`; runs
`20261123-103422` and `20261123-104155` on the fixed data passed with `slide=0.0`. The maintainer's eye is owed.

## 3. The body capsule (#1560)

**Symptom.** One spider's front passed into another's abdomen.

**Root cause.** The `body_capsule` in the live `Monsters/LOTR/lotr_monster_spider.xml` was a pill around the
chest: radius 0.50, y 0.45 to -0.45, so 1.9 m long. On `spider_skeleton` the centre line runs from y 0.57 to y
-2.66 and the hips sit up to 0.66 m from the middle. The numbers were typed in June and never held against the
skeleton.

**Fix.** Radius 0.70, pos1 `0.0, 0.25, 0.90`, pos2 `0.0, -1.95, 0.90`: the maintainer's choice of three sizes.
`tools/verify_mount_assets.py spider` now fails a capsule that stops more than 0.10 m short of the foremost or
the rearmost centre-line bone, or whose radius does not reach the widest hip.

**Proof.** The gate failed on the June capsule with both findings and passes on the new one. Run
`20261123-104155` passed with it. Whether a spider still fits through a gate is not tested.

## 4. What the review of this change found (2026-11-23)

`/deep-review`, tier 1: four lenses (data flow, engine compatibility, XML, tooling) and Codex at `high`. No defect
in the clips. One more fault in the live data, older than the day, and a set of gates that again checked less than
they said. All are fixed in the same change.

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| R1 | MEDIUM (engine lens) | The stand pace listed its `none/none` row first, and the usage loader files a `none` row under every direction and turn and keeps the first row of a key. The spider's turn-in-place and strafe rows of that pace were never stored, since #1541. | The loader had been read for another question; the expansion was two lines above the line that answered it. | The row is last, as the warg's is; `hidden_rows` simulates the loader and fails a hidden row. |
| R2 | MEDIUM (engine lens) | The wider capsule raised the rider's camera from 2.0 to 2.5 m: its height is the adder + pos1 z + radius. | Only the collision use of the capsule was considered. | `rider_camera_height_adder` 0.9 to 0.4. |
| R3 | MEDIUM (XML lens) | `audit_mount_parity.py --strict` still required `gallop_acceleration_head_action`, and the authoring doc said an idle there is fine. `data_gates.py` failed on the spider. | The slide fix changed one gate and not the other that held the same attribute. | The attribute left `STRICT_VERBS`; the doc says never an idle. A test holds the two tools together. |
| R4 | P1 (Codex), MEDIUM (tooling) | The key range gate skipped a bound clip whose master it did not find, and read one track. Two bound elephant clips went unread, their masters beside the mesh. | The first master list was one folder, and "not found" was a `continue`. | Every master under the creature's assets; no master is a finding; every keyed track is held. |
| R5 | P1 (Codex), LOW (data flow) | The slide meter could report 0 after judging nothing (no skeleton, readings far apart, no fast spider), and one slow reading dropped its open window. | The meter was built from the run that showed the fault, where every spider was fast and every tick was read. | A `judged` fact with a floor of 2 s for each rider; a slow reading is left out, and only a halt over 0.2 s ends the window. |
| R6 | P1 (Codex), MEDIUM (tooling) | The capsule gate read the length along Y and the radius: a capsule beside, above or tilted through the body passed, and so did one of numbers that are none. | The gate was written against the one wrong capsule it had met. | Side, height and finite checks; a missing file is a finding. |
| R7 | P1 (Codex) | `--rekey` could not know that a master holds one clip, took "ends on its first pose" for "already closed", and an in-place `--clip` rewrite moved key times under records that name them. | The migration was written for the 56 masters at hand, all of one form. | `--rekey` takes the record's sources and migrates that one form only; an in-place rewrite keeps the master's times. |
| R8 | P1 (Codex), then the design lens | The record writer rewrote a record in place when "only its range differed", having compared nine fields of it. The first fix compared every simple field and skipped the flags, which are a list, and the fields of each clip usage. | The comparison had grown one field at a time; the fix's type filter was not read against the record's own types. | Every simple field, the flags and each usage's fields are compared with a fresh clone of the template. |
| R9 | P1 (Codex) | The binder did not hold a rider row for each overlay action. | The overlay rows reuse June's actions, which are in no table of rows the tool writes. | `planned_problems` requires the row in each rider set. |
| R10 | P1 (Codex), LOW (tooling) | A backup folder equal to the master's folder skipped the backup and replaced the original; the write was not atomic; a folder inside the module was accepted. | The driver always passed a folder on another path. | All three are refused or fixed, with a test. |
| R11 | MEDIUM (data flow) | The capsule gate was not a row of `tools/data_gates.py`. | The gate was added to its tool and the tool was in no row. | Row `spider-body`. |

**The second pass.** Four more lenses (standards, efficiency, completeness, design) and a Codex pass on the fixes.
The lenses found stale doc lines, comment text and one real gap: the R8 comparison skipped the flags and the usage
fields (fixed, in the R8 row). Codex rated R8's and R9's reported inputs fixed and the hidden-row simulation
consistent with the loader as documented, and gave a new failing input for seven of its points:

- Fixed in code, each with a test: the slide meter kept a window open across a long halt, so a run before it was
  weighed with a slide after it (a halt over 0.2 s ends the window now); five judged seconds passed a run in which
  one spider of many was read (the floor is 2 s for each rider); `--resize` kept the old key times when the new key
  count happened to equal the old one; the record comparison compared floats as text; a track whose key times are
  no numbers hid behind the other tracks.
- Closed by saying less: the capsule gate does not prove that every bone is inside the capsule, and the abdomen's
  tip is 0.22 m outside the rear cap of the capsule the maintainer chose. Its docstring and the feature doc now
  name its four checks. `--rekey` cannot see a second record on the same master; its sources are mandatory now and
  its docstring says they are the caller's word.
- Left, with the reason: no test drives `check_gait_stride.main` or the trial host's sampling call, so removing
  the range check from `main`, or the meter call from the host, fails no unit test. The proof of both is the
  live gate run and the custom battle (`judged=147.4`).
  FOLLOW-UP: no issue: both need a fixture of real packages or the game; the runs are quoted in the commit.

The review stops here. Each fix of the second pass is a few lines with its own test, or a narrower claim; none
changes what the game loads.

R4 to R10 are the pattern of the review of #1541 (`rca-spider-locomotion-2026-11-22.md`): a gate built to pass
on the state the work had just made. This is its second sighting in two days in the same tools.

## Root-cause pattern: a number copied or typed where a rule could be read

- The source range was copied from ADOD's clips "as the Kit-made elephant clips have them". ADOD's clips were
  wrong in the same way, and the editor's rule was one decompile away.
- The overlay rows were filled with the nearest action that existed, with a comment that recorded the gap. The
  warg's rows, in the same file, showed the convention.
- The capsule was typed. The skeleton it must hold was on disk.

Each gate that existed checked what the work had just made (a stride, a binding, a tag) and not the neighbouring
fact a reader of the data would need. The three new gates read the rule from its owner: the editor's own
condition, the pace's own gait, the skeleton's own bones.

## Why it was missed

- The Kit's error log is not read after a cook. The first cook of #1541 held the warning 154 times.
- The spider step judged speeds. A sliding spider is as fast as a running one.
- Stills from above cannot show a gait, and the maintainer's first watched run was on 2026-11-23.

## Not fixed here

- The June `an_spi_idle` record (keys 1 to 43, sources 0 to 41) is out of range and unbound.
  FOLLOW-UP: no issue: no action set binds it, and the gate judges bound clips.
- The chariot's two backward-pace overlay rows play a 1.2 s clip beside a 0.8 s gait.
  FOLLOW-UP: no issue: nobody has reported a slide on the chariot, and its head clips are its own; noted in #1559.
- Reading the Kit's error log after every cook is a habit, not a gate.
  FOLLOW-UP: no issue: the key range gate now fails the one fault that log showed, before the Kit runs.
- The attacks and the rider's clips still play June takes (#1550, slices B and C).
- The unit of the turn records is still not read (#1550): two ways to read it failed, and they are in
  `docs/features/spider.md`.
- Three simplifications of older code from the design lens: one master index by GUID for the stride, stand and
  range checks of `check_gait_stride.py`; one apply path and one item lookup in `elephant_write_masters.py`, so that
  `--clip --apply` also writes through a temp file; `stand_findings` on `played_keys`.
  FOLLOW-UP: no issue: each changes what an older gate reads on the elephant, and none is a defect; the list is here.

## Owed in game

A person's eye on: a spider charge (no slide), a spider that turns in place and one that steps sideways from a
stand (the rows that were hidden), the rider's camera on a spider (unchanged at 2.0 m), spiders side by side (no
bodies inside each other), a slow step back on the backward walk's own twin, the spider in the party screen and
the encyclopedia, a spider at a gate or a narrow pass with the wider capsule, a hit, a death and its held last pose, a hard stop, a jump, and the
mumak's and the war elephant's gaits after the key time change.
