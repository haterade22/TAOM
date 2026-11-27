# Mirkwood textures

The separate [architectural trim record](trim.md) documents the six-band limestone and heartwood revisions.

## Smooth limestone revision

Mike rejected the first stone's square speckles and asked for smooth
limestone with some weathering. The current stone uses the local
Substance Painter export of *Smoothed Limestone Rock Surface*. Its
source is `rivendell/textures/v07/dressed/engine` in the kingdom kits
work area. The original exports and recipe are under `v07/dressed` and
`v07/cfg/dressed_recipe.json`. The Mirkwood input hashes are in
`kit_full/textures_02/evidence/limestone_source.json`.

The surface uses the donor's mineral grain and normal detail at a lower
strength. The body mean is approximately sRGB 157, 155, 145. The worked
trim mean is 177, 172, 156. A broad, soft veil introduces mild tone
variation. The Blender materials place vertical mineral runoff, some
darkening in recessed areas and a stronger damp tint in the bottom
0.235 m of the plinth. The macro weathering reads from the mesh position
and remains a Blender preview treatment. The latest stone maps include
the broader tone variation. The revised stone trim samples the same
limestone as the body, with the Mirkwood profile relief layered into
the normal. The heartwood and timber trim PNGs match the previous files
byte for byte.

The new review includes an oblique jamb crop at 1.30 m frame width, the
plinth, the full arch and the curved trim sample. Blender applies
weighted normals to ten jamb courses to soften the small chamfer
facets. The authored vertex positions and face topology have the same
digest as the carving study.

## Texture sets

The files are in `kit_full/textures_02/maps` under the Mirkwood work folder.
Each set has three 1024 x 1024 RGB PNGs.

| Set stem | Use | World repeat |
| --- | --- | --- |
| `t_mirkwood_stone_a` | Dressed body stone and the surface beneath the high-detail carving | 2.210 m x 2.210 m |
| `t_mirkwood_heartwood_a` | Posts, beams, curved braces and other timber stock | 2.210 m along the grain, 0.830 m across |
| `t_mirkwood_stone_trim_a` | Five dressed stone profiles including the beech-leaf fascia | 2.210 m in U |
| `t_mirkwood_timber_trim_a` | Five timber profiles with grain running along the trim | 2.210 m in U |

The stone takes its fine mineral grain from the dressed limestone export. The timber
grain bends around occasional knots and follows the brace centreline in the
Blender study. The trim height fields sample the authored profile sections.
The stone fascia uses the same leaf function as the stone model. Ten repeats of
221 mm fit the tile exactly. Each strip has eight pixels of edge dilation on
both V borders.

## Channels and preview

- `_d` is sRGB diffuse.
- `_n` is a DirectX tangent-space normal, green down.
- `_s` is linear RGB. Red is metallic, green is gloss and blue is ambient
  occlusion. Stone and timber use zero metallic. The separate bronze collar
  keeps its preview material.

The review shader reads the diffuse as sRGB and the other maps as Non-Color.
It flips normal green for Blender and inverts gloss to feed roughness. The
Blender preview uses the body normals on the high-detail carving. The separate
trim scene shows the atlas normals on simpler strips. It follows each band
from a straight run through a 0.79 m radius turn to a plain stop. The end-grain rings come from the Blender preview shader.

The same folder holds the editable `view/mirkwood_limestone_weathering.blend` with
all twelve images packed, eight renders and the review evidence. The local
`review_now.html` links each export.

## Verification

The export check opened all twelve PNGs and checked the normal lengths,
metallic values, horizontal atlas repeats and both axes of the body
tiles through 128 px. It compared the body normals to the donor, proved
the sign of both trim normal derivatives and checked eight pixels of
dilation at either edge of each band. The same U is present on both
sides of each shared vertex of the curved trim sample.

Modding Kit validation is pending. Import the specified maps with the
`pbr_metallic` setup, set `use_specular`, and check limestone grain, timber grain
direction and the carved relief under a raking light on the imported mesh.

[Design language](design-language.md) and
[shared texture pipeline](../tiling-texture-sets.md).
