# Codex fix re-review: Arachnophobia Mode (2026-11-09)

You are the reviewer (`.ai/roles/reviewer.md`). This is a review-only assignment: do not modify,
create or delete any file, do not run a build that deploys (`./build.ps1`), and do not commit. Read
`AGENTS.md`, `.ai/policy.md` and `.claude/rules/troops.md` first. Every claim needs a source location
you read in this run; mark anything you cannot prove UNVERIFIED. Print `git rev-parse HEAD` at the
top of your report.

## The change under review

Uncommitted work in this worktree (`E:/repos/taom-arachnophobia`, branch `feat/arachnophobia-mode`,
base `fc7cd73c4`), issue #1110. A first review ran on 2026-11-09
(`docs/reviews/raw/codex-prereview-arachnophobia-mode-2026-11-09.md`, final report after line 10329).
This pass reviews only the fixes made after it. The fix diff is
`docs/reviews/raw/arachnophobia-fix-2026-11-09.diff` (unified, 27 files, made against a snapshot taken
before the fixes). Read the current files for context; judge the diff.

What the maintainer decided (approved plan): the mode is off by default; troop records stay, because
saves refer to ids; spider troops already in a save stay as warg riders and are not converted; Fords
of War keeps its offer for now; lore text and cobwebs stay. Everything else in the diff, comments, the
hint and the feature doc included, is a claim to test.

Engine signatures and bodies: the installed v1.5.6 DLLs are the authority (`pwsh tools/taom-src.ps1
path <Type>`, or the `ilspy` MCP server). Quote only the short line that proves a claim.

## Questions (give each a verdict with evidence)

1. **Brood disband guard.** `CreatureBandParties.DisbandAll` now passes `party.PrisonRoster.TotalHeroes`
   to `CreatureBanditRules.CanDisbandBrood` instead of `TotalManCount`. Is it safe to destroy a brood
   that holds only regular prisoners (`DestroyPartyAction.Apply(null, party)`, then
   `MobileParty.RemoveParty`)? For a brood holding a hero, does the hero always leave on a later day,
   for an NPC hero and for the player? Is any other state (a quest, a map event, an encounter, co-op)
   reachable that the old guard covered and the new one does not?
2. **Upgrade edges.** `ArachnophobiaService.DropUpgradeEdgesIntoHiddenTroops` no longer skips a hidden
   source troop, so a hidden troop whose only targets are hidden gets an empty `UpgradeTargets` array.
   List each engine and TAOM reader that could meet such an array on these troops (the troops are the
   ones riding `Monster.spider` in `characters/spider_creature.xml`, `characters/creature_bandits.xml`,
   `troops/troops_goblin.xml` and `fords_of_war/fords_troops.xml`). Is any reader unsafe with it, in a
   new campaign, a loaded save with leftovers in parties and garrisons, or a volunteer slot? The new
   comment claims `DefaultAlleyModel`'s `UpgradeTargets[0]` never meets an array this emptied: test it.
3. **The swap stays before the route closing.** A lens proposed closing the routes before the swap. It
   was not applied: the comment at `ArachnophobiaService.cs` (the hidden set line) gives the reason.
   Is the reason sound, and is the new comment true for every failure point in `ApplyForGame`?
4. **The hint.** The new English hint (`TaomSettings.cs`, `taom_module_strings.xml`, and the same text in
   the 12 language files) makes specific promises. Test each against the code: volunteers, upgrades,
   the Elite Emissary, the encyclopedia, the Custom Battle list, broods "most at the start of the next
   day", leftovers "in parties, garrisons, prisons and volunteer slots", Fords of War.
5. **The manifest.** `tools/ingame_checks/1110.toml` gained a second `fail` pattern and two `[[human]]`
   lines. Does `tools/ingame_verdict.py` judge a `result=pass hidden=N sets=0` line as FAIL? Is the new
   header comment about one line per key per process true (`Main/Core/Logging/OutcomeLog.cs`)?
6. **The wiring tests.** Apply the two-edit test to each test in
   `TAOM.Tests/Features/Arachnophobia/ArachnophobiaWiringTests.cs`: which behaviour-breaking edit to the
   pinned source would still pass?
7. **Doc claims.** Test every claim the diff adds to `docs/features/arachnophobia-mode.md` and the counts
   changed in `docs/features/coop-interop.md`.

## Report format

Findings by severity (CRITICAL, HIGH, MEDIUM, LOW), each with file:line, the failing state, evidence
and a suggested fix. Then one verdict per question above. End with what you could not verify.
