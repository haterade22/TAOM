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

    public bool IsOnCooldown => CooldownRemaining > 0f;
    public bool IsReady => ChargeType == ChargeType.CooldownOnly
        ? !IsOnCooldown
        : CurrentCharge >= MaxCharge;

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
    }

    public void SetMaxCharge(float newMax)
    {
        MaxCharge = newMax;
        if (CurrentCharge > MaxCharge)
            CurrentCharge = MaxCharge;
    }
}
