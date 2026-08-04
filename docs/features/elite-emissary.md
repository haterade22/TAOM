# Elite Emissary (Settlement Special-Resource Troop Merchant)

## Overview

At a faction's key settlement (its capital), the player can "Speak with the faction emissary" from the
town/castle/village menu, have a short conversation, and buy that faction's **elite troops** for that
faction's **special resource** (Castar, War Spoils, Gems, Elven Wine, Marks, War Banners, War Drums).
A two-step popup picks the troop then the quantity; the resource is deducted and the troops join the
player's party.

Inspired by ROT's (`E:\ROT6.2`) `ROTTownTradersBehavior`, which sells troops at specific settlements for
**gold**. TAOM rebuilds the same experience on the existing [SpecialResources](special-resources.md)
economy instead of gold.

## Why This Exists

TAOM has a rich special-resource economy (11 resources earned by battle/raid/siege/etc.) but, before
this feature, resources were only spent on troop **upgrades** (Patch26) and a couple of volunteer
**recruits** (elephant/spider). There was no way to directly buy a faction's elite units. The Elite
Emissary gives the player a resource sink and a reason to hold a capital: it turns "war currency" into
"the faction's best troops."

Self-gating falls out of the design: the resource charged is the **settlement owner faction's** resource,
and the player only earns their own faction's resource — so in practice you buy your own faction's elites
at your own capitals. Conquering an enemy capital flips its offerings to the new owner.

## Architecture

### Design Challenge

- Reuse the SpecialResources economy (resolution / balance / charge) without coupling the new feature's
  price to the volunteer-recruit charging (an elite that is also a volunteer must not be double-charged).
- Offer the **current owner faction's** troops + resource, re-resolved each menu open so conquest flips
  the offerings with no extra wiring.
- Open a conversation from a settlement menu robustly, without the fragile `PlayerEncounter` machinery.

### Solution Approach

- **Separate `merchant_cost` field** on the existing `troop_resource_costs.xml` rows (parsed into
  `TroopResourceCostEntry.MerchantCost`). `recruit_cost` stays the volunteer-gate price; `merchant_cost`
  is the emissary price. Two new thin methods on `ISpecialResourceService`
  (`CanAffordMerchantPurchase` / `ChargeMerchantPurchase`) keep all resource-storage mutation inside
  SpecialResources; the volunteer gate is never touched.
- **Dynamic owner resolution** via `ISettlementOwnerAdapter` — `Settlement.OwnerClan?.Culture/Kingdom`
  (the engine's `OwnerClan` already hops village→bound-town). The owner culture selects the offer list;
  the owner kingdom+culture resolves the resource that is charged and whose balance is read.
- **Conversation** via `CampaignMapConversation.OpenConversation(player, notable)` with a custom dialog
  token, flag-gated (`_pendingEmissaryHeroId`) so the emissary greeting only fires for the conversation
  the menu launched — never hijacks a normal notable chat. If a settlement has no living notable, the
  purchase list opens directly (logged).
- **Transaction order = authority → afford → grant → charge.** The authority check comes first so a
  non-authoritative co-op peer is never charged at all (see Design Decisions). Granting before charging
  means a failed grant (no party, unknown troop id) never charges — no refund path needed (a single
  integer roster add can't partially apply).

### Component Diagram

```
[town/castle/village menu]  "Speak with the faction emissary"  (EliteEmissaryBehavior)
        │ condition: IsKeySettlement + HasPurchasableOffers (owner faction)
        ▼
  CampaignMapConversation.OpenConversation(player, settlement notable)
        │ dialog: greet → "I wish to purchase elite units."
        ▼
  EliteEmissaryInquiryPresenter.OpenTroopList(settlement)         ← boundary (engine UI)
        │  ISettlementOwnerAdapter.GetOwnerInfo → (kingdom, culture)
        │  IEliteEmissaryService.BuildOfferList(hero, kingdom, culture)
        ▼  ShowMultiSelectionInquiry (troop → quantity, afford-gray)
  EliteEmissaryInquiryPresenter.ExecutePurchase                       ← refuses if ShouldDeferToHost
        ▼
  IEliteEmissaryService.Purchase(hero, kingdom, culture, troop, qty)   ← pure service
        │  afford → IPlayerPartyAdapter.GrantTroop → ISpecialResourceService.ChargeMerchantPurchase
        ▼
  EmissaryPurchaseResult → player message
```

## Design Decisions & Known Edge Cases

All of these are intentional, not bugs. The first five came out of the 2026-06-25 deep review; the
co-op refusal was added 2026-08-03 from field testing.

- **Resource resolution is kingdom-first; offers are culture-keyed.** `BuildOfferList`/`Purchase` select
  offers by the owner **culture** but resolve the charged resource via the standard
  `ResolveResource(kingdom, culture)` (kingdom-first). This is consistent with the *earning* side — a
  settlement's owner faction earns the resource resolved from its kingdom. The only divergence is the rare
  dynamic state where a clan's culture and kingdom map to *different* resources (e.g. a Gondor-culture clan
  that defected into the Mordor kingdom and holds a key settlement): it would offer Gondor elites priced in
  the kingdom's resource (War Spoils). This never triggers in the shipping config (all capitals start
  culture==kingdom-aligned), is internally consistent (list, picker, and charge all use the resolved
  resource), and is not a money exploit — only a thematic mismatch in an edge state. Left as-is.
- **Disguise/war access gate** (`EliteEmissaryBehavior.IsEmissaryAccessBlocked`). My original assumption —
  "hostile settlements aren't enterable, so no war-gate is needed" — was **wrong**: Codex review (2026-06-25)
  found via decompile that vanilla **disguise** entry (`LimitedAccessSolution.Disguise`) lets an at-war
  player sneak into a hostile capital and reach the normal `town` menu. So `MenuCondition` and `BuyCondition`
  now block the emissary when the player is **disguised** (`Campaign.Current.IsMainHeroDisguised`) or **at war
  with the settlement owner** (`settlement.MapFaction.IsAtWarWith(...)`) — mirroring how vanilla
  `CanMainHeroTrade` gates disguised trade. You can't approach a faction's emissary while sneaking in as an
  enemy. Affordability still self-gates the normal case (you only hold a faction's resource if you fight for it).
- **Purchases are not party-size-capped.** `GrantTroop` adds the bought troops regardless of
  `PartySizeLimit` (like quest/ransom troop rewards). Buying over the cap is allowed; the player chose to.
- **Creature troops** (`taom_spider_creature`, `harad_elephant_rider`) were already player-recruitable as
  volunteers before this feature, so selling them via the emissary reaches no new command path — their
  native-crash mitigations already cover player-party instances. (Smoke-test that a bought spider mounts
  correctly, since the emissary is a new ownership entry point.)
- **Greeting-flag lifecycle is hardened.** `_pendingEmissaryHeroId` (which gates the emissary greeting so it
  only fires for the menu-launched conversation, never a normal notable chat) is cleared in both
  `GreetConsequence` AND on `CampaignEvents.ConversationEnded`, so it can never leak into a later
  conversation even if a higher-priority vanilla `start` line wins the emissary conversation.
- **A co-op guest cannot buy** (2026-08-03). Where `ICoopSessionProvider.ShouldDeferToHost` is true,
  `ExecutePurchase` refuses **before any charge** and prints
  `{=taom_emissary_coop_guest}` — *"The emissary only deals with the host of this campaign."* Charging
  there would stick, because TAOM's own `SyncData` carries the resource balance, while the purchased
  troops land in a client-side roster that the next resync overwrites; field testing confirmed the
  pay-real-get-phantom outcome. Granting them authoritatively instead needs a message TAOM cannot send
  without a compile-time dependency on one specific co-op mod, so declining is the chosen behaviour,
  not a placeholder. **The refusal is at the last step, not at the menu:** `MenuCondition` and
  `BuyCondition` are not co-op-gated, so a guest still sees "Speak with the faction emissary", has the
  conversation, browses the offers and picks a quantity, and is turned away only on confirm — unlike
  the disguise/war gate above, which hides the option outright.

## Configuration

### Config File: `Main/_Module/ModuleData/elite_emissary/elite_emissary_config.xml`

- `<KeySettlements>` — settlement StringIds where the emissary appears (verified against
  `TAOM_Map/settlements.xml`).
- `<CultureOffers>` — keyed by the settlement's **owner culture** StringId; each lists ordered troop ids.
  An offer troop with no `merchant_cost` row, or a culture id not in the known set, is dropped + warned at
  load. Key-settlement ids are validated against live settlements at session launch (warning per missing id).

### Config File: `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml`

- `merchant_cost="N"` per `<Troop>` — the one-time emissary price (in the owner faction's resource).
  Cost band: L36 ≈ 10–14, L41 ≈ 18, L46 ≈ 28, L51 ≈ 45, creatures ≈ premium.

### Current Values

11 cultures with offers (owner culture → capital → resource):

| Culture | Capital (StringId) | Resource | Example elites |
|---|---|---|---|
| gondor | Minas Tirith (`town_EW1`) | Castar | ithilien_ranger, fountain_guard, swan_knight |
| mordor | Barad Dûr (`town_ES1`) | War Spoils | uruk_captain, baraddurguard, spider |
| erebor | Erebor (`town_E1`) | Gems | royal_warden, royal_legionary, ironbreaker |
| dolguldur | Dol Guldur (`town_DG1`) | War Spoils | khamul_shadow_knight/reaper/bowman |
| isengard | Orthanc (`town_isengard`) | War Spoils | orthanc_bodyguard, nazg_hai, warden |
| gundabad | Mount Gundabad (`town_G1`) | War Spoils | dread_rider, bolgs_ironfang, berserker |
| mirkwood | Felegoth (`town_M1`) | Elven Wine | palaceguard, thingolheir, beleglas |
| rivendell | Rivendell (`town_R1`) | Elven Wine | high_captain, knight_golden_flower, glorfindel_guard |
| vlandia (Rohan) | Edoras (`town_V1`) | Marks | golden_hall_supreme_rider, kings_own_* |
| khuzait (Rhun) | Mistrand (`town_RU1`) | War Banners | dragon_wrath_obsidian_*, warlord_chariot |
| aserai (Harad) | Korb Taskral (`town_A1`) | War Drums | elephant_rider |

Omitted (no L36+ elites): Dale (sturgia), Dunland (empire), Umbar, Khand (battania), Lothlorien.
Omitted (no special-resource mapping): goblin, mistymountainorcs.

### MCM — group "Elite Emissary"

- `EnableEliteEmissary` (master, default on).
- `HideEmissaryWhenNoResource` (default on) — hide the option at settlements whose owner faction has no
  special resource; off = show it disabled with a hint.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/EliteEmissary/EliteEmissaryService.cs` | Pure logic: offer-list build, afford, transaction |
| `Main/Features/EliteEmissary/EliteEmissaryConfigProvider.cs` | Loads + validates the config XML |
| `Main/Features/EliteEmissary/EliteEmissarySettingsProvider.cs` | MCM-over-config-default |
| `Main/Features/EliteEmissary/Domain/*` | Offer/result records, `EliteEmissaryConfig` |
| `Main/Features/EliteEmissary/Hooks/EliteEmissaryBehavior.cs` | Menu options + dialog wiring + key-settlement validation |
| `Main/Features/EliteEmissary/Hooks/EliteEmissaryInquiryPresenter.cs` | The two-step purchase inquiry (boundary) + the non-authority refusal |
| `Main/Adapters/SettlementOwnerAdapter.cs` | Settlement → owner kingdom/culture |
| `Main/Adapters/PlayerPartyAdapter.cs` | Grant troops to the main party roster |
| `Main/Features/SpecialResources/...` | `MerchantCost` field + `*MerchantPurchase` methods |
| `Main/_Module/ModuleData/elite_emissary/elite_emissary_config.xml` | Key settlements + culture offers |
| `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml` | `merchant_cost` prices |
| `Main/_Module/ModuleData/taom_emissary_strings.xml` | Player-facing strings (12-lang registered — `taom_emissary_coop_guest` is **not** in here yet) |

## Dependencies

- [SpecialResources](special-resources.md) — resolution, balance, storage, the price table.
- `ICoopSessionProvider` (CoopInterop) — the authority check on purchase. Taken by both
  `EliteEmissaryBehavior` and `EliteEmissaryInquiryPresenter` (the behavior only forwards it to the
  presenter it constructs).
- MCM (`TaomSettings`), `IPathService`, `IModLogger`.
- No Harmony patch, no GameModel override, no SyncData.

## Tests

- `TAOM.Tests/Features/EliteEmissary/EliteEmissaryServiceTests.cs` — offer build, afford-gray, owner
  resolution, the full Purchase decision tree (Invalid / NoResource / NotOffered / Unaffordable / Success /
  grant-fail), grant-before-charge ordering.
- `TAOM.Tests/Features/EliteEmissary/EliteEmissaryConfigProviderTests.cs` — valid/missing/malformed XML,
  unknown-culture drop, unpriced-troop drop, enabled flag.
- `TAOM.Tests/Features/SpecialResources/SpecialResourceServiceTests.cs` — the two `*MerchantPurchase`
  methods (afford boundary, charge amount, no-merchant-cost / no-resource / zero-count no-ops).

**Not covered:** the co-op refusal. It lives in `EliteEmissaryInquiryPresenter`, which is engine-coupled
boundary code and not unit-testable per ADR-008 — verify it in a live two-client session.

## How to Add a Faction or Troop

1. Add the troop id with a `merchant_cost` to `troop_resource_costs.xml` (and confirm the id is a real
   `CharacterObject`).
2. Add it under the owner culture's `<Culture>` block in `elite_emissary_config.xml` (create the block if
   the culture is new; the culture must map to a special resource in `special_resources_config.xml`).
3. To add a new key settlement, add its StringId to `<KeySettlements>` (verify against
   `TAOM_Map/settlements.xml`).
4. New player-facing strings → `{=KEY}` in `taom_emissary_strings.xml`, then run `/localize`.

**Outstanding:** `{=taom_emissary_coop_guest}` skipped step 4. It exists only as the fallback literal in
`EliteEmissaryInquiryPresenter.cs`, is absent from `taom_emissary_strings.xml`, and has no translations.
Register it there first, then run `/localize`.

## Performance

The menu condition runs per menu open (not per frame): a HashSet membership check + one owner resolution +
an offer scan. The config is `Reuse.Singleton`, loaded once per process (edits need an app restart).

## Changelog

- 2026-08-03 — Purchases are declined on a non-authoritative co-op peer before the resource is charged, with a new `{=taom_emissary_coop_guest}` message (not yet registered in `taom_emissary_strings.xml`, not yet translated). `EliteEmissaryBehavior` and `EliteEmissaryInquiryPresenter` both gained an `ICoopSessionProvider` parameter.
- 2026-06-25 — Feature created. 11 cultures authored, L36+ elites, verified capitals.

## GitHub Issue

TBD (create on ship).
