# Career System

**Status:** Verified in-game (2026-04-14). Career button with sprite on Character Developer screen, GameState-based screen opening (no crash), career selection in Character Creation. Gondor campaign tested.

**2026-05-04 update.** Ability activation rebuilt as a uniform 30-second cooldown timer. The original charge-based readiness model (DamageDone / Kills / DamageTaken accumulators) was replaced because per-archetype charge types produced confusing UX — defensive careers like Captain of Osgiliath only charged when the player took damage, so back-line players never saw the ability ready. See [Cooldown System](#cooldown-system) and `CHANGELOG.md` (issue #103).

**2026-06-02 update — issues #102 + #104.** `CareerPerkMissionBehavior` refactored from 302 → 139 LOC by extracting three Singleton-lifetime controllers (`IAbilityActivationController` + `IAbilityHudController` + `IAbilityEffectExecutor`) and two adapters (`IAbilityInputAdapter` + `IMissionTimeProvider`); the V-key state machine is now unit-testable end-to-end. The 98 dead `MaxCharge` mutations in `taom_career_choices.xml` were repurposed as `CooldownReduction` (50× -6, 48× -9) targeting a new property on `AbilityTemplateData`; effective cooldown is `max(MinCooldownSeconds=5, 30 - reduction)`. Two Codex adversarial reviews + a 5-dimension Claude deep-review fan-out were applied; all 17 actionable findings triaged.

**2026-08-05 update — issues #377–#384 (career UX arc, adopted from external submission `TAOM-Career-UX-Upstream-2026-08-05` via `/adopt-external`; reviewed text ported, module/DLL never installed).**
- **#377 runtime fixes:** buff-tracker entries are now contribution-counted (`AddContribution`/`RemoveContribution`) and retire when the last restore fires — `GetBuff(heroId)` non-null now MEANS "ability window live" (pre-fix a zeroed entry lingered to mission end; `ExpiresAt`/`IsExpired` were written-never-read and are deleted). `CareerAbility` gained the active window (`BeginActiveWindow` from the executor with the same mutation-extended duration that schedules the restores; `ActiveDuration`/`ActiveRemaining`/`IsActive`/`ActiveProgress01`, NaN-guarded). A controlled-agent identity gate (`Mission.MainAgent.Character == hero.CharacterObject`, computed once per tick in the behavior) now gates the HUD, the ready toast, and V-presses — pre-fix a V-press while controlling a soldier consumed the cooldown while `ApplyAoeBuff` early-returned.
- **#378 button feel:** the career button got `Id="TaomCareerButton"` + a Brush (`CareerSystem.CareerButton`: Gauntlet derives Pressed/Hovered ONLY from a Brush; a bare `Sprite=` has no press state, verified in decompile) + a click sound in the mixin's `ExecuteOpenCareerScreen`. That method never ran (no `[DataSourceMethod]`, so UIExtenderEx never added it) and #971 deleted it: the click is handled by the Patch27 postfix, and its `ExecuteDone` plays vanilla's `event:/ui/default` through the developer screen's close callback. CareerScreen's `+`/`−` buttons had the same bare-Sprite defect and got the same brush treatment (`CareerSystem.PlusButton`/`MinusButton`, per-style ColorFactor variants, no new art needed).
- **#379 unspent-points badge:** `ICareerRegistry.GetUnspentPoints(career, level, taken)` is the single source (the career is passed since #769, so a `point_levels` schedule applies) (reused by `CareerScreenVM` + `CareerCampaignBehavior`); `CharacterDeveloperCareerMixin` exposes `HasUnspentPoints`/`UnspentPointsText`, rendered as a red disc + count child of the button (screen-independent by design: the badge exists for players who never open the career screen).
- **#380 keystone glyphs:** `keystone_icon="NNNNN"` per `<Career>` in `taom_careers.xml` (all 49 active + the commented-out `far_harad_halftroll` → 25013), parsed into `CareerDefinition.KeystoneIcon`, rendered as a 30px tinted medallion beside the "While active" badge. Since #388 no prefab binds `KeystoneIconSprite`: a keystone diamond shows the same glyph through `IconSprite`. Banner icons double as bare-number sprite names. **Deliberately NO culture fallback:** missing attr = no medallion + a load warning (the reference module's fallback regressed Rivendell to its live clan sigil 15009). Art caveats logged in #380 for a future curation pass (several picks are live clan sigils; Khand rides the Harad sheet).
- **#381 keystone branch exclusivity (design decision, user-approved 2026-08-05):** one keystone per tier; completing any full tier-3 group reopens all keystones; already-taken stones are never locked (grandfathered and refundable: the rule gates FUTURE takes only, so pre-rule saves keep their stacks). Extracted to `KeystoneExclusivityRule` (one rule, one consumer since #769: `CareerChoiceRules.CanTake` calls it for groups without `max_picks`, and the screen's click and its diamond display state both ask `CanTake`, so a closed keystone dims instead of silently rejecting the click). The one-per-tier core predated this arc (`43c9cb61`); the tier-3 exemption + display gating are new. **Inert until #766 (2026-10-08):** the rule, the click tier gate and the dimming all keyed on `CareerChoiceDefinition.GroupId`, and group choices parsed with an empty `GroupId` (the nested `<Choice>` carries no `group_id` attribute), so every one of them silently skipped. They apply since #766, when the provider began stamping the enclosing `<ChoiceGroup>` id.
- **#382 energy bar:** the 130×166 square `AbilityHUD` panel (which overlapped the tournament prize lane) is RETIRED — `IAbilityHudController`/`AbilityHudController`/`CareerAbilityHudVM`/`AbilityHUD.xml` deleted. Replacement: `CareerEnergyBarPrefab` (UIExtenderEx `PrefabExtensionInsertPatch` into Native's `AgentStatus` prefab — TAOM's first mission-screen prefab injection; container-safety verified: `MissionGauntletAgentStatus` does zero child traversal, only the damage feed is typed/positional and is untouched) + `MissionAgentStatusCareerMixin` (`[ViewModelMixin("Tick")]` on `MissionAgentStatusVM`) + the pure `CareerEnergyBarStateMapper` (Ready full / Active drains via `ActiveProgress01` / Cooldown refills with the derived rescale `(raw − d)/(1 − d)`, `d = ActiveDuration/CooldownDuration` — kills the measured empty→40% refill snap without captured state). Native ShieldHealthBar Canvas/Frame/Fill brushes (flat colors read as misaligned — the brush carries the bevel), career-glyph medallion, and a key chip fed from `IAbilityInputAdapter.ActivationKeyName` (single-sourced with the polled `InputKey.V`).
- **#383 damage attribution:** `CareerPerkMissionBehavior.OnScoreHit` (signature pinned against installed v1.4.7 DLL — two `in` params silently no-op the override if wrong) prints `{TARGET}: {DMG} damage (+{BONUS} from ability)` while the buff window is live. Bonus value comes from TAOM's own applied state (`CareerAbilityBuffTracker`, valid because of #377) — never reflection; share math in `AbilityDamageAttribution` (`dmg·f/(1+f)`, exact while the ability buff is the sole multiplicative term; the passive Damage effect uses `CalculateDamageAmplification`, a different mechanism this line does not claim). Utility abilities print a once-per-activation "boosts something other than damage" notice instead of silence. Threshold `min_reportable_bonus_damage` (default 0.5) on `<Global>` in `taom_ability_tuning.xml`.
- **#384 diagnostics:** `taom.print_hud_layout [maxNodes]` console command dumps the top screen's widget tree with real on-screen rectangles to `Logs/taom_hud_layout.log` (bounded, on-demand, collapsed-tree warning) — the reference module's layout dumps found three bugs code reading did not.
- **Review pass (same session, RCA `docs/reviews/rca-career-ux-arc-2026-08-05.md`):** 5-agent deep review + Codex xhigh (Review 81, 0 P1/2 P2, all six architecture suspects disputed with decompiled evidence). Fixes folded in: hero-death now snapshots buffed allies (`GetBuffedAllyIndices`) and forces `UpdateAgentProperties` on each (the cleared dictionary can't refresh engine-side baked stats); the career-hero identity predicate is single-sourced in `CareerHeroIdentityGate` (three sites, one had drifted); the `OnScoreHit` body lives in `AbilityDamageAttributionReporter` (per-mission boundary reporter); `isSiegeEngineHit` is filtered (siege missiles arrive with the operating player as affector — attributing ability bonus there would be a false claim); **activation is blocked while the active window is live** (`IsAbilityActive` gate — Codex proved olog_hai's Duration mutations reach a 16s window against a 15s cooldown floor, a 1s recast that would double-stack contributions); the badge text brush is the registered `CareerSystem.Badge.Text` (the copied `ButtonBrush1.Text` never existed — `BrushFactory` nulls silently; rule widened in `gui-ui.md`).
- **Install hygiene (audited 2026-08-06):** the reference package's `source/_Module/` mirrors TAOM's own layout, and during evaluation its files were copied into the live `Modules/TAOM/` — the installed `CareerScreen.xml` was byte-identical to the contributor's 466-line diamond rewrite, alongside their blanked `AbilityHUD.xml` and a clone of vanilla's `Mission/AgentStatus.xml`. A plain `./build.ps1` restored the canonical career screen, but the deploy runs `Clean="false"` and **cannot remove** files the repo no longer ships: `GUI/Prefabs/Mission/AgentStatus.xml` (shadows vanilla's combat HUD and references `CareerEnergyBarWidget`, a class only the reference DLL defines) and `GUI/Prefabs/CareerSystem/AbilityHUD.xml` must be deleted from the game install by hand. TAOM ships neither — the energy bar is injected by `CareerEnergyBarPrefab` at runtime, never by shipping a vanilla prefab clone. Vanilla's own `Modules/Native/.../AgentStatus.xml` was verified untouched. Lesson: `lessons/build-tooling-workflow.md`.
**2026-08-06 update — #388 diamond career screen.** The May pip-strip layout was replaced with a diamond grid after seeing both in-game: each choice is a diamond carrying an icon (five per group, two groups per tier), with a persistent **Active Effects** column in the right gutter. **Rank titles and lore group names are kept** — the diamond layout has room for both. Ported as TAOM-owned code from the reference module's prototype, dropping ~800 of its ~940 lines: its `CareerScreenStatePatch` (494) Harmony-scraped `CareerScreenVM` from outside and its `CareerEffectsPanelWidget` (146) polled a static because it could not bind — from inside, the VM just publishes `KeystoneEffectLines` / `PassiveEffectLines` (passives summed per effect type, so two +5% Damage picks read as one "+10% damage"), accumulated inside the existing `RebuildChoiceGroups` walk rather than a second registry pass.
- **Icons are banner icons, and need no sprite bake.** The per-choice sprites authored in `taom_career_choices.xml` (`career_choice_*`) were never drawn — 0 PNGs, 0 atlas entries — which is exactly why `IconSprite` was dead data bound by no prefab. `CareerChoiceObjectVM.IconSprite` now resolves to an already-baked banner icon: keystones show their career's own sigil (#380's `keystone_icon`), passives an icon for their effect type (`CareerEffectDisplayMap`).
- **Percent vs flat is keyed on effect TYPE, not magnitude size.** The reference used `|value| < 1 means percent`, which prints a 1.0 magnitude (=100%) as "+1" and a +1 companion limit as "+100%". TAOM authors Damage/Ammo/MovementSpeed/MountHealth/InventoryCapacity as fractions and Health/PartySize (and CompanionLimit) as counts. `CareerEffectDisplayMap` reads `CareerPerkConsumerMap.IsFlat`, which lists only the true counts (`Health`, `PartySize`, `CompanionLimit`, `DailyGold`, `TroopTraining`), so `MountHealth` and `InventoryCapacity` print as percentages ("+10% mount health").
- **Custom widget registration is automatic.** `WidgetInfo.CollectWidgetTypes()` scans every loaded assembly that references TaleWorlds.GauntletUI and collects all `Widget` subclasses (v1.4.7 decompile): defining `TaomCareerDiamondWidget` inside a loaded TAOM assembly (TAOM.Client.dll) is sufficient, there is no register call and `UIExtender.Register` does NOT do it (it only picks up `[PrefabExtension]`/`[ViewModelMixin]`). **The `Taom` prefix is load-bearing:** `WidgetFactory._builtinTypes` keys on the SIMPLE type name across assemblies, ignoring namespace, so an unprefixed `CareerDiamondWidget` would collide with the reference module's class if both were ever loaded.
- **Layout, after two in-game passes (2026-08-06).** The career info is a **header bar across the top** (name + description | portrait | ability icon, name and effect lines); as a full-height left column it ate the width the grid needed. Diamonds are 70px, the boxed group panels are gone (a group is its lore name above its row on the open background), and each diamond carries the bronze border sprite tinted by state: dim locked, bronze available, bright gold taken, and (#769) a fourth dark red `#6B2E2EFF` overlay for a choice locked out by a fork (the widget drives the taken glow). Rank title and its "Requires Level N" share one line above the groups; Free Points / Active Effects is a right-hand column. The tooltip is anchored above its diamond with `RenderLate` so it draws over neighbouring rows, not under their chrome.
- **Diamond rims are per-tier metals** (2026-08-06): tier 1 bronze → tier 2 silver → tier 3 gold, so the tree reads as a progression at a glance. Each tier owns three border colours — dim (locked) / mid (available) / bright (taken glow) — and they are deliberately NOT shared between tiers, so changing one tier's metal means editing all three of its values. The full hex table is in a comment above the body block in `CareerScreen.xml`. The taken glow is shown by `TaomCareerDiamondWidget` rather than a binding, so it is a plain colour on the widget's `DiamondGlow` child.
- **Selection is a toggle, and that is load-bearing.** Dropping the +/− button column left no way to refund a pick, so the diamond click calls `CareerChoiceObjectVM.ExecuteToggleChoice`: take an untaken choice, refund a taken one. Both directions are guarded by `CareerChoiceRules` (`CanTake` going in: points, tier, fork and keystone; `CanRefund` coming out: an untaken id, the root, a locked-in fork pick), which the diamond display state asks too (see [Forks](#forks-point-schedule-retrain-and-tree-versions)). A regression test pins it: without the toggle every taken choice is permanently stranded. Tier locks and keystone exclusivity apply on this path since #766; before it the click gate was inert for group choices (see the #381 note above). `CareerScreenVMTests.ExecuteToggleChoice_ParsedTier3ChoiceAtLevel1_IsRefused` and `ExecuteToggleChoice_ParsedSecondKeystoneInSameTier_IsRefused` drive the real click over a real provider and registry.
- **The screen is FIXED WIDTH (1720) and CENTRED — do not restore `StretchToParent`.** On a 32:9 monitor a stretching container made the layout as wide as the display, so every fixed-width child clustered left, the header bar ran into dead space, its ability text drifted past centre and clipped, and the Active Effects column stranded far from the grid. 1720 is sized to hold the body (1330 tiers + 20 + 340 column) and the header (700 + 290 + 24 + 92 + 16 + 560 = 1682); changing any of those widths means re-checking that sum. Lesson: `lessons/localization-ui.md`.
- **Reviewed 2026-08-06 (Review 83).** 6-agent deep review of both effect fixes, both retunes and the retune tool: Standards PASS, API Compatibility 6/6 verified against the installed v1.4.7 DLLs (it CONFIRMED the `GetEffectiveMaxHealth` hero branch -> `CharacterObject.MaxHitPoints()` -> `MaxHitpoints` chain the health fix rests on, and that both models register in lockstep), Completeness COMPLETE, Data Flow 38 flows / 0 gaps / no third instance of the bug class. 2 findings, both fixed: a CRITICAL idempotency bug in the retune tool (a second `--apply` would have double-shifted 71 of 105 TroopDamage pips) and a HIGH hot-path allocation this work created in `CareerPassiveService.GetPassiveMagnitude`. RCA: [rca-career-effect-layer-audit-2026-08-06.md](../reviews/rca-career-effect-layer-audit-2026-08-06.md).
- **Owed:** in-game verification of the two 2026-08-06 effect fixes: max health now reads on the character sheet (CONFIRMED working in-game 2026-08-06) and army-wide troop damage at the new 2-8% band (NOT yet checked); (dedicated issues **filed and closed 2026-08-06**: [#394](https://github.com/haterade22/TAOM/issues/394) max health, [#395](https://github.com/haterade22/TAOM/issues/395) troop damage; the numbers first used, #390/#391, were wrong: #390 is the armory meta-mesh issue and #391 did not exist); in-game smoke checklist (plan `review-this-and-identify-bubbly-moon.md` §Verification), 12-language `/localize` for `taom_career_dmg_attrib`/`taom_career_dmg_none` (English registered; translation env-gated on `ANTHROPIC_API_KEY`), keystone-icon art curation pass.
- **Owed (2026-10-14):** the #766 and #769 strings ship in English in the 12 language files until a `/localize` translation run (paid, on the maintainer's word): 57 `taom_career_*` ids match the English in all 12 files (the lock reasons, retrain and switch prompts, next point, tree reset, effect labels, the two value keys, class phrases and `taom_career_repair_refund`).

**2026-08-06 — #388: the `Health` pip only ever worked in battle.** A player took a "+75 max health"
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
  exactly one number. Re-running is a no-op via a file-level `already_applied()` guard (per-pip detection is impossible when a mapping's old and new values overlap — deep review 2026-08-06). `far_harad_halftroll_root`
  says "massive health bonus" with no number and is deliberately left alone.

**2026-08-06 — TroopDamage was wired to village raiding, not battle.** The second instance of the
same bug class, found by re-auditing every effect with the lens that catches it: not *"is anything
reading this"* but *"is it read where the description promises"*. `TroopDamage` (105 pips, the
second-most-used effect) promises *"+5% troop damage"* and *"Your troops smash through enemy lines"*,
but its only consumer was `TaomRaidModel.CalculateHitDamage` — vanilla's
`(sqrt(TroopCount) + 5)/900 * deltaHours`, accumulated by `RaidEventComponent` into
`_nextSettlementDamage`. That is how fast a village burns. Troops hit exactly as hard as ever.

- **Fixed as the mirror of `TroopResistance`,** which was already doing the defensive half correctly.
  `GetAttackerTroopLeaderHeroId` resolves the attacker's party leader and
  `CalculateDamageAmplification` applies the leader's `TroopDamage` to their non-hero troops' hits.
  Mount caveat inherited from the victim-side helper: for a couched/charge hit the AttackerAgent can
  BE the mount, whose own `Origin` is null, so the leader must resolve off `AttackerRiderAgentOrigin`
  or every cavalry impact silently loses the bonus. Not mask-gated when this shipped (the shipped
  pips carry no `attack_type_mask`, so it was a flat army-wide multiplier; since #769 it honours
  `attack_type_mask` and `troop_class`, see below, and the shipped pips are unchanged). Hero and troop paths are mutually
  exclusive (the boundary returns null for a hero attacker), pinned by a test.
- **The raid consumer is deliberately KEPT** — raid progress is a fair reading of "troop damage", it
  is behaviour players already have, and the two are different systems, so this is not the
  double-count the health fix had to avoid. `TroopDamage` is the one type with two consumers.
- **Retuned 3-20% -> 2-8% before going live** (t1 2-3%, t2 3-6%, t3 5-8%), same reasoning as the
  health pips: the magnitudes were authored while the effect did nothing in battle. An army-wide
  multiplier compounds across every soldier, so the band sits below the hero's own `Damage` pips
  (5-18%). `tools/retune_career_health.py --effect troopdamage` does all four surfaces; it grew an
  `EFFECTS` profile table rather than being copied, the new axis being `scale` (Health prints its
  flat magnitude, TroopDamage prints magnitude x100 as a percentage).
- **What the audit cleared.** Every other shipped effect is consumed at the layer its text promises:
  `TroopMorale`->`GetEffectivePartyMorale`, `TroopSurvival`->`GetSurvivalChance`,
  `HeroHealing`->`GetDailyHealingHpForHeroes`, `TroopWages`/`TroopUpgradeCost`->campaign economy,
  `RenownGain`->`TaomBattleRewardModel`, the map effects->visibility/size/speed models, and the
  hero/mount combat effects->the mission models. One cosmetic gap remains: 49 Keystone pips grant a
  passive their description never mentions (they describe only their ability mutation) — those work,
  they are just undisclosed.

**2026-09-03 update: the ability key is rebindable (#533).** The activation key shipped as a
`private const InputKey ActivationKey = InputKey.V` inside `AbilityInputAdapter`, so a player could
neither change it nor see it conflicting with anything else they had bound. It is now a native game
key in **Options > Keybindings > Action**, following the same pattern
`TaomTimeControlHotKeyCategory` already uses for the campaign-map time controls, and for the same
stated reason: MCM v5 has no keybind widget, and its dropdowns persist by index so a reordered
option list silently remaps every player's binding.

- **`TaomCareerHotKeyCategory`** registers one `GameKey` (id **510**, string id `TaomCareerAbility`)
  with `GameKeyMainCategories.ActionCategory`, defaulting to **V**, so a player who never opens
  Options notices nothing. `Action` rather than `Campaign Map` because the key is pressed in a
  mission. Registered from `SubModule.OnSubModuleLoad` next to the time-control category, in the
  same try/catch, and for the same timing reason: after Native's `RegisterInitialContexts`, which
  clears every category.
- **The id must stay at or above 116.** `KeyOptionVM` builds its localization id from
  `((GameKeyDefinition)Id).ToString()`, not from `StringId`, so an id inside that enum's range would
  render as a vanilla key's name and reuse vanilla's string. The context is sized to `510 + 1` slots
  because `RegisterGameKey` is an indexed write into a pre-nulled list.
- **`AbilityInputAdapter` now resolves the bound key** once through
  `HotKeyManager.GetAllCategories()` and caches the `GameKey` **reference**, never the `InputKey`
  behind it: `LoadAsync` applies a saved binding by mutating `KeyboardKey` when one exists and by
  replacing it when null, so only re-reading the property each poll observes both cases and lets a
  mid-session rebind take effect. It keeps the static `Input.IsKeyPressed`, which is edge-triggered
  as the activation state machine requires, and never registers the context on a screen layer:
  `GameKeyContextsInputManager` sizes its slot list from the first category registered there (116
  entries), so id 510 would throw.
- **A cleared binding is a supported state.** `BoundKey` returns `Invalid` for both shapes an unbound
  key takes (the ctor's null `KeyboardKey`, and Options' `ChangeKey(Invalid)` on the existing `Key`),
  the poll returns false without reaching `Input`, and the chip label goes empty.
- **The energy-bar key chip reads per refresh** rather than once in the mixin constructor, so
  rebinding through the in-mission Esc menu updates it without waiting for the next mission.
- **Strings live in `ModuleData/global_strings.xml`**, which must keep that exact name and
  root-level path: the Options screen resolves rows through `Module.CurrentModule.GlobalTextManager`,
  populated only by scanning modules for that literal path. A missing row renders the raw lookup id
  in the keybinding list, so `TaomCareerHotKeyCategoryTests` asserts both `str_key_name.` and
  `str_key_description.` exist for every registered id.

## Overview

Career/class progression system where each hero can have a career that provides passive bonuses, an active ability, and a 3-tier choice tree. 70 LOTR-themed careers, fully XML-driven. Each career has 31 choices (1 root + 6 groups x 5 choices) with keystones, passives, and ability mutations, except the three Gondor careers, which hold a root plus six forks of two or three options (14 choices each, since #769 C2).

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
- **Passive application:** `ICareerPassiveService` caches per-hero effect magnitudes, the GameModels read it through `ApplyFactor` / `ApplyFlat` on the service (the static `CareerPassiveHelper` is gone)
- **Mutations:** Hybrid XML + C# calculator registry — XML defines target/params, C# provides calculator functions by ID
- **UI:** `GauntletCareerScreen` with `CareerScreenVM` hierarchy (diamond grid, portraits, ability icons), `CharacterDeveloperCareerMixin` (UIExtenderEx) for career button with sprite. See [gui-sprite-system.md](gui-sprite-system.md) for full UI details. **Screen revamp 2026-05-30** ([RCA](../reviews/rca-career-ui-revamp-2026-05-30.md)): tiers ordered Tier 3 (top) → Tier 1 (bottom); locked tiers show a **"Requires Level N"** label (level from `CareerRegistry.GetTierUnlockLevel`: T1/1, T2/10, T3/20) instead of the old gate art; each node is an always-visible **point-pip strip** (3 brightness states, taken / available / empty, via `CareerChoiceObjectVM.IsUnavailable`) with perk descriptions on hover, using the shared `CareerSystem\career_point_pip` One Ring sprite; tier headers show per-career **rank titles** (`CareerDefinition.Rank1/2/3Name`, fallback "Tier N"); node headers show per-group **lore names** (`CareerChoiceGroupDefinition.DisplayName`, humanized-id fallback). 294 group names + 147 rank titles are web-researched Tolkien-grounded (`tools/career_group_names.json`, `tools/career_rank_names.json`). **In-game pass 2026-05-31:** names singularized (single-player career → singular titles, e.g. "Warden of the East Bank" not "Wardens"; via `tools/singularize_career_names.py`); tier rank labels set flush-left (`CoverChildren`+Left, no wrap-indent); locked-tier node spacing matched to Tier 1 by reserving the `+`/`−` button column (fixed 70px, buttons gated on `@IsActive`). **Pip sprite, two fixes (bake + render):** the pip first needed the offline sprite generator to bake it into the `ui_taom_career_system` atlas (`AssetSources/GauntletUI/...png` + `Assets/GauntletUI/..._tex.tpac`, **not** a `pack0.tpac`), and then a prefab fix because even baked it rendered invisibly at 22×28px/27% alpha (bumped to 38×38 + brighter opacities); the "Requires Level N" label was re-centered into the gap between the two node columns (`CoverChildren`+Center alone was insufficient: the 70px button reserve shifts the boxes left of row-center, so a `PositionXOffset="-40"` was needed); and the hover perk descriptions were made **inline with the pips** (the parallel pip + description `{Choices}` lists were given matching 46px rows and the description text switched to `CoverChildren`+left, so each description sits on its pip's row). **All confirmed working in-game (user screenshots, 2026-05-31).** **Taken-pip lit-ring follow-up (2026-06-19 → 22, issue #290):** the *taken* state now uses a brighter dedicated sprite `CareerSystem\career_point_pip_lit` (the One Ring ring whitened + a soft glow halo); available/locked stay on the shared hollow `career_point_pip` dimmed to `#FFFFFF55` / `#FFFFFF22`. The old scheme tinted all three states of the *same* hollow ring, so taken vs available was only a ~12% alpha gap; an increased skill never visibly "lit up." Gauntlet `Color` is a multiplicative tint (can't brighten past the sprite's own pixels), so a distinct brighter sprite was required, not just a tint change. Confirmed working in-game 2026-06-22. See [gui-sprite-system.md](gui-sprite-system.md) "The sprite-bake pipeline" (decompile-verified) + "Verifying a sprite (bake + render)" + the 2026-06-19 follow-up, and [RCA post-review in-game findings](../reviews/rca-career-ui-revamp-2026-05-30.md#post-review-in-game-findings-2026-05-31).
- **Battle:** `CareerPerkMissionBehavior` (thin entry point per ADR-002 — ~220 lines as of 2026-08-05, over the 150 ceiling as a recorded pre-existing condition; every handler is guards + delegation) delegates per-frame work to Singleton-lifetime controllers plus the per-mission `AbilityDamageAttributionReporter`:
  - [`IAbilityActivationController`](../../Main/Features/CareerSystem/Abilities/IAbilityActivationController.cs) — V-key + ready-state notification + charging-message throttle state machine. Returns an `AbilityActivationResult { JustBecameReady, Activated, Charging }` flags struct so the host can emit BOTH the green "ready" toast and the yellow "activated" toast on the same frame (legacy UX). Fully unit-testable via `IAbilityInputAdapter` + `IMissionTimeProvider` injection — no TaleWorlds statics.
  - ~~`IAbilityHudController`~~ — RETIRED 2026-08-05 (#382). The self-owned `GauntletLayer` HUD (and its `_attachedScreen` capture + try/catch/finally teardown discipline, worth remembering for any future custom layer) was replaced by the energy bar riding inside Native's AgentStatus movie — see the 2026-08-05 update above.
  - [`IAbilityEffectExecutor`](../../Main/Features/CareerSystem/Abilities/IAbilityEffectExecutor.cs) — per-activation pipeline: mutate template, apply CooldownReduction adjustment, allocate `MissionAbilityExecutionContext`, register with host's `_activeContexts` list, dispatch per-archetype effect executor, emit toast + sound + particles.
- **Singleton lifetime + per-step OnEndMission try/catch.** The two controllers (activation and effect executor) are `Reuse.Singleton`; the host `CareerPerkMissionBehavior` is constructed fresh per mission. Cross-mission state (`_abilityReadyNotified`, on `AbilityActivationController`) lives on the singleton and is cleared by `Reset()` in `OnEndMission`. Per the deep-review systemic finding, each of the four guarded `OnEndMission` steps runs in its own `try/catch` so a throw in one cannot abort the others: `_activationController.Reset()`, `_abilityService.ClearAll()`, `CareerAbilityBuffTracker.ClearAll()` and `ResetStatDiagnostics` (`ICareerAgentStatService.ResetDiagnostics`). `CareerAbilityService` forces `ChargeType.CooldownOnly` for every career; readiness is purely cooldown-timer based (see [Cooldown System](#cooldown-system)).
- **Ability effects:** `CareerAbilityEffectRegistry` dispatches to per-career `ICareerAbilityEffectExecutor` implementations. 3 role-based archetypes (Infantry/Ranged/Cavalry) serve every career with XML-tunable values via `taom_ability_tuning.xml`. All three archetypes apply AoE friendly-troop buffs within a 50-unit radius (standardized in templates): Infantry (damage + damage reduction), Ranged (speed + ranged damage + draw speed), Cavalry (mount speed + charge damage + damage). Buffs applied via `CareerAbilityBuffTracker` with separate hero and ally buff dictionaries (read by `TaomAgentStatCalculateModel`, so they survive a stat recalc). The two mount fields of the Cavalry buff (`MountSpeedBonus`, `ChargeDamageBonus`) apply on the MOUNT's driven properties through the rider's ids (`ApplyMountStatModifiers`, #611), because the base models write `MountSpeed` and `MountChargeDamage` only in their horse branch and the engine reads them off the horse; the buff apply and restore paths refresh `MountAgent` beside the human so the horse recomputes at once. Before 2026-09-17 both fields multiplied the rider's copies (default 0) and did nothing. Known limitation: the bonus is keyed on the horse's CURRENT rider at recompute time, the way vanilla's own Riding-skill maneuver and speed bonuses on the horse are (`UpdateHorseStats` reads `agent.RiderAgent`); a rider who dies or swaps horses mid-buff leaves the first horse on its last computed numbers until the engine recomputes that horse, and whether native does so on a rider change is not provable from managed code (`OnMount` / `OnDismount` recompute the RIDER). Whatever refresh vanilla's rider bonus gets, this one gets; snapshotting the specific horse in the restore closures was rejected (captured agent handles across frames, the #592 class, for a case bounded by vanilla parity). Smoke item: a buffed cavalry ally remounts or dies mid-buff, then check the abandoned horse's charge in the combat log.

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
  |            ApplyFactor/ApplyFlat → GameModels
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

Defines careers with: id, display name, description, portrait sprite, ability template ID, eligible cultures, choice group IDs, root choice id, min clan tier. **`rank1_name` / `rank2_name` / `rank3_name`** (added 2026-05-30): per-career tier-header rank titles shown on the career screen (e.g. `Ohtar → Roquen → Knight of the Golden Flower`); optional, the VM falls back to "Tier N". `max_perk_points` attribute on root element (default 30). #769 adds `retrain_gold_base` / `retrain_gold_per_level` on the root (defaults 1000 / 250, whole numbers of 0 or more, anything else logs a warning and uses the default), and on `<Career>` the optional `point_levels` and `tree_version` (see [Forks](#forks-point-schedule-retrain-and-tree-versions)). `<ChoiceGroup>` in the choices file gains `max_picks`. (Pre-2026-05-04 the schema also had `charge_type` and `max_charge`, both removed; cooldown is global, not per-career.)

### Choice Trees (`Main/_Module/ModuleData/career_system/taom_career_choices.xml`)

Defines standalone root choices and choice groups. Each group has a tier (1/2/3), an optional **`display_name`** (added 2026-05-30; the per-group lore "path" name shown as the node header, e.g. `Watchers of Henneth Annûn`; the VM humanizes the id as fallback), and contains choices (Keystone or Passive). Choices can have up to two PassiveEffects (type + magnitude + attack_type_mask + troop_class) and Mutations (target template + property + calculator + params).

**Two PassiveEffect schemas are accepted** (`CareerConfigProvider.ParseChoice`):
- **Direct (preferred):** `<Choice ...><PassiveEffect type="X" magnitude="0.10" attack_type_mask="Melee" /></Choice>`
- **Wrapped:** `<Choice ...><PassiveEffects><PassiveEffect type="X" value="0.10" /></PassiveEffects></Choice>`; the parser reads the `<PassiveEffect>` children of the plural `<PassiveEffects>` wrapper and accepts `value=` as an alias for `magnitude=` (`magnitude=` wins when both are present). The wrapped form was historically unparsed (310 dead choices across all 16 cultures); fixed 2026-05-29, see [RCA](../reviews/rca-career-partysize-2026-05-29.md).

**Multi-effect options (#769 E8).** A choice carries up to `CareerConfigProvider.MaxPassivesPerChoice` (2) passives, exposed as `CareerChoiceDefinition.Passives` (never null, empty when there is none; the old single `Passive` is gone). Direct `<PassiveEffect>` children and the children of any `<PassiveEffects>` wrapper feed one list in document order, so the two schemas may be mixed. A third and later element draws one load warning naming the choice and is ignored. Every consumer takes all of them: `CareerPassiveService` accumulates each passive of every taken choice and of the root, `ValidatePassiveConsumers` checks every passive, the diamond icon follows the first, and the Active Effects panel sums each passive of a taken group choice (it omits keystone and root-choice passives, see "Active Effects panel" below). `CareerChoicesIntegrationTests` pins that no shipped choice holds more than the cap and that every choice of an original (`tree_version` 1) tree still parses to the one passive it always did; a redesigned career may carry two.

`PassiveEffect` carries `type`, `magnitude`, `attack_type_mask` and `troop_class`. The legacy `operation`/`is_percentage` attributes were **parsed-but-never-read** and were removed 2026-06-25 (ignored if still present in XML).

**Attribute reference (#769).** Every attribute below is optional and an absent one keeps its default. A bad value logs a load warning naming its owner, except for `max_perk_points` and `magnitude`, which fall back silently.

| Where | Attribute | Rule |
|---|---|---|
| `<Careers>` | `max_perk_points` | Whole number, default 30. Caps the legacy one-point-per-level budget only. |
| `<Careers>` | `retrain_gold_base`, `retrain_gold_per_level` | Whole numbers of 0 or more, defaults 1000 and 250 (the shipped file states both). Anything else warns and takes the default. |
| `<Career>` | `tree_version` | Whole number of 1 or more, default 1; a redesigned tree is 2 or more. Anything else warns and reads as 1. See [Forks](#forks-point-schedule-retrain-and-tree-versions). |
| `<Career>` | `point_levels` | Comma list of strictly ascending positive integers (`"5,10,15"`): one point at each listed level, plus the free root. Anything else drops the attribute with a warning and keeps one point per level. Ignores `max_perk_points`. |
| `<ChoiceGroup>` | `max_picks` | Whole number of 0 or more, default 0 (unlimited). A positive value makes the group a fork. A career's groups are all forks or all unlimited (`CareerChoiceIntegrityTests.NoCareer_MixesForkGroupsWithUnlimitedGroups` fails a mix). Anything else warns and reads as 0. |
| `<Choice>` | group membership | Taken only from the enclosing `<ChoiceGroup>`; a non-empty `group_id` attribute is ignored with a warning (the roots' empty `group_id=""` is silent). |
| `<Choice>` | `<PassiveEffect>` children | Up to two, direct or inside any number of `<PassiveEffects>` wrappers; a third and later is ignored with one warning. |
| `<PassiveEffect>` | `type` | Read by `CareerConfigProvider.ParsePassive` with `Enum.TryParse`, ignoring case. That also accepts a digit string as an ordinal and a comma list as OR'd values, so a name is the safe spelling. A string `TryParse` rejects warns and parses as `Special` (an inert pip). |
| `<PassiveEffect>` | `magnitude` (alias `value`) | Float. Unparseable, NaN or infinite reads as 0. A magnitude above `CareerConfigProvider.MaxPassiveMagnitude` (10000, either sign) warns naming the choice and the value and drops the passive, so two pips cannot sum to Infinity. |
| `<PassiveEffect>` | `attack_type_mask` | Names `Melee`, `Ranged`, `All`, `Cut`, `Pierce`, `Blunt`, comma or pipe separated, any case; absent is `All`. Honoured on `PassiveEffectConsumers.MaskGated` (`Damage`, `Resistance`, `TroopDamage`, `TroopResistance`); on any other type it warns and is ignored. An unknown name warns and leaves the pip inert (`None`). |
| `<PassiveEffect>` | `troop_class` | Names `Infantry`, `Ranged`, `Cavalry`, `All`, comma or pipe separated, any case, spaces trimmed; absent is `All`. Read on `PassiveEffectConsumers.ClassFiltered` (`TroopDamage`, `TroopResistance`, the three counters and `TroopTraining`); on any other type it warns and is ignored. An unknown name on a class-filtered type drops the passive. |

To author a whole tree of forks, use the generator rather than hand-writing this XML: [career-forks-generator.md](career-forks-generator.md).

**Magnitude scale ↔ application method (IMPORTANT).** `CareerPassiveService` stores the summed `Magnitude` per type (`GetPassiveMagnitude`, unfiltered: it ignores both the mask and the troop class) plus a per-(type, mask, troop class) breakdown (`GetMaskedMagnitude` matches the mask and any class, `GetTroopMagnitude` matches the class and the hit mask; with no hit mask it skips only a `None` bucket, the loader's sentinel for an unparseable mask, and it serves the damage path, the troop counters, the console and the army-wide `ApplyFactor` / `ApplyFlat` reads); each consumer chooses flat vs factor by calling `ApplyFlat` / `ApplyFactor` or applying the magnitude directly. `ApplyFactor` is a plain `result.AddFactor`. For a class-filtered type `ApplyFactor` reads the army-wide pips only, and so does `ApplyFlat` unless it is given a troop class (`GetTroopMagnitude` with `TroopClassMask.None`, which the `All`-class buckets match), because their callers have no troop class: the raid speed of `TroopDamage` (`TaomRaidModel`) never counts a class-only pip such as "+10% infantry damage". **`ApplyFlat` is a RESULT-frame add**, not a plain `result.Add`: it divides the accumulated factor frame back out so an authored count is worth exactly that count (2026-08-18). So a passive's authored magnitude scale MUST match its consumer's method:
- **Fractional magnitude (0.10 = +10%)** → `ApplyFactor` / multiplicative. The convention for almost every type (TroopWages, PartyMovementSpeed, TroopMorale, etc.).
- **Whole-count magnitude (2 = +2 units)** → `ApplyFlat`. `PartySize` (`TaomPartySizeModel`) and `Health` (`TaomCharacterStatsModel.MaxHitpoints`) use `ApplyFlat`; `CompanionLimit` is a whole count that `TaomClanTierModel` adds directly; so are `DailyGold` (`TaomClanFinanceModel`, `ApplyFlat`) and `TroopTraining` (`TaomPartyTrainingModel`, `ApplyFlat` with the troop's class). Applying a whole-count via `ApplyFactor` multiplies the base (`AddFactor(2)` = ×3), the "+2 to +150" bug fixed 2026-05-29. A second, quieter version of the same bug lived in `ApplyFlat` itself until 2026-08-18: `ExplainedNumber` resolves as `BaseNumber * (1 + SumOfFactors)`, so a base-frame `Add` is multiplied by every factor anyone else put on the number. With only a culture feat in play that was a rounding error; once AiPartySize (#461) contributed a large factor, a "+4 party size" perk under a 3x party-size factor was worth about +13. `ApplyFlat` now divides the frame out, so the career screen tells the truth. The division cancels only the factors already on the number, so `ApplyFlat` must run after the last `AddFactor`; `TaomPartySizeModel` calls it after the AI lord and caravan scaling (#768, pinned by `AiPartySizeOrderingTests`). Pinned by four tests in `CareerPassiveServiceTests`.
- **Three more shapes (#769 E11).** A float a model returns (the raid loot multiplier, the trade penalty factor) or one share of a list (the loot gold chances) has no `ExplainedNumber` to put a factor on, so `ScaleByPassive(heroId, value, type)` returns `value * max(0, 1 + magnitude)`. The scale floors at 0, so a magnitude of -1 or less is the full cut (no trade penalty at all, a loot share of nothing) with no cliff at -1: a total of -1.01 is the same full cut as -1, never a sign flip and never the full penalty back. The loader keeps NaN and infinity out of the cache (`CareerConfigProviderTests`), so the scale is always finite. A flat count for a class-filtered type whose consumer HAS a class (`TroopTraining`) goes through `ApplyFlat(heroId, ref result, type, troopClass)`: with the class it reads the pips that reach it, and left at `None` it reads the army-wide ones, with the same result-frame rule. A factor that must not touch a negative number goes through `ApplyWoundRecovery(heroId, isPrisoner, ref result)`: only a healing value above zero on the non-prisoner call, and the factor stops at -1 so a total below -1 can never turn a recovery into a daily wound. `ApplyFactor` and `ScaleByPassive` write the `[CareerPerks]` line only for an event-scoped type and only when the number moved, so a factor on vanilla's 0 influence prints no "0 -> 0".

**attack_type_mask (Damage + Resistance).** `Damage` and `Resistance` honor the mask, which carries two axes since #613: delivery (`Melee`, `Ranged`, `All` for either) and damage kind (`Cut`, `Pierce`, `Blunt`, from the engine's `AttackCollisionData.DamageType` per hit). A pip matches a hit per axis (`AttackTypeMaskMatch.Matches`): its delivery bits, if it names any, must overlap the hit's, and its kind bits, if it names any, must overlap the hit's. So "+5% blunt resistance" (`Blunt`) fires on a mace and on a sling stone, "+10% resistance to melee" (`Melee`) fires on any melee kind, `Melee, Blunt` needs both, and a plain "+5% resistance" (no attribute, `All`) fires on everything, exactly as before. Names are comma or pipe separated, any case; an unknown name or a digit string warns at load and leaves the pip inert (`None`), and `CareerChoicesIntegrationTests` refuses a `None` mask in the shipped XML. Only the mask-gated types read a mask (`PassiveEffectConsumers.MaskGated`: `Damage`, `Resistance`, `TroopDamage`, `TroopResistance`); on any other type, the three troop counters included, the loader warns and ignores the attribute (the pip applies to every hit), because the counters see no hit and a "None is inert" reading would not hold for them. Until #613 the enum had only the delivery axis, `ParseEnum` fell back to `All` for the five kind pips, and they resisted every hit. `Damage` is applied multiplicatively on the per-hit **amplification** path (`CareerAgentStatService.CalculateDamageAmplification`), NOT as a flat `DamageMultiplierBonus`, so the mask can gate it.

**attack_type_mask on TroopResistance and TroopDamage (#769 E9).** The army-wide pair now honour the mask too, through `GetTroopMagnitude(leader, type, troopClass, hitMask)`, which `CalculateDamageAmplification` and `CalculateDamageReduction` call per blow. So `<PassiveEffect type="TroopResistance" magnitude="0.20" attack_type_mask="Ranged" troop_class="Infantry" />` is an arrow ward: the leader's infantry take 20% less from missiles and nothing else changes. The shipped troop pips carry no mask, so they read the same value for every hit, exactly as before. The Active Effects panel totals a type's magnitudes without the mask, so until it shows masks no shipped `TroopDamage` or `TroopResistance` pip may carry one: `CareerChoicesIntegrationTests.RealChoicesXml_NoTroopDamageOrResistancePip_CarriesAnAttackTypeMask` fails if one does, and says the panel must learn masks first.

**troop_class (#769 E9).** An optional attribute on `<PassiveEffect>`: `Infantry`, `Ranged`, `Cavalry` and `All`, any case, with the grammar of `attack_type_mask`: separated by commas or pipes, spaces trimmed around each name (`troop_class="Infantry, Ranged"`, `"Infantry|Ranged"`; a space alone is not a separator). Absent means every troop (`TroopClassMask.All`). A passive counts for a troop when its class is `All` or shares a bit with the troop's, so a troop with no class (resolved `None`) still receives the `All`-class effects and nothing narrower.
- **Fail closed.** An unknown name (including `None`, a digit string, an empty name in the list, a space-separated list or an empty value) logs one warning naming the choice and DROPS that passive. Defaulting a typo to `All` would widen an infantry bonus to the whole army. The other passive of the same choice is untouched.
- **Class-filtered types only.** `PassiveEffectConsumers.ClassFiltered` is `TroopDamage`, `TroopResistance`, the three troop counters below and `TroopTraining` (#769 E11), the types whose consumer passes a class. `troop_class` on any other type logs a warning and is ignored (applies to all), because honouring it would pretend to restrict a pip that cannot be restricted. `CareerChoicesIntegrationTests` requires every shipped `troop_class` to parse and sit on a class-filtered type, and fails when its scan finds none (9 shipped pips carry `troop_class` today: the Gondor forks' `TroopDamage`, `TroopResistance` and `TroopTraining`, 3 each). The set is pinned once, without the game, in `CareerConfigProviderTests`. The two-effect cap counts authored elements: a passive dropped for a bad `troop_class` still uses one of the two places, so a third element is ignored whatever happened to the first.
- **Display.** The career screen prints the classes on the line ("+10% troop damage for infantry"), and keeps a class-filtered total apart from an unfiltered one so they never read as a summed "+15%". Every effect label is a `{=taom_career_effect_<snake_case of the enum name>}{VALUE} <noun>` string (for example `{=taom_career_effect_damage}{VALUE} damage`), one key per `PassiveEffectType`, and two value keys build the `{VALUE}`: `taom_career_value_percent` (`{NUMBER}%`) and `taom_career_value_flat` (`{NUMBER}`), so each language can order the number and the noun. The class suffix is six whole-phrase strings (`taom_career_class_line_infantry`, `_ranged`, `_cavalry`, `_infantry_ranged`, `_infantry_cavalry`, `_ranged_cavalry`, each with an `{EFFECT}` variable), so a translator sees the sentence it is translating; `All` has no suffix. All of them are registered in `taom_module_strings.xml` and seeded in the 12 language files. An earlier draft built the suffix from fragment keys (`taom_career_class_infantry`, `_ranged`, `_cavalry`, `_join`, `taom_career_class_line`); they never shipped, and `CareerEffectLocalizationTests` keeps them out. `taom.career_perks` shows the army-wide value and each class value (collapsed to `All` when all read the same, a horse archer read as Ranged+Cavalry), and a per-hit-kind breakdown for the mask-gated types only.

**Which class a troop has.** One rule, `TroopClassMapper.FromCharacter`, shared by the damage boundary (`TaomAgentApplyDamageModel`, reading the `AttackInformation` snapshot's character beside the origin the leader id comes from: the rider's for a mount hit), the troop counters (`CareerTroopHooks.TryResolveTroop`) and the training model (`TaomPartyTrainingModel`). It maps the character's `DefaultFormationClass` through `TroopClassMapper.FromFormationClass`:

| `DefaultFormationClass` | Class |
|---|---|
| `Infantry`, `HeavyInfantry` | Infantry |
| `Ranged` | Ranged |
| `Cavalry`, `LightCavalry`, `HeavyCavalry` | Cavalry |
| `HorseArcher` | Ranged and Cavalry |
| `Skirmisher` | Infantry and Ranged |
| `General`, `Bodyguard`, `Unset`, anything else | None |
| any hero, or no character | None |

A troop of class None still receives the `All`-class effects and nothing narrower. A horse archer receives a pip for either class, and a pip naming both counts once. `TroopClassMaskTests` lists every `FormationClass` value in an expected table and fails on one it does not name, so an engine bump that adds a formation cannot land silently on None. Two departures from the engine are known and left as they are. The class is the troop's static default formation, so cavalry fighting on foot (a siege wall) still count as cavalry, where the engine re-classes troops only at spawn in siege, naval and sally-out battles (`Mission.GetAgentTroopClass`). And the mapper reads `Skirmisher` as Infantry and Ranged, `LightCavalry` as Cavalry and `General`, `Bodyguard` and `Unset` as None, where the engine's `TroopClassExtensions.DefaultClass` reduces them to Ranged, HorseArcher and Infantry; no shipped character has those default groups (every `default_group` is Infantry, Cavalry, Ranged or HorseArcher), so the difference is unreachable today.

**Troop counters (#769 E10).** Three class-filtered types that let the player's career blunt what an enemy does to the leader's own non-hero troops. They are read through `CareerTroopHooks` (`Main/Features/CareerSystem/Hooks/`), which reduces the engine agent to a party leader id and a `TroopClassMask` (the rider for a mount; nothing for a hero: heroes are excluded as they are for `TroopDamage` and `TroopResistance`, and no hero-level counter exists, so a companion hero takes the full effect) and asks `ICareerAgentStatService`. The hooks are static and stateless; each caller hands in the service it already holds (the two models their injected one, the Dread Aura and signature strike runners by constructor from their mission logics, the race ability runtime by its constructor), so nothing is looked up per blow or per drain. The service does the maths and keeps a numbers-only tally of how often each counter changed a value (see "Evidence" below).
- **`TroopFearResistance`:** `ScaleTroopFear` multiplies the drain by `1 - min(m, 1)`, so 1.0 drains nothing. Applied at the four fear drains, each wrapping its `moraleModel.CalculateMoraleChangeToCharacter(victim, x)` call: `DreadPulseRunner` (Dread Aura), `SignatureStrikeRunner` (the fear burst), `RaceAbilityDeaths` (on-death fright) and `RaceAbilityTicker` (the aura; scaled before `_aura.Offer`, so the ledger's strongest offer is the scaled value). `BattleMoraleModel.CalculateMoraleChangeToCharacter` is deliberately NOT overridden: it also scales the morale a soldier gains from kills.
- **`TroopKnockdownResistance`:** `ScaleTroopKnockdownResistance` multiplies the engine's resistance by `1 + m`, last in `TaomAgentStatCalculateModel.GetKnockDownResistance` (after the race abilities). The engine multiplies it into `HealthLimit`, and vanilla returns `float.MaxValue` for a non-human, so a product that is not finite returns the input. The engine reads `GetKnockDownResistance` in its weapon knockdown check and in its horse-charge branch; a dismount that fails falls through to the weapon check, so all three get it (a Cavalry-class pip keeps riders in the saddle; dismount resistance itself is untouched); TAOM's own charge knockdown (Combat Mechanics, Branch B of `ChargeKnockdownService`) reads the same model through `CombatMechanicsHooks.BuildChargeKnockdownContext` and so applies the counter; its Branch A (overwhelming mass) ignores resistance by design. Hammerhand's shield bashes and kicks (Combat Mechanics, #895) never ask `GetKnockDownResistance`, so `CombatMechanicsHooks.BuildShieldBashContext` passes the counter's `1 + m` as the victim's resistance factor, which divides the knockdown chance. It does NOT cover TAOM's forced knockdowns, which set the flag and never ask for a resistance: Sauron's overhead slam (the struck foe and the ring; the Nine's strikes never knock down), the elephant-like creature attacks (elephant, mumakil trample and tusk swing, war ram, great elk, Animalia elk and moose) and the troll brute-force ring. A mount's resistance is `float.MaxValue` and comes back unchanged.
- **`TroopCrushHold`:** `HoldsAgainstCrush` is a chance `clamp(m, 0, 1)`: `TaomCombatMechanicsModel.DecideCrushedThrough` wraps its whole `??` chain in `CareerTroopHooks.ApplyCrushHold`, which turns a crush-through into a hold when the defender's roll lands below the chance, from any verdict (a troll's included), and never turns a hold into a crush. A troop whose leader holds no pip that reaches its class draws no random number; one whose leader holds such a pip draws once from the hold's own generator, a `System.Random` under its own lock, never from `MBRandom`. The engine reaches `DecideCrushedThrough` from `Mission.GetDefendCollisionResults`, a callback declared multi-thread callable, so the hold may run off the main thread: on a crush verdict it reads the defender's map-event party, takes the passive-cache lock and draws its own locked generator: `MBRandom.RandomFloat` is `Game.Current.RandomGenerator`, whose XorShift state two concurrent draws corrupt (Codex reproduced it against the installed v1.5.5 DLL, 2026-10-15). The older `CombatMechanicsHooks.CrushThrough` draw on the same path still uses `MBRandom`, a follow-up (the thread inventory is in `.claude/rules/harmony-patches.md`). When the hold succeeds it also counts it in the stat service's troop counter tally under that service's log lock, and a first fault in the hook records the hook under the same lock, then logs once through the thread-safe `FileLogger`. Race Abilities' `crush forced` tally can count a crush this hold then undoes.
- **Class filter.** All three honour `troop_class` through `GetTroopMagnitude` (no hit mask), so `troop_class="Infantry"` shields only the infantry; a troop with no class gets the `All`-class pips. They read no hit, so an `attack_type_mask` on them is ignored with a load warning.
- **Fail open, and say so once.** The hooks never throw: no agent, a hero, no party leader or no service hands the input back, and so does an exception (the engine's answer stands; a crush that could not be checked stays a crush). The first fault of each hook is logged once, `[CareerPerks] CareerTroopHooks.<hook> failed, input kept: ...`, through `ICareerAgentStatService.ReportHookFault`, so a counter that has gone inert after a refactor or an engine bump leaves a line in the log.
- **Evidence.** The service tallies each time a counter changed a value, per leader and troop class (fear scaled, knockdown scaled, crush held; a roll that did not hold is not counted), and `ResetDiagnostics` writes one `[CareerPerks] troop counter summary` line per leader and class at mission end, beside the hit summary. A count is not an outcome count: it shows the counter changed a value for that leader and class, and it can exceed the effects that landed. A charge can read the resistance twice when the Combat Mechanics charge rule is off, Branch A ignores the raised value (a Hammerhand bash or kick counts once, and only when Hammerhand computes its roll), every overlapping race aura offer counts though only the strongest applies, and a drain dropped later still counts. The crush count is exact. The hit tallies and their DEBUG lines carry the troop class too (`[Melee, Cut] class Infantry`; a hero subject has no class text), so an infantry hit and a cavalry hit of one leader are told apart. Reading "the infantry bonus applies to infantry only" off the log needs an `All`-class `TroopDamage` pip beside the class pip, or an army of one class: a class-only pip leaves a wrong-class hit untallied, because no term fires for it.
- **Campaign only.** Hammerhand's bash and kick path asks the knockdown counter in Custom Battle too, where it falls through for the reason below. The Custom Battle twins (`TaomCustomBattleAgentStatCalculateModel`, `TaomCustomBattleDamageModel`) do not carry them: a Custom Battle agent origin is not a party, so no career hero ever resolves there. `CareerTroopHooksWiringTests` (no game needed) pins that every `CalculateMoraleChangeToCharacter(` call under `Main/Features` is wrapped in `ScaleFearDrain`, both model expressions, the services handed in and the twins' absence in source, and `CustomBattleDamageModelTests` lists the crush hold as a campaign-only call.
- **Shipped on Gondor only:** each of the three types ships in 3 pips, one per Gondor career (#769 C2, fork slot 4); no other career uses them.

**Campaign consumers (#769 E11).** Ten types, each read by one GameModel override. A cost, a consumption or a penalty is authored negative for a cut (`RecruitCost`, `FoodConsumption`, `TradePrices`), as `TroopWages` and `TroopUpgradeCost` already are. `DailyGold` and `TroopTraining` are counts; the rest are fractions. `InfluenceGain`, `RecruitCost` and `TroopTraining` ship in 3 pips each (the Gondor forks, #769 C2); the other seven are in no pip yet. The generator's `tools/career_design/_rules.json` holds the unit, the benefit sign and the cap of each (see [the generator](career-forks-generator.md)). The key hero is chosen per seam, and the callers below are from the v1.5.5 decompile.

| Type | Override | Key hero | Rule and traps |
|---|---|---|---|
| `InfluenceGain` | `TaomBattleRewardModel.CalculateInfluenceGain` | owner-first (`CareerPassiveHero.ResolveId`), like `RenownGain` | Base first, then `ApplyFactor`. Vanilla pays 0 outside a kingdom and the factor keeps 0. Sole caller `MapEvent.cs:1620`, once per winning party with a LeaderHero. Event-scoped |
| `RecruitCost` | `TaomPartyWageModel.GetTroopRecruitmentCost` | the buyer hero | A full replacement that rebuilds the cost from an int, so the factor goes INTO `WageModifierService.CalculateRecruitmentCost` (`RecruitmentPerkInputs.CareerRecruitCostFactor`) and sums with the perk discounts and mounted feats before `LimitMin(1)`; a factor on the rebuilt number would sit outside the floor. Callers (v1.5.5), by the buyer they pass: `Hero.MainHero` for the recruit screen (`RecruitVolunteerTroopVM.cs:271`, `RecruitmentVM`), tavern mercenaries (`RecruitmentCampaignBehavior`), the `LordNeedsGarrisonTroops` quest's `RewardGold` (1500 + 1.5 x cost x N, so it shrinks with a `RecruitCost` cut, as it does with vanilla's buyer perks), the two tutorial callers (SandBox.GauntletUI `TutorialHelper` and StoryMode `TutorialPhase`) and TAOM's SupplyLines `SupplySourceService`, so the shown price is the charged price; `mobileParty.LeaderHero` for AI recruitment (an AI hero holds no career, so no change); the town owner clan's leader for the garrison auto-recruit bill (`GarrisonRecruitmentCampaignBehavior.cs:89`), which gets the cut when the player owns the fief; and null for the ransom value (`DefaultRansomValueCalculationModel`) and the upgrade price (`DefaultPartyTroopUpgradeModel`), which get none |
| `PrisonerConversion` | `TaomPrisonerRecruitmentCalculationModel.GetConformityChangePerHour` | `party.LeaderHero` | Base first, then `ApplyFactor`. Sole caller `RecruitPrisonersCampaignBehavior.cs:100`: the rounded result times the hours elapsed, one prisoner stack per tick (the main party hourly, an AI party once a day for 24 hours) |
| `FoodConsumption` | `TaomFoodConsumptionModel.CalculateDailyFoodConsumptionf` | `party.LeaderHero` | After the cultural feats. The number is negative, so a negative magnitude eats less; the base's `LimitMax(-0.01)` stays on the number. Each party of an army is asked separately |
| `WoundRecovery` | `TaomPartyHealingModel.GetDailyHealingForRegulars` | `party.LeaderHero` (not owner-first: that would heal the garrisons, militias and the player's caravans too) | Through `ApplyWoundRecovery`: vanilla returns a negative value for a starving party, or a raft party whose -0.25 per regular outweighs its healing, and +1 when `isPrisoners` is true (the captor healing prisoners); the factor stops at -1. The non-prisoner caller is `PartyBase.HealingRateForMemberRegulars`: the main party heals hourly, other mobile parties four times a day, settlement parties daily. TAOM now overrides this regulars path, positive values only, so the starvation kill stays vanilla. `HeroHealing` is unchanged, and still has both flaws (its starving value and its prisoner call); fixing them is a separate follow-up |
| `DailyGold` | `TaomClanFinanceModel.CalculateClanGoldChange` and `CalculateClanIncome` | `clan.Leader` | One `ApplyFlat` per override, last, and OUTSIDE the enlistment wage line's `applyWithdrawals` guard: `ClanVariablesCampaignBehavior.DailyTickClan` pays with `CalculateClanGoldChange(applyWithdrawals: true)`, so that call is the payment and the tooltip and the income tile are projections that must carry the same term. The two overrides do not delegate to each other, so there is no double count. The term goes in after the base's wage withdrawals, and vanilla budgets the wages from `clan.Gold` plus the change so far (`DefaultClanFinanceModel` :816, :828), so on a day the clan cannot afford them the DailyGold shows in the number but cannot cover that day's unpaid wages. The transfer is silent (`disableNotification: true`), but for the player clan `DailyTickClan` then shows "Daily Gold Change: {CHANGE}" (`dPD5zood`, v1.5.5 :427-432) with the same rounded payment, so that message shows the paid total, DailyGold included, and is evidence a tester can read |
| `LootGold` | `TaomBattleRewardModel.GetLootGoldChances` | the winner's own `Party.LeaderHero` | The list is rebuilt with each winner's share scaled (`CareerLootShares.Scale`). `MapEvent` pays `plundered * share` to the LeaderHero with no renormalisation, so a bonus mints gold and the other winners keep theirs. Owner-first would pay the bonus on a caravan the player owns. Sole caller `MapEvent.cs:1271`. Event-scoped |
| `RaidLoot` | `TaomRaidModel.GetRaidLootMultiplier` | `receivingParty.LeaderHero` | Read four ways (`RaidEventComponent` :338, :419, :445, :464): the village gold (`AttackerSide.LeaderParty`), once per drawn item, the village's production goods, and the common-loot roll at 0.25 x the multiplier. The item is kept with chance 0.5 x the multiplier, so a total of +100% makes every drawn item certain and the pip should be capped there. A player inside an AI army gets no gold bonus (the army leader's party is the receiver) but does get the item bonus on his own draws |
| `TroopTraining` | `TaomPartyTrainingModel.GetEffectiveDailyExperience` (the Seasoned Hosts model, `SeasonedHosts/Models/`; the career term follows the Seasoned Hosts factor) | `party.LeaderHero` | A flat count per troop per day, class-filtered by `TroopClassMapper.FromCharacter(troop.Character)`. The base is 0 for the player's party and clan, so a factor would do nothing. Sole caller `MobilePartyTrainingBehavior.OnDailyTickParty`: `Round(ResultNumber * stack size)` added to each non-hero stack of every mobile party |
| `TradePrices` | `TaomPartyTradeModel.GetTradePenaltyFactor` (new) | `party.LeaderHero` (the client party) | `DefaultTradeItemPriceFactorModel.GetTradePenalty` multiplies its raw penalty by this factor, and `GetPriceFactor` turns the penalty into the buy price (`base * (1 + penalty)`) and the sell price (`base / (1 + penalty)`): one hook, both directions, and the only caller. Narrower than overriding `GetTradePenalty`. The scale floors at 0, so a cut of -100% or more is no penalty at all and never turns it into a discount under the base price. Owner-first would discount every caravan the player owns. The supply prices TAOM reads through `Town.GetItemPrice` move too |

**In-game checks, with the evidence each leaves.** Run `taom.career_perks` first: it lists the value of each held type (per class for `TroopTraining`). `InfluenceGain` and `LootGold` write `[CareerPerks] InfluenceGain +N% for 'hero': a -> b` and `[CareerPerks] LootGold +N% for 'hero': share -> share` once per battle end, only when the number moved, so a battle outside a kingdom prints no influence line. The other eight write nothing, by design: `RecruitCost` is read off the recruit screen price, `DailyGold` off the clan tooltip, the "Daily Gold Change" message (it shows the paid total, DailyGold included) and the day-to-day change in the leader's gold, `TroopTraining` off a stack's experience on the next daily tick, `PrisonerConversion` off the prisoner stack's progress, `FoodConsumption` off the party tooltip's daily food, `WoundRecovery` off the party tooltip's healing rate, `TradePrices` off a town's buy and sell prices, and `RaidLoot` off the village gold message and the loot rolled. A silent log for these is not evidence either way.

**Effect-type consumers — the canonical map.** `PassiveEffectConsumers` is the compiled source of
truth for *whether* a type is read; the table below is the source of truth for *where*. Both bugs of
2026-08-06 (`Health`, `TroopDamage`) passed the compiled gate and still did nothing the player could
see, because the gate cannot ask the only question that matters: **does the consumer govern the same
system the pip's text promises?** Keep this table in step with the code — it is what makes that audit
repeatable in one pass instead of a re-derivation.

Counts are shipped pips in `taom_career_choices.xml` (1,783 `PassiveEffect` elements across 30 types, counted outside XML comments; a 0 means the
type is wired but unused by any shipped pip, which is fine — it is the reverse, a used type with no
consumer, that the load gate and `CareerChoicesIntegrationTests` reject).

| Effect | Shipped pips | Consumer | Layer | Notes |
|---|---:|---|---|---|
| `Health` | 208 | `TaomCharacterStatsModel.MaxHitpoints` | campaign + mission | mission inherits it free via SandboxAgentStatCalculateModel's hero branch; never add it on the agent path too |
| `Damage` | 258 | `CareerAgentStatService.CalculateDamageAmplification` | mission | hero only; mask-gated by attack_type_mask |
| `Resistance` | 138 | `CareerAgentStatService.CalculateDamageReduction` | mission | hero only; mask-gated |
| `ArmorPenetration` | 61 | `CareerAgentStatService.CalculateDamageAmplification` | mission | hero only |
| `SwingSpeed` | 65 | `CareerAgentStatService.ApplyAgentStatModifiers` | mission | props.SwingSpeedMultiplier |
| `MovementSpeed` | 65 | `CareerAgentStatService.ApplyAgentStatModifiers` | mission | props.MaxSpeedMultiplier |
| `Ammo` | 20 | `TaomAgentStatCalculateModel.InitializeMissionEquipment` via `CareerAmmoApplier` | mission | multiplicative refill of every consumable slot (arrows, bolts, thrown) the way vanilla's Deep Quivers / Fletcher / Well Prepared do it: before the agent is built, `MissionEquipment.SetAmountOfSlot(slot, amount, addOverflowToMaxAmount: true)`, which raises the stack's max with the amount. Since #613; before it the refill ran in `OnAgentBuild` through `Agent.SetWeaponAmountInSlot` above an unchanged max (the engine caps pickups at `ModifiedMaxAmount`, so a full stack could not be trusted to take it) and gated on `IsAnyAmmo`, which skips thrown weapons, so the nine pips on the javelineer careers did nothing |
| `ShrugOff` | 0 | `CareerAgentStatService.ShouldShrugOffBlow` | mission | hero victim only |
| `MountHealth` | 61 | `CareerAgentStatService.ApplyMountHealthPassives` | mission | the MOUNT, keyed on its rider; multiplicative (a fraction; the Active Effects panel prints it as a percentage via `IsFlat`) |
| `MountChargeDamage` | 93 | `CareerAgentStatService.ApplyMountStatModifiers` | mission | the MOUNT, keyed on its rider (#611); multiplicative. Until 2026-09-17 it multiplied the RIDER's copy, which no base model writes and the engine never reads, so the pip was a phantom for three months while listed as consumed |
| `TroopDamage` | 149 | `CareerAgentStatService.CalculateDamageAmplification` **+** `TaomRaidModel.CalculateHitDamage` | mission + campaign | TWO consumers on purpose: battle damage for the leader's non-hero troops, plus settlement raid speed; the battle half honours `attack_type_mask` and `troop_class` (the raid half reads the army-wide pips only) |
| `TroopResistance` | 44 | `CareerAgentStatService.CalculateDamageReduction` | mission | the leader's non-hero troops; honours `attack_type_mask` and `troop_class` |
| `TroopFearResistance` | 3 | `CareerTroopHooks.ScaleFearDrain` -> `CareerAgentStatService.ScaleTroopFear` | mission (campaign) | the four fear drains; class-filtered; not the morale model |
| `TroopKnockdownResistance` | 3 | `CareerTroopHooks.KnockDownResistance` in `TaomAgentStatCalculateModel.GetKnockDownResistance` | mission (campaign) | multiplies the resistance; class-filtered; the engine's weapon, charge and dismount knockdown checks and Hammerhand's bashes and kicks (through `BuildShieldBashContext`), not TAOM's forced knockdowns |
| `TroopCrushHold` | 3 | `CareerTroopHooks.ApplyCrushHold` in `TaomCombatMechanicsModel.DecideCrushedThrough` | mission (campaign) | a chance to turn a crush-through into a hold; class-filtered |
| `PartyMovementSpeed` | 68 | `TaomPartySpeedModel` | campaign map |  |
| `PartySpottingRange` | 64 | `TaomMapVisibilityModel.GetPartySpottingRange` | campaign map |  |
| `StealthBonus` | 49 | `TaomMapVisibilityModel.GetPartySpottingRatioForMainPartySeeingRange` | campaign map | **NOT WIRED IN PRACTICE (#614):** that engine method decides whether an AI party is visible to the PLAYER (its only caller is `CalculateMobilePartyVisibilityAndInspected`), the player's party is never its argument, and careers are player-only, so the 49 shipped pips have never applied. The AI detects the player through a flat engage radius, not a seeing range; replan pending |
| `PartySize` | 73 | `TaomPartySizeModel` | campaign | flat; keyed on `party.LeaderHero` only since #768, not owner-first, so it grows the party its hero leads and no caravan, garrison, militia, villager, patrol, refuge or supply-caravan party of the owner's clan. Every `RefreshCache` invalidates the engine's cached size limit for the parties the affected heroes lead (`IPartySizeCacheInvalidator.InvalidateLedBy`, covering heroes who gained or lost passives). The `Career` line in the party-size tooltip shows the flat count divided by every factor already on the number (culture feats and, when it applies, the AI lord factor), so check the total, not the line |
| `CompanionLimit` | 0 | `TaomClanTierModel` | campaign |  |
| `InventoryCapacity` | 0 | `TaomInventoryCapacityModel.CalculateInventoryCapacity` | campaign | applied with `ApplyFactor` (a fraction; the Active Effects panel prints it as a percentage via `IsFlat`) |
| `TroopMorale` | 99 | `TaomPartyMoraleModel.GetEffectivePartyMorale` | campaign |  |
| `TroopWages` | 68 | `TaomPartyWageModel` | campaign | negative magnitudes |
| `TroopUpgradeCost` | 1 | `TaomPartyTroopUpgradeModel` | campaign | negative |
| `TroopSurvival` | 26 | `TaomPartyHealingModel.GetSurvivalChance` | post-battle | raw float, not ExplainedNumber: `ScaleByPassive`, capped at 1 |
| `HeroHealing` | 13 | `TaomPartyHealingModel.GetDailyHealingHpForHeroes` | campaign daily |  |
| `RenownGain` | 134 | `TaomBattleRewardModel` | post-battle |  |
| `SmithingCostReduction` | 0 | `TaomSmithingModel` | campaign |  |
| `InfluenceGain` | 3 | `TaomBattleRewardModel.CalculateInfluenceGain` | post-battle | factor; 0 outside a kingdom stays 0 (#769 E11) |
| `RecruitCost` | 3 | `TaomPartyWageModel.GetTroopRecruitmentCost` via `WageModifierService.CalculateRecruitmentCost` | campaign | negative = cheaper; summed before the 1-gold floor |
| `PrisonerConversion` | 0 | `TaomPrisonerRecruitmentCalculationModel.GetConformityChangePerHour` | campaign (main party hourly, an AI party daily x 24) | factor |
| `FoodConsumption` | 0 | `TaomFoodConsumptionModel.CalculateDailyFoodConsumptionf` | campaign daily | negative = eats less |
| `WoundRecovery` | 0 | `TaomPartyHealingModel.GetDailyHealingForRegulars` | campaign (main party hourly, other mobile parties four times a day, settlements daily) | positive values on the non-prisoner call only |
| `DailyGold` | 0 | `TaomClanFinanceModel.CalculateClanGoldChange` + `CalculateClanIncome` | campaign daily | flat count; tooltip, income tile and payment agree |
| `LootGold` | 0 | `TaomBattleRewardModel.GetLootGoldChances` | post-battle | the winner's own share, not renormalised |
| `RaidLoot` | 0 | `TaomRaidModel.GetRaidLootMultiplier` | campaign raid | gold, the item chance (0.5 x multiplier), the village production goods and the common-loot roll (0.25 x) |
| `TroopTraining` | 3 | `TaomPartyTrainingModel.GetEffectiveDailyExperience` | campaign daily | flat per troop per day; class-filtered |
| `TradePrices` | 0 | `TaomPartyTradeModel.GetTradePenaltyFactor` | campaign | negative = better prices, both directions |
| `SpecialResourceGain` | 4 | `SpecialResourceService` | campaign | scales every special-resource earning source, not only the daily income (#767) |
| `SpecialResourceUpkeepModifier` | 3 | `SpecialResourceService` | campaign | desertion and warnings judge upkeep after the modifier (#767) |
| `SpecialResourceUpgradeCostModifier` | 1 | `SpecialResourceService` | campaign |  |
| `BuffDuration` | 0 | `— none —` | n/a | reserved; deliberately unconsumed, and the phantom-gate test's exemplar |
| `Special` | 0 | `— none —` | n/a | parse fallback sentinel |

History: the six historically-dead "phantom" types — `Ammo`, `MountChargeDamage`, `MountHealth`,
`TroopResistance`, `StealthBonus`, `HeroHealing` — were wired 2026-06-25 and their ~211 magnitudes
re-tuned to a uniform 10-15% band via `tools/retune_phantom_passives.py` (`MountHealth` and `Ammo`
became multiplicative). `MountChargeDamage` was wired to the wrong agent that day (the rider's
properties instead of the mount's) and stayed a phantom until #611 (2026-09-17): the consumer set
answers "is anything reading it", not "is it read where the engine looks", which is the blind spot
`PassiveEffectConsumers.cs` names. A load-time gate (`CareerConfigProvider.ValidatePassiveConsumers`) plus a
shipped-XML regression test prevent a *fully* unconsumed type from shipping. Pure arithmetic for the
Ammo + StealthBonus consumers lives in the testable `CareerPassiveMath`. (Those six were previously
listed here under their pre-rename names `HorseChargeDamage` / `HorseHealth` / `HealthRegeneration`,
which no longer exist in `PassiveEffectType` — corrected 2026-08-06.)

### Ability Templates (`Main/_Module/ModuleData/career_system/taom_ability_templates.xml`)

Defines per-ability tunables: id, display name, duration (effect window), radius (AoE), `max_charge` (dead since #103 cooldown rework — present on the model for back-compat but unread by activation logic; designer mutations targeting it were repurposed as `CooldownReduction` in #104), `cooldown_reduction` (per-activation cooldown shortening in seconds, applied AFTER mutations have run; floored at `MinCooldownSeconds`), particle/sound effects, tooltip. Cooldown is *not* per-template; see [Cooldown System](#cooldown-system).

### Cooldown System

`Main/_Module/ModuleData/career_system/taom_ability_tuning.xml` declares a `<Global>` element shared by every career. Three tunables (the shipped file sets only `cooldown_seconds`):

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
- **`min_reportable_bonus_damage`: default 0.5 (#383).** Must be 0 or more; a negative value warns and falls back to the default. The threshold for the "(+N from ability)" damage attribution line (see the #383 bullet above).
- **Reload scope:** `CareerConfigProvider` is a `Reuse.Singleton` and caches the parsed config. Changes require a full Bannerlord application restart — not a save-load.
- **Per-career override:** Not supported at the tuning-config layer. **Per-keystone CooldownReduction is supported** — see CooldownReduction Mutations below.

#### CooldownReduction Mutations (#104, Option B)

A keystone choice can shorten the global cooldown for the next activation via a `<Mutation>` targeting the new `CooldownReduction` property on `AbilityTemplateData`:

```xml
<ChoiceGroup id="...">
  <Choice id="..._keystone_t1" type="Keystone">
    <Mutations>
      <Mutation target_id="<career_id>_ability" property="CooldownReduction" calculator="flat" value="6"/>
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

After the culture-default starting roster is applied at `OnCharacterCreationFinalize`, the player's career archetype drives a second roster application that overwrites the loadout. The archetype is one of four values:

| Archetype | Weapons | Armor |
|-----------|---------|-------|
| **Ranged** | bow + arrows + one-handed sidearm (Isengard, Erebor and Ered Luin: crossbow + bolts; Harad, Abanissa and Shaghana: javelins + shield) | the culture's lowest troop body + legs (Isengard and Mordor: their lowest uruk armour) |
| **Cavalry** | polearm + shield + sidearm + horse + harness (Ered Luin has no cavalry career, so no cavalry kit) | the same |
| **Infantry** | one-handed weapon + shield + polearm (Dol Guldur: two-handed axe) | the same |
| **TwoHanded** | two-handed axe + throwing axe, no one-handed weapon, shield or polearm (only Ered Luin's Mattock Hewer uses it so far; Ered Luin's mattock line carries the throwing axe from the Mattock Hewer, level 16, upward) | the same |

Since #629 every weapon and armour item is gear the culture's lowest troops carry, so a career never starts
better equipped than the troops it recruits, and a culture's archetypes share their armour. The pick
rule is in [starting-equipment-tuning.md](starting-equipment-tuning.md) "Career kits".

**Single source of truth:** [`CareerSystemIoC.GetCareerArchetypeMap()`](../../Main/Features/CareerSystem/CareerSystemIoC.cs) maps each careerId to a `CareerArchetype`. The same dictionary is consumed by the ability executor registry (Infantry/Ranged/Cavalry executors; a TwoHanded career runs the Infantry executor) and by [`ICareerArchetypeService`](../../Main/Features/CareerSystem/ICareerArchetypeService.cs). Cached in a static field, one allocation per app lifetime.

**Roster ID convention:** `player_career_{cultureId}_{infantry|ranged|cavalry|twohanded}_{f|m}`. Built by [`CareerEquipmentRosterIds.Build`](../../Main/Features/CharacterCreation/CareerEquipmentRosterIds.cs), looked up via `MBObjectManager.GetObject<MBEquipmentRoster>`. Rosters are authored in [`Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml`](../../Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml).

**Graceful fallback:** When no roster exists for a given (culture, archetype, gender) combination, [`CareerStartingEquipmentService`](../../Main/Features/CharacterCreation/CareerStartingEquipmentService.cs) logs a warning and leaves the already-applied culture default in place. This lets new cultures ship incrementally without code changes.

**Live preview during CC** ([`CareerMenuService.UpdateCareerEquipmentPreview`](../../Main/Features/CharacterCreation/CareerMenuService.cs)): when the player clicks a career option, two preview updates fire because two surfaces render the character from different sources:

1. **Career menu 3D agent** reads from the menu's `NarrativeMenuCharacter` buffer. Updated via `NarrativeMenuCharacter.SetEquipment(roster)` on the `player_career_character` — same pattern as `NarrativeMenuBuilder.UpdateYouthEquipment`.
2. **Review stage 3D agent** ([`CharacterCreationReviewStageView.AddCharacterEntity`](file:///E:/Decompiled_Bannerlord/Modules/SandBox.GauntletUI/SandBox.GauntletUI.CharacterCreation/CharacterCreationReviewStageView.cs)) reads from `Hero.MainHero.CharacterObject.Equipment` directly. Updated by running the same two-step apply chain that `OnCharacterCreationFinalize` does: `IPlayerEquipmentService.ApplyPlayerStartingEquipment` (resets to culture+title default) → `ICareerStartingEquipmentService.ApplyCareerStartingEquipment` (overlays career roster). This way switching careers (cavalry → ranged) starts from a clean culture-default slate rather than inheriting the previous career's overrides.

Same fallback policy as the runtime grant: missing roster → log + leave the youth/culture-default preview in place.

**`FillFrom` is a full replacement, not an overlay.** `Equipment.FillFrom(source)` (Equipment.cs:184-194, installed 1.4.8) copies all 12 slots unconditionally, so a slot the career roster omits is EMPTIED on the player; it does not inherit the culture-default value. That is why a careered player has no Head, Cape or Gloves (the career rosters set none) and why a ranged or infantry career roster needs no empty `<Equipment slot="Horse" id="" />` override to stay on foot. An earlier version of this paragraph said the opposite; `tools/wire_career_starter_armor.py`'s header (the script was deleted in #629) and [starting-equipment-tuning.md](starting-equipment-tuning.md) had it right, and `docs/modding/open-questions.md` recorded the contradiction until it was corrected on 2026-09-12. The one thing the career layer does NOT replace is the civilian set: the adapter applies battle and civilian sets independently, and the career rosters carry no civilian set, so the culture-default civilian set is the civilian kit for every culture.

### How to add a new culture's career rosters

1. Append 6 rosters to [`taom_career_starting_equipment.xml`](../../Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml),
   one per (archetype, gender), each with `culture="Culture.<id>"`: `Item0`-`Item2`, `Body`, `Leg` (a
   `twohanded` roster has no `Item2`; any existing ids as placeholders), and for cavalry `Horse` + `HorseHarness`, which are hand-kept (a dwarf
   culture only a war ram, and never a Horse without a harness). `Equipment.FillFrom` replaces the whole
   battle set, so ranged and infantry rosters simply omit the mount.
2. Run `python tools/generate_career_kits.py --apply`: it fills the kit's slots from the culture's troops by
   the rule in [starting-equipment-tuning.md](starting-equipment-tuning.md) "Career kits" (lowest troop gear,
   non-vanilla preferred up to level 21, bows and crossbows by lowest skill requirement). A culture whose sidearm or
   second weapon is not a sword or a polearm gets a row in the tool's `SIDEARM`/`SECOND` tables first, a ranged
   career that is not a bow career a row in `RANGED`, and a culture whose armour must come from one line a row
   in `ARMOUR_LINE`. No
   new items: the culture's troops already carry them. `--verify` and `StarterKitCoverageTests` pin the result.
3. If the culture is one of the five vanilla-mapped ones, re-run `python tools/wire_starter_kit_rosters.py --apply`
   so its careerless override follows the new kit.
4. Verify the archetype for each career in [`CareerSystemIoC.GetCareerArchetypeMap()`](../../Main/Features/CareerSystem/CareerSystemIoC.cs) and adjust if needed.
5. Run `StarterKitCoverageTests`, `python tools/audit_polearm_shield_parity.py` (a spear beside a shield must
   resolve one-handed) and `python tools/validate_moduledata.py`.
6. No code change required: `ICareerStartingEquipmentService` looks up by string-id at runtime.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CareerSystem/Domain/` (22 files) | Enums + immutable data classes, `PassiveEffectConsumers`, `TroopClassMask` and `TroopClassMapper` |
| `Main/Features/CareerSystem/ICareerDataService.cs` | Per-hero career state CRUD |
| `Main/Features/CareerSystem/CareerConfigProvider.cs` | XML config loading |
| `Main/Features/CareerSystem/CareerRegistry.cs` | Career lookup, eligibility, tier gating |
| `Main/Features/CareerSystem/CareerPassiveService.cs` | Session-scoped passive effect cache |
| `Main/Features/CareerSystem/CareerLootShares.cs` | The `LootGold` rule over the battle reward model's winner shares, generic over the winner so it runs without the game (#769 E11) |
| `Main/Features/CareerSystem/CareerPassiveMath.cs` | Pure arithmetic for the Ammo and StealthBonus consumers (`ApplyFactor` / `ApplyFlat` live on `CareerPassiveService`) |
| `Main/Features/CareerSystem/Mutations/` (6 files) | Calculator registry + built-in calculators + mutation service |
| `Main/Features/CareerSystem/Abilities/` (25 files, plus 3 in `Executors/`) | CareerAbility (with `AdjustCooldown`), ability service (with `ApplyCooldownAdjustment`), effect registry, the 3 executors, buff tracker, execution context, `IAbilityActivationController` / `AbilityActivationController`, `IAbilityEffectExecutor` / `AbilityEffectExecutor` with `IAbilityInputAdapter` + `IMissionTimeProvider` + impls, and `CareerAgentStatService` (per-hit maths, troop counters and their tallies) |
| `Main/Features/CareerSystem/Hooks/CareerTroopHooks.cs` | The three troop counters' seam: fear, knockdown resistance and crush hold, each taking the `ICareerAgentStatService` the caller holds |
| `Main/Features/CareerSystem/Diagnostics/` (6 files) | `taom.career_perks`: `CareerPerkRows` (row gathering), `CareerPerkMissionProbe` (mission block), `CareerPerkReport` (rendering, `ByClass`), `CareerPerkConsumerMap` (consumer text), `CareerPerkSnapshot`, `CareerPerkLogPolicy` |
| `Main/Features/CareerSystem/CareerCampaignBehavior.cs` | Campaign lifecycle events |
| `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` | Thin entry point (237 lines by `wc -l`, over the ADR-002 ceiling as the Battle bullet records; refactored #102), delegates to the two controllers + owns `_activeContexts` expiration list + `OnEndMission` per-step try/catch teardown |
| `Main/Features/CareerSystem/CareerCreationHandler.cs` | Character creation integration; also the one place a career is assigned (set, stamp the tree version, seed the root) |
| `Main/Features/CareerSystem/CareerSwitchService.cs` | Career switching with validation and the fork-pick gold charge (a plain class; its interface was deleted in #769) |
| `Main/Features/CareerSystem/CareerChoiceRules.cs` | The single take/refund rule (`CanTake`, `CanRefund`, `HoldsForkPick`) returning a `ChoiceLockReason` |
| `Main/Features/CareerSystem/CareerTreeVersion.cs` | The `career_tree_vN` hero flag: `StoredVersion` reads it, `Stamp` is its only writer |
| `Main/Features/CareerSystem/CareerLifecycleService.cs` | Foreign-choice repair, paid retrain (`TryRetrain`), tree-version migration (`MigrateAllTreeVersions` on the interface `ICareerLifecycleService.cs`; the per-hero `MigrateTreeVersion` on the class) |
| `Main/Features/CareerSystem/KeystoneExclusivityRule.cs` | One keystone per tier; consulted by `CareerChoiceRules.CanTake` for non-fork groups |
| `Main/Features/CareerSystem/Domain/ChoiceLockReason.cs`, `CareerRetrainResult.cs`, `CareerSwitchResult.cs` | Why a take or refund is refused; the retrain and switch outcomes |
| `Main/Adapters/ICareerPurseAdapter.cs`, `CareerPurseAdapter.cs` | The player's gold as the career system sees it (`Gold`, `ChargeGold`) |
| `Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml` | The career screen prefab; the diamond is the click target (`ExecuteToggleChoice`) |
| `Main/Features/CareerSystem/UI/` (9 files) | Career screen + VM hierarchy (`CareerScreenVM`, `CareerChoiceObjectVM`, `GauntletCareerScreen`), `CareerEffectDisplayMap`, `CareerEnergyBarStateMapper`. See [gui-sprite-system.md](gui-sprite-system.md) |
| `Client/Features/CareerSystem/UI/` (5 files) | TAOM.Client side (ADR-013): `TaomCareerDiamondWidget`, `CharacterDeveloperCareerMixin` and `MissionAgentStatusCareerMixin` (UIExtenderEx mixins), `CareerButtonPrefab` and `CareerEnergyBarPrefab` (prefab injections) |
| `Main/Features/CareerSystem/Models/` (6 files) | TaomAgentApplyDamageModel, TaomAgentStatCalculateModel, TaomClanTierModel, TaomInventoryCapacityModel, TaomMapVisibilityModel, TaomPartyTradeModel (the training model is `SeasonedHosts/Models/TaomPartyTrainingModel.cs`, which also carries the career `TroopTraining` term) |
| `Main/Adapters/ICareerHeroAdapter.cs` | Wraps Hero for service boundary |
| `Main/Adapters/ICareerHeroAdapterFactory.cs` | Factory for GameModel boundary |
| `Main/Features/CareerSystem/Domain/CareerArchetype.cs` | `enum CareerArchetype { Infantry, Ranged, Cavalry, TwoHanded }` |
| `Main/Features/CareerSystem/CareerArchetypeService.cs` | careerId → archetype lookup; backed by static map in `CareerSystemIoC` |
| `Main/Features/CharacterCreation/CareerStartingEquipmentService.cs` | Applies archetype roster at end of CC over the culture default |
| `Main/Features/CharacterCreation/CareerEquipmentRosterIds.cs` | Roster ID builder: `player_career_{culture}_{archetype}_{f\|m}` |
| `Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml` | Per-(culture, archetype, gender) rosters: 156 across 26 cultures, every playable culture (Ered Luin's six cover its ranged, infantry and two-handed careers only), each culture's lowest troop gear (#629; the last nine on 2026-10-23 with placeholder mounts) |

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
| CareerDataServiceTests | 27 | CRUD + persistence round-trip, flags, `ResetForNewCampaign` |
| CareerConfigProviderTests | 81 | XML parsing + missing file, `max_picks`, `point_levels`, `tree_version`, retrain gold, multi-effect options and the authored-element cap, `troop_class` (fail closed, the `attack_type_mask` grammar), `attack_type_mask` only on the mask-gated types, the class-filtered and mask-gated sets, a NaN or infinite `magnitude` loading as 0 |
| CareerRegistryTests | 55 | Lookup + eligibility + tier gating, `point_levels` budgets, `GetNextPointLevel`, `GetRetrainCost` |
| MutationCalculatorRegistryTests | 8 | All 5 built-in calculators |
| CareerPassiveServiceTests | 71 | Cache refresh + magnitude aggregation, mask buckets, `GetTroopMagnitude` by troop class and hit mask, `ApplyFactor` / `ApplyFlat` reading a class-filtered type army-wide, `ScaleByPassive` (scale, the floor at 0 with no cliff at -1, event line only when the number moved), `ApplyFlat` by troop class (literal count after a factor, a NaN factor frame), `ApplyWoundRecovery` (starving, zero, NaN and prisoner paths untouched, a total below -1 never wounds) (`RequiresGame`) |
| CareerCampaignPassivesTests | 25 | The ten E11 types: ordinals after `TroopCrushHold`, consumed, the event-scoped set, a `Describe` line naming consumer and evidence, English lines, the loot share rule (no game needed), and source pins on the model bodies, the ten reads and the `TroopSurvival` scale (statements in order, run against deleted, swapped, re-keyed and assignment-dropped mutants). Two of the 24, the English-line tests, need the game (`RequiresGame`) |
| CareerTroopCounterTests | 34 | The three counter maths: no leader, zero, negative, NaN, class mismatch, full fear resistance, `float.MaxValue` knockdown input, no RNG draw without the perk, roll below and at the chance; the mission-end counter summary per leader and class; `ReportHookFault` logs once per hook; enum coverage (`RequiresGame`) |
| CareerTroopHooksTests | 13 | The hook seam fails open without a service, an agent or a leader; a throwing service hands the input back and is reported once per hook (`RequiresGame`) |
| CareerTroopHooksWiringTests | 8 | Source pins, no game needed: every morale call under `Main/Features` wrapped in the fear counter, each site's injected service, the two model expressions, no service lookup in the hooks, the Custom Battle twins |
| CareerPerkRowsTests | 7 | `taom.career_perks` rows: hit-kind breakdown for the mask-gated types only, per-class view for the class-filtered ones (`RequiresGame`) |
| CareerEffectLocalizationTests | 8 | Every effect label and the six class phrases are keyed literals registered in all 13 strings files; the five retired fragment keys are gone; the strings file ends with one blank line |
| TroopClassMaskTests | 7 | `TroopClassMapper`: an explicit expected row for every `FormationClass` value (a new engine value fails), `troop_class` name parsing in the `attack_type_mask` grammar |
| MutationServiceTests | 5 | Template cloning + mutation application |
| MutationParamsTests | 7 | GetFloat: valid, missing key, unparseable, negative, NaN, ±Infinity |
| CareerAbilityTests | 29 | Charge types + cooldown + activation + ReadyProgress01 |
| CareerAbilityServiceTests | 17 | Force-CooldownOnly + configured cooldown duration + GetCooldownRemaining (hero present/absent) + IsAbilityReady transitions |
| AbilityActivationControllerTests | 19 | V-key state machine: NoCareer no-op, JustBecameReady one-shot, simultaneous-flag emit, charging throttle window, Reset clears both flags |
| CooldownReductionTests | 15 | AbilityTemplateData copy-ctor, GlobalTuning ctor, AdjustCooldown happy/floor/zero/negative/NaN/Infinity/charge-based no-op, service ApplyCooldownAdjustment unknown-hero + floor |
| CareerCreationHandlerTests | 6 | CC flow + root choice + tree-version stamp |
| CareerSwitchServiceTests | 19 | Switch validation, choice reset, the fork-pick charge and `CareerSwitchResult` |
| CareerScreenVMTests | 48 | VM state, choice selection, the click path, forks and retrain (`RequiresGame`) |
| CareerChoiceRulesTests | 34 | `CanTake` / `CanRefund` / `HoldsForkPick`: every `ChoiceLockReason` and the check order |
| CareerRetrainTests | 28 | `CareerLifecycleService`: `RefundAll`, `TryRetrain`, `MigrateTreeVersion` (refund once, proof the tree loaded, a newer stored version untouched) and `MigrateAllTreeVersions` (every hero, the player's count returned) |
| CareerTreeVersionTests | 13 | `FlagFor`, `StoredVersion`, `Stamp` |
| CareerSessionLaunchOrderTests (in `CareerCampaignBehaviorTests.cs`) | 1 | Pins `OnSessionLaunched` order: repair, migration, cache refresh |
| CareerScreenChecksTests | 4 | The #971 check ids, the failure reason and mode helpers, and the rendered `[Check]` lines the reader fixtures carry |
| CareerScreenPrefabBindingTests | 2 | Every `@Property` and `Command.*` in `CareerScreen.xml` resolves on its VM (`RequiresGame`) |
| CareerLifecycleServiceTests | 16 | Legacy-career fallback and the foreign-choice repair pass |
| CareerChoiceObjectVMTests | 6 | Diamond icon (first passive, keystone fallback) and the unavailable state (`RequiresGame`) |
| CareerEffectDisplayMapTests | 15 | Icon, label and Active Effects line format per effect type, the three counters, the class suffix; one Icons row per enum value (`RequiresGame` on 3 of the 15, the `Format` tests) |
| CareerChoicesIntegrationTests | 15 | Shipped XML: groups, passive cap, original trees unchanged by multi-effect, `troop_class` and mask placement, no phantom type, no magnitude above the loader bound |
| CareerChoiceIntegrityTests | 7 | Shipped XML: groups, roots and ability templates resolve, single ownership, a fork or paced career is `tree_version` 2 or more, no career mixes fork and unlimited groups |
| CareerPerkReportTests | 18 | `taom.career_perks` rendering, `ByClass`, a per-class count row for `TroopTraining`, a consumer line for every consumed type |
| CareerPerkMissionTeardownTests | 7 | The mission-end hit summary on `OnEndMission` and `OnRemoveBehavior` (`RequiresGame`) |
| CareerAgentStatServiceTests | 88 | Per-hit amplification and reduction, hit lines and tallies, summary formats (`RequiresGame`) |

## How-To

### Add a new career
1. Add `<Career>` element to `taom_careers.xml` with unique id, eligible cultures, choice groups
2. Add `<ChoiceGroup>` elements to `taom_career_choices.xml` (6 groups: 2 per tier, each with 1 keystone + 4 passives)
3. Add `<Choice id="xxx_root">` as the root choice
4. Add ability template to `taom_ability_templates.xml`
5. No C# changes required
6. For a tree of forks (`max_picks`, `point_levels`, `tree_version`), write it with [the generator](career-forks-generator.md) instead of by hand

### Add a new mutation calculator
1. Add function to `BuiltInCalculators.RegisterAll()`. Read float parameters only through
   `MutationParams.GetFloat`, which returns the default for a missing, unparseable, NaN or
   Infinity value; a float read any other way needs its own `FiniteFloatValidator` guard.
2. Reference by id in XML `<Mutation calculator="your_id" ... />`

### Add a new PassiveEffectType
1. Append the enum value at the END of `PassiveEffectType.cs`. The loader reads `type` with `Enum.TryParse` (`CareerConfigProvider.ParsePassive`), which also takes a digit string as an ordinal, so an ordinal shift would move any numeric reader; `CareerTroopCounterTests.TheThreeCounters_AreAppendedAfterTheExistingValues` pins the order of the three counters and `CareerCampaignPassivesTests.TheTenTypes_AreAppendedInOrderAfterTheTroopCounters` pins the E11 ten at their ordinals after `TroopCrushHold`; a new value goes after both and changes neither.
2. Add the consumer: a GameModel that calls `ICareerPassiveService.ApplyFactor` / `ApplyFlat` for a campaign effect (`ScaleByPassive` for a float it returns, `ApplyFlat` with the troop class for a count by troop class, `ApplyWoundRecovery` for a value that can be negative; list every engine caller of the method from the decompile, null-argument calls included, in the override's comment), or a method on `CareerAgentStatService` for a mission one (a troop counter goes through `CareerTroopHooks`, and the caller passes in the service it holds; a per-hit class-filtered type reads the class at the damage boundary, `TroopClassMapper.FromCharacter`).
3. Add the type to `PassiveEffectConsumers.Consumed` (the load-time phantom gate and the shipped-XML test read it) and give it a line in `CareerPerkConsumerMap.Describe` (`CareerPerkReportTests.Describe_EveryConsumedType_NamesAConsumer` fails without it). If the magnitude is a count, add it to `CareerPerkConsumerMap.IsFlat` as well. Say in the `Describe` line what evidence the read leaves: a `[CareerPerks]` line only for an event (`CareerPerkLogPolicy.IsEventScoped`, pinned by `CareerCampaignPassivesTests`), otherwise "no runtime line; taom.career_perks shows the value".
4. Add its diamond icon (`CareerEffectDisplayMap.Icons`) and label (`Labels`, written `{=taom_career_effect_<snake_case of the enum name>}{VALUE} default`, so the value sits inside the label and each language orders number and noun; the number comes from `taom_career_value_percent` (`{NUMBER}%`) or `taom_career_value_flat` (`{NUMBER}`)), register that key in `taom_module_strings.xml` and seed it in all 12 `std_taom_module_strings_*.xml` language files; `CareerEffectDisplayMapTests` and `CareerEffectLocalizationTests` fail on a missing icon, label or key. Decide count or fraction from what the consumer applies, then add a count to `CareerEffectDisplayMap`'s flat set and keep a fraction out of it. The flat set fails both ways: a count left out prints as a percentage (+2 shows as "+200%"), and a fraction put in rounds to 0 and prints nothing.
5. Add the type to `ClassFilteredTypes` in `PassiveEffectConsumers.cs` when its consumer passes a troop class, and to `MaskGatedTypes` when it passes the hit (exposed as `ClassFiltered` and `MaskGated`); `CareerConfigProviderTests` pin both sets. In `tools/career_design/_rules.json`, give the type a `vu_unit` (the generator refuses a type without one), list it in `flat_types` if it is a count, in `resource_types` or `upkeep_types` if it is a resource type, and in `class_filtered` if it is class-filtered.
6. Ask where the ENGINE reads the value, not only whether TAOM writes it: a mount's `Character` is null, its charge and speed are read off the horse, and the spotting ratio is the player seeing others (#611, #614)

### Testing a perk (does it do anything?)
1. Open the console with cheats on and run `taom.career_perks`. It lists every passive the hero holds, the effective Damage / Resistance magnitude per hit kind (also TroopDamage / TroopResistance), for the six class-filtered types the value army-wide (a troop with no class), for Infantry, Ranged, Cavalry and Ranged+Cavalry (one `All` entry when all five agree; the three troop counters and `TroopTraining` have no per-hit-kind breakdown), the consumer of each, and the campaign probes: a "Career line" on max hitpoints, party speed, seeing range, party size, morale, wages, inventory, hero healing, daily food change, regulars healing or the clan's daily gold change means the passive reached the engine's number; "no Career line" with a non-zero magnitude means it did not. Rows come from `CareerPerkRows`, the mission block from `CareerPerkMissionProbe`, the text from `CareerPerkReport` and the consumer lines from `CareerPerkConsumerMap`.
2. Run it again inside a battle: the mission block shows the player agent's `SwingSpeedMultiplier`, `MaxSpeedMultiplier`, `DamageMultiplierBonus`, `ThrustOrRangedReadySpeedMultiplier`, `ArmorEncumbrance`, the mount's `MountChargeDamage` / `MountSpeed` / `HealthLimit`, every consumable slot's count, and the live ability buff.
3. Read the TAOM debug log (`taom_debug_*.log`) for `[CareerPerks]`: the report lines above; `agent stats for '<hero>'` once per distinct set of applied values (spawn, buff on, buff off; the dedupe resets at mission end so every battle's spawn logs); `mount stats for rider`; `Ammo +N%` at spawn with the slot's amount/max before and after; `hit amp` / `hit reduction` at DEBUG once per battle for each hero or party leader, hit mask and troop class (`Melee, Blunt` `class Infantry`; no class text for a hero), and set of passives that moved the number, giving that first hit's base, result and terms (a troop with a party leader is keyed by the leader's id even when its only term is an ally buff; victims with neither a hero nor a leader id share one line per troop class, mask and set of terms); at mission end, one INFO `hit summary` block that counts every such hit (see "Per-hit summary lines" below); `RenownGain` / `TroopUpgradeCost` / `HeroHealing` / `SmithingCostReduction` / `InfluenceGain` / `LootGold` when they apply and move the number. Speed, morale, wages and range have no per-tick line by design; the probe reads them.
4. The three troop counters (`TroopFearResistance`, `TroopKnockdownResistance`, `TroopCrushHold`) are not in the list below: they leave a `troop counter summary` line per leader and class at mission end (see "Troop counter summary lines") and their per-class values in `taom.career_perks`, so a counter that fired is visible and one that did not is not mistaken for a working one. A counter whose hook threw leaves the fault line described there. Types that leave NO trace anywhere, by design or by gap: `ShrugOff` (0 shipped pips), `TroopSurvival` (rolled per casualty at battle end, no line), `SpecialResourceGain` / `SpecialResourceUpkeepModifier` / `SpecialResourceUpgradeCostModifier` (their `[SpecRes]` lines log the total, not the passive's share), `MountHealth` (only the mission block's mount `HealthLimit`), `TroopDamage`'s second consumer (raid speed), `StealthBonus` (#614), and eight of the ten E11 types (everything but `InfluenceGain` and `LootGold`; see "Campaign consumers"). A silent log for one of these is not evidence either way.
5. The hit kind follows vanilla's own correction: a bare-hand hit, a hit off the weapon's attach bone (haft), a kick or bash, fall damage and a horse charge count as Blunt whatever the engine's `DamageType` says (`MissionCombatMechanicsHelper.GetAttackCollisionResults:200` sets a local it never writes back), so a blunt-resistance pip covers a trample.

#### Per-hit summary lines

The per-hit DEBUG lines are written once per combination per battle, where a combination is the direction (amp or
reduction), the hero or party-leader id, the troop class (none for a hero), the hit mask and the set of terms that fired. Every hit is still counted in
a numbers-only table, and the end of the mission (`ResetDiagnostics`, from `CareerPerkMissionBehavior`) writes it at
INFO before clearing it. Nothing is written when no passive moved a hit.

**When it is written.** On every way a mission can end, from two engine callbacks. `OnEndMission` runs once
`Mission.EndMission` has: the battle's end, a retreat, a surrender, and the escape menu's "exit to main menu"
(`MBGameManager.EndGame`). `OnRemoveBehavior` runs when the mission state is finalised, and `GameStateManager.CleanStates`
does that without an `EndMission` first: the application shutting down (the engine's `CoreManaged.Finalize` callback
ends in `Game.OnFinalize`; whether closing the window mid-battle reaches it is native and unverified) or a mod that
loads a save mid-battle. In vanilla only the map's escape menu and the main menu open the load screen, so no vanilla
load runs inside a mission; if the window close does not reach the shutdown callback, the `OnRemoveBehavior` write is
defensive and covers no vanilla exit. After a normal end both run, and the second finds the table empty and writes
nothing; after an abort the stat service, a singleton, no longer carries the dead mission's counts into the next one.
Only a crash or a process kill mid-battle leaves each combination's first-hit DEBUG line in the log without the counts
of the hits after it. No snapshot is written during the battle.

**What it keeps and drops.** Per combination it keeps the hit count, the multiplier's minimum, average and maximum, the
summed base and result damage, and the count of non-finite hits. It drops the order and time of the hits, each hit's own
base and result, the individual term magnitudes behind a changing multiplier (only the product's range survives), and
which agent a victim with neither a hero nor a party-leader id was. Three hits of base damage 10, 20 and 70 and three of
10, 40 and 50 under one multiplier give the same first-hit line and the same summary, so this is an aggregate, not the
per-hit record the old lines were.

- Header, one per mission: `[CareerPerks] hit summary for this mission: 2 combination(s), 57 hit(s)`.
- One line per combination, amplification first, then reduction, each sorted by subject, troop class, mask and terms:
  `[CareerPerks] hit amp summary for 'lord1' [Melee, Cut] class Infantry (TroopDamage): hits=50 multiplier min=1.100 avg=1.100 max=1.100 damage 2500.0 -> 2750.0`.
  Fields: the subject (the hero or party-leader id; `'(ally-buffed agents)'` for victims with neither, whose only
  term is then an ally buff, since their agent index recycles), the hit mask, the troop class (`class Infantry`; omitted for a hero), the terms that fired, `hits` (every
  hit of the combination, the first included), the multiplier the terms applied (min, average and max over the
  hits), and the summed damage before and after. A hit whose multiplier, base or result is not finite is counted,
  adds ` nonFinite=N` after the range, and stays out of the range and the damage sums, which cover the finite hits
  (`multiplier n/a` and `damage n/a` when none was finite).
- `CareerAgentStatServiceTests` pins both formats literally, and covers a new term set for the same subject and mask
  (its own first-hit line and summary row) and hits from eight threads at once (every hit counted, the first-hit line
  written once). `CareerPerkMissionTeardownTests` (`RequiresGame`) pins the writes: `OnEndMission`, `OnRemoveBehavior`
  with no `EndMission`, one summary after both, no tally leaking into the next mission after an abort, and a throw from
  the summary contained and reported on either path.

#### Troop counter summary lines

Written by the same `ResetDiagnostics` pass, at INFO, right after the hit summary, and only when a counter took effect. The tally key is the leader id and the troop class of the victim (`None` is a classless troop; heroes are excluded from the counters, so none appears for one).

- Header, one per mission: `[CareerPerks] troop counter summary for this mission: 1 combination(s)`.
- One line per leader and class, sorted by leader then class: `[CareerPerks] troop counter summary for 'lord1' [Infantry]: fear scaled=12 knockdown scaled=3 crush held=1`. A count rises when the counter changed a value: a fear drain that was scaled, a knockdown resistance that was raised, a crush-through that was turned into a hold. A crush roll that did not hold is not counted, and nothing is counted when the service has no logger. Only the crush count is exact: a fear or knockdown count can exceed the effects that landed (a charge can read the resistance twice when the Combat Mechanics charge rule is off, Branch A ignores the raised value, every overlapping race aura offer counts though only the strongest applies, and a drain dropped later still counts).
- Fault line, outside the tally: the first exception in each hook logs once at ERROR, `[CareerPerks] CareerTroopHooks.<hook> failed, input kept: <exception>`, through `ICareerAgentStatService.ReportHookFault` (`<hook>` is `ScaleFearDrain`, `KnockDownResistance` or `ApplyCrushHold`). The set of faulted hooks is not cleared by `ResetDiagnostics`, so it is once per hook for the life of the stat service, a singleton, not once per mission. A counter that is silent in the summary but has a fault line in the log has gone inert; one with neither never took effect.
- `CareerTroopCounterTests` pins the summary and the once-per-hook report; `CareerTroopHooksTests` pins that a throwing service hands the input back.

### Retune the global ability cooldown
1. Edit `Main/_Module/ModuleData/career_system/taom_ability_tuning.xml` `<Global cooldown_seconds="N" />` (must be in `(0, 3600]`)
2. Restart Bannerlord (provider caches via `Reuse.Singleton`; save-load is NOT enough)

### Add a new ability icon
**All 49 careers enabled on 2026-07-07 got icons** (#101 closed by the named effect-icon set). Today 49 of the 70 careers have an icon; the 21 careers added later (the goblin, misty, craig, falathrim, shaghana and abanissa lines, `ranger_of_the_north`, `warden_of_fornost` and `knight_of_arthedain`) have none. To add/replace: drop a 256x256 PNG at `Main/_Module/GUI/SpriteParts/ui_taom_career_system/CareerSystem/Abilities/<career_id>_ability.png`, then run the sprite bake (do NOT hand-edit `TAOMSpriteData.xml`: the generator rewrites it; see [gui-sprite-system.md](./gui-sprite-system.md) "The sprite-bake pipeline"). The icon id is string-built in C# as `CareerSystem\Abilities\{ability_template_id}` (`CareerScreenVM.cs`); the `sprite=` attribute on `<AbilityTemplate>` is dead: nothing reads it. House style: full-bleed square "named effect-icon": the ability's effect/emblem as a gritty painterly oil painting with the ability name hand-lettered across the bottom (no soldiers, no circular token framing).

## Effect-Scope Badges in Choice Tree (UX)

`CareerChoiceObjectVM.EffectScopeBadge` renders a small "While active" text as the `@EffectScopeBadge` line of a keystone diamond's tooltip. The prefab gates the badge widget on `@IsKeystone` so passives render with no badge (the always-active default is the convention). The distinction matches the runtime for a choice's two kinds of effect: a choice's `PassiveEffect`s flow through `CareerPassiveService.GetPassiveMagnitude` and are read by GameModel overrides on every relevant calculation (always active, whatever the choice's type, so a keystone's passives are always on too); a Keystone's `Mutation`s are applied to a cloned `AbilityTemplateData` by `AbilityEffectExecutor.Execute` (`MutateTemplate`) on V-press only; the clone is discarded once the buff window expires. Open decision: the badge is keyed on the choice type, so it says "While active" for all keystones, including the 60 shipped ones that also carry a passive (for example `black_uruk_captain_t1_a_key`, a Radius mutation plus a Melee `Resistance` passive). Limiting the badge to keystones that have mutations, or rewording it, is the maintainer's call; no code changed for it.

**Active Effects panel (#388).** The right-hand column lists the descriptions of taken keystones and the passives of the career's root choice and of every taken choice, keystones included, summed per effect type and troop class (`CareerScreenVM.RebuildChoiceGroups` collects them, `CareerEffectDisplayMap.Format` prints them). The passive totals use the rule `CareerPassiveService.RefreshCache` applies: each of the hero's stored choice ids that the registry resolves is added once, then the root choice when the stored list does not hold it (the lifecycle service and the creation handler do store it, so it would otherwise count twice). The root sits in no group, so it is read through `GetChoice(career.RootChoiceId)`. A stored choice that sits in no group of the career is applied by the runtime (the repair pass keeps an id whose owner it cannot prove), so it is listed here too. A keystone's own description line stays as authored and still comes from the group walk; its passives, if any, join the passive totals. Known gap:
- It ignores `attack_type_mask`: `CareerScreenVM` keys its lines on effect type and troop class only, so a Melee pip and a Ranged pip of one type sum into one line. This is live for shipped hero pips: 258 `Damage` pips (142 Melee, 116 Ranged) and 30 masked `Resistance` pips. No shipped `TroopDamage` or `TroopResistance` pip carries a mask, and `CareerChoicesIntegrationTests` fails if one does. The always-held root is part of this gap now that it joins the panel: 46 of the 70 shipped roots carry a mask (22 Melee `Damage`, 23 Ranged `Damage`, 1 Melee `Resistance`), so the header reads `+5% melee damage.` while the panel reads `+5% damage`, and on `watchman_of_stangard` (a Melee `Resistance` root, Blunt and Ranged `Resistance` pips) and `avanc_luth_raider` (a Melee `Damage` root, a Ranged `Damage` pip) the root sums with a differently masked pip into one number. A follow-up issue is to be filed and must list the root case. A code fix (key the lines on the mask and print a localized mask phrase) needs new `{=KEY}` strings and the translation run.

**Root perk line.** The header bar shows the root choice's text under the career description, centred in the passive cream brush (`CareerSystem.Effects.Passive`, the colour the brush file gives always-on lines; the keystone gold is reserved for lines that change the ability), as the career's identity perk (`CareerScreenVM.RootPerkDescription`, gated on `HasRootPerk`, which is false when the root choice is missing from the registry or has no description). The text is the root choice's own description: `{=taom_cf_<root id>}` for a generated career (3 roots today), an older key for the other 67. It carries no label of its own, because a label would be a new localization key; adding one ("Career Perk") is a follow-up that needs the translation run.

This is the first time a root description reaches the player, and the old lore-style texts were never written for display. Three promise more than the root's single passive gives: `warden_of_fornost_root` ("Career Ability grants +5% melee damage and improves troop morale near rivers and coasts.", passive: Melee `Damage` 0.05 only; "Career Ability" is also a placeholder for the ability named "Hold the Line"), `knight_of_arthedain_root` ("Stampede grants +5% melee damage and scales with the force of a mounted charge.", passive: a flat Melee `Damage` 0.05) and `watchman_of_stangard_root` ("+5% resistance", passive: `Resistance` for Melee hits only). Until the #769 content step regenerates a career's root, its old text shows as written. Correcting these three is a data edit in `taom_career_choices.xml` plus the English registry rows, and an English change stales all 12 translations (`/localize`; the paid translation runs only on the maintainer's word), so it is a content follow-up, not part of the screen change.

## Career-Switch Picker (dialogue → switch-mode screen)

When the player picks "I wish to discuss my career path" on any companion (under vanilla's `hero_main_options` dialogue token), the dialogue gate uses `ICareerRegistry.GetEligibleSwitchTargets(currentCareerId, hero)` to decide whether to show the option. The consequence opens `GauntletCareerScreen` in switch mode (via `CareerScreenGameState.IsSwitchMode = true`). The same screen renders normal-mode (perk tree) or switch-mode (picker) based on the flag; gates in `CareerScreen.xml` use `@IsNormalMode` and `@IsSwitchMode` to swap the middle area.

**Lifecycle gotcha (Codex Review #46):** Vanilla `GameStateManager.CreateState<T>()` invokes the screen ctor synchronously via `HandleCreateState → OnCreateState → CreateScreen → Activator.CreateInstance(type, state)` BEFORE `OpenCareerScreen` can set `state.IsSwitchMode = true`. Any new feature flag plumbed through `CareerScreenGameState` MUST be read in `OnInitialize`, not in the screen ctor. See [`GauntletCareerScreen.cs:OnInitialize`](../../Main/Features/CareerSystem/UI/GauntletCareerScreen.cs).

**Empty-state UX.** The picker's `ScrollablePanel` is gated on `@IsBrowsingTargets` (= `_isSwitchMode && targets.Count > 0`); the empty-state `TextWidget` bound to `@NoTargetsMessage` is gated on `@HasNoSwitchTargets` (= `_isSwitchMode && targets.Count == 0`). The dialogue gate prevents the empty-list state being reached via normal flow, but a static-entry-point or `_heroAdapter == null` reaches it; `RebuildEligibleSwitchTargets` logs a warning so a player report of "blank picker" is triagable. `IsBrowsingTargets` and `HasNoSwitchTargets` are computed expression-body properties; the VM fires `OnPropertyChanged` for both after `_eligibleSwitchTargets.Clear() + Add()` mutations (Gauntlet does not re-evaluate computed bools on collection mutation).

**Switch contract:** `CareerSwitchService.SwitchCareer(heroStringId, hero, newCareerId)` returns a `CareerSwitchResult` (`Switched`, `NotEligible`, `NotEnoughGold`). It checks `CanSwitch` first (`CanSwitch` rejects same-career switches at the boundary: `hero.StringId` to `_dataService.GetCareerStringId`, ordinal-ignore-case against `newCareerStringId`), then charges `GetSwitchCost` (the retrain price only when the old career holds a fork pick, otherwise free) before anything changes, so a short purse leaves the old career untouched. Then it calls `ClearCareer` (old career, choices, tier unlocks, flags) and `CareerCreationHandler.OnCareerSelected`, which sets the new career, stamps its tree version, seeds the root choice and refreshes the passive cache. `GauntletCareerScreen` asks for confirmation when the cost is above zero, and that confirmation's Confirm button is disabled while the purse is short (`CanAfford`), as the retrain one is. `ICareerSwitchService` no longer exists.

## In-game check lines (#971)

The career button's click writes `[Check] id=taom.career-screen.open` from the Patch27 postfix: `result=pass source=button` when `ScreenManager.TopScreen` is the career screen after `OpenCareerScreen` returns, `result=fail source=button reason=not-on-top` when it is not (no main hero, or no screen built), and `reason=<exception type>` when the close or the open throws, after which the exception is rethrown as before. The end of `GauntletCareerScreen.OnInitialize` writes `[Check] id=taom.career-screen.init result=pass mode=normal` (or `mode=switch` for the dialogue's picker). Both go through the `Outcomes` facade, and the ids are consts in `CareerScreenChecks.cs`. `python tools/ingame_verdict.py --issue 971` reads them with `tools/ingame_checks/971.toml`, which counts only `mode=normal`.

## Forks, point schedule, retrain and tree versions

Added by #769 (engine steps E4 to E7 and E12). Trees of forks are written by [the career forks generator](career-forks-generator.md). **Live on Gondor's three careers (#769 C2):** `captain_of_osgiliath`, `ranger_of_ithilien` and `knight_of_belfalas` ship at `tree_version` 2 with `max_picks` on every group and `point_levels` 1, 5, 10, 15, 20, 25. Every other career stays at version 1 and carries none of the three attributes, so every path below falls back to the old behaviour for it. The one visible change on existing trees is the lock-reason line in the tooltip of a dimmed diamond.

**One rule.** `CareerChoiceRules.CanTake` and `CanRefund` return a `ChoiceLockReason`; the click and the diamond display state both ask them and act only on `None`. `CanTake` returns the first failing check, in this order: `NotInCareer` (a null input), `AlreadyTaken`, `NotInCareer` (the choice's group is not one of the career's), `TierLocked`, `ForkFull` (a fork group already holds `max_picks`), `KeystoneLocked` (non-fork groups only), `NoPoints`. `CanRefund` returns `NotTaken`, `NotInCareer` (the root is never refundable) or `LockedIn`.

The refusal reasons and the tooltip line each one gives the diamond (`CareerChoiceObjectVM`; `AlreadyTaken`, `NotTaken` and `NotInCareer` show nothing):

| Reason | Line shown |
|---|---|
| `TierLocked` | "Requires Level N" |
| `ForkFull` | "You took another choice on this path." (also turns the diamond dark red) |
| `KeystoneLocked` | "You took another keystone in this tier." |
| `NoPoints` | "No free career points." |
| `LockedIn` | "Locked in. Retrain to change this choice." |

**`max_picks` on `<ChoiceGroup>`.** 0 or absent is unlimited: the old behaviour, free refunds, and the keystone rule still applies there only. A positive value makes the group a fork that holds at most that many picks, and the keystone rule is skipped for it. A career's groups are all forks or all unlimited, because one career holding both would have two refund rules in one tree; `CareerChoiceIntegrityTests.NoCareer_MixesForkGroupsWithUnlimitedGroups` gates it.

**Lock on leave.** A fork pick refunds freely only during the screen visit it was taken in (`CareerScreenVM._takenThisVisit`); once the screen is closed it reads `LockedIn` ("Locked in. Retrain to change this choice."). A non-fork pick is refundable at any time.

**Point schedule.** `point_levels="5,10,15"` on `<Career>` grants one point at each listed level (strictly ascending positive integers, otherwise the attribute is dropped with a warning), plus the free root. Absent keeps one point per level, capped by `max_perk_points`. `GetMaxChoicesForHero(career, level)` and `GetUnspentPoints(career, level, taken)` take the career, and the screen shows "Next point at level N" from `GetNextPointLevel` (null for a legacy career or once every level is reached).

**Retrain.** A paid reset in `ICareerLifecycleService.TryRetrain`: it charges `GetRetrainCost(level)` = `retrain_gold_base` + `retrain_gold_per_level` x level (a level below 1 prices as 1; computed in `long` and clamped to `int.MaxValue`), then refunds every non-root choice. It keeps the career, tier unlocks and flags. Results: `Done`, `NoCareer`, `NothingToRetrain` (no fork pick held), `NotEnoughGold`; a short purse changes nothing. The screen shows the Retrain button only for a career with a fork group, enables it when a locked-in pick exists, and the confirmation's Confirm button is disabled while the purse is short.

**Switch cost.** `GetSwitchCost` charges the same retrain price when the old career holds a fork pick, and nothing otherwise. See the switch contract above.

**Tree version.** `tree_version="N"` on `<Career>` (default 1, minimum 1) marks a redesigned tree. A hero carries the `career_tree_vN` flag; no flag means version 1. `CareerTreeVersion.Stamp` is the only writer: it drops every version flag the hero holds first, higher ones included (the reader takes the highest), then writes the flag when the career is version 2 or above. `CareerCreationHandler.OnCareerSelected` stamps on assignment, so a new hero is not refunded on first load. On session launch, after the foreign-choice repair and before the cache refresh, `MigrateAllTreeVersions` refunds each hero's choices once when that hero's career version is above the stored one, then stamps. It covers every hero that holds career data, not only the main hero: a co-op hero, or a hero the player takes over later, is never the main hero at launch, and nothing else migrates it. Each migration needs proof the tree loaded (the career lists at least one group and every group resolves with at least one choice); without it nothing changes and no flag is set. Only the player sees a message, and only when the refund removed something.

**Trap: when a shipped career tree changes, its `tree_version` must rise.** Saves migrate (refund to the root, new flag) only when the stored version is below the career's. `MigrateTreeVersion` returns at once for a career at version 1 and for a hero whose stored version is already at or above the career's, so editing the groups or choices of a shipped tree without raising that career's `tree_version` leaves every existing save holding picks the new tree may not contain: nothing refunds them and nothing tells the player. A raised version refunds every non-root choice once (tier unlocks and flags stay), stamps the new `career_tree_vN` flag and, for the player only, shows one message. A hero stored above the career's version (a lowered `tree_version`) is never touched, so lowering undoes nothing. The one gate is `CareerChoiceIntegrityTests.EveryCareerWithAForkGroupOrPointLevels_IsTreeVersionTwoOrHigher`, which catches a fork or paced career left at version 1; no test notices a plain edit of an existing tree, so the bump falls to whoever changes the XML. The generator writes `tree_version` itself.

**Not here any more.** The +/- group buttons, the hover commands and `ButtonsVisible` are gone, and so is `ICareerSwitchService` (callers use `CareerSwitchService` directly).

**In-game check: the Gondor pilot (#769 C2).** One action and the evidence it leaves per step. Use a new Gondor campaign as a captain (`captain_of_osgiliath`) unless a step says otherwise, and read `taom.career_perks` for the held values. A fork holds one pick, so steps 11 and 12 each Retrain first.

1. Start the campaign: the screen shows 1 point at level 1. Take F1 A; F1 B dims and its tooltip reads "You took another choice on this path."
2. Close and reopen the screen: F1 A reads "Locked in. Retrain to change this choice." Press Retrain: the confirmation shows its cost, the gold drops by that cost, and the pick is refunded.
3. Console to level 10 and take F3 A: the party size limit rises by 50 at once, on the party tooltip, with no restart.
4. Take F2 B while serving Gondor: the special resource breakdown (Castar) shows the upkeep line at 0, and there is no desertion warning at a zero balance.
5. Fight a Nazgul with F4 A held: `taom.career_perks` lists `TroopFearResistance` at 60%, and the troops' morale drops less than without the pick.
6. Console to level 15, take F1 B (Word of the Captain). `taom.career_perks` shows `TroopDamage` at 8% for Infantry and 0 for Ranged and Cavalry; after a mixed-army battle the hit summary has class Infantry (`TroopDamage`) lines.
7. Load an old Gondor save made before this build: one message reports the refund, and the unspent points equal the level's schedule (`point_levels` 1, 5, 10, 15, 20, 25).
8. Load an old Rohan save: no message, no refund, the old tree and old points.
9. Give gold by console (a switch at level 10 costs 3,500), talk to a companion (the switch opens from a companion's dialogue) and switch career: the retrain cost is charged.
10. Join a kingdom, take F2 A and win a battle: the log has `[CareerPerks] InfluenceGain +25% for 'hero': a -> b` once. Leave the kingdom and win again: no influence line, because vanilla pays no influence outside a kingdom.
11. Retrain, take F4 B and fight with shielded infantry against a heavy-hitting foe: after the battle the `troop counter summary` line for the leader and class shows a non-zero crush held count.
12. Retrain, take F4 C: judge it by troops staying on their feet against blows and light charges, not by the counter summary. A full-speed heavy charge still knocks a man down (Combat Mechanics Branch A ignores resistance by design), and that is not a failure.
13. At level 20 take F5 A (Veterans of the Citadel for the captain): `taom.career_perks` shows `TroopTraining` 15 for Infantry only, and an infantry stack gains 15 experience per troop on the next daily tick while a ranged or cavalry stack gains none from it. The evidence for each type is listed under "In-game checks, with the evidence each leaves".
14. Open the career screen as the captain: the header bar shows the root perk line (`+5% melee damage.`, the root's own text) under the career description, and the Active Effects column lists `+5% damage` before any pick (the panel drops the melee and ranged mask: the known gap above). Take F1 A (`+15% melee damage`): that line reads `+20% damage`, once, not a second line. The captain's only keystone (F6 C) carries no passive, so check a keystone passive on a hero holding `black_uruk_captain`: take `black_uruk_captain_t1_a_key` (Iron Hide), whose description stays a gold keystone line while `+6% resistance` joins the passive lines beside the root's `+6 max health`. Judge the header bar too: the root line is centred in the cream brush like the description above it (check that alignment and that the colour reads as a separate line; the gold `CareerSystem.Effects.Keystone` brush is the alternative), and the fixed 185 px bar must not clip it. A text-metrics estimate from a review puts the tallest left column (a two-line root in German, Brazilian Portuguese or Russian) at about 120 px of the 185 px bar; Chinese, Japanese and Korean were not measured, so glance at one of them. **Owed:** the maintainer's in-game look, since the line and the sums are covered by unit tests and a prefab binding test but never rendered.

## Campaign lifecycle: when a career may be assigned, and the repair pass

Three rules hold this feature's state together. Breaking any one of them shows up to the player as
"levelling grants no career points", because points are derived (`GetMaxChoicesForHero(career, level) -
choicesTaken`) rather than stored, so a `choicesTaken` that is too high silently zeroes them.

**1. The legacy-career fallback belongs to loaded saves only.** `CareerCampaignBehavior` subscribes
`OnGameLoadedEvent` for it, never `OnSessionLaunchedEvent`. A `!HasCareer(...)` test cannot tell a new
campaign from an old save: `OnSessionLaunched` fires long before character creation, so on a new
campaign the test is always true. v1.4.8 `Campaign.DoLoadingForGameType` raises `OnSessionStart` at
line 1695 and CC is only pushed afterwards by `SandBoxGameManager.OnLoadFinished`, with
`Hero.MainHero` still on the vanilla `main_hero` template (culture `battania`, name Eren). Until
2026-09-02 this granted every new player `blademaster_of_ren` and its root choice about a minute
before they chose their own career.

**2. The career book resets on `OnNewGameCreatedEvent`.** `CareerPersistenceBehavior` wires
`ICareerDataService.ResetForNewCampaign`. The service is `Reuse.Singleton` and the container is built
once per process (`SubModule.OnSubModuleLoad`), while the player's dictionary key is the engine
constant `main_hero` (`CampaignData.MainHeroTag`) in every campaign. `SyncData` restores only on a
loading store and a brand-new campaign never syncs at all, so without the reset campaign B opens
holding campaign A's choices, `TryAddChoice` then refuses B's own root outright, and the player gets
zero points for as many levels as A had choices. `OnNewGameCreatedEvent` is safe to subscribe from
`RegisterEvents`: `Campaign.cs` calls `OnNewGameCreatedInternal` at 1583, that method ends with
`CampaignBehaviorManager.RegisterEvents()` at 1624, and the dispatcher fires afterwards at 1585.

**3. `OnSessionLaunched` repairs what earlier sessions wrote.**
`ICareerLifecycleService.RepairForeignChoices` drops stored choices that positively belong to a
different career, before `RefreshCache` runs, so saves already carrying a ghost heal on the next load
instead of needing the switch-career workaround. It is idempotent, so a healthy save is a no-op
forever after.

**The polarity is the safety property, not the logic.** This pass deletes save data, so it deletes
only on proof of foreignness, via `ICareerRegistry.GetOwningCareerId`. An owner that cannot be
resolved means keep. The inverse (an allow-list built from the career's own groups, dropping
everything outside it) reads identically on a healthy install and destroys the player's whole tree on
a broken one, because `CareerConfigProvider.EnsureLoaded` loads `taom_careers.xml` and
`taom_career_choices.xml` under separate try/catch blocks: a malformed choices file leaves every
career resolvable and every group empty, collapsing the allow-list to the root choice. If you ever
rewrite this pass, keep the polarity.

**Group choices resolve through their `<ChoiceGroup>` (#766).** Membership is the nesting only: the provider stamps the enclosing group id on each nested choice and never reads a `group_id` attribute (a non-empty one is logged and ignored). Until 2026-10-08 nested choices parsed with an empty `GroupId`, so `GetOwningCareerId` could not resolve any group choice and the repair pass kept every foreign group pick. The pass now relies on each group id, root choice id and choice id having exactly one owner. `CareerRegistry` gives an id with two claimants no owner and logs one warning at load: a group listed by two different careers (the same career listing it twice is fine), a `root_choice_id` used by two careers, and a choice id parsed more than once, checked before the root lookup. An ambiguous id is kept, never deleted, because the pass deletes only on proof. `CareerChoiceIntegrityTests.ShippedData_EveryOwnershipIdHasExactlyOneOwner` keeps the shipped XML free of such ids.

**Root choices resolve from `taom_careers.xml`, not from a group.** The generated roots (Gondor's
three) carry no `group_id` attribute and every older root carries `group_id=""`; the loader reads
neither, so a root always parses with `""`, and `GetOwningCareerId` indexes `CareerDefinition.RootChoiceId` directly. Without
that, a ghost root reads as ownerless, survives the repair, and the career-point bug is not fixed.
`CareerRegistryTests.GetOwningCareerId_RootChoiceWithEmptyGroupId_ResolvesToItsCareer` pins it against
a real registry rather than a substitute, because a stubbed registry hid exactly this during review.

**Accepted keystone trade (old trees only).** Completing a tier-3 group opens every keystone, and keystones taken that way stay taken after the group is later refunded, because `KeystoneExclusivityRule` never locks an already-taken choice (grandfathering). The forks redesign removes the one-keystone-per-tier rule, so this is not being engineered around.

**Known limitation:** `TierUnlocks` and `Flags` are stored per hero with no career dimension
(`IsTierUnlocked(heroId, tier)`), and only `ClearCareer` clears them (`CareerTreeVersion.Stamp` replaces the `career_tree_v*` version flags but touches no other flag). A tier unlocked by one career's
quest survives a reassignment through any path that does not clear, so a later career can read tier 2
or 3 as unlocked ahead of its level gate. Pre-existing; the repair pass covers `ChoiceIds` only.

The reason a stale entry is not merely cosmetic: `CareerPassiveService.RefreshCache` walks
`heroData.ChoiceIds` and resolves each id through the registry without checking it belongs to the
hero's career, so a foreign choice keeps applying its passive. A Gondor Ranger carrying a Khand
Blademaster's melee damage bonus was the second symptom of the same defect.

**`SetCareer` does not clear choices** (only `ClearCareer` does; its two callers are `CareerSwitchService`
and `CareerCampaignBehavior.OnHeroKilled`, for a dead non-player hero). Any new code path that assigns a career to a hero who may already have one must decide
explicitly whether the old choices should survive.

## Changelog
- 2026-10-30 (merge of the career forks into trunk): the engine has one PartyTrainingModel slot, so `TroopTraining` is applied by the Seasoned Hosts `TaomPartyTrainingModel` (the Seasoned Hosts factor first, then the career flat term, since `ApplyFlat` divides out only the factors already on the number); the separate career training model is gone. The TroopSurvival term moved into `SurvivalChanceLine` with the bounded `ScaleByPassive`. The 111 old Gondor keys were retired.
- 2026-10-27 ([ered-luin.md](ered-luin.md)): Ered Luin's three careers, cloned by `tools/insert_new_faction_careers.py` from Erebor's foot careers: Warden of Thorin's Halls (`warden_of_thorins_halls`, Infantry) and Mattock Hewer of Belegost (`hewer_of_belegost`, TwoHanded; first called Broadbeam Shield-guard, `shieldguard_of_belegost`, and replaced on 2026-10-28, #1045), both from `ironguard`, and Firebeard Arbalester (`nogrod_arbalester`, Ranged, from `crossbow_master`). `ram_rider` is not cloned, because dwarves do not ride. The tool can now clone one source more than once. The two foot careers share their numbers but not their starting kit.
- 2026-10-26 (#1005): the `CharacterDeveloper.SkillNameText`/`.DescriptionText` brush cleanup (RCA finding #3) is done.
- 2026-10-14 (#769, engine steps E4-E7 + E12): Career forks. One take/refund rule (`CareerChoiceRules`) with a `ChoiceLockReason` shown as a tooltip line; `max_picks` makes a fork with lock on leave; `point_levels` and "Next point at level N"; paid retrain and a gold charge for leaving a career that holds a fork pick (the switch now goes through `CareerCreationHandler.OnCareerSelected`); `tree_version` migration on session launch, stamped only by `CareerTreeVersion.Stamp`. `ICareerSwitchService`, the +/- group commands and `ButtonsVisible` are deleted. Inert until a career ships the attributes.
- 2026-10-15 (#769, root and keystone passives on the screen): The career screen shows the root perk. The Active Effects panel now sums the passives of the root choice and of every stored choice, keystones included, by the rule `CareerPassiveService.RefreshCache` applies (each stored id once, the root when it is not stored; a keystone's description line is unchanged), and the header bar shows the root choice's text as the career's identity perk (`RootPerkDescription`, `HasRootPerk`). Three stale lines that called `MountHealth` and `InventoryCapacity` flat or blank were corrected: `CareerPerkConsumerMap.IsFlat` treats both as fractions. The in-game look is owed (step 14 of the Gondor check).
- 2026-10-15 (#769, content step C2; [RCA](../reviews/rca-career-gondor-pilot-2026-10-15.md)): The Gondor pilot. `captain_of_osgiliath`, `ranger_of_ithilien` and `knight_of_belfalas` ship a generated tree from `tools/career_design/gondor.json`: a root plus six forks (14 choices), `tree_version` 2, `point_levels` 1, 5, 10, 15, 20, 25, so a Gondor hero is refunded once on the first load. First shipped pips for `TroopFearResistance`, `TroopKnockdownResistance`, `TroopCrushHold`, `InfluenceGain`, `RecruitCost`, `TroopTraining` and `SpecialResourceUpkeepModifier` (3 each), and for `troop_class` (9). The new English is registered and seeded into the 12 languages unchanged until C4; the old Gondor keys stay until C5 (see [the generator page](career-forks-generator.md)). The in-game check is the numbered list under "Forks, point schedule, retrain and tree versions".
- 2026-10-15 (#769, engine steps E11 + E11b): Ten campaign passives, each read by a GameModel override: `InfluenceGain`, `RecruitCost`, `PrisonerConversion`, `FoodConsumption`, `WoundRecovery`, `DailyGold`, `LootGold`, `RaidLoot`, `TroopTraining` and `TradePrices`. Two new models, `TaomPartyTrainingModel` and `TaomPartyTradeModel`; seven existing models change (the battle reward, wage, prisoner recruitment, food, clan finance, raid and healing models). The service gains `ScaleByPassive`, a troop class argument on `ApplyFlat` and `ApplyWoundRecovery`, and its event line is skipped when the number did not move; `TroopSurvival` now goes through `ScaleByPassive` too. `TroopTraining` joins the class-filtered set. Inert until a choice ships the types.
- 2026-10-14 (#769, doc pass after two code reviews): The page now covers the whole branch as built. New: the attribute reference table, the lock reason table, the class mapper table, the Active Effects panel gaps (keystone and root passives absent; `MountHealth` and `InventoryCapacity` print nothing), the troop counter summary and fault lines, the tree version trap and a link to the generator. Corrected: the troop counters take the `ICareerAgentStatService` the caller holds, effect labels are localized keys, the hit summary line carries the troop class, the `PassiveEffectType` how-to, the pip counts, the career count and the test table counts. Dropped the deleted `CareerPassiveHelper` and `AbilityHudController` from the architecture text and key files.
- 2026-10-14 (#769, engine step E10 + E13; [RCA](../reviews/rca-career-e8e10-2026-10-14.md)): Three class-filtered counters, `TroopFearResistance`, `TroopKnockdownResistance` and `TroopCrushHold`, read through `CareerTroopHooks` at the four fear drains, `GetKnockDownResistance` and `DecideCrushedThrough`. `taom.career_perks` shows their per-class values. Campaign only (the Custom Battle twins are unchanged), fail open, inert until a choice ships the types.
- 2026-10-14 (#769, engine steps E8 + E9; [RCA](../reviews/rca-career-e8e10-2026-10-14.md)): A choice carries up to two passives (`CareerChoiceDefinition.Passives`, `CareerConfigProvider.MaxPassivesPerChoice`; the singular `Passive` is deleted), and `TroopDamage` / `TroopResistance` take an optional `troop_class` (Infantry, Ranged, Cavalry) plus the `attack_type_mask` they previously ignored, read per blow through the new `ICareerPassiveService.GetTroopMagnitude`. The damage boundary resolves the struck or striking troop's class from its default formation (`TroopClassMapper`). An unparseable `troop_class` drops its passive. Inert until a choice ships the attributes: no shipped pip uses a second effect or `troop_class`, and a test pins that every shipped choice parses to the same single passive.
- 2026-10-14 (#769, engine steps E4-E7 + E12; [RCA](../reviews/rca-career-e4e7-2026-10-14.md)): Career forks. One take/refund rule (`CareerChoiceRules`) with a `ChoiceLockReason` shown as a tooltip line; `max_picks` makes a fork with lock on leave; `point_levels` and "Next point at level N"; paid retrain and a gold charge for leaving a career that holds a fork pick (the switch now goes through `CareerCreationHandler.OnCareerSelected`); `tree_version` migration on session launch, stamped only by `CareerTreeVersion.Stamp`. `ICareerSwitchService`, the +/- group commands and `ButtonsVisible` are deleted. Inert until a career ships the attributes.
- 2026-10-08 (#768): The PartySize passive reached every party the owner's clan touched (owner-first), and a pick stayed invisible until an unrelated roster event because `PartyBase.PartySizeLimit` caches on `MemberRoster.VersionNo`. `TaomPartySizeModel` now keys the passive on `party.LeaderHero` only, `CareerPassiveService.RefreshCache` calls `IPartySizeCacheInvalidator.InvalidateLedBy` with the union of the previous and new cache's hero ids (plus their army leader parties), and the call moved after the last `AddFactor` so the AI lord multiplier no longer amplifies the flat count. The Rohan Stangard tier 3 pip reads "Your company swells with volunteers" because it no longer reaches a garrison. `taom.career_perks` also prints the engine-cached limit.
- 2026-10-08 (#766; [RCA](../reviews/rca-career-e1e3-2026-10-08.md)): Group choices parsed with an empty `GroupId`, so the tier click gate, keystone exclusivity (#381), the dimming and the foreign-choice repair were all inert for them. `CareerConfigProvider` now takes membership from the enclosing `<ChoiceGroup>` only (a `group_id` attribute is ignored with a warning). `CareerRegistry` gives ambiguous ids (group listed by two careers, shared root, duplicated choice id) no owner and warns once per id; the repair pass keeps them. A shipped-data test pins single ownership, and the player sees one message when a repair refunds choices (`taom_career_repair_refund`).
- 2026-09-13 (#595): `CareerAbilityBuffTracker` takes a lock (its readers sit behind `TaomAgentStatCalculateModel`,
  reached through `Agent.UpdateAgentProperties` from a native callback of unknown thread). The AoE ally-buff
  restore closure now checks `AgentSlotIdentity.IsCurrentOccupant(ally)` before subtracting by the captured
  index: a dead ally's index can belong to a newly buffed agent by the time the restore fires.
  `CareerPerkMissionBehavior.OnAgentBuild` catches, because it runs inside `Mission.SpawnAgent`'s unguarded loop.
- 2026-09-02: Career points never appeared on a new campaign: the legacy-save fallback ran before character creation and granted a placeholder-culture career whose root choice then ate the level-1 point permanently. Fallback moved to `OnGameLoadedEvent`, `ICareerDataService.ResetForNewCampaign` wired to `OnNewGameCreatedEvent`, and a `RepairForeignChoices` repair pass added at session launch for saves already carrying a ghost.

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
- [docs/features/dev-console.md](./dev-console.md)
- [docs/features/starting-equipment-tuning.md](./starting-equipment-tuning.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-balance.md](../modding/configs-balance.md)
- [docs/modding/equipment-rosters.md](../modding/equipment-rosters.md)
- [docs/modding/load-order-and-dependencies.md](../modding/load-order-and-dependencies.md)
- [docs/reference/engine/mount-and-rider-runtime.md](../reference/engine/mount-and-rider-runtime.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/research/karpathy-autoresearch.md](../research/karpathy-autoresearch.md)
- [docs/reviews/rca-career-phantom-passives-2026-06-26.md](../reviews/rca-career-phantom-passives-2026-06-26.md)
- [docs/reviews/rca-career-starting-equipment-2026-05-19.md](../reviews/rca-career-starting-equipment-2026-05-19.md)

<!-- backlinks-end -->
