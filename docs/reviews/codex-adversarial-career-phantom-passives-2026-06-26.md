# Codex Adversarial Review — Career phantom-passive wiring (2026-06-26)

Model: gpt-5.5 (xhigh reasoning) · 269,888 tokens · **VERDICT: CLEAN (0 CRITICAL / 0 HIGH / 0 MEDIUM / 0 LOW)**

Prompt: [codex-adversarial-career-phantom-passives-2026-06-26.prompt.md](codex-adversarial-career-phantom-passives-2026-06-26.prompt.md).
Cleaned from the raw `codex exec` stream (reasoning trace + ANSI stripped); final answer verbatim below.

---

## VANILLA CODE

```csharp
// AttackInformation ctor, v1.4.6
VictimAgentCharacter = victimAgent.Character;
VictimAgentOrigin = victimAgent.Origin;
if (DoesVictimHaveRiderAgent) {
    Agent riderAgent = victimAgent.RiderAgent;
    VictimRiderAgentCharacter = riderAgent.Character;
    VictimRiderAgentOrigin = riderAgent.Origin;
}
```
Evidence: `...\TaleWorlds.MountAndBlade.AttackInformation.cs:279-286`.

```csharp
// AgentApplyDamageModel.CalculateDamage, v1.4.6 — pipeline order
baseDamage2 = ApplyDamageAmplifications(...);   // (1) masked career Damage + ArmorPenetration here
baseDamage2 = ApplyDamageScaling(...);          // (2)
baseDamage2 = ApplyDamageReductions(...);       // (3) armor
baseDamage2 = ApplyGeneralDamageModifiers(...); // (4) vanilla DamageMultiplierBonus here (after armor)

// SandboxAgentApplyDamageModel.ApplyGeneralDamageModifiers
val = new ExplainedNumber(baseDamage, false, null);
if (attackerAgent != null)
    val = new ExplainedNumber(val.ResultNumber *
        (1f + attackerAgent.AgentDrivenProperties.DamageMultiplierBonus), false, null);
```
Evidence: `...\AgentApplyDamageModel.cs:15-18`; `SandboxAgentApplyDamageModel.cs:597-623`.

```csharp
// PartyBase visibility consumer, v1.4.6
float num = CalculateVisibilityRangeOfMapPoint(...); // distance / seeing range
float ratio = Campaign.Current.Models.MapVisibilityModel
    .GetPartySpottingRatioForMainPartySeeingRange(mobileParty);
isVisible = mapPoint.IsActive && num <= ratio;       // lower ratio = must be closer = harder to spot
```
Evidence: `...\TaleWorlds.CampaignSystem.Party.PartyBase.cs:1078-1082`.

## KNOWN SUSPECTS

1. **PARTIAL.** Damage is no longer double-applied: `ApplyHeroPassives` removed the `DamageMultiplierBonus` write and applies `Damage` only through `GetMaskedMagnitude` in `CareerAgentStatService.cs:59-65`. But this is a real stage move: vanilla `DamageMultiplierBonus` is applied in general modifiers after reductions, while TAOM now applies career `Damage` during amplification before scaling/reductions. I found no concrete double-count or missing path.
2. **DISPUTED.** The mount-origin fix is correct. `TaomAgentApplyDamageModel.cs:58-88` resolves a struck mount to its rider and uses `VictimRiderAgentOrigin`; vanilla stores rider origin separately at `AttackInformation.cs:281-286`. Hero victims return null at `TaomAgentApplyDamageModel.cs:82-83`, so `Resistance` and `TroopResistance` do not double-apply.
3. **DISPUTED.** Mask logic is correct. `HitMask` is exactly one bit from `AttackCollisionData.IsMissile` (`TaomAgentApplyDamageModel.cs:44-47`; engine property at `AttackCollisionData.cs:86`). `_cache` and `_maskedCache` are built together and swapped under one lock in `CareerPassiveService.cs:38-84`; `All` intersects both hit bits at `CareerPassiveService.cs:133-138`.
4. **DISPUTED.** Ammo wiring is bounded and hero-scoped. `OnAgentBuild` exits on non-heroes and uses the spawned hero id (`CareerPerkMissionBehavior.cs:102-119`). Engine calls `OnAgentBuild` after `BuildAgent`/equipment setup (`Mission.cs:4337-4348`). `MissionWeapon.ModifiedMaxAmount`, `Amount`, and `IsAnyAmmo` are the relevant slot APIs (`MissionWeapon.cs:114-132`, `408-415`).
5. **DISPUTED.** Stealth direction is correct. TAOM lowers the ratio at `TaomMapVisibilityModel.cs:28-32`; vanilla visibility checks `distance / seeingRange <= ratio`, so lower ratio means the party must be closer to be visible.
6. **DISPUTED.** HorseHealth reaches mounts. Vanilla initializes non-human agent health through `GetEffectiveMaxHealth` (`SandboxAgentStatCalculateModel.cs:84-87`) and mount health uses the rider branch (`SandboxAgentStatCalculateModel.cs:757-779`). TAOM gates hero vs mount separately at `TaomAgentStatCalculateModel.cs:38-48`.
7. **DISPUTED.** Retune scope is clean. `retune_phantom_passives.py:28-33,58-74` only targets the six phantom types; old/new XML comparison found 1,260 passives both sides and zero non-phantom passive changes. Dry-run now reports `0` remaining magnitude rewrites. `Blunt`/`Cut` degrade to `All` through `ParseEnum` fallback (`CareerConfigProvider.cs:493-497`) and are tested.
8. **DISPUTED.** Consumer set is complete for shipped XML. `PassiveEffectConsumers.cs:20-55` covers every passive type currently used by `taom_career_choices.xml`; grep confirmed matching runtime consumers in `CareerAgentStatService`, `CareerPerkMissionBehavior`, `TaomMapVisibilityModel`, `TaomPartyHealingModel`, cultural-feat models, wage model, and `SpecialResourceService`.

## FINDINGS

None.

## FALSE-POSITIVE SELF-CHECK

- Considered flagging the 18 skipped keystone descriptions from `retune_phantom_descriptions.py`; disputed as pre-existing keystone convention — they describe ability-mutation text and the script correctly did not rewrite those numbers.
- Considered flagging `operation` / `is_percentage` still present in XML; disputed — the C# fields were removed and the parser now intentionally ignores those attributes (`CareerConfigProvider.cs:240-242`).
- Test run was attempted (`dotnet test ... --filter FullyQualifiedName~CareerSystem`) but MSBuild is sandbox-blocked probing `C:\Users\mikew\AppData\Local\Microsoft SDKs`. (TAOM-side: full suite green except 9 pre-existing, unrelated Dol Guldur fails.)

---

**CRITICAL: 0 | HIGH: 0 | MEDIUM: 0 | LOW: 0 — VERDICT: CLEAN**

Assessment (orchestrator): all 8 suspects independently verified; the suspect-1 stage-move was re-verified against `AgentApplyDamageModel.cs:15-18` and is a documented, acceptable consequence of honoring `attack_type_mask`, not a defect (see RCA "Codex adversarial pass"). No fixes required.
