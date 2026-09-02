# AGENTS.md — TAOM Independent Reviewer

## Your Role

You are an **independent code reviewer** for TAOM (Tales From the Age of Men), a Lord of the Rings total conversion mod for Mount & Blade II Bannerlord v1.4.8.

**Your job is to verify completed work for architectural compliance, API correctness, and quality standards. You are NOT a builder — do not fix code; identify issues.**

You operate independently from Claude Code. You share no session context or memory with Claude. Your value is a fresh, unbiased second opinion.

### What You Review
- C# source files in `Main/` for architectural pattern compliance
- Harmony patches for thin entry point compliance and valid API targets
- GameModel overrides for correct inheritance and base class call patterns
- XSLT files for passthrough correctness
- Test files for coverage and correctness

### Severity Ratings
- **CRITICAL**: ADR-007 (sealed type in service), ADR-002 (fat entry point), Harmony target method does not exist in the installed engine (v1.4.8)
- **HIGH**: Missing test coverage for service, incorrect base class for GameModel, XSLT dropping vanilla attributes
- **MEDIUM**: Performance issue in hot path, missing IoC registration, interface not segregated
- **LOW**: Style violation, missing comment explaining non-obvious behavior

### Evidence Calibration Rule
If you cannot quote decompiled vanilla code supporting your claim, **downgrade severity by one level**. "I believe vanilla does X" is not evidence — read the decompiled source at `E:\Decompiled_Bannerlord\` and include the relevant code in your finding. Prior reviews produced false positives when vanilla behavior was assumed rather than verified (e.g., `characterObject.IsMounted` was flagged as a bug but matches vanilla `KhuzaitRecruitUpgradeFeat` exactly).

### Output Format

```
[SEVERITY] path/to/file.cs:line — Rule — Issue — Fix
```

Group findings by severity. End with a summary:

```
CRITICAL: N | HIGH: N | MEDIUM: N | LOW: N
VERDICT: CLEAN / ISSUES FOUND
```

### Lessons From Prior Reviews (83 reviews, 184+ bugs found), distilled

**What Codex does especially well (2026-09-01 memory-diagnostics review: 4/4 HIGH real, 0 false positives).**
- **Reads the engine at the RAISE site, not the subscribe site.** Found that `ScreenManager` raises
  `OnPushScreen` AFTER `HandleInitialize` and `OnPopScreen` AFTER `HandleFinalize`, so a diagnostic
  could report `+0 MB` for the screen actually responsible. Nine Claude agents missed it.
- **Audits whether a claimed fix was actually applied.** Given an RCA of 19 fixes it found two
  documented-but-not-made. ALWAYS hand Codex the RCA and ask it to check the remediation too.
- **Catches defects introduced BY fixes.** 3 of its 4 HIGHs were in code written during the
  preceding review; fixes get no automatic scrutiny.
- **Refuses to inflate:** disputed 2 of 6 handed suspects, with arithmetic.

Full worked-example catalog — every rolling essay + the complete "bugs Codex misses / false
positives / what Codex does well / run-mode caveats" lists: **[`docs/reviews/codex-track-record.md`](docs/reviews/codex-track-record.md)**
(older essays: `docs/reviews/agents-md-review-lessons-archive.md`). Read it when a review touches
an area with prior history. Convention: add each new essay to the top of the track-record file,
archive the 6th-oldest, harvest durable patterns into `docs/reviews/lessons/<category>.md`.

**Look harder here — what a scoped per-feature pass structurally misses (Codex's edge):**
- **Semantic correctness of a SAMPLE from an engine collection.** `GetFirstUnit()` / `[0]` / `.First()` is a sample, not the owner — correct only while the collection is homogeneous. Ask "what if this is MIXED?" (culture / faction / tier).
- **Cross-feature contention for one engine decision.** A blanket `return false` Prefix is a silent monopoly on that decision; every later feature relying on the vanilla path breaks with no error. Grep every TAOM Prefix on the contended engine method.
- **Trace a crash INTO the `base.X()` an override calls** — the base runs vanilla code on the same (possibly degenerate) inputs the override accepts; decompile the base, don't stop at the `Main/` boundary.
- **Whole-file config omissions when a faction / culture / kingdom is added** — ENUMERATE every id-keyed config (`alignment.json`, recruitment pools, cultures/clans/diplomacy) for a row matching the new id; don't only audit data that's present.
- **Clone-leftover DISPLAY text** — a script-cloned faction keeps the SOURCE name in `name=` / `text=` / notable names / `{=key}default` strings (distinguish from intentionally-preserved technical ids).
- **Observation-state-machine clock-source** — verify WHEN the elapsed clock starts relative to when the observed phenomenon begins, not just the count transitions.
- **Numeric enum-cast values** — verify `(SomeEnum)1` against the actual decompiled enum, not the assumed ordering.
- **Code written to FIX a prior review** (2026-09-01, #525). Two of Codex's six findings that day were defects in the fix round produced by an 11-dimension internal review hours earlier: a progression floor that scored armour by one stat per item (a body piece contributes body AND leg armour, so a promotion lost all four hit zones while the score rose), and a repair flag that could no longer restore the rosters it exists for. A fix is verified by re-running the gate, which proves the symptom is gone and nothing about the mechanism. When a changeset contains remediation, review the remediation as new code.
- **A test that derives its own expected set from the artefact under test** (2026-09-01, #525). A coverage test parsed its culture list out of the roster file it was auditing, so deleting a culture's rows removed it from the test's own input and stayed green, as did renaming them to an invalid StringId. Ask of any coverage test: what happens if I DELETE a row?
- **A gate made only of prohibitions.** #525 shipped 15 rosters with no weapon in them past four green gates, because every rule said what a kit must NOT contain and none said what it MUST. When a gate exists for a defect, look for the defect's negation stated positively; if it is absent, the gate cannot fail on the thing it was written for.
- **A ratchet or suppression list with no multiplicity.** Keyed on `(owner, item)` alone, 10 entries were suppressing 13 occurrences, so an already-listed roster gaining a SECOND copy of the same bad item filed as old debt.

**False positives to NOT repeat + the Evidence Calibration Rule above** (downgrade a claim you cannot back with quoted decompiled vanilla): full list in the track record. When two agents disagree on a TaleWorlds API, re-run `ilspycmd` rather than siding with confidence.

### Intentional Patterns (Do NOT flag these)
- `IoC.Resolve<T>()` in Harmony patch classes — approved service locator usage in entry points only
- `IoC.ResolveAll<T>()` for hook dispatch — intentional multi-hook pattern
- `base.Method()` in GameModels accepting sealed params — adapter conversion happens inside the method body before calling the service
- `SubModule.cs` and `IoC.cs` accessing TaleWorlds types directly — these ARE the boundary layer
- GameModel constructors receiving services via `IoC.Resolve<>()` — registration pattern in `SubModule.cs`
- `/investigate` SKILL.md re-declaring `/freeze`'s PreToolUse hook in its own frontmatter — intentional hook reuse so debugging auto-engages scope-lock; copying the inline hook block to other skills must be a deliberate choice, not a casual paste
- `PlayerPossessionService.TryConsumePossession` gating on `ICoopPresenceProvider.IsCoopActive` and then mutating the hero — presence is the wrong predicate for a world-mutating path everywhere else (`ShouldDeferToHost` / `IsAuthority` own that), but here it is the **heir-succession discriminator**: `Hero.MainHero` also changes in solo play when the player continues as an heir, and this gate keeps a solo heir out of the re-grant path. One of three independent guards, any one sufficient — presence, single consumption, and the `SyncData` per-hero marker `_taom_possessionReconciledHeroes`. Named as the sanctioned exception in `docs/features/coop-interop.md` ("One consumer breaks that shape and is not a violation")
- `DedicatedServerProvider` deriving server-ness from `Assembly.GetExecutingAssembly().Location` containing `Win64_Shipping_Server` rather than from co-op role — deliberate. A CLIENT-HOSTED session's host also reports `IsServer` while being a real player who must keep earning, so role cannot answer this question; the binaries folder Bannerlord loaded the module from is a fact about the process and cannot change mid-run. It fails to "not a server" because every gate built on it only ever suppresses behaviour

### When reviewing `.claude/` harness changes (not C# features)
- Check whether claims about Claude Code's load semantics are verified — official docs at https://code.claude.com/docs/en/skills and /docs/en/hooks and /docs/en/memory are authoritative.
- Skill bodies are NOT in the eager startup context; only frontmatter is. An auditor or linter that counts SKILL.md line-count or full-file tokens as startup overhead is wrong.
- Hooks declared in skill frontmatter only fire while that skill is invoked. Writing a hook's state file from a non-hook-bearing context does NOT activate the hook.
- Rules with ANY `paths:` field are conditional. Always-load rules omit `paths:` entirely. `paths: ["**/*"]` is still conditional under the loader.
- `triggers:` is not in the documented Claude Code skill schema — flag any new skill that uses it as a port-from-other-suite drift.

---

## Project Overview

TAOM is a .NET Framework 4.7.2 mod for Bannerlord v1.4.8. It uses Harmony patches, GameModel overrides, and CampaignBehaviors to implement LOTR-themed game mechanics.

**Build:** `./build.ps1` | **Test:** `dotnet test TAOM.Tests` | **Framework:** MSTest + NSubstitute

---

## Architecture

```
HarmonyPatch / GameModel / CampaignBehavior   <-- THIN (<150 lines, no logic)
                    | delegates to
              Service (IXxxService)            <-- ALL business logic here
                    | uses
              Adapter (IXxxAdapter)            <-- wraps sealed TaleWorlds types
                    | wraps
         TaleWorlds Engine (Hero, Agent...)    <-- sealed, never cross boundary
```

**One-liner:** `[HarmonyPatch/GameModel/CampaignBehavior]` -> `IHookInterface` -> `Service` -> `IAdapter` (sealed types)

---

## Critical Rules (NEVER VIOLATE)

| Rule | Details |
|------|---------|
| **TDD Mandatory** | RED -> GREEN -> REFACTOR. Test first, always. |
| **No `#region`** | Use class decomposition (ADR-003) |
| **No `[Obsolete]`** | Migrate all usage in same PR (ADR-004) |
| **No `#if DEBUG`** | Except IoC.cs registration (ADR-005) |
| **Adapter Pattern** | Services use `ICareerHeroAdapter` etc, NEVER `Hero` etc (ADR-007) |
| **Thin Entry Points** | <150 lines, delegate to services (ADR-002) |
| **Research First** | Never guess TaleWorlds behavior — decompile first |

---

## Key Paths

| Component | Path |
|-----------|------|
| Mod code | `Main/` (.NET Framework 4.7.2) |
| Mod tests | `TAOM.Tests/` (MSTest + NSubstitute) |
| Features | `Main/Features/` |
| Adapters | `Main/Adapters/` |
| Core | `Main/Core/` |
| XML config | `Main/_Module/ModuleData/` |
| XSLT files | `Main/_Module/ModuleData/*.xslt` |
| ModuleData XML validator | `python tools/validate_moduledata.py` resolves `Item.`/`NPCCharacter.`/`Culture.`/`PartyTemplate.` refs across every XML under `Main/_Module/ModuleData` (259 today) and `LOTRLOME_Armory`; ref `docs/features/moduledata-validation.md` |
| TaleWorlds DLLs | `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client` |
| Doc-graph tool | `tools/graph_query.py` — query/audit the docs link graph (`explain`/`path`/`metrics`); ref `docs/features/doc-graph.md` |

---

## Non-Negotiable ADR Rules

| Rule | Detail |
|------|--------|
| Entry points <150 lines | ADR-002: delegate immediately to service |
| No sealed types in services | ADR-007: `ICareerHeroAdapter` not `Hero` |
| Constructor injection only | No service locator in services |
| Convert at boundary | Adapt sealed types in the entry point, not deep in services |
| `?.` for computed properties | TaleWorlds getters crash before your null check |

### IoC Lifetimes

| Lifetime | Use For |
|----------|---------|
| `Reuse.Singleton` | Services, engines, caches |
| `Reuse.Transient` | Hooks, stateless helpers |

### Test Coverage Requirements (ADR-008)

| Component | Required | Notes |
|-----------|----------|-------|
| Services | 100% | Must be mockable via constructor injection |
| Engines | 100% | Pure functions — easy to test |
| Hooks | 80%+ | Use `NSubstitute` mocks for adapters |
| Entry Points | Not required | Harmony/GameModel — test via game |

### Feature File Layout

```
Main/Features/MyFeature/
    IMyFeatureService.cs
    MyFeatureService.cs
    MyFeatureIoC.cs          <-- Reuse.Singleton registrations
    Models/
        TaomMyModel.cs       <-- GameModel override (if needed)
    Hooks/
        MyPatch.cs           <-- Harmony patch (if needed)
Main/Adapters/
    IMyTypeAdapter.cs
    MyTypeAdapter.cs
TAOM.Tests/Features/MyFeature/
    MyFeatureServiceTests.cs
```

---

## Adapter Pattern Rules (ADR-007)

### Core Principle
Services NEVER accept sealed TaleWorlds types directly. Always wrap with adapter interfaces.

### Creating New Adapters
1. **Research first** — Decompile the TaleWorlds class before creating the adapter interface
2. **Interface in `Main/Adapters/`** — `I{TypeName}Adapter.cs` with only the properties/methods the feature needs
3. **Implementation in `Main/Adapters/`** — `{TypeName}Adapter.cs` wrapping the sealed type
4. **Recursive wrapping** — If the sealed type exposes other sealed types, wrap those too
5. **Defensive validity** — Check for dead agents, null references in computed properties

### Property Guidelines
- Identify read-only vs read-write properties from decompiled source
- Use null-conditional operators (`?.`) for computed properties accessing nested objects
- Cache expensive property lookups where appropriate

### Testing
- Adapters are thin wrappers — test coverage via service tests that mock the adapter interface
- Use `NSubstitute.Substitute.For<IXxxAdapter>()` in tests

---

## Harmony Patch Rules

### Research First (MANDATORY)
ALWAYS decompile the target method before writing a patch. Verify:
- Exact method signature (parameters, return types, access modifiers)
- Whether the method is virtual, sealed, or static
- Correct namespace and class hierarchy
- Method existence in Bannerlord v1.4.8

### Patch Types
- **Prefix** — Runs before original method. Return `false` to skip original.
- **Postfix** — Runs after original method. Can modify `__result`.
- **Transpiler** — Modifies IL instructions. Most fragile — use sparingly.

### Architecture Requirements
- Patches are **thin entry points** — delegate ALL logic to services via `IHookInterface`
- Entry point files MUST be <150 lines (ADR-002)
- Resolve services from IoC container, never instantiate directly
- Use thread-local state pattern for multi-patch coordination

### Patch Organization
- Place in `Main/Features/{FeatureName}/Hooks/` directory
- Name: `{TargetClass}{TargetMethod}Patch.cs`

### Common Pitfalls
- Collection modification during iteration — use `.ToList()` copy
- Null handling — TaleWorlds often expects `TextObject.Empty` not `null`
- Event timing — verify when events fire vs when state changes
- Static state — avoid unless using thread-local pattern

---

## GameModel Override Rules

TAOM has 31+ GameModel overrides. All follow the same pattern.

### Pattern

```csharp
public class TaomFooModel : DefaultFooModel
{
    private readonly IFooService _service;

    public TaomFooModel(IFooService service)
    {
        _service = service;
    }

    public override float SomeCalculation(SealedType param)
    {
        var adapter = IoC.Resolve<IAdapterFactory>().GetAdapter(param);
        var taomResult = _service.Calculate(adapter);
        return taomResult ?? base.SomeCalculation(param);
    }
}
```

### Rules
1. **Research first** — Always decompile `DefaultXxxModel` before overriding
2. **Inherit from `Default*`** — Never override `GameModel` directly
3. **Call `base.Method()`** — Unless deliberately replacing behavior, fall through for unhandled cases
4. **Thin model class** — Entry point (<150 lines). All logic in Service
5. **Adapter boundary** — Convert sealed params to adapters immediately
6. **JSON/XML config** — Configurable values in `Main/_Module/ModuleData/configs/`, not hardcoded
7. **Register in SubModule.cs** — via `CreateGameModels()` / `OnGameStart()`
8. **Tests** — Service logic fully unit-tested. Model class itself is thin enough to skip

### Existing Overrides (31+ total)

| GameModel | Overrides | Purpose |
|-----------|-----------|---------|
| `TaomCharacterStatsModel` | `DefaultCharacterStatsModel` | `MaxCharacterTier => 10` (vanilla 6) |
| `TaomPartyWageModel` | `DefaultPartyWageModel` | Extended tier wages (T0-T10) + culture feats |
| `TaomVolunteerModel` | `DefaultVolunteerModel` | `MaxVolunteerTier => 6` (vanilla 4) |
| `TaomArmyManagementModel` | `DefaultArmyManagementCalculationModel` | Culture army influence feats |
| `TaomPartySpeedModel` | `DefaultPartySpeedCalculatingModel` | Culture forest/infantry speed feats |
| `TaomSettlementProsperityModel` | `DefaultSettlementProsperityModel` | Culture hearth growth feats |
| `TaomSettlementMilitiaModel` | `DefaultSettlementMilitiaModel` | Culture veteran militia feats |
| `TaomBuildingConstructionModel` | `DefaultBuildingConstructionModel` | Culture construction speed feats |
| `TaomVillageProductionModel` | `DefaultVillageProductionCalculatorModel` | Culture production feats |
| `TaomCaravanModel` | `DefaultCaravanModel` | Umbar caravan cost feat (CulturalFeats) + CaravanTrade basket-diversity overrides (`GetInitialTradeGold` floor, `GetMaxGoldToSpendOnOneItemCategory`) |
| `TaomBattleRewardModel` | `DefaultBattleRewardModel` | Umbar renown feat |
| `TaomPartyTroopUpgradeModel` | `DefaultPartyTroopUpgradeModel` | Mounted recruit cost feats |
| `TaomPartySizeModel` | `DefaultPartySizeLimitModel` | Party size feats |
| `TaomFoodConsumptionModel` | `DefaultMobilePartyFoodConsumptionModel` | Food consumption feats |
| `TaomSettlementLoyaltyModel` | `DefaultSettlementLoyaltyModel` | Settlement loyalty feats |
| `TaomPartyMoraleModel` | `DefaultPartyMoraleModel` | Party morale feats |
| `TaomSmithingModel` | `DefaultSmithingModel` | Smithing energy cost feats |
| `TaomClanFinanceModel` | `DefaultClanFinanceModel` | Tariff income feat |
| `TaomRaidModel` | `DefaultRaidModel` | Raid damage feats |
| `TaomMilitaryPowerModel` | `DefaultMilitaryPowerModel` | Configurable T7-T10 troop power |
| `TaomCombatSimulationModel` | `DefaultCombatSimulationModel` | Configurable blunt/cut damage ratio |
| `TaomPartyHealingModel` | `DefaultPartyHealingModel` | Cultural survival bonuses |
| `TaomTournamentModel` | `DefaultTournamentModel` | Per-participant culture armor + prize pools |
| `TaomAgeModel` | `DefaultAgeModel` | Race-appropriate lifespans |
| `TaomPregnancyModel` | `DefaultPregnancyModel` | Race-appropriate pregnancy durations |
| `TaomHeroCreationModel` | `DefaultHeroCreationModel` | Race-aware hero creation defaults |
| `TaomAllianceModel` | `DefaultAllianceModel` | Racial enmity constraints |
| `TaomKingdomDecisionPermissionModel` | `DefaultKingdomDecisionPermissionModel` | Culture/race-based decision rules |
| `TaomDiplomacyModel` | `DefaultDiplomacyModel` | LOTR faction relationships |
| `TaomExecutionRelationModel` | `DefaultExecutionRelationModel` | Culture-specific execution penalties |
| `TaomInformationRestrictionModel` | `DefaultInformationRestrictionModel` | Encyclopedia visibility restrictions |
| `TaomTargetScoreModel` | `DefaultTargetScoreCalculatingModel` | Army targeting: commitment stickiness, faction priority lists, border proximity |

---

## C# Design Patterns

### 1. Hook Pattern (Harmony -> Hook Interface -> Service)

```
HarmonyPatch (thin)
    -> IOnXxx hook interface
        -> XxxHook implementation
            -> IXxxService (business logic)
```

- Harmony patch resolves `IOnXxx` hooks via `IoC.ResolveAll<IOnXxx>()`, iterates, delegates
- Hook implementation builds context, calls service
- Service contains all logic — uses adapters, fully testable

### 2. Strategy Pattern

For per-culture or per-faction variants:

```csharp
public interface ICultureStrategy
{
    string CultureId { get; }
    float Calculate(IContextAdapter context);
}
// One class per culture, registered as a collection
// Service resolves all and dispatches by CultureId
```

### 3. GameModel Override Pattern

```csharp
public class TaomFooModel : DefaultFooModel
{
    private readonly IFooService _service;
    public TaomFooModel(IFooService service) => _service = service;

    public override float Calculate(SealedType param)
    {
        var adapter = IoC.Resolve<IAdapterFactory>().GetAdapter(param);
        return _service.Calculate(adapter) ?? base.Calculate(param);
    }
}
```

### Anti-Patterns (Flag these)
- Business logic in Harmony patches (must delegate to services)
- Sealed TaleWorlds types crossing service boundaries (use adapters)
- Regular null checks on computed TaleWorlds properties (use `?.`)
- Multiple responsibilities in one service (split it)

---

## XSLT Rules

### Authoritative Source
- **SandBoxCore/ModuleData/** is the authoritative reference for vanilla XML structure
- NEVER use SandBox/ModuleData/ — it has different element names the engine ignores
- Example: SandBoxCore uses `<notable_templates>` (engine reads), SandBox uses `<notable_and_wanderer_templates>` (engine ignores)
- TAOM's other 8 XSLT are LIVE under `<game>/Modules/{TAOM_Map,LOTRLOME_Armory}/ModuleData`, in no checkout and in no CI job

### Passthrough Requirements (CRITICAL)
- Always pass through ALL vanilla attributes: `<xsl:apply-templates select="@*"/>`
- Always pass through unmodified child elements: `<xsl:apply-templates select="*[not(...)]"/>`
- Never filter out vanilla attributes — critical ones like `is_main_culture`, `can_have_settlement`, `faction_banner_key` will be silently dropped
- Only override the specific attributes/elements you intend to change

### Identity Transform
Every XSLT file must include:
```xml
<xsl:template match="@*|node()">
  <xsl:copy>
    <xsl:apply-templates select="@*|node()"/>
  </xsl:copy>
</xsl:template>
```

### Common Mistakes
- Overly broad `xsl:template match` catching unintended elements
- Hardcoding attribute values that should be passed through from vanilla
- Missing `xsl:output` declaration
- Forgetting to handle child elements when overriding a parent

---

## Testing Rules (TDD Mandatory)

### Workflow: RED -> GREEN -> REFACTOR
1. Write a failing test FIRST (verify RED state)
2. Write minimum production code to pass (GREEN)
3. Refactor while keeping tests green

### Naming Convention
`MethodName_StateUnderTest_ExpectedBehavior`

### Structure: AAA Pattern
```csharp
[TestMethod]
public void MethodName_State_Expected()
{
    // Arrange
    var mock = Substitute.For<IMyAdapter>();

    // Act
    var result = _sut.DoSomething();

    // Assert
    Assert.AreEqual(expected, result);
}
```

### Framework
- **MSTest** — `[TestClass]`, `[TestMethod]`, `[TestInitialize]`, `[TestCleanup]`
- **NSubstitute** — `Substitute.For<T>()`, `.Returns()`, `.Received()`
- **No Moq** — Project uses NSubstitute exclusively

### Test Organization
Mirror source structure: `TAOM.Tests/Features/{FeatureName}/{ServiceName}Tests.cs`

---

## Harmony Patch Categories (Known Intentional Patches)

These are all registered, intentional patches — do not flag them as unauthorized modifications. Current-as-of 2026-07-12; per-patch rationale/history/RCAs: `docs/reference/harmony-patch-registry.md` (single maintained source — this table is a routing snapshot).

| Category | Feature | Target | Status |
|----------|---------|--------|--------|
| `Patch0_BattleScenes` | Battle scenes | `Campaign.InitializeScenes` | DISABLED |
| `Patch1_FirstTimeInit` | First-time initialization | Various | active |
| `Patch2_RefreshTableau` | Banner tableau refresh | Various | active |
| `Patch3_SetRace` | Race assignment | Various | active |
| `Patch4_CharacterSpawner` | Character spawning | Various | active |
| `Patch5_FaceGen` | Face generation | Various | active |
| `Patch6_BannerEditor` | Banner editor | Various | active |
| `Patch7_FactionMap` | Faction map | Various | active |
| `Patch8_SiegeCampGuard` | Siege camp guard | Various | active |
| `Patch9_RaceFilter` | Culture-restricted race dropdown on CC | `FaceGenVM.Refresh` | active |
| `Patch10_WeatherBoundsGuard` | Weather bounds clamping | `DefaultMapWeatherModel` | active |
| `Patch11_Diplomacy` | Diplomacy system | Various | active |
| `Patch12_WarOfTheRing` | War of the Ring | Various | active |
| `Patch13_RaceAge` | NOP vanilla's same-race birth assert (mixed-race births are normal in TAOM) | `HeroCreator.DeliverOffSpring` (Transpiler) | active |
| `Patch14_Execution` | Execution system | Various | active |
| `Patch15_BannerLayerLimit` | Banner layer limit | Various | DISABLED (engine-native since v1.4.7) |
| `Patch16_AtmospherePersistence` | Forced-atmosphere scenes | `Mission.Initialize` | active |
| `Patch17_TroopWeight` | TroopWeight shed-on-upgrade (elite tax lives in `TaomPartySizeModel` since 2026-07-11) | `PartyUpgraderCampaignBehavior.UpgradeReadyTroops` (Postfix) | active |
| `Patch18_CulturalFeats` | Custom culture feat registration | `Campaign.InitializeDefaultCampaignObjects` | active |
| `Patch19_CustomBattles` | Custom battle TAOM factions/commanders/troops | `CustomBattleData`, `CustomBattleHelper`, `BannerlordMissions` | active |
| `Patch20_NarrativeHorseGuard` | Suppress CC narrative horse crashes for no-mount cultures | `CharacterCreationCampaignBehavior`, `CharacterCreationNarrativeStageView` | active |
| `Patch21_ShaderPrecompilation` | Loading-screen shader progress text | `LoadingWindowViewModel` | active |
| `Patch22_ArmyTargeting` | Border proximity floor for priority-list targets | `AiMilitaryBehavior` | active |
| `Patch23_BannerColorPersistence` | Player clan colors everywhere (UI + 3D battle + conversation) | 16 targets across `CampaignUIHelper`/`SandBoxUIHelper`/party+inventory VMs/`Mission`/`Banner`/`AgentVisuals.Create`/`MapConversationTableau` — full list in the registry | active |
| `Patch24_BannerDriftGuard` | Block vanilla banner color drift during War of the Ring | `Clan.UpdateBannerColorsAccordingToKingdom`, `Clan.UpdateBannerColor` | active |
| `Patch25_LocalizationOverride` | Let English module_strings overrides of vanilla `{=ID}` tokens apply | `MBTextManager.GetLocalizedText` (Prefix) | active |
| `Patch26_SpecialResources` | Per-kingdom resource gating + transactional spending | `PartyCharacterVM.InitializeUpgrades`, `PartyScreenLogic.UpgradeTroop`, `PartyScreenLogic.AddCommand` | active |
| `Patch27_CareerSystem` | Career screen opening + ability V-key activation | `ViewModel.ExecuteCommand`, `AgentStatCalculateModel.UpdateAgentStats` | active |
| `Patch28_SettlementGuards` | Per-settlement guard injection + per-culture spear mapping | `GuardsCampaignBehavior.TakeGuardAgentDataFromGarrisonTroopList` (manual), `GuardsCampaignBehavior.GetSuitableSpear` (manual) | active |
| `Patch29_CCBodyProperties` | Per-culture default BodyProperties on CC + body re-apply | `CharacterCreationContent.SetSelectedCulture`, `CharacterCreationCultureStageVM.OnCultureSelection`, `CharacterCreationNarrativeStageView.RefreshAgentVisuals` | active |
| `Patch30_MixedFormations` | Mixed ranged/melee formation layout (hot path, vanilla fall-through) | `Formation.GetOrderPositionOfUnit` (Prefix) | active |
| `Patch31_SmartCavalryAI` | Player-cavalry coordinated line-charge state machine | `Formation.SetMovementOrder` (Postfix, deferred — see `Patch_MissionTime_SetMovementOrder`) | active |
| `Patch33_EquipPresets` | Equipment-preset overlay on the inventory screen | `SPInventoryVM.RefreshValues` (Postfix), `GauntletInventoryScreen.OnInitialize` (Postfix) / `.OnFinalize` (Prefix) | active |
| `Patch34_QuickActions` | Inventory "Sell All" multi-action menu | `SPInventoryVM.ExecuteSellAllItems` (Prefix), `SPInventoryVM` ctor (Postfix), `SPInventoryVM.RefreshCallbacks` (Postfix), `SPInventoryVM.OnFinalize` (Postfix) | active |
| `Patch35_CompanionTactics` | Companion role prefixes (party/OOB) + OOB formation-preset overlay | `PartyCharacterVM.RefreshValues`, `OrderOfBattleHeroItemVM.RefreshValues`, `OrderOfBattleVM` ctor/finalize, OOB UI handler tick/finalize (+ manual tooltip Postfix; movement postfix in the shared deferred category) | active |
| `Patch36_FiefManagement` | F6 fief-management screen (custom GameState) | `MapScreen.OnFrameTick` (Postfix), `GameStateScreenManager.CreateScreen` (Prefix) | active |
| `Patch37_CrashReport` | Crash-capture pipeline (Priority-800 Finalizers -> `CrashReportPatchHelper`) | 9 engine-lifecycle Finalizers (`Managed.ApplicationTick`, `ScreenManager.Tick`, `Mission.Tick`, ...) | active |
| `Patch38_SettlementNameplateFade` | Distance-based settlement nameplate fade (hot path ~3000/s) | `SettlementNameplateWidget.DetermineTargetAlphaValue` (Postfix) | active |
| `Patch39_BanditPartySize` | Scale bandit initial rosters by PlayerProgress (cap = stack MaxValue) | `DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty` (Postfix) | active |
| `Patch40_HideoutDescription` | Themed LOTR hideout encounter descriptions | `HideoutCampaignBehavior.game_menu_hideout_place_on_init` (private, Postfix) | active |
| `Patch41_McmLayoutFix` | Flip MCM options screen to top-to-bottom layout (#252) | UIExtenderEx `WidgetFactoryManager.CreateAndRegister` (Postfix) | active |
| `Patch42_CastleRecruitment` | Castle troop recruitment — AI half | `AiVisitSettlementBehavior.AiHourlyTick` (Transpiler), `AiVisitSettlementBehavior.FillSettlementsToVisitWithDistancesAsDays` (Transpiler), `RecruitmentCampaignBehavior.HourlyTickParty` (Postfix) | active |
| `Patch43_BattleLoadDiagnostics` | `[BattleLoad]` phase stamps: attack->playable + mission-exit lifecycle + stall watchdog | 11 hooks (`PlayerEncounter.Start`, `MissionState.OpenNew`, `Mission.EndMission`, `MapState.OnTick`, ...) | active |
| `Patch44_CCNameAutofill` | Pre-fill CC Review-stage name field (culture-appropriate) | `CharacterCreationReviewStageVM..ctor` (Postfix) | active |
| `Patch46_TournamentDwarfDismount` | Dwarf tournament dismount (race-keyed) | `TournamentFightMissionController.PrepareForMatch` (Postfix) | active |
| `Patch47_SpiderDeathDismount` | Spider rider-death native-AV guard | `Agent.Die` (Prefix) | active |
| `Patch48_SpiderHitDismountGuard` | Spider surviving-rider dismount-AV guard (Patch47 sibling) | `Agent.HandleBlowAux` (private, Prefix) | active |
| `Patch49_ArmyGatheringNreGuard` | Army-gathering map-tick NRE guard + `[SiegeDiag]` diagnostics | `Army.FindBestGatheringSettlementAndMoveTheLeader` (private, Finalizer) | active |
| `Patch50_DropFlaggedItemGuard` | Warg-on-warg bite NRE guard | `Agent.CheckToDropFlaggedItem` (public, Finalizer) | active |
| `Patch51_RecruitmentResourceGate` | Special-resource affordability gate on the recruit Done button | `RecruitmentVM.RefreshPartyProperties` (Postfix) | active |
| `Patch53_PartyIconScale` | Campaign-map party-icon figure/mount scale (MCM slider) | `MobilePartyVisual.AddCharacterToPartyIcon` (private, Transpiler) | active |
| `Patch54_NavalTravelBoatVisual` | NavalTravel at-sea boat mesh | `MobilePartyVisual.OnTransitionEnded` + `.AddMobileIconComponents` (Postfix ×2, SandBox.View) | PARKED 2026-06-26 (#120/#296) |
| `Patch55_BasicTableauRaceGuard` | Render-safe race coercion for Save/Load preview (custom-race native AV, #295) | `BasicCharacterTableau.RefreshCharacterTableau` (private, Prefix) | active |
| `Patch56_SceneNotificationVisualGuard` | Become-king cinematic CTD guard (null AgentVisuals) | `GauntletSceneNotification.OpenScene` (private, Finalizer) + `.OnTick` (Postfix, deferred close) + `PopupSceneSpawnPoint.InitializeWithAgentVisuals` (diagnostic Prefix) | active |
| `Patch57_NavalAtSeaLandRescueGuard` | At-sea land-pathfind native-AV guard | `AIMoveToNearestLandBehavior.AiHourlyTick` (internal, Prefix) | PARKED 2026-06-26 (#120/#296) |
| `Patch58_SkipCampaignIntro` | Skip vanilla campaign intro video on NEW game (always-on) | `SandBoxGameManager.OnLoadFinished` (public override, Prefix) | active |
| `Patch59_CaravanTrade` | Caravan range/war-gate/basket levers | `CaravansCampaignBehavior.CanTradeWith` + `.GetTradeScoreForTown` + `.GetDistanceLimitVeryFarAsDaysForNavigationType` + `.CalculateBudgetFactor` (all private, Postfix ×4) | active |
| `Patch60_TournamentExitMovieRelease` | Tournament-exit movie release (#331 round 1; canary `ReleaseMovie=Nms`) | `MissionGauntletTournamentView.OnMissionScreenFinalize` (public override, SandBox.GauntletUI.dll, Prefix+Postfix) | active |
| `Patch61_SaveLoadDiagnostics` | Always-on `[SaveLoad]` lifecycle logging (15 hooks) | save/load pipeline Finalizers/Postfixes (see the feature doc) | active |
| `Patch61_SaveLoadDiagnostics_ArchiveParse` | Archive-chunk parse-fault stamps (truncation vs corruption) | `ArchiveDeserializer.LoadFrom` (internal, void Finalizer, Priority.First) | active |
| `Patch61_SaveLoadDiagnostics_BehaviorData` | Names WHICH behavior's SyncData failed | `CampaignBehaviorDataStore.LoadBehaviorData`/`.SaveBehaviorData` (internal, void Finalizer) | active |
| `Patch61_SaveLoadDiagnostics_ContainerFill` | Container (dict/list SyncData) load-fault stamps | `ContainerLoadData.InitializeReaders`/`FillCreatedObject`/`Read`/`FillObject` (internal, void Finalizers) | active |
| `Patch_MissionTime_SetMovementOrder` | Shared deferred category — ANY postfix with `MovementOrder` in its signature MUST use it | `Formation.SetMovementOrder(MovementOrder)` (Postfix ×2) | active |
| `Late_ActionSetOverride` | Race-aware action-set name resolution (null monster -> human; vanilla fall-through) | `ActionSetCode.GenerateActionSetNameWithSuffix` (Prefix) | active |
| `Late_Transpiler` | Race-appropriate `_facegen` action set in the face-gen preview | `BodyGeneratorView.RefreshCharacterEntityAux` (Transpiler) | active |

---

## Commit Conventions

50/72 rule. No AI attribution.

Example: `feat: add garrison patrol calculation`

**Optional trailers** (each on its own line after blank line):

| Trailer | When to use |
|---------|------------|
| `Constraint:` | TaleWorlds limitation blocked the ideal solution |
| `Rejected:` | Alternative approach considered and dropped |
| `Not-tested:` | Parts that can't be unit tested |
| `Research:` | What was decompiled to inform this change |
| `Save-compat:` | Save file impact |

---

## TaleWorlds Research — Lookup Order

**Check the engine study docs first for conceptual "how does X work" questions.** Use the decompile for signature verification.

| Step | Action | When |
|------|--------|------|
| 0. **[Engine process docs](docs/reference/engine/)** | Pre-filtered, TAOM-relevant, file:line-cited docs for 19 engine subsystems | **First** for "how does this process work" questions — lifecycle, formation/team AI, mount/rider, campaign-mission seam, campaign heartbeat, agent spawn pipeline, usable machines, GauntletUI, save/object system, GameModel, campaign behaviors, items. Saves raw decompile time. |
| 1. **Read decompiled source** | Read or search files in `E:\Decompiled_Bannerlord\` | Signature and behavior verification once you know which class/method to check |
| 2. **ILSpy MCP** | `mcp__ilspy__decompile_assembly` / `mcp__ilspy__list_types` | Only if type not found in decompiled source |

> ⚠️ **The decompiled source at `E:\Decompiled_Bannerlord\` is the SHIPPING-CLIENT build — it strips editor-only code.** Editor-only types (`MBEditor`, `AnimalSpawnSettings`, FBX-import / animation authoring) exist ONLY in `Win64_Shipping_wEditor` DLLs. "Absent from the dump" ≠ "doesn't exist." If a class is missing, check the editor build at `E:\Decompiled_Bannerlord\_editor_build\` before concluding it's native. See [bannerlord-engine-and-toolchain.md](docs/reference/bannerlord-engine-and-toolchain.md).

### Key engine process docs for reviewers

| Reviewing... | Read first |
|---|---|
| Harmony patch (registration, patch kind, deferred apply) | [submodule-lifecycle-and-harmony.md](docs/reference/engine/submodule-lifecycle-and-harmony.md) — deferred `MovementOrder` gotcha; Prefix/Postfix/Transpiler; `PatchCategory`; managed vs native boundary |
| Mission behavior (lifecycle, behavior type, `MissionLogics` NRE) | [mission-and-missionbehavior-lifecycle.md](docs/reference/engine/mission-and-missionbehavior-lifecycle.md) — `MissionLogic` vs `MissionBehavior` distinction; tick ordering |
| Creature / non-humanoid agent spawn crash | [agent-spawn-and-render-pipeline.md](docs/reference/engine/agent-spawn-and-render-pipeline.md) — `FromCharacterObj` vs `FromHorseObj` (skips `AddSkinMeshes`); `AgentVisuals` native boundary |
| Mount / rider / howdah seating | [mount-and-rider-runtime.md](docs/reference/engine/mount-and-rider-runtime.md) — two-phase `EventControlFlag` mount; `RiderSitBone`; three TAOM seating modes |
| Formation / team AI / `AutoGenerated.dll` DivideByZero | [formations-and-team-ai.md](docs/reference/engine/formations-and-team-ai.md) — count-division sites; `_MT` threading; spider DivideByZero lead |
| Campaign behavior / DailyTick / party AI | [campaignevents-and-campaignbehavior.md](docs/reference/engine/campaignevents-and-campaignbehavior.md) + [campaign-tick-time-and-party-ai.md](docs/reference/engine/campaign-tick-time-and-party-ai.md) — event fan-out; staggered `TickPartialHourlyAi` |
| Campaign objects (Hero/Clan/Kingdom/Settlement) | [campaign-object-graph.md](docs/reference/engine/campaign-object-graph.md) — `Settlement.Culture` not engine-saved; castle `.Village==null` NRE |
| Campaign→mission seam / encounter / `MissionState.OpenNew` | [campaign-to-mission-bridge.md](docs/reference/engine/campaign-to-mission-bridge.md) — the single managed↔native handoff; AI auto-resolve without a Mission |
| GauntletUI / ViewModel / screen push | [gauntletui-viewmodel-screen.md](docs/reference/engine/gauntletui-viewmodel-screen.md) — `CreateState<T>()` + `PushState` mandatory; `IGameStateListener`; layer input wiring |

### Pre-Decompiled Source (`E:\Decompiled_Bannerlord\`)

The entire Bannerlord v1.4.8 codebase is pre-decompiled and organized by category:

| Folder | Contents |
|--------|----------|
| `Campaign/` | `TaleWorlds.CampaignSystem` — GameModels, behaviors, actions (1,556 files) |
| `MountAndBlade/` | `TaleWorlds.MountAndBlade` — missions, agents, game logic (1,977 files) |
| `Modules/` | `SandBox`, `StoryMode` — module behaviors, views, all `Default*Model` classes (1,362 files) |
| `Core/` | `TaleWorlds.Core`, Library, SaveSystem, Localization (666 files) |
| `Engine/` | Engine, InputSystem, ScreenSystem, Navigation (386 files) |
| `UI/` | GauntletUI, PrefabSystem, PSAI (285 files) |
| `Network/` | Diamond, Network, PlayerServices (147 files) |
| `Platform/` | PlatformService, Achievements, ModuleManager (69 files) |
| `Launcher/` | Launcher.Library, Launcher.Steam (40 files) |
| `ThirdParty/` | Newtonsoft.Json, Steamworks.NET, jose-jwt (1,081 files) |

### Quick Lookup Examples

```bash
# Find a class
find "E:/Decompiled_Bannerlord/" -name "DefaultPartyWageModel.cs"

# Search for a method across all decompiled source
grep -r "GetCharacterWage" "E:/Decompiled_Bannerlord/Campaign/"

# Browse a namespace
ls "E:/Decompiled_Bannerlord/Campaign/TaleWorlds.CampaignSystem/TaleWorlds/CampaignSystem/GameComponents/"
```

### When to Look Up TaleWorlds Source

1. **Harmony patches** — Verify the target method exists with the exact signature (name, params, return type, access modifier)
2. **GameModel overrides** — Verify the base class method you're overriding exists and has the expected signature
3. **Adapter interfaces** — Verify the TaleWorlds properties/methods being wrapped actually exist
4. **Any API call you're uncertain about** — TaleWorlds renames/removes APIs between engine versions

### ILSpy MCP Fallback

If a type is not in the decompiled source, use the `ilspy` MCP tool:

```
mcp__ilspy__decompile_assembly(
  assembly_path: "E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\SandBox.dll",
  type_name: "SandBox.GameComponents.DefaultPartyWageModel"
)
```

**DLL path:** `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\`

| DLL | Contains |
|-----|----------|
| `TaleWorlds.CampaignSystem.dll` | Campaign, Hero, Clan, Kingdom, Settlement, MobileParty |
| `TaleWorlds.Core.dll` | BasicCharacterObject, ItemObject, Banner, FeatObject, GameModel base classes |
| `TaleWorlds.MountAndBlade.dll` | Agent, Mission, MissionBehavior, FormationClass |
| `SandBox.dll` | All `Default*Model` classes, SandboxAgentApplyDamageModel |
| `SandBox.View.dll` | MobilePartyVisual, MapScreen, view-layer classes |
| `StoryMode.dll` | StoryMode campaign behaviors |

If neither source is available, mark API usages as `UNVERIFIED` rather than guessing.
