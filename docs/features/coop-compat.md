# TAOM.CoopCompat

## Overview

`TAOM.CoopCompat` is a separate module in the TAOM download that connects TAOM to BannerlordCoop.
When a client joins a co-op campaign, it compares the client's TAOM build and simulation settings
with the server's and tells the player about a difference, and it warns a player whose data mods
load in another order than the server's. It also sends the server's War of the Ring
momentum to every client, so a client's War of the Ring screen shows the server's war. And it lets a
guest buy from the Elite Emissary and use a town armoury: the guest's screen asks the host, and the
host runs TAOM's own purchase or upgrade for the guest's hero (#1329).

The module does nothing without Coop. A player who does not play co-op does not need it.

## Why This Exists

- **Vanilla behavior:** BannerlordCoop's join check compares module ids and version strings, then
  replicates vanilla campaign objects. It knows nothing about TAOM's own state or settings.
- **TAOM requirement:** every peer must run the same TAOM build and the same simulation settings, and
  TAOM state that only the server writes (War of the Ring momentum) must reach the clients.
- **Without this feature:** a client with a different build or different settings plays with no
  warning, and its War of the Ring meter stays at the join-time value from the save
  ([coop-interop.md](coop-interop.md), "What is NOT done").

Sternab's `TAOM.CoopCompat` for TAOM 2.0.29.5, Coop 0.1.6 and Bannerlord 1.4.8 showed the approach: a
compat module next to TAOM, a settings fingerprint relay and a momentum relay. This module is a
TAOM-owned rewrite for Bannerlord v1.5.7 and the current Coop 1.5.x API. Sternab gave permission to
port his work (2026-10-21); his two rules hold here: never decide on `ModInformation.IsServer` at
`OnSubModuleLoad`, and never subscribe a static method to Coop's `MessageBroker`.

Guest trade is a port of minimalcash63's `TAOM.CoopEmissaryArmory` (2026-11-15, with permission): the
host builds TAOM's own emissary and armoury services with the guest hero's adapters, keeps one request
in flight per guest, and copies the host's balance back to the guest's screen. From
Sternab's emissary relay in `TAOM-Coop` it takes the request checks: the hero comes from the sender's
current connection, never from the message, and every field is bounded before any hero is read.

## Architecture

### Design Challenge

- **A missing assembly ends the process.** The engine calls `Assembly.GetTypes()` on every submodule
  DLL before any `OnSubModuleLoad` ([ADR-013](../adrs/013-server-safe-core-client-ui-submodule.md)).
  Coop loads its own `Common` and `GameInterface` only in its `OnSubModuleLoad`, and on a dedicated
  server `DedicatedServer.Windows` installs Coop's resolver in its own. So at scan time no Coop assembly
  is loaded in any process, co-op included. A type whose base type, interface or value-typed field
  leads to a Coop assembly fails the scan, and the engine then aborts. So `TAOM.dll` reads Coop by
  reflection only, and the compile-time references live in this module
  ([ADR-014](../adrs/014-separate-coop-compat-module.md)).
- **Coop holds subscriptions weakly.** `MessageBroker` keeps a `WeakReference` to each delegate's
  target, so a handler on a static method or on an object nothing else holds is dropped. The
  transport and both services live in fields of the submodule.
- **The role changes per session.** `ModInformation.IsServer` is set late, and the session container
  changes on every host or join. The transport reads the role when each message arrives.
- **Same version on every peer.** Coop's `ModuleValidator` compares every module's version exactly
  across the peers. By convention this module's version also equals TAOM's, so one release number
  covers both (`CoopCompatPairing_VersionAndTaomMetadataEqualMainVersion` pins it).

### Solution Approach

- **Two DLLs.** Every type that needs a Coop assembly to load (the transport and the three wire
  contracts, which implement Coop's `ICommand` and `IEvent`) is in `TAOM.CoopCompat.Coop.dll`.
  `SubModule.xml` names only `TAOM.CoopCompat.dll`, so the engine scans only that one. The second DLL
  sits in the same bin folder and loads the first time the module runs code that uses it.
  `TypeScanGateTests` fails when a type of the scanned DLL needs a Coop assembly to load.
- `Coop/Transport/CoopTransport` subscribes instance methods to `MessageBroker`, resolves Coop's
  `INetwork` for each send, and runs received work on the game thread through `GameThread.RunSafe`,
  which logs a throw and does not cancel the rest of the frame's queued work. `ICoopTransport` names
  no Coop type, so the services compile and run in tests without Coop.
- `Handshake/SettingsHandshakeService`: on `ClientCampaignReady` the client sends a
  `CoopCompatHello` (protocol version, TAOM build text, the module version ids (MVIDs) of `TAOM.dll` and
  of both module DLLs, and its settings fingerprint). The server compares it with its own and
  answers with a `CoopCompatHelloReply`. The client shows a notice for a build mismatch or for
  different settings groups. It warns and never disconnects.
- `LoadOrder/LoadOrderCheckService`: the load order warning, carried by the same hello and reply.
  See "Load order warning" below.
- `Momentum/MomentumRelayService`: the server answers each hello with a `CoopCompatMomentumSnapshot`
  of the war state (`IMomentumStateStore.CaptureWarState`, without the per-player events). After a
  change it broadcasts again, at most once a minute of real time, plus once every 24 in-game hours.
  A client applies a snapshot with `ApplyWarState`, which replaces the war state and keeps the local
  player events. A client ignores an older sequence number and a snapshot with a different protocol.
- `GuestTrade/GuestTradeRelayService` (#1329) attaches itself to TAOM's `GuestTradeClient`. A guest's
  emissary or armoury screen sends a `CoopCompatTradeRequest`. The transport names the hero that the
  sender's current connection controls: `IPlayerManager.TryGetCurrentController` for connected peers
  only, then `TryGetSenderHero` for the hero's `StringId`. (`Player.HeroId` is Coop's object key,
  `"Hero_" + StringId`, which `CampaignObjectManager.Find` cannot resolve.) A connection that controls
  no hero gets a refusal, not silence. TAOM's `GuestTradeHost` checks the request (the hero leads a
  free party, is in that settlement, and is in no battle and no besieged settlement; the emissary is
  on, the settlement is a key one and not at war with the hero; the town has an armoury), runs
  `EliteEmissaryService.Purchase` or `ArmouryUpgradeService.Execute` with the guest hero's
  `IPlayerPartyAdapter` and `IArmouryPlayerAdapter`, and answers with a `CoopCompatTradeReply` that
  carries the result, the hero's balance and, for the armoury, the host's armoury level (a visiting
  armourer's bonus is host state only). The host's change to the roster, gold and inventory reaches
  the guest through Coop's own sync. Every request has a new id, so the host keeps no reply cache. When
  the host does not answer within 15 seconds, the guest's next click says so and sends nothing; the
  click after that sends again. A host on another protocol version, or an older `TAOM.CoopCompat` with
  no trade handler, never answers, so the guest sees that notice; the join handshake already reports
  either one as a build mismatch.
- The same relay carries the lord's gear ladder for co-op players
  ([coop-interop.md](coop-interop.md#lords-ladder-for-co-op-players)). `CoopCompatTradeReply` fields 14
  to 22 hold the server's `GuestLadderView` of the guest hero's rung: `HasLadder`, `LadderSlot` (the
  `LadderSlot` value), `LadderReady`, `LadderCanClaim`, `LadderLordLevel`, `LadderMaterialId` (`""` for
  no hand-in), `LadderNeeded`, `LadderCarried` and `LadderChoices`. The client reads them back with the
  server's caps: a slot that is not a defined `LadderSlot` gives no view, a material id over 128
  characters gives no hand-in, and the choices keep at most 64 ids of 1 to 128 characters (a null list
  reads as empty). The hand-in and the claim are trade requests with actions 5 and 6, the slot name in
  `Subject` and the piece in `Target`; no request field was added. `ProtocolVersion` stays 1: a v2.0.40
  peer skips fields 14 to 22, and a reply from a v2.0.40 server reads as no ladder, so that guest's
  armoury looks as before (`OlderPeerLayoutTests`).
- `CoopCompatSubModule` starts the transport and builds the three services at the first campaign start
  (`OnGameStart`), when every module's `OnSubModuleLoad` has run, so the module order against Coop and
  `DedicatedServer.Windows` does not matter. It then adds `MomentumRelayCampaignBehavior` to each
  campaign. If a Coop assembly is missing or changed, it logs one line and is off for that process;
  the campaign goes on.
- The Long Defeat co-op session (#1280) starts earlier, from `OnBeforeInitialModuleScreenSetAsRoot`
  (once, behind a latch), because a session starts from the main menu with no campaign.
  `CoopSessionTransport` wraps Coop's `SessionLauncher` and moves its events to the game thread;
  `LongDefeatSessionLink` passes on only sessions for TAOM's mod id and run-ended messages of this
  protocol to TAOM's `ILongDefeatCoopPort`. Against a Coop with no session API (no
  `Missions.Sessions.dll` in `CoopBinDir`, before BannerlordCoop #4694), the project builds
  `CoopSessionTransport.NoSessionApi.cs` instead: its `Start` throws, the module logs "sessions off",
  and the co-op option stays hidden.
- TAOM fief elections (#1346): `DecisionTypeRegistration` registers `TaomSettlementClaimantDecision`
  with Coop's `KingdomDecisionTypeRegistry`, once per process: from the main-menu hook on a client or
  a listen host, and from the first campaign start, which is the only one of the two the dedicated
  server reaches under the Coop host ([headless server](../research/taom-headless-server.md), row 8).
  Coop then sends a TAOM fief election to every client and players vote on it; an unregistered type
  would be decided by the host's AI. Coop compares the registered types at join, so a player without
  this module, or with a build that registers different types, is refused with Coop's own message.
  The payload (`FiefClaimantWire`) carries the settlement, the excluded clan and the proposer. Coop's
  round status carries the host's ordered candidates and a vote carries its clan's key, so the ballot
  and the result match the host's; a client's own merit and support figures can differ before the
  votes arrive. Patch70 runs at `Priority.High` so the host swaps the decision before Coop's own
  `AddDecision` prefix adds and publishes it.

### Component Diagram

```
Coop MessageBroker / INetwork
          |
    CoopTransport (TAOM.CoopCompat.Coop.dll)
          |  ICoopTransport
     /          \
SettingsHandshakeService     MomentumRelayService            GuestTradeRelayService   CareerSyncRelayService
  |  SettingsFingerprint        |  IMomentumStateStore (TAOM.dll)  |  GuestTradeClient (guest, TAOM.dll)
  |  BuildIdentity              |                                  |  GuestTradeHost (host, TAOM.dll)   CareerSyncClient, CareerSyncHost (TAOM.dll)
  ISettingsNotice               MomentumRelayCampaignBehavior      |    IGuestTradeShops, IGuestTradeHeroAdapter
```

### Load order warning

Coop's join check compares the peers' module lists but not their order. The engine merges module XML
in load order, so a later module's items or troops replace an earlier one's, and a player whose data
mods load in another order sees other gear or troops than the host, with no hint. "Server" below is
the co-op server process: under BannerlordCoop the hosting player is a client of it, like the others.

- The client sends its active module ids in load order as `CoopCompatHello.ModuleOrder` (field 9),
  at most 256, each at most 128 characters; a longer id is left out. `LoadOrder/ActiveModulesAdapter`
  reads `ModuleHelper.GetActiveModules()`, which walks the loaded modules in the order
  `InitializeModules` added them (the launcher's order), and each module's `SubModule.xml`. A
  failed read is logged and sends no order; it is not kept, so the next read tries again.
- The server compares (`LoadOrder/LoadOrderRules`) only TAOM's own block (`TAOM.Dependencies`,
  `LOTRLOME_Armory`, `DualWield`, `TAOM_Map`, `TAOM`, `TAOM_AIO_Compat`) and every other
  non-official module whose `SubModule.xml` lists object data (items, troops, cultures, factions,
  settlements and the like; text-only entries do not count). It cuts both orders to the compared
  modules that both sides have and reports the first pair that differs.
- The server logs the result (`[CoopCompat] load order peer=<label> match` or `differ: ...` with both
  orders, and once per process the compared modules with their count, even when that list is empty,
  so a module the rules miss shows in the log) and answers in
  `CoopCompatHelloReply` fields 5 to 7 (`LoadOrderDiffers`, `LoadOrderHostFirst`,
  `LoadOrderHostSecond`, the server's module names with their ids). The player gets one yellow line
  naming the two modules. Nothing is blocked.
- The order is compared only when both sides send the same `ProtocolVersion`, like the settings
  groups; a client ignores the load order fields of a reply from another protocol.
- The switch is read in the server process each time a hello arrives. Off, the server logs that it
  did not compare. Where the server takes the value from is under Configuration.
- `ProtocolVersion` stays 1. A v2.0.40 peer skips the new fields; a server that gets no
  `ModuleOrder` from such a client logs "not reported" and warns nobody; a client that gets a reply
  without the fields shows nothing. `OlderPeerLayoutTests` proves both directions on the bytes.

### Career sync

TAOM keeps careers in its career book, and only the co-op server's book is saved. The relay carries a
player's career to the server and the server's careers back; TAOM decides
([coop-interop.md](coop-interop.md#career-sync-967) has the rules and the known limits). "Server" is the
co-op server process: the hosting player is a client of it, like the others.

- `CareerSync/CareerSyncRelayService` attaches itself to TAOM's `CareerSyncClient`. At Coop's
  `ClientCampaignReady` (after `SwitchToPlayer`, so the player plays its own hero) the client sends a
  `CoopCompatCareerReport` with `JoinReport` set. After that the submodule's `OnApplicationTick` calls
  the relay's `Tick`, and the client sends one report per action that changed its own hero's career.
- The transport names the hero that the sender's current connection controls, as for guest trade; the
  report's `HeroId` is only compared with it. `CareerSyncHost.Handle` returns what to send: an accepted
  career goes to every player but the sender (`BroadcastExcept`, Coop's `INetwork.SendAllBut`); a refused
  one, or a join report that differs from the server's saved copy, goes back to the sender only
  (`SendToPeer`). A change the server makes itself (a death clear, a tree migration) goes to every
  player (`Broadcast`), on the server only.
- `CoopCompatCareerUpdate.Sequence` is the last report the server handled for that hero, so the
  player's own client drops an answer to an older report. `Known` is false when the server holds no
  entry for the hero, and the client then keeps its own copy.
- No catch-up message on join: the join save already carries the server's career book, and Coop
  queues messages for a peer that is still loading and replays them.
- Two new contract types, `ProtocolVersion` 1. A peer without them drops them and keeps its careers on
  its own PC, as before.

## Configuration

The module reads TAOM's settings (`TaomSettingsPages.All()`) for the fingerprint. TAOM's settings
page has a Co-op group with three switches. One is for this module: "Warn players whose load order
differs from the host's" (`CoopLoadOrderWarning`, default on), read through
`Main/Features/CoopInterop/CoopSettingsProvider.cs` in the co-op server process. A player's own
value is never read, so it is excluded from the fingerprint. A dedicated server (the server build,
`GameNetwork.IsDedicatedServer`) takes the value from `taom-server-settings.json`, which
`ServerSettingsSource` reads once per process: set `"CoopLoadOrderWarning": false` there and restart
the server ([server settings](server-settings.md)). The value in a player's own settings page does
not reach that server. The other two, `CoopCareerSync` and `CoopGuestLadder`, are read by TAOM.dll in
the co-op server process; see [TAOM's co-op interop](coop-interop.md).

## Key Files

| File | Purpose |
|------|---------|
| `CoopCompat/CoopCompatSubModule.cs` | Entry point: at the first campaign start, starts the transport, builds the services and adds the campaign behavior; at the main menu, starts the Long Defeat session link |
| `CoopCompat/_Module/SubModule.xml` | Module manifest: depends on TAOM and Coop, untagged so it loads on a client and on a dedicated server |
| `CoopCompat/_Module/README.txt` | Player README: what the module does, what to install, load order, the old 0.3.25 build trap, the join, trade, career and ladder messages. Update it when a player message changes |
| `CoopCompat/TAOM.CoopCompat.csproj` | The scanned DLL; deploys both DLLs to the Client and Server bins; skips the build when no Coop is found at `CoopBinDir` |
| `CoopCompat/Coop/TAOM.CoopCompat.Coop.csproj` | The DLL that holds every Coop-backed type; not named in `SubModule.xml` |
| `CoopCompat/Coop/Transport/CoopTransport.cs` | The Coop binding |
| `CoopCompat/Coop/Transport/ICoopTransport.cs` | The seam the services and tests use |
| `CoopCompat/Coop/Messages/*.cs` | The six wire messages (hello, reply, momentum snapshot, Long Defeat run ended, trade request and reply) and `CoopCompatProtocol.ProtocolVersion` |
| `CoopCompat/Coop/Sessions/CoopSessionTransport.cs` | The binding to Coop's session API (#1280); `CoopSessionTransport.NoSessionApi.cs` is its stub for an older Coop |
| `CoopCompat/Coop/Sessions/ICoopSessionTransport.cs` | The session seam the link and tests use |
| `CoopCompat/Sessions/LongDefeatSessionLink.cs` | Joins TAOM's Long Defeat co-op port to the session transport |
| `CoopCompat/Handshake/SettingsHandshakeService.cs` | Build and settings handshake |
| `CoopCompat/Handshake/BuildIdentity.cs` | Protocol, TAOM build text and MVIDs of this peer |
| `CoopCompat/Handshake/InformationManagerSettingsNotice.cs` | The three player notices |
| `CoopCompat/LoadOrder/LoadOrderRules.cs` | Which modules a load order check compares, and the first pair that differs |
| `CoopCompat/LoadOrder/LoadOrderCheckService.cs` | The server's load order check and its log lines |
| `CoopCompat/LoadOrder/ActiveModulesAdapter.cs` | The engine's active modules and their `SubModule.xml` |
| `CoopCompat/Momentum/MomentumRelayService.cs` | War of the Ring momentum relay |
| `CoopCompat/GuestTrade/GuestTradeRelayService.cs` | Guest trade relay (#1329) |
| `CoopCompat/CareerSync/CareerSyncRelayService.cs` | Career sync relay (#967) |
| `CoopCompat/Coop/Messages/CoopCompatCareerReport.cs`, `CoopCompatCareerUpdate.cs` | The career sync messages |
| `Main/Features/CoopInterop/CareerSync/` | TAOM's career sync: `CareerSyncClient`, `CareerSyncHost`, `CareerSyncRules` |
| `CoopCompat/Decisions/DecisionTypeRegistration.cs` | Registers TAOM's fief election with Coop at the main menu and at the first campaign start (#1346) |
| `CoopCompat/Coop/Decisions/CoopDecisionTypes.cs` | The one call into Coop's `KingdomDecisionTypeRegistry` |
| `Main/Features/FiefGranting/FiefClaimantWire.cs`, `FiefClaimantWireData.cs` | The fief election payload and its checks |
| `Main/Features/FiefGranting/FiefClaimantDecisionCodec.cs` | Builds the payload on the host and the decision on a client |
| `Main/Features/CoopInterop/GuestTrade/GuestTradeClient.cs` | The guest side: one request in flight, the reply callback, the balance copy |
| `Main/Features/CoopInterop/GuestTrade/GuestTradeHost.cs` | The host side: request checks, the emissary and armoury runs |
| `Main/Features/CoopInterop/GuestTrade/GuestTradeShops.cs` | Builds TAOM's emissary and armoury services for one guest hero |
| `Main/Adapters/GuestTradeHeroAdapter.cs` | Reads a guest's hero on the host; `ArmouryPlayerAdapter.ForHero`, `PlayerPartyAdapter.ForHero` |
| `Main/Features/WarOfTheRingMomentum/IMomentumStateStore.cs` | `CaptureWarState` and `ApplyWarState` |
| `Main/Features/CoopInterop/TaomSettingsPages.cs` | The four settings pages the fingerprint reads |

## Dependencies

- `TAOM.dll`: `IModLogger`, `IOutcomeLog`, `IMomentumStateStore`, `SettingsFingerprint`,
  `TaomSettingsPages`, `GuestTradeClient`, `GuestTradeHost`, `CareerSyncClient`, `CareerSyncHost`, resolved
  from TAOM's container.
- BannerlordCoop's `IPlayerManager`: the hero behind a connection, for guest trade and career sync.
- BannerlordCoop (`Common`, `Coop.Core`, `GameInterface` and Autofac from the Coop module):
  `MessageBroker`, `INetwork`, `ContainerProvider`, `ModInformation`, `GameThread`. Coop's DLLs are
  references only and never ship in the TAOM download.

## Tests

- `TAOM.CoopCompat.Tests/`: the handshake and momentum services against `FakeCoopTransport`, the
  wire contract round trip (empty arrays come back empty, not null) and names, the transport binding to
  Coop, the manifest, the type scan gate (`TypeScanGateTests`), the reference gate over both DLLs, the
  service signature and entry point line count gates.
- `TAOM.CoopCompat.Tests/LoadOrder/`: the load order rules and the server's check (the switch, an
  empty or refused list, a failed module list read, the compare). `SettingsHandshakeServiceTests` covers the order in the hello
  and the reply and the player notice; `OlderPeerLayoutTests` reads a v2.0.40 hello and reply into
  the new types and back. `TAOM.Tests/Features/CoopInterop/CoopSettingsProviderTests.cs` covers the
  switch.
- `CoopModelPatchBaseCallGateTests` (#1151): Coop fixes vanilla rules with Harmony patches on
  `Default*Model` methods, and a TAOM override that never calls `base` hides them (#1141). The gate
  reads the Coop patches that the referenced Coop build declares by attribute (a target on the patch
  method, on the class, or from `TargetMethod` and `TargetMethods`), and fails on a TAOM override of
  a patched `Default*Model` method with no base call, unless its `Reviewed` entry says where TAOM
  carries Coop's rule. One known target per way of declaring one is a canary. An override that calls
  `base` on some paths passes. Patches registered at run time (`harmony.Patch`, AutoSync) are outside
  its reach, and a base call does not prove that TAOM's own rule around it treats co-op players right.
- `TAOM.Tests/Features/WarOfTheRingMomentum/MomentumStateStoreTests.cs`: `CaptureWarState` and
  `ApplyWarState`.
- `TAOM.Tests/Features/CoopInterop/GuestTrade/`: the client (busy, the late reply, the balance copy
  and its NaN gate) and the host (every refusal, the emissary buy, the armoury upgrade and its offer
  match, request bounds) and the module wiring. `TAOM.CoopCompat.Tests/GuestTrade/` and the transport
  tests cover the relay's field mapping both ways, the protocol check, the connection check and the hero
  lookup. The presenters' guest branches have no unit test (thin Gauntlet code); step 4 below covers them.
  The ladder fields: `GuestTradeRelayServiceTests` (both directions, the caps, an unknown slot, a null
  list), `ContractRoundTripTests`, `WireContractNamesTests` and `OlderPeerLayoutTests` (a v2.0.40 reply
  in both directions).
- `TAOM.CoopCompat.Tests/CareerSync/` runs the relay with TAOM's real career sync client and server
  over the fake transport: the join report, one report per tick, the hero from the connection, an
  accepted career to the others only, a refusal to the sender only, the server's own change, the
  protocol check. The transport tests cover the two new events, the role checks and `BroadcastExcept`;
  `SubModuleShapeTests` pins the frame tick. The rules, the client and the server are tested in
  `TAOM.Tests/Features/CoopInterop/CareerSync/`.
- `TAOM.Tests/Features/FiefGranting/FiefClaimantWireTests.cs` covers the fief election payload;
  `TAOM.CoopCompat.Tests/Decisions/` checks that the type reaches Coop's registry, once per process,
  and `SubModuleShapeTests` that both hooks call it. Building the decision from ids is engine code
  with no unit seam; step 5 below covers it.
- `MomentumRelaySizeTests` measures the size of a full snapshot. Whether Coop's transport carries a
  snapshot of that size in one message is not verified in a session yet.

## How to verify in game

**Preconditions:** a co-op session (a dedicated server or a hosted game) with TAOM and
`TAOM.CoopCompat` on every peer, at the same version. The checks are in
`tools/ingame_checks/967.toml`; read them with `python tools/ingame_verdict.py --issue 967`.

1. Join the campaign from a client. The server's taom log shows
   `[Check] id=taom.coopcompat.settings.server result=pass verdict=match ...`, and the client's shows
   `[Check] id=taom.coopcompat.settings.client result=pass ...`. A `result=fail` on the client means a
   build mismatch, and the client shows the build notice.
2. Change one simulation setting on the client only, then join again. The client shows the settings
   notice with that setting's group, and its log shows `verdict=differ groups=1`.
3. On the same join, the server logs `[Check] id=taom.coopcompat.momentum.server result=pass` with the
   peer's label when it answers that client's hello, and the client logs
   `[Check] id=taom.coopcompat.momentum.client result=pass`. After a settlement
   changes hands on the server, the client's War of the Ring screen shows the new value within about
   a minute (a person must look at the screen).
4. Guest trade (#1329), from a client whose hero leads its party: enter a key settlement whose owner
   is not at war with you and buy elite troops from the emissary. The troops join your party, the
   charge shows in your balance, and the server's taom log shows `[GuestTrade] EmissaryBuy for
   <hero> ... None, status 0`. In a town, open the armoury and improve a carried piece: the new piece
   is in your inventory and the gold is gone. Save on the server, load, and check both again. Then
   leave the town and open the emissary from a saved inquiry, or trade while the town is besieged:
   the host refuses with "not in this settlement, or it is in a battle or a siege", and nothing is
   charged.
   Lords' Ladder: carry a culture's lord's materials and open a town's armoury. The window lists
   "Hand over ... for your ..." with the count you carry; hand them over: the materials leave your
   inventory on every PC and the server's taom log shows `[GuestTrade] LadderHandIn for <hero> ... None,
   status 0`. At a level 3 armoury the window now lists "Claim your ..."; pick a piece: it is in your
   inventory. A second player's rung is unchanged. Save on the server, load, join: the rung is still
   claimed. Then set `"CoopGuestLadder": false` in the server's `taom-server-settings.json`, restart the
   server and open the armoury: no ladder rows.
5. Fief elections (#1346): `python tools/ingame_verdict.py --issue 1346` reads the checks. Every
   peer's taom log shows `taom.coopcompat.fief-election.registered` (a client and a listen host at
   the main menu, a dedicated server at its campaign start). In the campaign, take a castle for the
   kingdom that a client's clan is in. The host logs `fief-election.sent` and the client
   `fief-election.read`; the client sees the same candidates as the host and can vote, and the fief
   goes to the clan the votes chose. The server's Coop log shows no "is not synced, so the server AI
   decides it" line for `TaomSettlementClaimantDecision`.
6. Load order: with two community mods that add items or troops on every peer, swap their order on
   one client, then join. Do not use `LOTRLOME_Armory` or `TAOM_Map`: TAOM's `SubModule.xml` asks for
   them before TAOM, so a BUTR or BLSE launcher may sort them back or flag the swap. The client shows
   one yellow line naming the two modules, and the server's taom log shows
   `[CoopCompat] load order peer=<label> differ: ...`. Then set `"CoopLoadOrderWarning": false` in the
   server's `taom-server-settings.json`, restart the server and join again: no line, and the server
   logs `not compared: the switch is off on this server`.
7. Career sync: a new player joins and picks a career in character creation. The server's taom log
   shows `[CareerSync] stored '<career>' for '<hero>'`, and a second player sees that career on the
   new player's hero. The new player takes a career choice: the server logs it stored again. The
   player leaves and joins again: the career and the choice are still there. Save on the server,
   reload, join: still there. Then set `"CoopCareerSync": false` in the server's
   `taom-server-settings.json`, restart the server and change a career on a client: the server logs
   `career sync is switched off on this server` once and stores nothing.

## Distribution

The TAOM download carries `Modules/TAOM.CoopCompat` beside `Modules/TAOM`. The player gets Coop from
BannerlordCoop's own release. `tools/package_release.py` refuses any file in the module's `bin`
folders other than `TAOM.CoopCompat.dll` and `TAOM.CoopCompat.Coop.dll`, and any `.dll` or `.exe`
elsewhere in the module, so no Coop, server or library binary can ship in it. The player README (`README.txt`) ships at the module root; the build copies it with `SubModule.xml`. The co-op dedicated server is private and is not in any TAOM package. A
server operator adds `TAOM.CoopCompat` to the server's module list after TAOM; its place against Coop
and `DedicatedServer.Windows` does not matter.

**The module id is shared with the 1.4.8 module.** Sternab's `TAOM.CoopCompat` 0.3.25 (Bannerlord
1.4.8, TAOM 2.0.29.5, Coop 0.1.6) uses the same id and folder. Coodsz's client launcher and server
tool (`Coodsz/Coodsz-TAOM`, read 2026-11-12) install that 0.3.25 build into `Modules/TAOM.CoopCompat`
with "Install or repair TAOM". On a v1.5.x install that overwrites this module. Coop's join check
then refuses the peer, because its version differs from the server's.

**Source mirror.** The module source is also in `Coodsz/Coodsz-TAOM` under `TAOM.CoopCompat/`, on the
branch `taom-coopcompat-1.5.7` (draft PR #17 there), so Coodsz's tools and this repo work from one
module. A change on either side gets copied to the other. Coodsz merges and releases in that repo.

## Changelog

- 2026-11-11: first version (#967 C3): build and settings handshake, War of the Ring momentum relay.
- 2026-11-15: guest trade (#1329): a guest buys from the Elite Emissary and uses the armoury through
  the host. Ported from minimalcash63's `TAOM.CoopEmissaryArmory`, with Sternab's request checks.
- 2026-11-15: TAOM fief elections sync to players (#1346).
- 2026-11-17: load order warning: the co-op server compares a joining player's data mod order with
  its own and warns that player; the switch is in TAOM's Co-op settings group, read by the server.
- 2026-11-17: career sync (#967): a player's career goes to the co-op server, which checks it with
  TAOM's career rules, keeps it in its save and passes it on to the other players.
- 2026-11-17: the guest trade reply carries a player's rung of the lord's gear ladder, and the relay
  carries the ladder hand-in and claim, run by the co-op server.

## GitHub Issue

- **Issue:** #967: co-op server authority and sync (sub-batch C3); #1329: guest trade; #1346: fief elections
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/014-separate-coop-compat-module.md](../adrs/014-separate-coop-compat-module.md)
- [docs/features/coop-dedicated-server.md](./coop-dedicated-server.md)
- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
