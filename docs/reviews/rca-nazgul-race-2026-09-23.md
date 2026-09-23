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

## Codex pass (Review 130, 2026-09-23)

Codex (gpt-6-astra, ultra) reviewed `9804f67b` together with #645's `b90fd3a4`: P1 0, P2 0. It
confirmed the restore skip's premise from the load sequence (the XML is re-read and copied onto saved
hero clones before `OnSessionLaunched`), ran the committed `lords.xslt` over the installed SandBox
`lords.xml` in memory (each of the Nine one `NPCCharacter`, race nazghul, one battle and one civilian
set), and found no race consumer the Nine now reach harmfully. Four #644 findings, all fixed in the
follow-up commit:

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| C1 | P3 (Codex O2) | `EveryCharacterRaceIsARealRegisteredRace` read XSLT races with one regex spelling of `<xsl:attribute name="race">`; a literal `<NPCCharacter race="...">` in a stylesheet (Codex's in-memory probe), a single-quoted `name` or an `xsl:text` child escaped it. All 19 shipped emissions use that spelling, so nothing escapes today. | Gate narrower than its claim | Written against the one form `lords.xslt` uses; the review checked that it found the 19 current emissions, not what it could miss. | Structural scan (`RacesEmittedByXslt`), a failure on a race computed at transform time, one probe test per construct; recurrence on `lessons/testing-qa.md` "Detection rulesets need a positive firing test per rule". |
| C2 | LOW (Codex S2, REPEAT of row 3) | Row 3's fix replaced "nothing changes a wraith's race" with "the only runtime writer is the player's own face editor or character import" (the latter is the `campaign.import_main_hero` cheat). Co-op join reconciliation (`JoinReconciliationService.ApplyRace`) is a third; it reaches a wraith only when a joining client is handed one, and the skip drops that edit on load like the others. | Unverified enumeration | The correction named the writer the engine lens found and walked the engine's writers, not TAOM's own `SetHeroRace` callers. | Comment and `hero-race.md` name all three writers, from a grep of every `SetHeroRace` caller; recurrence on `lessons/misc.md` "A correction is a new claim". |
| C3 | LOW (found verifying Codex S4) | The CHANGELOG entry said "the other 173 lords defined twice are #648". Measured at `9804f67b^` and `9804f67b`: 179 were defined twice before and 176 after, and #648 itself says 176. | Wrong count, correction not propagated | A hand subtraction from 179, and the later correction reached the issue but not the CHANGELOG draft. | Corrected; new lesson in `lessons/misc.md` "A claim found wrong is wrong everywhere it was written". |
| C4 | LOW (Codex S4) | "The Nazgul kit is the only kit" holds for a new campaign only: `Hero._battleEquipment` and `_civilianEquipment` are `[SaveableProperty(210)]` and `(220)` (v1.5.3 `Hero.cs:212-216`), so an existing campaign keeps the kit it rolled; on load `Hero.CheckInvalidEquipmentsAndReplaceIfNeeded` swaps only items that no longer resolve. | Lifecycle claim | The "existing saves" paragraph walked race persistence and no other changed field. | The CHANGELOG says so, and the old-save smoke owed in Review 130 expects the old kit. |

Codex also named two consequences for the smoke rather than defects, both checked in the code. The
agentless Load Game thumbnail coerces every race outside `BasicTableauRaceGuard`'s allow-list
(`uruk` only) to the human base, so a wraith's thumbnail is human-headed and the former uruk trio
lose that exception; nazghul joins the list only after an in-game render test, per the guard's own
rule. And the six formerly human wraiths now take `CharacterSpawner_InitWithCharacter_Patch`'s
non-human path (race above 0). Codex placed the three vanilla rows at `lords.xml:6079`, `:6121` and
`:6163`, the lines where each start tag closes; the elements open at 6068, 6110 and 6152.

The fix diff got its own six-lens deep review. Its #644 findings, all fixed:

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| R1 | MED (Standards, Engine, Data flow) | The first cut of the structural scan trimmed an `xsl:attribute`'s text, and a probe test pinned `' sauron '` reading as `sauron`. The transform emits the padding verbatim (proved with `XslCompiledTransform`) and `FaceGen.GetRaceOrDefault` indexes the exact string, so the gate passed a race the load would throw on. | Gate models the producer, not the consumer | The old regex's `\s*` tolerance was carried into the rewrite without asking what the engine does with whitespace. | The scan reads verbatim; `RacesEmittedByXslt_PaddedRace_IsReadVerbatim`; the testing-qa recurrence carries the point. |
| R2 | MED (Standards) | Rows C3 and C4 above said "Corrected" and "the CHANGELOG says so" before the CHANGELOG edit existed. | Summary ahead of its evidence | The CHANGELOG was left for after the review, and the rows were written in the finished tense. | The edit lands in the same commit; `evidence-over-claims.md` C1 already names the trap. |
| R3 | LOW (Standards, Engine) | C4 cited `Hero.cs:210` and `:220`, the `SaveableProperty` ids rather than lines; C2 named "character import" without its path; Codex's vanilla line aside was called a miscount. | Citation | Numbers copied from the attribute, and a verdict on Codex's lines given before reading them. | Corrected in place. |
| R4 | LOW (Completeness) | Nothing told a stylesheet author about the gate's rule. | Missing trigger | The gate was documented in tests and RCAs, not in the path rule an author reads. | One paragraph in `.claude/rules/xslt.md` "The gate". |

A convergence pass over those fixes found four more text slips, all corrected before the commit:
a line count for Codex's JSON input written from memory ("four-line"; it is 18), a quotation of
the old comment missing a word, a CHANGELOG sentence that counted the writers differently from the
REVIEW-LOG table, and a code span split across a line in `xslt.md`. The first two are
`evidence-over-claims.md` C's own trap, a fact stated without reading its source this turn.

The design lens proposed checking `lords.xslt`'s transform output over the installed `lords.xml`
instead of scanning stylesheets. Mike kept the scan: it covers every stylesheet in about 25 ms,
while the transform covers the one stylesheet over NPCCharacters today and adds about 3.3 s to each
full suite run.
