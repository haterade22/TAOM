# ADR-014: A Separate Module Holds the Compile-Time Coop References

**Status**: Accepted (2026-11-11, the maintainer: "Separate module it is")

**Date**: 2026-11-11

**Priority**: Standard

## Context

TAOM needs code that talks to BannerlordCoop: a settings and build handshake at join, and a relay of
state that only the server writes (War of the Ring momentum first). That code must use Coop's
`MessageBroker`, `INetwork` and its message interfaces, which means compile-time references to
Coop's assemblies.

`TAOM.dll` cannot hold those references. The engine calls `Assembly.GetTypes()` on every submodule
DLL before any `OnSubModuleLoad` ([ADR-013](./013-server-safe-core-client-ui-submodule.md)). Coop
loads its own `Common` and `GameInterface` only in its `OnSubModuleLoad`, and on a dedicated server
`DedicatedServer.Windows` installs Coop's resolver in its own. So no Coop assembly is loaded at scan
time in any process, co-op included, and a type whose base type, interface or value-typed field leads
to a Coop assembly fails the scan. On 2026-10-21 a failed scan ended a dedicated server with exit 5. Today `TAOM.dll` reads Coop by reflection only
([coop-interop](../features/coop-interop.md), "Layer 2").

The maintainer reports permission from the Coop maintainers for a TAOM module that references Coop
(2026-11-11). Coop builds are public; the co-op dedicated server is private.

## Decision

1. A new module, `TAOM.CoopCompat`, in the TAOM repo under `CoopCompat/`, holds every compile-time
   Coop reference. It depends on TAOM and Coop and loads after both. Its submodule is untagged, so it
   loads on a client and on a dedicated server.
2. `TAOM.dll` keeps reflection-only Coop detection and never references `TAOM.CoopCompat`. The module
   reaches TAOM through TAOM's public types and its container.
3. The module has two DLLs. Every type that needs a Coop assembly to load (the transport and the
   wire contracts) is in `TAOM.CoopCompat.Coop.dll`, which `SubModule.xml` does not name, so the
   engine never scans it. The services use `ICoopTransport`, which names no Coop type. The transport
   starts at the first campaign start, after every `OnSubModuleLoad`. `TypeScanGateTests` fails when a
   type of the scanned DLL needs a Coop assembly to load.
4. Coop's join check needs the same module version on every peer. By convention the module's
   version also equals TAOM's, so one release number covers both. A test pins the equality.
5. The TAOM download ships the module. It never ships a Coop or server assembly;
   `tools/package_release.py` refuses one.
6. A handshake mismatch warns the player and never disconnects (decision D2 of the plan).

## Consequences

### Positive
- Single-player TAOM loads no Coop code and cannot fail on a missing Coop assembly.
- Co-op code is typed and testable, where reflection would hide a Coop API change until a session.
- Players who do not play co-op can leave the module out.

### Negative
- Two more projects (the module and its Coop-backed DLL), one manifest and deploy step, and one more
  folder in the download.
- The module builds against one Coop build (`CoopBinDir`). A Coop release that changes a bound type
  needs a rebuild; until then the module logs one line and is off, and the campaign goes on.
- Every peer must run the same TAOM build and module set, which Coop's join check already requires.

### Neutral
- Without a Coop install at `CoopBinDir`, the build skips the project with one message, so a machine
  without Coop still builds TAOM.

## Alternatives Considered

### Alternative (a): put the Coop code in `TAOM.dll`
A compile-time reference in core fails the engine's type scan without Coop. Rejected.

### Alternative (b): reflection only, in `TAOM.dll`
Coop's message types are interfaces and records that a handler must implement or construct, and its
broker holds subscriptions weakly. Reflection would need emitted types and would hide API drift.
Rejected.

### Alternative (c): no compat module, document a manual settings check
This leaves the War of the Ring meter wrong on every client and depends on players reading logs.
Rejected.

## References

- [coop-compat](../features/coop-compat.md): the feature page.
- [coop-interop](../features/coop-interop.md): detection, authority and load order.
- [ADR-013](./013-server-safe-core-client-ui-submodule.md): the type scan and the server-safe core.
- Sternab's `TAOM.CoopCompat` for Bannerlord 1.4.8, the model for this module
  ([provenance register](../reference/provenance-register.md)).
- Issue #967 (sub-batch C3).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/README.md](./README.md)
- [docs/features/coop-compat.md](../features/coop-compat.md)
- [docs/features/coop-dedicated-server.md](../features/coop-dedicated-server.md)
- [docs/features/coop-interop.md](../features/coop-interop.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/provenance-register.md](../reference/provenance-register.md)

<!-- backlinks-end -->
