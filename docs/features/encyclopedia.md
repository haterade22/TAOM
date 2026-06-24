# Encyclopedia

## Overview
Overrides Bannerlord's `DefaultInformationRestrictionModel` to add a MCM setting that reveals all characters in the encyclopedia regardless of whether the player has encountered them. When the setting is off, vanilla behavior is preserved.

## Why This Exists
- **Vanilla behavior:** `DefaultInformationRestrictionModel.DoesPlayerKnowDetailsOf(Hero)` returns `false` for heroes the player has not met, hiding their encyclopedia entries. Vanilla also provides the console cheat `campaign.toggle_information_restrictions` to bypass this.
- **TAOM requirement:** Players and modders often need to inspect all lords and characters in the encyclopedia without using the console, especially during testing or when exploring the new LOTR factions.
- **Without this feature:** No in-game toggle exists; players must open the console to reveal encyclopedia characters.

## Architecture

### Design Challenge
`DefaultInformationRestrictionModel` is registered as a `GameModel`. It must be subclassed and the override registered in `SubModule.cs` (or via the standard `GameModel` replacement mechanism). The `TaomSettings` MCM class lives outside the feature folder and its `Instance` property can be null before MCM is initialized, so the model must guard against null safely.

### Solution Approach
`TaomInformationRestrictionModel` extends `DefaultInformationRestrictionModel`. It accepts a `Func<bool>` delegate for the "show all" check so the MCM dependency can be injected in tests without requiring a live MCM instance. The production constructor wires the delegate to `TaomSettings.Instance?.ShowAllEncyclopediaCharacters ?? false`. When the delegate returns `true`, `DoesPlayerKnowDetailsOf` short-circuits and returns `true` without calling `base`. When it returns `false`, it delegates to `base.DoesPlayerKnowDetailsOf(hero)` exactly as vanilla.

### Component Diagram
```
TaomSettings (MCM) -- ShowAllEncyclopediaCharacters
        |
        | Func<bool> delegate
        v
TaomInformationRestrictionModel : DefaultInformationRestrictionModel
        |
        | DoesPlayerKnowDetailsOf(hero)
        |-- true  -> return true (show all, skip base)
        |-- false -> base.DoesPlayerKnowDetailsOf(hero)
```

## Configuration

The feature is controlled entirely through the MCM settings UI (Mod Configuration Menu). No config files.

| Setting | Group | Default | Description |
|---------|-------|---------|-------------|
| Show All Characters | Encyclopedia | `false` | Reveals all heroes in encyclopedia including unencountered ones |

The setting is stored by MCM in its own JSON store under the `TAOM` folder.

## Key Files
| File | Purpose |
|------|---------|
| `Main/Features/Encyclopedia/Models/TaomInformationRestrictionModel.cs` | The `GameModel` override — sole implementation file for this feature |
| `Main/Features/TaomSettings.cs` | MCM settings class containing `ShowAllEncyclopediaCharacters` |
| `TAOM.Tests/Features/Encyclopedia/TaomInformationRestrictionModelTests.cs` | Unit tests for both short-circuit and delegate-to-base paths |

## Dependencies
- `TaomSettings` (MCM `AttributeGlobalSettings<TaomSettings>`) — provides the setting value via `TaomSettings.Instance`
- `DefaultInformationRestrictionModel` (TaleWorlds) — base class

## Tests
| File | Coverage |
|------|---------|
| `TAOM.Tests/Features/Encyclopedia/TaomInformationRestrictionModelTests.cs` | `DoesPlayerKnowDetailsOf_WhenShowAllEnabled_ReturnsTrueWithoutCallingBase` — verifies short-circuit; `DoesPlayerKnowDetailsOf_WhenShowAllDisabled_DelegatesToBase` — verifies base is called (evidenced by the `NullReferenceException` that base throws without campaign state) |

## How to Add a New Encyclopedia Restriction Override

1. Add a new `override` method to `TaomInformationRestrictionModel` for the relevant `DefaultInformationRestrictionModel` method.
2. Gate the new behavior on an existing or new `TaomSettings` property.
3. Add a test that covers both the short-circuit and delegate-to-base paths using the `Func<bool>` constructor.

## Changelog

- 2026-05-13 — #145: replaced concrete-singleton coupling (`TaomSettings.Instance?.ShowAllEncyclopediaCharacters`) in `TaomInformationRestrictionModel` with an injected `IEncyclopediaSettingsProvider` (new `EncyclopediaSettingsProvider` + `EncyclopediaIoC`, registered in `Main/IoC.cs`); tests updated to NSubstitute on the new interface.

## GitHub Issue
- **Issue:** Unknown (commits reference `9f14d24` and `b68feab` but no issue number)
- **Status:** Active

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
