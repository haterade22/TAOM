TAOM is a Lord of the Rings total conversion for Mount & Blade II: Bannerlord (.NET Framework 4.7.2). You are reviewing an uncommitted change for issue #1113 (War Pledge, Slice 1) in this working tree, a sibling worktree on branch `feat/war-pledge` at HEAD `8841aac99`. This is a read-only review: do not edit, stage, build or commit anything.

## Engine version and base (read this first)

- The installed game is Bannerlord v1.5.7 and the worktree pins v1.5.7 (`.claude/pinned-game-version.txt`). Read engine code from the installed DLLs: `pwsh tools/taom-src.ps1 path <Type>` prints the path of the decompiled file in its v1.5.7 cache.
- Trunk (`bannerlord-1.5.x`) has moved 9 commits past this base, to `335d6a65d`. One of them, `85e8788dd`, is Kingdom Allegiance (#1111): AI clans never join a realm of the opposed side (`TaomDiplomacyModel` join scores, `FactionSides.OfOwnClan`, `KingdomAllegianceService`). This change will be rebased onto trunk before it is committed. Read that commit with `git show 85e8788dd` and say how the two features interact. The rebase will also move three pinned settings counts by one more (530 total, 495 in `TaomSettings`, 338 relevant); do not report those numbers.

## The request and the maintainer's decisions

A community member proposed a lasting way for a Neutral realm to join the War of the Ring: the losing side courts a Neutral realm that leans its way. The maintainer approved this plan on 2026-11-10:

1. Slice 1 is the AI only. The player's persuasion, rumours, stalling and turning a realm are later slices.
2. A realm that joins is a member of that side for good: it is Hostile to every realm of the other side, and those wars are locked by the existing Full War rules. A static `Permanent` row in `diplomacy.json` wins.
3. A pledge is possible only in the Full War phase. No new `WarPhase` value.
4. A realm cannot pledge to a side that holds a realm it has a static `Hostile` row with (Umbar and Gondor).
5. The AI never pledges a realm a player rules, and adds at most one realm per 30 days.
6. Its own MCM switch, default on. Leans live in JSON, not in MCM.
7. Leans: Evil for Khand (`battania`), Rhudaur, Umbar, Shaghana, Abanissa; Free for Cardolan, Dorwinion; true neutral for Anduin Vale, Angaladh, South Rhovanion.

Do not re-litigate these seven decisions.

## Implementer's design choices (open to challenge)

- The side override is in `AlignmentService.GetKingdomSide` only, never in the lookup `GetCultureSide` shares, because `cardolan` and `battania` are both a kingdom id and a culture id.
- The tier override is in `DiplomacyService.GetRelationshipTier`: `Hostile` when either realm of the pair has a pledge and `FactionSideRules.AreOpposed` holds for the two kingdom sides. The existing `ReconcileDeclaredWars` then declares the wars and `ShouldBlockPeace` locks them, with no change to either.
- The join order is: write the pledge, make peace with every live realm of the new side, call `ReconcileDeclaredWars()`.
- The measure of "losing" is the momentum balance, `IMomentumQueryService.SliderValue` (-100 to +100, positive when the Free Peoples lead), at 25 or more against a side. The AI's courting needs Momentum Tracking on.
- The AI courts only realms that lean to the losing side, in the order of the lean table. It never courts a true neutral or a realm that leans to the other side.
- The first courting day is 30 days after the first Full War day the daily step sees with the switches on. The next courting day is saved.
- The MCM switch stops new AI joins only. A realm that pledged stays pledged, and the console command ignores the switch.
- `WarPledgeStore` is an optional constructor argument of `AlignmentService` and `DiplomacyService` (default null), so that tests and other worktrees that build them with the old argument list still compile. The container passes the store (`WarPledgeWiringTests`).
- `WarPledgeStore` never changes a map after it is published: each write builds a new map and swaps a volatile reference. The reason is `UnlookedForAidWiringTests.TheAlignmentReadPath_OnlyReadsItsMap`: a side is read on the party AI worker.
- The player is told by `MBInformationManager.AddQuickInformation` and a message-log line, not by a popup.
- Console: `taom.print_war_pledges` (read-only) and `taom.add_war_pledge <kingdom id> <free|evil> [confirm]` (dry run without `confirm`; it may pledge a realm a player rules).
- The `[Check]` line's key is the kingdom id.

## Files in scope

Show a modified file's change with `git diff HEAD -- <path>`; read a new file whole.

New:
- `Main/Features/WarPledge/`: `WarPledgeStore.cs`, `WarPledgeService.cs`, `WarPledgeBehavior.cs`, `WarPledgeConfig.cs`, `WarPledgeConfigProvider.cs`, `WarPledgeSettingsProvider.cs`, `WarPledgeIoC.cs`, `WarPledgeModule.cs`, `Cheats/WarPledgeCheats.cs`, `Cheats/WarPledgeCheatReport.cs`
- `Main/Adapters/IWarPledgeWorldAdapter.cs`, `Main/Adapters/WarPledgeWorldAdapter.cs`
- `Main/_Module/ModuleData/diplomacy/war_pledge.json`
- `TAOM.Tests/Features/WarPledge/` (ten files)
- `docs/features/war-pledge.md`, `tools/ingame_checks/1113.toml`

Modified:
- `Main/Features/Execution/AlignmentService.cs`, `Main/Features/Diplomacy/DiplomacyService.cs`, `Main/Composition/FeatureModules.cs` (one line), `Main/Features/TaomSettings.cs` (the `WarPledgeEnabled` block only)
- `Main/Features/WarChronicle/Ledger/WarLedgerService.cs` and `WarLedgerFormatter.cs` (the `ev=pledge` line)
- `Main/_Module/ModuleData/taom_module_strings.xml` and the 12 `Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml` files (two rows each)
- `TAOM.Tests/Features/UnlookedForAid/UnlookedForAidWiringTests.cs` (the alignment read-path gate), `TAOM.Tests/Features/WarChronicle/Ledger/WarLedgerServiceTests.cs`, `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`, `TAOM.Tests/Features/ServerSettings/ServerSettingsSchemaTests.cs`
- `tools/analyze_war_ledger.py`, `tools/tests/test_analyze_war_ledger.py`
- `docs/features/coop-interop.md`, `bannerlord-together-compat.md`, `dev-console.md`, `war-chronicle.md`, `war-of-the-ring.md`, `diplomacy.md`, `alignment-aware-execution.md`, `docs/reference/feature-map.md`

Context outside the diff: `Main/Features/Diplomacy/WarOfTheRingService.cs` and `WarOfTheRingBehavior.cs`, `Main/Features/Diplomacy/Models/TaomDiplomacyModel.cs`, `Main/Adapters/AllianceAdapter.cs`, `Main/Features/WarOfTheRingMomentum/MomentumEnrollmentService.cs` and `MomentumQueryService.cs`, `Main/Features/FireAndTribute/FireAndTributeService.OwnSide.cs`, `Main/Features/RealmBorders/`, `Main/Composition/ModuleRunner.cs`, `Main/IoC.cs`, `Main/SubModule.cs`.

## Known suspects: CONFIRM or DISPUTE each, with file:line evidence (v1.5.7 decompile for engine claims)

1. **Threads.** Which v1.5.7 and TAOM callers reach `AlignmentService.GetKingdomSide`, `ResolveSide` or `DiplomacyService.GetRelationshipTier` off the main thread (`TaomDiplomacyModel.IsAtConstantWar`, the party AI worker, `TWParallel`)? Is the store's copy-on-write read path safe for each? Is `NextCourtingDay` read or written off the main thread? Can a console command run while a worker reads?
2. **The declaration order.** In v1.5.7, what do `DeclareWarAction.ApplyInternal`, `FactionManager.DeclareWar` and `AllianceCampaignBehavior.OnWarDeclared` do when a join declares up to ten wars in one tick: alliances between the pledged realm and the other side, call-to-war decisions for allies, kingdom decisions, tribute, the player's notifications? Does writing the pledge first avoid the call-to-war decisions for the realm's new side, as the Full War transition does (#772)? What happens to a pair whose stance link does not exist yet (`IAllianceAdapter.HasDeclaredWar` documents that the engine stores it as War with no event)?
3. **Peace with the new side.** `MakePeaceAction.Apply` between the pledged realm and each realm of its new side, from a daily tick: sieges and battles in progress between them, prisoners, the player as a vassal or mercenary of either realm, TAOM's own `MakePeaceAction` prefix. Can any of the three peace-block layers refuse this peace?
4. **Stale war or side state.** After a pledge flips `IsAtConstantWar` for many pairs at once, does any vanilla or TAOM cache go stale: a kingdom's cached at-war list, `FactionManager` stances, `Kingdom.IsAtWarWith` results, the Alignment map mode, War Chronicle baselines and rally tiers, Fire and Tribute targets, the Eye of Sauron, Sworn Anew, tournaments? Name each reader that stores a side or a war flag across ticks.
5. **Session reset and load order.** `WarPledgeBehavior` is built by `ModuleRunner` in `OnGameStart`. Does anything read a side or a tier before its constructor resets the store (a hand-wired behavior's constructor, a GameModel factory)? On a load, is its `SyncData` restored before `WarOfTheRingBehavior.OnSessionLaunched` calls `ReconcileDeclaredWars`? What does a save from before this feature load as? What does a co-op client load?
6. **The daily tick.** Listeners run newest first, so `WarPledgeBehavior.OnDailyTick` runs before `WarOfTheRingBehavior` and the momentum behavior on the same day. Does any step of the join depend on something those two do later that day? Is a repeated day after a load harmless? Is the invalid-clock path right?
7. **Co-op.** The daily step gates on `IsAuthority` and not `ShouldDeferToHost`; the console also on `MayWriteSaveBackedState`. Is that the right gate set for a host, a client, BannerlordTogether and a dedicated server? `WarPledgeWorldAdapter.IsPlayerRuled` treats `PlayerControl.Unknown` as a player: correct?
8. **Kingdom Allegiance (#1111, on trunk).** After the rebase, the pledged realm reads Free or Evil through `ResolveSide`, and its own clans read their people's side through `FactionSides.OfOwnClan`. Does the join veto then push the pledged realm's own clans out, block new clans, or change mercenary scores in a way nobody decided? Does anything in `85e8788dd` construct `AlignmentService` or `DiplomacyService` in a way the optional argument breaks?
9. **Momentum.** After a join the enrollment sweep moves the realm onto a side (`MomentumEnrollmentService`). Does the meter then swing so that the other side courts at once after the 30 days? With Momentum Tracking off, or victory on and the war ended, what does `RunDailyCourting` do? Is `SliderValue` meaningful 30 days into Full War, or can one early battle hold it at plus or minus 100?
10. **The checks before a join.** Can a realm pass `CanJoin` that should not: a player-founded kingdom whose id is not in `alignment.json`, a kingdom being destroyed this tick, a realm whose only opposed realms are eliminated, a rebel or minor faction id? Does the guard on the `Hostile` row read the right tier while other realms hold pledges?
11. **Config and save parsing.** `WarPledgeConfigProvider` with Newtonsoft: a huge number, a float, a null row, duplicate keys in the JSON object, a byte order mark. `WarPledgeStore.LoadFrom` with a kingdom id that contains `=` or `;`. Is any value that reaches a comparison unvalidated?
12. **The in-game manifest and the ledger.** Does `tools/ingame_checks/1113.toml` match the line `OutcomeLog` really writes for `Pass` and `Fail` (read `Main/Core/Logging/OutcomeLog.cs` for how it formats the detail)? Does `tools/analyze_war_ledger.py` hold any metric that assumes a kingdom keeps one side for the whole campaign, now that a kingdom line's `side` can change after an `ev=pledge` line?
13. **Dedicated server (ADR-013).** `WarPledgeWorldAdapter` calls `MBInformationManager.AddQuickInformation` and `InformationManager.DisplayMessage` from core. Does either name a client-only assembly, or throw on a server with no UI?
14. **Tests.** Which decision paths in `WarPledgeService`, `WarPledgeStore`, `WarPledgeBehavior` and the two overrides have no test that would fail if the behaviour broke? Is any test a tautology?

## What NOT to flag

- Files outside the lists above: other sessions own them.
- The seven maintainer decisions.
- English text in the 12 language rows: the translator's `--seed-only` pass wrote placeholders, and the paid translation is tracked in #763.
- The settings counts (see "Engine version and base").
- Balance. The observer runs that tune the threshold are owed and are not part of this review.

## Output

Report P1, P2 and P3 findings. Cite `file:line` for every claim, and the v1.5.7 decompiled file and line for every engine claim. Mark a claim UNVERIFIED when you cannot read the evidence. State explicitly where you disagree with a design choice above.
