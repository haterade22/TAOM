# Codex pre-review: Hero Defence hero levels and pack rules (2026-11-09)

You are an adversarial reviewer. Review only. Do not edit, create or delete any file. Do not run `dotnet build`, `dotnet test` or `./build.ps1`: a verification run uses this worktree's build output, and the Hero Defence test filter already passed on this tree (423 passed, 0 failed). Reading files, grep, git read commands and short Python one-liners are fine. Return the full report as your FINAL MESSAGE. The dispatcher redirects stdout to `docs/reviews/raw/`; do not write that path yourself.

Feature: Hero Defence is a planned campaign-free mission mode for TAOM (a Bannerlord total conversion), a tower defence where the towers are named heroes (#1106). The previous commit `b3b1bedb7` added its pure core (run state machine, wave generator, config readers), reviewed then. This change adds three pure rules on top of it, still with no engine type and nothing registered in IoC or SubModule:

1. `HeroLevels` and `HeroDefenceRun.TryLevelHero`: hero levels 1 to 20, bought in a pause, with prices and per-level factors from the plan.
2. `PackOrderRule`: whether a creep pack marches (Move) or fights a hero (Charge), from spike run 5 evidence.
3. `PackStallClock` and `HeroDefenceRun.PackStalled`: removal of a pack that makes no progress and lands or takes no blow for 40 s.

READ FIRST:
1. The plan: `C:\Users\mikew\.claude\plans\i-have-a-new-dapper-crayon.md` (sections "How it plays", "How every fight ends", "Heroes" (Levels 1 to 20), "Engine recipes"). Its Gate 1 lines were revised tonight from the spike evidence.
2. `docs/features/hero-defence.md`, sections "Solution Approach", "Director contract" and "Spike results (Gate 1)".
3. `AGENTS.md` and `.ai/review-reference.md` for the project rules.

THE DIFF: `git diff b3b1bedb7 -- Main TAOM.Tests docs` plus the untracked files below.
Changed: `Main/Features/HeroDefence/Domain/DefenceTypes.cs` (`LevelResult`, `DefencePlan.Levels`), `HeroDefenceRun.cs` (`HeroEntry.Level`, `LevelOf`), `HeroDefenceRun.Shop.cs` (`TryLevelHero`, level 1 at purchase), `HeroDefenceRun.Wave.cs` (`PackStalled`), `TAOM.Tests/Features/HeroDefence/Domain/DefenceDriver.cs` (`WithLevels`).
New: `Main/Features/HeroDefence/Domain/HeroLevels.cs`, `PackOrderRule.cs`, `PackStallClock.cs`; tests `HeroLevelsTests.cs`, `HeroDefenceRunLevelTests.cs`, `PackOrderRuleTests.cs`, `PackStallClockTests.cs`, `HeroDefenceRunStallTests.cs` under `TAOM.Tests/Features/HeroDefence/Domain/`.
Reused unchanged: `Vec3f` (`Main/Features/FordsOfWar/Domain/Vec3f.cs`).

KNOWN SUSPECTS (CONFIRM or DISPUTE each, with file:line evidence):
S1. `TryLevelHero` adds a `HeroResets` order for a placed hero. The same pause tick can also hold a `HeroMoves` order (re-placement) or a `HeroSpawns` order (first placement, or `HeroDown` in a pause) for the same hero. Is the director contract for two orders on one hero in one tick defined anywhere? Can an order sequence leave the hero at the wrong post or with the wrong level's numbers?
S2. `HeroLevels` has no reader yet. Which settings still give a wrong result: `MaxLevel` below 1, a negative per-level share (health factor below 0), `PriceGrowth` below 1 (prices falling with level), huge values? `PriceOf` now returns 0 for any non-positive or NaN price; is 0 (a free level) the right fallback, or should the level be refused?
S3. `PackOrderRule.Next`: hysteresis between 12 m and 22 m, the per-pair height check before the minimum, NaN and infinite positions, empty lists, and the immediate Move when no hero is in reach. The plan text said "within 12 m of its lead"; the code keys on the nearest creep (spike BLOCK-O-M1 evidence). Find an input where the rule flaps, never releases, or charges a hero it should ignore. Note what a NaN or zero band value does.
S4. `PackStallClock`: the mark only moves on a gain of one step, so a pack pushed back must pass its old mark again. Is that right for a pack that a hero holds in place but that exchanges blows? For a pack whose progress reading is NaN for its whole life? What does the clock do with time that goes backwards?
S5. `PackStalled(slot)`: uses the wave-limit split (free with a hero standing, a leak with none). A slot is reused by the next pack once empty (`ReleaseNextPack`). Can a late `PackStalled` for an old pack remove the new pack in the same slot? State the director contract this needs (reset or drop a slot's clock when a new batch takes the slot) and whether the doc states it.
S6. Plan conformance of the level numbers: price `price x 0.4 x 1.3^(L-2)`, +10% health, +8% regular damage, +8% ability numbers, -2% cooldown, per level. Check the arithmetic and rounding at levels 2, 3, 10 and 20.

REQUIRED SECTIONS:
1. PLAN CONFORMANCE: each rule above against the plan and the Gate 1 doc section, pass or fail with evidence.
2. ARITHMETIC AND FLOAT PATHS: overflow, rounding, NaN, infinity, negative values.
3. CONTRACTS: what each new public member asks of the future engine director, and whether the feature doc states it.
4. TESTS: assertions that are vacuous, tests that pass for the wrong reason, missing boundary or NaN cases.
5. FINDINGS: each with severity (CRITICAL, HIGH, MEDIUM, LOW), file:line, a concrete failing input, and a fix. Mark each claim observed (you ran or read it) or inferred.

QUALITY GATES: cite file:line for every claim. A claim you could not check is UNVERIFIED, never a guess. Do not report style or naming unless it causes a defect.

Prior review lessons:
SUCCESSES: tracing a rule to every path that reaches it (pinned and drawn culture, the tick that ends a pause) found the last review's defects. Seeded transcriptions found rotation defects.
FAILURES: Codex flagged engine-matching code as bugs; a claim about engine behaviour needs the decompiled line.
