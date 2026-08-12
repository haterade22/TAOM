# Culture Playability Wiring

## Overview

What a TAOM culture needs before a player can actually finish character creation as it and land in a
working campaign. "Selectable" and "playable" are separate states, and the gap between them is
silent — no exception, no log line loud enough to notice, no failing test unless one is written for
it. This doc is the checklist and the record of the three ways that gap has already shipped.

## Why This Exists

`goblin` and `mistymountainorcs` shipped in June 2026 as CC-selectable factions
([new-factions-misty-mountains-lindon.md](new-factions-misty-mountains-lindon.md)). Every visible
gate said they were done: the region was clickable on the faction map, the culture confirmed, the
narrative pages rendered. Picking either one still produced a broken start, because three separate
systems that hand the player something at CC finalize had never heard of them.

- **Vanilla behavior:** vanilla gates the CC culture list on a hardcoded whitelist of its own six
  cultures, so it has no opinion about a modded one — it neither offers it nor validates it.
- **TAOM requirement:** a culture reached through the faction map must arrive at the campaign with
  equipment, denars, a career, a starting settlement and a race, like any other.
- **Without this:** the player finishes creation naked, broke, and standing in another faction's
  capital, with nothing in the log to explain it.

## The gate that is NOT `is_main_culture`

The most expensive wrong assumption here is that `is_main_culture="true"` makes a culture playable.
It does not. Verified in the v1.4.8 dump,
`Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CharacterCreationCampaignBehavior.cs:236-245`:

```csharp
public void InitializeCharacterCreationCultures(CharacterCreationManager characterCreationManager)
{
    foreach (CultureObject objectType in Game.Current.ObjectManager.GetObjectTypeList<CultureObject>())
    {
        if (objectType.StringId == "aserai" || objectType.StringId == "battania" || objectType.StringId == "empire"
            || objectType.StringId == "khuzait" || objectType.StringId == "sturgia" || objectType.StringId == "vlandia")
        {
            characterCreationManager.CharacterCreationContent.AddCharacterCreationCulture(objectType, 1, 10);
        }
    }
}
```

A literal six-string comparison. There is no `CanChooseCulture` property anywhere in the engine, and
`IsMainCulture` is read only by the encyclopedia, average-wage calculation and multiplayer. The only
supported way in is `AddCharacterCreationCulture`, which TAOM calls from
`CharacterCreationContentService.RegisterCustomCultures` under a handler registered at priority 1050.

**Corollary:** never remove one of the vanilla six from the registered set.
`CharacterCreationCultureStageVM.SortCultureList` (`:136-148`) is five consecutive
`listToWorkOn.Single(i => i.CultureID.Contains("vlan"))` calls — `Single` throws on a missing *or*
duplicated match, so dropping a vanilla culture, or adding one whose id contains `vlan` / `stur` /
`empi` / `aser` / `khuz`, hard-crashes the culture stage. `CharacterCreationContentService`'s
`VanillaCultureIds` skip-list exists for this reason; leave it alone.

## The three silent failure modes

Each of these was live in shipped builds. None of them raised an error.

### 1. No character-creation equipment — the player exits naked

`PlayerEquipmentRosterIds` builds the roster id unconditionally:

```csharp
return $"player_char_creation_{cultureId}_{titleType}_{(isFemale ? "f" : "m")}";
```

There is no existence check. When the roster is absent, `PlayerEquipmentService` logs
`RosterNotFound` and applies nothing — the player keeps whatever the previous stage left on them.
`taom_char_creation_equipment.xml` covered twelve cultures and neither orc culture, so all 24 ids
those two cultures could produce resolved to nothing.

A culture needs a male **and** female roster for **every** `title_type` its own `youth_menu.json`
options offer — not the full title set. Gondor offers no `bard`; Erebor no `retainer`. Deriving the
required set from the culture's own menu is the only way to avoid both gaps and dead rosters.

> The six vanilla-id cultures are the exception: TAOM repurposes them rather than redefining them,
> so their CC rosters still ship in the game's own `SandBox/ModuleData/sandbox_equipment_sets.xml`,
> outside this repository.

### 2. No startup-resources row — the player starts on zero denars

`startup_resources_config.xml` documents its own default as *"Default 0 (no warning when missing)"*.
That is deliberate for cultures that should grant nothing, and it makes an accidental omission
indistinguishable from an intentional zero. Both orc cultures were missing entirely.

### 3. No eligible career — the stage collapses to the fallback

A culture with no `<EligibleCultures>` entry in `taom_careers.xml` offers only "No specialization".
The harder version of this failure — a culture with zero options in a narrative menu — throws
`KeyNotFoundException` out of vanilla's `SelectedOptions[CurrentMenu]`, which is why
`TrySwitchToNextMenu_Patch` exists. The guard is what kept the career gap invisible.

## Full checklist

A culture is playable when every row below is satisfied. Rows marked **fatal** crash or blank a
stage; the rest fail silently.

| # | Surface | File | Fatal? |
|---|---------|------|--------|
| 1 | `<Culture>` object exists | `taom_spcultures.xml` | fatal — skipped with a warning otherwise |
| 2 | Registered for CC | `charactercreation/cultures.json` | fatal |
| 3 | Narrative options in all four culture-scoped menus | `charactercreation/{parents,youth,education,adulthood}_menu.json` | fatal |
| 4 | Default body | `charactercreation/cc_body_properties.xml` | falls back to a random vanilla body |
| 5 | CC starting equipment, m+f per `title_type` | `equipmentsets/taom_char_creation_equipment.xml` | silent |
| 6 | Starting denars | `startup_resources/startup_resources_config.xml` | silent |
| 7 | At least one eligible career | `career_system/taom_careers.xml` + `charactercreation/career_menu.json` | silent |
| 8 | Child / teen / lord / education equipment templates | `equipmentsets/taom_{child_equipment,lord_template_equipment,education_equipment}_templates.xml` | fatal — NRE on new game |
| 9 | Stage-2 education tutor templates | `taom_education_character_templates.xml` | fatal — age-8 CTD (#354) |
| 10 | Enlistment rosters, 4 ranks | `equipmentsets/taom_enlistment_equipment.xml` | silent — issues another culture's gear |
| 11 | Owns at least one settlement | `TAOM_Map/ModuleData/settlements.xml` (**live, external**) | fatal — daily-tick CTD (#374) |
| 12 | Volunteer recruitment pool | `Main/Features/TroopProgression/RecruitmentPools/` | silent — empty recruit slots |
| 13 | Party templates: all twelve authored, all eight attributes bound, both caravan child lists bound | `taom_partyTemplates.xml` **plus** `taom_spcultures.xml` or `spcultures.xslt` | mixed: an unbound one is silently Calradian, a null or empty one is an NRE in `SpawnPatrolParty` / `SpawnCaravan`. See the contract above |
| 14 | `as_<race>_facegen` action set, if it introduces a race | `LOTRLOME_Armory/ModuleData/action_sets.xml` (**live, external**) | fatal — T-pose / contorted mesh |

Rows 1 to 3 and 8 to 9 are the ones that produce a visible break. Rows 5 to 7, 10 and 12 are the ones
that ship. Row 13 sits in both camps and is the one that shipped nine times over: an unbound template is
silent (the culture just fields Calradians), while a null or empty one crashes in vanilla code with
no TAOM frame on the stack.

### What a data fix like row 13 does to an existing save

Culture objects re-deserialize from XML on every load, so a binding change reaches an existing
campaign immediately: every party spawned *after* the load uses the new template. Parties already on
the map keep the roster they were built with. Vanilla only tops a patrol up when it is below full
strength (`ReplenishParty`), so a pre-fix patrol keeps its Calradians until it is destroyed and
respawns, which can take a long time. That is expected, not a second bug. A tester loading an old
save and reporting "still vanilla troops" is seeing this; ask for a fresh campaign, or for a patrol
that has died and respawned, before treating it as a regression.

## How to make a culture playable

1. Confirm the `<Culture>` block exists and `basic_troop` / `elite_basic_troop` resolve.
2. Add it to `cultures.json` with `races`, `starting_settlement` and body defaults.
3. Give it narrative options in all four culture-scoped menus. `tools/insert_new_faction_cc_menus.py`
   clones another culture's with a per-culture text remap.
4. Read the `title_type` set from its own `youth_menu.json` options, then generate CC rosters:
   `python tools/generate_char_creation_equipment.py --append <culture> --apply`.
   Add a `no_mount: True` entry to that script's `CULTURES` table if the culture's troop tree
   contains no `slot="Horse"` — otherwise the player rides out on an animal the culture never fields.
5. Add the `startup_resources_config.xml` row.
6. Give it careers — `tools/insert_new_faction_careers.py --apply` clones an existing culture's.
7. Add enlistment rosters, child/teen/lord/education templates, and a recruitment pool.
8. Remove the culture from every `documentedExceptions` list in `TAOM.Tests/` that names it.
   **Leaving a fixed culture parked in an exception list is a permanent blind spot**, and the
   coverage tests are written to fail on a stale suppression for exactly that reason.
9. Restart Bannerlord fully and start a new campaign. A new XML file or `<XmlNode>` registration is
   null in-engine until process launch, so a green validator proves nothing about the running game.

## Promoting a kingdom that borrows another culture

`bluecraig` and `lindon` were kingdoms without cultures — real factions on the map running on
`Culture.goblin` and `Culture.rivendell`. Because the starting settlement is a property of the
*culture*, not of the faction-map region clicked, picking Blue Craig dropped the player in
Goblin-town and picking Lindon dropped them in Rivendell.

Two scripts do this, and **the order is not optional**:

1. `tools/promote_borrowed_cultures.py --apply` — writes the culture DATA: the `<Culture>` block,
   troops, NPCs, equipment sets, wanderers, party templates, child/lord/education templates, the
   six stage-2 tutor templates, enlistment rosters, and the `SubModule.xml` registrations.
2. `tools/retag_promoted_cultures.py --apply` — moves the kingdom, its clans, its lords and its
   settlements onto the new culture.

Running (2) before (1) leaves lords of a culture that does not exist. Running (1) without (2) leaves
a culture that owns no land, which is the `LANDLESS_CULTURE` daily-tick CTD. The window between
them is the dangerous state, so close it in one sitting and run the validator immediately after.

### Four traps these scripts exist to encode

**A culture's id-space is not namespaced by its own name.** `troops_rivendell.xml` defines 14
`imladris_*` ids next to 13 `rivendell_*` ones, plus `noldorin_*`, `rider_*` and `battlemaster_*`;
the equipment sets add `glorfindel_*`. A blanket culture rename leaves every one of those unchanged,
and the clone then re-defines ids that already exist — the engine silently shadows one copy. The
promotion script builds an explicit id map: ids containing the source name get it swapped, ids that
do not get prefixed with the target.

**Asset ids must survive the rename; data ids must not.** `Item.*`, `BodyProperty.*`, `SkillSet.*`,
`portrait_sprite`, `particle_effect` and `sound_effect` all name real files. Renaming
`Item.rivendell_helmet_infantry_tier2` to `Item.lindon_...` invents a reference to nothing — that
mistake produced 2470 validator errors in one run here. Feat ids are the same class for a different
reason: `CulturalFeatsService` matches `FeatObject` identity against what `TaomCulturalFeats.cs`
registers, so a renamed `taom_lindon_*` feat resolves to nothing and is dropped without a word,
while the CC faction card keeps advertising the bonus.

**Scope every retag by element name.** Rewriting `culture=` inside "any element whose id matches"
sounds safe until the pattern matches the ROOT element, whose id-bearing children include the ones
you want — at which point every culture in the file is rewritten. That reported 102 retags on
`lords.xml`, exactly Rivendell's 22 plus Goblin-town's 80, instead of the 50 that belong to the new
cultures.

**`<Culture id="x">` means two different things.** It defines a culture only in
`taom_spcultures.xml`. In `cc_body_properties.xml`, `startup_resources_config.xml` and
`taom_careers.xml` it is keyed BY culture and is supposed to name it. A duplicate-id check that
does not distinguish them reports every correctly-wired culture as a duplicate of itself.

## Generator drift — do not blind-regenerate

`tools/generate_char_creation_equipment.py` owns `taom_char_creation_equipment.xml`, but the shipped
file has been hand-corrected since it was last generated. Verified 2026-08-10 by reproducing the
generator's output in memory and diffing: 257 lines differ, including Gondor's shield, which was
fixed in the file from `gond_shld2` to `wm_gondor_shield_a02` across 8 rosters and would be silently
reverted by a full run.

That is why the script gained an `--append <culture>` mode rather than being re-run. The same
caution applies to any generator whose output has outlived its tables: **reproduce and diff before
regenerating**, and prefer a surgical append.

Related drift found in the same check and left alone as out of scope: Erebor's CC rosters carry 16
horse-bearing entries despite [no-mount-cultures.md](no-mount-cultures.md) recording their removal in
March 2026 — the May regeneration reverted it. `Patch20_NarrativeHorseGuard` detects horse *absence*,
so with horses present vanilla simply runs and hands a dwarf a mount.

## Party-template binding contract

A culture is not finished when the player can pick it. It also has to spawn the right troops when the
engine builds a party for it, and that is a separate surface with its own failure mode: the bindings
are engine-read XML attributes, so a missing one produces vanilla Calradians rather than an error.

`CultureObject.Deserialize` (v1.4.8, `CultureObject.cs:269-280`) reads eleven party-template
attributes. Eight of them are load-bearing for every settled culture, and every one has a reader that
will hand out Calradian troops if the binding is wrong:

| Attribute | Reader |
|---|---|
| `default_party_template` | `LordPartyComponent`, lord party spawn |
| `villager_party_template` | `VillagerCampaignBehavior`, village trade parties |
| `militia_party_template` | `MilitiaPartyComponent.CreateMilitiaParty` |
| `rebels_party_template` | `LordPartyComponent`, the `IsRebelClan` branch |
| `vassal_reward_party_template` | fief-grant troop reward |
| `settlement_patrol_template_level_1/2/3` | `DefaultSettlementPatrolModel.GetPartyTemplateForPatrolParty`, selected by Guard House level |

Three more exist and are deliberately unbound. `bandit_boss_party_template` belongs to bandit
cultures only. `fishing_party_template` and `settlement_patrol_template_coastal` are read only by
NavalDLC, and the reader is unreachable because no TAOM settlement declares a `port_posX` pair, so
`Settlement.HasPort` is false everywhere. Revisit both if TAOM ever ships ports.

Caravans are the exception that catches people: `caravan_party_template` and
`elite_caravan_party_template` look like attributes and are never read. The deserializer takes
caravans only from the plural `<caravan_party_templates>` / `<elite_caravan_party_templates>` CHILD
elements, and it **appends** every matching child into one list rather than replacing. In
`spcultures.xslt` that means overriding caravans takes two edits that must land together: emit the
TAOM block, and add the wrapper to that block's `not(self::...)` passthrough filter. Do only the
first and the culture carries vanilla's element too, so caravans roll Calradian about half the time.

Two of these are crashes rather than wrong troops, which is why the gate asserts presence and
non-emptiness and not merely non-vanilla-ness: `PatrolPartiesCampaignBehavior.SpawnPatrolParty`
dereferences `partyTemplate.ShipHulls` with no null guard, and
`CaravansCampaignBehavior.SpawnCaravan` calls `GetRandomElementWithPredicate` on the caravan list
with no empty guard.

**The two sources behave differently, and the difference is the whole trap.** A culture in
`taom_spcultures.xml` inherits nothing, so a missing attribute is a null and fails loudly the first
time a reader touches it. A culture retagged in `spcultures.xslt` inherits everything: the block's
`<xsl:apply-templates select="@*"/>` copies the vanilla value in, so an attribute the block never
names keeps its Calradian binding with nothing in the file to show for it. That is how Dunland,
Harad, Rohan and Rhun shipped with vanilla town patrols. Sharing another TAOM culture's templates is
fine and common (Lothlórien uses Rivendell's, Umbar and the two Haradrim sub-cultures use Harad's,
Khand uses Rhun's) as long as the target is TAOM-authored.

`TAOM.Tests/Core/CulturePartyTemplateTests.cs` pins all of it. See the fourth-instance entry in
[`docs/reviews/lessons/xslt-moduledata.md`](../reviews/lessons/xslt-moduledata.md) for how it works
and why it is a whitelist.

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/ModuleData/spcultures.xslt` | Retags the six vanilla cultures; party-template bindings live here |
| `Main/_Module/ModuleData/taom_spcultures.xml` | The 24 TAOM-native cultures |
| `Main/_Module/ModuleData/taom_partyTemplates.xml` | Every party template TAOM authors |
| `Main/Features/CharacterCreation/CharacterCreationContentService.cs` | Registers custom cultures, sets race, teleports to the starting settlement |
| `Main/Features/CharacterCreation/PlayerEquipmentRosterIds.cs` | Builds the CC roster id — unconditionally |
| `Main/Features/CharacterCreation/PlayerEquipmentService.cs` | Applies the roster; logs `RosterNotFound` |
| `Main/Features/FactionMap/CultureSettingService.cs` | Bridges a faction-map click to vanilla's CC data model |
| `Main/_Module/ModuleData/charactercreation/cultures.json` | The selectable-culture list |
| `tools/generate_char_creation_equipment.py` | CC equipment rosters (`--append`, `no_mount`) |
| `tools/insert_new_faction_careers.py` | Clones a culture's careers |
| `tools/promote_borrowed_cultures.py` | Culture data for a kingdom that borrows another culture |
| `tools/retag_promoted_cultures.py` | Moves kingdom/clans/lords/settlements onto the new culture |
| `tools/insert_new_faction_cc_menus.py` | Clones the four culture-scoped narrative menus |

## Tests

- `TAOM.Tests/Features/CharacterCreation/PlayerStartCoverageTests.cs` — CC equipment per offered
  `title_type`, and non-zero starting denars, for every culture in `cultures.json`.
- `TAOM.Tests/Features/CharacterCreation/NarrativeCultureCoverageTests.cs` — options in all four
  culture-scoped menus, plus a stale-suppression check.
- `TAOM.Tests/Features/CharacterCreation/CareerCultureCoverageTests.cs` — at least one eligible career.
- `TAOM.Tests/Features/Enlistment/EnlistmentRosterCultureCoverageTests.cs` — four rank rosters.
- `TAOM.Tests/Core/CulturePartyTemplateTests.cs`: every party-template binding on every culture, for
  both sources. Runs `spcultures.xslt` over a sentinel stub rather than reading the markup, which is
  what lets it see an attribute the block never named.

All four derive their culture list from the data rather than a hand-written list, which is the point.
Every one of the failures above reached a shipped build because the table that should have caught it
was written before the culture existed.

## Changelog

- 2026-08-12: nine town-owning cultures were spawning Calradian patrols, villagers, militia, rebels
  and caravans. Bound every party template on all six retagged cultures plus `umbar`, `shaghana` and
  `abanissa`, authored the three Dale templates that never existed, and added the party-template
  binding contract above with `CulturePartyTemplateTests` behind it.
- 2026-08-10 — Promoted `bluecraig` and `lindon` from borrowed cultures to their own. Both are now
  CC-selectable and start in their own capitals (`town_GBC1`, `town_LN1`) instead of Goblin-town and
  Rivendell. Added `promote_borrowed_cultures.py` + `retag_promoted_cultures.py` and recorded the
  four traps above, each of which produced a real defect during the work.
- 2026-08-10 — Recorded the three silent failure modes after fixing them for `goblin` and
  `mistymountainorcs`: 24 CC equipment rosters, 2 startup-resources rows, 6 cloned careers.
  Added `PlayerStartCoverageTests`; un-parked both cultures from the career exception list.

## GitHub Issue

- **Issue:** not yet filed — see [new-factions-misty-mountains-lindon.md](new-factions-misty-mountains-lindon.md)
  "Known Limitations", items 5–7, which this work closes out.
