# Batch A1 verification (#122–#133)

Verified by: general-purpose agent, Phase 9a, 2026-05-13
Inputs: triage-input-batch-A1.json + wiring-matrix.md + cluster-campaign-behaviors.md
HEAD: b4b4de1 fix(messengers): wire IoC + CampaignBehavior (#121)

## Summary

| Verdict | Count |
|---|---|
| VALID | 12 |
| STALE | 0 |
| FALSE-POSITIVE | 0 |
| DUPLICATE | 0 |
| SEVERITY-DRIFT | 0 |
| **Total** | 12 |

All twelve issues reflect bugs that still exist in the code at HEAD. No interim commits have touched the affected file regions since the audit (2026-05-13) — the only commit between audit and verification is `b4b4de1` (Messengers wiring fix #121), which does not address any of the implementation-level findings in #123–#133.

## Per-issue verification table

| # | Title (short) | Verdict | Re-confirmed severity | Proposed fix scope | Depends on | Notes |
|---|---|---|---|---|---|---|
| 122 | BannerColorPersistence MobilePartyVisual patch never initialized | VALID | P2 | Add `MobilePartyVisual_AddCharacterToPartyIcon_Patch.Initialize(bannerColorService, bannerHeroAdapter)` in `SubModule.cs` init block near lines 161-180 | — | One-line mechanical fix; mirror pattern of 19 sibling Initialize calls |
| 123 | Messengers singleton-state-reset gap + mission-null leak | VALID | P1+P1+P2+P2+P3+P3 | Move `_justLoadedFromSave=false` outside gate; null-guard `_currentMission` post-AddListener; replace `ClearListeners` with `RemoveNonSerializedListener`; decouple `_dialogsRegistered` from starter gate | — | All findings still present at original line numbers |
| 124 | BannerInjection singleton stale exclusions + perf | VALID | P1+P1+P2+P2+P3 | Reset `_playerModifiedIds` on `OnNewGameCreatedEvent` OR drop null-guard; init `list=null` in SyncData; lazy-cache config provider; batch InvalidateVisuals | — | SyncData defect mechanical; config-cache pure refactor |
| 125 | CharacterCreation ADR-007 violations + IoC.Resolve in service body | VALID | P2+P2+P2+P3 | Extract IPlayerHeroAdapter / IPlayerPartyAdapter / ISettlementAdapter / ICultureCreationDataProvider; ctor-inject ICareerCreationHandler + ICareerRegistry; reset SelectedCareerStringId in OnSessionLaunched; null-guard MobileParty.MainParty | — | Confirmed `Hero.MainHero`, `MobileParty.MainParty.Position`, `Settlement.Find`, `MBObjectManager.Instance.GetObject<>` all touched directly; IoC.Resolve at lines 218 + 235 |
| 126 | InitialChildGeneration config NaN/Infinity + crash on zero-adult clan | VALID | P1+P1+P2+P2+P3+P3 | FiniteFloatValidator guard for FemaleRatio (0..1) + ChildCountMultiplier (≥0); top-of-method guard in SelectTemplate; verify index timing via ilspycmd; post-parse MinAge≤MaxAge validate | — | `SelectTemplate` fallback at line 137 indexes empty list mechanically; doubles unvalidated at lines 60-61, 79-80, 95-96 |
| 127 | NamedCompanions Review #23 regressed in Prisoner+Fugitive + singleton _spawned | VALID | P1+P1+P1+P2+P2+P3 | Add `IsHeroPrisoner`/`IsHeroFugitive` to adapter, skip in service before PlaceInSettlement; OnSessionLaunched-bound `_spawned=false`; broaden IsRecruitedOrInParty | Depends on race/companion adapters; standalone fix | Confirmed `IsRecruitedOrInParty` at lines 27-31 only checks `CompanionOf` + `PartyBelongedTo` |
| 128 | CareerSystem SyncData mutates on save + NaN config + ability cache stale | VALID | P1+P2+P2+P2+P3+P3 | Gate reconstruct on `dataStore.IsLoading`; FiniteFloatValidator in ParseFloat helper; inject ICareerAbilityService into CareerCampaignBehavior + OnSessionLaunched clear; confirmation menu for multi-eligible career switch | — | RestoreData call unconditional at line 90; ClearAll only called in OnEndMission (CareerPerkMissionBehavior.cs:269) |
| 129 | Diplomacy WarOfTheRing CurrentPhase unsaved + config validation gaps | VALID | P1+P2+P2+P3 | Persist CurrentPhase in SyncData; skip already-attained phases in CheckPhaseTransition; add `?? new WarOfTheRingConfig()` / `?? new DiplomacyConfig()`; post-deserialize semantic validation | — | SyncData empty at line 24; CurrentPhase field-init at line 16; configs deref before fallback at lines 35 |
| 130 | HeroRace _heroRaceMap singleton stale across campaigns + null-guard gaps | VALID | P1+P2+P2+P3+P3 | Add `ResetForNewCampaign()`, subscribe OnNewGameCreatedEvent; `?.Race ?? 0` in adapter get + null-guard in set; capture race=0 OR explicitly purge stale entries | Cross-feature impact: feeds 6 downstream consumers | `_heroRaceMap` field-init at line 13; CaptureHeroRaces filters >0 at line 30; adapter line 12 bare `h.CharacterObject.Race` |
| 131 | RaceAge TaomPregnancyModel ADR-007 violation + R3 + R4 | VALID | P1+P1+P2+P2+P3 | Extract GetDailyPregnancyChance(IHeroAgeInfo) to IRaceAgeService; reset _raceIdCache on new-campaign; add FiniteFloatValidator + ordering invariants; IsValidRaceId gate before GetRaceNameFromId | Adapter for IHeroAgeInfo (new) | TaomPregnancyModel.cs:18-58 is 40+ lines of inline biz logic against sealed Hero — clear gamemodels.md rule-4 violation |
| 132 | Siege empty SyncData loses all active defense events on every load + R1 | VALID | P1+P1+P2+P2+P2+P3 | Implement SyncData properly (serialize _activeEvents with re-track on load); OnSessionLaunched Reset(); remove bare catch (or log+sensible fallback); wire or delete RelationshipThreshold/ResponseWindowDays; grant reward in OnSiegeEnded too | — | SyncData literally empty at SiegeDefenseBehavior.cs:29; dead config fields confirmed unused via grep |
| 133 | SpecialResources SyncData clamps wrong cap + ScreenManager event leak + R3 | VALID | P1+P1+P1+P2+P2+P2+P3 | Remove ClampAll from SyncData (move per-resource cap into RestoreData); OnSessionEndedEvent → unsubscribe OnPushScreen; TryParse + FiniteFloatValidator + range validate; OnNewGameCreatedEvent clear singletons; first-tick-after-load grace; versioned seed flag | Composite-key parsing helper | ClampAll on line 62; OnPushScreen subscribe at line 46 has no -=; ParseFloat at line 198 uses `float.Parse` |

## Detailed per-issue verification

### #122 — BannerColorPersistence: MobilePartyVisual_AddCharacterToPartyIcon_Patch wired but never initialized

**Current code at `Main/Features/BannerColorPersistence/Hooks/MobilePartyVisual_AddCharacterToPartyIcon_Patch.cs:14-21`:**
```csharp
private static IBannerColorService? _service;
private static IBannerHeroAdapter? _heroAdapter;

public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
{
    _service = service;
    _heroAdapter = heroAdapter;
}
```

**Current code at `Main/SubModule.cs:161-180` (BannerColorPersistence init block):** 19 `Initialize` calls for sibling patches; no entry for `MobilePartyVisual_AddCharacterToPartyIcon_Patch`. The Harmony binding at `Main/SubModule.cs:445-449` IS present — patch attaches to vanilla method but its statics remain null → silent no-op Postfix.

**Grep confirms zero callers of the Initialize method:**
```
Grep "MobilePartyVisual_AddCharacterToPartyIcon_Patch" in Main → only SubModule.cs (binding at line 444-449) and the patch file itself.
```

**Audit finding:** Patch class declares Initialize(IBannerColorService, IBannerHeroAdapter) that no caller invokes → world-map party icons fall back to vanilla colors silently.

**Verdict: VALID**

**Reasoning:** No commit since the audit (2026-05-13) has touched `Main/SubModule.cs` to add the missing Initialize call. Only intervening commit is `b4b4de1` (Messengers wiring), which doesn't touch BannerColorPersistence.

**Smallest fix:** Add `MobilePartyVisual_AddCharacterToPartyIcon_Patch.Initialize(bannerColorService, bannerHeroAdapter);` in the BannerColorPersistence init block (somewhere between line 161 and 180), mirroring the 19 sibling calls. One-line fix.

---

### #123 — audit(impl): Messengers — singleton-state-reset gap + mission-null leak

**Current code at `Main/Features/Messengers/MessengerCampaignBehavior.cs:154-165`:**
```csharp
if (_lastSessionStarter != starter)
{
    _lastSessionStarter = starter;
    _dialogsRegistered = false;
    _processingArrivedMessenger = false;
    _activeMessenger = null;
    _currentMission = null;
    _originalPosition = Vec2.Invalid;
    if (!_justLoadedFromSave)
        _store.Clear();
    _justLoadedFromSave = false;
}
```

**Current code at `Main/Features/Messengers/MessengerCampaignBehavior.cs:387-389`:**
```csharp
}

_currentMission?.AddListener(this);
```
(`_currentMission` may be `null` if `OpenConversationMission` or `CreateAndOpenMissionController` returned null; null-conditional silently no-ops; `_processingArrivedMessenger=true` set at line 282 stays true forever.)

**Current code at `Main/Features/Messengers/MessengerCampaignBehavior.cs:445`:**
```csharp
CampaignEvents.TickEvent.ClearListeners(this);
```

**Audit finding:** `_justLoadedFromSave` reset trapped inside starter-change gate; `_processingArrivedMessenger` permanently stuck if mission null; `ClearListeners` removes all TickEvent listeners (fragile); `_dialogsRegistered` coupled to starter gate.

**Verdict: VALID**

**Reasoning:** All four issue locations match the audit's line numbers exactly. The Codex review #34 reset path is partially present but contains the subtle defects the audit documented.

**Smallest fix:** (P1) Move `_justLoadedFromSave = false;` outside the `if (_lastSessionStarter != starter)` block to unconditional at end of OnSessionLaunched. (P1) After `_currentMission?.AddListener(this);`, add `if (_currentMission == null) { _processingArrivedMessenger = false; _activeMessenger = null; _store.Remove(messenger.TargetHeroId); }`. (P2) Replace `ClearListeners(this)` with `RemoveNonSerializedListener(this, CleanUpSettlementEncounter)`. (P2) Track `_dialogsRegistered` independent of starter equality.

---

### #124 — audit(impl): BannerInjection — singleton stale exclusions across campaigns + perf

**Current code at `Main/Features/BannerInjection/BannerExclusionService.cs:8`:**
```csharp
public class BannerExclusionService : IBannerExclusionService
{
    private HashSet<string> _playerModifiedIds = new();
```

**Current code at `BannerExclusionService.cs:22-30`:**
```csharp
public void SyncData(IDataStore dataStore)
{
    var list = new List<string>(_playerModifiedIds);
    dataStore.SyncData("_taom_playerModifiedBanners", ref list);
    if (list != null)
    {
        _playerModifiedIds = new HashSet<string>(list);
    }
}
```

**Current code at `Main/Features/BannerInjection/BannerInjectionService.cs:62-63, 89-90`:** `_kingdomAdapter.InvalidateVisuals(kingdom.StringId)` and `_clanAdapter.InvalidateVisuals(clan.StringId)` called per-entity inside the loop.

**Current code at `Main/Features/BannerInjection/BannerConfigProvider.cs:24-44`:** `GetKingdomBannerKeys()` / `GetClanBannerKeys()` open + parse files on every call; no cache fields.

**Audit finding:** Singleton `_playerModifiedIds` carries stale exclusions across campaigns; SyncData null-guard logic wrong; per-entity InvalidateVisuals perf cost; config provider re-parses on every call.

**Verdict: VALID**

**Reasoning:** Field-init `= new()` at line 8 means a singleton holds prior-campaign state. The `list != null` guard at line 26 prevents the absent-key path from clearing the set. Both perf concerns confirmed by direct read.

**Smallest fix:** Subscribe `OnNewGameCreatedEvent` and reset `_playerModifiedIds = new()`. Alternatively, in SyncData, initialize `list = null` (not `new List(...)`) so the absent-key path becomes a no-op. Add lazy dict caches inside the config provider.

---

### #125 — audit(impl): CharacterCreation — ADR-007 violations in service + IoC.Resolve in service body

**Current code at `Main/Features/CharacterCreation/CharacterCreationContentService.cs:166-176`:**
```csharp
var heroCultureBefore = Hero.MainHero?.Culture?.StringId ?? "null";
_logger.LogInfo($"CC Finalize: SelectedCulture='{selectedCulture.StringId}', Hero.Culture before='{heroCultureBefore}'");

if (Hero.MainHero != null && Hero.MainHero.Culture?.StringId != selectedCulture.StringId)
{
    Hero.MainHero.Culture = selectedCulture;
    ...
}

TeleportToStartingSettlement(cultureData);
SetPlayerRace(cultureData, Hero.MainHero?.StringId);
```

**Current code at `CharacterCreationContentService.cs:218, 235`:**
```csharp
var handler = IoC.Resolve<CareerSystem.ICareerCreationHandler>();
...
var registry = IoC.Resolve<CareerSystem.ICareerRegistry>();
```

**Current code at `CharacterCreationContentService.cs:327-332`:**
```csharp
var settlement = Settlement.Find(cultureData.StartingSettlement);
if (settlement != null)
{
    var position = settlement.GatePosition;
    MobileParty.MainParty.Position = position.IsNonZero() ? position : settlement.Position;
```

**Current code at `CharacterCreationContentService.cs:347`:**
```csharp
return MBObjectManager.Instance?.GetObject<CultureObject>(cultureId);
```

**Current code at `Main/Features/CharacterCreation/CareerMenuService.cs:23,68,130,164`:** `SelectedCareerStringId` field with single reset inside `RegisterCareerMenu` at line 68.

**Audit finding:** Service touches sealed Hero / MobileParty / Settlement / MBObjectManager directly (ADR-007 violations); IoC.Resolve inside service body; CareerMenuService.SelectedCareerStringId not reset between sessions; MobileParty.MainParty.Position lacks null-guard.

**Verdict: VALID**

**Reasoning:** All four findings confirmed verbatim against current source. ADR-007 mandates services use adapters, not sealed types; csharp-architecture.md "no service locator in services" rule is unambiguously violated at lines 218/235.

**Smallest fix:** Extract `IPlayerHeroAdapter` (Culture get/set, IsFemale, StringId), `IPlayerPartyAdapter` (Position write), `ISettlementAdapter` (Find), `ICultureCreationDataProvider.GetCultureObject`. Constructor-inject `ICareerCreationHandler` + `ICareerRegistry`. Add `ResetSession()` on `ICareerMenuService` and subscribe `OnSessionLaunchedEvent`. Null-guard `MobileParty.MainParty` at line 331.

---

### #126 — audit(impl): InitialChildGeneration — config NaN/Infinity + crash on zero-adult clan

**Current code at `Main/Features/InitialChildGeneration/InitialChildGenerationConfigProvider.cs:58-61`:**
```csharp
config.Defaults.MinAge = defaults.Value<int?>("min_age") ?? config.Defaults.MinAge;
config.Defaults.MaxAge = defaults.Value<int?>("max_age") ?? config.Defaults.MaxAge;
config.Defaults.FemaleRatio = defaults.Value<double?>("female_ratio") ?? config.Defaults.FemaleRatio;
config.Defaults.ChildCountMultiplier = defaults.Value<double?>("child_count_multiplier") ?? config.Defaults.ChildCountMultiplier;
```
(Identical pattern at lines 76-80 culture overrides + 92-97 clan overrides. No FiniteFloatValidator anywhere. No MinAge≤MaxAge invariant.)

**Current code at `Main/Features/InitialChildGeneration/InitialChildGenerationService.cs:133-140`:**
```csharp
if (pool.Count == 0)
{
    var fallback = isFemale ? clan.AdultMaleHeroIds : clan.AdultFemaleHeroIds;
    if (fallback.Count == 0)
        return clan.AdultMaleHeroIds.Count > 0 ? clan.AdultMaleHeroIds[0] : clan.AdultFemaleHeroIds[0];
    int idx = _random.Next(0, fallback.Count);
    return fallback[idx];
}
```
(When both lists are empty: `clan.AdultMaleHeroIds.Count > 0 ? ... : clan.AdultFemaleHeroIds[0]` — the else branch indexes an empty list → `ArgumentOutOfRangeException`.)

**Current code at `Main/Features/InitialChildGeneration/TaomInitialChildGenerationBehavior.cs:22-23`:** `if (index == 0)` triggers `GenerateInitialChildren()`.

**Audit finding:** Doubles accept NaN/Infinity; SelectTemplate throws on zero-adult clan + FixedChildCount; index==0 timing risk; MinAge/MaxAge ordering uninvariated.

**Verdict: VALID**

**Reasoning:** All four findings confirmed. Particularly worth noting: `clan.AdultMaleHeroIds.Count == 0 && clan.AdultFemaleHeroIds.Count == 0` is reachable when `clanOverride.FixedChildCount > 0` is set even though the clan has no adults (config can force this).

**Smallest fix:** Use `FiniteFloatValidator.IsFiniteInRange(0.0, 1.0)` for FemaleRatio, `IsFiniteAtLeast(0.0)` for ChildCountMultiplier (warn + revert on fail). Top-of-method guard in `SelectTemplate`: `if (clan.AdultMaleHeroIds.Count == 0 && clan.AdultFemaleHeroIds.Count == 0) return null;` + propagate null check to `CreateChild` callsite. Verify `OnNewGameCreatedPartialFollowUpEvent` timing via ilspycmd; bump `index == 1` if needed. Validate `MinAge <= MaxAge` post-parse.

---

### #127 — audit(impl): NamedCompanions — Review #23 regressed in Prisoner+Fugitive states + singleton _spawned

**Current code at `Main/Features/NamedCompanions/NamedCompanionService.cs:15,33-34`:**
```csharp
private bool _spawned;
...
public void SpawnCompanions()
{
    if (_spawned) return;
    _spawned = true;
```
(Field never reset.)

**Current code at `Main/Features/NamedCompanions/NamedCompanionService.cs:70-92`:** Skip-guard chain in `EnsureCompanionsPlaced` checks `!HeroExists / !IsHeroAlive / IsRecruitedOrInParty / IsPlacedInSettlement`. No Prisoner or Fugitive check.

**Current code at `Main/Adapters/NamedCompanionAdapter.cs:27-32`:**
```csharp
public bool IsRecruitedOrInParty(string characterId)
{
    var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == characterId);
    if (hero == null) return false;
    return hero.CompanionOf != null || hero.PartyBelongedTo != null;
}
```
(Does not check `PartyBelongedToAsPrisoner`, `IsPrisoner`, `HeroState == Fugitive`. Prisoner-with-mobile-captor and fugitive heroes pass through both `IsRecruitedOrInParty` and `IsPlacedInSettlement` since their `CurrentSettlement`/`StayingInSettlement` are null.)

**Audit finding:** `_spawned` singleton not reset across campaigns; Prisoner companion bypasses guards → force-placed; Fugitive companion bypasses guards → force-placed; tests missing; IsRecruitedOrInParty doesn't check PartyBelongedToAsPrisoner.

**Verdict: VALID**

**Reasoning:** Adapter at lines 27-31 confirmed missing prisoner/fugitive checks. Service at line 79 calls `IsRecruitedOrInParty` only. The audit's Entity State Matrix rule under csharp-architecture.md flags exactly this regression class.

**Smallest fix:** Add `IsHeroPrisoner` and `IsHeroFugitive` to `INamedCompanionAdapter` (`hero.IsPrisoner || hero.PartyBelongedToAsPrisoner != null`; `hero.HeroState == Hero.CharacterStates.Fugitive`). Add `_companionAdapter.IsHeroPrisoner(...) || _companionAdapter.IsHeroFugitive(...)` skip in `EnsureCompanionsPlaced` BEFORE `PlaceInSettlement`. Add `ResetSession()` on interface, subscribe `OnSessionLaunchedEvent` from `NamedCompanionBehavior`. Add corresponding skip-guard tests.

---

### #128 — audit(impl): CareerSystem — SyncData mutates on save + NaN config + ability cache stale

**Current code at `Main/Features/CareerSystem/CareerPersistenceBehavior.cs:24-92`:** SyncData body contains save-side flattening to dicts AND load-side reconstruction. The reconstruction block at lines 51-91 runs UNCONDITIONALLY — there is no `if (dataStore.IsLoading)` gate. At line 90: `_dataService.RestoreData(restored);`.

**Current code at `Main/Features/CareerSystem/CareerConfigProvider.cs:427-433`:**
```csharp
private static float ParseFloat(XElement el, string attrName, float defaultValue)
{
    var val = el.Attribute(attrName)?.Value;
    if (val == null) return defaultValue;
    return float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
        ? result : defaultValue;
}
```
(No finiteness check — `float.TryParse("NaN", ...)` returns `true` with NaN result.)

**Current code at `Main/Features/CareerSystem/Abilities/CareerAbilityService.cs:11`:**
```csharp
private readonly Dictionary<string, CareerAbility> _abilities = new Dictionary<string, CareerAbility>();
```
Method `ClearAll()` exists at line 80, called from `CareerPerkMissionBehavior.cs:269` inside `OnEndMission` only.

**Current code at `Main/Features/CareerSystem/CareerSwitchDialogueBehavior.cs:73-86`:**
```csharp
foreach (var career in allCareers)
{
    if (career.Id == currentCareerId) continue;
    if (_switchService.CanSwitch(adapter, career.Id))
    {
        _pendingNewCareerId = career.Id;
        return true;
    }
}
```
(Breaks on first eligible career; no menu to confirm.)

**Audit finding:** `RestoreData` runs unconditionally on save; `ParseFloat` no NaN guard; `_abilities` cleared only in OnEndMission; CareerSwitch implicit (no confirmation).

**Verdict: VALID**

**Reasoning:** All four findings confirmed verbatim. The NaN bug class shipped 3× already per `feedback_clamp_nan_infinity_propagates.md`.

**Smallest fix:** Gate the reconstruct block on `if (dataStore.IsLoading)` at line 50. Replace `ParseFloat` body with `FiniteFloatValidator.IsFinite(result) ? result : defaultValue`. Inject `ICareerAbilityService` into `CareerCampaignBehavior`, call `ClearAll()` in OnSessionLaunched. Either present a confirmation inquiry listing the eligible target career's DisplayName, OR restrict the switch to the single-eligible-career case.

---

### #129 — audit(impl): Diplomacy — WarOfTheRing.CurrentPhase unsaved + config validation gaps

**Current code at `Main/Features/Diplomacy/WarOfTheRingService.cs:16`:**
```csharp
public WarPhase CurrentPhase { get; private set; } = WarPhase.Peace;
```

**Current code at `Main/Features/Diplomacy/WarOfTheRingBehavior.cs:24`:**
```csharp
public override void SyncData(IDataStore dataStore) { }
```

**Current code at `WarOfTheRingService.cs:42-57`:**
```csharp
if (CurrentPhase == WarPhase.Peace && elapsedDays >= phase1Day)
{
    TransitionToPhase(WarPhase.IsengardWar);
}

if (CurrentPhase == WarPhase.IsengardWar && elapsedDays >= phase2Day)
{
    TransitionToPhase(WarPhase.FullWar);
}
```
(Two sequential non-nested `if` blocks: when starting in Peace past phase2Day, BOTH transitions fire on same call — currently idempotent but a latent design fault.)

**Current code at `Main/Features/Diplomacy/WarOfTheRingConfigProvider.cs:34-35`:**
```csharp
var config = JsonConvert.DeserializeObject<WarOfTheRingConfig>(json);
_logger.LogInfo($"Loaded War of the Ring config: Phase1 day {config.Phase1.TriggerDay}, Phase2 day {config.Phase2.TriggerDay}");
```
(No `?? new WarOfTheRingConfig()` fallback; would NRE if JSON literal is null.)

**Current code at `Main/Features/Diplomacy/DiplomacyConfigProvider.cs:34-35`:** Identical issue — `JsonConvert.DeserializeObject<DiplomacyConfig>(json)` then immediate `config.Relationships.Count` dereference.

**Audit finding:** CurrentPhase unserialized → re-derived destructively; config providers lack `?? new T()` fallback; no semantic validation.

**Verdict: VALID**

**Reasoning:** All findings confirmed. The latency-only nature of the CurrentPhase issue is correctly reflected by P1 severity per the audit's design-fault framing — any future non-idempotent side effect will replay on every load.

**Smallest fix:** Persist `CurrentPhase` in `WarOfTheRingBehavior.SyncData` (round-trip as int or enum-as-int). In `CheckPhaseTransition`, distinguish "advancing TO this phase" from "already at this phase or later" — skip TransitionToPhase for already-attained phases. Append `?? new WarOfTheRingConfig()` and `?? new DiplomacyConfig()`. Add post-deserialize validation: `phase1Day >= 1`, `phase2Day > phase1Day`, non-empty attacker/defender strings.

---

### #130 — audit(impl): HeroRace — _heroRaceMap singleton stale across campaigns + null-guard gaps

**Current code at `Main/Features/HeroRace/RacePersistenceService.cs:13`:**
```csharp
private Dictionary<string, int> _heroRaceMap = new();
```
(Field-initialized once; survives Singleton lifetime across multiple campaigns.)

**Current code at `RacePersistenceService.cs:23-35`:**
```csharp
public void CaptureHeroRaces()
{
    _heroRaceMap = new Dictionary<string, int>();
    ...
    foreach (var hero in heroes)
    {
        if (hero.Race > 0 && !_heroRaceMap.ContainsKey(hero.StringId))
        {
            _heroRaceMap[hero.StringId] = hero.Race;
        }
    }
}
```
(Race-0 humans excluded — asymmetric: hero deliberately reset to 0 retains stale non-human race from prior campaign.)

**Current code at `Main/Adapters/HeroRosterAdapter.cs:11-13, 22-29`:**
```csharp
return Hero.AllAliveHeroes
    .Select(h => new HeroRaceInfo(h.StringId, h.CharacterObject.Race))
    .ToList();
...
public void SetHeroRace(string heroStringId, int race)
{
    var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == heroStringId);
    if (hero != null)
    {
        hero.CharacterObject.Race = race;
    }
}
```
(Line 12: bare `h.CharacterObject.Race` — `CharacterObject` is computed and may be null. Line 27: same pattern in setter.)

**Audit finding:** `_heroRaceMap` singleton not reset; `?.` missing on computed property; race-0 excluded asymmetrically.

**Verdict: VALID**

**Reasoning:** Singleton lifetime confirmed via Reuse.Singleton registration referenced in audit + field-init at line 13. Adapter null-guard violation per `adapters.md` rule confirmed. HeroRace feeds 6 downstream consumers (Patch3_SetRace, Patch5_FaceGen, Patch9_RaceFilter, Patch29_CCBodyProperties, RaceAge, NamedCompanions) — cross-feature blast radius justifies P1.

**Smallest fix:** Add `public void ResetForNewCampaign() { _heroRaceMap = new(); }`, subscribe `OnNewGameCreatedEvent` from `RacePersistenceBehavior` to invoke. Adapter: `h.CharacterObject?.Race ?? 0` in get; `if (hero?.CharacterObject != null)` in set. Drop the `hero.Race > 0` filter in CaptureHeroRaces OR explicitly remove stale entries for heroes whose current race is 0.

---

### #131 — audit(impl): RaceAge — TaomPregnancyModel ADR-007 violation + singleton race cache stale + R3+R4

**Current code at `Main/Features/RaceAge/Models/TaomPregnancyModel.cs:18-59`:** 40+ lines of inline business logic:
```csharp
public override float GetDailyChanceOfPregnancyForHero(Hero hero)
{
    var race = hero.CharacterObject.Race;
    if (_raceAgeService.IsImmortal(race)) return 0f;
    if (hero.Spouse == null) return 0f;
    int comesOfAge = _raceAgeService.GetComesOfAge(race);
    int fertilityEnd = _raceAgeService.GetFertilityEndAge(race);
    if (hero.Age < comesOfAge || hero.Age > fertilityEnd) return 0f;
    int fertilityWindow = fertilityEnd - comesOfAge;
    float declineRate = fertilityWindow > 0 ? 1.08f / fertilityWindow : 0.04f;
    float ageFactor = 1.2f - (hero.Age - comesOfAge) * declineRate;
    int childCount = hero.Children.Count + 1;
    float clanCap = 4 + 4 * hero.Clan.Tier;
    int aliveLords = hero.Clan.AliveLords.Count;
    float populationFactor = ...
    float baseChance = ageFactor / (childCount * childCount) * 0.12f * populationFactor;
    baseChance *= _raceAgeService.GetFertilityModifier(race);
    var result = new ExplainedNumber(baseChance);
    if (hero.GetPerkValue(...) || hero.Spouse.GetPerkValue(...)) { result.AddFactor(...); }
    return result.ResultNumber;
}
```
(Direct sealed `Hero` access, `Math.Min`, `ExplainedNumber`, multi-line computation — gamemodels.md rule 4 explicitly forbids this.)

**Current code at `Main/Features/RaceAge/RaceAgeService.cs:11`:**
```csharp
private readonly Dictionary<int, RaceAgeEntry> _raceIdCache = new Dictionary<int, RaceAgeEntry>();
```

**Current code at `RaceAgeService.cs:42-54`:**
```csharp
private RaceAgeEntry GetEntry(int raceId)
{
    if (_raceIdCache.TryGetValue(raceId, out var cached))
        return cached;
    var raceName = _raceManager.GetRaceNameFromId(raceId);
    var entry = (raceName != null && _races.TryGetValue(raceName, out var found))
        ? found
        : _defaultEntry;
    _raceIdCache[raceId] = entry;
    return entry;
}
```
(No `IsValidRaceId(raceId)` validation — `GetRaceNameFromId` returns "human" fallback for unknown IDs per RaceManager.cs:126-130. Validate-before-lookup pattern violated per `feedback_validate_before_lookup_with_fallback.md`.)

**Current code at `Main/Features/RaceAge/RaceAgeConfigProvider.cs:34`:**
```csharp
var config = JsonConvert.DeserializeObject<RaceAgeConfig>(json);
```
(No semantic validation; `FertilityMod` accepts NaN; age fields have no ordering invariants.)

**Audit finding:** Inline business logic in TaomPregnancyModel (ADR-007 + GameModel rule 4); _raceIdCache singleton not reset; missing validate-before-lookup; missing semantic config validation.

**Verdict: VALID**

**Reasoning:** Three recurring patterns (R1 + R3 + R4) confirmed in one feature. The pregnancy model is the clearest GameModel rule 4 violation in the batch — 40+ lines of inline business logic on sealed Hero.

**Smallest fix:** Extract `float GetDailyPregnancyChance(IHeroAgeInfo)` on `IRaceAgeService`; convert `Hero → IHeroAgeInfo` at GameModel boundary; the override body becomes one line of delegation. Cross-reference `cluster-gamemodels.md` adds Phase-2 hero.Spouse/hero.Clan null-safety findings to fold into the same fix. Promote `_raceIdCache` to per-call local OR add reset on new-game. Validate via `_raceManager.IsValidRaceId(raceId)` before `GetRaceNameFromId`. Add `FiniteFloatValidator` for `FertilityMod` and ordering invariants for ages.

---

### #132 — audit(impl): Siege — empty SyncData loses all active defense events on every load + R1

**Current code at `Main/Features/Siege/SiegeDefenseBehavior.cs:29`:**
```csharp
public override void SyncData(IDataStore dataStore) { }
```

**Current code at `Main/Features/Siege/SiegeDefenseService.cs:30`:**
```csharp
private readonly Dictionary<string, ActiveSiegeDefenseEvent> _activeEvents = new Dictionary<string, ActiveSiegeDefenseEvent>();
```

**Current code at `SiegeDefenseService.cs:92-100`:**
```csharp
CampaignTime deadline;
try
{
    deadline = CampaignTime.DaysFromNow(_settings.SiegeDefenseResponseDays);
}
catch
{
    deadline = default;
}
```
(Bare catch swallows silently; `default(CampaignTime)` is campaign epoch — already in the past — `IsPast` immediately fires on next OnHourlyTick.)

**Current code at `Main/Features/Siege/Models/SiegeDefenseConfig.cs:9-10`:**
```csharp
public int RelationshipThreshold { get; set; } = -20;
public int ResponseWindowDays { get; set; } = 3;
```
(Grep confirms zero callsites for either across `Main/`.)

**Audit finding:** Empty SyncData → all active siege events lost on save-load (most user-visible bug in cluster); _activeEvents singleton not reset; bare catch silently swallows + sets deadline to epoch; dead config fields; reward delivery race.

**Verdict: VALID**

**Reasoning:** SyncData literally empty. Field-init `_activeEvents` survives across campaigns. Dead-field analysis confirmed via global grep.

**Smallest fix:** Implement SyncData proper round-trip on `_activeEvents` (serialize tuple `(SettlementId, DefenderFactionId, Deadline, PlayerAccepted, RewardClaimed)` as primitive lists keyed by SettlementId; reconstruct on load + re-register VisualTracker for `PlayerAccepted && !RewardClaimed` entries). Add `OnSessionLaunched` reset. Remove bare try/catch — let exception propagate OR fall back to `CampaignTime.DaysFromNow(3)` with explicit log. Decide: wire `RelationshipThreshold` + `ResponseWindowDays` (preferred — fallback for MCM) or delete (Simplicity Criterion). Grant reward in `OnSiegeEnded` if `PlayerAccepted && !RewardClaimed && playerAtSettlement`.

---

### #133 — audit(impl): SpecialResources — SyncData clamps wrong cap + ScreenManager event leak + R3

**Current code at `Main/Features/SpecialResources/SpecialResourcesBehavior.cs:46`:**
```csharp
ScreenManager.OnPushScreen += OnScreenPushed;
```
(No corresponding `-=` anywhere in the file.)

**Current code at `SpecialResourcesBehavior.cs:49-65`:**
```csharp
public override void SyncData(IDataStore dataStore)
{
    _logger.LogInfo("[SpecRes] SyncData called (save/load)");
    var data = _storage.GetAllData();
    dataStore.SyncData("_taom_specialResources", ref data);
    _storage.RestoreData(data);
    _logger.LogInfo($"[SpecRes] SyncData restored {data?.Count ?? 0} entries");

    var hero = Hero.MainHero;
    GetHeroIds(hero, out var kingdomId, out var cultureId);
    var resource = _service.ResolveResource(kingdomId, cultureId);
    if (resource != null)
    {
        _storage.ClampAll(resource.Cap);
        _logger.LogInfo($"[SpecRes] SyncData clamped all values to cap={resource.Cap}");
    }
}
```
(ClampAll uses the PLAYER's resource cap, applied to ALL keys regardless of resource type. Plus mutation runs on both save AND load.)

**Current code at `Main/Features/SpecialResources/SpecialResourceConfigProvider.cs:195-199`:**
```csharp
private static float ParseFloat(XElement el, string attr, float defaultValue)
{
    var val = el.Attribute(attr)?.Value;
    return val != null ? float.Parse(val, CultureInfo.InvariantCulture) : defaultValue;
}
```
(`float.Parse` throws on malformed; `float.Parse("NaN", invariant)` returns NaN. No `TryParse`; no `FiniteFloatValidator`.)

**Audit finding:** SyncData ClampAll uses wrong cap (corrupts multi-resource saves); ScreenManager event leak across campaigns; ParseFloat throws on malformed + accepts NaN; singleton fields not reset; desertion fires on first DailyTick after load; legacy seed fires on every kingdom change.

**Verdict: VALID**

**Reasoning:** Four-pattern cluster (R1+R2+R3+R5) confirmed. The ClampAll → wrong-cap corruption is mechanically certain since the SyncData hero's cap is applied indiscriminately across all keys (gems/wine/etc. heroes get wrong cap). ScreenManager static event leak across multi-campaign sessions is a known TaleWorlds pitfall.

**Smallest fix:** Remove `ClampAll` from SyncData; instead, inside `RestoreData`, parse each composite key `heroId:resourceId`, look up that specific resource's cap, apply per-key clamp. Subscribe `CampaignEvents.OnSessionEndedEvent` in `RegisterEvents`; in handler `ScreenManager.OnPushScreen -= OnScreenPushed`. Replace `float.Parse` with `float.TryParse` + `FiniteFloatValidator.IsFinite` + range validate (cap>0, rates≥0). Subscribe to existing `OnNewGameCreatedEvent` callback path to reset `_loggedResolveKeys` + `_pendingSpend` + `_inSession`. Add `_isFirstTickAfterLoad` flag set in SyncData(IsLoading), cleared at end of first OnDailyTickHero, used to gate desertion. Gate seeding on versioned SyncData flag.

---

## Anything that surprised you

Two items worth flagging for Phase 9 but **NOT** filed as separate findings (per "no new findings" hard constraint):

1. **#125 has an additional ADR-007 violation the audit didn't enumerate.** `CharacterCreationContentService.GrantPlayerStartupResources` (lines 181-209) reads `Hero.MainHero` and `Hero.MainHero.IsFemale` directly — same class as the audit's documented violations. Already covered by the audit's general extract-`IPlayerHeroAdapter` recommendation, so the fix scope doesn't change; just calling it out so the IPlayerHeroAdapter surface should include `IsFemale`.

2. **#131 R4 (validate-before-lookup) trap at `RaceAgeService.GetEntry` is more dangerous than the audit framed.** The "human" fallback also gets cached in `_raceIdCache[raceId]` at line 52 — so a single invalid raceId poisons the cache for the rest of the singleton's life. Reset is needed AND validation. The audit notes the cache + the lookup gap but doesn't connect them; the fix should be paired.

Neither item changes any verdict above. Both stay attached to their existing issue's fix scope.
