---
paths:
  - "Main/Adapters/**"
  - "Main/**/I*Adapter.cs"
  - "Main/**/*Adapter.cs"
---

# Adapter Pattern Rules (ADR-007)

## Core Principle
Services NEVER accept sealed TaleWorlds types directly. Always wrap with adapter interfaces.

## Creating New Adapters
1. **Research first** — Decompile the TaleWorlds class with `ilspycmd` before creating the adapter interface
2. **Interface in `Main/Adapters/`** — `I{TypeName}Adapter.cs` with only the properties/methods the feature needs
3. **Implementation in `Main/Adapters/`** — `{TypeName}Adapter.cs` wrapping the sealed type
4. **Recursive wrapping** — If the sealed type exposes other sealed types, wrap those too
5. **Defensive validity** — Check for dead agents, null references in computed properties

## Property Guidelines
- Identify read-only (get-only) vs read-write properties from decompiled source
- Use null-conditional operators (`?.`) for computed properties accessing nested objects
- Cache expensive property lookups where appropriate

**A computed getter throws BEFORE your `!= null` guard can run — guard the inner object, not the result.** Decompile the getter body first: a property defined as `X => A.B` (e.g. `PartyBase.Culture => MapFaction.Culture`, PartyBase.cs:255) dereferences `A` with no guard, so `if (party.Culture != null)` NREs inside the getter when `MapFaction` is null. Write `party.MapFaction?.Culture` instead. Distinguish computed getters (throw) from plain `[SaveableField]` fields (`Hero.Culture`, `Settlement.Culture` — safe once the parent is non-null) by reading the member definition, not by assuming. When several models resolve the same value, funnel them through one null-safe chokepoint (e.g. `CultureFeatAdapter.ResolvePartyCulture`) and never resolve inline — a future inline `?? party.Culture` fallback silently reintroduces the crash. This shipped as a campaign-map NRE: issue #281, RCA `docs/reviews/rca-culturefeat-partyculture-nre-2026-06-15.md`. Note that copying a vanilla helper verbatim (`PartyBaseHelper.HasFeat`) inherits its unstated preconditions — TAOM hit the NRE because it calls the helper on far more parties than vanilla does.

**A hero's character can be hollow (MANDATORY before reading a raw character field).** `IsRegistered` is saved (v1.4.8 `MBObjectBase.cs:23-25`), so a hero can keep a `CharacterObject` that XML never reaches while XML builds a new twin of its id. `CharacterObject.Culture` answers from the hero (`CharacterObject.cs:258-272`) and hides a null raw culture until `BasicCharacterObject.FillFrom` copies it onto a clone (`BasicCharacterObject.cs:193-195`). Read the raw culture through `BasicCharacterObject` and the raw name through its field (`Name` is virtual, so a cast does not help), and before calling a repair done, trace every member the protected method reads after the first throw ([repair](../../docs/features/stale-character-repair.md), #1076).

**`PartyBase.MainParty` is a computed getter that throws.** It returns null only without a campaign; with one it reads `Campaign.Current.MainParty.Party` (v1.4.8 `PartyBase.cs:208-218`), so it throws, `?.` included, while the stored main party is null. Check `Campaign.Current?.MainParty` and read `Campaign.Current?.MainParty?.Party`. `MainPartyNullCheckScanTests` fails a null comparison, `is null`, `??` or `?.` on the getter written directly or inside any number of its own parentheses, and passes a call argument (`Find (PartyBase.MainParty)?.X`); it cannot see a check through a local alias (`var p = PartyBase.MainParty; if (p == null)`), so review for that form, and it blanks comments but not strings, so a string holding the check fails it (#792).

**The campaign statics are computed getters too.** `Hero.MainHero` (`CharacterObject.PlayerCharacter.HeroObject`) and `MobileParty.MainParty` (`Campaign.Current.MainParty`) throw with no campaign, which is every Custom Battle, so `Hero.MainHero?.` guards nothing there. Code a mission can reach reads `CharacterObject.PlayerCharacter?.HeroObject` (or `(Game.Current?.PlayerTroop as CharacterObject)?.HeroObject`, which also survives no Game) and `Campaign.Current?.MainParty`, or returns on `Campaign.Current == null` first (#97, #699).

## Testing
- Adapters themselves are thin wrappers — test coverage via service tests that mock the adapter interface
- Use `NSubstitute.Substitute.For<IXxxAdapter>()` in tests

## Modifier-Preserving Overloads (MANDATORY for inventory/equipment adapters)

TaleWorlds' inventory and equipment APIs frequently expose **two parallel overloads**: a simpler `(ItemObject, int)` form and a richer `(EquipmentElement, int)` form. The simpler form internally calls `new EquipmentElement(item)` — discarding any `ItemModifier` (durability state, quality prefix like "Sharp"/"Damaged", cosmetic item, quest-item flag). When the adapter touches a slot that vanilla treats as `EquipmentElement`-shaped, **the richer overload is the correct API surface.**

**Examples of the parallel-overload pattern in v1.4.5:**
- `ItemRoster.AddToCounts(ItemObject, int)` ↔ `ItemRoster.AddToCounts(EquipmentElement, int)`
- `Equipment[EquipmentIndex] = ?` accepts only `EquipmentElement` — already lossless if you pass the captured element through
- `EquipmentHelper.AssignHeroEquipmentFromEquipment` takes `Equipment` directly — preserves modifier
- `ItemRoster.AddToCounts(ItemRosterElement)` and `Add(ItemRosterElement)` carry full element

**Rule:** Before calling a `(ItemObject, ...)` overload, search for the parallel `(EquipmentElement, ...)` form. If it exists, prefer it. Update the adapter's internal data to carry the full `EquipmentElement` (not bare `ItemObject` or `string` ID).

The adapter interface boundary stays ADR-007 compliant — services see opaque snapshot tokens that internally carry the full element. See `Main/Adapters/PartyMountInventoryAdapter.cs` + `Main/Features/SiegeDismount/Models/MountSnapshot.cs` for the canonical pattern.

**Anti-pattern (do NOT ship):** documenting "modifier/quality/cosmetic is lost on round-trip" as a known limitation in the feature doc without first verifying the limitation is inherent in the API. Codex review #34 (SiegeDismount, 2026-05-06) caught exactly this — the modifier-preserving overload existed; the adapter just used the wrong one.

## Agent handles: identity is the object, liveness is the slot (MANDATORY)

An adapter wrapping a deleted `Agent` keeps answering: its native-pointer members read the engine slot
that agent's index now belongs to (#592). `MissionAdapterFactory` therefore keys by object reference,
evicts on `OnAgentDeleted`, and `AgentAdapter.IsActive()` requires `AgentSlotIdentity.IsCurrentOccupant`.
Do not add an index-keyed cache beside it, do not hold an `IAgentAdapter` across frames without
re-asking `IsActive()`, and route every `SetActionChannel` through the clip-exists guard so a clip
requested on the wrong monster is a TAOM log line, not a native warning of unknown consequence.

## A destructible's shown stage must only move forward (MANDATORY)

Never raise a damaged `DestructableComponent`'s `HitPoint` (a heal, regeneration) unless the adapter has set
`OnCalculateDestructionStateIndex` to keep the shown stage from moving backward: the engine does not, so a smaller heal
steps the object back to an earlier model, and one that lifts `HitPoint` above the first line sets stage 0, where the
engine reads `_destructionStates[-1]` after hiding the model and the object stays invisible and unhittable (v1.4.8
`DestructableComponent.cs:399-412`, `:498`; [engine note](../../docs/reference/engine/usable-machines.md); found on the
1.5.x line, 2026-10-31).

## Relations and party-screen writes (MANDATORY)

- **Relations live on clan leaders.** `ChangeRelationAction` ignores `affectRelatives` and writes to the pair from
  `DiplomacyModel.GetHeroesForEffectiveRelation`, so per-lord deltas sum per clan: key a penalty list on that owner,
  strongest delta once per owner.
- **A write made in a conversation opened from the party screen is reverted by Escape** (`PartyScreenData.ResetUsing`):
  after a roster, inventory or hero-state write, commit the screen with `PartyScreenLogic.DoneLogic(true)`.
- Sources (v1.4.8): `ChangeRelationAction.cs:14-30`, `PartyScreenLogic.cs:1525-1534`, `PartyVM.cs:3246-3264`.
