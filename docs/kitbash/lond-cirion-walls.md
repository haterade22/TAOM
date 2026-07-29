# Lond Cirion wall kit

Ploppable city-wall sections for the Lond Cirion coastal-city scene, composed programmatically
from the Gondor castle L3 pieces (`AssetSources/Scenes/Gondor/walls/`) in the Minas Tirith
blockout template format. Built 2026-07-28/29 across ~20 in-editor iterations with the user;
every rule below was measured or caught live, not assumed.

**Kit FBX:** `TAOM_Map/AssetSources/Scenes/Gondor/blockout/lond_cirion_wall_a.fbx` — one file,
8 sections × 4 meshes each (base + `.lod3` + `.lod6` + `bo_` collision, single `stone` slot).
**Assembler:** `tools/oneoff/blender_assemble_lond_cirion_wall.py` (headless Blender via the
MS-Store launcher; completion = `E:\LOTRAOMAssets\_export\lond_cirion\wall_01\_report\DONE.txt`,
diagnostics in `log.txt` beside it — `print()` is LOST, the launcher detaches; use `log()`).

## Sections

| Section | Shape | Size | Notes |
|---|---|---|---|
| `lond_cirion_wall_01` | L-corner, two identical arms | ~95×95 m | Achiral: every mirrored variant is an editor rotation — never build mirrors of symmetric sections |
| `lond_cirion_wall_02` | Gatehouse straight (T·w·GATE·w·T) | 78 m | Gate deck tops at exactly 15.0 = the L3 wall deck despite the `l1` name |
| `lond_cirion_wall_03` | Straight run (w·T·w) | 50 m | The chaining piece |
| `lond_cirion_wall_04` | 22.5° kink | ~49 m | Vertex tower hides the bend |
| `lond_cirion_wall_05` | 45° kink | ~48 m | Same joint, sharper |
| `lond_cirion_wall_06` | Recessed gate court | 302×95 m | Wings on the waterfront, legs back, gate face between the leg end-towers; corner doors serve wing+leg, gate-side decks dead-end on blank faces |
| `lond_cirion_wall_07` | Coastal sweep | ~173 m | Four 2-wall runs through three 22.5° kink towers (67.5° total) |
| `lond_cirion_wall_08` | **The assembled ring** | ~600×520 m | Court + straight + sweeps + coast + headland + east run + closure; ~3M tris base with LOD tiers |

**Ring state (deliberate, do not "fix"):** towers only at direction changes and junctions (all
rhythm/mid-run towers removed, runs refilled — see the refill law); the **~222 m siege frontage
is OPEN** between the closure kink's exit and the north run's end — reserved for the engine's
breachable siege-wall entities. Standalone sections 01–07 keep their original tower rhythm; the
ring intentionally diverges.

## Source pieces + registration facts (all measured)

| Piece | Facts |
|---|---|
| `gondor_castle_wall_20m_l3_a` | 20 m, X −10..+10, deck z=15, **outer face +Y**; merlon add-ons `_m1.._m5` (outer) + `_m6` (inner rail) are separate meshes — include them |
| `gondor_castle_wall_tower_l3_a` | 10.8 m square; doors on BOTH ±X faces, centred y=0, threshold z=15 — flush in-line pass-through; full interior (floor + switchback stairs) |
| `gondor_castle_wall_tower_l3_b` | 14.2 m square; doors on ADJACENT +X/−Y faces at z=10 → **place at z=+5** (doors meet the deck, crown aligns with tower a) — the authored corner tower |
| `gondor_castle_gatehouse_l1_a` | 16.4 m wide, deck top 15.0, outer merlons +Y, ground gate tunnel through ±Y; no interior |
| Interiors | `INTERIOR_ROT = {tower_a: 90°, tower_b: 180°}` puts the stairwell descent on the window/plain walls (user decision; the flight's high end edge-clips one door lane — walkable). The interiors carry TWO hazards 90° apart — no rotation clears every door on its own |
| Joints | 0.1 m butt tuck everywhere; kink walls tuck 14.2 m from the vertex (merlon corners stay inside the tower shell); kink chain entry/exit endpoints = (±24.2·cos(half), −24.2·sin(half)) in the kink frame |
| Materials | The source FBXs share material names — Blender renames later imports to `.NNN`, which the editor CANNOT bind (slots bind by exact name; `.001` renders white). The assembler remaps duplicates before joining |

## The three laws (each caught live in-editor)

1. **Chirality.** A handed wall piece (length +X, outer +Y) cannot serve a perpendicular arm by
   rotating with the arm direction — orient outer-consistent FIRST, translate in world space
   after (`T(0,−s)@Rz(90)`, never `Rz(−90)@T(s,0,0)`). The chain walker's `flip` mode encodes
   the two conventions: outer-left-of-travel with right-turning kinks (backward-circuit
   traversal) vs outer-right with left-turning kinks (forward). **A chain recipe needs three
   facts: sequence, heading, AND chirality** — take all three from the user's placed reference.
2. **Refill.** Removing a chained piece leaves its footprint as a hole (a bare tower removal
   shipped 10 m gaps ring-wide). Refill the affected span with evenly-pitched pieces to the same
   endpoints; slack spreads as per-joint tucks (2–3 m is invisible at blockout scale).
3. **Verification.** For composites: the joined tri count must equal the exact sum of embedded
   pieces (catches drops and doubles); the per-section preview render is the accept gate (hide
   the imported SOURCE pieces first — they sit at the origin and z-fight the render; the export
   is unaffected via `use_selection`); the closure overlap audit drops any piece within 12 m of
   pre-existing geometry and logs the count.

## Workflow: adding or changing sections

1. Edit `section_plans()` in the assembler — sections are `[(kind, world-matrix)]` lists;
   compose bigger sections from existing plans (`[(k, A @ m) for k, m in plans[...]]`) or chain
   with `walk(cursor, heading, sequence, flip=)`. The closure solver computes junction legs
   (line intersection; heading deltas are always kink-multiples because every heading is
   22.5°-quantized).
2. Build headless; wait for `DONE.txt`; read the `[closure]`/audit lines in `log.txt`; check
   `preview_<section>.png` and, for ring changes, the top-down scratch render.
3. Re-import in the Modding Kit **with the editor closed** (resources scan at startup;
   re-import overwrites in place). Stale imports masquerade as geometry bugs — two of this
   kit's "defects" were entities from earlier imports or leftover hand-placed sections.

## History

Full iteration record in `CHANGELOG.md` (2026-07-25 → 07-29) and the assembler's comments.
Superseded approaches kept in git history: generated door bridges (rejected for interior
rotation, `6d601808`), sea-anchor + generated ramp sections (deleted per user, recoverable at
`52b600dc`; the kit's `gd_ramp_large_a1` is a dual-lane switchback to a 20 m platform — wrong
rise for the 15 m deck).
