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
| 0 | **Peace** | Game start | Normal diplomacy. Alliances and hostility tiers from `diplomacy.json` apply. No AI kingdom may declare war. The player's own hostile acts still start one (see "War Rule by Phase"). |
| 1 | **Isengard War** | Day 30 (configurable) | Isengard and Dunland declare war on Rohan. No peace blocking yet. Only these two wars may exist until Phase 2. |
| 2 | **Full War** | Day 44 (configurable) | All `Hostile`-tier kingdom pairs go to war. Peace is permanently blocked between hostile pairs. |

### Phase 1: The Isengard War

At the configured day (default: 30), the system declares war:
- **Isengard** → **Rohan** (vlandia)
- **Dunland** (empire) → **Rohan** (vlandia)

This mirrors the lore: Saruman's assault on Rohan is the opening salvo of the War of the Ring. Gundabad and Arthedain are not scripted here: they go to war on day 44 through their `Hostile` row, like every other Hostile pair. Ered Luin is not scripted either; its 9 Hostile rows (the realms Arthedain is Hostile to, Blue Craig among them) bring it in on day 44.

### Phase 2: The Full War

At the configured day (default: 44), the system:
1. Iterates all kingdom pairs from `diplomacy.json` that have `Hostile` tier
2. Declares war between any hostile pair not already at war
3. Activates permanent peace blocking

The phase turns to Full War first, so that vanilla's `OnWarDeclared` listeners already treat every Hostile pair as constantly at war: the ally filter in `AllianceCampaignBehavior.OnWarDeclared` then calls no ally into a war the loop is about to declare, and TAOM's peace gates stay live while `KingdomDecisionProposalBehavior` re-checks pending decisions. The declarations guard on the stored stance (`IAllianceAdapter.HasDeclaredWar`) rather than `AreAtWar`, which goes through the model and would report every Hostile pair as already at war, so each pair still gets its `DeclareWarAction`, its `OnWarDeclared` event and a rebuilt at-war list. On the host, every session launch runs `ReconcileDeclaredWars` before the phase check (`WarOfTheRingBehavior.OnSessionLaunched`). It returns at once unless the phase is Full War and the War of the Ring is enabled. In Full War it walks the Phase 2 wars again (the `phase2.wars` list, plus every Hostile pair when `autoWarBetweenHostileTiers` is on) and declares only a pair whose stored stance is neutral and that the model calls at war (`ShouldBlockPeace`), so a legitimate peace, or a run with `blockPeaceBetweenHostileTiers` off, is never undone. A save that reached Full War before #772 gets its undeclared wars at its first launch, and later launches find nothing to declare (#772). A scripted war in `phase1.wars` or `phase2.wars` that names an unknown or eliminated kingdom is skipped, with one warning per process naming the pair; the other entries still declare, and the reconcile neither declares nor counts it.

**Limits of the repair.** It cannot declare a Hostile pair that had no stored stance when Full War began: the engine creates that link as War from `IsAtConstantWar`, either on the reconcile's own read or earlier during vanilla's daily tribute pass, so the pair stays at war with no event and no war start date. In practice this means a campaign started on the stale Full War phase of #764. While War of the Ring is disabled the repair waits until the first session launch after it is re-enabled; meanwhile the model, the peace blocks and momentum enrollment keep treating the save as Full War, and only the stance link and the war-declared notices are missing. With `blockPeaceBetweenHostileTiers` on and `autoWarBetweenHostileTiers` off (not the shipped setting), Hostile pairs are at constant war in the model with no declared stance from the Phase 2 transition onward; that predates #772 and the reconcile does not repair it.

When Phase 2 declares its wars, vanilla's alliance logic asks each Permanent ally that is not itself Hostile with the enemy to join (for example Harad into Mordor's wars). A repaired Full War save shows the encyclopedia history entries and map notices of those wars, dated at the load day, but no chat line for them: the repair runs in `OnSessionLaunched`, and vanilla turns chat notices on, and registers `KingdomManager`'s war listener (a kingdom aggressiveness increase that nothing in v1.5.4 reads), only after the session-launch listeners have run (`Campaign.OnSessionStart`). Both were decided by the maintainer on 2026-10-14: keep.

**What gets blocked:** Any pair with `Hostile` relationship tier cannot make peace. This includes Gondor-Mordor, Rohan-Isengard, Erebor-Gundabad, all Elven kingdoms vs all evil kingdoms, etc.

**What stays free:** Southern kingdoms (Harad, Umbar, Khand) have `Natural` or `Neutral` relationships with each other. They are NOT blocked from making peace with each other. Note: Rhun (Easterlings) is now a Dark Power with Permanent alliances to Mordor, Isengard, Gundabad, and Dol Guldur — it is fully hostile to all Free Peoples and cannot make peace with them.

## War Rule by Phase

Which wars may be declared depends on the phase. `WarOfTheRingService.GetWarVeto(a, b)` returns the reason a war is refused, or `None`. The checks run in this order, and the first that applies decides:

1. A `Permanent` pair is refused (`Permanent`, text `taom_war_blocked`), in every phase.
2. With the War of the Ring on and the phase `WarEnded`, nothing more is refused.
3. A same-alignment pair is refused (`SameAlignment`, text `taom_war_blocked_alignment`), in every phase before `WarEnded`.
4. With the War of the Ring off, or the war player-caused, nothing more is refused.
5. In Peace the war is refused (`PhasePeace`); in Isengard War a pair outside `phase1.wars` is refused (`PhaseNotScripted`).

The side is checked before the phase, so the phase label reaches only opposed or neutral pairs. A same-side pair reads the alignment text in Peace and Isengard War too, not the phase text.

| Phase | War allowed between an opposed or neutral pair |
|---|---|
| Peace (day 0 to the Phase 1 day) | None for the AI. The player's own hostile acts start a war anyway (see below) |
| Isengard War (to the Phase 2 day) | Only the wars listed in `phase1.wars` of `war_of_the_ring.json` |
| Full War | Any: the old `IsWarAllowed` rule applies (no `Permanent` pair, no same-alignment pair) |
| War Ended | Same-alignment wars are allowed too; `Permanent` pairs never |

With the War of the Ring disabled the service falls back to `IsWarAllowed` in every phase.

**Where it applies.** The `DeclareWarAction.ApplyInternal` prefix (through `AllianceActionHook`), the kingdom decision permission model (`TaomKingdomDecisionPermissionModel` asks `AllianceActionHook.GetWarRefusalText`, which logs nothing; a refused declaration shows `taom_war_blocked_alignment`, `taom_war_blocked_peace` or `taom_war_blocked_unscripted`; `taom_war_blocked` stays the Permanent text), and the call-to-war paths. `ProposeCallToWarAgreementDecision_IsAllowed_Patch` and `AcceptCallToWarAgreementDecision_IsAllowed_Patch` make those decisions not allowed when the war would be vetoed, so vanilla cancels them before the player sees the offer and declining an impossible offer costs no -50 relation. A one-clan player kingdom never reaches those ballots: vanilla posts a map notice from `AllianceCampaignBehavior.OnCallToWarAgreementProposedByPlayerKingdom(Kingdom proposedKingdom, Kingdom kingdomToCallToWarAgainst)` without reading `IsAllowed`, so `AllianceCampaignBehavior_OnCallToWarAgreementProposedByPlayerKingdom_Patch` skips that method for a vetoed pair and no notice appears. The `AllianceCampaignBehavior.StartCallToWarAgreement` prefix stays as the backstop: a war that cannot start costs no gold and makes no agreement. That makes seven prefix sites behind the co-op veto gate (`CoopVetoClassificationTests.GatedVetoes_AreExactlyThePinnedSet`).

**Player-caused wars.** The `DeclareWarAction.ApplyInternal` prefix reads `declareWarDetail`. `CausedByPlayerHostility`, `CausedByCrimeRatingChange` and `CausedByKingdomCreation` count as player-caused and lift only the two phase vetoes (`PhasePeace`, `PhaseNotScripted`); `Permanent` and same-alignment pairs still block. So a player whose kingdom attacks another kingdom's party, caravan or settlement before day 44 starts a real war, and the crime-rating war latches once instead of re-applying a relation penalty on every tick. AI wars stay blocked by the phase.

The callers behind the classification:

| Detail | Caller |
|---|---|
| `CausedByPlayerHostility` | `BeHostileAction` (the attacker is the main party); the lord-conversation threaten option; `LordWantsRivalCapturedIssueBehavior`; TAOM's `ServiceDiplomacyAdapter` (the enlistment war mirror) |
| `CausedByCrimeRatingChange` | `ChangeCrimeRatingAction`, only when the player rules the kingdom |
| `CausedByKingdomCreation` | `KingdomManager.CreateKingdom`: when the player founds a kingdom before day 44, the clan's wars carry over to it. In v1.5.5 only the player creates kingdoms (the governor menu and the cheat); United Empire, the third caller, is removed by TAOM |

**Enlistment edge (maintainer decision: allowed).** A vassal who enlists under a lord of another kingdom during days 30 to 44 makes the enlistment war mirror declare that lord's wars for the player's liege kingdom, as a player-caused war. That can pull the liege into the Rohan war early. The daily sweep keeps it, because it is the player's kingdom's war. A clan-identity enlistment mirror (the player's own clan, no kingdom) is never vetoed, because the war and peace prefixes act between two kingdoms only, and a scripted war during service is followed by the hourly sync (not an event listener) without a double declaration. See [enlistment.md](enlistment.md).

**AI war proposals.** `TaomDiplomacyModel.GetScoreOfDeclaringWar` subtracts `WarOfTheRingService.PhaseVetoScorePenalty` (10,000,000, vanilla's own "never declare" magnitude) for a phase veto and `LoreVetoScorePenalty` (100,000,000) for `Permanent` or same-alignment, so the AI no longer proposes, and pays 200 influence for, a war the rule would cancel. The Mercenary start uses the same score to pick its enemy, so it picks an opposed neighbour, whose veto is only the phase, over a same-side one, which carries the lore penalty in every phase; that war is kept because it is the player's kingdom's.

**Existing wars.** `EndWarsForbiddenByPhase` makes peace for any war that exists in Peace or Isengard War but is vetoed. It runs only on the daily tick, after `CheckPhaseTransition`, on the authority only and never when deferring to a co-op host. It is not called from `OnSessionLaunched`: on a load that event runs before the map event and siege managers are restored, and a peace made then can disturb live sieges and battles. It skips a war of the player's kingdom (`Clan.PlayerClan.Kingdom`, so vassal and mercenary service too) when the only veto is a phase veto; a player-kingdom war vetoed as `Permanent` or same-alignment is still ended. It counts a war as ended only when it is really over. A Peace-phase save loses its running AI wars; an Isengard War save loses every AI war except the phase 1 ones. The advanced-start world scenarios that declare wars are hidden while the War of the Ring is on ([advanced-start-options.md](advanced-start-options.md)), so this sweep mainly catches old saves and wars that engine paths declare directly.

**No start wars in data.** Every `<relationship>` row in `spkingdoms.xslt` and `taom_spkingdoms.xml` is `value >= 0` and not `isAtWar`, because `Kingdom.Deserialize` declares war for any negative value. `ShippedKingdomDataTests` pins it.

**Known gap.** A player hostile action against a `Permanent` or same-alignment kingdom still starts the fight, but the war declaration is blocked, so the two kingdoms fight while at peace. The phase vetoes no longer cause this. A second gap: the lord-dialogue "sponsor a call to war" option is not silent. Vanilla's call-to-war cost (`DefaultAllianceModel.GetCallToWarCostForCalledKingdom`) subtracts `GetScoreOfDeclaringWar`, so for a vetoed pair the option shows a cost in the millions and is greyed out ("You don't have enough money"); only an ally with no fiefs or a player holding millions reaches the `StartCallToWarAgreement` backstop, which then does nothing.

**Open follow-ups.** #818 is this work. #819: switching the War of the Ring off during Full War does not unlock peace. #820: the phase transitions are gated on `IsAuthority` alone, so they run on every BannerlordTogether peer.

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
    "triggerDay": 30,
    "wars": [
      { "attacker": "isengard", "defender": "vlandia" },
      { "attacker": "empire", "defender": "vlandia" }
    ]
  },
  "phase2": {
    "triggerDay": 44,
    "autoWarBetweenHostileTiers": true,
    "blockPeaceBetweenHostileTiers": true,
    "wars": []
  },
  "testMode": {
    "enabled": false,
    "phase1Day": 1,
    "phase2Day": 3
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
| Phase 2 Start Day | 44 | Overrides JSON phase2.triggerDay |
| Enable Test Mode | false | Uses the JSON `testMode` delays (1/3) for rapid testing |

**Precedence order** (highest to lowest):
1. MCM Test Mode enabled → uses JSON `testMode.phase1Day` / `testMode.phase2Day`
2. JSON `testMode.enabled = true` → uses JSON `testMode.phase1Day` / `testMode.phase2Day`
3. MCM Phase 1/2 day values (when MCM is available)
4. JSON `phase1.triggerDay` / `phase2.triggerDay` (fallback when MCM is unavailable, on a client without MCM settings only; a dedicated server uses `taom-server-settings.json` ([server-settings](server-settings.md)))

For the master enable/disable toggle: MCM `WarOfTheRingEnabled` takes precedence over JSON `enabled` when MCM is available.

**MCM values persist per player.** `TaomSettings` is an `AttributeGlobalSettings`, so MCM writes the chosen days to `Documents/Mount and Blade II Bannerlord/Configs/ModSettings/Global/TAOM/`. Retuning the C# defaults only reaches fresh installs — a player who has already launched the mod keeps their stored days until they move the sliders or delete that folder.

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

### Phase state (mostly time-derived, phase + outcome persisted)

Phase escalation is driven by elapsed campaign days, so it survives save/load without migration. Each campaign tick:
1. Calculates elapsed campaign days via `CampaignStartTime.ElapsedDaysUntilNow` (engine-provided, survives save/load)
2. Compares against phase thresholds (from MCM or JSON config)
3. Declares wars only for pairs not already at war (`HasDeclaredWar` guard: the real stance, not the model-aware `AreAtWar`; #772)
4. Peace blocking checks elapsed time in real-time

`WarOfTheRingBehavior.SyncData` persists two ints: `WarOfTheRing_CurrentPhase` (Phase 9b #129, so past-Phase2 saves don't replay transitions) and `WarOfTheRing_Outcome` (WotR-momentum #327: the victor once the war ends). Both are additive keys; older saves lacking them load as `Peace` / `None` (vanilla `SyncData` returns false on a missing key without overwriting the ref). The elapsed-time computation still drives escalation; the persisted phase just avoids re-firing transitions on load. `ReconcileDeclaredWars` re-runs the declarations, not the transition, on every host session launch.

A new campaign in the same process now starts from a reset service (#764). `WarOfTheRingBehavior` is built fresh in every campaign's `OnGameStart`, before any campaign event and before a load's `SyncData`, and its constructor calls `ResetForNewSession()`; a load then restores the saved phase and outcome in `SyncData`. The service is a process-lifetime singleton and `SyncData` never runs in loading mode for a brand-new game, so before the fix a second campaign inherited the previous phase and outcome: stuck at FullWar it never declared the Phase 1 and 2 wars, and at WarEnded `EndWar` was a no-op. The reset cannot live in `OnSessionLaunched`, because two readers get there first. Campaign-event listeners run newest-first, so the momentum behavior, added after this one, runs its own session-launch code before ours; and vanilla caches each kingdom's at-war list in `OnNewGameCreated`, which the engine dispatches before `OnSessionLaunched`. The reset also cannot live in `RegisterEvents`: on a load that runs after `SyncData` and would wipe the restored phase. `WarOfTheRingWiringTests` pins that SubModule builds the behavior inline as `AddBehavior`'s argument, exactly once, and that no other production file constructs or registers it, since a cached or container-singleton instance would reset only once per process. The fix prevents new cases only: a save already written by an affected campaign keeps the phase it saved.

Benefits: MCM config changes take effect immediately on next tick. No save migration needed (new keys default safely).

### Phases

`WarPhase` (`Main/Features/Diplomacy/Models/WarPhase.cs`) has four values: `Peace` → `IsengardWar` → `FullWar` → `WarEnded`. The first three are the escalation ladder; **`WarEnded`** is a terminal state set by `IWarOfTheRingService.EndWar(WarOutcome)` when the [War of the Ring Momentum](war-of-the-ring-momentum.md) feature's victory condition fires. Because all three peace-block layers key off `IsWarOfTheRingActive` / `ShouldBlockPeace` (which are true only in `FullWar`), flipping to `WarEnded` lifts the peace block so the winning side's momentum service can peace-out the war.

### MCM Integration via ITaomSettingsProvider

MCM settings are accessed through an injected `ITaomSettingsProvider` interface rather than direct static access to `TaomSettings.Instance`. This:
- Keeps the service testable (tests inject a mock with `IsAvailable = false`)
- Follows the explicit-dependency-via-constructor-injection principle
- Falls back to JSON config when MCM is unavailable, on a client without MCM settings only; a dedicated server uses `taom-server-settings.json` ([server-settings](server-settings.md))

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Diplomacy/Models/WarPhase.cs` | Phase enum (Peace/IsengardWar/FullWar/WarEnded) |
| `Main/Features/Diplomacy/Models/WarVeto.cs` | Why a war is refused (`None`, `Permanent`, `SameAlignment`, `PhasePeace`, `PhaseNotScripted`); `GetWarVeto` tests them in that order, the side before the phase |
| `Main/Features/Diplomacy/Models/WarVetoReasons.cs` | Player-facing refusal text per veto |
| `Main/Features/Diplomacy/Hooks/AllianceCampaignBehavior_StartCallToWarAgreement_Patch.cs` | Harmony Prefix: no call-to-war agreement or gold for a war the rule refuses |
| `Main/Features/Diplomacy/Hooks/ProposeCallToWarAgreementDecision_IsAllowed_Patch.cs` | Harmony Prefix: the propose decision is not allowed when the war is vetoed |
| `Main/Features/Diplomacy/Hooks/AcceptCallToWarAgreementDecision_IsAllowed_Patch.cs` | Harmony Prefix: the accept decision is not allowed when the war is vetoed |
| `Main/Features/Diplomacy/Hooks/AllianceCampaignBehavior_OnCallToWarAgreementProposedByPlayerKingdom_Patch.cs` | Harmony Prefix: no call-to-war map notice for a player kingdom when the war is vetoed |
| `Main/Features/Diplomacy/Models/WarOfTheRingConfig.cs` | Config data models |
| `Main/Features/Diplomacy/Models/TaomDiplomacyModel.cs` | GameModel: `IsAtConstantWar` override, and `GetScoreOfDeclaringWar`, which subtracts the veto penalty |
| `Main/Features/Diplomacy/IWarOfTheRingService.cs` | Service interface |
| `Main/Features/Diplomacy/WarOfTheRingService.cs` | Core logic — phase transitions, war declarations |
| `Main/Features/Diplomacy/IWarOfTheRingConfigProvider.cs` | Config loading interface |
| `Main/Features/Diplomacy/WarOfTheRingConfigProvider.cs` | JSON loader |
| `Main/Features/Diplomacy/ITaomSettingsProvider.cs` | MCM settings interface (testable) |
| `Main/Features/Diplomacy/TaomSettingsProvider.cs` | MCM settings implementation |
| `Main/Features/Diplomacy/WarOfTheRingBehavior.cs` | DailyTick timer (uses CampaignStartTime); its constructor resets the singleton service for each campaign (#764); on session launch (host only) it calls `ReconcileDeclaredWars`, which declares any Full War wars an older save never declared, then rechecks the phase (#772) |
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

- `TAOM.Tests/Features/Diplomacy/WarOfTheRingServiceTests.cs`: 58 tests covering phase transitions, war declarations, peace blocking, test mode, idempotent re-checks, the day-ordering clamp across all four sources, the session reset (#764), and the real-stance guard and `ReconcileDeclaredWars` (#772), including the game-like `AreAtWar` stub, the MCM enable toggle and the declaration-had-no-effect log
- `TAOM.Tests/Features/Diplomacy/WarOfTheRingBehaviorSessionResetTests.cs`: 8 tests pinning the constructor reset, the host session launch calling the repair before the phase check, the daily tick, a save-then-load round trip that restores a non-default phase and outcome, a save without the keys, a new campaign after a WarEnded campaign, a save pass doing neither, and the co-op client gate on `OnSessionLaunched` (no phase check, no reconcile)
- `TAOM.Tests/Features/Diplomacy/WarOfTheRingWiringTests.cs`: 7 source-text tests pinning that SubModule builds the behavior inline as `AddBehavior`'s argument exactly once, that no other production file constructs or registers it, and that the predicate rejects cached, static, duplicated and container-resolved shapes (untagged, so the reference-assembly build runs them)
- `TAOM.Tests/Features/Diplomacy/WarOfTheRingPhaseDayTests.cs`: 5 tests for `GetEffectivePhaseDay` (#765): the Phase 1 and Phase 2 days, the MCM Test Mode days, the clamp on an inverted MCM pair, and null for `Peace`, `WarEnded` and an undefined value
- `TAOM.Tests/Features/Diplomacy/WarOfTheRingConfigProviderTests.cs`: 18 tests, one per `ValidateConfig` rule (phase + test-mode ordering, sub-config nulls, scripted wars lists: null list, null entry, self pair, blank id, valid pairs kept; missing file, malformed JSON)
- `TAOM.Tests/Features/Diplomacy/WarOfTheRingShippedConfigTests.cs`: pins the shipped `war_of_the_ring.json`, so a doc/code drift in the shipped days fails the suite rather than waiting for a review; the phase 1 list must be exactly `isengard->vlandia` and `empire->vlandia`
- `TAOM.Tests/Features/Diplomacy/WarOfTheRingWarVetoTests.cs`: `GetWarVeto` in every phase (including the side-before-phase order and player-caused wars), the score penalties, `IsEnabled`, and `EndWarsForbiddenByPhase` (the player kingdom's phase-vetoed war is kept, its lore-vetoed war ended)
- `TAOM.Tests/Features/Diplomacy/WarOfTheRingBehaviorPhaseWarsTests.cs` (`RequiresGame`): the sweep runs on the daily tick after the phase check, authority only and never when deferring to the host, and not on session launch
- `TAOM.Tests/Features/Diplomacy/WarVetoReasonsTests.cs`: every veto has its own keyed inline text, `Permanent` keeps `taom_war_blocked`, and `None` throws
- `TAOM.Tests/Features/Diplomacy/ShippedKingdomDataTests.cs`: sweeps every ModuleData xml and xslt for a `<relationship>` row that declares war, plus a `LiveInstall` check that the transformed output of `spkingdoms.xslt` has none
- `TAOM.Tests/Features/Diplomacy/DeclareWarActionPatchTests.cs` (`RequiresGame`): which `DeclareWarDetail` values count as player-caused
- `TAOM.Tests/Features/Diplomacy/DiplomacyWarPrefixBindingTests.cs` (`BindingVerification`): pins the war prefixes' parameter names and the decision fields they read against the installed engine
- `TAOM.Tests/Features/Diplomacy/PeaceActionHookTests.cs`: 5 tests for hook behavior

The MCM branch of `GetEffectivePhaseDays` had no coverage until 2026-07-30 — every test pinned `IsAvailable = false` in `Setup()`. When adding a test here, check which of the four day sources it actually exercises.

## How to Test In-Game

1. Set `testMode.enabled = true` in `war_of_the_ring.json` (or enable Test Mode in MCM) — otherwise the phases sit at Day 30 / Day 44
2. Start a new campaign
3. Phase 1 triggers on Day 1: check that Rohan is at war with Isengard and Dunland, and that no other war exists (Arthedain and Gundabad are still at peace). Before Day 1, no war may exist and a war declared from the kingdom screen is refused with the Peace-phase text
4. Phase 2 triggers on Day 3. On Day 2, open Gondor's encyclopedia page: Mordor is not in its Wars list. On Day 3 the chat shows '<ruler> of Gondor declares war on Mordor' (the order of the two kingdoms can be reversed), and both kingdoms' encyclopedia History gains that entry. Expect one such line for every Hostile pair still at peace on Day 2. The Phase 1 pairs (Isengard and Dunland against Rohan) and any war already declared get no new line. Arthedain against Gundabad is one of the Day 3 lines. After Day 3 the Wars list proves nothing, because the constant-war model shows every Hostile pair at war with or without a real declaration (#772). Peace proposals between Hostile pairs are blocked.
5. Verify Harad/Umbar/Khand can still make peace with each other (Rhun cannot — it's a Dark Power)
6. Second campaign in one process (#764): play past the Phase 2 day, exit to the main menu without closing the game, and start a new campaign. The log shows "phase reset to Peace at campaign start", then the Phase 1 wars on day 1, and the War of the Ring meter stays hidden until Full War.
7. Older Full War save (#772): with test mode on, play past Day 3 on a build without the fix (v2.0.34 has the old code) and save. Load that save on this build. The log shows 'War of the Ring: declared N missing Full War wars (#772)' once, and each kingdom's encyclopedia History lists the declarations; the chat shows no line for them, which is expected (Phase 2 above). Save and reload: the line must not appear again. The first load proves the host-only call in `OnSessionLaunched` runs. The quiet second load proves `AllianceAdapter.DeclareWar` wrote the real stance; the old model-aware guard would skip the declaration and log the line on every load.

## Do not add WarPhase values

The four phases are closed. Every rule that holds the war together asks for one phase by equality: the three peace-block layers key on `CurrentPhase == WarPhase.FullWar` (`ShouldBlockPeace`, `IsWarOfTheRingActive`), and momentum enrollment sweeps only in `FullWar` (`MomentumEnrollmentService.SweepEnrollment`). A new value after `FullWar` is not "more war": while the machine sits in it, all three peace-block layers lift at once, Hostile pairs can sign peace, and no kingdom enrolls in the momentum meter. Nothing fails or logs; the war quietly stops being one. The saved phase is an int, so a value inserted before `WarEnded` would also renumber older saves.

Later War of the Ring beats belong in their own state, keyed off the phases rather than added to them. The War Chronicle does this: it reads `CurrentPhase` and `GetEffectivePhaseDay(WarPhase)` and keeps its event states in its own save section ([war-chronicle.md](war-chronicle.md)). War Pledge does the same for a Neutral realm that joins the war: a saved pledge that the alignment and diplomacy services read, not a phase ([war-pledge.md](war-pledge.md)). The Ring mechanic will end the war through `EndWar(outcome)`, the one terminal transition.

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
| arthedain | Arthedain | Free |
| eredluin | Ered Luin | Free |
| empire_s | Mordor | Dark Power |
| isengard | Isengard | Dark Power |
| gundabad | Gundabad | Dark Power |
| dolguldur | Dol Guldur | Dark Power |
| khuzait | Easterlings (Rhun) | Dark Power |
| empire | Dunland | Evil (independent) |
| aserai | Harad | Southern |
| umbar | Umbar | Southern |
| battania | Khand | Neutral |
| cardolan | Cardolan | Neutral |
| rhudaur | Rhudaur | Neutral |
| anduinvale | Anduin Vale | Neutral |
| angaladh | Angáladh | Neutral |
| dorwinion | Dorwinion | Neutral |
| southrhovanion | South Rhovanion | Neutral |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/advanced-start-options.md](./advanced-start-options.md)
- [docs/features/alignment-aware-execution.md](./alignment-aware-execution.md)
- [docs/features/diplomacy.md](./diplomacy.md)
- [docs/features/enlistment.md](./enlistment.md)
- [docs/features/war-chronicle.md](./war-chronicle.md)
- [docs/features/war-of-the-ring-momentum.md](./war-of-the-ring-momentum.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-factions-and-world.md](../modding/configs-factions-and-world.md)
- [docs/modding/id-cheatsheet.md](../modding/id-cheatsheet.md)
- [docs/modding/kingdoms.md](../modding/kingdoms.md)
- [docs/modding/load-order-and-dependencies.md](../modding/load-order-and-dependencies.md)

<!-- backlinks-end -->
## Changelog

- 2026-11-10: War Pledge (#1113). `DiplomacyService.GetRelationshipTier` gives a pledged realm the `Hostile` tier against the other side, so `ReconcileDeclaredWars` and `ShouldBlockPeace` cover it with no change in this feature. See [war-pledge.md](war-pledge.md).
- 2026-10-18: Doc pass for the war rule. `GetWarVeto` checks the side before the phase (a same-side pair reads the alignment text in every phase before War Ended); `CausedByKingdomCreation` counts as player-caused; a seventh prefix, `OnCallToWarAgreementProposedByPlayerKingdom`, keeps vetoed call-to-war notices off a player kingdom's map; the enlistment edge is allowed; the lord-dialogue sponsor option is a known gap (#818, open follow-ups #819, #820).
- 2026-10-18: Follow-up to the war rule: a player-caused war (`CausedByPlayerHostility`, `CausedByCrimeRatingChange`) lifts the phase vetoes; `EndWarsForbiddenByPhase` runs on the daily tick only and spares the player kingdom's phase-vetoed wars; the AI's war score drops by 10,000,000 or 100,000,000 for a vetoed war; two `IsAllowed` prefixes hide impossible call-to-war offers; the war scenarios leave the Advanced Start list (#818).
- 2026-10-18: War rule by phase. Peace allows no war, Isengard War only the phase 1 wars, Full War the old `IsWarAllowed` rule; `EndWarsForbiddenByPhase` ends vetoed wars in saves and in scenario starts; a `StartCallToWarAgreement` prefix keeps gold out of a refused war. Start wars removed from the kingdom data, and `gundabad` to `arthedain` removed from phase 1 (they meet on day 44 through the Hostile row).
- 2026-10-08: `GetEffectivePhaseDay(WarPhase)` exposes the clamped Phase 1 and Phase 2 days to the War Chronicle (#765). "How to Add New War Phases" is replaced by "Do not add WarPhase values": a value after Full War would lift all three peace-block layers and stop momentum enrollment.
- 2026-10-08: Phase 2 now really declares the Hostile-pair wars (#772). The phase turns first and the guard reads the real stance (`HasDeclaredWar`), because the constant-war model had made every pair look already at war and skipped them all; the phase-first order keeps vanilla's call-to-war logic from pulling allies into wars the loop is about to declare. `ReconcileDeclaredWars` declares, at session launch, the Hostile-pair wars that a Full War save still stores as neutral.
- 2026-10-08: A new campaign in the same process no longer inherits the previous campaign's phase and outcome: `WarOfTheRingBehavior`'s constructor calls `WarOfTheRingService.ResetForNewSession()` before any campaign event can read the service (#764).
- 2026-07-30 — Phase defaults retuned to Day 30 (Isengard/Dunland attack Rohan) / Day 44 (full War of the Ring), set at all four sources (shipped JSON, MCM defaults, `TaomSettingsProvider` fallbacks, `WarOfTheRingConfig` compiled defaults). `GetEffectivePhaseDays` now clamps `phase2 > phase1 >= 1` for every source — previously only the JSON pair was validated (strictly-`<`, so equal days passed) while the MCM sliders were unvalidated, and an equal or inverted pair ran both transitions in one tick with `IsengardWar` never observable. RCA: `docs/reviews/rca-wotr-phase-ordering-2026-07-30.md`.
- 2026-05-22 — WotR phase defaults retuned to Day 2 (Phase 1) / Day 14 (Phase 2); `testMode` tightened to 1/3; phase state persists via `SyncData` (`WarOfTheRing_CurrentPhase`).
- 2026-05-13 — Phase 9b: `WarOfTheRingService.CurrentPhase` now persisted (no per-load transition replay); `WarOfTheRingConfigProvider` gains null-literal JSON fallback + semantic TriggerDay validation (closes #129).

## GitHub Issue

- **Issues:** #818 (the war rule by phase; open follow-ups #819 and #820), #764 (a new campaign inherited the previous phase) and #772 (Phase 2 declared no Hostile-pair war), both closed with `triage-needs-ingame`: steps 6 and 7 of "How to Test In-Game" are owed. #765 added `GetEffectivePhaseDay` for the War Chronicle.
- **Status:** Active
