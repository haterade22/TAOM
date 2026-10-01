using System;
using System.Globalization;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.BattleCorpses;

/// <summary>
/// Decides the per-battle corpse limits (#701). What native does with them, read from the v1.5.3
/// <c>TaleWorlds.Native.dll</c> (docs/features/battle-corpses.md):
/// <list type="bullet">
/// <item>The fade time removes every body older than it, each mission tick. Its default is
/// 3,600 s, so in a normal battle no body ever times out.</item>
/// <item>The corpse override is a raw count that REPLACES the player's own cap, so a value above
/// the player's would raise it; <see cref="Resolve"/> takes the lower of the two.</item>
/// <item>Native's mission reset restores both defaults, so they are set again for every battle.</item>
/// </list>
/// </summary>
public sealed class BattleCorpsePolicy
{
    public const float DefaultFadeSeconds = 60f;
    public const float MinFadeSeconds = 10f;
    public const float MaxFadeSeconds = 300f;
    public const int DefaultCorpseCap = 25;
    public const int MinCorpseCap = 0;
    public const int MaxCorpseCap = 250;
    public const int UnknownCap = -1;

    // Native's own table for the Number of Corpses option (the corpse limiter, v1.5.3). Unlimited is 1021.
    private static readonly int[] CapsByOption = { 0, 25, 75, 125, 250, 1021 };

    private readonly IBattleCorpseSettingsProvider _settings;
    private readonly IModLogger _logger;
    private bool _fadeWarned;
    private bool _capWarned;

    public BattleCorpsePolicy(IBattleCorpseSettingsProvider settings, IModLogger logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>The body count native allows for a Number of Corpses option, or <see cref="UnknownCap"/>.</summary>
    public static int CorpseCapForOption(int option) =>
        option >= 0 && option < CapsByOption.Length ? CapsByOption[option] : UnknownCap;

    public BattleCorpseLimits Resolve(int playerCorpseOption)
    {
        if (!_settings.IsCleanupEnabled)
            return BattleCorpseLimits.EngineDefaults;

        var cap = ValidCap();
        var playerCap = CorpseCapForOption(playerCorpseOption);
        if (playerCap != UnknownCap && playerCap < cap)
            cap = playerCap;

        return new BattleCorpseLimits(true, ValidFadeSeconds(), cap);
    }

    public static string DescribeLine(int ragdollOption, int corpseOption, int battleSizeOption, BattleCorpseLimits limits)
    {
        var head = string.Format(CultureInfo.InvariantCulture,
            "[BattleSettings] ragdollOption={0} corpseOption={1} battleSizeOption={2} taomOverride=",
            ragdollOption, corpseOption, battleSizeOption);
        return limits.IsOverridden
            ? head + string.Format(CultureInfo.InvariantCulture, "on fadeSeconds={0:0} corpseCap={1}", limits.FadeSeconds, limits.CorpseCap)
            : head + "off";
    }

    private float ValidFadeSeconds()
    {
        var raw = _settings.FadeSeconds;
        if (FiniteFloatValidator.IsFiniteInRange(raw, MinFadeSeconds, MaxFadeSeconds))
        {
            _fadeWarned = false;
            return raw;
        }

        if (!_fadeWarned)
        {
            _fadeWarned = true;
            _logger.LogWarning($"[BattleCorpses] corpse fade time {raw} is outside {MinFadeSeconds}-{MaxFadeSeconds} s; using {DefaultFadeSeconds}");
        }
        return DefaultFadeSeconds;
    }

    private int ValidCap()
    {
        var raw = _settings.CorpseCap;
        if (raw >= MinCorpseCap && raw <= MaxCorpseCap)
        {
            _capWarned = false;
            return raw;
        }

        if (!_capWarned)
        {
            _capWarned = true;
            _logger.LogWarning($"[BattleCorpses] corpse cap {raw} is outside {MinCorpseCap}-{MaxCorpseCap}; using {DefaultCorpseCap}");
        }
        return DefaultCorpseCap;
    }
}
