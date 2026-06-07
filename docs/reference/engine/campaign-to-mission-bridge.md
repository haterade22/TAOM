# Bannerlord campaign→mission bridge — MapEvent / PlayerEncounter / MissionState.OpenNew (Phase 17)

> **One process, traced from the decompile** (v1.4.5): the seam that turns a campaign-map encounter into a real-time
> battle — how two `MobileParty`s meeting (Phase 16) become a `MapEvent`, then a `Mission` full of `Agent`s
> (Phases 1-15). This is **the link between the two halves of the engine study**: managed campaign sim → native
> mission. TAOM's battle features (Siege, the spider/elephant in combat, the BattleLoadStallWatchdog) all ride it.
> Part of the phased engine study.

## WHAT it is

When parties meet on the map, the campaign creates a **`MapEvent`** (the abstract "a battle is happening here", with
an attacker + defender side). For the player it's wrapped in a **`PlayerEncounter`** (the encounter menu state). When
the battle actually starts, **`CampaignMission.OpenBattleMission`** → **`MissionState.OpenNew`** builds a `Mission`
(Phase 4) via the **Phase-11 `CreateState`+`PushState`** pattern — and from there the mission stack (Phases 1-15) takes
over. Outcomes (casualties) flow back to the campaign.

## HOW it works — the chain

### Campaign side (managed — Phase 16 objects)
1. **`EncounterManager.StartPartyEncounter(PartyBase attacker, PartyBase defender)`** (EncounterManager.cs:56) — fired
   when parties engage. It calls **`StartBattleAction.Apply(attacker, defender)`** (:87), which creates/joins a
   **`MapEvent`**. (`StartSettlementEncounter` :100 is the siege/raid variant.)
2. **`MapEvent` (sealed : MBObjectBase — MapEvent.cs:22)** holds `_sides[2]` (`MapEventSide` attacker/defender, :61),
   `StrengthOfSide[2]` (:87), `_state` (`MapEventState`, :58), `_battleState` (`BattleState`, :101).
   `MapEvent.PlayerMapEvent => MobileParty.MainParty?.MapEvent` (:107); `PlayerSide` (:109).
3. **`PlayerEncounter` (PlayerEncounter.cs:22)** — the player-facing encounter state. `PlayerEncounter.Current =>
   Campaign.Current.PlayerEncounter` (:124); `PlayerSide`/`OpponentSide` (:247/:250); `BattleState`/`WinningSide`
   (:180/:182). The encounter game-menu ("Attack"/"Leave") drives it; choosing to fight opens the mission.

### The seam → mission side (native — Phases 1-15)
4. **`CampaignMission.OpenBattleMission(...)`** (CampaignMission.cs:69) → `Campaign.Current.CampaignMissionManager
   .OpenBattleMission(rec)` (the SandBox `ICampaignMission` impl, :22). It builds a **`MissionInitializerRecord rec`**
   (scene name, `SceneLevels`, decal atlas — the managed→native handoff naming the scene) + an
   **`InitializeMissionBehaviorsDelegate handler`** (which `MissionBehavior`s to add — Phase 4).
5. **`MissionState.OpenNew(missionName, rec, handler, addDefaultMissionBehaviors)`** (MissionState.cs:312):
   ```
   Game.Current.OnMissionIsStarting(missionName, rec);
   MissionState missionState = Game.Current.GameStateManager.CreateState<MissionState>();   // ← Phase 11 CreateState
   Mission result = missionState.HandleOpenNew(missionName, rec, handler, addDefaultMissionBehaviors, …);  // builds Mission + adds behaviors
   Game.Current.GameStateManager.PushState(missionState);                                   // ← Phase 11 PushState
   ```
   `addDefaultMissionBehaviors` always adds **`BasicMissionHandler`, `CasualtyHandler`, `AgentCommonAILogic`** (+
   `MissionNetworkComponent`/`RecordMissionLogic` in MP/replay), then the `handler`'s behaviors (MissionState.cs:316-330).
6. **`MissionState : GameState` (MissionState.cs:11)**, `MissionState.Current` (:31), `CurrentMission` (:33). Activating
   the pushed state → `CurrentMission.OnMissionStateActivate` (:60) → the **mission lifecycle (Phase 4)**:
   `OnBehaviorInitialize` → `OnMissionBehaviorInitialize` (where the deferred `Formation.SetMovementOrder` patches apply
   — Phase 13) → scene load → **`Mission.SpawnAgent` per troop (Phase 1)** → tick/render (Phases 1-15).

### Outcome flows back
The `CasualtyHandler` (added in step 5) attributes kills during the mission; on mission end the result updates the
`MapEvent`, and `StartBattleAction`/the campaign applies roster losses, XP, and morale back onto the Phase-16 objects.

```
MobileParty meets MobileParty (Phase 16)
  → EncounterManager.StartPartyEncounter → StartBattleAction.Apply → MapEvent (attacker/defender MapEventSides)
  → (player) PlayerEncounter menu → choose Attack
  → CampaignMission.OpenBattleMission → MissionInitializerRecord + behavior handler
  → MissionState.OpenNew → CreateState<MissionState> + PushState (Phase 11)
  → Mission (Phase 4) → SpawnAgent per troop (Phase 1) → agents/formations/stats (Phases 3,13,15) → native render
  → CasualtyHandler → MapEvent outcome → campaign roster/XP/morale back to Phase-16 objects
```

## WHY it's shaped this way

`MapEvent` decouples "a battle exists in the simulation" (so AI parties resolve battles **without a mission** — the
strength numbers auto-resolve) from "the player is watching it" (`PlayerEncounter` + an actual `Mission`). Routing the
mission through `MissionState : GameState` (the Phase-11 state stack) means a battle is just another pushed game state —
the same machinery as menus/screens — so it suspends the campaign map cleanly and restores it on finish. The
`MissionInitializerRecord` (scene name only) is the thin managed→native handoff: managed decides *which* battle terrain,
native loads + simulates it.

## TAOM relevance + gotchas
- **Every TAOM battle feature rides this bridge.** A spider/elephant troop sits in a `MobileParty`'s `TroopRoster`
  (Phase 16) → this chain → `Mission.SpawnAgent` spawns it (Phase 1 + the spider spawn patch). The
  **`BattleLoadStallWatchdog`** (TAOM) instruments exactly this chain — the spider crash evidence "last
  phase=MissionInitialize seq=4 scene='battle_terrain_020'" is *step 6* (scene load / agent build) stalling.
- **TAOM mission behaviors are added in step 5's handler / `OnMissionBehaviorInitialize`** (Phase 4):
  `BehaviorTreeMissionLogic`, `SpiderMissionBehavior`, SmartCavalry/CompanionTactics logic. The
  **deferred-patch category** (`Patch_MissionTime_SetMovementOrder`, Phase 13) applies during
  `OnMissionBehaviorInitialize` *because* `MovementOrder.cctor` reads `Mission.Current.CurrentTime`, which is only valid
  once this chain has created the mission (`feedback_movementorder_cctor_mission_current`).
- **`CreateState`+`PushState` is mandatory** (Phase 11 / `feedback_gamestate_creation_pattern`): vanilla opens the
  mission via `CreateState<MissionState>()` + `PushState`, never `new MissionState()`. TAOM custom screens must follow
  the same pattern.
- **Casualty/XP attribution** flows through the `CasualtyHandler` (step 5) keyed off each agent's `Origin` (Phase 1) —
  why the spider spawn sets `agent.Origin = source.AgentOrigin` (a FromHorseObj agent still needs a valid `Origin` for
  the casualty handler to credit kills/deaths to the campaign roster).
- **AI battles skip the mission** — `MapEvent` auto-resolves via `StrengthOfSide` when no player is present; a TAOM
  feature that must affect *every* battle (not just player-watched ones) belongs in a GameModel/campaign hook, not a
  `MissionBehavior` (which only runs for missions the player actually loads).

## The native boundary
**Managed:** the entire campaign side (`MapEvent`, `PlayerEncounter`, `EncounterManager`, `StartBattleAction`,
`CampaignMissionManager`) and the `MissionState`/`GameState` orchestration. **Native:** the scene load + the mission
runtime (Phases 1-15 — agents, render, physics). **`MissionState.OpenNew` is the seam**: managed code names a scene
(`MissionInitializerRecord`) and lists behaviors, then the engine loads the native scene and runs the mission. This is
the single point where the all-managed campaign layer (Phase 16) hands off to the native mission layer.

## Evidence (file:line, v1.4.5)
- `EncounterManager.cs`:56 (`StartPartyEncounter`→`StartBattleAction.Apply` :87), :100 (`StartSettlementEncounter`).
- `MapEvent.cs`:22 (`sealed : MBObjectBase`), :61 (`_sides[2]` `MapEventSide`), :87 (`StrengthOfSide`), :58/:101 (`MapEventState`/`BattleState`), :107 (`PlayerMapEvent`), :109 (`PlayerSide`).
- `PlayerEncounter.cs`:22, :124 (`Current`), :180/:182 (`BattleState`/`WinningSide`), :247/:250 (`OpponentSide`/`PlayerSide`).
- `CampaignMission.cs`:12 (`static class`), :22 (`OpenBattleMission(rec)`), :69 (`OpenBattleMission`→`CampaignMissionManager`).
- `MissionState.cs`:11 (`: GameState`), :31 (`Current`), :33 (`CurrentMission`), :60 (`OnMissionStateActivate`), :312 (`OpenNew`→`CreateState<MissionState>`+`HandleOpenNew`+`PushState`), :316-330 (default behaviors: `BasicMissionHandler`/`CasualtyHandler`/`AgentCommonAILogic`).
- Linked: campaign-object-graph.md (Phase 16, the parties), mission-and-missionbehavior-lifecycle.md (Phase 4), agent-spawn-and-render-pipeline.md (Phase 1), gauntletui-viewmodel-screen.md (Phase 11, CreateState/PushState), formations-and-team-ai.md (Phase 13, deferred patch). Gotchas: `feedback_gamestate_creation_pattern`, `feedback_movementorder_cctor_mission_current`.
