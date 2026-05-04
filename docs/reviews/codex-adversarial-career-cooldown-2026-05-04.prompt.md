# Codex Adversarial Review -- CareerSystem Cooldown Rework

This is the prompt to dispatch to Codex via `/codex:adversarial-review --background`. Codex must produce its review at `docs/reviews/codex-adversarial-career-cooldown-2026-05-04.md`.

---

## Feature description

The TAOM CareerSystem ability activation mechanic was reworked from charge-based readiness (per-career `DamageDone` / `Kills` / `DamageTaken` accumulators) to a uniform 30-second cooldown timer. All 50 careers now start ready at battle open, fire on the V key, then lock for the configured cooldown duration before becoming ready again. The rework also includes a cleanup pass (dead-code removal, GameModel constructor injection, HUD-string per-mission cache) and an upper-bound validation on the configured cooldown duration.

User intent (verbatim): *"We need to change this to charge no matter what. It should be on a timer. 30 seconds."* Plus *"Should show text still charging"* (charging-message feedback when V is pressed early).

## TAOM ID CHEATSHEET

Kingdom IDs: `empire_w`=Gondor, `empire_s`=Mordor, `empire`=Dunland, `vlandia`=Rohan, `battania`=Khand, `aserai`=Harad, `khuzait`=Easterlings, `sturgia`=Dale/North, `erebor`=Erebor, `rivendell`=Rivendell, `lothlorien`=Lothlorien, `mirkwood`=Mirkwood, `isengard`=Isengard, `gundabad`=Gundabad, `dolguldur`=DolGuldur, `umbar`=Umbar, `shaghana`=Shaghana, `abanissa`=Abanissa.
Culture IDs (custom): `gondor`, `mordor`, `erebor`, `rivendell`, `lothlorien`, `mirkwood`, `isengard`, `gundabad`, `dolguldur`, `umbar`.
Culture IDs (XSLT/vanilla): `vlandia`=Rohan, `empire`=Dunland, `empire_w`=Gondor, `empire_s`=Mordor, `battania`=Khand, `aserai`=Harad, `khuzait`=Easterlings, `sturgia`=Dale.
NOTE: `rohan` is NOT a valid ID (Rohan uses `vlandia`). `dol_guldur` is NOT valid (use `dolguldur`).

## READ FIRST

- `docs/features/career-system.md` -- updated with the new "Cooldown System" section. The `<Global cooldown_seconds>` element is described there with bounds, fallback semantics, and reload scope.
- `Main/_Module/ModuleData/career_system/taom_ability_tuning.xml` -- the new `<Global cooldown_seconds="30" />` element.
- `Main/_Module/ModuleData/career_system/taom_careers.xml` -- 50 careers; `charge_type` and `max_charge` attrs were stripped.
- `Main/_Module/ModuleData/career_system/taom_career_choices.xml` -- still contains 98 `property="MaxCharge"` mutation entries (potential dead config -- see Known Suspect 1).
- `CHANGELOG.md` -- 2026-05-04 entry for issue #103.
- Issue #103 (rework), #102 (deferred 302-line refactor), #101 (deferred 41 missing icons).

## Known Suspects -- CONFIRM or DISPUTE each

**Suspect 1: 98 `property="MaxCharge"` mutations in `taom_career_choices.xml` are silently dead.**
The mutation pipeline `MutationService.MutateAbility -> ApplyMutation -> reflected SetValue on AbilityTemplateData.MaxCharge` still works mechanically -- `AbilityTemplateData.MaxCharge` was retained for that reason. But after the rework, no code reads `template.MaxCharge` for ability readiness (`CareerAbilityService` constructs `CareerAbility` with `maxCharge: 0f` and forces `ChargeType.CooldownOnly`). Choice-tree designers think they are tuning ability fill-rate, but the value goes nowhere. CONFIRM by tracing MaxCharge from `taom_career_choices.xml -> MutationService.cs:97 (prop.SetValue) -> any consumer`. DISPUTE if you find a consumer of mutated `template.MaxCharge` outside test code.

**Suspect 2: `OnMissionTick` accumulator drains slower than wall clock on long frames.**
`CareerPerkMissionBehavior.OnMissionTick` line ~85: `_tickAccumulator += dt; if (_tickAccumulator >= TickInterval) { _tickAccumulator -= TickInterval; _abilityService.Tick(heroId, TickInterval); }`. Single-bucket pattern. If a frame drops to 2.5s (alt-tab return, GC pause), only one `Tick(1f)` fires that frame even though 2.5s elapsed. Cooldown drains 1s instead of 2.5s. CONFIRM whether this could cause user-visible cooldown drift (e.g., a 30s cooldown taking 35-40s wall-clock under load). DISPUTE if the dt cap is enforced upstream by Mission infrastructure.

**Suspect 3: `_cachedHudHeroId` invalidation gap on career switch / hero death.**
`CareerPerkMissionBehavior.UpdateHud` at line ~210 caches `_cachedHudAbilityName` and `_cachedHudAbilitySprite`, keyed by `heroId`. The cache is only refreshed when `heroId != _cachedHudHeroId`. Reset only in `OnEndMission`. If the player's hero is replaced mid-mission (which I don't think happens, but verify) or if `CharacterObject.PlayerCharacter?.HeroObject?.StringId` returns the same id but the underlying career changed, the HUD shows stale name/sprite. CONFIRM whether any code path can change `Hero.MainHero` or its career mid-mission. DISPUTE if you can prove neither is possible.

**Suspect 4: `ParseGlobalTuning` accepts subnormal floats.**
`CareerConfigProvider.ParseGlobalTuning` rejects `<= 0` and `> 3600`. Accepts `0.000001f`. At a cooldown of 0.001s, `Tick(1f)` saturates at 0 the same frame, so abilities are effectively always ready -- spamming V triggers the activation animation/sound every frame. CONFIRM whether this is a real footgun or acceptable (the user can edit XML to whatever they want; very small values are intentional). Recommend a sensible lower bound (e.g., 1.0s) if confirmed.

**Suspect 5: Constructor-inject ordering in `SubModule.cs`.**
`SubModule.cs` lines 313-318: GameModels (`TaomAgentStatCalculateModel`, `TaomAgentApplyDamageModel`, `TaomClanTierModel`) now take `ICareerPassiveService` via constructor. `careerPassiveService` is resolved at line 300 (above). Verify that `ICareerPassiveService` is registered in DryIoc BEFORE this line runs. Also verify what happens if `IoC.Resolve<ICareerPassiveService>()` returns null -- each model defends with `if (_passiveService == null) return base.X(...)` -- but does a null GameModel construction silently break game startup, or is it caught?

**Suspect 6: `_lastChargingMessageTime` sentinel value interacts oddly with `Mission.CurrentTime`.**
Initialized to `-ChargingMessageThrottleSeconds` (= -2.0f). The check `now - _lastChargingMessageTime < ChargingMessageThrottleSeconds` -- if `Mission.CurrentTime` is undefined or returns a negative-ish value at very-early-mission, the throttle could be skipped or stuck. CONFIRM what `Mission.Current.CurrentTime` returns at frame 0 of a mission. DISPUTE if it always starts at 0 or larger.

## Files in scope

### Production C\#
- `Main/Features/CareerSystem/Abilities/CareerAbility.cs` -- added `ReadyProgress01`
- `Main/Features/CareerSystem/Abilities/CareerAbilityService.cs` -- inject `ICareerConfigProvider`, force `CooldownOnly`, add `GetCooldownRemaining`, removed `AddCharge`
- `Main/Features/CareerSystem/Abilities/ICareerAbilityService.cs` -- added `GetCooldownRemaining`, removed `AddCharge`
- `Main/Features/CareerSystem/CareerConfigProvider.cs` -- parses `<Global cooldown_seconds>` with `(0, 3600]` validation
- `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` -- charging-message else branch, HUD cache, removed AddCharge calls and SetMaxCharge mutation block
- `Main/Features/CareerSystem/Domain/AbilityTemplateData.cs` -- removed dead `Cooldown` and `SpriteName` fields (kept `MaxCharge` for reflection compat)
- `Main/Features/CareerSystem/Domain/AbilityTuningConfig.cs` -- new `GlobalTuning` class
- `Main/Features/CareerSystem/Domain/CareerDefinition.cs` -- removed `ChargeType` and `MaxCharge` props (dead under uniform cooldown)
- `Main/Features/CareerSystem/UI/CareerAbilityHudVM.cs` -- `Update` signature now `(progress01, isReady)` -- clamps to [0,1]
- `Main/Features/CareerSystem/UI/CharacterDeveloperCareerMixin.cs` -- resolve services once in ctor, not per call
- `Main/Features/CareerSystem/Models/TaomClanTierModel.cs` -- ctor injection of ICareerPassiveService
- `Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs` -- ctor injection of ICareerPassiveService
- `Main/Features/CareerSystem/Models/TaomAgentApplyDamageModel.cs` -- ctor injection of ICareerPassiveService
- `Main/SubModule.cs` lines 300-318 -- GameModel construction with resolved careerPassiveService

### XML / config
- `Main/_Module/ModuleData/career_system/taom_ability_tuning.xml` -- new `<Global cooldown_seconds="30" />`
- `Main/_Module/ModuleData/career_system/taom_careers.xml` -- 50 entries with `charge_type` / `max_charge` removed
- `Main/_Module/ModuleData/career_system/taom_career_choices.xml` -- NOT changed but consumed by mutation system (98 `property="MaxCharge"` entries)
- `Main/_Module/ModuleData/career_system/taom_ability_templates.xml` -- NOT changed (still has `cooldown="0"` per-template -- now ignored)
- `Main/_Module/GUI/Prefabs/CareerSystem/AbilityHUD.xml` -- NOT changed (consumes HudVM)

### Tests
- `TAOM.Tests/Features/CareerSystem/CareerAbilityServiceTests.cs` -- NEW, 11 methods
- `TAOM.Tests/Features/CareerSystem/CareerAbilityTests.cs` -- +6 ReadyProgress01 tests (20 total)
- `TAOM.Tests/Features/CareerSystem/CareerConfigProviderTests.cs` -- +5 Global parsing tests
- `TAOM.Tests/Features/CareerSystem/Abilities/{Cavalry,Infantry,Ranged}AbilityExecutorTests.cs` -- ctor signature update
- `TAOM.Tests/Features/CareerSystem/CareerCampaignBehaviorTests.cs`, `CareerCreationHandlerTests.cs`, `CareerPassiveServiceTests.cs`, `CareerRegistryTests.cs`, `CareerScreenVMTests.cs`, `CareerSwitchServiceTests.cs`, `MutationServiceTests.cs`, `CharacterCreation/CareerMenuServiceTests.cs` -- updated for trimmed `CareerDefinition` ctor

## REQUIRED SECTIONS

### VANILLA CODE -- decompile and paste

For each of these, decompile from `E:\Decompiled_Bannerlord\` and paste the relevant code blocks:

1. **`MissionBehavior.OnMissionTick(float dt)`** -- in `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds\MountAndBlade\MissionBehavior.cs`. What is the upstream `dt` cap, if any? Is `dt` clamped before passing to behaviors?
2. **`Mission.CurrentTime`** getter -- in `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds\MountAndBlade\Mission.cs`. What does it return at mission frame 0?
3. **`DefaultClanTierModel`** -- in `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds\CampaignSystem\GameComponents\DefaultClanTierModel.cs`. Does `GetCompanionLimit(Clan clan)` accept a null clan? What is the base behavior?
4. **`SandboxAgentStatCalculateModel.UpdateAgentStats`** -- in `E:\Decompiled_Bannerlord\Modules\SandBox\GameComponents\SandboxAgentStatCalculateModel.cs`. Confirm `agentDrivenProperties` is mutated in place (no return value).
5. **`Input.IsKeyPressed(InputKey)`** -- in `E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds\InputSystem\Input.cs`. Single-frame edge-trigger or sustained press?

Paste each as a fenced code block.

### Cooldown system specifics

For each of these scenarios, read the relevant TAOM source and answer with file:line citations:

1. **First-frame V press at battle start:** Mission begins, `Mission.CurrentTime` is approximately 0. Player presses V on the first tick. `_lastChargingMessageTime` is initialized to -2. `_abilityService.IsAbilityReady(heroId)` should be true (ability starts with `CooldownRemaining = 0`). Trace the activation path. Is the activation message correctly emitted?

2. **V pressed 0.5s into the cooldown:** Player just activated, `CooldownRemaining = 30`. They press V again 0.5s later. `_lastChargingMessageTime` was just set to ~0 by the prior charging-message check on activation? No -- on activation, `_lastChargingMessageTime` is NOT touched. So the throttle gate compares against the initial -2, which is older than 2s, so a charging message fires. Verify by reading `NotifyStillCharging` and confirming the throttle field is updated only inside `NotifyStillCharging`.

3. **Mission ended mid-cooldown, then re-entered:** `OnEndMission` clears `_abilityService.ClearAll()`. Next mission starts with a fresh CareerAbility (CooldownRemaining = 0, ready). Confirm via `CareerAbilityService.GetOrCreateAbility` that the dictionary is rebuilt correctly.

4. **Cooldown duration changes mid-session via XML edit:** `CareerConfigProvider` is `Reuse.Singleton` and caches `_abilityTuning` in `EnsureLoaded`. Verify that an XML edit + save-load (without app restart) does NOT pick up the new value -- confirming the doc note about restart scope.

### CONFIG CROSS-REFERENCE

1. **Cross-reference `taom_careers.xml` against the cleaned-up parser.** The parser now reads only: `id`, `display_name`, `description`, `portrait_sprite`, `ability_template_id`, `min_clan_tier`, `root_choice_id`, `EligibleCultures/Culture id=`, `ChoiceGroups/Group id=`. Verify that for every `<Career>` element in `taom_careers.xml`, ALL those required attributes are present and non-empty (except `description` and `min_clan_tier` which can default).

2. **Cross-reference `EligibleCultures/Culture id=` values across all 50 careers against the CHEATSHEET.** Flag any culture id that is not a valid TAOM/vanilla culture.

3. **Cross-reference `ChoiceGroups/Group id=` against `<ChoiceGroup id=>` declarations in `taom_career_choices.xml`.** Flag any group id referenced by a career but not defined.

4. **Cross-reference `ability_template_id` against `<AbilityTemplate id=>` in `taom_ability_templates.xml`.** Flag any orphan reference.

5. **Sprite registry cross-reference -- already known: 41 of 50 ability icons missing.** The runtime path is `CareerSystem\Abilities\<career_id>_ability` per `CareerPerkMissionBehavior.RefreshHudCache`. The 9 registered icons in `Main/_Module/GUI/TAOMSpriteData.xml` are: captain_of_osgiliath, crossbow_master, eotheod_windrider, ironguard, knight_of_belfalas, marksman_of_aldburg, ram_rider, ranger_of_ithilien, watchman_of_stangard. Issue #101 already filed. Do NOT re-flag this -- but DO confirm the runtime path matches the registered names (no path-construction bug).

### FINDINGS OR OBSERVATIONS

For each finding:
- File and line citation
- Severity (CRITICAL / HIGH / MEDIUM / LOW)
- What the bug is and what it would cause at runtime
- Recommended fix
- Whether the issue is rework-introduced or pre-existing in the file

Categorize "OBSERVATIONS" separately for design-quality concerns that aren't bugs (e.g., the 98 dead `MaxCharge` mutations -- design-level cleanup, not a runtime bug).

## QUALITY GATES

- Cite file paths with line numbers for every finding.
- Do NOT flag the 41 missing icon PNGs (already in #101).
- Do NOT flag `CareerPerkMissionBehavior.cs` line count (already in #102).
- Do NOT flag `CareerPassiveHelper`'s lazy-cached `IoC.Resolve` pattern -- the perf rule explicitly allows it.
- Do NOT flag `GauntletCareerScreen` constructor `IoC.Resolve` -- ScreenBase subclass is a sanctioned boundary class per `.claude/rules/csharp-architecture.md`.
- Verify ALL "missing" claims with `grep` before flagging. Codex has been wrong about these before.
- Decompile from the INSTALLED 1.3.15 DLLs via `ilspycmd` if you find any signature ambiguity in `E:\Decompiled_Bannerlord\` (which is 1.4).

## Prior review lessons

SUCCESSES: Config ID cross-reference caught `rohan` / `dol_guldur` mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.

FAILURES: Codex assumed `empire`=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

## Output

Save your full review to: `docs/reviews/codex-adversarial-career-cooldown-2026-05-04.md`.
