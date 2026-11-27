# The great gate of Erebor: design language and siege architecture v1

Draft written 2026-11-26 on branch `feat/erebor-gate` (`E:\repos\taom-erebor-gate`, base `550ae504c`) from the owner's
ten local references (`E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\erebor_gate\refs\owner_2026-11-26`), the live
Erebor kit measurements, and the settlement siege contract (`docs/kitbash/settlement-scene-needs.md` in
`E:\repos\taom-kingdom-kits`). This document is a design and engineering proposal for round 1 massing; the owner
decides every look choice.

## 1. Architectural reading of the references

Every picture of the primary front (`ref_01`, `ref_03`, `ref_04`, `ref_07`, `ref_08`, `ref_09`, `ref_10`) describes one
strictly symmetric, monumental fortress front carved straight into a recessed vertical cleft of a craggy mountain peak.
`ref_05` and `ref_06` show a compact multi-gallery reading of the same vocabulary with bronze knotwork inlay. `ref_02`
shows an alternate finned, pointed-gable concept with broad stepped terraces and dry-stone retaining walls.

Measured from the straight-on elevation (`ref_04`, 634 x 814 px, aspect 0.7789; symmetry axis at X = 47.5%, ground line
at Y = 86.5%, carved crown at Y = 24.5%, mountain peak at Y = 3.5%), checked against the oblique views (`ref_01`,
`ref_03`, `ref_07`, `ref_09`, `ref_10`):

| Zone (bottom to top) | Y band in `ref_04` | Share of carved height | Architectural anatomy |
|---|---:|---:|---|
| Ground and approach | 86.5% to 97.0% | below grade | Straight stone causeway crossing rocky forecourt and stream to the central threshold (`ref_03`, `ref_04`, `ref_10`) |
| Base apron and main door | 86.5% to 81.5% | 8.1% | Low fortified wall with close vertical ribs, carved sentinel piers flanking a recessed central double door (`ref_01`, `ref_09`) |
| Lower three-bay wall | 81.5% to 72.5% | 14.5% | Three flat recessed bays between wide monolithic vertical piers, pierced by two to three horizontal rows of square embrasure slots |
| Lower canted arches and outer bastions | 72.5% to 64.5% | 12.9% | Center chevron-flat arch with a crenellated sill gallery; twin taller stepped hexagonal flanking arches; projecting outer corner bastions with flat decks (`ref_09`) |
| First horizontal gallery and crest | 69.5% to 58.5% | 17.7% | Continuous fine-crenellated horizontal ledge across the entire front; central interlaced geometric crest topped by a tall crowned arched niche |
| Middle cleft and lattice towers | 58.5% to 39.5% | 30.6% | Deep narrow central vertical cleft between twin stepped monolith piers; flanking twin vertical bays with pointed lower arches, interlocking diamond stone lattice grilles, and upper slot rows; deep outer recessed arched chambers framed by giant diagonal stone ribs |
| Upper crown citadel | 39.5% to 24.5% | 24.2% | Narrowed central stepped pinnacle bay tucked deep into the rock arch, banded horizontal gallery colonnade, stepped trapezoidal crown portal |
| Enveloping mountain peak | 87.5% to 3.5% | 133.9% | Rough fractured brown-grey mountain cliff wrapping forward around both sides to create a deep recessed canyon and closing in a jagged crag above the crown (`ref_04`, `ref_07`) |
| Flanking colossal warrior statues | 85.0% to 64.0% | 33.9% | Twin kneeling dwarf warriors on stepped octagonal plinths, projecting well forward of the facade plane and facing inward toward the causeway: hexagonal scale skirts, banded shoulder plates, faceted geometric helms, braided beards, massive double-bitted two-handed axes (`ref_08`, `ref_09`). Helm tops align with the first horizontal gallery. `ref_01` shows the right statue collapsed and tilted; `ref_03` and `ref_04` show both whole |

Key geometric ratios from `ref_04` (correcting horizontal fractions by aspect 0.7789):

- Carved facade width (between cliff walls, X = 24.0% to 71.0%) to carved height (62.0% H): **0.59 : 1** (height is 1.70
  times width).
- Three-bay inner width (X = 31.0% to 64.0%) to total carved facade width: **0.70 : 1** (outer corner bastions add 15%
  per side).
- Statue helm height above plinth (21.0% H) to first horizontal gallery height (22.0% H): **0.95 : 1**.
- Total mountain base width across statues and cliff shoulders (100% W) to mountain peak height (83.0% H): **0.94 : 1**.

## 2. Measured colour palette and material comparison

Sampled from clean cropped regions of the owner's references (excluding viewer icons and marks; script
`erebor_gate/_tools/measure_refs.py`, results in `control/ref_and_control_measurements.json`). Image medians reflect
lighting and grade; calibrated engine albedos are tuned in the Modding Kit later.

| Material role | Reference sample | Rect (px) | Median sRGB | Notes |
|---|---|---|---:|---|
| Carved facade stone (sunlit) | `ref_01` | `[329, 257, 361, 300]` | `#d2e7c2` | Distinct verdigris green-grey shift (G > R > B) |
| Carved facade stone (midtone) | `ref_04` | `[278, 529, 317, 586]` | `#3a3b31` | Dark weathered green-slate granite |
| Carved facade stone (painted trim) | `ref_06` | `[463, 469, 543, 526]` | `#5c5548` | Cool sage-grey carved stonework |
| Deep shadow recess / cleft | `ref_04` | `[291, 341, 310, 439]` | `#1c1d18` | Dark recessed stone backing inside arches and cleft |
| Bronze / gold geometric inlay | `ref_06` | `[423, 341, 455, 412]` | `#483c2e` | Warm metallic knotwork accent on central crest and merlon posts |
| Colossal statue stone (midtone) | `ref_09` | `[466, 307, 518, 388]` | `#4c4b41` | Olive-grey carved stone, slightly warmer than the recessed facade |
| Mountain cliff rock (brown-grey) | `ref_04` | `[126, 244, 190, 358]` | `#362f2b` | Warm brown-charcoal craggy rock contrasting with the green facade |
| Mountain cliff rock (cool grey) | `ref_06` | `[159, 106, 279, 213]` | `#474842` | Neutral dark granite cliff option |
| Causeway and walk pavement | `ref_04` | `[297, 732, 317, 789]` | `#bfaca5` | Warm pale dressed stone flagstones |

Comparison with existing `TAOM_Map\AssetSources\Scenes\erebor\textures\` (all 2048 px, read only):

- `t_dw_wall_block_a1_d.png` (`#57514a`) and `t_dw_trim_a1_d.png` (`#5c564f`) are warm brown-grey, not green-grey. The
  great gate's carved facade needs its own 1024 px verdigris green-grey tiling stone and carved geometric trim sheet
  (or vertex-tinted/recoloured material set) so it reads unmistakably as Erebor's green facade against brown rock.
- `t_dw_rock_cliff_a1_d.png` (`#59544e`) and `t_dw_rock_cliff_b1_d.png` (`#4b4a4d`) are close in hue to the brown-grey
  and cool-grey cliff rock and can inform the cliff tiling set without copying or altering existing Erebor files.

## 3. What the references always have and never have

**Always present:**

- Straight lines, 45 degree and 60 degree canted corners, stepped trapezoidal or hexagonal arches, flat-topped chevron
  lintels, chamfered arrises, strict bilateral symmetry across the carved facade.
- Strong vertical rhythm (monolithic piers, deep vertical cleft, vertical diamond-lattice bays) countered by crisp
  horizontal bands (closely spaced merlon galleries and rows of square embrasure slots).
- Dramatic recessed depth: statues forward, flanking bastions mid-forward, main three-bay facade recessed in the cliff
  canyon, central cleft and upper crown recessed deeper still.
- Mountain rock wrapping tightly around the sides and arching over the crown.

**Never present:**

- Semicircular or pointed Gothic curved arches (every arch head is straight-segmented and canted).
- Timber half-framing, organic floral tracery, round cylindrical towers, or pitched shingled roofs on the gate facade.
- Floating ornaments or ungrounded masonry: every pier, buttress, plinth and mountain flank carries load to grade and
  extends below grade into a terrain skirt.

## 4. Exact craft vs free design

Following the owner's precision-and-creativity rule (`feedback_precision_and_creativity.md`) and built-architecture
checklist (`feedback_asset_art_rules.md`):

### Exact (enforced by automated tests and Blender gates)

- **Frame:** KIT frame in Blender (X across the frontage, +Y toward the field/attackers, -Y toward the mountain
  interior/courtyard, Z up, Z = 0.0 m at the approach ground line and gate threshold). Foundation skirt descends to
  `Z = -6.0 m` so a scene builder sinks the skirt into terrain rather than sinking the fighting line.
- **Mirror symmetry:** Every symmetric carved member is built from one half-profile and mirrored about X = 0 to `< 1 mm`
  vertex error.
- **Siege deck height:** Fighting wall walk at **Z = 12.0 m** flush across wall, breach bays, gatehouse deck and
  catapult bastions, matching the 14 m siege ladders (10.1 to 13.6 m, median 11.8 m) and 12 m siege towers (hinge at
  11.90 m, already placed in `taom_haterade_erebor_town_z`).
- **Flush walkable surfaces:** Every deck that troops walk on is one flush horizontal plane in mesh and collision body,
  minimum **4.5 m to 6.0 m clear width**, with zero raised lips, steps, or dead ends. Every walk connects to interior
  ground via dedicated stair entities (`has_steps` body flag, 0.25 m rise / 0.28 m run, 48 risers for 12.0 m).
- **Two breachable walls:** Left and right 20.0 m sections, each with an intact solid mesh (carrying 6 separate
  destructible parapet/merlon parts `_m1.._m6` for `tower_merlon` and `DestructableComponent`) and a matching broken
  `_dest` mesh (exact end-section match to the solid state, 12.2 m breach gap at deck level, walkable rubble mound with
  path slope <= 35 deg and body ramp <= 30 deg).
- **Two-gate gatehouse:** Outer gate opening >= **5.0 m wide x 7.1 m tall** (clears `european_castle_gate_outer_l2` at
  4.70 x 6.84 m with stone jamb stops and a 5.80 x 2.60 m swing pocket behind the hinges on the castle side); inner gate
  opening >= **4.8 m wide x 6.2 m tall** (clears `empire_castle_gate_inner_l3` at 4.52 x 5.77 m); horizontal distance
  between outer and inner gate planes **8.4 m to 9.5 m** (within vanilla's 7.15 to 9.81 m range); vaulted chamber
  ceiling >= 8.2 m; murder-hole shaft in the Z = 12.0 m deck directly above the kill chamber (deck height 12.0 m < 14 m
  rock-pile range limit); clear ram approach >= 6.25 m to the field of the outer gate.
- **Defender machine platforms:** Four level clear pads >= **5.50 x 5.72 m** (sized for a turned mangonel, crew and stone
  pile) with unobstructed 120 degree field firing arcs.
- **Clean topology:** Zero non-manifold edges on closed solids, zero degenerate/zero-area faces, outward-wound normals
  verified with back-face culling on.
- **One asset a piece and prefab cap:** Single top-level prefab placed once in the editor, using ~48 to 56 prefab
  entities total (less than 1% of the 6,193 remaining global prefab entity margin measured 2026-11-26).

### Free (varied across massing variants for owner selection)

- Where the 12.0 m siege line sits relative to the colossal facade (forward apron wall, integrated lower facade bays,
  or forward barbican bridge with recessed cliff forecourt).
- Overall mountain peak height and silhouette (96 m compact heroic vs 120 m deep approach vs 138 m full colossal).
- Statue state (both whole as in `ref_03`/`ref_04`, or right statue broken/leaning as in `ref_01`).
- Crag profile and organic rock relief of the enveloping mountain cliff.
- Placement of bronze/gold knotwork accents (`ref_06`) vs pure green-grey weathered stone (`ref_01`, `ref_04`).

## 5. Three massing variants for round 1 (all equal finish, no ranking)

Each variant keeps the unmistakable 1:1 Erebor three-bay facade, vertical cleft, diamond-lattice bays, twin kneeling
colossal statues on octagonal plinths, enveloping craggy mountain peak, and full Bannerlord siege geometry (including
both solid and broken states of the two 20 m breach walls).

| Aspect | Variant A: Colossal front + 12 m lower outwork | Variant B: Compact 1:1 integrated facade | Variant C: Two-tier barbican + cliff forecourt |
|---|---|---|---|
| Primary reference reading | `ref_01` + `ref_04` (true colossal scale; low crenellated outwork at foot) | `ref_04` + `ref_05` + `ref_06` (lowest gallery IS the 12 m fighting line) | `ref_01` + `ref_02` + `ref_10` (terraced outer curtain + vaulted causeway into cliff) |
| Total mountain width x depth x height | 148 m x 68 m x 138 m | 126 m x 58 m x 96 m | 136 m x 78 m x 120 m |
| Carved facade width x height | 66 m x 108 m (ratio 0.61) | 60 m x 76 m (ratio 0.79, widened for 20 m bays) | 64 m x 94 m (ratio 0.68) |
| Statue height (above plinth) and state | 34 m, both whole (aligns with 1st grand gallery at Z = 36 m) | 22 m, both whole (aligns above Z = 12 m gallery) | 28 m, right warrior broken/tilted outward into cliff (`ref_01`) |
| Where the 20 m breach walls sit | Left & right 20 m sections across the lower crenellated outwork directly at the base of the flanking bays (X = +-10.5 to +-30.5 m, Y = +12 m) | The carved lower left & right bays of the facade itself ARE the two 20 m breach walls with arrow slots & bronze merlons (X = +-10.5 to +-30.5 m, Y = +8 m) | Forward terraced curtain walls across the canyon mouth (X = +-10.5 to +-30.5 m, Y = +22 m), flanking a fortified causeway; outboard `ref_02` stepped terraces clear of siege lanes |
| Gatehouse (outer to inner gate) | Integrated through the outwork and facade base (outer Y = +13.5 m, inner Y = +4.5 m, gap 9.0 m) | Inside the central facade bay (outer Y = +9.0 m, inner Y = +0.0 m, gap 9.0 m) | Fortified vaulted causeway barbican linking outer curtain (Y = +23.0 m) to cliff inner portal (Y = +13.5 m, gap 9.5 m) |
| Defender catapult platforms (4) | Two outer corner bastions (X = +-33.5 m, Z = 12 m) + two gatehouse roof pads flanking the murder hole (X = +-5.5 m, Z = 12 m) | Two projecting corner bastions (X = +-33.5 m, Z = 12 m) + two gatehouse roof pads flanking the murder hole (X = +-5.5 m, Z = 12 m) | Two forward flank bastions (X = +-33.5 m, Z = 12 m) + two gatehouse barbican roof pads flanking the murder hole (X = +-5.5 m, Z = 12 m) |
| Interior access | Vaulted dwarven forehall behind facade (Y < 0) with two 12 m stair flights (`has_steps`) to the Z = 12 m walk | Vaulted mountain forehall behind facade (Y < -2 m) with two 12 m stair flights (`has_steps`) | Open recessed cliff forecourt between outer wall and cliff + interior stairways connecting all walks |

## 6. Target prefab entity tree and budget

Placed as ONE top-level prefab (`erebor_great_gate_a`, ~50 entities total, well under the 6,193 spare cap):

- `erebor_great_gate_a_main` (1 entity): fixed mountain peak, cliff flanks, upper carved facade, colossal statues,
  interior hall shell, gatehouse shell, and catapult bastions (dot-named sub-objects for materials/LODs, one `bo_` body).
- `erebor_great_gate_a_wall_l` and `_wall_r` (2 breach roots x 15 entities = 30 entities): `WallSegment` root ->
  `solid_child` (20 m wall mesh + body + 6 destructible `_m1.._m6` merlons each with a `debris_holder` tagged
  `reference`) + `broken_child` (`_dest` breached wall & rubble mound mesh + walkable body).
- `erebor_great_gate_a_gh_m1` to `_m4` (4 x 2 = 8 entities): destructible gatehouse parapet merlons + `debris_holder`.
- `erebor_great_gate_a_stair_l`, `_stair_r` (2 entities): dedicated stair meshes (with integrated railings, rule 10)
  carrying `has_steps` on their `bo_` bodies.
- Scene-placed (never nested in the prefab): `outer_gate`, `inner_gate` (`CastleGate` prefabs or route B custom leaves),
  `batteringram_a_spawner`, 4 `siege_ladder_14m_spawner`, 2 `siege_tower_12m_spawner`, 4 defender `DeploymentPoint`s,
  `throwable_rock_pile` + `murder_hole_target`, `archer_position`s, markers, and `navmesh.bin`.

## 7. Planned Tripo ("tripoli") usage after massing approval

Per `docs/features/tripo-cli.md`, never send the owner's reference pictures to Tripo, keep all prompts and filenames
generic, run `--dry-run --json` first, and use the proven **exact skeleton by code, organic skin from Tripo** rule:

1. **Colossal kneeling dwarf warrior statue** (1 generated model -> retopologized with the quad toolkit, mirrored/posed
   into left whole + right whole and optional broken variant): ~50 to 60 credits.
2. **Breach rubble mound and fallen dressed green-stone blocks** (height field + loose angular stones, reusing the
   Rivendell rubble teardown method): ~50 to 60 credits.
3. **Craggy mountain cliff rock relief** (orthogonal rock face relief / tiling cliff patch mapped onto our exact
   mountain hull and skirt): ~50 to 60 credits.

Every carved architectural member (piers, stepped canted arches, bevelled moldings, lattice grilles, interlaced crest,
crenellations, flush decks, stairs, gate throat and swing pockets) is built by exact parametric code for master-level
precision, crisp arrises, and exact symmetry.
