using System.Globalization;
using TAOM.Core.Logging;
using TAOM.Features.BlowDiagnostics.Domain;

namespace TAOM.Features.BlowDiagnostics;

// Formats + emits the [BlowDiag] stamps. Uses IModLogger's DURABLE levels (LogInfo) so a stamp
// survives the native AV it exists to catch — a native access violation kills the process
// without unwinding, and DEBUG lines still queued async would be lost (that is exactly why the
// plain debug log lost the final 20s of the crash this feature was built for).
//
// Every emit is wrapped so the diagnostic can NEVER turn a blow into a crash of its own.
public sealed class BlowDiagnosticService : IBlowDiagnosticService
{
    private const string Tag = "[BlowDiag]";

    private readonly IModLogger _logger;
    private readonly IBlowDiagnosticsSettingsProvider _settings;

    public BlowDiagnosticService(IModLogger logger, IBlowDiagnosticsSettingsProvider settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public bool IsEnabled => _settings.IsEnabled;

    public void LogBlow(BlowDiagRecord record) => Emit("blow", record);

    public void LogDeath(BlowDiagRecord record) => Emit("DIE", record);

    public void LogSiegeShot(string missileItemId, string side)
    {
        if (!IsEnabled) return;
        try
        {
            _logger.LogInfo($"{Tag} siege-shot item='{missileItemId ?? "<null>"}' side={side ?? "?"}");
        }
        catch { /* the diagnostic must never propagate */ }
    }

    private void Emit(string kind, BlowDiagRecord r)
    {
        if (!IsEnabled || r == null) return;
        try
        {
            _logger.LogInfo(Format(kind, r));
        }
        catch { /* the diagnostic must never propagate */ }
    }

    private static string Format(string kind, BlowDiagRecord r)
    {
        var ci = CultureInfo.InvariantCulture;
        string mount = r.VictimIsMounted ? $" mount='{r.MountMonster}'" : "";
        return string.Format(ci,
            "{0} {1} victim='{2}' race={3} player={4} mounted={5}{6} hp={7:0.#} " +
            "flags={8} dmgType={9} dmg={10} mag={11:0.#} missile={12} fall={13} part={14} attackerIdx={15}",
            Tag, kind, r.VictimName, r.VictimRace, r.VictimIsPlayer, r.VictimIsMounted, mount,
            r.VictimHealth, r.BlowFlags, r.DamageType, r.InflictedDamage, r.BaseMagnitude,
            r.IsMissile, r.IsFallDamage, r.VictimBodyPart, r.AttackerIndex);
    }
}
