# RCA: named companion backstory intro (#1061), 2026-10-29

## Top line

The #1060 review (Elegost) found that no named companion's backstory had ever been shown in game: TAOM
marks every companion met at placement, which closes vanilla's first-meeting chain, and that chain also
reads a null `Hero.Template` for a `heroes.xml` hero. Mike chose option A, a TAOM-owned intro (#1061),
built on Oaths of Service. Lenses 2 and 5 reviewed it: no incompatibility and no data-flow gap, four LOW
findings, all fixed before commit.

## Findings

| # | Sev | Finding | Why missed | Preventive action |
|---|---|---|---|---|
| 0 | HIGH (found in #1060) | The seven backstory strings per companion shipped unreachable since the feature began (2026-04-08) | The feature doc claimed vanilla's flow triggers; the gap was recorded in `wanderers-and-named-companions.md` as an open question, never as a defect, and no in-game check ever ran | The intro; `NamedCompanionShippedDataTests` now gates the four companion files together |
| 1 | LOW | An Evil-side player would hear a Free companion's story end with an offer to join, then be refused on hire | The intro was designed against the companion's state only, not the hire gate the story leads into | `IntroDue` takes `hireAllowed`; the dialogue asks the same Wanderer Allegiance verdict as the hire line; test `IntroDue_HireRefusedByWandererAllegiance_False` |
| 2 | LOW | `IntroDue` tested `isCoopClient` inline while `RecordIntro` used `CoopSessionPolicy.MayWriteSaveBackedState`; a future narrowing of the policy would show the intro without recording it, every talk | Two gates on one fact written two ways | Both use the named policy |
| 3 | LOW | Docs and the class comment said the intro outranks vanilla's greeting; the greeting is on `start` and always plays first, and the intro outranks the `lord_start` replies | The design note named the token, not the line that fills it | Reworded in `oaths-of-service.md` and `OathIntroDialog` |
| 4 | LOW | Docs missed the intro in the component diagram and the feature map; two docs cited v1.5.4 line numbers; the old-save note missed a dismissed companion | Doc sweep scoped to the files the plan named | Fixed; the old-save note states that a dismissed companion tells the story once more |

## Why it was missed

Finding 0 is the pattern from the Cardolan and Rhudaur RCA: a check run once (the original feature's
"vanilla flow triggers" claim) is not a gate. The question sat in `open-questions.md` for months because
nobody owned an in-game check. The intro's docs now name the in-game check as owed, and the companion
data has a test.
