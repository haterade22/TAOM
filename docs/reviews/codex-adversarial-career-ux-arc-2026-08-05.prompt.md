ADVERSARIAL REVIEW: TAOM Career UX arc (issues #377-#384), all uncommitted on branch bannerlord-1.4.5. Target: Bannerlord v1.4.7 (installed). Your job: find real bugs before they ship. Confirm or dispute every Known Suspect with evidence (paste code blocks from BOTH codebases). Adopted from an external reference module (TAOM-Career-UX-Upstream-2026-08-05); the module itself was NOT ported -- all code below is TAOM-authored.

WHAT WAS BUILT (8 issues):
#377 ability runtime: CareerAbilityBuffTracker gained contribution-counted Add/RemoveContribution + entry retirement (GetBuff non-null now means "buff window live"); CareerAbility gained BeginActiveWindow/ActiveDuration/ActiveRemaining/IsActive/ActiveProgress01; AbilityActivationController.Tick gained isControllingCareerHero gate; AbilityEffectExecutor calls BeginActiveWindow with the same mutated duration that schedules restores.
#378 career button: Id="TaomCareerButton", Brush="CareerSystem.CareerButton" (new Brushes/CareerSystem.xml), click sound via UISoundsHelper.PlayUISound in CharacterDeveloperCareerMixin.ExecuteOpenCareerScreen. CareerScreen.xml +/- buttons switched from bare Sprite= to new brushes.
#379 unspent-points badge: ICareerRegistry.GetUnspentPoints(level, taken); CharacterDeveloperCareerMixin exposes HasUnspentPoints/UnspentPointsText; badge widget nested in CareerButtonPrefab.cs inline XML.
#380 keystone glyphs: keystone_icon attr on all 50 Career rows in taom_careers.xml (banner-icon ids, doubling as bare-number sprite names); parsed to CareerDefinition.KeystoneIcon; CareerChoiceObjectVM.KeystoneIconSprite/HasKeystoneIcon; medallion widget in CareerScreen.xml keystone badge blocks (3 tier copies). NO culture fallback by design; missing attr logs a warning.
#381 keystone exclusivity: extracted to static KeystoneExclusivityRule.IsLocked(career, choice, registry, heroData) -- one keystone per tier, EXEMPT when any tier-3 group is fully taken, already-taken choices never locked (grandfathered). Three consumers: CareerScreenVM.ExecuteSelectChoice, TrySelectChoice, and RebuildChoiceGroups display gating (isFreeToTake).
#382 energy bar: old AbilityHUD square panel RETIRED (AbilityHudController/IAbilityHudController/CareerAbilityHudVM/AbilityHUD.xml deleted; CareerPerkMissionBehavior no longer takes a hud controller). New: CareerEnergyBarPrefab = UIExtenderEx PrefabExtensionInsertPatch("AgentStatus", "descendant::Widget[@VisualDefinition='AgentStatus']/Children") InsertType.Child, inserting a bar built from native ShieldHealthBar brushes; MissionAgentStatusCareerMixin = [ViewModelMixin("Tick")] on TaleWorlds.MountAndBlade.ViewModelCollection.MissionAgentStatusVM exposing IsCareerBarVisible/IsBarReady/IsBarActive/IsBarCooldown/BarFillWidth/ActivationKeyText/CareerGlyphSprite/HasCareerGlyph; pure CareerEnergyBarStateMapper maps ability state to bar state with a refill rescale (raw - d)/(1 - d) where d = ActiveDuration/CooldownDuration.
#383 damage attribution: CareerPerkMissionBehavior.OnScoreHit override prints "{TARGET}: {DMG} damage (+{BONUS} from ability)" while CareerAbilityBuffTracker.GetBuff(heroId) is non-null; share math in static AbilityDamageAttribution (dmg*f/(1+f)); zero-bonus abilities print a once-per-activation notice (_zeroBonusNoticeShown, re-armed on Activated); threshold GlobalTuning.MinReportableBonusDamage (new optional XML attr min_reportable_bonus_damage, default 0.5).
#384 diagnostics: taom.print_hud_layout console command (HudLayoutDumpCheats) dumps top screen widget tree to Logs/taom_hud_layout.log, bounded, via TaomConsole.RunAnywhere.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
docs/features/career-system.md (the 2026-08-05 update section)
Main/_Module/ModuleData/career_system/taom_careers.xml (keystone_icon rows)
Main/_Module/ModuleData/career_system/taom_ability_tuning.xml
Main/_Module/GUI/Brushes/CareerSystem.xml

KNOWN SUSPECTS (confirm or dispute each with evidence):
S1 (CRITICAL if real): The energy bar binds mixin properties (@IsCareerBarVisible etc.) on MissionAgentStatusVM, injected into the AgentStatus prefab. But AgentStatus.xml may be rendered under a DIFFERENT datasource context than the movie root (MissionGauntletAgentStatus loads movie "MainAgentHUD"). Trace the prefab chain: does MainAgentHUD.xml include/nest AgentStatus with a DataSource="{...}" context switch to a child VM? If the AgentStatus subtree binds a child VM, the mixin properties on the parent VM never resolve and the bar is permanently invisible. Decompile MissionGauntletAgentStatus (Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.GauntletUI.dll) and read the actual prefab XMLs under the installed Native/GUI/Prefabs.
S2 (HIGH if real): [ViewModelMixin("Tick")] -- verify UIExtenderEx 2.13.2 supports hooking an arbitrary public method name for OnRefresh (TAOM precedent only used "RefreshValues"). Read the UIExtenderEx source in C:/Users/mikew/.nuget/packages/bannerlord.uiextenderex/2.13.2/ (decompile the DLL) -- what does the string argument actually patch, and does MissionAgentStatusVM.Tick(float dt) match the expected shape (parameterless vs parameterized refresh method)? If the mixin never refreshes, the bar shows stale/default state.
S3 (HIGH if real): PrefabExtension("AgentStatus") -- does UIExtenderEx patch prefab FILES by name as loaded through WidgetFactory (so a non-movie included prefab like AgentStatus is patchable), or only movies loaded via LoadMovie? If AgentStatus is not independently patchable, the insert never happens.
S4: InsertType.Child default Index=0 inserts our bar as the FIRST child of the AgentStatus Widget's Children -- BEFORE BoolStateChangerWidget (which targets "..\." parent-relative). Confirm insertion index semantics and whether widget order affects the BoolStateChangerWidget target resolution or the VisualDefinition state animation.
S5: Clock divergence -- CareerAbility.ActiveRemaining decrements via accumulated mission dt (AbilityActivationController.Tick -> _abilityService.Tick(dt)), while MissionAbilityExecutionContext._pendingRestores expire against Mission.Current.CurrentTime. Under fast-forward sub-ticking or pause, can the HUD IsActive window and the actual buff restore diverge enough to matter (bar says active while buff expired, or vice versa -- which also gates OnScoreHit attribution)?
S6: Overlapping activations -- GlobalTuning.MinCooldownSeconds floor is 5s; ability duration is 8s (one 10s). With CooldownReduction mutations the effective cooldown can drop below the active duration, so a second activation can begin while window 1 is live. Trace: BeginActiveWindow restarts the window (fine), contribution counting accumulates (fine?), _zeroBonusNoticeShown re-arms, and the refill rescale d = ActiveDuration/CooldownDuration when ActiveDuration >= CooldownDuration falls back to raw progress. Any real desync or double-buff bug here? Note ApplyCooldownAdjustment floors at MinCooldownSeconds -- check actual reachable cooldown values vs the 8s/10s durations in taom_ability_templates.xml.
S7: OnScoreHit gates on affectorAgent == Mission.Current?.MainAgent AND affectorAgent.Character == hero.CharacterObject. Are there hit paths where affectorAgent is the hero's MOUNT or a summoned/owned agent that should not attribute? And isSiegeEngineHit is NOT filtered -- should siege-engine hits by the player attribute ability bonus (buff does not apply to siege engines -- DamageMultiplierBonus is an agent stat)? Check vanilla OnScoreHit call sites for what affectorAgent is for siege/ranged/mount collisions.
S8: taom_careers.xml -- the disabled far_harad_halftroll career sits inside an XML comment; the keystone_icon insertion script also edited inside the comment. Parse the file and confirm well-formedness and that exactly 49 ACTIVE careers all carry keystone_icon.

VANILLA CODE (decompile and paste as evidence):
E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll -> MissionBehavior.OnScoreHit signature
Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.GauntletUI.dll -> MissionGauntletAgentStatus (movie load + datasource)
bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.ViewModelCollection.dll -> MissionAgentStatusVM (Tick method, child VMs, properties)
Modules/Native/GUI/Prefabs/Mission/MainAgentHUD.xml + AgentStatus.xml (datasource contexts, Widget[@VisualDefinition='AgentStatus'])
C:/Users/mikew/.nuget/packages/bannerlord.uiextenderex/2.13.2/lib/netstandard2.0/Bannerlord.UIExtenderEx.dll -> ViewModelMixin attribute semantics, PrefabExtensionInsertPatch InsertType/Index semantics, prefab name resolution

TAOM FILES (all uncommitted -- review the working tree):
Runtime: Main/Features/CareerSystem/Abilities/CareerAbilityBuffTracker.cs, CareerAbility.cs, CareerAbilityService.cs, ICareerAbilityService.cs, AbilityActivationController.cs, IAbilityActivationController.cs, AbilityEffectExecutor.cs, MissionAbilityExecutionContext.cs, AbilityDamageAttribution.cs, AbilityInputAdapter.cs, IAbilityInputAdapter.cs
Feature root: CareerPerkMissionBehavior.cs, CareerRegistry.cs, ICareerRegistry.cs, CareerConfigProvider.cs, CareerCampaignBehavior.cs, CareerSystemIoC.cs, KeystoneExclusivityRule.cs, Domain/CareerDefinition.cs, Domain/AbilityTuningConfig.cs
UI: UI/CareerButtonPrefab.cs, UI/CareerChoiceObjectVM.cs, UI/CareerScreenVM.cs, UI/CharacterDeveloperCareerMixin.cs, UI/CareerEnergyBarPrefab.cs, UI/CareerEnergyBarStateMapper.cs, UI/MissionAgentStatusCareerMixin.cs
Other: Main/SubModule.cs (CareerPerkMissionBehavior construction ~line 1350), Main/Features/DevConsole/Cheats/HudLayoutDumpCheats.cs
Data: Main/_Module/ModuleData/career_system/taom_careers.xml, taom_ability_tuning.xml, Main/_Module/GUI/Brushes/CareerSystem.xml, Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml, Main/_Module/ModuleData/taom_module_strings.xml
Tests: TAOM.Tests/Features/CareerSystem/ (KeystoneExclusivityRuleTests, CareerEnergyBarStateMapperTests, Abilities/AbilityDamageAttributionTests, Abilities/CareerAbilityBuffTrackerTests + updated CareerAbilityTests, CareerAbilityServiceTests, AbilityActivationControllerTests, CareerRegistryTests, CareerConfigProviderTests, CareerScreenVMTests)

REQUIRED SECTIONS:
1. VANILLA CODE -- paste the decompiled evidence for S1/S2/S3 (datasource chain, mixin patch mechanism, prefab name resolution). This is the section that matters most; do not skip it.
2. ENERGY BAR DEEP ANALYSIS -- concrete scenario: campaign battle, career hero mounted with shield, activates ability at t=0 (cooldown 30, duration 8). Walk frame-by-frame what each mixin property returns at t=0, t=4, t=8, t=8.1, t=19, t=30. Then the same with a CooldownReduction mutation stacking to the 5s floor.
3. EXCLUSIVITY RULE ANALYSIS -- enumerate CareerScreenVM paths that mutate choices (select, deselect, switch career) and check the rule stays consistent; deselect a keystone then reselect the OTHER keystone in the tier -- allowed? Grandfathered saves with 2 keystones in tier 1 -- what does the display show for a third keystone in tier 2?
4. CONFIG CROSS-REFERENCE -- keystone_icon ids vs banner_icons.xml and TAOMSpriteData.xml; min_reportable_bonus_damage absent from taom_ability_tuning.xml (default path); the two new module strings.
5. FINDINGS OR OBSERVATIONS -- severity P1 (ship-blocking) / P2 (real bug, workaround exists) / P3 (polish). For each: file, line, evidence, concrete failure scenario, suggested fix.

QUALITY GATES:
- Paste code blocks from BOTH codebases for every finding. No finding without evidence.
- Verify "missing" claims by grepping before asserting.
- If TAOM code matches vanilla behavior, it is NOT a bug -- check vanilla first.
- The deleted files (AbilityHudController etc.) are intentional; do not flag their absence.
- Distinguish pre-existing issues (unlocalized ready/charging toasts, behavior line count) from NEW regressions -- flag pre-existing separately as P3 observations.

PRIOR REVIEW LESSONS:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches. Decompiling MobileParty caught cross-party capability propagation all 5 Claude agents missed.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

Output your review as markdown. Structure: summary, then the 5 required sections, then a findings table (# / severity / file / one-line description).
