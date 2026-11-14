# RCA: villagers spawn with no weapon (#1273, 2026-11-14)

## Top-line

Players reported villagers that fight with their fists. Every villager character in
`Main/_Module/ModuleData/characters/npcs_*.xml` had battle equipment rosters that held clothes
only. The fix puts a vanilla farm tool in `Item0` and `throwing_stone` in `Item1` of every battle
roster of every adult villager (53 characters, 73 rosters, 27 files). Children and teenagers stay
unarmed: the engine uses them only in village scenes, in civilian clothes.

The deep review ran at tier 2 (lens 5 Data flow and lens 7 XML). Codex was waived by the maintainer
for this change. One finding was confirmed and fixed before the commit.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MEDIUM | Six cultures' village women stayed unarmed after the first pass: `village_woman_mirkwood`, `_gundabad`, `_dolguldur`, `_goblin`, `_mistymountainorcs` and `_bluecraig` are `occupation="Townsfolk"`, and the one-off script selected on `occupation="Villager"`. The maintainer's rule is every adult villager, men and women. No battle reads a village woman's battle roster today (`CommonVillagersCampaignBehavior` spawns her with civilian equipment), so a player saw no difference. | Selection by a proxy attribute | The script and the orchestrator's own rescan used the same filter, so the rescan could only agree with the script. Both reviewers resolved each culture's `village_woman` field to its character and found the six. | Select the characters by the role that names them (the culture's `villager` and `village_woman` fields, or the id family), and check the result with a rule that does not share the edit's filter. Lesson appended to `lessons/data-content-cultures.md`. |

## Root cause of the bug itself

The villagers were first committed as village crowd characters with civilian rosters only
(`villager_gondor` in 1492e4300, 2026-03-06; Rohan, Erebor and Dale the same). Their battle rosters
came later as clothes-only rosters, either by hand or as twins of the civilian rosters from
`tools/add_townsfolk_battle_rosters.py` (#295), which exists so that arena spectators are not
naked. Vanilla's own villager carries a farm tool in every battle roster
(`villager_empire` in SandBoxCore `spnpccharactertemplates.xml`), and that part was never copied.
A villager party on the map is built from the culture's `villager` character
(`VillagerPartyComponent`, the culture's `villager_party_template`), and the engine arms it from
the battle rosters, so the party fought unarmed.

## Why each reviewer and gate missed it before

- `validate_moduledata.py` checks that item references resolve. A roster with no weapon has no
  broken reference.
- The lesson "Townsfolk/notables need a battle `<EquipmentRoster>`" asks for a battle twin of the
  civilian roster. That is right for a spectator and incomplete for a character that fights.

## Not verified

No game check. Owed: attack a villager party and confirm the tool in hand and the thrown stones;
the same on a dwarf, an elf and an orc villager; a hosted co-op session. The steps are in
`docs/modding/npcs-notables-and-townsfolk.md`, "Villagers that fight".

FOLLOW-UP: no issue: the Ered Luin generator check (`generate_new_faction_kingdoms.py --only
eredluin --check`, "would change: clans.xml") fails on trunk since f5b97a942 and is reported to the
maintainer, because the banner edit in `clans.xml` is another contributor's.
