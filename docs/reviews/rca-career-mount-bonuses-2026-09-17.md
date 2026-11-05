# RCA: career mount bonuses never reached the mount (#611), 2026-09-17

Ported to the 1.4.x line by plan 129 (port148 programme, 2026-11). On the 1.5.x line this fix is
recorded as a follow-up section of the cavalry charge RCA, which covers #610, a change this line
does not carry; this file keeps the #611 part on its own.

## What was wrong

`CareerAgentStatService` multiplied `MountChargeDamage` and `MountSpeed` on the rider's driven
properties at three sites: the `MountChargeDamage` passive, the Cavalry ability's self buff and its
ally buff, all behind `if (!isHuman) return;`. No base model writes either value on a human
(v1.4.8 `SandboxAgentStatCalculateModel.UpdateHorseStats` writes them at :1216, :1221 and :1230),
so the rider's copies stayed 0, and 0 times (1 + bonus) is 0. The engine reads the attacker's copy
(`AttackInformation.cs:335`), and the charge callback's attacker is the horse
(`Mission.ChargeDamageCallback`, :6034). The 92 `MountChargeDamage` pips of the 1.4.x data and the
mount half of the Cavalry ability never did anything, while `PassiveEffectConsumers` listed the
type as consumed.

## Fix

`ICareerAgentStatService.ApplyMountStatModifiers(riderHeroId, riderAgentIndex, mountProps)` applies
the passive and both buff sources on the mount's properties. `TaomAgentStatCalculateModel.UpdateAgentStats`
reads the rider off a mount and calls it after base has rewritten both values. The buff apply and
restore paths and the fallen-hero ally loop refresh `MountAgent` beside the human.

## Why missed

The consumer set answers "is anything reading it", not "is it read where the engine looks": the
blind spot `PassiveEffectConsumers.cs` names, after #394 and #395.

## Deferred, by decision

The bonus keys on the horse's current rider when the engine recomputes the horse, as vanilla's own
Riding skill bonuses on the horse do, so a rider who dies or changes horse during a buff leaves the
first horse on its last numbers until the engine recomputes it. Snapshotting the horse would mean
holding agent handles across frames (the #592 class).

## Verification

`CareerAgentStatServiceTests` (each source on the mount, stacking, no rider, a non-hero rider, a
hero rider without an index, a negative bonus, the human path leaving both mount values alone) and
`CareerMountBonusBindingTests` (IL pins). The in-game check is owed (plan 129).
