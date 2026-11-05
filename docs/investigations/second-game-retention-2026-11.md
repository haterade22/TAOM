# What a second game in one process keeps (MEMORY-05): heap snapshot procedure

> Hand-off: read this, run the procedure once on a deployed build of the 1.4.x line, and attach the
> numbers and root paths to the MEMORY-05 issue. Trigger: the 2026-11-04 port audit, finding MEMORY-05
> (`plans/_audit/2026-11-04-port148/deepdive/memory.md`), verified in
> `plans/_audit/2026-11-04-port148/verify/V07-memory.md`. Plan 119 ported the 1.5.x session resets
> (Enlistment plan 014, SpecialResources plan 001, hero races #1035) as correctness fixes. On the 1.5.x
> line the managed retention stayed after those fixes, so no memory fix is written until a snapshot
> names the holder.

## What is known

| Measurement | Value | Source |
|---|---|---|
| Process at the main menu after a campaign, v1.4.8 | 10,273 MB private against a cold menu of about 6.6 GB | [native-commit-audit-2026-08.md](native-commit-audit-2026-08.md), run 2 |
| Second campaign in one process, v1.4.8 | settles 1.8 GB above the first (14,720 against 12,9xx MB) | same |
| Managed heap (`heapMB`) at a return to the main menu | 523 MB on entry (2026-09-12, v1.4.8); on the 1.5.x line (v1.5.3) after the resets, seven returns entered at 300 to 393 MB and two at about 100 MB; a cold menu reads 73 to 83 MB | the 523 MB row: `plans/_audit/2026-11-04-port148/deepdive/memory.md`, MEMORY-05; the other rows: V07, section 3.2 |

The native part (engine allocator pools) is probably out of TAOM's reach. The managed part means
something still references objects of the finished campaign.

## Candidate holders found by reading the code

Read on `origin/bannerlord-1.4.5` on 2026-11-04. Each is reachable from a static root: TAOM's DryIoc
container is a static field (`_container` in `Main/IoC.cs`), and its `Reuse.Singleton` services live
for the whole process.

| Holder | Where | What it keeps reachable | Cleared |
|---|---|---|---|
| `_lastSessionStarter` on three container singletons | `EnlistmentBehavior` and `EnlistmentContentBehavior` (`Main/Features/Enlistment/Hooks/`), `MessengerCampaignBehavior` (`Main/Features/Messengers/`); registered `Reuse.Singleton` in `EnlistmentIoC.cs` and `MessengerIoC.cs` | the finished campaign's `CampaignGameStarter`, which holds that campaign's menu manager, conversation manager, every campaign behavior and every game model (installed v1.4.8 `TaleWorlds.CampaignSystem.CampaignGameStarter`, fields `_gameMenuManager`, `_conversationManager`, `_campaignBehaviors`, `_models`) | only when the next campaign's session launches |
| `CommanderLordAdapter._lastSeenMapEvent` (container singleton) | `Main/Adapters/CommanderLordAdapter.cs` | one finished `MapEvent` and the parties in it | never |
| `Patch70_FiefGrantDecisionSwap._faultReportedFor` (static) | `Main/Features/FiefGranting/Hooks/` | a whole `Campaign`, but only after a Patch70 fault | at the next fault |
| `MarriageClanPoolCache.PoolByCulture` (static) | `Main/Features/MarriageAlignment/` | `Clan` lists from the last game that rolled an NPC marriage | when its stamp changes |
| `MobilePartyAttachmentAdapter._cachedCommanderParty` (container singleton) | `Main/Adapters/` | the commander's `MobileParty` | on every load, new campaign and game end since plan 119 |

`PlayerAllianceProposalBehavior`, `CultureConversionBehavior` and `FieldCommissionBehavior` also keep a
`_lastSessionStarter`, but they are built with `new` for each campaign, so they hold it only while they
are reachable themselves.

## Probes already in the build

- `[MemStation] enter screen='GauntletInitialScreen' privMB=... heapMB=...` (and the matching `exit`)
  in `Logs/taom_debug_*.log` at every main-menu entry; on by default.
- Since plan 114 Part B, the `[SaveLoad]` `GameLoaded` and `GameInitializationFinished` lines carry `prevCampaign=alive|collected gen2Since=N`: a weak reference to the campaign that ended last. `alive` with `gen2Since` above 0 means the finished `Campaign` object itself is still reachable. See [save-growth-2026-11.md](save-growth-2026-11.md).

## Procedure

Setup:

- A deployed build of the merged 1.4.x branch (TAOM and TAOM.Dependencies from one build).
- `cheat_mode = 0` in `engine_config.txt`: with cheat mode on, opening your own inventory lists every
  item and adds gigabytes on its own (MEMORY-01). Open no inventory or party screen during the run.
- Other programs closed.
- Tools, free from Microsoft: PerfView for heap snapshots of a .NET Framework process; ProcDump and
  WinDbg with SOS for exact counts and root paths. Menu names below are from PerfView's own help and
  can differ between versions. Keep every snapshot and dump outside the repo, for example in
  `C:\taom-heap\`; a full dump is about as large as the process's private memory.

Steps, all in one game process started from the launcher:

1. At the main menu wait 60 s. Snapshot **A** (cold menu).
2. Load save S from the main menu. On the campaign map wait 60 s. Snapshot **B** (optional, in campaign).
3. Return to the main menu from the escape menu. Wait 60 s. Snapshot **C** (one finished campaign).
4. Load S again from the main menu, wait 60 s on the map, return to the main menu, wait 60 s.
   Snapshot **D** (two finished campaigns).
5. Optional, for the save-growth question (SAVE-TIME-01): load S, then load S again from the save and
   load screen over the campaign map (an in-game load), wait 60 s. Snapshot **E**; compare it with B.

With each snapshot, copy the `[MemStation]` line written when that screen opened, and the
`prevCampaign=` token if the build has it.

Taking a snapshot:

- PerfView: Memory, Take Heap Snapshot, pick the Bannerlord process, Dump GC Heap. It writes a
  `.gcDump` file of the live objects. PerfView samples a very large heap and scales the counts, so read
  its counts as estimates; whether a type is present at all is reliable.
- Exact counts and root paths: `procdump -ma <pid> C:\taom-heap\C.dmp`, open the dump in WinDbg, then
  run the commands below. If your SOS rejects `-live`, drop it and confirm each instance with
  `!gcroot`: an object with no root is garbage that has not been collected yet.

```
.loadby sos clr
!dumpheap -stat -live -type TaleWorlds.CampaignSystem.CampaignGameStarter
!dumpheap -mt <MethodTable from that line>
!gcroot <one object address from that list>
```

## How to compare

Count these types in A, C and D (and in B and E when taken): `TaleWorlds.CampaignSystem.Campaign`,
`TaleWorlds.CampaignSystem.CampaignGameStarter`, `TaleWorlds.CampaignSystem.Hero`,
`TaleWorlds.CampaignSystem.Clan`, `TaleWorlds.CampaignSystem.CharacterObject`,
`TaleWorlds.CampaignSystem.Party.MobileParty`, `TaleWorlds.CampaignSystem.Settlements.Settlement`,
`TaleWorlds.CampaignSystem.Army`, `TaleWorlds.CampaignSystem.MapEvents.MapEvent`, and every type whose
name starts with `TAOM.`.

- A holds no campaign: its counts are the floor.
- C above A for any campaign type: part of the finished campaign is still reachable at the menu. Take
  the root path of one instance of each such type (PerfView: the referrers view of that type; WinDbg:
  `!gcroot`).
- D about equal to C: one finished campaign is held and replaced at the next session. D about twice C:
  campaigns pile up.
- E holding about twice B's `Hero` count: the old campaign's heroes survive an in-game load.
- `heapMB` at C and D against A says what it costs.

## Decision table

| The root path ends in | Meaning | Next (a follow-up plan, never this doc) |
|---|---|---|
| TAOM's container, then `EnlistmentBehavior`, `EnlistmentContentBehavior` or `MessengerCampaignBehavior`, then `_lastSessionStarter` | those singletons keep the finished campaign's starter and all it holds | clear `_lastSessionStarter` at game end in the three behaviors; nothing else changes, because each campaign builds a new starter (installed v1.4.8 `Campaign.OnInitialize`) |
| `CommanderLordAdapter._lastSeenMapEvent` | one finished battle and its parties | clear it from `ServiceMaintenanceService.ResetSessionCaches` |
| `Patch70_FiefGrantDecisionSwap._faultReportedFor` | a Patch70 fault pinned a campaign | keep a weak reference or an id instead |
| `MarriageClanPoolCache.PoolByCulture` | the last game's clans | clear the pool at game end; see also the stale-clan lead in plan 114's maintenance notes |
| a static event's handler list, for example `ScreenManager.OnPushScreen` | a subscriber from the old campaign | unsubscribe it at game end |
| only TaleWorlds fields, no TAOM type on the path | engine retention | tell players to restart the game between sessions; report it to TaleWorlds |
| C has no campaign objects and `heapMB` is near A's | the managed side is clean | the rest is native allocator pools: no TAOM fix; advise a restart between sessions |
| `prevCampaign=collected` yet `heapMB` above 300 at the menu | the holder is not the campaign graph | compare the largest types between A and C (arrays, strings, UI and texture caches), not TAOM singletons (V07 section 3.3) |
