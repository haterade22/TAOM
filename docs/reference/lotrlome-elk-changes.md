# LOTRLOME_Armory changes for the Great Elk (2026-09-22)

The elk's **data plane lives in the external `LOTRLOME_Armory` module**
(`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\`), which is **not tracked by
this repo**. A module reinstall silently reverts every change below, and nothing in CI sees them. This ledger
records each edit, why it exists, and how to redo it.

Issue: [#636](https://github.com/haterade22/TAOM/issues/636). Feature doc: [elk.md](../features/elk.md). Built
as the war ram was: [lotrlome-war-ram-changes.md](./lotrlome-war-ram-changes.md).

> **Why this creature skips most of the creature-mount workflow.** `AssetSources/creature/elk/elk_001.fbx`
> carries only the vanilla horse bones: 39 bones under `horse_skeleton_notused`, `horsepelvis` to
> `horse_head`, the same bone set and parent links as the ram's `SK_EB_Goat_A.fbx` (byte-parsed 2026-09-22;
> the FBX names no bone outside the horse set). Mike confirmed the intent: horse skeleton, horse animations. So Phases 1 to 5 of
> [creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md) do not apply, and the elk authors
> no animation data: it moves on the horse's clips and attacks with the ram's.

## Assets (imported by Mike in the Modding Kit, 2026-09-22)

| Path | Note |
|---|---|
| `AssetSources/creature/elk/elk_001.fbx` + `textures/elk_001_{d,n,s}.png`, `elk_saddle_{d,n,s}.png` | Source, a 3ds Max export |
| `Assets/creature/elk/elk_001_geo.tpac` | Both metameshes, `elk_001` and `elk_saddle_001`, each with 5 LODs. Package GUID `91749DAC-63D4-4329-B052-4A141D472021` |
| `RuntimeDataCache/91749DAC-63D4-4329-B052-4A141D472021.rdc` | Present (1,391,433 bytes). `python tools/check_rdc_entries.py --under creature/elk`: 1 package, 0 without an RDC |
| `Assets/creature/elk/textures/*_tex.tpac`, `*_mtl.tpac` | The six textures have RDC entries; the two materials have none, which is normal (so do the ram's four) |

`elk_001_geo.tpac` also holds **`take 001`**: the FBX's default 3ds Max take, one frame, 120 curves with one key
each and no motion, with its skeleton slot all zeros. Nothing references it, but the Kit logged
`[20:12:59.113] Overriding item take 001` (`rgl_log_80756.txt:6779`), and every Max export names its take that.
Delete it in the Kit and save the Armory, or reimport with animation import off.

**The package's `.rdc` predates the package's last save:** RDC written 20:12:59.26, `elk_saddle_001_mtl.tpac`
20:13:23.78, `elk_001_geo.tpac` 20:13:27.56. The first pass logged `MetaMesh( elk_saddle_001 ) : Unable to find
material elk_saddle_001` for all six LODs (`rgl_log_80756.txt:6773-6778`); the 20:13:27 reprocess was clean.
Whether the client tolerates the older cache is unverified: the same Kit save refreshes it, and a textured saddle
in game settles it.

## Backups

Each edited file was copied beside itself with a **non-`.xml`** extension first (the engine globs `*.xml`, so a
`.xml` backup would load as data and duplicate every id):

```
ModuleData/LOTRLOME_items/LOTRAOM_horses.xml.bak-elk-20260922
SubModule.xml.bak-elk-20260922
ModuleData/Languages/loc_LOTRAOM_horses.xml.bak-elk-20260922
```

`lotr_monster_elk.xml` is new, so it has no backup; deleting it and the `SubModule.xml` block reverts it.

## 1. `ModuleData/Monsters/LOTR/lotr_monster_elk.xml`: new file

```xml
<Monster id="taom_elk" base_monster="horse" action_set="as_war_ram"
         weight="500" hit_points="250" />
```

| Decision | Why |
|---|---|
| `base_monster="horse"` | Inherits `Flags`, `family_type="1"`, `monster_usage="horse"`, `num_paces="6"`, every bone, the slope block and all twelve rein attributes, as the ram does. `monster_usage="horse"` also hands the elf rider vanilla's full horse overlay: `Monster.elf` is `as_human_warrior` / `monster_usage="human"` with `CanRide="true"`, so no race flag needed flipping (the ram had to flip `Monster.dwarf`) |
| `action_set="as_war_ram"` | The ram's set, a child of `as_horse` binding `act_war_ram_butt` to the clip `war_ram_butt`. That clip is authored on the engine `horse_skeleton`, so it plays on the elk and lowers the antlers: the antler charge. Of its two children, only `as_war_ram_map` is looked up by the engine (the party icon's mount: `ActionSetCode + "_map"`, and `MBGlobals.GetActionSet` throws on a miss); no managed code in v1.5.3 reads `_town_and_village`. The file's comment said the engine derives both; corrected the same night after review. Sharing a set between monsters is proven (`as_elephant`) |
| `weight="500" hit_points="250"` | A bigger animal than the vanilla horse (400 / 200); the ram went the other way (320 / 160). The elk spawns at 270, since `extra_health` adds to the Monster's hit points. The engine's mount-victim blow math reads the ITEM's weight (450); TAOM's knockdown math reads this one |
| No rider adders | An elf is human sized, so the horse's seat applies |

## 2. `SubModule.xml`: one registration block

A `<XmlNode><XmlName id="Monsters" path="Monsters/LOTR/lotr_monster_elk"/>` block, cloned from the war ram's and
inserted right after it, before the `LOTRAOM_horses` items node. BOM and CRLF preserved (the file also carries
three bare LFs; they were left alone). No `project.mbproj` entry, for the reason the ram's ledger gives.

## 3. `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml`: two items

Inserted after the ram bardings, before the warg block. BOM and LF preserved.

| id | mesh | stats |
|---|---|---|
| `taom_elk_a` "Great Elk" | `elk_001` | `monster="Monster.taom_elk"`, maneuver 74, speed 62, charge_damage 50 (40 until 2026-09-23), extra_health 20, `body_length="200"` (100 until 2026-09-23, section 5), `difficulty="0"`, value 1400, weight 450, `culture="Culture.mirkwood"`, `is_merchandise="true"`, `<Flags Civilian="true" />` |
| `taom_elk_saddle_a` "[Mirkwood] Elk Saddle" | `elk_saddle_001` | `HorseHarness`, `body_armor="45"`, Leather, `family_type="1"`, `mane_cover_type="none"`, weight 20, `culture="Culture.mirkwood"`, `<Flags Civilian="true" />`, no `UseTeamColor` (the saddle texture is coloured, not greyscale) |

`difficulty="0"` because the `elk_rider` career starts a player on this elk. The saddle is required beside every elk: `elk_001` is the
bare animal and the seat is the saddle mesh (`ElkMountWiringTests` pins the pairing).

## 4. `ModuleData/Languages/loc_LOTRAOM_horses.xml`: two English rows

`<string id="taom_elk_a" text="Great Elk"/>` and `<string id="taom_elk_saddle_a" text="[Mirkwood] Elk Saddle"/>`,
after the ram's rows. This is the English source the translator and `check_external_loc_coverage.py` read; without
it the keys are invisible to both. CRLF preserved (the file's one bare LF, after `warg_saddle`, was left alone).

## 5. 2026-09-23: the elk built at 2x

Mike, after the first in-game look: "Elk needs to be probably x2 the size of what it is currently. maybe even
bigger."

| File | Edit |
|---|---|
| `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` | `taom_elk_a` `body_length` 100 to 200, and the item comment's size paragraph rewritten. Backup `.bak-elkscale-20260923` |
| `ModuleData/Monsters/LOTR/lotr_monster_elk.xml` | The "SIZE LIVES ON THE HORSE ITEM" paragraph rewritten (comment only). Backup `.bak-elkscale-20260923` |
| `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` | `taom_elk_a` `charge_damage` 40 to 50 (Mike: "Charge Damage should be 50"). Backup `.bak-elkcharge-20260923` |

The first night's files said `body_length` also scales the rider and must stay at 100. That was copied from the
war ram's docs and is wrong: Mike observed on the 3x mumakil that the rider is not resized (TAOM
`docs/features/mumakil.md`, "RESOLVED"), which is also how the elephant went to 1.3x with no mahout work. What does
not scale for free is the antler charge's reach, a fixed metre measured from the elk's centre, so TAOM's
`ElkConfig.AuthoredScale` (2.0) now multiplies it (3 m trigger, 4 m radius), and `ElkConfigTests` fails if this
`body_length` and that constant drift. A 2x elk also means 2x capsules and 2x the Monster's rider-attach height:
check the elf's legs against the wider back, and gates and tight forest paths, in game.

## Verification actually run (2026-09-22)

| Check | Result |
|---|---|
| XML parse of all three files | All reparse; `LOTRAOM_horses.xml` BOM+LF, `SubModule.xml` BOM+CRLF with its 3 bare LFs unchanged, the new Monster file no BOM + LF like the ram's |
| Diff against each backup | Insert-only: one block in each file, no line changed |
| `python tools/validate_moduledata.py` | 0 errors. It sweeps the Armory ModuleData, so the repo's `Item.taom_elk_a` / `Item.taom_elk_saddle_a` references resolve |
| `python tools/validate_mesh_refs.py` | 0 errors, 0 warnings; every unique visual mesh in the Armory items resolves (3,948 of 3,948) |
| `python tools/audit_action_set_parity.py` | Exit 0, no gaps; `as_war_ram` and its two children still report under root `as_horse` (the elk adds no set) |
| `python tools/check_rdc_entries.py --under creature/elk` | 1 package, 0 without an RDC |
| `python tools/validate_xml_schemas.py` over the edited repo and Armory XML | PASS, 6 files validated, 0 failed, 0 registrations resolving to nothing |
| `python tools/audit_armory_refs.py --regen-catalogue` | Verdict CLEAN; catalogue drift back to 0 after the two elk meshes were classified in `tools/armory_catalogue_overrides.tsv` |
| `loc_LOTRAOM_horses.xml` after the two English rows | Reparses; CRLF kept (66 CRLF), its one pre-existing bare LF untouched |
| Deep review (7 lenses, 2 waves, plus a convergence pass) | No CRITICAL or HIGH; every finding fixed or filed (#637 to #642). `docs/reviews/rca-elk-2026-09-22.md` |

## Verification actually run (2026-09-23, the section 5 edits)

| Check | Result |
|---|---|
| XML parse of the two edited files | Both reparse; `LOTRAOM_horses.xml` BOM + LF (1,423 LF, 0 CRLF), `lotr_monster_elk.xml` no BOM + LF (42 LF), as on 2026-09-22 |
| Diff of `LOTRAOM_horses.xml` against `.bak-elkcharge-20260923` | One line: `charge_damage` 40 to 50 |
| `python tools/validate_moduledata.py` | 0 errors |
| `python tools/validate_xml_schemas.py` over both files | PASS: 2 validated, 0 failed |

## Still owed

1. **In-game checks**: the list in [elk.md](../features/elk.md) "How to verify in game", above all the front
   legs in motion (the FBX binds them lower than the ram's) and the seat.
2. **Delete `take 001`** in the Kit and save the Armory (also refreshes the package's `.rdc`); then re-run
   `python tools/check_rdc_entries.py --under creature/elk`.
3. **Translate the two item names**: `python tools/translate_with_claude.py --lang <L> --module Armory --sync-ids --apply`
   per language. `--sync-ids` seeds the keys into each language file first, or `write_back` silently discards the
   paid translation; `--apply` calls the API.
4. **Record the elk art's source and licence** in [provenance-register.md](./provenance-register.md) if it came
   from outside TAOM.
5. **Mirror sync**: the lotraom-assets mirror is Mike's to sync; it was not touched.
