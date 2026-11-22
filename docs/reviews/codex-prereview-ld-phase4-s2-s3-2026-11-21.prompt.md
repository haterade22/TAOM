TAOM is a Lord of the Rings total conversion for Mount & Blade II: Bannerlord (.NET Framework 4.7.2). You are reviewing an uncommitted change for issue #1079 (The Long Defeat, Phase 4, Slices 2 and 3) in this working tree, a sibling worktree on branch `feat/ld-phase4` at HEAD `5cf79902c` (the tip of trunk `bannerlord-1.5.x`). This is a read-only review: do not edit, stage, build or commit anything.

## Engine version

The installed game is Bannerlord v1.5.7 and the worktree pins v1.5.7 (`.claude/pinned-game-version.txt`). Read engine code from the installed DLLs: `pwsh tools/taom-src.ps1 path <Type>` prints the path of the decompiled file.

## The request and the maintainer's decisions

The Long Defeat is a wave survival mode with no campaign. Phase 4 adds an armoury (the lord buys gear sets), an engineer's yard (he buys an engine at a deployment point), and a death rule (a fallen lord takes over a soldier and rises at the next clear). The plan is `docs/features/long-defeat/phase4-plan.md`. The maintainer told the implementer to build Slices 2 and 3 of that plan first. Decisions that stand:

1. Slices 2 and 3 are pure: domain code, the report, the unattended bot and the Python wrapper. The engine half (the swap, the deploy, the walk, the spawn, the blow) is later slices'.
2. Nothing is for sale yet. `LocationOffer.Offered` stays `(false, false, false)`, so the shipped game must play exactly as before.
3. The Marshal of the plan (D41, D42, Slice 9, rule 24) is dropped: the shop's heroes (#1337) replaced him. A bought hero holds the general's seat while the lord is down (the maintainer, 2026-11-03).
4. The report stays schema 2 and every new field is additive.

Do not re-litigate these four.

## Implementer's design choices (open to challenge)

- `SurvivalRun.Locations.cs`: `TryBuyGear` and `TryBuyEngine` with the result order RunOver, NotOpen, NotOffered, Hidden, (NoSite), NotNear, NotOwnBody, Owned or SiteTaken or SiteLost, NoGold. The near rule reads `RunSnapshot.Lord` (an `LdLordFacts`), which no production code feeds yet, so in the game nothing is near until a later slice wires it.
- A buy is handed to the director on the next tick (`RunEffects.SwapGear`, `DeployEngines`), and that tick starts no wave (`holdStart`), as a fortification buy's does not.
- The bot's errand lives in the run: `TryStartErrand`, a clock counted from snapshot `dt`, one `Teleport` order at 20 s, given up 5 s later with the place left alone until the next wave. `RunEffects.Errand` carries Start, Teleport and End. The director does not act on any of these effects yet, and `LongDefeatDirector.PlayShop` does not dispatch the four new `BotActionKind` values (Errand, EndErrand, Gear, Engine).
- `ShopBot.Decide` gained the place schedule (plan D44). With nothing offered the view's `Locations` is `LocationsView.None` and the bot must decide exactly as before.
- `LordLife` (new) holds the body state Own, Taken or None. With no lord reading in the snapshot (today's game), a second fall before a rise is booked as a soldier's body.
- `SurvivalRun.TickLord` runs at the top of `Tick`, before the defenders-dead test, and uses `_stage` and `_waveInit` from the previous tick.
- The director's respawn at a clear moved: `OnWaveCleared` no longer spawns the lord; `Apply` serves `fx.RiseLord` after the events loop through the new `LongDefeatDirector.RiseLord`, which keeps the old guard (`_run.Result != null || _mainBody != 0` returns) and answers the run with `LordRose`.
- Under autoplay `RunEffects.TakeOverForBot` is set every 5 s while the lord has no body in a wave, and `RunEffects.General` whenever the seat changes. The director ignores both until Slice 4.
- `RunOptions.LordFalls` (a test option) is appended to the co-op session payload (`LongDefeatSessionPayload`) with no version bump. The implementer's reasoning: an old decoder refuses a longer payload at its length check, and a new decoder reading an old payload hits the end of the stream, which the catch turns into a refusal.
- The general's seat goes to the first standing hero in the shop's order (then the plan's free heroes), not purchase order.
- The Python wrapper (`tools/run_long_defeat.py`): two purchase kinds and two gold sinks, rules 22, 23 and 25 (`purchase_problem`, `lord_problem`), `--lord-falls` with `lord_falls_problem`, and `lordFalls` in the armed-fields check.

## What to look for

Known suspects, in order of the implementer's own doubt:

1. Behaviour parity of the shipped game. Find any path where a run with `LocationOffer.Offered` empty and no lord reading plays differently from trunk: an extra held tick, an effect the director now meets, a changed respawn (compare the old tail of `OnWaveCleared` with `RiseLord`, including the order against `RestShare`, `FieldCare`, the sally's `SallyHome`, and the run that ends at that clear), a changed `playerDeaths`, a changed report a judge would now refuse.
2. The co-op run (`Main/Features/LongDefeat/Coop/`): a follower's lord, a host's fall, and the payload. Does `TickLord` or `RiseLord` do anything wrong in a co-op host or follower run, where the lord rules differ?
3. `TickLord` at the top of `Tick`: the one-tick lag on `_stage` and `_waveInit`, the forced fall clock across a wave that begins with the horn, and a run that starts at `start_wave` N with `lord_falls` N.
4. The errand state machine: an errand open when the run ends, when the phase changes by timeout, when two closes happen in one tick, and `_errandEnd` pending while a new errand starts. Can the End order be lost or doubled?
5. Money: every path that spends must book one purchase and one sink, and a refused buy must spend nothing. `Purse.TrySpend` is the only gold test in the two new buys.
6. NaN and infinity at every float gate in the new code (`Within`, the clocks).
7. `LordLife.Observe` against the real `LdLordFacts` the adapter will give (`LongDefeatBattlefieldAdapter.Player.cs`, `ReadLord`): the sally's one-step remount, a hero body the player took (`removal.PlayerBody`), a lord who is present but fading.
8. The wrapper's rules: a report this build writes that `purchase_problem` or `lord_problem` would refuse, or an edited report they would accept. Check the C# report names against the Python constants.
9. Tests that would still pass with the behaviour broken (a test that cannot fail), and behaviour with no test.

## Files in scope

Show a modified file's change with `git diff HEAD -- <path>`; read a new file whole.

New:
- `Main/Features/LongDefeat/Domain/LdLocations.cs`, `SurvivalRun.Locations.cs`, `LordLife.cs`, `SurvivalRun.Lord.cs`
- `TAOM.Tests/Features/LongDefeat/Domain/SurvivalRunLocationTests.cs`, `ShopBotLocationTests.cs`, `LordLifeTests.cs`

Modified:
- `Main/Features/LongDefeat/Domain/`: `SurvivalRun.cs`, `SurvivalRun.Fort.cs`, `RunSnapshot.cs`, `RunEffects.cs`, `RunView.cs`, `RunOptions.cs`, `Purse.cs`, `LdFort.cs`, `ShopBot.cs`
- `Main/Features/LongDefeat/`: `LongDefeatDirector.Effects.cs`, `LongDefeatDirector.Player.cs`, `Autorun/LongDefeatAutorunPlan.cs`, `Coop/LongDefeatSessionPayload.cs`, `Diagnostics/LongDefeatRunReport.cs`
- `TAOM.Tests/Features/LongDefeat/`: `LongDefeatDirectorTests.cs`, `Autorun/LongDefeatAutorunPlanTests.cs`, `Coop/LongDefeatSessionPayloadTests.cs`, `Diagnostics/LongDefeatRunReportFortTests.cs`
- `tools/run_long_defeat.py`, `tools/tests/test_run_long_defeat.py`
- `docs/features/long-defeat.md`, `docs/features/long-defeat/leaderboard-plan.md`

## Output

List findings most severe first. For each: a severity (P1 a defect that changes shipped play, loses data or breaks a co-op run; P2 a defect in the new code a later slice will meet; P3 minor), the file and line, the concrete input or state that shows it, and the evidence you read (quote the line). Say UNVERIFIED for any claim you did not read the source for. End with the list of things you checked and found correct.
