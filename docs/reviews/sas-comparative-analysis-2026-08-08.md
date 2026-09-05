# Serve as Soldier (SAS) vs TAOM Enlistment — comparative analysis

**Date:** 2026-08-08 · **SAS version analysed:** 1.3.15 build (`E:\LOTRAOMAssets\Serve as Soldier-3242-1-3-15-2-1777731794\ServeAsSoldier`)
**Engine baseline:** every 1.4.7 signature below was verified with `pwsh tools/taom-src.ps1 path <Type>` against the installed DLLs, not the decompile dump.

Purpose: capture what SAS does that TAOM does not, with the engine facts already resolved, so a
future session can act on any of it without re-decompiling. Three recommendations from this
analysis already shipped (see "Landed"); the remainder are blocked on design decisions recorded here.

## Landed from this analysis

| Change | Commit | Why it mattered |
|---|---|---|
| Wait-menu options grey out instead of vanishing | `97d6b6e6` | SAS's own changelog records this as a bug they shipped and fixed. A hidden option reshuffles the list under the cursor at high game speed. |
| Enlisted survival roll redirected to the commander's party | `2a6b3061` | The serious one — see below. |
| Daily healing scales with Medicine, doubles when the column rests in a settlement | `2a6b3061` | A flat rate made both the Medicine skill and the surgeon duty mechanically inert. |

### The survival-roll defect (worth recording in full)

`DefaultPartyHealingModel.GetSurvivalChance(PartyBase party, …)` reads **every** survival bonus off
the party it is handed — `AddSurgeonSurvivalBonus(mobileParty, …)`, `PhysicianOfPeople`, and
`HasPerk(Medicine.CheatDeath, checkSecondaryRole: true)`. An enlisted TAOM player is a hidden,
inactive one-hero party with no surgeon and no perks, so going down in the commander's battle rolled
against nothing. **Serving made the player strictly more likely to die than freelancing**, with no
in-game signal.

SAS fixes this with a Prefix on `GetSurvivalChance` plus a `[ThreadStatic]` flag plus a
`PartyBase.get_MapEvent` Postfix (their MainParty has no MapEvent to satisfy the redirect). TAOM
already owns `TaomPartyHealingModel`, so the fix is eleven lines inside a GameModel we control and
needs no new patch surface. Guarded to the player, their own party, and live service; it can only
ever substitute a party, never return something worse.

Healing was deliberately left **below** SAS's ~28.8 HP/day field rate. SAS has no food handling at
all, so their number is partly compensation for out-healing vanilla's `-19/day` starvation branch.
TAOM provisions the enlisted player instead, removing the cause rather than out-running it.

## CORRECTION (added after the 13-agent comparative workflow returned)

**The premise of §1 below is inverted, and the real finding is more serious than the gap it
describes.** The section was written from the fact that `IsPlayerSergeant()` is unreachable for us.
That fact is correct; the conclusion drawn from it — "TAOM lacks battlefield command" — is not.
Follow the chain one step further, into what the engine does when the answer is *false*:

```csharp
// SandBox.SandBoxMissions, four call sites (OpenBattleMission :661/:732 among them)
bool isPlayerSergeant = MobileParty.MainParty.MapEvent.IsPlayerSergeant();
...
new AssignPlayerRoleInTeamMissionController(!isPlayerSergeant, isPlayerSergeant, isPlayerInArmy, …)
//                                          ^^^^^^^^^^^^^^^^^ isPlayerGeneral

// TaleWorlds.MountAndBlade.Team.SetPlayerRole
IsPlayerGeneral = isPlayerGeneral;
foreach (Formation item in FormationsIncludingSpecialAndEmpty)
    item.SetControlledByAI(this != Mission.PlayerTeam || !IsPlayerGeneral);
```

`ClearArmyAttachment()` runs in **both** `MobilePartyAttachmentAdapter.ParkNear` (`:63`) and
`RestorePresence` (`:97`), so `Army == null` holds in every enlistment regime, so
`IsPlayerSergeant()` is permanently false, so `isPlayerGeneral` is permanently **true**, so every
formation on the player's side gets `SetControlledByAI(false)`.

**A rank-1 enlisted private commands the entire battle line, and the lord he serves commands
nothing.** That is the precise inverse of the feature's fantasy, and it is not a missing feature —
it is a live defect. The work is to *remove* command, not to grant it.

Two candidate shapes, both needing the in-game check first (see below):

- **(a)** `Mission.PlayerTeam.SetPlayerRole(false, false)` from a `MissionLogic` while enlisted —
  public API, no Harmony patch. But "neither general nor sergeant" is a state vanilla never
  produces in a campaign battle (exactly one is always true), so it is untested engine ground.
- **(b)** Transient battle-only army join → `IsPlayerSergeant()` becomes true → the player commands
  one formation as a sergeant, which is both vanilla-supported and the better fantasy for a
  senior rank. This is what SAS actually does.

`ClearArmyAttachment` is itself load-bearing — `DefaultEncounterGameMenuModel.GetGenericStateMenu`
dereferences `mainParty.Army` unguarded inside its `AttachedTo != null` branch — so the open
question is whether `Army != null` with `AttachedTo == null` is a stable state. Unverified.

**The one check that settles it:** start any enlisted battle and look at whether the F1–F8 order UI
is live and all formations are selectable. Do that before any mission-layer work. Filed as **#424**
with the full chain, both candidate fixes, and the unresolved `Army != null` / `AttachedTo == null`
sub-question.

Note also that `GetCharacterSergeantScore` feeds `DefaultEncounterModel.GetLeadingScore →
GetLeaderOfMapEvent`, so SAS's score-rigging is not cosmetic — it changes *who leads the battle* at
campaign level (sally-out menu, `PlayerEncounter.LeaveBattle`). Making a private outrank his
commander there is a campaign-state mutation, not a UI tweak. Do not copy it.

## Blocked on a design decision — do not build without the user

### 1. Sergeant / formation command at max rank

> Superseded by the correction above — retained because its engine facts are accurate and still
> needed. Read the correction first.

SAS lets an enlisted soldier command a formation in battle. It is **not** custom order UI — they rig
vanilla's own Sergeant mechanic with three patches:

- `DefaultEncounterModel.GetCharacterSergeantScore(Hero)` → returns 1,000,000 while enlisted.
- `MapEvent.IsPlayerSergeant()` → forced **off** below their tier 6; vanilla decides at 6+.
- `BehaviorComponent.InformSergeantPlayer()` → Postfix for flavour (quick-info text, horn, voice line).

All three targets exist unchanged in 1.4.7.

**The blocker is architectural, not a signature.** Verified body:

```csharp
public bool IsPlayerSergeant()
{
    if (IsPlayerMapEvent && GetLeaderParty(PlayerSide) != PartyBase.MainParty && MobileParty.MainParty.Army != null)
        return MobileParty.MainParty.Army.LeaderParty != MobileParty.MainParty;
    return false;
}
```

`MobileParty.MainParty.Army != null` is required. TAOM's enlisted player is parked and joins battle
through `PlayerEncounter.JoinBattle(side)`, which sets `MapEventSide` and never touches `Army`. So
rigging the score alone does nothing for us — SAS only clears this gate because they transiently
army-join for battles and disband after.

Two routes, and picking between them is a design call:

- **(a)** Transiently add the player to the commander's army for the battle, as SAS does. Uses
  vanilla end to end. Costs the army-membership machinery we deliberately avoid. Note
  `Army.OnAddPartyInternal` guards its influence charge with `mobileParty != MobileParty.MainParty`,
  so the player joining costs the commander no influence — the objection that killed permanent army
  membership does **not** apply to a transient battle-only join.
- **(b)** Postfix `MapEvent.IsPlayerSergeant()` to return true when enlisted at top rank. One patch,
  no army involvement, but we own a behaviour vanilla normally derives.

**Also note `GetCharacterSergeantScore` has no caller inside the on-demand `taom-src` cache** — that
cache is populated per-request and is *not* a whole-engine scan, so absence there is not evidence of
absence. Resolve the real callers before building either route.

### 2. Duty → effective party role

SAS Prefixes the four `MobileParty.Effective{Quartermaster,Scout,Engineer,Surgeon}` getters to return
`Hero.MainHero` when the player holds the matching duty, so the player's own skill governs the
column's bonus.

TAOM has no equivalent: our duties pay the player XP, gold and trust, and **nothing reaches the
commander's party**. Verified — `grep` for all four `Effective*` members and the `SetParty*` setters
returns zero hits in `Main/`.

The public setters cannot substitute for the patches. Every getter guards with
`Scout.PartyBelongedTo != this`, and an enlisted player belongs to their own parked party, so
`SetPartyScout(MainHero)` on the commander's party is rejected and falls back to `LeaderHero`. That
guard is precisely why SAS patches the getters.

**Why this was not simply adopted:** those four getters are read for *every party in the world* on
campaign ticks (map speed, food, siege, healing), so four skip-original Prefixes put TAOM code on a
very hot path to serve one hero. The effects flow through GameModels instead — `EffectiveSurgeon`
into party healing (which TAOM already overrides), `EffectiveScout` into party speed, and so on — so
delivering the *effect* in models we own is narrower, testable, and patch-free. That is a
feature-sized design, not a port.

**Axis mismatch to be aware of:** TAOM's `ServiceAssignment` is a *combat* role
(`Infantry / Archer / Cavalry / Support`, `Main/Features/Enlistment/Content/Domain/ServiceEnums.cs:13`).
SAS's four roles are *staff* roles. This would be a new axis, not a re-use of the existing enum.

## Rejected — do not copy

| SAS behaviour | Why not |
|---|---|
| Permanent army membership for the enlisted player | Their *normal* following is the same technique TAOM already uses (`Position = commanderParty.Position`, `IsVisible = false`, `IsActive = false`). Army membership is transient and battle-only even in SAS. This reverses an earlier recommendation made before their following code was read. |
| Ripping lords out of their own armies | Mutates world state the player has no business touching. |
| `UpdateDiplomacy` declaring war on the player's behalf | Irreversible faction consequences from a service side-effect. |
| Both custom arena `MissionLogic` classes (913 / 432 lines) | God classes mixing spawn logic, per-weapon equipment tables, reward math and UI text. ADR-002/ADR-007 reject the shape outright. The *player-facing idea* (spar against your own column with blunted gear) is worth keeping; the implementation is not. |

## SAS patches that are already dead against 1.4.7

Recorded so nobody ports them assuming they work. Each verified this turn:

- **`PlayerEncounter.DoLootParty` no longer exists** — split into `DoLootInventory()`, `DoLootShips()`,
  `DoLootMembersAndPrisonersOfParty()`. SAS's entire "no loot while enlisted" mechanism is inert.
  A TAOM equivalent needs three patches, not one.
- **`Mission.SpawnTroop` has no `forceDismounted` parameter** in 1.4.7 (`Mission.cs:4430`). Harmony
  matches prefix params by name, so their patch throws at apply time.
- **`TournamentBehavior` moved** from `TaleWorlds.CampaignSystem.TournamentGames` to
  `SandBox.Tournaments.MissionLogics` and changed base class to `MissionLogic`. A baked `typeof`
  reference to the absent type can throw `TypeLoadException` — and because SAS applies everything
  through one undifferentiated `Harmony.PatchAll()` with no per-category try/catch, that risks
  aborting `Assembly.GetTypes()` and silently disabling unrelated patches. This is a live argument
  for TAOM's per-category isolation convention (cf. Patch43/60/61/62).
- **`ArmyDispersionReason` is now nested** as `Army.ArmyDispersionReason` (`Army.cs:31`) — a different
  CLR type, so their parameter match fails.
- **`TownArmourPatch` is dead code** — its Prefix is an unconditional `return true`. A donor mod's
  attributed patch is not necessarily load-bearing.

## Collisions with TAOM's registry

None. Verified against the full `docs/reference/harmony-patch-registry.md`. Three same-class,
disjoint-method adjacencies exist and are harmless: `TournamentFightMissionController`
(TAOM `.PrepareForMatch` vs SAS `.GetTeamWeaponEquipmentList`), `CaravansCampaignBehavior`, and
`LordConversationsCampaignBehavior` (Patch66's four conditions vs SAS's four different ones).

## Worth stealing, unranked and unblocked

- **The thread-static-scoped redirect pattern.** SAS's `MainPartyMapEventPatch` opens a
  `[ThreadStatic]` window for the duration of one call rather than leaving a blanket
  `PartyBase.MapEvent` Postfix live for every unrelated caller. If TAOM ever needs a
  "treat my party as the commander's" redirect somewhere a GameModel cannot reach, copy this
  narrow scoping — not a global patch.
- **Content beats** (Batch 11, still unstarted): trait-gated dialogue replies, named speakers on
  incidents, village ambush, luring bandits into an ambush.

## Standing caveat

Everything above sits on top of a battle-join path that **has never been verified in a live game**
(#406). Unit tests missed the original never-joins defect four times because they mock
`IEncounterAdapter` and cannot reach the adapter/engine seam. Build nothing further on this
foundation until the four in-game battle cases in #406 have actually been observed.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
