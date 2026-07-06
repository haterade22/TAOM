ADVERSARIAL REVIEW: CombatMechanics feature (TAOM, Bannerlord 1.4.6, issue #320)

You are reviewing a NEW feature that occupies the engine's single AgentApplyDamageModel slot. It is a spec-driven implementation (constants/formulas recorded as facts, no code copied). Your job: find real bugs. Be adversarial -- assume something is wrong and hunt for it. A 6-agent Claude deep review already ran and its 8 findings were fixed (docs/reviews/rca-combat-mechanics-2026-07-02.md) -- do NOT re-report those; find what it missed.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
NOTE: "rohan" is NOT a valid ID (Rohan uses "vlandia"). "dol_guldur" is NOT valid -- use "dolguldur".
RACE names in this feature come from the TAOM race registry (raceage precedent): human, dwarf, orc, uruk_hai, uruk, pale_uruk, dg_uruk, berserker, goblin, cave_troll, hill_troll, elf, nazghul, saruman. Monster ids: cave_troll, hill_troll, spider, taom_war_elephant, taom_mumakil, warg (+_settlement/_settlement_fast/_settlement_slow variants).

READ FIRST:
- docs/features/combat-mechanics.md (architecture + override table)
- Main/_Module/ModuleData/combat_mechanics/combat_mechanics_config.json
- docs/reviews/rca-combat-mechanics-2026-07-02.md (already-fixed findings -- do not re-report)

KNOWN SUSPECTS -- CONFIRM or DISPUTE each with evidence:
1. Monster-vs-shield crush-through is WEAKER than intended. CrushThroughService: a monster attacker vs a SHIELD block falls through (null) unless the orc or skill path fires, ultimately reaching the engine base 58f check which ALSO requires an overhead (AttackUp) swing direction. The upstream inspiration ran its own energy check inside the monster branch for shields regardless of direction. Question: can a troll swing (non-overhead) vs shield with totalAttackEnergy > 69.6 ever crush in TAOM? If not, is that a real gameplay regression vs the spec intent (spec says "shield block falls through to base energy check")?
2. Cleave-through-shield overpromise. TaomSettings hint for EnableCreatureCleave says one swing can chain "including through shield blocks". But ShouldForceSliceThrough requires inflictedDamage > 0; a fully blocked shield hit may have InflictedDamage == 0 at DecideWeaponCollisionReaction time. Decompile Mission.cs MeleeHitCallback + MissionCombatMechanicsHelper.DecideWeaponCollisionReaction on the installed 1.4.6 DLLs and determine what InflictedDamage contains when the blow was shield-blocked. If it is 0, the hint overpromises and the shield-block Bounced branch still terminates troll chains.
3. Race-id space mismatch. The model extracts Agent.Character.Race (int) and RaceCombatModifiersResolver maps config race NAMES to ids via TAOM's IRaceManager (Main/Core/Domain/RaceManager.cs -- read it). Verify IRaceManager's id space is the SAME id space as BasicCharacterObject.Race on 1.4.6 (both should be the FaceGen race registry indices). If they differ, every race modifier silently never applies.
4. KnockBack flag timing on the charge path. ChargeKnockdownService Branch B requires context.HasKnockBackFlag read from blow.BlowFlag. Decompile Mission.ChargeDamageCallback (installed 1.4.6) and verify blow.BlowFlag |= BlowFlags.KnockBack is applied to the SAME Blow struct instance that is subsequently passed (by in-reference) to DecideAgentKnockedDownByBlow -- i.e., the flag is visible at our read. If the engine copies the struct before the knockdown call, Branch B is dead (HasKnockBackFlag always false) and every non-Branch-A charge returns null.
5. Career shrug-off double-dip through stagger reentrancy. base.DecideAgentShrugOffBlow (CareerSystem parent) -> vanilla helper -> CalculateStaggerThresholdDamage on the REGISTERED model (ours) -> race StaggerThresholdMultiplier. Then IsUnstoppable adds creature thresholds. Verify no unintended interaction: a dwarf HERO with career shrug-off passives + 1.5x stagger multiplier -- is the multiplier applied ONCE (in the vanilla threshold path) and not compounded anywhere else?
6. MCM-wins-over-JSON dead toggles. CombatMechanicsSettingsProvider: TaomSettings.Instance?.X ?? jsonDefault. Once MCM loads, the MCM value (default true) always wins, so a user who sets "skillBasedEnabled": false in JSON but never touches MCM still gets the mechanic ON. Confirm whether that matches TAOM's established AlignmentDesertion pattern semantics, and whether docs/features/combat-mechanics.md claims JSON enables do anything post-MCM (if so, doc bug).

FILES:
Model + services (Main/Features/CombatMechanics/): Models/TaomCombatMechanicsModel.cs, CrushThroughService.cs, ChargeKnockdownService.cs, CreatureCombatService.cs, ShieldPenetrationService.cs, RaceCombatModifiersResolver.cs, CombatMechanicsConfigProvider.cs, CombatMechanicsSettingsProvider.cs, CombatMechanicsConfig.cs, CombatMechanicsIoC.cs, Domain/CrushThroughContext.cs, Domain/ChargeKnockdownContext.cs, Domain/RaceCombatModifiers.cs, all I*.cs interfaces
Parent model (modified to abstract): Main/Features/CareerSystem/Models/TaomAgentApplyDamageModel.cs
Wiring: Main/SubModule.cs (search "TaomCombatMechanicsModel"), Main/IoC.cs (search "CombatMechanics"), Main/Features/TaomSettings.cs (search "Combat Mechanics")
Config: Main/_Module/ModuleData/combat_mechanics/combat_mechanics_config.json
Tests: TAOM.Tests/Features/CombatMechanics/*.cs (7 files)

REQUIRED SECTIONS:

VANILLA CODE: Decompile from the INSTALLED 1.4.6 DLLs (E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/ and Modules/SandBox/bin/Win64_Shipping_Client/SandBox.dll; cached decompiles also exist at C:/Users/mikew/.taom-src/v1.4.6/). Paste as code blocks:
- SandBox.GameComponents.SandboxAgentApplyDamageModel: DecideCrushedThrough, DecideMissileWeaponFlags, CalculateShieldDamage, CalculateStaggerThresholdDamage
- TaleWorlds.MountAndBlade.MissionCombatMechanicsHelper: DecideAgentShrugOffBlow, DecideAgentKnockedBackByBlow, DecideAgentKnockedDownByBlow, DecideWeaponCollisionReaction, UpdateMomentumRemaining, DecideCombatEffect
- TaleWorlds.MountAndBlade.Mission: ChargeDamageCallback + the MeleeHitCallback region that wires CalculateRemainingMomentum into DecideWeaponCollisionReaction
- TaleWorlds.Core.Monster: Weight + RelativeSpeedLimitForCharge deserialization
- AgentStatCalculateModel.GetKnockDownResistance signature

FEATURE-SPECIFIC DEEP ANALYSIS -- concrete scenarios, trace each through the actual code with numbers:
- Scenario A: Native horse (400) + rider (80) charges a man (80) at velocity 4.3 (speedFactor 1.0), damage 50, maxHealth 100, knockDownResistance 0.6, KnockBack set. Compute TAOM Branch B verdict AND the vanilla verdict. They should match (neutral calibration). Show the arithmetic.
- Scenario B: same charge vs a dwarf (weight 100, raceResist 2.5). Compute the verdict.
- Scenario C: mumakil (9999) charging at ChargeVelocity 0.5 with rslc 1.0 -- does Branch A fire (speedFactor 0.5 >= 0.4)? Is that intended at half charge speed?
- Scenario D: troll (Monster cave_troll) swings at a shield-wall soldier, non-overhead, energy 70. Trace DecideCrushedThrough end to end. Then the same hit chain: CalculateRemainingMomentum -> DecideWeaponCollisionReaction. Does the swing crush, and does it chain?
- Scenario E: player (IsAIControlled false) playing an orc-race character swings at a shield: confirm the orc path does NOT fire and the plain skill path refuses the shield.
- Scenario F: skill CTB at delta exactly 200, energy 40 (full ramp), CrushThroughMaxChance MCM 0.5, roll 0.4999 vs 0.5 -- confirm exact-cap arithmetic.
- Edge: victim Monster null (victimWeight fallback 1) -- what does a mumakil charge vs a WARG (weight 500) or vs a horse (mount victim -- is the victim agent the horse or its rider on the charge path?) produce? Decompile ChargeDamageCallback to determine WHO the victim agent is when cavalry rams cavalry, and whether weightRatio uses the mount's weight (correct) or the rider's (wrong).

CONFIG CROSS-REFERENCE: every key in combat_mechanics_config.json vs the POCO property names (Json.NET camelCase binding) vs actual consumption; every monster id vs the Monster XML ids; every race name vs the race registry; MCM property names vs settings-provider reads.

FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 blocking / P2 should-fix / P3 nice-to-have), file:line, evidence (code you actually read), and a concrete failure scenario. If a section yields nothing, write "no findings" -- do not skip sections.

QUALITY GATES:
- Paste vanilla decompile snippets you actually read (not from memory).
- Verify every "missing X" claim by grepping the repo before reporting it.
- Upstream-comparison fairness: the reference targets Bannerlord 1.3.15; do not report 1.4.6 API differences as TAOM bugs.
- The spec doc is normative for constants -- a constant matching the spec is not a bug even if you would tune it differently.

PRIOR REVIEW LESSONS:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates (NavalTravel army propagation, Save/Load patch timing). Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

Output your review as markdown. Start with a one-paragraph verdict, then Known Suspects verdicts, then the required sections.
