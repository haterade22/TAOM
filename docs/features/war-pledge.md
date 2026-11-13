# War Pledge

## Overview

A Neutral realm can join the War of the Ring for good. In Full War, the side that is behind on the war meter takes one Neutral realm that leans its way. The realm makes peace with its new side and goes to war with every realm of the other side, and those wars are locked the way Gondor's and Mordor's are. A console command does the same for any realm that passes the checks.

This is Slice 1 of #1113: the AI only. The player's persuasion (Slice 2) and the rumours, stalling and turning of a realm (Slice 3) are not built.

## Why This Exists

- **Vanilla behavior:** a kingdom at war pays an ally to join through the call to war (`AllianceCampaignBehavior`, `ProposeCallToWarAgreementDecision`). The war that follows is an ordinary one: nothing locks it, and the called realm can make peace again.
- **TAOM requirement:** a community proposal by Elendil ("War of the Ring Diplomacy") asked for a lasting, lore-friendly way in: the losing side courts a Neutral realm, and it starts with the realms that lean its way. The aim is replayability: different realms join in different campaigns, and a side that is losing can recover.
- **Without this feature:** ten kingdoms read `neutral` in `execution/alignment.json` (Khand, Umbar, Shaghana, Abanissa, Cardolan, Rhudaur, Anduin Vale, Angáladh, Dorwinion, South Rhovanion). None is on the war meter, and nine of them have no `Hostile` row in `diplomacy/diplomacy.json`, so the scripted war of day 44 never reaches them.

## Architecture

### Design Challenge

- **Sides come from one static table.** `AlignmentService` loads `alignment.json` once, and 34 other files in `Main/` call it for a side (counted 2026-11-10; the table below names the search). A join that told each reader would miss one.
- **A kingdom id can also be a culture id.** `cardolan` and `battania` name both the realm and its people. The table is shared by `GetKingdomSide` and `GetCultureSide`, so an override in the shared lookup would turn the culture with the realm.
- **The war and its lock key on the `Hostile` tier.** `WarOfTheRingService.DeclareHostileTierWars`, `ReconcileDeclaredWars` and `ShouldBlockPeace` all read `DiplomacyService.GetRelationshipTier`, and the three peace-block layers read `ShouldBlockPeace`. A second lock beside them would have to copy all three layers.
- **The phases are closed** ([war-of-the-ring.md](war-of-the-ring.md), "Do not add WarPhase values"), so a join cannot be a phase.
- **Order matters.** When a war is declared, vanilla's `AllianceCampaignBehavior.OnWarDeclared` (v1.5.7, lines 670 to 709) asks every ally of both realms that is not yet at war to join, one kingdom decision each. Full War avoids that by turning the phase before it declares (#772); a join needs the same order.
- **The store is read off the main thread.** The party AI's late tick (`Campaign.LateAITick`, started by `MapScreen.OnPostFrameTick`) reads it on a worker by two routes: `TaomMobilePartyAIModel` asks `FireAndTributeService.IsOwnSide` for a kingdom's side, and vanilla's `MobilePartyAi` asks `FactionManager.IsAtWarAgainstFaction`, which reaches `TaomDiplomacyModel.IsAtConstantWar`, `ShouldBlockPeace` and `DiplomacyService.GetRelationshipTier`. The daily tick cannot overlap that task in v1.5.7, because `Campaign.RealTick` waits for it (`WaitAsyncTasks`, Campaign.cs:907) before `Campaign.Tick` fires the daily events. Whether a console command can run while the task is in flight is not known. `UnlookedForAidWiringTests` gates the alignment read path and the store's.
- **Vanilla charges a broken alliance or trade agreement to the declarer.** `AllianceCampaignBehavior.ApplyBrokenAlliancePenalty` (v1.5.7, lines 582 to 590) takes -100 relation between the ruler of the first faction of a declaration and the other ruler, and gives the player a trait hit when that first ruler is the player. `TradeAgreementsCampaignBehavior.ApplyBrokenTradeAgreementPenalty` (lines 195 to 203) does the same with -50. The Full War script declares in kingdom-list order.
- **The store is a process singleton** and the engine can run a day's tick twice after a load ([ticks](../reference/engine/campaign-tick-time-and-party-ai.md)).
- **`FactionSide`'s zero value is `Free`.** A failed dictionary lookup that leaves its `out` value alone reports Free.

### Solution Approach

One feature module, one service, one store, one campaign behavior. No Harmony patch and no GameModel.

- **The pledge.** `WarPledgeStore` maps a kingdom id to Free or Evil. It is saved by `WarPledgeBehavior` (two SyncData keys) and reset in that behavior's constructor, which the module runs for every campaign. A published map is never changed: each write builds a new map and swaps a volatile reference, as `FireAndTributeService` does for its host sides, so a reader on the AI worker sees the old map or the new one and never a map in the middle of a write.
- **The side, at the source.** `AlignmentService.GetKingdomSide` reads the store first. `GetCultureSide` and the shared lookup do not, so the culture keeps the table's side. `ResolveSide` and `AreSameAlignment` go through `GetKingdomSide`, so the realm, its lords (`FactionSides.OfHero`) and a player who serves it read the new side.
- **The wars, at the source.** `DiplomacyService.GetRelationshipTier` returns `Hostile` for a pledged realm against a realm of the other side. A static `Permanent` row wins, and a pair with no pledged realm reads the table alone. The three peace-block layers then lock the realm's wars with no change to any of them, and `ReconcileDeclaredWars`, at each session launch and each host daily tick, declares a war that is still missing, when `autoWarBetweenHostileTiers` is on.
- **The join** (`WarPledgeService.TryJoin`), in this order: write the pledge; make peace with every live realm of the new side; declare war on every live realm of the other side that the War of the Ring holds at war with it (`ShouldBlockPeace`, the test the reconcile uses). The pledged realm is the declarer of each war, so a broken alliance or trade agreement is charged to its ruler and not to the ruler of the realm it turned on. A real declaration is also what rebuilds both realms' cached at-war lists (`Kingdom.UpdateFactionsAtWarWith`), which the army and patrol AI read. Then one ledger line, one `[Check]` line, and the notice.
- **The checks before a join.** Full War and the War of the Ring on; a live kingdom; no pledge yet; Neutral by its id and by its culture (`ResolveSide`), so a player-founded realm that has a side through its culture cannot pledge; and no `Hostile` row with a live realm of the side it joins, a realm that is there by its own pledge included. The last one stops Umbar from standing beside Gondor.
- **Sides for the wars are by kingdom id.** The peace and the declarations read `GetKingdomSide`, as `DiplomacyService.IsWarAllowed` does. A kingdom that `alignment.json` does not list is on no side for them, so a join makes no war and no peace with a player-founded realm. The Full War rules treat the native members the same way.
- **The AI's courting** (`WarPledgeService.RunDailyCourting`), once a day on the co-op authority. The measure of "losing" is the momentum balance, the same number as the map meter (`IMomentumQueryService.SliderValue`, -100 to +100, positive when the Free Peoples lead). When it stands `losingBalance` or more against a side, that side takes the first row of the lean table that leans its way and passes the checks. It never takes a true neutral, a realm that leans to the other side, or a realm a player rules. It waits `daysBetweenAiJoins` days after the first Full War day it sees and after each join; the next courting day is saved, so a repeated day does nothing. The wait starts when the pledge is stored, also when a campaign listener throws inside the peace or a declaration after it. A candidate that the checks refuse stores no pledge and starts no wait.
- **Why the meter.** It starts at zero when the war starts, so it carries no bias from who holds more land on day 44, and the player can see it. The Free share of fortification points in the war ledger was the other candidate. The threshold is a first value; tune it against ledger runs.

### What a pledge changes for the services that read a side

Files that call each `IAlignmentService` method, outside the alignment service and this feature, on 2026-11-10: `grep -rlE "\.<Method>\(" Main Client --include=*.cs`. `Client/` has none.

| Call shape | Files | Effect of a pledge |
|---|---|---|
| `ResolveSide` | 30 | Follows the pledge when the id is the pledged realm: momentum enrollment, the war ledger, the rally, the Alignment map mode, the realm's lords, and Kingdom Allegiance's join veto (an AI clan of the other side does not join a pledged realm; a clan that already serves stays) |
| `GetKingdomSide` | 3 (alignment recruitment, alignment desertion, Eye of Sauron), and `DiplomacyService` for the tier | Follows the pledge |
| `AreSameAlignment` on two kingdom ids | 1 (`DiplomacyService.IsWarAllowed`) | Follows the pledge: no war inside a side |
| `GetCultureSide` | 4 (desertion's troop side, marriage, wanderers, prisoner recruitment) | No change, by design |

### Component Diagram

```
diplomacy/war_pledge.json        TaomSettings ("War of the Ring/War Pledge")
        |                                 |
WarPledgeConfigProvider          WarPledgeSettingsProvider
        \                                /
         WarPledgeService  <-- IMomentumQueryService, IMomentumSettingsProvider (the meter)
          |      |      \
          |      |       IWarPledgeWorldAdapter (who rules a realm, the notice)
          |      |
          |   IWarOfTheRingService.ShouldBlockPeace, IAllianceAdapter.MakePeace and DeclareWar
          |
     WarPledgeStore  <-- read by AlignmentService.GetKingdomSide
          |              and DiplomacyService.GetRelationshipTier
     WarPledgeBehavior (reset per campaign, SyncData, daily tick on the authority)
```

## Configuration

### Config File: `Main/_Module/ModuleData/diplomacy/war_pledge.json`

| Field | Type | Description |
|-------|------|-------------|
| `losingBalance` | int, 1 to 100 | A side courts when the momentum balance stands this far against it |
| `daysBetweenAiJoins` | int, 1 to 3650 | Days from the first Full War day to the first AI join, and between two AI joins |
| `leans` | list | One row per Neutral realm, in courting order |
| `leans[].kingdom` | string | The kingdom id |
| `leans[].lean` | `Free`, `Evil` or `Neutral` | The side the realm leans to; `Neutral` is a true neutral, which the AI never courts |

A number outside its range reverts to the default with a warning. A lean row with no kingdom, a second row for a kingdom, or a lean that is not one of the three words is dropped with a warning. A missing or unreadable file gives the defaults and an empty table, so the AI courts no realm. The provider is a process singleton: an edit needs a restart of the game.

### Current Values

`losingBalance` 25, `daysBetweenAiJoins` 30. Leans (Mike, 2026-11-10), in courting order:

| Lean | Realms |
|---|---|
| Evil | Khand (`battania`), `rhudaur`, `umbar`, `shaghana`, `abanissa` |
| Free | `cardolan`, `dorwinion` |
| True neutral | `anduinvale`, `angaladh`, `southrhovanion` |

### MCM

"War of the Ring/War Pledge", "Enable War Pledge", default on, no restart. The name and the hint are keyed (`taom_war_pledge_mcm_enabled`, `taom_war_pledge_mcm_enabled_hint`). Off stops new AI joins. A realm that has pledged stays pledged, and the console command still works. The AI's courting also needs "Enable War of the Ring" and "Enable Momentum Tracking": with tracking off the meter stands still, so its last value says nothing about today.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/WarPledge/WarPledgeService.cs` | The join, its checks, the AI's daily courting, the console status |
| `Main/Features/WarPledge/WarPledgeStore.cs` | The pledges and the next courting day; the save string |
| `Main/Features/WarPledge/WarPledgeBehavior.cs` | Per-campaign reset, SyncData, the daily tick's co-op and clock gates |
| `Main/Features/WarPledge/WarPledgeConfigProvider.cs`, `WarPledgeConfig.cs` | Loads and validates `war_pledge.json` |
| `Main/Features/WarPledge/WarPledgeSettingsProvider.cs` | The MCM switch |
| `Main/Features/WarPledge/WarPledgeModule.cs`, `WarPledgeIoC.cs` | Feature module and registrations |
| `Main/Features/WarPledge/Cheats/` | `taom.print_war_pledges`, `taom.add_war_pledge` |
| `Main/Adapters/WarPledgeWorldAdapter.cs` | Who rules a realm (co-op aware), the notice |
| `Main/Features/Execution/AlignmentService.cs` | `GetKingdomSide` reads the store |
| `Main/Features/Diplomacy/DiplomacyService.cs` | `GetRelationshipTier` reads the store |
| `Main/Features/WarChronicle/Ledger/` | The `ev=pledge` ledger line |
| `Main/_Module/ModuleData/diplomacy/war_pledge.json` | The lean table and the two numbers |
| `tools/ingame_checks/1113.toml` | In-game check manifest |

## Dependencies

- `IAlignmentService`, `IDiplomacyService` (Execution, Diplomacy): the side and the tier the checks read; both read the store
- `IWarOfTheRingService` (Diplomacy): the phase and `ShouldBlockPeace`; its `ReconcileDeclaredWars`, at session launch and on the host's daily tick, retries a declaration that failed, when `autoWarBetweenHostileTiers` is on, in kingdom-list order
- `IAllianceAdapter` (Adapters): the live kingdoms and their cultures, `AreAtWar`, `MakePeace`, `HasDeclaredWar`, `DeclareWar`
- `IMomentumQueryService`, `IMomentumSettingsProvider` (WarOfTheRingMomentum): the balance, and whether it is tracked
- `WarLedgerService` (WarChronicle): the `ev=pledge` line
- `IKingdomWarSnapshotAdapter` (Adapters): the whole campaign day, false for an invalid clock
- `ICoopSessionProvider`, `IPlayerControlQuery` (CoopInterop): the authority gate, and whether a human rules a realm
- `IOutcomeLog` (Core): the `[Check]` line and the swallowed notice fault

## Tests

- `TAOM.Tests/Features/WarPledge/WarPledgeServiceTests.cs`: the join over the real alignment, diplomacy and War of the Ring services (wars declared by the pledged realm, peace first, the lock, every refusal, a kingdom sided by its culture, the dry run, the pass and fail lines), the courting (anchor, wait, threshold, the two config numbers, order, player-ruled, true neutral, switches, a repeated day, a throw after the pledge) and the status.
- `PledgedSideOverrideTests.cs`: the side and tier overrides, the culture that keeps its side, `Permanent` winning, two native members left alone.
- `WarPledgeStoreTests.cs`: pledge rules, reset, the save string and every bad row; that no write changes a published map and that the map field is volatile.
- `TAOM.Tests/Features/UnlookedForAid/UnlookedForAidWiringTests.cs`: the alignment read path may call the store's `TryGetSide`, and `TryGetSide` may only count the map and look one id up.
- `WarPledgeConfigProviderTests.cs`: one test per validation rule.
- `WarPledgeShippedConfigTests.cs`: the shipped table against the shipped kingdoms, `alignment.json` and `diplomacy.json`.
- `WarPledgeBehaviorSessionResetTests.cs`: the constructor's reset, the save round trip into a fresh behavior, the daily tick's gates.
- `WarPledgeWiringTests.cs`: the module shape, and that the container hands the one store to `AlignmentService` and `DiplomacyService`.
- `WarPledgeCheatReportTests.cs`, `WarPledgeSettingsProviderTests.cs`; `tools/tests/test_analyze_war_ledger.py` for `ev=pledge`; `tools/tests/fixtures/war_ledger_v1.txt`, the one pledge line that the C# formatter test writes and the analyzer test reads.
- `TAOM.Tests/Features/WarOfTheRingMomentum/MomentumEnrollmentServiceTests.cs` and `WarOfTheRingMomentumKingdomDestroyedTests.cs`: a kingdom's destruction enrolls a realm that took a side since the last sweep before the side's kingdoms are counted, so that realm keeps the war open and is in a victory's peace.
- Co-op row: [coop-interop.md](coop-interop.md), `WarPledgeBehavior`.

## How to verify in game

**Preconditions:** cheat mode on; "Enable War of the Ring" and its Test Mode on (Full War on day 3); a new campaign; no restart needed. Nothing below has been run in game yet.

1. Pass day 4, then run `taom.add_war_pledge cardolan free`. It prints a dry run and the command that confirms it.
2. Run `taom.add_war_pledge cardolan free confirm`. The log shows `[Check] id=taom.war-pledge.joined result=pass side=free by=console wars=N expected=N peace=N stuck=0` and a `[WarLedger] ... ev=pledge k=cardolan side=free by=console` line. A banner and a message read "Cardolan joins the Free Peoples in the War of the Ring."
3. In the encyclopedia Cardolan is at war with every living realm of the Shadow. A peace proposal with one of them is refused.
4. After the next daily tick the momentum popup lists Cardolan on the Free side.
5. Save, reload, and run `taom.print_war_pledges`: Cardolan is still pledged Free. Start a second campaign without leaving the game: the command lists no pledge.
6. `python tools/ingame_verdict.py --issue 1113` reads the check line.
7. **Balance (owed):** two observer campaigns, switch off and on, compared with `python tools/analyze_war_ledger.py --compare off.log on.log`. Its `realms_pledged` row counts the joins, and `python tools/analyze_war_ledger.py on.log` lists them under "Realms pledged". A realm that pledged counts for its new side from that day, so `net_free`, `net_evil` and the share include its lands; both reports print a note when a log holds a pledge.

An unreachable step is reported with the command tried and the unmet precondition.

## How to change a lean

1. Edit the realm's row in `Main/_Module/ModuleData/diplomacy/war_pledge.json`; move the row to change the order a side courts in.
2. Run `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~WarPledgeShippedConfigTests"`. It fails for an id that is no kingdom, a realm that already has a side, a Neutral kingdom with no row, and a lean toward a side the realm is `Hostile` to. `ShippedConfig_Leans_AreTheDecidedOnesInCourtingOrder` pins the decided table, so update it with the new decision.
3. Update the realm list in the `WarPledgeEnabled` hint (`Main/Features/TaomSettings.cs`) and its English row `taom_war_pledge_mcm_enabled_hint`; no gate compares the hint with the file. The row is translated text, so follow `/localize` Case E.
4. No other code change. Restart the game.

## Known Limits

- **A pledge has no end.** No command or rule removes one. After the war ends the peace lock lifts for a pledged realm as for every realm, and it keeps its side.
- **The switch stops new joins only.** It does not undo a pledge.
- **The people keep their side.** A soldier or a clan sided by culture stays Neutral (`GetCultureSide`), so the pledged realm's own troops do not desert it. Troops of an opposed culture in its parties do desert, under the existing alignment desertion rule, because the owner now has a side.
- **Vanilla's call to war still runs.** A Neutral ally of the pledged realm gets one call-to-war decision per declared war, and so does a Neutral ally of each realm it declares on (`AllianceCampaignBehavior.OnWarDeclared`, v1.5.7 lines 683 to 708).
- **A pledge breaks an alliance and a trade agreement with the other side.** Each ends when the war is declared, at -100 and -50 relation between the two rulers. The pledged realm's ruler is the one charged; a player who rules the other realm gets no trait hit. A player who pledges a realm they rule through the console is the declarer and does get it. A player who is a vassal of a pledged realm gets one war notice per declared war.
- **A join that throws is not completed.** When a campaign listener throws inside the peace or a declaration, the pledge and the wait stand, and the ledger line, the `[Check]` line and the notice are not written. Wars with the realm's own side that the peace step had not reached stay open; nothing ends them in Full War. A declaration that was not reached is retried at the next session launch, in kingdom-list order, so that retry can charge a broken alliance to the other realm's ruler. No throwing listener is known.
- **A player-founded realm is outside the wars.** A kingdom that `alignment.json` does not list gets no war and no peace from a join, and cannot pledge when its culture gives it a side.
- **The peace block must be on.** With `blockPeaceBetweenHostileTiers` off in `war_of_the_ring.json`, the War of the Ring holds no pair at war, so a join changes the side and declares nothing. `autoWarBetweenHostileTiers` does not gate a join.
- **A pair with no stance link gets no declaration event.** The engine stores a missing link as War once the model holds the pair at war (`FactionManager.GetStanceLinkInternal`), with no `WarDeclared` event. Every kingdom pair gets its link when vanilla rebuilds the at-war lists at the new game, on a load and when a kingdom is created (`CampaignFactionManagerBehaviour`, `Kingdom.UpdateFactionsAtWarWith`), so this needs a kingdom created during the campaign, and such a kingdom is not in the lean table.
- **The Alignment map mode repaints on a war, peace, alliance or fief event** (`RealmBordersCampaignBehavior`). A join that declares no war repaints at the next such event.
- **Co-op:** only the authority courts and only the authority's console adds a pledge. TAOM's save data is not replicated, so a client that joined before a pledge sees the wars and not the new side. Not exercised in a co-op session.
- **Balance is not measured.** A joining realm goes to war with every realm of the other side. The observer runs in "How to verify in game" are owed.
- **Stored sides:** a search of `Main/**/*.cs` for fields that hold a `FactionSide` found only Fire and Tribute's host sides, which come from its own config. Two readers keep a side in another form. The border paint is a cache, and it refreshes on its events (above). Momentum keeps each side's kingdoms as a list (`MomentumWarState`), which the enrollment sweep rebuilds at session launch, on each daily tick and when an enrolled kingdom is destroyed, before the victory check that follows. The AI's join runs before that day's sweep. A realm pledged by the console is on the war meter and in the popup after the next sweep, which is less than a day away while Momentum Tracking is on. What happens before that sweep is not made good later. A battle the realm fights, a siege it wins, a raid it makes and an army it gathers are not recorded: no momentum and no statistic, because each `MomentumEventService` handler returns when a kingdom is not enrolled. A battle that another kingdom of its new side loses is scored against the side's strength without the realm, so that award is larger (`CalculateBattleMomentum`). The war ledger, the rally, Fire and Tribute, the Eye of Sauron, recruitment and desertion read the side again on each use.

## Changelog

- 2026-11-10: Slice 1 (#1113): the pledge store, the side and tier overrides, the join, the lean table, the AI's daily courting, the ledger line, the notice, two console commands and the MCM switch.
- 2026-11-10: after the Codex adversarial pass on the landed commit: momentum sweeps its enrollment when an enrolled kingdom is destroyed, before the victory check, so a realm that pledged that day counts for its side; the ledger analyzer no longer lists a pledge once a later kingdom line of that realm shows that a load took it back.

## GitHub Issue

- **Issue:** #1113: [War Pledge: Neutral realms join the War of the Ring for good](https://github.com/haterade22/TAOM/issues/1113)
- **Status:** Open
