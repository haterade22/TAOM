# TAOM - Tales From the Age of Men

A Lord of the Rings total conversion mod for Mount & Blade II: Bannerlord v1.3.

## Overview

TAOM reimagines Bannerlord as Middle-earth during the War of the Ring. Sixteen factions wage war across a custom map with over 500 unique troops, race-specific lifespans, autonomous warg AI, alignment-driven diplomacy, and a dozen other systems built to make Calradia feel like Tolkien's world. Every kingdom, clan, lord, and troop has been replaced or rewritten to fit the setting.

## Factions & Cultures

### Free Peoples

| Faction | Identity | Notable Traits |
|---------|----------|----------------|
| **Gondor** | The last great kingdom of Men | Regular, Ranger, and Gondolin elite lines |
| **Rohan** | Horse lords of the Riddermark | Cavalry-focused with Royal Guard elite |
| **Rivendell** | Elven refuge of Elrond | Immortal warriors, 2x troop weight |
| **Mirkwood** | Woodland realm of Thranduil | Scout and Palace Guard lines |
| **Lothlorien** | Golden Wood of Galadriel | Elven guardians and archers |
| **Erebor** | Dwarven kingdom under the Mountain | 5 troop lines across 41 units |
| **Dale** | Frontier kingdom of Men | Defenders, archers, and militia |
| **Arthedain** | Northern Dunedain remnant | Human kingdom |

### Dark Powers

| Faction | Identity | Notable Traits |
|---------|----------|----------------|
| **Mordor** | Sauron's domain | Orcs, Uruks, Trolls, Black Numenoreans |
| **Isengard** | Saruman's war machine | Uruk-Hai infantry and warg riders |
| **Gundabad** | Orcish stronghold | Orc hordes with warg cavalry |
| **Dol Guldur** | The Necromancer's fortress | 14-unit Khamul shadow line |
| **Easterlings** | Eastern empire of Rhun | 113 troops across 11 unit groups |
| **Harad** | Southern desert kingdom | Spearmen, archers, camel cavalry |
| **Khand** | Eastern tribal lands | Tribal warriors |

### Neutral

| Faction | Identity | Notable Traits |
|---------|----------|----------------|
| **Umbar** | Corsair kingdom | Hostile to both alignments |

Over **100 clans** and **500+ unique troop definitions** across all factions.

## Features

### War of the Ring

Scripted phased escalation into total war. Isengard and Dunland strike Rohan first, then all hostile-tier kingdom pairs are drawn into permanent conflict. Three layers of peace-blocking ensure the War of the Ring cannot end with a handshake. Configurable via JSON and MCM settings.

### Race & Age System

Each race has its own lifespan and fertility rate. Elves are effectively immortal with very low birth rates. Dwarves live 250 years. Orcs burn out at 50-60 but breed aggressively. Nazgul and Saruman cannot die of age. Heroes die and reproduce at race-appropriate rates, keeping faction demographics consistent with the lore.

### Alignment-Aware Execution

Executing an enemy of your alignment (Good kills Evil) carries no honor penalty and no relation hit with your allies. Kinslaying — executing someone on your own side — inflicts 1.5x vanilla penalties. Umbar is treated as hostile by everyone. Backed by an alignment map of all 16 kingdoms.

### Troop Weight System

Elite units consume more party capacity. A cave troll costs 4 party slots. Elven warriors cost 2. Standard infantry costs 1. This prevents doomstacks of pure elite troops and forces balanced army compositions. Configurable weights per troop in XML, toggleable via MCM.

### Warg Combat

Wargs fight autonomously using a behavior tree AI framework. They select and attack nearby enemies on their own. Taking heavy damage triggers rage mode (10% chance): the warg takes control from its rider for 2-3 attacks. Uses spatial grid partitioning for efficient proximity queries and bone-based collision detection for accurate hits.

### Atmosphere Persistence

Scenes with baked atmospheres (Moria, Dead Marshes, Fangorn) keep their intended look year-round. A Harmony prefix patch on `Mission.Initialize` detects forced-atmosphere scenes and prevents the campaign weather system from overriding them.

### Startup Resources

Factions begin with lore-appropriate economies. Rivendell and Lothlorien start wealthy (6M gold). Isengard and Gundabad are well-funded war machines (2M). Gondor and Rohan are standard kingdoms (500K). Configured per-culture in XML.

### Diplomacy & Alliances

Kingdom relationships are defined in tiers: Permanent Alliance, Alliance, Neutral, Natural Enemy, Hostile, and Permanent War. The diplomacy system integrates with War of the Ring to enforce permanent hostilities between Free Peoples and Dark Powers while allowing internal diplomacy within each alignment.

### Character Creation

Players choose from 10 custom cultures, each with culture-specific body properties, equipment, starting skills, and LOTR-themed backstory options.

### XSLT Transformations

Vanilla Bannerlord elements are renamed at load time using XSLT, preserving game structure while replacing all visible content. 415 hero biographies, ~350 lord names, 73 noble clan names, 8 kingdoms, and 6 cultures are transformed with Tolkien-appropriate identities.

### Offspring Race Inheritance

Children inherit their race and physical appearance from their same-sex parent. Male children take their father's race, female children take their mother's. Implemented via a custom `HeroCreationModel`.

### GameModel Overrides

| Model | Change |
|-------|--------|
| `TaomCharacterStatsModel` | Max character tier raised to 10 (vanilla: 6) |
| `TaomPartyWageModel` | Extended wage tiers T0-T10 |
| `TaomVolunteerModel` | Max volunteer tier raised to 6 (vanilla: 4) |

## For Players

### Requirements

- Mount & Blade II: Bannerlord **v1.3.15+**
- [Bannerlord.Harmony](https://www.nexusmods.com/mountandblade2bannerlord/mods/2006)
- [Mod Configuration Menu (MCM)](https://www.nexusmods.com/mountandblade2bannerlord/mods/612)
- Alliance.Wargs module (bundled)

### Installation

1. Install the required dependencies listed above
2. Extract the TAOM module folder into your Bannerlord `Modules/` directory
3. Enable the mod in the Bannerlord launcher
4. Start a new campaign — existing saves are not supported

## For Developers

### Setup

```powershell
# Configure environment (sets BANNERLORD_GAME_DIR)
.\setup-dev-env.ps1

# Build
.\build.ps1

# Build + run tests
.\build.ps1 -RunTests

# Tests only
dotnet test TAOM.Tests
```

### Project Structure

```
TAOM/
├── Main/                    # Mod source (.NET Framework 4.7.2)
│   ├── Features/            # Feature modules (Warg, Diplomacy, etc.)
│   ├── Core/                # Core infrastructure and IoC
│   ├── Adapters/            # Sealed type adapters (IHeroAdapter, etc.)
│   └── _Module/             # Bannerlord module files
│       ├── SubModule.xml    # Module manifest
│       └── ModuleData/      # XML/XSLT/JSON configurations
├── TAOM.Tests/              # Unit tests (MSTest + NSubstitute)
├── docs/
│   ├── adrs/                # Architecture Decision Records
│   ├── features/            # Feature documentation
│   └── migration/           # v1.2 -> v1.3 migration tracking
├── tools/                   # Rebalancing scripts (lords, troops, armor, weapons)
└── build.ps1                # Build script
```

### Architecture

All mod logic follows this pattern:

```
[HarmonyPatch / GameModel / CampaignBehavior]
    -> IHookInterface
        -> Service (business logic)
            -> IAdapter (sealed type wrappers)
```

Services never touch TaleWorlds sealed types directly — they work through adapter interfaces. This keeps business logic fully unit-testable.

**Key architectural rules:**
- TDD mandatory (red-green-refactor)
- Entry points under 150 lines
- No `#region`, no `[Obsolete]`, no `#if DEBUG` (except IoC registration)
- Research TaleWorlds internals before implementing — never guess

See [Architecture Decision Records](docs/adrs/) for the full set of design constraints.

### Documentation

| Resource | Path |
|----------|------|
| Feature documentation | [docs/features/](docs/features/) |
| Architecture decisions | [docs/adrs/](docs/adrs/) |
| Migration tracking | [docs/migration/TRACKING.md](docs/migration/TRACKING.md) |
| AI assistant guide | [CLAUDE.md](CLAUDE.md) |

## Contributing

1. Read [CLAUDE.md](CLAUDE.md) for coding standards and project conventions
2. Write tests first — TDD is mandatory for all new features
3. Use the adapter pattern for any TaleWorlds sealed types
4. Keep Harmony patches and entry points thin (< 150 lines), delegate to services
5. Research TaleWorlds behavior before implementing — use decompilation, not guesswork

## License

**Code** (C# mod source): [MIT License](https://opensource.org/licenses/MIT)

**Content** (art, lore, data, XML assets derived from Tolkien's works): [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/) — non-commercial, attribution required, share-alike.

This mod is a fan project and is not affiliated with or endorsed by the Tolkien Estate, New Line Cinema, or TaleWorlds Entertainment.
