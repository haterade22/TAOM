# Teardown: the torn core at the gap of the broken wall (2026-11-26)

The record of one teardown: what a reference had that our build did not, measured. The techniques that come out of it
become entries of the knowledge store (`docs/features/knowledge-library.md`, issue #1613) when the store is on trunk;
until then the section "Defaults that follow" holds them. The asset job is in
[rivendell-siege-set.md](../rivendell-siege-set.md), the Tripo runs in [tripo-cli.md](../../features/tripo-cli.md).

| | |
|---|---|
| **Part** | The torn core at both ends of the gap of the broken wall `rivendell_castle_wall_20m_a_dest`: the relief of the core between the skins, and the dressed ends of the two skins |
| **Reference** | Two image to 3D models (Tripo, `tripo-v3.1`, 60 credits each), made on 2026-11-25: `torn-face-tall-a-8571328f` for the left flank (`b_tall`; its GLB 1,943,774 triangles, md5 `b6a01fe665412a6011b78d12895aed13`) and `torn-face-irregular-a-5265962e` for the right flank (`irregular_a`; positions only, the file is cut off on the server). Read as height fields on a 0.04 m grid from front passes at 300 px a metre (`tripo\broken_wall_prep\torn\b_tall\hf`, `...\irregular_a\hf`), scaled so that the field's 5.0 m width is the wall thickness (`hf.json`) |
| **Measured against** | Before: stage 4 `build\v08\wall_broken\A_fracture` (02:45, FBX md5 `3c66ef1e671e07b4d6bc51068dc67f07`), the build the eye checker judged. Now: stage 5 `B_fracture` (11:08, FBX md5 `9be14974f453352b1256a243f60a199b`), whose three checkers had not reported. Also the relief our code took from the fields: the plan's `Relief` (0.2 m cells, `tanh` under 0.8 m), the same in both builds (`_plan\plan.pkl`) |
| **Files** | `kit_full\teardowns\_scratch_T3\` in the job's work folder `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\rivendell`: `t3_measure.py` (`t3_measures.json`), `t3_facets.py`, `t3_plateau.py`, `t3_probe_seam.py`, `t3_probe_arris.py`, `t3_probe_ref_arris.py`, each with its `.json`. No Blender job: the meshes were read with the pure FBX reader `rivendell_lod_checks.read_fbx`, the GLB with a numpy reader. The check of this teardown: `kit_full\teardowns\teardowns_check.json` |

The lead drew the fracture faces of stage 4 from numbers, and its eye checker wrote: "the dressed skin stops on a
razor-clean line against a darker core with visible texture patches and streaks" (`_check_eye\s4_check_eye.json`).
The two models' height fields then became the relief of the core (`RELIEF_GROUPS` in `rivendell_breach_layout.py`).
The teardown, on 2026-11-26 from 11:24 to 11:43, asked what of the fields survives into the built faces.

## How the numbers were taken

The section is the core between the skin backs, abs(y) at most 1.95 m (`bp.SKIN_IN`). Our faces were measured over
Z 5.0 to 11.9 m (over the mound, under the deck) on the reference's 0.04 m grid: the heap faces of `.wall` rasterized,
nearest to the gap. A relief is the depth after a best plane is taken off. The bands are Gaussian low passes with the
half power at 5.34 sigma. Paired values are left flank / right flank, or `b_tall` / `irregular_a` for the reference.
Each script writes its arguments into its output.

The verifier measured again and found the same plateau share (0.1795, left 0.0077), depth widths (1.89 / 3.35 m),
triangles and UV stretch. It found the reference slope lower than the teardown: 49.8 / 60.4 degrees against 50.2 /
62.6, and 43.4 / 37.9 on the field with no slope limit; and the reference grey about 4 levels higher than `hf.json`.
Its numbers are used below. It did not measure again the band rms, the shares steeper than 70 degrees, the facet shares, the steps on course lines, the
arris shares or the cost estimate of 33,500 triangles. The course phase is fitted, so the course line share of 0.79 is
an upper estimate. `irregular_a` has no UVs or colour, so the tone and UV numbers rest on `b_tall` alone. These numbers
cannot be made again from the repository: the scripts and builds are in the work folder.

## What was measured

| Entry | The reference has | Ours before, ours now |
|---|---|---|
| `stone-scale-relief-in-a-map` | Relief rms at a wavelength under 0.56 m (one stone) 0.059 / 0.073 m; 0.023 / 0.010 of the relief's variance | A: 0.034 / 0.018 m; share 0.016 / 0.005. The relief our code takes holds 0.016 / 0.018. B: 0.045 / 0.018; share 0.028 / 0.005 |
| `relief-depth-without-a-plateau` | Plane residual p05 to p95 1.60 / 2.97 m wide; depth behind the face p05 to p95 1.89 / 3.35 m; no clamp. Rms at 0.56 to 2.2 m 0.161 / 0.290, over 2.2 m 0.354 / 0.682 | Relief taken: depth 0.77 / 0.78 m (the clamp is 0.8), residual 0.74 / 0.85. Built residual A 1.08 / 1.02, B 1.03 / 0.99. Rms at 0.56 to 2.2 m A 0.097 / 0.096, B 0.099 / 0.085; over 2.2 m A 0.251 / 0.234, B 0.245 / 0.247. Cells within 2 percent of the clamp 0.0077 / 0.1795; by the plateau test's rule (within 1e-6) 0 / 0 |
| `torn-face-steep-facets` | Median slope against the face plane 49.8 / 60.4 degrees (the verifier's; the teardown wrote 50.2 / 62.6). Share steeper than 45 degrees 0.549 / 0.610 (the verifier's; the teardown wrote 0.556 / 0.633), steeper than 70 0.248 / 0.427 | Median A 24.6 / 24.2 (relief taken 23.9 / 21.3), B 20.1 / 21.8. Over 45 degrees A 0.149 / 0.245, B 0.074 / 0.169. Over 70 A 0.022 / 0.003 (taken 0 / 0), B 0.021 / 0.012 |
| `torn-face-no-large-flat-facet` | Area in planar facets (2 degree rule) of 0.1 m2 or more 0.009 / 0.109; of 0.5 m2 or more 0.000 / 0.022 | A: 0.133 / 0.311 and 0.082 / 0.238. B: 0.366 / 0.345 and 0.338 / 0.280 |
| `dressed-band-steps-with-courses` | Seen from the gap, the dressed band at the end is 0.72 and 1.28 / 0.68 and 1.48 m wide at p50 and p95 (`b_tall`); 3.0 / 3.7 steps of 0.12 m or more per metre of height (`irregular_a` 4.7 / 5.5); 0.79 of the steps within 0.08 m of a course line (course 0.651 m; chance 0.25). Core 0.08 to 0.24 m inside the band at p50 0.46 / 0.55 m (`irregular_a` 0.32 / 0.26) | A and B: 0.55 m at every height (the skin back at abs(y) 1.95 m), 0 steps. Core inside the band A 0.20 / 0.23 m, B 0.16 / 0.38 |
| `dressed-end-on-the-stone-material` | Dressed ends light and even against the core: grey 195.8 against 162.0, spread 21.6 against 47.8 (the verifier's; `hf.json` gives 191.8 and 157.2, spread 21.9 and 48.0) | The skin's torn ends are on the heap slot (A 27.1 m2 seen, B 27.5); one texture on both (spread 0.129); only the vertex grey differs, core over end A 0.867, B 0.887 |
| `torn-face-one-uv-run` | UV seams on 0.013 of the torn face's interior edge length (one baked atlas); stretch over 2 on 0.095 of its triangles | A: seams 0.264, stretch 0.185. B: seams 0.198, stretch 0 (largest 1.818) |
| `property-at-each-stage-of-the-chain` | (our method, from this teardown) | No stage reported what of the field survived. The stone scale rms fell from 0.059 / 0.073 m in the field to 0.016 / 0.018 m in the relief taken, between two passing gates |

## What ours has and the reference does not

- **Triangles:** B has 3,669 torn triangles (28.9 a m2) in a `.wall` of 8,917 under its cap of 9,600 (gate G1); A
  3,431 in 8,536. The model is 1,943,774, its torn face 13,172 a m2.
- **A tear along the wall:** our skins end on a drawn outline (B: 0 to 3 kinks, 2 or 3 long runs a skin,
  `s5_tear_result.json`); its knots lie within 0.08 m of a bed joint at 0.24 in A and 0.28 in B (chance 0.29). The
  reference's skins end flat in one plane: the outer 0.3 m at depth p95 -0.001 m.
- **A chipped arris:** B chips 234 of 465 lip edges (`dest_build_report.json`), 0.686 of the arris under 45 degrees;
  A was square (80 to 100 degrees) on 0.905 of its length. On `b_tall` only 0.12 / 0.02 of the rows have a chip of 1 cm
  or more: its arris is square.
- **Exact joints and bodies:** end sections equal the solid wall (G2: 0); the body inside the stone (G10: 0.011 and
  0.012 m against 0.02).
- **A tiling texture at 441 px a metre** (A 423) against the model's unique bake at 213, with no triangle stretched
  over 2 (G11).
- **Cavity tone tied to the relief:** grey against the local hollow (0.4 m ring) correlates at -0.75 / -0.84 in B
  (A -0.83 / -0.88) against -0.55 on the reference (`CAVITY_GAIN`), and the F7 finish blends it to the deck and the cut.

## Not taken into the stage 5 wall

The stone scale relief (it does not fit the triangle cap in geometry); the depth range with no plateau; the steep
facets; the small facets; the toothed band on the courses; the dressed ends on the stone material; the printed numbers
of the chain. Not taken on purpose: the reference's flat skins along the wall and its square arris.

## The properties this teardown measured

| Property | How it was measured |
|---|---|
| Relief by scale | Rms of the plane residual in Gaussian bands (half power at 5.34 sigma): under 0.56 m, 0.56 to 2.2 m, over 2.2 m |
| Depth range | p05 to p95 of the plane residual and of the depth behind the face |
| Plateau | Share of relief cells within 2 percent of a clamp, on both flanks |
| Slope | Angle against the face plane: median, shares over 45 and over 70 degrees |
| Facet size | Area in planar facets (2 degree rule) of 0.1 m2 and 0.5 m2 or more |
| Band at the end | Width of the dressed band from the face, steps of 0.12 m per metre of height, steps near a course line |
| Arris | Share of the arris length that is square (80 to 100 degrees); chipped lip edges |
| Tone | Grey and grey spread of dressed ends against the core; vertex grey ratio; material slot of the end faces |
| UV | Seam share of interior edge length, share of triangles stretched over 2, texel density |
| Cavity | Correlation of grey with the depth against a 0.4 m ring |
| Budget | Triangles of the torn faces, per m2, and of `.wall` against its cap |

## Defaults that follow

The technique files are written when the store is on trunk, one per slug, each with this record as its source. The
states are those of the library's ladder, judged on this job's trees, which are not in the repository, so no row is
above `instance`. `acts against`: a constant or a gate refuses what the reference has. The verifier's findings
(`teardowns_check.json`) replace this teardown's older claims where they differ. A `nothing` row becomes a `sentence`
when its file is written; its next names the step after that. The targets 0.02, 0.4 and 0.15 are this session's, not
the maintainer's word. Code paths are in `build\v07\_src_b\tools\oneoff\`.

| Slug | Default | Mechanism | Where it lives today | State | Next |
|---|---|---|---|---|---|
| `stone-scale-relief-in-a-map` | A torn core carries relief at one stone's scale (under 0.56 m, rms about 0.06 m), in a normal map baked from the field's fine band when geometry cannot hold it | Bumps of one stone throw small shadows, so the core reads as many fragments: 0.059 / 0.073 m on the reference, 0.016 / 0.018 in the relief taken. "One lumpy sheet" is the eye checker's word | Nothing carries it. `RELIEF_CELL` 0.2 in `rivendell_tear.py` (line 277) averages the 0.04 m field into 0.2 m cells, so no wavelength under 0.4 m survives in geometry; a 0.04 m grid would cost about 33,500 triangles a flank (16,781 cells seen) against the cap of 9,600 | nothing | Bake the field's band under 0.56 m onto the torn faces, and the build prints its rms |
| `relief-depth-without-a-plateau` | Take the reference's full depth range; at most 0.02 of relief cells lie within 2 percent of any clamp | Deep pockets read as missing stones; a `tanh` held near its clamp makes a level plateau, read as a sawn face: 0.1795 of the right relief is on it | `RELIEF_CLAMP` 0.8 in `rivendell_tear.py` (line 276), pinned by `test_the_module_keeps_the_numbers_of_the_design` in `test_rivendell_tear.py` (1.6 fails it). The plateau test `test_the_generated_field_gives_a_relief_in_range_with_no_flat_plateau` counts cells within 1e-6 of the clamp and reads the left field only | acts against | The plateau test counts cells within 2 percent of the clamp on both flanks, limit 0.02 (it fails today at 0.1795). Whether a deeper relief passes the body gate G10 and the walk is UNVERIFIED |
| `torn-face-steep-facets` | A torn face is steep: about 0.4 of its area turned more than 45 degrees from the face plane | A facet turned 45 degrees or more is fully lit or fully shaded, so the face reads as broken stone: 0.549 / 0.610 on the reference, 0.074 / 0.169 now | Nothing holds it. The 0.2 m cells, the clamp and `SIMPLIFY_TOL` 0.008 (`rivendell_breach_mesh.py`, line 418) all lower it; no build prints a slope | nothing | `dest_build_report.json` prints the share over 45 degrees of the torn faces; a gate follows at a floor the maintainer confirms |
| `torn-face-no-large-flat-facet` | At most about 0.15 of a torn face's area lies in planar facets of 0.1 m2 or more | A large flat facet shows the tiling texture as a decal and reads as cut: 0.009 / 0.109 on the reference, 0.366 / 0.345 now | Nothing holds it. The decimation `simplify_torn` with `SIMPLIFY_TOL` joins near flat triangles; nothing measures facet size | nothing | `dest_build_report.json` prints the facet share; a gate follows at a cap the maintainer confirms |
| `dressed-band-steps-with-courses` | At a break the dressed skin ends in a toothed band of block ends that steps with the courses | Masonry torn along its joints shows its bond: 3.0 / 3.7 steps a metre on the reference, 0.79 of them on a course line (chance 0.25); ours is one plane with 0 steps, the "razor line" | `SKIN_IN` 1.95 in `rivendell_breach_profile.py`, one constant for both skins over the whole height, read by `cutter_solids` and `core_surface` | acts against | The skin back becomes a function of the course (`bp.course_at`), read by the cutters, the bodies and `VisibleStone` alike |
| `dressed-end-on-the-stone-material` | A torn face of a dressed skin (abs(y) at or over `SKIN_IN`, normal along X) takes the dressed stone material, not the heap's | The eye finds the edge of the dressed wall at the end of the block, not at the arris: grey 195.8 against 162.0 on the reference; ours one texture on both | `core_tone` in `rivendell_breach_finish.py` keeps the stone's vertex tone there (the tone half); every cut face is on the heap slot | nothing | The builder puts those faces on the stone slot |
| `torn-face-one-uv-run` | One UV origin per view axis across a torn face, no mirror, and no heap triangle stretched over 2.0 | A seam between facets shows a jump in the pattern: seams 0.013 on the reference, 0.198 now; stretch over 2 on 0.095, 0 now | `core_uv` in `rivendell_breach_finish.py`; `HEAP_STRETCH_MAX` 2.0 in `rivendell_breach_mesh.py` (line 364), held by gate G11 (one triangle stretched 3 times fails it). The seam share has no gate and no report | instance | A tool in the repository prints the seam share and the stretch share of a mesh's faces |
| `property-at-each-stage-of-the-chain` | Print a reference's property at each stage (reference, prep, what the code takes, the built mesh) beside the reference's value | A number nobody prints cannot hold: the stone scale relief fell to about a quarter (0.059 to 0.016 m) between two passing gates | `t3_measure.py` in `kit_full\teardowns\_scratch_T3`, a scratch script that no build calls | nothing | `dest_build_report.json` prints band rms under 0.56 m, median slope and facet share of the torn faces beside the reference's |
