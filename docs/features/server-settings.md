# Server Settings

## Overview

A Bannerlord dedicated server does not load MCM, so TAOM's settings pages do not exist there. This feature
gives the server a JSON file, `taom-server-settings.json`, that carries any TAOM setting the operator wants
to change. Every key the file leaves out stays at its server default: the compiled MCM default, except the
hitch probe, which a server keeps off. Every property of the four TAOM settings classes is settable from
the file except 2 buttons and 2 parked settings (`ServerSettingsSchemaTests` pins the total). It is ADR-013 decision 2
([ADR-013](../adrs/013-server-safe-core-client-ui-submodule.md)).

## Why This Exists

- **Vanilla behavior:** MCM is a client mod. On a dedicated server `GlobalSettings<T>.Instance` returns null,
  and every TAOM provider used its own `??` fallback. Those fallbacks matched the compiled defaults in
  behaviour, except `EnableHitchProbe`, whose provider fails closed (`?? false`) because it installs Harmony
  patches (Patch98). Three culture-doctrine sub-toggle fallbacks differ from their compiled defaults on paper,
  but they sit behind the culture-doctrine master toggle, which is off both ways, so nothing changes in play.
  The server set keeps it off by default, so a server still does not install the probe unless the file asks.
- **TAOM requirement:** a server operator needs to tune the same simulation settings a host would tune in
  Mod Options, and the co-op settings fingerprint must hash what the server actually runs.
- **Without this feature:** a server always runs the providers' fallbacks, with no way to change them and no
  log line that says so.

## Architecture

### Design Challenge

More than two hundred call sites read `TaomSettings.Instance`. Some services resolve during `IoC.Configure`
(`HeroRaceIoC.cs` resolves `ITableauPositionService` there), and some read the settings once
in a constructor that keeps the reference (`NameplateFadeSettingsProvider`). A load step at a fixed boot
point would either come too late for those readers or need edits at every site.

### Solution Approach

Each of the four settings classes declares its own static `Instance`, hiding MCM's:

```csharp
public static new TaomSettings? Instance =>
    ServerSettings.ServerSettingsSource.Get<TaomSettings>() ?? GlobalSettings<TaomSettings>.Instance;
```

Every existing call site binds to the hide on recompile with no edit. `ServerSettingsSource.Get<T>()` loads
lazily: the first read in the process takes a lock, asks whether this is a dedicated server
(`GameNetwork.IsDedicatedServer`), and on a server reads the file into one fresh
instance of each class before it returns. Later reads cost one volatile field read. On a client the first
read records "no override", the file is never touched, and MCM's own instance comes back as before.

A read made while the load itself is running (a settings constructor, for example) gets null instead of
recursing. A missing, empty, malformed or unreadable file still serves the server set at its server defaults.
Only a throw inside the loader serves no instance: it becomes an error line in the report and the MCM
fallback, which is null on a server, so each provider's own fallback applies. It never throws and never retries.

`ServerSettingsModule` runs at ProcessLoad. On a server it forces the load if nothing has read a setting
yet, logs the boot line and the per-key lines, writes the template if there is no file, and refreshes the
defaults reference. On a client it does nothing.

The co-op fingerprint (`SettingsFingerprint`, logged from `SubModule.cs`) reads `X.Instance`, so on a server
it hashes the values from the file with no further change.

**Side effect on clients.** TAOM's own crash-bundle collector (`McmSettingsCollector`) looks for a static
`Instance` without `FlattenHierarchy`, so before this change it found no TAOM settings class. With the hide
it finds all four, and client crash bundles now list TAOM's settings, each dropdown as its selected option.
A crash bundle written on a server lists them too, with the values from the file. Crash bundles list
TAOM's four classes only: the collector reads only settings classes from TAOM's own assembly and skips
third-party classes by design (#982), so no third-party settings class is read inside crash handling and no
third-party value is copied.

**Reflection on `Instance`.** Each class now has two public static `Instance` properties, its own and MCM's.
A lookup with `BindingFlags.FlattenHierarchy` throws `AmbiguousMatchException`. No caller does that today.

### Component Diagram

```
taom-server-settings.json
        |
  ServerSettingsFileStore (path rule, the only file IO)
        |
  ServerSettingsSource (lazy, once per process, servers only)
        |
  ServerSettingsReader (schema-driven validation) -> ServerSettingsSet (four fresh instances)
        |
  TaomSettings.Instance and the three diagnostics pages (the hides)
```

## Configuration

### Current Values

Each setting's server default equals its compiled MCM default, with one exception: `EnableHitchProbe` is
`false` on a server (its compiled default is `true`). `ServerSettingsSet.cs` holds that override, and the gate
test `ServerSet_DiffersFromFreshInstances_OnlyInEnableHitchProbe` fails if another one appears without being
documented here. The full list of keys with their server defaults is generated on every server boot in
`taom-server-settings.defaults.json`, so this page does not copy it.

### Config File: `taom-server-settings.json`

**Where it lives.** The first rule that applies wins, and the boot line names it:

| Rule | Folder |
|---|---|
| `BANNERLORD_USER_DIR` | The folder in that variable, when it is set and not blank. The launcher sets it, and `server-config.json` lives there too. |
| `TAOM module folder` | Two folders above `TAOM.dll`, which is `Modules\TAOM`. |
| `current directory` | The process's working directory, only if the module folder cannot be found. |

**Format.** One flat JSON object from property name to value. Names are the C# property names (for example
`CastleNotablesPerCastle`) and are case-sensitive. `//` and `/* */` comments are allowed.

```json
// Only the keys you change.
{
  "CastleNotablesPerCastle": 4,
  "AiLordPartyScale": 2.5,
  "AiPlayerClanPartyScaling": "Always"
}
```

| Kind | Accepted value |
|---|---|
| bool | `true` or `false` only |
| int | a whole number inside the setting's MCM range |
| float | a number, finite, inside the setting's MCM range after narrowing to `float` |
| text | a string of at most 256 characters; `null` is rejected |
| dropdown | one of the option strings, exact case; an index is rejected |
| button | not settable: a warning, then ignored |

Two `TaomSettings` properties are parked (`EnableShaderPrecompilation`, `EnableShaderPrecompileScenePasses`):
their MCM attributes are commented out, MCM does not load them, and the file does not either. A key for one
warns and is ignored.

**Numbers.** Write plain decimals. Newtonsoft reads a leading-zero integer as octal (`030` is 24) and `0x10`
as hex, and `08` makes the whole file malformed.

**Failures.** A bad value keeps its server default with a warning, and one summary warning gives the count.
An unknown key, including one that differs only in case, warns and is ignored. A file that is not one JSON
object (bad syntax, a top-level array, a duplicate key) or cannot be read gives one error line and leaves
every setting at its server default. If the load itself throws, no instance is served and each provider's
own fallback applies. An empty file is zero keys. A failed write warns and the boot carries
on.

**The two generated files.** When no settings file exists, TAOM writes a template once: comment lines and
`{}`. It never rewrites a file that exists. On every server boot it also writes
`taom-server-settings.defaults.json` beside it, only when the content has changed: every settable key at its
server default, each with a `/* kind, range or options, group */` comment. TAOM never reads that file.

**Keep the operator file short.** Copy into `taom-server-settings.json` only the keys you change. A key left
out follows the server default (the compiled default, except the hitch probe), so a later TAOM release that changes a default reaches every key you did not
set. Each applied key logs its server default next to the file value, so a stale override is easy to spot.

**Reload scope.** The file is read once per process. Restart the server after an edit. It is also re-read on
every boot, so a crash-report QA trigger (`ThrowOnNext*`) set in it fires on every boot.

**Bare engine runs.** Without `BANNERLORD_USER_DIR` the file falls back to `Modules\TAOM`, which the server's
staging (`robocopy /MIR`) purges on the next re-stage. Set the variable, or start through the launcher.

**ModuleData configs no longer apply on a server.** Several providers fell back to a ModuleData JSON or XML
config (for example `signature_strikes_config.json`) when MCM was absent. On a server the settings instance now
exists, so the compiled MCM default or the file value applies instead. The shipped config values match the
compiled defaults; a customized config needs its keys moved into `taom-server-settings.json`.

**Effective values.** Cross-field invariants stay in the providers. For example
`CombatMechanicsSettingsProvider` lifts the auto-knockdown ratio (`ChargeAutoKnockdownRatio`) to a floor, so a logged
`set K = v` can differ from the value the game uses.

### Boot line

```
[ServerSettings] TAOM server settings (MCM is not loaded on a dedicated server): loaded 7 key(s) from <path> (BANNERLORD_USER_DIR), 1 reverted, 0 unknown
[ServerSettings] set CastleNotablesPerCastle = 4 (server default 3)
```

Every boot line starts `TAOM server settings (MCM is not loaded on a dedicated server): `, then one of:

| State | Rest of the line | What the server runs |
|---|---|---|
| Loaded or empty file | `loaded <n> key(s) from <path> (<rule>), <r> reverted, <u> unknown` | the server defaults plus the applied keys |
| Missing | `no file at <path> (<rule>), every setting is at its server default`, then `wrote an empty template to <path>` | the server defaults |
| Unreadable or malformed | `could not use <path> (<rule>), every setting is at its server default`, after an error line with the cause | the server defaults |
| LoaderFailed (the loader threw) | `could not load the server settings from <path> (<rule>), no settings instance is served, so each provider's own fallback applies`, after an error line naming the exception | each provider's `??` fallback |

When the path itself could not be resolved, `<path> (<rule>)` reads `an unresolved path`. These lines are for
the operator and are not localized.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/ServerSettings/ServerSettingsSource.cs` | Lazy, lock-guarded load; the `Get<T>()` the hides call; test seams |
| `Main/Features/ServerSettings/ServerSettingsSchema.cs` | Reflection over the four classes: one descriptor per settable property |
| `Main/Features/ServerSettings/ServerSettingsReader.cs` | Pure parse and per-kind validation |
| `Main/Features/ServerSettings/ServerSettingsDefaultsRenderer.cs` | Template and defaults reference text |
| `Main/Features/ServerSettings/ServerSettingsFileStore.cs` | Path rule, read, write-if-missing, write-if-changed |
| `Main/Features/ServerSettings/ServerSettingsReport.cs` | File state, counts, log lines, the boot line |
| `Main/Features/ServerSettings/ServerSettingsModule.cs` | The ProcessLoad step |
| `Main/Features/TaomSettings.cs` and the three diagnostics settings classes | The `Instance` hides |

## Dependencies

- `GameNetwork.IsDedicatedServer` (engine) for the server check, read the same way as `DedicatedServerProvider`
- Newtonsoft.Json 13 for parsing, with comments ignored and duplicate keys rejected
- `FiniteFloatValidator` (Core) for the NaN and infinity checks

## Tests

- `TAOM.Tests/Features/ServerSettings/ServerSettingsSchemaTests.cs`: 6 tests. Every settable property has one known kind (the parked pair is pinned), the schema matches the fingerprint's reflection per class (509 in all), and every compiled default passes its own validation.
- `TAOM.Tests/Features/ServerSettings/ServerSettingsReaderTests.cs`: 35 tests, one per validation rule, plus unknown keys, summaries, malformed files, comments and empty files.
- `TAOM.Tests/Features/ServerSettings/ServerSettingsDefaultsRendererTests.cs`: 5 tests. The defaults reference reads back with nothing reverted and shows the hitch probe off; the template reads as zero keys.
- `TAOM.Tests/Features/ServerSettings/ServerSettingsFileStoreTests.cs`: 12 tests in real temp folders: the path rule, write-once, the creation race, a failed template write leaving no file, write-if-changed, unwritable and unreadable paths.
- `TAOM.Tests/Features/ServerSettings/ServerSettingsSourceTests.cs`: 18 tests. The hides exist, an IL gate allows only the four hide getters to call MCM's `Instance`, a client never reads the file, a server loads once before the first read returns, the hitch probe stays off on a server unless the file enables it, re-entrant reads, a loader throw reports `LoaderFailed` while an IO error stays `Unreadable` and still serves the set, and the gate `ServerSet_DiffersFromFreshInstances_OnlyInEnableHitchProbe`: every settable property of the four classes in `new ServerSettingsSet()` equals a fresh instance's, except `EnableHitchProbe`.
- `TAOM.Tests/Features/ServerSettings/ServerSettingsModuleTests.cs`: 9 tests. Client and server boot behavior, the boot line after a loader throw, the module list, and the fingerprint hashing file values on a server.

## Performance

On a client the first `X.Instance` read records "no override" and every later read costs one volatile field
read before MCM's own getter. On a server the first read takes one lock-guarded load per process (read and
validate the file, build four instances), and every later read costs one volatile field read. Nothing is
re-read during play.

## How to Change a Setting on a Server

1. Start the server once with TAOM installed. It writes `taom-server-settings.json` and
   `taom-server-settings.defaults.json` in the folder the boot line names.
2. Find the setting in the defaults reference and copy its line into `taom-server-settings.json`.
3. Change the value, then restart the server.
4. Check the boot line: the key appears as `set <Name> = <value>`, and the reverted and unknown counts are 0.

## Changelog

- 2026-10-23: First version (ADR-013 batch B8). All four settings classes; the client crash bundle now lists TAOM settings as a side effect. A loader throw has its own state and boot line (`LoaderFailed`), separate from an unreadable file.

## GitHub Issue

- **Issue:** #968, [Co-op: run TAOM's code on the dedicated server (ADR-013)](https://github.com/haterade22/TAOM/issues/968)
- **Status:** Open. Owed: a staged server boot showing the boot line, and a client boot showing the MCM page works.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/013-server-safe-core-client-ui-submodule.md](../adrs/013-server-safe-core-client-ui-submodule.md)
- [docs/features/armour-acquisition.md](./armour-acquisition.md)
- [docs/features/combat-mechanics.md](./combat-mechanics.md)
- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/features/crash-report.md](./crash-report.md)
- [docs/features/mcm.md](./mcm.md)
- [docs/features/mission-perf-heartbeat.md](./mission-perf-heartbeat.md)
- [docs/features/war-of-the-ring-momentum.md](./war-of-the-ring-momentum.md)
- [docs/features/war-of-the-ring.md](./war-of-the-ring.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)
- [docs/research/taom-headless-server.md](../research/taom-headless-server.md)

<!-- backlinks-end -->
