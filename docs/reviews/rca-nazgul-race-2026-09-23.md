# RCA: the Nine become race nazghul (#644), 2026-09-23

## Top-line

#644 put the Nine on the Armory's `nazghul` race and made existing saves adopt it (the
`RacePersistenceService` restore now leaves the Nine's XML race alone). The seven-lens
`/deep-review` found no engine incompatibility and no data-flow gap in that change, but it found
one pre-existing HIGH the change sat on top of (three of the Nine were defined twice, so their kit
was a coin flip and their age came only from the row), one missing gate (a new reference into the
unversioned Armory with nothing in the repo checking it), and a set of documentation and comment
misses. All were fixed in the same session; the general double-definition class is #648. Two
decisions came from Mike mid-review: vanilla lords live only in `lords.xslt` and new lords only in
`characters/lords.xml`, and all nine get orc shield-crush.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH (pre-existing, XML lens) | `lord_1_48_1/2/3` had a `lords.xslt` template AND a `characters/lords.xml` row. The row won every attribute it stated (race uruk, age 20, face age 22.19) and its equipment sets were ADDED to the template's (`<Equipments>` is `AlwaysPreferMerge`, `EquipmentSet` unique on id + civilian + stealth), so `Hero.SetInitialValuesFromCharacter` (`Hero.cs:2315`, picks at `:2352`, `:2356`) dressed them in the Nazgul kit or a generic Mordor kit at random. The templates carried vanilla children's ages (31, 9, 11). | Data: double definition | The plan read "three are race uruk in characters/lords.xml" from a doc and grepped `lords.xslt` only for the six ids it expected there; either file alone looks complete, and no gate compares them. | Per Mike: the rows' winning values moved into the templates, the rows deleted. `NazgulRaceDataTests.LordsXml_DefinesNoneOfTheNine` and `..._KeepTheValuesTheirRowsCarried` pin it. #648 for the other 176 ids in both files (113 with differing kits) and the general gate. Lesson in `xslt-moduledata.md`. |
| 2 | MED (XML + engine lenses) | Nothing in the repo tied `race="nazghul"` to the Armory's `skins.xml`. `FaceGen.GetRaceOrDefault` is a plain dictionary index (v1.5.3 `TaleWorlds.MountAndBlade.FaceGen.cs:115-118`), so an install without the race throws out of the NPCCharacters load. | Unversioned-module dependency | The plan checked the Armory by reading it once. The "Unversioned modules" trap is phrased for a FIX landed in the Armory; a new REFERENCE into it reads as ordinary repo data, and every repo gate passed. | `CultureRaceConsistencyTests.EveryCharacterRaceIsARealRegisteredRace`, widened by the design lens to every `race` in ModuleData XML (2,029 declarations) and XSLT (19); Inconclusive without the install. Lesson in `data-content-cultures.md`. |
| 3 | LOW (engine lens) | The restore-skip comment said nothing changes a wraith's race at runtime. The player's face editor does (`CharacterObject.UpdatePlayerCharacterBodyProperties`, v1.5.3 `CharacterObject.cs:479`, `base.Race = race` at `:486`), for a Player Switcher wraith. | Unverified absolute claim | Written from design reasoning without enumerating the engine's writers of `BasicCharacterObject.Race`. | Comment rewritten; Mike kept the revert (a wraith stays a wraith), documented in `player-switcher.md` and `hero-race.md`. Covered by `evidence-over-claims.md` §C; no new rule. |
| 4 | LOW (XML lens) | Issue #644 said the nazghul skin binds no voice. It binds `male_02` to `male_08` (`skins.xml:31884-31892`). | Unverified fact in an artifact | Carried from the plan into the issue without reading the skin's `voice_types`. | Issue corrected, and the bald and clean-shaven consequence (one hair, one beard entry) added. §C again. |
| 5 | LOW (data-flow + completeness lenses) | Doc and comment drift: the owning doc `hero-race.md` did not know the restore rule; two more comments still described the old races (`UncapturableRegistryTests` summary, `UncapturableHeroesConfig.HeroSets`); `lords-and-heroes.md` advised editing the `lords.xml` row of a double-defined lord, the opposite of Mike's rule; counts (1184 rows, 179 in both, uruk 59 and 163) and a line reference (`lords.xslt:1060`) went stale by three; `nazgul-family.md` still had the trio as Dol Guldur. | Doc completeness | The stale-text sweep grepped for phrasings ("no race attribute", "eight of the Nine") and missed paraphrases ("NOT reachable by race"); it updated docs that mention the Nine, not the docs that own the changed code and data; and it never looked for numbers the deletion invalidated. | All fixed, counts re-measured with each doc's own command. Lesson in `misc.md`. |

A decision, not a defect: `orcShieldCrushRaces` listed `uruk`, so the three ex-uruk wraiths would
have lost shield-crush silently (data-flow and XML lenses). Mike: all nine get it.

A disagreement, resolved: the data-flow lens said the Nine go from 100 to 125 HP. The engine lens
showed campaign HP comes from `CharacterStatsModel` (`CharacterObject.MaxHitPoints`, `:398`, base
100), and only a non-campaign character takes the monster's 125. Verified this session; the issue
says "125 HP in a Custom Battle only".

## Root-cause pattern

Findings 1, 2 and 5 are one shape: **a fact about the Nine was read from the one place the plan
looked, and each had a second place.** The race lived in two files; the race's existence lived in a
file outside the repo; the documentation of the race lived in docs that paraphrase. The fix in
each case was to ask where ELSE the fact is stated or enforced before changing it.

## Why each lens missed or caught these

- Standards: passed; nothing in its rule set looks at data duplication.
- Engine: caught 2 and 3, verified the whole deserialise-and-load chain for a single definition;
  the double definition was outside its question.
- Data flow: traced every consumer of the race and caught the shield-crush decision and 5's owning
  doc; it traced flows, not definitions, so 1 passed it.
- XML: caught 1, 2 and 4, because its brief asked whether any other ModuleData row pins these ids.
- Efficiency: nothing to find; confirmed the restore skip is cheaper than restoring.
- Completeness: caught the rest of 5, including the reversed advice in `lords-and-heroes.md`.
- Design: confirmed the restore skip is the simplest shape (capture-time exclusion or a one-time
  clear both fail one of Mike's two requirements) and proposed the two test merges applied.

## Feedback memories to codify

None beyond the lessons entries: the three patterns are recorded in
`docs/reviews/lessons/xslt-moduledata.md`, `data-content-cultures.md` and `misc.md`.
