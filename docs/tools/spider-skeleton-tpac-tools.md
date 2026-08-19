# Bannerlord Skeleton TPAC Tools — Drop-in Reference

A self-contained guide to a set of Python + Node.js tools for inspecting and patching Bannerlord `.tpac` skeleton resources. Built originally for the LOTR-mod spider creature but generalizes to any non-humanoid Bannerlord rig (snakes, dragons, trolls, custom horses, etc.).

**Drop this file into Claude Code (or any LLM agent) and it will have everything needed to use the tools** — URL pointers to the source on GitHub, install commands, the binary format spec, and the workflow.

---

## What these tools solve

Bannerlord's Modding Kit gives you no way to populate `SkeletonUserData` (joint constraints + physics bodies) programmatically. A freshly imported skeleton comes in with:

- `Usage = 'other'` (wrong for mountable / animated creatures)
- All Bodies empty (mass=0, body_type='none', no ragdoll positions)
- Zero joint constraints

To author these manually in the Skeleton Editor, you click through every bone, set lock modes per axis, and tune swing/twist limits. For a 58-bone spider that's 57 joints × ~12 fields = **684 manual settings**. The animator's Maya→Blender→Bannerlord pipeline gets you a working mesh + skeleton + animations, but joint constraints remain a separate manual step.

**These tools let you ship a "Wargs-quality" UserData block in one command**, then refine specific joint limits afterward in the editor if visual testing reveals issues.

---

## Where the tools live

GitHub: `haterade22/TAOM` repo, `bannerlord-1.4.5` branch, `tools/` directory.

| Tool | Browse | Raw download |
|---|---|---|
| `tpac_skeleton_scan.py` | [view](https://github.com/haterade22/TAOM/blob/bannerlord-1.4.5/tools/tpac_skeleton_scan.py) | [raw](https://raw.githubusercontent.com/haterade22/TAOM/bannerlord-1.4.5/tools/tpac_skeleton_scan.py) |
| `tpac_skeleton_dump.py` | [view](https://github.com/haterade22/TAOM/blob/bannerlord-1.4.5/tools/tpac_skeleton_dump.py) | [raw](https://raw.githubusercontent.com/haterade22/TAOM/bannerlord-1.4.5/tools/tpac_skeleton_dump.py) |
| `tpac_skeleton_transplant.py` | [view](https://github.com/haterade22/TAOM/blob/bannerlord-1.4.5/tools/tpac_skeleton_transplant.py) | [raw](https://raw.githubusercontent.com/haterade22/TAOM/bannerlord-1.4.5/tools/tpac_skeleton_transplant.py) |
| `tpac_skeleton_inject.py` — **inject ONE Skeleton from a source tpac INTO a target mesh tpac**, producing a bundled skeleton+mesh tpac (the PROVEN structure every working creature uses). Use when a mesh re-export dropped the skeleton: re-bundle it from the backup into the new mesh tpac. `python tools/tpac_skeleton_inject.py <target_mesh.tpac> <source_with_skel.tpac> <skel_name> <out.tpac> [--dry-run]`. Validate with `tpac_skeleton_scan.py`. Born 2026-06-14 (spider standalone-extract crashed; this is the correct fix). | [view](https://github.com/haterade22/TAOM/blob/bannerlord-1.4.5/tools/tpac_skeleton_inject.py) | [raw](https://raw.githubusercontent.com/haterade22/TAOM/bannerlord-1.4.5/tools/tpac_skeleton_inject.py) |
| `tpac_skeleton_extract.py` — **DEPRECATED, DO NOT USE.** Extracts ONE Skeleton into a STANDALONE skeleton-only tpac. This structure CRASHED the engine (spider 2026-06-14, recursive worker-thread native AV — reused item_guid as package_guid + no creature ships a standalone skeleton tpac). The tool now refuses to run without `--i-know-this-crashes`. Use `tpac_skeleton_inject.py` instead. | [view](https://github.com/haterade22/TAOM/blob/bannerlord-1.4.5/tools/tpac_skeleton_extract.py) | [raw](https://raw.githubusercontent.com/haterade22/TAOM/bannerlord-1.4.5/tools/tpac_skeleton_extract.py) |
| `extract_fbx_bones.js` | [view](https://github.com/haterade22/TAOM/blob/bannerlord-1.4.5/tools/extract_fbx_bones.js) | [raw](https://raw.githubusercontent.com/haterade22/TAOM/bannerlord-1.4.5/tools/extract_fbx_bones.js) |
| `check_fbx_ik.js` | [view](https://github.com/haterade22/TAOM/blob/bannerlord-1.4.5/tools/check_fbx_ik.js) | [raw](https://raw.githubusercontent.com/haterade22/TAOM/bannerlord-1.4.5/tools/check_fbx_ik.js) |
| `blender_bone_retargeter.py` (Blender add-on, separate workflow) | [view](https://github.com/haterade22/TAOM/blob/bannerlord-1.4.5/tools/blender_bone_retargeter.py) | [raw](https://raw.githubusercontent.com/haterade22/TAOM/bannerlord-1.4.5/tools/blender_bone_retargeter.py) |

### Grab all three Python tools in one shot

```bash
mkdir -p tools
cd tools
curl -O https://raw.githubusercontent.com/haterade22/TAOM/bannerlord-1.4.5/tools/tpac_skeleton_scan.py
curl -O https://raw.githubusercontent.com/haterade22/TAOM/bannerlord-1.4.5/tools/tpac_skeleton_dump.py
curl -O https://raw.githubusercontent.com/haterade22/TAOM/bannerlord-1.4.5/tools/tpac_skeleton_transplant.py
```

(Or `wget` if you prefer. Or just clone the repo and copy `tools/`.)

---

## Setup

```bash
# Required: Python 3 + lz4 module
pip install lz4

# Optional (for FBX-side bone counters in Node.js):
cd tools && npm install fbx-parser
```

That's it. No other dependencies. Cross-platform.

---

## Quick start (4 commands)

```bash
# 1. Find the skeleton in your tpac
python3 tools/tpac_skeleton_scan.py path/to/creature_geo.tpac

# 2. Inspect its current state (bones, bodies, constraints)
python3 tools/tpac_skeleton_dump.py path/to/creature_geo.tpac creature_skeleton

# 3. Preview what the patch would do (no writes)
python3 tools/tpac_skeleton_transplant.py path/to/creature_geo.tpac creature_skeleton --dry-run

# 4. Apply the patch (creates .backup automatically)
python3 tools/tpac_skeleton_transplant.py path/to/creature_geo.tpac creature_skeleton
```

After step 4, verify by re-running step 2 — you should see Usage flipped to `'horse'`, Bodies populated with masses, and N-1 D6 joint constraints (one per parent-child link).

---

## Tool reference

### `tpac_skeleton_scan.py` — TOC walker

Stream-reads a `.tpac` file and lists every `Skeleton` asset inside it. Useful when you don't know which pack file your skeleton lives in.

Reports for each skeleton:
- Name + item GUID + file offset
- Metadata size
- Data segment info (SkeletonDefinitionData + SkeletonUserData with their offsets, compressed/uncompressed sizes, and storage format)

```bash
python3 tools/tpac_skeleton_scan.py <path-to-tpac>
python3 tools/tpac_skeleton_scan.py <path-to-tpac> --all-types  # show ALL items, not just Skeletons
```

Runs against any `.tpac` — per-asset compiled files (`*_geo.tpac`) or `AssetPackages/pack*.tpac`. Safe streaming parser that won't drift on 1.3 format (a bug TpacTool-Custom has).

### `tpac_skeleton_dump.py` — Skeleton inspector

Decompresses (LZ4) and parses both data segments of a named Skeleton resource. Reports:

- **Definition:** bone count, full bone list with parent indices, internal skeleton name
- **UserData:** Usage field (`horse`/`human`/`other`), bounding box, every Body (mass, body_type, ragdoll positions), every joint Constraint (D6 / hinge / IK with lock modes and swing/twist limits)

```bash
python3 tools/tpac_skeleton_dump.py <path-to-tpac> <skeleton-name>
# e.g.
python3 tools/tpac_skeleton_dump.py erkamspider_geo.tpac erkamspider_skeleton
```

This is the equivalent of "open the Skeleton Editor and look at every panel" but as text output, scriptable, and diff-friendly. The fastest way to confirm whether your skeleton has populated joints.

### `tpac_skeleton_transplant.py` — UserData patcher

**The headline tool.** Reads a Skeleton's bone hierarchy, generates a complete new SkeletonUserData block (Bodies + Constraints + Usage), re-compresses it via LZ4, and writes it back into the `.tpac` file — fixing all asset offsets in the segment table.

**What it generates:**

- **Usage = `'horse'`** (matches Wargs convention for mountable quadrupeds; required for proper engine animation dispatch)
- **One Body record per bone** — `body_type='abdomen'`, mass per anatomical group
- **One D6 joint constraint per parent-child bone link** — translation locked on all 3 axes, twist locked, swing1+swing2 Limited with anatomy-specific values copied from Wargs's pattern

**Body mass + constraint values per bone group (spider-tuned):**

| Group | Mass (kg) | Swing1 (rad) | Swing2 (rad) | Notes |
|---|---|---|---|---|
| body_axis (Root/spine/chest) | 8.0 | 0.30 | 0.50 | Stiff body |
| head | 3.0 | 0.70 | 1.20 | Look-around freedom |
| fang (chelicerae) | 0.5 | 0.40 | 0.60 | Bite articulation |
| abdomen/stinger | 4.0 | 0.30 | 0.50 | Tail-like bend |
| pedipalp | 0.3 | 0.80 | 1.40 | Articulated mouth parts |
| leg | 0.6 | 0.40 | 0.80 | Quadruped pattern |

All constraints: translation locked on X/Y/Z, twist locked, swing1+swing2 Limited.

**Bone classification is case-insensitive** and handles both naming conventions found in the wild:
- Old lowercase: `root_m`, `joint40_l`, `joint5_r`
- New mixed-case: `Root_M`, `joint40_L`, `joint5_R`

**Usage:**

```bash
# Dry-run — shows what would be written, no edits
python3 tools/tpac_skeleton_transplant.py <path-to-tpac> <skeleton-name> --dry-run

# Real run — writes <tpac>.backup first, then patches in place
python3 tools/tpac_skeleton_transplant.py <path-to-tpac> <skeleton-name>

# Optional: write to a different file instead of patching in place
python3 tools/tpac_skeleton_transplant.py <path-to-tpac> <skeleton-name> --out <new-tpac-path>
```

**Safety guarantees:**

- Always creates a `.backup` of the original tpac before mutation (one-shot — won't overwrite an existing backup)
- Rewrites the entire data section and updates ALL segment offsets in the TOC, so growing/shrinking the UserData block doesn't corrupt sibling segments (mesh, other animations) in the same tpac
- Dry-run mode shows the diff in advance

### `extract_fbx_bones.js` + `check_fbx_ik.js` — FBX-side bone counters

Node.js scripts using the npm `fbx-parser` package. Reads a binary FBX and reports:

- All `LimbNode` / `Limb` / `Null` bone-like models
- Bone hierarchy (depth-first walk via FBX OO connections)
- IK structures: `IKEffector` model types, FbxConstraint nodes, IK-named custom properties on bones

```bash
cd tools && npm install fbx-parser
node tools/extract_fbx_bones.js <path.fbx>
node tools/check_fbx_ik.js <path.fbx>
```

Useful for verifying bone counts match between an animation FBX and the skeleton FBX before import. Empirically confirms that vanilla creature FBXs (warg, spider) do NOT carry FBX-level IK structures — IK joints are authored post-import in the Modding Kit, not in the source FBX.

---

## Adapting `classify_bone()` for non-spider creatures

The transplant tool's only creature-specific logic lives in one function: `classify_bone(name)` in [`tpac_skeleton_transplant.py`](https://github.com/haterade22/TAOM/blob/bannerlord-1.4.5/tools/tpac_skeleton_transplant.py). The pattern is:

```python
def classify_bone(name: str) -> dict:
    n = name.lower()
    if n in ('root_m', 'spine1_m', 'spine2_m', 'chest_m'):
        return {'group': 'body_axis', 'body_type': 'abdomen', 'mass': 8.0, 'swing1': 0.30, 'swing2': 0.50}
    # ...etc for each anatomy group
    return {'group': 'unknown', 'body_type': 'abdomen', 'mass': 1.0, 'swing1': 0.50, 'swing2': 1.00}
```

To port to a new creature:

1. Run `tpac_skeleton_dump.py` against the new skeleton to get the full bone list.
2. Group the bones by anatomy (spine vs head vs limbs vs tail vs accessories).
3. Replace the spider-specific patterns with prefix-match rules for the new rig.
4. Tune mass + swing values per group — Wargs's pattern is a reasonable starting baseline (`locked / locked / locked / locked / limited / limited` axis locks + swing limits in radians, smaller = stiffer joint).
5. Dry-run and check the bone classification summary — every bone should land in a named group, none should be `unknown`.

The rest of the tool (TOC parser, LZ4 encoder, segment offset updater) is creature-agnostic and doesn't need changes.

---

## TPAC binary layout for Skeleton resources

Reverse-engineered from decompiling `TpacTool.Lib.dll` (the open-source TpacTool's parser, MIT-licensed). Reproduced here so you don't have to.

### Container header (offset 0)

```
magic              : uint32 (4 bytes — 0x43415054 = 'TPAC' little-endian)
version            : uint32 (4 — currently 2 across all Bannerlord 1.3.x tpacs)
package_guid       : 16 bytes
num_items          : uint32
padding            : 8 bytes (2 unused uint32s)
```

### Each AssetItem (loops `num_items` times after header)

```
type_guid          : 16 bytes (Skeleton = c635a3d5-eabb-45dd-883e-aa57e4196113)
item_guid          : 16 bytes
item_version       : uint32 (only if container version > 1)
name               : sized_string (int32 length prefix + UTF-8 bytes)
metadata_size      : int64
metadata           : metadata_size bytes (asset-type-specific)
checksum           : int64
segment_count      : int32
N × segment_header {
    offset         : uint64 (absolute file offset where this segment's data starts)
    actual_size    : uint64 (uncompressed size)
    storage_size   : uint64 (compressed size on disk; equals actual_size if uncompressed)
    owner_guid     : 16 bytes
    type_guid      : 16 bytes (segment-type — SkeletonDefinitionData / SkeletonUserData / etc.)
    unknown_ulong  : uint64
    unknown_uint   : uint32
    storage_format : uint8 (0 = uncompressed, 1 = LZ4HC)
}
udep_count         : int32 (UnknownDependences)
udep_count × { 16 + 16 + 16 = 48 bytes per entry }
```

Segment data lives at the absolute `offset` recorded in the segment header — typically after all the TOC entries in the file. Multiple segments can interleave in any order.

### Segment TYPE_GUIDs (interpret `type_guid` bytes as little-endian .NET Guid)

| GUID | Type |
|---|---|
| `c635a3d5-eabb-45dd-883e-aa57e4196113` | Skeleton (asset) |
| `11d07d37-e720-406b-ab67-c846f96a8771` | SkeletonDefinitionData (segment) |
| `9b6ac06d-a546-40af-a555-40d301ab4b2f` | SkeletonUserData (segment) |
| `5fce4668-0596-c44b-8db2-1edaa9408411` | Geometry (segment) |

(.NET serializes Guids with the first three fields in little-endian byte order and the last 8 bytes in big-endian — be careful when comparing raw bytes.)

### SkeletonDefinitionData payload (parsed by `tpac_skeleton_dump.py`)

```
name               : sized_string
bone_count         : int32
N × BoneNode {
    name           : sized_string
    parent_idx     : int32 (-1 if root)
    rest_frame     : 16 × float32 (4×4 matrix)
}
```

### SkeletonUserData payload

```
bb_padding         : float32 (4 bytes)
bb_min             : vec4 (16)
bb_max             : vec4 (16)
usage              : sized_string ('horse' / 'human' / 'other')
unknown_string     : sized_string
unknown_guid       : 16 bytes
body_count         : int32
N × Body {
    bone_name              : sized_string
    enable_blend           : bool (1 byte)
    type                   : sized_string
    body_type              : sized_string ('abdomen' / 'none' / etc.)
    mass                   : float32
    ragdoll_position1      : vec4
    ragdoll_position2      : vec4
    ragdoll_radius         : float32
    collision_position1    : vec4
    collision_position2    : vec4
    collision_radius       : float32
    collision_max_radius   : float32
}
unknown_int        : int32
constraint_count   : int32
M × Constraint {
    skipped            : uint32 (padding/seq)
    type               : sized_string ('d6' / 'hinge' / 'ik')
    name               : sized_string (convention: 'joint_<parent>_<child>')
    bone1              : sized_string (the child bone in the parent→child relationship)
    bone2              : sized_string (the parent bone)
    entity_space_rot   : quat (16 bytes — identity = 1, 0, 0, 0)
    position           : vec4 (16)
    // Type-specific tail (variant payload):
    if type == 'd6':
        axis_lock_x         : sized_string ('locked' / 'limited' / 'free')
        axis_lock_y         : sized_string
        axis_lock_z         : sized_string
        twist_lock          : sized_string
        swing1_lock         : sized_string
        swing2_lock         : sized_string
        axis_limit          : float32
        twist_lower         : float32
        twist_upper         : float32
        swing1_limit        : float32 (radians)
        swing2_limit        : float32 (radians)
    if type == 'ik':
        unknown_uint        : uint32
        swing1_limit        : float32
        swing2_limit        : float32
        twist_lower         : float32
        twist_upper         : float32
    if type == 'hinge':
        unknown_float1      : float32
        unknown_float2      : float32
}
```

Both segments are LZ4HC-compressed in practice (storage_format byte = 1). Decompression: `lz4.block.decompress(compressed_bytes, uncompressed_size=actual_size)`.

### Why TpacTool-Custom fails on 1.3 (heads-up)

The public TpacTool's `Skeleton.ReadMetadata()` override reads exactly 21 bytes (uint32 + bool + Guid) regardless of the declared metadata_size. If a future Bannerlord update bumps the per-asset metadata format (e.g., adds new fields), the extra bytes leak into the next read (checksum + segment_count), corrupting every subsequent asset offset in the same pack. Our Python parser explicitly bounds reads by the declared sizes, so it's resilient to format growth.

---

## What this tooling DOESN'T solve

The tools handle SkeletonUserData (Bodies + Constraints + Usage). They do NOT:

- **Create Animation Clip wrappers.** Those come from the Modding Kit's animation import flow. If your action_set is silent in-game (e.g., "Action set X could not be found"), that's a missing Clip wrapper, not a missing constraint.
- **Set bone-name conventions on the FBX side.** The `_notused` suffix on animation FBX root bones is a separate convention enforced by your DCC export (Blender / Maya). See [`tools/blender_bone_retargeter.py`](https://github.com/haterade22/TAOM/blob/bannerlord-1.4.5/tools/blender_bone_retargeter.py) for an animation-retargeting Blender add-on that handles the cleanup step.
- **Replace Skeleton Editor work for fine tuning.** Defaults are a "Wargs baseline." A skilled rigger eyeballing each bone in the editor can tighten swing limits further (especially for legs and head) for better visual results — load the patched tpac in the editor, tune what looks wrong, Save, done.

---

## End-to-end pipeline

The full workflow (animator's 5-step pipeline + this tooling):

1. Animator delivers `creatureX.fbx` via Maya → Blender pipeline (clean transforms, `_notused` skeleton suffix on animation FBXs, etc.)
2. Erkam/Nexxer imports in Modding Kit → produces `Assets/creatures/X/creatureX_geo.tpac` with the skeleton resource registered, but with empty UserData
3. **Run this tooling:**
   ```bash
   # Inspect what the kit gave us
   python3 tools/tpac_skeleton_dump.py creatureX_geo.tpac creatureX_skeleton

   # Preview the patch
   python3 tools/tpac_skeleton_transplant.py creatureX_geo.tpac creatureX_skeleton --dry-run

   # Apply it (creates .backup automatically)
   python3 tools/tpac_skeleton_transplant.py creatureX_geo.tpac creatureX_skeleton

   # Confirm
   python3 tools/tpac_skeleton_dump.py creatureX_geo.tpac creatureX_skeleton
   ```
4. Wire the skeleton into the engine via `monsters.xml` / `action_sets.xml` (ensure `skeleton="creatureX_skeleton"` matches)
5. Test in Custom Battle — refine joint limits in Skeleton Editor if any specific bone misbehaves

---

## Origin / credits

- Built for **TAOM** (Tales From The Age of Men), a LOTR total-conversion mod for Mount & Blade II: Bannerlord 1.4.5.
- Source repo: [github.com/haterade22/TAOM](https://github.com/haterade22/TAOM) (`bannerlord-1.4.5` branch)
- TPAC binary format reverse-engineered from [szszss/TpacTool](https://github.com/szszss/TpacTool) decompilation (MIT-licensed C# source).
- Tools developed in collaboration with Claude Code during iterative spider-skeleton debugging sessions in April–May 2026.

If your project uses a creature with substantially different anatomy (snakes, dragons, gigantic four-armed humanoids, etc.), the only thing that needs to change is `classify_bone()` in `tpac_skeleton_transplant.py` — replace the spider patterns with rules matching your bone names, keep the same return dict shape.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reference/provenance-register.md](../reference/provenance-register.md)

<!-- backlinks-end -->
