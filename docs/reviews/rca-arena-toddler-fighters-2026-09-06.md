# RCA: arena practice fighters and gear dummies rendered as toddlers (2026-09-06)

## Top line

Players reported that the arena's "Practice Fighter" and "Gear Dummy" spawn child-sized. A screenshot
from a Gondor arena shows a waist-high agent, nameplate "Practice Fighter", trading blows with a
normal-sized Harondor Militia troop.

The cause is one missing XML element. 46 `NPCCharacter` entries across ten cultures, the entire arena
practice set, shipped with no `<face>` block. That is the whole defect. No code was wrong, no patch
misfired, and no engine version changed.

## The mechanism

`BasicCharacterObject.Deserialize` (v1.4.8, decompiled from the installed
`TaleWorlds.Core.dll`) declares two local `BodyProperties` initialised to `default` at `:346-347`,
fills them from the `<face>` node's `<BodyProperties>` / `<BodyPropertiesMax>` children, and at
`:472-475` registers the character's `MBBodyProperty` from those two locals whenever no
`face_key_template` set `BodyPropertyRange`. With no `<face>` element at all, both locals stay
all-zero, so the character's body-properties age is 0.

The engine picks the body mesh from that age. `skins.xml` gives every race ten `<skin>` elements, two
per maturity tier, and age 0 lands on `mesh_maturity_type="toddler"`, `min_scale` 0.52 against the
adult 1.07. The Armory's `skins.xml` carries 24 toddler skins across its 12 races, so this is not a
human-only failure.

## Why nothing caught it

**Three separate guards exist, and all three are blind to an absent element rather than a wrong one.**

1. `Mission.SpawnAgent` forces age 29 when the character's age is exactly 0, and forces 27 for a
   sub-teenager in `Battle`, `Duel`, `Tournament` or `Stealth` mode (`Mission.cs:4101-4122`). Both
   read `agentCharacter.Age`, which is a different number: `BasicCharacterObject.Deserialize:486`
   sets it to `max(20f, BodyPropertyMax.Age)` when the XML has no `age=` attribute. A faceless
   character therefore reports a healthy campaign age of 20 while its visual age is 0. The two
   guards were written for exactly this failure and cannot see it.
2. `validate_moduledata.py`'s `BROKEN_BODY_PROPERTY_REF` fires on a `BodyProperty.*` reference that
   resolves to nothing. There was no reference. A typo would have been caught; an omission was not.
3. The engine's `NPCCharacters.xsd` enforces required *attributes*, not required child elements, and
   `<face>` is optional there because vanilla genuinely has characters without one.

**And the symptom does not look like a data bug.** A faceless character is otherwise perfect: correct
name, correct equipment, correct skills, correct culture, correct AI. It is only the wrong size. That
reads as a scaling, skeleton or race problem, which is where an investigation naturally starts and
where nothing is wrong.

## Why the ten cultures and not the other nine

The nine cultures authored later (bluecraig, dolguldur, erebor, goblin, gundabad, lindon, mirkwood,
mistymountainorcs, rivendell) all carry
`<face><face_key_template value="BodyProperty.fighter_<culture>" /></face>`, `race=`, and
`occupation="Townsfolk"`. The ten older ones carry `occupation="Soldier"` and no face. Vanilla's own
`gear_practice_dummy_empire` uses `BodyProperty.guard` and `occupation="Special"`.

So the correct shape existed in three places in the same repo and the authoring recipes did not
mention it. `docs/features/arena.md`, `docs/features/tournament-armor-assignment.md` and
`docs/modding/npcs-notables-and-townsfolk.md` each carried a "how to add a culture's dummy" recipe;
all three listed the id, the equipment roster and the item-id check, and none listed the face. The
ten cultures were authored by following those recipes correctly.

## What is still open

**How a practice character reaches an arena roster at all.** In stock 1.4.8 these entries are
equipment donors and nothing else:

| Entry | Read by | Read for |
|---|---|---|
| `weapon_practice_stage_N_<culture>` | `ArenaPracticeFightMissionController.AddRandomWeapons` | `.BattleEquipments` |
| `gear_practice_dummy_<culture>` | `DefaultTournamentModel.GetParticipantArmor`, and TAOM's `TaomTournamentModel` override | `.RandomBattleEquipment` |
| `CultureObject.GearDummy` | parsed from `gear_dummy=` and then read by no shipped code in any assembly | nothing |

`CultureObject` in 1.4.8 does not read the `gear_practice_dummy` or `weapon_practice_stage_*`
attributes at all; only `gear_dummy` survives, and it is dead. Both roster builders
(`ArenaPracticeFightMissionController.GetParticipantCharacters` and
`FightTournamentGame.GetParticipantCharacters`) draw from the town garrison and the culture's
basic-troop upgrade tree. A sweep of the merged install found zero upgrade edges into a practice
character, zero `MBPartyTemplateStack` entries referencing one across 778 templates, and nothing in
TAOM's recruitment pools. The screenshot proves they spawn. The path was not found and is recorded as
open rather than guessed at.

This does not block the fix: a correct `<face>` renders correctly wherever the path turns out to be.

**Six of the ten cultures cannot be reached anyway, for an unrelated reason.** Both lookups build
their id by string concatenation on `Culture.StringId`, and TAOM reskins six vanilla cultures through
`spcultures.xslt` rather than declaring new ones, so those keep the vanilla id: `empire` is Dunland,
`aserai` Harad, `vlandia` Rohan, `khuzait` Rhûn, `sturgia` Dale, `battania` Khand. `ResolveDummyId`
asks for `gear_practice_dummy_vlandia`; TAOM's entry is named `gear_practice_dummy_rohan`; SandBoxCore's
Calradian entry answers. Those six sets are authored but unreachable, and the arena fighters in those
towns wear vanilla kit. Renaming them changes the armour six cultures fight in, so it is a content
decision, not a typo repair, and it was not made here. Recorded in
`docs/features/tournament-armor-assignment.md`.

The reported case is Gondor, which is one of the four cultures among the ten that do resolve
(gondor, isengard, mordor, lothlorien).

## The fix

Each of the 46 characters gets its own file's fighter template, the same one its siblings use:

| Culture | Template | Culture | Template |
|---|---|---|---|
| dale | `default_character_creation_body_property_sturgia` | mordor | `fighter_uruk_mordor` |
| dunland | `fighter_dunland_a` | rhun | `fighter_rhun` |
| gondor | `fighter_gondor` | rohan | `fighter_rohan` |
| harad | `fighter_haradrim` | isengard | `fighter_uruk_hai` |
| khand | `default_character_creation_body_property_battania` | lothlorien | `fighter_rivendell` |

All ten were checked to exist in the merged registry with adult age ranges before use. Data only, no
code change.

## Preventive actions

| Action | Where |
|---|---|
| Every `NPCCharacter` under `Main/_Module/ModuleData` must declare a `<face>` | `TAOM.Tests/Core/CharacterFaceCoverageTests.cs` (new). Fails with the 46 named, passes after |
| The three "add a culture's dummy" recipes now list the face step | `docs/features/arena.md`, `docs/features/tournament-armor-assignment.md`, `docs/modding/npcs-notables-and-townsfolk.md` |
| The mechanism, and why the engine's age guards miss it | `docs/modding/body-properties.md` "Gotchas" |
| The gap in the reference-based validator, stated as a coverage boundary | `docs/features/moduledata-validation.md` |
| Crash-triage row | `CLAUDE.md` Traps |

## Verification

- `CharacterFaceCoverageTests` red before the fix with all 46 listed, green after.
- Full suite: 8294 passed, 0 failed, 3 skipped.
- `python tools/validate_moduledata.py` reports no new codes; its counts are unchanged by this
  change, which touches no reference.
- In-game confirmation of the corrected size still needs a player or a manual arena visit. Not done
  here.
