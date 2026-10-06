# Generated Lord Kits

## Overview

A lord the engine generates during a campaign wears gear that fits their culture **and race**: a kit the lords defined in TAOM's XML already wear. Before this, a generated Dunland lord ("Ulric") wore vanilla Empire armour, and nothing stopped a human Mordor lord from drawing uruk armour.

## Why This Exists

- **Vanilla behavior:** `DefaultEquipmentSelectionModel.GetSuitableEquipmentSet` pools every loaded `EquipmentRoster` whose culture is the hero's and whose `<Flags>` equal the request exactly, then picks one set at random. It never looks at race. Adult heroes created from a template (`HeroCreator.CreateSpecialHero`) skip the model and keep a clone of the template's gear.
- **What went wrong in TAOM:**
  - SandBoxCore and SandBox keep their lord, teen and ruler templates loaded for the six renamed vanilla cultures. A male Dunland (`empire`) lord's battle pool held 26 vanilla sets and one TAOM set, and the other five cultures were similar (Khand 37:1).
  - Those cultures' `lord_templates` and `rebellion_hero_templates` are still vanilla's minor-faction leaders in Calradic kit.
  - Mordor and Isengard field lords of more than one race, so a race-blind pick dresses one race in another's armour.
- **TAOM requirement:** generated lords look like the lords of their culture and race.

## Architecture

### Two layers

1. **Data (`lord_template_rosters.xslt`).** Removes `IsLordTemplate` and `IsKingdomRulerTemplate` from every non-`taom_` roster of `Culture.vlandia/empire/aserai/khuzait/sturgia/battania`. The engine's own pick then lands on TAOM's `taom_*` templates (`equipmentsets/taom_lord_template_equipment.xml`).
   - **Child templates keep their flags.** TAOM ships child templates for Khand only, and an empty pool hands the hero null equipment.
   - **Node order matters.** The engine applies a node's stylesheet only to roster nodes merged before it, so the node must stay the last `EquipmentRosters` node in `SubModule.xml` (pinned by a test).
2. **Code (peer kits).** When the engine equips a generated adult lord, `LordKitSelector` picks a kit that:
   - matches the hero's culture, race, sex and equipment type (battle or civilian);
   - at least two XML lords are **defined with**, so one lord's own gear (Théoden's, the Mouth of Sauron's) never spreads;
   - is worn by at least one living lord, so a kit only dead lords wore (the kept vanilla ancestors `dead_lord_6_*`) never comes back.

   With no such kit, layer 1's template stands.

### Who gets a peer kit

`LordKitSelector.Wants`: an adult lord outside a minor-faction clan.

| Path | Model | Peer kit? |
|---|---|---|
| A clan child comes of age | `TaomEquipmentSelectionModel.GetEquipmentForHeroComeOfAge` | Yes |
| The player makes a companion a lord | `GetEquipmentForCompanionWhenTurningToLord` | Yes |
| A ruler steps down after a ruling-clan change | `GetEquipmentsForChangingRuler`, old ruler only | Yes; the new ruler keeps TAOM's ruler template |
| A new companion clan's extra lords, rebel leaders | `TaomHeroCreationModel.GetCivilianEquipment`/`GetBattleEquipment` | Yes |
| Minor-faction heroes (Corsair Blades and the rest) | same | No: they keep their template (the clan is read at creation; `Hero.IsMinorFactionHero` is set only afterwards) |
| Children, teenagers, newborns | all | No: the engine's child and teen templates |
| Wanderers, notables, TAOM's commissioned and warden heroes | hero creation | No: not lords |

### Where the kits come from

`LordKitDonorAdapter` reads each XML lord's kit **as authored**, through the roster `BasicCharacterObject.Deserialize` registers under the lord's id (`MBObjectManager.GetObject<MBEquipmentRoster>(lordId)`).

- **Not live gear.** `CharacterObject.BattleEquipments` returns a hero's current gear, which for a dead hero is the engine's shared `DeadBattleEquipment`. Reading it let two deaths make that dead kit "shared".
- **Donors:** heroes (`HeroObject` set, which excludes the faction-picker copies) whose character is original (generated heroes are copies of a template), not a template, a lord, and not the player.
- **Prefilter:** only the requested culture, race and sex are turned into candidates.
- **Hand-out:** the picked kit is cloned, because every lord defined with a roster shares its `Equipment` instances.

### Component Diagram

```
Engine (AgingCampaignBehavior, CompanionRolesCampaignBehavior,
        NPCEquipmentsCampaignBehavior, HeroCreator)
        |
TaomEquipmentSelectionModel / TaomHeroCreationModel   (one-line delegations, ?? base)
        |
ILordKitDonorAdapter.PickKit(hero, type)
        |  describes the hero, reads XML lords' rosters
LordKitSelector.Wants / Pick   (pure, unit-tested)
```

## Configuration

None. The kit pool is the XML lords themselves: re-kit a lord in `characters/lords.xml` or `lords.xslt` and generated lords follow.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/GeneratedLordKits/LordKitSelector.cs` | Gate (`Wants`) and pick (`Pick`) |
| `Main/Features/GeneratedLordKits/LordKitCandidate.cs`, `LordKitRequest.cs` | Engine-free records |
| `Main/Features/GeneratedLordKits/Models/TaomEquipmentSelectionModel.cs` | Come of age, companion to lord, stepping-down ruler |
| `Main/Features/RaceAge/Models/TaomHeroCreationModel.cs` | Adult lords created at runtime (shares the model slot with RaceAge's offspring rule) |
| `Main/Adapters/ILordKitDonorAdapter.cs`, `LordKitDonorAdapter.cs` | Reads XML lords' rosters, clones the pick |
| `Main/_Module/ModuleData/lord_template_rosters.xslt` | Strips vanilla template flags for the six renamed cultures |
| `Main/_Module/ModuleData/equipmentsets/taom_lord_template_equipment.xml` | The fallback templates (`tools/generate_lord_template_equipment.py`; do not regenerate whole, #637) |

The adapter is registered in `RaceAgeIoC` and both models in `SubModule.RegisterRaceAgeAndFamily`, beside the hero-creation model they share a slot pattern with.

## Dependencies

- `ILordKitDonorAdapter` (Adapters): wraps `Hero`, `CharacterObject`, `MBEquipmentRoster`, `Equipment`.

## Tests

- `TAOM.Tests/Features/GeneratedLordKits/LordKitSelectorTests.cs`: culture, race, sex and type matching; the two-donor and one-alive rules; the gate (non-lord, child, minor faction); prefilter parity.
- `TAOM.Tests/Core/LordTemplateRosterTests.cs`: the stylesheet over a sentinel stub (what is stripped, what is kept); every pool the engine requests stays filled with TAOM sets; the node stays last; no named roster shares a lord id.

## Limits

- **Existing saves do not heal.** The engine only picks gear when a hero is created, comes of age or changes role, so a lord generated before this change keeps their gear.
- **Small race pools fall back.** A culture, race and sex whose XML lords share no kit (Mordor's and Isengard's few orc lords, some elf ladies) gets the `taom_*` template, which can be another race's armour in a mixed culture.
- **The new ruler** after a ruling-clan change keeps TAOM's ruler template, which is race-blind.
- **Khand's children** still draw vanilla Battanian child clothes beside TAOM's.

## Changelog

- 2026-10-06: added. Vanilla lord, teen and ruler templates stripped for the six renamed cultures; peer kits for generated lords by culture, race and sex; Dale's `taom_sturgia_*` templates ported from vanilla Sturgian items to Dale's mounted-noble kit (`dale_bat_template_medium_c`).

## GitHub Issue

- **Issue:** #747, [Generated lords wear vanilla Calradic gear, and the gear pick ignores race](https://github.com/haterade22/TAOM/issues/747)
- **Status:** Closed, `triage-needs-ingame` (in-game checklist owed)
