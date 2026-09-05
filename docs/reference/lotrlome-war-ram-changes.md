# LOTRLOME_Armory changes for the Dwarven War Ram (2026-08-28)

The war ram's **data plane lives in the external `LOTRLOME_Armory` module**
(`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\`), which is **not
tracked by this repo**. A module reinstall silently reverts every change below, and nothing in CI
sees them. This ledger records each edit, why it exists, and how to redo it.

Issue: [#515](https://github.com/haterade22/TAOM/issues/515). Feature doc:
[war-ram.md](../features/war-ram.md). Workflow this deviates from (deliberately, see below):
[creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md).

> **Why this creature skips most of the creature-mount workflow.** Both ram body meshes and all
> eight bardings are skinned to the **stock vanilla horse skeleton, bone for bone**: `horsepelvis`,
> `horsespine1-3`, `horseneck1-2`, `horse_head`, `horsel/rfemur`, `horsel/rtibia`,
> `horsel/rlargecannon`, `horsetail1-3`, including the `_nub_notused` bones. Verified by string-scan
> of both FBX. That makes the ram the first horse-skeleton reskin TAOM has shipped, so Phases 1-5 of
> the authoring doc (clips, `quad_movement` tagging, action_types, action_sets, monster_usage_sets,
> the rider partial) are **not needed at all**. No animation data is authored anywhere.

## Backups

> **Moved 2026-09-01.** The two of these that still existed (`monsters.xml` and `LOTRAOM_horses.xml`;
> the `SubModule.xml` copy was already gone) now live under
> `E:\Bannerlord_Backups\module_bak_sweep_2026-09-01\LOTRLOME_Armory\` at the same relative paths.
> Copy one back before rolling anything back. See [module-backup-sweep](module-backup-sweep.md).

Every file below was copied to a sibling with a **non-`.xml`** extension before editing:

```
ModuleData/monsters.xml.bak-warram-20260828
ModuleData/LOTRLOME_items/LOTRAOM_horses.xml.bak-warram-20260828
SubModule.xml.bak-warram-20260828
```

The non-`.xml` extension is load-bearing: the engine globs `GetFiles("*.xml")`, so a backup ending
in `.xml` is parsed as real data and duplicates every item id in the file.

## 1. `ModuleData/monsters.xml`: one attribute

| Change | Why |
|---|---|
| `Monster.dwarf` `<Flags>`: `CanRide="false"` becomes `CanRide="true"` | Dwarves were the only race besides `cave_troll` flagged unrideable. The flag does **not** gate AI spawn (`MissionAgentSpawn.BuildAgent` checks only `item.HasHorseComponent && item.HorseComponent.IsRideable`), so AI ram riders would spawn mounted regardless. It gates `Agent.CheckSkillForMounting`, which drives interactive mounting, remounting a loose mount, and the "riding skill not adequate to mount" tooltip. Without the flip, a dismounted dwarf can never get back on and the player dwarf cannot mount at all |

**Scope check:** only the `dwarf` block was touched. `cave_troll` and `cave_troll_settlement` keep
`CanRide="false"`. `dwarf_settlement` was already `true`. This does not let dwarves ride horses:
that is still blocked repo-side by the `MOUNTED_DWARF` validator rule, which now allowlists only the
two ram item ids.

## 2. `ModuleData/Monsters/LOTR/lotr_monster_war_ram.xml`: new file

The whole Monster is seven attributes, because it is the vanilla `horse_2` shape:

```xml
<Monster id="taom_war_ram" base_monster="horse" action_set="as_horse"
         weight="320" hit_points="160"
         jump_acceleration="7.5" relative_speed_limit_for_charge="4.0" />
```

| Decision | Why |
|---|---|
| `base_monster="horse"` | `Monster.Deserialize` (TaleWorlds.Core v1.4.8) copies `Flags`, `ActionSetCode`, `FemaleActionSetCode`, `MonsterUsage` and every capsule field from the base, and every attribute not named here keeps the inherited value (the deserialiser guards its defaults behind `if (!flag)`, where `flag` means "has a base_monster"). So the ram inherits `Mountable`/`CanRear`/`RunsAwayWhenHit`/`CanCharge`/`CanWander`, `family_type="1"`, `monster_usage="horse"`, `num_paces="6"`, every bone name, the slope block and **all twelve rein attributes** |
| That rein point specifically | Gotcha #18 of the authoring doc: vanilla pairs `Mountable` with all twelve rein attributes without exception, while the spider and warg declare 5 and the elephant and mumakil declare 0, and v1.4.8 changed the native rein path that runs on mounted-agent death. Inheriting from the horse makes the war ram **the only TAOM mount carrying vanilla's complete rein surface** |
| `action_set="as_horse"` (no new set) | The mesh is on the horse rig, so vanilla horse clips bind with nothing authored. Two monsters sharing one action set is already proven here (the elephant and mumakil both use `as_elephant`). It also means `as_horse_map` and `as_horse_town_and_village` already exist, and a missing `_map` / `_town_and_village` child is the elephant's "Crash #4" native AV class |
| `monster_usage="horse"` inherited | **Load-bearing for a dwarf rider.** `as_dwarf_warrior` has no `base_set`, so the "rider partial at the top of the file" trick that serves the spider and warg does not reach dwarves at all. It does already carry **203 `act_horse_*` / `act_ride*` rows, the identical count to vanilla `as_human_warrior`**, so inheriting the horse usage set hands the dwarf rider a complete authored overlay for zero XML. Any custom usage set would mean hand-authoring a second rider partial into a 4,843-action set |
| No `<Capsules>` override | Inherited from the horse and scaled by `AgentScale`. Revisit if enemies clip the smaller body |
| No rider adders | Deliberately inherited, pending an in-game seat check. See the warning below |

> ### The rider seat is the one thing still owed a measurement
>
> `Monster.dwarf`'s height fields are a **byte-for-byte copy of `human`'s**
> (`standing_eye_height="1.70"`, `crouch_eye_height="1.10"`, `mounted_eye_height="0.75"`,
> `arm_length="0.9"`; only `weight` differs, 100 against 80), while `dwarf_skeleton_a` renders at
> roughly **82% of human height**. Measured off armour meshes in one coordinate space: the dwarf
> chest armour tops at z=1.30, the human (Gondor Anfalas) at z=1.59.
>
> The engine therefore seats and measures a rider it believes is human sized. **That mismatch is the
> real cause** of the documented "a mounted dwarf spawns inside the horse mesh" defect that
> `taom_schema.py`, `Patch46_TournamentDwarfDismount` and `EyeHeightAdjustmentHook` all describe or
> work around.
>
> Because the ram is ridden **only** by dwarves, the correction belongs on this Monster:
> `rider_eye_height_adder`, `rider_body_capsule_height_adder`, `rider_camera_height_adder` and if
> necessary `rider_sit_bone`. It must **not** go on `Monster.dwarf`, which would move every dwarf on
> foot as well.

## 3. `SubModule.xml`: one registration block

A `<XmlNode><XmlName id="Monsters" path="Monsters/LOTR/lotr_monster_war_ram"/>` block cloned from
the **mumakil** block, inserted after the chariot's.

The mumakil was chosen as the template deliberately: the spider and elephant are registered in
**both** `project.mbproj` and `SubModule.xml`, while the mumakil and chariot are registered in
`SubModule.xml` **only** and demonstrably work. There is no documented rule for which mechanism to
use, so copy the one proven to work with the fewest moving parts.

**Do not** add a second `soln_action_sets` entry to `project.mbproj` for this feature. `MergeElements`
runs once per extra entry against the fully accumulated tree, and any child XPath absent from the XSD
throws `KeyNotFoundException` at startup. That is the elephant's "Crash #3".

## 4. `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml`: ten items

All Horse and HorseHarness items in the whole Armory live in this single file.

### Two mounts

| id | mesh | notes |
|---|---|---|
| `taom_war_ram_a` | `sk_eb_goat_a` | `monster="Monster.taom_war_ram"`, `maneuver="75" speed="42" charge_damage="18" body_length="100" extra_health="20"`, `difficulty="0"`, `culture="Culture.erebor"`, `is_merchandise="true"` |
| `taom_war_ram_b` | `sk_eb_goat_b` | identical stats, alternate pelt |

**`difficulty="0"`, matching `saddle_horse`.** It was 30 until the review. The `ram_rider` career
hands a starting player this exact mount, and `CheckSkillForMounting` compares effective Riding
against `MountDifficulty`, so a non-zero value risks giving a career a mount its own player cannot
remount after a dismount. `saddle_horse` (the item the ram replaced in that roster) and
`spider_mount_a` are both 0; the ram is a basic culture mount, not an elite beast like the
elephant (170) or mumakil (200).

**`body_length="100"` is the scale knob, and it does NOT scale the mount only.**
`EquipmentIndex.ArmorItemEndSlot` and `EquipmentIndex.Horse` are the same value (10), the scale
block in `BuildAgent` has no `IsMount` guard, and `BuildAgent` runs for the rider as well as the
mount with the Horse item still in the rider's spawn equipment. **Any `body_length` other than 100
scales the dwarf too.** 100 is identity, which is why it is safe. This is pre-existing engine
behaviour that also affects the mumakil (300) and the wargs (110/115); it is not this feature's to
fix, but do not retune the ram's scale without accounting for the rider.
`BuildAgent` calls `SetInitialAgentScale(0.01f * BodyLength)`. 100 means the ram ships at its
**authored size**, deliberately unshrunk.

This was 75 first, on the reasoning that a horse-sized ram would dwarf its ~82%-of-human rider. That
was backwards: the war goat is meant to dwarf its rider, and 75 read visibly too small in game. The
mesh is authored at exactly vanilla horse scale, measured not assumed: `horsepelvis` sits at 1.396 m
in this rig against vanilla horse's declared `standing_pelvis_height="1.40"`. At 1.0 the ram is
2.39 m long, 2.27 m to the horn tips and 1.40 m at the back, against a ~1.47 m dwarf.

**The two pelts are separate items on purpose.** They must never be combined by putting
`sk_eb_goat_b` in `<AdditionalMeshes>`, because a `HorseHarness` equipped on a mount **suppresses the
Horse item's `<AdditionalMeshes>`** (native compositing). That is exactly how the chariot lost its
cart the moment Rhun barding was worn.

### Eight bardings

`family_type="1"`, inherited from the horse family through the Monster.

| id | mesh | `body_armor` | material |
|---|---|---|---|
| `taom_ram_barding_light_a` | `sk_eb_goat_bard_light_a` | 20 | Leather |
| `taom_ram_barding_light_b` | `sk_eb_goat_bard_light_b` | 24 | Leather |
| `taom_ram_barding_med_a` | `sk_eb_goat_bard_med_a` | 30 | Chainmail |
| `taom_ram_barding_med_b` | `sk_eb_goat_bard_med_b` | 34 | Chainmail |
| `taom_ram_barding_heavy_a` | `sk_eb_goat_bard_heavy_a` | 40 | Chainmail |
| `taom_ram_barding_heavy_b` | `sk_eb_goat_bard_heavy_b` | 44 | Plate |
| `taom_ram_barding_elite_a` | `sk_eb_goat_bard_elite_a` | 50 | Plate |
| `taom_ram_barding_elite_b` | `sk_eb_goat_bard_elite_b` | 54 | Plate |

The armour values follow **measured** coverage, not guesswork. From the FBX vertex arrays, the tiers
really are a ladder and the `_b` variants really are more armour, not recolours:

| mesh | length covered | lowest point (z0) |
|---|---|---|
| Light A | 1.78 m | 0.82 |
| Med A | 2.25 m | 0.71 |
| Heavy A | 2.34 m | 0.71 |
| Elite A | 2.42 m | 0.71 |
| every `_b` | same as its `_a` | **0.65** (drapes lower) |

The ceiling stays under the Khamul barding's 55, currently the top of this file.

**On `family_type="1"`:** ram bardings will also fit horses in the player's inventory UI, and horse
caparisons will fit the ram. That is the documented trade, not an oversight.
`creature-mount-authoring.md` Phase 2 is explicit that family 1 is what carries vanilla's complete
rider-death, dismount and rider-fall surface, and that isolation belongs in C# rather than in the
family number. The spider tried family 11 and simply had no rider-death surface at all. AI rosters
assign the harness explicitly, so only the player can mix them.

## Verification actually run (2026-08-28)

| Check | Result |
|---|---|
| `python tools/validate_moduledata.py` | **PASS**, no issues. Its sweep reaches the Armory ModuleData, so this also proves all ten new item ids resolve from the repo-side troop rosters |
| `python tools/validate_all_troop_refs.py` | **PASS**, erebor 61 troops / 232 armor refs / 0 missing |
| `python tools/audit_polearm_shield_parity.py` | **PASS**. The new ram rosters pair `sm_dwarf_erebor_spear_a`/`_b` with metal shields and do not appear in the warn list |
| Mesh names against the tpacs | All ten `mesh=` values byte-scanned against `SK_EB_Goat_A_geo.tpac` and `SK_EB_Goat_Bard_A_geo.tpac`. Every one resolves exactly |
| XML well-formedness + encoding | All three edited files reparse; BOM and line endings round-tripped (monsters.xml and LOTRAOM_horses.xml are BOM+LF, SubModule.xml is BOM+CRLF) |

## Still owed

1. ~~**Rebuild `AssetPackages`.**~~ **RESOLVED 2026-09-01, and the premise was wrong.** The Armory
   has no `AssetPackages` directory at all any more (0 cooked packs against 4,364 loose
   `Assets/**/*.tpac`), so there is nothing to rebuild and nothing for the barding to be missing
   from. All eight barding meshes are present and resolving today: `sk_eb_goat_bard_{light,heavy,
   elite}_{a,b}` and siblings, verified against the live tpac TOCs, and
   `taom_ram_barding_light_a` -> `sk_eb_goat_bard_light_a` passes `validate_mesh_refs.py` with zero
   errors.

   The original measurement was correct at the time and its conclusion did not follow. "Absent from
   `pack*.tpac`" only means "will not render" if the engine reads the packs, and for this module it
   reads the loose tree. The spider mount hit the same trap from the other side and root-caused it:
   the engine loads loose `Assets`, not a stale baked pack
   ([lotrlome-spider-mount-changes.md](./lotrlome-spider-mount-changes.md)). See
   [armory-guide.md](./armory-guide.md) "Two asset trees" for the corrected model.

   **Confirmed three independent ways**, which is worth recording because the failure is silent:

   | Method | Result |
   |---|---|
   | Direct byte-scan of `AssetPackages/pack*.tpac` | `sk_eb_goat_a`/`_b`, `m_eb_ram_a1`/`_a2` and the body textures are present (pack3, pack5, pack7). Nothing matching `sk_eb_goat_bard` or `eb_ram_barding` is |
   | `python tools/validate_mesh_refs.py` (existing repo gate) | **10 `MISSING_MESH` errors: exactly the 8 ram bardings and the 2 Khamul bardings**, and none for the ram bodies |
   | `tools/audit_deleted_mesh_impact.py` (written the same day for the unrelated Armory asset cleanup) | counts "11 meshes broken the other way: imported but not yet cooked, so they render naked right now" and names the set: the Khamul barding, **the Erebor goat bardings**, and the Gondor horse plate |

   **Practical consequence: this is a live defect, not merely absent content.** The two mounts
   themselves are cooked and will render, but the eight barding items are already registered in
   `LOTRAOM_horses.xml` and every ram troop equips one, so **today a ram spawns with a bare body and
   no barding**. It is one re-cook away from correct, and until then it also shows up as 10 errors on
   anyone else's `validate_mesh_refs.py` run.

   **`validate_mesh_refs.py` exits 1, so it is a real gate, not just a report.** Its docstring says
   "1 if any ERROR" and `main()` returns 1 accordingly. Beware the measurement trap here: running it
   as `python tools/validate_mesh_refs.py | tail` and then reading `$?` gives **`tail`'s** exit code,
   which is always 0. Use `${PIPESTATUS[0]}`, or do not pipe. This ledger asserted the opposite until
   a second measurement caught it.

   ~~It also resolves against the **cooked packs** rather than the loose `Assets/` tree, which is
   precisely why it catches this class while a check against `Assets/` reads clean.~~ **Not true as
   of 2026-09-01, and it was the reasoning behind item 1's wrong conclusion.** For a module with no
   cooked tree the tool resolves against `Assets/**` and says so, and the engine reads that same
   tree (`rgl_log`: `Loading packages $BASE/Modules/LOTRLOME_Armory/Assets...`). A check against
   `Assets/` is the one that matches the running game here, not the one that reads falsely clean.
2. ~~**The same gap affects `sk_rh_khml_barding_a` and `_b`**~~ **RESOLVED 2026-09-01, same reason
   as item 1.** Both meshes are present and resolving in the live `Assets/` tree, verified against
   the tpac TOCs. There is no pack to rebuild and no gap to cover. The commit that added them
   (`68fe7b5b`, 2026-08-28) is still marked `Not-tested: in-game`, so an in-game look is owed, but
   the asset side is fine.
3. **Translate the ten item names.** The Armory's English source is the inline `{=KEY}default` in the
   item XML (see `tools/translate_with_claude.py`, which walks `LOTRLOME_Armory`), so the English
   strings already exist. The twelve per-language files need
   `python tools/translate_with_claude.py --lang <L> --module Armory --apply`.
4. **Confirm material bindings in the Modding Kit.** Only two barding materials exist
   (`m_eb_ram_barding_a1` and `_a2`) for eight meshes, so the `_a` and `_b` sets presumably share one
   each.
5. **`t_eb_ram_a2` ships `_d` only**: there is no `t_eb_ram_a2_n_tex.tpac` and no `_s_tex.tpac`, and
   the source PNG set has no `_n` or `_s` either. If `m_eb_ram_a2` is a real second pelt it will
   render flat unless the material reuses `a1`'s normal and specular maps. This affects
   `taom_war_ram_b`. Check before shipping that item.
6. **`t_eb_ram_barding_a2_d2.png`** is 2048x2048 but only 72 KB, so near-flat. Purpose unknown;
   confirm what binds it.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/file-catalogue.md](../modding/file-catalogue.md)
- [docs/modding/items-mounts-and-harness.md](../modding/items-mounts-and-harness.md)
- [docs/reference/bannerlord-skeleton-authoring.md](./bannerlord-skeleton-authoring.md)
- [docs/reference/doc-lookup.md](./doc-lookup.md)
- [docs/reference/feature-map.md](./feature-map.md)
- [docs/reference/lotrlome-soln-id-fix.md](./lotrlome-soln-id-fix.md)
- [docs/reference/lotrlome-warg-changes.md](./lotrlome-warg-changes.md)
- [docs/reference/module-backup-sweep.md](./module-backup-sweep.md)

<!-- backlinks-end -->
