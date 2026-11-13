# Reviewing arjun988/blender-skills v1.3.0 (2026-11-13)

Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)).
**Outcome:** nothing installed. Four ideas came over in TAOM's words and became one skill, two tools
and one feature doc ([blender-asset-gates.md](../features/blender-asset-gates.md), #1153). The pack's
engine rules were not taken: they are for Unity, Unreal and Godot, and TAOM's own are measured on
Bannerlord.

## Source, and what was read

Mike asked for the pack's skills, lessons and knowledge to go into TAOM's Blender workflows, with new
workflows built from them. The review used commit `904f095d8f36a6e72adaebb08d07f884a074743d`, version
1.3.0, pushed 2026-11-06. The licence is MIT. No earlier verdict exists in
[adoption-history.md](adoption-history.md).

The pack is 94 markdown skills with reference files for generic game art, driven through a
third-party Blender MCP server. The tree holds 579 files: 269 under `.claude/skills`, the same 269
byte for byte under `.cursor/skills` (compared by blob hash), a Next.js site and docs.

| Depth | Files |
|---|---|
| Read in full | `.mcp.json`, `.cursor/mcp.json`, both plugin manifests, `CLAUDE.md`, `README.md`, `QUICKSTART.md`, `CONTRIBUTING.md`, `docs/BLENDER_MCP_SETUP.md`, `site/package.json` and the two site config scripts; all nine shared references; `blender-director`, `qa-review`, `export-pipeline`, `collision-proxy`, `lod-pipeline`, `asset-optimization`, `retopology`, `uv-workflow`, `texture-workflow`, `rigging`, `animation`, `hair-groom`, `creature-artist`, `prop-artist`, `blender-modeler`, `cloth-sim` and `fantasy-worlds`, most of them with some or all of their references |
| Read in part | `hard-surface`, `character-artist` |
| Not read | The other style, genre, world, mood and era skills (about 60, each 500 to 900 bytes in the tree listing), the cinematic and simulation skills, the three engine export skills, `SKILLS_GUIDE.md`, `CHANGELOG.md`, `ROADMAP.md`, the site's source |

The verdicts below on unread skills rest on their names, sizes and the two samples read
(`fantasy-worlds` and its `medieval.md`, which is one line).

## Security

Safe to learn from: yes. Safe to install: no.

- The package is markdown and manifests. It has no hooks, no install script and no code that runs on
  use. The only script files in the tree are two site config files.
- `site/package.json` has no lifecycle script.
- `.mcp.json` starts an unpinned `blender-mcp` through `uvx`. That server is a third party's
  (`ahujasid/blender-mcp`), not the Blender MCP server TAOM uses, and its tool names differ.
- `docs/BLENDER_MCP_SETUP.md` installs `uv` by piping a downloaded script into a shell, and offers
  API keys for Sketchfab, Hyper3D and Hunyuan3D downloads.
- Installed as a plugin it would load 94 skill descriptions into every session.
- `python tools/audit_claude_config.py --root <clone> --external` scanned 579 files, the whole tree,
  and reported one HIGH: it read the `uv` setting `UV_PYTHON_PREFERENCE=only-managed` in `.mcp.json`
  as a hardcoded secret. That is a false positive, and TAOM takes nothing from the file.

The clone stayed in a scratch folder, and nothing from it ran.

## Verdict by item

| Item | Verdict | Reason |
|---|---|---|
| Director: route a task, then gate it | Taken, calibrated | TAOM had the workflows and no table that says which one owns a Blender task. The `/blender-asset` routing table points at TAOM's own docs |
| Validation checklist before export | Taken as a tool | The checks existed inside six tools, each for one asset kind. `tools/blender/mesh_preflight.py` runs them on any mesh. A check fails a run only where TAOM has a recorded failure; the pack's other items are notes |
| QA review: fixed screenshots, defect levels, explicit verdict | Taken, calibrated | `tools/blender/qa_views.py` culls back faces, which the pack does not ask for and the engine does. A Blender verdict never stands for the Kit import or the in-game check |
| MCP protocol: query state, never skip a failed step, keep the source | Taken into the skill | Joined with TAOM's own session lessons, which sat in three files |
| Reference-image match: analysis first, camera first, three tiers, three compare passes | Taken, cut down | No TAOM procedure existed. The lighting, grading and emissive rules were dropped: TAOM renders only to check a mesh |
| Production brief | Taken, cut down | The budget line names the asset being replaced, not a table |
| Bake symptoms, texel density formula | Taken as labelled text | In the pipeline doc, marked as not yet tested by TAOM |
| Polycount, texture and LOD percentage tables | Skip | Generic numbers. TAOM takes a budget from the asset it replaces, and its LOD ratios are measured |
| Naming (`SM_`, `MAT_`, `COL_`, `_LOD0`) | Skip | TAOM's are `sm_`, `m_`, `t_`, `bo_` and `.lodN`, lower case, and the Kit depends on them |
| Collision proxies (convex pieces, inset) | Skip | What Bannerlord does with a non-convex body is not verified here |
| Unity, Unreal, Godot export; ORM packing; FBX axes | Skip | Other engines. TAOM's FBX settings are proven against the Kit |
| Retopology, UV, hair, rigging, animation, combat timing, quadruped notes | Skip | TAOM's docs are more specific and name required tools. Bannerlord clip timing lives in data |
| Lighting, rendering, compositing, camera, cloth and physics simulation, VFX, geometry nodes | Skip | No consumer in a pipeline that ends at the Kit |
| Style, genre, world, mood and era packs | Skip | One to five lines each in the samples read. TAOM's art direction is Mike's and the artists' |
| Poly Haven, Hyper3D and Sketchfab downloads | Skip | Third-party servers and unrecorded licences |
| The plugin and its MCP config | Not installed | See Security |

One more line was considered and left out: a silhouette comparison after each LOD level. Whether
`tools/lod_fill_batch.py` already covers it was not checked.

## What changed against the plan

The plan Mike approved changed in six places while it was built. Each change is in the feature doc.

- **The inverted-face check has two limits, not one.** The first run failed the approved
  `erebor_dwarf_house_01` at 0.99%: 36 faces of thin trim slabs, wound inward, each with a face
  right behind it. Its views show no hole. A share between 0.1% and 5% is now a major that sends the
  reader to the views, and only a larger share fails the run. The broken canary measures 10.6%.
- **That share is a lower bound.** A flipped wall that sees out through a window is not counted
  (100 of 320 flipped faces on the canary).
- **Two checks were lowered** to match the rule that a blocker needs a recorded failure: an empty
  material slot is a major (inferred, not observed), a non-uniform scale a minor.
- **No `skinned` profile and no `--ref`.** The skin checks run on any mesh with an armature, and
  the budget is one number taken from `probe_fbx.py`.
- **The sixth view looks from below, not a wireframe.** A headless Workbench render draws no wire
  overlay, and undersides hide holes. Wireframes come from the live viewport.
- **No contact sheet.** `tools/blender/harness.py` creates its render folder when imported, so its
  `montage` was not reused, and six files at full size read better than one sheet scaled down.

## Review of the change set

Cycle step 6 ran as a workflow of six agents: three finders (correctness on Opus, consistency and
simplicity on Sonnet), each followed by a skeptic told to refute its findings. They reported 19
findings; the skeptics confirmed 11 and refuted 8. Each confirmed finding was checked against the
code before it was acted on, and all 11 held.

| Finding | Fix |
|---|---|
| The views fitted the mesh to the picture's width, and the picture was only three quarters as tall: a 1 x 1 x 10 box needed 10.0 and got 8.3 | Square pictures; a test names the reason; a tall box rendered whole |
| The sampling path for meshes over 200,000 faces sliced a bmesh sequence with a step, which Blender refuses | `enumerate` with a step; run in Blender with the cap forced low |
| A mesh in an excluded collection or hidden in the viewport crashed a `--blend` run and stretched the view frame | Both tools read visible meshes only and list the rest in `skipped_hidden` |
| The skin check counted every vertex group, so a mask group hid an unweighted vertex and made a fifth influence | Only groups named after a bone count (`skin_counts`, tested) |
| `x.lod1.001` was read as a LOD0 and `bo_x.001` as the body of nothing, which gave a false `body-missing` | The `.001` suffix is set aside before the name is classified, and reported as its own defect |
| LOD0 written `<name>_lod0` was read as a LOD, so it left the budget, the body check and the views | It is the base |
| One loose vertex far away stretched the ray offset past a thin wall | The offset comes from the vertices of faces only |
| A picture left by an earlier run could pass for a new render | The old file is removed first |
| The skin row cited an export gate that is stricter than the check | The row says so |
| The same facts stood in the feature doc and the skill | The feature doc points at the skill for the verdict and the compare loop |
| Three sentences in the skill changed nothing an agent does | Two removed; one rewritten to the part that is particular to a live Blender call |

Refuted, and left as they are: the labelled bake table (Mike asked for the pack's knowledge), the
pointer lines in two owner workflows, the plan-against-build section above, the brief and the
routing table in the skill, the template sections of the feature doc, the depth of the read (stated
above), and the eval case (its unrun state is in Limits).

## Limits

- No skinned mesh was run in Blender. The skin rules have unit tests only.
- Blender was not connected over MCP during this work. The live entry `preflight_scene` and the
  session rules were not run in a live session. The headless path calls the same reader.
- The canary is one FBX and one broken copy of it, on Blender 5.3.0.
- `tools/evals/route-blender-asset/case.yaml` was written and not run: an eval run costs model
  calls and needs Mike's word ([skill-evals.md](../features/skill-evals.md)).
- The audit tool's layers were not checked one by one. The `yara` module was importable when it ran.

---
