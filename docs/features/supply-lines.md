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
delivery: goods → ItemRoster, recruits → MemberRoster; escort released BEFORE party destroyed
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

## Source defects fixed in the port (not carried)

| Source behaviour | Port behaviour |
|---|---|
| Settlement stock read for the UI, cargo conjured on confirm (dupe) | `Consume` deducts from the settlement `ItemRoster`, prices what it actually took |
| Charged before spawn; spawn exception kept the money | Consume → spawn → charge; failure refunds consumption, charges nothing |
| Caravan destroyed BEFORE releasing the companion escort (stranded hero) | `ReleaseEscortAndDestroy`: release first, on every path, verified by call-order test |
| 2x-timeout delivery fired mid-battle/siege | Every delivery verdict is gated on the player not being in an encounter |
| Whole handcart subsystem dead (flag never set) + dead settings | Not ported; `CaravanHoursPerDistance` is the honest name for the speed constant the source borrowed from the dead branch |
| Unused order states (Arrived/PartiallyDelivered/Cancelled) | Enum has only states that occur |

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
| `Hooks/SupplyLinesCampaignBehavior.cs` | Events, SyncData halves, town/town_keep menu options |
| `UI/GauntletSupplyOrderScreen.cs` + `SupplyOrderScreenVM.cs` + row VMs | The order screen (attribute path, focus layer, latched teardown) |
| `Main/_Module/GUI/PreFabs/SupplyLines/TaomSupplyOrderScreen.xml` | Ported prefab, vanilla brushes only, `{=taom_sl_*}` texts |

Tests: pricing (36), engine verdicts (23), order book sequencing with `Received.InOrder` (19),
VM matrix (21), prefab-binding round-trip (forward + reverse dead-binding), engine bindings
(Bearing setter, VolunteerModel gate, wage model, CreateParty overload), plus the shipped-config
and localization-key sweeps that gate every feature.

## Traps

- The caravan party is **teleported**; anything else that moves that party fights the service.
  While the caravan is in a MapEvent its position is left alone.
- `MobileParty.CreateParty` may uniquify the requested StringId: the order records
  `party.StringId`, never the requested string.
- A lord-sourced order whose caravan dies cannot respawn across a save load (no settlement to
  respawn at); it resolves as Lost. Settlement orders respawn with travel time preserved.
- Escort companion policy: lord sources force Escort None; a companion escort with no living
  companion downgrades to None rather than sailing a null hero.

## Owed / follow-ups

- In-game smoke (#505 checklist): order from town + lord, delivery, cancel, save/load mid-transit.
- 12-language translation run for the `{=taom_sl_*}` keys (English fallbacks registered; run
  scheduled with the FieldCamp/Refuge strings).
