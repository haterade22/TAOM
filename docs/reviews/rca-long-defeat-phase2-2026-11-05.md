# RCA: The Long Defeat Phase 2 (#1079), 2026-11-05

Findings from the plan check and from each slice's review, with why each was missed and what now prevents it. The plan check (101 engine claims against the installed v1.5.6 engine: 38 held, 55 partly, 5 unsettled, 3 refuted) is recorded in `docs/features/long-defeat/phase2-plan.md`, which carries its corrections.

**Slice 1, foundations.** Tier 2 lenses 5 and Tooling.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The default seed is the clock's ticks, about 6.4e17, so a JavaScript leaderboard bot would read every board report's seed rounded and fail its replay rule | Value crosses a language boundary | The plan checked the seed's round trip through C# and Python only, and the bot's language was never part of the trace | Seed capped at 2^53-1 in `RunOptions.MaxSeed`, the arm parser and the wrapper; the leaderboard plan states why |
| 2 | LOW | The judge held `seed` and `startWave` to the report only when armed, so no unattended run proved the clock seed reached the report | Gate proves less than it says | The rule was written for armed values, and a default is a value too | The judge checks both in every run |
| 3 | LOW | The judge read `owner_missing` only from a log token nothing writes, and ignored the report's total | Gate proves less than it says | The token's emitter belongs to Slice 9, so the check had no source | The judge reads `poison.ownerMissing` from the report |
| 4 | LOW | The blow-owner gate credited a one-line `=>` member that kills to the member above it | Parser edge case | The member detector skipped lines that end in `;` | Expression-bodied members count; three tests pin it |
| 5 | LOW | The gate and the doc said `Fell` picks its owner on purpose, but `KillAgentCheat` uses the victim when no player stands | Doc claims more than code | The claim was written from intent | Text corrected; Slice 9 fixes and pins `Fell` |
| 6 | LOW | The leaderboard plan still named three hashed files after the hash grew to six | Stale doc | The edit touched the rule, not its other mentions | All mentions corrected |

Not applied: the armed class is not yet compared with the report's class, because no code fills that field before Slice 5; the Slice 5 row now requires it.
