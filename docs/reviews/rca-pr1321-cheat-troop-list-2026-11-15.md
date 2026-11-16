# RCA: review of PR #1321, the cheat menu troop list and party screen

PR #1321 (REDACTEDSTUFF, head 3c204d824) adds two entries to the campaign cheat menu's Troops page: "+10 of any troop
of your faction..." (a searchable list that `CheatTroopList` builds from the culture's lists, the TAOM recruitment
pools, the Elite Emissary offers and the clan templates, along every upgrade) and "Every troop (party screen)...",
which opens vanilla's cheat party screen. With the engine's cheat mode off, the adapter builds that screen by hand
and reads vanilla's list through the private `PartyScreenHelper.GetRosterWithAllGameTroops`. The review ran at tier 1
(engine calls, a pushed game state, reflection): lens 5 (data flow) and lens 2 (engine compatibility), each at the
agent definition's `max`. Codex did not run: it is out of usage (Mike, 2026-11-15).

Lens 2 verified 24 engine members and the PR's claims that the list method exists as described and that pushing a
`PartyState` from a menu option is safe (vanilla does the same from the prison menu). Lens 5 found the DI wiring,
the co-op refusal, the pick re-check and the arachnophobia filter connected.

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The hand-built screen's Done handler only returned true. Vanilla's `OpenScreenAsCheat` binds the private `PartyScreenHelper.DefaultDoneHandler`, which first calls `EndCaptivityAction.ApplyByReleasedByChoice` for the prisoners moved left. A lord released on the TAOM screen left the prison roster with no state change: a prisoner with no captor, whom no escape roll reaches, saved that way (lens 5 item 7, lens 2 finding 1; the session read `PartyScreenHelper.cs:142` and `:701-724`) | Same-name confusion | `PartyScreenLogic.DefaultDoneHandler` returns true and nothing else; the comment cited that one | Fixed: the handler releases the moved prisoners as vanilla does (the taken branch cannot run here: the left prisoner roster starts empty). Comment, interface and doc corrected. Lesson appended to `adapters-taleworlds-api.md` |
| 2 | MEDIUM | The reflection site had no row in the binding gate; the PR's pin test carried only `RequiresGame`, which neither the unit run nor the engine-bump gate selects (lens 2 finding 2) | Gate bypassed | A private test replaced the catalogue row | Fixed: `ReflectionSiteBindingTests` DataRow and a Category B row in `reflection-sites.md`; the pin test is deleted (the gate covers it) |
| 3 | MEDIUM | Trunk changed `Pick` to take an option id (#1297) after the branch was cut; the PR's call has the old form and the merged tree would not compile (lens 5 item 11) | Stale base | The branch predates #1297 | Fixed at the trunk merge: the call passes `taom_cheat_troops_any`, and `CheatMenuWiringTests` counts the third `Pick` call |
| 4 | LOW | The 4 new keys had no row in the 12 language files (lens 5 item 10) | Seed skipped | The branch predates the trunk seed of e4f783717 | Fixed: seeded after the trunk merge. Existing lesson `localization-ui.md`, "Deferring the paid translation never defers the free seed" |
| 5 | LOW | `MenuButtons_CallTheService` does not list the two new service calls (lens 5 item 13) | Wiring test not extended | | Fixed at the trunk merge with row 3 |
| 6 | LOW | "Nothing to do" is the message when the party screen cannot open, and the Troops page text names only recruits of your culture (lens 5 item 12) | Wording | | Not applied: wording only, no wrong state. FOLLOW-UP no issue: reword with the next cheat menu text change |
| 7 | LOW | `VolunteerRecruitmentService.AllPooledTroopIds` is still documented as a test accessor and is now called by production code (lens 5 item 4) | Stale comment | | Not applied: outside the PR's files. FOLLOW-UP no issue: a one-line comment fix, do it with the next recruitment change |

## Why each lens caught or missed these

- Both lenses found row 1 on their own; lens 2 also checked the IL to tell the two `DefaultDoneHandler` methods apart.
- Lens 2 alone found row 2: it knows the binding gate's categories.
- Lens 5 alone found rows 3 to 7, by a merge dry run against trunk and by tracing each key and test.

## In-game checks owed

`cheat-menu.md`: with the engine's cheat mode off, capture a lord, open "Every troop (party screen)...", move him left,
press Done; expect the release notice and the lord free afterwards. Then pick a top-tier troop from "+10 of any troop
of your faction..." as Gondor and as one reused-culture realm (Rohan).
