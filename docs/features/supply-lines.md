# Supply Lines

Order resupply convoys from the field (#505). Pick a town, castle or friendly lord, choose goods
from the source's real market stock and troops from its volunteers, pick an escort, and a caravan
crosses the map to your party. Port of the commissioned yotthani SupplyLines module (Bannerlord
1.4.5, decompiled and behaviourally ported 2026-08-22); provenance:
[provenance-register.md](../reference/provenance-register.md) "Yotthani commissioned modules".

## Problem

TAOM campaigns range far from friendly territory and the map is big: restocking food or replacing
losses means marching back. Vanilla has no way to have supplies come to you.

## Architecture

```
town/town_keep menu option ─▶ SupplyOrderScreens.Open()
                                  │  GameStateManager.PushState
                                  ▼
SupplyOrderGameState ── [GameStateScreen] attribute ──▶ GauntletSupplyOrderScreen
                                  │ LoadMovie("TaomSupplyOrderScreen")
                                  ▼
SupplyOrderScreenVM ◀── ISupplySourceService (sources, goods, troops)
        │                ISupplyPricingService (quote)
        │ Confirm
        ▼
ISupplyOrderService.TryPlaceOrder
        │ consume ─▶ spawn ─▶ charge (in that order; failure refunds, never charges)
        ▼
SupplyOrder (book, SyncData "_taomSupplyOrders")
        │ hourly: ISupplyOrderEngine verdicts (Continue / Deliver / Lose)
        │ frame:  ISupplyCaravanService.TickPositions + proximity delivery
        ▼
delivery: LIVE caravan cargo (capped by the order) → ItemRoster/MemberRoster;
          escort released BEFORE party destroyed
```

The screen rides the `[GameStateScreen(typeof(SupplyOrderGameState))]` attribute path
(GauntletCareerScreen precedent), so there is **no patch on `GameStateScreenManager.CreateScreen`**;
the source module had one, which would have collided with TAOM's Patch36 prefix.

## Key decisions

- **Caravan movement keeps the source's teleport-along-path model** (user decision 2026-08-22): AI
  disabled at spawn, position set along a cached nav path as a function of elapsed/planned travel
  time. Hardened: party lookups cached (no per-frame LINQ over `MobileParty.All`), no-change frames
  skip the native set, and the internal `MobileParty.Bearing` setter is reached through one cached
  `AccessTools.PropertySetter`, pinned by `SupplyCaravanBearingBindingTests` so an engine rename
  fails in CI instead of silently sliding caravans sideways.
- **Volunteers respect TAOM's recruitment stack**: sourcing goes through
  `Campaign.Current.Models.VolunteerModel.MaximumIndexHeroCanRecruitFromHero` and honours the
  alignment gate's `-1` (recruit nothing from this notable). Recruit prices run through the vanilla
  wage model with the source's tier premium (`x (1 + 0.15*(tier-1))`) and wartime surcharge (x1.5).
- **Fresh save surface**: definer base `726901001` (localIds from 101), SyncData keys
  `_taomSupplyOrders` / `_taomSupplyOrderCounter`. The source module's save identity
  (841200501, other type names) is deliberately not carried.
- **Session reset contract**: the services are process singletons and SyncData fires only when a
  saved record exists, so the behavior tracks `_syncedThisSession` (set only on a LOADING
  SyncData) and OnSessionLaunched calls `ResetForNewSession()` when it is false. That covers
  fresh campaigns AND saves without the record; without it, campaign A's orders ride into and
  get SAVED into campaign B. `LoadFrom` additionally drops the caravan trackers (a tracker's
  cached `MobileParty` belongs to the previous session; a loaded campaign has new objects under
  the same ids) and scrubs null/id-less rows from hostile save shapes, then `OnGameLoaded`
  rebinds trackers from the live campaign's party list.
- **A fighting caravan belongs to the engine**: the verdict input is `caravanInMapEvent` (ANY
  map event → Continue). Delivering would destroy a party still attached to a MapEvent side
  (engine detach-before-destroy contract); a defeat destroys the party and resolves as a loss
  through `CaravanExists`. The original `IsRaid` input could never fire for a field battle
  (`IsRaid` is the settlement-raid battle type only) and was retired.
- **Delivery hands over the LIVE cargo**, capped by what was ordered: goods eaten in transit and
  recruits lost to a battle stay lost (partial delivery), and template caravan guards or
  mercenary escorts are never delivered because they were never in the order. Deliveries also
  freeze while the player is a prisoner (captivity rides the same delivery-blocked input as an
  encounter).
- **Camp-placed orders are marked** (`SupplyOrder.PlacedFromCamp`, threaded
  `SupplyOrderScreens.Open(fromCamp) → game state → screen → VM → TryPlaceOrder`). Breaking a
  field camp calls `CancelCampOrders()`, which forfeits those and ONLY those; town-placed
  orders keep travelling. `CancelAll` remains for genuine cancel-everything paths.
- **The dispatch origin is persisted on the order** (SaveableFields 114-116, set at first
  spawn): route building and respawn anchor there, never at the source lord's current position,
  and a lord-sourced caravan can now respawn after a load even when the lord lost his party.
- **Clicking the caravan never opens vanilla's meeting**: `SupplyCaravanEncounterPatch`
  (category `Patch73_SupplyLines`, prefix on `PlayerEncounter.DoMeeting`) finishes the
  encounter with a one-line notice. The component's `Leader` is null, so vanilla would strike a
  stranger conversation with the highest-tier roster troop, or a null partner on an empty
  roster.

## Source defects fixed in the port (not carried)

| Source behaviour | Port behaviour |
|---|---|
| Settlement stock read for the UI, cargo conjured on confirm (dupe) | `Consume` deducts from the settlement `ItemRoster`, prices what it actually took |
| Charged before spawn; spawn exception kept the money | Consume → spawn → charge; failure refunds consumption, charges nothing |
| Caravan destroyed BEFORE releasing the companion escort (stranded hero) | `ReleaseEscortAndDestroy`: release first, on every path, verified by call-order test |
| 2x-timeout delivery fired mid-battle/siege | Every delivery verdict is gated on the player not being in an encounter |
| Whole handcart subsystem dead (flag never set) + dead settings | Not ported; `CaravanHoursPerDistance` is the honest name for the speed constant the source borrowed from the dead branch |
| Unused order states (Arrived/PartiallyDelivered/Cancelled) | Enum has only states that occur |

## Deliberate departures from the source

- **The lord reinforcement conversation (`sl_reinf_*`) is not ported.** The source added a
  "Could you spare some of your men to reinforce me?" option on every same-faction lord
  (three priced package sizes) that created a hero-sourced order. The port reaches the same
  outcome through the supply screen's lord sources (friendly lords within 80 map units appear
  in the source list), so the conversation was cut as a second entry point to one mechanic.
  Restoring it maps cleanly onto `TryPlaceOrder` with a lord `SupplySourceInfo` if it is ever
  wanted; tracked under Owed / follow-ups.
- **Villages are eligible sources again** (restored after review round A flagged the silent
  cut): the source offered towns, villages and castles, and the port's town/castle-only filter
  was an undocumented departure. A village prices goods at item base value (no `Town`
  component) and its volunteers flow through the same notable-slot walk.
- **The ambush/battle mechanics of the sibling FieldCamp feature are out of scope here**; this
  feature's only camp coupling is the `PlacedFromCamp` marker and `CancelCampOrders`.

## Configuration

MCM group **Supply Lines** (GroupOrder 45): `EnableSupplyLines`, `SupplyGoodsMarkupFactor` (1.05),
`SupplyTransportFeePerDistance` (2), `SupplyMercenaryWagePerDistance` (10),
`SupplyMercenaryGuardCount` (10), `SupplyCaravanHoursPerDistance` (2), `SupplyShowRouteVisual`.
All validated in `SupplyLinesSettingsProvider` (finite + range, fallback to defaults). All but the
route visual are coop simulation-relevant (`CoopSettingsRelevance`); the route visual is
presentation. Toggling the feature off stops NEW orders and menu options; in-transit orders still
complete so cargo is never stranded by a toggle.

## Key files

| File | Purpose |
|---|---|
| `Main/Features/SupplyLines/Domain/SupplyOrder.cs` | Persisted order POCO; travel progress derived from timestamps, never stored |
| `Domain/SupplyLinesSaveDefiner.cs` | Base 726901001; order, caravan component, enums, container |
| `SupplyPricingService.cs` | Pure quote/troop-price/planned-hours maths, positive-requirement NaN gates |
| `SupplyOrderEngine.cs` | Pure hourly verdicts (Continue/Deliver/Lose), encounter gating, loss precedence |
| `SupplySourceService.cs` | Source eligibility, goods/troops enumeration, deducting consumption, alignment gate |
| `SupplyCaravanService.cs` | Spawn/teardown/respawn + the hardened teleport movement; AI re-pin on load |
| `SupplyOrderService.cs` | The order book: place/advance/deliver/lose/cancel; campaign statics behind protected virtuals |
| `SupplyRouteVisualService.cs` | Throttled map arrow trail (0.25h resample, retint on change, 40-arrow cap) |
| `Components/SupplyCaravanComponent.cs` | First custom PartyComponent in TAOM; identity only |
| `Hooks/SupplyLinesCampaignBehavior.cs` | Events, SyncData halves, session reset gate, town/town_keep menu options |
| `Hooks/SupplyCaravanEncounterPatch.cs` | Patch73_SupplyLines: DoMeeting guard, caravan click-through suppressed |
| `UI/GauntletSupplyOrderScreen.cs` + `SupplyOrderScreenVM.cs` + row VMs | The order screen (attribute path, focus layer, latched teardown) |
| `Main/_Module/GUI/PreFabs/SupplyLines/TaomSupplyOrderScreen.xml` | Ported prefab, vanilla brushes only, `{=taom_sl_*}` texts |

Tests: pricing (36), engine verdicts (23), order book incl. reset/cancel-camp/live-cargo (37),
order POCO incl. dispatch origin (12), behavior session-reset contract (7), VM matrix (25),
prefab-binding round-trip (forward + reverse dead-binding), engine bindings (Bearing setter,
VolunteerModel gate, wage model, CreateParty overload) plus the Patch73 target/category pins (3),
plus the shipped-config and localization-key sweeps that gate every feature.

## Traps

- The caravan party is **teleported**; anything else that moves that party fights the service.
  While the caravan is in a MapEvent its position is left alone.
- `MobileParty.CreateParty` may uniquify the requested StringId: the order records
  `party.StringId`, never the requested string.
- Every order records its dispatch origin at first spawn (SaveableFields 114-116) and
  respawns there, lord-sourced orders included. Only a legacy order with no recorded origin
  (placed before the fields existed) still resolves as Lost when its lord-sourced caravan is
  missing after a load.
- Escort companion policy: lord sources force Escort None; a companion escort with no living
  companion downgrades to None rather than sailing a null hero. On respawn the escort is
  re-attached ONLY when he is free (alive, not a prisoner, not with another party, not staying
  in a settlement); otherwise the caravan continues unescorted and the hero stays where fate
  put him.
- `Patch73_SupplyLines` must be in the Harmony category registration list in `SubModule`
  (single-owner file; registered by the orchestrator) or the DoMeeting guard silently never
  patches.

## Owed / follow-ups

- In-game smoke (#505 checklist): order from town, village + lord, delivery after caravan
  losses (partial), cancel, camp-placed order + camp break, save/load mid-transit, click the
  caravan (no stranger conversation).
- 12-language translation run for the `{=taom_sl_*}` keys, including the new
  `taom_sl_caravan_meet` (English fallbacks registered; run scheduled with the
  FieldCamp/Refuge strings).
- Optional fidelity restore: the `sl_reinf_*` lord conversation (see Deliberate departures).
