namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// MCM live values (<c>TaomSettings.Instance</c>) over the config default. TaomSettings.Instance is null
/// early in startup and when MCM fails to load, so every read falls back safely (the
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
}
