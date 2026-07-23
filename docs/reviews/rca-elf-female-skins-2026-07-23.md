# RCA — Female elf skins moved to the human female basemesh (2026-07-23)

**Date:** 2026-07-23
**Scope:** `<race id="elf">` female `<skin>` blocks in `LOTRLOME_Armory/ModuleData/skins.xml` (live + repo snapshot)
**Trigger:** User request ("the female basemesh isn't in currently, we use the male basemesh") — then two rounds of user-supplied in-game screenshots that caught what XML verification had passed.

## Top-line

Female elves rendered on the **male** elf basemesh because LOTRLOME never authored a female one. The
swap to the vanilla human female set was correct in substance but shipped broken twice before it was
right, and **both regressions were caught by the user looking at the game, not by any check we ran.**

1. The first pass swapped the face *mesh* but left the face *material* — a garbled face on every adult
   female elf. Verification reported "clean."
2. The second pass fixed that but left an unrelated pre-existing off-by-one in the tattoo list, which
   the user spotted in the character-creation picker.
3. `/deep-review` then found a third, cosmetic regression (`<eyebrow_mesh>` indentation) with the same
   root cause as #1's verification failure.

Final state is verified at byte level: the `<race id="elf">` block is the only region of the file
differing from the pre-change backup, and within it only the 5 `gender="1"` skins. Male elf skins,
the sauron clone and all 12 other races are byte-identical. The tattoo-picker fix is **not yet
game-verified**.

## Root cause of the data defects

LOTRLOME's elf skins were derived from vanilla human **female** (their `min_scale` values match
exactly at all five maturities) and then had every mesh slot re-pointed at the male elf basemesh.
`uses_stitching="true"` and `body_mesh_suffix="_fem"` were left set — a female configuration driving
a male body. Three further defects rode along in the same blocks and had shipped for a long time:
the mouth used a **dwarf** material, `<eyebrow_meshes>` held one `name=""` entry (female elves had no
eyebrows at all), and the adult tattoo list omitted vanilla's leading nameless `Cleanface` entry,
shifting every character-creation tattoo index by one.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Face mesh swapped to `head_female_a` while `<face_textures>` still named `m_elf_basemesh_a1_head` → garbled face in-game | Verification scope | The post-condition grepped `sk_elf_` — the **mesh** prefix. Materials use `m_elf_`. The search string was derived from what had been *edited*, not from what could still be *wrong* | Lesson (a) — search by race token, then diff the whole subtree against vanilla with a parser |
| 2 | HIGH | The same post-condition went blind for the tail of every block | Loose substring | Block detection used `"<skin" in line`, which also matches `<skin_color_gradient_points>` — 3710 matching lines where 140 real `<skin>` elements exist (26× overcount). That element sits *before* `<face_textures>`, so gender state was wiped exactly where finding #1 lived | Lesson (b) — `<Tag\b`, never bare `"<Tag"` |
| 3 | MED | `<eyebrow_mesh>` rebuilt at 3-tab indent instead of 4 | Loose substring | Indent was derived via `next(s for s in section if "<eyebrow_mesh" in s)`; `section[0]` is the `<eyebrow_meshes>` **parent**, which contains that substring. Third instance of the same family in one session | Lesson (b) covers it |
| 4 | MED | Adult female tattoo list missing vanilla's nameless index-0 entry → picker off by one | No coverage | Pre-existing LOTRLOME defect. No automated gate exists for `skins.xml`; `tools/Audit-MeshRefs.ps1` only reads `mesh=` attributes and never sees `body_meta_mesh=`/`face_meta_mesh=` | Noted as a tooling gap below |
| 5 | LOW | 8 inserted lines used hardcoded `\n` into a CRLF file | I/O convention | The element-rebuild helpers captured the source line's terminator; the insertion helper hardcoded `"\n"` | `tools/README.md` XML I/O convention — reuse the captured terminator in *every* line-constructing helper |
| 6 | LOW | Reported `hair_meshes` "identical to vanilla" — it is not | Comparison depth | The comparison walked one level (child tag → grandchild attributes) and never saw `<style_tags>` inside `<hair_mesh>`. Pre-existing and unchanged, so not a regression — but the verdict was wrong | State the depth a comparison actually reaches; "identical" without a depth qualifier is a claim, not a result |

## Root-cause pattern

**A verification predicate derived from the strings you edited cannot prove the strings you didn't
edit are correct.** Findings #1, #2, #3 and #6 are all one failure: the check was built by looking at
the diff rather than by asking "what would a correct block look like?" The fix that finally worked
was to stop grepping and instead **parse both files and compare the whole `<skin>` subtree against
its vanilla equivalent** — which surfaced the material mismatch, the mouth material, the missing
eyebrows and the tattoo count in a single pass.

The secondary pattern is loose substring matching on XML names, which fired **three times** here
(`sk_elf_` vs `m_elf_`, `"<skin"` vs `<skin_color_gradient_points>`, `"<eyebrow_mesh"` vs
`<eyebrow_meshes>`). It is a known repeat offender — `lessons/xslt-moduledata.md` already records the
`<TagNames>`-wrapper overcount, and `lessons/build-tooling-workflow.md` records substring keyword
false-matching.

**The most telling data point:** during the `/deep-review` of this very change, **2 of the 5 review
agents independently committed the same error** — grep-counting `<tattoo_material`, which also matches
the `<tattoo_materials>` parent — and both reported the tattoo counts one high. One of them quoted the
`<TagNames>` overcount lesson two paragraphs before making that exact mistake. The finding was rejected
after re-counting with an XML parser. A failure mode that survives being explicitly written down, read
aloud, and then repeated by the reader needs a mechanical guard, not more documentation.

## Why each check missed (or caught) these

- **In-game screenshot — caught #1 and #4.** The only check that saw either. Both were invisible to
  every XML-level test we ran: #1 because the reference was syntactically valid and the asset existed,
  #4 because an off-by-one index list is perfectly well-formed.
- **`grep sk_elf_` post-condition — actively misleading.** Printed "zero elf-mesh refs remain" on data
  containing four `m_elf_basemesh_a1_head` references. Two independent defects (wrong prefix + wiped
  state) produced one confident false green.
- **XML parse / well-formedness — correctly silent.** Every defect here was semantically wrong and
  syntactically valid. Well-formedness proves nothing about correctness.
- **ET subtree comparison vs vanilla — caught #1's real shape, plus the dwarf mouth and empty eyebrows.**
  This is the check that should have run first.
- **`/deep-review` asset agent — cleared all 31 IDs** (role-matched, plus a 14 GB `.tpac` shadow scan).
- **`/deep-review` tooling agent — caught #3 and #5.**
- **`/deep-review` adversarial agent — caught #6**, and proved the byte-level containment claims.

## Tooling gap

No automated validator covers `skins.xml`. `tools/Audit-MeshRefs.ps1` scans `mesh="..."` attributes
only, so it never inspects `body_meta_mesh=` / `face_meta_mesh=` / `legs_mesh=` / `hands_mesh=` or any
`<face_texture>` / `<mouth_texture>` / `<eyebrow_mesh>` reference. A small checker that, for any
non-vanilla race skin, asserts every referenced asset name also resolves in Native's `skins.xml` would
have caught #1 mechanically. Worth building before the next race-appearance change; not built here.

## Lessons codified

- `docs/reviews/lessons/data-content-cultures.md` — "Swapping a mesh in `skins.xml` means swapping its
  materials too — meshes are `sk_*`, materials are `m_*`."
- `docs/reviews/lessons/xslt-moduledata.md` — "Match XML element starts with `<Tag\b`, never the bare
  substring `"<Tag"` — a longer sibling silently hijacks the match."

## Deliberately out of scope

Male elf skins carry the **same class of defect** and were left alone: `toddler_male` pairs
`sk_elf_basemesh_a1_shoulders` with a vanilla toddler body, `kid_3_male` wears adult elf underwear on a
vanilla kid body, and elf males still lack the nameless tattoo index-0 (so they can never roll "no
tattoo" while females now can). The `sauron` race — an elf clone — likewise keeps the old male-elf
female skins, since no female sauron ever spawns. Both divergences are recorded in the snapshot README
so a future re-sync audit doesn't silently undo them.

## Status

- **Owed:** in-game verification of the tattoo picker after a shader-cache-sack clear (face + eyebrows
  already confirmed in-game).
- **Owed:** GitHub issue, created retroactively as repair; close after the in-game check.
- **Follow-up candidates:** the male-elf defects above; a `skins.xml` asset-reference validator;
  re-evaluating `TableauSafeRaceNames` in `Main/Features/HeroRace/BasicTableauRaceGuard.cs` now that
  female elves sit on vanilla morph-bearing heads.
