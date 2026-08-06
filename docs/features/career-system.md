# Career System

**Status:** Verified in-game (2026-04-14). Career button with sprite on Character Developer screen, GameState-based screen opening (no crash), career selection in Character Creation. Gondor campaign tested.

**2026-05-04 update.** Ability activation rebuilt as a uniform 30-second cooldown timer. The original charge-based readiness model (DamageDone / Kills / DamageTaken accumulators) was replaced because per-archetype charge types produced confusing UX — defensive careers like Captain of Osgiliath only charged when the player took damage, so back-line players never saw the ability ready. See [Cooldown System](#cooldown-system) and `CHANGELOG.md` (issue #103).

**2026-06-02 update — issues #102 + #104.** `CareerPerkMissionBehavior` refactored from 302 → 139 LOC by extracting three Singleton-lifetime controllers (`IAbilityActivationController` + `IAbilityHudController` + `IAbilityEffectExecutor`) and two adapters (`IAbilityInputAdapter` + `IMissionTimeProvider`); the V-key state machine is now unit-testable end-to-end. The 98 dead `MaxCharge` mutations in `taom_career_choices.xml` were repurposed as `CooldownReduction` (50× -6, 48× -9) targeting a new property on `AbilityTemplateData`; effective cooldown is `max(MinCooldownSeconds=5, 30 - reduction)`. Two Codex adversarial reviews + a 5-dimension Claude deep-review fan-out were applied; all 17 actionable findings triaged.

**2026-08-05 update — issues #377–#384 (career UX arc, adopted from external submission `TAOM-Career-UX-Upstream-2026-08-05` via `/adopt-external`; reviewed text ported, module/DLL never installed).**
- **#377 runtime fixes:** buff-tracker entries are now contribution-counted (`AddContribution`/`RemoveContribution`) and retire when the last restore fires — `GetBuff(heroId)` non-null now MEANS "ability window live" (pre-fix a zeroed entry lingered to mission end; `ExpiresAt`/`IsExpired` were written-never-read and are deleted). `CareerAbility` gained the active window (`BeginActiveWindow` from the executor with the same mutation-extended duration that schedules the restores; `ActiveDuration`/`ActiveRemaining`/`IsActive`/`ActiveProgress01`, NaN-guarded). A controlled-agent identity gate (`Mission.MainAgent.Character == hero.CharacterObject`, computed once per tick in the behavior) now gates the HUD, the ready toast, and V-presses — pre-fix a V-press while controlling a soldier consumed the cooldown while `ApplyAoeBuff` early-returned.
- **#378 button feel:** the career button got `Id="TaomCareerButton"` + a Brush (`CareerSystem.CareerButton` — Gauntlet derives Pressed/Hovered ONLY from a Brush; a bare `Sprite=` has no press state, verified in decompile) + a click sound (`UISoundsHelper.PlayUISound`, the FiefManagement idiom; type lives in the Native module's `TaleWorlds.MountAndBlade.View.dll`). CareerScreen's `+`/`−` buttons had the same bare-Sprite defect — same brush treatment (`CareerSystem.PlusButton`/`MinusButton`, per-style ColorFactor variants, no new art needed).
- **#379 unspent-points badge:** `ICareerRegistry.GetUnspentPoints(level, taken)` is the single source (reused by `CareerScreenVM` + `CareerCampaignBehavior`); `CharacterDeveloperCareerMixin` exposes `HasUnspentPoints`/`UnspentPointsText`, rendered as a red disc + count child of the button (screen-independent by design — the badge exists for players who never open the career screen).
- **#380 keystone glyphs:** `keystone_icon="NNNNN"` per `<Career>` in `taom_careers.xml` (all 49 active + the commented-out `far_harad_halftroll` → 25013), parsed into `CareerDefinition.KeystoneIcon`, rendered as a 30px tinted medallion beside the "While active" badge. Banner icons double as bare-number sprite names. **Deliberately NO culture fallback** — missing attr = no medallion + a load warning (the reference module's fallback regressed Rivendell to its live clan sigil 15009). Art caveats logged in #380 for a future curation pass (several picks are live clan sigils; Khand rides the Harad sheet).
- **#381 keystone branch exclusivity (design decision, user-approved 2026-08-05):** one keystone per tier; completing any full tier-3 group reopens all keystones; already-taken stones are never locked (grandfathered + refundable — the rule gates FUTURE takes only, so pre-rule saves keep their stacks). Extracted to `KeystoneExclusivityRule` (one rule, three consumers: both `CareerScreenVM` select paths + `RebuildChoiceGroups` display state, so a closed keystone dims instead of silently rejecting the click). The one-per-tier core predated this arc (`43c9cb61`); the tier-3 exemption + display gating are new.
- **#382 energy bar:** the 130×166 square `AbilityHUD` panel (which overlapped the tournament prize lane) is RETIRED — `IAbilityHudController`/`AbilityHudController`/`CareerAbilityHudVM`/`AbilityHUD.xml` deleted. Replacement: `CareerEnergyBarPrefab` (UIExtenderEx `PrefabExtensionInsertPatch` into Native's `AgentStatus` prefab — TAOM's first mission-screen prefab injection; container-safety verified: `MissionGauntletAgentStatus` does zero child traversal, only the damage feed is typed/positional and is untouched) + `MissionAgentStatusCareerMixin` (`[ViewModelMixin("Tick")]` on `MissionAgentStatusVM`) + the pure `CareerEnergyBarStateMapper` (Ready full / Active drains via `ActiveProgress01` / Cooldown refills with the derived rescale `(raw − d)/(1 − d)`, `d = ActiveDuration/CooldownDuration` — kills the measured empty→40% refill snap without captured state). Native ShieldHealthBar Canvas/Frame/Fill brushes (flat colors read as misaligned — the brush carries the bevel), career-glyph medallion, and a key chip fed from `IAbilityInputAdapter.ActivationKeyName` (single-sourced with the polled `InputKey.V`).
- **#383 damage attribution:** `CareerPerkMissionBehavior.OnScoreHit` (signature pinned against installed v1.4.7 DLL — two `in` params silently no-op the override if wrong) prints `{TARGET}: {DMG} damage (+{BONUS} from ability)` while the buff window is live. Bonus value comes from TAOM's own applied state (`CareerAbilityBuffTracker`, valid because of #377) — never reflection; share math in `AbilityDamageAttribution` (`dmg·f/(1+f)`, exact while the ability buff is the sole multiplicative term; the passive Damage effect uses `CalculateDamageAmplification`, a different mechanism this line does not claim). Utility abilities print a once-per-activation "boosts something other than damage" notice instead of silence. Threshold `min_reportable_bonus_damage` (default 0.5) on `<Global>` in `taom_ability_tuning.xml`.
- **#384 diagnostics:** `taom.print_hud_layout [maxNodes]` console command dumps the top screen's widget tree with real on-screen rectangles to `Logs/taom_hud_layout.log` (bounded, on-demand, collapsed-tree warning) — the reference module's layout dumps found three bugs code reading did not.
- **Review pass (same session, RCA `docs/reviews/rca-career-ux-arc-2026-08-05.md`):** 5-agent deep review + Codex xhigh (Review 81, 0 P1/2 P2, all six architecture suspects disputed with decompiled evidence). Fixes folded in: hero-death now snapshots buffed allies (`GetBuffedAllyIndices`) and forces `UpdateAgentProperties` on each (the cleared dictionary can't refresh engine-side baked stats); the career-hero identity predicate is single-sourced in `CareerHeroIdentityGate` (three sites, one had drifted); the `OnScoreHit` body lives in `AbilityDamageAttributionReporter` (per-mission boundary reporter); `isSiegeEngineHit` is filtered (siege missiles arrive with the operating player as affector — attributing ability bonus there would be a false claim); **activation is blocked while the active window is live** (`IsAbilityActive` gate — Codex proved olog_hai's Duration mutations reach a 16s window against a 15s cooldown floor, a 1s recast that would double-stack contributions); the badge text brush is the registered `CareerSystem.Badge.Text` (the copied `ButtonBrush1.Text` never existed — `BrushFactory` nulls silently; rule widened in `gui-ui.md`).
- **Install hygiene (audited 2026-08-06):** the reference package's `source/_Module/` mirrors TAOM's own layout, and during evaluation its files were copied into the live `Modules/TAOM/` — the installed `CareerScreen.xml` was byte-identical to the contributor's 466-line diamond rewrite, alongside their blanked `AbilityHUD.xml` and a clone of vanilla's `Mission/AgentStatus.xml`. A plain `./build.ps1` restored the canonical career screen, but the deploy runs `Clean="false"` and **cannot remove** files the repo no longer ships: `GUI/Prefabs/Mission/AgentStatus.xml` (shadows vanilla's combat HUD and references `CareerEnergyBarWidget`, a class only the reference DLL defines) and `GUI/Prefabs/CareerSystem/AbilityHUD.xml` must be deleted from the game install by hand. TAOM ships neither — the energy bar is injected by `CareerEnergyBarPrefab` at runtime, never by shipping a vanilla prefab clone. Vanilla's own `Modules/Native/.../AgentStatus.xml` was verified untouched. Lesson: `lessons/build-tooling-workflow.md`.
**2026-08-06 update — #388 diamond career screen.** The May pip-strip layout was replaced with a diamond grid after seeing both in-game: each choice is a diamond carrying an icon (five per group, two groups per tier), with a persistent **Active Effects** column in the right gutter. **Rank titles and lore group names are kept** — the diamond layout has room for both. Ported as TAOM-owned code from the reference module's prototype, dropping ~800 of its ~940 lines: its `CareerScreenStatePatch` (494) Harmony-scraped `CareerScreenVM` from outside and its `CareerEffectsPanelWidget` (146) polled a static because it could not bind — from inside, the VM just publishes `KeystoneEffectLines` / `PassiveEffectLines` (passives summed per effect type, so two +5% Damage picks read as one "+10% damage"), accumulated inside the existing `RebuildChoiceGroups` walk rather than a second registry pass.
- **Icons are banner icons, and need no sprite bake.** The per-choice sprites authored in `taom_career_choices.xml` (`career_choice_*`) were never drawn — 0 PNGs, 0 atlas entries — which is exactly why `IconSprite` was dead data bound by no prefab. `CareerChoiceObjectVM.IconSprite` now resolves to an already-baked banner icon: keystones show their career's own sigil (#380's `keystone_icon`), passives an icon for their effect type (`CareerEffectDisplayMap`).
- **Percent vs flat is keyed on effect TYPE, not magnitude size.** The reference used `|value| < 1 means percent`, which prints a 1.0 magnitude (=100%) as "+1" and a +1 companion limit as "+100%". TAOM authors Damage/Ammo/MovementSpeed as fractions and Health/PartySize/MountHealth as counts.
- **Custom widget registration is automatic.** `WidgetInfo.CollectWidgetTypes()` scans every loaded assembly that references TaleWorlds.GauntletUI and collects all `Widget` subclasses (v1.4.7 decompile) — defining `TaomCareerDiamondWidget` inside TAOM.dll is sufficient, there is no register call and `UIExtender.Register` does NOT do it (it only picks up `[PrefabExtension]`/`[ViewModelMixin]`). **The `Taom` prefix is load-bearing:** `WidgetFactory._builtinTypes` keys on the SIMPLE type name across assemblies, ignoring namespace, so an unprefixed `CareerDiamondWidget` would collide with the reference module's class if both were ever loaded.
- **Layout, after two in-game passes (2026-08-06).** The career info is a **header bar across the top** (name + description | portrait | ability icon, name and effect lines) — as a full-height left column it ate the width the grid needed. Diamonds are 70px, the boxed group panels are gone (a group is its lore name above its row on the open background), and each diamond carries the bronze border sprite tinted by state: dim locked, bronze available, bright gold taken (the widget drives the taken glow). Rank title and its "Requires Level N" share one line above the groups; Free Points / Active Effects is a right-hand column. The tooltip is anchored above its diamond with `RenderLate` so it draws over neighbouring rows, not under their chrome.
- **Diamond rims are per-tier metals** (2026-08-06): tier 1 bronze → tier 2 silver → tier 3 gold, so the tree reads as a progression at a glance. Each tier owns three border colours — dim (locked) / mid (available) / bright (taken glow) — and they are deliberately NOT shared between tiers, so changing one tier's metal means editing all three of its values. The full hex table is in a comment above the body block in `CareerScreen.xml`. The taken glow is shown by `TaomCareerDiamondWidget` rather than a binding, so it is a plain colour on the widget's `DiamondGlow` child.
- **Selection is a toggle, and that is load-bearing.** Dropping the +/− button column left no way to refund a pick, so the diamond click calls `CareerChoiceObjectVM.ExecuteToggleChoice` — take an untaken choice, refund a taken one. Both directions were already guarded (free points, tier locks and keystone exclusivity going in; a no-op on an untaken id coming out). A regression test pins it: without the toggle every taken choice is permanently stranded.
- **The screen is FIXED WIDTH (1720) and CENTRED — do not restore `StretchToParent`.** On a 32:9 monitor a stretching container made the layout as wide as the display, so every fixed-width child clustered left, the header bar ran into dead space, its ability text drifted past centre and clipped, and the Active Effects column stranded far from the grid. 1720 is sized to hold the body (1330 tiers + 20 + 340 column) and the header (700 + 290 + 24 + 92 + 16 + 560 = 1682); changing any of those widths means re-checking that sum. Lesson: `lessons/localization-ui.md`.
- **Owed:** in-game smoke checklist (plan `review-this-and-identify-bubbly-moon.md` §Verification), 12-language `/localize` for `taom_career_dmg_attrib`/`taom_career_dmg_none` (English registered; translation env-gated on `ANTHROPIC_API_KEY`), keystone-icon art curation pass, pre-existing `CharacterDeveloper.SkillNameText`/`.DescriptionText` brush cleanup (RCA finding #3).

**2026-08-06 — #390: the `Health` pip only ever worked in battle.** A player took a "+75 max health"
choice and the character screen still read `Max. Hit Points 100 / Base +100`. `PassiveEffectType.Health`
had exactly one consumer, `TaomAgentStatCalculateModel.GetEffectiveMaxHealth` — the **mission**
`AgentStatCalculateModel` slot. Everything the player reads on the campaign layer (the tooltip,
`Hero.MaxHitPoints`, `MapInfoVM`, the daily heal cap, the wounded threshold) goes through
`CharacterStatsModel.MaxHitpoints`, and `TaomCharacterStatsModel` overrode only `MaxCharacterTier`.
So the pip applied in battle and was invisible + inert everywhere else.

- **Fixed by moving the add, not copying it.** `TaomCharacterStatsModel` now takes
  `ICareerPassiveService` and applies `Health` via `ApplyFlat` on `MaxHitpoints`, and the hero branch
  was **deleted** from `CareerAgentStatService` (`ApplyMaxHealthPassives` → `ApplyMountHealthPassives`,
  mount-only). The trap: `SandboxAgentStatCalculateModel.GetEffectiveMaxHealth` opens with
  `if (agent.IsHero) return agent.Character.MaxHitPoints();`, which *is*
  `CharacterStatsModel.MaxHitpoints`. Keeping both adds turns a +75 pip into +150 in battle
  (100 → 175 → 250). A unit test pins that the agent-stat path never reads `Health` again.
- **Engine signature, verified on the installed v1.4.7 DLL:**
  `MaxHitpoints(CharacterObject character, bool includeDescriptions = false)` — the second parameter
  is a `bool`, **not** a `StatExplainer`. A binding test in `GameModelOverrideBindingTests` pins both
  the override's existence and that signature; the two generic gates in that file cannot catch its
  removal, because `MaxCharacterTier` alone keeps "declares an override" green.
- **The phantom gate could not have caught this.** `PassiveEffectConsumers` answers "is anything
  reading it", not "is it read on the layer the player sees" — `Health` was listed as consumed for
  the whole life of the feature. The blind spot is now documented in that file's header.
- **Audit of the other effects (2026-08-06, same session):** all 22 other shipped `PassiveEffect`
  types have a live read site at a layer that matches their wording, and magnitudes are unit-correct
  (fractions for the `ApplyFactor` types, flat counts for `Health`/`PartySize` — no `is_percentage`
  mismatch, despite that attribute being parsed and never read). All 302 keystone `<Mutation>` entries
  resolve: 3 distinct properties (`Duration`/`Radius`/`CooldownReduction`), all present as floats on
  `AbilityTemplateData`, and all 50 `target_id`s match a real ability template. `Health` was the only
  broken effect.
- **Out of scope, flagged:** `WoundedHitPointLimit` stays at vanilla's flat 20. Current `HitPoints` is
  stored state and stays where it was on an existing save; the hero heals up to the new maximum over
  campaign days.
- **Follow-on rebalance (same day):** the pips were authored for a mission-only consumer, where the
  number only ever moved a health bar, so they had grown to +75/+100 on tier 3. Against vanilla's flat
  100 base that is a +75% campaign swing, so all 165 dropped to a **5-10 band** via
  `tools/retune_career_health.py` (25→5, 30/35→6, 40/45→7, 50/60→8, 75→9, 100→10; root/t1 5-7, t2 6-8,
  t3 8-10). The tool rewrites the magnitude, the English description, the `taom_career_strings.xml`
  source string AND the 1,968 translated strings in all 12 languages — a Health pip's number appears on
  four surfaces and a retune that moves only the first makes the other three lie. It swaps translations
  directly rather than deferring to `translate_with_claude.py` (the `retune_phantom_descriptions.py`
  precedent) because only a digit changes and every translated health string was verified to carry
  exactly one number; keyed on the old magnitude, so re-running is a no-op. `far_harad_halftroll_root`
  says "massive health bonus" with no number and is deliberately left alone.

## Overview

Career/class progression system where each hero can have a career that provides passive bonuses, an active ability, and a 3-tier choice tree. 50 LOTR-themed careers across 16 factions, fully XML-driven. Each career has 31 choices (1 root + 6 groups x 5 choices) with keystones, passives, and ability mutations.

## Why This Exists

- **Vanilla behavior:** Bannerlord has perks but no career/class system with branching choice trees or active abilities
- **TAOM requirement:** Middle-earth factions need distinct playstyles beyond cultural feats — a Mordor Warboss should feel different from a Gondor Knight
- **Without this feature:** All heroes of the same culture play identically after initial perk selection

## Architecture

### Design Challenge

A hardcoded-C#-career design (static singletons + ViewModel Harmony patches) was rejected. TAOM needs XML-driven careers (add without recompilation), DryIoc injection, adapter pattern compliance, and UIExtenderEx integration.

### Solution Approach

- **Data model:** Plain C# classes (not PropertyObject) loaded from XML via `ICareerConfigProvider`
- **Persistence:** `CareerPersistenceBehavior` flattens each hero's `HeroCareerData` into **four** primitive `Dictionary<string, string>` stores — `_taom_careerIds`, `_taom_careerChoices` (comma-joined choice ids), `_taom_careerTiers`, `_taom_careerFlags`. Deliberately primitive: it avoids a `SaveableTypeDefiner` for the choice data (the `CareerQuest` objects do use one, `CareerQuestSaveableTypeDefiner`). The load path is gated on `if (!dataStore.IsLoading) return;` — before that guard, the reconstruct also ran during SAVES and clobbered `_heroData` mid-pass. *(Corrected 2026-08-06: this line previously described a single `_taom_careerData` store of `Dictionary<string, HeroCareerData>`, which never matched the code.)*
- **Passive application:** `ICareerPassiveService` caches per-hero effect magnitudes, `CareerPassiveHelper` wires into 8 existing GameModels
- **Mutations:** Hybrid XML + C# calculator registry — XML defines target/params, C# provides calculator functions by ID
- **UI:** `GauntletCareerScreen` with `CareerScreenVM` hierarchy (expandable panels, portraits, ability icons), `CharacterDeveloperCareerMixin` (UIExtenderEx) for career button with sprite. See [gui-sprite-system.md](gui-sprite-system.md) for full UI details. **Screen revamp 2026-05-30** ([RCA](../reviews/rca-career-ui-revamp-2026-05-30.md)): tiers ordered Tier 3 (top) → Tier 1 (bottom); locked tiers show a **"Requires Level N"** label (level from `CareerRegistry.GetTierUnlockLevel` — T1/1, T2/10, T3/20) instead of the old gate art; each node is an always-visible **point-pip strip** (3 brightness states — taken / available / empty — via `CareerChoiceObjectVM.IsUnavailable`) with perk descriptions on hover, using the shared `CareerSystem\career_point_pip` One Ring sprite; tier headers show per-career **rank titles** (`CareerDefinition.Rank1/2/3Name`, fallback "Tier N"); node headers show per-group **lore names** (`CareerChoiceGroupDefinition.DisplayName`, humanized-id fallback). 294 group names + 147 rank titles are web-researched Tolkien-grounded (`tools/career_group_names.json`, `tools/career_rank_names.json`). **In-game pass 2026-05-31:** names singularized (single-player career → singular titles, e.g. "Warden of the East Bank" not "Wardens"; via `tools/singularize_career_names.py`); tier rank labels set flush-left (`CoverChildren`+Left, no wrap-indent); locked-tier node spacing matched to Tier 1 by reserving the `+`/`−` button column (fixed 70px, buttons gated on `@IsActive`). **Pip sprite — two fixes (bake + render):** the pip first needed the offline sprite generator to bake it into the `ui_taom_career_system` atlas (`AssetSources/GauntletUI/...png` + `Assets/GauntletUI/..._tex.tpac` — **not** a `pack0.tpac`), and then a prefab fix because even baked it rendered invisibly at 22×28px/27% alpha (bumped to 38×38 + brighter opacities); the "Requires Level N" label was re-centered into the gap between the two node columns (`CoverChildren`+Center alone was insufficient — the 70px button reserve shifts the boxes left of row-center, so a `PositionXOffset="-40"` was needed); and the hover perk descriptions were made **inline with the pips** (the parallel pip + description `{Choices}` lists were given matching 46px rows and the description text switched to `CoverChildren`+left, so each description sits on its pip's row). **All confirmed working in-game (user screenshots, 2026-05-31).** **Taken-pip lit-ring follow-up (2026-06-19 → 22, issue #290):** the *taken* state now uses a brighter dedicated sprite `CareerSystem\career_point_pip_lit` (the One Ring ring whitened + a soft glow halo); available/locked stay on the shared hollow `career_point_pip` dimmed to `#FFFFFF55` / `#FFFFFF22`. The old scheme tinted all three states of the *same* hollow ring, so taken vs available was only a ~12% alpha gap — an increased skill never visibly "lit up." Gauntlet `Color` is a multiplicative tint (can't brighten past the sprite's own pixels), so a distinct brighter sprite was required, not just a tint change. Confirmed working in-game 2026-06-22. See [gui-sprite-system.md](gui-sprite-system.md) "The sprite-bake pipeline" (decompile-verified) + "Verifying a sprite (bake + render)" + the 2026-06-19 follow-up, and [RCA post-review in-game findings](../reviews/rca-career-ui-revamp-2026-05-30.md#post-review-in-game-findings-2026-05-31).
- **Battle:** `CareerPerkMissionBehavior` (thin entry point per ADR-002 — ~220 lines as of 2026-08-05, over the 150 ceiling as a recorded pre-existing condition; every handler is guards + delegation) delegates per-frame work to Singleton-lifetime controllers plus the per-mission `AbilityDamageAttributionReporter`:
  - [`IAbilityActivationController`](../../Main/Features/CareerSystem/Abilities/IAbilityActivationController.cs) — V-key + ready-state notification + charging-message throttle state machine. Returns an `AbilityActivationResult { JustBecameReady, Activated, Charging }` flags struct so the host can emit BOTH the green "ready" toast and the yellow "activated" toast on the same frame (legacy UX). Fully unit-testable via `IAbilityInputAdapter` + `IMissionTimeProvider` injection — no TaleWorlds statics.
  - ~~`IAbilityHudController`~~ — RETIRED 2026-08-05 (#382). The self-owned `GauntletLayer` HUD (and its `_attachedScreen` capture + try/catch/finally teardown discipline, worth remembering for any future custom layer) was replaced by the energy bar riding inside Native's AgentStatus movie — see the 2026-08-05 update above.
  - [`IAbilityEffectExecutor`](../../Main/Features/CareerSystem/Abilities/IAbilityEffectExecutor.cs) — per-activation pipeline: mutate template, apply CooldownReduction adjustment, allocate `MissionAbilityExecutionContext`, register with host's `_activeContexts` list, dispatch per-archetype effect executor, emit toast + sound + particles.
- **Singleton lifetime + per-step OnEndMission try/catch.** All three controllers are `Reuse.Singleton`; the host `CareerPerkMissionBehavior` is constructed fresh per mission. Cross-mission state (`_abilityReadyNotified`, `_hudInitialized` etc.) lives on the singletons and is cleared by explicit `Reset()` / `Cleanup()` calls in `OnEndMission`. Per the deep-review systemic finding, each cleanup op runs in its own `try/catch` so a throw in one (most plausibly `_hudController.Cleanup`) cannot abort the others. `CareerAbilityService` forces `ChargeType.CooldownOnly` for all 50 careers — readiness is purely cooldown-timer based (see [Cooldown System](#cooldown-system)).
- **Ability effects:** `CareerAbilityEffectRegistry` dispatches to per-career `ICareerAbilityEffectExecutor` implementations. 3 role-based archetypes (Infantry/Ranged/Cavalry) serve all 50 careers with XML-tunable values via `taom_ability_tuning.xml`. All three archetypes apply AoE friendly-troop buffs within a 50-unit radius (standardized in templates): Infantry (damage + damage reduction), Ranged (speed + ranged damage + draw speed), Cavalry (mount speed + charge damage + damage). Buffs applied via `CareerAbilityBuffTracker` with separate hero and ally buff dictionaries (read by `TaomAgentStatCalculateModel` — survives stat recalc).

### Component Diagram

```
taom_careers.xml / taom_career_choices.xml
        |
  CareerConfigProvider (loads XML)
        |
  CareerRegistry (lookup/eligibility/tier gating)
        |
  ┌─────┴──────┐
  |            |
CareerDataService   CareerPassiveService (cache)
(per-hero CRUD)         |
  |            CareerPassiveHelper → 8 GameModels
  |
CareerCampaignBehavior  CareerPerkMissionBehavior
(session/level/death)   (battle tick/charge/ability effects)
  |                           |
CareerCreationHandler   CareerAbilityEffectRegistry
(CC integration)        → InfantryAbilityExecutor (AoE damage + reduction)
  |                     → RangedAbilityExecutor (speed + ranged dmg + draw)
  |                     → CavalryAbilityExecutor (mount speed + charge + dmg)
  |                     → CareerAbilityBuffTracker (hero + ally buffs, read by stat model)
CareerSwitchService     → MissionAbilityExecutionContext (boundary adapter)
(NPC dialogue switching)
  |
GauntletCareerScreen → CareerScreenVM → CareerChoiceGroupObjectVM → CareerChoiceObjectVM
                                      → CareerAbilityEffectVM (ability effects list)
```

## Configuration

### Career Definitions (`Main/_Module/ModuleData/career_system/taom_careers.xml`)

Defines careers with: id, display name, description, portrait sprite, ability template ID, eligible cultures, choice group IDs, root choice id, min clan tier. **`rank1_name` / `rank2_name` / `rank3_name`** (added 2026-05-30) — per-career tier-header rank titles shown on the career screen (e.g. `Ohtar → Roquen → Knight of the Golden Flower`); optional, the VM falls back to "Tier N". `max_perk_points` attribute on root element (default 30). (Pre-2026-05-04 the schema also had `charge_type` and `max_charge` — both removed; cooldown is global, not per-career.)

### Choice Trees (`Main/_Module/ModuleData/career_system/taom_career_choices.xml`)

Defines standalone root choices and choice groups. Each group has a tier (1/2/3), an optional **`display_name`** (added 2026-05-30 — the per-group lore "path" name shown as the node header, e.g. `Watchers of Henneth Annûn`; the VM humanizes the id as fallback), and contains choices (Keystone or Passive). Choices can have PassiveEffect (type + magnitude + operation) and Mutations (target template + property + calculator + params).

**Two PassiveEffect schemas are accepted** (`CareerConfigProvider.ParseChoice`):
- **Direct (preferred):** `<Choice ...><PassiveEffect type="X" magnitude="0.10" attack_type_mask="Melee" /></Choice>`
- **Wrapped:** `<Choice ...><PassiveEffects><PassiveEffect type="X" value="0.10" /></PassiveEffects></Choice>` — the parser reads the *first* `<PassiveEffect>` inside the plural `<PassiveEffects>` wrapper (one child only — multi-child wrappers silently drop the rest), and accepts `value=` as an alias for `magnitude=` (`magnitude=` wins when both are present). The wrapped form was historically unparsed (310 dead choices across all 16 cultures); fixed 2026-05-29 — see [RCA](../reviews/rca-career-partysize-2026-05-29.md).

`PassiveEffect` carries `type`, `magnitude`, and `attack_type_mask`. The legacy `operation`/`is_percentage` attributes were **parsed-but-never-read** and were removed 2026-06-25 (ignored if still present in XML).

**Magnitude scale ↔ application method (IMPORTANT).** `CareerPassiveService` stores the summed `Magnitude` per type (`GetPassiveMagnitude`) plus a per-(type, mask) breakdown (`GetMaskedMagnitude`); each consumer chooses flat vs factor by calling `ApplyFlat` (`result.Add`) / `ApplyFactor` (`result.AddFactor`) or applying the magnitude directly. So a passive's authored magnitude scale MUST match its consumer's method:
- **Fractional magnitude (0.10 = +10%)** → `ApplyFactor` / multiplicative. The convention for almost every type (TroopWages, PartyMovementSpeed, TroopMorale, etc.).
- **Whole-count magnitude (2 = +2 units)** → `ApplyFlat`. Only **`PartySize`** (`TaomPartySizeModel`) and the agent-stat flat `Health` / `CompanionLimit`. Applying a whole-count via `ApplyFactor` multiplies the base (`AddFactor(2)` = ×3) — the "+2 → +150" bug fixed 2026-05-29.

**attack_type_mask (Damage + Resistance).** `Damage` and `Resistance` honor the mask — a "+X% ranged damage" pip fires only on ranged hits. The damage path derives the hit's delivery type (`IsMissile ? Ranged : Melee`) and `GetMaskedMagnitude` sums every authored-mask bucket that intersects it (an `All`-masked entry applies to both). `Damage` is applied multiplicatively on the per-hit **amplification** path (`CareerAgentStatService.CalculateDamageAmplification`), NOT as a flat `DamageMultiplierBonus`, so the mask can gate it. `Blunt`/`Cut` masks (5 shipped Resistance pips) aren't representable in the `[Flags]` enum and deliberately degrade to `All` (every hit).

**Effect-type consumers.** Every `PassiveEffectType` used by a shipped pip now has a runtime consumer — `PassiveEffectConsumers` is the compiled source of truth. The six historically-dead/phantom types — `Ammo`, `HorseChargeDamage`, `HorseHealth`, `TroopResistance`, `StealthBonus`, `HealthRegeneration` — were wired 2026-06-25 (HorseHealth + Ammo are multiplicative; consumers span `CareerAgentStatService`, `TaomAgentStatCalculateModel`, `TaomAgentApplyDamageModel`, `TaomMapVisibilityModel`, `TaomPartyHealingModel`, `CareerPerkMissionBehavior.OnAgentBuild`), and their ~211 magnitudes re-tuned to a uniform 10–15% band via `tools/retune_phantom_passives.py`. A load-time gate (`CareerConfigProvider.ValidatePassiveConsumers`) + a shipped-XML regression test (`CareerChoicesIntegrationTests`) now prevent a new phantom type from shipping silently. Pure arithmetic for the Ammo + StealthBonus consumers lives in the testable `CareerPassiveMath`.

### Ability Templates (`Main/_Module/ModuleData/career_system/taom_ability_templates.xml`)

Defines per-ability tunables: id, display name, duration (effect window), radius (AoE), `max_charge` (dead since #103 cooldown rework — present on the model for back-compat but unread by activation logic; designer mutations targeting it were repurposed as `CooldownReduction` in #104), `cooldown_reduction` (per-activation cooldown shortening in seconds, applied AFTER mutations have run; floored at `MinCooldownSeconds`), particle/sound effects, tooltip. Cooldown is *not* per-template; see [Cooldown System](#cooldown-system).

### Cooldown System

`Main/_Module/ModuleData/career_system/taom_ability_tuning.xml` declares a `<Global>` element shared by all 50 careers. Two tunables:

```xml
<AbilityTuning>
  <Global cooldown_seconds="30" min_cooldown_seconds="5" />
  <Infantry .../>
  <Ranged .../>
  <Cavalry .../>
</AbilityTuning>
```

- **`cooldown_seconds` — Default 30s.** Validation: must be in `(0, 3600]`. Non-finite (`NaN`/`±Infinity`), out-of-range, malformed, or missing values fall back to 30s with a `LogWarning` (`CareerConfigProvider.ParseGlobalTuning`). NaN guard precedes the range check because IEEE-754 NaN comparisons always yield false (see `feedback_clamp_nan_infinity_propagates.md` — bug has shipped three times).
- **`min_cooldown_seconds` — Default 5s (#104).** Validation: must be ≥ 0 AND ≤ `cooldown_seconds`. Floor for designer `CooldownReduction` mutations — prevents stack-up cheese where 4+ keystones with `-9` reductions would zero the cooldown. Same NaN/Infinity guards as the cooldown itself.
- **Reload scope:** `CareerConfigProvider` is a `Reuse.Singleton` and caches the parsed config. Changes require a full Bannerlord application restart — not a save-load.
- **Per-career override:** Not supported at the tuning-config layer. **Per-keystone CooldownReduction is supported** — see CooldownReduction Mutations below.

#### CooldownReduction Mutations (#104, Option B)

A keystone choice can shorten the global cooldown for the next activation via a `<Mutation>` targeting the new `CooldownReduction` property on `AbilityTemplateData`:

```xml
<ChoiceGroup id="...">
  <Choice id="..._keystone_t1" type="Keystone">
    <Mutations>
      <Mutation target="cooldown_reduction" property="CooldownReduction" calculator="flat" value="6"/>
    </Mutations>
  </Choice>
</ChoiceGroup>
```

- **`6` for tier-1 keystones, `9` for tier-2/3 keystones** is the convention. Effective cooldown = `max(min_cooldown_seconds, cooldown_seconds - reduction)`. With defaults: tier-1 = `max(5, 30-6) = 24s`; tier-2/3 = `max(5, 30-9) = 21s`. Stacks of 4 tier-2/3 keystones (which would sum to 36) floor at 5s.
- **Application order in `AbilityEffectExecutor.Execute`:** the per-activation pipeline calls `MutateTemplate` to apply all choice mutations to a cloned template, runs the per-archetype effect executor, THEN calls `_abilityService.ApplyCooldownAdjustment(heroId, template.CooldownReduction, min)`. The adjustment runs AFTER `executor.Execute` so a throw from the effect executor (e.g., `ApplyAoeBuff` mid-iteration) does not shorten the cooldown for an activation whose effect did not fully apply. The HUD pip on the next frame still sees the adjusted cooldown.
- **Designer convention — POSITIVE values only.** `<Mutation property="CooldownReduction" ... value="6"/>` means "shorten the next cooldown by 6 seconds." The `flat` calculator is `baseValue + value` (`BuiltInCalculators.cs:7-8`), so the mutated template's `CooldownReduction` is positive 6. `AbilityEffectExecutor` guards on `template.CooldownReduction > 0f` before calling `ApplyCooldownAdjustment`. A negative value would be silently rejected by that guard. Codex pass-2 (2026-06-02) caught a HIGH where the first XML rewrite emitted `-6`/`-9` and the entire feature was a no-op; fixed by re-running the script with the positive-value variant.
- **History.** 98 mutations originally targeted `MaxCharge` (-20 / -30) under the charge-based system. After the #103 cooldown rework `MaxCharge` became unread; #104 Option B rewrote them via `tools/rename_maxcharge_to_cooldownreduction.py` (50 × 6, 48 × 9) preserving the 1.5× tier ratio. The `MaxCharge` property itself remains on `AbilityTemplateData` for back-compat — harmless dead but reflective-mutation pipeline (`MutationService`) would still write to it if a future XML re-introduces a mutation. Removing it is deferred (see Codex pass-2 LOW#2; tracked as future cleanup).

#### In-battle UX

- Abilities start ready at battle open.
- Pressing `V` while ready: yellow *"<Ability> activated!"* message + buff/sound/particle effect.
- Pressing `V` while on cooldown: throttled gray *"Career ability still charging — Ns remaining"* (one message per 2s).
- One-shot green *"Career ability is ready! Press V to activate"* when the cooldown elapses. On the frame the ability becomes ready AND V is pressed simultaneously, BOTH the green ready toast and the yellow activated toast emit (preserving legacy UX — see `AbilityActivationResult` flags struct).

**HUD placement (HISTORICAL — panel retired 2026-08-05 by #382).** The old on-screen ability panel (icon + "Press V" prompt + charge bar) was a standalone Gauntlet layer loaded from `AbilityHUD.xml` by `AbilityHudController` (both deleted). Its right-anchoring lesson survives and applied to the replacement: the vanilla health/ammo HUD is pinned to the screen's right edge, so a `Center`-anchored panel drifts far left on ultrawide displays — the energy bar avoids the whole problem by living INSIDE Native's AgentStatus prefab (see the 2026-08-05 update above), inheriting the cluster's anchoring, `@IsCombatUIActive` visibility, and shrink animation. Bar placement knobs are in `CareerEnergyBarPrefab.cs` (`MarginRight="95"` / `MarginBottom="208"`, prefab units — GUI-live-only, only the running game confirms placement).

### Starting Equipment Override (per-archetype)

After the culture-default starting roster is applied at `OnCharacterCreationFinalize`, the player's career archetype drives a second roster application that overwrites the loadout. The archetype is one of three values:

| Archetype | Weapons | Armor |
|-----------|---------|-------|
| **Ranged** | bow + arrows + sword | light (low armor, very low weight) |
| **Cavalry** | spear + shield + sword + horse + harness | medium (chainmail) |
| **Infantry** | 1H + shield + (2H or spear — culture-decides) | heavy (plate-tier weight) |

**Single source of truth:** [`CareerSystemIoC.GetCareerArchetypeMap()`](../../Main/Features/CareerSystem/CareerSystemIoC.cs) maps each careerId to a `CareerArchetype`. The same dictionary is consumed by the ability executor registry (Infantry/Ranged/Cavalry executors) and by [`ICareerArchetypeService`](../../Main/Features/CareerSystem/ICareerArchetypeService.cs). Cached in a static field — one allocation per app lifetime.

**Roster ID convention:** `player_career_{cultureId}_{infantry|ranged|cavalry}_{f|m}`. Built by [`CareerEquipmentRosterIds.Build`](../../Main/Features/CharacterCreation/CareerEquipmentRosterIds.cs), looked up via `MBObjectManager.GetObject<MBEquipmentRoster>`. Rosters are authored in [`Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml`](../../Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml).

**Graceful fallback:** When no roster exists for a given (culture, archetype, gender) combination, [`CareerStartingEquipmentService`](../../Main/Features/CharacterCreation/CareerStartingEquipmentService.cs) logs a warning and leaves the already-applied culture default in place. This lets new cultures ship incrementally without code changes.

**Live preview during CC** ([`CareerMenuService.UpdateCareerEquipmentPreview`](../../Main/Features/CharacterCreation/CareerMenuService.cs)): when the player clicks a career option, two preview updates fire because two surfaces render the character from different sources:

1. **Career menu 3D agent** reads from the menu's `NarrativeMenuCharacter` buffer. Updated via `NarrativeMenuCharacter.SetEquipment(roster)` on the `player_career_character` — same pattern as `NarrativeMenuBuilder.UpdateYouthEquipment`.
2. **Review stage 3D agent** ([`CharacterCreationReviewStageView.AddCharacterEntity`](file:///E:/Decompiled_Bannerlord/Modules/SandBox.GauntletUI/SandBox.GauntletUI.CharacterCreation/CharacterCreationReviewStageView.cs)) reads from `Hero.MainHero.CharacterObject.Equipment` directly. Updated by running the same two-step apply chain that `OnCharacterCreationFinalize` does: `IPlayerEquipmentService.ApplyPlayerStartingEquipment` (resets to culture+title default) → `ICareerStartingEquipmentService.ApplyCareerStartingEquipment` (overlays career roster). This way switching careers (cavalry → ranged) starts from a clean culture-default slate rather than inheriting the previous career's overrides.

Same fallback policy as the runtime grant: missing roster → log + leave the youth/culture-default preview in place.

**Critical: `FillFrom` does NOT clear unspecified slots.** `Equipment.FillFrom(source)` copies only the slots that are present in the source roster — it does not zero-clear the target's other slots first. This means if your culture-default roster sets a Horse and your career roster does not mention Horse, the horse persists. For archetypes that should be on foot (ranged, infantry), include explicit empty overrides:

```xml
<Equipment slot="Horse" id="" />
<Equipment slot="HorseHarness" id="" />
```

The empty `id=""` resolves to a null `ItemObject`, which `Equipment.DeserializeNode` accepts as an empty slot.

### How to add a new culture's career rosters

1. Create starter armor items in LOTRLOME_Armory at `LOTRLOME_items/<culture>/starter_armors.xml` — 15 items total (3 archetypes × 5 slots: head/body/leg/cape/gloves). Reuse existing meshes; vary weight + armor stats per archetype (ranged ≈ 0.5× source weight, cavalry ≈ 0.75×, infantry ≈ 1.0×). Use the `starter_{archetype}_{culture}_{slot}_a` naming convention — see Gondor [`starter_armors.xml`](file:///E:/Steam/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/starter_armors.xml) as the template.
2. **Required cover attributes** — LOTRLOME armor items render their mesh only when the `Armor` element declares it covers the slot:
   - Head items: `hair_cover_type="..."` + `beard_cover_type="..."` (cloth → `type1`/`type2`, plate → `type1`/`all`)
   - Body items: `covers_body="true"` (required) plus optionally `covers_legs="true"` for long robes / `covers_hands="true"` for full gauntlets that extend past the arm
   - **Leg items: `covers_legs="true"` is REQUIRED** — without it the leg mesh does not render, the player appears with bare legs even though the item is equipped
   - **Glove items: `covers_hands="true"` is REQUIRED** — same failure mode for hands
   - Cape items: no cover attribute needed
   - Source-of-truth: cross-check against any existing LOTRLOME `{leg,arm}_armors.xml` entries — every leg item has `covers_legs="true"` and every glove item has `covers_hands="true"`. Don't omit these on duplicates.
3. **Path encoding trap** — the LOTRLOME_Armory path on Windows contains `&` (`Mount & Blade II Bannerlord`). The Write tool has been observed entity-encoding `&` → `&amp;` and silently writing to a phantom directory. After authoring, `ls` the real path to confirm. See `feedback_write_tool_ampersand_path_encoding.md`.
4. Append 6 rosters to [`taom_career_starting_equipment.xml`](../../Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml) — one per (archetype, gender). Reference existing low-tier culture weapons + the new starter armor. **Don't forget the explicit Horse/HorseHarness clears for ranged + infantry** — `Equipment.FillFrom` is a slot-by-slot merge and will leave culture-default horses in place if you don't override.
5. Verify the archetype for each career in [`CareerSystemIoC.GetCareerArchetypeMap()`](../../Main/Features/CareerSystem/CareerSystemIoC.cs) — adjust if needed.
6. No code change required — `ICareerStartingEquipmentService` looks up by string-id at runtime.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CareerSystem/Domain/` (11 files) | Enums + immutable data classes |
| `Main/Features/CareerSystem/ICareerDataService.cs` | Per-hero career state CRUD |
| `Main/Features/CareerSystem/CareerConfigProvider.cs` | XML config loading |
| `Main/Features/CareerSystem/CareerRegistry.cs` | Career lookup, eligibility, tier gating |
| `Main/Features/CareerSystem/CareerPassiveService.cs` | Session-scoped passive effect cache |
| `Main/Features/CareerSystem/CareerPassiveHelper.cs` | Static helper wiring passives into GameModels |
| `Main/Features/CareerSystem/Mutations/` (6 files) | Calculator registry + built-in calculators + mutation service |
| `Main/Features/CareerSystem/Abilities/` (16 files) | CareerAbility (now with `AdjustCooldown`), ability service (now with `ApplyCooldownAdjustment`), effect registry, 3 executors, buff tracker, execution context, + 6 new files for #102: `IAbilityActivationController` / `AbilityActivationController` / `IAbilityHudController` deps `IAbilityInputAdapter` + `IMissionTimeProvider` + impls, `IAbilityEffectExecutor` / `AbilityEffectExecutor` |
| `Main/Features/CareerSystem/UI/AbilityHudController.cs` | HUD lifecycle controller (boundary class — verified in-battle) |
| `Main/Features/CareerSystem/CareerCampaignBehavior.cs` | Campaign lifecycle events |
| `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` | Thin entry point (139 LOC, ADR-002 compliant — refactored #102) — delegates to the three controllers + owns `_activeContexts` expiration list + `OnEndMission` per-step try/catch teardown |
| `Main/Features/CareerSystem/CareerCreationHandler.cs` | Character creation integration |
| `Main/Features/CareerSystem/CareerSwitchService.cs` | Career switching with validation |
| `Main/Features/CareerSystem/UI/` (7 files) | Career screen + VM hierarchy + UIExtenderEx mixin + ability HUD + prefab. See [gui-sprite-system.md](gui-sprite-system.md) |
| `Main/Features/CareerSystem/Models/` (3 files) | TaomAgentApplyDamageModel, TaomAgentStatCalculateModel, TaomClanTierModel |
| `Main/Adapters/ICareerHeroAdapter.cs` | Wraps Hero for service boundary |
| `Main/Adapters/ICareerHeroAdapterFactory.cs` | Factory for GameModel boundary |
| `Main/Features/CareerSystem/Domain/CareerArchetype.cs` | `enum CareerArchetype { Infantry, Ranged, Cavalry }` |
| `Main/Features/CareerSystem/CareerArchetypeService.cs` | careerId → archetype lookup; backed by static map in `CareerSystemIoC` |
| `Main/Features/CharacterCreation/CareerStartingEquipmentService.cs` | Applies archetype roster at end of CC over the culture default |
| `Main/Features/CharacterCreation/CareerEquipmentRosterIds.cs` | Roster ID builder: `player_career_{culture}_{archetype}_{f\|m}` |
| `Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml` | Per-(culture, archetype, gender) rosters; Gondor only as of 2026-05-19 |

## Dependencies

- DryIoc (IoC container)
- UIExtenderEx (UI injection)
- TaleWorlds.CampaignSystem (Hero, CampaignEvents, ExplainedNumber)
- TaleWorlds.MountAndBlade (MissionBehavior, Agent)
- TaleWorlds.Engine.GauntletUI (GauntletLayer, GlobalLayer)

## Tests

| Test File | Methods | Coverage |
|-----------|---------|----------|
| HeroCareerDataTests | 12 | Domain data class |
| CareerDataServiceTests | 17 | CRUD + persistence round-trip |
| CareerConfigProviderTests | 5 | XML parsing + missing file |
| CareerRegistryTests | 16 | Lookup + eligibility + tier gating |
| MutationCalculatorRegistryTests | 8 | All 5 built-in calculators |
| CareerPassiveServiceTests | 7 | Cache refresh + magnitude aggregation |
| MutationServiceTests | 5 | Template cloning + mutation application |
| CareerAbilityTests | 20 | Charge types + cooldown + activation + ReadyProgress01 |
| CareerAbilityServiceTests | 10 | Force-CooldownOnly + configured cooldown duration + GetCooldownRemaining (hero present/absent) + IsAbilityReady transitions |
| AbilityActivationControllerTests | 13 | V-key state machine — NoCareer no-op, JustBecameReady one-shot, simultaneous-flag emit, charging throttle window, Reset clears both flags |
| CooldownReductionTests | 15 | AbilityTemplateData copy-ctor, GlobalTuning ctor, AdjustCooldown happy/floor/zero/negative/NaN/Infinity/charge-based no-op, service ApplyCooldownAdjustment unknown-hero + floor |
| CareerCreationHandlerTests | 4 | CC flow + root choice |
| CareerSwitchServiceTests | 5 | Switch validation + choice reset |
| CareerScreenVMTests | 5 | VM state + choice selection |

## How-To

### Add a new career
1. Add `<Career>` element to `taom_careers.xml` with unique id, eligible cultures, choice groups
2. Add `<ChoiceGroup>` elements to `taom_career_choices.xml` (6 groups: 2 per tier, each with 1 keystone + 4 passives)
3. Add `<Choice id="xxx_root">` as the root choice
4. Add ability template to `taom_ability_templates.xml`
5. No C# changes required

### Add a new mutation calculator
1. Add function to `BuiltInCalculators.RegisterAll()`
2. Reference by id in XML `<Mutation calculator="your_id" ... />`

### Add a new PassiveEffectType
1. Add enum value to `PassiveEffectType.cs`
2. Add `CareerPassiveHelper.ApplyFactor/ApplyFlat` call in the relevant GameModel

### Retune the global ability cooldown
1. Edit `Main/_Module/ModuleData/career_system/taom_ability_tuning.xml` `<Global cooldown_seconds="N" />` (must be in `(0, 3600]`)
2. Restart Bannerlord (provider caches via `Reuse.Singleton`; save-load is NOT enough)

### Add a new ability icon
**All 49 enabled careers have icons as of 2026-07-07** (#101 closed by the named effect-icon set; only the disabled `far_harad_halftroll` lacks one). To add/replace: drop a 256x256 PNG at `Main/_Module/GUI/SpriteParts/ui_taom_career_system/CareerSystem/Abilities/<career_id>_ability.png`, then run the sprite bake (do NOT hand-edit `TAOMSpriteData.xml` — the generator rewrites it; see [gui-sprite-system.md](./gui-sprite-system.md) "The sprite-bake pipeline"). The icon id is string-built in C# as `CareerSystem\Abilities\{ability_template_id}` (`CareerScreenVM.cs` + `AbilityHudController.cs`); the `sprite=` attribute on `<AbilityTemplate>` is dead — nothing reads it. House style: full-bleed square "named effect-icon" — the ability's effect/emblem as a gritty painterly oil painting with the ability name hand-lettered across the bottom (no soldiers, no circular token framing).

## Effect-Scope Badges in Choice Tree (UX)

`CareerChoiceObjectVM.EffectScopeBadge` renders a small "While active" text next to keystone (mutation) bullets in the choice tree. The prefab gates the badge widget on `@IsKeystone` so passives render with no badge (the always-active default is the convention). The distinction matches the runtime: `PassiveEffect` choices flow through `CareerPassiveService.GetPassiveMagnitude` and are read by GameModel overrides on every relevant calculation (always active); `Mutation` (Keystone) choices are applied to a cloned `AbilityTemplateData` inside `ExecuteAbilityEffect` on V-press only — the clone is discarded once the buff window expires.

## Career-Switch Picker (dialogue → switch-mode screen)

When the player picks "I wish to discuss my career path" on any companion (under vanilla's `hero_main_options` dialogue token), the dialogue gate uses `ICareerRegistry.GetEligibleSwitchTargets(currentCareerId, hero)` to decide whether to show the option. The consequence opens `GauntletCareerScreen` in switch mode (via `CareerScreenGameState.IsSwitchMode = true`). The same screen renders normal-mode (perk tree) or switch-mode (picker) based on the flag; gates in `CareerScreen.xml` use `@IsNormalMode` and `@IsSwitchMode` to swap the middle area.

**Lifecycle gotcha (Codex Review #46):** Vanilla `GameStateManager.CreateState<T>()` invokes the screen ctor synchronously via `HandleCreateState → OnCreateState → CreateScreen → Activator.CreateInstance(type, state)` BEFORE `OpenCareerScreen` can set `state.IsSwitchMode = true`. Any new feature flag plumbed through `CareerScreenGameState` MUST be read in `OnInitialize`, not in the screen ctor. See [`GauntletCareerScreen.cs:OnInitialize`](../../Main/Features/CareerSystem/UI/GauntletCareerScreen.cs).

**Empty-state UX.** The picker's `ScrollablePanel` is gated on `@IsBrowsingTargets` (= `_isSwitchMode && targets.Count > 0`); the empty-state `TextWidget` bound to `@NoTargetsMessage` is gated on `@HasNoSwitchTargets` (= `_isSwitchMode && targets.Count == 0`). The dialogue gate prevents the empty-list state being reached via normal flow, but a static-entry-point or `_heroAdapter == null` reaches it; `RebuildEligibleSwitchTargets` logs a warning so a player report of "blank picker" is triagable. `IsBrowsingTargets` and `HasNoSwitchTargets` are computed expression-body properties; the VM fires `OnPropertyChanged` for both after `_eligibleSwitchTargets.Clear() + Add()` mutations (Gauntlet does not re-evaluate computed bools on collection mutation).

**Switch contract:** `ICareerSwitchService.SwitchCareer(heroStringId, hero, newCareerId)` clears old career + choices + tier unlocks + flags, sets the new career, adds the new root choice, refreshes the passive cache. `CanSwitch` rejects same-career switches at the boundary (`hero.StringId → _dataService.GetCareerStringId → ordinal-ignore-case == newCareerStringId`).

## Changelog

- 2026-07-07 — All 49 enabled careers got ability icons (closes the #101 art gap): "named effect-icon" style — the ability's effect/emblem as a gritty oil painting with the name hand-lettered in the art (user-generated via Midjourney from per-ability prompts; 256x256; baked into the `ui_taom_career_system` atlas). Battle HUD compacted: panel 220x132→130x166, icon 64→110, career-name line and black backdrop removed (icon + "Press V" + charge bar only). Renamed the `cave_troll_master` ability "Troll Frenzy"→"Gundabad Berserker" (English source; the 12 translation files are stale for those 8 strings until the next `/localize` run). Battle-HUD render verified in-game; career-screen render uses the same sprite id (not separately eyeballed).
- 2026-06-19 — Career-ability pips now visibly light up when a skill is increased: taken state uses a brighter dedicated `career_point_pip_lit` sprite (One-Ring ring whitened + glow halo) instead of a ~12% alpha bump on the shared hollow ring (#290).
- 2026-06-15 — Right-anchored the in-battle ability HUD beside the player health bar (`MarginRight="480"`) so it tracks the right edge and stays reachable on ultrawide displays.
- 2026-06-02 — Decomposed `CareerPerkMissionBehavior` 302→139 LOC into three Singleton controllers + two adapters; repurposed 98 dead `MaxCharge` mutations as `CooldownReduction` with a `min_cooldown_seconds` floor (closes #102, #104).
- 2026-06-01 — Added the "While active" effect-scope badge in the choice tree + dialogue-driven career-switch picker (#265); added the career-tied quest framework (1.4.5-verified) with Gondor proof-of-life.
- 2026-05-31 — In-game review pass on the career-screen revamp (singularized names, tier-label alignment, node spacing, pip bake + render fixes).
- 2026-05-30 — Career-screen UI revamp: Tier 3→Tier 1 ordering, "Requires Level N" locked-tier labels, always-visible point-pip strips, 441 web-researched lore names + per-career rank titles.
- 2026-05-29 — Fixed party-size passive applied flat instead of as a ×N factor; activated 310 dead wrapped-schema career passives.
- 2026-05-27 — Added starter-equipment rosters for every culture (12 new cultures × 6 archetypes).
- 2026-05-26 — Re-enabled `cave_troll_master` as Gundabad Berserker (Infantry); renamed Gundabad/Rivendell/Dunland/Isengard/Dale careers to Tolkien-flavored display names.
- 2026-05-24 — All 50 ability tooltips now state actual archetype effects + duration; added warg-mount cavalry starters (Isengard/Gundabad/Mordor/Dol Guldur); fixed Gondor cavalry starter.
- 2026-05-21 — Fixed Captain of Osgiliath Keystone descriptions to say "Career Ability" not "Sailing".
- 2026-05-20 — Review-stage 3D preview now matches career selection (#206).
- 2026-05-19 — Added archetype-driven starting equipment at character creation; removed orphan `career_menu.json` entries for disabled WIP careers.

_See the repository-root `CHANGELOG.md` for full chronological history._

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/battle-balance.md](./battle-balance.md)
- [docs/features/career-quest-system.md](./career-quest-system.md)
- [docs/features/starting-equipment-tuning.md](./starting-equipment-tuning.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/research/karpathy-autoresearch.md](../research/karpathy-autoresearch.md)
- [docs/reviews/rca-career-phantom-passives-2026-06-26.md](../reviews/rca-career-phantom-passives-2026-06-26.md)
- [docs/reviews/rca-career-starting-equipment-2026-05-19.md](../reviews/rca-career-starting-equipment-2026-05-19.md)

<!-- backlinks-end -->
