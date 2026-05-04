# Codex Adversarial Review - CareerSystem Cooldown Rework - 2026-05-04

Scope reviewed:
- Production C#: `Main/Features/CareerSystem/**`, `Main/SubModule.cs`
- Config/XML: `Main/_Module/ModuleData/career_system/*.xml`, `Main/_Module/GUI/TAOMSpriteData.xml`
- Tests: `TAOM.Tests/Features/CareerSystem/**`
- Vanilla evidence: `E:\Decompiled_Bannerlord\...`

## VANILLA CODE - Decompiled Evidence

### 1. `MissionBehavior.OnMissionTick(float dt)`

Source: `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:146`

```csharp
public virtual void OnMissionTick(float dt)
{
}
```

The behavior method itself does not clamp `dt`.

The upstream managed caller is `MissionState.TickMission` / `TickMissionAux`:

Source: `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionState.cs:150`

```csharp
float num = realDt;
...
num *= timeSpeed;
...
TickMissionAux(num, realDt, updateCamera: true, asyncAITick: true);
```

Source: `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3730`

```csharp
for (int num2 = MissionBehaviors.Count - 1; num2 >= 0; num2--)
{
    MissionBehaviors[num2].OnMissionTick(dt);
}
```

Conclusion: normal-speed mission ticks pass scaled `realDt` through the managed path without a cap before `OnMissionTick(dt)`. Fast-forward has chunking, but the normal path does not.

### 2. `Mission.CurrentTime` getter

Source: `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:925`, `:1182`, `:1638`

```csharp
private float _cachedMissionTime;
public float CurrentTime => _cachedMissionTime;
internal void UpdateMissionTimeCache(float curTime)
{
    _cachedMissionTime = curTime;
}
```

Conclusion: the managed field defaults to `0f` before native updates it. I found no managed initialization to a negative sentinel.

### 3. `DefaultClanTierModel.GetCompanionLimit(Clan clan)`

Source: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultClanTierModel.cs:151`

```csharp
public override int GetCompanionLimit(Clan clan)
{
    int num = GetCompanionLimitFromTier(clan.Tier);
    if (clan.Leader.GetPerkValue(DefaultPerks.Leadership.WePledgeOurSwords))
```

Conclusion: vanilla does not accept `null` for `clan`; it dereferences `clan.Tier` and `clan.Leader`.

### 4. `SandboxAgentStatCalculateModel.UpdateAgentStats`

Source: `E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.GameComponents\SandboxAgentStatCalculateModel.cs:204`

```csharp
public override void UpdateAgentStats(Agent agent, AgentDrivenProperties agentDrivenProperties)
{
    if (agent.IsHuman) UpdateHumanStats(agent, agentDrivenProperties);
    else UpdateHorseStats(agent, agentDrivenProperties);
}
```

Conclusion: the method is `void` and mutates `agentDrivenProperties` in place.

### 5. `Input.IsKeyPressed(InputKey)`

Source: `E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:188`

```csharp
public static bool IsKeyPressed(InputKey key)
{
    return InputManager.IsKeyPressed(key);
}
```

Source: `E:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine\TaleWorlds.Engine\IInput.cs:84`

```csharp
[EngineMethod("is_key_down", false, null, true)]
bool IsKeyDown(InputKey key);
[EngineMethod("is_key_pressed", false, null, true)]
bool IsKeyPressed(InputKey key);
```

Conclusion: managed code exposes `IsKeyPressed` as a distinct engine call from `IsKeyDown`; TAOM is using the edge-trigger API. Exact native lifetime is engine-side, not visible in decompiled C#.

## Cooldown System Scenarios

### 1. First-frame V press at battle start

Trace:
- `CareerPerkMissionBehavior.OnMissionTick` resolves the player hero and `heroId` at `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:71`.
- `UpdateHud(heroId)` runs before input handling at `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:87`.
- `UpdateHud` creates the ability via `_abilityService.GetOrCreateAbility(...)` at `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:217`.
- `CareerAbilityService.GetOrCreateAbility` forces `ChargeType.CooldownOnly`, `maxCharge: 0f`, and configured cooldown at `Main/Features/CareerSystem/Abilities/CareerAbilityService.cs:38`.
- New `CareerAbility` instances start with `CooldownRemaining = 0f` because the constructor does not set it at `Main/Features/CareerSystem/Abilities/CareerAbility.cs:24`.
- The ready message fires at `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:101`.
- Pressing V then activates at `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:118`, calls `ActivateAbility` at `:122`, and emits the yellow activation message at `:170`.

Result: activation is correctly emitted, assuming HUD initialization succeeds before `UpdateHud`. A first-tick press can also show the initial green "ready" message in the same tick because readiness is checked before input.

### 2. V pressed 0.5s into cooldown

Confirmed. Activation does not touch `_lastChargingMessageTime`; the activation branch only calls `ActivateAbility`, resets `_abilityReadyNotified`, logs, and executes the effect at `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:120`.

`_lastChargingMessageTime` is updated only inside `NotifyStillCharging` at `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:134`. With the initial value `-2f` from `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:35`, a press at `CurrentTime ~= 0.5` passes the `2s` throttle and displays "still charging".

### 3. Mission ended mid-cooldown, then re-entered

Confirmed. `OnEndMission` resets mission-local state and calls `_abilityService.ClearAll()` at `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:262`. `CareerAbilityService.ClearAll` clears the dictionary at `Main/Features/CareerSystem/Abilities/CareerAbilityService.cs:80`. The next mission rebuilds the ability through `GetOrCreateAbility` at `Main/Features/CareerSystem/Abilities/CareerAbilityService.cs:19`, and the fresh ability starts ready.

### 4. Cooldown duration changes mid-session via XML edit

Confirmed. `CareerSystemIoC` registers `ICareerConfigProvider` as `Reuse.Singleton` at `Main/Features/CareerSystem/CareerSystemIoC.cs:17`. `CareerConfigProvider.EnsureLoaded` returns immediately once `_careers` is non-null at `Main/Features/CareerSystem/CareerConfigProvider.cs:67`, and `LoadAbilityTuningXml` is only called during the initial `EnsureLoaded` path at `:78`. A save-load in the same process will not reload an edited XML value. This matches `docs/features/career-system.md:92`.

## CONFIG CROSS-REFERENCE

Automated XML checks:

- Careers parsed: 50
- Required career attributes checked: `id`, `display_name`, `portrait_sprite`, `ability_template_id`, `root_choice_id`
- Missing/empty required attributes: 0
- Culture IDs referenced: `aserai`, `battania`, `dolguldur`, `empire`, `empire_s`, `empire_w`, `erebor`, `gondor`, `gundabad`, `isengard`, `khuzait`, `lothlorien`, `mirkwood`, `mordor`, `rivendell`, `sturgia`, `umbar`, `vlandia`
- Invalid culture IDs against the cheatsheet: 0
- Declared choice groups: 300
- Referenced choice groups: 300
- Missing choice group declarations: 0
- Ability templates declared: 50
- Orphan `ability_template_id` references: 0

Sprite path confirmation:
- Runtime path is built as `CareerSystem\\Abilities\\{career.AbilityTemplateId}` at `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:238`.
- Careers use ability template IDs with `_ability`, e.g. `ranger_of_ithilien_ability` at `Main/_Module/ModuleData/career_system/taom_careers.xml:12`.
- Registered sprite names match that path shape, e.g. `CareerSystem\Abilities\ranger_of_ithilien_ability` at `Main/_Module/GUI/TAOMSpriteData.xml:7278` and `:10690`.

I did not re-flag the 41 missing icons covered by issue #101.

## Known Suspects

### Suspect 1: 98 `MaxCharge` mutations are silently dead

CONFIRMED as an observation, not a runtime bug.

Evidence:
- `taom_career_choices.xml` contains 98 `property="MaxCharge"` mutations, e.g. `Main/_Module/ModuleData/career_system/taom_career_choices.xml:82`.
- `MutationService.ApplyMutation` still reflects and writes the mutated value at `Main/Features/CareerSystem/Mutations/MutationService.cs:74`.
- `AbilityTemplateData.MaxCharge` is retained at `Main/Features/CareerSystem/Domain/AbilityTemplateData.cs:9`.
- Runtime readiness ignores mutated `template.MaxCharge`: `CareerAbilityService` constructs all abilities with `ChargeType.CooldownOnly` and `maxCharge: 0f` at `Main/Features/CareerSystem/Abilities/CareerAbilityService.cs:40`.
- Grep over `Main/**/*.cs` found no production consumer of `template.MaxCharge` after mutation. Remaining `MaxCharge` references are in `CareerAbility`, parser/DTO copying, and tests.

Recommendation: either remove the 98 designer-facing `MaxCharge` mutations and document that ability fill-rate no longer exists, or repurpose them into a supported cooldown modifier. Do not leave them looking functional.

### Suspect 2: `OnMissionTick` accumulator drains slower than wall clock on long frames

CONFIRMED. This is Finding 1.

Vanilla managed code does not cap normal `dt` before `OnMissionTick`. TAOM adds all elapsed `dt` to `_tickAccumulator`, but only subtracts and ticks once when the accumulator crosses `1f` at `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:93`. A 2.5s frame drains 1.0s of cooldown and leaves 1.5s queued, so the displayed 30s cooldown can take longer than 30s wall-clock under stalls. Even without stalls, the 1Hz bucket can add up to nearly 1s of quantization delay depending on when activation happens relative to the bucket.

### Suspect 3: `_cachedHudHeroId` invalidation gap on career switch / hero death

PARTIALLY CONFIRMED as an observation, not enough evidence for a battle-runtime bug.

The cache is keyed only by `heroId` at `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:224`, while `CareerSwitchService` can change the same hero's career at `Main/Features/CareerSystem/CareerSwitchService.cs:49`. If that happens while `CareerPerkMissionBehavior` is active, the HUD name/sprite remains stale and `CareerAbilityService.GetOrCreateAbility` returns the old ability object. However, I found no TAOM call to `ChangePlayerCharacterAction` and no proof that the career switch dialogue can be used during a battle mission. I would not file this as a defect without a reproducible mid-mission switch path.

### Suspect 4: `ParseGlobalTuning` accepts subnormal floats

Subnormal acceptance is CONFIRMED, but the "activate every frame" premise is DISPUTED.

`ParseGlobalTuning` accepts `1E-45` and `0.000001` because it only rejects `<= 0` and `> 3600` at `Main/Features/CareerSystem/CareerConfigProvider.cs:395`. With the current 1Hz scheduler, a tiny positive cooldown remains non-ready until the next one-second tick, so it is effectively a roughly 1s cooldown, not every frame.

However, the same validation also accepts `NaN`, which is a real bug. This is Finding 2.

### Suspect 5: Constructor-inject ordering in `SubModule.cs`

DISPUTED.

`IoC.Configure()` runs during `OnSubModuleLoad` at `Main/SubModule.cs:75`. `CareerSystemIoC.RegisterCareerSystemFeature` is called at `Main/IoC.cs:73`, and `ICareerPassiveService` is registered before game model construction at `Main/Features/CareerSystem/CareerSystemIoC.cs:19`. `SubModule` resolves it before constructing the models at `Main/SubModule.cs:300`.

`IoC.Resolve<T>()` is a direct DryIoc resolve at `Main/IoC.cs:102`; if registration were missing, startup would throw rather than return null. The model null guards are defensive but should not be reached for a missing registration.

Vanilla `DefaultClanTierModel.GetCompanionLimit` does not accept null clans, so `TaomClanTierModel.GetCompanionLimit` also cannot safely support null because it calls `base.GetCompanionLimit(clan)` first at `Main/Features/CareerSystem/Models/TaomClanTierModel.cs:18`. I found no evidence this rework introduced a null-clan call path.

### Suspect 6: `_lastChargingMessageTime` sentinel vs `Mission.CurrentTime`

DISPUTED.

`Mission.CurrentTime` returns `_cachedMissionTime`; the managed field defaults to `0f`, and native updates it through `UpdateMissionTimeCache`. With `_lastChargingMessageTime = -2f` at `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:35`, the first early press at `CurrentTime == 0f` passes the throttle check at `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:137`.

## FINDINGS

### MEDIUM

[MEDIUM] Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:93 — Cooldown timing — `OnMissionTick` uses a single-bucket accumulator, so long frames and activation timing make a configured 30s cooldown drain slower than elapsed mission time; a 2.5s frame only applies one `Tick(1f)`, and normal play can still incur up to nearly 1s of quantization delay — Fix: for cooldowns, tick the ability with actual `dt` every frame, or change the accumulator to a `while (_tickAccumulator >= TickInterval)` loop that drains all elapsed buckets. Rework-exposed: the accumulator pattern existed before, but the cooldown rework made it user-visible readiness timing.

[MEDIUM] Main/Features/CareerSystem/CareerConfigProvider.cs:389 — Config validation — `float.TryParse` accepts `NaN`, and the later `<= 0` / `> 3600` checks both return false for `NaN`; `GlobalTuning(NaN)` then makes `CareerAbility.Activate` set `CooldownRemaining = NaN`, while `IsOnCooldown` returns false because `NaN > 0f` is false, so V can be re-activated on every `IsKeyPressed` edge — Fix: reject non-finite values with `float.IsNaN(seconds) || float.IsInfinity(seconds)` before constructing `GlobalTuning`; consider a documented lower bound such as `>= 1f` if sub-second cooldowns are not intended. Rework-introduced.

## OBSERVATIONS

- `Main/_Module/ModuleData/career_system/taom_career_choices.xml:82` and 97 other `MaxCharge` mutations are now designer-facing dead config. `MutationService` still applies them, but readiness ignores them because every ability is constructed as `CooldownOnly` with `maxCharge: 0f`.
- `Main/Features/CareerSystem/Abilities/CareerAbility.cs:32` and `:54` still retain production-dead charge APIs (`AddCharge`, `SetMaxCharge`) and `TAOM.Tests/Features/CareerSystem/CareerAbilityTests.cs:13` still codifies charge-based behavior. This is not a runtime defect, but it keeps removed design semantics alive.
- `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:217` creates abilities as a side effect of HUD update. It works in the normal battle HUD path, but the ability lifecycle is coupled to UI initialization. If `TryInitializeHud` cannot create `_hudVM`, `IsAbilityReady` remains false for an otherwise eligible hero.
- The HUD cache at `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs:224` is only keyed by `heroId`. If a same-hero career switch can occur during an active mission, the cache and existing ability object can become stale. I did not find enough evidence to file this as a battle defect.

## Verification

- `dotnet test TAOM.Tests` could not run in this sandbox. First attempt failed because the .NET first-run sentinel tried to write under `C:\Users\CodexSandboxOffline`. Retrying with `DOTNET_CLI_HOME` inside the repo got past that, but MSBuild then failed resolving the Windows SDK because access to `C:\Users\mikew\AppData\Local\Microsoft SDKs` is denied.
- XML cross-reference was run with PowerShell XML parsing against the working tree.
- Grep-equivalent verification was run with `Select-String` because `rg` is not installed in this shell.

CRITICAL: 0 | HIGH: 0 | MEDIUM: 2 | LOW: 0
VERDICT: ISSUES FOUND
