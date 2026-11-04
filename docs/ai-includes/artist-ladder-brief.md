# Brief for KEYforce's AI assistant: TAOM's balance ladders

You are helping KEYforce (Erkam), TAOM's 3D artist, who also edits troop, weapon and armour data.
TAOM balances those through generated "ladders": numbers that climb with troop tier, set by tools,
not by hand. Edits that look harmless in XML have repeatedly broken them. Follow this brief on every
edit to troops, items, crafting pieces, weapon descriptions or name strings. If a rule here conflicts
with what KEYforce asks, say so and stop; Mike (the maintainer) decides.

You work in one of two places. The TAOM repo (`tools/`, `Main/_Module/ModuleData/`) holds the troop
data and every tool named below. The art mirror (`lotraom-assets`, copies of the TAOM module and
`LOTRLOME_Armory` under `v1.5/`) holds art and a copy of the data, but not the tools. In the mirror
you cannot run the tools: describe the intent (section 6) and leave the numbers alone.

Sources for everything below: `docs/features/ranged-ladders.md`, `docs/features/melee-damage-model.md`,
`docs/features/armor-balance.md`, `.claude/rules/troops.md`, `.claude/rules/moduledata-validation.md`,
`docs/reviews/rca-keyforce-lamedon-port-2026-09-25.md`, `docs/reviews/rca-harad-repair-2026-10-15.md`
and GitHub issue #805.

## 1. The ladders and why they exist

**Tier** is a pure function of `level=`: `clamp(ceil((level - 5) / 5), 0, 10)`. Level 6 to 10 is
tier 1, level 46 to 50 is tier 9. Every ladder below is indexed by it.

| Ladder | What it fixes | Where the numbers live |
|---|---|---|
| Skills | An upgrade must never make a troop worse at a skill (`UPGRADE_SKILL_REGRESSION`, an error) | The skill values in `troops_*.xml`; repair tool `rebalance_troops.py --fix-monotonicity` |
| Armour | Each kingdom has a cap (its elite chest value, e.g. Gondor 57, Harad 44, Erebor 70); every slot and band is a fixed ratio of it, so kingdoms differ in armour power on purpose (#583). A second ladder keys armour meshes to levels via the id token `_light_`, `_med_`, `_heavy_`, `_elite_`, `_lord_` (#609) | `tools/rebalance_armor.py` (`KINGDOM_CAPS`), `tools/fix_armour_mesh_ladder.py` |
| Ranged | Reach, damage, accuracy and Bow/Crossbow skill climb with tier inside a line, and better-ranked kingdoms beat worse ones at the same tier. Every troop bow or crossbow is a generated item `ladder_<line>_<bow\|xbow>_t<tier>` (#582, #617) | `tools/ranged_ladders.json` |
| Melee | Sustained damage per second (DPS, not one blow) must fit a band for the troop's tier and kingdom: a curve from 25 DPS at tier 0 to 114 at tier 10, band plus or minus 25%, ceiling 140 DPS (#631) | `tools/melee_ladders.json` |

Melee detail you need: Bannerlord stores no damage on a crafted weapon. Damage is simulated from its
pieces, and it lives on the **blade piece's** `damage_factor`. One blade backs many weapons, so
editing a blade moves every weapon and every troop on it. Never edit `damage_factor` by hand.

Ranged detail you need: a troop's launcher stats come from the line's cell in the spec, and the
troop's own Bow/Crossbow skill comes from the same cell. A raw bow on a troop breaks both.

## 2. Hard rules for troops (`Main/_Module/ModuleData/troops/troops_*.xml`)

1. **Never lower a skill on an upgrade edge.** A child troop's every skill must be at least its
   source's. Why: `UPGRADE_SKILL_REGRESSION` fails the commit gate. Recent case: a tree replacement
   shipped 7 such errors and 43 skill values needed clamping.
2. **Never delete a troop and never change an id.** To retire one, remove it from every
   `upgrade_targets` and keep the entry in the file; rename display names only. Why: a save holding
   a deleted troop keeps a hollow character that engine daily ticks read (#670). Two KEYforce drops
   deleted troops (`gondor_ring_peasant`, then 25 Harad troops) and Mike had to accept saves breaking.
   Gate: `TROOP_ID_REMOVED` (`tools/shipped_troop_ids.txt`).
3. **Fill each armour slot in every battle set of a troop, or in none.** The engine draws each slot
   from an independently chosen set, so a slot filled in one set and empty in another ships
   combinations nobody authored (`INCONSISTENT_ARMOUR_SLOT`). The same goes for weapons: if any
   battle set has a bow in a slot, every battle set has a bow in that slot, with arrows at one fixed
   slot index. Keep at least one weapon in the same slot in every set
   (`INCONSISTENT_WEAPON_SLOT`). Pair `Horse` and `HorseHarness` consistently.
4. **Troop bows and crossbows must be ladder cells.** Put `ladder_<line>_<bow|xbow>_t<tier>` for the
   troop's line and engine tier, never a raw bow, and never hand-set Bow/Crossbow skill. Why: your
   Harad longbows (`hr_st_longbow_light/med/heavy`) out-shot the tier-7 Serpent marksman on lower
   tiers. To give a line new bow art, change that line's `donor` or `donor_by_band` in
   `tools/ranged_ladders.json`, then regenerate (section 5); the cell stats stay.
5. **A tier a line has no cell for needs a spec entry first.** A troop at an unlisted tier is a
   `RANGED_LADDER_INVERSION` finding (Harad tier 7 had none, #803). Ask TAOM to add the cell.
6. **Armour mesh tokens must fit the level.** Allowed per level: light up to 6, light or medium up
   to 16, medium up to 21, heavy up to 26, heavy or elite up to 31, elite up to 36, elite or lord
   from 41 (`ARMOUR_MESH_TIER_LADDER`). Over-dressing is the harmful direction: one low-level troop in
   `_lord_` chests pins the whole line's armour value low. Noble-line troops are judged one tier up.
7. **A higher tier never wears less total armour than its upgrade source**
   (`UPGRADE_ARMOUR_REGRESSION`). Use `tools/fix_upgrade_armour_regressions.py` (dry run first).
8. **Do not fan out weapons by name.** Names do not imply tier; a spear is placed by DPS against the
   melee band. A shield troop never gets a polearm whose primary usage is `requires_no_shield`.
9. **Display-name renames change English text that is already translated.** See rule 18.
10. `troops_goblin.xml` feeds three cultures (`goblin`, `bluecraig`, `mistymountainorcs`); an edit
    there lands on all three.

## 3. Hard rules for items (Armory XML, crafting pieces, weapon descriptions)

11. **Never re-stat armour by hand.** The curve tools own `body_armor`, `head_armor` and the other
    slots. Why: a Gondor re-stat of 49 Anorien pieces landed below the kingdom-cap curve and was not
    installed (#805 finding 2). A new armour piece gets the right id tier token and a plausible
    value; TAOM's tools place the number (section 6).
12. **Never re-stat weapon damage.** No `damage_factor` edits, no `thrust_damage` on ladder items.
13. **Player-reachable launchers need a difficulty a player can meet.** Anything in merchandise, loot,
    a start or career kit or an enlistment kit goes into the player's inventory, and
    `CharacterHelper.CanUseItem` refuses it below the skill. Your three longbows needed Bow 80, 100 and
    120 while sold in shops; an enlistment archer kit shipped `difficulty="100"` bows nobody could use.
    The enlistment archer-kit test pins launchers at difficulty 50 or less; keep other
    player-reachable launchers in that range, or mark them `is_merchandise="false"`. Troop
    bows are `is_merchandise="false"` clones and keep the donor's difficulty, which is why rule 4
    matters.
14. **Spear and lance pieces go into every `WeaponDescription` the weapon should use.** In
    `weapon_descriptions.xslt` register the pieces in `OneHandedPolearm`, `TwoHandedPolearm`,
    `TwoHandedPolearm_Bracing` and, for anything that couches, `TwoHandedPolearm_Couchable`. A
    description applies only when every piece the weapon uses is listed in it. Why:
    `TwoHandedPolearm`'s primary usage forbids a shield; `OneHandedPolearm` comes first in Native's
    template, so listing the pieces there makes one-handed-with-shield the primary usage. A spear
    missing from it makes shield-carrying troops drop it for a sword when the fight starts, and a lance
    missing from Couchable cannot couch. Your Harad spears were left out of Couchable.
    `tools/register_one_handed_polearms.py` replays the `OneHandedPolearm` registration;
    `tools/audit_polearm_shield_parity.py` is the gate.
15. **Keep every mesh's own collision body and runtime cache.** A weapon or shield mesh `X` needs a
    `bo_X` body authored in its own FBX, never another kit's `bo_` (`COLLISION_BODY_BORROWED`, error).
    A `body_name` no package ships hangs mission loading forever (`MISSING_COLLISION_BODY`, error).
    A `.tpac` without its `RuntimeDataCache` `.rdc` is skipped silently; only the Modding Kit writes
    the `.rdc`, so open the module in the Kit and save, then run `check_rdc_entries.py`.
    Keep the existing misspelled `bo_capwm_isengard_shield_a02_clean`.
16. **Never rename a mesh that XML references.** A rename strands the refs and hangs preload. If art
    must move, repoint every ref in the same change (`audit_armory_refs.py` lists them), and never
    restore an old `.tpac` to quiet a gate.
17. **Register every player-facing name as `{=KEY}default`.** An unregistered key shows English in all
    12 languages with no error; 50 of your Harad item names shipped that way. Item names go as English
    rows in the Armory's `Languages/loc_<folder>.xml`; troop names are picked up by
    `generate_name_localization_strings.py`. Translation is a paid run: it is Mike's call.
18. **Do not change the English of an existing key casually.** The translator cache is keyed by id
    alone, so an English edit leaves 12 languages (and `tools/translation_cache/<lang>.json`) showing
    the old wording, and a later rebuild writes it back. If a rename is wanted, follow the checklist in
    `docs/localization/TRANSLATOR_GUIDE.md` "The checklist when you edit an existing English string",
    or hand it to Mike. A copied character needs its own key (`aom_<id>_name`), or every other
    language shows the original's name.
19. **Tell Mike before switching a material to a new shader.** Twelve of your bow materials used three
    new TAOM shaders that no release shader cache carries; Mike accepted the risk that bows may render
    wrong for players until the shaders ship (#805 finding 1). Do not repeat it silently.
20. **Items live in `LOTRLOME_items/<folder>/`.** A second folder defining the same id shadows one
    silently (`DUPLICATE_ITEM_DEF`); grep the id before adding. Keep backups as `.bak-*`, never
    `*.xml`: the folder is globbed and a `.xml` backup injects duplicate ids. Item XML loads only at
    process launch, so a new file needs a full game restart before it can be judged.

## 4. Generated files: never hand-edit

Edit the source and regenerate. A hand edit is overwritten by the next run, or fails its `--verify`.

| File | Generator (TAOM repo) |
|---|---|
| `Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml` (career kits) | `tools/generate_career_kits.py`, then `tools/wire_starter_kit_rosters.py --apply` |
| `taom_troop_name_strings.xml` and the lord, clan and kingdom name strings | `tools/generate_name_localization_strings.py` |
| `Main/_Module/ModuleData/armour_acquisition/armour_classes.xml` | `tools/generate_armour_classes.py` |
| `equipmentsets/taom_enlistment_equipment.xml` (enlistment kits) | `tools/generate_enlistment_rosters.py` |
| `LOTRLOME_items/<folder>/ranged_ladder.xml` and its name rows in `Languages/loc_<folder>.xml` | `tools/generate_ranged_ladder_items.py` |
| `LOTRLOME_items/<folder>/starter_kit.xml` (the `starter_<donor>` twins) | `tools/generate_starter_kit.py` |
| Bows, crossbows and ammo stats of the Armory's own launchers | `tools/restat_ranged_donors.py` (from `donor_stats`, `ammo_stats`) |
| `docs/reference/ranged-troops.html`, the troop roster page | `rebalance_ranged_ladders.py`, `generate_troop_roster_page.py` |
| `docs/reference/armory-catalogue/catalogue.tsv` | `tools/generate_armory_catalogue.py` |
| `CHANGELOG.md` | the release process; never by hand |

After any troop add, delete or gear change, **every generator that derives from troops must be
re-checked**, not just the validator: deleting one troop turned `generate_career_kits.py --verify`
red because it moved four career slots. Also, some tools write by default: read each tool's
docstring first. `rebalance_ranged_ladders.py` rewrites the tracked `docs/reference/ranged-troops.html`
even without `--apply`, and `audit_armory_refs.py` rewrites `docs/audits/armory-ref-audit.md` unless
given `--report -`.

## 5. Checks to run before every commit

TAOM's CI was removed on 2026-10-05. The repo's git hooks (`.githooks/commit-msg`, `pre-commit` and
`pre-merge-commit`) run on a plain `git commit` once the clone sets `git config core.hooksPath .githooks`; set
it once per clone. Without it nothing runs on your commit, and a Harad tree replacement plus the next commit
reached trunk with red gates for five days. The hooks run a subset, so run these yourself too, from the TAOM
repo root, with the game install present. A check that prints SKIPPED has not passed; the script reports it as NOT RUN.

```
python tools/data_gates.py --all
```

It prints PASS, FAIL or NOT RUN for each gate, and the dotnet rows print NOT RUN with their command (ask Mike for those, below). It covers what this list held before: `validate_moduledata.py` (read the warnings as well as the errors), the career-kit, name-localization, armour-class, ranged-ladder-item, donor and starter-kit generator checks, `audit_polearm_shield_parity.py`, `audit_armory_refs.py --report -` (meshes, collision bodies, dead item ids), `check_rdc_entries.py` (a listed package needs a Modding Kit save), `register_armory_names.py --check`, `check_external_loc_coverage.py`, `check_generator_item_refs.py`, `validate_xml_schemas.py` and `check_external_xslt.py`. A plain `python tools/data_gates.py` runs only the gates for the files git shows as changed; name a live Armory file you edited on its command line.

Order and baselines:

- `register_armory_names.py --check` comes first: new `{=KEY}` names need a Languages row; fix with `--apply --prefix <your ids>`.
- `check_external_loc_coverage.py` comes after any name or language edit. `--apply` raises the untranslated count by N rows per language: run its `--update-baseline` in the same change. Translating those rows is a separate paid run.
- `generate_armour_classes.py --check` exits 1 when stale and 2 with no Armory; fix a stale one with `--apply`.

`generate_ranged_ladder_items.py --verify` and `generate_starter_kit.py --verify` check the live Armory
only. `restat_ranged_donors.py --verify` also checks the lotraom-assets mirror; drift reported only in its
`mirror` tree means the mirror lags the live install, and Mike syncs it. Never run a generator with
`--asset-repo` to quiet it.

Warnings to treat as blocking for your change: `RANGED_LADDER_INVERSION`, `RANGED_DAMAGE_CEILING`,
`MELEE_LADDER_INVERSION`, `ARMOUR_MESH_TIER_LADDER`, `UPGRADE_ARMOUR_REGRESSION`,
`CROSS_CULTURE_ARMOUR_INVERSION`, `ARMOUR_CLASS_TABLE_DRIFT`, `INCONSISTENT_ARMOUR_SLOT`,
`INCONSISTENT_WEAPON_SLOT`. Compare the
warning list before and after your edit; a new one is yours to fix or to report.

The C# test suite (`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`) also reads troop and kit data (pools, enlistment kits,
shipped-data coverage). If your change touches troops, ask Mike to run it; a tree replacement left six
of its tests red.

Never commit a file you did not change. Stage explicit paths, never `git add -A`.

## 6. When unsure, describe the intent and let TAOM place the numbers

You do not need to pick a balance number. Instead, write down what you want and hand it over:

- **New armour piece:** the mesh ids, the kingdom, the slot, and which troop line and level should
  wear it. Give the id its tier token. TAOM runs the roster-first curve restat.
- **New or changed weapon:** the mesh ids, the crafting pieces, the kingdom, and the troops that
  should carry it. Register the pieces in all relevant descriptions (rule 14). TAOM prices the blade
  by DPS (`fix_melee_ladder.py`, `restat_melee_blades.py`). Blade restats are refused for couchable
  blades that would be raised, so a spear priced like the vanilla one it replaces (about 35 DPS) stays
  under its band until a person decides.
- **New bow art:** name the line and the donor mesh you want (`donor` or `donor_by_band` in
  `tools/ranged_ladders.json`). Do not put the bow on a troop.
- **New troop or tier:** the id, culture, level, upgrade source and wanted role. If the line has no
  ladder cell at that tier, ask for one.
- **Renames:** the old and new display name and every troop id affected.

The ladder tools default to a dry run and some write the live Armory, which is unversioned. Run them
without `--apply`, paste the plan into your message to Mike, and let him or a TAOM session apply it.

## 7. Before you hand work over

State in your message: which files changed, which section-5 checks you ran with their results, which
generated files you regenerated, which warnings are new, and anything you chose not to do because a
rule above blocked it. Do not claim a check passed that you did not run.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
