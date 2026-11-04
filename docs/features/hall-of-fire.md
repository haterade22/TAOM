# Hall of Fire

## Overview

A map conversation held in a settlement of a TAOM culture gets a backdrop from a fitting vanilla culture: a Gondor
lord talks in front of an imperial town, an Erebor notable in front of a Sturgian hall, an Umbar merchant in front of
an Aserai street. The conversation scene is still vanilla's; only the atmosphere name it asks for changes.

## Why This Exists

- **Vanilla behavior:** `SandBox.View.Map.DefaultMapConversationDataProvider.GetAtmosphereNameFromData` (v1.5.5)
  builds the atmosphere name from the settlement's culture. A settlement gives
  `conv_<Settlement.Culture.StringId>_town_<night|sunset|noon>_0`, or `conv_<culture>_tavern_0` or
  `conv_<culture>_lordshall_0` inside, or `conv_<culture>_shipyard_<time>_0` at a shipyard. A null settlement or a
  hideout gives a wilderness set by terrain: `conv_<snow|desert|steppe|forest|plains>_<time>_0`.
  `MapConversationTableau` passes the name to `Scene.SetAtmosphereWithName`.
- **What Native ships:** `Modules/Native/Atmospheres` holds the `conv_*` atmospheres of the six vanilla culture ids
  (empire, vlandia, sturgia, aserai, khuzait, battania) plus the wilderness sets. No module ships one for a TAOM
  culture. Shipyard backdrops ship only with the naval DLC, whose port location TAOM never loads. The native call is a no-op on a name it cannot find, so the cached conversation scene keeps its previous
  atmosphere.
- **TAOM requirement:** the backdrop behind a conversation fits the realm the conversation is in.
- **Without this feature:** a Gondor town asks for `conv_gondor_town_noon_0` and finds nothing: the previous
  conversation's sky stays in place, and for the first conversation after a load the conversation scene's own default
  atmosphere shows. The six reused vanilla ids keep vanilla backdrops, two of them unsuitable: Dunland (`empire`) gets marble Calradian
  towns and Khand (`battania`) a Celtic forest.

## Architecture

### Design Challenge

The provider is a plain interface held in one static slot. `MapConversationTableau.FirstTimeInit` calls the provider
once per conversation, and again after the partner's equipment changes; `OnTick` retries `FirstTimeInit` every tick
until it completes, so a throw here would repeat every tick. The replacement must never throw and must leave vanilla's
answer alone wherever TAOM has no row.

`FirstTimeInit` also reveals the scene entities tagged with the atmosphere name: `scn_conversation_tableau` holds six
hidden hall interiors tagged `conv_<vanilla id>_lordshall_0`, so a lord's-hall conversation of a TAOM culture showed no
hall, and with the swap it shows the borrowed culture's hall. That is why the name is rewritten rather than shipping
`conv_<culture>_*` atmosphere files: a new atmosphere would bring a sky but no hall.

### Solution Approach

No patch. `SandBoxViewSubModule` exposes the slot through a static getter (`MapConversationDataProvider`) and a public
static setter (`SetMapConversationDataProvider`). At `ApplyPhase.MainMenu` the module's `HallOfFireInstaller` wraps the
current provider in `TaomMapConversationDataProvider` (TAOM.Client, built through `IMapConversationProviderWrapper`; core registers a null wrapper and logs a warning when no client wrapper is present). The wrapper calls the inner provider outside any `try`, then
asks `HallOfFireService.Resolve(tag, culture id)`. The service swaps the prefix `conv_<culture>_` for
`conv_<set>_` once, when the culture has a row and the tag starts with that prefix as the engine wrote it. A hideout
needs no case of its own: vanilla gives it a wilderness tag, so the prefix never matches. A failure in the rewrite logs once and returns vanilla's tag.

The install is skipped on a dedicated server, for an empty slot and when the wrapper is already installed. The slot is
behind `IMapConversationProviderSlot` (ADR-007), so the installer is tested against a fake. The map is looked up by the
normalized culture id, but the prefix match uses the raw id, because the engine writes
`Settlement.Culture.StringId` verbatim. A remapped vanilla id (`empire`, `battania`) swaps once and never chains.

### Component Diagram

```
hall_of_fire.json -> HallOfFireConfigProvider -> HallOfFireService
                                                        |
SandBoxViewSubModule slot <- HallOfFireInstaller -> TaomMapConversationDataProvider(inner = vanilla)
   (IMapConversationProviderSlot)                     tag = inner(data); Resolve(tag, culture)
```

## Configuration

### Config File: `Main/_Module/ModuleData/hall_of_fire/hall_of_fire.json`

`{ "cultures": { "<culture id>": "<vanilla set>" } }`. The set is one of `empire`, `vlandia`, `sturgia`, `aserai`,
`khuzait`, `battania`, each of which ships all five conversation backdrops. Keys and values are trimmed and
lower-cased. A row is dropped with a warning when its key is empty, its value is not one of the six sets, its key
equals its value (a no-op), or its key differs from an earlier one only in case or whitespace (it is dropped with a
warning and the first is kept). An exact duplicate never reaches the loader: the JSON reader keeps the last value. A
missing, unparseable or null file gives the built-in `HallOfFireMap.Default()`. There is no in-game setting. To turn the
feature off, set `"cultures": {}`; a missing or empty file loads the built-in rows. A JSON edit needs a game restart.

### Current Values

| Set | Cultures | Why |
|---|---|---|
| empire | gondor, arthedain, cardolan, rhudaur, rivendell, lindon | Stone towns and halls of the Men of the West and the Elves |
| battania | mirkwood, lothlorien, dolguldur, empire (Dunland) | Forest settings; Dunland's marble Calradian default does not fit |
| sturgia | erebor, eredluin, anduinvale, angaladh, dorwinion, gundabad, mistymountainorcs, goblin, bluecraig | Cold, heavy halls for the mountain peoples |
| aserai | umbar, shaghana, abanissa | Southern desert towns |
| khuzait | mordor, battania (Khand) | Bare, hard country; Khand's Celtic forest default does not fit |
| vlandia | isengard, southrhovanion | Fortified keep look |

`empire` is Dunland and `battania` is Khand in TAOM. Rohan (`vlandia`), Dale (`sturgia`), Rhun (`khuzait`) and Harad
(`aserai`) keep their vanilla backdrops and have no row.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/HallOfFire/HallOfFireMap.cs` | The map, the allowed sets, key normalizing and the built-in default |
| `Main/Features/HallOfFire/HallOfFireService.cs` | The pure tag rewrite |
| `Main/Features/HallOfFire/HallOfFireConfigProvider.cs`, `IHallOfFireConfigProvider.cs` | Validating JSON loader |
| `Main/Features/HallOfFire/IMapConversationProviderSlot.cs`, `MapConversationProviderSlot.cs` | ADR-007 adapter over the engine's static slot |
| `Client/Features/HallOfFire/TaomMapConversationDataProvider.cs` | The wrapper, never throws after the inner call (TAOM.Client); `HallOfFireConversationWrapper` builds and recognizes it |
| `Main/Features/HallOfFire/HallOfFireInstaller.cs` | The install decision |
| `Main/Features/HallOfFire/HallOfFireModule.cs` | Registrations and the MainMenu install |
| `Main/_Module/ModuleData/hall_of_fire/hall_of_fire.json` | The shipped rows |

## Dependencies

None. `IDedicatedServerProvider` (CoopInterop) gates the install; no other feature is read.

## Tests

- `TAOM.Tests/Features/HallOfFire/HallOfFireServiceTests.cs`: town, tavern, lordshall and shipyard tags, unmapped,
  null cases, the five wilderness tags a hideout gets, case and whitespace, a remapped vanilla id that does not chain
- `TAOM.Tests/Features/HallOfFire/HallOfFireConfigProviderTests.cs`: one test per validation rule, and
  `{"cultures": {}}` (the off switch) giving an empty map, no warning and an info line with 0 rows
- `TAOM.Tests/Features/HallOfFire/HallOfFireBindingTests.cs`: the installed `MapConversationTableau` constructor reads
  `SandBoxViewSubModule.MapConversationDataProvider`, and `FirstTimeInit` calls `GetAtmosphereNameFromData` and
  `Scene.SetAtmosphereWithName`
- `TAOM.Tests/Features/HallOfFire/ShippedHallOfFireConfigTests.cs`: the shipped JSON equals the default with no
  warning, every key is a real culture id, and every culture in `IVolunteerRecruitmentService.GetPooledCultureIds()`
  (the culture-conversion targets) has a row or is a vanilla id; `LiveInstall` tests read the live `TAOM_Map`
  settlements and require a row or a vanilla id for every culture that owns a non-hideout settlement, and check that
  each of the six sets ships its five backdrops in `Modules/Native/Atmospheres`
- `TAOM.Tests/Features/HallOfFire/HallOfFireWiringTests.cs`: module listing, singletons, install at `MainMenu` only,
  no behavior, model or patch
- `TAOM.Tests/Features/HallOfFire/HallOfFireInstallerTests.cs`: wraps once, idempotent, never sets null, skipped on a
  dedicated server, a throwing slot is logged
- `TAOM.Tests/Features/HallOfFire/TaomMapConversationDataProviderTests.cs`: the no-settlement path, the inner call
  outside the guard, a Gondor settlement rewritten to the imperial tag, and a throwing config returning the inner tag
  with exactly one logged error across two calls

## Adding a culture

Add a row to the JSON and to `HallOfFireMap.Default()` (a test keeps the two equal). The CI test
`ShippedConfig_EveryConversionTargetCultureHasARowOrIsVanilla` fails until every culture-conversion target has a row or
is one of the six vanilla ids; the `LiveInstall` test `EveryCultureOwningANonHideoutSettlement_HasARowOrIsVanilla`
does the same for every culture that owns a settlement in `TAOM_Map`.

## In-game check

Not yet checked in game. Load a save and check, for each of a Gondor town, an Erebor town, a Dunland town and a Khand
town (the two remapped vanilla ids), plus a Rohan or Dale town as the control (no row, so it keeps its vanilla
backdrop):

- the first conversation after a load shows the mapped set's town backdrop, not the scene's default atmosphere;
- a conversation in open plains, then back in the town, shows the town again, not the previous conversation's sky;
- the tavern and the lord's hall use the mapped set's interiors;
- a lord's hall in a Gondor castle shows the imperial hall;
- a hideout conversation shows the wilderness backdrop for its terrain (vanilla's own name, which the swap never
  matches);
- the log has one `HallOfFireInstaller: wrapped` line and no `TaomMapConversationDataProvider` error.

## Not done

Cloth simulation in the conversation tableau is left alone until #635 is understood.

## Changelog

- 2026-10-20: added (#826).

## GitHub Issue

- **Issue:** #826 Map conversations in TAOM-culture settlements show a wrong backdrop
- **Status:** Closed, `triage-needs-ingame`

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
