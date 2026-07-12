# Adversarial review: CaravanTrade recency-visit-memory fix (issue #335)

You are an adversarial code reviewer. Your job is to find real defects, not to rubber-stamp. Read the TAOM source and the decompiled installed engine before asserting anything. State CONFIRMED or DISPUTED for each Known Suspect with evidence (file:line). Use the FINDINGS format at the end.

## Feature (1-2 lines)

TAOM (Bannerlord total-conversion mod, installed engine v1.4.7) `CaravanTrade` fix. Bug: AI caravans leave a town and immediately return instead of circulating. Fix adds a per-caravan recency memory that penalizes just-visited towns, and removes the home town's distance-reweight exemption.

## Scope -- review ONLY these files (the change under review)

New:
- Main/Features/CaravanTrade/ICaravanVisitMemory.cs
- Main/Features/CaravanTrade/CaravanVisitMemory.cs
- Main/Features/CaravanTrade/CaravanVisitMemoryBehavior.cs
- TAOM.Tests/Features/CaravanTrade/CaravanVisitMemoryTests.cs

Modified:
- Main/Features/CaravanTrade/CaravanTradeService.cs (ReweightTradeScore new signature + body)
- Main/Features/CaravanTrade/ICaravanTradeService.cs
- Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs
- Main/Features/CaravanTrade/CaravanTradeConfig.cs
- Main/Features/CaravanTrade/CaravanTradeConfigProvider.cs
- Main/Features/CaravanTrade/CaravanTradeSettingsProvider.cs
- Main/Features/CaravanTrade/ICaravanTradeSettingsProvider.cs
- Main/Features/CaravanTrade/CaravanTradeIoC.cs
- Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json
- Main/SubModule.cs (ONE AddBehavior line near the CultureMarketplace block)
- TAOM.Tests/Features/CaravanTrade/CaravanTradeServiceTests.cs
- TAOM.Tests/Features/CaravanTrade/CaravanTradeConfigProviderTests.cs

IGNORE all other uncommitted changes in the working tree (troop-weight, shader-precompilation, CLAUDE.md, LESSONS-LEARNED, ReflectionSiteBindingTests) -- they are a separate in-flight effort, NOT part of this review.

## READ FIRST

- docs/features/caravan-trade.md (feature doc; the "Home rubber-band -- FIXED" known-limitation describes this change)
- Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json (the shipped config the doc mirrors)

## Verified vanilla facts (confirm independently against the installed DLLs via ilspycmd; do not take these on faith)

Decompile path base: E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\... ; authoritative signatures via `pwsh tools/taom-src.ps1 path <FullType>` against the installed v1.4.7 DLLs.

- CaravansCampaignBehavior.FindNextDestinationForCaravan is a plain argmax over Town.AllTowns; it excludes ONLY the current parked settlement: `allTown.Owner.Settlement != caravanParty.CurrentSettlement` (~line 923). GetTradeScoreForTown (the patched method) is only called for towns that passed this filter.
- The caravan re-decides its destination while STILL PARKED (HourlyTickParty ~669-677 dereferences CurrentSettlement, then calls ThinkNextDestination).
- MobileParty.LastVisitedSettlement is set ONLY on settlement ENTER (MobileParty.cs:602, inside the CurrentSettlement setter's non-null branch) and never cleared on leave. So while parked, LastVisitedSettlement == CurrentSettlement.
- GetTradeScoreForTown real signature (private, single overload): `private float GetTradeScoreForTown(MobileParty caravanParty, Town town, CampaignTime, float, bool, out MobileParty.NavigationType, out bool)`. Home gravity `num5 = 1 + elapsedDays*0.1*(elapsedDays*0.1)` is folded into the returned score upstream; the TAOM reweight only rescales the distance component, so num5 survives.
- DefaultClanFinanceModel.AddIncomeFromParty pays the caravan owner `(PartyTradeGold - 10000)/10` on the finance tick with NO CurrentSettlement==HomeSettlement gate -- i.e. caravan income is NOT home-gated.
- CampaignEvents.SettlementEntered = IMbEvent<MobileParty, Settlement, Hero>; CampaignEvents.MobilePartyDestroyed = IMbEvent<MobileParty, PartyBase>.

## Known Suspects -- CONFIRM or DISPUTE each with evidence

1. INERT-PENALTY REGRESSION (highest priority). The OLD anti-shuttle penalty was inert: it keyed on LastVisitedSettlement, which equals the parked current town at decision time, and vanilla already excludes the current town from candidates -- so it never fired on a selectable town. The NEW design records the last 4 ENTERED towns per caravan (CaravanVisitMemory, depth 4) and penalizes by recency. QUESTION: at the parked decision, is the genuinely-previous town (the one the caravan came from) actually a SELECTABLE candidate that receives a real penalty, or does the recency penalty ALSO only ever land on the excluded current town (i.e. is the fix still inert)? Trace: caravan enters B, enters C (now parked at C), re-decides. What is the ring, what rank is B at, is B in the candidate set, does B get factor < 1? Prove it holds against the engine's parked-decision timing.

2. SINGLETON LIFETIME. CaravanVisitMemoryBehavior WRITES visits; the GetTradeScoreForTown hook READS the recency factor. Both must bind to the SAME ICaravanVisitMemory instance. Check CaravanTradeIoC.cs -- is ICaravanVisitMemory registered Reuse.Singleton (not Transient)? A Transient reg makes the behavior write to one instance and the hook read an empty other -> the fix silently no-ops. Confirm the DryIoc lifetime and that both resolutions go through the one container.

3. HOME-EXEMPTION REMOVAL / PAYOUT STARVATION. ReweightTradeScore now distance-compresses the home town like any other (homeDistanceReweight default true) instead of returning its raw score. Does this starve caravan owner income by making caravans never return home? Verify DefaultClanFinanceModel.AddIncomeFromParty is NOT home-gated (income paid wherever the caravan is), and that num5 home-gravity (upstream in rawScore, uncapped quadratic) still eventually wins the argmax to bring the caravan home. If income IS somehow home-gated, this is a HIGH finding.

3b. RECENCY vs num5 -- does the recency penalty on home ever PERMANENTLY suppress the home return? The recency factor decays out of the ring after 4 other town visits and is floored strictly positive; num5 grows unbounded. Argue whether home return is guaranteed or can be starved.

4. STRANDING. The recency factor must be a strictly-positive multiplicative floor, never a hard exclusion. Read CaravanVisitMemory.GetRecencyPenaltyFactor: can it return 0 (or negative)? MinRecencyFactor floor value? A rawScore>0 multiplied by a factor in (0,1] stays >0 -> still beats a non-candidate (0/-1) in the argmax. Confirm a caravan in a sparse 2-town or all-recently-visited region cannot be stranded (return null forever). Also confirm K=4 depth cannot exceed the reachable-town set in a way that strands.

5. NaN POLARITY (engine-float gates). In ReweightTradeScore: `if (FiniteFloatValidator.IsFiniteInRange(recencyPenaltyFactor,0f,1f)) result *= recencyPenaltyFactor;` -- a NaN factor must be IGNORED (never multiply by NaN). The `days > 0f` gate must keep NaN days out of Math.Pow. In CaravanVisitMemory: a NaN/out-of-range strength must return factor 1.0. Confirm all three polarities (NaN must FAIL the gate, not pass into the active branch).

6. MEMORY LEAK. CaravanVisitMemoryBehavior.OnMobilePartyDestroyed must evict (_memory.Clear(party.StringId)) so the Dictionary<string,List<string>> doesn't grow unbounded over a long campaign. Confirm Clear is keyed by the SAME id (StringId) as RecordVisit -- a key mismatch = eviction never matches = leak. Confirm the per-caravan List is bounded to depth 4.

7. PLAYER-SCOPE ROUTING. The recency factor must flow THROUGH ReweightTradeScore's IsActiveFor(isPlayerCaravan) gate, not be multiplied around it in the hook. With ApplyToPlayerCaravans=false, a player caravan must get NO penalty. Confirm the hook passes the factor as a parameter and the service applies it only after the IsActiveFor early-return. Also: the behavior records player caravans unconditionally (intentional -- confirm it is harmless because recording without an active consumer changes nothing).

8. CONFIG. antiShuttlePenalty default changed 0.35 -> 0.5 and repurposed as recency strength; homeDistanceReweight added (bool, default true). Confirm: CaravanTradeConfigProvider copies homeDistanceReweight in its validated clone; antiShuttlePenalty keeps its [0,1] FiniteFloatValidator gate; the service no longer reads AntiShuttlePenalty (moved to CaravanVisitMemory) -- no dead read; the JSON, the DTO default, and the doc config table agree (0.5 / true).

## Also check what the 5-agent deep-review may have missed

- Master-toggle fold: enabled=false must yield EXACT vanilla score (IsActiveFor is the first line of ReweightTradeScore).
- The behavior is actually AddBehavior'd in SubModule.cs (else RegisterEvents never runs, memory stays empty, hook always sees factor 1.0 = inert).
- RemoveAt(0) ring trim + consecutive-same-town collapse in RecordVisit: any off-by-one that drops the wrong town or lets the ring exceed depth.
- Recency rank math: GetRecencyPenaltyFactor uses the MOST RECENT occurrence of a revisited town; verify the loop direction and rank formula (rank 0 = newest).

## REQUIRED OUTPUT SECTIONS

1. VANILLA CODE -- paste the decompiled snippets you actually verified (FindNextDestinationForCaravan filter line, GetTradeScoreForTown signature + num5, DefaultClanFinanceModel.AddIncomeFromParty, LastVisitedSettlement setter).
2. KNOWN SUSPECTS -- CONFIRMED/DISPUTED per suspect (1-8) with evidence.
3. CONFIG CROSS-REFERENCE -- JSON vs DTO defaults vs doc table vs consumer reads.
4. FINDINGS -- each: severity (HIGH/MED/LOW), file:line, what is wrong, why, minimal fix. If none, say so explicitly per suspect.

## QUALITY GATES

- Do not flag vanilla-matching behavior as a bug.
- Do not assume kingdom/culture IDs; not relevant here (no IDs in this change).
- If you cannot verify a claim against the installed DLLs, say UNVERIFIED rather than guessing.
- The single highest-value questions are Suspect 1 (is the fix actually non-inert?) and Suspect 2 (singleton lifetime) -- a wrong answer on either means the entire fix silently does nothing. Spend your budget there.
