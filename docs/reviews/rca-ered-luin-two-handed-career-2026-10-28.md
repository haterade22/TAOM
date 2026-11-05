# RCA: Ered Luin two-handed career deep review, 2026-10-28

## Top line

Mike asked for Ered Luin's careers to be one per fighting style (#1045): one-handed and shield, two-handed,
ranged. The change adds `CareerArchetype.TwoHanded`, replaces `shieldguard_of_belegost` with the Mattock
Hewer of Belegost (`hewer_of_belegost`), and gives it a kit of a two-handed axe and a throwing axe with no
one-handed weapon, spear or shield.

`/deep-review` ran at tier 1, because the change adds a case to the ability executor registry and renames a
career id that saves hold. Codex ran at `ultra` with nine questions written for the change, beside one wave
of four lenses (5 Data flow, 2 Engine compatibility, 7 XML, Tooling), each with its own question list.
Codex reported 1 HIGH and 2 LOW. The data-flow lens showed that no save can hold the old id, so the HIGH
describes a defect that predates the change and is now #1046. The lenses reported 2 MEDIUM and 7 LOW. One
LOW was a design call for Mike, who chose to give Ered Luin's mattock line throwing axes; that removed the
borrow from Erebor and the C# test exemption Codex found too wide. A convergence pass over the fixes found
6 LOW (four doc sentences, two untested code paths). Every confirmed finding is fixed in this change, except
two that are follow-ups with an owner.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH as reported, not reachable | A save holding `shieldguard_of_belegost` loads with no crash but an empty career: no tree, no ability, no passives, and a warning logged every frame in battle (Codex 1, lens 5 F1) | Renamed persisted id | The rename was checked against the data and the code, not against what a save can hold | Lens 5 showed no save can hold it: the only session on the 01:17 build chose the career at 07:09:16 and logged no `SyncData`, the newest save is from 2026-10-26, and no release tag contains Ered Luin. No migration. The unknown-id handling itself predates this change: FOLLOW-UP #1046. Lesson: check whether a save can hold a renamed id before writing a migration |
| 2 | MED | `generate_career_kits.py` crashed with `KeyError` on a roster that carries a slot its layout lacks (an `Item2` copied into a two-handed skeleton) instead of refusing it (Tooling F1, lens 5 F12) | Unhandled input after a schema change | The `None` slot was added to the layout, but the lookup that assumed every roster slot is in the layout was not revisited | `CareerKitError` naming the slot; test `test_a_roster_with_a_slot_its_layout_lacks_fails_loudly`. `main()` also catches `ET.ParseError` and `OSError` (exit 2, not a traceback read as drift); test `TestMain` |
| 3 | MED | `verify()` walked only the slots present in a roster, so a roster that lost a slot passed it, and the rewritten missing-slot refusal had no test (Tooling F2) | One-directional gate | The refusal was rewritten for the four-slot kit and tested on the accepting side only; the blind spot in `verify()` predates the change | `verify()` reports a slot the kit expects and the roster lacks; tests for both refusals, each proven by a mutation |
| 4 | LOW | The C# exemption in `StarterKitCoverageTests` accepted any item Erebor's troops carry in a two-handed kit's `Item1`, armour included (Codex 2) | Test looser than the rule it mirrors | The C# test cannot see item classes, so the exemption mirrored the borrow by culture only | Removed with the borrow (finding 8); the test is back to its HEAD form and passes |
| 5 | LOW to MED | No test built the real ability executor registry, so deleting the new `case CareerArchetype.TwoHanded:` would leave every test green while the Hewer's ability did nothing (lens 5 F2) | Composition untested | The archetype map was tested; the switch that consumes it was tested only with substitute executors | `CareerAbilityRegistryWiringTests` calls the production `BuildAbilityEffectRegistry`; RED with the case removed. **Repeat** of the 2026-09-28 lesson "a test builds the REAL registry" |
| 6 | LOW | The Hewer's tier-1 group kept the name "Hammer of the Deeps", the same as the Warden's on the same career screen (lens 7, 1) | Cloned wording | The clone's remap table lost the row that renamed the group, and only a whole-screen read sees two careers side by side | "Mattock of the Deeps" through a `CAREER_TEXT_REMAP` row; tests that the Hewer's group names are unique and that the shipped Hewer equals a fresh `build()` |
| 7 | LOW | The archetype alternation was written in three places, and `verify()` skipped a `player_career_` roster whose archetype matched none of them (Tooling F3) | Duplicated list | Each regex was edited by hand for the new archetype | `ARCH = "\|".join(LAYOUT)` feeds both regexes; `verify()` reports an unmatched roster; tests for a `berserker` roster and a clean four-slot two-handed roster |
| 8 | LOW, Mike's decision | The throwing axe was a vanilla item that Erebor's troop data first carries at level 36, on a noble line; it reached the kit through a borrow from Erebor and the pick rule's last-resort branch, against the rule "a career never starts better equipped than the troops it recruits" (lens 7, 2) | Fallback hides a rule break | No Ered Luin troop throws; the borrow made the kit pass, and nothing reports a pick made by the fallback branch | Mike chose to give the mattock line the throwing axe from the Mattock Hewer (level 16) up; `THROWN_FROM` and its exemption are deleted. Lesson: a new slot class needs a troop of the culture that carries it |
| 9 | LOW | Stale text: the in-game check asked for a one-handed axe; roster counts, "five slots", "four rosters", "infantry and ranged only" and "50 careers mapped to 3 archetypes" in docs and comments; the convergence pass then found four more doc sentences (the borrow, the retired Shield-guard, the id rationale, the how-to) (all lenses, convergence pass) | Docs after a design change | The design changed twice in one session (sidearm, then borrow, then troop axes), and each round updated the lines it searched for | All corrected; the convergence pass reads every changed sentence against the final data |
| 10 | LOW, deferred | Every cloned career, the Hewer included, shows no ability icon, because the screen builds the icon name from the template id (lens 5 F11) | Art binding by id | Predates this change | Follow-up closed 2026-11-04 (4d9ae0e8a) no issue: the same gap as row 15 of [rca-ered-luin-2026-10-27.md](rca-ered-luin-2026-10-27.md), a C# change across 30 careers that Mike has not scheduled |

Not applied: Tooling F4 (a depth limit on the borrow) became moot when the borrow was deleted.

## Root-cause pattern

Findings 2, 3, 4 and 7 come from one move: the kit tool's layout gained a shape it had never had (a slot
of `None`, and a borrowed source), and the code around the layout kept assuming the old shape. Each
assumption sat in a different function (the slot lookup, `verify()`, the regexes, a C# test), so changing
the layout in one place could not find them. A tool's data model change is a search for every reader of
the model, not an edit at the point of use.

Finding 8 shows the same thing at the design level: the borrow made a rule break look like a pass.

## Why each agent missed these

- **The implementer** wrote the change and its tests from the brief, which described the new layout but
  not the readers of the old one. It ran the tests it changed; a mutation run was not part of the brief.
- **Codex** found findings 1 and 4 and confirmed the engine facts the kit depends on (`Equipment.FillFrom`
  copies all 12 slots; crafted items have difficulty 0). It ran no mutation, so it did not see 2, 3 or 7.
- **Lens 5 (Data flow)** found 5 and the reachability that settles 1, by reading the debug log and the
  save folder as well as the code.
- **Lens 2 (Engine)** found no defect: it proved the starting weapon, the stack and the skill gate.
- **Lens 7 (XML)** found 6 and 8 by reading the career screen and the kit as a player meets them.
- **Tooling** found 2, 3 and 7 by mutating a scratch copy of the tool and running its tests.

## Process notes

- At 08:09:14 something in the main checkout switched it from `feat/ered-luin-two-handed-career` to
  `bannerlord-1.5.x` and pulled `f4d3ff6e6`, while this work was uncommitted. The work rode along with no
  conflict. This is the second time in two days (row "Process notes" of the 2026-10-27 RCA).
- Codex's `gh issue view 1045` failed inside its sandbox (the GitHub CLI could not read its config); the
  prompt carried the design, so the review did not depend on it.

## Follow-up

- #1046: the career system leaves an empty career for a saved id the data no longer defines.
- The ability icons of the 30 cloned careers (finding 10) stay a known gap in
  [ered-luin.md](../features/ered-luin.md) "Known gaps".
