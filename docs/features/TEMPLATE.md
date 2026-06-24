# Feature Name

## Overview

What this feature does in 2-3 sentences. What gameplay behavior does it change or add?

## Why This Exists

The problem this feature solves. Be specific — reference vanilla Bannerlord behavior, Middle-earth lore requirements, or gameplay gaps.

- **Vanilla behavior:** What Bannerlord does by default
- **TAOM requirement:** What we need instead and why
- **Without this feature:** What goes wrong (crashes, immersion breaks, gameplay issues)

## Architecture

### Design Challenge

What makes this non-trivial? API limitations, engine constraints, sealed types, etc.

### Solution Approach

How the feature is structured. Which Bannerlord extension points are used (GameModel override, CampaignBehavior, Harmony patch, etc.) and why.

### Component Diagram

```
config_file.json/xml
        |
  ConfigProvider (loads data)
        |
    Service (core logic)
       / \
      /   \
Model/Behavior  Adapter
(engine hook)   (TaleWorlds API wrapper)
```

## Configuration

### Config File: `Main/_Module/ModuleData/<path>`

Description of the config format and what each field controls.

| Field | Type | Description |
|-------|------|-------------|
| `exampleField` | string | What it does |

### Current Values

Table of current configuration values with notes explaining the choices.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/<Name>/Service.cs` | Core logic |
| `Main/Features/<Name>/IService.cs` | Service interface |
| `Main/Features/<Name>/Behavior.cs` | CampaignBehavior or GameModel |
| `Main/Features/<Name>/IoC.cs` | DryIoc registration |
| `Main/Adapters/IXxxAdapter.cs` | Adapter interface |
| `Main/Adapters/XxxAdapter.cs` | TaleWorlds API wrapper |
| `Main/_Module/ModuleData/<config>` | Configuration data |

## Dependencies

- `IServiceName` (Core/Feature) — What it provides
- `IAdapterName` (Adapters) — What TaleWorlds API it wraps

## Tests

- `TAOM.Tests/Features/<Name>/ServiceTests.cs` — N tests covering [summary]
- `TAOM.Tests/Features/<Name>/BehaviorTests.cs` — N tests covering [summary]

## How to [Common Operation]

Step-by-step guide for the most common modification (e.g., "How to add a new race", "How to add a new culture", "How to change X threshold").

1. Step one
2. Step two
3. No code changes needed / Code changes required in X

## Performance

_Optional section — include only if there are meaningful performance considerations._

Description of any optimizations, caching, or performance-sensitive patterns used.

## Changelog

Dated, feature-sliced history (newest first) — mirrors the entries about this feature from the repo-root `CHANGELOG.md`. Add a bullet here whenever the feature changes; the global `CHANGELOG.md` remains the chronological log of record.

- YYYY-MM-DD — summary of the change to this feature.

## GitHub Issue

- **Issue:** #NNN — [title](link)
- **Status:** Closed / Open
