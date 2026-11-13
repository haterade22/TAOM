# Bannerlord campaign→mission bridge — MapEvent / PlayerEncounter / MissionState.OpenNew (Phase 17)

> **One process, traced from the decompile** (v1.4.5): the seam that turns a campaign-map encounter into a real-time
> battle — how two `MobileParty`s meeting (Phase 16) become a `MapEvent`, then a `Mission` full of `Agent`s
> (Phases 1-15). This is **the link between the two halves of the engine study**: managed campaign sim → native
> mission. TAOM's battle features (Siege, the spider/elephant in combat, the BattleLoadDiagnostics phase log) all ride it.
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
  (Phase 16) → this chain → `Mission.SpawnAgent` spawns it (Phase 1). The
  **`BattleLoadDiagnostics`** (TAOM) instruments exactly this chain — the spider crash evidence (the last phase line, `phase=MissionInitialize scene='battle_terrain_020'`) is *step 6* (scene load / agent build) stalling.
- **TAOM mission behaviors are added from `SubModule.OnMissionBehaviorInitialize`** (Phase 4; `AddTaomMissionBehaviors`, which calls
  `AddTaomBehavior(new BehaviorTreeMissionLogic())` and `AddTaomBehavior(new SpiderMissionBehavior())`, SubModule.cs:2183 and :2186), not from step 5's handler. SmartCavalry adds `SmartCavalryAIMissionBehavior` the same way (:2197), and its Formation patches use `Patch_MissionTime_SmartCavalryAI`. CompanionTactics adds no behavior in this method and applies `Patch35_CompanionTactics` (:1841). The
  **deferred-patch categories** (`Patch_MissionTime_SmartCavalryAI`, `Patch_MissionTime_CompanionTactics` and `Patch_MissionTime_MarshalsWord`, Phase 13) apply during
  `OnMissionBehaviorInitialize` *because* `MovementOrder.cctor` reads `Mission.Current.CurrentTime`, which is only valid
  once this chain has created the mission ([harmony-il lessons](../../reviews/lessons/harmony-il.md), "Defer patches whose parameter types have Mission/Campaign-dependent cctors").
- **`CreateState`+`PushState` is mandatory** (Phase 11; [gamemodels-services lessons](../../reviews/lessons/gamemodels-services.md), "Push custom GameStates via `GameStateManager.CreateState<T>()`, never `new`"): vanilla opens the
  mission via `CreateState<MissionState>()` + `PushState`, never `new MissionState()`. TAOM custom screens must follow
  the same pattern.
- **Casualty/XP attribution** flows through the `CasualtyHandler` (step 5) keyed off each agent's `Origin` (Phase 1) —
  why the spider spawn sets `agent.Origin = source.AgentOrigin` (a FromHorseObj agent still needs a valid `Origin` for
  the casualty handler to credit kills/deaths to the campaign roster).
- **AI battles skip the mission** — `MapEvent` auto-resolves via `StrengthOfSide` when no player is present; a TAOM
  feature that must affect *every* battle (not just player-watched ones) belongs in a GameModel/campaign hook, not a
  `MissionBehavior` (which only runs for missions the player actually loads).

## Custom Battle driven without a player [yotthani, spike on v1.5.6]

Custom Battle is the one mission entry that needs no campaign. yotthani's night spike (29.10.2026, Bannerlord v1.5.6, TAOM 2.0.35; record
`docs/balance-sim/app-0.3-spike.md@c45d8374`, "Calls that work on 1.5.6", "Hit data (S5, C12)" and "C11 finding"; code `HoN/BattlePresets/**`
and `HoN/CampaignProbe/Integration/Battles/BattleDriver.cs@c45d8374:306-307, 321-324, 395-398`) ran 50 against 50 battles unattended, about 110 s of wall time each. It is code that ran
on one PC; none of it is re-run on v1.5.7.
- **Start:** from the main menu, `MBGameManager.StartNewGame(new CustomGameManager())` at `InitialState`, wait for `CustomBattleState` plus 3 s, then
  `BattleStarter.TryStart`. **Deployment:** after 600 mission ticks a pending action on the next `OnApplicationTick` calls
  `DeploymentMissionController.FinishDeployment()`. **AI handover:** `Mission.PlayerTeam.DelegateCommandToAI()`, every formation
  `SetControlledByAI(true)`, `Agent.Main.Controller = AgentControllerType.AI`; watch mode sets `Mission.MainAgent = null` afterwards (S7 partial, no human check).
- **End:** count `IsHuman && IsActive() && Team != null` agents per side every 30 ticks. `Mission.EndMission()` returns to `CustomBattleState` in 0.6 s
  with no results screen, and `Utilities.QuitGame()` after a done marker exits the game.
- **Traps in the hit data** (yotthani's findings; not re-checked by TAOM): (a) if the player character has the same troop id as the player side's rows,
  only one agent spawns on that side, all rows of that id vanish (51 against 50 works with a different troop id; the record says the cause is not
  located in the engine source); (b) friendly hits carry `int.MinValue` in `InflictedDamage` and `AbsorbedByArmor` (62 rows in one battle);
  (c) shield-blocked hits (`AttackBlockedWithShield`) have `through` and `absorbed` filled but `blow.InflictedDamage = 0`
  (106 of 398 cross hits in one battle; on v1.5.7 both values hold the shield damage, `MissionCombatMechanicsHelper.cs:214-218`).
  yotthani lists `MissileBlockedWithWeapon` with them; that flag zeroes both values and its managed path registers no blow, see
  [combat-mechanics.md](../../features/combat-mechanics.md#missile-blocked-with-a-weapon) [TAOM-verified, v1.5.7]; (d) hits on a riderless horse have no troop, and hits without a weapon item (charge, kick, fall) were
  73 to 175 of about 500 in a cavalry battle; (e) `Hero.MainHero` reads `Game.Current.PlayerTroop`, which is null in a custom battle
  (`Hero.cs:958`, `CharacterObject.cs:256`), so it throws a NullReferenceException; (f) `CustomBattleAgentStatCalculateModel` returns 1 for the
  difficulty factors (commit `a21cbea0`). TAOM's [blow-diagnostics.md](../../features/blow-diagnostics.md) explains `int.MinValue` as a NaN damage
  multiplier cast to `int` on net472; whether yotthani's friendly-fire value is the same cast is not checked.
- **The sergeant's commander is found by display name.** `GeneralsAndCaptainsAssignmentLogic.SetGeneralAgentOfTeam` (`:180-205`): unless the player
  is the team's general, it takes the one agent whose `Character.GetName()` equals the passed name (`TextObject.Equals` compares value and
  attributes, `TextObject.cs:269-281`); with no name, or not exactly one match, it takes the strongest non-main hero (`MaxBy(CharacterPowerCached)`),
  or none [TAOM-verified, v1.5.7 cache]. Vanilla passes a name only for the player's side as sergeant (`BannerlordMissions.cs:144`); the enemy side
  gets null. In Custom Battle the player's side holds the player (excluded as the main agent) and one general, so a name clash falls back to the
  strongest other hero, which is that general if he is a hero. yotthani's note "the engine may not find him"
  (`HoN/BattlePresets/Core/PresetCheck.cs@c45d8374:225-229`, read from code, no version stated) is too strong for this flow. The same preset
  code says `MaxBattleSize` is a hard per-side cap that counts leaders (`ICatalog.cs@c45d8374:35-36`), and that when both sides together
  exceed `BannerlordConfig.GetRealBattleSize()` the rest arrive as reinforcement waves, so a player who is not a hero may spawn in a
  later wave (`PresetCheck.cs@c45d8374:234-247`; not re-checked).

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
- Linked: campaign-object-graph.md (Phase 16, the parties), mission-and-missionbehavior-lifecycle.md (Phase 4), agent-spawn-and-render-pipeline.md (Phase 1), gauntletui-viewmodel-screen.md (Phase 11, CreateState/PushState), formations-and-team-ai.md (Phase 13, deferred patch). Gotchas: `docs/reviews/lessons/gamemodels-services.md` (GameState creation), `docs/reviews/lessons/harmony-il.md` (deferred patch).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../../INDEX.md)
- [docs/reference/doc-lookup.md](../doc-lookup.md)

<!-- backlinks-end -->
