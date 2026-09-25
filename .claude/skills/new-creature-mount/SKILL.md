---
name: new-creature-mount
description: Use when adding a rideable creature or mount (custom rig, horse-skeleton reskin, or a bought quadruped pack moved onto horse_skeleton). Warg parity is law.
---

# New Creature Mount

Thin entry point over the authoritative doc:
**[docs/ai-includes/creature-mount-authoring.md](../../../docs/ai-includes/creature-mount-authoring.md)**
— read it FIRST and follow its phases in order. It encodes both full campaigns (war elephant
2026-06, giant spider 2026-06) including the 16-gotcha index and the v1.4.6 lookup-hardening
rules. This skill adds only the execution order, the gates, and the top traps.

**Architecture (never deviate):** vanilla cavalry spawn — rider troop + Horse-slot item whose
`HorseComponent` names the Monster; engine does ALL mount work; TAOM layers attacks via a
per-agent behavior tree. NO spawn patches, NO detached combatants (built twice, deleted twice).
**The warg (Alliance.Wargs) is the reference implementation — when in doubt, do what the warg
does, byte-for-byte in shape.**

## FIRST: is this a reskin? (if yes, Phases 1 to 5 are skipped outright)

**A bought four-legged pack on its OWN rig with its OWN clips** (Fab, a marketplace) becomes a reskin: follow
[quadruped-pack-to-horse-skeleton-workflow.md](../../../docs/ai-includes/quadruped-pack-to-horse-skeleton-workflow.md)
end to end (the Animalia elk and moose, #646). It bends the mesh onto `horse_skeleton`, retargets the pack's clips
onto it, and binds them in an `as_horse` child set of its own.

If the mesh is skinned to an **already-registered skeleton**, answer this before authoring
anything. The war ram uses the stock vanilla `horse_skeleton`, so its Monster is the vanilla
`horse_2` shape: `base_monster="horse"` + an action set + a few tuning attributes, inheriting
Flags, `family_type`, `monster_usage`, every bone, the slope block and all twelve rein
attributes. **No new rig, no `quad_movement` authoring, no `monster_usage_sets`, no rider partial:** Phases 1
to 5 below do not apply. What a reskin may still add is an `as_horse` child set binding clips of its own (the
Animalia animals), with its `_map` twin (`MobilePartyVisual` throws without it, Phase 4), and its own typed attack
action with its clip (the ram's `act_war_ram_butt`).

**The cost is shared vocabulary.** A reskin inherits the donor's *behaviour*, not just its
animations, so "our code never fires this" stops implying "nothing fires this". Before binding
any action to a behavior tree check three things: its type in `action_types.xml`, whether the
inherited `monster_usage` set names it in a verb slot or table, and whether the engine branches
on that type. The ram got this wrong twice. The vanilla horse rig's **only attack clip is the
kick** (horses damage by charge collision, so `monster_usage_strikes` is a hit-REACTION table):
`act_horse_kick` (`actt_kick`, `ActionCodeType.Kick = 28`), which the usage set fires itself.
`act_horse_rear` is `actt_rear` and blocks `Agent.Mount`; `act_horse_strike_front` is
`actt_mount_strike = 52`, just outside the half-open `48..51` band `Agent.IsInBeingStruckAction`
reads (`MBMath.IsBetween(type, 48, 52)`), so its type is harmless and its clip, the horse's hit
reaction, is the problem. Worked example: [docs/features/war-ram.md](../../../docs/features/war-ram.md).

## Phase order (each gated before the next)

1. **Assets** (doc Phase 0-1): skeleton **≤63 bones**: the ONLY cap is
   `Skeleton.MaxBoneCount = 64`, a skeleton TOTAL. **There is no per-mesh bone limit** (the old
   "~38 bones, split + recombine" rule is retired): keep the whole body in ONE mesh, split only
   for a genuinely separate sub-mesh such as the warg's cloth-simulated fur. Clips in-place;
   **every gait clip carries `quad_movement` + step points** (Kit Clip *usages*, not Flags),
   gallop-pace runs also `cyclic`.
2. **Monster XML** (Phase 2): `num_paces=6`, `family_type=1`, Flags EXACTLY
   `Mountable CanRear RunsAwayWhenHit CanCharge CanWander` — **`CanAttack` is forbidden**
   (engine attack-AI path; 1.4.6 charge CTD). Rein surface + rider capsule/eye adders.
3. **action_types** (Phase 3): the typed-verb table verbatim — 12 `actt_fall` (+`_continue`),
   rear/kick/dash/quick-stops/hit_object/strikes typed; light strikes UNTYPED `*_while_moving`;
   **`jump_start` action typed `actt_dash`, NEVER `actt_jump`**; a dedicated `actt_idle` `_1`.
4. **action_sets** (Phase 4): bind every usage-referenced action to a VALIDATED clip; explicit
   `act_horse_forward_canter` binding; `_map` + `_town_and_village` children (`_map` is REQUIRED:
   `MobilePartyVisual` looks up `ActionSetCode + "_map"` and throws on a miss; `_town_and_village` only
   mirrors vanilla, nothing derives it, so never write that the engine needs both); the rider partial
   `as_human_warrior` **at the TOP of the file** (base_set snapshots at definition).
5. **monster_usage_sets** (Phase 5): all 10 verb attrs; per-pace `direction="none"` reference
   rows; **jump table TOTAL — all 9 directions × all states = 45 rows** (a missing lookup key
   CRASHES on 1.4.6; an extra row is inert); warg-exact falls + strikes matrices. Registration
   = `project.mbproj` standard `soln_*` ids ONLY (subfolder XML copies are dead decoys).
6. **Item + troop** (Phase 6), then **C#** (Phase 7): clone the elephant's
   MissionBehavior/BT wiring (attach keyed on `Monster.StringId`, never character id); pure
   attack service; mount-lock in `TaomAgentStatCalculateModel`; **extend Patch47's monster
   predicate** so riders dying on the new mount take the dismount-before-death path.

## Validation gates (run BEFORE any battle test — parity-audit-first beats per-crash fixing)

1. Extend `tools/audit_mount_parity.py` (`FILES` + `MOUNTS` maps) for the new creature →
   **zero unaccepted deltas** vs warg/elephant/horse.
2. Animation-target sweep: every `animation=` byte-scanned against the real tpac inventories
   (module + Alliance.Wargs packs + vanilla `Native\...\animation_clips.tpac`) — a phantom
   target compiles a degenerate record that AVs later.
3. XML parse-validate every edited file; back up external-module files first
   (`.bak-<topic>` beside each, never overwrite existing backups).
4. In-game ladder: thumbnail → deployment → charge (jumps!) → melee → rider deaths → mount
   deaths. On any CTD: **`/native-crash-triage`** — never blind-retry.

## Standard follow-through

External LOTRLOME edits get ledger entries
(`docs/reference/lotrlome-spider-mount-changes.md` pattern); feature doc per
`docs/features/elephant.md`/`spider.md` shape; the commit body (the changelog entry); GitHub issue; the `/ship` sequence
for the C# delta.
