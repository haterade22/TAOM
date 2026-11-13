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
---

# Trap sheet: missions

Mission logic, agents, formations, siege ladders, moving platforms and native calls.

One line per trap: its trigger and what never to do. The linked doc holds the mechanism, the
history and the gate. A new trap for this area is a new row here, never a row in the always-loaded
[trap index](../../../docs/ai-includes/orientation.md)
([ADR-011](../../../docs/adrs/011-knowledge-delivery-tiers.md)).

| Trap | Rule | Doc |
|---|---|---|
| Moving platforms | Agents need a navmesh riding the entity, plus physics; teleporting fails. Crew stand inside the deck | [mumakil](../../../docs/features/mumakil.md) |
| Mission logic | Logic `BehaviorType` needs `: MissionLogic`; no throw and no spawn in `AfterStart` | [lifecycle](../../../docs/reference/engine/mission-and-missionbehavior-lifecycle.md) |
| Stop order | `StandGround` never forms a line; shape a formation with a Move | [cavalry](../../../docs/features/smart-cavalry-ai.md) |
| Ladder queue | No climb gate: exclude faces per agent, at most 6 ids per scene (bits 0, 7 reserved), fixed order, never removed | [siege](../../../docs/features/creature-siege-role.md) |
| Agent slots, threads | Indices recycle and callbacks run off-thread: never key on `Agent.Index`, write via `RunOrDefer` | [rule](../csharp-architecture.md) |
| Native branches | ILSpy hides them (`GetNearbyAgentsAux` past 15 m scans every agent): `native_decompile.py` first | [lesson](../../../docs/reviews/lessons/adapters-taleworlds-api.md) |
