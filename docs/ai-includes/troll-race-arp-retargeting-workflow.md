# Troll race + Auto-Rig Pro humanoid-retargeting workflow

How to give a humanoid **race** (the troll) a full animation set by **retargeting Bannerlord human
animations onto an Auto-Rig Pro (ARP) rig**, then exporting/compiling/binding it as a standalone race.
This is the RACE counterpart to the creature-MOUNT workflow in
[creature-animation-blender-mcp-workflow.md](creature-animation-blender-mcp-workflow.md) — mounts are
hand-authored quadrupeds; a humanoid race reuses the human library via retargeting.

> **Status (2026-06-14):** BOTH the **extraction** (tpac→FBX) and the **retarget** halves are PROVEN
> on Blender 5.1.2 (human walk → troll, 72 moving fcurves, clean run). Extraction is tooled
> (`tools/extract_human_anims_tpac.ps1`) but **headless-UNSTABLE** (assimp access-violation — see
> Hard boundaries); source clips via the Modding Kit instead. The **export → compile → bind → in-game**
> half is gated on the Kit GUI + the game. Separately, the `cave_troll` race (`human_skeleton` + the
> human clip library) is **ENABLED + SHIPPING today** as the working troll — see
> [troll-race.md](../features/troll-race.md). The bespoke-skeleton set here is the quality refinement.

## Hard boundaries (what is NOT scriptable / autonomous)

| Step | Why it's a hand-off |
|---|---|
| **ARP Game-Engine FBX export** | ARP's export op calls nested `bpy.ops.object.select_all` that **poll-fails under a scripted `temp_override`** (confirmed 2026-06-13). Run it from the 3D-view **ARP → Export** tab. Settings confirmed below. |
| **FBX → `.tpac` compile** | The Modding Kit's resource importer turns an FBX into a `SkeletalAnimation` master (`_geo.tpac`) + the skeleton tpac. `TpacTool.Lib` can clone/edit *clip metadata* (`_anm.tpac`) but cannot bake FBX curves into a SkeletalAnimation. GUI-only. |
| **In-game test** | Launch + Custom Battle. Yours. |

Everything **above** those lines — source acquisition, retarget, the bone map, the race data — is scriptable.

## Auto-Rig Pro: install + license

- ARP is a **paid Blender Market addon under GPL-2.0**. **Do NOT commit ARP source into the TAOM repo.**
  Install it separately; we ship an installable zip at
  `E:\LOTRAOMAssets\Auto-Rig Pro v3.78.10\auto_rig_pro.zip` (built from the `auto_rig_pro-master`
  checkout, hyphen-free top folder). Install via **Preferences → Add-ons → ▾ → Install from Disk**.
- **Confirmed working on Blender 5.1.2** with ARP 3.78.10 (`bl_ext.user_default.auto_rig_pro`, 167 ops),
  even though its manifest only declares 4.2+ (the `blender_version_max` is commented out).
- We commit only OUR config/scripts: `tools/blender/arp_retarget.py` (the retarget driver) and
  `tools/blender/bannerlord_human_to_troll.bmap` (the saved bone map).

## The troll as a humanoid RACE

Bannerlord humanoid races (dwarf/orc/troll) are defined by: a `Monster` with `monster_usage="human"`,
an `action_set` (combat/movement), a `skin` (skeleton + meshes), a `BodyProperty`, and an NPCCharacter
troop with `race="<id>"`. The race string→int id comes from the engine's `FaceGen.GetRaceNames()`;
TAOM's `RaceManager` caches it (no C# change needed to add a race the engine already lists).

LOTRLOME_Armory already ships **`hill_troll`** (`skeleton="troll_skeleton"`, mesh `mordor_hill_troll`)
and **`cave_troll`** (`skeleton="human_skeleton"`, mesh `lotr_troll_body`) — both `monster_usage="human"`
with action sets `base_set="as_human_warrior"` (they *inherit* human clips for free). TAOM has a
`cave_troll` troop + `BodyProperty.fighter_cave_troll`, currently **disabled** (2026-05-14) — that is the
ready-made template for the race data (see [troll-race.md](../features/troll-race.md)).

**Design decision (2026-06-13): the troll is a SEPARATE race with its OWN skeleton + a full bespoke
clip set** (NOT `base_set="as_human_warrior"` reuse). That frees the skeleton (the ARP-derived deform
bones need not match human bone names) at the cost of compiling a full clip set — which ARP retargeting
makes feasible by sourcing every clip from the human library.

## The pipeline

### A. Source human animations into Blender  *(gate; choose one)*
- **Modding-Kit export (recommended, reliable):** in the Kit, select human `SkeletalAnimation` clips →
  Export FBX. Start with a core wave: idle / walk / run / turn + a few attacks + death.
- **tpac extract (BUILT — `tools/extract_human_anims_tpac.ps1`; PROVEN-correct but headless-unstable):**
  the human `Skeleton` (`human_skeleton`, GUID `dd7f3586-…`) lives in
  `Native/EmAssetPackages/human/human.tpac` — **NOT** `AssetPackages/skeletons.tpac` (which holds only
  animal/prop skeletons + `human_low_skeleton`). The clips are in `AssetPackages/animations.tpac`
  (**569 MB**). All Native tpacs share ONE package GUID, so `AssetManager` holds only one at a time —
  load human.tpac eagerly (skeleton + body mesh) and add animations.tpac as the lazy resolver
  (`AddPackage` + `SetAsDefaultGlobalResolver`). Export with the concrete `FbxExporter` (set
  `.Skeleton`/`.Model`/`.Animation`/`.FixBoneForBlender=true`, then `.Export(path)`).
  **A MESH is mandatory** — a mesh-less FBX imports the bones as static *empties* with no animation;
  pass `body_male_a` with each `Mesh.Material`/`SecondMaterial` **cleared** (an in-memory dummy
  `AssetDependence<Material>`, since the body's real materials live in another tpac and would throw
  `ResolveFailedException`). Also skip `Duration==0` clips (static single-pose, e.g. `anim_stand_idle`).
  A walk exported this way imported as a 28-bone armature with **269 fcurves** — the chariot "Takes"
  risk did NOT materialize, and the 569 MB load is fine.
  **⚠ Caveat — headless export is unreliable:** TpacTool's assimp `ExportSceneToBlob` access-violates
  (`0xC0000005`) when driven from a script in BOTH pwsh-7 (.NET 10) and Windows-PowerShell-5.1
  (.NET Framework). A few single exports succeeded early, then it crashed deterministically. **Prefer
  the Modding-Kit resource exporter** (runs assimp in its own stable process). RCA: this session 2026-06-14.
- A human source FBX already on disk: `E:\LOTRAOMAssets\human_skeleton_with_male_body.fbx` is the
  canonical 28-bone `human_skeleton` rest rig; the `elephant_rider_*.fbx` clips are human_skeleton
  *animations* (mounted poses — fine to validate the pipeline, wrong content for a standing troll).

### B. Retarget human → troll  *(PROVEN — scriptable)*
Use `tools/blender/arp_retarget.py`:
```python
exec(open(r'C:\Users\mikew\source\repos\TAOM\tools\blender\arp_retarget.py').read(), globals())
src = import_source_fbx(r'...\some_human_clip.fbx')          # -> 'human_skeleton'
act = retarget(src, 'rig', frame_start=1, frame_end=62)     # bakes onto the troll rig
```
`retarget()` sets ARP's source/target rigs, runs `build_bones_list`, applies the **corrected**
`HUMAN_TO_TROLL_FK` map, and bakes. ARP's auto-guess gets several bones WRONG — the driver fixes them:

| Source (human_skeleton) | Troll ARP control bone | ARP auto-guess was |
|---|---|---|
| pelvis (root) | `c_root.x` | c_root_master.x |
| spine / spine1 / spine2 | `c_spine_01.x` / `_02.x` / `_03.x` | empty / empty / c_spine_02.x ❌ |
| neck / head | `c_neck.x` / `c_head.x` | ok |
| l/r_clavicle | `c_shoulder.l/r` | ok |
| l/r_upperarm_twist | `c_arm_fk.l/r` | ok |
| l/r_foretwist | `c_forearm_fk.l/r` | c_foot_ik.l ❌ |
| l/r_hand | `c_hand_fk.l/r` | ok |
| l/r_thigh / calf / foot / toe0 | `c_thigh_fk` / `c_leg_fk` / `c_foot_fk` / `c_toes_fk` (.l/r) | ok |
| l/r_finger0, *_twist1 | *(cleared — not retargeted)* | c_spine_01.x ❌ / various |

The map is also saved as `tools/blender/bannerlord_human_to_troll.bmap` (re-import via ARP's
`import_config`). All targets are the **FK** controllers (simplest, no IK foot-planting); revisit IK feet
(`c_foot_ik`) later if locomotion slides. **Root motion:** `set_as_root` on pelvis is set but didn't
persist in the first pass — handle root extraction deliberately for movement clips.

**Three more hard-won retarget fixes the driver now encodes (RCA 2026-06-14) — a flat or crashing
retarget is almost always one of these:**
1. **DROP unmapped bones_map entries — don't blank them.** Setting an entry's target to `''` makes ARP
   try to create a tweak bone for an empty target → `AttributeError: 'NoneType' has no attribute 'name'`
   during *Creating Bones*. The driver REMOVES the twist/finger entries from `bones_map_v2` instead.
2. **Assign the source action's `action_slot`** (Blender ≥4.4 slotted actions). An imported FBX assigns
   the action but NOT the slot, so the source armature evaluates to its rest pose during ARP's bake →
   a 198-fcurve `_remap` that is entirely **flat** (zero motion). Verify with `action_motion_count()`.
3. **Override with window/area/region ONLY — never pin `active_object`.** ARP's retarget switches the
   active object internally (`set_active_object(source)` then `(target)`); a `temp_override(active_object=…)`
   overrides those switches, so ARP edits the WRONG armature → `eb=None` *Creating Bones* crash AND a
   `mode_set('POSE')` "Toggle Pose Mode" error that aborts the unbind and leaves a stuck
   `target_rig["arp_retarget_bound"]=True` flag (every later bake then re-bakes a static bind). The
   driver also defensively clears that flag before binding.

### C. Export the troll deform skeleton + baked clip  *(ARP UI hand-off)*
Select the troll `rig` → **ARP → Export**, with: `arp_export_rig_type = UNIVERSAL` (export the deform
bones as-is — the troll's own skeleton), `arp_engine_type = OTHERS`, `arp_bone_axis_primary_export = Y`,
`arp_bone_axis_secondary_export = X`. Output FBX carries ONLY the clean 30-bone deform skeleton
(`root.x`, `spine_01.x`, `arm_stretch.l`, `thigh_stretch.l`, …) + the baked action.
*(Scripting this hit the poll wall above; a custom deform-bake exporter is a candidate future tool.)*

### D. Compile → bind → register  *(Kit + data)*
1. **Kit:** import the deform FBX → `troll_skeleton` (the troll's game skeleton tpac) + import each clip
   FBX → `SkeletalAnimation` `_geo.tpac`.
2. **Clips:** author `_anm.tpac` per clip (flags + frame range) — the `_clipgen` `TpacTool.Lib`
   pattern (clone + re-point `.Animation` to the new master GUID; wire the SkeletalAnimation `Skeleton`
   GUID). Movement clips that drive a `quadrupedal`/mount usage need `quad_movement`; a bipedal humanoid
   uses the standard human movement codes (no `quad_movement`).
3. **Bind:** `as_troll_warrior` (standalone action set, `skeleton="troll_skeleton"`,
   `movement_system="bipedal"`) maps each `act_*` to the troll clips.
4. **Register:** see the race-authoring recipe in [troll-race.md](../features/troll-race.md).

## Proven prototype (2026-06-13)
- ARP enabled on Blender 5.1.2 (167 ops). Imported the 28-bone `human_skeleton` + a real action.
- Built + corrected the human→troll FK map; `arp.retarget` baked **198 fcurves** onto the troll `rig`.
- Troll deforms (`hand.l` Δ1.55, `foot.l` Δ1.23 across the cycle). Saved to
  `E:\LOTRAOMAssets\troll_anim_WORK_20260613.blend` (`troll_rig_01.blend` untouched).

## Reusable assets
- `tools/blender/arp_retarget.py` — retarget driver (import source, build+correct map, bake, save .bmap).
- `tools/blender/bannerlord_human_to_troll.bmap` — the saved bone map.
- `E:\LOTRAOMAssets\Auto-Rig Pro v3.78.10\auto_rig_pro.zip` — installable ARP package.
- `E:\LOTRAOMAssets\troll_anim_WORK_20260613.blend` — the proven prototype scene.

## Hand-off checklist
1. Install + enable ARP (zip above). 2. Source the human clips (Kit export). 3. Run `arp_retarget.py`
per clip (or batch). 4. **ARP → Export** each (settings in C). 5. Kit-compile FBX → tpac (skeleton +
clips). 6. Author `as_troll_warrior` + the race data ([troll-race.md](../features/troll-race.md)).
7. Custom-Battle test.
