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

## Equipment sets are mixed PER SLOT, not chosen whole (MANDATORY before editing any troop roster)

**A non-hero troop never wears one of its equipment sets. The engine builds its kit slot by slot,
taking each slot from a different, independently chosen set.** So a troop's battle sets are not
alternatives; they are a menu the engine orders one item from per slot. Write every battle set of a
troop to be **interchangeable slot by slot with every other**, or the engine will assemble a
combination you never authored.

`Equipment.GetRandomEquipmentElements` (v1.5.6, `TaleWorlds.Core.Equipment.cs`) loops the 12 slots and
draws each from `list[weaponSetNo]`. There is **no set-count threshold** in the method, so this
starts at **two** battle sets. A campaign battle always takes the seeded branch, which re-rolls the
set index at the top of every iteration and so makes all 12 slots independent:
`Mission.SpawnTroop` chains `AgentData.TroopOrigin`, which calls `EquipmentSeed(troopOrigin.Seed)`,
and `CharacterHelper.GetPartyMemberFaceSeed` (`CharacterHelper.cs:589`) returns `abs(...) % 2000`,
so the seed can never be the `-1` that would group slots. (`GuardsCampaignBehavior` does pass `-1`
for settlement guard visuals: seed -1 groups slots 0-1, 2-3 and the rest. Battle spawns never
pass it.)

| Rule | Why |
|---|---|
| If any battle set puts a bow in a slot, **every** battle set must put a bow in that same slot, and the arrows must sit at one fixed slot index present in every set | Gate: `INCONSISTENT_WEAPON_SLOT` (#529) |
| At least one slot index must hold a weapon in **every** battle set | Gate: `INCONSISTENT_WEAPON_SLOT`. Promote the sidearm into a deleted bow's slot |
| A shield in one set and a `requires_no_shield` weapon in another is the same defect as pairing them in one set | The AI will not draw the weapon. `audit_polearm_shield_parity.py` is per-set and cannot see the cross-set form (#531) |
| `Horse` and `HorseHarness` (slots 10 and 11) also draw independently | Pair them consistently across sets |
| Heroes are exempt | `CharacterObject` returns the single `HeroObject.BattleEquipment` when `IsHero`, and `Mission.BuildAgent` forces `FixedEquipment(true)`. Wanderer/companion templates pick a whole set once at creation (`Hero.SetInitialValuesFromCharacter` does `list.GetRandomElement().Clone()`). For those, in-set completeness is the whole requirement |
| Civilian sets mix too, but only among themselves | Battle and civilian pools never cross. A single civilian set always resolves whole |

**You cannot check this in game by looking at the troop.** The encyclopedia, party screen, troop
tree and tournament all use whole-set selection and always render set #1, so a broken troop looks
correct in every UI surface. Only a live mission agent mixes. Check it in the data, or in a battle.

## Troop ID Naming Convention

`{culture_prefix}_{origin}_{role}` — Examples:
- `dg_goblin_slave` — Dol Guldur, goblin race, slave role
- `dg_khamul_shadow_initiate` — Dol Guldur, Khamul's line, shadow initiate
- `gondor_ano_peasant` — Gondor, Anórien origin, peasant role

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

Twelve per-culture types, not nine.

A clan template id is a convention: never compute one. Grep `characters/clans.xml`, `spclans.xslt` and `lord_party_templates/lord_party_templates.json` for its binding ([party-templates.md](../../docs/modding/party-templates.md)).

**`max_value` is not the party's size.** The engine draws ONE uniform ratio per party and fills every
stack to `min + (max - min) * r`, so a template's max sum is a spawn ceiling and the expected spawn
roster is the midpoint of its min and max sums; `PartySizeLimit` still governs recruitment afterwards.
Read [docs/reference/party-template-sizing.md](../../docs/reference/party-template-sizing.md) before
retuning these numbers, and use `tools/rebalance_party_template_maxes.py` rather than hand-editing.

A template no culture, clan or hero binds is dead data, and a retagged culture inherits Calradia for what its block omits. Gate: `CulturePartyTemplateTests`. Contract: [culture-playability-wiring.md](../../docs/features/culture-playability-wiring.md).

## Save Compatibility

- Never rename or delete a troop id; retire one by removing it from every upgrade_targets. Gate: `TROOP_ID_REMOVED`.
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

## Weapon tiers

Names do not imply tier. Launchers are generated ladder cells: give a new archer any bow of the class, then `python tools/rebalance_ranged_ladders.py --apply` (`docs/features/ranged-ladders.md`). Melee weapons fit a DPS band: `MELEE_LADDER_INVERSION`, repair with `python tools/fix_melee_ladder.py`.
