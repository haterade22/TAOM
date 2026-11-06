# Architecture Guide

Overview of the TAOM project architecture and design decisions.

---

## Architectural Overview

```
+------------------------------------------------------------------+
|                        ENTRY POINTS                               |
|  (Harmony Patches, GameModels, MissionLogics, CampaignBehaviors) |
|                        [THIN LAYER]                              |
+------------------------------------------------------------------+
                              |
                              | delegates to
                              v
+------------------------------------------------------------------+
|                         SERVICES                                  |
|      (Business Logic, Feature Implementation, Calculations)      |
|                    [CORE LOGIC LAYER]                            |
+------------------------------------------------------------------+
                              |
                              | uses
                              v
+------------------------------------------------------------------+
|                         ADAPTERS                                  |
|      (Wrap TaleWorlds sealed types for testability)              |
|                    [ABSTRACTION LAYER]                           |
+------------------------------------------------------------------+
                              |
                              | wraps
                              v
+------------------------------------------------------------------+
|                     TALEWORLDS ENGINE                            |
|           (Agent, Hero, Kingdom, CultureObject, etc.)            |
|                      [SEALED TYPES]                              |
+------------------------------------------------------------------+
```

---

## Dependency Flow

```
Entry Point → [Hook Interface] → Service → Engine → Adapter
     ↓               ↓              ↓        ↓         ↓
   Thin     Narrow seam (opt.)    Logic  Algorithm   Wrap
```

The hook interface is optional: a patch uses one only when it needs a narrow seam or a test fake; most patches call their service directly.

## Folder Structure

```
Main/
├── Adapters/                    # I<X>Adapter.cs beside <X>Adapter.cs
│   ├── IAgentAdapter.cs
│   ├── AgentAdapter.cs
│   ├── ICareerHeroAdapter.cs
│   ├── CareerHeroAdapter.cs
│   └── Models/                  # adapter-facing DTOs (MovementOrderType, NearbyAgentSnapshot)
├── BehaviorTrees/               # behavior-tree runtime
├── BehaviorTreeWrapper/
├── Composition/                 # feature modules, ModuleRunner
├── Core/                        # cross-cutting infrastructure
│   ├── Diagnostics/
│   ├── Domain/
│   ├── Infrastructure/
│   ├── Logging/                 # IModLogger.cs, FileLogger.cs
│   └── Validation/
├── Features/                    # one folder per feature, each self-contained
│   └── CareerSystem/            # a representative one
│       ├── Abilities/
│       ├── Domain/
│       ├── Hooks/               # Harmony patches live INSIDE the feature
│       ├── Models/              # GameModel overrides
│       ├── Mutations/
│       ├── Quests/
│       └── UI/
├── SceneScripts/
├── _Module/                     # shipped payload: ModuleData, Prefabs, GUI, SubModule.xml
├── IoC.cs                       # single-owner DI composition root
├── ManualPatchApplicator.cs
├── PatchCategoryApplier.cs      # guarded apply of one Harmony patch category
├── PatchCategoryIndex.cs        # category index built class by class (#653)
└── SubModule.cs                 # single-owner engine entry point

TAOM.Tests/
├── Adapters/
├── BehaviorTrees/
├── BehaviorTreeWrapper/
├── Core/
├── Features/                    # mirrors Main/Features
├── Infrastructure/
├── Migration/
└── SceneScripts/
```

---

## Related Guides

- [patterns.md](./patterns.md) - Thread-local and cache patterns
- [testing-guide.md](./testing-guide.md) - Shipped-data tests
- [code-quality.md](./code-quality.md) - Code quality
- [taleworlds-research-guide.md](./taleworlds-research-guide.md) - TaleWorlds investigation

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/agent-operating-manual.md](./agent-operating-manual.md)
- [docs/ai-includes/agent-teams.md](./agent-teams.md)
- [docs/ai-includes/code-quality.md](./code-quality.md)
- [docs/ai-includes/patterns.md](./patterns.md)
- [docs/ai-includes/testing-guide.md](./testing-guide.md)
- [docs/features/alignment-aware-execution.md](../features/alignment-aware-execution.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
