# Codex Adversarial Review -- Career phantom-passive wiring (2026-06-26)

You are an adversarial code reviewer for TAOM, a Bannerlord 1.4.6 total-conversion mod. Review an UNCOMMITTED changeset. Be skeptical, verify against the actual source + the decompiled engine, and report only real, specific defects. For every claim, cite file:line and the engine evidence you read. Default to DISPUTED if you cannot confirm a hypothesis by reading code.

## What changed (one feature)

TAOM career "pips" grant per-pip passive bonuses. Six PassiveEffectType values were authored into ~211 pips but had NO runtime consumer -- selecting them was cached and applied to nothing (~16% of all pip-passives). This change:
- Wires all six: HorseChargeDamage, HorseHealth, HealthRegeneration, StealthBonus, TroopResistance, Ammo.
- Honors attack_type_mask for Damage + Resistance: Damage was moved OFF the flat AgentDrivenProperties.DamageMultiplierBonus onto the per-hit amplification path so a "+X% melee damage" pip only fires on melee hits. CareerPassiveService gained a per-(type, mask) cache (_maskedCache) + GetMaskedMagnitude.
- Removed the never-read operation / is_percentage fields from PassiveEffect (kept + wired attack_type_mask).
- Re-tuned the six types magnitudes to a uniform 10-15% band (tier-scaled 0.10/0.13/0.15) via tools/retune_phantom_passives.py, and synced the English descriptions via tools/retune_phantom_descriptions.py.
- Added a load-time gate (PassiveEffectConsumers + CareerConfigProvider.ValidatePassiveConsumers) so an unconsumed type warns + a shipped-XML regression test pins it.

This already passed a 6-dimension multi-agent deep-review (0 HIGH, 6 MED, 3 LOW); those fixes are IN this diff. RCA: docs/reviews/rca-career-phantom-passives-2026-06-26.md. Your job is the independent second opinion -- find what that review missed, and confirm/dispute the fixes.

## READ FIRST

- docs/features/career-system.md (sections: PassiveEffect schemas, Magnitude scale, attack_type_mask, Effect-type consumers)
- docs/reviews/rca-career-phantom-passives-2026-06-26.md
- Main/_Module/ModuleData/career_system/taom_career_choices.xml (the re-tuned pip data)
- Main/_Module/ModuleData/taom_career_strings.xml (the synced descriptions)

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa.
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar.
NOTE: "rohan" is NOT a valid ID (Rohan uses vlandia). "dol_guldur" is NOT valid -- use "dolguldur".

## Engine research

Authoritative v1.4.6 signatures: run `pwsh tools/taom-src.ps1 path <FullTypeName>` then read the printed path. Browse E:\Decompiled_Bannerlord\ for patterns (it is v1.4.5 -- prefer taom-src for signatures). Decompile these targets to verify the findings below:
- TaleWorlds.MountAndBlade.AttackInformation (fields VictimAgent, IsVictimAgentMount, VictimAgentOrigin, VictimRiderAgentOrigin, IsVictimAgentNull).
- TaleWorlds.MountAndBlade.AttackCollisionData (IsMissile).
- TaleWorlds.MountAndBlade.AgentDrivenProperties (MountChargeDamage, DamageMultiplierBonus) -- note WHERE the engine reads DamageMultiplierBonus in its damage pipeline vs where ApplyDamageAmplifications runs.
- SandBox.GameComponents.SandboxAgentApplyDamageModel.ApplyDamageAmplifications / ApplyDamageReductions (the base TAOM calls).
- SandBox.GameComponents.SandboxAgentStatCalculateModel.GetEffectiveMaxHealth / UpdateAgentStats.
- TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel.GetPartySpottingRatioForMainPartySeeingRange + its consumer in PartyBase (the visibility comparison -- confirm whether a LOWER returned ratio makes a party harder to spot).
- TaleWorlds.CampaignSystem.ComponentInterfaces.PartyHealingModel.GetDailyHealingHpForHeroes (signature + that ExplainedNumber.AddFactor is the right apply).
- TaleWorlds.MountAndBlade.Agent (IsMount, RiderAgent null semantics, SetWeaponAmountInSlot) + MissionWeapon (IsAnyAmmo/Amount/ModifiedMaxAmount) + MissionBehavior.OnAgentBuild.

## Known Suspects -- CONFIRM or DISPUTE each by reading code

1. Damage moved to the amplification path. Damage was removed from CareerAgentStatService.ApplyHeroPassives (flat DamageMultiplierBonus) and now applies in CalculateDamageAmplification as result *= (1 + maskedDamage). Decompile the engine: is DamageMultiplierBonus applied at a DIFFERENT stage (e.g. after armor) than ApplyDamageAmplifications (before reductions)? Does moving it change the effective damage materially, double-count, or interact wrongly with ArmorPenetration which is also applied there? Is there any path where Damage is now applied twice or not at all?

2. TroopResistance victim resolution. CalculateDamageReduction now reduces damage for the victim troop's PARTY LEADER's TroopResistance. The model (TaomAgentApplyDamageModel.GetVictimTroopLeaderHeroId) resolves the leader from info.IsVictimAgentMount ? info.VictimRiderAgentOrigin : info.VictimAgentOrigin (this was a deep-review fix -- the mount's own Origin is null). Confirm: (a) the fix is correct and there is no remaining null-origin case; (b) it keys on the VICTIM's own party leader, so an ENEMY troop's hit never pulls the player's TroopResistance (and vice versa); (c) a hero victim returns null here (heroes use Resistance, not TroopResistance) -- verify no double-application for a hero. Is "the victim troop's leader has the passive" the intended semantic, or should it be the ATTACKER-relative side?

3. Masked cache + thread-safety. CareerPassiveService._maskedCache (Dictionary<hero, Dictionary<type, Dictionary<AttackTypeMask, float>>>) is built outside the lock and swapped under the SAME lock as _cache; GetMaskedMagnitude captures the snapshot under the lock then iterates lock-free. AttackTypeMask is [Flags] None=0/Melee=1/Ranged=2/All=3. GetMaskedMagnitude sums buckets where (authoredMask & hitMask) != None. Confirm: All-masked entries apply to both melee and ranged; a hit mask is always a single bit (TaomAgentApplyDamageModel.HitMask = IsMissile ? Ranged : Melee); the two caches can never be observed inconsistently (both swapped together). Is there any concurrency or correctness gap?

4. Ammo OnAgentBuild. CareerPerkMissionBehavior.OnAgentBuild fires per agent; early-outs on !agent.IsHero; for a hero with the Ammo passive it loops weapon slots and calls CareerPassiveMath.BoostAmmo(ModifiedMaxAmount, Amount, bonus) -> SetWeaponAmountInSlot. Confirm: (a) ModifiedMaxAmount is the right base (not the current Amount); (b) the no-shrink guard (boosted > Amount) and short-clamp are correct; (c) it cannot wrongly fire for the WRONG hero or a non-ranged loadout (IsAnyAmmo gate); (d) which heroes have career data -- is this player-only, or could AI lords/companions with career data get free ammo? Per-spawn cost acceptable?

5. StealthBonus direction. TaomMapVisibilityModel.GetPartySpottingRatioForMainPartySeeingRange returns CareerPassiveMath.ApplyStealthRatio(ratio, bonus) = ratio * (1 - bonus) for bonus != 0. Decompile the engine consumer of this ratio (PartyBase visibility) and CONFIRM a LOWER ratio means the party is harder for others to spot (so a positive StealthBonus is a buff, not a nerf).

6. HorseHealth on the mount. TaomAgentStatCalculateModel.GetEffectiveMaxHealth: for a hero -> + Health (flat); for a MOUNT whose RiderAgent is a hero -> base * (1 + HorseHealth) via CareerAgentStatService.ApplyMaxHealthPassives. Confirm: GetEffectiveMaxHealth is actually invoked for mount agents in v1.4.6; RiderAgent is null-safe for a riderless mount; a hero and a mount can never both match (no double-apply); base.GetEffectiveMaxHealth is not already including something this multiplies wrongly.

7. Re-tune integrity (data). tools/retune_phantom_passives.py rewrote ONLY magnitude/value numbers for the six phantom types to 0.10/0.13/0.15 by tier. tools/retune_phantom_descriptions.py rewrote descriptions type-phrase-anchored. Confirm by reading the scripts + spot-checking taom_career_choices.xml + git diff: no NON-phantom passive type had its magnitude changed; no non-magnitude attribute changed; no ability-mutation number was wrongly rewritten in a description; the magnitudes and the description %-figures now agree per pip; Ammo descriptions read "+N% ammo" (the consumer is multiplicative). Note 5 Resistance pips author attack_type_mask="Blunt"/"Cut" which the [Flags] enum cannot represent and which deliberately degrade to All -- confirm that is intended and that the parser does not crash or zero them.

8. Consumer set completeness. PassiveEffectConsumers is the compiled source of truth for "types with a consumer." Confirm every entry actually has a runtime consumer in Main/, and every real consumer's type is listed (a missing entry would make the load-gate warn on a working type; an extra entry would let a future phantom slip the regression test). Cross-check against the actual consumers.

## Also check (things the deep-review may have under-weighted)

- operation / is_percentage removal: grep for any remaining reference to PassiveEffect.Operation / .IsPercentage anywhere in Main/ or TAOM.Tests/. ParseBool was deleted from CareerConfigProvider -- confirm no other caller.
- SubModule.cs registration: TaomAgentStatCalculateModel dropped its ICareerPassiveService ctor param; TaomPartyHealingModel GAINED one (resolved inline at the call site). Confirm both registrations compile and resolve, and no other constructor mismatch.
- Save-compat: any new SaveableType / SyncData? (should be none.)
- Balance: stacking several same-type masked Damage pips multiplies -- is the cumulative magnitude bounded/reasonable, or can a tree stack to an extreme multiplier?

## REQUIRED OUTPUT SECTIONS

1. VANILLA CODE -- paste the decompiled engine signatures/bodies you relied on (AttackInformation ctor origin assignment, the DamageMultiplierBonus read site, the spotting-ratio consumer).
2. KNOWN SUSPECTS -- one CONFIRMED / DISPUTED / PARTIAL verdict per suspect (1-8) with file:line + engine evidence.
3. FINDINGS -- any additional defects, each with severity (HIGH/MED/LOW), file:line, why it is a bug, and a concrete fix.
4. FALSE-POSITIVE SELF-CHECK -- list anything you considered flagging but confirmed was correct (so the orchestrator does not re-investigate it).

## QUALITY GATES

- Do NOT flag vanilla-matching code as a bug -- decompile before claiming a deviation.
- Do NOT assume an API's semantics from its name -- read a real call site (especially the DamageMultiplierBonus stage and the spotting-ratio direction).
- Cite file:line for every claim. "I didn't find X" is not evidence -- grep for it.
- empire = Dunland, NOT Rohan. Rohan = vlandia. Do not invent ID mismatches.

## Prior review lessons

SUCCESSES: config ID cross-ref caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland); Codex flagged vanilla-matching code as bugs; Codex skipped hard sections (decompile the DamageMultiplierBonus stage -- do not skip suspect 1).
