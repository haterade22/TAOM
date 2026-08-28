# Battlefield Banner Bearers

> **Issue:** [#351 — feat(banner-bearers): formations raise their faction standard, bearers keep their race](https://github.com/haterade22/TAOM/issues/351) · **RCA:** [rca-banner-bearers-2026-07-16.md](../reviews/rca-banner-bearers-2026-07-16.md)

## Overview

Formations in TAOM battles field **banner bearers** — soldiers who raise their faction's standard, granting the formation's banner bonus and giving battle lines a visible identity. Bearers keep their own race: an orc formation's bearer is an orc, a dwarf formation's is a dwarf.

The feature is thin on purpose. Bannerlord already ships a complete banner-bearer system (`BannerBearerLogic`) and already runs it in every field battle, sally-out and siege. It just never switches it on. TAOM supplies the missing trigger plus the policy the engine asks for through a GameModel.

## Why This Exists

- **Vanilla behavior:** `BannerBearerLogic` is added to every battle, but a formation only receives a banner from `GeneralsAndCaptainsAssignmentLogic.cs:264`, and only when it has a hero **captain** whose `FormationBanner` is non-null (or via the player's Order-of-Battle screen, `OrderOfBattleFormationItemVM.cs:1258`). Those are the *only* two gameplay callers of `SetFormationBanner`.
- **TAOM requirement:** TAOM's lords carry no banner items, so no formation ever gets one and no bearer ever appears. Middle-earth armies should march under standards.
- **Without this feature:** every TAOM battle line is bannerless, and the engine's banner morale/damage effects — already implemented and paid for — are dead code.

### Why not port "Raise your Banner"

The third-party RYB mod solves the same problem by **spawning clone bearers** from a synthetic `ryb_banner_bearer` NPCCharacter. That character declares no `race` attribute, so `BasicCharacterObject.Race` defaults to `0` and `AgentData(IAgentOriginBase)` derives *both* skeleton and skin from it (`TaleWorlds.Core.AgentData.cs:57-61`) — **every bearer spawns human**, regardless of the source unit.

That bug is not fixable in RYB's architecture:

| Attempted fix | Why it fails |
|---|---|
| `AgentBuildData.Race(int)` | `AgentRace` is referenced **zero times** in `Mission.cs` — it is multiplayer-only. |
| `.Race()` anyway | `AgentData.Race()` sets `GenderOverriden = true` without setting `AgentIsFemale` (engine copy-paste bug, `AgentData.cs:185-190`) — it silently forces every bearer **male**. |
| Fix the skin | Skin comes from `Character.Race`, read live in `AddSkinMeshes` (`Agent.cs:5409`). `CharacterObject` is a shared `MBObjectManager` singleton — mutating it changes that troop campaign-wide. |

The native path has no such problem **by construction** (see below). Driving it costs ~400 lines instead of RYB's 4,535, needs no third-party code or assets, adds no agents, and touches no party roster.

## Architecture

### Design Challenge

Three engine constraints shape the whole feature.

1. **Race is decided by two things, and `AgentBuildData.Race` is neither.** The skeleton/action-set/capsule come from `AgentBuildData.AgentMonster`; the skin comes from `agent.Character.Race`. Anything that spawns a bearer from a *different* character than the source troop gets the race wrong.
2. **`SetFormationBanner` is deployment-only.** It unconditionally calls `UpdateBannerBearersForDeployment` (`BannerBearerLogic.cs:644`) → `UpdateAgent` → `agent.SetIsAIPaused(true)` (`:760`). The **only** unpause in a battle is `DeploymentMissionController.cs:62` (`FinishDeployment`), which then removes itself from the mission. Call it post-deployment and the banners appear while every bearer stands motionless for the whole battle.
3. **`GetBannerBearerReplacementWeapon` can return null.** `CreateBannerEquipmentForAgent` writes that null into the weapon slot and clears slots 1–3 (`:824-828`), leaving an **unarmed** bearer.

### Solution Approach

**Race safety is inherited, not implemented.** `BannerBearerLogic.UpdateAgent` converts an **existing** agent in place (`UpdateSpawnEquipmentAndRefreshVisuals`) and never respawns it, so `Character`, `Monster` and skin are untouched. Reinforcement bearers go through `SpawnBannerBearer` → `Mission.SpawnTroop(realTroopOrigin, ...)`, which derives race from a real troop. There is no code path in which a bearer's race can drift.

`Agent.IsHuman` — which vanilla's `CanAgentBecomeBannerBearer` requires — is `(GetAgentFlags() & AgentFlag.IsHumanoid) != 0`, i.e. the **humanoid flag, not the human race**. All 14 LOTRLOME races declare `IsHumanoid="true"`, so orcs, dwarves, elves and goblins already qualify.

**One call is the whole feature.** After `SetFormationBanner` has run once for a formation, the engine self-heals: `MissionAgentSpawnLogic` re-checks `GetMissingBannerCount(formation) > 0` per spawned troop and calls `SpawnBannerBearer` (`TaleWorlds.MountAndBlade.cs:82839-82841`). No tick loop, no post-deployment re-application.

### Siege safety — the heraldry guard (native CTD, #349)

Broadening `SetFormationBanner` from vanilla's player-side, hero-captained formations to **every team's** formations re-opened a precondition vanilla never had to check: the bearer's heraldry must exist. The native tableau rebuilt by `UpdateSpawnEquipmentAndRefreshVisuals` renders the bearer's heraldry `Banner`, seeded at spawn from `agent.Origin.Banner` (`Mission.cs:4434`). A custom-faction garrison/militia party whose clan/kingdom has no heraldry gives its agents a null `Banner` (or an empty `BannerDataList`); the native builder dereferences it and access-violates (`0xC0000005 @ TaleWorlds.Native.dll+0x28ac0e`) — a 100%-repro CTD on siege OrderOfBattle at `sturgia_town_c`, invisible to BUTR because it is a native AV, not a managed exception.

`TryAssignBanner` guards against it: it skips `SetFormationBanner` unless **every** bearer-candidate carries renderable heraldry. Two subtleties, both from the `/deep-review` of the fix (RCA below):

- **All candidates, not slot 0.** The engine picks the bearer by priority across all non-detached units (`BannerBearerLogic.FindBannerBearableAgents`), so a mixed-origin formation can pass a slot-0 check yet render a null-banner bearer. [`FormationBannerHeraldry.CollectCandidateBannerDataCounts`](../../Main/Features/BannerBearers/Hooks/FormationBannerHeraldry.cs) samples every candidate; [`BannerBearerService.HasRenderableHeraldry`](../../Main/Features/BannerBearers/BannerBearerService.cs) requires all renderable (empty set → skip).
- **Exception-safe read.** `PartyAgentOrigin.Banner` is a computed getter that falls through to `Party.MapFaction.Banner` with no null guard, so it can *throw*, not just return null — `?.` cannot cover that. `SafeBannerDataCount` wraps the read; any failure degrades to "skip."

Skipping is provably sufficient: with no `SetFormationBanner` call the engine builds no `FormationBannerController` for that formation, so the reinforcement (`GetMissingBannerCount` → null controller → 0) and `OnMissionTick` re-application paths are inert for it. Siege banner bearers still appear for every formation whose parties have valid heraldry; only heraldry-less formations go bannerless. RCA: [rca-banner-bearers-siege-ctd-2026-07-23.md](../reviews/rca-banner-bearers-siege-ctd-2026-07-23.md).

### Component Diagram

```
banner_bearers/banner_bearers_config.json      taom_spcultures.xml / spcultures.xslt
        |                                        (banner_bearer_replacement_weapons)
  BannerBearerConfigProvider                                    |
  (validating loader, Lazy<T> process-lifetime cache)           |
        |                                                       |
  BannerBearerService                                           |
  (pure policy: density curve, race gate,                       |
   culture->banner, majority-culture vote)                      |
       /                    \                                   |
TaomBattleBannerBearersModel   BannerBearerAssignmentMissionLogic
  (GameModel: the engine          (MissionLogic: OnTeamDeployed ->
   asks it "how many?" and          SetFormationBanner for formations
   "may this agent?"; each          vanilla skipped)
   disabled path -> base)
                    \            /
                  engine BannerBearerLogic  <--- reads the culture XML directly
        (promotion, drops, ground pickup, banner-search AI,      for the bearer's sidearm
         reinforcement bearers, formation effects — all free)

MixedFormations Patch30 falls through for `unit.Banner != null`
  so the engine's banner slots survive (see mixed-formations.md)
```

### Registration

| Where | What |
|---|---|
| [Main/IoC.cs](../../Main/IoC.cs) | `BannerBearersIoC.RegisterBannerBearersFeature(container)` — both services `Reuse.Singleton` |
| [Main/SubModule.cs](../../Main/SubModule.cs) `OnGameStart` | `campaignStarter.AddModel<BattleBannerBearersModel>(new TaomBattleBannerBearersModel(...))` |
| [Main/SubModule.cs](../../Main/SubModule.cs) `OnMissionBehaviorInitialize` | `AddTaomBehavior(new BannerBearerAssignmentMissionLogic())` — unconditional, gates internally |

`MissionGameModels` resolves the slot in its constructor via `GameModelsManager.GetGameModel<T>()`, which iterates **backwards** — last registered wins. TAOM's `OnGameStart` runs after SandBox's `InitializeGameStarter`, so TAOM's model wins. This is the same seam `TaomCombatMechanicsModel` (`AgentApplyDamageModel`) already uses.

**Campaign-only.** Custom Battle builds `CustomBattleBannerBearersModel` off a `BasicGameStarter` and is unaffected.

### Why subclass `SandboxBattleBannerBearersModel` rather than decorate `MBGameModel.BaseModel`

`BannerBearerLogic.OnBehaviorInitialize` calls `InitializeModel(this)` on `MissionGameModels.Current.BattleBannerBearersModel` — i.e. **only on the resolved model** (ours). A `BaseModel` instance never receives the logic, so:

- `BaseModel.CanFormationDeployBannerBearers` would read a null `BannerBearerLogic` and return `false` forever, silently disabling every banner.
- `BaseModel.GetAgentBannerBearingPriority` would call *its own* `CanAgentBecomeBannerBearer`, bypassing TAOM's race gate.

Subclassing keeps `this.BannerBearerLogic` (the initialized one) behind every `base` call and keeps virtual dispatch landing on our overrides. Same pattern as `TaomAgentApplyDamageModel : SandboxAgentApplyDamageModel`.

## Configuration

`Main/_Module/ModuleData/banner_bearers/banner_bearers_config.json`. **Edits need a full application restart** — the provider is a `Reuse.Singleton` with a `Lazy<T>`, so the config is cached for the Bannerlord process, not per save-load.

| Field | Default | Valid | Notes |
|---|---|---|---|
| `Enabled` | `true` | — | Master switch. Off = exact vanilla: the MissionLogic assigns nothing, **and every GameModel override defers to `base`** so vanilla's own hero-captain / Order-of-Battle banner path keeps working untouched. |
| `MinimumFormationTroopCount` | `4` | 2–100 | Engine floor is 2. **Must not vary mid-mission** — `OnAgentAdded`/`OnAgentRemoved` detect the threshold with exact equality (`CountOfUnits == minimum`). |
| `MaxBearersPerFormation` | `6` | 1–6 | Ceiling of 6 is the engine's: bearer arrangement tables are `RelativeFormationPosition[6]`. |
| `AllowedFormationGroups` | `["Infantry"]` | FormationClass names | Which classes may carry a banner. **Default infantry-only** — see below. Unknown names dropped at load; empty/all-invalid reverts to Infantry. |
| `InfantryBannerPerSoldiers` | `10` | 0–1000 | One banner per N soldiers. `0` disables that class. A class produces bearers only if it's **both** in `AllowedFormationGroups` **and** has ratio > 0. |
| `RangedBannerPerSoldiers` | `25` | 0–1000 | Inert while `Ranged` isn't in `AllowedFormationGroups`. |
| `CavalryBannerPerSoldiers` | `15` | 0–1000 | Inert while `Cavalry` isn't in `AllowedFormationGroups`. |
| `HorseArcherBannerPerSoldiers` | `15` | 0–1000 | Inert while `HorseArcher` isn't in `AllowedFormationGroups`. |
| `OtherBannerPerSoldiers` | `25` | 0–1000 | Any formation class outside the four defaults. |
| `ExcludedRaces` | trolls + named | — | Race ids from `LOTRLOME_Armory/ModuleData/skins.xml`. |
| `CultureBanners` | 28 entries | — | Culture **StringId** → banner `ItemObject` id. Map to `""` for no banner. See the StringId table below — keys are ids, not LOTR names. |
| `DefaultBannerItemId` | `""` | — | Fallback for unmapped cultures. **Empty by design — fail closed.** See below. |

### Why `DefaultBannerItemId` ships empty

38 cultures are registered at runtime; only the 28 in `CultureBanners` are meaningfully TAOM's. The remaining 10 are vanilla leftovers still carrying **~99 live references in TAOM's own ModuleData** — `looters`, `sea_raiders`, `forest_bandits`, `desert_bandits`, `mountain_bandits`, `steppe_bandits`, `nord`, `vakken`, `darshi` — plus `neutral_culture`.

Any non-empty default hands a banner to **all** of them. The first cut shipped `standard_of_duty_t1`, which would have put the Gondorian Standard of Duty in the hands of every vanilla-culture bandit warband in the game.

A forgotten culture with **no** banner is a cosmetic absence. A forgotten culture with the **wrong** banner is an immersion break wearing a correct-looking mask. Absence wins — map a culture explicitly to give it a standard.

Out-of-range values **revert per-field to the compiled default with a warning**, plus a summary warning — they are never silently applied (`csharp-architecture.md` "Config Providers MUST Validate").

### ⚠️ `CultureBanners` keys are StringIds, not LOTR names

TAOM re-skins the six vanilla cultures through `spcultures.xslt`, which overrides `<name>` but **never `id`**. So the real StringIds are:

| Faction | Real StringId | Declared in |
|---|---|---|
| Rohirrim | **`vlandia`** | `spcultures.xslt` |
| Dunlendings | **`empire`** | `spcultures.xslt` |
| Haradrim | **`aserai`** | `spcultures.xslt` |
| Easterlings (Rhûn) | **`khuzait`** | `spcultures.xslt` |
| Barding (Dale) | **`sturgia`** | `spcultures.xslt` |
| Variag (Khand) | **`battania`** | `spcultures.xslt` |
| Gondor, Erebor, Mordor, … (22 more) | own id | `taom_spcultures.xml` |

There is **no** culture with id `rohan`, `dunland`, `harad`, `rhun`, `dale` or `khand`. Keying on those names is silent at every layer — no type error, no parse error, no engine warning; the lookup simply misses and the faction flies `DefaultBannerItemId`. The first cut of this feature shipped exactly that bug for all six; `ShippedBannerBearerConfigTests` now pins every key against the real culture set. See [rca-banner-bearers-2026-07-16.md](../reviews/rca-banner-bearers-2026-07-16.md).

### Banner art

Vanilla ships 45 banner items over 18 meshes (`SandBoxCore/ModuleData/items/banners.xml`). They are `culture="Culture.neutral_culture"` with `using_tableau="true"`, so **the cloth renders the party's own heraldry** regardless of which mesh family is chosen — the item id picks a pole/cloth silhouette *and* a `BannerComponent` effect tier, it does not lock the faction.

TAOM ships zero banner items of its own; custom LOTR meshes can be added later and swapped in by editing `CultureBanners`, with **no code change**.

> **The banner effect does not stack.** `GetActiveBanner` returns the component if *any* bearer is active. Extra bearers buy visual density and redundancy (the bonus survives a bearer dying) — not a bigger bonus.

> **The item choice is a balance decision.** `BannerComponent` carries `banner_level` and `effect`. All shipped mappings are `t1` (level 1) deliberately — the conservative tier.

### Race gate

`cave_troll`, `hill_troll`, `nazghul`, `saruman`, `sauron` never carry a standard. Trolls are beasts; the named races are heroes anyway (`!IsHero` already excludes them) and are listed belt-and-braces. This mirrors the cave-troll guard exclusion in [#346](https://github.com/haterade22/TAOM/issues/346).

`IsRaceAllowed` **validates before lookup**: `RaceManager.GetRaceNameFromId` coerces unknown ids to `"human"`, which is not on the exclusion list, so a lookup-first check would silently admit corrupt race ids. Invalid ids fail closed.

### Infantry-only gate

By default only troops whose `default_group` is **Infantry** become bearers. A bearer swaps its weapons for a banner + a 1H sidearm — it loses its bow, shield, or mount — so converting an archer or cavalry troop wastes that unit. `AllowedFormationGroups` (default `["Infantry"]`) controls which classes are eligible; `CanAgentBecomeBannerBearer` checks `agent.Character.DefaultFormationClass` against it (the class is set from the same `default_group` XML attribute — `BasicCharacterObject.cs:489-497`).

The guarantee is airtight because vanilla does the formation-level gating for us:

- **Pure archer/cavalry formations get zero bearers.** `CanFormationDeployBannerBearers` returns false unless ≥1 unit passes `CanAgentBecomeBannerBearer` — an all-non-infantry formation has none.
- **Mixed formations never fall back to a non-infantry bearer.** `FindBannerBearableAgents` (`BannerBearerLogic.cs:379`) filters its candidate pool by `CanAgentBecomeBannerBearer`, so an archer mixed into an infantry formation is excluded from candidacy, not merely deprioritized. A formation short on infantry gets *fewer* bearers, never an archer.

To give another class banners, add its FormationClass name (`"Cavalry"`, `"Ranged"`, …) to `AllowedFormationGroups` — its per-class ratio then applies. `FormationClass` is a fixed engine enum, so a typo is caught at load (dropped + warned); an empty or all-invalid list reverts to Infantry rather than silently disabling every banner (the master `Enabled` toggle is the real off switch).

### The unarmed-bearer trap — solved in data, not code

`GetBannerBearerReplacementWeapon` returns **null** for a culture with no `<banner_bearer_replacement_weapons>`, and `CreateBannerEquipmentForAgent` writes that null into the weapon slot and clears slots 1–3 (`BannerBearerLogic.cs:824-828`) — leaving a bearer holding a banner and **nothing else**. Vanilla never hits this because those cultures never get banners; switching banners on for every TAOM culture creates the bug.

TAOM does **not** override `GetBannerBearerReplacementWeapon`. Instead every culture declares the data, and a build-time test (`ShippedBannerBearerConfigTests.ShippedConfig_EveryBanneredCultureDeclaresReplacementWeapons`) pins the invariant. A test that fails the build beats runtime C# defending against the same gap — and keeps the GameModel free of the loop that ADR-002 forbids.

Coverage after the 2026-07-16 fix (all 28 cultures declare replacement weapons):

| Source | Cultures | Weapons |
|---|---|---|
| `spcultures.xslt` (TAOM override) | `empire` (Dunland), `aserai` (Harad), `vlandia` (Rohan), `khuzait` (Rhûn) | declared by the XSLT |
| `spcultures.xslt` (vanilla passthrough — not stripped) | `sturgia` (Dale), `battania` (Khand) | vanilla's Calradian swords — works, lore-off; a candidate future polish |
| `taom_spcultures.xml` | 14 main cultures | LOTR items |
| `taom_spcultures.xml` | 8 `is_bandit="true"` cultures | **added 2026-07-16** — each mirrors its parent culture |

The 8 bandit cultures (`dunland_raiders`, `rhun_raiders`, `harad_raiders`, `gundabad_raiders`, `umbar_corsairs`, `gondor_soldiers`, `erebor_warriors`, `mirkwood_stalkers`) had none and would have fielded unarmed bearers.

### Which culture a formation flies

`Formation.GetFirstUnit()` is **not** a culture owner — it is literally `Arrangement.GetAllUnits()[0]`, an arrangement slot. Sampling it would make a mixed-culture formation (an allied Gondor+Rohan army, a mercenary-heavy player party) fly whichever standard happened to be arranged into slot 0, and the answer could differ between deployments.

`BannerBearerAssignmentMissionLogic` instead collects every unit's culture id at the boundary and asks the service for the **majority**, with an ordinal tie-break so a 50/50 formation always resolves the same way. `GetFirstUnit()` survives only as a fallback for the case where every unit is detached (`UnitsWithoutLooseDetachedOnes` empty while `CountOfUnits > 0`). Codex review 74, MED.

## Testing

74 tests: `TAOM.Tests/Features/BannerBearers/`.

| File | Covers |
|---|---|
| `BannerBearerServiceTests.cs` (42) | Density curve (disabled, below/at minimum, scaling, engine cap, per-class ratios, negative counts); race gate (trolls/named excluded, all playable races allowed, **invalid id fails closed**, case-insensitivity, null entries); banner resolution; **majority-culture vote** (mixed formation ignores slot 0, tie is order-independent, null/empty entries); **unknown-excluded-race warning fires once**; **infantry-only gate** (Infantry allowed by default, Ranged/Cavalry/HorseArcher not, configurable, case-insensitive, empty/null/unknown handling, disabled → nothing). |
| `BannerBearerConfigProviderTests.cs` (19) | Missing file, malformed JSON, full parse, `ObjectCreationHandling.Replace` on both collections, one test per validation rule, summary-warning behaviour, `Lazy<T>` caching; **`AllowedFormationGroups` validation** (valid parse, unknown dropped + warned, all-invalid/empty/null revert to Infantry). |
| `ShippedBannerBearerConfigTests.cs` (13) | The **shipped** config parses with zero rejections; trolls excluded, ordinary races not; every banner id is a real vanilla item; **allows infantry only**. Plus the 2026-07-16 regression pins: **every culture key is a real StringId**, **no LOTR display name is used as a key**, **every bannered culture declares replacement weapons**, **the default stays empty**, **vanilla leftover cultures stay unmapped**. |

The GameModel and MissionLogic are entry points (ADR-008: not unit tested — they delegate).

Known test gap (deep-review, LOW): the config provider's upper-bound reverts (`MinimumFormationTroopCount > 100`, `*BannerPerSoldiers > 1000`) have no dedicated tests; the lower bounds do.

### In-game verification still owed

This is the feature's one real gap. Both reviews are static; the failure mode it is most exposed to — bearers appearing and standing motionless — is only observable in a live battle.

- Field battle, both sides, 2+ cultures → bearers present **and moving** (watch a bearer specifically — this is the freeze check).
- **An orc/dwarf/elf formation's bearer is still an orc/dwarf/elf.** The headline requirement.
- No troll ever raises a banner.
- Reinforcement wave → new bearers spawn, no freeze. **Post-#360:** bearers carry a 1H sidearm + banner; no `[BannerBearers] Patch63 ANOMALY` WARN in `Logs/taom_debug_*.log` (an anomaly line names the drop mechanism — either outcome is signal, record it in the RCA).
- Feature toggled OFF + a vanilla hero-captain-armed formation loses its bearer mid-battle → a replacement bearer still spawns (Patch63 vanilla-parity check, deep-review Flow-4).
- Player Order-of-Battle deploy → finish → bearers unpause with everyone else.
- Formation whose captain already has a `FormationBanner` → not double-applied.
- Sally-out + siege (the other two `BannerBearerLogic` sites).
- A bandit-culture formation → bearer **is armed**.
- A looter / `sea_raiders` warband → **no banner at all** (the fail-closed default).
- **MixedFormations enabled + banners** → bearers stand in the engine's banner positions, not scattered through the ranks (verifies the Patch30 fall-through).
- A mixed-culture allied army → each formation flies its **majority** culture's standard.
- Shader-precompile walk → no NRE, no hang.
- Master toggle off → identical to pre-feature behaviour.

## Gotchas

1. **Never call `SetFormationBanner` outside `Mission.Mode == MissionMode.Deployment.`** It freezes bearers for the entire battle and can throw `KeyNotFoundException` (`OnDeploymentFinished` clears `_initialSpawnEquipments`, which the demote branch indexes unguarded at `:757`). The guard lives in `BannerBearerAssignmentMissionLogic.OnTeamDeployed`.
2. **Every override's disabled path must be `return base.<Method>(...)`, never a computed "off" value.** The model stays registered when the feature is off and the engine still asks it about formations TAOM never touched — including ones vanilla's hero-captain path banners. Returning `0` while "disabled" *suppresses* that vanilla mechanic instead of leaving it alone. The first cut did exactly that; see [rca-banner-bearers-2026-07-16.md](../reviews/rca-banner-bearers-2026-07-16.md).
3. **`CultureBanners` keys are StringIds, not LOTR names** — Rohan is `vlandia`, Dunland is `empire`. A dead key is silent everywhere. See the table above.
4. **`SetFormationBanner` validates nothing.** Its check is a stripped `Debug.Assert`, so a typo'd or non-banner item id is accepted **silently** and then fails downstream with no diagnostic. `BannerBearerAssignmentMissionLogic` checks `BannerBearerLogic.IsBannerItem` and warns once per bad id.
5. **`DefaultBannerItemId` must stay empty.** 38 cultures are registered; 28 are mapped. A non-empty default hands a banner to the other 10 — vanilla leftovers like `looters` and `sea_raiders` that still have ~99 live references in TAOM's data. Fail closed.
6. **`Agent.IsHuman` means humanoid, not human.** Do not "fix" it to exclude custom races.
7. **Never sample one unit for a formation-wide property.** `GetFirstUnit()` is `Arrangement.GetAllUnits()[0]` — a slot, not an owner. Aggregate (majority) or use a real owner (captain/general).
8. **Keep vanilla's `CanFormationDeployBannerBearers` gate** in `GetDesiredNumberOfBannerBearersForFormation` — `SpawnBannerBearer` dereferences the controller's `BannerItem` with no null check of its own.
9. **`GetDesiredNumberOfBannerBearersForFormation` is a hot path** (called per spawned troop via `GetMissingBannerCount`, and vanilla's gate is already O(units)). Keep it allocation-free.
10. **The model instance is process-lifetime; `BannerBearerLogic` is per-mission and null outside one.** Never cache per-mission state in model fields.
11. **Bearers lose their shield.** `CreateBannerEquipmentForAgent` clears weapon slots 1–3. That is vanilla behaviour, not a TAOM bug.
12. **Don't override `GetBannerBearerReplacementWeapon`** to paper over a culture missing `banner_bearer_replacement_weapons` — add the data and let the test catch it. The loop that costs is an ADR-002 breach, and the sealed types involved make a service extraction an ADR-007 breach.
13. **This feature depends on MixedFormations' Patch30 falling through for bearers.** Patch30 blanket-suppresses vanilla `GetOrderPositionOfUnit` in field battles; without `if (unit?.Banner != null) return true;` the engine's banner slots are ignored and standards scatter. If bearer placement ever looks wrong, check that line first.
14. **Replacement weapons MUST be one-handed.** The banner rides in ExtraWeaponSlot as `HeldInOffHand + HasToBeHeldUp + DropOnWeaponChange`; every vanilla culture ships only 1H swords and the vanilla model tier-matches with **no weapon-class filter** — an undeclared engine invariant. A 2H sidearm plausibly forces a native banner drop during the reinforcement spawn's `wieldInitialWeapons`, after which the engine's unguarded slot-4 read in `SpawnBannerBearer` is a `0xC0000005` CTD (issue #360, siege of Glad Thaw). `BannerBearerReplacementWeaponDataTests` pins the invariant build-time; [rca-banner-bearers-reinforcement-av-2026-07-25.md](../reviews/rca-banner-bearers-reinforcement-av-2026-07-25.md).
15. **The engine's reinforcement path applies no per-agent bearer policy.** `MissionBattleSideSpawnContext.SpawnTroops` checks only `!IsHero` before `SpawnBannerBearer` — `CanAgentBecomeBannerBearer` (and therefore the race/formation-group gates) is deployment-only. **Patch63** reimplements `SpawnBannerBearer` with the toggle-folded gate (`IsReinforcementBearerAllowed` — disabled ⇒ vanilla parity, never suppress a vanilla-armed formation's bearers), a managed slot-4 check before the engine's unguarded native read (anomaly ⇒ WARN, not CTD), and an AV-only catch. Registry: [harmony-patch-registry.md](../reference/harmony-patch-registry.md) `Patch63_BannerBearerSpawnGuard`.

## Related

- [`docs/reference/engine/agent-spawn-and-render-pipeline.md`](../reference/engine/agent-spawn-and-render-pipeline.md) — how race reaches a spawned agent
- [`docs/features/hero-race.md`](hero-race.md) — the race system
- [`docs/features/mixed-formations.md`](mixed-formations.md) — Patch30 falls through for banner bearers so the engine can place them; removing that breaks this feature silently
- [`docs/features/banner-color-persistence.md`](banner-color-persistence.md) — clan colours on the banner cloth (`Equipment.FillFrom(SpawnEquipment, Origin?.Banner)`)
- [`docs/reviews/rca-banner-bearers-2026-07-16.md`](../reviews/rca-banner-bearers-2026-07-16.md) — both review passes; why four documented copies of the culture-id fact still shipped the bug
- [`.claude/rules/xml-data.md`](../../.claude/rules/xml-data.md) — "Config ID Cross-Reference (MANDATORY)"; its `paths:` now include ModuleData JSON because of this feature

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/dread-aura.md](./dread-aura.md)
- [docs/features/mixed-formations.md](./mixed-formations.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
