using System;
using System.Collections.Generic;
using System.Reflection;

namespace TAOM.Features.CoopInterop;

/// <summary>
/// Which MCM settings can make two co-op peers simulate differently.
/// </summary>
/// <remarks>
/// TAOM's settings live in a per-user file outside the save and are read live by the feature
/// providers. No co-op mod syncs them, so two peers holding different values compute different
/// outcomes — battle autocalc, bandit density, AI army targeting, desertion — with nothing said.
/// <see cref="SettingsFingerprint"/> exists to say it; this type decides what it looks at.
///
/// <para><b>Include by default.</b> A property is relevant unless it is named here. A new
/// gameplay knob is therefore covered the day it is added, which is the safe direction: the
/// cost of a missing exclusion is one spurious mismatch line, the cost of a missing inclusion
/// is a silent divergence, and only the second one corrupts a campaign.</para>
///
/// <para><b>Three reasons to exclude, and no others.</b> Instrumentation changes what is
/// written to a log, never what is computed. Player-local convenience is invoked BY a player —
/// the co-op layer replicates the result of the click, so two thresholds do not diverge two
/// simulations. Presentation is pixels. Anything that survives those three tests stays in,
/// including knobs that look cosmetic: a nameplate is presentation, a party-size divisor is
/// not, and the boundary is whether a peer's campaign or battle math reads it.</para>
///
/// <para>Derived by tracing every setting to the feature that consumes it and asking whether
/// that feature ships a GameModel, a CampaignBehavior, a MissionBehavior or a Harmony patch.
/// The nine <c>*Debug</c> / <c>*Diagnostics</c> entries below sit inside gameplay groups —
/// <c>SmartCavalryDebug</c> is filed under Battle Tactics — and would otherwise be counted as
/// simulation because their feature computes. They gate log lines.</para>
/// </remarks>
public static class CoopSettingsRelevance
{
    /// <summary>MCM base-class members: settings identity, not settings.</summary>
    private static readonly HashSet<string> Infrastructure = new HashSet<string>(StringComparer.Ordinal)
    {
        "Id", "DisplayName", "FolderName", "FormatType", "SubFolder", "SubGroupDelimiter",
    };

    /// <summary>Instrumentation — gates a log line, never a computation.</summary>
    private static readonly HashSet<string> Instrumentation = new HashSet<string>(StringComparer.Ordinal)
    {
        "BattleActionBarDebug", "CompanionRolesDebug", "EnableSiegePropDiagnostics",
        "FormationPresetsDebug", "MixedFormationsDebug", "SiegeDismountDebug",
        "SiegePropDiagnosticsVerbose", "SmartCavalryDebug", "FiefManagementDebug",
        "EquipPresetsDebug", "QuickActionsDebug",
        "EnableBattleLoadDiagnostics", "EnableBlowDiagnostics", "EnableCrashCapture",
        "EnableNativeToManagedCapture", "SuspendButterLibHandler", "WriteCrashBundle",
        "EnableMemorySampler", "MemorySampleIntervalSeconds",
        "EnableStallWatchdog", "EnableStallWatchdogBundle", "StallWatchdogSeconds",
        "EnableExitStallSampler", "ThrowOnNextApplicationTick", "ThrowOnNextMissionTick",
    };

    /// <summary>Player-local convenience: the player acts, the co-op layer replicates the result.</summary>
    private static readonly HashSet<string> PlayerLocal = new HashSet<string>(StringComparer.Ordinal)
    {
        "EnableQuickActions", "EnableInventorySearch", "QuickActionsShowConfirmation",
        "QuickActionsPlaySounds", "DamagedQualityDropdown", "DamagedThreshold",
        "UseCustomThreshold", "SellDamagedEquipped", "ExcludeDamagedHorses",
        "LowValueThreshold", "SellLowValueEquipped", "ExcludeLowValueFood",
        "ExcludeLowValueHorses", "ExcludeLowValueTradeGoods",
        "EnableEquipmentPresets", "MaxPresetsPerCharacter",
    };

    /// <summary>Presentation, and the clock BT already owns.</summary>
    private static readonly HashSet<string> Presentation = new HashSet<string>(StringComparer.Ordinal)
    {
        "ShowAllEncyclopediaCharacters", "EnableNameplateFade", "MapFigureScale",
        "NameplateFadeFarDistance", "NameplateFadeNearDistance",
        "EnableScenePassPrecompilation", "EnableShaderPrecompilation",
        "EnableNativeSkinFixes",
        // Already suppressed under co-op — TimeAcceleration's UI carries
        // [CoopSuppressedUi("BannerlordTogether owns campaign time under co-op")].
        "FastForwardMultiplier", "ExtraFastForwardMultiplier", "CtrlSpaceMultiplier",
    };

    /// <summary>True when a difference in this property can make two peers simulate differently.</summary>
    public static bool IsSimulationRelevant(PropertyInfo property)
    {
        if (property == null) return false;
        // A setting round-trips through the MCM file; a computed member does not.
        if (!property.CanRead || !property.CanWrite) return false;
        if (property.GetIndexParameters().Length != 0) return false;
        return !IsExcluded(property.Name);
    }

    /// <summary>True when the name is on one of the three exclusion lists.</summary>
    public static bool IsExcluded(string propertyName) =>
        propertyName != null
        && (Infrastructure.Contains(propertyName)
            || Instrumentation.Contains(propertyName)
            || PlayerLocal.Contains(propertyName)
            || Presentation.Contains(propertyName));

    /// <summary>Every excluded name, for the coverage test and for the docs table.</summary>
    public static IEnumerable<string> ExcludedNames()
    {
        foreach (var n in Infrastructure) yield return n;
        foreach (var n in Instrumentation) yield return n;
        foreach (var n in PlayerLocal) yield return n;
        foreach (var n in Presentation) yield return n;
    }
}
