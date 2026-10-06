# LOTRLOME_Armory: beards shown under 47 helmets (2026-10-01)

The helmets below live in the external `LOTRLOME_Armory` module
(`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\`), which **this repo does not
track**. A reinstall, an Armory sync or a regenerated file silently puts the old value back, and no validator
reads `beard_cover_type`. This ledger records the edit, why it exists and how to redo it.

## The change

Mike, 2026-10-01: a list of 36 in-game helmet names (Gondor, Dale, Rhûn, Harad; Rohan needed none) whose wearers
lost their beard. All 47 matching items were `beard_cover_type="all"`, which hides the beard. Each is now
**`beard_cover_type="type1"`**, Mike's pick over `none`: the beard shows with the engine's mildest trim.
`hair_cover_type` was not touched.

- **Engine:** `ArmorComponent.Deserialize` reads the attribute with `Enum.Parse(..., ignoreCase: true)`
  (`TaleWorlds.Core.ArmorComponent.cs:191`, v1.5.3 and v1.4.8 alike), and only the Head slot's item decides
  (`Equipment.cs:101`).
- **What `type1` trims:** in `Native/ModuleData/skins.xml`, `cover_type1` swaps 4 of the 41 human male beards for
  a shorter mesh; the other 37 show in full. The Armory's dwarf and Saruman races map every cover type to the
  full beard.
- **Names to ids:** read from the English `Languages/loc_*.xml`. Five Khamul names each cover several ids with
  different meshes, so every id with the name changed.

## Items (`ModuleData/LOTRLOME_items/<folder>/head_armors.xml`)

| Folder | Ids | Shown as |
|---|---|---|
| `gondor` (11) | `sk_gd_anf_inf_helmet_med_b`, `sk_gd_anf_inf_helmet_heavy_b` | Anfalas Infantry (Heavy) Helmet B |
| | `sk_gd_los_noble_helmet_med_a`, `_heavy_a`, `_elite_a` | Lossarnach Noble (Heavy, Elite) Helmet A |
| | `sk_gd_lon_helmet_med_b`, `_heavy_b`, `_elite_b` | Lond-Galen Noble (Heavy, Elite) Helmet B |
| | `sk_gd_vale_helmet_heavy_b`, `_elite_b` | Blackroot Vale Heavy / Elite Helmet B |
| | `sk_gd_sere_helmet_elite_b` | Serelond Elite Helmet B |
| `dale` (4) | `sk_dale_helmet_chivlary_a03`, `_a04`, `_b03`, `_b04` | (Elite) Knight Helmet A03, A04, B03, B04 |
| `rhun` (30) | `sk_rh_drag_helmet_cult_med_a`, `_cult_heavy_a`, `_b`, `_c` | Dragon Wrath (Heavy) Cultist Helmet, II, III |
| | `sk_rh_drag_helmet_cav_elite_f`, `_cav_lord_f` | Dragon Wrath Elite / Lord Cavalry Helmet VI |
| | `sk_rh_drag_helmet_east_elite_a`, `_b` | Dragon Wrath Elite Leather Helmet, II |
| | `sk_dg_khml_helmet_cult_med_a`, `_cult_heavy_a`, `_b`, `_c` | Khamul (Heavy) Cultist Helmet |
| | `sk_dg_khml_helmet_cav_elite_a`, `_b`, `_c` | Khamul Elite Cavalry Helmet |
| | `sk_dg_khml_helmet_cav_elite_d`, `_e`, `_f` | Khamul Elite Cavalry Helmet with Horns |
| | `sk_dg_khml_helmet_cav_lord_a` to `_f` | Khamul Lord Cavalry Helmet |
| | `sk_rh_loke_helmet_cav_elite_c`, `_cav_lord_c` | Loke-Rim Elite / Lord Cavalry Helmet (Horned) |
| | `sk_rh_loke_helmet_cav_lord_f` | Loke-Rim Lord Cavalry Helmet II (Horned) |
| | `sk_rh_loke_helmet_cav_elite_f` | Loke-Rim Cavalry Elite Helmet F |
| | `sk_rh_loke_helmet_east_elite_b`, `_c` | Loke-Rim Eastern Elite Helmet B, C |
| `harad` (2) | `harad07_helmet`, `harad06_v1_helmet` | Bone Helmet, Serpent Guard Helmet |

**Six of these reverse an earlier choice.** Mirror commit `c8e28b0a` (2026-07-25, "Update XMLs, Dale Armors,
etc") set 18 Gondor helmets to `all` (17 from `none`, one from `type1`) with no recorded reason. This edit moves
six of them back: `sk_gd_lon_helmet_{med,heavy,elite}_b`, `sk_gd_vale_helmet_{heavy,elite}_b` and
`sk_gd_sere_helmet_elite_b`. If that earlier change was for clipping, these six show it first.

## How it can revert

| Path | Effect |
|---|---|
| Armory reinstall or sync from the lotraom-assets mirror or a release copy | All 47 back to `all`: on 2026-10-01 the mirror and the public, patreon and testing copies still held the pre-edit files |
| `generate_dale_armor.py`, `generate_rhun_armor.py`, `generate_gondor_armor_phase2.py` | Safe: each skips ids that already exist. The Dale generator rewrote all five Dale files with `beard_cover_type="all"` until 2026-10-01 (`tools/tests/test_generate_dale_armor_apply.py` pins the fix); an older copy of it would still revert the 4 Dale ids |

## Redo

For each id above, inside its `<Item>` block, set `beard_cover_type="type1"`. Edit by hand or byte-faithfully
(`tools/README.md` "XML I/O convention"): Gondor, Dale and Harad are CRLF, Rhûn is LF only, and none has a BOM.
Restart the game afterwards; item XML loads only at launch.

**Backups** of the four pre-edit files: `E:\Temp\claude\armory-backups\beard-2026-10-01\<folder>\head_armors.xml`,
outside the module so the engine never loads them. Restoring one undoes only this edit.

## Verification actually run (2026-10-01)

| Check | Result |
|---|---|
| Diff of each live file against its backup | 47 lines changed, each `all` to `type1` and nothing else; line endings and encoding unchanged |
| XML parse of the four files | All parse; the 47 ids each read `type1` |
| `python tools/validate_moduledata.py` | 0 errors |
| `python tools/validate_xml_schemas.py` over the four files | PASS, 4 validated (the XSD types the attribute as a string, so the enum check above is the real proof) |
| `python tools/audit_armory_refs.py --report -` | CLEAN |
| Duplicate definitions | Each of the 47 ids is one engine `Item` across all installed modules; no other item shares any of their meshes |

## 2026-10-05: Thenn helmets to beard `type2`

Mike's audit against [helmet-hair-beard-cover.md](helmet-hair-beard-cover.md): all seven
`thenn/head_armors.xml` helmets (`thenn_helm1` to `thenn_helm7`) went from `beard_cover_type="type3"` to
`"type2"`. `hair_cover_type` stays `all`. No troop wears them, so check from the inventory. Backup:
`E:\Temp\claude\armory-backups\cover-2026-10-05\thenn\head_armors.xml`; the diff against it is those 7 lines plus
the `thenn_helm6` rename below.

## 2026-10-05: Sauron, Witch King and Nazgûl helmets fully covered

Mike: these helmets get hair `all`, beard `all` and `covers_head="true"`, matching the Mouth of Sauron's helm
(`sk_mordor_mouth_of_sauron_helm`, already set). In `mordor/head_armors.xml`: `sauron_helmet_player`,
`sauron_helmet` (the unused "Dont Use" twin, kept in step), `witch_king_helmet`, `nazgul_helmet` and
`nazgul_v1_helmet`. The Witch King's helmet and the Nazgûl hood were beard `type3`; the rest were already
`all`/`all`. `covers_head` hides the head skin and switches off facegen head scaling ([items-armor.md](../modding/items-armor.md)).
Backup: `E:\Temp\claude\armory-backups\cover-2026-10-05\mordor\head_armors.xml`; the diff is 5 inserted
`covers_head` lines and 2 beard lines.

## 2026-10-05: Arnor beards (audit sheet v2)

Mike's audit workbook (`E:\Temp\claude\helmet-cover-audit-v2.xlsx`, read from a copy), 13 beard changes in
`arnor/head_armors.xml`; hair and `covers_head` untouched:

| New beard | Ids (`sk_ar_art_...`) | Was |
|---|---|---|
| `type2` | `helmet_cav_elite_b`, `helmet_guard_elite_b`, `helmet_inf_elite_a`, `helmet_noble_elite_a`, `helmet_noble_prince_a`, `helmet_warden_elite_b` | `all` |
| `type2` | `helmet_guard_heavy_b` | `type1` |
| `none` | `helmet_cav_heavy_a`, `helmet_guard_heavy_a`, `helmet_inf_hvy_a`, `helmet_noble_heavy_a`, `helmet_warden_heavy_a`, `crown_king_a` | `type1` |

Backup: `E:\Temp\claude\armory-backups\cover-2026-10-05\arnor\head_armors.xml`; the diff is those 13 lines.
No troop wears any Arnor helmet, so check from the inventory.

## 2026-10-05: duplicate helmet names numbered

Mike, from the inventory: same-named helmets should read in order, `X Helmet I`, `X Helmet II`. 124 helmets in 43
same-name groups got a Roman numeral, assigned by id order (`_a` is I, `_b` II), in Arnor (13 groups), Rhûn's
Khamul line (27), `mercenary` Northern (2) and Thenn, where `thenn_helm6` was a second "Thenn Helm V" and is now VI.

- **Where:** the inline default in each item's `name="{=aom_<id>_name}..."`, the English
  `Languages/loc_<folder>.xml`, all 12 `Languages/<LANG>/loc_<folder>.xml` (the numeral appended to the
  translation, so no row goes stale), and the matching entries in the repo's `tools/translation_cache/<lang>.json`
  so a re-translation reproduces them.
- **Left for Mike:** `[Erebor] Legionary Helmet I` to `IV` (each shared by an `_x` and `_x2` id, names already
  numbered) and `[Gondor] Ithilien Hood Masked` (`ithilien_hood_masked`, `_masked_var`).
- **Proof:** 67 files backed up to `E:\Temp\claude\armory-backups\helmet-numbering-2026-10-05\`; a diff against
  them changed only name text, no line ending, BOM or line count changed, every file parses;
  `validate_moduledata.py` 0 errors; `audit_armory_refs.py` CLEAN; the regenerated name list has no duplicate
  helmet name outside the two groups left.
- **Reverts on** an Armory reinstall or sync, like the beard edit above; the cache entries survive, so
  `translate_with_claude.py --module Armory` would restore the translated numerals but not the English.

## Still owed

1. **In game, after a full restart:** check for clipping on the six reversed Gondor helmets, a Dale A03 and B03,
   a plain and a horned Khamul, and the Loke-Rim F. 25 of the 47 are worn by no troop, so check those from the
   inventory, not Custom Battle.
2. **Release copies:** `E:\LOTRAOM_Releases\{public,patreon,testing}` still hold the pre-edit files (byte
   identical to the backups), so copying the four live files over carries exactly this change. Mike's call.
3. **Mirror:** the lotraom-assets mirror is Mike's to sync; it was not touched.
