# Black Numenorean Line (Mordor)

## Overview

Adds Mordor's first human troops: a 13-troop Black Numenorean tree spanning T5 to T9 with Cavalry,
Infantry, and Archer branches, plus the 113 Armory entries that dress and arm them. Corrupted Men of
Numenor who serve Sauron, they are the highest-level line Mordor fields and the culture's only horse
cavalry. The line is deliberately AI-only: it appears in lord parties rather than in recruitment.

Assets shipped by Erkam to `E:\repos\lotraom-assets` on 2026-08-15 (`6abe7a65`) and 2026-08-17
(`2e4ced3c`). Neither commit touched any `ModuleData/`, so nothing referenced the meshes until this
pass. Spec: `lotraom-assets/tools/mordor_armor_and_troops.md`, "Black Numenorean" section.

## Why This Exists

- **Before:** Mordor fielded Orcs (T1-T5), Morannon orcs (T2-T6), Black Uruks (T2-T7) and Nurn warg
  riders. Every one is `race="orc"` or `race="uruk"`, and the only mounted troops were warg riders.
  Mordor had no human troops and no horse cavalry at all.
- **Lore requirement:** the Black Numenoreans are Sauron's human nobility (the Mouth of Sauron was
  one). They should be the most heavily armoured thing Mordor puts on a battlefield, and they should
  ride.
- **Without it:** 106 authored meshes, 16 material packages and 48 texture packages sit in the
  Armory unreferenced.

## What Shipped

| Layer | Count | Location |
|---|---:|---|
| Armour items | 78 | `LOTRLOME_items/mordor/{head,body,shoulder,arm,leg}_armors.xml` |
| Crafting pieces | 22 | `LOTRLOME_crafting_pieces.xml` |
| Crafted weapons | 7 | `LOTRLOME_items/LOTRAOM_weapons.xml` (3 one-handed, 3 two-handed, 1 lance) |
| Shields | 6 | `LOTRLOME_items/LOTRAOM_shields.xml` |
| Troops | 13 | `Main/_Module/ModuleData/troops/troops_mordor.xml` |
| Party-template stacks | 13 x 16 | `taom_partyTemplates.xml` |

92 of the 106 authorable meshes appear on a troop roster. The 14 that do not: 8 lord-tier pieces
(authored, hero-reserved), the 4 light-tier pieces (see the armour ladder below), and
`sk_md_num_hood_a` / `_b`, two untiered plain hoods the spec doc never lists.

## The Tree

```
mordor_num_initiate (T5, level 26)
 |- mordor_num_cavalry  (T6,31) -> vet_cavalry  (T7,36) -> knight   (T8,41) -> temple_knight (T9,46)
 |- mordor_num_infantry (T6,31) -> vet_infantry (T7,36) -> warden   (T8,41) -> temple_guard  (T9,46)
 |- mordor_num_archer   (T6,31) -> vet_archer   (T7,36) -> marksman (T8,41) -> shadowbow     (T9,46)
```

Two equipment rosters per troop, three on the two T9 leaves that have spare elite variants.

## Facts a Future Session Should Not Re-derive

### Race: omit the attribute entirely

Black Numenoreans carry **no `race=` attribute**. `BasicCharacterObject.Deserialize` (v1.4.8, lines
315-328) sets `Race = 0` then only overwrites it if the attribute is present. TAOM has zero
`race="human"` occurrences anywhere: every human troop is simply unmarked. Writing `race="human"`
would work but breaks the convention.

That race index 0 is `human` is **strong inference, not decompile-proof**. `FaceGen`'s
`_raceNamesArray` comes from a native call, `MBAPI.IMBFaceGen.GetRaceIds().Split(';')`, so the
ordering algorithm is not readable from managed code. Three things support it: `Native/ModuleData/monsters.xml`
declares `<Monster id="human">` as its first entry, `Main/_Module/SubModule.xml` pins
`<DependedModule Id="Native"/>` with `order="LoadBeforeThis"`, and every existing TAOM race troop
already depends on the same assumption. The in-game smoke test is what finally settles it.

**`FaceGen.GetRaceOrDefault` does not live up to its name.** It is
`return _raceNamesDictionary[raceId];`, a bare dictionary indexer with no fallback, so an
unrecognised or mis-cased `race="..."` string throws `KeyNotFoundException` at load rather than
defaulting. That path is not reached by these troops (they omit the attribute entirely), but it
means a typo in any future troop's race string is a hard load failure, not a silent default.

The meshes support this. `SK_MD_Num_Inf_Helmets_A.fbx` rigs to `spine`, `spine1`, `spine2`,
`l_clavicle`, `r_clavicle`, `l_thigh`, `r_thigh`, `l_calf`, `r_calf`, the standard humanoid rig. A
second, weaker signal points the same way: `SK_MD_Num_Inf_Bracers_A_geo.tpac` references
`m_ar_art_gloves_a`, an Arnor material, so the glove section was built from the human Arnor set.

The 15 race ids that exist, from the merged `skins.xml` files: `human` (Native), then `dwarf`,
`uruk`, `nazghul`, `orc`, `uruk_hai`, `berserker`, `cave_troll`, `hill_troll`, `pale_uruk`,
`dg_uruk`, `goblin`, `elf`, `saruman`, `sauron` (LOTRLOME_Armory). There is no `numenorean` race and
no `men` race.

Body property is `BodyProperty.fighter_gondor`. TAOM has no Mordor human body property, and
Numenoreans and Gondorians are the same racial stock. The deleted `mordor_black_numenorean` troop
used vanilla `BodyProperty.fighter_empire`; `fighter_gondor` is TAOM-authored and therefore visible
to `validate_moduledata.py`, which checks body-property refs.

### Tier maps to level as `5T + 1`

`DefaultCharacterStatsModel.GetTier` is `ceiling((level - 5) / 5)` clamped to
`MaxCharacterTier`, and `TaomCharacterStatsModel` raises that cap to 10. So T5 through T9 is
**26 / 31 / 36 / 41 / 46**, not 21 through 41. Cross-check: `mordor_uruk_grunt` is level 11 and the
spec calls it T2, which the formula agrees with.

At level 46 the T9 troops are Mordor's strongest, above `mordor_uruk_captain` (36) and below only
`cave_troll` (51). That is in line with other cultures: Gundabad, Isengard and Rohan all top out at
41, Erebor and Dol Guldur at 46.

### Standalone elite line: what is deliberately NOT wired

`taom_spcultures.xml` is untouched. `elite_basic_troop` stays `mordor_uruk_warrior` and
`basic_troop` stays `mordor_uruk_grunt`. No volunteer pool gained an entry, so
`VolunteerRecruitmentService.Mordor.cs` and its boundary `[DataRow]` tests are unchanged.

The consequence is that `AllNonMilitiaNonBossTroops_AreReachableFromARecruitmentPoolRoot` fails
without an exemption, because nothing recruits an Initiate. That test's own failure message names the
fix for an intentionally AI-only line ("or (if intentionally AI-only) extend
IsIntentionallyUnrecruited"), so `mordor_num_` joins militia, `_boss`, `_merc` and `cave_troll` in
that clause with the reasoning recorded inline. **If the line is ever made recruitable, delete the
clause rather than widening it, and add `mordor_num_initiate` to the Mordor pools.**

They reach the field through lord party templates, the level-3 patrol, the vassal reward, and
prisoner recruitment (which then walks the upgrade tree normally).

**"AI-only" is the wrong label and an earlier draft used it.** `vassal_reward_troops_mordor` grants
`mordor_num_vet_infantry`, and `DefaultVassalRewardsModel` adds every stack in that template straight
into the joining player's own troop roster. Prisoner recruitment is a second player-facing route. The
accurate statement is narrower: **no volunteer pool offers them**, which is all the reachability test
measures. The exemption is also an explicit 13-id set rather than a `mordor_num_` prefix, because a
prefix exempts an unbounded namespace and would silently swallow a future orphaned troop. A companion
test pins the set against the troops actually defined, so it cannot drift in either direction.

### Party-template stacks must be followed by a re-normalise

`max_value` is a **ceiling, not a count**. The engine draws one uniform ratio per party and fills
each stack to `min + (max - min) * r`. Adding 13 stacks pushed each Mordor lord template from its
3500 target to 3653, so `tools/rebalance_party_template_maxes.py --apply` has to run afterwards; it
scales every stack's spread so the sum lands back on 3500. It is idempotent and absolute rather than
multiplicative, so the other 176 templates were no-ops.

The level-3 patrol and vassal-reward templates use exact `min == max` counts and sit outside that
tool's scope, which is why their single entries each are hand-set.

### Armour stats come from the curve function, not a copied table

`tools/generate_black_numenorean_armor.py` imports `rebalance_armor.calculate_stats` instead of
carrying a private `STAT_TIERS` dict. Four sibling generators each hold their own copy and all four
went stale at once, silently reverting a shipped shoulder fix (found 2026-07-31, now pinned by
`tools/tests/test_armor_curve_invariant.py::GeneratorCurveSyncTests`). Importing removes that failure
mode, and applies Mordor's `protection: -1` / `weight_mult: 1.10` from `CULTURAL_MODS` rather than
hardcoding the result.

Resulting rows (mesh tier maps one-to-one onto the curve tier):

| Tier | head | body (+arm) | arm | leg | shoulder (body/arm) |
|---|---|---|---|---|---|
| light | 14 | 19 (+6) | 7 | 11 | 4 / 2 |
| med | 23 | 31 (+10) | 13 | 19 | 8 / 5 |
| heavy | 31 | 41 (+14) | 19 | 27 | 12 / 10 |
| elite | 39 | 49 (+20) | 25 | 33 | 18 / 16 |
| lord | 47 | n/a | 33 | n/a | n/a |

That lands elite exactly 2 under Gondor elite at every slot, which is the #342 parity rule ("Gondor
leads shared kit by 1, Mordor-exclusive kit by 2"). Do not insert a sixth interpolated row: the
curve's 8-to-9-point steps exist to clear the plate legendary +12 across two tiers, and a 4-point
step breaks the two-tier invariant.

> **Do NOT run `rebalance_armor.py --apply --cultures mordor`.** Mordor is in `PRESERVE_CULTURES`
> because its kit is hand-authored. Measured 2026-08-17, a scoped run would rewrite existing items:
> Sauron's Pauldrons 50 -> 24, Captain's Chainmail 19 -> 60.

### Weapon numbers, and the constraint that turned out to be unsatisfiable

Measured blades: orc 1H tier 1 at Pierce 1.78 / Cut 2.67, uruk tier-3 at 3.12 / 3.74, hero
`wm_nazgul_sword_blade` tier 4 at 3.10 / 3.00, hero `wm_witch_king_sword_blade` tier 5 at 3.5 / 3.5.

The first draft aimed for "above uruk, below hero kit" and shipped 3.8 to 4.0 cut, which beat **every
hero blade in the game**. The framing was wrong, not only the numbers: the shipped uruk TROOP blade
already out-cuts both hero blades (3.74 against 3.50), so hero kit is not the ceiling on cut and
never was. The real ceiling for a troop blade is the best shipped troop blade.

Final: lead the uruk on thrust, stay at or under 3.74 on cut.

| piece | tier | thrust | cut |
|---|---:|---:|---:|
| `sword_1h_blade_a` / `_b` | 4 | 3.20 | 3.60 |
| `sword_1h_blade_c` | 4 | 3.30 | 3.70 |
| `sword_2h_blade_a` / `_b` | 4 | 3.30 | 3.70 |
| `sword_2h_blade_c` | 4 | 3.40 | 3.74 |
| `lance_blade_a` | 4 | 3.40 | none |

All blades cost `Iron6 x9` and guards `Iron5 x9`, matching every shipped Mordor blade. An earlier
draft keyed the count off blade length and made the 1H blades cheaper to craft than the tier-2 uruk
blade they outclass. Sword blades carry `<Flag name="Civilian" type="ItemFlags" />` like their
siblings; the lance does not, following the spear-head flag set.

`tools/rebalance_weapons.py` was **not** run. Its `ARMORY_DIR` still points at
`taommod/src/data/armory`, a dead tree, so its items-XML writes go nowhere.

Shields, after review pulled them under Gondor's ceiling: infantry 440 hp (med) and 505 (heavy) in
wood, cavalry 415 and 470 in metal. The first draft ran 460 / 560 / 430 / 520, which put the infantry
heavy above **every** Gondor shield (their best is `gond_shld4` at 520) rather than below it, against
the #342 direction. Lengths are the spec's measured mesh lengths, and every `<Weapon>` stat is a
whole number because the single-piece `<Item>` schema types them `unsignedInt`.

The shield `item_usage` / offhand-bone invariant has no automated check
([armory-shield-audit.md](../reference/armory-shield-audit.md) says so), so it was verified by parse
over the whole file during review: 232 shields, zero violations, and all 6 new ones are
`item_usage="shield"` with `ForceAttachOffHandSecondaryItemBone="true"` and no Primary.

### Traps that bit during authoring

| Trap | Detail |
|---|---|
| **`_slim` is not universal** | The spec claims every chest has a `_slim` variant. `sm_md_num_chest_light_a` does not. Exactly 18 `_slim` meshes ship, matching the 18 tiered chests, so `has_gender_variations="true"` goes on those 18 and `"false"` on the shared T5 chest. A generator that assumes a universal `_slim` emits a dangling ref. |
| **T7 cape pauldrons are cloth-only** | The spec lists `sm_md_num_{cav,inf}_pauld_cape_a`. On disk they are `clo_` cloth proxies with no plain renderable sibling, and no `cloth_bodies.xml` entry exists for any Numenorean piece. They are NOT authored. T7 wears the plain `pauld_med_a`; `cape_heavy_a` and `cape_elite_a` are real `sm_` meshes and work as static geometry at T8 and T9. |
| **Only `_a` shields have collision bodies** | 15 `bo_` meshes ship: 8 shield (4 `bo_` plus 4 `bo_cap_`, all `_a`), 1 lance, 3 sword_1h, 3 sword_2h. `inf_shield_med_b` points at the `med_a` pair and `inf_shield_heavy_b` at the `heavy_a` pair, the same hitbox-group pattern the orc shields use. |
| **The lance must exclude `swing`** | The head declares `<Thrust>` only and the `TwoHandedPolearm` description carries a `swing` token, so without `excluded_item_usage_features="swing"` the lance gets a swing attack with zero swing damage. This is the defect class from `rca-crafting-usage-features-2026-07-26.md` (20 mace heads). |
| **The spec's 2H `bo_` list is a copy-paste of the 1H one** | Its "2H Sword Parts" block repeats the 1H hull names. The real hulls are `bo_sm_md_num_sword_2h_blade_{a,b,c}` and they do exist. |
| **A Cavalry troop with a mountless roster is a silent bug** | `_isMounted` is set once at deserialize from `default_group`, never from the equipment, and `RandomBattleEquipment` picks uniformly among battle rosters with no check that the chosen one carries a Horse. So a Cavalry-grouped troop that rolls a mountless roster walks while the AI treats it as cavalry. All 8 cavalry rosters here carry both Horse and HorseHarness; nothing in the engine enforces that, so keep it true when editing. |
| **`F:\Project_TAoM\` does not exist** | The spec's stated source path is dead. Real FBX sources are under `lotraom-assets/v1.4/LOTRLOME_Armory/AssetSources/`. |

## Verification

```bash
python tools/generate_black_numenorean_armor.py --dry-run       # 78 items
python tools/generate_black_numenorean_weapons.py --dry-run     # 22 pieces + 7 crafted + 6 shields
python tools/apply_black_numenorean_troops.py --dry-run         # 13 troops
python tools/wire_black_numenorean_troops.py --dry-run          # 18 templates + weights + costs

python tools/validate_moduledata.py                             # PASS, no DUPLICATE_ITEM_DEF
python tools/validate_all_troop_refs.py                         # mordor 196 refs, 0 missing
python -m unittest discover -s tools/tests -p "test_*.py"       # 571 OK
dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=   # 6,655 passed
```

All four generators default to a dry run, are idempotent on re-run, read and write with `newline=""`
so line endings survive, and drop a dated `.bak-blacknum-<date>` sidecar before any write outside the
repo. The extension is deliberately not `.xml`: `LOTRLOME_items/<culture>/` is globbed `*.xml` and an
`.xml` backup would inject duplicate item ids.

`validate_mesh_refs.py --scan-bodies` reports the Black Numenorean bodies as missing, but so does it
for 744 pre-existing refs including `bo_mordor_shield_mid_a` and `bo_wm_witch_king_sword_blade`. The
cause is a tool-scope defect, not the data: `tpac_paths_for_modules` globs
`<game>/Modules/<m>/AssetPackages/*.tpac`, and `LOTRLOME_Armory` has no `AssetPackages` directory at
all. Its 5,272 tpacs live under `Assets/**`, so that gate has never seen an Armory body. All 15
Black Numenorean bodies were verified directly by length-prefix extraction from the shipping tpacs.

### Still owed: the in-game pass

Item files were appended to, not created, and their folder was already registered, so the
new-file-needs-restart trap does not apply. A **full game restart is still required** before any
visual check because there is no hot-reload.

1. Spawn a Black Numenorean in a custom battle and **confirm the meshes fit the human skeleton.**
   This is the one inference in the whole feature that only the game can settle. If they render
   distorted the fallback is `race="uruk"` plus a proportions re-check.
2. No bare hands, legs or heads (the cover-attribute failures).
3. The cavalry troops mount, and the harness is accepted rather than silently refused.
4. Swing a 1H sword and a 2H sword; couch the lance and confirm it has no swing attack.
5. **Check the lance blade sits at the tip of the shaft.** `piece_offset` is 0, matching the working
   Dale long shaft, but the spec suggested roughly 65 or -65 and was unsure of the sign. A floating
   or sunken blade is a one-attribute fix; `tools/BannerlordCraftingTool/` is the visual
   aligner for it.
6. Block with each of the 6 shields.
7. Load a Mordor lord party and confirm Black Numenoreans appear at a sensible frequency.

### The armour ladder, and why five tiers ride four rows

The line has **five troop tiers and four armour rows**, so one row has to serve twice. Review
measured the first attempt: the Initiate at 50 total personal armour was the single least-armoured
level-26 troop in the game (cohort n=157, median 157, next-lowest 82). It wore the light row because
the mesh tier names mapped one-to-one onto the curve tiers, which is right for the items and wrong
for the wearer.

Final assignment:

| tier | level | armour row | cape | total |
|---|---:|---|---|---:|
| T5 Initiate | 26 | med (4 rosters across all three lines' med kit) | none | 96 |
| T6 Cav / Inf / Arc | 31 | heavy | med | 145 |
| T7 Veteran | 36 | heavy | heavy | 154 |
| T8 Knight / Warden / Marksman | 41 | elite | heavy | 188 (archer 180) |
| T9 Temple | 46 | elite | elite | 200 |

Every branch is now strictly increasing: 96 / 145 / 154 / 188 / 200. A first attempt had T5 and T6
sharing the med row so that no mesh went unworn, which made the T5 to T6 upgrade cost resources and
grant **zero** added survivability. Codex flagged it; mesh coverage is not worth a dead upgrade edge.
The cape ladder carries the T6-to-T7 step, and the T8 elite row carries T7-to-T8.

The Initiate instead carries **four** rosters covering all three lines' med kit, since it is
pre-split and can plausibly wear any of them. That leaves the four light-tier pieces unworn: they are
calibrated for level 13 and below, and this line starts at 26.

**`derive_armor_tiers.py` cannot adjudicate this.** An earlier draft of this doc predicted it would
flag the light pieces as UNDER. It does not and structurally cannot: `derive()` applies the id
keyword unconditionally before ever consulting the roster anchor, so `_light_` in the id wins and
every item reports `delta: 0`. The roster band it would have used (`level_to_tier(26)` is `heavy`) is
what actually motivated moving the Initiate up.

### Three tooling hazards this feature exposed

**1. `rebalance_armor.detect_tier` used to mis-tier the whole set, and it was fixed here.**
`elite_keywords` contained the literal string `'black numenorean'`. Every item's display name is
`[Mordor] Black Numenorean <something>`, so a **light** hood classified as elite: 45 of 78 mis-tiered,
and `rebalance_armor.py --apply --cultures mordor` would have flattened them all onto the elite row.
It was a line-name keyword sitting in a tier-keyword list, and once this set shipped it matched those
78 items and nothing else in the Armory. Removed 2026-08-17; the id-based `_light_` / `_med_` /
`_heavy_` / `_elite_` tokens now decide, verified correct across all five tiers.

**2. A clan-heraldry regeneration would delete this feature.**
`tools/generate_clan_heraldry.py` operation C upserts `<MBPartyTemplate id="...">` **wholesale** from
the roster in `Main/_Module/ModuleData/clan_heraldry/mordor.json`. That JSON carries 15 clan rosters
and zero `mordor_num` entries, while the live templates now carry 23 to 26 stacks each. So
`python tools/generate_clan_heraldry.py --spec mordor --apply` would silently drop all 13 Black
Numenorean stacks from 15 of the 16 templates **and** revert the 3500 rescale. The JSON was already
stale before this feature; this widens the blast radius from a rebalance to a feature deletion.
**Fixed here.** `upsert_party_template` now refuses to replace a template when the spec would drop
troops the live file already has, naming exactly what would be lost. 19 of the 21 culture specs pass
unaffected; it catches Mordor and, separately, a **pre-existing** case where the Gondor spec would
have deleted 5 Lossarnach noble troops. Regenerating a spec (or adding the missing troops to it)
clears the refusal.

**3. Two validators are silently green on this feature.**
`validate_mesh_refs.py --scan-bodies` checks **nothing** in the Armory: `tpac_paths_for_modules`
globs `<game>/Modules/<m>/AssetPackages/*.tpac` and `LOTRLOME_Armory` has no such directory (its
5,272 tpacs live under `Assets/**`), so Tier B and Tier C both skip and it exits green. And
`validate_all_troop_refs.py`'s prefix regex covers 24 of this line's 79 item refs; the `sm_md_num_*`
**armour** (chests, pauldrons, greaves) is outside it, not just the weapons and horses. Both mesh and
body closure were therefore verified by hand, by length-prefix extraction from the shipping tpacs:
106 mesh refs and 15 collision bodies, all resolving, method validated against the Morannon set first.

### Gondor parity at level 46

Item for item, the elite row sits exactly 2 under Gondor's, which is the #342 rule. At the **troop**
level it does not: a Temple Guard totals 200 personal armour against `gondor_mt_fountain_guard`'s 194
and `gondor_ith_moon_guard`'s 180. That gap is a Gondor **rostering** gap, not a Black Numenorean
stat error. Those two troops do not wear Gondor's own best items, and `gondor_da_swan_knight` (the
level-46 flagship cavalry) has no `Horse` or `HorseHarness` in either roster at all. Nerfing this
line would break its exact curve conformance to paper over that. Worth its own issue.

For scale: across the whole level-46-plus band this line ranks about 32nd, below every
Rivendell, Lindon, Erebor, Mirkwood and Rhun troop, and below Dol Guldur's Khamul guards and knights
(259 and 252). Mordor did not get the best line in the game; it got a mid-pack one that happens to
out-armour two under-rostered Gondor troops.

## Owed Elsewhere

- **Report to Erkam:** the missing `_slim` on `chest_light_a`; the `clo_`-only T7 cape pauldrons and
  the absent cloth body; `m_ar_art_gloves_a` (an Arnor material) on the infantry bracer;
  `m_md_num_chainmail_a` shipping only `t_md_num_chainmail_b_*` textures; the dead
  `F:\Project_TAoM\` path in the spec.
- **Armory copy drift.** `sync-modules.ps1` maps `LOTRLOME_Armory` to `shared\LOTRLOME_Armory`, and
  `shared\`, `v1.2\` and `v1.3\` do not exist (only `v1.4\` does), so the script silently skips the
  Armory on every run. The live and assets-repo copies of the five `mordor/` item files had drifted
  by 88 / 66 / 48 / 40 / 208 lines with identical item counts, meaning stat edits. This pass appends
  to **both** copies, which cannot collide with that drift, but the drift itself is unresolved and
  deciding which side wins is a separate call.
- **`lord_SE9_c1` "Pagarios"** carries `skill_template="SkillSet.taom_black_numenorean_skills"` with
  `race="uruk"`, which looks wrong for a corrupted Man. Not changed here: the agreed scope was to
  author the lord-tier items without touching lord records.
- **Localization.** Item and troop names use the `{=aom_*}` key plus an English fallback, matching
  existing practice. No `sk_md_mor_` key exists in any Armory language file and no troop-name key
  exists in any `taom_*_strings.xml`, so a `/localize` run here would break with convention for one
  line rather than fixing it repo-wide.

## Related

- Spec: `E:\repos\lotraom-assets\tools\mordor_armor_and_troops.md`
- Prior art: the Morannon sub-line, `ae2313e8` (armour) then `9747c01b` (troops), RCA
  `docs/reviews/rca-morannon-2026-06-08.md`
- [armor-balance.md](armor-balance.md) for the curve, [troop-weight-system.md](troop-weight-system.md)
  for the 2.0 elite band, [weapon-creation-workflow.md](../ai-includes/weapon-creation-workflow.md)
  for the 4-file weapon path
