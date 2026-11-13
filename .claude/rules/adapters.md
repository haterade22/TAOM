---
paths:
  - "Main/Adapters/**"
  - "Main/**/I*Adapter.cs"
  - "Main/**/*Adapter.cs"
  - "Client/**/I*Adapter.cs"
  - "Client/**/*Adapter.cs"
---

# Adapter Pattern Rules (ADR-007)

## Core Principle
Services NEVER accept sealed TaleWorlds types directly. Always wrap with adapter interfaces. Gate: `ServiceSignatureGateTests`.

## Creating New Adapters
1. **Research first:** read the type with `pwsh tools/taom-src.ps1 path <Type>` before creating the adapter interface.
2. **Interface in `Main/Adapters/`** — `I{TypeName}Adapter.cs` with only the properties/methods the feature needs
3. **Implementation in `Main/Adapters/`** — `{TypeName}Adapter.cs` wrapping the sealed type
4. **Recursive wrapping** — If the sealed type exposes other sealed types, wrap those too
5. **Defensive validity** — Check for dead agents, null references in computed properties

## Property Guidelines
- Identify read-only (get-only) vs read-write properties from decompiled source
- Use null-conditional operators (`?.`) for computed properties accessing nested objects
- Cache expensive property lookups where appropriate

**A computed getter throws before your `!= null` guard runs: guard the inner object.** `PartyBase.Culture => MapFaction.Culture` (v1.5.6 PartyBase.cs:256) NREs when `MapFaction` is null, so write `party.MapFaction?.Culture`. Read the member definition to tell a computed getter from a saved field. Several models resolving one value go through one null-safe chokepoint (`CultureFeatAdapter.ResolvePartyCulture`). A vanilla helper copied verbatim keeps its unstated preconditions (`rca-culturefeat-partyculture-nre-2026-06-15.md`).

**The campaign statics are computed getters too.** `Hero.MainHero` (`CharacterObject.PlayerCharacter.HeroObject`) and `MobileParty.MainParty` (`Campaign.Current.MainParty`) throw with no campaign, which is every Custom Battle, so `Hero.MainHero?.` guards nothing there. Code a mission can reach reads `CharacterObject.PlayerCharacter?.HeroObject` (or `(Game.Current?.PlayerTroop as CharacterObject)?.HeroObject`, which also survives no Game) and `Campaign.Current?.MainParty`, or returns on `Campaign.Current == null` first (#97, #699). `PartyBase.MainParty` is the inverse case: it returns null only without a campaign, and with one it reads `Campaign.Current.MainParty.Party` (v1.5.5 `PartyBase.cs:209-219`), so it throws, `?.` included, while the stored main party is null. Check `Campaign.Current?.MainParty` and read `Campaign.Current?.MainParty?.Party`; `MainPartyNullCheckScanTests` fails a null comparison, `is null`, `??` or `?.` on the getter written directly or inside any number of its own parentheses, and passes a call argument (`Find (PartyBase.MainParty)?.X`); it cannot see a check through a local alias (`var p = PartyBase.MainParty; if (p == null)`), so review for that form, and it blanks comments but not strings, so a string holding the check fails it (#792).

## A singleton adapter keeps no engine object after its operation (MANDATORY)

Adapters are `Reuse.Singleton`, and patches hold them in statics, so a field that keeps a `Hero`, a `Clan` or a list of
them keeps that campaign reachable after a load. Return engine objects inside the snapshot a call hands back. When one
operation must hold them across calls, the adapter exposes a release, the service calls it in a `finally`, and a test
covers the fault path (`IAllegianceRelationAdapter.Release`; #952, repeated in #1111;
[lessons](../../docs/reviews/lessons/adapters-taleworlds-api.md)).

## Testing
- Adapters themselves are thin wrappers — test coverage via service tests that mock the adapter interface
- Use `NSubstitute.Substitute.For<IXxxAdapter>()` in tests

## Modifier-Preserving Overloads (MANDATORY for inventory/equipment adapters)

**Modifier-preserving overloads.** Before calling a `(ItemObject, ...)` overload, search for the `(EquipmentElement, ...)` form (v1.5.6 `ItemRoster.AddToCounts` has both, ItemRoster.cs:185,194). Prefer it and carry the full element in the adapter's data; services see opaque tokens (`PartyMountInventoryAdapter`, `MountSnapshot`). Never document a lost modifier as a limitation before checking the API.

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
engine reads `_destructionStates[-1]` after hiding the model and the object stays invisible and unhittable
([engine note](../../docs/reference/engine/usable-machines.md), Fords of War tower regeneration, 2026-10-31).

## A hero's character can be hollow (MANDATORY before reading a raw character field)

`IsRegistered` is saved, so a hero can keep a `CharacterObject` that XML never reaches, while XML builds a new twin of its
id. `CharacterObject.Culture` and `Name` answer from the hero and hide the nulls until `FillFrom` copies them onto a clone.
Read the raw culture through `BasicCharacterObject` and the raw name through its field (`Name` is virtual, so a cast does
not help), and before calling a repair done, trace every member the protected method reads after the first throw
([repair](../../docs/features/stale-character-repair.md), #1076).

## Relations and party-screen writes (MANDATORY)

- **Relations live on the owner the active DiplomacyModel returns** (`GetHeroesForEffectiveRelation`): his clan leader
  under the vanilla mapping and in co-op; the lord himself for the player's own pairs under Personal Relations (single
  player, on by default; AI-to-AI pairs stay on clan leaders). Under the vanilla mapping a clanless hero and a member
  of the player's clan (companions included) own their own relation.
  `ChangeRelationAction` ignores `affectRelatives`; never pass it as true (`AffectRelativesGateTests`, #923). Key a penalty list on that owner, strongest delta once per owner,
  and list every lord the deed should cost: never drop a hit because today's mapping merges it.
- **A write made in a conversation opened from the party screen is reverted by Escape** (`PartyScreenData.ResetUsing`):
  after a roster, inventory or hero-state write, commit the screen with `PartyScreenCommit.CommitIfMainParty`, the one
  way to force a party screen done (main party's own screen only; it skips the ransom screen with prisoners on the broker side).
- Both, with sources: [lessons](../../docs/reviews/lessons/adapters-taleworlds-api.md) (2026-10-19).
