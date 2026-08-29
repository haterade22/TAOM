---
paths:
  - "Main/_Module/ModuleData/troops/**"
  - "Main/_Module/ModuleData/taom_partyTemplates.xml"
  - "Main/Features/TroopProgression/**"
---

# Troop Management Rules

## When Adding or Restructuring Troops

Update ALL of the following (checklist):

| Step | File(s) | What to do |
|------|---------|------------|
| 1. Define troops | `Main/_Module/ModuleData/troops/troops_{culture}.xml` | Add NPCCharacter with skills, equipment, upgrade_targets, race, culture |
| 2. Party templates | `Main/_Module/ModuleData/taom_partyTemplates.xml` | Add to ALL relevant templates for the culture (hero, patrol L1/L2/L3, outlaw, rebels, mercenary, vassal_reward, militia, villager, caravan, elite caravan). Twelve, see the table below |
| 3. Culture config | `Main/_Module/ModuleData/taom_spcultures.xml` **and** `spcultures.xslt` | Update `basic_troop` / `elite_basic_troop` if the entry point changed, and confirm every party template is BOUND. A retagged vanilla culture lives in the XSLT, not the XML, and inherits Calradia for anything its block does not name |
| 4. Recruitment code | `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` | Add/update settlement, clan, and culture fallback pools |
| 5. Recruitment tests | `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs` | TDD: write tests FIRST, then implement |
| 6. NPC references | `Main/_Module/ModuleData/characters/npcs_{culture}.xml` | Check villager upgrade_targets, caravan guard references |
| 7. CHANGELOG | `CHANGELOG.md` | Document the changes |

## Troop ID Naming Convention

`{culture_prefix}_{origin}_{role}` — Examples:
- `dg_goblin_slave` — Dol Guldur, goblin race, slave role
- `dg_khamul_shadow_initiate` — Dol Guldur, Khamul's line, shadow initiate
- `gondor_ano_peasant` — Gondor, Anórien origin, peasant role

## Race Attributes by Culture

| Culture | Race Lines | Race Attribute |
|---------|-----------|---------------|
| Dol Guldur | Goblin | `race="goblin"` |
| Dol Guldur | Orc | `race="orc"` |
| Dol Guldur | Uruk | `race="dg_uruk"` |
| Dol Guldur | Khamul (human) | no `race` attribute |
| Gondor | Human | no `race` attribute |
| Gundabad | Goblin/Orc | `race="goblin"` / `race="orc"` |

## Three cultures, one goblin tree

`goblin` (Goblin-town), `bluecraig` (Ered Luin) and `mistymountainorcs` all field
`troops/troops_goblin.xml`. There is no `troops_bluecraig.xml` or `troops_mistymountainorcs.xml`:
both were clones of the goblin tree that never diverged in any way a player could see, so they were
retired. Blue Craig and the Orc-host each keep exactly ONE bespoke troop, a T7 capstone, and both
live in `troops_goblin.xml` carrying their own `culture=` attribute.

Two consequences worth knowing before you edit any of the three:

- **Editing `troops_goblin.xml` changes three kingdoms.** A stat or gear tweak meant for Goblin-town
  lands on Blue Craig and the Misty Mountains as well.
- **The capstones are not upgrade-reachable and that is deliberate.** The only troop that could
  promote into them is the shared `goblin_chosen_of_tharzog`, and putting them there would let a
  Goblin-town player promote into another kingdom's signature unit. They reach the player through
  the vassal reward and through prisoner recruitment, the same route the Black Numenorean line uses.
  `VolunteerRecruitmentServiceTests.BorrowedCultureCapstones` is the exemption that records this.

Cross-culture sharing is the normal TAOM pattern, not a special case: Lothlorien fields Rivendell's
tree whole, Umbar fields Harad's but keeps `umbar_elite`. `CulturePartyTemplateTests` allows it as
long as the target is TAOM-authored.

## Party Template Types

Each culture typically has these templates in `taom_partyTemplates.xml`:

| Template | Purpose | Typical Composition |
|----------|---------|-------------------|
| `kingdom_hero_party_{culture}_template` | Lord armies (culture default) | Full range T1-T9 |
| `kingdom_hero_party_{culture}_{clan}_template` | Per-clan lord armies (what most named lords field) | Same range as the culture default |
| `kingdom_hero_party_mercenary_{culture}_template` | Mercenary bands | Mid-tier professional |
| `kingdom_hero_party_outlaw_{culture}_template` | Outlaw parties | Low-tier rabble |
| `patrol_party_{culture}_template_level_1` | Weak patrols | Low-mid tier |
| `patrol_party_{culture}_template_level_2` | Medium patrols | Mid tier |
| `patrol_party_{culture}_template_level_3` | Elite patrols | High tier |
| `rebels_{culture}_template` | Rebel uprisings | Low tier masses |
| `vassal_reward_troops_{culture}` | Vassal rewards | Elite troops |
| `militia_{culture}_template` | Town garrison | Militia troops |
| `villager_{culture}_template` | Village trade parties | 15-30 of the `villager_{culture}` NPC |
| `caravan_template_{culture}` | Caravans | 1 armed trader + 5-10 guards + 1-5 veterans |
| `elite_caravan_template_{culture}` | Elite caravans | 1 armed trader + 10-20 guards + 5-10 veterans |

Twelve per-culture types, not nine. The last three were missing from this table until 2026-08-12, and
Dale had shipped without any of them, so its villagers and caravans were vanilla Sturgians. Authoring
the three was the only new content the whole party-template fix needed.

The clan row is per CLAN, not per culture, and that is where 176 of the 193 lord-party templates live
(the other 17 are the culture defaults, which `Clan.DefaultPartyTemplate` falls back to only when the
clan binds nothing). The binding is `default_party_template` in `characters/clans.xml` or
`spclans.xslt`, and those two files are what you grep to prove a clan template is reachable, NOT the
culture files. The id is a convention, not a derivation, so never compute one. Most embed the clan id
minus its `clan_` prefix (`clan_erebor_1` binds `kingdom_hero_party_erebor_erebor_1_template`), but
Gondor's 14 embed a fief instead and the clan id appears nowhere in them (`clan_empire_west_9` binds
`kingdom_hero_party_gondor_blackroot_vale_template`). The leading token names the ROSTER's culture,
which is not always the clan's: the five `Culture.bluecraig` clans bind
`..._goblin_bluecraig_N_template`, and those stacks really are `goblin_*` troops. The five
`Culture.mistymountainorcs` clans are the counter-example that proves the token is a convention and
not a derivation: they keep `..._mistymountainorcs_mistymountainorcs_N_template` while their stacks
are `goblin_*` too, because renaming a bound template id breaks `clans.xml`. Computing instead of
grepping also hides dead data. Two of the 193 (`..._gondor_ithilien_template`,
`..._gondor_belfalas_template`) are bound by nothing as of 2026-08-14.

**`max_value` is not the party's size.** The engine draws ONE uniform ratio per party and fills every
stack to `min + (max - min) * r`, so a template's max sum is a spawn ceiling and the expected spawn
roster is the midpoint of its min and max sums; `PartySizeLimit` still governs recruitment afterwards.
Read [docs/reference/party-template-sizing.md](../../docs/reference/party-template-sizing.md) before
retuning these numbers, and use `tools/rebalance_party_template_maxes.py` rather than hand-editing.

**Writing a template is half the job. It is dead data until a culture binds it**, and a culture in
`spcultures.xslt` inherits Calradia for every attribute its block does not name. That has shipped four
times (Dale, Rohan, Khand, settlement patrols). The binding contract, both crash surfaces and the
`CulturePartyTemplateTests` gate: [culture-playability-wiring.md](../../docs/features/culture-playability-wiring.md).
Quick check that a template you just wrote is actually reachable: grep `taom_spcultures.xml` and
`spcultures.xslt` for its id, and if there are zero hits it is dead.

## Save Compatibility

- **Never change troop IDs** — rename display names only (keep `id` attribute)
- **Never delete troops** — orphan them (remove from upgrade_targets) but keep in file
- **is_basic_troop** — marks a troop as a standalone recruitment entry point
- **Tier shifts are allowed** — moving a troop from T6 → T5 is fine if you also re-pick its skill curve + armor + equipment to match the new tier. Engine re-applies on next load. (Dale `dale_royal_cavalier` T6→T5, `dale_kinsman_of_eorl` T7→T6 worked cleanly across an existing save.)
- **Display-name desync is OK** — `dale_master_crossbowman` can legitimately display "Royal Crossbowman" if a later rename swap put "Royal" at the higher tier. Document the desync in the feature doc.

## Volunteer Recruitment Lookup Priority (MANDATORY when editing `VolunteerRecruitmentService.cs`)

The pool resolution order is (highest priority first):

1. **`ConditionalSettlementMap[settlementId]`** — state-sensitive (e.g., Ithil Guard at `town_ES2` only when Gondor-owned).
2. **`SettlementMap[settlementId]`** — per-settlement override (e.g., Lake-Town `town_S1` = 9× Peasant + 1× Levy).
3. **`ClanMap[ownerClanId]`** — per-clan override (e.g., all 11 `clan_vlandia_*` recruit all 7 Rohan basic troops).
4. **`CultureMap[cultureId]`** — culture-level fallback.

When you add an entry to a higher-priority map, lower-priority entries are **shadowed** for that settlement/clan — not merged. If you want per-settlement to extend (not replace) the culture pool, you must copy the culture entries into the settlement entry explicitly.

## Per-Tier Explicit Armor Pattern (use when authoring a culture's tree)

Don't rely on the generic `_armor_suffix(tier, variant)` tier→suffix table for new cultures. Use explicit-suffix helpers that take a literal `a01`..`b04` string:

| Helper (in `tools/generate_dale_troops.py`) | Mesh class | Solus spelling quirk |
|---|---|---|
| `chivalry_armor_explicit(suffix)` | cavalry (chivlary + chivalry chest) | chest uses `chivalry`, other 4 slots use `chivlary` typo |
| `infantry_armor_explicit(suffix)` | royal infantry | `infrantry` typo throughout |
| `archer_armor_explicit(suffix)` | archer / crossbowman | shoulder fallback for missing `a02/b02` variants |
| `lake_town_armor_explicit(suffix, no_helmet=, no_shoulder=, no_bracers=)` | Lake-Town mariner | shoulder fallback `a02→a01, a04→a03, b02→b01, b04→b03` |

Color convention: `a` = bronze, `b` = silver. Light lines use `a`, heavy lines use `b` (or invert per user spec — Dale's cavalry inverts this).

## "Royal Goes Last" Naming Convention

Across all Dale lines, "Royal" is reserved for the highest-rank tier (typically T7). "Master" is the T6 stepping-stone. If you author a chain with both, "Royal" must be on the more elite troop. If only "Master" exists (no "Royal" sibling in the line), it's fine to leave "Master" at the top.

This is a TAOM-wide convention as of Dale (May 2026). Apply when authoring new culture trees.

## Cross-Reference Vanilla Weapon Stats Before Tier-Ordered Picks

Names don't imply tier. Codex Review #227 caught Dale's `lowland_yew_bow` placed at T5 while `lowland_longbow` at T6, but vanilla stats: yew = higher difficulty / damage / speed than longbow. The T5 archer could roll a stronger bow than its T6 upgrade.

Before committing tier-ordered weapons (bows, crossbows, polearms, swords), grep vanilla stats:

```bash
grep -A20 'id="<weapon_id>"' "<game>/Modules/SandBoxCore/ModuleData/items/weapons.xml" \
  | grep -E 'difficulty|damage|missile_speed|speed'
```

Then sort by primary damage stat and assign tiers in ascending order.
