# Creature Skeleton Specs — index + how-to

Declarative JSON skeleton specs, built by `tools/blender/skeleton_spec.py` (`build_from_spec`).
Spec format: `{"name", "bones":[{"name","parent","head":[x,y,z],"tail":[x,y,z],"roll"}]}`.
Frame: **Y = forward, Z = up, X = right, meters**. Bones may be listed in any order (parents are linked in a second pass).

## Specs (all built + verified live)
| Spec | Bones | Type | Validated |
|---|---|---|---|
| `spider_skeleton_spec.json` (in `_auto_workspace`) | 59 | arachnid (extracted) | iter 8 — round-trip names+parents match |
| `tools/blender/specs/chariot.json` | 9 | rigid / mechanical (wheels = spin bones) | iter 9 — built + 9 auto-weight vgroups |
| `tools/blender/specs/ram.json` | 22 | quadruped (body + neck/head + 2 horns + 2 tail + 4 legs×3) | iter 11 — built + 22 vgroups |
| `tools/blender/specs/goat.json` | 22 | quadruped (ram variant, smaller) | iter 11 — built, names+parents match |
| `tools/blender/specs/troll.json` | 21 | humanoid (troll/ogre; can reuse the human action_set) | iter 12 — built + 21 vgroups |

## How to author a new creature skeleton
1. **Write a JSON spec** — copy `ram.json` (quadruped) or `chariot.json` (mechanical) and edit bone names + `head`/`tail` coords. Or generate programmatically (see the ram/goat generator pattern in `loop-log.md` iter 11).
2. **Build (Blender MCP):** `exec(open(r'…\tools\blender\skeleton_spec.py').read(), globals()); obj = build_from_spec(json.load(open(r'…\yours.json')))`.
3. **Verify:** bone count + 0 parent mismatches (or `roundtrip_check("<armature>")` when extracting an existing one).
4. **Skin:** `auto_weight(obj, mesh_obj)` for a first pass (`ARMATURE_AUTO`); refine weight-paint manually.
5. **Into Bannerlord:** animate → `export_all_actions_fbx` → Modding-Kit compile → populate ragdoll `SkeletonUserData` with `tools/tpac_skeleton_transplant.py` (adapt its `classify_bone()` to the new bone names) → wire `monsters.xml` / `action_sets`.

## Validation evidence (sessions all returned to baseline)
- chariot: 9 bones, 9 auto-weight vertex groups (iter 9).
- ram: 22 bones, 22 vertex groups; goat: 22 bones (iter 11).
- spider: 59-bone round-trip, names + parents identical (iter 8).
