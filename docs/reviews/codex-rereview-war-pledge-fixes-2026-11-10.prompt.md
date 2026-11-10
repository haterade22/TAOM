TAOM is a Lord of the Rings total conversion for Mount & Blade II: Bannerlord (.NET Framework 4.7.2). This is a second, narrow pass over issue #1113 (War Pledge, Slice 1), uncommitted in this working tree (branch `feat/war-pledge`, HEAD `8841aac99`). Your first pass is in `docs/reviews/raw/codex-prereview-war-pledge-2026-11-10.md`, and the findings of every reviewer are in `docs/reviews/rca-war-pledge-2026-11-10.md`. This is a read-only review: do not edit, stage, build or commit anything.

Read engine code from the installed v1.5.7 DLLs: `pwsh tools/taom-src.ps1 path <Type>` prints the path of the decompiled file.

## What changed since your first pass

Review only these changes. Everything else was reviewed and is unchanged in behaviour.

1. **Eligibility by culture** (your F1, accepted in part). `Main/Features/WarPledge/WarPledgeService.cs`, `CanJoin`: after the id-only check, a kingdom that `_alignment.ResolveSide(id, _alliance.GetKingdomCultureId(id))` puts on a side is refused. The peace and the declarations still read `GetKingdomSide` (id only), by decision: the Full War rules read ids only (`DiplomacyService.IsWarAllowed`), so a player-founded realm gets no war and no peace from a join. Do not re-litigate that decision; check that the code and the tests do what it says.
2. **The pledged realm declares its own wars.** `TryJoin` no longer calls `IWarOfTheRingService.ReconcileDeclaredWars()`. After the peace loop it walks the live kingdoms, and for each one whose kingdom side is opposed and for which `_wotr.ShouldBlockPeace(id, other)` holds, it calls `_alliance.DeclareWar(id, other)` when `HasDeclaredWar(id, other)` is false, then counts `expected` and `wars`. The reason: v1.5.7 `AllianceCampaignBehavior.ApplyBrokenAlliancePenalty` charges a broken alliance to the ruler of the first faction of the declaration, and the reconcile declares in kingdom-list order. `WarOfTheRingBehavior.OnSessionLaunched` still calls the reconcile on every authority session launch, which retries a declaration that failed.
3. **The AI's wait survives a throw.** `RunDailyCourting`: the loop skips a realm that already holds a pledge before the call, and sets `NextCourtingDay` in a `finally` when the realm holds a pledge after the call.
4. **The courting side is on the status object.** `WarPledgeStatus.CourtingSide` is computed by the service's `LosingSide`; `Cheats/WarPledgeCheatReport.cs` prints it.
5. **Text and data.** The two notice sentences are present tense (`Main/Adapters/WarPledgeWorldAdapter.cs`, the two English rows and the 24 seeded rows). The MCM name and hint of `WarPledgeEnabled` are keyed (`Main/Features/TaomSettings.cs`, two English rows, 24 seeded rows). Comments in `WarPledgeStore.cs`, `DiplomacyService.cs`, `FireAndTributeService.OwnSide.cs` and two test files state the thread facts as Lens 2 and you found them.
6. **The analyzer.** `tools/analyze_war_ledger.py`: one pledge per kingdom (the last line read, `pledge_last`); a side's first capital loss is updated on every observed loss; a note under net change and in `--compare` when a log holds a pledge. `tools/tests/fixtures/war_ledger_v1.txt` holds one pledge line that `WarLedgerFormatterTests` writes and the analyzer test parses. `tools/ingame_checks/1113.toml`: the pass pattern ties `wars` to `expected` with a back-reference.
7. **Tests.** `TAOM.Tests/Features/WarPledge/WarPledgeServiceTests.cs` and `WarPledgeFixture.cs` (a culture table, the declarer of each war, a peace that throws), `TAOM.Tests/Features/Execution/SidePairScanTests.cs` (one allow-list row for the two-argument `ResolveSide` call in `CanJoin`).

## Questions: CONFIRM or DISPUTE each, with file:line evidence (v1.5.7 decompile for engine claims)

1. **The declaration loop.** Is the set of wars the join now declares the same set the reconcile would have declared for the pledged realm (`WarOfTheRingService.DeclareHostileTierWars` with `ShouldBlockPeace` as the filter), apart from `autoWarBetweenHostileTiers`, which the join deliberately does not read? Does TAOM's own `DeclareWarAction` prefix (`GetWarVeto`) pass each of these declarations? Does declaring with the pledged realm as the first faction change anything else in v1.5.7 `DeclareWarAction.ApplyInternal`, `FactionManager.DeclareWar` or their listeners, compared with the other order (political stagnation, notices, kingdom decisions, call-to-war proposals)?
2. **The penalty.** Does `ApplyBrokenAlliancePenalty` now charge the pledged realm's ruler in every case, including a pair whose war the session-launch reconcile declares later in list order?
3. **The `finally`.** Can `NextCourtingDay` be moved for a realm this call did not pledge? Can a throw before the store write move it? Is anything else skipped when `TryJoin` throws (the ledger line, the check line, the notice), and is that state recoverable at the next session launch?
4. **Eligibility.** Is there a kingdom that should be able to pledge and now cannot (a lean-table realm whose culture id has a side in `alignment.json`)? Check the ten lean ids' cultures against `Main/_Module/ModuleData/execution/alignment.json` and the kingdom data.
5. **The analyzer.** Does `pledge_last` behave under the load rewind and exact repeats as the chronicle's rule does? Does the capital-loss change alter any result for a log with no pledge?
6. **Tests.** Would each new test fail if its behaviour broke? Name any behaviour of these seven changes that no test pins.

## What NOT to flag

- Anything outside the seven changes, the decisions recorded in the RCA's "Not applied" section, the settings counts, the English text in the language rows, balance.

## Output

Report P1, P2 and P3 findings with `file:line` evidence, and the v1.5.7 decompiled file and line for every engine claim. Mark a claim UNVERIFIED when you cannot read the evidence.
