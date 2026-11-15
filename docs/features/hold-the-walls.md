# Hold the Walls

## Overview

When an AI army of a Free realm takes a town or castle by siege, it stays inside for a while instead of marching off at once. Vanilla pauses a capturing army leader for 10 hours; Hold the Walls lengthens that same pause, for 12 to 30 hours from the capture (22 for an average lord) by the lord's Valor and Calculating. Mordor's armies hold too (the `Factions` list, #1283); other Evil armies, and Neutral ones by default, keep rolling on. Nothing is minted: the army's presence is the whole effect. Single-player only.

## Why This Exists

- **Vanilla behavior:** after a siege win every capturing army leader enters the fortress and its AI is disabled for 10 hours (`KingdomManager.SiegeCompleted`, `KingdomManager.cs:241-274`). A captured settlement also leaves the target pool of the army targeting model ([army-targeting.md](army-targeting.md)), so nothing keeps the army near it afterwards.
- **TAOM requirement:** when Gondor retakes Osgiliath or Cair Andros its army should dig in for a time rather than leave the fortress exposed while its new garrison is thin.
- **Without this feature:** the capturing army marches away after about 10 hours and the enemy walks back in.

## What the player sees

Nothing is shown. There is no notice and no new text, so there is nothing to localize. An AI army of a Free realm (or of Mordor) that took a fortress is simply still inside it on the map for about a day, and the log carries one `[HoldTheWalls]` info line when a hold starts or ends early and one debug line per day.

## Rules

| Rule | Detail |
|---|---|
| Recorded | A siege capture of a town or castle: `OnSettlementOwnerChangedEvent` with the detail `BySiege` (`ChangeOwnerOfSettlementAction.cs:94`, `:117`). Every other change and every village is ignored. The event fires inside the action, before the capturing parties enter the fortress and before their 10 hour pause, so the record is judged on the next hourly pass. A second siege capture of the same fortress replaces the unjudged record; the hold the fortress had is not touched there, and the next pass releases it once its army no longer belongs to the fortress's realm. |
| Who holds | Each army whose leader stands inside the fortress, whose leader belongs to the realm that now owns it, and who is not led by the player and has not been joined by the player. Found through the fortress's own parties (`Settlement.Parties`), not through the hero the owner-change event names: that hero owns the party that won the battle (`KingdomManager.cs:260`), which can be an army member rather than the army's leader. |
| Start gates | The leader's realm resolves to a side listed in `CapturedFortHold.Sides` (default `Free`; Neutral and Evil roll on), or the realm's id is in `CapturedFortHold.Factions` (shipped: Mordor); cohesion at least `MinCohesionToHold` (45); the leanest party has at least `ReleaseFoodDays` of food (a hold that would be released at once is never started); the fortress is not under siege (the same test that releases a hold, `HoldTheWallsMath.MayStart`); the leader is not in a map event; the leader's AI is still paused (the engine paused only the leaders that took the fortress, so a same-realm army that walked in later is skipped; an army that fled into the fortress just before the first pass also reads paused, because `EnterSettlementAction` pauses a fleeing party for 5 hours, and may be held, an edge accepted). A capture is judged once; an army that fails a gate is not revisited. |
| Length | `clamp(BaseHours + PerTraitPointHours x (Calculating - Valor), MinHours, MaxHours)` = `clamp(22 + 5 x (Calculating - Valor), 12, 30)` hours from the capture hour. Code bounds apply whatever the file says: never above 32 and never at or below 10. The ceiling comes from the engine's inactivity disband: an army whose leader waits on Hold for 48 hours is disbanded (`Army.CheckInactivity`, `Army.cs:442-477`, threshold `Army.cs:174`). The hold counts toward that allowance and releasing it does not reset the counter, so the 30 hour ceiling leaves the army about 14 or more hours to march out. |
| How | `MobilePartyAi.DisableForHours(ceil(end - now))` on the army leader, the call vanilla makes (`MobilePartyAi.cs:1571`). The engine saves the pause (`_isDisabled`, `_enableAgainAtHour`, `MobilePartyAi.cs:76`, `:118`) and ends it itself in the leader's AI tick (`:437-441`). |
| Early end | Each hourly pass, in this order. First the gates, which end every hold: a co-op session is live, a co-op mod TAOM cannot probe is loaded, or this is a dedicated server; then the MCM switch (or AI Strategic Intelligence) is off. With an unreadable clock the pass does nothing else, and the gates still resume the AI. Then each hold in turn: its time ran out (dropped, no write); its army is gone; the army left the fortress or its realm no longer owns it; the player joined the army; the fortress is under siege; cohesion is below `ReleaseCohesion` (40); the leanest party has less than `ReleaseFoodDays` (1) of food. A NaN reading fails the requirement and releases. |
| Release | Restores what is left of the engine's own 10 hours, counted from the capture: `DisableForHours(ceil(remaining))` while some remain, `EnableAi()` (`MobilePartyAi.cs:1583`) once they are over. The hours round up, so the pause can outlast the engine's own by less than an hour. A gone or moved army is released the same way, because its leader may live on with TAOM's longer pause. |
| Dropped, no write | A hold whose time ran out: the engine's timer ended the pause, so nothing is written. A hold whose hourly step throws is also dropped with no write (the error is logged), so that leader keeps TAOM's pause until the hold's end. |
| Inactivity disbands | The behavior also listens to `CampaignEvents.ArmyDispersed` (`Action<Army, Army.ArmyDispersionReason, bool>`). The service counts the AI armies (not the player's) the engine disbands with reason `Inactivity`, whether the switch is on or off (still single-player only), and the daily line reports `inactivityDisbands=N`. Two runs compare it: a rise with the switch on is what the hold costs. |
| Records | In memory only. A campaign starts with none (the behavior's constructor resets the service), and a loaded save starts with none: a hold that was running keeps its saved pause, ends by the engine's timer at the latest, and can no longer be released early. |
| Nothing minted | No troops, food, wall repair, garrison or building progress. Vanilla already leaves a garrison on entry, repairs walls daily, appoints a governor and lets parties buy food; the hold adds the army's presence. No `Army.HourlyTick` patch (it would freeze cohesion and the disband rules) and no write to `AiBehaviorObject`. |
| Budget (proposed) | The hourly pass is under 1 ms per campaign hour for all captures and holds together, the budget Watchful Holds and Fire and Tribute keep. With nothing recorded the pass returns before it reads anything. `maxHourMs` in the daily debug line is the measure; it is owed from a real campaign. |

## Architecture

### Design Challenge

The pause is the engine's own and the engine saves it, so the feature must never own the army's schedule: a hold that persisted its own end time would leave an army stuck after a load or a mod removal. The 48 hour inactivity disband bounds any hold inside a fortress. The target-score model cannot make an army stay, because a captured fortress is no longer a candidate and pushing the other targets down risks that same disband (see [army-targeting.md](army-targeting.md)).

### Solution Approach

A feature module (`HoldTheWallsModule`, one line appended to `Main/Composition/FeatureModules.cs`; `Main/IoC.cs` and `Main/SubModule.cs` are untouched). A thin behavior routes the owner-change event, the army dispersal and the hourly and daily ticks to a service; the service holds the rules and talks to an adapter that reads armies as plain values and writes only the AI pause. Rejected: patching `Army.HourlyTick`, minting resources, a 3 to 7 day hold (it needs a "defend" order whose vanilla score divides by zero with no attacker).

### Component Diagram

```
army_targeting.json  ("CapturedFortHold" section, validated)
        |
ArmyTargetingConfigProvider --------- IArmyTargetingConfigProvider
                                              |
HoldTheWallsBehavior (thin)  ---->  HoldTheWallsService ----> HoldTheWallsMath (pure)
  OnSettlementOwnerChangedEvent        |   |   |
  ArmyDispersed (Inactivity only)      |   |   +--> IArmyTargetingSettingsProvider (MCM)
  HourlyTickEvent, DailyTickEvent      |   |        HoldCapturedForts, EnableArmyStrategicIntelligence
                                       |   +------> IAlignmentService.ResolveSide(SideIds)
                                       |   +------> ICoopSessionProvider, IDedicatedServerProvider
                                       v
                              IFortHoldAdapter / FortHoldAdapter (Main/Adapters/)
                                reads: fortress parties, army, cohesion, food, traits, siege, map event
                                writes: MobilePartyAi.DisableForHours, MobilePartyAi.EnableAi (nothing else)
```

### Engine facts (v1.5.5)

| Fact | Where |
|---|---|
| A siege win moves every capturing leader into the fortress and pauses its AI for 10 hours | `KingdomManager.cs:260-273` |
| The owner-change event fires inside the action, with the detail and the clan's party owner as capturer | `ChangeOwnerOfSettlementAction.cs:94`, `:111-117` |
| The new owner is the capturing kingdom's leader, so the fortress's map faction is the capturer's kingdom | `KingdomManager.cs:247`, `ChangeOwnerOfSettlementAction.cs:30` |
| A later claimant vote raises a second owner change inside the same realm, so a non-siege change does not end a hold | `SettlementClaimantDecision.cs:287`, `ChangeOwnerOfSettlementAction.cs:60` |
| The only callers of `DisableForHours` in the campaign code are this capture, a fleeing party entering a settlement (5 hours) and a rebel party (5 hours); the NavalDLC module is not counted, because TAOM declares it incompatible | `KingdomManager.cs:273`, `EnterSettlementAction.cs:63`, `RebellionsCampaignBehavior.cs:331` |
| An army whose leader waits on Hold gains inactivity each hour and disbands at 48 | `Army.cs:174`, `:442-477` |
| An army hourly tick does nothing while its leader sits in a besieged settlement; cohesion below 30 disbands it | `Army.cs:341-343`, `:358` |
| `GetNumDaysForFoodToLast` casts the quotient to int; a party that eats nothing divides by zero and reads as `int.MinValue`, so the adapter takes the same quotient as a float | `MobileParty.cs:3602-3611`, `FoodChange` at `:964` |
| The fortress's parties are `Settlement.Parties`; under siege is `SiegeEvent != null` | `Settlement.cs:261`, `:460` |

## Configuration

### MCM (in-game, "AI Strategic Intelligence" group)

| Setting | Default | Description |
|---|---|---|
| Good armies hold captured forts | on | The hold. Takes effect within the hour, no restart. Effective only while "Enable AI Strategic Intelligence" is on. Read through `IArmyTargetingSettingsProvider.HoldCapturedForts`. Holds are not saved, so after loading a save a running hold cannot end early. |

### Config File: `Main/_Module/ModuleData/configs/army_targeting.json`, section `CapturedFortHold`

Read once per Bannerlord process (the provider is a `Reuse.Singleton`): an edit needs a full application restart, not a new campaign and not a save-load. Every value is validated on load; a rejected field reverts to its default with a warning, a violated pair reverts both fields, and one summary warning fires.

| Field | Default | Valid | Description |
|---|---|---|---|
| `Sides` | `["Free"]` | `Free`, `Neutral`, `Evil` (any case), one or more | Sides whose armies hold. An unknown, blank or numeric entry is dropped with a warning; a list left empty reverts to `["Free"]` (the MCM switch is the place to turn the feature off). |
| `Factions` | `[]` (shipped: `["empire_s"]`) | Kingdom StringIds, exact case | Realms whose armies hold whatever their side (#1283). An army holds when its side is in `Sides` or its realm is here. A blank entry is dropped with a warning; the rest are trimmed and kept once each. `WarTheaterConfigInvariantsTests` checks that every shipped id is a kingdom. |
| `MinCohesionToHold` | 45 | 0 to 100 | Cohesion needed when the hold would start. |
| `ReleaseCohesion` | 40 | 0 to 100, not above `MinCohesionToHold` | Cohesion below which a hold ends early. |
| `ReleaseFoodDays` | 1 | 0 to 30 | Days of food in the leanest party below which a hold ends early. |
| `BaseHours` | 22 | 1 to 100 | Hold hours for a lord with Valor equal to Calculating. |
| `PerTraitPointHours` | 5 | 0 to 24 | Hours added per level of Calculating above Valor (removed per level of Valor above Calculating). |
| `MinHours` | 12 | 11 to 32, not above `MaxHours` | Shortest hold. Above the engine's own 10. |
| `MaxHours` | 30 | 11 to 32 | Longest hold. The code never exceeds 32. |

Current values: with Valor and Calculating both 0 a lord holds 22 hours. The extremes land on the bounds: Calculating 2 with Valor -2 would give 42 (held at 30), Valor 2 with Calculating -2 would give 2 (held at 12).

## Co-op

Single-player only. `HoldTheWallsService` reads `ICoopSessionProvider.IsSessionActive`, `ShouldDeferToHost` and `IDedicatedServerProvider.IsDedicatedServer` live: any of them stands the feature down. A capture is not recorded while the gate is closed, and the first hourly pass after it closes releases every running hold and drops the unjudged captures. The behavior's row is in [coop-interop.md](coop-interop.md), "Gated (host-only)". The setting is simulation-relevant (it changes AI armies), so it is in the settings fingerprint.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/ArmyTargeting/HoldTheWalls/HoldTheWallsModule.cs` | Feature module: registers, builds the behavior per campaign |
| `Main/Features/ArmyTargeting/HoldTheWalls/Hooks/HoldTheWallsBehavior.cs` | Thin entry point: owner change, army dispersal, hourly, daily |
| `Main/Features/ArmyTargeting/HoldTheWalls/HoldTheWallsService.cs` | Captures, holds, early release, daily line |
| `Main/Features/ArmyTargeting/HoldTheWalls/HoldTheWallsMath.cs` | Pure rules: hold length, engine hours, release reason |
| `Main/Features/ArmyTargeting/HoldTheWalls/HoldEnd.cs` | How a hold ended |
| `Main/Features/ArmyTargeting/IArmyTargetingSettingsProvider.cs`, `ArmyTargetingSettingsProvider.cs` | The MCM switch, `HoldCapturedForts`, beside its siblings |
| `Main/Features/ArmyTargeting/CapturedFortHoldConfig.cs` | The config section |
| `Main/Features/ArmyTargeting/ArmyTargetingConfigProvider.cs` | Validates the section |
| `Main/Adapters/IFortHoldAdapter.cs`, `FortHoldAdapter.cs`, `FortArmyReading.cs` | Engine reads and the two AI-pause writes |
| `Main/_Module/ModuleData/configs/army_targeting.json` | `CapturedFortHold` values |

## Dependencies

- `IArmyTargetingConfigProvider` (ArmyTargeting): the validated section.
- `IArmyTargetingSettingsProvider` (ArmyTargeting): `HoldCapturedForts` and `EnableArmyStrategicIntelligence`.
- `IAlignmentService` (Execution): the side of the army's realm, resolved from a `SideIds` pair that `FactionSides` builds from the realm's id and its own culture.
- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop): the single-player gate.
- `IFortHoldAdapter` (Adapters): the engine.

## Tests

`TAOM.Tests/Features/ArmyTargeting/HoldTheWalls/`:

- `CapturedFortHoldConfigTests`: one test per validation rule, the pairs, the summary warning, and the shipped file loading through the real provider with no warning, a `Sides` list replacing the default rather than joining it, and a missing section loading the compiled defaults.
- `HoldTheWallsMathTests`: trait extremes, the clamps, the 32 hour cap, rounding up, NaN gates, `MayStart` sharing the release rules.
- `HoldTheWallsStartTests`: who holds (Free, Evil, Neutral by config, an Evil realm listed in `Factions` while an unlisted one does not), what is skipped, hours counted from the capture, an army whose AI is not paused, a throwing fortress read, two events for one fortress, two armies in one.
- `HoldTheWallsReleaseTests`: every early-release reason, the vanilla remainder, expiry dropped with no write, gone and moved armies released, the switch and the master switch, an unreadable clock, the co-op gates, a throwing read, a throwing write.
- `HoldTheWallsLifecycleTests`: the per-campaign reset, the idle pass, the daily line, the inactivity count.
- `HoldTheWallsSettingsTests`, `HoldTheWallsWiringTests`, `HoldTheWallsBehaviorTests`: the switch, its group and the shared provider's default, the module (listed once, built with `new`, no hand wiring), the handlers.
- `FortHoldAdapterIlTests`: the adapter holds no field and writes only `DisableForHours` and `EnableAi`.
- `SettingsFingerprintTests` and `ServerSettingsSchemaTests` carry the new setting's counts.

## How to Retune

1. Edit the `CapturedFortHold` section of `army_targeting.json`.
2. Restart Bannerlord (the file is read once per process).
3. To let Neutral realms hold too, add `"Neutral"` to `Sides`.
4. To let one more realm hold, add its kingdom id (not its lore name: `empire_s` is Mordor) to `Factions`.

## Debug Line

`[HoldTheWalls] day N: holds=, released=, cohesionAtCapture=, dispersedWhileHeld=, inactivityDisbands=, maxHourMs=` once per campaign day, at debug level. `holds` is the running count. `released` is `siege:a/cohesion:b/food:c/off:d/coop:e/player:f/expired:g/moved:h` for the day (`none` when all are zero); every reason but `expired` wrote to the engine. `cohesionAtCapture` lists the cohesion of each hold started that day, joined with `/`. `dispersedWhileHeld` counts holds whose army was gone. `inactivityDisbands` counts AI armies the engine disbanded for inactivity that day, counted with the switch on or off. `maxHourMs` is the slowest hourly pass of the day.

## Still owed

- **In-game check:** a Free army takes a fortress and stays about 22 hours, then leaves; an Evil army leaves after 10; a besieged held fortress releases its army; and the hours after the release show no rise in `inactivityDisbands` (the engine's `ArmyDispersed` with reason `Inactivity`) over a switch-off baseline.
- **Balance runs:** two fast-forward runs to day 200 from one save, with the switch on and off, compared with `tools/analyze_war_ledger.py --compare`, which prints net change per side, fief churn, capital losses and lead changes, plus `inactivityDisbands` summed from the daily line of both runs. A ledger capture event for retaken fortresses is a possible follow-up.
- **Budget check:** confirm the under 1 ms hourly pass from `maxHourMs` in a real campaign.

## Changelog

- 2026-11-15: A `Factions` list of kingdom ids lets one realm hold whatever its side (#1283). The shipped file lists Mordor (`empire_s`): a player reported that Mordor takes the forts and then stalls, and its armies marched off a captured fort after the engine's 10 hours. Isengard is not listed; the AI campaign on #1283 decides whether it should be.
- 2026-10-23: Hold the Walls added (#871). Review fixes: hold range 12 to 30 hours (base 22, code cap 32), gone and moved armies released, an `inactivityDisbands` counter, the switch folded into `IArmyTargetingSettingsProvider`.

## GitHub Issue

- **Issue:** #871, Hold the Walls: Good armies hold a fortress they just captured
- **Status:** Shipped on `bannerlord-1.5.x` in `1e04de50a` (v2.0.35). Issue open: the in-game check and the balance runs are owed.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/army-targeting.md](./army-targeting.md)
- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
