# LOTRLOME_Armory changes for the Warg absorption (2026-08-28)

The warg's **data plane moved out of the standalone `Alliance.Wargs` module and into the external
`LOTRLOME_Armory` module** (`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\`),
which is **not tracked by this repo**. A module reinstall silently reverts every change below, and
nothing in CI sees them. This ledger records each edit, why it exists, and how to redo it.

Goal: one fewer module a player has to install and enable. Precedent: the war elephant's
`ADOD_Beasts` absorption on 2026-06-08 ([elephant.md](../features/elephant.md), "Action-sets
deployment crash history"). Ledger template: [lotrlome-war-ram-changes.md](lotrlome-war-ram-changes.md).

> **Provenance.** The warg assets are Byak0's, given to the TAOM maintainer with full permission to
> use, which is why the module shipped its `AssetSources/` alongside the cooked packs. Recorded in
> [provenance-register.md](provenance-register.md) as `author-granted, terms informal` /
> `redistributed`. That row also corrects an earlier misclassification: until this change the
> register listed `Alliance.Wargs` as TAOM-owned, which was an ownership claim over another author's
> art.

## Two things that made this more than housekeeping

1. **The dependency was undeclared.** `Main/_Module/SubModule.xml` never named `Alliance.Wargs`, yet
   TAOM ships **392 `Item.warg_*` references across 12 ModuleData files** (8 equipment sets, 4 troop
   files) plus `AgentAdapter.IsWarg()` matching `Monster.StringId == "warg"`. Only
   `launchSettings.json` and `README.md` mentioned the module at all.
2. **The spider depended on it too.** `action_sets.xml`'s spider rider partial binds 35 actions to 12
   distinct `rider_warg_*` clips, and a byte-scan of all ten LOTRLOME packs found none of them.
   Removing `Alliance.Wargs` without this absorption would have broken the spider's rider animation,
   not just the warg.

## Ids were NOT renamed, deliberately

`warg`, `as_warg`, the 80 `act_warg_*` types, the `warg` usage set, the sound class and all four item
ids are **verbatim**. Renaming would have rewritten 392 references and broken every existing save's
warg mounts, which is the same failure class as the "wearing nothing" bug fixed earlier the same day.
The accepted cost: a player who re-enables `Alliance.Wargs` feeds a second `as_warg` into the native
action_sets merge. See "Retirement" below.

## Backups

Every edited file was copied to a sibling with a **non-`.xml`** extension before editing:

```
ModuleData/action_sets.xml.bak-wargabsorb-20260828
ModuleData/action_types.xml.bak-wargabsorb-20260828
ModuleData/monster_usage_sets.xml.bak-wargabsorb-20260828
ModuleData/monster_usage_sets.xslt.bak-wargabsorb-20260828
ModuleData/module_sounds.xml.bak-wargabsorb-20260828
ModuleData/project.mbproj.bak-wargabsorb-20260828
ModuleData/LOTRLOME_items/LOTRAOM_horses.xml.bak-wargabsorb-20260828
SubModule.xml.bak-wargabsorb-20260828
```

The non-`.xml` extension is load-bearing: the engine globs `GetFiles("*.xml")`, so a backup ending in
`.xml` is parsed as real data and duplicates every id in the file. The suffix deliberately reads
`wargabsorb`, not `warg`, so it cannot be misread as the same day's `bak-warram` ram backups.

## 1. `ModuleData/action_sets.xml` (3.9 MB, 1,229 sets after the change)

| Change | Where | Note |
|---|---|---|
| **75 warg rider rows folded into the existing `as_human_warrior` partial** | inside the partial opening at line 13, after the spider's 47 rows | The partial is the rider overlay. It now carries 122 rows, 47 spider and 75 warg, with no duplicate `@type` |
| **`as_warg` (110 actions), verbatim** | after the `/WAR CHARIOT` comment, before `TAOM-CIVILIAN-COVERAGE:START` | `skeleton="skeleton_warg" movement_system="quadrupedal"` |
| **`as_warg_town_and_village` (1 action), authored NEW** | immediately after `as_warg` | Parity only, see below |
| **`as_warg_map` (18 actions), verbatim** | after that | `base_set="as_warg"`, no `skeleton=` attribute, exactly as Alliance shipped it |

**Never add a second `soln_action_sets` entry to `project.mbproj`.** `MergeElements` runs once per
extra entry against the fully accumulated tree from every prior module, and any child XPath absent
from the action_sets XSD throws `KeyNotFoundException` at startup. That is the elephant's Crash #3.
The warg sets are merged into the one existing file instead.

**Insertion point matters.** The block sits before the `TAOM-CIVILIAN-COVERAGE` markers because
`tools/generate_race_civilian_action_sets.py` strips those markers and re-inserts at the last
`</action_sets>`. Anything appended after `:END` gets shuffled on the next generator run.

**About `as_warg_town_and_village`.** Alliance never shipped one, and that was **not** a bug: vanilla
gives the camel only `as_camel_map` with no `_town_and_village`, and `TaleWorlds.Core.ActionSetCode`
has no `_town_and_village` constant at all (zero hits across the v1.4.8 managed decompile). The one
vanilla instance is `as_horse_town_and_village`. It was added anyway because it costs one line and it
restores warg/spider/elephant/chariot parity, so `audit_mount_parity.py` stops reporting the warg as
the odd one out.

## 2. `ModuleData/action_types.xml`: 80 rows

All 80 `act_warg_*` declarations copied verbatim, of which **32 carry an explicit `type=`**
(`actt_mount_strike`, `actt_fall`, `actt_dash`, `actt_jump*`, `actt_idle`, `actt_hit_object`,
`actt_mount_quick_stop`). Those typings are what the 1.4.6 native lookup hardening depends on: copy
them, never re-derive them. Zero collisions with LOTRLOME's existing 151 declarations.

## 3. `ModuleData/monster_usage_sets.xml` and its XSLT

The `monster_usage_set id="warg"` was appended after `chariot`. It brings 29 movements, 29 movement
adders, 12 upper-body movements, 10 jumps, 9 falls and 4 strikes.

> The 10 jump rows are Alliance's shipped data, not the 45-row total table the authoring doc asks
> for. That rule exists for BT-driven creatures that turn mid-jump; it came out of the spider's 1.4.6
> riverbank crash. The warg has shipped 10 rows for a long time without incident, so this move copies
> proven data rather than changing it. Revisit only with a crash to point at.

The XSLT half is easy to miss. `lotr_monster_usage_warg.xslt` injected **22 rows** into vanilla's
`monster_usage_set[@id="human"]`, not one block. Those folded into LOTRLOME's three existing
templates, which already held elephant and chariot rows:

| Template | Warg rows added |
|---|---|
| `monster_usage_mountings` | 6 |
| `monster_usage_strikes` | 8 |
| `monster_usage_falls` | 8 |

Alliance's `ModuleData/Animations/action_sets.xslt` was **not** ported. The engine derives an XSLT
name from the registered *file* name, so for `action_sets_warg.xml` it looks for
`action_sets_warg.xslt`. A file called `action_sets.xslt` was never loaded. It was an identity
transform anyway.

## 4. New files

| File | Note |
|---|---|
| `ModuleData/Monsters/LOTR/lotr_monster_warg.xml` | Verbatim. `action_set="as_warg"`, `monster_usage="warg"`, `family_type="1"`, `sound_and_collision_info_class="warg"`, Maya-style `_M`/`_L`/`_R` bone names |
| `ModuleData/physics_materials.xml` and `physics_materials.xslt` | LOTRLOME had neither. Declares `sound_and_collision_info_class_definition name="warg"`, which both the Monster and the voice definition depend on. The XSLT injects the same class into the accumulated definitions, which is Alliance's belt-and-braces shape. Carries `al_metal_shield_nostick` along: unreferenced, but harmless |
| `ModuleData/CollisionInfos/LOTR/collision_infos_warg.xml` | Verbatim, 31 KB. Roughly two thirds is a copy of vanilla's `collision_infos.xml` structure with `sound_and_collision_info_class="warg"` substituted. Copied whole rather than trimmed: it is proven data, and `soln_collision_infos` has no XSD, so the merge is a plain concat where the redundancy is inert |

## 5. `ModuleData/module_sounds.xml` and `ModuleSounds/LOTR/Monsters/Warg/`

17 `<module_sound>` events added (attacks, barks, dies, grunts, hits, howlss, saddles, plus the
footstep and run families for dirt, grass, rock, snow and water), alongside 87 `.wav` files totalling
4 MB. Paths are module-relative, so the `LOTR/Monsters/Warg/` subtree had to be created inside
LOTRLOME's previously flat `ModuleSounds/`.

**These three are one atomic unit.** `collision_infos_warg.xml` binds ten of those events by exact
name and the voice definition binds eight more. Splitting the wavs, the sound rows and the collision
file across separate changes produces silent footsteps or missing-event log spam. All ten collision
references and every variation path were verified present after the copy.

## 6. `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml`: four items

`warg_brown`, `warg_dark`, `warg_albino` (all `Type="Horse"`, `monster="Monster.warg"`,
`mesh="warg_low"`) and `warg_saddle` (`Type="HorseHarness"`, `mesh="orc_rider_saddle"`). Every Horse
and HorseHarness item in the whole Armory already lives in this one file, so no registration change
was needed. 43 items total afterwards, no duplicate ids.

## 7. Registration

`ModuleData/project.mbproj` gained three rows:

```xml
<file id="soln_monsters" name="ModuleData/Monsters/LOTR/lotr_monster_warg.xml" type="monster" />
<file id="soln_physics_materials" name="ModuleData/physics_materials.xml" type="physics_material" />
<file id="soln_collision_infos" name="ModuleData/CollisionInfos/LOTR/collision_infos_warg.xml" type="collision_infos" />
```

**Duplicating a `soln_*` id is safe here, and that is not a guess.** `GetMergedXmlForNative(id)`
selects every `<file>` row with that id across all modules and merges them. Whether the merge takes
the crash-prone `MergeElements` path or a plain concat depends on whether `XmlSchemas/<id>.xsd`
exists. The install ships exactly 14 `soln_*.xsd`, and `soln_monsters`, `soln_physics_materials` and
`soln_collision_infos` are **not** among them, so those three take the concat path. `soln_action_sets`
and `soln_action_types` are among them, which is why the warg content for those two was merged into
the existing files instead.

**Do not invent custom `soln_*` ids.** `GetMergedXmlForNative` is only ever called with the fixed
vanilla id strings, so a custom id matches nothing on the native side. LOTRLOME's pre-existing
`soln_spider_monster` and `soln_lotr_misc_action_types` are dead for exactly this reason; the spider
survives only because it is also registered managed-side in `SubModule.xml`.

`SubModule.xml` gained one `<XmlNode>` for `Monsters/LOTR/lotr_monster_warg`, cloned from the
mumakil's. The warg is registered in **both** places, matching the spider and elephant, because
unlike the war ram it brings its own animation data.

## 8. Assets

| Destination | Source | Size |
|---|---|---|
| `AssetPackages/warg.tpac` | `Alliance.Wargs/AssetPackages/pack0.tpac`, byte-identical | 307 MB, 277 items |
| `Assets/creature/warg/` | `Alliance.Wargs/Assets/2_lotr/monster/warg/` | 23 MB, 163 files |
| `ModuleSounds/LOTR/Monsters/Warg/` | the same path in Alliance | 4 MB, 87 files |

**The cooked pack is required, and the loose assets alone are not enough.** Every loose
`*_tex.tpac` and `*_mtl.tpac` under `monster/warg/` is a 466 to 504 byte definition stub with no
pixel payload, the same shape as LOTRLOME's working elephant texture stubs. The payload lives in the
cooked pack. Confirmed by the mesh validator: `warg_low` and `orc_rider_saddle` resolve after the
copy and did not before.

The rename to `warg.tpac` is mandatory, not cosmetic: LOTRLOME already has `pack0.tpac` through
`pack9.tpac`. Arbitrary names are fine, and vanilla `Native/AssetPackages` proves it with 150
differently named packs.

**The shared package guid is not a collision.** `807e3d8d-e501-499b-a1d3-22ae3f4e64f3` is the Modding
Kit's default: all ten LOTRLOME packs carry it, and so do vanilla's `animation_clips.tpac` and TAOM's
own `pack0.tpac`. Only individually authored tpacs, such as the Yotthani camp props, get unique guids.

`Assets/creature/warg/` is Kit source rather than something the engine reads. It is kept so a future
re-cook has its input, matching how the elephant and spider keep loose sources beside cooked packs.

### Deliberately left behind

`AssetSources/` (646 MB, editor-only FBX and PNG), `RuntimeDataCache/` (1.1 GB, regenerated), the
`fell warg` pelt set (10 MB), and the `uruk/` and `isengard/equipment/` trees (13 MB), all verified
referenced by nothing in TAOM or LOTRLOME. Also `Skins/LOTR/lotr_skins_orc.xml` (LOTRLOME's own
`skins.xml` already defines `<race id="orc">` at line 45257), `CraftingPieces/`, `CraftingTemplates/`,
`WeaponDescriptions/`, `CharactersTest/` and `Languages/al_strings_weapon_usage.xml` (all
loader-unreachable in both `<Xmls>` and `project.mbproj`), `cloth_bodies.xml` and `cloth_materials.xml`
(LOTRLOME already defines `al_legs` at line 1710, `al_orc_hood` at 1742 and `flora_leaves`), the
Lurtz and uruk voice content (TAOM's own module already ships it), and `Shaders/` (LOTRLOME's own
`shader_mapping.bin` is 12.6 MB against Alliance's 35 KB, so overwriting it would be destructive).

## 9. The one piece that went to TAOM instead

`Main/_Module/ModuleData/project.mbproj` line 9 already declared
`ModuleData/VoiceDefinitions/LOTR/lotr_warg_voice_def.xml`, and that directory did not exist. It was
a dangling entry that failed silently, logged as a defect in
[kingdom-voices.md](../features/kingdom-voices.md). The warg voice definition (`warg_01`) now fills
that slot, which puts it in a **version-controlled** module where a LOTRLOME reinstall cannot revert
it. Alliance's duplicate `uruk_01` was not carried over, because TAOM already owns that one.

## Verification performed

| Gate | Result |
|---|---|
| `python tools/audit_item_refs.py` | **0 broken refs** across 3,017 distinct referenced items, with `Alliance.Wargs` removed from the registry roots first. This is what proves the 392 `Item.warg_*` references resolve from LOTRLOME |
| `python tools/validate_moduledata.py` | PASS |
| `python tools/validate_mesh_refs.py` | 10 errors, the same 10 that were there before the change (8 `taom_ram_barding_*`, 2 `sk_rh_khml_barding_*`, none yet cooked). Zero warg entries |
| `python tools/audit_mount_parity.py` | Reads the new LOTRLOME locations; "warg: every usage-set action is bound in its action_set" passes |
| Animation-target sweep | All **75** warg-side `animation=` targets, including the spider's 12 borrowed `rider_warg_*` clips, resolve against LOTRLOME's packs plus vanilla `animation_clips.tpac`. **Zero** require `Alliance.Wargs` |
| `dotnet test TAOM.Tests` | 7,716 passed, 0 failed, 2 skipped |
| XML parse of every edited and new file | PASS |

**Still owed: the in-game ladder.** Nothing here has been run in the game yet. In order: main menu
boot (where Crash #3 fires), party and inventory thumbnail of a warg-mounted troop (Crash #4),
campaign map party icon, a settlement walk-in, an Isengard field battle with warg cavalry including a
mounted-death pass, **a spider battle** (the `rider_warg_*` closure), and a footstep and voice audit.

## Retirement

`Alliance.Wargs` is already out of the launch chain in `Main/Properties/launchSettings.json`, which is
the non-destructive off-switch: the folder is untouched on disk, so a single-variable A/B is still
possible. **Rename it to `Alliance.Wargs.OFF` rather than deleting it**, following the
`Invoke-RdcAbTest.ps1` precedent that the script never deletes, and delete only after a full campaign
session.

Consider `<IncompatibleModules><Module Id="Alliance.Wargs"/></IncompatibleModules>` in
`LOTRLOME_Armory/SubModule.xml` **last**, after the folder is gone. `ModuleInfo` parses it and reads
the `Id` attribute (`TaleWorlds.ModuleManager.cs:692-699`), and the vanilla launcher enforces it:
`CanBeSelected` returns false while an incompatible module is selected
(`Launcher.Library.cs:1447-1455`). Added early it would cost the A/B, because
`Launcher.Library.cs:1421-1429` also deselects any module whose incompatible list names the module
the player just clicked, so toggling `Alliance.Wargs` back on silently unchecks `LOTRLOME_Armory`.
BUTR and BLSE launcher behaviour here is not established.
