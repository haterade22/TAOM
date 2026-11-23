# LOTRLOME_Armory changes for the warg's canter and gallop (#1563, 2026-11-23)

The warg's clip records live in the external `LOTRLOME_Armory` module, which is not in this repository. This
ledger records the seven records changed there on 2026-11-23, why, how to check them and how to roll back. The
fell warg plays the same action set (`as_warg`), so it changes with the warg. The dire wolf has its own records
and is not touched.

## The report and the cause

Players reported that AI warg riders "walk everywhere". Two unattended custom battles showed the wargs at full
speed (9.4 m/s beside horsemen at 11.1 in run `20261123-143304`, and 9.8 beside 10.3 in run `20261123-143843`),
and a recording of a charge showed the legs out of step with that speed. The cause was in the
clip records.

The mount's movement node picks a pace from its speed against the pace limits of the current pace's front
record, and takes the pace's loop time from two records' `Duration`
([elephant.md](../features/elephant.md), "What the engine does with a clip record"). The review of this change
read the node again on TaleWorlds.Native.dll v1.5.7 (2026-11-23: loop time 0x749B10, pace choice 0x74E890, row
loader 0x621E40) and confirmed both, and that the in-place twin (`warg_canter_stand`,
`warg_forward_gallop_left_foot_stand`) is the "none" clip of the warg's canter and gallop.

One step is not read on v1.5.7: that the node's speed is the agent's divided by its scale. That division was
read on v1.5.6 (#1081), and the writer of the node's speed was not found on v1.5.7. `warg_brown` has
`body_length="110"` and the trial read a scale of 1.10, so with the division a limit of 8.0 is 8.8 m/s on the
field. Without it every field speed below is 10% lower and the paws are 10% off; nothing else changes. Before
the change:

| Pace | Record | Master it played | Paws cover a loop | Record claimed | Loop time |
|---|---|---|---|---|---|
| walk | `warg_walkfast` | `Warg_Walk` | 1.80 m | 1.6 m | 1.10 s |
| trot | `warg_trot` | `Warg_Trot` | 2.71 m | 2.9 m | 0.73 s |
| canter, 4.6 to 8.0 | `warg_canter` | `Warg_Trot`, the trot's keys | 2.71 m | 4.0 m | 0.55 s, fixed |
| gallop, 8.0 to 14.1 | `warg_forward_gallop_left_foot` | `Warg_Run` | 3.92 m | 8.5 m | 0.58 s, fixed |

A charging warg spends most of its time between 5 and 9 m/s, which was the canter: trot legs that covered 2.71 m
while the body moved about 3.9 m a loop. Above 8.8 m/s the gallop's legs covered 3.92 m while the body moved
about 5.0 m. In both paces the front record and its in-place twin carried one `Duration`, so the legs kept one
cadence at every speed. The pack's own canter, the master `Warg_Slow_run` (30 keys, 820 to 850), was bound to no
record.

The paw figures are measured, not typed: `check_gait_stride.measure` on the paw joints (`Fingers1_L`,
`Fingers1_R`, `Toes1_L`, `Toes1_R`) of `skeleton_warg`, over the keys each record plays.
`tools/tests/test_warg_gait_plan.py` repeats the measure on the installed masters and fails when a planned
travel is more than 5% from it.

## What changed

`python tools/repoint_anim_clip.py --plan tools/warg_gait_plan.json --under creature/warg/animations --apply`,
2026-11-23 15:40. Every file is under `Assets/creature/warg/animations/`. The tool changes the fields below and
the item checksum, and keeps every other byte. Backups: `<file>.bak-repoint-20261123-154054` beside each.

| Record | Master and keys, before | Master and keys, now | `Duration` | Other fields |
|---|---|---|---|---|
| `warg_canter` | `Warg_Trot` 1 to 17 | `Warg_Slow_run` 820 to 850 | 0.55 to 0.46 | travel 4.0 to 3.22; limits 4.6..8.0 to 4.6..7.0; step points 0.167, 0.3, 0.467, 0.667 |
| `warg_canter_stand` | `Warg_Trot` 1 to 17 | `Warg_Slow_run` 820 to 850 | 0.55 to 0.70 | none |
| `warg_canter_strafe` | `Warg_Trot` 1 to 17 | `Warg_Slow_run` 820 to 850 | 0.55 to 0.46 | none (travel 0.8 and limits 0..0 kept) |
| `warg_canter_l` | `Warg_Trot_L` 1 to 17 | `Warg_Run_L` 760 to 780 | 0.55 to 0.46 | none |
| `warg_canter_r` | `Warg_Trot_R` 1 to 17 | `Warg_Run_R` 790 to 810 | 0.55 to 0.46 | none |
| `warg_forward_gallop_left_foot` | `Warg_Run` 180 to 200 | the same | 0.58 to 0.33 | travel 8.5 to 3.92; limits 8.0..14.1 to 7.0..12.0 |
| `warg_forward_gallop_left_foot_stand` | `Warg_Run` 180 to 200 | the same | 0.58 to 0.56 | none |

Why these numbers:

- **Travel** is what the paws cover: 3.22 m for the slow run, 3.92 m for the run.
- **Cadence follows speed.** The node plays one loop in the in-place twin's `Duration` at the pace's lower limit
  and in the front record's at the upper, on a straight line between. Each `Duration` is travel divided by that
  limit, so the paws match the ground at both ends: the canter runs from 0.70 s at 4.6 to 0.46 s at 7.0, the
  gallop from 0.56 s at 7.0 to 0.33 s at 12.0. The trial read `DrivenProperty.MountSpeed` 13.0 on a warg and a
  top speed of 10.0 m/s; that the property is in metres a second is not proved, so 12.0 is a ceiling chosen
  above every speed seen. Above the upper limit the node keeps the gallop and holds the loop time between 0.7
  times the front record's `Duration` and 1.3 times the twin's (0.23 s to 0.73 s).
- **The shared limit.** The canter ends and the gallop begins at 7.0, as they shared 8.0 before. The node steps
  up only above the current pace's upper limit and down only below its lower, so the two paces do not flicker at
  7.0, and they have no band of hysteresis either.
- **The canter's turn rows** take the run's turning masters. The pack has no turning slow run, and the run's
  paws land at the same parts of the loop as the slow run's (0.15, 0.30, 0.45, 0.60 against 0.17, 0.30, 0.47,
  0.67), where the trot's do not. These two rows are upper-body rows of the usage set
  (`monster_usage_upper_body_movement`), played by another node. The Codex pre-review's disassembly supports that
  a turn record follows the movement phase (the records carry `synch_with_movement`; the action tick tests the
  flag at 0x659F22), which would make its `Duration` unread while the warg moves. That was not read with
  `tools/native_decompile.py`, so it stays unproved, and how the run's turning poses blend with the slow run is
  for a person's eye.
- **The canter's strafe record** plays the slow run so that it matches the front record's legs. The movement
  node does not read a strafe record's `Duration` in a moving pace (native read, 2026-11-23); 0.46 only keeps
  the record like the one it belongs to.
- **The canter's step points** are where the slow run's paws land.

Not changed: the gallop's turn records (`warg_gallop_turn_left_head`, `_right_head`), which two strike actions
also bind (`act_warg_strike_back_while_moving`, `act_warg_strike_front_while_moving`), so a new `Duration` could
change those strikes (how an action's length follows the record was not read); the gallop's strafe record; the
walk and the trot.

Each of the five canter records ends with a dependency entry that still names its old master. The cook did not
follow it (next section).

## The cook

The game plays a clip from the keys the Modding Kit cooked into its `RuntimeDataCache` entry, not from the
master. A changed record plays its old keys until the Kit cooks it again: start the Modding Kit once with the
Armory loaded, then close it. The check below reads `cooked=False` until then.

Cooked on 2026-11-23 at 16:01. The check has two signals: the entry's stamp is the record's checksum, and the
entry is no older than its master's file (a master whose keys change under one GUID leaves record and stamp
alike). Neither reads the cooked keys, so the entries themselves were read once to prove the cook took the new
masters:

| Entry | Size | Keys it holds |
|---|---|---|
| `warg_trot`, `warg_trot_stand` (not changed) | 4,859 bytes | the trot's |
| `warg_canter`, `_stand`, `_strafe` | 8,155 bytes, one payload | not the trot's: the 30-key slow run |
| `warg_canter_l` | 6,087 bytes | byte for byte the payload of `warg_gallop_turn_left_head`, which plays `Warg_Run_L` |
| `warg_canter_r` | 6,154 bytes | byte for byte the payload of `warg_gallop_turn_right_head`, which plays `Warg_Run_R` |
| the two gallop records | 5,796 bytes | the payload of the gallop's strafe record, cooked in August: the run's keys, as before |

Run `E:/Temp/command-trial-runs/20261123-160513` after the cook: `step=warg_gaits result=PASS riders=11
mounted=11 stand=0.0 top=10.0 limit=13.0 horse=10.3 scale=1.10`, a clean unload and no warg line with a warning
in the engine log. The maintainer watched a warg charge in game the same day: the legs look better.

## Check and gate

```bash
python tools/repoint_anim_clip.py --plan tools/warg_gait_plan.json --under creature/warg/animations --check
```

Exit 0 when each of the seven records holds the plan's values and is cooked as it is now (above); 1 on a record
that is pending or not cooked, or on a plan the tool refuses; 2 when it did not run (no Armory, no `xxhash`).
It is the `warg-gaits` row of `tools/data_gates.py`, which a change to a warg clip package, to a cache entry of
the Armory, to the plan or to the tool selects. A reinstall of the Armory or an art drop reverts the
records, and no other gate sees it: run the same command with `--apply` in place of `--check`, then start the Kit
once.

## Roll back

Copy each `.bak-repoint-20261123-154054` file over its record, then start the Kit once. The check then reads
`PENDING` for all seven, which is the old state.

## Known limitations

- A turning canter plays the run's turning keys on its upper-body turn rows; no turning slow run exists.
- The walk claims 1.6 m against 1.80 m and the trot 2.9 m against 2.71 m, and both keep one cadence at every
  speed (#1565). The warg is not yet a creature of `tools/check_gait_stride.py`: that gate finds a master by its
  clip's name and holds every clip to a slip limit the purchased clips do not meet (#1565).
- The players' copies of the Armory change only with a new Armory package for each channel. A channel ships the
  Armory as compiled `AssetPackages/pack*.tpac` with no `Assets` folder, so the check below cannot read a
  channel: build the package from an install on which the check exits 0
  ([release-process.md](release-process.md)).
- The two battles that showed the wargs at full speed ran on the live Armory. No run used a channel's package.
- The layout the tool writes was read from the live records and matches what the movement node reads in memory
  (travel, then the lower and the upper limit). The engine's reader of the stored record was not found (#1566).

## In-game check owed

Done on 2026-11-23 by the maintainer: a warg charge after the cook, and the legs look better. Still owed, each
by a person's eye: a turn at speed, the change from canter to gallop near 7.7 m/s, and a fell warg. `python
tools/run_command_trial.py --steps enter,warg_gaits` proves the speed and prints the scale; it cannot see the
legs, and it passed before the records were fixed
([what the step does](../features/warg-combat.md#the-warg-gait-step-of-the-command-trial)).
