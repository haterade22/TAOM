# TAOM — Tales From the Age of Men

A Lord of the Rings total conversion mod for **Mount & Blade II: Bannerlord v1.5.3**.

![The TAOM world map — Middle-earth at the time of the War of the Ring](tools/factionmap_output/verification_full.png)

## What is it

TAOM reimagines Bannerlord as Middle-earth during the War of the Ring. More than twenty kingdoms wage
war across a custom map — hundreds of unique troops, rideable war beasts (war elephants, giant
spiders, wargs), race-specific lifespans, alignment-driven diplomacy, a full career/class progression
system, per-kingdom special resources, and dozens of other systems. Every kingdom, clan, lord, and
troop has been replaced or rewritten to fit Tolkien's world.

> Development happens on **`bannerlord-1.5.x`** (Bannerlord v1.5.3). The GitHub default branch, **`bannerlord-1.4.5`**, is the v1.4.8 line and shows its own README.

## Working with AI

Start any AI client with [AGENTS.md](AGENTS.md) and the
[shared workflow](.ai/README.md). Any provider can build, review or adjudicate;
the task determines the role. For Codex, use the
[operating guide](docs/ai-includes/codex-operating-guide.md) and the five
repository skills under `.agents/skills/`. Claude imports the same shared rules.

Paid reviewers run only when explicitly requested. Review and verification work
uses the [non-deploying checks](.ai/verification.md), not the deploying build
commands in the developer quick start below.

## Quick Start (Developers)

**Prerequisites**

- Mount & Blade II: Bannerlord **v1.5.3** installed (the Steam beta branch)
- Visual Studio 2022 (or the .NET SDK + MSBuild) — targets .NET Framework 4.7.2
- `BANNERLORD_GAME_DIR` environment variable pointing at your game install
  (the `setup-dev-env.ps1` script configures this)

> **Two machines.** Development happens on a desktop that holds the full content set and a laptop
> that deliberately does not. Absolute paths in `docs/` are the desktop's `E:\` layout; the laptop
> resolves its own through `BANNERLORD_GAME_DIR` and `TAOM_DECOMPILE_ROOT`. A machine without the
> complete `TAOM_Map` and `LOTRLOME_Armory` installs will report thousands of broken content
> references that are not repo defects, so check which machine you are on before believing one.
> See [docs/reference/development-machines.md](docs/reference/development-machines.md).

**Build & test**

```powershell
git clone -b bannerlord-1.5.x https://github.com/haterade22/TAOM
cd TAOM

.\setup-dev-env.ps1        # set BANNERLORD_GAME_DIR (asks for your install path)
.\build.ps1                # build the mod
.\build.ps1 -RunTests      # build + run the test suite
dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=   # tests only, no deploy
```

A successful build deploys the module into your game's `Modules/` folder. Enable **TAOM** in the
Bannerlord launcher and start a **new campaign** (existing saves are not supported).

`TAOM.sln` at the root contains both `Main` (mod code) and `TAOM.Tests`. Tests run with MSTest +
NSubstitute. Shared build settings live in [`Directory.Build.props`](Directory.Build.props).

## Project Structure

```
TAOM/
├── Main/                     # Mod source (.NET Framework 4.7.2)
│   ├── Features/             # Feature modules (CareerSystem, SpecialResources, LotrIssues, Elephant, …)
│   ├── Core/                 # Core infrastructure + IoC
│   ├── Adapters/             # Sealed-type adapters (IHeroAdapter, etc.)
│   └── _Module/              # Bannerlord module files (SubModule.xml, ModuleData, GUI)
├── TAOM.Tests/               # Unit tests (MSTest + NSubstitute)
├── docs/
│   ├── adrs/                 # Architecture Decision Records
│   ├── features/             # Feature documentation
│   └── migration/            # Bannerlord version-migration tracking
├── tools/                    # Rebalancing + localization scripts
├── .ai/                      # Shared AI policy, roles, scope and review packets
├── .agents/skills/           # Codex-discoverable TAOM workflow instructions
├── .claude/                  # Claude Code config (skills, agents, rules, hooks, memory)
├── .codex/                   # Codex configuration and onboarding pointer
├── CLAUDE.md                 # Shared AGENTS.md import + Claude-specific workflows
├── AGENTS.md                 # Provider-neutral AI instruction entry point
└── build.ps1                 # Build script
```

## Architecture

All mod logic follows one pattern:

```
[HarmonyPatch / GameModel / CampaignBehavior] → IHookInterface → Service → IAdapter
```

Services never touch TaleWorlds sealed types directly — they work through adapter interfaces, which
keeps business logic fully unit-testable.

**Non-negotiable rules:**

- TDD mandatory (red → green → refactor)
- Entry points under 150 lines — delegate to services
- No `#region`, no `[Obsolete]`, no `#if DEBUG` (except IoC registration)
- Adapter pattern for any TaleWorlds sealed type
- Research TaleWorlds internals before implementing — never guess signatures

See the [Architecture Decision Records](docs/adrs/) for the full set of design constraints.

## Features

### Factions

| Free Peoples | Dark Powers | Independent |
|--------------|-------------|-------------|
| Gondor · Rohan · Erebor · Dale · Rivendell · Lothlórien · Mirkwood · **Lindon** | Mordor · Isengard · Dol Guldur · Gundabad · **Misty Mountain Orcs** · **Goblin-town** · **Blue Craig** · Dunland · Easterlings (Rhûn) · Harad · Khand | Umbar (corsairs) · Shaghâna · Âbanissa |

Over 100 clans and 800+ unique troop definitions across all factions. Settlements like Erebor are
hand-kitbashed in the editor — see the [build reference](docs/kitbash/erebor/).

### Headline systems

- **Career System** — culture-specific careers; pick one at character creation, progress a
  tiered choice tree, unlock passive bonuses + an active battlefield ability (press **V**).
- **Legendary War Beasts** — ride wargs, Harad **war elephants** (trample + tusk auto-attacks), and
  Dol Guldur **giant spiders** (auto-bite); each driven by behavior-tree AI and fielded as cavalry.
- **Special Resources** — 11 per-kingdom resources (War Spoils, Gems, Elven Wine, …) that gate
  elite troop upgrades; XML-driven with many-to-one kingdom/culture mappings.
- **Cultural Feats** — lore-driven per-culture bonuses (Rohan cavalry speed, Erebor smithing, Mordor
  raid damage, Gondor loyalty, …), each backed by a dedicated GameModel override.
- **Culture Conversion** — conquered towns, castles, and their villages gradually adopt the
  conqueror's culture, switching recruitment pools to match.
- **Smart Battle AI** — coordinated cavalry line-charges, companion-led formation tactics, and mixed
  formations that interleave unit types instead of segregating them.
- **War of the Ring** — scripted phased escalation into permanent total war between Free Peoples and
  Dark Powers; configurable via JSON + MCM.
- **Race & Age System** — race-appropriate lifespans and fertility (immortal elves, 250-year
  dwarves, fast-breeding orcs, ageless Nazgûl).
- **Named Companions** — 18 lore companions (Aragorn, Legolas, Gimli, …) as recruitable wanderers.
- **Bandit Management** — five LOTR bandit cultures replace vanilla hideouts, with configurable
  density and themed encounter flavor.
- **Localization** — full text support across **12 languages** with graceful English fallback.

…and dozens more systems (castle recruitment, culture marketplace, troop-weight balancing,
messengers, quick-action inventory, banner color persistence, settlement guards, custom battles,
siege defense, tournament armor, shader precompilation, and more). Each is documented under
[`docs/features/`](docs/features/). LOTR rules are enforced through GameModel overrides and
Harmony patches, both catalogued in [harmony-patch-registry.md](docs/reference/harmony-patch-registry.md) and [gamemodel-registry.md](docs/reference/gamemodel-registry.md).

## How It's Built (AI-assisted pipeline)

TAOM is developed with a structured, AI-assisted engineering pipeline.

- **[Claude Code](https://docs.anthropic.com/en/docs/claude-code)** is integrated as more than a
  code generator: custom slash-command skills, specialized agents, automated hooks,
  path-scoped rule files, persistent cross-session memory, and project MCP servers (symbolic code
  navigation, decompilation, git, GitHub). [AGENTS.md](AGENTS.md), with Claude's [CLAUDE.md](CLAUDE.md) layer, is the authoritative reference
  every session loads.
- **Codex** (OpenAI) runs as an *independent adversarial reviewer* — it shares no session context
  with Claude, so it provides a genuine second opinion. Review
  instructions live in [AGENTS.md](AGENTS.md).
- **Mandatory completion workflow** — every C# feature passes a 4-phase gate before merge:
  build + internal `/deep-review` → Codex adversarial review → self-review of the fixes →
  closeout (issue, feature doc, CHANGELOG).

## Installing to Play (non-developers)

TAOM ships as four modules, all at the same version: `TAOM`, `TAOM.Dependencies`, `TAOM_Map` and
`LOTRLOME_Armory`. `TAOM.Dependencies` carries Harmony, ButterLib, UIExtenderEx and Mod
Configuration Menu (MCM) inside it, so none of those is installed separately: a standalone Workshop
or Nexus copy of any of them must be removed before TAOM is enabled.

Bannerlord **v1.5.3** is required (the Steam beta branch). Place the four modules
in your Bannerlord `Modules/` directory, enable them in the launcher, and start a **new campaign**:
existing saves are not supported.

## Contributing

1. Read [AGENTS.md](AGENTS.md) for coding standards and conventions
2. Write tests first — TDD is mandatory
3. Use the adapter pattern for any TaleWorlds sealed type
4. Keep Harmony patches and entry points thin (< 150 lines); delegate to services
5. Research TaleWorlds behavior before implementing — decompile, don't guess

## License

TAOM is licensed in parts. [LICENSE-CONTENT.md](LICENSE-CONTENT.md) is the overview and carries the
path-by-path scope; [LICENSE](LICENSE) is kept as plain MIT text so license detection keeps working.

| What | License |
|------|---------|
| **Code** (C# source, tests, tooling, build scripts, Gauntlet UI definitions) | [MIT](LICENSE) |
| **Content** (art, 3D and 2D assets, the world map, game data, lore text) | [CC BY-NC-SA 4.0](LICENSE-CONTENT.md): non-commercial, attribution required, share-alike |
| **Audio, fonts, and third-party binaries** | Not TAOM's to license. See [THIRD-PARTY-LICENSES.txt](Main/_Module/THIRD-PARTY-LICENSES.txt) |
| **The TAOM name, "Tales From the Age of Men", and the branding** | Granted by neither license. See [TRADEMARK.md](TRADEMARK.md) |

Forks are welcome and the code is genuinely free to use. Two things it does not come with: the
audio and fonts (which are not ours to pass on) and the project's name (which no open license
grants). "A fork of TAOM" is fine and always will be. Calling your fork TAOM is not.

Provenance record for the content: [docs/reference/asset-provenance.md](docs/reference/asset-provenance.md).

This mod is a fan project and is not affiliated with or endorsed by the Tolkien Estate,
New Line Cinema, Middle-earth Enterprises, or TaleWorlds Entertainment.

