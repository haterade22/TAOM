# Diplomacy

## Overview
Encodes LOTR lore-based relationships between kingdoms (Permanent alliance, Natural alliance, Neutral, Hostile), enforces those relationships at runtime by blocking alliance dissolution and war declarations between permanently-allied kingdoms, and drives a scripted "War of the Ring" escalation that triggers wars between factions on a configurable day schedule.

## Why This Exists
- **Vanilla behavior:** Bannerlord's AI makes alliance and war decisions based solely on power scores, relations, and proximity. Over time kingdoms freely ally with or declare war on any faction including their lore enemies.
- **TAOM requirement:** LOTR factions have fixed alignment (e.g., Gondor and Rohan are permanently allied; Mordor, Gundabad, Isengard, and Dol Guldur are permanently allied). These alliances must not dissolve. Additionally, the War of the Ring (Isengard attacking Rohan, then the full conflict) must trigger on schedule regardless of AI decisions.
- **Without this feature:** Mordor might ally with Gondor; the War of the Ring would never start; permanent lore alliances would be broken by AI diplomacy within days.

## Architecture

### Design Challenge
Two problems exist:

1. **Permanent alliances** — `AllianceCampaignBehavior.EndAlliance` and `DeclareWarAction.ApplyInternal` are sealed TaleWorlds methods. They cannot be overridden; Harmony Prefix patches are needed to block execution when the affected kingdoms are permanently allied.
2. **War of the Ring** — The scripted war must trigger at a configurable day and must respect an MCM override. The AI must be prevented from making peace between hostile-tier kingdoms once the full war is active.

### Solution Approach

**Diplomacy subsystem:**
- `DiplomacyService` (implements `IDiplomacyService`) loads `diplomacy.json` at startup via `IDiplomacyConfigProvider` and builds an in-memory dictionary of `(kingdomA, kingdomB) -> AllianceTier` using a canonical key (alphabetical order).
- `AllianceActionHook` (implements `IOnAllianceAction`) consults `DiplomacyService` to decide whether to block alliance-end and war-declaration events.
- `Patch11_Diplomacy` uses Harmony Prefix patches on `AllianceCampaignBehavior.EndAlliance` and `DeclareWarAction.ApplyInternal`, delegating to `IOnAllianceAction`.
- `TaomAllianceModel` overrides `DefaultAllianceModel.GetScoreOfStartingAlliance` to add a score modifier (+1000 for Permanent, +500 for Natural, -10000 for Hostile) so the AI naturally favors or avoids alliances matching lore.
- `DiplomacyBehavior` (`CampaignBehaviorBase`) establishes initial permanent alliances on new game creation and re-enforces them on every session load.

**Player Alliance Freedom subsystem:**

A player who founds their own kingdom could not form alliances. This is **not** a TAOM block — it is two vanilla limitations: (1) the player can never *initiate* an alliance (`KingdomDecisionProposalBehavior.DailyTickClan` early-returns for `Clan.PlayerClan`; `AllianceCampaignBehavior` only delivers offers *to* the player), and (2) a new player kingdom cannot clear `DefaultAllianceModel.CanMakeAlliance`'s `>= 50f` acceptance score (vanilla scores 0 without bordering fiefs + a >430-threat neighbor), so AI never offers and the v1.4.6 Kingdom→Diplomacy "Propose/Enact Alliance" button stays greyed.

- **Receive + unblock the vanilla button (Part A).** `IDiplomacyService` exposes player-aware overloads: `GetAllianceScoreModifier(a, b, involvesPlayer)` returns `+1000` when the player's kingdom is one of the pair (clears the 50f wall, skips the `-10000` Hostile penalty), and `IsAllianceDecisionAllowed(a, b, involvesPlayer)` returns true for any player-involved pair. `TaomAllianceModel` and `TaomKingdomDecisionPermissionModel` compute `involvesPlayer = querier == Clan.PlayerClan?.Kingdom || queried == Clan.PlayerClan?.Kingdom` at the boundary and delegate. The v1.4.6 Kingdom-screen button's enable-gate routes through `StartAllianceDecision.IsAllowed()` (→ `IsStartAllianceDecisionAllowedBetweenKingdoms` → the permission model) and `CanMakeDecision()` (→ `CanMakeAlliance` → the alliance score), so Part A turns the *existing vanilla button* on. **No custom UI was built** — the vanilla button already exists; a UIExtenderEx duplicate would only add crash surface.
- **Initiate via dialog (Part B).** `PlayerAllianceProposalBehavior` (`CampaignBehaviorBase`) registers a conversation line — "I propose an alliance between our realms." — that appears when a player who *rules* their kingdom talks one-on-one to another kingdom's *ruler*. On accept it forms the alliance via `IAllianceAdapter.StartAlliance`. Gated by `CanPlayerProposeAlliance` (ids present + distinct, at peace, not already allied). Stateless (alliances persist via vanilla's `AllianceCampaignBehavior`).
- **Full freedom (design decision):** player-involved pairs ignore the lore `Hostile` tier — a Gondor-culture player kingdom may ally Mordor. AI-vs-AI diplomacy is unchanged: the retained 2-arg `GetAllianceScoreModifier`/`IsAllianceAllowed` paths behave exactly as before, and `involvesPlayer:false` is byte-identical to the legacy behavior.
- **Cost asymmetry (intentional):** the dialog forms the alliance directly at **0 influence**; the vanilla Kingdom-screen button uses the influence-cost `StartAllianceDecision` flow (~200 influence for a multi-clan kingdom). Two deliberate paths.
- `MaxNumberOfAlliances => int.MaxValue` (pre-existing on `TaomAllianceModel`) removes the vanilla cap of 2 — model-global, so it also lets AI kingdoms exceed 2 alliances.
- **Durability (2026-06-17 follow-up — UNDER INVESTIGATION, diagnostics only).** A formed player alliance was reported vanishing from the encyclopedia. Confirmed vanilla mechanism: `AllianceCampaignBehavior.OnWarDeclared` (AllianceCampaignBehavior.cs:678-681) calls `EndAlliance` the instant war is declared between two allied kingdoms, and TAOM's `AllianceCampaignBehavior_EndAlliance_Patch` protects **only `Permanent`-tier** pairs — a player alliance is `Neutral` → unprotected. But the *trigger* (what declares the war) was never reproduced, so form-then-broken vs never-persists is **unconfirmed**. A first-pass fix (a `DiplomacyService.IsWarAllowed` branch blocking war between the player's ruled kingdom and a current ally) was **reverted** — `/review-codex` showed it soft-locks the player: v1.4.6 has **no "break alliance" UI**, so the player's only exit from an alliance is to *declare war on the ally* (`KingdomDiplomacyVM` → `DeclareWarDecision` → `DeclareWarAction.ApplyByKingdomDecision`), which the block prevented at both the permission model and the `DeclareWarAction` prefix. `IsWarAllowed` is back to its prior behavior (Permanent + same-alignment only). Next step is diagnostics-first (below), then a targeted fix against the confirmed cause.
- **Diagnostics (TEMPORARY — strip after in-game sign-off).** `AllianceCampaignBehavior_StartAlliance_Patch` (Postfix, `Patch11_Diplomacy`) logs `[Diplomacy][diag] Player alliance FORMED` on any path (kingdom-screen button + dialog); the `EndAlliance` patch logs `[Diplomacy][diag] Player alliance END attempt`. Together they distinguish **form-then-break** (FORMED then END attempt — and what war triggered it) from **never-persist** (FORMED never logs). These are the only behavior in the 2026-06-17 follow-up that ships; they make no gameplay change. Remove once the root cause is confirmed in-game and the real fix lands (per `feedback_comprehensive_diag_logging_then_remove`).

**War of the Ring subsystem:**
- `WarOfTheRingService` (implements `IWarOfTheRingService`) tracks `CurrentPhase` (`Peace`, `IsengardWar`, `FullWar`). Each daily tick it checks elapsed campaign days against configured thresholds and transitions phases.
- Phase 1 (`IsengardWar`): declares specific wars from `phase1.wars` in `war_of_the_ring.json`.
- Phase 2 (`FullWar`): declares more wars, and if `autoWarBetweenHostileTiers` is true sweeps all hostile-tier kingdom pairs and declares war between any that are not already at war.
- `PeaceActionHook` (implements `IOnPeaceAction`) blocks `MakePeaceAction.ApplyInternal` when `FullWar` is active and the two factions are in a hostile-tier relationship.
- Phase day thresholds are overridable via MCM (`ITaomSettingsProvider` wraps `TaomSettings`); a test-mode with short day counts is available in the config.

### Component Diagram
```
CampaignEvents.OnNewGameCreatedPartialFollowUpEvent
CampaignEvents.OnSessionLaunchedEvent
        |
DiplomacyBehavior --> IDiplomacyService.EstablishInitialAlliances
                      IDiplomacyService.EnforcePermanentAlliances
                               |
                         IAllianceAdapter (StartAlliance, AreAllied)

CampaignEvents.DailyTickEvent
        |
WarOfTheRingBehavior --> IWarOfTheRingService.CheckPhaseTransition
                                  |
                            IAllianceAdapter (DeclareWar, AreAtWar)
                            IDiplomacyService.GetRelationshipTier

Harmony Patch11_Diplomacy
  AllianceCampaignBehavior.EndAlliance [Prefix] -> IOnAllianceAction.ShouldPreventAllianceEnd  (+ [diag] log)
  DeclareWarAction.ApplyInternal       [Prefix] -> IOnAllianceAction.ShouldPreventWarDeclaration -> IsWarAllowed (Permanent + same-alignment only)
  MakePeaceAction.ApplyInternal        [Prefix] -> IOnPeaceAction.ShouldPreventPeace
  AllianceCampaignBehavior.StartAlliance [Postfix] -> [diag] log (TEMPORARY, player-involved)

GameModel: TaomAllianceModel : DefaultAllianceModel
  GetScoreOfStartingAlliance -> IDiplomacyService.GetAllianceScoreModifier(a,b,involvesPlayer)

GameModel: TaomKingdomDecisionPermissionModel : DefaultKingdomDecisionPermissionModel
  IsStartAllianceDecisionAllowedBetweenKingdoms -> IDiplomacyService.IsAllianceDecisionAllowed(a,b,involvesPlayer)

Conversation dialog (initiate)
  PlayerAllianceProposalBehavior --> IDiplomacyService.CanPlayerProposeAlliance
                                     IDiplomacyService.FormPlayerAlliance
                                              |
                                        IAllianceAdapter.StartAlliance

Vanilla Kingdom->Diplomacy "Propose/Enact Alliance" button (receive/initiate)
  StartAllianceDecision.IsAllowed      -> TaomKingdomDecisionPermissionModel (player bypass)
  StartAllianceDecision.CanMakeDecision -> TaomAllianceModel score (+1000 player bonus clears 50f gate)
```

## Configuration

### `Main/_Module/ModuleData/diplomacy/diplomacy.json`
Defines all kingdom pair relationships. Each entry has `kingdomA`, `kingdomB`, and `tier` (`Permanent`, `Natural`, `Neutral`, or `Hostile`). Keys are matched order-insensitively.

Current data: 5 Free Peoples permanent alliances, 11 Natural alliances, 10 Dark Powers permanent alliances, and 33 Hostile pairs.

### `Main/_Module/ModuleData/diplomacy/war_of_the_ring.json`
Controls the scripted war escalation:
```json
{
  "enabled": true,
  "phase1": { "triggerDay": 1, "wars": [{"attacker":"isengard","defender":"vlandia"}, ...] },
  "phase2": { "triggerDay": 1, "autoWarBetweenHostileTiers": true, "blockPeaceBetweenHostileTiers": true, "wars": [] },
  "testMode": { "enabled": false, "phase1Day": 2, "phase2Day": 5 }
}
```
Both `triggerDay` values are currently set to 1 (immediate on new game). MCM overrides `phase1TriggerDay` and `phase2TriggerDay` at runtime.

### MCM Settings (`TaomSettings`)
| Setting | Default | Description |
|---------|---------|-------------|
| Enable War of the Ring | `true` | Master switch for phase transitions |
| Phase 1 Start Day | `30` | MCM override for Phase 1 trigger (Isengard attacks Rohan) |
| Phase 2 Start Day | (see TaomSettings.cs) | MCM override for Phase 2 trigger (full war) |

## Key Files
| File | Purpose |
|------|---------|
| `Main/Features/Diplomacy/DiplomacyIoC.cs` | DryIoc registrations and static `InitializeHooks` wiring |
| `Main/Features/Diplomacy/DiplomacyBehavior.cs` | `CampaignBehaviorBase`: establishes/enforces permanent alliances on new game and session load |
| `Main/Features/Diplomacy/WarOfTheRingBehavior.cs` | `CampaignBehaviorBase`: daily tick drives phase transition checks |
| `Main/Features/Diplomacy/PlayerAllianceProposalBehavior.cs` | `CampaignBehaviorBase`: conversation dialog letting a player kingdom-ruler initiate an alliance with another ruler |
| `Main/Features/Diplomacy/IDiplomacyService.cs` | Service interface: tier lookup, score modifier, alliance enforcement, player-freedom overloads + proposal methods |
| `Main/Features/Diplomacy/DiplomacyService.cs` | Implementation: loads config, computes scores, enforces alliances, player-freedom score/permission + `CanPlayerProposeAlliance`/`FormPlayerAlliance` |
| `Main/Features/Diplomacy/IWarOfTheRingService.cs` | Service interface: phase state, peace blocking, phase transition |
| `Main/Features/Diplomacy/WarOfTheRingService.cs` | Implementation: phase state machine, war declarations |
| `Main/Features/Diplomacy/DiplomacyConfigProvider.cs` | Deserializes `diplomacy.json` |
| `Main/Features/Diplomacy/WarOfTheRingConfigProvider.cs` | Deserializes `war_of_the_ring.json` |
| `Main/Features/Diplomacy/TaomSettingsProvider.cs` | Wraps `TaomSettings` MCM for testable access |
| `Main/Features/Diplomacy/Hooks/AllianceActionHook.cs` | `IOnAllianceAction` — decides whether to block alliance end / war declaration |
| `Main/Features/Diplomacy/Hooks/PeaceActionHook.cs` | `IOnPeaceAction` — decides whether to block peace during full war |
| `Main/Features/Diplomacy/Hooks/AllianceCampaignBehavior_EndAlliance_Patch.cs` | Harmony Prefix — calls `IOnAllianceAction.ShouldPreventAllianceEnd` (+ temporary `[diag]` log of player-involved end attempts) |
| `Main/Features/Diplomacy/Hooks/AllianceCampaignBehavior_StartAlliance_Patch.cs` | Harmony Postfix — **TEMPORARY diagnostic** (`Patch11_Diplomacy`): logs `[Diplomacy][diag] Player alliance FORMED` on any path (kingdom-screen button + dialog). Strip after in-game durability sign-off. |
| `Main/Features/Diplomacy/Hooks/DeclareWarAction_ApplyInternal_Patch.cs` | Harmony Prefix — calls `IOnAllianceAction.ShouldPreventWarDeclaration` (→ `IsWarAllowed`: blocks war on Permanent allies + same-alignment pairs) |
| `Main/Features/Diplomacy/Hooks/MakePeaceAction_ApplyInternal_Patch.cs` | Harmony Prefix — calls `IOnPeaceAction.ShouldPreventPeace` |
| `Main/Features/Diplomacy/Models/TaomAllianceModel.cs` | `DefaultAllianceModel` override: adds lore score modifier to alliance scoring |
| `Main/Features/Diplomacy/Models/TaomKingdomDecisionPermissionModel.cs` | `DefaultKingdomDecisionPermissionModel` override: blocks lore-Hostile alliance decisions (AI pairs) but allows any decision involving the player's kingdom (full freedom); also blocks war on permanent allies + peace during full War of the Ring |
| `Main/Features/Diplomacy/Models/TaomDiplomacyModel.cs` | Additional diplomacy model override |
| `Main/Features/Diplomacy/Models/AllianceTier.cs` | Enum: `Permanent`, `Natural`, `Neutral`, `Hostile` |
| `Main/Features/Diplomacy/Models/KingdomRelationship.cs` | POCO: kingdom pair + tier |
| `Main/Features/Diplomacy/Models/DiplomacyConfig.cs` | POCO: list of `KingdomRelationship` |
| `Main/Features/Diplomacy/Models/WarOfTheRingConfig.cs` | POCO: phase configs and test mode |
| `Main/Features/Diplomacy/Models/WarPhase.cs` | Enum: `Peace`, `IsengardWar`, `FullWar` |
| `Main/_Module/ModuleData/diplomacy/diplomacy.json` | Kingdom relationship data |
| `Main/_Module/ModuleData/diplomacy/war_of_the_ring.json` | War of the Ring phase config |

## Dependencies
- `IAllianceAdapter` — wraps `AllianceCampaignBehavior`, `StanceLink`, `Kingdom` (sealed TaleWorlds types); provides `AreAllied`, `StartAlliance`, `AreAtWar`, `DeclareWar`, `MakePeace`, `GetAllKingdomIds`
- `IDiplomacyConfigProvider` — loads diplomacy.json
- `IWarOfTheRingConfigProvider` — loads war_of_the_ring.json
- `ITaomSettingsProvider` — wraps `TaomSettings` MCM
- `IModLogger` — logging

## Tests
| File | Coverage |
|------|---------|
| `TAOM.Tests/Features/Diplomacy/DiplomacyServiceTests.cs` | Tier lookup, score modifiers, alliance allowed/blocked, initial alliance establishment, enforcement, player-freedom score/permission overloads, `CanPlayerProposeAlliance` (same/empty/at-war/already-allied/Hostile-tier), `FormPlayerAlliance`, `IsWarAllowed` (Permanent/same-alignment/neutral) |
| `TAOM.Tests/Features/Diplomacy/WarOfTheRingServiceTests.cs` | Phase transitions, peace blocking, MCM override, test-mode day overrides, hostile-tier auto-war |
| `TAOM.Tests/Features/Diplomacy/DiplomacyConfigProviderTests.cs` | JSON parsing, missing file handling |
| `TAOM.Tests/Features/Diplomacy/AllianceActionHookTests.cs` | `ShouldPreventAllianceEnd` and `ShouldPreventWarDeclaration` for permanent vs non-permanent tiers |
| `TAOM.Tests/Features/Diplomacy/PeaceActionHookTests.cs` | `ShouldPreventPeace` during active vs inactive War of the Ring |

## How to Add a New Kingdom Relationship

1. Open `Main/_Module/ModuleData/diplomacy/diplomacy.json`.
2. Add an entry: `{ "kingdomA": "your_kingdom", "kingdomB": "other_kingdom", "tier": "Permanent" }`. Kingdom IDs must match their `StringId` values in the game (e.g., `empire_w`, `erebor`, `empire_s`).
3. Tier values: `Permanent` (score +1000, alliance enforced, war blocked), `Natural` (score +500, AI likely to ally), `Neutral` (no modifier), `Hostile` (score -10000, alliance blocked, war auto-declared in Phase 2 if `autoWarBetweenHostileTiers` is set).
4. No code changes needed — `DiplomacyService` reads the file at startup.

## How to Add a War of the Ring Phase War

1. Open `Main/_Module/ModuleData/diplomacy/war_of_the_ring.json`.
2. Add a `WarDeclaration` entry to the appropriate phase's `wars` array: `{ "attacker": "empire_s", "defender": "empire_w" }`.
3. The war is declared idempotently (only if the factions are not already at war).

## Changelog

- 2026-06-17 — Instrumented player-alliance loss with `[Diplomacy][diag]` logging only; the durability war-block (`DiplomacyService.IsWarAllowed` branch) was reverted after review (it soft-locked the player out of the only alliance-exit path).
- 2026-06-16 — Let player-founded kingdoms form alliances: player-aware service overloads unblock the vanilla Kingdom→Diplomacy button (Part A) and a new `PlayerAllianceProposalBehavior` dialog lets a kingdom-ruler initiate (Part B); full freedom, AI-vs-AI diplomacy unchanged.
- 2026-05-22 — War of the Ring phase defaults retuned to Day 2 (Phase 1) / Day 14 (Phase 2), both MCM-tunable.
- 2026-05-22 — Split peace + alliance invariants in `EnforcePermanentAlliances` (Mordor showing in both Wars and Alliances lists); closed Dale↔Isengard gap.
- 2026-05-22 — Promoted Harad (`empire_s ↔ aserai`) from Natural to Permanent alliance with Mordor + added MakePeace step in alliance enforcement.
- 2026-05-19 — Blocked war between same-alignment kingdoms (#203).
- 2026-05-13 — War of the Ring phase persistence via SyncData + config validation in the JSON providers (#129); plus prefix documentation + diagnostic logs (#152, #153).
- 2026-04-09 — Added `CanMakeAlliance` override to `TaomAllianceModel` as a hard gate (via `IDiplomacyService.IsAllianceAllowed`) for permanently hostile factions.
- 2026-03-27 — Added diagnostic/initialization logging to diplomacy enforcement hooks, behaviors, and the 3 diplomacy Harmony patches.

## GitHub Issue
- **Issue:** Unknown (commits reference `16f7f4e` for initial implementation; no issue number in messages)
- **Status:** Active

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
