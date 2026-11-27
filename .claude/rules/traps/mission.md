---
paths:
  - "Main/**/*Mission*.cs"
  - "Client/**/*Mission*.cs"
  - "Main/**/*Agent*.cs"
  - "Main/**/*Formation*.cs"
  - "Main/**/BehaviorTreeElements/**"
  - "Main/Features/Mumakil/**"
  - "Main/Features/SmartCavalryAI/**"
  - "Main/Features/CreatureSiegeRole/**"
  - "Main/Features/Siege/**"
  - "Main/Features/StagedSiege/**"
  - "Main/Features/CombatMechanics/**"
  - "Main/Features/AdvancedCombat/**"
  - "Client/Features/LongDefeat/**"
  - "Client/Features/FordsOfWar/**"
---

# Trap sheet: missions

Mission logic, agents, formations, siege ladders, moving platforms and native calls.

| Trap | Rule | Doc |
|---|---|---|
| Moving platforms | Hold crew with `SetForceAttachedEntity` plus `SetTargetPosition` each tick; never a teleport, never `SetTargetZ` (NoPhysics mode: 0 shots measured); clear both on release. Navmesh and physics ride the entity. Crew stand inside the deck | [mumakil](../../../docs/features/mumakil.md) |
| Mission logic | Logic `BehaviorType` needs `: MissionLogic`; no throw and no spawn in `AfterStart` | [lifecycle](../../../docs/reference/engine/mission-and-missionbehavior-lifecycle.md) |
| Stop order | `StandGround` never forms a line; shape a formation with a Move | [cavalry](../../../docs/features/smart-cavalry-ai.md) |
| Ladder queue | No climb gate: exclude faces per agent, at most 6 ids per scene (bits 0, 7 reserved), fixed order, never removed | [siege](../../../docs/features/creature-siege-role.md) |
| Agent slots, threads | Indices recycle and callbacks run off-thread: never key on `Agent.Index`, write via `RunOrDefer` | [rule](../csharp-architecture.md) |
| Battle overlay layer | A `GauntletLayer` with widgets: `SetInputRestrictions(false, InputUsageMask.MouseButtons)`, reset before `RemoveLayer`, no `IsFocusLayer` | [rule](../gui-ui.md) |
| Native branches | ILSpy hides them (`GetNearbyAgentsAux` past 15 m scans every agent): `native_decompile.py` first | [lesson](../../../docs/reviews/lessons/adapters-taleworlds-api.md) |
