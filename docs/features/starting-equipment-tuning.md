# Starting Equipment Tuning

## Overview

Character-creation starting gear is deliberately weak and deliberately cheap. The culture-default rosters
hand out `starter_<donor>` twins of real items: same meshes, same crafting pieces, stats floored to a
per-class anchor, never above the donor, not merchandise, and (for crafted weapons) an explicit low price.
The career rosters, and the vanilla override built from them, hand out the real items each culture's
lowest troops carry (#629), so a career never starts better equipped than the troops it recruits. This doc
records how the engine computes damage and price (the parts that decide the design), how the kit is
structured, the tools that author and wire it, and what a change to any of it has to prove before it is
done.

## Why this exists

Two problems, a year apart. In 2026-06 players were selling their character-creation kit for about 20,000
denars, because item value is exponential in tier and a chest with three armour numbers tiers up fast. In
2026-09 (#569) the kit was found to be some of the best gear in the game: the Gondor starter sword swung at
damage factor 4.18 against a one-handed-sword median of 3.8 across TAOM and vanilla, the Rivendell (4.9),
Harad (5.31) and Rohan (5.01) starters sat near the top of the whole game, career bows ran 83 to 99 damage
against vanilla's hunting bow at 40, and the elven starter shield carried 600 HP against 220 for a
battered kite. Both problems have the same fix: hand the player a twin with its own numbers.

A week later (#629) an audit found the twins had two faults of their own in the career layer. A twin keeps
its donor's meshes, so 33 of 40 careers still rendered vanilla gear through twins of vanilla donors (the
battered kite shield, bodkin arrows, the Footman's Spatha, the hunting and recurve bows, the raider spear).
And once the #617 ranged ladder lowered low-tier troop bows to 33 to 43 damage, the 40 to 45 starter bows,
the 9-point starter legs and the 220 HP starter shields sat above some cultures' regular troop gear. The
career kits now take the culture's lowest troop gear directly, with no twins.

## The engine facts the design rests on (1.4.8 dump)

- **Crafted damage is proportional to the blade's factor.** `Crafting.cs:134-135` reads the swing and thrust
  factors from the Blade piece alone; `363-381` multiply a magnitude computed from speed, reach, weight,
  inertia and centre of mass by that factor. A clone that keeps the donor's pieces, weight and length
  therefore does exactly donor x (new factor / old factor). One new blade piece is the whole nerf; the
  guard, handle and pommel are reused (pieces resolve from the global registry, `ItemObject.cs:425-470`).
- **Factors compare within a class only.** A long two-handed polearm at 1.4 still out-hits a one-handed
  sword at 2.5; the anchors sit at or below each class's p5, and the generator prints each clone's
  percentile within its class so the claim is checkable.
- **A hidden piece still needs registering.** `CraftingPiece.cs:201` reads `is_hidden` into
  `IsHiddenOnDesigner`, consumed only by the smithing UI (`WeaponDesignVM.cs:1483, 1634`) and the unlock
  behaviour (`CraftingCampaignBehavior.cs:668`). A crafted item resolves its usages by matching every
  fitted piece against `WeaponDescription.AvailablePieces` and the template's `UsablePieces`
  (`Crafting.cs:566-608`), so the starter blade must appear in every stylesheet block the donor blade
  appears in, or the item silently loses that usage (a spear missing from `OneHandedPolearm` resolves
  `requires_no_shield`, the CLAUDE.md trap). `Crafting.cs:682` uses `First(p => !IsHiddenOnDesigner)`, so a
  hidden piece must never be the only piece registered for a slot.
- **Weapon tier is the mean of the fitted pieces' tiers** (`Crafting.cs:406-421`, `WeaponTiers` Tier1 to
  Tier4), so a tier-1 blade on tier-5 elven fittings is still a Tier4 weapon.
- **Price.** `ItemObject.Deserialize` uses an XML `value=` verbatim when present and otherwise
  `DefaultItemValueModel.CalculateValue` = 100 (120 for body, hand and leg armour) x 2.75^Tierf, scaled by
  an appearance term (`(1 + 0.2 x (appearance - 1))` plus a flat bonus above 1).
  For a crafted weapon Tierf = 0.6 x a stats tier + 0.4 x a crafted tier, and the crafted tier comes from the
  fitted pieces' tiers and their iron grades (`DefaultItemValueModel.cs:41-49, 171-215`), which a clone keeps.
  That is why crafted starter twins carry an explicit `value="150"`: a floored blade on tier-5 fittings would
  otherwise still price in the thousands. Plain items (bows, arrows, shields, armour) tier from their
  floored numbers and compute a trivial price with no `value=`.
- **`is_merchandise="false"`** sets `ItemObject.NotMerchandise`, which keeps an item out of battle and
  hideout loot, workshop output, tournament prizes and caravan stock; no trade path reads it, so the
  player can still sell a starter item.
- **Same-id rosters across modules merge, they do not replace.** `MBObjectManager.MergeElements`
  (`799-875`) merges a later module's same-id element into the earlier one, keyed by the schema's unique
  attributes; `EquipmentRosters.xsd` keys only `EquipmentRoster/@id`. `_replaceWhileMerging="true"` on the
  roster element makes the engine drop every vanilla attribute and child first (`804-808`, `829-832`);
  on a keyless child such as `EquipmentSet` every set resolves to the last one, so the replace lands on the
  wrong set. The attribute is legal anywhere because the engine injects it into every schema complex type
  (`1092`).

## How the kit is structured

Two stacked rosters build the player's kit at CC finalize (`Main/Features/CharacterCreation/`,
`CharacterCreationContentService.GrantPlayerStartupResources`):

1. **Culture-default**: `player_char_creation_{culture}_{title}_{m|f}` in
   `Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml`. 256 rosters (16 cultures x 8
   titles x 2), each with a battle set and a civilian set. This is the final kit for the careerless cultures
   (lothlorien, umbar, lindon, goblin, mistymountainorcs, bluecraig, shaghana, abanissa) and the civilian kit
   for everyone, because the career layer carries no civilian set and `PlayerEquipmentAdapter` applies the
   two independently. The same file also holds the childhood, education and show-stage rosters and the
   parents' rosters, which share the id prefix and are not the player's kit.
2. **Career override**: `player_career_{culture}_{ranged|cavalry|infantry}_{m|f}` in
   `taom_career_starting_equipment.xml`, 78 rosters, applied via `Equipment.FillFrom`: a complete 12-slot
   replacement of the battle set (`Equipment.cs:184-194` copies every slot), so Head, Cape and Gloves are
   empty for a careered player by design. Real troop items, not twins (#629); see "Career kits" below.
3. **Vanilla override**: `taom_player_start_vanilla_override.xml`, 70 rosters with the same ids as vanilla
   SandBox's for vlandia (Rohan), empire (Dunland), sturgia (Dale), aserai (Harad), khuzait (Rhun) and
   battania (Khand), each roster carrying `_replaceWhileMerging="true"`. Before it those six started in
   Calradian gear. Each is the mapped culture's career kit by title, copied as the career file names it
   (hunter, skirmisher and bard take the ranged kit; guard and infantry the infantry kit; retainer the
   cavalry kit with its mount), plus a civilian set of the kit's one-handed weapon, body and legs. Khand
   borrows Rhun's kit until it has one (#571).

### Career kits: the lowest troop's gear (#629)

`tools/generate_career_kits.py` derives each weapon and armour slot of a career roster from the troops
the culture fields, and its `--verify` pins the file to the rule:

1. candidates are the items of the slot's class (bow, arrows, one-handed sword or axe, polearm, shield,
   body, leg) that a non-hero troop of the culture carries in a battle set (`troops/troops_*.xml`: inline
   `EquipmentRoster`s that are not civilian, plus `<Equipments>/<equipment>` overrides, which the engine
   applies to every set; the standalone sets the troop files reference are all civilian templates);
2. a bow first keeps only the candidates with the lowest `difficulty`, the Bow skill the engine's
   `CharacterHelper.CanUseItem` demands to re-equip it (a ladder bow can ask for 100);
3. the lowest-level non-vanilla candidate first carried at level 21 or below wins, otherwise the lowest
   troop's own item even when vanilla. Tie-break: most carried, then id.

Class exceptions, in the tool's `SIDEARM`/`SECOND` tables: Erebor and Dunland carry a one-handed axe as
the sidearm (no non-vanilla Dunland sword exists), and Dol Guldur infantry a two-handed axe. The level cap
keeps a Rhun tier-7 bow (first carried at L36) out of a starting kit. The full per-culture table is in issue
#629. Vanilla left by rule 3: arrows for every career culture but Isengard, Erebor, Rivendell and Mirkwood;
the Rohan, Dunland and Rhun bows, and Gundabad's (its only bow a new character can re-equip); Harad's sword,
lance and shield (Harad troops carry no culture weapons). The consequence is deliberate: these kits equal
the lowest regular gear, which is stronger than the old twins (Gondor body 23 against 5 to 9) and sells at
its real price, and a culture's three archetypes share their body and leg armour. Bows still ask for their
troop requirement where the culture fields nothing lower (Harad and Rivendell 100): the player starts with
the bow equipped and can use it, but cannot put it back on once removed until Bow reaches the number.

### The starter items

Named `starter_<donor_id>` (a trailing `_starter` on the donor is stripped first, so
`gondor_steel_bow_starter` becomes `starter_gondor_steel_bow`), one per donor, 127 on disk today: 39
crafted weapons, 38 plain weapons (bows, arrows, shields), 50 armour. 18 of them only the pre-#629 career
kits named; the generator keeps those in its `RETIRED_DONORS` list (see Tools) because saves started since
#569 hold them. They live in
`LOTRLOME_items/<folder>/starter_kit.xml` in the live `LOTRLOME_Armory` module and the `lotraom-assets`
mirror, keyed by the donor's `culture=` (vanilla cultures map to their TAOM folder, an item with no
culture falls back to the folder its own file lives in, then to `mercenary`; every target must be a folder
the Armory's `SubModule.xml` registers, or the file never loads). The 39 starter blades sit in a
`<!-- TAOM-STARTER-KIT:START/END -->` block of `LOTRLOME_crafting_pieces.xml`, and each is registered in a
marker block of every `weapon_descriptions.xslt` and `crafting_templates.xslt` template the donor blade is
in (15 description blocks, 8 template blocks); two vanilla-only javelin descriptions the Armory never
overrode got new template blocks, passthrough included, so vanilla's rows survive.

| Class (key) | Floor, min() with the donor |
|---|---|
| OneHandedSword | swing 2.5, thrust 1.7 |
| TwoHandedSword | swing 2.8, thrust 2.0 |
| OneHandedAxe, TwoHandedAxe | swing 2.3 |
| Mace 1.9, TwoHandedMace 2.0 | swing |
| TwoHandedPolearm, Pike | thrust 1.4; swing 1.4 only where the donor has a Swing entry |
| Javelin | thrust 1.7 |
| Dagger | swing 2.0, thrust 1.9 |
| Bow | `thrust_damage` 45; Crossbow 55 |
| Arrow, Bolt | `thrust_damage` 1 |
| LargeShield, SmallShield | `hit_points` 220, `body_armor` 1 |
| BodyArmor | body 9, arm 4 (a chest's leg and head numbers are dropped) |
| LegArmor 9, HeadArmor 9, HandArmor 6, Cape body 6 arm 2 | |

The older career-layer armour keeps its own scheme: `starter_{archetype}_{culture}_{body|leg}_a` in each
folder's `starter_armors.xml`, anchors Ranged 5, Cavalry 7, Infantry 9, from `generate_starter_armor.py`
(Gondor hand-tuned). Every `covers_*` and cover-type attribute is copied verbatim on every clone; without
`covers_legs` the mesh equips invisibly.

## Tools

| Tool | Purpose |
|---|---|
| `tools/generate_career_kits.py` | Derive the 78 career kits from the troops (#629, rule above): dry run by default, `--apply` rewrites only the five slot ids per roster (byte-faithful), `--verify` exits 1 when the career file drifts from the rule, for instance after a troop rebalance moves a culture's floor. Needs the install; stops rather than guesses without it. Follow an `--apply` with the wiring tool. |
| `tools/generate_starter_kit.py` | Author the twins. Reads the culture-default roster file (not the career file since #629, whose troop items it would otherwise twin) and plans every donor in `RETIRED_DONORS` as well, a committed list of the 18 twins only the old career kits named, so a reinstall that wiped them restores them and a retired donor that stops resolving fails the run. A twin already on disk keeps its folder when its donor's culture later changes (the run prints a note). Indexes the Armory plus vanilla item and piece files, floors per class, writes the per-folder item files and the three marker blocks in the live Armory and the mirror. Dry run by default; `--apply`; `--verify` (exit 1 on any missing item, piece or registration: the reversion gate for the unversioned install); `--revert`; `--crafted-value` (0 lets the engine price crafted twins). Prints per clone the ratio, the class percentile and the resulting weapon tier. Aborts on a donor with no definition, a class with no anchor, a blade registered nowhere, a folder the Armory does not register. |
| `tools/wire_starter_kit_rosters.py` | Point the culture-default rosters at the twins: in-place `id=` substitution over the 256 rosters of `REWIRE_FILES` (both sets, Horse and HorseHarness untouched; the career file is excluded since #629), then write the vanilla override from the career kit exactly as the career file names it, refusing any override id vanilla's `sandbox_equipment_sets.xml` lacks (the engine would merge such a roster into the first vanilla roster instead of adding it). Shares the id rule and the roster filter with the generator. Re-run it after any career roster change. |
| `tools/generate_starter_armor.py` | The older career-layer armour pass (see above); its items stay on disk for old saves. Its wiring script, `wire_career_starter_armor.py`, was deleted in #629. |

Re-run the generator whenever a culture-default roster gains a new donor, then the wiring tool. `--verify` after any
Armory refresh: a reinstall wipes `starter_kit.xml` and the marker blocks, and `validate_moduledata.py` then
reports every player roster slot as `BROKEN_ITEM_REF`, which is the backstop.

## Gates

- `python tools/validate_moduledata.py`: every `starter_` reference resolves (0 errors).
- `python tools/audit_polearm_shield_parity.py`: a spear beside a shield resolves one-handed. Its
  `KNOWN_FAILURES` hold the eight Mordor player rosters under #526: four culture-default rosters on
  `starter_wm_mordor_set1_polearm_a01`, four career rosters on `wm_mordor_set1_polearm_a02` (the lowest
  Mordor troop polearm, which resolves two-handed the same way).
- `python tools/check_external_xslt.py`: both modified stylesheets compile.
- `python tools/generate_starter_kit.py --verify` on both Armory copies (the live install and
  `lotraom-assets\v1.5`). It checks ids only, not stats.
- `python tools/generate_career_kits.py --verify`: the career file is exactly what the rule derives.
- Python: `tools/tests/test_generate_starter_kit.py` (52), `test_wire_starter_kit_rosters.py` (16, including
  the committed override being byte-for-byte what the tool builds from the committed career file) and
  `test_generate_career_kits.py` (19, one of them the install-gated `--verify` pin).
- C#: `TAOM.Tests/Features/CharacterCreation/StarterKitCoverageTests.cs` pins that every weapon and armour
  slot in the culture-default file is a `starter_` item (no allowlist), that every career and override slot
  names an item a non-hero troop of its culture carries in a battle set (Khand through Rhun, no borrowing),
  that every career roster's `culture=` matches its id and its `_m` and `_f` kits are identical, that both
  culture-default sets are rewired, that the childhood, education, show and parent rosters are not, that
  every vanilla-mapped title has an override roster, and that every override roster carries the replace
  attribute.
  `PlayerStartCoverageTests` reads the override file too and no longer excludes the six.

## Verifying in-game (MANDATORY, validators cannot catch this)

`validate_moduledata.py` PASS, a green build and green tests do **not** prove the items load in-engine:
none of them start a campaign. Bannerlord registers each `LOTRLOME_items/<culture>` directory at **process
launch** and globs it for `*.xml` at **campaign start**, with no hot-reload (`Module.cs:246, 1032`;
`Campaign.cs:1471 LoadXML("Items")`; `MBObjectManager.cs:894-903`). A starter file authored after the game
launched is null in-engine and the character is **naked or unarmed**.

After any `--apply`:

1. **Fully restart Bannerlord** (close to desktop, relaunch).
2. Start four **new campaigns**: a career culture with a career picked (Gondor), a careerless culture
   (Lothlorien or Goblin, the culture-default layer is the final kit), Rohan (proves the override replaced
   vanilla's roster rather than appending to it), and one cavalry pick (mount plus starter body and legs).
3. In each, open the inventory. A careerless TAOM-culture start shows a `... (Starter)` item in every
   weapon and armour slot with the anchor numbers; a career start (and a Rohan careerless start, which
   reads the override) shows the career kit's troop items from issue #629. Meshes render (no bare legs or
   hands), the smithing designer offers no starter blade, and a town merchant stocks no starter item.

Keep backups on a non-`.xml` extension (`.bak-starterkit`): the glob is `*.xml`, so a `*.xml` backup left in
an item folder loads as a duplicate item id.

## Residuals / follow-ups

- About 127 new `{=starter_*}` item names use inline English defaults, plus the 180 older
  `{=starter_*}` armour names: none harvested into the 12-language pipeline (`/localize`).
- Horse and HorseHarness are untouched by #629; the career layer hands a vanilla `saddle_horse` to seven
  cultures' cavalry (six of them with `light_harness`), a warg to four, the war ram to Erebor (#515) and the
  great elk to Mirkwood (#636). The Armory has no riding horse of its own: every Armory `Horse` is a creature
  (wargs, rams, spiders, mumakil, elephant, chariot, elk).
- The careerless culture-default kits of Umbar, Khand, Shaghana and Abanissa still hand out vanilla weapons,
  Lothlorien and Lindon a vanilla sumpter horse and harness, and Goblin, Misty Mountains and Blue Craig
  vanilla arrows (out of #629's scope).
- Since the 1.5.3 bump vanilla `battered_kite_shield` carries `culture="Culture.empire"`, which maps to
  `dunland`, while its twin sits in `mercenary`. The generator keeps an on-disk twin where it is, so
  `--verify` is green and the run prints a note instead of moving it.
- Twin stats have drifted from their donors since #569 (ten `starter_kit.xml` files: armour numbers,
  `modifier_group`, the kite shield's new `culture=`; and the 39 starter blades in
  `LOTRLOME_crafting_pieces.xml` now render four `<Flag>` rows the on-disk block lacks). `--verify` checks
  ids, not content, so it stays green; the next `--apply` rewrites those eleven files. Nothing in the careers reads these twins any more; the
  culture-default kits do.
- shaghana and abanissa ship 16 culture-default rosters each but offer no youth-menu title, so the
  coverage test cannot reach them (#570). Khand has no kit of its own (#571).
- Vanilla also ships `mercenary` (and, for battania, `kern`) player rosters for the six overridden
  cultures; TAOM's youth menu never offers those titles, so they are unreachable and have no
  override. The coverage test derives its expectations from the youth menu, so adding one of those
  titles later without an override roster would put that start back in Calradian gear unnoticed;
  re-run the wiring tool whenever the youth menu changes.
- A run of the generator after the rosters are rewired maps every `starter_` id back to its donor
  and plans the same set; `--apply` refuses to drop a starter id already on disk unless it is kept by
  `RETIRED_DONORS` or `--allow-shrink` is given, so a run that cannot see a donor cannot empty a marker block.
- Starter weights and lengths are the donor's; only damage, hit points and armour numbers were lowered.
- Old saves keep the old strong items: every donor id still exists, only new ids were added, and rosters
  are read at character creation, not on load.

## Key files

| File | Role |
|---|---|
| `Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml` | Culture-default rosters (in repo) |
| `Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml` | Career rosters: the culture's lowest troop gear (in repo) |
| `Main/_Module/ModuleData/equipmentsets/taom_player_start_vanilla_override.xml` | The six vanilla-mapped cultures' rosters, generated from the career kit (in repo, registered in `SubModule.xml`) |
| `LOTRLOME_Armory/.../LOTRLOME_items/<folder>/starter_kit.xml` | Starter item defs, generated (live install and `E:\repos\lotraom-assets\v1.5\LOTRLOME_Armory`) |
| `LOTRLOME_Armory/.../LOTRLOME_crafting_pieces.xml`, `weapon_descriptions.xslt`, `crafting_templates.xslt` | Starter blades and their registrations, in marker blocks |
| `LOTRLOME_Armory/.../LOTRLOME_items/<culture>/starter_armors.xml` | The older career-layer starter armour |

## History

- 2026-05-19: `taom_career_starting_equipment.xml` shipped Gondor-only as proof of life with hand-tuned
  `starter_*_gondor_*` armour.
- 2026-06-30: the price problem. Item value found to be exponential in tier; starter armour cut to
  single-stat anchors and generated for the 12 other career cultures; the first ship showed every non-Gondor
  character naked because the new files post-dated the game launch (RCA in LESSONS-LEARNED, "A NEW item XML
  file only loads at process launch").
- 2026-07-12: 78 career rosters across 13 cultures; the "15 starter items per culture" claim corrected to 6.
- 2026-09-12 (#569): the strength problem. Every player-start item becomes a `starter_` twin, the six
  vanilla-mapped cultures get TAOM override rosters, and the invariant is pinned by
  `StarterKitCoverageTests`. On the way, the 238 references the 2026-09-01 Gondor sword rebuild broke were
  repointed from Erkam's assets-repo fix (#568).
- 2026-09-19 (#629): the vanilla audit. 33 of 40 careers rendered vanilla gear through twins of vanilla
  donors, and the twin floors sat above some cultures' regular troop gear after #617. The 78 career rosters
  now name the culture's lowest troop gear, the override follows, the wiring tool no longer rewires the
  career file, and the generator no longer reads it but retains its 18 old twins. The deep review the same
  day committed the rule as `tools/generate_career_kits.py` (bows by lowest skill requirement first, no
  arrow borrowing), made the retained twins an explicit list that keeps each twin in its folder, pointed the
  generator at the `v1.5` mirror, taught the wiring tool to refuse an override id vanilla lacks, and deleted
  the two scripts that could still write the old career kits.

## Related docs

- [career-cc-selection.md](career-cc-selection.md): the CC career-selection stage and the archetype-driven
  starting-equipment system this tunes.
- [career-system.md](career-system.md): the broader career feature (`CareerStartingEquipmentService`).
- [character-creation.md](character-creation.md): the CC pipeline, cultures, narrative menus, race.
- [armor-balance.md](armor-balance.md): the armour stat-balancing tools; orthogonal to value, same files.
- [../modding/items-weapons-and-crafting.md](../modding/items-weapons-and-crafting.md): the four files a
  crafted weapon spans and the two registration gates.
- [../modding/load-order-and-dependencies.md](../modding/load-order-and-dependencies.md): how a later
  module's XML merges into an earlier one, and where `_replaceWhileMerging` fits.
- [../reference/engine/item-equipment-model.md](../reference/engine/item-equipment-model.md): `ItemObject` /
  `ItemComponent` engine model (where `Value` / `Tierf` live).
- CLAUDE.md "Equipment & Armory": the canonical LOTRLOME folder per item-id prefix.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/career-system.md](./career-system.md)
- [docs/features/character-creation.md](./character-creation.md)
- [docs/features/lord-identity-reconciliation.md](./lord-identity-reconciliation.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/balance-levers.md](../modding/balance-levers.md)
- [docs/modding/editing-safely.md](../modding/editing-safely.md)
- [docs/modding/file-catalogue.md](../modding/file-catalogue.md)
- [docs/modding/items-armor.md](../modding/items-armor.md)
- [docs/modding/load-order-and-dependencies.md](../modding/load-order-and-dependencies.md)
- [docs/modding/party-templates.md](../modding/party-templates.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
