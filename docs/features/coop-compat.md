# TAOM.CoopCompat

## Overview

`TAOM.CoopCompat` is a separate module in the TAOM download that connects TAOM to BannerlordCoop.
When a client joins a co-op campaign, it compares the client's TAOM build and simulation settings
with the server's and tells the player about a difference. It also sends the server's War of the Ring
momentum to every client, so a client's War of the Ring screen shows the server's war.

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
- `Momentum/MomentumRelayService`: the server answers each hello with a `CoopCompatMomentumSnapshot`
  of the war state (`IMomentumStateStore.CaptureWarState`, without the per-player events). After a
  change it broadcasts again, at most once a minute of real time, plus once every 24 in-game hours.
  A client applies a snapshot with `ApplyWarState`, which replaces the war state and keeps the local
  player events. A client ignores an older sequence number and a snapshot with a different protocol.
- `CoopCompatSubModule` starts the transport and builds both services at the first campaign start
  (`OnGameStart`), when every module's `OnSubModuleLoad` has run, so the module order against Coop and
  `DedicatedServer.Windows` does not matter. It then adds `MomentumRelayCampaignBehavior` to each
  campaign. If a Coop assembly is missing or changed, it logs one line and is off for that process;
  the campaign goes on.

### Component Diagram

```
Coop MessageBroker / INetwork
          |
    CoopTransport (TAOM.CoopCompat.Coop.dll)
          |  ICoopTransport
     /          \
SettingsHandshakeService     MomentumRelayService
  |  SettingsFingerprint        |  IMomentumStateStore (TAOM.dll)
  |  BuildIdentity              |
  ISettingsNotice               MomentumRelayCampaignBehavior (tick)
```

## Configuration

None. The module reads TAOM's settings (`TaomSettingsPages.All()`) for the fingerprint and has no
settings of its own.

## Key Files

| File | Purpose |
|------|---------|
| `CoopCompat/CoopCompatSubModule.cs` | Entry point: at the first campaign start, starts the transport, builds the services and adds the campaign behavior |
| `CoopCompat/_Module/SubModule.xml` | Module manifest: depends on TAOM and Coop, untagged so it loads on a client and on a dedicated server |
| `CoopCompat/TAOM.CoopCompat.csproj` | The scanned DLL; deploys both DLLs to the Client and Server bins; skips the build when no Coop is found at `CoopBinDir` |
| `CoopCompat/Coop/TAOM.CoopCompat.Coop.csproj` | The DLL that holds every Coop-backed type; not named in `SubModule.xml` |
| `CoopCompat/Coop/Transport/CoopTransport.cs` | The Coop binding |
| `CoopCompat/Coop/Transport/ICoopTransport.cs` | The seam the services and tests use |
| `CoopCompat/Coop/Messages/*.cs` | The three wire messages and `CoopCompatProtocol.ProtocolVersion` |
| `CoopCompat/Handshake/SettingsHandshakeService.cs` | Build and settings handshake |
| `CoopCompat/Handshake/BuildIdentity.cs` | Protocol, TAOM build text and MVIDs of this peer |
| `CoopCompat/Handshake/InformationManagerSettingsNotice.cs` | The two player notices |
| `CoopCompat/Momentum/MomentumRelayService.cs` | War of the Ring momentum relay |
| `Main/Features/WarOfTheRingMomentum/IMomentumStateStore.cs` | `CaptureWarState` and `ApplyWarState` |
| `Main/Features/CoopInterop/TaomSettingsPages.cs` | The four settings pages the fingerprint reads |

## Dependencies

- `TAOM.dll`: `IModLogger`, `IOutcomeLog`, `IMomentumStateStore`, `SettingsFingerprint`,
  `TaomSettingsPages`, resolved from TAOM's container.
- BannerlordCoop (`Common`, `Coop.Core`, `GameInterface` and Autofac from the Coop module):
  `MessageBroker`, `INetwork`, `ContainerProvider`, `ModInformation`, `GameThread`. Coop's DLLs are
  references only and never ship in the TAOM download.

## Tests

- `TAOM.CoopCompat.Tests/`: the handshake and momentum services against `FakeCoopTransport`, the
  wire contract round trip (empty arrays come back empty, not null) and names, the transport binding to
  Coop, the manifest, the type scan gate (`TypeScanGateTests`), the reference gate over both DLLs, the
  service signature and entry point line count gates.
- `TAOM.Tests/Features/WarOfTheRingMomentum/MomentumStateStoreTests.cs`: `CaptureWarState` and
  `ApplyWarState`.
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

## Distribution

The TAOM download carries `Modules/TAOM.CoopCompat` beside `Modules/TAOM`. The player gets Coop from
BannerlordCoop's own release. `tools/package_release.py` refuses any file in the module's `bin`
folders other than `TAOM.CoopCompat.dll` and `TAOM.CoopCompat.Coop.dll`, and any `.dll` or `.exe`
elsewhere in the module, so no Coop, server or library binary can ship in it. The co-op dedicated server is private and is not in any TAOM package. A
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

## GitHub Issue

- **Issue:** #967: co-op server authority and sync (sub-batch C3)
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
