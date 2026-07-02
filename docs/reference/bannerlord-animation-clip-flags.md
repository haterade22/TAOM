# Bannerlord animation-clip flags (`AnimFlags`) — the per-clip-type recipe for custom creatures

> **Why this exists:** a custom creature's clips must have the right **animation-clip flags** set or the creature
> won't animate correctly even when it renders + spawns fine. The spider's clips shipped with **zero flags**; the upstream pack's
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

## Full `AnimFlags` reference — every flag, by category

Deep-dive (2026-06-06, 6-agent workflow grounded in the decompiled engine). **How flags work:** the 64-bit
`AnimFlags` word = **low byte (`0xFF`) is a priority integer** + **high bits are independent behavior flags**.
The engine reads a clip's flags natively via `[EngineMethod("get_animation_flags")]`
(`MBActionSet.GetActionAnimationFlags` → `IMBAnimation.GetAnimationFlags`). **Only TWO flags are ever bit-tested in
managed C#** (`anf_synch_with_ladder_movement`, `anf_displace_position`); every other flag is consumed entirely in
`TaleWorlds.Native.dll`. **Consequence for modders:** flags are **authored into the clip** (Kit Animation Clip
Inspector → baked in the `_anm.tpac`); you can't toggle them from a Harmony patch — only OR a few into
`Agent.SetActionChannel(..., AnimFlags additionalFlags, ...)` at play time. Confidence below: *confirmed* =
grep-backed managed use; *strong* = strong convention; *inferred* = from name + category (native-only).

### Cat 1 — Action priority (low byte `0xFF`, NOT independent bits)
The priority integer arbitrates which clip owns a channel: **a request with priority ≥ the current clip's priority
wins; lower is rejected.** `ignorePriority:true` bypasses it. Channels: **0 = lower body/locomotion, 1 = upper
body.** Levels: `continue 0x1` (locomotion/idle — floor) · `jump/ride/crouch 0x2` · `attack 0xA` · `cancel 0xC` ·
`defend 0xE` · `parry/throw/blocked/parried 0xF` · `kick 0x21` · `reload 0x3C` · `mount 0x40` · `equip 0x46` ·
`rear 0x4A` · `upperbody_while_kick 0x4B` · `striked 0x50` (hit-reaction) · `fall_from_horse/jump_loop 0x51` ·
`jump_end 0x52` · `die 0x5F` (top — interrupts anything) · `mask 0xFF` (the extraction mask). **Creature recipe:**
locomotion → `continue (0x1)`, attacks → `attack (0xA)`, hurt → `striked (0x50)`, death → `die (0x5F)` + play with
`ignorePriority:true`. Wrong priority (e.g. a high-priority walk) is the classic "the creature won't play its
attack/death" bug. *(verified: `Agent.cs:2242-2243` plays act_none at `reload` priority; `AgentVictoryLogic.cs:209`
clamps to `0x49`, one below `rear` — proof the low byte is a priority int.)*

### Cat 2 — Movement / root-motion (the creature-critical category)
| Flag | Bit | Purpose | Creature relevance |
|---|---|---|---|
| `anf_synch_with_movement` | `0x2000000` | Time-warps a locomotion clip's playback to the agent's ground speed → **no foot-sliding**. Clip authored **in-place**; engine translates the agent. | **REQUIRED on walk/run/turn.** The #1 anti-skate flag. |
| `anf_use_last_step_point_as_data` | `0x800` | Marks the **stride-reference** clip the gait builder samples for stride length. | The `*_stand_for_movement_data` clip. |
| `anf_displace_position` | `0x400000000000` | **Root motion ON** — the clip's baked root travel MOVES the agent. *(confirmed: `AnimationPoint.cs:305`)* | One-shot lunge/leap only — **never** on cyclic walk/run. |
| `anf_affected_by_movement` | `0x40000000000` | Clip blended/biased by movement state (broader than synch). | An attack played while moving, if it looks frozen. |
| `anf_lock_movement` | `0x1000000` | **Pins the agent in place** for the clip. | Stationary attacks + death. |
| `anf_synch_with_horse` | `0x200000` | Rider clip synced to the mount's gait. | Rider clips only — N/A to a creature-troop. |
| `anf_synch_with_ladder_movement` | `0x10000000` | Climb synced to ladder ascent. *(confirmed managed: `AgentVictoryLogic.cs:204`)* | N/A. |
| `anf_enforce_root_rotation` | `0x8000000000` | Facing follows the clip's baked root rotation. | Turn-in-place clips. |
| `anf_align_with_ground` | `0x100000000000` | Tilts the body to the terrain normal. | Maybe — a wide creature on slopes (test vs warg). |
| `anf_ignore_slope` | `0x200000000000` | Inverse — keep clip-authored orientation, ignore slope. | Flat-authored poses. |
| `anf_ignore_scale_on_root_position` | `0x1000000000000` | Apply root displacement WITHOUT the body-scale multiply. | A scaled creature's displacing one-shot that overshoots. |
| `anf_blends_according_to_look_slope` | `0x100000` | Blend by look pitch (aim up/down). | Aim clips — N/A to melee creature. |

### Cat 3 — Body enforcement / layers / lifecycle
| Flag | Bit | Purpose | Creature relevance |
|---|---|---|---|
| `anf_enforce_lowerbody` | `0x1000000000` | Clip fully OVERRIDES the leg bones (no cross-blend). | A planted-stance attack. |
| `anf_enforce_all` | `0x2000000000` | Clip overrides the WHOLE skeleton (no blend smear). | **Death** + hard full-body transitions. |
| `anf_allow_head_movement` | `0x10000000000` | Carves the head OUT so look-at keeps steering it. | Idle/locomotion if the creature should track targets. |
| `anf_animation_layer_flags_mask` | `0xFFFF000000000` | 16-bit field (bits 36-51) = the clip's animation **layer** routing. | Don't hand-set — author via the channel/action-set. |
| `anf_animation_layer_flags_bits` | `0x24` | The **shift** (=36) locating the layer field — metadata, NOT a flag. | **Never OR into a clip** (it overlaps the priority byte). |
| `anf_cyclic` | `0x4000000000` | **Loops** the clip. | **REQUIRED on idle + all locomotion** (or they play once and stop). |
| `anf_keep` | `0x4000` | Hold/freeze on the last frame. | A held threat/rear pose. Don't combine with cyclic. |
| `anf_restart` | `0x8000` | Re-trigger from frame 0 even if already playing. | A spammable repeat attack. |
| `anf_disable_auto_increment_progress` | `0x100000000` | Engine stops advancing progress — script scrubs it. | Only a pose-driven special; **never** on normal clips (they'd freeze). |
| `anf_disable_alternative_randomization` | `0x80000000` | Opt this clip OUT of the random-variant pool. | A signature attack you never want swapped. |

### Cat 4 — IK / collision / physics
| Flag | Bit | Purpose | Creature relevance |
|---|---|---|---|
| `anf_disable_hand_ik` | `0x40000` | Turn off hand-IK → hands play as authored. | **Set on creature clips** (no hand grip target to solve toward). |
| `anf_enable_hand_spring_ik` | `0x4000000` | Spring-damped hand IK (smooths target pops). | N/A (no grip). |
| `anf_enable_hand_blend_ik` | `0x8000000` | Blended hand IK (partial commit to target). | N/A (no grip). |
| `anf_enable_left_hand_ik` | `0x800000000000` | IK the OFF hand (fore-grip/reins/shield). | N/A (no off-hand). |
| `anf_disable_foot_ik` | `0x20000000000` | Turn off foot-grounding IK. | **Do NOT set on grounded walk/run** (feet must track slopes); DO set on jump/rear/death. ⚠️ vanilla rig grounds only **2 feet** (`r_foot/l_foot`) — a >2-leg spider may only partially ground. |
| `anf_disable_agent_agent_collisions` | `0x100` | Pass through OTHER agents (not world). | A big creature's mount/death/special so it doesn't bulldoze troops. |
| `anf_ignore_all_collisions` | `0x200` | Ignore agents AND world. | Death/spawn/special only — **dangerous** (can sink through floor). |
| `anf_ignore_static_body_collisions` | `0x400` | Ignore world only (stay solid vs agents). | An oversized pose wedging on geometry. |
| `anf_update_bounding_volume` | `0x80000000000` | Recompute the cull/hit bounds from the live pose. | **Wide-pose clips** (rear/lunge/death) so the creature isn't culled or mis-hit-tested when limbs sweep past rest bounds. |

### Cat 5 — Sound / particle / camera / network prediction
| Flag | Bit | Purpose | Creature relevance |
|---|---|---|---|
| `anf_make_bodyfall_sound` | `0x1000` | Emit the heavy body-impact "thud". | Death/collapse clip. |
| `anf_make_walk_sound` | `0x20000` | Emit footstep foley in gait cadence. | **Walk/run clips** (footsteps). |
| `anf_do_not_keep_track_of_sound` | `0x20000000` | Fire-and-forget the clip's sound. | Rare. |
| `anf_attach_sound_to_agent` | `0x400000000` | Spawned sound follows the moving agent. | A growl/charge loop that must track the body. |
| `anf_spawn_particle` | `0x800000000` | Enable the clip's baked particle/VFX keys. | Only if the `.anim` has particle keys (Blender clips don't). |
| `anf_lock_camera` | `0x800000` | Pin the camera to a clip pose. | Cinematic/mount clips — N/A to a troop. |
| `anf_reset_camera_height` | `0x40000000` | Snap camera height back to baseline. | N/A. |
| `anf_client_prediction` | `0x2000` | MP: predict the clip on any client. | **MP-only — irrelevant to SP TAOM creatures.** |
| `anf_client_owner_prediction` | `0x10000` | MP: predict only on the owning client. | MP-only — irrelevant. |

### Cat 6 — Item/weapon handling + randomization (almost all humanoid-only)
| Flag | Bit | Purpose | Creature relevance |
|---|---|---|---|
| `anf_stick_item_to_left_hand` | `0x80000` | Rigidly parent the held item to the left-hand bone. | **Leave UNSET** (no hand/item). |
| `anf_switch_item_between_hands` | `0x200000000` | Item changes hands mid-clip. | UNSET. |
| `anf_blend_main_item_bone_entitially` | `0x2000000000000` | Blend the weapon bone at entity level (no swap-pop). | UNSET (no weapon bone). |
| `anf_use_left_hand_during_attack` | `0x400000` | Attack delivered with the off hand. | UNSET (creature attack is service-driven, not a weapon swing). |
| `anf_enforce_weapon_tip_with_rope_stretched` / `_relaxed` | `0x4000000000000` / `0x8000000000000` | Constrain a flail/rope weapon tip (stretched vs slack). | UNSET (no rope weapon). |
| `anf_randomization_weight_1/2/4/8` (+ `_mask` `0xF…`) | top nibble (bits 60-63) | Packed 4-bit **weight** biasing random selection among an action's alternative clips. | **Generic** — applies if you author multiple alternative idle/stand clips and want weighted random pick. |

**Net for a TAOM creature (spider/warg):** the flags that matter are Cat 1 priority (continue/attack/striked/die),
Cat 2 `synch_with_movement` + Cat 3 `cyclic` on locomotion, Cat 2 `lock_movement` + Cat 3 `enforce_all` on
attacks/death, Cat 4 `disable_hand_ik` (+ leave foot-IK on for grounded clips, `update_bounding_volume` on wide
poses), Cat 5 `make_walk_sound`/`make_bodyfall_sound`. Everything else is humanoid/MP/cinematic and stays unset.
**Source of truth = mirror the warg's per-clip flag set** (`Alliance.Wargs`), don't reconstruct from this table.

## TODO — a tpac clip-flag tool (not yet built)

The existing `tpac_skeleton_*` tools read/patch **Skeleton** UserData, not **Animation** clip metadata. A
heuristic scan of the `_anm.tpac` metadata for the `AnimFlags` ulong was **unreliable** (false positives from
adjacent duration floats — the Animation metadata layout isn't reverse-engineered). A proper
`tpac_clip_flags.py` (read the exact baked flags; optionally patch them to avoid per-clip Kit clicking) is a
high-value follow-up: it would let us read the upstream pack's exact per-clip flags authoritatively and set the spider's
programmatically. Until then: read/set flags in the Kit's Animation Clip Inspector.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/adod-beasts-architecture-and-taom-port.md](./adod-beasts-architecture-and-taom-port.md)
- [docs/reference/bannerlord-engine-and-toolchain.md](./bannerlord-engine-and-toolchain.md)
- [docs/reference/engine/animation-binding-and-playback.md](engine/animation-binding-and-playback.md)

<!-- backlinks-end -->
