# RCA: Harad war paint (#1100, deep review, 2026-11-08)

## Top-line

Tier 2 review: three lenses in the first wave, four in the second after a HIGH, and one convergence pass. Codex was not authorized and is owed before merge. The review found one HIGH: 54 Harad characters with a fixed face key got no paint, against the maintainer's decision that everyone in Harad wears it. The other confirmed items are MEDIUM or lower. One finding was disproved by reading the native decoder. One MEDIUM needs the maintainer's decision and is not fixed.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|------------|-------------------|
| F1 | HIGH | 53 `Culture.aserai` lords and `named_companion_igor` hold their own key with design 0 or a vanilla design. 28 lords also had an inline `Cleanface`, and 25 lords exist only in `lords.xslt`. | Target set derived from a proxy | The plan counted users of `fighter_haradrim`, and the test iterated the same set, so the test could not see what the plan missed. The scan read `*.xml` only. | Derive a target set from the culture, in every file that defines the character, XSLT included. The new `FixedKeyLordTests` does this. |
| F2 | MEDIUM | The first fix gave two twin pairs different paint, so `LordFaceVarietyService` rule B no longer grouped them. It groups on the whole key. | Consumer of the edited value not traced | The fix changed key text without checking who compares that text. | One design and colour per pair. `test_lords_with_one_face_also_share_one_key` guards it. |
| F2b | MEDIUM | The first fix also wrote paint into Igor's key. He shared the key with Aragorn's group, so he left the group and kept Aragorn's exact face. | Same as F2 | The same fix, before the consumer was traced. The convergence pass found it. | Igor keeps his key, and rule B rolls him from his `harad` tag. `test_igor_stays_in_aragorns_face_group_and_rolls_from_the_tag`. |
| F3 | MEDIUM | The gate read only the first `<race>` and first `<tattoo_materials>`, and a dict hid a duplicate skin. | Gate passes on a broken input | Written against the valid file only; `.claude/rules/tests.md` asks for the states a gate will meet. | Compare whole lists; one block per skin. |
| F4 | MEDIUM | Nothing proved that a material draws its own design. | Gate gap | The recipe clones a material, so a forgotten texture swap is the likely slip, and only the name was checked. | `test_each_material_binds_its_own_texture`. |
| F5 | MEDIUM | The longer palette moves the tattoo colour of every other culture: `fighter_rhun` and 270 fixed-key rows on 2026-11-08. | Side effect outside the requested scope | The doc stated the shift in one sentence and gave no scale. | Not fixed: the maintainer decides. The doc's Known limits names the scale and the options. |
| F6 | LOW | Rolled Harad could not reach the five cool colours: the preset held colour values 34 and 51. | Value range not checked against the new palette | The palette was extended without decoding the preset's colour bits. | Top value 63. `test_harad_preset_rolls_across_every_harad_colour`. |
| F7 | LOW | `tools/oneoff/apply_troop_bodyproperties.py` would put Umbar back on the painted preset. | Stale generator | No search for tools that write the edited value. | Map entry and docstring corrected. |
| F8 | LOW | Facts this change made false: `lord-face-variety.md`, `module-taom.md`, `file-catalogue.md`, `body-properties.md`, and two open questions it answers. | Doc sweep by feature name only | The sweep came after the first review draft and missed counts. | Corrected. Sweep for the fact's words and numbers. |
| F9 | LOW | The doc gave the colour mapping without the renderer's 0.99 clamp, and overstated "Harad colours only". | Engine detail | The renderer was not read when the doc was written. | Corrected. |
| F10 | LOW | 68 preset lords repeated the preset's tattoo tag inline. | Redundant data | The first edit replaced `Cleanface` one for one. | Deleted; `test_preset_lords_leave_the_tattoo_tag_to_the_preset`. |

## Disproved

The Data flow lens read the tattoo colour from KeyPart8 bits 19 to 24 and reported that rolled Harad reach only gold and bone. Those bits write offset `0x55C`, which is `HeightMultiplier` in `FaceGenerationParams`. The colour is KeyPart1 bits 18 to 23, offset `0x558` (`FUN_18057b4e0`). The Engine compatibility lens confirmed this independently. F6 is what remains of that finding.

## Not applied

- Dropping the `aserai` style tag from the 75 entries: no XML selects it, but whether a native path reads style tags outside selection is unverified, so parity is unproven. The tag also matches yotthani's package.

## Root-cause pattern

F1, F2 and F5 share one shape: the change was judged by the set it meant to touch, and not by who else reads the same value. F1 is a target set taken from a preset instead of a culture. F2 is a key compared as whole text by a consumer. F5 is a palette position read by every culture.

## Why each agent missed these

- **First wave:** XML, Data flow and Tooling each found F1 independently. Tooling found it by testing the gate, and Data flow added that a tag alone cannot fix a fixed key.
- **F2:** the first-wave lenses ran before the fix existed. Engine compatibility and Completeness caught it in the second wave.
- **F2b:** the Design and Completeness lenses named Igor as a gap to paint, and neither traced his shared key. The convergence pass compared the diff with HEAD and found the group.
- **F5:** only Data flow traced the palette across cultures; its bit decode was wrong, but the shift it reported is real.
- **Efficiency:** no finding, as expected for a test of this size.

## Follow-ups

- FOLLOW-UP: no issue: the citation `tools/package_release.py:126-135` in `module-taom.md` predates this change and now sits at about line 846; a doc refresh corrects it.

## Feedback memories to codify

None. The durable rule is in `docs/reviews/lessons/data-content-cultures.md`.
