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
   `Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml`. 400 rosters (25 cultures x 8
   titles x 2), each with a battle set and a civilian set. Since 2026-10-23 every playable culture has career kits, so its battle
   sets show only in the previews before the career menu; it stays the civilian kit
   for everyone, because the career layer carries no civilian set and `PlayerEquipmentAdapter` applies the
   two independently. The same file also holds the childhood, education and show-stage rosters and the
   parents' rosters, which share the id prefix and are not the player's kit.
2. **Career override**: `player_career_{culture}_{ranged|cavalry|infantry|twohanded}_{m|f}` in
   `taom_career_starting_equipment.xml`, 180 rosters (28 cultures x 3 archetypes x 2, plus the six
   each of Ered Luin and Angaladh: infantry, ranged and two-handed, no cavalry career), applied via `Equipment.FillFrom`: a complete 12-slot
   replacement of the battle set (`Equipment.cs:184-194` copies every slot), so Head, Cape and Gloves are
   empty for a careered player by design. Real troop items, not twins (#629); see "Career kits" below.
3. **Vanilla override**: `taom_player_start_vanilla_override.xml`, 58 rosters with the same ids as vanilla
   SandBox's for vlandia (Rohan), empire (Dunland), sturgia (Dale), khuzait (Rhun) and
   battania (Khand), each roster carrying `_replaceWhileMerging="true"`. Before it those five started in
   Calradian gear. aserai (Harad) is no longer in this file: since commit 08b30bfc4 (2026-10-10) it has its own
   culture-default rosters in `taom_char_creation_equipment.xml`. Because vanilla SandBox defines the same
   roster ids, the wiring tool gives every culture-default roster whose id vanilla also defines
   `_replaceWhileMerging="true"` (37 today: the 16 aserai youth rosters plus the aserai parent, childhood,
   education and show rosters), and leaves out of the override any roster the culture file defines. Each is the mapped culture's career kit by title, copied as the career file names it
   (hunter, skirmisher and bard take the ranged kit; guard and infantry the infantry kit; retainer the
   cavalry kit with its mount), plus a civilian set of the kit's one-handed weapon, body and legs. Khand
   borrows Rhun's kit (#571); its own career kits are Rhun's troop gear too (`KIT_SOURCE`).

### Career kits: the lowest troop's gear (#629)

`tools/generate_career_kits.py` derives each weapon and armour slot of a career roster from the troops
the culture fields, and its `--verify` pins the file to the rule:

1. candidates are the items of the slot's class (bow or crossbow, arrows or bolts, javelin, one-handed
   sword or axe, polearm, shield, body, leg) that a non-hero troop of the culture carries in a battle set
   (`troops/troops_*.xml`: inline `EquipmentRoster`s that are not civilian, plus `<Equipments>/<equipment>`
   overrides, which the engine applies to every set; the standalone sets the troop files reference are all
   civilian templates). For Isengard's and Mordor's Body and Leg, only their uruk line (`ARMOUR_LINE`:
   `sk_uruk_hai_` and `sk_uruk_mordor_`); a culture with no piece on its line stops the tool with an error;
2. a bow or crossbow first keeps only the candidates with the lowest `difficulty`, the skill the engine's
   `CharacterHelper.CanUseItem` demands to re-equip it (a ladder bow can ask for 100);
3. the lowest-level non-vanilla candidate first carried at level 21 or below wins, otherwise the lowest
   troop's own item even when vanilla. Tie-break: most carried, then id.

The uruk line is a restriction, not a preference. Both cultures field level-1 orcs in Mordor orc armour,
which beat the uruks on level and dressed every Isengard and Mordor career as a Mordor orc until
2026-10-06, and orc armour does not fit an uruk player. Every race the two cultures offer (uruk, uruk_hai,
berserker, orc, human, saruman) uses the human skeleton and no equip path checks race, so the uruk pieces
equip on any of them; how they look on an orc or a human player is owed in game. Isengard's line starts at
the Uruk-hai recruit (L6), Mordor's at the uruk grunt (L11), so Mordor's career armour comes from
engine tier 2, two tiers above its level-1 orcs; the weapons still come from the lowest troops.

The `twohanded` layout (#1045) is a two-handed axe and a throwing axe (class `ThrowingAxe`, the `THROWN`
token), with no one-handed weapon, shield, polearm or third weapon: its roster has four slots (`Item0`,
`Item1`, `Body`, `Leg`) and no `Item2`. Ered Luin's mattock line carries `southern_throwing_axe_1_t4` from the Mattock Hewer (L16) up, so
the kit takes it by the same pick rule, from Ered Luin's own troops. Angaladh has a two-handed career
too, but no Angaladh or Dale troop carries a throwing weapon, so its second slot is the culture's
one-handed axe (`THROWN_FOR`).

Class exceptions, in the tool's `SIDEARM`/`SECOND`/`RANGED` tables: Erebor, Ered Luin, Dunland, Rhudaur and Anduin Vale
carry a one-handed axe as the sidearm (no non-vanilla Dunland sword exists; Rhudaur's troops carry none), Dol Guldur infantry a two-handed axe, and five
ranged careers carry their own weapon: Isengard's Uruk Crossbow, Erebor's Crossbow Master and Ered Luin's
Firebeard Arbalester a crossbow and bolts, Harad's Pezarsani Javelineer (and Abanissa's Jiret Javelineer, through `KIT_SOURCE`) and
Shaghana's Chatak Javelineer javelins and a shield (every Shaghana bow needs skill 100). Umbar's Corsair
Crossbow starts with a bow: Umbar's only crossbow is first carried at L26, above the cap. Erebor fields no crossbow below L21, so its
start is `ladder_erebor_xbow_t4`. Ered Luin's first crossbow is the same one, carried by its L21 Firebeard
Arbalester, so its ranged kit is `ladder_erebor_xbow_t4`, `sm_dwarf_iron_bolt_a` and the axe
`sm_dwarf_erebor_1h_axe_c`. Ered Luin offers no cavalry career (dwarves do not ride, and it fields no war
ram), so it has no cavalry kit. Its Warden of Thorin's Halls takes the infantry kit
(`sm_dwarf_erebor_1h_axe_c`, `sm_dwarf_erebor_shield_leather_a`, `sm_dwarf_erebor_spear_a`), and its
Mattock Hewer of Belegost takes the two-handed kit above. The level cap keeps a Rhun tier-7 bow (first carried at L36) out of a
starting kit. The full per-culture table is in issue #629, amended by #743. Vanilla left by rule 3: arrows
for every bow career but the elven ones (Rivendell, Mirkwood, Lothlorien, Lindon); the Rohan, Dunland and Rhun bows, and Gundabad's (its only
bow a new character can re-equip); Harad's sword, lance, javelin and shield (Harad troops carry no culture
weapons). The consequence is deliberate: these kits equal the lowest regular gear, which is stronger than
the old twins (Gondor body 23 against 5 to 9) and sells at its real price, and a culture's three archetypes
share their body and leg armour. Bows still ask for their troop requirement where the culture fields
nothing lower (Rivendell 100): the player starts with the bow equipped and can use it, but cannot put it
back on once removed until Bow reaches the number.

### The starter items

Named `starter_<donor_id>` (a trailing `_starter` on the donor is stripped first, so
`gondor_steel_bow_starter` becomes `starter_gondor_steel_bow`), one per donor, 128 on disk today: 40
crafted weapons, 38 plain weapons (bows, arrows, shields), 50 armour. 18 of them only the pre-#629 career
kits named; the generator keeps those in its `RETIRED_DONORS` list (see Tools) because saves started since
#569 hold them. They live in
`LOTRLOME_items/<folder>/starter_kit.xml` in the live `LOTRLOME_Armory` module (the `lotraom-assets`
mirror only with `--asset-repo`), keyed by the donor's `culture=` (vanilla cultures map to their TAOM folder, an item with no
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
| `tools/generate_career_kits.py` | Derive the 180 career kits from the troops (#629, rule above; `KIT_SOURCE` points Khand, Lothlorien, Abanissa, the Misty Mountain orcs and Blue Craig at the troops they recruit, which carry another culture's id): dry run by default, `--apply` rewrites only the kit's slot ids per roster (byte-faithful), `--verify` exits 1 when the career file drifts from the rule, for instance after a troop rebalance moves a culture's floor. Needs the install; stops rather than guesses without it. Follow an `--apply` with the wiring tool. |
| `tools/generate_starter_kit.py` | Author the twins. Reads the culture-default roster file (not the career file since #629, whose troop items it would otherwise twin) and plans every donor in `RETIRED_DONORS` as well, a committed list of the 18 twins only the old career kits named, so a reinstall that wiped them restores them and a retired donor that stops resolving fails the run. A twin already on disk keeps its folder when its donor's culture later changes (the run prints a note). Indexes the Armory plus vanilla item and piece files, floors per class, writes the per-folder item files and the three marker blocks in the live Armory (the mirror only with `--asset-repo`). Dry run by default; `--apply`; `--verify` (exit 1 on any missing item, piece or registration: the reversion gate for the unversioned install); `--revert`; `--crafted-value` (0 lets the engine price crafted twins). Prints per clone the ratio, the class percentile and the resulting weapon tier. Aborts on a donor with no definition, a class with no anchor, a blade registered nowhere, a folder the Armory does not register. It also refuses to rewrite or revert a marker block holding another author's rows in any of the three files (the two stylesheets and the pieces file), computing and parsing every new text before any write, and `--verify` exits 2 when a gate checked nothing (a missing Armory). |
| `tools/wire_starter_kit_rosters.py` | Point the culture-default rosters at the twins: in-place `id=` substitution over the 400 rosters of `REWIRE_FILES` (both sets, Horse and HorseHarness untouched; the career file is excluded since #629), then write the vanilla override from the career kit exactly as the career file names it, refusing any override id that both vanilla SandBox and SandBoxCore lack (the engine would merge such a roster into the first vanilla roster instead of adding it). Shares the id rule and the roster filter with the generator. It also gives `_replaceWhileMerging="true"` to every roster of the culture-default file whose id vanilla SandBox or SandBoxCore also defines (37 today, all aserai: the 16 youth rosters plus the parent, childhood, education and show rosters), refuses that flag on any roster whose id vanilla lacks (the engine would merge it into the first roster of the document), and leaves out of the vanilla override every roster the culture-default file defines; without the flag the engine appends the roster's sets after vanilla's and the player keeps vanilla's first set. Re-run it after any career roster change. |
| `tools/generate_starter_armor.py` | The older career-layer armour pass (see above); its items stay on disk for old saves. Its wiring script, `wire_career_starter_armor.py`, was deleted in #629. |

Re-run the generator whenever a culture-default roster gains a new donor, then the wiring tool. `--verify` after any
Armory refresh: a reinstall wipes `starter_kit.xml` and the marker blocks, and `validate_moduledata.py` then
reports every player roster slot as `BROKEN_ITEM_REF`, which is the backstop.

## Gates

- `python tools/validate_moduledata.py`: every `starter_` reference resolves (0 errors).
- `python tools/audit_polearm_shield_parity.py`: a spear beside a shield resolves one-handed. Its
  `KNOWN_FAILURES` hold the four Mordor culture-default player rosters under #526, on
  `starter_wm_mordor_set1_polearm_a01`. The four Mordor career rosters left the list on 2026-10-06 (#743):
  their polearm is `sm_md_num_lance_a`, which resolves one-handed.
- `python tools/check_external_xslt.py`: both modified stylesheets compile.
- `python tools/generate_starter_kit.py --verify` on the live Armory. It checks ids only, not stats. The
  lotraom-assets mirror is checked only with `--asset-repo`; it stayed red (5 drift lines on 2026-10-14)
  until Mike syncs it, and the live Armory is the source of truth.
- `python tools/generate_career_kits.py --verify`: the career file is exactly what the rule derives.
- Python: `tools/tests/test_generate_starter_kit.py`, `test_wire_starter_kit_rosters.py` (including
  the committed override being byte-for-byte what the tool builds from the committed career file, and the
  flag rules above) and `test_generate_career_kits.py` (one test is the install-gated `--verify` pin).
- C#: `TAOM.Tests/Features/CharacterCreation/StarterKitCoverageTests.cs` pins that every weapon and armour
  slot in the culture-default file is a `starter_` item (no allowlist), that every career and override slot
  names an item a non-hero troop of its culture carries in a battle set (Khand through Rhun, no borrowing),
  that every career roster's `culture=` matches its id and its `_m` and `_f` kits are identical, that both
  culture-default sets are rewired, that the childhood, education, show and parent rosters are not, that
  every vanilla-mapped title has an override roster, and that every override roster carries the replace
  attribute. It counts each of the six vanilla cultures as covered either by the override or, for a
  culture with its own culture-default rosters (aserai), only when each of its youth rosters carries the flag.
  `PlayerStartCoverageTests` reads the override file too and no longer excludes the six.

## Verifying in-game (MANDATORY, validators cannot catch this)

`validate_moduledata.py` PASS, a green build and green tests do **not** prove the items load in-engine:
none of them start a campaign. Bannerlord registers each `LOTRLOME_items/<culture>` directory at **process
launch** and globs it for `*.xml` at **campaign start**, with no hot-reload (`Module.cs:246, 1032`;
`Campaign.cs:1471 LoadXML("Items")`; `MBObjectManager.cs:894-903`). A starter file authored after the game
launched is null in-engine and the character is **naked or unarmed**.

After any `--apply`:

1. **Fully restart Bannerlord** (close to desktop, relaunch).
2. Start four **new campaigns**: a career culture with a career picked (Gondor), a culture whose
   kit comes from another culture's troops (Lothlorien or Goblin), Rohan (proves the override replaced
   vanilla's roster rather than appending to it), and one cavalry pick (mount plus starter body and legs).
3. In each, open the inventory. A careerless TAOM-culture start shows a `... (Starter)` item in every
   weapon and armour slot with the anchor numbers; a career start (and a Rohan careerless start, which
   reads the override) shows the career kit's troop items from issue #629. Meshes render (no bare legs or
   hands), the smithing designer offers no starter blade, and a town merchant stocks no starter item.

Owed since #795 (2026-10-14), run `./build.ps1` first because the installed `Modules\TAOM` still holds
the old roster files, then restart fully and use new campaigns:

- A Harad start without a career and with one: check both the battle kit and the civilian outfit.
- The Harad parent, childhood and education screens.
- Abanissa and Shaghana starts, if the youth menu reaches them (#570).

Keep backups on a non-`.xml` extension (`.bak-starterkit`): the glob is `*.xml`, so a `*.xml` backup left in
an item folder loads as a duplicate item id.

## Residuals / follow-ups

- About 128 new `{=starter_*}` item names use inline English defaults, plus the 180 older
  `{=starter_*}` armour names: none harvested into the 12-language pipeline (`/localize`).
- Horse and HorseHarness are untouched by #629; the career layer hands a vanilla `saddle_horse` to 19
  cultures' cavalry (the three Arnor realms, Anduin Vale, Dorwinion and Gondor with `starter_cavalry_gondor_horse_armor_a`,
  Harad, Abanissa and Shaghana with `aseran_village_harness`, Rhun and Khand with `steppe_harness`,
  the other eight, Dunland and South Rhovanion among them, with `light_harness`), a warg to seven, the war ram to Erebor (#515) and the
  Animalia elk to Mirkwood (#646; the great elk, #636, until 2026-09-23, when Mike put the career start on the
  lower cavalry's mount). Ered Luin and Angaladh have no cavalry kit and so no mount. The Armory has no riding horse of its own: every Armory `Horse` is a creature
  (wargs, rams, spiders, mumakil, elephant, chariot, the great elk, the Animalia elk and moose).
- Lothlorien's, Lindon's and Rivendell's archers start with an elven ladder bow that needs Bow 100,
  so a new character cannot put it back on once it is taken off. Elf troops carry no javelin, so the
  rule has no other pick; left as it is for now (Mike, 2026-10-23).
- **Why the saddle horse and not a culture horse** (mount analysis, 2026-10-23). Every vanilla culture
  horse is stronger (speed 44 to 54 against 37, maneuver 55 to 67 against 52, charge 14 to 22 against
  12), and each culture's lowest rider already sits on a tier-2 horse or a war horse. It needs
  Riding 10 to 20 (`aserai_horse` and `empire_horse` 10, `khuzait_horse` and `vlandia_horse` 15,
  `battania_horse` and `sturgia_horse` 20), and careers grant no skill (`career_menu.json`), so a fresh
  character may be below it and could not put the horse back on: the elven bow trap again. The saddle
  horse needs Riding 0 and already beats vanilla's own start, `sumpter_horse` (speed 34, maneuver 41,
  charge 8). Harnesses need no skill, so each culture takes the harness vanilla's character creation
  gives its retainer (Dunland is vanilla `empire`: the light harness; Khand follows Rhun's kit). The goblins, Misty Mountains and Blue Craig ride the brown warg, which their markets stock.
- Since the 1.5.3 bump vanilla `battered_kite_shield` carries `culture="Culture.empire"`, which maps to
  `dunland`, while its twin sits in `mercenary`. The generator keeps an on-disk twin where it is, so
  `--verify` is green and the run prints a note instead of moving it.
- Twin stats have drifted from their donors since #569 (ten `starter_kit.xml` files: armour numbers,
  `modifier_group`, the kite shield's new `culture=`; and the 39 starter blades in
  `LOTRLOME_crafting_pieces.xml` now render four `<Flag>` rows the on-disk block lacks). `--verify` checks
  ids, not content, so it stays green; the next `--apply` rewrites those eleven files. Nothing in the careers reads these twins any more; the
  culture-default kits do.
- shaghana and abanissa ship 16 culture-default rosters each but offer no youth-menu title, so the
  coverage test cannot reach them (#570). Khand has no gear of its own (#571).
- Vanilla also ships `mercenary` (and, for battania, `kern`) player rosters for the five overridden
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
| `Main/_Module/ModuleData/equipmentsets/taom_player_start_vanilla_override.xml` | The five vanilla-mapped cultures' rosters, generated from the career kit (in repo, registered in `SubModule.xml`) |
| `LOTRLOME_Armory/.../LOTRLOME_items/<folder>/starter_kit.xml` | Starter item defs, generated (live install; the `lotraom-assets` mirror only with `--asset-repo`) |
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
- 2026-10-06: Isengard and Mordor careers started in Mordor orc armour (`sk_md_orc_arc_chest_light_a`,
  `sk_md_orc_boots_a`), carried by each culture's level-1 orcs (#743). Their Body and Leg now come only
  from the uruk line: Isengard wears `sk_uruk_hai_chainmail_a1` and `sk_uruk_hai_shoes_a1`, Mordor
  `sk_uruk_mordor_chainmail_medium_a1` and `sk_uruk_mordor_boots_a`. Three ranged careers got their own
  weapon: the Uruk Crossbow `ladder_isengard_xbow_t2` and `wm_isengard_bolt_a01`, the Crossbow Master
  `ladder_erebor_xbow_t4` and `sm_dwarf_iron_bolt_a`, the Pezarsani Javelineer `eastern_javelin_3_t4` and
  `desert_round_shield` (with Harad's hunter, skirmisher and bard override starts). The Gondor careers
  went back to the rule's picks, `wm_gondor_sword_a01` and `wm_gondor_light_spear`, also the weakest
  Gondor troop weapons (simulated sustained damage 34.8 against `_a02` 35.4, and 28.5 against
  `wm_gondor_spear_b` 32.1): `998f054c` (#669) re-kitted Gondor's low troops on 2026-09-25 without
  regenerating, so the file had drifted from v2.0.31 to v2.0.34 with the install-gated pin unrun.
- 2026-10-14 (#795): the starter-kit fix for the aserai (Harad) start. Five twins were generated in the live
  Armory (`rhun/` and `harad/` `starter_kit.xml`), 320 roster slots in 48 aserai, abanissa and shaghana
  rosters were repointed to twins, the 37 aserai rosters whose ids vanilla also defines got
  `_replaceWhileMerging="true"` (without it the engine appended their sets after vanilla's), and the
  vanilla override shrank from 70 to 58 rosters because Harad uses its own culture-default rosters since
  Erkam's `08b30bfc4`. `StarterKitCoverageTests` now accepts that route. In-game checks are owed (see
  above). Committed in `776054c0c`; the tool's single roster-id reader and its comment skip followed in
  `2952ccc54` ([RCA](../reviews/rca-orc-goblin-locomotion-2026-10-14.md)).
- 2026-10-15: the aserai career kits wear `sk_hr_st_tunic_a` as Body, the generator rule (the lowest Harad troop, `harad_reg_levy` at level 6, wears it). The cavalry and infantry careers' lance became `khuzait_lance_1_t3`, the lance the level-11 Harad militia spearmen carry, once `harad_noble_merc` (the only low troop with `aserai_lance_1_t5`) was deleted ([RCA](../reviews/rca-harad-repair-2026-10-15.md)).
- 2026-10-15, later (#805): KEYforce's Harad regular weapons put non-vanilla gear on Harad troops at or below the rule's level cap (21; the shield first at level 11, the spear at 16, the sword at 21), and the rule prefers non-vanilla items there, so all six aserai careers now carry his `sm_hr_st_shield_cav_light_b` and `hr_st_sword_a`, and the cavalry and infantry careers his `hr_st_spear_light_a` in place of the militia lance (16 slots).
- 2026-10-27 ([ered-luin.md](ered-luin.md)): Ered Luin's 55 rosters in `taom_char_creation_equipment.xml`
  (16 of them the culture-default player starts), generated with
  `generate_char_creation_equipment.py --append eredluin` and `no_mount=True`, so none carries a Horse or
  HorseHarness slot, and its four career kits (ranged and infantry, no cavalry). `generate_career_kits.py`
  gained `eredluin` rows in `SIDEARM` and `RANGED`; `CareerKitCoverageTests` now asks each culture for a
  kit per archetype among the careers it offers, not all six.

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

- [docs/ai-includes/orientation.md](../ai-includes/orientation.md)
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
