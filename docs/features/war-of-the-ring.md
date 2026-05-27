# War of the Ring System

## Overview

The War of the Ring system creates a scripted, phased escalation into total war between the Free Peoples and Dark Powers of Middle-earth. Once activated, hostile kingdoms are permanently locked in conflict — no peace treaties, no truces, no diplomacy.

## Why This Exists

Vanilla Bannerlord allows kingdoms to freely make peace, which breaks the LOTR narrative. Mordor shouldn't negotiate a truce with Gondor mid-campaign. The War of the Ring is an inevitable, permanent conflict that defines the Third Age.

Key design goals:
- **Phased escalation** — Isengard strikes first (lore-accurate), then the full war erupts
- **Configurable timing** — adjustable via MCM or JSON for testing and balance
- **Southern free-for-all** — Harad, Umbar, Khand, and Easterlings can still fight each other freely; they're vassals of convenience, not permanent allies
- **Peace blocking** — Once the full war starts, hostile kingdoms cannot make peace through any mechanism

## War Phases

| Phase | Name | Trigger | Effects |
|-------|------|---------|---------|
| 0 | **Peace** | Game start | Normal diplomacy. Alliances and hostility tiers from `diplomacy.json` apply. |
| 1 | **Isengard War** | Day 1 (configurable) | Isengard and Dunland declare war on Rohan. No peace blocking yet. |
| 2 | **Full War** | Day 1 (configurable) | All `Hostile`-tier kingdom pairs go to war. Peace is permanently blocked between hostile pairs. |

### Phase 1: The Isengard War

At the configured day (default: 1), the system declares war:
- **Isengard** → **Rohan** (vlandia)
- **Dunland** (empire) → **Rohan** (vlandia)

This mirrors the lore — Saruman's assault on Rohan is the opening salvo of the War of the Ring.

### Phase 2: The Full War

At the configured day (default: 1), the system:
1. Iterates all kingdom pairs from `diplomacy.json` that have `Hostile` tier
2. Declares war between any hostile pair not already at war
3. Activates permanent peace blocking

**What gets blocked:** Any pair with `Hostile` relationship tier cannot make peace. This includes Gondor-Mordor, Rohan-Isengard, Erebor-Gundabad, all Elven kingdoms vs all evil kingdoms, etc.

**What stays free:** Southern kingdoms (Harad, Umbar, Khand) have `Natural` or `Neutral` relationships with each other. They are NOT blocked from making peace with each other. Note: Rhun (Easterlings) is now a Dark Power with Permanent alliances to Mordor, Isengard, Gundabad, and Dol Guldur — it is fully hostile to all Free Peoples and cannot make peace with them.

## Three-Layer Peace Blocking

The system uses three independent layers to ensure peace cannot be made during the War of the Ring:

### Layer 1: TaomDiplomacyModel (GameModel)

Overrides `DefaultDiplomacyModel.IsAtConstantWar(IFaction, IFaction)`. When WotR is active and the pair is hostile, returns `true`. The vanilla `IsPeaceDecisionAllowedBetweenKingdoms` already checks this and blocks peace proposals in the UI with "These kingdoms can not declare peace at this time."

### Layer 2: TaomKingdomDecisionPermissionModel (GameModel)

Overrides `IsPeaceDecisionAllowedBetweenKingdoms`. When WotR blocks peace for the pair, returns `false` with the message: "The War of the Ring rages. There can be no peace."

### Layer 3: MakePeaceAction Harmony Patch

Prefix on `MakePeaceAction.ApplyInternal` (private static method with signature `void ApplyInternal(IFaction, IFaction, int, int, MakePeaceDetail)`). Safety net that catches any peace attempt that bypasses the GameModel checks (e.g., direct API calls, other mods). Returns `false` to skip execution.

### Verified Coverage (All Vanilla Peace Paths)

The three layers were verified against decompiled 1.3.x source to cover every vanilla peace-making path:

| Path | Blocked By |
|------|-----------|
| AI proposes kingdom peace decision | Layer 1 (AI checks `IsAtConstantWar` before proposing) |
| Player proposes peace in kingdom menu | Layer 2 (`IsPeaceDecisionAllowed` returns false) |
| AI peace offer to player (ruler accepts directly) | Layer 3 (Harmony prefix on `ApplyInternal`) |
| AI peace offer to player (vassal, goes to vote) | Layer 2 (`IsAllowed()` fails) |
| Clan joining kingdom triggers auto-peace | Layer 1 (checks `IsAtConstantWar` first) |
| Clan leaving kingdom triggers auto-peace | Layer 1 (checks `IsAtConstantWar` first) |
| Minor faction/clan AI peace | Layer 1 (checks `IsAtConstantWar`) |
| Cheat console `declare_peace` | Layer 1 (checks `IsAtConstantWarAgainstFaction`) |

All paths ultimately funnel through `MakePeaceAction.ApplyInternal`, which Layer 3 catches as an absolute safety net.

## Configuration

### JSON: `Main/_Module/ModuleData/diplomacy/war_of_the_ring.json`

```json
{
  "enabled": true,
  "phase1": {
    "triggerDay": 1,
    "wars": [
      { "attacker": "isengard", "defender": "vlandia" },
      { "attacker": "empire", "defender": "vlandia" }
    ]
  },
  "phase2": {
    "triggerDay": 1,
    "autoWarBetweenHostileTiers": true,
    "blockPeaceBetweenHostileTiers": true,
    "wars": []
  },
  "testMode": {
    "enabled": false,
    "phase1Day": 2,
    "phase2Day": 5
  }
}
```

| Field | Description |
|-------|-------------|
| `enabled` | Master switch for the entire WotR system |
| `phase1.triggerDay` | Days after campaign start to trigger Phase 1 |
| `phase1.wars` | Explicit war declarations for Phase 1 |
| `phase2.triggerDay` | Days after campaign start to trigger Phase 2 |
| `phase2.autoWarBetweenHostileTiers` | If true, automatically declares war between all `Hostile` pairs from `diplomacy.json` |
| `phase2.blockPeaceBetweenHostileTiers` | If true, blocks peace between `Hostile` pairs |
| `testMode.enabled` | Overrides trigger days with short values for testing |

### MCM Settings (In-Game)

The MCM options menu (via `Bannerlord.MCM.v5`) provides runtime overrides:

| Setting | Default | Description |
|---------|---------|-------------|
| Enable War of the Ring | true | Master toggle |
| Phase 1 Start Day | 30 | Overrides JSON phase1.triggerDay |
| Phase 2 Start Day | 45 | Overrides JSON phase2.triggerDay |
| Enable Test Mode | false | Uses 2/5 day delays for rapid testing |

**Precedence order** (highest to lowest):
1. MCM Test Mode enabled → uses JSON `testMode.phase1Day` / `testMode.phase2Day`
2. JSON `testMode.enabled = true` → uses JSON `testMode.phase1Day` / `testMode.phase2Day`
3. MCM Phase 1/2 day values (when MCM is available)
4. JSON `phase1.triggerDay` / `phase2.triggerDay` (fallback when MCM is unavailable)

For the master enable/disable toggle: MCM `WarOfTheRingEnabled` takes precedence over JSON `enabled` when MCM is available.

JSON provides the structural data (which kingdoms fight, war declarations). MCM provides timing and toggle overrides.

## Architecture

### Component Diagram

```
war_of_the_ring.json     TaomSettings (MCM)
         \                  /
    WarOfTheRingConfig   ITaomSettingsProvider
        Provider        (injected, testable)
              \          /
          WarOfTheRingService ←── IDiplomacyService (hostile tier lookup)
             /       |       \
            /        |        \
    TaomDiplomacy  TaomKingdom   MakePeace
    Model          DecisionModel  Patch
    (IsAtConstant  (IsPeaceAllow  (Harmony
     War)           ed)            prefix)
            \        |        /
             \       |       /
          WarOfTheRingBehavior
          (DailyTick — uses CampaignStartTime)
```

### Stateless Design

The WotR system stores **no save data**. Each campaign tick:
1. Calculates elapsed campaign days via `CampaignStartTime.ElapsedDaysUntilNow` (engine-provided, survives save/load)
2. Compares against phase thresholds (from MCM or JSON config)
3. Declares wars only for pairs not already at war (`AreAtWar` guard)
4. Peace blocking checks elapsed time in real-time

The behavior uses the engine's built-in `Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow` — the same pattern vanilla uses internally. This is computed from the persisted `MapTimeTracker` ticks and the deterministic campaign start constant. No custom `SyncData` serialization is needed, avoiding any save/load performance impact.

Benefits: MCM config changes take effect immediately on next tick. No save migration needed. Phase state is reconstructed from elapsed time on every tick, so save/load is seamless.

### MCM Integration via ITaomSettingsProvider

MCM settings are accessed through an injected `ITaomSettingsProvider` interface rather than direct static access to `TaomSettings.Instance`. This:
- Keeps the service testable (tests inject a mock with `IsAvailable = false`)
- Follows the explicit-dependency-via-constructor-injection principle
- Gracefully falls back to JSON config when MCM is unavailable

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Diplomacy/Models/WarPhase.cs` | Phase enum (Peace/IsengardWar/FullWar) |
| `Main/Features/Diplomacy/Models/WarOfTheRingConfig.cs` | Config data models |
| `Main/Features/Diplomacy/Models/TaomDiplomacyModel.cs` | GameModel — IsAtConstantWar override |
| `Main/Features/Diplomacy/IWarOfTheRingService.cs` | Service interface |
| `Main/Features/Diplomacy/WarOfTheRingService.cs` | Core logic — phase transitions, war declarations |
| `Main/Features/Diplomacy/IWarOfTheRingConfigProvider.cs` | Config loading interface |
| `Main/Features/Diplomacy/WarOfTheRingConfigProvider.cs` | JSON loader |
| `Main/Features/Diplomacy/ITaomSettingsProvider.cs` | MCM settings interface (testable) |
| `Main/Features/Diplomacy/TaomSettingsProvider.cs` | MCM settings implementation |
| `Main/Features/Diplomacy/WarOfTheRingBehavior.cs` | DailyTick timer (uses CampaignStartTime) |
| `Main/Features/Diplomacy/Hooks/IOnPeaceAction.cs` | Peace hook interface |
| `Main/Features/Diplomacy/Hooks/PeaceActionHook.cs` | Hook implementation |
| `Main/Features/Diplomacy/Hooks/MakePeaceAction_ApplyInternal_Patch.cs` | Harmony safety net |
| `Main/Features/TaomSettings.cs` | MCM settings class |
| `Main/_Module/ModuleData/diplomacy/war_of_the_ring.json` | Phase timing config |

## Dependencies

- `IDiplomacyService` — Relationship tier lookups (determines which pairs are "Hostile")
- `IAllianceAdapter` — War declaration and war-status checks
- `ITaomSettingsProvider` — MCM settings access (injected, testable)
- `IPathService` / `IModLogger` — Standard infrastructure

## Relationship to Diplomacy Feature

The WotR system **extends** the existing Diplomacy feature, not replaces it:
- It uses the same `diplomacy.json` relationship tiers (Hostile pairs become war targets)
- It reuses `IAllianceAdapter` for war declarations
- `TaomKingdomDecisionPermissionModel` now serves both alliance blocking AND peace blocking
- `DiplomacyIoC` registers all WotR services alongside diplomacy services

## Tests

- `TAOM.Tests/Features/Diplomacy/WarOfTheRingServiceTests.cs` — 15 tests covering phase transitions, war declarations, peace blocking, test mode, idempotent re-checks
- `TAOM.Tests/Features/Diplomacy/PeaceActionHookTests.cs` — 3 tests for hook behavior

## How to Test In-Game

1. Set `testMode.enabled = true` in `war_of_the_ring.json` (or enable Test Mode in MCM)
2. Start a new campaign
3. Phase 1 triggers on Day 2 — check Rohan is at war with Isengard and Dunland
4. Phase 2 triggers on Day 5 — check all hostile pairs are at war, peace proposals blocked
5. Verify Harad/Umbar/Khand can still make peace with each other (Rhun cannot — it's a Dark Power)

## How to Add New War Phases

1. Add a new `PhaseConfig` to `WarOfTheRingConfig`
2. Add a new `WarPhase` enum value
3. Add transition logic in `WarOfTheRingService.CheckPhaseTransition`
4. Update tests

## Kingdom ID Reference

| Kingdom ID | LOTR Name | Side |
|------------|-----------|------|
| empire_w | Gondor | Free |
| vlandia | Rohan | Free |
| erebor | Erebor | Free |
| sturgia | Dale | Free |
| rivendell | Rivendell | Free |
| lothlorien | Lothlorien | Free |
| mirkwood | Mirkwood | Free |
| empire_s | Mordor | Dark Power |
| isengard | Isengard | Dark Power |
| gundabad | Gundabad | Dark Power |
| dolguldur | Dol Guldur | Dark Power |
| khuzait | Easterlings (Rhun) | Dark Power |
| empire | Dunland | Evil (independent) |
| aserai | Harad | Southern |
| umbar | Umbar | Southern |
| battania | Khand | Neutral |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/alignment-aware-execution.md](./alignment-aware-execution.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
