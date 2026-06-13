---
name: new-creature-mount
description: Author a rideable creature mount end-to-end (assets, Monster/action/usage XML, C# behavior tree, validation) following the elephant+spider-proven workflow. Warg parity is law.
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

## Phase order (each gated before the next)

1. **Assets** (doc Phase 0–1): skeleton ≤64 bones; meshes ≤~38 bones each (split + recombine
   via `<AdditionalMeshes>`); clips in-place; **every gait clip carries `quad_movement` +
   step points** (Kit Clip *usages*, not Flags), gallop-pace runs also `cyclic`.
2. **Monster XML** (Phase 2): `num_paces=6`, `family_type=1`, Flags EXACTLY
   `Mountable CanRear RunsAwayWhenHit CanCharge CanWander` — **`CanAttack` is forbidden**
   (engine attack-AI path; 1.4.6 charge CTD). Rein surface + rider capsule/eye adders.
3. **action_types** (Phase 3): the typed-verb table verbatim — 12 `actt_fall` (+`_continue`),
   rear/kick/dash/quick-stops/hit_object/strikes typed; light strikes UNTYPED `*_while_moving`;
   **`jump_start` action typed `actt_dash`, NEVER `actt_jump`**; a dedicated `actt_idle` `_1`.
4. **action_sets** (Phase 4): bind every usage-referenced action to a VALIDATED clip; explicit
   `act_horse_forward_canter` binding; `_map` + `_town_and_village` children; the rider partial
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
`docs/features/elephant.md`/`spider.md` shape; CHANGELOG; GitHub issue; the `/ship` sequence
for the C# delta.
