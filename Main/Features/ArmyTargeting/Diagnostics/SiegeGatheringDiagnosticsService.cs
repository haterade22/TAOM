using System.Collections.Concurrent;
using System.Globalization;
using TAOM.Core.Logging;

namespace TAOM.Features.ArmyTargeting.Diagnostics;

/// <summary>
/// Turns the Patch49 gathering dead end into a reviewable to-fix list: classifies each failure,
/// dedups by <c>(kingdom, focus settlement)</c>, and logs one detailed WARNING per distinct siege
/// (repeats are counted and dropped to DEBUG). Pure <see cref="Classify"/> + <see cref="Format"/>
/// keep it fully unit-testable; the only side effect is <see cref="IModLogger"/> output.
/// </summary>
public sealed class SiegeGatheringDiagnosticsService : ISiegeGatheringDiagnosticsService
{
    private const string Tag = "[SiegeDiag]";
    private readonly IModLogger _logger;
    private readonly ConcurrentDictionary<string, int> _seen = new();

    public SiegeGatheringDiagnosticsService(IModLogger logger)
    {
        _logger = logger;
    }

    public void Record(SiegeGatheringFailureInfo info)
    {
        if (info == null) return;

        var reason = Classify(info);
        string key = (info.KingdomId ?? SiegeGatheringFailureInfo.Unknown) + "|" +
                     (info.FocusSettlementId ?? SiegeGatheringFailureInfo.Unknown);

        int count = _seen.AddOrUpdate(key, 1, (_, v) => v + 1);

        if (count == 1)
            _logger.LogWarning(Format(info, reason));
        else
            _logger.LogDebug($"{Tag} repeat ({count}x): {key} (reason={reason})");
    }

    public SiegeGatheringFailureReason Classify(SiegeGatheringFailureInfo info)
    {
        if (info == null) return SiegeGatheringFailureReason.Unknown;
        if (info.KingdomIsNull) return SiegeGatheringFailureReason.KingdomNull;
        if (info.FortificationsTotal < 0) return SiegeGatheringFailureReason.Unknown;
        if (info.FortificationsTotal == 0) return SiegeGatheringFailureReason.NoFortifications;
        if (info.FortificationsUnderSiege >= info.FortificationsTotal)
            return SiegeGatheringFailureReason.AllFortificationsUnderSiege;
        return SiegeGatheringFailureReason.NoReachableFortification;
    }

    public string Format(SiegeGatheringFailureInfo info, SiegeGatheringFailureReason reason)
    {
        string army = Str(info.ArmyName);
        string leader = Str(info.LeaderName);
        string clan = Str(info.ClanId);
        string kingdomName = Str(info.KingdomName);
        string kingdomId = Str(info.KingdomId);
        string focusName = Str(info.FocusSettlementName);
        string focusId = Str(info.FocusSettlementId);
        string culture = Str(info.FocusCultureId);
        string faction = Str(info.FocusFactionId);

        return $"{Tag} Army '{army}' (leader {leader}, clan {clan}, kingdom {kingdomName} [{kingdomId}]) " +
               $"could not resolve a gathering fortification for focus '{focusName}' " +
               $"[{focusId}, culture {culture}, faction {faction}]. " +
               $"Reason={reason}. Fortifications: {Count(info.FortificationsTotal)} total, " +
               $"{Count(info.FortificationsUnderSiege)} under siege. " +
               $"Leader@({Pos(info.LeaderPartyX)},{Pos(info.LeaderPartyY)}) " +
               $"focus@({Pos(info.FocusX)},{Pos(info.FocusY)}). Time={Str(info.CampaignTimeText)}.";
    }

    private static string Str(string value) =>
        string.IsNullOrEmpty(value) ? SiegeGatheringFailureInfo.Unknown : value;

    private static string Count(int value) =>
        value < 0 ? "n/a" : value.ToString(CultureInfo.InvariantCulture);

    private static string Pos(float value) =>
        float.IsNaN(value) ? "?" : value.ToString("F1", CultureInfo.InvariantCulture);
}
