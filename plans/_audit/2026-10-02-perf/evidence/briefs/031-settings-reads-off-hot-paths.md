Plan 031: stop resolving MCM settings on every read in the providers and patches that run per blow, per
frame or per agent, using the pattern commit 7feca96b proved for BattleBalanceSettingsProvider; and move
the other cheap per-frame and per-blow reads the audit found to the cheapest point. Behaviour-preserving:
live MCM edits still apply.

THE PATTERN (7feca96b, "perf(battlebalance): read the MCM settings once per process"):
Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs caches `TaomSettings.Instance` once in its
constructor and reads THROUGH the reference (`_settings?.X ?? default`), so live MCM edits still apply,
"same contract as NameplateFadeSettingsProvider (Patch38)". Its precondition, checked in that commit: the
provider is first resolved after MCM created the settings (its only resolve is under OnGameStart; the eager
pass at the end of IoC.Configure in OnSubModuleLoad does not reach it), so the constructor never caches a
null. It shipped with an IL rule test, Getters_NeverReadTaomSettingsInstance_TheConstructorDoes, RED
against the old provider. Read that commit (`git show 7feca96b`) and model every change on it. What one
`TaomSettings.Instance` read costs: MCM's GlobalSettings<T>.Instance does a ConcurrentDictionary ContainsKey
plus indexer, then BaseSettingsProvider.GetSettings walks the settings containers with string-keyed
lookups (E:/Decompiled_Bannerlord/_modules_build/TAOM.Dependencies__MCMv5.cs:6321-6330, :2214-2232).

WHERE IT STILL HAPPENS (audit 2026-10-02, at dffdf879; the writer re-verifies each and the resolve point
of each provider, because a provider resolved before MCM creates the settings must use a lazy cache that
keeps resolving until it gets a non-null instance, never cache null):
- Per blow: Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs:19-38 (each *Enabled reads
  TaomSettings.Instance twice: MasterEnabled plus its own field) and :40-70 (float getters), read before
  cheaper checks by CreatureCombatService.cs:67, :82, :90; RaceCombatModifiersResolver.cs:35;
  CrushThroughService.cs:88, :100, :104, :139; ChargeKnockdownService.cs:40, :66, :78-79;
  ShieldPenetrationService.cs:51, :71 (about eight reads per ordinary melee hit, more on blocked swings).
  Also order those per-hit gates cheapest first where the order cannot change a result.
- Per blow while switched off: BlowDiagnosticsSettingsProvider.cs:9-10 (reads BlowDiagnosticsSettings.Instance,
  its own MCM class) from Agent_HandleBlowAux_BlowDiag_Patch.cs:31-33.
- Per frame: MixedFormationsSettingsProvider.cs:7 (read by FormationLayoutService.cs:68 and by
  MixedFormationsMissionBehavior.cs:50, whose :124-133 also reads CycleHotkey and calls Enum.TryParse every
  frame: parse only when the string changes); CompanionTacticsSettingsProvider.cs:14 (Patch35 Formation
  presets; plan 030 deletes the empty Mission.OnTick postfix, so do not touch Patch35_Mission_OnTick.cs);
  DreadAuraSettingsProvider.cs:33 (DreadAuraMissionLogic.cs:73 every frame); the SiegePropDiagnostics
  per-frame read (SiegePropDiagnostics behaviour :50); the howdah diagnostics provider
  (HowdahDiagnosticsSettingsProvider.cs:10, read per seat per frame at TaomHowdahStandingPoint.cs:219);
  the SmartCavalryAI provider (SmartCavalryAIMissionBehavior.cs:66, :93); BattleActionBarMissionView.cs:76.
- Per agent update: CultureDoctrineSettingsProvider.cs:12-19 and ChargeDamageService.cs:25 with its
  provider :38, read from TaomAgentStatCalculateModel.UpdateAgentStats bursts (TaomAgentStatCalculateModel.cs:96-126).
- Per order, every team: Patch35_Formation_SetMovementOrder.cs:37 reads CancelStanceOnMove before the team
  filter at :49: move the filter first.
- Per frame with presets on: OOBOverlayService.cs:73-82 (an MCM read plus a reflection FieldInfo.GetValue
  with boxing every frame): cache the FieldInfo (it may already be cached; check) and read the setting
  through a cached reference.
- Per localized string: Main/Features/LocalizationOverride/Hooks/MBTextManager_GetLocalizedText_Patch.cs:29-50
  allocates text.Substring(2, idLength) for every {=ID} text before its dictionary probe: probe without
  allocating (for example a dictionary keyed by a struct that wraps (string, start, length) with a
  comparer, or a precomputed hash over the override keys; keep it simple and correct).
- BattleActionBarMissionView.cs:110-126 creates a new FormationAdapter every 0.5 s, which defeats the
  adapter's own 500 ms composition TTL (FormationAdapter.cs:88-126): keep one adapter per formation.

TESTS: per changed provider, the IL rule used by 7feca96b (getters never read the static Instance; the
constructor or the lazy path does), and default pins with no MCM present (null settings give the same
defaults as before); behaviour tests for the hotkey parse-on-change, the SetMovementOrder filter order, the
localization probe (same lookups, same results, overrides still win) and the action-bar adapter reuse.
Live MCM edits: a test that changes a property on the cached instance and sees the provider return it.

OUT OF SCOPE: Formation.GetOrderPositionOfUnit (Patch30) and the Patch93 wield getters (plan 032 owns
them, including FormationLayoutService's lock); any MCM default change.

STOP conditions to include: a provider whose first resolve happens before MCM creates its settings and
whose lazy cache would change behaviour; any change that would alter a combat result; an IoC lifetime
change (Main/IoC.cs is single-owner).
