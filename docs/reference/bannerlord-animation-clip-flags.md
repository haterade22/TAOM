# Bannerlord animation-clip flags (`AnimFlags`) — the per-clip-type recipe for custom creatures

> **Why this exists:** a custom creature's clips must have the right **animation-clip flags** set or the creature
> won't animate correctly even when it renders + spawns fine. The spider's clips shipped with **zero flags**; ADOD's
> elephant clips have a **specific set per clip type**. This is the "simpler-fix-first" class — broken creature
> locomotion is usually a clip-flag config problem, **not** an engine-internals problem. Discovered 2026-06-06 (user
> spotted it in the Kit's Animation Clip Inspector).
>
> **This is distinct from the render AV.** The render crash is the *mesh* (native `Agent.PreloadForRendering`);
> these flags are about *animation playback*. A creature can render and still animate wrong (slide, T-pose, not
> loop) if the flags are missing.

## Where the flags live (and where they DON'T)

- **They are baked into the compiled clip** — the `*_anm.tpac` metadata. Set them in the Modding Kit's
  **Animation Clip Inspector** (the checkbox list), then the clip is recompiled with the flag bitfield.
- **NOT in `action_types.xml`** — that file declares action *types* (`name`, `usage_direction`) and binds them to
  clips in the `action_set`; it does **not** carry the per-clip flags.
- The engine reads them at runtime via the native `MBAnimation_get_animation_flags` callback. The bitfield is the
  `AnimFlags : ulong` enum in `TaleWorlds.MountAndBlade` (engine struct `Anim_flags`).

## The flags that matter most for creatures (`AnimFlags`, authoritative — from the engine enum)

| Flag | Hex bit | What it does | Set it on |
|---|---|---|---|
| `anf_synch_with_movement` | `0x2000000` | **Plays the clip in sync with the agent's actual movement speed.** Without it the legs don't track the body → the creature **slides**. | **all locomotion** (walk/run/canter/gallop/turn) |
| `anf_cyclic` | `0x4000000000` | The clip **loops**. | locomotion + idle/stand |
| `anf_lock_movement` | `0x1000000` | The agent **can't move** while the clip plays. | attacks (some idles) |
| `anf_enforce_all` | `0x2000000000` | Force the clip on the **whole body** (override blending). | attacks, death, rear |
| `anf_enforce_lowerbody` | `0x1000000000` | Force the clip on the lower body. | faster gaits (canter/gallop) |
| `anf_enforce_root_rotation` | `0x8000000000` | The clip rotates the **root** — for **turn** clips. | turning |
| `anf_client_prediction` | `0x2000` | MP client-side prediction. | ~every clip |
| `anf_use_last_step_point_as_data` | `0x800` | Marks the **movement-data reference** clip. | `*_stand_for_movement_data` |
| `anf_affected_by_movement` | `0x40000000000` | Clip is affected by movement. | locomotion |
| `anf_make_walk_sound` | `0x20000` | Emit footstep sounds. | walk/run |
| `anf_align_with_ground` | `0x100000000000` | Align the body to ground slope. | locomotion + stand |
| `amf_priority_*` (low byte `0xFF`) | e.g. `attack=0xA`, `die=0x5F`, `rear=0x4A` | Action **priority** (which clip wins when several want to play). | per action class |

(Full enum: `E:/Decompiled_Bannerlord/MountAndBlade/.../AnimFlags.cs` — 60+ flags incl. IK, camera, particle, sound.)

## Per-clip-type recipe

**CONFIRMED** from the elephant clips in the Kit (2026-06-06 screenshots):

| Clip type | Flags (confirmed) |
|---|---|
| **Walk / movement** | `anf_synch_with_movement` + `anf_cyclic` + `anf_client_prediction` |
| **Attack** | `anf_lock_movement` + `anf_enforce_all` + `anf_client_prediction` |

**CONVENTION** (from the enum + standard Bannerlord creature clips — verify each in the Kit before relying on it):

| Clip type | Flags (convention — confirm in Kit) |
|---|---|
| **Run / canter / gallop** | walk set + `anf_enforce_lowerbody` (+ `anf_make_walk_sound`) |
| **Turn left/right** | `anf_enforce_root_rotation` + `anf_synch_with_movement` + `anf_cyclic` + `anf_client_prediction` |
| **Idle / stand** | `anf_cyclic` + `anf_client_prediction` (+ `anf_align_with_ground`) |
| **Stand-for-movement-data** | `anf_use_last_step_point_as_data` (the locomotion reference clip — the gait builder reads it) |
| **Death** | `anf_enforce_all` + death priority (`amf_priority_die`), **no** `anf_cyclic` |
| **Rear** | `anf_enforce_all` + rear priority (`amf_priority_rear`) |

## The spider gap (what to fix)

The spider's `an_spi_*` clips have **no flags set** → even once the render AV is solved, the spider would slide
(no `synch_with_movement`), not loop (no `cyclic`), and not lock during bites (no `lock_movement`/`enforce_all`).
**Action:** in the Kit's Animation Clip Inspector, set the per-type flags above on each `an_spi_*` clip:
- `an_spi_walk_2` / `an_spi_walk_left` / `an_spi_walk_right` / `an_spi_run*` → movement set.
- `an_spi_turn_left` / `an_spi_turn_right` → turning set.
- `an_spi_attack_*` → attack set.
- `an_spi_idle*` → idle set.
- (this is **separate** from re-exporting the 13 missing `_anm.tpac` — see the spider RCA; a clip needs both its
  `_anm` to exist AND the right flags on it.)

## TODO — a tpac clip-flag tool (not yet built)

The existing `tpac_skeleton_*` tools read/patch **Skeleton** UserData, not **Animation** clip metadata. A
heuristic scan of the `_anm.tpac` metadata for the `AnimFlags` ulong was **unreliable** (false positives from
adjacent duration floats — the Animation metadata layout isn't reverse-engineered). A proper
`tpac_clip_flags.py` (read the exact baked flags; optionally patch them to avoid per-clip Kit clicking) is a
high-value follow-up: it would let us read ADOD's exact per-clip flags authoritatively and set the spider's
programmatically. Until then: read/set flags in the Kit's Animation Clip Inspector.
