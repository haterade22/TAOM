# Erebor design patterns

Composition rules, confirmed conventions, and ready-to-use templates for
assembling erebor buildings from the kitbash by writing prefab XML directly.

## Confirmed conventions (calibrated 2026-04-23)

From iteration 1 of `test_erebor_hut.xml`, a 6-piece 3m × 3m hut that
assembled correctly on the first draft.

| Convention | Confirmed value |
|---|---|
| Coordinate system | Z-up, world units = metres |
| Rotation unit | Radians |
| Z rotation direction | Positive = CCW looking down +Z |
| `sm_dw_ground_3m_a1` pivot | Tile centre, top surface at local Z = 0 |
| `sm_dw_wall_3m_*` pivot | Base-centre — bottom of wall at local Z = 0 |
| `sm_dw_wall_3m_*` footprint | 3m along the wall's local X axis |
| `sm_dw_wall_3m_*` default facing | Outer face points to +Y at rotation 0 |
| `sm_dw_wall_3m_*` height | 3m (top at local Z = 3) |
| `sm_dw_roof_top_a1` | Ridge cap strip (not a flat square) — sits along roof apex |

## Texture-family rule

Letter suffix (`_a`, `_b`, `_c`) = material family. Within one building,
every piece must share the same letter or produce visible texture breaks.
Valid example (A-family): `sm_dw_ground_3m_a1` + `sm_dw_wall_3m_a` +
`sm_dw_wall_3m_door_a1` + `sm_dw_roof_str_a1`.

Across buildings: different letters for different structures is fine — the
texture break reads as "these are two separate buildings".

## Rotation cheat sheet (Z-axis only)

| Rotation (radians) | Result for a wall that defaults facing +Y |
|---|---|
| 0 | Faces +Y (north) |
| −π/2 = −1.5708 | Faces +X (east) — 90° CW looking down |
| +π/2 = +1.5708 | Faces −X (west) — 90° CCW looking down |
| π = 3.14159 | Faces −Y (south) — 180° |

## Template: single-room dwarven hut, 3m × 3m

Working pattern from `test_erebor_hut.xml`.

```
Floor:        sm_dw_ground_3m_a1        (0, 0, 0)      rot (0, 0, 0)
North wall:   sm_dw_wall_3m_a           (0, +1.5, 0)   rot (0, 0, 0)
East wall:    sm_dw_wall_3m_b           (+1.5, 0, 0)   rot (0, 0, -π/2)
South wall:   sm_dw_wall_3m_door_a1     (0, -1.5, 0)   rot (0, 0, π)
West wall:    sm_dw_wall_3m_c           (-1.5, 0, 0)   rot (0, 0, +π/2)
Roof (flat):  sm_dw_roof_top_a1         (0, 0, 3.0)    rot (0, 0, 0)
```

Known issues: walls are A/B/C mixed (texture break at corners), roof is a
ridge cap used as a flat square (reads okay but not architecturally correct).

## Template: single-room hut, A-family unified, corner walls, pitched roof

Upgraded version. Uses matching A-family textures, corner-cleat walls at
joints, and a proper pitched-roof composition.

```
Floor:
  sm_dw_ground_3m_a1                    (0, 0, 0)        rot (0, 0, 0)

Trim border (optional, adds framed edge):
  sm_dw_ground_trim_3m_a1               (0, +1.5, 0)     rot (0, 0, 0)
  sm_dw_ground_trim_3m_a1               (+1.5, 0, 0)     rot (0, 0, -π/2)
  sm_dw_ground_trim_3m_a1               (0, -1.5, 0)     rot (0, 0, π)
  sm_dw_ground_trim_3m_a1               (-1.5, 0, 0)     rot (0, 0, +π/2)
  sm_dw_ground_trim_corner_a1           (+1.5, +1.5, 0)  rot (0, 0, 0)       [NE corner]
  sm_dw_ground_trim_corner_a1           (+1.5, -1.5, 0)  rot (0, 0, -π/2)    [SE corner]
  sm_dw_ground_trim_corner_a1           (-1.5, -1.5, 0)  rot (0, 0, π)       [SW corner]
  sm_dw_ground_trim_corner_a1           (-1.5, +1.5, 0)  rot (0, 0, +π/2)    [NW corner]

Walls (corner-cleat variants for clean joints):
  sm_dw_wall_3m_corn_a  (NW corner)     (-1.5, +1.5, 0)  rot (0, 0, 0)
  sm_dw_wall_3m_corn_a  (NE corner)     (+1.5, +1.5, 0)  rot (0, 0, -π/2)
  sm_dw_wall_3m_corn_a  (SE corner)     (+1.5, -1.5, 0)  rot (0, 0, π)
  sm_dw_wall_3m_corn_a  (SW corner)     (-1.5, -1.5, 0)  rot (0, 0, +π/2)
  — OR replace one side with —
  sm_dw_wall_3m_door_a1  (south face)   (0, -1.5, 0)     rot (0, 0, π)
  — AND/OR add windows —
  sm_dw_wall_3m_win_a1   (window face)  e.g. (0, +1.5, 0) rot (0, 0, 0)

Pitched roof (ridge running east-west):
  sm_dw_roof_str_a1   (N panel sloping down to north)
                                        (0, +0.75, 3.0)  rot (±pitch, 0, 0)
  sm_dw_roof_str_a1   (S panel sloping down to south)
                                        (0, -0.75, 3.0)  rot (∓pitch, 0, 0)
  sm_dw_roof_top_a1   (ridge cap at apex)
                                        (0, 0, 3.0 + peak_h)  rot (0, 0, 0)
  sm_dw_roof_side_a1  (E gable edge)    (+1.5, 0, 3.0)   rot (0, 0, -π/2)
  sm_dw_roof_side_a1  (W gable edge)    (-1.5, 0, 3.0)   rot (0, 0, +π/2)
```

**Unknowns to resolve in iteration 2** of the pitched-roof upgrade:
- Is `sm_dw_roof_str_a1` authored pre-pitched (sits flat but angled geometry)
  or horizontal (needs a rotation to create the pitch)?
- Exact ridge apex height — where does the ridge cap sit relative to
  wall-top (Z=3)?
- Whether corner walls need a specific orientation convention different from
  plain walls.

## Template: 6m × 6m keep (2-storey) — not yet built

Sketch for the next build up from the hut. Validates corner walls at 90°
and introduces the platform system.

- Ground floor (A-family): 4× `sm_dw_ground_6m_a1` tiles or a 2×2 grid of
  `sm_dw_ground_3m_a1`
- Wall course 1 (base, 0–3m): 2× `sm_dw_wall_3m_*` per side + 4× `sm_dw_wall_3m_corn_a` at corners. One wall slot for `_door_a1`.
- Platform (mid-floor, Z=3): 4× `sm_dw_platform_6m_a1` or a 2×2 grid
- Wall course 2 (upper, 3–6m): same layout as course 1, but with `_win_a*` for windows
- Roof (Z=6): pitched roof system, 4-sided hip roof using `_cor_tri_*` at corners + `_top_a1` along ridges

## Template: castle wall section with tower

Sketch for fortifications. Validates the merlon-on-top pattern.

- Anchor tower: `sm_dw_castle_tower_a1` at (0, 0, 0)
- Wall extending east: 3× `sm_dw_castle_wall_a1_str` at (+3, 0, 0), (+6, 0, 0), (+9, 0, 0) — assuming 3m wall width
- Merlons on top of each wall: `sm_dw_castle_wall_a1_str_mrln_01..10` (pick varied) at Z = wall-top
- Gate in the middle: replace one `_str` with `sm_dw_castle_wall_a1_gate`
- Access: `sm_dw_castle_wall_stairs_a1` on interior face
- Ramp up to tower: `sm_dw_castle_wall_a1_ramp` approaching tower base

Unknowns: tower and wall height (3m each? or taller?), exact wall-module
width (3m assumed, needs confirmation).

## Calibration notes — roof system (from 4x8 house iteration 1)

Findings from studying `dwarf_house_a` / `dwarf_house_b` and building
`test_erebor_house_4x8.xml`:

### Roof piece roles (corrected)

| Mesh | Actual role | DON'T use it for |
|---|---|---|
| `sm_dw_roof_str_a1` | Pitched slope panel, 3m wide × 3m deep. Use along all 4 eaves. | — |
| `sm_dw_roof_top_a1` | Ridge cap, 3m long. Sits along the apex at Z = WALL_H + 1.5. | — |
| `sm_dw_roof_side_str_a1` | Under-ridge strip that alternates with `roof_top_a1` to form a continuous ridge. | — |
| `sm_dw_roof_side_cor_out_a1` | **Eave trim** — placed at Z=WALL_H along the wall-top edges | — |
| `sm_dw_roof_cor_tri_a1` | **Interior valley fill** — where two roof slopes meet at an inner 90° corner | ❌ NOT for outer hip corners of a simple rectangular roof |
| `sm_dw_roof_cor_in_*` | **Inside corner** — roof valley fold | — |
| `sm_dw_roof_cor_out_*` | **Outside corner panel** for L-shaped buildings | — |

### Simple rectangular hip roof (the 4x8 pattern)

For a `W × L` tile rectangle with long axis = Y:

1. **Long-side slopes (E + W)**: `sm_dw_roof_str_a1` at Z=WALL_H, every 3m along Y,
   placed at X = ±half_w, rotations ±π/2.
2. **Short-end hip slopes (N + S)**: `sm_dw_roof_str_a1` at Z=WALL_H, every 3m along X,
   placed at Y = ±half_l, rotations 0 and π.
3. **Ridge cap** along long axis: `sm_dw_roof_top_a1` at Z=WALL_H+1.5, X=0,
   spanning the inner length (Y from -(half_l − TILE) to +(half_l − TILE)) —
   **ridge is shorter than the building by one tile on each end** to leave
   room for the short-end hip slopes.
4. **NO `roof_cor_tri_a1` at outer building corners** — this is for interior valley
   corners (like an L-shaped roof plan), not outer hips.
5. **Eave trim** (optional): `roof_side_cor_out_a1` along long-side eaves.

### Hip-roof overhang

Placing `roof_str_a1` AT the wall line (X = ±half_w for E/W) produces a
roughly 1.5m overhang past the wall, because the panel's pivot sits at its
centre. This matches `dwarf_house_b`'s authored roof and is intentional.

### Decorative conventions from `dwarf_house_b`

- Base beams `_wall_beam_3m_b` at Z=0 along the outside of each wall.
- Top beams `_wall_beam_3m_c` at Z=3 along the outside of each wall.
- Column tops (`_wall_clmn_top_*`) with scale `1.1, 1.1, 1.1` or `1.5, 1.5, 1.2`
  at 0.524 rad (30°) rotations at corners.
- `_trim_corner_a1` at Z=4.4 is used INSIDE a roof valley, not along outer eaves.

## Calibration notes — corner pieces, trim studs, roof decoration

From close-up interior + exterior + roof shots of `dwarf_house_b`:

### `sm_dw_wall_3m_corn_a` = DIAGONAL bevel wall, not a 90° corner reinforcement

This is a **45°-rotated wall panel** meant to span a **chamfered corner**.
Using it at a 90° outer corner produces wrong geometry (the diagonal panel
sticks out past the orthogonal walls).

**Correct usage:** replace the last 1.5m of one wall AND the first 1.5m of
the perpendicular wall with a single `corn_a` piece at 45°, creating an
octagonal footprint. The matching floor tile `sm_dw_ground_3m_a1_corner`
fills the chamfered floor area.

**For a purely rectangular building:** do NOT use `corn_a` at all. Let plain
`sm_dw_wall_3m_a/b/c` walls butt up directly at the 90° corners. This is
what the calibration hut used and it worked.

**For an octagonal (chamfered-rectangle) building:** the 4 outer corners
each get a `corn_a` diagonal wall + a `_ground_3m_a1_corner` floor tile.
Rotations appear to follow 0 / -π/2 / π / +π/2 going CW (verify per build).

### `sm_dw_ground_trim_corner_a1` = decorative STUD, not an L-corner tile

Small square capstone / nub. Sits at the grid intersection where two trim
strips cross. **Place at tile-boundary intersections** (at X = multiples of
TILE and Y = multiples of TILE, starting from the floor centre), NOT at
outer corners.

Also used on roofs: appears as the capstone where two decorative roof
strips meet at a roof edge corner. Z should match whatever roof surface
level you're decorating — e.g., Z=3 for eave-level, Z=4.5 for ridge-level,
not the fixed 4.4 I was using.

### Roof decoration system (from close-ups of `dwarf_house_b`'s roof)

The roof isn't just `roof_str_*` tiles — it has an overlay decoration:

1. **Base roof**: `sm_dw_roof_str_*` (pitched slate panels) laid over the building.
2. **Vertical rib strips**: `sm_dw_wall_clmn_3m_*` pieces laid ON the sloped
   roof, running from eave up to ridge. They divide the slope into bays —
   3 strips per slope (one per 3m bay if the slope is 9m wide). Rotations
   follow the slope angle.
3. **Horizontal edge strips**: `sm_dw_wall_beam_3m_*` along the eave line
   (wall-top height), and along the ridge.
4. **Corner capstones**: `sm_dw_ground_trim_corner_a1` studs where strips
   meet at corners (top of each rib, where eave + rib meet, where ridge +
   rib meet).

This is what gives the roof its ribbed / framed look in the close-ups.

### `sm_dw_wall_3m_door_a1` = full door wall with built-in arch

The door mesh includes a carved arched doorframe — don't try to compose an
arch from separate pieces. Just drop the door wall at any wall slot and the
door geometry is built in.

## Update rule

Each time a build iteration reveals something new — a pivot quirk, a
surprising rotation, a mesh whose shape doesn't match the inference — add
the correction to this file. This doc is the persistent playbook that
shortens every future build.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/kitbash/erebor/README.md](./README.md)

<!-- backlinks-end -->
