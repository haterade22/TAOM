# Blender asset gates

## Overview

Two checks that a mesh passes in Blender before it goes to the Modding Kit, and the skill that
runs them. `tools/blender/mesh_preflight.py` reads a mesh the way the export will write it and
gives one verdict. `tools/blender/qa_views.py` renders six fixed views with back faces culled, the
way the engine draws them. The [`/blender-asset`](../../.claude/skills/blender-asset/SKILL.md)
skill holds the rules for a live Blender session, sends each task to the workflow that owns it,
and runs the two gates. This is tooling: it changes no gameplay.

## Why This Exists

TAOM's Blender rules were strong where Bannerlord is particular (collision bodies, LODs, texture
packing, retopology selection, hair, rigs, clips) and thin in the layer around them.

- **Before:** the mesh checks lived inside six tools, each for its own asset kind (hair finalize,
  the rig export, the UV unwrap, the Erebor assembler, the LOD audit, the mesh reference
  validator). A prop modelled by hand, or changed over the Blender MCP, passed through none.
- **The failures this would have caught:** every box of the Carn Dûm icon wound inward through
  nine versions, because Blender draws back faces and the engine culls them; a material named
  `x.001` that the Kit binds to nothing; a missing `bo_` twin, which hangs the mission load (#352).
- **Also missing:** one place for the session rules of the Blender MCP, a standard set of views,
  a procedure for matching a reference picture, and a table that says which workflow owns which
  Blender task.

The idea of a routed pipeline with a validation gate and a screenshot-backed verdict comes from
[arjun988/blender-skills](https://github.com/arjun988/blender-skills) (MIT). Its text was rewritten
for Bannerlord and its engine rules were not taken: see
[the review](../reviews/adopt-blender-skills-2026-11-13.md).

## Architecture

```
/blender-asset (skill)
   |-- session rules, routing table, brief
   |-- Gate 1: mesh_preflight.py     pure rules  <- extract_mesh (bpy, bmesh, BVH rays)
   |             headless: --fbx | --blend -> <out>/_report/DONE.txt
   |             live:     preflight_scene(names) -> dict (read-only)
   |-- Gate 2: qa_views.py           six PNG files, back faces culled (headless only)
   '-- verdict: SHIP | SHIP WITH NOTES | NO-SHIP
```

**Design choices**

- The rules are pure functions over plain records, so `tools/tests` covers every rule without
  Blender. A thin layer reads the records from Blender. That layer is proven by the canary below.
- A mesh is read as the export will write it: modifiers applied, in world space, a mirrored
  object's faces reversed. The Kit's own frame (an FBX read back at rotation X 90 and scale 0.01)
  is uniform and is accepted.
- No commit gate. The check needs Blender, which is not on every machine, and most FBX sources
  live in the unversioned live modules. The skill step is the trigger.
- LOD chains stay with `tools/audit_fbx_lods.py`, UV fold-over and texel density with the probes
  of `tools/oneoff/blender_prep_tripo_prop.py`, name references with `tools/validate_mesh_refs.py`.

## The checks

A check is a blocker only where TAOM has a recorded failure or gate for it, and a major where a
recorded trap applies. The counts taken from the source's generic checklist are notes.

| Severity | Code | What it finds | Why (evidence) |
|---|---|---|---|
| Blocker | `faces-inverted` | Over 5% of the surface is seen only from behind | The Carn Dûm boxes ([lesson](../reviews/lessons/build-tooling-workflow.md), "A renderer that draws back faces hides a mesh wound inside out") |
| Major | `faces-inverted-suspect` | 0.1% to 5% of the surface is seen only from behind | Geometry cannot tell a hole from a face with another face right behind it; Gate 2 decides (see "Measured") |
| Note | `faces-inverted-few`, `faces-inverted-interior` | Under 0.1%, or any share with `--interior` | Crevices between overlapping shells (175 faces on Carn Dûm after the fix); a hall or cave is seen from inside |
| Blocker | `material-duplicate-suffix` | A material named `x.001` | The Kit binds by exact name and renders an unknown name unbound ([armory-guide.md](../reference/armory-guide.md), "Materials on a re-import"); the Erebor assembler refuses the same |
| Blocker | `uv-missing` | A visual mesh with no UV layer | A material has nothing to map; the hair and groom gates already refuse it for their meshes |
| Blocker | `body-missing` (`prop`) | A visual mesh with no `bo_` twin for its prefix | #352, the infinite mission load ([mesh-ref-validation.md](mesh-ref-validation.md)) |
| Blocker | `skin-unweighted` | A vertex with no weight in a group named after a bone | `tools/blender/export_rig_for_kit.py` refuses it. That gate is stricter: it first drops weights under 0.01 |
| Blocker | `skin-too-many-bones` | More than 64 bones | `Skeleton.MaxBoneCount` ([spider pipeline](spider-skeleton-animation-pipeline.md)) |
| Blocker | `no-meshes` | The file holds no mesh | An empty input must not read as a pass |
| Major | `material-empty-slot` | Faces with no material, or no slot at all | Inferred from the same binding rule; not observed |
| Major | `uv-collapsed` | Over 1% of the surface has UVs with no area | #760, 118 of 223 hair cards |
| Major | `transform-mirrored` | An object with a negative scale | Baking it flips the winding ([pipeline doc](../reference/ue-to-bannerlord-asset-pipeline.md), "Blender headless staleness"); what the Kit does with it unbaked is not verified |
| Major | `transform-multi-user` | Shared mesh data on a moved object | `transform_apply` skips it silently (same section) |
| Major | `skin-over-four-influences` | A vertex with more than 4 weights | The Kit keeps the 4 strongest, so the game bends the mesh differently from Blender |
| Major | `name-duplicate-suffix` | An object named `x.001` | The Kit reads `<mesh>.<part>` as a sub-mesh ([armory-guide.md](../reference/armory-guide.md), "Naming") |
| Major | `body-no-physics-material` | A `bo_` with no named slot | The body carries its physics material as a slot name (`stone`, `wood`) |
| Major | `icon-has-body` (`icon`) | A `bo_` in a map icon file | The settlement entity has its own ([map-icons.md](../kitbash/map-icons.md)) |
| Major | `over-budget` | LOD0 triangles over `--budget-tris` | The budget is the asset it replaces (`/prep-tripo-prop` Phase 1) |
| Minor | `material-unused-slot` | A slot no face uses | It reaches the Kit as an empty sub-mesh |
| Minor | `transform-non-uniform-scale` | Scale differs by axis | No recorded failure |
| Note | `name-upper-case`, `uv-extra-layers`, `topo-non-manifold`, `topo-loose-verts`, `topo-zero-area`, `topo-ngons` | Counts | No recorded failure. The Kit lowercases names, and an XML reference in another case finds nothing |

**Limits of the inverted-face rule.** A face counts when no ray leaves the mesh along its normal
and rays do along the reversed normal. The share is a lower bound: a flipped wall that sees out
through a window is not counted (100 of 320 flipped faces on the canary). Each mesh is tested
alone. Above 200,000 faces an even sample is read and the report says `inverted_sampled`.

**What is read.** Only meshes visible in the view layer. A mesh that is hidden or in an excluded
collection is listed in `skipped_hidden`. LOD0 is the bare name or `<name>_lod0`; a Blender `.001`
suffix does not change what a name is (`x.lod1.001` is still a LOD, `bo_x.001` still the body of
`x`), and is reported on its own.

## The views

`qa_views.py` writes six square orthographic PNG files, named by where the camera stands in Blender's
axes: `3q_front_left` (-X -Y, above), `3q_back_right` (+X +Y, above), `front` (-Y), `side` (-X),
`top` (+Z) and `3q_below` (-X -Y, below). The mesh is one grey, back faces are culled, and the
world is magenta. A hole shows as magenta, or as the inside of the mesh seen through a missing
wall. Only the visual LOD0 meshes are shown; `--bodies` shows the collision bodies instead. The
picture is square because an orthographic frame spans the longer side: on a wide picture a tall
mesh lost its top and bottom.

"front" means Blender's -Y and nothing more. An FBX's facing in game follows its kit's frame rule,
so a direction is judged from the axes.

Not in these views: textures (`tools/blender/render_painter_preview.py`), wireframes and armatures
(the live viewport, through the Blender MCP screenshot tool).

## Verdict

Defect levels, from the source's QA gate: a **blocker** stops the asset, a **major** is fixed or
named with the evidence that it is harmless, a **minor** and a **note** are reported. The verdict
form and what SHIP means are in the skill, section 6.

## Reference analysis template

Fill this in before any change when Mike gives a picture to match.

```markdown
## Reference analysis: <asset>
Reference: <picture>            Existing file: <path, or new scene>
Match goal: <similar | close | these features only>

1. Subject: <kind, style, forward axis, size in metres>
2. Silhouette: <3 to 5 main masses>; proportions <share of length or height each>
3. Camera: <angle, lens feel, how much of the frame the subject fills>
4. Materials: <surface kinds, matte to gloss, wear>
5. Must have: [ ] <feature 1>  [ ] <feature 2>  [ ] <feature 3>
6. Differs today (existing file only):
   | Area | Reference | Now | Priority |
```

The build passes and the compare loop are in the skill, section 7. In each compare, rate frame,
silhouette, depth, materials and features as match, close or miss; each miss gets one fix.

## Key Files

| File | Purpose |
|------|---------|
| `.claude/skills/blender-asset/SKILL.md` | Session rules, routing table, brief, the two gates, the verdict form |
| `tools/blender/mesh_preflight.py` | Gate 1: rules, the Blender reader, the headless and live entry points |
| `tools/blender/qa_views.py` | Gate 2: the view set and the culled renders |
| `tools/blender/blender_job.py` | Shared: the report every headless exit writes, the FBX import |
| `tools/oneoff/kitbash_kits.py` | Shared: `split_name` (base, LOD, `bo_`) |
| `tools/evals/route-blender-asset/case.yaml` | Routing eval for the skill |

## Tests

- `tools/tests/test_mesh_preflight.py`: names, topology counts, the ray rule, every finding with
  the state that raises it and the state that does not, the file-level rules per profile, the
  verdict, the arguments, the report order.
- `tools/tests/test_qa_views.py`: the view set, the camera frame, the object filter, the arguments.
- Run: `python -m unittest tools.tests.test_mesh_preflight tools.tests.test_qa_views`.

The Blender side has no unit test. It is proven by the canary.

## Measured

Blender 5.3.0, 2026-11-13, on `erebor_dwarf_house_01.fbx` (approved in the Kit 2026-10-15) and a
copy broken by script (the faces of one wall material flipped, one material renamed to `.001`,
the `bo_` removed). The source file's hash was the same before and after.

| File | Profile | Verdict | Findings |
|---|---|---|---|
| Approved house | `prop` | PASS WITH NOTES | 3 major `faces-inverted-suspect` (0.99%, 1.00%, 1.08% on the three levels), 3 notes |
| Broken copy | `prop` | FAIL | `body-missing`, `faces-inverted` (10.6% to 13.5%), `material-duplicate-suffix` |

The house's 36 suspect faces are thin trim slabs on the wall base, wound inward, each with a face
2 to 20 cm behind it. Its six views show no hole at those slabs, which is why that share is a
major and not a blocker. The `front` and `side` views do show magenta through three window
openings: the house has no faces behind them. Whether the world shows through them in game is a
question for the Kit or a game check, not for these tools.
The broken copy's `3q_front_left` view shows the missing walls and the inside of the house.

Three more runs, after the review:

| Case | Result |
|---|---|
| The house with the face cap forced to 1,000, so the sampling path runs | No crash; `inverted_sampled` true on the three visual meshes; PASS WITH NOTES |
| A 1 x 1 x 10 box in a `.blend`, with a 50 x 50 mesh in an excluded collection | PASS; `skipped_hidden` names the excluded mesh; the whole box is inside the `front` view |
| The house and the broken copy again, on the fixed code | The same verdicts and shares as above |

Not run: the live-session entry `preflight_scene` (no Blender was connected over MCP). It calls
the same reader as the headless run. No skinned mesh was run in Blender; the skin rules have unit
tests only.

## How to verify in game

These tools stop at the Kit boundary, so they have no in-game check of their own. Each asset keeps
the check of its own workflow (for a prop: the last step of `/prep-tripo-prop` Phase 4; for a creature:
[creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md)).

**Preconditions:** Blender installed (the Store build), an FBX staged outside the live modules.

1. Run Gate 1 on a Kit-proven FBX. `_report\DONE.txt` must say `"status": "ok"` and a `verdict`
   that is not `FAIL`.
2. Run Gate 1 on a copy with one face group flipped. The report must say `"status": "error"`,
   `"verdict": "FAIL"` and name `faces-inverted`.
3. Run Gate 2 on both and read the PNG files. Only a person, or an agent that reads the pictures,
   can see this result.

## How to add a check

1. Find the recorded failure, gate or trap. Without one the check is a note.
2. Add a test to `tools/tests/test_mesh_preflight.py` for the state that raises it and the state
   that does not, and see it fail.
3. Add the field to the record in `extract_mesh` and the rule to `check_mesh` (one mesh) or
   `check_set` (the whole file).
4. Add its row to "The checks" above, with the evidence.

## Changelog

- 2026-11-13: first version (#1153): the skill, both gates, the canary.

## GitHub Issue

- **Issue:** #1153: [Blender asset gates](https://github.com/haterade22/TAOM/issues/1153)
- **Status:** Open
