# Codex pre-review: Sworn Anew sides a captured lord by their own clan (#1114)

You are a read-only reviewer. Do not edit files, do not run git commands that change state, do not build into the game. Report findings only.

## Repository and scope

Repository: E:/repos/TAOM, branch bannerlord-1.5.x, base HEAD 99d914b88, uncommitted working tree. Bannerlord v1.5.7 is installed and pinned. Engine source: `pwsh tools/taom-src.ps1 path <Type>`.

Review ONLY these uncommitted changes (`git diff HEAD -- <path>`):

- Main/Adapters/SwornAnewAdapter.cs
- Main/Features/SwornAnew/Models/DefectionFacts.cs
- Main/Features/SwornAnew/SwornAnewService.cs
- TAOM.Tests/Adapters/FactionSidesTests.cs
- TAOM.Tests/Features/SwornAnew/SwornAnewWiringTests.cs
- docs/features/sworn-anew.md, docs/features/kingdom-allegiance.md, docs/reviews/rca-kingdom-allegiance-2026-11-10.md

Other uncommitted files in the tree belong to other sessions; ignore them.

## The change

Sworn Anew (#869, docs/features/sworn-anew.md) lets the player recruit a captured lord into the player's realm. It refuses when the lord's side and the player's side are Free against Evil (`FactionSideRules.AreOpposed`). The lord's side was `FactionSides.OfHero(hero)`: the clan's map faction (the realm) with the realm's culture. It is now `FactionSides.OfOwnClan(hero)`: the clan's own id and culture (`Of(clan)`), or a clanless hero's culture. The player's side is unchanged (`FactionSides.OfPlayer()`, the player clan's map faction).

Maintainer decision (2026-11-10): Sworn Anew and Kingdom Allegiance (#1111) use one definition of a lord's side, the lord's own people. Kingdom Allegiance already uses `OfOwnClan`.

## Questions

1. Trace every value of `DefectionFacts.LordSide` to its consumer (`SwornAnewService.Allows` through `IAlignmentService.ResolveSide(SideIds)`). Does `ResolveSide` on a clan id (not a kingdom id) behave correctly: an unlisted clan id, a clan id listed in `Main/_Module/ModuleData/execution/alignment.json`, a clan id listed as Neutral? Read `AlignmentService.ResolveSide`.
2. Which lords change verdict? Enumerate: a lord whose clan culture differs from the realm's side; minor-faction and mercenary lords (the service refuses them earlier: confirm); clanless prisoners; a clan with a null culture. Is any changed verdict wrong against the rule "good peoples serve only Free or Neutral realms, evil peoples only Evil or Neutral realms"?
3. Do the docs (sworn-anew.md Sides bullet, checklist step 6 and new step 17, kingdom-allegiance.md Known Limits) state the behaviour the code now has? Check step 6: are all starting clans of the Shaghana and Abanissa realms of a Neutral culture (`characters/clans.xml`, `spclans.xslt`)?
4. Are the two new source-text tests meaningful by the two-edit test (break the behaviour: they fail; change internals only: they pass)?
5. Any other TAOM caller that should now agree with this definition and does not? List them as information, do not ask for changes.

## Output

For each finding: severity (CRITICAL, HIGH, MEDIUM, LOW, INFO), file:line, the defect, the evidence you read (quote the line), and a concrete failure scenario. Say UNVERIFIED where you could not read the evidence. End with a one-line verdict.
