# Codex adversarial review: great elk delta + any-rider creature attacks (#636, #643), 2026-09-23

ROLE: reviewer only (AGENTS.md; .ai/roles/reviewer.md). Do not edit, stage, commit, deploy or push anything. Report findings with file:line, impact, and proving code or decompiled engine code. Try to refute each claim before reporting it. Missing engine or native evidence stays UNVERIFIED. This is a working-tree review on branch bannerlord-1.5.x; nothing is committed. The working tree also holds OTHER sessions' uncommitted work (Dependencies/Foundation/*, Main/Features/CrashReport/*, DreadAura, SignatureStrikes, HeroRace, NazgulFamily, UncapturableHeroes, TaomSettings.cs, IoC.cs, SubModule.cs, Animalia elk/moose tools): OUT OF SCOPE unless a file below calls into it.

## FEATURE (what changed)

Bannerlord 1.5.3 mod TAOM. Four elephant-like creature mounts (war elephant, mumakil, Dwarven war ram, great elk) run a per-agent behavior tree that layers an automatic attack on top of the engine's mount handling. This delta, on the maintainer Mike's instructions:
1. #643: the four trees no longer gate their attack branch on IsAiControlledDecorator, so a PLAYER-ridden creature attacks too (the warg's behaviour).
2. The attack's synthetic blow is now OWNED BY THE RIDER (was the creature), falling back to the creature only when the rider is no longer active. The warg's WargAttackService does `attacker.RiderAgent ?? attacker`.
3. The elk's antler charge is one 60-damage BLUNT blow (all other creatures stay Pierce). CustomAttacksUtils.TakeDamage gained `DamageTypes damageType = DamageTypes.Pierce`; a Blunt synthetic blow carries WeaponFlags.CanKillEvenIfBlunt (pure ComposeWeaponFlags(DamageTypes)), because a Blunt killing blow otherwise only wounds in a campaign.
4. TakeDamage now sets `combatLogData.DamageType = damageType` after the CombatLogData constructor (which hard-codes Blunt); before, every synthetic creature blow logged "Blunt".
5. The elk's antler blow scales by the rider's career charge bonus: new ICareerAgentStatService.MountChargeMultiplier(riderHeroId, riderAgentIndex) = (1 + MountChargeDamage passive) x (1 + self buff ChargeDamageBonus) x (1 + ally buff ChargeDamageBonus keyed by the rider's agent index). CareerAgentStatService.ApplyMountStatModifiers now multiplies the mount's MountChargeDamage by exactly that product (it used to multiply the three terms in sequence inside ApplyMountBuff, now ApplyMountSpeedBuff, speed only). ElephantLikeAttackService.ComputeInflictedDamage gained `float riderMultiplier = 1f`, applied only when FiniteFloatValidator.IsFiniteInRange(riderMultiplier, float.Epsilon, 10f), else 1. The per-creature ElephantLikeCombatProfile carries `DamageType` and an optional `Func<Agent, float>? RiderMultiplier`; only the elk sets them (ElkCombat.RiderChargeMultiplier extracts the rider hero id at the boundary and caches the resolved ICareerAgentStatService in a static field).
6. From the review's convergence pass: CareerPerkMissionBehavior.OnScoreHit now returns early when attackerWeapon == null, because a player rider now owns creature blows and AbilityDamageAttributionReporter would otherwise print a false "+N from ability" share for a blow that never ran the damage model; and MountChargeMultiplier returns 1 when its product is not finite.
7. The elk (horse-skeleton reskin, Monster taom_elk on the war ram's action set as_war_ram) is built at 2x: item body_length 200, charge_damage 50 in the LIVE LOTRLOME_Armory; its attack reach is scaled by ElkConfig.AuthoredScale = 2.0.

An internal 7-lens deep review (Review 128) already ran on this delta; its findings, Mike's four decisions and the fixes are in docs/reviews/rca-elk-delta-2026-09-23.md. Find what it missed.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

## READ FIRST

- docs/reviews/rca-elk-delta-2026-09-23.md (this delta's internal review, decisions and fixes)
- docs/features/elk.md (design, tuning table, smoke steps)
- docs/features/elephant.md, section "Player-ridden elephants attack too (#643, 2026-09-23)" (blow owner and combat log evidence)
- .claude/rules/csharp-architecture.md sections "Engine-Float Decision Gates" and "Mission-scope agent handles and the engine's threads"
- AGENTS.md (the Critical Rules; ADR-007 adapters; TDD)

## KNOWN SUSPECTS (CONFIRM or DISPUTE each, with code)

KS1. Rider as blow owner. TakeDamage builds the blow from the attacker: `new Blow(attacker.Index)`, GlobalPosition = midpoint of attacker and victim plus the victim's eye height, `mainHandItemBoneIndex = attacker.Monster.MainHandItemBoneIndex`, CombatLogData from attacker.IsHuman / IsMine / RiderAgent / IsMount. With the RIDER passed, is there any native or managed consumer of the blow that assumes the owner is the agent physically touching the victim (knockback or knockdown direction, friendly-fire or same-team checks, a "hit by own mount" rule, the player's own hit feedback), or that double-counts the rider's hit? Hypothesis: harmless beyond the sound position and kill credit.

KS2. CanKillEvenIfBlunt on a synthetic blow. Mission.GetAgentState is an [MBCallback] that receives damageType and weaponFlags from native. Hypothesis: native reads them from the killing Blow's DamageType and WeaponRecord.WeaponFlags, so the elk's charge kills in a campaign. Find any MANAGED evidence either way (callers, Agent.Die, Agent.HandleBlowAux, how vanilla's own blunt-lethal weapons reach GetAgentState). If only native can answer, say UNVERIFIED and name the in-game check.

KS3. The combat log fix. Confirm GetLogString prints CombatLogData.DamageType and that nothing else reads that field in a way that changes gameplay. Confirm the collision data's int damage type (the third positional int of AttackCollisionData.GetAttackCollisionDataForDebugPurpose) is still consistent with blow.DamageType and which consumers read it (OnEntityHit, Agent.HandleBlow, sound selection).

KS4. Ally buff keyed by agent index. The antler multiplier reads CareerAbilityBuffTracker.GetAllyBuff(rider.Index), as the mount path (#611) already did. Agent indices are recycled after deletion (#592). Does the tracker evict entries when an agent is removed or deleted, or can a stale ally buff under a recycled index scale a different rider's antler charge (and body charge)? Pre-existing exposure extended by this change: say which.

KS5. ElkCombat's static `_careerStats` cache (IoC.Resolve<ICareerAgentStatService>() once per process). Is ICareerAgentStatService a DryIoc singleton, and does TAOM ever rebuild the container (new game, tests) such that the static field holds a stale instance? Is the resolve safe in Custom Battle and on a dedicated server?

KS7. The weaponless-hit gate in CareerPerkMissionBehavior.OnScoreHit (attackerWeapon == null). Which vanilla hits by the player's own agent arrive with a null attackerWeapon (bare hands, kicks, shield bash, anything else) and DID run the damage model that the ability's DamageMultiplierBonus feeds, so their true "+N from ability" line is now lost? Is there any TAOM synthetic blow that arrives with a NON-null weapon and still gets a false line? Also: native resolves the affector that Mission.OnAgentRemoved receives (Agent.Die hands the blow to IMBAgent.Die); find any managed evidence that it is the blow's OwnerId.

KS6. Parity of CareerAgentStatService.ApplyMountStatModifiers before and after (git diff HEAD -- Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs): same MountChargeDamage result for every combination of passive, self buff and ally buff (zero, negative, NaN), same MountSpeed result, same no-op and logging for null ids.

## FILES

C# (changed; Main/Features/Elk/ is untracked, read the files, git diff will not show it):
- Main/Features/AdvancedCombat/CustomAttacksUtils.cs (TakeDamage, ComposeWeaponFlags, ComposeBlowFlags)
- Main/Features/ElephantLike/ElephantLikeAttackService.cs, Main/Features/ElephantLike/IElephantLikeAttackService.cs
- Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeAttackTasks.cs (Execute, Hit), ElephantLikeCombatProfile.cs, ElephantLikeEngageDecorator.cs, ElephantLikeAttackOffCooldownDecorator.cs
- Main/Features/Elephant/ElephantBehaviorTree.cs, Main/Features/Mumakil/MumakilBehaviorTree.cs, Main/Features/WarRam/WarRamBehaviorTree.cs, Main/Features/Elk/ElkBehaviorTree.cs
- Main/Features/Elk/ElkConfig.cs, ElkAttackService.cs, IElkAttackService.cs, ElkCombat.cs, ElkMissionBehavior.cs, ElkIoC.cs
- Main/Features/Elephant/ElephantCombat.cs, Main/Features/Mumakil/MumakilCombat.cs, Main/Features/WarRam/WarRamCombat.cs (unchanged profiles; confirm they keep Pierce and no multiplier)
- Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs, ICareerAgentStatService.cs, CareerAbilityBuffTracker.cs (read-only context), Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs (read-only context)
- Main/Features/CareerSystem/CareerPerkMissionBehavior.cs (OnScoreHit gate), Main/Features/CareerSystem/Abilities/AbilityDamageAttributionReporter.cs and AbilityDamageAttribution.cs (read-only context)
- Main/Features/Warg/WargAttackService.cs, Main/Features/Spider/SpiderAttackService.cs, Main/Features/SignatureStrikes/Hooks/SignatureStrikeRunner.cs (other TakeDamage callers; confirm unaffected)
Tests:
- TAOM.Tests/Features/Elk/ElkAttackServiceTests.cs, ElkConfigTests.cs, ElkMountWiringTests.cs
- TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsBlowFlagsTests.cs
- TAOM.Tests/Features/CareerSystem/CareerAgentStatServiceTests.cs (MountChargeMultiplier block), CareerMountBonusBindingTests.cs
- TAOM.Tests/Features/{Elephant,Mumakil,WarRam}/*AttackServiceTests.cs (unchanged; confirm still meaningful)
Data (live, external, untracked; read-only):
- E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\LOTRAOM_horses.xml (item taom_elk_a, taom_elk_saddle_a)
- E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\Monsters\LOTR\lotr_monster_elk.xml
- Main/_Module/ModuleData/career_system/taom_career_choices.xml (elk_rider choices: MountChargeDamage passives), taom_ability_templates.xml (elk_rider_ability)
Docs: docs/features/elk.md, elephant.md, mumakil.md, war-ram.md (#643 sections), docs/ai-includes/creature-mount-authoring.md (Phase 7 table), CHANGELOG.md (the #643 and #636 entries only)

## REQUIRED SECTIONS

### VANILLA CODE
Engine source for v1.5.3: installed DLLs are authoritative. Pre-decompiled files: C:\Users\mikew\.taom-src\v1.5.3\ (e.g. TaleWorlds.MountAndBlade.CombatLogData.cs, TaleWorlds.MountAndBlade.Mission.cs, TaleWorlds.MountAndBlade.Agent.cs, TaleWorlds.MountAndBlade.MissionCombatMechanicsHelper.cs, SandBox.GameComponents.SandboxAgentDecideKilledOrUnconsciousModel.cs, TaleWorlds.MountAndBlade.DefaultAgentDecideKilledOrUnconsciousModel.cs, SandBox.Missions.MissionLogics.BattleAgentLogic.cs, TaleWorlds.CampaignSystem.GameComponents.DefaultCombatXpModel.cs, TaleWorlds.Core.ItemObject.cs, TaleWorlds.CampaignSystem.CharacterObject.cs); the browseable dump is E:\Decompiled_Bannerlord\ (may lag); the ilspy MCP is available. Paste the decompiled lines that prove or refute each engine claim, with file:line. Specifically: CombatLogData constructor and GetLogString; MissionCombatMechanicsHelper.GetAttackCollisionResults (where vanilla sets combatLog.DamageType); Mission.RegisterBlow and PrintAttackCollisionResults; Agent.HandleBlow (affector from Blow.OwnerId, RegisterLastBlow); Mission.GetAgentState and its callers; SandboxAgentDecideKilledOrUnconsciousModel.GetAgentStateProbability and DefaultPartyHealingModel.GetSurvivalChance; BattleAgentLogic.OnScoreHit and OnAgentRemoved; Mission.CreateHorseAgentFromRosterElements.

### FEATURE-SPECIFIC DEEP ANALYSIS (walk each scenario, state the result)
S1. A campaign battle, the player (elk_rider career, Antler Crash active) rides the elk into an enemy infantryman who is not shield-blocking: the damage number, the blow owner, the knockdown, the combat log line, the kill-or-wound result if it finishes him, who gets the kill and XP.
S2. Same, the victim shield-blocks: damage, knockdown.
S3. An AI Mirkwood cavalry troop on an elk with no career buff: damage 60, owner the AI rider.
S4. The rider dies in the same tick the charge fires (rider.IsActive() false): owner falls back to the elk; any crash, stale handle or wrong team check?
S5. A player riding the war ram, the elephant or the mumakil (Custom Battle): the attack fires, its damage is unchanged from before this delta, the owner is the player.
S6. A NaN or huge career passive magnitude (corrupt config): the antler damage, and the mount's MountChargeDamage.
S7. The warg, the spider and a signature strike after this delta: identical blows except the combat log now reads their true type.

### CONFIG CROSS-REFERENCE
- ElkConfig.AuthoredScale 2.0 vs live taom_elk_a body_length="200"; ElkConfig.AttackDamage 60 vs the docs; the item's charge_damage 50.
- Every culture or kingdom id in the files above against the cheatsheet (mirkwood is valid).
- The elk_rider career's MountChargeDamage passive magnitudes and the cavalry ability's ChargeDamageBonus source (AbilityTuningConfig, CavalryAbilityExecutor) vs what elk.md and the Antler Crash tooltip promise.

### FINDINGS OR OBSERVATIONS
For each: severity (P0 critical, P1 high, P2 medium, P3 low), file:line, what is wrong, the proving code, the minimal fix. Then UNVERIFIED items with the in-game check that would settle each. Then things you checked that are correct (briefly).

## QUALITY GATES
- Every engine claim carries pasted decompiled code with file:line; no claim from memory.
- Do not flag vanilla-matching behaviour as a TAOM bug; do not flag the other sessions' out-of-scope files.
- Distinguish pre-existing exposure from what this delta introduced or extended.
- Before calling a test missing, grep TAOM.Tests for it.
- Do not run builds, tests or tools that write files.

## PRIOR REVIEW LESSONS
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

## OUTPUT
Return the full report as your FINAL MESSAGE. The dispatcher redirects your stdout into docs/reviews/raw/codex-adversarial-elk-delta-2026-09-23.md; do NOT write that path yourself, and do not create other files.
