# Autonomous Overnight Loop — Claude × Blender × Bannerlord Creature Pipeline

> **How to run:** paste the section below ("THE PROMPT") as the kickoff message of an autonomous session, or invoke `/loop` (self-paced, no interval) with it. It is written to be executed by a fresh Claude Code session with the Blender MCP already wired. Cost is not a constraint; optimize for durable capability, not speed.

---

## THE PROMPT

You are running an **autonomous overnight loop**. No human will answer questions until morning. Continue established work; do not stop to ask permission. Optimize for the most durable, correct, *general* capability — token/cost is not a constraint.

### Your mission
Build, prove, and continuously refine a **reusable pipeline + toolkit + knowledge base** for creating **custom creature skeletons and animations** in Bannerlord 1.4.5, driven by Claude Code + the Blender MCP. The spider (`E:\LOTRAOMAssets\ErkamSpider (1).blend`, already open and wired) is proving ground #1. The end goal is a repeatable, documented process that scales to **trolls, rams, goats, chariots, and beyond** — both *building skeletons* and *producing/cleaning animations*. Treat each creature as a lesson that improves the shared toolkit, not a one-off.

### Operating reality — know this cold
- **Blender is live** via the official Blender Lab MCP (`blmcp`, socket `localhost:9876`). Drive it with `mcp__blender__execute_blender_code` (full `bpy`) plus the read/screenshot/render/doc-search tools. The server bundles the Blender 5.1 Python API + manual — use `search_api_docs` / `search_manual_docs` / `get_python_api_docs` **before** writing `bpy`. Never guess an API; look it up.
- **Three HARD MANUAL SEAMS you cannot cross autonomously** — do not fake them, *prepare* them:
  1. The Modding-Kit **FBX → `.tpac` clip compile** is GUI-only.
  2. **In-game testing** needs a human.
  3. **Cascadeur posing** is GUI/manual.
  Everything on either side of these IS automatable. Queue the manual steps into the morning report.
- **Reuse, never reinvent:** `tools/tpac_skeleton_{scan,dump,transplant}.py` (inspect/patch `.tpac` skeleton UserData — Bodies/constraints/Usage), `tools/blender_bone_retargeter.py` (bpy retarget add-on), `tools/extract_fbx_bones.js`, `tools/validate_mesh_refs.py`. The TPAC binary spec + end-to-end workflow are in `docs/tools/spider-skeleton-tpac-tools.md`; the spider feature in `docs/features/spider.md`.
- **Skeleton reality (verified):** the new `.blend` rig is `sp_skeleton` = **59 bones** (mixed-case `Root_M`, `joint40_R`…); mesh `sk_spider_forest_bm_a1` (6 skinned parts). The in-game compiled skeleton resource is `spider_skeleton` = **62 bones**; mesh `sk_spider_forest_c`. (The action_set previously bound a typo'd `erkamspider_skeleton`; fixed to `spider_skeleton` on 2026-06-03 — no tpac ever provided `erkamspider_skeleton`. See `clip-binding-map.md`/`spider-rig-generations.md`.) `.tpac` filename ≠ internal clip name (action_set binds the internal `an_dg_spi_*` name). The new `.blend` has **34 actions**, all real/dense (413 fcurves, 59 bones each); `walk_right` is present and genuine here; `attack_back` is absent; there is no 3rd idle.

### MUST-READ before your first iteration (ground yourself — do not skip)
1. `C:\Users\mikew\.claude\plans\claude-code-and-blender-hazy-flurry.md` (plan + Phase 0 findings + A/B integration fork).
2. `docs/features/spider.md` and `docs/tools/spider-skeleton-tpac-tools.md`.
3. `.claude/rules/evidence-over-claims.md`, `.claude/rules/simplicity-criterion.md`, `.claude/rules/think-before-coding.md`, and CLAUDE.md "Working Discipline → Autonomous-loop stewardship".
4. `MEMORY.md` in the project memory dir for any spider/skeleton/animation feedback.

### The cycle — one iteration, repeated until a stop condition
Keep ONE running log at `docs/research/creature-pipeline/loop-log.md`. Stamp each entry with an iteration number + the output of `date` (via Bash).

1. **SELECT** the single highest-value item from the backlog (or one you discover). One line: what it is + expected gain.
2. **GROUND** — read the exact API/manual/code/asset it touches. Cite what you read. No guessing.
3. **IMPLEMENT — non-destructively:**
   - Blender: operate on a *duplicated* action/object, or save derived results to the workspace. **NEVER overwrite `ErkamSpider (1).blend` or any live `Modules/*` asset in place.** Save new `.blend`/`.fbx` under `E:\LOTRAOMAssets\_auto_workspace\` (create it).
   - Code/docs: new or extended files under `tools/` and `docs/research/creature-pipeline/` only.
4. **VERIFY with evidence** — re-run the relevant metric (the audit script, a `bpy` measurement, `python tools/...`, `python -m pytest tools/tests` for tool code). Capture before→after numbers, and for visual changes a `render_viewport_to_path` screenshot. Apply the **simplicity criterion**: keep only measurable improvements; revert changes that don't help.
5. **RECORD (document everything — this is a first-class goal, not an afterthought)** — append to `loop-log.md`: what / why / evidence (numbers + file paths) / keep-or-revert. **ALSO, every iteration:** any reusable lesson → append to `LESSONS-LEARNED.md` (believed → reality → evidence → rule-it-changes); any measurable gain → append to `IMPROVEMENTS.md` (before → after + evidence). These two ledgers are how the user sees improvements and lessons learned — keep them current. A flawed approach that you reverted is still a lesson worth recording (e.g. a non-discriminative metric).
6. **CRITIQUE (completeness)** — "what's still wrong, what did I not verify, what would make the *pipeline more general*?" Turn the best answer into the next backlog item.
7. **ADVANCE** — next item. If 2 consecutive iterations on one track yield no net gain, switch tracks.

### Guardrails — autonomous, non-negotiable
- **Reversible only.** Allowed: Blender ops on copies; new/edited files under the workspace + `tools/` + `docs/research/`; read-only/analysis/test commands.
- **FORBIDDEN without a human — queue them, do not do them:** `git push`, PRs, branch create/delete; **any commit that stages a file you did not create this loop** (the working tree holds the user's uncommitted *new-factions* work — NEVER `git add -A`/`git add .`; if you commit at all, `git add` only your explicitly-named workspace/tool/doc paths); modifying any live `Modules/LOTRLOME_Armory` (or other game module) asset in place; deleting anything; re-enabling the spider feature; and any edit to `Main/IoC.cs`, `Main/SubModule.cs`, `Main/_Module/SubModule.xml`, the new-factions files, `CLAUDE.md`, ADRs, or `Directory.Build.props`.
- **Never overwrite source art.** Back up before any in-place asset change (you generally should make none).
- **Evidence over claims.** Every "improved / works / done" needs a fresh tool result you actually read. Never invent a number, render, or test result. If you didn't read it, you don't know it.
- **Don't break what builds.** If you touch tool code, run its tests first. Do not touch C# at all.
- **Stay scoped.** Blast radius = the Blender workspace, `tools/` (new/extended pipeline scripts + tests), `docs/research/creature-pipeline/`. Nothing else.

### Seeded backlog — start here, reorder by value, grow as you learn
**Track A — Spider animation quality (the "improve" pillar):**
- Determine whether Bannerlord creature locomotion expects **in-place** vs **root-motion** clips (search the bundled manual + `docs/`), then act: `walk_left/right` carry ~2.36u baked Root_M Z-translation while `walk_1/2`, `run_2`, idles are in-place → produce in-place variants (strip Root_M translation, re-bake) and **measure foot-slide reduction**.
- Fix `sp_walk_right_001`'s anomalous rotation loop-seam (13.4 vs `walk_left` 2.5 — likely a mirror artifact); re-measure.
- Dedup near-identical takes (`walk_1`≈`walk_2`, `run_001` vs `.001`, `attack_front` vs `_001`, `attack_top_001/2/3`): pick canonical by metric, document the choice.
- Close loop seams on cyclic clips; decimate redundant keyframes; smooth jitter — each measured before/after.
- Derive `attack_back` from an existing attack (mirror/retime), export-ready.
- Export-prep every curated clip with the Bannerlord FBX preset (−Y Forward, Z Up, X/Y bone axes, no leaf bones, 30 fps, `_notused` root, one clip per FBX) into the workspace; write the compile checklist for the morning queue.

**Track B — The reusable toolkit (the "build the process" pillar):**
- Make `tools/blender_bone_retargeter.py` headless-callable (module functions: scan→constrain→`nla.bake`) without losing the GUI panel; add a smoke test.
- Author `tools/blender/creature_anim_ops.py`: `audit_actions`, `mirror_action`, `reverse_action`, `retime_action`, `close_loop`, `strip_root_motion`, `decimate`, `export_bannerlord_fbx` — each idempotent, each with a self-check.
- Generalize `classify_bone()` in `tpac_skeleton_transplant.py` toward a data-driven creature table.
- Compute + save the spider 59→62 bone-name mapping; headlessly test baking one action onto the old skeleton.

**Track C — Generalize to new creatures (the "scale to many" pillar):**
- Design a declarative **creature skeleton spec** (JSON: bones, parents, head/tail positions, roll, symmetry, per-group mass/swing for ragdoll) and a `bpy` **scaffolder** that builds an armature from it + an auto-weight first pass (`ARMATURE_AUTO`).
- Validate the scaffolder by round-tripping the spider's own skeleton (spec → armature → diff vs `sp_skeleton`).
- Author starter specs for the easy wins: **chariot** (rigid/mechanical — fully automatable: body + wheels with spin + yoke), a simple **quadruped** (ram/goat: 4 legs ×3-4 joints + spine + head + horns), and note where **troll** can reuse the human skeleton as a base.
- For each: scaffold the skeleton in Blender, auto-weight, render a turntable frame, and queue the human steps (final weight paint, animation, Modding-Kit compile).

### Stop conditions
- Stop when a hard human-blocker dominates every remaining track, OR "loop-until-dry" (3 consecutive iterations across different tracks with no net improvement), OR morning.
- On a genuinely broken assumption, record the outcome and move on — don't grind a doomed approach.

### Deliverable at stop — write `docs/research/creature-pipeline/MORNING-REPORT.md`
- **What improved** — per item: before→after evidence, file paths, screenshots.
- **The toolkit now** — new/extended tools + how to call them.
- **MORNING QUEUE** — exact human-gated steps, ordered: which FBXs to Modding-Kit-compile (with target clip names), what to test in-game, what to pose in Cascadeur, what to commit (with precise `git add` paths — never `-A`).
- **Process lessons** — what you learned about making the pipeline more general (the meta-goal).
- **Recommended next priorities.**

---

## Why it's shaped this way (design notes — not part of the prompt)

- **The cycle = the live audit, generalized.** Select → ground → implement → verify-with-evidence → record-a-lesson → critique → advance. The spider-clip audit (root motion + loop seams) was iteration zero.
- **Honest automation boundary.** Overnight, Claude can do analysis, derivative clip generation (mirror/reverse/retime/loop-close/root-strip/decimate), procedural polish, skeleton *scaffolding* from a spec + auto-weights, export-prep, tooling, and documentation. It cannot compile clips (Modding Kit), test in-game, or hand-pose (Cascadeur) — so those are *queued*, never faked. Chariots are the most fully-automatable (rigid/mechanical, no organic deformation); rams/goats are a clean second proving case; trolls can lean on the human skeleton.
- **Guardrails fit the current repo state.** The tree holds your uncommitted new-factions work, so the loop is forbidden from broad commits and from touching single-owner / factions / config files; its blast radius is a dedicated workspace + `tools/` + `docs/research/`. Worst case overnight = a pile of reviewable artifacts + a report, with nothing irreversible done.
- **Compounding capability.** Every iteration must extract a *process lesson* and fold it back into the toolkit/specs/docs — that's how "improve the spider" becomes "rig any creature."
