# Tournament team templates per culture

## Overview

Each town-holding culture has its own tournament team templates: the weapons the teams carry into the
arena come from what that culture's own troops wield. A town's current culture is used (vanilla opens the
arena with `Settlement.Culture`), so a converted town switches to its new culture's weapons. Issue #422,
part 1. The lord templates and the female lord rosters (parts 2 and 3 of the issue) are not part of this.

## Why This Exists

- **Vanilla behavior:** `TournamentFightMissionController.GetTeamWeaponEquipmentList` reads
  `Settlement.Culture.TournamentTeamTemplatesForOne/Two/FourParticipant`, picks one NPCCharacter at random and
  gives roster `i` of it to team member `i`. Only the weapon and mount slots are used; slots 5 to 9 come from the
  fighter's own armour (`GetParticipantArmor`, see [tournament-armor-assignment.md](tournament-armor-assignment.md)).
  An empty list falls back to vanilla's `tournament_template_empire_*_set_v1`.
- **TAOM requirement:** every TAOM culture in `taom_spcultures.xml` listed vanilla's `tournament_template_empire_*`
  (aserai, sturgia or vlandia for the cultures cloned from those), so a Gondor, Erebor or Mordor tournament was
  fought with Calradian swords, kite shields and horses. The six cultures `spcultures.xslt` renames (empire is
  Dunland, vlandia Rohan, aserai Harad, khuzait Rhun, sturgia Dale, battania Khand) listed vanilla's own
  Calradian sets.
- **Without this feature:** the arena weapons look wrong for the host culture, and a dwarf or orc carries a
  weapon no troop of its people uses.

## Architecture

### Data

`Main/_Module/ModuleData/characters/taom_tournament_templates.xml` is generated and never edited by hand. It
holds 9 NPCCharacters per culture (30 cultures: the 24 TAOM town cultures plus the six renamed ones):
`taom_tournament_<culture>_<one|two|four>_participant_set_v<1..3>`, where `<culture>` is the culture's string id
(`empire`, not `dunland`, for Dunland). The shape mirrors vanilla: 1, 2 or 4 `EquipmentRoster`s (one per team
member, so the mission never indexes past the list), weapon slots `Item0` to `Item3` plus `Horse` and
`HorseHarness` for a rider, a civilian `EquipmentSet`, level 6, the vanilla level 6 skill template. The template
`culture` is the listing culture.

- TAOM cultures: `taom_spcultures.xml` lists the three templates per size. `tools/promote_borrowed_cultures.py`
  rewrites the same lists when it regenerates a cloned culture (anduinvale, angaladh, dorwinion, southrhovanion),
  so `test_generator_regions_in_sync` stays green.
- Renamed cultures: one `spcultures.xslt` template matches the three `tournament_team_templates_*` children of the
  six renamed `Culture` nodes and replaces each vanilla list with three templates built from the culture's vanilla
  id and the list's size, so the sets are `taom_tournament_empire_*` and so on. The six culture templates' passthrough
  lines are unchanged from trunk.
- `SubModule.xml` registers the file as an `NPCCharacters` node for every game type the cultures load in.
- Bandit cultures hold no town and keep no template list.

### How the weapons are chosen

`python tools/generate_tournament_templates.py` (`--apply` writes, `--check` fails when the file is stale; it
needs the installed game for item classes: `BANNERLORD_GAME_DIR`).

1. Every battle roster of every troop in `troops/*.xml` whose `culture` is the culture (or its named source:
   Khand uses the Rhun roster as `spcultures.xslt` documents, Abanissa the Harad one (`Culture.aserai`, as `TREE_ALIASES` in `generate_enlistment_rosters.py` says), Blue Craig and
   Misty Mountain orcs the goblin one) is a candidate kit. A kit is an observed, working loadout, so shield and
   polearm pairs follow what the troops already carry (the shield audit holds for them) and a dwarf or orc never
   gets a horse or a bow its people do not use.
2. A kit is classed `mounted` (has a Horse), `archer` (bow or crossbow with its ammunition), `onehand` (a shield,
   or only one-handed melee weapons) or `twohand` (a two-hander or polearm without a shield). Rosters with an
   item the class registry does not know in a weapon slot (a banner) are dropped, and so are mounts that are
   creatures or dwarf rams (`DWARF_MOUNT_IDS`, the Patch46 dismount).
3. Within a class the kits are ranked by how many troops carry them, then spread so the first few differ in the
   main weapon. The plans hand them out round-robin: one-participant sets are onehand, twohand, archer;
   two-participant sets are onehand+onehand, onehand+archer, twohand+mounted; four-participant sets mix
   onehand, twohand, archer and mounted. A class a culture lacks falls back to another (a dwarf culture has no
   mounted sets and fills them with two-hand or one-hand kits).
4. Arrows and bolts become vanilla's blunt `tournament_arrows` and `tournament_bolts`, a second quiver is dropped.

### Not blunt

Vanilla's melee weapons in these sets are blunt sparring crafted items. The Armory ships no blunt twin of its
melee weapons (they are crafted items, so a twin needs its own pieces), so melee weapons are the troop's own
sharp ones, a change from vanilla, whose tournament melee weapons are blunt. Safety relies on vanilla's
`ArenaAgentStateDeciderLogic` (see Knock-out only), not on the weapon. Only ammunition is blunt.

## Tests

- `tools/tests/test_generate_tournament_templates.py`: classing, ammunition swap, roster counts per team size,
  fallbacks, shared mount slots, and that the shipped file equals a fresh run when the game is installed.
- `TAOM.Tests/Core/CultureTournamentTemplateTests.cs`: every TAOM town culture and, through the compiled
  `spcultures.xslt`, each of the six renamed cultures lists three own templates per size, each defined, owned by
  the listing culture, with exactly as many rosters as the team size and weapon slots only.
- `TAOM.Tests/Features/Arena/TournamentKnockOutTests.cs`: the engine's arena decider still implements
  `IAgentStateDecider`, so the knock-out-only behaviour the sharp weapons rely on is still there.

## Not verified

The in-game look of the arena teams (item meshes on every race, mounted sets, sharp melee weapons) has not been
checked in a running game.

## How to verify in game

1. Start a campaign and enter a town of a TAOM culture (for example a Gondor or Erebor town) with a tournament
   running; join it or watch a round.
2. Check that the teams carry that culture's weapons and shields, that archers fire arrows, and that a mounted
   fighter rides one of the culture's own horses (never a dwarf on a horse).
3. Repeat in a Rohan, Dunland, Harad, Rhun, Dale and Khand town (the six renamed cultures), and in a town you
   converted to another culture (the new culture's weapons appear).
4. Confirm that a hit with a melee weapon knocks a fighter out and never kills them (expected from the engine, not yet seen in game).
5. Open a Custom Battle troop picker: "Tournament Fighter" is not listed. Open the encyclopedia Troops page: it is not
   shown there either.
6. In an Erebor or Ered Luin town, run tournaments until 2- and 4-participant sets have spawned: no errors, no
   rider on a horse.

## Knock-out only

Every tournament mission adds `ArenaAgentStateDeciderLogic` (SandBox `TournamentMissionStarter`), which
`Mission.GetAgentState` consults before the death roll; it always returns Unconscious, so the culture weapons knock
out and never kill. Checked in the v1.5.7 SandBox code, not yet seen in game. `TournamentKnockOutTests` pins that
the decider still implements `IAgentStateDecider`, so an engine change that drops it fails a test.

Not covered: the arena practice fights (`ArenaPracticeFightMissionController`, a separate mission with its own weapons).

## Generator gates

After `--apply`, run `python tools/audit_polearm_shield_parity.py`: it must exit 0. The generator already refuses a
Shield beside a TwoHanded weapon (the AI drops it); a polearm beside a shield is copied only from a troop roster the
audit already accepts. Exit codes of the generator: 1 stale, 2 no Armory, 3 a culture has no usable kit, 4 no
`LOTRLOME_items` folder.

## Custom Battle and mounts

The templates carry `occupation="NotAssigned"` so Custom Battle's Soldier-occupation troop pickers do not list
"Tournament Fighter". Dwarf cultures (Erebor, Ered Luin) get `race="dwarf"` and an inline dwarf civilian roster, never
a vanilla civilian set (#1431). A creature mount a rider cannot sit (spider, elephant, Mumakil) and the dwarf ram family
are kept out of the kits; a warg or dire wolf from an orc culture is not blocked by `CanAgentRideMount`.

## Key Files

| File | Purpose |
|------|---------|
| `tools/generate_tournament_templates.py` | Generator: derives each culture's rosters from its troops |
| `Main/_Module/ModuleData/characters/taom_tournament_templates.xml` | Generated templates (never edit by hand) |
| `Main/_Module/ModuleData/taom_spcultures.xml` | Lists the three templates per size for each TAOM culture |
| `Main/_Module/ModuleData/spcultures.xslt` | One template replaces the vanilla lists of the six renamed cultures |
| `Main/_Module/SubModule.xml` | Registers the templates file as an `NPCCharacters` node |
| `tools/promote_borrowed_cultures.py` | Writes the same lists when it regenerates a cloned culture |
| `tools/tests/test_generate_tournament_templates.py` | Generator unit tests and the shipped-file check |
| `TAOM.Tests/Core/CultureTournamentTemplateTests.cs` | Config validation, including the compiled XSLT |
| `TAOM.Tests/Features/Arena/TournamentKnockOutTests.cs` | Pins the engine's arena knock-out decider |

## Dependencies

- Vanilla `TournamentFightMissionController.GetTeamWeaponEquipmentList` and `CultureObject.TournamentTeamTemplatesFor*Participant` (engine side, unchanged).
- Vanilla `ArenaAgentStateDeciderLogic` (SandBox) for knock-out-only fights.
- `tools/taom_schema.py` item class registry, `tools/generate_enlistment_rosters.py` (creature mount ids), `tools/_gamedir.py`; the installed Armory for item classes.
- `Patch46` (dwarf tournament dismount) still clears a dwarf's mount at match start.

## Changelog

- 2026-11-19: Each town-holding culture lists its own generated tournament team templates; the six renamed cultures
  get theirs through one `spcultures.xslt` template. Abanissa's kits come from the Harad (`Culture.aserai`) troops.
  Melee weapons are the troops' own sharp ones.

## GitHub Issue

- **Issue:** #422: per-culture tournament gear, part 1 (team templates); parts 2 (lord templates) and 3 (female lord rosters) are owed.
- **Status:** Open
