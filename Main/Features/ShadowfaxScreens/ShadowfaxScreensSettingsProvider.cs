using System;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Features.ShadowfaxScreens;

/// <summary>
/// Reads the Screen Performance group of the Battle Load Diagnostics page. The reference is cached on its first non-null
/// read and read THROUGH, never snapshotted (the NameplateCull contract): MCM edits its one registered instance in place,
/// so a live edit applies to the next screen open. With no MCM instance, or a read that throws, each getter returns its
/// shipped default: the four fast-open switches on, the timer off.
/// </summary>
public sealed class ShadowfaxScreensSettingsProvider : IShadowfaxScreensSettingsProvider
{
    private BattleLoadDiagnosticsSettings? _settings;

    public ShadowfaxScreensSettingsProvider()
    {
    }

    internal ShadowfaxScreensSettingsProvider(BattleLoadDiagnosticsSettings settings) => _settings = settings;

    private BattleLoadDiagnosticsSettings? Settings => _settings ??= BattleLoadDiagnosticsSettings.Instance;

    public bool LogScreenOpenTimes => Read(s => s.LogScreenOpenTimes, false);

    public bool ClanFastOpen => Read(s => s.ClanFastOpen, true);

    public bool InventoryFastOpen => Read(s => s.InventoryFastOpen, true);

    public bool PartyFastOpen => Read(s => s.PartyFastOpen, true);

    public bool QuestLogFastOpen => Read(s => s.QuestLogFastOpen, true);

    private bool Read(Func<BattleLoadDiagnosticsSettings, bool> getter, bool fallback)
    {
        try
        {
            var settings = Settings;
            return settings == null ? fallback : getter(settings);
        }
        catch
        {
            return fallback;
        }
    }
}
