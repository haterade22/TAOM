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

**Do not invent custom `soln_*` ids.** `GetMergedXmlForNative` matches `XmlResource.MbprojXmls`
entries on exact string equality, and it is reached only from eight hardcoded ids plus a native
callback that builds `"soln_" + xmlType` from type names native itself supplies, so a custom id
matches nothing. LOTRLOME's pre-existing `soln_spider_monster` and `soln_lotr_misc_action_types` were
dead for exactly this reason. **Both were removed on 2026-08-28**, after this was written: the spider
row was inert but harmless, since the Monster is also registered managed-side in `SubModule.xml`,
while the misc row meant 20 action types had never loaded at all while `action_sets.xml` bound them
221 times. Ledger: [lotrlome-soln-id-fix.md](lotrlome-soln-id-fix.md).

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


## 10. The `Alliance.Editor` source pointers (found in game, 2026-08-28)

Launching with the absorbed warg produced a modal `RGL WARNING`:

```
Unable to locate source file
$BASE/Modules/Alliance.Editor/AssetSources/2_lotr/monster/warg/Warg_skin_n.png
of texture Warg_skin_n to compile
```

**Cause.** The warg assets were imported and cooked in an editor module called `Alliance.Editor`,
and that module name is baked into the tpacs as the asset's *source* path. No such module exists in
any install. The engine reads a loose asset definition, decides the texture needs compiling, looks
for the source PNG under a module that is not there, and warns.

Two places carried the dangling pointer, and only one of them mattered at runtime:

| Where | Count | Effect |
|---|---|---|
| Loose `Assets/creature/warg/**` (78 of 163 files) | 78 | **This is what warned.** A loose definition is what makes the engine attempt a compile |
| Cooked `AssetPackages/warg.tpac` | 54 | Inert at runtime, but untrue metadata. LOTRLOME's own packs point at `$BASE/Modules/LOTRLOME_Armory/...`, their own module |

**Why the elephant never warned.** Its loose stubs
(`Assets/creature/elephant/textures/*_tex.tpac`, 456 to 553 bytes) contain only the texture *name*,
`t_creature_elephant_a1_d`, with no `$BASE/Modules/...` source path. Nothing to compile, nothing to
warn about. The warg stubs are the same size but carry a full source path, which is the whole
difference.

### What was actually wrong, and the shape that works

It took three attempts. The two failed ones are recorded because each was a reasonable-looking move
that broke something different, and the reasons generalise to every absorbed creature.

**Attempt 1: remove the loose `Assets/creature/warg/` tree.** Correct that it is redundant at
runtime: all 75 warg-side animation targets resolve from `AssetPackages/warg.tpac` (62) plus vanilla
`animation_clips.tpac` (13), verified against warg.tpac alone and against all twelve cooked packs.
Wrong because `Assets/` is what the **Modding Kit asset browser reads**. The cooked pack does not
appear in the Kit at all, so the warg vanished from the editor.

**Attempt 2: repoint the tpacs and supply the sources they name.** `Alliance.Editor` and
`LOTRLOME_Armory` are both exactly 15 characters, so the substitution is byte-safe, and the 111
source files were copied in so every pointer resolved. This **crashed the game on startup**:

```
18:40:08.658  rglAsset_package_item_texture validate_rdc : Warg_skin_d
18:40:09.448  Compiled image Warg_skin_d(B8G8R8->DXT1)(2048x2048->2048x2048)
18:40:09.452  rglAsset_manager::signal_package_item_change - Warg_skin_d
18:40:09.459  Assertion Failed!  rglIntrusive_ptr.h:151  Expression: px != nullptr
```

**The mechanism, and the rule to take from it.** A loose asset definition whose source is missing is
harmless: the engine warns "unable to locate source file to compile" and moves on. Make that source
resolvable and the warning becomes a real compile. The engine then recompiles a texture the cooked
pack has **already registered under the same name**, signals a package-item swap mid-startup, and
dereferences a null intrusive pointer.

> **A loose `Assets/` definition and a cooked `AssetPackages/` entry must never claim the same asset
> name with a reachable source.** Dangling is safe. Cooked-only is safe. Both, resolvable, crashes at
> `signal_package_item_change`. This is why the elephant is fine: its loose stubs carry a bare texture
> name and no `$BASE/Modules/...` source path, so nothing can ever trigger the recompile.

### The shipped shape

| Location | State | Why |
|---|---|---|
| `AssetPackages/warg.tpac` | 307 MB, kept | The runtime form. Self-sufficient, and the only warg asset players need |
| `AssetSources/creature/warg/` | 78 files, 177 MB, kept | Real sources for re-baking. `package_release.py` marks `AssetSources` EXCLUDE, so it never ships |
| `Assets/creature/warg/` | **absent** | Its removal is what stops the startup recompile. Parked at `<game>/_taom_disabled/warg_loose_assets_20260828/` |

**The sources follow LOTRLOME's own tree, not the donor module's.** The first copy preserved Alliance's
`2_lotr/monster/warg/` shape, which put a foreign top-level folder in `AssetSources` and matched
nothing else in the Armory. Restructured 2026-08-28 into the convention the other five creatures
already use, `creature/<name>/{animations, mesh, textures}`:

```
AssetSources/creature/warg/animations/   56 FBX    20.7 MB
AssetSources/creature/warg/mesh/          1 FBX    14.5 MB   Warg_Rig_V5.fbx
AssetSources/creature/warg/textures/     21 PNG   141.7 MB   warg skin/fur + orc_rider_saddle
```

`AssetSources/creature/` now reads `chariot elephant mumakil ram spider warg`, mirroring
`Assets/creature/`. The rider gear that rode along in the same pack went to the existing Isengard
tree rather than staying under a creature: `AssetSources/Isengard/{orc_rider, orc_weapons, uruk}`,
33 files. Nothing in TAOM or LOTRLOME references those, but they are what `warg.tpac`'s own items
were built from, so they are kept with their culture. The `2_lotr` folder is gone.

**The tpac source pointers now dangle again, and that is the correct state.** They still name
`AssetSources/2_lotr/...`, which no longer exists. Both crash preconditions are therefore false:
there is no loose `Assets/` definition to trigger a compile, and no reachable source for one to
compile from. Re-baking in the Kit will write fresh pointers at the new paths, which is why they were
not rewritten by hand: unlike the `Alliance.Editor` to `LOTRLOME_Armory` swap, the new paths are a
different length, so a byte-preserving in-place substitution is not available.

### What to check in game

Relaunch and confirm the `RGL WARNING` is gone. The warg should render with full textures from the
cooked pack. If a texture is missing rather than merely un-compilable, restore the loose folder from
`_taom_disabled/` and reopen the question, because that would mean the engine wanted the loose
definitions after all.

## 11. Materials and animations after the Kit re-import (2026-08-28)

Absorbing the data plane is not the whole job. Once the meshes and textures are re-imported
through the Modding Kit so they live under this module's `AssetSources`, the creature still needs
its materials and its animation wiring, and none of that survives a copy unchanged.

### What the import produced, and what was missing

| | `_geo` | `_tex` | `_anm` clips | `_mtl` materials |
|---|---|---|---|---|
| After the Kit import | 57 | 21 | **0** | **0** |
| Donor module | 57 | 21 | 73 | 12 |
| Elephant, for reference | 73 | 18 | 36 | 6 |

The Kit imports geometry and textures. Materials and animation clips are authored assets: it does
not invent them, so both were absent and were copied from the donor. They are worth copying rather
than recreating, because they carry authored options that are tedious to reproduce and easy to get
subtly wrong:

- clips: `quad_movement` 15, `cyclic` 10, `make_walk_sound` 14, `make_bodyfall_sound` 5,
  `lock_movement` 3, `client_prediction` 42, `synch_with_horse` 11
- materials: `two_sided` 12/12, `bumpmap` 12/12, `skinning` 12/12, `use_specular` 6, `alpha_test` 5

Every count matches the donor exactly after the move.

### The guid problem, and two ways to get it wrong

**The Kit mints a fresh asset guid for every item it imports.** Copied materials and clips still
name the donor's guids, so every reference dangles. Nothing fails loudly; you get
`CONTENT WARNING: Unable to find DiffuseMap of material <name>` and a creature that renders
untextured, plus `Unable to find item to add dependency(depender <clip>)`.

Measured here: all 21 texture guids changed, each of the 12 materials embedded exactly 3 stale ones
(Diffuse, Normal, Specular) for 36 dead references, and the clips carried a further 28.

`tools/remap_creature_asset_guids.py` repairs this by matching items between the two trees and
substituting guid for guid. Two mistakes were made getting there, both worth knowing:

**Mistake 1: keying the map on item 0.** A `_geo` from an FBX import holds two items, the source
`.fbx` and the skeleton animation named `<rig>_notused|<clip>`, and **their order is not stable**.
The Kit writes the `.fbx` first on import and the skeleton animation first once the asset is saved,
which is the order the donor's files are in. Keying on item 0 therefore mapped a donor skeleton
animation onto a new `.fbx`, and **broke 17 clips that had been correctly wired**. The tool now
matches every item by name.

**Mistake 2: patching after patching.** Once the first pass had overwritten a guid, the second pass
had nothing to match: the donor value it needed was already gone. The repair was to stop patching,
restore all 85 files from the backups (verified byte-identical to the donor originals) and run one
correct pass. Prefer a restore-and-redo over a corrective patch whenever a previous pass wrote the
wrong value.

### Clip names and take names do not correspond

56 source FBX takes yield 73 clips, because one take is sliced into several clips with frame ranges
in the clip's Source1/Source2 fields. The names diverge accordingly: the clip `rider_warg_forward_walk`
comes from the take `warg_rider_walkfast`, and `rider_warg_gallop_turn_left_head` from
`warg_rider_gallop_l`. **The pairing is not derivable from filenames.** It survives only because the
donor's guids encode it, which is the whole reason the copied clips are worth having.

Result: 65 clips wired, pairing identical to the donor's, 0 differing. 8 clips the donor never wired
either, so there is nothing to copy for those.

### Owner Skeleton has to be set in the Kit

A skeleton animation's metadata is `int32(1) | GUID_A(16) | GUID_B(16) | trailer(13)`. GUID_B is the
Owner Skeleton and is all-zero when unset, which produces:

```
RGL WARNING: Please set owner skeleton name for anim: rider_warg_canter
Unable to write RDC for animation clip rider_warg_canter.
```

**Writing that field by hand does not work, and the reason is the checksum.** Each item carries an
8-byte checksum immediately after its metadata block. Substituting a guid for another guid leaves
the item otherwise unchanged and the existing checksum stays consistent, which is why the clip
repair above holds. Introducing a value into a field that was zero is a real content change, so the
checksum must be recomputed, and its algorithm is not known here. A patch that skips it is silently
ignored: the Kit still shows the field unset. Verified by diffing a hand-set file against the patched
one, same size, different checksum:

```
rider_warg_dash_geo.tpac   11565 -> 11565 bytes   checksum 1347615467139852255 -> -3110447808552502235
```

> **The rule.** Guid-for-guid substitution in a tpac is safe. Writing a value into a previously empty
> field is not, because the item checksum goes stale and the change is discarded. Reading back the
> bytes you just wrote proves only that you wrote them, not that the engine will accept them.

So the 48 Owner Skeleton assignments were made in the Kit by hand. What automation can still do is
tell you **which** skeleton each one takes, derived from the FBX rigs rather than guessed:

| | takes |
|---|---|
| `skeleton_warg` | 34 |
| `human_skeleton` | 22 |

**`Warg_AnimRider_Idle.fbx` is the one whose name lies:** it is rigged to the human skeleton despite
the `Warg_` prefix. Every other file follows the `rider_*` / `Warg_*` convention. Two independent
sources agree on the split: the bones present in each FBX, and the action sets, where
`as_human_warrior` (`skeleton="human_skeleton"`) binds exactly 22 clips and `as_warg`
(`skeleton="skeleton_warg"`) binds 40, with none claimed by both. `skeleton_warg2_notused` has a
single anim (`Warg_Taunt2_geo.tpac`) and takes `skeleton_warg`.

### Where it ended up

| | |
|---|---|
| Owner Skeleton set | 48 of 48 |
| Clips wired to their skeleton anim | 65, matching the donor exactly |
| Clips unwired in the donor too | 8 |
| Material to texture references live | 36 |
| Stale donor guids anywhere in the tree | 0 |

The remaining 8 (`new_animation_clip`, `new_animation_clip_3` and six others) have no source to copy
from and need an Animation source chosen by hand if they are wanted.

## 12. Why the warg did not render: the materials were not on the meshes (2026-08-29)

Everything in sections 1 through 11 was correct and the warg still did not appear, in battle or on
the campaign map. It also had a second symptom that turned out to be the same defect: the three
colour variants (`warg_brown`, `warg_dark`, `warg_albino`) all looked identical, brown.

**The cause is that the re-imported rig bound only 3 of the 7 materials the donor bound.** The Kit
imports the material assignments the FBX carries, and `Warg_Rig_V5.fbx` carries exactly three
(`warg_skin`, `warg_fur`, `orc_rider_saddle`) for five meshes. The other four were assigned by hand
in Alliance's editor and saved into its compiled tpac. **An FBX re-import cannot recover a material
assignment that was never in the FBX**, and it does not warn, because from its point of view nothing
is missing.

| Mesh | After the re-import | Alliance | After the fix |
|---|---|---|---|
| `warg_low` | `warg_fur` x3, `warg_skin` x3 | same | same |
| `warg_low_fur` | `warg_fur` x4 | + `warg_fur_lod` x4 | matches |
| `warg_low_fur_with_saddle` | `warg_fur` x4 | + `warg_fur_lod` x4 | matches |
| `warg_low_fur_with_saddle_2` | `warg_fur` x4 | + `warg_fur_2` x4 | matches |
| `warg_low_fur_with_saddle_3` | `warg_fur` x4 | + `warg_fur_3`, `warg_fur_3_lod` x3 | `warg_fur_3` x4 |
| `orc_rider_saddle` | byte-identical to Alliance | | unchanged |

`orc_rider_saddle` returning byte-identical is the control that matters: the import itself is
faithful, so the divergence is specifically the assignments the FBX never held.

### How the colour variants work, and why they collapsed

Each item names the same base mesh and a different fur mesh:

| Item | mesh | AdditionalMesh | Material override |
|---|---|---|---|
| `warg_brown` | `warg_low` | `warg_low_fur_with_saddle` | `warg_skin`, `warg_skin2`, `warg_skin3`, `warg_skin4` |
| `warg_dark` | `warg_low` | `warg_low_fur_with_saddle_2` | `warg_skin_2` |
| `warg_albino` | `warg_low` | `warg_low_fur_with_saddle_3` | `warg_skin_3` |

The colour lives in the fur mesh's own material, not in the item XML. All twelve materials were
present the whole time and resolved to distinct textures (`warg_fur_2` to `warg_fur_2_d/_n/_s`,
`warg_fur_3` to `warg_fur_3_d/_n/_s`), so the assets were never the problem. With every fur mesh
bound to plain `warg_fur`, all three variants were brown and nothing in the data said otherwise.

### The four missing bindings sat one per LOD

They appear four times each, alongside each `warg_fur` binding, which is one per LOD level. That is
consistent with a creature that renders correctly in a close-up UI preview and is absent in the
world, where it is drawn at a lower LOD.

### The wrong turn, recorded so it is not taken again

This section previously claimed the cause was that the warg had never been packaged into
`EmAssetPackages`. That was wrong. The war ram is the counterexample: it lives only as loose tpac at
`Assets/creature/ram/SK_EB_Goat_A_geo.tpac`, has no `EmAssetPackages` entry, and renders in game.
**Loose `Assets/` is read at runtime.** The correlation that theory rested on (four cooked creatures
render, two uncooked ones do not) had a sample of two on the failing side and one of them had simply
not been checked. Do not build a cause on an unchecked half of a correlation.

Neither packages folder was ever relevant, and the reason is what they are for. `AssetPackages` is
the cooked form shipped to **players**; `EmAssetPackages` is the cooked form shipped to **other
modders**, so they can open the module in the Modding Kit without being given `AssetSources`. On a
dev install the editor and the game both read `Assets/`, so a creature with no entry in either
packages folder is in its normal pre-release state rather than broken. This module happens to have
no `AssetPackages` folder at all and 13.6 GB of `EmAssetPackages`; neither fact had anything to do
with the warg. Folder semantics:
[bannerlord-engine-and-toolchain.md](bannerlord-engine-and-toolchain.md) section 6.1.

### It could not be patched from files

Fixing this outside the Kit would mean inserting a material reference, not substituting one. The
metadata lengths differ between the two versions of the same mesh (1,317 bytes against 1,349 on the
dark variant), so there is no slot to overwrite. That runs into the same wall as the Owner Skeleton
in section 11: adding content invalidates the item's 8-byte checksum, whose algorithm is not known
here, and the Kit then discards the edit silently. The assignments were made in the Modding Kit.

### Residual

`warg_low_fur_with_saddle_3` now carries `warg_fur_3` on all four LODs where Alliance used
`warg_fur_3` on LOD0 and `warg_fur_3_lod` on the lower three. Both resolve to the same
`warg_fur_3_d/_n/_s` textures, so it looks correct; it runs the full-detail material at distance.
The other two fur meshes do use `warg_fur_lod` correctly. Worth tidying on the next Kit pass, not
worth one of its own.

> **The rule.** After re-importing a creature from FBX, diff its mesh-to-material bindings against
> the donor before anything else. The FBX carries only what the DCC tool assigned; anything assigned
> in the editor lives in the compiled tpac alone and is lost on re-import, with no warning. A missing
> binding does not read as an error, it reads as a creature that is the wrong colour, or absent.

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
