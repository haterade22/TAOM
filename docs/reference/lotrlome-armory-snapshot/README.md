# LOTRLOME_Armory snapshot

Reference copies of LOTRLOME_Armory's race-defining XML files. **Not registered or loaded by TAOM.** Storage only — for reference and to preserve edits we've applied to LOTRLOME's working copy in the Steam install.

## Why this exists

TAOM depends on `LOTRLOME_Armory` (the LOTR armor/items module) for dwarf/uruk/orc/elf monster definitions, skeletons, and animation sets. We've patched LOTRLOME_Armory's `action_sets.xml` directly in the Steam install (e.g., adding Bannerlord 1.3 action-type aliases for CC parent animations — see CHANGELOG entry "Fix: CC parent agents not rendering for custom-race cultures" 2026-05-04). Those edits live at:

```
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\
```

Any future Steam-Workshop or manual update of `LOTRLOME_Armory` will overwrite our edits silently. This snapshot is the safety net — copy these files back into the Steam install if a LOTRLOME update breaks something we previously fixed.

## Files

| File | Purpose | Size |
|---|---|---|
| `action_sets.xml` | All LOTRLOME race action sets (combat / facegen / villager / etc.). **Includes the 1.3 action-type aliases** added 2026-05-04 across all 12 pre-existing facegen sets, **plus** the new `as_elf_facegen` + `as_elf_female_facegen` action_sets authored 2026-05-22 (see CC parent fix checklist below). | ~3.7 MB |
| `monsters.xml` | LOTRLOME monster definitions (dwarf, uruk, nazghul, orc, etc.) and their skeleton bindings | ~63 KB |
| `skins.xml` | Race-to-skeleton mapping, body proportions, mesh slot configurations | ~5.3 MB |
| `weapon_descriptions.xslt` | The Armory's additive override of Native's `<AvailablePieces>` lists: which crafting pieces each `WeaponDescription` accepts, and therefore which usages a crafted weapon resolves to. **Carries the one-handed-polearm registration** for the Dale spears and the Black Numenorean lance (see APPLIED EDIT below). | ~79 KB |

> The first three are race-defining XML and are auto-loaded from every module root. `weapon_descriptions.xslt` is neither — it is here because it now carries an applied edit, and unlike the other three the live copy is already CRLF, so live and snapshot are byte-identical rather than differing by line ending.

## How to restore from this snapshot

If a LOTRLOME update overwrites the working copies and breaks something we previously fixed:

```bash
cp "docs/reference/lotrlome-armory-snapshot/action_sets.xml" \
   "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/action_sets.xml"
```

Same for `monsters.xml` and `skins.xml` if needed. Then delete the compressed shader-cache sacks (see `feedback_shader_cache_invisible_cc.md` memory) and re-launch — Bannerlord will re-cook shaders and the action-set edits will take effect.

## Loading status — DO NOT REGISTER

These files **must not** be referenced by `Main/_Module/SubModule.xml` or moved into `Main/_Module/ModuleData/`. Bannerlord auto-loads `<module>/ModuleData/{action_sets,monsters,skins}.xml` from every enabled module's root, so duplicating these into TAOM's ModuleData would cause double-load conflicts (same IDs in two modules) — exactly the dead-duplicate state we cleaned up on 2026-05-04 (commit `307df40`).

## CC parent fix — required action_sets checklist

The Character Creation parent menu renders both parents with race-specific skeleton + animations via an engine lookup of `as_<race>_facegen` (male) and `as_<race>_female_facegen` (female). If either ID is missing for any race that TAOM cultures consume, the parents render as a contorted / T-pose mesh (see screenshot in CHANGELOG 2026-05-22 entry).

After restoring from this snapshot — or after any LOTRLOME update — `action_sets.xml` MUST contain BOTH the male and female facegen entries for every race below:

| Race | Required `_facegen` entries | Source of CC parent anims |
|---|---|---|
| `berserker` | `as_berserker_facegen`, `as_berserker_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |
| `cave_troll` | `as_cave_troll_facegen`, `as_cave_troll_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |
| `dg_uruk` | `as_dg_uruk_facegen`, `as_dg_uruk_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |
| `dwarf` | `as_dwarf_facegen`, `as_dwarf_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |
| **`elf`** | **`as_elf_facegen`, `as_elf_female_facegen`** | **TAOM-authored 2026-05-22 — base_set=`as_human_warrior` (elf monster uses human skeleton)** |
| `goblin` | `as_goblin_facegen`, `as_goblin_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |
| `orc` | `as_orc_facegen`, `as_orc_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |
| `pale_uruk` | `as_pale_uruk_facegen`, `as_pale_uruk_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |
| `uruk` | `as_uruk_facegen`, `as_uruk_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |
| `uruk_hai` | `as_uruk_hai_facegen`, `as_uruk_hai_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |

The races `hill_troll`, `nazghul`, and `saruman` also have `_facegen` entries in LOTRLOME but are not consumed by any TAOM culture; they're listed here only so a future re-snapshot doesn't accidentally drop them.

The `sauron` race (issue #321 — elf clone for lord_1_17, adult `min_scale` 1.40, appended at the END of `skins.xml`/`monsters.xml`) is NPC-only and **intentionally has NO `_facegen` entries**: (**No longer a *verbatim* clone as of 2026-07-23** — the elf race's female skins moved to the vanilla human female basemesh while sauron's deliberately did not, since no female sauron ever spawns. A future "re-sync sauron with elf" audit must NOT undo that divergence.) facegen action_sets are required only for CC-playable races, and no culture's `cultures.json` `races[]` lists `sauron`. Do not "fix" sauron's **facegen** in a future facegen audit — it stays intentionally absent (NPC-only, not CC-playable; battles/conversations use `Monster.ActionSetCode` = `as_human_warrior` directly). Its settlement/map **civilian** sets, which the engine GENERATES from the base monster id (`as_sauron_lord`, `as_sauron_villager`, `as_sauron_map`, … via `ActionSetCode.GenerateActionSetNameWithSuffix` / `MBGlobals.GetActionSetWithSuffix`), formerly didn't exist and resolved through the engine's **native silent fallback** on missing ids — the same path elf rode. As of **2026-07-11** those civilian sets are authored for sauron — and elf, plus the 3 prop-carry sets every non-human race was missing — as `as_human_*` aliases; see "Civilian action-set family coverage" below.

**Quick sanity check after any restore** (run from a shell with grep):
```bash
grep -oE 'id="as_[a-z_]+_facegen"' "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/action_sets.xml" | sort -u
```
Output should contain `as_elf_facegen` and `as_elf_female_facegen`. If either is missing, the snapshot got partially overwritten — re-apply the elf block from the snapshot file directly.

That grep sees ids only, so it cannot see a **structural** defect. Run these two as well:

```bash
# Exit 0 == no humanoid parity gap AND no root-level <action>. See the parity section below.
python tools/audit_action_set_parity.py

# Idempotence probe: on a clean file both copies print "0 stray action(s) before, 0/12 group(s)
# nested" plus a skipped-warning per race. Pass --apply only if a copy reports strays.
python tools/oneoff/fix_orphaned_tavern_conversation_actions.py
```

**Each facegen action_set must contain ALL of the following directly declared** — the engine does NOT fall through `base_set` inheritance for these action types, so they must live in the facegen action_set itself, not in its base:

1. **14 CC parent action types** — 7 × Bannerlord 1.2 names (`act_character_creation_<gender>_default_0..6`) AND 7 × Bannerlord 1.3 named slots (`_default_standing`, `_side_to_side_1`, `_mother_front`, `_father_sitting`, `_side_to_side_2`, `_side_to_side_3`, `_hugging`). The 2026-05-04 commit (`307df40`) added the 1.3 aliases to the 12 pre-existing facegen sets but did not author the missing `as_elf_facegen` pair; the 2026-05-22 fix closes that gap.
2. **7 `act_character_creation_toddler_0..6` actions** — mapped to `anim_toddler_0..6`.
3. **All `act_childhood_*` actions used by the post-parent narrative stages** (~60 of them: `tough`, `decisive`, `ready`, `leader`, `athlete`, `memory`, `numbers`, `manners`, `animals`, `streets`, `peddlers[_2]`, `militia`, `schooled`, `apprentice`, `fierce`, `sharp`, `vibrant`, `fox`, `gracious`, `clever[_2]`, `book`, `arms[_2]`, `artisan`, `polearm`, `roguery`, `spotting`, `manners_2[_3]`, `closed_tutor`, `confident_tutor`, `confident2_tutor`, `demure_tutor`, `hip_tutor`, `ready_bow`, `guard_up_staff`, `spoiled`, `explorer[2]`, `play[_2]`, `genius`, `honor`, `hardened`, `grit`, `defend`, `focus`, `strenght`(sic), `leader_2`, `contemplate`, `tactician`, `appearances`, `sceptic`, `ready_throw`, `riding_2`). Cross-reference LOTRLOME's `as_dwarf_facegen` (lines ~16812-17134 of `action_sets.xml`) for the authoritative list — when creating a new race facegen, copy that block verbatim.
4. **8 `act_childhood_toddler_*` actions** — `sleep`, `vigor`, `social`, `endurance`, `intelligence`, `control`, `cunning`, `tantrum`.
5. **Inventory / banner-editor / stand / sit poses** — `act_inventory_idle[_start]`, `act_visual_test_morph_animation`, `act_command`, `act_walk_idle_unarmed`, `act_stand_1`, `act_stand_2`, `act_sit_1`.
6. **12 rider/horse story-background actions** — `act_rider_story_background_1..6` and `act_horse_story_background_1..6`.

**Warning — slim facegen entries are insufficient.** A facegen action_set that declares only the 14 CC parent action types (relying on `base_set="as_human_warrior"` or similar to inherit childhood / toddler / inventory actions) WILL render the parent menu correctly but break **every subsequent CC stage** (Early Childhood lying-down pose, Youth / Adolescence / Adulthood narrative options without any character animation). This was caught on 2026-05-22 after the first in-game test of the slim elf entries; the v2 fix replaced them with verbatim copies of LOTRLOME's `as_dwarf_facegen` / `as_dwarf_female_facegen` blocks (with only `id` and `base_set` renamed). See memory `feedback_lotrlome_action_set_aliases.md` 2026-05-22 addendum for the "declare everything, don't trust inheritance" rule.

**Important rule** carried over to memory `feedback_lotrlome_action_set_aliases.md`: when fixing CC parent rendering, the recipe must both (a) **patch** existing facegen action_sets with 1.3 aliases AND (b) **create** missing facegen action_sets for any race in TAOM cultures that LOTRLOME's authors never anticipated as a playable race. Patching alone is not enough.

## Standalone combat action_set parity — `as_dwarf_warrior`

Separate from the `_facegen` (Character Creation) sets above: `as_dwarf_warrior` is the dwarf race's **combat** action set, and it is **standalone** — `skeleton="dwarf_skeleton_a"`, **no `base_set`**. Standalone sets inherit nothing, so every action type the engine gains after the set was authored is simply absent until added by hand.

This is the only LOTR race at risk. The other races' combat sets are stubs with `base_set="as_human_warrior"` (`as_orc_warrior`, `as_uruk_warrior`, `as_goblin_warrior`, and **both trolls** — `as_cave_troll_warrior`, `as_hill_troll_warrior`), and LOTRLOME's own `as_human_warrior` is a 48-line PARTIAL that **field-merges into Native's full `as_human_warrior`** (the load-order comment at the top of `action_sets.xml` explains this; the engine merge is confirmed in `Module.cs` `CreateProcessedActionSetsXMLForNative`) — Native carries the water/swim/stagger actions, so every `base_set="as_human_warrior"` race inherits them. `as_dwarf_warrior` has no merge partner.

Enumerating every `action_set` with a `skeleton=` and no `base_set` confirms the scope: the LIVE file has only **5** standalone sets — the `as_human_warrior` merge-partial, `as_dwarf_warrior`, and the creature mounts `as_spider` / `as_elephant` / `as_chariot`. `as_dwarf_warrior` is the **only standalone humanoid combat set**; the creature mounts use creature movement systems (the bipedal water-dive path doesn't apply to them).

`as_dwarf_warrior` was originally seeded from **Native 1.3** action types (`tools/Generate-ActionSets.ps1`). By Native **1.4.6** it had silently drifted to **423 missing active action types** — including the engine's water actions (`act_dive_*` / `act_swim_*`), which CTD the game when a dwarf falls into water (the 2026-06-25 crash). The fix restores full parity with Native's active `as_human_warrior`:

```bash
# Dry-run (shows the gap; 0 missing == at parity). Defaults: Native install path, set-id as_dwarf_warrior.
python tools/patch_dwarf_action_parity.py --target "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/action_sets.xml"

# Apply to BOTH the live file and this snapshot (additive, comment-safe, idempotent, writes a .bak):
python tools/patch_dwarf_action_parity.py --target "<live action_sets.xml>" --apply
python tools/patch_dwarf_action_parity.py --target "docs/reference/lotrlome-armory-snapshot/action_sets.xml" --apply
```

**Re-run after every engine bump** (it's in the `/engine-bump` checklist): a new engine version can add action types to `as_human_warrior` that the standalone dwarf set won't get automatically. The script is `--set-id`-parameterized for any future standalone humanoid set (`as_dwarf_warrior` is the default). Use an XML reader for any parity diff, not raw grep — Native comments out ~126 disabled actions that a text scan would wrongly count as "missing."

To find *which* sets need fixing (not just dwarf), run **`python tools/audit_action_set_parity.py`** first — it resolves every action_set's effective surface (own actions + full `base_set` chain + the cross-module merge) and exits non-zero listing any HUMANOID set short of Native's surface. As of 2026-08-03 it reports 0 humanoid gaps across all 1304 humanoid sets (1327 total; the 9 creature-mount sets spider/elephant/chariot use a separate surface — `audit_mount_parity.py` — and 14 are vanilla animal roots).

**It gates a second, unrelated failure class**: any root-level `<action>` — an element parented by `<action_sets>` instead of an `<action_set>`. That is structural rather than a parity gap, and it is **server-only**: the game client loads such a file silently, while the dedicated-server engine throws `KeyNotFoundException` in `MBObjectManager.MergeElements` at `/action_sets/action` and dies on boot. The audit exits non-zero on either class, so a green run proves both. Fixer for the orphan class: `tools/oneoff/fix_orphaned_tavern_conversation_actions.py` (see the 2026-08-03 snapshot-date entry).

## Civilian action-set family coverage — every race's townsfolk idles

Separate from the combat parity above: a settlement NPC's idle-role animation comes from a
GENERATED action-set name `as_<race>[_female]_<suffix>` (`villager` / `villager_2` / `lord` /
`beggar` / `guard` / notable / tavern / `villager_carry_*` / `map` …), built by
`ActionSetCode.GenerateActionSetNameWithSuffix`. If `as_<race>_<suffix>` doesn't exist the
lookup silently falls back to ONE default set, so **every NPC of that role plays the same
idle**. Elves shipped with ONLY `as_elf_facegen`/`_female_facegen`, so every elf civilian
shared one animation in every town (the 2026-07-11 report); every other non-human race was
also missing the 3 prop-carry sets (`villager_carry_bucket_on_lefthand`, `villager_carry_fish_buckets`,
`worker_carry_wood_on_shoulder`).

Elves/sauron use the human skeleton and have no race-specific civilian clips, so the fix
aliases each missing set to the human (or, for an own-skeleton race, its own) family:

| Race skeleton | root | `base_set` for a missing `as_<race>_<suffix>` |
|---|---|---|
| human-skeleton (elf, sauron, orc, uruk, uruk_hai, goblin, dg_uruk, pale_uruk, berserker, nazghul, saruman) | `as_human_warrior` | `as_human_<suffix>` (correct role animation, shared skeleton) |
| own-skeleton (dwarf) | `as_dwarf_warrior` | `as_dwarf_villager` (same skeleton, safe generic idle — never a human clip on the dwarf rig) |

Thin self-closing aliases are the proven form — vanilla's own female carry sets are exactly
this (`as_human_female_villager_carry_axe base_set="as_human_villager_carry_axe" />`); civilian
lookups DO fall through `base_set` (unlike the facegen path, per the elf-CC RCA above). The
generated block lives between `<!-- TAOM-CIVILIAN-COVERAGE:START/END -->` markers.

```bash
# Audit coverage per race (read-only; exits non-zero if any settlement race has a gap):
python tools/audit_civilian_action_set_coverage.py

# (Re)generate the alias block — idempotent, comment-safe, writes .bak, --dry-run default.
# Run against BOTH the live file AND this snapshot:
python tools/generate_race_civilian_action_sets.py --apply
python tools/generate_race_civilian_action_sets.py \
  --target "docs/reference/lotrlome-armory-snapshot/action_sets.xml" --apply
```

**Re-run after every engine bump** (a new human civilian set becomes a new per-race gap) and
after any LOTRLOME update that overwrites the live file. Trolls (`cave_troll`/`hill_troll`) are
intentionally NOT covered — although their monsters carry `_settlement` boilerplate, no TAOM culture
assigns a troll race to townsfolk/notables, so they never stand in a town and their civilian family is N/A.

**Do not blame this generator for the 2026-08-03 orphan bug.** The twelve sets repaired then are
`as_<race>_female_villager_in_aserai_tavern` — a tavern civilian family, so the resemblance is
misleading. They are hand-authored and sit OUTSIDE the marker block (in the snapshot: the tavern sets
at lines ~17.9k / ~54.4k / ~57.6k, the markers at 61194-61401), and the generator only ever rewrites
the region between `TAOM-CIVILIAN-COVERAGE:START` and `:END`. A future audit that "fixes" the
generator for this will be fixing the wrong file.

### ⚠️ APPLIED EDIT — female dwarf `underwear_bottom_mesh` in `skins.xml` (#403 fix, 2026-08-07)

**Live edit to `LOTRLOME_Armory/ModuleData/skins.xml` that an Armory update WILL silently revert.**
Live backup: `skins.xml.bak-dwarf-female-underwear` (true pre-change state). LIVE and this snapshot
are both patched and in sync.

```diff
 <race id="dwarf">   <skin gender="1" name="woman" …>
-      underwear_bottom_mesh="sk_dwarf_underwear_female"
+      underwear_bottom_mesh="sk_dwarf_underwear_female_a"
```

The bare name is not a shipped resource — the armory ships `sk_dwarf_underwear_female_a` (8
occurrences), and the short form appears **only as a prefix of it**. That prefix relationship is the
trap: any substring search reports the mesh present. Check with a trailing-boundary match, or just
run the audit below.

Two players hit it as a hard CTD whenever a female dwarf came into view in a settlement, battle or
tournament — never in dialogue, because the facegen path does not draw underwear. Native access
violation (`0xC0000005` at `TaleWorlds.Native.dll+0x58232C`, faulting address `0x24C` = a 16-bit
index read at a null base), no managed exception, therefore **no TAOM crash bundle** — which is why
it went unattributed for so long. It was the only unresolved mesh reference in the file, and the
adult female dwarf is the only dwarf skin with a non-empty `underwear_bottom_mesh` (males and every
child entry are `""`), so it touched female dwarves and nothing else. Found by @Sternab (#403).

**Gate for this whole defect class — run after any Armory update or `skins.xml` edit:**

```bash
# Exit 0 == every mesh name resolves to a real .tpac entry. skins.xml was ALWAYS in this
# tool's scan root; it simply did not know the eight skin mesh attribute names until 2026-08-07.
python tools/validate_mesh_refs.py --no-rgl-log
```

Verified both ways on 2026-08-07: **red** on the pre-fix backup (`MISSING_MESH … skins.xml:1780`),
**green** afterwards — 0 errors across the whole Armory ModuleData (items, crafting pieces, skins).

### Two dwarf-skin divergences from vanilla that are NOT defects

Checked against `Native/ModuleData/skins.xml` on 2026-08-07 while validating the fix above. Both look
wrong at a glance and neither should be "fixed":

- **`uses_stitching="false"`** — diverges from vanilla's `true`, but so do **22 skins across 8
  LOTRLOME races** (berserker, dg_uruk, dwarf, elf, goblin, pale_uruk, sauron, uruk_hai). It is the
  house convention for custom-mesh races, not a dwarf defect.
- **`body_mesh_suffix=""` on the adult female dwarf** — this one IS a genuine one-of-a-kind outlier:
  across all 14 races the pattern is uniform (adult `woman` = `_fem`, the three kid rows = `_fem`,
  `toddler_female` = `""`), and `dwarf/woman` is the **only** adult female in the file set to `""`.
  It is still very likely deliberate: she is also the only skin in the file with a dedicated female
  body-mesh family (`sk_dwarf_bm_f1_*`), whereas every other race's `woman` reuses the male/base
  meshes and needs `_fem` to pick up female armour variants. There are no `_fem` dwarf armour meshes
  in the module at all, so setting `_fem` here would point every armour slot at something that does
  not ship. **Do not change it without a symptom.** (Note the corollary: the other races carry `_fem`
  with no `_fem` armour variants either and do not crash, so a missing *suffix* variant is evidently
  survivable — unlike a missing *skin* mesh, which is the CTD fixed above.)

## Known asset defects in LOTRLOME_Armory (not fixed here)

These are defects in the dependency module's **binary assets**, which this snapshot does not cover —
it holds XML only. Recorded so a future session does not re-derive them.

### ⚠️ APPLIED EDIT: shield-compatible polearms registered under `OneHandedPolearm` (2026-08-10, extended 2026-08-20)

**This is a live edit to `ModuleData/weapon_descriptions.xslt` that a future Armory update WILL
silently revert.** Re-apply with `python tools/register_one_handed_polearms.py --apply`
(`--revert --apply` removes it; the backup is `weapon_descriptions.xslt.bak-1hpolearm` beside the
live file). The nine `<AvailablePiece>` lines sit inside `<!-- TAOM-1H-POLEARM:START/END -->` markers
at the end of the `OneHandedPolearm` template, so the script is idempotent and the block is
greppable.

**The block covers two fixes.** Seven entries for the four Dale spears (2026-08-10, #449) and two
for the Black Numenorean lance `sm_md_num_lance_a` (2026-08-20). The lance is the same defect on a
troop tree that landed eight days later, caught by this edit's own gate on its first run against
current trunk. That is the argument for the gate, made by the gate. An install patched before the lance was
added carries the superseded `TAOM-DALE-1H` marker; the script sweeps it, so re-running converges on
one block rather than listing the Dale pieces twice.

**Root cause.** A crafted weapon takes a `WeaponDescription` only when **every** piece it uses is in
that description's `<AvailablePieces>`, and the **first** match in the crafting template's
description order becomes the primary usage (`Crafting.cs:566-608`). The Armory registers 19
cultures' spear pieces under `OneHandedPolearm` and none of Dale's, so all four Dale spears resolved
to `TwoHandedPolearm`, whose usage set `polearm_block_long_shield_swing_thrust` is flagged
`requires_no_shield` (`Native/ModuleData/item_usage_sets.xml`). All 28 Dale spear rosters also carry
a shield, so the AI abandoned the spear the moment combat started. The Black Numenorean lance is the
same shape: `crafting_template="TwoHandedPolearm"`, blade plus handle, no inline `<Weapon>`, and 8
cavalry rosters carrying a shield. Registering the pieces makes one-handed-with-shield primary and
two-handed the alternate, the layout Rohan's spears already had.

**Scope.** The four spears (`dale_spear_a/b`, `dale_winged_spear_a/b`) and `sm_md_num_lance_a`.
Dale's halberds, poleaxe and war spear stay two-handed: they share *handles* with the spears but not
*heads*, and the every-piece-must-match rule means a shared handle alone grants nothing. No roster
pairs them with a shield either.

**Verified** by running the real transform chain offline (Native XML → this XSLT, lxml): the only
delta in the merged document is the `AvailablePiece` entries on `OneHandedPolearm`, no other
description touched. The Dale half was confirmed in game 2026-08-09 by a tester running the
equivalent change.

**Gate.** `python tools/audit_polearm_shield_parity.py` — fails when any roster pairs a shield with a
polearm whose primary usage forbids one. Proven to fire, twice: reverting the Dale half takes it from
PASS to exactly 28 extra findings (measured 2026-08-20 against current trunk), and it caught the 8
lance rosters on its own before they had ever been looked at.

### ⚠️ APPLIED EDIT — `UseTeamColor="true"` on 79 Isengard items (#389 fix, 2026-08-06)

**This is a live edit to `LOTRLOME_items/isengard/` that a future Armory update WILL silently revert.**
Re-apply with `python tools/oneoff/fix_uruk_hai_hands_teamcolor.py --apply`
(`--revert` restores from the `.bak-teamcolor` backups beside each file).

**Root cause.** `m_uruk_hai_hands_a1` is authored to require the shader flag
`use_double_colormap_with_mask_texture`. The only thing that adds it is
`AgentVisuals.AddTeamColorToMesh`, which the engine calls **only when the item carries
`UseTeamColor="true"`** — it does `GetMaterial().CreateCopy()` (hence the `(copy)` suffix seen in the
Patch67 render census) then `AddMaterialShaderFlag(...)`. Measured at runtime:

| item flag | material as bound | shader flags | result |
|---|---|---|---|
| `UseTeamColor="false"` | `m_uruk_hai_hands_a1` | `0x480090` | **black** |
| `UseTeamColor="true"` | `m_uruk_hai_hands_a1(copy)` | `0x4C0090` | correct |

Delta is exactly `0x40000` — the mask flag. 13 Isengard head items already had it and rendered fine;
79 did not and were black. Every mesh binding that material now team-colours (92/92).

Changed: 43 `head_armors.xml`, 12 `arm_armors.xml`, 10 `body_armors.xml`, 8 `shoulder_armors.xml`,
6 `leg_armors.xml`. 158-line diff, CRLF preserved, item counts unchanged, `validate_moduledata.py` PASS.

**Backups use `.bak-teamcolor`, deliberately NOT `.xml`** — Bannerlord globs `*.xml` in these
registered folders, so an `.xml` backup injects duplicate item ids.
**Item XML loads at process launch**, so testing requires a full game restart, not a save-load.

The cleaner upstream fix is below: the hand/glove sub-meshes should not be bundled into helmet,
bracer, greave and pauldron MetaMeshes in the first place.

### Uruk-Hai helmets bundle glove/hand sub-meshes (the upstream cause of #389)

Every `sk_uruk_hai_helmet_*` MetaMesh carries three materials where the equivalent orc helmet carries
one. From `LOTRLOME_Armory/Shaders/D3D11/shader_compile_report.log` (`mesh material shader variants`):

```
sk_uruk_hai_helmet_sword_light_a2 -> m_uruk_hai_gloves_a1(120), m_uruk_hai_hands_a1(888), m_uruk_hai_helmet_a1(120)
sk_uruk_hai_helmet_spear_light_a3 -> m_uruk_hai_gloves_a1(120), m_uruk_hai_hands_a1(888), m_uruk_hai_helmet_a1(120)
sk_uruk_hai_helmet_bers_a2        -> m_uruk_hai_gloves_a1(120), m_uruk_hai_hands_a1(888), m_uruk_hai_helmet_a2(120)
sk_uruk_hai_helmet_skir_a2        -> m_uruk_hai_gloves_a1(120), m_uruk_hai_hands_a1(888), m_uruk_hai_helmet_a1(120)
sk_uruk_hai_helmet_sword_light_a4 -> m_uruk_hai_gloves_a1(120),                           m_uruk_hai_helmet_a1(120)

sk_gn_orc_mrd_helmet_light_a      -> m_md_orc_helmets_b(120)     <-- working control: ONE material
```

A helmet should not contain glove or hand geometry, and the **888 variant count** on
`m_uruk_hai_hands_a1` is the signature of a *skin* material (skin/morph permutations) rather than an
armour one. Every troop wearing one of these renders as a black silhouette in the encyclopedia;
`urukhai_recruit`, the only Uruk-Hai troop with no Head item, renders correctly. Source geometry:
`Assets/Isengard/isengard_armors/SK_Uruk_Hai_Helmets_{A,B}_geo.tpac`. All referenced materials and
`_d`/`_n`/`_s` textures exist — nothing is missing, the bundling itself is the anomaly.

**Confirmed as the cause and worked around, not corrected.** #389 is closed by setting
`UseTeamColor="true"` on the 92 affected items (see the APPLIED EDIT above); the bundling itself is
still wrong and is tracked as **[#398](https://github.com/haterade22/TAOM/issues/398)**. Investigation:
[`docs/reviews/rca-isengard-black-tableau-2026-08-06.md`](../../reviews/rca-isengard-black-tableau-2026-08-06.md).

### 42 meta-meshes referenced but absent from the packages (#390)

`CONTENT WARNING: Meta mesh with name <X> cannot be found!` for 18 `sm_uruk_*` weapon parts, 18
`wm_mordor_set1_*` weapon parts, 5 `sm_mordor_shield_*` and `wm_gondor_shield_dd`. All are referenced
by crafting-piece / item definitions in `ModuleData/LOTRLOME_items/` and exist nowhere in the install.
Long-shipping and invisible because they are warnings rather than errors.

### The dual-tree trap (applies to any binary fix)

A dev machine with a Modding Kit `Assets/` directory loads the **loose** tree; a player install loads
`AssetPackages/pack*.tpac`. Confirm which by grepping a session's `rgl_log` for `Loading packages`.
**Any asset fix must land in both**, or it works for the developer and not for players.

## Snapshot date

2026-08-20: `weapon_descriptions.xslt` **patched in place (LIVE)** with nine `<AvailablePiece>`
entries under `OneHandedPolearm`, inside `<!-- TAOM-1H-POLEARM:START/END -->` markers: the four Dale
spears' blades and handles (added 2026-08-10) plus the Black Numenorean lance's (added 2026-08-20,
replacing the Dale-only `TAOM-DALE-1H` marker). See the APPLIED EDIT section above. Unlike the other
three files here, live and snapshot are **byte-identical**, because the live copy is already CRLF, so there
is no line-ending skew to normalise, though `tr -d '\r' | md5sum` remains the safe comparison.
`action_sets.xml`, `monsters.xml` and `skins.xml` untouched. **Not yet verified in the engine by this
session**. A tester confirmed the equivalent Dale change in game on 2026-08-09, but the in-game
smoke against *this* file (Dale cavalry keep spear + shield, Black Numenorean cavalry keep the lance,
Dale halberd troops unchanged) is still owed.

The refresh also picks up ten days of unrelated Armory drift, all of it from the Black Numenorean
generator run (`32e24144`, 2026-08-18): `OneHandedSword` +9 and `TwoHandedSword` +11 sword pieces,
and the lance's blade and handle under `TwoHandedPolearm`, `TwoHandedPolearm_Bracing` and
`TwoHandedPolearm_Couchable`. Worth reading twice: that generator registered the lance in three of
the four polearm descriptions and skipped `OneHandedPolearm`, which is precisely the omission that
made the Dale spears unusable. The defect class reproduces itself through the authoring tools, not
just through hand edits.
Previous: 2026-08-07 — `skins.xml` **patched in place (LIVE + this snapshot)**: one attribute, the adult female
dwarf's `underwear_bottom_mesh`, `sk_dwarf_underwear_female` → `sk_dwarf_underwear_female_a` (#403).
See the APPLIED EDIT section above for the evidence and the audit that gates it. Both copies still
parse; line endings preserved on each side (live LF, snapshot CRLF — compare with
`tr -d '\r' < file | md5sum`, never a raw byte or hash comparison). `action_sets.xml` and
`monsters.xml` untouched. **Not yet verified in the engine** — the reporters' repro (walk into an
Erebor keep interior with a female dwarf present) has not been re-run against the corrected file.
Previous: 2026-08-03 — `action_sets.xml` **patched in place (LIVE + this snapshot)**: 168 root-level `<action>` elements re-parented back into the twelve `as_<race>_female_villager_in_aserai_tavern` sets that had been authored SELF-CLOSING (61434 → 61402 lines; 192 insertions / 224 deletions). This is a **reparenting, not a re-snapshot** — no action was added or removed: the file still holds 1226 `action_set` and 34247 `action` elements, and now 0 root-level `<action>`. Each group's 14 female-conversation overrides matched vanilla's own `as_human_female_villager_in_aserai_tavern` byte for byte, in order, which is what made a mechanical fix safe. Motivation: the game client tolerates the malformed file, the dedicated-server engine does not — it throws `KeyNotFoundException` in `MBObjectManager.MergeElements` at `/action_sets/action` and dies on boot, which is why server operators had to run the single-player module order. Fixer `tools/oneoff/fix_orphaned_tavern_conversation_actions.py` (rewrites both copies in one pass); guard `tools/audit_action_set_parity.py`, which now exits non-zero on any root-level `<action>`. **LIVE and this snapshot are in sync as of this date** — both 3,902,345 bytes, sha256 prefix `ad6675f49b12ad74`. `monsters.xml` and `skins.xml` untouched. Not yet verified in the engine: no dedicated server has been booted against the corrected file.
Previous: 2026-07-31 — `action_sets.xml` **fully re-snapshotted** from the live install (61044 → 61434 lines). The mirror had drifted 390 lines behind since the 2026-06-25 partial patch, and the missing region was the spider-rider partial redefinition of `as_human_warrior` at the top of the file — the block carrying the `LOAD-ORDER CRITICAL: this partial MUST precede every race set that declares base_set="as_human_warrior"` comment, added during the June 2026 mount work. Any audit run against the mirror before this date was auditing data the game never loads. `monsters.xml` and `skins.xml` were verified identical to live **after newline normalisation** and left untouched — they are NOT byte-identical: this snapshot stores both with CRLF while the live files are LF, so `skins.xml` is 5,899,564 bytes here vs 5,678,589 live, and `monsters.xml` 69,267 vs 67,296. Compare with `tr -d '\r' < file | md5sum` on both sides (verified equal 2026-08-06); a raw byte or hash comparison will report a spurious difference. Both `tools/audit_action_set_parity.py` (0 humanoid gaps across 1304 sets) and `tools/audit_civilian_action_set_coverage.py` (all 13 settlement races 43/43 male, 39/39 female) pass against the live files as of this date.
Previous: 2026-07-23 — `skins.xml` patched in place (LIVE + this snapshot): all 5 **female** `<skin>` blocks of `<race id="elf">` re-pointed off the male `sk_elf_basemesh_a1_*` set onto the vanilla human female set, attribute-for-attribute identical to Native's own female skins at each maturity. Face assets follow the mesh: `<face_textures>` → `head_female_a/b/c/e` + `lod_material="head_female_a.lod"` + `color="0xFFCAD3E0"` (the elf's wider `face_texture1..10` tag coverage is kept deliberately, so saved keys referencing tags 5-10 still resolve); `<mouth_textures>` → the `mouth_mat*` family (was the **dwarf** `m_dwarf_basemesh_mouth_a`); `<eyebrow_meshes>` → the vanilla `female_eyebrow_*` set (was a single `name=""` — female elves had no eyebrows). Adult female `<tattoo_materials>` gained the leading nameless `Cleanface` entry vanilla has, restoring index parity (33 → 34); `zero_probability="85"` deliberately left alone (LOTRLOME design choice governing random-NPC tattoo frequency, not part of the indexing bug). **Two traps for a future audit:** (a) the female toddler's `body_meta_mesh_shoulders="body_male_a_sh"` is CORRECT — vanilla's own `toddler_female` uses the male shoulders mesh; do not "fix" it. (b) **Male** elf skins are untouched and still carry the same class of defect (`toddler_male` pairs `sk_elf_basemesh_a1_shoulders` with a vanilla toddler body; `kid_3_male` wears `sk_elf_underwear_male_a` on a vanilla kid body; elf males still lack the nameless tattoo index-0) — deliberately out of scope, not an oversight. Backups beside the live file: `skins.xml.bak-elf-female` is the true pre-change state — **restore from that one**; `skins.xml.bak-elf-face` is a mid-session intermediate holding the KNOWN-BROKEN garbled-face state — **never restore from it**. RCA: [`docs/reviews/rca-elf-female-skins-2026-07-23.md`](../../reviews/rca-elf-female-skins-2026-07-23.md).
Previous: 2026-07-11 — `action_sets.xml` patched in place (LIVE + this snapshot): +194 civilian action-set aliases (208 lines) giving every settlement race the full townsfolk idle family — elf + sauron the full 82 each (they had only facegen), the other non-human races the 3 prop-carry sets they lacked. Additions-only, between `TAOM-CIVILIAN-COVERAGE` markers, generated by `tools/generate_race_civilian_action_sets.py` (audit: `tools/audit_civilian_action_set_coverage.py`). Fixes the "all elves do the same idle in every town" report. See "Civilian action-set family coverage" above.
Previous: 2026-07-02 — `skins.xml` + `monsters.xml` patched in place: appended race `sauron` (verbatim elf clone, adult `min_scale` 1.40, 5 Monster entries; issue #321) to BOTH the live files and this snapshot via a scripted append-at-end (`.bak-sauron` backups left beside the live files). No `action_sets.xml` change — the race is NPC-only and needs no facegen sets.
Previous: 2026-06-25 — `action_sets.xml` patched in place: +423 missing Native 1.4.6 action types added to `as_dwarf_warrior` for engine parity (+1311 lines, additions-only; the dwarf water-CTD fix). Done via `tools/patch_dwarf_action_parity.py`, NOT a full re-snapshot — so the snapshot still lags the LIVE file on the spider/elephant/chariot creature sets added during the June 2026 mount work (10 action_sets present in LIVE, absent here). Re-snapshot those separately if they ever need a restore.
Previous: 2026-05-22 — `action_sets.xml` re-snapshotted with elf CC parent entries appended.
Previous: 2026-05-04 — initial snapshot with the 1.3 action-type alias edits across the 12 pre-existing facegen sets.

If you re-snapshot later (e.g., after a LOTRLOME update we want to track), bump this date and note any changes vs. the previous snapshot.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/character-creation.md](../../features/character-creation.md)
- [docs/reference/armory-guide.md](../armory-guide.md)
- [docs/reviews/rca-isengard-black-tableau-2026-08-06.md](../../reviews/rca-isengard-black-tableau-2026-08-06.md)

<!-- backlinks-end -->
