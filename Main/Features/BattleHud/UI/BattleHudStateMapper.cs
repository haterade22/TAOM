using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Features.BattleHud.UI;

/// <summary>
/// Issue #1095: pure mapping from plain agent values and the career ability to the circular
/// battle HUD. Free of TaleWorlds types so the optional-channel rules are unit-testable; the
/// mixin reads the main agent and its mount at the boundary and hands the numbers in.
/// Health is always channel slot 0. Horse and shield channels exist only while the equipment
/// does and pack inward (horse before shield), so a missing one leaves no empty ring.
/// </summary>
public static class BattleHudStateMapper
{
    /// <summary>The ring channels plus the centre. A null <paramref name="ability"/> (custom battle, no
    /// career) gives the neutral centre; the rings never depend on it.</summary>
    public static BattleHudState Map(BattleHudInput input, CareerAbility? ability, string? activationKey)
    {
        var state = default(BattleHudState);
        var hasHorse = input.HasHorse && input.HorseMax > 0f;
        var hasShield = input.HasShield && input.ShieldMax > 0f;
        state.RingCount = BattleHudGeometry.PackSlots(hasHorse, hasShield, out var horseSlot, out var shieldSlot);

        state.Health = new BattleHudChannel(true, Ratio(input.Health, input.HealthMax), 0);
        state.Horse = hasHorse
            ? new BattleHudChannel(true, Ratio(input.Horse, input.HorseMax), horseSlot)
            : BattleHudChannel.Absent;
        state.Shield = hasShield
            ? new BattleHudChannel(true, Ratio(input.Shield, input.ShieldMax), shieldSlot)
            : BattleHudChannel.Absent;
        state.Centre = ability != null
            ? MapCentre(true, CareerEnergyBarStateMapper.Map(ability), activationKey)
            : default;
        return state;
    }

    // Split out so the centre keeps CareerEnergyBarStateMapper's active, ready and cooldown
    // semantics verbatim; without an ability the centre is a neutral dark disk with no key.
    public static BattleHudCentre MapCentre(bool hasAbility, CareerEnergyBarState bar, string? activationKey)
    {
        if (!hasAbility) return default;

        return new BattleHudCentre
        {
            HasAbility = true,
            IsReady = bar.IsReady,
            IsActive = bar.IsActive,
            IsCooldown = bar.IsCooldown,
            Fill01 = bar.Fill01,
            ShowKey = !string.IsNullOrEmpty(activationKey),
        };
    }

    // Positive-requirement form: a NaN or non-positive max, or a NaN value, lands at 0.
    private static float Ratio(float value, float max)
    {
        if (!(max > 0f)) return 0f;
        var r = value / max;
        if (!(r > 0f)) return 0f;
        return r > 1f ? 1f : r;
    }
}

public readonly struct BattleHudInput
{
    public BattleHudInput(float health, float healthMax, bool hasHorse, float horse, float horseMax,
        bool hasShield, float shield, float shieldMax)
    {
        Health = health;
        HealthMax = healthMax;
        HasHorse = hasHorse;
        Horse = horse;
        HorseMax = horseMax;
        HasShield = hasShield;
        Shield = shield;
        ShieldMax = shieldMax;
    }

    public float Health { get; }
    public float HealthMax { get; }
    public bool HasHorse { get; }
    public float Horse { get; }
    public float HorseMax { get; }
    public bool HasShield { get; }
    public float Shield { get; }
    public float ShieldMax { get; }
}

public readonly struct BattleHudChannel
{
    public static readonly BattleHudChannel Absent = new BattleHudChannel(false, 0f, -1);

    public BattleHudChannel(bool isVisible, float fill01, int slot)
    {
        IsVisible = isVisible;
        Fill01 = fill01;
        Slot = slot;
    }

    public bool IsVisible { get; }
    public float Fill01 { get; }
    public int Slot { get; }
}

public struct BattleHudCentre
{
    public bool HasAbility;
    public bool IsReady;
    public bool IsActive;
    public bool IsCooldown;
    public float Fill01;
    public bool ShowKey;
}

public struct BattleHudState
{
    public BattleHudChannel Health;
    public BattleHudChannel Horse;
    public BattleHudChannel Shield;
    public BattleHudCentre Centre;
    public int RingCount;
}
