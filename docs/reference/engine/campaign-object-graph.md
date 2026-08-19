# Bannerlord campaign object graph — Hero / Clan / Kingdom / MobileParty / Settlement (Phase 16)

> **One process, traced from the decompile** (`TaleWorlds.CampaignSystem`, v1.4.5): the campaign-map simulation
> entities and how they reference each other. Phase 9 covered the *hooks* (`CampaignBehaviorBase` + `CampaignEvents`);
> this covers the *objects* those behaviors read + mutate. **Every** TAOM campaign feature (CultureConversion,
> SpecialResources, Messengers, Siege, CastleRecruitment, BanditManagement, …) navigates this graph. Part of the
> phased engine study; the campaign counterpart to the in-mission stack (Phases 1-4,12-15).

## WHAT it is

The campaign world is a graph of five core objects: a **`Hero`** (a character) belongs to a **`Clan`**, which may
belong to a **`Kingdom`** (both `Clan` and `Kingdom` are `IFaction`s). A hero leads a **`MobileParty`** (a dot on the
map), whose troops live in a **`PartyBase`**. A **`Settlement`** (town / castle / village) is owned by a `Clan`. All are
`MBObjectBase` (Phase 5 — `StringId` + `MBObjectManager`) except `MobileParty` (`CampaignObjectBase`), and all are
save-persisted (Phase 6).

## HOW it works — the graph

```
Kingdom (sealed : MBObjectBase, IFaction)
  ├─ RulingClan ─► Clan          Leader => RulingClan?.Leader     Clans / Fiefs / Towns / Settlements / FactionsAtWarWith / Culture
  │
Clan (sealed : MBObjectBase, IFaction)
  ├─ Kingdom ─► Kingdom          Leader ─► Hero                   Heroes / AliveLords / Companions / Settlements / HomeSettlement / Culture / Banner
  │
Hero (sealed : MBObjectBase, ITrackableCampaignObject)
  ├─ Clan ─► Clan                Culture (field)                  Father/Mother/Spouse/Children
  ├─ PartyBelongedTo ─► MobileParty
  └─ CurrentSettlement / StayingInSettlement / HomeSettlement / BornSettlement ─► Settlement
  │
MobileParty (sealed : CampaignObjectBase, IMapPoint)   [static MainParty]
  ├─ Party ─► PartyBase          LeaderHero / Owner ─► Hero       ActualClan ─► Clan   Army ─► Army
  ├─ CurrentSettlement / TargetSettlement / HomeSettlement ─► Settlement
  └─ MemberRoster => Party.MemberRoster ─► TroopRoster
  │
Settlement (sealed : MBObjectBase, IMapPoint, ITrackableCampaignObject, …)
  ├─ Town / Village (─► null when not that type)   IsTown / IsCastle / IsVillage
  ├─ OwnerClan ─► Clan  (resolves via Town.OwnerClan / Village.Bound.OwnerClan / Hideout)   Owner => OwnerClan.Leader
  ├─ Culture (field)    BoundVillages ─► Village[]    Party ─► PartyBase (garrison/siege)
  └─ MapFaction => SettlementComponent?.MapFaction

PartyBase (sealed : IBattleCombatant)  ── Settlement | MobileParty ──  MemberRoster / PrisonRoster ─► TroopRoster
```

**`MapFaction`** is the top-level allegiance, resolved up the graph: `Hero`→`Clan`→`Kingdom` (or an independent
`Clan`); `Settlement`→`OwnerClan`→`Kingdom`; `MobileParty`→`Owner`/`ActualClan`→`Kingdom`. `IFaction` is the shared
interface both `Clan` and `Kingdom` implement (`Kingdom.MapFaction => this`).

## WHY it's shaped this way

A single object graph (rather than tables) lets campaign logic navigate naturally — "this party's leader's clan's
kingdom's fiefs" is a property chain. `PartyBase` is the shared "has troops + can fight" abstraction so a settlement's
**garrison** and a **mobile party** use the same roster/battle code. The `IFaction` interface unifies clan-level and
kingdom-level allegiance so diplomacy/war code treats both uniformly. Everything being `MBObjectBase`/`CampaignObjectBase`
gives stable `StringId` lookup (Phase 5) + automatic save (Phase 6).

## TAOM relevance + gotchas (the recurring campaign-object rules)
- **ADR-007: never cross these sealed types into a service.** All five are `sealed`; services take `ICareerHeroAdapter` /
  `ISettlementOwnerAdapter` / etc., adapted at the boundary (the CLAUDE.md architecture one-liner). The campaign behavior
  (Phase 9) extracts primitives + builds the adapter; the service never sees `Hero`.
- **`?.` on computed/chained properties** (`.claude/rules/adapters.md`): `Settlement.OwnerClan` chains through
  `Town.OwnerClan` / `Village.Bound.OwnerClan` and can NRE *before* your null check — use
  `settlement.OwnerClan?.Culture?.StringId` (CultureMarketplace's exact idiom for dynamic owner culture).
- **`Settlement.Culture` and `Hero.Culture` are public FIELDS** (Settlement.cs:70, Hero.cs:117) — directly settable,
  but **`Settlement.Culture` is NOT engine-saved**: CultureConversion sets it on conquest, persists its own override in
  `CultureConversionStore`, and **re-applies it `OnGameLoadedEvent`** (Phase 9). Mutating it on load must follow the
  **entity-state matrix** (`.claude/rules/csharp-architecture.md` — destructive load-path ops need state guards).
- **A castle's `Settlement.Village` is `null`** (and a town's the same): widening a town/village gate to castles must
  audit the full downstream chain for `settlement.Village.X` derefs (CastleRecruitment used **castle-safe occupations
  only** because `RuralNotable` NREs on a castle's null `Village` — `feedback_widening_settlement_type_gate_audit`).
- **Lookup by id** (Phase 5): `MBObjectManager.Instance.GetObject<Settlement>(stringId)` / `Settlement.Find(id)` /
  `Hero.FindFirst(...)`; missing id → clean `null`. TAOM keys ~81 recruitment pools on hard-coded settlement ids.
- **Per-kingdom / per-culture mapping**: SpecialResources, BanditManagement, CulturalFeats map by
  `Kingdom`/`CultureObject.StringId` (see `kingdom-culture-mapping` memory + factions.json — `feedback_faction_map_update_with_cultural_feats`).
- **`Clan`/`Kingdom` cached lists** (`Settlements`/`Heroes`/`AliveLords`/`Clans`/`Fiefs`) are `MBReadOnlyList` caches —
  read-only; mutate membership via the campaign actions (`ChangeKingdomAction`, `ChangeOwnerOfSettlementAction`), not by
  editing the list.

## The native boundary
**None.** The campaign simulation is **entirely managed** (`Hero`/`Clan`/`Kingdom`/`MobileParty`/`Settlement` are all
C# `MBObjectBase`/`CampaignObjectBase`; their relationships, the daily/hourly tick, diplomacy, and economy are managed).
The only engine touch is the campaign **map scene** (a `MobileParty`'s position is a `CampaignVec2` on the managed
`MapScene`, which lazy-resolves via the native map mesh — `feedback_campaign_coupled_property_in_editor`: prefer raw
accessors in editor mode). Contrast the in-mission stack (Phases 1-15), which is heavily native-backed — the campaign
layer is where TAOM features live precisely because it's pure managed simulation.

## Evidence (file:line, v1.4.5)
- `Hero.cs`:26 (`sealed : MBObjectBase, ITrackableCampaignObject`), :117 (`Culture` field), :498 (`Clan`), :631 (`PartyBelongedTo`), :646/:704/:717/:741 (`StayingIn/Born/Home/CurrentSettlement`), :791/:807/:825 (`Father`/`Mother`/`Spouse`).
- `Clan.cs`:21 (`sealed : MBObjectBase, IFaction`), :107 (`Culture`), :143 (`Kingdom`), :176/:184/:180 (`Settlements`/`Heroes`/`AliveLords`), :277 (`Leader`), :377 (`HomeSettlement`).
- `Kingdom.cs`:24 (`sealed : MBObjectBase, IFaction`), :118 (`Culture`), :146/:148/:152 (`Fiefs`/`Towns`/`Settlements`), :168 (`Leader`), :205 (`Clans`), :207 (`RulingClan`), :262 (`MapFaction => this`), :142 (`FactionsAtWarWith`).
- `MobileParty.cs`:25 (`sealed : CampaignObjectBase, IMapPoint`), :255 (`static MainParty`), :353 (`Party`), :577/:741/:626 (`Current/Target/HomeSettlement`), :659 (`Army`), :775/:795 (`LeaderHero`/`Owner`), :942 (`ActualClan`), :1071 (`MemberRoster`), :1079 (`MapFaction`).
- `Settlement.cs`:26 (`sealed : MBObjectBase, IMapPoint, …`), :70 (`Culture` field), :83/:85 (`Town`/`Village`), :107 (`Owner`), :292 (`MapFaction`), :317 (`BoundVillages`), :369/:381/:405 (`IsTown`/`IsCastle`/`IsVillage`), :489 (`OwnerClan` via Town/Village.Bound/Hideout).
- `PartyBase.cs`:21 (`sealed : IBattleCombatant`), :119/:122 (`Settlement`/`MobileParty`), :129/:132 (`MemberRoster`/`PrisonRoster`).
- Linked: object-system-mbobjectmanager.md (Phase 5), save-system.md (Phase 6), campaignevents-and-campaignbehavior.md (Phase 9). Gotcha memories: `feedback_widening_settlement_type_gate_audit`, `feedback_faction_map_update_with_cultural_feats`, `feedback_campaign_coupled_property_in_editor`, `kingdom-culture-mapping`; rules `adapters.md`, `csharp-architecture.md` (entity-state matrix).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../../INDEX.md)
- [docs/reference/doc-lookup.md](../doc-lookup.md)
- [docs/reference/engine/settlement-economy-food-prosperity.md](./settlement-economy-food-prosperity.md)
- [docs/reference/party-template-sizing.md](../party-template-sizing.md)

<!-- backlinks-end -->
