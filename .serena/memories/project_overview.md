# TAOM - Tales From the Age of Men

## Purpose
Total conversion mod for Mount & Blade II: Bannerlord v1.3.12, transforming the game into a Lord of the Rings setting.

## Tech Stack
- **Language**: C# (.NET Framework 4.7.2)
- **Game Engine**: TaleWorlds Bannerlord modding API
- **Patching**: Harmony (runtime method patching)
- **IoC**: Custom dependency injection via `Main/IoC.cs`
- **Testing**: MSTest + NSubstitute (mock framework)
- **Data**: XML + XSLT transformations for game data
- **Build**: PowerShell script (`build.ps1`), MSBuild

## Architecture
Entry points (`[HarmonyPatch]`, `GameModel`, `CampaignBehavior`) → `IHookInterface` → `Service` → `IAdapter` (wraps sealed TaleWorlds types).

Services must use adapter interfaces (e.g., `IHeroAdapter`), never raw TaleWorlds types directly.

## Key Directories
- `Main/` — Mod source code
- `Main/Features/` — Feature modules (BannerInjection, BattleScenes, CharacterSelection, FactionMap, HeroRace, TroopProgression)
- `Main/Adapters/` — Adapter interfaces and implementations for sealed TaleWorlds types
- `Main/Core/` — Core infrastructure (Domain, Infrastructure, Logging)
- `Main/_Module/ModuleData/` — XML/XSLT game data
- `TAOM.Tests/` — Unit tests (MSTest)
- `tools/` — Development utilities (PowerShell scripts)
- `docs/` — Documentation, ADRs, migration guides

## Critical Rules
- TDD mandatory (RED → GREEN → REFACTOR)
- No `#region`, no `[Obsolete]`, no `#if DEBUG` (except IoC.cs)
- Adapter pattern required for sealed TaleWorlds types
- Entry points < 150 lines
- Research TaleWorlds behavior via decompilation before guessing