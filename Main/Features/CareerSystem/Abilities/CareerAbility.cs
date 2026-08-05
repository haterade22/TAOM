using System;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Abilities;

public class CareerAbility
{
    public string TemplateId { get; }
    public ChargeType ChargeType { get; }
    public float MaxCharge { get; private set; }
    public float CurrentCharge { get; private set; }
    public float CooldownDuration { get; }
    public float CooldownRemaining { get; private set; }

    // Issue #377 — the active window (how long the cast buff is still live). Set by
    // BeginActiveWindow with the SAME (possibly mutation-extended) duration that schedules
    // the buff restores, so the HUD and the buff expire on the same tick. Runs alongside
    // the cooldown, which starts at Activate() and does not pause during the window.
    public float ActiveDuration { get; private set; }
    public float ActiveRemaining { get; private set; }

    public bool IsActive => ActiveRemaining > 0f;
    public float ActiveProgress01 => ActiveDuration > 0f ? ActiveRemaining / ActiveDuration : 0f;

    public bool IsOnCooldown => CooldownRemaining > 0f;
    public bool IsReady => ChargeType == ChargeType.CooldownOnly
        ? !IsOnCooldown
        : CurrentCharge >= MaxCharge;

    public float ReadyProgress01 => ChargeType == ChargeType.CooldownOnly
        ? (CooldownDuration > 0f ? (CooldownDuration - CooldownRemaining) / CooldownDuration : 1f)
        : (MaxCharge > 0f ? CurrentCharge / MaxCharge : 0f);

    public CareerAbility(string templateId, ChargeType chargeType, float maxCharge, float cooldownDuration)
    {
        TemplateId = templateId;
        ChargeType = chargeType;
        MaxCharge = maxCharge;
        CooldownDuration = cooldownDuration;
    }

    public void AddCharge(float amount, ChargeType sourceType)
    {
        if (ChargeType == ChargeType.CooldownOnly) return;
        if (ChargeType != ChargeType.Custom && ChargeType != sourceType) return;

        CurrentCharge = Math.Min(CurrentCharge + amount, MaxCharge);
    }

    public void Activate()
    {
        if (ChargeType == ChargeType.CooldownOnly)
            CooldownRemaining = CooldownDuration;
        else
            CurrentCharge = 0f;
    }

    public void Tick(float dt)
    {
        if (CooldownRemaining > 0f)
            CooldownRemaining = Math.Max(0f, CooldownRemaining - dt);
        if (ActiveRemaining > 0f)
            ActiveRemaining = Math.Max(0f, ActiveRemaining - dt);
    }

    // Issue #377 — start the active window. Guards mirror AdjustCooldown: a non-finite or
    // non-positive duration (poisoned config) must not produce a stuck "always active" state
    // (NaN-gate rule, csharp-architecture.md — positive requirement, NaN fails it).
    public void BeginActiveWindow(float durationSeconds)
    {
        if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds)) return;
        if (!(durationSeconds > 0f)) return;

        ActiveDuration = durationSeconds;
        ActiveRemaining = durationSeconds;
    }

    public void SetMaxCharge(float newMax)
    {
        MaxCharge = newMax;
        if (CurrentCharge > MaxCharge)
            CurrentCharge = MaxCharge;
    }

    // Issue #104 Option B — shorten the active CooldownRemaining by reductionSeconds (clamped
    // to floor minCooldownSeconds). No-op for charge-based abilities (the cooldown machinery
    // doesn't run). NaN/Infinity/negative inputs are rejected so a bad XML mutation cannot
    // produce a frozen state machine or a 0s cooldown bypass (see feedback_clamp_nan_infinity_propagates.md).
    public void AdjustCooldown(float reductionSeconds, float minCooldownSeconds)
    {
        if (ChargeType != ChargeType.CooldownOnly) return;
        if (float.IsNaN(reductionSeconds) || float.IsNaN(minCooldownSeconds)) return;
        if (float.IsInfinity(reductionSeconds) || float.IsInfinity(minCooldownSeconds)) return;
        if (reductionSeconds <= 0f) return;
        if (minCooldownSeconds < 0f) minCooldownSeconds = 0f;

        var effective = Math.Max(minCooldownSeconds, CooldownRemaining - reductionSeconds);
        CooldownRemaining = effective;
    }
}
