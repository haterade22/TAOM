# TAOM - Tales From the Age of Men

A total conversion mod for Mount & Blade II: Bannerlord v1.3.

## Overview

TAOM transforms Bannerlord into a new setting with custom cultures, factions, troops, and gameplay mechanics.

## Requirements

- Mount & Blade II: Bannerlord v1.3.12+
- .NET Framework 4.7.2 (for development)
- Visual Studio 2022 or VS Code (for development)

## Setup (Development)

### 1. Set Bannerlord Game Directory

Run the setup script to configure your environment:

```powershell
.\setup-dev-env.ps1
```

This sets the `BANNERLORD_GAME_DIR` environment variable pointing to your Bannerlord installation.

### 2. Build the Mod

```powershell
.\build.ps1
```

For release builds:

```powershell
.\build.ps1 -Configuration Release
```

### 3. Run Tests

```powershell
dotnet test TAOM.Tests
```

## Project Structure

```
TAOM/
├── Main/                    # Main mod project
│   ├── Features/            # Feature implementations
│   ├── Core/                # Core infrastructure
│   ├── Adapters/            # Sealed type adapters
│   └── _Module/             # Bannerlord module files
│       ├── SubModule.xml    # Module manifest
│       └── ModuleData/      # XML configurations
├── TAOM.Tests/              # Unit tests
├── docs/                    # Documentation
│   ├── adrs/                # Architecture Decision Records
│   ├── ai-includes/         # AI guidance documents
│   └── migration/           # v1.2→v1.3 migration guides
├── CLAUDE.md                # AI assistant guidance
└── build.ps1                # Build script
```

## Migration Status

This mod is being migrated from Bannerlord v1.2 to v1.3. See [docs/migration/TRACKING.md](docs/migration/TRACKING.md) for current status.

### XSLT Transformation Approach

TAOM uses XSLT transformations to modify vanilla XML at load time, allowing us to:
- Rename factions, cultures, clans, and characters with themed names
- Add biographical text to heroes
- Preserve vanilla structure for maximum compatibility

| XSLT File | Transforms | Status |
|-----------|------------|--------|
| `spkingdoms.xslt` | 8 kingdoms (Dunland, Gondor, Mordor, Dale, Harad, Rohan, Khand, Rhun) | Complete |
| `spcultures.xslt` | 6 cultures (Dunlending, Barding, Haradrim, Rohirrim, Variag, Easterling) | Complete |
| `spclans.xslt` | 73 noble clans | Complete |
| `splords.xslt` | ~350 lord names | Complete |
| `spheroes.xslt` | 415 hero biographies | Complete |

## Documentation

- [CLAUDE.md](CLAUDE.md) - AI assistant guidance and quick reference
- [docs/adrs/](docs/adrs/) - Architecture Decision Records
- [docs/migration/](docs/migration/) - Migration guides and API changes

## Contributing

1. Follow the coding standards in [CLAUDE.md](CLAUDE.md)
2. Write tests first (TDD is mandatory)
3. Use the adapter pattern for TaleWorlds sealed types
4. Keep entry points thin (<150 lines)

## License

[TBD]
