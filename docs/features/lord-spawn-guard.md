# Lord Spawn Guard

**Status:** implemented 2026-08-04 · **Issue:** [#374](https://github.com/haterade22/TAOM/issues/374) · **Patch category:** `Patch65_LandlessCultureSpawnGuard` · **Code:** `Main/Features/LordSpawnGuard/` · **RCA:** [rca-landless-culture-spawn-2026-08-04.md](../reviews/rca-landless-culture-spawn-2026-08-04.md)

## Overview

Keeps the campaign alive when a lord belongs to a culture that owns no settlement. Vanilla assumes
every culture holds land; TAOM's map does not guarantee that, and the gap surfaces as a hard CTD on
an ordinary daily clan tick with no TAOM code anywhere on the stack.

The feature has two halves — a Harmony guard that makes the engine path survivable, and a data fix
that removed the one landless culture TAOM actually authored.

## Why This Exists

**Vanilla behavior.** `HeroSpawnCampaignBehavior.SpawnLordParty` (v1.4.7, lines 252-260) picks a
settlement to spawn a lord's party around:

```csharp
Settlement settlement = SettlementHelper.GetBestSettlementToSpawnAround(hero);
if (settlement == null || settlement.MapFaction != hero.MapFaction)
    settlement = hero.MapFaction.InitialHomeSettlement;
if (settlement == null)
    settlement = Settlement.All.First(x => x.Culture == hero.Culture);   // unguarded
```

That last line is safe in Calradia because every culture owns settlements. It is a `First`, not a
`FirstOrDefault`, so an empty match throws `InvalidOperationException` — out of `Campaign.Tick`,
past every managed backstop, straight to the desktop.

**TAOM requirement.** `TAOM_Map/ModuleData/settlements.xslt` contains
`<xsl:template match="Settlement"/>` — it deletes *every* vanilla settlement and replaces the world
with TAOM_Map's own 988. Those 988 covered 27 of the 38 defined cultures. Five of the eleven
landless cultures can sit on an `Occupation.Lord` hero:

| Culture | TAOM meaning | Carried by |
|---|---|---|
| `battania` | **Variag** | kingdom `battania`, `clan_battania_1-8`, `wolfskins`, 41 vanilla (incl. `main_hero`) + 18 TAOM `Lord` characters |
| `darshi` | vanilla | `ghilman` |
| `nord` | vanilla | `skolderbrotva` |
| `vakken` | vanilla | `forest_people` |
| `neutral_culture` | vanilla | — |

The bandit cultures (`looters`, `sea_raiders`, the four `*_bandits`) are landless too but
unreachable: `GetBestAvailableCommander` filters on `Occupation.Lord`.

**Without this feature.** Crash report `099f650c` (2026-08-04, TAOM v2.0.16, Bannerlord v1.4.7,
Summer 2 / 1084). No TAOM patch on frames 0-11 — the report's own Harmony correlation says so.

## Architecture

### Design challenge

The throwing line is only reached when `hero.MapFaction.InitialHomeSettlement` is null, so the
crash needs **two** independent faults:

1. **A faction with no initial home settlement.** Every TAOM clan and kingdom has a valid
   `initial_home_settlement` once the `spclans.xslt` / `spkingdoms.xslt` attribute rewrites are
   resolved — note that several templates *drop* the inherited attribute via
   `@*[local-name() != 'initial_home_settlement']` and re-add it, so a naive read of the vanilla
   XML gives the wrong answer. **Vanilla ships no such clan at all.** An earlier revision of this
   doc named three (`guardians`, `chosen_of_the_sky`, `freemen`) — that was wrong: all three sit
   inside XML comment spans in SandBox `spclans.xml`. An `ElementTree` parse finds 95 live
   `<Faction>` elements, **zero** missing `initial_home_settlement`; a line-oriented grep finds 98
   and produced the false claim. The remaining non-mod path is a settlement rename that misses
   `spclans.xslt` — it re-points 26 vanilla clans whose original ids are absent from TAOM_Map, so
   a rename landing outside it would strand one. Otherwise this
   leaves **clans created at runtime that never call `SetInitialHomeSettlement`** — a third-party
   mod. `Warlord` v1.1.6.1 (`com.bloc.warlord`) is the only clan-creating mod in the reporter's
   load order, but it is not installed on the dev machine and was never decompiled, so that
   attribution is **unconfirmed**. The guard does not depend on it.
2. **A landless culture**, above.

TAOM only controls fault 2, and only for cultures it authors. Fault 1 is unbounded — any mod can
produce it — so the engine path itself has to stop being fatal.

### Solution approach

**Repair the precondition, don't reimplement the method.** The prefix gives the faction an
`InitialHomeSettlement`, which makes vanilla take the branch *above* the throwing line. Everything
downstream stays vanilla: spawn position, the `isNewGame` roster fill, `GiveInitialItemsToParty`.
A skip-original prefix would have had to duplicate all of it.

`Clan.InitialHomeSettlement` is a `[SaveableProperty(114)]`, so the write persists — one repair per
broken faction for the life of the save, not a per-tick patch-up. The anchor is chosen
deterministically (no `MBRandom`), so a reloaded save repairs the same faction the same way:

```
hero.HomeSettlement -> hero.BornSettlement -> clan leader's settlement
                    -> nearest non-hostile -> nearest of any allegiance
```

Evaluated lazily and short-circuited — the last two walk `Settlement.All`.

**The finalizer is the backstop**, not the mechanism. It is scoped to `InvalidOperationException`
only; anything else propagates untouched. Nulling `__result` is safe because
`ConsiderSpawningLordParties` already null-checks before `GiveInitialItemsToParty`, so the lord
simply raises no party that day. It covers a faction the prefix could not anchor — for example a
faction type with no writable setter.

**Gate ordering matters.** `FactionHasInitialHomeSettlement` is one property read and clears every
healthy faction; the culture check walks all 988 settlements. The service checks the anchor first,
and `EnsureSpawnAnchor_HealthyFaction_DoesNotScanSettlementsForTheCulture` pins that ordering.

### Component diagram

```
HeroSpawnCampaignBehavior.SpawnLordParty (vanilla, private)
   |
   +-- [HarmonyPrefix]  Patch65_LandlessCultureSpawnGuard
   |      -> ILordSpawnGuardService.EnsureSpawnAnchor(heroId)      (cached IoC resolve)
   |           -> ILordSpawnGuardAdapter                            (only TaleWorlds touchpoint)
   |                Hero / Clan / Kingdom / Settlement
   |
   +-- [HarmonyFinalizer] InvalidOperationException -> __result = null
```

## The data half: Variag was a landless culture

TAOM authored `battania` into the **Variag** culture — `{=TAOM_battania_culture}Variag`, 7.3 KB of
culture XSLT, 26 notable templates, 68 `*_khand` NPCCharacter bindings, a kingdom, 8 clans and 18
TAOM-authored lords (plus 41 vanilla `battania` `Lord` characters, `main_hero` among them) — but
never migrated the settlements. All 27 K-series settlements stayed `Culture.khuzait` (Easterlings),
while the Variag clans held 10 of them:

```
town_K1  "Sturlurtsa Khand"  owner=clan_battania_1     castle_K1, castle_K6  owner=clan_battania_6
town_K2  "Lermsakun"         owner=clan_battania_4     castle_K5, castle_K7  owner=clan_battania_7
town_K3  "Ardivar"           owner=clan_battania_2     castle_K3             owner=clan_battania_5
town_K4  "Yanik Anli"        owner=clan_battania_3     castle_K2             owner=clan_battania_8
```

So Khand produced Easterling notables, volunteer pools, guards and marketplace stock, and Variag
owned nothing.

`CultureConversion` was never going to fix it: `RunDailyChecks` only drains records seeded by
`OnSettlementConquered` (`OnSettlementOwnerChangedEvent`), so a settlement whose culture never
matched its owner from day 1 is never enqueued. The crash log confirms it — 90+ in-game days, zero
conversion lines.

**26 of the 27 settlements were retagged to `Culture.battania`.** `castle_K4` belongs to
`clan_khuzait_1` and stays Easterling. Variag went 0 -> 26 settlements; khuzait 136 -> 110.

### What the retag activated (and why two more edits were needed)

The Variag culture's troop bindings were still **vanilla and dormant** — nothing carried the
culture, so nobody noticed. Retagging woke them:

| Binding | Before the fix | Now |
|---|---|---|
| `basic_troop` | `battanian_volunteer` | `loke_rim_initiate` |
| `melee_militia_troop` | `battanian_militia_spearman` | `rhun_militia_spearman` |
| `ranged_militia_troop` | `battanian_militia_archer` | `rhun_militia_archer` |
| `*_elite_militia_troop` | `battanian_militia_veteran_*` | `rhun_militia_veteran_*` |
| `default_party_template` | `kingdom_hero_party_battania_template` | `kingdom_hero_party_rhun_template` |

TAOM redefines the `battanian_*` ids **nowhere**, so left alone the retag would have garrisoned
Sturlurtsa Khand with Calradian Battanian militia. The Rhun roster is what those settlements
produced before the retag, so this keeps behaviour identical while making the culture correct.
Khand still has no roster of its own — re-theme in `spcultures.xslt` if one is ever authored.

`CultureMap["battania"]` was added for the same class of reason. The volunteer cascade ends at
`ResolvePool(CultureId, CultureMap)` and the K-series settlements have **no per-settlement pools**,
so the retag would have silently dropped Khand's volunteers to null. It aliases the Rhun pool. A
side benefit: `HasCulturePool` gates `CultureConversion`, so a fief taken by a Variag clan could
never convert to Variag before — now it can. The pre-existing
`HasCulturePool_PlayableCultureWithoutTroopSet_ReturnsFalse_KnownGap` test documented that gap and
was retired.

## Preventing the regression

`tools/validate_moduledata.py` gained a `LANDLESS_CULTURE` check: every culture on a
`Lord`-occupation `NPCCharacter`, a `<Faction>` or a `<Kingdom>` in TAOM's ModuleData must own at
least one settlement, or appear in `_LANDLESS_BY_DESIGN` with a stated reason.

The settlement registry (`build_settled_cultures`) walks the settlement-contributing modules in
load order and **honours an unconditional `<xsl:template match="Settlement"/>` strip**. Without
that it would count vanilla's 494 deleted settlements and report every culture as landed — the
check would pass while the game crashed.

`_LANDLESS_BY_DESIGN` currently holds the six bandit cultures (unreachable through the
`Occupation.Lord` filter), `neutral_culture`, and `darshi` / `nord` / `vakken`.

Scope is TAOM's own ModuleData, matching the validator's stated contract. Vanilla-inherited
factions are Patch65's problem, not a TAOM data defect.

## Known gaps

- **`darshi` / `nord` / `vakken`** — `ghilman`, `skolderbrotva` and `forest_people` are vanilla
  minor factions TAOM inherits but never re-cultured. All three keep a valid
  `initial_home_settlement`, so vanilla never reaches the `First()`; Patch65 covers them if a mod
  ever re-parents their lords. Worth a follow-up: they have no TAOM content at all.
- **Warlord attribution is unconfirmed** — see Design challenge above.
- **The retag hits existing saves too — the reverse of what this doc first claimed.**
  `Settlement.Culture` is a bare `public CultureObject Culture;` (installed v1.4.7
  `Settlement.cs:70`) with **no** `[SaveableField]`, it has no entry in
  `AutoGeneratedSaveManager`'s Settlement member set, and `Settlement.cs:961` re-reads it from XML
  on every load. That is exactly why TAOM's own `CultureConversion` has to re-apply converted
  cultures on load. Contrast `Hero.Culture` — also a bare public field, but it *does* get an
  auto-generated accessor and therefore *does* persist; the attribute alone does not tell you which
  kind you have. So every player who loads a save after installing this gets Variag Khand
  immediately, and the existing-save path needs testing as much as a new campaign does.
  The Patch65 anchor repair also applies to existing saves (and persists, since
  `Clan.InitialHomeSettlement` *is* `[SaveableProperty(114)]`).

## Testing

| Test | Covers |
|---|---|
| `LordSpawnGuardServiceTests` (16) | gate ordering, every fallback rung, blank-candidate skipping, once-per-faction logging, adapter-throws containment |
| `Patch65LandlessCultureSpawnGuardBindingTests` (2) | the private-by-name target, the `MobileParty` return type the `ref __result` finalizer needs, the `hero` parameter name Harmony binds by, and both `InitialHomeSettlement` setters |
| `HarmonyPatchBindingTests` | 61/61 against the installed v1.4.7 engine |
| `VolunteerRecruitmentConversionTests` | `battania` moved into the has-a-pool row |
| `tools/validate_moduledata.py` | 18 `LANDLESS_CULTURE` errors before the retag, PASS after |

`./build.ps1 -RunTests` — 4912 passed, 0 failed, 2 skipped.

**Owed in-game — two cases, not one:**

1. **New campaign:** Khand towns show Variag notables and Rhun volunteers; fast-forward ~30 days so
   `DailyTickClan` sweeps every clan.
2. **Existing save, created before this change.** Not optional and not a lesser case — per the
   save-compat note above, the retag lands on load, so this is the path every current player is on.
   Load a pre-change save and confirm the Khand cluster reads Variag with no notable/volunteer
   breakage.

Reproducing the *original* crash needs a clan with a null `InitialHomeSettlement` — either Warlord,
or a debug console command that calls `Clan.CreateClan` without `SetInitialHomeSettlement`.

## Files

| File | Role |
|---|---|
| `Main/Features/LordSpawnGuard/Hooks/Patch65_LandlessCultureSpawnGuard.cs` | prefix + finalizer |
| `Main/Features/LordSpawnGuard/LordSpawnGuardService.cs` | decision logic |
| `Main/Adapters/LordSpawnGuardAdapter.cs` | sole TaleWorlds touchpoint |
| `Main/_Module/ModuleData/spcultures.xslt` | Variag troop bindings |
| `Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.Rhun.cs` | `CultureMap["battania"]` |
| `tools/oneoff/retag_khand_to_variag.py` | the retag (dry-run default, idempotent, refuses to write on any surprise) |
| `tools/taom_schema.py` | `LANDLESS_CULTURE` + `build_settled_cultures` |

**Live-file note:** the retag targets
`<game>/Modules/TAOM_Map/ModuleData/settlements.xml`. The repo's
`Main/_Module/ModuleData/settlements.xml` is a stale shadow — editing it changes nothing
(CLAUDE.md Traps).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/culture-conversion.md](./culture-conversion.md)
- [docs/features/moduledata-validation.md](./moduledata-validation.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reviews/lessons/data-content-cultures.md](../reviews/lessons/data-content-cultures.md)
- [docs/reviews/lessons/harmony-il.md](../reviews/lessons/harmony-il.md)
- [docs/reviews/rca-landless-culture-spawn-2026-08-04.md](../reviews/rca-landless-culture-spawn-2026-08-04.md)

<!-- backlinks-end -->
