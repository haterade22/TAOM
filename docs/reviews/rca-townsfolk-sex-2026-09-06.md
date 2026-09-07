# RCA: townsfolk and notable sex, 2026-09-06

A player reported that Townswomen, Tavern Wenches and female notables have male bodies, and named
the cultures: Gondor, Rhun, Harad, Dale, Erebor, the Silvan elves and Rivendell. Their closing line
is the whole diagnosis in six words: **"only rohan is normal."**

Two independent defects sat behind it, both pure data, neither reachable from C#.

## Findings

**1. `is_female="true"` was missing from 166 female-role entries across 17 of the 22
`npcs_<culture>.xml` files.** `is_female` defaults to false, so the engine picked the male skin, the
male face range and the male action set for every Townswoman, village woman, Tavern Wench, beggar
and dancer outside Rohan. Rohan was correct only because commit `f3dbbfe6` added the attribute
there. `git log -S` shows the attribute was never present-then-removed anywhere else: the other 16
cultures were simply never back-filled.

**2. All 596 notable templates were male, in every culture including Rohan.** A notable's sex is its
template's sex: `HeroCreator.CreateNotable` builds the hero with `CharacterObject.CreateFrom(template)`
and `HeroInitializationArgs` reads `IsFemale` back off that hero. Nothing randomises it. Vanilla
ships 28 female out of 128. So TAOM had no female merchants, gang leaders, preachers, artisans,
rural notables or headmen anywhere, and because `GenerateFirstAndFullName` draws from `<male_names>`
for a male template, no woman was ever even named as a notable.

Both are fixed. 180 female-role entries now carry the attribute, and 139 of 596 notable templates
are female (23%, against vanilla's 22%), split Merchant 66, GangLeader 51, Preacher 22. Rural
notables and headmen stay all male, matching vanilla. The two female barbers, `barber_harad` and
`barber_rhun`, were made male so all 18 agree.

## The root-cause pattern: the gate was never asked the question

The lord roster had the same defect class and was fixed on 2026-08-29
([rca-lord-identity](rca-lord-identity-2026-08-29.md)). That pass built
`LordNameAndSexConsistencyTests`, which asserts that no `is_female="true"` lord carries
`<beard_tags>`. It was scoped to `characters/lords.xml` and nobody asked whether the sibling
`npcs_*.xml` files had the same problem. They did, in the opposite direction: lords were the wrong
sex, townsfolk had no sex at all.

`docs/features/moduledata-validation.md` had already written down the exact gap, in these words:
**"`is_female` has no rule at all."** The sentence sat there since the lord RCA as an
acknowledged boundary of the validator, and was read as a description of a limitation rather than
as a list of what to go and check. A known blind spot is a to-do, not a disclaimer.

The reason no automated gate could see it is worth stating precisely.
`tools/schemas/taom_npccharacter.json` enumerates `default_group` and nothing else, and
`validate_moduledata.py` has zero occurrences of `is_female`. The validator models ids and enums,
so it catches a reference that points nowhere. It cannot catch a reference that resolves fine while
describing the wrong person, because that needs the semantic step of knowing an id named
`townswoman_gondor` implies a woman.

## Why it stayed invisible for so long

- **It fails silently and looks deliberate.** No error, no log line, no crash. A modded LOTR town
  full of men reads as an art or lore choice, not a data bug.
- **The clothing was already right.** An earlier pass gave these characters dresses and slim
  variants without ever setting `is_female`, so the outfits said "woman" while the body said "man".
  That made the defect look like a mesh problem, which is the wrong place to look.
- **The wiring was correct.** Every `townswoman=` and `tavern_wench=` in `taom_spcultures.xml`
  points at exactly the right id, so anyone auditing the culture layer found nothing wrong.
- **Rohan being correct hid the scale.** One working culture reads as "the others need art",
  not as "one culture got an attribute the other 17 never did".

## Still open, recorded not fixed

- **Non-human female bodies are the male mesh.** `LOTRLOME_Armory/ModuleData/skins.xml` gives
  dwarf, elf, orc, uruk, uruk_hai, pale_uruk, dg_uruk, goblin and both trolls the same
  `body_meta_mesh` for both genders. Those skins do set `body_mesh_suffix="_fem"`, so garments,
  faces, animations and names come out right, but the silhouette underneath stays male. Fixing it
  means authoring female base meshes in the Armory, which is a game-install asset outside this repo,
  and a module reinstall reverts hand edits there.
- **`characters/heroes.xml` has 1,001 `Hero` rows and zero female.** Found while measuring, not
  investigated. It may be correct, since sex may be carried on the paired `lords.xml` entry, but
  nobody has checked.
- **`Frequency` is still unused.** The one lever for "this archetype should be rare" appears zero
  times in the folder, so the new female templates are drawn at the same weight as every other.

## Lessons added

[data-content-cultures](lessons/data-content-cultures.md): a documented gate limitation is a
backlog item, and a defect class fixed in one file should be re-run against its siblings before the
issue is closed.

## Referenced by

- [npcs-notables-and-townsfolk](../modding/npcs-notables-and-townsfolk.md), three new gotchas plus
  the measured numbers
- [moduledata-validation](../features/moduledata-validation.md), the semantic-boundary section
- `TAOM.Tests/Core/TownsfolkAndNotableSexConsistencyTests.cs`, the gate this produced

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/moduledata-validation.md](../features/moduledata-validation.md)
- [docs/modding/npcs-notables-and-townsfolk.md](../modding/npcs-notables-and-townsfolk.md)

<!-- backlinks-end -->
