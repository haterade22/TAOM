TAOM is a Lord of the Rings total-conversion mod for Mount & Blade II: Bannerlord (.NET Framework 4.7.2, engine v1.5.7). You are reviewing one commit on branch bannerlord-1.5.x (trunk): 5832fc5c6, "fix(balance): v2.0.40 - No forced Dunland-Gondor war, Mordor survival". Read it with `git show 5832fc5c6`. Review only: do not edit, create, move or delete any file, do not run a git command that changes the index, a ref or the working tree, and do not build into the game install. Other sessions are working in this checkout.

## Why this change exists

A player reported that Dunland ends up at war with Gondor in every campaign, that Mordor cannot replace its losses, and that Gondor shows far more strength than Mordor. Issue: https://github.com/haterade22/TAOM/issues/1283.

The commit changes data and the text that describes it:

1. `Main/_Module/ModuleData/diplomacy/diplomacy.json`: the Hostile row between `empire_w` (Gondor) and `empire` (Dunland) is removed. Kingdom ids: `empire` is Dunland, `empire_w` Gondor, `empire_s` Mordor, `vlandia` Rohan.
2. `Main/_Module/ModuleData/configs/battle_balance_config.json` and the compiled defaults in `Main/Features/BattleBalance/BattleBalanceConfig.cs`: the `mordor` survival row (-0.2) is removed and `mirkwood` is added at 0.4.
3. `Main/Features/TaomSettings.cs`: the hint of the MCM switch "Enable Cultural Survival Bonuses" no longer quotes config values.
4. Tests in `TAOM.Tests/Features/BattleBalance/` and `TAOM.Tests/Features/Diplomacy/`, two feature docs, one RCA and one lesson.

## Decisions by the maintainer (do not re-litigate)

- No Isengard-Dunland alliance row is added.
- Dunland's priority target list in `configs/army_targeting.json` is unchanged.

## What to attack

Try to disprove that the commit does what its message says, and look for a reader the builder missed. The evidence rules are in AGENTS.md and `.ai/review-reference.md`: quote the line that produces an effect, give file and line, and mark anything you could not read as UNVERIFIED. Engine signatures and behaviour come from the installed DLLs (`pwsh tools/taom-src.ps1 path <Type>` prints the path of the decompiled file).

1. **Diplomacy.** Find every reader of the relationship tier (`DiplomacyService.GetRelationshipTier`) and every vanilla reader of "at war" for this pair. After the change, which routes can still put Gondor and Dunland at war, or make them allies, in each War of the Ring phase (Peace to day 29, Isengard War from day 30, Full War from day 44, WarEnded)? Is the "Known limitation" paragraph of the commit message complete and correct? Does a save made in Full War before this change load into a consistent state?
2. **Survival.** Find every reader of `CulturalSurvivalBonuses` and of the key `mordor`. With the row gone from the JSON and from the compiled defaults, can any path still apply -0.2? Is the arithmetic in the commit message, in the changelog line of `docs/features/battle-balance.md` and in the test comments correct for the engine's `DefaultPartyHealingModel.GetSurvivalChance`, including the conditions those texts state?
3. **Tests.** For each new or changed test: does it fail when the behaviour breaks, and pass when only internals change? Does a comment or a failure message claim an effect that the code does not produce?
4. **Text copies.** Is any other statement of the old data left in the repo (docs, MCM hints, tooltips, tools, tests): the Dunland-Gondor Hostile pair, the count of Hostile rows, Mordor's -0.2 or "-20%"?
5. **Config rules.** Does the change meet `.claude/rules/config-providers.md` and the "Config ID Cross-Reference" section of `.claude/rules/xml-data.md`?

## Output

Write your report to stdout only. For each finding give: severity (P1 to P3), file and line, the quoted evidence, and what would prove the finding wrong. End with a list of what you checked and found correct, and a list of what you could not verify.
