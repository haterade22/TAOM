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
  recruits lost to a battle stay lost (partial delivery). Template caravan guards and mercenary
  escorts are never delivered even when they share a character id with a purchased recruit: the
  order persists a **non-cargo manifest** (`SupplyOrder.NonCargoTroops`, SaveableField 117,
  recorded at every spawn) and delivery subtracts it from the live roster before capping. The
  contract is deterministic and conservative: casualties bill against the cargo first, so the
  player is never handed a troop that might be a guard (Codex round 2 #6). Orders saved before
  the field existed deserialize it as null and keep the legacy live-cap behaviour. Deliveries
  also freeze while the player is a prisoner (captivity rides the same delivery-blocked input
  as an encounter).
- **Payments credit the source** (review round B): the goods and troops share of the quote goes
  to the settlement's coffers (`GiveGoldAction.ApplyForCharacterToSettlement`) or the lord
  (`ApplyBetweenCharacters`), matching vanilla purchases and keeping the #317 town ledger
  explainable; the earlier port destroyed the whole payment while real stock and soldiers left
  the source. Transport and guard fees ARE destroyed deliberately, like vanilla mercenary
  wages: they pay carriers who are not economy actors.
- **A destroyed caravan records its loss immediately**: the behavior listens on
  `MobilePartyDestroyed` and routes `SupplyCaravanComponent` parties to
  `ISupplyOrderService.OnCaravanDestroyed`, synchronously at destroy time, so an autosave
  between an AI battle and the next hourly tick can never serialize a stale InTransit row that
  a load would resurrect with its full cargo (Codex round 2 #7). Our own teardown paths flip
  the order status BEFORE destroying the party, so the synchronous event ignores them; the
  destroyed-path tracker cleanup (`ForgetDestroyed`) never re-destroys and never touches the
  companion, whose fate the destroying battle already decided.
- **Camp-placed orders are marked** (`SupplyOrder.PlacedFromCamp`, threaded
  `SupplyOrderScreens.Open(fromCamp) → game state → screen → VM → TryPlaceOrder`). Breaking a
  field camp calls `CancelCampOrders()`, which forfeits those and ONLY those; town-placed
  orders keep travelling. The blanket `CancelAll` was deleted in review round B: it had no
  production caller, and it existed only as a footgun that looked symmetrical to
  `CancelCampOrders` while destroying town-placed orders the player paid for.
- **The dispatch origin is persisted on the order** (SaveableFields 114-116, set at first
  spawn): route building and respawn anchor there, never at the source lord's current position,
  and a lord-sourced caravan can now respawn after a load even when the lord lost his party.
- **Small round-B fidelity/behaviour fixes** (2026-08-23): the caravan's map banner is the
  player's MAP-FACTION banner again (source parity; the port briefly used the clan banner),
  falling back to the clan banner for a factionless player. Lord-sourced orders confirm with
  the source's named "{LORD} sends reinforcements" line instead of the generic caravan message
  (`SupplyOrderService.DispatchMessageTemplate` pins the branch). A source row whose distance
  resolves to the `float.MaxValue` unreachable sentinel (or NaN) is now disabled with the same
  no-route reason the confirm path uses and shows "?" for distance, instead of quoting
  near-zero transport for an order the service would reject. The per-frame bearing write shares
  the position write's `MinPositionDelta` change gate instead of running per caravan per frame.
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
| Order counter trusted from the save alone | `LoadFrom` derives the counter floor from the loaded `taom_so_N` ids, so a recovered save missing the counter key cannot mint an id over a live order |
| Post-load rebind trusted the party StringId alone | `RespawnMissing` binds a surviving party only when its component is `SupplyCaravanComponent` with the matching order id; anything else is logged and a fresh caravan is spawned (a hostile row could otherwise hand `main_party` to the teleport pass and to `DestroyPartyAction`) |

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
- **The force-deliver failsafe fires at 1.5x planned time** (`SupplyOrderEngine.ForceDeliverFraction`),
  the source's route-tick threshold for routed caravans. The first port pass shipped the hourly
  tick's 2.0 with a comment claiming 2.0 governed routed caravans; review round B showed that
  claim was inverted (2.0 was only the no-route backstop, and every caravan this port spawns is
  routed).

## Food

The caravan party is NOT `IsCaravan` (custom component), so vanilla's
`DefaultMobilePartyFoodConsumptionModel.DoesPartyConsumeFood` returns true for it (verified on
the installed 1.4.8: the exclusion list is IsGarrison/IsCaravan/IsBandit/IsMilitia/IsPatrolParty
plus IsVillager) and `FoodConsumptionBehavior` eats from its `ItemRoster` daily. Before round B
an escorted goods-less order starved silently for the whole transit: no message (the starving
notifications are `IsMainParty`-gated), no resupply (the caravan has no `LeaderHero`, so
`PartiesBuyFoodCampaignBehavior` skips it). `Spawn` now stocks provisions:
`ComputeProvisionCount` loads one food per 20 men per day (vanilla
`NumberOfMenOnMapToEatOneFood`) for the worst-case 1.5x-planned transit, plus one spare, as
"grain" (an engine DEFAULT item created in code by `DefaultItems.RegisterAll`, so it exists in
every campaign). Provisions are goods the order never listed, so delivery's cap-by-ordered
never hands them over; ordered food that transit partially ate can still arrive whole when the
provisions covered the consumption, which only ever favours the player.

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
| `Main/_Module/GUI/PreFabs/SupplyLines/TaomSupplyOrderScreen.xml` | Ported prefab, `{=taom_sl_*}` texts. Text brushes are `Popup.Description.Text` / `Popup.Button.Text` (Native/GUI/Brushes/Popup.xml, grep-verified on the installed 1.4.8) with per-site `Brush.FontSize`; the port originally shipped `Popup.Text.Medium`/`.Small`, which exist in NO brush file anywhere and silently rendered 22 widgets with the engine default brush (round B critic) |

Tests: pricing, engine verdicts (incl. the 1.5x force-deliver pin), the order book (reset,
cancel-camp, live-cargo, counter derivation, destroy-event loss recording,
status-before-destroy ordering, dispatch-message branch), cargo/provision maths
(`SupplyCaravanCargoMathTests`), order POCO incl. dispatch origin, behavior session-reset
contract, VM matrix (incl. the unreachable-sentinel row states), prefab-binding round-trip
(forward + reverse dead-binding, sprite/brush allowlist), engine bindings (Bearing setter,
VolunteerModel gate, wage model, CreateParty overload) plus the Patch73 target/category pins,
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
- 12-language TRANSLATION run for the `{=taom_sl_*}` keys (backlog issue #508). Registration and
  English rows are done: every key including `taom_sl_caravan_meet` and `taom_sl_lord_dispatched`
  is registered in `taom_module_strings.xml` and present in all 12 language files as English
  fallback (batch-2 integration).
- Optional fidelity restore: the `sl_reinf_*` lord conversation (see Deliberate departures).

## Prefab labels are VM properties (field-tested 2026-08-25)

The six button labels on the order screen (escort None/Mercenaries/Companion, Confirm, Clear,
Cancel) shipped as literal `Text="{=key}Label"` attributes and rendered the raw token in-game:
Gauntlet does not localize literal prefab text, only VM-bound strings pass through TextObject.
They are now `@EscortNoneText` etc., built in the SupplyOrderScreenVM constructor, and
`SupplyOrderPrefabBindingTests.NoPrefabAnywhere_CarriesALiteralLocalizationToken` sweeps every
prefab in the module so the class cannot ship again.
