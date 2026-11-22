namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// MCM live values (<c>TaomSettings.Instance</c>) over the config default. TaomSettings.Instance is null
/// in tests, before MCM initializes, or on a client whose MCM failed to load (a dedicated server always gets
/// the instance built from <c>taom-server-settings.json</c>), so every read falls back safely (the
/// EliteEmissarySettingsProvider pattern). The master switch is read once per game init, by
/// <see cref="IArmourGateService.ApplyGating"/>; every consumer of the two sub-toggles runs only while that
/// game's gate is active, so they need not fold the live master in (turning the master off mid-game then
/// changes nothing until the next load, as its hint says).
/// </summary>
public sealed class ArmourAcquisitionSettingsProvider : IArmourAcquisitionSettingsProvider
{
    private readonly IArmourAcquisitionConfigProvider _config;

    public ArmourAcquisitionSettingsProvider(IArmourAcquisitionConfigProvider config)
    {
        _config = config;
    }

    public bool IsEnabled => TaomSettings.Instance?.EnableArmourAcquisition ?? _config.GetConfig().Enabled;

    public bool LordEventEnabled => TaomSettings.Instance?.EnableArmourLordEvent ?? true;

    public bool VisitingArmourerEnabled => TaomSettings.Instance?.EnableVisitingArmourer ?? true;

    /// <summary>The default of "Lords' Ladder for co-op players" (TaomSettings.CoopGuestLadder).</summary>
    public const bool DefaultCoopGuestLadder = true;

    public bool CoopGuestLadderEnabled => TaomSettings.Instance?.CoopGuestLadder ?? DefaultCoopGuestLadder;
}
