# Mirkwood architectural trim

## Material direction

Mike asked to settle the trim as a shared building system after the first
siege assemblies. Keep Mirkwood's smooth limestone and weathered dark
heartwood as its two main surfaces. Carry the same moulding vocabulary
from large thresholds to smaller framed windows and timber buildings.
Broad wall faces use the plain or laid-ashlar limestone. Put the beech
frieze on focal openings and the quieter architrave on repeated windows.
Keep damp stone on exposed feet, sills and drip ledges. One dressed
limestone block takes the bearing load under a jamb or post.

## Baked sheets

Two 1024 × 1024 RGB sheets, each with diffuse, DirectX normal and packed
metallic/gloss/AO channels. The maps are derived from the approved
[limestone and heartwood body surfaces](textures.md). Row intervals below
are top-down and end-exclusive. Eight pixels of edge dilation sit inside
each end of each V interval. Run U 2.210 m per repeat along the path.

| Limestone band | Atlas rows | Nominal cross span | Use |
| --- | --- | --- | --- |
| Water table and coping | 0 to 192 | 0.389 m | Shed water above walls, at sills and on parapet caps |
| Bundled ribs | 192 to 384 | 0.389 m | Three unequal stone rolls around doors and windows |
| Beech frieze | 384 to 560 | 0.354 m | Veined leaves for focal arches and entrances |
| Dressed architrave | 560 to 736 | 0.354 m | Quiet edge for repeated jambs and headers |
| Recessed reveal | 736 to 880 | 0.283 m | Shadow break around openings and panels |
| Damp drip | 880 to 1024 | 0.283 m | Low courses and exposed threshold returns |

| Heartwood band | Atlas rows | Nominal cross span | Use |
| --- | --- | --- | --- |
| Hewn beam | 0 to 224 | 0.449 m | Long faces of beams and lintels |
| Bundled jamb and handhold | 224 to 432 | 0.414 m | Post edges, jambs and handrail stock |
| Stop chamfer | 432 to 608 | 0.345 m | Brace edges and headers with formed plain shoulders |
| Key groove and peg seat | 608 to 768 | 0.311 m | Stiles, joined panels and narrow joint details |
| Sawn end grain | 768 to 912 | 0.276 m | Four bounded cut-face islands, each 0.414 m wide |
| Small bead and fillet | 912 to 1024 | 0.207 m | Fine rail, screen and door-frame edges |

## Place the trim

1. Use the row's measured cross section and bottom-up `usable_v` values in
   `evidence/stone.json` or `evidence/timber.json`. The UVs sample the centres
   of the first and last usable texels. Where a small window needs a
   narrower moulding, record the changed width and keep U pitch.
2. Measure U along the member's centreline. Keep the accumulated distance
   through the straight-to-curve transition and retain material orientation
   when reversing the path. Unwrap the two sides and end caps over their
   actual depth so the UVs have area even when the front shares an edge.
   For wood, align along the grain on every long face. Put a sawn face
   wholly in one end-grain island and keep its rings in proportion.
3. The beech has ten full 221 mm pitches across the sheet. There is an
   11 mm quiet margin on both sides of each pitch boundary. Finish at a
   whole pitch and make the remaining length a dressed plain stop. The
   entrance uses 44 pitches and two roughly 0.18 m plain stops. The timber
   stop-chamfer examples taper back into square shoulders over 77 mm and
   leave 0.14 m and 0.16 m of plain seat.
4. For simple trim geometry, use the atlas normal. For formed profiles,
   keep the atlas diffuse and packed masks. Use the grain normal from the
   plain stone or timber on the separate `Mirkwood Grain` UV. This keeps
   the normal from repeating relief already in the mesh. Reserve physical
   leaves for close-up focal pieces. The four-leaf geometry proof has a
   measured crest of 11.39 mm and uses the same relief sampler as the bake.
5. Assign the diffuse as sRGB. The normal and masks are linear data.
   Blender flips DirectX normal green and converts gloss to roughness.
   The export PNG normal stays green-down.

## Review files and reproduction

Use `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/mirkwood/review_now.html`
for the nine renders and the six maps. The editable file is
`kit_full/trim_03/view/mirkwood_architectural_trim.blend` in the same work
area. Its five trim scenes cover all bands at scale, a public entrance,
paired narrow town windows, a housed timber lintel, four straight-to-curve
returns, and baked versus modeled beech. The four new material assets are
marked in Blender for reuse. The seven source scenes from Siege 04 remain
in the same file.

The map stems are `t_mirkwood_stone_trim_b` and
`t_mirkwood_timber_trim_b`. The six source PNGs are in
`kit_full/trim_03/maps`. The evidence directory holds profiles, usable
UV bounds, input and output hashes, and first and second preview images.
The third preview checked the narrower window versions, formed stone
joints, wood grain around the posts and the reshaped plinth contact.
The final pass corrected the UVs on the thickness faces and end caps.

The generators and focused checks are in the local `_tools/trim_03`
folder. Rerun the bakes into an explicit working folder with
`stone_trim.py --input-maps <textures_02/maps> --out <folder>` and
`timber_trim.py --source <textures_02/maps> --out <folder>`. For the
Blender build, start from the Siege 04 review and run
`build_trim_review.py` with the output folder after Blender's
`--`. The shared `blender_one.py` runner captures the render job and
waits for `evidence/trim_report.json`. Reopen the saved blend with
`verify_trim_review.py --out <folder>` through the same runner.

The atlas checks covered gutter extension, U tiling through 128 px,
normal length and normal polarity. The independent Blender reload
checked 45 trim UV samples, all four end-grain islands, the two lintel
faces, all 21 packed PNG hashes and the seven original scenes. Use the
maps as local building candidates. Bannerlord appearance, texture
compression and the distant mip chain await Modding Kit checks.

## Mip edge study

A separate proof sampled both usable V edges of each band and the
four end-grain islands. It used recursive 2 × 2 box reduction and
bilinear lookup at the published texel centres. The six map hashes
match the approved exports. Both atlases had zero adjacent-row
contribution at 128 and 64 px under repeat and clamp in V. At
32 px the worst sample drew 36.72% from a neighbouring row.
At 16 px the worst contribution was 46.29%, with up to 12.87°
of timber normal drift against the isolated band. End-grain U
margins contributed to filtered corners, while no sampled edge
drew directly from a neighbouring island through 16 px.

The reproducible Python tool and JSON results are under
kit_full/trim_mip_01. Point the tool's --trim at kit_full/trim_03
and give --out a separate folder. The proof covers ideal box and
bilinear filtering. Compression, anisotropy and the engine's mip
selection remain editor checks.
