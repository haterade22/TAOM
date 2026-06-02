# Codex Adversarial Review -- CareerSystem Switch Picker + Effect-Scope Badges -- 2026-06-01

Dispatch this prompt to Codex via `codex exec --output-last-message <output> - < <this>`. Output review to `docs/reviews/codex-adversarial-career-switch-picker-2026-06-01.md`.

---

## Feature description

Two related UX changes to the TAOM CareerSystem:

**Concern 1 -- Effect-scope clarity in choice tree.** The career-screen choice groups (e.g. "Hammer of the Deeps") render 5 bullets that mix always-active passives (+8% melee damage, etc.) with ability-bound mutations (Mithril Bastion radius increased, which only fires while the V-key career ability is active). Players couldn't tell which was which. The fix added a "While active" badge next to keystone bullets via a new `EffectScopeBadge` / `EffectScopeTooltip` `[DataSourceProperty]` on `CareerChoiceObjectVM`, gated on `@IsKeystone` in the prefab.

**Concern 2 -- Dialogue-driven career-switch picker.** The "I wish to discuss my career path" dialogue option (on companion NPCs under `hero_main_options`) used to silently auto-switch the player to whatever career was first in registry iteration order. Replaced with a picker: dialogue option now opens `GauntletCareerScreen` in a new "switch mode" that renders a list of eligible alternatives (`ICareerRegistry.GetEligibleSwitchTargets` -- filters by culture + clan tier AND excludes current career). Each target card has a "Choose" button -> `CareerSwitchService.SwitchCareer`. The service also gained a same-career rejection (defensive boundary).

User intent (verbatim): *"Also for this. When I want to discuss my career path, it should give me an option to pick the one I want."* And on the effect bullets: *"One of the things about these abilities are it is unclear if they are always active (like a passive) or only when they activate the career ability. We need to understand that."*

## TAOM ID CHEATSHEET

Kingdom IDs: `empire_w`=Gondor, `empire_s`=Mordor, `empire`=Dunland, `vlandia`=Rohan, `battania`=Khand, `aserai`=Harad, `khuzait`=Easterlings, `sturgia`=Dale/North, `erebor`=Erebor, `rivendell`=Rivendell, `lothlorien`=Lothlorien, `mirkwood`=Mirkwood, `isengard`=Isengard, `gundabad`=Gundabad, `dolguldur`=DolGuldur, `umbar`=Umbar.
Culture IDs (custom): `gondor`, `mordor`, `erebor`, `rivendell`, `lothlorien`, `mirkwood`, `isengard`, `gundabad`, `dolguldur`, `umbar`.
Culture IDs (XSLT/vanilla): `vlandia`=Rohan, `empire`=Dunland, `empire_w`=Gondor, `empire_s`=Mordor, `battania`=Khand, `aserai`=Harad, `khuzait`=Easterlings, `sturgia`=Dale.

## READ FIRST

- The plan: `C:\Users\mikew\.claude\plans\for-our-career-system-structured-stroustrup.md` (the version that was approved and shipped).
- The prior session's reviews of related CareerSystem work: `docs/reviews/codex-adversarial-career-cooldown-2026-05-04.md`, `REVIEW-LOG.md` Reviews #29 / #30 / #31.
- TAOM rules pertinent to this changeset: `.claude/rules/csharp-architecture.md`, `.claude/rules/gui-ui.md`, `.claude/rules/csharp-patterns.md`.

## Files in scope (production)

- `Main/Features/CareerSystem/UI/CareerChoiceObjectVM.cs` -- added `EffectScopeBadge` + `EffectScopeTooltip` `[DataSourceProperty]` (computed, allocate `TextObject` per get).
- `Main/Features/CareerSystem/ICareerRegistry.cs` -- added `GetEligibleSwitchTargets(currentCareerId, hero)`.
- `Main/Features/CareerSystem/CareerRegistry.cs` -- impl. Iterates all careers, calls `IsEligible`, excludes current id (ordinal-ignore-case).
- `Main/Features/CareerSystem/CareerSwitchService.cs` -- `CanSwitch` now reads `hero.StringId` to look up current career via `_dataService.GetCareerStringId` and rejects same-career.
- `Main/Features/CareerSystem/CareerSwitchDialogueBehavior.cs` -- rewired. Condition uses `GetEligibleSwitchTargets` for visibility gate (hides option if 0 alternatives). Consequence calls `GauntletCareerScreen.OpenCareerScreen(switchMode: true)`. The legacy `_pendingNewCareerId` field is gone. Constructor still accepts `ICareerSwitchService` (unused internally; kept for IoC stability via `_ = switchService`).
- `Main/Features/CareerSystem/UI/CareerSwitchTargetVM.cs` -- **NEW** file. Per-eligible-career card VM: `Name`, `Description`, `PortraitSprite`, `AbilityName`, `AbilitySpriteName`, `ChooseLabel` (all loc'd via `TextObject` at ctor time), `ExecuteChoose()` -> callback.
- `Main/Features/CareerSystem/UI/CareerScreenVM.cs` -- added 3 optional ctor params at end (`isSwitchMode`, `heroAdapter`, `onChooseSwitchTarget`). Added `IsSwitchMode` / `IsNormalMode` / `IsBrowsingTargets` / `EligibleSwitchTargets` / `SwitchModeTitle` / `SwitchModeSubtitle` `[DataSourceProperty]`. `RefreshValues` early-returns after `RebuildEligibleSwitchTargets` in switch mode and sets `HasCareer = false`.
- `Main/Features/CareerSystem/UI/CareerScreenGameState.cs` -- added `IsSwitchMode { get; set; }` flag.
- `Main/Features/CareerSystem/UI/GauntletCareerScreen.cs` -- rewrote. `OpenCareerScreen(bool switchMode = false)` sets `state.IsSwitchMode` before `PushState`. Ctor reads `state.IsSwitchMode`, resolves `ICareerSwitchService` + `ICareerHeroAdapterFactory`, creates an adapter via factory ONLY in switch mode. New `OnChooseSwitchTarget(newCareerId)` callback wired into the VM -- calls `_switchService.SwitchCareer` then `CloseScreen`.

## Files in scope (XML)

- `Main/_Module/GUI/Prefabs/CareerSystem/CareerScreen.xml` -- 3 changes:
  1. Each of the three tier choice-template `<TextWidget Text="@Description">` is now wrapped in a `<ListPanel>` that adds a sibling small text badge bound to `@EffectScopeBadge` and gated `IsVisible="@IsKeystone"`.
  2. The middle-area widget at line 30 now gates on `IsVisible="@IsNormalMode"`.
  3. New parallel "switch-mode picker panel" inserted just before the `Standard.DialogCloseButtons` -- outer container gated on `IsVisible="@IsSwitchMode"`, inner `ScrollablePanel` with a `ListPanel DataSource="{EligibleSwitchTargets}"` and per-card `Command.Click="ExecuteChoose"` button (`Brush="Popup.GreenButton"`).
- `Main/_Module/ModuleData/taom_module_strings.xml` -- 8 new keys: `taom_career_choice_while_active`, `_passive_tooltip`, `_keystone_tooltip`, `taom_career_switch_title`, `_choose`, `_subtitle`, `taom_career_switch`, `taom_career_switched_open_screen`.

## Files in scope (tests)

- `TAOM.Tests/Features/CareerSystem/CareerRegistryTests.cs` -- +5 `GetEligibleSwitchTargets_*` tests.
- `TAOM.Tests/Features/CareerSystem/CareerSwitchServiceTests.cs` -- +2 same-career tests.
- `TAOM.Tests/Features/CareerSystem/CareerScreenVMTests.cs` -- +5 switch-mode tests via new `CreateSwitchModeVM` helper.

`./build.ps1 -RunTests` = 2894 passed / 2 skipped (pre-existing Warg) / 0 failed.

## Known Suspects -- CONFIRM or DISPUTE each

**Suspect 1: `Popup.GreenButton` brush may not exist in vanilla 1.4.5.** The new prefab picker uses `Brush="Popup.GreenButton"` on the Choose button at the per-card level. Verify this brush exists in the installed `bin/Win64_Shipping_Client/...` brush registry. If not, the button will paint unstyled / invisible. Decompile or grep vanilla `GUI/Brushes/*.xml` to confirm.

**Suspect 2: Nested `ListPanel` with `Command.Click` on item-template VM may not bind.** The picker has `<ListPanel DataSource="{EligibleSwitchTargets}"><ItemTemplate>...<ButtonWidget Command.Click="ExecuteChoose">` where `ExecuteChoose` is a public method on `CareerSwitchTargetVM` (the item VM, NOT the screen VM). Vanilla Gauntlet typically binds `Command.Click` to the ROOT data-source's methods; verify that item-template scope correctly routes the click to the per-item VM. If it routes to the screen VM, `ExecuteChoose` resolves nowhere and nothing happens.

**Suspect 3: Picker outer panel gated on `@IsSwitchMode` not `@IsBrowsingTargets` -- empty-state UX gap.** If the player has 0 eligible targets, the picker container shows but the inner list is empty. The dialogue gate hides the option in that case so this should be unreachable in practice -- but if a state mutation between dialogue-condition and screen-open invalidates the eligible set (level-up restricting clan-tier check, etc.), the player sees an empty modal until Esc. CONFIRM whether this is impossible (state can't change between condition and open) or a real edge case.

**Suspect 4: `CareerSwitchDialogueBehavior` ctor still accepts `ICareerSwitchService` but stores nothing.** Pattern is `_ = switchService;` to silence unused-param warnings while preserving the ctor signature for IoC stability. Is this a code-smell that breaks dependency-graph correctness (DryIoc still injects but the dep is dead)? Or is it the right backwards-compat move?

**Suspect 5: `CareerChoiceObjectVM.EffectScopeBadge` / `EffectScopeTooltip` allocate `TextObject` per get.** Computed `[DataSourceProperty]`s with no caching. The prefab binds them. If Gauntlet polls these per refresh tick rather than once per data-source change, there's N choices x M tick allocations. The choice screen is cold (not OnMissionTick), but each `CareerChoiceObjectVM` is rebuilt on every `RefreshValues` -- verify whether `RefreshValues` fires on every perk-take or only on hover etc. Run `taom-src` against `ViewModel` base to understand the polling model.

**Suspect 6: GameState flag race -- `state.IsSwitchMode = true; PushState(state)` then ctor reads `state.IsSwitchMode`.** Vanilla `GameStateManager` -- does `CreateState<T>()` invoke the ctor synchronously, or does it defer until `PushState`? If it defers ctor invocation, then setting `state.IsSwitchMode = true` after `CreateState` correctly reaches the ctor. If `CreateState` invokes the ctor synchronously (before the line that sets the flag), `state.IsSwitchMode` is always false at ctor-read time. Decompile `GameStateManager.CreateState<T>` to confirm.

**Suspect 7: Same-career rejection bypass when `hero.StringId` is null/empty.** `CareerSwitchService.CanSwitch` only applies the same-career guard if `heroId` is non-empty. If a mock or test passes a hero adapter with `StringId.Returns(null)`, the guard is skipped and the call falls through to `IsEligible`. Verify this is a non-issue (Hero.MainHero.StringId always populated in production) and that no test fixture relies on the bypassed behavior.

**Suspect 8: `OnSessionLaunchedEvent.AddNonSerializedListener` registers dialogues -- if the player has 0 eligible alternatives at session-launch, does the condition method get RE-evaluated on subsequent dialogue opens?** AddPlayerLine registers ONCE per session; the condition lambda re-fires on every dialogue open. Verify -- if condition is cached, the dialogue option could be permanently hidden even after the player gains clan tier.

## REQUIRED SECTIONS

### VANILLA CODE -- decompile and paste

1. **`CampaignGameStarter.AddPlayerLine` / `AddDialogLine`** -- in `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds\CampaignSystem\CampaignGameStarter.cs`. Paste the full signature with default param order. Confirm 7-arg overload `(id, inputToken, outputToken, text, condition, consequence, priority)` exists.

2. **`GameStateManager.CreateState<T>()` / `PushState(state)`** -- in `E:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds\Core\GameStateManager.cs`. Paste the implementation. Verify whether `CreateState` synchronously invokes the screen ctor or defers.

3. **`hero_main_options` dialogue token** -- grep `E:\Decompiled_Bannerlord` for vanilla `AddPlayerLine(..., "hero_main_options", ...)`. List the canonical vanilla NPC-options branches under this token. Verify TAOM's `career_switch_start` doesn't collide with a vanilla id.

4. **`Brush="Popup.GreenButton"`** -- grep `E:\Decompiled_Bannerlord` (or the installed `GUI/Brushes/`) for `Popup.GreenButton`. Confirm presence.

### Switch flow specifics

For each scenario, read TAOM source and answer with file:line:

1. **Switch from Gondor career A to Gondor career B.** `CareerSwitchService.SwitchCareer` clears old career + chosen perks, sets new career, adds root choice, refreshes passive cache. Trace and confirm no orphan perks survive.

2. **0 eligible alternatives.** Dialogue condition hides the option (`targets.Count > 0`). Confirmed in `CareerSwitchDialogueBehavior.CareerSwitchCondition`. If the player force-opens via console / other entry, what happens?

3. **Player exits switch screen via Esc without choosing.** `GauntletCareerScreen.OnFrameTick` calls `CloseScreen`. No career change. Confirm.

4. **The dialogue text "Very well -- let us discuss what paths lie before you" is referenced as `{=taom_career_switched_open_screen}...`.** Verify the loc key exists in `taom_module_strings.xml` and that the file is registered as a `GameText` source in `Main/SubModule.xml`.

5. **The same-career rejection** -- if `hero.StringId` is `"hero1"` and `_dataService.GetCareerStringId("hero1") == "warboss"`, then `CanSwitch(hero, "warboss")` rejects. Trace + confirm at file:line.

### CONFIG CROSS-REFERENCE

1. **All `{=taom_...}` references in the changed files cross-checked against `taom_module_strings.xml`.** Build the list of every `{=` in changed `.cs` + the prefab, then verify each has a `<string id=...>` entry. Flag orphans.

2. **`hero_main_options` outputToken** -- verify TAOM's `career_switch_response` chain (`career_switch_start -> career_switch_response`) doesn't collide with another TAOM behavior that registers `career_switch_response` separately.

### FINDINGS OR OBSERVATIONS

For each finding:
- File and line citation
- Severity (CRITICAL / HIGH / MEDIUM / LOW)
- What the bug is and what it would cause at runtime
- Recommended fix
- Rework-introduced vs pre-existing

## QUALITY GATES

- Cite file paths with line numbers for every finding.
- Do NOT flag the 41 missing icon PNGs (#101). Do NOT flag `CareerPerkMissionBehavior.cs` line count (#102). Do NOT flag the 98 dead `MaxCharge` mutations (#104).
- Verify ALL "missing" claims with grep before flagging. Codex has been wrong about these before.

## Prior review lessons

SUCCESSES: Config ID cross-reference caught `rohan`/`dol_guldur` mismatches. Vanilla decompilation caught missing gates (#31 single-bucket tick, #31 NaN admission). Lifecycle tracing caught stale caches.

FAILURES: Codex assumed `empire`=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

## Output

Save your full review to: `docs/reviews/codex-adversarial-career-switch-picker-2026-06-01.md`.
