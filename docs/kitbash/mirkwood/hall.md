# Mirkwood vaulted hall

The hall uses the deep stone recesses, split dark timber ribs and small
amber-lit pockets seen in refs 02, 07 and 09. It begins at the reusable
[gallery](approach.md), passes under a low flaring limestone barrel and
opens into a full-height heartwood vault. The end room uses one beech
frieze over a broad raised floor. Timber shoulders meet their supports
and stand on individual dressed limestone shoes.

| Collection | Authored join and function |
| --- | --- |
| `mirkwood_hall_vestibule_8m_a` | Y−4 to +4. The stone walk expands from a 4.50 m civic deck to the 11.50 m hall deck at Z0. The narrow lane is 3.60 m. |
| `mirkwood_hall_bay_8m_a` | Y−4 to +4 at Z0. The continuous 9.10 m aisle sits between the bearings. Two timber bents rise to a vault intrados of 8.72 m. |
| `mirkwood_hall_dais_8m_a` | Same entry deck, retaining courses and vault. Three 180 mm rises reach a 6.60 m-wide gathering floor. The rear limestone screen closes the room. |
| `mirkwood_hall_roof_8m_a` | Optional split slate cover for the repeat bay and dais when exposed outdoors. Twelve courses per slope and a continuous ridge. Shares the underlying piece origin. |

The common hall deck extends from X−5.75 m to X+5.75 m and is 0.28 m
deep. The retaining ends begin at X±5.25 m. Wall heads reach Z5.54.
The 0.22 m-thick vault follows the half section cubic at
`(5.63, 5.47), (5.04, 6.80), (2.77, 8.15), (0, 8.72)`, mirrored around
X0. The arcade and dais stay reusable with the stone and timber trim
materials already in the file.

## Blender review

The saved interior places the gallery, vestibule, two identical bays
and the dais on one axis. The measured floor had 885 valid samples.
All 42 narrow-route and 42 aisle rays passed. All 15 tread checks and
20 gathering floor and headroom checks passed. A separate reload of
one, two and three bays found flush dock floor samples, a maximum
limiting roof join discrepancy of 0.89 mm and no aisle obstructions.
A planted 30 mm bay offset failed the floor and roof checks.

The lengthwise section uses cut copies for review. The piece library
allows the three interior modules to be reused without the section.
The exterior slate shell is a separate collection in the compound
study. The 9 mm probe depth at the sampled dock excludes
decorative lining rails set back from the joining plane.

The editable interior and reports live under
`E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/mirkwood/kit_full/hall_01`.
The headless builder and independent checks are in `_tools/hall_01` in
the Mirkwood work folder. The saved file round-trip checked all 21
packed maps and 16 earlier scene fingerprints. Bannerlord collision,
occlusion and navigation need their own checks.

## Localized lantern lighting

The comparison scene uses a cool entrance and four staggered
lanterns to lead toward the amber dais. Smaller light pools and
a darker vault give the heartwood ribs a quieter silhouette.
The two EEVEE previews use the evaluated entry and dais cameras
from the compound file. The separate lighting file links the
same 23 authored objects. The builder verified the original
file digest and both camera transforms and lens data.
The EEVEE previews are under kit_full/lighting_01. The first
saved copy reopened without the new scene. The corrected study in
kit_full/lighting_02 opens in Amber Alcoves. An independent reload
checked both camera transforms and lenses, the 23 source objects,
four lantern instances and 21 packed map hashes. The matched Cycles
comparison uses the two verified 1760 × 1267 stills in
kit_full/morning_lighting_02.

## Fit the rear timber joints

The amber close-up exposed nearly black patches at both fork roots,
the crown and the stone pier tops. Camera rays located the overlapping
faces. The alternate sets the secondary branches behind the main ribs,
steps the dressed stone returns and leaves a narrow seam over the
existing crown tie. The matched Cycles pair reduced pixels with all
three RGB channels at 2 or below from 823 to zero across five 21 × 21
windows. The saved study reopened with eight adjusted parts, 38 reused
dais parts and 21 verified packed maps. All 40 floor and 20 headroom
checks passed. The editable study and the two measured views are under
kit_full/hall_joint_01.
